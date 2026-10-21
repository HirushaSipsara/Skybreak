# Skybreak: The Descent

A single-player science-fantasy shooter built in Unity for **SE3032 Graphics & Visualization** and **SE3062 Intelligent Systems**. Explore the Aurora Vault with Arc and Relay, fight its mechanical guardians, and confront Rift Warden in the final chamber.

**Main scene:** [SkybreakMain.unity](Assets/Scenes/GameLevel/SkybreakMain.unity)  
**Unity Editor:** **6000.6.0f1**, pinned in [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt).  
**Rendering:** Universal Render Pipeline. Keep the committed versions in [Packages/manifest.json](Packages/manifest.json).

![Arc in the main dungeon map](Docs/IS/CrystalEchoWarden/MainMap/ThirdPersonView.png)

## Game setting

Arc is a half-human, half-machine pathfinder exploring an ancient fortress around the fractured Aether Core. With Relay's support, Arc crosses stone halls, crystal machinery and security chambers to reach the corrupted Rift Warden.

The science-fantasy mission is to defeat the guardian, awaken the Core, seal the rift and return through the restored gate. The intended closing line is **"Rift sealed. Sky reclaimed."** The complete introduction and victory presentation belong to the pending local work below.

## Development status

| System | Shared main-map implementation |
| --- | --- |
| Dungeon map | Connected loading court, winding passage, security wing, side chambers and boss room. Stone textures, weathering, cavern rocks, conduits, lanterns, statues and glowing crystals. |
| Lighting and navigation | Saved lightmaps, reflection probes and an initial main-map NavMesh. Moving holding gates use carving obstacles. See [world verification](Docs/GV/LevelDesign/Reports/WorldFinish/). |
| Arc and carbine | Imported character and weapon, aligned energy-shot traces, reload animation, double jump, damage feedback and first-person/third-person views. See [combat code](Assets/Scripts/Player/Combat/). |
| Relay | Support companion that follows, heals, defends, retreats and searches for safer positions. |
| Crystal Echo Warden | Four hovering crystal guards, with quiet patrol loops, perception, tactical movement, charged attacks and a defeat breakup effect. |
| Rift Disruptor | Four tactical enemies that respond to the player's aim, telegraph pulses, relocate and adapt to dodges. Defeat scatters the model's armor parts. |
| Vault Hound | Two coordinated hunters inside the security-wing holding rooms. Clearing the loading-court enemies opens their gates and releases the pair. See [integration](Docs/IS/AgentDesign/SawanIS/MainMap/INTEGRATION.md). |
| Rift Warden | Final boss with destructible arms, an exposed-core stage, close rear teleports, attack effects and a death sequence. |

**Pending local work:** the maintainer's working copy also contains a title menu, skippable introduction, pause/defeat screens, objectives, portal extraction and a victory screen. Unlimited energy reserve belongs to that uncommitted mission-flow work. A fresh clone of shared main does not include those features yet.

The shared carbine has a **20-shot magazine, 80 reserve rounds and a 2.2-second reload**. The pending local version retains the magazine and recharge delay while making the reserve unlimited. [LocalMagazine.cs](Assets/Scripts/Player/Combat/LocalMagazine.cs) controls those rules.

## Open and play

### First clone

Install Git, Git LFS and Unity Hub. Install the matching Editor above, then run:

~~~powershell
git lfs install
git clone https://github.com/HirushaSipsara/Skybreak.git
Set-Location -LiteralPath '.\Skybreak'
git lfs pull
git config user.name "YOUR REAL NAME"
git config user.email "YOUR OWN GIT ACCOUNT EMAIL"
~~~

Repository access must be available to your GitHub account. In Unity Hub, add the cloned project root containing Assets, Packages and ProjectSettings. Allow package restoration and asset import to finish.

Open the main scene linked above and press **Play**. Click the Game view to capture the mouse. Only this scene is enabled in the default build list; see [EditorBuildSettings.asset](ProjectSettings/EditorBuildSettings.asset).

If the Game view is blank, check the scene path, finish importing assets, run **git lfs pull** and inspect the Console for compilation errors. Opening a project folder alone does not select its main scene.

### Update an existing clone

Save your work and close Unity. Check **git status**; commit intended changes on your own branch before switching. With a clean working tree:

~~~powershell
git switch main
git pull --ff-only origin main
git lfs pull
~~~

Reopen Unity and the main scene. If Git refuses the update or reports conflicts, preserve your changes and resolve them with the team. A GitHub merge does not automatically update another teammate's Editor.

## Main-map controls

| Input | Action |
| --- | --- |
| WASD / mouse | Move / look and aim |
| Left Shift | Sprint |
| Space, then Space again | Jump and double jump |
| Hold left mouse | Fire the carbine |
| R | Reload |
| V | Switch first-person/third-person view |
| F | Interact with nearby objects or guarded crystals |
| Q | Place a sound decoy, with a cooldown |
| Escape / click Game view | Release / recapture the cursor in the shared combat controller |
| Backspace | Reset the shared combat test state |

The pending local mission flow uses Escape for pause/resume and Backspace for a full mission restart. Its title menu starts gameplay through **Start Mission**.

Testing shortcuts in [LocalCombatDriver.cs](Assets/Scripts/Player/Combat/LocalCombatDriver.cs): **F2** activates the boss, **F7** moves Arc to the boss area, **F3/F4** destroy its left/right arms, and **F5** damages the exposed core. Test scenes have their own controls.

## Intelligent agents

Each coursework member owns one distinct IS agent design. Rift Warden is additional boss scope. Multiple instances of one design remain the same agent type.

