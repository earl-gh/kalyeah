# apex-rig-forge R1-R3 on one normalized kid:
#   - enforces unit scale/rotation, soles at z=0, centered x=0, facing -Y (glTF +Z)
#   - strips path-breaking bones (Armature -> mixamorig:Hips exact R2 hierarchy)
#   - rebinds to the roster reference A-pose and copies exact reference rest orientations (<= 0.1 deg)
#   - adds R2 twist (ArmTwist, ForeArmTwist1/2) and half-angle helper (ForeArmHelper, LegHelper, HandHelper) bones
#   - runs full R3 skinning passes: skirt/cloth thigh binding (zero skirt tearing), rigid limb cores,
#     outer-biased joint blend bands, twist ramps, half-angle helpers, localized joint smoothing,
#     1.5x-radius stray influence pruning, seam-twin parity, top-4 cap, normalize
#   - exports contracted .glb and gltf_yup <kid>_rig.json (capsules, girth offsets, leg ratio, springChains, etc.)
# usage:
#   blender --background --python contract_kid.py -- norm.glb ref_rest.json out.glb out_rig.json [kid_id]
#   python contract_kid.py norm.glb ref_rest.json out.glb out_rig.json [kid_id]
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
SRC, REF, DST, RIGJ = argv[0:4]
KID_ID = (argv[4] if len(argv) > 4 else os.path.splitext(os.path.basename(SRC))[0]).lower()
P = "mixamorig:"
SKIP = {s.strip() for s in os.environ.get("SKIP", "").split(",") if s.strip()}
ref = json.load(open(REF, encoding="utf-8"))

HINGES = [
    ("ForeArm", "Arm", 1.0, (0.0, 1.0, 0.0)),   # elbow outer side is +Y (back)
    ("Leg", "UpLeg", 1.0, (0.0, -1.0, 0.0)),    # knee outer side is -Y (front kneecap)
    ("Hand", "ForeArm", 0.7, (0.0, 0.0, 0.0)),  # wrist
    ("Foot", "Leg", 0.7, (0.0, 0.0, 0.0)),      # ankle
]
SEGMENTS = ["Arm", "ForeArm", "UpLeg", "Leg"]


def to_gltf(v):
    """Blender (x=left, y=back, z=up) -> glTF right-handed Y-up (x=left, y=up, z=forward)."""
    return [round(float(v[0]), 5), round(float(v[2]), 5), round(float(-v[1]), 5)]


def smoothstep(x, e0, e1):
    t = float(np.clip((x - e0) / max(e1 - e0, 1e-8), 0.0, 1.0))
    return t * t * (3.0 - 2.0 * t)


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
for a in list(bpy.data.actions):
    bpy.data.actions.remove(a)
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
if arm.animation_data:
    arm.animation_data_clear()
arm.data.pose_position = "REST"
mesh = max((o for o in bpy.data.objects if o.type == "MESH" and o.vertex_groups), key=lambda o: len(o.data.vertices))
for o in [o for o in bpy.data.objects if o.type == "MESH" and o != mesh]:
    bpy.data.objects.remove(o, do_unlink=True)
bpy.context.view_layer.update()
log = []


def set_mode(obj, mode):
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    if mode != "OBJECT":
        bpy.ops.object.mode_set(mode=mode)


# ---- 1. Apply object scale/rotation and snap soles to z = 0 --------------------------------------------------------
set_mode(arm, "OBJECT")
if any(abs(s - 1.0) > 1e-4 for s in arm.matrix_world.to_scale()) or any(abs(s - 1.0) > 1e-4 for s in mesh.matrix_world.to_scale()):
    for o in bpy.context.view_layer.objects:
        o.select_set(o in (arm, mesh))
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    log.append("applied object scale/rotation")

# Ground soles to z = 0 and center horizontally on Hips.x.
bpy.context.view_layer.update()
min_z = min((mesh.matrix_world @ v.co).z for v in mesh.data.vertices)
hips_b = arm.data.bones.get(P + "Hips")
hips_x = (arm.matrix_world @ hips_b.head_local).x if hips_b else 0.0
if abs(min_z) > 1e-4 or abs(hips_x) > 0.02:
    shift = Vector((-hips_x if abs(hips_x) > 0.02 else 0.0, 0.0, -min_z))
    arm.location += shift
    if mesh.parent != arm:
        mesh.location += shift
    bpy.context.view_layer.update()
    for o in bpy.context.view_layer.objects:
        o.select_set(o in (arm, mesh))
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.context.view_layer.update()
    log.append(f"grounded soles (dz={-min_z:+.4f}m)")

# ---- 2. Strip bones that break the R2 contract paths ---------------------------------------------------------------
def is_contract(n):
    return n.startswith(P) and "WristExtra" not in n


bones = arm.data.bones
breakers = [
    b.name for b in bones
    if not is_contract(b.name)
    and ("WristExtra" in b.name or any(is_contract(c.name) for c in b.children_recursive))
]
target = {}
for n in breakers:
    x = bones[n].parent
    while x is not None and not is_contract(x.name):
        x = x.parent
    target[n] = x.name if x is not None else P + "Hips"

vgs = mesh.vertex_groups
for n, t in target.items():
    src = vgs.get(n)
    if src is None:
        continue
    dst = vgs.get(t) or vgs.new(name=t)
    for v in mesh.data.vertices:
        for g in v.groups:
            if g.group == src.index and g.weight > 0:
                dst.add([v.index], g.weight, "ADD")
    vgs.remove(src)

set_mode(arm, "EDIT")
eb = arm.data.edit_bones
for n in breakers:
    b = eb[n]
    for c in list(b.children):
        c.use_connect = False
        c.parent = b.parent if (b.parent is None or b.parent.name not in breakers) else None
    eb.remove(b)

for b in eb:
    if b.name == P + "Hips":
        b.parent = None
        continue
    if b.name.startswith(P):
        rp = ref.get(b.name[len(P):], {}).get("parent")
        if rp and P + rp in eb and (b.parent is None or b.parent.name != P + rp):
            b.use_connect = False
            b.parent = eb[P + rp]

