"""The six Kalyeah kids as chunky, smooth, matte stylised 3D models.

Run with the Blender Python module (bpy 4.5 LTS):
    python tools/blender/characters.py -- <out_dir> [lineup|export|both] [name ...]

Reference: the owner's picture (Junjun, Popoy, Ligaya, Migo, Toni, Inday). See
kidlib.py for the style guide; audit.py checks the result.
"""

import math
import random
import sys

import bpy
from mathutils import Vector

import kidlib as L
from kidlib import M, block, ball, sweep

V = Vector
HEIGHT = 1.5  # metres, top of the head

# ---- skeleton: key points, all in metres ----------------------------------------------

HEAD_Z0, HEAD_Z1 = 0.94, 1.50

# the head profile: (fraction of head height, (half width, half depth, exponent))
HEAD_PROFILE = [
    (0.00, (0.13, 0.115, 3.0)), (0.10, (0.225, 0.20, 3.0)), (0.30, (0.285, 0.255, 2.8)),
    (0.52, (0.30, 0.27, 2.6)), (0.76, (0.285, 0.265, 2.5)), (0.92, (0.22, 0.21, 2.4)),
    (1.00, (0.10, 0.10, 2.2)),
]


def head_at(f):
    """(half width, half depth, exponent) of the head at height fraction f."""
    return L._interp(HEAD_PROFILE, f)


def head_front_y(f, x=0.0):
    """Y of the face surface at height fraction f and sideways offset x."""
    rx, ry, e = head_at(f)
    t = min(abs(x) / rx, 0.999)
    return -ry * (1.0 - t ** e) ** (1.0 / e)


def head_z(f):
    return HEAD_Z0 + (HEAD_Z1 - HEAD_Z0) * f


class Pose:
    """Arm and leg joint positions (left side; the right side is mirrored in x)."""

    def __init__(self, elbow=(0.37, -0.05, 0.60), wrist=(0.33, -0.20, 0.64), stance=0.0,
                 elbow_r=None, wrist_r=None):
        self.elbow_l, self.wrist_l = V(elbow), V(wrist)
        self.elbow_r = V(elbow_r) if elbow_r else V((-elbow[0], elbow[1], elbow[2]))
        self.wrist_r = V(wrist_r) if wrist_r else V((-wrist[0], wrist[1], wrist[2]))
        self.stance = stance


TORSO_Z = [(0.34, (0.19, 0.14)), (0.42, (0.205, 0.155)), (0.52, (0.20, 0.155)), (0.62, (0.235, 0.17)),
           (0.72, (0.26, 0.17)), (0.80, (0.265, 0.155)), (0.85, (0.21, 0.125)), (0.89, (0.115, 0.105)),
           (0.97, (0.10, 0.10))]
TORSO_SPAN = 0.97 - 0.34


def torso_at(z, belly=1.0, width=1.0):
    """(half width, half depth) of the torso at height z."""
    keys = [(zz, (rx, ry)) for zz, (rx, ry) in TORSO_Z]
    rx, ry = L._interp([((zz - 0.34) / TORSO_SPAN, v) for zz, v in keys], (z - 0.34) / TORSO_SPAN)
    b = belly if 0.36 < z < 0.70 else 1.0
    return rx * width * b, ry * b


def torso(kid, name, material, grow=0.0, z0=0.34, z1=0.97, belly=1.0, width=1.0, subdiv=1,
          crease0=0.0, crease1=0.0, samples=16):
    """The torso (or a shell over it: grow > 0, between z0 and z1)."""
    prof = []
    for i in range(samples):
        t = i / (samples - 1)
        z = z0 + (z1 - z0) * t
        rx, ry = torso_at(z, belly, width)
        prof.append((t, (rx + grow, ry + grow, 2.8)))
    return sweep(kid, name, [V((0, 0, z0)), V((0, 0, z1))], prof, material, samples=samples, subdiv=subdiv,
                 crease_start=crease0, crease_end=crease1)


def leg_path(side, pose):
    s = 1 if side > 0 else -1
    sp = pose.stance
    return [V((s * 0.095, 0.0, 0.43)), V((s * (0.12 + sp * 0.3), -0.02, 0.25)), V((s * (0.125 + sp * 0.7), 0.0, 0.09))]


def arm_path(side, pose):
    s = 1 if side > 0 else -1
    shoulder = V((s * 0.235, 0.0, 0.76))
    return [shoulder, pose.elbow_l, pose.wrist_l] if s > 0 else [shoulder, pose.elbow_r, pose.wrist_r]


ARM_PROFILE = [(0.0, (0.09, 0.09)), (0.5, (0.08, 0.08)), (1.0, (0.074, 0.074))]
LEG_PROFILE = [(0.0, (0.105, 0.11)), (0.5, (0.092, 0.095)), (1.0, (0.075, 0.075))]


