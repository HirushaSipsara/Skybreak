# Rift Disruptor — agent movement, rotation and animation (Student 4, GV)

**Role (GV brief):** Agent Controller. Translate the IS module's path arrays into smooth 3D movement, rotations and animations.
**Result:** an original 2.28 m mech that follows the Rift Disruptor brain's A\* path arrays through a CharacterController motor. It turns at a capped rate and is animated by seven in-place clips driven by *measured* movement. The same motor and presenter work for any agent.

![Pose sheet: idle, front, walk contact, walk passing, run, charge telegraph, release recoil, death](Images/RiftDisruptor_pose_sheet.png)

## Try it

| Scene | What it shows |
|---|---|
| `Assets/Scenes/Tests/Student4Movement/AgentMovementLab.unity` | **Movement only.** The brain is switched off and you drive the motor with path arrays |
| `Assets/Scenes/Tests/RiftDisruptor/RiftDisruptorLab.unity` | **Full agent.** The IS brain plans; the mech moves, telegraphs, fires and relocates. F8 shows the AI overlay |

| Key (movement lab) | Action |
|---|---|
| Click floor (Shift = run) | Plan an A\* route on the nav grid and follow it |
| 1 | Three-point route |
| 2 | 90° corner course |
| 3 | Route through the doorway |
| 4 | Invalid path (a NaN waypoint) |
| 5 | Empty path |
| 6 | Replace the path mid-route |
| D / B | Toggle the door / the barricade |
| C / X / H / K | Charge / Release / Hurt / Defeat animations |
| V | Swap the robot for the capsule stand-in (the motor is unchanged) |
| T | Frame cap: none, 60, 30 fps |
| Q / E, scroll | Orbit and zoom the camera |
| R | Reset |

## 1. Pipeline and tools

```
Blender 5.2 (Python)                          Unity 6000.6.0f1 (editor C#)                        Runtime
build_rift_disruptor.py  ──FBX──▶  RiftDisruptorAssetBuilder  ──▶  RiftDisruptor.prefab  ──▶  SmoothPathFollower
  parts + pivot empties               import settings, URP materials,       CharacterController         AgentAnimationPresenter
  merge per joint, UVs                wear texture, 7 clips, controller,    + brain + presenter          DisruptorVisuals
  .blend, FBX, preview renders        prefab, lab scenes, preview renders
```

Every step is a script, so the whole asset chain can be rebuilt and reviewed from source.

1. **Model.** Run `blender -b --factory-startup --python ArtSource/Student4/RiftDisruptor/Source/build_rift_disruptor.py -- <repo>`. It writes:
   - `Source/RiftDisruptor.blend`
   - `Exports/RiftDisruptor.fbx` and `Assets/Models/Custom/RiftDisruptor/RiftDisruptor.fbx`
   - preview renders in `Evidence/`
2. **Unity assets.** Use the menu **Skybreak → Rift Disruptor → Build GV assets**. It configures the importer, builds the materials, wear texture, clips, controller and prefab.
3. **Scenes.** **Create lab scene** and **Create movement lab scene**.
4. **Evidence.** **Run movement checks** (writes `MovementChecks.txt`) and **Render previews** (writes `Images/`).

## 2. The model

The design follows the team's reference image: a chunky bipedal mech in teal paint with worn cream armour plates, dark metal under-structure and bronze joint rings. It has orange glowing vents (chest, back, cannon), a visor slit, an antenna, a **right-arm cannon** and a **three-finger left claw**.

| | |
|---|---|
| Size | 2.28 m to the antenna tip; feet at the origin; faces +Z in Unity |
| Triangles | 7,508 |
| Renderers | 18, one per joint; 123 modelled parts merged per joint |
| Materials | 5 shared URP Lit materials: `RD_TealPaint`, `RD_CreamPlate`, `RD_DarkMetal`, `RD_Bronze`, `RD_Glow` |
| Joints | 20 empties: Hips, Torso, Head, Shoulder/Elbow ×2, Wrist_L, 3 claw fingers, Cannon_R, Muzzle, Hip/Knee/Ankle ×2 |

