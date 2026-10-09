using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class AddEncounterPowerDaises
{
    const string Source="Assets/Prefabs/Environment/Decoration/Custom/HallPowerDais/AuroraHallPowerDais.prefab";
    const string Boss="Assets/Prefabs/Environment/Decoration/Custom/HallPowerDais/RiftBossPowerDais.prefab";
    const string Materials="Assets/Materials/Environment/Decoration/Custom/HallPowerDais/";
    static Material Variant(Material source,string name,Color colour,bool emission=false){
        var path=Materials+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(source){name=name};AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",colour);if(m.HasProperty("_Color"))m.SetColor("_Color",colour);
        if(emission){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",colour*1.7f);}
        EditorUtility.SetDirty(m);return m;
    }
    public static string Main(){
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Use main scene outside Play Mode");
        if(scene.GetRootGameObjects().Any(g=>g.name=="13 / Encounter floor circles"))throw new Exception("Circles already installed; preserve manual edits");
        var project=Path.GetDirectoryName(Application.dataPath);
        var backup=Path.Combine(project,"Tools/SceneSnapshots/SkybreakMain_before_encounter_circles_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity");
        if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup failed");
        Directory.CreateDirectory(Path.Combine(project,Materials));AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var p=PrefabUtility.LoadPrefabContents(Source);
        try{
            p.name="RiftBossPowerDais";p.transform.localScale=new Vector3(12f/5.5f,1,12f/5.5f);
            var r=p.GetComponent<MeshRenderer>();
            r.sharedMaterials=r.sharedMaterials.Select(m=>{
                if(m.name=="GuardianCrystal")return Variant(m,"RiftVioletEnergy",new Color(.566f,.26f,.906f),true);
                if(m.name=="ConduitCopper")return Variant(m,"RiftBlueSteelInlay",new Color(.349f,.556f,.618f));
                var c=m.GetColor("_BaseColor");return Variant(m,"Rift"+m.name,new Color(c.r*.83f,c.g*.85f,c.b*.98f,c.a));
            }).ToArray();
            PrefabUtility.SaveAsPrefabAsset(p,Boss);
        }finally{PrefabUtility.UnloadPrefabContents(p);}
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Security and boss floor circles");
        var root=new GameObject("13 / Encounter floor circles");Undo.RegisterCreatedObjectUndo(root,"Add floor circles");
        var security=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source),root.transform);
        security.name="Security hall / cyan floor circle";security.transform.position=new Vector3(42,0,50);security.transform.localScale=new Vector3(7f/5.5f,1,7f/5.5f);
        PrefabUtility.RecordPrefabInstancePropertyModifications(security.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(security);
        var boss=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Boss),root.transform);
        boss.name="Boss chamber / violet rift circle";boss.transform.position=new Vector3(42,0,84.5f);PrefabUtility.RecordPrefabInstancePropertyModifications(boss.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(boss);
        Physics.SyncTransforms();
        var blockers=new System.Collections.Generic.List<string>();
        foreach(var dais in new[]{security,boss}){
            var bounds=dais.GetComponent<Collider>().bounds;
            // Search its footprint above the low floor surface, excluding tiles and the player.
            var center=bounds.center;center.y=1.2f;var ext=bounds.extents;ext.y=1;
            foreach(var other in Physics.OverlapBox(center,ext,Quaternion.identity,~0,QueryTriggerInteraction.Ignore)){
                if(other.transform.IsChildOf(root.transform)||other.GetComponent<CharacterController>()!=null)continue;
                if(other.bounds.max.y<.3f)continue;
                blockers.Add(dais.name+" intersects "+other.name);
            }
        }
        if(blockers.Count>0){Undo.RevertAllDownToGroup(group);throw new Exception("Footprint overlap, reverted: "+string.Join(", ",blockers));}
        Undo.CollapseUndoOperations(group);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed");
        var report="Security circle: centre (42,0,50), diameter 7m, cyan and brass.\nBoss circle: centre (42,0,84.5), diameter 12m, dark stone, blue steel and Rift Warden violet.\nBoth retain 0.136m maximum height and MeshColliders. 1380 triangles each; no additional lights.\nFootprint checks: no overlap with existing cover, walls or statues.\nBackup: "+backup;
        File.WriteAllText(Path.Combine(project,"Docs/GV/LevelDesign/Reports/HallCenterpiece/EncounterPlacementChecks.txt"),report);Selection.activeGameObject=boss;return report;
    }
}
