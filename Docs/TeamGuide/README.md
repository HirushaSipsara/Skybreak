# Skybreak team work guide

Updated 3 October 2026 for the reorganised project. **Start with [what each member should do next](START_HERE.md)**, then use this page and the detailed guide for your graphics role.

To connect the assistant to the applications, use the [Unity and Blender MCP setup guide](MCP_SETUP.md).

## 1. What the assignment actually requires

Sources: the supplied **Assignmnet_GV.pdf** (pages 1–3) and **Assignmnet_IS.pdf** (pages 1–2). The requirements below come from those documents. Specific models, scripts, dimensions, development dates and agent assignments proposed later in this guide are our implementation plan.

| Member | Required GV responsibility | Detailed guide |
| --- | --- | --- |
| Student 1 — you | Level layout, cohesive texturing, lighting bake and initial NavMesh | Your integration checklist below |
| Student 2 | Player physics and environmental interactions | [Student 2 guide](STUDENT_2_INTERACTION_PHYSICS.md) |
| Student 3 | At least two custom models designed and modelled from scratch in Blender/Maya, and import workflow | [Student 3 guide](STUDENT_3_BLENDER_MODELS.md) |
| Student 4 | Smooth movement, rotations and animations following the IS path arrays | [Student 4 guide](STUDENT_4_AGENT_MOVEMENT.md) |

Every member ALSO owns **one distinct autonomous IS agent**, its implementation, commits and technical explanation. Student 4's graphics role does not make them responsible for writing everyone's AI. Student 3's modelling role does not remove her IS responsibility.

GV submission: source code and a **3-minute demo video**, deadline **21 October 2026**. Milestones: Week 11 concept/role pitch, Week 13 imported model and functional interaction prototype, Week 15 showcase/viva. The PDFs do not map these week numbers to calendar dates.

GV marks: cohesion 15 + stability 15 (group); role quality 20 + Git 10 + workflow/tools 20 + design/optimisation justification 20 (individual). IS marks: integration/stability 20 (group); effort/code quality 15 + originality 15 + complexity 25 + technical viva 25 (individual).

The IS brief describes an **FPS environment**, whereas our current camera is third-person for inspecting the map. Plan an FPS mode for the final shooter, or confirm acceptance of third-person with the lecturer. This guide preserves the current exploration controls; it does not claim the finished shooting game exists.

The IS brief states five weeks but supplies no explicit deadline or submission-format list. Confirm its submission details separately. Do not infer that GV's date automatically applies to IS.

## 2. Our current project and scope

Shared project: `F:/UnityProjects/Skybreak` on Student 1's machine. Everyone else's clone can be elsewhere; use the same relative asset paths.

Unity version recorded in ProjectSettings: **6000.6.0f1**. Packages include URP **17.6.0**, Input System **1.20.0**, AI Navigation **2.0.14**. Match these project versions; do not independently upgrade packages.

Main scene: `Assets/Scenes/GameLevel/SkybreakMain.unity`. Earlier demos are stored separately in `Assets/Scenes/Demos/`; the main map is the only enabled default build scene. See [the scene guide](../SCENES.md).

Saved files and previous Play Mode reports show:

- One continuous route: entrance → first battle arena → winding passage → second arena/holding rooms → boss chamber.
- 2,783 editable stone floor tiles, textured perimeter walls, and two security rooms.
- Walls are 6.5 m high, or 7 m in the passage. Room openings are **3 m wide × 3 m high**.
- Arc has 1.3 visual scale, a **2.444 m** capsule height and **0.416 m** capsule radius. Camera pivot 2.08 m, distance 5 m.
- Arc walks, runs, jumps and uses a camera that checks walls. Previous controller checks traversed the route and both doorways.

These are saved-project observations, not a new live test on 3 October: the Unity Editor was unavailable during this guide's preparation. The current builder creates preview lighting; no completed handbuilt lighting/NavMesh bake was established from the saved assets. AI Navigation being installed does not mean a NavMesh was baked.

The handbuilt scene does **not yet** have the requested operating room doors, movable physics props, custom student models, autonomous combat agents, or final shooting/progression systems. Old scenes and HTML prototypes contain other experiments; do not treat them as integrated handbuilt gameplay.

Suggested scene design for this work:

