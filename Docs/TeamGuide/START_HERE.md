# Skybreak — what each member should do next

Updated 3 October 2026, after the functional asset-folder migration. Read this checklist, then your detailed role guide. All paths below are relative to your cloned Skybreak project root.

## 1. Get the current shared project

**Existing clone:** save your Unity/Blender work, close Unity and check `git status`. Commit your intended changes on your own branch before switching branches. With a clean working tree, run:

```powershell
git switch main
git pull --ff-only origin main
git lfs pull
```

If Git reports a conflict or refuses to switch/pull, stop and resolve it with the team. Do not discard your work or force the update.

**First clone:** follow [the setup instructions](README.md#first-setup-on-each-teammates-computer). Use the shared repository, your own Git identity and Unity **6000.6.0f1**. Student 3 can work entirely in Blender and Git.

The main map is **`Assets/Scenes/GameLevel/SkybreakMain.unity`**. Preserved demos are under `Assets/Scenes/Demos/`. Supporting assets now use the functional folders; `Assets/Skybreak/Handbuilt/` and `Assets/Skybreak/Descent/` are retired. See [the current folder map](../FOLDER_STRUCTURE.md).

## 2. First tasks by member

| Member | First deliverable | Working locations | Handoff |
| --- | --- | --- | --- |
| Student 1 — World Builder | Continue the main map; confirm door clearance and walkable routes, then prepare lighting and initial navigation | `Assets/Scenes/GameLevel/SkybreakMain.unity`; environment Models, Materials, Textures and Prefabs folders | Give Students 2/4 doorway dimensions, layer rules and navigation configuration; integrate their reviewed prefabs |
| Student 2 — Systems Engineer | Create an interaction lab and make one reusable security door work with the player | `Assets/Scenes/Tests/Student2Interactions/InteractionLab.unity`; `Assets/Scripts/Player/`, `Physics/`, `Core/Interfaces/`; `Assets/Prefabs/InteractiveObjects/` | Door/prop prefabs, scripts, usage instructions and test evidence |
| Student 3 — Core Developer | Model an original SecurityRobot and AccessConsole from scratch; start with the robot's basic shapes | `ArtSource/Student3/SecurityRobot/` and `AccessConsole/`, each with Source, Exports, Textures and Evidence | `.blend` source, FBX exports, textures, dimensions and screenshots; Student 1 imports into Unity with Student 3 reviewing the result |
| Student 4 — Agent Controller | Create a movement lab; move a capsule through three waypoints with smooth turns and correct stopping | `Assets/Scenes/Tests/Student4Movement/AgentMovementLab.unity`; `Assets/Scripts/AgentMovement/`, `Pathfinding/`, `Core/Interfaces/`; `Assets/Animations/SharedRobot/`; `Assets/Prefabs/Enemies/` | Reusable path follower and animation setup, sample actor prefab, usage instructions and test evidence |

The two lab scenes are **to create**; their folders exist. The current main map is an exploration baseline, with floors, walls, side rooms and Arc. Combat, autonomous agents, working encounter doors and final-boss progression remain to implement.

### Student 1: use the existing environment assets

- Floor FBX models: `Assets/Models/Environment/StoneFloor/`.
- Wall meshes: `Assets/Models/Environment/StoneKit/Meshes/`.
- Floor/wall prefabs: `Assets/Prefabs/Environment/StoneKit/`.
- Materials: `Assets/Materials/Environment/StoneKit/`.
- Textures: `Assets/Textures/Environment/StoneKit/`.
- Layout data: `Assets/Data/World/StoneKit/`.
- Editing instructions: [Stone map guide](../GV/LevelDesign/StoneKit_EDITING.md).

### Student 2: start with the existing player

Use `Assets/Prefabs/Player/ArcExplorer.prefab` and read `Assets/Scripts/Player/DescentCharacterController.cs`. Assign a suitable camera in your lab. Match the main map's player collider configuration rather than assuming the prefab's default size matches it. Start with a simple floor and doorway; build player interaction before adding carry/throw mechanics. Follow [the full interaction guide](STUDENT_2_INTERACTION_PHYSICS.md).

### Student 3: Blender-only workflow

Save each original `.blend` under that model's `Source/` folder. Export FBX to `Exports/`, images to `Textures/`, and progress/UV screenshots to `Evidence/`. Commit your own work incrementally. Student 1 imports your FBX into `Assets/Models/Custom/<ModelName>/`, adds Unity materials and colliders, and builds a prefab. Review scale, appearance and animations together and retain import-workflow evidence. Follow [the complete Blender guide](STUDENT_3_BLENDER_MODELS.md).

### Student 4: test movement before replacing the capsule

Use your own lab camera and a temporary capsule. First demonstrate a path around a corner, stopping at the destination and handling a blocked route. Keep path decisions separate from the motor. Agree the waypoint coordinate system and blocked-path notification with the other members. Add Student 3's model and animations after basic movement works. Follow [the full movement guide](STUDENT_4_AGENT_MOVEMENT.md).

## 3. Agree shared contracts before parallel implementation

In one short team session, agree:

1. World-space waypoint format, arrival tolerance and how paths are replaced/cancelled.
2. Who updates the navigation graph when doors or barricades change.
3. Interaction key, usable distance and the shared interaction interface.
4. Robot dimensions, pivot, facing direction and animation names.
5. Each member's distinct IS agent. The existing guide proposes Striker, Bastion, Gunner and Disruptor; confirm that allocation together.

Every member has an IS responsibility alongside their graphics role. Student 4's shared motor does not replace the other members' agent brains. Student 3's Blender workflow does not remove her agent responsibility; follow the team's agreed logic-development and Unity-integration handoff.

## 4. Branches and integration

Create your own branch from updated `main`, for example:

```powershell
git switch -c feature/student2-security-door
```

Other examples: `feature/world-lighting`, `feature/student3-security-robot`, and `feature/student4-path-movement`. Use an existing task branch if you already have one; do not recreate it.

Student 1 integrates changes into the main map. Students 2/4 work in their labs and reusable prefabs. Save Unity work outside Play Mode; include matching `.meta` files for new Unity assets. Move imported assets through Unity's Project window to preserve their IDs. Original Blender sources stay in ArtSource.

Before a handoff, supply a focused commit/branch, setup instructions, what you tested, screenshots/video and any known limitations. Review and test the integrated project before merging into main. Each teammate then updates their clone; a push does not update another person's Editor automatically.

## 5. Current verification and remaining checks

The folder migration preserved the main map and both demos. All three loaded with zero missing scripts, meshes or materials; scripts compiled; a main-scene Play Mode smoke check confirmed Arc was present, grounded and had its camera/visual references assigned.

This does not establish completed combat, agent logic, a baked main-map NavMesh, final lighting, weakest-laptop performance or a standalone build. Test those as their corresponding tasks are completed. Keep evidence under Docs/GV and Docs/IS and use [the full team guide](README.md) for assignment requirements and submission planning.
