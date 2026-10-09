"""Editable AI-assisted dungeon decoration, built in an isolated Blender process."""
import bpy, math, json
from pathlib import Path
from mathutils import Vector

if not bpy.app.background:
    raise RuntimeError('Run in a separate background Blender process; preserve the open scene.')
ROOT=Path(__file__).resolve().parents[1]
for d in ('Source','Exports','Previews','Documentation','References'):(ROOT/d).mkdir(parents=True,exist_ok=True)

def material(name,col,metal=0,emit=0):
    m=bpy.data.materials.new(name);m.use_nodes=True
    m.diffuse_color=(*col,1);n=m.node_tree.nodes.get('Principled BSDF')
    n.inputs['Base Color'].default_value=(*col,1);n.inputs['Metallic'].default_value=metal;n.inputs['Roughness'].default_value=.75 if metal==0 else .4
    if emit:n.inputs['Emission Color'].default_value=(*col,1);n.inputs['Emission Strength'].default_value=emit
    return m
def fresh(name):
    bpy.ops.wm.read_factory_settings(use_empty=True);s=bpy.context.scene;s.name=name;s.unit_settings.system='METRIC'
    c=bpy.data.collections.new('01_ASSET / Export');s.collection.children.link(c);root=bpy.data.objects.new(name+'_ROOT',None);c.objects.link(root);root['authorship']='AI-assisted Blender modelling'
    p={'stone':material('GuardianStone',(.19,.26,.29)), 'edge':material('GuardianStoneLight',(.32,.39,.41)), 'dark':material('CarvedShadow',(.065,.095,.105)), 'brass':material('ConduitCopper',(.43,.22,.075),.6), 'cyan':material('GuardianCrystal',(.065,.62,.72),0,1.3)}
    return s,c,root,p
def link(o,name,c,root,mat):
    o.name=name
    for old in list(o.users_collection):old.objects.unlink(o)
    c.objects.link(o);o.parent=root;o.data.materials.append(mat);return o
def box(c,r,name,pos,size,m,bevel=.012):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);link(o,name,c,r,m)
    if bevel:b=o.modifiers.new('Editable chipped corners','BEVEL');b.width=bevel;b.segments=1
    return o
def mesh(c,r,name,v,f,m):
    data=bpy.data.meshes.new(name+'_mesh');data.from_pydata(v,[],f);data.update();o=bpy.data.objects.new(name,data);c.objects.link(o);o.parent=r;o.data.materials.append(m);return o
