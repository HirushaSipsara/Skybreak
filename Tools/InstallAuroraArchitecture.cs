using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class InstallAuroraArchitecture
{
    const string Folder="AuroraArchitecture/";
    const string Models="Assets/Models/Custom/"+Folder;
    const string Prefabs="Assets/Prefabs/Environment/Decoration/Custom/"+Folder;
    const string Mats="Assets/Materials/Environment/Decoration/Custom/"+Folder;
    static Dictionary<string,Material> materials=new Dictionary<string,Material>();
    static void Material(string name,Color color,float metal=0,float glow=0){
        var path=Mats+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",metal>0?.5f:.22f);
        if(glow>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*glow);}
        materials[name]=m;EditorUtility.SetDirty(m);
    }
    static GameObject MakePrefab(string name){
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(Models+name+".fbx");if(model==null)throw new Exception("Missing model "+name);
        // Preserve the FBX's Blender-to-Unity axis conversion beneath an identity placement root.
        var preview=EditorSceneManager.NewPreviewScene();var g=new GameObject(name);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(g,preview);
        var content=UnityEngine.Object.Instantiate(model);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(content,preview);content.transform.SetParent(g.transform,false);content.name="Model / preserve imported axis conversion";
        try{
            foreach(var r in g.GetComponentsInChildren<Renderer>()){
                r.sharedMaterials=r.sharedMaterials.Select(m=>materials.ContainsKey(m.name)?materials[m.name]:throw new Exception("Unmapped material "+m.name)).ToArray();
                if(name!="VaultWallLantern" && !r.name.Contains("Ring") && !r.name.Contains("Energy") && !r.name.Contains("Crystal") && !r.name.Contains("Cyan") && !r.name.Contains("Inset")){
                    var collider=r.gameObject.AddComponent<BoxCollider>();var mesh=r.GetComponent<MeshFilter>().sharedMesh;
                    collider.center=mesh.bounds.center;collider.size=mesh.bounds.size;
                }
            }
            return PrefabUtility.SaveAsPrefabAsset(g,Prefabs+name+".prefab");
        }finally{UnityEngine.Object.DestroyImmediate(g);EditorSceneManager.ClosePreviewScene(preview);}
    }
    public static string Main(){
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Open main scene outside Play Mode.");
        var project=Path.GetDirectoryName(Application.dataPath);Directory.CreateDirectory(Path.Combine(project,Mats));Directory.CreateDirectory(Path.Combine(project,Prefabs));AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Material("VaultStone",new Color(.25f,.35f,.40f));Material("PaleStone",new Color(.47f,.56f,.59f));Material("Brass",new Color(.75f,.51f,.25f),.65f);
        Material("DarkMetal",new Color(.11f,.15f,.17f),.7f);Material("CyanEnergy",new Color(.23f,.86f,.95f),0,1.2f);Material("VioletEnergy",new Color(.55f,.18f,.92f),0,1.4f);Material("AmberCore",new Color(1,.59f,.15f),0,2.4f);
        Material("PortalSwirl_BlenderPreview",new Color(.28f,.10f,.62f),0,1.5f);
        var portal=AssetDatabase.FindAssets("AuroraPortal t:Material").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();if(portal!=null)materials["PortalSwirl_BlenderPreview"]=AssetDatabase.LoadAssetAtPath<Material>(portal);
        var lantern=MakePrefab("VaultWallLantern");var shrine=MakePrefab("AuroraReactorShrine");MakePrefab("AuroraTeleportGate");
        var walls=scene.GetRootGameObjects().First(g=>g.name=="07 / Modular walls");var decoration=scene.GetRootGameObjects().First(g=>g.name=="09 / Dungeon decoration");
        var oldGroup=decoration.transform.Cast<Transform>().FirstOrDefault(t=>t.name.StartsWith("08 /"));if(oldGroup==null)throw new Exception("Missing lamp group");
        var specs=new[]{new Vector3(-4.45f,0,-3),new Vector3(4.45f,0,-3),new Vector3(-12.45f,0,19),new Vector3(12.45f,0,25),new Vector3(23.35f,0,26),new Vector3(23.35f,0,33),new Vector3(7.65f,0,41),new Vector3(7.65f,0,46),new Vector3(28.65f,0,42),new Vector3(55.35f,0,58),new Vector3(14.65f,0,54),new Vector3(70.35f,0,46),new Vector3(38.65f,0,66),new Vector3(45.35f,0,67),new Vector3(28.8f,0,83),new Vector3(55.2f,0,87),new Vector3(36,0,97),new Vector3(48,0,97)};
        var normals=new[]{Vector3.right,Vector3.left,Vector3.right,Vector3.left,Vector3.left,Vector3.left,Vector3.right,Vector3.right,Vector3.right,Vector3.left,Vector3.right,Vector3.left,Vector3.right,Vector3.left,Vector3.right,Vector3.left,Vector3.back,Vector3.back};
        Physics.SyncTransforms();var anchors=new List<RaycastHit>();
        for(int i=0;i<specs.Length;i++){
            var hits=Physics.RaycastAll(specs[i]+Vector3.up*3.7f,-normals[i],8,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.transform.IsChildOf(walls.transform)).OrderBy(h=>h.distance).ToArray();
            if(hits.Length==0)throw new Exception("No wall at lamp "+i+" "+specs[i]);anchors.Add(hits[0]);
        }
        var backup=Path.Combine(project,"Tools/SceneSnapshots/SkybreakMain_before_approved_architecture_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity");Directory.CreateDirectory(Path.GetDirectoryName(backup));if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup failed");
        Undo.IncrementCurrentGroup();Undo.SetCurrentGroupName("Approved Aurora architecture");var undo=Undo.GetCurrentGroup();Undo.DestroyObjectImmediate(oldGroup.gameObject);
        var group=new GameObject("08 / Enclosed wall lanterns");group.transform.SetParent(decoration.transform,false);Undo.RegisterCreatedObjectUndo(group,"Aurora architecture");
        var report=new List<string>{"Approved architecture; enlarged shrine and all lanterns lit; rear crystal pedestals removed.","Backup: "+backup};var lit=new HashSet<int>(Enumerable.Range(0,18));
        for(int i=0;i<anchors.Count;i++){
            var hit=anchors[i];var g=(GameObject)PrefabUtility.InstantiatePrefab(lantern,group.transform);g.name="Vault wall lantern "+(i+1).ToString("00");
            g.transform.SetPositionAndRotation(hit.point+hit.normal*.04f,Quaternion.LookRotation(hit.normal,Vector3.up));Undo.RegisterCreatedObjectUndo(g,"Aurora architecture");PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);
            if(lit.Contains(i)){
                var core=g.GetComponentsInChildren<Renderer>().First(r=>r.name=="Lantern_Core");var lightGo=new GameObject("Warm local glow / no realtime shadows");lightGo.transform.SetParent(g.transform);lightGo.transform.position=core.bounds.center+hit.normal*.1f;var light=lightGo.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.58f,.24f);light.range=5;light.intensity=1.8f;light.shadows=LightShadows.None;
            }
            report.Add(g.name+" wall="+hit.collider.name+" position="+g.transform.position+" facing="+hit.normal);
        }
        var previous=decoration.transform.Cast<Transform>().FirstOrDefault(t=>t.name=="09 / Aurora reactor shrine");if(previous!=null)Undo.DestroyObjectImmediate(previous.gameObject);
        var shrineGroup=new GameObject("09 / Aurora reactor shrine");shrineGroup.transform.SetParent(decoration.transform,false);Undo.RegisterCreatedObjectUndo(shrineGroup,"Aurora architecture");
        var s=(GameObject)PrefabUtility.InstantiatePrefab(shrine,shrineGroup.transform);s.transform.SetPositionAndRotation(new Vector3(42,0,95.4f),Quaternion.Euler(0,180,0));s.transform.localScale=Vector3.one*1.35f;Undo.RegisterCreatedObjectUndo(s,"Aurora architecture");PrefabUtility.RecordPrefabInstancePropertyModifications(s.transform);
        foreach(var pedestal in decoration.GetComponentsInChildren<Transform>().Where(t=>t.name=="CrystalPedestal" && (Vector3.Distance(t.position,new Vector3(38.5f,0,95))<.1f||Vector3.Distance(t.position,new Vector3(45.5f,0,95))<.1f)).ToArray())Undo.DestroyObjectImmediate(pedestal.gameObject);
        report.Add("Reactor shrine positioned at "+s.transform.position+"; gate prefab prepared but not placed in main map.");
        Undo.CollapseUndoOperations(undo);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Main scene save failed");
        var outDir=Path.Combine(project,"Docs/GV/LevelDesign/Reports/Decoration");Directory.CreateDirectory(outDir);File.WriteAllLines(Path.Combine(outDir,"AuroraArchitecturePlacement.txt"),report);
        Selection.activeGameObject=s;SceneView.lastActiveSceneView?.FrameSelected();return "Saved: 18 wall-mounted enclosed lanterns (18 warm lights), 1 reactor shrine at scale 1.35. Two rear crystal pedestals removed. Gate prefab saved separately. Backup="+backup;
    }
}
