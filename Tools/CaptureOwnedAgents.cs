using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Skybreak.AI.RiftWarden;
using Skybreak.AI.Relay;
public static class CaptureOwnedAgents
{
    const string Dir="Docs/IS/AgentDesign/Reports/OwnedAgents";
    static void Capture(Camera camera,string name)
    {
        var old=RenderTexture.active;var rt=new RenderTexture(1440,1000,24);var image=new Texture2D(1440,1000,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.aspect=1.44f;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1440,1000),0,0);image.Apply();File.WriteAllBytes(Dir+"/"+name+".png",image.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
    }
    public static string Main()
    {
        Directory.CreateDirectory(Dir);var boss=UnityEngine.Object.FindAnyObjectByType<RiftWardenBrain>();var relay=UnityEngine.Object.FindAnyObjectByType<RelayBrain>();
        var go=new GameObject("Temporary owned-agent preview camera");var camera=go.AddComponent<Camera>();camera.fieldOfView=45;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.015f,.03f,.05f);camera.nearClipPlane=.1f;camera.farClipPlane=150;
        try{camera.transform.position=boss.transform.position+new Vector3(-5,3,-7);camera.transform.LookAt(boss.transform.position+Vector3.up*1.8f);Capture(camera,"RiftWardenInArena");camera.transform.position=relay.transform.position+new Vector3(1.6f,2.4f,2.3f);camera.transform.LookAt(relay.transform.position+Vector3.up*1.7f);Capture(camera,"RelayInDungeon");}
        finally{UnityEngine.Object.DestroyImmediate(go);}
        return "Actual Unity boss and Relay previews captured.";
    }
    public static string VerifyMain()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Open main in Edit Mode");
        var ts=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();int missing=ts.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));if(missing>0)throw new Exception("Missing scripts");
        var dependencies=AssetDatabase.GetDependencies(scene.path,true);if(dependencies.Any(p=>p.StartsWith("Assets/LocalCombatLab/")))throw new Exception("Permanent scene depends on local test code");
        if(AssetDatabase.GetDependencies("Assets/Prefabs/Enemies/RiftWarden/RiftWarden.prefab",true).Any(p=>p.StartsWith("Assets/LocalCombatLab/"))||AssetDatabase.GetDependencies("Assets/Prefabs/Companions/Relay/Relay.prefab",true).Any(p=>p.StartsWith("Assets/LocalCombatLab/")))throw new Exception("Permanent prefab depends on local test code");
        var surfaces=UnityEngine.Object.FindObjectsByType<Unity.AI.Navigation.NavMeshSurface>();if(surfaces.Length!=2||surfaces.Any(s=>s.navMeshData==null))throw new Exception("Navigation surfaces missing");
        var boss=UnityEngine.Object.FindAnyObjectByType<RiftWardenBrain>();if(boss.presenter.clips.Length!=20||boss.presenter.animator.avatar==null)throw new Exception("Boss clips/avatar incomplete");foreach(var p in boss.GetComponentsInChildren<RiftDamagePart>())if(p.GetComponent<Collider>().bounds.size.magnitude>4)throw new Exception("Hitbox scale wrong");
        Main();EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();string report="Main scene saved in Edit Mode.\nTwo separate navigation surfaces: original Medium Robot and wider Rift Warden arena.\n20 boss clips / Generic avatar; independent arm/core hitboxes; 100x imported bone scale compensated.\nMissing scripts: zero. Main scene and both owned prefabs have no LocalCombatLab dependency.\nExisting environment lighting retained: "+LightmapSettings.lightmaps.Length+" atlases / "+LightmapSettings.lightProbes.count+" probes.\nTemporary shooter/player health/double jump/restart/lab scene live under locally Git-excluded Assets/LocalCombatLab; local test tools likewise excluded.\nNo commit or push performed. Final teammate integration, game build and weakest-machine profiling remain outstanding.";
        File.WriteAllText(Dir+"/Verification.txt",report);return report;
    }
}
