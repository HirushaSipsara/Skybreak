using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Skybreak.Descent;
public static class CheckAndCaptureCavern
{
    static string Dir=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Docs/GV/LevelDesign/Reports/Cavern");
    static void Capture(Camera camera,string name){
        var old=RenderTexture.active;var oldTarget=camera.targetTexture;var rt=new RenderTexture(1440,900,24);var img=new Texture2D(1440,900,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;img.ReadPixels(new Rect(0,0,1440,900),0,0);img.Apply();File.WriteAllBytes(Path.Combine(Dir,name+".png"),img.EncodeToPNG());}
        finally{camera.targetTexture=oldTarget;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(img);}
    }
    public static string Main(){
        if(!Application.isPlaying)throw new Exception("Use Play Mode");
        Directory.CreateDirectory(Dir);var root=GameObject.Find("15 / Cavern backdrop and entrance arch");if(root==null)throw new Exception("Cavern absent");
        if(root.GetComponentsInChildren<Collider>().Length!=0||root.GetComponentsInChildren<Light>().Length!=0)throw new Exception("Background must not alter collision/lights");
        var renderers=root.GetComponentsInChildren<MeshRenderer>();if(renderers.Any(r=>r.sharedMaterials.Any(m=>m==null)))throw new Exception("Missing material");
        var player=UnityEngine.Object.FindAnyObjectByType<DescentCharacterController>();var capsule=player.GetComponent<CharacterController>();
        player.ReturnToStart();for(int i=0;i<140;i++)player.MoveCharacter(Vector2.up,false,false,.02f);
        var end=player.transform.position;if(end.z<7.5f||Mathf.Abs(end.x)>.2f||Mathf.Abs(end.y)>.25f)throw new Exception("Entrance traversal failed "+end);
        player.ReturnToStart();
        var pitch=typeof(DescentCharacterController).GetField("pitch",BindingFlags.NonPublic|BindingFlags.Instance);pitch.SetValue(player,-8f);player.UpdateCamera(1);Capture(player.viewCamera,"EntrancePlayerView");
        foreach(var view in new[]{Tuple.Create("LoadingCourt",new Vector3(0,.04f,7)),Tuple.Create("SecurityHall",new Vector3(42,.04f,42)),Tuple.Create("BossChamber",new Vector3(42,.04f,76))}){
            capsule.enabled=false;player.transform.position=view.Item2;capsule.enabled=true;pitch.SetValue(player,-4f);player.UpdateCamera(1);Capture(player.viewCamera,view.Item1);
        }
        var temporary=UnityEngine.Object.Instantiate(player.viewCamera.gameObject);temporary.name="Temporary cavern overview";var camera=temporary.GetComponent<Camera>();camera.transform.position=new Vector3(120,125,-76);camera.transform.LookAt(new Vector3(28,3,45));camera.farClipPlane=350;camera.fieldOfView=55;
        try{Capture(camera,"CavernOverview");}finally{UnityEngine.Object.DestroyImmediate(temporary);}
        player.ReturnToStart();player.UpdateCamera(1);
        var report="Play Mode: entrance traversed from spawn without jumping; end "+end+".\nBackground has no colliders or lights. All materials assigned.\nSaved five actual Unity camera renders. Player returned to spawn.\nMesh renderer count: "+renderers.Length+". Static-batched runtime triangle count is excluded; original source budget recorded separately.\nExisting player/door/prop systems retained. No NavMesh, lighting bake or hardware performance measurement performed by this check.";
        File.WriteAllText(Path.Combine(Dir,"Validation.txt"),report);return report;
    }
}
