#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Skybreak;
using Skybreak.PhysicsInteraction;
using Skybreak.AI.SawanIS.VaultHound;
using Skybreak.AI.SawanIS.VaultHound.Testing;

public static class Member3MergeVerification
{
    const string Report = "Tools/Member3MergeChecks.txt";
    const string Active = "Member3MergeVerification.Active";
    static int stage;
    static float started;
    static VaultHoundBrain[] hounds;
    static Vector3[] positions;
    static PhysicsProp prop;
    static GameObject carrier;
    static float beforeHealth;
    static CombatHealth target;
    static int errors;

    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(Active, false)) return;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived += Log;
    }
    public static void Run()
    {
        File.WriteAllText(Report, "Member 3 PR9/PR10 integration checks\n");
        try
        {
            Skybreak.Editor.InteractionLabSetup.SetupLab();
            Assert(UnityEngine.Object.FindObjectsByType<PhysicsProp>().Length >= 2, "Interaction lab contains throwable props.");
            Assert(UnityEngine.Object.FindAnyObjectByType<PhysicsBarricade>() != null, "Interaction lab contains a heavy barricade.");
            Assert(UnityEngine.Object.FindAnyObjectByType<SlidingSecurityDoor>() != null, "Interaction lab contains a security door.");
            CheckMissing();
            Skybreak.AI.SawanIS.VaultHound.Editor.VaultHoundLabBuilder.BuildScene();
            CheckMissing();
            Assert(UnityEngine.Object.FindObjectsByType<VaultHoundBrain>().All(h => NavMesh.SamplePosition(h.transform.position, out _, 1,
                new NavMeshQueryFilter { agentTypeID = h.GetComponent<NavMeshAgent>().agentTypeID, areaMask = NavMesh.AllAreas })), "Both lab Hounds spawn on their baked navigation surface.");
            SessionState.SetBool(Active, true);
            Resume(); EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Fail(e); }
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount < 5) return;
        Application.runInBackground = true;
        try
        {
            if (stage == 0)
            {
                started = Time.time;
                hounds = UnityEngine.Object.FindObjectsByType<VaultHoundBrain>();
                positions = hounds.Select(h => h.transform.position).ToArray();
                Assert(hounds.Length == 2 && hounds.All(h => h.CurrentState != HoundState.Chambered && h.GetComponent<NavMeshAgent>().isOnNavMesh), "Lab harness deploys both Hounds on the NavMesh.");
                var pack = UnityEngine.Object.FindAnyObjectByType<HoundPack>();
                pack.ReportSighting(GameObject.Find("TargetDummy_Player").transform.position);
                stage = 1;
            }
            if (stage == 1 && Time.time - started > 2)
            {
                Assert(hounds.Where((h, i) => Vector3.Distance(h.transform.position, positions[i]) > .25f).Count() == 2, "Both Hounds move through the lab.");
                var grid = UnityEngine.Object.FindAnyObjectByType<VaultHoundGrid>();
                var cell = grid.WorldToCell(new Vector3(6, 0, 0)); int version = grid.Version;
                var harness = UnityEngine.Object.FindAnyObjectByType<VaultHoundLabHarness>();
                harness.ToggleObstacle();
                Assert(grid.Version > version && !grid.IsPassable(cell.x, cell.y), "Obstacle toggle rebakes blocked cells and invalidates cached paths.");
                harness.ToggleObstacle();
                Assert(grid.IsPassable(cell.x, cell.y), "Removing the obstacle restores passability.");
                harness.EliminateHoundA();
                var survivor = hounds.Single(h => h.GetComponent<CombatHealth>().IsAlive);
                Assert(UnityEngine.Object.FindAnyObjectByType<HoundPack>().IsPursuer(survivor), "Surviving Hound inherits the pursuer role.");
                foreach (var h in hounds) { h.enabled = false; h.GetComponent<NavMeshAgent>().enabled = false; }
                carrier = new GameObject("Physics verification carrier"); carrier.transform.position = new Vector3(0, 1, -8);
                carrier.AddComponent<BoxCollider>(); carrier.AddComponent<Camera>();
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "Physics verification prop";
                go.transform.position = new Vector3(0, 1, -6); go.transform.localScale = Vector3.one * .4f;
                var body = go.AddComponent<Rigidbody>(); body.mass = 10; body.linearDamping = .22f;
                prop = go.AddComponent<PhysicsProp>();
                prop.PickUp(carrier);
                Assert(prop.IsCarried && !body.useGravity && Physics.GetIgnoreCollision(go.GetComponent<Collider>(), carrier.GetComponent<Collider>()), "Pickup retains compatibility and ignores carrier collision.");
                prop.Drop();
                Assert(!prop.IsCarried && body.useGravity && Mathf.Approximately(body.linearDamping, .22f), "Drop restores the original physics settings.");
                target = GameObject.CreatePrimitive(PrimitiveType.Cube).AddComponent<CombatHealth>();
                target.name = "Physics verification target"; target.transform.position = new Vector3(0, 1.2f, -2);
                target.transform.localScale = new Vector3(2, 3, .5f); target.gameObject.layer = 8;
                beforeHealth = target.CurrentHealth;
                prop.Pickup(carrier); prop.Throw(); started = Time.time; stage = 2;
            }
            if (stage == 2 && Time.time - started > 1)
            {
                Assert(target.CurrentHealth < beforeHealth, "Thrown prop deals collision-based kinetic damage.");
                Assert(!Physics.GetIgnoreCollision(prop.GetComponent<Collider>(), carrier.GetComponent<Collider>()), "Carrier collision is restored after the throw clears.");
                prop.Pickup(carrier); prop.gameObject.SetActive(false);
                Assert(!Physics.GetIgnoreCollision(prop.GetComponent<Collider>(), carrier.GetComponent<Collider>()), "Disabling a carried prop restores ignored collisions.");
                Assert(errors == 0, "No runtime errors during the lab and physics checks.");
                File.AppendAllText(Report, "PASS: Unity compiled the combined PRs and completed runtime checks.\n");
                SessionState.SetBool(Active, false); EditorApplication.update -= Tick;
                EditorApplication.Exit(0);
            }
        }
        catch (Exception e) { Fail(e); }
    }
    static void CheckMissing()
    {
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0) throw new Exception("Missing script: " + t.name);
        File.AppendAllText(Report, "PASS: Scene has no missing scripts.\n");
    }
    static void Assert(bool condition, string text)
    {
        if (!condition) throw new Exception(text);
        File.AppendAllText(Report, "PASS: " + text + "\n");
    }
    static void Log(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        errors++; File.AppendAllText(Report, "RUNTIME ERROR: " + message + "\n");
    }
    static void Fail(Exception e)
    {
        File.AppendAllText(Report, "FAIL: " + e + "\n");
        SessionState.SetBool(Active, false); EditorApplication.update -= Tick;
        EditorApplication.Exit(1);
    }
}
#endif
