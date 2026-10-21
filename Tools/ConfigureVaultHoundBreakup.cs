using System;
using UnityEditor;
using UnityEngine;
using Skybreak.AI.SawanIS.VaultHound;

public static class ConfigureVaultHoundBreakup
{
    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Run in Edit Mode.");
        var importer = (ModelImporter)AssetImporter.GetAtPath("Assets/Models/Custom/VaultHound/VaultHound_Animated.fbx");
        if (!importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
        const string path = "Assets/Prefabs/Enemies/VaultHound.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var brain = new SerializedObject(prefab.GetComponent<VaultHoundBrain>());
            brain.FindProperty("pursueSpeed").floatValue = 6.2f;
            brain.FindProperty("deploySpeed").floatValue = 4f;
            brain.ApplyModifiedPropertiesWithoutUndo();
            prefab.GetComponent<UnityEngine.AI.NavMeshAgent>().speed = 4f;
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        return "Enabled readable Hound mesh for bone fragments; prefab chase 6.2 m/s, deployment 4.0 m/s.";
    }
}