def profile(c,r,name,outline,depth,centre,m):
    # Faceted armour plate extruded through the front/back axis.
    v=[(x+centre[0],y+centre[1],z+centre[2]) for y in (-depth*.5,depth*.5) for x,z in outline];n=len(outline)
    f=[tuple(range(n-1,-1,-1)),tuple(range(n,n*2))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return mesh(c,r,name,v,f,m)
def stone_limb(c,r,name,a,b,width,depth,m):
    a,b=Vector(a),Vector(b);o=box(c,r,name,(a+b)/2,(width,depth,(b-a).length),m,.045);o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return o
def pipe(c,r,name,a,b,radius,m):
    a,b=Vector(a),Vector(b);bpy.ops.mesh.primitive_cylinder_add(vertices=10,radius=radius,depth=(b-a).length,location=(a+b)/2);o=bpy.context.object;link(o,name,c,r,m);o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return o
def crystal(c,r,name,centre,width,height,m):
    v=[(0,0,-height/2),(width/2,0,0),(0,width/2,0),(-width/2,0,0),(0,-width/2,0),(0,0,height/2)];v=[tuple(Vector(q)+Vector(centre)) for q in v]
    return mesh(c,r,name,v,[(0,2,1),(0,3,2),(0,4,3),(0,1,4),(5,1,2),(5,2,3),(5,3,4),(5,4,1)],m)
def finish(name,s,c,r,target,ortho):
    objects=[o for o in c.objects if o.type=='MESH'];bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.15192,island_margin=.025);bpy.ops.object.mode_set(mode='OBJECT')
    r.select_set(True);bpy.context.view_layer.objects.active=r
    bpy.ops.export_scene.fbx(filepath=str(ROOT/'Exports'/f'{name}.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,bake_anim=False,add_leaf_bones=False)
    dg=bpy.context.evaluated_depsgraph_get();triangles=0;corners=[]
    for o in objects:
        ev=o.evaluated_get(dg);me=ev.to_mesh();me.calc_loop_triangles();triangles+=len(me.loop_triangles);ev.to_mesh_clear();corners.extend(o.matrix_world@Vector(v) for v in o.bound_box)
    dims=[max(v[i] for v in corners)-min(v[i] for v in corners) for i in range(3)]
    report={'asset':name,'meshes':len(objects),'triangles':triangles,'dimensions_blender_xyz':dims,'authorship':'AI-assisted','uv':'Smart projected for colour/stone texture use'}
    (ROOT/'Documentation'/f'{name}.json').write_text(json.dumps(report,indent=2));print('DUNGEON_ASSET '+json.dumps(report))
    stage=bpy.data.collections.new('02_PREVIEW / not exported');s.collection.children.link(stage)
    def aim(o):o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
    for n,pos,power,color in [('Key',(ortho,-ortho,ortho*1.5),300*ortho,(1,.82,.63)),('Fill',(-ortho,-ortho,ortho),220*ortho,(.53,.75,1)),('Rim',(0,ortho,ortho),400*ortho,(.65,.8,1))]:
        data=bpy.data.lights.new(n,'AREA');data.energy=power;data.color=color;data.size=ortho;o=bpy.data.objects.new(n,data);stage.objects.link(o);o.location=pos;aim(o)
    data=bpy.data.cameras.new('PreviewCamera');camera=bpy.data.objects.new('PreviewCamera',data);stage.objects.link(camera);camera.location=(ortho*.8,-ortho*1.7,ortho*.85);aim(camera);data.type='ORTHO';data.ortho_scale=ortho;s.camera=camera
    world=bpy.data.worlds.new('Studio');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.045,.055,.07,1);world.node_tree.nodes['Background'].inputs[1].default_value=.4;s.world=world
    s.render.engine='BLENDER_EEVEE';s.render.resolution_x=1000;s.render.resolution_y=1000;s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG';s.render.filepath=str(ROOT/'Previews'/f'{name}.png');s.view_settings.view_transform='AgX';bpy.ops.render.render(write_still=True)
    stage.hide_viewport=True
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':area.spaces.active.region_3d.view_location=Vector(target);area.spaces.active.region_3d.view_distance=ortho*1.5;area.spaces.active.shading.type='MATERIAL'
    bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Source'/f'{name}.blend'))

s,c,r,p=fresh('CarvedStoneTrim_1m')
box(c,r,'Trim backing',(0,-.025,0),(1,.09,.24),p['stone'])
for z in (-.16,.16):box(c,r,'Trim moulding',(0,-.10,z),(1,.22,.07),p['edge'])
for x in (-.36,-.12,.12,.36):box(c,r,'Carved square dentil',(x,-.095,0),(.12,.11,.17),p['edge'],.007)
finish('CarvedStoneTrim_1m',s,c,r,(0,0,0),1.3)

s,c,r,p=fresh('DiamondWallPanel')
box(c,r,'Panel backing',(0,0,0),(1.12,.12,1.20),p['stone'])
box(c,r,'Recess shadow',(0,-.068,0),(.89,.025,.96),p['dark'],.005)
for x in (-.48,.48):box(c,r,'Frame side',(x,-.12,0),(.16,.17,1.14),p['edge'])
for z in (-.51,.51):box(c,r,'Frame end',(0,-.12,z),(.98,.17,.16),p['edge'])
v=[(0,-.095,-.37),(.30,-.095,0),(0,-.095,.37),(-.30,-.095,0),(0,-.22,0)]
mesh(c,r,'Raised diamond relief',v,[(0,1,4),(1,2,4),(2,3,4),(3,0,4),(3,2,1,0)],p['edge'])
finish('DiamondWallPanel',s,c,r,(0,0,0),1.65)

s,c,r,p=fresh('CopperWallConduit_1m')
pipe(c,r,'Copper main line',(-.50,-.13,0),(.50,-.13,0),.046,p['brass'])
pipe(c,r,'Secondary slim line',(-.50,-.09,.13),(.50,-.09,.13),.022,p['brass'])
for x in (-.35,.35):box(c,r,'Wall fastener',(x,-.025,.06),(.08,.10,.28),p['dark']);pipe(c,r,'Copper coupling',(x-.04,-.13,0),(x+.04,-.13,0),.068,p['brass'])
box(c,r,'Status housing',(0,-.13,.05),(.16,.13,.24),p['dark']);box(c,r,'Cyan status lens',(0,-.201,.05),(.050,.014,.145),p['cyan'],.003)
finish('CopperWallConduit_1m',s,c,r,(0,0,.05),1.3)

s,c,r,p=fresh('WeatheredGuardianStatue')
box(c,r,'Pedestal lower',(0,0,.13),(1.14,.94,.26),p['stone'],.03);box(c,r,'Pedestal bevel cap',(0,0,.295),(1.2,1,.13),p['edge'],.02)
for side in (-1,1):
    x=side*.22
    profile(c,r,'Carved boot',[(-.18,0),(.19,0),(.16,.18),(.08,.26),(-.13,.22)],.48,(x,-.08,.36),p['stone'])
    profile(c,r,'Shin armour',[(-.13,0),(.13,0),(.17,.61),(.09,.71),(-.13,.63)],.29,(x,0,.59),p['stone'])
    profile(c,r,'Knee shield',[(-.17,.04),(0,-.03),(.17,.04),(.17,.20),(0,.28),(-.17,.20)],.13,(x,-.20,1.15),p['edge'])
    stone_limb(c,r,'Upper leg',(x,0,1.16),(side*.19,.015,1.63),.29,.33,p['stone'])
profile(c,r,'Waist sculpt',[(-.31,0),(.31,0),(.35,.20),(-.35,.20)],.40,(0,0,1.5),p['dark'])
profile(c,r,'Breastplate',[(-.30,0),(.30,0),(.39,.44),(.31,.64),(-.31,.64),(-.39,.44)],.40,(0,0,1.67),p['stone'])
profile(c,r,'Central chest relief',[(-.22,0),(.22,0),(.29,.36),(0,.49),(-.29,.36)],.08,(0,-.24,1.77),p['edge'])
profile(c,r,'Front waist tabard',[(-.22,0),(0,-.10),(.22,0),(.24,.41),(-.24,.41)],.07,(0,-.23,1.27),p['edge'])
for side in (-1,1):
    x=side*.44
    shoulder=[(-.22,0),(.22,.01),(.19,.31),(.04,.40),(-.17,.31)]
    if side==1:shoulder=[(-.22,0),(.22,.01),(.19,.23),(.12,.24),(.13,.31),(.04,.40),(-.17,.31)]
    profile(c,r,'Chipped shoulder' if side==1 else 'Shoulder armour',shoulder,.42,(x,-.01,2.0),p['edge'])
    stone_limb(c,r,'Upper arm',(side*.43,0,2.13),(side*.50,-.17,1.83),.26,.28,p['stone'])
    stone_limb(c,r,'Forearm gauntlet',(side*.50,-.17,1.83),(side*.18,-.39,1.85),.24,.24,p['edge'])
    box(c,r,'Hand around crystal',(side*.16,-.43,1.88),(.20,.20,.19),p['stone'],.025)
    for z in (1.83,1.89,1.95):box(c,r,'Carved finger',(side*.14,-.54,z),(.19,.045,.035),p['edge'],.004)
pipe(c,r,'Brass collar',(0,0,2.29),(0,0,2.35),.20,p['brass'])
profile(c,r,'Faceted helmet',[(-.16,0),(.16,0),(.20,.25),(.11,.40),(0,.45),(-.15,.36),(-.20,.23)],.33,(0,0,2.35),p['stone'])
box(c,r,'Recessed visor',(0,-.181,2.59),(.24,.025,.09),p['dark'],.008)
profile(c,r,'Helmet face ridge',[(-.035,0),(.035,0),(.052,.27),(-.052,.27)],.05,(0,-.21,2.36),p['edge'])
crystal(c,r,'Held cyan crystal',(0,-.41,2.09),.25,.42,p['cyan'])
finish('WeatheredGuardianStatue',s,c,r,(0,0,1.4),3.45)
print('DUNGEON_WALL_DETAILS_COMPLETE')
