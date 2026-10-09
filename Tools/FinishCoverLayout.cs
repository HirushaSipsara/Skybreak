using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class FinishCoverLayout
{
    public static string Main()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Open main map outside Play Mode.");
        var root=scene.GetRootGameObjects().First(g=>g.name=="09 / Dungeon decoration");
        var stations=root.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("COVER ")).ToArray();
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Environment/StoneKit/Wall_Textured_Floor2_Body.mat");
        foreach(var station in stations.Where(t=>t.name.Contains("Low stone")))
        {
            if(station.Find("Solid stone cover infill")!=null)continue;
            var visual=station.Find("barrier_column");
            var panel=GameObject.CreatePrimitive(PrimitiveType.Cube);Undo.RegisterCreatedObjectUndo(panel,"Fill solid cover panels");
            panel.name="Solid stone cover infill";panel.transform.SetParent(station);
            panel.transform.SetPositionAndRotation(station.position+Vector3.up*.76f,visual.rotation);
            panel.transform.localScale=new Vector3(station.name.Contains("COVER T")?2.2f:3.5f,1.45f,.76f);
            panel.GetComponent<Renderer>().sharedMaterial=material;
        }
        Physics.SyncTransforms();
        var report=new List<string>{"Cover ray obstruction test. Low cover at 0.8m, tall cover at 2.0m; tests front and side rays. Combat projectile integration remains separate."};
        int passed=0;
        foreach(var station in stations)
        {
            bool low=station.name.Contains("Low stone");
            var origin=station.position+Vector3.up*(low?.8f:2f);
            var forward=low?station.Find("barrier_column").forward:Vector3.forward;
            var side=Vector3.Cross(Vector3.up,forward);
            bool front=Physics.RaycastAll(origin-forward*5,forward,10,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.transform.IsChildOf(station));
            bool flank=Physics.RaycastAll(origin-side*5,side,10,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.transform.IsChildOf(station));
            if(front&&flank)passed++;
            report.Add(station.name+": front="+front+", side="+flank+"; position="+station.position.ToString("F2"));
        }
        report.Add("Passed "+passed+" / "+stations.Length);
        File.WriteAllLines(Path.Combine(Path.GetDirectoryName(Application.dataPath),"Docs/GV/LevelDesign/Reports/Decoration/CoverCollisionChecks.txt"),report);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        if(passed!=24)throw new Exception("Cover validation failed: "+passed+"/24");
        return "24/24 cover positions block front and side test rays; low barricades now have solid textured stone infill. Main map saved.";
    }
}
