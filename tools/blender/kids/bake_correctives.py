# apex-rig-forge R5 Pose-Space Correctives:
#   - bends each joint to its R2/per-kid limit (with half-angle helper & twist bones active!)
#   - bakes volume-preserving Corrective Smooth deltas back to rest space via inverse blended skinning matrix
#   - detects > 3 mm limb-vs-limb contact penetration at full elbow/knee flex and bakes mid-plane Squash shapes
#   - enforces seam-twin delta parity, > 0.5 mm delta threshold, and <= 10 shapes per kid budget
#   - writes gltf_yup driver metadata into BOTH mesh extra `kalyeah_correctives` AND `<kid>_rig.json`
# usage:
#   blender --background --python bake_correctives.py -- in.glb out.glb [--limits rig__joint_limits.json] [--rig-json kid_rig.json] [--kid narda]
import argparse
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
ap = argparse.ArgumentParser()
ap.add_argument("src")
ap.add_argument("dst")
ap.add_argument("--limits", default="")
ap.add_argument("--rig-json", default="")
ap.add_argument("--kid", default="")
args = ap.parse_args(argv)

P = "mixamorig:"
MIN_DELTA_M = 0.0005      # R5: deltas only where > 0.5 mm
MIN_SHAPE_PEAK_M = 0.0012 # skip shape if joint doesn't need >= 1.2 mm correction
MIN_SHAPE_VERTS = 10
MAX_SHAPES = 10
CONTACT_THRESHOLD_M = 0.003  # R4/R5: > 3 mm penetration triggers squash shape


def to_gltf(v):
    return [round(float(v[0]), 5), round(float(v[2]), 5), round(float(-v[1]), 5)]


def load_limits(path, kid_name):
    if not path or not os.path.isfile(path):
        return {}
    raw = json.load(open(path, encoding="utf-8"))
    data = raw.get("data", raw)
    joints = data.get("joints", data.get("bones", {}))
    overrides = data.get("overrides", {})
    kid_ovr = {}
    for k, v in overrides.items():
        if k.lower() == kid_name.lower():
            kid_ovr = v
            break
    merged = {}
    for jn, jv in joints.items():
        merged[jn] = dict(jv) if isinstance(jv, dict) else jv
    for jn, ov in kid_ovr.items():
        if jn in merged and isinstance(merged[jn], dict) and isinstance(ov, dict):
            merged[jn].update(ov)
    return merged


def limit_flex_max(limits, bone_short, default_deg):
    for key in (bone_short, bone_short.replace("Left", "").replace("Right", "")):
        entry = limits.get(key)
        if isinstance(entry, dict):
            flex = entry.get("flex") or entry.get("flexDeg") or entry.get("flexRange")
            if isinstance(flex, (list, tuple)) and len(flex) == 2:
                return float(flex[1])
            if isinstance(flex, dict) and "max" in flex:
                return float(flex["max"])
    return float(default_deg)


kid_id = (args.kid or os.path.splitext(os.path.basename(args.src))[0]).lower()
limits_data = load_limits(args.limits, kid_id)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.render.fps = 30
bpy.context.scene.render.fps_base = 1.0
bpy.ops.import_scene.gltf(filepath=args.src)
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
saved_action = arm.animation_data.action if arm.animation_data else None
if arm.animation_data:
    arm.animation_data.action = None
arm.data.pose_position = "POSE"

mesh = max((o for o in bpy.data.objects if o.type == "MESH" and o.vertex_groups), key=lambda o: len(o.data.vertices))
AM, MW = arm.matrix_world.copy(), mesh.matrix_world.copy()
AMi, MWi = AM.inverted(), MW.inverted()
pb = arm.pose.bones
me = mesh.data
nv = len(me.vertices)
rest = np.array([tuple(v.co) for v in me.vertices], dtype=float)
restw = np.array([tuple(MW @ v.co) for v in me.vertices], dtype=float)
vg_name = {g.index: g.name for g in mesh.vertex_groups}
weights = [[(vg_name[g.group], g.weight) for g in v.groups if g.weight > 0] for v in me.vertices]

# Welded seam-twin lookup & mesh adjacency
twins = {}
for i, p_key in enumerate(np.round(rest / 1e-5).astype(np.int64)):
    twins.setdefault(tuple(p_key), []).append(i)
