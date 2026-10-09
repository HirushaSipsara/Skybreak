using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
public static class FinishAndCaptureWorldBake
{
    static string Dir=>Path.Combine(Path.GetDirectoryName(Application.dataPath),"Docs/GV/LevelDesign/Reports/WorldFinish");
    static void Capture(Camera cam,string name){var old=RenderTexture.active;var rt=new RenderTexture(1440,900,24);var img=new Texture2D(1440,900,TextureFormat.RGB24,false);try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;img.ReadPixels(new Rect(0,0,1440,900),0,0);img.Apply();File.WriteAllBytes(Path.Combine(Dir,name+".png"),img.EncodeToPNG());}finally{cam.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(img);}}
    public static string Main(){
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Finish bake in Edit Mode first");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Wrong scene");
        var maps=LightmapSettings.lightmaps;if(maps.Length==0||maps.Any(m=>m.lightmapColor==null))throw new Exception("No valid baked lightmaps");
        if(LightmapSettings.lightProbes==null||LightmapSettings.lightProbes.count<100)throw new Exception("Probes not baked");
        var surface=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<NavMeshSurface>()).Single();if(surface.navMeshData==null||NavMesh.CalculateTriangulation().indices.Length==0)throw new Exception("Navigation missing");
        var renderers=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>()).ToArray();var missing=renderers.Count(r=>r.sharedMaterials.Any(m=>m==null));if(missing>0)throw new Exception("Missing materials "+missing);
        int missingScripts=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));if(missingScripts>0)throw new Exception("Missing scripts "+missingScripts);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        var cameraObject=new GameObject("Temporary world finish camera");var cam=cameraObject.AddComponent<Camera>();cam.fieldOfView=58;cam.nearClipPlane=.12f;cam.farClipPlane=300;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.02f,.045f,.06f);
        try{
            cam.transform.position=new Vector3(0,2.8f,7);cam.transform.LookAt(new Vector3(0,4.0f,22));Capture(cam,"BakedLoadingCourt");
            cam.transform.position=new Vector3(42,3.0f,73);cam.transform.LookAt(new Vector3(42,4,90));Capture(cam,"BakedBossChamber");
            cam.transform.position=new Vector3(18,3.0f,36);cam.transform.LookAt(new Vector3(12,4,43));Capture(cam,"BakedPassage");
            cam.transform.position=new Vector3(58.4f,2.7f,46);cam.transform.LookAt(new Vector3(64,1.8f,46));Capture(cam,"BakedHoldingDoor");
            var tri=NavMesh.CalculateTriangulation();var go=new GameObject("Temporary navigation overlay");var mesh=new Mesh{name="Baked NavMesh preview"};mesh.vertices=tri.vertices.Select(v=>v+Vector3.up*.08f).ToArray();mesh.triangles=tri.indices;mesh.RecalculateNormals();var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.SetColor("_BaseColor",new Color(.04f,.70f,.8f));go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
            try{cam.transform.position=new Vector3(58,99,5);cam.transform.LookAt(new Vector3(29,0,45));cam.fieldOfView=62;Capture(cam,"InitialNavMeshOverview");}finally{UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(material);}
        }finally{UnityEngine.Object.DestroyImmediate(cameraObject);}
        var lights=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>()).ToArray();
        var report="Completed lighting bake: "+maps.Length+" lightmap atlases (colour + shadowmask where present), "+LightmapSettings.lightProbes.count+" baked light probes.\nLighting data: "+AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset)+".\nLightmaps: "+string.Join(", ",maps.Select(m=>AssetDatabase.GetAssetPath(m.lightmapColor)))+".\nMixed lights "+lights.Count(l=>l.lightmapBakeType==LightmapBakeType.Mixed)+"; baked lights "+lights.Count(l=>l.lightmapBakeType==LightmapBakeType.Baked)+"; realtime-only lights "+lights.Count(l=>l.lightmapBakeType==LightmapBakeType.Realtime)+".\nMapped static renderers: "+renderers.Count(r=>r.lightmapIndex>=0&&r.lightmapIndex<maps.Length)+".\nInitial NavMesh data saved, with "+NavMesh.CalculateTriangulation().indices.Length/3+" triangles in Edit Mode. Missing scripts/materials: 0.\nFive actual Unity previews saved. Scene saved in Edit Mode.\nThese checks do not establish final autonomous agent behavior or integrated-GPU frame rate.";
        File.WriteAllText(Path.Combine(Dir,"LightingBake.txt"),report);return report;
    }
}
