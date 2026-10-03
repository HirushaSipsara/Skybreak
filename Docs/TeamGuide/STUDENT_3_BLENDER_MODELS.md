# Student 3 — original Blender models and assisted Unity import

Read the [team agreement](README.md) first. You can complete the modelling/export work on your computer **without Unity**. Student 1 performs the Unity import and testing on their machine using your handoff package.

## 1. What you must demonstrate

GV pages 2–3 require **at least two custom 3D models made from scratch using Blender or Maya**, with understanding of topology, UV mapping and importing. The two proposed models are:

1. **SecurityRobot** — an original robot enemy with separate moving parts for Student 4.
2. **AccessConsole** — an original facility console that Student 2 can make interactable.

These are suggested designs, not objects prescribed by the PDF. You can change their silhouettes after agreeing dimensions and pivots with Students 1/4. The downloaded floor slabs, our reused Arc character and downloaded statues cannot substitute for your original models. Duplicating one model with another colour is also a weak way to demonstrate two different designs.

Using Blender's cube/cylinder primitives as building blocks is fine: design and edit an actual recognisable model, show its development, and explain your decisions. Do not label downloaded geometry or an unchanged tutorial asset as your own creation.

You ALSO need your own IS agent. The team proposal is Gunner, with independently testable decision logic; see section 13. Modelling does not replace that responsibility.

## 2. Tools and files

Use Blender, Git/Git LFS, and optionally a simple image editor. You do not need Unity to export FBX. Record your installed Blender version in the handoff note; minor menu labels can vary.

Create this source structure in the team's repository:

```text
ArtSource/Student3/
  SecurityRobot/
    Source/SecurityRobot.blend
    Exports/SecurityRobot.fbx
    Textures/                         # only if you use image textures
    Evidence/                         # design, modelling, wireframe, UV screenshots
    MODEL_NOTES.md
  AccessConsole/
    Source/AccessConsole.blend
    Exports/AccessConsole.fbx
    Textures/
    Evidence/
    MODEL_NOTES.md
```

