"""Relay - small hovering support robot for Skybreak. Builds, previews and exports headlessly.

blender -b --factory-startup --python relay_build.py -- <out_dir> [render]

Blender: Z up, front faces -Y, origin = body centre. Character-left = +X (blaster side).
Hierarchy: Relay (empty) -> Relay_Body, Relay_EyeRing, Relay_EyeLens, Relay_Joint_L/R,
Relay_Pod_L/R (pivots on the joint axis), Relay_Blaster (child of Relay_Pod_L),
Relay_Thruster, Relay_HealEmitter.
"""
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix, Euler

R = math.radians


def srgb(h):
    c = [int(h[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


PALETTE = {
    "Relay_Cream":     (srgb("#EADDC7"), 0.0, 0.45, 0.0),
    "Relay_Teal":      (srgb("#2FB3B2"), 0.0, 0.40, 0.0),
    "Relay_DarkMetal": (srgb("#3E4852"), 0.55, 0.40, 0.0),
    "Relay_Orange":    (srgb("#FF8A3C"), 0.0, 0.45, 0.0),
    "Relay_CyanGlow":  (srgb("#32F6FF"), 0.0, 0.15, 1.5),
}
MATS = {}

BODY = (0.325, 0.275, 0.225)        # half extents: 0.65 W x 0.55 D x 0.45 H
SUPER_N = 2.6                       # 2 = ellipsoid, higher = boxier
JOINT_Z = 0.02
JOINT_X = 0.33                      # pod pivot, on the body side
EYE = Vector((0.0, -0.262, -0.005))


def T(x, y, z):
    return Matrix.Translation((x, y, z))


def Rot(rx=0, ry=0, rz=0):
    return Euler((R(rx), R(ry), R(rz)), "XYZ").to_matrix().to_4x4()


def S(s):
    return Matrix.Diagonal((s, 1, 1, 1))


def make_materials():
    for name, (col, metal, rough, emit) in PALETTE.items():
        m = bpy.data.materials.new(name)
        try:
            m.use_nodes = True
        except Exception:
            pass
        b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        for s in b.inputs:
            if s.identifier == "Base Color":
                s.default_value = (*col, 1)
            elif s.identifier == "Metallic":
                s.default_value = metal
            elif s.identifier == "Roughness":
                s.default_value = rough
            elif s.identifier == "Emission Color" and emit:
                s.default_value = (*col, 1)
            elif s.identifier == "Emission Strength":
                s.default_value = emit
        m.diffuse_color = (*col, 1)
        MATS[name] = m


class Part:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []

    def mi(self, key):
        if key not in self.mats:
            self.mats.append(key)
        return self.mats.index(key)

    def merge(self, bm, mat=None, mat_fn=None, sharp_deg=35.0):
        for f in bm.faces:
            f.smooth = True
            if mat_fn:
                f.material_index = self.mi(mat_fn(f))
            elif mat:
                f.material_index = self.mi(mat)
        for e in bm.edges:
            if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > R(sharp_deg):
                e.smooth = False
        me = bpy.data.meshes.new("_tmp")
        bm.to_mesh(me)
        bm.free()
        self.bm.from_mesh(me)
        bpy.data.meshes.remove(me)

    def build(self, pivot, parent=None):
        bm = self.bm
        bm.transform(Matrix.Translation(-Vector(pivot)))
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        for k in self.mats:
            me.materials.append(MATS[k])
        ob = bpy.data.objects.new(self.name, me)
        bpy.data.collections["Relay"].objects.link(ob)
        ob.location = pivot
        if parent is not None:
            bpy.context.view_layer.update()
            world = Vector(pivot)
            ob.parent = parent
            ob.location = world - parent.matrix_world.translation
        return ob


def lathe(prof, segs, M, mats, cap_mode=None):
    """Revolve (r, h) points around local Z. mats: one material per band.
    Winding: the face normal is (dh, -dr) in the (r, h) plane, i.e. the outside must be on
    the right when walking along the profile (r to the right, h up). Mirrored matrices are
    compensated."""
    bm = bmesh.new()
    rings = []
    for r, h in prof:
        if r < 1e-6:
            rings.append([bm.verts.new((0, 0, h))])
        else:
            rings.append([bm.verts.new((r * math.cos(2 * math.pi * i / segs), r * math.sin(2 * math.pi * i / segs), h))
                          for i in range(segs)])
    tags = []
    for k in range(len(rings) - 1):
        a, b = rings[k], rings[k + 1]
        for i in range(segs):
            j = (i + 1) % segs
            if len(a) == 1:
                vs = (a[0], b[j], b[i])
            elif len(b) == 1:
                vs = (a[i], a[j], b[0])
            else:
                vs = (a[i], a[j], b[j], b[i])
            f = bm.faces.new(vs)
            tags.append((f, mats[k]))
    bm.transform(M)
    if M.to_3x3().determinant() < 0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    lookup = {f: m for f, m in tags}
    return bm, (lambda f: lookup.get(f, mats[0]))


def super_ellipsoid(a, b, c, n=SUPER_N, segs=24, rings=14):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=1.0)
    for v in bm.verts:
        x, y, z = v.co
        k = (abs(x) ** n + abs(y) ** n + abs(z) ** n) ** (1 / n)
        v.co = Vector((x / k * a, y / k * b, z / k * c))
    return bm


def rbox(size, loc, bev=0.008, rot=(0, 0, 0), F=None):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    if bev > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges) + list(bm.verts), offset=min(bev, min(size) * 0.45),
                        offset_type="OFFSET", segments=2, profile=0.5, affect="EDGES", clamp_overlap=True)
    M = (F or Matrix.Identity(4)) @ T(*loc) @ Rot(*rot)
    bm.transform(M)
    if M.to_3x3().determinant() < 0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    return bm


