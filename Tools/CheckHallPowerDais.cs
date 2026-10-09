using System;
using System.IO;
using UnityEngine;
using Skybreak.Descent;

public static class CheckHallPowerDais
{
    public static string Main(){
        if(!Application.isPlaying)throw new Exception("Use Play Mode for the actual player collision check");
        var actor=UnityEngine.Object.FindFirstObjectByType<DescentCharacterController>();var cc=actor.GetComponent<CharacterController>();var wasEnabled=actor.enabled;
        var directory=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Docs/GV/LevelDesign/Reports/HallCenterpiece");Directory.CreateDirectory(directory);
        try{
            actor.enabled=false;float peak=0;
            foreach(var forward in new[]{true,false}){
                actor.ReturnToStart();cc.enabled=false;actor.transform.position=new Vector3(0,.12f,forward?11:23);cc.enabled=true;UnityEngine.Physics.SyncTransforms();
                int steps=Mathf.CeilToInt(12f/actor.walkSpeed*60);
                for(int i=0;i<steps;i++){actor.MoveCharacter(forward?Vector2.up:Vector2.down,false,false,1f/60);peak=Mathf.Max(peak,actor.transform.position.y);}
                if(forward?actor.transform.position.z<22.8f:actor.transform.position.z>11.2f)throw new Exception("Player failed to cross dais: "+actor.transform.position);
            }
            File.WriteAllText(Path.Combine(directory,"PlayerCrossingChecks.txt"),"Actual DescentCharacterController.MoveCharacter used with the scene CharacterController and colliders.\nNorthbound and southbound walks across the centre of the dais: passed, without jumping.\nPeak player foot height: "+peak+"m.\nThis is a scripted movement/collision check, not a manual input or complete gameplay test.");
            actor.ReturnToStart();cc.enabled=false;actor.transform.position=new Vector3(-8.7f,.12f,17);actor.transform.rotation=Quaternion.identity;cc.enabled=true;UnityEngine.Physics.SyncTransforms();
            Capture(directory,"LargerGuardianWithPlayer",new Vector3(-5.3f,3.6f,10),new Vector3(-10,2.1f,17),60);
            actor.ReturnToStart();
            Capture(directory,"HallPowerDaisInUnity",new Vector3(7.2f,5,7),new Vector3(0,.9f,16),65);
            return "Player walked across the dais in both directions without jumping; saved actual hall and size-comparison captures.";
        }finally{actor.ReturnToStart();actor.enabled=wasEnabled;}
    }
    static void Capture(string directory,string name,Vector3 pos,Vector3 target,float fov){
        var go=new GameObject("Temporary hall capture"){hideFlags=HideFlags.HideAndDontSave};var c=go.AddComponent<Camera>();c.transform.position=pos;c.transform.LookAt(target);c.fieldOfView=fov;c.nearClipPlane=.1f;c.farClipPlane=150;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.02f,.06f,.08f);
        var rt=new RenderTexture(1280,900,24);var old=RenderTexture.active;
        try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;var img=new Texture2D(1280,900,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1280,900),0,0);img.Apply();File.WriteAllBytes(Path.Combine(directory,name+".png"),img.EncodeToPNG());UnityEngine.Object.DestroyImmediate(img);}
        finally{c.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
    }
}
