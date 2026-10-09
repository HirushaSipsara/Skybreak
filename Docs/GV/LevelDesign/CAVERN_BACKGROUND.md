# Cavern background — first environment pass

Main scene: `Assets/Scenes/GameLevel/SkybreakMain.unity`.

The dungeon now sits among tall, irregular rock faces instead of floating in an empty background. The entrance has a weathered stone arch. Halls remain open above the player; tunnel ceilings and the high boss-room vault are later environment passes.

## Edit the backdrop in Unity

1. Open the main scene and stop Play Mode.
2. Expand **15 / Cavern backdrop and entrance arch** in the Hierarchy.
3. Edit the individual prefabs in these groups:
   - **01 / Outer cliff faces:** 45 tall rocks.
   - **02 / Low rocky ledges:** 21 smaller rocks.
   - **03 / Entrance stone arch:** one wide entry arch.
   - **04 / Buried rock foundation / visual only:** foundation beneath the map.
   - **05 / Connected outer rock shelves:** 39 broad connecting rock masses.
4. Use Move/Rotate/Scale, then save the scene. These changes persist in Edit Mode.
5. Keep the rocks outside the floor and the arch piers outside the starting corridor. Use Play to check the camera before finalizing a moved rock.

The arch opening is 10.4 metres wide with a 7.3-metre spring height. Existing walls continue to provide gameplay collision. Background geometry has no colliders, scripts, or additional lights.

## Source and asset paths

- Editable Blender source: `ArtSource/Student1/CavernKit/Source/SkybreakCavernKit.blend`.
- FBX exports: `ArtSource/Student1/CavernKit/Exports/`.
- Procedural source textures: `ArtSource/Student1/CavernKit/Textures/`.
- Imported meshes: `Assets/Models/Environment/Dungeon/Cavern/`.
- Prefabs: `Assets/Prefabs/Environment/Decoration/Custom/Cavern/`.
- Materials and textures: `Assets/Materials/Environment/Decoration/Custom/Cavern/` and `Assets/Textures/Environment/Decoration/Custom/Cavern/`.

The Blender library arranges the variants separately for inspection. Exports retain an origin suitable for Unity placement. Meshes and the seamless mineral maps were generated with AI assistance; record that provenance. These are Student 1 environment assets, not evidence of Student 3 independently modelling her required two models.

## Checks and evidence

- All cliff, ledge and shelf bounding footprints were checked against the floor with at least one metre of separation; tall cliffs and shelves use 1.2 metres.
- The playable character crossed the entrance from spawn to z=9.16 without jumping.
- All background materials were assigned; no background colliders or new lights were present in Play Mode.
- Existing map, floor, wall details, props, player and holding-room doors were retained.
- Total added geometry: 12,364 triangles across 107 mesh renderers. Texture maps are 512 pixels with mipmaps and anisotropic filtering. Hardware frame rate has not been measured.
- Actual Unity previews and reports: `Docs/GV/LevelDesign/Reports/Cavern/`.
- Pre-change scene backup: `Tools/SceneSnapshots/SkybreakMain_before_cavern_20261009_205338.unity`.

This environment checkpoint does not complete the lighting bake, initial NavMesh, combat, or final hardware optimization. Git history records the saved environment baseline; publishing it to the remote is a separate step.

The subsequent world-preparation pass completed wall weathering, baked lighting and the initial main-map NavMesh. See [world preparation](WORLD_PREPARATION.md) for current settings, checks and rebake instructions. The checkpoint above describes the earlier cavern pass.

## Reproduction tools

- `Tools/BuildCavernKit.py`: isolated background Blender mesh/texture creation, normal and closed-mesh checks, FBX export and editable source save.
- `Tools/InstallCavernBackground.cs`: imports assets, backs up the scene and places the first backdrop.
- `Tools/RefineCavernShelves.cs`: connects cliff faces and tiles the foundation material.
- `Tools/CheckAndCaptureCavern.cs`: player entrance traversal and Unity camera previews.

Install/refinement scripts guard against running twice so manually placed backdrop objects are preserved.
