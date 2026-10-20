"""Render sampled authored clips in Blender; assemble the frames with make_motion_preview.py."""
import bpy
import json
from pathlib import Path

base=Path(__file__).resolve().parent.parent
scene=bpy.context.scene
rig=scene.objects.get('CEW_Rig')
assert rig is not None
folder=base.parents[2]/'Tools/WardenTests/bin/AnimationFrames'
folder.mkdir(parents=True,exist_ok=True)
previous=(scene.render.engine,scene.render.resolution_x,scene.render.resolution_y,rig.animation_data.action,scene.frame_current)
try:
    scene.render.engine='BLENDER_WORKBENCH'
except TypeError as error:
    raise RuntimeError('Workbench render unavailable: '+str(error))
shading=scene.display.shading
allowed=lambda key:[v.identifier for v in shading.bl_rna.properties[key].enum_items]
assert 'MATERIAL' in allowed('color_type') and 'WORLD' in allowed('background_type')
shading.color_type='MATERIAL';shading.background_type='WORLD'
shading.show_shadows=True;shading.show_cavity=True
scene.render.resolution_x=scene.render.resolution_y=460
scene.render.resolution_percentage=100
scene.render.film_transparent=False
assert 'PNG' in [v.identifier for v in scene.render.image_settings.bl_rna.properties['file_format'].enum_items]
scene.render.image_settings.file_format='PNG'
manifest=[]
for name,last in [('Idle',61),('Glide',31),('TurnLeft',31),('TurnRight',31),('Retreat',31),('Attack',28),('Hit',13),('Death',46)]:
    rig.animation_data.action=bpy.data.actions['CEW_'+name]
    for step in range(8):
        frame=1+round((last-1)*step/7)
        scene.frame_set(frame)
        filename=folder/(str(len(manifest)).zfill(3)+'.png')
        scene.render.filepath=str(filename)
        bpy.ops.render.render(write_still=True)
        manifest.append({'path':str(filename),'clip':name,'frame':frame})
(folder/'manifest.json').write_text(json.dumps(manifest))
scene.render.engine,scene.render.resolution_x,scene.render.resolution_y,rig.animation_data.action,frame=previous
scene.frame_set(frame)
print('Rendered '+str(len(manifest))+' animation preview frames to '+str(folder))
