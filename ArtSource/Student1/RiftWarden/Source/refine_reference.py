import bpy, math, os
from mathutils import Vector

# Rebuilds only the named boss collection. Presentation and other collections are preserved.
COLLECTION = 'RIFT WARDEN | FBX CHARACTER'
col = bpy.data.collections.get(COLLECTION)
if col is None: raise RuntimeError('Expected Rift Warden model is not open')
for o in list(col.objects): bpy.data.objects.remove(o, do_unlink=True)
M = {k:bpy.data.materials[n] for k,n in {
    'teal':'RW | Deep teal armor', 'blue':'RW | Blue steel panels',
    'dark':'RW | Graphite joints', 'ivory':'RW | Warm ivory trim',
    'violet':'RW | Rift violet', 'cyan':'RW | Core cyan'}.items()}
for key in ['violet','cyan']:
    node=next(n for n in M[key].node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    node.inputs['Emission Color'].default_value = (0.43,0.008,1,1) if key=='violet' else (0.005,0.65,1,1)
    node.inputs['Emission Strength'].default_value=2.5 if key=='violet' else 3.5

def link(o,name,mat=None,parent=None):
    o.name=name
    for c in list(o.users_collection): c.objects.unlink(o)
    col.objects.link(o)
    if mat: o.data.materials.append(M[mat])
    if parent:
        mw=o.matrix_world.copy(); o.parent=parent; o.matrix_world=mw
    return o

def empty(name,loc,parent=None):
    o=bpy.data.objects.new(name,None); col.objects.link(o); o.location=loc
    bpy.context.view_layer.update()
    if parent:
        mw=o.matrix_world.copy(); o.parent=parent; o.matrix_world=mw
    return o

def bevel(o,w=.018):
    bpy.context.view_layer.objects.active=o
    mod=o.modifiers.new('Machined edges','BEVEL'); mod.width=w; mod.segments=1
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return o

def cube(name,loc,dims,mat,parent,rot=None,w=.018):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc)
    o=bpy.context.object; o.dimensions=dims
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if rot: o.rotation_euler=rot
    bpy.context.view_layer.update(); link(o,name,mat,parent)
    if w: bevel(o,w)
    return o

def cyl(name,loc,r,depth,mat,parent,axis=(0,-1,0),verts=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts,radius=r,depth=depth,location=loc)
    o=bpy.context.object; o.rotation_euler=Vector(axis).to_track_quat('Z','Y').to_euler()
    bpy.context.view_layer.update(); link(o,name,mat,parent); bevel(o,.008)
    return o

def ring(name,loc,outer,inner,depth,mat,parent,axis=(0,-1,0),n=12):
    vs=[]
    for z,r in [(-depth/2,outer),(depth/2,outer),(-depth/2,inner),(depth/2,inner)]:
        vs.extend([(r*math.cos(i*2*math.pi/n),r*math.sin(i*2*math.pi/n),z) for i in range(n)])
    fs=[]
    for i in range(n):
        j=(i+1)%n
        fs.extend([(i,j,n+j,n+i),(2*n+i,3*n+i,3*n+j,2*n+j),(i,2*n+i,2*n+j,j),(n+i,n+j,3*n+j,3*n+i)])
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(vs,[],fs); mesh.update()
    o=bpy.data.objects.new(name,mesh); col.objects.link(o); o.location=loc
    o.rotation_euler=Vector(axis).to_track_quat('Z','Y').to_euler(); o.data.materials.append(M[mat])
    bpy.context.view_layer.update()
    if parent: mw=o.matrix_world.copy(); o.parent=parent; o.matrix_world=mw
    return o

def plate(name,outline,y,depth,mat,parent,w=.012):
    # Shape drawn in X/Z, extruded along Y; all parts remain editable meshes.
    n=len(outline); vs=[(x,y-depth/2,z) for x,z in outline]+[(x,y+depth/2,z) for x,z in outline]
    fs=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(vs,[],fs); mesh.update()
    o=bpy.data.objects.new(name,mesh); col.objects.link(o); o.data.materials.append(M[mat])
    if parent: o.parent=parent; o.matrix_parent_inverse=parent.matrix_world.inverted()
    if w: bevel(o,w)
    return o

def bar(name,a,b,width,mat,parent,depth=None):
    a,b=Vector(a),Vector(b)
    o=cube(name,(a+b)/2,(width,depth or width,(b-a).length),mat,parent,w=.007)
    mw=o.matrix_world.copy(); mw=Vector(b-a).to_track_quat('Z','Y').to_matrix().to_4x4(); mw.translation=(a+b)/2
    o.matrix_world=mw
    return o

