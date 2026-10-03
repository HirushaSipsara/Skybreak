# Student 4 — agent movement, rotation and animation

**Before starting:** follow [the current setup and first-task checklist](START_HERE.md), including pulling the reorganised project.

Read [the team guide](README.md) first. This is a build guide and proposed design, not an already implemented or tested movement system. Recommended values are starting points, not numbers specified by the assignment.

## 1. Your responsibility

The GV brief assigns you **Agent Animation & Movement**: translate mathematical path arrays from IS into smooth 3D movement, rotations and animations in Unity.

You ALSO own one distinct IS agent, like every other member. Our suggested allocation gives you **Disruptor**, but the team must agree it. Creating a shared movement system does not replace your own intelligent decision-making implementation.

Your graphics demonstration should show:

1. An agent receives a path array and follows it without teleporting.
2. It turns smoothly at corners and respects walls and closed doors.
3. Idle, locomotion and action animations match what it actually does.
4. It handles a blocked or invalid route without walking through geometry.
5. Several independent AI brains can use the same movement/animation interface.

## 2. Tools and first task

Use the team's Unity **6000.6.0f1** project and its existing packages. Use Visual Studio, Rider or VS Code for C#, plus Git/Git LFS. Follow the baseline/branch instructions in the team guide before modifying assets.

Put motors/presentation code in `Assets/Scripts/AgentMovement/`, common contracts in `Assets/Scripts/Core/Interfaces/`, clips/controllers in `Assets/Animations/SharedRobot/` or the relevant agent folder, and actor prefabs in `Assets/Prefabs/Enemies/`. Develop in `Assets/Scenes/Tests/Student4Movement/AgentMovementLab.unity`, keeping Student 1's main scene under their ownership. Put your own brain in `Assets/Scripts/AI/Disruptor/` if that allocation is agreed.

**First session:** create a floor, two walls forming a corner and a capsule agent. Supply three safe waypoints manually. Make the capsule walk, stop and turn correctly. Leave combat and elaborate animation until this works.

Use a camera looking down at the lab. The capsule is a temporary visual; your motor must survive replacing it with Student 3's robot.

## 3. Keep the AI and its movement separate

Agree these responsibilities with all four members:

```text
World/perception → individual agent brain → path planner → path follower
                                                     ↓
                                              actual movement
                                                     ↓
                                           animation/presentation
```

| Component | Responsibility | Owner |
| --- | --- | --- |
| Perception/world adapter | Supply sight, sound, last-known target, doors and obstacles | Shared contract; each agent author chooses what their brain uses |
| Agent brain | Choose pursue, flank, attack, retreat, wait or search | That agent's IS author |
| Path planner | Calculate a route using the agreed graph and algorithm | Agreed IS authors/shared implementation with documented contributions |
| Path follower/motor | Turn a supplied route into collision-aware movement | Student 4 |
| Animation presenter | Display movement and requested actions | Student 4, using Student 3's model |
| Damage resolver | Apply an authorised attack once, at the agreed moment | Agent/combat owner |

The motor reports `Arrived`, `Blocked`, `PathInvalid` and actual speed to the brain. It does not decide to chase the player on its own. A brain can cancel or replace a path when it chooses another action.

Similarly, an animation should not secretly select a target or generate damage every frame. Decide one clear owner for each action.

## 4. Agree the path contract before coding

Suggested shared path result:

```text
PathResult
  status: Complete | Partial | NoPath
  worldWaypoints: ordered ground positions in Unity metres
  navigationVersion: graph version used to calculate this path
  targetId: identity of the goal, when applicable
```

Suggested motor commands/reports:

```text
SetPath(result, requestedSpeed)
Stop()
Face(worldPosition)
Report: Moving | Arrived | Blocked | Invalid | Stopped
ActualPlanarVelocity
```

Treat this as a proposed interface for the team to implement together. Keep one shared definition rather than four incompatible copies.

Important rules:

