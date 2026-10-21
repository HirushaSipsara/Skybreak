"""Arc model; references the shared ArcExplorer prefab silhouette and palette.
Run in Blender's Scripting workspace. Creates a new scene, preserving other scenes.
"""
from pathlib import Path
import bpy, math, json
from mathutils import Vector

BASE = Path(__file__).resolve().parents[1]
for folder in ('Exports', 'Evidence'):
    (BASE / folder).mkdir(exist_ok=True)
scene = bpy.data.scenes.new('Arc Explorer - Model Studio')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
model = bpy.data.collections.new('ArcExplorer_Model')
studio = bpy.data.collections.new('PreviewOnly')
scene.collection.children.link(model)
scene.collection.children.link(studio)
parts = []

def material(name, color, metal=0, rough=.48, emission=0):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=metal; p.inputs['Roughness'].default_value=rough
    p.inputs['Emission Color'].default_value=(*color,1); p.inputs['Emission Strength'].default_value=emission
    return m
teal=material('Arc - teal fabric',(.035,.20,.23))
orange=material('Arc - orange rescue vest',(.78,.255,.055))
dark=material('Arc - graphite rubber',(.022,.036,.047))
armor=material('Arc - blue grey armor',(.19,.28,.31),.35)
cream=material('Arc - reflective ivory',(.82,.83,.67),.15)
skin=material('Arc - warm skin',(.49,.275,.15))
hair=material('Arc - dark hair',(.035,.021,.017))
cyan=material('Arc - cyan illuminated lens',(.025,.68,.79),.35,.2,.65)
glass=material('Arc - dark visor surround',(.012,.08,.1),.55,.18)

def relocate(obj, collection):
    for c in list(obj.users_collection): c.objects.unlink(obj)
    collection.objects.link(obj)

def finish(obj,name,mat,bone,bevel=0):
    obj.name=name; relocate(obj,model); obj.data.materials.append(mat)
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=obj.modifiers.new('Soft manufactured edges','BEVEL'); mod.width=bevel; mod.segments=2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj['attachment_bone']=bone
    parts.append(obj)
    return obj

def box(name,loc,size,mat,bone='Chest',bevel=.012):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc)
    o=bpy.context.object; o.dimensions=size
    return finish(o,name,mat,bone,min(bevel,min(size)*.22))

def limb(name,a,b,width,depth,mat,bone):
    a,b=Vector(a),Vector(b)
    o=box(name,(a+b)/2,(width,depth,(b-a).length),mat,bone,.025)
    o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    return o

def sphere(name,loc,scale,mat,bone):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=6,radius=1,location=loc)
    o=bpy.context.object;o.scale=scale
    return finish(o,name,mat,bone)

# Blender front is -Y; Z up. Feet sit on Z=0.
box('Pelvis suit',(0,0,.85),(.44,.29,.22),teal,'Hips',.035)
box('Torso tailored rescue suit',(0,0,1.16),(.53,.31,.48),teal,'Chest',.05)
box('Vest orange shell',(0,-.174,1.18),(.46,.085,.39),orange,bevel=.035)
box('Vest centre fastening',(0,-.223,1.18),(.035,.017,.35),dark)
for s in (-1,1):
    box('Reflective vest strip',(.155*s,-.222,1.21),(.035,.016,.30),cream)
    box('Vest utility pocket',(.08*s,-.236,1.065),(.10,.04,.08),orange)
    box('Pocket clasp',(.08*s,-.263,1.09),(.035,.012,.02),dark)
box('Belt',(0,0,.905),(.48,.33,.072),dark,'Hips')
box('Belt buckle',(0,-.18,.905),(.095,.027,.055),armor,'Hips')
box('Buckle inset',(0,-.198,.905),(.055,.012,.025),cyan,'Hips')
box('Collar',(0,0,1.43),(.25,.24,.09),dark,'Neck')
box('Neck',(0,0,1.48),(.16,.17,.13),skin,'Neck')
box('Head',(0,-.005,1.685),(.36,.32,.37),skin,'Head',.055)
box('Hair cap',(0,.005,1.866),(.385,.34,.105),hair,'Head',.035)
box('Hair rear',(0,.145,1.77),(.35,.055,.18),hair,'Head')
for x,z,ang in [(-.115,1.827,-.16),(0,1.845,.1),(.11,1.85,.18)]:
    o=box('Swept hair fringe',(x,-.157,z),(.14,.06,.085),hair,'Head');o.rotation_euler.y=ang
