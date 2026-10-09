"""AI-assisted editable crystal pedestal asset. Run with Blender --background --python."""
import bpy
import json
import math
from pathlib import Path
from mathutils import Vector

if not bpy.app.background:
    raise RuntimeError('Run this script in a separate Blender background process with --factory-startup; do not run it inside an open modelling session.')
SCRIPT_DIR = Path(__file__).resolve().parent
ROOT = SCRIPT_DIR.parent if SCRIPT_DIR.name == 'Source' else SCRIPT_DIR.parent / 'ArtSource/Student1/CrystalPedestal'
for folder in ('Source', 'Exports', 'Previews', 'Documentation'):
    (ROOT / folder).mkdir(parents=True, exist_ok=True)

# This script runs in an isolated factory-startup process, never the user's open file.
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.name = 'CrystalPedestal_Presentation'
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
asset = bpy.data.collections.new('01_ASSET / Export these meshes')
scene.collection.children.link(asset)
stage = bpy.data.collections.new('02_PREVIEW / Do not export')
scene.collection.children.link(stage)

def put(obj, collection):
    for current in list(obj.users_collection):
        current.objects.unlink(obj)
    collection.objects.link(obj)

def linear_component(c):
    return c / 12.92 if c <= .04045 else ((c + .055) / 1.055) ** 2.4

def material(name, rgb, roughness=.65, metallic=0, emission=0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    color = tuple(linear_component(c) for c in rgb) + (1.0,)
    m.diffuse_color = color
    bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = color
    bsdf.inputs['Roughness'].default_value = roughness
    bsdf.inputs['Metallic'].default_value = metallic
    if emission:
        bsdf.inputs['Emission Color'].default_value = color
        bsdf.inputs['Emission Strength'].default_value = emission
    return m

stone_dark = material('StoneDark', (.12549, .21569, .26275), .65)
stone_light = material('StoneLight', (.39608, .49412, .53333), .65)
brass = material('Brass', (.84314, .65098, .41569), .53, .5)
crystal_mat = material('Crystal_Cyan', (.46275, .90588, .89020), .45, .2, .9)

root = bpy.data.objects.new('CrystalPedestal_ROOT', None)
asset.objects.link(root)
root.empty_display_size = .35
root['provenance'] = 'AI-assisted model recreation from the supplied Skybreak reference. Not evidence of manual modelling.'
root['units'] = 'Metres; ground-centred origin; total height 2.63m'

def box(name, dimensions, z, mat, bevel=.012):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, z))
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    put(obj, asset)
    obj.parent = root
    obj.data.materials.append(mat)
    # Tiny bevels catch light; modifiers remain editable in the blend file.
    mod = obj.modifiers.new('Editable edge bevel', 'BEVEL')
    mod.width = bevel
    mod.segments = 1
    return obj

body = box('01_StoneBase', (.95, .95, 1.14), .57, stone_dark)
slab = box('02_TopSlab', (1.18, 1.18, .22), 1.16, stone_light)
socket = box('03_BrassSocket', (.64, .64, .13), 1.34, brass, .008)

# Four-sided double pyramid: eight flat triangular facets, matching the reference.
verts = [(0, 0, -.7), (.27, 0, 0), (0, .27, 0), (-.27, 0, 0), (0, -.27, 0), (0, 0, .7)]
faces = [(0, 2, 1), (0, 3, 2), (0, 4, 3), (0, 1, 4), (5, 1, 2), (5, 2, 3), (5, 3, 4), (5, 4, 1)]
mesh = bpy.data.meshes.new('EightFacetCrystal_Mesh')
mesh.from_pydata(verts, [], faces)
mesh.update()
gem = bpy.data.objects.new('04_FacetedCrystal', mesh)
asset.objects.link(gem)
gem.location.z = 1.93
gem.parent = root
gem.data.materials.append(crystal_mat)
uv = mesh.uv_layers.new(name='UVMap')
for poly in mesh.polygons:
    for loop, coordinate in zip(poly.loop_indices, ((.1, .1), (.9, .1), (.5, .9))):
        uv.data[loop].uv = coordinate

objects = [body, slab, socket, gem]
for obj in objects:
    obj['role'] = 'Editable asset mesh; included in FBX export'
    obj['generated_with_assistance'] = True
    for polygon in obj.data.polygons:
        polygon.use_smooth = False

