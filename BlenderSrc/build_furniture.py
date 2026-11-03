# Clean modern furniture, lighting, plants and wall controls for the demo house.
# exec(open(lib.py)) first. Front faces -Y, origin = floor centre (lights/switches: attachment point).
import random

for p in ('F_', 'Switch_'):
    clear_prefix(p)

# ---------------------------------------------------------------- materials
FAB_BLUE = M('m_fabric_blue', (0.38, 0.44, 0.52), 0.92)
FAB_SAND = M('m_fabric_sand', (0.80, 0.74, 0.64), 0.92)
FAB_GREY = M('m_fabric_grey', (0.64, 0.65, 0.67), 0.92)
WALNUT = M('m_walnut', (0.30, 0.19, 0.12), 0.42)
OAK = M('m_oak', (0.74, 0.58, 0.40), 0.5)
BLACK = M('m_blackmetal', (0.045, 0.047, 0.055), 0.35, 0.8)
BRASS = M('m_brass', (0.80, 0.62, 0.32), 0.28, 1.0)
WHITE = M('m_white_matte', (0.94, 0.94, 0.925), 0.55)
STONE = M('m_stone', (0.11, 0.115, 0.13), 0.22)
STEEL = M('m_steel', (0.72, 0.74, 0.77), 0.3, 0.9)
CERAMIC = M('m_ceramic', (0.96, 0.95, 0.93), 0.18)
LEAF = M('m_leaf', (0.16, 0.40, 0.20), 0.42)
LEAF2 = M('m_leaf2', (0.10, 0.30, 0.16), 0.42)
SOIL = M('m_soil', (0.07, 0.055, 0.045), 0.9)
RUG = M('m_rug', (0.90, 0.87, 0.82), 0.97)
GLOW = M('m_glow', (1.0, 0.85, 0.6), 0.3, 0.0, (1.0, 0.78, 0.45), 2.0)
SCREEN = M('m_screen', (0.008, 0.010, 0.016), 0.04)
BED = M('m_bedding', (0.93, 0.94, 0.95), 0.92)
THROW = M('m_throw', (0.50, 0.58, 0.67), 0.92)
LED = M('m_led', (0.10, 0.80, 1.00), 0.3, 0.0, (0.2, 0.9, 1.0), 4.0)
PLATE = M('m_plate', (0.96, 0.96, 0.95), 0.4)
TEAK = M('m_teak', (0.55, 0.38, 0.22), 0.45)


# ---------------------------------------------------------------- small helpers
def rb(size, loc, bevel=0.02, seg=3):
    return b_rbox(size, bevel, seg, loc)


def cyl(r, h, loc=(0, 0, 0), cap=0.004, seg=40):
    b = b_body(0, h, r, r, cb=cap, ct=cap, st=3, mid=2, seg=seg)
    return xf(b, loc=loc)


def limb(p0, p1, r0, r1, seg=24, cap=None):
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    L = d.length
    c0 = (cap if cap is not None else r0 * 0.7)
    c1 = (cap if cap is not None else r1 * 0.7)
    b = b_body(0, L, r0, r1, cb=c0, ct=c1, st=4, mid=2, seg=seg)
    q = Vector((0, 0, 1)).rotation_difference(d.normalized())
    m = Matrix.Translation(p0) @ q.to_matrix().to_4x4()
    bmesh.ops.transform(b, matrix=m, verts=b.verts[:])
    return b


def aim(bm, p0, d, roll=0.0):
    d = Vector(d).normalized()
    q = Vector((0, 0, 1)).rotation_difference(d)
    m = Matrix.Translation(Vector(p0)) @ q.to_matrix().to_4x4() @ Matrix.Rotation(math.radians(roll), 4, 'Z')
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts[:])
    return bm


def groove_lines_x(w, y, z0, z1, xs, t=0.004):
    return [rb((t, 0.004, z1 - z0), (x, y, (z0 + z1) / 2), 0.0, 1) for x in xs]


