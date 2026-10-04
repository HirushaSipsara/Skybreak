# Skybreak — The Descent

Shared Unity coursework project for SE3032 Graphics & Visualization and SE3062 Intelligent Systems. The current direction is a single-player stylised shooter in one continuous facility: entrance → first encounter → winding passage → second encounter and security rooms → final chamber.

## Game story and mission

**Skybreak — The Descent** follows **Arc**, a rescue engineer sent into the **Aurora Vault** to restore emergency power to Skybreak's district hospital. The Vault is an old underground facility with stone chambers, winding passages and energy machinery. An unexplained failure has shut down its power controls, sealed its gates and cut communication with the surface. The player must reach the central reactor, overcome the dangers inside and restart the emergency supply.

The journey takes place in **one connected map**: the loading court, the maintenance passage and security wing, and the final reactor chamber. Each section takes the player deeper into the facility.

| Section | Player objective | Possible enemy appearances |
| --- | --- | --- |
| **Loading court** | Enter the Vault and open the maintenance passage. | Near the entrance checkpoint, behind cover or around the first gate. |
| **Maintenance passage and security wing** | Follow the winding tunnel and reach the reactor access gate. | Around corners, at corridor junctions and inside the side rooms. Opening room doors can reveal threats or release them onto the route. |
| **Reactor chamber** | Overcome the final obstacle, reach the control console and restore power. | Inside the chamber, near its entrances or in concealed positions around the reactor. |

The tunnel becomes quieter and darker as the player descends. Warning lights, opening doors and movement from hidden rooms build tension. The player continues towards a clear rescue objective throughout the journey.

After the final danger is overcome, Arc activates the reactor console. Lights return through the facility, communication reconnects and the hospital confirms that its emergency systems are online. The closing message is **“Power restored. People first.”**

**Enemy designs are open for each member to develop.** This story does not assign enemy types, numbers, abilities, attack patterns or member roles. Encounter locations provide a shared setting for those designs.

The visual direction remains a stylised cartoon environment with readable lighting and effects, designed for the team's weakest Intel-integrated laptop. This is the intended story; implementation status is listed below. Read the [shareable story and encounter-location guide](Docs/GV/Concept/GAME_STORY.md).

## Current playable scene

Open **`Assets/Scenes/GameLevel/SkybreakMain.unity`**, press Play, then click the Game window. This is the main project scene and the only enabled scene in the default build list.

| Control | Action |
| --- | --- |
| WASD / arrow keys | Walk relative to the camera |
| Mouse | Look around |
| Shift | Run |
| Space | Jump |
| Escape | Release the cursor |
| Click Game view | Capture the cursor again |

This scene currently contains editable stone floors, textured walls, two security-room extensions and a controllable Arc explorer. Combat, operating room doors, the planned agents and final encounter progression are still work to implement. Completed lighting/NavMesh bakes and weakest-laptop performance are not claimed.

Earlier demos are preserved under `Assets/Scenes/Demos/`: `SkybreakLastLightDemo.unity` (city/escort prototype) and `SkybreakDescentDemo.unity` (earlier dungeon environment). Their controls/features differ. They are excluded from the default game build. See [the scene guide](Docs/SCENES.md).

The template scene, duplicate demo copy and six incremental map backups have been removed from Assets. Their original files remain recoverable from Git history. New editor-tool snapshots go to ignored `Tools/SceneSnapshots/` outside Assets.

## Open a fresh clone

1. Ask the repository owner for collaborator access; this repository is private.
2. Install Git and Git LFS. Install Unity Hub and **Unity 6000.6.0f1** for members using Unity.
3. Run the following in your chosen parent folder:

   ```powershell
   git lfs install
   git clone https://github.com/HirushaSipsara/Skybreak.git
   Set-Location -LiteralPath '.\Skybreak'
   git lfs pull
   git config user.name "YOUR REAL NAME"
   git config user.email "YOUR OWN GIT ACCOUNT EMAIL"
   ```

4. In Unity Hub: Projects → Add → Add project from disk → select this cloned root, which contains Assets, Packages and ProjectSettings.
5. Open using the matching Editor and allow package/asset import. Keep the committed package versions; do not independently upgrade them.
6. Open the current scene above. Check Console and test the controls. Save permanent scene changes outside Play Mode.

Student 3 can skip Unity entirely and work on `ArtSource/Student3/` in Blender. Student 1 imports her FBX exports into Unity, with her reviewing the result. See her guide for the assessed import-workflow handoff.