root=empty('RiftWardenRoot | ground origin',(0,0,0))
root['AssetVersion']='Reference refinement v2'; root['Forward']='Blender -Y'; root['TargetHeightMetres']=3.4
torso=empty('TorsoRoot',(0,0,2.35),root)
legs=empty('LegsRoot',(0,0,1.66),root)

# Tapered torso, narrow mechanical waist, layered abdominal plates.
plate('Torso | tapered armored chassis',[(-.6,2.84),(.6,2.84),(.7,2.62),(.47,2.12),(.28,2.03),(-.28,2.03),(-.47,2.12),(-.7,2.62)],.03,.6,'dark',torso)
cube('Waist | articulated spindle',(0,0,1.96),(.52,.42,.23),'dark',torso)
for z,width in [(2.06,.5),(1.96,.44),(1.86,.49)]:
    plate('Abdomen | layered chevron',[(-width/2,z+.07),(width/2,z+.07),(width/2-.04,z-.05),(0,z-.09),(-width/2+.04,z-.05)],-.28,.08,'teal',torso)
plate('Pelvis | girdle',[(-.4,1.88),(.4,1.88),(.47,1.69),(.28,1.55),(-.28,1.55),(-.47,1.69)],.03,.48,'dark',legs)
plate('Pelvis | pointed front guard',[(-.17,1.86),(.17,1.86),(.12,1.35),(-.08,1.35)],-.31,.13,'teal',legs)

# Head: recessed faceted face, cheek blades and luminous V visor.
head=empty('HeadRoot',(0,0,3.06),torso)
cyl('Neck spindle',(0,0,2.92),.14,.2,'dark',head,axis=(0,0,1))
plate('Helmet | faceted shell',[(-.25,3.25),(-.17,3.38),(.1,3.4),(.24,3.28),(.21,3.02),(0,2.94),(-.21,3.02)],0,.36,'teal',head)
plate('Face | recessed mask',[(-.19,3.23),(0,3.16),(.19,3.23),(.14,3.03),(0,2.98),(-.14,3.03)],-.208,.065,'dark',head)
for s in [-1,1]:
    plate('Helmet | ivory cheek '+str(s),[(s*.21,3.29),(s*.26,3.24),(s*.2,3.02),(s*.13,3.0),(s*.17,3.12)],-.13,.12,'ivory',head)
    bar('Visor | V wing '+str(s),(s*.175,-.252,3.205),(0,-.275,3.12),.036,'violet',head,depth=.023)
bar('Visor | central lower slit',(0,-.274,3.12),(0,-.264,3.04),.022,'violet',head)
plate('Helmet | crown ridge',[(-.045,3.4),(.045,3.4),(.065,3.28),(0,3.21),(-.065,3.28)],-.16,.055,'blue',head)

# Chest reactor with octagonal socket, hinged chest plates and collar armor.
cyl('Core | graphite socket',(0,-.335,2.46),.245,.1,'dark',torso)
ring('Core | ivory bezel',(0,-.4,2.46),.22,.183,.055,'ivory',torso)
ring('Core | violet containment',(0,-.438,2.46),.181,.127,.034,'violet',torso)
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=.125,location=(0,-.443,2.46))
core=link(bpy.context.object,'CoreTarget | separate vulnerable mesh','cyan',torso)
core['DamageRule']='Enable only after both arms destroyed'
for s in [-1,1]:
    hinge=empty('ChestPlateHinge_'+('L' if s==1 else 'R'),(s*.55,-.3,2.7),torso)
    plate('ChestPlate_'+('L' if s==1 else 'R'),[(s*.08,2.74),(s*.59,2.87),(s*.66,2.63),(s*.43,2.5),(s*.27,2.48),(s*.21,2.64)],-.35,.15,'teal',hinge)
    plate('Chest | lower jaw '+str(s),[(s*.28,2.4),(s*.54,2.5),(s*.47,2.23),(s*.15,2.16),(s*.08,2.24),(s*.24,2.31)],-.33,.14,'blue',hinge)
    bar('Chest | pale rim '+str(s),(s*.1,-.445,2.75),(s*.56,-.43,2.86),.044,'ivory',hinge,depth=.04)
    bar('Chest | bottom rim '+str(s),(s*.13,-.424,2.21),(s*.46,-.414,2.31),.044,'ivory',hinge)
    bar('Chest | energy inset '+str(s),(s*.32,-.438,2.64),(s*.49,-.43,2.7),.049,'dark',hinge,depth=.025)
    bar('Chest | violet inset '+str(s),(s*.33,-.456,2.644),(s*.48,-.454,2.699),.025,'violet',hinge,depth=.015)
    bar('Collar | raised guard '+str(s),(s*.29,0,2.85),(s*.36,.03,3.04),.10,'ivory',torso)

