# Skybreak size cleanup — 11 October 2026

Requested scope: retain the finished dungeon map and needed gameplay content; remove unused assets and obsolete copies. No commit or push was performed.

## Size

Measured file sizes, decimal units:

| Content | Size |
| --- | ---: |
| Before cleanup | 4.30 GB |
| After reopening Unity and rebuilding caches | 2.17 GB |
| Space saved at final measurement | 2.14 GB, approximately 50% |
| Imported `Assets` | 164 MB |
| Project sources, documentation and models excluding Library and Git | 451 MB |

Unity's generated cache grows during imports, search indexing and later builds. The measurement is a snapshot, not a permanent size limit. `before-size.json` and `after-size.json` contain exact bytes and times.

## Removed

- 1,493 unused imported files (approximately 94 MB before their metadata), through Unity AssetDatabase. Includes the two disabled earlier demo scenes, their unused meshes/materials/prefabs, unused downloaded asset variants and template tutorial content.
- Obsolete `Tools/SceneSnapshots` and `Tools/AssetSnapshots` copies: approximately 370 MB.
- Duplicate downloaded third-party archives and extracted model packs: approximately 188 MB. Attribution, licences, download hashes and source-page records retained.
- Two Blender `.blend1` backup files, approximately 37 MB; their current `.blend` sources retained.
- Unused Unity Assistant and inference packages, through Unity Package Manager. Custom Relay/Rift Warden code does not use these packages. Pipeline, AI Navigation, Input System, URP and other retained dependencies remain installed.
- Generated Library, logs and solution/project files while Unity and its import workers were closed. Unity was reopened and rebuilt its required caches successfully.
- One obsolete generated team-guide ZIP; editable guide documents retained.

Full inventories: `unused-assets.tsv`, `deleted-assets.txt`, `deleted-local-content.csv`, `deleted-generated-content.csv`.

## Retained and verified

- Finished `Assets/Scenes/GameLevel/SkybreakMain.unity`, environment props, floor/wall/cavern textures, crystal/portal materials and effects.
- Five baked lightmap atlases, 506 light probes and both navigation surfaces with NavMesh data.
- Relay and Rift Warden prefabs/scripts, 20 boss animation clips, damage parts, current combat materials and all 12 audio clips.
- Locally excluded player/rifle, shooting, reload, hit feedback, double jump and boss-phase testing scripts and lab scene. Local exclusions remain intact.
- Editable original Blender sources, Stone Floor source dependencies, project documentation/evidence, assignment ownership scaffolding and Git history.

All 373 retained on-disk asset files and metadata GUIDs matched their pre-cleanup SHA256/GUID audit after reimport. Main scene reopened after the fresh cache rebuild with zero missing scripts, meshes, materials or shaders. Unity compiled and the removed packages were absent from the resolved package list.

The Play Mode smoke check after asset/package removal verified the runtime player/rifle/muzzle, both agents, audio references, 20+80 ammunition, shooting and reload start. After cache rebuilding, scene integrity and compilation were verified again; this is not a full boss encounter, standalone build or performance test.

Evidence: `retained-hashes.csv`, `retained-integrity.json`, `scene-verification.txt`, `gameplay-verification.txt`.

## Sharing and recovery

Do not include `Library`, `Temp`, `Logs` or `.git` in a project ZIP. Retain `Assets`, `Packages`, `ProjectSettings`, and needed source/documentation folders. Keep `Assets/LocalCombatLab` in local test handoffs when current player controls are needed; it remains intentionally excluded from Git.

Deleted tracked demo/import files remain recoverable from Git history. Removed ignored local snapshots and duplicate downloads are no longer stored. Original `.blend` sources and current retained assets remain available. Licence copies are under `resources/DungeonAssetLibrary/Licences` and `resources/MapDecorationKit/Licences`; historical download metadata is beside them. Copies are also retained in this report's `Licences/` folder for version-controlled attribution.
