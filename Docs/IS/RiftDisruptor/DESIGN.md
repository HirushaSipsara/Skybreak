# Rift Disruptor — Student 4's intelligent agent

**Role:** a hit-and-run phase drone. It moves between tactical positions, decides when to approach and when to attack, telegraphs a pulse, then relocates.
**Hook:** it **reads the player's aim**. When your crosshair stays on it, it breaks off and repositions to a flank outside your view. It also **learns how often you dodge** its pulse and adapts.
**Architecture:** GOAP (A* over world states) on top of utility goal selection, threat-aware grid A*, weighted position search and a Beta–Bernoulli learner.

Try it: open `Assets/Scenes/Tests/RiftDisruptor/RiftDisruptorLab.unity`, press Play and click the Game view. **F8** shows the live reasoning overlay.

| Key | Action |
|---|---|
| WASD / mouse / Shift / Space | Move / look / sprint / jump |
| Left click | Fire (16 damage, 0.2 s) |
| R | Reset the round |
| F2 | Open/close the side-room door (live map change) |
| F3 | One or three Disruptors |
| F8 | AI overlay: goal scores, plan, facts, beliefs, tactics |

---

## 1. PEAS and environment

| | |
|---|---|
| **Performance** | Pulses that land, pulses not wasted (dodged or staggered), survival, time spent outside the player's crosshair |
| **Environment** | Aurora Vault rooms and corridors, cover, doors and barricades that change the map, other agents |
| **Actuators** | Follow a path array (through the shared `IPathFollower`), face a point, charge/release the pulse |
| **Sensors** | Sight (range 22 m, 160° field of view, wall-occluded ray), the player's aim ray, hit notifications (direction of the shooter) |

The environment is **partially observable**: walls block sight and the player can hide. It is **dynamic**: the player moves, and doors and barricades change the map. It is **continuous**, **sequential** and **multi-agent**: other Disruptors and the team's other agents share it. Because the world is partially observable, the agent keeps internal state. That state is a decaying memory of the player plus a learned model of the player's dodging, so it is a model-based, goal-based and utility-based agent combined.

## 2. Architecture

```
every 0.15 s                                       every frame
┌──────────────┐  facts   ┌──────────────┐ goal  ┌───────────────┐ plan ┌──────────────┐ path ┌───────────────┐
│ Perception   │────────▶│ Goal selector│──────▶│ GOAP planner  │─────▶│ Action runner│─────▶│ IPathFollower │
│ sight/memory │          │ (utility +   │       │ (A* over      │      │ preconditions│      │ (GV motor)    │
│ aim pressure │          │  hysteresis) │       │  fact states) │      │ replan rules │      └───────────────┘
└──────────────┘          └──────────────┘       └───────▲───────┘      └──────┬───────┘
        ▲                                                │ costs               │ pulse outcome
        │                                  ┌─────────────┴────────┐     ┌──────▼───────┐
        │                                  │ Tactics: anchors +   │     │ Dodge learner│
        └──────── hits ───────────────────│ threat-aware grid A* │     │ Beta(α, β)   │
                                           └──────────────────────┘     └──────────────┘
```

| Layer | File | Unity-free? |
|---|---|---|
| Facts, planner, domain | `Assets/Scripts/AI/Disruptor/Core/WorldFacts.cs`, `GoapPlanner.cs`, `DisruptorDomain.cs` | Yes |
| Grid A* and smoothing | `Core/GridSearch.cs` | Yes |
| Position scoring | `Core/AnchorScoring.cs` | Yes |
| Dodge learner | `Core/DodgeLearner.cs` | Yes |
| Goal arbitration | `Core/GoalSelector.cs` | Yes |
| Brain (cycle, actions, damage) | `Runtime/DisruptorBrain.cs` | No |
| Perception and memory | `Runtime/DisruptorPerception.cs` | No |
| Candidate positions and routes | `Runtime/DisruptorTactics.cs` | No |
| Navigation grid and door watching | `Runtime/DisruptorNavGrid.cs` | No |
| Pulse timing state | `Runtime/DisruptorPulse.cs` | No |
| Telegraph ring and placeholder body | `Runtime/DisruptorTelegraph.cs` | No (view only) |
| Debug overlay | `Runtime/DisruptorDebugOverlay.cs` | No (view only) |
| Shared path contract | `Assets/Scripts/Core/Interfaces/IPathFollower.cs` | No |

The `Core` folder is its own assembly (`Skybreak.Disruptor.Core`, `noEngineReferences: true`), so the decision logic cannot accidentally depend on visuals or physics. It is covered by 26 EditMode tests.

## 3. Why GOAP, and not an FSM or behaviour tree

