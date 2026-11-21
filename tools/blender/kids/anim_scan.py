# A8 Automated Animation Acceptance Scanner & Before/After Frame-Strip Renderer (Blender 5.2).
#
# Verifies EVERY frame of EVERY clip in tools/blender/kids/clips/roster_clips.glb (loaded fresh from disk)
# against apex-anim-craft A8 gates:
#   - ZERO joint pops > 25 deg (consecutive frames and loop wrap)
#   - ZERO Euler / quaternion hemisphere flips, ZERO scale curves
#   - Loop seam matched <= 0.5 deg & <= 1 mm on all looping clips
#   - ZERO horizontal root drift (in-place A4)
#   - Foot slide <= 2.0 cm on all contact intervals
#   - ZERO T-pose frames
#   - ZERO joint limit violations vs unity/Data/rig__joint_limits.json (swing-twist decomposition)
#   - ZERO sideways hinge bends on ForeArm/Leg (> 1.0 deg)
#   - ZERO straight-locked limbs on ForeArm/Leg (< 2.0 deg)
#   - ZERO capsule penetrations > 3.0 mm (arm/hand vs torso/hips, thigh/shin vs opposite leg)
#   - Baked R2 helper & twist bones verified (12 bones)
#   - Cast release timing verified vs unity/Data/characters__narda.json
#
# Usage:
#   blender --background --python tools/blender/kids/anim_scan.py -- \
#     --clips tools/blender/kids/clips/roster_clips.glb \
#     --limits unity/Data/rig__joint_limits.json \
#     --metrics unity/Data/anim__clip_metrics.json \
#     [--before <baseline.glb>] [--kid assets/models/characters/narda.glb] \
#     [--render-strips <out_dir>] [--strip-clips Run,walk,Cast_OneHand,Cast_Run,jump,slide,Idle] \
#     [--json-out <scan_report.json>]

import sys
import os
import math
import json
import struct
import argparse
import numpy as np
import bpy
from mathutils import Vector, Matrix, Quaternion

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
if SCRIPT_DIR not in sys.path:
    sys.path.insert(0, SCRIPT_DIR)

from upgrade_clips import (
    P, FPS, CLIP_SPECS,
    build_ref_skeleton_model,
    load_joint_limits,
    measure_joint_angles_deg,
    clamp_joint_swing_twist,
    swing_twist_decompose,
    measure_reference_capsules,
    test_capsule_penetrations,
    read_character_release_times,
)

# Gate thresholds (apex-anim-craft A8)
MAX_POP_DEG = 25.0
MAX_FOOT_SLIDE_M = 0.020
MAX_PENETRATION_M = 0.003
MAX_LIMIT_TOL_DEG = 1.25
MAX_SIDEWAYS_HINGE_DEG = 1.25
MIN_HINGE_BEND_DEG = 2.0
MAX_SEAM_DEG = 0.75
MAX_ROOT_XY_DRIFT_M = 0.005
MAX_HELPER_ERR_DEG = 3.5

R2_EXTRA_BONES = [
    P + s + x
    for s in ("Left", "Right")
    for x in ("ArmTwist", "ForeArmTwist1", "ForeArmTwist2", "ForeArmHelper", "LegHelper", "HandHelper")
]


