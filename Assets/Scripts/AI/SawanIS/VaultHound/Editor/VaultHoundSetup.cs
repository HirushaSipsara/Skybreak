#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using Skybreak;

namespace Skybreak.AI.SawanIS.VaultHound.Editor
{
    /// <summary>
    /// One-click setup for the Vault Hound Unity assets.
    ///
    /// Menu: Tools → SawanIS → Setup Vault Hound Assets
    ///
    /// What it creates / configures:
    ///   1. FBX import settings — Generic rig, correct scale, 8 named animation clips with loop flags.
    ///   2. URP Lit material — BaseColor map, Emission map (orange tint, multiplier 1.8), Metallic 0.45, Smoothness 0.62.
    ///   3. Animator Controller — states + parameter-driven transitions matching VaultHoundAnimator contract.
    ///   4. Complete Prefab   — Assets/Prefabs/Enemies/VaultHound.prefab with model, NavMeshAgent, CombatHealth, CapsuleCollider, Animator, Brain, and Animator Presenter.
    /// </summary>
    public static class VaultHoundSetup
    {
        // ── Paths ─────────────────────────────────────────────────────────────

        const string FbxPath       = "Assets/Models/Custom/VaultHound/VaultHound_Animated.fbx";
        const string BaseColorPath = "Assets/Textures/Characters/VaultHound/VaultHound_BaseColor.png";
        const string EmissionPath  = "Assets/Textures/Characters/VaultHound/VaultHound_Emission.png";
        const string MaterialDir   = "Assets/Materials/Characters/VaultHound";
        const string MaterialPath  = "Assets/Materials/Characters/VaultHound/VaultHound.mat";
        const string AnimatorDir   = "Assets/Animations/VaultHound";
        const string AnimatorPath  = "Assets/Animations/VaultHound/VaultHoundController.controller";
        const string PrefabDir     = "Assets/Prefabs/Enemies";
        const string PrefabPath    = "Assets/Prefabs/Enemies/VaultHound.prefab";

        // ── Menu entry ────────────────────────────────────────────────────────

