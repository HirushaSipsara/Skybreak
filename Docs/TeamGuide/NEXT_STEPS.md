# Next steps after the cavern environment checkpoint

Reviewed 9 October 2026 against the current main scene and the brief requirements recorded in `ProjectPlanning/PROJECT_BRIEF.md` in the parent workspace.

## Current state

The main scene is `Assets/Scenes/GameLevel/SkybreakMain.unity`. The level layout, textured floors/walls, cover crates, environmental props, manual holding-room doors, floor circles and cavern backdrop provide the environment baseline. The player currently supports third-person exploration.

Live Editor inspection found two real-time directional lights (one with soft shadows) and 20 real-time point lights without shadows, zero baked lightmaps, and no NavMeshSurface in the main scene. This is sufficient to preview the environment; the final lighting and initial navigation bake remain unfinished.

## 1. Finish the World Builder work

Lighting, texturing and the initial NavMesh bake belong to Student 1's GV role. Student 4 consumes navigation/path data for movement; that does not replace Student 1's initial world preparation.

Recommended small visual pass, not yet implemented:

- Chip selected upper wall edges with a few reusable broken-stone cap variants.
- Add shallow cracks and occasional recessed/missing upper stones.
- Blend selected wall tops/corners into the surrounding cavern rock.
- Place limited rubble against the wall base outside movement lanes and cover positions.
- Keep the main wall silhouette and lower collision surfaces dependable.
- Reserve violet accents for the boss chamber; keep warm lantern light and cool stone elsewhere.

After geometry stabilizes, prepare lightmap UVs/static receivers, bake environment lighting, and provide lighting for moving characters. Check lantern appearance, dark corners, camera views, door animation and weakest-laptop performance. Bake initial navigation for the intended agent size; verify rooms, entrances, cover routes and closed/open doors. Door-triggered route updates are a shared integration task.

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

This file is a plan. Wall weathering, lighting/navigation baking and the AI behaviors above are not established as complete by this checkpoint.
