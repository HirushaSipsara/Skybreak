# Weathering, lighting and initial world navigation

Main scene: `Assets/Scenes/GameLevel/SkybreakMain.unity`. This pass prepares the environment for later intelligent-agent integration.

## Weathered walls

190 existing one-metre wall sections use reusable chipped-top mesh variations. Their original textures and lower geometry remain intact. The changed wall sections use matching MeshColliders. Original source meshes are retained; variations are under `Assets/Models/Environment/Dungeon/Weathering/`.

Expand **16 / Weathered stone walls** to edit 20 shallow fracture details and 17 small fallen stones. These details are visual-only. Keep rubble beside the walls and outside cover/door routes. Pipes and doorframe bounds were protected during placement. The generator is `Tools/WeatherDungeonWalls.cs`; changes are AI-assisted procedural asset work.

## Initial navigation

Expand **17 / World navigation and lighting probes** and select its NavMeshSurface.

- Agent type: **Skybreak Medium Robot**. Use this same type on regular-sized NavMeshAgents.
- Radius: **0.45m**; height: **2.44m**; step/climb: **0.30m**; maximum slope: **45 degrees**.
- Geometry: physical colliders; voxel size **0.10m**, tile size **128**.
- Data: `Assets/Navigation/NavMesh/SkybreakMain_InitialNavMesh.asset`.
- Floors and power circles are walkable. Walls, crates and solid props are blockers. Decorative rubble, cavern rocks and the player are excluded.
- Original Humanoid agent settings and demo navigation assets remain unchanged.
- No permanent agents or AI brains were added.

The boss may need a separate surface/agent-size bake once its gameplay collision size is agreed. This initial surface should not be assumed to fit a much larger boss.

### Holding gates

Each moving grille has a box NavMeshObstacle matching its physical gate collider. Carving updates as the gate lifts; the moving grille is excluded from the static geometry bake. Fixed rails remain blockers. Agents wait/replan when a closed gate blocks their route; encounter logic still decides when to call the existing door `Open()` method.

Runtime checks showed both holding-room routes blocked when closed, complete when raised, and blocked again after closing. Eight main-route legs were complete through the court, winding passage, security hall and boss chamber. All 62 tested ground-level solid cover centres were outside the walkable NavMesh.

When modifying floor, walls or static cover, rebake the surface in Edit Mode and repeat doorway/route checks. Save the scene and the navigation data with their `.meta` files. The runtime carving needs frame updates; automated Editor tests enable `Application.runInBackground` temporarily while testing an unfocused window.

## Lighting setup

- Settings: `Assets/Lighting/GameLevel/SkybreakMainLighting.lighting`.
- Static environment uses secondary lightmap UVs and baked GI.
- Warm directional light is Mixed, retaining dynamic character shadows.
- Eighty-six point lights (34 lanterns, 8 crystal pedestals, 9 guardian crystals, 34 wall-crystal lights and the final reactor crystal) and the cool directional fill are Baked.
- 506 probes at several heights provide baked lighting for moving characters and gates.
- Initial bake uses 12 texels per metre, 1024-pixel compressed nondirectional atlases, two indirect bounces and automatic filtering. Distant rocks and small wall details have lower atlas density.
- Initial GPU baking is an authoring choice; the baked textures are used during play on all supported machines.

The initial world-preparation bake produced **7 lightmap atlases and all 506 light probes**, verified in Edit Mode and loaded in Play Mode. Generated lighting data, textures and shadowmasks are under `Assets/Scenes/GameLevel/SkybreakMain/`, Unity's scene-associated bake folder. That initial bake mapped 6,880 environment mesh renderers, with no missing scripts or materials. The later lantern-placement bake is recorded separately below.

After changing static geometry, light positions/intensity or baked materials, save the scene and generate lighting again. Keep moving characters and moving gate grilles out of Contribute GI; use their light probes instead. The verified bake result and exact texture paths are recorded in `Reports/WorldFinish/LightingBake.txt`.

Post-bake Play Mode checks confirmed entrance traversal, both gates' closed/open/reclosed capsule collision, and refusal to close on an occupied doorway. Two player-camera lighting previews are retained alongside the room renders and actual NavMesh overview.

