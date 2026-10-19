# Relay — Autonomous Support Companion: Agent Design

**Module:** SE3062 Intelligent Systems — Autonomous Agents in 3D Environments
**Agent owner:** Student 1 (Relay). The Rift Warden boss is an additional, separately documented agent.
**Status:** design baseline (step 1 of the build plan, section 9). Sections marked *Current* describe the code as it is today; everything else is the target design.

Relay is a small hovering support robot that accompanies the player (Arc) through the Skybreak dungeon and the Rift Warden fight. It keeps the player alive, protects them, and stays out of their way, without being controlled by the player.

---

## 1. Why Relay needs more than a basic search

The brief requires each agent to use "a decision-making architecture that goes beyond a basic uninformed search". Relay meets this with a **hybrid (subsumption) architecture** (Lecture 03, slide 62): five layers, each a named course technique, stacked so that fast reflexes override slower reasoning.

| Layer | Purpose | Technique | Lecture |
|---|---|---|---|
| 0 Reflex | Survive immediate danger | Prioritised condition-action rules | L03 Simple Reflex, subsumption |
| 1 World model | Remember what it cannot currently see | Internal state + transition model | L03 Model-Based, L02 partial observability |
| 2 Assessment | Judge how urgent each need is | Fuzzy Inference System (Mamdani, COG) | L09 Parts 3–4 |
| 3 Decision | Choose the best action | Expected-utility selection | L03 Utility-Based |
| 4 Planning | Reach a position safely | Tactical A* with an admissible heuristic | L05 Informed Search |
| 5 Positioning | Find the best place to stand | Simulated annealing (local search) | L05 Local Search |

*Current* (`Assets/Scripts/AI/Relay/RelayBrain.cs`): a single utility selector with hand-tuned linear scores for Follow / Heal / Defend / Retreat, 3 s of threat memory, and Unity's NavMesh for movement. This is the baseline that the evaluation in section 8 compares against.

---

## 2. Task environment — PEAS (Lecture 02)

| | Relay |
|---|---|
| **Performance measure** | Player survival time and health kept above 30%; heals applied while the player is genuinely in need; damage dealt to visible threats; damage Relay itself takes (lower is better); time spent blocking the player's line of fire (lower is better); never stuck or idle when needed. |
| **Environment** | The Skybreak dungeon (entrance court, winding passage, security wing, final chamber) and the Rift Warden arena: walls, doors, crates, pillars, a baked NavMesh, the player Arc, the Rift Warden and its damageable parts, boss hazards (slam landing zone, cannon line, blade sweep). |
| **Actuators** | Hover movement (`IAgentMotor.MoveTo` / `Stop`); heal beam (`DamageSink.RestoreHealth` on the player); defensive shot (`DamageSink.ReceiveDamage` on a hostile); facing/turning of its visual; beam and audio cues (`CombatSignals`, `CombatAudio`). |
| **Sensors** | Line-of-sight checks to every active `DamageSink` (`DamageSink.Visible`); distances; the player's health and position; Relay's own health; the boss's current `RiftAction` and which parts are vulnerable; NavMesh queries (reachability, `NavMesh.Raycast`); hazard zones announced by the encounter. |

### Percepts and actions

- **Percept** (sampled every `senseInterval` = 0.2 s): `{ playerPos, playerVel, playerHealth01, relayPos, relayHealth01, visibleThreats[{id, pos, vel, danger}], bossAction, hazards[{centre, radius, timeToImpact}], healReady, attackReady }`.
- **Actions:** `Follow`, `Heal`, `Defend`, `Retreat`, `Reposition`, `Evade` (new; reflex layer only), `Disabled`.

---

## 3. Environment dimensions (Lecture 02) and their design consequences

