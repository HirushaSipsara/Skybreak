"""Prepare integration exports from Sachini's original models without changing them."""
import bpy
from collections import defaultdict
from pathlib import Path
from mathutils import Matrix

if not bpy.app.background:
    raise RuntimeError("Run in an isolated Blender background process.")
project = Path(__file__).resolve().parents[1]

def export(path, objects):
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True,
        object_types={"MESH", "ARMATURE"}, axis_forward="-Z", axis_up="Y",
        add_leaf_bones=False, bake_anim=False, use_mesh_modifiers=True)

def join_materials(objects, key):
    groups = defaultdict(list)
    for obj in objects:
        groups[key(obj)].append(obj)
    joined = []
    for name, group in groups.items():
        bpy.ops.object.select_all(action="DESELECT")
        for obj in group:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = group[0]
        if len(group) > 1:
            bpy.ops.object.join()
        obj = bpy.context.object
        obj.name = name
        joined.append(obj)
    return joined

bpy.ops.wm.open_mainfile(filepath=str(project / "ArtSource/Student3/ArcExplorer/Source/ArcExplorer.blend"))
rig = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
parts = [o for o in bpy.context.scene.objects if o.type == "MESH" and o.parent == rig]
assert len(parts) == 92
parts = join_materials(parts, lambda o: o.data.materials[0].name)
assert len(parts) <= 9 and len(rig.data.bones) == 18
export(project / "Assets/Models/Custom/ArcExplorer/ArcExplorer.fbx", [rig] + parts)
print("Arc integration export:", len(parts), "skinned material groups; 18 bones")

bpy.ops.wm.open_mainfile(filepath=str(project / "ArtSource/Student3/ArcRescueCarbine/Source/ArcRescueCarbine.blend"))
anchor = bpy.data.objects["FP_WeaponAnchor"]
parts = [o for o in anchor.children if o.type == "MESH" and any(c.name.startswith("ArcRescueCarbine") for c in o.users_collection)]
if len(parts) != 24:
    parts = [o for o in anchor.children if o.type == "MESH" and not o.name.startswith(("Right palm", "Right thumb"))]
assert len(parts) == 24, len(parts)
bpy.context.view_layer.update()
normalize = Matrix.Translation((-.22, .49, .20)) @ anchor.matrix_world.inverted()
for obj in parts:
    matrix = normalize @ obj.matrix_world
    obj.data = obj.data.copy()
    obj.data.transform(matrix)
    obj.parent = None
    obj.matrix_world = Matrix.Identity(4)
    obj["magazine_piece"] = obj.name in ("Cyan energy cell", "Energy cell window")
parts = join_materials(parts, lambda o: ("Magazine__" if o.get("magazine_piece") else "Body__") + o.data.materials[0].name)
export(project / "Assets/Models/Custom/ArcRescueCarbine/ArcRescueCarbine.fbx", parts)
print("Carbine integration export:", len(parts), "material groups; muzzle at Unity (0,0,.565)")
