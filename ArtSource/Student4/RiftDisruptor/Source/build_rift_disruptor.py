"""
Rift Disruptor mech — reproducible Blender build (Student 4).

Builds the model from code as rigid armour parts parented under pivot empties, so Unity can animate
it with transform clips (no skinning). Saves the .blend, exports the FBX for Unity and renders previews.

Run (Blender 5.x):
  blender -b --factory-startup --python build_rift_disruptor.py -- <repo root>

Conventions
  * Metres, Z up. Parts are described facing -Y (Blender's front view) and every part is turned 180 degrees
    about Z as it is created, so the finished robot faces +Y and imports into Unity facing +Z with identity
    joint rotations. (Rotating a parent instead would also rotate every joint's local axes.)
  * Every moving part hangs from an empty named after its joint (Hips, Torso, Head, Shoulder_L, Elbow_L, ...).
    Empties sit exactly at the joint so a rotation in Unity turns the part about the right point.
  * Five shared materials (names are matched by the Unity import step): RD_TealPaint, RD_CreamPlate,
    RD_DarkMetal, RD_Bronze, RD_Glow.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

ROOT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.getcwd()
ART = os.path.join(ROOT, "ArtSource", "Student4", "RiftDisruptor")
BLEND = os.path.join(ART, "Source", "RiftDisruptor.blend")
FBX_EXPORT = os.path.join(ART, "Exports", "RiftDisruptor.fbx")
FBX_UNITY = os.path.join(ROOT, "Assets", "Models", "Custom", "RiftDisruptor", "RiftDisruptor.fbx")
EVIDENCE = os.path.join(ART, "Evidence")

# ----------------------------------------------------------------------------- scene reset
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
collection = bpy.data.collections.new("RiftDisruptor")
scene.collection.children.link(collection)

# ----------------------------------------------------------------------------- materials
def material(name, colour, metallic=0.0, roughness=0.6, emission=None, strength=0.0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*colour, 1)
    try:
        m.use_nodes = True          # deprecated (always on) in Blender 5, required in 4.x
    except Exception:
        pass
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf is not None:
        bsdf.inputs["Base Color"].default_value = (*colour, 1)
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
        if emission is not None:
            bsdf.inputs["Emission Color"].default_value = (*emission, 1)
            bsdf.inputs["Emission Strength"].default_value = strength
    return m

TEAL = material("RD_TealPaint", (0.022, 0.072, 0.075), metallic=0.3, roughness=0.5)
CREAM = material("RD_CreamPlate", (0.42, 0.38, 0.29), metallic=0.1, roughness=0.62)
DARK = material("RD_DarkMetal", (0.035, 0.037, 0.04), metallic=0.85, roughness=0.45)
BRONZE = material("RD_Bronze", (0.42, 0.27, 0.11), metallic=1.0, roughness=0.32)
GLOW = material("RD_Glow", (1.0, 0.28, 0.02), roughness=0.4, emission=(1.0, 0.3, 0.025), strength=2.5)

# ----------------------------------------------------------------------------- hierarchy helpers
def empty(name, location, parent=None):
    """Joint pivot. 'location' is in world space; parenting keeps it there."""
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = "PLAIN_AXES"
    e.empty_display_size = 0.08
    collection.objects.link(e)
    place(e, flip(location), parent)
    return e

FLIP = Matrix.Rotation(math.pi, 3, "Z")

def flip(location):
    return Vector((-location[0], -location[1], location[2]))

def place(ob, location, parent):
    """Parent with an identity parent-inverse and a parent-relative location. Pivots only translate,
    so the local transform is just the offset; FBX exporters and Unity both read that unambiguously."""
    if parent is not None:
        ob.parent = parent
        ob.matrix_parent_inverse = Matrix.Identity(4)
        ob.location = Vector(location) - parent.matrix_world.translation
    else:
        ob.location = location
    bpy.context.view_layer.update()

def finish_mesh(name, bm, mat, location, parent, rotation=None):
    # Orientation goes into the vertices, not the object, so joints keep identity rotations.
    if rotation is not None:
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Euler(rotation).to_matrix())
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=FLIP)
    location = flip(location)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for poly in me.polygons:
        poly.use_smooth = False     # hard-surface faceting, like the reference
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me)
    collection.objects.link(ob)
    place(ob, location, parent)
    return ob

def bevel(bm, width, segments=1):
    if width > 0:
        bmesh.ops.bevel(bm, geom=list(bm.verts) + list(bm.edges), offset=width, segments=segments,
                        affect="EDGES", profile=0.5, clamp_overlap=True)

def box(name, size, location, mat, parent, chamfer=0.02, segments=1, taper=None, rotation=None):
    """Chamfered box. taper=(sx, sy) scales the top face, for wedge-shaped armour."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
        if taper is not None and v.co.z > 0:
            v.co.x *= taper[0]
            v.co.y *= taper[1]
    bevel(bm, min(chamfer, min(size) * 0.45), segments)
    return finish_mesh(name, bm, mat, location, parent, rotation)

