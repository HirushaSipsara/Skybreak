# Sachini — Crystal Echo Warden
Owner: Sachini. Branch: sachini-ISagent-crystal-echo-warden, based on main 0c4da35.
This agent protects a crystal, hunts using acoustic evidence, and can be distracted.
The supplied SE3062 brief and lectures are the design basis.

## Delivery stages
1. Preserve the approved visual and export it; define contracts and pure C# graph.
2. Add bounded A*, sound attenuation, Bayesian beliefs and evidence decay.
3. Add utility decisions with action-specific features and constrained hill-climbing tactics.
4. Connect Unity perception, collision-aware movement, combat, presentation and a standalone FPS lab.
5. Add regression evidence, Unity checks, integration instructions and viva explanations.
Each stage is committed and pushed separately with its actual date. These commits do not substitute for five weeks of individual development.

## PEAS
Performance: protect the pedestal, find intruders using available evidence, survive, remain fair and responsive.
Environment: partially observable, dynamic, continuous FPS; a finite waypoint graph approximates navigable space.
Actuators: follow route, rotate, search, signal a wind-up and fire, retreat.
Sensors: field of view and occlusion rays, attenuated sound amplitude, objective alarm, own health.

## Boundaries
Core C# contains no Unity references. Unity adapters sample the world and execute decisions.
A* owns the complete route. The movement adapter follows it without a second NavMesh planner.
Only direct sightings update the exact last-known player location. Sound updates likelihoods over candidate zones.
Sound propagation is a simulated graph model, not a claim of a physically accurate acoustic engine.
Tactical eligibility checks are constraints, not a full CSP solver. There is no Minimax or genetic algorithm.
Unity runtime, four-agent integration and frame-rate validation require Unity 6000.6.0f1 and are pending on this machine.

## Lecture-to-code mapping

| Supplied lecture | Application | Code navigation |
| --- | --- | --- |
| Lecture 02: Intelligent Agents | PEAS; partially observable dynamic environment; percepts versus hidden world state | `WardenPerception.Tick`, `WardenBrain.Think` |
| Lecture 03: Agent Architectures & Autonomy | Internal world model, goals and utility-based selection; reactive attack validation | `BeliefMap`, `Decision.Choose`, `WardenBrain.ResolveAttack` |
| Lecture 04: Searching & Problem Solving | Explicit state/edge/cost model and uniform-cost baseline | `WorldGraph`, `TacticalSearch.TravelCosts` |
| Lecture 05: Informed Search | A* with accumulated cost and a consistent Euclidean heuristic | `Search.AStar` |
| Lecture 05: Local Search Algorithms | Random-restart hill climbing, local maxima, feasible candidate filtering | `TacticalSearch.Climb`, candidate evaluation in `WardenBrain.Think` |
| Lecture 09: Reasoning Under Uncertainty | Prior, likelihood, posterior and normalization using Bayes' rule | `BeliefMap.Observe`, `SoundLikelihood` |
| Lecture 11: Constraint Satisfaction | Feasibility constraints for clear, reachable, tactically suitable positions | `TacticalPoint.Feasible`; concept only, no backtracking/CSP solver |

The supplied lectures are the conceptual sources. This implementation does not claim to implement every lecture: no evolutionary optimization, formal inference engine, minimax, reinforcement learning or Bayesian network. The utility function is a manually designed weighted score informed by probabilistic belief; it is not a full expectation over a learned outcome-transition model.

## Perception and beliefs

Navigation nodes also approximate possible intruder zones. Let `b(i)` be the probability of zone `i`. Initially all zones have probability `1/N`. A direct sighting assigns 0.98 to the observed zone and distributes the remaining 0.02 across the others. A single-zone graph assigns 1.

For sound, uniform-cost search finds the least total attenuation from the listener to each possible source zone. Runtime link loss is `0.8 * distance + 12` when a collider blocks the link, otherwise `0.8 * distance`. A closed door can transmit an attenuated sound while remaining impassable for movement. These are gameplay units, not calibrated decibels. Only existing authored links carry sound; a missing link does not transmit it.

For source loudness `L`, loss `d(i)` and received amplitude `r`:

```text
expected(i) = max(0, L - d(i))
likelihood(i) = 0.01 + 0.99 * exp(-(expected(i) - r)^2 / (2 * 1.5^2))
posterior(i) = prior(i) * likelihood(i) / sum_j(prior(j) * likelihood(j))
```

The sensor ignores received amplitudes below 2 and adds small repeatable amplitude noise. The 0.01 likelihood floor preserves alternatives when a noisy cue conflicts with previous evidence. The bus event origin is used to simulate the sensor reading, then discarded from the belief update. Known emission loudness is a simplifying assumption. Equal attenuation paths receive equal likelihoods; the Warden cannot infer a unique hidden location from one amplitude measurement.

Between observations, a fraction `min(0.5, 0.3 * dt)` of each zone's probability diffuses to its open neighbours, with `dt` clamped to one second. A small uniform mixture `0.02 * dt` prevents permanent certainty. Looking at an empty, visible zone multiplies its probability by 0.05 and renormalizes; this is approximate negative evidence because a point does not describe an entire room.

Freshness is `exp(-age / 6)` and is separate from the probability distribution. After six seconds it is about 0.368. Spatial concentration is `1 - H(b)/log(N)`, where `H(b) = -sum b(i) log b(i)`. Fresh evidence can still be spatially ambiguous. A sighting or new sound updates evidence time; an empty-zone check does not. Repeated cues are approximated as conditionally independent after prediction, which can overstate certainty for correlated sounds; freshness decay and diffusion mitigate, but do not fully solve, this limitation.

