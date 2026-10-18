using Skybreak.Presentation;
using UnityEngine;

namespace Skybreak.AI.RiftWarden
{
    // Presentation only: reads RiftWardenBrain and adds hit-stop, camera shake, hit flashes and
    // the death slow-motion. Never writes to the brain. Time.timeScale is only changed while one of
    // these effects is running and is always restored to 1 afterwards (and when disabled).
    [DisallowMultipleComponent]
    public sealed class RiftCombatImpact : MonoBehaviour
    {
        public RiftWardenBrain brain;
        public bool effectsEnabled = true;
        [Tooltip("Hit-stop and slow motion only happen while the player is this close to the arena centre.")]
        public float arenaReach = 18, shakeFalloff = 30;

        [Header("Hit-stop (real seconds)")]
        public float partHitStop = .035f, armBreakStop = .14f, slamStop = .08f, playerHurtStop = .06f, minGapBetweenStops = .25f;
        [Range(0, 1)] public float hitStopTimeScale = .05f;

        [Header("Camera shake (trauma 0..1)")]
        public float partHitShake = .07f, cannonShake = .18f, slashShake = .3f, slamShake = .75f, armBreakShake = .85f;
        public float coreOpenShake = .55f, teleportShake = .22f, playerHurtShake = .35f, deathShake = 1f;

        [Header("Hit flash")]
        public Color flashColour = new Color(1.3f, .95f, 2.2f);
        public float flashSeconds = .14f, flashEmissionBoost = 5f, flashLightIntensity = 26f, flashLightRange = 5f;

        [Header("Death slow motion")]
        public float deathTimeScale = .3f, deathSlowSeconds = 1.4f;

        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        SkinnedMeshRenderer coreRenderer;
        Renderer[] partRenderers = new Renderer[3];
        Color[] baseEmission = new Color[3];
        float[] flashUntil = new float[3], flashStrength = new float[3];
        MaterialPropertyBlock block;
        Light flashLight; float flashLightUntil, flashLightStrength; Vector3 flashLightAt;

        RiftAction lastAction; bool lastCoreExposed; int lastShots, lastWindows, lastTeleports;
        float lastLeft, lastRight, lastCore, lastPlayer = -1;
        float stopUntil, slowUntil, lastStopAt = -10; bool ownsTimeScale;
        CameraShake shake;

        void Awake() { if (brain == null) brain = GetComponent<RiftWardenBrain>(); }

