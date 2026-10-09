using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Skybreak.Descent;

public static class CheckEncounterPowerDaises
{
    public static string Main(){
        if(!Application.isPlaying)throw new Exception("Use Play Mode");
        var actor=UnityEngine.Object.FindAnyObjectByType<DescentCharacterController>();var cc=actor.GetComponent<CharacterController>();var enabled=actor.enabled;
        var dir=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Docs/GV/LevelDesign/Reports/HallCenterpiece");
        var result=new System.Text.StringBuilder();
        try{
            actor.enabled=false;
            foreach(var centre in new[]{new Vector3(42,0,50),new Vector3(42,0,84.5f)}){
                float radius=centre.z<60?3.5f:6;float distance=radius*2+2;
                foreach(bool north in new[]{true,false}){
                    actor.ReturnToStart();cc.enabled=false;actor.transform.position=centre+new Vector3(0,.12f,north?-radius-1:radius+1);cc.enabled=true;Physics.SyncTransforms();
                    int steps=Mathf.CeilToInt(distance/actor.walkSpeed*60);
                    for(int i=0;i<steps;i++)actor.MoveCharacter(north?Vector2.up:Vector2.down,false,false,1f/60);
                    float expected=centre.z+(north?radius+1:-radius-1);
                    if(Mathf.Abs(actor.transform.position.z-expected)>.25f)throw new Exception("Crossing failed: "+actor.transform.position+" expected z="+expected);
                    if(actor.transform.position.y<-.1f||actor.transform.position.y>.35f)throw new Exception("Unexpected foot height");
                }
                result.AppendLine("Circle at "+centre+": actual player controller crossed north and south without jumping.");
            }
            actor.ReturnToStart();Capture(dir,"SecurityHallCircle",new Vector3(47,6,40),new Vector3(42,.5f,51));
            Capture(dir,"BossChamberCircle",new Vector3(47,7,73),new Vector3(42,.5f,86));
            File.WriteAllText(Path.Combine(dir,"EncounterCrossingChecks.txt"),result+"Scripted controller/collision checks and actual Unity renders. Full combat and hardware performance were not tested.");return result.ToString();
        }finally{actor.ReturnToStart();actor.enabled=enabled;}
    }
    static void Capture(string dir,string name,Vector3 pos,Vector3 target){
        var go=new GameObject("Temporary encounter capture"){hideFlags=HideFlags.HideAndDontSave};var cam=go.AddComponent<Camera>();cam.transform.position=pos;cam.transform.LookAt(target);cam.fieldOfView=65;cam.nearClipPlane=.1f;cam.farClipPlane=150;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.02f,.06f,.08f);
        var rt=new RenderTexture(1280,900,24);var old=RenderTexture.active;
        try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var img=new Texture2D(1280,900,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1280,900),0,0);img.Apply();File.WriteAllBytes(Path.Combine(dir,name+".png"),img.EncodeToPNG());UnityEngine.Object.DestroyImmediate(img);}
        finally{cam.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
    }
}
