"""AI-assisted Vault Hound model, rig, animation and export construction.

Creates a separate scene and writes only that scene to the handoff files.
Run from Blender's Python console with __file__ defined, or with --python.
"""
import bpy
import math
import json
from pathlib import Path
from mathutils import Vector

BASE = Path(__file__).resolve().parents[1]
for directory in ("Source", "Exports", "Textures", "Previews", "Evidence", "References"):
    (BASE / directory).mkdir(parents=True, exist_ok=True)
scene = bpy.data.scenes.new("Vault Hound Workshop")
if bpy.context.window:
    bpy.context.window.scene = scene
if bpy.app.background:
    for other in list(bpy.data.scenes):
        if other != scene:
            bpy.data.scenes.remove(other)
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1
scene.render.fps = 30
parts_collection = bpy.data.collections.new("01 Editable armor and mechanism")
scene.collection.children.link(parts_collection)
rig_collection = bpy.data.collections.new("02 Animation rig")
scene.collection.children.link(rig_collection)
studio = bpy.data.collections.new("03 Preview studio - not exported")
scene.collection.children.link(studio)

palette = [(0.055,.09,.11), (.12,.25,.29), (.79,.76,.65), (.27,.31,.32),
           (.48,.32,.16), (.94,.34,.035), (1,.57,.065), (.015,.022,.027)]
def linear(x):
    return x/12.92 if x <= .04045 else ((x+.055)/1.055)**2.4

