# apex-rig-forge R1-R3 on one normalized kid:
#   apply scale, strip bones that break the shared bone paths, rebind to the roster reference rest pose,
#   copy the reference rest orientations, add twist + half-angle helper bones, skinning passes
#   (rigid cores, hinge blend bands, twist ramps, helpers, stray influences, seam twins, top-4 normalize),
#   export the .glb and a rig-data json (lengths, limb capsules).
# usage: python contract_kid.py norm.glb ref_rest.json out.glb out_rig.json
import sys, bpy, math, json
import numpy as np
from mathutils import Vector, Matrix

SRC, REF, DST, RIGJ = sys.argv[1:5]
P = "mixamorig:"
import os
SKIP = set(os.environ.get("SKIP", "").split(","))
ref = json.load(open(REF))
HINGES = [("ForeArm", "Arm", 1.0), ("Leg", "UpLeg", 1.0)]           # child, parent, band x radius
SEGMENTS = ["Arm", "ForeArm", "UpLeg", "Leg"]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
for a in list(bpy.data.actions):
    bpy.data.actions.remove(a)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
if arm.animation_data:
    arm.animation_data_clear()
mesh = max((o for o in bpy.data.objects if o.type == 'MESH' and o.vertex_groups), key=lambda o: len(o.data.vertices))
for o in [o for o in bpy.data.objects if o.type == 'MESH' and o != mesh]:
    bpy.data.objects.remove(o, do_unlink=True)
bpy.context.view_layer.update()
log = []


def set_mode(obj, mode):
    for o in bpy.data.objects:
        if o is not None:
            o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    if obj.mode != mode:
        bpy.ops.object.mode_set(mode=mode)


# ---- 1. apply object scale/rotation (Tere's 0.01 armature)
set_mode(arm, 'OBJECT')
if any(abs(s - 1.0) > 1e-4 for s in arm.matrix_world.to_scale()) or any(abs(s - 1.0) > 1e-4 for s in mesh.matrix_world.to_scale()):
    for o in bpy.data.objects:
        o.select_set(o in (arm, mesh))
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    log.append("applied object scale")

# ---- 2. strip bones that break the contract paths (weights to the nearest contract bone)
def is_contract(n):
    return n.startswith(P) and "WristExtra" not in n

bones = arm.data.bones
breakers = [b.name for b in bones if not is_contract(b.name) and
            ("WristExtra" in b.name or any(is_contract(c.name) for c in b.children_recursive))]
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
                dst.add([v.index], g.weight, 'ADD')
    vgs.remove(src)
set_mode(arm, 'EDIT')
eb = arm.data.edit_bones
for n in breakers:
    b = eb[n]
    for c in list(b.children):
        c.use_connect = False
        c.parent = b.parent if (b.parent is None or b.parent.name not in breakers) else None
    eb.remove(b)
# any orphaned contract bone goes back under its contract parent (Hips becomes the root)
for b in eb:
    if b.parent is None and b.name != P + "Hips" and b.name.startswith(P):
        rp = ref.get(b.name[len(P):], {}).get("parent")
        if rp and P + rp in eb:
            b.parent = eb[P + rp]
    if b.name == P + "Hips":
        b.parent = None
set_mode(arm, 'OBJECT')
log.append(f"removed {len(breakers)} path-breaking bones")

# ---- 3. rebind to the reference rest pose (directions), then 4. copy the reference roll
AM = arm.matrix_world.copy(); AMi = AM.inverted()
pb = arm.pose.bones
REPOSE = {s + n for s in ("Left", "Right") for n in ("Arm", "ForeArm", "Hand")}   # big rest differences: re-pose
FINGER = ("Thumb", "Index", "Middle", "Ring", "Pinky")
order = sorted([b for b in arm.data.bones if b.name.startswith(P) and b.name[len(P):] in REPOSE],
               key=lambda b: len(b.parent_recursive))
arm_dir = {}
set_mode(arm, 'POSE')
for b in order:
    p = pb[b.name]
    bpy.context.view_layer.update()
    d = (AM @ p.tail - AM @ p.head).normalized()
    R = ref[b.name[len(P):]]["rot"]
    rr = Vector((R[0][1], R[1][1], R[2][1])).normalized()
    key = b.name[len(P):]
    if key.endswith("Arm") and not key.endswith("ForeArm"):
        horiz = Vector((d.x, d.y, 0.0)).normalized()            # the kid's own direction across the floor
        elev = math.asin(max(-1.0, min(1.0, rr.z)))              # the reference's elevation (40 deg down)
        r = (horiz * math.cos(elev) + Vector((0, 0, 1)) * math.sin(elev)).normalized()
        arm_dir[key[:-3]] = r
    else:
        r = arm_dir.get(key.replace("ForeArm", "").replace("Hand", ""), rr)   # forearm and hand in line
    if (d - r).length < 1e-5:
        continue
    q = d.rotation_difference(r).to_matrix().to_4x4()
    pivot = AM @ p.head
    p.matrix = AMi @ (Matrix.Translation(pivot) @ q @ Matrix.Translation(-pivot) @ AM @ p.matrix)