# ---------------------------------------------------------------- seating
def sofa():
    W, D = 2.5, 0.96
    parts = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append((cyl(0.017, 0.12, (sx * (W / 2 - 0.12), sy * (D / 2 - 0.12), 0)), BLACK))
    parts.append((rb((W, D, 0.17), (0, 0, 0.205), 0.04), FAB_BLUE))           # base
    parts.append((rb((W, 0.2, 0.62), (0, D / 2 - 0.10, 0.5), 0.07), FAB_BLUE))   # back frame
    for ax in (-1, 1):
        parts.append((rb((0.22, D, 0.55), (ax * (W / 2 - 0.11), 0, 0.455), 0.08), FAB_BLUE))
    sw = (W - 0.44) / 3.0
    for i in range(3):
        x = -((W - 0.44) / 2) + sw * (i + 0.5)
        parts.append((rb((sw - 0.01, 0.70, 0.17), (x, -0.07, 0.375), 0.065, 4), FAB_BLUE))
        bk = rb((sw - 0.02, 0.2, 0.40), (0, 0, 0), 0.07, 4)
        xf(bk, loc=(x, 0.22, 0.66), rot=(-12, 0, 0))
        parts.append((bk, FAB_BLUE))
    return make('F_Sofa', parts)


def armchair():
    parts = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append((limb((sx * 0.26, sy * 0.26, 0), (sx * 0.30, sy * 0.30, 0.2), 0.012, 0.018), WALNUT))
    parts.append((rb((0.84, 0.82, 0.14), (0, 0, 0.27), 0.05), FAB_SAND))
    parts.append((rb((0.74, 0.62, 0.15), (0, -0.04, 0.40), 0.06, 4), FAB_SAND))
    bk = rb((0.78, 0.18, 0.5), (0, 0, 0), 0.08, 4)
    xf(bk, loc=(0, 0.30, 0.62), rot=(-14, 0, 0))
    parts.append((bk, FAB_SAND))
    for ax in (-1, 1):
        parts.append((rb((0.14, 0.7, 0.30), (ax * 0.36, -0.04, 0.45), 0.06), FAB_SAND))
    return make('F_Armchair', parts)


def coffee_table():
    parts = [(b_body(0.34, 0.385, 0.46, 0.46, cb=0.012, ct=0.016, seg=80, st=4, mid=2), WALNUT),
             (b_body(0.13, 0.16, 0.34, 0.34, cb=0.01, ct=0.01, seg=80, st=3, mid=2), OAK),
             (cyl(0.028, 0.34, (0, 0, 0.0)), BLACK),
             (b_body(0.0, 0.02, 0.2, 0.2, cb=0.008, ct=0.008, seg=60, st=3, mid=2), BLACK)]
    return make('F_CoffeeTable', parts)


def side_table():
    parts = [(b_body(0.46, 0.5, 0.2, 0.2, cb=0.008, ct=0.012, seg=60, st=3, mid=2), BRASS),
             (cyl(0.016, 0.46), BRASS), (b_body(0.0, 0.02, 0.15, 0.15, cb=0.008, ct=0.008, seg=48, st=3, mid=2), BRASS)]
    return make('F_SideTable', parts)


def dining_table():
    parts = [(b_rrect(1.9, 0.95, 0.045, 0.14, 10, 0.012, (0, 0, 0.715)), WALNUT)]
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append((limb((sx * 0.8, sy * 0.36, 0), (sx * 0.84, sy * 0.40, 0.72), 0.022, 0.026), BLACK))
    return make('F_DiningTable', parts)


def dining_chair():
    parts = [(rb((0.46, 0.44, 0.05), (0, 0, 0.45), 0.02, 3), FAB_GREY)]
    for sx in (-1, 1):
        parts.append((limb((sx * 0.20, -0.20, 0), (sx * 0.19, -0.18, 0.45), 0.013, 0.016), WALNUT))
        parts.append((limb((sx * 0.21, 0.24, 0), (sx * 0.19, 0.20, 0.45), 0.013, 0.016), WALNUT))
        parts.append((limb((sx * 0.19, 0.20, 0.45), (sx * 0.20, 0.26, 0.86), 0.013, 0.012), WALNUT))
    bk = rb((0.44, 0.03, 0.30), (0, 0, 0), 0.012, 3)
    xf(bk, loc=(0, 0.255, 0.70), rot=(-6, 0, 0))
    parts.append((bk, WALNUT))
    return make('F_DiningChair', parts)


