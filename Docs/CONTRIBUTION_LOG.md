# Skybreak contribution log

Each member records their work here, including what was AI-assisted or downloaded, so assessed contributions are reported accurately. Add new entries at the top of your own section. Reference commits by hash.

**Columns:** *Work* is what was produced. *How* is who or what produced it (student, AI assistance, downloaded asset). *Evidence* points to the commit, document or test results.

---

## Student 1 — Hirusha Sipsara

Assessed IS agent: **Relay** (support companion). Additional work: Rift Warden boss.

### Relay intelligent-agent implementation (IS assessment)

AI assistance: Claude (Anthropic), through Claude Code. Design, code, tests, evaluation and documentation were produced by the AI from the SE3062 lecture material and the student's requirements. The student set the goals, chose between options, reviewed each step and committed it, or asked for it to be committed under their account. AI assistance is recorded here rather than in the commit messages.

| Date | Work | How | Evidence |
|---|---|---|---|
| 2026-10-19 | Step 1: Relay design document (PEAS, 7 environment dimensions, layered architecture, lecture map, test plan) | AI-drafted from lectures 02–11; reviewed and committed by the student | `70e5f41`, `Docs/IS/AgentDesign/Relay/RELAY_DESIGN.md` |
| 2026-10-19 | Step 2: `RelayBrain` split into reflex, perception and utility layers, with no behaviour change | AI-written; committed by the student | `9d21aff`, `Docs/IS/Testing/Relay/step2_*.txt` (identical before/after snapshots) |
| 2026-10-19 | Step 3: Mamdani fuzzy inference library, Relay's 14-rule fuzzy model, expected-utility selection, 6 unit tests from Lecture 09 examples | AI-written; committed at the student's request | `1965872`, `Docs/IS/Testing/Relay/step3_*` |
| 2026-10-19 | Step 4: best-first search engine (A*, uniform-cost, greedy), tactical NavMesh grid with exposure costs, threat tracks, 9 unit tests (Lecture 05 Romania) | AI-written; committed at the student's request | `c300802`, `Docs/IS/Testing/Relay/step4_*` |
| 2026-10-19 | Step 5: simulated annealing and hill climbing, position objective, Reposition action, 5 unit tests | AI-written; committed at the student's request | `2a2f76b`, `Docs/IS/Testing/Relay/step5_*` |
| 2026-10-19 | Step 6: F9 debug overlay, 8-seed baseline-vs-layered evaluation, interactive study guide | AI-written; committed at the student's request | `efb8683`, `Docs/IS/Testing/Relay/EVALUATION.md`, `Docs/IS/AgentDesign/Relay/relay_study_guide.html` |
| 2026-10-19 | Local lab test tools: `RelayRegressionCheck.cs`, `RelayEvaluation.cs` | AI-written; kept in the git-excluded `Assets/LocalCombatLab` folder because they depend on the local test lab | Their output files are committed under `Docs/IS/Testing/Relay/` |

### Rift Warden adaptive boss AI (additional agent)

AI assistance: Claude (Anthropic), through Claude Code, at the student's request: "improve the boss's reading of the fight, with IS techniques and beyond". The student set the goal, approved the plan and asked for each step to be committed. AI assistance is recorded here rather than in the commit messages.

