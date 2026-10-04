# Rift Warden — animated asset handoff

> **Skybreak project copy:** These files were transferred locally on 2026-10-04 as Student 1's final-boss work. Open `ArtSource/Student1/RiftWarden/Source/RiftWarden_Animated.blend`; the Unity FBX is `Assets/Models/Custom/RiftWarden/RiftWarden_Animated.fbx`. Video and stills are under `ArtSource/Student1/RiftWarden/Evidence/`. Manifest, palette and validation JSON files are beside this guide. The older `ArtSource/Student1/RiftWarden/Source/RiftWarden.blend` is the unrigged reference. The short Source/Exports/Previews paths below describe the original ZIP layout. No commit or push was made during transfer.

Prepared 4 October 2026. This local package contains the rigged boss, 20 authored animation clips, an FBX export, a preview video and timing data. It has not been placed in a Unity scene, committed or pushed. Blender 5.1.2 was used. Keep the original files when making team edits.

## Open these first

1. Watch `Previews/RiftWarden_AnimationPreview.mp4` for all 20 clips. It is a fast material-colour preview; the still PNG renders show the studio materials and lighting more accurately.
2. Open `Source/RiftWarden_Animated.blend` in Blender 5.1.2 or a compatible newer version.
3. Select `RiftWardenRig` in the Outliner. The file opens with `RW_Idle` selected.
4. In the Dope Sheet, select **Action Editor**, then use its Action dropdown to choose another `RW_` action. If it asks for an action slot, select the rig's slot.
5. Set the timeline end to the chosen clip's end frame from the table below. Start at frame 1 and press Space over the timeline to play. The source frame rate is 30 fps. To edit bones, enable viewport Overlays and switch the selected rig to Pose Mode.
6. NLA tracks are intentionally muted; use one active action for preview. Do not unmute all tracks together.
7. For phase 3, hide mesh objects `RW_Arm_L` and `RW_Arm_R` with the Outliner eye controls. Turn them on again for other clips. In Blender's standalone action preview, arm-break clips show the reaction but do not automatically hide an arm. The supplied video simulates that visibility change at the detach marker.

No scripts need to be enabled to open or play the file. The optional reproducibility scripts remain in the original ZIP delivery. The project copy contains the completed `.blend` and `.fbx`, so rebuilding is not necessary.

## Included model and rig

- 19-bone rigid mechanical skeleton with 16 skinned mesh assemblies. Each armor vertex follows one joint so metal pieces do not bend like skin.
- `RW_Arm_L`: character-left cannon assembly. `RW_Arm_R`: character-right blade assembly. Left/right are from the boss's perspective.
- Shoulder sockets remain part of the chest and stay visible after arm renderers are disabled.
- `RW_Shutter_L` and `RW_Shutter_R`: new center shutter covering the core during stages 1 and 2.
- `RW_ChestPlate_L` and `RW_ChestPlate_R`: opening outer armor.
- `RW_Core`: separate core mesh, ready for a separate Unity damage hitbox.
- Main bones: `Root`, `Pelvis`, `Chest`, `Head`, `UpperArm.L/R`, `Forearm.L/R`, `Thigh.L/R`, `Shin.L/R`, `Foot.L/R`, `ChestPlate.L/R`, `Shutter.L/R`, `Core`.
- Approximate rest height 3.39 m. Blender uses Z up and -Y forward. The FBX uses Y up; check facing and size against Arc in your test scene.

## Bring the handoff into the team repository

Do this in your own local clone and feature branch after reviewing the asset. Import the new animated file alongside the old static model rather than overwriting it; the hierarchy has changed.

| Package file | Suggested project destination |
|---|---|
| Source/RiftWarden_Animated.blend | ArtSource/Student1/RiftWarden/Source/ |
| Source/RiftWarden_Unrigged.blend | ArtSource/Student1/RiftWarden/Source/ |
| Exports/RiftWarden_Animated.fbx | Assets/Models/Custom/RiftWarden/ |
| HANDOFF.md and the JSON manifests | Docs/IS/AgentDesign/RiftWarden/ |
| Preview video and contact sheet | ArtSource/Student1/RiftWarden/Evidence/ |

Let Unity generate `.meta` files, then commit each imported asset with its `.meta`. Preserve existing `.meta` files when revising an already imported asset. Keep Library, Temp and local preview frames out of Git. Use the repository's existing Git LFS configuration for Blender/FBX assets. The raw render Frames folder and `.blend1` backups are intentionally excluded from the ZIP.