### Lantern positions and crystal glow

The four annotated top-view images are represented by 34 wall-mounted lanterns: 8 in the court/entrance, 10 along the winding passage, 8 in the security hall/holding rooms, and 8 in the boss chamber/entrance. Repeated yellow strokes at one location are treated as one lamp. Mounts snap to nearby wall-stone centres, with level brackets on uneven stone faces.

Lantern light intensity is 2.5 (previously 1.8), range 5.5 metres, and amber core emission is 3.0 (previously 2.4). All eight CrystalPedestal crystal meshes use a dedicated cyan emissive material and a soft baked point light at intensity 1.3, range 4 metres. Stone bases and brass settings retain their original materials. These local lights are baked, with no extra realtime shadow lights.

`Reports/LanternPlacement/Placement.txt` contains exact coordinates and the pre-edit scene backup path. `Positions_*.png` previews show temporary yellow lamp markers; those markers are removed before the main scene is saved. `Tools/ArrangeMarkedLanterns.cs` reproduces placement; `Tools/CaptureMarkedLighting.cs` verifies the new bake and saves room/position previews. Current verification is in `Reports/LanternPlacement/Verification.txt`.

Completed lantern-placement bake: **7 lightmap atlases, 506 baked probes and 7,072 mapped environment renderers**. There are 43 baked lights and one Mixed directional light. All 34 lanterns have baked lightmaps and no movement colliders; the original 743-triangle navigation data remains loaded. Missing scripts, meshes and materials: zero. The main scene is saved in Edit Mode; this pass has not been committed or pushed.

### Guardian positions and crystal glow

The later statue pass follows the blue position marks: one guardian at the entrance's rear wall, three around the loading court (north centre and both southern wall returns), and three in the security hall (two northern wall sections and the southern wall centre). Three additional instances bring the total to nine. The two boss guardians retain their positions `(31.5, 0, 90.5)` and `(52.5, 0, 90.5)`, and rotations of 145 and 215 degrees. All statues retain their original 1.45 scale / 4.06 metre height.

Only each guardian's crystal material slot receives the cyan emissive material. Each crystal has a soft baked spill light at intensity 0.9, range 2.8 metres; stone and metal keep their existing materials. The light-probe grid was refreshed to avoid probes inside the moved statues, retaining 506 eligible positions.

Navigation was rebaked around the new statue colliders, retaining the original navigation asset GUID, agent settings and gate carving. The new mesh contains 765 triangles. All eight main-route legs are complete, all nine statue collider centres are excluded from walking surfaces, and the seven placed guardians do not intersect existing solid scenery. Existing lanterns, pedestal crystals, walls and cover retain their transforms.

`Reports/StatuePlacement/PlacementAndNavigation.txt` records coordinates, backups and navigation checks. `Verification.txt` records the completed lighting bake, and seven Unity previews show the guardian glow and placement. Temporary blue markers appear only in the `Positions_*.png` previews. Reproduction tools: `Tools/ArrangeMarkedStatues.cs` (`Main`, then `RefreshProbes`) and `Tools/CaptureMarkedStatues.cs` after baking lighting.

Completed statue-placement bake: **7 lightmap atlases, 506 probes and 7,075 mapped renderers**. All nine statues have valid lightmaps. There are 52 Baked lights, one Mixed directional light and no realtime-only lights. Missing scripts, mesh or material references: zero. The scene is saved in Edit Mode; this pass has not been committed or pushed.

### Glowing crystal wall panels

The cyan marks in the next four top-view images define 30 wall-panel positions: 3 in the court/entrance, 11 along the winding passage, 5 in the security hall and above its holding-room doorways, and 11 in the boss chamber/entrance. The 12 surviving earlier panels are repositioned and 18 additional instances fill the remaining marks. Panel brackets remain upright and snap to wall faces; their mounts are usually 4.2 metres high, 4.55 metres above the holding doorways, and 5.1 metres at the boss chamber's north centre.

The panel mesh retains its frame, backing, dark recess, UVs and centre-diamond shape. A separate fourth material slot isolates the diamond's six triangles, giving only the crystal cyan emission. Each panel has a restrained baked spill light at intensity 0.65 and range 2.4 metres. The source panel mesh/prefab remain available without modifications; scene instances reference the new crystal mesh and material through overrides. No panel colliders are added, so the existing 765-triangle navigation data remains unchanged.

