import bpy
import json
from pathlib import Path
from mathutils import Vector

root = Path('F:/UnityProjects/Skybreak/ArtSource/Student1/CrystalPedestal')
collection = bpy.data.collections.get('01_ASSET / Export these meshes')
assert collection is not None
meshes = [o for o in collection.objects if o.type == 'MESH']
assert len(meshes) == 4
assert all(o.data.uv_layers for o in meshes)
assert all(o.data.materials for o in meshes)
assert all(abs(s-1) < 1e-5 for o in meshes for s in o.scale)
blend_ok = {'meshes': len(meshes), 'uv_layers_present': True, 'materials_present': True, 'scale_applied': True}

# Import the FBX into a second isolated scene to verify actual export content.
scene = bpy.data.scenes.new('FBX_Reimport_Validation')
bpy.context.window.scene = scene
bpy.ops.import_scene.fbx(filepath=str(root / 'Exports/CrystalPedestal.fbx'))
imported = [o for o in scene.objects if o.type == 'MESH']
assert len(imported) == 4
assert not any(o.type in {'LIGHT', 'CAMERA'} for o in scene.objects)
corners = [o.matrix_world @ Vector(v) for o in imported for v in o.bound_box]
dimensions = [max(v[i] for v in corners)-min(v[i] for v in corners) for i in range(3)]
assert max(abs(a-b) for a,b in zip(dimensions, (1.18, 1.18, 2.63))) < .01, dimensions
triangles = 0
for o in imported:
    o.data.calc_loop_triangles()
    triangles += len(o.data.loop_triangles)
assert triangles == 140, triangles
report = {'blend': blend_ok, 'fbx': {'mesh_objects': len(imported), 'dimensions_metres': dimensions,
          'triangles': triangles, 'preview_objects_excluded': True},
          'unity_import': 'Instructions provided; no Unity model import or scene replacement was performed.'}
(root / 'Documentation/Validation.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print('VALIDATED_CRYSTAL_PACKAGE ' + json.dumps(report))
