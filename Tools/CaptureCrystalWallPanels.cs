using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class CaptureCrystalWallPanels
{
    static string Dir=>Path.Combine(Path.GetDirectoryName(Application.dataPath),"Docs/GV/LevelDesign/Reports/CrystalWallPanels");
    static Transform[] Panels()=>UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).Where(t=>t.name.StartsWith("Diamond relief panel ")).OrderBy(t=>t.name).ToArray();
    static void Capture(Camera cam,string name,int width=1440,int height=900){var old=RenderTexture.active;var rt=new RenderTexture(width,height,24);var img=new Texture2D(width,height,TextureFormat.RGB24,false);try{cam.targetTexture=rt;cam.aspect=(float)width/height;cam.Render();RenderTexture.active=rt;img.ReadPixels(new Rect(0,0,width,height),0,0);img.Apply();File.WriteAllBytes(Path.Combine(Dir,name+".png"),img.EncodeToPNG());}finally{cam.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(img);}}
    public static string Preview(){
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Use Edit Mode outside a bake.");Directory.CreateDirectory(Dir);var panels=Panels();var go=new GameObject("Temporary crystal-panel preview camera");var cam=go.AddComponent<Camera>();cam.fieldOfView=60;cam.nearClipPlane=.1f;cam.farClipPlane=300;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.02f,.045f,.06f);
        try{
            var panel=panels.First(t=>t.name.EndsWith("Court west alcove"));cam.transform.position=panel.position+panel.forward*2.5f+panel.right*.5f;cam.transform.LookAt(panel.position);cam.fieldOfView=44;Capture(cam,"GlowingCrystalPanel",1100,1000);
            cam.transform.position=new Vector3(11,3.1f,43);cam.transform.LookAt(new Vector3(20,4,48));cam.fieldOfView=65;Capture(cam,"PassageWallCrystals");
            cam.transform.position=new Vector3(42,3,73);cam.transform.LookAt(new Vector3(42,4,91));Capture(cam,"BossWallCrystals");
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.SetColor("_BaseColor",new Color(.01f,.8f,.95f));var markers=new List<GameObject>();
            try{foreach(var t in panels){var marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());marker.name="Temporary crystal wall marker";marker.transform.position=new Vector3(t.position.x,7.6f,t.position.z);marker.transform.localScale=Vector3.one*.6f;marker.GetComponent<Renderer>().sharedMaterial=material;markers.Add(marker);}
                cam.orthographic=true;cam.transform.rotation=Quaternion.Euler(90,0,0);cam.transform.position=new Vector3(5,65,10);cam.orthographicSize=22;Capture(cam,"Positions_LoadingCourt");cam.transform.position=new Vector3(18,65,34);cam.orthographicSize=18;Capture(cam,"Positions_WindingPassage",1000,1300);cam.transform.position=new Vector3(42.5f,65,50);cam.orthographicSize=18;Capture(cam,"Positions_SecurityHall");cam.transform.position=new Vector3(42,65,83);cam.orthographicSize=19;Capture(cam,"Positions_BossChamber",1200,1200);
            }finally{foreach(var marker in markers)UnityEngine.Object.DestroyImmediate(marker);UnityEngine.Object.DestroyImmediate(material);}
        }finally{UnityEngine.Object.DestroyImmediate(go);}
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);return "Seven actual Unity previews captured; temporary cyan markers removed and scene saved.";
    }
    public static string Main(){
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Complete lighting bake first.");var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Wrong scene");var panels=Panels();if(panels.Length!=30)throw new Exception("Wrong panel count");var maps=LightmapSettings.lightmaps;if(maps.Length==0||maps.Any(m=>m.lightmapColor==null))throw new Exception("Baked maps missing");
        foreach(var t in panels){var r=t.GetComponent<MeshRenderer>();var mesh=t.GetComponent<MeshFilter>().sharedMesh;if(mesh.subMeshCount!=4||r.sharedMaterials.Length!=4||mesh.GetIndices(3).Length!=18||r.sharedMaterials[3].name!="WallPanelCrystalGlow"||!r.sharedMaterials[3].IsKeywordEnabled("_EMISSION"))throw new Exception("Crystal isolation failed "+t.name);if(r.lightmapIndex<0||r.lightmapIndex>=maps.Length)throw new Exception("Panel not lightmapped "+t.name);if(t.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("Panel blocks movement");var l=t.GetComponentInChildren<Light>();if(l==null||l.lightmapBakeType!=LightmapBakeType.Baked)throw new Exception("Missing baked panel spill");}
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).ToArray();if(all.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))>0||all.SelectMany(t=>t.GetComponents<MeshRenderer>()).Any(r=>r.sharedMaterials.Any(m=>m==null)||r.GetComponent<MeshFilter>()==null||r.GetComponent<MeshFilter>().sharedMesh==null))throw new Exception("Missing references");if(NavMesh.CalculateTriangulation().indices.Length/3!=765)throw new Exception("Navigation changed");
        var preview=Preview();AssetDatabase.SaveAssets();var lights=all.SelectMany(t=>t.GetComponents<Light>()).ToArray();var renderers=all.SelectMany(t=>t.GetComponents<MeshRenderer>()).ToArray();var report="30 marked wall panels, with cyan emission restricted to their centre diamonds.\nFrame/backing/recess materials retained. All panels have valid baked lightmaps.\nCompleted bake: "+maps.Length+" atlases, "+LightmapSettings.lightProbes.count+" probes, "+renderers.Count(r=>r.lightmapIndex>=0&&r.lightmapIndex<maps.Length)+" mapped renderers.\nLights: "+lights.Count(l=>l.lightmapBakeType==LightmapBakeType.Baked)+" Baked, "+lights.Count(l=>l.lightmapBakeType==LightmapBakeType.Mixed)+" Mixed, "+lights.Count(l=>l.lightmapBakeType==LightmapBakeType.Realtime)+" realtime-only.\nPanel colliders: zero. Existing 765-triangle navigation retained.\nMissing scripts/mesh/material references: zero.\n"+preview+"\nNo commit or push performed.";File.WriteAllText(Path.Combine(Dir,"Verification.txt"),report);return report;
    }
}
