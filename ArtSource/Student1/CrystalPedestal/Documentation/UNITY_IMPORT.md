# Unity handoff

This ZIP contains a Blender source and FBX. It does not automatically replace the existing map prefab or include a Unity prefab with GUIDs.

1. Copy `Exports/CrystalPedestal.fbx` into `Assets/Models/Custom/CrystalPedestal/`. Keep the blend source outside Assets, for example `ArtSource/Student1/CrystalPedestal/Source/`.
2. Select the FBX in Unity. Use Scale Factor 1 and Convert Units enabled. Disable Import Animation. Apply and inspect its measured height (2.63m at scale 1).
3. Create four materials under `Assets/Materials/Environment/CrystalPedestal/` using Universal Render Pipeline/Lit.
4. Assign materials by mesh: `01_StoneBase` to StoneDark; `02_TopSlab` to StoneLight; `03_BrassSocket` to Brass; `04_FacetedCrystal` to cyan Crystal.

| Material | Base colour | Metallic | Smoothness |
| --- | --- | --- | --- |
| StoneDark | #203743 | 0 | .35 |
| StoneLight | #657E88 | 0 | .35 |
| Brass | #D7A66A | .5 | .47 |
| Crystal | #76E7E3 | .2 | .55 |

FBX material conversion varies; reassign URP materials if imported materials are incorrect or pink. Enable cyan Emission on the crystal. Blender's material node graph and light settings are not transferred as a complete Unity lighting setup. Glow around the crystal also needs Bloom in Unity; emission alone can look bright without a halo.

5. Drag the FBX into a test scene at position Y=0. Create an empty parent if you need a distinct collider or light object. Keep the ground-centred root.
6. Add a BoxCollider to the stone base only, or a compound collider if other parts should block the player. Avoid a tall bounding box enclosing empty space around the crystal.
7. Drag the assembled object into `Assets/Prefabs/Environment/Decoration/Custom/CrystalPedestal.prefab`.
8. Test it beside the current crystal pedestal. Its authored height is 2.63m; uniform scale approximately 1.027 gives 2.7m, matching the current decoration placement height.

To replace existing pedestals later, preserve each instance's position, rotation and scale. Compare appearance and collision before saving the main scene. No replacement has been performed by this ZIP delivery.

Commit source, exported model, new Unity materials/prefab and their generated .meta files under your own identity when requested. Preserve an honest description of how the model was created and your own changes.
