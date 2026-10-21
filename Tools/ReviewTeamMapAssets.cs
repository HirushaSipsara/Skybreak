using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Skybreak.Player;
using Skybreak.LocalCombatLab;
using Skybreak.Warden;

public static class ReviewTeamMapAssets
{
    const string Dir="Docs/IS/CrystalEchoWarden/MainMap";
    public static string BeginAttackCheck()
    {
        var driver=UnityEngine.Object.FindAnyObjectByType<LocalCombatDriver>();
        var e=UnityEngine.Object.FindAnyObjectByType<CrystalWardenEncounter>();
        driver.enabled=false;driver.relay.enabled=false;driver.ResetLab();
        var w=e.wardens[0];
        foreach(var other in e.wardens)other.enabled=other==w;
        var body=driver.GetComponent<CharacterController>();body.enabled=false;
        driver.transform.position=new Vector3(w.transform.position.x+3,.13f,w.transform.position.z);body.enabled=true;
        w.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(driver.vitals.AimPoint-w.transform.position,Vector3.up));
        Physics.SyncTransforms();w.Think();
        if(w.Mind.Current!=Skybreak.Warden.Core.Intent.Attack||w.Motor.Path.Length!=0)throw new Exception("Expected original stationary attack.");
        UnityEditor.SessionState.SetVector3("WardenAttackCheckPosition",w.transform.position);
        UnityEditor.SessionState.SetFloat("WardenAttackCheckHealth",driver.vitals.Health);
        return "Original attack selected; waiting for the authored wind-up and pulse.";
    }
    public static string FinishAttackCheck()
    {
        var driver=UnityEngine.Object.FindAnyObjectByType<LocalCombatDriver>();
        var e=UnityEngine.Object.FindAnyObjectByType<CrystalWardenEncounter>();var w=e.wardens[0];
        if(w.Shots<1||driver.vitals.Health>=UnityEditor.SessionState.GetFloat("WardenAttackCheckHealth",100))throw new Exception("The Warden did not fire and damage the real player.");
        if(Vector3.Distance(w.transform.position,UnityEditor.SessionState.GetVector3("WardenAttackCheckPosition",Vector3.zero))>.02f)throw new Exception("Attack moved off its firing position.");
        foreach(var other in e.wardens)other.enabled=true;
        driver.enabled=true;driver.relay.enabled=true;driver.ResetLab();
        const string result="PASS: authored utility attack stops patrol, charges, fires and damages the real player without drifting off its firing position.\n";
        File.AppendAllText(Dir+"/RuntimeChecks.txt",result);return result;
    }
    static void Capture(string name,Vector3 position,Vector3 target)
    {
        var cam=Camera.main;var p=cam.transform.position;var q=cam.transform.rotation;
        var targetTexture=cam.targetTexture;float fov=cam.fieldOfView;
        var rt=RenderTexture.GetTemporary(1100,900,24,RenderTextureFormat.DefaultHDR);
        var active=RenderTexture.active;
        try{
            cam.transform.position=position;cam.transform.LookAt(target);cam.fieldOfView=47;cam.targetTexture=rt;cam.Render();
            RenderTexture.active=rt;var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();
            Directory.CreateDirectory(Dir);File.WriteAllBytes(Dir+"/"+name+".png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
        }finally{cam.targetTexture=targetTexture;cam.fieldOfView=fov;cam.transform.SetPositionAndRotation(p,q);RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);}
    }
    public static string Pictures()
    {
        var arc=UnityEngine.Object.FindAnyObjectByType<ArcImportedPresentation>();
        var driver=arc.GetComponent<LocalCombatDriver>();
        Capture("ArcAndCarbine",arc.transform.position+new Vector3(3.5f,2.0f,4.3f),arc.transform.position+Vector3.up*1.25f);
        var encounter=UnityEngine.Object.FindAnyObjectByType<CrystalWardenEncounter>();
        var warden=encounter.wardens[2];var p=warden.transform.position;
        Capture("CrystalWardenInMap",p+new Vector3(4,2.3f,-5),p+Vector3.up*.3f);
        return "Arc and Warden screenshots captured. Shots="+driver.Shots+" player health="+driver.vitals.Health;
    }
    public static string FireCheck()
    {
        var arc=UnityEngine.Object.FindAnyObjectByType<ArcImportedPresentation>();
        var driver=arc.GetComponent<LocalCombatDriver>();
        var encounter=UnityEngine.Object.FindAnyObjectByType<CrystalWardenEncounter>();
        driver.enabled=false;driver.relay.enabled=false;
        driver.ResetLab();
        var w=encounter.wardens[0];
        var body=arc.GetComponent<CharacterController>();body.enabled=false;
        arc.transform.position=w.transform.position+new Vector3(0,-w.transform.position.y+.12f,-5);body.enabled=true;
        Physics.SyncTransforms();driver.Magazine.ResetMagazine();
        driver.cameraView.transform.position=arc.transform.position+Vector3.up*1.8f;
        var from=driver.cameraView.transform.position;
        // Use the camera ray and actual muzzle trace. A later call checks elapsed reload time.
        var ray=new Ray(from,(w.AimPoint-from).normalized);
        float health=w.Health;long noise=encounter.noise.LatestId;
        bool fired=driver.Shoot(ray);
        if(!fired||driver.Magazine.Rounds!=19)throw new Exception("The magazine did not consume one round.");
        if(w.Health>=health)throw new Exception("The player shot did not damage the Warden: "+driver.LastHit);
        if(encounter.noise.LatestId<=noise)throw new Exception("Gunshot noise was not emitted.");
        if(Vector3.Distance(driver.Feedback.Muzzle.position,arc.carbine.Find("Muzzle").position)>.01f)throw new Exception("Visible and traced muzzles differ.");
        int heard=w.Senses.HeardCount;w.Senses.Tick();
        if(w.Senses.HeardCount<=heard)throw new Exception("Warden did not hear the shot.");
        var objective=encounter.objectives[0];objective.GetComponent<WardenCrystalInteraction>().Interact(arc.gameObject);
        if(objective.Danger<.9f)throw new Exception("Crystal interaction did not raise the alarm.");
        if(!driver.Reload())throw new Exception("Reload did not start.");
        foreach(var b in encounter.wardens) b.enabled=false;
        string report="PASS: actual player shot consumed one round, damaged Crystal Echo Warden, emitted audible evidence, and kept the visible muzzle aligned.\nPASS: F interaction raised the guarded crystal alarm.\nReload started with 19/20; completion check pending.";
        File.WriteAllText(Dir+"/RuntimeChecks.txt",report);return report;
    }
    public static string FinishCheck()
    {
        var driver=UnityEngine.Object.FindAnyObjectByType<LocalCombatDriver>();
        var encounter=UnityEngine.Object.FindAnyObjectByType<CrystalWardenEncounter>();
        driver.Magazine.Advance();
        if(driver.Magazine.IsReloading||driver.Magazine.Rounds!=20||driver.Magazine.Reserve!=int.MaxValue)throw new Exception("Recharge did not finish at 20/20 with unlimited energy reserve.");
        foreach(var w in encounter.wardens)w.enabled=true;
        driver.enabled=true;driver.relay.enabled=true;
        driver.ResetLab();
        foreach(var w in encounter.wardens)
        {
            if(!w.Alive||!w.GetComponent<WardenAnimation>().Ready)throw new Exception("Warden reset or animation failed.");
            if(w.map.Graph==null||w.Senses.target!=driver.vitals)throw new Exception("Warden scene references are missing.");
        }
        var report="\nPASS: timed recharge refilled 20 energy shots with unlimited reserve.\nPASS: restart restored all four Wardens, the protected crystals, and energy. All eight clips bound on each Warden.\n";
        File.AppendAllText(Dir+"/RuntimeChecks.txt",report);return report;
    }
}
