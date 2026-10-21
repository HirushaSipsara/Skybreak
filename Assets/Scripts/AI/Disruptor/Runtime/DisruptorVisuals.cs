using System.Collections.Generic;
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
        [Min(0)] public float breakupDelay = .4f;
        [Min(1)] public float debrisLifetime = 4f;

        DisruptorBrain brain;
        Material glow;
        float flashUntil;
        float defeatAt = -1, shatteredAt;
        bool shattered;
        MeshRenderer[] parts;
        GameObject debris;
        Light burstLight;
        readonly List<MeshRenderer> hiddenParts = new List<MeshRenderer>();
        readonly List<Collider> disabledColliders = new List<Collider>();
        readonly List<Transform> fragments = new List<Transform>();
        readonly List<Vector3> fragmentScales = new List<Vector3>();

        void Awake()
        {
            brain = GetComponent<DisruptorBrain>();
            if (presenter == null) presenter = GetComponent<AgentAnimationPresenter>();
            parts = GetComponentsInChildren<MeshRenderer>(true);
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
            ResetDefeatFx();
        }

        void OnReleased(DisruptorBrain b) { flashUntil = Time.time + flashSeconds; presenter?.SetCharging(false); presenter?.PlayRelease(); }
        void OnStaggered(DisruptorBrain b) { presenter?.SetCharging(false); presenter?.PlayHurt(); }
        void OnDamaged(DisruptorBrain b, float amount) { if (b.Alive && !b.Pulse.Busy) presenter?.PlayHurt(); }
        void OnDefeated(DisruptorBrain b)
        {
            presenter?.SetDead(true);
            if (defeatAt >= 0) return;
            defeatAt = Time.time;
            foreach (var collider in GetComponentsInChildren<Collider>())
                if (collider.enabled) { disabledColliders.Add(collider); collider.enabled = false; }
        }

        void LateUpdate()
        {
            if (brain.Alive)
            {
                if (defeatAt >= 0) ResetDefeatFx();
                return;
            }
            if (defeatAt < 0) OnDefeated(brain);
            if (!shattered && Time.time - defeatAt >= breakupDelay) BreakApart();
            if (debris == null) return;
            float elapsed = Time.time - shatteredAt;
            if (burstLight != null) burstLight.intensity = 7 * Mathf.Clamp01(1 - elapsed / .3f);
            float scale = Mathf.Clamp01((debrisLifetime - elapsed) / .7f);
            for (int i = 0; i < fragments.Count; i++)
                if (fragments[i] != null) fragments[i].localScale = fragmentScales[i] * scale;
            if (elapsed >= debrisLifetime) ClearDebris();
        }

        void BreakApart()
        {
            shattered = true; shatteredAt = Time.time;
            debris = new GameObject("Rift Disruptor broken pieces (runtime)") { hideFlags = HideFlags.DontSave };
            var actorColliders = new List<Collider>(GetComponentsInChildren<Collider>(true));
            foreach (var actor in Skybreak.Combat.DamageSink.Active)
                if (actor != null) actorColliders.AddRange(actor.GetComponentsInChildren<Collider>(true));
            var pieceColliders = new List<Collider>();
            Vector3 origin = brain.AimPoint;
            foreach (var source in parts)
            {
                if (source == null || !source.enabled || !source.gameObject.activeInHierarchy) continue;
                var filter = source.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var piece = new GameObject(source.name + " fragment") { layer = 8 };
                piece.transform.SetParent(debris.transform, false);
                // Keep the imported joint mesh's pivot and scale; its vertices are already offset from that pivot.
                piece.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                piece.transform.localScale = source.transform.lossyScale;
                piece.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                piece.AddComponent<MeshRenderer>().sharedMaterials = source.sharedMaterials;
                var collider = piece.AddComponent<BoxCollider>();
                collider.center = filter.sharedMesh.bounds.center;
                collider.size = Vector3.Max(filter.sharedMesh.bounds.size * .85f, Vector3.one * .025f);
                foreach (var actor in actorColliders) if (actor != null) Physics.IgnoreCollision(collider, actor);
                foreach (var other in pieceColliders) Physics.IgnoreCollision(collider, other);
                pieceColliders.Add(collider);
                var body = piece.AddComponent<Rigidbody>();
                body.mass = .15f; body.linearDamping = .45f; body.angularDamping = .6f;
                Vector3 outward = source.bounds.center - origin; outward.y = 0;
                if (outward.sqrMagnitude < .01f) outward = Random.insideUnitSphere;
                body.AddForce(outward.normalized * Random.Range(1.5f, 3.2f) + Vector3.up * Random.Range(1.5f, 3), ForceMode.VelocityChange);
                body.AddTorque(Random.insideUnitSphere * 7, ForceMode.VelocityChange);
                fragments.Add(piece.transform); fragmentScales.Add(piece.transform.localScale);
                source.enabled = false; hiddenParts.Add(source);
            }
            var flash = new GameObject("Amber core flash"); flash.transform.SetParent(debris.transform, false); flash.transform.position = origin;
            burstLight = flash.AddComponent<Light>(); burstLight.type = LightType.Point;
            burstLight.color = glowColour; burstLight.range = 5; burstLight.intensity = 7; burstLight.shadows = LightShadows.None;
        }

        void ClearDebris()
        {
            if (debris != null) { debris.SetActive(false); Destroy(debris); debris = null; }
            fragments.Clear(); fragmentScales.Clear(); burstLight = null;
        }

        void ResetDefeatFx()
        {
            ClearDebris();
            foreach (var part in hiddenParts) if (part != null) part.enabled = true;
            foreach (var collider in disabledColliders) if (collider != null) collider.enabled = true;
            hiddenParts.Clear(); disabledColliders.Clear(); defeatAt = -1; shattered = false;
            presenter?.SetDead(false);
        }

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
            if (!brain.Alive) intensity = shattered ? .04f + releaseFlash * Mathf.Clamp01(1 - (now - shatteredAt) / .35f) : 1.5f;
            else if (now < flashUntil) intensity = releaseFlash;
            else if (brain.Pulse.Busy)
            {
                float p = brain.Pulse.Progress(now);
                intensity = 1 + chargeBoost * p + 0.6f * p * Mathf.Sin(now * 40);     // brightening, flickering telegraph
            }
            else intensity = 1 + 0.12f * Mathf.Sin(now * 2.2f);                     // idle "breathing"
            glow.SetColor("_EmissionColor", glowColour * intensity);
        }

        void OnDestroy() { ClearDebris(); if (glow != null) Destroy(glow); }
    }
}