twin_of = {}
for ids in twins.values():
    if len(ids) > 1:
        for i in ids:
            twin_of[i] = ids

nbr = [[] for _ in range(nv)]
for e in me.edges:
    a, b = e.vertices
    nbr[a].append(b)
    nbr[b].append(a)
for ids in twins.values():
    if len(ids) > 1:
        for i in ids:
            nbr[i].extend(j for j in ids if j != i)

# Clear any old corr_* shape keys so re-baking is 100% idempotent.
if me.shape_keys is not None:
    for kb in list(me.shape_keys.key_blocks):
        if kb.name.startswith("corr_"):
            mesh.shape_key_remove(kb)
if me.shape_keys is None:
    mesh.shape_key_add(name="Basis", from_mix=False)

zone = mesh.vertex_groups.new(name="__corr_zone__")
cs = mesh.modifiers.new("__corr_smooth__", "CORRECTIVE_SMOOTH")
cs.rest_source = "ORCO"
cs.smooth_type = "LENGTH_WEIGHTED"
cs.vertex_group = zone.name
cs.use_only_smooth = False
cs.show_viewport = False


def reset_pose():
    for b in pb:
        b.rotation_mode = "QUATERNION"
        b.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()


def swing_bone(bone_name, target_world_vec, deg):
    """Swing bone_name so its axis turns toward target_world_vec by deg, and drive its R2 helper bone!"""
    bpy.context.view_layer.update()
    b = pb[bone_name]
    d = (AM @ b.tail - AM @ b.head).normalized()
    pivot = AM @ b.head
    t = Vector(target_world_vec).normalized()
    axis = d.cross(t)
    if axis.length < 1e-6:
        return None
    axis.normalize()
    R = Matrix.Rotation(math.radians(deg), 4, axis)
    b.matrix = AMi @ (Matrix.Translation(pivot) @ R @ Matrix.Translation(-pivot) @ AM @ b.matrix)
    bpy.context.view_layer.update()

    # Drive matching half-angle helper (50% of local swing) so corrective fixes only post-helper residual!
    short = bone_name[len(P):] if bone_name.startswith(P) else bone_name
    helper_map = {
        "LeftForeArm": P + "LeftForeArmHelper",
        "RightForeArm": P + "RightForeArmHelper",
        "LeftLeg": P + "LeftLegHelper",
        "RightLeg": P + "RightLegHelper",
        "LeftHand": P + "LeftHandHelper",
        "RightHand": P + "RightHandHelper",
    }
    hn = helper_map.get(short)
    if hn and hn in pb:
        q_local = b.matrix_basis.to_quaternion()
        pb[hn].rotation_mode = "QUATERNION"
        pb[hn].rotation_quaternion = Quaternion((1, 0, 0, 0)).slerp(q_local, 0.5)
        bpy.context.view_layer.update()
    return axis


def evaluated_mesh_coords():
    dg = bpy.context.evaluated_depsgraph_get()
    ev = mesh.evaluated_get(dg)
    m = ev.to_mesh()
    verts = np.array([tuple(v.co) for v in m.vertices], dtype=float)
    polys = [tuple(p.vertices) for p in m.polygons]
    ev.to_mesh_clear()
    return verts, polys


def skinning_matrices():
    S = {}
    for b in arm.data.bones:
        S[b.name] = (MWi @ AM @ pb[b.name].matrix @ b.matrix_local.inverted() @ AMi @ MW).to_3x3()
    return S


