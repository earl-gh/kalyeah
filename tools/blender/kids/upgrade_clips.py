# Deterministic Blender 5.2 AAA Gameplay Animation Upgrade Pipeline (apex-anim-craft A2 + A3 + A5).
#
# Upgrades the 20 existing shared clips in tools/blender/kids/clips/roster_clips.glb without
# inventing new hero clips or changing clip names:
#   1. Clean (30 fps resample, quaternion continuity, zero scale curves, loop seam lock, in-place root)
#   2. Retime for snap (Fortnite holds/anticipation while locking CharacterDef cast release frames)
#   3. Exaggerate & line of action (additive spine/arm/leg arcs, zero non-uniform bone scale)
#   4. Overlap (1-3 frame phase lag on Spine1/Spine2/Neck/Head behind Hips)
#   5. Analytical 2-bone IK (foot-lock <= 2 cm slide + toe-roll, zero sideways elbows/knees)
#   6. Swing-twist limit clamp (rig__joint_limits.json) + A5 capsule penetration pass (+-2f smoothed)
#   7. Bake R2 half-angle helpers & twist bones (ForeArmHelper/LegHelper/HandHelper, ArmTwist/ForeArmTwist1/2)
#   8. Keyframe reduction (0.5 deg / 0.5 mm), GLB export, and unity/Data/anim__clip_metrics.json generation.
#
# Usage:
#   blender --background --python tools/blender/kids/upgrade_clips.py -- \
#     [--src <in.glb>] [--dst <out.glb>] [--limits unity/Data/rig__joint_limits.json] \
#     [--metrics unity/Data/anim__clip_metrics.json] [--report tools/blender/kids/clips/roster_clips_report.json] \
#     [--chars-dir assets/models/characters] [--data-dir unity/Data]

import sys
import os
import math
import json
import glob
import argparse
import bpy
from mathutils import Vector, Matrix, Quaternion

P = "mixamorig:"
FPS = 30
ARM_DROP_DEG = 40.0

# Exact clip metadata matching apex-anim-craft A1 & A6 crossfade table.
CLIP_SPECS = {
    "Idle":                 dict(loop=True,  archetype="idle",       crossfade=0.15, mask=False, sync="none", mode="planted"),
    "walk":                 dict(loop=True,  archetype="locomotion", crossfade=0.15, mask=False, sync="loco", mode="loco"),
    "Run":                  dict(loop=True,  archetype="locomotion", crossfade=0.15, mask=False, sync="loco", mode="loco"),
    "sprint":               dict(loop=True,  archetype="locomotion", crossfade=0.15, mask=False, sync="loco", mode="loco"),
    "Exhausted_Run":        dict(loop=True,  archetype="locomotion", crossfade=0.15, mask=False, sync="loco", mode="loco"),
    "jump":                 dict(loop=False, archetype="traversal",  crossfade=0.08, mask=False, sync="none", mode="jump"),
    "Airborne":             dict(loop=True,  archetype="traversal",  crossfade=0.08, mask=False, sync="none", mode="air"),
    "crouch":               dict(loop=True,  archetype="traversal",  crossfade=0.15, mask=False, sync="none", mode="planted"),
    "slide":                dict(loop=False, archetype="traversal",  crossfade=0.08, mask=False, sync="none", mode="slide"),
    "Cast_OneHand":         dict(loop=False, archetype="cast",       crossfade=0.05, mask=True,  sync="none", mode="cast_stand"),
    "Cast_Run":             dict(loop=False, archetype="cast",       crossfade=0.05, mask=True,  sync="none", mode="cast_run"),
    "throw":                dict(loop=False, archetype="cast",       crossfade=0.05, mask=True,  sync="none", mode="cast_quick"),
    "cast_release_left":    dict(loop=False, archetype="cast",       crossfade=0.05, mask=True,  sync="none", mode="cast_quick"),
    "cast_release_right":   dict(loop=False, archetype="cast",       crossfade=0.05, mask=True,  sync="none", mode="cast_quick"),
    "cast_release_twohand": dict(loop=False, archetype="cast",       crossfade=0.05, mask=True,  sync="none", mode="cast_quick"),
    "hit":                  dict(loop=False, archetype="reaction",   crossfade=0.03, mask=False, sync="none", mode="planted"),
    "Stunned":              dict(loop=True,  archetype="reaction",   crossfade=0.03, mask=False, sync="none", mode="planted"),
    "trip":                 dict(loop=False, archetype="reaction",   crossfade=0.03, mask=False, sync="none", mode="fall"),
    "Tripped":              dict(loop=False, archetype="reaction",   crossfade=0.03, mask=False, sync="none", mode="fall"),
    "victory":              dict(loop=True,  archetype="frontend",   crossfade=0.15, mask=False, sync="none", mode="victory"),
}

# Clips authored by author_clips.py (Strafe_L/R, Backpedal, Aim_Hold, Fire, Reload, Vault, Cast_*) need no stand-in.
STAND_INS = {
    "Run_Start": "Run",
    "Run_Stop": "walk",
    "Turn_180": "Run",
    "Hit_Heavy": "hit",
    "Knockdown": "Tripped",
    "GetUp": "Tripped",
    "Idle_Break": "Idle",
    "Lobby_Idle": "Idle",
    "Showcase": "victory",
    "Defeat": "Idle",
}

# Built-in apex-rig-forge R2 joint limits in roster reference bone-local space (used if rig__joint_limits.json
# is absent or missing a bone; overridden automatically when rig__joint_limits.json is provided).
def default_r2_limits():
    lims = {}
    def add(name, flex, abduct, twist, f_ax=(1, 0, 0), a_ax=(0, 0, 1), t_ax=(0, 1, 0), **extra):
        d = {
            "flex": list(flex),
            "abduct": list(abduct),
            "twist": list(twist),
            "flexAxis": list(f_ax),
            "abductAxis": list(a_ax),
            "twistAxis": list(t_ax),
        }
        d.update(extra)
        lims[name] = d

    for sp in ("Spine", "Spine1", "Spine2"):
        add(sp, (-15.0, 25.0), (-15.0, 15.0), (-20.0, 20.0))
    add("Neck", (-40.0, 40.0), (-30.0, 30.0), (-50.0, 50.0))
    add("Head", (-25.0, 25.0), (-15.0, 15.0), (-30.0, 30.0))
    for side, sgn in (("Left", 1.0), ("Right", -1.0)):
        add(side + "Shoulder", (-20.0, 20.0), (-10.0, 30.0), (0.0, 0.0), f_ax=(1, 0, 0), a_ax=(0, 0, sgn))
        add(side + "Arm", (-50.0, 160.0), (-30.0, 140.0), (-80.0, 80.0), f_ax=(1, 0, 0), a_ax=(0, 0, sgn))
        add(side + "ForeArm", (-5.0, 145.0), (0.0, 0.0), (-85.0, 85.0), f_ax=(1, 0, 0), a_ax=(0, 0, sgn), hinge=True, minSoftFlex=3.0)
        add(side + "Hand", (-60.0, 70.0), (-20.0, 35.0), (-80.0, 80.0), f_ax=(1, 0, 0), a_ax=(0, 0, sgn))
        add(side + "UpLeg", (-20.0, 125.0), (-25.0, 45.0), (-40.0, 40.0), f_ax=(1, 0, 0), a_ax=(0, 0, sgn))
        add(side + "Leg", (-3.0, 140.0), (0.0, 0.0), (-10.0, 10.0), f_ax=(1, 0, 0), a_ax=(0, 0, sgn), hinge=True, twistMinFlex=20.0, minSoftFlex=3.0)
        add(side + "Foot", (-45.0, 35.0), (-30.0, 30.0), (0.0, 0.0), f_ax=(1, 0, 0), a_ax=(0, 0, sgn))
        add(side + "ToeBase", (-30.0, 50.0), (0.0, 0.0), (0.0, 0.0), f_ax=(1, 0, 0), a_ax=(0, 0, sgn), hinge=True)
        for fg in ("Index", "Middle", "Ring", "Pinky"):
            splay = 20.0 if fg in ("Index", "Pinky") else 15.0
            add(side + "Hand" + fg + "1", (-15.0, 90.0), (-splay, splay), (0.0, 0.0), f_ax=(1, 0, 0), a_ax=(0, 0, sgn))
            add(side + "Hand" + fg + "2", (0.0, 100.0), (0.0, 0.0), (0.0, 0.0), f_ax=(1, 0, 0), a_ax=(0, 0, sgn), hinge=True)
            add(side + "Hand" + fg + "3", (-5.0, 80.0), (0.0, 0.0), (0.0, 0.0), f_ax=(1, 0, 0), a_ax=(0, 0, sgn), hinge=True)
        add(side + "HandThumb1", (-10.0, 50.0), (-60.0, 60.0), (-30.0, 30.0), f_ax=(1, 0, 0), a_ax=(0, 0, sgn))
        add(side + "HandThumb2", (0.0, 60.0), (0.0, 0.0), (0.0, 0.0), f_ax=(1, 0, 0), a_ax=(0, 0, sgn), hinge=True)
        add(side + "HandThumb3", (-10.0, 80.0), (0.0, 0.0), (0.0, 0.0), f_ax=(1, 0, 0), a_ax=(0, 0, sgn), hinge=True)
    return lims


def load_joint_limits(path, R_ref=None):
    """
    Load unity/Data/rig__joint_limits.json (SimData wrapped per #17, schema from char-intake-qa 995b623f).
    Reads exact bone-local axes from spec['axes']['local_blender'] (or model_blender transformed via R_ref),
    exact ranges, hinge kind, twistRequiresFlexDeg, and optional per-kid overrides/capsules.
    """
    lims = default_r2_limits()
    meta = {"overrides": {}, "capsules": {}}
    if not path or not os.path.exists(path):
        return lims, False
    with open(path, "r", encoding="utf-8") as fh:
        raw = json.load(fh)
    data = raw.get("data", raw)
    meta["overrides"] = data.get("overrides", {})
    meta["capsules"] = data.get("capsules", {})
    joints = data.get("joints", data.get("bones", data.get("limits", {})))
    if not isinstance(joints, dict):
        return lims, False

    for raw_name, spec in joints.items():
        if not isinstance(spec, dict):
            continue
        short = raw_name[len(P):] if raw_name.startswith(P) else raw_name
        entry = dict(lims.get(short, {}))
        for k_dst, keys in (
            ("flex", ("flex", "flexDeg", "flex_deg", "flexRange")),
            ("abduct", ("abduct", "abductDeg", "abduct_deg", "splay", "abductRange")),
            ("twist", ("twist", "twistDeg", "twist_deg", "twistRange")),
        ):
            for k in keys:
                if k in spec and spec[k] is not None:
                    val = spec[k]
                    if isinstance(val, (list, tuple)) and len(val) == 2:
                        entry[k_dst] = [float(val[0]), float(val[1])]
                    elif isinstance(val, dict):
                        entry[k_dst] = [float(val.get("min", 0.0)), float(val.get("max", 0.0))]
                    elif isinstance(val, (int, float)):
                        entry[k_dst] = [-abs(float(val)), abs(float(val))]
                    break

        # Exact char-intake-qa schema: spec["axes"]["local_blender"] -> {flex, abduct, twist}
        axes_block = spec.get("axes", {})
        if isinstance(axes_block, dict) and isinstance(axes_block.get("local_blender"), dict):
            lb = axes_block["local_blender"]
            for k_dst, k_src in (("flexAxis", "flex"), ("abductAxis", "abduct"), ("twistAxis", "twist")):
                v = lb.get(k_src)
                if isinstance(v, (list, tuple)) and len(v) == 3:
                    vec = Vector((float(v[0]), float(v[1]), float(v[2])))
                    if vec.length > 1e-6:
                        vec.normalize()
                        entry[k_dst] = [float(vec.x), float(vec.y), float(vec.z)]
        elif isinstance(axes_block, dict) and isinstance(axes_block.get("model_blender"), dict) and R_ref is not None and (P + short) in R_ref:
            mb = axes_block["model_blender"]
            for k_dst, k_src in (("flexAxis", "flex"), ("abductAxis", "abduct"), ("twistAxis", "twist")):
                v = mb.get(k_src)
                if isinstance(v, (list, tuple)) and len(v) == 3:
                    vec = (R_ref[P + short].transposed() @ Vector((float(v[0]), float(v[1]), float(v[2])))).normalized()
                    entry[k_dst] = [float(vec.x), float(vec.y), float(vec.z)]

        if spec.get("kind") == "hinge" or spec.get("hinge"):
            entry["hinge"] = True
        tw_req = spec.get("twistRequiresFlexDeg", spec.get("twistMinFlex", spec.get("twist_min_flex", 0.0)))
        if tw_req and float(tw_req) > 0.0:
            entry["twistMinFlex"] = float(tw_req)
        if short.endswith("Leg") and not short.endswith("UpLeg"):
            entry["hinge"] = True
            entry.setdefault("twistMinFlex", 20.0)
            entry.setdefault("minSoftFlex", 3.0)
        if short.endswith("ForeArm"):
            entry["hinge"] = True
            entry.setdefault("minSoftFlex", 3.0)
        lims[short] = entry

    lims["_meta"] = meta
    return lims, True


# ---- Exact Swing-Twist Decomposition & Limit Clamping (apex-rig-forge R2 / apex-anim-craft A5) -----------------

def swing_twist_decompose(q, twist_axis):
    """Decompose unit quaternion q = q_swing * q_twist around unit twist_axis."""
    q = q.normalized()
    if q.w < 0.0:
        q = -q
    ax = twist_axis.normalized()
    v = Vector((q.x, q.y, q.z))
    proj = ax * v.dot(ax)
    q_twist = Quaternion((q.w, proj.x, proj.y, proj.z))
    if q_twist.magnitude < 1e-8:
        q_twist = Quaternion((1.0, 0.0, 0.0, 0.0))
    else:
        q_twist.normalize()
    q_swing = (q @ q_twist.inverted()).normalized()
    if q_swing.w < 0.0:
        q_swing = -q_swing
    twist_angle = 2.0 * math.atan2(Vector((q_twist.x, q_twist.y, q_twist.z)).dot(ax), q_twist.w)
    return q_swing, q_twist, twist_angle


def measure_joint_angles_deg(q, lim):
    """Measure (flex_deg, abduct_deg, twist_deg) via exact swing-twist decomposition."""
    f_ax = Vector(lim["flexAxis"]).normalized()
    a_ax = Vector(lim["abductAxis"]).normalized()
    t_ax = Vector(lim["twistAxis"]).normalized()
    # Ensure orthonormal swing basis perpendicular to twist axis
    f_ax = (f_ax - f_ax.dot(t_ax) * t_ax).normalized()
    a_ax = (a_ax - a_ax.dot(t_ax) * t_ax - a_ax.dot(f_ax) * f_ax).normalized()
    if a_ax.length < 1e-6:
        a_ax = t_ax.cross(f_ax).normalized()
    q_swing, _, twist_rad = swing_twist_decompose(q, t_ax)
    sw_axis, sw_angle = q_swing.to_axis_angle()
    sw_vec = sw_axis * sw_angle
    flex_deg = math.degrees(sw_vec.dot(f_ax))
    abduct_deg = math.degrees(sw_vec.dot(a_ax))
    twist_deg = math.degrees(twist_rad)
    return flex_deg, abduct_deg, twist_deg, f_ax, a_ax, t_ax