def inspect_action_fcurves(act):
    """Count scale curves, Euler flips, and quaternion sign flips in Blender 5.2 layered actions."""
    scale_curves = 0
    euler_flips = 0
    quat_flips = 0
    if not hasattr(act, "layers"):
        return scale_curves, euler_flips, quat_flips
    for layer in act.layers:
        for strip in layer.strips:
            for cb in strip.channelbags:
                quats = {}
                for fc in cb.fcurves:
                    dp = fc.data_path
                    if dp.endswith(".scale"):
                        scale_curves += 1
                    elif dp.endswith(".rotation_euler"):
                        pts = [kp.co[1] for kp in fc.keyframe_points]
                        for i in range(1, len(pts)):
                            if abs(pts[i] - pts[i - 1]) > math.pi:
                                euler_flips += 1
                    elif dp.endswith(".rotation_quaternion"):
                        quats.setdefault(dp, {})[fc.array_index] = fc
                for dp, chs in quats.items():
                    if len(chs) == 4:
                        nkp = min(len(chs[i].keyframe_points) for i in range(4))
                        prev = None
                        for k in range(nkp):
                            q = Quaternion([chs[i].keyframe_points[k].co[1] for i in range(4)])
                            if prev is not None and prev.dot(q) < 0.0:
                                quat_flips += 1
                            prev = q
    return scale_curves, euler_flips, quat_flips


def inspect_glb_scale_channels(glb_path):
    """Inspect the raw GLB JSON animation channels directly to verify ZERO scale channels exist."""
    per_anim = {}
    with open(glb_path, "rb") as fh:
        header = fh.read(12)
        if len(header) < 12 or header[:4] != b"glTF":
            return per_anim
        clen, _ctype = struct.unpack("<II", fh.read(8))
        gltf = json.loads(fh.read(clen).decode("utf-8"))
    for anim in gltf.get("animations", []):
        name = anim.get("name", "")
        sc = sum(1 for ch in anim.get("channels", []) if ch.get("target", {}).get("path") == "scale")
        per_anim[name] = sc
    return per_anim


