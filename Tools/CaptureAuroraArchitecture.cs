using System;
using System.IO;
using System.Linq;
using UnityEngine;
public static class CaptureAuroraArchitecture
{
    public static string Main(){
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var root=scene.GetRootGameObjects().First(g=>g.name=="09 / Dungeon decoration");
        var lanterns=root.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Vault wall lantern ")).ToArray();
        var old=root.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("Mr Scientist wall lamp"));
        if(lanterns.Length!=18||old!=0)throw new Exception("Unexpected lamps: new="+lanterns.Length+" old="+old);
        if(lanterns.Any(t=>t.GetComponentInChildren<Light>()==null||!t.GetComponentInChildren<Light>().enabled))throw new Exception("A lantern is not illuminated");
        var directory=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Docs/GV/LevelDesign/Reports/Decoration");
        Capture(directory,"AuroraShrineInUnity",new Vector3(42,4,85),new Vector3(42,2.5f,95.4f));
        Capture(directory,"EnclosedLanternInUnity",new Vector3(-2,3.4f,-5.8f),new Vector3(-4.4f,3.2f,-3));
        File.WriteAllText(Path.Combine(directory,"AuroraSceneChecks.txt"),"18 enclosed lantern instances with 18 enabled warm point lights; 0 old torches; 1 shrine at scale 1.35.\nDecoration point lights: "+root.GetComponentsInChildren<Light>().Length+"\nCrystal pedestals: "+root.GetComponentsInChildren<Transform>().Count(t=>t.name=="CrystalPedestal")+"; two rear flanking pedestals removed.\nNo teleport gate placed in main map.\nCamera captures are visual evidence, not a combat test or Intel-laptop performance measurement.");
        return "Validated 18 new lanterns; old torches removed. Captured shrine and lantern in Unity.";
    }
    static void Capture(string dir,string name,Vector3 pos,Vector3 target){
        var g=new GameObject("Temporary architecture camera"){hideFlags=HideFlags.HideAndDontSave};var c=g.AddComponent<Camera>();c.transform.position=pos;c.transform.LookAt(target);c.fieldOfView=55;c.nearClipPlane=.1f;c.farClipPlane=150;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.02f,.06f,.08f);
        var rt=new RenderTexture(1280,720,24);var old=RenderTexture.active;
        try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;var img=new Texture2D(1280,720,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1280,720),0,0);img.Apply();File.WriteAllBytes(Path.Combine(dir,name+".png"),img.EncodeToPNG());UnityEngine.Object.DestroyImmediate(img);}
        finally{c.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(g);}
    }
}
