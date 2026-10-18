// Menu: Skybreak > Rift Warden > Install Fight Audio
// Sets import settings for the Rift Warden fight audio and adds/updates RiftBossAudio on the boss in the
// open SkybreakMain scene and in the RiftWarden prefab. Run it outside Play mode, then save the scene.
using System.Linq;
using System.Text;
using Skybreak.AI.RiftWarden;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RiftAudioInstaller
{
    const string Dir = "Assets/Audio/Combat/RiftWarden";
    const string Prefab = "Assets/Prefabs/Enemies/RiftWarden/RiftWarden.prefab";

    static AudioClip Clip(string name, bool music, StringBuilder sb)
    {
        string path = $"{Dir}/{name}.wav";
        var imp = AssetImporter.GetAtPath(path) as AudioImporter;
        if (imp == null) { sb.AppendLine($"Missing: {path}"); return null; }
        var s = imp.defaultSampleSettings;
        bool intro = name.Contains("Intro");   // intro hum/roar stay stereo
        if (music)
        {
            imp.forceToMono = false; imp.loadInBackground = true;
            s.loadType = AudioClipLoadType.CompressedInMemory; s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = .7f;
        }
        else
        {
            imp.forceToMono = !intro; imp.loadInBackground = false;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = intro ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM; s.quality = .8f;
        }
        s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        imp.defaultSampleSettings = s;
        imp.SaveAndReimport();
        var c = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        sb.AppendLine($"{name}: {(c != null ? c.length.ToString("F1") + " s" : "not loaded")}");
        return c;
    }

    static void Assign(RiftBossAudio a, RiftWardenBrain brain, RiftBossAudio t)
    {
        a.brain = brain;
        a.introHum = t.introHum; a.introRoar = t.introRoar; a.crystalHits = t.crystalHits; a.slamBoom = t.slamBoom; a.deathShatter = t.deathShatter;
        a.musicStage1 = t.musicStage1; a.musicStage2 = t.musicStage2; a.musicStage3 = t.musicStage3;
    }

    [MenuItem("Skybreak/Rift Warden/Install Fight Audio")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Install Fight Audio", "Stop Play mode first, then run this again.", "OK");
            return;
        }
        var sb = new StringBuilder();
        AssetDatabase.ImportAsset("Assets/Audio/Combat/OwnedAgents/PlayerShot.wav", ImportAssetOptions.ForceUpdate);

        var holder = new GameObject("Rift audio installer (temp)") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var t = holder.AddComponent<RiftBossAudio>();
            t.introHum = Clip("RiftIntroHum", false, sb);
            t.introRoar = Clip("RiftIntroRoar", false, sb);
            t.crystalHits = new[] { Clip("RiftCrystalHit1", false, sb), Clip("RiftCrystalHit2", false, sb), Clip("RiftCrystalHit3", false, sb) };
            t.slamBoom = Clip("RiftSlamBoom", false, sb);
            t.deathShatter = Clip("RiftDeathShatter", false, sb);
            t.musicStage1 = Clip("RiftMusic_Stage1", true, sb);
            t.musicStage2 = Clip("RiftMusic_Stage2", true, sb);
            t.musicStage3 = Clip("RiftMusic_Stage3", true, sb);

            // prefab
            var root = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                var brain = root.GetComponentInChildren<RiftWardenBrain>(true);
                if (brain != null)
                {
                    var a = brain.GetComponent<RiftBossAudio>() ?? brain.gameObject.AddComponent<RiftBossAudio>();
                    Assign(a, brain, t);
                    PrefabUtility.SaveAsPrefabAsset(root, Prefab);
                    sb.AppendLine("Prefab: RiftBossAudio added/updated.");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            // every boss in the open scenes (main scene, or a lab scene if that is what's open)
            int bosses = 0;
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var brain in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RiftWardenBrain>(true)))
                {
                    var a = brain.GetComponent<RiftBossAudio>();
                    if (a == null) a = Undo.AddComponent<RiftBossAudio>(brain.gameObject);
                    else Undo.RecordObject(a, "Install Fight Audio");
                    Assign(a, brain, t);
                    EditorUtility.SetDirty(a);
                    EditorSceneManager.MarkSceneDirty(scene);
                    bosses++;
                }
            }
            sb.AppendLine($"Open scene(s): {bosses} boss(es) updated. Save the scene to keep it.");
        }
        finally { Object.DestroyImmediate(holder); }
        Debug.Log("[Rift Warden] Install Fight Audio\n" + sb);
        EditorUtility.DisplayDialog("Install Fight Audio", sb.ToString(), "OK");
    }
}
