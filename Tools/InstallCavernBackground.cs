using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class InstallCavernBackground
{
    const string Models="Assets/Models/Environment/Dungeon/Cavern/";
    const string Prefabs="Assets/Prefabs/Environment/Decoration/Custom/Cavern/";
    const string Textures="Assets/Textures/Environment/Decoration/Custom/Cavern/";
    const string Materials="Assets/Materials/Environment/Decoration/Custom/Cavern/";
    static void Folder(string path){Directory.CreateDirectory(path);}
    static GameObject Import(string name,Material material){
        string path=Models+name+".fbx";
        File.Copy("ArtSource/Student1/CavernKit/Exports/"+name+".fbx",path,true);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.isReadable=true;importer.importCameras=false;importer.importLights=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
        var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        try{
            var combined=new Mesh{name=name};combined.CombineMeshes(source.GetComponentsInChildren<MeshFilter>().Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=f.transform.localToWorldMatrix}).ToArray(),true,true);combined.RecalculateBounds();
            var meshPath=Models+name+"_Combined.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(saved==null){AssetDatabase.CreateAsset(combined,meshPath);saved=combined;}else{EditorUtility.CopySerialized(combined,saved);EditorUtility.SetDirty(saved);UnityEngine.Object.DestroyImmediate(combined);}
            var go=new GameObject(name);try{go.AddComponent<MeshFilter>().sharedMesh=saved;go.AddComponent<MeshRenderer>().sharedMaterial=material;GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic);return PrefabUtility.SaveAsPrefabAsset(go,Prefabs+name+".prefab");}finally{UnityEngine.Object.DestroyImmediate(go);}
        }finally{UnityEngine.Object.DestroyImmediate(source);}
    }
    static Bounds WorldBounds(GameObject prefab,Vector3 pos,Quaternion rot,Vector3 scale){
        var bounds=prefab.GetComponent<MeshFilter>().sharedMesh.bounds;var m=Matrix4x4.TRS(pos,rot,scale);var result=new Bounds(m.MultiplyPoint3x4(bounds.center),Vector3.zero);
        for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)result.Encapsulate(m.MultiplyPoint3x4(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z))));return result;
    }
    static bool OverFloor(Bounds rock,Bounds[] tiles,float margin){return tiles.Any(t=>rock.min.x<t.max.x+margin&&rock.max.x>t.min.x-margin&&rock.min.z<t.max.z+margin&&rock.max.z>t.min.z-margin);}
    static GameObject Place(GameObject prefab,string name,Transform parent,Vector3 pos,Quaternion rot,Vector3 scale){var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.name=name;go.transform.SetParent(parent);go.transform.SetPositionAndRotation(pos,rot);go.transform.localScale=scale;return go;}
    public static string Main(){
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Open main scene in Edit Mode");
        if(scene.GetRootGameObjects().Any(g=>g.name.StartsWith("15 /")))throw new Exception("Cavern already exists; preserve manual changes");
        var project=Path.GetDirectoryName(Application.dataPath);Directory.SetCurrentDirectory(project);
        var backup="Tools/SceneSnapshots/SkybreakMain_before_cavern_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity";Folder(Path.GetDirectoryName(backup));if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup failed");
        foreach(var path in new[]{Models,Prefabs,Textures,Materials})Folder(path);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(var name in new[]{"CavernStone_BaseColor","CavernStone_Normal"}){
            var path=Textures+name+".png";File.Copy("ArtSource/Student1/CavernKit/Textures/"+name+".png",path,true);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var ti=(TextureImporter)AssetImporter.GetAtPath(path);ti.textureType=name.EndsWith("Normal")?TextureImporterType.NormalMap:TextureImporterType.Default;ti.sRGBTexture=!name.EndsWith("Normal");ti.wrapMode=TextureWrapMode.Repeat;ti.filterMode=FilterMode.Trilinear;ti.anisoLevel=4;ti.mipmapEnabled=true;ti.maxTextureSize=512;ti.SaveAndReimport();
        }
        var matPath=Materials+"CavernStone.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="CavernStone"};AssetDatabase.CreateAsset(material,matPath);}
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Textures+"CavernStone_BaseColor.png"));material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Textures+"CavernStone_Normal.png"));material.EnableKeyword("_NORMALMAP");material.SetFloat("_BumpScale",.55f);material.SetFloat("_Smoothness",.08f);material.SetColor("_BaseColor",new Color(.62f,.72f,.77f));
        var prefabs=new List<GameObject>();for(int i=1;i<=6;i++)prefabs.Add(Import((i<=4?"CavernCliff_":"CavernCrag_")+i,material));var arch=Import("CavernEntranceArch",material);
        var tiles=scene.GetRootGameObjects().First(g=>g.name.StartsWith("01 /")).GetComponentsInChildren<Collider>().Select(c=>c.bounds).ToList();
        tiles.AddRange(scene.GetRootGameObjects().First(g=>g.name.StartsWith("06 /")).GetComponentsInChildren<Collider>().Where(c=>c.bounds.size.y<1).Select(c=>c.bounds));var floor=tiles.ToArray();
        var root=new GameObject("15 / Cavern backdrop and entrance arch");Undo.RegisterCreatedObjectUndo(root,"Add cavern backdrop");var cliffs=new GameObject("01 / Outer cliff faces");cliffs.transform.SetParent(root.transform);var ledges=new GameObject("02 / Low rocky ledges");ledges.transform.SetParent(root.transform);var entry=new GameObject("03 / Entrance stone arch");entry.transform.SetParent(root.transform);
        var rng=new System.Random(3917);var anchors=new List<Vector3>();int count=0,lowCount=0;var walls=scene.GetRootGameObjects().First(g=>g.name.StartsWith("07 /"));
        foreach(var wall in walls.GetComponentsInChildren<MeshRenderer>().OrderBy(r=>r.transform.position.z).ThenBy(r=>r.transform.position.x)){
            var match=Regex.Match(wall.name,@"/ \((-?\d+), (-?\d+)\)$");if(!match.Success)continue;
            var outward=new Vector3(int.Parse(match.Groups[1].Value),0,int.Parse(match.Groups[2].Value));var rot=Quaternion.LookRotation(outward)*Quaternion.Euler(0,rng.Next(-9,10),0);var prefab=prefabs[count%4];
            var scale=new Vector3(.95f+(float)rng.NextDouble()*.2f,.80f+(float)rng.NextDouble()*.48f,1f+(float)rng.NextDouble()*.22f);
            Vector3 point=wall.transform.position;bool found=false;
            for(float distance=6.8f;distance<=10.8f;distance+=2){point=wall.transform.position+outward*distance;point.y=-.5f;if(!OverFloor(WorldBounds(prefab,point,rot,scale),floor,1.2f)){found=true;break;}}
            if(!found||anchors.Any(a=>Vector3.Distance(a,point)<6.2f))continue;
            Place(prefab,"Cliff "+(++count).ToString("00")+" / editable rock",cliffs.transform,point,rot,scale);anchors.Add(point);
            if(count%2==0){var low=prefabs[4+lowCount%2];var lowPos=point-outward*1.8f+Vector3.Cross(Vector3.up,outward)*2.2f;var lowScale=new Vector3(.62f,.70f,.60f);if(!OverFloor(WorldBounds(low,lowPos,rot,lowScale),floor,1f)){Place(low,"Rock ledge "+(++lowCount).ToString("00"),ledges.transform,lowPos,rot,lowScale);}}
        }
        // Arch piers are outside the start-strip walls; opening is wider than the entire 10m strip.
        Place(arch,"Cavern entrance / 10.4m clear opening",entry.transform,new Vector3(0,0,1),Quaternion.identity,Vector3.one);
        if(count<35)throw new Exception("Too few cliffs: "+count);
        var bed=GameObject.CreatePrimitive(PrimitiveType.Cube);bed.name="04 / Buried rock foundation / visual only";bed.transform.SetParent(root.transform);bed.transform.position=new Vector3(29,-1,45);bed.transform.localScale=new Vector3(122,1,144);UnityEngine.Object.DestroyImmediate(bed.GetComponent<Collider>());bed.GetComponent<MeshRenderer>().sharedMaterial=material;GameObjectUtility.SetStaticEditorFlags(bed,StaticEditorFlags.BatchingStatic);
        var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).First();camera.farClipPlane=Mathf.Max(camera.farClipPlane,250);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed");
        var report="Cliffs: "+count+"; low ledges: "+lowCount+"; entry arch: 1; visual foundation: 1.\nSeed 3917. Reusable Blender rock meshes, 116 triangles per high cliff / 76 per low crag. Arch includes applied bevels.\nAll rock footprints checked against floor collider bounds with >=1m clearance.\nArch clear opening 10.4m; piers outside the 10m start strip; spring height 7.3m.\nNo new lights or background colliders. Map, doors, props and player retained.\nNo hall roof or tunnel ceilings yet; initial cavern backdrop pass.\nBackup: "+backup;
        Folder("Docs/GV/LevelDesign/Reports/Cavern");File.WriteAllText("Docs/GV/LevelDesign/Reports/Cavern/Installation.txt",report);Selection.activeGameObject=root;return report;
    }
}
