"""Run in Blender's Text Editor after selecting one floor kit mesh.
The target FBX belongs to this working kit, not the original downloaded folder.
"""
import bpy, os

obj = bpy.context.view_layer.objects.active
if obj is None or obj.type != 'MESH':
    raise RuntimeError('Select a Stone Floor kit mesh first.')
target = 'F:/UnityProjects/Skybreak/Assets/Models/Environment/StoneFloor/' + obj.name + '.fbx'
if not os.path.exists(target):
    raise RuntimeError('This object has no existing working-kit FBX: ' + obj.name)
selected = list(bpy.context.selected_objects)
position = obj.location.copy()
try:
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    obj.location = (0, 0, 0)
    bpy.context.view_layer.update()
    valid_axes = {item.identifier for item in bpy.ops.export_scene.fbx.get_rna_type().properties['axis_forward'].enum_items}
    if '-Z' not in valid_axes:
        raise RuntimeError('This Blender exporter does not support the kit axis settings.')
    bpy.ops.export_scene.fbx(filepath=target, use_selection=True, object_types={'MESH'},
        axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
        use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False)
finally:
    obj.location = position
    bpy.ops.object.select_all(action='DESELECT')
    for previous in selected:
        previous.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.context.view_layer.update()
print('Updated working-kit FBX:', target)
