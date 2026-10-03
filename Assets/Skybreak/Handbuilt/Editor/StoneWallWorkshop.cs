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
    public static class StoneWallWorkshop
    {
        const string Root = "Assets/Skybreak/Handbuilt";
        const string ScenePath = Root + "/Scenes/SkybreakStoneFloor.unity";
        const string WallRoot = "07 / Modular walls";
        const float Depth = .72f;
        static readonly Vector2Int[] Directions = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
        [Serializable] class Rooms { public Room[] rooms; }
        [Serializable] class Room { public string id; public float doorX, doorZ; }
        struct Block { public Vector3 centre, size; public int material; }

        static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); return t;
        }
        static Material[] Paints()
        {
            return new[] { "Stone", "StoneLight", "StoneDark" }.Select(name =>
            {
                string p = Root + "/Materials/Wall_" + name + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m != null) return m;
                var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Skybreak/Descent/Materials/" + name + ".mat");
                if (source == null) throw new InvalidOperationException("Missing original wall material " + name);
                m = new Material(source) { name = "Wall_" + name, enableInstancing = true };
                AssetDatabase.CreateAsset(m, p); return m;
            }).ToArray();
        }
        static GameObject MakePrefab(string name, List<Block> blocks, Material[] paints, Action<GameObject> collision)
        {
            string path = Root + "/Prefabs/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var blockMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Skybreak/Descent/Meshes/BeveledBlock.asset");
            if (blockMesh == null) throw new InvalidOperationException("Original softened block mesh is missing.");
            var parts = new List<Mesh>();
            for (int family = 0; family < paints.Length; family++)
            {
                var mesh = new Mesh();
                mesh.CombineMeshes(blocks.Where(b => b.material == family).Select(b => new CombineInstance
                { mesh = blockMesh, subMeshIndex = 0, transform = Matrix4x4.TRS(b.centre, Quaternion.identity, b.size) }).ToArray(), true, true);
                parts.Add(mesh);
            }
            var combined = new Mesh { name = name + " / combined masonry" };
            combined.CombineMeshes(parts.Select(m => new CombineInstance { mesh = m, transform = Matrix4x4.identity }).ToArray(), false, true);
            foreach (var m in parts) UnityEngine.Object.DestroyImmediate(m);
            if (!AssetDatabase.IsValidFolder(Root + "/Meshes")) AssetDatabase.CreateFolder(Root, "Meshes");
            AssetDatabase.CreateAsset(combined, Root + "/Meshes/" + name + ".asset");
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = combined;
            go.AddComponent<MeshRenderer>().sharedMaterials = paints;
            collision(go);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }
        static void Collider(GameObject go, Vector3 centre, Vector3 size)
        { var c = go.AddComponent<BoxCollider>(); c.center = centre; c.size = size; }
        static GameObject WallPrefab(float height, Material[] paints)
        {
            var blocks = new List<Block>();
            float rowHeight = (height - .54f) / 5;
            for (int row = 0; row < 5; row++)
                blocks.Add(new Block { centre = new Vector3(0, .26f + (row + .5f) * rowHeight, 0), size = new Vector3(.99f, rowHeight - .035f, Depth), material = row % 3 == 0 ? 1 : 0 });
            blocks.Add(new Block { centre = new Vector3(0, .13f, 0), size = new Vector3(1, .26f, Depth), material = 2 });
            blocks.Add(new Block { centre = new Vector3(0, height - .14f, 0), size = new Vector3(1, .28f, Depth), material = 1 });
            return MakePrefab(height > 4.5f ? "Wall_Passage_1m_49" : "Wall_Stone_1m_45", blocks, paints,
                go => Collider(go, new Vector3(0, height / 2, 0), new Vector3(1, height, Depth)));
        }
        static GameObject DoorPrefab(Material[] paints)
        {
            var blocks = new List<Block>();
            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < 4; row++) blocks.Add(new Block { centre = new Vector3(side * 1.75f, (row + .5f) * .75f, 0), size = new Vector3(.5f, .715f, Depth), material = row % 3 == 0 ? 1 : 0 });
                blocks.Add(new Block { centre = new Vector3(side * 1.75f, .13f, 0), size = new Vector3(.5f, .26f, Depth), material = 2 });
            }
            for (int row = 0; row < 2; row++) blocks.Add(new Block { centre = new Vector3(0, 3 + (row + .5f) * .61f, 0), size = new Vector3(4, .575f, Depth), material = row == 0 ? 0 : 1 });
            blocks.Add(new Block { centre = new Vector3(0, 4.36f, 0), size = new Vector3(4, .28f, Depth), material = 1 });
            return MakePrefab("Wall_Doorway_3mClear", blocks, paints, go =>
            {
                Collider(go, new Vector3(-1.75f, 1.5f, 0), new Vector3(.5f, 3, Depth));
                Collider(go, new Vector3(1.75f, 1.5f, 0), new Vector3(.5f, 3, Depth));
                Collider(go, new Vector3(0, 3.75f, 0), new Vector3(4, 1.5f, Depth));
            });
        }
        static bool Passage(float x, float z) => x > 6 && x < 28 && z > 24 && z < 50;

        [MenuItem("Skybreak/Handbuilt Floor/Add modular walls (once)")]
        public static string Add()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != ScenePath) throw new InvalidOperationException("Open the stone floor scene in Edit Mode first.");
            if (GameObject.Find(WallRoot) != null) throw new InvalidOperationException("Walls already exist; edit the saved wall modules instead of regenerating them.");
            var floor = GameObject.Find("01 / STONE FLOOR / editable one metre tiles");
            if (floor == null) throw new InvalidOperationException("Floor root is missing.");
            var tileColliders = floor.GetComponentsInChildren<BoxCollider>();
            var cells = new HashSet<Vector2Int>(tileColliders.Select(c => new Vector2Int(Mathf.FloorToInt(c.transform.position.x), Mathf.FloorToInt(c.transform.position.z))));
            if (cells.Count != tileColliders.Length) throw new InvalidOperationException("Floor cells overlap; fix those before placing walls.");
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the current scene.");
            string backup = AssetDatabase.GenerateUniqueAssetPath(Root + "/Scenes/SkybreakStoneFloor_BeforeWalls_20261002.unity");
            if (!AssetDatabase.CopyAsset(ScenePath, backup)) throw new IOException("Backup failed; stopped before adding walls.");
            var paints = Paints();
            var normal = WallPrefab(4.5f, paints); var passage = WallPrefab(4.9f, paints); var door = DoorPrefab(paints);
            var root = new GameObject(WallRoot).transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Add modular perimeter walls");
            var groups = new Dictionary<string, Transform>();
            int perimeter = 0, tall = 0;
            foreach (var cell in cells.OrderBy(c => c.y).ThenBy(c => c.x))
                foreach (var delta in Directions)
                {
                    if (cells.Contains(cell + delta)) continue;
                    float edgeX = cell.x + .5f + delta.x * .5f, edgeZ = cell.y + .5f + delta.y * .5f;
                    bool tunnel = Passage(edgeX, edgeZ);
                    string area = edgeZ > 71 ? "Boss chamber" : tunnel ? "Winding passage" : edgeZ >= 38 ? "Second arena and holding rooms" : "Entrance and first arena";
                    if (!groups.ContainsKey(area)) groups[area] = Group(area, root);
                    var wall = (GameObject)PrefabUtility.InstantiatePrefab(tunnel ? passage : normal, groups[area]);
                    wall.name = "Wall [" + cell.x + ", " + cell.y + "] / " + delta;
                    // Keep the inside wall face on the floor edge, preserving the full corridor width.
                    wall.transform.position = new Vector3(edgeX + delta.x * Depth / 2, 0, edgeZ + delta.y * Depth / 2);
                    wall.transform.rotation = Quaternion.Euler(0, delta.x == 0 ? 0 : 90, 0);
                    perimeter++; if (tunnel) tall++;
                }
            var rooms = JsonUtility.FromJson<Rooms>(File.ReadAllText(Root + "/Data/SecurityRoomLayout.json"));
            var doorGroup = Group("Holding room doorway walls / 3m clear", root);
            foreach (var room in rooms.rooms)
            {
                var frame = (GameObject)PrefabUtility.InstantiatePrefab(door, doorGroup);
                frame.name = room.id + " / open doorway walls";
                frame.transform.SetPositionAndRotation(new Vector3(room.doorX, 0, room.doorZ), Quaternion.Euler(0, 90, 0));
            }
            Physics.SyncTransforms();
            int boundaryHits = 0;
            foreach (var cell in cells)
                foreach (var d in Directions)
                    if (!cells.Contains(cell + d) && Physics.Raycast(new Vector3(cell.x + .5f, 1.3f, cell.y + .5f), new Vector3(d.x, 0, d.y), out var hit, 1.2f) && hit.transform.IsChildOf(root)) boundaryHits++;
            int blockedCentres = cells.Count(c => Physics.OverlapSphere(new Vector3(c.x + .5f, 1.3f, c.y + .5f), .1f).Any(h => h.transform.IsChildOf(root)));
            var route = new[] { new Vector3(0,1.3f,-4), new Vector3(0,1.3f,14), new Vector3(0,1.3f,21), new Vector3(8,1.3f,21), new Vector3(21,1.3f,21), new Vector3(21,1.3f,35), new Vector3(9,1.3f,35), new Vector3(9,1.3f,46), new Vector3(37,1.3f,46), new Vector3(42,1.3f,46), new Vector3(42,1.3f,57), new Vector3(42,1.3f,66), new Vector3(42,1.3f,84) };
            int blockedRoute = 0;
            for (int i = 1; i < route.Length; i++)
            {
                Vector3 v = route[i] - route[i-1];
                if (Physics.SphereCastAll(route[i-1], .3f, v.normalized, v.magnitude).Any(h => h.transform.IsChildOf(root))) blockedRoute++;
            }
            int clearDoors = rooms.rooms.Count(r => !Physics.SphereCastAll(new Vector3(r.doorX - 1.5f, 1.3f, r.doorZ), .3f, Vector3.right, 3).Any(h => h.transform.IsChildOf(root)));
            bool unchanged = floor.GetComponentsInChildren<BoxCollider>().Length == tileColliders.Length;
            var report = new[] { "Backup: " + backup, "Editable perimeter modules: " + perimeter + " (" + tall + " taller passage modules)", "Open doorway wall frames: " + rooms.rooms.Length + " / opening width 3m, height 3m", "Boundary collider hits: " + boundaryHits + " / " + perimeter, "Floor centres blocked by walls: " + blockedCentres, "Mission route segments blocked: " + blockedRoute, "Open room entrances: " + clearDoors + " / " + rooms.rooms.Length, "Original floor tiles/colliders: " + tileColliders.Length + " / retained: " + unchanged, "Only wall geometry and wall colliders added; no doors, props, agents or lights added." };
            File.WriteAllLines(Root + "/Reports/WallBuild.txt", report);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            if (boundaryHits != perimeter || blockedCentres != 0 || blockedRoute != 0 || clearDoors != rooms.rooms.Length || !unchanged)
                throw new InvalidOperationException("Wall validation needs attention; inspect Reports/WallBuild.txt.");
            Selection.activeGameObject = root.gameObject;
            var preview = UnityEngine.Object.FindFirstObjectByType<DescentMapPreview>(); if (preview != null) preview.ShowShot(0);
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.LookAt(new Vector3(0, 1.5f, 12), Quaternion.Euler(40, -30, 0), 25);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            return string.Join("\n", report);
        }
    }
}