def clamp_joint_swing_twist(q, lim, enforce_soft_min=True):
    """
    Clamp bone local quaternion q inside rig__joint_limits ellipse + twist bounds.
    Returns (q_clamped, overshoot_deg, detail_dict).
    """
    flex_deg, abduct_deg, twist_deg, f_ax, a_ax, t_ax = measure_joint_angles_deg(q, lim)
    f_min, f_max = float(lim["flex"][0]), float(lim["flex"][1])
    a_min, a_max = float(lim["abduct"][0]), float(lim["abduct"][1])
    t_min, t_max = float(lim["twist"][0]), float(lim["twist"][1])

    if enforce_soft_min and "minSoftFlex" in lim:
        f_min = max(f_min, float(lim["minSoftFlex"]))

    # Swing clamping: hinge (1-DOF) or asymmetric flex x abduct ellipse (2-DOF)
    is_hinge = bool(lim.get("hinge", False)) or (abs(a_min) < 1e-4 and abs(a_max) < 1e-4)
    if is_hinge:
        cf = max(f_min, min(f_max, flex_deg))
        ca = 0.0
    else:
        rf = f_max if flex_deg >= 0.0 else abs(f_min)
        ra = a_max if abduct_deg >= 0.0 else abs(a_min)
        nf = (flex_deg / rf) if rf > 1e-4 else (0.0 if abs(flex_deg) < 1e-4 else 1e6)
        na = (abduct_deg / ra) if ra > 1e-4 else (0.0 if abs(abduct_deg) < 1e-4 else 1e6)
        r2 = nf * nf + na * na
        if r2 > 1.0:
            inv = 1.0 / math.sqrt(r2)
            cf = (nf * inv * rf) if rf > 1e-4 else 0.0
            ca = (na * inv * ra) if ra > 1e-4 else 0.0
        else:
            cf = flex_deg
            ca = abduct_deg
        cf = max(f_min, min(f_max, cf))
        ca = max(a_min, min(a_max, ca))

    # Knee conditional twist: twist only allowed while flexed > twistMinFlex
    if "twistMinFlex" in lim and cf < float(lim["twistMinFlex"]):
        ct = 0.0
    else:
        ct = max(t_min, min(t_max, twist_deg))

    sw_clamped = f_ax * math.radians(cf) + a_ax * math.radians(ca)
    sw_ang = sw_clamped.length
    q_sw_c = Quaternion(sw_clamped / sw_ang, sw_ang) if sw_ang > 1e-8 else Quaternion((1.0, 0.0, 0.0, 0.0))
    q_tw_c = Quaternion(t_ax, math.radians(ct)) if abs(ct) > 1e-8 else Quaternion((1.0, 0.0, 0.0, 0.0))
    q_out = (q_sw_c @ q_tw_c).normalized()
    overshoot = math.degrees(q.normalized().rotation_difference(q_out).angle)
    return q_out, overshoot, {
        "raw": (round(flex_deg, 2), round(abduct_deg, 2), round(twist_deg, 2)),
        "clamped": (round(cf, 2), round(ca, 2), round(ct, 2)),
    }


# ---- Capsule Distance Math (A3 Girth & A5/A8 Penetration) -------------------------------------------------------

def seg_seg_distance(p1, q1, p2, q2):
    """Exact shortest distance between 3D line segments [p1,q1] and [p2,q2]."""
    d1 = q1 - p1
    d2 = q2 - p2
    r = p1 - p2
    a = d1.dot(d1)
    e = d2.dot(d2)
    f = d2.dot(r)
    if a <= 1e-10 and e <= 1e-10:
        return (p1 - p2).length
    if a <= 1e-10:
        s = 0.0
        t = max(0.0, min(1.0, f / e))
    else:
        c = d1.dot(r)
        if e <= 1e-10:
            t = 0.0
            s = max(0.0, min(1.0, -c / a))
        else:
            b = d1.dot(d2)
            denom = a * e - b * b
            if abs(denom) > 1e-10:
                s = max(0.0, min(1.0, (b * f - c * e) / denom))
            else:
                s = 0.0
            t = (b * s + f) / e
            if t < 0.0:
                t = 0.0
                s = max(0.0, min(1.0, -c / a))
            elif t > 1.0:
                t = 1.0
                s = max(0.0, min(1.0, (b - c) / a))
    c1 = p1 + d1 * s
    c2 = p2 + d2 * t
    return (c1 - c2).length


def build_ref_skeleton_model(arm):
    """
    Build the roster 40-deg A-pose reference frames (matching build_reference.py) and exact
    bone topology/offsets from roster_clips.glb's armature.
    """
    AM = arm.matrix_world.copy()
    AMi = AM.inverted()
    AM3 = AM.to_3x3()
    pb = arm.pose.bones
    if arm.animation_data:
        arm.animation_data.action = None
    for b in pb:
        b.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()

    order = [b.name for b in sorted(arm.data.bones, key=lambda b: len(b.parent_recursive))]
    parent_of = {b.name: (b.parent.name if b.parent else None) for b in arm.data.bones}
    R_src = {}
    head_src = {}
    tail_src = {}
    for bn in order:
        b = arm.data.bones[bn]
        R_src[bn] = (AM3 @ b.matrix_local.to_3x3()).normalized()
        head_src[bn] = (AM @ b.head_local).copy()
        tail_src[bn] = (AM @ b.tail_local).copy()
    L_src = {}
    for bn in order:
        p = parent_of[bn]
        L_src[bn] = (R_src[p].transposed() @ R_src[bn]) if p else R_src[bn].copy()

    # Pose arms to 40 deg below horizontal exactly as build_reference.py does
    def rotate_world(name, axis, deg):
        b = pb[name]
        pivot = AM @ b.head
        R = Matrix.Rotation(math.radians(deg), 4, axis)
        b.matrix = AMi @ (Matrix.Translation(pivot) @ R @ Matrix.Translation(-pivot) @ AM @ b.matrix)
        bpy.context.view_layer.update()

    for side, sgn in (("Left", 1.0), ("Right", -1.0)):
        n = P + side + "Arm"
        if n not in pb:
            continue
        d = (AM @ pb[n].tail - AM @ pb[n].head).normalized()
        cur = math.degrees(math.asin(max(-1.0, min(1.0, -d.z))))
        rotate_world(n, Vector((0, 1, 0)) * sgn, -(ARM_DROP_DEG - cur))
        d2 = (AM @ pb[n].tail - AM @ pb[n].head).normalized()
        if abs(math.degrees(math.asin(max(-1.0, min(1.0, -d2.z)))) - ARM_DROP_DEG) > 1.0:
            rotate_world(n, Vector((0, 1, 0)) * sgn, 2.0 * (ARM_DROP_DEG - cur))

    R_ref = {}
    head_ref = {}
    tail_ref = {}
    for bn in order:
        m = AM @ pb[bn].matrix
        R_ref[bn] = m.to_3x3().normalized()
        head_ref[bn] = (AM @ pb[bn].head).copy()
        tail_ref[bn] = (AM @ pb[bn].tail).copy()

    # Reset pose
    for b in pb:
        b.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()

    L_ref = {}
    off_ref = {}
    for bn in order:
        p = parent_of[bn]
        if p:
            L_ref[bn] = R_ref[p].transposed() @ R_ref[bn]
            off_ref[bn] = R_ref[p].transposed() @ (head_ref[bn] - head_ref[p])
        else:
            L_ref[bn] = R_ref[bn].copy()
            off_ref[bn] = Vector((0.0, 0.0, 0.0))

    # Reference limb segment lengths & ground heights
    ref_leg_len = (head_ref[P + "LeftUpLeg"] - head_ref[P + "LeftFoot"]).length
    foot_rest_z = head_ref[P + "LeftFoot"].z
    toe_rest_z = head_ref[P + "LeftToeBase"].z

    return {
        "order": order,
        "parent_of": parent_of,
        "R_src": R_src,
        "L_src": L_src,
        "head_src": head_src,
        "tail_src": tail_src,
        "R_ref": R_ref,
        "L_ref": L_ref,
        "head_ref": head_ref,
        "tail_ref": tail_ref,
        "off_ref": off_ref,
        "ref_leg_len": ref_leg_len,
        "foot_rest_z": foot_rest_z,
        "toe_rest_z": toe_rest_z,
    }


def compute_fk(skel, hips_delta, q_map):
    """Fast exact forward kinematics in world space from ref_rest local quaternions."""
    W_pos = {}
    W_rot = {}
    hips = P + "Hips"
    for bn in skel["order"]:
        p = skel["parent_of"][bn]
        q = q_map.get(bn)
        qm = q.to_matrix() if q is not None else Matrix.Identity(3)
        if p is None:
            W_pos[bn] = skel["head_ref"][bn] + (hips_delta if bn == hips else Vector((0.0, 0.0, 0.0)))
            W_rot[bn] = skel["R_ref"][bn] @ qm
        else:
            W_pos[bn] = W_pos[p] + (W_rot[p] @ skel["off_ref"][bn])
            W_rot[bn] = W_rot[p] @ skel["L_ref"][bn] @ qm
    return W_pos, W_rot


def measure_reference_capsules(arm):
    """Measure capsule radii from the reference mesh in roster_clips.glb (or safe child defaults)."""
    mesh = max((o for o in bpy.data.objects if o.type == "MESH" and o.vertex_groups),
               key=lambda o: len(o.data.vertices), default=None)
    caps = {
        "torso_r": 0.125,
        "hips_r": 0.130,
        "arm_r": 0.048,
        "forearm_r": 0.040,
        "hand_r": 0.042,
        "thigh_r": 0.072,
        "shin_r": 0.052,
    }
    if mesh is None:
        return caps
    AM = arm.matrix_world
    MM = mesh.matrix_world
    B = arm.data.bones

    def seg_r(bname, child_name=None, pct=0.75, default=0.05):
        if P + bname not in B:
            return default
        vg = mesh.vertex_groups.get(P + bname)
        if vg is None:
            return default
        a = AM @ B[P + bname].head_local
        b = (AM @ B[P + child_name].head_local) if (child_name and P + child_name in B) else (AM @ B[P + bname].tail_local)
        ab = b - a
        ab2 = max(ab.dot(ab), 1e-8)
        gi = vg.index
        ds = []
        for v in mesh.data.vertices:
            w = next((g.weight for g in v.groups if g.group == gi), 0.0)
            if w > 0.45:
                p = MM @ v.co
                t = max(0.15, min(0.85, (p - a).dot(ab) / ab2))
                ds.append((p - (a + ab * t)).length)
        if not ds:
            return default
        ds.sort()
        return ds[int(pct * (len(ds) - 1))]

    caps["torso_r"] = min(0.118, max(0.095, seg_r("Spine1", "Spine2", 0.55, 0.112)))
    caps["hips_r"] = min(0.118, max(0.098, seg_r("Hips", "Spine", 0.55, 0.114)))
    caps["arm_r"] = min(0.056, max(0.036, seg_r("LeftArm", "LeftForeArm", 0.60, 0.044)))
    caps["forearm_r"] = min(0.046, max(0.030, seg_r("LeftForeArm", "LeftHand", 0.60, 0.036)))
    # Flat palm thickness (~2.4 cm) so natural hand-on-hip contact (Idle/victory) is allowed while deep sink is caught
    caps["hand_r"] = min(0.026, max(0.018, seg_r("LeftHand", None, 0.35, 0.024)))
    caps["thigh_r"] = min(0.082, max(0.055, seg_r("LeftUpLeg", "LeftLeg", 0.62, 0.068)))
    caps["shin_r"] = min(0.060, max(0.040, seg_r("LeftLeg", "LeftFoot", 0.62, 0.048)))
    return caps


def test_capsule_penetrations(skel, W_pos, W_rot, caps):
    """
    Test arm/forearm/hand vs torso/hips and thigh/shin vs opposite thigh/shin.
    Returns dict of penetration depths in meters (0.0 = clear).
    """
    # Torso segments: lower torso (Hips->Spine1) and upper torso (Spine1->Spine2)
    h_p = W_pos[P + "Hips"]
    s1_p = W_pos[P + "Spine1"]
    s2_p = W_pos[P + "Spine2"]
    n_p = W_pos[P + "Neck"]
    # Trim torso ends slightly so shoulder/hip joints don't self-flag at their own socket
    torso_segs = [
        (h_p.lerp(s1_p, 0.20), s1_p, caps["hips_r"]),
        (s1_p, s2_p.lerp(n_p, 0.25), caps["torso_r"]),
    ]

    pen = {"LeftArm": 0.0, "RightArm": 0.0, "LeftUpLeg": 0.0, "RightUpLeg": 0.0}
    for side in ("Left", "Right"):
        a_p = W_pos[P + side + "Arm"]
        fa_p = W_pos[P + side + "ForeArm"]
        ha_p = W_pos[P + side + "Hand"]
        # Hand tip along hand bone axis
        ht_p = ha_p + (W_rot[P + side + "Hand"] @ Vector((0.0, 0.09, 0.0)))
        # Start upper arm test at 35% along humerus so deltoid socket doesn't false-trigger against chest
        arm_segs = [
            (a_p.lerp(fa_p, 0.35), fa_p, caps["arm_r"]),
            (fa_p.lerp(ha_p, 0.10), ha_p, caps["forearm_r"]),
            (ha_p, ht_p, caps["hand_r"]),
        ]
        worst = 0.0
        for p1, q1, r1 in arm_segs:
            for p2, q2, r2 in torso_segs:
                d = seg_seg_distance(p1, q1, p2, q2)
                ov = (r1 + r2) - d
                if ov > worst:
                    worst = ov
        pen[side + "Arm"] = worst

    # Left leg vs Right leg (thigh & shin, trimmed 25% below hip socket so crotch gap is natural)
    lu_p, ll_p, lf_p = W_pos[P + "LeftUpLeg"], W_pos[P + "LeftLeg"], W_pos[P + "LeftFoot"]
    ru_p, rl_p, rf_p = W_pos[P + "RightUpLeg"], W_pos[P + "RightLeg"], W_pos[P + "RightFoot"]
    l_segs = [(lu_p.lerp(ll_p, 0.28), ll_p, caps["thigh_r"]), (ll_p, lf_p, caps["shin_r"])]
    r_segs = [(ru_p.lerp(rl_p, 0.28), rl_p, caps["thigh_r"]), (rl_p, rf_p, caps["shin_r"])]
    leg_worst = 0.0
    for p1, q1, r1 in l_segs:
        for p2, q2, r2 in r_segs:
            d = seg_seg_distance(p1, q1, p2, q2)
            ov = (r1 + r2) - d
            if ov > leg_worst:
                leg_worst = ov
    pen["LeftUpLeg"] = leg_worst
    pen["RightUpLeg"] = leg_worst
    return pen


# ---- Analytical 2-Bone IK Solver (Zero Sideways Hinge, Soft-Lock Protected) ------------------------------------