set_mode(arm, "OBJECT")
log.append(f"removed {len(breakers)} path-breaking bones")

# ---- 3. Rebind to reference rest A-pose & 4. Copy exact reference rest frames --------------------------------------
AM = arm.matrix_world.copy()
AMi = AM.inverted()
pb = arm.pose.bones
FINGER = ("Thumb", "Index", "Middle", "Ring", "Pinky")
set_mode(arm, "POSE")
HAND_TWIST_MAX = 30.0


def whead(n):
    bpy.context.view_layer.update()
    return AM @ pb[P + n].head


def signed_angle(a, b, axis):
    a = (a - a.dot(axis) * axis).normalized()
    b = (b - b.dot(axis) * axis).normalized()
    return math.atan2(a.cross(b).dot(axis), a.dot(b))


def turn(n, rot3):
    p = pb[P + n]
    bpy.context.view_layer.update()
    pivot = AM @ p.head
    p.matrix = AMi @ (Matrix.Translation(pivot) @ rot3.to_4x4() @ Matrix.Translation(-pivot) @ AM @ p.matrix)
    bpy.context.view_layer.update()


rhead = {k: Vector(v["head"]) for k, v in ref.items()}
for s_ in ("Left", "Right"):
    if not all(P + s_ + n in pb for n in ("Arm", "ForeArm", "Hand", "HandMiddle1", "HandIndex1", "HandPinky1")):
        continue
    a0 = (whead(s_ + "ForeArm") - whead(s_ + "Arm")).normalized()
    f0 = (whead(s_ + "Hand") - whead(s_ + "ForeArm")).normalized()
    prebend = math.degrees(a0.angle(f0))
    n0 = a0.cross(f0)
    R = ref[s_ + "Arm"]["rot"]
    rr = Vector((R[0][1], R[1][1], R[2][1])).normalized()
    horiz = Vector((a0.x, a0.y, 0.0)).normalized()
    elev = math.asin(max(-1.0, min(1.0, rr.z)))
    r = (horiz * math.cos(elev) + Vector((0, 0, 1)) * math.sin(elev)).normalized()
    q = a0.rotation_difference(r)
    turn(s_ + "Arm", q.to_matrix())
    etw = 0.0
    if prebend > 4.0:
        etw = signed_angle(q @ n0, r.cross(Vector((0, -1, 0))), r)
        turn(s_ + "Arm", Matrix.Rotation(etw, 3, r))
    f1 = (whead(s_ + "Hand") - whead(s_ + "ForeArm")).normalized()
    turn(s_ + "ForeArm", f1.rotation_difference(r).to_matrix())
    h1 = (whead(s_ + "HandMiddle1") - whead(s_ + "Hand")).normalized()
    turn(s_ + "Hand", h1.rotation_difference(r).to_matrix())
    ak = whead(s_ + "HandIndex1") - whead(s_ + "HandPinky1")
    hr = (rhead[s_ + "HandMiddle1"] - rhead[s_ + "Hand"]).normalized()
    ar = r.rotation_difference(hr).inverted() @ (rhead[s_ + "HandIndex1"] - rhead[s_ + "HandPinky1"])
    ptw = signed_angle(ak, ar, r)
    lim = math.radians(HAND_TWIST_MAX)
    turn(s_ + "Hand", Matrix.Rotation(max(-lim, min(lim, ptw)), 3, r))
    log.append(
        f"{s_}: elbow pre-bend {prebend:.0f} straightened, arm rolled {math.degrees(etw):+.0f}, "
        f"palm roll {math.degrees(ptw):+.0f}"
    )

bpy.context.view_layer.update()
set_mode(mesh, "OBJECT")
mod = next(m for m in mesh.modifiers if m.type == "ARMATURE")
bpy.ops.object.modifier_apply(modifier=mod.name)
set_mode(arm, "POSE")
bpy.ops.pose.armature_apply(selected=False)

# Align rest frames to exact reference rest orientations (<= 0.1 deg mismatch across all 12 kids).
set_mode(arm, "EDIT")
eb = arm.data.edit_bones
for b in eb:
    key = b.name[len(P):] if b.name.startswith(P) else None
    if key not in ref or any(f in key for f in FINGER):
        continue
    R = ref[key]["rot"]
    y = AMi.to_3x3() @ Vector((R[0][1], R[1][1], R[2][1]))
    z = AMi.to_3x3() @ Vector((R[0][2], R[1][2], R[2][2]))
    L = max((b.tail - b.head).length, 1e-4)
    for c in b.children:
        c.use_connect = False
    b.tail = b.head + y.normalized() * L
    b.align_roll(z)

# Ensure _End and Tip markers stay non-deforming.
for b in eb:
    if b.name.startswith(P) and (b.name.endswith("_End") or b.name.endswith("Tip")):
        b.use_deform = False

# ---- 5. Add R2 twist and half-angle helper bones -------------------------------------------------------------------
def add_bone(name, parent, head_pt, tail_pt, roll_like):
    if name in eb:
        nb = eb[name]
    else:
        nb = eb.new(name)
    nb.head = head_pt.copy()
    nb.tail = tail_pt.copy()
    nb.parent = eb[parent]
    nb.use_connect = False
    nb.roll = eb[roll_like].roll
    nb.use_deform = True
    return nb


added = []
for s in ("Left", "Right"):
    A_, F_, H_, U_, L_ = (P + s + x for x in ("Arm", "ForeArm", "Hand", "UpLeg", "Leg"))
    if not all(n in eb for n in (A_, F_, H_, U_, L_)):
        continue
    a, f, h, u, l = eb[A_], eb[F_], eb[H_], eb[U_], eb[L_]
    # ArmTwist covers proximal upper arm (shoulder -> mid-bicep) and counter-twists -50% relative to Arm -> +50% world twist.
    add_bone(P + s + "ArmTwist", A_, a.head, a.head.lerp(a.tail, 0.5), A_)
    add_bone(P + s + "ForeArmTwist1", F_, f.head.lerp(f.tail, 1.0 / 3.0), f.head.lerp(f.tail, 2.0 / 3.0), F_)
    add_bone(P + s + "ForeArmTwist2", F_, f.head.lerp(f.tail, 2.0 / 3.0), f.tail, F_)
    add_bone(P + s + "ForeArmHelper", A_, f.head, f.head + (f.tail - f.head) * 0.25, F_)
    add_bone(P + s + "LegHelper", U_, l.head, l.head + (l.tail - l.head) * 0.25, L_)
    add_bone(P + s + "HandHelper", F_, h.head, h.head + (h.tail - h.head) * 0.5, H_)
    added += [s + x for x in ("ArmTwist", "ForeArmTwist1", "ForeArmTwist2", "ForeArmHelper", "LegHelper", "HandHelper")]

