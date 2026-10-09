using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class PrepareWorldLighting
{
    public static string Main(){
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Use main scene in Edit Mode");
        var project=Path.GetDirectoryName(Application.dataPath);Directory.CreateDirectory(Path.Combine(project,"Assets/Lighting/GameLevel"));AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        string settingsPath="Assets/Lighting/GameLevel/SkybreakMainLighting.lighting";var settings=AssetDatabase.LoadAssetAtPath<LightingSettings>(settingsPath);if(settings==null){settings=new LightingSettings{name="SkybreakMainLighting"};AssetDatabase.CreateAsset(settings,settingsPath);}
        settings.bakedGI=true;settings.realtimeGI=false;settings.autoGenerate=false;settings.lightmapper=LightingSettings.Lightmapper.ProgressiveGPU;settings.mixedBakeMode=MixedLightingMode.Shadowmask;settings.directionalityMode=LightmapsMode.NonDirectional;settings.lightmapResolution=12;settings.lightmapMaxSize=1024;settings.lightmapPadding=3;settings.compressLightmaps=true;settings.directSampleCount=32;settings.indirectSampleCount=64;settings.environmentSampleCount=32;settings.bounces=2;settings.ao=true;settings.aoMaxDistance=.5f;settings.aoExponentIndirect=1;settings.aoExponentDirect=0;settings.filteringMode=LightingSettings.FilterMode.Auto;Lightmapping.lightingSettings=settings;EditorUtility.SetDirty(settings);
        var roots=scene.GetRootGameObjects();var staticRenderers=new List<MeshRenderer>();
        foreach(var root in roots){
            bool staticRoot=root.name.StartsWith("01 /")||root.name.StartsWith("07 /")||root.name.StartsWith("09 /")||root.name.StartsWith("11 /")||root.name.StartsWith("12 /")||root.name.StartsWith("13 /")||root.name.StartsWith("14 /")||root.name.StartsWith("15 /")||root.name.StartsWith("16 /");
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>()){
                var flags=GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                if(staticRoot){flags|=StaticEditorFlags.ContributeGI|StaticEditorFlags.BatchingStatic;renderer.receiveGI=ReceiveGI.Lightmaps;staticRenderers.Add(renderer);}
                else{flags&=~StaticEditorFlags.ContributeGI;renderer.receiveGI=ReceiveGI.LightProbes;renderer.lightProbeUsage=LightProbeUsage.BlendProbes;}
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,flags);
                var so=new SerializedObject(renderer);var scale=so.FindProperty("m_ScaleInLightmap");if(scale!=null){scale.floatValue=root.name.StartsWith("15 /")?(renderer.name.StartsWith("04 /")?.04f:.12f):root.name.StartsWith("14 /")?.2f:root.name.StartsWith("16 /")?.2f:1f;so.ApplyModifiedPropertiesWithoutUndo();}EditorUtility.SetDirty(renderer);
            }
        }
        var supportFolder="Assets/Models/Environment/Dungeon/LightmapSupport/";Directory.CreateDirectory(Path.Combine(project,supportFolder));AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);var builtinCopies=new Dictionary<Mesh,Mesh>();
        foreach(var filter in staticRenderers.Select(r=>r.GetComponent<MeshFilter>()).Where(f=>f!=null&&f.sharedMesh!=null)){
            if(AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith("Assets/"))continue;
            if(!builtinCopies.TryGetValue(filter.sharedMesh,out var copy)){var path=supportFolder+filter.sharedMesh.name+"_LightmapMesh.asset";copy=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(copy==null){copy=UnityEngine.Object.Instantiate(filter.sharedMesh);copy.name=filter.sharedMesh.name+"_LightmapMesh";AssetDatabase.CreateAsset(copy,path);}builtinCopies.Add(filter.sharedMesh,copy);}filter.sharedMesh=copy;
        }
        var backupFolder=Path.Combine(project,"Tools/AssetSnapshots/BeforeLightmapUVs_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"));Directory.CreateDirectory(backupFolder);int native=0,imported=0;
        foreach(var modelPath in staticRenderers.Select(r=>r.GetComponent<MeshFilter>()).Where(f=>f!=null&&f.sharedMesh!=null).Select(f=>AssetDatabase.GetAssetPath(f.sharedMesh)).Where(p=>p.StartsWith("Assets/")).Distinct()){
            var importer=AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if(importer!=null){if(!importer.generateSecondaryUV){var meta=modelPath+".meta";File.Copy(Path.Combine(project,meta),Path.Combine(backupFolder,modelPath.Replace('/','_')+".meta"));importer.generateSecondaryUV=true;importer.SaveAndReimport();imported++;}continue;}
            foreach(var mesh in AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Mesh>())if(mesh.uv2.Length!=mesh.vertexCount){
                var snapshot=Path.Combine(backupFolder,modelPath.Replace('/','_'));File.Copy(Path.Combine(project,modelPath),snapshot,true);File.Copy(Path.Combine(project,modelPath+".meta"),snapshot+".meta",true);
                UnwrapParam.SetDefaults(out var param);param.packMargin=.025f;if(!Unwrapping.GenerateSecondaryUVSet(mesh,param))throw new Exception("UV2 unwrap failed "+modelPath);EditorUtility.SetDirty(mesh);native++;
            }
        }
        var lights=roots.SelectMany(g=>g.GetComponentsInChildren<Light>()).ToArray();foreach(var light in lights){
            if(light.name=="Warm skylight"){light.lightmapBakeType=LightmapBakeType.Mixed;light.shadows=LightShadows.Soft;}
            else{light.lightmapBakeType=LightmapBakeType.Baked;light.shadows=LightShadows.None;}EditorUtility.SetDirty(light);
        }
        var probeParent=roots.Single(g=>g.name.StartsWith("17 /"));var probeGroup=probeParent.GetComponentInChildren<LightProbeGroup>();if(probeGroup==null){var go=new GameObject("Light probes / moving characters and doors");go.transform.SetParent(probeParent.transform);probeGroup=go.AddComponent<LightProbeGroup>();}
        var positions=new List<Vector3>();var floor=roots.Single(g=>g.name.StartsWith("01 /"));Physics.SyncTransforms();
        foreach(var col in floor.GetComponentsInChildren<Collider>()){
            var p=col.bounds.center;if(((int)Mathf.Floor(p.x)%4+4)%4!=0||((int)Mathf.Floor(p.z)%4+4)%4!=0)continue;
            foreach(float height in new[]{.65f,1.75f,3.0f}){var world=new Vector3(p.x,height,p.z);if(!Physics.CheckSphere(world,.14f,~(1<<8),QueryTriggerInteraction.Ignore))positions.Add(probeGroup.transform.InverseTransformPoint(world));}
        }
        probeGroup.probePositions=positions.ToArray();probeGroup.dering=true;EditorUtility.SetDirty(probeGroup);
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.19f,.26f,.32f);RenderSettings.ambientEquatorColor=new Color(.12f,.17f,.20f);RenderSettings.ambientGroundColor=new Color(.065f,.08f,.085f);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        var report="Prepared "+staticRenderers.Count+" static mesh renderers for baked GI. Secondary UVs generated: "+native+" native mesh assets / "+imported+" FBX importers; GUIDs retained.\nLighting: 1 mixed directional light with dynamic shadows; "+(lights.Length-1)+" baked lights (20 points and cool directional fill).\nGPU progressive bake, 12 texels/metre, 1024 atlases, nondirectional compressed maps, 32 direct / 64 indirect samples, 2 bounces, automatic filtering, restrained indirect AO.\nCavern and tiny details use lower lightmap density.\nPlaced "+positions.Count+" light probes across floor areas at .65, 1.75 and 3m; points inside physical obstacles excluded. Moving player/doors use probes rather than static lightmaps.\nUV backup: "+backupFolder;
        File.WriteAllText(Path.Combine(project,"Docs/GV/LevelDesign/Reports/WorldFinish/LightingPreparation.txt"),report);return report;
    }
}
