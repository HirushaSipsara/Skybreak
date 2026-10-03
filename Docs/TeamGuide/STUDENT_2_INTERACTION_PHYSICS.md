# Student 2 — player interaction and physics

Read the [team agreement](README.md) first. This is a guide for work to implement; it does not claim these systems already exist in the handbuilt scene.

## 1. Your assignment responsibility

GV pages 2–3 assign you player interaction physics: scripts and physics that let the player open doors, move barricades or throw objects. These are examples; the PDF does not explicitly require all three. Our recommended deliverable is **one reusable door system plus one movable/throwable physics prop system**. This gives a visible, useful interaction with the AI's navigation.

You ALSO own one IS agent. The proposed allocation is Bastion; agree it with the group. Build its AI in its own module rather than putting agent decisions inside the door or player scripts.

Your finished demonstration should show: the player interacts deliberately, the object behaves physically, obstacles affect navigation, and the player cannot move objects through walls.

## 2. Tools and starting point

- Same Unity Editor as the project: 6000.6.0f1; keep package versions from the repository.
- C# editor such as Visual Studio or Rider, Git and Git LFS.
- Existing player: `Assets/Skybreak/Descent/Scripts/DescentCharacterController.cs`.
- Existing controls: WASD/arrows, mouse, Shift, Space, Esc. Input System is already installed. Read `Keyboard.current`/`Mouse.current` or agree an Input Action setup; do not mix in legacy input accidentally.
- Current player collision height 2.444 m, radius 0.416 m, root scale 1.

Create your own `Assets/Scenes/Tests/Student2Interactions/InteractionLab.unity`. Put player interaction code in `Assets/Scripts/Player/`, physics code in `Assets/Scripts/Physics/`, and reusable prefabs in `Assets/Prefabs/InteractiveObjects/`. Start with a floor, two walls, the existing Arc prefab and one door opening. Test there before asking Student 1 to place your prefab in the main map. Do not run the old Descent builder on the handbuilt map.

The existing `DescentGate.cs` belongs to an older map. It snaps leaves vertically and changes colliders; it is a reference, not a complete physics/interaction system for this map. Write your reusable door component in your own folder.

## 3. Plan your components

| Component to write | Responsibility |
| --- | --- |
| `PlayerInteractor` | Find a nearby usable object, show a prompt and call its interaction method |
| `IInteractable` | Common interaction contract shared by doors and props |
| `SlidingSecurityDoor` | Door state, physical leaf movement, open/close clearance and state events |
| `CarryThrowObject` or `PhysicsProp` | Pick up/drop/throw one Rigidbody safely |
| `InteractionPrompt` | Show a clear key/action message; agree the UI implementation with the group |
| `DoorReleaseCoordinator` | Receives encounter/trigger signals; opens both holding-room doors once |

Suggested contract, to agree with the team before implementing:

```csharp
public interface IInteractable
{
    string Prompt { get; }
    bool CanInteract(UnityEngine.GameObject actor);
    void Interact(UnityEngine.GameObject actor);
}
```

Put an agreed shared interface in `Assets/Scripts/Core/Interfaces/`, not independent duplicate copies in several namespaces. The interactor knows the interface; it should not contain a growing switch over door/barricade/console names.

## 4. First task — interaction detection

1. Add `PlayerInteractor` to the Arc root, without adding another movement script.
2. Assign Arc's actual view camera in the Inspector. Do not create a second AudioListener/camera.
3. Choose **F** for interaction and display it in the prompt. The current controller does not use F.
4. Cast from the camera's screen centre toward the scene. Use a full relevant geometry mask so a wall is the nearest hit; excluding walls would allow interaction through them.
5. Exclude the player layer (currently layer 8). Include interactable objects and blocking environment geometry. Account for child colliders by finding the interactable owner on their parent.
6. After the hit, check distance from the player, not just the camera. Start with a 3.5 m player interaction range and tune it in the lab.
7. If needed, also check line of sight from the player to the selected interaction point. A shoulder camera must not let the player use a console around a solid corner.
8. If the object is valid, show e.g. `F — Open security door`; otherwise clear the prompt.
9. On F pressed this frame, call `CanInteract` and then `Interact` once. A held key must not trigger a door every frame.
10. Handle missing camera, destroyed targets, a released cursor and UI focus. Disable interaction when the game is not taking player input.

