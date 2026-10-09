# Next steps after the cavern environment checkpoint

Reviewed 9 October 2026 against the current main scene and the brief requirements recorded in `ProjectPlanning/PROJECT_BRIEF.md` in the parent workspace.

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

Integrate player health, shooting, damage, death/restart and basic encounter progression. The IS brief refers to an FPS environment; agree a first-person gameplay mode before treating the current exploration camera as the final assessed view. Add the planned double jump before tuning boss attacks that rely on it.

## 3. Agree the shared IS contracts

Keep the flow: perception/world state -> agent decision -> path planner -> waypoint movement -> animation.

Agree world-space waypoint data, path cancellation/replanning, blocked-path notifications, damage/healing interfaces, team identity, perception inputs and door/world-state events. Agent movement and animation are separate from each member's decision logic. Navigation alone is not the whole intelligent-agent implementation.

## 4. Build one agent end to end first

Use Relay as the user's first agent: follow/reposition, perceive player health and threats, choose heal/defend/retreat actions, apply range/cooldown constraints, and handle unreachable routes. Show its decisions in a debug overlay and document why the action was chosen. Integrate the other members' independently chosen agents using the same contracts.

## 5. Add Rift Warden after shared systems work

Implement separate arm/core damage targets, arm removal and chest exposure, the warned double-teleport attack, jump/dodge timing, attack commitment and recovery, then the exposed-core slam phase. Choose valid landing points and handle interrupted/dead/blocked states. The Blender model/animations do not implement this AI automatically.

## 6. Validate the final version

Test all assigned agents operating together, closed/open doors, blocked/unreachable paths, death/restart and boss progression. Build a standalone version early and measure it on the weakest teammate laptop. Preserve individual implementation history and explain actual contributions and AI-assisted/downloaded asset provenance accurately.

Student 3 creates her own two original models with sources and evidence; Student 1 can import/integrate them. Each member's GV duty remains separate from their distinct IS-agent responsibility.

The environment preparation is now verified locally. The gameplay and AI behaviors above remain a plan; they are not established as complete by the world bakes.