The Disruptor's behaviour is a *sequence* whose shape depends on the situation:

- Unaware player, pulse ready: move to a flank, charge, release, then dash out.
- Already at a flank: charge, release, dash. No move is needed.
- No escape route reachable: move, charge, release. It strikes without the run.
- Player remembered but not visible: investigate first.

An FSM needs a hand-written transition for every one of these orderings, and each new action multiplies the transitions. A behaviour tree fixes the order of its children at design time. GOAP describes each action once, as preconditions and effects, and the planner *derives* the sequence. The "run" after the hit is not scripted anywhere. It emerges because `ReleasePulse` has the effect `Concealed = false` (the flash reveals it) and the Disrupt goal requires `Concealed = true`. So the cheapest plan has to end with a relocation.

Costs are **not constants**. They are rewritten before every plan from measured path lengths, exposure to the player's aim and the dodge belief. That is how the same action set switches between a direct approach and a flank (§6).

## 4. GOAP planner (A* over world states)

- **State:** a 32-bit set of 10 facts (`PlayerKnown`, `PlayerVisible`, `AtStrikePosition`, `PulseReady`, `PulseCharged`, `PlayerDisrupted`, `Concealed`, `AreaSwept`, `StrikeAnchorAvailable`, `EscapeAnchorAvailable`).
- **Edge:** an action whose preconditions hold. Its effects overwrite the masked bits.
- **Search:** forward A* with a closed set and a budget of 256 expansions. The heap breaks ties towards lower h.
- **Heuristic:** `h(s) = ceil(u(s) / k) × c_min`
  - `u(s)` is the number of goal facts not yet satisfied.
  - `k` is the most goal facts any single enabled action can set.
  - `c_min` is the cheapest enabled action. Every action costs at least 0.5 (`StepOverhead`).
  - **Admissible:** each action fixes at most `k` facts and costs at least `c_min`.
  - **Consistent:** h drops by at most `c_min` per step, so a closed state never needs reopening.
  - Why not plain `u(s)`? `ReleasePulse` sets two facts at once, so counting facts would overestimate and lose optimality.
- **Goals:**
  - Disrupt = `{PlayerDisrupted, Concealed}`, falling back to `{PlayerDisrupted}` when no escape exists
  - Evade = `{Concealed}`
  - Hunt = `{PlayerVisible}`
  - Guard = `{AreaSwept}`

| Action | Preconditions | Effects | Cost |
|---|---|---|---|
| Move to strike anchor | Known, StrikeAnchor | AtStrike, Visible, ¬Concealed | 0.5 + path/3.4 + 2·exposure |
| Close in directly | Visible | AtStrike, ¬Concealed | 0.5 + dist/3.4 + 1.5·(1 + P(dodge)) |
| Charge pulse | AtStrike, PulseReady, Visible | Charged | 0.5 + charge time + 2.5·P(dodge) |
| Release pulse | Charged | Disrupted, ¬Charged, ¬Ready, ¬Concealed | 0.5 |
| Phase-dash | EscapeAnchor | Concealed, ¬AtStrike, ¬Visible | 0.5 + path/8 |
| Investigate | Known, ¬Visible | Visible | 0.5 + path/3.4 |
| Patrol | ¬Visible | AreaSwept | 0.5 + path/2.2 |

Costs are seconds of effort plus weighted risk, so they share one unit and can be added.

## 5. Goal selection (utility with hysteresis)

Every goal gets an insistence in [0, 1]. Goals are tried best-first, and the first one the planner can achieve wins.

- **Disrupt** = `0.9 × (1 − 0.6·aimPressure)` while the player is visible. If the player is only remembered, it is `0.25·memory`, so Hunt (find them first) outranks it. It applies only with a ready pulse and a living, known player.
- **Evade** = `max(aimPressure·(0.75 + 0.25·damage), recharging)`. Here `recharging = 0.45 + 0.2·cooldown` while the player is visible and the pulse is not ready. This is the "run" between strikes.
- **Hunt** = `0.3 + 0.4·memory` once sight is lost.
- **Guard** = 0.2, a constant floor so there is always something to do.

Disrupt and Evade cross where `0.9 − 0.54p = 0.75p`, at an aim pressure of p ≈ 0.7. That means roughly 0.4 s of steady aiming makes it break off. The current goal gets +0.1 (**hysteresis**) so near-ties do not flip the plan every 0.15 s.

## 6. Tactical position search and threat-aware A*

**Candidates:**
- hand-placed `DisruptorAnchor`s
- 24 strike samples on two rings around the player (strike distance, ×0.75)
- 24 escape samples on rings around itself (7 m) and the player (11 m)