set_mode(arm, "OBJECT")
mod = mesh.modifiers.new("Armature", "ARMATURE")
mod.object = arm
log.append(f"rebound to reference rest; added {len(added)} twist/helper bones")

# ---- 6. Full R3 skinning passes ------------------------------------------------------------------------------------
AM = arm.matrix_world.copy()
B = arm.data.bones
co = np.array([tuple(mesh.matrix_world @ v.co) for v in mesh.data.vertices])
nv = len(co)
vgs = mesh.vertex_groups
gid = {g.name: g.index for g in vgs}
for n in added:
    if P + n not in gid:
        gid[P + n] = vgs.new(name=P + n).index
# Ensure every contract deforming bone has a group id ready
for s in ("Left", "Right"):
    for n in ("UpLeg", "Leg", "Arm", "ForeArm", "Hand", "Foot"):
        if P + s + n in B and P + s + n not in gid:
            gid[P + s + n] = vgs.new(name=P + s + n).index
if P + "Hips" in B and P + "Hips" not in gid:
    gid[P + "Hips"] = vgs.new(name=P + "Hips").index

gname = {i: n for n, i in gid.items()}
W = [{g.group: g.weight for g in v.groups if g.weight > 0} for v in mesh.data.vertices]


def seg(n):
    b = B[P + n]
    return np.array(AM @ b.head_local), np.array(AM @ b.tail_local)


def param(x, a, b):
    ab = b - a
    L2 = max(float(ab @ ab), 1e-12)
    t = ((x - a) @ ab) / L2
    if np.ndim(t):
        proj = a + np.clip(t, 0.0, 1.0)[..., None] * ab
        return t, np.linalg.norm(x - proj, axis=-1)
    proj = a + float(np.clip(t, 0.0, 1.0)) * ab
    return float(t), float(np.linalg.norm(x - proj))


def dominant(i):
    return max(W[i], key=W[i].get) if W[i] else None


extra_g = {gid[n] for n in gid if not n.startswith(P)}
skirt_g = {gid[n] for n in gid if n.startswith("Skirt_")}
cape_g = {gid[n] for n in gid if n.startswith("Cape_")}
hair_g = {gid[n] for n in gid if n.startswith("Hair_")}
hair_cape_g = extra_g - skirt_g
hips_g = gid.get(P + "Hips")
spine2_g = gid.get(P + "Spine2")
head_g = gid.get(P + "Head")
l_upleg_g = gid.get(P + "LeftUpLeg")
r_upleg_g = gid.get(P + "RightUpLeg")
l_leg_g = gid.get(P + "LeftLeg")
r_leg_g = gid.get(P + "RightLeg")

# Measure true limb core radii (median skin thickness around each bone so short sleeves move with arms).
radius = {}
for s in ("Left", "Right"):
    for n in SEGMENTS:
        if P + s + n not in B:
            continue
        a, b = seg(s + n)
        g = gid.get(P + s + n)
        idx = [i for i in range(nv) if dominant(i) == g and not any(eg in extra_g for eg in W[i])]
        if not idx:
            idx = [i for i in range(nv) if dominant(i) == g]
        if not idx:
            continue
        t, d = param(co[idx], a, b)
        core = (t > 0.28) & (t < 0.72)
        dc = d[core] if core.any() else d
        pct = 45 if n in ("Arm", "ForeArm") else 25
        radius[s + n] = max(float(np.percentile(dc, pct)), 0.026)

# Welded mesh adjacency (used for smooth skirt transitions and localized joint smoothing)
nbr = [[] for _ in range(nv)]
for e in mesh.data.edges:
    a_i, b_i = e.vertices
    nbr[a_i].append(b_i)
    nbr[b_i].append(a_i)
pos_map = {}
for i, p_key in enumerate(np.round(co / 1e-5).astype(np.int64)):
    pos_map.setdefault(tuple(p_key), []).append(i)
for ids in pos_map.values():
    if len(ids) > 1:
        for i in ids:
            nbr[i].extend(j for j in ids if j != i)

# Classify skirt/loose-cloth vertices accurately so limb passes never tear skirts or hair/capes.
hips_z = float((AM @ B[P + "Hips"].head_local).z)
knee_z = 0.5 * (
    float((AM @ B[P + "LeftLeg"].head_local).z) + float((AM @ B[P + "RightLeg"].head_local).z)
)
foot_z = 0.5 * (
    float((AM @ B[P + "LeftFoot"].head_local).z) + float((AM @ B[P + "RightFoot"].head_local).z)
)
lu_a, lu_b = seg("LeftUpLeg")
ru_a, ru_b = seg("RightUpLeg")
r_thigh = max(radius.get("LeftUpLeg", 0.065), radius.get("RightUpLeg", 0.065))

is_skirt_vert = np.zeros(nv, dtype=bool)
is_cloth_vert = np.zeros(nv, dtype=bool)
for i in range(nv):
    w = W[i]
    has_skirt = any(g in skirt_g for g in w)
    has_hc = any(g in hair_cape_g for g in w)
    both_legs = (l_upleg_g in w and r_upleg_g in w and w.get(l_upleg_g, 0) > 0.08 and w.get(r_upleg_g, 0) > 0.08)
    if has_skirt or (both_legs and co[i, 2] < hips_z and co[i, 2] > foot_z + 0.05):
        is_skirt_vert[i] = True
        is_cloth_vert[i] = True
    elif has_hc or (hips_g is not None and w.get(hips_g, 0) > 0.15):
        is_cloth_vert[i] = True

