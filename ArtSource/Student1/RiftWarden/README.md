# Rift Warden Blender asset

Stylized low-poly final-boss visual for Skybreak. The design uses dark teal and blue-steel armor, violet energy accents and a cyan chest core. It is built from simple, separate mesh parts to suit the team's Intel-integrated graphics target.

## Files

- Current animated source: `Source/RiftWarden_Animated.blend`
- Animated export: `Exports/RiftWarden_Animated.fbx`
- Unity animated import copy: `../../../Assets/Models/Custom/RiftWarden/RiftWarden_Animated.fbx`
- Animation preview: `Evidence/RiftWarden_AnimationPreview.mp4`
- Clip timings, validation and integration guide: `../../../Docs/IS/AgentDesign/RiftWarden/`
- Editable source: `Source/RiftWarden.blend`
- FBX handoff: `Exports/RiftWarden.fbx`
- Unity import copy: `../../../Assets/Models/Custom/RiftWarden/RiftWarden.fbx`
- Preview render: `References/RiftWardenPreview.png`
- Concept reference: `References/RiftWardenBlenderModelSheet.png`

## Model structure

Reference refinement v2 replaces the first blockout with tapered chest armor, a faceted helmet and V visor, layered pauldrons and greaves, visible joint rings, a hollow cannon muzzle, a longer energy blade and rear reactor vents. The first source is preserved as `Source/RiftWarden_Blockout_v1.blend`. The current editable source remains `Source/RiftWarden.blend`.

Measured Blender geometry: 7,008 triangles across 117 mesh parts, approximately 3.246 m wide including the weapons, 1.331 m deep and 3.39 m high. Mesh parts are deliberately separate for editing; merge static decorative pieces within each moving assembly during Unity optimization to reduce renderer count. Actual game performance has not been measured.

The hierarchy has a root, torso, legs, character-left cannon arm, character-right blade arm, two chest plates and a separate core. The left/right labels are from the character's perspective. The intended scale is approximately 3.4 m tall; verify the imported model's actual scale and facing in Unity before building the boss arena around it.

## Unity status

The animated version is Student 1's final-boss asset, created with AI assistance. It has a 19-bone rigid mechanical rig, 16 skinned assemblies and 20 animation clips, including chest shutters and core exposure. Source and export were transferred locally on 2026-10-04. The earlier static model is retained for reference.

The animated FBX has been copied into Unity's Models folder. No game scene placement, gameplay prefab, Animator Controller, colliders, detachable-arm physics, damage logic or teleport AI has been added. Unity integration should follow the guide in `Docs/IS/AgentDesign/RiftWarden/HANDOFF.md`. FBX re-import and animation checks are recorded there; Unity runtime behaviour has not been verified.

Open `RiftWarden_Animated.blend` to edit the rig and actions. Its game export includes the rig and skinned meshes, excluding the camera, lights and display stage. The older `RiftWarden.blend` remains the unrigged reference with named component parts.