        [MenuItem("Tools/SawanIS/Setup Vault Hound Assets")]
        public static void Run()
        {
            bool ok = true;
            ok &= ConfigureFbxImport();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ok &= CreateMaterial();
            ok &= CreateAnimatorController();
            ok &= CreatePrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (ok)
            {
                Debug.Log("[VaultHoundSetup] Setup complete! FBX, Material, Animator Controller, and Prefab created.");
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("Vault Hound Setup",
                        "Setup complete!\n\n" +
                        "• FBX import settings applied\n" +
                        "• URP material created at:\n  " + MaterialPath + "\n" +
                        "• Animator Controller created at:\n  " + AnimatorPath + "\n" +
                        "• Prefab created at:\n  " + PrefabPath + "\n\n" +
                        "Vault Hound is ready for scene placement!", "OK");
                }
            }
            else
            {
                Debug.LogWarning("[VaultHoundSetup] Setup completed with warnings. Check Console.");
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("Vault Hound Setup",
                        "Setup completed with warnings. Check the Console for details.", "OK");
                }
            }
        }

        // ── 1. FBX import ─────────────────────────────────────────────────────

        static bool ConfigureFbxImport()
        {
            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[VaultHoundSetup] FBX not found at {FbxPath}. " +
                    "Copy VaultHound_Animated.fbx there first.");
                return false;
            }

            // Rig
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;

            // Scale — FBX uses metres; Unity default is 0.01 for FBX. Set to 1.
            importer.globalScale   = 1f;
            importer.useFileScale  = true;

            // Clip definitions (matches ModelReport.json frame ranges at 30fps)
            importer.clipAnimations = new ModelImporterClipAnimation[]
            {
                Clip("Idle",   1,  61, loop: true),
                Clip("Walk",   1,  31, loop: true),
                Clip("Run",    1,  21, loop: true),
                Clip("Alert",  1,  31, loop: false),
                Clip("Sniff",  1,  61, loop: true),
                Clip("Lunge",  1,  25, loop: false),
                Clip("Hit",    1,  13, loop: false),
                Clip("Death",  1,  46, loop: false),
            };

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log("[VaultHoundSetup] FBX import settings applied.");
            return true;
        }

        static ModelImporterClipAnimation Clip(string name, int first, int last, bool loop)
        {
            return new ModelImporterClipAnimation
            {
                name        = name,
                takeName    = $"VaultHound_Rig|{name}",
                firstFrame  = first,
                lastFrame   = last,
                loopTime    = loop,
                lockRootRotation = false,
                lockRootHeightY = false,
                lockRootPositionXZ = false,
                keepOriginalOrientation = true,
                keepOriginalPositionY   = true,
                keepOriginalPositionXZ  = true,
            };
        }

        // ── 2. URP Material ───────────────────────────────────────────────────

        static bool CreateMaterial()
        {
            // Ensure folder exists.
            if (!AssetDatabase.IsValidFolder(MaterialDir))
            {
                string parent = Path.GetDirectoryName(MaterialDir).Replace('\\', '/');
                string folder = Path.GetFileName(MaterialDir);
                AssetDatabase.CreateFolder(parent, folder);
            }

            // Find URP Lit shader.
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[VaultHoundSetup] URP Lit shader not found. " +
                    "Make sure URP is installed. Material creation skipped.");
                return false;
            }

            // Load or create material.
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            else
            {
                mat.shader = shader;
            }

            // Base color map.
            var baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath);
            if (baseColor != null)
                mat.SetTexture("_BaseMap", baseColor);
            else
                Debug.LogWarning($"[VaultHoundSetup] BaseColor texture not found at {BaseColorPath}.");

            // Emission map + tint (orange sensor glow).
            var emission = AssetDatabase.LoadAssetAtPath<Texture2D>(EmissionPath);
            if (emission != null)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetTexture("_EmissionMap", emission);
                // Orange tint at multiplier 1.8
                mat.SetColor("_EmissionColor",
                    new Color(1.0f, 0.45f, 0.0f, 1.0f) * 1.8f);
            }
            else
            {
                Debug.LogWarning($"[VaultHoundSetup] Emission texture not found at {EmissionPath}.");
            }

            // PBR values from README.
            mat.SetFloat("_Metallic",   0.45f);
            mat.SetFloat("_Smoothness", 0.62f);

            EditorUtility.SetDirty(mat);
            Debug.Log($"[VaultHoundSetup] Material created/updated at {MaterialPath}.");
            return true;
        }

        // ── 3. Animator Controller ────────────────────────────────────────────

        static bool CreateAnimatorController()
        {
            if (!AssetDatabase.IsValidFolder(AnimatorDir))
            {
                string parent = Path.GetDirectoryName(AnimatorDir).Replace('\\', '/');
                string folder = Path.GetFileName(AnimatorDir);
                AssetDatabase.CreateFolder(parent, folder);
            }

            // Delete and recreate for a clean setup.
            if (File.Exists(Path.GetFullPath(AnimatorPath)))
                AssetDatabase.DeleteAsset(AnimatorPath);

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(AnimatorPath);

            // ── Parameters ────────────────────────────────────────────────────
            ctrl.AddParameter("State",     AnimatorControllerParameterType.Int);
            ctrl.AddParameter("Speed",     AnimatorControllerParameterType.Float);
            ctrl.AddParameter("IsAlive",   AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Searching", AnimatorControllerParameterType.Bool);

            var layer  = ctrl.layers[0];
            var sm     = layer.stateMachine;

            // ── States ────────────────────────────────────────────────────────
            // Helper to load a clip by name from the imported FBX.
            AnimationClip GetClip(string clipName)
            {
                var subAssets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
                foreach (var obj in subAssets)
                {
                    if (obj is AnimationClip clip)
                    {
                        if (string.Equals(clip.name, clipName, System.StringComparison.OrdinalIgnoreCase) ||
                            clip.name.EndsWith("|" + clipName, System.StringComparison.OrdinalIgnoreCase) ||
                            clip.name.IndexOf(clipName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return clip;
                        }
                    }
                }

                var repAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(FbxPath);
                foreach (var obj in repAssets)
                {
                    if (obj is AnimationClip clip)
                    {
                        if (string.Equals(clip.name, clipName, System.StringComparison.OrdinalIgnoreCase) ||
                            clip.name.EndsWith("|" + clipName, System.StringComparison.OrdinalIgnoreCase) ||
                            clip.name.IndexOf(clipName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return clip;
                        }
                    }
                }

                string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/Models/Custom/VaultHound" });
                foreach (var g in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(g);
                    var clips = AssetDatabase.LoadAllAssetsAtPath(path);
                    foreach (var c in clips)
                    {
                        if (c is AnimationClip clip && (
                            string.Equals(clip.name, clipName, System.StringComparison.OrdinalIgnoreCase) ||
                            clip.name.EndsWith("|" + clipName, System.StringComparison.OrdinalIgnoreCase) ||
                            clip.name.IndexOf(clipName, System.StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            return clip;
                        }
                    }
                }

                Debug.LogWarning($"[VaultHoundSetup] Clip '{clipName}' not found.");
                return null;
            }

            AnimatorState AddState(string name, AnimationClip clip, Vector3 pos)
            {
                var s = sm.AddState(name, pos);
                if (clip != null) s.motion = clip;
                return s;
            }

            // States
            var sIdle     = AddState("Idle",     GetClip("Idle"),   new Vector3(  0,  0, 0));
            var sWalk     = AddState("Walk",     GetClip("Walk"),   new Vector3(280,  0, 0));
            var sRun      = AddState("Run",      GetClip("Run"),    new Vector3(560,  0, 0));
            var sAlert    = AddState("Alert",    GetClip("Alert"),  new Vector3(  0,-80, 0));
            var sSniff    = AddState("Sniff",    GetClip("Sniff"),  new Vector3(280,-80, 0));
            var sLunge    = AddState("Lunge",    GetClip("Lunge"),  new Vector3(560,-80, 0));
            var sHit      = AddState("Hit",      GetClip("Hit"),    new Vector3(280,-160,0));
            var sDeath    = AddState("Death",    GetClip("Death"),  new Vector3(  0,-160,0));

            sm.defaultState = sIdle;

            // ── Transitions ───────────────────────────────────────────────────

            // Death — from Any State, has exit time false, immediate.
            var tDeath = sm.AddAnyStateTransition(sDeath);
            tDeath.hasExitTime  = false;
            tDeath.duration     = 0.15f;
            tDeath.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAlive");

            // Idle ↔ Walk
            var t = sIdle.AddTransition(sWalk);
            t.hasExitTime = false; t.duration = 0.15f;
            t.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed");

            t = sWalk.AddTransition(sIdle);
            t.hasExitTime = false; t.duration = 0.15f;
            t.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed");

            // Walk ↔ Run
            t = sWalk.AddTransition(sRun);
            t.hasExitTime = false; t.duration = 0.12f;
            t.AddCondition(AnimatorConditionMode.Greater, 1.5f, "Speed");

            t = sRun.AddTransition(sWalk);
            t.hasExitTime = false; t.duration = 0.12f;
            t.AddCondition(AnimatorConditionMode.Less, 1.5f, "Speed");

            // Idle → Run shortcut
            t = sIdle.AddTransition(sRun);
            t.hasExitTime = false; t.duration = 0.12f;
            t.AddCondition(AnimatorConditionMode.Greater, 1.5f, "Speed");

            // Searching (Sniff) — from any locomotion state
            foreach (var src in new[] { sIdle, sWalk, sRun })
            {
                t = src.AddTransition(sSniff);
                t.hasExitTime = false; t.duration = 0.2f;
                t.AddCondition(AnimatorConditionMode.If, 0, "Searching");
            }
            t = sSniff.AddTransition(sIdle);
            t.hasExitTime = false; t.duration = 0.2f;
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "Searching");

            // Alert (chamber wait / deploy start) — State == 0 (Chambered) or 1 (Deploying)
            var tAlert = sm.AddAnyStateTransition(sAlert);
            tAlert.hasExitTime = false; tAlert.duration = 0.15f;
            tAlert.AddCondition(AnimatorConditionMode.Equals, (int)HoundState.Chambered, "State");

            // Lunge — State == Attack (6)
            var tLunge = sm.AddAnyStateTransition(sLunge);
            tLunge.hasExitTime = false; tLunge.duration = 0.05f;
            tLunge.AddCondition(AnimatorConditionMode.Equals, (int)HoundState.Attack, "State");

            // Hit — State == LungeRecovery (7)
            var tHit = sm.AddAnyStateTransition(sHit);
            tHit.hasExitTime = false; tHit.duration = 0.05f;
            tHit.AddCondition(AnimatorConditionMode.Equals, (int)HoundState.LungeRecovery, "State");

            // Return from Lunge / Hit to Idle
            t = sLunge.AddTransition(sIdle); t.hasExitTime = true; t.exitTime = 1.0f; t.duration = 0.1f;
            t = sHit.AddTransition(sIdle);   t.hasExitTime = true; t.exitTime = 1.0f; t.duration = 0.1f;

            EditorUtility.SetDirty(ctrl);
            Debug.Log($"[VaultHoundSetup] Animator Controller created at {AnimatorPath}.");
            return true;
        }

        // ── 4. Prefab Creation ────────────────────────────────────────────────

        static bool CreatePrefab()
        {
            if (!AssetDatabase.IsValidFolder(PrefabDir))
            {
                string parent = Path.GetDirectoryName(PrefabDir).Replace('\\', '/');
                string folder = Path.GetFileName(PrefabDir);
                AssetDatabase.CreateFolder(parent, folder);
            }

            var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbxAsset == null)
            {
                Debug.LogWarning($"[VaultHoundSetup] FBX asset not found at {FbxPath}. Cannot create prefab.");
                return false;
            }

            var instance = Object.Instantiate(fbxAsset);
            instance.name = "VaultHound";

            // NavMeshAgent setup
            var agent = instance.GetComponent<NavMeshAgent>();
            if (agent == null) agent = instance.AddComponent<NavMeshAgent>();
            agent.speed = 5.5f;
            agent.angularSpeed = 360f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 1.8f;
            agent.radius = 0.5f;
            agent.height = 1.15f;
            agent.baseOffset = 0f;

            // CombatHealth setup
            var health = instance.GetComponent<CombatHealth>();
            if (health == null) health = instance.AddComponent<CombatHealth>();
            var serializedHealth = new SerializedObject(health);
            var maxHpProp = serializedHealth.FindProperty("maximumHealth");
            if (maxHpProp != null)
            {
                maxHpProp.floatValue = 80f;
                serializedHealth.ApplyModifiedProperties();
            }

            // CapsuleCollider setup (Y-axis, centered on body)
            var col = instance.GetComponent<CapsuleCollider>();
            if (col == null) col = instance.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.6f, 0f);
            col.radius = 0.5f;
            col.height = 1.2f;
            col.direction = 1; // Y-axis

            // EyePoint for sensor raycasts
            var eyePoint = instance.transform.Find("EyePoint");
            if (eyePoint == null)
            {
                var eyeGo = new GameObject("EyePoint");
                eyeGo.transform.SetParent(instance.transform, false);
                eyeGo.transform.localPosition = new Vector3(0f, 0.9f, 0.75f);
                eyePoint = eyeGo.transform;
            }

            // Animator setup
            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorPath);
            if (ctrl != null) animator.runtimeAnimatorController = ctrl;

            // Material assignment on all renderers
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat != null)
            {
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    r.sharedMaterial = mat;
                }
            }

            // VaultHoundBrain setup
            var brain = instance.GetComponent<VaultHoundBrain>();
            if (brain == null) brain = instance.AddComponent<VaultHoundBrain>();
            var serializedBrain = new SerializedObject(brain);
            var eyeProp = serializedBrain.FindProperty("eyePoint");
            if (eyeProp != null && eyePoint != null)
            {
                eyeProp.objectReferenceValue = eyePoint;
                serializedBrain.ApplyModifiedProperties();
            }

            // VaultHoundAnimator setup
            var houndAnim = instance.GetComponent<VaultHoundAnimator>();
            if (houndAnim == null) houndAnim = instance.AddComponent<VaultHoundAnimator>();

            // Save Prefab
            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);

            Debug.Log($"[VaultHoundSetup] Prefab created at {PrefabPath}.");
            return true;
        }
    }
}
#endif