def stool():
    parts = [(b_body(0.66, 0.70, 0.19, 0.19, cb=0.012, ct=0.016, seg=60, st=3, mid=2), FAB_GREY),
             (b_body(0.28, 0.295, 0.15, 0.15, cb=0.003, ct=0.005, seg=60, st=2, mid=2), BLACK)]
    for k in range(4):
        a = math.radians(45 + 90 * k)
        parts.append((limb((math.cos(a) * 0.17, math.sin(a) * 0.17, 0.66), (math.cos(a) * 0.20, math.sin(a) * 0.20, 0), 0.012, 0.012), BLACK))
    return make('F_Stool', parts)


# ---------------------------------------------------------------- kitchen
def island():
    parts = [(rb((2.7, 0.92, 0.1), (0, 0.0, 0.05), 0.0, 1), BLACK),
             (rb((2.7, 0.98, 0.79), (0, 0.02, 0.495), 0.012, 3), WHITE),
             (b_rrect(2.9, 1.22, 0.05, 0.03, 4, 0.006, (0, 0.06, 0.88)), STONE)]
    for x in (-0.9, -0.45, 0.0, 0.45, 0.9):
        parts.append((rb((0.004, 0.004, 0.66), (x, -0.472, 0.52), 0, 1), BLACK))
    return make('F_Island', parts)


def counter():
    parts = [(rb((4.0, 0.58, 0.1), (0, 0.0, 0.05), 0.0, 1), BLACK),
             (rb((4.0, 0.64, 0.78), (0, 0.0, 0.49), 0.012, 3), WHITE),
             (b_rrect(4.04, 0.68, 0.04, 0.012, 4, 0.004, (0, 0, 0.90)), STONE)]
    for x in [-1.5 + 0.5 * i for i in range(7)]:
        parts.append((rb((0.004, 0.004, 0.66), (x, -0.322, 0.52), 0, 1), BLACK))
    parts.append((rb((4.0, 0.004, 0.004), (0, -0.322, 0.84), 0, 1), BLACK))
    return make('F_Counter', parts)


def fridge():
    parts = [(rb((0.76, 0.74, 1.86), (0, 0, 0.95), 0.03, 4), STEEL),
             (rb((0.76, 0.004, 0.006), (0, -0.372, 1.25), 0, 1), BLACK)]
    for z0, z1 in ((1.30, 1.75), (0.35, 0.95)):
        parts.append((rb((0.018, 0.03, z1 - z0), (0.30, -0.385, (z0 + z1) / 2), 0.008, 2), BLACK))
    parts.append((rb((0.7, 0.62, 0.04), (0, 0, 0.02), 0.0, 1), BLACK))
    return make('F_Fridge', parts)


# ---------------------------------------------------------------- bedroom
def bed():
    parts = [(rb((2.0, 2.16, 0.26), (0, 0, 0.14), 0.03), OAK),
             (rb((1.92, 2.02, 0.24), (0, -0.02, 0.38), 0.07, 4), BED),
             (rb((2.22, 0.1, 1.0), (0, 1.08, 0.5), 0.05, 3), FAB_GREY)]
    for sx in (-1, 1):
        pl = rb((0.66, 0.40, 0.15), (0, 0, 0), 0.07, 4)
        xf(pl, loc=(sx * 0.46, 0.76, 0.58), rot=(-14, 0, 0))
        parts.append((pl, BED))
    parts.append((rb((1.96, 1.1, 0.06), (0, -0.30, 0.52), 0.03, 3), THROW))
    parts.append((rb((1.97, 0.46, 0.075), (0, -0.78, 0.53), 0.035, 3), BED))
    return make('F_Bed', parts)


def nightstand():
    parts = [(rb((0.5, 0.42, 0.38), (0, 0, 0.34), 0.02, 3), WALNUT),
             (rb((0.44, 0.004, 0.14), (0, -0.212, 0.40), 0, 1), BLACK),
             (rb((0.12, 0.012, 0.012), (0, -0.222, 0.40), 0.004, 2), BRASS)]
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append((limb((sx * 0.21, sy * 0.16, 0), (sx * 0.22, sy * 0.17, 0.16), 0.012, 0.016), BLACK))
    return make('F_Nightstand', parts)


