using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class DecorateSkybreakMap
{
    static Transform root;
    static int props;
    static Material stone, energy;
    static List<string> records;
    const string Library="Assets/Prefabs/Environment/Decoration/";
    static Transform Group(string name){var g=new GameObject(name);Undo.RegisterCreatedObjectUndo(g,"Decorate Skybreak map");g.transform.SetParent(root);return g.transform;}
    static GameObject Place(Transform group,string pack,string name,float x,float z,float height=0,float yaw=0,float y=0,bool grey=false)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Library+pack+"/"+name+".prefab");
        if(prefab==null)throw new Exception("Missing decoration prefab: "+pack+"/"+name);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,group);
        Undo.RegisterCreatedObjectUndo(go,"Decorate Skybreak map");
        go.name=name+" / "+x+", "+z;
        go.transform.SetPositionAndRotation(new Vector3(x,y,z),Quaternion.Euler(0,yaw,0));
        var renderers=go.GetComponentsInChildren<Renderer>();
        var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
        if(height>0 && bounds.size.y>0.001f)go.transform.localScale=Vector3.one*(height/bounds.size.y);
        if(grey)foreach(var r in renderers)r.sharedMaterials=r.sharedMaterials.Select(m=>stone).ToArray();
        props++;records.Add(pack+"/"+name+" at "+go.transform.position.ToString("F2"));
        return go;
    }
    static void Torch(Transform group,float x,float z,float yaw,bool lit=false,bool cyan=false)
    {
        var go=Place(group,"KayKit_Dungeon","torch_lit",x,z,1.25f,yaw,2.7f);
        if(lit){var light=new GameObject(cyan?"Cyan power accent":"Warm torch accent");light.transform.SetParent(go.transform,false);light.transform.position=new Vector3(x,3.7f,z);var l=light.AddComponent<Light>();l.type=LightType.Point;l.color=cyan?new Color(.20f,.82f,1):new Color(1,.48f,.16f);l.intensity=cyan?1.8f:1.3f;l.range=6;l.shadows=LightShadows.None;}
    }
    static Material Mat(string path,Color color,bool glow=false)
    {
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat!=null)return mat;
        mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",.35f);
        if(glow){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*2);}
        AssetDatabase.CreateAsset(mat,path);return mat;
    }
    public static string Main()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Open the main map outside Play Mode.");
        if(scene.GetRootGameObjects().Any(g=>g.name=="09 / Dungeon decoration"))throw new Exception("Decoration group already exists; edit it instead of duplicating the pass.");
        var project=Path.GetDirectoryName(Application.dataPath);
        var backupDir=Path.Combine(project,"Tools/SceneSnapshots");Directory.CreateDirectory(backupDir);
        var backup=Path.Combine(backupDir,"SkybreakMain_before_decoration_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity");
        if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Could not create scene recovery copy.");
        Directory.CreateDirectory(Path.Combine(project,"Assets/Materials/Environment/Decoration/MapAccents"));AssetDatabase.Refresh();
        stone=Mat("Assets/Materials/Environment/Decoration/MapAccents/VaultStone.mat",new Color(.34f,.41f,.43f));
        energy=Mat("Assets/Materials/Environment/Decoration/MapAccents/CyanEnergy.mat",new Color(.10f,.78f,.94f),true);
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Decorate Skybreak dungeon");
        var main=new GameObject("09 / Dungeon decoration");Undo.RegisterCreatedObjectUndo(main,"Decorate Skybreak map");root=main.transform;
        records=new List<string>{"Recovery copy: "+backup};props=0;
        var entrance=Group("01 / Entrance supply checkpoint");
        foreach(float x in new[]{-3.8f,3.8f}){Place(entrance,"Quaternius_ModularDungeons","Column2",x,-6,4.7f,0,0,true);Torch(entrance,x,-5.1f,0,true);}
        Place(entrance,"KayKit_Dungeon","crates_stacked",-3.4f,1.5f,2.4f,12);
        Place(entrance,"KayKit_Dungeon","barrel_large",3.5f,2,1.7f);
        Place(entrance,"Kenney_FactoryKit","screen-panel-wide",3.8f,4.8f,1.1f,180,1.5f);

        var court=Group("02 / First court cover and masonry");
        foreach(var p in new[]{new Vector2(-11.6f,7),new Vector2(11.6f,7),new Vector2(-11.6f,25),new Vector2(11.6f,25)})Place(court,"Quaternius_ModularDungeons","Column2",p.x,p.y,5.6f,0,0,true);
        Place(court,"KayKit_Dungeon","crates_stacked",-7,13,2.5f,18);
        Place(court,"KayKit_Dungeon","crates_stacked",7,16,2.4f,-16);
        Place(court,"Kenney_SpaceStationKit","structure-barrier",-5,23,1.65f,90);
        Place(court,"Kenney_SpaceStationKit","container-wide",9,24.5f,1.8f,90);
        Place(court,"KayKit_Dungeon","barrel_large",-15.5f,11,1.9f);
        Place(court,"KayKit_Dungeon","barrel_small_stack",-16,14,1.7f);
        Place(court,"Kenney_FactoryKit","machine-fortified",15.6f,13,2.8f,270);
        foreach(float x in new[]{-12.6f,12.6f}){
            float yaw=x<0?90:270;
            Place(court,"KayKit_Dungeon","banner_thin_blue",x,17,2.6f,yaw,2.8f);
            Torch(court,x,10,yaw,true);Torch(court,x,23,yaw);
        }
        Place(court,"KayKit_Dungeon","rubble_half",-10,25.5f,.7f,60);
        Place(court,"Quaternius_ModularDungeons","Cobweb",-12.6f,26.5f,1.3f,90,4.5f);

        var passage=Group("03 / Maintenance passage lamps and wear");
        Torch(passage,23.6f,26,270,true);Torch(passage,23.6f,33,270);
        Torch(passage,19.4f,32,90);Torch(passage,14,37.6f,180,true);
        Torch(passage,7.4f,40,90);Torch(passage,7.4f,45,90,true);
        Torch(passage,16,44.4f,0);Torch(passage,25,48.6f,180);
        Place(passage,"KayKit_Dungeon","barrel_large",16.5f,25,1.65f);
        Place(passage,"Quaternius_ModularDungeons","Cobweb2",15.6f,29.2f,1.2f,90,4.1f);
        Place(passage,"KayKit_Dungeon","rubble_half",16.4f,40.5f,.55f,120);
        Place(passage,"Quaternius_Ruins_Selected","Bricks",20,24,.3f);
        Place(passage,"Kenney_FactoryKit","pipe-large",23.5f,29,1.5f,0,4.6f);

        var security=Group("04 / Security wing cover and rooms");
        foreach(var p in new[]{new Vector2(29.5f,39.5f),new Vector2(54.5f,39.5f),new Vector2(29.5f,60.5f),new Vector2(54.5f,60.5f)})Place(security,"Quaternius_ModularDungeons","Column2",p.x,p.y,5.6f,0,0,true);
        Place(security,"Kenney_SpaceStationKit","container-wide",34.5f,51,2,90);
        Place(security,"KayKit_Dungeon","crates_stacked",49,43,2.4f,12);
        Place(security,"Kenney_SpaceStationKit","structure-barrier",47,55,1.65f,90);
        Place(security,"KayKit_Dungeon","barrel_large",31,58,1.8f);
        Place(security,"KayKit_Dungeon","barrel_small_stack",52,59,1.8f);
        foreach(float x in new[]{28.4f,55.6f}){Torch(security,x,41,x<40?90:270,true);Torch(security,x,58,x<40?90:270);Place(security,"KayKit_Dungeon","banner_thin_blue",x,50,2.6f,x<40?90:270,2.8f);}
        Place(security,"KayKit_Dungeon","table_medium",16.1f,56.5f,1.35f);
        Place(security,"Kenney_SpaceStationKit","computer-system",16.1f,56.5f,.6f,180,1.35f);
        Place(security,"KayKit_Dungeon","crates_stacked",16.1f,51.5f,2.1f);
        Torch(security,14.4f,54,90,true);
        Place(security,"Kenney_SpaceStationKit","computer-wide",68.5f,48.6f,1.4f,180);
        Place(security,"Kenney_SpaceStationKit","container-tall",69.5f,43.5f,2.4f);
        Torch(security,70.6f,46,270,true);
        // Frames flank the existing 3m openings, rather than filling them.
        foreach(float z in new[]{51.8f,56.2f})Place(security,"Quaternius_ModularDungeons","Column",22.3f,z,3.8f,0,0,true);
        foreach(float z in new[]{43.8f,48.2f})Place(security,"Quaternius_ModularDungeons","Column",62.7f,z,3.8f,0,0,true);

        var reactor=Group("05 / Reactor chamber stone monuments");
        foreach(float x in new[]{30.2f,53.8f})foreach(float z in new[]{76.5f,85})Place(reactor,"Quaternius_ModularDungeons","Column2",x,z,5.7f,0,0,true);
        foreach(float x in new[]{31.4f,52.6f}){
            Place(reactor,"Quaternius_ModularDungeons","Pedestal",x,94.5f,1.2f,0,0,true);
            Place(reactor,"Quaternius_ModularDungeons","Statue_Horse",x,94.5f,4.3f,x<42?90:270,1.2f,true);
            Place(reactor,"KayKit_Dungeon","banner_thin_blue",x,98.6f,3,180,2.5f);
        }
        Place(reactor,"Kenney_FactoryKit","machine-fortified",42,96.5f,3.2f,180);
        Place(reactor,"Kenney_FactoryKit","screen-panel-wide",42,94.5f,1.4f,180,1.5f);
        foreach(float x in new[]{28.4f,55.6f}){Torch(reactor,x,78,x<42?90:270,true,true);Torch(reactor,x,90,x<42?90:270);}
        foreach(float x in new[]{39,45})Place(reactor,"Quaternius_ModularDungeons","Column",x,68,5.4f,0,0,true);
        // Readable energy beacons at the final access corridor.
        foreach(float x in new[]{39,45}){
            var orb=GameObject.CreatePrimitive(PrimitiveType.Sphere);Undo.RegisterCreatedObjectUndo(orb,"Decorate Skybreak map");orb.name="Cyan access beacon";orb.transform.SetParent(reactor);orb.transform.position=new Vector3(x,5.6f,68);orb.transform.localScale=Vector3.one*.45f;orb.GetComponent<Renderer>().sharedMaterial=energy;UnityEngine.Object.DestroyImmediate(orb.GetComponent<Collider>());
        }
        AssetDatabase.SaveAssets();Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Undo.CollapseUndoOperations(undo);
        var reportDir=Path.Combine(project,"Docs/GV/LevelDesign/Reports/Decoration");Directory.CreateDirectory(reportDir);
        records.Insert(0,"Decoration prefab instances: "+props);File.WriteAllLines(Path.Combine(reportDir,"MapDecorationPlacement.txt"),records);
        Selection.activeGameObject=main;
        return "Placed "+props+" prefab instances, plus two energy beacons. Saved main scene; recovery copy: "+backup;
    }
}
