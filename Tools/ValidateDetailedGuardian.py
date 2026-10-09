import bpy,bmesh,json
from pathlib import Path
from mathutils import Vector
root=Path('F:/UnityProjects/Skybreak/ArtSource/Student1/DetailedGuardian')
name='WeatheredGuardianDetailed';expected=json.loads((root/'Documentation'/f'{name}.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(root/'Source'/f'{name}.blend'))
meshes=[o for o in bpy.data.collections['01_ASSET / Export'].objects if o.type=='MESH']
assert len(meshes)==expected['meshes'] and all(o.data.uv_layers and o.data.materials for o in meshes)
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(root/'Exports'/f'{name}.fbx'))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(meshes)==expected['meshes']
assert not any(o.type in ('CAMERA','LIGHT') for o in bpy.context.scene.objects)
corners=[o.matrix_world@Vector(v) for o in meshes for v in o.bound_box];dims=[max(v[i] for v in corners)-min(v[i] for v in corners) for i in range(3)]
assert max(abs(a-b) for a,b in zip(dims,expected['dimensions_blender_xyz']))<.01
triangles=0
for o in meshes:o.data.calc_loop_triangles();triangles+=len(o.data.loop_triangles)
assert triangles==expected['triangles']
for o in meshes:
    bm=bmesh.new();bm.from_mesh(o.data)
    assert bm.calc_volume(signed=True)>0, 'Inward normals: '+o.name
    assert all(e.is_manifold for e in bm.edges), 'Open geometry: '+o.name
    bm.free()
report={'triangles':triangles,'meshes':len(meshes),'dimensions':dims,'source_and_export_verified':True,'preview_helpers_excluded':True}
report['all_meshes_closed_with_outward_normals']=True
(root/'Documentation'/'Validation.json').write_text(json.dumps(report,indent=2));print('VALIDATED_DETAILED_GUARDIAN '+json.dumps(report))

