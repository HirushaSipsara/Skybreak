"""Build the approved shrine/lantern and the existing Aurora portal as AI-assisted Blender assets."""
import bpy, math, json
from pathlib import Path
from mathutils import Vector

if not bpy.app.background:
    raise RuntimeError('Run in a separate Blender background process to preserve open work.')
ROOT = Path('F:/UnityProjects/Skybreak/ArtSource/Student1/AuroraArchitecture')
for d in ('Source','Exports','Previews','Documentation','References'):
    (ROOT/d).mkdir(parents=True,exist_ok=True)

def fresh(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    s=bpy.context.scene;s.name=name;s.unit_settings.system='METRIC';s.unit_settings.scale_length=1
    c=bpy.data.collections.new('01_ASSET / Export');s.collection.children.link(c)
    r=bpy.data.objects.new(name+'_ROOT',None);c.objects.link(r)
    r['provenance']='AI-assisted Blender construction; not evidence of manual student modelling.'
    return s,c,r

def mat(name,rgb,metal=0,rough=.6,emit=0):
    m=bpy.data.materials.new(name);m.use_nodes=True
    lin=lambda c:c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4
    col=tuple(lin(c) for c in rgb)+(1,);m.diffuse_color=col
    n=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    n.inputs['Base Color'].default_value=col;n.inputs['Roughness'].default_value=rough;n.inputs['Metallic'].default_value=metal
    if emit:n.inputs['Emission Color'].default_value=col;n.inputs['Emission Strength'].default_value=emit
    return m

def palette():
    return {'stone':mat('VaultStone',(.25,.35,.40)), 'cap':mat('PaleStone',(.47,.56,.59)),
        'brass':mat('Brass',(.75,.51,.25),.65,.38), 'metal':mat('DarkMetal',(.11,.15,.17),.7,.42),
        'cyan':mat('CyanEnergy',(.23,.86,.95),.1,.3,2), 'violet':mat('VioletEnergy',(.55,.18,.92),.1,.35,2.5),
        'warm':mat('AmberCore',(1,.59,.15),0,.35,3)}

def linked(o,c,r,m=None):
    for col in list(o.users_collection):col.objects.unlink(o)
    c.objects.link(o);o.parent=r
    if m:o.data.materials.append(m)
    return o

def cube(c,r,name,pos,size,m,bevel=.015):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.name=name;o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);linked(o,c,r,m)
    if bevel:b=o.modifiers.new('Editable bevel','BEVEL');b.width=bevel;b.segments=1
    return o

def cylinder(c,r,name,pos,radius,depth,m,vertices=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=pos)
    o=bpy.context.object;o.name=name;return linked(o,c,r,m)

def segment(c,r,name,a,b,radius,m):
    a,b=Vector(a),Vector(b);o=cylinder(c,r,name,(a+b)*.5,radius,(b-a).length,m,8)
    o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return o

def crystal(c,r,name,pos,width,height,m):
    v=[(0,0,-height/2),(width/2,0,0),(0,width/2,0),(-width/2,0,0),(0,-width/2,0),(0,0,height/2)]
    f=[(0,2,1),(0,3,2),(0,4,3),(0,1,4),(5,1,2),(5,2,3),(5,3,4),(5,4,1)]
    mesh=bpy.data.meshes.new(name+'_mesh');mesh.from_pydata(v,[],f);mesh.update();mesh.uv_layers.new(name='UVMap')
    o=bpy.data.objects.new(name,mesh);c.objects.link(o);o.parent=r;o.location=pos;o.data.materials.append(m);return o

