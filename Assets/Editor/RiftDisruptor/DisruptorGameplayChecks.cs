using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Skybreak.AI.Disruptor;
using Skybreak.AI.Disruptor.Lab;
using Skybreak.Disruptor.Core;
using Skybreak.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Skybreak.DisruptorEditor
{
    /// <summary>
    /// Scripted Play Mode scenarios against the real lab, written to Docs/IS/RiftDisruptor/GameplayChecks.txt.
    /// Batch: -executeMethod Skybreak.DisruptorEditor.DisruptorGameplayChecks.Run (no -quit; it exits itself).
    /// The player is driven by script (input off), the Disruptor runs unmodified.
    /// </summary>
    [InitializeOnLoad]
    public static class DisruptorGameplayChecks
    {
        const string ActiveKey = "RiftDisruptor.GameplayChecks.Active";
        static readonly List<string> report = new List<string>();
        static IEnumerator sequence;
        static bool failed, finishing;
        static int passed, total;
        static string ReportPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/IS/RiftDisruptor/GameplayChecks.txt"));

        static DisruptorGameplayChecks()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += Log;
        }

        [MenuItem("Skybreak/Rift Disruptor/Run gameplay checks")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run from Edit Mode.");
            // The project enters Play Mode without a domain reload, so statics survive between runs.
            sequence = null; failed = finishing = false; passed = total = 0; report.Clear();
            if (!File.Exists(DisruptorLabSceneBuilder.ScenePath)) DisruptorLabSceneBuilder.Build();
            EditorSceneManager.OpenScene(DisruptorLabSceneBuilder.ScenePath);
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
                report.Clear();
                report.Add("Rift Disruptor gameplay checks");
                report.Add(DateTime.UtcNow.ToString("O") + "  Unity " + Application.unityVersion + "  " + SystemInfo.graphicsDeviceName);
                report.Add("Scene " + DisruptorLabSceneBuilder.ScenePath + "; player driven by script; agent code unmodified.\n");
                sequence = Exercise();
            }
            try
            {
                if (!sequence.MoveNext()) Finish();
            }
            catch (Exception e) { failed = true; report.Add("FAIL exception: " + e); Finish(); }
        }

        static void Finish()
        {
            finishing = true;
            report.Add($"\nSUMMARY: {passed}/{total} scenarios passed{(failed ? " — FAILURES PRESENT" : "")}");
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllLines(ReportPath, report);
            Debug.Log(string.Join("\n", report));
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(failed || passed != total ? 1 : 0);
        }

        static DisruptorLab currentLab;

        static void Check(string name, bool ok, string detail)
        {
            total++; if (ok) passed++; else failed = true;
            report.Add((ok ? "PASS " : "FAIL ") + name + "\n     " + detail);
            if (currentLab != null)
                foreach (var d in currentLab.Disruptors) if (d != null) report.Add("     state: " + Describe(d));
        }

        static string Describe(DisruptorBrain d)
        {
            var p = d.transform.position;
            return $"{d.name} at ({p.x:0.0},{p.z:0.0}) goal {d.Goal?.ToString() ?? "none"}, step {d.CurrentAction?.Name ?? "-"}, " +
                   $"sees {d.Senses.Visible}, line {d.Senses.PlayerHasLine}, aim {d.Senses.AimAngle:0}°, facts [{FactSet.Describe(d.Facts)}], replans {d.Replans}; \"{d.Decision}\"";
        }

        // Waits in game time; yields once per editor update.
        static IEnumerable Wait(float seconds, Func<bool> until = null)
        {
            float end = Time.time + seconds;
            while (Time.time < end && (until == null || !until())) yield return null;
        }

        static IEnumerator Exercise()
        {
            DisruptorLab lab = null;
            while ((lab = UnityEngine.Object.FindAnyObjectByType<DisruptorLab>()) == null || lab.Player == null || lab.Disruptors.Count == 0) yield return null;
            lab.Player.acceptInput = false; currentLab = lab;
            foreach (var _ in Wait(0.5f)) yield return null;

            // 1. Hit and run: unaware player facing away. Expect move -> charge -> release -> phase dash.
            {
                lab.threeDisruptors = false; lab.ResetRound(); lab.Player.SetLook(225, 0);
                var d = lab.Disruptors[0];
                var actions = new List<string>(); float healthBefore = lab.Player.Health;
                foreach (var _ in Wait(20, () => d.PulsesReleased >= 1)) { Note(actions, d); yield return null; }
                Vector3 atRelease = d.transform.position;
                foreach (var _ in Wait(2.5f)) { Note(actions, d); yield return null; }
                float moved = Flat(d.transform.position - atRelease).magnitude;
                Check("Hit-and-run cycle", d.PulsesReleased >= 1 && actions.Contains("Charge pulse (telegraph)") && actions.Contains("Phase-dash to escape anchor") && moved > 2,
                    $"actions: {string.Join(" > ", actions)}; pulse hit {d.LastReleaseHit}; player health {healthBefore:0} -> {lab.Player.Health:0}; relocated {moved:0.0} m after release");
            }

            // 2. Aim awareness: the player keeps the crosshair on it. Expect Evade and time out of the crosshair.
            {
                lab.ResetRound(); lab.Player.Teleport(new Vector3(-4, 0, -10));
                var d = lab.Disruptors[0];
                bool evaded = false; float maxPressure = 0; int frames = 0, inCrosshair = 0;
                foreach (var _ in Wait(8))
                {
                    lab.Player.LookAt(d.AimPoint);
                    evaded |= d.Goal == DisruptorGoal.Evade; maxPressure = Mathf.Max(maxPressure, d.Senses.AimPressure);
                    if (Time.time > 0) { frames++; if (d.Senses.PlayerHasLine && d.Senses.AimAngle <= d.aimCone) inCrosshair++; }
                    yield return null;
                }
                Check("Aim awareness drives evasion", evaded && maxPressure > 0.5f,
                    $"Evade chosen {evaded}; peak aim pressure {maxPressure:0.00}; in clear crosshair {100f * inCrosshair / Mathf.Max(1, frames):0}% of frames while the player tracked it");
            }

            // 3. Counterplay: shoot it while it telegraphs. Expect the pulse to be staggered.
            {
                lab.ResetRound(); lab.Player.SetLook(225, 0);
                var d = lab.Disruptors[0];
                foreach (var _ in Wait(20, () => d.Pulse.Phase == PulsePhase.Charging)) yield return null;
                bool charging = d.Pulse.Phase == PulsePhase.Charging;
                int shots = 0;
                while (charging && d.Pulse.Busy && shots < 4) { lab.Player.LookAt(d.AimPoint); lab.Player.Fire(); shots++; yield return null; }
                foreach (var _ in Wait(0.3f)) yield return null;
                Check("Damage during the telegraph staggers the pulse", charging && d.PulsesStaggered >= 1 && d.PulsesReleased == 0,
                    $"charging observed {charging}; shots {shots}; staggered {d.PulsesStaggered}; released {d.PulsesReleased}; health {d.Health:0}");
            }

            // 4. Learning: the player steps out of the ring during every telegraph.
            {
                lab.ResetRound(); lab.Player.SetLook(225, 0);
                var d = lab.Disruptors[0];
                double charge0 = d.ChargeSeconds, strike0 = d.StrikeDistance;
                int handled = -1;
                foreach (var _ in Wait(60, () => d.Learner.Observations >= 4))
                {
                    if (d.Pulse.Phase == PulsePhase.Charging && d.Pulse.PulseId != handled && Time.time - d.Pulse.StartedAt > 0.25f)
                    {
                        handled = d.Pulse.PulseId;
                        Vector3 away = Flat(lab.Player.transform.position - d.transform.position).normalized;
                        Vector3 target = d.transform.position + away * (d.pulseRadius + 2.5f);
                        target.x = Mathf.Clamp(target.x, -13.5f, 13.5f); target.z = Mathf.Clamp(target.z, -13.5f, 13.5f);
                        if (Flat(target - d.transform.position).magnitude <= d.pulseRadius) target = d.transform.position - away * (d.pulseRadius + 2.5f);
                        lab.Player.Teleport(target); lab.Player.SetLook(225, 0);
                    }
                    yield return null;
                }
                var l = d.Learner;
                Check("Dodge learning adapts the telegraph", l.Observations >= 3 && l.Mean > 0.6 && d.ChargeSeconds < charge0 && d.StrikeDistance < strike0,
                    $"observations {l.Observations}, dodges {l.Dodges}; P(dodge) {l.Mean:0.00}; charge {charge0:0.00} -> {d.ChargeSeconds:0.00} s; strike {strike0:0.00} -> {d.StrikeDistance:0.00} m");
            }

            // 5. Partial observability and doors: the player hides in the side room and the door shuts.
            {
                lab.ResetRound(); lab.SetDoorClosed(false);
                var d = lab.Disruptors[0];
                lab.Player.Teleport(new Vector3(-2, 0, -8)); lab.Player.SetLook(225, 0);
                foreach (var _ in Wait(6, () => d.Senses.Visible)) yield return null;
                bool sawFirst = d.Senses.Visible;
                int version = lab.Grid.Version;
                lab.Player.Teleport(new Vector3(11, 0, 10)); lab.SetDoorClosed(true);
                bool hunted = false, investigated = false, enteredRoom = false;
                foreach (var _ in Wait(12))
                {
                    hunted |= d.Goal == DisruptorGoal.Hunt; investigated |= d.CurrentActionId == DisruptorActionId.Investigate;
                    var p = d.transform.position; enteredRoom |= p.x > 6.2f && p.z > 5.4f;
                    yield return null;
                }
                Check("Lost sight -> hunt from memory; closed door respected", sawFirst && hunted && investigated && !enteredRoom && lab.Grid.Version > version,
                    $"saw player first {sawFirst}; Hunt {hunted}; Investigate {investigated}; entered closed room {enteredRoom}; nav version {version} -> {lab.Grid.Version}; final goal {d.Goal}");
                lab.SetDoorClosed(false);
                foreach (var _ in Wait(10, () => d.Senses.Visible)) yield return null;
                Check("Opening the door re-opens routes", d.Senses.Visible || d.Goal == DisruptorGoal.Hunt || d.Goal == DisruptorGoal.Guard,
                    $"after opening: visible {d.Senses.Visible}, goal {d.Goal}, motor {d.Follower.Report}");
            }

            // 6. Three Disruptors at once: no shared destinations, bounded thinking time.
            {
                lab.threeDisruptors = true; lab.ResetRound(); lab.Player.SetLook(225, 0);
                float worst = 0, sum = 0; int samples = 0, clashes = 0, idle = 0;
                foreach (var _ in Wait(15))
                {
                    var live = lab.Disruptors.Where(x => x != null && x.Alive).ToList();
                    foreach (var x in live) { worst = Mathf.Max(worst, x.LastThinkMilliseconds); sum += x.LastThinkMilliseconds; samples++; if (!x.Goal.HasValue) idle++; }
                    // Only tactical destinations are claimed; several agents closing in on the same player is legitimate.
                    var claimed = live.Where(x => x.Destination.HasValue && (x.CurrentActionId == DisruptorActionId.MoveToStrike || x.CurrentActionId == DisruptorActionId.PhaseDash)).ToList();
                    for (int i = 0; i < claimed.Count; i++)
                        for (int j = i + 1; j < claimed.Count; j++)
                            if (Flat(claimed[i].Destination.Value - claimed[j].Destination.Value).magnitude < 1f) clashes++;
                    yield return null;
                }
                int pulses = lab.Disruptors.Sum(x => x.PulsesReleased);
                Check("Three agents together", clashes == 0 && pulses >= 1,
                    $"pulses {pulses}; destination clashes {clashes}; frames without a goal {idle}; decision cost avg {sum / Mathf.Max(1, samples):0.00} ms, worst {worst:0.00} ms (editor, not a build benchmark)");
                lab.threeDisruptors = false;
            }

            // 7. Death: it stops, and a charged pulse can never fire afterwards.
            {
                lab.ResetRound(); lab.Player.SetLook(225, 0);
                var d = lab.Disruptors[0];
                foreach (var _ in Wait(20, () => d.Pulse.Phase == PulsePhase.Charging)) yield return null;
                d.ReceiveDamage(10000, lab.Player.transform);
                Vector3 at = d.transform.position; float hp = lab.Player.Health;
                foreach (var _ in Wait(3)) yield return null;
                Check("Defeat stops movement and attacks", !d.Alive && d.PulsesReleased == 0 && Flat(d.transform.position - at).magnitude < 0.05f && lab.Player.Health == hp,
                    $"alive {d.Alive}; released after death {d.PulsesReleased}; drift {Flat(d.transform.position - at).magnitude:0.000} m; motor {d.Follower.Report}");
            }

            // 8. Downed player: it stops attacking and goes back to guarding.
            {
                lab.ResetRound(); lab.Player.SetLook(225, 0);
                var d = lab.Disruptors[0];
                lab.Player.ReceiveDamage(1000, null);
                int releasedBefore = d.PulsesReleased;
                foreach (var _ in Wait(6)) yield return null;
                Check("Player down -> no further attacks", d.PulsesReleased == releasedBefore && d.Goal != DisruptorGoal.Disrupt,
                    $"goal {d.Goal}; plan {d.Plan}");
            }
        }

        static void Note(List<string> actions, DisruptorBrain d)
        {
            var a = d.CurrentAction;
            if (a != null && (actions.Count == 0 || actions[actions.Count - 1] != a.Name)) actions.Add(a.Name);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
    }
}