- Copy or take explicit ownership of a submitted array; a planner must not mutate it while the motor reads it.
- Empty `NoPath` means stop and report failure, not walk directly at the target.
- Reaching the end of a `Partial` route does not mean the original goal was reached.
- Reject non-finite positions and invalid/obstructed route segments.
- If a replaced path starts at the agent's current location, consume that first node safely.
- Decide how to project nodes to valid floor height and keep that conversion in one adapter.

### Grid coordinates are not automatically world coordinates

The current handbuilt floor uses **1 m tile spacing**, with Y up and +Z deeper into the facility. The older footprint JSON contains `cellSize: 2`; do not use it without verifying the actual graph definition.

For a grid whose origin is the lower-left tile corner:

```text
world = gridOrigin + ((column + 0.5) × cellSize,
                       floorHeight,
                     (row + 0.5) × cellSize)
```

Here `floorHeight` is relative to the grid origin's Y. If the graph defines its origin at a cell centre, omit the extra half-cell offset. If a grid is rotated, transform its local coordinates into world space instead of assuming global X/Z.

Example: corner origin `(0,0,0)`, cell size 1, ground height 0:

| IS cell `(column,row)` | Ground waypoint `(X,Y,Z)` |
| --- | --- |
| `(7,44)` | `(7.5,0,44.5)` |
| `(8,44)` | `(8.5,0,44.5)` |
| `(9,44)` | `(9.5,0,44.5)` |

For world-to-cell conversion, subtract the grid origin, divide by cell size and use floor rather than truncation; negative coordinates otherwise map incorrectly. Convert each result once. Do not multiply by cell size again inside the motor.

Agree with Student 1 on the graph's origin, passable cells, obstacle inflation for agent radius and door links. A visual tile seam is not automatically an obstacle.

## 5. Create the agent prefab

Recommended starting hierarchy:

```text
AgentRoot                         # feet origin; scale (1,1,1)
  CharacterController
  PathFollower
  AgentBrain / Unity brain adapter
  AnimationPresenter
  VisualRoot                      # model correction/visual scaling only
    RobotVisual                   # Student 3's FBX hierarchy
      Animator
      Body / head / limb joints
  DebugMarkers                    # optional; hidden in final presentation
```

For the initial broad robot, try a **2.2 m** CharacterController height, radius around **0.65 m**, and centre Y=1.1 m. Set the root at its feet. Verify these against the imported model, limb swing and the map's **3 m × 3 m** door openings. The proposed shoulders are wider than its torso: a torso-sized capsule could let arms clip walls. If the team changes the silhouette, revise collider and graph clearance together. These are agent settings, not the player's existing settings.

Keep AgentRoot scale 1 so collider and movement distances are predictable. Put import orientation/visual corrections on VisualRoot. Do not solve a 100-times FBX size error by scaling gameplay roots arbitrarily.

### Choose one movement authority

The recommended first implementation uses a **CharacterController** driven by your path follower. Its `Move` API takes a displacement and constrains movement through collisions; gravity must be handled by your script. [Unity CharacterController.Move](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/CharacterController.Move.html)

Do not also let a dynamic Rigidbody, a NavMeshAgent and animation root motion move this same root. Conflicting movement owners cause sliding, jitter and unreliable collision behaviour.

A NavMeshAgent-based design is an alternative if the team deliberately agrees it. Calling `SetDestination` lets Unity calculate its own path; it does not demonstrate following the team's custom IS path arrays. Document how your chosen motor uses the IS result and how collision/navigation checks support it. Never assume a custom grid route can cross geometry just because a NavMesh exists nearby.

## 6. Follow the array smoothly

Suggested initial tuning:

| Value | Starting point | Check |
| --- | --- | --- |
| Walk speed | 2.5 m/s | Covers distance consistently at different frame rates |
| Pursuit speed | 4 m/s | Does not outrun turns or ignore stopping distance |
| Turn rate | 360 degrees/s | Turns visibly without snapping |
| Waypoint acceptance radius | 0.1–0.2 m | Small enough to preserve safe corner clearance |
| Gravity | About -20 m/s² | Settles on the floor without floating |
| Step offset | About 0.25 m | Appropriate for tile seams; not climbing barricades |

