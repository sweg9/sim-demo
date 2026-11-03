# Blender helper library for the CLOiD demo assets.
# Run inside Blender:  exec(open('/Users/admin/LG/CLOiD-Demo/BlenderSrc/lib.py').read())
# Conventions: metres, Z up, the model's front faces -Y, origin at floor centre unless stated.
import bpy, bmesh, math, os
from mathutils import Vector, Matrix, Euler

SCENE_NAME = 'CLOiD_Build'
OUT_DIR = '/Users/admin/LG/CLOiD-Demo/Assets/Resources/Models'


# ------------------------------------------------------------------ scene
def get_scene():
    sc = bpy.data.scenes.get(SCENE_NAME)
    if sc is None:
        sc = bpy.data.scenes.new(SCENE_NAME)
    try:
        for w in bpy.context.window_manager.windows:
            w.scene = sc
    except Exception as e:
        print('window switch failed', e)
    return sc


def clear_prefix(prefix):
    sc = get_scene()
    for o in list(sc.objects):
        if o.name.startswith(prefix):
            bpy.data.objects.remove(o, do_unlink=True)


# ------------------------------------------------------------------ materials
def M(name, rgb, rough=0.5, metal=0.0, emit=None, es=0.0):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if b is None:
        b = m.node_tree.nodes.new('ShaderNodeBsdfPrincipled')
        out = next((n for n in m.node_tree.nodes if n.type == 'OUTPUT_MATERIAL'), None) or m.node_tree.nodes.new('ShaderNodeOutputMaterial')
        m.node_tree.links.new(b.outputs['BSDF'], out.inputs['Surface'])
    b.inputs['Base Color'].default_value = (rgb[0], rgb[1], rgb[2], 1)
    b.inputs['Roughness'].default_value = rough
    b.inputs['Metallic'].default_value = metal
    if emit is not None:
        b.inputs['Emission Color'].default_value = (emit[0], emit[1], emit[2], 1)
        b.inputs['Emission Strength'].default_value = es
    return m


# ------------------------------------------------------------------ geometry builders (return bmesh)
def r_body(zb, zt, rb, rt, yr=1.0, n=2.0, cb=None, ct=None, st=10, mid=8, bulge=0.0, cx=0.0, cy=0.0):
    """Ring list for a rounded elliptical/superelliptical body between zb and zt (works for zt<zb too)."""
    sgn = 1 if zt >= zb else -1
    h = abs(zt - zb)
    cb = 0.0 if cb is None else cb
    ct = 0.0 if ct is None else ct

    def rad(t):
        return (rb + (rt - rb) * t) * (1 + bulge * math.sin(math.pi * t))

    rings = []
    for k in range(st + 1):
        th = (math.pi / 2) * k / st
        z = zb + sgn * cb * (1 - math.cos(th))
        t = (z - zb) / (zt - zb) if zt != zb else 0
        f = math.sin(th)
        r = rad(min(max(t, 0), 1))
        rings.append((z, r * f, r * f * yr, n, cx, cy))
    zs, ze = zb + sgn * cb, zt - sgn * ct
    for i in range(1, mid):
        z = zs + (ze - zs) * i / mid
        t = (z - zb) / (zt - zb)
        r = rad(t)
        rings.append((z, r, r * yr, n, cx, cy))
    for k in range(st + 1):
        th = (math.pi / 2) * (1 - k / st)
        z = zt - sgn * ct * (1 - math.cos(th))
        t = (z - zb) / (zt - zb) if zt != zb else 1
        f = math.sin(th)
        r = rad(min(max(t, 0), 1))
        rings.append((z, r * f, r * f * yr, n, cx, cy))
    return rings


