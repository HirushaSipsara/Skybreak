using UnityEngine;
using Skybreak.Warden.Core;

namespace Skybreak.Warden
{
    // Plays authored skeletal clips on the visual child. CharacterController owns translation.
    [DefaultExecutionOrder(100)]
    public sealed class WardenAnimation : MonoBehaviour
    {
        public WardenBrain brain;
        public Transform visual;
        public float crossFadeSeconds = .16f;
        public bool Ready { get; private set; }
        public MotionClip Current { get; private set; }
        Animation player;
        Vector3 lastPosition;
        float lastYaw, speed, turnRate, lastHealth, hitUntil, recoveryUntil;
        bool initialized, hasClip;

        public void Initialize()
        {
            if (initialized || brain == null || visual == null) return;
            initialized = true;
            lastPosition = transform.position;
            lastYaw = transform.eulerAngles.y;
            lastHealth = brain.Health;
            player = visual.GetComponent<Animation>();
            if (player == null) player = visual.gameObject.AddComponent<Animation>();
            player.playAutomatically = false;
            player.cullingType = AnimationCullingType.AlwaysAnimate;
            player.Stop();
            foreach (var clip in Resources.LoadAll<AnimationClip>("CrystalEchoWarden/CrystalEchoWarden"))
            {
                if (!clip.legacy || !MotionChoice.TryName(clip.name, out var kind)) continue;
                string key = kind.ToString();
                player.AddClip(clip, key);
                player[key].wrapMode = MotionChoice.Loops(kind) ? WrapMode.Loop : WrapMode.ClampForever;
            }
            Ready = true;
            foreach (MotionClip kind in System.Enum.GetValues(typeof(MotionClip)))
                if (player[kind.ToString()] == null) Ready = false;
            if (!Ready)
            {
                Debug.LogError("Warden requires all eight legacy skeletal clips. Reimport its FBX with WardenAnimationImporter enabled.", this);
                return;
            }
            brain.Fired += OnShot;
            SetClip(MotionClip.Idle);
        }

        void Start() { Initialize(); }
        void OnShot(Vector3 from, Vector3 to) { recoveryUntil = Time.time + Mathf.Max(.12f, brain.windupSeconds * .5f); }

        void LateUpdate()
        {
            if (!Ready || brain == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            Vector3 delta = transform.position - lastPosition;
            delta.y = 0;
            float measured = delta.magnitude < 2 ? delta.magnitude / dt : 0; // Reset/teleport is not locomotion.
            float yaw = transform.eulerAngles.y;
            float measuredTurn = Mathf.Clamp(Mathf.DeltaAngle(lastYaw, yaw) / dt, -360, 360);
            float smooth = 1 - Mathf.Exp(-12 * dt);
            speed = Mathf.Lerp(speed, measured, smooth);
            turnRate = Mathf.Lerp(turnRate, measuredTurn, smooth);
            lastPosition = transform.position;
            lastYaw = yaw;
            if (brain.Health < lastHealth && brain.Alive) hitUntil = Time.time + .3f;
            lastHealth = brain.Health;
            bool attack = brain.WindingUp || Time.time < recoveryUntil;
            var chosen = MotionChoice.Choose(brain.Alive, attack, Time.time < hitUntil,
                brain.Mind != null && brain.Mind.Current == Intent.Retreat, speed, turnRate);
            SetClip(chosen);
            var state = player[Current.ToString()];
            if (Current == MotionClip.Attack) state.speed = .6f / Mathf.Max(.05f, brain.windupSeconds);
            else if (Current == MotionClip.Glide || Current == MotionClip.Retreat)
                state.speed = Mathf.Clamp(speed / Mathf.Max(.1f, brain.Motor.speed), .6f, 1.5f);
            else state.speed = 1;
        }

        void SetClip(MotionClip clip)
        {
            if (hasClip && Current == clip) return;
            Current = clip;
            string key = clip.ToString();
            player[key].time = 0;
            if (hasClip) player.CrossFade(key, Mathf.Max(.01f, crossFadeSeconds));
            else player.Play(key);
            hasClip = true;
        }

        void OnDisable()
        {
            if (player != null) player.Stop();
            hasClip = false;
            speed = turnRate = hitUntil = recoveryUntil = 0;
        }
        void OnEnable()
        {
            lastPosition = transform.position;
            lastYaw = transform.eulerAngles.y;
            if (brain != null) lastHealth = brain.Health;
        }
        void OnDestroy() { if (brain != null) brain.Fired -= OnShot; }
    }
}