# Pass 6A: Skirt / shorts hem thigh binding (R3 step 5: >= 70% UpLeg near hem, smooth centerline bridge + multi-pass smooth -> ZERO tearing/ridges)
skirt_bound = 0
if "skirt" not in SKIP and skirt_g:
    arm_hand_g = {
        gid[n] for n in gid
        if any(k in n for k in ("Arm", "ForeArm", "Hand", "Shoulder"))
    }
    skirt_zone = set()
    for i in range(nv):
        w = W[i]
        sk_sum = sum(w.get(g, 0.0) for g in skirt_g)
        vz = float(co[i, 2])
        if vz > hips_z + 0.04 or vz < foot_z + 0.04:
            continue
        # Include both directly skirt-weighted verts AND adjacent skirt shell verts around the thighs
        if sk_sum < 0.01 and not is_skirt_vert[i]:
            continue
        skirt_zone.add(i)
        is_skirt_vert[i] = True
        is_cloth_vert[i] = True
        for g in list(w):
            if g in arm_hand_g:
                del w[g]
        u = (hips_z - vz) / max(hips_z - knee_z, 0.12)
        thigh_share = 0.84 * smoothstep(u, 0.08, 0.55)
        spring_share = min(sk_sum, 0.14) * smoothstep(u, 0.05, 0.35)
        hips_share = max(0.0, 1.0 - thigh_share - spring_share)

        s_left = smoothstep(float(co[i, 0]), -0.029, 0.029)
        s_right = 1.0 - s_left

        shin_frac = 0.42 * smoothstep(knee_z - vz, 0.0, max(knee_z - foot_z, 0.15) * 0.65)
        ul_frac = 1.0 - shin_frac

        target_w = {}
        if hips_share > 0.01 and hips_g is not None:
            target_w[hips_g] = hips_share
        if spring_share > 0.01 and any(g in w for g in skirt_g):
            best_sk = max((g for g in skirt_g if g in w), key=lambda g: w[g])
            target_w[best_sk] = spring_share
        if thigh_share > 0.01:
            if s_left > 0.02:
                target_w[l_upleg_g] = target_w.get(l_upleg_g, 0.0) + thigh_share * s_left * ul_frac
                if shin_frac > 0.02 and l_leg_g is not None:
                    target_w[l_leg_g] = target_w.get(l_leg_g, 0.0) + thigh_share * s_left * shin_frac
            if s_right > 0.02:
                target_w[r_upleg_g] = target_w.get(r_upleg_g, 0.0) + thigh_share * s_right * ul_frac
                if shin_frac > 0.02 and r_leg_g is not None:
                    target_w[r_leg_g] = target_w.get(r_leg_g, 0.0) + thigh_share * s_right * shin_frac

        # Smooth blend at skirt boundary (no hard step at sk_sum == 0.05)
        blend = smoothstep(sk_sum, 0.0, 0.20) if sk_sum > 0.0 else 1.0
        cur_tot = sum(w.values()) or 1.0
        nw = {}
        for g in set(w) | set(target_w):
            nw[g] = (1.0 - blend) * (w.get(g, 0.0) / cur_tot) + blend * target_w.get(g, 0.0)
        W[i] = nw
        skirt_bound += 1

    # Expand skirt smooth zone by 2 rings and run 4 smoothing iterations (clamping cross-side past |x| > 0.029)
    left_side_skirt = {gid[n] for n in gid if n.startswith(P + "Left") or "_L" in n}
    right_side_skirt = {gid[n] for n in gid if n.startswith(P + "Right") or "_R" in n}
    smooth_skirt = set(skirt_zone)
    for _ in range(2):
        for i in list(smooth_skirt):
            for j in nbr[i]:
                if hips_z + 0.04 >= co[j, 2] >= foot_z + 0.04:
                    smooth_skirt.add(j)
    for _ in range(4):
        nxt = {}
        for i in smooth_skirt:
            if not nbr[i]:
                continue
            acc = {}
            for j in nbr[i]:
                for g, val in W[j].items():
                    acc[g] = acc.get(g, 0.0) + val
            cnt = len(nbr[i])
            cand = {g: 0.55 * W[i].get(g, 0.0) + 0.45 * acc.get(g, 0.0) / cnt for g in set(acc) | set(W[i])}
            vx = float(co[i, 0])
            if abs(vx) > 0.029:
                bad = right_side_skirt if vx > 0.029 else left_side_skirt
                for g in list(cand):
                    if g in bad:
                        del cand[g]
            tot = sum(cand.values()) or 1.0
            nxt[i] = {g: v / tot for g, v in cand.items()}
        for i, nw in nxt.items():
            W[i] = nw
    log.append(f"skirt hem thigh-bound verts: {skirt_bound} (smoothed {len(smooth_skirt)})")