def body(kid, skin, pose, belly=1.0, width=1.0):
    """Skin: torso, head, arms, hands, legs, feet."""
    torso(kid, "torso", skin, belly=belly, width=width)
    sweep(kid, "head", [V((0, 0, HEAD_Z0)), V((0, 0, HEAD_Z1))], HEAD_PROFILE, skin, samples=22, ring=32, subdiv=1)
    for side in (1, -1):
        path = arm_path(side, pose)
        prof = [(t, (rx * width ** 0.5, ry)) for t, (rx, ry) in ARM_PROFILE]
        sweep(kid, "arm", path, prof, skin, e=2.7, samples=14, subdiv=1)
        hand(kid, path[-1], (path[-1] - path[-2]).normalized(), skin, side)
        lp = leg_path(side, pose)
        sweep(kid, "leg", lp, [(t, (rx * width ** 0.4, ry)) for t, (rx, ry) in LEG_PROFILE], skin, e=2.7, samples=14, subdiv=1)
        foot(kid, lp[-1], skin)
    for side in (1, -1):
        ball(kid, "ear", V((side * 0.318, 0.015, head_z(0.46))), (0.04, 0.035, 0.07), skin)


def hand(kid, wrist, d, skin, side):
    """A big chunky fist: a blocky mitten with a thumb."""
    start = wrist - d * 0.015
    sweep(kid, "fist", [start, start + d * 0.075, start + d * 0.14],
          [(0.0, (0.058, 0.055, 3.0)), (0.55, (0.076, 0.068, 3.0)), (1.0, (0.062, 0.055, 3.0))],
          skin, samples=8, subdiv=1)
    base = wrist + d * 0.05 + V((-side * 0.045, -0.04, 0.0))
    sweep(kid, "thumb", [base, base + d * 0.04 + V((0, -0.02, 0.0)), base + d * 0.075 + V((0, -0.025, 0.0))],
          [(0.0, (0.028, 0.028)), (1.0, (0.024, 0.024))], skin, samples=5, subdiv=1)


def foot(kid, ankle, skin):
    sweep(kid, "foot", [V((ankle.x, 0.02, 0.1)), V((ankle.x, -0.05, 0.06)), V((ankle.x, -0.16, 0.04))],
          [(0.0, (0.068, 0.066, 2.8)), (0.6, (0.074, 0.05, 2.8)), (1.0, (0.066, 0.034, 2.8))],
          skin, samples=8, subdiv=1, hint=(0, 0, 1))


# ---- face ---------------------------------------------------------------------------

def face(kid, skin, mouth_open=1.0, brow_tilt=1.0):
    """Big eyes, thick angled brows, a button nose and an open grin."""
    white = M("eye_white", "#fbf8f2", 0.7)
    iris = M("eye_iris", "#4a2b17", 0.7)
    pupil = M("eye_pupil", "#150d0a", 0.7)
    brow_m = M("brow", "#2b1810")
    mouth_m = M("mouth", "#5c1a1c")
    teeth_m = M("teeth", "#fbf8f2", 0.7)
    tongue_m = M("tongue", "#cf5f68")
    z_eye = head_z(0.50)
    for s in (1, -1):
        ex = s * 0.108
        fy = head_front_y(0.50, ex)
        ball(kid, "eye", V((ex, fy + 0.035, z_eye)), (0.068, 0.048, 0.08), white)
        ball(kid, "iris", V((ex - s * 0.006, fy + 0.002, z_eye - 0.004)), (0.045, 0.026, 0.056), iris)
        ball(kid, "pupil", V((ex - s * 0.006, fy - 0.009, z_eye - 0.006)), (0.03, 0.012, 0.037), pupil)
        ball(kid, "glint", V((ex - s * 0.022, fy - 0.018, z_eye + 0.022)), (0.012, 0.005, 0.013), teeth_m, subdiv=1)
        # brows slope down toward the nose: a determined, ready-to-play look
        by = head_front_y(0.62, 0.12) + 0.005
        inner = V((s * 0.04, by - 0.004, head_z(0.585) - 0.02 * (brow_tilt - 1.0)))
        outer = V((s * 0.19, by + 0.012, head_z(0.585) + 0.045 * brow_tilt))
        sweep(kid, "brow", [inner, (inner + outer) / 2 + V((0, -0.008, 0.008)), outer],
              [(0.0, (0.03, 0.017)), (0.5, (0.03, 0.017)), (1.0, (0.022, 0.013))],
              brow_m, e=2.6, samples=7, subdiv=1)
    ball(kid, "nose", V((0, head_front_y(0.36) + 0.005, head_z(0.37))), (0.042, 0.035, 0.036), skin)
    f_m = 0.215
    my = head_front_y(f_m)
    z_m = head_z(f_m)
    w = 0.22 + 0.1 * mouth_open
    h = 0.07 + 0.09 * mouth_open
    # flush with the face (no snout): thin blocks whose front sits on the surface
    block(kid, "mouth", V((0, my + 0.014, z_m)), (w, 0.034, h), mouth_m, e=2.1, rot=(math.pi / 2, 0, 0), crease=0.0)
    block(kid, "teeth", V((0, my + 0.0035, z_m + h * 0.34)), (w * 0.84, 0.012, h * 0.3), teeth_m, e=3.4, rot=(math.pi / 2, 0, 0), crease=0.0)
    block(kid, "tongue", V((0, my + 0.0065, z_m - h * 0.26)), (w * 0.52, 0.012, h * 0.3), tongue_m, e=2.4, rot=(math.pi / 2, 0, 0), crease=0.0)
    # smiling cheeks
    for s2 in (1, -1):
        ball(kid, "cheek", V((s2 * 0.2, head_front_y(0.3, 0.2) + 0.012, head_z(0.3))), (0.07, 0.04, 0.06), skin, subdiv=1)


