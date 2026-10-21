using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Skybreak.AI.Disruptor;
using Skybreak.Movement;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Skybreak.DisruptorEditor
{
    /// <summary>
    /// Builds every Unity asset for the Rift Disruptor from the imported FBX, reproducibly:
    ///   import settings and URP materials, the paint-wear texture, seven in-place transform clips,
    ///   the Animator Controller and the gameplay prefab.
    /// Menu Skybreak/Rift Disruptor/Build GV assets, or batch:
    ///   -executeMethod Skybreak.DisruptorEditor.RiftDisruptorAssetBuilder.BuildAll -quit
    /// </summary>
    public static class RiftDisruptorAssetBuilder
    {
        public const string ModelPath = "Assets/Models/Custom/RiftDisruptor/RiftDisruptor.fbx";
        public const string MaterialFolder = "Assets/Materials/Characters/RiftDisruptor";
        public const string TexturePath = "Assets/Textures/Characters/RiftDisruptor/RD_Wear.png";
        public const string AnimationFolder = "Assets/Animations/Disruptor";
        public const string ControllerPath = AnimationFolder + "/RiftDisruptor.controller";
        public const string PrefabPath = "Assets/Prefabs/Enemies/RiftDisruptor/RiftDisruptor.prefab";

        // Body dimensions (metres). The model is 2.28 m to the antenna tip, 1.33 m across the pauldrons.
        public const float BodyRadius = 0.55f, BodyHeight = 2.1f, EyeHeight = 1.6f;

        [MenuItem("Skybreak/Rift Disruptor/Build GV assets")]
        public static void BuildAll()
        {
            ConfigureModel();
            var materials = BuildMaterials();
            RemapMaterials(materials);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            ValidateModel(model);
            var clips = BuildClips(model);
            var controller = BuildController(clips);
            BuildPrefab(model, controller, materials["RD_Glow"]);
            AssetDatabase.SaveAssets();
            Debug.Log("RIFT_DISRUPTOR_ASSETS_BUILT");
        }

        // ------------------------------------------------------------------ import
        static void ConfigureModel()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath) ?? throw new InvalidOperationException("Missing " + ModelPath + ": run the Blender build first.");
            importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
            importer.preserveHierarchy = true;                         // keep the pivot empties as transforms
            importer.importCameras = false; importer.importLights = false; importer.importVisibility = false;
            importer.importAnimation = false;                          // clips are authored in Unity on the rigid hierarchy
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.meshCompression = ModelImporterMeshCompression.Low;
            importer.isReadable = false;                               // no CPU copy of the mesh in memory
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.SaveAndReimport();
        }

        static Dictionary<string, Material> BuildMaterials()
        {
            Directory.CreateDirectory(MaterialFolder);
            var wear = BuildWearTexture();
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            Material Make(string name, Color colour, float metallic, float smoothness, bool worn, Color? emission = null)
            {
                string path = $"{MaterialFolder}/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(lit) { name = name }; AssetDatabase.CreateAsset(m, path); }
                m.shader = lit;
                m.SetColor("_BaseColor", colour);
                m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smoothness);
                m.SetTexture("_BaseMap", worn ? wear : null);
                m.SetTextureScale("_BaseMap", Vector2.one * 3);   // finer chips than one tile per UV island
                m.enableInstancing = true;
                if (emission.HasValue)
                {
                    m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;   // no baked/realtime GI cost
                }
                else m.DisableKeyword("_EMISSION");
                EditorUtility.SetDirty(m);
                return m;
            }
            return new Dictionary<string, Material>
            {
                ["RD_TealPaint"] = Make("RD_TealPaint", new Color(.17f, .32f, .33f), .35f, .45f, true),
                ["RD_CreamPlate"] = Make("RD_CreamPlate", new Color(.74f, .70f, .60f), .1f, .35f, true),
                ["RD_DarkMetal"] = Make("RD_DarkMetal", new Color(.15f, .15f, .16f), .8f, .5f, true),
                ["RD_Bronze"] = Make("RD_Bronze", new Color(.72f, .52f, .27f), 1f, .62f, false),
                ["RD_Glow"] = Make("RD_Glow", new Color(.45f, .2f, .04f), 0, .5f, false, new Color(1f, .5f, .1f) * 2.4f),
            };
        }

        /// <summary>Procedural 256x256 grime/chip mask (white = clean paint), so no external texture is needed.</summary>
        static Texture2D BuildWearTexture()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TexturePath));
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var rng = new System.Random(3032);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float n = Mathf.PerlinNoise(x * .035f, y * .035f) * .6f + Mathf.PerlinNoise(x * .13f + 40, y * .13f + 17) * .4f;
                    float v = Mathf.Lerp(.9f, 1f, n);
                    pixels[y * size + x] = new Color(v, v, v, 1);
                }
            // chips: small irregular dark flecks, denser where the noise is high (edges of plates read as worn)
            for (int i = 0; i < 260; i++)
            {
                int cx = rng.Next(size), cy = rng.Next(size);
                float r = 0.6f + (float)rng.NextDouble() * 1.4f;
                for (int y = -4; y <= 4; y++)
                    for (int x = -4; x <= 4; x++)
                    {
                        float d = Mathf.Sqrt(x * x + y * y * 1.6f);
                        if (d > r) continue;
                        int px = (cx + x + size) % size, py = (cy + y + size) % size;
                        float dark = Mathf.Lerp(.55f, .8f, d / r);
                        var c = pixels[py * size + px]; pixels[py * size + px] = new Color(Mathf.Min(c.r, dark), Mathf.Min(c.g, dark * .97f), Mathf.Min(c.b, dark * .93f), 1);
                    }
            }
            tex.SetPixels(pixels); tex.Apply();
            File.WriteAllBytes(TexturePath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(TexturePath);
            var ti = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            ti.sRGBTexture = true; ti.mipmapEnabled = true; ti.maxTextureSize = 256; ti.wrapMode = TextureWrapMode.Repeat;
            ti.textureCompression = TextureImporterCompression.Compressed;   // block-compressed on the GPU
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        static void RemapMaterials(Dictionary<string, Material> materials)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            foreach (var kv in materials) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            importer.SaveAndReimport();
        }

        static void ValidateModel(GameObject model)
        {
            var names = model.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToHashSet();
            foreach (var bone in Bones) if (!names.Contains(bone)) throw new InvalidOperationException("Model is missing joint " + bone);
            var instance = UnityEngine.Object.Instantiate(model);
            try
            {
                var bounds = new Bounds(instance.transform.position, Vector3.zero);
                foreach (var r in instance.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                var shoulderL = Find(instance.transform, "Shoulder_L").position;
                var visor = Find(instance.transform, "Head").position;
                string report = $"RIFT_DISRUPTOR_MODEL bounds min {bounds.min} max {bounds.max}; Shoulder_L {shoulderL}; renderers {instance.GetComponentsInChildren<Renderer>().Length}; " +
                                $"missing materials {instance.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Count(m => m == null || !m.name.StartsWith("RD_"))}";
                Debug.Log(report);
                if (Mathf.Abs(bounds.min.y) > 0.05f || bounds.max.y < 2f || bounds.max.y > 2.5f) throw new InvalidOperationException("Unexpected model size/origin: " + report);
                if (shoulderL.x > 0) throw new InvalidOperationException("Model is mirrored or facing -Z (left shoulder on +X): " + report);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        // ------------------------------------------------------------------ animation
        static readonly string[] Bones =
        {
            "Hips", "Torso", "Head", "Shoulder_L", "Elbow_L", "Wrist_L", "FingerA_L", "FingerB_L", "Thumb_L",
            "Shoulder_R", "Elbow_R", "Cannon_R", "Hip_L", "Knee_L", "Ankle_L", "Hip_R", "Knee_R", "Ankle_R",
        };

        static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);

        /// <summary>
        /// One clip under construction. Rotations are offsets in degrees from the bind pose about the bone's
        /// local axes (Unity: +X swings a hanging limb backwards, -X forwards; +X tips the torso forward).
        /// Every bone gets all three Euler channels in every clip, so the Animator writes the whole rig each
        /// frame and procedural offsets added afterwards cannot accumulate.
        /// </summary>
        sealed class ClipBuilder
        {
            public readonly string Name; public readonly float Length; public readonly bool Loop;
            readonly Dictionary<(string bone, int axis), List<Keyframe>> rot = new Dictionary<(string, int), List<Keyframe>>();
            readonly Dictionary<(string bone, int axis), List<Keyframe>> pos = new Dictionary<(string, int), List<Keyframe>>();
            public ClipBuilder(string name, float length, bool loop) { Name = name; Length = length; Loop = loop; }

            public ClipBuilder R(string bone, char axis, params float[] timeValue) { Add(rot, bone, axis, timeValue); return this; }
            public ClipBuilder P(string bone, char axis, params float[] timeValue) { Add(pos, bone, axis, timeValue); return this; }
            /// <summary>Same value for the whole clip.</summary>
            public ClipBuilder Hold(string bone, char axis, float value) => R(bone, axis, 0, value, Length, value);
            /// <summary>Samples a function at n points (used for ankle compensation and shake).</summary>
            public ClipBuilder Sampled(string bone, char axis, int n, Func<float, float> f)
            {
                var tv = new List<float>();
                for (int i = 0; i <= n; i++) { float t = Length * i / n; tv.Add(t); tv.Add(f(t)); }
                return R(bone, axis, tv.ToArray());
            }
            public float Value(string bone, char axis, float t)
            {
                if (!rot.TryGetValue((bone, axis - 'x'), out var keys)) return 0;
                return new AnimationCurve(keys.ToArray()).Evaluate(t);
            }

            static void Add(Dictionary<(string, int), List<Keyframe>> map, string bone, char axis, float[] tv)
            {
                var keys = new List<Keyframe>();
                for (int i = 0; i + 1 < tv.Length; i += 2) keys.Add(new Keyframe(tv[i], tv[i + 1]));
                map[(bone, axis - 'x')] = keys;
            }

            public AnimationClip Build(Transform model)
            {
                var clip = new AnimationClip { name = Name, frameRate = 30 };
                foreach (var bone in Bones)
                {
                    var t = Find(model, bone);
                    string path = AnimationUtility.CalculateTransformPath(t, model);
                    Vector3 bindEuler = t.localEulerAngles, bindPos = t.localPosition;
                    for (int axis = 0; axis < 3; axis++)
                    {
                        clip.SetCurve(path, typeof(Transform), "localEulerAnglesRaw." + "xyz"[axis], Curve(rot, bone, axis, bindEuler[axis]));
                        if (bone == "Hips") clip.SetCurve(path, typeof(Transform), "m_LocalPosition." + "xyz"[axis], Curve(pos, bone, axis, bindPos[axis]));
                    }
                }
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = Loop; settings.loopBlend = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                return clip;
            }

            AnimationCurve Curve(Dictionary<(string, int), List<Keyframe>> map, string bone, int axis, float bind)
            {
                var keys = map.TryGetValue((bone, axis), out var k) ? k : new List<Keyframe> { new Keyframe(0, 0), new Keyframe(Length, 0) };
                var curve = new AnimationCurve(keys.Select(key => new Keyframe(key.time, key.value + bind)).ToArray());
                for (int i = 0; i < curve.length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                }
                return curve;
            }
        }

        // Gait helpers: a leg's hip swing and knee flex over one cycle, phase-shifted for the other leg.
        static float Wrap(float t, float length) => ((t % length) + length) % length;

        static ClipBuilder Gait(string name, float cycle, float hipSwing, float kneeStance, float kneeSwing, float bob, float lean, float armSwing, float cannonSwing, float elbowL, float elbowR)
        {
            var c = new ClipBuilder(name, cycle, true);
            float h = cycle / 2;
            // Left leg: forward (contact) at 0, back at half cycle. Right leg is half a cycle later.
            float Hip(float t) => -hipSwing * Mathf.Cos(2 * Mathf.PI * t / cycle);
            float Knee(float t)
            {
                float p = Wrap(t, cycle) / cycle;                 // 0..1, 0 = contact
                if (p < .5f) return kneeStance * Mathf.Sin(p * 2 * Mathf.PI);           // stance: small load bend
                return kneeSwing * Mathf.Sin((p - .5f) * 2 * Mathf.PI);                // swing: lift the foot
            }
            c.Sampled("Hip_L", 'x', 16, Hip).Sampled("Hip_R", 'x', 16, t => Hip(t + h));
            c.Sampled("Knee_L", 'x', 16, Knee).Sampled("Knee_R", 'x', 16, t => Knee(t + h));
            // Ankles keep the sole roughly parallel to the floor: undo most of hip + knee rotation.
            c.Sampled("Ankle_L", 'x', 16, t => -0.85f * (Hip(t) + Knee(t))).Sampled("Ankle_R", 'x', 16, t => -0.85f * (Hip(t + h) + Knee(t + h)));
            // Body: drops at each contact (twice per cycle), sways over the stance leg, counter-twists.
            c.P("Hips", 'y', 0, -bob, cycle * .25f, 0, h, -bob, cycle * .75f, 0, cycle, -bob);
            c.R("Hips", 'z', 0, 2.5f, h, -2.5f, cycle, 2.5f);
            c.R("Torso", 'y', 0, -5, h, 5, cycle, -5).Hold("Torso", 'x', lean);
            c.R("Head", 'y', 0, 3, h, -3, cycle, 3).Hold("Head", 'x', -lean * .6f);
            // Arms counter-swing their opposite leg; the heavy cannon arm swings less.
            c.R("Shoulder_L", 'x', 0, armSwing, h, -armSwing, cycle, armSwing);
            c.R("Shoulder_R", 'x', 0, -cannonSwing, h, cannonSwing, cycle, -cannonSwing);
            c.Hold("Elbow_L", 'x', elbowL).Hold("Elbow_R", 'x', elbowR);
            c.Hold("FingerA_L", 'x', -8).Hold("FingerB_L", 'x', -8).Hold("Thumb_L", 'x', 8);
            return c;
        }

        static Dictionary<string, AnimationClip> BuildClips(GameObject modelAsset)
        {
            Directory.CreateDirectory(AnimationFolder);
            var model = UnityEngine.Object.Instantiate(modelAsset).transform;
            try
            {
                var specs = new List<ClipBuilder>
                {
                    Idle(),
                    Gait("RD_Walk", 0.9f, hipSwing: 22, kneeStance: 12, kneeSwing: 42, bob: .035f, lean: 4, armSwing: 14, cannonSwing: 6, elbowL: -18, elbowR: -22),
                    Gait("RD_Run", 0.62f, hipSwing: 34, kneeStance: 20, kneeSwing: 72, bob: .06f, lean: 14, armSwing: 28, cannonSwing: 10, elbowL: -55, elbowR: -60),
                    Charge(), Release(), Hurt(), Death(),
                };
                var result = new Dictionary<string, AnimationClip>();
                foreach (var spec in specs)
                {
                    var clip = spec.Build(model);
                    string path = $"{AnimationFolder}/{spec.Name}.anim";
                    var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (existing != null) { EditorUtility.CopySerialized(clip, existing); clip = existing; }
                    else AssetDatabase.CreateAsset(clip, path);
                    result[spec.Name] = clip;
                }
                return result;
            }
            finally { UnityEngine.Object.DestroyImmediate(model.gameObject); }
        }

        static ClipBuilder Idle()
        {
            const float L = 2.4f;
            var c = new ClipBuilder("RD_Idle", L, true);
            c.P("Hips", 'y', 0, 0, L / 2, -.014f, L, 0);
            c.R("Torso", 'x', 0, 2, L / 2, 3.5f, L, 2);
            c.R("Head", 'y', 0, 0, L * .3f, 9, L * .55f, 9, L * .8f, -6, L, 0).R("Head", 'x', 0, 0, L / 2, 3, L, 0);
            c.R("Shoulder_L", 'x', 0, 1, L / 2, -2, L, 1).R("Shoulder_R", 'x', 0, -1, L / 2, 1.5f, L, -1);
            c.R("Shoulder_L", 'z', 0, -3, L / 2, -4, L, -3).R("Shoulder_R", 'z', 0, 3, L / 2, 4, L, 3);   // arms slightly out
            c.Hold("Elbow_L", 'x', -12).Hold("Elbow_R", 'x', -16);
            c.R("FingerA_L", 'x', 0, -6, L * .4f, -6, L * .5f, -18, L * .6f, -6, L, -6).R("FingerB_L", 'x', 0, -6, L * .45f, -6, L * .55f, -18, L * .65f, -6, L, -6).Hold("Thumb_L", 'x', 8);
            c.Hold("Hip_L", 'x', -3).Hold("Hip_R", 'x', -3).Hold("Knee_L", 'x', 6).Hold("Knee_R", 'x', 6).Hold("Ankle_L", 'x', -3).Hold("Ankle_R", 'x', -3);
            c.Hold("Hip_L", 'z', -2).Hold("Hip_R", 'z', 2);   // slightly wide stance
            return c;
        }

        // Telegraph: drop into a braced crouch, swing the cannon up to point forward, open the claw,
        // and tremble with the building charge. Readable from across a room, which is the point.
        static ClipBuilder Charge()
        {
            const float L = 0.8f;
            var c = new ClipBuilder("RD_Charge", L, true);
            c.Hold("Hips", 'x', 0).P("Hips", 'y', 0, -.13f, L, -.13f);
            c.Hold("Hip_L", 'x', -32).Hold("Hip_R", 'x', -18).Hold("Knee_L", 'x', 48).Hold("Knee_R", 'x', 40).Hold("Ankle_L", 'x', -16).Hold("Ankle_R", 'x', -22);
            c.Hold("Hip_L", 'z', -6).Hold("Hip_R", 'z', 6);
            c.Sampled("Torso", 'x', 16, t => 6 + 1.2f * Mathf.Sin(t * 70));
            c.Sampled("Shoulder_R", 'x', 16, t => -82 + 1.5f * Mathf.Sin(t * 55)).Hold("Shoulder_R", 'z', 6);
            c.Hold("Elbow_R", 'x', -8).Hold("Cannon_R", 'y', 0);
            c.Hold("Shoulder_L", 'x', -35).Hold("Shoulder_L", 'z', -18).Hold("Elbow_L", 'x', -70);
            c.Hold("FingerA_L", 'x', -30).Hold("FingerB_L", 'x', -30).Hold("Thumb_L", 'x', 30);
            c.Hold("Head", 'x', 4);
            return c;
        }

        static ClipBuilder Release()
        {
            const float L = 0.55f;
            var c = new ClipBuilder("RD_Release", L, false);
            c.P("Hips", 'y', 0, -.13f, .06f, -.05f, L, 0);
            c.R("Torso", 'x', 0, 6, .06f, -12, .25f, -4, L, 2);
            c.R("Shoulder_R", 'x', 0, -82, .06f, -112, .2f, -95, L, -1).Hold("Shoulder_R", 'z', 3);
            c.R("Elbow_R", 'x', 0, -8, .06f, -2, L, -16);
            c.R("Shoulder_L", 'x', 0, -35, .1f, 10, L, 1).R("Shoulder_L", 'z', 0, -18, L, -3).R("Elbow_L", 'x', 0, -70, L, -12);
            c.R("Hip_L", 'x', 0, -32, L, -3).R("Hip_R", 'x', 0, -18, L, -3).R("Knee_L", 'x', 0, 48, L, 6).R("Knee_R", 'x', 0, 40, L, 6);
            c.R("Ankle_L", 'x', 0, -16, L, -3).R("Ankle_R", 'x', 0, -22, L, -3);
            c.R("Head", 'x', 0, 4, .06f, -10, L, 0);
            c.R("FingerA_L", 'x', 0, -30, L, -6).R("FingerB_L", 'x', 0, -30, L, -6).R("Thumb_L", 'x', 0, 30, L, 8);
            return c;
        }

        static ClipBuilder Hurt()
        {
            const float L = 0.4f;
            var c = new ClipBuilder("RD_Hurt", L, false);
            c.R("Torso", 'x', 0, 2, .08f, -14, .2f, -6, L, 2).R("Torso", 'z', 0, 0, .08f, 6, L, 0);
            c.R("Head", 'x', 0, 0, .08f, -16, L, 0).R("Head", 'y', 0, 0, .08f, -10, L, 0);
            c.R("Shoulder_L", 'x', 0, 1, .1f, 18, L, 1).R("Shoulder_R", 'x', 0, -1, .1f, 12, L, -1);
            c.Hold("Elbow_L", 'x', -12).Hold("Elbow_R", 'x', -16);
            c.P("Hips", 'y', 0, 0, .1f, -.04f, L, 0);
            c.Hold("Hip_L", 'x', -3).Hold("Hip_R", 'x', -3).Hold("Knee_L", 'x', 6).Hold("Knee_R", 'x', 6).Hold("Ankle_L", 'x', -3).Hold("Ankle_R", 'x', -3);
            return c;
        }

        // Collapse onto the knees and slump forward; the clip holds its last pose (no loop).
        static ClipBuilder Death()
        {
            const float L = 1.6f;
            var c = new ClipBuilder("RD_Death", L, false);
            c.P("Hips", 'y', 0, 0, .25f, -.03f, .8f, -.42f, 1.1f, -.5f, L, -.5f);
            c.R("Hip_L", 'x', 0, -3, .8f, -70, L, -78).R("Hip_R", 'x', 0, -3, .8f, -60, L, -70);
            c.R("Knee_L", 'x', 0, 6, .8f, 105, L, 112).R("Knee_R", 'x', 0, 6, .8f, 95, L, 104);
            c.R("Ankle_L", 'x', 0, -3, .8f, -30, L, -34).R("Ankle_R", 'x', 0, -3, .8f, -32, L, -34);
            c.R("Torso", 'x', 0, 2, .25f, -12, .9f, 20, 1.3f, 48, L, 52).R("Torso", 'z', 0, 0, .9f, 8, L, 10);
            c.R("Head", 'x', 0, 0, .25f, -15, 1.2f, 35, L, 40);
            c.R("Shoulder_L", 'x', 0, 1, .9f, 25, L, 30).R("Shoulder_L", 'z', 0, -3, L, -8).R("Elbow_L", 'x', 0, -12, L, -4);
            c.R("Shoulder_R", 'x', 0, -1, .9f, 20, L, 28).R("Shoulder_R", 'z', 0, 3, L, 10).R("Elbow_R", 'x', 0, -16, L, -6);
            c.R("FingerA_L", 'x', 0, -6, L, 10).R("FingerB_L", 'x', 0, -6, L, 10).R("Thumb_L", 'x', 0, 8, L, -6);
            return c;
        }

        static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter { name = "LocomotionRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
            controller.AddParameter("Charging", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Release", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            var sm = controller.layers[0].stateMachine;

            var locomotion = controller.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D; tree.blendParameter = "Speed"; tree.useAutomaticThresholds = false;
            tree.AddChild(clips["RD_Idle"], 0); tree.AddChild(clips["RD_Walk"], 2.6f); tree.AddChild(clips["RD_Run"], 7.5f);
            locomotion.speedParameterActive = true; locomotion.speedParameter = "LocomotionRate";
            sm.defaultState = locomotion;

            var charge = sm.AddState("Charge"); charge.motion = clips["RD_Charge"];
            var release = sm.AddState("Release"); release.motion = clips["RD_Release"];
            var hurt = sm.AddState("Hurt"); hurt.motion = clips["RD_Hurt"];
            var dead = sm.AddState("Dead"); dead.motion = clips["RD_Death"];

            AnimatorStateTransition T(AnimatorState from, AnimatorState to, float duration, bool exitTime = false, float exit = 0.9f)
            {
                var t = from.AddTransition(to); t.duration = duration; t.hasFixedDuration = true; t.hasExitTime = exitTime; t.exitTime = exit; return t;
            }
            T(locomotion, charge, .12f).AddCondition(AnimatorConditionMode.If, 0, "Charging");
            T(locomotion, release, .05f).AddCondition(AnimatorConditionMode.If, 0, "Release");
            // Release is listed before the cancel so a release that also clears Charging plays the shot.
            T(charge, release, .04f).AddCondition(AnimatorConditionMode.If, 0, "Release");
            T(charge, locomotion, .18f).AddCondition(AnimatorConditionMode.IfNot, 0, "Charging");
            T(release, locomotion, .2f, true, .85f);
            var hurtToCharge = T(hurt, charge, .1f, true, .8f); hurtToCharge.AddCondition(AnimatorConditionMode.If, 0, "Charging");
            T(hurt, locomotion, .15f, true, .85f);

            var anyHurt = sm.AddAnyStateTransition(hurt); anyHurt.duration = .05f; anyHurt.canTransitionToSelf = false;
            anyHurt.AddCondition(AnimatorConditionMode.If, 0, "Hurt"); anyHurt.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            var anyDead = sm.AddAnyStateTransition(dead); anyDead.duration = .1f; anyDead.canTransitionToSelf = false;
            anyDead.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            EditorUtility.SetDirty(controller);
            return controller;
        }

        // ------------------------------------------------------------------ prefab
        static void BuildPrefab(GameObject model, AnimatorController controller, Material glow)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var root = new GameObject("RiftDisruptor");
            try
            {
                var body = root.AddComponent<CharacterController>();
                body.radius = BodyRadius; body.height = BodyHeight; body.center = Vector3.up * BodyHeight / 2;
                body.stepOffset = .3f; body.slopeLimit = 45; body.skinWidth = .05f; body.minMoveDistance = 0;
                var motor = root.AddComponent<SmoothPathFollower>();
                motor.turnRate = 300; motor.acceleration = 9; motor.deceleration = 14;
                var brain = root.AddComponent<DisruptorBrain>();
                brain.eyeHeight = EyeHeight;
                var telegraph = root.AddComponent<DisruptorTelegraph>();
                telegraph.buildPlaceholderBody = false; telegraph.brain = brain;
                telegraph.idleColour = new Color(1f, .45f, .1f); telegraph.chargeColour = new Color(1f, .18f, .04f); telegraph.releaseColour = new Color(1f, .9f, .7f);
                var presenter = root.AddComponent<AgentAnimationPresenter>();
                var visuals = root.AddComponent<DisruptorVisuals>();
                visuals.presenter = presenter; visuals.glowMaterial = glow;

                var visualRoot = new GameObject("Visual");
                visualRoot.transform.SetParent(root.transform, false);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, visualRoot.transform);
                instance.name = "RiftDisruptorModel";
                var animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;   // off-screen agents skip posing
                presenter.animator = animator;
                presenter.headBone = Find(instance.transform, "Head");
                presenter.leanBone = Find(instance.transform, "Torso");
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 8;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