def wardrobe():
    W = 2.4
    parts = [(rb((W, 0.62, 2.2), (0, 0, 1.1), 0.02, 3), WHITE)]
    for i in range(4):
        x = -W / 2 + W / 8 + i * W / 4
        parts.append((rb((W / 4 - 0.012, 0.012, 2.1), (x, -0.316, 1.1), 0.004, 2), OAK if i % 2 else WHITE))
        parts.append((rb((0.012, 0.014, 0.5), (x + (0.11 if i % 2 == 0 else -0.11), -0.328, 1.1), 0.004, 2), BLACK))
    return make('F_Wardrobe', parts)


# ---------------------------------------------------------------- living
def tv_unit():
    parts = [(rb((2.3, 0.46, 0.42), (0, 0, 0.36), 0.015, 3), WALNUT),
             (rb((2.28, 0.004, 0.004), (0, -0.232, 0.36), 0, 1), BLACK)]
    for x in (-0.76, 0.0, 0.76):
        parts.append((rb((0.004, 0.004, 0.36), (x * 0.5 if x else 0.0, -0.232, 0.36), 0, 1), BLACK))
    for sx in (-1, 1):
        parts.append((limb((sx * 1.05, 0.0, 0), (sx * 1.07, 0.0, 0.15), 0.014, 0.02), BLACK))
    return make('F_TVUnit', parts)


def tv():
    parts = [(rb((1.66, 0.04, 0.96), (0, 0, 1.38), 0.012, 3), BLACK),
             (rb((1.60, 0.006, 0.90), (0, -0.021, 1.38), 0.006, 2), SCREEN)]
    return make('F_TV', parts)


def rug():
    return make('F_Rug', [(b_rrect(3.4, 2.4, 0.022, 0.12, 8, 0.008, (0, 0, 0)), RUG)])


def rug_round():
    return make('F_RugRound', [(b_body(0, 0.022, 1.2, 1.2, cb=0.006, ct=0.008, seg=96, st=3, mid=2), RUG)])


def console():
    parts = [(b_rrect(1.5, 0.36, 0.035, 0.02, 4, 0.008, (0, 0, 0.78)), WALNUT),
             (rb((1.4, 0.3, 0.03), (0, 0, 0.30), 0.01, 2), WALNUT)]
    for sx in (-1, 1):
        parts.append((rb((0.03, 0.3, 0.78), (sx * 0.68, 0, 0.39), 0.008, 2), BLACK))
    return make('F_Console', parts)


def vase():
    prof = r_profile([(0, 0.05), (0.07, 0.085), (0.16, 0.06), (0.24, 0.032), (0.29, 0.04)], n=2, cb=0.03, ct=0.004, st=8, samples=24)
    return make('F_Vase', [(b_loft(prof, 48), CERAMIC)])


# ---------------------------------------------------------------- lighting (the *_Glow objects are driven by the wall switches)
def floor_lamp():
    parts = [(b_body(0, 0.025, 0.15, 0.15, cb=0.008, ct=0.008, seg=60, st=3, mid=2), BLACK),
             (cyl(0.011, 1.55, (0, 0, 0.02)), BLACK)]
    sh = r_profile([(1.40, 0.19), (1.50, 0.185), (1.62, 0.15), (1.70, 0.10)], n=2, cb=0.0, ct=0.07, st=8, samples=24)
    parts.append((b_loft(sh, 72), WHITE))
    make('F_FloorLamp', parts)
    gl = [(b_body(1.395, 1.405, 0.185, 0.185, cb=0.004, ct=0.004, seg=60, st=2, mid=2), GLOW),
          (b_body(1.43, 1.5, 0.045, 0.045, cb=0.045, ct=0.045, seg=32, st=8, mid=2), GLOW)]
    return make('F_FloorLamp_Glow', gl)


