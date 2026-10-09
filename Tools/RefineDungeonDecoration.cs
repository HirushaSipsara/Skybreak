using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class RefineDungeonDecoration
{
    public static string Main()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var root=scene.GetRootGameObjects().First(g=>g.name=="09 / Dungeon decoration");
        var stone=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Environment/Decoration/MapAccents/VaultStone.mat");
        foreach(var go in root.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("structure-barrier / ")).ToArray()){
            var parent=go.parent;var pos=go.position;var rot=go.rotation;
            Undo.DestroyObjectImmediate(go.gameObject);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Decoration/KayKit_Dungeon/barrier_column.prefab");
            var replacement=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);Undo.RegisterCreatedObjectUndo(replacement,"Refine dungeon cover");
            replacement.name="Solid stone cover / "+pos.x+", "+pos.z;replacement.transform.SetPositionAndRotation(pos,rot);replacement.transform.localScale=Vector3.one*(1.65f/1.4f);
            foreach(var r in replacement.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(m=>stone).ToArray();
        }
        var reactor=root.transform.Cast<Transform>().First(t=>t.name=="05 / Reactor chamber stone monuments");
        foreach(var t in reactor.Cast<Transform>()) {
            if(t.name.StartsWith("Statue_Horse") || t.name.StartsWith("Pedestal")) {
                Undo.RecordObject(t,"Move monument inside chamber");var p=t.position;p.x=p.x<42?33:51;p.z=92;t.position=p;
            }
            if(t.name.StartsWith("banner_thin_blue")) {
                Undo.RecordObject(t,"Align rear banners");var p=t.position;p.x=p.x<42?39:45;p.z=98.3f;t.position=p;
            }
        }
        var archPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Decoration/Quaternius_Ruins_Selected/Arch_Round.prefab");
        foreach(var pos in new[]{new Vector3(0,0,5),new Vector3(42,0,71)}){
            if(root.transform.Cast<Transform>().Any(t=>t.name=="Stone transition arch / "+pos))continue;
            var arch=(GameObject)PrefabUtility.InstantiatePrefab(archPrefab,root.transform);Undo.RegisterCreatedObjectUndo(arch,"Add stone transition arch");
            arch.name="Stone transition arch / "+pos;arch.transform.position=pos;arch.transform.localScale=Vector3.one*(6f/3.53f);
            foreach(var r in arch.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(m=>stone).ToArray();
            foreach(var mf in arch.GetComponentsInChildren<MeshFilter>()){var col=mf.gameObject.AddComponent<MeshCollider>();col.sharedMesh=mf.sharedMesh;}
        }
        Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return "Replaced frame cover with stone barriers; added two open arches; moved monuments inside chamber.";
    }
}
