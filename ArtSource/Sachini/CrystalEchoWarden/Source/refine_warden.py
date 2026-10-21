"""Refine the compact Warden in its existing Blender scene. Front is -Y."""
import bpy
import math
import shutil
from pathlib import Path
from mathutils import Vector

scene = bpy.context.scene
root = scene.objects.get('CrystalEchoWarden_Root')
assert root is not None, 'Open the Crystal Echo Warden scene first.'
collection = root.users_collection[0]
out = Path(bpy.data.filepath).parent.parent
backup = out / 'Source' / 'CrystalEchoWarden_Blockout_v1.blend'
if not backup.exists():
    shutil.copy2(bpy.data.filepath, backup)

def material(name, color, metal, rough, glow=0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    b.inputs['Base Color'].default_value = (*color, 1)
    b.inputs['Metallic'].default_value = metal
    b.inputs['Roughness'].default_value = rough
    b.inputs['Emission Color'].default_value = (*color, 1)
    b.inputs['Emission Strength'].default_value = glow
    m.diffuse_color = (*color, 1)
    return m

blue = material('Warden Blue Armour', (.035,.095,.19), .55, .36)
dark = material('Warden Graphite', (.016,.027,.044), .65, .34)
joint = material('Warden Dark Joint', (.012,.018,.027), .35, .4)
cyan = material('Warden Cyan Energy', (.015,.65,.85), .3, .3, .9)
amber = material('Warden Amber Accent', (.95,.36,.04), .45, .35, .3)
trim = material('Warden Edge Alloy', (.18,.28,.34), .7, .3)

def mesh(name, verts, faces, mat, bevel=0):
    data = bpy.data.meshes.new(name+' refined mesh')
    data.from_pydata(verts, [], faces)
    data.update()
    obj = scene.objects.get(name)
    if obj is None:
        obj = bpy.data.objects.new(name, data)
        collection.objects.link(obj)
    else:
        obj.data = data
        obj.modifiers.clear()
    obj.parent = root
    obj.location = (0,0,0)
    obj.rotation_euler = (0,0,0)
    obj.scale = (1,1,1)
    data.materials.append(mat)
    if bevel:
        mod = obj.modifiers.new('Machined edges', 'BEVEL')
        mod.width = bevel
        mod.segments = 2
    return obj

def plate(name, profile, front, back, mat, bevel=.015):
    # profile is counterclockwise in the front X/Z view.
    n = len(profile)
    verts = [(x,front,z) for x,z in profile]+[(x,back,z) for x,z in profile]
    faces = [tuple(range(n)),tuple(reversed(range(n,2*n)))]
    faces += [(i,n+i,n+(i+1)%n,(i+1)%n) for i in range(n)]
    return mesh(name,verts,faces,mat,bevel)

def diamond(name, cx, cy, cz, w, h, depth, mat):
    verts=[(cx,cy,cz+h/2),(cx-w/2,cy,cz),(cx,cy,cz-h/2),
           (cx+w/2,cy,cz),(cx,cy-depth,cz+.015),(cx,cy+depth*.5,cz)]
    faces=[(i,(i+1)%4,4) for i in range(4)] + [((i+1)%4,i,5) for i in range(4)]
    return mesh(name,verts,faces,mat)

# Shoulders broad enough to feel protective, lower hull tapered around the hover unit.
plate('Main hull',[(-.28,-.14),(.28,-.14),(.45,.12),(.45,.35),(.30,.46),(-.30,.46),(-.45,.35),(-.45,.12)],-.24,.24,dark,.045)
plate('Blue chest panel',[(-.20,-.07),(.20,-.07),(.39,.16),(.38,.34),(.24,.41),(-.24,.41),(-.38,.34),(-.39,.16)],-.285,-.22,blue,.022)
plate('Core socket',[(-.19,.17),(0,-.085),(.19,.17),(0,.405)],-.323,-.28,trim,.012)
plate('Core recess',[(-.16,.17),(0,-.045),(.16,.17),(0,.365)],-.344,-.32,joint,.006)
diamond('Cyan resonance core',0,-.365,.16,.245,.355,.065,cyan)

# Helmet becomes a small faceted mask rather than a second rectangular box.
plate('Head shell',[(-.17,.49),(.17,.49),(.29,.63),(.27,.79),(.16,.86),(-.16,.86),(-.27,.79),(-.29,.63)],-.21,.18,blue,.025)
plate('Visor recess',[(-.245,.685),(0,.615),(.245,.685),(.235,.75),(0,.69),(-.235,.75)],-.244,-.21,joint,.008)
plate('CEW_Visor',[(-.222,.697),(0,.637),(.222,.697),(.222,.724),(0,.668),(-.222,.724)],-.258,-.244,cyan,.004)
plate('Helmet crest',[(-.055,.79),(.055,.79),(.075,.84),(-.075,.84)],-.226,-.208,trim,.007)

# Upright crystal fins are the recognizable hearing sensors.
for side, suffix in [(-1,'L'),(1,'R')]:
    mount=scene.objects['Sensor mount '+suffix]
    mount.location=(side*.34,0,.69)
    mount.scale=(.75,1,.8)
    diamond('Sensor frame '+suffix,side*.46,0,.73,.20,.40,.065,trim)
    diamond('Crystal sound sensor '+suffix,side*.46,-.06,.73,.14,.32,.045,cyan)
    # Matching short arms with an intentional inward curl to each claw.
    prof=[(.45,.32),(.55,.34),(.64,.22),(.65,.03),(.53,-.035),(.49,.12)]
    if side<0: prof=list(reversed([(-x,z) for x,z in prof]))
    plate('Arm upper '+suffix,prof,-.11,.10,blue,.025)
    elbow=scene.objects['Arm joint '+suffix]
    elbow.location=(side*.59,-.01,-.045)
    elbow.scale=(.78,.78,.78)
    prof=[(.53,-.065),(.66,-.05),(.70,-.24),(.59,-.28),(.53,-.17)]
    if side<0: prof=list(reversed([(-x,z) for x,z in prof]))
    plate('Claw block '+suffix,prof,-.14,.07,dark,.02)
    for name,profile in [('Claw upper ',[(.58,-.22),(.61,-.25),(.58,-.34),(.55,-.36),(.54,-.28)]),
                         ('Claw lower ',[(.66,-.23),(.70,-.23),(.70,-.35),(.65,-.40),(.63,-.37),(.66,-.32)])]:
        if side<0: profile=list(reversed([(-x,z) for x,z in profile]))
        plate(name+suffix,profile,-.16,-.05,trim,.008)
    prof=[(.285,.13),(.31,.15),(.35,.22),(.32,.24)]
    if side<0: prof=list(reversed([(-x,z) for x,z in prof]))
    plate('Chest signal '+suffix,prof,-.31,-.285,amber,.004)

# Downward pointed emitter, compact contrasting collar.
emitter=scene.objects['Cyan hover emitter']
emitter.rotation_euler.x=math.pi
emitter.location.z=-.37
collar=scene.objects['Hover stabilizer']
collar.data.materials.clear(); collar.data.materials.append(trim)
strip=scene.objects['Amber status strip']
strip.location=(0,-.17,-.235); strip.scale=(.55,1,.65)

scene.world.use_nodes=True
bg=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND')
bg.inputs['Color'].default_value=(.025,.042,.08,1)
bg.inputs['Strength'].default_value=.3
for o in scene.objects:
    if o.type=='LIGHT':
        if o.location.x>1: o.data.energy=240; o.data.color=(.78,.88,1)
        elif o.location.x<-1: o.data.energy=180; o.data.color=(.22,.65,1)
        else: o.data.energy=260; o.data.color=(.45,.65,1)
floor=scene.objects['Preview floor (not export)']
floor.scale=(.72,.72,1)
floor.data.materials.clear()
floor.data.materials.append(material('Warden Studio Floor',(.015,.025,.045),.2,.6))
camera=scene.camera
camera.location=(1.95,-3.9,1.65)
camera.rotation_euler=(Vector((0,0,.20))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.lens=68
scene.render.resolution_x=900
scene.render.resolution_y=900
scene.render.resolution_percentage=100
scene.render.filepath=str(out/'Evidence'/'CrystalEchoWarden_Refined.png')
# Show the model from the prepared camera on opening the file.
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_perspective='CAMERA'
        area.spaces.active.overlay.show_overlays=False
        area.spaces.active.shading.color_type='MATERIAL'
bpy.ops.object.select_all(action='DESELECT')
root.select_set(True)
bpy.context.view_layer.objects.active=root
bpy.ops.wm.save_as_mainfile(filepath=str(out/'Source'/'CrystalEchoWarden.blend'))
bpy.ops.render.render(write_still=True)
print('Saved refined Warden and preview; initial blockout preserved at '+str(backup))
