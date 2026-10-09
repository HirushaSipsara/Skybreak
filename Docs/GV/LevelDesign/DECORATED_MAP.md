# Main map: selected dungeon props

Updated 7 October 2026. Scene: `Assets/Scenes/GameLevel/SkybreakMain.unity`.

The user selected a simpler dungeon palette: preserve the existing stone columns and use Mr Scientist wooden crates, Quaternius skulls and the existing cyan CrystalPedestal prefab. The approved Aurora reactor shrine and enclosed wall lanterns were added on 7 October 2026. Floors, walls, side rooms, player and demo scenes remain intact. The shrine gives the hospital emergency-power story a visible focal point in the final chamber. Previous machinery and signs remain available in the recovery snapshot.

## What is in the current scene

| Item | Count | Placement |
| --- | --- | --- |
| Preserved stone columns | 12 | Original court, security-wing and chamber positions; original instance transforms retained. |
| Mr Scientist wooden crates | 55 | 24 cover groups plus seven storage props in bays and side rooms. Most use `Objects_Crate 2`; other crate variants come from the same pack. |
| Enclosed Aurora wall lanterns | 18 | Wall backplates and brackets face inward along the route and chamber edges; replace all earlier Mr Scientist torches. |
| Aurora reactor shrine | 1 | Rear of final chamber, `(42, 0, 95.4)`, facing toward the entrance; enlarged to scale 1.35 (7.22m wide, 6.34m tall, 3.04m deep). Its own structural piers are retained. |
| Cyan crystal pedestals | 8 | Entrance, encounter edges and final chamber perimeter. The two separate pedestals beside the reactor were removed at the user's request. |
| Small skull accents | 12 | Side bays, room corners and chamber edges. |

All other objects from the previous **09 / Dungeon decoration** pass were removed: factory cabinets, previous crate models, low stone barricades, orange strips, signs, arches, pipes, reactor equipment and old block-shaped wall lamps. Imported prefab assets are still available in the Project window; this was a scene-placement cleanup, not an asset-library deletion.

## Cover and routes

24 named `COVER` groups retain the same general layout: two entrance positions, six loading-court positions, two maintenance-alcove positions, eight security-wing positions and six final-chamber positions.

- Low cover uses paired 1.6m wooden crates. The narrow maintenance pair is 1.1m high. The paired crates touch so test rays do not pass through a seam.
- Tall cover uses stacked 1.8m and 1.05m crates, reaching 2.85m.
- Each crate retains its BoxCollider. Tall stacks provide standing concealment; low crates provide partial protection. Crouch and projectile damage logic remain separate integration work.
- The centre route and both room entrances stay clear. The final chamber centre remains open.

Checks: 24/24 groups block front and side rays at 0.8m; tall stacks additionally block a ray at 2m. Arc-sized route sweeps found no added-decoration obstructions. Reports: `Reports/Decoration/CoverCollisionChecks.txt` and `RouteClearance.txt`. These are geometry checks, not a full combat playthrough or NavMesh bake.

Two animated dungeon gates were subsequently added under **10 / Holding-room dungeon gates** at the west and east room entrances. Those entrances now intentionally block passage while closed and clear when opened. Press F nearby in Play Mode for preview. The main travel route is unchanged. Runtime opening, closing, capsule-clearance and threshold-occupancy checks passed for both doors; see `Reports/Doors/DoorChecks.txt`. Gate integration guide: `Docs/GV/Physics/HOLDING_ROOM_DOORS.md`. Enemy release and AI navigation are not connected yet.

## Lighting

The enclosed lanterns use shared charcoal-metal, brass and brighter amber-emission materials. All eighteen lanterns now have enabled warm point lights (range 5m, intensity 1.8); two cyan crystals also have point lights. These twenty lights cast no realtime shadows. The shrine is emissive. Existing scene directional lights remain unchanged. The larger light count has not been benchmarked on Intel graphics; lighting baking and performance on the weakest laptop still need measurement.

## Edit and preview

Expand **09 / Dungeon decoration** in the Hierarchy. Nine groups separate preserved columns, regional crate cover, crystals, skulls, enclosed wall lanterns and the reactor shrine. Edit outside Play Mode, then save.

Prefab paths:

