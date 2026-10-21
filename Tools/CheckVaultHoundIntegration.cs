using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using Skybreak.Combat;
using Skybreak.AI.SawanIS.VaultHound;
using Skybreak.PhysicsInteraction;
using Skybreak.LocalCombatLab;

public static class CheckVaultHoundIntegration
{
    const string Folder = "Docs/IS/AgentDesign/SawanIS/MainMap";
    static int passed;
    static IEnumerator sequence;
    static string failure;
    static bool oldBackground;
    static float oldTimeScale;
    public static string Main()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Enter Play Mode first.");
        Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/IntegrationChecks.txt", "Vault Hound main-map checks\n");
        passed = 0; failure = null; oldBackground = Application.runInBackground; oldTimeScale = Time.timeScale;
        Application.runInBackground = true; Time.timeScale = 1;
        sequence = Exercise(); EditorApplication.update += Tick;
        return "Running main-map chamber release, navigation, roles, gun damage, swipe, death and reset checks.";
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
        Application.runInBackground = true;
        try { if (sequence.MoveNext()) return; }
        catch (Exception e) { failure = e.ToString(); File.AppendAllText(Folder + "/IntegrationChecks.txt", "FAIL: " + failure + "\n"); }
        EditorApplication.update -= Tick;
        File.AppendAllText(Folder + "/IntegrationChecks.txt", $"SUMMARY: {passed} checks passed; failure={failure != null}\n");
        Time.timeScale = oldTimeScale; Application.runInBackground = oldBackground;
        if (failure != null) Debug.LogError(failure);
    }
    static IEnumerable Wait(float seconds, Func<bool> until = null)
    {
        float end = Time.time + seconds;
        while (Time.time < end && (until == null || !until())) yield return null;
    }
    static void Check(string text, bool ok)
    {
        if (!ok) throw new Exception(text);
        passed++; File.AppendAllText(Folder + "/IntegrationChecks.txt", "PASS: " + text + "\n");
    }
    static void Place(Transform actor, Vector3 pos)
    {
        var agent = actor.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(pos, out var hit, 2, filter)) throw new Exception("No navigation at " + pos);
            agent.Warp(hit.position); agent.ResetPath();
        }
        else
        {
            var body = actor.GetComponent<CharacterController>(); body.enabled = false; actor.position = pos; body.enabled = true;
        }
        Physics.SyncTransforms();
    }
    static IEnumerator Exercise()
    {
        foreach (var _ in Wait(.3f)) yield return null;
        var hounds = UnityEngine.Object.FindObjectsByType<VaultHoundBrain>().OrderBy(x => x.transform.position.x).ToArray();
        var pack = UnityEngine.Object.FindAnyObjectByType<HoundPack>();
        var release = UnityEngine.Object.FindAnyObjectByType<ChamberReleaseController>();
        var doors = UnityEngine.Object.FindObjectsByType<DungeonHoldingDoor>();
        var driver = UnityEngine.Object.FindAnyObjectByType<LocalCombatDriver>();
        driver.enabled = false;
        driver.relay.enabled = false;
        foreach (var w in UnityEngine.Object.FindObjectsByType<Skybreak.Warden.WardenBrain>()) w.enabled = false;
        foreach (var d in UnityEngine.Object.FindObjectsByType<Skybreak.AI.Disruptor.DisruptorBrain>()) d.decisionsEnabled = false;
        Check("Two chambered hounds, closed gates and four loading-court enemies", hounds.Length == 2 && hounds.All(x => x.CurrentState == HoundState.Chambered) && doors.All(x => x.IsClosed) && release.TotalEnemies == 4);
        var renderers = hounds.SelectMany(x => x.GetComponentsInChildren<Renderer>()).ToArray();
        Check("Models, materials and eight animation clips resolve", renderers.Length > 0 && renderers.All(x => x.sharedMaterials.All(m => m != null)) && hounds.All(x => x.GetComponent<Animator>().runtimeAnimatorController.animationClips.Select(c => c.name).Distinct().Count() == 8));
        var so = new SerializedObject(release); var array = so.FindProperty("area1Agents");
        var enemies = Enumerable.Range(0, array.arraySize).Select(i => (DamageSink)array.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
        foreach (var e in enemies.Take(3)) e.ReceiveDamage(10000, driver.transform);
        foreach (var _ in Wait(.3f)) yield return null;
        Check("Chambers remain locked before the last court enemy dies", !release.Released && doors.All(x => x.IsClosed));
        enemies.Last().ReceiveDamage(10000, driver.transform);
        foreach (var _ in Wait(4, () => hounds.All(x => x.CurrentState != HoundState.Chambered))) yield return null;
        Check("Court clear opens both real gates and deploys both hounds", release.Released && doors.All(x => x.IsOpen) && hounds.All(x => x.CurrentState != HoundState.Chambered && x.GetComponent<NavMeshAgent>().isOnNavMesh));
        var initial = hounds.Select(x => x.transform.position).ToArray();
        foreach (var _ in Wait(3)) yield return null;
        Check("Both hounds navigate out of their chambers", hounds.Select((x,i) => Vector3.Distance(x.transform.position, initial[i]) > .7f).All(x => x));
        Place(driver.transform, new Vector3(42, .1f, 50));
        Place(hounds[0].transform, new Vector3(38,0,50)); Place(hounds[1].transform, new Vector3(46,0,50));
        foreach (var h in hounds) h.transform.LookAt(new Vector3(driver.transform.position.x, h.transform.position.y, driver.transform.position.z));
        pack.ReportSighting(driver.transform.position);
        bool pursuer = false, flanker = false;
        foreach (var _ in Wait(1))
        {
            pursuer |= hounds.Any(x => x.CurrentState == HoundState.DirectPursue);
            flanker |= hounds.Any(x => x.CurrentState == HoundState.FlankPursue);
            yield return null;
        }
        Check("Shared sighting produces direct-pursuer and flanker roles", pursuer && flanker && pack.HasActiveSighting);
        foreach (var h in hounds) h.enabled = false;
        hounds[0].StopAllCoroutines(); hounds[1].StopAllCoroutines();
        Place(hounds[0].transform, new Vector3(42,0,56)); Place(hounds[1].transform, new Vector3(48,0,54));
        driver.transform.rotation = Quaternion.identity; driver.SetFirstPerson(false); driver.Magazine.ResetMagazine();
        var sink = hounds[0].GetComponent<VaultHoundDamageAdapter>(); float hp = sink.Health;
        bool fired = driver.Shoot(new Ray(driver.cameraView.transform.position, (sink.AimPoint - driver.cameraView.transform.position).normalized));
        Check("Player carbine damages the hound through the shared combat adapter", fired && sink.Health < hp);
        Place(hounds[0].transform, driver.transform.position + Vector3.forward * 1.2f);
        float playerHp = driver.vitals.Health, relayHp = driver.relay.Health;
        var swipe = typeof(VaultHoundBrain).GetMethod("TrySwipeHit", BindingFlags.Instance | BindingFlags.NonPublic);
        bool hitPlayer = (bool)swipe.Invoke(hounds[0], null);
        Check("Swipe damages the real player and leaves Relay unharmed", hitPlayer && driver.vitals.Health < playerHp && driver.relay.Health == relayHp);
        var attack = (IEnumerator)typeof(VaultHoundBrain).GetMethod("AttackSequence", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hounds[0], null);
        hounds[0].enabled = true; hounds[0].StartCoroutine(attack);
        foreach (var _ in Wait(.32f)) yield return null;
        sink.ReceiveDamage(10000, driver.transform);
        var atDeath = hounds[0].transform.position; playerHp = driver.vitals.Health;
        foreach (var _ in Wait(.7f)) yield return null;
        Check("Death during a lunge stops movement, damage and colliders", hounds[0].CurrentState == HoundState.Dead && Vector3.Distance(atDeath, hounds[0].transform.position) < .01f && driver.vitals.Health == playerHp && !hounds[0].GetComponent<NavMeshAgent>().enabled && hounds[0].GetComponentsInChildren<Collider>().All(c => !c.enabled));
        Check("Survivor becomes the sole pursuer", pack.IsPursuer(hounds[1]) && !pack.IsFlanker(hounds[1]));
        driver.ResetLab();
        foreach (var hound in hounds) hound.enabled = true;
        foreach (var _ in Wait(.2f)) yield return null;
        Check("Reset restores both hounds and clears release/pack memory", !release.Released && hounds.All(x => x.CurrentState == HoundState.Chambered && x.GetComponent<VaultHoundDamageAdapter>().Alive && x.GetComponent<NavMeshAgent>().enabled) && !pack.HasActiveSighting);
        Capture(hounds[1].transform.position);
        driver.relay.enabled = true;
    }
    public static void CaptureCurrent() => Capture(UnityEngine.Object.FindObjectsByType<VaultHoundBrain>().OrderByDescending(x => x.transform.position.x).First().transform.position);
    static void Capture(Vector3 origin)
    {
        var camera = Camera.main; var pos = camera.transform.position; var rot = camera.transform.rotation;
        var target = camera.targetTexture; var active = RenderTexture.active; var rt = RenderTexture.GetTemporary(1000,800,24);
        try
        {
            camera.transform.position = origin + new Vector3(-2.6f,1.3f,-2); camera.transform.LookAt(origin + Vector3.up * .65f);
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(1000,800,TextureFormat.RGB24,false); tex.ReadPixels(new Rect(0,0,1000,800),0,0); tex.Apply();
            File.WriteAllBytes(Folder + "/VaultHoundInMap.png", tex.EncodeToPNG()); UnityEngine.Object.Destroy(tex);
        }
        finally { camera.transform.SetPositionAndRotation(pos,rot); camera.targetTexture = target; RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); }
    }
}