1. Add usable doors and physics objects.
2. Replace agreed placeholders with two original models.
3. Give each agent its own AI brain; connect all brains to one shared movement/animation interface.
4. Finish lighting, navigation, combat and mission integration, then demonstrate them together.

Student 1's own agent work includes a support agent and final boss, with the boss model, rig and animation export available. Other members choose their own agents and coordinate integration with the shared mission. Preserve each member's required role deliverables.

## 3. Work ownership and handoff

| Work | Author/owner | Integrator or reviewer |
| --- | --- | --- |
| Main map scene, static materials, lighting, initial navigation | Student 1 | Students 2 and 4 check clearance/navigation |
| Door, barricade and throwable scripts/prefabs | Student 2 | Student 1 places approved prefabs in the map |
| Robot and access-console `.blend`, UVs, FBX exports, asset notes | Student 3 | Student 1 performs Unity import; Student 4 checks animatable parts |
| Path-following motor, turning, Animator, movement test scene | Student 4 | Each agent author supplies paths and intent |
| Each individual IS brain | Its assigned member | Student 4 connects presentation; group verifies combined behaviour |
| Shooting, damage, health and encounter coordination | Assign explicitly at the first meeting | Suggested: Student 2 owns player-side interaction/combat integration; each AI owner owns that agent's attacks |

**Student 3 cannot run Unity:** she works in Blender and Git, delivers original sources and FBX files, and joins your import review using screenshots/video or a screen-share. You import and test on your computer. Her authored commits must remain hers; your import/configuration commits remain yours. GV explicitly assigns import to her role, so explain this assisted workflow to the lecturer and confirm how she should demonstrate that part in the viva. Do not report that she operated Unity if you did the import.

She can still own an IS agent: write its independent logic in plain C# with a lightweight console test project and fake world inputs, then have you/Student 4 integrate the Unity adapter. Its behaviour, weights, tests and commits are hers. If this arrangement is impractical, resolve it with the lecturer; do not silently assign her entire agent to another member.

### IS ownership and design freedom

Student 1's own designs are Relay and the Rift Warden. Other members choose their own agent concepts, roles and behaviours; they are not assigned regular hostile enemies by the story. Agree how each agent enters the map and interacts with the player before integration. See the [shared story and encounter locations](../GV/Concept/GAME_STORY.md).

The Striker, Bastion, Gunner and Disruptor folder names are earlier scaffolding, not mandatory designs or a final member allocation. Each member remains responsible for the assignment's individual agent requirements.

Each member must explain why their architecture and heuristic/weights suit the agent. A name such as "utility AI" or "FSM" alone does not establish advanced behaviour. Show dynamic choices, memory, blocked-route handling and evidence. No particular algorithm is mandated in the supplied briefs.

## 4. Project organisation and Git

The team folder scaffold is created for the first baseline; folders do not create working game components. See [the complete folder map](../FOLDER_STRUCTURE.md). Existing work has been migrated into the functional folders, with asset GUIDs preserved. See the folder map for current paths.

```text
Assets/Scripts/Core/Interfaces/       # agreed interfaces
Assets/Scripts/Core/World/            # world/navigation adapters
Assets/Scripts/Player/                # player interaction and future shooting
Assets/Scripts/Physics/               # doors, props and throwing
Assets/Scripts/AgentMovement/         # common motors and animation presenter
Assets/Scripts/Pathfinding/           # shared route data and algorithms
Assets/Scripts/AI/<AgentName>/        # each distinct brain
Assets/Scenes/Tests/                  # separate interaction and movement labs
Assets/Models/Custom/<ModelName>/     # Unity FBX imports
Assets/Prefabs/                       # Player, Enemies, Environment, InteractiveObjects
Assets/Animations/                    # shared robot and agent-specific clips
ArtSource/Student3/                   # editable Blender files, export notes and evidence
Docs/TeamGuide/                       # this guide
```

For each Student 3 model, keep the original `.blend` under `<ModelName>/Source/`, FBX files under Exports, image files under Textures, and modelling/UV screenshots under Evidence. Both Source folders are present in the scaffold. Materials and Textures each have actual Environment, Characters and InteractiveObjects subfolders.

Keep each agent's decision-making and behaviour under `Assets/Scripts/AI/<AgentName>/`. `Core/World/` contains observations, world-state access and integration/navigation adapters, not agent decisions. Use the functional folders for gameplay code; GV/IS grouping belongs in Docs and evidence.

