"""Check saved skinning, animated deformation and FBX round-trip structure."""
import bpy
import json
from pathlib import Path

folder=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(folder/"Source/VaultHound_Rigged.blend"))
scene=bpy.context.scene
rig=next(o for o in scene.objects if o.type=="ARMATURE")
mesh=next(o for o in scene.objects if o.name.startswith("VaultHound_SkinnedMesh"))
assert len(rig.data.bones)==19
assert len(mesh.data.materials)==1 and len(mesh.data.uv_layers)==1
assert all(len(v.groups)==1 and abs(v.groups[0].weight-1)<.001 for v in mesh.data.vertices)
clips={t.name:t.strips[0].action for t in rig.animation_data.nla_tracks}
assert set(clips)=={"Idle","Walk","Run","Alert","Sniff","Lunge","Hit","Death"}
def positions():
    evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
    result=evaluated.to_mesh()
    values=[v.co.copy() for v in result.vertices]
    evaluated.to_mesh_clear()
    return values
rig.animation_data.action=clips["Run"]
scene.frame_set(1);a=positions()
scene.frame_set(6);b=positions()
changed=sum((x-y).length>.0001 for x,y in zip(a,b))
assert changed>len(a)//2,changed
saved_mesh_count=len(mesh.data.vertices)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(folder/"Exports/VaultHound_Animated.fbx"))
meshes=[o for o in bpy.context.scene.objects if o.type=="MESH"]
rigs=[o for o in bpy.context.scene.objects if o.type=="ARMATURE"]
assert len(meshes)==1 and len(rigs)==1,(len(meshes),len(rigs))
assert len(rigs[0].data.bones)==19
assert len(meshes[0].data.vertices)==saved_mesh_count
assert len(bpy.data.actions)==8,len(bpy.data.actions)
assert all(len(v.groups)>0 for v in meshes[0].data.vertices)
report={"source_skinning":"pass","single_atlas_uv":"pass","source_clips":sorted(clips),
    "run_vertices_moving":changed,"fbx_meshes":1,"fbx_bones":19,
    "fbx_clips":sorted(a.name for a in bpy.data.actions),"fbx_weights":"present for all vertices",
    "unity_import_tested":False,"gameplay_ai_included":False}
(folder/"Evidence/ExportValidation.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
print(json.dumps(report,indent=2))
