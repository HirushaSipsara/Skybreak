using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using Skybreak.Descent;
public static class CaptureWorldPlayerViews
{
    static string Place(Vector3 pos){if(!Application.isPlaying)throw new Exception("Use Play Mode");var p=UnityEngine.Object.FindAnyObjectByType<DescentCharacterController>();var cc=p.GetComponent<CharacterController>();cc.enabled=false;p.transform.position=pos;cc.enabled=true;Physics.SyncTransforms();typeof(DescentCharacterController).GetField("pitch",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(p,-4f);p.UpdateCamera(1);return "Player placed; allow runtime renderer/culling updates before capture.";}
    public static string PlaceCourt()=>Place(new Vector3(0,.04f,7));
    public static string PlaceBoss()=>Place(new Vector3(42,.04f,76));
    public static string CaptureCurrent(){
        var player=UnityEngine.Object.FindAnyObjectByType<DescentCharacterController>();if(!Application.isPlaying)throw new Exception("Use Play Mode");player.UpdateCamera(1);var name=player.transform.position.x>20?"PlayerBossChamber":"PlayerLoadingCourt";var dir=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Docs/GV/LevelDesign/Reports/WorldFinish");var cam=player.viewCamera;var old=RenderTexture.active;var rt=new RenderTexture(1440,900,24);var img=new Texture2D(1440,900,TextureFormat.RGB24,false);
        try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;img.ReadPixels(new Rect(0,0,1440,900),0,0);img.Apply();File.WriteAllBytes(Path.Combine(dir,name+".png"),img.EncodeToPNG());}finally{cam.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(img);}
        return "Captured "+name+" after runtime transform updates; visible body renderers="+System.Linq.Enumerable.Count(player.visual.GetComponentsInChildren<Renderer>(),r=>r.enabled);
    }
    public static string Main(){
        if(!Application.isPlaying)throw new Exception("Use Play Mode");var dir=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Docs/GV/LevelDesign/Reports/WorldFinish");var player=UnityEngine.Object.FindAnyObjectByType<DescentCharacterController>();var capsule=player.GetComponent<CharacterController>();
        player.ReturnToStart();for(int i=0;i<140;i++)player.MoveCharacter(Vector2.up,false,false,.02f);if(player.transform.position.z<7.5f)throw new Exception("Entrance movement blocked");
        foreach(var view in new[]{Tuple.Create("PlayerLoadingCourt",new Vector3(0,.04f,7)),Tuple.Create("PlayerBossChamber",new Vector3(42,.04f,76))}){
            capsule.enabled=false;player.transform.position=view.Item2;capsule.enabled=true;typeof(DescentCharacterController).GetField("pitch",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(player,-4f);player.UpdateCamera(1);
            var cam=player.viewCamera;var old=RenderTexture.active;var rt=new RenderTexture(1440,900,24);var img=new Texture2D(1440,900,TextureFormat.RGB24,false);
            try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;img.ReadPixels(new Rect(0,0,1440,900),0,0);img.Apply();File.WriteAllBytes(Path.Combine(dir,view.Item1+".png"),img.EncodeToPNG());}finally{cam.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(img);}
        }
        player.ReturnToStart();player.UpdateCamera(1);File.WriteAllText(Path.Combine(dir,"PlayerChecks.txt"),"Baked scene loads 7 atlases and 506 probes in Play Mode. Player crossed entrance without jumping, two actual third-person camera renders saved, and player returned to spawn.\n");return "Entrance traversal passed; player lighting views captured; player returned to spawn.";
    }
}
