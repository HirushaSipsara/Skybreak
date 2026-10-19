# Crystal Echo Warden — Sachini

The Crystal Echo Warden is a compact hovering enemy that protects a crystal. It estimates where an unseen intruder might be from attenuated sounds, follows a route, searches when evidence becomes uncertain, and balances combat, interception and retreat. A player can distract it with a decoy and break its line of sight during its attack wind-up.

Branch: `sachini-ISagent-crystal-echo-warden`. Base: `main` at `0c4da35`.

## What is delivered

- Approved refined Blender model, standalone source and FBX export: 30 mesh parts, 3,140 triangles.
- Engine-independent C# graph, A*, acoustic search, Bayesian beliefs, utility decisions and constrained random-restart hill climbing.
- Unity adapters for perception, movement, damage, crystal alarms, footsteps and visual feedback.
- A standalone FPS lab that builds its layout and actors when Play Mode starts.
- Local regression tests and a Unity Editor smoke-check menu.

**Validation status:** 37 core checks passed, including 150 randomized graph comparisons and 1,200 combined cycles across four independent cores. All 16 agent/editor C# files passed syntax parsing. Unity is unavailable on the development machine; Unity API compilation, Play Mode, FBX appearance and full team gameplay are still unverified. See [Validation.md](Validation.md).

## Open the lab

1. Open this branch's project folder in Unity **6000.6.0f1**, as specified by `ProjectSettings/ProjectVersion.txt`. Use this worktree, not another checkout of the project.
2. Allow package restoration and asset import. Resolve any Console compilation errors before playing.
3. Use **Skybreak > Crystal Echo Warden > Open FPS lab**, or open `Assets/Scenes/Tests/CrystalEchoWarden/CrystalEchoWardenLab.unity`.
4. Press Play. The saved scene contains a bootstrap object; the arena, camera, player and Wardens are created at runtime.
5. Use **Skybreak > Crystal Echo Warden > Run Play Mode smoke checks**. The check resets the lab and writes its actual results to `Docs/IS/CrystalEchoWarden/UnitySmokeResults.txt`.

| Input | Action |
| --- | --- |
| WASD / mouse | Move / look |
| Left Shift / Space | Sprint / jump |
| Left mouse | Fire test weapon |
| Q | Emit a decoy sound at the aimed position, with a cooldown |
| F | Disrupt the crystal when nearby |
| F2 | Open or close the centre door |
| F3 | Trigger a diagnostic crystal alarm |
| F4 | Switch between one and four Wardens; resets round |
| R | Reset health, crystal, evidence and Wardens |
| Escape / click | Release / recapture mouse |

The lab weapon is a ray-based test weapon with a crosshair. It does not include the separate gun/Arc Explorer assets from the modelling branch. The lab's crystal is a simple objective marker. The existing team gameplay scene is unchanged.

## Files to inspect

| Purpose | Project-relative location |
| --- | --- |
| Clean Blender source | `ArtSource/Sachini/CrystalEchoWarden/Source/CrystalEchoWarden_Standalone.blend` |
| Refined preview | `ArtSource/Sachini/CrystalEchoWarden/Evidence/CrystalEchoWarden_Refined.png` |
| Source FBX | `ArtSource/Sachini/CrystalEchoWarden/Exports/CrystalEchoWarden.fbx` |
| Unity model resource | `Assets/Resources/CrystalEchoWarden/CrystalEchoWarden.fbx` |
| Algorithms | `Assets/Scripts/AI/CrystalEchoWarden/Core/` |
| Unity components | `Assets/Scripts/AI/CrystalEchoWarden/Runtime/` |
| Unity smoke checks | `Assets/Editor/CrystalEchoWarden/WardenLabChecks.cs` |
| Standalone tests | `Tools/WardenTests/` |

[Theory and implementation](Implementation.md) · [Team integration](Integration.md) · [Validation and demo checklist](Validation.md) · [Development record](DevelopmentLog.md)