| Dimension | Relay's world | Consequence for the design |
|---|---|---|
| Fully vs partially observable | **Partially observable.** Walls hide enemies, and the boss teleports out of sight. | A simple reflex agent is not enough, so Relay needs a world model with memory (layer 1). |
| Single vs multi-agent | **Multi-agent**: cooperative with the player, competitive with the boss. | Positions are judged relative to both (stay near Arc, stay hidden from the boss); the player's line of fire is a constraint. |
| Deterministic vs stochastic | **Stochastic** from Relay's view: the player's input is unknown, and the boss picks attacks it cannot see. | Decisions use degrees of belief (fuzzy sets), and actions are scored by *expected* utility. |
| Episodic vs sequential | **Sequential.** Healing now puts heal on cooldown; position now decides exposure later. | Cooldowns and memory are part of the state; positions are planned (A*), not reacted to. |
| Static vs dynamic | **Dynamic.** The world moves while Relay thinks. | Real-time budget: a decision cycle every 0.2 s, an A* node limit, a fixed annealing iteration count, and replanning when the goal moves. |
| Discrete vs continuous | **Continuous** positions and health. | Continuous inputs are fuzzified (layer 2), and space is discretised into a waypoint graph for A* (layer 4); annealing searches continuous positions directly (layer 5). |
| Known vs unknown | **Known** physics and rules; the map layout is known through the NavMesh. | No learning is needed for correctness; an optional learning critic is a stretch goal (section 9). |

---

## 4. Architecture

```mermaid
flowchart TD
    S[Sensors<br/>line of sight, health, positions, boss action, hazards] --> M[Layer 1 World model<br/>threat memory + transition model]
    M --> R{Layer 0 Reflex rules<br/>any rule fires?}
    R -- yes --> A[Actuators]
    R -- no --> F[Layer 2 Fuzzy assessment<br/>HealUrgency, DefendUrgency, DangerToRelay]
    F --> U[Layer 3 Expected utility<br/>argmax EU over actions]
    U --> P5[Layer 5 Simulated annealing<br/>best support position]
    P5 --> P4[Layer 4 Tactical A*<br/>safest path to that position]
    U -- heal / shoot in range --> A
    P4 --> A
```

### 4.1 Agent program (one decision cycle)

```
function RELAY-AGENT(percept) returns action
    persistent: model (layer 1), plan (current A* path), lastAction
    model ← UPDATE-MODEL(model, percept)                       // layer 1
    rule ← FIRST-MATCHING-REFLEX(model)                         // layer 0, by priority
    if rule ≠ none then return rule.action
    urgency ← FUZZY-ASSESS(model)                               // layer 2
    action ← argmax over a in Actions of EU(a | model, urgency) // layer 3
    if action needs a position then
        goal ← ANNEAL-POSITION(action, model)                   // layer 5
        if plan is empty or goal moved > 1.5 m or plan exposed then
            plan ← TACTICAL-A*(relayPos, goal, model)           // layer 4
    return EXECUTE(action, plan)
```

### 4.2 Layer 0 — Reflex safety layer (Lecture 03)

These are condition-action rules checked in priority order (the "drone obstacle" activity's question of rule priority). The first one that matches wins and skips all deliberation that cycle: subsumption.

| Priority | Condition | Action |
|---|---|---|
| 1 | Relay health = 0 | `Disabled`: stop and clear signals |
| 2 | Relay is inside a hazard zone with time-to-impact < 1.2 s | `Evade`: move to the nearest NavMesh point outside the zone (radius + 1 m), away from its centre |
| 3 | Relay is within 0.6 m of the player's aim ray, between Arc and the aim point | `Evade` sideways 1.5 m (do not block the player's shots) |
| 4 | Player is down | Stop and wait (existing behaviour) |

Why reflexes: they are cheap and immediate, which matters in a dynamic world. Lecture 03's self-driving example uses the same split: a utility engine plans the route, and a reflex layer works the brakes.

### 4.3 Layer 1 — World model (Lecture 03, model-based)

| State variable | Updated from | Transition model (when unseen) |
|---|---|---|
| Threat track `{lastPos, velocity, lastSeen}` per hostile | Line-of-sight sightings | Constant velocity: `predicted = lastPos + velocity·Δt`, capped at 4 m |
| Threat confidence `c` | Set to 1 on sighting | Decays `c = e^(−Δt/τ)`, τ = 1.5 s; the track is forgotten when c < 0.1 |
| Player health trend | Health samples | Rate of health loss over the last 2 s (smoothed) |
| Hazards `{centre, radius, timeToImpact}` | Encounter warnings | Removed after impact |
| Cooldowns, own health | Own actions / damage | Count down in real time |

*Current:* only one remembered position with a fixed 3 s memory. The new model keeps per-threat tracks, predicts movement, and gives a confidence that later layers use as a probability.