First test: a temporary cube prints one deliberate interaction message. Then replace the message with door operation. Do not start with carry, throw, mission logic and AI all in one script.

## 5. Second task — the security door

Main-map door anchors are west `(22,0,54)` and east `(63,0,46)`, both Y rotation 90°. Their frames provide a 3 × 3 m opening. These positions are for integration; your lab door can start at the origin.

### Suggested prefab

```text
SecurityDoor                  # root at centre of opening, Y=0, scale 1
  DoorLeaf                    # Rigidbody + BoxCollider, visual mesh child if needed
  ClearanceVolume             # trigger BoxCollider or an explicit overlap-check shape
  InteractionPoint            # optional point for prompt/range checks
```

Use a simple cube leaf first, approximately **2.96 m wide, 2.96 m high, 0.18 m thick**. Centre it at local Y=1.5. The visual leaf is a placeholder, not one of Student 3's required original models.

A vertical lift is a manageable first design. Place its movement plane clear of the fixed wall frame rather than inside the frame's collider. Choose the closed/open local positions in the lab; the open position must leave the entire 3 m opening usable. Check the raised leaf against the 6.5 m wall/header height.

### Physics and state

1. Give the leaf a Rigidbody with **Is Kinematic on**, gravity off and interpolation on.
2. Keep solid collider geometry on the leaf. Do not give static wall frames a dynamic Rigidbody.
3. Store closed/open positions relative to the door root. Convert them to world positions when calling Rigidbody movement, so rotating the whole prefab still works.
4. Track `Closed → Opening → Open → Closing` explicitly. Make Open requests idempotent.
5. Move the kinematic Rigidbody toward the current target in **FixedUpdate**. `MovePosition` is intended for kinematic movement and supports interpolation; do not also animate the same Rigidbody's Transform from another script. [Unity MovePosition](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rigidbody.MovePosition.html)
6. Decide when passage becomes traversable. Prefer an open-complete event when the leaf has cleared the required height, not immediately when opening starts.
7. Before closing, check the swept doorway clearance for a player, agent or prop. If occupied, wait or reopen. Check only relevant blocking objects, not your own leaf/frame.
8. Emit the agreed event after a meaningful state change. Consumers update navigation; the door should not contain each agent's AI.
9. Let the physical leaf position block the player. Do not disable its collider early while the mesh still looks closed.

For the first integration, one-way mission doors can open and remain open. That reduces edge cases; closing is a useful lab demonstration if time permits.

### Mission handoff

Our planned release condition is **first encounter cleared AND player entered the first tunnel**. This condition is not implemented yet. Student 1 gives you the encounter-clear signal and intended trigger location (the existing plan uses the passage near X=21, Z=27).

The coordinator keeps two booleans, checks their conjunction and requests both doors to open exactly once. An agent must not be spawned on the player's current position to simulate pursuit. Preplaced room agents can become active/replan when their room exit becomes usable.

If you build a trigger volume, ensure the Rigidbody/Collider combination actually produces trigger events in this player setup. The player uses CharacterController, so test callbacks in the real scene rather than assuming a Rigidbody player. A kinematic Rigidbody on a dedicated trigger object is a practical option; do not add a competing dynamic Rigidbody to Arc.

## 6. Third task — movable props and throwing

Use one box-shaped prop first, approximately 1.2 × 0.8 × 0.5 m. Later give it a designed mesh. Suggested starting values, not mandatory assignment values: mass 8–15 kg, gravity on, box collision, modest friction and interpolation.

