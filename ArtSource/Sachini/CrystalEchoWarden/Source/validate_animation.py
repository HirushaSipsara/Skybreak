"""Validate the authored rig and FBX takes in Blender; does not run Unity."""
import bpy
import json
from pathlib import Path
from io_scene_fbx import parse_fbx

base=Path(__file__).resolve().parent.parent
scene=bpy.context.scene
rig=scene.objects['CEW_Rig']
report=[]
def check(ok,label):
    assert ok,label
    report.append('PASS '+label)
expected={'Idle':(61,True),'Glide':(31,True),'TurnLeft':(31,True),'TurnRight':(31,True),
          'Retreat':(31,True),'Attack':(28,False),'Hit':(13,False),'Death':(46,False)}
check(len(rig.data.bones)==12,'12-bone rig')
meshes=[o for o in scene.objects if o.type=='MESH' and any(m.type=='ARMATURE' and m.object==rig for m in o.modifiers)]
check(len(meshes)==30,'30 mesh parts bound to this rig')
for obj in meshes:
    for vertex in obj.data.vertices:
        check_group=[g for g in vertex.groups if g.weight>0]
        assert len(check_group)==1 and abs(check_group[0].weight-1)<1e-6,obj.name
        assert obj.vertex_groups[check_group[0].group].name in rig.data.bones,obj.name
check(True,'Every mesh vertex has exactly one valid full-weight bone')
previous=rig.animation_data.action,scene.frame_current
for name,(end,loop) in expected.items():
    action=bpy.data.actions['CEW_'+name]
    rig.animation_data.action=action
    check(tuple(round(x) for x in action.frame_range)==(1,end),name+' frame range')
    scene.frame_set(1)
    first={p.name:p.matrix.copy() for p in rig.pose.bones}
    scene.frame_set(end)
    last={p.name:p.matrix.copy() for p in rig.pose.bones}
    if loop:
        error=max(abs(first[n][r][c]-last[n][r][c]) for n in first for r in range(4) for c in range(4))
        check(error<1e-5,name+' loop seam matches')
    scene.frame_set(1+end//2)
    movement=max(abs(first[p.name][r][c]-p.matrix[r][c]) for p in rig.pose.bones for r in range(4) for c in range(4))
    check(movement>.001,name+' contains changing skeletal poses')
    for frame in range(1,end+1):
        scene.frame_set(frame)
        p=rig.pose.bones['Root']
        assert p.location.length<1e-6 and sum(abs(v) for v in p.rotation_euler)<1e-6,name
check(True,'All clips keep Root stationary')
tree,version=parse_fbx.parse(str(base/'Exports/CrystalEchoWarden.fbx'))
objects=next(e for e in tree.elems if e.id==b'Objects')
stacks=[e for e in objects.elems if e.id==b'AnimationStack']
names=[e.props[1].split(b'\x00')[0].decode() for e in stacks]
check(set(names)=={'CEW_'+n for n in expected},'Exported FBX contains exactly the eight named animation takes')
verify=bpy.data.scenes.get('CEW FBX verification')
if verify:
    imported=[o for o in verify.objects if o.type=='ARMATURE']
    check(len(imported)==1 and len(imported[0].data.bones)==12,'FBX round trip retains rig and bones')
    check(len([o for o in verify.objects if o.type=='MESH'])==30,'FBX round trip retains all 30 mesh parts')
rig.animation_data.action,frame=previous
scene.frame_set(frame)
report+=['Unity import, playback transitions and Play Mode are still unverified.']
(base/'Evidence/AnimationValidation.txt').write_text('\n'.join(report)+'\n')
print('\n'.join(report))
