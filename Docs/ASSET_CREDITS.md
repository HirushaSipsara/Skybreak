# External asset credits and provenance

## Stone Floor — 4engine

The downloaded Stone Floor kit is credited to **4engine**, as identified in the user's supplied download-page screenshot. Original supplied files are under `resources/Stone Floor/`; the prepared Blender source is in `ArtSource/FloorKit/`. Imported FBX assets are under `Assets/Models/Environment/StoneFloor/`, and the original floor textures are under `Assets/Textures/Environment/StoneKit/StoneFloor/`.

These meshes and their slab-based variations remain externally sourced. Metre conversion, repaired materials, changed UV variations and Unity map assembly do not turn them into Student 3's original models.

The exact download-page URL and its license text were not included with the local files. Do not describe this kit as CC0 or apply a new open-source license to it. Keep the original source/license record when available and review its terms before publicly redistributing raw assets. The current team repository is private.

## Poly Haven tiled_floor_001

Earlier source metadata is retained in `ArtSource/FloorKit/polyhaven-info.json` and `polyhaven-files.json`. The handbuilt map README records that these ceramic textures are not used in the current stone map.

## Map decoration kit — Kenney and Quaternius

Retrieved 2026-10-04. Kenney Factory Kit 3.0, Space Station Kit and Modular Dungeon Kit 1.0, plus selected Quaternius Ultimate Modular Ruins models, are externally sourced CC0 assets. Official links, creator page snapshots, original Kenney licences and download hashes are under `resources/MapDecorationKit/`; see its `SOURCES.md` for provenance.

42 selected models are imported under `Assets/Models/Environment/Decoration/`, with palette textures under `Assets/Textures/Environment/Decoration/`, URP materials under `Assets/Materials/Environment/Decoration/` and prepared prefabs under `Assets/Prefabs/Environment/Decoration/`. Importing, remapping materials, preparing collision and arranging these assets are World Builder tasks; these downloads do not constitute Student 3's original modelling work. See `Docs/GV/LevelDesign/DECORATION_KIT.md`.

## Additional dungeon assets

KayKit Dungeon Pack Free 1.1 (Kay Lousberg), Quaternius Modular Dungeons, RGSDev Free Modular Low Poly Dungeon and MrScientist Low-Poly Modular Dungeon v0.3 were obtained from the creators' free CC0 distributions on 2026-10-04. Original downloads are local and ignored under `resources/DungeonAssetLibrary/`. Source URLs, exact contents and licence records are described in `Docs/GV/LevelDesign/DUNGEON_ASSET_LIBRARY.md`. Imported derivatives use `KayKit_Dungeon`, `Quaternius_ModularDungeons`, `RGSDev_Dungeon` and `MrScientist_Dungeon` subfolders in the Models/Materials/Textures/Prefabs Environment Decoration trees. These remain externally authored models.

## Original team assets

Student 3's proposed SecurityRobot and AccessConsole have folder scaffolding only at this baseline. Record her authorship, original source files, modelling evidence and any externally sourced references/maps when the models are created.

## Cavern environment kit — AI-assisted world-building

The rock variants, stone entrance arch and seamless mineral colour/normal maps in `ArtSource/Student1/CavernKit/` were generated with Codex assistance in Blender on 2026-10-09. Editable `.blend` source, FBX exports and the generation script are retained. They support Student 1's environment work and should be described as AI-assisted assets. They are not downloaded models or evidence of Student 3's independent from-scratch modelling. Imported derivatives are under the Environment Dungeon Cavern mesh folder and corresponding Environment Decoration Custom Cavern prefab, material and texture folders. See `Docs/GV/LevelDesign/CAVERN_BACKGROUND.md`.

## Unity template/configuration provenance

The project retains assets/configuration from its Unity URP template. `.gitignore` and `.gitattributes` retain their upstream source/license comments. These assets are separate from original student models.
