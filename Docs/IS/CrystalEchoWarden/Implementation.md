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

