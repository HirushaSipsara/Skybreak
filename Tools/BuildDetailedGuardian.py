"""Build an editable detailed revision, preserving the original guardian source."""
from pathlib import Path
import bpy, bmesh, math
source=Path(__file__).with_name('BuildDungeonWallDetails.py').read_text(encoding='utf-8')
helper=source.split("s,c,r,p=fresh('CarvedStoneTrim_1m')")[0]
helper=helper.replace("ROOT=Path('F:/UnityProjects/Skybreak/ArtSource/Student1/DungeonWallDetails')","ROOT=Path('F:/UnityProjects/Skybreak/ArtSource/Student1/DetailedGuardian')")
exec(compile(helper,'GuardianHelpers','exec'))
base=source.split("s,c,r,p=fresh('WeatheredGuardianStatue')")[1].split("finish('WeatheredGuardianStatue'")[0]
s,c,r,p=fresh('WeatheredGuardianDetailed')
exec(compile(base,'OriginalGuardianBody','exec'))

# A stepped plinth, inset name tablet and a continuous carved border.
box(c,r,'Pedestal bottom moulding',(0,0,.045),(1.26,1.04,.09),p['edge'],.022)
box(c,r,'Pedestal upper shadow reveal',(0,0,.237),(1.17,.97,.035),p['dark'],.006)
box(c,r,'Pedestal inset tablet',(0,-.486,.145),(.56,.028,.12),p['dark'],.012)
for x in (-.31,.31):box(c,r,'Tablet stone frame',(x,-.50,.145),(.04,.045,.16),p['edge'],.008)
for z in (.077,.213):box(c,r,'Tablet border',(0,-.50,z),(.64,.045,.025),p['edge'],.004)
for x in (-.2,-.1,0,.1,.2):
    o=box(c,r,'Carved tablet glyph',(x,-.511,.145),(.016,.012,.065),p['brass'],.002);o.rotation_euler.y=.45 if x<0 else -.45
for x in (-.49,.49):
    for y in (-.38,.38):box(c,r,'Plinth corner cap',(x,y,.29),(.16,.16,.115),p['edge'],.022)

# Helmet: brow, inset eyes, cheek guards, crest and small breathing slots.
profile(c,r,'Helmet brow',[(-.195,0),(-.03,-.018),(0,-.045),(.03,-.018),(.195,0),(.16,.045),(-.16,.045)],.047,(0,-.206,2.65),p['edge'])
for side in (-1,1):
    profile(c,r,'Helmet cheek guard',[(side*.055,0),(side*.15,.035),(side*.177,.22),(side*.055,.17)],.045,(0,-.19,2.39),p['edge'])
    box(c,r,'Deep eye slit',(side*.092,-.22,2.613),(.088,.017,.03),p['dark'],.004)
    for i in range(3):box(c,r,'Faceplate breathing cut',(side*(.07+i*.032),-.222,2.475),(.012,.016,.052),p['dark'],.002)
profile(c,r,'Crown centre ridge',[(-.027,0),(.027,0),(.028,.22),(0,.255),(-.028,.22)],.37,(0,-.004,2.545),p['edge'])
pipe(c,r,'Collar upper stone lip',(0,0,2.325),(0,0,2.36),.216,p['edge'])

