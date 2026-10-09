using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class UpgradeVaultLayout
{
    static Transform root;
    static Material stone, metal, orange, cyan, cream, dark;
    static List<string> coverLog;
    static string project;
    const string Library="Assets/Prefabs/Environment/Decoration/";
    static Transform Group(string name){var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Upgrade Aurora Vault");go.transform.SetParent(root,false);return go.transform;}
    static Material Mat(string name,Color color,bool glow=false){
        var path="Assets/Materials/Environment/Decoration/MapAccents/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.32f);m.enableInstancing=true;
        if(glow){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*2);}
        EditorUtility.SetDirty(m);return m;
    }
    static GameObject Place(Transform parent,string pack,string name,Vector3 position,Vector3 size,float yaw=0,Material material=null){
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Library+pack+"/"+name+".prefab");
        if(prefab==null)throw new Exception("Missing prefab "+pack+"/"+name);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);Undo.RegisterCreatedObjectUndo(go,"Upgrade Aurora Vault");
        go.name=name;go.transform.position=Vector3.zero;go.transform.rotation=Quaternion.identity;
        var renders=go.GetComponentsInChildren<Renderer>();var bounds=renders[0].bounds;foreach(var r in renders.Skip(1))bounds.Encapsulate(r.bounds);
        go.transform.localScale=new Vector3(size.x/Mathf.Max(bounds.size.x,.01f),size.y/Mathf.Max(bounds.size.y,.01f),size.z/Mathf.Max(bounds.size.z,.01f));
        go.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
        if(material!=null)foreach(var r in renders)r.sharedMaterials=r.sharedMaterials.Select(m=>material).ToArray();
        return go;
    }
    static GameObject Block(Transform parent,string name,Vector3 position,Vector3 size,Material mat,float yaw=0,bool collider=false){
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);Undo.RegisterCreatedObjectUndo(go,"Upgrade Aurora Vault");go.name=name;go.transform.SetParent(parent);go.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;
        if(!collider)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
    }
    static void Sign(Transform parent,string text,Vector3 position,float width=3.5f,float yaw=0){
        position-=Quaternion.Euler(0,yaw,0)*Vector3.forward*.6f;
        var sign=new GameObject(text);sign.transform.SetParent(parent);sign.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
        var plate=Block(sign.transform,"Enamel sign plate",position,new Vector3(width,1.25f,.10f),dark,yaw);
        Block(sign.transform,"Orange sign rail",position+Vector3.up*.66f,new Vector3(width,.07f,.12f),orange,yaw);
        var label=new GameObject("Facility lettering");label.transform.SetParent(sign.transform,false);label.transform.localPosition=new Vector3(0,0,-.065f);
        var mesh=label.AddComponent<TextMesh>();mesh.text=text;mesh.anchor=TextAnchor.MiddleCenter;mesh.alignment=TextAlignment.Center;mesh.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");mesh.fontSize=80;mesh.characterSize=.038f;mesh.color=new Color(.85f,.91f,.88f);
        label.GetComponent<Renderer>().sharedMaterial=mesh.font.material;
    }
    static void Cover(Transform area,string id,string type,float x,float z,float yaw=0){
        var station=new GameObject("COVER "+id+" / "+type);station.transform.SetParent(area);station.transform.position=new Vector3(x,0,z);
        bool low=type=="Low stone barricade";
        if(low) {
            Place(station.transform,"KayKit_Dungeon","barrier_column",new Vector3(x,0,z),new Vector3(id.StartsWith("T")?2.5f:3.8f,1.6f,.9f),yaw,stone);
            Block(station.transform,"Solid stone cover infill",new Vector3(x,.76f,z),new Vector3(id.StartsWith("T")?2.2f:3.5f,1.45f,.76f),AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Environment/StoneKit/Wall_Textured_Floor2_Body.mat"),yaw,true);
        } else if(type=="Supply crate") {
            Place(station.transform,"KayKit_Dungeon","box_large",new Vector3(x,0,z),new Vector3(id.StartsWith("S")?1.8f:2.1f,2.85f,1.9f),yaw);
        } else {
            Place(station.transform,"Kenney_FactoryKit","machine-fortified",new Vector3(x,0,z),new Vector3(2.2f,2.85f,2.2f),yaw,metal);
            Block(station.transform,"Power cabinet warning cap",new Vector3(x,2.85f,z),new Vector3(2.3f,.1f,2.3f),orange,yaw);
            Block(station.transform,"Cabinet cyan status strip",new Vector3(x,2.3f,z-1.12f),new Vector3(1.4f,.12f,.08f),cyan);
        }
        // Orange ground strips identify installed equipment pads; they are not UI markers.
        float front=low?.65f:1.25f;
        var rotation=Quaternion.Euler(0,yaw,0);
        Block(station.transform,"Equipment safety edge",new Vector3(x,.02f,z)+rotation*new Vector3(0,0,-front),new Vector3(low?3.9f:2.3f,.035f,.12f),orange,yaw);
        coverLog.Add("| "+id+" | "+type+" | "+x+", "+z+" | "+(low?"1.6m; standing shooting lip / future crouch cover":"2.85m; full standing cover and side peeking")+" |");
    }
    static void Lamp(Transform area,Vector3 position,float yaw,bool light){
        yaw+=180;
        Block(area,"Emergency luminaire housing",position,new Vector3(.58f,.95f,.28f),metal,yaw);
        var rotation=Quaternion.Euler(0,yaw,0);
        Block(area,"Emergency luminaire lens",position+rotation*new Vector3(0,0,-.16f),new Vector3(.35f,.7f,.06f),cream,yaw);
        if(light){var go=new GameObject("Local emergency illumination");go.transform.SetParent(area);go.transform.position=position+rotation*new Vector3(0,0,-.4f);var l=go.AddComponent<Light>();l.type=LightType.Point;l.color=new Color(1,.72f,.42f);l.range=6;l.intensity=1.7f;l.shadows=LightShadows.None;}
    }
    public static string Main(){
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Open main map outside Play Mode.");
        project=Path.GetDirectoryName(Application.dataPath);var backup=Path.Combine(project,"Tools/SceneSnapshots/SkybreakMain_before_cover_upgrade_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity");Directory.CreateDirectory(Path.GetDirectoryName(backup));if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup failed.");
        stone=Mat("VaultConcrete",new Color(.39f,.46f,.46f));metal=Mat("VaultTealMetal",new Color(.10f,.24f,.27f));orange=Mat("VaultWarningOrange",new Color(.95f,.36f,.08f));cyan=Mat("VaultStatusCyan",new Color(.06f,.72f,.87f),true);cream=Mat("VaultWarmLamp",new Color(.96f,.73f,.35f),true);dark=Mat("VaultSignBacking",new Color(.025f,.06f,.075f));
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Upgrade Aurora Vault cover and story setting");
        var old=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="09 / Dungeon decoration");if(old!=null)Undo.DestroyObjectImmediate(old);
        var go=new GameObject("09 / Dungeon decoration");Undo.RegisterCreatedObjectUndo(go,"Upgrade Aurora Vault");root=go.transform;
        coverLog=new List<string>{"# Aurora Vault cover layout", "", "Main map upgraded to match the hospital emergency-power mission. No enemy types are assigned.", "", "| Cover ID | Equipment | Unity X,Z | Use |", "| --- | --- | --- | --- |"};
        var entry=Group("01 / Hospital emergency supply checkpoint");
        Cover(entry,"S01","Supply crate",-3.5f,1.8f);Cover(entry,"S02","Low stone barricade",3.5f,2,90);
        Sign(entry,"AURORA VAULT\nEMERGENCY POWER",new Vector3(0,4.6f,5),4.1f);
        Lamp(entry,new Vector3(-4.7f,3.8f,-3),90,true);Lamp(entry,new Vector3(4.7f,3.8f,-3),270,false);

        var court=Group("02 / Loading court / six cover positions");
        Cover(court,"A01","Low stone barricade",-6.5f,11);Cover(court,"A02","Supply crate",6.5f,12);
        Cover(court,"A03","Power cabinet",-8,18);Cover(court,"A04","Low stone barricade",7.5f,18.5f);
        Cover(court,"A05","Low stone barricade",-6,24);Cover(court,"A06","Supply crate",8.5f,25);
        foreach(var p in new[]{new Vector2(-11.6f,7),new Vector2(11.6f,7),new Vector2(-11.6f,25),new Vector2(11.6f,25)})Place(court,"Quaternius_ModularDungeons","Column2",new Vector3(p.x,0,p.y),new Vector3(1.5f,5.6f,1.5f),0,stone);
        Place(court,"KayKit_Dungeon","box_stacked",new Vector3(-15.5f,0,13),new Vector3(3.3f,2.8f,3.3f));
        Place(court,"Kenney_FactoryKit","machine-fortified",new Vector3(15.5f,0,13),new Vector3(3,3.4f,3),270,metal);
        Sign(court,"HOSPITAL RESERVE\nSUPPLY STORAGE",new Vector3(-15.5f,4.6f,15.7f),3.8f);
        Sign(court,"MAINTENANCE\nREACTOR ACCESS",new Vector3(11.5f,4.6f,23.5f),3.4f,270);
        Lamp(court,new Vector3(-12.6f,3.8f,11),90,true);Lamp(court,new Vector3(12.6f,3.8f,25),270,false);

        var tunnel=Group("03 / Maintenance / machinery alcoves");
        Cover(tunnel,"T01","Supply crate",16.5f,26.3f);Cover(tunnel,"T02","Low stone barricade",14.5f,40);
        foreach(var p in new[]{new Vector3(23.6f,3.8f,26),new Vector3(23.6f,3.8f,33),new Vector3(7.4f,3.8f,41),new Vector3(7.4f,3.8f,46)})Lamp(tunnel,p,p.x>20?270:90,p.z==26||p.z==41);
        foreach(float z in new[]{25f,28f,31f,34f})Place(tunnel,"Kenney_FactoryKit","pipe-large",new Vector3(23.4f,4.7f,z),new Vector3(.55f,.55f,2.9f),0,metal);
        Place(tunnel,"Quaternius_Ruins_Selected","Bricks",new Vector3(16.7f,0,29),new Vector3(1.2f,.25f,1.1f),0,stone);
        Place(tunnel,"KayKit_Dungeon","rubble_half",new Vector3(16.4f,0,41.1f),new Vector3(1.2f,.55f,.9f),0,stone);
        Sign(tunnel,"INNER SECURITY",new Vector3(25,4.3f,48.6f),3.2f);

        var security=Group("04 / Security wing / eight cover positions");
        Cover(security,"B01","Low stone barricade",34,42);Cover(security,"B02","Power cabinet",49,42);
        Cover(security,"B03","Supply crate",33.5f,51);Cover(security,"B04","Low stone barricade",49.5f,51);
        Cover(security,"B05","Low stone barricade",36,57);Cover(security,"B06","Supply crate",48.5f,58);
        Cover(security,"B07","Power cabinet",30.3f,49.5f);Cover(security,"B08","Low stone barricade",54,55,90);
        foreach(float x in new[]{29.5f,54.5f})foreach(float z in new[]{39.5f,60.5f})Place(security,"Quaternius_ModularDungeons","Column2",new Vector3(x,0,z),new Vector3(1.5f,5.6f,1.5f),0,stone);
        Sign(security,"SECURITY WING\nREACTOR ACCESS",new Vector3(42,4.8f,61.6f),4.4f);
        Lamp(security,new Vector3(28.4f,3.8f,42),90,true);Lamp(security,new Vector3(55.6f,3.8f,58),270,false);
        var rooms=Group("05 / Service bays / reserved agent entrances");
        Place(rooms,"Kenney_SpaceStationKit","computer-wide",new Vector3(16,0,56.6f),new Vector3(2.3f,1.5f,1),0,metal);
        Place(rooms,"KayKit_Dungeon","shelf_large",new Vector3(15.8f,0,51.5f),new Vector3(2.4f,3.2f,1.1f),90,metal);
        Place(rooms,"Kenney_SpaceStationKit","container-tall",new Vector3(69.3f,0,43.5f),new Vector3(1.8f,2.8f,1.8f),0,metal);
        Place(rooms,"Kenney_SpaceStationKit","computer-system",new Vector3(68.7f,0,48.5f),new Vector3(2,1.6f,.9f),0,metal);
        Sign(rooms,"SERVICE BAY 01",new Vector3(22.3f,3.8f,54),2.6f,270);
        Sign(rooms,"SERVICE BAY 02",new Vector3(62.7f,3.8f,46),2.6f,90);
        Lamp(rooms,new Vector3(14.4f,3.8f,54),90,false);Lamp(rooms,new Vector3(70.6f,3.8f,46),270,false);

        var reactor=Group("06 / Reactor / six cover positions and power controls");
        Cover(reactor,"C01","Power cabinet",34.5f,79);Cover(reactor,"C02","Power cabinet",49.5f,79);
        Cover(reactor,"C03","Low stone barricade",33.5f,88);Cover(reactor,"C04","Low stone barricade",50.5f,88);
        Cover(reactor,"C05","Power cabinet",36.5f,93);Cover(reactor,"C06","Power cabinet",47.5f,93);
        foreach(float x in new[]{30.2f,53.8f})foreach(float z in new[]{76.5f,85})Place(reactor,"Quaternius_ModularDungeons","Column2",new Vector3(x,0,z),new Vector3(1.5f,5.7f,1.5f),0,stone);
        Place(reactor,"Kenney_FactoryKit","machine-fortified",new Vector3(42,0,95.5f),new Vector3(4.2f,4.8f,3.2f),180,metal);
        Block(reactor,"Reactor housing energy face",new Vector3(42,2.7f,93.85f),new Vector3(1.5f,2,.1f),cyan);
        Place(reactor,"Kenney_SpaceStationKit","computer-wide",new Vector3(42,0,92.7f),new Vector3(2.2f,1.6f,.9f),0,metal);
        Sign(reactor,"EMERGENCY REACTOR\nHOSPITAL SUPPLY",new Vector3(42,5.8f,98.1f),5);
        Sign(reactor,"RESTORE POWER",new Vector3(42,2.3f,93.05f),2.5f);
        Lamp(reactor,new Vector3(28.4f,3.8f,83),90,true);Lamp(reactor,new Vector3(55.6f,3.8f,90),270,false);
        var arches=Group("07 / Stone access frames");
        foreach(var position in new[]{new Vector3(0,0,5),new Vector3(42,0,71)}){
            var arch=Place(arches,"Quaternius_Ruins_Selected","Arch_Round",position,new Vector3(5.3f,6,.85f),0,stone);
            foreach(var mf in arch.GetComponentsInChildren<MeshFilter>()){var collider=mf.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=mf.sharedMesh;}
        }
        coverLog.Add("");coverLog.Add("Cover colliders block character/raycast travel. Shooting/AI/crouch mechanics remain separate work. Low cover does not hide a standing 2.44m character.");coverLog.Add("Recovery copy: "+backup);
        var reports=Path.Combine(project,"Docs/GV/LevelDesign/Reports/Decoration");Directory.CreateDirectory(reports);File.WriteAllLines(Path.Combine(reports,"CoverLayoutV2.md"),coverLog);
        AssetDatabase.SaveAssets();Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Undo.CollapseUndoOperations(undo);Selection.activeGameObject=go;
        return "Upgraded map: 24 explicit solid cover positions; facility equipment and story signage; medieval clutter removed. Backup: "+backup;
    }
}
