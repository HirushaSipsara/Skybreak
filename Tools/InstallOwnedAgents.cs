using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using Skybreak.Combat;
using Skybreak.Movement;
using Skybreak.Presentation;
using Skybreak.AI;
using Skybreak.AI.RiftWarden;
using Skybreak.AI.Relay;

public static class InstallOwnedAgents
{
    const string BossModel="Assets/Models/Custom/RiftWarden/RiftWarden_Animated.fbx";
    static void Folder(string path){Directory.CreateDirectory(path);AssetDatabase.Refresh();}
    public static string ImportRig()
    {
        var importer=(ModelImporter)AssetImporter.GetAtPath(BossModel);
        importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.motionNodeName="Root";importer.optimizeGameObjects=false;
        importer.animationCompression=ModelImporterAnimationCompression.Off;
        var clips=importer.defaultClipAnimations;
        foreach(var c in clips){string n=c.name.Split('|').Last();c.loopTime=new[]{"RW_Idle","RW_CoreIdle","RW_Walk","RW_Run","RW_CoreWalk"}.Contains(n);c.keepOriginalPositionY=true;c.lockRootHeightY=true;}
        importer.clipAnimations=clips;importer.SaveAndReimport();return "Generic avatar created; five locomotion/idle loops configured; root motion kept in pose.";
    }
    static Material Material(string name,Color colour,bool glow=false)
    {
        string path="Assets/Materials/Characters/OwnedAgents/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",colour);m.SetFloat("_Smoothness",.55f);
        if(glow){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",colour*.7f);}return m;
    }
    static void Layer(GameObject go){foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=8;}
    static NavMeshMotor Motor(GameObject go,int type,float radius,float height,float speed)
    {
        var a=go.AddComponent<NavMeshAgent>();a.agentTypeID=type;a.radius=radius;a.height=height;a.speed=speed;a.acceleration=10;a.angularSpeed=200;a.autoRepath=true;a.obstacleAvoidanceType=ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        var motor=go.AddComponent<NavMeshMotor>();return motor;
    }
    static CombatSignals Signals(GameObject parent,Material material)
    {
        var go=new GameObject("Attack and support cues");go.transform.SetParent(parent.transform,false);var fx=go.AddComponent<CombatSignals>();
        foreach(bool ring in new[]{false,true}){var child=new GameObject(ring?"Warning circle":"Energy beam");child.transform.SetParent(go.transform,false);var line=child.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;line.widthMultiplier=ring?.07f:.045f;line.positionCount=ring?49:2;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;if(ring)fx.ring=line;else fx.line=line;}return fx;
    }
    static RiftDamagePart Part(RiftWardenBrain boss,Transform bone,SkinnedMeshRenderer renderer,RiftPart part)
    {
        var go=new GameObject(part+" damage target");go.transform.SetParent(bone,false);go.transform.position=renderer.bounds.center;go.transform.rotation=boss.transform.rotation;var boneScale=bone.lossyScale;go.transform.localScale=new Vector3(1/boneScale.x,1/boneScale.y,1/boneScale.z);
        var box=go.AddComponent<BoxCollider>();box.size=part==RiftPart.Core?new Vector3(.48f,.52f,.36f):renderer.bounds.size*.85f;
        var target=go.AddComponent<RiftDamagePart>();target.boss=boss;target.part=part;return target;
    }
    static GameObject BuildBoss(int type,Material fx)
    {
        var go=new GameObject("Rift Warden / owned boss");var brain=go.AddComponent<RiftWardenBrain>();brain.motor=Motor(go,type,.85f,3.5f,3.2f);brain.motor.GetComponent<NavMeshAgent>().updateRotation=true;
        var body=go.AddComponent<CapsuleCollider>();body.radius=.8f;body.height=3.4f;body.center=Vector3.up*1.7f;
        var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BossModel));visual.transform.SetParent(go.transform,false);visual.name="Rift Warden animated visual";
        var presenter=go.AddComponent<TimedClipPresenter>();brain.presenter=presenter;presenter.animator=visual.GetComponent<Animator>();if(presenter.animator==null)presenter.animator=visual.AddComponent<Animator>();presenter.animator.applyRootMotion=false;presenter.animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        presenter.clips=AssetDatabase.LoadAllAssetsAtPath(BossModel).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")).ToArray();
        Transform Bone(string n)=>visual.GetComponentsInChildren<Transform>().Single(t=>t.name==n);
        var rs=visual.GetComponentsInChildren<SkinnedMeshRenderer>();brain.leftArm=rs.Single(r=>r.name=="RW_Arm_L");brain.rightArm=rs.Single(r=>r.name=="RW_Arm_R");
        brain.leftTarget=Part(brain,Bone("UpperArm.L"),brain.leftArm,RiftPart.LeftArm);brain.rightTarget=Part(brain,Bone("UpperArm.R"),brain.rightArm,RiftPart.RightArm);brain.coreTarget=Part(brain,Bone("Core"),rs.Single(r=>r.name=="RW_Core"),RiftPart.Core);
        foreach(var r in rs){r.shadowCastingMode=ShadowCastingMode.On;r.lightProbeUsage=LightProbeUsage.BlendProbes;r.updateWhenOffscreen=true;}
        brain.signals=Signals(go,fx);Layer(go);return go;
    }
    static GameObject Piece(Transform parent,string name,PrimitiveType shape,Vector3 p,Vector3 scale,Material mat)
    {
        var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=scale;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=mat;return go;
    }
    static GameObject BuildRelay(Material fx)
    {
        var go=new GameObject("Relay / owned support companion");var brain=go.AddComponent<RelayBrain>();brain.motor=Motor(go,-1372625422,.35f,1.9f,6);brain.motor.GetComponent<NavMeshAgent>().updateRotation=false;
        var collider=go.AddComponent<CapsuleCollider>();collider.radius=.32f;collider.height=.6f;collider.center=Vector3.up*1.7f;
        var visual=new GameObject("Relay modular visual").transform;visual.SetParent(go.transform,false);visual.localPosition=Vector3.up*1.7f;brain.visual=visual;
        var cream=Material("RelayCream",new Color(.92f,.84f,.69f));var teal=Material("RelayTeal",new Color(.04f,.56f,.57f));var dark=Material("RelayDarkMetal",new Color(.065f,.11f,.14f));var cyan=Material("RelayCyanLens",Color.cyan,true);var orange=Material("RelayOrange",new Color(1,.38f,.08f));
        Piece(visual,"Cream body",PrimitiveType.Sphere,Vector3.zero,new Vector3(.7f,.45f,.55f),cream);
        Piece(visual,"Teal upper armour",PrimitiveType.Sphere,new Vector3(0,.10f,0),new Vector3(.71f,.31f,.56f),teal);
        Piece(visual,"Dark eye bezel",PrimitiveType.Cylinder,new Vector3(0,0,.26f),new Vector3(.32f,.035f,.32f),dark).transform.localRotation=Quaternion.Euler(90,0,0);
        Piece(visual,"Cyan lens",PrimitiveType.Sphere,new Vector3(0,0,.30f),new Vector3(.23f,.23f,.08f),cyan);
        foreach(float side in new[]{-1f,1f}){Piece(visual,"Orange pod socket",PrimitiveType.Cylinder,new Vector3(side*.36f,0,0),new Vector3(.14f,.06f,.14f),orange).transform.localRotation=Quaternion.Euler(0,0,90);Piece(visual,"Teal hover pod",PrimitiveType.Sphere,new Vector3(side*.46f,0,0),new Vector3(.20f,.38f,.31f),teal);}
        Piece(visual,"Hover thruster",PrimitiveType.Cylinder,new Vector3(0,-.24f,0),new Vector3(.19f,.05f,.19f),dark);
        Piece(visual,"Hover cyan core",PrimitiveType.Sphere,new Vector3(0,-.29f,0),new Vector3(.14f,.08f,.14f),cyan);
        Piece(visual,"Heal emitter",PrimitiveType.Cube,new Vector3(.21f,-.16f,.23f),new Vector3(.12f,.10f,.19f),orange);
        brain.signals=Signals(go,fx);Layer(go);return go;
    }
    public static string Main()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(EditorApplication.isPlaying||Lightmapping.isRunning||scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Open main scene in Edit Mode outside a bake");
        string backup="Tools/SceneSnapshots/SkybreakMain_before_owned_agents_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity";EditorSceneManager.SaveScene(scene,backup,true);
        if(scene.GetRootGameObjects().Any(g=>g.name=="18 / Student 1 owned agents"))throw new Exception("Owned agents already installed; preserve edits and inspect before reinstalling");
        Folder("Assets/Materials/Characters/OwnedAgents");Folder("Assets/Prefabs/Enemies/RiftWarden");Folder("Assets/Prefabs/Companions/Relay");
        var root=new GameObject("18 / Student 1 owned agents");Undo.RegisterCreatedObjectUndo(root,"Owned agents and boss navigation");
        var settings=NavMesh.CreateSettings();var settingsObject=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset").Single();var settingsSo=new SerializedObject(settingsObject);var entries=settingsSo.FindProperty("m_Settings");var names=settingsSo.FindProperty("m_SettingNames");
        for(int i=0;i<entries.arraySize;i++){var entry=entries.GetArrayElementAtIndex(i);if(entry.FindPropertyRelative("agentTypeID").intValue!=settings.agentTypeID)continue;entry.FindPropertyRelative("agentRadius").floatValue=.85f;entry.FindPropertyRelative("agentHeight").floatValue=3.5f;entry.FindPropertyRelative("agentClimb").floatValue=.3f;entry.FindPropertyRelative("agentSlope").floatValue=45;names.GetArrayElementAtIndex(i).stringValue="Skybreak Rift Warden";}
        settingsSo.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(settingsObject);
        var navgo=new GameObject("Rift Warden arena navigation / wider capsule");navgo.transform.SetParent(root.transform,false);var surface=navgo.AddComponent<NavMeshSurface>();surface.agentTypeID=settings.agentTypeID;surface.collectObjects=CollectObjects.Volume;surface.center=new Vector3(42,2,84.5f);surface.size=new Vector3(34,12,34);surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.layerMask=~(1<<8);surface.overrideVoxelSize=true;surface.voxelSize=.15f;surface.overrideTileSize=true;surface.tileSize=128;surface.BuildNavMesh();AssetDatabase.CreateAsset(surface.navMeshData,"Assets/Navigation/NavMesh/RiftWardenArena.asset");
        var fxpath="Assets/Materials/Characters/OwnedAgents/CombatCues.mat";var fx=new Material(Shader.Find("Universal Render Pipeline/Unlit"));fx.SetColor("_BaseColor",Color.white);AssetDatabase.CreateAsset(fx,fxpath);
        var boss=BuildBoss(settings.agentTypeID,fx);PrefabUtility.SaveAsPrefabAsset(boss,"Assets/Prefabs/Enemies/RiftWarden/RiftWarden.prefab");boss.transform.SetParent(root.transform,false);boss.transform.SetPositionAndRotation(new Vector3(42,.04f,90),Quaternion.Euler(0,180,0));
        var relay=BuildRelay(fx);PrefabUtility.SaveAsPrefabAsset(relay,"Assets/Prefabs/Companions/Relay/Relay.prefab");relay.transform.SetParent(root.transform,false);relay.transform.position=new Vector3(2,.04f,-4);
        var player=UnityEngine.Object.FindAnyObjectByType<Skybreak.Descent.DescentCharacterController>();var b=boss.GetComponent<RiftWardenBrain>();b.player=player.transform;relay.GetComponent<RelayBrain>().player=player.transform;b.returnPoint=UnityEngine.Object.FindAnyObjectByType<Skybreak.World.FinalTeleportVisual>();
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);return "Boss and Relay prefabs installed. Separate wider boss NavMesh baked; existing regular NavMesh retained. Backup: "+backup;
    }
}
