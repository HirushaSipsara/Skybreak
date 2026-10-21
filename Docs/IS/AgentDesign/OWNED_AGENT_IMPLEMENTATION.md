# Student 1 agent implementation — 10 October 2026

## Status and ownership

The World Builder preparation is complete: internal geometry, textures, chipped walls, baked lighting, probes and initial navigation. The environment checkpoint is local commit `f900c0f`; this agent work has not been committed or pushed.

This pass implements Student 1's Relay decision logic and the additionally chosen Rift Warden boss. The main scene contains both actors under `18 / Student 1 owned agents`. It retains the existing environment lighting and original navigation asset, and adds a separate wider boss navigation surface. This is an AI-assisted implementation; record actual contributions accurately.

The final player weapon, health, double jump, restart, door progression and assessed camera remain Student 2's integration work. Student 4's final shared movement/search integration and the other members' distinct agents remain team work. This pass supplies a working NavMesh motor so the owned agents can be exercised now. It does not claim a custom A* implementation or that all four assessed agents operate together.

## Try combat in the main map or local lab

Open `Assets/Scenes/GameLevel/SkybreakMain.unity`, then press Play. The locally excluded `MainCombatBootstrap` attaches the temporary shooter, health and feedback at runtime, disables the exploration controller and binds boss/Relay to player health. Press F7 to reset the encounter and jump to its entrance. The separate `Assets/LocalCombatLab/OwnedAgentsLab.unity` still starts directly in the arena.

| Control | Action |
|---|---|
| WASD / mouse | Move / aim |
| Shift | Run |
| Left mouse | Rifle, 18 damage per hit; 0.22s minimum shot interval |
| Space twice | Jump / second jump |
| R | Reload the 20-round magazine; 2.2s delay, 80 starting spare rounds |
| Backspace | Restore player, boss, Relay and ammunition to their starting states |
| F2 | Activate the boss encounter |
| F3 / F4 | Break the cannon arm / blade arm to test phase transitions |
| F5 | Finish the exposed core; has no effect while the core is protected |
| F7 | Reset the encounter and move player/Relay to the boss room |
| Escape | Release the cursor |

The temporary controller supplies 100 player health, shooting and two jumps in both test entry points. `Assets/LocalCombatLab/`, its folder meta, and `Tools/LocalCombatLab/` are excluded through this checkout's `.git/info/exclude`. Those exclusions are local; anyone copying these files needs equivalent exclusions. The permanent main scene and both prefabs have no serialized dependency on the temporary code. Main-map shooting requires the local bootstrap to be present; it will not travel with a Git commit of the permanent assets. The lab is not in the build scene list.

### Combat feedback and audio update

Arc now uses an armored explorer presentation: chamfered helmet/armor, cyan visor and energy strips, orange scarf/trim, utility pouches, compact backpack and a larger rifle. Both arms have elbow joints and solve toward the grip/support points; the support hand follows the removable magazine during reload. Damage briefly shakes/rolls the camera, flashes red screen edges and identifies the hit direction. A confirmed damaging weapon hit shows a short hit marker. The two jumps retain their sounds, energy bursts and landing reset. `LocalPlayerFeedback` and the temporary player implementation remain excluded from Git.

The weapon first selects a point under the camera crosshair, aims its barrel toward it and then resolves damage with a second ray from the actual muzzle. Nearby cover blocks the muzzle ray even if the camera can see past it. The camera is updated before acquiring the shot target to avoid using the previous frame's view after a mouse turn. Shot tracers originate at the actual muzzle. At 18 damage, a solo player needs 10 hits per 180-health arm and 15 for the 260-health core, or 35 successful hits total.

`LocalMagazine` starts with 20 rounds loaded and 80 in reserve. Fire requires an available round, no active reload, and the 0.22s shot interval. R starts a 2.2s reload; the empty magazine reloads automatically if reserve remains. Reload transfers only the missing rounds from reserve, blocks shooting and permits movement/jumping. Reload does not grant ammunition when reserve is exhausted. Backspace/F7 resets ammunition and cancels any active reload. The ammo HUD shows loaded/spare rounds and reload time. A magazine insertion pose and short generated mechanical latch sounds accompany the reload.

Warden's cannon recoil now uses a bounded 6-degree shoulder rotation and no shoulder translation. `RiftWeaponFeedback` restores the unmodified animated pose before graph evaluation, clip replacement and every recoil application; it also restores on disable. This fixes the accumulated offset that made the cannon arm drift away during firing, including when animation evaluation pauses. Player weapon/reaction poses remain procedural test placeholders.

`Assets/Audio/Combat/OwnedAgents/CombatSoundBank.asset` contains 12 original synthesized cues: player/cannon fire, teleport, slash, hit, first/second jump, landing, arm break, core opening, defeat and healing. The redesigned set replaces pitched drum/chirp tones with electrical cracks, filtered noise, inharmonic metal tails and restrained air bursts. `Tools/CreateOwnedCombatAudio.cs` reproducibly generates mono 24kHz PCM WAV files (about 209KiB sample payload) while preserving existing GUIDs. `CombatAudio` uses foreground playback for the temporary player and spatial playback for boss/Relay. The camera's existing single AudioListener is retained. Boss/Relay audio is saved on their permanent prefabs and main-scene actors; temporary player feedback also runs in the main map through the excluded bootstrap.