Implementation outline below is **pseudocode**, not a paste-ready tested C# component:

```text
each update(dt):
    validate path status and navigation version
    budget = requestedSpeed × dt
    oldPosition = root.position
    consume duplicate/already-reached nodes with a bounded loop

    while budget > 0 and there is a next waypoint:
        direction = planar difference to next waypoint
        proposedStep = direction.normalized × min(budget, distance)
        check that proposed segment has valid clearance
        move the CharacterController by proposedStep
        measure the actual planar displacement
        if blocked or displacement is much less than proposed:
            stop consuming this frame; update blocked timer
        else:
            subtract the used distance from budget
            advance the node only when it was actually reached
        bound iterations to the remaining nodes / a safety limit

    update vertical velocity; apply gravity movement
    reset downward velocity when grounded; handle ceiling collision
    actualVelocity = (root.position - oldPosition) / dt
    smoothly rotate toward agreed movement/facing direction
    feed actual planar speed and action state to animation
```

Use a movement budget so crossing a short waypoint does not create a pause every frame or overshoot the next one. At turns, consume distance along each safe segment. Never skip several nodes by testing only that the agent is within a large radius; that can cut through a corner wall.

Ensure diagonal paths cannot cut between touching blocked grid cells. A route for a point is insufficient for a capsule: both the planner and segment checks must account for its radius.

Rotation example: create a target yaw from the horizontal direction, then use `Quaternion.RotateTowards(current, target, turnRate * Time.deltaTime)`. The step is angular degrees per update. Do not aim the entire upright body at a waypoint's vertical offset. [Unity Quaternion.RotateTowards](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Quaternion.RotateTowards.html)

Agree whether the root faces travel direction or attack aim direction. For the first version, stop to face the attack target; add independent upper-body aiming only if time allows.

## 7. Doors, barricades and replanning

Coordinate with Student 2. The room enemies can exist behind closed doors; once the first encounter and tunnel trigger conditions are satisfied, both doors open and their AI can find routes to the player.

The movement layer must support the following sequence:

1. Closed door is unavailable in the custom graph and physically blocked.
2. The brain waits/searches or uses another valid route.
3. Door physically reaches its usable opening and emits a state update.
4. The world adapter changes the graph and its version.
5. The brain/planner obtains a new route; the motor follows it out of the room.

Student 1's initial NavMesh and dynamic NavMesh obstacles can support navigation validation, but **NavMesh carving does not update a separate custom IS graph**. Maintain the agreed graph adapter too. [Unity navigation obstacles](https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/manual/AboutObstacles.html)

Starting replan policy: reconsider the route when a relevant obstacle changes, the target moves roughly 1 m, or the agent attempts movement but stays blocked for roughly 0.5 seconds. Coalesce requests and use a modest cooldown, perhaps 0.3–1 second depending on agent count. These are design choices; measure responsiveness.

Do not run a full search every rendered frame. Also do not wait indefinitely while an agent pushes against the same closed leaf. Report blockage and allow the brain to wait, search another route or choose another action.

If the player is out of sight, the brain should use its allowed information, such as the last-seen position or an audible event. Giving every enemy the player's exact location through walls would undermine the tracking behaviour we want to demonstrate.

For several agents, begin with stop/yield behaviour near occupied passages and a small local separation adjustment that is itself collision-checked. Local avoidance handles neighbours; it cannot make an invalid global route pass through a wall. Watch for two agents permanently refusing to move, and give yielding a timeout or priority rule.

## 8. Work with Student 3's robot

Ask for these before she finalises the export:

- Separate body, head, arm and leg meshes.
- Stable names and pivot empties at shoulders/hips, as described in her guide.
- Feet origin, dimensions and stated forward direction.
- Rotation-axis notes and a preview showing each joint moves correctly.

A simple robot can animate separate rigid parts without a skinned humanoid rig. You can create Unity transform-animation clips for those part joints. Check the imported joint axes; FBX coordinate conversion means you must not assume every joint's local X has the same meaning.

