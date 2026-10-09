using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Skybreak.PhysicsInteraction;

public static class CaptureDungeonWallDetails
{
    public static string Main(){
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var root=scene.GetRootGameObjects().First(g=>g.name=="11 / Carved walls and guardian statues");
        if(root.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("Guardian statue "))!=6)throw new Exception("Expected six statues");
        if(root.GetComponentsInChildren<Renderer>().Any(r=>r.sharedMaterials.Any(m=>m==null||m.shader==null||!m.shader.isSupported)))throw new Exception("Missing or unsupported material");
        var directory=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Docs/GV/LevelDesign/Reports/WallDetails");Directory.CreateDirectory(directory);
        Capture(directory,"GuardianEntrance",new Vector3(-.1f,2.45f,-4.3f),new Vector3(-3.7f,1.75f,-7.2f),60);
        Capture(directory,"CarvedLoadingCourt",new Vector3(-5.7f,3.2f,13),new Vector3(-11.3f,2.35f,20),65);
        Capture(directory,"HoldingDoorWallDetails",new Vector3(58,3.2f,46),new Vector3(64,3.1f,46),65);
        Capture(directory,"GuardianFinalChamber",new Vector3(37.5f,3.2f,83),new Vector3(31.5f,2.3f,91),65);
        Capture(directory,"ReactorWallDetails",new Vector3(42,4,85),new Vector3(42,3.1f,95.4f),65);
        return "Six guardian statues and materials verified; captured five actual Unity views.";
    }
    static void Capture(string directory,string name,Vector3 position,Vector3 target,float fov){
        var g=new GameObject("Temporary wall-detail capture"){hideFlags=HideFlags.HideAndDontSave};var c=g.AddComponent<Camera>();c.transform.position=position;c.transform.LookAt(target);c.fieldOfView=fov;c.nearClipPlane=.1f;c.farClipPlane=180;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.02f,.06f,.08f);
        var rt=new RenderTexture(1280,900,24);var old=RenderTexture.active;
        try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;var img=new Texture2D(1280,900,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1280,900),0,0);img.Apply();File.WriteAllBytes(Path.Combine(directory,name+".png"),img.EncodeToPNG());UnityEngine.Object.DestroyImmediate(img);}
        finally{c.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(g);}
    }
}
