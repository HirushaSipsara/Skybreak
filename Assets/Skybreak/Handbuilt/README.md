# Skybreak — stone floor workshop

## Open the new floor

In Unity's Project window, open `Assets/Skybreak/Handbuilt/Scenes/SkybreakStoneFloor.unity`.
Click **Play**, then click inside the **Game** window.

| Control | Action |
| --- | --- |
| W / A / S / D or arrow keys | Walk relative to the camera direction |
| Move mouse | Look around and turn |
| Hold Shift | Run |
| Space | Jump |
| Escape | Release the mouse cursor |
| Click Game view | Capture the mouse and resume character control |

This is a **map exploration scene**, with a controllable Arc character and editable floors and walls. It has no combat, enemies or gameplay progression yet.

## Walk around as Arc

The character is under **08 / ARC / Walk around the map** in the Hierarchy, starting at `(0, 0.12, -4)`. It reuses the existing stylized `Assets/Skybreak/Descent/Prefabs/ArcExplorer.prefab`, including simple arm/leg movement, a CharacterController, gravity and jumping. No weapon or attack system was added.

Arc is now **30% larger**: his visual child uses scale `1.3`, with a matching 2.444 metre high collision capsule (radius 0.416 metres). The player root stays at scale 1. His follow camera uses a 2.08 metre pivot, 5 metre distance and 0.8 metre shoulder offset. These are scene overrides; the source Arc prefab keeps its original size. Stop Play Mode before adjusting these Inspector values. If changing visual scale later, resize the CharacterController height/centre/radius too.

The previous size is preserved in `Scenes/SkybreakStoneFloor_BeforeLargerArc_20261002.unity`; size settings are recorded in `Reports/ArcSize.txt`. The shared controller now exposes camera pivot height, shoulder offset and body-hide distance, using its former values as defaults for existing scenes.

The existing map camera now follows Arc in third person. It pulls closer near walls and temporarily hides Arc when the camera is too close to his body. Mouse look supports looking up and down. If Arc falls below the map, he returns to the starting position.

The flying inspection component **DescentMapPreview** is disabled to prevent two control systems driving the same camera. F1–F5 and Q/E flying controls are therefore inactive while exploring. To return to the former free-camera mode, stop Play Mode, deactivate the Arc object, and enable DescentMapPreview on the inspection camera. Its saved viewpoints remain available: F1 whole map, F2 entrance, F3 passage, F4 second arena/rooms, F5 boss chamber.

The pre-character scene is preserved in `Scenes/SkybreakStoneFloor_BeforeExplorer_20261002.unity`. Build verification is in `Reports/ExplorerBuild.txt`; Play Mode movement/collision checks are in `Reports/ExplorerPlayCheck.txt`. The real CharacterController reached all 12 route waypoints, passed both room doorways, collided with the perimeter wall, and jumped/landed successfully. These checks used the movement API; actual keyboard/mouse event injection was not used. An actual Unity camera capture is `Reports/ExplorerInMap.png`.

## Edit the walls

The walls reuse the original map's softened stone-block shape, now textured with the same cracked-stone atlas, normal map and smoothness map as the floor. Grey-green walls match the arenas, blue-teal walls match the passage, and muted-violet walls match the boss chamber. There are no added wall lights, pipes, decorative props or working doors in this pass.

Expand **07 / Modular walls** in the Hierarchy. Each boundary section is an editable prefab, grouped into the entrance/first arena, winding passage, second arena/holding rooms and boss chamber. Standard sections are 1 metre long, 0.72 metres thick and **6.5 metres high**. Passage sections are **7 metres high**. Their inner faces sit on the floor boundary so the original corridor width is retained.

Select a complete wall parent to move or duplicate it; rotate Y in 90-degree increments for grid-aligned walls. Current prefabs are **Wall_Textured_65_V1** through **V4**, **Wall_Textured_70_V1** through **V4**, and **Wall_Textured_Doorway_65_3mClear**, in `Assets/Skybreak/Handbuilt/Prefabs`. Each ordinary module has one combined masonry mesh and one solid BoxCollider. The four mesh variants vary the stone patterns without duplicating textures. Each block samples one stone from the 4 × 4 floor atlas, avoiding the entire atlas repeating inside a single brick.

Wall materials are `Wall_Textured_Floor2_Body/Cap/Base`, `Wall_Textured_PassageFloor_Body/Cap/Base`, and `Wall_Textured_CoreFloor_Body/Cap/Base`. They share the floor's existing 2K texture maps; the cap is slightly lighter and the base darker. Their Smoothness is 0.65 to keep a satin stone finish. Changing these materials affects walls without changing floor materials. Duplicate a scene wall to preserve its regional material overrides; a freshly dragged prefab uses the neutral grey-green set by default.

