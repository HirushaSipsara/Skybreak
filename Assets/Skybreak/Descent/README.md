# Skybreak — preserved Descent demo assets

Open `Assets/Scenes/Demos/SkybreakDescentDemo.unity`. The main game map is `Assets/Scenes/GameLevel/SkybreakMain.unity`; this folder contains shared explorer components and the earlier demo's supporting assets.

This is the native Unity environment pass for the continuous dungeon map agreed in the HTML prototype. The scene contains the loading court, winding service passage with ambush bays, generator hall, inner checkpoint and circular Aurora reactor chamber. Gameplay anchors mark encounter, spawn and checkpoint positions for later combat integration.

## Walk around as Arc

Press Play and click the Game view. The scene now starts with the controllable Arc explorer at the entrance, using a third-person camera.

| Input | Action |
|---|---|
| WASD / arrow keys | Move Arc relative to the camera |
| Mouse | Look around |
| Shift | Sprint |
| Space | Jump |
| Escape | Release the cursor |
| Left click after releasing cursor | Resume control |

Arc uses a CharacterController capsule with gravity and physical wall/prop collision. The camera pulls forward near walls and hides the character when it is too close. Gates stay open for exploration and have a `DescentGate.SetClosed` hook for future encounter scripting. This pass adds movement only; combat and encounter systems are pending.

`Prefabs/ArcExplorer.prefab` contains the stylized character, simple procedural walking animation and controller. The camera is assigned by the scene; a fresh prefab instance falls back to `Camera.main`. The `DescentPlayer` layer (8) separates the character from camera obstruction queries.

The earlier `DescentMapPreview` component and its five viewpoints remain on the scene camera, disabled to avoid competing camera movement. To return to the flying inspection mode, disable Arc's `DescentCharacterController` and enable `DescentMapPreview`. In that mode, F1–F5 select viewpoints, RMB looks, WASD flies, Q/E changes height and Shift increases speed.

## Assets and visuals

- Native Unity scene, meshes, prefabs, materials and collision geometry.
- Beveled stone blocks, copper equipment, glowing crystal pedestals and an animated reactor portal.
- URP HDR lighting with modest bloom, ACES tonemapping and color grading.
- Geometry combined by nearby chunks and shared materials, enabling Unity to cull distant chunks.
- Navigation baked with the default Humanoid agent using physics colliders. Route validation is recorded in `Reports/MapValidation.txt`.
- `Data/DescentMapLayout.json` preserves the approved HTML map layout as build input; the Unity scene has no browser dependency.

The scene uses `Settings/DescentBalancedURP.asset`: 2x MSAA, 1024 main light shadow map, 40 m shadow distance and no point light shadows. This asset is assigned to project graphics and the active quality tier. Actual performance on teammates' Intel graphics laptops still needs measurement.

## Editing

The hierarchy groups the environment, reusable props, collision, gameplay anchors, lighting and camera viewpoints. Edit the scene directly in Unity. `Editor/DescentMapBuilder.cs` creates the initial assets; its menu command refuses to replace an existing Descent scene.

The earlier city prototype is preserved at `Assets/Scenes/Demos/SkybreakLastLightDemo.unity`. Both demos are excluded from the default build list; `SkybreakMain` is the enabled main game scene.
