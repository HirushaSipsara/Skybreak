# Next steps after the cavern environment checkpoint

Updated 10 October 2026 against the current main scene and the brief requirements recorded in `ProjectPlanning/PROJECT_BRIEF.md` in the parent workspace.

## Current state

The main scene is `Assets/Scenes/GameLevel/SkybreakMain.unity`. The level layout, textured floors/walls, cover crates, environmental props, manual holding-room doors, floor circles and cavern backdrop provide the environment baseline. The player currently supports third-person exploration.

The world-preparation passes completed chipped wall tops, shallow fractures, glowing crystals and a transparent final return-point effect. The latest lighting bake has five atlases and 506 probes, and the main-map NavMesh has 765 triangles. Live tests passed all eight main-route legs, cover exclusion, both gates' closed/open/reclosed navigation states, physical doorway clearance and occupied-threshold protection. The return-point proximity and power controls also passed Play Mode checks. See `../GV/LevelDesign/WORLD_PREPARATION.md` and its reports. Hardware performance remains unmeasured.

## 1. Finish the World Builder work

Lighting, texturing and the initial NavMesh bake belong to Student 1's GV role. Student 4 consumes navigation/path data for movement; that does not replace Student 1's initial world preparation.

World pass status:

- Completed: 190 chipped upper wall sections, 20 shallow cracks and 17 small fallen stones beside walls.
- Completed: preserved doorframes, textures, lower wall geometry and existing cover.
- Completed: baked static lantern/fill lighting, one Mixed directional light and probes for moving objects.
- Completed: initial Medium Robot navigation with live holding-gate carving and route validation.
- Future optional art: additional rock/wall blending or passage arches. These are not needed to begin the IS implementation.

Rebake lighting/navigation whenever relevant static geometry changes. Validate actual agent sizes against the initial .45m-radius, 2.44m-height navigation type; larger actors may need their own bake. Weakest-laptop profiling and gameplay-triggered door-release/replanning remain integration tasks.

## 2. Establish the playable game loop

Student 2 integrates final player health, shooting, damage, death/restart and encounter progression. A separate locally Git-excluded `Assets/LocalCombatLab/OwnedAgentsLab.unity` now supplies temporary versions for Student 1's boss/Relay checks. It is not the final player implementation. The IS brief refers to an FPS environment; agree a first-person gameplay mode before treating the exploration camera as the final assessed view. Preserve the tested boss warning/jump windows when adding the final double jump.

## 3. Agree the shared IS contracts

Keep the flow: perception/world state -> agent decision -> path planner -> waypoint movement -> animation.

The owned-agent baseline now separates `DamageSink`, `NavMeshMotor`/`IAgentMotor`, `TimedClipPresenter` and decision components. See [the integration guide](../IS/AgentDesign/OWNED_AGENT_IMPLEMENTATION.md). Integrate the final player's health adapter, agreed planner/motor and door/world-state events. Agent movement and animation remain separate from each member's decision logic. Navigation alone is not the whole intelligent-agent implementation.

## 4. Build one agent end to end first

Relay is now implemented and placed in the main scene. Live lab checks passed autonomous following, healing and its cooldown, defence, low-health retreat, unreachable destinations and shutdown. Its utilities and decision reasons are visible in the lab overlay and documented in the integration guide. Replace its modular placeholder visual if desired; integrate the other members' independently chosen agents using the agreed contracts.

## 5. Add Rift Warden after shared systems work

Rift Warden is now implemented as Student 1's additionally chosen boss work. Its prefab includes independent arm/core hitboxes, all 20 imported clips, timed attacks, destruction/exposure/defeat and a separate wider arena NavMesh. The double teleport was revised to arrive 1.6m directly behind the player's current facing twice, one second apart, with one immediate strike on the second arrival. Live checks cover turning, blocked rear placement, jump avoidance, visible player/cannon shot pulses, shielding, arm removal, core death, interruption and core-phase landing safety. See `../IS/AgentDesign/Reports/OwnedAgents/RearStrikeChecks.txt`. Final player/objective integration and combat tuning with teammates remain.

## 6. Validate the final version

Test all assigned agents operating together, closed/open doors, blocked/unreachable paths, death/restart and boss progression. Build a standalone version early and measure it on the weakest teammate laptop. Preserve individual implementation history and explain actual contributions and AI-assisted/downloaded asset provenance accurately.

Student 3 creates her own two original models with sources and evidence; Student 1 can import/integrate them. Each member's GV duty remains separate from their distinct IS-agent responsibility.

The environment preparation is verified locally and committed in checkpoint `f900c0f`. The owned-agent implementation is saved locally and uncommitted; its evidence is separate from the world bakes. Final player systems, all four assessed agents together, end objective, standalone build and weakest-machine performance are not established as complete by these local checks.
