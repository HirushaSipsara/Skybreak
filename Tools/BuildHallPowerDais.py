"""Build a low, walkable hall landmark in a separate Blender process."""
from pathlib import Path
import bpy,math,json
from mathutils import Vector

if not bpy.app.background:raise RuntimeError('Use a separate background Blender process.')
helper=Path(__file__).with_name('BuildDungeonWallDetails.py').read_text(encoding='utf-8').split("s,c,r,p=fresh('CarvedStoneTrim_1m')")[0]
helper=helper.replace("ROOT=Path('F:/UnityProjects/Skybreak/ArtSource/Student1/DungeonWallDetails')","ROOT=Path('F:/UnityProjects/Skybreak/ArtSource/Student1/HallPowerDais')")
exec(compile(helper,'DungeonWallAssetHelpers','exec'))
s,c,r,p=fresh('AuroraHallPowerDais')
def arc(name,inner,outer,a,b,z,height,mat):
    vertices=[]
    for level in (z,z+height):
        for angle,radius in ((a,outer),(b,outer),(b,inner),(a,inner)):vertices.append((radius*math.cos(angle),radius*math.sin(angle),level))
    return mesh(c,r,name,vertices,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],mat)
# A broad flat central slab, with a shallow bevelled outer ring. Everything remains below 0.14m.
bpy.ops.mesh.primitive_cylinder_add(vertices=48,radius=2.50,depth=.09,location=(0,0,.045));link(bpy.context.object,'Central stone plate',c,r,p['stone'])
for i in range(16):
    a=i*math.tau/16+.002;b=(i+1)*math.tau/16-.002
    arc('Segmented outer stone rim',2.48,2.75,a,b,0,.10,p['edge'])
    arc('Brass rim inlay',2.34,2.38,a,b,.092,.018,p['brass'])
    arc('Cyan circuit segment',1.85,1.88,a+.07,b-.07,.092,.016,p['cyan'])
    angle=(a+b)/2
    if i%2==0:
        o=box(c,r,'Radial engraved brass bar',(2.13*math.cos(angle),2.13*math.sin(angle),.10),(.28,.037,.025),p['brass'],.003);o.rotation_euler.z=angle
# Low central diamond motif echoes the wall relief; this is an architectural floor seal.
base=box(c,r,'Central brass diamond socket',(0,0,.105),(1.15,1.15,.025),p['brass'],.006);base.rotation_euler.z=math.pi/4
diamond=box(c,r,'Inset cyan diamond',(0,0,.123),(.93,.93,.026),p['cyan'],.009);diamond.rotation_euler.z=math.pi/4
for angle in (0,math.pi/2,math.pi,math.pi*1.5):
    o=box(c,r,'Short cyan floor trace',(1.34*math.cos(angle),1.34*math.sin(angle),.098),(.54,.036,.017),p['cyan'],.003);o.rotation_euler.z=angle
finish('AuroraHallPowerDais',s,c,r,(0,0,0),6.7)
print('HALL_POWER_DAIS_COMPLETE')
