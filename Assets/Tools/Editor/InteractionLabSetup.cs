#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Skybreak.PhysicsInteraction;
using Skybreak.PhysicsInteraction.Testing;

namespace Skybreak.Editor
{
    /// <summary>
    /// Populates InteractionLab.unity with the 4 interactive physics demonstration stations:
    /// Station 1: Kinematic Security Door with Anti-Crush OverlapBox safety.
    /// Station 2: Movable Heavy Barricade with floor friction damping and AI grid sync.
    /// Station 3: Throwable / Carryable Physics Props with spring follow and impulse launch.
    /// Station 4: Combat Target Dummy to demonstrate kinetic impact damage (Ek = 0.5*m*v^2).
    /// Menu: Tools → SawanGV → Setup Interaction Lab Scene
    /// </summary>
    public static class InteractionLabSetup
    {
        const string ScenePath        = "Assets/Scenes/Tests/Student2Interactions/InteractionLab.unity";
        const string DoorPrefabPath   = "Assets/Prefabs/InteractiveObjects/Doors/SecurityDoor.prefab";
        const string PlayerPrefabPath = "Assets/Prefabs/Player/ArcExplorer.prefab";
        const string MetalPhysMatPath = "Assets/Materials/Physics/PM_MetalProp.physicMaterial";
        const string BarricadePhysMat = "Assets/Materials/Physics/PM_HeavyBarricade.physicMaterial";

        [MenuItem("Tools/SawanGV/Setup Interaction Lab Scene")]
        public static void SetupLab()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Load physics materials
            var metalMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(MetalPhysMatPath);
            var barricadeMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(BarricadePhysMat);

            // 1. Station 1: Security Door
            var existingDoor = GameObject.Find("SecurityDoor");
            if (existingDoor == null)
            {
                var doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
                if (doorPrefab != null)
                {
                    var door = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab);
                    door.name = "SecurityDoor";
                    door.transform.position = Vector3.zero;
                    door.transform.rotation = Quaternion.identity;
                }
            }

            // 2. Station 2: Heavy Barricade
            var existingBarricade = GameObject.Find("Station2_HeavyBarricade");
            if (existingBarricade == null)
            {
                var barricade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                barricade.name = "Station2_HeavyBarricade";
                barricade.transform.position = new Vector3(5f, 0.75f, 1f);
                barricade.transform.localScale = new Vector3(2f, 1.5f, 0.8f);

                var rb = barricade.AddComponent<Rigidbody>();
                rb.mass = 120f;
                rb.linearDamping = 3.2f;
                rb.angularDamping = 4.5f;

                var boxCol = barricade.GetComponent<BoxCollider>();
                if (barricadeMat != null) boxCol.material = barricadeMat;

                barricade.AddComponent<PhysicsBarricade>();
            }

            // 3. Station 3: Throwable Physics Props (Crates)
            for (int i = 0; i < 2; i++)
            {
                string propName = $"Station3_PhysicsProp_Crate_{i + 1}";
                if (GameObject.Find(propName) == null)
                {
                    var prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    prop.name = propName;
                    prop.transform.position = new Vector3(-5f, 0.5f, 0f + i * 2f);
                    prop.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);

                    var rb = prop.AddComponent<Rigidbody>();
                    rb.mass = 10f;
                    rb.linearDamping = 0.05f;

                    var boxCol = prop.GetComponent<BoxCollider>();
                    if (metalMat != null) boxCol.material = metalMat;

                    prop.AddComponent<PhysicsProp>();
                }
            }

            // 4. Station 4: Target Dummy
            var existingDummy = GameObject.Find("Station4_TargetDummy");
            if (existingDummy == null)
            {
                var dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                dummy.name = "Station4_TargetDummy";
                dummy.transform.position = new Vector3(0f, 1f, 8f);
                dummy.AddComponent<CombatHealth>();
            }

            // 5. Lab Harness HUD
            var existingHarness = GameObject.Find("InteractionLabHarness");
            if (existingHarness == null)
            {
                var harnessGo = new GameObject("InteractionLabHarness");
                harnessGo.AddComponent<InteractionLabHarness>();
            }

            // 6. Player
            var existingPlayer = GameObject.Find("ArcExplorer");
            if (existingPlayer == null)
            {
                var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
                if (playerPrefab != null)
                {
                    var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                    player.name = "ArcExplorer";
                    player.transform.position = new Vector3(0f, 0f, -4f);
                    player.transform.rotation = Quaternion.identity;
                }
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[InteractionLabSetup] InteractionLab fully populated with 4 physics demonstration stations!");
        }
    }
}
#endif
