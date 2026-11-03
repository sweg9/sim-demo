# CLOiD-style home robot: white pearl shell, dark visor face, wheeled base, two articulated arms.
# exec(open(lib.py)) first.
clear_prefix('CLOiD_')

M_WHITE = M('m_white', (0.94, 0.945, 0.95), 0.18)
M_PEARL = M('m_pearl', (0.84, 0.86, 0.89), 0.28, 0.2)
M_GREY = M('m_grey', (0.50, 0.53, 0.57), 0.30, 0.7)
M_DARK = M('m_dark', (0.025, 0.03, 0.04), 0.06)
M_BASE = M('m_base', (0.09, 0.10, 0.115), 0.45)
M_LED = M('m_led', (0.10, 0.80, 1.00), 0.3, 0.0, (0.2, 0.9, 1.0), 4.0)

# ---------------------------------------------------------------- base
base = b_body(0.0, 0.17, 0.31, 0.305, cb=0.05, ct=0.05, seg=72)
bumper = b_body(0.050, 0.125, 0.326, 0.326, cb=0.032, ct=0.032, seg=72)
led = b_body(0.132, 0.150, 0.322, 0.322, cb=0.007, ct=0.007, seg=72)
make('CLOiD_Base', [(base, M_BASE), (bumper, M_GREY), (led, M_LED)])

# ---------------------------------------------------------------- body: one continuous, softly tapered shell
YR = 0.66
R_T = r_profile([(0.165, 0.262), (0.30, 0.238), (0.46, 0.208), (0.62, 0.196), (0.80, 0.208), (0.98, 0.226), (1.10, 0.232), (1.16, 0.226)],
                yr=YR, n=2.5, cb=0.02, ct=0.07, st=10, samples=56)
torso = b_loft(R_T, 96)
panel = b_patch(R_T, 0.80, 1.00, 38, mask_n=4.5, nu=190, nv=120, offset=0.003)
line = b_patch(R_T, 0.822, 0.832, 28, mask_n=10, nu=120, nv=4, offset=0.0045)
seam = b_patch(R_T, 0.592, 0.597, 180, center_deg=-90, mask_n=30, nu=120, nv=2, offset=0.0015)
neck = b_body(1.12, 1.23, 0.050, 0.050, cb=0.006, ct=0.006, seg=40)
caps = []
for sx in (1, -1):
    c = b_body(-0.074, 0.074, 0.074, 0.074, cb=0.074, ct=0.074, st=12, mid=2, seg=48)
    xf(c, loc=(sx * 0.262, 0, 1.075))
    caps.append(c)
make('CLOiD_Torso', [(torso, M_WHITE), (panel, M_DARK), (line, M_LED), (seam, M_GREY), (neck, M_DARK), (caps[0], M_GREY), (caps[1], M_GREY)])

# ---------------------------------------------------------------- head (origin at neck top so it can pan/tilt)
R_H = r_body(1.20, 1.46, 0.176, 0.176, yr=0.78, n=3.8, cb=0.07, ct=0.085, st=14, mid=8)
head = b_loft(R_H, 112)
visor = b_patch(R_H, 1.262, 1.412, 62, mask_n=5.0, nu=220, nv=120, offset=0.0028)
rg = ring_at(R_H, 1.338)
yf = -(rg[1]) - 0.0045
eyes = []
for sx in (1, -1):
    e = b_body(-0.028, 0.028, 0.0115, 0.0115, yr=0.5, cb=0.0115, ct=0.0115, st=8, mid=2, seg=32)
    xf(e, loc=(sx * 0.050, yf, 1.337))
    eyes.append(e)
rg2 = ring_at(R_H, 1.436)
lens = b_body(0.0, 0.008, 0.012, 0.012, cb=0.002, ct=0.002, seg=32)
xf(lens, loc=(0, -(rg2[1]) + 0.002, 1.436), rot=(90, 0, 0))
ears = []
for sx in (1, -1):
    ear = b_body(-0.008, 0.008, 0.040, 0.040, cb=0.007, ct=0.007, seg=48)
    xf(ear, loc=(sx * 0.178, 0, 1.325), rot=(0, 90, 0))
    ears.append(ear)
make('CLOiD_Head', [(head, M_WHITE), (visor, M_DARK), (eyes[0], M_LED), (eyes[1], M_LED), (lens, M_DARK),
                    (ears[0], M_GREY), (ears[1], M_GREY)], origin=(0, 0, 1.22))

# ---------------------------------------------------------------- arms (hanging pose, length along -Z)
SH = 1.075
for side, sx in (('P', 1), ('N', -1)):
    S = Vector((sx * 0.262, 0, SH))
    # upper arm: origin at shoulder
    up = b_body(-0.02, -0.40, 0.050, 0.041, cb=0.03, ct=0.03, st=8, mid=8, seg=48)
    el = b_body(-0.047, 0.047, 0.047, 0.047, cb=0.047, ct=0.047, st=10, mid=2, seg=40)
    xf(el, loc=(0, 0, -0.40))
    make('CLOiD_Upper_' + side, [(up, M_WHITE), (el, M_GREY)], origin=S, local=True)
    # forearm: origin at elbow
    E = S + Vector((0, 0, -0.40))
    fo = b_body(-0.02, -0.355, 0.040, 0.032, cb=0.03, ct=0.02, st=8, mid=8, seg=48)
    cuff = b_body(-0.338, -0.378, 0.037, 0.037, cb=0.008, ct=0.008, seg=40)
    make('CLOiD_Fore_' + side, [(fo, M_PEARL), (cuff, M_DARK)], origin=E, local=True)
    # hand: origin at wrist
    W = E + Vector((0, 0, -0.38))
    palm = b_body(-0.004, -0.088, 0.036, 0.034, yr=0.55, n=2.4, cb=0.02, ct=0.035, st=8, mid=3, seg=40)
    idx = b_body(-0.07, -0.130, 0.0095, 0.0085, cb=0.01, ct=0.0095, st=6, mid=2, seg=24)
    xf(idx, loc=(0.014, -0.004, 0))
    f2 = b_body(-0.07, -0.108, 0.0095, 0.0085, cb=0.01, ct=0.0095, st=6, mid=2, seg=24)
    xf(f2, loc=(-0.004, -0.004, 0))
    f3 = b_body(-0.07, -0.100, 0.0090, 0.0080, cb=0.01, ct=0.009, st=6, mid=2, seg=24)
    xf(f3, loc=(-0.020, -0.004, 0))
    th = b_body(-0.01, -0.062, 0.0105, 0.0095, cb=0.01, ct=0.01, st=6, mid=2, seg=24)
    xf(th, loc=(0.0, -0.032, 0.0), rot=(-38, 0, 0))
    make('CLOiD_Hand_' + side, [(palm, M_WHITE), (idx, M_WHITE), (f2, M_WHITE), (f3, M_WHITE), (th, M_WHITE)], origin=W, local=True)

print('robot objects:', [o.name for o in get_scene().objects if o.name.startswith('CLOiD_')])