bpy.context.view_layer.update()
set_mode(mesh, 'OBJECT')
mod = next(m for m in mesh.modifiers if m.type == 'ARMATURE')
bpy.ops.object.modifier_apply(modifier=mod.name)
set_mode(arm, 'POSE')
bpy.ops.pose.armature_apply(selected=False)
# rest frames = the reference's (no mesh change): direction and roll, own head and length;
# fingers keep their own palm-facing frames (shared clips carry no finger channels)
set_mode(arm, 'EDIT')
eb = arm.data.edit_bones
for b in eb:
    key = b.name[len(P):] if b.name.startswith(P) else None
    if key not in ref or any(f in key for f in FINGER):
        continue
    R = ref[key]["rot"]
    y = AMi.to_3x3() @ Vector((R[0][1], R[1][1], R[2][1]))
    z = AMi.to_3x3() @ Vector((R[0][2], R[1][2], R[2][2]))
    L = (b.tail - b.head).length
    kids = [(c, c.use_connect) for c in b.children]
    for c, _ in kids:
        c.use_connect = False
    b.tail = b.head + y.normalized() * L
    b.align_roll(z)

# ---- 5. twist and half-angle helper bones (contract R2)
def add_bone(name, parent, head, tail, roll_like):
    nb = eb.new(name)
    nb.head, nb.tail = head, tail
    nb.parent = eb[parent]
    nb.use_connect = False
    nb.roll = eb[roll_like].roll
    return nb

added = []
for s in ("Left", "Right"):
    A_, F_, H_, U_, L_ = (P + s + x for x in ("Arm", "ForeArm", "Hand", "UpLeg", "Leg"))
    if not all(n in eb for n in (A_, F_, H_, U_, L_)):
        continue
    a, f, h, u, l = eb[A_], eb[F_], eb[H_], eb[U_], eb[L_]
    add_bone(P + s + "ArmTwist", A_, a.head.lerp(a.tail, 0.5), a.tail, A_)
    add_bone(P + s + "ForeArmTwist1", F_, f.head.lerp(f.tail, 1 / 3), f.head.lerp(f.tail, 2 / 3), F_)
    add_bone(P + s + "ForeArmTwist2", F_, f.head.lerp(f.tail, 2 / 3), f.tail, F_)
    add_bone(P + s + "ForeArmHelper", A_, f.head.copy(), f.head + (f.tail - f.head) * 0.25, F_)
    add_bone(P + s + "LegHelper", U_, l.head.copy(), l.head + (l.tail - l.head) * 0.25, L_)
    add_bone(P + s + "HandHelper", F_, h.head.copy(), h.head + (h.tail - h.head) * 0.5, H_)
    added += [s + x for x in ("ArmTwist", "ForeArmTwist1", "ForeArmTwist2", "ForeArmHelper", "LegHelper", "HandHelper")]
set_mode(arm, 'OBJECT')
mod = mesh.modifiers.new("Armature", 'ARMATURE'); mod.object = arm
log.append(f"rebound to the reference rest; added {len(added)} twist/helper bones")

# ---- 6. skinning passes
AM = arm.matrix_world
B = arm.data.bones
co = np.array([tuple(mesh.matrix_world @ v.co) for v in mesh.data.vertices])
vgs = mesh.vertex_groups
gid = {g.name: g.index for g in vgs}
for n in added:
    if P + n not in gid:
        gid[P + n] = vgs.new(name=P + n).index
gname = {i: n for n, i in gid.items()}
W = [{g.group: g.weight for g in v.groups if g.weight > 0} for v in mesh.data.vertices]


def seg(n):
    b = B[P + n]
    return np.array(AM @ b.head_local), np.array(AM @ b.tail_local)


def param(x, a, b):
    ab = b - a
    t = ((x - a) @ ab) / max(ab @ ab, 1e-12)
    return t, np.linalg.norm(x - (a + np.clip(t, 0, 1)[..., None] * ab), axis=-1) if np.ndim(t) else np.linalg.norm(x - (a + np.clip(t, 0, 1) * ab))


def dominant(i):
    return max(W[i], key=W[i].get) if W[i] else None


extra_g = {gid[n] for n in gid if not n.startswith(P)}
hips_g = gid.get(P + "Hips")


