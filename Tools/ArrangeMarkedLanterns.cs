using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

// Scene authoring tool. Coordinates follow the four annotated top views.
public static class ArrangeMarkedLanterns
{
    struct Spot { public string region; public Vector3 p,n; public Spot(string r,float x,float z,Vector3 normal){region=r; if(Mathf.Abs(normal.x)>.5f)z=Mathf.Floor(z)+.5f;else x=Mathf.Floor(x)+.5f;p=new Vector3(x,3.7f,z);n=normal;} }
    static Spot[] Spots()=>new[]{
        new Spot("Court",-7,26.5f,Vector3.back),new Spot("Court",4.5f,26.5f,Vector3.back),
        new Spot("Court",-12.5f,20,Vector3.right),new Spot("Court",17,22.5f,Vector3.back),
        new Spot("Court",-9,5.5f,Vector3.forward),new Spot("Court",9,5.5f,Vector3.forward),
        new Spot("Entrance",-4.5f,-3,Vector3.right),new Spot("Entrance",4.5f,-3,Vector3.left),
        new Spot("Passage",7.5f,47,Vector3.right),new Spot("Passage",16,48.5f,Vector3.back),
        new Spot("Passage",21,44.5f,Vector3.forward),new Spot("Passage",14,41.5f,Vector3.back),
        new Spot("Passage",7.5f,40,Vector3.right),new Spot("Passage",12,33.5f,Vector3.forward),
        new Spot("Passage",23.5f,32,Vector3.left),new Spot("Passage",15.5f,27,Vector3.right),
        new Spot("Passage",23.5f,26,Vector3.left),new Spot("Passage",21,18.5f,Vector3.forward),
        new Spot("Security",34,61.5f,Vector3.back),new Spot("Security",49,61.5f,Vector3.back),
        new Spot("Security",55.5f,55,Vector3.left),new Spot("Security",28.5f,42,Vector3.right),
        new Spot("Security",36,38.5f,Vector3.forward),new Spot("Security",49,38.5f,Vector3.forward),
        new Spot("West holding room",14.5f,54,Vector3.right),new Spot("East holding room",70.5f,46,Vector3.left),
        new Spot("Boss",36,97.5f,Vector3.back),new Spot("Boss",48,97.5f,Vector3.back),
        new Spot("Boss",28,85,Vector3.right),new Spot("Boss",56,85,Vector3.left),
        new Spot("Boss",32,73,Vector3.forward),new Spot("Boss",51,73,Vector3.forward),
        new Spot("Boss entrance",38.5f,67,Vector3.right),new Spot("Boss entrance",45.5f,67,Vector3.left)
    };
    static RaycastHit[] Anchors(){
        var walls=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().First(g=>g.name.StartsWith("07 /"));
        Physics.SyncTransforms();return Spots().Select(s=>{
            string direction="/ ("+(-s.n.x).ToString("0")+", "+(-s.n.z).ToString("0")+")";
            var hits=Physics.RaycastAll(s.p+s.n*.75f,-s.n,4,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.transform.IsChildOf(walls.transform)&&h.collider.name.EndsWith(direction)&&Vector3.Dot(h.normal,s.n)>.5f).OrderBy(h=>h.distance).ToArray();
            if(hits.Length==0)throw new Exception("No wall for "+s.region+" "+s.p);return hits[0];
        }).ToArray();
    }
    public static string Preview()=>string.Join("\n",Anchors().Select((h,i)=>Spots()[i].region+" "+h.point+" / "+h.collider.name));
    static Material GlowMaterial(string path,Material original,Color emission){
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(original);AssetDatabase.CreateAsset(m,path);}
        m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",emission);m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;EditorUtility.SetDirty(m);return m;
    }
    public static string Main(){
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||Lightmapping.isRunning||scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Use the main scene in Edit Mode, with no running bake.");
        var anchors=Anchors();var spots=Spots();var project=Path.GetDirectoryName(Application.dataPath);
        var backup=Path.Combine(project,"Tools/SceneSnapshots/SkybreakMain_before_marked_lanterns_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity");
        Directory.CreateDirectory(Path.GetDirectoryName(backup));if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup failed.");
        var root=scene.GetRootGameObjects().First(g=>g.name.StartsWith("09 /"));var group=root.transform.Cast<Transform>().Single(t=>t.name.StartsWith("08 /"));
        var lamps=group.Cast<Transform>().Where(t=>t.name.StartsWith("Vault wall lantern ")).OrderBy(t=>t.name).ToList();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Decoration/Custom/AuroraArchitecture/VaultWallLantern.prefab");
        var crystals=root.GetComponentsInChildren<Transform>().Where(t=>t.name=="CrystalPedestal").ToArray();
        var lampMat=GlowMaterial("Assets/Materials/Environment/Decoration/Custom/AuroraArchitecture/MarkedLanternGlow.mat",lamps[0].GetComponentsInChildren<Renderer>().Single(r=>r.name=="Lantern_Core").sharedMaterial,new Color(1,.59f,.15f)*3.0f);
        var crystalMat=GlowMaterial("Assets/Materials/Environment/Dungeon/CrystalSoftGlow.mat",crystals[0].GetComponentsInChildren<Renderer>().Single(r=>r.name=="Faceted crystal").sharedMaterial,new Color(.18f,.8f,1)*1.8f);
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Marked lantern placement and crystal glow");
        var report=new List<string>{"34 lantern locations from the four supplied annotated views; each cluster of yellow strokes represents one lamp.","Backup: "+backup,"Lantern intensity 2.5 (previously 1.8), range 5.5m; amber emission 3.0 (previously 2.4).","All eight pedestal crystals: dedicated cyan emissive material and baked 1.3-intensity / 4m spill light. Plinth and brass remain unchanged."};
        for(int i=0;i<spots.Length;i++){
            GameObject go;if(i<lamps.Count)go=lamps[i].gameObject;else{go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,group);Undo.RegisterCreatedObjectUndo(go,"Additional marked lantern");}
            Undo.RecordObject(go.transform,"Marked wall position");Undo.RecordObject(go,"Lamp region name");go.name="Vault wall lantern "+(i+1).ToString("00")+" / "+spots[i].region;
            go.transform.SetPositionAndRotation(anchors[i].point+spots[i].n*.06f,Quaternion.LookRotation(spots[i].n,Vector3.up));
            foreach(var r in go.GetComponentsInChildren<MeshRenderer>()){
                GameObjectUtility.SetStaticEditorFlags(r.gameObject,GameObjectUtility.GetStaticEditorFlags(r.gameObject)|StaticEditorFlags.ContributeGI|StaticEditorFlags.BatchingStatic);r.receiveGI=ReceiveGI.Lightmaps;
                if(r.name=="Lantern_Core"){Undo.RecordObject(r,"Emissive lamp core");r.sharedMaterial=lampMat;PrefabUtility.RecordPrefabInstancePropertyModifications(r);}
                var mesh=r.GetComponent<MeshFilter>().sharedMesh;if(mesh.uv2.Length!=mesh.vertexCount)throw new Exception("UV2 missing: "+r.name);
            }
            var core=go.GetComponentsInChildren<Renderer>().Single(r=>r.name=="Lantern_Core");var l=go.GetComponentInChildren<Light>();
            if(l==null){var lightGo=new GameObject("Warm local glow / baked");lightGo.transform.SetParent(go.transform,false);Undo.RegisterCreatedObjectUndo(lightGo,"Lamp light");l=Undo.AddComponent<Light>(lightGo);}
            Undo.RecordObject(l,"Brighten lantern");Undo.RecordObject(l.transform,"Lamp spill position");l.transform.position=core.bounds.center+spots[i].n*.12f;
            l.enabled=true;l.type=LightType.Point;l.lightmapBakeType=LightmapBakeType.Baked;l.color=new Color(1,.58f,.24f);l.range=5.5f;l.intensity=2.5f;l.shadows=LightShadows.None;
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(l);PrefabUtility.RecordPrefabInstancePropertyModifications(l.transform);
            report.Add(go.name+" | wall="+anchors[i].collider.name+" | origin="+go.transform.position+" | inward="+spots[i].n);
        }
        for(int i=spots.Length;i<lamps.Count;i++)Undo.DestroyObjectImmediate(lamps[i].gameObject);
        foreach(var c in crystals){
            var r=c.GetComponentsInChildren<Renderer>().Single(t=>t.name=="Faceted crystal");Undo.RecordObject(r,"Soft crystal emission");r.sharedMaterial=crystalMat;PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            var l=c.GetComponentInChildren<Light>();if(l==null){var lightGo=new GameObject("Crystal soft glow / baked");lightGo.transform.SetParent(c,false);Undo.RegisterCreatedObjectUndo(lightGo,"Crystal spill");l=Undo.AddComponent<Light>(lightGo);}
            Undo.RecordObject(l,"Baked crystal glow");Undo.RecordObject(l.transform,"Crystal light position");l.transform.position=r.bounds.center+Vector3.up*.12f;l.type=LightType.Point;l.lightmapBakeType=LightmapBakeType.Baked;l.color=new Color(.18f,.8f,1);l.intensity=1.3f;l.range=4;l.shadows=LightShadows.None;l.enabled=true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(l);PrefabUtility.RecordPrefabInstancePropertyModifications(l.transform);
            report.Add("CrystalPedestal "+c.position+" | crystal only emissive, baked spill centre="+l.transform.position);
        }
        Undo.CollapseUndoOperations(undo);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed");
        var dir=Path.Combine(project,"Docs/GV/LevelDesign/Reports/LanternPlacement");Directory.CreateDirectory(dir);File.WriteAllLines(Path.Combine(dir,"Placement.txt"),report);
        return "Saved 34 wall-mounted lanterns and 8 glowing crystal pedestals. Existing walls, cover and NavMesh unchanged. Rebuild lighting next.";
    }
}
