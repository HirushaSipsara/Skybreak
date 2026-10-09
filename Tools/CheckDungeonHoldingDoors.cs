using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Skybreak.PhysicsInteraction;

public static class CheckDungeonHoldingDoors
{
    static DungeonHoldingDoor[] Doors(){var ds=UnityEngine.Object.FindObjectsByType<DungeonHoldingDoor>();if(ds.Length!=2)throw new Exception("Expected two doors");return ds;}
    static string Dir=>Path.Combine(Path.GetDirectoryName(Application.dataPath),"Docs/GV/LevelDesign/Reports/Doors");
    static bool Blocked(DungeonHoldingDoor d){
        UnityEngine.Physics.SyncTransforms();var start=d.transform.position-Vector3.right*1.5f;
        return UnityEngine.Physics.CapsuleCastAll(start+Vector3.up*.50f,start+Vector3.up*2.06f,.416f,Vector3.right,3,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.transform.IsChildOf(d.transform));
    }
    public static string ClosedThenOpen(){
        Directory.CreateDirectory(Dir);var ds=Doors();foreach(var d in ds)if(!d.IsClosed||!Blocked(d))throw new Exception("Closed gate does not block: "+d.name);
        Capture("HoldingGateClosed");File.WriteAllText(Path.Combine(Dir,"DoorChecks.txt"),"Closed: 2/2 gates block an Arc-sized capsule through the doorway.\n");foreach(var d in ds)d.Open();return "Closed collision passed 2/2; gates are opening.";
    }
    public static string OpenThenClose(){
        var ds=Doors();foreach(var d in ds)if(!d.IsOpen||Blocked(d))throw new Exception("Open gate does not clear: "+d.name);
        Capture("HoldingGateOpen");
        var probe=new GameObject("Temporary door occupancy check"){hideFlags=HideFlags.HideAndDontSave};
        try{
            probe.transform.position=ds[0].transform.position+Vector3.up*.04f;var cc=probe.AddComponent<CharacterController>();cc.height=2.44f;cc.radius=.416f;cc.center=Vector3.up*1.22f;UnityEngine.Physics.SyncTransforms();
            ds[0].Close();if(ds[0].IsMoving||!ds[0].IsOpen)throw new Exception("Door attempted to close on occupied threshold");
        }finally{UnityEngine.Object.DestroyImmediate(probe);UnityEngine.Physics.SyncTransforms();}
        File.AppendAllText(Path.Combine(Dir,"DoorChecks.txt"),"Open: 2/2 gates clear an Arc-sized capsule through the doorway.\nOccupied threshold: Close() rejected while a temporary CharacterController occupied the opening.\n");
        foreach(var d in ds)d.Close();return "Opening animation completed; clearance passed 2/2; occupied-threshold safety passed. Gates closing.";
    }
    public static string ConfirmClosed(){
        foreach(var d in Doors())if(!d.IsClosed||!Blocked(d))throw new Exception("Gate failed to close");File.AppendAllText(Path.Combine(Dir,"DoorChecks.txt"),"Closing animation completed: 2/2 gates closed and block again.\nThese are runtime door/collider checks, not encounter spawning or AI-navigation tests.\n");return "Both closing animations completed and colliders block again.";
    }
    static void Capture(string name){
        var go=new GameObject("Temporary door camera"){hideFlags=HideFlags.HideAndDontSave};var camera=go.AddComponent<Camera>();camera.transform.position=new Vector3(58,2.6f,46);camera.transform.LookAt(new Vector3(64,1.6f,46));camera.fieldOfView=55;camera.nearClipPlane=.1f;camera.farClipPlane=100;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.02f,.06f,.08f);
        var rt=new RenderTexture(1000,800,24);var old=RenderTexture.active;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var img=new Texture2D(1000,800,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1000,800),0,0);img.Apply();File.WriteAllBytes(Path.Combine(Dir,name+".png"),img.EncodeToPNG());UnityEngine.Object.DestroyImmediate(img);}
        finally{camera.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
    }
}
