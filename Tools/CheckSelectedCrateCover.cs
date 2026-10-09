using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class CheckSelectedCrateCover
{
    public static string Main(){
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Open main map outside Play Mode.");
        var root=scene.GetRootGameObjects().First(g=>g.name=="09 / Dungeon decoration");
        var stations=root.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("COVER ")).ToArray();
        foreach(var station in stations){
            var ground=station.Cast<Transform>().Where(t=>t.position.y<.2f).ToArray();
            if(ground.Length!=2)continue;
            var direction=(ground[1].position-ground[0].position).normalized;float offset=station.name.StartsWith("COVER T")?.55f:.8f;
            Undo.RecordObjects(ground,"Close crate cover seam");ground[0].position=station.position-direction*(offset-.005f);ground[1].position=station.position+direction*(offset-.005f);
            foreach(var g in ground)PrefabUtility.RecordPrefabInstancePropertyModifications(g);
        }
        Physics.SyncTransforms();var lines=new List<string>{"Selected crate layout: front and side rays through cover at 0.8m; tall stacks additionally checked at 2m. Geometry check only; combat scripts must respect colliders."};int passed=0;
        foreach(var s in stations){
            var origin=s.position+Vector3.up*.8f;
            bool front=Physics.RaycastAll(origin-Vector3.forward*4,Vector3.forward,8,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.transform.IsChildOf(s));
            bool side=Physics.RaycastAll(origin-Vector3.right*4,Vector3.right,8,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.transform.IsChildOf(s));
            bool tall=s.Cast<Transform>().Any(t=>t.position.y>1);
            bool upper=!tall||Physics.RaycastAll(s.position+new Vector3(0,2,-4),Vector3.forward,8,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.transform.IsChildOf(s));
            if(front&&side&&upper)passed++;lines.Add(s.name+": front="+front+", side="+side+", tall="+tall+", upper="+upper);
        }
        lines.Add("Passed "+passed+" / "+stations.Length);
        File.WriteAllLines(Path.Combine(Path.GetDirectoryName(Application.dataPath),"Docs/GV/LevelDesign/Reports/Decoration/CoverCollisionChecks.txt"),lines);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        if(passed!=24)throw new Exception("Crate cover check failed: "+passed+"/24");return "24/24 crate cover positions block front and side rays; tall stacks block a 2m ray. Small gaps between paired low crates closed.";
    }
}
