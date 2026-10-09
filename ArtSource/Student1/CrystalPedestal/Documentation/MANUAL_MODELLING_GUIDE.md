# Build the pedestal yourself in Blender

This guide lets you practise the actual modelling steps. Following it means you perform the steps; the generated file itself is not proof of those steps. Keep genuine progress screenshots and state that you used a reference/guide.

## 1. Setup

Create your own new Blender file. Remove the default cube if starting fresh. Scene Properties > Units: Metric, Unit Scale 1. Blender uses Z for height. Use the Item panel (N) to set Dimensions and Location. Apply Scale with Ctrl+A > Scale after changing dimensions.

## 2. Stone base

Shift+A > Mesh > Cube. Rename `01_StoneBase`.

- Dimensions X=.95, Y=.95, Z=1.14 metres.
- Location X=0, Y=0, Z=.57.
- Ctrl+A > Scale.
- Add a Bevel modifier, Width .012m, Segments 1. Keep it editable.

Give it a dark teal stone material. Base colour approximately hex `203743`, Roughness .65, Metallic 0.

## 3. Top slab

Add another cube; rename `02_TopSlab`.

- Dimensions 1.18, 1.18, .22m.
- Location 0, 0, 1.16m.
- Apply Scale; bevel .012m, one segment.
- Material approximately `657E88`, Roughness .65, Metallic 0.

The slab slightly overlaps the body, so no visible seam opens between them.

## 4. Brass socket

Add cube `03_BrassSocket`.

- Dimensions .64, .64, .13m.
- Location 0, 0, 1.34m.
- Apply Scale; bevel .008m, one segment.
- Material approximately `D7A66A`, Roughness .53, Metallic .5.

## 5. Faceted crystal from scratch

Add a circle mesh with **4 vertices**, radius .27m, Fill Type Nothing, at world Z=1.93m. Set these parameters in the Add Circle panel immediately after adding it (or F9).

Enter Edit Mode. The four vertices form the broad middle of the crystal.

1. Select the whole ring, press E, then Z, type `.7`, Enter. Scale the new ring to zero with S, 0, Enter.
2. Merge the four coincident top vertices: M > By Distance. This makes a pointed upper pyramid.
3. Deselect. Select the four original middle vertices. With X-ray enabled, Box Select a narrow horizontal band around the middle from Front View, making sure the top tip is excluded.
4. Press E, Z, type `-.7`, Enter; then S, 0, Enter; M > By Distance. This forms the lower tip.
5. Select all, Shift+N to recalculate outside normals. Use Shade Flat.

The finished mesh should have six vertices and eight triangular faces. Rename it `04_FacetedCrystal`. Its tips are at Z=1.23 and 2.63m; the lower tip is embedded in the brass socket.

Create material `Crystal_Cyan`: colour approximately `76E7E3`, Metallic .2, Roughness .45. Set Principled BSDF emission to cyan with Strength .9. Emission makes it appear bright; it does not necessarily illuminate nearby surfaces in realtime. Add a modest point light separately in Unity if needed.

## 6. Origin and organisation

Put the four objects into one collection. Add an Empty at the world origin (0,0,0), name `CrystalPedestal_ROOT`, select the four meshes, select the Empty last, then Ctrl+P > Object (Keep Transform). Keep this root at the floor centre for convenient placement in Unity.

Save your own blend file under a distinct name. Document actual changes you make: proportions, facets, engraving, colours or a different socket shape. Record real intermediate work; don't present the supplied render as a screenshot of your own construction process.

## 7. FBX export

Select the four meshes and their root. File > Export > FBX:

- Selected Objects on; export Mesh and Empty.
- Scale 1; Forward -Z; Up Y; Apply Unit on; Apply Modifiers on.
- Animation off. Exclude preview camera, lights and ground.

Follow `UNITY_IMPORT.md` for material setup. Test your own export before using it in the team scene.