def pendant():
    parts = [(cyl(0.004, 0.7, (0, 0, -0.74)), BLACK),
             (b_body(-0.04, 0.0, 0.03, 0.03, cb=0.004, ct=0.004, seg=32, st=3, mid=2), BRASS)]
    sh = r_profile([(-0.72, 0.03), (-0.78, 0.10), (-0.88, 0.20), (-0.96, 0.25)], n=2, cb=0.0, ct=0.012, st=6, samples=24)
    parts.append((b_loft(sh, 72), WHITE))
    make('F_Pendant', parts)
    return make('F_Pendant_Glow', [(b_body(-0.972, -0.962, 0.235, 0.235, cb=0.004, ct=0.004, seg=60, st=2, mid=2), GLOW),
                                    (b_body(-0.93, -0.87, 0.04, 0.04, cb=0.04, ct=0.04, seg=24, st=6, mid=2), GLOW)])


def sconce():
    make('F_Sconce', [(rb((0.07, 0.02, 0.22), (0, 0.01, 0), 0.01, 2), BRASS), (cyl(0.012, 0.06, (0, 0.03, -0.08)), BRASS)])
    glow = b_body(-0.10, 0.10, 0.04, 0.04, cb=0.04, ct=0.04, seg=32, st=6, mid=2)
    xf(glow, loc=(0, -0.05, 0))
    return make('F_Sconce_Glow', [(glow, GLOW)])


def bedside_lamp():
    parts = [(b_body(0, 0.02, 0.06, 0.06, cb=0.006, ct=0.006, seg=40, st=3, mid=2), BRASS),
             (cyl(0.008, 0.2, (0, 0, 0.02)), BRASS)]
    sh = r_profile([(0.20, 0.10), (0.27, 0.095), (0.34, 0.085)], n=2, cb=0.0, ct=0.0, st=6, samples=16)
    parts.append((b_loft(sh, 48), WHITE))
    make('F_BedsideLamp', parts)
    return make('F_BedsideLamp_Glow', [(b_body(0.215, 0.225, 0.09, 0.09, cb=0.004, ct=0.004, seg=40, st=2, mid=2), GLOW)])


def led_strip(name, length):
    return make(name, [(rb((length, 0.012, 0.012), (0, 0, 0), 0.004, 2), GLOW)])


# ---------------------------------------------------------------- plants
def plant_large():
    rnd = random.Random(7)
    pot = r_profile([(0.0, 0.14), (0.16, 0.19), (0.34, 0.22)], n=2, cb=0.02, ct=0.01, st=6, samples=18)
    parts = [(b_loft(pot, 64), CERAMIC),
             (b_body(0.33, 0.345, 0.205, 0.205, cb=0.003, ct=0.003, seg=48, st=2, mid=2), SOIL),
             (limb((0, 0, 0.34), (0.05, 0.02, 0.95), 0.022, 0.016), WALNUT),
             (limb((0.05, 0.02, 0.95), (0.0, 0.05, 1.45), 0.016, 0.008), WALNUT)]
    leafprof = [(0, 0.012), (0.06, 0.07), (0.15, 0.085), (0.26, 0.055), (0.32, 0.012)]
    for i in range(26):
        t = i / 25.0
        z = 0.62 + 0.9 * t
        trunk_x = 0.05 * min(1.0, (z - 0.34) / 0.6) * (1 - max(0, (z - 0.95)) / 0.5 * 0.9)
        az = math.radians(i * 137.5)
        el = math.radians(rnd.uniform(5, 40))
        d = Vector((math.cos(az) * math.cos(el), math.sin(az) * math.cos(el), math.sin(el) + 0.1 - 0.35 * (1 - t)))
        lf = b_loft(r_profile(leafprof, yr=0.07, n=2, cb=0.01, ct=0.02, st=5, samples=20), 28)
        aim(lf, (trunk_x, 0.02 * t, z), d, roll=rnd.uniform(0, 360))
        parts.append((lf, LEAF if i % 3 else LEAF2))
    return make('F_Plant', parts)


