# Approved wall decoration pass

Implemented 7 October 2026 in `Assets/Scenes/GameLevel/SkybreakMain.unity` from the approved wall-detail concept.

The scene now has continuous carved stone bands, thirteen diamond relief panels, nine copper-conduit assemblies and six reusable weathered guardian statues. The statues use the map's stone textures, hold small cyan crystals and remain architectural props. Their positions keep the main path and room approaches clear. No ceiling, enemy logic or extra point lights were added.

All new work is under **11 / Carved walls and guardian statues**. The existing map and working doors remain in their previous groups. Standalone prefabs and combined trim-run prefabs are in `Assets/Prefabs/Environment/Decoration/Custom/DungeonWallDetails/`.

Editable Blender models, FBX exports, provenance and editing instructions: `ArtSource/Student1/DungeonWallDetails/README.md`. The reference concept is copied into that asset package's `References/` folder; actual Unity renders are in `Reports/WallDetails/`.

Checks found zero new-detail obstructions along the main route and both room approaches, no unexpected statue collider overlap, and no missing/unsupported materials. Blender reimport checks matched exported sizes and triangle counts. These checks do not establish full gameplay, NavMesh correctness or weak-laptop performance.

Backup: `Tools/SceneSnapshots/SkybreakMain_before_wall_details_20261007_180820.unity`. Saved locally; no commit or push performed.

## Larger statues and hall centerpiece

The six guardian statue instances now use scale 1.45 (45% larger, approximately 4.06m tall). Original stone columns and source-model dimensions are unchanged. Updated statue overlap and player-sized route checks found zero unexpected obstructions.

A circular stone-and-brass power dais sits at `(0, 0, 16)` under **12 / Loading court power dais**. It is 5.5m across, at most 0.136m high, and has cyan emissive inlays without additional lights. The player's actual movement controller crossed it in both directions without jumping. Editable source and guide: `ArtSource/Student1/HallPowerDais/README.md`. Reports and Unity screenshots: `Reports/HallCenterpiece/`.

Backup before these changes: `Tools/SceneSnapshots/SkybreakMain_before_larger_statues_and_hall_dais_20261007_184756.unity`.

## Detailed guardian revision

The shared guardian prefab now uses a 4,092-triangle model with layered shoulders, carved helmet and vents, chest ribs, detailed hands, shin relief and an engraved stepped pedestal. All six existing placements update through the original mesh/prefab GUIDs; scale and colliders stay intact. Export validation and a bounds check confirmed the new mesh fits the existing collider. The Unity preview was inspected. Current source and recovery details: `ArtSource/Student1/DetailedGuardian/README.md`.

## Branching copper wall pipes

Added **14 / Branching copper wall vines** with 24 varied assemblies: eight in the entrance/loading court, eight in the maintenance/security wing and eight in the boss chamber. They reuse `CopperWallConduit_1m.prefab`, with copper lines, charcoal brackets and cyan fittings. A fixed seed creates different turns and offshoots; the saved decoration does not change during gameplay.

The 218 modules are combined per assembly, adding 69,760 triangles. Each assembly can be moved, rotated or deleted in Edit Mode. Individual segments are baked into its mesh; use the original conduit prefab to manually add or change individual pipe segments. Source meshes are in `Assets/Models/Environment/Dungeon/WallDetails/Meshes/BranchingPipes/`. Build script: `Tools/AddBranchingWallPipes.cs`; its existing-root guard protects later manual edits.

Segment centres and ends were checked against wall planes, and placements avoid existing panels, statues, conduits and decoration bounds. They sit between 3.25m and 6.08m high with no colliders or new lights. Actual Unity views were inspected; Intel graphics performance has not been measured. Reports and previews: `Reports/WallDetails/BranchingWallPipes.txt` and `BranchingPipes_Court.png`, `BranchingPipes_Security.png`, `BranchingPipes_Boss.png`.

Original map backup: `Tools/SceneSnapshots/SkybreakMain_before_branching_pipes_20261007_220249.unity`. Saved locally without a Git commit or push.

