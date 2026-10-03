# Scene organisation

## Active project scenes

| Scene | Purpose | Default build |
| --- | --- | --- |
| `Assets/Scenes/GameLevel/SkybreakMain.unity` | Current main game map; continue world building here | Enabled, first |
| `Assets/Scenes/Demos/SkybreakLastLightDemo.unity` | Preserved earlier city/escort shooting prototype | Disabled |
| `Assets/Scenes/Demos/SkybreakDescentDemo.unity` | Preserved earlier dungeon environment/explorer demo | Disabled |

The main map retains the stone floor, walls, side rooms and current Arc explorer. Moving/renaming it does not implement the remaining combat, autonomous agents or progression systems.

Student 2 creates a lab scene under `Assets/Scenes/Tests/Student2Interactions/`; Student 4 creates one under `Assets/Scenes/Tests/Student4Movement/`. These folders are ready; their lab scenes have not been built yet. MainMenu is also a future scene folder.

## Preserved identity and references

All three scenes were moved with Unity's AssetDatabase. Their file contents and original metadata GUIDs were preserved:

- Main: `d3fb248ff81d58640a05f578764ee8ce`.
- Last Light demo: `dcb9e430ac71bbe4c83c500368e08bb6`.
- Descent demo: `ef59766c98721aa4587134d144ebeb48`.

Existing supporting assets remain in their established locations, including `Assets/Skybreak/Handbuilt/` and `Assets/Skybreak/Descent/`. The main scene no longer lives inside either asset-kit folder. Editor tools use the shared constants in `Assets/Tools/Editor/SkybreakScenePaths.cs`.

Checked in the connected Unity 6000.6.0f1 Editor on 3 October 2026: scripts compiled without errors; all three scenes loaded with zero missing scripts. The main scene entered Play Mode with Arc grounded at its starting position, with its camera and visual references assigned. This was a scene-organisation smoke check, not a full gameplay or standalone-build test. Scene contents and GUIDs were also compared against the pre-cleanup Git revision, with no changes to their contents or identity.

## Removed redundant scenes and recovery

Removed from Unity's imported Assets tree:

- The untouched template `SampleScene.unity`.
- The identical `SkybreakDescent_Preserved_20261002.unity` duplicate.
- Six `SkybreakStoneFloor_Before..._20261002.unity` snapshots: before rooms, colours, walls, wall textures, explorer and larger Arc.

The full pre-cleanup version remains in Git commit **`cfc629cd18934aedc5e83283266304e16bb6e71d`**. Browse that revision's old `Assets/Skybreak/Handbuilt/Scenes/` folder to recover an earlier stage. Recover into a temporary folder outside Assets first; coordinate any restoration of an old scene and its metadata to avoid duplicate GUIDs alongside the current map.

Historical reports retain the filenames/path names that existed when those checks were made; they are evidence records rather than instructions to open the old locations.

## Future authoring snapshots

World Editing tools take local safety copies under **`Tools/SceneSnapshots/`**, which is outside Assets and ignored by Git. Unity therefore does not import each snapshot as another scene. Normal saved work should be committed incrementally to Git.

When restoring a local snapshot, stop Play Mode, preserve current work and replace the intended main-scene contents deliberately. Do not copy duplicate metadata into a second scene while the original is still present.
