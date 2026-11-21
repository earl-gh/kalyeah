# Rig normalization for one kid (apex-rig-forge R0/R2/R3 prep):
#   - applies non-1 object scale/rotation (e.g. Tere 0.01 cm rig)
#   - renames skeleton to shared R2 contract names (mixamorig:*), inserting missing Spine1 / _End / Tip markers
#   - strips neutral_bone, folds _End/Tip weights into parents (use_deform=False)
#   - cleans and consolidates hair/skirt/cape leaf chains to <= 4 chains x <= 4 bones and <= 75 total deforming bones
#   - removes orphan vertex groups, fixes unweighted vertices, prunes cross-side limb leaks,
#     smooths joint zones, enforces seam-twin parity, prunes < 0.01, caps at top 4, normalizes.
# usage:
#   blender --background --python normalize_kid.py -- in.glb out.glb
#   python normalize_kid.py in.glb out.glb
import sys
import bpy
import numpy as np
from mathutils import Vector, kdtree

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
SRC, DST = argv[0], argv[1]
P = "mixamorig:"
JOINT_SMOOTH = {
    "ForeArm": 0.055, "Arm": 0.065, "Hand": 0.04,
    "Leg": 0.06, "UpLeg": 0.07, "Spine1": 0.06, "Neck": 0.045,
}
SMOOTH_ITERS, SMOOTH_F = 2, 0.35
MAX_EXTRA_CHAINS = 4
MAX_CHAIN_BONES = 4
MAX_DEFORM_BONES = 75
MIN_CLOTH_CHAIN_LEN = 0.11  # fold tiny <11 cm eye/ear/strap nubs into parent body bone

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


def set_mode(obj, mode):
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    if mode != "OBJECT":
        bpy.ops.object.mode_set(mode=mode)


# Apply non-unit scale / rotation early so all coordinates and bone lengths are in true metres.
if any(abs(s - 1.0) > 1e-4 for s in arm.matrix_world.to_scale()) or any(abs(s - 1.0) > 1e-4 for s in mesh.matrix_world.to_scale()):
    set_mode(arm, "OBJECT")
    for o in bpy.context.view_layer.objects:
        o.select_set(o in (arm, mesh))
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bpy.context.view_layer.update()
    print("[norm] applied object scale/rotation")

AM = arm.matrix_world.copy()
B = arm.data.bones
head = {b.name: np.array(AM @ b.head_local) for b in B}
tail = {b.name: np.array(AM @ b.tail_local) for b in B}
mz = np.array([tuple(mesh.matrix_world @ v.co) for v in mesh.data.vertices])
H = float(mz[:, 2].max() - mz[:, 2].min())
floor = float(mz[:, 2].min())


def sub_min_z(b):
    return min(head[x.name][2] for x in [b] + list(b.children_recursive))


def follow(b):
    """Single-child chain from b, ending at the first bone with 0 or >= 2 children."""
    out = [b]
    while len(out[-1].children) == 1:
        out.append(out[-1].children[0])
    return out


def fold_weight(src_name, dst_name):
    vgs = mesh.vertex_groups
    src = vgs.get(src_name)
    if src is None or src_name == dst_name:
        return
    dst = vgs.get(dst_name) or vgs.new(name=dst_name)
    for v in mesh.data.vertices:
        for g in v.groups:
            if g.group == src.index and g.weight > 0:
                dst.add([v.index], g.weight, "ADD")
    vgs.remove(src)


