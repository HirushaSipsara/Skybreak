"""Create the Arc Rescue Carbine first-person weapon package in Blender."""
from pathlib import Path
import bpy, math, json
from mathutils import Vector

BASE = Path(__file__).resolve().parents[1]
for directory in ('Exports', 'Evidence'):
    (BASE / directory).mkdir(exist_ok=True)

scene = bpy.data.scenes.new('Arc Rescue Carbine - First Person')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
asset = bpy.data.collections.new('ArcRescueCarbine_Asset')
studio = bpy.data.collections.new('PreviewOnly')
scene.collection.children.link(asset)
scene.collection.children.link(studio)
parts = []

def make_material(name, color, metallic=0.0, roughness=.45, emission=0.0):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1)
    material.use_nodes = True
    shader = next(node for node in material.node_tree.nodes if node.type == 'BSDF_PRINCIPLED')
    shader.inputs['Base Color'].default_value = (*color, 1)
    shader.inputs['Metallic'].default_value = metallic
    shader.inputs['Roughness'].default_value = roughness
    shader.inputs['Emission Color'].default_value = (*color, 1)
    shader.inputs['Emission Strength'].default_value = emission
    return material

graphite = make_material('Carbine graphite polymer', (.025, .045, .06), .2, .32)
teal = make_material('Carbine teal casing', (.035, .23, .28), .25, .35)
orange = make_material('Carbine rescue orange', (.86, .30, .055), .15, .38)
steel = make_material('Carbine blue steel', (.23, .34, .39), .7, .27)
cyan = make_material('Carbine cyan energy', (.02, .75, .9), .35, .16, 1.8)
ivory = make_material('Carbine safety ivory', (.8, .86, .76), .1, .45)

def place_in_asset(obj):
    for collection in list(obj.users_collection):
        collection.objects.unlink(obj)
    asset.objects.link(obj)

def finish(obj, name, material, bevel=.01):
    obj.name = name
    place_in_asset(obj)
    obj.data.materials.append(material)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        modifier = obj.modifiers.new('Soft manufactured edges', 'BEVEL')
        modifier.width = bevel
        modifier.segments = 2
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    parts.append(obj)
    return obj

def box(name, location, dimensions, material, bevel=.01):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.dimensions = dimensions
    return finish(obj, name, material, bevel)

def cylinder(name, location, radius, depth, material, vertices=12, rotation=(math.pi / 2, 0, 0), bevel=.008):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location, rotation=rotation)
    return finish(bpy.context.object, name, material, bevel)

def rounded_bar(name, start, end, width, material):
    start, end = Vector(start), Vector(end)
    direction = end - start
    midpoint = (start + end) / 2
    obj = box(name, midpoint, (width, width, direction.length), material, width*.25)
    obj.rotation_euler = direction.to_track_quat('Z', 'Y').to_euler()
    return obj

# Origin is the camera/weapon anchor. Blender view front is -Y.
anchor = bpy.data.objects.new('FP_WeaponAnchor', None)
asset.objects.link(anchor)
anchor.empty_display_type = 'ARROWS'
anchor.empty_display_size = .14
anchor.location = (.35, -1.30, -.55)
anchor.rotation_euler.z = -.28
anchor['unity_anchor'] = 'Parent this to the first-person camera or weapon socket.'
anchor['forward_axis'] = 'Blender -Y; export is Unity +Z forward.'

# The receiver sits low-right when parented to the camera: x is right, y is forward, z is down.
receiver = box('Receiver housing', (.22, -.48, -.21), (.24, .44, .15), teal, .028)
receiver.parent = anchor
box('Receiver upper rail', (.22, -.52, -.10), (.12, .30, .04), steel, .009).parent = anchor
box('Cyan status strip', (.22, -.38, -.205), (.10, .024, .035), cyan, .005).parent = anchor
box('Rescue orange side plate', (.10, -.49, -.20), (.018, .19, .10), orange, .006).parent = anchor
box('Safety marker', (.095, -.42, -.165), (.012, .043, .018), ivory, .003).parent = anchor

# Compact barrel and recognisable energy muzzle.
cylinder('Barrel shroud', (.22, -.79, -.20), .068, .40, graphite, 12).parent = anchor
cylinder('Teal barrel collar', (.22, -.65, -.20), .086, .075, teal, 12).parent = anchor
cylinder('Cyan muzzle emitter', (.22, -.998, -.20), .049, .10, cyan, 12).parent = anchor
cylinder('Muzzle outer ring', (.22, -1.055, -.20), .073, .035, steel, 12).parent = anchor
for i in range(3):
    angle = i * math.tau / 3
    x = .22 + .075 * math.cos(angle)
    z = -.20 + .075 * math.sin(angle)
    fin = box('Muzzle cooling fin ' + str(i), (x, -.93, z), (.022, .10, .04), steel, .004)
    fin.rotation_euler.y = angle
    fin.parent = anchor