def solve_two_bone_ik(skel, W_pos, W_rot, bone_a, bone_b, bone_c, target_pos, pole_dir,
                      flex_min_deg=4.0, flex_max_deg=135.0):
    """
    Solve analytical 2-bone IK for chain bone_a -> bone_b -> bone_c in ref_rest space.
    Aligns bone_b's local +X axis exactly to the hinge axis (n_pole x u), guaranteeing
    0.0 deg sideways hinge bend and flexion strictly within [flex_min_deg, flex_max_deg].
    Returns (q_ref_a, q_ref_b, W_rot_a, W_rot_b).
    """
    pA = W_pos[bone_a]
    p_parent = skel["parent_of"][bone_a]
    W_rot_parent = W_rot[p_parent] if p_parent else Matrix.Identity(3)

    off_b = skel["off_ref"][bone_b]
    off_c = skel["off_ref"][bone_c]
    L1 = off_b.length
    L2 = off_c.length

    vec = target_pos - pA
    dist = vec.length
    if dist < 1e-6:
        u = (W_rot[bone_a] @ off_b).normalized()
    else:
        u = vec / dist

    fmin = math.radians(flex_min_deg)
    fmax = math.radians(flex_max_deg)
    d_max = math.sqrt(max(1e-8, L1 * L1 + L2 * L2 - 2.0 * L1 * L2 * math.cos(math.pi - fmin)))
    d_min = math.sqrt(max(1e-8, L1 * L1 + L2 * L2 - 2.0 * L1 * L2 * math.cos(math.pi - fmax)))
    d = max(d_min, min(d_max, dist))

    cos_a = max(-1.0, min(1.0, (L1 * L1 + d * d - L2 * L2) / max(2.0 * L1 * d, 1e-8)))
    alpha = math.acos(cos_a)
    cos_inner = max(-1.0, min(1.0, (L1 * L1 + L2 * L2 - d * d) / max(2.0 * L1 * L2, 1e-8)))
    beta = math.pi - math.acos(cos_inner)  # desired anatomical bend angle >= fmin

    # Project pole_dir perpendicular to target axis u
    n_pole = pole_dir - pole_dir.dot(u) * u
    if n_pole.length < 1e-5:
        fallback = Vector((0.0, -1.0, 0.0)) if abs(u.y) < 0.9 else Vector((0.0, 0.0, 1.0))
        n_pole = fallback - fallback.dot(u) * u
    n_pole.normalize()

    d1 = (u * math.cos(alpha) + n_pole * math.sin(alpha)).normalized()
    hinge_world = n_pole.cross(u).normalized()

    # Align bone_a so its off_b direction -> d1 and child's +X hinge axis -> hinge_world
    e1_loc = off_b.normalized()
    h_in_a = (skel["L_ref"][bone_b] @ Vector((1.0, 0.0, 0.0))).normalized()
    e2_loc = (h_in_a - h_in_a.dot(e1_loc) * e1_loc).normalized()
    e3_loc = e1_loc.cross(e2_loc)

    e1_w = d1
    e2_w = hinge_world
    e3_w = e1_w.cross(e2_w)

    M_w = Matrix((e1_w, e2_w, e3_w)).transposed()
    M_l = Matrix((e1_loc, e2_loc, e3_loc))
    W_rot_a = (M_w @ M_l).normalized()
    q_ref_a = (skel["L_ref"][bone_a].transposed() @ W_rot_parent.transposed() @ W_rot_a).to_quaternion().normalized()

    # Exact hinge angle accounting for any slight rest angle between off_b and off_c
    W_rot_b_zero = W_rot_a @ skel["L_ref"][bone_b]
    d2_zero = (W_rot_b_zero @ off_c).normalized()
    pB = pA + d1 * L1
    d2_want = ((pA + u * d) - pB).normalized()
    # Signed angle around hinge_world from d2_zero to d2_want
    z0 = (d2_zero - d2_zero.dot(hinge_world) * hinge_world).normalized()
    z1 = (d2_want - d2_want.dot(hinge_world) * hinge_world).normalized()
    beta_local = math.atan2(z0.cross(z1).dot(hinge_world), max(-1.0, min(1.0, z0.dot(z1))))
    beta_local = max(fmin, min(fmax, beta_local))

    q_ref_b = Quaternion((1.0, 0.0, 0.0), beta_local)
    W_rot_b = (W_rot_b_zero @ q_ref_b.to_matrix()).normalized()
    return q_ref_a, q_ref_b, W_rot_a, W_rot_b


# ---- Interpolation & Signal Utilities ---------------------------------------------------------------------------

def sample_quat_seq(seq, f_float, loop):
    """Fractional-frame slerp sampling with periodic wrap for loops."""
    n = len(seq) - 1  # frames 0..n
    if n <= 0:
        return seq[0].copy()
    if loop:
        f_mod = f_float % n
        i0 = int(math.floor(f_mod)) % n
        i1 = (i0 + 1) % n
        t = f_mod - math.floor(f_mod)
    else:
        fc = max(0.0, min(float(n), f_float))
        i0 = min(n - 1, int(math.floor(fc)))
        i1 = i0 + 1
        t = fc - i0
    q0 = seq[i0]
    q1 = seq[i1]
    if q0.dot(q1) < 0.0:
        q1 = -q1
    return q0.slerp(q1, t).normalized()


def sample_vec_seq(seq, f_float, loop):
    n = len(seq) - 1
    if n <= 0:
        return seq[0].copy()
    if loop:
        f_mod = f_float % n
        i0 = int(math.floor(f_mod)) % n
        i1 = (i0 + 1) % n
        t = f_mod - math.floor(f_mod)
    else:
        fc = max(0.0, min(float(n), f_float))
        i0 = min(n - 1, int(math.floor(fc)))
        i1 = i0 + 1
        t = fc - i0
    return seq[i0].lerp(seq[i1], t)


def smooth_scalar_seq(vals, radius, loop):
    """Triangular smoothing kernel over +-radius frames (periodic for loops)."""
    n = len(vals) - 1
    if n <= 0 or radius <= 0:
        return list(vals)
    out = [0.0] * (n + 1)
    weights = [radius + 1 - abs(k) for k in range(-radius, radius + 1)]
    wsum = float(sum(weights))
    for i in range(n + 1):
        acc = 0.0
        for k, w in zip(range(-radius, radius + 1), weights):
            if loop:
                j = (i + k) % n
            else:
                j = max(0, min(n, i + k))
            acc += vals[j] * w
        out[i] = acc / wsum
    if loop:
        out[n] = out[0]
    return out


def smooth_quat_seq(seq, alpha, loop, pinned=None):
    """Gentle 3-tap quaternion smoothing with optional pinned frame set."""
    n = len(seq) - 1
    if n < 2:
        return [q.copy() for q in seq]
    pinned = pinned or set()
    out = []
    for i in range(n + 1):
        if i in pinned or (not loop and (i == 0 or i == n)):
            out.append(seq[i].copy())
            continue
        i_prev = (i - 1) % n if loop else i - 1
        i_next = (i + 1) % n if loop else i + 1
        qp = seq[i_prev]
        qc = seq[i]
        qn = seq[i_next]
        if qp.dot(qc) < 0.0:
            qp = -qp
        if qn.dot(qc) < 0.0:
            qn = -qn
        mid = qp.slerp(qn, 0.5).normalized()
        if mid.dot(qc) < 0.0:
            mid = -mid
        out.append(qc.slerp(mid, alpha).normalized())
    if loop:
        out[n] = out[0].copy()
    return out


def smooth_vec_seq(seq, alpha, loop, pinned=None):
    n = len(seq) - 1
    if n < 2:
        return [v.copy() for v in seq]
    pinned = pinned or set()
    out = []
    for i in range(n + 1):
        if i in pinned or (not loop and (i == 0 or i == n)):
            out.append(seq[i].copy())
            continue
        i_prev = (i - 1) % n if loop else i - 1
        i_next = (i + 1) % n if loop else i + 1
        mid = (seq[i_prev] + seq[i_next]) * 0.5
        out.append(seq[i].lerp(mid, alpha))
    if loop:
        out[n] = out[0].copy()
    return out


def ensure_continuous_quats(seq):
    """Eliminate any quaternion hemisphere sign flips (dot < 0) across frames."""
    if not seq:
        return seq
    out = [seq[0].normalized()]
    if out[0].w < 0.0:
        out[0] = -out[0]
    for i in range(1, len(seq)):
        q = seq[i].normalized()
        if q.dot(out[-1]) < 0.0:
            q = -q
        out.append(q)
    return out


def piecewise_time_warp(t_out, knots):
    """
    Smooth C1 monotone time remap mapping output frame t_out -> source frame t_in.
    knots: sorted list of (out_frame, src_frame) including endpoints.
    Uses subtle ease inside each interval while hitting EVERY knot exactly (locks gameplay release frames!).
    """
    if t_out <= knots[0][0]:
        return float(knots[0][1])
    if t_out >= knots[-1][0]:
        return float(knots[-1][1])
    for k in range(len(knots) - 1):
        o0, s0 = knots[k]
        o1, s1 = knots[k + 1]
        if o0 <= t_out <= o1:
            if o1 <= o0:
                return float(s0)
            u = (t_out - o0) / (o1 - o0)
            return s0 + (s1 - s0) * u
    return float(knots[-1][1])


# ---- Clip Upgrade Stages ----------------------------------------------------------------------------------------

def extract_clip_frames(arm, act, skel):
    """Sample an action at 30 fps into ref_rest local quaternions + world hips deltas."""
    scn = bpy.context.scene
    scn.render.fps = FPS
    if arm.animation_data is None:
        arm.animation_data_create()
    arm.animation_data.action = act
    try:
        arm.animation_data.action_slot = act.slots[0]
    except Exception:
        pass
    f0 = int(round(act.frame_range[0]))
    f1 = int(round(act.frame_range[1]))
    n_frames = max(1, f1 - f0)
    AM = arm.matrix_world
    AM3 = AM.to_3x3()

    hips_deltas = []
    q_tracks = {bn: [] for bn in skel["order"]}

    for f in range(f0, f0 + n_frames + 1):
        scn.frame_set(f)
        bpy.context.view_layer.update()
        # World bone orientations & hips position
        W_pose = {}
        for bn in skel["order"]:
            pb = arm.pose.bones[bn]
            W_pose[bn] = (AM3 @ pb.matrix.to_3x3()).normalized()
        hd = (AM @ arm.pose.bones[P + "Hips"].head) - skel["head_src"][P + "Hips"]
        hips_deltas.append(hd.copy())

        for bn in skel["order"]:
            p = skel["parent_of"][bn]
            if p is None:
                M = skel["R_ref"][bn].transposed() @ W_pose[bn]
            else:
                M = skel["L_ref"][bn].transposed() @ W_pose[p].transposed() @ W_pose[bn]
            q_tracks[bn].append(M.to_quaternion().normalized())

    for bn in skel["order"]:
        q_tracks[bn] = ensure_continuous_quats(q_tracks[bn])
    return n_frames, hips_deltas, q_tracks


def read_character_release_times(data_dir):
    """Read authoritative standing/moving cast release times from unity/Data/characters__narda.json."""
    stand_t = 0.725
    move_t = 1.008
    narda_json = os.path.join(data_dir, "characters__narda.json")
    if os.path.exists(narda_json):
        with open(narda_json, "r", encoding="utf-8") as fh:
            d = json.load(fh).get("data", {})
        stand_t = float(d.get("cast_release_standing", stand_t))
        move_t = float(d.get("cast_release_moving", move_t))
    return stand_t, move_t


def fix_cast_run_legs(n_frames, hips_deltas, q_tracks, run_hips, run_q):
    """
    Replace Cast_Run's broken lower-body splice (frames 25..36 teleported by 36.7 deg!)
    with a continuous phase-synced Run leg cycle while keeping upper-body cast 100% intact.
    """
    run_n = len(run_hips) - 1
    leg_bones = {P + "Hips"} | {
        P + s + x for s in ("Left", "Right") for x in ("UpLeg", "Leg", "Foot", "ToeBase", "Toe_End")
    }
    # Cast_Run starts synchronized with Run at f=0; map each frame smoothly across the 66 frames
    # using 3.6667 cycles -> align to exact 4 cycles or natural Run cadence (18f period)
    # Wait: 66 / 18 = 3.667, and Cast_Run f=0..18 already matched Run f=0..18!
    # Using exact Run cadence (f % 18) keeps f=0..25 matching original Cast_Run and eliminates the f=26..35 freeze/snap!
    for f in range(n_frames + 1):
        rf = float(f % run_n)
        # Blend smoothly: 100% continuous Run legs throughout so feet never freeze or teleport!
        hips_deltas[f] = Vector((0.0, 0.0, sample_vec_seq(run_hips, rf, True).z))
        for bn in leg_bones:
            if bn in q_tracks and bn in run_q:
                q_run = sample_quat_seq(run_q[bn], rf, True)
                if bn == P + "Hips":
                    # Preserve Cast_Run's subtle torso twist/yaw during throw while keeping Run hip bob/pelvis rock
                    q_tracks[bn][f] = q_run
                else:
                    q_tracks[bn][f] = q_run


