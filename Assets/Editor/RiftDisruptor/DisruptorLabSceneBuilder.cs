using System.IO;
using Skybreak.AI.Disruptor.Lab;
using Skybreak.Movement.Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Skybreak.DisruptorEditor
{
    // Menu, or batch: -executeMethod Skybreak.DisruptorEditor.DisruptorLabSceneBuilder.BuildBoth -quit
    public static class DisruptorLabSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Tests/RiftDisruptor/RiftDisruptorLab.unity";
        public const string MovementScenePath = "Assets/Scenes/Tests/Student4Movement/AgentMovementLab.unity";

        [MenuItem("Skybreak/Rift Disruptor/Create lab scene")]
        public static void Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lab = new GameObject("Rift Disruptor Lab").AddComponent<DisruptorLab>();
            lab.disruptorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RiftDisruptorAssetBuilder.PrefabPath);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log("Rift Disruptor lab saved to " + ScenePath + (lab.disruptorPrefab != null ? " (mech prefab)" : " (capsule placeholder)"));
        }

        [MenuItem("Skybreak/Rift Disruptor/Create movement lab scene")]
        public static void BuildMovementLab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MovementScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lab = new GameObject("Agent Movement Lab").AddComponent<AgentMovementLab>();
            lab.robotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RiftDisruptorAssetBuilder.PrefabPath);
            EditorSceneManager.SaveScene(scene, MovementScenePath);
            AssetDatabase.Refresh();
            Debug.Log("Agent movement lab saved to " + MovementScenePath);
        }

        public static void BuildBoth() { Build(); BuildMovementLab(); }
    }
}
