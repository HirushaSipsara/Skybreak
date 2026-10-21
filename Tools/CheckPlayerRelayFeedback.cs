using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Skybreak.LocalCombatLab;
using Skybreak.Player;

public static class CheckPlayerRelayFeedback
{
    public static string Main()
    {
        if (!EditorApplication.isPlaying || !MissionFlowController.InputAllowed) throw new Exception("Start gameplay first.");
        var driver = UnityEngine.Object.FindAnyObjectByType<LocalCombatDriver>();
        driver.enabled = false; driver.ResetLab();
        var relay = driver.relay; relay.enabled = false; relay.decisionsEnabled = true;
        var agent = relay.motor.Agent;
        agent.Warp(driver.transform.position + Vector3.right * 2);
        Physics.SyncTransforms();
        string report = "Docs/GameFlow/Verification/PlayerRelayChecks.txt";
        File.WriteAllText(report, "Player feedback and Relay support smoke checks\n");
        void Assert(bool ok, string text) { if (!ok) throw new Exception(text); File.AppendAllText(report, "PASS: " + text + "\n"); }
        int bursts = driver.Feedback.JumpBursts;
        Assert(driver.RequestJump() && driver.RequestJump() && !driver.RequestJump() && driver.JumpsUsed == 2 && driver.Feedback.JumpBursts == bursts + 2,
            "Two jumps trigger feedback; a third airborne jump is rejected.");
        int hits = driver.Feedback.HitsReacted;
        driver.vitals.ReceiveDamage(60, relay.transform);
        Assert(driver.vitals.Health == 40 && driver.Feedback.HitsReacted == hits + 1 && driver.Feedback.HitActive && driver.Feedback.CameraAngles.z != 0,
            "Incoming damage triggers player hit feedback and camera response.");
        relay.ResetSupport(); int heals = relay.HealsApplied; float before = driver.vitals.Health;
        relay.SenseAndDecide();
        Assert(relay.HealsApplied > heals && driver.vitals.Health > before, "Relay selects healing and restores health when the player is hurt and reachable.");
        before = driver.vitals.Health; relay.SenseAndDecide();
        Assert(driver.vitals.Health == before, "Relay healing respects its cooldown.");
        driver.ResetLab(); driver.enabled = true; relay.enabled = true;
        return File.ReadAllText(report);
    }
}