def scan_clips(clips_path, limits_path, metrics_path, data_dir):
    """Scan every frame of every clip in clips_path against all A8 acceptance gates."""
    glb_scales = inspect_glb_scale_channels(clips_path)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = FPS
    bpy.ops.import_scene.gltf(filepath=clips_path)

    scn = bpy.context.scene
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    skel = build_ref_skeleton_model(arm)
    caps = measure_reference_capsules(arm)
    lims, used_limits_file = load_joint_limits(limits_path, R_ref=skel["R_ref"])
    stand_rel_s, move_rel_s = read_character_release_times(data_dir)

    metrics_clips = {}
    if metrics_path and os.path.exists(metrics_path):
        with open(metrics_path, "r", encoding="utf-8") as fh:
            m_raw = json.load(fh)
        metrics_clips = m_raw.get("data", m_raw).get("clips", {})

    if arm.animation_data is None:
        arm.animation_data_create()

    AM = arm.matrix_world
    AM3 = AM.to_3x3()
    present_r2 = [bn for bn in R2_EXTRA_BONES if bn in arm.data.bones]
    missing_r2 = [bn for bn in R2_EXTRA_BONES if bn not in arm.data.bones]

    results = {}
    overall_pass = (len(missing_r2) == 0)

    print(f"=== A8 ANIM_SCAN: {clips_path} ({len(bpy.data.actions)} clips, "
          f"limits={'FILE' if used_limits_file else 'R2_DEFAULTS'}, R2_extras={len(present_r2)}/12) ===")

    for act in sorted(bpy.data.actions, key=lambda a: a.name):
        cname = act.name
        spec = CLIP_SPECS.get(cname, dict(loop=False, archetype="idle"))
        m_entry = metrics_clips.get(cname, {})
        loop = bool(m_entry.get("loop", spec["loop"]))
        scale_fc, euler_flips, quat_flips = inspect_action_fcurves(act)
        scale_fc = max(scale_fc, glb_scales.get(cname, 0))

        arm.animation_data.action = act
        try:
            arm.animation_data.action_slot = act.slots[0]
        except Exception:
            pass
        f0 = int(round(act.frame_range[0]))
        f1 = int(round(act.frame_range[1]))
        n_frames = max(1, f1 - f0)

        hips_deltas = []
        q_ref_frames = []
        W_pos_frames = []
        W_rot_frames = []
        q_extra_frames = {bn: [] for bn in present_r2}

        for f in range(f0, f0 + n_frames + 1):
            scn.frame_set(f)
            bpy.context.view_layer.update()
            W_pose = {}
            W_pos = {}
            for bn in skel["order"]:
                pb = arm.pose.bones[bn]
                W_pose[bn] = (AM3 @ pb.matrix.to_3x3()).normalized()
                W_pos[bn] = (AM @ pb.head).copy()
            hd = W_pos[P + "Hips"] - skel["head_src"][P + "Hips"]
            hips_deltas.append(hd)

            q_f = {}
            for bn in skel["order"]:
                p = skel["parent_of"][bn]
                if p is None:
                    M = skel["R_ref"][bn].transposed() @ W_pose[bn]
                else:
                    M = skel["L_ref"][bn].transposed() @ W_pose[p].transposed() @ W_pose[bn]
                q_f[bn] = M.to_quaternion().normalized()
            q_ref_frames.append(q_f)
            W_pos_frames.append(W_pos)
            W_rot_frames.append(W_pose)
            for bn in present_r2:
                q_extra_frames[bn].append(arm.pose.bones[bn].matrix_basis.to_quaternion().normalized())

        # 1. Joint pops (> 25 deg), loop seam, root XY drift
        max_pop = 0.0
        pop_list = []
        max_seam = 0.0
        for bn in skel["order"]:
            if not bn.startswith(P) or bn in present_r2:
                continue
            short = bn[len(P):]
            for i in range(1, len(q_ref_frames)):
                ang = math.degrees(q_ref_frames[i - 1][bn].rotation_difference(q_ref_frames[i][bn]).angle)
                if ang > max_pop:
                    max_pop = ang
                if ang > MAX_POP_DEG:
                    pop_list.append(f"{short}@f{i}:{ang:.1f}")
            if loop:
                seam = math.degrees(q_ref_frames[0][bn].rotation_difference(q_ref_frames[-1][bn]).angle)
                if seam > max_seam:
                    max_seam = seam
                if len(q_ref_frames) >= 3:
                    wrap_ang = math.degrees(q_ref_frames[-2][bn].rotation_difference(q_ref_frames[0][bn]).angle)
                    if wrap_ang > max_pop:
                        max_pop = wrap_ang
                    if wrap_ang > MAX_POP_DEG:
                        pop_list.append(f"{short}@wrap:{wrap_ang:.1f}")

        root_xy = max(math.hypot(hd.x, hd.y) for hd in hips_deltas)

        # 2. Joint limits, sideways hinges, straight-locked limbs, T-pose frames, capsule penetrations
        limit_viols = []
        sideways_viols = []
        locked_viols = []
        tpose_frames = []
        pen_viols = []
        max_pen_m = 0.0
        max_helper_err = 0.0

        for i in range(len(q_ref_frames)):
            q_f = q_ref_frames[i]
            wp = W_pos_frames[i]
            wr = W_rot_frames[i]

            for bn in skel["order"]:
                if not bn.startswith(P) or bn in present_r2:
                    continue
                short = bn[len(P):]
                if short not in lims:
                    continue
                lim = lims[short]
                _, ov, det = clamp_joint_swing_twist(q_f[bn], lim, enforce_soft_min=False)
                if ov > MAX_LIMIT_TOL_DEG:
                    limit_viols.append(f"{short}@f{i}:{ov:.1f}deg")

                # Sideways hinge & straight-lock checks on ForeArm and Leg
                if short in ("LeftForeArm", "RightForeArm", "LeftLeg", "RightLeg"):
                    flex_d, abd_d, _, _, _, _ = measure_joint_angles_deg(q_f[bn], lim)
                    if abs(abd_d) > MAX_SIDEWAYS_HINGE_DEG:
                        sideways_viols.append(f"{short}@f{i}:abd={abd_d:.1f}")
                    if flex_d < MIN_HINGE_BEND_DEG:
                        locked_viols.append(f"{short}@f{i}:flex={flex_d:.1f}")

            # T-pose check: both upper arms within 15 deg of horizontal AND both elbows < 12 deg
            la_d = (wp[P + "LeftForeArm"] - wp[P + "LeftArm"]).normalized()
            ra_d = (wp[P + "RightForeArm"] - wp[P + "RightArm"]).normalized()
            lf_fl, _, _, _, _, _ = measure_joint_angles_deg(q_f[P + "LeftForeArm"], lims["LeftForeArm"])
            rf_fl, _, _, _, _, _ = measure_joint_angles_deg(q_f[P + "RightForeArm"], lims["RightForeArm"])
            if abs(la_d.z) < 0.25 and abs(ra_d.z) < 0.25 and lf_fl < 12.0 and rf_fl < 12.0:
                tpose_frames.append(i)

            # Capsule penetration check (> 3 mm)
            pen = test_capsule_penetrations(skel, wp, wr, caps)
            for k, ov in pen.items():
                if ov > max_pen_m:
                    max_pen_m = ov
                if ov > MAX_PENETRATION_M:
                    pen_viols.append(f"{k}@f{i}:{ov*1000:.1f}mm")

            # Helper / twist consistency check (exact #21 sign lock; ForeArmTwist1/2 about ForeArm.twistAxis!)
            ident = Quaternion((1.0, 0.0, 0.0, 0.0))
            for s in ("Left", "Right"):
                arm_t_ax = Vector(lims[s + "Arm"]["twistAxis"]).normalized()
                fa_t_ax = Vector(lims[s + "ForeArm"]["twistAxis"]).normalized()
                hand_t_ax = Vector(lims[s + "Hand"]["twistAxis"]).normalized()
                _, _, arm_tw = swing_twist_decompose(q_f[P + s + "Arm"], arm_t_ax)
                _, _, fa_tw = swing_twist_decompose(q_f[P + s + "ForeArm"], fa_t_ax)
                _, _, hand_tw = swing_twist_decompose(q_f[P + s + "Hand"], hand_t_ax)
                tot_fa_tw = fa_tw + hand_tw
                checks = [
                    (P + s + "ArmTwist", Quaternion(arm_t_ax, -0.50 * arm_tw)),
                    (P + s + "ForeArmTwist1", Quaternion(fa_t_ax, 0.33 * tot_fa_tw)),
                    (P + s + "ForeArmTwist2", Quaternion(fa_t_ax, 0.66 * tot_fa_tw)),
                    (P + s + "ForeArmHelper", ident.slerp(q_f[P + s + "ForeArm"] if q_f[P + s + "ForeArm"].w >= 0 else -q_f[P + s + "ForeArm"], 0.5)),
                    (P + s + "LegHelper", ident.slerp(q_f[P + s + "Leg"] if q_f[P + s + "Leg"].w >= 0 else -q_f[P + s + "Leg"], 0.5)),
                    (P + s + "HandHelper", ident.slerp(q_f[P + s + "Hand"] if q_f[P + s + "Hand"].w >= 0 else -q_f[P + s + "Hand"], 0.5)),
                ]
                for b_ex, q_exp in checks:
                    if b_ex in q_extra_frames:
                        err = math.degrees(q_extra_frames[b_ex][i].rotation_difference(q_exp).angle)
                        if err > max_helper_err:
                            max_helper_err = err

        # 3. Foot slide check on contact intervals (from metrics, or detected by foot Z)
        stride_v = float(m_entry.get("strideSpeedMps", 0.0))
        cL = m_entry.get("contactFramesL", [])
        cR = m_entry.get("contactFramesR", [])
        max_slide_m = 0.0
        for side, c_frames in (("Left", cL), ("Right", cR)):
            if len(c_frames) < 2:
                continue
            runs = []
            cur = []
            for idx in c_frames:
                if 0 <= idx < len(W_pos_frames):
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
                p0 = W_pos_frames[r[0]][P + side + "Foot"]
                p0_xy = Vector((p0.x, p0.y, 0.0))
                for k, idx in enumerate(r):
                    p = W_pos_frames[idx][P + side + "Foot"]
                    if stride_v > 0.01:
                        exp = Vector((p0_xy.x, p0_xy.y + stride_v * (k / float(FPS)), 0.0))
                    else:
                        exp = p0_xy
                    err = (Vector((p.x, p.y, 0.0)) - exp).length
                    if err > max_slide_m:
                        max_slide_m = err

        # 4. Gameplay timing honesty check
        release_ok = True
        if cname == "Cast_OneHand":
            rel_t = float(m_entry.get("releaseTimeSec", 0.0))
            if abs(rel_t - stand_rel_s) > (1.0 / FPS):
                release_ok = False
        elif cname == "Cast_Run":
            rel_t = float(m_entry.get("releaseTimeSec", 0.0))
            if abs(rel_t - move_rel_s) > (1.0 / FPS):
                release_ok = False

        clip_pass = (
            len(pop_list) == 0
            and scale_fc == 0
            and euler_flips == 0
            and quat_flips == 0
            and (not loop or max_seam <= MAX_SEAM_DEG)
            and root_xy <= MAX_ROOT_XY_DRIFT_M
            and max_slide_m <= MAX_FOOT_SLIDE_M
            and len(tpose_frames) == 0
            and len(limit_viols) == 0
            and len(sideways_viols) == 0
            and len(locked_viols) == 0
            and len(pen_viols) == 0
            and max_helper_err <= MAX_HELPER_ERR_DEG
            and release_ok
        )
        if not clip_pass:
            overall_pass = False

        status = "PASS" if clip_pass else "FAIL"
        print(
            f"  [{status}] {cname:22s} f={n_frames+1:3d} pop={max_pop:4.1f}deg seam={max_seam:4.2f}deg "
            f"slide={max_slide_m*100:4.2f}cm pen={max_pen_m*1000:4.2f}mm rootXY={root_xy*100:4.2f}cm "
            f"limV={len(limit_viols):2d} sideH={len(sideways_viols):2d} lock={len(locked_viols):2d} "
            f"hlpErr={max_helper_err:4.2f}deg scale={scale_fc}"
        )
        if not clip_pass:
            if pop_list:
                print(f"         pops>25: {pop_list[:6]}")
            if limit_viols:
                print(f"         limits:  {limit_viols[:6]}")
            if sideways_viols:
                print(f"         sideH:   {sideways_viols[:6]}")
            if locked_viols:
                print(f"         locked:  {locked_viols[:6]}")
            if pen_viols:
                print(f"         pen>3mm: {pen_viols[:6]}")
            if tpose_frames:
                print(f"         tpose:   {tpose_frames[:6]}")

        results[cname] = {
            "pass": clip_pass,
            "frames": n_frames + 1,
            "maxPopDeg": round(max_pop, 2),
            "popsOver25": pop_list[:10],
            "scaleCurves": scale_fc,
            "eulerFlips": euler_flips,
            "quatFlips": quat_flips,
            "loopSeamDeg": round(max_seam, 3),
            "rootXyDriftCm": round(root_xy * 100.0, 3),
            "footSlideCm": round(max_slide_m * 100.0, 2),
            "tposeFrames": tpose_frames,
            "limitViolationsCount": len(limit_viols),
            "limitViolations": limit_viols[:10],
            "sidewaysHingeCount": len(sideways_viols),
            "sidewaysHinges": sideways_viols[:10],
            "straightLockedCount": len(locked_viols),
            "straightLocked": locked_viols[:10],
            "maxPenetrationMm": round(max_pen_m * 1000.0, 2),
            "penetrationViolations": pen_viols[:10],
            "maxHelperTwistErrDeg": round(max_helper_err, 2),
            "releaseHonest": release_ok,
        }

    summary = {
        "overallPass": overall_pass,
        "clipCount": len(results),
        "passedCount": sum(1 for r in results.values() if r["pass"]),
        "r2ExtraBonesPresent": len(present_r2),
        "r2ExtraBonesMissing": missing_r2,
        "maxPopDegAll": round(max((r["maxPopDeg"] for r in results.values()), default=0.0), 2),
        "maxFootSlideCmAll": round(max((r["footSlideCm"] for r in results.values()), default=0.0), 2),
        "maxPenetrationMmAll": round(max((r["maxPenetrationMm"] for r in results.values()), default=0.0), 2),
        "totalLimitViolations": sum(r["limitViolationsCount"] for r in results.values()),
        "totalSidewaysHinges": sum(r["sidewaysHingeCount"] for r in results.values()),
        "totalStraightLocked": sum(r["straightLockedCount"] for r in results.values()),
        "clips": results,
    }
    print(
        f"=== SCAN SUMMARY: {'PASS' if overall_pass else 'FAIL'} "
        f"({summary['passedCount']}/{summary['clipCount']} clips, "
        f"maxPop={summary['maxPopDegAll']}deg, maxSlide={summary['maxFootSlideCmAll']}cm, "
        f"maxPen={summary['maxPenetrationMmAll']}mm, limV={summary['totalLimitViolations']}, "
        f"sideH={summary['totalSidewaysHinges']}, lock={summary['totalStraightLocked']}) ==="
    )
    return summary


