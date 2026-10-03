using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Skybreak.Descent;

namespace Skybreak.Handbuilt.Editor
{
    public static class TexturedWallUpgrade
    {
        const string Root = "Assets/Skybreak/Handbuilt";
        const string ScenePath = Root + "/Scenes/SkybreakStoneFloor.unity";
        struct Block { public Vector3 centre, size; public int material, pattern; }
        static Material[] Palette(string region)
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Skybreak_StoneAtlas_" + region + ".mat");
            if (source == null) throw new InvalidOperationException("Floor palette material is missing: " + region);
            return Enumerable.Range(0, 3).Select(i =>
            {
                string name = "Wall_Textured_" + region + "_" + new[] { "Body", "Cap", "Base" }[i];
                string path = Root + "/Materials/" + name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null) return mat;
                mat = new Material(source) { name = name, enableInstancing = true };
                var c = source.GetColor("_BaseColor");
                float factor = i == 1 ? 1.08f : i == 2 ? .7f : 1;
                mat.SetColor("_BaseColor", new Color(Mathf.Min(1,c.r*factor),Mathf.Min(1,c.g*factor),Mathf.Min(1,c.b*factor),1));
                mat.SetFloat("_Smoothness", .65f);
                AssetDatabase.CreateAsset(mat, path);
                return mat;
            }).ToArray();
        }
        static GameObject Kit(string name, List<Block> blocks, Material[] mats, float height, bool doorway)
        {
            string path = Root + "/Prefabs/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var bevel = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Skybreak/Descent/Meshes/BeveledBlock.asset");
            if (bevel == null) throw new InvalidOperationException("Original wall mesh is missing.");
            var mapped = Enumerable.Range(0, 16).Select(i =>
            {
                var mesh = UnityEngine.Object.Instantiate(bevel);
                // Each block samples one of the floor atlas's 16 stones, with padding to avoid neighbouring cells.
                Vector2 offset = new Vector2((i % 4) * .25f + .004f, (i / 4) * .25f + .004f);
                mesh.uv = mesh.uv.Select(uv => offset + uv * .242f).ToArray();
                mesh.RecalculateTangents(); return mesh;
            }).ToArray();
            var parts = new List<Mesh>();
            for (int family = 0; family < 3; family++)
            {
                var mesh = new Mesh();
                mesh.CombineMeshes(blocks.Where(b => b.material == family).Select(b => new CombineInstance
                { mesh = mapped[b.pattern % 16], transform = Matrix4x4.TRS(b.centre, Quaternion.identity, b.size) }).ToArray(),true,true);
                parts.Add(mesh);
            }
            var combined = new Mesh { name = name + " / atlas mapped masonry" };
            combined.CombineMeshes(parts.Select(m => new CombineInstance { mesh = m, transform = Matrix4x4.identity }).ToArray(),false,true);
            combined.RecalculateTangents(); combined.RecalculateBounds();
            foreach (var mesh in mapped.Concat(parts)) UnityEngine.Object.DestroyImmediate(mesh);
            AssetDatabase.CreateAsset(combined, Root + "/Meshes/" + name + ".asset");
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = combined;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            if (!doorway) AddCollider(go, new Vector3(0,height/2,0),new Vector3(1,height,.72f));
            else
            {
                AddCollider(go,new Vector3(-1.75f,1.5f,0),new Vector3(.5f,3,.72f));
                AddCollider(go,new Vector3(1.75f,1.5f,0),new Vector3(.5f,3,.72f));
                AddCollider(go,new Vector3(0,(height+3)/2,0),new Vector3(4,height-3,.72f));
            }
            GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccluderStatic);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go,path); UnityEngine.Object.DestroyImmediate(go); return prefab;
        }
        static void AddCollider(GameObject go, Vector3 centre, Vector3 size)
        { var c=go.AddComponent<BoxCollider>();c.center=centre;c.size=size; }
        static GameObject Wall(float height,int variation,Material[] mats)
        {
            var blocks=new List<Block>();int rows=height>6.5f?8:7;float step=(height-.54f)/rows;
            for(int r=0;r<rows;r++) blocks.Add(new Block{centre=new Vector3(0,.26f+(r+.5f)*step,0),size=new Vector3(.99f,step-.035f,.72f),material=0,pattern=r*7+variation*3});
            blocks.Add(new Block{centre=new Vector3(0,.13f,0),size=new Vector3(1,.26f,.72f),material=2,pattern=variation+5});
            blocks.Add(new Block{centre=new Vector3(0,height-.14f,0),size=new Vector3(1,.28f,.72f),material=1,pattern=variation+9});
            return Kit("Wall_Textured_"+(height>6.5f?"70":"65")+"_V"+(variation+1),blocks,mats,height,false);
        }
        static GameObject Door(Material[] mats)
        {
            var blocks=new List<Block>();
            for(int side=-1;side<=1;side+=2)
            {
                for(int r=0;r<4;r++)blocks.Add(new Block{centre=new Vector3(side*1.75f,(r+.5f)*.75f,0),size=new Vector3(.5f,.715f,.72f),material=0,pattern=r*3+(side+1)*2});
                blocks.Add(new Block{centre=new Vector3(side*1.75f,.13f,0),size=new Vector3(.5f,.26f,.72f),material=2,pattern=5});
            }
            for(int x=0;x<4;x++)
            {
                for(int r=0;r<4;r++)blocks.Add(new Block{centre=new Vector3(x-1.5f,3+(r+.5f)*.805f,0),size=new Vector3(.99f,.77f,.72f),material=0,pattern=r*7+x*3});
                blocks.Add(new Block{centre=new Vector3(x-1.5f,6.36f,0),size=new Vector3(1,.28f,.72f),material=1,pattern=x+9});
            }
            return Kit("Wall_Textured_Doorway_65_3mClear",blocks,mats,6.5f,true);
        }
        [MenuItem("Skybreak/Handbuilt Floor/Upgrade walls with stone textures and height")]
        public static string Apply()
        {
            var scene=SceneManager.GetActiveScene();var root=GameObject.Find("07 / Modular walls");
            if(EditorApplication.isPlaying||scene.path!=ScenePath||root==null)throw new InvalidOperationException("Open the stone floor scene with walls in Edit Mode.");
            var existing=root.GetComponentsInChildren<MeshRenderer>();
            if(existing.Any(r=>r.sharedMaterials.Any(m=>m!=null&&m.name.StartsWith("Wall_Textured_"))))throw new InvalidOperationException("Textured wall upgrade already exists; edit the saved modules manually.");
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
            string backup=AssetDatabase.GenerateUniqueAssetPath(Root+"/Scenes/SkybreakStoneFloor_BeforeTexturedWalls_20261002.unity");
            if(!AssetDatabase.CopyAsset(ScenePath,backup))throw new IOException("Backup failed; stopped before changing walls.");
            var grey=Palette("Floor2");var blue=Palette("PassageFloor");var violet=Palette("CoreFloor");
            var normal=Enumerable.Range(0,4).Select(i=>Wall(6.5f,i,grey)).ToArray();
            var passage=Enumerable.Range(0,4).Select(i=>Wall(7,i,grey)).ToArray();var door=Door(grey);
            int standard=0,tall=0,doors=0;
            Undo.SetCurrentGroupName("Texture and raise walls");
            foreach(var renderer in existing)
            {
                var old=renderer.gameObject;bool frame=old.GetComponents<BoxCollider>().Length==3;
                bool tunnel=!frame&&old.GetComponent<BoxCollider>().size.y>4.6f;
                int pattern=(int)((uint)(Mathf.RoundToInt(old.transform.position.x*2)*73856093^Mathf.RoundToInt(old.transform.position.z*2)*19349663)%4);
                var replacement=(GameObject)PrefabUtility.InstantiatePrefab(frame?door:tunnel?passage[pattern]:normal[pattern],old.transform.parent);
                Undo.RegisterCreatedObjectUndo(replacement,"Replace wall module");
                replacement.name=old.name;replacement.transform.localPosition=old.transform.localPosition;replacement.transform.localRotation=old.transform.localRotation;replacement.transform.localScale=old.transform.localScale;
                var mats=old.transform.parent.name=="Boss chamber"?violet:tunnel?blue:grey;
                var r=replacement.GetComponent<MeshRenderer>();r.sharedMaterials=mats;PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                Undo.DestroyObjectImmediate(old);
                if(frame)doors++;else if(tunnel)tall++;else standard++;
            }
            Physics.SyncTransforms();
            var boundaries=root.GetComponentsInChildren<BoxCollider>().Where(c=>c.GetComponents<BoxCollider>().Length==1).ToArray();
            int hits=0;
            foreach(var c in boundaries)
            {
                Vector3 centre=c.transform.position;Vector3 toward=c.transform.forward;
                if(c.Raycast(new Ray(centre-toward*1.2f+Vector3.up*1.3f,toward),out var hit,2f))hits++;
            }
            int clearDoors=0;foreach(var frame in root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.GetComponents<BoxCollider>().Length==3))
            {var t=frame.transform;if(!Physics.SphereCastAll(t.position-t.forward*1.5f+Vector3.up*1.3f,.3f,t.forward,3).Any(h=>h.transform.IsChildOf(root.transform)))clearDoors++;}
            var floor=GameObject.Find("01 / STONE FLOOR / editable one metre tiles");
            bool textureShared=root.GetComponentsInChildren<MeshRenderer>().All(r=>r.sharedMaterials.All(m=>m.GetTexture("_BaseMap")==grey[0].GetTexture("_BaseMap")&&m.GetTexture("_BumpMap")==grey[0].GetTexture("_BumpMap")));
            var report=new[]{"Backup: "+backup,"Standard wall modules: "+standard+" / 6.5m high","Passage modules: "+tall+" / 7m high","Doorway frames: "+doors+" / 6.5m high, 3m clear opening retained","Boundary collider checks: "+hits+" / "+boundaries.Length,"Open doorway checks: "+clearDoors+" / "+doors,"Shared floor albedo and normal maps: "+textureShared,"Floor tiles retained: "+floor.GetComponentsInChildren<BoxCollider>().Length,"Old wall prefab/mesh/material assets unchanged; only scene wall instances replaced."};
            File.WriteAllLines(Root+"/Reports/TexturedWallUpgrade.txt",report);
            var preview=UnityEngine.Object.FindFirstObjectByType<DescentMapPreview>();preview.ShowShot(1);
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            if(hits!=boundaries.Length||clearDoors!=doors||!textureShared)throw new InvalidOperationException("Wall validation needs attention; inspect TexturedWallUpgrade.txt.");
            return string.Join("\n",report);
        }

        public static string ValidateExisting()
        {
            var root=GameObject.Find("07 / Modular walls");var floor=GameObject.Find("01 / STONE FLOOR / editable one metre tiles");
            if(root==null||floor==null)throw new InvalidOperationException("Map roots are missing.");
            Physics.SyncTransforms();
            var tiles=floor.GetComponentsInChildren<BoxCollider>();
            var cells=new HashSet<Vector2Int>(tiles.Select(c=>new Vector2Int(Mathf.FloorToInt(c.transform.position.x),Mathf.FloorToInt(c.transform.position.z))));
            var directions=new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right};int expected=0,hits=0;
            foreach(var cell in cells)foreach(var d in directions)
            {
                if(cells.Contains(cell+d))continue;expected++;
                if(Physics.Raycast(new Vector3(cell.x+.5f,1.3f,cell.y+.5f),new Vector3(d.x,0,d.y),out var hit,1.2f)&&hit.transform.IsChildOf(root.transform))hits++;
            }
            var renderers=root.GetComponentsInChildren<MeshRenderer>();
            int doors=0,clearDoors=0,standard=0,tall=0,wrongHeights=0;
            foreach(var r in renderers)
            {
                var c=r.GetComponents<BoxCollider>();
                if(c.Length==3)
                {
                    doors++;var t=r.transform;
                    if(!Physics.SphereCastAll(t.position-t.forward*1.5f+Vector3.up*1.3f,.3f,t.forward,3).Any(h=>h.transform.IsChildOf(root.transform)))clearDoors++;
                    if(Mathf.Abs(c.Max(b=>b.center.y+b.size.y/2)-6.5f)>.01f)wrongHeights++;
                }
                else {bool tunnel=r.transform.parent.name=="Winding passage";if(tunnel)tall++;else standard++;if(Mathf.Abs(c[0].size.y-(tunnel?7:6.5f))>.01f)wrongHeights++;}
            }
            int blockedCentres=cells.Count(c=>Physics.OverlapSphere(new Vector3(c.x+.5f,1.3f,c.y+.5f),.1f).Any(h=>h.transform.IsChildOf(root.transform)));
            var route=new[]{new Vector3(0,1.3f,-4),new Vector3(0,1.3f,14),new Vector3(0,1.3f,21),new Vector3(8,1.3f,21),new Vector3(21,1.3f,21),new Vector3(21,1.3f,35),new Vector3(9,1.3f,35),new Vector3(9,1.3f,46),new Vector3(37,1.3f,46),new Vector3(42,1.3f,46),new Vector3(42,1.3f,57),new Vector3(42,1.3f,66),new Vector3(42,1.3f,84)};
            int blockedRoute=0;for(int i=1;i<route.Length;i++){var v=route[i]-route[i-1];if(Physics.SphereCastAll(route[i-1],.3f,v.normalized,v.magnitude).Any(h=>h.transform.IsChildOf(root.transform)))blockedRoute++;}
            var source=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Skybreak_StoneAtlas_Floor2.mat");
            bool shared=renderers.All(r=>r.sharedMaterials.All(m=>new[]{"_BaseMap","_BumpMap","_MetallicGlossMap"}.All(p=>m.GetTexture(p)==source.GetTexture(p))));
            string path=Root+"/Reports/TexturedWallUpgrade.txt";
            string backup=File.Exists(path)?File.ReadLines(path).First():"Backup: see Scenes/SkybreakStoneFloor_BeforeTexturedWalls_20261002.unity";
            var report=new[]{backup,"Standard wall modules: "+standard+" / 6.5m high","Passage modules: "+tall+" / 7m high","Doorway frames: "+doors+" / 6.5m high, 3m clear opening retained","Actual floor-boundary collider hits: "+hits+" / "+expected,"Incorrect collider heights: "+wrongHeights,"Floor centres blocked: "+blockedCentres,"Mission route segments blocked: "+blockedRoute,"Open doorway checks: "+clearDoors+" / "+doors,"Shared floor albedo, normal and smoothness maps: "+shared,"Floor tiles retained: "+tiles.Length,"Old wall prefab/mesh/material assets unchanged; only scene wall instances replaced."};
            File.WriteAllLines(path,report);
            if(hits!=expected||wrongHeights!=0||blockedCentres!=0||blockedRoute!=0||clearDoors!=doors||!shared)throw new InvalidOperationException("Wall validation needs attention: "+string.Join("\n",report));
            return string.Join("\n",report);
        }
    }
}
