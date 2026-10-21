# Vault Hound — Chamber Hunter Agent (A* + BFS)

> **Module:** Intelligent Systems (IS) — SE3062  
> **Student:** Member 3 (SawanIS)  
> **Agent Class:** Vault Hound — Mechanical Quadruped Chamber Hunter  
> **Namespace:** `Skybreak.AI.SawanIS.VaultHound`  
> **Primary Algorithms:** Informed A\* Search (Manhattan Heuristic) & Uninformed Breadth-First Search (BFS Flanking & Area Sweep)

---

## 1. Design Summary

**Vault Hound** is a coordinated pair of mechanical quadruped chamber hunters placed inside the two side chambers of the Aurora Vault security wing. Both instances share one AI brain script (`VaultHoundBrain`) and one coordination object (`HoundPack`). This design counts as **one agent design** with two instances.

The Vault Hound implements a **Model-Based Goal-Driven Agent Architecture** with multi-agent coordination, memory, and dynamic search. It provides clear architectural diversity from the other agents in the project:

| Agent | Architecture | Primary distinction |
|---|---|---|
| Relay | Utility selector | Companion; weighs competing cooperative actions |
| Bastion | Model-based utility | Solo cover-aware tactical combatant |
| Rift Warden | Scripted phase boss | Three-phase attack choreography |
| **Vault Hound** | **Goal-driven + BFS coordination** | **Paired hunters with role-split pursuit and BFS flanking** |

---

## 2. IS Justification

| Requirement | Implementation | Justification |
|---|---|---|
| **Memory** | `lastKnownPlayerPosition` + linear confidence decay | Player can duck behind walls. Hound stores last sighting and decays confidence over `memoryDuration` seconds: `c = clamp01(1 - Δt / T_mem)`. When confidence → 0 the hound redeploys. |
| **Search** | BFS area sweep (uninformed) | After sight is lost, no heuristic exists for which direction the player fled. BFS expands in concentric rings around the last sighting, guaranteeing coverage of the nearest hiding spots before deeper ones. |
| **Route planning** | A\* with Manhattan heuristic (informed) | Direct pursuit and chamber deployment use A\* over a discrete 4-connected cardinal grid. Manhattan distance is admissible and consistent on this topology (see proofs below). |
| **Partner coordination** | `HoundPack` shared memory + role assignment | One hound becomes the **pursuer** (direct A\*) and the other the **flanker** (BFS-discovered alternate route). Roles are assigned when a sighting is first confirmed. Shared sighting lets both respond to the same confirmed position rather than searching independently. |
| **Blocked-route recovery** | Progress check every `stuckSeconds` | If the agent moves less than `minimumProgress` metres between checks while a path is active, `routeBlocked` is set and a fresh path is planned from the current position. Grid version changes also invalidate cached paths. |
| **Survivor mode** | Pack notification on death | When one hound dies, `HoundPack.NotifyHoundDestroyed` reassigns the survivor to solo `DirectPursue` without requiring scene changes. |

---

## 3. Search Algorithm Specifications and Mathematical Proofs

### 3.1 Informed Search: A\*

The evaluation function for node $n$:

$$f(n) = g(n) + h(n)$$

- $g(n)$: exact accumulated path cost from start to $n$. Cardinal step cost $c = 1.0$.  
- $h(n)$: Manhattan distance heuristic to the goal:

$$h(n) = |\text{goal}_x - n_x| + |\text{goal}_z - n_z|$$

#### Admissibility proof

A heuristic is admissible when $h(n) \le h^*(n)$ for all $n$.

*Proof:* In a 4-connected cardinal grid with step cost 1, any path between $(x_1, z_1)$ and $(x_2, z_2)$ requires at least $|x_2 - x_1|$ horizontal and $|z_2 - z_1|$ vertical steps. Obstacles can only increase this minimum. Therefore $h(n) = |\Delta x| + |\Delta z| \le h^*(n)$. ∎

#### Consistency proof

A heuristic is consistent when $h(n) \le c(n, n') + h(n')$ for every node $n$ and successor $n'$.

*Proof:* One cardinal step changes the Manhattan distance to the goal by at most 1 (either $+1$ or $-1$). So $|h(n) - h(n')| \le 1 = c(n, n')$, and the triangle inequality $h(n) \le c(n, n') + h(n')$ holds. Consistency implies admissibility, and it guarantees that the first time A\* expands a node the path to it is optimal — no closed node needs to be re-opened. ∎