# Pass 6A2: Cape & Hair isolation — flood-fill full cape apron behind torso so arm/leg motion NEVER yanks cape corners
if "cape" not in SKIP and (cape_g or hair_g):
    limb_g_all = {
        gid[n] for n in gid
        if n.startswith(P) and any(k in n for k in ("Arm", "ForeArm", "Hand", "UpLeg", "Leg", "Foot", "Toe"))
    }
    cape_bones_sorted = sorted(
        [b for b in B if b.name.startswith("Cape_")],
        key=lambda b: float((AM @ b.head_local).z),
        reverse=True,
    )
    spine2_y = float((AM @ B[P + "Spine2"].head_local).y) if P + "Spine2" in B else 0.0
    shoulder_z = float((AM @ B[P + "Spine2"].tail_local).z) if P + "Spine2" in B else hips_z + 0.35

    # Seed cape zone with every vertex carrying ANY Cape_* weight (>0.005) and grow across back-of-body cape sheet
    cape_zone = {i for i in range(nv) if sum(W[i].get(g, 0.0) for g in cape_g) > 0.005}
    if cape_zone:
        frontier = set(cape_zone)
        for _ in range(12):
            nxt_f = set()
            for i in frontier:
                for j in nbr[i]:
                    if j in cape_zone:
                        continue
                    # Grow only on the back sheet behind torso/legs (Blender +Y is back) below shoulder collar
                    if co[j, 1] > spine2_y + 0.055 and co[j, 2] < shoulder_z + 0.02:
                        cape_zone.add(j)
                        nxt_f.add(j)
            frontier = nxt_f
            if not frontier:
                break

    cape_cleaned = 0
    for i in cape_zone:
        is_cloth_vert[i] = True
        w = W[i]
        c_sum = sum(w.get(g, 0.0) for g in cape_g)
        dropped_w = 0.0
        for g in list(w):
            if g in limb_g_all:
                dropped_w += w.pop(g)
        if dropped_w > 0.0 or c_sum < 0.05:
            # Map smoothly to nearest cape chain bone by Z height (or Spine2 near collar)
            vz = float(co[i, 2])
            if cape_bones_sorted:
                nearest_cb = min(cape_bones_sorted, key=lambda b: abs(0.5 * ((AM @ b.head_local).z + (AM @ b.tail_local).z) - vz))
                dst_g = gid[nearest_cb.name]
            else:
                dst_g = spine2_g
            if dst_g is not None:
                collar_blend = smoothstep(vz, shoulder_z - 0.14, shoulder_z + 0.02)
                if spine2_g is not None and collar_blend > 0.02:
                    w[spine2_g] = w.get(spine2_g, 0.0) + dropped_w * collar_blend
                    w[dst_g] = w.get(dst_g, 0.0) + dropped_w * (1.0 - collar_blend)
                else:
                    w[dst_g] = w.get(dst_g, 0.0) + max(dropped_w, 1.0 if not w else 0.0)
            cape_cleaned += 1

    # Smooth cape weights across 4 passes so collar & hem deform cleanly with zero sharp spikes
    if cape_zone:
        for _ in range(4):
            nxt = {}
            for i in cape_zone:
                if not nbr[i]:
                    continue
                acc = {}
                cnt = 0
                for j in nbr[i]:
                    if j not in cape_zone and any(g in limb_g_all for g in W[j]):
                        continue
                    cnt += 1
                    for g, val in W[j].items():
                        if g not in limb_g_all:
                            acc[g] = acc.get(g, 0.0) + val
                if cnt == 0:
                    continue
                cand = {g: 0.55 * W[i].get(g, 0.0) + 0.45 * acc.get(g, 0.0) / cnt for g in set(acc) | set(W[i]) if g not in limb_g_all}
                tot = sum(cand.values()) or 1.0
                nxt[i] = {g: v / tot for g, v in cand.items()}
            for i, nw in nxt.items():
                W[i] = nw

    # Hair isolation
    for i in range(nv):
        w = W[i]
        h_sum = sum(w.get(g, 0.0) for g in hair_g)
        if h_sum > 0.15:
            dropped_w = 0.0
            for g in list(w):
                if g in limb_g_all:
                    dropped_w += w.pop(g)
            if dropped_w > 0.0:
                best = max((g for g in hair_g if g in w), key=lambda g: w[g], default=head_g)
                if best is not None:
                    w[best] = w.get(best, 0.0) + dropped_w
                cape_cleaned += 1
    if cape_cleaned:
        log.append(f"cape/hair limb-pull cleaned verts: {cape_cleaned} (cape_zone={len(cape_zone)})")

# Pass 6B: Rigid limb cores (R3 step 2: middle 50-60% of segment = 100% its bone, zero noodle limbs)
rigid_core_mask = np.zeros(nv, dtype=bool)
for s in ("Left", "Right"):
    for n in SEGMENTS:
        if "core" in SKIP or P + s + n not in B or s + n not in radius:
            continue
        a, b = seg(s + n)
        g = gid[P + s + n]
        idx = [i for i in range(nv) if dominant(i) == g and not is_cloth_vert[i]]
        if not idx:
            continue
        t, d = param(co[idx], a, b)
        r = radius[s + n]
        r_lim = (1.65 if n in ("Arm", "ForeArm") else 1.38) * r
        for i, tt, dd in zip(idx, t, d):
            if 0.16 < tt < 0.84 and dd < r_lim and W[i].get(g, 0.0) >= 0.55:
                e = min(smoothstep(tt, 0.16, 0.25), smoothstep(0.84 - tt, 0.0, 0.09))
                if e > 0.98:
                    rigid_core_mask[i] = True
                W[i] = {gg: (vv * (1.0 - e) + (e if gg == g else 0.0)) for gg, vv in W[i].items()}
                if g not in W[i]:
                    W[i][g] = e

# Pass 6C: Joint-radius blend bands (R3 step 3: 1.0x radius at elbow/knee, 0.7x wrist/ankle, 60/40 parent on outside)
for s in ("Left", "Right"):
    for child, parent, k, outer_dir in HINGES:
        if "hinge" in SKIP or P + s + child not in B or P + s + parent not in B:
            continue
        r_ref = radius.get(s + child) or radius.get(s + parent)
        if not r_ref:
            continue
        gc, gp = gid[P + s + child], gid[P + s + parent]
        a, b = seg(s + child)
        axis = (b - a) / max(np.linalg.norm(b - a), 1e-8)
        band = k * r_ref
        outer = np.array(outer_dir, dtype=float)
        has_outer = np.linalg.norm(outer) > 0.5
        perp_lim = (1.70 if child in ("ForeArm", "Hand") else 1.40) * r_ref
        for i in range(nv):
            if is_cloth_vert[i]:
                continue
            w = W[i]
            if not w or (gc not in w and gp not in w):
                continue
            tot = w.get(gc, 0.0) + w.get(gp, 0.0)
            if tot < 0.6:
                continue
            rel = co[i] - a
            x = float(rel @ axis)
            if abs(x) > band:
                continue
            perp = rel - x * axis
            if float(np.linalg.norm(perp)) > perp_lim:
                continue
            x_eff = x
            if has_outer:
                o_proj = float(perp @ outer) / max(r_ref, 1e-6)
                if o_proj > 0.0:
                    x_eff -= 0.14 * band * min(o_proj, 1.0)
            tt = float(np.clip((x_eff / band + 1.0) * 0.5, 0.0, 1.0))
            wc = tt * tt * (3.0 - 2.0 * tt)
            W[i][gc] = tot * wc
            W[i][gp] = tot * (1.0 - wc)

