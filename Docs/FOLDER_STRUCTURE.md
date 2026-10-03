# Repository folder map

Updated 3 October 2026. The main scene and preserved demos are organised under `Assets/Scenes/`. Existing supporting meshes, materials, prefabs and scripts remain under `Assets/Skybreak/` with their references preserved. The other new folders are scaffolding for future work.

```text
Skybreak/
├── README.md
├── .gitignore
├── .gitattributes
├── Assets/
│   ├── Skybreak/                       # existing work; keep current references
│   │   ├── Handbuilt/                  # supporting stone environment assets/tools
│   │   └── Descent/                    # shared explorer and earlier demo assets
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
│   │   ├── Environment/
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
