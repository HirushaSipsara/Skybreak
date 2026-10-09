# World Builder decoration kit

## Current status

2026-10-04: 42 selected third-party FBX models imported through the running Unity Editor. URP Lit materials and bottom-centred reusable prefabs were created. A live check found no missing meshes, missing materials or incorrect material shaders across the 42 prefabs. The active main scene remained clean and unchanged. Models have not yet been placed in the map or tested for gameplay, camera clearance or performance.

The CLI import response timed out at 30 seconds, but the operation completed; the generated import report and subsequent live asset validation confirmed the result. Do not rerun solely because of that timeout.

## Asset paths

An additional 512 dungeon prefabs are now available in `KayKit_Dungeon`, `Quaternius_ModularDungeons`, `MrScientist_Dungeon` and `RGSDev_Dungeon` subfolders. See `DUNGEON_ASSET_LIBRARY.md` for their sources, validation and limitations. The original 42 prefabs described above are preserved.

- Ready-to-place prefabs: `Assets/Prefabs/Environment/Decoration/<PackName>/`
- Source FBX models: `Assets/Models/Environment/Decoration/<PackName>/`
- URP materials: `Assets/Materials/Environment/Decoration/<PackName>/`
- Palette textures: `Assets/Textures/Environment/Decoration/<PackName>/`
- Original downloaded kit, licences and inventory: `resources/MapDecorationKit/`
- Model dimensions and collider report: `Docs/GV/LevelDesign/Reports/Decoration/ImportReport.md`
- Import tool: `Tools/ImportMapDecoration.cs` (run through Unity CLI, outside Assets)

Pack names: `Kenney_FactoryKit`, `Kenney_SpaceStationKit`, `Kenney_ModularDungeonKit`, `Quaternius_Ruins_Selected`.

## Use in the current map

1. Open `Assets/Scenes/GameLevel/SkybreakMain.unity`, outside Play Mode.
2. Drag a prefab from the Decoration folder into the scene. Use prefabs rather than raw FBX models to retain URP material remapping and the prepared pivot/collider.
3. For cover, start with `structure-barrier`, `structure-barrier-high`, `container-wide`, `machine-fortified`, `Crate` and the stone columns.
4. Scale each object against the current enlarged Arc character. Imported authored dimensions vary; the report records them. Maintain walkable gaps and keep security doorways clear.
5. Boxes, containers, barriers, machines, columns, crate, barrel and chest prefabs have BoxColliders. Arches, gates, pipes, rubble and other decoration currently have no colliders. Open doorway meshes deliberately do not get a solid box across the opening. Add appropriate collision when integrating them.
6. Keep existing stone floors and walls. Use arches/pillars as accents, machinery and pipes as facility equipment, and teal/orange/cyan as restrained accents. Default downloaded palettes still need visual review against map lighting.
7. Test camera clearance, movement, eventual bullet obstruction and navigation after placement. Cover height, crouch/peek behaviour and enemy combat are not implemented by the imported assets.
8. Save the scene. When committing, include the new assets and their generated `.meta` files, and only the intended scene changes. The kit is your World Builder sourcing/integration work; it does not satisfy Student 3's two original models requirement.

The two earlier demos remain under `Assets/Scenes/Demos/`. No imported example scene replaced them.