Both security-room entrances have 6.5 metre doorway walls leaving a **3 metre wide × 3 metre high** clear opening. The taller header and two side pieces have their own colliders; no moving door has been added. The existing door-position guides remain under **06 / Security holding rooms**.

The pre-wall scene is backed up as `Scenes/SkybreakStoneFloor_BeforeWalls_20261002.unity`. Wall verification is in `Reports/WallBuild.txt`. The editor menu **Skybreak → Handbuilt Floor → Add modular walls (once)** refuses to add another wall set if this wall root already exists. Continue by editing the saved modules manually.

The short untextured walls are preserved in `Scenes/SkybreakStoneFloor_BeforeTexturedWalls_20261002.unity`; their original wall prefabs, meshes and materials are also unchanged. The upgrade report is `Reports/TexturedWallUpgrade.txt`. Actual Unity captures are `Reports/TexturedWallsCloseup.png` and `Reports/TexturedWallsOverview.png`. The upgrade menu refuses to repeat the replacement once textured walls exist.

## Security holding rooms

The two 8 × 8 metre side-room floors connect to the existing side branches of the second arena. Each new entrance is 2 metres long and 4 metres wide, leaving space for a 3 metre sliding door and its future side frame.

| Addition | Location in Unity metres |
| --- | --- |
| West room | X 14–22, Z 50–58 |
| West entrance | X 22–24, Z 52–56 |
| West door position | (22, 0, 54), rotation Y = 90° |
| East room | X 63–71, Z 42–50 |
| East entrance | X 61–63, Z 44–48 |
| East door position | (63, 0, 46), rotation Y = 90° |

The **orange** and **cyan** floor outlines identify the west and east rooms. Thick strips identify the door sills. These are visual layout guides with no blocking colliders. Room perimeter and doorway walls now exist under **07 / Modular walls**; moving doors and agents come later.

Room tiles live under `01 / STONE FLOOR / editable one metre tiles`, in the `West holding room / 8m x 8m` and `East holding room / 8m x 8m` groups. Door anchors and outlines live under `06 / Security holding rooms`. Select a `DoorOpening_...` object to inspect the 3 × 3 metre door volume in Scene view with Gizmos enabled. Hide `06 / Security holding rooms` to hide the guides while retaining all room floors.

To reposition a complete room, select its floor group, matching guide group, perimeter wall sections and doorway wall frame together. Its tiles are the same editable stone prefabs used by the original floor.

The planned encounter condition is **first arena cleared + player enters the tunnel**, followed by the holding-room doors opening. This condition is documented only; it is not implemented in this floor pass.

## Edit the floor manually

1. Stop Play Mode before editing; changes made during Play are discarded by Unity.
2. Expand `01 / STONE FLOOR / editable one metre tiles` in the Hierarchy.
3. Expand a section such as `fight1`, then a `Row Z=...` group.
4. Select a `Tile [X, Z] / NN` **parent object**. Press **F** in Scene view to focus it.
5. Use **W** to move or **E** to rotate. Move in increments of **1 metre** and rotate Y in increments of **90 degrees**.
6. Duplicate with **Ctrl+D**, or drag another `FloorTile_01` through `FloorTile_16` prefab from `Assets/Skybreak/Handbuilt/Prefabs` into the scene.
7. Tile centres use X/Z values ending in `.5`; keep Y at `0` for a flat floor.
8. Press **Ctrl+S** to save the scene.

Each parent prefab contains the visible stone mesh, a ground mesh filling the joints, and a simple 1 × 1 metre floor collider. Move the parent so all three stay aligned. Imported FBX children contain Blender-to-Unity axis/unit conversion; leave those child transforms alone.

The 16 variants have different cracked stone patterns. Each surface is 0.985 metres wide, leaving a narrow joint on the 1 metre grid. Small stone chips and seam grass are separate objects under `02 / Floor wear`; move or delete them independently.

## Change the material

The saved floor now uses the original Descent colour palette:

| Area | Shared material suffix | Original palette |
| --- | --- | --- |
| Arenas, entrance and holding rooms | `_Floor0` through `_Floor4` | Five grey-green shades |
| Winding passage | `_PassageFloor` | Blue-teal |
| Boss floor | `_CoreFloor` | Muted violet |

In `Assets/Skybreak/Handbuilt/Materials`, each colour has a `Skybreak_StoneAtlas_...` material for the main surface and a `Skybreak_StoneDetails_...` material for its sides and joint ground. These materials share the original stone albedo, normal and smoothness textures. Tints use the original palette with 1.65 brightness compensation because the downloaded albedo is darker than the original untextured floor. Lighting and texture detail affect the final visible colour.

