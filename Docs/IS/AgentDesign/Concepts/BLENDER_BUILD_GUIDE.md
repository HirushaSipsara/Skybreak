# Relay and Rift Warden — Blender construction plan

> **Rift Warden status (2026-10-04):** Student 1's animated boss now exists with 19 bones, 16 skinned assemblies and 20 clips. Source: `ArtSource/Student1/RiftWarden/Source/RiftWarden_Animated.blend`; export: `ArtSource/Student1/RiftWarden/Exports/RiftWarden_Animated.fbx`; Unity import copy: `Assets/Models/Custom/RiftWarden/RiftWarden_Animated.fbx`; preview: `ArtSource/Student1/RiftWarden/Evidence/RiftWarden_AnimationPreview.mp4`. Follow `Docs/IS/AgentDesign/RiftWarden/HANDOFF.md` for current animation and integration instructions. The older static model is retained. Unity hitboxes, controller, arm debris, damage and teleport AI still need implementation.

These sheets are generated visual concepts, not existing meshes or exact engineering blueprints. Match a coherent silhouette first; reconcile minor differences between illustrated views during blockout. Dimensions are proposed starting values. Gameplay decisions, damage and teleportation belong to Unity scripts; Blender supplies geometry, materials, pivots and animation-ready parts.

## References and source ownership

- Relay: `ArtSource/Student1/Relay/References/RelayBlenderModelSheet.png`.
- Boss: `ArtSource/Student1/RiftWarden/References/RiftWardenBlenderModelSheet.png`.
- Save original Blender files under each agent's `Source/` folder; FBX under `Exports/`, texture images under `Textures/`, progress captures under `Evidence/`.
- These are Student 1's proposed IS agents. Student 3's two assessed original models remain her responsibility.

## Start in Blender

1. Create one Blender file per agent. Set Scene Units to Metric with Unit Scale 1; model using metre dimensions.
2. Add the reference sheet as an image reference. Use front and right orthographic views to check shapes; display the relevant part of the sheet beside your model. It is a composite sheet, so do not stretch the entire page to a character's physical dimensions.
3. Block out simple shapes before small trims or glowing details. Use named, separate objects. Set a consistent forward direction and test orientation in Unity during the first export.
4. Use small bevels on the main silhouette. Keep curved parts modest in polygon count; do not apply heavy subdivision across the complete model. Preserve moving joints and use simple shared materials.

## Relay: build the easier agent first

Suggested starting envelope: 0.95 m wide including pods, 0.45 m high and 0.55 m deep. Main body width about 0.65 m. Adjust these together after checking against Arc.

1. **Body:** scale a cube into the body envelope; bevel its edges into a rounded rectangular capsule. Model the teal upper shell as a separate simple shape.
2. **EyeRing:** add a short cylinder facing forward. Use another smaller cylinder for the dark inset and a separate lens object. Avoid complex boolean topology in the first pass.
3. **EyeLens:** use a shallow curved lens or bevelled cylinder with a cyan material. The glow is an appearance choice, not a light source required for gameplay.
4. **LeftPod/RightPod:** model one pod using a bevelled cube or low-resolution sphere and a short cylindrical connector. Duplicate it across the body. Keep each pod separate and place its origin at its rotation joint.
5. **HoverThruster:** a short cylinder underneath with a smaller cyan inset. Actual hovering is controlled in Unity.
6. **HealEmitter:** a separate small underside nozzle. The healing beam is a later Unity effect.
7. **Blaster:** a small separate side-mounted assembly made from bevelled boxes and a nozzle cylinder.
8. Add an Empty named `RelayRoot`. Parent the model parts to this root while preserving their positions. For the initial version, make each animated pod/tool a direct child with its own usable pivot; do not rely on the schematic sheet's decorative connector lines as a compulsory hierarchy.
9. Apply rotation and scale on mesh objects once their basic dimensions are correct. Keep a clean root scale and sensible origins; do not automatically apply location to every joint.

