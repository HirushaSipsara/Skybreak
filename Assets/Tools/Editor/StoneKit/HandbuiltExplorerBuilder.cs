using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Skybreak.Descent;

namespace Skybreak.Handbuilt.Editor
{
    public static class HandbuiltExplorerBuilder
    {

        const string ScenePath = Skybreak.Editor.SkybreakScenePaths.Main;

        [MenuItem("Skybreak/World Editing/Add controllable explorer")]
        public static string Add()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != ScenePath)
                throw new InvalidOperationException("Open the handbuilt map in Edit Mode first.");
            if (UnityEngine.Object.FindFirstObjectByType<DescentCharacterController>() != null)
                throw new InvalidOperationException("An explorer already exists in this scene.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/ArcExplorer.prefab");
            var preview = UnityEngine.Object.FindFirstObjectByType<DescentMapPreview>();
            var camera = preview != null ? preview.view : Camera.main;
            if (prefab == null || camera == null) throw new InvalidOperationException("Explorer prefab or inspection camera is missing.");
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
            string backup = Skybreak.Editor.SkybreakScenePaths.BackupMainScene("BeforeExplorer");

            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Add handbuilt map explorer");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(player, "Add Arc explorer");
            player.name = "08 / ARC / Walk around the map";
            player.transform.SetPositionAndRotation(new Vector3(0, .12f, -4), Quaternion.identity);
            var controller = player.GetComponent<DescentCharacterController>();
            if (controller == null) throw new InvalidOperationException("The explorer prefab is missing its movement controller.");
            controller.viewCamera = camera;
            controller.environmentMask = ~(1 << 8);
            foreach (var t in player.GetComponentsInChildren<Transform>()) t.gameObject.layer = 8;
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
            Undo.RecordObject(camera, "Configure exploration camera");
            camera.fieldOfView = 65; camera.nearClipPlane = .08f;
            if (preview != null) { Undo.RecordObject(preview, "Disable flying inspection controls"); preview.enabled = false; }
            Physics.SyncTransforms();
            var pivot = player.transform.position + Vector3.up * 1.6f;
            var rotation = Quaternion.Euler(10, 0, 0);
            var offset = rotation * new Vector3(.65f, 0, -controller.cameraDistance);
            float distance = offset.magnitude;
            if (Physics.SphereCast(pivot, .22f, offset.normalized, out var cameraHit, distance, controller.environmentMask, QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(.25f, cameraHit.distance - .10f);
            camera.transform.SetPositionAndRotation(pivot + offset.normalized * distance, rotation);
            bool floorHit = Physics.Raycast(player.transform.position + Vector3.up, Vector3.down, out var hit, 3) && Mathf.Abs(hit.point.y) < .01f;
            if (!floorHit) throw new InvalidOperationException("Explorer spawn is not above the floor.");
            Undo.CollapseUndoOperations(undo);
            Selection.activeGameObject = player;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the explorer scene.");
            string report = "Backup: " + backup + "\nExplorer: " + player.name + "\nReused ArcExplorer prefab; original prefab and controller source unchanged.\nSpawn: (0, 0.12, -4); floor support checked.\nOne CharacterController, one existing camera. Flying preview disabled.\nWASD / arrows move; mouse look; Shift run; Space jump; Esc releases cursor.\nFloor, walls and room openings unchanged. No combat components added.\n";
            File.WriteAllText("Docs/GV/LevelDesign/Reports/StoneKit/ExplorerBuild.txt", report);
            return report;
        }
    }
}
