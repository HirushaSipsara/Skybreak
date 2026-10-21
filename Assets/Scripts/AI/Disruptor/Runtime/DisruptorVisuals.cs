using Skybreak.Movement;
using UnityEngine;

namespace Skybreak.AI.Disruptor
{
    /// <summary>
    /// Disruptor-specific presentation glue: turns brain events into Animator requests and drives the
    /// emissive parts (visor, vents, cannon). It only reads the brain; removing it leaves the AI unchanged.
    ///
    /// Glow uses one material instance per Disruptor, shared by all its glowing sub-meshes, rather than a
    /// MaterialPropertyBlock: property blocks opt renderers out of URP's SRP Batcher.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DisruptorBrain))]
    public sealed class DisruptorVisuals : MonoBehaviour
    {
        public AgentAnimationPresenter presenter;
        public Material glowMaterial;
        [ColorUsage(false, true)] public Color glowColour = new Color(1f, .5f, .1f) * 2.4f;
        public float chargeBoost = 3f, releaseFlash = 6f, flashSeconds = .15f;

        DisruptorBrain brain;
        Material glow;
        float flashUntil;

        void Awake()
        {
            brain = GetComponent<DisruptorBrain>();
            if (presenter == null) presenter = GetComponent<AgentAnimationPresenter>();
            if (glowMaterial == null) return;
            glow = new Material(glowMaterial) { name = glowMaterial.name + " (" + name + ")" };
            glow.EnableKeyword("_EMISSION");
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials; bool changed = false;
                for (int i = 0; i < mats.Length; i++) if (mats[i] == glowMaterial) { mats[i] = glow; changed = true; }
                if (changed) r.sharedMaterials = mats;
            }
        }

        void OnEnable()
        {
            brain.PulseReleased += OnReleased; brain.PulseStaggered += OnStaggered; brain.Damaged += OnDamaged; brain.Defeated += OnDefeated;
        }

        void OnDisable()
        {
            brain.PulseReleased -= OnReleased; brain.PulseStaggered -= OnStaggered; brain.Damaged -= OnDamaged; brain.Defeated -= OnDefeated;
        }

        void OnReleased(DisruptorBrain b) { flashUntil = Time.time + flashSeconds; presenter?.SetCharging(false); presenter?.PlayRelease(); }
        void OnStaggered(DisruptorBrain b) { presenter?.SetCharging(false); presenter?.PlayHurt(); }
        void OnDamaged(DisruptorBrain b, float amount) { if (b.Alive && !b.Pulse.Busy) presenter?.PlayHurt(); }
        void OnDefeated(DisruptorBrain b) { presenter?.SetDead(true); }

        void Update()
        {
            // With the brain switched off (movement lab), leave the Animator to whoever drives it.
            if (presenter != null && brain.Alive && brain.isActiveAndEnabled)
            {
                presenter.SetCharging(brain.Pulse.Busy);
                var s = brain.Senses;
                bool known = s.Player != null && (s.Visible || s.Confidence(Time.time, brain.memoryTau) > 0.3f);
                presenter.lookTarget = known ? (s.Visible ? s.PlayerChest : s.LastKnown + Vector3.up * 1.4f) : (Vector3?)null;
            }
            if (glow == null) return;
            float now = Time.time, intensity;
            if (!brain.Alive) intensity = 0.04f;
            else if (now < flashUntil) intensity = releaseFlash;
            else if (brain.Pulse.Busy)
            {
                float p = brain.Pulse.Progress(now);
                intensity = 1 + chargeBoost * p + 0.6f * p * Mathf.Sin(now * 40);     // brightening, flickering telegraph
            }
            else intensity = 1 + 0.12f * Mathf.Sin(now * 2.2f);                     // idle "breathing"
            glow.SetColor("_EmissionColor", glowColour * intensity);
        }

        void OnDestroy() { if (glow != null) Destroy(glow); }
    }
}