Save each original `.blend` in `ArtSource/Student3/<ModelName>/Source/`; keep Exports, Textures and Evidence alongside that Source folder. Materials and Textures already have Environment, Characters and InteractiveObjects subfolders under Assets.

## Team roles

| Member | Graphics responsibility | Starting locations |
| --- | --- | --- |
| Student 1 | World layout, textures, lighting, initial NavMesh and main-scene integration | `Assets/Scenes/GameLevel/SkybreakMain.unity`; existing environment assets and new environment folders |
| Student 2 | Player interactions and physics | `Assets/Scripts/Player/`, `Assets/Scripts/Physics/`, `Assets/Prefabs/InteractiveObjects/` |
| Student 3 | Two original Blender models and assisted import workflow | `ArtSource/Student3/`, imported assets in `Assets/Models/Custom/` |
| Student 4 | Follow IS path arrays, smooth turning and animation | `Assets/Scripts/AgentMovement/`, `Assets/Animations/`, enemy prefabs |

Each member designs their own IS agent and agrees its integration with the group. The shared story establishes the mission and possible encounter locations; it does not require other members to build regular hostile enemies or use a particular enemy type. Existing AI folder names are earlier scaffolding, not an agreed roster or behaviour requirement.

Student 1's boss model, 19-bone rig and 20 animation clips are available at `ArtSource/Student1/RiftWarden/Source/RiftWarden_Animated.blend` and `Assets/Models/Custom/RiftWarden/RiftWarden_Animated.fbx`. The FBX has been imported into Unity, but boss gameplay, controller and scene placement remain to implement. See the [boss handoff guide](Docs/IS/AgentDesign/RiftWarden/HANDOFF.md).

Agent decisions belong in `Assets/Scripts/AI/<AgentName>/`. `Assets/Scripts/Core/World/` is for observations, world-state access and adapters connecting navigation/perception to the game; it is not a home for agent behaviour. Gameplay folders are organised by function, while GV/IS evidence is grouped under Docs.

## Working together

- Start with [what each member should do next](Docs/TeamGuide/START_HERE.md), then read [the complete team guide](Docs/TeamGuide/README.md) and [the folder map](Docs/FOLDER_STRUCTURE.md).
- Keep `main` as the reviewed integrated project. Use a feature branch for each task.
- Student 1 owns the main map scene. Other Unity members use the separate test-scene folders and reusable prefabs, then hand those to Student 1 for placement.
- Save your work, review the diff, commit only the intended changes and matching `.meta` files, push your branch and open a pull request.
- Test before merging, then everyone updates their local `main`. A Git push does not automatically update another person's Unity Editor.
- Save/close Unity before switching branches or pulling for the team's beginner workflow.
- Preserve `.meta` identities; move existing assets using Unity's Project window.
- Use your own commit identity and genuine incremental history. Do not rewrite another member's work as yours.

Example feature names: `feature/world-walls`, `feature/security-doors`, `feature/custom-models`, `feature/agent-movement`.

## Files and source control

Track Assets with their metadata, Packages, ProjectSettings, original art sources, tooling and documentation. Git LFS stores models/textures and other binary assets according to `.gitattributes`.

Library, Temp, Logs, UserSettings, IDE caches, local build outputs, `.blend1` backups and machine-local Codex configuration are ignored. Do not commit credentials. New folder `.gitkeep` markers preserve empty scaffolding through clones; Unity folder metadata is included too.

The first commit captures the current saved project and team scaffolding. Static integrity checks do not replace opening a fresh clone or testing a standalone build.

## Role guides and credits

- [Student 2 — interaction physics](Docs/TeamGuide/STUDENT_2_INTERACTION_PHYSICS.md)
- [Student 3 — Blender models and Unity handoff](Docs/TeamGuide/STUDENT_3_BLENDER_MODELS.md)
- [Student 4 — path movement and animation](Docs/TeamGuide/STUDENT_4_AGENT_MOVEMENT.md)
- [Existing map editing instructions](Docs/GV/LevelDesign/StoneKit_EDITING.md)
- [External asset credits](Docs/ASSET_CREDITS.md)

GV requires source and a 3-minute demo, with the supplied brief's deadline of 21 October 2026. Confirm IS submission details separately. See the team guide for requirements, role evidence, calendar targets and viva preparation.
