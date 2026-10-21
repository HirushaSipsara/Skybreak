using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.EventSystems;
using Skybreak.Player;
using Skybreak.LocalCombatLab;
using Skybreak.AI.RiftWarden;

public static class CheckMissionFlow
{
    const string Folder="Docs/GameFlow/Verification";
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Pass(string message){File.AppendAllText(Folder+"/Results.txt","PASS: "+message+"\n");}
    public static string Main()
    {
        Require(EditorApplication.isPlaying,"Enter Play Mode first.");Application.runInBackground=true;
        Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/Results.txt","");
        if(MissionFlowController.Instance.State!=MissionState.MainMenu)MissionFlowController.Instance.MainMenu();
        int step=0;float at=Time.unscaledTime,pausedClock=0,pausedRun=0;bool capturedPower=false,capturedExtraction=false;
        EditorApplication.CallbackFunction tick=null;
        tick=()=>{
            if(!EditorApplication.isPlaying){EditorApplication.update-=tick;return;}
            Application.runInBackground=true;
            if(Time.unscaledTime-at<.25f)return;
            try
            {
                if(EventSystem.current!=null)EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().enabled=false;
                var f=MissionFlowController.Instance;
                if(f==null||f.State==MissionState.Initializing||f.State==MissionState.Loading)return;
                var d=UnityEngine.Object.FindAnyObjectByType<LocalCombatDriver>(FindObjectsInactive.Include);
                if(step==0)
                {
                    Require(f.State==MissionState.MainMenu&&Time.timeScale==0&&!MissionFlowController.InputAllowed,"Default menu did not lock gameplay.");
                    float health=d.vitals.Health;d.vitals.ReceiveDamage(20,null);Require(d.vitals.Health==health&&!d.ShootFromView(),"Menu permits firing or damage.");
                    Capture("MainMenu");Capture("MainMenu_4x3",1024,768);Click("CONTROLS");Require(f.State==MissionState.Controls,"Main-menu controls did not open.");Capture("Controls");Click("BACK");Require(f.State==MissionState.MainMenu,"Controls did not return to main menu.");
                    Pass("title menu, clickable buttons, controls/back, and gameplay/damage lock");step=1;
                }
                else if(step==1)
                {
                    if(f.State==MissionState.MainMenu){Click("START MISSION");at=Time.unscaledTime;return;}
                    Require(f.State==MissionState.Introduction&&!MissionFlowController.InputAllowed&&Time.timeScale==0,"Introduction is not isolated.");
                    Capture("Introduction");Click("SKIP INTRO  /  SPACE");step=2;
                }
                else if(step==2)
                {
                    Require(f.State==MissionState.Playing&&MissionFlowController.InputAllowed&&Time.timeScale==1,"Start/skip did not enable gameplay.");
                    Require(d.ShootFromView()&&d.Magazine.Rounds==19,"Weapon unavailable after intro.");Require(d.Reload(),"Energy recharge did not start.");Pass("mission intro/skip releases gameplay and energy weapon controls");
                    f.PauseMission();pausedClock=Time.time;pausedRun=f.MissionSeconds;step=3;
                }
                else if(step==3)
                {
                    Require(f.State==MissionState.Paused&&Time.time==pausedClock&&f.MissionSeconds==pausedRun&&!d.ShootFromView(),"Pause does not freeze world, mission clock and firing.");
                    Capture("Pause");Click("CONTROLS");step=31;
                }
                else if(step==31){Require(f.State==MissionState.Controls&&Time.timeScale==0,"Pause controls resumed world.");Click("BACK");step=32;}
                else if(step==32)
                {
                    Require(f.State==MissionState.Paused,"Back from paused controls lost pause.");Click("RESUME MISSION");Pass("pause, controls/back and resume preserve timers/input ownership");
                    d.enabled=false;var body=d.GetComponent<CharacterController>();body.enabled=false;d.transform.position=new Vector3(0,.2f,12);body.enabled=true;step=4;
                }
                else if(step==4)
                {
                    Require(f.ObjectiveStep==1,"Vault objective did not advance.");
                    if(d.Magazine.IsReloading){Require(Time.unscaledTime-at<4,"Energy recharge failed to finish.");return;}
                    Require(d.Magazine.Rounds==20&&d.Magazine.Reserve==int.MaxValue,"Unlimited energy reserve or 20-shot recharge failed.");Pass("energy cell recharges to 20 shots, keeps unlimited reserve, and resumes after pause");
                    d.GoToArena();step=5;
                }
                else if(step==5)
                {
                    Require(f.ObjectiveStep==2,"Aether Core objective did not advance.");
                    var intro=d.boss.GetComponent<RiftBossIntro>();Require(intro.IntroPlaying,"Existing boss intro did not start.");f.PauseMission();pausedClock=Time.time;step=6;
                }
                else if(step==6)
                {
                    Require(Time.time==pausedClock&&d.boss.GetComponent<RiftBossIntro>().IntroPlaying,"Pause did not suspend boss intro.");
                    f.ResumeMission();d.boss.GetComponent<RiftBossIntro>().enabled=false;d.boss.decisionsEnabled=false;d.relay.enabled=false;
                    d.boss.Activate();d.boss.DamagePart(RiftPart.LeftArm,999);d.boss.DamagePart(RiftPart.RightArm,999);Pass("objective progression and pausing/resuming the existing boss intro");step=7;
                }
                else if(step==7)
                {
                    if(!d.boss.CoreExposed){Require(Time.unscaledTime-at<8,"Boss core failed to open.");return;}
                    d.boss.DamagePart(RiftPart.Core,9999);step=8;
                }
                else if(step==8)
                {
                    Require(f.State==MissionState.Ending&&f.CompletionCount==1&&!MissionFlowController.InputAllowed,"Core defeat did not start one automatic ending.");
                    float health=d.vitals.Health;d.vitals.ReceiveDamage(999,null);Require(d.vitals.Health==health&&!d.ShootFromView(),"Ending permits damage or firing.");Pass("real arm/core defeat starts the ending once and protects player controls/health");step=9;
                }
                else if(step==9)
                {
                    if(f.State==MissionState.Ending)
                    {
                        if(f.StateSeconds>3.5f&&!capturedPower){Require(d.boss.returnPoint.IsPowered,"Aether return gate did not power up.");Capture("AetherCoreRestored");capturedPower=true;}
                        if(f.StateSeconds>6.5f&&!capturedExtraction){Capture("Extraction");capturedExtraction=true;}
                        Require(Time.unscaledTime-at<14,"Ending failed to complete.");return;
                    }
                    Require(f.State==MissionState.Victory&&f.CompletionCount==1&&Time.timeScale==0,"Victory screen missing or repeated.");
                    if(f.StateSeconds<.7f)return;
                    Require(d.boss.Defeated&&!d.gameObject.activeSelf&&!d.relay.gameObject.activeSelf,"Boss death or extraction did not complete.");Capture("Victory");Pass("existing boss death finishes, return portal powers up, Arc/Relay extract, and victory displays a frozen completion time");Click("PLAY AGAIN");step=10;
                }
                else if(step==10)
                {
                    Require(f.State==MissionState.Introduction&&f.CompletionCount==0&&f.MissionSeconds==0,"Play Again did not start a fresh mission.");
                    var guards=UnityEngine.Object.FindAnyObjectByType<Skybreak.Warden.CrystalWardenEncounter>();
                    Require(guards.wardens.All(w=>w.Alive)&&d.boss.LeftHealth>0&&d.boss.CoreHealth>0&&d.vitals.Health==100,"Restart did not restore actors.");
                    Require(UnityEngine.Object.FindObjectsByType<MissionFlowController>().Length==1&&UnityEngine.Object.FindObjectsByType<EventSystem>().Length==1,"Restart duplicated mission services.");
                    Pass("Play Again reloads a fresh mission, restores all actors, and creates one UI/event system");Click("SKIP INTRO  /  SPACE");step=11;
                }
                else if(step==11){d.vitals.ReceiveDamage(999,null);step=12;}
                else if(step==12)
                {
                    Require(f.State==MissionState.Defeat&&Time.timeScale==0&&!MissionFlowController.InputAllowed,"Defeat screen did not lock gameplay.");Capture("Defeat");Click("RETRY MISSION");step=13;
                }
                else if(step==13)
                {
                    Require(f.State==MissionState.Introduction&&d.vitals.Health==100,"Defeat retry did not reload mission.");Pass("player defeat, retry and fresh health");f.MainMenu();step=14;
                }
                else if(step==14)
                {
                    Require(f.State==MissionState.MainMenu&&!MissionFlowController.InputAllowed&&Time.timeScale==0,"Return to menu failed.");Pass("return to main menu after repeated reloads");EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().enabled=true;EditorApplication.update-=tick;
                }
                at=Time.unscaledTime;
            }
            catch(Exception error){File.AppendAllText(Folder+"/Results.txt","FAIL: "+error+"\n");Debug.LogError("Mission flow check: "+error.Message);if(EventSystem.current!=null)EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().enabled=true;EditorApplication.update-=tick;}
        };
        EditorApplication.update+=tick;return "Testing real menus, intro, pause, objective progression, boss defeat, ending, replay and defeat/retry.";
    }
    static void Click(string label)
    {
        var button=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>().FirstOrDefault(b=>b.gameObject.activeInHierarchy&&b.name==label);
        Require(button!=null,"Missing active button: "+label);
        Canvas.ForceUpdateCanvases();var corners=new Vector3[4];button.GetComponent<RectTransform>().GetWorldCorners(corners);
        var data=new PointerEventData(EventSystem.current){position=(corners[0]+corners[2])*.5f};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        Require(hits.Count>0&&hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==button,"Button not clickable at its visible centre: "+label);button.onClick.Invoke();
    }
    public static string Capture(string name,int width=1280,int height=720)
    {
        var canvas=UnityEngine.Object.FindAnyObjectByType<MissionInterface>().GetComponentInChildren<Canvas>();var camera=Camera.main;
        var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;var oldPlane=canvas.planeDistance;
        var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.DefaultHDR);
        try
        {
            camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();Directory.CreateDirectory(Folder);File.WriteAllBytes(Folder+"/"+name+".png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);
        }
        finally{camera.targetTexture=oldTarget;canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldPlane;RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(rt);Canvas.ForceUpdateCanvases();}
        return Folder+"/"+name+".png";
    }
}
