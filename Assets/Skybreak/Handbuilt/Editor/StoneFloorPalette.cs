using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Skybreak.Handbuilt.Editor
{
    // Scene material overrides preserve the downloaded mesh, textures and prefab links.
    public static class StoneFloorPalette
    {
        const string Root = "Assets/Skybreak/Handbuilt";
        const string ScenePath = Root + "/Scenes/SkybreakStoneFloor.unity";
        const float TextureTintCompensation = 1.65f;
        static readonly string[] Names = { "Floor0", "Floor1", "Floor2", "Floor3", "Floor4", "PassageFloor", "CoreFloor" };

        [MenuItem("Skybreak/Handbuilt Floor/Apply original floor colours")]
        public static string Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != ScenePath)
                throw new InvalidOperationException("Open the stone floor scene and stop Play Mode first.");
            var floor = GameObject.Find("01 / STONE FLOOR / editable one metre tiles");
            if (floor == null) throw new InvalidOperationException("Stone floor root was not found.");
            var tiles = floor.GetComponentsInChildren<BoxCollider>();
            var bases = new[] { Load("Skybreak_StoneAtlas"), Load("Skybreak_StoneDetails") };
            var originals = Names.Select(n => AssetDatabase.LoadAssetAtPath<Material>("Assets/Skybreak/Descent/Materials/" + n + ".mat")).ToArray();
            if (bases.Any(m => m == null) || originals.Any(m => m == null))
                throw new InvalidOperationException("A source stone material or original palette material is missing.");
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the current floor.");
            string backup = AssetDatabase.GenerateUniqueAssetPath(Root + "/Scenes/SkybreakStoneFloor_BeforeOriginalColours_20261002.unity");
            if (!AssetDatabase.CopyAsset(ScenePath, backup)) throw new IOException("Could not back up the floor; stopped before recolouring.");

            var palette = new Material[Names.Length, 2];
            var report = new List<string> { "Backup: " + backup, "Only scene floor material references were changed.", "Original palette hues with 1.65 brightness compensation for the darker downloaded stone albedo." };
            for (int colour = 0; colour < Names.Length; colour++)
            {
                Color original = originals[colour].GetColor("_BaseColor");
                Color tint = new Color(Mathf.Min(1, original.r * TextureTintCompensation), Mathf.Min(1, original.g * TextureTintCompensation), Mathf.Min(1, original.b * TextureTintCompensation), 1);
                for (int family = 0; family < bases.Length; family++)
                {
                    string name = bases[family].name + "_" + Names[colour];
                    var mat = Load(name);
                    if (mat == null) { mat = new Material(bases[family]) { name = name }; AssetDatabase.CreateAsset(mat, Root + "/Materials/" + name + ".mat"); }
                    else mat.CopyPropertiesFromMaterial(bases[family]);
                    mat.SetColor("_BaseColor", tint);
                    mat.enableInstancing = true;
                    EditorUtility.SetDirty(mat);
                    palette[colour, family] = mat;
                    if (!mat.shader.isSupported) throw new InvalidOperationException("Unsupported floor shader.");
                    foreach (string map in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap" })
                        if (mat.GetTexture(map) != bases[family].GetTexture(map)) throw new InvalidOperationException("A shared texture reference changed.");
                }
                report.Add(Names[colour] + ": source #" + ColorUtility.ToHtmlStringRGB(original) + ", stone tint #" + ColorUtility.ToHtmlStringRGB(tint));
            }
            var counts = new int[Names.Length];
            int replaced = 0;
            Undo.RecordObjects(floor.GetComponentsInChildren<MeshRenderer>(), "Apply original floor palette");
            foreach (var tile in tiles)
            {
                float x = tile.transform.position.x, z = tile.transform.position.z;
                int index = z > 71 ? 6 : x > 6 && x < 28 && z > 24 && z < 50 ? 5
                    : (int)((uint)(Mathf.FloorToInt(x) * 73856093 ^ Mathf.FloorToInt(z) * 19349663) % 5u);
                counts[index]++;
                foreach (var renderer in tile.GetComponentsInChildren<MeshRenderer>())
                {
                    var materials = renderer.sharedMaterials;
                    bool changed = false;
                    for (int slot = 0; slot < materials.Length; slot++)
                        for (int family = 0; family < bases.Length; family++)
                            if (materials[slot] != null && (materials[slot] == bases[family] || materials[slot].name.StartsWith(bases[family].name + "_")))
                            { materials[slot] = palette[index, family]; replaced++; changed = true; break; }
                    if (!changed) continue;
                    renderer.sharedMaterials = materials;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    EditorUtility.SetDirty(renderer);
                }
            }
            if (floor.GetComponentsInChildren<BoxCollider>().Length != tiles.Length)
                throw new InvalidOperationException("Floor collider count changed unexpectedly.");
            for (int i = 0; i < Names.Length; i++) report.Add(Names[i] + " tiles: " + counts[i]);
            report.Add("Preserved floor tiles/colliders: " + tiles.Length);
            report.Add("Recoloured material slots: " + replaced);
            report.Add("14 shared palette materials; original albedo, normal and smoothness maps retained. Original neutral materials and prefab assets unchanged.");
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save recoloured floor.");
            File.WriteAllLines(Root + "/Reports/FloorPalette.txt", report);
            return string.Join("\n", report);
        }

        static Material Load(string name) => AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/" + name + ".mat");
    }
}