# Legs: stance, mechanical joint circles and separate greaves.
for s,label in [(1,'Left'),(-1,'Right')]:
    hip=empty(label+'HipRoot',(s*.29,0,1.67),legs)
    cyl(label+' hip joint',(s*.34,0,1.59),.155,.38,'dark',hip,axis=(1,0,0))
    bar(label+' thigh mechanism',(s*.34,0,1.62),(s*.48,0,1.12),.23,'dark',hip)
    plate(label+' thigh armor',[(s*.25,1.56),(s*.48,1.6),(s*.61,1.21),(s*.47,1.1),(s*.29,1.24)],-.17,.18,'teal',hip)
    bar(label+' hip ivory rail',(s*.46,-.05,1.64),(s*.59,-.11,1.37),.075,'ivory',hip)
    knee=empty(label+'KneeRoot',(s*.49,0,1.08),hip)
    cyl(label+' knee hinge',(s*.5,0,1.08),.16,.33,'dark',knee,axis=(1,0,0))
    ring(label+' knee violet axle',(s*.68,0,1.08),.1,.065,.035,'violet',knee,axis=(1,0,0))
    plate(label+' knee cap',[(s*.38,1.22),(s*.59,1.22),(s*.65,1.08),(s*.52,.95),(s*.35,1.02)],-.205,.17,'dark',knee)
    bar(label+' shin piston',(s*.5,0,1.0),(s*.58,.03,.35),.22,'dark',knee)
    plate(label+' broad shin armor',[(s*.34,1.0),(s*.66,1.03),(s*.77,.42),(s*.65,.29),(s*.39,.33),(s*.29,.57)],-.11,.37,'teal',knee)
    plate(label+' inset greave',[(s*.39,.89),(s*.61,.93),(s*.66,.67),(s*.57,.48),(s*.38,.53)],-.32,.07,'blue',knee)
    plate(label+' ankle trim',[(s*.38,.43),(s*.63,.47),(s*.76,.29),(s*.69,.24),(s*.42,.29)],-.36,.08,'ivory',knee)
    foot=empty(label+'FootRoot',(s*.57,0,.22),knee)
    cube(label+' foot sole',(s*.57,-.14,.10),(.51,.78,.18),'dark',foot,w=.055)
    plate(label+' toe armor',[(s*.32,.12),(s*.81,.12),(s*.77,.26),(s*.63,.34),(s*.39,.31)],-.40,.28,'teal',foot)
    for dx in [-.12,.12]: cube(label+' toe split',(s*.57+dx,-.556,.13),(.012,.022,.16),'dark',foot,w=.003)

