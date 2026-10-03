"""Run in the connected Blender. Uses the user's downloaded 4engine meshes.
Does not overwrite the downloaded files or remove the user's original scene.
"""
import bpy, os, json, math
from mathutils import Vector, Quaternion

project = 'F:/UnityProjects/Skybreak'
scene = bpy.data.scenes['Skybreak Stone Floor Workshop']
bpy.context.window.scene = scene
if bpy.data.collections.get('02 - One metre tile variations') is not None:
    raise RuntimeError('The tile kit already exists. Edit it and use export_selected_stone.py; do not regenerate over your edits.')
kit = bpy.data.collections['01 - Editable source kit']
texdir = project + '/resources/Stone Floor/Textures/Textures'
exportdir = project + '/Assets/Skybreak/Handbuilt/Models/StoneFloor'
os.makedirs(exportdir, exist_ok=True)

def material(name, prefix, grass=False):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Metallic'].default_value = 0
    bsdf.inputs['Roughness'].default_value = .3 if not grass else .8
    bsdf.inputs['Coat Weight'].default_value = .12 if not grass else 0
    bsdf.inputs['Coat Roughness'].default_value = .25
    extension = '.tga' if grass else '.jpg'
    color = nodes.new('ShaderNodeTexImage')
    color.image = bpy.data.images.load(texdir + '/' + prefix + '_Diffuse' + extension, check_existing=True)
    color.image.colorspace_settings.name = 'sRGB'
    color.location = (-700, 220)
    links.new(color.outputs['Color'], bsdf.inputs['Base Color'])
    if grass:
        links.new(color.outputs['Alpha'], bsdf.inputs['Alpha'])
    normal = nodes.new('ShaderNodeTexImage')
    normal.image = bpy.data.images.load(texdir + '/' + prefix + '_Normal.jpg', check_existing=True)
    normal.image.colorspace_settings.name = 'Non-Color'
    normal.location = (-900, -200)
    # Original .blend refers to missing *_Normal_Invert maps. Reconstruct that
    # OpenGL orientation from the supplied main normal map without changing it.
    separate = nodes.new('ShaderNodeSeparateXYZ')
    combine = nodes.new('ShaderNodeCombineXYZ')
    flip = nodes.new('ShaderNodeMath'); flip.operation = 'SUBTRACT'
    flip.inputs[0].default_value = 1
    links.new(normal.outputs['Color'], separate.inputs[0])
    links.new(separate.outputs['X'], combine.inputs['X'])
    links.new(separate.outputs['Y'], flip.inputs[1])
    links.new(flip.outputs[0], combine.inputs['Y'])
    links.new(separate.outputs['Z'], combine.inputs['Z'])
    normalmap = nodes.new('ShaderNodeNormalMap')
    normalmap.inputs['Strength'].default_value = .45
    links.new(combine.outputs[0], normalmap.inputs['Color'])
    links.new(normalmap.outputs['Normal'], bsdf.inputs['Normal'])
    if not grass:
        spec = nodes.new('ShaderNodeTexImage')
        spec.image = bpy.data.images.load(texdir + '/' + prefix + '_Specular.jpg', check_existing=True)
        spec.image.colorspace_settings.name = 'Non-Color'
        multiply = nodes.new('ShaderNodeMath'); multiply.operation = 'MULTIPLY'
        multiply.inputs[1].default_value = .25
        subtract = nodes.new('ShaderNodeMath'); subtract.operation = 'SUBTRACT'
        subtract.inputs[0].default_value = .38
        links.new(spec.outputs['Color'], multiply.inputs[0])
        links.new(multiply.outputs[0], subtract.inputs[1])
        links.new(subtract.outputs[0], bsdf.inputs['Roughness'])
    mat.diffuse_color = (.36, .40, .40, 1) if not grass else (.18,.25,.06,1)
    return mat