# Simple neutral preview stage; excluded from game exports.
bpy.ops.mesh.primitive_plane_add(size=200)
ground = bpy.context.object
ground.name = 'Preview ground / not part of asset'
ground.location.z = -.016
ground.data.materials.append(material('Preview_Background', (.10, .15, .17), .8))
put(ground, stage)

world = bpy.data.worlds.new('Soft studio world')
world.use_nodes = True
background = next(n for n in world.node_tree.nodes if n.type == 'BACKGROUND')
background.inputs['Color'].default_value = (.055, .075, .10, 1)
background.inputs['Strength'].default_value = .45
scene.world = world

def aim(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()

for name, pos, power, color, size in (
    ('Key', (3, -4, 5.5), 600, (1, .86, .70), 4),
    ('Fill', (-4, -1, 3), 400, (.60, .85, 1), 3),
    ('Rim', (1, 3, 4), 750, (.45, .85, 1), 3),
):
    light = bpy.data.lights.new(name, 'AREA')
    light.energy = power
    light.color = color
    light.shape = 'DISK'
    light.size = size
    obj = bpy.data.objects.new(name + ' / preview', light)
    stage.objects.link(obj)
    obj.location = pos
    aim(obj, (0, 0, 1.3))

camera_data = bpy.data.cameras.new('Presentation camera')
camera = bpy.data.objects.new('Presentation camera / preview', camera_data)
stage.objects.link(camera)
camera.location = (4.2, -6, 3.7)
aim(camera, (0, 0, 1.3))
camera_data.type = 'ORTHO'
camera_data.ortho_scale = 3.5
scene.camera = camera
scene.render.engine = 'BLENDER_EEVEE'
scene.render.resolution_x = 900
scene.render.resolution_y = 1000
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.film_transparent = False
scene.view_settings.view_transform = 'Standard'
scene.render.filepath = str(ROOT / 'Previews/CrystalPedestal_Beauty.png')

# Preserve a clear modelling view on opening the file.
bpy.ops.object.select_all(action='DESELECT')
for obj in objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = gem
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            area.spaces.active.region_3d.view_location = Vector((0, 0, 1.3))
            area.spaces.active.region_3d.view_distance = 4.8
            area.spaces.active.shading.type = 'MATERIAL'
stage.hide_viewport = True

# Selected meshes and the ground-origin root are exported. No camera/lights/plane.
root.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(ROOT / 'Exports/CrystalPedestal.fbx'), use_selection=True,
    object_types={'MESH', 'EMPTY'}, apply_unit_scale=True, global_scale=1,
    axis_forward='-Z', axis_up='Y', use_mesh_modifiers=True, bake_anim=False,
    add_leaf_bones=False, path_mode='AUTO')

stage.hide_viewport = False
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / 'Source/CrystalPedestal.blend'))
bpy.ops.render.render(write_still=True)
camera.location = (0, -6, 1.3)
aim(camera, (0, 0, 1.3))
scene.render.filepath = str(ROOT / 'Previews/CrystalPedestal_Front.png')
bpy.ops.render.render(write_still=True)
# Restore beauty camera and keep preview stage out of the editable viewport.
camera.location = (4.2, -6, 3.7)
aim(camera, (0, 0, 1.3))
scene.render.filepath = str(ROOT / 'Previews/CrystalPedestal_Beauty.png')
stage.hide_viewport = True
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / 'Source/CrystalPedestal.blend'))

evaluated_triangles = 0
for obj in objects:
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    evaluated_mesh = evaluated.to_mesh()
    evaluated_mesh.calc_loop_triangles()
    evaluated_triangles += len(evaluated_mesh.loop_triangles)
    evaluated.to_mesh_clear()
report = {'asset': 'CrystalPedestal', 'mesh_objects': len(objects),
          'evaluated_triangles': evaluated_triangles,
          'dimensions_metres': {'width': 1.18, 'depth': 1.18, 'height': 2.63},
          'authorship': 'AI-assisted generated model; personal manual edits must be documented separately.',
          'exports': ['CrystalPedestal.fbx'], 'blender_version': bpy.app.version_string}
(ROOT / 'Documentation/AssetReport.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print('CRYSTAL_PACKAGE_CREATED ' + json.dumps(report))