# First-person sight and short stock silhouette.
box('Holographic sight base', (.22, -.50, -.02), (.095, .10, .05), graphite, .009).parent = anchor
box('Holographic sight frame', (.22, -.52, .045), (.11, .025, .10), steel, .008).parent = anchor
box('Holographic sight lens', (.22, -.535, .047), (.075, .009, .062), cyan, .002).parent = anchor
stock = box('Compact shoulder stock', (.22, -.20, -.22), (.17, .24, .12), graphite, .02)
stock.rotation_euler.x = -.18
stock.parent = anchor
box('Stock orange accent', (.22, -.12, -.205), (.10, .06, .024), orange, .006).parent = anchor

# Angled pistol grip, trigger guard and an energy-cell magazine.
grip = box('Pistol grip', (.22, -.35, -.36), (.105, .16, .23), graphite, .022)
grip.rotation_euler.x = -.26
grip.parent = anchor
box('Grip teal insert', (.22, -.405, -.37), (.075, .02, .13), teal, .006).parent = anchor
rounded_bar('Trigger guard front', (.22, -.46, -.275), (.22, -.38, -.35), .019, steel).parent = anchor
rounded_bar('Trigger guard rear', (.22, -.38, -.35), (.22, -.31, -.275), .019, steel).parent = anchor
box('Trigger', (.22, -.405, -.305), (.018, .027, .07), orange, .005).parent = anchor
cell = box('Cyan energy cell', (.22, -.57, -.34), (.12, .12, .18), steel, .02)
cell.rotation_euler.x = .16
cell.parent = anchor
box('Energy cell window', (.22, -.635, -.34), (.075, .009, .09), cyan, .004).parent = anchor

# Faint optional hand reference geometry: it is excluded from FBX, just helps line up the gun in first person.
hand_collection = bpy.data.collections.new('HandReference_ExcludeFromExport')
scene.collection.children.link(hand_collection)
for name, location, scale in [('Right palm', (.22, -.32, -.43), (.105, .08, .09)), ('Right thumb', (.15, -.39, -.38), (.04, .05, .08))]:
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=6, radius=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(ivory)
    for collection in list(obj.users_collection): collection.objects.unlink(obj)
    hand_collection.objects.link(obj)
    obj.parent = anchor
hand_collection.hide_render = True

# Preview camera represents a first-person camera, with the gun occupying the lower-right frame.
world = bpy.data.worlds.new('Carbine studio world')
world.use_nodes = True
scene.world = world
background = next(node for node in world.node_tree.nodes if node.type == 'BACKGROUND')
background.inputs['Color'].default_value = (.012, .025, .045, 1)
background.inputs['Strength'].default_value = .25
def aim(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()
for name, location, power, color in [('Key', (-1.8, -.9, 1.0), 380, (.7, .88, 1)), ('Rim', (1.4, .3, .8), 580, (.2, .85, 1))]:
    data = bpy.data.lights.new(name, 'AREA')
    data.energy = power
    data.shape = 'DISK'
    data.size = 2
    data.color = color
    obj = bpy.data.objects.new(name, data)
    studio.objects.link(obj)
    obj.location = location
    aim(obj, (.22, -.55, -.22))
camera_data = bpy.data.cameras.new('First-person preview camera')
camera = bpy.data.objects.new('First-person preview camera', camera_data)
studio.objects.link(camera)
camera.location = (0, 0, 0)
camera.rotation_euler = (1.280, .104, -3.064)
camera_data.lens = 32
scene.camera = camera
scene.render.engine = 'BLENDER_EEVEE'
scene.render.resolution_x = 1280
scene.render.resolution_y = 720
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.filepath = str(BASE / 'Evidence/FirstPersonPreview.png')

# The actual exported asset excludes camera, lights, and hand reference objects.
bpy.ops.object.select_all(action='DESELECT')
for obj in parts + [anchor]:
    obj.select_set(True)
bpy.context.view_layer.objects.active = receiver
bpy.ops.export_scene.fbx(filepath=str(BASE / 'Exports/ArcRescueCarbine.fbx'), use_selection=True, object_types={'MESH', 'EMPTY'}, axis_forward='-Z', axis_up='Y', bake_anim=False, use_mesh_modifiers=True)
triangles = sum(sum(len(polygon.vertices)-2 for polygon in obj.data.polygons) for obj in parts)
coordinates = [obj.matrix_world @ Vector(point) for obj in parts for point in obj.bound_box]
report = {'asset':'ArcRescueCarbine','type':'stylised first-person rescue carbine','mesh_parts':len(parts),'triangles':triangles,'dimensions_metres':[max(p[i] for p in coordinates)-min(p[i] for p in coordinates) for i in range(3)],'anchor':'FP_WeaponAnchor','muzzle':'Cyan muzzle emitter','external_textures':False,'unity_tested':False}
(BASE / 'Evidence/Validation.json').write_text(json.dumps(report, indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(BASE / 'Source/ArcRescueCarbine.blend'))
print(json.dumps(report))
