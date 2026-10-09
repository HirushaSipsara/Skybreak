# Skybreak: Aurora architecture

The reactor-shrine and enclosed wall-lantern designs were approved on 7 October 2026. This package contains editable Blender assets, FBX exports, previews, construction scripts and validation reports. The teleport gate recreates the proportions of the earlier Unity Descent demo.

## Open in Blender

1. Extract the ZIP before opening files.
2. Use **File > Open** and choose a file in `Source/`:
   - `VaultWallLantern.blend`: enclosed amber lamp, backplate and hanging bracket.
   - `AuroraReactorShrine.blend`: stone piers, arch, console and suspended cyan crystal.
   - `AuroraTeleportGate.blend`: large stone arch with violet and cyan rings and an energy surface.
3. Expand **01_ASSET / Export** in the Outliner to select individual parts. The root empty groups the asset.
4. Press **Home** over the viewport to frame the scene. Use the numpad for front/side views and middle-mouse to orbit. Press **Z > Material Preview** to see materials.
5. **02_PREVIEW / not exported** contains studio cameras, lights and a ground plane. It is hidden in the viewport; enable its monitor icon if you want to edit the preview setup. Press **F12** to render.
6. Edit geometry in Edit Mode; change material colours in Material Properties. Save a new version before large changes.

## Scale and origins

| Asset | Approximate width / depth / height | Origin |
| --- | --- | --- |
| Lantern | 0.44 / 0.78 / 0.98 metres | Centre of wall backplate, not bottom of lamp |
| Shrine | 5.35 / 2.25 / 4.70 metres | Ground centre |
| Gate | 10.90 / 2.05 / 9.05 metres | Ground centre |

Blender uses Z for height and the asset front faces -Y. After FBX import into Unity the asset front faces +Z; keep the FBX's axis-conversion transform. In the prepared Unity prefabs, an identity parent handles placement while its child preserves that conversion. The gate's inner ring has a centred pivot for future visual rotation.

## Export after editing

1. Select the root and mesh objects in **01_ASSET / Export** only.
2. Apply scale after changing dimensions; preserve deliberate child rotations and the ring pivot.
3. **File > Export > FBX**: enable Selected Objects, choose Mesh and Empty, Forward **-Z**, Up **Y**, Apply Modifiers enabled, Bake Animation disabled.
4. Save to `Exports/`. No external textures are required: these assets use solid-colour materials. The UV layers on most parts are placeholders, not finished texture unwraps. Unwrap them before adding image textures.
5. The procedural portal shader in Blender does not transfer through FBX. Replace it with a Unity portal material; the project's prepared gate prefab uses `AuroraPortal.mat`.

## Unity project integration

Open `Assets/Scenes/GameLevel/SkybreakMain.unity`. Under **09 / Dungeon decoration**:

- **08 / Enclosed wall lanterns**: 18 lamps anchored to wall surfaces; all eighteen have local warm point lights without realtime shadows and brighter emissive cores.
- **09 / Aurora reactor shrine**: one shrine at `(42, 0, 95.4)` facing toward the chamber entrance, with Unity instance scale 1.35 (35% larger; about 7.22m wide and 6.34m tall). The source Blender/FBX geometry remains at its original dimensions listed above.

The earlier columns, crates and skulls are preserved. Eight crystal pedestals remain; the two flanking the rear reactor were removed. The shrine's own structural piers remain. The teleport gate is available as a prefab but is not placed in the main map. Gate placement needs its own layout decision because it is almost 11 metres wide and taller than the current walls.

Project folders:

- Models: `Assets/Models/Custom/AuroraArchitecture/`
- Materials: `Assets/Materials/Environment/Decoration/Custom/AuroraArchitecture/`
- Prefabs: `Assets/Prefabs/Environment/Decoration/Custom/AuroraArchitecture/`

Drag prefabs into a scene to reuse them. To update geometry, overwrite the existing FBX and retain its `.meta` file. Preserve object names so prefab material and collider references can survive reimport; review those references after edits. Save scene edits outside Play Mode.

The gate is an environment model with an energy material. It does not contain teleport triggers, boss AI, damage, animation clips or working player teleportation. Those are separate gameplay tasks. The shrine and lanterns are static props.

## Verification

The `.blend` files were reopened and every FBX reimported in Blender 5.1.2. Mesh counts, triangle counts, scale and dimensions matched; studio helpers were excluded. See `Documentation/Validation.json`.

| Asset | Exported triangles |
| --- | ---: |
| Lantern | 528 |
| Shrine | 1,352 |
| Gate | 2,832 |

Unity camera captures are in `Previews/Unity/`. Main-route and both room-access capsule sweeps found zero added-decoration obstructions after the final placement. This is a geometric check, not a full gameplay test, NavMesh bake or performance measurement on Intel graphics. Main-map lighting baking remains a later world-building task.

Recovery before the first architecture placement: `Tools/SceneSnapshots/SkybreakMain_before_approved_architecture_20261007_160507.unity` in the project. The subsequent snapshot at `160722` preserves the first import before its axis correction.

## Authorship

These are AI-assisted Blender assets generated through the supplied construction script, with editable meshes. The reference concept is AI-generated. They are not evidence that a student manually modelled them from scratch. Keep this provenance with the files and document any later modelling or material changes separately.

No commit or push was performed for this update.
