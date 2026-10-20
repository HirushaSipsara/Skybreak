"""Run in Blender with the refined Warden scene active; preserve the original scene.
Build a rigid-weight robot rig, authored actions and an animation-bearing FBX.
"""
import bpy
import math
import json
import shutil
from pathlib import Path
from mathutils import Vector

base = Path(__file__).resolve().parent.parent
source = bpy.context.scene
original = source.objects.get('CrystalEchoWarden_Root')
assert original is not None, 'Select the original refined Warden scene first'
assert not bpy.data.objects.get('CEW_Rig'), 'Animated rig already exists; inspect before rebuilding'
scene = bpy.data.scenes.new('Crystal Echo Warden - Animation Studio')
scene.world = source.world.copy() if source.world else None
copies = {}
for old in source.objects:
    new = old.copy()
    if old.data is not None:
        new.data = old.data.copy()
    new.animation_data_clear()
    scene.collection.objects.link(new)
    copies[old] = new
for old, new in copies.items():
    if old.parent in copies:
        new.parent = copies[old.parent]
    if old == source.camera:
        scene.camera = new
bpy.context.window.scene = scene
root = copies[original]
root.name = 'CEW_Model'
scene.render.engine = source.render.engine
scene.render.fps = 30
scene.render.resolution_x = scene.render.resolution_y = 600
scene.render.resolution_percentage = 100
scene.view_settings.view_transform = source.view_settings.view_transform
scene.render.image_settings.file_format = source.render.image_settings.file_format

data = bpy.data.armatures.new('CEW_RigData')
rig = bpy.data.objects.new('CEW_Rig', data)
scene.collection.objects.link(rig)
rig.parent = root
rig.show_in_front = True
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
modes = [v.identifier for v in bpy.ops.object.mode_set.get_rna_type().properties['mode'].enum_items]
assert 'EDIT' in modes and 'OBJECT' in modes
bpy.ops.object.mode_set(mode='EDIT')
bone_defs = [('Root',(0,0,-.5),None), ('Body',(0,0,.12),'Root'),
             ('Head',(0,0,.49),'Body'), ('Thruster',(0,0,-.22),'Body')]
for side, suffix in [(-1,'L'),(1,'R')]:
    bone_defs += [('Sensor_'+suffix,(side*.34,0,.69),'Head'),
                  ('UpperArm_'+suffix,(side*.47,0,.28),'Body'),
                  ('Forearm_'+suffix,(side*.59,-.01,-.045),'UpperArm_'+suffix),
                  ('Claw_'+suffix,(side*.62,-.10,-.25),'Forearm_'+suffix)]
for name, head, parent in bone_defs:
    bone = data.edit_bones.new(name)
    bone.head = head
    bone.tail = Vector(head) + Vector((0,0,.12))
    if parent:
        bone.parent = data.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')
rotation_modes = [v.identifier for v in bpy.types.PoseBone.bl_rna.properties['rotation_mode'].enum_items]
assert 'XYZ' in rotation_modes
for bone in rig.pose.bones:
    bone.rotation_mode = 'XYZ'

bindings = {}
depsgraph = bpy.context.evaluated_depsgraph_get()
for old in original.children_recursive:
    obj = copies[old]
    name = old.name
    suffix = name[-1]
    bone = 'Body'
    if name in ('Head shell','CEW_Visor','Helmet crest','Visor recess','VisionPoint','SoundReceiver'):
        bone = 'Head'
    elif name.startswith(('Sensor ', 'Crystal sound sensor ')):
        bone = 'Sensor_'+suffix
    elif name.startswith('Arm upper '):
        bone = 'UpperArm_'+suffix
    elif name.startswith(('Arm joint ', 'Claw block ')):
        bone = 'Forearm_'+suffix
    elif name.startswith(('Claw upper ', 'Claw lower ')):
        bone = 'Claw_'+suffix
    elif name in ('Cyan hover emitter','Hover stabilizer'):
        bone = 'Thruster'
    if obj.type != 'MESH':
        continue
    # Bake bevels before rigid skinning, with exactly one full-weight bone per vertex.
    evaluated = obj.evaluated_get(depsgraph)
    mesh = bpy.data.meshes.new_from_object(evaluated, depsgraph=depsgraph)
    obj.modifiers.clear()
    obj.data = mesh
    obj.vertex_groups.clear()
    group = obj.vertex_groups.new(name=bone)
    assert 'REPLACE' in [v.identifier for v in bpy.types.VertexGroup.bl_rna.functions['add'].parameters['type'].enum_items]
    group.add(list(range(len(mesh.vertices))),1.0,'REPLACE')
    modifier = obj.modifiers.new('Warden rigid skin','ARMATURE')
    modifier.object = rig
    bindings[name] = bone

def pose(name, degrees=(0,0,0), location=(0,0,0)):
    p = rig.pose.bones[name]
    p.rotation_euler = tuple(math.radians(a) for a in degrees)
    p.location = location

clips = {'Idle':(61,True),'Glide':(31,True),'TurnLeft':(31,True),'TurnRight':(31,True),
         'Retreat':(31,True),'Attack':(28,False),'Hit':(13,False),'Death':(46,False)}
