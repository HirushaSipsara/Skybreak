using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Skybreak.AI.RiftWarden
{
    // Presentation only: reads RiftWardenBrain and drives the arena's post-processing, lights and floor rune ring.
    // Never writes to the brain. Everything it creates is built at runtime and destroyed with it.
    [DisallowMultipleComponent]
    public sealed class RiftArenaMood : MonoBehaviour
    {
        [System.Serializable]
        public struct Mood
        {
            public float bloom; public Color bloomTint;
            public float vignette; public Color vignetteColour;
            public Color filter; public float saturation, contrast, aberration;
            public Color light; public float lightIntensity;
            public Color rune; public float runeIntensity, pulse;

            public static Mood Lerp(Mood a, Mood b, float t) => new Mood
            {
                bloom = Mathf.Lerp(a.bloom, b.bloom, t), bloomTint = Color.Lerp(a.bloomTint, b.bloomTint, t),
                vignette = Mathf.Lerp(a.vignette, b.vignette, t), vignetteColour = Color.Lerp(a.vignetteColour, b.vignetteColour, t),
                filter = Color.Lerp(a.filter, b.filter, t), saturation = Mathf.Lerp(a.saturation, b.saturation, t),
                contrast = Mathf.Lerp(a.contrast, b.contrast, t), aberration = Mathf.Lerp(a.aberration, b.aberration, t),
                light = Color.Lerp(a.light, b.light, t), lightIntensity = Mathf.Lerp(a.lightIntensity, b.lightIntensity, t),
                rune = Color.Lerp(a.rune, b.rune, t), runeIntensity = Mathf.Lerp(a.runeIntensity, b.runeIntensity, t),
                pulse = Mathf.Lerp(a.pulse, b.pulse, t),
            };
        }

        static Mood M(float bloom, Color tint, float vig, Color vigCol, Color filter, float sat, float con, float ca,
                      Color light, float lightI, Color rune, float runeI, float pulse) =>
            new Mood { bloom = bloom, bloomTint = tint, vignette = vig, vignetteColour = vigCol, filter = filter, saturation = sat,
                       contrast = con, aberration = ca, light = light, lightIntensity = lightI, rune = rune, runeIntensity = runeI, pulse = pulse };

        public RiftWardenBrain brain;

        [Header("Reach: full strength inside the arena, fading out beyond it")]
        public float fullStrengthMargin = 3, fadeDistance = 10, blendSeconds = 1.2f;

        [Header("Lights and ring")]
        public float arenaLightHeight = 5.5f, arenaLightRange = 26, weaponLightRange = 3.2f, ringWidth = .22f;
        public Color weaponGlow = new Color(.62f, .2f, 1f), coreGlow = new Color(.2f, .9f, 1f);

        [Header("Phase moods")]
        public Mood dormant = M(.6f, Color.white, .2f, Color.black, Color.white, 0, 5, 0,
                                new Color(.35f, .8f, .85f), 14f, new Color(.2f, .8f, .85f), .5f, 0);
        public Mood stageOne = M(1.1f, new Color(.85f, 1f, 1f), .32f, new Color(.01f, .04f, .08f), new Color(.84f, .96f, 1f), 8, 14, .06f,
                                 new Color(.3f, .85f, 1f), 32f, new Color(.15f, .85f, .95f), 1.2f, .15f);
        public Mood stageTwo = M(1.4f, new Color(1f, .8f, 1f), .38f, new Color(.08f, .01f, .12f), new Color(.93f, .84f, 1f), 12, 18, .14f,
                                 new Color(.7f, .3f, 1f), 40f, new Color(.7f, .2f, 1f), 1.6f, .3f);
        public Mood coreOpen = M(1.8f, new Color(.8f, .85f, 1f), .45f, new Color(.1f, 0f, .16f), new Color(.84f, .8f, 1f), 16, 22, .22f,
                                 new Color(.5f, .15f, 1f), 46f, new Color(.8f, .1f, 1f), 1.9f, .6f);
        public Mood dying = M(2.8f, new Color(.85f, 1f, 1f), .52f, new Color(.05f, 0f, .1f), new Color(1f, .95f, 1f), 18, 24, .38f,
                              new Color(.75f, .95f, 1f), 70f, new Color(.9f, .35f, 1f), 2.4f, 1f);
        public Mood defeated = M(.8f, new Color(1f, .97f, .9f), .2f, Color.black, new Color(1f, .97f, .9f), 6, 8, 0,
                                 new Color(1f, .85f, .6f), 22f, new Color(.25f, .8f, .8f), .3f, 0);
        public int Phase { get; private set; }
        public float Weight { get; private set; }

        Volume volume; VolumeProfile profile;
        Bloom bloom; Vignette vignette; ColorAdjustments colour; ChromaticAberration aberration;
        Light arenaLight, bladeLight, cannonLight, coreLight;
        LineRenderer outerRing, innerRing; Material ringMaterial;
        GameObject arenaRoot;
        Mood current; bool built;
        int lastShots, lastWindows; float shotFlare, slashFlare;

        void Awake() { if (brain == null) brain = GetComponent<RiftWardenBrain>(); }

        void Start()
        {
            Build();
            current = MoodFor(Phase = ComputePhase());
            if (brain != null) { lastShots = brain.ShotsFired; lastWindows = brain.DamageWindows; }
        }

        public void Build()
        {
            if (built || brain == null) return;
            built = true;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.hideFlags = HideFlags.DontSave;
            bloom = profile.Add<Bloom>(); bloom.intensity.Override(0); bloom.tint.Override(Color.white); bloom.threshold.Override(.95f); bloom.scatter.Override(.72f);
            vignette = profile.Add<Vignette>(); vignette.intensity.Override(0); vignette.color.Override(Color.black); vignette.smoothness.Override(.45f);
            colour = profile.Add<ColorAdjustments>(); colour.colorFilter.Override(Color.white); colour.saturation.Override(0); colour.contrast.Override(0);
            aberration = profile.Add<ChromaticAberration>(); aberration.intensity.Override(0);

            arenaRoot = new GameObject("Rift arena mood (runtime)") { hideFlags = HideFlags.DontSave };
            arenaRoot.transform.position = brain.arenaCentre;
            var vgo = new GameObject("Arena mood volume") { hideFlags = HideFlags.DontSave };
            vgo.transform.SetParent(arenaRoot.transform, false);
            volume = vgo.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 50; volume.weight = 0; volume.sharedProfile = profile;

            arenaLight = MakeLight("Arena key light", arenaRoot.transform, Vector3.up * arenaLightHeight, arenaLightRange);
            bladeLight = MakeLight("Blade glow", Bone("Forearm.R"), Vector3.zero, weaponLightRange);
            cannonLight = MakeLight("Cannon glow", Bone("Forearm.L"), Vector3.zero, weaponLightRange);
            coreLight = MakeLight("Core glow", Bone("Core"), Vector3.zero, 5f);
            bladeLight.color = cannonLight.color = weaponGlow; coreLight.color = coreGlow;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            ringMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            if (ringMaterial.HasProperty("_Cull")) ringMaterial.SetFloat("_Cull", 0);
            outerRing = MakeRing("Rune ring", brain.arenaRadius - .3f, ringWidth);
            innerRing = MakeRing("Rune ring inner", brain.arenaRadius * .62f, ringWidth * .55f);
        }

        Transform Bone(string name)
        {
            foreach (var t in brain.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return brain.transform;
        }

        static Light MakeLight(string name, Transform parent, Vector3 local, float range)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.range = range; l.shadows = LightShadows.None; l.intensity = 0;
            return l;
        }

        LineRenderer MakeRing(string name, float radius, float width)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(arenaRoot.transform, false);
            go.transform.localPosition = Vector3.up * .04f;
            go.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false; lr.loop = true; lr.alignment = LineAlignment.TransformZ;
            lr.widthMultiplier = width; lr.sharedMaterial = ringMaterial;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false;
            const int n = 96; lr.positionCount = n;
            for (int i = 0; i < n; i++) { float a = i * Mathf.PI * 2 / n; lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0)); }
            return lr;
        }

        int ComputePhase()
        {
            if (brain == null) return 0;
            if (brain.Defeated) return 5;
            if (brain.Action == RiftAction.Dying) return 4;
            if (brain.Action == RiftAction.Dormant) return 0;
            if (brain.CoreExposed || brain.Action == RiftAction.CoreOpen) return 3;
            if (brain.LeftHealth <= 0 || brain.RightHealth <= 0) return 2;
            return 1;
        }

        public Mood MoodFor(int phase) => phase switch { 1 => stageOne, 2 => stageTwo, 3 => coreOpen, 4 => dying, 5 => defeated, _ => dormant };

        void Update()
        {
            if (!built || brain == null) return;
            float dt = Time.deltaTime;
            Phase = ComputePhase();
            current = Mood.Lerp(current, MoodFor(Phase), 1 - Mathf.Exp(-dt * 3f / Mathf.Max(.05f, blendSeconds)));

            if (brain.ShotsFired > lastShots) shotFlare = 1;
            if (brain.DamageWindows > lastWindows && (brain.Action == RiftAction.Slash || brain.Action == RiftAction.DoubleTeleport)) slashFlare = 1;
            lastShots = brain.ShotsFired; lastWindows = brain.DamageWindows;
            shotFlare = Mathf.MoveTowards(shotFlare, 0, dt * 6); slashFlare = Mathf.MoveTowards(slashFlare, 0, dt * 4);

            float w = 0;
            if (brain.player != null)
            {
                Vector3 d = brain.player.position - brain.arenaCentre; d.y = 0;
                w = 1 - Mathf.Clamp01((d.magnitude - (brain.arenaRadius + fullStrengthMargin)) / Mathf.Max(.1f, fadeDistance));
            }
            Apply(Time.time, w);
        }

        /// <summary>Applies the current mood. Public so editor tools can preview a phase without Play mode.</summary>
        public void Apply(float time, float weight)
        {
            Weight = weight;
            float pulse = 1 + current.pulse * .35f * Mathf.Sin(time * 4.2f);
            float healthLeft = Mathf.Clamp01((brain.LeftHealth + brain.RightHealth + brain.CoreHealth) /
                                             Mathf.Max(1, brain.armMaximum * 2 + brain.coreMaximum));
            float healthBoost = Mathf.Lerp(1.7f, 1f, healthLeft);

            volume.weight = weight;
            bloom.intensity.value = current.bloom * (1 + shotFlare * .6f);
            bloom.tint.value = current.bloomTint;
            vignette.intensity.value = current.vignette;
            vignette.color.value = current.vignetteColour;
            colour.colorFilter.value = current.filter;
            colour.saturation.value = current.saturation;
            colour.contrast.value = current.contrast;
            aberration.intensity.value = current.aberration * (1 + shotFlare * .8f);

            arenaLight.color = current.light;
            arenaLight.intensity = current.lightIntensity * pulse;

            bool bladeAlive = brain.rightArm != null && brain.rightArm.enabled && brain.RightHealth > 0 && Phase < 4;
            bool cannonAlive = brain.leftArm != null && brain.leftArm.enabled && brain.LeftHealth > 0 && Phase < 4;
            float slashCharge = brain.Action == RiftAction.Slash || brain.Action == RiftAction.DoubleTeleport ? 1.5f : 0;
            bladeLight.intensity = bladeAlive ? 3f + slashCharge * 3 + slashFlare * 16 : 0;
            cannonLight.intensity = cannonAlive ? 2f + shotFlare * 22 : 0;
            float coreOn = Phase == 3 ? (brain.CoreExposed ? 1 : .4f) : Phase == 4 ? 1.6f + Mathf.PerlinNoise(time * 18, 0) * 2.4f : 0;
            coreLight.intensity = 9f * coreOn * pulse;

            Color rune = current.rune * current.runeIntensity * healthBoost * pulse;
            ringMaterial.SetColor("_BaseColor", rune);
            if (ringMaterial.HasProperty("_Color")) ringMaterial.SetColor("_Color", rune);
        }

        /// <summary>Editor/test helper: snap to a phase's mood and apply it at full weight.</summary>
        public void PreviewPhase(int phase, float time = 0)
        {
            Build();
            Phase = phase; current = MoodFor(phase);
            Apply(time, 1);
        }

        void OnDestroy()
        {
            if (arenaRoot != null) Destroy(arenaRoot);
            foreach (var l in new[] { bladeLight, cannonLight, coreLight }) if (l != null) Destroy(l.gameObject);
            if (profile != null) Destroy(profile);
            if (ringMaterial != null) Destroy(ringMaterial);
        }
    }
}
