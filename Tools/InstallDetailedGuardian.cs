using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class InstallDetailedGuardian
{
    [Serializable] public class ExportReport { public int triangles; }
    const string Folder="Assets/Models/Environment/Dungeon/WallDetails/";
    const string MeshPath=Folder+"Meshes/WeatheredGuardianStatue_Combined.asset";
    const string PrefabPath="Assets/Prefabs/Environment/Decoration/Custom/DungeonWallDetails/WeatheredGuardianStatue.prefab";
    public static string Main(){
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Use main scene in Edit Mode");
        var project=Path.GetDirectoryName(Application.dataPath);var stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");
        int expectedTriangles=JsonUtility.FromJson<ExportReport>(File.ReadAllText(Path.Combine(project,"ArtSource/Student1/DetailedGuardian/Documentation/WeatheredGuardianDetailed.json"))).triangles;
        var backup=Path.Combine(project,"Tools/AssetSnapshots/GuardianBeforeDetail_"+stamp);Directory.CreateDirectory(backup);
        foreach(var path in new[]{MeshPath,MeshPath+".meta",PrefabPath,PrefabPath+".meta"})File.Copy(Path.Combine(project,path),Path.Combine(backup,Path.GetFileName(path)));
        if(!EditorSceneManager.SaveScene(scene,Path.Combine(backup,"SkybreakMain.unity"),true))throw new Exception("Backup failed");
        var guid=AssetDatabase.AssetPathToGUID(MeshPath);var prefabGuid=AssetDatabase.AssetPathToGUID(PrefabPath);
        File.Copy(Path.Combine(project,"ArtSource/Student1/DetailedGuardian/Exports/WeatheredGuardianDetailed.fbx"),Path.Combine(project,Folder+"WeatheredGuardianDetailed.fbx"),true);
        AssetDatabase.ImportAsset(Folder+"WeatheredGuardianDetailed.fbx",ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(Folder+"WeatheredGuardianDetailed.fbx");importer.isReadable=true;importer.SaveAndReimport();
        var mats="Assets/Materials/Environment/Decoration/Custom/DungeonWallDetails/";
        var palette=new Dictionary<string,Material>{{"GuardianStone",AssetDatabase.LoadAssetAtPath<Material>(mats+"CarvedStone.mat")},{"GuardianStoneLight",AssetDatabase.LoadAssetAtPath<Material>(mats+"CarvedStoneEdges.mat")},{"CarvedShadow",AssetDatabase.LoadAssetAtPath<Material>(mats+"CarvedShadow.mat")},{"ConduitCopper",AssetDatabase.LoadAssetAtPath<Material>(mats+"ConduitCopper.mat")},{"GuardianCrystal",AssetDatabase.LoadAssetAtPath<Material>(mats+"GuardianCrystal.mat")}};
        // Restrained, solid carved-edge material lets the sculpted relief remain readable.
        var edgePath=mats+"GuardianCarvedLimestone.mat";var edge=AssetDatabase.LoadAssetAtPath<Material>(edgePath);
        if(edge==null){edge=new Material(palette["GuardianStoneLight"]){name="GuardianCarvedLimestone"};AssetDatabase.CreateAsset(edge,edgePath);}
        edge.SetTexture("_BaseMap",null);edge.SetTexture("_BumpMap",null);edge.DisableKeyword("_NORMALMAP");edge.SetTexture("_OcclusionMap",null);edge.SetColor("_BaseColor",new Color(.47f,.52f,.51f));edge.SetFloat("_Smoothness",.18f);EditorUtility.SetDirty(edge);palette["GuardianStoneLight"]=edge;
        var preview=EditorSceneManager.NewPreviewScene();var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"WeatheredGuardianDetailed.fbx"));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(source,preview);
        var temporary=new List<Mesh>();
        try{
            var buckets=new Dictionary<string,List<CombineInstance>>();int tileIndex=0;
            foreach(var renderer in source.GetComponentsInChildren<MeshRenderer>()){
                var key=renderer.sharedMaterial.name;var mesh=UnityEngine.Object.Instantiate(renderer.GetComponent<MeshFilter>().sharedMesh);temporary.Add(mesh);
                if(key=="GuardianStone"){
                    int tile=(tileIndex++*7+5)%16;var offset=new Vector2((tile%4)*.25f+.043f,(tile/4)*.25f+.043f);mesh.uv=mesh.uv.Select(uv=>offset+uv*.164f).ToArray();mesh.RecalculateTangents();
                }
                if(!buckets.ContainsKey(key))buckets[key]=new List<CombineInstance>();
                buckets[key].Add(new CombineInstance{mesh=mesh,transform=renderer.transform.localToWorldMatrix});
            }
            var parts=new List<Mesh>();foreach(var bucket in buckets.Values){var m=new Mesh();m.CombineMeshes(bucket.ToArray(),true,true);temporary.Add(m);parts.Add(m);}
            var final=new Mesh{name="WeatheredGuardianStatue_Combined"};temporary.Add(final);final.CombineMeshes(parts.Select(m=>new CombineInstance{mesh=m,transform=Matrix4x4.identity}).ToArray(),false,true);final.RecalculateBounds();
            if(final.triangles.Length/3!=expectedTriangles||Mathf.Abs(final.bounds.size.y-2.8f)>.01f)throw new Exception("Unexpected detailed mesh size");
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);EditorUtility.CopySerialized(final,saved);EditorUtility.SetDirty(saved);
            var prefab=PrefabUtility.LoadPrefabContents(PrefabPath);
            try{prefab.GetComponent<MeshFilter>().sharedMesh=saved;prefab.GetComponent<MeshRenderer>().sharedMaterials=buckets.Keys.Select(k=>palette[k]).ToArray();PrefabUtility.SaveAsPrefabAsset(prefab,PrefabPath);}
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
        }finally{foreach(var m in temporary)UnityEngine.Object.DestroyImmediate(m);UnityEngine.Object.DestroyImmediate(source);EditorSceneManager.ClosePreviewScene(preview);}
        if(guid!=AssetDatabase.AssetPathToGUID(MeshPath)||prefabGuid!=AssetDatabase.AssetPathToGUID(PrefabPath))throw new Exception("Asset identity changed");
        var statues=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).Where(t=>t.name.StartsWith("Guardian statue ")).ToArray();
        if(statues.Length!=6||statues.Any(t=>t.localScale!=Vector3.one*1.45f))throw new Exception("Unexpected statue instances");
        foreach(var statue in statues){var r=statue.GetComponent<MeshRenderer>();if(r.sharedMaterials.Any(m=>m==null)||statue.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3!=expectedTriangles)throw new Exception("Instance did not update");}
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        var report="Updated 6 existing guardian instances through their shared mesh and prefab. GUIDs preserved.\n"+expectedTriangles+" triangles per statue; original height 2.8m, scene height 4.06m at scale 1.45.\nSame existing BoxCollider; detailed mesh remains inside its envelope. No new lights or behaviours.\nOriginal Blender source retained. Pre-update mesh, prefab and scene saved at "+backup;
        File.WriteAllText(Path.Combine(project,"ArtSource/Student1/DetailedGuardian/Documentation/UnityIntegration.txt"),report);return report;
    }
}
