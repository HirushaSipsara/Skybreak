# Scene organisation

## Active project scenes

| Scene | Purpose | Default build |
| --- | --- | --- |
| `Assets/Scenes/GameLevel/SkybreakMain.unity` | Current main game map; continue world building here | Enabled, first |

The two earlier demo scenes were removed on 11 October 2026 at the user's request to retain the finished map. They and their previous dependencies remain recoverable from Git history. Only `SkybreakMain` is in Build Settings. The locally excluded combat lab and its scripts remain available for current shooting and boss-phase tests.

The main map retains the stone floor, walls, side rooms and current Arc explorer. Moving/renaming it does not implement the remaining combat, autonomous agents or progression systems.

Student 2 creates a lab scene under `Assets/Scenes/Tests/Student2Interactions/`; Student 4 creates one under `Assets/Scenes/Tests/Student4Movement/`. These folders are ready; their lab scenes have not been built yet. MainMenu is also a future scene folder.

## Preserved identity and references

All three scenes were moved with Unity's AssetDatabase. Their file contents and original metadata GUIDs were preserved:

- Main: `d3fb248ff81d58640a05f578764ee8ce`.
- Last Light demo: `dcb9e430ac71bbe4c83c500368e08bb6`.
- Descent demo: `ef59766c98721aa4587134d144ebeb48`.

Supporting assets are organised by function under Models, Materials, Textures, Prefabs, Scripts, Data, Shaders, Lighting and Navigation. The old `Assets/Skybreak/` kit folders have been retired. Editor tools are under `Assets/Tools/Editor/`, and `SkybreakScenePaths.cs` holds the shared scene paths. See [the folder map](FOLDER_STRUCTURE.md).

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

Old local scene/asset snapshots were removed during the 11 October size cleanup. Historical references to those deleted paths below and in older reports describe prior evidence; Git history is the recovery source for committed work. New authoring operations can create fresh snapshots. See `Docs/Maintenance/2026-10-11-cleanup/` for the deletion inventory and verification.

When restoring a local snapshot, stop Play Mode, preserve current work and replace the intended main-scene contents deliberately. Do not copy duplicate metadata into a second scene while the original is still present.

## Asset migration verification

On 3 October 2026, supporting assets were migrated into the functional folders. All 670 files were preserved and all 641 imported asset GUIDs remained unchanged. The main scene and both demos loaded with zero missing scripts, meshes or materials; scripts compiled without errors. The main scene entered Play Mode with Arc grounded and its camera/visual references assigned. Scene contents and build settings were unchanged by this asset migration. See FOLDER_STRUCTURE.md for the current asset paths.
