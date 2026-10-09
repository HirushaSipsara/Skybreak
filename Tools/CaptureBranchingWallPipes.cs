using System;
using System.IO;
using System.Linq;
using UnityEngine;
public static class CaptureBranchingWallPipes
{
    public static string Main(){
        if(!Application.isPlaying)throw new Exception("Use Play Mode");
        var root=GameObject.Find("14 / Branching copper wall vines");
        var dir=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Docs/GV/LevelDesign/Reports/WallDetails");
        var groups=root.transform.Cast<Transform>().ToArray();
        for(int region=0;region<3;region++){
            var walls=GameObject.Find("07 / Modular walls").transform;
            var candidates=groups.Where(g=>region==0?g.position.z<28:region==1?g.position.z>=28&&g.position.z<70:g.position.z>=70).OrderByDescending(g=>g.GetComponent<MeshRenderer>().bounds.size.sqrMagnitude).ToArray();
            var group=candidates.FirstOrDefault(g=>!Physics.RaycastAll(g.GetComponent<MeshRenderer>().bounds.center+g.forward*.4f,g.forward,6f).Any(h=>h.collider.transform.IsChildOf(walls)))??candidates[0];
            var bounds=group.GetComponent<MeshRenderer>().bounds;var target=bounds.center;
            var pos=target+group.forward*5.5f+Vector3.up*.3f;
            var go=new GameObject("Temporary pipe capture");var cam=go.AddComponent<Camera>();cam.transform.position=pos;cam.transform.LookAt(target);cam.fieldOfView=58;cam.nearClipPlane=.1f;cam.farClipPlane=150;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.02f,.06f,.08f);
            var rt=new RenderTexture(1280,900,24);var old=RenderTexture.active;
            try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var img=new Texture2D(1280,900,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1280,900),0,0);img.Apply();File.WriteAllBytes(Path.Combine(dir,"BranchingPipes_"+new[]{"Court","Security","Boss"}[region]+".png"),img.EncodeToPNG());UnityEngine.Object.DestroyImmediate(img);}
            finally{cam.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
        }
        return "Saved actual Unity pipe views for all three regions";
    }
}
