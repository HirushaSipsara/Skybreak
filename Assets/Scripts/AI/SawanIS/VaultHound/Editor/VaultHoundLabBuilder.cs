#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Skybreak;
using Skybreak.AI.SawanIS.VaultHound;
using Skybreak.AI.SawanIS.VaultHound.Testing;

namespace Skybreak.AI.SawanIS.VaultHound.Editor
{
    /// <summary>
    /// Programmatically generates an isolated test arena scene for Vault Hound.
    /// Menu: Tools → SawanIS → Build Vault Hound Lab Scene
    /// </summary>
    public static class VaultHoundLabBuilder
    {
        const string SceneDir   = "Assets/Scenes/Tests/SawanIS";
        const string ScenePath  = "Assets/Scenes/Tests/SawanIS/VaultHoundLab.unity";
        const string PrefabPath = "Assets/Prefabs/Enemies/VaultHound.prefab";

        [MenuItem("Tools/SawanIS/Build Vault Hound Lab Scene")]
        public static void BuildScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes/Tests/SawanIS"))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Scenes/Tests"))
                {
                    AssetDatabase.CreateFolder("Assets/Scenes", "Tests");
                }
                AssetDatabase.CreateFolder("Assets/Scenes/Tests", "SawanIS");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Lighting & Environment
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 2. Arena Geometry
            var arenaRoot = new GameObject("Arena");

            // Floor (40m x 40m)
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.parent = arenaRoot.transform;
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(40f, 1f, 40f);

            // Perimeter walls
            CreateWall("Wall_North", arenaRoot.transform, new Vector3(0f, 2f, 20f), new Vector3(40f, 4f, 1f));
            CreateWall("Wall_South", arenaRoot.transform, new Vector3(0f, 2f, -20f), new Vector3(40f, 4f, 1f));
            CreateWall("Wall_East",  arenaRoot.transform, new Vector3(20f, 2f, 0f), new Vector3(1f, 4f, 40f));
            CreateWall("Wall_West",  arenaRoot.transform, new Vector3(-20f, 2f, 0f), new Vector3(1f, 4f, 40f));

            // Central divider with 2 door openings (left at -6, right at +6)
            CreateWall("Divider_Left",   arenaRoot.transform, new Vector3(-14f, 2f, 0f), new Vector3(12f, 4f, 1f));
            CreateWall("Divider_Center", arenaRoot.transform, new Vector3(0f, 2f, 0f),   new Vector3(6f, 4f, 1f));
            CreateWall("Divider_Right",  arenaRoot.transform, new Vector3(14f, 2f, 0f),  new Vector3(12f, 4f, 1f));

            // Test Obstacle (blocks right door opening at (6, 2, 0))
            var obstacle = CreateWall("TestObstacle_DoorBlocker", arenaRoot.transform, new Vector3(6f, 2f, 0f), new Vector3(4f, 4f, 1f));
            obstacle.SetActive(false); // starts open

            // 3. Grid Manager
            var gridGo = new GameObject("VaultHoundGridManager");
            var grid = gridGo.AddComponent<VaultHoundGrid>();
            var serGrid = new SerializedObject(grid);
            serGrid.FindProperty("origin").vector3Value = new Vector3(-20f, 0f, -20f);
            serGrid.FindProperty("width").intValue = 40;
            serGrid.FindProperty("height").intValue = 40;
            serGrid.FindProperty("cellSize").floatValue = 1f;
            serGrid.FindProperty("drawGizmos").boolValue = true;
            serGrid.ApplyModifiedProperties();

            // 4. Hound Pack Coordinator
            var packGo = new GameObject("VaultHoundPack");
            var pack = packGo.AddComponent<HoundPack>();

            // 5. Target Dummy (Player)
            var targetGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            targetGo.name = "TargetDummy_Player";
            targetGo.tag = "Player";
            targetGo.transform.position = new Vector3(0f, 1f, 12f);
            targetGo.AddComponent<CombatHealth>();

            // 6. Spawn Vault Hounds
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject houndAGo = null;
            GameObject houndBGo = null;
            VaultHoundBrain brainA = null;
            VaultHoundBrain brainB = null;

            if (prefab != null)
            {
                houndAGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                houndAGo.name = "VaultHound_A (Pursuer)";
                houndAGo.transform.position = new Vector3(-6f, 0f, -12f);
                brainA = houndAGo.GetComponent<VaultHoundBrain>();

                houndBGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                houndBGo.name = "VaultHound_B (Flanker)";
                houndBGo.transform.position = new Vector3(6f, 0f, -12f);
                brainB = houndBGo.GetComponent<VaultHoundBrain>();

                WireBrain(brainA, grid, pack);
                WireBrain(brainB, grid, pack);
            }

            // 7. Test Harness
            var harnessGo = new GameObject("LabHarness");
            var harness = harnessGo.AddComponent<VaultHoundLabHarness>();
            var serHarness = new SerializedObject(harness);
            serHarness.FindProperty("houndA").objectReferenceValue = brainA;
            serHarness.FindProperty("houndB").objectReferenceValue = brainB;
            serHarness.FindProperty("pack").objectReferenceValue = pack;
            serHarness.FindProperty("grid").objectReferenceValue = grid;
            serHarness.FindProperty("target").objectReferenceValue = targetGo.transform;
            serHarness.FindProperty("testObstacle").objectReferenceValue = obstacle;
            serHarness.ApplyModifiedProperties();

            // 8. Camera setup
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 22f, -22f);
            camGo.transform.rotation = Quaternion.Euler(45f, 0f, 0f);

            // Save Scene
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[VaultHoundLabBuilder] Lab scene created at {ScenePath}.");
        }

        static GameObject CreateWall(string name, Transform parent, Vector3 pos, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.parent = parent;
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            return wall;
        }

        static void WireBrain(VaultHoundBrain brain, VaultHoundGrid grid, HoundPack pack)
        {
            if (brain == null) return;
            var ser = new SerializedObject(brain);
            var gProp = ser.FindProperty("grid");
            if (gProp != null) gProp.objectReferenceValue = grid;
            var pProp = ser.FindProperty("pack");
            if (pProp != null) pProp.objectReferenceValue = pack;
            ser.ApplyModifiedProperties();
        }
    }
}
#endif