### 3.2 Uninformed Search: BFS (Flanking and Sweep)

BFS is used in two distinct contexts:

**Sweep search** — when a Hound loses sight of the player:  
No heuristic can predict where the player hid. BFS expands outward from the last sighting in concentric Manhattan rings, ensuring the agent systematically inspects nearest hiding spots before farther ones (shallowest-first property of BFS).

**Flank path discovery** — assigning the partner an alternate route:  
After the pursuer plans a direct A\* path, its cell keys are shared with the flanker. The flanker runs BFS from the player's cell outward, collecting candidate destination cells that are *not* on the pursuer's path. It then selects the reachable candidate with the shortest A\* approach route from its own position. This produces a meaningful flanking trajectory without requiring precomputed waypoints.

*Viva explanation for BFS choice over A\*:*  
"A\* requires a known goal coordinate for the heuristic. When the player breaks line of sight, the flanker has no information about which alternate corridor is open relative to the pursuer. BFS is uninformed — it explores uniformly in all directions, which correctly models the flanker's genuine ignorance about the alternative path layout."

---

## 4. State Machine

```
                 +------------------------------------------+
                 |              CHAMBERED                    |
                 | Motionless; waits for OnChamberOpen call  |
                 +------------------------------------------+
                              | OnChamberOpen()
                              v
                 +------------------------------------------+
                 |              DEPLOYING                    |
                 | Walks to last shared combat sighting      |
                 +------------------------------------------+
                       |                            ^
          Player seen  |           Memory expired / | Sweep done
          (sighting    |            no sighting     |
           reported)   |                            |
                       v                            |
       [Role: PURSUER]           [Role: FLANKER]    |
   +------------------+      +--------------------+ |
   |   DIRECT_PURSUE  |      |   FLANK_PURSUE     | |
   | A* to player pos |      | BFS alternate route| | <--+
   +------------------+      +--------------------+ |    |
            |                          |            |    |
  Sight lost|              Sight lost  |            |    |
            +----------+   +-----------+            |    |
                       v   v                        |    |
                 +------------------------------------------+
                 |              SEARCH                       |
                 | BFS sweep route around last known pos     |
                 +------------------------------------------+
                       |                            |
          Player seen  |             Sweep complete | Memory expired
                       |                            v
                       |                     (DEPLOYING again)
         Close range ↓ | reassign roles
                 +------------------------------------------+
                 |              ATTACK                       |
                 | Crouch → Lunge → Swipe hit check         |
                 +------------------------------------------+
                       |  Connected         |  Missed
                       v                   v
              (resume pursue)    +-------------------+
                                 |   LUNGE_RECOVERY  |
                                 | 1.2 s window      |
                                 +-------------------+
                                        |
                                 (resume pursue)
                                 
                    Any state + HP = 0 → DEAD (stop, notify pack)
```

### State transition matrix

| Current | Condition | Next | Effect |
|---|---|---|---|
| **Chambered** | Chamber door opens | **Deploying** | Enable NavMeshAgent, head to last sighting |
| **Deploying** | Player seen | **DirectPursue / FlankPursue** | Pack assigns pursuer/flanker roles |
| **Deploying** | Reached target, no sighting | **Deploying** | Await shared sighting; investigate area |
| **DirectPursue** | Player within `attackTriggerRange` | **Attack** | Begin coroutine |
| **DirectPursue** | Sight lost, memory valid | **Search** | BFS sweep around last known |
| **DirectPursue** | Sight lost, memory expired | **Deploying** | Fall back to last shared sighting |
| **FlankPursue** | Same as DirectPursue | same as above | same |
| **Search** | Player re-acquired | **DirectPursue / FlankPursue** | Re-assign roles, re-plan |
| **Search** | Memory expired | **Deploying** | Reset |
| **Search** | Sweep cells exhausted | **Deploying** | Fall back |
| **Attack** | Hit connected | **DirectPursue / FlankPursue** | Short pause, resume pursuit |
| **Attack** | Lunge missed | **LungeRecovery** | 1.2 s recovery, then resume |
| **LungeRecovery** | Timer expired | **DirectPursue / FlankPursue** | Resume |
| *Any* | HP = 0 | **Dead** | Stop; notify HoundPack; survivor switches to solo |