# ---- 1. Map humanoid bones to mixamorig:* --------------------------------------------------------------------------
mapping = {}
names = set(B.keys())
if {"Hips", "LeftUpperArm", "LeftLowerArm"} <= names:
    table = {
        "Hips": "Hips", "Chest": "Spine", "UpperChest": "Spine2",
        "Neck": "Neck", "Head": "Head", "head_end": "HeadTop_End",
    }
    for side in ("Left", "Right"):
        table.update({
            f"{side}Shoulder": f"{side}Shoulder",
            f"{side}UpperArm": f"{side}Arm",
            f"{side}LowerArm": f"{side}ForeArm",
            f"{side}Hand": f"{side}Hand",
            f"{side}UpperLeg": f"{side}UpLeg",
            f"{side}LowerLeg": f"{side}Leg",
            f"{side}Foot": f"{side}Foot",
            f"{side}Toes": f"{side}ToeBase",
        })
    mapping.update({k: v for k, v in table.items() if k in names})
    hands = {"Left": B["LeftHand"], "Right": B["RightHand"]}
else:
    reach = [
        b for b in B
        if head[b.name][2] > floor + 0.35 * H
        and sum(1 for c in b.children if sub_min_z(c) < floor + 0.12 * H) >= 2
    ]
    hips = max(reach, key=lambda b: len(b.parent_recursive))
    mapping[hips.name] = "Hips"

    legs = [c for c in hips.children if sub_min_z(c) < floor + 0.12 * H]
    for side, sgn in (("Left", 1), ("Right", -1)):
        leg = max(legs, key=lambda c: sgn * head[c.name][0])
        ch = follow(leg)
        foot = ch[-1] if len(ch[-1].children) >= 2 else next(
            (x for x in ch if head[x.name][2] < floor + 0.12 * H), ch[-1]
        )
        i = ch.index(foot)
        if i >= 2:
            mapping[ch[i - 2].name] = f"{side}UpLeg"
            mapping[ch[i - 1].name] = f"{side}Leg"
        mapping[foot.name] = f"{side}Foot"
        kids = list(foot.children) or ([ch[i + 1]] if i + 1 < len(ch) else [])
        if kids:
            toe = min(kids, key=lambda c: tail[c.name][1])  # forward is -Y
            mapping[toe.name] = f"{side}ToeBase"
            if toe.children:
                toe_end = min(toe.children, key=lambda c: tail[c.name][1])
                mapping[toe_end.name] = f"{side}Toe_End"

    top = max(B, key=lambda b: head[b.name][2])
    spine0 = next(c for c in hips.children if top == c or top in c.children_recursive)
    ch = follow(spine0)
    chest = ch[-1]
    if len(ch) == 1:
        mapping[chest.name] = "Spine2"
    elif len(ch) == 2:
        mapping[ch[0].name] = "Spine"
        mapping[chest.name] = "Spine2"
    else:
        mapping[ch[0].name] = "Spine"
        mapping[ch[len(ch) // 2].name] = "Spine1"
        mapping[chest.name] = "Spine2"

    neck = next(
        (c for c in chest.children if top == c or top in c.children_recursive),
        min(chest.children, key=lambda c: abs(head[c.name][0]) - head[c.name][2]),
    )
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
        if len(rest) >= 4 or (len(rest) == 3 and abs(head[rest[0].name][0]) < 0.06 * H):
            mapping[rest[0].name] = f"{side}Shoulder"
            rest = rest[1:]
        if len(rest) >= 2:
            mapping[rest[0].name] = f"{side}Arm"
            mapping[rest[1].name] = f"{side}ForeArm"
        for k, extra in enumerate(rest[2:]):
            mapping[extra.name] = f"{side}WristExtra{k + 1}"

# Fingers: thumb + Index..Pinky away from thumb
extra_tip_folds = []
for side, hand in hands.items():
    roots = []
    for c in hand.children:
        if len(follow(c)[-1].children) >= 2:
            roots.extend(follow(c)[-1].children)
        else:
            roots.append(c)
    hh = head[hand.name]
    tips = {r.name: tail[follow(r)[-1].name] for r in roots}
    down = np.mean(list(tips.values()), axis=0) - hh
    down /= max(np.linalg.norm(down), 1e-8)
    named = [r for r in roots if "Thumb" in r.name]
    thumb = named[0] if named else min(roots, key=lambda r: float((head[r.name] - hh) @ down))
    others = [r for r in roots if r != thumb]
    kn = np.mean([head[r.name] for r in others], axis=0)
    t = head[thumb.name] - kn
    ax = np.mean([tips[r.name] for r in others], axis=0) - kn
    ax /= max(np.linalg.norm(ax), 1e-8)
    lat = t - (t @ ax) * ax
    lat /= max(np.linalg.norm(lat), 1e-8)
    others.sort(key=lambda r: -float((head[r.name] - kn) @ lat))
    fingers = [("Thumb", thumb)] + list(zip(("Index", "Middle", "Ring", "Pinky"), others))
    if len(others) != 4:
        print(f"[norm] WARNING {side} hand: {len(others) + 1} digits")
    for fname, r in fingers:
        fch = follow(r)
        for j, b in enumerate(fch):
            if j < 3:
                mapping[b.name] = f"{side}Hand{fname}{j + 1}"
            elif j == 3:
                mapping[b.name] = f"{side}Hand{fname}Tip"
            else:
                mapping[b.name] = f"{side}Hand{fname}TipExtra{j - 2}"
                extra_tip_folds.append((P + mapping[b.name], P + f"{side}Hand{fname}3"))

for old, new in mapping.items():
    if old in B:
        B[old].name = P + new

# ---- 2. Remove neutral_bone (replace with nearest skinned vertex weights) ------------------------------------------
nb = mesh.vertex_groups.get("neutral_bone")
if nb is not None:
    bad = [v.index for v in mesh.data.vertices if any(g.group == nb.index and g.weight > 0 for g in v.groups)]
    good = [v.index for v in mesh.data.vertices if v.groups and not any(g.group == nb.index for g in v.groups)]
    if good and bad:
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
                mesh.vertex_groups[g].add([i], w, "REPLACE")
    mesh.vertex_groups.remove(nb)
    print(f"[norm] neutral_bone: reassigned {len(bad)} verts")

if "neutral_bone" in arm.data.bones:
    set_mode(arm, "EDIT")
    arm.data.edit_bones.remove(arm.data.edit_bones["neutral_bone"])
    set_mode(arm, "OBJECT")

# ---- 3. Ensure complete R2 contract hierarchy (Spine1, HeadTop_End, Toe_End, Hand*Tip) -----------------------------
# Fold TipExtra bones (> 4 finger bones, e.g. Jumong) into Hand*3 and delete them.
for src_bn, dst_bn in extra_tip_folds:
    fold_weight(src_bn, dst_bn)

set_mode(arm, "EDIT")
eb = arm.data.edit_bones
for src_bn, _ in extra_tip_folds:
    if src_bn in eb:
        eb.remove(eb[src_bn])

# Insert Spine1 if Spine and Spine2 exist without Spine1 (e.g. Tere).
inserted_spine1 = False
if P + "Spine" in eb and P + "Spine2" in eb and P + "Spine1" not in eb:
    sp0, sp2 = eb[P + "Spine"], eb[P + "Spine2"]
    mid = sp0.head.lerp(sp2.head, 0.5)
    sp1 = eb.new(P + "Spine1")
    sp1.head = mid
    sp1.tail = sp2.head.copy()
    sp1.roll = sp0.roll
    sp1.parent = sp0
    sp1.use_connect = False
    sp0.tail = mid.copy()
    sp2.use_connect = False
    sp2.parent = sp1
    inserted_spine1 = True
    print("[norm] inserted missing mixamorig:Spine1 between Spine and Spine2")

# Reparent anything hanging off HeadTop_End under Head so HeadTop_End stays a clean leaf.
if P + "HeadTop_End" in eb and P + "Head" in eb:
    for c in list(eb[P + "HeadTop_End"].children):
        c.use_connect = False
        c.parent = eb[P + "Head"]

# Create missing non-deforming leaf markers (_End / Hand*Tip).
if P + "Head" in eb and P + "HeadTop_End" not in eb:
    h = eb[P + "Head"]
    he = eb.new(P + "HeadTop_End")
    he.head = h.tail.copy()
    he.tail = h.tail + (h.tail - h.head).normalized() * 0.04
    he.parent = h
    he.use_deform = False

for side in ("Left", "Right"):
    tb_name, te_name = P + f"{side}ToeBase", P + f"{side}Toe_End"
    if tb_name in eb and te_name not in eb:
        tb = eb[tb_name]
        te = eb.new(te_name)
        te.head = tb.tail.copy()
        te.tail = tb.tail + (tb.tail - tb.head).normalized() * 0.03
        te.parent = tb
        te.use_deform = False
    for fn in ("Thumb", "Index", "Middle", "Ring", "Pinky"):
        p3_name, tip_name = P + f"{side}Hand{fn}3", P + f"{side}Hand{fn}Tip"
        if p3_name in eb and tip_name not in eb:
            p3 = eb[p3_name]
            tp = eb.new(tip_name)
            tp.head = p3.tail.copy()
            tp.tail = p3.tail + (p3.tail - p3.head).normalized() * 0.012
            tp.parent = p3
            tp.use_deform = False

set_mode(arm, "OBJECT")

if inserted_spine1:
    # Split Spine weights smoothly between Spine (lower half) and Spine1 (upper half).
    vg_sp = mesh.vertex_groups.get(P + "Spine")
    vg_sp1 = mesh.vertex_groups.get(P + "Spine1") or mesh.vertex_groups.new(name=P + "Spine1")
    if vg_sp is not None:
        z0 = (arm.matrix_world @ arm.data.bones[P + "Spine"].head_local).z
        z1 = (arm.matrix_world @ arm.data.bones[P + "Spine1"].head_local).z
        z2 = (arm.matrix_world @ arm.data.bones[P + "Spine2"].head_local).z
        span = max(z2 - z0, 1e-4)
        for v in mesh.data.vertices:
            w_sp = 0.0
            for g in v.groups:
                if g.group == vg_sp.index:
                    w_sp = g.weight
                    break
            if w_sp <= 0:
                continue
            vz = (mesh.matrix_world @ v.co).z
            t = float(np.clip((vz - (z0 + 0.25 * span)) / (0.5 * span), 0.0, 1.0))
            s = t * t * (3.0 - 2.0 * t)
            if s > 0.01:
                vg_sp.add([v.index], w_sp * (1.0 - s), "REPLACE")
                vg_sp1.add([v.index], w_sp * s, "ADD")

# Fold weights on all _End and Hand*Tip bones into their parent and mark use_deform=False (R2: _End carry no weights).
end_bones = [
    b.name for b in arm.data.bones
    if b.name.startswith(P) and (b.name.endswith("_End") or b.name.endswith("Tip"))
]
for bn in end_bones:
    b = arm.data.bones[bn]
    if b.parent is not None:
        fold_weight(bn, b.parent.name)
    b.use_deform = False

# ---- 4. Clean & consolidate extra leaf chains (R2: <= 4 chains x <= 4 bones, <= 75 total deforming bones) ---------
def has_contract_descendant(b):
    return any(c.name.startswith(P) and "WristExtra" not in c.name for c in b.children_recursive)


# Find extra leaf subtrees (parent is a contract bone, subtree contains no contract bones).
extra_roots = []
for b in arm.data.bones:
    if b.name.startswith(P):
        continue
    if has_contract_descendant(b):
        continue  # path-breaker above Hips or metacarpal -> handled by contract_kid.py
    if b.parent is not None and b.parent.name.startswith(P):
        extra_roots.append(b)

ALLOWED_CLOTH_PARENTS = {P + "Head", P + "Neck", P + "Spine2", P + "Spine1", P + "Spine", P + "Hips"}
to_remove = []
surviving_chains = []  # list of (parent_name, kind, chain_bones_names, length_m, weight_mass, root_x)

# Precompute vertex weight sum per bone name to rank chains cleanly.
vg_id_to_name = {g.index: g.name for g in mesh.vertex_groups}
bone_mass = {}
for v in mesh.data.vertices:
    for g in v.groups:
        nm = vg_id_to_name.get(g.group)
        if nm:
            bone_mass[nm] = bone_mass.get(nm, 0.0) + g.weight

AM = arm.matrix_world.copy()
for root in extra_roots:
    pname = root.parent.name
    # Fold any sub-branches into the main linear path of this subtree first.
    all_sub = [root] + list(root.children_recursive)
    # Linearize longest path from root
    def longest_path(b):
        if not b.children:
            return [b]
        best = max((longest_path(c) for c in b.children), key=len)
        return [b] + best

    chain = longest_path(root)
    chain_set = set(chain)
    side_extras = [x for x in all_sub if x not in chain_set]
    total_len = sum((AM @ x.tail_local - AM @ x.head_local).length for x in chain)
    mass = sum(bone_mass.get(x.name, 0.0) for x in all_sub)
    rx = float((AM @ root.head_local).x)

    # Fold non-cloth attachments (feet/toes/limbs), headfront, or tiny <11 cm nubs directly into parent.
    is_tiny_or_stray = (
        pname not in ALLOWED_CLOTH_PARENTS
        or root.name.lower() == "headfront"
        or (pname in (P + "Head", P + "Neck") and total_len < MIN_CLOTH_CHAIN_LEN)
        or total_len < 0.06
        or mass < 1.0
    )
    if is_tiny_or_stray:
        dst = pname
        if dst.endswith("_End") and arm.data.bones[dst].parent:
            dst = arm.data.bones[dst].parent.name
        for x in all_sub:
            fold_weight(x.name, dst)
            to_remove.append(x.name)
        continue

    # Fold any side twigs on this chain into their closest main-chain ancestor.
    for x in side_extras:
        anc = x.parent
        while anc is not None and anc not in chain_set:
            anc = anc.parent
        fold_weight(x.name, anc.name if anc else root.name)
        to_remove.append(x.name)

    kind = "Hair" if pname in (P + "Head", P + "Neck") else ("Cape" if "Spine" in pname else "Skirt")
    surviving_chains.append([pname, kind, [x.name for x in chain], total_len, mass, rx])

# Cap to <= MAX_EXTRA_CHAINS (4): drop lowest-mass redundant chain (folding into parent / neighbor).
if len(surviving_chains) > MAX_EXTRA_CHAINS:
    surviving_chains.sort(key=lambda c: -(c[4] * c[3]))
    kept = surviving_chains[:MAX_EXTRA_CHAINS]
    dropped = surviving_chains[MAX_EXTRA_CHAINS:]
    for pname, kind, cnames, _, _, rx in dropped:
        # Fold dropped skirt/hair chain into nearest kept chain of same parent, else parent body bone.
        same = [k for k in kept if k[0] == pname]
        dst = min(same, key=lambda k: abs(k[5] - rx))[2][0] if same else pname
        for bn in cnames:
            fold_weight(bn, dst)
            to_remove.append(bn)
    surviving_chains = kept

# Consolidate any chain longer than MAX_CHAIN_BONES (4) down to 4 evenly spaced bones.
# Also compute remaining extra-bone budget against MAX_DEFORM_BONES (64 core + twist/helpers = 64 -> max 11 extras).
CORE_DEFORM_TARGET = 64  # includes the 12 twist/helper bones contract_kid.py will add
max_extra_total = max(0, MAX_DEFORM_BONES - CORE_DEFORM_TARGET)

chain_edit_specs = []  # (kept_names, new_heads, new_tails, old_to_remove)
for idx, item in enumerate(surviving_chains):
    pname, kind, cnames, total_len, mass, rx = item
    if len(cnames) > MAX_CHAIN_BONES:
        # Sample MAX_CHAIN_BONES + 1 points along the original polyline
        pts = [arm.data.bones[cnames[0]].head_local.copy()] + [arm.data.bones[bn].tail_local.copy() for bn in cnames]
        cum = [0.0]
        for a, b in zip(pts[:-1], pts[1:]):
            cum.append(cum[-1] + (b - a).length)
        L = max(cum[-1], 1e-6)

        def sample_pt(u):
            d = u * L
            for k in range(len(cum) - 1):
                if cum[k] <= d <= cum[k + 1] or k == len(cum) - 2:
                    f = (d - cum[k]) / max(cum[k + 1] - cum[k], 1e-8)
                    return pts[k].lerp(pts[k + 1], float(np.clip(f, 0.0, 1.0)))
            return pts[-1].copy()

        kept_names = cnames[:MAX_CHAIN_BONES]
        drop_names = cnames[MAX_CHAIN_BONES:]
        # Map each original bone midpoint to its nearest kept segment index [0..MAX_CHAIN_BONES-1]
        for oi, obn in enumerate(cnames):
            um = 0.5 * (cum[oi] + cum[oi + 1]) / L
            ki = min(MAX_CHAIN_BONES - 1, int(um * MAX_CHAIN_BONES))
            if obn != kept_names[ki]:
                fold_weight(obn, kept_names[ki])
        new_pts = [sample_pt(k / MAX_CHAIN_BONES) for k in range(MAX_CHAIN_BONES + 1)]
        chain_edit_specs.append((kept_names, new_pts[:-1], new_pts[1:]))
        to_remove.extend(drop_names)
        item[2] = kept_names

# Enforce total extra deforming bones <= max_extra_total (11) by trimming tips of longest chains.
while sum(len(item[2]) for item in surviving_chains) > max_extra_total:
    longest = max(surviving_chains, key=lambda c: len(c[2]))
    if len(longest[2]) <= 1:
        break
    tip_bn = longest[2].pop()
    fold_weight(tip_bn, longest[2][-1])
    to_remove.append(tip_bn)

# Apply bone removals & chain consolidations in Edit mode.
set_mode(arm, "EDIT")
eb = arm.data.edit_bones
for kept_names, new_heads, new_tails in chain_edit_specs:
    for bn, nh, nt in zip(kept_names, new_heads, new_tails):
        if bn in eb:
            eb[bn].use_connect = False
            eb[bn].head = nh
            eb[bn].tail = nt
for bn in set(to_remove):
    if bn in eb:
        eb.remove(eb[bn])
set_mode(arm, "OBJECT")

# Rename surviving extra chains cleanly so contract_kid.py and <kid>_rig.json know their kind & side.
kind_counts = {}
for pname, kind, cnames, total_len, mass, rx in surviving_chains:
    side_tag = "L" if rx > 0.025 else ("R" if rx < -0.025 else "C")
    key = f"{kind}_{side_tag}"
    kind_counts[key] = kind_counts.get(key, 0) + 1
    suffix = f"{kind_counts[key]}" if kind_counts[key] > 1 else ""
    base = f"{key}{suffix}"
    for j, old_bn in enumerate(cnames):
        if old_bn in arm.data.bones:
            arm.data.bones[old_bn].name = f"{base}_{j + 1}"

extra_deform = [b.name for b in arm.data.bones if not b.name.startswith(P) and b.use_deform]
print(f"[norm] surviving extra chains: {len(surviving_chains)} ({len(extra_deform)} bones): {extra_deform}")

# ---- 5. Whole-body weight cleanup (no orphans, no unweighted, no cross-side leaks, seam twins, top 4) --------------
valid_deform = {b.name for b in arm.data.bones if b.use_deform}
vgs = mesh.vertex_groups
for g in list(vgs):
    if g.name not in valid_deform:
        vgs.remove(g)

vg_name = {g.index: g.name for g in vgs}
nv = len(mesh.data.vertices)
co = np.array([tuple(v.co) for v in mesh.data.vertices])
cw = np.array([tuple(mesh.matrix_world @ v.co) for v in mesh.data.vertices])
W = [{g.group: g.weight for g in v.groups if g.weight > 0.01} for v in mesh.data.vertices]

# Prune cross-side weights beyond |x| > 0.028 m (including Shoulder, limbs, digits, skirt sides).
left_gids = {g.index for g in vgs if g.name.startswith(P + "Left") or "_L" in g.name}
right_gids = {g.index for g in vgs if g.name.startswith(P + "Right") or "_R" in g.name}
cross_pruned = 0
for i in range(nv):
    if len(W[i]) <= 1:
        continue
    x = cw[i, 0]
    bad_set = right_gids if x > 0.028 else (left_gids if x < -0.028 else None)
    if bad_set:
        for g in list(W[i]):
            if g in bad_set and len(W[i]) > 1:
                del W[i][g]
                cross_pruned += 1

# Reassign any unweighted vertices via KDTree from nearest skinned vertex.
unweighted = [i for i in range(nv) if not W[i]]
if unweighted:
    good = [i for i in range(nv) if W[i]]
    kd = kdtree.KDTree(len(good))
    for k, i in enumerate(good):
        kd.insert(mesh.data.vertices[i].co, k)
    kd.balance()
    for i in unweighted:
        _, k, _ = kd.find(mesh.data.vertices[i].co)
        W[i] = dict(W[good[k]])
    print(f"[norm] reassigned {len(unweighted)} unweighted verts via KDTree")

# Welded adjacency + localized joint smoothing.
nbr = [[] for _ in range(nv)]
for e in mesh.data.edges:
    a, b = e.vertices
    nbr[a].append(b)
    nbr[b].append(a)
pos = {}
for i, p in enumerate(np.round(co / 1e-5).astype(np.int64)):
    pos.setdefault(tuple(p), []).append(i)
for ids in pos.values():
    for i in ids:
        nbr[i].extend(j for j in ids if j != i)

AM = arm.matrix_world.copy()
zone = set()
for key, r in JOINT_SMOOTH.items():
    for s in ("Left", "Right", ""):
        n = P + s + key
        if n in arm.data.bones:
            c = np.array(AM @ arm.data.bones[n].head_local)
            zone |= set(np.where(np.linalg.norm(cw - c, axis=1) < r)[0].tolist())

for _ in range(SMOOTH_ITERS):
    new = {}
    for i in zone:
        acc = {}
        for j in nbr[i]:
            for g, val in W[j].items():
                acc[g] = acc.get(g, 0.0) + val
        n = max(1, len(nbr[i]))
        new[i] = {
            g: (1.0 - SMOOTH_F) * W[i].get(g, 0.0) + SMOOTH_F * acc.get(g, 0.0) / n
            for g in set(acc) | set(W[i])
        }
    for i, w in new.items():
        W[i] = w

# Seam twins identical, prune < 0.01, top 4, normalize.
for ids in pos.values():
    if len(ids) < 2:
        continue
    avg = {}
    for i in ids:
        for g, val in W[i].items():
            avg[g] = avg.get(g, 0.0) + val / len(ids)
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
        vgs[g].add([i], val / tot, "REPLACE")

deform_count = sum(1 for b in arm.data.bones if b.use_deform)
print(
    f"[norm] bones={len(arm.data.bones)} (deform={deform_count}), "
    f"smoothed={len(zone)}, cross_pruned={cross_pruned}, unweighted={empty}"
)
bpy.ops.export_scene.gltf(
    filepath=DST, export_format="GLB", export_yup=True,
    export_animations=False, export_skins=True, export_def_bones=False,
    export_image_format="AUTO",
)
print("[norm] exported", DST)