# ---- clothing helpers ---------------------------------------------------------------

def sleeve(kid, pose, side, material, t1=0.42, grow=0.016, width=1.0, crease=0.8, trim=None):
    path = arm_path(side, pose)
    prof = [(t, (rx * width ** 0.5 + grow, ry + grow)) for t, (rx, ry) in ARM_PROFILE]
    obj = sweep(kid, "sleeve", path, prof, material, e=2.7, samples=10, subdiv=1, t0=0.0, t1=t1,
                crease_end=crease, caps=(False, False))
    if trim:
        sweep(kid, "sleeve_trim", path, [(t, (rx * width ** 0.5 + grow + 0.004, ry + grow + 0.004)) for t, (rx, ry) in ARM_PROFILE],
              trim, e=2.7, samples=4, subdiv=1, t0=t1 - 0.06, t1=t1, crease_start=0.5, crease_end=0.5, caps=(False, False))
    return obj


def shorts(kid, pose, material, to=0.55, grow=0.016, belly=1.0, width=1.0, trim=None, trim_t=0.07, belt=None):
    """A waist/seat shell and a leg shell on each leg, ending `to` of the way down."""
    torso(kid, "shorts_hips", material, grow=grow, z0=0.35, z1=0.56, belly=belly, width=width, crease1=0.6)
    for side in (1, -1):
        lp = leg_path(side, pose)
        prof = [(t, (rx * width ** 0.4 + grow, ry + grow)) for t, (rx, ry) in LEG_PROFILE]
        sweep(kid, "shorts_leg", lp, prof, material, e=2.7, samples=10, subdiv=1, t0=0.0, t1=to,
              crease_end=0.8, caps=(False, False))
        for k, (tm, off) in enumerate(trim or []):
            sweep(kid, "shorts_trim", lp, [(t, (rx * width ** 0.4 + grow + off, ry + grow + off)) for t, (rx, ry) in LEG_PROFILE],
                  tm, e=2.7, samples=4, subdiv=1, t0=to - trim_t * (k + 1) - 0.01, t1=to - trim_t * k,
                  crease_start=0.5, crease_end=0.5, caps=(False, False))
    if belt:
        torso(kid, "waistband", belt, grow=grow + 0.008, z0=0.5, z1=0.56, belly=belly, width=width, crease0=0.7, crease1=0.7)


def pants(kid, pose, material, grow=0.017):
    torso(kid, "pants_hips", material, grow=grow, z0=0.35, z1=0.56, crease1=0.6)
    for side in (1, -1):
        lp = leg_path(side, pose)
        prof = [(t, (rx + grow, ry + grow)) for t, (rx, ry) in LEG_PROFILE]
        sweep(kid, "pants_leg", lp, prof, material, e=2.7, samples=12, subdiv=1, crease_end=0.8, caps=(False, False))


def flipflops(kid, pose, color, strap_color=None):
    sole = M("flipflop_" + color, color)
    strap = M("flipflop_strap_" + (strap_color or color), strap_color or color)
    for side in (1, -1):
        x = leg_path(side, pose)[-1].x
        sweep(kid, "sole", [V((x, 0.11, 0.016)), V((x, -0.08, 0.016)), V((x, -0.24, 0.016))],
              [(0.0, (0.066, 0.018, 3.2)), (0.5, (0.088, 0.02, 3.2)), (1.0, (0.074, 0.018, 3.2))],
              sole, samples=9, subdiv=1, hint=(0, 0, 1), crease_start=0.9, crease_end=0.9)
        for s in (1, -1):
            sweep(kid, "strap", [V((x + s * 0.062, -0.01, 0.025)), V((x + s * 0.052, -0.06, 0.075)), V((x + s * 0.005, -0.15, 0.052))],
                  [(0.0, (0.014, 0.01)), (1.0, (0.012, 0.01))], strap, samples=6, subdiv=1)


def sneakers(kid, pose, color, sole_color):
    up = M("sneaker", color)
    sole = M("sneaker_sole", sole_color)
    for side in (1, -1):
        x = leg_path(side, pose)[-1].x
        sweep(kid, "shoe", [V((x, 0.03, 0.115)), V((x, -0.05, 0.085)), V((x, -0.17, 0.062))],
              [(0.0, (0.088, 0.088, 2.8)), (0.55, (0.092, 0.074, 2.8)), (1.0, (0.082, 0.05, 2.8))],
              up, samples=9, subdiv=1, hint=(0, 0, 1), crease_start=0.5)
        sweep(kid, "shoe_sole", [V((x, 0.1, 0.02)), V((x, -0.08, 0.02)), V((x, -0.21, 0.022))],
              [(0.0, (0.075, 0.022, 3.2)), (0.5, (0.098, 0.024, 3.2)), (1.0, (0.088, 0.022, 3.2))],
              sole, samples=9, subdiv=1, hint=(0, 0, 1), crease_start=0.8, crease_end=0.8)