def palette_image(name, emission=False):
    image = bpy.data.images.new(name, width=256, height=256, alpha=False)
    pixels = []
    for y in range(256):
        for x in range(256):
            idx = min(7, (x//64)+(y//128)*4)
            color = palette[idx] if not emission or idx in (5,6) else (0,0,0)
            pixels.extend((*color,1))
    image.pixels.foreach_set(pixels)
    image.filepath_raw = str(BASE / "Textures" / (name+".png"))
    image.file_format = "PNG"
    image.save()
    return image

base_image = palette_image("VaultHound_BaseColor")
emission_image = palette_image("VaultHound_Emission", True)
material = bpy.data.materials.new("VaultHound_Atlas")
material.use_nodes = True
nodes = material.node_tree.nodes
shader = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
shader.inputs["Metallic"].default_value = .45
shader.inputs["Roughness"].default_value = .38
for image, socket in ((base_image,"Base Color"),(emission_image,"Emission Color")):
    node = nodes.new("ShaderNodeTexImage")
    node.image = image
    node.interpolation = "Closest"
    material.node_tree.links.new(node.outputs["Color"],shader.inputs[socket])
shader.inputs["Emission Strength"].default_value = 1.8
parts = []
weights = {}

def finish(obj, name, color, bone, bevel=0):
    obj.name = name
    for collection in list(obj.users_collection):
        collection.objects.unlink(obj)
    parts_collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new("Armor edge", "BEVEL")
        mod.width = bevel
        mod.segments = 1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.clear()
    obj.data.materials.append(material)
    for layer in list(obj.data.uv_layers):
        obj.data.uv_layers.remove(layer)
    uv = obj.data.uv_layers.new(name="UVMap")
    uv.active_render = True
    u = ((color%4)+.5)/4
    v = ((color//4)+.5)/2
    for loop in uv.data:
        loop.uv = (u,v)
    for face in obj.data.polygons:
        face.use_smooth = False
    weights[obj.name] = bone
    parts.append(obj)
    return obj

def box(name, pos, size, color, bone="Body", bevel=.015, rotation=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    obj=bpy.context.object
    obj.dimensions=size
    if rotation:
        obj.rotation_euler=rotation
    return finish(obj,name,color,bone,min(bevel,min(size)*.18))

def joint(name,pos,radius,length,color,bone,axis=(1,0,0),vertices=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=length, location=pos)
    obj=bpy.context.object
    obj.rotation_euler=Vector(axis).to_track_quat('Z','Y').to_euler()
    return finish(obj,name,color,bone,.003)

def beam(name,a,b,width,depth,color,bone):
    middle=(Vector(a)+Vector(b))*.5
    direction=Vector(b)-Vector(a)
    obj=box(name,middle,(width,depth,direction.length),color,bone,.012)
    obj.rotation_euler=direction.to_track_quat('Z','Y').to_euler()
    return obj

def wedge(name, verts, color, bone):
    faces=[(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)]
    mesh=bpy.data.meshes.new(name+"Mesh")
    mesh.from_pydata(verts,[],faces)
    mesh.update()
    obj=bpy.data.objects.new(name,mesh)
    parts_collection.objects.link(obj)
    return finish(obj,name,color,bone,.007)

# The spine, undercarriage and overlapping armor make a single canine silhouette.
box("Power core and undercarriage",(0,.05,.64),(.42,.82,.29),0)
box("Front chest guard",(0,-.34,.64),(.43,.24,.34),1)
box("Rear hip guard",(0,.39,.64),(.43,.27,.30),1)
box("Belly reinforcement",(0,.04,.48),(.27,.56,.075),7)
for i,y in enumerate((-.22,.01,.24)):
    box("Spine plate %02d"%i,(0,y,.84),(.46,.215,.11),1,bevel=.025)
    box("Orange spine seam %02d"%i,(0,y+.095,.812),(.32,.022,.035),5)
    for side in (-1,1):
        box("Flank plate %d %d"%(i,side),(side*.243,y,.69),(.065,.19,.20),1,bevel=.02,
            rotation=(0,side*.19,0))
        box("Flank energy slot %d %d"%(i,side),(side*.28,y,.66),(.012,.115,.023),5)
box("Back cream shield",(0,.36,.82),(.30,.21,.10),2)
joint("Sensor turntable",(0,.16,.94),.055,.065,4,"Sensor",axis=(0,0,1))
box("Dorsal tracking sensor",(0,.16,1.02),(.13,.12,.12),0,"Sensor",.012)
box("Dorsal orange lens",(0,.095,1.02),(.078,.012,.07),6,"Sensor",.008)
box("Short rear stabilizer",(0,.69,.76),(.12,.32,.075),1,"Tail",.012,rotation=(.28,0,0))
box("Stabilizer light",(0,.845,.80),(.10,.018,.026),5,"Tail",.004)

# Angular skull, raised ears, circular optics and a narrow snout.
beam("Neck joint",(0,-.33,.72),(0,-.55,.80),.21,.21,3,"Neck")
wedge("Canine skull",[(-.20,-.48,.94),(.20,-.48,.94),(.155,-.77,.85),(-.155,-.77,.85),
                      (-.20,-.48,.70),(.20,-.48,.70),(.155,-.77,.65),(-.155,-.77,.65)],1,"Head")
box("Cream forehead stripe",(0,-.64,.943),(.105,.20,.03),2,"Head",.007,rotation=(.25,0,0))
wedge("Long armored muzzle",[(-.13,-.73,.80),(.13,-.73,.80),(.10,-1.00,.69),(-.10,-1.00,.69),
                             (-.13,-.73,.65),(.13,-.73,.65),(.10,-1.00,.56),(-.10,-1.00,.56)],2,"Head")
box("Dark nose cap",(0,-1.01,.63),(.20,.045,.11),0,"Head",.012,rotation=(.10,0,0))
box("Articulated lower jaw",(0,-.85,.577),(.17,.28,.055),0,"Jaw",.008)
for side,label in ((-1,"L"),(1,"R")):
    joint("Eye socket "+label,(side*.152,-.78,.81),.069,.055,0,"Head",axis=(side*.32,-1,.14))
    joint("Brass optic ring "+label,(side*.16,-.806,.814),.054,.014,4,"Head",axis=(side*.32,-1,.14))
    joint("Orange sensor eye "+label,(side*.163,-.819,.816),.039,.012,6,"Head",axis=(side*.32,-1,.14))
    joint("Jaw hinge "+label,(side*.17,-.58,.72),.046,.025,4,"Head")
    x=side*.145
    wedge("Pointed ear "+label,[(x-.065,-.50,.91),(x+.065,-.50,.91),(x+.025,-.51,1.145),(x-.015,-.51,1.145),
                               (x-.065,-.42,.91),(x+.065,-.42,.91),(x+.025,-.44,1.145),(x-.015,-.44,1.145)],0,"Head")
    box("Ear inset "+label,(x,-.511,1.015),(.036,.012,.11),2,"Head",.004,rotation=(.13,0,0))
    box("Cheek armor "+label,(side*.205,-.57,.80),(.055,.19,.16),2,"Head",.012)

leg_points={}
for row,y in (("Front",-.36),("Rear",.39)):
    for side,label in ((-1,"L"),(1,"R")):
        name=row+"_"+label
        hip=Vector((side*.26,y,.69))
        knee=Vector((side*.33,y+(.055 if row=="Rear" else .055),.38))
        ankle=Vector((side*.34,y-.075,.12))
        toe=ankle+Vector((0,-.13,-.04))
        leg_points[name]=(hip,knee,ankle,toe)
        upper=name+"_Upper";lower=name+"_Lower";paw=name+"_Paw"
        joint(name+" hip bearing",hip,.092,.125,0,upper)
        joint(name+" hip bronze ring",hip+Vector((side*.075,0,0)),.068,.022,4,upper)
        joint(name+" hip hub",hip+Vector((side*.09,0,0)),.034,.018,3,upper)
        beam(name+" upper leg",hip,knee,.15,.16,1,upper)
        plate=beam(name+" cream shoulder",hip+Vector((side*.06,-.025,-.025)),knee+Vector((side*.065,-.035,.04)),.15,.19,2,upper)
        joint(name+" knee",knee,.068,.11,0,lower)
        joint(name+" knee ring",knee+Vector((side*.064,0,0)),.044,.018,4,lower)
        beam(name+" shin piston",knee,ankle,.095,.115,3,lower)
        beam(name+" shin armor",knee+Vector((0,-.045,-.035)),ankle+Vector((0,-.045,.025)),.13,.10,2,lower)
        box(name+" ankle cuff",ankle+Vector((0,0,.035)),(.145,.15,.09),1,lower)
        box(name+" paw",ankle+Vector((0,-.07,-.065)),(.205,.255,.11),0,paw,.018)
        for i,x in enumerate((-.068,0,.068)):
            box(name+" claw %d"%i,ankle+Vector((x,-.205,-.077)),(.05,.13,.06),3,paw,.006,rotation=(.12,0,0))

# Explicit parenting and rigid weights preserve hard mechanical armor under posing.
arm_data=bpy.data.armatures.new("VaultHound_Skeleton")
rig=bpy.data.objects.new("VaultHound_Rig",arm_data)
rig_collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig
rig.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
def bone(name,head,tail,parent=None):
    b=arm_data.edit_bones.new(name)
    b.head=head;b.tail=tail
    if parent:b.parent=arm_data.edit_bones[parent]
    return b
bone("Root",(0,0,0),(0,0,.20))
bone("Body",(0,.15,.62),(0,-.25,.66),"Root")
bone("Neck",(0,-.30,.71),(0,-.53,.79),"Body")
bone("Head",(0,-.53,.79),(0,-.83,.74),"Neck")
bone("Jaw",(0,-.69,.61),(0,-.94,.59),"Head")
bone("Tail",(0,.48,.73),(0,.80,.80),"Body")
bone("Sensor",(0,.16,.90),(0,.16,1.06),"Body")
for name,(hip,knee,ankle,toe) in leg_points.items():
    bone(name+"_Upper",hip,knee,"Body")
    bone(name+"_Lower",knee,ankle,name+"_Upper")
    bone(name+"_Paw",ankle,toe,name+"_Lower")
bpy.ops.object.mode_set(mode="OBJECT")
rig.show_in_front=True
for obj in parts:
    group=obj.vertex_groups.new(name=weights[obj.name])
    group.add(range(len(obj.data.vertices)),1,"REPLACE")
    mod=obj.modifiers.new("Mechanical skeleton","ARMATURE")
    mod.object=rig
    obj.parent=rig
    obj["bone"]=weights[obj.name]
    obj["palette_tile"]=int(round(obj.data.uv_layers.active.data[0].uv.x*4-.5))

clips={"Idle":(61,True),"Walk":(31,True),"Run":(21,True),"Alert":(31,False),
       "Sniff":(61,True),"Lunge":(25,False),"Hit":(13,False),"Death":(46,False)}
actions={}
for name,(end,loop) in clips.items():
    rig.animation_data_clear()
    action=bpy.data.actions.new(name)
    action.use_fake_user=True
    rig.animation_data_create().action=action
    for frame in range(1,end+1):
        t=(frame-1)/(end-1)
        wave=math.sin(t*2*math.pi)
        for p in rig.pose.bones:
            p.rotation_mode="XYZ";p.rotation_euler=(0,0,0);p.location=(0,0,0);p.scale=(1,1,1)
        body=rig.pose.bones["Body"]
        head=rig.pose.bones["Head"]
        sensor=rig.pose.bones["Sensor"]
        if name=="Idle":
            body.rotation_euler.x=.012*wave
            sensor.rotation_euler.y=.13*wave
            rig.pose.bones["Tail"].rotation_euler.z=.04*wave
        elif name in ("Walk","Run"):
            amplitude=.27 if name=="Walk" else .45
            for leg in leg_points:
                offset=0 if leg in ("Front_L","Rear_R") else math.pi
                stride=math.sin(t*2*math.pi+offset)
                rig.pose.bones[leg+"_Upper"].rotation_euler.x=amplitude*stride
                rig.pose.bones[leg+"_Lower"].rotation_euler.x=-amplitude*.6*stride
                rig.pose.bones[leg+"_Paw"].rotation_euler.x=-amplitude*.4*stride
            body.rotation_euler.x=(.018 if name=="Walk" else .045)*math.sin(t*4*math.pi)
        elif name=="Alert":
            head.rotation_euler.x=-.12*math.sin(t*math.pi)
            head.rotation_euler.z=.18*math.sin(t*2*math.pi)
            sensor.rotation_euler.y=.35*math.sin(t*2*math.pi)
        elif name=="Sniff":
            head.rotation_euler.x=.20+.08*wave
            head.rotation_euler.z=.10*math.sin(t*4*math.pi)
            rig.pose.bones["Neck"].rotation_euler.x=.08
        elif name=="Lunge":
            p=math.sin(t*math.pi)
            body.rotation_euler.x=.16*math.sin(t*2*math.pi)
            head.rotation_euler.x=-.10*p
            rig.pose.bones["Jaw"].rotation_euler.x=.36*p
            for leg in leg_points:
                rig.pose.bones[leg+"_Upper"].rotation_euler.x=(-.36 if leg.startswith("Front") else .27)*p
                rig.pose.bones[leg+"_Lower"].rotation_euler.x=.15*p
        elif name=="Hit":
            body.rotation_euler.z=.14*math.sin(t*math.pi)
            head.rotation_euler.z=-.15*math.sin(t*math.pi)
        elif name=="Death":
            settle=min(1,t*1.8)
            body.rotation_euler.z=.65*settle
            body.rotation_euler.x=.16*settle
            for leg in leg_points:
                rig.pose.bones[leg+"_Upper"].rotation_euler.x=.58*settle
                rig.pose.bones[leg+"_Lower"].rotation_euler.x=-.75*settle
        for p in rig.pose.bones:
            p.keyframe_insert(data_path="rotation_euler",frame=frame,group=p.name)
    action["loop"]=loop
    action["root_motion"]=False
    action["clip_frames"]=[1,end]
    actions[name]=action

rig.animation_data_clear()
rig.animation_data_create()
for name,action in actions.items():
    track=rig.animation_data.nla_tracks.new()
    track.name=name
    strip=track.strips.new(name,1,action)
    strip.action_frame_start=1
    strip.action_frame_end=clips[name][0]
    track.mute=True
rig.animation_data.action=actions["Idle"]
scene.frame_start=1;scene.frame_end=61;scene.frame_set(1)
rig["forward"]="Blender -Y; FBX Y up / -Z forward"
rig["authoring"]="AI-assisted Blender construction from the Vault Hound concept; see Source/build_vault_hound.py"

def aim(obj,target):
    obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
ground_mat=bpy.data.materials.new("Studio charcoal")
ground_mat.use_nodes=True
p=next(n for n in ground_mat.node_tree.nodes if n.type=="BSDF_PRINCIPLED")
p.inputs["Base Color"].default_value=(.035,.052,.065,1)
p.inputs["Roughness"].default_value=.8
bpy.ops.mesh.primitive_plane_add(size=200)
ground=bpy.context.object
for c in list(ground.users_collection):c.objects.unlink(ground)
studio.objects.link(ground)
ground.name="Preview floor - exclude from exports"
ground.location.z=-.013
ground.data.materials.append(ground_mat)
world=bpy.data.worlds.new("Vault Hound studio world")
world.use_nodes=True
bg=next(n for n in world.node_tree.nodes if n.type=="BACKGROUND")
bg.inputs["Color"].default_value=(.10,.15,.20,1)
bg.inputs["Strength"].default_value=.35
scene.world=world
for name,pos,power,color,size in [("Key",(2,-3,4),450,(1,.87,.72),3),
    ("Fill",(-3,-1,2),300,(.62,.82,1),3),("Rim",(1,3,3),550,(.80,.9,1),2)]:
    light=bpy.data.lights.new(name,"AREA")
    light.energy=power;light.color=color;light.shape="DISK";light.size=size
    obj=bpy.data.objects.new(name,light);studio.objects.link(obj);obj.location=pos
    aim(obj,(0,-.1,.55))
camera_data=bpy.data.cameras.new("Hound presentation camera")
camera=bpy.data.objects.new("Hound presentation camera",camera_data)
studio.objects.link(camera)
camera.location=(2.1,-3,1.6)
aim(camera,(0,-.05,.57))
camera_data.type="ORTHO";camera_data.ortho_scale=2.2
scene.camera=camera
try:scene.render.engine="BLENDER_EEVEE"
except TypeError:scene.render.engine="BLENDER_EEVEE_NEXT"
scene.render.resolution_x=1200;scene.render.resolution_y=1000
scene.render.resolution_percentage=100
scene.render.image_settings.file_format="PNG"
scene.render.film_transparent=False
scene.view_settings.view_transform="AgX"
scene.render.filepath=str(BASE/"Previews/VaultHound_ThreeQuarter.png")

# Live execution preserves the user's other scenes; background builds save normal files.
def write_source(path):
    if bpy.app.background:
        bpy.ops.wm.save_as_mainfile(filepath=str(path),copy=True,compress=True)
    else:
        bpy.data.libraries.write(str(path),{scene,*actions.values()},compress=True)

for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=="VIEW_3D":
            area.spaces.active.region_3d.view_location=Vector((0,-.05,.57))
            area.spaces.active.region_3d.view_distance=2.8
            area.spaces.active.region_3d.view_rotation=camera.rotation_euler.to_quaternion()
            area.spaces.active.shading.type="MATERIAL"
bpy.ops.object.select_all(action="DESELECT")
for obj in parts:obj.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active=rig
for image in (base_image,emission_image):image.pack()
write_source(BASE/"Source/VaultHound_Editable.blend")

mesh_count=len(parts)
for obj in parts:
    obj.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
rig.select_set(False)
bpy.ops.object.join()
game=bpy.context.object
game.name="VaultHound_SkinnedMesh"
game.data.name="VaultHound_Geometry"
# Joined pieces share one atlas and one skinning modifier.
assert len(game.modifiers)==1 and game.modifiers[0].type=="ARMATURE"
game.data.calc_loop_triangles()
triangles=len(game.data.loop_triangles)
assert triangles<16000,triangles
assert all(len(v.groups)==1 and abs(v.groups[0].weight-1)<.001 for v in game.data.vertices)
assert all(v.groups[0].group < len(game.vertex_groups) for v in game.data.vertices)
assert len(rig.data.bones)==19
scene.frame_set(1)
write_source(BASE/"Source/VaultHound_Rigged.blend")

bpy.ops.object.select_all(action="DESELECT")
game.select_set(True);rig.select_set(True)
bpy.context.view_layer.objects.active=rig
rig.animation_data.action=None
for track in rig.animation_data.nla_tracks:track.mute=False
bpy.ops.export_scene.fbx(filepath=str(BASE/"Exports/VaultHound_Animated.fbx"),
    use_selection=True,object_types={"MESH","ARMATURE"},axis_forward="-Z",axis_up="Y",
    apply_unit_scale=True,global_scale=1,add_leaf_bones=False,use_armature_deform_only=True,
    bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=True,
    bake_anim_use_all_actions=False,bake_anim_step=1,bake_anim_simplify_factor=0,
    path_mode="COPY",embed_textures=True)
for track in rig.animation_data.nla_tracks:track.mute=True
rig.animation_data.action=actions["Idle"]
scene.frame_set(1)
bpy.ops.export_scene.gltf(filepath=str(BASE/"Exports/VaultHound.glb"),use_selection=True,use_active_scene=True,
    export_format="GLB",export_animations=False,export_yup=True)
deps=bpy.context.evaluated_depsgraph_get()
evaluated=game.evaluated_get(deps)
bounds=[evaluated.matrix_world@Vector(v) for v in evaluated.bound_box]
dimensions=[max(p[i] for p in bounds)-min(p[i] for p in bounds) for i in range(3)]
report={"asset":"VaultHound","blender":bpy.app.version_string,"editable_parts":mesh_count,
    "export_meshes":1,"materials":len(game.data.materials),"triangles":triangles,
    "vertices":len(game.data.vertices),"bones":len(rig.data.bones),"dimensions_xyz_metres":dimensions,
    "forward":"Blender -Y; FBX Y up/-Z forward","fps":30,
    "clips":{k:{"frames":[1,v[0]],"seconds":(v[0]-1)/30,"loop":v[1]} for k,v in clips.items()},
    "root_motion":False,"weighted_vertices":"one bone at full weight per vertex",
    "uv":"Palette atlas UVs; intentionally shared swatches, not a unique lightmap unwrap",
    "unity_runtime_tested":False,"ai_implemented":False,"source_work_preserved":True}
(BASE/"Evidence/ModelReport.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
print(json.dumps(report,indent=2))