Earlier feedback checks are recorded in `Reports/OwnedAgents/CombatFeedbackChecks.txt`; its player damage/rate values describe the preceding unlimited test weapon. Audio device volume and subjective mixing should be checked while playing on the target machine.

The subsequent `Reports/OwnedAgents/MainCombatChecks.txt` verifies main-map runtime setup, 300 repeated recoil updates without drift, 24 actual cannon shots with an anchored shoulder, redesigned audio sample bounds, protected-core rejection, both arms/core destroyed through actual player shots, return-field activation and the F7 encounter reset. `CannonArmFixed.png` shows the repaired firing pose rendered in Unity.

The latest rifle/reload behavior is checked in `Reports/OwnedAgents/ArcRifleChecks.txt`: aim alignment at different angles, close-cover obstruction, ammo/rate gating, manual reload duration/reserve accounting, animated magazine movement, automatic empty reload, actual player shots through the full boss progression and reset. `ArcRangerRifle.png` is an actual Unity front/back preview of the new player presentation. Older lab test tools that assume unlimited 25-damage shots require adaptation before reuse.

### Gameplay camera alignment correction

The earlier rifle checks supplied aiming rays directly and did not check the camera after firing recoil. A real gameplay measurement found the rendered impact 12.531 pixels below the camera centre because recoil changed the camera after calculating the shot. The controller now queues input in Update and resolves camera, fire recoil, centre ray, damage and rifle pose together in LateUpdate, after other actors/animation. A 1.05m shoulder offset gives a clearer view. The reticle/hit marker use centred geometry aligned to the camera pixel rectangle, rather than a padded text label. Player tracers preserve their original world-space path for their short pulse; the muzzle flash follows the weapon without bending the tracer. Boss tracer behavior is retained.

`AimAlignmentChecks.txt` records six shots through the actual gameplay frame order, including different aim angles, movement and damage shake. All tested unobstructed impacts projected within 0.04 pixels of camera centre. Muzzle cover can still block a shot before the camera's selected point; this is intentional physical obstruction. `GameplayAimFixed.png` captures the actual gameplay view and HUD during a shot.

## Rift Warden

Prefab: `Assets/Prefabs/Enemies/RiftWarden/RiftWarden.prefab`.

`RiftWardenBrain` reads player position and line of sight, remembers the last seen position for four seconds, scores available attacks, and issues movement/presentation requests. It activates on entry to the arena, and holds when the target leaves the arena. Decisions run every 0.2 seconds during pursuit. Visibility queries include environment cover and exclude actor layer 8.

| Condition | Decision |
|---|---|
| Blade intact, target under 3m, slash ready | Slash utility 1 |
| Blade intact, target 4–17m, teleport ready | Double teleport utility 0.95 |
| Cannon intact, target under 19m, cannon ready | Cannon utility 0.4–0.85 with range |
| Neither attack available | Approach using a complete path |
| Sight lost within four seconds | Investigate last seen position |
| Sight memory expired / player outside arena | Stop and wait |
| Both arms destroyed, core open | Core teleport followed by warned slam |

Each arm has 180 health. The core has 260 health and rejects damage until both arms are destroyed and the opening animation reaches 1.5 seconds. An arm's destruction immediately interrupts the old attack, detaches a temporary baked mesh at 0.3 seconds, and disables that arm's renderer/hitbox. This also handles both arms being destroyed during a reaction. Imported bones have 100x scale, so the anatomical hitbox children explicitly compensate that scale.

| Action | Timing and damage |
|---|---|
| Cannon | Aim locks at 0.4s; three hits at 0.567 / 0.867 / 1.167s; each hit deals 8% of the receiver's maximum health; four-second cooldown |
| Close slash | One impact at 0.667s, 2.7m radius, front sector, 30% maximum health; three-second cooldown |
| Double teleport | First close rear arrival after 0.18s; pause one second from the actual arrival; second arrival directly behind the player's current facing, with one immediate 30% hit in the same frame, within 2.15m; ten-second cooldown |
| Core teleport / slam | Arrival at 0.667s, then slam; ground target tracks until 1.267s of slam; impact at 1.5s, 4.2m radius, 30% maximum health; seven-second cooldown |
| Recovery | 1.15s after the attack clip finishes |

Markers and animation use elapsed time through `TimedClipPresenter`; each impact executes once even when a frame crosses multiple markers. The revised rear signature retimes the existing teleport clip during its one-second pause and seeks to the slash impact pose on the second arrival. There is no extra radius warning delay for this signature. Low attacks can be jumped when the actor's feet clear the impact plane by 1.05m. Moving out of the other attacks' locked impact areas also avoids damage. Cover blocks damage. Slam warnings stay on the ground when the player jumps.

