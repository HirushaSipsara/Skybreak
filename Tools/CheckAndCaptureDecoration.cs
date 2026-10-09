using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public static class CheckAndCaptureDecoration
{
    public static string Main()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var decoration=scene.GetRootGameObjects().First(g=>g.name=="09 / Dungeon decoration");
        Physics.SyncTransforms();
        var routes=new[]{new[]{new Vector2(0,-4),new Vector2(0,21),new Vector2(21,21),new Vector2(21,35),new Vector2(9,35),new Vector2(9,46),new Vector2(42,46),new Vector2(42,66),new Vector2(42,84)},new[]{new Vector2(42,54),new Vector2(19,54)},new[]{new Vector2(42,46),new Vector2(66,46)}};
        var hits=new List<string>();
        foreach(var route in routes)for(int i=1;i<route.Length;i++){
            var delta=route[i]-route[i-1];var start=new Vector3(route[i-1].x,.08f,route[i-1].y);
            var direction=new Vector3(delta.x,0,delta.y).normalized;
            foreach(var hit in Physics.CapsuleCastAll(start+Vector3.up*.44f,start+Vector3.up*2.02f,.416f,direction,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
                if(hit.collider.transform.IsChildOf(decoration.transform))hits.Add(route[i-1]+" -> "+route[i]+": "+hit.collider.name);
        }
        var directory=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Docs/GV/LevelDesign/Reports/Decoration");
        Directory.CreateDirectory(directory);
        File.WriteAllLines(Path.Combine(directory,"RouteClearance.txt"),new[]{"Decoration-only capsule sweeps for Arc-sized capsule. This is a geometric clearance check, not player-input or NavMesh validation.","Decoration obstructions: "+hits.Count}.Concat(hits));
        Capture(directory,"EntranceDecorated",new Vector3(1.5f,3.5f,-7),new Vector3(0,2.1f,16));
        Capture(directory,"SecurityDecorated",new Vector3(34,4,41),new Vector3(44,2.4f,55));
        Capture(directory,"ReactorDecorated",new Vector3(42,4.1f,73),new Vector3(42,3,94));
        Capture(directory,"DecorationOverview",new Vector3(103,118,-55),new Vector3(25,0,43));
        return "Route obstructions from added decoration: "+hits.Count+"; saved four actual Unity camera captures.";
    }
    static void Capture(string directory,string name,Vector3 position,Vector3 target)
    {
        var go=new GameObject("Temporary decoration capture"){hideFlags=HideFlags.HideAndDontSave};
        var camera=go.AddComponent<Camera>();camera.transform.position=position;camera.transform.LookAt(target);camera.fieldOfView=name=="DecorationOverview"?48:65;camera.nearClipPlane=.15f;camera.farClipPlane=350;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.02f,.06f,.08f);
        var rt=new RenderTexture(1280,720,24);var before=RenderTexture.active;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Path.Combine(directory,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);}
        finally{camera.targetTexture=null;RenderTexture.active=before;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
    }
}