---

## 5. Memory Model

$$\text{Confidence}(t) = \text{clamp01}\!\left(1 - \frac{t - t_{\text{lastSeen}}}{T_{\text{mem}}}\right)$$

- While the player is visible: Confidence = 1.0.
- On losing visual contact: decays linearly over `memoryDuration` seconds (default 5 s).
- When Confidence → 0: hound terminates search and redeploys.
- Shared sightings via `HoundPack.ReportSighting` refresh the pack's memory independently of each brain's private timer.

---

## 6. Partner Coordination Protocol

1. Both hounds `RegisterHound(this)` in `Awake`. The pack assigns houndA → pursuer, houndB → flanker.
2. The first hound to call `ReportSighting` sets the shared position and timestamp.
3. `IsPursuer` / `IsFlanker` queries let each brain self-select its path algorithm.
4. The pursuer shares its `CurrentPathCells` (encoded grid keys) so the flanker's BFS can generate a genuinely different route.
5. On death, `NotifyHoundDestroyed` sets the survivor as the sole pursuer; `IsFlanker` returns false for all remaining hounds.

---

## 7. Combat: Lunge and Swipe

| Phase | Duration | Effect |
|---|---|---|
| Crouch (warning) | 0.25 s | Hound lowers visually (animation cue to player) |
| Lunge travel | 0.4 s | Lerp from start to player's position |
| Swipe hit check | instant | `Physics.OverlapSphere` radius 1.6 m, deals 22 damage |
| Miss recovery | 1.2 s | Hound motionless — player's punish window |

The hit is resolved at the moment the lunge travel completes. If no valid `IDamageable` target is inside the swipe radius, the lunge is classified as a miss and recovery begins.

---

## 8. Chamber Release Integration

`ChamberReleaseController` monitors all GameObjects tagged `"Area1Enemy"`:

- **Excluded automatically:** GameObjects without the `"Area1Enemy"` tag. This means Relay, Vault Hounds (inside chambers), and Area 2+ enemies are never counted.
- **Trigger:** exactly once, when `deadArea1 >= totalArea1`.
- **Door activation:** calls `SetActive(true)` on serialized door GameObjects and sets the `"Open"` animator bool.
- **Hound entry:** wire the `ChamberReleaseController.OnChambersOpen` C# event to each hound's `OnChamberOpen()` method in the scene.

---

## 9. File Map

| File | Responsibility |
|---|---|
| `VaultHoundGrid.cs` | Discrete passable/blocked world grid; Physics-baked; dynamic door support |
| `VaultHoundPathfinder.cs` | Static A\* and BFS algorithms; flanking path; sweep route builder |
| `HoundPack.cs` | Shared sighting memory; pursuer/flanker role assignment; survivor protocol |
| `VaultHoundBrain.cs` | Full state machine, perception, memory, attack coroutine, debug overlay |
| `ChamberReleaseController.cs` | Area 1 clear counter; one-shot chamber door trigger |

---

## 10. Viva Quick Reference

**Q: Why use two algorithms instead of one?**  
*"A\* is optimal for goal-directed navigation because the Manhattan heuristic is admissible and consistent, so the first path found is always the shortest. BFS is chosen for flanking and sweep search because in those situations the agent has no information about which direction leads to an open alternative route or where the player is hiding — a heuristic would be an arbitrary guess. BFS explores uniformly, which correctly models the agent's epistemic state."*

**Q: How do the two hounds avoid converging on the same route?**  
*"The pursuer plans an A\* path and encodes its cell keys. When the flanker runs BFS it filters out any candidate destination that shares cells with the pursuer's path, then runs A\* from its own position to the best remaining candidate. This guarantees a structurally different approach even if both hounds start from symmetric chambers."*

**Q: What happens if one hound dies?**  
*"The brain calls HoundPack.NotifyHoundDestroyed. The pack sets the survivor as the sole pursuer and clears the flanker reference. From that point IsFlanker returns false and the survivor always plans via direct A\*."*

**Q: Why is the chamber release counter separate from the hound AI?**  
*"It's a world-state monitor, not agent behaviour. Coupling it to the brain would force every hound to maintain a global enemy count, which breaks separation of concerns. The controller subscribes to each Area 1 enemy's OnDefeated event and fires the chamber trigger once — independent of how many hounds are alive or what state they are in."*
