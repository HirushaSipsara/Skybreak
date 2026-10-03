using System;
using System.IO;
using UnityEditor;

namespace Skybreak.Editor
{
    public static class SkybreakScenePaths
    {
        public const string Main = "Assets/Scenes/GameLevel/SkybreakMain.unity";
        public const string LastLightDemo = "Assets/Scenes/Demos/SkybreakLastLightDemo.unity";
        public const string DescentDemo = "Assets/Scenes/Demos/SkybreakDescentDemo.unity";

        // Keep authoring safety copies outside Assets so Unity does not import them as scenes.
        public static string BackupMainScene(string reason)
        {
            if (!File.Exists(Main)) throw new FileNotFoundException("Open the main scene first.", Main);
            foreach (char character in reason)
                if (!char.IsLetterOrDigit(character)) throw new ArgumentException("Use an alphanumeric snapshot label.", nameof(reason));
            const string directory = "Tools/SceneSnapshots";
            Directory.CreateDirectory(directory);
            string path = directory + "/SkybreakMain_" + reason + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".unity";
            if (File.Exists(path)) path = directory + "/SkybreakMain_" + reason + "_" + Guid.NewGuid().ToString("N") + ".unity";
            File.Copy(Main, path, false);
            File.Copy(Main + ".meta", path + ".meta", false);
            return path;
        }

        public static void ConfigureBuildScenes()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Main) == null) return;
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(Main, true)
            };
            foreach (string demo in new[] { LastLightDemo, DescentDemo })
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(demo) != null)
                    scenes.Add(new EditorBuildSettingsScene(demo, false));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