def straps(kid, material, width=0.03, spread=0.11, top=0.80):
    """Two straps over the shoulders (sando, jersey, sundress): from the chest, over
    the top of the shoulder, down the back."""
    for s in (1, -1):
        rx, ry = torso_at(0.87)
        sweep(kid, "strap", [V((s * spread, -0.135, top - 0.03)), V((s * (spread + 0.01), -0.1, 0.865)),
                             V((s * (spread + 0.02), 0.0, 0.885)), V((s * (spread + 0.01), 0.105, 0.86)),
                             V((s * spread, 0.14, top - 0.04))],
              [(0.0, (width, 0.013)), (1.0, (width, 0.013))], material, samples=14, subdiv=1, hint=(0, 0, 1))


# ---- hair helpers --------------------------------------------------------------------

def hair_cap(kid, material, f0, grow=0.02, back=0.0, lift=0.0, subdiv=2, ring=32):
    """The hair mass: the top of the head from height fraction f0, slightly bigger."""
    z0 = head_z(f0)
    z1 = HEAD_Z1 + lift
    keys = [(0.0, (head_at(f0)[0] + grow, head_at(f0)[1] + grow, head_at(f0)[2]))]
    for f, (rx, ry, e) in HEAD_PROFILE:
        if f > f0:
            keys.append(((head_z(f) - z0) / (z1 - z0), (rx + grow, ry + grow, e)))
    return sweep(kid, "hair", [V((0, back, z0)), V((0, back, z1))], keys, material, samples=16, ring=ring, subdiv=subdiv)


def spike(kid, material, base, direction, length, radius):
    d = V(direction).normalized()
    sweep(kid, "spike", [V(base), V(base) + d * length * 0.5, V(base) + d * length],
          [(0.0, (radius, radius * 0.85)), (1.0, (0.008, 0.008))], material, e=2.4, samples=7, subdiv=1)


def lock(kid, material, pts, radii, subdiv=1, e=2.6, samples=14):
    """A flattened ribbon of hair following `pts`."""
    n = len(radii)
    sweep(kid, "lock", pts, [(i / (n - 1), r) for i, r in enumerate(radii)], material, e=e, samples=samples, subdiv=subdiv)


def bump(obj, scale=0.12, strength=-0.045, subdiv=3):
    return L.bumpy(obj, scale, strength, subdiv=subdiv)


def clip_flower(kid, centre, petal, middle, size=0.032):
    c = V(centre)
    for p in range(5):
        a = p * 2 * math.pi / 5
        ball(kid, "petal", c + V((math.cos(a) * size, -0.004, math.sin(a) * size)), (size * 0.8, size * 0.45, size * 0.8), petal, subdiv=1)
    ball(kid, "flower_mid", c + V((0, -0.016, 0)), (size * 0.5, size * 0.35, size * 0.5), middle, subdiv=1)


def loop_band(kid, material, centre, rx, ry, half_h, thick, tilt=0.0, start=0.2, end=2 * math.pi - 0.2, n=14):
    """A band running round the head (a headband)."""
    pts = [V((centre.x + math.cos(a) * rx, centre.y + math.sin(a) * ry, centre.z + math.sin(a) * tilt))
           for a in (start + (end - start) * i / (n - 1) for i in range(n))]
    sweep(kid, "band", pts, [(0.0, (thick, half_h)), (1.0, (thick, half_h))], material, e=3.0, samples=n * 2, subdiv=1, hint=(0, 0, 1))


def text(kid, body_text, location, size, material):
    bpy.ops.object.text_add(location=location, rotation=(math.pi / 2, 0, 0))
    t = bpy.context.active_object
    t.data.body = body_text
    t.data.size = size
    t.data.extrude = 0.008
    t.data.align_x = "CENTER"
    t.data.align_y = "CENTER"
    bpy.ops.object.convert(target="MESH")
    return kid.adopt(bpy.context.active_object, material, subdiv=0)


# ---- the six kids -------------------------------------------------------------------

