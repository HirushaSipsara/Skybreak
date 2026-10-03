# Repository folder map

Updated 3 October 2026. The main scene and preserved demos are organised under `Assets/Scenes/`. Existing assets now use the functional Models, Materials, Textures, Prefabs, Scripts and Tools folders with their original GUIDs preserved. The other new folders are scaffolding for future work.

## Existing assets: where to work now

| Asset | Current path |
| --- | --- |
| Floor FBX models | `Assets/Models/Environment/StoneFloor/` |
| Wall meshes | `Assets/Models/Environment/StoneKit/Meshes/` |
| Earlier dungeon meshes | `Assets/Models/Environment/Dungeon/Meshes/` |
| Stone materials | `Assets/Materials/Environment/StoneKit/` |
| Stone textures | `Assets/Textures/Environment/StoneKit/` |
| Floor and wall prefabs | `Assets/Prefabs/Environment/StoneKit/` |
| Arc explorer prefab | `Assets/Prefabs/Player/ArcExplorer.prefab` |
| Arc controller | `Assets/Scripts/Player/DescentCharacterController.cs` |
| Arc materials | `Assets/Materials/Characters/Arc/` |
| Map layout inputs | `Assets/Data/World/StoneKit/` and `Dungeon/` |
| Editor tools | `Assets/Tools/Editor/StoneKit/`, `Dungeon/` and `Demos/` |
| Lighting profiles and demo pipeline settings | `Assets/Lighting/StoneKit/` and `Dungeon/` |
| Existing demo NavMesh | `Assets/Navigation/NavMesh/DescentNavMesh.asset` |
| Saved reports and screenshots | `Docs/GV/LevelDesign/Reports/StoneKit/` and `Dungeon/` |
| Map editing guide | [Stone map guide](GV/LevelDesign/StoneKit_EDITING.md) |
| Earlier demo guide | [Dungeon demo guide](GV/LevelDesign/Dungeon_EDITING.md) |

The old `Assets/Skybreak/` folder has been retired. All 670 existing files were preserved; the 641 imported assets retain their GUIDs. Reports and guides, including their existing metadata records, were moved outside Unity's imported Assets tree. Historical reports retain old paths as evidence of the original checks.

## Overall layout

```text
Skybreak/
├── README.md
├── .gitignore
├── .gitattributes
├── Assets/
│   ├── Scenes/
│   │   ├── MainMenu/
│   │   ├── GameLevel/
│   │   │   └── SkybreakMain.unity      # current main game map
│   │   ├── Demos/
│   │   │   ├── SkybreakLastLightDemo.unity
│   │   │   └── SkybreakDescentDemo.unity
│   │   └── Tests/
│   │       ├── Student2Interactions/
│   │       └── Student4Movement/
│   ├── Models/
│   │   ├── Environment/               # StoneFloor FBX, StoneKit and Dungeon meshes
│   │   └── Custom/
│   │       ├── SecurityRobot/
│   │       └── AccessConsole/
│   ├── Materials/
│   │   ├── Environment/
│   │   ├── Characters/
│   │   └── InteractiveObjects/
│   ├── Textures/
│   │   ├── Environment/
│   │   ├── Characters/
│   │   └── InteractiveObjects/
│   ├── Data/World/                    # StoneKit and Dungeon layout inputs
│   ├── Shaders/Environment/
│   ├── Tools/Editor/                  # StoneKit, Dungeon, Demos and shared scene tools
│   ├── Lighting/
│   ├── Navigation/NavMesh/
│   ├── Animations/
│   │   ├── Striker/
│   │   ├── Bastion/
│   │   ├── Gunner/
│   │   ├── Disruptor/
│   │   └── SharedRobot/
│   ├── Prefabs/
│   │   ├── Player/
│   │   ├── Enemies/
│   │   ├── Environment/
│   │   └── InteractiveObjects/
│   ├── Audio/
│   ├── UI/
│   └── Scripts/
│       ├── Player/
│       ├── Physics/
│       ├── AI/                        # proposed distinct brains; not implemented
│       │   ├── Striker/
│       │   ├── Bastion/
│       │   ├── Gunner/
│       │   └── Disruptor/
│       ├── Pathfinding/
│       ├── AgentMovement/
│       └── Core/
│           ├── Interfaces/
│           └── World/                 # world/perception/navigation adapters
├── ArtSource/
│   ├── FloorKit/                      # existing editable source and preparation tools
│   └── Student3/
│       ├── SecurityRobot/
│       │   ├── Source/                # original editable SecurityRobot.blend
│       │   ├── Exports/
│       │   ├── Textures/
│       │   └── Evidence/
│       └── AccessConsole/
│           ├── Source/                # original editable AccessConsole.blend
│           ├── Exports/
│           ├── Textures/
│           └── Evidence/
├── Docs/
│   ├── TeamGuide/
│   ├── GV/
│   │   ├── Concept/
│   │   ├── LevelDesign/
│   │   ├── Lighting/
│   │   ├── Texturing/
│   │   ├── CustomModels/
│   │   ├── Physics/
│   │   └── Optimization/
│   └── IS/
│       ├── AgentDesign/
│       ├── Algorithms/
│       ├── Architecture/
│       └── Testing/
├── resources/                         # existing original downloaded source files
├── Tools/                             # existing external checks/helpers
├── Packages/
└── ProjectSettings/
```

