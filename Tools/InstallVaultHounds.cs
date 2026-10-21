using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Skybreak.Combat;
using Skybreak.AI.Disruptor;
using Skybreak.AI.SawanIS.VaultHound;
using Skybreak.PhysicsInteraction;

public static class InstallVaultHounds
{
    const string Scene = "Assets/Scenes/GameLevel/SkybreakMain.unity";
    const string Prefab = "Assets/Prefabs/Enemies/VaultHound.prefab";
    const int AgentType = -1372625422;
    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Run in Edit Mode.");
        EditorSceneManager.OpenScene(Scene);
        var existing = GameObject.Find("22 / Vault Hound chamber encounter");
        if (existing != null) throw new Exception("Vault Hound encounter already installed.");
        var model = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 8;
            if (model.GetComponent<VaultHoundDamageAdapter>() == null) model.AddComponent<VaultHoundDamageAdapter>();
            var agent = model.GetComponent<NavMeshAgent>(); agent.agentTypeID = AgentType;
            agent.radius = .45f; agent.height = 1.2f; agent.stoppingDistance = .05f;
            var animator = model.GetComponent<Animator>(); animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Set(model.GetComponent<VaultHoundBrain>(), "showDecisionOverlay", false);
            PrefabUtility.SaveAsPrefabAsset(model, Prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(model); }
        var root = new GameObject("22 / Vault Hound chamber encounter");
        var pack = root.AddComponent<HoundPack>(); var grid = root.AddComponent<VaultHoundGrid>();
        Set(grid, "width", 96); Set(grid, "height", 120); Set(grid, "cellSize", 1f);
        Set(grid, "origin", new Vector3(-20, 0, -10)); Set(grid, "obstacleMask", ~(1 << 8));
        Set(grid, "requireNavMesh", true); Set(grid, "agentTypeID", AgentType);
        var doors = UnityEngine.Object.FindObjectsByType<DungeonHoldingDoor>(FindObjectsSortMode.None).OrderBy(d => d.transform.position.x).ToArray();
        if (doors.Length != 2) throw new Exception("Expected the two existing holding gates.");
        var spawns = new[] { new Vector3(18, 0, 54), new Vector3(68, 0, 46) };
        var hounds = new VaultHoundBrain[2];
        for (int i = 0; i < hounds.Length; i++)
        {
            if (!NavMesh.SamplePosition(spawns[i], out var hit, 2, new NavMeshQueryFilter { agentTypeID = AgentType, areaMask = NavMesh.AllAreas }))
                throw new Exception("Hound spawn is outside the baked navigation mesh.");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
            go.name = i == 0 ? "Vault Hound / west chamber / pursuer" : "Vault Hound / east chamber / flanker";
            go.transform.SetParent(root.transform); go.transform.position = hit.position;
            Vector3 facing = doors[i].transform.position - hit.position; facing.y = 0;
            go.transform.rotation = Quaternion.LookRotation(facing);
            hounds[i] = go.GetComponent<VaultHoundBrain>(); Set(hounds[i], "grid", grid); Set(hounds[i], "pack", pack);
            Set(doors[i], "allowManualPreview", false); Set(doors[i], "startOpen", false);
        }
        var guards = UnityEngine.Object.FindObjectsByType<Skybreak.Warden.WardenBrain>(FindObjectsSortMode.None).Where(x => x.transform.position.z < 30).Cast<DamageSink>();
        var disruptors = UnityEngine.Object.FindObjectsByType<DisruptorBrain>(FindObjectsSortMode.None).Where(x => x.transform.position.z < 30).Cast<DamageSink>();
        var area = guards.Concat(disruptors).ToArray();
        if (area.Length != 4) throw new Exception("Expected two crystal guards and two Disruptors in the loading court.");
        var release = root.AddComponent<ChamberReleaseController>();
        SetArray(release, "area1Agents", area); SetArray(release, "holdingDoors", doors); SetArray(release, "hounds", hounds);
        Set(release, "grid", grid); Set(release, "pack", pack); Set(release, "logEvents", false);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        return "Installed two chambered Vault Hounds with shared pack/grid, real holding gates and four loading-court defeat inputs.";
    }
    static void Set(UnityEngine.Object target, string name, object value)
    {
        var so = new SerializedObject(target); var p = so.FindProperty(name);
        if (p == null) throw new Exception("Missing property " + name);
        if (value is UnityEngine.Object obj) p.objectReferenceValue = obj;
        else if (value is bool b) p.boolValue = b;
        else if (value is int i) p.intValue = i;
        else if (value is float f) p.floatValue = f;
        else if (value is Vector3 v) p.vector3Value = v;
        so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
    }
    static void SetArray(UnityEngine.Object target, string name, UnityEngine.Object[] values)
    {
        var so = new SerializedObject(target); var p = so.FindProperty(name); p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
    }
}