def torus(c,r,name,rad,tube,z,y,m):
    verts=[];faces=[];rings=64;sides=6
    for i in range(rings):
        a=i*2*math.pi/rings
        for j in range(sides):
            b=j*2*math.pi/sides
            verts.append(((rad+tube*math.cos(b))*math.cos(a),tube*math.sin(b)+y,z+(rad+tube*math.cos(b))*math.sin(a)*1.13))
    for i in range(rings):
        for j in range(sides):faces.append((i*sides+j,((i+1)%rings)*sides+j,((i+1)%rings)*sides+(j+1)%sides,i*sides+(j+1)%sides))
    mesh=bpy.data.meshes.new(name+'_mesh');mesh.from_pydata(verts,[],faces);mesh.update();mesh.uv_layers.new(name='UVMap')
    o=bpy.data.objects.new(name,mesh);c.objects.link(o);o.parent=r;o.data.materials.append(m);return o

def energy_disc(c,r,p):
    verts=[(0,.05,4.8)]+[(3.53*math.cos(i*math.pi/32),.05,4.8+3.53*1.13*math.sin(i*math.pi/32)) for i in range(64)]
    faces=[(0,i+1,(i+1)%64+1) for i in range(64)]
    mesh=bpy.data.meshes.new('PortalEnergySurface_mesh');mesh.from_pydata(verts,[],faces);mesh.update()
    uv=mesh.uv_layers.new(name='UVMap')
    for poly in mesh.polygons:
        for loop in poly.loop_indices:
            vertex=mesh.vertices[mesh.loops[loop].vertex_index].co
            uv.data[loop].uv=(vertex.x/7.06+.5,(vertex.z-4.8)/(7.06*1.13)+.5)
    o=bpy.data.objects.new('PortalEnergySurface / shader replaced in Unity',mesh);c.objects.link(o);o.parent=r
    m=bpy.data.materials.new('PortalSwirl_BlenderPreview');m.use_nodes=True
    nodes=m.node_tree.nodes;links=m.node_tree.links
    bsdf=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
    coord=nodes.new('ShaderNodeTexCoord');noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=4;noise.inputs['Detail'].default_value=2
    links.new(coord.outputs['UV'],noise.inputs['Vector'])
    ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.2;ramp.color_ramp.elements[0].color=(.008,.002,.026,1)
    ramp.color_ramp.elements[1].position=.8;ramp.color_ramp.elements[1].color=(.04,.65,.8,1)
    middle=ramp.color_ramp.elements.new(.5);middle.color=(.28,.025,.60,1)
    links.new(noise.outputs['Fac'],ramp.inputs['Fac']);links.new(ramp.outputs['Color'],bsdf.inputs['Base Color']);links.new(ramp.outputs['Color'],bsdf.inputs['Emission Color']);bsdf.inputs['Emission Strength'].default_value=2
    o.data.materials.append(m)
    return o

