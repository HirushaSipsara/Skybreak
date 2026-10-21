# Skybreak Agent Field Lab

An interactive HTML explanation and tuning workbench for your real Rift Warden and Relay agents. Everything runs locally; no packages need installing.

## Open it

With Python 3 installed, run from PowerShell:

```powershell
python F:\UnityProjects\Skybreak\Tools\AIWorkbench\server.py
```

Then open **http://127.0.0.1:8765**. Keep that terminal running; Ctrl+C stops it. `Start-Workbench.ps1` runs the same command. Port already in use usually means a copy is running; open the URL first.

You can also open `index.html` directly for an offline preview with the included defaults. Direct file mode cannot save to Unity or read C# sources. JSON export/import still works.

## Four sections

1. **Rift Warden arena** — watch Auto demo, or switch it off: WASD move, Shift sprint, mouse aim, hold click shoot, R reload, Space double jump. Aim at the arms first, then the exposed core. The rifle has 20 rounds, 80 spare, 18 damage and a 2.2-second reload. Training buttons break an arm or replenish the preview player only.
2. **Relay decisions** — change health, range, threat visibility, cooldown readiness and route availability. See the selected action, eligible utility scores and explanation. Relay has no arena simulation.
3. **Tune the real AI** — adjust existing behaviour, difficulty and performance parameters. Changes affect the browser preview immediately. **Save to Unity** explicitly applies them to your project.
4. **How the IS works** — the perception, utility scores, action states, three boss phases, teleport rules and navigation explained in plain language.

## The real connection

```text
HTML controls → local Python bridge → Assets/Resources/AI/AgentTuning.json
                                           ↓
                              AgentTuning.cs validates/loads
                                           ↓
                    RiftWardenBrain + RelayBrain + NavMeshMotor
```

The browser simulates the rules in JavaScript. It **does not run C# or Unity**. The C# agents themselves now read the same JSON, so saved tuning really changes their behaviour in Unity.

- In Editor Play Mode, the loader checks the JSON file about once per second when agents read it. Existing cooldowns finish normally. Restart Play Mode or reset the encounter/support agent to apply health changes.
- In a built game, the loader reads the packaged Resources asset once. Rebuild to distribute new defaults.
- Save rejects invalid values and stale revisions (another editor changed the file). Reload from Unity before applying another edit after a conflict.
- The bridge retains one previous config at `.previous-config.json`. Import it to inspect/restore it, then save. **Restore defaults in preview** is another way to reset: it does not write until Save to Unity.
- Only the shared configuration can be written. The C# source panel reads four allowlisted project files. The bridge listens only on `127.0.0.1`, requires a same-origin session token for saving, and performs atomic writes.
- No scenes, map assets, player scripts, Git commits or pushes are changed by the workbench's Save button.

## Useful tuning experiments

| Experiment | Controls | What to observe in Unity |
| --- | --- | --- |
| Rear attack timing | Warden rear delay, teleport cooldown | First close rear arrival, pause, second current-rear arrival, one immediate strike. Default delay remains **1 second**. |
| Boss attack preferences | Cannon base/weight, blade utility, teleport utility | Highest eligible score wins; a broken arm permanently disables its abilities. |
| Support priorities | Relay healing threshold/base/weight, retreat threshold/utility | Low player health favours healing; low Relay health plus recent danger enables retreat. |
| Less expensive sensing | Warden sight/decision intervals; Relay sensing interval | Longer intervals reduce query frequency but delay reactions. |
| Navigation cost | Replan interval, stuck timeout | Fewer path requests versus slower responses to blocked routes. |

This is manual tuning, not automatic learning or an optimizer that guarantees the best values. The work-rate labels estimate checks per second, not FPS. Use the Unity Profiler to measure actual CPU cost and test doors, cover, obstructed teleports and frame timing.

The browser uses simplified 2D movement, cover, hit shapes and animation timing. Navigation settings affect Unity's motor; the preview has no NavMesh and does not evaluate their performance. Health percentages in Relay's scenario are normalized inputs, not a live connection to the companion's current health.

## Editing or extending the rules

- `Assets/Scripts/AI/RiftWarden/RiftWardenBrain.cs`: boss scoring, action states, attack windows and phase transitions.
- `Assets/Scripts/AI/Relay/RelayBrain.cs`: support sensing, scoring, healing, defence and retreat.
- `Assets/Scripts/AgentMovement/NavMeshMotor.cs`: Unity path validation, replan scheduling and stuck handling.
- `Assets/Scripts/AI/Shared/AgentTuning.cs`: shared loader and Editor hot reload.
- `Assets/Scripts/AI/Shared/AgentTuningData.cs`: typed fields, defaults and validation bounds.

To change an existing value, use the workbench or JSON. To add a new tuning field, add its metadata to `build_tuning.py`, regenerate with `python Tools/AIWorkbench/build_tuning.py`, add the field to the existing `AgentTuning.json` (the generator preserves it), then consume it in C#. Update `engine.js` if the preview should model it. The generator refreshes `schema.json`, `defaults.json`, `data.js` and `AgentTuningData.cs`; its integration patches are guarded against reapplication. `baseline/` preserves the three scripts from before this workbench integration.

To add an ability or change formulas, edit the C# brain directly. A slider cannot create a new state or algorithm. Keep the corresponding browser explanation/simulation synchronized with any such change.

## Verification — 13 October 2026

- `node Tools/AIWorkbench/check_engine.js`: Relay choices, reposition/disabled cases, broken-arm eligibility, protected core, blocked rear cancellation, finite magazine and complete default three-phase encounter passed.
- `python Tools/AIWorkbench/check_server.py`: isolated temporary-config tests passed for valid saving, stale-write conflict, invalid parameters, origin and token rejection.
- Unity compiled the integration. A browser save changed rear delay from 1 to 1.25; `CheckWorkbench.cs` read **1.25 from the real boss brain in Play Mode**, confirmed both brains share the loaded data and checked invalid numeric rejection. The browser then restored 1; Unity confirmed it. Evidence: `verification.txt`.
- Browser UI checked: arena, Relay Retreat scenario, live tuning save/restore and real C# source loading.

This verifies the configuration link and selected decision rules. It is not a complete Unity encounter regression or measured performance benchmark. Play-test the real game after making substantial tuning changes.