| Date | Work | How | Evidence |
|---|---|---|---|
| 2026-10-20 | Step 1: adaptive boss design; brain split into perception and attack scoring with no behaviour change | AI-written | `24524b6`, `Docs/IS/AgentDesign/RiftWarden/RIFT_WARDEN_AI_DESIGN.md`, `Docs/IS/Testing/RiftWarden/step1_*` |
| 2026-10-20 | Step 2: player model (velocity, range, strafe habit, Dirichlet answer beliefs) and predictive aim | AI-written | `5b7ee38`, `step2_*` |
| 2026-10-20 | Step 3: Bayesian Beta attack learner with Thompson sampling | AI-written | `70deb29`, `step3_*` |
| 2026-10-20 | Step 4: depth-2 expectimax combo planner | AI-written | `e89f56a`, `step4_*` |
| 2026-10-20 | Step 5: phases, tactics (shield weak arm, punish healer, cut off), fairness director | AI-written | `4a5b90f`, `step5_*` |
| 2026-10-20 | Step 6: F10 overlay; original vs adaptive evaluation against 4 scripted player styles | AI-written | `97079ec`, `Docs/IS/Testing/RiftWarden/EVALUATION.md` |
| 2026-10-20 | Step 7: boss section and Thompson-sampling explorer in the study guide | AI-written | this commit, `Docs/IS/AgentDesign/Relay/relay_study_guide.html` |
| 2026-10-20 | Standalone Rift Warden guide page: fight sheet, AI layers (IS and beyond), interactive explainers, instructions | AI-written | `Docs/IS/AgentDesign/RiftWarden/rift_warden_guide.html` |
| 2026-10-20 | Local lab tools `RiftRegressionCheck.cs` and `RiftEvaluation.cs` | AI-written; kept in the git-excluded `Assets/LocalCombatLab` | Outputs under `Docs/IS/Testing/RiftWarden/` |

### Other AI-assisted work (not the assessed agent)

