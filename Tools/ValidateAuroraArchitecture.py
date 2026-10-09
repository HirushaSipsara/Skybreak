import bpy, json
from pathlib import Path
from mathutils import Vector

root=Path('F:/UnityProjects/Skybreak/ArtSource/Student1/AuroraArchitecture')
results=[]
for name in ('VaultWallLantern','AuroraReactorShrine','AuroraTeleportGate'):
    bpy.ops.wm.open_mainfile(filepath=str(root/'Source'/f'{name}.blend'))
    collection=bpy.data.collections['01_ASSET / Export']
    source=[o for o in collection.objects if o.type=='MESH']
    expected=json.loads((root/'Documentation'/f'{name}_Report.json').read_text())
    assert len(source)==expected['meshes']
    assert all(o.data.materials for o in source)
    assert all(abs(s-1)<1e-4 for o in source for s in o.scale)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(root/'Exports'/f'{name}.fbx'))
    imported=[o for o in bpy.context.scene.objects if o.type=='MESH']
    assert len(imported)==len(source)
    assert not any(o.type in ('CAMERA','LIGHT') for o in bpy.context.scene.objects)
    corners=[o.matrix_world@Vector(v) for o in imported for v in o.bound_box]
    dimensions=[max(v[i] for v in corners)-min(v[i] for v in corners) for i in range(3)]
    assert max(abs(a-b) for a,b in zip(dimensions,expected['dimensions_xyz_metres']))<.02,dimensions
    triangles=0
    for o in imported:
        o.data.calc_loop_triangles();triangles+=len(o.data.loop_triangles)
    assert triangles==expected['evaluated_triangles']
    results.append({'asset':name,'meshes':len(imported),'triangles':triangles,'fbx_dimensions_metres':dimensions,'source_scale_applied':True,'preview_helpers_excluded':True})
(root/'Documentation'/'Validation.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
print('VALIDATED_AURORA_ARCHITECTURE '+json.dumps(results))