def stage_retime_and_exaggerate(clip_name, spec, n_frames, hips_deltas, q_tracks, stand_rel_s, move_rel_s):
    """
    A2 Steps 2-3: Retime for Fortnite snap (anticipation holds + crisp strike spacing while strictly
    locking CharacterDef release frames) + additive Line-of-Action exaggeration (zero non-uniform scale).
    Returns (release_frame, phase_ranges_or_None).
    """
    loop = spec["loop"]
    mode = spec["mode"]
    release_frame = -1
    phase_ranges = None

    # ---- Step 2: Retime curves (piecewise knot warps preserving exact gameplay frames)
    knots = None
    if clip_name == "Cast_OneHand":
        rel_f = int(round(stand_rel_s * FPS))  # 22 (0.733s vs 0.725s = within 0.25 frame!)
        release_frame = rel_f
        # Original peak windup is f=16, release f=22, follow-through peak f=27
        # Retime: snappy windup into 3f anticipation hold (f13..16), explosive whip f16->22 (pinned!),
        # bold follow-through hold f25..28, smooth recovery f28..66.
        knots = [(0, 0), (13, 15.2), (17, 16.4), (rel_f, 22.0), (25, 26.8), (29, 27.5), (n_frames, n_frames)]
    elif clip_name == "Cast_Run":
        rel_f = int(round(move_rel_s * FPS))  # 30 (1.000s vs 1.008s = within 0.24 frame!)
        release_frame = rel_f
        # Retime upper body only (legs already clean continuous Run!)
        knots = [(0, 0), (22, 24.0), (26, 25.5), (rel_f, 30.2), (34, 33.8), (38, 35.0), (n_frames, n_frames)]
    elif clip_name in ("throw", "cast_release_left", "cast_release_right"):
        release_frame = 15  # 0.500s
        # Hold windup f7..10, snap to release f15, brief follow-through hold f16..19
        knots = [(0, 0), (7, 7.6), (10, 8.4), (15, 15.5), (19, 17.5), (n_frames, n_frames)]
    elif clip_name == "cast_release_twohand":
        release_frame = 18  # 0.600s
        knots = [(0, 0), (9, 9.8), (12, 10.6), (18, 18.0), (22, 19.5), (n_frames, n_frames)]
    elif clip_name == "hit":
        # A1/A2: directional reaction peak <= 6 frames (peak at f5, hold f5..7, quick recover)
        knots = [(0, 0), (4, 5.2), (7, 6.0), (n_frames, n_frames)]
    elif clip_name == "jump":
        # Split cleanly into takeoff (0..6), air (6..20), land (20..27) with apex hang!
        phase_ranges = {"takeoff": [0, 6], "air": [6, 20], "land": [20, n_frames]}
        knots = [(0, 0), (3, 1.2), (6, 6.5), (12, 13.0), (16, 14.8), (20, 21.5), (23, 25.5), (n_frames, n_frames)]
    elif clip_name == "slide":
        # Snappy drop-in (0..5), extended low slide hold (6..21), crisp pop-up (22..30)
        knots = [(0, 0), (5, 7.5), (12, 14.5), (19, 15.5), (24, 22.0), (n_frames, n_frames)]
    elif clip_name == "victory":
        # Hero pose hold (A1: hold on the hero pose)
        knots = [(0, 0), (14, 15.0), (22, 22.0), (38, 30.0), (46, 44.0), (n_frames, n_frames)]

    if knots is not None:
        upper_only = (clip_name == "Cast_Run")
        upper_bones = {
            P + x for x in (
                "Spine", "Spine1", "Spine2", "Neck", "Head",
                "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
                "RightShoulder", "RightArm", "RightForeArm", "RightHand",
            )
        }
        orig_hips = [v.copy() for v in hips_deltas]
        orig_q = {bn: [q.copy() for q in qs] for bn, qs in q_tracks.items()}
        for f in range(n_frames + 1):
            tf = piecewise_time_warp(float(f), knots)
            if not upper_only:
                hips_deltas[f] = sample_vec_seq(orig_hips, tf, loop)
            for bn in q_tracks:
                if upper_only and bn not in upper_bones:
                    continue
                q_tracks[bn][f] = sample_quat_seq(orig_q[bn], tf, loop)

    # ---- Step 3: Additive Line of Action & Exaggeration (bounded, zero non-uniform scale!)
    def add_rot(bn, f, axis, deg):
        if bn in q_tracks and abs(deg) > 1e-4:
            q_tracks[bn][f] = (q_tracks[bn][f] @ Quaternion(axis, math.radians(deg))).normalized()

    if clip_name in ("walk", "Run", "sprint", "Exhausted_Run"):
        # Athletic forward lean along line of action + crisp spine counter-twist.
        # Neck & Head counter-pitch 100% of total forward lean (incl. residual pelvis tilt) so kids
        # look straight down the street instead of staring at the floor!
        lean_map = {"walk": 3.0, "Run": 9.5, "sprint": 13.5, "Exhausted_Run": 12.0}
        twist_amp = {"walk": 3.5, "Run": 7.5, "sprint": 9.5, "Exhausted_Run": 4.0}[clip_name]
        pelvis_res_map = {"walk": 0.75, "Run": 1.65, "sprint": 2.10, "Exhausted_Run": 1.0}
        lean = lean_map[clip_name]
        head_comp = lean + pelvis_res_map[clip_name]
        for f in range(n_frames + 1):
            phase = 2.0 * math.pi * (f / float(n_frames))
            add_rot(P + "Spine", f, Vector((1, 0, 0)), lean * 0.35)
            add_rot(P + "Spine1", f, Vector((1, 0, 0)), lean * 0.35)
            add_rot(P + "Spine2", f, Vector((1, 0, 0)), lean * 0.30)
            # Full eyes-up counter-pitch across Neck + Head (+10% proud chin lift on Run/sprint!)
            comp_scale = 0.85 if clip_name == "Exhausted_Run" else 1.08
            add_rot(P + "Neck", f, Vector((1, 0, 0)), -head_comp * 0.52 * comp_scale)
            add_rot(P + "Head", f, Vector((1, 0, 0)), -head_comp * 0.48 * comp_scale)
            # Counter-twist torso opposite hips for crisp 64px silhouette
            tw = math.sin(phase) * twist_amp
            add_rot(P + "Spine1", f, Vector((0, 1, 0)), tw * 0.5)
            add_rot(P + "Spine2", f, Vector((0, 1, 0)), tw * 0.5)
            add_rot(P + "Neck", f, Vector((0, 1, 0)), -tw * 0.6)
            if clip_name == "Exhausted_Run":
                bob = math.sin(2.0 * phase) * 3.5
                add_rot(P + "Head", f, Vector((1, 0, 0)), 3.0 + bob)

    elif clip_name == "Idle":
        # Playful subtle breathing & weight shift (grounded Valorant/Fortnite ready feel)
        for f in range(n_frames + 1):
            phase = 2.0 * math.pi * (f / float(n_frames))
            breath = math.sin(phase)
            sway = math.sin(phase * 0.5) if not loop else math.sin(phase)
            add_rot(P + "Spine1", f, Vector((1, 0, 0)), 2.0 + 1.5 * breath)
            add_rot(P + "Spine2", f, Vector((1, 0, 0)), 1.5 * breath)
            add_rot(P + "Head", f, Vector((1, 0, 0)), -1.5 - 1.0 * breath)
            add_rot(P + "Spine", f, Vector((0, 0, 1)), 1.8 * sway)
            add_rot(P + "Head", f, Vector((0, 0, 1)), -1.2 * sway)
            hips_deltas[f].z += 0.008 * breath

    elif clip_name == "jump":
        # Bigger pre-jump crouch dip (f1..4) and crisp landing hip settle (f21..25)
        for f in range(n_frames + 1):
            # Anticipation crouch peak at f=3
            if 1 <= f <= 5:
                k = math.sin(math.pi * (f - 0.5) / 5.0)
                hips_deltas[f].z -= 0.065 * k
                add_rot(P + "Spine", f, Vector((1, 0, 0)), 8.0 * k)
                add_rot(P + "Spine1", f, Vector((1, 0, 0)), 7.0 * k)
                add_rot(P + "Head", f, Vector((1, 0, 0)), -8.0 * k)
            # Apex heroic lift (f=9..17)
            elif 8 <= f <= 18:
                k = math.sin(math.pi * (f - 8) / 10.0)
                hips_deltas[f].z += 0.035 * k
                add_rot(P + "Spine2", f, Vector((1, 0, 0)), -5.0 * k)
            # Landing settle (f=21..26)
            elif 20 <= f <= n_frames - 1:
                k = math.sin(math.pi * (f - 20) / max(1, n_frames - 20))
                hips_deltas[f].z -= 0.055 * k
                add_rot(P + "Spine", f, Vector((1, 0, 0)), 7.0 * k)
                add_rot(P + "Spine1", f, Vector((1, 0, 0)), 6.0 * k)

    elif clip_name in ("Cast_OneHand", "Cast_Run", "throw", "cast_release_left", "cast_release_right", "cast_release_twohand"):
        # Coil torso during anticipation -> uncoil into release with <=10% overshoot settle
        sgn = -1.0 if clip_name == "cast_release_left" else 1.0
        if clip_name == "cast_release_twohand":
            sgn = 0.0
        rf = release_frame if release_frame > 0 else n_frames // 2
        wind_f = max(3, rf - 7)
        for f in range(n_frames + 1):
            # Coil peak at wind_f, snap uncoil at rf..rf+2, settle by rf+8
            coil = 0.0
            lean = 0.0
            if 0 < f <= wind_f:
                u = f / float(wind_f)
                coil = math.sin(u * math.pi * 0.5)
                lean = -5.0 * coil
            elif wind_f < f <= rf + 2:
                u = (f - wind_f) / float(max(1, rf + 2 - wind_f))
                coil = math.cos(u * math.pi)  # +1 -> -1
                lean = -5.0 * (1.0 - u) + 8.0 * u
            elif rf + 2 < f <= min(n_frames, rf + 10):
                u = (f - (rf + 2)) / float(max(1, min(n_frames, rf + 10) - (rf + 2)))
                # 10% damped overshoot settle
                coil = -math.cos(u * math.pi * 0.5) * (1.0 - 0.1 * math.sin(u * math.pi))
                lean = 8.0 * (1.0 - u)
            if abs(sgn) > 0.1 and abs(coil) > 1e-3:
                add_rot(P + "Spine1", f, Vector((0, 1, 0)), -sgn * 8.0 * coil)
                add_rot(P + "Spine2", f, Vector((0, 1, 0)), -sgn * 10.0 * coil)
                add_rot(P + "Neck", f, Vector((0, 1, 0)), sgn * 10.0 * coil)
            if abs(lean) > 1e-3:
                add_rot(P + "Spine1", f, Vector((1, 0, 0)), lean * 0.5)
                add_rot(P + "Spine2", f, Vector((1, 0, 0)), lean * 0.5)

    elif clip_name == "hit":
        # Crisp recoil snap peaking at f=5 (<= 6f) + rapid athletic recovery
        for f in range(n_frames + 1):
            if f <= 5:
                k = math.sin(0.5 * math.pi * f / 5.0)
            else:
                k = max(0.0, 1.0 - (f - 5) / float(max(1, n_frames - 5)))
                k = k * k
            add_rot(P + "Spine1", f, Vector((1, 0, 0)), -8.0 * k)
            add_rot(P + "Spine2", f, Vector((1, 0, 0)), -10.0 * k)
            add_rot(P + "Head", f, Vector((1, 0, 0)), -10.0 * k)

    elif clip_name == "victory":
        # Proud chest lift during hero hold
        for f in range(n_frames + 1):
            phase = 2.0 * math.pi * (f / float(n_frames))
            lift = 0.5 - 0.5 * math.cos(phase)
            add_rot(P + "Spine2", f, Vector((1, 0, 0)), -6.0 * lift)
            add_rot(P + "Head", f, Vector((1, 0, 0)), -4.0 * lift)

    if loop:
        hips_deltas[n_frames] = hips_deltas[0].copy()
        for bn in q_tracks:
            q_tracks[bn][n_frames] = q_tracks[bn][0].copy()

    return release_frame, phase_ranges


def stage_overlap(clip_name, spec, n_frames, q_tracks, release_frame):
    """A2 Step 4: 1-3 frame organic phase lag on spine/neck/head behind hips."""
    loop = spec["loop"]
    # Keep release timing razor-sharp on fast cast clips by dialing back lag slightly on casts
    scale = 0.55 if spec["archetype"] == "cast" else 1.0
    lags = [
        (P + "Spine1", 0.75 * scale),
        (P + "Spine2", 1.40 * scale),
        (P + "Neck",   2.00 * scale),
        (P + "Head",   2.50 * scale),
    ]
    for bn, lag in lags:
        if bn not in q_tracks or lag < 0.05:
            continue
        orig = [q.copy() for q in q_tracks[bn]]
        new_seq = []
        for f in range(n_frames + 1):
            if not loop and (f == 0 or f == n_frames):
                new_seq.append(orig[f].copy())
                continue
            # Taper lag cleanly near start/end of one-shot clips so boundaries stay pop-free
            eff_lag = lag
            if not loop:
                edge = min(f, n_frames - f)
                eff_lag *= min(1.0, edge / 3.0)
            new_seq.append(sample_quat_seq(orig, float(f) - eff_lag, loop))
        if loop:
            new_seq[n_frames] = new_seq[0].copy()
        q_tracks[bn] = ensure_continuous_quats(new_seq)


