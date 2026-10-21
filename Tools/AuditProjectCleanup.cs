using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class AuditProjectCleanup
{
    public const string MainScene = "Assets/Scenes/GameLevel/SkybreakMain.unity";
    public const string ReportDir = "Docs/Maintenance/2026-10-11-cleanup";
    static readonly string[] ProtectedPrefixes = {
        "Assets/Scripts/", "Assets/LocalCombatLab/", "Assets/Settings/", "Assets/Data/",
        "Assets/Audio/", "Assets/Navigation/", "Assets/Animations/", "Assets/Lighting/",
        "Assets/UI/", "Assets/Shaders/", "Assets/Tools/", "Assets/Models/Custom/",
        "Assets/Prefabs/Environment/Decoration/Custom/", "Assets/Materials/Characters/OwnedAgents/"
    };
    static HashSet<string> Kept()
    {
        var roots = new HashSet<string>(StringComparer.Ordinal) { MainScene,
            "Assets/Prefabs/Companions/Relay/Relay.prefab",
            "Assets/Prefabs/Enemies/RiftWarden/RiftWarden.prefab" };
        foreach(var p in AssetDatabase.GetAllAssetPaths())
            if(p.StartsWith("Assets/") && !AssetDatabase.IsValidFolder(p) &&
               (ProtectedPrefixes.Any(x=>p.StartsWith(x)) || p.Contains("/Resources/") || p.Contains("/StreamingAssets/"))) roots.Add(p);
        // Project-wide render/input configuration is not necessarily a scene dependency.
        foreach(var f in Directory.GetFiles("ProjectSettings", "*.asset"))
            foreach(Match m in Regex.Matches(File.ReadAllText(f), @"guid:\s*([0-9a-f]{32})")) {
                var p=AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);if(p.StartsWith("Assets/") && !p.EndsWith(".unity"))roots.Add(p);
            }
        return new HashSet<string>(AssetDatabase.GetDependencies(roots.ToArray(), true), StringComparer.Ordinal);
    }
    static string[] Candidates(HashSet<string> kept) => AssetDatabase.GetAllAssetPaths()
        .Where(p=>p.StartsWith("Assets/") && !AssetDatabase.IsValidFolder(p) && File.Exists(p) &&
            (!p.EndsWith(".cs") || p.StartsWith("Assets/TutorialInfo/")) && !p.EndsWith(".asmdef") && !p.EndsWith(".asmref") &&
            !p.EndsWith(".dll") && !p.EndsWith(".gitkeep") && !kept.Contains(p))
        .OrderBy(p=>p).ToArray();
    public static string Audit()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play mode before auditing assets");
        Directory.CreateDirectory(ReportDir);
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.isDirty)throw new Exception("Unsaved scene changes need preserving before cleanup");
        if(scene.path!=MainScene)EditorSceneManager.OpenScene(MainScene);
        var kept=Kept();var candidates=Candidates(kept);
        File.WriteAllLines(ReportDir+"/retained-assets.txt",kept.Where(p=>p.StartsWith("Assets/")).OrderBy(p=>p));
        File.WriteAllLines(ReportDir+"/unused-assets.tsv",new[]{"bytes\tguid\tpath"}.Concat(candidates.Select(p=>new FileInfo(p).Length+"\t"+AssetDatabase.AssetPathToGUID(p)+"\t"+p)));
        return "Retained assets: "+kept.Count+". Unused candidates: "+candidates.Length+" / "+candidates.Sum(p=>new FileInfo(p).Length)+" bytes. Scene: "+MainScene;
    }
    public static string DeleteReviewed()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play mode before removing assets");
        var kept=Kept();var current=new HashSet<string>(Candidates(kept));
        var reviewed=File.ReadAllLines(ReportDir+"/unused-assets.tsv").Skip(1).Select(l=>l.Split('\t')[2]).ToArray();
        if(reviewed.Any(p=>!current.Contains(p)))throw new Exception("Dependencies changed since review");
        var deleted=new List<string>();
        // Remove disabled demo entries before deleting their scenes.
        EditorBuildSettings.scenes=EditorBuildSettings.scenes.Where(s=>!reviewed.Contains(s.path)).ToArray();
        AssetDatabase.StartAssetEditing();
        try { foreach(var p in reviewed) { if(!AssetDatabase.DeleteAsset(p))throw new Exception("Delete failed: "+p);deleted.Add(p); } }
        finally { AssetDatabase.StopAssetEditing();File.WriteAllLines(ReportDir+"/deleted-assets.txt",deleted); }
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        return "Removed "+deleted.Count+" reviewed unused assets through AssetDatabase.";
    }
    public static string Verify()
    {
        if(EditorApplication.isPlaying)throw new Exception("Verify in Edit mode");
        var scene=EditorSceneManager.OpenScene(MainScene);
        var roots=scene.GetRootGameObjects();
        var ts=roots.SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        int missing=ts.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
        int meshes=roots.SelectMany(g=>g.GetComponentsInChildren<MeshFilter>(true)).Count(m=>m.sharedMesh==null);
        int skinMeshes=roots.SelectMany(g=>g.GetComponentsInChildren<SkinnedMeshRenderer>(true)).Count(m=>m.sharedMesh==null);
        int materials=roots.SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).Sum(r=>r.sharedMaterials.Count(m=>m==null||m.shader==null));
        var boss=UnityEngine.Object.FindAnyObjectByType<Skybreak.AI.RiftWarden.RiftWardenBrain>();
        var relay=UnityEngine.Object.FindAnyObjectByType<Skybreak.AI.Relay.RelayBrain>();
        var surfaces=UnityEngine.Object.FindObjectsByType<Unity.AI.Navigation.NavMeshSurface>();
        if(missing+meshes+skinMeshes+materials!=0||boss==null||relay==null||surfaces.Length!=2||surfaces.Any(s=>s.navMeshData==null)||boss.presenter.clips.Length!=20||boss.presenter.clips.Any(c=>c==null)||LightmapSettings.lightmaps.Length!=5||LightmapSettings.lightProbes.count!=506)throw new Exception("Main scene verification failed: missing scripts="+missing+", meshes="+(meshes+skinMeshes)+", materials="+materials);
        var report="PASS: Main scene opens; zero missing scripts, meshes, materials or shaders.\nPASS: Relay and Rift Warden retained; 20 boss animation clips.\nPASS: Both navigation surfaces retain NavMesh data.\nPASS: Five baked lightmap atlases and 506 light probes retained.\n";
        File.WriteAllText(ReportDir+"/scene-verification.txt",report);
        return report;
    }
    public static string RemoveUnusedPackages()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play mode before changing packages");
        UnityEditor.PackageManager.Client.AddAndRemove(null, new[]{"com.unity.ai.assistant", "com.unity.ai.inference"});
        return "Requested removal of unused Assistant and inference packages through Unity Package Manager; verify resolved manifest and Editor compilation after reload.";
    }
}
