using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Skybreak.DisruptorEditor
{
    /// <summary>
    /// Renders the prefab in URP at sampled animation poses, for documentation and visual checks.
    /// Needs a graphics device: batch mode without -nographics.
    ///   -executeMethod Skybreak.DisruptorEditor.RiftDisruptorPreviewRenderer.Render -quit
    /// </summary>
    public static class RiftDisruptorPreviewRenderer
    {
        const string OutFolder = "Docs/GV/AgentMovement/Images";
        const int W = 640, H = 760;

        struct Shot { public string file, clip; public float time, yaw, distance, height; }

        [MenuItem("Skybreak/Rift Disruptor/Render previews")]
        public static void Render()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var temp = new List<Object>();
            Material Mat(Color c, float smooth = .2f)
            {
                var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); temp.Add(m); return m;
            }
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.transform.localScale = Vector3.one * 2;
            floor.GetComponent<Renderer>().sharedMaterial = Mat(new Color(.16f, .15f, .14f), .35f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = new Vector3(0, 3, -4); wall.transform.localScale = new Vector3(20, 6, .5f);
            wall.GetComponent<Renderer>().sharedMaterial = Mat(new Color(.2f, .19f, .18f));
            var key = new GameObject("Key").AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 2.2f; key.color = new Color(1f, .78f, .55f);
            key.transform.rotation = Quaternion.Euler(40, -150, 0); key.shadows = LightShadows.Soft;
            var rim = new GameObject("Rim").AddComponent<Light>(); rim.type = LightType.Directional; rim.intensity = .9f; rim.color = new Color(.4f, .8f, 1f);
            rim.transform.rotation = Quaternion.Euler(25, 20, 0);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.2f, .22f, .27f);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RiftDisruptorAssetBuilder.PrefabPath);
            var robot = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var animator = robot.GetComponentInChildren<Animator>();
            var model = animator.gameObject;

            var cam = new GameObject("Preview camera").AddComponent<Camera>();
            cam.fieldOfView = 30; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.035f, .04f, .055f);
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;

            var shots = new[]
            {
                new Shot { file = "hero_idle", clip = "RD_Idle", time = .3f, yaw = 30, distance = 6.4f, height = 1.5f },
                new Shot { file = "front_idle", clip = "RD_Idle", time = 0, yaw = 0, distance = 6.4f, height = 1.3f },
                new Shot { file = "walk_contact", clip = "RD_Walk", time = 0, yaw = 90, distance = 6.4f, height = 1.2f },
                new Shot { file = "walk_passing", clip = "RD_Walk", time = .225f, yaw = 90, distance = 6.4f, height = 1.2f },
                new Shot { file = "run_stride", clip = "RD_Run", time = .155f, yaw = 70, distance = 6.4f, height = 1.2f },
                new Shot { file = "charge_telegraph", clip = "RD_Charge", time = .2f, yaw = 35, distance = 6.4f, height = 1.4f },
                new Shot { file = "release_recoil", clip = "RD_Release", time = .06f, yaw = 55, distance = 6.4f, height = 1.4f },
                new Shot { file = "death_final", clip = "RD_Death", time = 1.6f, yaw = 50, distance = 6.4f, height = 1.4f },
            };
            Directory.CreateDirectory(OutFolder);
            Object.DestroyImmediate(Capture(cam, rt));   // warm-up: the first URP frame has no lighting/shadow state yet
            var sheet = new Texture2D(W / 2 * 4, H / 2 * 2, TextureFormat.RGB24, false);
            for (int i = 0; i < shots.Length; i++)
            {
                var s = shots[i];
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{RiftDisruptorAssetBuilder.AnimationFolder}/{s.clip}.anim");
                clip.SampleAnimation(model, s.time);
                Vector3 focus = new Vector3(0, s.clip == "RD_Death" ? .8f : 1.1f, 0);
                cam.transform.position = focus + Quaternion.Euler(0, s.yaw, 0) * Vector3.back * -s.distance + Vector3.up * (s.height - focus.y);
                cam.transform.LookAt(focus);
                var image = Capture(cam, rt);
                File.WriteAllBytes(Path.Combine(OutFolder, $"RiftDisruptor_{s.file}.png"), image.EncodeToPNG());
                // half-size copy into the contact sheet
                int cx = i % 4, cy = 1 - i / 4;
                for (int y = 0; y < H / 2; y++)
                    for (int x = 0; x < W / 2; x++)
                        sheet.SetPixel(cx * W / 2 + x, cy * H / 2 + y, image.GetPixelBilinear((x + .5f) / (W / 2f), (y + .5f) / (H / 2f)));
                Object.DestroyImmediate(image);
            }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(OutFolder, "RiftDisruptor_pose_sheet.png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt);
            foreach (var o in temp) Object.DestroyImmediate(o);
            Debug.Log("RIFT_DISRUPTOR_PREVIEWS " + Path.GetFullPath(OutFolder));
        }

        static Texture2D Capture(Camera cam, RenderTexture rt)
        {
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else cam.Render();
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            RenderTexture.active = previous;
            return tex;
        }
    }
}