def cylinder(name, radius, depth, location, mat, parent, axis="Z", segments=16, radius_top=None, chamfer=0.0, tilt=None):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segments,
                          radius1=radius, radius2=radius if radius_top is None else radius_top, depth=depth)
    bevel(bm, chamfer)
    rot = {"Z": None, "X": (0, math.radians(90), 0), "Y": (math.radians(90), 0, 0)}[axis] if tilt is None else tilt
    return finish_mesh(name, bm, mat, location, parent, rot)

def grille(prefix, centre, size, parent, facing="-Y", slats=4):
    """Glowing vent: emissive panel behind dark horizontal slats, in a dark frame."""
    w, d, h = size
    box(prefix + "_Glow", (w * 0.86, d * 0.6, h * 0.86), centre, GLOW, parent, chamfer=0.004)
    offset = -d * 0.35 if facing == "-Y" else d * 0.35
    for i in range(slats):
        z = centre[2] - h * 0.36 + i * (h * 0.72 / max(1, slats - 1))
        box(f"{prefix}_Slat{i}", (w * 0.9, d * 0.5, h * 0.06), (centre[0], centre[1] + offset, z), DARK, parent, chamfer=0.003)
    for side in (-1, 1):
        box(f"{prefix}_FrameSide{side:+d}", (w * 0.1, d, h), (centre[0] + side * w * 0.5, centre[1], centre[2]), DARK, parent, chamfer=0.006)
    for side in (-1, 1):
        box(f"{prefix}_FrameCap{side:+d}", (w * 1.1, d, h * 0.1), (centre[0], centre[1], centre[2] + side * h * 0.5), DARK, parent, chamfer=0.006)

# ----------------------------------------------------------------------------- body
root = empty("RiftDisruptor", (0, 0, 0))
hips = empty("Hips", (0, 0, 1.02), root)
box("Pelvis", (0.46, 0.30, 0.20), (0, 0, 1.02), DARK, hips, chamfer=0.04)
box("PelvisPlate", (0.30, 0.06, 0.14), (0, -0.16, 0.99), TEAL, hips, chamfer=0.02, taper=(1.2, 1))

torso = empty("Torso", (0, 0, 1.14), hips)
cylinder("WaistRing", 0.2, 0.08, (0, 0, 1.15), BRONZE, torso, segments=20, chamfer=0.01)
cylinder("Abdomen", 0.17, 0.18, (0, 0, 1.25), DARK, torso, segments=10, radius_top=0.2)
box("Chest", (0.7, 0.46, 0.52), (0, 0, 1.56), TEAL, torso, chamfer=0.1, taper=(1.08, 1.0))
box("ChestKeel", (0.22, 0.08, 0.30), (0, -0.21, 1.47), TEAL, torso, chamfer=0.03, taper=(1.3, 1))
for side, x in (("L", 1), ("R", -1)):
    box(f"ChestPlate_{side}", (0.12, 0.40, 0.34), (x * 0.37, 0, 1.52), CREAM, torso, chamfer=0.03, taper=(0.8, 0.9))
grille("ChestVent", (0.17, -0.235, 1.6), (0.16, 0.05, 0.17), torso)          # robot's left chest, as in the reference
box("ChestRivetPlate", (0.13, 0.05, 0.09), (-0.17, -0.225, 1.66), CREAM, torso, chamfer=0.015)
cylinder("Collar", 0.15, 0.06, (0, 0, 1.81), BRONZE, torso, segments=20, chamfer=0.008)
# back vents (the glowing box behind the right shoulder in the reference)
box("BackPack", (0.40, 0.18, 0.32), (0, 0.27, 1.6), DARK, torso, chamfer=0.04)
grille("BackVent_R", (-0.2, 0.28, 1.82), (0.17, 0.16, 0.16), torso, facing="+Y", slats=4)
grille("BackVent_L", (0.2, 0.28, 1.82), (0.13, 0.14, 0.12), torso, facing="+Y", slats=3)

