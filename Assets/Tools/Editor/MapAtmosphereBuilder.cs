// Builds the map atmosphere pass: zone Volume profiles, fog, and ambient particle systems.
// Menu: Skybreak > Map > Rebuild Atmosphere (open scene). Batch: MapAtmosphereBuilder.ApplyBatch / PreviewBatch.
// Everything goes under the scene root "19 / Atmosphere and ambient life"; rebuilding replaces it.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Skybreak.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class MapAtmosphereBuilder
{
    const string RootName = "19 / Atmosphere and ambient life";
    const string ProfileDir = "Assets/Lighting/Atmosphere";
    const string MatDir = "Assets/Materials/Effects/Atmosphere";
    static readonly string[] Scenes = { "Assets/Scenes/GameLevel/SkybreakMain.unity", "Assets/LocalCombatLab/OwnedAgentsLab.unity" };
    const string PreviewDir = @"F:\UnityProjects\Deliverables\Map_Atmosphere\Previews";

    // ------------------------------------------------------------------ zone definitions
    struct ZoneDef
    {
        public string name; public Vector3 centre, size; public float blend;
        public Color fog; public float density;
        public float temperature, tint, exposure, contrast, saturation; public Color filter;
        public float vignette; public Color vignetteColour; public float bloom; public Color bloomTint;
        public Color mote, mist; public bool groundMist;
    }

    static readonly ZoneDef[] Zones =
    {
        new ZoneDef { name = "Entrance and loading court", centre = new Vector3(-.5f, 4, 8.5f), size = new Vector3(36, 10, 36), blend = 6,
            fog = new Color(.13f, .115f, .10f), density = .011f, temperature = 18, tint = 0, exposure = .2f, contrast = 8, saturation = 8,
            filter = new Color(1f, .96f, .9f), vignette = .22f, vignetteColour = new Color(.06f, .03f, 0), bloom = .8f, bloomTint = new Color(1, .85f, .65f),
            mote = new Color(1f, .82f, .55f, .8f) },
        new ZoneDef { name = "Winding passage", centre = new Vector3(17, 4, 37), size = new Vector3(22, 10, 26), blend = 5,
            fog = new Color(.06f, .10f, .14f), density = .026f, temperature = -18, tint = 4, exposure = .3f, contrast = 8, saturation = -2,
            filter = new Color(.85f, .95f, 1f), vignette = .3f, vignetteColour = new Color(0, .02f, .05f), bloom = .9f, bloomTint = new Color(.7f, .9f, 1),
            mote = new Color(.6f, .85f, 1f, .75f), mist = new Color(.55f, .72f, .88f, .2f), groundMist = true },
        new ZoneDef { name = "Security wing", centre = new Vector3(42.5f, 4, 50), size = new Vector3(58, 10, 20), blend = 5,
            fog = new Color(.08f, .09f, .10f), density = .016f, temperature = -12, tint = 0, exposure = .3f, contrast = 10, saturation = -10,
            filter = new Color(.92f, .97f, 1f), vignette = .3f, vignetteColour = Color.black, bloom = .6f, bloomTint = new Color(.85f, .95f, 1),
            mote = new Color(.8f, .9f, 1f, .6f) },
        new ZoneDef { name = "Final chamber approach", centre = new Vector3(42, 4, 79), size = new Vector3(26, 10, 42), blend = 6,
            fog = new Color(.09f, .05f, .13f), density = .016f, temperature = -6, tint = 18, exposure = .25f, contrast = 10, saturation = 12,
            filter = new Color(.95f, .86f, 1f), vignette = .36f, vignetteColour = new Color(.07f, 0, .12f), bloom = 1f, bloomTint = new Color(.9f, .7f, 1),
            mote = new Color(.82f, .55f, 1f, .8f), mist = new Color(.62f, .45f, .88f, .22f), groundMist = true },
    };

    // ------------------------------------------------------------------ assets
    static VolumeProfile Profile(string name)
    {
        Directory.CreateDirectory(ProfileDir);
        string path = $"{ProfileDir}/{name}.asset";
        var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (p != null) { foreach (var c in p.components.ToList()) { p.Remove(c.GetType()); } foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path).Where(a => a is VolumeComponent)) Object.DestroyImmediate(sub, true); }
        else { p = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(p, path); }
        return p;
    }

    static T Add<T>(VolumeProfile p) where T : VolumeComponent
    {
        var c = p.Add<T>(false);
        c.name = typeof(T).Name; c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(c, p);
        return c;
    }

    static VolumeProfile BaseProfile()
    {
        var p = Profile("Atmosphere_Base");
        var b = Add<Bloom>(p); b.threshold.Override(.95f); b.intensity.Override(.6f); b.scatter.Override(.7f);
        var v = Add<Vignette>(p); v.intensity.Override(.18f); v.smoothness.Override(.4f);
        EditorUtility.SetDirty(p); return p;
    }

    static VolumeProfile ZoneProfile(ZoneDef z)
    {
        var p = Profile("Zone_" + z.name.Replace(" ", ""));
        var wb = Add<WhiteBalance>(p); wb.temperature.Override(z.temperature); wb.tint.Override(z.tint);
        var ca = Add<ColorAdjustments>(p); ca.postExposure.Override(z.exposure); ca.contrast.Override(z.contrast); ca.saturation.Override(z.saturation); ca.colorFilter.Override(z.filter);
        var v = Add<Vignette>(p); v.intensity.Override(z.vignette); v.color.Override(z.vignetteColour); v.smoothness.Override(.45f);
        var b = Add<Bloom>(p); b.intensity.Override(z.bloom); b.tint.Override(z.bloomTint);
        EditorUtility.SetDirty(p); return p;
    }

    static Material ParticleMat(string name, string texPath, Color colour, bool additive)
    {
        Directory.CreateDirectory(MatDir);
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool create = m == null;
        if (create) m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath)); m.SetColor("_BaseColor", colour);
        m.SetFloat("_Surface", 1); m.SetFloat("_Blend", additive ? 2 : 0);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0); m.SetFloat("_Cull", 0); m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)RenderQueue.Transparent;
        var type = typeof(Editor).Assembly.GetType("UnityEditor.BaseShaderGUI") ?? TypeCache.GetTypesDerivedFrom<ShaderGUI>().FirstOrDefault(t => t.Name == "BaseShaderGUI");
        type?.GetMethod("SetupMaterialBlendMode", new[] { typeof(Material) })?.Invoke(null, new object[] { m });
        if (create) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------------ particles
    static ParticleSystem PS(Transform parent, string name, Vector3 pos, Material mat, int max, Vector2 life, Vector2 speed, Vector2 size, Color colour, float rate)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true; main.prewarm = true; main.playOnAwake = true; main.duration = 5; main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = colour;
        var em = ps.emission; em.rateOverTime = rate;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .2f), new GradientAlphaKey(1, .7f), new GradientAlphaKey(0, 1) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.sortMode = ParticleSystemSortMode.Distance;
        return ps;
    }

    static void Noise(ParticleSystem ps, float strength, float freq)
    {
        var n = ps.noise; n.enabled = true; n.strength = strength; n.frequency = freq; n.scrollSpeed = .15f; n.quality = ParticleSystemNoiseQuality.Low;
    }

    static void Rise(ParticleSystem ps, float lo, float hi, float orbit = 0)
    {
        var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
        // every velocity curve must share one mode (TwoConstants), or Unity ignores the module
        v.x = new ParticleSystem.MinMaxCurve(0, 0); v.z = new ParticleSystem.MinMaxCurve(0, 0); v.y = new ParticleSystem.MinMaxCurve(lo, hi);
        v.orbitalX = new ParticleSystem.MinMaxCurve(0, 0); v.orbitalZ = new ParticleSystem.MinMaxCurve(0, 0); v.orbitalY = new ParticleSystem.MinMaxCurve(orbit, orbit);
    }

    static void Shape(ParticleSystem ps, ParticleSystemShapeType type, Vector3 scale, float radius = 1, Vector3 rotation = default)
    {
        var s = ps.shape; s.enabled = true; s.shapeType = type; s.scale = scale; s.radius = radius; s.rotation = rotation; s.radiusThickness = 1;
    }

    static Bounds RB(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.zero);
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
    }

    static IEnumerable<GameObject> PrefabInstances(UnityEngine.SceneManagement.Scene scene, string assetNameContains) =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject)
            .Where(g => PrefabUtility.IsOutermostPrefabInstanceRoot(g) && (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g) ?? "").Contains(assetNameContains));

    // ------------------------------------------------------------------ build
    public static string Build(UnityEngine.SceneManagement.Scene scene)
    {
        var sb = new StringBuilder();
        var old = scene.GetRootGameObjects().FirstOrDefault(r => r.name == RootName);
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject(RootName);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);

        // moods
        var moods = new GameObject("Zone moods (fog + colour)"); moods.transform.SetParent(root.transform, false);
        var baseVol = new GameObject("Map base / bloom + vignette").AddComponent<Volume>();
        baseVol.transform.SetParent(moods.transform, false); baseVol.isGlobal = true; baseVol.priority = 1; baseVol.sharedProfile = BaseProfile();
        var atmo = moods.AddComponent<MapAtmosphere>();
        var zones = new List<MapAtmosphere.Zone>();
        foreach (var z in Zones)
        {
            var v = new GameObject("Zone / " + z.name).AddComponent<Volume>();
            v.transform.SetParent(moods.transform, false); v.isGlobal = true; v.priority = 5; v.weight = 0; v.sharedProfile = ZoneProfile(z);
            zones.Add(new MapAtmosphere.Zone { name = z.name, centre = z.centre, size = z.size, blend = z.blend, volume = v, fogColour = z.fog, fogDensity = z.density });
        }
        atmo.zones = zones.ToArray();
        atmo.defaultFogColour = new Color(.09f, .10f, .12f); atmo.defaultFogDensity = .012f;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = atmo.defaultFogColour; RenderSettings.fogDensity = atmo.defaultFogDensity;
        sb.AppendLine($"ATMO moods: base volume + {zones.Count} zone volumes, fog ExponentialSquared {atmo.defaultFogDensity}");

        var mote = ParticleMat("Atmo_Mote", "Assets/Textures/Effects/RiftFx/RiftFx_Glow.png", new Color(1.5f, 1.5f, 1.5f, 1), true);
        var mist = ParticleMat("Atmo_Mist", "Assets/Textures/Effects/RiftFx/RiftFx_Smoke.png", Color.white, false);
        int systems = 0;

        // dust motes and ground mist per zone
        var dust = new GameObject("Dust motes"); dust.transform.SetParent(root.transform, false);
        var mists = new GameObject("Ground mist"); mists.transform.SetParent(root.transform, false);
        foreach (var z in Zones)
        {
            float area = z.size.x * z.size.z;
            var ps = PS(dust.transform, "Dust / " + z.name, new Vector3(z.centre.x, 2.6f, z.centre.z), mote, 380, new Vector2(9, 14), new Vector2(.02f, .1f), new Vector2(.05f, .11f), z.mote, Mathf.Min(34, area * .03f));
            Shape(ps, ParticleSystemShapeType.Box, new Vector3(z.size.x * .95f, 4.6f, z.size.z * .95f)); Noise(ps, .25f, .25f); systems++;
            if (z.groundMist)
            {
                var m = PS(mists.transform, "Mist / " + z.name, new Vector3(z.centre.x, .45f, z.centre.z), mist, 60, new Vector2(12, 18), new Vector2(.05f, .15f), new Vector2(3, 6), z.mist, 3.2f);
                Shape(m, ParticleSystemShapeType.Box, new Vector3(z.size.x * .9f, .5f, z.size.z * .9f));
                var mm = m.main; mm.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                var rot = m.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-.1f, .1f);
                systems++;
            }
        }

        // crystal pedestals
        var crystals = new GameObject("Crystal sparkles"); crystals.transform.SetParent(root.transform, false);
        foreach (var c in PrefabInstances(scene, "CrystalPedestal"))
        {
            var b = RB(c);
            var ps = PS(crystals.transform, "Sparkles / " + c.name, new Vector3(b.center.x, b.max.y - .6f, b.center.z), mote, 40, new Vector2(1.8f, 3), new Vector2(0, .08f), new Vector2(.06f, .14f), new Color(.5f, .95f, 1f, 1f), 9);
            Shape(ps, ParticleSystemShapeType.Sphere, Vector3.one, .4f); Rise(ps, .35f, .85f); Noise(ps, .3f, .6f);
            var so = ps.sizeOverLifetime; so.enabled = true; so.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, .2f)));
            systems++;
        }

        // shrine and dais energy
        var energy = new GameObject("Shrine and dais energy"); energy.transform.SetParent(root.transform, false);
        foreach (var (key, colourA, colourB, radius, rate) in new[] {
            ("AuroraReactorShrine", new Color(.75f, .35f, 1f, .85f), new Color(.35f, .9f, 1f, .85f), 1.6f, 16f),
            ("AuroraHallPowerDais", new Color(.35f, .9f, 1f, .8f), new Color(.6f, 1f, 1f, .8f), 2.4f, 12f) })
            foreach (var g in PrefabInstances(scene, key))
            {
                var b = RB(g);
                var ps = PS(energy.transform, "Energy / " + g.name, new Vector3(b.center.x, b.min.y + .15f, b.center.z), mote, 110, new Vector2(3, 5), Vector2.zero, new Vector2(.07f, .16f), colourA, rate);
                var main = ps.main; main.startColor = new ParticleSystem.MinMaxGradient(colourA, colourB);
                Shape(ps, ParticleSystemShapeType.Circle, Vector3.one, radius, new Vector3(-90, 0, 0)); Rise(ps, .6f, 1.4f, .6f);
                systems++;
            }

        // lantern embers
        var embers = new GameObject("Lantern embers"); embers.transform.SetParent(root.transform, false);
        int lanterns = 0;
        foreach (var g in PrefabInstances(scene, "Lantern"))
        {
            var b = RB(g);
            var ps = PS(embers.transform, "Embers / " + g.name, b.center, mote, 10, new Vector2(2, 3.5f), new Vector2(0, .05f), new Vector2(.035f, .07f), new Color(1f, .7f, .35f, 1f), 2.2f);
            Shape(ps, ParticleSystemShapeType.Sphere, Vector3.one, .12f); Rise(ps, .15f, .35f); Noise(ps, .15f, .8f);
            systems++; lanterns++;
        }

        // pipe steam (every third copper vine)
        var steam = new GameObject("Pipe steam"); steam.transform.SetParent(root.transform, false);
        var vineRoot = scene.GetRootGameObjects().FirstOrDefault(r => r.name.Contains("copper wall vines"));
        int vents = 0;
        if (vineRoot != null)
        {
            var vines = vineRoot.transform.Cast<Transform>().ToList();
            for (int i = 0; i < vines.Count; i += 3)
            {
                var b = RB(vines[i].gameObject);
                var ps = PS(steam.transform, "Steam / " + vines[i].name, new Vector3(b.center.x, b.max.y, b.center.z), mist, 14, new Vector2(2.5f, 4), new Vector2(.3f, .6f), new Vector2(.3f, .6f), new Color(.85f, .9f, .92f, .2f), 1.4f);
                Shape(ps, ParticleSystemShapeType.Cone, Vector3.one, .1f, new Vector3(-90, 0, 0)); var s = ps.shape; s.angle = 15;
                var so = ps.sizeOverLifetime; so.enabled = true; so.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .5f), new Keyframe(1, 2.2f)));
                systems++; vents++;
            }
        }

        // dust trickling from the tallest cliffs near the play area
        var trickles = new GameObject("Cliff dust"); trickles.transform.SetParent(root.transform, false);
        var cliffs = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .Where(t => t.name.StartsWith("Cliff ") && PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
            .Select(t => (t, b: RB(t.gameObject))).OrderByDescending(x => x.b.max.y).Take(6).ToList();
        Vector3 mapCentre = new Vector3(27, 0, 45);
        foreach (var (t, b) in cliffs)
        {
            Vector3 toward = (mapCentre - b.center); toward.y = 0; toward = toward.normalized;
            Vector3 top = new Vector3(b.center.x, b.max.y - .8f, b.center.z) + toward * Mathf.Min(b.extents.x, b.extents.z) * .8f;
            var ps = PS(trickles.transform, "Dust fall / " + t.name, top, mist, 50, new Vector2(5, 8), Vector2.zero, new Vector2(.05f, .14f), new Color(.6f, .57f, .52f, .45f), 5);
            Shape(ps, ParticleSystemShapeType.Box, new Vector3(1.6f, .2f, .6f)); var main = ps.main; main.gravityModifier = .22f; Noise(ps, .2f, .4f);
            systems++;
        }
        sb.AppendLine($"ATMO particles: {systems} systems (lanterns {lanterns}, steam vents {vents}, cliffs {cliffs.Count})");
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    [MenuItem("Skybreak/Map/Rebuild Atmosphere (open scene)")]
    public static void BuildOpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorUtility.DisplayDialog("Atmosphere", "Stop Play mode first.", "OK"); return; }
        var scene = EditorSceneManager.GetActiveScene();
        var report = Build(scene); EditorSceneManager.MarkSceneDirty(scene);
        EditorUtility.DisplayDialog("Atmosphere", report + "\nSave the scene to keep it.", "OK");
    }

    public static void ApplyBatch()
    {
        var sb = new StringBuilder();
        foreach (var path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            sb.Append($"[{scene.name}] ").Append(Build(scene));
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        Debug.Log(sb.ToString());
        EditorApplication.Exit(0);
    }

    // ------------------------------------------------------------------ previews
    public static void PreviewBatch()
    {
        ShaderUtil.allowAsyncCompilation = false;
        Directory.CreateDirectory(PreviewDir);
        var scene = EditorSceneManager.OpenScene(Scenes[0], OpenSceneMode.Single);
        var root = scene.GetRootGameObjects().First(r => r.name == RootName);
        var atmo = root.GetComponentInChildren<MapAtmosphere>();
        var shots = new (string name, Vector3 eye, Vector3 look)[]
        {
            ("1_entrance", new Vector3(0, 1.8f, -7), new Vector3(0, 1.4f, 12)),
            ("2_passage", new Vector3(9.5f, 1.8f, 34), new Vector3(10, 1.5f, 46)),
            ("3_security", new Vector3(24, 1.8f, 47), new Vector3(60, 1.5f, 51)),
            ("4_final_approach", new Vector3(42, 1.8f, 62), new Vector3(42, 2.2f, 88)),
            ("5_overview", new Vector3(27, 42, -26), new Vector3(27, 0, 45)),
        };
        foreach (var s in shots)
        {
            root.SetActive(false); RenderSettings.fog = false;
            Shot(s.eye, s.look, $"{s.name}_before.png");
            root.SetActive(true);
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true)) ps.Simulate(14, true, true, true);
            atmo.Apply(s.eye, 1);
            Shot(s.eye, s.look, $"{s.name}_after.png");
        }
        Debug.Log("ATMO previews written");
        EditorApplication.Exit(0);
    }

    static void Shot(Vector3 eye, Vector3 look, string file)
    {
        var go = new GameObject("TempAtmoCamera");
        var cam = go.AddComponent<Camera>();
        var d = go.AddComponent<UniversalAdditionalCameraData>(); d.renderPostProcessing = true; d.volumeLayerMask = ~0;
        cam.allowHDR = true; cam.fieldOfView = 60; cam.nearClipPlane = .1f; cam.farClipPlane = 280;
        go.transform.position = eye; go.transform.LookAt(look);
        var rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt; cam.Render(); cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(960, 540, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply();
        File.WriteAllBytes(Path.Combine(PreviewDir, file), tex.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(go); Object.DestroyImmediate(tex);
    }
}
