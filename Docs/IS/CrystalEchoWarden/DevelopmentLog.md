# Development record

This records actual implementation stages. Consult Git for author and timestamp; these stages are not separate weeks of work.

| Stage | Commit | Change |
| --- | --- | --- |
| 1 | `8d7f398` | Refined model, standalone Blender source, FBX handoff, graph contracts and baseline test harness. |
| 2 | `e4d106f` | Bounded A*, acoustic attenuation, normalized Bayesian beliefs and evidence decay. |
| 3 | `0bbd7d7` | Utility decisions, commitment rules, constrained tactical candidates and random-restart hill climbing. |
| 4 | `0d19d68` | Unity sensors, movement, damage, presentation, model resource and a standalone FPS lab. |
| 5 | `ca0ccbb` | Regression/stress cases, firing-transition fix, safe death/reset handling, Unity smoke-check menu and theory/integration/validation documents. |
| Movement follow-up | See animation commit | 12-bone robot rig, eight authored clips, animated FBX, velocity-driven Unity clip playback and movement-selection tests. |
| Unity validation follow-up | See validation commit | Installed the matching Editor; corrected object-ID and SphereCast compatibility errors; imported eight clips; passed scene and extended gameplay checks with recorded results. |

## Findings that changed the implementation

- Quiet patrol initially could lose to guard because of the switching margin. The default patrol utility now clears that margin.
- Objective interception could outweigh combat at firing distance. The attack danger bonus and immediate acceptance of a winning attack permit firing without waiting behind an old travel intent. A transition regression covers entering range during interception commitment.
- Defeated agents must stop their controller and pending attack. Round reset clears objective alarms and deactivates old actors before their deferred destruction.
- A blocked route must be distinguishable from an invalid endpoint or an expansion-budget exhaustion. Tests exercise all three results.
- Sound cannot be treated as an exact location observation. Symmetric acoustic hypotheses remain ambiguous, and fading evidence returns the agent toward patrol.
- Real Unity compilation found API errors that syntax-only checks could not detect. Explicit spawn staggering and the correct SphereCast overload resolve them.
- Deterministic tests must suppress input without disabling the sensed player. The lab player now exposes `acceptInput`; it remains enabled and damageable in automated tests.

## Next real development sessions

Complete the remaining manual Unity checklist and rendered performance captures, tune the acoustic/utility values from human playtests, and integrate with the other three student agents. Compilation, smoke and automated gameplay results are now recorded in this folder. Commit each tested improvement with its actual date and retain a short explanation of the observed issue and outcome. Prepare the viva by tracing one sound event through belief update, action choice and routing, and by changing a weight live and explaining the result.