## Where to put each deliverable

| Work | Location |
| --- | --- |
| Current main map | `Assets/Scenes/GameLevel/SkybreakMain.unity` |
| Preserved earlier demos | `Assets/Scenes/Demos/` |
| Door/prop scripts | `Assets/Scripts/Physics/` |
| Player interaction/shooting scripts | `Assets/Scripts/Player/` |
| Tested reusable door/prop prefabs | `Assets/Prefabs/InteractiveObjects/` |
| Student 2 lab scene | `Assets/Scenes/Tests/Student2Interactions/InteractionLab.unity` |
| Student 3 editable Blender source | `ArtSource/Student3/<ModelName>/Source/<ModelName>.blend` |
| Her FBX handoff | `ArtSource/Student3/<ModelName>/Exports/` |
| FBX imported by Student 1 | `Assets/Models/Custom/<ModelName>/` |
| Imported model's materials/textures | `Assets/Materials/Characters/` or `InteractiveObjects/`; corresponding Textures folders |
| Common contracts | `Assets/Scripts/Core/Interfaces/` |
| World/graph adapters | `Assets/Scripts/Core/World/` |
| Path calculations and data | `Assets/Scripts/Pathfinding/` |
| Smooth motor/rotation/animation presenter | `Assets/Scripts/AgentMovement/` |
| Student 4 lab scene | `Assets/Scenes/Tests/Student4Movement/AgentMovementLab.unity` |
| Robot clips/controllers | `Assets/Animations/SharedRobot/` or the relevant agent folder |
| Individual agent brain | `Assets/Scripts/AI/<AgentName>/` |
| Evidence, diagrams and test notes | Relevant `Docs/GV/` or `Docs/IS/` folder |

Use descriptive filenames, namespaces and stable prefab references. A folder named for an agent does not create an AI or require a separate pathfinding engine for each brain. The four members agree interfaces and shared contributions before coding.

## System boundaries

- `Scripts/AI/<AgentName>/`: that agent's decisions, action selection, behaviour state, memory and configuration. Each member owns their distinct agent's logic.
- `Scripts/Pathfinding/`: route data, graph/search algorithms and shared planning services.
- `Scripts/AgentMovement/`: follows supplied paths, turns actors and drives animation presentation.
- `Scripts/Core/World/`: perception observations, world-state access and navigation/game-world adapters. It supplies information and integration services; it does not own agent behaviour or choose attacks.
- `Scripts/Core/Interfaces/`: the agreed contracts between these systems.

Typical locomotion flow: AI decision → path calculation → agent motor → animation → visible 3D character. Other actions, such as an attack request, use the agreed action interface rather than forcing every action through pathfinding.

Keep GV/IS grouping in documentation and evidence under `Docs/`. Gameplay scripts remain organised by function; do not create `Assets/Scripts/GV/` or `Assets/Scripts/IS/`.

## Git and Unity notes

Empty Git folders need a tracked file, so scaffolding uses `.gitkeep`. New Unity folders also have their own `.meta` files with unique GUIDs. Never delete/recreate existing metadata to tidy filenames.

No empty placeholder `.cs` files or fake scenes were created. Add actual tested components through Unity when the member starts that task. A future move of existing assets must be performed through Unity with references checked afterwards.

The main map and two demos were moved through Unity's AssetDatabase with their original GUIDs and scene bytes preserved. Redundant scene copies were removed; see [the scene guide](SCENES.md) for locations and recovery details.

There is no automatic CI build yet. Add `.github/workflows/` when the team deliberately configures a real build job; an empty YAML file would not provide a useful check.
