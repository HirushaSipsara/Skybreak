using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Skybreak.AI.SawanIS.VaultHound;

public static class CheckReleaseReadiness
{
    const string Report = "Docs/GameFlow/Verification/ReleaseChecks.txt";
    public static string Assets()
    {
        const string main = "Assets/Scenes/GameLevel/SkybreakMain.unity";
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Require(scene.path == main, "Main map is loaded.");
        File.WriteAllText(Report, "Skybreak combined main-map validation\n");
        int objects = 0;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                objects++;
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, "Missing script: " + t.name);
                foreach (var renderer in t.GetComponents<MeshRenderer>())
                {
                    var mesh = renderer.GetComponent<MeshFilter>();
                    Require(mesh != null && mesh.sharedMesh != null, "Missing mesh: " + t.name);
                    Require(renderer.sharedMaterials.All(m => m != null && m.shader != null && m.shader.isSupported), "Missing/unsupported material: " + t.name);
                }
                foreach (var renderer in t.GetComponents<SkinnedMeshRenderer>())
                {
                    Require(renderer.sharedMesh != null, "Missing skinned mesh: " + t.name);
                    Require(renderer.sharedMaterials.All(m => m != null && m.shader != null && m.shader.isSupported), "Missing skinned material: " + t.name);
                }
            }
        File.AppendAllText(Report, "PASS: " + objects + " main-map objects have no missing scripts, meshes or material shaders.\n");
        var dependencies = AssetDatabase.GetDependencies(main, true).Where(p => p.StartsWith("Assets/")).ToArray();
        foreach (string path in dependencies)
        {
            Require(File.Exists(path), "Missing scene dependency: " + path);
            using (var reader = new StreamReader(path))
            {
                var first = new char[43]; int count = reader.Read(first, 0, first.Length);
                Require(!new string(first, 0, count).StartsWith("version https://git-lfs.github.com/spec/v1"), "Unresolved LFS pointer: " + path);
            }
        }
        File.AppendAllText(Report, "PASS: " + dependencies.Length + " main-map dependencies exist with no unresolved Git LFS pointers.\n");
        Require(EditorBuildSettings.scenes.Count(s => s.enabled && s.path == main) == 1 && EditorBuildSettings.scenes.Count(s => s.enabled) == 1, "Default build scene selection is incorrect.");
        var hounds = UnityEngine.Object.FindObjectsByType<VaultHoundBrain>();
        Require(hounds.Length == 2, "Expected two Hounds.");
        foreach (var h in hounds)
        {
            var so = new SerializedObject(h);
            Require(Mathf.Approximately(so.FindProperty("pursueSpeed").floatValue, 6.2f) && Mathf.Approximately(so.FindProperty("deploySpeed").floatValue, 4), "Hound speed mismatch.");
            Require(h.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh.isReadable, "Hound mesh not readable for breakup.");
        }
        File.AppendAllText(Report, "PASS: Main scene selected for builds; both Hounds retain faster speeds and readable breakup geometry.\n");
        return File.ReadAllText(Report);
    }
    public static string Build()
    {
        Require(!EditorApplication.isPlaying, "Stop Play Mode before building.");
        Directory.CreateDirectory("Builds/Verification");
        EditorApplication.delayCall += () =>
        {
            try
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/GameLevel/SkybreakMain.unity" },
                    locationPathName = "Builds/Verification/Skybreak.exe",
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });
                string status = report.summary.result == BuildResult.Succeeded ? "PASS: " : report.summary.result == BuildResult.Cancelled ? "NOT COMPLETED: " : "FAIL: ";
                File.AppendAllText(Report, status + "Windows development build " + report.summary.result + "; errors=" + report.summary.totalErrors + ", warnings=" + report.summary.totalWarnings + ".\n");
            }
            catch (Exception e) { File.AppendAllText(Report, "FAIL: Windows build " + e + "\n"); Debug.LogException(e); }
        };
        return "Queued Windows development build to ignored Builds/Verification/Skybreak.exe.";
    }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
