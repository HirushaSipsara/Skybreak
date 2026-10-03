using System;
using System.Collections.Generic;
using System.IO;
using Skybreak;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class SkybreakStarterScene
{
    const string ScenePath = "Assets/Skybreak/Scenes/SkybreakStarter.unity";
    const string MaterialDirectory = "Assets/Skybreak/Materials";
    static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
    static Transform section;

    static Material Paint(string name, Color color)
    {
        if (Materials.TryGetValue(name, out var result)) return result;
        string path = MaterialDirectory + "/" + name + ".mat";
        result = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (result == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            result = new Material(shader) { name = name, color = color, enableInstancing = true };
            AssetDatabase.CreateAsset(result, path);
        }
        Materials[name] = result;
        return result;
    }

    static GameObject Shape(string name, PrimitiveType kind, Vector3 position, Vector3 scale, string paint, Color color, bool solid = true)
    {
        var go = GameObject.CreatePrimitive(kind);
        go.name = name;
        go.transform.position = position;
        go.transform.localScale = scale;
        if (section != null) go.transform.SetParent(section, true);
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = Paint(paint, color);
        if (!solid) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject Box(string name, float x, float y, float z, float width, float height, float depth, string paint, Color color, bool solid = true)
    {
        return Shape(name, PrimitiveType.Cube, new Vector3(x, y, z), new Vector3(width, height, depth), paint, color, solid);
    }

    static void House(string name, float x, float z, float width, float depth, float height, string wallPaint, Color wall)
    {
        Box(name + " / teal walls", x, height / 2, z, width, height, depth, wallPaint, wall);
        Box(name + " / stone trim", x, .34f, z, width + .1f, .68f, depth + .1f, "Stone", new Color(.74f, .72f, .63f));
        Box(name + " / terracotta roof", x, height + .08f, z, width + .3f, .16f, depth + .3f, "Roof", new Color(.79f, .36f, .18f));
        for (float rz = z - depth / 2 + .55f; rz < z + depth / 2; rz += 1.2f)
            Box(name + " / tile row", x, height + .18f, rz, width + .27f, .06f, .17f, "RoofRidge", new Color(.9f, .48f, .25f), false);
        for (float wz = z - depth / 2 + 2f; wz < z + depth / 2 - 1; wz += 3.1f)
        {
            float side = x < 0 ? 1 : -1;
            Box(name + " / window", x + side * (width / 2 + .02f), 1.9f, wz, .08f, 1.2f, .8f, "Window", new Color(.12f, .34f, .39f), false);
            Box(name + " / window sill", x + side * (width / 2 + .13f), 1.24f, wz, .25f, .14f, 1.03f, "Cream", new Color(.93f, .89f, .77f), false);
        }
    }

    static void Awning(float x, float z, string name)
    {
        Box(name + " / counter", x, .52f, z, 1.7f, 1.04f, 2.4f, "Wood", new Color(.55f, .37f, .22f));
        for (int i = 0; i < 6; i++)
            Box(name + " / stripe " + i, x, 2.24f, z - 1.18f + i * .46f, 2.2f, .11f, .43f,
                i % 2 == 0 ? "Orange" : "Cream", i % 2 == 0 ? new Color(.94f, .52f, .21f) : new Color(.93f, .89f, .77f), false);
        for (int end = -1; end <= 1; end += 2)
            Box(name + " / awning post", x + .86f, 1.6f, z + end * 1.1f, .08f, 3.2f, .08f, "Iron", new Color(.15f, .28f, .29f), false);
    }

    static Light Lamp(float x, float z)
    {
        Box("Street lamp / post", x, 1.4f, z, .12f, 2.8f, .12f, "Iron", new Color(.15f, .28f, .29f), false);
        Box("Street lamp / glass", x, 3f, z, .45f, .55f, .45f, "LampGlass", new Color(.95f, .8f, .49f), false);
        var light = new GameObject("Emergency lamp").AddComponent<Light>();
        light.transform.position = new Vector3(x, 3f, z);
        light.type = LightType.Point;
        light.color = new Color(1f, .78f, .42f);
        light.range = 6f;
        light.intensity = 2f;
        light.shadows = LightShadows.None;
        return light;
    }

    static GameObject Robot(string name, float x, float z, string paint, Color color)
    {
        var root = new GameObject(name);
        root.transform.position = new Vector3(x, 0, z);
        Transform old = section;
        section = root.transform;
        Shape(name + " / body", PrimitiveType.Capsule, new Vector3(x, 1, z), new Vector3(.75f, 1f, .65f), paint, color, false);
        Shape(name + " / eye", PrimitiveType.Sphere, new Vector3(x, 1.5f, z + .31f), new Vector3(.38f, .13f, .13f), "Cyan", new Color(.31f, .9f, .84f), false);
        section = old;
        var capsule = root.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0, 1, 0);
        capsule.height = 2f;
        capsule.radius = .39f;
        root.AddComponent<SecurityTarget>();
        return root;
    }

    [MenuItem("Skybreak/Build starter scene")]
    public static string Build()
    {
        var original = SceneManager.GetActiveScene();
        if (original.isDirty) throw new InvalidOperationException("Save the open scene before building Skybreak.");
        if (File.Exists(ScenePath)) throw new InvalidOperationException("Starter scene already exists; preserving it.");
        Directory.CreateDirectory("Assets/Skybreak/Scenes");
        Directory.CreateDirectory(MaterialDirectory);
        AssetDatabase.Refresh();
        Materials.Clear();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "SkybreakStarter";
        RenderSettings.ambientLight = new Color(.73f, .81f, .78f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(.82f, .89f, .86f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 45f;
        RenderSettings.fogEndDistance = 120f;

        section = new GameObject("01 / STONE DISTRICT").transform;
        Box("Raised stone district", 0, -.33f, 0, 49f, .66f, 82f, "Stone", new Color(.74f, .72f, .63f));
        for (int z = -38; z <= 38; z += 3)
            for (int x = -22; x <= 22; x += 3)
                Box("Paving stone", x, .025f, z, 2.94f, .045f, 2.94f, (x + z) % 2 == 0 ? "PavingA" : "PavingB",
                    (x + z) % 2 == 0 ? new Color(.82f, .8f, .71f) : new Color(.75f, .77f, .69f), false);
        foreach (float x in new float[] { -24f, 24f })
            Box("Perimeter stone wall", x, .7f, 0, .7f, 1.4f, 80, "Stone", new Color(.74f, .72f, .63f));
        foreach (float z in new float[] { -40f, 40f })
            Box("Perimeter stone wall", 0, .7f, z, 48, 1.4f, .7f, "Stone", new Color(.74f, .72f, .63f));
        House("Depot west", -19, 28, 8, 15, 5.5f, "Teal", new Color(.22f, .49f, .51f));
        House("Depot east", 19, 28, 8, 15, 5.5f, "Teal", new Color(.22f, .49f, .51f));
        House("Market west A", -12, 9, 11, 14, 3.2f, "Teal", new Color(.22f, .49f, .51f));
        House("Market west B", -12, -7, 11, 10, 3.2f, "Teal", new Color(.22f, .49f, .51f));
        House("Market east A", 15, 7, 9, 17, 3.2f, "TealLight", new Color(.35f, .58f, .55f));
        House("Market east B", 15, -10, 9, 9, 3.2f, "TealLight", new Color(.35f, .58f, .55f));
        House("Tower west", -17, -28, 9, 15, 5.5f, "Cream", new Color(.93f, .89f, .77f));
        House("Tower east", 17, -28, 9, 15, 5.5f, "Cream", new Color(.93f, .89f, .77f));
        House("Emergency tower", 0, -36, 8, 5, 16, "Cream", new Color(.93f, .89f, .77f));
        for (int side = -1; side <= 1; side += 2)
        {
            var ramp = Box("Rooftop ramp", side < 0 ? -12 : 15, 1.56f, 20, 5, .38f, 9, "Stone", new Color(.74f, .72f, .63f));
            ramp.transform.rotation = Quaternion.Euler(20, 0, 0);
            for (int i = 0; i < 6; i++)
                Box("Cyan route marker", side < 0 ? -12 : 15, 3.25f, 13 - i, .16f, .04f, .58f, "Cyan", new Color(.31f, .9f, .84f), false);
        }
        foreach (Vector3 c in new Vector3[] { new Vector3(-5, 0, 27), new Vector3(6, 0, 28), new Vector3(-3, 0, 15), new Vector3(2, 0, 6), new Vector3(-3, 0, -6), new Vector3(9, 0, -18), new Vector3(-6, 0, -26) })
            Box("Cargo crate", c.x, .55f, c.z, 1.65f, 1.1f, 1.65f, "Wood", new Color(.55f, .37f, .22f));
        section = new GameObject("02 / MARKET AND ROUTE").transform;
        for (int i = 0; i < 5; i++)
        {
            Awning(-5.8f, 11 - i * 5, "West market " + i);
            Awning(8.6f, 10 - i * 5, "East market " + i);
        }
        Vector3[] path = { new Vector3(0, 0, 22), new Vector3(0, 0, 12), new Vector3(6, 0, 12), new Vector3(6, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 0, -15), new Vector3(0, 0, -28) };
        for (int i = 1; i < path.Length; i++)
        {
            Vector3 a = path[i - 1], b = path[i];
            Vector3 center = (a + b) / 2;
            var line = Box("Orange sled route", center.x, .065f, center.z, .38f, .045f, Vector3.Distance(a, b), "Orange", new Color(.94f, .52f, .21f), false);
            line.transform.rotation = Quaternion.Euler(0, Vector3.SignedAngle(Vector3.forward, b - a, Vector3.up), 0);
        }
        var lights = new List<Light>();
        foreach (Vector2 at in new Vector2[] { new Vector2(-9, 32), new Vector2(10, 31), new Vector2(-9, -8), new Vector2(9, -13), new Vector2(-9, -29), new Vector2(9, -30) })
            lights.Add(Lamp(at.x, at.y));
        var coreDisplay = Shape("Tower / cyan power socket", PrimitiveType.Sphere, new Vector3(0, 7.2f, -33.34f), new Vector3(1.8f, 1.8f, .45f), "Cyan", new Color(.31f, .9f, .84f), false);
        var ring = Shape("Tower / orange socket rim", PrimitiveType.Cylinder, new Vector3(0, 7.2f, -33.52f), new Vector3(2.25f, .18f, 2.25f), "Orange", new Color(.94f, .52f, .21f), false);
        ring.transform.rotation = Quaternion.Euler(90, 0, 0);
        section = null;

        var sun = new GameObject("Warm afternoon sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(44, -35, 0);
        sun.intensity = 1.35f;
        sun.color = new Color(1f, .91f, .78f);
        sun.shadows = LightShadows.Soft;
        RenderSettings.sun = sun;

        var player = new GameObject("ARC / rescue hero");
        player.transform.position = new Vector3(0, .08f, 31);
        var controller = player.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = .36f;
        controller.center = new Vector3(0, .9f, 0);
        controller.stepOffset = .4f;
        controller.slopeLimit = 55;
        section = player.transform;
        Shape("Arc / teal suit", PrimitiveType.Capsule, new Vector3(0, 1f, 31), new Vector3(.65f, .86f, .47f), "Teal", new Color(.22f, .49f, .51f), false);
        Shape("Arc / head", PrimitiveType.Sphere, new Vector3(0, 1.73f, 31), new Vector3(.48f, .48f, .48f), "Cream", new Color(.93f, .89f, .77f), false);
        Box("Arc / orange harness", 0, 1.2f, 31.31f, .5f, .54f, .11f, "Orange", new Color(.94f, .52f, .21f), false);
        section = null;
        var camera = new GameObject("Third person camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.fieldOfView = 68;
        camera.backgroundColor = new Color(.82f, .89f, .86f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.gameObject.AddComponent<AudioListener>();
        var arc = player.AddComponent<ArcController>();
        arc.viewCamera = camera;
        camera.transform.SetPositionAndRotation(new Vector3(-.72f, 2.28f, 35.6f), Quaternion.Euler(0, 180, 0));

        section = new GameObject("03 / CHARACTERS AND OBJECTIVE").transform;
        Shape("RELAY / companion placeholder", PrimitiveType.Sphere, new Vector3(-2, 1.8f, 29), new Vector3(.8f, .8f, .8f), "Cream", new Color(.93f, .89f, .77f), false);
        Shape("RELAY / cyan eye", PrimitiveType.Sphere, new Vector3(-2, 1.84f, 28.58f), new Vector3(.35f, .22f, .13f), "Cyan", new Color(.31f, .9f, .84f), false);
        var sled = Box("CORE SLED / escort objective", 0, .5f, 22, 2.05f, .72f, 2.55f, "Orange", new Color(.94f, .52f, .21f));
        var power = Shape("Sled / cyan core", PrimitiveType.Cylinder, new Vector3(0, 1.1f, 22), new Vector3(.92f, 1.25f, .92f), "Cyan", new Color(.31f, .9f, .84f), false);
        power.transform.rotation = Quaternion.Euler(0, 0, 90);
        power.transform.SetParent(sled.transform, true);
        Robot("STRIKER / practice target", -3, 16, "Striker", new Color(.83f, .25f, .19f));
        Robot("GUNNER / practice target", 5, 5, "Gunner", new Color(.95f, .7f, .16f));
        Robot("BASTION / practice target", -3, -8, "Bastion", new Color(.21f, .54f, .8f));
        Robot("GUNNER / tower target", 4, -26, "Gunner", new Color(.95f, .7f, .16f));
        section = null;
        var mission = new GameObject("MISSION / recover, escort, restore").AddComponent<SkybreakMission>();
        mission.arc = arc;
        mission.sled = sled.transform;
        mission.towerCore = coreDisplay.GetComponent<Renderer>();
        mission.districtLights = lights.ToArray();

        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Unity did not save " + ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = player;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Frame(new Bounds(new Vector3(0, 1, -1), new Vector3(50, 20, 84)), false);
        return "Created " + ScenePath + " with Arc, the city route, four practice targets and a working core mission.";
    }
}