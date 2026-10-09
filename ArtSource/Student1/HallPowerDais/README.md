# Loading court power dais

An AI-assisted, editable Blender model made for Skybreak's loading hall. The circular stone-and-brass floor seal suggests the dungeon's old power network. Cyan inlays use emissive materials; it has no gameplay or teleport behaviour.

## Files and editing

- Blender source: `Source/AuroraHallPowerDais.blend`.
- FBX: `Exports/AuroraHallPowerDais.fbx`.
- Blender preview: `Previews/AuroraHallPowerDais.png`.
- Validation: `Documentation/Validation.json`.

Open the source in Blender, expand the asset export collection, select a mesh and press Tab to edit. Save a copy before major changes. Export only asset meshes and their root, excluding preview cameras and lights; use FBX Forward -Z and Up Y.

## Unity placement

Scene: `Assets/Scenes/GameLevel/SkybreakMain.unity`.

Hierarchy: **12 / Loading court power dais**. The centerpiece is at `(0, 0, 16)`, 5.5m in diameter and at most 0.136m high. It uses a static MeshCollider and 1,380 triangles. Move it outside Play Mode and save the scene.

Prefab: `Assets/Prefabs/Environment/Decoration/Custom/HallPowerDais/AuroraHallPowerDais.prefab`.

Model and combined mesh: `Assets/Models/Environment/Dungeon/HallPowerDais/`. Shared materials live in `Assets/Materials/Environment/Decoration/Custom/DungeonWallDetails/`. Unity uses the existing stone atlas; the standalone Blender file uses basic preview materials. The combined Unity mesh has the FBX axis conversion baked in.

## Checks and rebuild scripts

Blender source reopening and FBX reimport matched mesh count, dimensions and triangle count. UVs and materials were present; studio helpers were excluded. The actual Unity player controller crossed the dais in both directions without jumping. Reports and screenshots: `Docs/GV/LevelDesign/Reports/HallCenterpiece/`. Full gameplay, lighting baking, navigation and hardware performance need their own checks.

Build script: `Tools/BuildHallPowerDais.py`, which depends on the helper definitions in sibling `Tools/BuildDungeonWallDetails.py`. Validation script: `Tools/ValidateHallPowerDais.py`. Installation script: `Tools/InstallHallPowerDais.cs`; it refuses to replace the existing scene root. Edit existing instances instead of rerunning installation over manual edits.

Scene backup: `Tools/SceneSnapshots/SkybreakMain_before_larger_statues_and_hall_dais_20261007_184756.unity`.

## Security hall and boss chamber variants

Two additional circles are under **13 / Encounter floor circles** in the main scene:

- Security hall: `(42, 0, 50)`, 7m diameter, original cyan/brass materials.
- Boss chamber: `(42, 0, 84.5)`, 12m diameter, cooler dark stone, blue steel and violet energy sampled from the Rift Warden palette. Reusable prefab: `Assets/Prefabs/Environment/Decoration/Custom/HallPowerDais/RiftBossPowerDais.prefab`. Variant materials: `Assets/Materials/Environment/Decoration/Custom/HallPowerDais/`.

Only horizontal scale changes; maximum height stays 0.136m. Each uses the original combined mesh (1,380 triangles), with no additional lights. Footprint checks found no overlap with cover or architectural props. Actual player-controller checks crossed both circles north and south without jumping. Screenshots and reports are in `Docs/GV/LevelDesign/Reports/HallCenterpiece/`. These are decorative floors, with no boss spawning or combat logic attached.

Installation and checks: `Tools/AddEncounterPowerDaises.cs` and `Tools/CheckEncounterPowerDaises.cs`. Installation refuses to overwrite the existing root. Backup: `Tools/SceneSnapshots/SkybreakMain_before_encounter_circles_20261007_194409.unity`.

Saved locally; no Git commit or push performed. Keep the procedural scripts and AI-assisted provenance with the source, and document any later manual modelling separately.
