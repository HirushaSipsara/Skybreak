# Rift Warden evaluation: original boss vs adaptive boss

Raw results: `step6_evaluation_original.txt` and `step6_evaluation_adaptive.txt` in this folder.

## Method

| | |
|---|---|
| Scene | `OwnedAgentsLab` (arena), batch Play Mode, audio muted, `Time.timeScale = 2` |
| Fight | 75 s of game time per run, 3 runs per player style. Relay is parked out of range: boss against player only. |
| Player shooting | Scripted 6 damage/s at the cannon arm, then the blade, then the core (so every fight passes through all three phases) |
| Player styles | **Strafer**: circles the arena at 3.4 m/s, always counter-clockwise. **Jumper**: stands at mid range and times a jump so its peak lands on each melee/slam impact. **Kiter**: backs away at 3.4 m/s and slides along the arena edge. **Camper**: stands still at 9 m. |
| Original boss | Decision logic from commit `24524b6` (proved identical to the pre-upgrade boss) |
| Adaptive boss | Current code: player model, predictive aim, Bayesian learner, expectimax planner, phases, tactics, director |
| Measurement | By the harness (`Assets/LocalCombatLab/Editor/RiftEvaluation.cs`, local lab tool), using only API both versions have |

- **Hit rate** = player-damage events ÷ attack trials (each cannon shot plus each melee/slam damage window).
- **Entropy** is the Shannon entropy of the attack-choice mix: higher means the choice is harder to predict.

## Results (mean of 3 runs per style)

| Style | Hit rate (original → adaptive) | Damage to player | Player falls | Attack-mix entropy (bits) | Same attack twice in a row |
|---|---|---|---|---|---|
| Strafer | 38% → **51%** | 190 → **270** | 1.0 → 2.0 | 1.95 → 1.74 | 12% → 38% |
| Jumper | 71% → **83%** | 320 → **490** | 3.0 → 4.0 | 1.34 → 1.39 | 52% → 61% |
| Kiter | 16% → **37%** | 74 → **97** | 0 → 0.3 | 1.25 → 0.96 | 32% → 47% |
| Camper | 100% → 90% | 530 → 530 | 5 → 5 | 1.34 → 1.39 | 52% → 61% |

Mercy windows (director) engaged 2–4 times per fight for the adaptive boss, and never for the original, which has no director.

## Discussion

- **It reads moving players much better.**
  - Against all three moving styles the hit rate rose by 12–21 percentage points, and damage dealt by 24–53%.
  - The biggest gain is against the **Kiter** (more than double the hit rate). The original boss kept firing at where the kiter *was*; the adaptive boss leads the shot and learned that its cannon at mid and far range is its best tool against this player.
  - Against the **Strafer**, the player model locked onto the habit within seconds (strafe bias −0.98 to −0.99, "always circles one way"), and the double rear teleport, which ignores strafing, became its main attack (7 of 17 choices).
- **The still player is unaffected.** The Camper is hit 90% of the time instead of 100%: the boss spends a few attacks exploring options that the original's fixed order never tried. Damage is identical (530).
- **The trade-off is less variety.** Against players with a fixed habit, the adaptive boss repeats what works: entropy is lower against the Strafer and Kiter, and it repeats an attack more often. That is the exploitation side of the bandit doing its job. It makes the boss *more effective*, but not more varied, against a scripted opponent. Against a real player who changes tactics, forgetting (γ = 0.97) and Thompson sampling bring variety back. These scripted archetypes never change, so this evaluation cannot show that.
- **Learning within a fight.** For the Strafer, the hit rate went from 43% in the first half to 81% in the second, against 30% → 100% for the original. Both second halves are small samples (5–6 trials) dominated by the core-phase slam, so this is weak evidence of learning on its own. The stronger evidence is the overall hit-rate gain on all three moving styles, and the unit tests showing the learner converging (`BanditTests`).
- **Fairness.**
  - Every telegraph and damage value is unchanged.
  - The mercy window engaged whenever the player fell below 25%.
  - Predictive aim is capped (lead factor 0.4–0.85, at most 2.5 m), so changing direction still escapes.
  - The Jumper still dodges jumpable attacks. Only the slam is a low attack; the slash and rear strike are not meant to be jumped. The original boss's near-zero second-half hit rate against the Jumper comes from the core phase, where it only slams.
- **Determinism.** The 3 runs per style are nearly identical, because the boss's random generator is seeded and the scripted players are deterministic. The small differences come from frame timing. The variation between styles is far larger than the variation between runs.