Use shared Cream, Teal, DarkMetal, Orange and Cyan materials. Begin with flat material colours; UV unwrap when using texture images. Simple rigid-part rotation can animate the pods without a deforming skeleton.

## Rift Warden: build in passes

Suggested starting size: 3.4 m tall and roughly 2.6 m across the complete arm/shoulder envelope. The boss is intended for the final arena; check clearance against the actual map before accepting dimensions.

### Pass 1 — silhouette

1. Add an Empty `RiftWardenRoot` at ground level between the feet.
2. Create a torso, pelvis, head, upper/lower legs and feet from bevelled cubes. Use cylinders for visible joints.
3. Make the robot stable-looking in a relaxed A-pose. Check front and side proportions before armour trims.
4. Parent both leg chains to the root/pelvis arrangement; keep upper and lower leg objects separate if they will bend. Place hip, knee and ankle origins at the actual joints.

### Pass 2 — detachable arms

1. Create `LeftCannonArmRoot` at the character's left shoulder and parent its armour, cannon cylinders and moving parts beneath it.
2. Create `RightBladeArmRoot` at the character's right shoulder and build its angular blade from simple wedge geometry.
3. Character left/right matters: in a front view, the left cannon appears on the viewer's right. In a back view this reverses.
4. Keep each complete arm assembly detachable as a group. In Unity, disabling/detaching that assembly must remove its weapon visuals and attack behaviour together.

### Pass 3 — chest core

1. Make `Core` a separate sphere/cylinder inside the torso.
2. Model `ChestPlate_L` and `ChestPlate_R` separately around it, avoiding an opening that exposes the vulnerable target from every angle while closed.
3. Set each plate's origin at its intended hinge. Rotate them outward to verify the exposed-core shape and clearance.
4. Keep optional shielding as an explicit appearance/effect layer. A glowing core in the illustration does not mean it takes damage while shielded.

### Pass 4 — verify gameplay shapes

Preview these states by hiding the arm groups and rotating the plates; restore the default state before saving/exporting the baseline:

- Both arms: cannon and blade attached, core protected.
- One arm lost: remove whichever arm the player destroyed; the other remains functional.
- Both arms lost: complete weapon assemblies absent, leaving shoulder joints.
- Core exposed: chest plates open and Core selectable/visible for its later hitbox.

Teleport trails, slash effects, landing markers, damage logic and AI are later Unity work. They do not need to be baked into the Blender mesh. Rigid-part parenting is sufficient for an initial prototype; add an armature only if the animation workflow requires one.

## Export and Unity handoff

1. Save the editable `.blend` with clearly named objects, sensible pivots and a clean root.
2. Export the actor hierarchy to FBX, including all intended child meshes. Exclude image references, presentation plinths, cameras and lights. Keep source/export units consistent.
3. Import first into `Assets/Models/Custom/Relay/` or `Assets/Models/Custom/RiftWarden/`. Verify forward direction, dimensions and joint rotation in Unity before investing in detailed animations.
4. Put Unity materials under `Assets/Materials/Characters/<AgentName>/` and any images under `Assets/Textures/Characters/<AgentName>/`.
5. Build a friendly-agent prefab under `Assets/Prefabs/Companions/` and the boss under `Assets/Prefabs/Enemies/`. Companions is a proposed new functional subfolder.
6. Use simple separate gameplay colliders/hitboxes in Unity. Keep boss arm damage targets and the core target separate from decorative geometry; the AI/state logic controls when each can take damage.
7. Keep modelling-stage screenshots and an export checklist under each agent's Evidence folder. Test in an agent lab before placement in the main map.

The Relay workflow above remains a proposed modelling workflow. The Rift Warden now has its animated source and FBX; Unity import review, gameplay colliders, an Animator Controller, a prefab and AI implementation remain. This guide's construction passes describe the earlier model-building process; use the delivered handoff guide for the finished rig.