| Date | Work | How | Evidence |
|---|---|---|---|
| 2026-10-19 | Rift Warden v2 model upgrade, imported with URP materials; prefab and both scenes switched to v2 | AI-assisted in Blender and Unity, upgrading the existing handoff model | `0c4da35`, `Deliverables/RiftWarden_Upgraded/UPGRADE_NOTES.md` (outside the repo) |
| 2026-10-19 | Relay Blender model and pod animation (`RelayPodAnimator`) | AI-generated by script (`relay_build.py`) | `0c4da35` |
| 2026-10-19 | Boss presentation (arena mood, hit impact, attack effects, intro cutscene, fight audio) and a new player shot sound | AI-written; synthesised audio | `0c4da35` |
| 2026-10-19 | Map atmosphere pass: fog, per-zone moods with bloom, ambient particles | AI-written builder (`MapAtmosphereBuilder.cs`) | `0c4da35` |
| 2026-10-19 | Arc player-model experiments (procedural model, Canva concept art, Hunyuan3D-generated model) | AI-generated; **removed from the project at the student's request**, the original Arc is kept | Archived outside the repo in `Deliverables/Arc_Model/` |
| 2026-10-04 | Rift Warden model, rig and animation handoff | AI-assisted (see the handoff's own contribution note) | `9f7ae49`, `Docs/IS/AgentDesign/RiftWarden/HANDOFF.md` |
| earlier | First Relay utility brain and Rift Warden brain | AI-assisted (see the document) | `Docs/IS/AgentDesign/OWNED_AGENT_IMPLEMENTATION.md` |

### Student 1's own contributions

*To be completed by the student:* requirements and decisions you made, testing you did in Play mode, tuning, and anything you wrote or changed yourself.

-

---

## Student 2

-

## Student 3

-

## Student 4 — Samith Bandara

Assessed IS agent: **Rift Disruptor** (hit-and-run phase drone). Design: `Docs/IS/RiftDisruptor/DESIGN.md`.

### Rift Disruptor intelligent-agent implementation (IS assessment)

AI assistance: Claude (Anthropic), through Claude Code. The agent concept (Rift Disruptor: moves between positions, chooses when to approach or attack, telegraphs a pulse, then relocates) came from the team proposal. The AI chose the architecture after reading the existing agents, and wrote the code, tests and documentation. The student set the goal, reviewed the work and committed it. AI assistance is recorded here rather than in the commit messages.

| Date | Work | How | Evidence |
|---|---|---|---|
| 2026-10-21 | Unity-free core: fact states, GOAP planner (A* over world states, admissible heuristic), Disruptor action domain and cost model | AI-written | `Assets/Scripts/AI/Disruptor/Core/` |
| 2026-10-21 | Threat-aware grid A* (octile, no corner cutting, partial routes) and danger-preserving path smoothing | AI-written | `Core/GridSearch.cs` |
| 2026-10-21 | Position scoring, Beta–Bernoulli dodge learner with forgetting, utility goal selection with hysteresis | AI-written | `Core/AnchorScoring.cs`, `DodgeLearner.cs`, `GoalSelector.cs` |
| 2026-10-21 | Runtime brain, aim-aware perception, tactics, nav grid with door/barricade watching, pulse, telegraph, F8 overlay | AI-written | `Assets/Scripts/AI/Disruptor/Runtime/` |
| 2026-10-21 | Shared `IPathFollower`/`PathResult` contract and a basic follower | AI-written | `Assets/Scripts/Core/Interfaces/IPathFollower.cs`, `Assets/Scripts/AgentMovement/BasicPathFollower.cs` |
| 2026-10-21 | Self-building lab, 26 EditMode tests, 9 scripted Play Mode gameplay checks | AI-written; run in Unity batch mode | `Assets/Scenes/Tests/RiftDisruptor/`, `Assets/Tests/EditMode/DisruptorCore/`, `Docs/IS/RiftDisruptor/GameplayChecks.txt` |

### Agent movement, rotation and animation (GV assessment)

AI assistance: Claude (Anthropic), through Claude Code. The student supplied the visual reference for the mech and the Blender install, set the goal, and reviewed the work. The AI wrote the Blender build script, the Unity editor builders, the runtime motor and presenters, the checks and the documentation. AI assistance is recorded here rather than in the commit messages.

| Date | Work | How | Evidence |
|---|---|---|---|
| 2026-10-21 | Rift Disruptor mech modelled from the student's reference: rigid parts on 20 joint pivots, merged to 18 renderers, 7.5k triangles; .blend, FBX, preview renders | AI-written Blender script | `ArtSource/Student4/RiftDisruptor/` |
| 2026-10-21 | Unity import settings, URP materials, generated wear texture, seven in-place transform clips, Animator Controller, prefab | AI-written editor builder | `Assets/Editor/RiftDisruptor/RiftDisruptorAssetBuilder.cs`, `Assets/Animations/Disruptor/`, `Assets/Prefabs/Enemies/RiftDisruptor/` |
| 2026-10-21 | `SmoothPathFollower` (movement budget, braking and corner speed, capped yaw, validation, blocked detection) and shared `AgentAnimationPresenter` | AI-written | `Assets/Scripts/AgentMovement/` |
| 2026-10-21 | Disruptor presentation glue (events to Animator, glow), mech in the IS lab, `AgentMovementLab` scene | AI-written | `DisruptorVisuals.cs`, `Assets/Scenes/Tests/Student4Movement/` |
| 2026-10-21 | 14 movement/animation checks, URP pose renders, GV design and optimisation document | AI-written; run in Unity batch mode | `Docs/GV/AgentMovement/` |

### Student 4's own contributions

| Date | Contribution |
|---|---|
| 2026-10 | Proposed the Rift Disruptor concept in the team pitch: moves between positions, chooses when to approach or attack, telegraphs a pulse, then relocates. Chosen to exercise the Student 4 movement and animation system |
| 2026-10-21 | Supplied the IS and GV briefs and the team's agent proposals; set the requirement that the agent fit the existing game alongside the other members' agents |
| 2026-10-21 | Set the work order: IS agent first on `module/is-samith`, then GV movement and animation on `module/gv-samith` |
| 2026-10-21 | Reviewed the implementation, design document and test evidence; chose not to let the AI commit, and committed the work in eight logical commits under own account |
| 2026-10-21 | Chose the visual design for the mech (reference image: teal and cream armour, arm cannon, claw, orange vents); installed Blender 5.2 for the model build; merged the IS branch into `module/gv-samith` before the GV work |

*Still to add:* own play testing in the lab (what you tried, what you observed) and any tuning or code changes you make yourself.