def junjun():
    """White sando, red basketball shorts (#23, white trim), spiky brown hair, blue tsinelas."""
    kid = L.Kid("junjun")
    pose = Pose(elbow=(0.40, -0.04, 0.60), wrist=(0.40, -0.19, 0.70), elbow_r=(-0.36, -0.10, 0.62), wrist_r=(-0.34, -0.30, 0.74))
    skin = M("skin_junjun", "#c98a5e")
    body(kid, skin, pose)
    face(kid, skin, brow_tilt=1.3, mouth_open=1.15)
    white = M("sando_white", "#f3f0e8")
    torso(kid, "sando", white, grow=0.016, z0=0.50, z1=0.80, crease0=0.8, crease1=0.9)
    straps(kid, white)
    red, trim = M("shorts_red", "#d63a35"), M("trim_white", "#f3f0e8")
    shorts(kid, pose, red, to=0.66, trim=[(trim, 0.004)], trim_t=0.07, belt=trim)
    for s in (1, -1):
        lp = leg_path(s, pose)
        off = [0.122, 0.108, 0.094]
        side = [V((p.x + s * o, p.y, p.z)) for p, o in zip(lp, off)]
        sweep(kid, "side_stripe", side, [(0.0, (0.03, 0.011)), (1.0, (0.028, 0.011))], trim, samples=10, subdiv=1,
              hint=(1, 0, 0), t0=0.28, t1=0.6)
    text(kid, "23", V((-0.12, -0.132, 0.36)), 0.1, trim)
    hair = M("hair_junjun", "#5c3320")
    hair_cap(kid, hair, 0.5, grow=0.022, back=0.01)
    rng = random.Random(3)
    for _ in range(22):
        a = rng.uniform(0, 2 * math.pi)
        up = rng.uniform(0.35, 0.95)
        d = V((math.cos(a) * (1 - up) * 0.9, math.sin(a) * (1 - up) * 0.8 + 0.12, up)).normalized()
        spike(kid, hair, V((d.x * 0.285, d.y * 0.26, 1.21 + d.z * 0.29)), d, rng.uniform(0.15, 0.24), rng.uniform(0.045, 0.06))
    for x, z in ((-0.17, 0.0), (-0.06, 0.02), (0.05, 0.015), (0.16, -0.005)):
        spike(kid, hair, V((x, -0.255, head_z(0.84) + z)), V((x * 1.4, -0.6, 0.55)), 0.15, 0.045)
    flipflops(kid, pose, "#2d4fc9")
    return kid


def popoy():
    """Chubby: blue-and-white striped polo with a collar, blue jersey shorts with white and red trim, short curls."""
    kid = L.Kid("popoy")
    pose = Pose(elbow=(0.43, -0.03, 0.82), wrist=(0.41, -0.06, 1.05), elbow_r=(-0.40, -0.06, 0.56), wrist_r=(-0.34, -0.20, 0.55), stance=0.02)
    skin = M("skin_popoy", "#d6a07b")
    belly, width = 1.22, 1.18
    body(kid, skin, pose, belly=belly, width=width)
    face(kid, skin, mouth_open=1.1, brow_tilt=1.2)
    white, blue = M("polo_white", "#f6f4ef"), M("polo_blue", "#2f63c8")
    top = torso(kid, "polo", white, grow=0.018, z0=0.50, z1=0.88, belly=belly, width=width, crease0=0.8)
    sleeves = [sleeve(kid, pose, s, white, t1=0.45, grow=0.02, width=width) for s in (1, -1)]
    for obj in [top] + sleeves:
        L.color_faces(obj, lambda p: blue if math.sin(p.z * 44.0) > 0.0 else white, [white, blue])
    collar = M("collar", "#f6f4ef")
    for s in (1, -1):
        block(kid, "collar", V((s * 0.075, -0.16, 0.855)), (0.14, 0.03, 0.075), collar, rot=(math.radians(-25), 0, s * 0.45), crease=0.6)
    shorts(kid, pose, M("shorts_blue", "#2c4bb0"), to=0.62, belly=belly, width=width,
           trim=[(M("trim_white", "#f6f4ef"), 0.004), (M("trim_red", "#d23b3b"), 0.004)], trim_t=0.06)
    hair = M("hair_popoy", "#2b1a12")
    cap = hair_cap(kid, hair, 0.5, grow=0.032, back=0.01, subdiv=3)
    bump(cap, scale=0.075, strength=-0.04)
    flipflops(kid, pose, "#2d4fc9")
    return kid