def stage_foot_lock_and_arm_ik(clip_name, spec, skel, n_frames, hips_deltas, q_tracks, release_frame):
    """
    A2 Steps 5-6:
    - Zero horizontal root drift (in-place A4).
    - Analytical 2-bone Leg IK with foot contact lock (<= 2 cm slide), clean swing lift,
      toe-roll push-off, and grounded hip height (zero underground clipping, zero straight-locked knees).
    - Analytical 2-bone Arm IK eliminating ALL sideways elbow bends (0.0 deg Z-hinge) and smoothing hand arcs.
    Returns (stride_distance_m, stride_speed_mps, contact_frames_L, contact_frames_R).
    """
    loop = spec["loop"]
    mode = spec["mode"]
    hips = P + "Hips"
    foot_z0 = min(skel["foot_rest_z"], 0.105)
    toe_z0 = skel["toe_rest_z"]
    L_thigh = skel["off_ref"][P + "LeftLeg"].length
    L_shin = skel["off_ref"][P + "LeftFoot"].length
    # cos(4 deg / 2) = 0.9994 -> 0.9975 guarantees soft athletic >= 7 deg knee bend without Groucho squatting!
    L_leg_max = (L_thigh + L_shin) * 0.9975

    # Zero horizontal Hips drift right now (retopo_kid.py also strips X/Y; keeping Z only guarantees 100% parity!)
    for f in range(n_frames + 1):
        hips_deltas[f] = Vector((0.0, 0.0, hips_deltas[f].z))

    # Sample current FK positions & rotations across all frames before IK
    fk_pos = []
    fk_rot = []
    for f in range(n_frames + 1):
        q_f = {bn: q_tracks[bn][f] for bn in skel["order"]}
        wp, wr = compute_fk(skel, hips_deltas[f], q_f)
        fk_pos.append(wp)
        fk_rot.append(wr)

    # ---- PART A: LEG IK & FOOT LOCK ---------------------------------------------------------------------
    stride_distance_m = 0.0
    stride_speed_mps = 0.0
    contact_L = []
    contact_R = []

    if mode == "loco" or mode == "cast_run":
        # Constant-velocity treadmill foot lock for walk / Run / sprint / Exhausted_Run / Cast_Run!
        # Calibrated symmetric stance geometry per gait (physically honest for ~0.696m leg length).
        # Note: Blender -Y is forward, +Y is backward (foot travels -Y -> +Y during ground stance!).
        # Tall athletic Fortnite stride parameters:
        # - walk: inverted pendulum (tall mid-stance ~8-10 deg knee, ZERO Groucho crouch!)
        # - Run / Cast_Run / sprint: wide athletic reach + trailing push-off kick + buoyant flight lift!
        # Note: Blender -Y is forward, +Y is backward (foot travels -Y -> +Y during ground stance!).
        gait_cfg = {
            "walk":          dict(duty=0.50, y_front=-0.27, y_back=0.28, swing_h=0.060, trail_kick=0.02, lane_x=0.106, hip_drop=-0.015, hip_bob=0.014, bob_phase=0.00, heel_lift=0.085, pelvis_pitch=5.0),
            "Exhausted_Run": dict(duty=0.46, y_front=-0.25, y_back=0.26, swing_h=0.065, trail_kick=0.03, lane_x=0.112, hip_drop=-0.028, hip_bob=0.018, bob_phase=0.05, heel_lift=0.090, pelvis_pitch=8.0),
            "Run":           dict(duty=0.36, y_front=-0.33, y_back=0.39, swing_h=0.165, trail_kick=0.11, lane_x=0.102, hip_drop=-0.022, hip_bob=0.036, bob_phase=0.12, heel_lift=0.115, pelvis_pitch=11.0),
            "Cast_Run":      dict(duty=0.36, y_front=-0.33, y_back=0.39, swing_h=0.165, trail_kick=0.11, lane_x=0.102, hip_drop=-0.022, hip_bob=0.036, bob_phase=0.12, heel_lift=0.115, pelvis_pitch=11.0),
            "sprint":        dict(duty=0.32, y_front=-0.30, y_back=0.33, swing_h=0.185, trail_kick=0.14, lane_x=0.100, hip_drop=-0.026, hip_bob=0.038, bob_phase=0.12, heel_lift=0.130, pelvis_pitch=14.0),
        }[clip_name]

        # Period in frames (Cast_Run runs at 18f Run cadence!)
        cycle_f = 18.0 if clip_name == "Cast_Run" else float(n_frames)
        cycle_s = cycle_f / FPS
        stance_len_f = gait_cfg["duty"] * cycle_f
        stance_s = stance_len_f / FPS
        step_disp = gait_cfg["y_back"] - gait_cfg["y_front"]
        v_stride = step_disp / stance_s  # exact constant ground speed during stance (m/s)
        stride_speed_mps = v_stride
        stride_distance_m = v_stride * cycle_s

        # Determine phase offset of Left foot heel strike from original clip (where LeftFoot.y is minimum = most forward)
        search_end = min(int(round(cycle_f)), n_frames)
        f_l_strike = min(range(search_end), key=lambda i: fk_pos[i][P + "LeftFoot"].y)
        f_r_strike = (f_l_strike + cycle_f * 0.5) % cycle_f

        def foot_target_at(f_idx, strike_f, x_sign):
            # Phase u in [0, 1) where u=0 is heel strike, u=duty is toe-off
            u = ((float(f_idx) - strike_f) % cycle_f) / cycle_f
            duty = gait_cfg["duty"]
            x = x_sign * gait_cfg["lane_x"]
            if u <= duty:
                s = u / duty  # 0..1 across stance
                y = gait_cfg["y_front"] + step_disp * s
                # Flat sole through s in [0.12, 0.72], subtle heel-strike landing at start, smooth toe-roll push-off at end
                if s < 0.12:
                    # Touchdown settle: heel lowers cleanly to flat
                    k = (1.0 - s / 0.12)
                    z = foot_z0 + 0.012 * (k * k)
                    pitch = 12.0 * k  # dorsiflexed heel strike
                    toe_bend = -6.0 * k
                elif s <= 0.72:
                    z = foot_z0
                    pitch = 0.0
                    toe_bend = 0.0
                else:
                    # Toe-roll push-off: heel lifts smoothly while toe stays planted on ground
                    k = (s - 0.72) / 0.28
                    k_sm = k * k * (3.0 - 2.0 * k)
                    z = foot_z0 + gait_cfg["heel_lift"] * k_sm
                    pitch = -28.0 * k_sm  # plantarflex ankle
                    toe_bend = 26.0 * k_sm  # toes stay flat on street
                return Vector((x, y, z)), True, pitch, toe_bend
            else:
                # Swing phase: expressive trailing heel-kick right after toe-off, then high knee drive forward!
                sw = (u - duty) / (1.0 - duty)  # 0..1 across swing
                y0 = gait_cfg["y_back"]
                y1 = gait_cfg["y_front"]
                m0 = step_disp * (1.0 - duty) / duty * 0.65
                m1 = step_disp * (1.0 - duty) / duty * 0.45
                h00 = 2 * sw**3 - 3 * sw**2 + 1
                h10 = sw**3 - 2 * sw**2 + sw
                h01 = -2 * sw**3 + 3 * sw**2
                h11 = sw**3 - sw**2
                # Add subtle early-swing trailing extension kick so Run/sprint silhouette shows strong rear leg drive
                trail_bump = gait_cfg["trail_kick"] * math.sin(math.pi * min(1.0, sw / 0.42)) if sw < 0.42 else 0.0
                y = h00 * y0 + h10 * m0 + h01 * y1 + h11 * m1 + trail_bump
                # Vertical arc: starts from heel_lift, peaks strongly during trailing heel-kick & knee drive
                z0 = foot_z0 + gait_cfg["heel_lift"]
                z1 = foot_z0 + 0.012
                z_base = z0 * (1.0 - sw) + z1 * sw
                z = z_base + gait_cfg["swing_h"] * (math.sin(math.pi * sw) ** 0.85)
                pitch = -28.0 * ((1.0 - sw) ** 2) + 12.0 * (sw ** 2)
                toe_bend = 26.0 * ((1.0 - sw) ** 2) - 6.0 * (sw ** 2)
                return Vector((x, y, z)), False, pitch, toe_bend

        # Compute clean double-frequency hip bob + athletic anterior pelvic tilt (compensated 85% at Spine
        # so UpLeg stays strictly inside -20 deg extension while torso stays tall & eyes stay level!)
        pp_rad = math.radians(gait_cfg["pelvis_pitch"])
        b_phase = gait_cfg["bob_phase"]
        for f in range(n_frames + 1):
            u_l = ((float(f) - f_l_strike) % cycle_f) / cycle_f
            bob = -math.cos(4.0 * math.pi * (u_l - b_phase))
            hips_deltas[f].z = gait_cfg["hip_drop"] + gait_cfg["hip_bob"] * bob
            if pp_rad > 1e-4:
                q_tracks[hips][f] = (q_tracks[hips][f] @ Quaternion((1.0, 0.0, 0.0), pp_rad)).normalized()
                q_tracks[P + "Spine"][f] = (Quaternion((1.0, 0.0, 0.0), -pp_rad * 0.85) @ q_tracks[P + "Spine"][f]).normalized()
                if clip_name == "Cast_Run":
                    # Counter-pitch Neck/Head on Cast_Run too so kid keeps eyes on target while running
                    rem = pp_rad * 0.15
                    q_tracks[P + "Neck"][f] = (Quaternion((1.0, 0.0, 0.0), -rem * 0.55) @ q_tracks[P + "Neck"][f]).normalized()
                    q_tracks[P + "Head"][f] = (Quaternion((1.0, 0.0, 0.0), -rem * 0.55) @ q_tracks[P + "Head"][f]).normalized()

        # Solve analytical 2-bone Leg IK for every frame
        for f in range(n_frames + 1):
            lt, l_on, l_pitch, l_toe = foot_target_at(f, f_l_strike, +1.0)
            rt, r_on, r_pitch, r_toe = foot_target_at(f, f_r_strike, -1.0)
            if l_on:
                contact_L.append(f)
            if r_on:
                contact_R.append(f)

            # Recompute upper-root FK to get exact hip socket world positions
            q_f = {bn: q_tracks[bn][f] for bn in skel["order"]}
            wp, wr = compute_fk(skel, hips_deltas[f], q_f)

            # Guarantee both feet are comfortably reachable without straight-locking knees (drop Hips.z only as needed)
            for side, tgt in (("Left", lt), ("Right", rt)):
                hp = wp[P + side + "UpLeg"]
                d_req = (tgt - hp).length
                if d_req > L_leg_max - 0.002:
                    hz_needed = tgt.z + math.sqrt(max(0.01, (L_leg_max - 0.003) ** 2 - (tgt.x - hp.x) ** 2 - (tgt.y - hp.y) ** 2))
                    drop = wp[P + side + "UpLeg"].z - hz_needed
                    if drop > 0.0:
                        hips_deltas[f].z -= drop
                        wp, wr = compute_fk(skel, hips_deltas[f], q_f)

            # Forward knee pole (aligned with pelvis facing -Y, slight natural outward splay 6 deg)
            pelvis_fwd = (wr[hips] @ Vector((0.0, 0.0, 1.0)))  # Hips +Z is world -Y (forward!)
            pelvis_fwd.z = 0.0
            if pelvis_fwd.length < 1e-4:
                pelvis_fwd = Vector((0.0, -1.0, 0.0))
            pelvis_fwd.normalize()

            for side, sgn, tgt, f_pitch, t_bend in (
                ("Left", 1.0, lt, l_pitch, l_toe),
                ("Right", -1.0, rt, r_pitch, r_toe),
            ):
                pole = (pelvis_fwd + Vector((sgn * 0.14, 0.0, 0.0))).normalized()
                qa, qb, wra, wrb = solve_two_bone_ik(
                    skel, wp, wr, P + side + "UpLeg", P + side + "Leg", P + side + "Foot",
                    tgt, pole, flex_min_deg=5.0, flex_max_deg=104.0
                )
                q_tracks[P + side + "UpLeg"][f] = qa
                q_tracks[P + side + "Leg"][f] = qb
                wr[P + side + "UpLeg"] = wra
                wr[P + side + "Leg"] = wrb

                # World-aligned foot orientation (flat on street + heel-strike/toe-roll pitch)
                W_foot_flat = Matrix.Rotation(math.radians(f_pitch), 3, Vector((-1.0, 0.0, 0.0))) @ skel["R_ref"][P + side + "Foot"]
                q_foot = (skel["L_ref"][P + side + "Foot"].transposed() @ wrb.transposed() @ W_foot_flat).to_quaternion().normalized()
                q_tracks[P + side + "Foot"][f] = q_foot
                q_tracks[P + side + "ToeBase"][f] = Quaternion((1.0, 0.0, 0.0), math.radians(t_bend))

    elif mode in ("planted", "cast_stand", "cast_quick", "jump", "victory"):
        # Stationary / planted-foot IK pinning (zero skating on Idle, crouch, Stunned, casts, hit, jump takeoff/land, victory)
        # First ensure Hips never sinks feet underground (fixes baseline crouch z=-0.044!)
        min_foot_z = min(min(wp[P + "LeftFoot"].z, wp[P + "RightFoot"].z) for wp in fk_pos)
        if min_foot_z < foot_z0 - 0.005:
            z_lift = (foot_z0 - min_foot_z)
            for f in range(n_frames + 1):
                hips_deltas[f].z += z_lift
            for f in range(n_frames + 1):
                q_f = {bn: q_tracks[bn][f] for bn in skel["order"]}
                fk_pos[f], fk_rot[f] = compute_fk(skel, hips_deltas[f], q_f)

        # Determine per-side planted intervals and anchor positions
        for side, sgn in (("Left", 1.0), ("Right", -1.0)):
            f_bn = P + side + "Foot"
            zs = [wp[f_bn].z for wp in fk_pos]
            zmin = min(zs)
            if mode == "jump":
                # Jump: feet planted during takeoff (f0..4) and landing absorb (f22..n)
                on_flags = [(f <= 4 or f >= 22) for f in range(n_frames + 1)]
            elif mode == "victory":
                # Victory: ground any frame within 4cm of rest foot Z
                on_flags = [(zs[f] <= zmin + 0.04) for f in range(n_frames + 1)]
            else:
                # Idle, crouch, Stunned, hit, Cast_OneHand, throw, cast_release_*: BOTH feet planted throughout!
                # (Wait: in Cast_OneHand, right foot steps slightly back cleanly — check travel)
                on_flags = [True] * (n_frames + 1)

            # Compute anchor point(s) per contiguous planted run
            targets = [wp[f_bn].copy() for wp in fk_pos]
            # Split into contiguous True runs
            runs = []
            cur = []
            for f, flag in enumerate(on_flags):
                if flag:
                    cur.append(f)
                elif cur:
                    runs.append(cur)
                    cur = []
            if cur:
                runs.append(cur)
            # If looping and both frame 0 and frame N are planted, unify their anchor!
            if loop and len(runs) >= 2 and runs[0][0] == 0 and runs[-1][-1] == n_frames:
                merged = runs[-1] + runs[0]
                runs = [merged] + runs[1:-1]

            for r in runs:
                # Anchor at median X, median Y, exact ground height foot_z0
                ax = sum(fk_pos[i][f_bn].x for i in r) / len(r)
                ay = sum(fk_pos[i][f_bn].y for i in r) / len(r)
                # Ensure slight minimum stance width so thighs never collide
                if sgn > 0:
                    ax = max(ax, 0.115)
                else:
                    ax = min(ax, -0.115)
                anchor = Vector((ax, ay, foot_z0))
                for i in r:
                    targets[i] = anchor.copy()

            # Smooth 2-frame transition at takeoff/landing boundaries for jump/victory
            if mode in ("jump", "victory"):
                for f in range(n_frames + 1):
                    if not on_flags[f]:
                        # Keep airborne height >= foot_z0 + 0.02
                        targets[f].z = max(targets[f].z, foot_z0 + 0.02)
                # Blend 2 frames around contact transitions
                for _ in range(2):
                    pinned_set = {f for f in range(n_frames + 1) if on_flags[f]}
                    targets = smooth_vec_seq(targets, 0.4, loop, pinned=pinned_set)

            if side == "Left":
                contact_L = [f for f, fl in enumerate(on_flags) if fl]
                lt_list = targets
                l_on_list = on_flags
            else:
                contact_R = [f for f, fl in enumerate(on_flags) if fl]
                rt_list = targets
                r_on_list = on_flags

        # Solve analytical 2-bone Leg IK on every frame
        for f in range(n_frames + 1):
            q_f = {bn: q_tracks[bn][f] for bn in skel["order"]}
            wp, wr = compute_fk(skel, hips_deltas[f], q_f)

            # Adjust Hips.z only if a planted foot would over-straighten (< 6 deg knee bend)
            for side, tgt, is_on in (("Left", lt_list[f], l_on_list[f]), ("Right", rt_list[f], r_on_list[f])):
                if not is_on:
                    continue
                hp = wp[P + side + "UpLeg"]
                d_req = (tgt - hp).length
                if d_req > L_leg_max - 0.002:
                    hz_needed = tgt.z + math.sqrt(max(0.01, (L_leg_max - 0.003) ** 2 - (tgt.x - hp.x) ** 2 - (tgt.y - hp.y) ** 2))
                    drop = hp.z - hz_needed
                    if drop > 0.0:
                        hips_deltas[f].z -= drop
                        wp, wr = compute_fk(skel, hips_deltas[f], q_f)

            pelvis_fwd = (wr[hips] @ Vector((0.0, 0.0, 1.0)))
            pelvis_fwd.z = 0.0
            if pelvis_fwd.length < 1e-4:
                pelvis_fwd = Vector((0.0, -1.0, 0.0))
            pelvis_fwd.normalize()

            for side, sgn, tgt, is_on in (
                ("Left", 1.0, lt_list[f], l_on_list[f]),
                ("Right", -1.0, rt_list[f], r_on_list[f]),
            ):
                pole = (pelvis_fwd + Vector((sgn * 0.16, 0.0, 0.0))).normalized()
                qa, qb, wra, wrb = solve_two_bone_ik(
                    skel, wp, wr, P + side + "UpLeg", P + side + "Leg", P + side + "Foot",
                    tgt, pole, flex_min_deg=4.5, flex_max_deg=104.0
                )
                q_tracks[P + side + "UpLeg"][f] = qa
                q_tracks[P + side + "Leg"][f] = qb
                wr[P + side + "UpLeg"] = wra
                wr[P + side + "Leg"] = wrb
                if is_on:
                    W_foot_flat = skel["R_ref"][P + side + "Foot"]
                    q_tracks[P + side + "Foot"][f] = (
                        skel["L_ref"][P + side + "Foot"].transposed() @ wrb.transposed() @ W_foot_flat
                    ).to_quaternion().normalized()
                    q_tracks[P + side + "ToeBase"][f] = Quaternion((1.0, 0.0, 0.0), 0.0)
                else:
                    W_foot_orig = fk_rot[f][P + side + "Foot"]
                    q_tracks[P + side + "Foot"][f] = (
                        skel["L_ref"][P + side + "Foot"].transposed() @ wrb.transposed() @ W_foot_orig
                    ).to_quaternion().normalized()

    else:
        # slide / trip / Tripped / Airborne: fix underground clipping without floating in mid-air,
        # and enforce pure knee hinge (>= 5 deg, 0 deg sideways!).
        for f in range(n_frames + 1):
            q_f = {bn: q_tracks[bn][f] for bn in skel["order"]}
            wp, wr = compute_fk(skel, hips_deltas[f], q_f)
            if mode == "slide":
                # Keep Hips LOW at true ground-slide height (never jacking the kid +16cm into mid-air!).
                # Instead, solve 2-bone Leg IK directly on ground-clamped foot targets:
                #   - back tucked LeftFoot lifted cleanly onto z >= 0.085 (never underground!)
                #   - front extended RightFoot skimming cleanly along street z = 0.095..0.11 (never floating!)
                # Smooth blend factor s_w: 0 at standing f=0/n, 1 across core slide f=5..25.
                s_w = min(1.0, min(f, n_frames - f) / 5.0)
                # Keep Hips cleanly above floor (min 0.38m)
                if wp[P + "Hips"].z < 0.38:
                    hips_deltas[f].z += (0.38 - wp[P + "Hips"].z)
                    wp, wr = compute_fk(skel, hips_deltas[f], q_f)
                for side, sgn in (("Left", 1.0), ("Right", -1.0)):
                    tgt = wp[P + side + "Foot"].copy()
                    if side == "Left":
                        # Tucked back foot: rescue from underground (-0.118m) onto street surface (+0.085m)
                        tgt.z = max(tgt.z, 0.088)
                    else:
                        # Extended front sliding foot: skim low along street (+0.095m) instead of kicking mid-air
                        tgt.z = tgt.z * (1.0 - s_w) + max(0.090, min(tgt.z, 0.115)) * s_w
                    orig_knee = wp[P + side + "Leg"] - (wp[P + side + "UpLeg"] + wp[P + side + "Foot"]) * 0.5
                    if orig_knee.length > 1e-4:
                        pole = orig_knee.normalized()
                    else:
                        pole = (pelvis_fwd + Vector((sgn * 0.25, 0.0, 0.0))).normalized()
                    qa, qb, wra, wrb = solve_two_bone_ik(
                        skel, wp, wr, P + side + "UpLeg", P + side + "Leg", P + side + "Foot",
                        tgt, pole, flex_min_deg=6.0, flex_max_deg=104.0
                    )
                    q_tracks[P + side + "UpLeg"][f] = qa
                    q_tracks[P + side + "Leg"][f] = qb
                    wr[P + side + "UpLeg"] = wra
                    wr[P + side + "Leg"] = wrb
                    W_foot_orig = fk_rot[f][P + side + "Foot"]
                    q_tracks[P + side + "Foot"][f] = (
                        skel["L_ref"][P + side + "Foot"].transposed() @ wrb.transposed() @ W_foot_orig
                    ).to_quaternion().normalized()
                continue
            else:
                min_z = min(wp[P + "LeftFoot"].z, wp[P + "RightFoot"].z)
                floor_z = 0.055 if mode == "fall" else foot_z0
                if min_z < floor_z:
                    hips_deltas[f].z += (floor_z - min_z)
                    wp, wr = compute_fk(skel, hips_deltas[f], q_f)

            pelvis_fwd = (wr[hips] @ Vector((0.0, 0.0, 1.0)))
            pelvis_fwd.z = 0.0
            if pelvis_fwd.length < 1e-4:
                pelvis_fwd = Vector((0.0, -1.0, 0.0))
            pelvis_fwd.normalize()

            for side, sgn in (("Left", 1.0), ("Right", -1.0)):
                tgt = wp[P + side + "Foot"].copy()
                # Use original knee direction blended with pelvis forward as pole
                orig_knee = wp[P + side + "Leg"] - (wp[P + side + "UpLeg"] + tgt) * 0.5
                pole = (orig_knee.normalized() * 0.5 + (pelvis_fwd + Vector((sgn * 0.18, 0.0, 0.0))).normalized() * 0.5).normalized()
                qa, qb, wra, wrb = solve_two_bone_ik(
                    skel, wp, wr, P + side + "UpLeg", P + side + "Leg", P + side + "Foot",
                    tgt, pole, flex_min_deg=5.0, flex_max_deg=104.0
                )
                q_tracks[P + side + "UpLeg"][f] = qa
                q_tracks[P + side + "Leg"][f] = qb
                wr[P + side + "UpLeg"] = wra
                wr[P + side + "Leg"] = wrb
                W_foot_orig = fk_rot[f][P + side + "Foot"]
                q_tracks[P + side + "Foot"][f] = (
                    skel["L_ref"][P + side + "Foot"].transposed() @ wrb.transposed() @ W_foot_orig
                ).to_quaternion().normalized()
        if mode == "slide":
            # Baseball slide glides horizontally across street (no stationary foot plant during glide).
            contact_L = []
            contact_R = []

    # ---- PART B: ANALYTICAL 2-BONE ARM IK (ELIMINATES ALL SIDEWAYS ELBOWS + SMOOTHS HAND ARCS!) --------
    # Recompute FK after leg/spine updates to extract updated shoulder positions while preserving authored hand targets
    hand_targets = {"Left": [], "Right": []}
    hand_rots = {"Left": [], "Right": []}
    elbow_hints = {"Left": [], "Right": []}
    for f in range(n_frames + 1):
        q_f = {bn: q_tracks[bn][f] for bn in skel["order"]}
        wp, wr = compute_fk(skel, hips_deltas[f], q_f)
        for side, sgn in (("Left", 1.0), ("Right", -1.0)):
            sh_p = wp[P + side + "Arm"]
            el_p = wp[P + side + "ForeArm"]
            ha_p = wp[P + side + "Hand"]
            hand_targets[side].append(ha_p.copy())
            hand_rots[side].append(wr[P + side + "Hand"].copy())
            # Anatomical elbow pole: tucked back-and-down during locomotion (never boxy robot wings!),
            # more faithful to authored elbow plane on expressive/hand-on-hip poses (Idle/victory/casts).
            chest_rot = wr[P + "Spine2"]
            # In Spine2 local: +X=Left, +Y=Up, +Z=Forward(-Y world) -> Back is -Z, Out is +sgn*X, Down is -Y
            if mode in ("loco", "cast_run"):
                anat_pole = (chest_rot @ Vector((sgn * 0.24, -0.48, -0.84))).normalized()
                orig_w = 0.30
            else:
                anat_pole = (chest_rot @ Vector((sgn * 0.62, -0.38, -0.68))).normalized()
                orig_w = 0.65
            orig_pole = el_p - (sh_p + ha_p) * 0.5
            if orig_pole.length > 0.02:
                orig_pole.normalize()
                # Reject anatomically inverted elbow poles (elbow pointing straight forward!)
                if orig_pole.dot(anat_pole) > -0.25:
                    pole = (orig_pole * orig_w + anat_pole * (1.0 - orig_w)).normalized()
                else:
                    pole = anat_pole
            else:
                pole = anat_pole
            elbow_hints[side].append(pole)

    # A2 Step 5: Clean hand arcs (mild 3-tap arc smoothing on hand path, pinning cast release frame!)
    pinned_hands = {release_frame} if release_frame >= 0 else set()
    for side in ("Left", "Right"):
        hand_targets[side] = smooth_vec_seq(hand_targets[side], 0.25, loop, pinned=pinned_hands)
        elbow_hints[side] = [v.normalized() for v in smooth_vec_seq(elbow_hints[side], 0.35, loop)]
        # Subtle outward clearance on sprint/Run pump (clean silhouette without boxy wings)
        if clip_name in ("Run", "sprint", "Cast_Run"):
            out_pad = 0.016 if clip_name == "sprint" else 0.010
            sgn = 1.0 if side == "Left" else -1.0
            for f in range(n_frames + 1):
                if clip_name == "Cast_Run" and side == "Right" and 16 <= f <= 42:
                    continue
                hand_targets[side][f].x += sgn * out_pad

    # Solve analytical 2-bone Arm IK on every frame (pure 1-DOF elbow hinge + anatomical humeral twist!)
    for f in range(n_frames + 1):
        q_f = {bn: q_tracks[bn][f] for bn in skel["order"]}
        wp, wr = compute_fk(skel, hips_deltas[f], q_f)
        for side, sgn in (("Left", 1.0), ("Right", -1.0)):
            qa, qb, wra, wrb = solve_two_bone_ik(
                skel, wp, wr, P + side + "Arm", P + side + "ForeArm", P + side + "Hand",
                hand_targets[side][f], elbow_hints[side][f],
                flex_min_deg=6.0, flex_max_deg=138.0
            )
            q_tracks[P + side + "Arm"][f] = qa
            q_tracks[P + side + "ForeArm"][f] = qb
            wr[P + side + "Arm"] = wra
            wr[P + side + "ForeArm"] = wrb
            # Preserve hand world orientation (twist will ramp cleanly along ForeArmTwist1/2!)
            W_hand_want = hand_rots[side][f]
            q_hand = (skel["L_ref"][P + side + "Hand"].transposed() @ wrb.transposed() @ W_hand_want).to_quaternion().normalized()
            q_tracks[P + side + "Hand"][f] = q_hand

    if loop:
        hips_deltas[n_frames] = hips_deltas[0].copy()
        for bn in q_tracks:
            q_tracks[bn][n_frames] = q_tracks[bn][0].copy()
    for bn in q_tracks:
        q_tracks[bn] = ensure_continuous_quats(q_tracks[bn])

    return stride_distance_m, stride_speed_mps, contact_L, contact_R


