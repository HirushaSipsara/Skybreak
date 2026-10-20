using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Skybreak.Warden;
using Skybreak.Warden.Core;

namespace Skybreak.WardenEditor
{
    // These checks must run in Unity. The standalone harness only parses this file.
    public static class WardenLabChecks
    {
        const string ScenePath="Assets/Scenes/Tests/CrystalEchoWarden/CrystalEchoWardenLab.unity";
        [MenuItem("Skybreak/Crystal Echo Warden/Open FPS lab")]
        static void OpenLab()
        {
            if(EditorApplication.isPlaying){Debug.LogWarning("Stop Play Mode before opening the lab.");return;}
            if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Skybreak/Crystal Echo Warden/Run Play Mode smoke checks")]
        static void CheckLab()
        {
            var lab=UnityEngine.Object.FindFirstObjectByType<WardenLab>();
            if(!EditorApplication.isPlaying||lab==null||lab.Player==null)
            {Debug.LogWarning("Open the Crystal Echo Warden lab, enter Play Mode, then run these checks.");return;}
            var report=new List<string>{"Crystal Echo Warden Unity smoke checks",DateTime.UtcNow.ToString("O"),"Unity "+Application.unityVersion};
            bool originalFour=lab.fourWardens;
            try
            {
                lab.fourWardens=false;lab.ResetRound();lab.SetDoorClosed(false);
                Require(Resources.Load<GameObject>("CrystalEchoWarden/CrystalEchoWarden")!=null,"FBX model imports as a Unity resource",report);
                Require(lab.Map.Graph!=null&&lab.Map.Graph.Count>0,"Navigation graph builds",report);
                var brain=lab.Wardens[0];
                Require(brain.GetComponent<WardenAnimation>()!=null&&brain.GetComponent<WardenAnimation>().Ready,
                    "All eight skeletal clips import and bind to the visual model",report);
                Require(brain.Senses.target==lab.Player&&brain.Motor.map==lab.Map,"Perception and movement references are connected",report);
                var g=lab.Map.Graph;
                int left=g.Nearest(new Point(-4,1,0)),right=g.Nearest(new Point(4,1,0));
                var open=Search.AStar(g,left,right);
                int version=g.Version;
                lab.SetDoorClosed(true);
                var closed=Search.AStar(g,left,right);
                Require(open.Status==RouteStatus.Found&&closed.Status==RouteStatus.Found&&closed.Cost>open.Cost&&g.Version>version,
                    "Closing the door changes topology and produces a longer legal route",report);
                lab.SetDoorClosed(false);
                // Keep the target comfortably inside the 16 m range to isolate occlusion.
                lab.Player.ResetPlayer(new Vector3(-4,0,0));
                brain.transform.rotation=Quaternion.LookRotation(lab.Player.AimPoint-brain.Senses.Eye);
                Physics.SyncTransforms();brain.Senses.Tick();
                Require(brain.Senses.Visible,"Direct sight detects the player across the open doorway",report);
                Vector3 lastSeen=brain.Senses.LastSeen;
                lab.SetDoorClosed(true);brain.Senses.Tick();
                Require(!brain.Senses.Visible&&brain.Senses.LastSeen==lastSeen,"Closed door blocks sight without revealing a new player location",report);
                int heard=brain.Senses.HeardCount;
                // A deliberately loud diagnostic event guarantees audibility behind this door.
                lab.GetComponent<WardenNoiseBus>().Emit(lab.Player.transform.position,100,WardenNoiseKind.Decoy);
                brain.Senses.Tick();
                Require(brain.Senses.HeardCount==heard+1&&brain.Senses.LastSeen==lastSeen,"Occluded sound updates evidence without changing exact last sighting",report);
                brain.Senses.Tick();
                Require(brain.Senses.HeardCount==heard+1,"A noise event is consumed only once",report);
                brain.ReceiveDamage(brain.MaximumHealth,lab.Player.transform);brain.Think();
                Require(!brain.Alive&&!brain.GetComponent<CharacterController>().enabled&&brain.Motor.Path.Length==0&&brain.Mind.Current==Intent.Disabled,
                    "Defeat stops navigation, decisions and actor collision",report);
                lab.fourWardens=true;lab.ResetRound();
                Require(lab.Wardens.Count==4,"Four Wardens spawn",report);
                foreach(var warden in lab.Wardens)
                {
                    warden.Think();
                    Require(warden.Alive&&warden.Senses.Belief!=null&&warden.Senses.target==lab.Player&&warden.Motor.map==lab.Map,
                        "Warden has an independent initialized mind and connected adapters",report);
                }
                Require(!ReferenceEquals(lab.Wardens[0].Mind,lab.Wardens[1].Mind)&&
                    !ReferenceEquals(lab.Wardens[0].Senses.Belief,lab.Wardens[1].Senses.Belief),"Per-agent state is independent",report);
                report.Add("PASS smoke suite. Combat timing, visual import, frame rates and the other three team agents still require the manual checklist.");
            }
            catch(Exception error){report.Add("FAIL "+error);Debug.LogException(error);}
            finally
            {
                lab.fourWardens=originalFour;lab.SetDoorClosed(false);lab.ResetRound();
                string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Docs/IS/CrystalEchoWarden"));
                Directory.CreateDirectory(folder);
                File.WriteAllLines(Path.Combine(folder,"UnitySmokeResults.txt"),report);
                Debug.Log(string.Join("\n",report));
            }
        }
        static void Require(bool condition,string label,List<string> report)
        {
            if(!condition)throw new InvalidOperationException(label);
            report.Add("PASS "+label);
        }
    }
}