# Arms: angular pauldrons, exposed elbows, asymmetrical weapons.
for s,label in [(1,'LeftCannon'),(-1,'RightBlade')]:
    arm=empty(label+'ArmRoot | detachable target',(s*.74,0,2.76),torso)
    arm['UnityTarget']='LeftArm' if s==1 else 'RightArm'
    cyl(label+' shoulder joint',(s*.76,0,2.72),.205,.32,'dark',arm,axis=(1,0,0))
    ring(label+' shoulder bearing',(s*.91,0,2.72),.17,.12,.035,'ivory',arm,axis=(1,0,0))
    plate(label+' pauldron silhouette',[(s*.65,2.99),(s*.92,3.04),(s*1.17,2.88),(s*1.2,2.6),(s*1.03,2.5),(s*.77,2.63)],0,.58,'teal',arm)
    plate(label+' pauldron layered face',[(s*.77,2.96),(s*.94,2.98),(s*1.11,2.85),(s*1.12,2.69),(s*.99,2.62),(s*.82,2.72)],-.308,.05,'blue',arm)
    bar(label+' shoulder panel seam',(s*.83,-.342,2.86),(s*1.08,-.342,2.76),.012,'dark',arm)
    plate(label+' raised shoulder fin',[(s*.98,2.94),(s*1.04,3.22),(s*1.13,3.07),(s*1.16,2.74)],.09,.09,'teal',arm)
    bar(label+' upper arm actuator',(s*.92,0,2.57),(s*1.04,-.03,2.23),.19,'dark',arm)
    cube(label+' bicep armor',(s*.98,-.04,2.4),(.28,.3,.28),'teal',arm,rot=(0,s*-.2,0),w=.04)
    bar(label+' arm ivory seam',(s*.94,-.21,2.51),(s*1.02,-.21,2.31),.06,'ivory',arm)
    fore=empty(label+'ForearmRoot',(s*1.07,-.04,2.16),arm)
    cyl(label+' elbow bearing',(s*1.06,-.03,2.17),.145,.3,'dark',fore,axis=(1,0,0))
    ring(label+' elbow trim',(s*1.23,-.03,2.17),.112,.073,.025,'ivory',fore,axis=(1,0,0))
    if s==1:
        axis=Vector((.14,-.28,-.7)).normalized(); base=Vector((1.1,-.1,2.12))
        cyl('Cannon | armored forearm',base+axis*.15,.25,.40,'teal',fore,axis=axis)
        cyl('Cannon | barrel',base+axis*.48,.23,.52,'dark',fore,axis=axis)
        ring('Cannon | rear energy band',base+axis*.29,.258,.223,.054,'violet',fore,axis=axis)
        ring('Cannon | heavy muzzle armor',base+axis*.77,.257,.167,.13,'teal',fore,axis=axis)
        ring('Cannon | muzzle energy ring',base+axis*.842,.218,.178,.023,'violet',fore,axis=axis)
        cyl('Cannon | dark recessed bore',base+axis*.77,.166,.015,'dark',fore,axis=axis)
        cyl('Cannon | recessed emitter',base+axis*.785,.068,.016,'violet',fore,axis=axis)
        for i in range(6):
            angle=i*math.pi/3; perp=axis.cross(Vector((0,1,0))).normalized(); perp2=axis.cross(perp)
            off=(perp*math.cos(angle)+perp2*math.sin(angle))*.235
            bar('Cannon | barrel rail '+str(i),base+axis*.35+off,base+axis*.69+off,.024,'blue',fore)
    else:
        plate('Blade arm | gauntlet',[(-.95,2.09),(-1.18,2.11),(-1.3,1.76),(-1.19,1.59),(-.98,1.7)],-.02,.39,'teal',fore)
        plate('Blade arm | dorsal ridge',[(-1.22,2.17),(-1.4,2.36),(-1.44,2.03),(-1.27,1.87)],.09,.09,'dark',fore)
        plate('Blade | armored spine',[(-1.24,1.92),(-1.38,1.91),(-1.58,.76),(-1.24,1.25)],-.17,.11,'dark',fore)
        plate('Blade | violet energy edge',[(-1.31,1.89),(-1.41,1.83),(-1.66,.66),(-1.35,1.12),(-1.27,1.62)],-.242,.048,'violet',fore,w=.004)
        bar('Blade | cyan cutting accent',(-1.43,-.27,1.56),(-1.60,-.27,.87),.012,'cyan',fore)
        cube('Blade arm | fist',(-1.16,-.03,1.57),(.23,.25,.21),'dark',fore,w=.027)

# Back machinery: plates, rear energy vents, external pistons.
plate('Back | reactor casing',[(-.4,2.87),(.4,2.87),(.46,2.34),(.23,2.19),(-.23,2.19),(-.46,2.34)],.4,.18,'dark',torso)
plate('Back | central dorsal panel',[(-.23,2.82),(.23,2.82),(.2,2.42),(0,2.31),(-.2,2.42)],.52,.08,'teal',torso)
for s in [-1,1]:
    cube('Back | vent housing '+str(s),(s*.35,.52,2.59),(.13,.11,.32),'dark',torso)
    for j in range(4): cube('Back | violet vent '+str(s)+' '+str(j),(s*.35,.59,2.49+j*.065),(.075,.022,.032),'violet',torso,w=.004)

bpy.context.view_layer.update()
# Fix outward mesh normals and use hard-surface flat shading throughout.
for o in list(col.objects):
    if o.type=='MESH':
        bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active=o
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.normals_make_consistent(inside=False); bpy.ops.object.mode_set(mode='OBJECT')
for o in bpy.context.selected_objects: o.select_set(False)
root.select_set(True); bpy.context.view_layer.objects.active=root
scene=bpy.context.scene
scene.camera.location=(4.8,-8.8,4.0)
scene.camera.rotation_euler=(Vector((0,0,1.72))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
scene.camera.data.type='ORTHO'; scene.camera.data.ortho_scale=4.65
scene.render.resolution_x=1200; scene.render.resolution_y=1200; scene.render.resolution_percentage=100
base=r'F:\UnityProjects\Skybreak\ArtSource\Student1\RiftWarden'
scene.render.filepath=os.path.join(base,'References','RiftWardenPreview.png')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(base,'Source','RiftWarden.blend'))
print('Reference refinement saved:',len(col.objects),'objects')