# ---------------------------------------------------------------- parts
def build_body(root):
    p = Part("Relay_Body")
    p.merge(super_ellipsoid(*BODY), "Relay_Cream")
    # teal top armour shell: top and back of the body, lifted off the surface
    sh = super_ellipsoid(BODY[0] + 0.012, BODY[1] + 0.012, BODY[2] + 0.012, segs=32, rings=18)
    # cut along two clean planes: level across the front, sloping down over the back
    flat_co, flat_no = Vector((0, 0, 0.07)), Vector((0, 0, 1))
    tilt_co, tilt_no = Vector((0, -0.05, 0.07)), Vector((0, 0.667, 1)).normalized()
    for co, no in ((flat_co, flat_no), (tilt_co, tilt_no)):
        bmesh.ops.bisect_plane(sh, geom=sh.verts[:] + sh.edges[:] + sh.faces[:], plane_co=co, plane_no=no)

    def armour(f):
        c = f.calc_center_median()
        return (c - flat_co).dot(flat_no) > 0 or (c - tilt_co).dot(tilt_no) > 0
    bmesh.ops.delete(sh, geom=[f for f in sh.faces if not armour(f)], context="FACES")
    bmesh.ops.delete(sh, geom=[v for v in sh.verts if not v.link_faces], context="VERTS")
    bmesh.ops.solidify(sh, geom=sh.faces[:], thickness=0.022)
    bmesh.ops.recalc_face_normals(sh, faces=sh.faces[:])
    p.merge(sh, "Relay_Teal", sharp_deg=50)
    # back hatch, top vent, small orange tabs
    p.merge(rbox((0.30, 0.03, 0.15), (0, 0.293, -0.01), bev=0.012), "Relay_Teal")
    p.merge(rbox((0.08, 0.012, 0.02), (0, 0.312, -0.04), bev=0.004), "Relay_Orange")
    p.merge(rbox((0.15, 0.09, 0.022), (0, 0.03, 0.247), bev=0.008), "Relay_DarkMetal")
    for s in (1, -1):
        p.merge(rbox((0.022, 0.05, 0.035), (0.31, -0.13, -0.075), bev=0.006, rot=(0, 0, -18), F=S(s)), "Relay_Orange")
    return p.build((0, 0, 0), root)


