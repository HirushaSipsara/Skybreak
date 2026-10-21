# Crystal Echo Warden movement

The approved hovering design now has a 12-bone rig with rigid skin weights and eight authored Blender actions. Each of the 30 metal/crystal parts follows one bone without rubber-like bending. The original unrigged sources remain available; use `ArtSource/Sachini/CrystalEchoWarden/Source/CrystalEchoWarden_Animated.blend` for animation work.

| Clip | Duration at 30 FPS | Movement |
| --- | --- | --- |
| CEW_Idle | 2.0 s, looping | Gentle body hover, head scan, sensor and claw motion |
| CEW_Glide | 1.0 s, looping | Forward body lean, counter-rotating head, arm swing and thruster tilt |
| CEW_TurnLeft | 1.0 s, looping | Left bank with head anticipation and arm stabilization |
| CEW_TurnRight | 1.0 s, looping | Right bank with head anticipation and arm stabilization |
| CEW_Retreat | 1.0 s, looping | Braced body and arms with a different thruster angle |
| CEW_Attack | 0.9 s, once | Arms rise during a 0.6 s charge, body recoils and then recovers |
| CEW_Hit | 0.4 s, once | Brief body/head reaction |
| CEW_Death | 1.5 s, holds final pose | Body settles, head and arms droop, sensors fold |

The exported `CrystalEchoWarden.fbx` now includes the rig and all eight takes. The source builder and validation script are beside the animated Blender file. The Blender studio opens on the glide action; select `CEW_Rig` and use the Action Editor to choose another `CEW_` action. NLA tracks are muted for studio viewing to avoid stacking unrelated poses. For export, use the builder's export settings with individual NLA strips enabled; do not export all the muted tracks without enabling them first.

## Unity wiring

`WardenAnimationImporter` applies only to this model, enables animation import, retains the eight takes and sets looping/one-shot modes. It uses Unity's Legacy skeletal `Animation` playback system, which supports this non-humanoid robot without a Humanoid avatar or Animator Controller. The import hook uses Unity's documented [OnPreprocessAnimation](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/assetpostprocessor/onpreprocessanimation) and default clip list.

`WardenAnimation` is added and initialized by the FPS lab. For manually placed Wardens, add the component to the gameplay root and assign `brain` plus the FBX instance root as `visual`. Do not add a competing Animator or a second pose driver. `Ready` reports whether all eight imported clips were found. The smoke-check menu checks this binding in Unity.

The component measures actual horizontal displacement and yaw change, smooths them, and crossfades clips over 0.16 seconds. Blocked agents return to idle as measured speed settles, even if the AI still wants to move. Attack timing follows the brain's wind-up, and firing retains a short recovery. Loss of sight cancels an uncompleted charge; only the combat code can apply damage. Death overrides all other clips and holds its final pose. Locomotion playback speed follows measured speed. The old whole-model bobbing/shrinking code is removed to avoid fighting the rig.

These are in-place clips: `Root` never translates or rotates. `WardenMotor` and its CharacterController remain responsible for world-space movement, turning and collisions. Bone posing is visual only and cannot pull the enemy through a wall. Presentation still controls the core glow and beam separately.

## Verification

Inspect `ArtSource/Sachini/CrystalEchoWarden/Evidence/AnimationValidation.txt` for the actual Blender checks. The labelled `CrystalEchoWarden_Movement.gif` shows sampled poses from all eight authored clips; its uniform review timing is not the exact in-game playback speed or a recording of Unity gameplay.

On 20 October 2026, Unity 6000.6.0f1 compiled the runtime and imported/bound all eight clips. Automated Play Mode checks verified actual controller movement selects glide, stopping selects idle, wind-up selects attack and defeat retains death. Cover cancels pending damage, and four Wardens completed a five-minute run with door changes and repeated resets. See `UnitySmokeResults.txt` and `UnityGameplayResults.txt` in this document folder.

These assertions do not measure visual transition quality or rendered frame rate. In the FPS lab, also inspect continuous banking, hit/retreat poses, rig orientation, unsnapped parts and one attack reaction per shot.