Coordinates and backup paths: `Reports/CrystalWallPanels/Placement.txt`. Bake verification and seven actual Unity previews: `Reports/CrystalWallPanels/`. Temporary cyan position markers appear only in `Positions_*.png` and are removed before saving. Reproduction tools: `Tools/ArrangeCrystalWallPanels.cs` and `Tools/CaptureCrystalWallPanels.cs` after baking lighting.

Completed wall-crystal bake: **7 lightmap atlases, 506 baked probes and 7,093 mapped renderers**. All 30 panels have valid lightmaps and isolated cyan crystal materials. There are 82 Baked lights, one Mixed directional light and no realtime-only lights. Existing navigation remains at 765 triangles. Missing scripts, meshes and materials: zero. Saved in Edit Mode; not committed or pushed.

### Final teleport-point visual

The large crystal inside `AuroraReactorShrine` has its own shiny cyan emissive material, balanced to retain its facets, and a baked cyan spill at intensity 1.5 / range 5.5 metres. The stone arch, brass sockets and pillar materials remain intact.

The child `Final return point / transparent energy` holds a transparent cyan field with faint violet strands behind the crystal, inside the arch. Its 4.35 by 4.6 metre aperture uses a two-triangle mesh and the separate `Skybreak/Final Teleport Field` shader. Soft ripples radiate outward; the wall remains visible through the effect. It has no collider, shadows, depth writing or baked-GI contribution. Existing portal shaders and demo assets are unchanged.

`Assets/Scripts/Core/World/FinalTeleportVisual.cs` smoothly strengthens the field as Arc approaches within 12 metres, reaching full strength at 4 metres, and gently pulses the crystal emission. The public `SetPowered(bool)` hook hides/restores the field for later quest or encounter activation. This component is visual only: it does not teleport Arc, award completion or end the game.

Installation/backup details and actual Unity previews: `Reports/FinalTeleportPoint/`. Reproduction tools: `Tools/InstallFinalTeleportVisual.cs` and `Tools/CaptureFinalTeleportVisual.cs`. The latter has separate idle/near/power-hook checks and restores Arc's original pose and background setting after testing.

The pre-edit snapshot already contained four additional wall-crystal lights beyond the preceding 30-panel pass; these were retained. This change adds only the reactor's baked spill light. Completed bake: **5 lightmap atlases, 506 probes, 7,097 mapped renderers, 87 Baked lights, one Mixed directional light and no realtime-only lights**. The crystal has a valid lightmap, the field shader has no compilation errors, navigation remains at 765 triangles, and missing scripts/meshes/materials are zero. Play Mode checks confirmed idle field energy 0.6 at 15.4 metres, full near energy 1.15 at 3.4 metres, and the power-off/on hook. Arc's original pose and background setting were restored. Saved locally in Edit Mode; not committed or pushed.

## Environment completion checkpoint

The requested map-completion commit includes these environment passes, their scene dependencies, baked lighting/navigation, reproduction tools and verification evidence. Historical reports retain the Git status at the time they were captured; use the repository log for the current commit state. AI concepts, Relay source work and local asset/scene backups are separate from this map checkpoint. Remote publication is a separate step.

## Reports and reproduction tools

Reports and actual Unity previews are under `Docs/GV/LevelDesign/Reports/WorldFinish/`.

- Weathering backup: `Tools/SceneSnapshots/SkybreakMain_before_world_finish_20261009_211629.unity`.
- Lightmap UV backups: `Tools/AssetSnapshots/BeforeLightmapUVs_*`.
- Navigation setup: `Tools/BakeInitialWorldNavigation.cs`.
- Closed/open/reclosed route validation: `Tools/CheckWorldNavigation.cs`.
- Lightmap/probe preparation: `Tools/PrepareWorldLighting.cs`.
- Bake verification and previews: `Tools/FinishAndCaptureWorldBake.cs`.

These world checks do not complete the IS agents, final combat, boss phase logic or performance testing on the weakest teammate laptop.