def r_profile(pts, yr=1.0, n=2.0, cb=0.03, ct=0.05, st=10, samples=40, cx=0.0, cy=0.0, ny=None):
    """Smooth ring list from (z, radius) control points (Catmull-Rom), with rounded end caps."""
    P = pts
    def cr(p0, p1, p2, p3, t):
        return 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t)
    ext = [P[0]] + P + [P[-1]]
    zs, rs = [], []
    seg = len(P) - 1
    for i in range(seg):
        for k in range(samples // seg if samples // seg > 0 else 1):
            t = k / float(samples // seg)
            zs.append(cr(ext[i][0], ext[i + 1][0], ext[i + 2][0], ext[i + 3][0], t))
            rs.append(cr(ext[i][1], ext[i + 1][1], ext[i + 2][1], ext[i + 3][1], t))
    zs.append(P[-1][0]); rs.append(P[-1][1])
    sgn = 1 if zs[-1] >= zs[0] else -1
    rings = []
    z0, r0 = zs[0], rs[0]
    for k in range(st + 1):
        th = (math.pi / 2) * k / st
        rings.append((z0 + sgn * cb * (1 - math.cos(th)), r0 * math.sin(th), r0 * math.sin(th) * yr, n, cx, cy))
    for z, r in zip(zs, rs):
        if sgn * (z - (z0 + sgn * cb)) > 0 and sgn * ((zs[-1] - sgn * ct) - z) > 0:
            rings.append((z, r, r * yr, n, cx, cy))
    z1, r1 = zs[-1], rs[-1]
    for k in range(st + 1):
        th = (math.pi / 2) * (1 - k / st)
        rings.append((z1 - sgn * ct * (1 - math.cos(th)), r1 * math.sin(th), r1 * math.sin(th) * yr, n, cx, cy))
    return rings


def b_loft(rings, seg=64):
    bm = bmesh.new()
    rows = []
    for (z, rx, ry, n, cx, cy) in rings:
        if rx <= 1e-7 and ry <= 1e-7:
            rows.append([bm.verts.new((cx, cy, z))])
            continue
        row = []
        e = 2.0 / n
        for j in range(seg):
            a = 2 * math.pi * j / seg
            c, s = math.cos(a), math.sin(a)
            x = rx * math.copysign(abs(c) ** e, c)
            y = ry * math.copysign(abs(s) ** e, s)
            row.append(bm.verts.new((cx + x, cy + y, z)))
        rows.append(row)
    for i in range(len(rows) - 1):
        r0, r1 = rows[i], rows[i + 1]
        for j in range(seg):
            j2 = (j + 1) % seg
            if len(r0) == 1 and len(r1) == 1:
                continue
            if len(r0) == 1:
                bm.faces.new((r0[0], r1[j2], r1[j]))
            elif len(r1) == 1:
                bm.faces.new((r0[j], r0[j2], r1[0]))
            else:
                bm.faces.new((r0[j], r0[j2], r1[j2], r1[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def b_body(zb, zt, rb, rt, seg=64, **kw):
    return b_loft(r_body(zb, zt, rb, rt, **kw), seg)


def b_rbox(size, bevel=0.02, seg=4, loc=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co.x *= size[0]; v.co.y *= size[1]; v.co.z *= size[2]
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=min(bevel, min(size) * 0.49), segments=seg,
                        profile=0.5, affect='EDGES')
    for v in bm.verts:
        v.co += Vector(loc)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def b_rrect(w, d, h, r, seg=10, bevel=0.01, loc=(0, 0, 0)):
    """Rounded-rectangle slab (plan view), e.g. table tops, rugs. Origin at bottom centre."""
    rings = []
    # build via loft of superellipse approximated: use polygon outline extrude
    bm = bmesh.new()
    pts = []
    for cxs, cys, a0 in ((1, 1, 0), (-1, 1, 90), (-1, -1, 180), (1, -1, 270)):
        for k in range(seg + 1):
            a = math.radians(a0 + 90 * k / seg)
            pts.append(((w / 2 - r) * cxs + r * math.cos(a), (d / 2 - r) * cys + r * math.sin(a)))
    bot = [bm.verts.new((x, y, 0)) for x, y in pts]
    top = [bm.verts.new((x, y, h)) for x, y in pts]
    n = len(pts)
    for i in range(n):
        bm.faces.new((bot[i], bot[(i + 1) % n], top[(i + 1) % n], top[i]))
    bm.faces.new(bot[::-1])
    bm.faces.new(top)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=min(bevel, h * 0.49), segments=3, profile=0.5, affect='EDGES')
    for v in bm.verts:
        v.co += Vector(loc)
    return bm


def ring_at(rings, z):
    best = None
    for i in range(len(rings) - 1):
        a, b = rings[i], rings[i + 1]
        lo, hi = min(a[0], b[0]), max(a[0], b[0])
        if lo - 1e-9 <= z <= hi + 1e-9 and abs(b[0] - a[0]) > 1e-9:
            t = (z - a[0]) / (b[0] - a[0])
            best = tuple(a[k] + (b[k] - a[k]) * t for k in range(1, 6))
            break
    return best  # (rx, ry, n, cx, cy)


def b_patch(rings, z0, z1, u_deg, center_deg=-90.0, mask_n=4.0, nu=60, nv=40, offset=0.003):
    """Thin rounded-rectangle skin laid over a loft body (visor / chest panel)."""
    bm = bmesh.new()
    grid = {}
    for i in range(nv + 1):
        z = z0 + (z1 - z0) * i / nv
        rg = ring_at(rings, z)
        if rg is None:
            continue
        rx, ry, n, cx, cy = rg
        for j in range(nu + 1):
            u = -u_deg + 2 * u_deg * j / nu
            a = math.radians(center_deg + u)
            c, s = math.cos(a), math.sin(a)
            e = 2.0 / n
            x = rx * math.copysign(abs(c) ** e, c)
            y = ry * math.copysign(abs(s) ** e, s)
            L = math.hypot(x / max(rx, 1e-6), y / max(ry, 1e-6))
            nx_, ny_ = x / max(rx, 1e-6) ** 2, y / max(ry, 1e-6) ** 2
            nl = math.hypot(nx_, ny_) or 1
            grid[(i, j)] = bm.verts.new((cx + x + offset * nx_ / nl, cy + y + offset * ny_ / nl, z))
    for i in range(nv):
        for j in range(nu):
            uu = ((j + 0.5) / nu) * 2 - 1
            vv = ((i + 0.5) / nv) * 2 - 1
            if abs(uu) ** mask_n + abs(vv) ** mask_n > 1.0:
                continue
            ks = [(i, j), (i, j + 1), (i + 1, j + 1), (i + 1, j)]
            if all(k in grid for k in ks):
                try:
                    bm.faces.new([grid[k] for k in ks])
                except ValueError:
                    pass
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


# ------------------------------------------------------------------ transforms
def xf(bm, loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
    mat = Matrix.Translation(Vector(loc)) @ Euler([math.radians(a) for a in rot], 'XYZ').to_matrix().to_4x4()
    sm = Matrix.Diagonal(Vector((scale[0], scale[1], scale[2], 1)))
    mat = mat @ sm
    bmesh.ops.transform(bm, matrix=mat, verts=bm.verts[:])
    return bm


def b_join(bms):
    out = bmesh.new()
    for b in bms:
        tm = bpy.data.meshes.new('t')
        b.to_mesh(tm)
        out.from_mesh(tm)
        bpy.data.meshes.remove(tm)
    return out


def mirror_x(bm):
    for v in bm.verts:
        v.co.x = -v.co.x
    bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    return bm


# ------------------------------------------------------------------ object creation
def make(name, parts, origin=None, sharp=42, collection=None, local=False):
    """parts: list of (bmesh, material). Merge into one object with one slot per material."""
    master = bmesh.new()
    mats = []
    for bm, mat in parts:
        if mat not in mats:
            mats.append(mat)
        idx = mats.index(mat)
        start = len(master.faces)
        tm = bpy.data.meshes.new('t')
        bm.to_mesh(tm)
        master.from_mesh(tm)
        bpy.data.meshes.remove(tm)
        master.faces.ensure_lookup_table()
        for f in master.faces[start:]:
            f.material_index = idx
    org = Vector(origin) if origin is not None else Vector((0, 0, 0))
    if origin is not None and not local:
        for v in master.verts:
            v.co -= org
    bmesh.ops.recalc_face_normals(master, faces=master.faces[:])
    ang = math.radians(sharp)
    for f in master.faces:
        f.smooth = True
    for e in master.edges:
        if len(e.link_faces) == 2:
            try:
                if e.calc_face_angle() > ang:
                    e.smooth = False
            except ValueError:
                pass
    me = bpy.data.meshes.new(name)
    master.to_mesh(me)
    master.free()
    for m in mats:
        me.materials.append(m)
    old = bpy.data.objects.get(name)
    if old:
        bpy.data.objects.remove(old, do_unlink=True)
    ob = bpy.data.objects.new(name, me)
    ob.location = org
    (collection or get_scene().collection).objects.link(ob)
    return ob


def export_fbx(path, prefix=None, names=None):
    sc = get_scene()
    for o in sc.objects:
        o.select_set(False)
    n = 0
    for o in sc.objects:
        if o.type != 'MESH':
            continue
        if (prefix and o.name.startswith(prefix)) or (names and o.name in names):
            o.select_set(True)
            n += 1
    vl = sc.view_layers[0]
    ov = {}
    try:
        win = bpy.context.window_manager.windows[0]
        ov = dict(window=win, scene=sc, view_layer=vl)
    except Exception:
        pass
    os.makedirs(os.path.dirname(path), exist_ok=True)
    kw = dict(filepath=path, use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y',
              global_scale=1.0, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
              bake_space_transform=True, mesh_smooth_type='EDGE', use_mesh_modifiers=True,
              add_leaf_bones=False, path_mode='AUTO')
    with bpy.context.temp_override(**ov):
        bpy.ops.export_scene.fbx(**kw)
    return n
