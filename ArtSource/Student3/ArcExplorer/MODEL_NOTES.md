# Arc Explorer — Blender model handoff

Created on `sachini-gv-models`, based on `origin/main` at `0c4da35`.

## Files

- `Source/ArcExplorer.blend`: editable character, named parts, materials, UVs, posing rig and preview studio. The original startup scene is preserved separately.
- `Source/build_arc_explorer.py`: reproducible Blender construction script. Run from a saved script with `__file__` defined; it creates a new scene.
- `Exports/ArcExplorer.fbx`: character geometry, material slots and skeleton; excludes preview lights and camera.
- `Evidence/ArcExplorer_ThreeQuarter.png`: rendered model preview.
- `Evidence/Validation.json`: measured geometry and rig checks.

## Design and provenance

This is an **AI-assisted model**, constructed with Blender primitives, bevels, explicit part weights and UV projection. It adapts the existing shared `Assets/Prefabs/Player/ArcExplorer.prefab` design: teal suit, orange rescue vest, cyan visor, dark boots/gloves and backpack. Added details include a headset, wrist scanner, pockets, reflective strips and backpack vents. It is not a claim of unaided student modelling or an entirely independent character design.

## Dimensions and rig

- Approximately 1.92 m tall; ground-level root at the centre of the feet.
- Blender: Z up, front toward -Y. FBX: Y up with -Z forward export convention. Verify actual facing after Unity import.
- 10,020 triangles, 92 editable mesh pieces, 18 bones, 9 material colours.
- Every vertex has a rigid weight to its relevant bone. Armor segments are designed to rotate independently; this is not a smooth organic deformation rig.
- Each mesh has projected UVs. UVs are per-piece and are not a shared non-overlapping texture atlas. No external texture files are required.
- No animation clips, IK controls, facial rig or individual finger bones are included.

## Unity handoff

Import the FBX into a new character model folder on an integration branch. Use a Generic rig initially and review scale, orientation, material conversion and pose deformation. Assign project URP materials as needed; FBX does not preserve Blender's exact shader appearance or emission setup. The existing player is larger in some scene instances; preserve its gameplay capsule and camera settings independently of this model's base scale.

This deliverable does not replace the live player prefab or alter gameplay scripts/scenes. Unity runtime integration, animation retargeting, first-person hand setup, collider alignment and low-end performance profiling remain unverified. For production, combine compatible meshes/materials after review to reduce renderer overhead.