# Pass 6D: Twist ramps (R2: proximal ArmTwist 50%, ForeArm 0 -> 33 -> 66 -> 100%)
for s in ("Left", "Right"):
    if "twist" in SKIP or P + s + "ArmTwist" not in gid or P + s + "ForeArm" not in B:
        continue
    ga, gat = gid[P + s + "Arm"], gid[P + s + "ArmTwist"]
    a, b = seg(s + "Arm")
    for i in range(nv):
        if is_cloth_vert[i] or ga not in W[i]:
            continue
        t, _ = param(co[i], a, b)
        # Proximal shoulder-to-mid-bicep carries ArmTwist (-50% local -> +50% world twist); distal stays 100% Arm.
        share = 1.0 - smoothstep(t, 0.12, 0.72)
        if share > 0.01:
            w = W[i][ga]
            W[i][ga] = w * (1.0 - share)
            W[i][gat] = W[i].get(gat, 0.0) + w * share

    gf, g1, g2 = gid[P + s + "ForeArm"], gid[P + s + "ForeArmTwist1"], gid[P + s + "ForeArmTwist2"]
    a, b = seg(s + "ForeArm")
    for i in range(nv):
        if is_cloth_vert[i] or gf not in W[i]:
            continue
        t, _ = param(co[i], a, b)
        t = float(np.clip(t, 0.0, 1.0))
        w = W[i].pop(gf)
        nodes = [(0.0, gf), (1.0 / 3.0, g1), (2.0 / 3.0, g2), (1.0, g2)]
        for (t0, n0), (t1, n1) in zip(nodes[:-1], nodes[1:]):
            if t0 <= t <= t1:
                f = (t - t0) / max(t1 - t0, 1e-8)
                W[i][n0] = W[i].get(n0, 0.0) + w * (1.0 - f)
                W[i][n1] = W[i].get(n1, 0.0) + w * f
                break

# Pass 6E: Half-angle helpers (R2/R3: ForeArmHelper, LegHelper, HandHelper in crease blend bands)
for s in ("Left", "Right"):
    for helper, side_a, side_b in (
        ("ForeArmHelper", ["Arm", "ArmTwist"], ["ForeArm", "ForeArmTwist1"]),
        ("LegHelper", ["UpLeg"], ["Leg"]),
        ("HandHelper", ["ForeArm", "ForeArmTwist2"], ["Hand"]),
    ):
        if "helper" in SKIP or P + s + helper not in gid:
            continue
        gh = gid[P + s + helper]
        ga_ = [gid[P + s + n] for n in side_a if P + s + n in gid]
        gb_ = [gid[P + s + n] for n in side_b if P + s + n in gid]
        for i in range(nv):
            if is_cloth_vert[i]:
                continue
            wa = sum(W[i].get(g, 0.0) for g in ga_)
            wb = sum(W[i].get(g, 0.0) for g in gb_)
            m = min(wa, wb)
            if m < 0.06:
                continue
            take = min(m, 0.55)
            for g in ga_:
                if g in W[i] and wa > 0:
                    W[i][g] -= 0.5 * take * (W[i][g] / wa)
            for g in gb_:
                if g in W[i] and wb > 0:
                    W[i][g] -= 0.5 * take * (W[i][g] / wb)
            W[i][gh] = W[i].get(gh, 0.0) + take

# Pass 6F: Localized joint-zone smoothing (R3 step 6: factor 0.40, 3 iterations; NEVER bleeds into rigid cores!)
if "smooth" not in SKIP:
    smooth_zone = set()
    for s in ("Left", "Right"):
        for jname, r_scale in (("Arm", 1.5), ("ForeArm", 1.05), ("Hand", 0.75), ("UpLeg", 1.4), ("Leg", 1.05), ("Foot", 0.8)):
            bn = P + s + jname
            if bn not in B:
                continue
            c = np.array(AM @ B[bn].head_local)
            r_lim = r_scale * radius.get(s + jname, radius.get(s + ("ForeArm" if jname == "Hand" else "Leg"), 0.05))
            hits = np.where(np.linalg.norm(co - c, axis=1) < r_lim)[0]
            for idx_v in hits:
                if not rigid_core_mask[idx_v] and not is_cloth_vert[idx_v]:
                    smooth_zone.add(int(idx_v))
    for _ in range(3):
        nxt = {}
        for i in smooth_zone:
            acc = {}
            cnt = 0
            for j in nbr[i]:
                if is_cloth_vert[j]:
                    continue
                cnt += 1
                for g, val in W[j].items():
                    acc[g] = acc.get(g, 0.0) + val
            if cnt == 0:
                continue
            nxt[i] = {
                g: 0.60 * W[i].get(g, 0.0) + 0.40 * acc.get(g, 0.0) / cnt
                for g in set(acc) | set(W[i])
            }
        for i, nw in nxt.items():
            W[i] = nw

# Re-lock middle rigid cores (t in [0.24, 0.76]) to their segment's own twist-chain bones ONLY (guarantees <= 2% stretch / <= 3% core rigidity!)
if "core" not in SKIP:
    seg_chain_g = {}
    for s in ("Left", "Right"):
        seg_chain_g[s + "Arm"] = {gid[P + s + x] for x in ("Arm", "ArmTwist") if P + s + x in gid}
        seg_chain_g[s + "ForeArm"] = {gid[P + s + x] for x in ("ForeArm", "ForeArmTwist1", "ForeArmTwist2") if P + s + x in gid}
        seg_chain_g[s + "UpLeg"] = {gid[P + s + "UpLeg"]} if P + s + "UpLeg" in gid else set()
        seg_chain_g[s + "Leg"] = {gid[P + s + "Leg"]} if P + s + "Leg" in gid else set()
    for s in ("Left", "Right"):
        for n in SEGMENTS:
            key = s + n
            allowed = seg_chain_g.get(key)
            if not allowed or P + key not in B or key not in radius:
                continue
            a, b = seg(key)
            r_lim = (1.65 if n in ("Arm", "ForeArm") else 1.38) * radius[key]
            for i in range(nv):
                if is_cloth_vert[i]:
                    continue
                own_sum = sum(W[i].get(g, 0.0) for g in allowed)
                if own_sum < 0.55:
                    continue
                t, d = param(co[i], a, b)
                if 0.23 < t < 0.77 and d < r_lim:
                    W[i] = {g: val / own_sum for g, val in W[i].items() if g in allowed}

