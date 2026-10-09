using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

public static class ImportMapDecoration
{
    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Exit Play Mode before importing.");
        var root = Path.GetDirectoryName(Application.dataPath);
        var packs = new Dictionary<string, string[]> {
            {"Kenney_FactoryKit", new[]{"box-large","box-long","box-wide","machine","machine-fortified","pipe-large","pipe-large-bend","pipe-large-junction","pipe-large-valve","screen-panel-wide","screen-hanging-small","door-wide-closed"}},
            {"Kenney_SpaceStationKit", new[]{"structure-barrier","structure-barrier-high","container","container-wide","container-tall","computer","computer-system","computer-wide","pipe","pipe-bend","wall-switch","rocks"}},
            {"Kenney_ModularDungeonKit", new[]{"gate","gate-door","gate-metal-bars","template-wall-half","template-wall-detail-a","template-wall-top"}},
            {"Quaternius_Ruins_Selected", new[]{"Arch_Round","Arch_Gothic","Column_Round","Column_Square","Column_Round_Short","Crate","Barrel","Bricks","Brick","Doors_RoundArch","Flag_Wall","Chest"}}
        };
        var paths = new List<string>();
        AssetDatabase.StartAssetEditing();
        try {
            foreach (var pack in packs) {
                var source = "resources/MapDecorationKit/Packs/" + pack.Key + (pack.Key.StartsWith("Kenney") ? "/Models/FBX format/" : "/FBX/");
                var dest = "Assets/Models/Environment/Decoration/" + pack.Key + "/";
                Directory.CreateDirectory(Path.Combine(root,dest));
                foreach (var name in pack.Value) {
                    var path = dest+name+".fbx";
                    if (!File.Exists(Path.Combine(root,path))) File.Copy(Path.Combine(root,source+name+".fbx"),Path.Combine(root,path));
                    paths.Add(path);
                }
                if (pack.Key.StartsWith("Kenney")) {
                    var textureDest = "Assets/Textures/Environment/Decoration/"+pack.Key+"/";
                    Directory.CreateDirectory(Path.Combine(root,textureDest));
                    var texturePath = Path.Combine(root,textureDest+"colormap.png");
                    if (!File.Exists(texturePath)) File.Copy(Path.Combine(root,source+"Textures/colormap.png"),texturePath);
                }
            }
        } finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new Exception("URP Lit shader missing.");
        var report = new List<string>{"# Decoration import report", "", "Imported source models and reusable prefabs only; main scene and demos were not edited.", "", "| Prefab | Bounds in metres (X,Y,Z) | Collider |", "| --- | --- | --- |"};
        foreach (var path in paths) {
            var pack = path.Split('/')[4];
            var name = Path.GetFileNameWithoutExtension(path);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation = false;
            importer.isReadable = false;
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) throw new Exception("Model failed import: "+path);
            var materialDir = "Assets/Materials/Environment/Decoration/"+pack;
            var prefabDir = "Assets/Prefabs/Environment/Decoration/"+pack;
            Directory.CreateDirectory(Path.Combine(root,materialDir));
            Directory.CreateDirectory(Path.Combine(root,prefabDir));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var instance = UnityEngine.Object.Instantiate(model);
            var holder = new GameObject(name);
            try {
                instance.name = "Visual";
                instance.transform.SetParent(holder.transform,false);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true)) {
                    var remapped = new List<Material>();
                    foreach (var original in renderer.sharedMaterials) {
                        string materialName = pack.StartsWith("Kenney") ? "Atlas" : (original != null ? original.name : "Stone");
                        foreach (var c in Path.GetInvalidFileNameChars()) materialName = materialName.Replace(c,'_');
                        var matPath = materialDir+"/"+materialName+".mat";
                        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                        if (mat == null) {
                            mat = new Material(shader);
                            mat.name = materialName;
                            mat.SetFloat("_Smoothness",0.25f);
                            if (pack.StartsWith("Kenney")) {
                                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Environment/Decoration/"+pack+"/colormap.png");
                                if (tex == null) throw new Exception("Missing palette for "+pack);
                                mat.SetTexture("_BaseMap",tex);
                                mat.SetColor("_BaseColor",Color.white);
                            } else {
                                var color = original != null && original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : original != null && original.HasProperty("_Color") ? original.GetColor("_Color") : new Color(0.55f,0.60f,0.59f);
                                mat.SetColor("_BaseColor",color);
                            }
                            AssetDatabase.CreateAsset(mat,matPath);
                        }
                        remapped.Add(mat);
                    }
                    renderer.sharedMaterials = remapped.ToArray();
                }
                var renders = instance.GetComponentsInChildren<Renderer>();
                if (renders.Length == 0) throw new Exception("No renderers: "+path);
                var bounds = renders[0].bounds;
                foreach (var r in renders.Skip(1)) bounds.Encapsulate(r.bounds);
                instance.transform.position -= new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                var collision = name.StartsWith("box-") || name.StartsWith("container") || name.StartsWith("structure-barrier") || name.StartsWith("machine") || name.StartsWith("Column_") || name == "Crate" || name == "Barrel" || name == "Chest";
                if (collision) {
                    var box = holder.AddComponent<BoxCollider>();
                    box.center = new Vector3(0,bounds.size.y/2,0);
                    box.size = bounds.size;
                }
                var prefabPath = prefabDir+"/"+name+".prefab";
                if (!File.Exists(Path.Combine(root,prefabPath))) PrefabUtility.SaveAsPrefabAsset(holder,prefabPath);
                report.Add("| "+prefabPath+" | "+bounds.size.ToString("F2")+" | "+(collision ? "BoxCollider" : "Decorative; no collider")+" |");
            } finally { UnityEngine.Object.DestroyImmediate(holder); }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Directory.CreateDirectory(Path.Combine(root,"Docs/GV/LevelDesign/Reports/Decoration"));
        File.WriteAllLines(Path.Combine(root,"Docs/GV/LevelDesign/Reports/Decoration/ImportReport.md"),report);
        return "Imported "+paths.Count+" FBX models; created URP materials and bottom-centred prefabs. Scene unchanged.";
    }
}