# Layered shoulder armour with engraved borders and restrained brass studs.
for side in (-1,1):
    x=side*.44
    for i in range(2):
        profile(c,r,'Layered shoulder lames',[(-.18,0),(.18,0),(.17,.16),(0,.23),(-.17,.16)],.065,(x,-.245-i*.025,2.005+i*.115),p['stone'] if i==0 else p['edge'])
    for dx in (-.125,.125):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=8,ring_count=4,radius=.022,location=(x+dx,-.317,2.22));link(bpy.context.object,'Shoulder brass rivet',c,r,p['brass'])
    # Raised chest ribs frame the central crystal instead of covering it.
    for i in range(3):
        a=(side*(.08+i*.055),-.293,2.22-i*.046)
        b=(side*(.29+i*.015),-.276,2.26-i*.07)
        pipe(c,r,'Carved breastplate rib',a,b,.014,p['edge'])
    # Cuffs, individual thumb and shallow knuckle plates.
    box(c,r,'Wrist cuff border',(side*.275,-.41,1.875),(.065,.255,.26),p['stone'],.016)
    box(c,r,'Wrist cuff inset',(side*.275,-.544,1.875),(.038,.017,.16),p['brass'],.004)
    thumb=box(c,r,'Sculpted thumb',(side*.095,-.475,1.995),(.085,.11,.055),p['edge'],.015);thumb.rotation_euler.y=side*.3
    for i in range(3):box(c,r,'Finger knuckle relief',(side*.17,-.569,1.83+i*.06),(.052,.028,.035),p['stone'],.007)
    # Greaves, boots and hip plates give the lower half the same level of detail.
    x=side*.22
    profile(c,r,'Inset shin shield',[(-.08,0),(.08,0),(.095,.4),(0,.48),(-.095,.4)],.045,(x,-.166,.66),p['edge'])
    pipe(c,r,'Shin central fluting',(x,-.197,.71),(x,-.197,1.08),.015,p['stone'])
    for dx in (-.07,.07):pipe(c,r,'Shin side fluting',(x+dx,-.196,.74),(x+dx,-.196,1.02),.008,p['dark'])
    profile(c,r,'Knee inset diamond',[(0,0),(.085,.075),(0,.15),(-.085,.075)],.029,(x,-.286,1.185),p['stone'])
    for z in (.78,1.04):box(c,r,'Greave strap',(x,.009,z),(.292,.308,.026),p['stone'],.006)
    # Keep the sloped boots clean; a horizontal overlay pierced the sloped toe surface.
    profile(c,r,'Hip armour leaf',[(-.13,0),(.13,0),(.105,.24),(-.105,.24)],.075,(side*.26,-.245,1.37),p['stone'])
    pipe(c,r,'Tabard side border',(side*.205,-.278,1.31),(side*.21,-.278,1.62),.012,p['stone'])

box(c,r,'Belt raised band',(0,-.235,1.65),(.64,.072,.065),p['stone'],.01)
box(c,r,'Belt square buckle',(0,-.282,1.65),(.115,.035,.105),p['brass'],.012)
profile(c,r,'Tabard diamond relief',[(0,0),(.075,.09),(0,.18),(-.075,.09)],.021,(0,-.285,1.36),p['stone'])
# Crystal cradle makes the held object read as a ceremonial relic.
pipe(c,r,'Crystal socket',(0,-.41,1.905),(0,-.41,1.975),.088,p['brass'])
for side in (-1,1):pipe(c,r,'Crystal cradle prong',(side*.08,-.41,1.94),(side*.11,-.41,2.015),.014,p['brass'])
# Back detailing is visible when the prop is reused away from a wall.
profile(c,r,'Back mantle panel',[(-.22,0),(.22,0),(.28,.47),(0,.58),(-.28,.47)],.065,(0,.226,1.70),p['edge'])
for z in (1.86,2.0,2.14):box(c,r,'Back spine segment',(0,.273,z),(.12,.065,.085),p['stone'],.013)

# Profile winding differs for mirrored outlines. Recalculate every closed part
# before export, so Unity back-face culling cannot turn armour into holes/spikes.
for obj in c.objects:
    if obj.type != 'MESH': continue
    bm=bmesh.new();bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    if bm.calc_volume(signed=True)<=0:raise RuntimeError('Invalid outward volume: '+obj.name)
    if any(not e.is_manifold for e in bm.edges):raise RuntimeError('Open mesh: '+obj.name)
    bm.to_mesh(obj.data);bm.free();obj.data.update()
finish('WeatheredGuardianDetailed',s,c,r,(0,0,1.4),3.45)
print('DETAILED_GUARDIAN_COMPLETE')