**Why rigid parts and not a skinned rig?** A robot's armour does not deform, so each part simply rides its joint. Transform clips on rigid parts are cheaper than skinning: no bone weights and no skinned-mesh update. They also cannot produce stretching artefacts, and the team guide recommends this approach for the robot.

**Orientation and pivots.** Each joint empty sits exactly at its rotation point, with an identity parent-inverse and *identity rotation*. Two problems had to be solved to keep it that way:
- Exporting with the FBX exporter's experimental "bake space transform" distorted the hierarchy. I turned that off and let Unity's axis conversion handle Z-up to Y-up.
- The first import faced −Z. Rotating a parent 180° would have flipped every joint's local axes, so animation would bend backwards. Instead the build turns each *part's vertices* 180° as it is created.

The asset builder validates the result on every build: model height, feet at y = 0, and left shoulder on −X. A wrong export fails loudly instead of producing a backwards robot.

## 3. Motor: from path array to movement (`SmoothPathFollower`)

The brain hands over a `PathResult`: an ordered array of floor waypoints from the threat-aware A\* and string pulling. The follower copies it, so the planner can reuse its buffers. The shared `IPathFollower` contract means the brain never knows which motor it has.

Each frame:

1. **Target speed.** It is the requested speed × three factors, then capped by braking:
   - **Heading alignment** = 1 below 20° of heading error, falling to 0 at 100°. The robot slows to turn instead of moonwalking sideways.
   - **Corner factor** = within 1.4 m of a waypoint, lerp(1, 0.45, turn/90°). It slows into sharp turns.
   - **Braking cap** = √(2·a·d), where d is the distance left on the path and a = 14 m/s². This is v² = 2ad solved for v, so it stops exactly on the last waypoint.
2. **Ease.** Current speed moves towards the target at +9 m/s² when accelerating and −14 m/s² when braking.
3. **Movement budget.** budget = speed × dt is spent segment by segment, with **one CharacterController.Move per segment** (at most 4 per frame).
   - A fast frame never cuts the chord of a corner.
   - A waypoint only advances when the body *actually* reached it.
   - If a collision eats the step, spending stops for the frame.
4. **Gravity** is a separate vertical move (−20 m/s²), so it never eats the horizontal budget.
5. **Yaw.** `Quaternion.RotateTowards(current, travel direction, turnRate·dt)` with turnRate = 300°/s. It is applied to the root, never pitched towards a waypoint's height.
6. **Measurement.** `PlanarVelocity = actual displacement / dt` after collisions. Animation, the brain and the checks all use this measured value.

**Reports to the brain:** Moving, Arrived, Blocked, Invalid, Stopped. The motor never picks a destination itself.

| Situation | Handling |
|---|---|
| Empty, NoPath or null route; non-finite waypoint; zero or NaN speed | Rejected: `Invalid` with a reason; no movement |
| Segment crosses geometry when submitted (e.g. a door closed after planning) | A thin waist-height sphere-cast along each segment → `Invalid` |
| Body pushes but moves under 25% of the requested distance for 0.5 s | `Blocked`; speed zeroed; the brain replans |
| Replacement path starting at the current position | Reached nodes are consumed in a bounded loop; no jump |
| Partial route | Followed to its end and reported `Arrived`; the brain knows it was partial |
| High frame rates | `minMoveDistance = 0`. Unity's 1 mm default silently drops speed×dt steps below 1 mm, i.e. above about 2,600 fps at 2.6 m/s; the IS gameplay checks caught this in batch mode |

**Clearance.** The nav grid inflates obstacles by body radius + 0.1 m (0.65 m), using a capsule overlap of the robot's own height. The planner's routes are therefore safe for the whole body, the 1.33 m pauldron span included. The motor's segment check is deliberately thinner than the body: smoothed segments run between cell centres and may legitimately pass closer to a wall than a cell centre does.

## 4. Animation

### Clips

All seven clips are authored as keyframes in `RiftDisruptorAssetBuilder`. They are **in place**: no root translation or rotation is keyed.