The double-teleport signature places the boss 1.6m directly behind the player at each arrival, recomputing position and facing immediately before each warp. This is centre-to-centre clearance for the boss/player capsules, rather than a circular/random placement search. A larger player capsule increases the necessary separation. The position must fit the boss's NavMesh and pass world capsule/actor occupancy checks; if it cannot fit, the attack cancels without another side/front destination. The core-phase teleport retains its separate warned candidate-placement behaviour and cancels if its locked point becomes occupied.

The boss uses the existing 20 Generic animation clips, with root movement kept in the pose and horizontal movement handled by the actor motor. Core-phase clips preserve the open chest. Defeat triggers `BossDefeated` once and powers `FinalTeleportVisual`. This enables the existing return field; actual level completion/transport is still a gameplay integration task. Combat cues use explicit URP Unlit materials: violet cannon tracers, muzzle/impact pulses and rear-arrival flashes. The local player weapon also shows cyan tracers and muzzle/impact flashes. These effects reuse their renderers/materials after first use and add no real-time lights. Sound cues are supplied by the separate `CombatAudio` component.

## Relay

Prefab: `Assets/Prefabs/Companions/Relay/Relay.prefab`. Its modular placeholder body follows the teal/cream/orange concept and can be replaced without changing decisions.

`RelayBrain` is a utility selector with a 0.2-second sensor interval. It selects visible hostile damage targets within 16m; shielded/destroyed boss parts are excluded. Threat memory lasts three seconds.

| Action | Rule |
|---|---|
| Follow | Utility 0.15–0.65 based on player distance; seek a position behind/to the side when over 3.3m away |
| Heal | Player missing at least 25% health; utility 0.45 + missing fraction × 0.55; heal 18 within 4m and clear sight; five-second cooldown |
| Defend | Visible threat near player; utility 0.35–0.70; 10 damage within 12m; 1.8-second cooldown |
| Retreat | Relay below 30% of its 90 health and recent threat remembered; utility 1; seek a point 4m away from threat |
| Reposition | Requested route unavailable; retry a player-side position or wait for a fresh plan |
| Disabled | Health zero; stop movement and clear cues |

Damage, healing, decisions, navigation and presentation are separate components. Its visible body hovers while its navigation root follows the ground mesh.

## Teammate integration contract

### Student 2 — final player

1. Add a final player adapter deriving from `Skybreak.Combat.DamageSink`. Forward `ReceiveDamage` and `RestoreHealth` to the final player health system. Return actual `Health`, positive `MaximumHealth`, `Team = Allegiance.Player`, and an appropriate torso `AimPoint`.
2. Put actor colliders on layer 8. Keep walls/cover on environment layers. The area-damage query uses actor layer 8 so floor tiles cannot fill its result buffer.
3. Final weapon rays should resolve `RiftDamagePart`/`DamageSink` from the anatomical hitbox. Ignore the boss's general navigation/body capsule when it has no `DamageSink`, and continue to the arm/core collider. Still stop at environment cover. `CanDamage` decides whether a hit is allowed.
4. Bind `player`/`playerVitals` on the two brains, or use the existing Arc object for automatic discovery. Main retains its serialized exploration controller; the local bootstrap replaces its runtime controls with temporary shooting/health for testing. Remove or disable that bootstrap when integrating the final player to prevent duplicate controllers.
5. Hook death/restart to `ResetEncounter` and `ResetSupport`, and reposition actors on valid navigation. Hook `BossDefeated` to the final objective flow. Preserve warning/recovery timing when tuning the final double jump.

### Student 4 — movement and search

`IAgentMotor` exposes `MoveTo(destination, stopDistance)`, `Stop()` and `Blocked`. `NavMeshMotor` is the current baseline. It checks a matching agent type, samples within 1.2m, accepts complete paths only, replans at most every 0.4s unless the destination changes materially, and detects sustained blockage. Both brains currently serialize a concrete `NavMeshMotor`; replacing it with the team's motor needs an explicit adapter/reference refactor. Boss teleport placement still needs its navigation query/warp service.

Keep the original Medium Robot surface for Relay and the regular route. Rift Warden uses radius 0.85m / height 3.5m / step 0.3m / slope 45 degrees, with separate `Assets/Navigation/NavMesh/RiftWardenArena.asset`. Do not use the smaller actor bake as proof that the boss fits. Preserve doorway carving and verify the final planner responds to changed doors; a separate custom graph needs its own door-state updates.

The planning brief does not establish a requirement for handwritten A*. Resolve the lecturer's expected search evidence before treating this baseline as the final assessed movement implementation.

## Validation and remaining work

See `Reports/OwnedAgents/RuntimeChecks.txt` for the initial live Play Mode assertions, `RearStrikeChecks.txt` for the revised close-rear choreography/effects checks, and `Verification.txt` for permanent scene checks. The revised rear signature supersedes the original signature's distant, locked landing tests; those safety methods still apply to the core-phase teleport. Focused tests force individual actions to cover timing/interruptions; separate checks exercise normal autonomous following and boss action selection. These are correctness checks, not a performance benchmark or proof of final multi-agent integration.

Remaining team gates: final player adapter/weapon/camera, other members' agents, shared planner integration, gameplay-controlled doors and end objective, standalone build, and performance measurement on the weakest teammate laptop. No new commit or push was made.
