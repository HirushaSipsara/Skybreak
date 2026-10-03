using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Skybreak.Descent.Editor
{
    public static class DescentCharacterBuilder
    {

        static Mesh block;
        static Material suit, orange, armor, dark, skin, glow;

        static Material Paint(string name, string hex)
        {
            string path="Assets/Materials/Characters/Arc/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) { material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name}; AssetDatabase.CreateAsset(material,path); }
            ColorUtility.TryParseHtmlString("#"+hex,out var color);material.color=color;
            material.SetFloat("_Smoothness",.28f);material.enableInstancing=true;EditorUtility.SetDirty(material);return material;
        }

        static Transform Joint(string name,Transform parent,Vector3 position)
        {
            var go=new GameObject(name);go.layer=8;go.transform.SetParent(parent,false);go.transform.localPosition=position;return go.transform;
        }

        static void Box(string name,Transform parent,Vector3 position,Vector3 size,Material material)
        {
            var t=Joint(name,parent,position);t.localScale=size;t.gameObject.AddComponent<MeshFilter>().sharedMesh=block;
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
        }

        [MenuItem("Skybreak/Descent/Add controllable Arc")]
        public static string Build()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
            if(scene.path!=DescentMapBuilder.ScenePath)throw new InvalidOperationException("Open the Descent map scene first.");
            if(scene.isDirty)throw new InvalidOperationException("Save the current scene first.");
            if(UnityEngine.Object.FindFirstObjectByType<DescentCharacterController>()!=null)return "Controllable Arc already exists.";
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Add controllable Arc");
            var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            tags.FindProperty("layers").GetArrayElementAtIndex(8).stringValue="DescentPlayer";tags.ApplyModifiedProperties();
            block=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/Environment/Dungeon/Meshes/BeveledBlock.asset");
            suit=Paint("Arc_Suit","236E78");orange=Paint("Arc_Harness","DF8139");armor=Paint("Arc_Armor","E8DFBE");dark=Paint("Arc_Dark","14232D");skin=Paint("Arc_Skin","B9875F");
            glow=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Environment/Dungeon/Cyan.mat");
            var player=new GameObject("07 / ARC / Controllable character");player.layer=8;Undo.RegisterCreatedObjectUndo(player,"Create Arc");
            player.transform.position=new Vector3(0,.12f,-4);
            var capsule=player.AddComponent<CharacterController>();capsule.height=1.88f;capsule.radius=.32f;capsule.center=new Vector3(0,.94f,0);capsule.stepOffset=.3f;capsule.slopeLimit=45;capsule.skinWidth=.04f;capsule.minMoveDistance=0;
            var visual=Joint("Arc / stylized rescue suit",player.transform,Vector3.zero);
            Box("Torso",visual,new Vector3(0,1.12f,0),new Vector3(.56f,.60f,.33f),suit);
            Box("Orange rescue vest",visual,new Vector3(0,1.17f,.18f),new Vector3(.44f,.42f,.075f),orange);
            Box("Vest center stripe",visual,new Vector3(0,1.17f,.23f),new Vector3(.09f,.34f,.025f),armor);
            Box("Belt",visual,new Vector3(0,.82f,0),new Vector3(.57f,.13f,.38f),dark);
            Box("Belt buckle",visual,new Vector3(0,.82f,.21f),new Vector3(.14f,.10f,.04f),orange);
            Box("Backpack",visual,new Vector3(0,1.13f,-.25f),new Vector3(.45f,.49f,.22f),orange);
            Box("Backpack inset",visual,new Vector3(0,1.13f,-.375f),new Vector3(.31f,.34f,.04f),dark);
            var ring=Joint("Cyan rescue unit",visual,new Vector3(0,1.14f,-.405f));ring.localScale=Vector3.one*.045f;
            ring.gameObject.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/Environment/Dungeon/Meshes/PortalInnerRing.asset");ring.gameObject.AddComponent<MeshRenderer>().sharedMaterial=glow;
            Box("Neck",visual,new Vector3(0,1.45f,0),new Vector3(.19f,.16f,.20f),skin);
            var head=GameObject.CreatePrimitive(PrimitiveType.Sphere);head.name="Head";head.layer=8;head.transform.SetParent(visual,false);head.transform.localPosition=new Vector3(0,1.67f,0);head.transform.localScale=new Vector3(.41f,.43f,.39f);UnityEngine.Object.DestroyImmediate(head.GetComponent<Collider>());head.GetComponent<MeshRenderer>().sharedMaterial=skin;
            Box("Hair cap",visual,new Vector3(0,1.84f,-.025f),new Vector3(.43f,.16f,.39f),dark);
            Box("Hair back",visual,new Vector3(0,1.72f,-.18f),new Vector3(.38f,.22f,.10f),dark);
            Box("Hair fringe",visual,new Vector3(-.09f,1.79f,.17f),new Vector3(.24f,.09f,.07f),dark);
            Box("Visor frame",visual,new Vector3(0,1.69f,.195f),new Vector3(.34f,.10f,.035f),dark);
            Box("Cyan visor",visual,new Vector3(0,1.69f,.22f),new Vector3(.27f,.043f,.02f),glow);
            var leftArm=Joint("Left shoulder",visual,new Vector3(-.37f,1.39f,0));
            var rightArm=Joint("Right shoulder",visual,new Vector3(.37f,1.39f,0));
            foreach(var arm in new[]{leftArm,rightArm})
            {
                Box("Shoulder armor",arm,new Vector3(0,-.04f,0),new Vector3(.24f,.22f,.29f),armor);
                Box("Sleeve",arm,new Vector3(0,-.23f,0),new Vector3(.18f,.32f,.22f),suit);
                Box("Forearm cuff",arm,new Vector3(0,-.39f,0),new Vector3(.21f,.12f,.25f),orange);
                Box("Glove",arm,new Vector3(0,-.50f,.015f),new Vector3(.18f,.17f,.20f),dark);
            }
            var leftLeg=Joint("Left hip",visual,new Vector3(-.155f,.78f,0));
            var rightLeg=Joint("Right hip",visual,new Vector3(.155f,.78f,0));
            foreach(var leg in new[]{leftLeg,rightLeg})
            {
                Box("Trouser",leg,new Vector3(0,-.25f,0),new Vector3(.22f,.48f,.25f),suit);
                Box("Knee guard",leg,new Vector3(0,-.35f,.15f),new Vector3(.19f,.17f,.06f),armor);
                Box("Boot",leg,new Vector3(0,-.64f,.035f),new Vector3(.25f,.26f,.34f),orange);
                Box("Boot sole",leg,new Vector3(0,-.76f,.035f),new Vector3(.26f,.04f,.36f),dark);
            }
            var camera=Camera.main;Undo.RecordObject(camera,"Configure Arc camera");camera.fieldOfView=65;camera.nearClipPlane=.08f;
            var preview=UnityEngine.Object.FindFirstObjectByType<DescentMapPreview>();Undo.RecordObject(preview,"Disable free camera");preview.enabled=false;
            var controller=player.AddComponent<DescentCharacterController>();controller.viewCamera=camera;controller.visual=visual;controller.leftArm=leftArm;controller.rightArm=rightArm;controller.leftLeg=leftLeg;controller.rightLeg=rightLeg;
            camera.transform.SetPositionAndRotation(player.transform.position+new Vector3(.65f,2.54f,-5.32f),Quaternion.Euler(10,0,0));
            PrefabUtility.SaveAsPrefabAssetAndConnect(player,"Assets/Prefabs/Player/ArcExplorer.prefab",InteractionMode.AutomatedAction);
            Undo.CollapseUndoOperations(group);Selection.activeGameObject=player;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            return "Added Arc explorer prefab, CharacterController and third-person camera. Saved Descent scene.";
        }
    }
}