        void Start()
        {
            if (brain == null) return;
            foreach (var r in brain.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (r.name == "RW_Core") coreRenderer = r;
            partRenderers[(int)RiftPart.LeftArm] = brain.leftArm;
            partRenderers[(int)RiftPart.RightArm] = brain.rightArm;
            partRenderers[(int)RiftPart.Core] = coreRenderer;
            for (int i = 0; i < 3; i++)
                baseEmission[i] = partRenderers[i] != null && partRenderers[i].sharedMaterial != null && partRenderers[i].sharedMaterial.HasProperty(EmissionColor)
                    ? partRenderers[i].sharedMaterial.GetColor(EmissionColor) : Color.black;
            var go = new GameObject("Rift hit flash light") { hideFlags = HideFlags.DontSave };
            flashLight = go.AddComponent<Light>();
            flashLight.type = LightType.Point; flashLight.shadows = LightShadows.None; flashLight.range = flashLightRange; flashLight.intensity = 0;
            Snapshot();
        }

        void Snapshot()
        {
            lastAction = brain.Action; lastCoreExposed = brain.CoreExposed;
            lastShots = brain.ShotsFired; lastWindows = brain.DamageWindows; lastTeleports = brain.SuccessfulTeleports;
            lastLeft = brain.LeftHealth; lastRight = brain.RightHealth; lastCore = brain.CoreHealth;
            lastPlayer = brain.playerVitals != null ? brain.playerVitals.Health : -1;
        }

        bool PlayerInArena()
        {
            if (brain.player == null) return false;
            Vector3 d = brain.player.position - brain.arenaCentre; d.y = 0;
            return d.magnitude < arenaReach;
        }

        void LateUpdate()
        {
            if (brain == null) return;
            if (effectsEnabled) Detect();
            else Snapshot();
            UpdateTime();
            UpdateFlashes();
        }

        void Detect()
        {
            Vector3 boss = brain.transform.position + Vector3.up * 1.7f;
            var a = brain.Action;
            if (a != lastAction)
            {
                if (a == RiftAction.BreakLeft || a == RiftAction.BreakRight)
                {
                    var part = a == RiftAction.BreakLeft ? RiftPart.LeftArm : RiftPart.RightArm;
                    HitStop(armBreakStop, true); Shake(armBreakShake, boss); Flash(part, 2.2f);
                }
                else if (a == RiftAction.Dying)
                {
                    Shake(deathShake, boss); Flash(RiftPart.Core, 3f);
                    if (PlayerInArena()) slowUntil = Time.unscaledTime + deathSlowSeconds;
                }
            }
            if (!lastCoreExposed && brain.CoreExposed) { Shake(coreOpenShake, boss); Flash(RiftPart.Core, 1.6f); }
            if (brain.ShotsFired > lastShots && brain.leftTarget != null) Shake(cannonShake, brain.leftTarget.AimPoint);
            if (brain.DamageWindows > lastWindows)
            {
                if (a == RiftAction.Slam) { HitStop(slamStop, true); Shake(slamShake, boss); }
                else Shake(slashShake, boss);
            }
            if (brain.SuccessfulTeleports > lastTeleports) Shake(teleportShake, boss);

            if (brain.LeftHealth < lastLeft - .001f) PartHit(RiftPart.LeftArm);
            if (brain.RightHealth < lastRight - .001f) PartHit(RiftPart.RightArm);
            if (brain.CoreHealth < lastCore - .001f && a != RiftAction.Dying) PartHit(RiftPart.Core);

            if (brain.playerVitals != null)
            {
                float hp = brain.playerVitals.Health;
                if (lastPlayer >= 0 && hp < lastPlayer - .001f && brain.Action != RiftAction.Dormant)
                {
                    HitStop(playerHurtStop, false);
                    if (brain.player != null) Shake(playerHurtShake, brain.player.position);
                }
            }
            Snapshot();
        }

        void PartHit(RiftPart part)
        {
            Flash(part, 1f);
            HitStop(partHitStop, false);
            var target = part == RiftPart.LeftArm ? brain.leftTarget : part == RiftPart.RightArm ? brain.rightTarget : brain.coreTarget;
            if (target != null) Shake(partHitShake, target.AimPoint);
        }

        void Shake(float amount, Vector3 source)
        {
            if (amount <= 0) return;
            if (shake == null) shake = CameraShake.ForMainCamera();
            if (shake != null) shake.Add(amount, source, shakeFalloff);
        }

        void HitStop(float seconds, bool force)
        {
            if (seconds <= 0 || !PlayerInArena()) return;
            float now = Time.unscaledTime;
            if (!force && now - lastStopAt < minGapBetweenStops) return;
            lastStopAt = now;
            stopUntil = Mathf.Max(stopUntil, now + seconds);
        }

        void UpdateTime()
        {
            float now = Time.unscaledTime;
            float scale = 1;
            if (now < slowUntil)
            {
                float u = 1 - (slowUntil - now) / Mathf.Max(.01f, deathSlowSeconds);   // 0 -> 1 over the slow-mo
                scale = Mathf.Lerp(deathTimeScale, 1, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.65f, 1f, u)));
            }
            if (now < stopUntil) scale = Mathf.Min(scale, hitStopTimeScale);
            if (scale < 1) { Time.timeScale = scale; ownsTimeScale = true; }
            else if (ownsTimeScale) { Time.timeScale = 1; ownsTimeScale = false; }
        }

        public void Flash(RiftPart part, float strength)
        {
            int i = (int)part;
            flashUntil[i] = Time.unscaledTime + flashSeconds * Mathf.Max(1, strength * .8f);
            flashStrength[i] = strength;
            var target = part == RiftPart.LeftArm ? brain.leftTarget : part == RiftPart.RightArm ? brain.rightTarget : brain.coreTarget;
            flashLightAt = target != null ? target.AimPoint : brain.transform.position + Vector3.up * 2.4f;
            flashLightUntil = flashUntil[i];
            flashLightStrength = strength;
        }

        void UpdateFlashes()
        {
            float now = Time.unscaledTime;
            block ??= new MaterialPropertyBlock();
            for (int i = 0; i < 3; i++)
            {
                var r = partRenderers[i];
                if (r == null) continue;
                float k = flashUntil[i] > now ? Mathf.Clamp01((flashUntil[i] - now) / (flashSeconds * Mathf.Max(1, flashStrength[i] * .8f))) : 0;
                if (k > 0)
                {
                    r.GetPropertyBlock(block);
                    block.SetColor(EmissionColor, baseEmission[i] + flashColour * flashEmissionBoost * flashStrength[i] * k * k);
                    r.SetPropertyBlock(block);
                }
                else if (r.HasPropertyBlock()) r.SetPropertyBlock(null);
            }
            if (flashLight != null)
            {
                float k = flashLightUntil > now ? Mathf.Clamp01((flashLightUntil - now) / (flashSeconds * Mathf.Max(1, flashLightStrength * .8f))) : 0;
                flashLight.transform.position = flashLightAt;
                flashLight.color = flashColour / Mathf.Max(.01f, flashColour.maxColorComponent);
                flashLight.intensity = flashLightIntensity * flashLightStrength * k * k;
            }
        }

        void OnDisable()
        {
            if (ownsTimeScale) { Time.timeScale = 1; ownsTimeScale = false; }
            stopUntil = slowUntil = 0;
            foreach (var r in partRenderers) if (r != null) r.SetPropertyBlock(null);
            if (flashLight != null) flashLight.intensity = 0;
        }

        void OnDestroy() { if (flashLight != null) Destroy(flashLight.gameObject); }
    }
}