def is_cloth(i):
    """cloth/hair or hip-blended vertices: hinge bands and helpers leave them alone"""
    w = W[i]
    return any(g in extra_g for g in w) or (hips_g is not None and w.get(hips_g, 0) > 0.1)


radius = {}
for s in ("Left", "Right"):
    for n in SEGMENTS:
        if P + s + n not in B:
            continue
        a, b = seg(s + n)
        g = gid.get(P + s + n)
        idx = [i for i in range(len(co)) if dominant(i) == g]
        if not idx:
            continue
        t, d = param(co[idx], a, b)
        core = (t > 0.3) & (t < 0.7)
        # limb thickness from the skin hugging the bone (auto-rigs tie torso/skirt points to limbs too)
        dc = d[core] if core.any() else d
        radius[s + n] = float(np.percentile(dc, 20))
        # rigid core: the middle of the segment is 100% its bone (no noodle), only on the limb itself
        for i, tt, dd in zip(idx, t, d):
            if "core" in SKIP:
                break
            if 0.25 < tt < 0.75 and dd < 1.35 * radius[s + n] and W[i].get(g, 0) >= 0.75:
                W[i] = {g: 1.0}     # only firm up skin already mostly on this bone (cloth blends stay)

# hinge blend bands (elbow, knee): smoothstep across 1.0 x radius at the crease, 60/40 toward the parent outside
for s in ("Left", "Right"):
    for child, parent, k in HINGES:
        if "hinge" in SKIP or P + s + child not in B or s + child not in radius:
            continue
        gc, gp = gid[P + s + child], gid[P + s + parent]
        a, b = seg(s + child)
        axis = (b - a) / np.linalg.norm(b - a)
        band = k * radius[s + child]
        for i in range(len(co)):
            w = W[i]
            if not w or (gc not in w and gp not in w):
                continue
            if sum(v for g, v in w.items() if g in (gc, gp)) < 0.6 or is_cloth(i):
                continue
            x = (co[i] - a) @ axis
            if abs(x) > band:
                continue
            if np.linalg.norm((co[i] - a) - x * axis) > 1.4 * radius[s + child]:
                continue                      # cloth hanging off the limb (skirts) keeps its weights
            tt = np.clip((x / band + 1) / 2, 0, 1)
            wc = tt * tt * (3 - 2 * tt)
            tot = w.get(gc, 0) + w.get(gp, 0)
            W[i][gc] = tot * wc; W[i][gp] = tot * (1 - wc)

# twist ramps: upper arm (ArmTwist takes up to 50%), forearm 0 -> 33 -> 66 -> 100% across ForeArm/Twist1/Twist2
for s in ("Left", "Right"):
    if "twist" in SKIP or P + s + "ArmTwist" not in gid or P + s + "ForeArm" not in B:
        continue
    ga, gat = gid[P + s + "Arm"], gid[P + s + "ArmTwist"]
    a, b = seg(s + "Arm")
    for i in range(len(co)):
        if ga in W[i]:
            t, _ = param(co[i], a, b)
            share = 0.5 * float(np.clip((t - 0.3) / 0.7, 0, 1))
            if share > 0:
                w = W[i][ga]; W[i][ga] = w * (1 - share); W[i][gat] = W[i].get(gat, 0) + w * share
    gf, g1, g2 = gid[P + s + "ForeArm"], gid[P + s + "ForeArmTwist1"], gid[P + s + "ForeArmTwist2"]
    a, b = seg(s + "ForeArm")
    for i in range(len(co)):
        if gf in W[i]:
            t, _ = param(co[i], a, b)
            t = float(np.clip(t, 0, 1)); w = W[i].pop(gf)
            nodes = [(0.0, gf), (1 / 3, g1), (2 / 3, g2), (1.0, g2)]
            for (t0, n0), (t1, n1) in zip(nodes[:-1], nodes[1:]):
                if t0 <= t <= t1:
                    f = (t - t0) / (t1 - t0)
                    W[i][n0] = W[i].get(n0, 0) + w * (1 - f)
                    W[i][n1] = W[i].get(n1, 0) + w * f
                    break