def build_eye(root):
    M = T(*EYE) @ Rot(90, 0, 0)          # lathe +Z -> world -Y (out of the face)
    ring = Part("Relay_EyeRing")
    bm, fn = lathe([(0.138, -0.035), (0.138, 0.036), (0.127, 0.048), (0.108, 0.048), (0.098, 0.035), (0.098, -0.03)],
                   32, M, ["Relay_DarkMetal"] * 5)
    ring.merge(bm, "Relay_DarkMetal", sharp_deg=30)
    bm, fn = lathe([(0.1, -0.012), (0, -0.012)], 32, M, ["Relay_DarkMetal"])    # socket back plate
    ring.merge(bm, "Relay_DarkMetal")
    ring_ob = ring.build(tuple(EYE), root)
    lens = Part("Relay_EyeLens")
    bm, fn = lathe([(0.097, -0.01), (0.097, 0.004), (0.075, 0.018), (0.045, 0.028), (0, 0.032)], 32, M,
                   ["Relay_CyanGlow"] * 4)
    lens.merge(bm, "Relay_CyanGlow", sharp_deg=60)
    lens_ob = lens.build(tuple(EYE), root)
    return ring_ob, lens_ob


def build_joint(s, root):
    side = "L" if s > 0 else "R"
    pivot = S(s) @ Vector((JOINT_X, 0, JOINT_Z))
    F = S(s) @ T(JOINT_X, 0, JOINT_Z) @ Rot(0, 90, 0)   # lathe +Z -> world +X (outward)
    j = Part(f"Relay_Joint_{side}")
    bm, _ = lathe([(0, -0.045), (0.05, -0.045), (0.056, -0.038), (0.056, 0.038), (0.05, 0.046), (0, 0.046)],
                  20, F, ["Relay_DarkMetal"] * 5)
    j.merge(bm, "Relay_DarkMetal", sharp_deg=30)
    bm, _ = lathe([(0.057, -0.012), (0.067, -0.008), (0.067, 0.012), (0.057, 0.016)], 20, F, ["Relay_Orange"] * 3)
    j.merge(bm, "Relay_Orange", sharp_deg=30)
    return j.build(tuple(pivot), root)


def build_pod(s, root):
    side = "L" if s > 0 else "R"
    pivot = S(s) @ Vector((JOINT_X, 0, JOINT_Z))
    p = Part(f"Relay_Pod_{side}")
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=10, radius=1.0)
    bm.transform(Rot(0, 90, 0))                        # poles on the X axis -> rings cut cleanly
    bm.transform(S(s) @ T(0.405, 0, JOINT_Z) @ Matrix.Diagonal((0.07, 0.10, 0.105, 1)))
    if s < 0:                                          # the mirror flips winding; keep normals outward
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    # teal cap on the upper-outer side, cream underneath: not symmetric about the joint axis,
    # so spinning the pod on its joint reads clearly in game
    pc = S(s) @ Vector((0.405, 0, JOINT_Z))
    split = Vector((0.55 * s, 0, 0.83)).normalized()
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=pc, plane_no=split)
    p.merge(bm, mat_fn=lambda f: "Relay_Teal" if (f.calc_center_median() - pc).dot(split) > 0 else "Relay_Cream",
            sharp_deg=80)
    return p.build(tuple(pivot), root)


