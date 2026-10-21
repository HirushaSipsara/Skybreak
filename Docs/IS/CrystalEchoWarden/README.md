# Crystal Echo Warden — Sachini

The Crystal Echo Warden is a compact hovering enemy that protects a crystal. It estimates where an unseen intruder might be from attenuated sounds, follows a route, searches when evidence becomes uncertain, and balances combat, interception and retreat. A player can distract it with a decoy and break its line of sight during its attack wind-up.

Branch: `sachini-ISagent-crystal-echo-warden`. Base: `main` at `0c4da35`.

## What is delivered

- Approved refined Blender model, standalone source and FBX export: 30 mesh parts, 3,140 triangles.
- A 12-bone animation rig with eight clips for hovering, gliding, turning, retreat, attack, hit and defeat. See [Animations.md](Animations.md).
- Engine-independent C# graph, A*, acoustic search, Bayesian beliefs, utility decisions and constrained random-restart hill climbing.
- Unity adapters for perception, movement, damage, crystal alarms, footsteps and visual feedback.
- A standalone FPS lab that builds its layout and actors when Play Mode starts.
- Local regression tests and a Unity Editor smoke-check menu.

**Validation status:** Unity 6000.6.0f1 compilation, the scene smoke suite and automated Play Mode gameplay checks passed on 20 October 2026. The gameplay run covers controller movement, animation selection, door replanning, attack timing, cover/actor obstruction, death and five minutes with four Wardens. The 46 standalone checks and Blender rig/export checks are recorded separately. Rendering performance and integration with four distinct student agents remain to be assessed. See [Validation.md](Validation.md) for evidence, warnings and limits.

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
| Rigged animation source | `ArtSource/Sachini/CrystalEchoWarden/Source/CrystalEchoWarden_Animated.blend` |
| Animated preview | `ArtSource/Sachini/CrystalEchoWarden/Evidence/CrystalEchoWarden_Movement.gif` |
| Refined preview | `ArtSource/Sachini/CrystalEchoWarden/Evidence/CrystalEchoWarden_Refined.png` |
| Source FBX | `ArtSource/Sachini/CrystalEchoWarden/Exports/CrystalEchoWarden.fbx` |
| Unity model resource | `Assets/Resources/CrystalEchoWarden/CrystalEchoWarden.fbx` |
| Algorithms | `Assets/Scripts/AI/CrystalEchoWarden/Core/` |
| Unity components | `Assets/Scripts/AI/CrystalEchoWarden/Runtime/` |
| Unity smoke checks | `Assets/Editor/CrystalEchoWarden/WardenLabChecks.cs` |
| Automated Unity gameplay checks | `Assets/Editor/CrystalEchoWarden/WardenGameplayChecks.cs` |
| Standalone tests | `Tools/WardenTests/` |

[Theory and implementation](Implementation.md) · [Team integration](Integration.md) · [Validation and demo checklist](Validation.md) · [Development record](DevelopmentLog.md)
