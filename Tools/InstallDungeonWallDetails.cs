using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class InstallDungeonWallDetails
{
    const string Models="Assets/Models/Environment/Dungeon/WallDetails/";
    const string Prefabs="Assets/Prefabs/Environment/Decoration/Custom/DungeonWallDetails/";
    const string Mats="Assets/Materials/Environment/Decoration/Custom/DungeonWallDetails/";
    static Dictionary<string,Material> palette=new Dictionary<string,Material>();
    static Dictionary<int,GameObject> trimRuns=new Dictionary<int,GameObject>();
    static Mesh trimMesh;static Material[] trimMats;static int trims,panels,conduits;
    static void Stone(string key,string name,float factor){
        var path=Mats+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Environment/StoneKit/Wall_Textured_CoreFloor_Body.mat")){name=name};AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",new Color(factor,factor,factor));m.SetFloat("_Smoothness",.24f);m.enableInstancing=true;EditorUtility.SetDirty(m);palette[key]=m;
    }
    static void Flat(string key,Color color,float metal=0,float glow=0){
        var path=Mats+key+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",.3f);m.enableInstancing=true;
        if(glow>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*glow);}EditorUtility.SetDirty(m);palette[key]=m;
    }
    static Mesh SaveMesh(Mesh mesh,string name){
        var path=Models+"Meshes/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing==null){mesh.name=name;AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EditorUtility.CopySerialized(mesh,existing);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(mesh);return existing;
    }
    static GameObject BuildPrefab(string name){
        var importer=(ModelImporter)AssetImporter.GetAtPath(Models+name+".fbx");importer.isReadable=true;importer.SaveAndReimport();
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(Models+name+".fbx");
        var preview=EditorSceneManager.NewPreviewScene();var content=UnityEngine.Object.Instantiate(model);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(content,preview);
        var result=new GameObject(name);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(result,preview);
        var pieces=new List<Mesh>();var combinedByMaterial=new List<Mesh>();
        try{
            // Bake imported axis conversion into meshes; reusable prefab roots stay at identity.
            var buckets=new Dictionary<string,List<CombineInstance>>();int stoneVariant=0;
            foreach(var renderer in content.GetComponentsInChildren<MeshRenderer>()){
                var source=renderer.GetComponent<MeshFilter>().sharedMesh;var copy=UnityEngine.Object.Instantiate(source);pieces.Add(copy);
                var materialKey=renderer.sharedMaterial.name;if(!palette.ContainsKey(materialKey))throw new Exception("Unmapped material "+materialKey);
                if(materialKey.StartsWith("GuardianStone")){
                    int tile=(stoneVariant++*7+5)%16;var offset=new Vector2((tile%4)*.25f+.004f,(tile/4)*.25f+.004f);copy.uv=copy.uv.Select(uv=>offset+uv*.242f).ToArray();copy.RecalculateTangents();
                }
                if(!buckets.ContainsKey(materialKey))buckets[materialKey]=new List<CombineInstance>();
                buckets[materialKey].Add(new CombineInstance{mesh=copy,transform=renderer.transform.localToWorldMatrix});
            }
            foreach(var list in buckets.Values){var m=new Mesh();m.CombineMeshes(list.ToArray(),true,true);combinedByMaterial.Add(m);}
            var final=new Mesh();final.CombineMeshes(combinedByMaterial.Select(m=>new CombineInstance{mesh=m,transform=Matrix4x4.identity}).ToArray(),false,true);final.RecalculateBounds();
            var asset=SaveMesh(final,name+"_Combined");result.AddComponent<MeshFilter>().sharedMesh=asset;result.AddComponent<MeshRenderer>().sharedMaterials=buckets.Keys.Select(k=>palette[k]).ToArray();
            if(name=="WeatheredGuardianStatue"){var collider=result.AddComponent<BoxCollider>();collider.center=new Vector3(0,1.4f,.04f);collider.size=new Vector3(1.34f,2.8f,1.12f);}
            GameObjectUtility.SetStaticEditorFlags(result,StaticEditorFlags.BatchingStatic);
            return PrefabUtility.SaveAsPrefabAsset(result,Prefabs+name+".prefab");
        }finally{foreach(var m in pieces.Concat(combinedByMaterial))UnityEngine.Object.DestroyImmediate(m);UnityEngine.Object.DestroyImmediate(content);UnityEngine.Object.DestroyImmediate(result);EditorSceneManager.ClosePreviewScene(preview);}
    }
    static GameObject TrimRun(int length){
        if(trimRuns.ContainsKey(length))return trimRuns[length];
        var parts=new List<Mesh>();
        for(int sub=0;sub<trimMesh.subMeshCount;sub++){
            var mesh=new Mesh();mesh.CombineMeshes(Enumerable.Range(0,length).Select(i=>new CombineInstance{mesh=trimMesh,subMeshIndex=sub,transform=Matrix4x4.Translate(Vector3.right*i)}).ToArray(),true,true);parts.Add(mesh);
        }
        var final=new Mesh();final.CombineMeshes(parts.Select(m=>new CombineInstance{mesh=m,transform=Matrix4x4.identity}).ToArray(),false,true);foreach(var m in parts)UnityEngine.Object.DestroyImmediate(m);
        var g=new GameObject("Carved stone trim / "+length+"m run");g.AddComponent<MeshFilter>().sharedMesh=SaveMesh(final,"CarvedTrimRun_"+length+"m");g.AddComponent<MeshRenderer>().sharedMaterials=trimMats;
        GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.BatchingStatic);var p=PrefabUtility.SaveAsPrefabAsset(g,Prefabs+"CarvedTrimRun_"+length+"m.prefab");UnityEngine.Object.DestroyImmediate(g);trimRuns[length]=p;return p;
    }
    static Transform Group(string name,Transform parent){var g=new GameObject(name);g.transform.SetParent(parent,false);Undo.RegisterCreatedObjectUndo(g,"Approved wall details");return g.transform;}
    static GameObject Place(GameObject prefab,Transform parent,Vector3 pos,Quaternion rot,string name){
        var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);g.name=name;g.transform.SetPositionAndRotation(pos,rot);Undo.RegisterCreatedObjectUndo(g,"Approved wall details");PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);return g;
    }
    static RaycastHit WallHit(Transform walls,Vector3 origin,Vector3 wallward,float range=8){
        var hits=Physics.RaycastAll(origin,wallward,range,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.transform.IsChildOf(walls)).OrderBy(h=>h.distance).ToArray();if(hits.Length==0)throw new Exception("Missing wall at "+origin);return hits[0];
    }
    public static string Main(){
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Open the main map outside Play Mode");
        if(scene.GetRootGameObjects().Any(g=>g.name=="11 / Carved walls and guardian statues"))throw new Exception("Wall details already placed; preserve current edits.");
        var project=Path.GetDirectoryName(Application.dataPath);foreach(var path in new[]{Models+"Meshes/",Prefabs,Mats})Directory.CreateDirectory(Path.Combine(project,path));AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Stone("GuardianStone","CarvedStone",.83f);Stone("GuardianStoneLight","CarvedStoneEdges",1.06f);Flat("CarvedShadow",new Color(.075f,.11f,.13f));Flat("ConduitCopper",new Color(.58f,.34f,.15f),.65f);Flat("GuardianCrystal",new Color(.17f,.70f,.81f),0,.65f);
        var trim=BuildPrefab("CarvedStoneTrim_1m");var panel=BuildPrefab("DiamondWallPanel");var conduit=BuildPrefab("CopperWallConduit_1m");var statue=BuildPrefab("WeatheredGuardianStatue");trimMesh=trim.GetComponent<MeshFilter>().sharedMesh;trimMats=trim.GetComponent<MeshRenderer>().sharedMaterials;
        var walls=scene.GetRootGameObjects().First(g=>g.name=="07 / Modular walls").transform;Physics.SyncTransforms();
        var panelSpec=new[]{new Vector3(-12,4.2f,23),new Vector3(12,4.2f,8.5f),new Vector3(0,4.2f,-8),new Vector3(23,4.2f,29.5f),new Vector3(8,4.2f,43.5f),new Vector3(29,4.2f,55.5f),new Vector3(55,4.2f,40.5f),new Vector3(39,4.2f,69),new Vector3(45,4.2f,64),new Vector3(29,4.2f,82),new Vector3(55,4.2f,89),new Vector3(22.7f,4.55f,54),new Vector3(62.3f,4.55f,46)};
        var panelDirs=new[]{Vector3.left,Vector3.right,Vector3.back,Vector3.right,Vector3.left,Vector3.left,Vector3.right,Vector3.left,Vector3.right,Vector3.left,Vector3.right,Vector3.left,Vector3.right};
        var panelHits=panelSpec.Select((p,i)=>WallHit(walls,p,panelDirs[i])).ToArray();
        var statuePositions=new[]{new Vector3(-3.7f,0,-7.5f),new Vector3(-11.45f,0,17),new Vector3(30.1f,0,43),new Vector3(54,0,57.7f),new Vector3(31.5f,0,90.5f),new Vector3(52.5f,0,90.5f)};
        var floor=scene.GetRootGameObjects().First(g=>g.name.StartsWith("01 / STONE FLOOR"));
        foreach(var pos in statuePositions)if(!Physics.RaycastAll(pos+Vector3.up*2,Vector3.down,3).Any(h=>h.collider.transform.IsChildOf(floor.transform)))throw new Exception("Statue is outside floor "+pos);
        var backup=Path.Combine(project,"Tools/SceneSnapshots/SkybreakMain_before_wall_details_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity");Directory.CreateDirectory(Path.GetDirectoryName(backup));if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup failed");
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Approved carved walls and guardian statues");
        var root=Group("11 / Carved walls and guardian statues",null);var trimGroup=Group("01 / Continuous carved stone bands",root);var panelGroup=Group("02 / Diamond wall relief panels",root);var conduitGroup=Group("03 / Copper lantern conduits",root);var statueGroup=Group("04 / Six weathered guardian statues",root);
        var sections=new List<Tuple<Vector3,Vector3>>();
        foreach(var renderer in walls.GetComponentsInChildren<MeshRenderer>()){
            var match=Regex.Match(renderer.name,@"/ \((-?\d+), (-?\d+)\)$");if(!match.Success)continue;
            var normal=-new Vector3(int.Parse(match.Groups[1].Value),0,int.Parse(match.Groups[2].Value));var face=renderer.transform.position+normal*.385f+Vector3.up*2.65f;sections.Add(Tuple.Create(face,normal));
        }
        // Merge contiguous one-metre bands into short reusable runs instead of hundreds of renderers.
        foreach(var line in sections.GroupBy(s=>s.Item2.ToString()+" / "+Vector3.Dot(s.Item1,s.Item2).ToString("F2"))){
            var normal=line.First().Item2;var rotation=Quaternion.LookRotation(normal);var tangent=rotation*Vector3.right;var ordered=line.OrderBy(s=>Vector3.Dot(s.Item1,tangent)).ToArray();int begin=0;
            for(int i=1;i<=ordered.Length;i++)if(i==ordered.Length||Vector3.Distance(ordered[i].Item1,ordered[i-1].Item1)>1.02f){var length=i-begin;Place(TrimRun(length),trimGroup,ordered[begin].Item1,rotation,"Stone band / "+length+"m");trims+=length;begin=i;}
        }
        foreach(var frame in walls.GetComponentsInChildren<Transform>().Where(t=>t.name.Contains("open doorway walls"))){
            foreach(var normal in new[]{frame.forward,-frame.forward}){
                var rotation=Quaternion.LookRotation(normal);var face=frame.position+normal*.385f;
                foreach(var side in new[]{-1,1}){var g=Place(trim,trimGroup,face+frame.right*(side*1.75f)+Vector3.up*2.65f,rotation,"Door pier carved band");g.transform.localScale=new Vector3(.5f,1,1);PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);trims++;}
                Place(TrimRun(4),trimGroup,face-(rotation*Vector3.right)*1.5f+Vector3.up*3.4f,rotation,"Door lintel carved band");trims+=4;
            }
        }
        foreach(var hit in panelHits){Place(panel,panelGroup,hit.point+hit.normal*.075f,Quaternion.LookRotation(hit.normal),"Diamond relief panel "+(++panels).ToString("00"));}
        var decoration=scene.GetRootGameObjects().First(g=>g.name=="09 / Dungeon decoration");var lamps=decoration.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Vault wall lantern ")).OrderBy(t=>t.name).ToArray();
        for(int i=0;i<lamps.Length;i+=2){
            var lamp=lamps[i];var assembly=Group("Conduit assembly / "+lamp.name,conduitGroup);var normal=lamp.forward;var rotation=Quaternion.LookRotation(normal);var tangent=rotation*Vector3.right;
            foreach(float offset in new[]{-1f,0,1f}){
                var origin=lamp.position+tangent*offset+normal*.5f;origin.y=5.25f;
                var hits=Physics.RaycastAll(origin,-normal,1.5f).Where(h=>h.collider.transform.IsChildOf(walls)).OrderBy(h=>h.distance).ToArray();if(hits.Length==0)continue;
                Place(conduit,assembly,hits[0].point+normal*.065f,rotation,"Copper high wall line");conduits++;
            }
            foreach(float y in new[]{4.05f,4.85f}){
                var origin=lamp.position+tangent*.43f+normal*.5f;origin.y=y;var hit=WallHit(walls,origin,-normal,1.5f);
                var g=Place(conduit,assembly,hit.point+normal*.065f,rotation*Quaternion.Euler(0,0,90),"Copper downfeed to lantern");g.transform.localScale=new Vector3(.8f,1,1);PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);conduits++;
            }
        }
        var yaw=new[]{0,90,90,270,145,215};for(int i=0;i<statuePositions.Length;i++)Place(statue,statueGroup,statuePositions[i],Quaternion.Euler(0,yaw[i],0),"Guardian statue "+(i+1).ToString("00")+" / architectural decoration");
        Physics.SyncTransforms();var routes=new[]{new[]{new Vector2(0,-4),new Vector2(0,21),new Vector2(21,21),new Vector2(21,35),new Vector2(9,35),new Vector2(9,46),new Vector2(42,46),new Vector2(42,66),new Vector2(42,84)},new[]{new Vector2(42,54),new Vector2(19,54)},new[]{new Vector2(42,46),new Vector2(66,46)}};var blocks=new List<string>();
        foreach(var route in routes)for(int i=1;i<route.Length;i++){
            var delta=route[i]-route[i-1];var start=new Vector3(route[i-1].x,.08f,route[i-1].y);var dir=new Vector3(delta.x,0,delta.y).normalized;
            blocks.AddRange(Physics.CapsuleCastAll(start+Vector3.up*.44f,start+Vector3.up*2.02f,.416f,dir,delta.magnitude,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.transform.IsChildOf(root)).Select(h=>h.collider.name));
        }
        if(blocks.Count>0){Undo.RevertAllDownToGroup(undo);throw new Exception("New details blocked routes; scene changes rolled back: "+string.Join(",",blocks));}
        Undo.CollapseUndoOperations(undo);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed");
        var report="Stone trim: "+trims+" one-metre equivalents / "+trimGroup.childCount+" reusable continuous runs or doorway pieces.\nDiamond panels: "+panels+"\nConduit modules: "+conduits+" / "+conduitGroup.childCount+" assemblies\nStatues: 6; each 2.8m high and 1036 exported triangles.\nNew detail capsule route obstructions: "+blocks.Count+"\nNo new realtime lights. Floor, walls, cover, reactor, lanterns and working gates retained.\nBackup: "+backup;
        var dirOut=Path.Combine(project,"Docs/GV/LevelDesign/Reports/WallDetails");Directory.CreateDirectory(dirOut);File.WriteAllText(Path.Combine(dirOut,"PlacementChecks.txt"),report);
        Selection.activeGameObject=root.gameObject;SceneView.lastActiveSceneView?.FrameSelected();return report;
    }
}
