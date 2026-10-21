using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Skybreak.Warden;
using Skybreak.Warden.Core;

namespace Skybreak.WardenEditor
{
    // -executeMethod Skybreak.WardenEditor.WardenGameplayChecks.Run (omit -quit).
    // SessionState survives the domain reload when entering and leaving Play Mode.
    [InitializeOnLoad]
    public static class WardenGameplayChecks
    {
        const string ActiveKey = "CrystalEchoWarden.GameplayChecks.Active";
        const string ScenePath = "Assets/Scenes/Tests/CrystalEchoWarden/CrystalEchoWardenLab.unity";
        static readonly List<string> report = new List<string>();
        static IEnumerator sequence;
        static int lastFrame = -1;
        static double started;
        static bool failed, finishing;
        static string ReportPath => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../Docs/IS/CrystalEchoWarden/UnityGameplayResults.txt"));

        static WardenGameplayChecks()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += Log;
        }

        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run from Edit Mode.");
            EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.isPlaying = true;
        }

        static void Log(string message, string stack, LogType type)
        {
            if (!SessionState.GetBool(ActiveKey, false) || finishing) return;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            failed = true;
            report.Add("FAIL Unity log: " + message + "\n" + stack);
        }

        static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false) || finishing || !EditorApplication.isPlaying) return;
            if (sequence == null)
            {
                started = EditorApplication.timeSinceStartup;
                report.InsertRange(0, new[] { "Crystal Echo Warden Unity gameplay checks",
                    DateTime.UtcNow.ToString("O"), "Unity " + Application.unityVersion,
                    "Graphics: " + SystemInfo.graphicsDeviceName });
                sequence = Exercise();
            }
            try
            {
                if (EditorApplication.timeSinceStartup - started > 480)
                    throw new TimeoutException("Gameplay checks exceeded eight minutes.");
                if (Time.frameCount == lastFrame) return;
                lastFrame = Time.frameCount;
                if (!sequence.MoveNext()) Finish();
            }
            catch (Exception error)
            {
                failed = true;
                report.Add("FAIL " + error);
                Finish();
            }
        }

        static void Finish()
        {
            finishing = true;
            report.Add(failed ? "FAIL gameplay suite" : "PASS gameplay suite");
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllLines(ReportPath, report);
            SessionState.SetBool(ActiveKey, false);
            Debug.Log(string.Join("\n", report));
            if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
            else EditorApplication.isPlaying = false;
        }

        static void Require(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            report.Add("PASS " + label);
        }

        static IEnumerator Exercise()
        {
            WardenLab lab;
            do { yield return null; lab = UnityEngine.Object.FindFirstObjectByType<WardenLab>(); }
            while (lab == null || lab.Player == null);
            // Disable keyboard/mouse control while driving deterministic scenarios.
            lab.Player.acceptInput = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            WardenLabChecks.CheckLab();
            Require(!failed, "Immediate scene smoke suite");
            lab.fourWardens = false;
            lab.ResetRound();
            lab.SetDoorClosed(false);
            yield return null;
            var brain = lab.Wardens[0];
            var motion = brain.GetComponent<WardenAnimation>();
            brain.enabled = false;
            var start = brain.transform.position;
            brain.Motor.Go(lab.Map.Graph.Nearest(new Point(12, 1, 0)));
            bool glided = false;
            float until = Time.time + 2.5f;
            while (Time.time < until)
            {
                glided |= motion.Current == MotionClip.Glide;
                yield return null;
            }
            Require(Vector3.Distance(start, brain.transform.position) > 2, "Controller physically follows a route");
            Require(glided, "Actual translation selects the skeletal glide clip");
            brain.Motor.Stop();
            until = Time.time + .8f;
            while (Time.time < until) yield return null;
            Require(motion.Current == MotionClip.Idle, "Stopped movement returns to idle");

            // Route across the doorway, then close it while the controller is moving.
            Place(brain, new Vector3(4, 1, 0), new Vector3(-4, 1, 0));
            brain.Motor.Go(lab.Map.Graph.Nearest(new Point(-4, 1, 0)));
            until = Time.time + .3f;
            while (Time.time < until) yield return null;
            lab.SetDoorClosed(true);
            bool clipped = false;
            until = Time.time + 18;
            while (Time.time < until && !brain.Motor.Arrived)
            {
                var p = brain.transform.position;
                clipped |= Mathf.Abs(p.x) < .9f && Mathf.Abs(p.z) < 1.5f;
                yield return null;
            }
            Require(!clipped && Vector3.Distance(brain.transform.position, new Vector3(-4, 1, 0)) < .3f,
                "Door closes mid-route: controller reaches the goal around cover without crossing the door");

            lab.ResetRound();
            brain = lab.Wardens[0]; motion = brain.GetComponent<WardenAnimation>();
            Place(brain, new Vector3(4, 1, 0), new Vector3(-2, 1, 0));
            lab.Player.ResetPlayer(new Vector3(-2, 0, 0));
            lab.SetDoorClosed(false);
            Physics.SyncTransforms();
            until = Time.time + 4;
            while (!brain.WindingUp && Time.time < until) yield return null;
            Require(brain.WindingUp, "Visible in-range player triggers a telegraphed attack");
            float health = lab.Player.Health, chargeStart = Time.time;
            yield return null;
            Require(motion.Current == MotionClip.Attack, "Wind-up selects the skeletal attack clip");
            lab.SetDoorClosed(true);
            until = Time.time + 1;
            while (Time.time < until) yield return null;
            Require(lab.Player.Health == health && brain.Shots == 0, "Closing cover during wind-up prevents damage");
            Place(brain, new Vector3(4, 1, 0), new Vector3(-2, 1, 0));
            lab.SetDoorClosed(false);
            until = Time.time + 4;
            while (!brain.WindingUp && Time.time < until) yield return null;
            Require(brain.WindingUp, "Reopening cover permits a new charge");
            chargeStart = Time.time;
            until = Time.time + 3;
            while (brain.Shots == 0 && Time.time < until) yield return null;
            Require(brain.Shots == 1 && Mathf.Approximately(lab.Player.Health, health - brain.attackDamage),
                "A completed charge deals exactly one pulse of damage");
            Require(Time.time - chargeStart >= brain.windupSeconds - .1f,
                "Pulse respects the configured wind-up (one sampled-frame tolerance)");

            // Layer 8 actor blocks discharge, although environment-only sight remains clear.
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            blocker.name = "Test friendly actor obstruction"; blocker.layer = 8;
            blocker.transform.position = new Vector3(1, 1.5f, 0);
            Physics.SyncTransforms();
            int shots = brain.Shots; health = lab.Player.Health;
            until = Time.time + brain.attackCooldown + brain.windupSeconds + 1;
            while (Time.time < until) yield return null;
            Require(brain.Shots == shots && lab.Player.Health == health,
                "Intervening actor collider blocks a pulse");
            UnityEngine.Object.Destroy(blocker);
            yield return null;
            until = Time.time + 4;
            while (!brain.WindingUp && Time.time < until) yield return null;
            Require(brain.WindingUp, "Another attack begins after obstruction is removed");
            brain.ReceiveDamage(brain.MaximumHealth, lab.Player.transform);
            start = brain.transform.position; shots = brain.Shots; health = lab.Player.Health;
            until = Time.time + 2;
            while (Time.time < until) yield return null;
            Require(!brain.Alive && brain.Shots == shots && lab.Player.Health == health &&
                Vector3.Distance(start, brain.transform.position) < .01f,
                "Defeat during wind-up cancels damage and all root movement");
            Require(motion.Current == MotionClip.Death,
                "Defeated actor remains in the authored death animation");

            lab.fourWardens = true;
            lab.ResetRound();
            int frames = 0, resets = 0;
            double worstTick = 0;
            until = Time.time + 300;
            float nextChange = Time.time + 5;
            while (Time.time < until)
            {
                foreach (var actor in lab.Wardens)
                {
                    RequireFinite(actor.transform.position);
                    worstTick = Math.Max(worstTick, actor.LastTickMilliseconds);
                }
                if (Time.time >= nextChange)
                {
                    nextChange += 5;
                    lab.SetDoorClosed((resets & 1) == 0);
                    lab.GetComponent<WardenNoiseBus>().Emit(new Vector3(-8, 1, 0), 24, WardenNoiseKind.Decoy);
                    if (++resets % 3 == 0) lab.ResetRound();
                }
                frames++;
                yield return null;
            }
            Require(lab.Wardens.Count == 4 && frames > 300, "Five-minute four-Warden soak completes through door changes and round resets");
            report.Add("Observed " + frames + " frames; maximum sampled AI tick " + worstTick.ToString("F3") +
                " ms. This is diagnostic timing, not a target-hardware FPS or four-distinct-agent benchmark.");
            Require(!failed, "No Unity error, assertion or exception logged during gameplay checks");
        }

        static void Place(WardenBrain brain, Vector3 position, Vector3 face)
        {
            brain.Motor.Stop();
            var body = brain.GetComponent<CharacterController>();
            body.enabled = false;
            brain.transform.SetPositionAndRotation(position, Quaternion.LookRotation(face - position));
            body.enabled = true;
            Physics.SyncTransforms();
        }

        static void RequireFinite(Vector3 p)
        {
            if (!WorldGraph.Finite(p.x) || !WorldGraph.Finite(p.y) || !WorldGraph.Finite(p.z))
                throw new InvalidOperationException("An actor has a non-finite position.");
        }
    }
}