## Unity import setup for the person integrating the boss

This asset uses Unity's **Generic** animation rig. Select the FBX in Project, configure Rig as Generic and create the avatar from this model. Choose the skeleton's `Root` bone when selecting a root node. Apply the importer settings. See [Unity's Generic animation import documentation](https://docs.unity.com/en-us/engine/6000.6/manual/assets-and-media/asset-types/models/importing/generic-animations).

Enable animation import and check that all 20 takes appear. Imported names can carry a `RiftWardenRig|` prefix; the suffixes in the table identify the clips. Enable Loop Time only for the five clips marked Loop. Keep one-shot clips nonlooping and hold the final death pose. Blender Actions and NLA strips are already preserved in the source; the FBX exports separate takes. See [Unity's external animation import documentation](https://docs.unity.com/en-us/engine/6000.6/manual/animation-section/animation-mecanim/animation-clips/animations-import).

Locomotion and turns are in place: Unity supplies world movement and heading. Keep Animator Apply Root Motion off for this setup. Preserve root vertical movement in the pose (Bake Into Pose for Root Transform Position Y when exposed by the clip importer). The slam contains a visual vertical jump and other clips have small floor-clearance adjustments, so do not add a second scripted vertical jump. Verify the slam apex in the import preview before controller work. Use the gameplay object's transform for horizontal destination changes and safe navigation placement.

Leave Optimize Game Objects off during initial integration so bone transforms remain accessible for hitboxes and effects. Start with animation compression off while comparing with the Blender preview; then tune compression/performance on the lowest-spec team machine. No performance claim is made from mesh count alone.

Use the game's render-pipeline-compatible materials under `Assets/Materials/Characters/RiftWarden/`. `MaterialPalette.json` records the source colours, metallic, roughness and emission values. For URP/Lit, smoothness starts around `1 - roughness`; match the appearance by eye in the game's lighting. Violet/cyan materials need emission enabled; studio lighting and bloom are not embedded in the FBX. The export excludes the camera, lights and display plinth.

Build the gameplay prefab later under `Assets/Prefabs/Enemies/`, and validate it in Student 4's movement test scene before the main map. This package intentionally does not include an Animator Controller, AI scripts, attack effects, colliders, damage or a scene placement.

## Fight integration contract

**Stage 1:** use Idle, Walk/Run, turns, cannon and blade attacks. Both arm health targets are damageable; torso/core shots are blocked. The shutter is closed. Choose which weapon to use from the agent's decision logic.

**Stage 2:** either arm may be destroyed first. Play the corresponding ArmBreak reaction. At its detach marker, disable that arm renderer and weapon hitboxes, cancel its active attack, and spawn falling debris. The skinned arm itself should not be made a loose Rigidbody while still driven by the rig. Bake its current skinned mesh to a temporary debris mesh, give the debris a simple collider/Rigidbody, and remove it after a short lifetime. This maintains the destruction pose. The remaining arm's attacks continue; normal locomotion clips still work because the destroyed renderer stays hidden.

**Stage 3:** after the second arm is disabled, play CoreOpen once. Enable core damage at frame 46, when the shutter is open. Then use CoreIdle/CoreWalk/CoreTeleportOut/CoreTeleportIn/CoreSlam/CoreHit and Death. Those clips preserve the open chest. Do not transition back to regular Idle/Walk, which close the shutter. Arm visibility remains a persistent gameplay state independent of animation.

**Double teleport:** `RW_DoubleTeleportSlash` includes anticipation, a pause after the second teleport, slash and recovery. At the manifest markers Unity validates landing locations, hides/shows the renderer briefly and warps the actor behind/front of the player. It also creates portal/audio warnings. The clip does not travel across the arena itself. Disable this blade attack after the right arm is destroyed. Phase 3 uses the Core teleport clips followed by CoreSlam instead. The player's double jump and the desired 30%-health attack damage are separate gameplay settings, not part of the model.

**Damage timing:** Blender pose markers do not automatically become Unity Animation Events. Add events in Unity's clip importer, or use a state-timed gameplay controller, using `AnimationManifest.json`. Seconds are `(frame - 1) / 30`. If playback speed changes, sync events to the animation rather than fixed wall-clock timers. Trigger each shot/impact once, and always clear attack hitboxes when an attack is interrupted or an arm is lost.

Do not put AI decisions in the rig. Animation supplies the visible movement; IS chooses actions and targets. Unity owns hitboxes, projectiles, health, navigation, physics, portals, sounds, particles, camera response and UI.

## What was checked

- The exported FBX was imported in a fresh Blender process: all 20 actions, 19 bones and 16 skinned meshes were present with the expected height. Details: `ExportValidation.json`.
- Loop endpoints match. Sampled frames across all clips were checked for flat-floor penetration and corrected. Details: `AnimationValidation.json`. This is not a terrain or collision simulation.
- Pose renders and the animation contact sheet were visually inspected. The video previews each clip. Materials in the video use Workbench preview shading.
- Unity runtime transitions, gameplay timing, terrain foot placement, actor/player collision and lower-end hardware performance remain integration checks. The animations are a working first pass for this project; further artistic polish and controller tuning can be done by the team.

## Editing and contribution record

The base model, rig and clips in this handoff were created with AI assistance. The receiving teammate can edit geometry, weights, poses and timing and commit those changes under her own account; record the actual edits in the project contribution log. The handoff does not establish that an untouched model was personally modelled from scratch for the assessed custom-model requirement.

Blender reference: [Action Editor](https://docs.blender.org/manual/en/4.4/editors/dope_sheet/modes/action.html). Use the rig's actions to edit clips; preserve their names or update the Unity integration mapping when renaming them.

## Clip list

The following table is generated from the delivered manifest. Times are measured from the first frame of each clip.
<!-- GENERATED CLIPS -->

| Clip | Frames | Seconds | Playback | Event markers (frame) |
|---|---:|---:|---|---|
| RW_Idle | 1–91 | 3.0 | Loop | — |
| RW_CoreIdle | 1–91 | 3.0 | Loop | — |
| RW_Walk | 1–41 | 1.333 | Loop | FOOT_L (1); FOOT_R (21) |
| RW_Run | 1–31 | 1.0 | Loop | FOOT_L (1); FOOT_R (16) |
| RW_CoreWalk | 1–41 | 1.333 | Loop | FOOT_L (1); FOOT_R (21) |
| RW_TurnLeft | 1–37 | 1.2 | Once | — |
| RW_TurnRight | 1–37 | 1.2 | Once | — |
| RW_CannonBurst | 1–55 | 1.8 | Once | FIRE_1 (18); FIRE_2 (27); FIRE_3 (36) |
| RW_BladeSlash | 1–54 | 1.767 | Once | HITBOX_ON (21); HITBOX_OFF (28); RECOVERY_START (32) |
| RW_TeleportOut | 1–24 | 0.767 | Once | PORTAL_WARNING (8); TELEPORT_OUT (21) |
| RW_TeleportIn | 1–25 | 0.8 | Once | TELEPORT_IN (1); LAND_FX (8) |
| RW_CoreTeleportOut | 1–24 | 0.767 | Once | PORTAL_WARNING (8); TELEPORT_OUT (21) |
| RW_CoreTeleportIn | 1–25 | 0.8 | Once | TELEPORT_IN (1); LAND_FX (8) |
| RW_DoubleTeleportSlash | 1–91 | 3.0 | Once | FIRST_PORTAL_WARNING (8); TELEPORT_BEHIND (18); SECOND_PORTAL_WARNING (26); TELEPORT_FRONT (36); ATTACK_WARNING (40); HITBOX_ON (57); HITBOX_OFF (63); RECOVERY_START (67) |
| RW_ArmBreakLeft | 1–43 | 1.4 | Once | DETACH_LEFT_ARM (10); SHOULDER_SPARKS (14) |
| RW_ArmBreakRight | 1–43 | 1.4 | Once | DETACH_RIGHT_ARM (10); SHOULDER_SPARKS (14) |
| RW_CoreOpen | 1–61 | 2.0 | Once | CHEST_UNLOCK (12); CORE_VULNERABLE (46); PHASE3_READY (56) |
| RW_CoreSlam | 1–91 | 3.0 | Once | GROUND_WARNING (10); TAKEOFF (26); TARGET_LOCK (39); SLAM_DAMAGE (46); RECOVERY_START (50) |
| RW_CoreHit | 1–25 | 0.8 | Once | CORE_HIT_FX (7) |
| RW_Death | 1–91 | 3.0 | Once | CORE_OVERLOAD (18); DEATH_IMPACT (59); BOSS_DEFEATED (80) |
