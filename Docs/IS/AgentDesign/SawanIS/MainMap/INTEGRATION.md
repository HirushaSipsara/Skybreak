# Vault Hound main-map integration

Open `Assets/Scenes/GameLevel/SkybreakMain.unity` in Unity 6000.6.0f1. The `22 / Vault Hound chamber encounter` object connects the two hounds, shared pack/grid and chamber release monitor.

## Encounter

- The west hound starts inside the holding room near `(18, 0.05, 54)`; the east hound starts near `(68, 0.05, 46)`.
- Both remain chambered while the loading court contains living enemies. The serialized release list contains its two Crystal Echo Wardens and two Rift Disruptors.
- Clearing that list opens the existing west and east `DungeonHoldingDoor` gates. After both gates finish lifting, the controller waits for navigation carving to update and releases the pair.
- A dispatch sighting sends the pair toward the player location at court clearance. Later pursuit uses the hounds' own sight and shared memory.
- The pack assigns a direct pursuer and a flanker. A* supplies pursuit paths; BFS supplies flank candidates and search locations.
- Defeating one hound makes its survivor the sole pursuer. Lethal damage stops active attack coroutines, navigation and collision.
- The shared combat restart restores the crystal guards. The release monitor detects their restoration and resets the hounds, pack memory and gates. Scene reload also restores the encounter.

## Runtime connections

| Component | Connection |
| --- | --- |
| `VaultHoundDamageAdapter` | Exposes the supplied `CombatHealth` through the player's and Relay's shared hostile `DamageSink` contract. |
| `VaultHoundBrain` | Finds the real player, filters friendly actors from swipes, respects cover and moves lunges across valid navigation. |
| `VaultHoundGrid` | Shares physics occupancy and optional baked-NavMesh validation between both hounds. The main-map grid excludes actor layer 8. |
| `HoundPack` | Shares sightings, pursuit roles and the survivor transition. |
| `ChamberReleaseController` | Observes the explicit court enemies, opens the actual gates, refreshes the grid and releases the hounds after clearance. |
| `VaultHoundAnimator` | Drives the supplied eight clips from state, speed, search and life parameters. Root motion is disabled. |

The hounds use the existing Skybreak Medium Robot NavMesh type, matching the main-map surface. No additional global bake or agent-type change is required. The original Striker script and metadata remain available for the legacy Striker test scene; Vault Hound is the integrated encounter.

## Verification

[IntegrationChecks.txt](IntegrationChecks.txt) records the real main-scene Play Mode checks. They cover initial locks, animation/material references, partial/full court clearance, gate timing, chamber exit, shared roles, carbine damage, player-only swipes, death during a lunge, survivor behavior and shared restart.

`Tools/CheckVaultHoundIntegration.cs` runs these checks through Unity Pipeline's `run_script` command. It drives actors and damage directly for repeatability. It does not establish player difficulty balance, weakest-laptop frame rate or standalone-build performance.

`Tools/InstallVaultHounds.cs` records the Editor API setup. It refuses to install a duplicate encounter. Configure existing placements and references in Unity's Inspector; keep the pack and grid shared by both hounds.
