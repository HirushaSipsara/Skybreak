using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Skybreak.LocalCombatLab;
using Skybreak.Warden;

public static class CheckWardenBreakup
{
    const string Report="Docs/IS/CrystalEchoWarden/MainMap/BreakupChecks.txt";
    public static string Main()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
        Application.runInBackground=true;
        var driver=UnityEngine.Object.FindAnyObjectByType<LocalCombatDriver>();
        var encounter=UnityEngine.Object.FindAnyObjectByType<CrystalWardenEncounter>();
        driver.enabled=false;driver.relay.enabled=false;driver.ResetLab();
        foreach(var guard in encounter.wardens)guard.enabled=false;
        var w=encounter.wardens[2];var fx=w.GetComponent<WardenPresentation>();
        var before=w.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r=>r.enabled).ToArray();
        w.ReceiveDamage(999,driver.transform);
        float start=Time.time;int stage=0;GameObject pieces=null;
        string error=null;
        EditorApplication.CallbackFunction tick=null;
        tick=()=>{
            if(!EditorApplication.isPlaying){EditorApplication.update-=tick;return;}
            try
            {
                if(stage==0&&Time.time-start>.65f)
                {
                    pieces=GameObject.Find("Crystal Echo Warden broken pieces (runtime)");
                    if(pieces==null)throw new Exception("No breakup spawned.");
                    var bodies=pieces.GetComponentsInChildren<Rigidbody>();
                    if(bodies.Length!=30)throw new Exception("Expected the Warden's 30 model parts; got "+bodies.Length);
                    foreach(var b in bodies)
                    {
                        var size=b.GetComponent<MeshFilter>().sharedMesh.bounds.size;
                        if(size.magnitude>2||Vector3.Distance(b.position,w.transform.position)>5)throw new Exception("Incorrect piece scale/position: "+b.name+" size="+size+" distance="+Vector3.Distance(b.position,w.transform.position));
                        if(Physics.GetIgnoreCollision(b.GetComponent<Collider>(),driver.GetComponent<CharacterController>())==false)throw new Exception("Debris can block the player.");
                    }
                    if(w.Alive||w.GetComponent<CharacterController>().enabled||w.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>r.enabled))throw new Exception("Defeated source is still visible or collidable.");
                    File.WriteAllText(Report,"PASS: lethal damage produces 30 correctly scaled falling model parts, hides the intact model and disables its controller. Debris ignores player collisions.\n");
                    Capture(w.transform.position+new Vector3(3,2,-4),w.transform.position);
                    stage=1;
                }
                if(stage==1&&Time.time-start>fx.breakupDelay+fx.debrisLifetime+.2f)
                {
                    if(pieces!=null)throw new Exception("Debris did not expire.");
                    File.AppendAllText(Report,"PASS: debris and temporary meshes expire after the configured lifetime.\n");
                    w.ResetForEncounter();
                    if(!w.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r=>r.enabled).SequenceEqual(before))throw new Exception("Reset did not restore the original model.");
                    w.ReceiveDamage(999,driver.transform);start=Time.time;stage=2;
                }
                if(stage==2&&Time.time-start>.65f)
                {
                    if(GameObject.Find("Crystal Echo Warden broken pieces (runtime)")==null)throw new Exception("Second defeat failed to spawn debris.");
                    driver.ResetLab();
                    if(GameObject.Find("Crystal Echo Warden broken pieces (runtime)")!=null)throw new Exception("Restart left active debris.");
                    if(encounter.wardens.Any(b=>!b.Alive||!b.GetComponent<CharacterController>().enabled||b.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>!r.enabled)))throw new Exception("Restart did not restore the four guards.");
                    File.AppendAllText(Report,"PASS: a second defeat works and encounter restart immediately removes its debris and restores all four guards.\n");
                    stage=3;
                }
            }
            catch(Exception ex){error=ex.Message;File.AppendAllText(Report,"FAIL: "+error+"\n");stage=3;}
            if(stage!=3)return;
            EditorApplication.update-=tick;
            foreach(var guard in encounter.wardens)guard.enabled=true;
            driver.enabled=true;driver.relay.enabled=true;driver.ResetLab();
            if(error!=null)Debug.LogError("Warden breakup check: "+error);
        };
        EditorApplication.update+=tick;
        return "Running real defeat, debris expiry, repeated defeat and restart checks.";
    }
    static void Capture(Vector3 position,Vector3 target)
    {
        var camera=Camera.main;var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;
        var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(1000,800,24,RenderTextureFormat.DefaultHDR);
        try
        {
            camera.transform.position=position;camera.transform.LookAt(target);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(1000,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1000,800),0,0);image.Apply();
            File.WriteAllBytes("Docs/IS/CrystalEchoWarden/MainMap/WardenBreakup.png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);
        }
        finally{camera.transform.SetPositionAndRotation(oldPosition,oldRotation);camera.targetTexture=oldTarget;RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(rt);}
    }
}