def ligaya():
    """Floral sundress, long wavy brown hair with a pink flower clip, pink tsinelas."""
    kid = L.Kid("ligaya")
    pose = Pose(elbow=(0.37, -0.05, 0.62), wrist=(0.30, -0.19, 0.68), elbow_r=(-0.39, -0.02, 0.80), wrist_r=(-0.37, -0.05, 1.02), stance=0.015)
    skin = M("skin_ligaya", "#d8a079")
    body(kid, skin, pose, width=0.96)
    face(kid, skin, mouth_open=1.2, brow_tilt=1.1)
    cream = M("dress_cream", "#fcf4ea")
    torso(kid, "bodice", cream, grow=0.016, z0=0.56, z1=0.775, width=0.96, crease1=0.9)
    sweep(kid, "skirt", [V((0, 0, 0.58)), V((0, 0, 0.30))], [(0.0, (0.215, 0.17)), (0.45, (0.30, 0.25)), (1.0, (0.355, 0.30))],
          cream, e=2.4, samples=12, subdiv=1, crease_end=0.9, caps=(True, False))
    straps(kid, cream, width=0.028, spread=0.115)
    rng = random.Random(8)
    petals = [M("flower_pink", "#f06a98"), M("flower_red", "#e0453f"), M("flower_peach", "#f7a35c")]
    mid = M("flower_mid", "#f7d24a")
    for _ in range(54):
        if rng.random() < 0.3:
            z, rx, ry = rng.uniform(0.62, 0.76), 0.235, 0.18
        else:
            z = rng.uniform(0.31, 0.56)
            f = (0.58 - z) / 0.28
            rx, ry = 0.215 + 0.14 * f, 0.17 + 0.13 * f
        a = rng.uniform(math.pi * 1.0, math.pi * 2.0)
        c = V((math.cos(a) * rx, math.sin(a) * ry, z))
        n = V((math.cos(a), math.sin(a) * 0.9, 0)).normalized()
        tang = V((-n.y, n.x, 0))
        col = rng.choice(petals)
        for p in range(5):
            ang = p * 2 * math.pi / 5
            ball(kid, "petal", c + tang * math.cos(ang) * 0.024 + V((0, 0, math.sin(ang) * 0.024)) + n * 0.005, (0.017, 0.017, 0.017), col, subdiv=1)
        ball(kid, "pmid", c + n * 0.012, (0.011, 0.011, 0.011), mid, subdiv=1)
    hair = M("hair_ligaya", "#7a4a2a")
    hair_cap(kid, hair, 0.52, grow=0.026, back=0.015, subdiv=2)
    lock(kid, hair, [V((0, 0.19, 1.40)), V((0, 0.285, 1.14)), V((0.02, 0.29, 0.92)), V((-0.02, 0.27, 0.74))],
         [(0.29, 0.07), (0.33, 0.08), (0.30, 0.07), (0.22, 0.05)], samples=16)
    for s in (1, -1):
        lock(kid, hair, [V((s * 0.21, 0.02, 1.40)), V((s * 0.325, 0.06, 1.18)), V((s * 0.34, 0.1, 0.98)), V((s * 0.3, 0.12, 0.8)), V((s * 0.33, 0.12, 0.7))],
             [(0.09, 0.09), (0.095, 0.09), (0.085, 0.075), (0.075, 0.07), (0.03, 0.03)], samples=18)
    for x, z, r in ((-0.17, 0.0, 0.07), (-0.02, 0.012, 0.075), (0.14, 0.0, 0.07)):
        lock(kid, hair, [V((x * 1.3, -0.205, head_z(0.9) + z)), V((x * 1.1, -0.262, head_z(0.78) + z)), V((x * 1.25, -0.275, head_z(0.62) + z))],
             [(r, 0.04), (r * 1.1, 0.045), (r * 0.7, 0.03)], samples=8)
    clip_flower(kid, V((0.205, -0.17, head_z(0.83))), M("clip_pink", "#f38bc6"), M("clip_mid", "#f7d24a"), size=0.036)
    flipflops(kid, pose, "#ef6aa3")
    return kid


def migo():
    """Pastel tie-dye tee knotted at the side with a smiley, denim shorts with cuffs, purple belt bag with a crown, big curls, pastel headband."""
    kid = L.Kid("migo")
    pose = Pose(elbow=(0.38, -0.05, 0.60), wrist=(0.34, -0.2, 0.62), elbow_r=(-0.36, -0.06, 0.58), wrist_r=(-0.36, -0.22, 0.62), stance=0.015)
    skin = M("skin_migo", "#c58a60")
    body(kid, skin, pose)
    face(kid, skin)
    pastels = [M("tie_pink", "#f6b6d8"), M("tie_violet", "#c9b6f2"), M("tie_blue", "#a9d8f2"), M("tie_cream", "#f9e8bd")]
    top = torso(kid, "tee", pastels[0], grow=0.017, z0=0.58, z1=0.87, crease0=0.9)
    sleeves = [sleeve(kid, pose, s, pastels[0], t1=0.4, grow=0.022) for s in (1, -1)]

    def dye(p):
        v = math.sin(p.x * 19 + p.z * 11) + math.sin(p.z * 15 - p.x * 9) * 0.9 + math.sin(p.x * 7 + p.y * 13)
        return pastels[int((v + 3) * 0.7) % 4]

    for obj in [top] + sleeves:
        L.color_faces(obj, dye, pastels)
    ball(kid, "knot", V((0.2, -0.12, 0.6)), (0.05, 0.045, 0.045), M("knot", "#e3b6ef"), subdiv=1)
    yellow, ink = M("smiley", "#f7d24a"), M("smiley_ink", "#3a2a1a")
    block(kid, "smiley", V((-0.05, -0.178, 0.72)), (0.1, 0.02, 0.1), yellow, e=2.0, rot=(math.pi / 2, 0, 0), crease=0.0)
    for s in (1, -1):
        ball(kid, "smiley_eye", V((-0.05 + s * 0.02, -0.19, 0.735)), (0.008, 0.005, 0.012), ink, subdiv=1)
    sweep(kid, "smile", [V((-0.077, -0.192, 0.7)), V((-0.05, -0.194, 0.686)), V((-0.023, -0.192, 0.7))],
          [(0.0, (0.006, 0.006)), (1.0, (0.006, 0.006))], ink, samples=6, subdiv=1)
    shorts(kid, pose, M("denim", "#7ea3d8"), to=0.42, trim=[(M("denim_cuff", "#a8c4ea"), 0.01)], trim_t=0.08, belt=M("belt", "#3b2b2b"))
    purple, gold = M("belt_bag", "#9a5fd6"), M("crown_gold", "#f2c23c")
    block(kid, "bag", V((0.09, -0.19, 0.5)), (0.17, 0.08, 0.11), purple, e=3.0, rot=(math.radians(-8), 0, 0), crease=0.6)
    block(kid, "bag_zip", V((0.09, -0.228, 0.52)), (0.15, 0.012, 0.012), gold, e=3.0, crease=0.0)
    for c in (-0.035, 0.0, 0.035):
        sweep(kid, "crown", [V((0.09 + c, -0.236, 0.465)), V((0.09 + c, -0.238, 0.495))],
              [(0.0, (0.012, 0.006)), (1.0, (0.002, 0.004))], gold, samples=3, subdiv=0)
    hair = M("hair_migo", "#3a2216")
    cap = hair_cap(kid, hair, 0.36, grow=0.075, back=0.02, lift=0.1, subdiv=3)
    bump(cap, scale=0.1, strength=-0.06)
    loop_band(kid, pastels[1], V((0, 0.0, head_z(0.72))), 0.345, 0.325, 0.04, 0.03, tilt=0.03)
    flipflops(kid, pose, "#2d4fc9")
    return kid