Keep `.blend` sources outside Unity's Assets folder. Deliver exported FBX files so teammates do not need Blender installed to import them; Unity's native `.blend` import invokes the modelling application. [Unity model import](https://docs.unity3d.com/6000.0/Documentation/Manual/ImportingModelFiles.html)

## 3. Agree these design limits first

| Item | Starting target for this project |
| --- | --- |
| Robot overall height | Approximately 2.2 m, comfortably below the 3 m door header |
| Robot width | Around 1.0–1.3 m including shoulders; agree collision radius with Student 4 |
| Console height | Approximately 1.6 m |
| Origin | Root at centre of base/feet, ground level |
| Blender axes | Z up; mark which direction is the front in your notes |
| Unity validation | Model upright, feet at Y=0; agreed forward direction +Z |
| Robot mesh budget | Aim around 2,000–4,000 triangles; measure actual exported geometry |
| Console mesh budget | Aim around 500–1,500 triangles |
| Image textures | Prefer one 1024² image per model if needed; otherwise use a few simple materials |
| Style | Faceted, softened corners, simple readable features; teal/grey, cream, orange and cyan accents |

These budgets are team design targets, not PDF rules. A clean simpler asset is better than unnecessary subdivision. The current floor is detailed stone; the robot and console can use painted metal to distinguish interactable objects while sharing the colour palette.

## 4. Blender basics you will use

| Key/action | Meaning |
| --- | --- |
| Middle mouse | Orbit view |
| Shift + middle mouse | Pan view |
| Mouse wheel | Zoom |
| Numpad 1 / 3 / 7 | Front / side / top |
| Numpad 5 | Switch perspective/orthographic |
| Shift+A | Add an object |
| G then X/Y/Z | Move along an axis |
| S then X/Y/Z | Scale along an axis |
| R then X/Y/Z | Rotate around an axis |
| N | Sidebar; exact Location/Rotation/Dimensions under Item |
| Tab | Object/Edit Mode |
| 1/2/3 in Edit Mode | Vertex/edge/face selection, with the default Blender keymap |
| E | Extrude selected mesh geometry |
| Ctrl+B | Bevel selected edges in Edit Mode |
| Shift+D | Duplicate |
| Ctrl+A in Object Mode | Apply transforms; choose Rotation & Scale when instructed |
| Ctrl+S | Save |

Use the menu equivalents if your keyboard lacks a numpad or your keymap is different. Type exact metre dimensions in N → Item rather than estimating by zoom level.

## 5. Set up a modelling scene

1. Open Blender and start a new General file. Save it as `ArtSource/Student3/SecurityRobot/Source/SecurityRobot.blend` immediately, relative to your repository root.
2. In **Scene Properties → Units**, choose Metric, Unit Scale 1, Length Metres.
3. Keep camera/lights in a separate `PreviewOnly` collection. They must not be included in the game FBX export.
4. Make a `SecurityRobot_Source` collection and name every object as you create it.
5. Add an Empty at `(0,0,0)` named `SecurityRobot_Root`. It defines the exported base origin.
6. Decide the visual front; the following recipe puts face details on the **negative Blender Y** side. Put a temporary front arrow in PreviewOnly and mention this choice in MODEL_NOTES.
7. Start with large forms, then add a few recognisable features. Save screenshots and commits as the design develops.

## 6. Model 1 — SecurityRobot, step by step

### A. Build the silhouette

For each box: Shift+A → Mesh → Cube; enter the dimensions and centre location below in the Item panel; rename it. These are **Blender X/Y/Z values**, with Z being height.

| Part | Dimensions X/Y/Z (m) | Centre X/Y/Z (m) |
| --- | --- | --- |
| Left boot | 0.34 / 0.52 / 0.28 | -0.24 / -0.08 / 0.14 |
| Right boot | 0.34 / 0.52 / 0.28 | +0.24 / -0.08 / 0.14 |
| Left leg | 0.26 / 0.32 / 0.72 | -0.24 / 0 / 0.50 |
| Right leg | 0.26 / 0.32 / 0.72 | +0.24 / 0 / 0.50 |
| Body | 0.80 / 0.44 / 0.72 | 0 / 0 / 1.22 |
| Neck | 0.22 / 0.22 / 0.12 | 0 / 0 / 1.60 |
| Head | 0.56 / 0.50 / 0.56 | 0 / 0 / 1.90 |
| Left arm | 0.24 / 0.30 / 0.56 | -0.55 / 0 / 1.24 |
| Right arm | 0.24 / 0.30 / 0.56 | +0.55 / 0 / 1.24 |
| Front visor plate | 0.34 / 0.04 / 0.14 | 0 / -0.27 / 1.93 |

The leg/boot overlap is intentional so there is no visible open joint. Adjust the silhouette if it looks too blocky: narrow the waist, broaden the upper body slightly, or extrude a chest plate. Keep total height near 2.2 m and avoid adding a huge weapon before the body is complete.

### B. Add designed features

1. Make a chest plate from a shallow box on the front. Add two small shoulder plates, a waist band and a modest backpack/vent on the back.
2. Select the head, enter Edit Mode, select its top/front faces and reshape/extrude a small brow or helmet ridge. Do not add a subdivision modifier just to make it smooth.
3. Use a cylinder with 8–12 sides for a joint or small eye lens if it improves the silhouette. Avoid default high vertex counts for tiny unseen parts.
4. Add one clear identity feature for the Gunner design: for example an asymmetric shoulder sensor or a recessed front lens. Model it yourself.
5. Inspect front, side and three-quarter views. Check that the feet sit at Z=0 and the model has no floating body parts.

### C. Apply scale, bevel and clean

1. In Object Mode, select each mesh and **Ctrl+A → Rotation & Scale**. Do this before beveling or setting animation pivots.
2. Add a **Bevel modifier** to visible hard-edged pieces. Start with width 0.01–0.02 m and 1–2 segments; choose values that do not distort the thin visor.
3. Keep corners faceted or use sensible smooth shading. Inspect for black/shading artifacts; do not cover broken geometry with a random smooth-shading setting.
4. Save an editable source version before applying modifiers. On export-ready meshes, apply only the modifiers you intend to bake into the final geometry.
5. In Edit Mode, select all faces and recalculate normals outward with Shift+N. Use Face Orientation overlay to spot reversed surfaces.
6. Check for accidental duplicate faces or internal geometry. Merge by Distance only where it is appropriate; a large threshold can destroy designed joints.
7. Turn on viewport statistics and record vertices/faces/triangles. Do not claim a budget from memory; measure the final exported version too.

### D. Make the robot animatable without a complex skin rig

Our simplest approach is **separate rigid parts**. Student 4 animates their transforms in Unity. A full armature/weight painting is optional only if the group agrees and has time.

1. Keep `Body`, `Head`, each arm and each leg independently addressable. Do not join the whole robot into one object.
2. Parent boot and leg pieces under a named `LeftLegJoint`/`RightLegJoint`, and arm/shoulder pieces under named arm joints. Use Empty objects as these joint roots.
3. Place hip joints around `(-0.24,0,0.86)` and `(0.24,0,0.86)`; shoulder joints around `(-0.55,0,1.52)` and `(0.55,0,1.52)`.
4. Select children then the intended joint Empty last; **Ctrl+P → Object (Keep Transform)**. Parent joint roots and body/head roots to `SecurityRobot_Root` with Keep Transform.
5. Test rotating each joint a small amount. The arm must swing from its shoulder, not from the centre of the hand. Undo the test rotation before export.
6. If using mesh origins instead of Empty joints: set the 3D cursor to the intended joint coordinate, select the mesh, then **Object → Set Origin → Origin to 3D Cursor**. This changes the pivot without moving the visible mesh.
7. Do not apply Location afterward: it can defeat your deliberately positioned origins. Agree stable names with Student 4 before they make clips.

Suggested hierarchy:

```text
SecurityRobot_Root
  Body
  HeadJoint
    Head
    Visor
  LeftArmJoint
    LeftArm
    LeftShoulder
  RightArmJoint
    RightArm
    RightShoulder
  LeftLegJoint
    LeftLeg
    LeftBoot
  RightLegJoint
    RightLeg
    RightBoot
```

This rigid-part workflow has no skin weights. Explain that honestly; do not claim you built a skinned humanoid rig.

## 7. Model 2 — AccessConsole, step by step

Create a separate `ArtSource/Student3/AccessConsole/Source/AccessConsole.blend`, using the same units, palette and front-direction convention. Add `AccessConsole_Root` at the base origin.

Suggested starting parts:

| Part | Dimensions X/Y/Z (m) | Centre X/Y/Z (m) |
| --- | --- | --- |
| Base | 0.90 / 0.65 / 0.20 | 0 / 0 / 0.10 |
| Body pedestal | 0.72 / 0.48 / 1.10 | 0 / 0 / 0.75 |
| Screen housing | 0.82 / 0.22 / 0.50 | 0 / -0.22 / 1.35 |
| Screen inset | 0.64 / 0.03 / 0.32 | 0 / -0.345 / 1.37 |
| Keypad slab | 0.50 / 0.06 / 0.16 | 0 / -0.275 / 1.00 |

1. Form the pedestal and housing with cubes, apply Rotation & Scale, then use a small bevel as with the robot.
2. Edit the body mesh to taper the upper pedestal or inset a panel. Add a lip around the screen; do not leave it as a stack of unchanged cubes.
3. Put three or four small buttons on the keypad, using simple boxes or low-sided cylinders. Their front surfaces must be visible, not hidden inside the pedestal.
4. Create a lower rear service panel or ventilation slots as a small original design feature. Use a shallow inset/extrusion rather than expensive holes if a flat shape reads well.
5. Make the screen material cyan and the body teal/grey with cream/orange accents. Keep the screen geometry slightly proud of the housing to avoid overlapping coplanar faces.
6. Add an Empty named `InteractionPoint` near the front panel for Student 2. It is a marker, not an implemented interaction script.
7. Parent parts to the root with Keep Transform. Static console pieces can be joined if material slots and names remain useful; keep the interaction marker separate.
8. Check the base rests at Z=0, clean normals/topology, and record the final triangle count.

This model's function is an interactable facility control. Its import can use a simple compound box collider and does not need a dynamic Rigidbody or character rig.

## 8. Materials and UVs for both models

### Materials first

Create a few clearly named materials, e.g. `Robot_BodyTeal`, `Robot_TrimCream`, `Robot_AccentOrange`, `Robot_VisorCyan`; use consistent Principled BSDF colours and moderate roughness. Avoid complicated procedural shader networks you expect to transfer automatically to Unity. FBX does not preserve arbitrary Blender node graphs; send image maps or a colour/roughness list for Student 1 to recreate.

### UV workflow

1. Finish export-ready geometry and apply the intended bevels before the final unwrap.
2. Switch to **UV Editing**. Select the relevant meshes, enter Edit Mode and select faces.
3. For a first box-like prototype, **U → Smart UV Project** is a practical start. Inspect the result rather than assuming the automatic result is final.
4. For key visible parts, mark seams along hidden/back edges and use **U → Unwrap**. Explain where the surface is cut into islands.
5. Use **UV → Average Islands Scale**, then **UV → Pack Islands**. Keep an appropriate gutter; a 0.01–0.02 normalised margin is a reasonable initial layout choice for this small project, then inspect the packed result.
6. Put all intended islands inside the 0–1 square, with no unintended overlaps. Keep mirrored overlaps only when deliberately using the same surface appearance, and note them.
7. Test using a generated checker/colour-grid image temporarily. Squares should not be heavily stretched on prominent faces. Screenshot this test and the UV layout.
8. Export the UV layout image as evidence. A checker is a validation texture, not your final hand-painted art.
9. If using flat-colour materials, the UVs still demonstrate the modelling workflow. If using image textures, make an original simple colour/dirt/decal texture and save it outside the `.blend` too.
10. If you create/paint a Blender Image datablock, use **Image → Save As**; saving only the `.blend` is not a reliable separate-texture handoff. Use File → External Data → Pack Resources as an additional source-file convenience.

Pack Islands arranges islands within texture space and provides margin controls; it does not judge whether your chosen seams are visually good. [Blender UV editing reference](https://docs.blender.org/manual/de/5.1/modeling/meshes/uv/editing.html)

## 9. FBX export checklist

Keep the editable `.blend`, and export one FBX for each distinct model.

1. Select the model root and all intended child meshes/Empty joints. Do not include the preview camera, lights, ground plane or front-arrow helper.
2. File → Export → FBX. Some Blender versions expose FBX through the FBX add-on/legacy exporter; use the installed export entry and record which one.
3. Enable **Selected Objects**; include Mesh and Empty object types for the rigid-part robot. Include Armature only if you deliberately chose an armature rig.
4. Start with **Scale 1**, Forward **-Z**, Up **Y**. Keep unit conversion consistent with the Metric source scene. Validate the actual Unity import with Student 1; do not guess that a scale factor always means the same thing across exporters.
5. Apply intended mesh modifiers for export. Keep final joint locations/hierarchy.
6. Disable Bake Animation for these initial static/rigid-part FBX handoffs; Student 4 will create Unity clips. If later exporting animation, agree the clip/rig workflow first rather than enabling every Action automatically.
7. If using an armature later, disable unnecessary leaf bones and export only the intended deformation rig after testing; this is not required for the rigid-part plan.
8. Save with the stable filenames `SecurityRobot.fbx` and `AccessConsole.fbx`. Put texture files alongside in the handoff package. Do not rely only on embedding.
9. Import the FBX into a new Blender test file and inspect dimensions, front direction, pivots, UVs and hierarchy. This catches omissions even though Unity validation is still required.

Blender's documented FBX export exposes scale, axis conversion, selected-object and animation controls. The final test is the exported asset in the receiving engine. [Blender FBX export reference](https://docs.blender.org/manual/fr/5.1/addons/import_export/scene_fbx.html)

## 10. Your handoff to Student 1

For each model, deliver:

- Editable `.blend` source and stable FBX export.
- Any separate PNG/JPG maps; name them clearly by function.
- Front/side/three-quarter screenshots, wireframe/topology screenshot and UV screenshot.
- Triangle count, dimensions, origin position, front direction and Blender/exporter version.
- Material names, hex colours, roughness/metallic intention and any emission intention.
- Robot joint names and a video showing that each part rotates at the correct pivot.
- Your original design sketch/reference notes, including whether any external references/textures were used.
- Your own Git commits; Student 1's Unity import must not erase your authorship history.

MODEL_NOTES can use this template:

```text
Author:
Model and purpose:
Original modelling steps:
Blender version / exporter:
Dimensions and triangle count:
Units / origin / front:
Moving-part names and pivots:
UV maps / texture files:
Material colours and intended finish:
Export settings:
Known issues:
Unity feedback and revision made:
```

## 11. Student 1 — your Unity import procedure

These are the steps **you** perform on your computer, with Student 3 reviewing the result.

1. Keep source `.blend` files in `ArtSource/Student3/<ModelName>/Source/`; copy the FBX from its Exports folder to `Assets/Models/Custom/<ModelName>/` through the project workflow. Put its maps/materials in the appropriate `Assets/Textures/Characters/` and `Assets/Materials/Characters/` folders for the robot, or the corresponding InteractiveObjects folders for the console.
2. Select the FBX in the Project window. Inspect Model/Materials/Animation settings. Check actual world dimensions using a 1 m Unity cube and the known 2.444 m player capsule.
3. Confirm the robot is upright, front faces +Z after agreed conversion, feet/base are at Y=0, and joint names/pivots survived. Test one limb rotation with Student 4.
4. If scale/orientation is wrong, send clear measurements back to Student 3 to fix the source/export. Do not compensate with an unexplained 100× prefab scale and then build physics around it.
5. Create explicit **URP/Lit** materials. Remap imported slots to them. Import normal maps as Normal Map; colour maps use colour space appropriate to their purpose. Recreate emission deliberately; Blender procedural nodes are not automatically Unity shaders.
6. For this rigid-part robot, import mesh/transforms without expecting a humanoid avatar. If no FBX animation exists and clips will be made in Unity, leave Animation import off/rig None. For an agreed armature or FBX clip workflow, use a tested Generic setup instead.
7. Put the model inside a new gameplay prefab root with scale 1 and a `Visual` child. Add one motor/collider setup on that gameplay root; visual pieces do not each need solid colliders.
8. Console: add a simple box/compound collider and connect Student 2's interface/prompt/interaction marker. Do not add a dynamic Rigidbody unless the console is meant to move.
9. Robot: Student 4 adds the agreed CharacterController or movement setup and Animator. Do not mark the moving robot static.
10. Static console: generate/check separate lightmap UVs if it will receive baked lighting, then let Student 1's bake include it. Student 3's UV0 texture layout and the Unity lightmap UV channel serve different purposes.
11. Save the prefab, test in a small scene, then place the approved asset in the map. Check the real 3 m doorway and normal camera distance.
12. Commit FBX importer settings, material/prefab assets and their `.meta` files under your own name. Keep Student 3's sources/exports credited to her. On later updates replace the same FBX file in place; preserve its `.meta` identity.

Send Student 3 screenshots of Inspector settings, size comparison, materials, joint tests and the final map placement. Arrange a screen-share so she can explain and review the import workflow. Confirm with the lecturer how this assisted import satisfies her individual role evidence.

## 12. Completion and viva checklist

Both models are complete when: sources prove original construction; shapes are distinct/useful; topology is clean; triangle count is recorded; pivots and scale are correct; UVs are inspected; files export/import successfully; URP materials are coherent; and the models are actually used in the game.

Practise explaining: why each form was chosen; how you built it from primitives/edited geometry; bevel size versus polygon cost; outward normals; topology cleanup; why seams are placed where they are; UV0 versus lightmap UVs; metre scale/origins; why rigid parts were chosen over skinning; FBX handoff; and who performed the Unity import.

Suggested genuine commits: design/blockout → edited silhouette → joints/pivots → topology cleanup → UVs/materials → export → correction from Unity feedback. Keep source stages/screenshots even if you later replace the current working file.

## 13. Your separate IS agent while Unity is unavailable

Suggested Gunner logic can be written in plain C# without UnityEngine references. With a .NET SDK and editor, make a console/test project whose inputs are numbers and mock observations: distance, visible/hidden target, ammunition/cooldown, last-known location, candidate-position safety and route availability.

Example choices: fire a burst when visible and safely in range; reposition when exposed/too close; investigate the last-known position when sight is lost; wait/replan when the route is blocked. Normalise score inputs, document weights and add hysteresis so tiny score changes do not cause constant switching.

Test at least: visible safe target, close dangerous target, lost target, empty path, blocked doorway, changing player location and tied scores. Record inputs, chosen action and the reason. You own and defend the algorithm; Student 4's motor only makes that choice visible in Unity.

Agree a plain data contract with the group. Student 1/4 writes or connects the Unity adapter that collects observations and executes outputs; you review it and participate in integrated tests through recorded/screen-shared runs. Do not call an unintegrated console simulation a completed in-game agent.