- `Assets/Prefabs/Environment/Decoration/MrScientist_Dungeon/Objects_Crate 2.prefab`
- `Assets/Prefabs/Environment/Decoration/Custom/AuroraArchitecture/VaultWallLantern.prefab`
- `Assets/Prefabs/Environment/Decoration/Custom/AuroraArchitecture/AuroraReactorShrine.prefab`
- `Assets/Prefabs/Environment/Decoration/Quaternius_ModularDungeons/Skull.prefab`
- `Assets/Prefabs/Environment/Dungeon/CrystalPedestal.prefab`
- `Assets/Prefabs/Environment/Decoration/Quaternius_ModularDungeons/Column2.prefab`

Actual Unity captures: `Reports/Decoration/EntranceDecorated.png`, `SecurityDecorated.png`, `ReactorDecorated.png` and `DecorationOverview.png`.

Close-up checks: `Reports/Decoration/EnclosedLanternInUnity.png` and `AuroraShrineInUnity.png`. Final route checks still report zero added-decoration obstructions. Placement and lamp checks are recorded in `AuroraArchitecturePlacement.txt` and `AuroraSceneChecks.txt`.

The earlier large teleportation gate was recreated in Blender. Its separate prefab is `Assets/Prefabs/Environment/Decoration/Custom/AuroraArchitecture/AuroraTeleportGate.prefab`; it has not been placed in the main map and has no teleport gameplay logic. Blender source and editing guide: `ArtSource/Student1/AuroraArchitecture/README.md`. All three new assets retain AI-assisted provenance.

The approved wall-detail concept was subsequently implemented under **11 / Carved walls and guardian statues**: carved bands, 13 diamond panels, 9 copper conduit assemblies and 6 textured guardian statues. The earlier decoration stays in **09** and working gates in **10**. See `WALL_DETAIL_PASS.md` and `ArtSource/Student1/DungeonWallDetails/README.md` for editing instructions. New-detail route checks and statue overlap checks passed; no extra realtime lights were added.

Raw download libraries remain Git-ignored; imported Unity dependencies remain trackable. Attribution is in `Docs/ASSET_CREDITS.md`. Previous cover reports describe superseded layouts.

The six guardian statues in **11** now use scene scale 1.45 (approximately 4.06m tall). The original twelve stone columns retain their original scale. **12 / Loading court power dais** adds a 5.5m circular stone-and-brass floor seal at `(0, 0, 16)` with cyan inlays. Its height is only 0.136m; actual player-controller checks crossed it north and south without jumping. Editing instructions: `ArtSource/Student1/HallPowerDais/README.md`; evidence: `Reports/HallCenterpiece/`.

## Recovery

Branching copper conduits were added under **14 / Branching copper wall vines**: 24 varied assemblies distributed evenly across the court, security wing and boss chamber. Copper lines and cyan fittings use the existing conduit palette. Wall backing and decoration clearance were checked, with no added colliders or lights. See `WALL_DETAIL_PASS.md` for editing and preview paths. Backup before the pipe pass: `Tools/SceneSnapshots/SkybreakMain_before_branching_pipes_20261007_220249.unity`.

Additional floor circles now occupy the security-hall centre `(42, 0, 50)` and boss-chamber centre `(42, 0, 84.5)`, under **13 / Encounter floor circles**. Diameters are 7m and 12m respectively. The boss variant uses dark stone, blue steel and Rift Warden violet. Both retain the original 0.136m height. Footprints are clear of existing cover and architecture; player-controller crossings in both directions passed. No extra lights or combat logic were added. See `ArtSource/Student1/HallPowerDais/README.md` and `Reports/HallCenterpiece/`.

Before these additions: `Tools/SceneSnapshots/SkybreakMain_before_encounter_circles_20261007_194409.unity`.

Before this replacement: `Tools/SceneSnapshots/SkybreakMain_before_selected_props_20261007_152648.unity`.

Before the approved shrine/lantern placement: `Tools/SceneSnapshots/SkybreakMain_before_approved_architecture_20261007_160507.unity`.

Before enlargement, removal of the two rear pedestals and lighting every lantern: `Tools/SceneSnapshots/SkybreakMain_before_larger_reactor_20261007_161539.unity`. Change report: `Reports/Decoration/LargerReactorAndLanterns.txt`.

Before the carved-wall and guardian-statue pass: `Tools/SceneSnapshots/SkybreakMain_before_wall_details_20261007_180820.unity`.

Before enlarging the six statues and adding the hall centerpiece: `Tools/SceneSnapshots/SkybreakMain_before_larger_statues_and_hall_dais_20261007_184756.unity`.

Saved locally. No Git commit or push was requested.