def toni():
    """Backwards red cap, black basketball jersey #11, olive cargo shorts, dark sandals."""
    kid = L.Kid("toni")
    pose = Pose(elbow=(0.36, -0.12, 0.52), wrist=(0.24, -0.22, 0.44), elbow_r=(-0.36, -0.12, 0.52), wrist_r=(-0.24, -0.22, 0.44), stance=0.04)
    skin = M("skin_toni", "#bf8359")
    body(kid, skin, pose, width=1.04)
    face(kid, skin, mouth_open=0.8, brow_tilt=1.5)
    black, white = M("jersey_black", "#1f1f24"), M("jersey_number", "#f4f1ea")
    torso(kid, "jersey", black, grow=0.017, z0=0.48, z1=0.775, width=1.04, crease0=0.8, crease1=0.9)
    straps(kid, black, width=0.04, spread=0.12)
    text(kid, "11", V((0.0, -0.186, 0.64)), 0.15, white)
    shorts(kid, pose, M("cargo", "#6f7148"), to=0.7, width=1.04, trim=[(M("cargo_hem", "#5e6039"), 0.003)], trim_t=0.06, grow=0.02)
    pocket = M("cargo_pocket", "#5e6039")
    for s in (1, -1):
        lp = leg_path(s, pose)
        block(kid, "pocket", V((lp[1].x + s * 0.01, -0.12, 0.3)), (0.16, 0.05, 0.14), pocket, e=3.0, crease=0.5)
    hair = M("hair_toni", "#3a2216")
    hair_cap(kid, hair, 0.46, grow=0.026, back=0.01, subdiv=2)
    cap_m = M("cap_red", "#d6332f")
    sweep(kid, "cap", [V((0, 0.015, head_z(0.66))), V((0, 0.015, HEAD_Z1 + 0.055))],
          [(0.0, (0.335, 0.31, 2.5)), (0.5, (0.33, 0.31, 2.3)), (0.9, (0.24, 0.24, 2.2)), (1.0, (0.05, 0.05, 2.0))],
          cap_m, samples=12, ring=32, subdiv=2, crease_start=0.9)
    sweep(kid, "peak", [V((0, 0.26, head_z(0.76))), V((0, 0.37, head_z(0.74))), V((0, 0.47, head_z(0.70)))],
          [(0.0, (0.16, 0.022, 3.0)), (0.5, (0.18, 0.02, 3.0)), (1.0, (0.14, 0.017, 3.0))], cap_m, samples=8, subdiv=1,
          hint=(0, 0, 1), crease_start=0.5, crease_end=0.8)
    for x in (-0.16, -0.05, 0.07, 0.17):
        lock(kid, hair, [V((x * 1.2, -0.215, head_z(0.7))), V((x * 1.2, -0.27, head_z(0.6)))], [(0.05, 0.03), (0.04, 0.03)], samples=4)
    flipflops(kid, pose, "#3b3b40")
    return kid


