using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Skybreak.AI;
using Skybreak.AI.RiftWarden;
using Skybreak.AI.Relay;
public static class CheckWorkbench
{
    public static string Check()
    {
        var t=AgentTuning.Current;
        if(!t.Valid())throw new Exception("Tuning validation failed");
        var boss=UnityEngine.Object.FindAnyObjectByType<RiftWardenBrain>();
        var relay=UnityEngine.Object.FindAnyObjectByType<RelayBrain>();
        if(boss==null||relay==null)throw new Exception("Open main scene with both agents");
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var bt=(WardenTuning)boss.GetType().GetProperty("T",flags).GetValue(boss);
        var rt=(RelayTuning)relay.GetType().GetProperty("T",flags).GetValue(relay);
        if(!ReferenceEquals(t.warden,bt)||!ReferenceEquals(t.relay,rt))throw new Exception("Brains not sharing loaded tuning");
        var invalid=new AgentTuningData();invalid.warden.rearDelay=float.NaN;
        if(invalid.Valid())throw new Exception("NaN accepted");
        invalid=new AgentTuningData();invalid.movement.replanInterval=-1;
        if(invalid.Valid())throw new Exception("Invalid path interval accepted");
        var report="PASS Unity: compiled shared tuning classes; both real brain properties use the loaded configuration.\nBoss rearDelay="+bt.rearDelay+"; Relay healAmount="+rt.healAmount+"; revision="+AgentTuning.Revision+"; Play Mode="+EditorApplication.isPlaying+".\nPASS: invalid numeric parameters rejected by C# validation.\n";
        File.AppendAllText("Tools/AIWorkbench/verification.txt",report);
        return report;
    }
}
