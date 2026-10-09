# Skybreak crystal pedestal

Editable Blender asset recreating the pedestal in the supplied map screenshot: dark stone base, blue-grey top slab, brass socket and cyan faceted crystal.

## Contents

- `Source/CrystalPedestal.blend`: Blender 5.1.2 project; four separate asset meshes, editable bevel modifiers and a separate preview-stage collection.
- `Source/BuildCrystalPedestalPackage.py`: the generation script used to make this package.
- `Exports/CrystalPedestal.fbx`: selected asset meshes and a ground-centred root, excluding preview lights/camera/ground; Y-up export for Unity.
- `Previews/`: actual Blender renders, not evidence of manual modelling.
- `Documentation/`: manual modelling instructions, Unity import notes, provenance and validation reports.

Open the blend file in Blender. Expand `01_ASSET / Export these meshes`. Select a part and press Tab to edit its mesh. The preview stage is hidden in the modelling viewport and available for F12 rendering.

The included generation script is optional. It rebuilds files within this package and must run in a separate Blender background process with `--factory-startup --python Source/BuildCrystalPedestalPackage.py`. It refuses to run inside an interactive Blender session to protect open work. Use the source file or manual guide for ordinary editing.

Asset dimensions: 1.18m wide, 1.18m deep, 2.63m high. Four meshes; 140 evaluated triangles with bevels. Materials use flat colours; no external texture files are required. The crystal's bottom tip extends inside its socket intentionally.

## Authorship

This package was generated with AI assistance. It is not a model manually created by the student and is not evidence that someone completed a from-scratch modelling requirement. Use the modelling guide to build your own version, or make and document your own changes, and describe those contributions accurately. Whether an assisted model satisfies an assignment requirement depends on that brief and instructor guidance.

Suggested description: "AI-assisted custom crystal pedestal for Skybreak. My own work: [list the modelling, materials or integration changes you personally completed]."

No Unity scene was changed and no Git commit or push was made while preparing this package.