def write_delta_shape(shape_name, idx_list, delta_posed_local):
    """Transform posed mesh-space deltas back to rest space, enforce seam twins & >0.5mm threshold."""
    S = skinning_matrices()
    dr_all = {}
    for i in idx_list:
        dp = Vector(tuple(delta_posed_local[i]))
        if dp.length < 1e-6:
            continue
        M = Matrix(((0, 0, 0), (0, 0, 0), (0, 0, 0)))
        for gnm, w in weights[i]:
            if gnm in S:
                M = M + S[gnm] * w
        try:
            dr = M.inverted() @ dp
        except ValueError:
            continue
        dr_all[int(i)] = np.array(tuple(dr), dtype=float)

    # Seam-twin delta parity
    for i in list(dr_all):
        ids = twin_of.get(i)
        if ids:
            avg = np.mean([dr_all.get(j, np.zeros(3)) for j in ids], axis=0)
            for j in ids:
                dr_all[j] = avg

    # Threshold > 0.5 mm (R5)
    filtered = {i: dr for i, dr in dr_all.items() if float(np.linalg.norm(dr)) > MIN_DELTA_M}
    if len(filtered) < MIN_SHAPE_VERTS:
        return 0, 0.0
    peak = max(float(np.linalg.norm(dr)) for dr in filtered.values())
    if peak < MIN_SHAPE_PEAK_M:
        return 0, peak

    kb = mesh.shape_key_add(name=shape_name, from_mix=False)
    for i, dr in filtered.items():
        kb.data[i].co = Vector(tuple(rest[i] + dr))
    return len(filtered), peak


def detect_contact_squash(parent_bn, child_bn, posed_local, polys):
    """Detect > 3 mm penetration between child and parent limb regions and compute mid-plane push-back."""
    parent_set = {parent_bn, parent_bn.replace("Arm", "ArmTwist")}
    child_set = {
        child_bn,
        child_bn.replace("ForeArm", "ForeArmTwist1"),
        child_bn.replace("ForeArm", "ForeArmTwist2"),
    }
    p_verts, c_verts = set(), set()
    for i in range(nv):
        if not weights[i]:
            continue
        dom = max(weights[i], key=lambda q: q[1])[0]
        if dom in parent_set:
            p_verts.add(i)
        elif dom in child_set:
            c_verts.add(i)
    if len(p_verts) < 20 or len(c_verts) < 20:
        return 0.0, {}, []

    # Build BVH for parent and child faces separately (only core faces well away from the crease line)
    joint_pos = np.array(AM @ arm.data.bones[child_bn].head_local)
    p_faces = [f for f in polys if all(vi in p_verts for vi in f) and float(np.linalg.norm(restw[f[0]] - joint_pos)) > 0.03]
    c_faces = [f for f in polys if all(vi in c_verts for vi in f) and float(np.linalg.norm(restw[f[0]] - joint_pos)) > 0.03]
    if not p_faces or not c_faces:
        return 0.0, {}, []

    verts_vec = [Vector(tuple(v)) for v in posed_local]
    bvh_p = BVHTree.FromPolygons(verts_vec, p_faces)
    bvh_c = BVHTree.FromPolygons(verts_vec, c_faces)

    max_depth = 0.0
    push = {}

    def check_side(src_verts, other_bvh):
        nonlocal max_depth
        for vi in src_verts:
            if float(np.linalg.norm(restw[vi] - joint_pos)) <= 0.035:
                continue
            pt = verts_vec[vi]
            loc, nrm, _, dist = other_bvh.find_nearest(pt, 0.035)
            if loc is None or nrm is None:
                continue
            # Inside other surface if (pt - loc) points opposite to outward surface normal
            signed = (pt - loc).dot(nrm)
            if signed < -0.001:
                depth = -signed
                if depth > max_depth:
                    max_depth = depth
                # Push halfway to contact surface (shared mid-plane, capped <= 7mm so limb core rigidity stays intact)
                mag = min(0.45 * depth, 0.007)
                push[vi] = np.array(tuple(nrm * mag), dtype=float)

    check_side(c_verts, bvh_p)
    check_side(p_verts, bvh_c)
    if max_depth <= CONTACT_THRESHOLD_M or not push:
        return max_depth, {}, []

    # Smooth push deltas over 2 rings of neighbors for organic compressed skin transition
    affected = set(push.keys())
    for _ in range(2):
        ring = set(affected)
        for vi in list(affected):
            ring.update(nbr[vi])
        nxt = {}
        for vi in ring:
            acc = push.get(vi, np.zeros(3)) * 0.5
            wsum = 0.5
            for nb_i in nbr[vi]:
                acc = acc + push.get(nb_i, np.zeros(3)) * (0.5 / max(len(nbr[vi]), 1))
                wsum += 0.5 / max(len(nbr[vi]), 1)
            nxt[vi] = acc / max(wsum, 1e-6)
        push = nxt
        affected = ring

    delta_arr = np.zeros((nv, 3), dtype=float)
    for vi, dv in push.items():
        delta_arr[vi] = dv
    return max_depth, delta_arr, sorted(affected)


