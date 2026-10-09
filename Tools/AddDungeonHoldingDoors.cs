using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Skybreak.PhysicsInteraction;

public static class AddDungeonHoldingDoors
{
    const string Folder="Assets/Prefabs/InteractiveObjects/Doors/";
    static GameObject Part(string name,Transform parent,Vector3 position,Vector3 size,Material material){
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());var filter=go.GetComponent<MeshFilter>();
        var bevel=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/Environment/Dungeon/Meshes/BeveledBlock.asset");if(bevel!=null)filter.sharedMesh=bevel;
        go.GetComponent<Renderer>().sharedMaterial=material;return go;
    }
    static Material Mat(string name,Color color,float metallic){
        var path="Assets/Materials/InteractiveObjects/Doors/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",.35f);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
    }
    public static string Main(){
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/GameLevel/SkybreakMain.unity")throw new Exception("Open main map in Edit Mode");
        var project=Path.GetDirectoryName(Application.dataPath);Directory.CreateDirectory(Path.Combine(project,Folder));Directory.CreateDirectory(Path.Combine(project,"Assets/Materials/InteractiveObjects/Doors"));AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var markers=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).Where(t=>t.name.StartsWith("DoorOpening_")).ToArray();if(markers.Length!=2)throw new Exception("Expected two door markers");
        var player=scene.GetRootGameObjects().First(g=>g.name.StartsWith("08 / ARC")).GetComponentInChildren<CharacterController>();if(player==null)throw new Exception("Missing explorer");
        if(scene.GetRootGameObjects().Any(g=>g.name=="10 / Holding-room dungeon gates"))throw new Exception("Doors already installed; preserve current instances.");
        var backup=Path.Combine(project,"Tools/SceneSnapshots/SkybreakMain_before_holding_doors_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity");Directory.CreateDirectory(Path.GetDirectoryName(backup));if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Backup failed");
        var iron=Mat("HoldingGate_Iron",new Color(.13f,.20f,.22f),.65f);var brass=Mat("HoldingGate_Brass",new Color(.58f,.39f,.19f),.55f);
        var preview=EditorSceneManager.NewPreviewScene();var model=new GameObject("DungeonHoldingGate");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(model,preview);
        GameObject prefab;
        try{
            var gate=new GameObject("Gate / moving grille");gate.transform.SetParent(model.transform,false);
            // All rails stay within the existing three-metre clear opening.
            for(int side=-1;side<=1;side+=2){
                var rail=Part("Fixed guide rail",model.transform,new Vector3(side*1.46f,1.5f,0),new Vector3(.08f,3,.30f),iron);var col=rail.AddComponent<BoxCollider>();col.center=Vector3.zero;col.size=Vector3.one;
                Part("Guide foot",model.transform,new Vector3(side*1.46f,.15f,0),new Vector3(.08f,.30f,.34f),brass);
            }
            Part("Lintel mechanism / fixed",model.transform,new Vector3(0,3.03f,0),new Vector3(3,.20f,.42f),iron);
            Part("Lintel brass strip",model.transform,new Vector3(0,3.03f,.225f),new Vector3(2.8f,.045f,.025f),brass);
            Part("Lower armoured panel",gate.transform,new Vector3(0,.34f,0),new Vector3(2.82f,.58f,.20f),iron);
            Part("Lower brass reinforcement",gate.transform,new Vector3(0,.35f,.115f),new Vector3(2.77f,.07f,.04f),brass);
            for(int i=0;i<9;i++)Part("Iron grille bar "+(i+1),gate.transform,new Vector3(-1.28f+i*.32f,1.78f,0),new Vector3(.075f,2.28f,.13f),iron);
            foreach(float y in new[]{.66f,1.65f,2.90f})Part("Grille cross brace",gate.transform,new Vector3(0,y,0),new Vector3(2.82f,.095f,.18f),iron);
            foreach(float x in new[]{-1.23f,-.62f,0,.62f,1.23f})foreach(float z in new[]{-.12f,.12f})Part("Brass rivet",gate.transform,new Vector3(x,.34f,z),new Vector3(.085f,.085f,.035f),brass);
            // One continuous blocker stops actors slipping between the decorative bars.
            var collider=gate.AddComponent<BoxCollider>();collider.center=new Vector3(0,1.49f,0);collider.size=new Vector3(2.82f,2.90f,.22f);
            var body=gate.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;body.interpolation=RigidbodyInterpolation.Interpolate;
            var controller=model.AddComponent<DungeonHoldingDoor>();var so=new SerializedObject(controller);so.FindProperty("gate").objectReferenceValue=gate.transform;so.FindProperty("gateCollider").objectReferenceValue=collider;so.ApplyModifiedPropertiesWithoutUndo();
            prefab=PrefabUtility.SaveAsPrefabAsset(model,Folder+"DungeonHoldingGate.prefab");
        }finally{UnityEngine.Object.DestroyImmediate(model);EditorSceneManager.ClosePreviewScene(preview);}
        var root=new GameObject("10 / Holding-room dungeon gates");Undo.RegisterCreatedObjectUndo(root,"Add dungeon holding doors");
        foreach(var marker in markers){
            var door=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root.transform);door.name=marker.name.Contains("West")?"West holding room / Dungeon gate":"East holding room / Dungeon gate";
            door.transform.SetPositionAndRotation(marker.position,marker.rotation);Undo.RegisterCreatedObjectUndo(door,"Add dungeon holding doors");PrefabUtility.RecordPrefabInstancePropertyModifications(door.transform);
            var controller=door.GetComponent<DungeonHoldingDoor>();var so=new SerializedObject(controller);so.FindProperty("interactionTarget").objectReferenceValue=player.transform;so.ApplyModifiedProperties();PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            var sill=marker.GetComponentsInChildren<Renderer>().FirstOrDefault();if(sill!=null){Undo.RecordObject(sill,"Hide old door guide stripe");sill.enabled=false;}
            var info=marker.GetComponent<Skybreak.Handbuilt.SecurityRoomLayoutMarker>();if(info!=null){Undo.RecordObject(info,"Update door layout label");info.description="DUNGEON HOLDING GATE / F preview / Open() for encounter release";}
        }
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed");
        Selection.activeGameObject=root;SceneView.lastActiveSceneView?.FrameSelected();return "Saved two closed animated dungeon gates at west (22,0,54) and east (63,0,46). F toggles nearby gate; encounter code can call Open(). Backup: "+backup;
    }
}