| Clip | Length | Purpose |
|---|---|---|
| `RD_Idle` | 2.4 s loop | Breathing bob, slow head scan, claw twitch, wide stance |
| `RD_Walk` | 0.9 s loop | Hip swing ±22°; knee 12° under load and 42° in swing; arms counter-swing (the heavy cannon swings less); body drops at each contact and counter-twists |
| `RD_Run` | 0.62 s loop | Hip ±34°, knee up to 72°, 14° forward lean, arms pumping, cannon tucked |
| `RD_Charge` | 0.8 s loop | **The telegraph:** braced crouch, cannon swung forward, claw opened and raised, trembling with the charge |
| `RD_Release` | 0.55 s | Recoil: cannon kicks up, torso rocks back, then it recovers |
| `RD_Hurt` | 0.4 s | Flinch: torso and head snap back, arms flare |
| `RD_Death` | 1.6 s, holds | Staggers, buckles onto the knees and slumps forward |

- **Gait.** The gait is generated from equations, not hand-placed keys.
  - Hip angle = −A·cos(2πt/T).
  - The knee bends a little during stance and a lot during swing (half-sine profiles).
  - The right leg is the left shifted by half a cycle.
  - **Ankles cancel 85% of hip + knee rotation**, so the soles stay roughly parallel to the floor without IK.
- **Rig coverage.** Every clip keys all three Euler channels of all 18 joints. The Animator therefore rewrites the whole rig every frame, and the procedural layers (below) can never accumulate.
- **Binding check.** All 399 curve bindings are verified to resolve on the imported model.

### Animator Controller (`Assets/Animations/Disruptor/RiftDisruptor.controller`)

