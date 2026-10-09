using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class AddBranchingWallPipes
{
    class Face { public Vector3 point,normal; }
    const string MeshFolder="Assets/Models/Environment/Dungeon/WallDetails/Meshes/BranchingPipes/";
    static bool WallAt(Vector3 point,Vector3 normal,Transform walls){
        return Physics.RaycastAll(point+normal*.65f,-normal,1.2f,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.transform.IsChildOf(walls)&&Vector3.Dot(h.normal,normal)>.98f&&Mathf.Abs(Vector3.Dot(h.point-point,normal))<.06f);
    }
    public static string Main(){
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Use main scene in Edit Mode");
        if(scene.GetRootGameObjects().Any(g=>g.name=="14 / Branching copper wall vines"))throw new Exception("Already installed; preserve manual edits");
        var project=Path.GetDirectoryName(Application.dataPath);
        var backup=Path.Combine(project,"Tools/SceneSnapshots/SkybreakMain_before_branching_pipes_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity");
        if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup failed");
        var walls=scene.GetRootGameObjects().Single(g=>g.name=="07 / Modular walls").transform;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Decoration/Custom/DungeonWallDetails/CopperWallConduit_1m.prefab");
        var source=prefab.GetComponent<MeshFilter>().sharedMesh;var materials=prefab.GetComponent<MeshRenderer>().sharedMaterials;
        var existing=scene.GetRootGameObjects().Single(g=>g.name=="11 / Carved walls and guardian statues").GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.Contains("relief panel")||r.name.StartsWith("Guardian statue")||r.name.Contains("Copper")).Select(r=>r.bounds).ToList();
        existing.AddRange(scene.GetRootGameObjects().Single(g=>g.name=="09 / Dungeon decoration").GetComponentsInChildren<MeshRenderer>().Select(r=>r.bounds));
        var faces=new List<Face>();
        foreach(var renderer in walls.GetComponentsInChildren<MeshRenderer>()){
            var match=Regex.Match(renderer.name,@"/ \((-?\d+), (-?\d+)\)$");if(!match.Success)continue;
            var n=-new Vector3(int.Parse(match.Groups[1].Value),0,int.Parse(match.Groups[2].Value));var point=renderer.transform.position+n*.36f;point.y=4.35f;
            if(n.sqrMagnitude>.9f)faces.Add(new Face{point=point,normal=n});
        }
        Directory.CreateDirectory(Path.Combine(project,MeshFolder));AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);Physics.SyncTransforms();
        var rng=new System.Random(7142);var anchors=new List<Vector3>();var root=new GameObject("14 / Branching copper wall vines");Undo.RegisterCreatedObjectUndo(root,"Growing wall pipes");int count=0,total=0;
        var regions=new[]{"Entrance and loading court","Maintenance and security wing","Boss chamber"};
        var regionCounts=new int[3];
        for(int region=0;region<3;region++){
            var candidates=faces.Where(f=>region==0?f.point.z<28:region==1?f.point.z>=28&&f.point.z<70:f.point.z>=70).OrderBy(f=>rng.Next()).ToArray();
            foreach(var face in candidates){
                if(regionCounts[region]>=8)break;
                if(anchors.Any(a=>Vector3.Distance(a,face.point)<4.4f))continue;
                var rotation=Quaternion.LookRotation(face.normal);var tangent=rotation*Vector3.right;
                var origin=face.point;origin.y=4.05f+(float)rng.NextDouble()*.9f;
                var lines=new List<Tuple<Vector2,Vector2>>();var occupied=new HashSet<string>();
                Func<Vector2,Vector3> world=p=>origin+tangent*p.x+Vector3.up*p.y;
                Func<Vector2,Vector2,bool> add=(a,b)=>{
                    var middle=(a+b)*.5f;var pos=world(middle);
                    if(Mathf.Min(world(a).y,world(b).y)<3.25f||Mathf.Max(world(a).y,world(b).y)>6.08f)return false;
                    if(!WallAt(world(a),face.normal,walls)||!WallAt(world(b),face.normal,walls)||!WallAt(pos,face.normal,walls))return false;
                    var bounds=new Bounds(pos+face.normal*.15f,new Vector3(.35f,.35f,.35f));bounds.Encapsulate(world(a)+face.normal*.15f);bounds.Encapsulate(world(b)+face.normal*.15f);
                    if(existing.Any(e=>e.Intersects(bounds)))return false;
                    var key=middle.ToString("F2");if(!occupied.Add(key))return false;
                    lines.Add(Tuple.Create(a,b));return true;
                };
                // Stable seeded variation: meandering trunk, then smaller offshoots.
                var node=Vector2.zero;var nodes=new List<Vector2>{node};float direction=rng.Next(2)==0?-1:1;
                for(int i=0;i<7;i++){
                    var step=i%3==2?new Vector2(0,rng.Next(2)==0?-.75f:.75f):new Vector2(direction*.75f,0);
                    if(!add(node,node+step)){step=new Vector2(0,world(node).y<4.7f?.75f:-.75f);if(!add(node,node+step))break;}
                    node+=step;nodes.Add(node);
                }
                foreach(var junction in nodes.Where((n,i)=>i>0&&i%2==1).ToArray()){
                    var branch=junction;var step=new Vector2(0,world(branch).y>4.6f?-.75f:.75f);
                    if(add(branch,branch+step)){branch+=step;var side=new Vector2((rng.Next(2)==0?-1:1)*.75f,0);add(branch,branch+side);}
                }
                if(lines.Count<4)continue;
                var parts=new List<Mesh>();
                for(int sub=0;sub<source.subMeshCount;sub++){
                    var mesh=new Mesh();mesh.CombineMeshes(lines.Select(line=>{
                        var delta=line.Item2-line.Item1;var angle=Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg;
                        var local=new Vector3((line.Item1.x+line.Item2.x)*.5f,(line.Item1.y+line.Item2.y)*.5f,.065f);
                        return new CombineInstance{mesh=source,subMeshIndex=sub,transform=Matrix4x4.TRS(local,Quaternion.Euler(0,0,angle),new Vector3(delta.magnitude,1,1))};
                    }).ToArray(),true,true);parts.Add(mesh);
                }
                var combined=new Mesh{name="Branching copper vines "+(++count).ToString("00")};combined.CombineMeshes(parts.Select(m=>new CombineInstance{mesh=m,transform=Matrix4x4.identity}).ToArray(),false,true);foreach(var m in parts)UnityEngine.Object.DestroyImmediate(m);
                var meshPath=MeshFolder+"BranchingCopperVines_"+count.ToString("00")+".asset";
                var savedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(savedMesh==null)AssetDatabase.CreateAsset(combined,meshPath);
                else{EditorUtility.CopySerialized(combined,savedMesh);EditorUtility.SetDirty(savedMesh);UnityEngine.Object.DestroyImmediate(combined);combined=savedMesh;}
                var go=new GameObject(regions[region]+" / pipe vine "+count.ToString("00"));Undo.RegisterCreatedObjectUndo(go,"Growing wall pipes");go.transform.SetParent(root.transform);go.transform.SetPositionAndRotation(origin,rotation);go.AddComponent<MeshFilter>().sharedMesh=combined;go.AddComponent<MeshRenderer>().sharedMaterials=materials;GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic);
                anchors.Add(face.point);total+=lines.Count;regionCounts[region]++;
            }
        }
        if(count<15)throw new Exception("Too few suitable wall locations: "+count);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed");
        var report=count+" branching wall-pipe assemblies, "+total+" conduit modules.\nRegions: "+string.Join(", ",regionCounts)+".\nSeed: 7142; growth is fixed when saved, not random during gameplay.\nAll pipe segment endpoints and centres backed by the matching wall plane. Height 3.25m–6.08m, no colliders or additional lights.\nExisting relief panels/statues/conduits/decoration avoided by bounds checks.\nCombined per assembly, "+total*320+" added triangles.\nBackup: "+backup;
        File.WriteAllText(Path.Combine(project,"Docs/GV/LevelDesign/Reports/WallDetails/BranchingWallPipes.txt"),report);Selection.activeGameObject=root;return report;
    }
}
