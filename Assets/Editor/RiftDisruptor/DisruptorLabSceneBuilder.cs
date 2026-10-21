using System.IO;
using Skybreak.AI.Disruptor.Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Skybreak.DisruptorEditor
{
    // Menu, or batch: -executeMethod Skybreak.DisruptorEditor.DisruptorLabSceneBuilder.Build -quit
    public static class DisruptorLabSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Tests/RiftDisruptor/RiftDisruptorLab.unity";

        [MenuItem("Skybreak/Rift Disruptor/Create lab scene")]
        public static void Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Rift Disruptor Lab").AddComponent<DisruptorLab>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log("Rift Disruptor lab saved to " + ScenePath);
        }
    }
}