def inday():
    """White PE shirt with green trim and an emblem, green track pants with white stripes, curly pigtails with pink ties, white sneakers."""
    kid = L.Kid("inday")
    pose = Pose(elbow=(0.37, -0.05, 0.60), wrist=(0.32, -0.20, 0.64), elbow_r=(-0.40, -0.02, 0.80), wrist_r=(-0.38, -0.05, 1.02), stance=0.02)
    skin = M("skin_inday", "#a8693f")
    body(kid, skin, pose)
    face(kid, skin, mouth_open=1.15, brow_tilt=1.2)
    white, green = M("pe_white", "#f6f4ef"), M("pe_green", "#1f8a57")
    torso(kid, "pe_shirt", white, grow=0.017, z0=0.50, z1=0.875, crease0=0.8)
    for s in (1, -1):
        sleeve(kid, pose, s, white, t1=0.42, grow=0.02, trim=green)
    torso(kid, "pe_collar", green, grow=0.024, z0=0.835, z1=0.875, crease0=0.5, crease1=0.5)
    block(kid, "emblem", V((-0.1, -0.186, 0.72)), (0.075, 0.016, 0.075), green, e=2.0, rot=(math.pi / 2, 0, 0), crease=0.0)
    block(kid, "emblem_in", V((-0.1, -0.196, 0.72)), (0.045, 0.012, 0.045), white, e=2.0, rot=(math.pi / 2, 0, 0), crease=0.0)
    pants(kid, pose, M("track_green", "#1c8a5a"))
    for s in (1, -1):
        lp = leg_path(s, pose)
        for a, b in ((lp[0], lp[1]), (lp[1], lp[2])):
            sweep(kid, "stripe", [a + V((s * 0.118, 0, 0)), b + V((s * 0.104, 0, 0))],
                  [(0.0, (0.028, 0.011)), (1.0, (0.026, 0.011))], white, samples=6, subdiv=1, hint=(1, 0, 0))
    hair = M("hair_inday", "#2a1810")
    cap = hair_cap(kid, hair, 0.5, grow=0.04, back=0.015, subdiv=3)
    bump(cap, scale=0.07, strength=-0.035)
    pink, mid = M("tie_pink2", "#f38bc6"), M("tie_mid", "#f7d24a")
    for s in (1, -1):
        puff = ball(kid, "pigtail", V((s * 0.36, 0.06, head_z(0.78))), (0.15, 0.15, 0.15), hair, subdiv=3)
        bump(puff, scale=0.07, strength=-0.04, subdiv=3)
        ball(kid, "tie", V((s * 0.25, 0.0, head_z(0.84))), (0.05, 0.05, 0.05), pink, subdiv=1)
        clip_flower(kid, V((s * 0.27, -0.06, head_z(0.84))), pink, mid, size=0.028)
    sneakers(kid, pose, "#f6f4ef", "#dcd9d2")
    return kid


BUILDERS = {"junjun": junjun, "popoy": popoy, "ligaya": ligaya, "migo": migo, "toni": toni, "inday": inday}
ORDER = ["junjun", "popoy", "ligaya", "migo", "toni", "inday"]


def build(name):
    """Builds one kid (all parts joined into one mesh named after them), at the origin."""
    return L.finish(BUILDERS[name]())


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    L._MATS.clear()


def stage(background="#d9b58a"):
    scene = bpy.context.scene
    world = bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (*L.srgb(background), 1.0)
    bg.inputs["Strength"].default_value = 0.6
    bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, -0.001))
    bpy.context.active_object.data.materials.append(M("ground", "#c4a07a", 0.95))
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 3.0
    sun.data.color = L.srgb("#ffe4bd")
    sun.data.angle = math.radians(10)
    sun.rotation_euler = (math.radians(52), 0, math.radians(-32))
    scene.collection.objects.link(sun)
    fill = bpy.data.objects.new("Fill", bpy.data.lights.new("Fill", "AREA"))
    fill.data.energy = 700
    fill.data.size = 6
    fill.data.color = L.srgb("#d9e4ff")
    fill.location = (3.5, -6.0, 4.0)
    fill.rotation_euler = (math.radians(55), 0, math.radians(30))
    scene.collection.objects.link(fill)
    return scene


def shoot(scene, path, cam_loc, look, lens, res, samples=24, ortho=None):
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    scene.collection.objects.link(cam)
    cam.location = cam_loc
    cam.rotation_euler = (V(look) - V(cam_loc)).to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = lens
    if ortho:
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = ortho
    scene.camera = cam
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.filepath = path
    scene.view_settings.view_transform = "AgX"
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["."]
    out_dir = argv[0]
    mode = argv[1] if len(argv) > 1 else "lineup"
    names = argv[2:] or ORDER
    reset()
    objs = []
    for i, name in enumerate(names):
        obj = build(name)
        obj.location.x = (i - (len(names) - 1) / 2) * 1.0
        objs.append(obj)
    if mode in ("lineup", "both"):
        scene = stage()
        if len(names) == 1:
            for k, ang in enumerate((0, 35, 90, 180)):
                a = math.radians(ang)
                shoot(scene, f"{out_dir}/{names[0]}_{k}.png", (math.sin(a) * 4.2, -math.cos(a) * 4.2, 1.0), (0, 0, 0.78), 50, (700, 900), samples=20)
        else:
            shoot(scene, f"{out_dir}/kids_lineup.png", (0, -2.0 - 1.6 * len(names), 1.5), (0, 0, 0.78), 42, (2000, 820))


if __name__ == "__main__":
    main()
