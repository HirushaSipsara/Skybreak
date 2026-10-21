using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Skybreak.AI.Disruptor;

public static class CheckDisruptorBreakup
{
    const string Folder = "Docs/IS/RiftDisruptor/MainMap";
    public static string Main()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Enter Play Mode first.");
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "/BreakupChecks.txt", "Rift Disruptor breakup checks\n");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/RiftDisruptor/RiftDisruptor.prefab");
        var clone = UnityEngine.Object.Instantiate(prefab, new Vector3(0, 0, 22), Quaternion.identity);
        clone.name = "Disruptor breakup test (runtime)";
        clone.transform.localScale = new Vector3(.9f, 1.1f, .95f);
        var brain = clone.GetComponent<DisruptorBrain>(); brain.decisionsEnabled = false;
        var fx = clone.GetComponent<DisruptorVisuals>();
        var sourceParts = clone.GetComponentsInChildren<MeshRenderer>();
        var before = sourceParts.Select(x => x.enabled).ToArray();
        var scale = sourceParts.ToDictionary(x => x.name + " fragment", x => x.transform.lossyScale);
        float oldTimeScale = Time.timeScale; bool oldBackground = Application.runInBackground;
        Application.runInBackground = true; Time.timeScale = 1;
        int stage = 0; float start = Time.time; GameObject pieces = null;
        brain.ReceiveDamage(10000, null);
        EditorApplication.CallbackFunction tick = null;
        tick = () =>
        {
            bool done = false;
            try
            {
                if (!EditorApplication.isPlaying) throw new Exception("Play Mode ended during the check.");
                if (stage == 0 && Time.time - start > fx.breakupDelay + .15f)
                {
                    pieces = GameObject.Find("Rift Disruptor broken pieces (runtime)");
                    if (pieces == null) throw new Exception("No broken pieces spawned.");
                    var bodies = pieces.GetComponentsInChildren<Rigidbody>();
                    if (bodies.Length != 18) throw new Exception("Expected 18 model parts, got " + bodies.Length);
                    if (brain.Alive || sourceParts.Any(x => x.enabled) || clone.GetComponentsInChildren<Collider>().Any(x => x.enabled))
                        throw new Exception("Intact source remains alive, visible or collidable.");
                    foreach (var body in bodies)
                    {
                        // Rotating joints under the deliberately non-uniform test scale change lossyScale slightly.
                        Vector3 expected = scale[body.name], actual = body.transform.localScale;
                        if (Vector3.Distance(actual, expected) > expected.magnitude * .25f)
                            throw new Exception("Imported fragment scale changed: " + body.name + " expected " + expected + ", got " + actual);
                        if (Vector3.Distance(body.position, clone.transform.position) > 4) throw new Exception("Fragment spawned away from the body.");
                        foreach (var actor in Skybreak.Combat.DamageSink.Active)
                            foreach (var col in actor.GetComponentsInChildren<Collider>())
                                if (col.enabled && !Physics.GetIgnoreCollision(body.GetComponent<Collider>(), col)) throw new Exception("Debris can block an actor.");
                    }
                    Pass("Lethal damage produces 18 correctly positioned/scaled armor pieces, hides the source, disables its colliders and ignores characters.");
                    Capture(clone.transform.position);
                    stage = 1;
                }
                if (stage == 1 && Time.time - start > fx.breakupDelay + fx.debrisLifetime + .2f)
                {
                    if (pieces != null) throw new Exception("Debris did not expire.");
                    Pass("Debris expires after four seconds.");
                    brain.ResetAgent(); stage = 2; start = Time.time;
                }
                if (stage == 2 && Time.time - start > .1f)
                {
                    if (!sourceParts.Select(x => x.enabled).SequenceEqual(before) || !clone.GetComponent<CharacterController>().enabled || fx.presenter.Dead)
                        throw new Exception("Reset failed to restore the model, collider or animation state.");
                    Pass("ResetAgent restores armor visibility, collisions and animation state.");
                    brain.ReceiveDamage(10000, null); stage = 3; start = Time.time;
                }
                if (stage == 3 && Time.time - start > fx.breakupDelay + .15f)
                {
                    if (GameObject.Find("Rift Disruptor broken pieces (runtime)") == null) throw new Exception("Repeated defeat failed.");
                    clone.SetActive(false);
                    if (GameObject.Find("Rift Disruptor broken pieces (runtime)") != null) throw new Exception("Disable left active debris behind.");
                    Pass("Repeated defeat works; disabling the actor immediately clears its debris.");
                    done = true;
                }
            }
            catch (Exception ex) { File.AppendAllText(Folder + "/BreakupChecks.txt", "FAIL: " + ex + "\n"); Debug.LogError(ex); done = true; }
            if (!done) return;
            EditorApplication.update -= tick;
            if (clone != null) UnityEngine.Object.Destroy(clone);
            Time.timeScale = oldTimeScale; Application.runInBackground = oldBackground;
        };
        EditorApplication.update += tick;
        return "Checking actual prefab defeat, 18 fragments, correct scale, collisions, expiry, reset and repeated defeat.";
    }
    static void Pass(string text) { File.AppendAllText(Folder + "/BreakupChecks.txt", "PASS: " + text + "\n"); }
    static void Capture(Vector3 origin)
    {
        var camera = Camera.main; var position = camera.transform.position; var rotation = camera.transform.rotation;
        var target = camera.targetTexture; var active = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(1000, 800, 24, RenderTextureFormat.DefaultHDR);
        try
        {
            camera.transform.position = origin + new Vector3(3, 2, -4); camera.transform.LookAt(origin + Vector3.up);
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var image = new Texture2D(1000, 800, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1000, 800), 0, 0); image.Apply();
            File.WriteAllBytes(Folder + "/DisruptorBreakup.png", image.EncodeToPNG()); UnityEngine.Object.Destroy(image);
        }
        finally { camera.transform.SetPositionAndRotation(position, rotation); camera.targetTexture = target; RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); }
    }
}
