using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Skybreak.Movement;
using Skybreak.Movement.Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Skybreak.DisruptorEditor
{
    /// <summary>
    /// The movement guide's acceptance table (Docs/TeamGuide/STUDENT_4_AGENT_MOVEMENT.md, section 11), run
    /// against the real motor, prefab and Animator in AgentMovementLab. Writes Docs/GV/AgentMovement/MovementChecks.txt.
    /// Batch: -executeMethod Skybreak.DisruptorEditor.AgentMovementChecks.Run (no -quit; it exits itself).
    /// </summary>
    [InitializeOnLoad]
    public static class AgentMovementChecks
    {
        const string ActiveKey = "RiftDisruptor.MovementChecks.Active";
        static readonly List<string> report = new List<string>();
        static IEnumerator sequence;
        static bool failed, finishing;
        static int passed, total;
        static string ReportPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/GV/AgentMovement/MovementChecks.txt"));

        static AgentMovementChecks() { EditorApplication.update += Tick; Application.logMessageReceived += Log; }

        [MenuItem("Skybreak/Rift Disruptor/Run movement checks")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run from Edit Mode.");
            sequence = null; failed = finishing = false; passed = total = 0; report.Clear();
            report.Add("Rift Disruptor movement and animation checks");
            report.Add(DateTime.UtcNow.ToString("O") + "  Unity " + Application.unityVersion);
            report.Add("Scene " + DisruptorLabSceneBuilder.MovementScenePath + "; prefab " + RiftDisruptorAssetBuilder.PrefabPath + "\n");
            BindingCheck();
            EditorSceneManager.OpenScene(DisruptorLabSceneBuilder.MovementScenePath);
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.isPlaying = true;
        }

        // Every curve in every clip must find its transform on the imported model (no silent "missing" bindings).
        static void BindingCheck()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(RiftDisruptorAssetBuilder.ModelPath);
            var instance = UnityEngine.Object.Instantiate(model);
            int bindings = 0, missing = 0; var clipNames = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { RiftDisruptorAssetBuilder.AnimationFolder }))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                clipNames.Add($"{clip.name} {clip.length:0.00}s{(clip.isLooping ? " loop" : "")}");
                foreach (var b in AnimationUtility.GetCurveBindings(clip)) { bindings++; if (AnimationUtility.GetAnimatedObject(instance, b) == null) missing++; }
            }
            UnityEngine.Object.DestroyImmediate(instance);
            Check("Animation clips bind to the imported model", bindings > 0 && missing == 0, $"{clipNames.Count} clips ({string.Join(", ", clipNames)}); {bindings} curve bindings, {missing} unresolved");
        }

        static void Log(string message, string stack, LogType type)
        {
            if (!SessionState.GetBool(ActiveKey, false) || finishing) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { failed = true; report.Add("FAIL Unity log: " + message + "\n" + stack); }
        }

        static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false) || finishing || !EditorApplication.isPlaying) return;
            if (sequence == null) sequence = Exercise();
            try { if (!sequence.MoveNext()) Finish(); }
            catch (Exception e) { failed = true; report.Add("FAIL exception: " + e); Finish(); }
        }

        static void Finish()
        {
            finishing = true; Time.captureDeltaTime = 0;
            report.Add($"\nSUMMARY: {passed}/{total} checks passed{(failed ? " — FAILURES PRESENT" : "")}");
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllLines(ReportPath, report);
            Debug.Log(string.Join("\n", report));
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(failed || passed != total ? 1 : 0);
        }

        static void Check(string name, bool ok, string detail)
        {
            total++; if (ok) passed++; else failed = true;
            report.Add((ok ? "PASS " : "FAIL ") + name + "\n     " + detail);
        }

        static IEnumerable Wait(float seconds, Func<bool> until = null)
        {
            float end = Time.time + seconds;
            while (Time.time < end && (until == null || !until())) yield return null;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        sealed class Trace
        {
            public float maxStepExcess, maxYawStep, maxYawExcess, time; public int frames, overlaps;
            public float speedError; public int speedSamples;
        }

        // Follows a route to the end, recording teleport, turn-rate, wall-overlap and animation-speed evidence.
        static IEnumerable Follow(AgentMovementLab lab, Trace t, float timeout, Func<bool> stop = null)
        {
            var motor = lab.Motor; var body = lab.Robot.GetComponent<CharacterController>();
            float start = Time.time, lastYaw = lab.Robot.transform.eulerAngles.y;
            Vector3 last = lab.Robot.transform.position;
            int mask = ~(1 << 8);
            while (Time.time - start < timeout && motor.Report == MotorReport.Moving && (stop == null || !stop()))
            {
                yield return null;
                float dt = Time.deltaTime; if (dt <= 0) continue;
                Vector3 p = lab.Robot.transform.position;
                float step = Flat(p - last).magnitude;
                t.maxStepExcess = Mathf.Max(t.maxStepExcess, step - motor.RequestedSpeed * dt);
                float yaw = lab.Robot.transform.eulerAngles.y, dYaw = Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw));
                t.maxYawStep = Mathf.Max(t.maxYawStep, dYaw); t.maxYawExcess = Mathf.Max(t.maxYawExcess, dYaw - motor.turnRate * dt);
                Vector3 a = p + Vector3.up * (body.radius + .1f), b = p + Vector3.up * (body.height - body.radius);
                if (Physics.CheckCapsule(a, b, body.radius * .9f, mask, QueryTriggerInteraction.Ignore)) t.overlaps++;
                float measured = motor.PlanarVelocity.magnitude;
                if (measured > .5f) { t.speedError += Mathf.Abs(lab.Presenter.AnimatedSpeed - measured); t.speedSamples++; }
                t.frames++; last = p; lastYaw = yaw;
            }
            t.time = Time.time - start;
        }

        static string StateName(AgentMovementLab lab)
        {
            var info = lab.Presenter.animator.GetCurrentAnimatorStateInfo(0);
            foreach (var s in new[] { "Locomotion", "Charge", "Release", "Hurt", "Dead" }) if (info.IsName(s)) return s;
            return "?";
        }

        static IEnumerator Exercise()
        {
            AgentMovementLab lab = null;
            while ((lab = UnityEngine.Object.FindAnyObjectByType<AgentMovementLab>()) == null || lab.Motor == null) yield return null;
            foreach (var _ in Wait(.3f)) yield return null;
            var motor = lab.Motor;

            // 1. Three-point route at 60 fps.
            {
                Time.captureDeltaTime = 1f / 60; lab.ResetRobot(); foreach (var _ in Wait(.2f)) yield return null;
                lab.Send(AgentMovementLab.ThreePoint, lab.walkSpeed, "check 1");
                var t = new Trace(); int order = 0; var route = AgentMovementLab.ThreePoint;
                foreach (var _ in Follow(lab, t, 30, () => { if (order < route.Length && Flat(lab.Robot.transform.position - route[order]).magnitude < .3f) order++; return false; })) yield return null;
                float end = Flat(lab.Robot.transform.position - route[route.Length - 1]).magnitude;
                Check("Three-point route: correct order, final stop, no teleport", motor.Report == MotorReport.Arrived && order == route.Length && end <= motor.arriveRadius + .05f && t.maxStepExcess < .01f,
                    $"report {motor.Report}; waypoints reached in order {order}/{route.Length}; final error {end:0.000} m; largest step beyond speed x dt {Mathf.Max(0, t.maxStepExcess) * 1000:0.0} mm; {t.time:0.00} s");
                Check("Smooth turning within the turn rate", t.maxYawExcess < .5f,
                    $"largest yaw change {t.maxYawStep:0.00} deg/frame; allowed {motor.turnRate / 60:0.00} at 60 fps");
                Check("Animator Speed follows measured speed", t.speedSamples > 30 && t.speedError / t.speedSamples < .3f,
                    $"mean |Speed parameter - measured speed| {t.speedError / Mathf.Max(1, t.speedSamples):0.000} m/s over {t.speedSamples} moving frames");

                // 2. Same route at 30 fps: distance/time must not depend on frame rate.
                float t60 = t.time; Vector3 p60 = lab.Robot.transform.position;
                Time.captureDeltaTime = 1f / 30; lab.ResetRobot(); foreach (var _ in Wait(.2f)) yield return null;
                lab.Send(AgentMovementLab.ThreePoint, lab.walkSpeed, "check 2");
                var t2 = new Trace(); foreach (var _ in Follow(lab, t2, 30)) yield return null;
                float timeDiff = Mathf.Abs(t2.time - t60) / t60, posDiff = Flat(lab.Robot.transform.position - p60).magnitude;
                Check("30 fps and 60 fps give the same motion", motor.Report == MotorReport.Arrived && timeDiff < .03f && posDiff < .1f,
                    $"arrival {t60:0.000} s at 60 fps vs {t2.time:0.000} s at 30 fps ({timeDiff * 100:0.0}%); end positions {posDiff * 100:0.0} cm apart");
                Time.captureDeltaTime = 1f / 60;
            }

            // 3. Corner course and fast running: no wall overlap, no corner cutting.
            {
                lab.ResetRobot(); foreach (var _ in Wait(.2f)) yield return null;
                lab.Send(AgentMovementLab.CornerCourse, lab.runSpeed, "check 3");
                var t = new Trace(); foreach (var _ in Follow(lab, t, 30)) yield return null;
                Check("Sharp corners at run speed without clipping walls", motor.Report == MotorReport.Arrived && t.overlaps == 0 && t.maxStepExcess < .01f,
                    $"report {motor.Report}; frames with the body (90% radius) inside geometry {t.overlaps}/{t.frames}; {t.time:0.00} s");
            }

            // 4. A* route from the grid (the IS path array) followed end to end.
            {
                lab.ResetRobot(); lab.SetDoor(false); foreach (var _ in Wait(.2f)) yield return null;
                var path = lab.PlanTo(new Vector3(6, 0, 9), lab.walkSpeed, "check 4");
                var t = new Trace(); foreach (var _ in Follow(lab, t, 40)) yield return null;
                Check("Follows an A* path array through the doorway", path.Status == PathStatus.Complete && motor.Report == MotorReport.Arrived && t.overlaps == 0,
                    $"A* {path.Status}, {path.Waypoints.Length} waypoints, {path.Length:0.0} m; report {motor.Report}; overlaps {t.overlaps}");
            }

            // 5. Door closes in front of a moving robot: physical stop, Blocked report, animation stops.
            {
                lab.ResetRobot(); lab.SetDoor(false); foreach (var _ in Wait(.2f)) yield return null;
                var body = lab.Robot.GetComponent<CharacterController>();
                body.enabled = false; lab.Robot.transform.position = new Vector3(3, 0, -1); body.enabled = true; Physics.SyncTransforms();
                lab.Send(AgentMovementLab.ThroughDoor, lab.walkSpeed, "check 5");
                foreach (var _ in Wait(.5f)) yield return null;
                lab.SetDoor(true);
                float maxZ = float.MinValue;
                foreach (var _ in Wait(6, () => motor.Report != MotorReport.Moving)) { maxZ = Mathf.Max(maxZ, lab.Robot.transform.position.z); yield return null; }
                foreach (var _ in Wait(.6f)) yield return null;
                float doorFace = lab.Door.transform.position.z - lab.Door.transform.localScale.z / 2;
                Check("Closed door: physical stop and Blocked report", motor.Report == MotorReport.Blocked && maxZ + body.radius <= doorFace + .06f,
                    $"report {motor.Report}; furthest body front {maxZ + body.radius:0.00} vs door face {doorFace:0.00}");
                Check("Locomotion animation stops at the blockage", lab.Presenter.AnimatedSpeed < .3f,
                    $"Speed parameter {lab.Presenter.AnimatedSpeed:0.00} after blocking (no running on the spot)");
                lab.Send(AgentMovementLab.ThroughDoor, lab.walkSpeed, "check 5b");
                Check("A new route through the closed door is rejected", motor.Report == MotorReport.Invalid, $"report {motor.Report}: {motor.LastIssue}");
                lab.SetDoor(false);
            }

            // 6. Invalid inputs never move the robot or throw.
            {
                lab.ResetRobot(); foreach (var _ in Wait(.2f)) yield return null;
                Vector3 before = lab.Robot.transform.position;
                lab.Send(new[] { Vector3.zero, new Vector3(float.NaN, 0, 1) }, lab.walkSpeed, "nan");
                var nan = motor.Report;
                lab.Send(Array.Empty<Vector3>(), lab.walkSpeed, "empty");
                var empty = motor.Report;
                lab.Send(AgentMovementLab.ThreePoint, 0, "zero speed");
                var zero = motor.Report;
                motor.SetPath(null, 2);
                var none = motor.Report;
                foreach (var _ in Wait(.5f)) yield return null;
                float drift = Flat(lab.Robot.transform.position - before).magnitude;
                Check("Invalid, empty, null and zero-speed paths are rejected", nan == MotorReport.Invalid && empty == MotorReport.Invalid && zero == MotorReport.Invalid && none == MotorReport.Invalid && drift < .01f,
                    $"NaN {nan}, empty {empty}, zero speed {zero}, null {none}; drift {drift * 1000:0.0} mm");
            }

            // 7. Path replaced mid-route, starting where the robot stands.
            {
                lab.ResetRobot(); foreach (var _ in Wait(.2f)) yield return null;
                lab.Send(AgentMovementLab.ThreePoint, lab.walkSpeed, "replace");
                foreach (var _ in Wait(1.5f)) yield return null;
                var replacement = new[] { lab.Robot.transform.position, new Vector3(-6, 0, 3), new Vector3(-9, 0, 3) };
                lab.Send(replacement, lab.walkSpeed, "replacement");
                var t = new Trace(); foreach (var _ in Follow(lab, t, 20)) yield return null;
                float end = Flat(lab.Robot.transform.position - replacement[2]).magnitude;
                Check("Replacement path: first node consumed, no jump", motor.Report == MotorReport.Arrived && end < motor.arriveRadius + .05f && t.maxStepExcess < .01f,
                    $"report {motor.Report}; final error {end:0.000} m; largest step excess {Mathf.Max(0, t.maxStepExcess) * 1000:0.0} mm");
            }

            // 8. Capsule stand-in: the motor works unchanged without the model.
            {
                lab.ResetRobot(); lab.SetVisual(false); foreach (var _ in Wait(.2f)) yield return null;
                lab.Send(AgentMovementLab.ThreePoint, lab.walkSpeed, "capsule");
                var t = new Trace(); foreach (var _ in Follow(lab, t, 30)) yield return null;
                Check("Capsule and robot use the same motor", motor.Report == MotorReport.Arrived, $"capsule run {t.time:0.00} s, report {motor.Report}");
                lab.SetVisual(true);
            }

            // 9. Action states: charge telegraph, release once, hurt, death holds.
            {
                lab.ResetRobot(); foreach (var _ in Wait(.3f)) yield return null;
                lab.Presenter.SetCharging(true);
                foreach (var _ in Wait(.4f)) yield return null;
                string charge = StateName(lab);
                lab.Presenter.SetCharging(false); lab.Presenter.PlayRelease();
                foreach (var _ in Wait(.15f)) yield return null;
                string release = StateName(lab);
                foreach (var _ in Wait(.9f)) yield return null;
                string after = StateName(lab);
                lab.Presenter.PlayHurt(); foreach (var _ in Wait(.12f)) yield return null;
                string hurt = StateName(lab);
                foreach (var _ in Wait(.6f)) yield return null;
                lab.Presenter.SetDead(true);
                lab.Presenter.PlayHurt();                                                     // must be ignored once dead
                foreach (var _ in Wait(2.5f)) yield return null;
                string dead = StateName(lab);
                Check("Charge, release, hurt and death states", charge == "Charge" && release == "Release" && after == "Locomotion" && hurt == "Hurt" && dead == "Dead",
                    $"charging -> {charge}; release -> {release}; then {after}; hurt -> {hurt}; defeated (+ late hurt request) -> {dead}");
                lab.Presenter.SetDead(false);
            }
        }
    }
}
