# Skybreak: carved walls and guardian statues

Approved concept implemented 7 October 2026. Assets are AI-assisted Blender models, with editable mesh parts, smart-projected UVs, FBX exports, previews and validation reports. The saved Unity map uses the existing stone atlas on the trim, relief panels and statues; copper and crystal details use shared colour materials.

## Contents

| Asset | Exported triangles | Approximate size in Unity |
| --- | ---: | --- |
| CarvedStoneTrim_1m | 308 | 1m wide, 0.39m tall, 0.23m deep |
| DiamondWallPanel | 270 | 1.12m wide, 1.20m tall, 0.28m deep |
| CopperWallConduit_1m | 320 | 1m wide, 0.28m tall, 0.23m deep |
| WeatheredGuardianStatue | 1,036 | 1.33m wide, 2.80m tall, 1.06m deep |

The guardian is an inert architectural sculpture, not an enemy. The chipped shoulder, stone fingers, visor, brass collar, held crystal and pedestal are separate editable pieces in the Blender source. These assets have no animation or AI logic.

The table above describes this original source package. The live Unity guardian prefab now uses the **4,092-triangle detailed revision**, with layered armour, helmet vents, finger relief and an engraved pedestal. Its editable source and current guide are in `ArtSource/Student1/DetailedGuardian/README.md`; the simpler Blender source here is preserved. All six current instances remain at scale 1.45, approximately 4.06m tall.

## Edit in Blender

1. Extract the package and open a `.blend` file from `Source/`.
2. Expand **01_ASSET / Export** in the Outliner. Select the part to edit; Tab enters Edit Mode.
3. Press Home to frame the model; Z > Material Preview shows its basic materials.
4. **02_PREVIEW / not exported** contains a camera and studio lights and is hidden in the viewport. F12 produces the included style of Blender preview.
5. Save a new version before major changes. Mesh scales are applied. Blender height is Z; the front faces -Y.
6. To export, select meshes and the root from **01_ASSET / Export** only. Export FBX with Selected Objects, Mesh/Empty, Forward -Z, Up Y, Apply Modifiers enabled and Bake Animation disabled. Exclude the preview helpers.

Blender previews show basic colour materials. Unity's stone-atlas material is a project dependency, not embedded into these standalone FBX files. UVs are smart-projected; the Unity import step remaps stone UVs into selected cells of that atlas. The reusable Unity prefabs contain combined meshes with the FBX axis conversion baked into them, so their placement roots have identity orientation and face +Z. Do not replace the combined `.asset` mesh with an unconverted imported mesh.

## Find and edit in Unity

Scene: `Assets/Scenes/GameLevel/SkybreakMain.unity`.

Expand **11 / Carved walls and guardian statues**:

- **01 / Continuous carved stone bands**: 142 runs/doorway pieces, representing 592 one-metre trim units. Standard bands sit 2.65m above the floor; doorway lintels have raised bands at 3.4m. Runs merge repeated units to reduce the number of renderers while retaining editable scene transforms.
- **02 / Diamond wall relief panels**: 13 panels, including the two holding-room entrances.
- **03 / Copper lantern conduits**: 9 assemblies containing 45 modules. These are decorative copper lines and cyan indicators, with no extra point lights.
- **04 / Six weathered guardian statues**: six statues in entrance, loading-court, security-wing and final-chamber positions. Their scene instances now use scale 1.45, approximately 4.06m tall; the original Blender source and default prefab remain 2.8m tall. Each has a simple blocking BoxCollider.

Edit outside Play Mode. Move or rotate a statue/panel/assembly in the Hierarchy or Scene view, then save. Trim runs can be moved together; for shorter custom bands use the standalone one-metre prefab. The existing floor, walls, columns, crates, skulls, crystal pedestals, enlarged reactor, 18 illuminated lanterns and two working dungeon gates remain intact.

Models and combined meshes: `Assets/Models/Environment/Dungeon/WallDetails/`.

Prefabs: `Assets/Prefabs/Environment/Decoration/Custom/DungeonWallDetails/`.

Materials: `Assets/Materials/Environment/Decoration/Custom/DungeonWallDetails/`.

The creation script is `Tools/InstallDungeonWallDetails.cs`. It refuses to rerun over the existing decoration root, protecting later manual edits. To revise the scene, edit existing instances or their prefabs instead of regenerating the whole pass. Blender build and validation scripts are included in `Scripts/`; they run in a separate background Blender process.

## Checks and limits

- Blender sources reopened and FBXs reimported: mesh count, triangle count, scale and dimensions matched; UVs/materials were present and preview helpers excluded.
- Actual Unity camera captures were reviewed for statue, wall, gate and reactor placement and materials.
- Player-sized capsule sweeps of the main route and both room approaches found zero obstructions from the new detail root. The closed holding-room gates still deliberately block their openings.
- Six statue colliders had no unexpected penetrations against existing walls, columns or cover.
- No new realtime lights were added. Lighting bake, NavMesh integration, complete combat testing and Intel-laptop performance measurement remain separate work.

Reports: `Documentation/` in this package and `Docs/GV/LevelDesign/Reports/WallDetails/` in the Unity project.

Scene backup before this pass: `Tools/SceneSnapshots/SkybreakMain_before_wall_details_20261007_180820.unity`.

## Provenance

The reference concept and these procedural modelling steps are AI-assisted. Keep the scripts and this note with the files. Document any later manual modelling separately; these files do not establish a student's independent from-scratch modelling contribution. No Git commit or push was performed for this update.

