# Relay evaluation: step-2 baseline vs layered agent

Raw results: `step6_evaluation_baseline.txt` and `step6_evaluation_layered.txt` in this folder.

## Method

| | |
|---|---|
| Scene | `OwnedAgentsLab` (Rift Warden arena), batch Play Mode, audio muted, `Time.timeScale = 2` |
| Player | Scripted: walks a 7 m circle around the arena centre at 0.3 rad/s, always facing the boss (so the line of fire sweeps the arena). Does not dodge or shoot. |
| Boss | Fully autonomous (`RiftWardenBrain`, unchanged) |
| Relay | Fully autonomous. **Baseline** = the step-2 code (commit `9d21aff`, linear utility, NavMesh movement). **Layered** = the current code (fuzzy inference, expected utility, tactical A*, simulated annealing). |
| Runs | 8 seeds per version (11, 22, …, 88), 90 s of game time or until the player falls |
| Measurement | By the harness (`Assets/LocalCombatLab/Editor/RelayEvaluation.cs`, local lab tool), independent of Relay's code. It uses only API both versions share, so the same file measures both. |

The two versions were run from the same working tree: the step-2 files were restored from git for the baseline run and the current files put back afterwards.

## Results (mean of 8 runs)

| Metric | Baseline | Layered | Per minute: baseline → layered |
|---|---|---|---|
| Player fell | 6 / 8 | **4 / 8** | — |
| Player below 30% health (s) | 14.7 | **4.0** | 11.6 → **3.8** s/min |
| Lowest player health | 1 | **14** | — |
| Heals given | 7.6 | 7.8 | 6.0 → **7.5** /min |
| Heals given while player < 40% | 1.8 | 1.4 | — |
| Damage taken by Relay | 77 | **35** | 61 → **34** /min |
| Relay in the player's line of fire (s) | 7.8 | **4.6** | 6.2 → **4.4** s/min |
| Relay in the boss's line of sight (s) | 74.8 | 62.1 | 59.1 → 59.5 s/min (no change) |
| Mean distance to player (m) | 5.8 | **3.5** | — |
| Run length (s) | 75.9 | 62.6 | — |
| Decision cycle (`SenseAndDecide`) | 0.005 ms | 1.1 ms | — |

Per-minute values divide by each version's mean run length, because runs end early when the player falls.

## Discussion

- **Relay survives far better.** It takes 45% less damage per minute: self-preservation urgency triggers retreats to covered, annealed spots instead of fixed "4 m away" points.
- **It supports more.**
  - It stays about 2.3 m closer to the player.
  - It heals 24% more often per minute (the expected utility of healing rises as soon as heal urgency is Medium, not only below a crisp 25%).
  - It spends 29% less time in the player's line of fire, thanks to the Reposition action.
- **Player outcome improved, with a caveat.** Fewer falls (4 vs 6 of 8) and much less time below 30% health. With 8 runs per version this is indicative, not statistically conclusive. The layered runs that did fall, fell earlier (27–45 s), and heals while the player was below 40% were not higher (1.4 vs 1.8). The scripted player never dodges and the boss can out-damage one 18-point heal every 5 s, so Relay alone cannot prevent every fall.
- **Line of sight to the boss did not change** (≈ 59 s per minute for both). The arena is open ground with almost no cover, so the tactical cost rarely finds a covered alternative there. The best detour found in the step-4 sweep was 9 watched cells instead of 11. Tactical A* matters more in the corridors of the main map.
- **Cost.** The decision cycle went from microseconds to about 1.1 ms (fuzzy inference over 1001 samples × 3 outputs, A* up to 2,500 nodes, 60 annealing steps with raycasts). That is about 0.6% of the 0.2 s decision interval and acceptable in real time.
- **Reproducibility.** Seeds fix the random generator but not frame timing, so a seed does not reproduce exactly between runs (an earlier 3-seed layered run gave different numbers for seed 11). This is why several seeds are averaged.