# Pass 6G: Stray-influence pruning (R3 step 7: > 1.5x limb radius, shoulder/hip root tolerance, cloth-safe)
LIMB_MAP = {
    "Arm": "Arm", "ArmTwist": "Arm",
    "ForeArm": "ForeArm", "ForeArmTwist1": "ForeArm", "ForeArmTwist2": "ForeArm", "ForeArmHelper": "ForeArm",
    "Hand": "ForeArm", "HandHelper": "ForeArm",
    "UpLeg": "UpLeg", "Leg": "Leg", "LegHelper": "Leg",
}
seg_cache = {}
for s in ("Left", "Right"):
    for n, base in LIMB_MAP.items():
        if P + s + n in gid and P + s + base in B and s + base in radius:
            a, b = seg(s + base)
            is_root = base in ("Arm", "UpLeg")
            is_leg = base in ("UpLeg", "Leg")
            seg_cache[gid[P + s + n]] = (a, b, radius[s + base], is_root, is_leg)

removed = 0
for i in range(nv):
    if "stray" in SKIP:
        break
    for g in list(W[i]):
        if g not in seg_cache or len(W[i]) <= 1:
            continue
        a, b, r, is_root, is_leg = seg_cache[g]
        # Protect skirt vertices' intentional UpLeg/Leg bindings; still prune stray Arm/ForeArm/Hand from skirts!
        if is_skirt_vert[i] and is_leg:
            continue
        if any(eg in hair_cape_g for eg in W[i]) and not is_leg:
            continue
        t, d = param(co[i], a, b)
        lim = (2.25 if (is_root and t < 0.22) else 1.55) * r
        if d > lim and W[i][g] < 0.35:
            del W[i][g]
            removed += 1
log.append(f"stray influences removed: {removed}")

# Prune cross-side influences beyond |x| > 0.028m (R0/R3 zero opposite-side bleed)
left_side_g = {gid[n] for n in gid if n.startswith(P + "Left") or "_L" in n}
right_side_g = {gid[n] for n in gid if n.startswith(P + "Right") or "_R" in n}
cross_removed = 0
for i in range(nv):
    vx = float(co[i, 0])
    if abs(vx) <= 0.028:
        continue
    bad_g = right_side_g if vx > 0.028 else left_side_g
    dropped_sum = 0.0
    for g in list(W[i]):
        if g in bad_g:
            dropped_sum += W[i].pop(g)
            cross_removed += 1
    if dropped_sum > 0.0 and not W[i]:
        # Reassign to centerline torso/hips if every group was opposite-side
        fb = hips_g if co[i, 2] < hips_z + 0.12 else (spine2_g or hips_g)
        if fb is not None:
            W[i][fb] = 1.0
log.append(f"cross-side influences removed: {cross_removed}")

# Seam-twin parity, strip non-deforming _End/Tip weights, prune < 0.01, cap at top 4, normalize.
non_deform_g = {gid[b.name] for b in B if not b.use_deform and b.name in gid}
pos = {}
for i, p_key in enumerate(np.round(co / 1e-5).astype(np.int64)):
    pos.setdefault(tuple(p_key), []).append(i)
for ids in pos.values():
    if len(ids) > 1:
        avg = {}
        for i in ids:
            for g, val in W[i].items():
                if g not in non_deform_g:
                    avg[g] = avg.get(g, 0.0) + val / len(ids)
        if avg:
            for i in ids:
                W[i] = dict(avg)

for i, v in enumerate(mesh.data.vertices):
    w = {g: val for g, val in W[i].items() if val > 0.01 and g not in non_deform_g}
    if not w:
        # fallback to Hips if all pruned
        w = {hips_g: 1.0} if hips_g is not None else {}
    top = dict(sorted(w.items(), key=lambda q: -q[1])[:4])
    tot = sum(top.values()) or 1.0
    for g in list(v.groups):
        vgs[g.group].remove([i])
    for g, val in top.items():
        vgs[g].add([i], val / tot, "REPLACE")

# Remove any vertex group for non-deforming bones so glTF skin stays clean
for bn in list(gid.keys()):
    if bn in B and not B[bn].use_deform:
        vg = vgs.get(bn)
        if vg is not None:
            vgs.remove(vg)

# ---- 7. Build rich <kid>_rig.json in gltf_yup space ---------------------------------------------------------------
height_m = float(co[:, 2].max() - co[:, 2].min())
leg_len = float(
    np.linalg.norm(
        np.array(AM @ B[P + "LeftUpLeg"].head_local) - np.array(AM @ B[P + "LeftFoot"].head_local)
    )
) if (P + "LeftUpLeg" in B and P + "LeftFoot" in B) else 0.7
ref_leg_len = float(
    np.linalg.norm(np.array(ref["LeftUpLeg"]["head"]) - np.array(ref["LeftFoot"]["head"]))
) if ("LeftUpLeg" in ref and "LeftFoot" in ref) else leg_len

# Torso capsule: hips to spine2, 80th percentile radius from spine vertices
tg = [gid[P + n] for n in ("Hips", "Spine", "Spine1", "Spine2") if P + n in gid]
tidx = [i for i in range(nv) if dominant(i) in tg and not is_cloth_vert[i]]
torso_r = 0.14
capsules = {}
if tidx and P + "Hips" in B and P + "Spine2" in B:
    ta = np.array(AM @ B[P + "Hips"].head_local)
    tb = np.array(AM @ B[P + "Spine2"].tail_local)
    _, td = param(co[tidx], ta, tb)
    torso_r = float(np.percentile(td, 80))
    capsules["Torso"] = {"bone": P + "Spine1", "a": to_gltf(ta), "b": to_gltf(tb), "radius": round(torso_r, 4)}