# half-angle helpers: where a joint's two sides blend, part of the pair goes to the helper
for s in ("Left", "Right"):
    for helper, side_a, side_b in (("ForeArmHelper", ["Arm", "ArmTwist"], ["ForeArm", "ForeArmTwist1"]),
                                   ("LegHelper", ["UpLeg"], ["Leg"]),
                                   ("HandHelper", ["ForeArmTwist2"], ["Hand"])):
        if "helper" in SKIP or P + s + helper not in gid:
            continue
        gh = gid[P + s + helper]
        ga_ = [gid[P + s + n] for n in side_a if P + s + n in gid]
        gb_ = [gid[P + s + n] for n in side_b if P + s + n in gid]
        for i in range(len(co)):
            if is_cloth(i):
                continue
            wa = sum(W[i].get(g, 0) for g in ga_); wb = sum(W[i].get(g, 0) for g in gb_)
            m = min(wa, wb)
            if m < 0.05:
                continue
            take = m              # the helper carries the overlap (follows half the joint)
            for g in ga_:
                if g in W[i] and wa > 0: W[i][g] -= take * W[i][g] / wa / 2 * 2 * 0.5
            for g in gb_:
                if g in W[i] and wb > 0: W[i][g] -= take * W[i][g] / wb / 2 * 2 * 0.5
            W[i][gh] = W[i].get(gh, 0) + take

# stray influences: drop an influence whose bone segment is farther than 1.5 x the local limb radius
# limb bones only; distance to the limb segment (head of the segment to the next joint) vs that limb's thickness
LIMB = {"Arm": "Arm", "ArmTwist": "Arm", "ForeArm": "ForeArm", "ForeArmTwist1": "ForeArm", "ForeArmTwist2": "ForeArm",
        "UpLeg": "UpLeg", "Leg": "Leg"}
seg_cache = {}
for s in ("Left", "Right"):
    for n, base in LIMB.items():
        if P + s + n in gid and P + s + base in B and s + base in radius:
            a, b = seg(s + base)
            seg_cache[gid[P + s + n]] = (a, b, radius[s + base])
removed = 0
for i in range(len(co)):
    if "stray" in SKIP or any(g in extra_g for g in W[i]):
        continue                      # cloth/hair vertices keep every influence (they must follow the legs)
    for g in list(W[i]):
        if g not in seg_cache or len(W[i]) == 1:
            continue
        a, b, r = seg_cache[g]
        _, d = param(co[i], a, b)
        if d > 6.0 * r and W[i][g] < 0.2:
            del W[i][g]; removed += 1
log.append(f"stray influences removed: {removed}")

# seam twins identical, prune, top 4, normalize
pos = {}
for i, p in enumerate(np.round(co / 1e-5).astype(np.int64)):
    pos.setdefault(tuple(p), []).append(i)
for ids in pos.values():
    if len(ids) > 1:
        avg = {}
        for i in ids:
            for g, val in W[i].items():
                avg[g] = avg.get(g, 0) + val / len(ids)
        for i in ids:
            W[i] = dict(avg)
for i, v in enumerate(mesh.data.vertices):
    w = {g: val for g, val in W[i].items() if val > 0.01}
    top = dict(sorted(w.items(), key=lambda q: -q[1])[:4])
    tot = sum(top.values()) or 1.0
    for g in list(v.groups):
        vgs[g.group].remove([i])
    for g, val in top.items():
        vgs[g].add([i], val / tot, 'REPLACE')

# ---- 7. rig data: lengths and limb capsules (for push-out, scans and girth offsets)
rig = {"bones": {}, "capsules": {}, "leg_length": 0.0, "log": log}
for b in B:
    if b.name.startswith(P):
        rig["bones"][b.name[len(P):]] = {"head": list(AM @ b.head_local), "tail": list(AM @ b.tail_local),
                                         "length": b.length * AM.to_scale()[0]}
for k, r in radius.items():
    a, b = seg(k)
    rig["capsules"][k] = {"a": list(a), "b": list(b), "radius": r}
# torso capsule: hips to spine2, radius from vertices the spine bones drive
tg = [gid[P + n] for n in ("Hips", "Spine", "Spine1", "Spine2") if P + n in gid]
tidx = [i for i in range(len(co)) if dominant(i) in tg]
if tidx:
    a = np.array(AM @ B[P + "Hips"].head_local); b = np.array(AM @ B[P + "Spine2"].tail_local)
    _, d = param(co[tidx], a, b)
    rig["capsules"]["Torso"] = {"a": list(a), "b": list(b), "radius": float(np.percentile(d, 80))}
for s in ("Left",):
    if P + s + "UpLeg" in B and P + s + "Foot" in B:
        rig["leg_length"] = float(np.linalg.norm(np.array(AM @ B[P + s + "UpLeg"].head_local) - np.array(AM @ B[P + s + "Foot"].head_local)))
json.dump(rig, open(RIGJ, "w"), indent=1, default=float)
print("\n".join(log))
bpy.ops.export_scene.gltf(filepath=DST, export_format='GLB', export_animations=False, export_skins=True,
                          export_morph=True, export_extras=True, export_image_format='AUTO')
print("exported", DST)
