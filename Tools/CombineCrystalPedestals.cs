using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class CombineCrystalPedestals
{
    const string ScenePath = "Assets/Scenes/GameLevel/SkybreakMain.unity";
    const string PrefabPath = "Assets/Prefabs/Environment/Dungeon/CrystalPedestal.prefab";
    const string ModelPath = "Assets/Models/Custom/CrystalPedestalCombined/CrystalPedestalCombinedTop.fbx";
    const string MeshDir = "Assets/Models/Custom/CrystalPedestalCombined/Meshes";
    const string PreviewDir = "ArtSource/Shared/CrystalPedestalCombined/Previews";

    static Transform[] Pedestals() => UnityEngine.SceneManagement.SceneManager.GetActiveScene()
        .GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
        .Where(t => t.name == "CrystalPedestal").ToArray();

    static Mesh BakeMesh(MeshFilter source, string name)
    {
        var mesh = UnityEngine.Object.Instantiate(source.sharedMesh);
        mesh.name = name;
        var matrix = source.transform.localToWorldMatrix;
        mesh.vertices = mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
        var normalMatrix = matrix.inverse.transpose;
        mesh.normals = mesh.normals.Select(n => normalMatrix.MultiplyVector(n).normalized).ToArray();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        Unwrapping.GenerateSecondaryUVSet(mesh);
        string path = MeshDir + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) AssetDatabase.CreateAsset(mesh, path);
        else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; }
        return mesh;
    }

    static void Visual(Transform parent, string name, Mesh mesh, Material material, bool preserveMaterial)
    {
        var child = parent.Find(name);
        if (child == null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            child = go.transform;
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
        }
        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;
        var filter = child.GetComponent<MeshFilter>();
        var renderer = child.GetComponent<MeshRenderer>();
        filter.sharedMesh = mesh;
        if (!preserveMaterial) renderer.sharedMaterial = material;
        // New upper geometry uses the existing probes; lower baked renderers stay intact.
        GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
        renderer.receiveGI = ReceiveGI.LightProbes;
        renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
        renderer.lightmapIndex = -1;
        EditorUtility.SetDirty(filter);
        EditorUtility.SetDirty(renderer);
        if (PrefabUtility.IsPartOfPrefabInstance(child))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(child);
            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
    }

    static void UpdateTop(Transform parent, Dictionary<string, Mesh> meshes, Material step, Material slab, Material brass)
    {
        Visual(parent, "Copper setting", meshes["Brass socket"], brass, true);
        Visual(parent, "Faceted crystal", meshes["Faceted crystal"], null, true);
        Visual(parent, "Combined stepped stone", meshes["Stepped stone"], step, false);
        Visual(parent, "Combined upper slab", meshes["Upper slab"], slab, false);
    }

    public static string Main()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || Lightmapping.isRunning || scene.path != ScenePath)
            throw new Exception("Use the main map in Edit Mode without a lighting bake.");
        var instances = Pedestals();
        if (instances.Length != 8) throw new Exception("Expected eight existing pedestal instances.");
        string backup = "Tools/SceneSnapshots/CombinedPedestal_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);
        if (!EditorSceneManager.SaveScene(scene, backup + "/SkybreakMain.unity", true))
            throw new Exception("Scene backup failed.");
        File.Copy(PrefabPath, backup + "/CrystalPedestal.prefab", false);

        var originalEffects = instances.SelectMany(t => t.GetComponentsInChildren<Light>(true))
            .ToDictionary(l => l, l => EditorJsonUtility.ToJson(l));
        var particleSystems = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ParticleSystem>(true))
            .Where(p => p.name == "Sparkles / CrystalPedestal").ToArray();
        var particleSettings = particleSystems.ToDictionary(p => p, p => EditorJsonUtility.ToJson(p));
        var originalMaterials = instances.ToDictionary(t => t, t => t.Find("Faceted crystal").GetComponent<Renderer>().sharedMaterial);
        var originalCollider = instances.ToDictionary(t => t, t => EditorJsonUtility.ToJson(t.GetComponent<BoxCollider>()));

        Directory.CreateDirectory(MeshDir);
        Directory.CreateDirectory(PreviewDir);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) throw new Exception("Combined FBX failed to import.");
        var meshes = model.GetComponentsInChildren<MeshFilter>().ToDictionary(f => f.name, f => BakeMesh(f, f.name));
        if (meshes.Count != 4 || meshes["Faceted crystal"].bounds.max.y < 3.2f)
            throw new Exception("Unexpected imported top scale or geometry.");
        var slab = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Environment/Dungeon/StoneLight.mat");
        var brass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Environment/Dungeon/Brass.mat");
        const string stepPath = "Assets/Models/Custom/CrystalPedestalCombined/SteppedStone.mat";
        var step = AssetDatabase.LoadAssetAtPath<Material>(stepPath);
        if (step == null)
        {
            step = new Material(slab) { name = "Combined stepped stone" };
            var color = slab.GetColor("_BaseColor");
            step.SetColor("_BaseColor", new Color(color.r * .78f, color.g * .78f, color.b * .78f, color.a));
            AssetDatabase.CreateAsset(step, stepPath);
        }
        var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            UpdateTop(prefabRoot.transform, meshes, step, slab, brass);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefabRoot); }

        foreach (var t in instances)
        {
            Undo.RegisterFullObjectHierarchyUndo(t.gameObject, "Combine crystal pedestal models");
            UpdateTop(t, meshes, step, slab, brass);
            var crystal = t.Find("Faceted crystal").GetComponent<Renderer>();
            var sparkles = particleSystems.OrderBy(p => (new Vector2(p.transform.position.x, p.transform.position.z) - new Vector2(t.position.x, t.position.z)).sqrMagnitude).FirstOrDefault();
            if (sparkles != null && Vector2.Distance(new Vector2(sparkles.transform.position.x, sparkles.transform.position.z), new Vector2(t.position.x, t.position.z)) < .2f)
            {
                Undo.RecordObject(sparkles.transform, "Align existing crystal sparkles");
                sparkles.transform.position = new Vector3(sparkles.transform.position.x, crystal.bounds.center.y, sparkles.transform.position.z);
            }
            if (crystal.sharedMaterial != originalMaterials[t]) throw new Exception("Crystal glow material changed.");
            if (EditorJsonUtility.ToJson(t.GetComponent<BoxCollider>()) != originalCollider[t]) throw new Exception("Pedestal collision changed.");
            if (t.GetComponentsInChildren<Renderer>().Any(r => r.sharedMaterials.Any(m => m == null)))
                throw new Exception("Missing pedestal material.");
        }
        if (originalEffects.Any(p => p.Key == null || EditorJsonUtility.ToJson(p.Key) != p.Value))
            throw new Exception("Existing light settings changed.");
        if (particleSettings.Any(p => p.Key == null || EditorJsonUtility.ToJson(p.Key) != p.Value))
            throw new Exception("Existing sparkle settings changed.");
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Map save failed.");
        string result = "Updated eight pedestal instances and the shared prefab. Original lower body and cornice retained. Crystal glow materials, eight sparkle systems and existing lights retained. Original collider sizes retained. New upper geometry uses existing light probes. Backup: " + backup;
        File.WriteAllText(PreviewDir + "/Installation.txt", result);
        return result;
    }

    public static string Capture()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != ScenePath) throw new Exception("Use the main map.");
        var t = Pedestals().OrderBy(x => Vector3.Distance(x.position, new Vector3(-10.3f, 0, 17.04f))).First();
        bool live = EditorApplication.isPlaying;
        var go = live ? null : new GameObject("Temporary combined pedestal preview");
        var cam = live ? Camera.main : go.AddComponent<Camera>();
        if (cam == null) throw new Exception("No live camera.");
        if (!live && Camera.main != null) cam.CopyFrom(Camera.main);
        var oldPosition = cam.transform.position;
        var oldRotation = cam.transform.rotation;
        var oldTarget = cam.targetTexture;
        float oldFov = cam.fieldOfView, oldNear = cam.nearClipPlane, oldFar = cam.farClipPlane;
        if (!live) cam.enabled = false;
        cam.fieldOfView = 48;
        cam.nearClipPlane = .05f;
        cam.farClipPlane = 200;
        if (!live) cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        cam.transform.position = t.position + new Vector3(2.7f, 2.75f, -4.0f);
        cam.transform.LookAt(t.position + Vector3.up * 1.7f);
        var rt = RenderTexture.GetTemporary(1000, 1100, 24, RenderTextureFormat.DefaultHDR);
        var previous = RenderTexture.active;
        try
        {
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(PreviewDir + (live ? "/InMapWithEffects.png" : "/InMap.png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
        finally
        {
            cam.targetTexture = oldTarget;
            cam.transform.SetPositionAndRotation(oldPosition, oldRotation);
            cam.fieldOfView = oldFov;
            cam.nearClipPlane = oldNear;
            cam.farClipPlane = oldFar;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
        return PreviewDir + (live ? "/InMapWithEffects.png" : "/InMap.png");
    }
}
