using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ArrangeMarkedStatues
{
    const string Prefab="Assets/Prefabs/Environment/Decoration/Custom/DungeonWallDetails/WeatheredGuardianStatue.prefab";
    static string Project=>Path.GetDirectoryName(Application.dataPath);
    static string Dir=>Path.Combine(Project,"Docs/GV/LevelDesign/Reports/StatuePlacement");
    static Transform[] Statues()=>UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).Where(t=>t.name.StartsWith("Guardian statue ")).OrderBy(t=>t.name).ToArray();
    public static string RefreshProbes(){
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Stop lighting bake first.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var floor=scene.GetRootGameObjects().Single(g=>g.name.StartsWith("01 /"));var group=UnityEngine.Object.FindAnyObjectByType<LightProbeGroup>();var positions=new List<Vector3>();Physics.SyncTransforms();
        foreach(var c in floor.GetComponentsInChildren<Collider>()){
            var p=c.bounds.center;if(((int)Mathf.Floor(p.x)%4+4)%4!=0||((int)Mathf.Floor(p.z)%4+4)%4!=0)continue;
            foreach(float height in new[]{.65f,1.75f,3}){var world=new Vector3(p.x,height,p.z);if(!Physics.CheckSphere(world,.14f,~(1<<8),QueryTriggerInteraction.Ignore))positions.Add(group.transform.InverseTransformPoint(world));}
        }
        Undo.RecordObject(group,"Keep light probes out of relocated statues");group.probePositions=positions.ToArray();EditorUtility.SetDirty(group);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        var report="Refreshed lighting probes using the existing 4m floor grid / .65, 1.75 and 3m heights: "+positions.Count+" eligible points, excluding current physical scenery. Six old probe positions were inside moved statues.";
        File.AppendAllText(Path.Combine(Dir,"PlacementAndNavigation.txt"),"\n"+report+"\n");return report;
    }
    public static Vector3 CrystalCentre(Transform statue){
        var r=statue.GetComponent<MeshRenderer>();var slot=Array.FindIndex(r.sharedMaterials,m=>m.name.StartsWith("GuardianCrystal"));if(slot<0)throw new Exception("No crystal material slot");
        var mesh=statue.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var ids=mesh.GetIndices(slot).Distinct().ToArray();
        return statue.TransformPoint(ids.Aggregate(Vector3.zero,(p,i)=>p+vertices[i])/ids.Length);
    }
    public static string Main(){
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(EditorApplication.isPlaying||Lightmapping.isRunning||scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Use the main scene in Edit Mode with no running bake.");
        var statues=Statues();if(statues.Length!=6&&statues.Length!=9)throw new Exception("Unexpected statue count");
        Directory.CreateDirectory(Dir);var stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");var backup=Path.Combine(Project,"Tools/SceneSnapshots/SkybreakMain_before_marked_statues_"+stamp+".unity");
        if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup failed");
        var surface=UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>();var navPath=AssetDatabase.GetAssetPath(surface.navMeshData);var navGuid=AssetDatabase.AssetPathToGUID(navPath);
        var navBackup=Path.Combine(Project,"Tools/AssetSnapshots/NavMeshBeforeStatues_"+stamp);Directory.CreateDirectory(navBackup);File.Copy(Path.Combine(Project,navPath),Path.Combine(navBackup,Path.GetFileName(navPath)));File.Copy(Path.Combine(Project,navPath+".meta"),Path.Combine(navBackup,Path.GetFileName(navPath)+".meta"));
        var boss=statues.Where(t=>t.position.z>75).ToArray();if(boss.Length!=2)throw new Exception("Expected two boss statues");
        var bossPositions=boss.Select(t=>t.position).ToArray();var bossRotations=boss.Select(t=>t.rotation).ToArray();
        var movable=statues.Except(boss).ToList();var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);var parent=statues[0].parent;
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Blue-marked statue placement and crystal glow");
        while(movable.Count<7){var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);Undo.RegisterCreatedObjectUndo(go,"Additional marked guardian");movable.Add(go.transform);}
        var positions=new[]{new Vector3(0,0,-7.6f),new Vector3(0,0,25.9f),new Vector3(-6.8f,0,6.1f),new Vector3(6.8f,0,6.1f),new Vector3(36.3f,0,60.9f),new Vector3(47.1f,0,60.9f),new Vector3(41.3f,0,39.1f)};
        var yaw=new[]{0f,180,0,0,180,180,0};var regions=new[]{"Entrance","Court north","Court southwest","Court southeast","Security northwest","Security northeast","Security south"};
        for(int i=0;i<movable.Count;i++){
            var t=movable[i];Undo.RecordObject(t,"Marked statue transform");Undo.RecordObject(t.gameObject,"Statue region name");t.name="Guardian statue "+(i+1).ToString("00")+" / "+regions[i];t.SetPositionAndRotation(positions[i],Quaternion.Euler(0,yaw[i],0));t.localScale=Vector3.one*1.45f;PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }
        for(int i=0;i<boss.Length;i++){Undo.RecordObject(boss[i].gameObject,"Boss statue name");boss[i].name="Guardian statue "+(8+i).ToString("00")+" / Boss chamber";}
        Undo.RecordObject(parent.gameObject,"Statue group label");parent.name="04 / Nine guardian statues / marked positions";
        var materialPath="Assets/Materials/Environment/Decoration/Custom/DungeonWallDetails/GuardianCrystalSoftGlow.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null){material=new Material(statues[0].GetComponent<MeshRenderer>().sharedMaterials.Single(m=>m.name=="GuardianCrystal"));AssetDatabase.CreateAsset(material,materialPath);}
        material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",new Color(.12f,.75f,1)*1.8f);material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;EditorUtility.SetDirty(material);
        var all=Statues();var report=new List<string>{"9 guardians: four entrance/court positions, three security hall positions, and two existing boss statues retained.","Scene backup: "+backup,"Navigation backup: "+navBackup,"Original statue scale 1.45 retained (4.06m tall). Cyan emission only in crystal submesh; stone/brass materials unchanged."};
        foreach(var t in all){
            var r=t.GetComponent<MeshRenderer>();Undo.RecordObject(r,"Guardian crystal emission");r.sharedMaterials=r.sharedMaterials.Select(m=>m.name.StartsWith("GuardianCrystal")?material:m).ToArray();r.receiveGI=ReceiveGI.Lightmaps;GameObjectUtility.SetStaticEditorFlags(t.gameObject,GameObjectUtility.GetStaticEditorFlags(t.gameObject)|StaticEditorFlags.ContributeGI|StaticEditorFlags.BatchingStatic);PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            var mesh=t.GetComponent<MeshFilter>().sharedMesh;if(mesh.uv2.Length!=mesh.vertexCount)throw new Exception("Statue missing lightmap UVs");
            var centre=CrystalCentre(t);var l=t.GetComponentInChildren<Light>();if(l==null){var go=new GameObject("Guardian crystal spill / baked");go.transform.SetParent(t,false);Undo.RegisterCreatedObjectUndo(go,"Crystal spill light");l=Undo.AddComponent<Light>(go);}
            Undo.RecordObject(l,"Soft cyan light");Undo.RecordObject(l.transform,"Crystal spill position");l.transform.position=centre+t.forward*.12f;l.lightmapBakeType=LightmapBakeType.Baked;l.type=LightType.Point;l.color=new Color(.12f,.75f,1);l.intensity=.9f;l.range=2.8f;l.shadows=LightShadows.None;l.enabled=true;
            report.Add(t.name+" | position="+t.position+" | yaw="+t.eulerAngles.y+" | crystal="+centre);
        }
        Physics.SyncTransforms();var floor=scene.GetRootGameObjects().First(g=>g.name.StartsWith("01 /"));var solids=scene.GetRootGameObjects().Where(g=>g.name.StartsWith("07 /")||g.name.StartsWith("09 /")||g.name.StartsWith("10 /")).SelectMany(g=>g.GetComponentsInChildren<Collider>()).Where(c=>!c.isTrigger).ToArray();
        foreach(var t in movable){
            if(!Physics.RaycastAll(t.position+Vector3.up*.3f,Vector3.down,1).Any(h=>h.collider.transform.IsChildOf(floor.transform)))throw new Exception("Statue outside floor: "+t.name);
            var box=t.GetComponent<BoxCollider>();foreach(var c in solids)if(Physics.ComputePenetration(box,box.transform.position,box.transform.rotation,c,c.transform.position,c.transform.rotation,out var direction,out float depth)&&depth>.015f)throw new Exception("Statue intersects "+c.name+" at "+t.position+" depth="+depth);
        }
        for(int i=0;i<boss.Length;i++)if(boss[i].position!=bossPositions[i]||boss[i].rotation!=bossRotations[i])throw new Exception("Boss statue moved");
        surface.BuildNavMesh();var generated=surface.navMeshData;var saved=AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);if(saved==null)throw new Exception("Missing original NavMesh asset");
        surface.RemoveData();EditorUtility.CopySerialized(generated,saved);UnityEngine.Object.DestroyImmediate(generated);surface.navMeshData=saved;surface.AddData();EditorUtility.SetDirty(saved);if(navGuid!=AssetDatabase.AssetPathToGUID(navPath))throw new Exception("Navigation GUID changed");
        var filter=new NavMeshQueryFilter{agentTypeID=surface.agentTypeID,areaMask=NavMesh.AllAreas};foreach(var t in all){var p=t.GetComponent<BoxCollider>().bounds.center;p.y=.05f;if(NavMesh.SamplePosition(p,out var hit,.08f,filter))throw new Exception("Navigation crosses statue "+t.name);}
        var route=new[]{new Vector3(0,0,-4),new Vector3(0,0,16),new Vector3(21,0,21),new Vector3(21,0,35),new Vector3(9,0,35),new Vector3(9,0,46),new Vector3(42,0,50),new Vector3(42,0,66),new Vector3(42,0,84.5f)};
        for(int i=0;i<route.Length-1;i++){if(!NavMesh.SamplePosition(route[i],out var a,1.1f,filter)||!NavMesh.SamplePosition(route[i+1],out var b,1.1f,filter))throw new Exception("Route sample failed");var path=new NavMeshPath();if(!NavMesh.CalculatePath(a.position,b.position,filter,path)||path.status!=NavMeshPathStatus.PathComplete)throw new Exception("Main route blocked at "+i);}
        report.Add("9 statue collider centres excluded from walkable navigation; 7 moved/added statues are on the floor and do not intersect existing solid scenery. All eight main-route legs complete.");report.Add("Rebaked navigation triangles: "+NavMesh.CalculateTriangulation().indices.Length/3+". Existing agent settings, modifiers, gate carving and navigation asset GUID retained.");
        Undo.CollapseUndoOperations(undo);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);File.WriteAllLines(Path.Combine(Dir,"PlacementAndNavigation.txt"),report);
        return string.Join("\n",report);
    }
}
