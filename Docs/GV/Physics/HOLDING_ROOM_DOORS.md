# Dungeon holding-room gates

Added 7 October 2026 to `Assets/Scenes/GameLevel/SkybreakMain.unity`.

## Find and try them

Expand **10 / Holding-room dungeon gates** in the Hierarchy.

- West holding-room gate: `(22, 0, 54)`.
- East holding-room gate: `(63, 0, 46)`.

Both gates start closed. Press Play, walk within 3.8 metres of a gate and press **F** to open or close it. An on-screen prompt appears nearby. The grille lifts 3.12 metres over 1.4 seconds with easing, clearing the existing three-metre doorway. A continuous moving BoxCollider blocks actors while closed even though the grille has visible gaps. Fixed side rails remain when open. A closing gate reopens if it detects a physical actor at the threshold; Close() is rejected if the threshold is already occupied.

The model was assembled in Unity from mesh parts, with shared dark-metal and brass materials. The gate fits the existing masonry opening without changing the room walls or floor. The cyan authoring sill stripe was hidden at each doorway.

Reusable prefab: `Assets/Prefabs/InteractiveObjects/Doors/DungeonHoldingGate.prefab`.

Controller: `Assets/Scripts/Physics/DungeonHoldingDoor.cs`, namespace `Skybreak.PhysicsInteraction`.

## Connect encounter/agent release later

Assign each gate component to the encounter manager in the Inspector. Its public methods are:

```csharp
using Skybreak.PhysicsInteraction;
using UnityEngine;

// Example fields inside an encounter manager:
[SerializeField] DungeonHoldingDoor westGate;
[SerializeField] DungeonHoldingDoor eastGate;

// Call when the required mission phase begins:
westGate.Open();
eastGate.Open();
```

These calls open the doors; they do not spawn or activate enemies. Use **Fully Opened** Inspector events or the `IsOpen` property to release waiting agents once there is clearance. Available methods: `Open()`, `Close()`, `Toggle()`, `SetOpen(bool)`. Available state: `IsOpen`, `IsClosed`, `IsMoving`. Events: `FullyOpened` and `FullyClosed`.

Disable **Allow Manual Preview** on the scene gate components when encounter logic controls release, so F cannot open them prematurely. Assign **Interaction Target** to the player when reusing the prefab; the two installed instances already have that reference. **Start Open** determines initial state.

Physics collision works, but AI navigation is not connected in this change. A closed physical gate alone does not update a NavMesh route. The agent/pathfinding integration must respect gate state and update its route or navigation blocker when doors move. Navigation baking and enemy logic remain separate work.

## Verification and recovery

Runtime checks passed for both gates: closed actor-sized capsule blocked, opening animation completed, open actor-sized capsule clear, closing animation completed and collision restored. A temporary CharacterController in the threshold prevented closing. This is not a complete enemy encounter test.

Evidence: `Docs/GV/LevelDesign/Reports/Doors/DoorChecks.txt`, `HoldingGateClosed.png`, `HoldingGateOpen.png`.

Backup: `Tools/SceneSnapshots/SkybreakMain_before_holding_doors_20261007_171144.unity`.

The scene is saved with both gates closed. The main travel route is unchanged; room entrances are intentionally blocked until opened. Earlier route reports describe the layout before these gates existed. No commit or push was performed.