def finish(name,scene,col,root,view_target,ortho,cam_pos):
    meshes=[o for o in col.objects if o.type=='MESH']
    for o in meshes:
        if not o.data.uv_layers:o.data.uv_layers.new(name='UVMap')
        o['authorship']='AI-assisted';
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes:o.select_set(True)
    root.select_set(True);bpy.context.view_layer.objects.active=root
    bpy.ops.export_scene.fbx(filepath=str(ROOT/'Exports'/f'{name}.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,use_mesh_modifiers=True,bake_anim=False,add_leaf_bones=False)
    # Studio helpers remain separated and excluded from the FBX.
    stage=bpy.data.collections.new('02_PREVIEW / not exported');scene.collection.children.link(stage)
    world=bpy.data.worlds.new('StudioWorld');world.use_nodes=True
    b=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND');b.inputs['Color'].default_value=(.025,.04,.06,1);b.inputs['Strength'].default_value=.4;scene.world=world
    def aim(o,p):o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
    for n,xyz,power,color,size in [('Key',(ortho,-ortho,ortho*1.4),120*ortho,(1,.80,.61),ortho),('Fill',(-ortho,-ortho*.2,ortho*.8),90*ortho,(.50,.78,1),ortho),('Rim',(1,ortho,ortho),160*ortho,(.65,.60,1),ortho)]:
        l=bpy.data.lights.new(n,'AREA');l.energy=power;l.color=color;l.shape='DISK';l.size=size;o=bpy.data.objects.new(n,l);stage.objects.link(o);o.location=xyz;aim(o,view_target)
    cd=bpy.data.cameras.new('PreviewCamera');cam=bpy.data.objects.new('PreviewCamera',cd);stage.objects.link(cam);cam.location=cam_pos;aim(cam,view_target);cd.type='ORTHO';cd.ortho_scale=ortho;scene.camera=cam
    scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=1200;scene.render.resolution_y=1100;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='Standard'
    if name!='VaultWallLantern':
        bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.02));floor=bpy.context.object
        for current in list(floor.users_collection):current.objects.unlink(floor)
        stage.objects.link(floor);floor.data.materials.append(mat('PreviewGround',(.09,.13,.16),0,.75))
    scene.render.filepath=str(ROOT/'Previews'/f'{name}.png');bpy.ops.render.render(write_still=True)
    stage.hide_viewport=True
    for scr in bpy.data.screens:
        for a in scr.areas:
            if a.type=='VIEW_3D':a.spaces.active.region_3d.view_location=Vector(view_target);a.spaces.active.region_3d.view_distance=ortho*1.4;a.spaces.active.shading.type='MATERIAL'
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Source'/f'{name}.blend'))
    dg=bpy.context.evaluated_depsgraph_get();triangles=0;corners=[]
    for o in meshes:
        ev=o.evaluated_get(dg);me=ev.to_mesh();me.calc_loop_triangles();triangles+=len(me.loop_triangles);ev.to_mesh_clear()
        corners.extend(o.matrix_world@Vector(v) for v in o.bound_box)
    dims=[max(v[i] for v in corners)-min(v[i] for v in corners) for i in range(3)]
    report={'asset':name,'meshes':len(meshes),'evaluated_triangles':triangles,'dimensions_xyz_metres':dims,'authorship':'AI-assisted','blender':bpy.app.version_string}
    (ROOT/'Documentation'/f'{name}_Report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print('ARCHITECTURE_CREATED '+json.dumps(report))

# Approved enclosed wall lantern. Root is the wall mounting origin, not floor origin.
s,c,r=fresh('VaultWallLantern');p=palette()
cube(c,r,'WallMount_Backplate',(0,0,0),(.28,.05,.46),p['metal'])
cube(c,r,'Bracket_Arm',(0,-.255,.16),(.07,.51,.075),p['metal'])
cube(c,r,'Bracket_Drop',(0,-.51,.01),(.05,.05,.27),p['brass'],.004)
cube(c,r,'Lantern_Top',(0,-.51,-.16),(.44,.44,.09),p['metal'])
cube(c,r,'Lantern_Bottom',(0,-.51,-.71),(.44,.44,.09),p['metal'])
cube(c,r,'Lantern_Core',(0,-.51,-.435),(.29,.29,.45),p['warm'],.004)
for x in (-.18,.18):
    for y in (-.69,-.33):cube(c,r,'Frame_Post',(x,y,-.435),(.045,.045,.48),p['metal'],.004)
for z in (-.24,-.63):cube(c,r,'Front_BrassBand',(0,-.738,z),(.44,.026,.04),p['brass'],.003)
finish('VaultWallLantern',s,c,r,(0,-.35,-.25),1.55,(2,-3,1.4))

# Approved Aurora reactor shrine, grounded with a central suspended crystal.
s,c,r=fresh('AuroraReactorShrine');p=palette()
cube(c,r,'Foundation_Lower',(0,0,.12),(5.35,2.25,.24),p['stone'])
cube(c,r,'Foundation_Upper',(0,0,.33),(5.05,1.95,.20),p['cap'])
for x in (-2,2):
    cube(c,r,'Pillar_Foot',(x,0,.69),(.95,.95,.52),p['cap'])
    cube(c,r,'Pillar_Shaft',(x,0,2.29),(.68,.68,2.70),p['stone'])
    cube(c,r,'Pillar_Capital',(x,0,3.77),(.98,.98,.27),p['cap'])
    cube(c,r,'Pillar_BrassTop',(x,-.37,3.42),(.72,.12,.12),p['brass'])
    cube(c,r,'Pillar_BrassBottom',(x,-.37,1.12),(.72,.12,.12),p['brass'])
    cube(c,r,'Pillar_CyanInset',(x,-.351,2.28),(.065,.022,2.1),p['cyan'],.002)
    for dx in (-.13,.13):cube(c,r,'Inset_MetalEdge',(x+dx,-.36,2.28),(.045,.05,2.1),p['brass'],.002)
for i in range(9):
    a=i*math.pi/9+.008;b=(i+1)*math.pi/9-.008;v=[]
    for y in (-.43,.43):
        for angle,rx,rz in [(a,2.39,1.06),(b,2.39,1.06),(b,1.92,.62),(a,1.92,.62)]:v.append((rx*math.cos(angle),y,3.65+rz*math.sin(angle)))
    me=bpy.data.meshes.new('ArchStone_mesh');me.from_pydata(v,[],[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]);me.update()
    ob=bpy.data.objects.new(f'ArchStone_{i+1:02}',me);c.objects.link(ob);ob.parent=r;ob.data.materials.append(p['cap'])
cube(c,r,'Console_Body',(0,-.07,.93),(1.55,1.0,1.0),p['stone'])
cube(c,r,'Console_FrontFrame',(0,-.63,1.0),(.45,.12,.65),p['brass'])
cube(c,r,'Console_CyanStatus',(0,-.70,1.0),(.22,.015,.41),p['cyan'],.003)
cylinder(c,r,'Lower_CrystalSocket',(0,0,1.56),.3,.20,p['brass'])
cylinder(c,r,'Upper_CrystalSocket',(0,0,4.01),.27,.20,p['brass'])
crystal(c,r,'Suspended_FacetedCrystal',(0,0,2.82),1.05,2.10,p['cyan'])
for side in (-1,1):
    for z in (1.0,1.35):
        segment(c,r,'Brass_Conduit',(side*.72,0,z),(side*1.65,0,z+.14),.055,p['brass'])
        segment(c,r,'Cyan_Conduit',(side*.72,-.065,z+.015),(side*1.65,-.065,z+.155),.015,p['cyan'])
finish('AuroraReactorShrine',s,c,r,(0,0,2.25),6.3,(7,-11,6.8))

# Recreate the earlier Descent demo gate from its recorded radii and stone-arch layout.
s,c,r=fresh('AuroraTeleportGate');p=palette()
torus(c,r,'Violet_ContainmentRing',3.6,.15,4.8,0,p['violet'])
inner=torus(c,r,'Cyan_RuneRing',3.1,.035,4.8,-.12,p['cyan'])
# Keep the inner ring's origin at its centre so future Unity rotation can work.
for vertex in inner.data.vertices:vertex.co.z-=4.8
inner.location.z=4.8
energy_disc(c,r,p)
for i in range(13):
    a=i*math.pi/12;x=math.cos(a)*4.7;z=1.2+math.sin(a)*7.2
    cube(c,r,f'StoneArch_{i+1:02}',(x,.3,z),(1.2,1.7,1.3),p['cap'],.045)
    cube(c,r,f'Copper_Inlay_{i+1:02}',(x,-.635,z),(.25,.09,.6),p['brass'],.01)
for x in (-4.7,4.7):cube(c,r,'Gate_Foundation',(x,.3,.275),(1.5,2.05,.55),p['stone'])
finish('AuroraTeleportGate',s,c,r,(0,0,4.5),12.5,(12,-18,10))
print('AURORA_ARCHITECTURE_COMPLETE')
