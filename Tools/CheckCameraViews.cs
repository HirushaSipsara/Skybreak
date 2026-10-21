using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Skybreak.LocalCombatLab;
using Skybreak.Player;
using Skybreak.Warden;

public static class CheckCameraViews
{
    const string Folder="Docs/IS/CrystalEchoWarden/MainMap";
    public static string Main()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
        Application.runInBackground=true;
        var d=UnityEngine.Object.FindAnyObjectByType<LocalCombatDriver>();
        var model=d.GetComponent<ArcImportedPresentation>();
        var encounter=UnityEngine.Object.FindAnyObjectByType<CrystalWardenEncounter>();
        d.enabled=false;d.relay.enabled=false;d.ResetLab();
        foreach(var guard in encounter.wardens)guard.enabled=false;
        var target=encounter.wardens[0];
        var body=d.GetComponent<CharacterController>();
        body.enabled=false;d.transform.position=new Vector3(0,100,0);body.enabled=true;
        d.SetFirstPerson(false);PlaceTarget(d,target);
        float at=Time.time;int stage=0;GameObject wall=null;
        Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/CameraViewChecks.txt","");
        EditorApplication.CallbackFunction tick=null;
        tick=()=>{
            if(!EditorApplication.isPlaying){EditorApplication.update-=tick;return;}
            if(Time.time-at<.3f)return;
            try
            {
                if(stage==0||stage==1)
                {
                    bool first=stage==1;
                    if(d.IsFirstPerson!=first)throw new Exception("Incorrect view mode.");
                    if(model.modelRoot.GetComponentsInChildren<Renderer>().Any(r=>r.enabled==first))throw new Exception("Player body visibility does not match the view.");
                    if(model.carbine.GetComponentsInChildren<Renderer>().Any(r=>!r.enabled))throw new Exception("Weapon hidden in "+(first?"first":"third")+" person.");
                    float before=target.Health;
                    if(!d.ShootFromView()||target.Health>=before)throw new Exception("The actual centre-screen shot missed in "+(first?"first":"third")+" person: "+d.LastHit);
                    if(Vector3.Distance(d.Feedback.Muzzle.position,model.carbine.Find("Muzzle").position)>.01f)throw new Exception("Visible muzzle and traced muzzle differ.");
                    if(Vector3.Angle(d.Feedback.Muzzle.forward,d.AimPoint-d.Feedback.Muzzle.position)>.1f)throw new Exception("Barrel does not align with aim point.");
                    File.AppendAllText(Folder+"/CameraViewChecks.txt","PASS: "+(first?"first":"third")+" person has correct body/weapon visibility and the actual centre-screen shot damages its target with aligned muzzle.\n");
                    if(stage==0){d.ToggleView();PlaceTarget(d,target);stage=1;at=Time.time;return;}
                    if(d.Magazine.Rounds!=18||!d.Reload())throw new Exception("Expected two shots and a reload.");
                    d.ToggleView();
                    if(d.IsFirstPerson||!d.Magazine.IsReloading||d.Magazine.Rounds!=18)throw new Exception("Switching interrupted or refilled reload.");
                    stage=2;at=Time.time;return;
                }
                if(stage==2)
                {
                    if(Time.time-at<d.Magazine.reloadDuration+.1f)return;
                    if(d.Magazine.Rounds!=20||d.Magazine.Reserve!=78)throw new Exception("Reload did not finish correctly after switching views.");
                    File.AppendAllText(Folder+"/CameraViewChecks.txt","PASS: switching during reload preserves the timer and ammunition; 20 rounds loaded, reserve 78.\n");
                    d.SetFirstPerson(true);
                    wall=new GameObject("Camera-to-muzzle cover check");
                    wall.transform.SetPositionAndRotation(Vector3.Lerp(d.cameraView.transform.position,d.Feedback.Muzzle.position,.5f),d.cameraView.transform.rotation);
                    wall.AddComponent<BoxCollider>().size=Vector3.one*.1f;Physics.SyncTransforms();
                    float before=target.Health;
                    if(!d.ShootFromView()||!d.ShotBlocked||target.Health!=before||d.LastHit!=wall.name)throw new Exception("First-person barrel can shoot through close cover.");
                    UnityEngine.Object.Destroy(wall);wall=null;
                    File.AppendAllText(Folder+"/CameraViewChecks.txt","PASS: close cover between camera and muzzle prevents first-person damage.\n");
                    d.ResetLab();d.SetFirstPerson(true);stage=3;at=Time.time;return;
                }
                if(stage==3){Capture(d.cameraView,"FirstPersonView");d.ToggleView();stage=4;at=Time.time;return;}
                if(stage==4){Capture(d.cameraView,"ThirdPersonView");Cleanup();}
            }
            catch(Exception error)
            {
                File.AppendAllText(Folder+"/CameraViewChecks.txt","FAIL: "+error.Message+"\n");
                Debug.LogError("Camera view check: "+error.Message);Cleanup();
            }
        };
        void Cleanup()
        {
            EditorApplication.update-=tick;
            if(wall!=null)UnityEngine.Object.Destroy(wall);
            foreach(var guard in encounter.wardens)guard.enabled=true;
            d.enabled=true;d.relay.enabled=true;d.ResetLab();d.SetFirstPerson(false);
        }
        EditorApplication.update+=tick;
        return "Testing both actual camera shots, presentation, reload continuity and close-cover obstruction.";
    }
    static void PlaceTarget(LocalCombatDriver d,WardenBrain target)
    {
        var collider=target.GetComponent<CharacterController>();collider.enabled=false;
        target.transform.position=d.cameraView.ViewportPointToRay(new Vector3(.5f,.5f,0)).GetPoint(7)-Vector3.up*.2f;
        collider.enabled=true;Physics.SyncTransforms();
    }
    static void Capture(Camera camera,string name)
    {
        var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(1100,800,24,RenderTextureFormat.DefaultHDR);
        try
        {
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
            File.WriteAllBytes(Folder+"/"+name+".png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);
        }
        finally{camera.targetTexture=oldTarget;RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(rt);}
    }
}
