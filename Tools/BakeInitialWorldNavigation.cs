using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using Skybreak.PhysicsInteraction;
public static class BakeInitialWorldNavigation
{
    static NavMeshModifier Modifier(GameObject go,bool ignore,bool blocked){var m=go.GetComponent<NavMeshModifier>();if(m==null)m=go.AddComponent<NavMeshModifier>();m.ignoreFromBuild=ignore;m.overrideArea=blocked;m.area=1;m.applyToChildren=true;EditorUtility.SetDirty(m);return m;}
    public static string Main(){
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Use main scene in Edit Mode");
        var project=Path.GetDirectoryName(Application.dataPath);Directory.CreateDirectory(Path.Combine(project,"Assets/Navigation/NavMesh"));AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var settingsObject=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset").Single();var settings=new SerializedObject(settingsObject);var entries=settings.FindProperty("m_Settings");var names=settings.FindProperty("m_SettingNames");int index=-1;
        for(int i=0;i<names.arraySize;i++)if(names.GetArrayElementAtIndex(i).stringValue=="Skybreak Medium Robot")index=i;
        if(index<0){var created=NavMesh.CreateSettings();settings.Update();entries=settings.FindProperty("m_Settings");names=settings.FindProperty("m_SettingNames");for(int i=0;i<entries.arraySize;i++)if(entries.GetArrayElementAtIndex(i).FindPropertyRelative("agentTypeID").intValue==created.agentTypeID)index=i;}
        if(index<0)throw new Exception("Agent settings creation failed");
        var entry=entries.GetArrayElementAtIndex(index);entry.FindPropertyRelative("agentRadius").floatValue=.45f;entry.FindPropertyRelative("agentHeight").floatValue=2.44f;entry.FindPropertyRelative("agentClimb").floatValue=.30f;entry.FindPropertyRelative("agentSlope").floatValue=45;names.GetArrayElementAtIndex(index).stringValue="Skybreak Medium Robot";var agentType=entry.FindPropertyRelative("agentTypeID").intValue;settings.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(settingsObject);
        foreach(var root in scene.GetRootGameObjects()){
            if(root.name.StartsWith("08 /")||root.name.StartsWith("02 /")||root.name.StartsWith("06 /")||root.name.StartsWith("15 /")||root.name.StartsWith("16 /"))Modifier(root,true,false);
            if(root.name.StartsWith("07 /")||root.name.StartsWith("09 /")||root.name.StartsWith("10 /")||root.name.StartsWith("11 /"))Modifier(root,false,true);
        }
        var doors=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DungeonHoldingDoor>()).ToArray();
        foreach(var door in doors){
            var so=new SerializedObject(door);var gate=(Transform)so.FindProperty("gate").objectReferenceValue;var collider=(BoxCollider)so.FindProperty("gateCollider").objectReferenceValue;Modifier(gate.gameObject,true,false);
            var obstacle=gate.GetComponent<NavMeshObstacle>();if(obstacle==null)obstacle=gate.gameObject.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.center=collider.center;obstacle.size=collider.size;obstacle.carving=true;obstacle.carveOnlyStationary=false;obstacle.carvingMoveThreshold=.08f;EditorUtility.SetDirty(obstacle);
        }
        var go=scene.GetRootGameObjects().FirstOrDefault(g=>g.name.StartsWith("17 /"));if(go==null){go=new GameObject("17 / World navigation and lighting probes");Undo.RegisterCreatedObjectUndo(go,"Prepare world navigation");}
        var surface=go.GetComponent<NavMeshSurface>();if(surface==null)surface=go.AddComponent<NavMeshSurface>();surface.agentTypeID=agentType;surface.collectObjects=CollectObjects.Volume;surface.center=new Vector3(29,2,45);surface.size=new Vector3(110,18,130);surface.layerMask=~(1<<8);surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.ignoreNavMeshAgent=true;surface.ignoreNavMeshObstacle=true;surface.overrideVoxelSize=true;surface.voxelSize=.10f;surface.overrideTileSize=true;surface.tileSize=128;surface.minRegionArea=.5f;surface.buildHeightMesh=false;Physics.SyncTransforms();surface.BuildNavMesh();
        if(surface.navMeshData==null||NavMesh.CalculateTriangulation().indices.Length==0)throw new Exception("Navigation bake empty");
        var path="Assets/Navigation/NavMesh/SkybreakMain_InitialNavMesh.asset";var old=AssetDatabase.LoadAssetAtPath<NavMeshData>(path);if(old!=null){surface.RemoveData();EditorUtility.CopySerialized(surface.navMeshData,old);UnityEngine.Object.DestroyImmediate(surface.navMeshData);surface.navMeshData=old;surface.AddData();EditorUtility.SetDirty(old);}else AssetDatabase.CreateAsset(surface.navMeshData,path);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        var report="Baked main-scene NavMesh: "+(NavMesh.CalculateTriangulation().indices.Length/3)+" triangles.\nAgent type "+agentType+" / Skybreak Medium Robot: radius .45m, height 2.44m, climb .30m, slope 45 degrees. Original Humanoid settings unchanged.\nPhysics geometry, voxel .10m, tile 128, no automatic jump links. Floor/dais walkable; walls/cover/solid props not walkable. Player, visual layout guides, cavern background and small weathering props ignored.\n2 moving gates excluded from static bake and fitted with live carving box obstacles. Opening raises the blocker above agent height. Fixed rails remain solid.\nData: "+path+". No permanent agents added.";
        File.WriteAllText(Path.Combine(project,"Docs/GV/LevelDesign/Reports/WorldFinish/NavigationBake.txt"),report);return report;
    }
}
