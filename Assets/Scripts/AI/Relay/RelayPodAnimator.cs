using UnityEngine;

namespace Skybreak.AI.Relay
{
    // Presentation only: reads RelayBrain's public state and turns the side pods on their joints.
    // Runs in LateUpdate, after RelayBrain has placed and turned the visual, and never writes to the brain.
    // Pod angle convention: positive = pod nose tips forward/down around Relay's side axis.
    [DisallowMultipleComponent]
    public sealed class RelayPodAnimator : MonoBehaviour
    {
        public RelayBrain brain;
        public Transform leftPod, rightPod;            // Relay_Pod_L carries the blaster
        public Renderer healEmitter, blaster;

        [Header("Hover")]
        public float idleSwing = 9f;
        public float idleSpeed = 2.5f;                 // matches RelayBrain's hover bob
        public float maxLean = 22f;
        public float leanPerMetrePerSecond = 9f;
        [Range(0, 1)] public float defendSteadiness = 0.7f;

        [Header("Heal: both pods spin a full turn")]
        public float healSpinDuration = 0.75f;
        public float rightPodDelay = 0.08f;

        [Header("Fire: blaster pod recoils")]
        public float recoilAngle = 28f;
        public float recoilDuration = 0.35f;

        [Header("Disabled: pods droop, glow dims")]
        public float droopAngle = 40f;

        [Header("Glow pulses (CyanGlow emission)")]
        public Color glowColour = new Color(0.3f, 1.9f, 2f);
        public float pulseBoost = 3f;

        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        Quaternion leftRest, rightRest;
        Vector3 leftAxis, rightAxis;
        bool cached;
        int lastHeals, lastAttacks;
        float healAt = -100, fireAt = -100, lean, droop, steady = 1;
        Vector3 lastPosition;
        MaterialPropertyBlock block;

        void Awake()
        {
            if (brain == null) brain = GetComponent<RelayBrain>();
            FindParts();
        }

        public void FindParts()
        {
            Transform root = brain != null && brain.visual != null ? brain.visual : transform;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (leftPod == null && t.name == "Relay_Pod_L") leftPod = t;
                else if (rightPod == null && t.name == "Relay_Pod_R") rightPod = t;
                else if (healEmitter == null && t.name == "Relay_HealEmitter") healEmitter = t.GetComponent<Renderer>();
                else if (blaster == null && t.name == "Relay_Blaster") blaster = t.GetComponent<Renderer>();
            }
        }

        void Start()
        {
            CacheRest();
            if (brain != null) { lastHeals = brain.HealsApplied; lastAttacks = brain.AttacksApplied; }
            lastPosition = transform.position;
        }

        void CacheRest()
        {
            if (cached || leftPod == null || rightPod == null) return;
            Vector3 side = (brain != null && brain.visual != null ? brain.visual : transform).right;
            leftRest = leftPod.localRotation;
            rightRest = rightPod.localRotation;
            leftAxis = Quaternion.Inverse(leftPod.rotation) * side;     // the joint axis in each pod's own frame
            rightAxis = Quaternion.Inverse(rightPod.rotation) * side;
            cached = true;
        }

        void LateUpdate()
        {
            if (!cached || brain == null) return;
            float t = Time.time, dt = Mathf.Max(Time.deltaTime, 1e-4f);

            if (brain.HealsApplied != lastHeals) { if (brain.HealsApplied > lastHeals) healAt = t; lastHeals = brain.HealsApplied; }
            if (brain.AttacksApplied != lastAttacks) { if (brain.AttacksApplied > lastAttacks) fireAt = t; lastAttacks = brain.AttacksApplied; }

            Transform heading = brain.visual != null ? brain.visual : transform;
            float forwardSpeed = Vector3.Dot((transform.position - lastPosition) / dt, heading.forward);
            lastPosition = transform.position;
            float k = 1 - Mathf.Exp(-8 * dt);
            lean = Mathf.Lerp(lean, Mathf.Clamp(forwardSpeed * leanPerMetrePerSecond, -maxLean, maxLean), k);
            droop = Mathf.Lerp(droop, brain.Alive ? 0 : droopAngle, 1 - Mathf.Exp(-4 * dt));
            steady = Mathf.Lerp(steady, brain.Action == RelayAction.Defend ? 1 - defendSteadiness : 1, k);

            Pose(t, (t - healAt) / healSpinDuration, (t - fireAt) / recoilDuration, lean, droop, brain.Alive, steady);
        }

        /// <summary>Applies one pod pose. heal/fire are 0..1 progress (outside that range = inactive).
        /// Public so editor tools can preview poses without entering Play mode.</summary>
        public void Pose(float time, float heal, float fire, float leanDegrees, float droopDegrees, bool alive, float steadiness = 1)
        {
            CacheRest();
            if (!cached) return;
            float life = alive ? 1 : 0;
            float swing = idleSwing * steadiness * life;
            float phase = time * idleSpeed;
            float left = swing * Mathf.Sin(phase + 0.6f) + leanDegrees + droopDegrees;
            float right = swing * Mathf.Sin(phase + 0.6f + Mathf.PI) + leanDegrees + droopDegrees;

            left += 360f * EaseOutCubic(heal);
            right += 360f * EaseOutCubic(heal - rightPodDelay / Mathf.Max(healSpinDuration, 1e-3f));

            float kick = fire < 0 || fire > 1 ? 0 : fire < 0.15f ? fire / 0.15f : Sq(1 - (fire - 0.15f) / 0.85f);
            left -= recoilAngle * kick;
            right -= recoilAngle * 0.3f * kick;

            leftPod.localRotation = leftRest * Quaternion.AngleAxis(left, leftAxis);
            rightPod.localRotation = rightRest * Quaternion.AngleAxis(right, rightAxis);

            float dim = alive ? 1 : 0.15f;
            SetGlow(healEmitter, dim * (1 + pulseBoost * Pulse(heal)));
            SetGlow(blaster, dim * (1 + pulseBoost * Pulse(fire)));
        }

        void SetGlow(Renderer r, float strength)
        {
            if (r == null) return;
            block ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(EmissionColor, glowColour * strength);
            r.SetPropertyBlock(block);
        }

        static float EaseOutCubic(float u) => u <= 0 ? 0 : u >= 1 ? 1 : 1 - Sq(1 - u) * (1 - u);
        static float Pulse(float u) => u < 0 || u > 1 ? 0 : Sq(1 - u);
        static float Sq(float x) => x * x;
    }
}
