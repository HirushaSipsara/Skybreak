using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ReplaceWithSelectedDungeonProps
{
    const string Prefabs="Assets/Prefabs/Environment/Decoration/";
    static Transform root;
    static Material flame;
    static int crates,lamps,skulls,crystals;
    static Transform Group(string name){var g=new GameObject(name);Undo.RegisterCreatedObjectUndo(g,"Replace selected dungeon props");g.transform.SetParent(root,false);return g.transform;}
    static Bounds BoundsOf(GameObject g){var rs=g.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;}
    static GameObject Place(Transform parent,string path,string name,Vector3 bottom,float height,float yaw=0){
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(asset==null)throw new Exception("Missing "+path);
        var g=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);Undo.RegisterCreatedObjectUndo(g,"Replace selected dungeon props");
        g.name=name;g.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        var b=BoundsOf(g);g.transform.localScale*=height/b.size.y;g.transform.rotation=Quaternion.Euler(0,yaw,0);
        b=BoundsOf(g);g.transform.position+=bottom-new Vector3(b.center.x,b.min.y,b.center.z);
        // Record prefab overrides so the saved scene retains the selected transform.
        PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);return g;
    }
    static void Crate(Transform p,Vector3 bottom,float height,float yaw,string variant="Objects_Crate 2"){
        Place(p,Prefabs+"MrScientist_Dungeon/"+variant+".prefab",variant,bottom,height,yaw);crates++;
    }
    static void Cover(Transform p,string id,float x,float z,bool low,float yaw=0){
        var g=new GameObject("COVER "+id+" / Mr Scientist wooden crates");g.transform.SetParent(p);g.transform.position=new Vector3(x,0,z);
        if(low){var side=Quaternion.Euler(0,yaw,0)*Vector3.right;float offset=id.StartsWith("T")?.55f:.8f;float h=id.StartsWith("T")?1.1f:1.6f;
            Crate(g.transform,g.transform.position-side*offset,h,yaw);Crate(g.transform,g.transform.position+side*offset,h,yaw);
        }else{
            Crate(g.transform,g.transform.position,1.8f,yaw);
            Crate(g.transform,g.transform.position+Vector3.up*1.8f,1.05f,yaw+12);
        }
    }
    static void Skull(Transform p,float x,float z,float yaw){Place(p,Prefabs+"Quaternius_ModularDungeons/Skull.prefab","Skull",new Vector3(x,.025f,z),.32f,yaw);skulls++;}
    static void Crystal(Transform p,float x,float z,bool light){
        var g=Place(p,"Assets/Prefabs/Environment/Dungeon/CrystalPedestal.prefab","CrystalPedestal",new Vector3(x,0,z),2.7f);
        if(g.GetComponentsInChildren<Collider>().Length==0){var b=BoundsOf(g);var c=g.AddComponent<BoxCollider>();c.center=g.transform.InverseTransformPoint(b.center);c.size=new Vector3(b.size.x/g.transform.lossyScale.x,b.size.y/g.transform.lossyScale.y,b.size.z/g.transform.lossyScale.z);}
        if(light)Point(g.transform,new Vector3(x,2.1f,z),new Color(.18f,.78f,1),1.3f,5);
        crystals++;
    }
    static void Point(Transform parent,Vector3 position,Color color,float intensity,float range){
        var g=new GameObject("Local glow / no realtime shadows");g.transform.SetParent(parent);g.transform.position=position;
        var l=g.AddComponent<Light>();l.type=LightType.Point;l.color=color;l.range=range;l.intensity=intensity;l.shadows=LightShadows.None;
    }
    static void Lamp(Transform p,float x,float z,float yaw,bool light){
        var g=Place(p,Prefabs+"MrScientist_Dungeon/Prefabs_Torch Models_Torch Holder.prefab","Mr Scientist wall lamp",new Vector3(x,2.4f,z),1.6f,yaw);
        foreach(var r in g.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("Flame"))){r.sharedMaterials=r.sharedMaterials.Select(m=>flame).ToArray();PrefabUtility.RecordPrefabInstancePropertyModifications(r);}
        if(light)Point(g.transform,new Vector3(x,3.65f,z),new Color(1,.54f,.22f),2.1f,6);
        lamps++;
    }
    public static string Main(){
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Open main map outside Play Mode.");
        var project=Path.GetDirectoryName(Application.dataPath);var backup=Path.Combine(project,"Tools/SceneSnapshots/SkybreakMain_before_selected_props_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity");
        Directory.CreateDirectory(Path.GetDirectoryName(backup));if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup failed");
        foreach(var p in new[]{Prefabs+"MrScientist_Dungeon/Objects_Crate 2.prefab",Prefabs+"MrScientist_Dungeon/Prefabs_Torch Models_Torch Holder.prefab",Prefabs+"Quaternius_ModularDungeons/Skull.prefab","Assets/Prefabs/Environment/Dungeon/CrystalPedestal.prefab"})if(AssetDatabase.LoadAssetAtPath<GameObject>(p)==null)throw new Exception("Missing "+p);
        var flamePath="Assets/Materials/Environment/Decoration/MapAccents/MrScientist_LampFlame.mat";
        flame=AssetDatabase.LoadAssetAtPath<Material>(flamePath);
        if(flame==null){var original=AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"MrScientist_Dungeon/Prefabs_Torch Models_Torch Holder.prefab").GetComponentsInChildren<Renderer>().First(r=>r.name.StartsWith("Flame")).sharedMaterial;flame=new Material(original);AssetDatabase.CreateAsset(flame,flamePath);}
        flame.EnableKeyword("_EMISSION");flame.SetColor("_EmissionColor",new Color(1,.44f,.10f)*2.5f);flame.SetTexture("_EmissionMap",flame.GetTexture("_BaseMap"));EditorUtility.SetDirty(flame);
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Selected dungeon props with preserved columns");
        var old=scene.GetRootGameObjects().First(g=>g.name=="09 / Dungeon decoration");
        var columns=old.GetComponentsInChildren<Transform>().Where(t=>t.name=="Column2" && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)).ToArray();
        if(columns.Length!=12)throw new Exception("Expected 12 preserved columns; found "+columns.Length);
        var newRoot=new GameObject("09 / Dungeon decoration");root=newRoot.transform;Undo.RegisterCreatedObjectUndo(newRoot,"Replace dungeon props");
        var kept=Group("01 / Preserved stone columns");foreach(var column in columns)Undo.SetTransformParent(column,kept,"Keep stone columns");
        Undo.DestroyObjectImmediate(old);
        crates=lamps=skulls=crystals=0;
        var entry=Group("02 / Entrance and loading court / crate cover");
        Cover(entry,"S01",-3.5f,1.8f,false);Cover(entry,"S02",3.5f,2,true,90);
        Cover(entry,"A01",-6.5f,11,true);Cover(entry,"A02",6.5f,12,false);Cover(entry,"A03",-8,18,false);
        Cover(entry,"A04",7.5f,18.5f,true);Cover(entry,"A05",-6,24,true);Cover(entry,"A06",8.5f,25,false);
        Crate(entry,new Vector3(-15.6f,0,12.4f),1.35f,20,"Objects_Crate");Crate(entry,new Vector3(-16.3f,0,14.5f),1.2f,-10);
        Crate(entry,new Vector3(15.5f,0,13),1.4f,15,"Objects_Crate 3");
        var tunnel=Group("03 / Winding passage / crate cover");Cover(tunnel,"T01",16.5f,26.3f,false);Cover(tunnel,"T02",14.5f,40,true);
        var wing=Group("04 / Security wing and rooms / crate cover");
        Cover(wing,"B01",34,42,true);Cover(wing,"B02",49,42,false);Cover(wing,"B03",33.5f,51,false);Cover(wing,"B04",49.5f,51,true);
        Cover(wing,"B05",36,57,true);Cover(wing,"B06",48.5f,58,false);Cover(wing,"B07",30.3f,49.5f,false);Cover(wing,"B08",54,55,true,90);
        Crate(wing,new Vector3(16,0,56.7f),1.45f,5,"Objects_Crate");Crate(wing,new Vector3(20.5f,0,51.5f),1.2f,12);
        Crate(wing,new Vector3(69.3f,0,43.5f),1.45f,-15);Crate(wing,new Vector3(68.7f,0,48.5f),1.2f,10,"Objects_Crate 3");
        var reactor=Group("05 / Final chamber / crate cover");
        Cover(reactor,"C01",34.5f,79,false);Cover(reactor,"C02",49.5f,79,false);Cover(reactor,"C03",33.5f,88,true);Cover(reactor,"C04",50.5f,88,true);
        Cover(reactor,"C05",36.5f,93,false);Cover(reactor,"C06",47.5f,93,false);
        var crystalGroup=Group("06 / Cyan crystal pedestals");
        Crystal(crystalGroup,-3.4f,-4,false);Crystal(crystalGroup,3.4f,-4,false);
        Crystal(crystalGroup,-10.3f,21,false);Crystal(crystalGroup,10.3f,10,false);
        Crystal(crystalGroup,31,58.5f,false);Crystal(crystalGroup,53,42.5f,false);
        Crystal(crystalGroup,32,83,true);Crystal(crystalGroup,52,83,true);Crystal(crystalGroup,38.5f,95,false);Crystal(crystalGroup,45.5f,95,false);
        var skullGroup=Group("07 / Small skull accents / off the route");
        foreach(var p in new[]{new Vector2(-16.3f,11),new Vector2(16,15),new Vector2(-11,23.3f),new Vector2(16,28.6f),new Vector2(16,41),new Vector2(30.5f,59),new Vector2(53.2f,58.7f),new Vector2(15.4f,51.3f),new Vector2(70,48.5f),new Vector2(31,85),new Vector2(53,86),new Vector2(44,96.6f)})Skull(skullGroup,p.x,p.y,(skulls*37)%360);
        var lights=Group("08 / Mr Scientist wall lamps");
        Lamp(lights,-4.45f,-3,270,true);Lamp(lights,4.45f,-3,90,false);
        Lamp(lights,-12.45f,12,270,true);Lamp(lights,12.45f,25,90,false);
        Lamp(lights,23.35f,26,90,true);Lamp(lights,23.35f,33,90,false);Lamp(lights,7.65f,41,270,true);Lamp(lights,7.65f,46,270,false);
        Lamp(lights,28.65f,42,270,true);Lamp(lights,55.35f,58,90,false);
        Lamp(lights,14.65f,54,270,false);Lamp(lights,70.35f,46,90,false);
        Lamp(lights,38.65f,66,270,true);Lamp(lights,45.35f,67,90,false);
        Lamp(lights,28.8f,83,270,false);Lamp(lights,55.2f,87,90,false);
        Lamp(lights,36,97,0,false);Lamp(lights,48,97,0,false);
        AssetDatabase.SaveAssets();Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Undo.CollapseUndoOperations(undo);Selection.activeGameObject=newRoot;
        var report="Selected props pass: preserved "+columns.Length+" stone columns; "+crates+" Mr Scientist crates at 24 cover positions and storage edges; "+lamps+" wall lamps; "+crystals+" crystal pedestals; "+skulls+" skulls.\nRemoved all other previous decoration, including barricades, factory cabinets, orange strips, signs, arches, pipes, reactor equipment and old wall lamps.\nBackup: "+backup+"\n";
        File.WriteAllText(Path.Combine(project,"Docs/GV/LevelDesign/Reports/Decoration/SelectedPropsPass.txt"),report);
        return report;
    }
}