box('Visor gasket',(0,-.171,1.715),(.345,.055,.115),dark,'Head',.022)
box('Cyan visor',(0,-.205,1.718),(.295,.024,.072),cyan,'Head',.014)
box('Visor reflection',(-.075,-.22,1.737),(.092,.006,.009),cream,'Head',.002)
box('Nose',(0,-.173,1.635),(.065,.06,.065),skin,'Head')
box('Mouth',(0,-.171,1.582),(.075,.007,.009),hair,'Head',.002)
for s in (-1,1):
    sphere('Ear',(.187*s,0,1.685),(.034,.045,.065),skin,'Head')
    box('Visor side band',(.188*s,0,1.717),(.018,.29,.03),dark,'Head')
box('Comms earpiece',(.215,.005,1.69),(.035,.085,.095),armor,'Head')
limb('Comms boom',(.223,-.025,1.67),(.15,-.20,1.61),.016,.016,dark,'Head')
sphere('Microphone',(.15,-.20,1.61),(.022,.024,.018),dark,'Head')

bones=[('Root',(0,0,0),(0,0,.15),None),('Hips',(0,0,.81),(0,0,1.0),'Root'),('Spine',(0,0,1.0),(0,0,1.19),'Hips'),('Chest',(0,0,1.19),(0,0,1.43),'Spine'),('Neck',(0,0,1.43),(0,0,1.53),'Chest'),('Head',(0,0,1.53),(0,0,1.91),'Neck')]
for s,side in [(-1,'L'),(1,'R')]:
    shoulder=(s*.32,0,1.37); elbow=(s*.43,0,1.115); wrist=(s*.49,-.015,.90)
    upper='UpperArm.'+side; fore='Forearm.'+side; hand='Hand.'+side
    bones += [(upper,shoulder,elbow,'Chest'),(fore,elbow,wrist,upper),(hand,wrist,(s*.51,-.015,.78),fore)]
    limb('Sleeve upper '+side,shoulder,elbow,.19,.23,teal,upper)
    box('Shoulder plate '+side,(s*.33,0,1.36),(.25,.29,.16),armor,upper,.025)
    box('Shoulder safety stripe '+side,(s*.33,-.153,1.36),(.13,.015,.035),orange,upper)
    sphere('Elbow joint '+side,elbow,(.09,.105,.085),dark,fore)
    limb('Forearm sleeve '+side,elbow,wrist,.155,.19,teal,fore)
    box('Wrist cuff '+side,(s*.485,-.01,.94),(.18,.23,.085),armor,fore)
    box('Glove palm '+side,(s*.51,-.015,.82),(.16,.19,.14),dark,hand,.025)
    for i in range(3):
        box('Glove finger seam '+side,(s*.51+(.04*(i-1)),-.115,.806),(.022,.018,.075),armor,hand,.004)
    sphere('Glove thumb '+side,(s*.438,-.06,.84),(.039,.052,.067),dark,hand)
    thigh='Thigh.'+side; shin='Shin.'+side; foot='Foot.'+side
    hip=(s*.14,0,.84); knee=(s*.15,0,.49); ankle=(s*.15,0,.14)
    bones += [(thigh,hip,knee,'Hips'),(shin,knee,ankle,thigh),(foot,ankle,(s*.15,-.18,.09),shin)]
    limb('Trouser thigh '+side,hip,knee,.205,.265,teal,thigh)
    limb('Trouser shin '+side,knee,ankle,.18,.23,teal,shin)
    box('Knee plate '+side,(s*.15,-.143,.49),(.17,.055,.145),armor,shin,.02)
    box('Knee reflective bar '+side,(s*.15,-.175,.51),(.10,.012,.02),cream,shin)
    box('Boot '+side,(s*.15,-.045,.14),(.235,.34,.24),dark,foot,.03)
    box('Boot toe cap '+side,(s*.15,-.17,.105),(.24,.115,.115),armor,foot,.022)
    box('Boot sole '+side,(s*.15,-.045,.025),(.25,.36,.05),dark,foot,.008)
    for z in (.16,.205): box('Boot strap '+side,(s*.15,-.16,z),(.17,.035,.024),armor,foot)
    box('Backpack shoulder strap '+side,(s*.195,-.155,1.36),(.055,.045,.14),dark)