The CharacterController does not automatically give the same prop-pushing behaviour as a dynamic Rigidbody player. Add deliberate pushing or grabbing logic and test it. [Unity Character Controllers](https://docs.unity3d.com/6000.0/Documentation/Manual/CharacterControllers.html)

### Pick up and carry

1. Keep the prop dynamic when resting: Rigidbody + primitive collider, not a non-convex moving MeshCollider.
2. On pickup, reject heavy/locked objects, out-of-range objects and a second carried prop.
3. Save its original Rigidbody settings and collider/layer state.
4. Create a hold target in front of the **player**, not blindly two metres in front of the behind-the-player camera. An initial target is around chest height, 1–1.5 m ahead of Arc.
5. One manageable implementation uses a kinematic held Rigidbody moved in FixedUpdate. Before moving it, sweep/check its shape toward the target and stop before walls; additionally reject targets already overlapping solid geometry. Kinematic status alone does not prevent clipping.
6. Keep environment collision active. If ignoring collision with the holder, ignore only those specific player/prop collider pairs and restore them on release.
7. When space is blocked, keep the prop at its last safe position or drop it safely. Do not let it teleport through the wall to the target.
8. On drop, restore dynamic behaviour and gravity. Do not release an object while it overlaps the player's capsule or a wall.

### Throw

Choose a key that does not compete with future shooting. Suggested: **G** throws the held prop and F drops it. Leave mouse buttons available for the shooter unless the group agrees another layout.

1. Use the camera's aim direction, but originate the throw from the safe held-object position.
2. Restore a dynamic Rigidbody before adding force.
3. Apply one impulse, not a force every Update frame. With `ForceMode.Impulse`, mass affects the resulting velocity; `VelocityChange` instead changes velocity independently of mass. [Unity AddForce](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rigidbody.AddForce.html)
4. Select a modest initial impulse and tune it with measured travel; the guide does not prescribe a magic force value for every mass.
5. If a fast object tunnels through walls, test an appropriate continuous collision mode and simple collider, and review timestep/speed. Do not blindly use the most expensive mode on every object.
6. Limit speed/throw frequency if needed. Clean up/reset props that fall out of the test area.

For pushing without grabbing, use CharacterController collision information to apply a bounded horizontal push to a dynamic Rigidbody. Avoid applying a downward push to an object supporting the player; do not overwrite all velocities indiscriminately.

## 7. Connect props and doors to navigation

Student 1 owns the initial navigation bake; you provide object states and blocker geometry. Student 4 and every IS planner consume those changes.

- A closed door must prevent path passage. An open door must remove that block after sufficient clearance.
- A moved barricade must change traversability after its relevant position changes.
- NavMeshObstacle carving can make stationary blockers affect NavMesh planning; ordinary local avoidance is not the same as a path-planning block. Carving may update after the object settles and has update timing costs. [Unity obstacle behaviour](https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/manual/AboutObstacles.html)
- If the IS module uses a custom grid, project the prop/door bounds into that grid too. Do not assume NavMesh carving updates a separate data structure.
- Coalesce changes, increment a navigation/world version, and ask affected agents to replan. Avoid a full world rebuild or four searches every rendering frame.

Suggested event contract to agree: `DoorTraversabilityChanged(doorId, isOpen)` and `NavigationChanged(worldVersion)`. Names are proposals, not existing code.

## 8. Required checks before handoff

Record expected and actual results in your notes:

| Test | Expected |
| --- | --- |
| Interact behind a solid wall | No action/prompt through the wall |
| Stand outside range | Cannot operate the door |
| Press/hold F repeatedly while opening | No duplicate state transitions |
| Walk through open room doorway | Arc and the agreed robot fit |
| Close with Arc/agent/prop in opening | Waits/reopens; no crushing or ejection |
| Rotate/move prefab root | Leaf movement still follows the door's local axis |
| Carry toward a corner | Prop remains on the safe side of the wall |
| Drop/throw and repeat | Gravity/collision settings restore; no stuck ignored collisions |
| Change a door/barricade position | Both Unity navigation and custom planner inputs update correctly |
| Agent target becomes unreachable | Agent waits/replans instead of walking through it |
| Save, reopen and build | Inspector references remain assigned; no console errors |

## 9. Deliverables and viva preparation

Deliver your scripts, prefabs, test scene, `.meta` files, a setup note listing fields/keys/layers, a short video and your own commits. Tell Student 1 exactly which root transforms to place and which mission events to connect.

Explain these in the viva: dynamic versus kinematic bodies; why movement/forces use the physics timestep; collider choice; carrying collision checks; mass and impulse; why Arc uses CharacterController; door closing clearance; how dynamic world changes reach the agent planners; and which optimisations you actually measured.

Suggested commit milestones: detection/prompt → reusable door states → physical movement/clearance → pickup/drop → safe throwing → navigation events → integrated map checks. Commit genuine incremental work, not one final upload.