head = empty("Head", (0, 0, 1.84), torso)
cylinder("Neck", 0.06, 0.1, (0, 0, 1.87), DARK, head, segments=10)
box("Helmet", (0.3, 0.31, 0.22), (0, -0.01, 1.99), TEAL, head, chamfer=0.04, taper=(0.9, 0.88))
box("HelmetBrow", (0.28, 0.09, 0.055), (0, -0.15, 2.045), TEAL, head, chamfer=0.015)
box("Visor", (0.22, 0.03, 0.04), (0, -0.172, 1.99), GLOW, head, chamfer=0.006)
box("Jaw", (0.2, 0.12, 0.07), (0, -0.11, 1.905), DARK, head, chamfer=0.015)
for side, x in (("L", 1), ("R", -1)):
    cylinder(f"Ear_{side}", 0.055, 0.04, (x * 0.16, 0, 1.98), BRONZE, head, axis="X", segments=14, chamfer=0.006)
cylinder("Antenna", 0.012, 0.24, (-0.09, 0.06, 2.15), DARK, head, segments=6, tilt=(math.radians(-12), math.radians(-14), 0))
cylinder("AntennaTip", 0.02, 0.03, (-0.115, 0.085, 2.265), BRONZE, head, segments=8)

# ----------------------------------------------------------------------------- arms
def shoulder(side, x):
    pivot = empty(f"Shoulder_{side}", (x * 0.47, 0, 1.69), torso)
    cylinder(f"ShoulderJoint_{side}", 0.12, 0.12, (x * 0.44, 0, 1.69), BRONZE, pivot, axis="X", segments=18, chamfer=0.01)
    box(f"Pauldron_{side}", (0.26, 0.34, 0.2), (x * 0.54, 0, 1.76), CREAM, pivot, chamfer=0.025, segments=1, taper=(0.62, 0.9))
    cylinder(f"UpperArm_{side}", 0.07, 0.3, (x * 0.53, 0, 1.47), DARK, pivot, segments=10)
    box(f"Bicep_{side}", (0.17, 0.18, 0.2), (x * 0.53, 0, 1.47), TEAL, pivot, chamfer=0.03)
    elbow = empty(f"Elbow_{side}", (x * 0.53, 0, 1.31), pivot)
    cylinder(f"ElbowJoint_{side}", 0.09, 0.18, (x * 0.53, 0, 1.31), BRONZE, elbow, axis="X", segments=16, chamfer=0.01)
    return pivot, elbow

# Left: claw arm
sh_l, el_l = shoulder("L", 1)
box("Forearm_L", (0.15, 0.17, 0.30), (0.53, 0, 1.13), TEAL, el_l, chamfer=0.03, taper=(1.1, 1.1))
box("ForearmPlate_L", (0.05, 0.15, 0.24), (0.615, 0, 1.14), CREAM, el_l, chamfer=0.015)
wrist = empty("Wrist_L", (0.53, 0, 0.97), el_l)
cylinder("WristJoint_L", 0.06, 0.1, (0.53, 0, 0.97), BRONZE, wrist, axis="X", segments=14)
box("Palm_L", (0.13, 0.14, 0.09), (0.53, 0, 0.91), DARK, wrist, chamfer=0.02)
for name, (fx, fy) in {"FingerA_L": (0.04, -0.05), "FingerB_L": (-0.04, -0.05), "Thumb_L": (0.0, 0.055)}.items():
    f = empty(name, (0.53 + fx, fy, 0.87), wrist)
    sign = -1 if fy < 0 else 1
    box(name + "_Base", (0.04, 0.04, 0.11), (0.53 + fx, fy + sign * 0.01, 0.81), DARK, f, chamfer=0.008)
    box(name + "_Tip", (0.034, 0.034, 0.1), (0.53 + fx, fy + sign * 0.035, 0.72), DARK, f, chamfer=0.008,
        rotation=(math.radians(-25 * sign), 0, 0))
    cylinder(name + "_Knuckle", 0.02, 0.04, (0.53 + fx, fy, 0.87), BRONZE, f, axis="X", segments=8)