# Rescue equipment; no weapon is included in this character asset.
box('Backpack shell',(0,.245,1.17),(.42,.22,.47),armor,bevel=.035)
box('Backpack orange panel',(0,.367,1.17),(.30,.03,.33),orange,bevel=.022)
box('Backpack rescue cross vertical',(0,.389,1.22),(.042,.015,.15),cream)
box('Backpack rescue cross horizontal',(0,.39,1.22),(.135,.015,.04),cream)
for x in (-.09,-.045,0,.045,.09): box('Backpack vent',(x,.39,1.075),(.019,.016,.055),dark)
box('Power unit',(0,.39,1.37),(.18,.035,.04),dark)
for x in (-.06,0,.06): box('Power status diode',(x,.414,1.37),(.025,.013,.016),cyan)
box('Belt tool pouch',(.30,.03,.86),(.14,.19,.18),orange,'Hips')
box('Pouch flap',(.30,-.073,.9),(.145,.025,.07),dark,'Hips')
box('Forearm scanner',(-.475,-.134,1.01),(.13,.04,.10),dark,'Forearm.L')
box('Scanner screen',(-.475,-.158,1.02),(.095,.012,.047),cyan,'Forearm.L')

# Explicit rigid weights suit the separate low-poly armor pieces. No baked animation.
bpy.ops.object.select_all(action='DESELECT')
armdata=bpy.data.armatures.new('ArcExplorer_Skeleton')
rig=bpy.data.objects.new('ArcExplorer_Rig',armdata); model.objects.link(rig)
bpy.context.view_layer.objects.active=rig;rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for name,head,tail,parent in bones:
    b=armdata.edit_bones.new(name);b.head=head;b.tail=tail
    if parent:b.parent=armdata.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')
rig.show_in_front=True
for obj in parts:
    group=obj.vertex_groups.new(name=obj['attachment_bone'])
    group.add(list(range(len(obj.data.vertices))),1,'REPLACE')
    mod=obj.modifiers.new('Arc posing rig','ARMATURE');mod.object=rig
    obj.parent=rig
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(island_margin=.025)
    bpy.ops.object.mode_set(mode='OBJECT')

world=bpy.data.worlds.new('Arc studio world');world.use_nodes=True;scene.world=world
next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs[0].default_value=(.07,.1,.14,1)
next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs[1].default_value=.4
def aim(obj,target):obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
for name,loc,power,size,color in [('Key',(-3,-4,5),650,4,(1,.86,.72)),('Fill',(4,-2,3),450,3,(.65,.86,1)),('Rim',(1,3,4),800,3,(.4,.85,1))]:
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;d.color=color
    o=bpy.data.objects.new(name,d);studio.objects.link(o);o.location=loc;aim(o,(0,0,1))
camdata=bpy.data.cameras.new('Arc presentation camera');cam=bpy.data.objects.new('Arc presentation camera',camdata);studio.objects.link(cam)
cam.location=(3,-5,2.7);aim(cam,(0,0,.97));camdata.type='ORTHO';camdata.ortho_scale=2.55;scene.camera=cam
scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
scene.render.engine='CYCLES';scene.cycles.samples=24
scene.render.filepath=str(BASE/'Evidence/ArcExplorer_ThreeQuarter.png')

# Select only the character for FBX export. Camera and lights stay outside the export.
bpy.ops.object.select_all(action='DESELECT')
for obj in parts+[rig]:obj.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(BASE/'Exports/ArcExplorer.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,use_mesh_modifiers=True)
triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in parts)
assert all(o.data.uv_layers for o in parts)
assert all(all(v.groups for v in o.data.vertices) for o in parts)
report={'asset':'ArcExplorer','design_reference':'Existing shared ArcExplorer prefab design','meshes':len(parts),'triangles':triangles,'bones':len(bones),'uv_mapped':True,'weighted_vertices':'all; rigid per-part weights','height_metres':max((o.matrix_world@Vector(c)).z for o in parts for c in o.bound_box),'front':'Blender -Y; exported with -Z forward and Y up','animations':0,'unity_tested':False}
(BASE/'Evidence/Validation.json').write_text(json.dumps(report,indent=2))
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_distance=3.6
            area.spaces.active.region_3d.view_location=Vector((0,0,1))
            area.spaces.active.region_3d.view_rotation=cam.rotation_euler.to_quaternion()
            area.spaces.active.shading.color_type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=str(BASE/'Source/ArcExplorer.blend'))
print(json.dumps(report))
