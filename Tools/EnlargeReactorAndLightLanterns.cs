using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class EnlargeReactorAndLightLanterns
{
    public static string Main(){
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Open main map outside Play Mode.");
        var root=scene.GetRootGameObjects().First(g=>g.name=="09 / Dungeon decoration");
        var shrine=root.GetComponentsInChildren<Transform>().Single(t=>t.name=="AuroraReactorShrine");
        var lanterns=root.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Vault wall lantern ")).ToArray();
        if(lanterns.Length!=18)throw new Exception("Expected 18 lanterns");
        var pedestals=root.GetComponentsInChildren<Transform>().Where(t=>t.name=="CrystalPedestal" && (Vector3.Distance(t.position,new Vector3(38.5f,0,95))<.1f||Vector3.Distance(t.position,new Vector3(45.5f,0,95))<.1f)).ToArray();
        if(pedestals.Length!=2)throw new Exception("Expected the two rear pedestals from the screenshot");
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Environment/Decoration/Custom/AuroraArchitecture/AmberCore.mat");if(material==null)throw new Exception("Missing amber material");
        var project=Path.GetDirectoryName(Application.dataPath);var backup=Path.Combine(project,"Tools/SceneSnapshots/SkybreakMain_before_larger_reactor_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity");Directory.CreateDirectory(Path.GetDirectoryName(backup));
        if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup failed");
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Larger reactor and all glowing lanterns");
        foreach(var pedestal in pedestals)Undo.DestroyObjectImmediate(pedestal.gameObject);
        Undo.RecordObject(shrine,"Enlarge reactor");shrine.localScale=Vector3.one*1.35f;PrefabUtility.RecordPrefabInstancePropertyModifications(shrine);
        Undo.RecordObject(material,"Brighten lantern cores");material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",new Color(1,.59f,.15f)*2.4f);EditorUtility.SetDirty(material);
        foreach(var lantern in lanterns){
            var core=lantern.GetComponentsInChildren<Renderer>().Single(r=>r.name=="Lantern_Core");
            var light=lantern.GetComponentInChildren<Light>();
            if(light==null){var go=new GameObject("Warm local glow / no realtime shadows");Undo.RegisterCreatedObjectUndo(go,"Light every lantern");go.transform.SetParent(lantern,false);light=Undo.AddComponent<Light>(go);}
            Undo.RecordObject(light,"Light every lantern");Undo.RecordObject(light.transform,"Position lamp glow");
            light.transform.position=core.bounds.center+lantern.forward*.1f;light.enabled=true;light.type=LightType.Point;light.color=new Color(1,.58f,.24f);light.range=5;light.intensity=1.8f;light.shadows=LightShadows.None;
        }
        Undo.CollapseUndoOperations(undo);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed");
        Physics.SyncTransforms();var rs=shrine.GetComponentsInChildren<Renderer>();var bounds=rs[0].bounds;foreach(var renderer in rs.Skip(1))bounds.Encapsulate(renderer.bounds);
        var report="Reactor scale: 1.35 (35% larger); bounds metres: "+bounds.size+"\nRemoved two separate rear crystal pedestals at (38.5,0,95) and (45.5,0,95); reactor's own piers retained.\nLanterns: "+lanterns.Length+"; enabled lantern point lights: "+lanterns.Count(t=>t.GetComponentInChildren<Light>()!=null && t.GetComponentInChildren<Light>().enabled)+"; no realtime shadows.\nRemaining crystal pedestals: "+root.GetComponentsInChildren<Transform>().Count(t=>t.name=="CrystalPedestal")+"\nBackup: "+backup;
        File.WriteAllText(Path.Combine(project,"Docs/GV/LevelDesign/Reports/Decoration/LargerReactorAndLanterns.txt"),report);
        Selection.activeGameObject=shrine.gameObject;SceneView.lastActiveSceneView?.FrameSelected();return report;
    }
}
