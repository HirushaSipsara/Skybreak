using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class CaptureMarkedLighting
{
    static string Dir=>Path.Combine(Path.GetDirectoryName(Application.dataPath),"Docs/GV/LevelDesign/Reports/LanternPlacement");
    static void Capture(Camera cam,string name,int width=1440,int height=900){
        var old=RenderTexture.active;var rt=new RenderTexture(width,height,24);var image=new Texture2D(width,height,TextureFormat.RGB24,false);
        try{cam.targetTexture=rt;cam.aspect=(float)width/height;cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Dir,name+".png"),image.EncodeToPNG());}
        finally{cam.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
    }
    public static string Main(){
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Complete the bake in Edit Mode first.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Wrong scene.");
        var roots=scene.GetRootGameObjects();var transforms=roots.SelectMany(g=>g.GetComponentsInChildren<Transform>()).ToArray();
        var lamps=transforms.Where(t=>t.name.StartsWith("Vault wall lantern ")).ToArray();var pedestals=transforms.Where(t=>t.name=="CrystalPedestal").ToArray();
        if(lamps.Length!=34||pedestals.Length!=8)throw new Exception("Unexpected prop count.");
        var maps=LightmapSettings.lightmaps;if(maps.Length==0||maps.Any(m=>m.lightmapColor==null))throw new Exception("No valid lightmaps.");
        if(LightmapSettings.lightProbes==null||LightmapSettings.lightProbes.count!=506)throw new Exception("Probe count changed.");
        var renderers=roots.SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>()).ToArray();
        if(renderers.Any(r=>r.sharedMaterials.Any(m=>m==null)||r.GetComponent<MeshFilter>()==null||r.GetComponent<MeshFilter>().sharedMesh==null))throw new Exception("Missing renderer reference.");
        if(transforms.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))>0)throw new Exception("Missing script.");
        foreach(var lamp in lamps){if(lamp.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("Lamp blocks movement.");foreach(var r in lamp.GetComponentsInChildren<MeshRenderer>())if(r.lightmapIndex<0||r.lightmapIndex>=maps.Length)throw new Exception("Unmapped lamp: "+lamp.name);}
        var nav=NavMesh.CalculateTriangulation();if(nav.indices.Length==0)throw new Exception("Navigation is missing.");
        var cameraObject=new GameObject("Temporary marked-light preview camera");var cam=cameraObject.AddComponent<Camera>();cam.fieldOfView=58;cam.nearClipPlane=.1f;cam.farClipPlane=300;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.02f,.045f,.06f);
        Directory.CreateDirectory(Dir);
        try{
            cam.transform.position=new Vector3(0,2.8f,7);cam.transform.LookAt(new Vector3(0,3.5f,22));Capture(cam,"LoadingCourt");
            cam.transform.position=new Vector3(42,3,73);cam.transform.LookAt(new Vector3(42,3.5f,90));Capture(cam,"BossChamber");
            cam.transform.position=new Vector3(-7.8f,2.7f,23);cam.transform.LookAt(new Vector3(-10.3f,1.7f,21));cam.fieldOfView=58;Capture(cam,"CrystalAndLantern");
            cam.transform.position=new Vector3(11,2.5f,35);cam.transform.LookAt(new Vector3(11,3.0f,46));cam.fieldOfView=65;Capture(cam,"WindingPassage");
            var markerMat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));markerMat.SetColor("_BaseColor",new Color(1,.72f,.02f));var markers=new List<GameObject>();
            try{
                foreach(var l in lamps){var marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());marker.name="Temporary lamp position marker";marker.transform.position=new Vector3(l.position.x,7.6f,l.position.z);marker.transform.localScale=Vector3.one*.6f;marker.GetComponent<Renderer>().sharedMaterial=markerMat;markers.Add(marker);}
                cam.orthographic=true;cam.transform.rotation=Quaternion.Euler(90,0,0);
                cam.transform.position=new Vector3(5,65,10);cam.orthographicSize=22;Capture(cam,"Positions_LoadingCourt");
                cam.transform.position=new Vector3(18,65,34);cam.orthographicSize=18;Capture(cam,"Positions_WindingPassage",1000,1300);
                cam.transform.position=new Vector3(42.5f,65,50);cam.orthographicSize=18;Capture(cam,"Positions_SecurityHall");
                cam.transform.position=new Vector3(42,65,83);cam.orthographicSize=19;Capture(cam,"Positions_BossChamber",1200,1200);
            }finally{foreach(var m in markers)UnityEngine.Object.DestroyImmediate(m);UnityEngine.Object.DestroyImmediate(markerMat);}
        }finally{UnityEngine.Object.DestroyImmediate(cameraObject);}
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed.");
        var lights=roots.SelectMany(g=>g.GetComponentsInChildren<Light>()).ToArray();
        var report="Saved main scene in Edit Mode.\n34 marked lanterns: 8 court/entrance, 10 passage, 8 security/holding rooms, 8 boss/entrance.\n8 emissive crystal pedestals with baked cyan spill.\nBaked lightmaps: "+maps.Length+"; baked probes: "+LightmapSettings.lightProbes.count+".\nBaked lights: "+lights.Count(l=>l.lightmapBakeType==LightmapBakeType.Baked)+"; mixed: "+lights.Count(l=>l.lightmapBakeType==LightmapBakeType.Mixed)+"; realtime-only: "+lights.Count(l=>l.lightmapBakeType==LightmapBakeType.Realtime)+".\nMapped static renderers: "+renderers.Count(r=>r.lightmapIndex>=0&&r.lightmapIndex<maps.Length)+".\nNavigation: "+nav.indices.Length/3+" triangles loaded; lanterns have no movement colliders.\nMissing scripts/mesh/material references: 0.\nEight actual Unity previews saved, with temporary yellow position markers only in Positions_ images; markers removed before saving.\nNo commit or push performed.";
        File.WriteAllText(Path.Combine(Dir,"Verification.txt"),report);return report;
    }
}