| Agent | Decisions and algorithms | Source and guide |
| --- | --- | --- |
| Relay | Layered reflexes and perception, fuzzy urgency, expected utility, threat-aware A* and simulated annealing for positioning. | [Code](Assets/Scripts/AI/Relay/), [design](Docs/IS/AgentDesign/Relay/RELAY_DESIGN.md), [evaluation](Docs/IS/Testing/Relay/EVALUATION.md) |
| Crystal Echo Warden | Acoustic evidence, Bayesian beliefs, utility decisions, A* routes and constrained position search. Separate world-model, decision, movement and debug components. | [Code](Assets/Scripts/AI/CrystalEchoWarden/), [implementation](Docs/IS/CrystalEchoWarden/Implementation.md), [validation](Docs/IS/CrystalEchoWarden/Validation.md) |
| Vault Hound | Goal-driven deployment, shared sightings and roles, A* pursuit, BFS flank candidates and last-sighting sweeps. | [Code](Assets/Scripts/AI/SawanIS/VaultHound/), [design](Docs/IS/AgentDesign/SawanIS/VaultHoundDesign.md), [integration](Docs/IS/AgentDesign/SawanIS/MainMap/INTEGRATION.md) |
| Rift Disruptor | Utility goal selection, GOAP, threat-aware A*, tactical position scoring and Bayesian dodge learning. | [Code](Assets/Scripts/AI/Disruptor/), [design](Docs/IS/RiftDisruptor/DESIGN.md) |
| Rift Warden | Player-motion prediction, Bayesian attack learning, utility scores, expectimax combos and animation-timed attacks. Destroy both arms before attacking the core. | [Code](Assets/Scripts/AI/RiftWarden/), [design](Docs/IS/AgentDesign/RiftWarden/RIFT_WARDEN_AI_DESIGN.md), [evaluation](Docs/IS/Testing/RiftWarden/EVALUATION.md) |

Agent decisions live under Assets/Scripts/AI. Shared world adapters and combat contracts live under Assets/Scripts/Core; movement implementations live under Assets/Scripts/AgentMovement.

### Agent field lab

The [HTML workbench](Tools/AIWorkbench/README.md) explains Relay and Rift Warden and previews boss combat. With Python 3, run from the project root:

~~~powershell
python Tools/AIWorkbench/server.py
~~~

Open **http://127.0.0.1:8765**. **Save to Unity** writes the shared [AgentTuning.json](Assets/Resources/AI/AgentTuning.json). The browser uses a simplified JavaScript simulation; test saved tuning in Unity.

## Test scenes and evidence

| Scene | Purpose |
| --- | --- |
| Assets/Scenes/Tests/CrystalEchoWarden/CrystalEchoWardenLab.unity | Crystal guard perception, combat and navigation lab |
| Assets/Scenes/Tests/RiftDisruptor/RiftDisruptorLab.unity | Disruptor decision and combat lab |
| Assets/Scenes/Tests/Student4Movement/AgentMovementLab.unity | Shared movement and animation checks |
| Assets/Scenes/Tests/Student2Interactions/InteractionLab.unity | Physics and interaction lab |
| Assets/Scenes/Tests/StrikerIS/StrikerLab.unity | Legacy Striker experiment; the main map uses Vault Hound |

Earlier demonstration maps are recoverable from Git history. Test scenes are excluded from the default game build.

Recorded checks include [world navigation](Docs/GV/LevelDesign/Reports/WorldFinish/NavigationChecks.txt), [camera views](Docs/IS/CrystalEchoWarden/MainMap/CameraViewChecks.txt), [Disruptor gameplay](Docs/IS/RiftDisruptor/GameplayChecks.txt), [Disruptor breakup](Docs/IS/RiftDisruptor/MainMap/BreakupChecks.txt) and [Vault Hound integration](Docs/IS/AgentDesign/SawanIS/MainMap/IntegrationChecks.txt).

For the engine-independent Crystal Echo Warden checks, install the .NET SDK specified in [Tools/WardenTests/global.json](Tools/WardenTests/global.json), then run:

~~~powershell
dotnet run --project Tools/WardenTests/WardenTests.csproj
~~~

Evidence describes the tested scenarios and revisions. It does not establish a complete standalone release, final difficulty balance or performance on the weakest teammate's Intel-integrated laptop.

## Team workflow and assets

- Use feature branches and pull requests. Coordinate main-scene edits; test reusable agents and props in separate scenes before integration.
- Commit intended files and matching Unity .meta files. Move assets through Unity's Project window to preserve references.
- Keep Blender originals, exports and evidence under ArtSource. Imported models, materials, textures and prefabs belong under their functional Assets folders.
- Preserve each contributor's source files and actual commit history. Graphics responsibilities and individual IS responsibilities remain separate.
- Track Assets, Packages, ProjectSettings, documentation and source art. Git LFS handles binary types listed in [.gitattributes](.gitattributes).
- Keep generated Library, Temp, caches and local builds out of source control. Local scene snapshots belong under ignored Tools/SceneSnapshots.

| Graphics responsibility | Guide |
| --- | --- |
| World layout, textures, lighting and initial navigation | [Map editing](Docs/GV/LevelDesign/StoneKit_EDITING.md) |
| Player interactions and physics | [Interaction guide](Docs/TeamGuide/STUDENT_2_INTERACTION_PHYSICS.md) |
| Custom Blender models and import handoff | [Modelling guide](Docs/TeamGuide/STUDENT_3_BLENDER_MODELS.md) |
| Path following, smooth turning and animation | [Movement guide](Docs/TeamGuide/STUDENT_4_AGENT_MOVEMENT.md) |

See [asset credits](Docs/ASSET_CREDITS.md), [the folder map](Docs/FOLDER_STRUCTURE.md) and [Unity/Blender MCP setup](Docs/TeamGuide/MCP_SETUP.md). Older team-planning sections describe earlier project stages; use the current code and integration reports for implemented behavior.