**Two-stage search** bounds the cost:
1. All candidates are scored with a straight-line travel estimate.
2. Only the best 4 get a real A* route and are re-scored with its length.

**Strike score** = 0.30·range + 0.35·flank + 0.20·travel + 0.15·novelty.
- Flank = aim angle / 70°, capped at 1. Flank is the heaviest weight because attacking from where the player is not looking is the agent's identity.
- Novelty penalises a spot used within 8 s, so it is not predictable.
- Hard constraints return −∞: no line to the player, out of range, or claimed by another Disruptor.

**Escape score** = 0.45·broken line of sight + 0.20·flank + 0.15·distance + 0.20·travel.

**Grid A\*** (`GridSearch.FindPath`):
- 8-connected, 0.5 m cells, and **no corner cutting**: a diagonal step needs both orthogonal neighbours free.
- **Cell walkability** is a capsule overlap of the agent's own size, so obstacles are inflated by its radius.
- **Step cost** = length × (1 + danger).
- **Danger** = 3 × (1 − angle/30°) inside the player's 30° aim cone. Routes therefore curve around the crosshair instead of walking down it.
- **Heuristic** is octile distance: exact for an empty grid with zero danger, so it is admissible and consistent.
- **Unreachable goal:** it returns `Partial`, the route to the nearest reachable cell. The brain treats that as "anchor unreachable" and never as arrival.

**Smoothing** (string pulling) removes cells only when the straight segment stays on walkable cells and cuts no blocked corner. The segment must also be no more dangerous than the A* route it replaces, so smoothing cannot undo the detour around the aim cone.

**Worked example** (unit test `DodgingPlayerMakesTheFlankCheaperThanAFrontalApproach`). Compare a flank route of 10 m with a straight approach of 4 m:

| | Flank (move to strike anchor) | Close in directly |
|---|---|---|
| P(dodge) = 0 | 0.5 + 10/3.4 = **3.44** | 0.5 + 4/3.4 + 1.5 = **3.18** → wins |
| P(dodge) = 1 | **3.44** → wins | 0.5 + 4/3.4 + 3.0 = **4.68** |

Against a player who never dodges, the shorter frontal approach wins. Against a player who dodges, the flank wins even though it is 2.5 times longer.

## 7. Aim awareness and memory

- **Aim:** the angle between the player's camera ray and the direction to the Disruptor, checked every 0.15 s.
  - Inside 12° with a clear line, *aim pressure* fills over 0.6 s. Outside, it drains over 1.2 s.
  - A hit adds 0.35 and reveals the shooter's position, so sniping from hiding is not free.
- **Memory:** after sight is lost, confidence is `exp(−Δt / 2.5 s)`. The player is "known" while it is above 0.05, about 7.5 s.
  - Investigating the last known position and finding nothing clears the memory, so it does not circle forever.

## 8. Learning the player (Beta–Bernoulli with forgetting)

`p = P(player escapes the ring before release)`. The belief is a Beta distribution.

- **Prior:** Beta(2, 2), mean 0.5, worth only four pulses of evidence.
- **Update:** a dodge adds 1 to α and a hit adds 1 to β. This is the conjugate update.
- **Forgetting:** before each update both counts decay towards the prior by γ = 0.9. Effective memory is about 1/(1−γ) = 10 pulses, so the belief recovers when the player changes style. In the unit test, 15 misses after 30 dodges bring the mean from 0.86 down to 0.29.
- **Learning sample:** only pulses that started with the player inside the ring count. The player had to react, so the outcome is evidence about dodging.

The posterior mean adapts two things:

| | P(dodge) = 0 | P(dodge) = 1 |
|---|---|---|
| Telegraph time | 1.1 s | 0.7 s (fairness floor) |
| Strike distance | 0.85 × radius | 0.6 × radius |

It also feeds the planner costs, which make flanking more attractive (§6). The 0.7 s floor keeps the attack readable: human reaction time (~0.25 s) plus a sprint step still escapes.

## 9. Pulse and counterplay

- **Charge:** it stops, faces the player and grows a ground ring to the 4.6 m radius.
- **Release:** it damages once (20) if the player is inside the ring and not behind cover.
  - Each pulse has an id, and `Release` refuses an id already resolved, so damage can never apply twice.
- **Stagger:** 30 damage taken while charging cancels the pulse with half the cooldown. This rewards shooting it during the telegraph.
- **Commitment:** once charging starts, goal changes cannot abort it. This prevents dithering. Only a stagger, its death or the player's death can end it.

## 10. Edge cases handled