def plant_small():
    rnd = random.Random(3)
    pot = r_profile([(0.0, 0.10), (0.12, 0.13), (0.24, 0.15)], n=2, cb=0.015, ct=0.008, st=5, samples=16)
    parts = [(b_loft(pot, 48), CERAMIC), (b_body(0.23, 0.24, 0.145, 0.145, cb=0.003, ct=0.003, seg=40, st=2, mid=2), SOIL)]
    for i in range(11):
        az = math.radians(i * 137.5)
        tilt = rnd.uniform(4, 22)
        L = rnd.uniform(0.45, 0.85)
        pr = [(0, 0.012), (L * 0.2, 0.03), (L * 0.6, 0.034), (L * 0.95, 0.008)]
        lf = b_loft(r_profile(pr, yr=0.22, n=2, cb=0.01, ct=0.03, st=5, samples=20), 24)
        d = Vector((math.cos(az) * math.sin(math.radians(tilt)), math.sin(az) * math.sin(math.radians(tilt)), math.cos(math.radians(tilt))))
        aim(lf, (math.cos(az) * 0.03, math.sin(az) * 0.03, 0.24), d, roll=math.degrees(az))
        parts.append((lf, LEAF2 if i % 2 else LEAF))
    return make('F_PlantSmall', parts)


# ---------------------------------------------------------------- charging dock
def dock():
    parts = [(b_body(0, 0.022, 0.52, 0.52, cb=0.01, ct=0.01, seg=96, st=3, mid=2), BLACK)]
    make('F_Dock', parts + [(rb((0.24, 0.07, 0.55), (0, 0.46, 0.275), 0.03, 4), WHITE)])
    ring = b_body(0.020, 0.028, 0.50, 0.50, cb=0.003, ct=0.003, seg=96, st=2, mid=2)
    inner = b_body(0.020, 0.030, 0.47, 0.47, cb=0.002, ct=0.002, seg=96, st=2, mid=2)
    # make the ring hollow by only keeping the outer lip visible: lip radius slightly larger than the inner disc
    strip = rb((0.05, 0.012, 0.3), (0, 0.418, 0.3), 0.004, 2)
    return make('F_Dock_Glow', [(ring, LED), (strip, LED)])


# ---------------------------------------------------------------- wall controls (front faces -Y, origin on the wall surface)
def switches():
    plate = rb((0.092, 0.012, 0.092), (0, -0.006, 0), 0.012, 4)
    socket = rb((0.020, 0.004, 0.020), (0.032, -0.013, 0.032), 0.004, 2)
    make('Switch_Plate', [(plate, PLATE), (socket, BLACK)])
    # rocker: origin at its centre; rotates about X
    rk = rb((0.046, 0.012, 0.066), (0, -0.006, 0), 0.008, 4)
    make('Switch_Rocker', [(rk, PLATE)], origin=(0, -0.016, 0), local=False)
    pb = b_body(0, 0.012, 0.027, 0.027, cb=0.004, ct=0.005, seg=48, st=4, mid=2)
    xf(pb, loc=(0, -0.012, 0), rot=(90, 0, 0))
    ring = b_body(0, 0.003, 0.034, 0.034, cb=0.001, ct=0.001, seg=48, st=2, mid=2)
    xf(ring, loc=(0, -0.012, 0), rot=(90, 0, 0))
    make('Switch_Push', [(pb, PLATE), (ring, BLACK)], origin=(0, -0.012, 0))
    kn = b_body(0, 0.016, 0.031, 0.029, cb=0.004, ct=0.005, seg=64, st=4, mid=2)
    xf(kn, loc=(0, -0.012, 0), rot=(90, 0, 0))
    mark = rb((0.006, 0.003, 0.016), (0, -0.0285, 0.017), 0.001, 2)
    make('Switch_Dial', [(kn, PLATE), (mark, BLACK)], origin=(0, -0.012, 0))
    led = b_body(-0.005, 0.005, 0.0055, 0.0055, cb=0.0055, ct=0.0055, seg=16, st=5, mid=2)
    xf(led, loc=(0.032, -0.0165, 0.032))
    make('Switch_Led', [(led, LED)], origin=(0.032, -0.0165, 0.032))


for fn in (sofa, armchair, coffee_table, side_table, dining_table, dining_chair, stool, island, counter, fridge, bed,
           nightstand, wardrobe, tv_unit, tv, rug, rug_round, console, vase, floor_lamp, pendant, sconce, bedside_lamp,
           plant_large, plant_small, dock, switches):
    fn()
led_strip('F_LedStrip_Glow', 1.0)
print(len([o for o in get_scene().objects if o.name.startswith('F_') or o.name.startswith('Switch_')]), 'furniture objects')