# ---- Bake R5 corrective shapes (8 symmetric primaries first, then highest-penetration squash pair <= 10) -----------
meta = []
squash_candidates = []  # (pair_key, max_pen, short, pbn, bn, hinge_gltf, rest_ang, start_deg, posed_ang, squash_idx, squash_delta)
elbow_lim = min(135.0, limit_flex_max(limits_data, "LeftForeArm", 130.0))
knee_lim = min(130.0, limit_flex_max(limits_data, "LeftLeg", 125.0))

# 1) Hinge correctives (elbows & knees) — collect contact squash candidates for after primaries
HINGE_SPECS = [
    ("LeftForeArm", "LeftArm", (0, -1, 0), elbow_lim, 0.068, 5, 0.35, 25.0, "ForeArm"),
    ("RightForeArm", "RightArm", (0, -1, 0), elbow_lim, 0.068, 5, 0.35, 25.0, "ForeArm"),
    ("LeftLeg", "LeftUpLeg", (0, 1, 0), knee_lim, 0.095, 8, 0.45, 25.0, "Leg"),
    ("RightLeg", "RightUpLeg", (0, 1, 0), knee_lim, 0.095, 8, 0.45, 25.0, "Leg"),
]

for short, par_short, target_dir, deg, zone_r, iters, fac, start_deg, pair_key in HINGE_SPECS:
    if len(meta) >= MAX_SHAPES:
        break
    bn, pbn = P + short, P + par_short
    if bn not in pb or pbn not in pb:
        continue
    reset_pose()
    centre = np.array(AM @ arm.data.bones[bn].head_local)
    d = np.linalg.norm(restw - centre, axis=1)
    idx = np.where(d < zone_r)[0]
    zone.remove(list(range(nv)))
    for i in idx:
        zone.add([int(i)], float(1.0 - (d[i] / zone_r) ** 2), "REPLACE")

    cs.iterations, cs.factor = iters, fac
    hinge_axis_world = swing_bone(bn, target_dir, deg)
    if hinge_axis_world is None:
        continue

    cs.show_viewport = False
    posed, polys = evaluated_mesh_coords()
    cs.show_viewport = True
    fixed, _ = evaluated_mesh_coords()
    cs.show_viewport = False

    par_b = arm.data.bones[pbn]
    cur_b = arm.data.bones[bn]
    rest_ang = math.degrees(
        (par_b.tail_local - par_b.head_local).normalized().angle((cur_b.tail_local - cur_b.head_local).normalized())
    )
    posed_ang = math.degrees(
        (pb[pbn].tail - pb[pbn].head).normalized().angle((pb[bn].tail - pb[bn].head).normalized())
    )
    hinge_gltf = to_gltf(hinge_axis_world)

    # Outer-cap volume preservation (restores LBS radial collapse on outer elbow/knee cap without affecting rigid cores!)
    hinge_pos_local = MWi @ (AM @ pb[bn].head)
    par_dir_w = (AM @ pb[pbn].tail - AM @ pb[pbn].head).normalized()
    cur_dir_w = (AM @ pb[bn].tail - AM @ pb[bn].head).normalized()
    # Bisector pointing OUTWARD on the convex outer cap of the bent joint
    outer_w = (par_dir_w - cur_dir_w).normalized()
    outer_local = (MWi.to_3x3() @ outer_w).normalized()
    joint_rest_local = MWi @ (AM @ arm.data.bones[bn].head_local)
    rest_par_dir = (MWi.to_3x3() @ (AM @ arm.data.bones[pbn].tail_local - AM @ arm.data.bones[pbn].head_local)).normalized()
    vol_delta = fixed - posed
    for i in idx:
        r_vec = Vector(tuple(rest[i])) - joint_rest_local
        along = abs(r_vec.dot(rest_par_dir))
        if along > 0.65 * zone_r:
            continue  # stay strictly inside joint zone, NEVER touch rigid limb core!
        r_rest = (r_vec - r_vec.dot(rest_par_dir) * rest_par_dir).length
        p_vec = Vector(tuple(fixed[i])) - hinge_pos_local
        proj_out = p_vec.dot(outer_local)
        if proj_out > 0.005 and r_rest > 0.015:
            # Radial shrink relative to rest radius on the outer cap
            r_cur = p_vec.length
            deficit = max(0.0, r_rest - r_cur)
            if deficit > 0.0005:
                falloff = max(0.0, 1.0 - (along / (0.65 * zone_r)) ** 2)
                push_v = outer_local * (min(deficit * 0.85, 0.018) * falloff)
                vol_delta[i] = vol_delta[i] + np.array(tuple(push_v), dtype=float)

    moved, peak = write_delta_shape(f"corr_{short}", idx, vol_delta)
    if moved > 0:
        meta.append({
            "shape": f"corr_{short}",
            "kind": "hinge",
            "bone": bn,
            "parent": pbn,
            "axis": hinge_gltf,
            "restAngleDeg": round(rest_ang, 2),
            "startAngleDeg": round(start_deg, 2),
            "fullAngleDeg": round(max(posed_ang, start_deg + 10.0), 2),
        })
        print(f"[corr] corr_{short}: {moved} verts, peak={peak*1000:.2f}mm, {start_deg:.0f}->{posed_ang:.1f} deg")
    else:
        print(f"[corr] corr_{short}: skipped (peak={peak*1000:.2f}mm <= threshold)")

    # Record contact penetration > 3 mm as squash candidate (baked symmetrically after the 8 primaries!)
    max_pen, squash_delta, squash_idx = detect_contact_squash(pbn, bn, fixed, polys)
    if max_pen > CONTACT_THRESHOLD_M and squash_idx:
        # Capture inverse-mapped rest deltas immediately while pose matrices are live!
        S_live = skinning_matrices()
        squash_candidates.append(
            (pair_key, max_pen, short, pbn, bn, hinge_gltf, rest_ang, start_deg, posed_ang, squash_idx, squash_delta, S_live)
        )

