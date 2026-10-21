using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Skybreak;
using Skybreak.Descent;
using Skybreak.Player;
using Skybreak.Warden;

public static class InstallTeamMapAssets
{
    const string ReportDir = "Docs/IS/CrystalEchoWarden/MainMap";
    static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    static void Folder(string path) { Directory.CreateDirectory(path); }
    static Material ConvertMaterial(Material source, string group)
    {
        string name = source != null ? source.name : "Default";
        string key = group + "_" + name;
        if (materials.TryGetValue(key, out var cached)) return cached;
        string path = "Assets/Materials/Characters/TeamIntegration/" + key + ".mat";
        var result = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (result == null)
        {
            result = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = key };
            Color color = source != null && source.HasProperty("_Color") ? source.GetColor("_Color") : new Color(.1f, .3f, .4f);
            if (source != null && source.HasProperty("_BaseColor")) color = source.GetColor("_BaseColor");
            result.SetColor("_BaseColor", color);
            result.SetFloat("_Smoothness", .3f);
            string lower = name.ToLowerInvariant();
            if (lower.Contains("cyan") || lower.Contains("energy") || lower.Contains("amber"))
            {
                result.EnableKeyword("_EMISSION");
                result.SetColor("_EmissionColor", lower.Contains("amber") ? new Color(1,.35f,.03f) : new Color(.12f,.85f,1)*1.1f);
            }
            AssetDatabase.CreateAsset(result, path);
        }
        materials[key] = result;
        return result;
    }
    static void MapMaterials(GameObject root, string group)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterials = r.sharedMaterials.Select(m => ConvertMaterial(m, group)).ToArray();
            r.lightProbeUsage = LightProbeUsage.BlendProbes;
            GameObjectUtility.SetStaticEditorFlags(r.gameObject, 0);
        }
    }
    static void ActorLayer(GameObject root)
    { foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 8; }
    static Transform Marker(string name, Transform parent, Vector3 local)
    { var t = new GameObject(name).transform; t.SetParent(parent,false);t.localPosition=local;return t; }

    static GameObject Carbine()
    {
        const string path="Assets/Prefabs/Player/ArcRescueCarbine.prefab";
        var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Custom/ArcRescueCarbine/ArcRescueCarbine.fbx");
        if(model==null)throw new Exception("Carbine integration export missing.");
        var root=new GameObject("Arc Rescue Carbine");
        var magazine=Marker("Magazine",root.transform,new Vector3(0,-.14f,.08f));
        foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
        {
            var go=new GameObject(filter.name);bool mag=filter.name.StartsWith("Magazine__");
            go.transform.SetParent(mag?magazine:root.transform,false);
            var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.name=filter.name;
            var matrix=filter.transform.localToWorldMatrix;
            Vector3 offset=mag?magazine.localPosition:Vector3.zero;
            mesh.vertices=mesh.vertices.Select(v=>matrix.MultiplyPoint3x4(v)-offset).ToArray();
            var normalMatrix=matrix.inverse.transpose;
            mesh.normals=mesh.normals.Select(n=>normalMatrix.MultiplyVector(n).normalized).ToArray();
            mesh.RecalculateBounds();mesh.RecalculateTangents();
            string meshPath="Assets/Models/Custom/ArcRescueCarbine/Meshes/"+filter.name+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(existing==null)AssetDatabase.CreateAsset(mesh,meshPath);
            else{EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial=ConvertMaterial(filter.GetComponent<Renderer>().sharedMaterial,"Carbine");
        }
        Marker("Muzzle",root.transform,new Vector3(0,0,.565f));
        Marker("Right grip",root.transform,new Vector3(0,-.16f,-.14f));
        Marker("Left grip",root.transform,new Vector3(-.06f,-.08f,.18f));
        ActorLayer(root);
        try{return PrefabUtility.SaveAsPrefabAsset(root,path);}
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
    static GameObject WardenPrefab()
    {
        var root=new GameObject("Crystal Echo Warden");
        var body=root.AddComponent<CharacterController>();body.radius=.62f;body.height=1.5f;body.center=Vector3.up*.2f;body.stepOffset=.1f;
        root.AddComponent<WardenMotor>();root.AddComponent<WardenPerception>();
        var brain=root.AddComponent<WardenBrain>();brain.showDebug=false;brain.patrolRadius=6;
        var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/CrystalEchoWarden/CrystalEchoWarden.fbx");
        var visual=(GameObject)PrefabUtility.InstantiatePrefab(model);visual.transform.SetParent(root.transform,false);visual.name="Crystal Echo Warden visual";
        MapMaterials(visual,"EchoWarden");
        var animation=root.AddComponent<WardenAnimation>();animation.brain=brain;animation.visual=visual.transform;
        var fx=root.AddComponent<WardenPresentation>();fx.brain=brain;fx.visual=visual.transform;
        fx.core=visual.GetComponentsInChildren<Renderer>().FirstOrDefault(r=>r.name.ToLowerInvariant().Contains("resonance"));
        var beam=new GameObject("Echo pulse").AddComponent<LineRenderer>();beam.transform.SetParent(root.transform,false);
        beam.positionCount=2;beam.useWorldSpace=true;beam.widthMultiplier=.045f;beam.enabled=false;
        var energy=visual.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).First(m=>m.name.ToLowerInvariant().Contains("cyan"));
        beam.sharedMaterial=energy;fx.beam=beam;
        ActorLayer(root);
        try{return PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/Enemies/CrystalEchoWarden.prefab");}
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
    static bool Dynamic(Collider c) => c.GetComponentInParent<Rigidbody>()!=null || c.GetComponentsInParent<MonoBehaviour>().Any(b=>b is IInteractable);
    static bool PermanentFree(Vector3 point)
    {return Physics.OverlapSphere(point+Vector3.up*.2f,.7f,~(1<<8),QueryTriggerInteraction.Ignore).All(Dynamic);}
    static bool PermanentSegment(Vector3 a,Vector3 b)
    {
        var delta=b-a;
        return Physics.SphereCastAll(a+Vector3.up*.2f,.7f,delta.normalized,delta.magnitude,~(1<<8),QueryTriggerInteraction.Ignore).All(h=>Dynamic(h.collider));
    }
    static void Graph(WardenMap map)
    {
        var nodes=new List<Vector3>();
        for(float x=-12.5f;x<=74;x+=3.5f)for(float z=-4;z<=98;z+=3.5f)
        {
            if(!NavMesh.SamplePosition(new Vector3(x,0,z),out var hit,.4f,NavMesh.AllAreas))continue;
            var node=hit.position+Vector3.up;
            if(PermanentFree(node)&&!nodes.Any(p=>(p-node).sqrMagnitude<.25f))nodes.Add(node);
        }
        if(nodes.Count<80||nodes.Count>256)throw new Exception("Unexpected graph node count: "+nodes.Count);
        var edges=new List<WardenMap.Connection>();
        for(int a=0;a<nodes.Count;a++)for(int b=a+1;b<nodes.Count;b++)
            if(Vector3.Distance(nodes[a],nodes[b])<5.1f&&PermanentSegment(nodes[a],nodes[b]))edges.Add(new WardenMap.Connection{a=a,b=b});
        map.nodes=nodes.ToArray();map.connections=edges.ToArray();map.Build();
        if(map.Graph==null)throw new Exception(map.ValidationError);
    }
    public static string Main()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Use main map in Edit Mode.");
        var arc=UnityEngine.Object.FindAnyObjectByType<DescentCharacterController>();
        if(arc.GetComponent<ArcImportedPresentation>()!=null)throw new Exception("Team assets are already installed.");
        foreach(var folder in new[]{"Assets/Materials/Characters/TeamIntegration","Assets/Prefabs/Enemies","Assets/Prefabs/Player","Assets/Models/Custom/ArcRescueCarbine/Meshes",ReportDir})Folder(folder);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var carbinePrefab=Carbine();var wardenPrefab=WardenPrefab();

        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Custom/ArcExplorer/ArcExplorer.fbx");
        var character=(GameObject)PrefabUtility.InstantiatePrefab(source);character.name="Sachini Arc Explorer";
        character.transform.SetParent(arc.visual,false);
        MapMaterials(character,"Arc");ActorLayer(character);
        var renderers=character.GetComponentsInChildren<Renderer>();
        var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        float factor=2.444f/bounds.size.y;
        character.transform.localScale*=factor;
        var gun=(GameObject)PrefabUtility.InstantiatePrefab(carbinePrefab);gun.transform.SetParent(arc.visual,false);
        gun.transform.localPosition=new Vector3(.13f,1.20f,.37f);
        foreach(var r in arc.visual.GetComponentsInChildren<Renderer>())
            if(!r.transform.IsChildOf(character.transform)&&!r.transform.IsChildOf(gun.transform))r.enabled=false;
        var presenter=arc.gameObject.AddComponent<ArcImportedPresentation>();
        presenter.modelRoot=character.transform;presenter.carbine=gun.transform;
        presenter.rightGrip=gun.transform.Find("Right grip");presenter.leftGrip=gun.transform.Find("Left grip");presenter.magazine=gun.transform.Find("Magazine");
        PrefabUtility.SaveAsPrefabAssetAndConnect(arc.gameObject,"Assets/Prefabs/Player/ArcExplorerIntegrated.prefab",InteractionMode.AutomatedAction);

        var services=new GameObject("19 / Crystal Echo Warden encounters");
        var map=services.AddComponent<WardenMap>();var noise=services.AddComponent<WardenNoiseBus>();
        Physics.SyncTransforms();Graph(map);
        var encounter=services.AddComponent<CrystalWardenEncounter>();encounter.player=arc;encounter.map=map;encounter.noise=noise;
        var pedestals=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Where(t=>t.name=="CrystalPedestal").ToArray();
        var targets=new[]{new Vector3(-10.3f,0,17.04f),new Vector3(10.3f,0,13.05f),new Vector3(31,0,53.98f),new Vector3(53,0,46)};
        var wardens=new List<WardenBrain>();var objectives=new List<WardenObjective>();
        foreach(var target in targets)
        {
            var pedestal=pedestals.OrderBy(t=>Vector3.Distance(t.position,target)).First();
            if(Vector3.Distance(pedestal.position,target)>.2f)throw new Exception("Marked pedestal not found: "+target);
            var objective=pedestal.gameObject.AddComponent<WardenObjective>();objective.noise=noise;objective.crystalVisual=pedestal.Find("Faceted crystal");
            pedestal.gameObject.AddComponent<WardenCrystalInteraction>().objective=objective;
            var desired=target+new Vector3(target.x<20?3:-3,1,-2);
            var spawn=map.nodes.Where(p=>map.ClearSegment(p,p)&&Vector3.Distance(p,target)>2.4f&&Vector3.Distance(p,target)<6.5f).OrderBy(p=>(p-desired).sqrMagnitude).First();
            var go=(GameObject)PrefabUtility.InstantiatePrefab(wardenPrefab,services.transform);
            go.name="Crystal Echo Warden / crystal "+(wardens.Count+1);
            go.transform.SetPositionAndRotation(spawn,Quaternion.Euler(0,180,0));
            var brain=go.GetComponent<WardenBrain>();brain.map=map;brain.objective=objective;
            go.GetComponent<WardenMotor>().map=map;
            var perception=go.GetComponent<WardenPerception>();perception.map=map;perception.noise=noise;
            PrefabUtility.RecordPrefabInstancePropertyModifications(brain);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.GetComponent<WardenMotor>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(perception);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            wardens.Add(brain);objectives.Add(objective);
        }
        encounter.wardens=wardens.ToArray();encounter.objectives=objectives.ToArray();
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        string report="Integrated Arc (8 skinned renderer groups, 18 bones), Rescue Carbine, and four Crystal Echo Wardens.\nGraph: "+map.nodes.Length+" nodes, "+map.connections.Length+" links.\n"+string.Join("\n",wardens.Select(w=>w.name+" at "+w.transform.position+" guarding "+w.objective.transform.position))+"\nExisting pedestal glow and particles retained. Runtime checks pending.";
        File.WriteAllText(ReportDir+"/Installation.txt",report);return report;
    }
}
