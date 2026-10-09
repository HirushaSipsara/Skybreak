import bpy,json
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[1]
results=[]
for name in ('CarvedStoneTrim_1m','DiamondWallPanel','CopperWallConduit_1m','WeatheredGuardianStatue'):
    expected=json.loads((root/'Documentation'/f'{name}.json').read_text())
    bpy.ops.wm.open_mainfile(filepath=str(root/'Source'/f'{name}.blend'))
    meshes=[o for o in bpy.data.collections['01_ASSET / Export'].objects if o.type=='MESH']
    assert len(meshes)==expected['meshes']
    assert all(o.data.materials and o.data.uv_layers for o in meshes)
    assert all(abs(scale-1)<1e-4 for o in meshes for scale in o.scale)
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(root/'Exports'/f'{name}.fbx'))
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(meshes)==expected['meshes']
    assert not any(o.type in ('CAMERA','LIGHT') for o in bpy.context.scene.objects)
    corners=[o.matrix_world@Vector(v) for o in meshes for v in o.bound_box];dims=[max(v[i] for v in corners)-min(v[i] for v in corners) for i in range(3)]
    assert max(abs(a-b) for a,b in zip(dims,expected['dimensions_blender_xyz']))<.01,dims
    triangles=0
    for o in meshes:o.data.calc_loop_triangles();triangles+=len(o.data.loop_triangles)
    assert triangles==expected['triangles'],triangles
    results.append({'asset':name,'triangles':triangles,'source_and_fbx_meshes':len(meshes),'dimensions':dims,'uv_and_materials_present':True,'preview_helpers_excluded':True})
(root/'Documentation'/'Validation.json').write_text(json.dumps(results,indent=2));print('VALIDATED_WALL_DETAILS '+json.dumps(results))