def stage_limit_clamp_and_penetration(clip_name, spec, skel, lims, caps, n_frames, hips_deltas, q_tracks, release_frame,
                                      contacts_L=None, contacts_R=None):
    """
    A5 Swing-Twist Limit Clamping + Capsule Penetration Push-out (smoothed +-2f) + Pop Suppression (<= 22 deg/f).
    Preserves stance-locked leg frames so foot slide stays <= 2 cm!
    Returns (overshoot_logs, penetration_fixes_count).
    """
    loop = spec["loop"]
    overshoot_logs = []
    lock_L = set(contacts_L or ())
    lock_R = set(contacts_R or ())
    left_leg_bones = {P + "LeftUpLeg", P + "LeftLeg", P + "LeftFoot", P + "LeftToeBase"}
    right_leg_bones = {P + "RightUpLeg", P + "RightLeg", P + "RightFoot", P + "RightToeBase"}

    def clamp_all_frames(record_logs=False):
        for bn in skel["order"]:
            short = bn[len(P):] if bn.startswith(P) else bn
            if short not in lims:
                continue
            lim = lims[short]
            qs = q_tracks[bn]
            for f in range(n_frames + 1):
                qc, ov, det = clamp_joint_swing_twist(qs[f], lim, enforce_soft_min=True)
                qs[f] = qc
                if record_logs and ov > 10.0:
                    overshoot_logs.append(f"{clip_name}:{short}@f{f}:{ov:.1f}deg")
            if loop:
                qs[n_frames] = qs[0].copy()
            q_tracks[bn] = ensure_continuous_quats(qs)

    # First limit clamp (logs any source overshoots > 10 deg)
    clamp_all_frames(record_logs=True)

    # A5 Capsule Penetration Pass: solve minimum Arm abduct & UpLeg spread (+5mm margin), smooth over +-2 frames
    add_arm = {"Left": [0.0] * (n_frames + 1), "Right": [0.0] * (n_frames + 1)}
    add_leg = {"Left": [0.0] * (n_frames + 1), "Right": [0.0] * (n_frames + 1)}
    pen_fixes = 0

    for f in range(n_frames + 1):
        q_f = {bn: q_tracks[bn][f] for bn in skel["order"]}
        wp, wr = compute_fk(skel, hips_deltas[f], q_f)
        pen = test_capsule_penetrations(skel, wp, wr, caps)

        # Resolve arm vs torso by smallest abduction increment
        for side, sgn in (("Left", 1.0), ("Right", -1.0)):
            if pen[side + "Arm"] > 0.0005:
                bn = P + side + "Arm"
                lim = lims[side + "Arm"]
                ab_ax = Vector(lim["abductAxis"]).normalized()
                base_q = q_f[bn].copy()
                solved = 0.0
                for deg in range(2, 46, 2):
                    q_try, _, _ = clamp_joint_swing_twist(
                        (Quaternion(ab_ax, math.radians(deg)) @ base_q).normalized(), lim
                    )
                    q_f[bn] = q_try
                    wp2, wr2 = compute_fk(skel, hips_deltas[f], q_f)
                    if test_capsule_penetrations(skel, wp2, wr2, caps)[side + "Arm"] <= -0.005:
                        solved = float(deg)
                        break
                    solved = float(deg)
                q_f[bn] = base_q
                add_arm[side][f] = solved
                pen_fixes += 1

        # Resolve thigh/shin vs opposite thigh/shin by symmetric UpLeg abduction spread
        if pen["LeftUpLeg"] > 0.0005:
            base_l = q_f[P + "LeftUpLeg"].copy()
            base_r = q_f[P + "RightUpLeg"].copy()
            ax_l = Vector(lims["LeftUpLeg"]["abductAxis"]).normalized()
            ax_r = Vector(lims["RightUpLeg"]["abductAxis"]).normalized()
            solved = 0.0
            for deg in range(1, 26, 1):
                ql, _, _ = clamp_joint_swing_twist((Quaternion(ax_l, math.radians(deg)) @ base_l).normalized(), lims["LeftUpLeg"])
                qr, _, _ = clamp_joint_swing_twist((Quaternion(ax_r, math.radians(deg)) @ base_r).normalized(), lims["RightUpLeg"])
                q_f[P + "LeftUpLeg"] = ql
                q_f[P + "RightUpLeg"] = qr
                wp2, wr2 = compute_fk(skel, hips_deltas[f], q_f)
                if test_capsule_penetrations(skel, wp2, wr2, caps)["LeftUpLeg"] <= -0.004:
                    solved = float(deg)
                    break
                solved = float(deg)
            q_f[P + "LeftUpLeg"] = base_l
            q_f[P + "RightUpLeg"] = base_r
            add_leg["Left"][f] = solved
            add_leg["Right"][f] = solved
            pen_fixes += 1

    # Smooth push-out angles over +-2 frames (taking max with smoothed so clearance never drops below needed!)
    if pen_fixes > 0:
        for side in ("Left", "Right"):
            sm_a = smooth_scalar_seq(add_arm[side], 2, loop)
            sm_l = smooth_scalar_seq(add_leg[side], 2, loop)
            # Keep max(raw, smoothed) then gently smooth once more so clearance holds with zero pop
            comb_a = smooth_scalar_seq([max(a, b) for a, b in zip(add_arm[side], sm_a)], 1, loop)
            comb_l = smooth_scalar_seq([max(a, b) for a, b in zip(add_leg[side], sm_l)], 1, loop)
            ab_arm = Vector(lims[side + "Arm"]["abductAxis"]).normalized()
            ab_leg = Vector(lims[side + "UpLeg"]["abductAxis"]).normalized()
            for f in range(n_frames + 1):
                if comb_a[f] > 0.1:
                    q_tracks[P + side + "Arm"][f] = (Quaternion(ab_arm, math.radians(comb_a[f])) @ q_tracks[P + side + "Arm"][f]).normalized()
                if comb_l[f] > 0.1:
                    q_tracks[P + side + "UpLeg"][f] = (Quaternion(ab_leg, math.radians(comb_l[f])) @ q_tracks[P + side + "UpLeg"][f]).normalized()

    # Pop suppression pass: guarantee ZERO inter-frame joint jumps > 25 deg A8 gate!
    # Keep stance-locked leg frames pinned (allow up to 24.2 deg/f during fast sprint stance) so foot slide stays <= 2 cm!
    for _pass in range(14):
        any_pop = False
        for bn in skel["order"]:
            qs = q_tracks[bn]
            locked_f = lock_L if bn in left_leg_bones else (lock_R if bn in right_leg_bones else set())
            for f in range(1, n_frames + 1):
                ang = math.degrees(qs[f - 1].rotation_difference(qs[f]).angle)
                both_locked = ((f - 1) in locked_f) and (f in locked_f)
                thresh = 24.2 if both_locked else 22.5
                if ang > thresh:
                    any_pop = True
                    can_prev = (f - 1 != release_frame) and (loop or f - 1 > 0) and ((f - 1) not in locked_f)
                    can_curr = (f != release_frame) and (loop or f < n_frames) and (f not in locked_f)
                    if not can_prev and not can_curr:
                        qs[f] = qs[f - 1].slerp(qs[f], 0.90).normalized()
                    else:
                        idx_prev = (f - 2) % n_frames if loop else max(0, f - 2)
                        idx_next = (f + 1) % n_frames if loop else min(n_frames, f + 1)
                        if can_prev:
                            qs[f - 1] = qs[idx_prev].slerp(qs[f], 0.5).normalized()
                        if can_curr:
                            qs[f] = qs[f - 1].slerp(qs[idx_next], 0.5).normalized()
            if loop:
                qs[n_frames] = qs[0].copy()
                if n_frames >= 2:
                    ang_wrap = math.degrees(qs[n_frames - 1].rotation_difference(qs[0]).angle)
                    if ang_wrap > 22.5:
                        any_pop = True
                        if (n_frames - 1) not in locked_f:
                            qs[n_frames - 1] = qs[n_frames - 2].slerp(qs[0], 0.5).normalized()
                        if 1 not in locked_f:
                            qs[1] = qs[0].slerp(qs[2], 0.5).normalized()
            q_tracks[bn] = ensure_continuous_quats(qs)
        if not any_pop:
            break

    # Final exact swing-twist limit clamp so every single frame sits 100% inside rig__joint_limits.json!
    clamp_all_frames(record_logs=False)
    return overshoot_logs, pen_fixes


# ---- R2 Helper & Twist Bone Baking (Exact Sign Lock per #21) ----------------------------------------------------

def add_r2_helper_twist_bones(arm):
    """Add the 12 R2 helper/twist bones to roster_clips.glb's armature matching contract_kid.py."""
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm.data.edit_bones

    def ensure_bone(name, parent_name, head, tail, roll_like):
        if name in eb:
            nb = eb[name]
        else:
            nb = eb.new(name)
        nb.head = head.copy()
        nb.tail = tail.copy()
        nb.parent = eb[parent_name]
        nb.use_connect = False
        nb.roll = eb[roll_like].roll
        return nb

    added = []
    for s in ("Left", "Right"):
        A_, F_, H_, U_, L_ = (P + s + x for x in ("Arm", "ForeArm", "Hand", "UpLeg", "Leg"))
        if not all(n in eb for n in (A_, F_, H_, U_, L_)):
            continue
        a, f, h, u, l = eb[A_], eb[F_], eb[H_], eb[U_], eb[L_]
        # Use joint-to-joint spans so twist bone heads/tails sit accurately on the anatomical segments
        ensure_bone(P + s + "ArmTwist", A_, a.head.lerp(f.head, 0.5), f.head, A_)
        ensure_bone(P + s + "ForeArmTwist1", F_, f.head.lerp(h.head, 1.0 / 3.0), f.head.lerp(h.head, 2.0 / 3.0), F_)
        ensure_bone(P + s + "ForeArmTwist2", F_, f.head.lerp(h.head, 2.0 / 3.0), h.head, F_)
        ensure_bone(P + s + "ForeArmHelper", A_, f.head, f.head + (h.head - f.head) * 0.25, F_)
        ensure_bone(P + s + "LegHelper", U_, l.head, l.head + (eb[P + s + "Foot"].head - l.head) * 0.25, L_)
        ensure_bone(P + s + "HandHelper", F_, h.head, h.head + (h.tail - h.head) * 0.5, H_)
        added += [P + s + x for x in ("ArmTwist", "ForeArmTwist1", "ForeArmTwist2", "ForeArmHelper", "LegHelper", "HandHelper")]

    bpy.ops.object.mode_set(mode="OBJECT")
    return added