Select a colour material and edit **Base Map's colour swatch** in the Inspector to change every tile using that colour. To colour one tile differently, expand its parent, select the stone mesh and assign the desired Atlas/Details material pair to the corresponding renderer slots; assign the Details material to its joint-ground child too. Keep the material families in their existing slots.

Duplicate an existing scene tile with **Ctrl+D** to retain its colour. Prefabs dragged fresh from the Project window still use the neutral source materials; apply one of the palette material pairs to colour them.

The neutral source materials `Skybreak_StoneAtlas.mat` and `Skybreak_StoneDetails.mat` remain available for the source kit and floor-wear objects. `Skybreak_StoneGrass.mat` remains the grass material.

These are shared URP materials. Changing a material affects every tile using it. Duplicate a material first if you want to experiment on one tile. Lower the material's **Smoothness** to make the floor less shiny. The supplied specular maps were used to create an artistic satin finish; they are not measured roughness maps. Stone remains non-metallic.

Source stone textures are 4K on disk; Unity imports them at **2K**, and grass at **1K**, with mipmaps. The scene uses two directional lights and a small 128-pixel reflection probe. Intel integrated-graphics frame rate has not been measured.

## Blender working source

Open `F:/UnityProjects/Skybreak/ArtSource/FloorKit/SkybreakStoneFloorKit.blend`.

Select the scene **Skybreak Stone Floor Workshop**. Its collections contain:

- `01 - Editable source kit`: the downloaded slabs, ground, fragments and grass, with corrected metre scale and repaired materials.
- `02 - One metre tile variations`: `FloorTile_01` to `FloorTile_16`, ready for mesh/UV edits.
- `03 - Preview lighting`: only the Blender presentation lights and camera.

Select a tile, press **Tab** for Edit Mode, and edit its vertices or UVs. Save the working `.blend`. To update its Unity FBX, use `ArtSource/FloorKit/export_selected_stone.py` from Blender's Scripting workspace; it exports just the selected tile at the origin and restores its presentation position afterward. Unity will reimport the FBX while preserving material remaps and prefab references.

The user's original Blender scene is retained as a separate scene named `Scene`. Downloaded `.blend`, FBX and texture files in `resources/Stone Floor` are unchanged.

## What was built

- 26 Blender exports: 10 downloaded kit meshes plus 16 slab-based tile variations.
- 16 reusable floor prefabs.
- Original 2,639 individually editable floor cells retained, plus 144 room/entrance cells: **2,783 total**.
- Approved continuous Descent footprint retained, with the two requested side-room extensions on the same 1 metre grid.
- 2,783 / 2,783 cells connected to the entrance.
- Original 2,639 floor collider checks passed in the first floor pass; 144 / 144 new room/entrance collider checks passed in this extension.
- Actual Unity close-up and overview captures are in `Reports`.

These downloaded models remain **external assets**, including the slab geometry reused for the variations. The changes here are scale conversion, texture repair, UV variations, Unity materials and layout assembly; this does not make them original student-authored models.

## Original map and source credit

The original `Assets/Skybreak/Descent/Scenes/SkybreakDescent.unity` was preserved, and its file hash was checked after the floor build. A separate saved copy is `SkybreakDescent_Preserved_20261002.unity` in the same folder.

The stone floor before these side-room additions is backed up at `Assets/Skybreak/Handbuilt/Scenes/SkybreakStoneFloor_BeforeSideRooms_20261002.unity`. Room layout coordinates are stored in `Data/SecurityRoomLayout.json`; extension verification is recorded in `Reports/SecurityRoomFloorBuild.txt`. Do not edit that backup scene when continuing the new map.

The complete floor with both rooms, before recolouring, is backed up at `Assets/Skybreak/Handbuilt/Scenes/SkybreakStoneFloor_BeforeOriginalColours_20261002.unity`. Palette verification is recorded in `Reports/FloorPalette.txt`; actual Unity captures are `Reports/FloorOriginalColoursOverview.png` and `Reports/FloorOriginalColoursCloseup.png`. All 2,783 tile colliders were retained. The editor menu **Skybreak → Handbuilt Floor → Apply original floor colours** can reapply the region colours after manual layout edits, creating a fresh backup each time; it overwrites scene floor material choices with the region palette.

**Downloaded asset:** Stone Floor, creator **4engine**, as shown in the user's download-page screenshot. Local source: `F:/UnityProjects/Skybreak/resources/Stone Floor`. The exact listing URL and license text were not supplied with these files; no CC0 status is assumed. The previously downloaded Poly Haven ceramic textures are retained separately and are not used in this scene.

The editor builder refuses to replace an existing floor scene. Continue editing the saved scene rather than running a regeneration command.
