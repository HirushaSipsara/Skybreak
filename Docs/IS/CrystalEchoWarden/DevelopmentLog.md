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

## Findings that changed the implementation

- Quiet patrol initially could lose to guard because of the switching margin. The default patrol utility now clears that margin.
- Objective interception could outweigh combat at firing distance. The attack danger bonus and immediate acceptance of a winning attack permit firing without waiting behind an old travel intent. A transition regression covers entering range during interception commitment.
- Defeated agents must stop their controller and pending attack. Round reset clears objective alarms and deactivates old actors before their deferred destruction.
- A blocked route must be distinguishable from an invalid endpoint or an expansion-budget exhaustion. Tests exercise all three results.
- Sound cannot be treated as an exact location observation. Symmetric acoustic hypotheses remain ambiguous, and fading evidence returns the agent toward patrol.

## Next real development sessions

Run the Unity checklist, save actual results and captures, adjust model orientation/materials if required, tune the acoustic/utility values from playtests, and integrate with the other three student agents. Commit each tested improvement with its actual date and retain a short explanation of the observed issue and outcome. Prepare the viva by tracing one sound event through belief update, action choice and routing, and by changing a weight live and explaining the result.
