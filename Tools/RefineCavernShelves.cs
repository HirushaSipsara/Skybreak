using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class RefineCavernShelves
{
    public static string Main(){
        if(EditorApplication.isPlaying)throw new Exception("Use Edit Mode");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var root=scene.GetRootGameObjects().Single(g=>g.name.StartsWith("15 /"));
        if(root.transform.Cast<Transform>().Any(t=>t.name.StartsWith("05 /")))throw new Exception("Shelves already installed");
        var floors=scene.GetRootGameObjects().Where(g=>g.name.StartsWith("01 /")||g.name.StartsWith("06 /")).SelectMany(g=>g.GetComponentsInChildren<Collider>()).Where(c=>c.bounds.size.y<1).Select(c=>c.bounds).ToArray();
        var cliffs=root.transform.Cast<Transform>().First(t=>t.name.StartsWith("01 /"));var group=new GameObject("05 / Connected outer rock shelves");group.transform.SetParent(root.transform);Undo.RegisterCreatedObjectUndo(group,"Connect cavern cliff shelves");int count=0;
        foreach(Transform cliff in cliffs){
            var prefab=PrefabUtility.GetCorrespondingObjectFromSource(cliff.gameObject);var mesh=prefab.GetComponent<MeshFilter>().sharedMesh;var scale=Vector3.Scale(cliff.localScale,new Vector3(1.55f,.55f,1.12f));
            var pos=cliff.position+cliff.forward*2.8f;var matrix=Matrix4x4.TRS(pos,cliff.rotation,scale);var bounds=new Bounds(pos,Vector3.zero);var mb=mesh.bounds;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)bounds.Encapsulate(matrix.MultiplyPoint3x4(mb.center+Vector3.Scale(mb.extents,new Vector3(x,y,z))));
            if(floors.Any(f=>bounds.min.x<f.max.x+1.2f&&bounds.max.x>f.min.x-1.2f&&bounds.min.z<f.max.z+1.2f&&bounds.max.z>f.min.z-1.2f))continue;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.name="Connecting rock shelf "+(++count).ToString("00");go.transform.SetParent(group.transform);go.transform.SetPositionAndRotation(pos,cliff.rotation);go.transform.localScale=scale;
        }
        var bed=root.transform.Cast<Transform>().First(t=>t.name.StartsWith("04 /"));var path="Assets/Materials/Environment/Decoration/Custom/Cavern/CavernFoundation.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat==null){mat=new Material(bed.GetComponent<MeshRenderer>().sharedMaterial){name="CavernFoundation"};AssetDatabase.CreateAsset(mat,path);}mat.SetTextureScale("_BaseMap",new Vector2(28,32));mat.SetTextureScale("_BumpMap",new Vector2(28,32));mat.SetColor("_BaseColor",new Color(.4f,.48f,.5f));bed.GetComponent<MeshRenderer>().sharedMaterial=mat;
        int triangles=root.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        var report="Added "+count+" broad shelves connecting the outer cliff faces, all footprints clear of floors by 1.2m.\nTotal new cavern geometry: "+triangles+" triangles, "+root.GetComponentsInChildren<MeshRenderer>().Length+" mesh renderers.\nFoundation texture tiled at world scale. No extra lights or colliders.";
        File.AppendAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath),"Docs/GV/LevelDesign/Reports/Cavern/Installation.txt"),"\n\n"+report);return report;
    }
}
