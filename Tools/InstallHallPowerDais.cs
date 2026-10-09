using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class InstallHallPowerDais
{
    const string Models="Assets/Models/Environment/Dungeon/HallPowerDais/";
    const string PrefabPath="Assets/Prefabs/Environment/Decoration/Custom/HallPowerDais/AuroraHallPowerDais.prefab";
    static GameObject BuildPrefab(){
        var importer=(ModelImporter)AssetImporter.GetAtPath(Models+"AuroraHallPowerDais.fbx");if(importer==null)throw new Exception("Missing dais model");importer.isReadable=true;importer.SaveAndReimport();
        var keys=new Dictionary<string,string>{{"GuardianStone","CarvedStone"},{"GuardianStoneLight","CarvedStoneEdges"},{"ConduitCopper","ConduitCopper"},{"GuardianCrystal","GuardianCrystal"}};
        var materialRoot="Assets/Materials/Environment/Decoration/Custom/DungeonWallDetails/";
        var preview=EditorSceneManager.NewPreviewScene();var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Models+"AuroraHallPowerDais.fbx"));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(model,preview);
        var root=new GameObject("AuroraHallPowerDais");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,preview);
        var tempMeshes=new List<Mesh>();
        try{
            var buckets=new Dictionary<string,List<CombineInstance>>();int stoneIndex=0;
            foreach(var r in model.GetComponentsInChildren<MeshRenderer>()){
                var key=r.sharedMaterial.name;var mesh=UnityEngine.Object.Instantiate(r.GetComponent<MeshFilter>().sharedMesh);tempMeshes.Add(mesh);
                if(key.StartsWith("GuardianStone")){int tile=(stoneIndex++*7+3)%16;var offset=new Vector2((tile%4)*.25f+.004f,(tile/4)*.25f+.004f);mesh.uv=mesh.uv.Select(uv=>offset+uv*.242f).ToArray();mesh.RecalculateTangents();}
                if(!buckets.ContainsKey(key))buckets[key]=new List<CombineInstance>();buckets[key].Add(new CombineInstance{mesh=mesh,transform=r.transform.localToWorldMatrix});
            }
            var materialMeshes=new List<Mesh>();foreach(var bucket in buckets.Values){var mesh=new Mesh();mesh.CombineMeshes(bucket.ToArray(),true,true);materialMeshes.Add(mesh);tempMeshes.Add(mesh);}
            var combined=new Mesh{name="AuroraHallPowerDais / combined mesh"};combined.CombineMeshes(materialMeshes.Select(m=>new CombineInstance{mesh=m,transform=Matrix4x4.identity}).ToArray(),false,true);combined.RecalculateBounds();
            var path=Models+"AuroraHallPowerDais_Combined.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null){AssetDatabase.CreateAsset(combined,path);saved=combined;}else{EditorUtility.CopySerialized(combined,saved);UnityEngine.Object.DestroyImmediate(combined);EditorUtility.SetDirty(saved);}
            root.AddComponent<MeshFilter>().sharedMesh=saved;root.AddComponent<MeshRenderer>().sharedMaterials=buckets.Keys.Select(k=>AssetDatabase.LoadAssetAtPath<Material>(materialRoot+keys[k]+".mat")??throw new Exception("Missing material "+k)).ToArray();
            root.AddComponent<MeshCollider>().sharedMesh=saved;GameObjectUtility.SetStaticEditorFlags(root,StaticEditorFlags.BatchingStatic);
            return PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        }finally{foreach(var mesh in tempMeshes)UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(model);EditorSceneManager.ClosePreviewScene(preview);}
    }
    public static string Main(){
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Open main scene outside Play Mode");
        if(scene.GetRootGameObjects().Any(g=>g.name=="12 / Loading court power dais"))throw new Exception("Dais already installed; preserve edits");
        var statues=scene.GetRootGameObjects().Single(g=>g.name=="11 / Carved walls and guardian statues").GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Guardian statue ")).ToArray();if(statues.Length!=6)throw new Exception("Expected six guardian statues");
        var project=Path.GetDirectoryName(Application.dataPath);Directory.CreateDirectory(Path.Combine(project,Path.GetDirectoryName(PrefabPath)));AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);var prefab=BuildPrefab();
        var backup=Path.Combine(project,"Tools/SceneSnapshots/SkybreakMain_before_larger_statues_and_hall_dais_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity");Directory.CreateDirectory(Path.GetDirectoryName(backup));if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup failed");
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Larger guardian statues and hall centerpiece");
        foreach(var statue in statues){Undo.RecordObject(statue,"Enlarge guardian statue");statue.localScale=Vector3.one*1.45f;PrefabUtility.RecordPrefabInstancePropertyModifications(statue);}
        var parent=new GameObject("12 / Loading court power dais");Undo.RegisterCreatedObjectUndo(parent,"Hall centerpiece");var dais=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent.transform);dais.transform.position=new Vector3(0,0,16);Undo.RegisterCreatedObjectUndo(dais,"Hall centerpiece");PrefabUtility.RecordPrefabInstancePropertyModifications(dais.transform);
        Physics.SyncTransforms();var floor=scene.GetRootGameObjects().First(g=>g.name.StartsWith("01 / STONE FLOOR"));var overlap=new List<string>();
        foreach(var statue in statues){var c=statue.GetComponent<BoxCollider>();foreach(var other in Physics.OverlapBox(c.bounds.center,c.bounds.extents,Quaternion.identity,~0,QueryTriggerInteraction.Ignore)){
            if(other==c||other.transform.IsChildOf(floor.transform))continue;Vector3 direction;float distance;
            if(Physics.ComputePenetration(c,c.transform.position,c.transform.rotation,other,other.transform.position,other.transform.rotation,out direction,out distance)&&distance>.02f)overlap.Add(statue.name+" with "+other.name+" by "+distance);
        }}
        if(overlap.Count>0){Undo.RevertAllDownToGroup(undo);throw new Exception("Statue placement overlaps; rolled back: "+string.Join(",",overlap));}
        var routes=new[]{new[]{new Vector2(0,-4),new Vector2(0,21),new Vector2(21,21),new Vector2(21,35),new Vector2(9,35),new Vector2(9,46),new Vector2(42,46),new Vector2(42,66),new Vector2(42,84)},new[]{new Vector2(42,54),new Vector2(19,54)},new[]{new Vector2(42,46),new Vector2(66,46)}};int blocked=0;
        foreach(var route in routes)for(int i=1;i<route.Length;i++){
            var delta=route[i]-route[i-1];var start=new Vector3(route[i-1].x,.08f,route[i-1].y);
            blocked+=Physics.CapsuleCastAll(start+Vector3.up*.44f,start+Vector3.up*2.02f,.416f,new Vector3(delta.x,0,delta.y).normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore).Count(h=>statues.Any(t=>h.collider.transform.IsChildOf(t)));
        }
        if(blocked>0){Undo.RevertAllDownToGroup(undo);throw new Exception("Larger statues block a route; rolled back");}
        Undo.CollapseUndoOperations(undo);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed");
        var report="Six guardian statues scaled to 1.45: 45% larger, approximately 4.06m tall.\nStatue overlap checks: 0 unexpected overlaps.\nLarger statue route obstructions: "+blocked+"\nHall dais at (0,0,16): 5.5m diameter, 0.136m maximum height, 1380 triangles.\nPlayer step offset: 0.3m; actual crossing check follows separately.\nExisting stone columns retained at their original scale. No extra point lights.\nBackup: "+backup;
        var dir=Path.Combine(project,"Docs/GV/LevelDesign/Reports/HallCenterpiece");Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"PlacementChecks.txt"),report);Selection.activeGameObject=dais;SceneView.lastActiveSceneView?.FrameSelected();return report;
    }
}
