using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ImportDungeonLibrary
{
    private static string Root => Path.GetDirectoryName(Application.dataPath);
    private const string Models = "Assets/Models/Environment/Decoration/";
    private const string Textures = "Assets/Textures/Environment/Decoration/";
    private const string Materials = "Assets/Materials/Environment/Decoration/";
    private const string Prefabs = "Assets/Prefabs/Environment/Decoration/";
    private static string[] Packs = {"KayKit_Dungeon", "Quaternius_ModularDungeons", "MrScientist_Dungeon", "RGSDev_Dungeon"};

    public static string Prepare()
    {
        if(EditorApplication.isPlaying) throw new Exception("Exit Play Mode first.");
        var library = Path.Combine(Root,"resources/DungeonAssetLibrary/Packs");
        var sources = new[]{
            Path.Combine(library,"KayKit_Dungeon_Free_1.1/KayKit_Dungeon_Pack_1.1_FREE/Assets/fbx(unity)"),
            Path.Combine(library,"Quaternius_ModularDungeons/FBX"),
            Path.Combine(library,"MrScientist_LowPolyDungeon_v0.3/Assets/Models"),
            Path.Combine(library,"RGSDev_ModularDungeon/Free Modular Low Poly Dungeon")
        };
        int count=0;
        AssetDatabase.StartAssetEditing();
        try {
            for(int i=0;i<Packs.Length;i++) {
                foreach(var dir in new[]{Models,Materials,Prefabs,Textures}) Directory.CreateDirectory(Path.Combine(Root,dir+Packs[i]));
                foreach(var source in Directory.GetFiles(sources[i],"*.fbx",SearchOption.AllDirectories)) {
                    if(source.EndsWith("low poly dungeon sample.fbx")) continue;
                    var relative=source.Substring(sources[i].Length+1).Replace('\\','/');
                    var dest=Path.Combine(Root,Models+Packs[i]+"/"+relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    if(!File.Exists(dest))File.Copy(source,dest);
                    count++;
                }
            }
            CopyTexture(Path.Combine(library,"KayKit_Dungeon_Free_1.1/KayKit_Dungeon_Pack_1.1_FREE/Assets/textures/dungeon_texture.png"),"KayKit_Dungeon");
            CopyTexture(Path.Combine(library,"MrScientist_LowPolyDungeon_v0.3/Assets/Materials/Colour Palette.png"),"MrScientist_Dungeon");
        } finally {AssetDatabase.StopAssetEditing();}
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        return "Imported "+count+" FBX sources and palette textures. Scene edits preserved.";
    }
    private static void CopyTexture(string source,string pack) {
        var dest=Path.Combine(Root,Textures+pack+"/"+Path.GetFileName(source));
        if(!File.Exists(dest))File.Copy(source,dest);
    }

    public static string Build()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play Mode first.");
        var shader=Shader.Find("Universal Render Pipeline/Lit");
        if(shader==null)throw new Exception("Missing URP Lit shader.");
        var preview=EditorSceneManager.NewPreviewScene();
        int created=0;
        var report=new List<string>{"# Dungeon prefab import report","","Assets prepared without saving or modifying the user's map. Authored scale preserved; pivots moved to bottom centre.","","| Prefab | Bounds (metres) | Collision |","| --- | --- | --- |"};
        try {
            foreach(var pack in Packs) {
                var paths=AssetDatabase.FindAssets("t:Model",new[]{Models+pack}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)).OrderBy(p=>p).ToArray();
                foreach(var path in paths) {
                    var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if(model==null)throw new Exception("Failed model: "+path);
                    var imported=(GameObject)PrefabUtility.InstantiatePrefab(model,preview);
                    try {
                        var pieces=pack=="RGSDev_Dungeon" ? imported.transform.Cast<Transform>().Where(t=>t.GetComponentsInChildren<Renderer>(true).Length>0).Select(t=>t.gameObject).ToArray() : new[]{imported};
                        if(pieces.Length==0)pieces=new[]{imported};
                        foreach(var piece in pieces) {
                            var name=pack=="RGSDev_Dungeon" ? piece.name : path.Substring((Models+pack+"/").Length).Replace('/','_').Replace(".fbx","");
                            foreach(var c in Path.GetInvalidFileNameChars())name=name.Replace(c,'_');
                            var prefabPath=Prefabs+pack+"/"+name+".prefab";
                            if(File.Exists(Path.Combine(Root,prefabPath))) continue;
                            var holder=new GameObject(name);
                            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(holder,preview);
                            try {
                                var visual=UnityEngine.Object.Instantiate(piece,holder.transform);
                                visual.name="Visual";
                                visual.transform.localPosition=Vector3.zero;
                                foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true)) {
                                    var remapped=new List<Material>();
                                    foreach(var original in renderer.sharedMaterials) {
                                        var atlas=pack=="KayKit_Dungeon" || pack=="MrScientist_Dungeon";
                                        var matName=atlas ? "DungeonAtlas" : original!=null ? original.name : "DefaultStone";
                                        foreach(var c in Path.GetInvalidFileNameChars())matName=matName.Replace(c,'_');
                                        var matPath=Materials+pack+"/"+matName+".mat";
                                        var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                                        if(mat==null) {
                                            mat=new Material(shader){name=matName};
                                            mat.SetFloat("_Smoothness",0.22f);
                                            if(atlas) {
                                                var texPath=Textures+pack+"/"+(pack=="KayKit_Dungeon" ? "dungeon_texture.png" : "Colour Palette.png");
                                                var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                                                if(tex==null)throw new Exception("Missing atlas: "+texPath);
                                                mat.SetTexture("_BaseMap",tex);mat.SetColor("_BaseColor",Color.white);
                                            } else {
                                                var color=original!=null && original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : original!=null && original.HasProperty("_Color") ? original.GetColor("_Color") : Color.gray;
                                                mat.SetColor("_BaseColor",color);
                                            }
                                            AssetDatabase.CreateAsset(mat,matPath);
                                        }
                                        remapped.Add(mat);
                                    }
                                    renderer.sharedMaterials=remapped.ToArray();
                                }
                                var renders=visual.GetComponentsInChildren<Renderer>(true);
                                if(renders.Length==0)throw new Exception("No renderers: "+name);
                                var bounds=renders[0].bounds;
                                foreach(var r in renders.Skip(1))bounds.Encapsulate(r.bounds);
                                visual.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                                var lower=name.ToLowerInvariant();
                                bool meshCollision=new[]{"wall","arch","stair","floor","door","gate","fence","trapdoor"}.Any(lower.Contains);
                                bool boxCollision=new[]{"crate","barrel","column","pillar","pedestal","chest","table","chair","bench","box","statue"}.Any(lower.Contains);
                                string collision="Decorative; none";
                                if(meshCollision) {
                                    foreach(var mf in visual.GetComponentsInChildren<MeshFilter>(true))if(mf.sharedMesh!=null){var col=mf.gameObject.AddComponent<MeshCollider>();col.sharedMesh=mf.sharedMesh;col.convex=false;}
                                    collision="Static mesh; preserves openings";
                                } else if(boxCollision) {
                                    var col=holder.AddComponent<BoxCollider>();col.size=bounds.size;col.center=new Vector3(0,bounds.size.y/2,0);collision="BoxCollider";
                                }
                                PrefabUtility.SaveAsPrefabAsset(holder,prefabPath);
                                created++;
                                report.Add("| "+prefabPath+" | "+bounds.size.ToString("F2")+" | "+collision+" |");
                            } finally {UnityEngine.Object.DestroyImmediate(holder);}
                        }
                    } finally {UnityEngine.Object.DestroyImmediate(imported);}
                }
            }
        } finally {EditorSceneManager.ClosePreviewScene(preview);}
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory(Path.Combine(Root,"Docs/GV/LevelDesign/Reports/Decoration"));
        File.WriteAllLines(Path.Combine(Root,"Docs/GV/LevelDesign/Reports/Decoration/DungeonImportReport.md"),report);
        return "Created "+created+" dungeon prefabs. Map was not saved or edited.";
    }
}
