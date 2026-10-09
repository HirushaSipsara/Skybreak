# Local dungeon asset library

Downloaded 2026-10-04 for the World Builder. Original downloads and extracted files are under `resources/DungeonAssetLibrary/`, outside Unity's imported Assets tree. This folder and the older `resources/MapDecorationKit/` are explicitly ignored by Git. Clones do not receive these libraries. Existing imported dependencies under Assets are intentionally not ignored.

## Downloaded packs

| Creator / pack | Included | Official source / licence |
| --- | --- | --- |
| Kay Lousberg / KayKit Dungeon Pack | Complete Free 1.1 ZIP and extracted FBX, Unity-oriented FBX, OBJ, glTF, textures and samples | https://kaylousberg.itch.io/kaykit-dungeon-pack — CC0 |
| Quaternius / Modular Dungeons | All 48 FBX models, creator's licence and preview | https://quaternius.com/packs/modulardungeon.html — CC0 |
| RGSDev / Free Modular Low Poly Dungeon | Official ZIP, licence, assets FBX and sample FBX | https://opengameart.org/content/free-modular-low-poly-dungeon-pack — CC0 |
| MrScientist / Low-Poly Modular Dungeon | Latest listed v0.3 Unity package, extracted original assets and metadata | https://mrscientist.itch.io/3d-low-poly-modular-dungeon — CC0 according to creator's page |

Original author licences are retained where supplied. These external models do not count as student-created models from scratch. No paid KayKit Extra or Source edition was purchased or downloaded.

## Where to browse

- KayKit: `resources/DungeonAssetLibrary/Packs/KayKit_Dungeon_Free_1.1/KayKit_Dungeon_Pack_1.1_FREE/Assets/fbx(unity)/` is the preferred Unity export set. The ordinary FBX directory contains alternative exports of the same models; do not import both.
- Quaternius: `resources/DungeonAssetLibrary/Packs/Quaternius_ModularDungeons/FBX/`. Includes torches, cobwebs, horse statue, pedestals, columns, wall covers, banners, doors, crates and barrels.
- RGSDev: `resources/DungeonAssetLibrary/Packs/RGSDev_ModularDungeon/`; its two FBX files are scene/model collections, not two unique props.
- MrScientist: `resources/DungeonAssetLibrary/Packs/MrScientist_LowPolyDungeon_v0.3/Assets/`; original Unity paths are preserved under the local pack folder. Torches, candles, chains/hooks and debris are useful candidates. Keep the entire original package outside Assets until selectively reviewed; its old materials/effects may need URP adjustments.
- `MODEL_INVENTORY.csv` lists all included model file paths, formats and sizes.
- `DOWNLOAD_HASHES.csv` records hashes and sizes of downloaded archives and Quaternius files.

There are 527 FBX files across the four new packs: KayKit 422 (211 regular + 211 Unity-oriented alternatives), MrScientist 55, Quaternius 48 and RGSDev 2 collection FBX files. File counts are not unique-prop counts.

## Unity prefab integration

The user subsequently requested these assets in `Assets/Prefabs/Environment/Decoration/`. Imported folders are `KayKit_Dungeon`, `Quaternius_ModularDungeons`, `MrScientist_Dungeon` and `RGSDev_Dungeon`. Models, textures and URP materials use the corresponding functional Decoration folders. Only KayKit's Unity-oriented exports are imported; the duplicate standard FBX set is excluded. The RGSDev sample scene is excluded, and the assets collection is separated into individual prop prefabs.

`Tools/ImportDungeonLibrary.cs` prepares models and prefabs via the Unity Editor. `Docs/GV/LevelDesign/Reports/Decoration/DungeonImportReport.md` records prefab dimensions and collision choices. Prefabs retain authored scale with bottom-centred pivots. Cover/furniture uses simple boxes; architectural meshes use static MeshColliders to avoid blocking arch openings; small decoration may have no collider. Doors/traps are visual assets with no interactive or damage behaviour added. MrScientist's particle-effect prefabs were not imported; this step prepares its model meshes and palette material only.

The existing main map may have unsaved user edits. Integration uses a temporary preview scene and does not save or overwrite the user's map. Imported dependencies and their .meta files are trackable; original download libraries remain ignored. Scale, camera clearance, navigation and combat need checking after placement.

Completed prefab integration: KayKit 211, Quaternius 48, MrScientist 55 and RGSDev 198 individual collection children, totalling 512 new prefabs. A live Unity check found zero missing meshes, invalid/missing URP materials or missing MeshCollider meshes; 330 collider components were present. The active main scene retained its pre-existing dirty state. The first 42 decoration prefabs remain available, making 554 prefabs in the combined Decoration library. Counts represent prefab assets, including variants, not unique gameplay objects. No new scene placement, visual/performance testing, commit or push occurred.

## Download verification and next step

ZIP CRC checks passed. The Unity package was read as a gzip/tar archive and reconstructed from its pathname entries after path validation. The 48 Quaternius FBX headers and PNG preview header were checked. This establishes download integrity, not Unity compatibility or visual/performance quality. No new pack was imported or placed into the current scene during this download task.

For the first placement pass, choose dungeon lamps, cobwebs, a few pillars, doorway trims and crates. Match their materials to the existing stone environment. Keep the continuous route, holding-room entrances and boss dodge lanes open. Imported assets must go into the functional Models/Materials/Textures/Prefabs folders with their Unity-generated .meta files tracked, so teammates receive required scene dependencies. Downloaded libraries stay ignored.
