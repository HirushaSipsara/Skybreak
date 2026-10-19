# Validation record and Unity acceptance checks

## Executed locally

On 20 October 2026, the .NET 8 harness compiled with zero warnings/errors and passed 37 checks. It includes shortest paths, dynamic doors, sealed goals, invalid inputs, expansion budgets, Bayesian updates, conflicting/stale evidence, normalization, utility transitions, critical retreat, local-search restarts and unavailable cover.

The randomized regression compares A* cost and legal route edges against uniform-cost/Dijkstra results on 150 weighted graphs with changing links. The stress check completes 1,200 combined perception-belief/decision/route cycles across four independent cores on a 144-node graph. Its elapsed time excludes Unity physics, rendering, input, animation and the other students' agents. It is **not an FPS benchmark**.

Roslyn also parses all 16 agent/editor C# files as C# 9. This catches syntax errors but does not resolve Unity APIs or compile the Unity project. The core is compiled separately against .NET 8; Unity API/assembly compatibility remains to be tested in the editor.

Recorded console output: [CoreTestResults.txt](CoreTestResults.txt).

`python Tools/WardenTests/validate_assets.py` also passed: 25 agent assets/folders have unique metadata, the lab's bootstrap script reference resolves, and the Unity FBX resource is byte-identical to the exported source. This validates file wiring, not Unity's model import.

To repeat from PowerShell, first change into `Tools/WardenTests` so its SDK selection applies:

```powershell
dotnet build WardenTests.csproj --disable-build-servers
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet bin/Debug/net8.0/WardenTests.dll
```

The harness uses SDK-bundled Roslyn and no downloaded NuGet packages. SDK 8.0.426 or a compatible installed .NET 8 feature band is required by `global.json`.

## Pending Unity checks

**None of the following has been executed on this machine.** Unity Editor is unavailable. `UnitySmokeResults.txt` is intentionally absent until the Editor menu runs. A pass from that menu covers immediate wiring, occlusion, graph changes, single-consumption hearing, death and independent actor setup; it does not simulate an extended match.

| Scenario | Expected observation and evidence |
| --- | --- |
| Import and compile | Unity 6000.6.0f1 opens the branch without Console errors. FBX has correct scale/facing, cyan core, no missing materials. Capture screenshot. |
| Menu smoke checks | Enter lab Play Mode and run the checks. Inspect the generated report for every PASS and no FAIL. |
| Quiet patrol | With no recent evidence, Warden moves among reachable points near the crystal/home. It must not follow an unseen silent player exactly. |
| Sound ambiguity | Fire or sprint behind cover. Evidence updates and likely zones change; equal acoustic paths may remain ambiguous. Stand still afterward and observe freshness decay. |
| Decoy | Place Q decoy away from your position. Warden investigates possible source zones and can be distracted. Record route and belief before/after. |
| Lost sight | Enter view, then hide behind a wall. Exact last sighting stops updating. Warden searches; it does not track your hidden live transform. |
| Attack fairness | At less than 8 m in clear view, core winds up for 0.6 s before a 10-damage pulse. Moving behind cover during wind-up prevents damage. A friendly actor between shooter and target blocks the pulse. |
| Door during movement | Close F2 while a route uses the doorway. Warden stops/replans around it without clipping. Reopen and verify a route becomes available. |
| No route | Seal all connections to a destination with solid colliders. Warden reports a failure/stops or selects a reachable goal. It does not teleport or loop indefinitely in one frame. |
| Low health | Damage Warden below 35% health with available cover. It selects a feasible retreat; no-cover situations remain safe. |
| Crystal alarm | F3 or nearby F raises objective danger. Warden can intercept a predicted approach, while a visible in-range target permits firing instead of endless travel. |
| Crystal removed | Reduce integrity to zero. No null errors or attempts to interact with a missing crystal. R restores integrity, visual and alarm state. |
| Death and reset | Defeat Warden during movement/wind-up. No late shot; collider disabled. Repeated R/F4 resets do not leave active old actors or stale alarm/evidence. |
| Four Wardens | F4, then run for at least five minutes with doors, decoys, combat and repeated resets. Check collision congestion, duplicate actor state and Console. |
| Four student agents | Combine all four distinct agents in the final map. Test shared player damage, target references, round reset and resource ownership. Record profiler capture and failures fixed. |

## Performance capture

Use the Unity Profiler or a development build on the target demonstration machine. Record hardware, resolution, quality level, graph size, one-agent and four-distinct-agent scenarios, median/95th-percentile frame time, CPU main-thread time, GC allocations and AI tick duration. Agree a target such as 60 FPS (16.7 ms frame budget) with the team and report measured results rather than asserting it is achieved. The HUD displays a single AI tick duration for diagnosis only.

Decision ticks run every 0.25 s with small start offsets; map checks every 0.4 s; graph size is capped at 256; A* has a 256-expansion runtime budget; hill climbing uses four starts and at most 24 steps per start. Event storage is capped at 128 and events older than one second are ignored. Physics queries and allocation costs still need profiling and tuning in Unity.

## Assignment evidence

The algorithm tests support implementation correctness for their covered cases. The model, sound deception and objective-driven tactics support the design discussion. Unity acceptance and final four-agent profiling are needed before claiming robust gameplay integration. Real iterative development and the student's ability to explain and modify the code remain essential; this delivery does not establish five weeks of effort or guarantee a mark.