def compute_helper_twist_tracks(n_frames, q_tracks, lims):
    """
    Compute exact R2 helper & twist local rotations per #21 sign lock:
      - ArmTwist:      armTwist^(-0.5)   (proximal upper arm counter-twist -> 50% world twist)
      - ForeArmTwist1: (faTwist + handTwist)^(+0.33) about ForeArm.twistAxis (matches ForeArm roll!)
      - ForeArmTwist2: (faTwist + handTwist)^(+0.66) about ForeArm.twistAxis (matches ForeArm roll!)
      - ForeArmHelper: slerp(Identity, q_ForeArm, 0.5)
      - LegHelper:     slerp(Identity, q_Leg, 0.5)
      - HandHelper:    slerp(Identity, q_Hand, 0.5)
    """
    ident = Quaternion((1.0, 0.0, 0.0, 0.0))
    extra = {}
    for s in ("Left", "Right"):
        arm_t_ax = Vector(lims[s + "Arm"]["twistAxis"]).normalized()
        fa_t_ax = Vector(lims[s + "ForeArm"]["twistAxis"]).normalized()
        hand_t_ax = Vector(lims[s + "Hand"]["twistAxis"]).normalized()
        at_seq, ft1_seq, ft2_seq = [], [], []
        fh_seq, lh_seq, hh_seq = [], [], []
        for f in range(n_frames + 1):
            _, _, arm_tw = swing_twist_decompose(q_tracks[P + s + "Arm"][f], arm_t_ax)
            _, _, fa_tw = swing_twist_decompose(q_tracks[P + s + "ForeArm"][f], fa_t_ax)
            _, _, hand_tw = swing_twist_decompose(q_tracks[P + s + "Hand"][f], hand_t_ax)
            tot_fa_tw = fa_tw + hand_tw
            at_seq.append(Quaternion(arm_t_ax, -0.50 * arm_tw))
            ft1_seq.append(Quaternion(fa_t_ax, 0.33 * tot_fa_tw))
            ft2_seq.append(Quaternion(fa_t_ax, 0.66 * tot_fa_tw))

            qf = q_tracks[P + s + "ForeArm"][f]
            if qf.w < 0.0:
                qf = -qf
            ql = q_tracks[P + s + "Leg"][f]
            if ql.w < 0.0:
                ql = -ql
            qh = q_tracks[P + s + "Hand"][f]
            if qh.w < 0.0:
                qh = -qh
            fh_seq.append(ident.slerp(qf, 0.5).normalized())
            lh_seq.append(ident.slerp(ql, 0.5).normalized())
            hh_seq.append(ident.slerp(qh, 0.5).normalized())

        extra[P + s + "ArmTwist"] = ensure_continuous_quats(at_seq)
        extra[P + s + "ForeArmTwist1"] = ensure_continuous_quats(ft1_seq)
        extra[P + s + "ForeArmTwist2"] = ensure_continuous_quats(ft2_seq)
        extra[P + s + "ForeArmHelper"] = ensure_continuous_quats(fh_seq)
        extra[P + s + "LegHelper"] = ensure_continuous_quats(lh_seq)
        extra[P + s + "HandHelper"] = ensure_continuous_quats(hh_seq)
    return extra


# ---- Keyframe Reduction (A2 Step 1 & A3: 0.5 deg / 0.5 mm, ZERO scale curves) -----------------------------------

def _rdp_quat_indices(seq, tol_rad, pinned):
    """Ramer-Douglas-Peucker key reduction on quaternion sequence preserving pinned frames."""
    n = len(seq) - 1
    keep = {0, n} | {i for i in pinned if 0 <= i <= n}

    def recurse(i0, i1):
        if i1 <= i0 + 1:
            return
        q0, q1 = seq[i0], seq[i1]
        if q0.dot(q1) < 0.0:
            q1 = -q1
        max_err = 0.0
        best_i = -1
        span = float(i1 - i0)
        for i in range(i0 + 1, i1):
            t = (i - i0) / span
            qi = q0.slerp(q1, t)
            err = qi.rotation_difference(seq[i]).angle
            if err > max_err:
                max_err = err
                best_i = i
        if max_err > tol_rad and best_i > 0:
            keep.add(best_i)
            recurse(i0, best_i)
            recurse(best_i, i1)

    sorted_pins = sorted(keep)
    for a, b in zip(sorted_pins, sorted_pins[1:]):
        recurse(a, b)
    return sorted(keep)


def _rdp_vec_indices(seq, tol_m, pinned):
    n = len(seq) - 1
    keep = {0, n} | {i for i in pinned if 0 <= i <= n}

    def recurse(i0, i1):
        if i1 <= i0 + 1:
            return
        v0, v1 = seq[i0], seq[i1]
        max_err = 0.0
        best_i = -1
        span = float(i1 - i0)
        for i in range(i0 + 1, i1):
            t = (i - i0) / span
            err = (v0.lerp(v1, t) - seq[i]).length
            if err > max_err:
                max_err = err
                best_i = i
        if max_err > tol_m and best_i > 0:
            keep.add(best_i)
            recurse(i0, best_i)
            recurse(best_i, i1)

    sorted_pins = sorted(keep)
    for a, b in zip(sorted_pins, sorted_pins[1:]):
        recurse(a, b)
    return sorted(keep)


def bake_action_to_armature(arm, skel, clip_name, n_frames, hips_deltas, q_tracks, extra_tracks, pinned_frames):
    """
    Convert ref_rest local quaternions back to arm's local bone space, apply 0.5 deg / 0.5 mm
    keyframe reduction, write ZERO scale curves, and create a clean Blender 5.2 Action.
    """
    act = bpy.data.actions.new(clip_name)
    act.use_fake_user = True
    if arm.animation_data is None:
        arm.animation_data_create()
    arm.animation_data.action = act

    all_bones = list(skel["order"]) + [bn for bn in extra_tracks if bn in arm.pose.bones and bn not in skel["order"]]
    # Precompute arm-space quaternions for every frame so hemisphere continuity & RDP reduction are exact!
    arm_quats = {bn: [] for bn in all_bones}
    arm_hips_loc = []
    hips_bn = P + "Hips"
    R_src_hips_T = skel["R_src"][hips_bn].transposed()

    for f in range(n_frames + 1):
        q_f = {bn: q_tracks[bn][f] for bn in skel["order"]}
        _, W_rot = compute_fk(skel, hips_deltas[f], q_f)
        arm_hips_loc.append(R_src_hips_T @ hips_deltas[f])
        for bn in skel["order"]:
            p = skel["parent_of"][bn]
            if p is None:
                M = skel["R_src"][bn].transposed() @ W_rot[bn]
            else:
                M = skel["L_src"][bn].transposed() @ W_rot[p].transposed() @ W_rot[bn]
            arm_quats[bn].append(M.to_quaternion().normalized())
        for bn, seq in extra_tracks.items():
            if bn in arm_quats:
                arm_quats[bn].append(seq[f].normalized())

    for bn in all_bones:
        arm_quats[bn] = ensure_continuous_quats(arm_quats[bn])

    # Keyframe reduction (A2/A3: 0.5 deg rotation, 0.5 mm position; keep dense keys on IK legs/hips for <= 2cm foot lock!)
    tol_rot = math.radians(0.45)
    tol_leg = math.radians(0.20)  # tighter on legs so glTF interpolation never introduces foot slide!
    tol_pos = 0.0004
    leg_set = {P + s + x for s in ("Left", "Right") for x in ("UpLeg", "Leg", "Foot", "ToeBase")} | {hips_bn}

    # Write Hips location
    pb_hips = arm.pose.bones[hips_bn]
    pb_hips.rotation_mode = "QUATERNION"
    h_idx = _rdp_vec_indices(arm_hips_loc, tol_pos, pinned_frames)
    for f in h_idx:
        pb_hips.location = arm_hips_loc[f]
        pb_hips.keyframe_insert("location", frame=f)

    # Write bone rotations (ZERO scale curves!)
    keys_written = len(h_idx)
    for bn in all_bones:
        pb = arm.pose.bones.get(bn)
        if pb is None:
            continue
        pb.rotation_mode = "QUATERNION"
        tol = tol_leg if bn in leg_set else tol_rot
        k_idx = _rdp_quat_indices(arm_quats[bn], tol, pinned_frames)
        for f in k_idx:
            pb.rotation_quaternion = arm_quats[bn][f]
            pb.keyframe_insert("rotation_quaternion", frame=f)
        keys_written += len(k_idx)

    # Set linear/auto clean handles with zero overshoot ringing on glTF linear sampling
    if hasattr(act, "layers"):
        for layer in act.layers:
            for strip in layer.strips:
                for cb in strip.channelbags:
                    for fc in cb.fcurves:
                        for kp in fc.keyframe_points:
                            kp.interpolation = "LINEAR"

    arm.animation_data.action = None
    return act, keys_written


# ---- A3 Per-Kid Runtime Adaptation Measurements (All 12 Kids) ---------------------------------------------------

def measure_all_12_kids(chars_dir, ref_leg_len, lims):
    """
    Sequentially inspect all 12 kids in assets/models/characters/*.glb to measure:
      - legLengthM & legLengthRatio (vs reference kid, for A3 Hips height scaling)
      - girthArmAbductDeg & girthHipSpreadDeg (exact minimum clearance + 1 cm so chubbier kids
        like Domeng never clip arms into belly or thighs together!)
      - aimYawMaxDeg & aimPitchMaxDeg (from rig__joint_limits Spine+Spine2+Neck budgets).
    """
    # Compute aim budget directly from limits (40/30/30 split across Spine, Spine2, Neck per A6)
    aim_yaw = sum(float(lims[b]["twist"][1]) for b in ("Spine", "Spine2", "Neck") if b in lims)
    aim_pitch = sum(min(abs(float(lims[b]["flex"][0])), float(lims[b]["flex"][1])) for b in ("Spine", "Spine2", "Neck") if b in lims)
    aim_yaw = min(65.0, round(aim_yaw * 0.75, 1))
    aim_pitch = min(40.0, round(aim_pitch * 0.65, 1))

    kids_out = {}
    for path in sorted(glob.glob(os.path.join(chars_dir, "*.glb"))):
        kid_id = os.path.splitext(os.path.basename(path))[0].lower()
        if "_lod" in kid_id:
            continue
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=path)
        arm = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
        mesh = max((o for o in bpy.data.objects if o.type == "MESH" and o.vertex_groups),
                   key=lambda o: len(o.data.vertices), default=None)
        if arm is None or mesh is None:
            continue
        AM = arm.matrix_world
        MM = mesh.matrix_world
        B = arm.data.bones
        if P + "LeftUpLeg" not in B or P + "LeftFoot" not in B:
            continue
        leg_len = (AM @ B[P + "LeftUpLeg"].head_local - AM @ B[P + "LeftFoot"].head_local).length
        leg_ratio = leg_len / max(ref_leg_len, 1e-4)

        # Measure true medial-facing torso half-width (excluding capes/backpacks), medial arm radius,
        # and medial thigh radius (below skirt hem) so slim kids stay 0 deg and chubby kids get exact clearance!
        torso_g = {mesh.vertex_groups[P + n].index for n in ("Hips", "Spine", "Spine1", "Spine2") if P + n in mesh.vertex_groups}
        extra_g = {g.index for g in mesh.vertex_groups if not g.name.startswith(P)}
        hz = (AM @ B[P + "Hips"].head_local).z
        s2z = (AM @ B[P + "Spine2"].head_local).z
        torso_xs = []
        for v in mesh.data.vertices:
            if any(g.group in extra_g and g.weight > 0.15 for g in v.groups):
                continue
            tw = sum(g.weight for g in v.groups if g.group in torso_g)
            if tw > 0.60:
                p = MM @ v.co
                if hz + 0.03 <= p.z <= s2z - 0.03 and abs(p.y) < 0.16:
                    torso_xs.append(abs(p.x))
        torso_xs.sort()
        torso_rx = torso_xs[int(0.80 * (len(torso_xs) - 1))] if torso_xs else 0.135

        def medial_limb_r(b1, b2, side_sign, t0=0.35, t1=0.75, pct=0.55, cap_max=0.095, default=0.052):
            if P + b1 not in B or P + b2 not in B:
                return default
            vg = mesh.vertex_groups.get(P + b1)
            if vg is None:
                return default
            a = AM @ B[P + b1].head_local
            b = AM @ B[P + b2].head_local
            ab = b - a
            ab2 = max(ab.dot(ab), 1e-8)
            gi = vg.index
            ds = []
            for v in mesh.data.vertices:
                if any(g.group in extra_g and g.weight > 0.15 for g in v.groups):
                    continue
                w = next((g.weight for g in v.groups if g.group == gi), 0.0)
                if w > 0.55:
                    p = MM @ v.co
                    t = (p - a).dot(ab) / ab2
                    if t0 <= t <= t1:
                        c = a + ab * t
                        # Medial-facing side (toward body centerline x=0) avoids outer baggy sleeves/pockets
                        if (p.x - c.x) * (-side_sign) >= -0.01:
                            ds.append((p - c).length)
            if not ds:
                return default
            ds.sort()
            return min(cap_max, ds[int(pct * (len(ds) - 1))])

        arm_r = 0.5 * (
            medial_limb_r("LeftArm", "LeftForeArm", +1.0, 0.30, 0.78, 0.55, 0.082, 0.048)
            + medial_limb_r("RightArm", "RightForeArm", -1.0, 0.30, 0.78, 0.55, 0.082, 0.048)
        )
        thigh_r = 0.5 * (
            medial_limb_r("LeftUpLeg", "LeftLeg", +1.0, 0.42, 0.82, 0.55, 0.115, 0.072)
            + medial_limb_r("RightUpLeg", "RightLeg", -1.0, 0.42, 0.82, 0.55, 0.115, 0.072)
        )
        arm_len = (AM @ B[P + "LeftForeArm"].head_local - AM @ B[P + "LeftArm"].head_local).length
        thigh_len = (AM @ B[P + "LeftLeg"].head_local - AM @ B[P + "LeftUpLeg"].head_local).length
        sh_x = 0.5 * (abs((AM @ B[P + "LeftArm"].head_local).x) + abs((AM @ B[P + "RightArm"].head_local).x))
        hip_x = 0.5 * (abs((AM @ B[P + "LeftUpLeg"].head_local).x) + abs((AM @ B[P + "RightUpLeg"].head_local).x))

        # Tightest shared-clip pose check (A3):
        # 1) Hanging arm at 70 deg below horizontal (already clamped inside -30 deg abduct limit from 40 deg A-pose):
        #    midpoint x = sh_x + 0.55 * arm_len * cos(70 deg). Clearance target: torso_rx + arm_r + 1 cm!
        base_arm_x = sh_x + 0.55 * arm_len * math.cos(math.radians(70.0))
        req_arm_x = torso_rx + arm_r + 0.010
        if req_arm_x > base_arm_x:
            needed_cos = min(0.88, (req_arm_x - sh_x) / max(0.55 * arm_len, 0.05))
            needed_elev = math.degrees(math.acos(max(-1.0, min(1.0, needed_cos))))
            girth_arm_deg = max(0.0, min(18.0, round(70.0 - needed_elev, 1)))
        else:
            girth_arm_deg = 0.0

        # 2) Narrowest leg stance (~0.104m foot lane): medial thigh center sits at hip_x;
        #    also incorporate measured rig__joint_limits.json capsules/overrides when present (e.g. Domeng!).
        meta = lims.get("_meta", {})
        lim_caps = meta.get("capsules", {}).get(kid_id, {})
        lim_ov = meta.get("overrides", {}).get(kid_id, {})
        if lim_caps:
            # Blend in authoritative intake_audit capsule radii for consistent A3 clearance
            ext_thigh_r = float(lim_caps.get("LeftUpLeg", {}).get("radiusM", thigh_r))
            thigh_r = max(thigh_r, min(0.145, ext_thigh_r * 0.85))
            ext_torso_r = float(lim_caps.get("Torso", {}).get("halfWidthXM", torso_rx))
            torso_rx = max(torso_rx, min(0.22, ext_torso_r * 0.90))
        req_hip_x = thigh_r + 0.005
        if req_hip_x > hip_x:
            sin_a = min(0.35, (req_hip_x - hip_x) / max(0.60 * thigh_len, 0.05))
            girth_hip_deg = max(0.0, min(12.0, round(math.degrees(math.asin(sin_a)), 1)))
        else:
            girth_hip_deg = 0.0
        # Honor narrowed Arm abduct override floor (e.g. Domeng Arm abduct min -25 vs -30 => >= 5 deg clearance)
        arm_ov_min = float(lim_ov.get("LeftArm", {}).get("abduct", [-30.0, 140.0])[0])
        if arm_ov_min > -30.0:
            girth_arm_deg = max(girth_arm_deg, round(arm_ov_min - (-30.0), 1))
        leg_ov_min = float(lim_ov.get("LeftUpLeg", {}).get("abduct", [-25.0, 45.0])[0])
        if leg_ov_min > -15.0 and girth_hip_deg < 2.0:
            girth_hip_deg = max(girth_hip_deg, round((leg_ov_min - (-20.0)) * 0.25, 1))

        kids_out[kid_id] = {
            "legLengthM": round(leg_len, 4),
            "legLengthRatio": round(leg_ratio, 4),
            "girthArmAbductDeg": girth_arm_deg,
            "girthHipSpreadDeg": girth_hip_deg,
            "aimYawMaxDeg": aim_yaw,
            "aimPitchMaxDeg": aim_pitch,
        }
    return kids_out