# Right: cannon arm. Barrel runs down the forearm; the charge pose swings it forward.
sh_r, el_r = shoulder("R", -1)
cannon = empty("Cannon_R", (-0.53, 0, 1.31), el_r)
cylinder("CannonBody", 0.15, 0.46, (-0.53, 0, 1.05), DARK, cannon, segments=12, chamfer=0.02)
cylinder("CannonBand", 0.165, 0.14, (-0.53, 0, 1.2), TEAL, cannon, segments=8, chamfer=0.02)
cylinder("CannonRing", 0.16, 0.035, (-0.53, 0, 1.11), BRONZE, cannon, segments=18)
grille("CannonVent", (-0.53, -0.16, 1.0), (0.13, 0.05, 0.15), cannon, slats=4)
grille("CannonVentSide", (-0.68, 0, 1.0), (0.05, 0.13, 0.15), cannon, slats=4)
cylinder("CannonCollar", 0.14, 0.05, (-0.53, 0, 0.83), BRONZE, cannon, segments=18, chamfer=0.008)
cylinder("CannonMuzzle", 0.12, 0.04, (-0.53, 0, 0.8), DARK, cannon, segments=18)
cylinder("MuzzleGlow", 0.09, 0.02, (-0.53, 0, 0.785), GLOW, cannon, segments=18)
empty("Muzzle", (-0.53, 0, 0.77), cannon)

# ----------------------------------------------------------------------------- legs
def leg(side, x):
    hip = empty(f"Hip_{side}", (x * 0.2, 0, 0.98), hips)
    cylinder(f"HipJoint_{side}", 0.1, 0.12, (x * 0.22, 0, 0.98), BRONZE, hip, axis="X", segments=16, chamfer=0.01)
    box(f"Thigh_{side}", (0.25, 0.28, 0.36), (x * 0.21, 0, 0.78), TEAL, hip, chamfer=0.045, taper=(1.1, 1.05))
    box(f"ThighPlate_{side}", (0.16, 0.06, 0.2), (x * 0.21, -0.15, 0.82), CREAM, hip, chamfer=0.02, taper=(1.15, 1))
    box(f"ThighSide_{side}", (0.05, 0.2, 0.22), (x * 0.345, 0, 0.8), CREAM, hip, chamfer=0.018)
    knee = empty(f"Knee_{side}", (x * 0.21, 0, 0.58), hip)
    cylinder(f"KneeJoint_{side}", 0.095, 0.24, (x * 0.21, 0, 0.58), BRONZE, knee, axis="X", segments=16, chamfer=0.01)
    box(f"KneeCap_{side}", (0.16, 0.08, 0.14), (x * 0.21, -0.12, 0.6), CREAM, knee, chamfer=0.03)
    box(f"Shin_{side}", (0.22, 0.26, 0.42), (x * 0.21, 0.01, 0.36), TEAL, knee, chamfer=0.045, taper=(1.12, 1.1))
    box(f"ShinPlate_{side}", (0.17, 0.06, 0.24), (x * 0.21, -0.14, 0.34), CREAM, knee, chamfer=0.02)
    ankle = empty(f"Ankle_{side}", (x * 0.21, 0, 0.15), knee)
    cylinder(f"AnkleJoint_{side}", 0.06, 0.18, (x * 0.21, 0, 0.15), DARK, ankle, axis="X", segments=12)
    box(f"Foot_{side}", (0.27, 0.44, 0.12), (x * 0.21, -0.06, 0.06), DARK, ankle, chamfer=0.03, taper=(0.9, 0.85))
    box(f"FootPlate_{side}", (0.22, 0.2, 0.06), (x * 0.21, -0.17, 0.12), CREAM, ankle, chamfer=0.02)
    for i, tx in enumerate((-0.07, 0.0, 0.07)):
        box(f"Toe{i}_{side}", (0.06, 0.08, 0.07), (x * 0.21 + tx, -0.3, 0.04), DARK, ankle, chamfer=0.012)
    box(f"Heel_{side}", (0.14, 0.08, 0.08), (x * 0.21, 0.17, 0.05), DARK, ankle, chamfer=0.015)

leg("L", 1)
leg("R", -1)

# ----------------------------------------------------------------------------- merge per joint
# Every part under one pivot moves rigidly together, so it can be one mesh with a slot per material.
# 123 pieces become one renderer per joint: far fewer draw calls and transforms for Unity.
part_count = len([o for o in collection.objects if o.type == "MESH"])
for pivot in [o for o in collection.objects if o.type == "EMPTY"]:
    parts = [c for c in pivot.children if c.type == "MESH"]
    if not parts:
        continue
    if len(parts) > 1:
        with bpy.context.temp_override(active_object=parts[0], object=parts[0],
                                       selected_objects=parts, selected_editable_objects=parts):
            bpy.ops.object.join()
    merged = parts[0]
    merged.name = merged.data.name = pivot.name + "_Mesh"