def build_blaster(pod_l):
    p = Part("Relay_Blaster")
    cx, cy, cz = 0.40, -0.05, -0.13
    p.merge(rbox((0.075, 0.17, 0.07), (cx, cy, cz), bev=0.014), "Relay_Cream")
    p.merge(rbox((0.06, 0.03, 0.052), (cx, cy - 0.095, cz), bev=0.008), "Relay_DarkMetal")
    bm, _ = lathe([(0.012, 0.0), (0.012, 0.012), (0, 0.012)], 12, T(cx, cy - 0.11, cz) @ Rot(90, 0, 0), ["Relay_CyanGlow"] * 2)
    p.merge(bm, "Relay_CyanGlow")
    p.merge(rbox((0.006, 0.09, 0.02), (cx + 0.0395, cy + 0.005, cz + 0.004), bev=0.002), "Relay_CyanGlow")
    p.merge(rbox((0.03, 0.04, 0.03), (cx - 0.005, cy + 0.03, cz + 0.045), bev=0.006), "Relay_DarkMetal")
    p.merge(rbox((0.04, 0.02, 0.012), (cx, cy + 0.06, cz + 0.036), bev=0.003), "Relay_Orange")
    return p.build((cx, cy, cz), pod_l)


def build_thruster(root):
    c = Vector((0, 0.04, 0))
    p = Part("Relay_Thruster")
    M = T(c.x, c.y, 0)
    bm, _ = lathe([(0.062, -0.248), (0.08, -0.246), (0.088, -0.232), (0.088, -0.19)], 24, M, ["Relay_DarkMetal"] * 3)
    p.merge(bm, "Relay_DarkMetal", sharp_deg=30)
    bm, _ = lathe([(0, -0.272), (0.022, -0.27), (0.045, -0.262), (0.062, -0.248)], 24, M, ["Relay_CyanGlow"] * 3)
    p.merge(bm, "Relay_CyanGlow", sharp_deg=60)
    return p.build((c.x, c.y, -0.235), root)


def build_heal(root):
    c = Vector((0, -0.13, 0))
    p = Part("Relay_HealEmitter")
    M = T(c.x, c.y, 0)
    bm, _ = lathe([(0.028, -0.238), (0.032, -0.232), (0.032, -0.19)], 16, M, ["Relay_Cream"] * 2)
    p.merge(bm, "Relay_Cream", sharp_deg=30)
    bm, _ = lathe([(0.03, -0.248), (0.037, -0.242), (0.037, -0.232), (0.033, -0.226)], 16, M, ["Relay_Orange"] * 3)
    p.merge(bm, "Relay_Orange", sharp_deg=30)
    bm, _ = lathe([(0, -0.272), (0.01, -0.27), (0.02, -0.262), (0.026, -0.246)], 16, M, ["Relay_CyanGlow"] * 3)
    p.merge(bm, "Relay_CyanGlow", sharp_deg=60)
    return p.build((c.x, c.y, -0.24), root)


def build_relay():
    col = bpy.data.collections.new("Relay")
    bpy.context.scene.collection.children.link(col)
    make_materials()
    root = bpy.data.objects.new("Relay", None)
    root.empty_display_type = "ARROWS"
    root.empty_display_size = 0.3
    col.objects.link(root)
    o = {"Body": build_body(root)}
    o["EyeRing"], o["EyeLens"] = build_eye(root)
    for s in (1, -1):
        side = "L" if s > 0 else "R"
        o["Joint_" + side] = build_joint(s, root)
        o["Pod_" + side] = build_pod(s, root)
    o["Blaster"] = build_blaster(o["Pod_L"])
    o["Thruster"] = build_thruster(root)
    o["HealEmitter"] = build_heal(root)
    bpy.context.view_layer.update()
    # outward-normal check: for each part, area-weighted normals should point away from its centre
    for name, ob in o.items():
        me = ob.data
        cen = sum((v.co for v in me.vertices), Vector()) / len(me.vertices)
        score = sum((p.center - cen).normalized().dot(p.normal) * p.area for p in me.polygons
                    if (p.center - cen).length > 1e-6)
        if score <= 0:
            print("RELAY WARNING inward normals on", name, round(score, 4), flush=True)
    tris = sum(sum(len(p.vertices) - 2 for p in ob.data.polygons) for ob in o.values())
    pts = [ob.matrix_world @ v.co for ob in o.values() for v in ob.data.vertices]
    lo = [min(p[k] for p in pts) for k in range(3)]
    hi = [max(p[k] for p in pts) for k in range(3)]
    print("RELAY tris", tris, "size", [round(hi[k] - lo[k], 3) for k in range(3)],
          "min", [round(c, 3) for c in lo], "max", [round(c, 3) for c in hi], flush=True)
    return root, o