# ---- Main Pipeline ----------------------------------------------------------------------------------------------

def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", default="tools/blender/kids/clips/roster_clips.glb")
    ap.add_argument("--dst", default="tools/blender/kids/clips/roster_clips.glb")
    ap.add_argument("--limits", default="unity/Data/rig__joint_limits.json")
    ap.add_argument("--metrics", default="unity/Data/anim__clip_metrics.json")
    ap.add_argument("--report", default="tools/blender/kids/clips/roster_clips_report.json")
    ap.add_argument("--chars-dir", default="assets/models/characters")
    ap.add_argument("--data-dir", default="unity/Data")
    return ap.parse_args(argv)


def main():
    args = parse_args()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = FPS
    bpy.ops.import_scene.gltf(filepath=args.src)

    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    skel = build_ref_skeleton_model(arm)
    caps = measure_reference_capsules(arm)
    lims, used_limits_file = load_joint_limits(args.limits, R_ref=skel["R_ref"])
    stand_rel_s, move_rel_s = read_character_release_times(args.data_dir)
    print(f"[upgrade_clips] Loaded {args.src}: {len(bpy.data.actions)} actions, "
          f"limits={'FILE:' + args.limits if used_limits_file else 'R2_DEFAULTS'}, "
          f"cast_release=({stand_rel_s:.3f}s, {move_rel_s:.3f}s)")

    # Extract all original clips first before mutating actions
    raw_clips = {}
    orig_actions = list(bpy.data.actions)
    for act in orig_actions:
        raw_clips[act.name] = extract_clip_frames(arm, act, skel)

    # Remove old actions cleanly
    if arm.animation_data:
        arm.animation_data.action = None
    for act in orig_actions:
        bpy.data.actions.remove(act)

    # Add R2 helper & twist bones to armature (preserves all existing bones & rest pose!)
    added_bones = add_r2_helper_twist_bones(arm)
    print(f"[upgrade_clips] Ensured {len(added_bones)} R2 helper/twist bones on roster_clips armature")

    # Upgrade Run first so Cast_Run can borrow its clean continuous lower-body stride!
    ordered_names = sorted(raw_clips.keys(), key=lambda n: (0 if n == "Run" else 1, n))
    upgraded_cache = {}
    clips_metrics = {}
    report_clips = {}

    for cname in ordered_names:
        spec = CLIP_SPECS.get(cname, dict(loop=False, archetype="idle", crossfade=0.14, mask=False, sync="none", mode="planted"))
        n_frames, hips_deltas, q_tracks = raw_clips[cname]
        hips_deltas = [v.copy() for v in hips_deltas]
        q_tracks = {bn: [q.copy() for q in qs] for bn, qs in q_tracks.items()}

        if cname == "Cast_Run" and "Run" in upgraded_cache:
            fix_cast_run_legs(n_frames, hips_deltas, q_tracks, upgraded_cache["Run"][0], upgraded_cache["Run"][1])

        release_frame, phase_ranges = stage_retime_and_exaggerate(
            cname, spec, n_frames, hips_deltas, q_tracks, stand_rel_s, move_rel_s
        )
        stage_overlap(cname, spec, n_frames, q_tracks, release_frame)
        stride_d, stride_v, contacts_L, contacts_R = stage_foot_lock_and_arm_ik(
            cname, spec, skel, n_frames, hips_deltas, q_tracks, release_frame
        )
        overshoots, pen_fixes = stage_limit_clamp_and_penetration(
            cname, spec, skel, lims, caps, n_frames, hips_deltas, q_tracks, release_frame,
            contacts_L=contacts_L, contacts_R=contacts_R
        )
        extra_tracks = compute_helper_twist_tracks(n_frames, q_tracks, lims)

        # Pin first, last, release frame, and contact boundary frames during keyframe reduction
        pinned = {0, n_frames}
        if release_frame >= 0:
            pinned.add(release_frame)
            if release_frame > 0:
                pinned.add(release_frame - 1)
            if release_frame < n_frames:
                pinned.add(release_frame + 1)
        if contacts_L:
            pinned.add(contacts_L[0])
            pinned.add(contacts_L[-1])
        if contacts_R:
            pinned.add(contacts_R[0])
            pinned.add(contacts_R[-1])

        _, keys_written = bake_action_to_armature(
            arm, skel, cname, n_frames, hips_deltas, q_tracks, extra_tracks, pinned
        )
        upgraded_cache[cname] = (hips_deltas, q_tracks)

        # Measure achieved foot slide directly on upgraded poses
        max_slide_m = 0.0
        for side, c_frames in (("Left", contacts_L), ("Right", contacts_R)):
            if len(c_frames) < 2:
                continue
            # Group contiguous runs
            runs = []
            cur = []
            for idx in c_frames:
                if not cur or idx == cur[-1] + 1:
                    cur.append(idx)
                else:
                    runs.append(cur)
                    cur = [idx]
            if cur:
                runs.append(cur)
            for r in runs:
                if len(r) < 2:
                    continue
                pts = []
                for idx in r:
                    q_f = {bn: q_tracks[bn][idx] for bn in skel["order"]}
                    wp, _ = compute_fk(skel, hips_deltas[idx], q_f)
                    pts.append(wp[P + side + "Foot"])
                p0 = Vector((pts[0].x, pts[0].y, 0.0))
                for k, p in enumerate(pts):
                    if stride_v > 0.01:
                        exp = Vector((p0.x, p0.y + stride_v * (k / float(FPS)), 0.0))
                    else:
                        exp = p0
                    err = (Vector((p.x, p.y, 0.0)) - exp).length
                    if err > max_slide_m:
                        max_slide_m = err

        duration_s = round(n_frames / float(FPS), 4)
        rel_time_s = round(release_frame / float(FPS), 4) if release_frame >= 0 else 0.0
        if cname == "Cast_OneHand":
            rel_time_s = round(stand_rel_s, 4)
        elif cname == "Cast_Run":
            rel_time_s = round(move_rel_s, 4)

        cm = {
            "name": cname,
            "durationSec": duration_s,
            "frameCount": n_frames + 1,
            "fps": FPS,
            "loop": bool(spec["loop"]),
            "strideDistanceM": round(stride_d, 4),
            "strideSpeedMps": round(stride_v, 4),
            "releaseTimeSec": rel_time_s,
            "releaseFrame": int(release_frame),
            "contactFramesL": contacts_L,
            "contactFramesR": contacts_R,
            "crossfadeInSec": float(spec["crossfade"]),
            "archetype": spec["archetype"],
            "upperBodyMask": bool(spec["mask"]),
            "syncGroup": spec["sync"],
            "maxFootSlideM": round(max_slide_m, 4),
        }
        if phase_ranges is not None:
            cm["phaseRanges"] = phase_ranges
        clips_metrics[cname] = cm
        report_clips[cname] = {
            "frames": n_frames + 1,
            "durationSec": duration_s,
            "strideSpeedMps": round(stride_v, 3),
            "maxFootSlideCm": round(max_slide_m * 100.0, 2),
            "keysWritten": keys_written,
            "penetrationFixes": pen_fixes,
            "sourceOvershootsOver10Deg": len(overshoots),
            "sampleOvershoots": overshoots[:5],
        }
        print(f"  {cname:22s} f=0..{n_frames:3d} ({duration_s:4.2f}s) stride={stride_v:4.2f}m/s "
              f"slide={max_slide_m*100:4.2f}cm penFix={pen_fixes:2d} ov>10={len(overshoots):3d} keys={keys_written}")

    # Reset armature to clean rest pose before exporting
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pb.location = (0.0, 0.0, 0.0)
        pb.scale = (1.0, 1.0, 1.0)
    bpy.context.view_layer.update()

    # Export upgraded roster_clips.glb (exact contract intact: Armature + mesh + all 20 Actions, 30fps, zero scale curves!)
    os.makedirs(os.path.dirname(os.path.abspath(args.dst)), exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.gltf(
        filepath=args.dst,
        export_format="GLB",
        use_selection=True,
        export_yup=True,
        export_animations=True,
        export_animation_mode="ACTIONS",
        export_force_sampling=False,
        export_bake_animation=False,
        export_skins=True,
        export_all_influences=False,
        export_image_format="AUTO",
        export_morph=False,
        export_apply=False,
        export_texcoords=True,
        export_normals=True,
        export_optimize_animation_size=True,
        export_optimize_animation_keep_anim_armature=False,
    )
    print(f"[upgrade_clips] Exported upgraded clip pack -> {args.dst} ({os.path.getsize(args.dst) // 1024} KB)")

    # Reload exported GLB and measure TRUE post-export / post-reduction foot slide so anim__clip_metrics.json
    # and roster_clips_report.json reflect actual exported GLB values (identical to anim_scan.py!).
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = FPS
    bpy.ops.import_scene.gltf(filepath=args.dst)
    scn = bpy.context.scene
    arm_check = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    if arm_check.animation_data is None:
        arm_check.animation_data_create()
    AM_c = arm_check.matrix_world
    for cname, cm in clips_metrics.items():
        act_c = bpy.data.actions.get(cname)
        if act_c is None:
            continue
        arm_check.animation_data.action = act_c
        try:
            arm_check.animation_data.action_slot = act_c.slots[0]
        except Exception:
            pass
        f0 = int(round(act_c.frame_range[0]))
        f1 = int(round(act_c.frame_range[1]))
        nf = max(1, f1 - f0)
        fpos_L, fpos_R = [], []
        for f in range(f0, f0 + nf + 1):
            scn.frame_set(f)
            bpy.context.view_layer.update()
            fpos_L.append((AM_c @ arm_check.pose.bones[P + "LeftFoot"].head).copy())
            fpos_R.append((AM_c @ arm_check.pose.bones[P + "RightFoot"].head).copy())
        sv = float(cm["strideSpeedMps"])
        post_slide = 0.0
        for fpos, c_frames in ((fpos_L, cm["contactFramesL"]), (fpos_R, cm["contactFramesR"])):
            runs = []
            cur = []
            for idx in c_frames:
                if 0 <= idx < len(fpos):
                    if not cur or idx == cur[-1] + 1:
                        cur.append(idx)
                    else:
                        runs.append(cur)
                        cur = [idx]
            if cur:
                runs.append(cur)
            for r in runs:
                if len(r) < 2:
                    continue
                p0_xy = Vector((fpos[r[0]].x, fpos[r[0]].y, 0.0))
                for k, idx in enumerate(r):
                    p = fpos[idx]
                    exp = Vector((p0_xy.x, p0_xy.y + sv * (k / float(FPS)), 0.0)) if sv > 0.01 else p0_xy
                    err = (Vector((p.x, p.y, 0.0)) - exp).length
                    if err > post_slide:
                        post_slide = err
        cm["maxFootSlideM"] = round(post_slide, 4)
        report_clips[cname]["maxFootSlideCm"] = round(post_slide * 100.0, 2)

    # Measure all 12 kids for A3 adaptation data (runs sequentially in the same Blender session!)
    ref_leg_len = skel["ref_leg_len"]
    kids_metrics = measure_all_12_kids(args.chars_dir, ref_leg_len, lims)
    print(f"[upgrade_clips] Measured {len(kids_metrics)} kids for A3 runtime adaptation")

    # Write unity/Data/anim__clip_metrics.json (SimData wrapped per coordinator directive #17, strict LF endings)
    metrics_doc = {
        "source": "res://data/anim/clip_metrics.tres",
        "class": "AnimClipMetrics",
        "data": {
            "schemaVersion": 1,
            "fps": FPS,
            "referenceKid": "roster_ref",
            "referenceLegLengthM": round(ref_leg_len, 4),
            "speedMatchClamp": {"min": 0.7, "max": 1.35},
            "crossfadeMatrixSec": {
                "locomotionToLocomotion": 0.15,
                "intoCast": 0.05,
                "intoHit": 0.03,
                "outOfHitOrStun": 0.20,
                "intoAir": 0.08,
                "default": 0.14,
            },
            "clips": {k: clips_metrics[k] for k in sorted(clips_metrics.keys())},
            "standIns": STAND_INS,
            "kids": {k: kids_metrics[k] for k in sorted(kids_metrics.keys())},
        },
    }
    os.makedirs(os.path.dirname(os.path.abspath(args.metrics)), exist_ok=True)
    import re
    pretty = json.dumps(metrics_doc, indent=2)
    # Keep flat numeric arrays (contactFramesL/R, phaseRanges) compact on single lines for readability
    pretty = re.sub(r"\[\s+([0-9,\s]+?)\s+\]", lambda m: "[" + ", ".join(x.strip() for x in m.group(1).split(",") if x.strip()) + "]", pretty)
    with open(args.metrics, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(pretty)
        fh.write("\n")
    print(f"[upgrade_clips] Wrote {args.metrics}")

    # Write lightweight reference upgrade report next to roster_clips.glb
    if args.report:
        report_doc = {
            "schemaVersion": 1,
            "fps": FPS,
            "limitsSource": "unity/Data/rig__joint_limits.json" if used_limits_file else "R2_DEFAULTS",
            "referenceCapsulesM": {k: round(v, 4) for k, v in caps.items()},
            "clipCount": len(report_clips),
            "clips": {k: report_clips[k] for k in sorted(report_clips.keys())},
        }
        with open(args.report, "w", encoding="utf-8", newline="\n") as fh:
            json.dump(report_doc, fh, indent=2)
            fh.write("\n")
        print(f"[upgrade_clips] Wrote {args.report}")


if __name__ == "__main__":
    main()
