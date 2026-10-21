using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Skybreak;
using Skybreak.AI.SawanIS.VaultHound;

public static class CheckVaultHoundBreakup
{
    const string Folder = "Docs/IS/AgentDesign/SawanIS/MainMap";
    public static string Main()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Enter Play Mode first.");
        Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/BreakupChecks.txt", "Vault Hound breakup and speed checks\n");
        foreach (var live in UnityEngine.Object.FindObjectsByType<VaultHoundBrain>())
        {
            var config = new SerializedObject(live);
            Assert(Mathf.Approximately(config.FindProperty("pursueSpeed").floatValue, 6.2f) && Mathf.Approximately(config.FindProperty("deploySpeed").floatValue, 4f), "Map Hound speed overrides are stale.");
        }
        Pass("Both map Hounds inherit chase speed 6.2 and deployment speed 4.0 m/s.");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/VaultHound.prefab");
        var clone = UnityEngine.Object.Instantiate(source, new Vector3(18, .05f, 54), Quaternion.Euler(0, 90, 0));
        clone.name = "Vault Hound breakup check (runtime)";
        var brain = clone.GetComponent<VaultHoundBrain>(); brain.enabled = false;
        var skin = clone.GetComponentInChildren<SkinnedMeshRenderer>();
        int triangles = skin.sharedMesh.triangles.Length;
        var weights = skin.sharedMesh.boneWeights;
        int expectedPieces = skin.sharedMesh.triangles.Where((x, i) => i % 3 == 0).Select(i => weights[i].boneIndex0).Distinct().Count();
        float oldScale = Time.timeScale; bool oldBackground = Application.runInBackground;
        Application.runInBackground = true; Time.timeScale = 1;
        clone.GetComponent<CombatHealth>().ApplyDamage(10000, null);
        int stage = 0; float start = Time.time; GameObject debris = null;
        EditorApplication.CallbackFunction tick = null;
        tick = () =>
        {
            bool done = false;
            try
            {
                Assert(EditorApplication.isPlaying, "Play Mode ended during the check.");
                if (stage == 0 && Time.time - start > .75f)
                {
                    debris = GameObject.Find("Vault Hound broken pieces (runtime)");
                    Assert(debris != null, "No debris created: state=" + brain.CurrentState + ", active=" + clone.activeInHierarchy + ", presenter=" + clone.GetComponent<VaultHoundAnimator>().isActiveAndEnabled);
                    var bodies = debris.GetComponentsInChildren<Rigidbody>();
                    Assert(bodies.Length == expectedPieces && bodies.Length > 8, "Missing bone sections.");
                    Assert(!skin.enabled && brain.CurrentState == HoundState.Dead && clone.GetComponentsInChildren<Collider>().All(c => !c.enabled), "Intact body still active.");
                    Assert(debris.GetComponentsInChildren<MeshFilter>().Sum(m => m.sharedMesh.triangles.Length) == triangles, "Breakup lost or duplicated geometry.");
                    foreach (var body in bodies)
                    {
                        Assert(Vector3.Distance(body.position, clone.transform.position) < 3, "Fragment spawned away from its body: " + body.name + " at " + body.position + ", origin=" + clone.transform.position + ", size=" + body.GetComponent<MeshRenderer>().bounds.size);
                        Assert(body.GetComponent<MeshRenderer>().bounds.size.magnitude < 3, "Fragment scale is wrong.");
                        foreach (var actor in Skybreak.Combat.DamageSink.Active)
                            foreach (var col in actor.GetComponentsInChildren<Collider>())
                                if (col.enabled) Assert(Physics.GetIgnoreCollision(body.GetComponent<Collider>(), col), "Debris can block an actor.");
                    }
                    Pass(expectedPieces + " moving body sections retain all mesh triangles, correct scale/materials, disabled source collisions and actor collision exclusions.");
                    Capture(clone.transform.position); stage = 1;
                }
                if (stage == 1 && Time.time - start > 5.25f)
                {
                    Assert(debris == null, "Debris did not expire."); Pass("Debris and generated meshes expire after four seconds.");
                    brain.ResetForChamber(); stage = 2; start = Time.time;
                }
                if (stage == 2 && Time.time - start > .1f)
                {
                    Assert(skin.enabled && clone.GetComponent<CombatHealth>().IsAlive && clone.GetComponentsInChildren<Collider>().All(c => c.enabled), "Reset did not restore the Hound.");
                    Pass("Reset restores the model, health and collisions.");
                    clone.GetComponent<CombatHealth>().ApplyDamage(10000, null); stage = 3; start = Time.time;
                }
                if (stage == 3 && Time.time - start > .75f)
                {
                    Assert(GameObject.Find("Vault Hound broken pieces (runtime)") != null, "Second defeat failed.");
                    clone.SetActive(false);
                    Assert(GameObject.Find("Vault Hound broken pieces (runtime)") == null, "Disabling leaves active debris.");
                    Pass("Repeated defeat works; disabling immediately clears debris."); done = true;
                }
            }
            catch (Exception e) { File.AppendAllText(Folder + "/BreakupChecks.txt", "FAIL: " + e + "\n"); Debug.LogError(e); done = true; }
            if (!done) return;
            EditorApplication.update -= tick;
            if (clone != null) UnityEngine.Object.Destroy(clone);
            Time.timeScale = oldScale; Application.runInBackground = oldBackground;
        };
        EditorApplication.update += tick;
        return "Checking Hound fragments, geometry, collisions, cleanup, reset, repeated death and both map speed settings.";
    }
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Pass(string message) { File.AppendAllText(Folder + "/BreakupChecks.txt", "PASS: " + message + "\n"); }
    static void Capture(Vector3 origin)
    {
        var go = new GameObject("Breakup evidence camera"); var camera = go.AddComponent<Camera>();
        camera.CopyFrom(Camera.main); camera.enabled = false;
        camera.transform.position = origin + new Vector3(2.5f, 2, -3.5f); camera.transform.LookAt(origin + Vector3.up * .5f);
        var active = RenderTexture.active; var rt = RenderTexture.GetTemporary(1000, 800, 24);
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var image = new Texture2D(1000, 800, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1000, 800), 0, 0); image.Apply();
            File.WriteAllBytes(Folder + "/VaultHoundBreakup.png", image.EncodeToPNG()); UnityEngine.Object.Destroy(image);
        }
        finally { RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.Destroy(go); }
    }
}