for k, r in radius.items():
    a, b = seg(k)
    capsules[k] = {"bone": P + k, "a": to_gltf(a), "b": to_gltf(b), "radius": round(float(r), 4)}

arm_r = 0.5 * (radius.get("LeftArm", 0.04) + radius.get("RightArm", 0.04))
thigh_r = 0.5 * (radius.get("LeftUpLeg", 0.065) + radius.get("RightUpLeg", 0.065))
# Height-normalized girth clearance offsets vs slim 1.70m reference (~0.112*H torso, ~0.036*H arm, ~0.052*H bare thigh)
h_ref = max(height_m, 1.0)
arm_abduct_deg = round(max(0.0, min(14.0, ((torso_r + arm_r) / h_ref - 0.148) * 160.0)), 2)
# Exclude skirt flare from hip spread by capping thigh_r when kid has skirt chains
eff_thigh_r = min(thigh_r, 0.055 * h_ref) if skirt_g else thigh_r
hip_spread_deg = round(max(0.0, min(10.0, (eff_thigh_r / h_ref - 0.052) * 200.0)), 2)

# Detect surviving extra hair/skirt/cape chains for runtime Verlet spring bones (High & Ultra)
spring_chains = []
extra_roots = [
    b for b in B
    if not b.name.startswith(P) and b.use_deform and (b.parent is None or b.parent.name.startswith(P))
]
for root in sorted(extra_roots, key=lambda b: b.name):
    chain_names = []
    cur = root
    while cur is not None:
        chain_names.append(cur.name)
        nxt_kids = [c for c in cur.children if not c.name.startswith(P) and c.use_deform]
        cur = nxt_kids[0] if nxt_kids else None
    low_name = root.name.lower()
    if low_name.startswith("hair"):
        kind, stiff, damp, grav, cols = "hair", 0.35, 0.28, 0.65, ["Torso"]
    elif low_name.startswith("cape"):
        kind, stiff, damp, grav, cols = "cape", 0.25, 0.22, 0.85, ["Torso", "LeftUpLeg", "RightUpLeg"]
    elif low_name.startswith("skirt"):
        kind, stiff, damp, grav, cols = "skirt", 0.45, 0.32, 0.70, ["LeftUpLeg", "RightUpLeg"]
    else:
        kind, stiff, damp, grav, cols = "cloth", 0.35, 0.28, 0.70, ["Torso"]
    spring_chains.append({
        "name": root.name.rsplit("_", 1)[0].lower(),
        "kind": kind,
        "rootParent": root.parent.name if root.parent else P + "Hips",
        "bones": chain_names,
        "stiffness": stiff,
        "damping": damp,
        "gravity": grav,
        "colliders": cols,
    })

bones_meta = {}
for b in B:
    if b.name.startswith(P):
        short = b.name[len(P):]
        bones_meta[short] = {
            "parent": (b.parent.name[len(P):] if b.parent and b.parent.name.startswith(P) else None),
            "head": to_gltf(AM @ b.head_local),
            "tail": to_gltf(AM @ b.tail_local),
            "length": round(float((AM @ b.tail_local - AM @ b.head_local).length), 5),
        }

twist_bones = []
helper_bones = []
for s in ("Left", "Right"):
    if P + s + "ArmTwist" in B:
        twist_bones.append({"bone": P + s + "ArmTwist", "source": P + s + "Arm", "share": -0.5})
    if P + s + "ForeArmTwist1" in B:
        twist_bones.append({"bone": P + s + "ForeArmTwist1", "source": P + s + "Hand", "share": 0.33})
    if P + s + "ForeArmTwist2" in B:
        twist_bones.append({"bone": P + s + "ForeArmTwist2", "source": P + s + "Hand", "share": 0.66})
    for h_name, src_name in (("ForeArmHelper", "ForeArm"), ("LegHelper", "Leg"), ("HandHelper", "Hand")):
        if P + s + h_name in B:
            helper_bones.append({"bone": P + s + h_name, "source": P + s + src_name, "share": 0.5})

rig = {
    "schemaVersion": 1,
    "kid": KID_ID,
    "coordSpace": "gltf_yup",
    "heightM": round(height_m, 4),
    "legLengthM": round(leg_len, 4),
    "refLegLengthM": round(ref_leg_len, 4),
    "legLengthRatio": round(leg_len / max(ref_leg_len, 1e-4), 4),
    "girth": {
        "torsoRadiusM": round(torso_r, 4),
        "armRadiusM": round(arm_r, 4),
        "thighRadiusM": round(thigh_r, 4),
        "armAbductOffsetDeg": arm_abduct_deg,
        "hipSpreadOffsetDeg": hip_spread_deg,
    },
    "jointLimitsRef": "rig__joint_limits",
    "bones": bones_meta,
    "capsules": capsules,
    "twistBones": twist_bones,
    "helperBones": helper_bones,
    "springChains": spring_chains,
    "correctives": [],
    "lods": {},
    "textures": {
        "baseColorEmbedded": True,
        "normal": f"models/characters/{KID_ID}_2",
        "orm": f"models/characters/{KID_ID}_orm",
        "ormChannels": "R=AO,G=Roughness,B=Metallic",
    },
    "log": log,
}

with open(RIGJ, "w", encoding="utf-8", newline="\n") as f:
    json.dump(rig, f, indent=2)
    f.write("\n")

deform_bones = sum(1 for b in B if b.use_deform)
print("\n".join(log))
print(f"[contract] {KID_ID}: height={height_m:.3f}m, legRatio={rig['legLengthRatio']:.3f}, deformBones={deform_bones}, springs={len(spring_chains)}")
bpy.ops.export_scene.gltf(
    filepath=DST, export_format="GLB", export_yup=True,
    export_animations=False, export_skins=True, export_def_bones=False,
    export_morph=True, export_extras=True, export_image_format="AUTO",
)
print("[contract] exported", DST)