# ---------------------------------------------------------------- preview + export
def preview_rig():
    col = bpy.data.collections.new("Preview (not exported)")
    bpy.context.scene.collection.children.link(col)
    tgt = Vector((0, 0, -0.01))

    def light(name, kind, energy, loc, color=(1, 1, 1), size=1.0):
        ld = bpy.data.lights.new(name, kind)
        ld.energy = energy
        ld.color = color
        if kind == "AREA":
            ld.size = size
        ob = bpy.data.objects.new(name, ld)
        ob.location = loc
        ob.rotation_euler = (tgt - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        col.objects.link(ob)
    light("Key", "AREA", 120, (1.4, -1.8, 1.6), (1.0, 0.97, 0.92), 1.2)
    light("Fill", "AREA", 50, (-1.8, -1.2, 0.6), (0.8, 0.9, 1.0), 1.5)
    light("Rim", "AREA", 90, (0.2, 1.9, 1.2), (0.85, 0.95, 1.0), 1.0)
    cam_d = bpy.data.cameras.new("PreviewCam")
    cam_d.type = "ORTHO"
    cam_d.ortho_scale = 1.25
    cam = bpy.data.objects.new("PreviewCam", cam_d)
    col.objects.link(cam)
    sc = bpy.context.scene
    sc.camera = cam
    world = bpy.data.worlds.new("RelayPreviewWorld")
    sc.world = world
    try:
        world.use_nodes = True
    except Exception:
        pass
    bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value = (0.045, 0.05, 0.06, 1)
    bg.inputs[1].default_value = 1.0
    return cam, tgt


def render_views(out_dir, cam, tgt):
    sc = bpy.context.scene
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT", "CYCLES"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            continue
    if sc.render.engine == "CYCLES":
        sc.cycles.samples = 48
    sc.render.resolution_x = sc.render.resolution_y = 700
    try:
        sc.view_settings.view_transform = "Standard"     # closer to Unity's look than AgX
    except TypeError:
        pass
    sc.render.film_transparent = False
    views = {"front": (0, -1, 0.12), "back": (0, 1, 0.15), "side": (1, 0, 0.1), "three_quarter": (0.75, -1, 0.45)}
    for name, d in views.items():
        dv = Vector(d).normalized()
        cam.location = tgt + dv * 3.0
        cam.rotation_euler = (tgt - cam.location).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(out_dir, f"Relay_{name}.png")
        bpy.ops.render.render(write_still=True)
    print("RELAY rendered with", sc.render.engine, flush=True)


def export_fbx(path, root):
    vl = bpy.context.view_layer
    for ob in vl.objects:
        ob.select_set(False)
    root.select_set(True)
    for ob in root.children_recursive:
        ob.select_set(True)
    vl.objects.active = root
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"EMPTY", "MESH"},
                             axis_forward="-Z", axis_up="Y", apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_ALL", bake_space_transform=False,
                             mesh_smooth_type="OFF", add_leaf_bones=False, bake_anim=False)
    for ob in vl.objects:
        ob.select_set(False)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = args[0] if args else os.getcwd()
    for sub in ("Source", "Exports", "Previews"):
        os.makedirs(os.path.join(out, sub), exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    root, parts = build_relay()
    cam, tgt = preview_rig()
    export_fbx(os.path.join(out, "Exports", "Relay.fbx"), root)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, "Source", "Relay.blend"))
    if "render" in args:
        render_views(os.path.join(out, "Previews"), cam, tgt)
        bpy.ops.wm.save_mainfile()
    print("RELAY done", flush=True)