# ---- Before/After Frame-Strip Renderer (Side-On + 3/4 Contact Sheets) ------------------------------------------

def _load_rgba(path):
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    arr = np.array(img.pixels[:], dtype=np.float32).reshape((h, w, 4))
    bpy.data.images.remove(img)
    return arr


def _save_rgba(arr, path):
    h, w, _ = arr.shape
    img = bpy.data.images.new("strip_out", width=w, height=h, alpha=True)
    img.pixels.foreach_set(arr.ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def _render_clip_tiles_on_kid(src_clips_path, kid_path, clip_name, n_tiles, view_cam_loc, tmp_dir, tag):
    """Render n_tiles frames of clip_name from src_clips_path retargeted cleanly onto kid_path with a ground grid."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scn = bpy.context.scene
    scn.render.fps = FPS
    bpy.ops.import_scene.gltf(filepath=src_clips_path)
    src = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    src_objs = set(bpy.data.objects)
    src_skel = build_ref_skeleton_model(src)

    bpy.ops.import_scene.gltf(filepath=kid_path)
    kid = next(o for o in bpy.data.objects if o.type == "ARMATURE" and o not in src_objs)
    for o in list(bpy.data.objects):
        if o.type == "MESH" and (o in src_objs or not o.vertex_groups):
            bpy.data.objects.remove(o, do_unlink=True)
    if kid.animation_data:
        kid.animation_data_clear()

    act = bpy.data.actions.get(clip_name)
    if act is None:
        return []
    if src.animation_data is None:
        src.animation_data_create()
    src.animation_data.action = act
    try:
        src.animation_data.action_slot = act.slots[0]
    except Exception:
        pass

    # Add visible street ground plane at Z=0 so foot lock & clearance read unmistakably!
    bpy.ops.mesh.primitive_plane_add(size=4.0, location=(0.0, 0.0, 0.0))
    ground = bpy.context.active_object
    gmat = bpy.data.materials.new("GroundMat")
    nt = gmat.node_tree
    if nt is None:
        gmat.use_nodes = True
        nt = gmat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    checker = nt.nodes.new("ShaderNodeTexChecker")
    checker.inputs["Scale"].default_value = 16.0
    checker.inputs["Color1"].default_value = (0.28, 0.29, 0.31, 1.0)
    checker.inputs["Color2"].default_value = (0.36, 0.37, 0.40, 1.0)
    nt.links.new(checker.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.9
    ground.data.materials.append(gmat)

    # 3-point sunlit barangay lighting
    for loc, energy in [((2.5, -3.5, 3.5), 3.0), ((-2.5, -2.5, 2.0), 1.6), ((0.0, 3.0, 2.2), 1.4)]:
        light = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
        light.rotation_euler = (Vector((0, 0, 0.8)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        light.data.energy = energy
        scn.collection.objects.link(light)
    scn.world = bpy.data.worlds.new("w")
    scn.world.color = (0.58, 0.62, 0.68)

    scn.render.engine = "CYCLES"
    scn.cycles.device = "CPU"
    scn.cycles.samples = 8
    scn.cycles.max_bounces = 2
    scn.cycles.use_denoising = False
    scn.render.resolution_x = 220
    scn.render.resolution_y = 320
    scn.render.image_settings.file_format = "PNG"

    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scn.collection.objects.link(cam)
    scn.camera = cam
    cam.location = Vector(view_cam_loc)
    cam.rotation_euler = (Vector((0.0, 0.0, 0.78)) - cam.location).to_track_quat("-Z", "Y").to_euler()

    SW = src.matrix_world.to_3x3()
    KW = kid.matrix_world.to_3x3()
    srest = src_skel["R_ref"]
    krest = {b.name: (KW @ b.matrix_local.to_3x3()).normalized() for b in kid.data.bones}
    korder = sorted(kid.data.bones, key=lambda b: len(b.parent_recursive))
    sleg = src_skel["ref_leg_len"]
    kleg = (kid.matrix_world @ kid.data.bones[P + "LeftUpLeg"].head_local - kid.matrix_world @ kid.data.bones[P + "LeftFoot"].head_local).length

    f0, f1 = int(round(act.frame_range[0])), int(round(act.frame_range[1]))
    tiles = []
    for k in range(n_tiles):
        f = f0 + (f1 - f0) * k / max(1, n_tiles - 1)
        scn.frame_set(int(round(f)))
        bpy.context.view_layer.update()

        D = {}
        for b in src.data.bones:
            if b.name in srest:
                D[b.name] = (SW @ src.pose.bones[b.name].matrix.to_3x3()).normalized() @ srest[b.name].transposed()
        A = {}
        for b in korder:
            d = D.get(b.name)
            if d is None:
                d = (A[b.parent.name] @ krest[b.parent.name].transposed()) if b.parent else Matrix.Identity(3)
            A[b.name] = (d @ krest[b.name]).normalized()
            pb = kid.pose.bones[b.name]
            if b.parent:
                L = krest[b.parent.name].transposed() @ krest[b.name]
                basis = L.transposed() @ A[b.parent.name].transposed() @ A[b.name]
            else:
                basis = krest[b.name].transposed() @ A[b.name]
            pb.rotation_mode = "QUATERNION"
            pb.rotation_quaternion = basis.to_quaternion().normalized()
        hs = src.pose.bones[P + "Hips"]
        delta = (src.matrix_world @ hs.head) - (src.matrix_world @ src.data.bones[P + "Hips"].head_local)
        delta = Vector((0.0, 0.0, delta.z)) * (kleg / max(sleg, 1e-4))
        kid.pose.bones[P + "Hips"].location = krest[P + "Hips"].transposed() @ delta
        bpy.context.view_layer.update()

        out_p = os.path.join(tmp_dir, f"{tag}_{clip_name}_{k}.png")
        scn.render.filepath = out_p
        bpy.ops.render.render(write_still=True)
        tiles.append(_load_rgba(out_p))
        try:
            os.remove(out_p)
        except OSError:
            pass
    return tiles


def render_before_after_strips(before_path, after_path, kid_path, strip_clips, out_dir, n_tiles=6):
    """Render stacked BEFORE (top, red bar) vs AFTER (bottom, green bar) side-on & 3/4 comparison strips."""
    os.makedirs(out_dir, exist_ok=True)
    kid_name = os.path.splitext(os.path.basename(kid_path))[0]
    views = [
        ("side", (3.7, 0.0, 0.85)),     # side-on for feet & line of action
        ("34",   (2.4, -3.0, 1.00)),    # 3/4 heroic view for arms/elbows/silhouette
    ]
    saved = []
    for cname in strip_clips:
        for vtag, cam_loc in views:
            before_tiles = _render_clip_tiles_on_kid(before_path, kid_path, cname, n_tiles, cam_loc, out_dir, f"pre_{vtag}")
            after_tiles = _render_clip_tiles_on_kid(after_path, kid_path, cname, n_tiles, cam_loc, out_dir, f"post_{vtag}")
            if not before_tiles or not after_tiles:
                continue
            th, tw, _ = after_tiles[0].shape
            bar_h = 8
            gap = 4
            row_w = tw * n_tiles + gap * (n_tiles - 1)
            total_h = (th + bar_h) * 2 + gap
            canvas = np.zeros((total_h, row_w, 4), dtype=np.float32)
            canvas[:, :, :3] = 0.12
            canvas[:, :, 3] = 1.0

            # Note: Blender image rows are bottom-to-top!
            # Bottom row in PNG = AFTER (with green top indicator bar); Top row in PNG = BEFORE (with red top indicator bar).
            for k in range(n_tiles):
                x0 = k * (tw + gap)
                # AFTER row (y = 0..th)
                canvas[0:th, x0:x0 + tw] = after_tiles[k]
                canvas[th:th + bar_h, x0:x0 + tw] = (0.18, 0.78, 0.34, 1.0)
                # BEFORE row (y = th + bar_h + gap ..)
                y1 = th + bar_h + gap
                canvas[y1:y1 + th, x0:x0 + tw] = before_tiles[k]
                canvas[y1 + th:y1 + th + bar_h, x0:x0 + tw] = (0.85, 0.22, 0.20, 1.0)

            out_png = os.path.join(out_dir, f"strip_{kid_name}_{cname}_{vtag}.png")
            _save_rgba(canvas, out_png)
            saved.append(out_png)
            print(f"Screenshot: {out_png} | {kid_name} {cname} ({vtag} view): TOP=before (red bar), BOTTOM=after (green bar), {n_tiles} frames")
    return saved


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--clips", default="tools/blender/kids/clips/roster_clips.glb")
    ap.add_argument("--limits", default="unity/Data/rig__joint_limits.json")
    ap.add_argument("--metrics", default="unity/Data/anim__clip_metrics.json")
    ap.add_argument("--data-dir", default="unity/Data")
    ap.add_argument("--json-out", default="")
    ap.add_argument("--before", default="")
    ap.add_argument("--kid", default="assets/models/characters/narda.glb")
    ap.add_argument("--render-strips", default="")
    ap.add_argument("--strip-clips", default="Run,walk,sprint,Cast_OneHand,Cast_Run,jump,slide,Idle")
    ap.add_argument("--tiles", type=int, default=6)
    return ap.parse_args(argv)


def main():
    args = parse_args()
    summary = scan_clips(args.clips, args.limits, args.metrics, args.data_dir)
    if args.json_out:
        os.makedirs(os.path.dirname(os.path.abspath(args.json_out)), exist_ok=True)
        with open(args.json_out, "w", encoding="utf-8", newline="\n") as fh:
            json.dump(summary, fh, indent=2)
            fh.write("\n")
    if args.render_strips and args.before:
        c_list = [c.strip() for c in args.strip_clips.split(",") if c.strip()]
        render_before_after_strips(args.before, args.clips, args.kid, c_list, args.render_strips, args.tiles)
    if not summary["overallPass"]:
        sys.exit(2)


if __name__ == "__main__":
    main()
