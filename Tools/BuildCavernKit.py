"""AI-assisted cavern meshes and procedural stone maps; isolated Blender process."""
import bpy, bmesh, math, random, json
from pathlib import Path
from mathutils import Vector, noise

if not bpy.app.background:
    raise RuntimeError('Use background Blender so the user scene stays intact.')
ROOT=Path('F:/UnityProjects/Skybreak/ArtSource/Student1/CavernKit')
for folder in ('Source','Exports','Textures','Documentation'):
    (ROOT/folder).mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.unit_settings.system='METRIC'

# Seamless procedural mineral texture, no external image or brick seams.
N=512
values=[]
for y in range(N):
    v=2*math.pi*y/N
    for x in range(N):
        u=2*math.pi*x/N
        p=Vector(((2+.65*math.cos(v))*math.cos(u),(2+.65*math.cos(v))*math.sin(u),.65*math.sin(v)))
        val=.55*noise.noise(p*2.3)+.28*noise.noise(p*9)+.12*noise.noise(p*38)+.05*noise.noise(p*113)
        values.append(val)
pixels=[]; normals=[]
for y in range(N):
    for x in range(N):
        t=values[y*N+x]; grain=1+t*.7
        pixels.extend((.28*grain,.34*grain,.36*grain,1))
        dx=values[y*N+(x+1)%N]-values[y*N+(x-1)%N]
        dy=values[((y+1)%N)*N+x]-values[((y-1)%N)*N+x]
        n=Vector((-dx*1.7,-dy*1.7,1)).normalized()
        normals.extend(((n.x+1)/2,(n.y+1)/2,(n.z+1)/2,1))
images=[]
for name,data,space in [('CavernStone_BaseColor',pixels,'sRGB'),('CavernStone_Normal',normals,'Non-Color')]:
    img=bpy.data.images.new(name,width=N,height=N);img.colorspace_settings.name=space;img.pixels.foreach_set(data)
    img.filepath_raw=str(ROOT/'Textures'/f'{name}.png');img.file_format='PNG';img.save();images.append(img)
mat=bpy.data.materials.new('CavernStone');mat.use_nodes=True
bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.93
tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=images[0];mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
nt=mat.node_tree.nodes.new('ShaderNodeTexImage');nt.image=images[1];normal=mat.node_tree.nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.55
mat.node_tree.links.new(nt.outputs['Color'],normal.inputs['Color']);mat.node_tree.links.new(normal.outputs['Normal'],bs.inputs['Normal'])
collection=bpy.data.collections.new('01_CAVERN / editable AI-assisted source');bpy.context.scene.collection.children.link(collection)
reports=[]

def build_mesh(name,verts,faces):
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update()
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    if not all(e.is_manifold for e in bm.edges):raise RuntimeError(name+' is not closed')
    if bm.calc_volume(signed=True)<=0:raise RuntimeError(name+' has inward normals')
    bm.to_mesh(me);bm.free()
    o=bpy.data.objects.new(name,me);collection.objects.link(o);me.materials.append(mat)
    uv=me.uv_layers.new(name='Stone planar facets')
    for poly in me.polygons:
        axis=max(range(3),key=lambda i:abs(poly.normal[i])); a,b=[i for i in range(3) if i!=axis]
        for li in poly.loop_indices:
            v=me.vertices[me.loops[li].vertex_index].co;uv.data[li].uv=(v[a]*.22,v[b]*.22)
    o['authorship']='AI-assisted procedural Blender mesh, Student 1 world-building asset'
    return o

def export(name,objects):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(ROOT/'Exports'/f'{name}.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,bake_anim=False,add_leaf_bones=False)
    triangles=0
    for o in objects:o.data.calc_loop_triangles();triangles+=len(o.data.loop_triangles)
    reports.append({'asset':name,'triangles':triangles,'pieces':len(objects)})

for variant in range(6):
    rng=random.Random(3917+variant);low=variant>=4
    height=6 if low else 18
    # Irregular rings create long vertical facets and broken ledges, with no spikes.
    levels=[-.6,.14,.38,.64,.86,1] if not low else [-.6,.2,.58,1]
    vertices=[];sides=10
    for j,l in enumerate(levels):
        radius=[1.07,1,.91,.98,.78,.57][j] if not low else [1.15,1,.82,.47][j]
        sx=rng.uniform(-.35,.35);sy=rng.uniform(-.25,.25)
        for k in range(sides):
            angle=2*math.pi*k/sides
            radial=radius*rng.uniform(.90,1.12)
            z=height*l+(rng.uniform(-.55,.55) if j>0 else 0)
            vertices.append((math.cos(angle)*4*radial+sx,math.sin(angle)*3*radial+sy,z))
    faces=[tuple(range(sides-1,-1,-1))]
    for j in range(len(levels)-1):
        for k in range(sides):
            a=j*sides+k;b=j*sides+(k+1)%sides;c=(j+1)*sides+(k+1)%sides;d=(j+1)*sides+k
            # A few triangulated fractures break regular ring bands.
            if (j+k+variant)%3==0:faces.extend([(a,b,c),(a,c,d)])
            else:faces.append((a,b,c,d))
    faces.append(tuple((len(levels)-1)*sides+k for k in range(sides)))
    name=('CavernCliff_' if not low else 'CavernCrag_')+str(variant+1)
    o=build_mesh(name,vertices,faces);export(name,[o]);o.location.x=variant*13

# Wide walk-through entry arch: opening 10.4m, spring height 7.3m.
arch=[]
for side in (-1,1):
    for row in range(5):
        bpy.ops.mesh.primitive_cube_add(size=1,location=(side*6.2,0,row*1.46+.73))
        o=bpy.context.object;o.name='Entry pier / '+str(side)+' / '+str(row);o.dimensions=(1.9,2.4,1.42)
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
        for c in list(o.users_collection):c.objects.unlink(o)
        collection.objects.link(o);o.data.materials.append(mat)
        bevel=o.modifiers.new('Chipped stone edges','BEVEL');bevel.width=.09;bevel.segments=1
        arch.append(o)
for i in range(13):
    a=math.pi*i/13+.008;b=math.pi*(i+1)/13-.008
    verts=[(r*math.cos(t),y,7.3+r*.54*math.sin(t)) for y in (-1.2,1.2) for r,t in [(5.2,a),(7.15,a),(7.15,b),(5.2,b)]]
    o=build_mesh('Arch voussoir '+str(i),verts,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)])
    bevel=o.modifiers.new('Weathered arch edges','BEVEL');bevel.width=.06;bevel.segments=1;arch.append(o)
export('CavernEntranceArch',arch)
for o in arch:o.location.y+=18
bpy.context.scene['provenance']='AI-assisted kit. Generated mineral maps and meshes; not evidence of independent manual modelling.'
for img in images:img.pack()
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Source'/'SkybreakCavernKit.blend'))
(ROOT/'Documentation'/'Kit.json').write_text(json.dumps(reports,indent=2))
print('CAVERN_KIT_COMPLETE '+json.dumps(reports))