| Situation | Behaviour |
|---|---|
| No path, or only a partial path to an anchor | Anchor blacklisted for 5 s; the agent replans with another candidate |
| Motor reports Blocked or Invalid | Action fails, destination blacklisted, replan |
| Door, gate or barricade changes | Watched colliders rescan only their cells. The grid version increments, the current leg is re-routed, or the action fails if the route is now closed. Student 2's `SlidingSecurityDoor`, `DungeonHoldingDoor`, `DescentGate` and `MovableBarricade` are watched automatically |
| Precondition broken mid-plan (player moved, hid) | Checked before each step against live facts; replan |
| Nothing achievable | Next goal down the ranking; Guard always exists |
| Replan storm in a broken world | Replans are throttled to one per 0.25 s |
| Several Disruptors | Destinations claimed; candidates within 2.2 m of another agent's destination are rejected |
| Player dead or missing | Disrupt, Evade and Hunt score 0, so it guards/patrols. A charge in progress is cancelled |
| Disruptor dies | Pulse cancelled, motor stopped, no further actions |
| Player without a `DamageSink` (main map today) | Falls back to `IDamageable`. The agent also implements both `DamageSink` and `IDamageable`, so the team's two weapon styles both hit it |

## 11. Evidence

- **Unit tests:** `Assets/Tests/EditMode/DisruptorCore/`, 26 tests covering the planner, grid search, scoring, learner and goal selection.
  - Run them: Window → General → Test Runner → EditMode → `Skybreak.Disruptor.Tests`.
- **Gameplay checks:** `Assets/Editor/RiftDisruptor/DisruptorGameplayChecks.cs` runs 9 scripted Play Mode scenarios in the real lab. It runs from the menu **Skybreak → Rift Disruptor → Run gameplay checks** and writes `Docs/IS/RiftDisruptor/GameplayChecks.txt`.
- **Latest run, 21 October 2026, Unity 6000.6.0f1 batch mode:** 26/26 unit tests and 9/9 gameplay scenarios passed.
  - Hit-and-run: the pulse hit for 20 damage, then the Disruptor relocated 15.7 m.
  - Stagger: 2 shots during the telegraph cancelled the pulse.
  - Learning: 4 dodges moved P(dodge) from 0.50 to 0.73 and the telegraph from 0.90 s to 0.81 s.
  - Three Disruptors: 5 pulses and 0 clashes over claimed destinations.

### Bugs the gameplay checks found (and the fixes)

| Symptom | Cause | Fix |
|---|---|---|
| The agent zig-zagged and never reached a strike spot | The novelty penalty was applied when a spot was *chosen*, so the next re-evaluation preferred a different one | Mark a spot used only when striking from it, and keep the current route when a replan picks the same spot |
| The dash after a pulse was cancelled | A periodic replan saw the scripted player facing away ("concealed") | The relocation after a pulse is committed (up to 3 s) |
| Two agents dashed to one spot | The escape point was chosen 1–2 s before the dash claimed it | The claim is re-checked when the move starts |
| Movement crawled at high frame rates | `CharacterController.minMoveDistance` (default 1 mm) drops per-frame steps when speed × dt < 1 mm | The follower sets `minMoveDistance = 0` |

## 12. Integration notes

- Add a `DisruptorNavGrid` to the scene, covering the play area at 0.5 m cells. Optionally place `DisruptorAnchor`s at corners and pillars.
- The Disruptor root needs:
  - a `CharacterController` (radius 0.5, height 1.8, centre y 0.9) on layer 8
  - an `IPathFollower`. `BasicPathFollower` is added automatically if none is present; the GV branch supplies the smooth motor.
  - `DisruptorBrain`
  - optionally `DisruptorTelegraph`
- The brain finds the player through, in order:
  1. a player-team `DamageSink`
  2. `DescentCharacterController`
  3. `ArcController`
  4. the `Player` tag
- The main scene is Student 1's. Placement there is a hand-off, not done here.

## 13. Viva quick answers

- **Why A\* for the planner?** Plans must be cheapest under situation-dependent costs. Uniform-cost search would also be optimal but expands more states, and greedy search is not optimal.
- **Why that heuristic?** See §4. Counting unsatisfied facts alone is *not* admissible, because one action can set two goal facts.
- **Why octile and not Manhattan?** Movement is 8-connected. Manhattan overestimates diagonal moves, so it is not admissible here.
- **Where is the plan made?** `DisruptorBrain.Replan` → `GoapPlanner.Plan`.
- **Where is it executed?** `DisruptorBrain.TickAction`, `StartAction`, `RunAction`.
- **Where are the costs?** `DisruptorDomain.ApplyCosts`.
- **What stops oscillation?** Goal hysteresis (+0.1), commitment while charging, the replan throttle, and the 5 s blacklist.
- **What makes it more than if/else?** The action order is searched, not written. Costs come from measurements and a learned belief, and goals compete numerically.