Let Student 1 integrate `Assets/Scenes/GameLevel/SkybreakMain.unity`. Other Unity members develop in small separate test scenes and reusable prefabs. This reduces competing edits to a large scene file. Preserve `Assets` `.meta` files; they hold reference identities.

The first baseline captures the saved Unity project, original art sources, folder scaffold and guides. The configured remote is `https://github.com/HirushaSipsara/Skybreak.git`; its default branch is `main`. The reviewed asset reorganisation has been pushed to main; teammates should update their clones before starting new work. See Git history for the actual commit/push state; a documentation statement alone is not proof of publication.

The current `.gitattributes` already marks `.blend`, `.fbx` and many other binaries for Git LFS. Check/install LFS before adding assets. Verify another clone actually downloads the models, not only pointer text. Do not track Library, Temp, Logs or local build outputs.

### First setup on each teammate's computer

Student 1 must give everyone collaborator access to the private repository. Existing clones follow the update steps in [Start here](START_HERE.md#1-get-the-current-shared-project).

1. Install [Git for Windows](https://git-scm.com/install/windows) and [Git LFS](https://git-lfs.com/), then reopen your terminal. Run `git --version`, `git lfs version`, and `git lfs install`. The LFS setup command installs its hooks/configuration for your account; follow the official LFS setup if the command is unavailable.
2. Clone the shared repository to a local working folder using its actual URL. Do not create a separate unrelated Unity project or copy only the scene file. Use this repository URL:

   ```powershell
   git clone https://github.com/HirushaSipsara/Skybreak.git Skybreak
   Set-Location -LiteralPath '.\Skybreak'
   git lfs pull
   git config user.name "YOUR REAL NAME"
   git config user.email "YOUR OWN GIT ACCOUNT EMAIL"
   git status
   ```

   These identity settings are repository-local. Use your own identity, not Student 1's. If authentication fails, ask Student 1 to verify repository access; do not share passwords or tokens in commits.
3. **Students 2 and 4:** install Unity Hub and the project's exact Editor version, **6000.6.0f1**, plus your C# editor. In Hub choose **Projects → Add → Add project from disk**, then select the cloned root containing Assets, Packages and ProjectSettings. Open it with the matching version. [Unity Hub project workflow](https://docs.unity.com/en-us/hub/project-manage)
4. Wait for package/asset import; the first opening can take longer than later openings. Check Console for errors, open `Assets/Scenes/GameLevel/SkybreakMain.unity`, and enter Play Mode briefly to confirm the shared baseline opens. Stop Play Mode before editing/saving permanent changes.
5. Create your separate lab scene from the appropriate role guide. Keep the shared map scene with Student 1, and add your work through prefabs/scripts rather than rebuilding their scene.
6. **Student 3:** install Blender and open/create sources under ArtSource. You can skip Unity Hub/Editor entirely. Test FBX exports by reimporting in Blender and send the handoff files to Student 1 using the same repository.
7. Before your first change, create your agreed branch using `git switch -c "feature/student2-doors"`, for example. Student 3/4 choose their own descriptive branch name; do not all use the example branch.

If a full clone exceeds Student 3's storage/network budget, the team can arrange Git's sparse checkout for only Docs, ArtSource and her plain C# agent project. Plan that checkout deliberately so her source commits still reach the same repository. This is optional, not a required extra setup.

Daily workflow after the baseline and shared remote exist:

1. Use your own Git identity and install/configure Git LFS for the repository.
2. Fetch the team's default branch before work. Create your agreed feature branch, e.g. `feature/student2-doors`.
3. Develop one understandable piece, test it and save outside Play Mode.
4. Review `git status` and the diff. Stage only your intended scripts, assets, matching `.meta` files and evidence.
5. Commit with a specific message, e.g. `Add blocked-door close check` or `Unwrap original security robot`.
6. Push your branch and ask a teammate to review. Include what was tested and known limitations.
7. Integrator merges; everyone checks the main game again.

Student 3 does not need to open Unity to commit `ArtSource/Student3` and her FBX handoff files. You generate/commit Unity importer settings and `.meta` files during integration. Commit actual progress as it happens; do not fabricate a five-week commit history.

## 5. Shared agreements before parallel work

Hold a 30-minute team meeting and record:

- Names of all four members, graphics roles and IS agent ownership.
- The exact two custom models and their dimensions, origins and moving parts.
- Final camera interpretation for the IS brief.
- Agent size, motor type, path representation and animation parameters.
- Door states and events: `Closed`, `Opening`, `Open`, `Closing`; who can open which door.
- How a changed door/barricade tells every path planner to recheck its route.
- Shared input ownership: existing Arc controller moves the player; Student 2 adds interaction controls without a second movement controller.
- Scene owner and branch review procedure.

Coordinates: Unity **Y up**, **+Z deeper into the facility**, one unit = one metre. The floor is at Y=0. The actual handbuilt tiles use a 1 m grid; do not assume the old footprint JSON's `cellSize: 2` describes their tile spacing.

Door positions: west `(22, 0, 54)` and east `(63, 0, 46)`, rotation Y=90°. Use the existing guides under **06 / Security holding rooms**. Frame geometry is under **07 / Modular walls**.

## 6. Your Student 1 integration checklist

1. Back up the current scene before placing team prefabs. Keep existing preserved scenes.
2. Review Student 3's exports using her guide, import them, and give Student 4 one robot prefab with stable part names/pivots.
3. Place Student 2's tested door prefabs at the two anchors; test with our 2.444 m player.
4. Agree with Student 4 on a navigation agent type (example: robot height 2.2 m, radius about 0.65 m for the proposed broad robot), then create the initial NavMeshSurface. Match this to the actual collider and model clearance, not just its central body. Use **Physics Colliders** and an explicit environment-layer mask so decorative stone seams do not fragment navigation. Keep characters and transient effects out of the bake. [Unity NavMeshSurface](https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/manual/NavMeshSurface.html)
5. Bake traversable geometry with room entrances available. Treat moving door leaves and barricades as dynamic blockers. Also update the custom IS navigation graph when their traversability changes; a carved NavMesh alone cannot change a separate custom A* grid.
6. Verify paths from each room through the passage to the player. Check agent-specific clearance, not only the larger human capsule.
7. Prepare lightmap UVs for static assets, set appropriate static contributors, bake static lighting, and provide light probes for moving actors. Keep moving doors, props and agents out of static lightmaps. Keep one modest main shadow source and restrained effects for integrated graphics. These are starting design choices; measure performance on the weakest laptop.
8. After geometry/collider changes, rebuild navigation; after static lighting/UV changes, regenerate the affected lighting. Save generated data required by a fresh clone/build.
9. Run all four agents together with usable physics props and original models. Check a standalone build, not only the Editor.

Give Student 4 the bake settings, obstacle policy and the saved scene. Give Student 2 the door anchors, player dimensions and mission-trigger locations. Record which values are final.

## 7. Suggested calendar plan

These are our remaining work dates, not the PDF's numbered teaching weeks.

| Dates | Team target |
| --- | --- |
| 3–5 October | Publish reviewed baseline; agree contracts and IS ownership; each role builds a small first example |
| 6–10 October | Two original model exports/imports, working interaction lab, path follower with fake paths, first IS decisions |
| 11–15 October | Four agents and interactions integrated into the continuous map; handle doors, barricades and unreachable targets |
| 16–18 October | Final navigation/lighting, weakest-laptop profiling, standalone build and regression fixes |
| 19–20 October | Record the 3-minute GV video; verify source package/clone; practise individual viva |
| 21 October | Submit GV through the required course channel before its stated deadline/time |

Agree the actual submission cutoff time with the course. If behind, reduce cosmetic extras and boss complexity before removing required role evidence.

## 8. Demo and evidence

Suggested 3-minute GV recording: 0:00–0:30 map/material/light overview; 0:30–1:10 interaction physics; 1:10–1:45 two original models and brief Blender/UV evidence; 1:45–2:25 path-driven movement/turning/animations; 2:25–3:00 integrated gameplay and credits. Rehearse the timings; the written brief specifies the video length, not this running order.

Each person keeps: source files, clear commits, setup notes, a short individual demonstration, measured checks and a list of what they can explain live. Screenshots alone do not replace functioning source. All students should be able to navigate their own code or modelling steps during the viva.

Definition of team-ready: a clean clone opens, no missing references, original models are credited to their author, interactions work, all four autonomous agents work concurrently, navigation reacts to obstacles, animation matches movement, and an agreed hardware test/build is recorded.