actions = {}
rig.animation_data_create()
for name, (last, loop) in clips.items():
    action = bpy.data.actions.new('CEW_'+name)
    action.use_fake_user = True
    rig.animation_data.action = action
    for frame in range(1,last+1):
        t = (frame-1)/(last-1)
        wave = math.sin(2*math.pi*t)
        sway = math.cos(2*math.pi*t)
        for p in rig.pose.bones:
            p.location=(0,0,0); p.rotation_euler=(0,0,0); p.scale=(1,1,1)
        # Bone local Y points up in Blender. No root translation in any clip.
        pose('Body',(1.8*wave,0,1.2*sway),(0,.025*wave,0))
        pose('Head',(-1.2*wave,7*wave,0))
        for side, suffix in [(-1,'L'),(1,'R')]:
            pose('UpperArm_'+suffix,(3*wave,0,side*(4+2*sway)))
            pose('Forearm_'+suffix,(7+3*sway,0,0))
            pose('Claw_'+suffix,(4*wave,0,side*2*wave))
            pose('Sensor_'+suffix,(0,side*5*wave,side*2*sway))
        if name in ('Glide','Retreat','TurnLeft','TurnRight'):
            retreat = name=='Retreat'
            turn = -1 if name=='TurnLeft' else 1 if name=='TurnRight' else 0
            pose('Body',((-8 if retreat else 12)+1.8*wave,0,turn*10+1.8*wave),(0,.035*wave,0))
            pose('Head',((6 if retreat else -7),-turn*12,0))
            pose('Thruster',(-6 if retreat else 6,0,-turn*6))
            for side, suffix in [(-1,'L'),(1,'R')]:
                pose('UpperArm_'+suffix,((-12 if retreat else -20)+side*6*wave,0,side*(10+2*sway)))
                pose('Forearm_'+suffix,(18+side*5*wave,0,0))
                pose('Claw_'+suffix,(12+side*5*wave,0,side*3))
        if name=='Attack':
            # 0.6 s charge (frame 19), pulse/recoil, then recovery.
            seconds=(frame-1)/30
            charge=min(1,seconds/.6)
            recoil=max(0,1-abs(seconds-.67)/.12) if seconds>=.6 else 0
            recover=max(0,(seconds-.72)/.18)
            lift=charge*(1-recover)
            pose('Body',(-4*lift-11*recoil,0,0),(0,.025*lift,0))
            pose('Head',(3*lift,0,0))
            for side, suffix in [(-1,'L'),(1,'R')]:
                pose('UpperArm_'+suffix,(-62*lift+10*recoil,0,side*15*lift))
                pose('Forearm_'+suffix,(-20*lift,0,0))
                pose('Claw_'+suffix,(18*lift,0,side*8*lift))
                pose('Sensor_'+suffix,(0,0,side*9*lift))
        if name=='Hit':
            kick=math.sin(math.pi*t)*math.exp(-2*t)
            pose('Body',(-22*kick,0,10*kick),(0,-.05*kick,0))
            pose('Head',(12*kick,-8*kick,0))
        if name=='Death':
            ease=t*t*(3-2*t)
            pose('Body',(24*ease,0,16*ease),(0,-.25*ease,0))
            pose('Head',(18*ease,0,0))
            for side, suffix in [(-1,'L'),(1,'R')]:
                pose('UpperArm_'+suffix,(12*ease,0,side*8*ease))
                pose('Forearm_'+suffix,(22*ease,0,0))
                pose('Claw_'+suffix,(20*ease,0,0))
                pose('Sensor_'+suffix,(0,0,side*18*ease))
        for p in rig.pose.bones:
            p.keyframe_insert(data_path='rotation_euler',frame=frame,group=p.name)
            p.keyframe_insert(data_path='location',frame=frame,group=p.name)
    actions[name]=action
rig.animation_data.action=None
for name, action in actions.items():
    track=rig.animation_data.nla_tracks.new()
    track.name='CEW_'+name
    strip=track.strips.new('CEW_'+name,1,action)
    strip.name='CEW_'+name
    # Keep all strips enabled for FBX; isolate the selected action for studio viewing below.
    assert 'NOTHING' in [v.identifier for v in strip.bl_rna.properties['extrapolation'].enum_items]
    strip.extrapolation='NOTHING'

scene.frame_start=1; scene.frame_end=61; scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT')
for obj in [root,*root.children_recursive]: obj.select_set(True)
bpy.context.view_layer.objects.active=rig
fbx=base/'Exports'/'CrystalEchoWarden.fbx'
enum = [e.identifier for e in bpy.ops.export_scene.fbx.get_rna_type().properties['object_types'].enum_items]
assert all(e in enum for e in ('MESH','ARMATURE','EMPTY'))
bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},
    add_leaf_bones=False,use_armature_deform_only=False,bake_anim=True,bake_anim_use_all_actions=False,
    bake_anim_use_nla_strips=True,bake_anim_force_startend_keying=True,bake_anim_simplify_factor=0,
    axis_forward='-Z',axis_up='Y')
resource=base.parents[2]/'Assets'/'Resources'/'CrystalEchoWarden'/'CrystalEchoWarden.fbx'
assert resource.parent.is_dir(),resource
shutil.copy2(fbx,resource)
for track in rig.animation_data.nla_tracks: track.mute=True
rig.animation_data.action=actions['Glide']
scene.frame_end=31; scene.frame_set(8)
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_perspective='CAMERA'
        area.spaces.active.overlay.show_overlays=False
        area.spaces.active.shading.color_type='MATERIAL'
blend=base/'Source'/'CrystalEchoWarden_Animated.blend'
bpy.data.libraries.write(str(blend),{scene,*actions.values()},fake_user=True)
report={'bones':len(data.bones),'rigid_mesh_parts':len(bindings),'vertex_weights':'one full-weight bone per vertex',
        'fps':30,'root_motion':False,'clips':{k:{'frames':[1,v[0]],'seconds':(v[0]-1)/30,'loop':v[1]} for k,v in clips.items()},
        'bindings':bindings,'unity_play_mode_tested':False}
(base/'Evidence'/'AnimationReport.json').write_text(json.dumps(report,indent=2))
print(json.dumps({'saved':str(blend),'fbx':str(fbx),'bones':len(data.bones),'clips':list(clips)},indent=2))
