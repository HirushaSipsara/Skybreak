using System;
using System.IO;
using UnityEngine;
using UnityEditor;
public static class CaptureGuardianRepair
{
    public static string Main(){
        if(!Application.isPlaying)throw new Exception("Use Play Mode to render refreshed geometry");
        var statue=GameObject.Find("Guardian statue 02 / architectural decoration");
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/Environment/Dungeon/WallDetails/Meshes/WeatheredGuardianStatue_Combined.asset");
        var box=statue.GetComponent<BoxCollider>();var envelope=new Bounds(box.center,box.size);
        if(!envelope.Contains(mesh.bounds.min)||!envelope.Contains(mesh.bounds.max))throw new Exception("Geometry outside original collider");
        var dir=Path.Combine(Path.GetDirectoryName(Application.dataPath),"ArtSource/Student1/DetailedGuardian/Previews");
        var views=new[]{new Vector3(0,1.8f,4.1f),new Vector3(-3.3f,1.8f,2.4f),new Vector3(3.3f,1.8f,2.4f)};
        var names=new[]{"RepairedGuardianFront","RepairedGuardianLeft","RepairedGuardianRight"};
        for(int i=0;i<views.Length;i++){
            var go=new GameObject("Temporary guardian inspection");var cam=go.AddComponent<Camera>();cam.transform.position=statue.transform.TransformPoint(views[i]);cam.transform.LookAt(statue.transform.TransformPoint(new Vector3(0,1.4f,0)));cam.fieldOfView=44;cam.nearClipPlane=.1f;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.02f,.06f,.08f);
            var rt=new RenderTexture(1000,1000,24);var old=RenderTexture.active;
            try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var img=new Texture2D(1000,1000,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1000,1000),0,0);img.Apply();File.WriteAllBytes(Path.Combine(dir,names[i]+".png"),img.EncodeToPNG());UnityEngine.Object.DestroyImmediate(img);}
            finally{cam.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
        }
        return "Three actual Unity views saved; repaired mesh still fits original collider.";
    }
}