Student 1 imports the FBX/materials first. You create a gameplay prefab that contains it as the visual child. Avoid renaming/reparenting the imported hierarchy after animation clips bind to its paths; those changes can break clip bindings.

If she chooses an armature, agree a Generic rig and how clips will be authored/exported. Do not configure a robot as Humanoid unless it actually meets that rig's requirements. A full humanoid rig is optional for this proposed design.

## 9. Build the Animator step by step

Unity's Animator Controller organises clips and state transitions. Use a small controller before adding blend trees or extra layers. [Unity animation overview](https://docs.unity3d.com/6000.0/Documentation/Manual/AnimationOverview.html)

### Create the first clips

1. In your lab, instantiate the imported robot inside the gameplay prefab and select its animation root.
2. Open **Window → Animation → Animation**. Create `Robot_Idle.anim` in your Animation folder; check where Unity adds the Animator.
3. Add local rotation keys to the shoulder/hip joints. A subtle repeating idle cycle is enough.
4. Create `Robot_Walk.anim`, around 0.8 seconds to start. Opposite arm/leg pairs swing in opposite directions; add midpoint keys and return to the start pose at the end.
5. Enable looping for idle and locomotion. Test the loop seam.
6. Add `Robot_Run.anim` only if you have a distinct run state. Make a quicker cycle, rather than translating the root forward.
7. Create short attack, hurt and death clips. Leave one-shot clips non-looping. Animate the relevant part joints; establish a clear telegraph pose for Disruptor's pulse.

Begin with small swing angles, roughly 15–25 degrees, and watch for limbs entering the torso/floor. Tune by looking at the imported model, not only by copying values.

Keep these clips **in place**: do not keyframe gameplay-root position or rotation. Turn off **Apply Root Motion** for this script-driven setup, and do not add an `OnAnimatorMove` implementation that independently moves the actor. Unity describes root motion as animation-driven displacement, and its checkbox behaves differently when `OnAnimatorMove` is implemented. [Unity Animator.applyRootMotion](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Animator-applyRootMotion.html)

### Controller and parameters

Suggested parameters:

| Parameter | Type | Meaning |
| --- | --- | --- |
| `Speed` | Float | Actual planar speed, metres/second |
| `Attack` | Trigger | Start the authorised attack animation |
| `Hurt` | Trigger | Play the hit response |
| `Dead` | Bool | Stay in the death state |

Create Idle ↔ Walk transitions using Speed, then Run if needed. Use short blended transitions, perhaps 0.08–0.15 seconds initially, rather than abrupt pose changes. You can later replace the locomotion states with a 1D Speed blend tree if this helps.

To make the controller in the Editor:

1. In the Animation folder, right-click → Create → Animation → Animator Controller; name it `SecurityRobotController` and assign it to the robot's Animator. If Create Clip already made a controller, use that one rather than leaving two competing copies.
2. Open **Window → Animation → Animator** and select that controller. Drag your clips into the graph to create states.
3. Right-click Idle → Set as Layer Default State. Add the parameters above in the Parameters tab using the + button.
4. Right-click Idle → Make Transition → Walk. Select the transition arrow; turn off Has Exit Time and add `Speed Greater 0.1` as a starting condition.
5. Create Walk → Idle with Has Exit Time off and `Speed Less 0.1`. Check the Inspector's transition duration settings: seconds and normalised durations are different.
6. Add Attack transitions using the Attack trigger. Let a one-shot Attack return after its exit time to the appropriate locomotion state. Add Dead transitions with the Dead condition and no outgoing transition.
7. Test each parameter manually in Play Mode, then let the presenter set it. Tune thresholds to stop flickering near zero, and check interruption rules before adding Hurt or Run.

Calculate Speed from **actual displacement** after collision, not requested speed. Otherwise a bot walking into a closed door looks like it is running in place. Unity supports damped float updates, for example:

```csharp
// Presenter fragment only: obtain measuredPlanarSpeed from the motor.
animator.SetFloat("Speed", measuredPlanarSpeed, 0.1f, Time.deltaTime);
```

[Unity Animator.SetFloat](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Animator.SetFloat.html)