### 4.4 Layer 2 — Fuzzy assessment (Lecture 09, Fuzzy Inference System)

A Mamdani FIS following the lecture pipeline: **fuzzify, apply rules (AND = min, OR = max), aggregate (max), defuzzify by centre of gravity (COG)**.

**Inputs and membership functions** (trapezoid `[a,b,c,d]`, triangle `[a,b,c]`):

| Linguistic variable | Universe | Terms |
|---|---|---|
| `PlayerHealth` | 0–1 | Critical `[0,0,.15,.3]`, Low `[.15,.35,.55]`, Healthy `[.45,.7,1,1]` |
| `ThreatDistance` (to the player) | 0–20 m | Near `[0,0,4,8]`, Medium `[5,10,15]`, Far `[12,16,20,20]` |
| `HealthTrend` (player loss per second) | 0–.2 | Stable `[0,0,.01,.03]`, Dropping `[.02,.05,.08]`, Plummeting `[.06,.1,.2,.2]` |
| `RelayHealth` | 0–1 | Weak `[0,0,.2,.4]`, Okay `[.3,.6,1,1]` |

**Outputs** (each 0–1, terms Low `[0,0,.2,.4]`, Medium `[.3,.5,.7]`, High `[.6,.8,1,1]`): `HealUrgency`, `DefendUrgency`, `SelfPreservation`.

**Rule base (initial):**

1. IF PlayerHealth is Critical THEN HealUrgency is High
2. IF PlayerHealth is Low AND HealthTrend is Dropping THEN HealUrgency is High
3. IF PlayerHealth is Low AND HealthTrend is Stable THEN HealUrgency is Medium
4. IF PlayerHealth is Healthy THEN HealUrgency is Low
5. IF ThreatDistance is Near AND PlayerHealth is NOT Critical THEN DefendUrgency is High
6. IF ThreatDistance is Medium THEN DefendUrgency is Medium
7. IF ThreatDistance is Far THEN DefendUrgency is Low
8. IF HealthTrend is Plummeting THEN DefendUrgency is High
9. IF RelayHealth is Weak AND ThreatDistance is Near THEN SelfPreservation is High
10. IF RelayHealth is Okay THEN SelfPreservation is Low

Why fuzzy: "low health" and "near" are vague concepts with no correct crisp threshold. The current code's single `healThreshold = 0.25` is exactly the crisp-logic failure the lecture describes; fuzzy rules degrade smoothly instead.

### 4.5 Layer 3 — Expected-utility selection (Lecture 03)

`EU(a) = P(success | a, state) × U(a)`. Relay picks `argmax EU`, with a small hysteresis bonus (+0.05) for keeping the current action so it does not flicker between two choices.

| Action | U(a) | P(success \| a, state) |
|---|---|---|
| Heal | `HealUrgency` | 1 if in heal range with line of sight and the heal is ready; otherwise 0 when on cooldown, and `0.7 × reachable` when it would first need to move |
| Defend | `DefendUrgency × threatConfidence` | Line of sight to a vulnerable target and attack ready; scaled down by the share of the target's parts that are shielded |
| Retreat | `SelfPreservation` | Reachable safe point found (layer 5) |
| Follow | `0.2 + 0.4 × clamp(distanceToPlayer / 9)` | Path to the player exists |
| Reposition | `0.3 × exposure` of the current spot | A better spot exists (layer 5 improvement > 0.1) |

Outcomes are uncertain because the boss and player act unpredictably, and threats come from a memory with confidence below 1. That is why utilities are weighted by probability instead of used directly: this is the lecture's expected-utility formulation.

### 4.6 Layer 4 — Tactical A* (Lecture 05, Informed Search)

Relay finds routes with **its own A*** instead of handing the destination straight to Unity's NavMesh, so that "safe" can be part of the path cost.

- **Graph:** a 1 m grid of nodes sampled on the NavMesh (`NavMesh.SamplePosition`) within 20 m of Relay. There are edges to the 8 neighbours where `NavMesh.Raycast` is clear, built lazily and cached per area.
- **Step cost:** `c(n, n') = |n − n'| + λ · exposure(n')`, where `exposure(n') ∈ [0,1]` is the summed confidence of remembered threats with line of sight to `n'` (capped at 1), and λ = 3 by default.
- **Heuristic:** `h(n) = |n − goal|`, the straight-line distance, the same relaxed-problem idea as the lecture's `h_SLD`.
  - **Admissible:** every step costs at least its length (exposure ≥ 0), so the true remaining cost ≥ straight-line distance.
  - **Consistent:** `h(n) ≤ |n − n'| + h(n') ≤ c(n, n') + h(n')` by the triangle inequality. So A* with graph search returns the cheapest (safest-and-shortest) path (Lecture 05 theorem).
