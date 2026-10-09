using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class FixDungeonTorchMounts
{
    public static string Main(){
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Open main scene outside Play Mode.");
        var root=scene.GetRootGameObjects().First(g=>g.name=="09 / Dungeon decoration");
        var walls=scene.GetRootGameObjects().First(g=>g.name=="07 / Modular walls").transform;
        var lamps=root.GetComponentsInChildren<Transform>().Where(t=>t.name=="Mr Scientist wall lamp").ToArray();
        if(lamps.Length!=18)throw new Exception("Expected 18 wall lamps, found "+lamps.Length);
        var project=Path.GetDirectoryName(Application.dataPath);var backup=Path.Combine(project,"Tools/SceneSnapshots/SkybreakMain_before_lamp_mount_fix_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity");
        if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup failed");
        Physics.SyncTransforms();var report=new List<string>{"Existing torch mounting fixes; replacement enclosed-lantern design awaits approval.","Backup: "+backup};
        foreach(var lamp in lamps){
            // In this source mesh the flame projects toward local +Z. Old yaw was reversed.
            var wallward=lamp.forward;
            var hit=Physics.RaycastAll(lamp.position+Vector3.up*.75f,wallward,4,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>h.collider.transform.IsChildOf(walls)).OrderBy(h=>h.distance).FirstOrDefault();
            if(hit.collider==null)throw new Exception("No wall found for lamp at "+lamp.position);
            Undo.RecordObject(lamp,"Correct torch mount");
            var inward=hit.normal;inward.y=0;inward.Normalize();lamp.rotation=Quaternion.LookRotation(inward,Vector3.up);
            var meshFilters=lamp.GetComponentsInChildren<MeshFilter>().Where(f=>!f.name.StartsWith("Flame"));
            float rear=meshFilters.SelectMany(f=>f.sharedMesh.vertices.Select(v=>Vector3.Dot(f.transform.TransformPoint(v)-hit.point,inward))).Min();
            lamp.position+=inward*(.018f-rear);
            var flame=lamp.GetComponentsInChildren<Renderer>().First(r=>r.name.StartsWith("Flame"));
            foreach(var light in lamp.GetComponentsInChildren<Light>()){
                Undo.RecordObject(light.transform,"Align light with flame");light.transform.position=flame.bounds.center+inward*.20f;
                light.range=5;light.intensity=1.8f;light.shadows=LightShadows.None;
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(lamp);
            lamp.name="Mr Scientist wall lamp / mounted inward";
            report.Add(lamp.position.ToString("F3")+": backing surface aligned 0.018m in front of wall; flame toward interior.");
        }
        Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllLines(Path.Combine(project,"Docs/GV/LevelDesign/Reports/Decoration/LampMountFix.txt"),report);
        return "Fixed 18 torch orientations and wall mounts; existing lights aligned with flame tips. Main scene saved. Backup: "+backup;
    }
}
