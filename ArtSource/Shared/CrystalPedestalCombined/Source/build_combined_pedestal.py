"""Combine the existing Student1 plinth and Sachini's Student3 crystal assembly.

Run in a separate Blender background process. Both source models are preserved.
"""
import bpy
import json
from pathlib import Path
from mathutils import Matrix, Vector

if not bpy.app.background:
    raise RuntimeError("Run in a separate background Blender process.")

project = Path(__file__).resolve().parents[4]
folder = Path(__file__).resolve().parents[1]
for name in ("Exports", "Previews"):
    (folder / name).mkdir(parents=True, exist_ok=True)
unity_folder = project / "Assets/Models/Custom/CrystalPedestalCombined"
unity_folder.mkdir(parents=True, exist_ok=True)
original = project / "ArtSource/Student1/CrystalPedestal/Source/CrystalPedestal.blend"
teammate = project / "ArtSource/Student3/CrystalPedestal/Source/CrystalPedestal.blend"
bpy.ops.wm.open_mainfile(filepath=str(original))
scene = bpy.context.scene
scene.name = "Combined Crystal Pedestal"
root = bpy.data.objects["CrystalPedestal_ROOT"]
root["provenance"] = "Shared derivative: original Student1 body/slab plus Sachini's Student3 stepped base, socket and crystal; combined with AI assistance."
root["source_models"] = str(original.relative_to(project)) + " + " + str(teammate.relative_to(project))

for name in ("03_BrassSocket", "04_FacetedCrystal"):
    bpy.data.objects.remove(bpy.data.objects[name], do_unlink=True)
lower = [bpy.data.objects["01_StoneBase"], bpy.data.objects["02_TopSlab"]]
asset_collection = root.users_collection[0]
with bpy.data.libraries.load(str(teammate), link=False) as (source, target):
    target.objects = [name for name in source.objects if name in {
        "Stone base", "Stone base bevel step", "Blue-grey plinth slab",
        "Brass socket base", "Socket inner recess", "Faceted cyan crystal",
        *[f"Brass retaining fin {i}" for i in range(4)]}]
upper = list(target.objects)
assert len(upper) == 10, "Unexpected teammate model structure."
for obj in upper:
    asset_collection.objects.link(obj)
bpy.context.view_layer.update()
# The stepped base sits exactly on the original slab; the crystal stays tall
# without making the assembled prop dominate the existing dungeon rooms.
placement = Matrix.Translation((0, 0, 1.27)) @ Matrix.Diagonal((0.85, 0.85, 0.75, 1))
for obj in upper:
    world = placement @ obj.matrix_world.copy()
    obj.parent = root
    obj.matrix_world = world
    obj["source"] = "Sachini / ArtSource/Student3/CrystalPedestal"
bpy.context.view_layer.update()

groups = {
    "Stepped stone": [o for o in upper if o.name in ("Stone base", "Stone base bevel step", "Socket inner recess")],
    "Upper slab": [o for o in upper if o.name == "Blue-grey plinth slab"],
    "Brass socket": [o for o in upper if o.name.startswith("Brass")],
    "Faceted crystal": [o for o in upper if o.name == "Faceted cyan crystal"],
}
parts = []
for name, objects in groups.items():
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    if len(objects) > 1:
        bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    parts.append(obj)

gem = next(o for o in parts if o.name == "Faceted crystal")
gem_mat = gem.data.materials[0]
bsdf = next(n for n in gem_mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
bsdf.inputs["Emission Color"].default_value = (0.12, 0.55, 0.66, 1)
bsdf.inputs["Emission Strength"].default_value = 0.45

all_meshes = lower + parts
bpy.context.view_layer.update()
bounds = [o.matrix_world @ Vector(v) for o in all_meshes for v in o.bound_box]
height = max(v.z for v in bounds) - min(v.z for v in bounds)
width = max(v.x for v in bounds) - min(v.x for v in bounds)
assert 3.2 < height < 3.3 and 1.17 < width < 1.19, (height, width)
assert abs(min(v.z for v in bounds)) < 0.001, "Base must remain at ground level."

def export(path, objects):
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for obj in objects:
        obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True,
        object_types={"MESH", "EMPTY"}, apply_unit_scale=True, global_scale=1,
        axis_forward="-Z", axis_up="Y", use_mesh_modifiers=True,
        bake_anim=False, add_leaf_bones=False, path_mode="AUTO")

export(folder / "Exports/CrystalPedestalCombined.fbx", all_meshes)
# Unity retains its existing lower renderers and colliders; import only the top.
export(unity_folder / "CrystalPedestalCombinedTop.fbx", parts)

scene.camera.location = (4.2, -6, 4.0)
scene.camera.rotation_euler = (Vector((0, 0, height * 0.49)) - scene.camera.location).to_track_quat("-Z", "Y").to_euler()
scene.camera.data.ortho_scale = 4.15
scene.render.resolution_x = 900
scene.render.resolution_y = 1050
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = str(folder / "Previews/CrystalPedestalCombined.png")
scene.cycles.samples = 32
bpy.ops.wm.save_as_mainfile(filepath=str(folder / "Source/CrystalPedestalCombined.blend"))
bpy.ops.render.render(write_still=True)
print(json.dumps({"source": str(folder / "Source/CrystalPedestalCombined.blend"), "mesh_parts": len(all_meshes), "height": height, "width": width}))