# 2) Shoulder ball-joint cone correctives (_Up and _Fwd) — always complete L/R pairs before squash shapes
CONE_SPECS = [
    ("LeftArm", "Up", (0, 0, 1), 85.0, 0.11, 10, 0.50, 20.0),
    ("RightArm", "Up", (0, 0, 1), 85.0, 0.11, 10, 0.50, 20.0),
    ("LeftArm", "Fwd", (0, -1, 0), 85.0, 0.11, 8, 0.45, 20.0),
    ("RightArm", "Fwd", (0, -1, 0), 85.0, 0.11, 8, 0.45, 20.0),
]

for short, tag, target_dir, deg, zone_r, iters, fac, start_deg in CONE_SPECS:
    if len(meta) >= MAX_SHAPES:
        break
    bn = P + short
    if bn not in pb:
        continue
    pbn = arm.data.bones[bn].parent.name if arm.data.bones[bn].parent else P + "Spine2"
    reset_pose()
    centre = np.array(AM @ arm.data.bones[bn].head_local)
    rest_dir = (AM @ arm.data.bones[bn].tail_local - AM @ arm.data.bones[bn].head_local).normalized()
    d = np.linalg.norm(restw - centre, axis=1)
    idx = np.where(d < zone_r)[0]
    zone.remove(list(range(nv)))
    for i in idx:
        zone.add([int(i)], float(1.0 - (d[i] / zone_r) ** 2), "REPLACE")

    cs.iterations, cs.factor = iters, fac
    axis = swing_bone(bn, target_dir, deg)
    if axis is None:
        continue
    cs.show_viewport = False
    posed, _ = evaluated_mesh_coords()
    cs.show_viewport = True
    fixed, _ = evaluated_mesh_coords()
    cs.show_viewport = False

    shape_name = f"corr_{short}_{tag}"
    moved, peak = write_delta_shape(shape_name, idx, fixed - posed)
    if moved > 0:
        meta.append({
            "shape": shape_name,
            "kind": "cone",
            "bone": bn,
            "parent": pbn,
            "targetDir": to_gltf(Vector(target_dir).normalized()),
            "boneRestDir": to_gltf(rest_dir),
            "startAngleDeg": round(start_deg, 2),
            "fullAngleDeg": round(deg, 2),
        })
        print(f"[corr] {shape_name}: {moved} verts, peak={peak*1000:.2f}mm, {start_deg:.0f}->{deg:.0f} deg")
    else:
        print(f"[corr] {shape_name}: skipped (peak={peak*1000:.2f}mm <= threshold)")