## Why A* and why this heuristic

`f(n) = g(n) + h(n)`, with `h(n)` equal to straight-line distance to the goal. Every movement edge costs at least its straight-line length because extra costs cannot be negative. By the triangle inequality, `h(n) <= cost(n,m) + h(m)`, so the heuristic is consistent and admissible for this graph. With unchanged nonnegative costs during a query and a sufficient budget, the first popped goal has optimal graph cost. It need not be the shortest possible continuous-world path because the graph discretizes the map.

A binary min-heap prioritizes the frontier. A* uses open movement links; acoustic search uses all links with their sound losses. Graph refresh happens between synchronous searches. A new graph version causes route recalculation, so this is repeated A* on dynamic topology, not D* Lite or an incremental shortest-path algorithm. Bounded failure returns `BudgetExceeded` rather than falsely claiming that a path does not exist. Random graph tests compare results with uniform-cost search.

Compared with BFS, A* supports unequal edge costs. Compared with greedy best-first search, it accounts for cost already paid. A consistent heuristic can reduce explored nodes relative to uniform-cost search, although actual expansions depend on the map. Heap-based graph search has the usual `O((V+E) log V)` bound for these finite graphs; local physics queries are a separate runtime cost.

## Decisions and tuning rationale

The brain first constructs feasible choices from current evidence, health, objective alarm and reachable positions. It then scores them. Invalid actions have negative-infinite utility, so commitment cannot preserve an impossible attack or route candidate. Health, freshness, danger, travel and candidate qualities are normalized to `[0,1]`.

| Action | Default score and eligibility |
| --- | --- |
| Guard | `0.20 + 0.38 * danger` |
| Patrol | `0.32 * (1 - freshness)`; patrol available |
| Investigate | `0.58 * freshness + 0.16 * (1 - concentration) - 0.12 * travel`; reachable belief zone, freshness > 0.08 |
| Pursue | `0.60 + 0.18 * freshness - 0.12 * travel`; direct sight |
| Intercept | `0.58 + 0.40 * danger`; candidate available, danger > 0.25 |
| Flank | `0.52 + 0.32 * quality - 0.12 * travel`; candidate available, freshness > 0.25 |
| Retreat | `0.65 + 0.35 * (1 - health) + 0.05 * quality`; feasible cover, health < 0.35, freshness > 0.08 |
| Attack | `0.82 + 0.08 * health + 0.12 * danger`; visible and within range |

The `travel` score is the normalized cost to the selected investigation zone. It is reused as a common chase effort penalty in pursue/flank; the flank quality separately includes its own travel cost. This is a heuristic simplification worth revisiting during tuning, not a physically calibrated utility measure.

The uncertainty term gives ambiguous fresh observations a reason to investigate. Patrol is low under fresh evidence. Retreat becomes strong when health is low and cover exists. A full-health in-range target at maximum danger scores 1.02, above interception at 0.98. At 10% health with maximum retreat quality, retreat scores 1.015, above attack at 0.948. These relationships are intentional gameplay priorities, not empirically optimized weights.

Ordinary switches require a utility gain of 0.08 and a 0.65-second minimum commitment. A winning attack, retreat or urgent interception bypasses this commitment; an infeasible current action is always discarded. This limits oscillation without retaining an attack after sight is lost. The regression suite verifies important transitions rather than simply checking that a formula executes.

To tune, vary one parameter at a time and compare detection delay, false investigations, objective protection time, damage dealt/taken and action switches per minute under repeatable scenarios. Record the values and observed tradeoff. Avoid claiming these defaults are optimal before playtesting.

## Tactical local search and interception

All tactical candidates must be reachable and collision-free. Flank candidates also require a 3–11 m range, a side-angle factor above 0.35, and travel greater than 0.7 m. Their quality is:

```text
0.4 * sideAngle + 0.3 * covered + 0.2 * preferredRange + 0.1 * cheapTravel
```

The preferred distance is 6 m. Retreat candidates require cover and at least 4 m separation; quality is `0.6 + 0.25 * separation + 0.15 * cheapTravel`. Candidate scoring is performed across the small graph; the local search then tries the previous candidate and random feasible starts, moving only to strictly better adjacent feasible points. Four restarts and a 24-step cap bound work. Restarts can escape an inferior basin, but they do not guarantee a global optimum. On this small map an exhaustive best-candidate choice is also reasonable; retaining hill climbing makes the lecture tradeoff demonstrable, not automatically faster because candidate features are still evaluated for every node.

When the objective is threatened, the brain predicts an A* route from the most likely intruder zone to the guard zone. Along it, it maximizes `predicted intruder arrival time - Warden travel time`, using an assumed intruder speed of 5 m/s. It selects the best available margin, even if every margin is negative; therefore an interception attempt is not a guarantee of arriving first. Prediction uses beliefs and observations, not the hidden player transform. Destroying the crystal removes this goal.

## Creative gameplay and limits

The Warden combines acoustic ambiguity, deceptive decoys and a competing protection goal. Loud movement can reveal an approach, a closed door changes both route and received sound, and an alarm may pull it away from a decoy. A visible amber wind-up gives the player time to break line of sight. The creativity is in those interacting gameplay rules; they need a recorded Unity demonstration to establish how engaging they feel.

The implementation goes beyond basic uninformed search through belief updates, informed routing, weighted decisions and local tactical optimization. Passing algorithm tests does not establish a perfectly executed full game. In particular, graph discretization, hard-coded sound classes, correlated cues, crowd congestion, Unity import and final integration remain practical limits to explain honestly in the viva.