- **Frontier:** a binary-heap priority queue on `f = g + h`, with a closed set. The node limit is 2,500 per search; past that the search returns the best partial path (closest by `h`) and logs it.
- **Execution:** Relay steers along the waypoint list using `NavMeshMotor` for the low-level movement between consecutive waypoints. The choice of route is Relay's own.
- **Replanning triggers:** the goal moves more than 1.5 m, a threat sighting makes the next three nodes exposed, or the motor reports `Blocked`.
- **Comparison for the defence:** the same search with λ = 0 behaves as plain shortest-path A*, and with `h = 0` it becomes uniform-cost search (Lecture 04). The debug overlay can switch between them to show the expanded node counts.

### 4.7 Layer 5 — Support position by simulated annealing (Lecture 05, Local Search)

Where Relay *should* stand is an optimisation problem: only the final state matters, not the path, so this is local search, not pathfinding.

- **State:** a candidate point `x` on the NavMesh.
- **Objective (maximise):**
  `E(x) = w1·inHealRange(x) + w2·hiddenFromThreats(x) + w3·lineOfSightToPlayer(x) − w4·inPlayerFireLane(x) − w5·inHazard(x) − w6·distanceFromRelay(x)/10`
  The weights depend on the chosen action: Retreat raises `w2`; Defend swaps `hiddenFromThreats` for `lineOfSightToTarget`.
- **Neighbour:** move `x` by a random offset of up to `r = 2 m` (shrinking with temperature), snapped to the NavMesh.
- **Acceptance:** always accept improvements; accept a worse move with probability `P = e^(ΔE / T)`.
- **Schedule:** T₀ = 1.0, geometric cooling `T ← 0.85·T`, 60 iterations per decision (a fixed budget, because the world is dynamic), and a warm start from the previous best spot.
- **Why not hill climbing:** hill climbing gets stuck on local maxima. Behind a crate the score is good, but behind a pillar 3 m away it is better, and the scores dip between the two. Annealing's occasional downhill moves escape local maxima (lecture slide "Overcoming Local Minima"). Hill climbing with random restarts is kept as the comparison baseline.

---

## 5. Constraints and boundaries

- Relay only reads other agents (`RiftWardenBrain`, player health); it never changes their behaviour. Hazard information comes from a read-only registry that the encounter publishes.
- **Real-time budget per decision cycle:** A* ≤ 2,500 node expansions, annealing ≤ 60 iterations, line-of-sight raycasts cached for 0.2 s.
- Tuning values stay in `AgentTuningData` (`RelayTuning`) so they can be adjusted without code changes.
- The existing presentation scripts (`RelayPodAnimator`, audio, signals) keep working because `RelayBrain.Action` and its public scores remain available.

## 6. Code structure (planned)

| File | Contents |
|---|---|
| `Assets/Scripts/AI/Relay/RelayBrain.cs` | Orchestrates the decision cycle (4.1) and exposes `Action`, `Decision` and scores |
| `Assets/Scripts/AI/Relay/RelayPerception.cs` | Sensors + world model (layer 1) |
| `Assets/Scripts/AI/Relay/RelayReflexes.cs` | Reflex rules (layer 0) |
| `Assets/Scripts/AI/Relay/RelayUtility.cs` | Expected-utility selection (layer 3) |
| `Assets/Scripts/AI/Algorithms/Fuzzy/*.cs` | Generic FIS: membership functions, rules, Mamdani inference, COG |
| `Assets/Scripts/AI/Algorithms/Search/*.cs` | Generic A* over a graph interface, priority queue |
| `Assets/Scripts/AI/Algorithms/Local/SimulatedAnnealing.cs` | Generic annealer over a state type |
| `Assets/Scripts/AI/Relay/RelayTacticalGraph.cs` | NavMesh-sampled grid + exposure costs (layer 4) |
| `Assets/Scripts/AI/Relay/RelayPositionSearch.cs` | Objective function + annealing (layer 5) |
| `Assets/Scripts/AI/Relay/RelayDebugOverlay.cs` | Live visualisation for the demo and defence |
| `Assets/Tests/EditMode/RelayAlgorithms/*.cs` | Unit tests |