# 3) Symmetric contact squash pairs (fill remaining slots <= MAX_SHAPES, prioritizing highest penetration pair)
pairs_by_key = {}
for cand in squash_candidates:
    pairs_by_key.setdefault(cand[0], []).append(cand)
ranked_pairs = sorted(pairs_by_key.values(), key=lambda grp: -max(c[1] for c in grp))
for grp in ranked_pairs:
    if len(meta) + len(grp) > MAX_SHAPES:
        continue
    for _, max_pen, short, pbn, bn, hinge_gltf, rest_ang, start_deg, posed_ang, squash_idx, squash_delta, S_live in grp:
        # Transform posed squash deltas using captured live skinning matrices
        dr_all = {}
        for i in squash_idx:
            dp = Vector(tuple(squash_delta[i]))
            if dp.length < 1e-6:
                continue
            M = Matrix(((0, 0, 0), (0, 0, 0), (0, 0, 0)))
            for gnm, w in weights[i]:
                if gnm in S_live:
                    M = M + S_live[gnm] * w
            try:
                dr = M.inverted() @ dp
            except ValueError:
                continue
            dr_all[int(i)] = np.array(tuple(dr), dtype=float)
        for i in list(dr_all):
            ids = twin_of.get(i)
            if ids:
                avg = np.mean([dr_all.get(j, np.zeros(3)) for j in ids], axis=0)
                for j in ids:
                    dr_all[j] = avg
        filtered = {i: dr for i, dr in dr_all.items() if float(np.linalg.norm(dr)) > MIN_DELTA_M}
        if len(filtered) < MIN_SHAPE_VERTS:
            continue
        speak = max(float(np.linalg.norm(dr)) for dr in filtered.values())
        if speak < MIN_SHAPE_PEAK_M:
            continue
        kb = mesh.shape_key_add(name=f"corr_{short}_Squash", from_mix=False)
        for i, dr in filtered.items():
            kb.data[i].co = Vector(tuple(rest[i] + dr))
        meta.append({
            "shape": f"corr_{short}_Squash",
            "kind": "squash",
            "bone": bn,
            "parent": pbn,
            "axis": hinge_gltf,
            "restAngleDeg": round(rest_ang, 2),
            "startAngleDeg": round(max(start_deg, posed_ang - 35.0), 2),
            "fullAngleDeg": round(posed_ang, 2),
        })
        print(f"[corr] corr_{short}_Squash: pen={max_pen*1000:.1f}mm -> {len(filtered)} verts, peak={speak*1000:.2f}mm")

# Restore rest pose & remove temporary bake helpers
reset_pose()
mesh.modifiers.remove(cs)
mesh.vertex_groups.remove(zone)
if arm.animation_data and saved_action is not None:
    arm.animation_data.action = saved_action

me["kalyeah_correctives"] = json.dumps(meta)

# Mirror correctives metadata into <kid>_rig.json if provided (so Unity reads it reliably without glTFast extras hooks)
if args.rig_json and os.path.isfile(args.rig_json):
    rig = json.load(open(args.rig_json, encoding="utf-8"))
    rig["correctives"] = meta
    with open(args.rig_json, "w", encoding="utf-8", newline="\n") as f:
        json.dump(rig, f, indent=2)
        f.write("\n")

for o in bpy.context.view_layer.objects:
    o.select_set(o in (arm, mesh))
bpy.ops.export_scene.gltf(
    filepath=args.dst, export_format="GLB", use_selection=True, export_yup=True,
    export_animations=True, export_animation_mode="ACTIONS",
    export_force_sampling=False, export_bake_animation=False,
    export_skins=True, export_all_influences=False, export_def_bones=False,
    export_morph=True, export_extras=True, export_image_format="AUTO",
    export_texcoords=True, export_normals=True, export_tangents=True,
)
print(f"[corr] exported {args.dst}: {len(meta)} corrective shapes (<= {MAX_SHAPES})")
