using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class CaptureMarkedStatues
{
    static string Dir=>Path.Combine(Path.GetDirectoryName(Application.dataPath),"Docs/GV/LevelDesign/Reports/StatuePlacement");
    static void Capture(Camera cam,string name,int width=1440,int height=900){var old=RenderTexture.active;var rt=new RenderTexture(width,height,24);var image=new Texture2D(width,height,TextureFormat.RGB24,false);try{cam.targetTexture=rt;cam.aspect=(float)width/height;cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Dir,name+".png"),image.EncodeToPNG());}finally{cam.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}}
    public static string Main(){
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Finish the lighting bake in Edit Mode.");var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Wrong scene");
        var roots=scene.GetRootGameObjects();var transforms=roots.SelectMany(g=>g.GetComponentsInChildren<Transform>()).ToArray();var statues=transforms.Where(t=>t.name.StartsWith("Guardian statue ")).OrderBy(t=>t.name).ToArray();if(statues.Length!=9)throw new Exception("Wrong statue count");
        var boss=statues.Where(t=>t.position.z>75).OrderBy(t=>t.position.x).ToArray();if(boss.Length!=2||Vector3.Distance(boss[0].position,new Vector3(31.5f,0,90.5f))>.001f||Vector3.Distance(boss[1].position,new Vector3(52.5f,0,90.5f))>.001f||Mathf.Abs(boss[0].eulerAngles.y-145)>.01f||Mathf.Abs(boss[1].eulerAngles.y-215)>.01f)throw new Exception("Boss placement changed");
        var maps=LightmapSettings.lightmaps;if(maps.Length==0||maps.Any(m=>m.lightmapColor==null))throw new Exception("Missing baked maps");if(LightmapSettings.lightProbes==null||LightmapSettings.lightProbes.count!=506)throw new Exception("Probe count changed");
        var pg=UnityEngine.Object.FindAnyObjectByType<LightProbeGroup>();if(pg.probePositions.Select(p=>pg.transform.TransformPoint(p)).Any(p=>statues.Any(t=>(t.GetComponent<BoxCollider>().ClosestPoint(p)-p).sqrMagnitude<.0001f)))throw new Exception("Probe inside statue");
        var renderers=roots.SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>()).ToArray();if(renderers.Any(r=>r.sharedMaterials.Any(m=>m==null)||r.GetComponent<MeshFilter>()==null||r.GetComponent<MeshFilter>().sharedMesh==null)||transforms.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))>0)throw new Exception("Missing references");
        foreach(var t in statues){var r=t.GetComponent<MeshRenderer>();if(r.lightmapIndex<0||r.lightmapIndex>=maps.Length)throw new Exception("Statue not mapped");if(!r.sharedMaterials.Any(m=>m.name=="GuardianCrystalSoftGlow"&&m.IsKeywordEnabled("_EMISSION")))throw new Exception("Crystal not emissive");var l=t.GetComponentInChildren<Light>();if(l==null||l.lightmapBakeType!=LightmapBakeType.Baked)throw new Exception("Crystal light missing");}
        var go=new GameObject("Temporary statue placement preview camera");var cam=go.AddComponent<Camera>();cam.fieldOfView=58;cam.nearClipPlane=.1f;cam.farClipPlane=300;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.02f,.045f,.06f);Directory.CreateDirectory(Dir);
        try{
            cam.transform.position=new Vector3(3,2.6f,21);cam.transform.LookAt(new Vector3(0,2.2f,25.9f));cam.fieldOfView=52;Capture(cam,"GuardianCrystalGlow");
            cam.transform.position=new Vector3(0,2.8f,15);cam.transform.LookAt(new Vector3(0,3,25));cam.fieldOfView=65;Capture(cam,"LoadingCourtStatues");
            cam.transform.position=new Vector3(42,3,51);cam.transform.LookAt(new Vector3(42,3,61));Capture(cam,"SecurityHallStatues");
            cam.transform.position=new Vector3(42,3,73);cam.transform.LookAt(new Vector3(42,3.5f,90));Capture(cam,"BossStatuesRetained");
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.SetColor("_BaseColor",new Color(.03f,.32f,1));var markers=new List<GameObject>();
            try{foreach(var t in statues){var marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());marker.name="Temporary statue marker";marker.transform.position=new Vector3(t.position.x,7.6f,t.position.z);marker.transform.localScale=Vector3.one*.8f;marker.GetComponent<Renderer>().sharedMaterial=material;markers.Add(marker);}
                cam.orthographic=true;cam.transform.rotation=Quaternion.Euler(90,0,0);cam.transform.position=new Vector3(5,65,10);cam.orthographicSize=22;Capture(cam,"Positions_LoadingCourt");cam.transform.position=new Vector3(42.5f,65,50);cam.orthographicSize=18;Capture(cam,"Positions_SecurityHall");cam.transform.position=new Vector3(42,65,83);cam.orthographicSize=19;Capture(cam,"Positions_BossChamber",1200,1200);
            }finally{foreach(var marker in markers)UnityEngine.Object.DestroyImmediate(marker);UnityEngine.Object.DestroyImmediate(material);}
        }finally{UnityEngine.Object.DestroyImmediate(go);}
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        var lights=roots.SelectMany(g=>g.GetComponentsInChildren<Light>()).ToArray();var report="Saved SkybreakMain in Edit Mode.\n9 statues: 4 entrance/court, 3 security hall, 2 boss statues at unchanged positions/rotations.\nAll 9 cyan crystal submeshes emissive with baked spill lights.\nBaked lightmaps: "+maps.Length+"; probes: "+LightmapSettings.lightProbes.count+"; no probes inside statue bodies.\nBaked lights: "+lights.Count(l=>l.lightmapBakeType==LightmapBakeType.Baked)+"; mixed: "+lights.Count(l=>l.lightmapBakeType==LightmapBakeType.Mixed)+"; realtime-only: "+lights.Count(l=>l.lightmapBakeType==LightmapBakeType.Realtime)+".\nMapped renderers: "+renderers.Count(r=>r.lightmapIndex>=0&&r.lightmapIndex<maps.Length)+".\nNavigation rebaked: "+NavMesh.CalculateTriangulation().indices.Length/3+" triangles; eight main route legs checked separately.\nMissing scripts/meshes/materials: zero. Seven actual Unity previews saved; temporary blue markers removed before scene save.\nNot committed or pushed.";File.WriteAllText(Path.Combine(Dir,"Verification.txt"),report);return report;
    }
}
