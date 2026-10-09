# Detailed guardian statue

AI-assisted Blender revision created 7 October 2026. Adds a carved helmet with visor and breathing slots, layered shoulders with brass studs, chest ribs, cuffs and finger relief, a crystal cradle, belt buckle, knee shields, fluted shin plates, back mantle and engraved stepped pedestal.

Open `Source/WeatheredGuardianDetailed.blend` in Blender. Expand **01_ASSET / Export** to edit the separate parts. Studio camera and lighting live in **02_PREVIEW / not exported**. The FBX in `Exports/` excludes these helpers. The original simpler source remains in `ArtSource/Student1/DungeonWallDetails/Source/WeatheredGuardianStatue.blend`.

The detailed export contains 116 editable mesh pieces and 4,092 triangles, combined into one renderer with five material slots in Unity. Source dimensions are approximately 1.331m wide, 2.8m high and 1.103m deep. Existing scene scale 1.45 gives a height of 4.06m. No animation, AI or lights were added.

## Unity

The existing prefab is updated in place: `Assets/Prefabs/Environment/Decoration/Custom/DungeonWallDetails/WeatheredGuardianStatue.prefab`. All six scene statues receive the new mesh while retaining their transforms. Their original BoxCollider contains the detailed mesh.

Combined mesh: `Assets/Models/Environment/Dungeon/WallDetails/Meshes/WeatheredGuardianStatue_Combined.asset`. Import source: `Assets/Models/Environment/Dungeon/WallDetails/WeatheredGuardianDetailed.fbx`. Mesh and prefab GUIDs were preserved.

The body uses the existing stone texture. A dedicated `GuardianCarvedLimestone` material gives raised edges a calmer grey surface so fine carvings read clearly against the walls; original shared materials remain available. Materials are under `Assets/Materials/Environment/Decoration/Custom/DungeonWallDetails/`.

## Evidence and recovery

`Documentation/Validation.json` records successful source reopening and FBX reimport: matching dimensions, mesh and triangle counts, UVs/materials present, no exported studio helpers. `Previews/DetailedGuardianInUnity.png` is the actual Unity render; `Previews/WeatheredGuardianDetailed.png` is the Blender studio preview. This does not establish full-game performance on Intel graphics.

Before-update mesh, prefab, their metadata and main-scene snapshot: `Tools/AssetSnapshots/GuardianBeforeDetail_20261007_201107/`. A scene backup alone cannot restore a shared asset; use these paired asset backups if reverting the model.

Build: `Tools/BuildDetailedGuardian.py`, which reads helper functions and the original body from `Tools/BuildDungeonWallDetails.py`. Validate: `Tools/ValidateDetailedGuardian.py`. Install: `Tools/InstallDetailedGuardian.cs`. Edit existing assets normally after installation; rerunning the installer will replace the generated mesh.

Keep AI-assisted provenance and the procedural scripts with the source. Saved locally without a Git commit or push.

## Geometry repair

Corrected inward-facing surfaces on 29 extruded armour pieces, including the helmet, torso, shoulders, knees and boots. Mirrored profile winding is now corrected automatically in the Blender build script. The leg straps are slimmer and stone-coloured; intersecting boot overlays were removed. All 116 exported mesh pieces are closed with outward-facing normals, verified after FBX reimport. Repaired front and side Unity screenshots are `Previews/RepairedGuardianFront.png`, `RepairedGuardianLeft.png` and `RepairedGuardianRight.png`; the earlier `DetailedGuardianInUnity.png` predates this repair. All six instances retain their positions, scale and colliders.

