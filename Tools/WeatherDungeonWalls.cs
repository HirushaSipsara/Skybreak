using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class WeatherDungeonWalls
{
    const string Folder="Assets/Models/Environment/Dungeon/Weathering/";
    static Mesh Chipped(Mesh source,int variant){
        var path=Folder+source.name+"_Chipped_"+variant+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing!=null)return existing;
        var mesh=UnityEngine.Object.Instantiate(source);mesh.name=source.name+" / weathered upper stones "+variant;float top=source.bounds.max.y;
        var vertices=mesh.vertices;
        for(int i=0;i<vertices.Length;i++)if(vertices[i].y>top-.8f){
            float x=vertices[i].x;float height=top;
            switch(variant){
                case 0:height=top-.12f-.57f*Mathf.Clamp01((x+.5f)*1.2f);break;
                case 1:height=top-.10f-.53f*Mathf.Clamp01((.5f-x)*1.4f);break;
                case 2:height=top-.30f-.23f*Mathf.Abs(x)*2;break;
                case 3:height=top-.09f-.52f*(1-Mathf.Abs(x)*1.65f);break;
                default:height=top-.36f-.2f*Mathf.Clamp01((x+.5f)*1.5f);break;
            }
            height-=Mathf.Max(0,vertices[i].z)*.18f;var v=vertices[i];v.y=Mathf.Min(v.y,height);vertices[i]=v;
        }
        mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    static Mesh Crack(int variant){
        string path=Folder+"WallCrack_"+variant+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved!=null)return saved;
        var rng=new System.Random(870+variant);var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
        Action<Vector2,Vector2,float> strip=(a,b,width)=>{var n=new Vector2(-(b-a).y,(b-a).x).normalized*width;int k=vertices.Count;foreach(var p in new[]{a-n,a+n,b+n,b-n}){vertices.Add(new Vector3(p.x,p.y,0));uv.Add(Vector2.zero);}triangles.AddRange(new[]{k,k+2,k+1,k,k+3,k+2});};
        var points=new List<Vector2>{Vector2.zero};for(int i=0;i<7;i++)points.Add(new Vector2((float)rng.NextDouble()*.28f-.14f,-(i+1)*.18f));
        for(int i=0;i<7;i++)strip(points[i],points[i+1],.010f+(float)rng.NextDouble()*.009f);
        strip(points[2],points[2]+new Vector2(.22f,-.21f),.010f);strip(points[4],points[4]+new Vector2(-.25f,-.17f),.007f);
        var mesh=new Mesh{name="Shallow stone fracture "+variant};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    public static string Main(){
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Use main scene in Edit Mode");
        if(scene.GetRootGameObjects().Any(g=>g.name.StartsWith("16 /")))throw new Exception("Weathering exists; preserve manual changes");
        var project=Path.GetDirectoryName(Application.dataPath);var backup=Path.Combine(project,"Tools/SceneSnapshots/SkybreakMain_before_world_finish_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity");Directory.CreateDirectory(Path.GetDirectoryName(backup));if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup failed");
        Directory.CreateDirectory(Path.Combine(project,Folder));Directory.CreateDirectory(Path.Combine(project,"Assets/Materials/Environment/Decoration/Custom/Weathering"));AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var root=new GameObject("16 / Weathered stone walls");Undo.RegisterCreatedObjectUndo(root,"Weather dungeon walls");var cracks=new GameObject("01 / Shallow stone fractures");cracks.transform.SetParent(root.transform);var rubble=new GameObject("02 / Fallen stones beside walls");rubble.transform.SetParent(root.transform);
        var walls=scene.GetRootGameObjects().Single(g=>g.name.StartsWith("07 /"));var protectedBounds=scene.GetRootGameObjects().Where(g=>g.name.StartsWith("10 /")||g.name.StartsWith("14 /")).SelectMany(g=>g.GetComponentsInChildren<Renderer>()).Select(r=>r.bounds).ToArray();
        var candidates=walls.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh.bounds.size.x<1.1f&&Regex.IsMatch(f.name,@"/ \((-?\d+), (-?\d+)\)$")).OrderBy(f=>f.transform.position.z).ThenBy(f=>f.transform.position.x).ToArray();int changed=0,fractures=0,stones=0;var rng=new System.Random(6219);
        var materialPath="Assets/Materials/Environment/Decoration/Custom/Weathering/RecessedStone.mat";var dark=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="RecessedStone"};dark.SetColor("_BaseColor",new Color(.055f,.069f,.068f));dark.SetFloat("_Smoothness",.04f);AssetDatabase.CreateAsset(dark,materialPath);
        var floor=scene.GetRootGameObjects().Single(g=>g.name.StartsWith("01 /"));var obstacles=scene.GetRootGameObjects().Where(g=>g.name.StartsWith("09 /")||g.name.StartsWith("10 /")||g.name.StartsWith("11 /")).SelectMany(g=>g.GetComponentsInChildren<Collider>()).Select(c=>c.bounds).ToArray();var boulder=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Decoration/Custom/Cavern/CavernCrag_5.prefab");
        for(int i=0;i<candidates.Length;i++){
            var f=candidates[i];var world=f.GetComponent<Renderer>().bounds;var upper=new Bounds(world.center+Vector3.up*(world.extents.y-.35f),new Vector3(world.size.x,.7f,world.size.z));
            if(i%9<3&&!protectedBounds.Any(b=>b.Intersects(upper))){
                Undo.RecordObject(f,"Chip upper wall stones");f.sharedMesh=Chipped(f.sharedMesh,rng.Next(5));var collider=f.GetComponent<BoxCollider>();if(collider!=null){Undo.DestroyObjectImmediate(collider);var mc=Undo.AddComponent<MeshCollider>(f.gameObject);mc.sharedMesh=f.sharedMesh;}changed++;
            }
            var m=Regex.Match(f.name,@"/ \((-?\d+), (-?\d+)\)$");var inward=-new Vector3(int.Parse(m.Groups[1].Value),0,int.Parse(m.Groups[2].Value));
            if(i%23==7){
                var pos=f.transform.position+inward*.373f+Vector3.up*(4.8f+(float)rng.NextDouble()*.8f);var probe=new Bounds(pos-Vector3.up*.65f,new Vector3(.55f,1.45f,.55f));
                if(!protectedBounds.Any(b=>b.Intersects(probe))){var go=new GameObject("Stone crack "+(++fractures).ToString("00"));go.transform.SetParent(cracks.transform);go.transform.SetPositionAndRotation(pos,Quaternion.LookRotation(inward));go.AddComponent<MeshFilter>().sharedMesh=Crack(fractures%4);var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=dark;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic);}
            }
            if(i%21==4&&(f.transform.position.z<28||f.transform.position.z>39)){
                var pos=f.transform.position+inward*.8f;var check=new Bounds(pos+Vector3.up*.22f,new Vector3(.9f,.65f,.9f));
                if(obstacles.Any(b=>b.Intersects(check))||!Physics.RaycastAll(pos+Vector3.up,Vector3.down,2).Any(h=>h.collider.transform.IsChildOf(floor.transform)))continue;
                var go=(GameObject)PrefabUtility.InstantiatePrefab(boulder);go.name="Fallen wall stone "+(++stones).ToString("00");go.transform.SetParent(rubble.transform);go.transform.SetPositionAndRotation(pos,Quaternion.Euler(0,rng.Next(360),0));go.transform.localScale=new Vector3(.075f,.065f,.085f);PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            }
        }
        if(changed<70)throw new Exception("Too few weathered wall sections");AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory(Path.Combine(project,"Docs/GV/LevelDesign/Reports/WorldFinish"));var report="Weathered upper edges of "+changed+" wall sections; "+fractures+" shallow fractures; "+stones+" small fallen stones.\nLower wall vertices, textures, doorframes, floor and existing cover retained. Changed walls use matching MeshColliders. Door/pipeline bounds protected. Rubble is visual-only and avoids existing obstacles.\nSeed 6219. AI-assisted procedural mesh variations, not independent Student 3 modelling.\nBackup: "+backup;File.WriteAllText(Path.Combine(project,"Docs/GV/LevelDesign/Reports/WorldFinish/Weathering.txt"),report);return report;
    }
}