| Parameter | Type | Set by |
|---|---|---|
| `Speed` | float | **Measured** planar speed, damped over 0.08 s |
| `LocomotionRate` | float | Playback rate = speed / authored speed, clamped to 0.6–1.5, so steps match the ground and feet slide less |
| `Charging` | bool | The pulse is charging (the brain's pulse state) |
| `Release`, `Hurt` | trigger | Brain events, fired once per event |
| `Dead` | bool | Defeat; the controller never leaves the Dead state |

States and transitions:
- **Locomotion** is a 1D blend tree: Idle at 0, Walk at 2.6 and Run at 7.5 m/s.
- **Charge** is entered when `Charging` is set (0.12 s blend).
- **Charge → Release** is listed *before* Charge → Locomotion. A release that also clears `Charging` therefore plays the shot instead of cancelling.
- **Hurt** is reachable from any state unless Dead, and returns to Charge if still charging.
- **Dead** is reachable from any state, with no exits.
- Triggers are reset on death, so a late hurt or release request cannot revive the pose.

### Presenters

- **`AgentAnimationPresenter`** (shared, for any agent):
  - feeds `Speed` and `LocomotionRate`
  - in LateUpdate, adds a clamped **head look-at** (±65° yaw, ±30° pitch, 220°/s)
  - adds a **lean into turns** from the measured yaw rate (up to 7°)
  - **Root motion is off**: the motor alone moves the GameObject.
- **`DisruptorVisuals`** (this agent only):
  - turns brain events into Animator requests
  - points the head at the player it sees or remembers
  - drives the glow: breathing at idle, brightening and flickering with charge progress, a flash on release, dark when defeated.

Removing either presenter leaves the AI unchanged.

## 5. Optimisation decisions

| Decision | Effect |
|---|---|
| 123 parts merged into **18 renderers**, one per joint, with material slots | Far fewer renderers and transforms per robot; each moving part costs one transform update |
| **7.5k triangles**, flat-shaded chamfers | Hard-surface detail comes from bevels, not subdivision |
| **5 shared URP Lit materials** with GPU instancing; compatible with the SRP Batcher | Draws batch across all Disruptors |
| Glow uses **one material instance per robot**, not a MaterialPropertyBlock | Property blocks opt renderers out of URP's SRP Batcher |
| Emission with **no realtime lights** and GI flag None | The glow costs nothing in lighting |
| Wear detail from a **generated 256² block-compressed texture**, tiled ×3 | Tiny memory; no external texture download |
| Mesh compression Low; **Read/Write disabled** | No CPU copy of the mesh |
| Animator culling **CullUpdateTransforms** | Off-screen robots skip posing |
| Transform clips on rigid parts instead of skinning | No skinning cost |
| Motor: **no per-frame allocations**; waypoints copied once per path; at most 4 Move calls per frame | Constant per-frame cost |
| Grid A\* reuses its search buffers (IS side) | No garbage from replanning |

Not yet measured: frame time on the team's weakest laptop with all four agents in the main map. This must be measured before any performance claim.

## 6. Evidence

**`MovementChecks.txt`** contains 14 automated checks, all passing, run against the real prefab and Animator:

| Check | Result |
|---|---|
| Three-point route | Waypoints reached in order; final stop 0.12 m from the end, within the 0.12 m arrival radius |
| No teleporting | Step beyond speed × dt: 0.0 mm |
| Turning | 5.00° per frame at 60 fps, equal to the 300°/s cap |
| Animator speed | Within 0.053 m/s of measured speed |
| 30 vs 60 fps | Arrival 9.300 s vs 9.333 s (0.4%); end positions 3.3 cm apart |
| Corner course at run speed | Body never inside geometry: 0 of 314 frames |
| A\* route through the doorway | Followed end to end |
| Door closed in front of it | Stops 5 cm short of the door face and reports Blocked; Speed parameter falls to 0 |
| New route through the closed door | Rejected as Invalid |
| NaN, empty, null and zero-speed paths | Rejected, with 0 mm drift |
| Replacement path | Followed with no jump |
| Capsule stand-in | Same motor arrives normally |
| Animator states | Charge, Release, Hurt and Dead reached correctly; a late hurt request is ignored after death |

**Other evidence:**
- The IS gameplay checks (`Docs/IS/RiftDisruptor/GameplayChecks.txt`) also pass 9/9 with this prefab and motor in the loop. That covers hit-and-run, aim evasion, stagger, learning, doors, three agents and death.
- Images in `Images/`: pose sheet and individual poses rendered in URP. Blender renders are in `ArtSource/Student4/RiftDisruptor/Evidence/`.

## 7. Integration notes and limitations

- **Placing it in a level:**
  - Place `RiftDisruptor.prefab` and a `DisruptorNavGrid` covering the area.
  - Set the grid's `agentRadius` to 0.65 and `agentHeight` to 2.1.
  - Optionally place `DisruptorAnchor`s.
  - The main map belongs to Student 1, so placement there is a hand-off.
- **Other agents** can reuse `SmoothPathFollower` and `AgentAnimationPresenter` by submitting a `PathResult` and providing an Animator with the same parameters.
- **Local avoidance:** agents do not steer around each other. CharacterControllers prevent overlap, and the brain's destination claims keep Disruptors apart, but a crowded corridor can stall until the 0.5 s Blocked report triggers a replan.
- **Feet:** no foot IK. On slopes or stairs the soles follow the clip, not the ground.

## 8. Viva quick answers

- **Grid to world:** `world = origin + ((col + 0.5)·cell, floorY, (row + 0.5)·cell)`. The half-cell offset is there because the grid origin is the corner of cell (0, 0). See `DisruptorNavGrid.CellToWorld`.
- **Why speed × dt and a budget?** Distance = speed × time makes motion frame-rate independent (the 30/60 fps check). The budget lets one frame finish a segment and start the next without overshooting or pausing at each waypoint.
- **Why measured speed for the Animator?** Requested speed would show running on the spot against a closed door. Measured speed falls to 0, as the check shows.
- **Why is root motion off?** One movement authority. If the animation also translated the root, it would fight the motor and the CharacterController.
- **Who decides where to go?** The IS brain plans the path; the motor only follows it and reports what happened. See `IPathFollower`.
- **How does a door change the route?** The grid rescans the door's cells and bumps its version. The brain sees the version change and replans. A route submitted through the closed door is rejected by the motor's segment check.
