# Rig tuning for one kid: rename the skeleton to the shared humanoid names (mixamorig:*), then refine the whole
# body's skin weights (prune, seam twins identical, smooth around the big joints, normalize, max 4 bones).
# usage: python normalize_kid.py in.glb out.glb
import sys, bpy
import numpy as np
from mathutils import Vector

SRC, DST = sys.argv[1], sys.argv[2]
P = "mixamorig:"
JOINT_SMOOTH = {"ForeArm": 0.055, "Arm": 0.065, "Hand": 0.04, "Leg": 0.06, "UpLeg": 0.07, "Spine1": 0.06, "Neck": 0.045}
SMOOTH_ITERS, SMOOTH_F = 2, 0.35

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
arm.data.pose_position = 'REST'
mesh = max((o for o in bpy.data.objects if o.type == 'MESH' and o.vertex_groups), key=lambda o: len(o.data.vertices))
for o in [o for o in bpy.data.objects if o.type == 'MESH' and o != mesh and not o.vertex_groups]:
    bpy.data.objects.remove(o, do_unlink=True)        # the importer's stray icosphere
AM = arm.matrix_world
B = arm.data.bones
head = {b.name: np.array(AM @ b.head_local) for b in B}
tail = {b.name: np.array(AM @ b.tail_local) for b in B}
zs = [head[n][2] for n in head] + [tail[n][2] for n in tail]
mz = np.array([tuple(mesh.matrix_world @ v.co) for v in mesh.data.vertices])
H = mz[:, 2].max() - mz[:, 2].min()
floor = mz[:, 2].min()


def sub_min_z(b):
    return min([head[x.name][2] for x in [b] + list(b.children_recursive)])


def follow(b):
    """single-child chain from b, ending at the first bone with 0 or >= 2 children"""
    out = [b]
    while len(out[-1].children) == 1:
        out.append(out[-1].children[0])
    return out


mapping = {}
names = set(B.keys())
if {"Hips", "LeftUpperArm", "LeftLowerArm"} <= names:      # already humanoid (Tere)
    table = {"Hips": "Hips", "Chest": "Spine", "UpperChest": "Spine2", "Neck": "Neck", "Head": "Head",
             "head_end": "HeadTop_End"}
    for side in ("Left", "Right"):
        table.update({f"{side}Shoulder": f"{side}Shoulder", f"{side}UpperArm": f"{side}Arm",
                      f"{side}LowerArm": f"{side}ForeArm", f"{side}Hand": f"{side}Hand",
                      f"{side}UpperLeg": f"{side}UpLeg", f"{side}LowerLeg": f"{side}Leg",
                      f"{side}Foot": f"{side}Foot", f"{side}Toes": f"{side}ToeBase"})
    mapping.update({k: v for k, v in table.items() if k in names})
    hands = {"Left": B["LeftHand"], "Right": B["RightHand"]}