atlas = material('Skybreak_StoneAtlas', 'StoneFloor')
details = material('Skybreak_StoneDetails', 'StoneFloorDetails')
grass = material('Skybreak_StoneGrass', 'StoneFloorGrass', True)
sources = list(kit.objects)
for index, obj in enumerate(sources):
    if not obj.get('skybreak_metres'):
        for vertex in obj.data.vertices:
            vertex.co *= .01
        if 'Slab' in obj.name:
            top = max(v.co.z for v in obj.data.vertices)
            for vertex in obj.data.vertices: vertex.co.z -= top
        if 'Ground' in obj.name:
            # Soil sits just below the tile seams, not at the large source offset.
            top = max(v.co.z for v in obj.data.vertices)
            for vertex in obj.data.vertices: vertex.co.z += -.06 - top
        obj['skybreak_metres'] = True
    obj.data.materials.clear()
    obj.data.materials.append(grass if 'Grass' in obj.name else details)
    for polygon in obj.data.polygons: polygon.material_index = 0
    obj.location = ((index % 5) * 1.5 - 3, -3 - (index // 5) * 1.5, 0)

variants = bpy.data.collections.new('02 - One metre tile variations')
scene.collection.children.link(variants)
base = bpy.data.objects['StoneFloor_SlabOne']
tile_objects = []
for index in range(16):
    mesh = base.data.copy()
    mesh.name = 'Tile_%02d_UV' % (index + 1)
    width = max(v.co.x for v in mesh.vertices) - min(v.co.x for v in mesh.vertices)
    depth = max(v.co.y for v in mesh.vertices) - min(v.co.y for v in mesh.vertices)
    for v in mesh.vertices:
        v.co.x *= .985 / width
        v.co.y *= .985 / depth
    mesh.update()
    mesh.materials.clear(); mesh.materials.append(atlas); mesh.materials.append(details)
    col, row = index % 4, index // 4
    for poly in mesh.polygons:
        if poly.normal.z > .5:
            poly.material_index = 0
            for li in poly.loop_indices:
                co = mesh.vertices[mesh.loops[li].vertex_index].co
                u = (co.x / .985 + .5) * .244 + .003
                v = (co.y / .985 + .5) * .244 + .003
                mesh.uv_layers.active.data[li].uv = (col * .25 + u, row * .25 + v)
        else:
            poly.material_index = 1
    obj = bpy.data.objects.new('FloorTile_%02d' % (index + 1), mesh)
    variants.objects.link(obj)
    obj.location = (col - 1.5, row + 1, 0)
    obj['source'] = '4engine StoneFloor_SlabOne: metre conversion and 4x4 atlas UV variation'
    obj['grid_spacing_m'] = 1.0
    tile_objects.append(obj)

# One actual downloaded ground model per cell fills the joints.
ground = bpy.data.objects['StoneFloor_Ground']
width = max(v.co.x for v in ground.data.vertices) - min(v.co.x for v in ground.data.vertices)
depth = max(v.co.y for v in ground.data.vertices) - min(v.co.y for v in ground.data.vertices)
for vertex in ground.data.vertices:
    vertex.co.x *= 1.0 / width
    vertex.co.y *= 1.0 / depth

scene.world = bpy.data.worlds.new('Stone Workshop studio environment')
scene.world.use_nodes = True
background = next(n for n in scene.world.node_tree.nodes if n.type == 'BACKGROUND')
background.inputs['Color'].default_value = (.14,.19,.23,1)
background.inputs['Strength'].default_value = .6
light_collection = bpy.data.collections.new('03 - Preview lighting')
scene.collection.children.link(light_collection)
for name, pos, energy, color, size in [
    ('Large softbox', (1,-1,5), 800, (1,.88,.73), 5),
    ('Cool rim', (-4,4,3), 650, (.54,.82,1), 4)]:
    data = bpy.data.lights.new(name, 'AREA'); data.energy = energy; data.color = color; data.size = size
    obj = bpy.data.objects.new(name, data); light_collection.objects.link(obj); obj.location = pos
    obj.rotation_euler = (Vector((0,1,0))-obj.location).to_track_quat('-Z','Y').to_euler()
camera_data = bpy.data.cameras.new('Tile Kit Camera')
camera = bpy.data.objects.new('Tile Kit Camera', camera_data)
light_collection.objects.link(camera); scene.camera = camera
camera.location = (6,-7,8)
camera.rotation_euler = (Vector((0,0,0))-camera.location).to_track_quat('-Z','Y').to_euler()
camera_data.lens = 45
scene.render.resolution_x = 1400; scene.render.resolution_y = 1000
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.filepath = project + '/ArtSource/FloorKit/StoneFloorKit-preview.png'

report=[]
for obj in sources + tile_objects:
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True); bpy.context.view_layer.objects.active = obj
    position = obj.location.copy(); obj.location = (0,0,0)
    bpy.context.view_layer.update()
    path = exportdir + '/' + obj.name + '.fbx'
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'},
        axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
        use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False)
    obj.location = position
    obj.data.calc_loop_triangles()
    report.append({'name':obj.name, 'dimensions_m':list(obj.dimensions), 'triangles':len(obj.data.loop_triangles), 'fbx':path})

# Fix only images introduced by this kit; do not touch unrelated scene images.
for img in list(bpy.data.images):
    if img.name.startswith('StoneFloor') and img.size[0] == 0:
        basename = os.path.basename(img.filepath.replace('\\','/')).replace('_Normal_Invert','_Normal')
        newpath = texdir + '/' + basename
        if os.path.exists(newpath):
            img.filepath = newpath; img.reload()

scene['source_assets'] = 'User-provided 4engine Stone Floor kit. Original downloads remain in resources/Stone Floor.'
scene['notes'] = 'Tile variants reuse the provided slab geometry. Textures repathed; original centimetres converted to metres. These are external assets, not original student models.'
for image in bpy.data.images:
    if image.name.startswith('StoneFloor') and image.size[0] > 0 and not image.packed_file: image.pack()
for area in bpy.context.screen.areas:
    if area.type == 'VIEW_3D':
        area.spaces.active.shading.type = 'MATERIAL'
        area.spaces.active.region_3d.view_location = Vector((0,0,0))
        area.spaces.active.region_3d.view_distance = 11
        area.spaces.active.region_3d.view_rotation = camera.rotation_euler.to_quaternion()
bpy.ops.object.select_all(action='DESELECT')
tile_objects[5].select_set(True); bpy.context.view_layer.objects.active = tile_objects[5]
blendpath = project + '/ArtSource/FloorKit/SkybreakStoneFloorKit.blend'
bpy.ops.wm.save_as_mainfile(filepath=blendpath)
with open(project + '/ArtSource/FloorKit/stone-kit-manifest.json','w',encoding='utf8') as out:
    json.dump({'blend':blendpath,'source':project+'/resources/Stone Floor','assets':report},out,indent=2)
print(json.dumps({'saved_blend':blendpath,'export_count':len(report),'scene':scene.name,'original_scene_object_count':len(bpy.data.scenes['Scene'].objects),'missing_kit_images':[i.name for i in bpy.data.images if i.name.startswith('StoneFloor') and i.size[0] == 0]}))
