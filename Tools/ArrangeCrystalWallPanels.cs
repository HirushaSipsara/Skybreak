using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class ArrangeCrystalWallPanels
{
    const string Prefab="Assets/Prefabs/Environment/Decoration/Custom/DungeonWallDetails/DiamondWallPanel.prefab";
    static string Project=>Path.GetDirectoryName(Application.dataPath);
    struct Spot {public string region;public Vector3 p,n;public bool gate;public Spot(string r,float x,float z,Vector3 normal,bool doorway=false,float height=4.2f){region=r;n=normal;gate=doorway;if(!doorway){if(Mathf.Abs(normal.x)>.5f)z=Mathf.Floor(z)+.5f;else x=Mathf.Floor(x)+.5f;}p=new Vector3(x,height,z);}}
    static Spot[] Spots()=>new[]{
        new Spot("Court west alcove",-17.5f,12,Vector3.right),new Spot("Court east alcove",17.5f,12,Vector3.left),new Spot("Entrance",0,-8.5f,Vector3.forward),
        new Spot("Passage",11,48.5f,Vector3.back),new Spot("Passage",22,48.5f,Vector3.back),new Spot("Passage",7.5f,44,Vector3.right),
        new Spot("Passage",23,44.5f,Vector3.forward),new Spot("Passage",7.5f,36,Vector3.right),new Spot("Passage",19,37.5f,Vector3.back),
        new Spot("Passage",19.5f,31,Vector3.right),new Spot("Passage",23.5f,29,Vector3.left),new Spot("Passage",18,24.5f,Vector3.forward),
        new Spot("Passage",16,22.5f,Vector3.back),new Spot("Passage",23.5f,21,Vector3.left),
        new Spot("West holding doorway",23.5f,54,Vector3.right,true,4.55f),new Spot("East holding doorway",61.5f,46,Vector3.left,true,4.55f),
        new Spot("Security east upper",55.5f,51,Vector3.left),new Spot("Security east lower",55.5f,40,Vector3.left),new Spot("Security south",44,38.5f,Vector3.forward),
        new Spot("Boss north",42,98.5f,Vector3.back,false,5.1f),new Spot("Boss northwest",33,94.5f,Vector3.back),new Spot("Boss northeast",50,94.5f,Vector3.back),
        new Spot("Boss west upper",29,90,Vector3.right),new Spot("Boss east upper",55,90,Vector3.left),new Spot("Boss west lower",28,80,Vector3.right),new Spot("Boss east lower",56,80,Vector3.left),
        new Spot("Boss southwest",31,74,Vector3.right),new Spot("Boss southeast",53,74,Vector3.left),
        new Spot("Boss entrance west",38.5f,65,Vector3.right),new Spot("Boss entrance east",45.5f,65,Vector3.left)
    };
    static RaycastHit[] Anchors(){var walls=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().First(g=>g.name.StartsWith("07 /"));Physics.SyncTransforms();return Spots().Select(s=>{
        var direction="/ ("+(-s.n.x).ToString("0")+", "+(-s.n.z).ToString("0")+")";
        var hits=Physics.RaycastAll(s.p+s.n*.65f,-s.n,5,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.transform.IsChildOf(walls.transform)&&(s.gate||h.collider.name.EndsWith(direction))&&Vector3.Dot(h.normal,s.n)>.45f).OrderBy(h=>h.distance).ToArray();if(hits.Length==0)throw new Exception("Missing wall for "+s.region+" "+s.p);return hits[0];}).ToArray();}
    public static string Preview()=>string.Join("\n",Anchors().Select((h,i)=>Spots()[i].region+" / "+h.collider.name+" / "+h.point));
    public static string Main(){
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(EditorApplication.isPlaying||Lightmapping.isRunning||scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Use the main scene in Edit Mode with no running bake.");
        var anchors=Anchors();var spots=Spots();var root=scene.GetRootGameObjects().Single(g=>g.name.StartsWith("11 /"));var group=root.transform.Cast<Transform>().Single(t=>t.name.StartsWith("02 /"));var panels=group.Cast<Transform>().Where(t=>t.name.StartsWith("Diamond relief panel ")).OrderBy(t=>t.name).ToList();
        var backup=Path.Combine(Project,"Tools/SceneSnapshots/SkybreakMain_before_crystal_wall_panels_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity");if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup failed.");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);var source=prefab.GetComponent<MeshFilter>().sharedMesh;
        var meshPath="Assets/Models/Environment/Dungeon/WallDetails/Meshes/DiamondWallPanel_Crystal.asset";var crystalMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(crystalMesh==null){
            crystalMesh=UnityEngine.Object.Instantiate(source);crystalMesh.name="DiamondWallPanel_Crystal";var frame=new List<int>();var crystal=new List<int>();var vertices=source.vertices;var indices=source.GetIndices(0);
            for(int i=0;i<indices.Length;i+=3){bool isCrystal=Enumerable.Range(i,3).All(k=>Mathf.Abs(vertices[indices[k]].x)<.31f&&Mathf.Abs(vertices[indices[k]].y)<.38f);var target=isCrystal?crystal:frame;target.AddRange(new[]{indices[i],indices[i+1],indices[i+2]});}
            if(crystal.Count!=18)throw new Exception("Unexpected centre-diamond triangles: "+crystal.Count);
            crystalMesh.subMeshCount=4;crystalMesh.SetTriangles(frame,0);crystalMesh.SetTriangles(crystal,3);crystalMesh.RecalculateBounds();AssetDatabase.CreateAsset(crystalMesh,meshPath);
        }
        var materialPath="Assets/Materials/Environment/Decoration/Custom/DungeonWallDetails/WallPanelCrystalGlow.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null){material=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Environment/Decoration/Custom/DungeonWallDetails/GuardianCrystal.mat"));AssetDatabase.CreateAsset(material,materialPath);}
        material.SetColor("_BaseColor",new Color(.16f,.72f,.83f));material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",new Color(.12f,.75f,1)*1.8f);material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;EditorUtility.SetDirty(material);
        var materials=prefab.GetComponent<MeshRenderer>().sharedMaterials.Concat(new[]{material}).ToArray();
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Cyan crystal wall panels at marked positions");
        var report=new List<string>{"30 crystal wall panels: 3 entrance/court, 11 winding passage, 5 security/holding doorways, 11 boss chamber/entrance.","Backup: "+backup,"Only the six triangles of the centre diamond use cyan emission. Original frame, backing, recess, UVs and geometry retained.","Baked cyan spill lights: intensity .65 / range 2.4m. No colliders; navigation is unchanged."};
        for(int i=0;i<spots.Length;i++){
            GameObject go;if(i<panels.Count)go=panels[i].gameObject;else{go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,group);Undo.RegisterCreatedObjectUndo(go,"Additional marked wall panel");}
            Undo.RecordObject(go,"Panel region name");Undo.RecordObject(go.transform,"Wall panel anchor");go.name="Diamond relief panel "+(i+1).ToString("00")+" / "+spots[i].region;go.transform.SetPositionAndRotation(anchors[i].point+spots[i].n*.11f,Quaternion.LookRotation(spots[i].n,Vector3.up));go.transform.localScale=Vector3.one;
            var filter=go.GetComponent<MeshFilter>();var r=go.GetComponent<MeshRenderer>();Undo.RecordObject(filter,"Separate centre diamond material slot");Undo.RecordObject(r,"Cyan centre crystal");filter.sharedMesh=crystalMesh;r.sharedMaterials=materials;r.receiveGI=ReceiveGI.Lightmaps;GameObjectUtility.SetStaticEditorFlags(go,GameObjectUtility.GetStaticEditorFlags(go)|StaticEditorFlags.ContributeGI|StaticEditorFlags.BatchingStatic);
            var l=go.GetComponentInChildren<Light>();if(l==null){var lightGo=new GameObject("Wall crystal spill / baked");lightGo.transform.SetParent(go.transform,false);Undo.RegisterCreatedObjectUndo(lightGo,"Panel crystal light");l=Undo.AddComponent<Light>(lightGo);}
            Undo.RecordObject(l,"Soft crystal spill");Undo.RecordObject(l.transform,"Panel light position");l.transform.position=go.transform.TransformPoint(new Vector3(0,0,.35f));l.type=LightType.Point;l.lightmapBakeType=LightmapBakeType.Baked;l.color=new Color(.12f,.75f,1);l.intensity=.65f;l.range=2.4f;l.shadows=LightShadows.None;l.enabled=true;
            foreach(var c in new UnityEngine.Object[]{go.transform,filter,r,l,l.transform})PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            report.Add(go.name+" | origin="+go.transform.position+" | inward="+spots[i].n+" | wall="+anchors[i].collider.name);
        }
        for(int i=spots.Length;i<panels.Count;i++)Undo.DestroyObjectImmediate(panels[i].gameObject);
        if(crystalMesh.uv2.Length!=crystalMesh.vertexCount)throw new Exception("Missing lightmap UVs");Undo.CollapseUndoOperations(undo);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);var dir=Path.Combine(Project,"Docs/GV/LevelDesign/Reports/CrystalWallPanels");Directory.CreateDirectory(dir);File.WriteAllLines(Path.Combine(dir,"Placement.txt"),report);return string.Join("\n",report);
    }
}
