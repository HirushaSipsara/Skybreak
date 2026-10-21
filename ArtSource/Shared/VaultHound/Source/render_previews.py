"""Render the saved model and a short run sequence in a background Blender."""
import bpy
from pathlib import Path
from mathutils import Vector

folder=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(folder/"Source/VaultHound_Rigged.blend"))
scene=bpy.context.scene
rig=next(o for o in scene.objects if o.type=="ARMATURE")
camera=scene.camera
def aim(target):
    camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
def capture(filename):
    scene.render.filepath=str(folder/"Previews"/filename)
    bpy.ops.render.render(write_still=True)
scene.frame_set(1)
capture("VaultHound_ThreeQuarter.png")
camera.location=(0,-3,1.05)
aim((0,-.05,.57))
capture("VaultHound_Front.png")
camera.location=(3,.12,1.14)
aim((0,-.05,.57))
capture("VaultHound_Side.png")
camera.location=(2.1,-3,1.6)
aim((0,-.05,.57))
scene.render.resolution_x=600
scene.render.resolution_y=500
rig.animation_data.action=next(t.strips[0].action for t in rig.animation_data.nla_tracks if t.name=="Run")
(folder/"Previews/RunFrames").mkdir(exist_ok=True)
for i,f in enumerate(range(1,21,2)):
    scene.frame_set(f)
    capture("RunFrames/Run_%02d.png"%i)
rig.animation_data.action=next(t.strips[0].action for t in rig.animation_data.nla_tracks if t.name=="Lunge")
scene.frame_set(13)
capture("VaultHound_Lunge.png")
print("Rendered front, side, three-quarter, lunge and ten run frames.")