bpy.context.view_layer.update()

# ----------------------------------------------------------------------------- UVs, stats, save
meshes = [o for o in collection.objects if o.type == "MESH"]
print(f"RIFT_DISRUPTOR_MERGE parts={part_count} renderers={len(meshes)}")
for ob in meshes:
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    try:
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
        bpy.ops.object.mode_set(mode="OBJECT")
    except Exception as error:   # UVs only drive the wear texture; the model is valid without them
        print("UV unwrap skipped for", ob.name, error)
        if bpy.context.object and bpy.context.object.mode != "OBJECT":
            bpy.ops.object.mode_set(mode="OBJECT")

triangles = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes)
height = max((o.matrix_world @ Vector(c)).z for o in meshes for c in o.bound_box)
print(f"RIFT_DISRUPTOR_STATS meshes={len(meshes)} empties={len([o for o in collection.objects if o.type == 'EMPTY'])} triangles={triangles} height={height:.3f}")

for path in (BLEND, FBX_EXPORT, FBX_UNITY):
    os.makedirs(os.path.dirname(path), exist_ok=True)
os.makedirs(EVIDENCE, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=BLEND)

bpy.ops.object.select_all(action="SELECT")
export = dict(use_selection=False, object_types={"EMPTY", "MESH"}, apply_unit_scale=True,
              apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=False,
              mesh_smooth_type="FACE", use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False, path_mode="AUTO")
bpy.ops.export_scene.fbx(filepath=FBX_EXPORT, **export)
bpy.ops.export_scene.fbx(filepath=FBX_UNITY, **export)

# ----------------------------------------------------------------------------- preview renders
def render_views():
    floor = bpy.data.objects.new("PreviewFloor", bpy.data.meshes.new("PreviewFloor"))
    bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=6); bm.to_mesh(floor.data); bm.free()
    floor.data.materials.append(material("PreviewStone", (0.09, 0.08, 0.075), roughness=0.85))
    scene.collection.objects.link(floor)
    world = bpy.data.worlds.new("PreviewWorld"); scene.world = world
    try:
        world.use_nodes = True
    except Exception:
        pass
    bg = world.node_tree.nodes.get("Background")
    if bg is not None:
        bg.inputs[0].default_value = (0.02, 0.025, 0.035, 1); bg.inputs[1].default_value = 1.0
    def light(name, kind, energy, location, colour, size=1.0):
        data = bpy.data.lights.new(name, kind); data.energy = energy; data.color = colour
        if kind == "AREA": data.size = size
        ob = bpy.data.objects.new(name, data); ob.location = location; scene.collection.objects.link(ob)
        ob.rotation_euler = (Vector((0, 0, 1.1)) - Vector(location)).to_track_quat("-Z", "Y").to_euler()
    light("Key", "AREA", 600, (2.5, 3.0, 3.2), (1.0, 0.72, 0.45), 2.5)       # warm lantern key, as in the reference
    light("Rim", "AREA", 350, (-2.5, -2.5, 2.8), (0.45, 0.85, 1.0), 2.0)          # cool crystal rim
    light("Fill", "AREA", 120, (-3.0, 2.5, 1.2), (0.8, 0.85, 1.0), 3.0)
    cam_data = bpy.data.cameras.new("PreviewCam"); cam_data.lens = 50
    cam = bpy.data.objects.new("PreviewCam", cam_data); scene.collection.objects.link(cam); scene.camera = cam
    for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "CYCLES"):
        try:
            scene.render.engine = engine; break
        except Exception:
            continue
    if scene.render.engine == "CYCLES":
        scene.cycles.samples = 32
    scene.render.resolution_x, scene.render.resolution_y = 900, 1100
    scene.render.image_settings.file_format = "PNG"
    try:
        scene.view_settings.view_transform = "AgX"
    except Exception:
        pass
    target = Vector((0, 0, 1.08))
    for name, angle in (("front", 0), ("three_quarter", 35), ("side", 90), ("back", 180)):
        a = math.radians(angle)
        cam.location = Vector((math.sin(a) * 5.2, math.cos(a) * 5.2, 1.5))   # robot faces +Y
        cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(EVIDENCE, f"RiftDisruptor_{name}.png")
        bpy.ops.render.render(write_still=True)
    print("RIFT_DISRUPTOR_RENDER engine=" + scene.render.engine)

if "--no-render" not in sys.argv:
    try:
        render_views()
    except Exception as error:
        print("Preview render failed:", error)
print("RIFT_DISRUPTOR_DONE")