else:
    # hips: the deepest bone with two children whose subtrees reach the floor
    reach = [b for b in B if head[b.name][2] > floor + 0.35 * H and
             sum(1 for c in b.children if sub_min_z(c) < floor + 0.12 * H) >= 2]
    hips = max(reach, key=lambda b: len(b.parent_recursive))
    mapping[hips.name] = "Hips"
    legs = [c for c in hips.children if sub_min_z(c) < floor + 0.12 * H]
    for side, sgn in (("Left", 1), ("Right", -1)):
        leg = max(legs, key=lambda c: sgn * head[c.name][0])
        ch = follow(leg)
        foot = ch[-1] if len(ch[-1].children) >= 2 else next((x for x in ch if head[x.name][2] < floor + 0.12 * H), ch[-1])
        i = ch.index(foot)
        if i >= 2:
            mapping[ch[i - 2].name] = f"{side}UpLeg"; mapping[ch[i - 1].name] = f"{side}Leg"
        mapping[foot.name] = f"{side}Foot"
        kids = list(foot.children) or ([ch[i + 1]] if i + 1 < len(ch) else [])
        if kids:
            toe = min(kids, key=lambda c: tail[c.name][1])      # forward is -Y
            mapping[toe.name] = f"{side}ToeBase"
    # spine: the child of the hips whose subtree holds the top of the head
    top = max(B, key=lambda b: head[b.name][2])
    spine0 = next(c for c in hips.children if top == c or top in c.children_recursive)
    ch = follow(spine0)
    chest = ch[-1]
    sp = ["Spine", "Spine1", "Spine2"]
    if len(ch) == 1:
        mapping[chest.name] = "Spine2"
    elif len(ch) == 2:
        mapping[ch[0].name] = "Spine"; mapping[chest.name] = "Spine2"
    else:
        mapping[ch[0].name] = "Spine"; mapping[ch[len(ch) // 2].name] = "Spine1"; mapping[chest.name] = "Spine2"
    neck = next((c for c in chest.children if top == c or top in c.children_recursive),
                min(chest.children, key=lambda c: abs(head[c.name][0]) - head[c.name][2]))
    nch = follow(neck)
    for n, b in zip(("Neck", "Head", "HeadTop_End"), nch):
        mapping[b.name] = n
    hands = {}
    for side, sgn in (("Left", 1), ("Right", -1)):
        a0 = max([c for c in chest.children if c != neck], key=lambda c: sgn * head[c.name][0])
        ach = follow(a0)
        hand = ach[-1]
        hands[side] = hand
        mapping[hand.name] = f"{side}Hand"
        rest = ach[:-1]
        # collarbone present when 4+ bones lead to the hand, or when 3 do and the first starts at the spine
        if len(rest) >= 4 or (len(rest) == 3 and abs(head[rest[0].name][0]) < 0.06 * H):
            mapping[rest[0].name] = f"{side}Shoulder"; rest = rest[1:]
        if len(rest) >= 2:
            mapping[rest[0].name] = f"{side}Arm"; mapping[rest[1].name] = f"{side}ForeArm"
        for k, extra in enumerate(rest[2:]):
            mapping[extra.name] = f"{side}WristExtra{k + 1}"     # in-line auto-rig bone, folded into ForeArm later

# fingers: thumb (named, else the chain that starts nearest the wrist), then index..pinky away from the thumb
for side, hand in hands.items():
    roots = []
    for c in hand.children:
        x = c
        while len(x.children) == 1 and len(follow(x)[-1].children) >= 2 and x.children[0].children and len(x.children[0].children) >= 2:
            x = x.children[0]
        if len(follow(c)[-1].children) >= 2:            # a metacarpal carrying several fingers (Jumong)
            roots.extend(follow(c)[-1].children)
        else:
            roots.append(c)
    hh = head[hand.name]
    tips = {r.name: tail[follow(r)[-1].name] for r in roots}
    down = np.mean(list(tips.values()), axis=0) - hh; down /= np.linalg.norm(down)
    named = [r for r in roots if "Thumb" in r.name]
    thumb = named[0] if named else min(roots, key=lambda r: float((head[r.name] - hh) @ down))
    others = [r for r in roots if r != thumb]
    kn = np.mean([head[r.name] for r in others], axis=0)
    t = head[thumb.name] - kn; ax = np.mean([tips[r.name] for r in others], axis=0) - kn; ax /= np.linalg.norm(ax)
    lat = t - (t @ ax) * ax; lat /= np.linalg.norm(lat)
    others.sort(key=lambda r: -float((head[r.name] - kn) @ lat))
    fingers = [("Thumb", thumb)] + list(zip(("Index", "Middle", "Ring", "Pinky"), others))
    if len(others) != 4:
        print(f"WARNING {side} hand: {len(others) + 1} digits")
    for fname, r in fingers:
        for j, b in enumerate(follow(r)):
            mapping[b.name] = f"{side}Hand{fname}{j + 1}" if j < 3 else f"{side}Hand{fname}Tip" if j == 3 else f"{side}Hand{fname}Tip{j - 2}"

# rename (vertex groups follow their bones)
for old, new in mapping.items():
    if old in B:
        B[old].name = P + new
unmapped = [b.name for b in B if not b.name.startswith(P)]
print(f"mapped {len(mapping)} bones; unmapped {len(unmapped)}: {unmapped[:14]}{' ...' if len(unmapped) > 14 else ''}")
need = ["Hips", "Spine", "Spine2", "Neck", "Head"] + [f"{s}{n}" for s in ("Left", "Right") for n in
        ("Arm", "ForeArm", "Hand", "UpLeg", "Leg", "Foot", "HandThumb1", "HandIndex1", "HandMiddle1", "HandRing1", "HandPinky1")]
missing = [n for n in need if P + n not in B]
print("missing core bones:", missing or "none")

# ---- vertices tied only to the exporter's placeholder bone take the weights of the nearest skinned vertex
from mathutils import kdtree
nb = mesh.vertex_groups.get("neutral_bone")
if nb is not None:
    bad = [v.index for v in mesh.data.vertices if any(g.group == nb.index and g.weight > 0 for g in v.groups)]
    good = [v.index for v in mesh.data.vertices if v.groups and not any(g.group == nb.index for g in v.groups)]
    kd = kdtree.KDTree(len(good))
    for k, i in enumerate(good):
        kd.insert(mesh.data.vertices[i].co, k)
    kd.balance()
    for i in bad:
        _, k, _ = kd.find(mesh.data.vertices[i].co)
        src = mesh.data.vertices[good[k]]
        ws = [(g.group, g.weight) for g in src.groups]
        nb.remove([i])
        for g, w in ws:
            mesh.vertex_groups[g].add([i], w, 'REPLACE')
    mesh.vertex_groups.remove(nb)
    if "neutral_bone" in arm.data.bones:
        bpy.context.view_layer.objects.active = arm
        bpy.ops.object.mode_set(mode='EDIT'); arm.data.edit_bones.remove(arm.data.edit_bones["neutral_bone"])
        bpy.ops.object.mode_set(mode='OBJECT')
    print(f"placeholder bone: {len(bad)} vertices given their neighbours' weights")

# ---- whole-body weight refinement
vgs = mesh.vertex_groups
vg_bone = {g.index: g.name for g in vgs}
nv = len(mesh.data.vertices)
W = [{g.group: g.weight for g in v.groups if g.weight > 0.01} for v in mesh.data.vertices]
co = np.array([tuple(v.co) for v in mesh.data.vertices])
nbr = [[] for _ in range(nv)]
for e in mesh.data.edges:
    a, b = e.vertices; nbr[a].append(b); nbr[b].append(a)
pos = {}
for i, p in enumerate(np.round(co / 1e-5).astype(np.int64)):
    pos.setdefault(tuple(p), []).append(i)
for ids in pos.values():
    for i in ids:
        nbr[i].extend(j for j in ids if j != i)
# smooth around the big joints
MWi = mesh.matrix_world.inverted()
cw = np.array([tuple(mesh.matrix_world @ v.co) for v in mesh.data.vertices])
zone = set()
for key, r in JOINT_SMOOTH.items():
    for s in ("Left", "Right", ""):
        n = P + s + key
        if n in B:
            c = np.array(AM @ B[n].head_local)
            zone |= set(np.where(np.linalg.norm(cw - c, axis=1) < r)[0].tolist())
for _ in range(SMOOTH_ITERS):
    new = {}
    for i in zone:
        acc = {}
        for j in nbr[i]:
            for g, val in W[j].items():
                acc[g] = acc.get(g, 0) + val
        n = max(1, len(nbr[i]))
        new[i] = {g: (1 - SMOOTH_F) * W[i].get(g, 0) + SMOOTH_F * acc.get(g, 0) / n for g in set(acc) | set(W[i])}
    for i, w in new.items():
        W[i] = w
# seam twins identical (no cracks), then prune, top 4, normalize
for ids in pos.values():
    if len(ids) < 2:
        continue
    avg = {}
    for i in ids:
        for g, val in W[i].items():
            avg[g] = avg.get(g, 0) + val / len(ids)
    for i in ids:
        W[i] = dict(avg)
empty = 0
for i, v in enumerate(mesh.data.vertices):
    w = {g: val for g, val in W[i].items() if val > 0.01}
    top = dict(sorted(w.items(), key=lambda q: -q[1])[:4])
    tot = sum(top.values())
    for g in list(v.groups):
        vgs[g.group].remove([i])
    if tot <= 0:
        empty += 1
        continue
    for g, val in top.items():
        vgs[g].add([i], val / tot, 'REPLACE')
print(f"weights: {len(zone)} joint vertices smoothed, {nv} normalized (max 4 bones), {empty} with no weight")
bpy.ops.export_scene.gltf(filepath=DST, export_format='GLB', export_animations=True, export_skins=True,
                          export_image_format='AUTO')
print("exported", DST)