The algorithms (`Assets/Scripts/AI/Algorithms`) are pure C# in their own assembly definition (`Skybreak.AI.Algorithms`), depending only on `UnityEngine` math types. This keeps them independent of the scene and testable in EditMode.

## 7. Requirement-to-lecture map (for the technical defence)

| Question an examiner may ask | Answer and where to show it |
|---|---|
| What architecture is Relay? | Hybrid / subsumption (L03): reflex override above a model-based, utility-based deliberative core. *Show:* section 4 diagram, `RelayBrain.Decide`. |
| Why not a simple reflex agent? | The environment is partially observable (L02/L03), and Relay has to remember threats behind walls. *Show:* layer 1 tracks, overlay of predicted positions. |
| How is it beyond uninformed search? | Informed A* with an admissible, consistent heuristic (L05), plus local search (L05), fuzzy reasoning (L09) and expected utility (L03). *Show:* expanded node counts, A* vs UCS. |
| Is your heuristic admissible? | Yes: step cost ≥ step length, and h is the straight-line distance (proof in 4.6). *Show:* unit test comparing with Dijkstra. |
| Why fuzzy logic? | Health and distance thresholds are vague; crisp cut-offs make the agent flip at the boundary. *Show:* membership degrees live, and the COG worked example as a test. |
| Why simulated annealing? | Position choice is an optimisation with local maxima; annealing escapes them (`P = e^(ΔE/T)`). *Show:* overlay of samples and the accepted/rejected moves. |
| How do you know it works? | Section 8 tests and the baseline comparison. |

## 8. Verification and evaluation

**Unit tests (EditMode):**
- **FIS:** membership values at the breakpoints, rule firing strengths, and COG matching the lecture's worked defuzzification example within 0.01.
- **A*:** on hand-made graphs, A* finds the same cost as uniform-cost search; it expands fewer nodes than UCS; with exposure it avoids a costly corridor; it reports failure on an unreachable goal.
- **Annealing:** on a 1-D function with two peaks it finds the global peak more often than hill climbing over 100 seeded runs.

**In-game scenario checks** (lab scene `OwnedAgentsLab`, fixed seeds):
- Player at 20% health with no threat: Relay heals within 2 s.
- A threat behind a wall: Relay keeps the track and predicts its position for up to 3 s.
- Boss slam warning on Relay: Relay is outside the ring before impact.
- Path to the player crosses the boss's line of fire: Relay takes the covered route (exposure on the overlay).
- Relay is never in the player's fire lane for longer than 0.5 s.

**Baseline comparison:** run the same five-minute scripted fight with the *current* `RelayBrain` and with the new one. Record heals applied while the player is below 40%, time the player spends below 30%, damage taken by Relay, time spent in the fire lane, and average decision time in ms. Report a table and a short discussion.

## 9. Build plan (one commit per step)

| Step | Work | Done when |
|---|---|---|
| 1 | This design document | Reviewed and committed |
| 2 | Split `RelayBrain` into perception, reflexes and utility files, **with no behaviour change** | Lab checks behave as before |
| 3 | Generic FIS + Relay rule base, replacing the linear scores | FIS tests pass; overlay shows memberships |
| 4 | Generic A* + Relay tactical graph | A* tests pass; Relay takes covered routes |
| 5 | Simulated-annealing position search | Annealing test passes; Relay finds cover and stays out of the fire lane |
| 6 | Debug overlay + baseline evaluation | Results table in `Docs/IS/Testing/` |

**Stretch goals (only after step 6):**
- **Bayes' rule (L09 Part 2):** a belief `P(PlayerInDanger | health drop, threat visible)` feeding the utility.
- **Expectimax (L10):** to choose an evade spot against the boss's probabilistic attack choice.
- **Learning critic (L03):** one that adjusts the rule-output weights from the evaluation metrics.

## 10. AI assistance

This design and its implementation are developed with AI assistance (Claude). Record each step accurately in the team contribution log, and make sure every algorithm in this document can be explained and traced by hand for the technical defence.