Set attack triggers once when an attack begins, not each Update. Define whether an attack interrupts walking and what the motor does while it plays. Ensure a dead actor cannot return to locomotion or execute further attacks.

The attack owner must apply damage once per attack at its valid damage window. An Animation Event can call that agreed interface, but it needs an attack ID/state guard so repeated events, interruptions or transitions cannot apply duplicate damage. Do not assign damage simultaneously to a timer and an event.

## 10. Connect the four IS brains

Integrate one agent first, then reuse the same motor/presenter:

1. Agent author supplies a path and requested speed after choosing an action.
2. Motor checks the path and starts following it.
3. Brain receives arrived/blocked/invalid reports and makes its next decision.
4. Action interface requests the appropriate animation and controls attack timing.
5. Motor can stop or face a target without destroying its own components.
6. Add the other agents, each with its own brain/configuration and visible identity.

Do not hide agent-specific decision trees inside the common Animator or path follower. Your own Disruptor brain might compare distance, visibility, cooldown and route danger to choose approach, telegraph, pulse or reposition. Its pulse should be readable and cause the agreed direct player damage; this is separate from the graphics movement deliverable.

## 11. Debugging and acceptance checks

Draw path lines/waypoints and the current node index in development. Display motor status, requested/actual speed, graph version and the brain action. Disable noisy logs/debug overlays for the final video/build.

| Check | Expected result |
| --- | --- |
| Three-point open route | Smooth travel, correct order, final stop; no teleport |
| Duplicate/empty nodes | No endless loop or exception; failure status handled |
| Sharp corner/diagonal blocked cells | No wall clipping or corner cutting |
| 30 and 60 FPS comparison | Similar distance/time; movement uses elapsed time |
| Closed room door | Physical stop and appropriate wait/replan |
| Open room door | Fresh route through a genuinely clear opening |
| Barricade moved into route | Blockage detected; graph/world update and replan |
| Unreachable player/partial path | No false success or direct walk through walls |
| Several agents in passage | No permanent mutual jam or forced overlap |
| Wall collision | Actual speed falls; locomotion animation reflects it |
| Attack interrupted/death | No duplicate hit and no dead actor movement |
| Replace capsule with FBX | Correct size/origin; motor unchanged; clips bind |
| Four agents together | Different brains remain active and responsive |
| Fresh clone/standalone build | No missing Animator/controller/clip/script references |

Record a real performance check on the weakest team laptop. Avoid continuous allocations, per-frame path searches and excessive simultaneous effects. Do not claim integrated-graphics performance solely because it runs on Student 1's RTX laptop.

## 12. What to hand over

- Path follower, rotation and animation presenter source.
- Agreed shared interfaces and a brief integration example.
- Robot gameplay prefab, Animator Controller and all clips, with `.meta` files.
- AgentMovementLab scene with sample valid/invalid paths and obstacles.
- Setup notes: dimensions, layer masks, speed/turn values, gravity, root-motion policy and supported path statuses.
- Test evidence, known limitations and clear authored Git commits.
- Your distinct IS brain/configuration and its own decision/path tests.

Suggested incremental commits: `Follow supplied waypoints with capsule motor`; `Handle blocked route and path replacement`; `Add smooth agent yaw`; `Create in-place robot walk animation`; `Connect measured movement to Animator`; `Integrate Disruptor decisions and movement`.

## 13. Viva preparation

Be ready to explain and demonstrate:

1. How grid coordinates become world positions and why half-cell offsets depend on the graph's origin.
2. Why displacement is speed × time, and how the remaining movement budget avoids overshoot.
3. How capsule radius affects path clearance.
4. Who calculates a path and who follows it.
5. How a door state change invalidates a custom route.
6. Why animation uses measured speed and root motion is disabled here.
7. How joint pivots, clips, parameters and transitions create smooth presentation.
8. How your own IS agent makes decisions, including its memory, scores/heuristics and failure handling.
9. What you measured on the weakest laptop and what you changed in response.

Your first deliverable is the capsule following a safe path. Your final deliverable is the same reliable system presenting all four independent agents smoothly in the team's actual map.
