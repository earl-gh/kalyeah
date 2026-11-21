#!/usr/bin/env python3
"""apex-rig-forge R0 quantitative intake audit for Kalyeah character .glb models.

Audits raw Meshy models (assets/models/raw/) and/or processed models (assets/models/characters/)
sequentially inside one Blender 5.2 session. Outputs a crisp summary table to stdout and optional
machine-readable JSON (--out).

Usage:
  blender --background --python tools/blender/kids/intake_audit.py -- [paths...]
  blender --background --python tools/blender/kids/intake_audit.py -- --dir assets/models/raw --out report.json
  blender --background --python tools/blender/kids/intake_audit.py -- --dir assets/models/characters
"""
from __future__ import annotations

import argparse
import glob
import json
import math
import struct
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Vector

P = "mixamorig:"
ARM_DROP_DEG = 40.0

CORE_BONES = [
    "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head", "HeadTop_End",
    "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
    "RightShoulder", "RightArm", "RightForeArm", "RightHand",
    "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase", "LeftToe_End",
    "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase", "RightToe_End",
]
DIGIT_NAMES = ("Thumb", "Index", "Middle", "Ring", "Pinky")
DIGIT_BONES = [
    f"{s}Hand{d}{j}"
    for s in ("Left", "Right")
    for d in DIGIT_NAMES
    for j in ("1", "2", "3")
]
TWIST_BONES = [
    f"{s}{n}" for s in ("Left", "Right") for n in ("ArmTwist", "ForeArmTwist1", "ForeArmTwist2")
]
HELPER_BONES = [
    f"{s}{n}" for s in ("Left", "Right") for n in ("ForeArmHelper", "LegHelper", "HandHelper")
]
CONTRACT_DEFORM_SET = set(CORE_BONES + DIGIT_BONES + TWIST_BONES + HELPER_BONES)
CONTRACT_END_SET = {
    "HeadTop_End", "LeftToe_End", "RightToe_End",
    *(f"{s}Hand{d}Tip" for s in ("Left", "Right") for d in DIGIT_NAMES),
    *(f"{s}Hand{d}4" for s in ("Left", "Right") for d in DIGIT_NAMES),
}
CONTRACT_ALL = CONTRACT_DEFORM_SET | CONTRACT_END_SET

# Expected R2 parent map for path validation (short names without mixamorig:)
EXPECTED_PARENT = {
    "Hips": None,
    "Spine": "Hips", "Spine1": "Spine", "Spine2": "Spine1",
    "Neck": "Spine2", "Head": "Neck", "HeadTop_End": "Head",
}
for _s in ("Left", "Right"):
    EXPECTED_PARENT.update({
        f"{_s}Shoulder": "Spine2", f"{_s}Arm": f"{_s}Shoulder",
        f"{_s}ForeArm": f"{_s}Arm", f"{_s}Hand": f"{_s}ForeArm",
        f"{_s}ArmTwist": f"{_s}Arm",
        f"{_s}ForeArmTwist1": f"{_s}ForeArm", f"{_s}ForeArmTwist2": f"{_s}ForeArm",
        f"{_s}ForeArmHelper": f"{_s}Arm",
        f"{_s}HandHelper": f"{_s}ForeArm",
        f"{_s}UpLeg": "Hips", f"{_s}Leg": f"{_s}UpLeg",
        f"{_s}Foot": f"{_s}Leg", f"{_s}ToeBase": f"{_s}Foot", f"{_s}Toe_End": f"{_s}ToeBase",
        f"{_s}LegHelper": f"{_s}UpLeg",
    })
    for _d in DIGIT_NAMES:
        EXPECTED_PARENT[f"{_s}Hand{_d}1"] = f"{_s}Hand"
        EXPECTED_PARENT[f"{_s}Hand{_d}2"] = f"{_s}Hand{_d}1"
        EXPECTED_PARENT[f"{_s}Hand{_d}3"] = f"{_s}Hand{_d}2"
        EXPECTED_PARENT[f"{_s}Hand{_d}Tip"] = f"{_s}Hand{_d}3"

LOOP_TARGETS = {
    "ForeArm": 3, "Leg": 3,
    "Arm": 2, "UpLeg": 2, "Hand": 2, "Foot": 2,
}


def repo_root() -> Path:
    return Path(__file__).resolve().parents[3]


def png_dimensions(path: Path) -> tuple[int, int] | None:
    try:
        with open(path, "rb") as f:
            hdr = f.read(24)
        if len(hdr) == 24 and hdr[:8] == b"\x89PNG\r\n\x1a\n" and hdr[12:16] == b"IHDR":
            return struct.unpack(">II", hdr[16:24])
    except OSError:
        pass
    return None


def iter_action_fcurves(action):
    """Blender 5.x layered Actions compatible F-curve iterator."""
    if hasattr(action, "layers") and action.layers:
        for layer in action.layers:
            for strip in getattr(layer, "strips", ()):
                for bag in getattr(strip, "channelbags", ()):
                    for fc in bag.fcurves:
                        yield fc
        return
    if hasattr(action, "fcurves"):
        for fc in action.fcurves:
            yield fc


def load_reference_rotations(ref_source: Path) -> dict[str, Matrix]:
    """Compute reference rest 3x3 world orientations from roster_clips.glb (or ref_rest.json)."""
    if not ref_source.exists():
        return {}
    if ref_source.suffix.lower() == ".json":
        raw = json.loads(ref_source.read_text(encoding="utf-8"))
        return {k: Matrix(v["rot"]).to_3x3().normalized() for k, v in raw.items() if "rot" in v}

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(ref_source))
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    arm = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
    if arm is None:
        return {}
    if arm.animation_data:
        arm.animation_data_clear()
    AM = arm.matrix_world.copy()
    AMi = AM.inverted()
    pb = arm.pose.bones
    for b in pb:
        b.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()

    def rotate_world(name: str, axis: Vector, deg: float) -> None:
        b = pb[name]
        pivot = AM @ b.head
        R = Matrix.Rotation(math.radians(deg), 4, axis)
        b.matrix = AMi @ (Matrix.Translation(pivot) @ R @ Matrix.Translation(-pivot) @ AM @ b.matrix)
        bpy.context.view_layer.update()

    for side, sgn in (("Left", 1.0), ("Right", -1.0)):
        bn = P + side + "Arm"
        if bn not in pb:
            continue
        d = (AM @ pb[bn].tail - AM @ pb[bn].head).normalized()
        cur = math.degrees(math.asin(max(-1.0, min(1.0, -d.z))))
        rotate_world(bn, Vector((0, 1, 0)) * sgn, -(ARM_DROP_DEG - cur))
        d2 = (AM @ pb[bn].tail - AM @ pb[bn].head).normalized()
        if abs(math.degrees(math.asin(max(-1.0, min(1.0, -d2.z)))) - ARM_DROP_DEG) > 1.0:
            rotate_world(bn, Vector((0, 1, 0)) * sgn, 2.0 * (ARM_DROP_DEG - cur))

    out: dict[str, Matrix] = {}
    for b in arm.data.bones:
        if b.name.startswith(P):
            m = AM @ pb[b.name].matrix
            out[b.name[len(P):]] = m.to_3x3().normalized()
    return out


def load_character_def(root: Path, kid_stem: str) -> dict | None:
    p = root / "unity" / "Data" / f"characters__{kid_stem.lower()}.json"
    if not p.exists():
        return None
    try:
        raw = json.loads(p.read_text(encoding="utf-8"))
        return raw.get("data", raw)
    except (OSError, json.JSONDecodeError):
        return None


def resolve_anatomical_bones(arm, H: float, floor_z: float) -> dict[str, str]:
    """Map canonical short names (Hips, LeftArm, LeftForeArm, ...) to actual bone names.

    Works on mixamorig: rigs, standard humanoid rigs (Tere), and Meshy Bone_NNN auto-rigs.
    """
    B = arm.data.bones
    AM = arm.matrix_world
    names = set(B.keys())
    mapping: dict[str, str] = {}

    if any(n.startswith(P) for n in names):
        for n in names:
            if n.startswith(P):
                mapping[n[len(P):]] = n
        return mapping

    # Standard humanoid naming (e.g. raw/Tere.glb)
    if {"Hips", "LeftUpperArm", "LeftLowerArm"} <= names:
        table = {
            "Hips": "Hips", "Spine": "Chest", "Spine2": "UpperChest",
            "Neck": "Neck", "Head": "Head", "HeadTop_End": "head_end",
        }
        for s in ("Left", "Right"):
            table.update({
                f"{s}Shoulder": f"{s}Shoulder", f"{s}Arm": f"{s}UpperArm",
                f"{s}ForeArm": f"{s}LowerArm", f"{s}Hand": f"{s}Hand",
                f"{s}UpLeg": f"{s}UpperLeg", f"{s}Leg": f"{s}LowerLeg",
                f"{s}Foot": f"{s}Foot", f"{s}ToeBase": f"{s}Toes",
            })
        for k, v in table.items():
            if v in names:
                mapping[k] = v
        return mapping

    # Heuristic fallback for Meshy Bone_NNN skeletons (matches normalize_kid.py)
    head = {b.name: np.array(AM @ b.head_local) for b in B}
    tail = {b.name: np.array(AM @ b.tail_local) for b in B}

    def sub_min_z(b):
        return min(head[x.name][2] for x in [b] + list(b.children_recursive))

    def follow(b):
        ch = [b]
        while len(ch[-1].children) == 1:
            ch.append(ch[-1].children[0])
        return ch

    reach = [
        b for b in B
        if head[b.name][2] > floor_z + 0.35 * H
        and sum(1 for c in b.children if sub_min_z(c) < floor_z + 0.12 * H) >= 2
    ]
    if not reach:
        return mapping
    hips = max(reach, key=lambda b: len(b.parent_recursive))
    mapping["Hips"] = hips.name

    legs = [c for c in hips.children if sub_min_z(c) < floor_z + 0.12 * H]
    for s, sgn in (("Left", 1.0), ("Right", -1.0)):
        if not legs:
            break
        leg = max(legs, key=lambda c: sgn * head[c.name][0])
        ch = follow(leg)
        foot = ch[-1] if len(ch[-1].children) >= 2 else next(
            (x for x in ch if head[x.name][2] < floor_z + 0.12 * H), ch[-1]
        )
        i = ch.index(foot)
        if i >= 2:
            mapping[f"{s}UpLeg"] = ch[i - 2].name
            mapping[f"{s}Leg"] = ch[i - 1].name
        elif i == 1:
            mapping[f"{s}UpLeg"] = ch[0].name
        mapping[f"{s}Foot"] = foot.name
        kids = list(foot.children) or ([ch[i + 1]] if i + 1 < len(ch) else [])
        if kids:
            toe = min(kids, key=lambda c: tail[c.name][1])
            mapping[f"{s}ToeBase"] = toe.name

    top = max(B, key=lambda b: head[b.name][2])
    spine_c = [c for c in hips.children if top == c or top in c.children_recursive]
    if spine_c:
        ch = follow(spine_c[0])
        chest = ch[-1]
        if len(ch) == 1:
            mapping["Spine2"] = chest.name
        elif len(ch) == 2:
            mapping["Spine"] = ch[0].name
            mapping["Spine2"] = chest.name
        else:
            mapping["Spine"] = ch[0].name
            mapping["Spine1"] = ch[len(ch) // 2].name
            mapping["Spine2"] = chest.name
        neck_c = [c for c in chest.children if top == c or top in c.children_recursive]
        neck = neck_c[0] if neck_c else (
            min(chest.children, key=lambda c: abs(head[c.name][0]) - head[c.name][2])
            if chest.children else None
        )
        if neck is not None:
            for k, b in zip(("Neck", "Head", "HeadTop_End"), follow(neck)):
                mapping[k] = b.name
        arm_candidates = [c for c in chest.children if c != neck]
        for s, sgn in (("Left", 1.0), ("Right", -1.0)):
            if not arm_candidates:
                break
            a0 = max(arm_candidates, key=lambda c: sgn * head[c.name][0])
            ach = follow(a0)
            hand = ach[-1]
            mapping[f"{s}Hand"] = hand.name
            rest = ach[:-1]
            if len(rest) >= 4 or (len(rest) == 3 and abs(head[rest[0].name][0]) < 0.06 * H):
                mapping[f"{s}Shoulder"] = rest[0].name
                rest = rest[1:]
            if len(rest) >= 2:
                mapping[f"{s}Arm"] = rest[0].name
                mapping[f"{s}ForeArm"] = rest[1].name
            elif len(rest) == 1:
                mapping[f"{s}Arm"] = rest[0].name
    return mapping


def count_digits(arm, anat: dict[str, str], H: float, floor_z: float) -> dict:
    """Count digit chains per hand both by contract names and geometrically by leaf tips."""
    B = arm.data.bones
    AM = arm.matrix_world
    res = {}
    for side, sgn in (("Left", 1.0), ("Right", -1.0)):
        contract_digits = [d for d in DIGIT_NAMES if f"{P}{side}Hand{d}1" in B]
        hand_bn = anat.get(f"{side}Hand")
        geom_tips = []
        chain_roots = []
        if hand_bn and hand_bn in B:
            hb = B[hand_bn]
            leaves = [c for c in hb.children_recursive if not c.children]
            geom_tips = [c.name for c in leaves]
            # direct chains off hand (excluding helper/metacarpal single-bone stubs)
            for c in hb.children:
                if any(tok in c.name.lower() for tok in ("helper", "twist")):
                    continue
                # If a metacarpal carrier fans out into multiple digits, count its children
                if len(c.children) >= 2:
                    for gc in c.children:
                        chain_roots.append(gc.name)
                else:
                    chain_roots.append(c.name)
        else:
            pts = [(b, AM @ b.tail_local) for b in B]
            xs = max((abs(p.x) for _, p in pts), default=1.0)
            geom_tips = [
                b.name for b, p in pts
                if not b.children and sgn * p.x > 0.65 * xs
                and floor_z + 0.25 * H < p.z < floor_z + 0.75 * H
            ]
        res[side] = {
            "contract_count": len(contract_digits),
            "contract_digits": contract_digits,
            "geom_tip_count": len(geom_tips),
            "chain_root_count": len(chain_roots),
        }
    return res


def measure_joint_loops(mesh, arm, anat: dict[str, str], co: np.ndarray, dom_bone: list[str | None]) -> dict:
    """Measure edge-loop density around elbows, knees, shoulders, hips, wrists, and ankles."""
    B = arm.data.bones
    AM = arm.matrix_world
    me = mesh.data
    n_edges = len(me.edges)
    if n_edges == 0:
        return {}
    ev = np.empty(n_edges * 2, dtype=np.int64)
    me.edges.foreach_get("vertices", ev)
    ev = ev.reshape(-1, 2)
    p0 = co[ev[:, 0]]
    p1 = co[ev[:, 1]]
    ediff = p1 - p0
    elen = np.linalg.norm(ediff, axis=1)

    # Face quad ratio per vertex (for pole/tri detection in crease)
    poly_is_quad = np.array([len(p.vertices) == 4 for p in me.polygons], dtype=bool)
    poly_centers = np.array([tuple(mesh.matrix_world @ p.center) for p in me.polygons], dtype=np.float64) if len(me.polygons) else np.zeros((0, 3))

    out = {}
    for s in ("Left", "Right"):
        for short, min_loops in LOOP_TARGETS.items():
            key = s + short
            bn = anat.get(key)
            if not bn or bn not in B:
                out[key] = {"loops": 0, "target": min_loops, "ok": False, "reason": "missing_bone"}
                continue
            b = B[bn]
            jpos = np.array(AM @ b.head_local, dtype=np.float64)
            tail = np.array(AM @ b.tail_local, dtype=np.float64)
            seg_vec = tail - jpos
            seg_len = float(np.linalg.norm(seg_vec))
            if seg_len < 1e-5:
                out[key] = {"loops": 0, "target": min_loops, "ok": False, "reason": "zero_len"}
                continue
            axis = seg_vec / seg_len
            par_bn = b.parent.name if b.parent else None
            # Blend axis with parent bone direction when continuous limb
            if b.parent is not None and short in ("ForeArm", "Leg", "Hand", "Foot"):
                p_vec = np.array(AM @ b.parent.tail_local) - np.array(AM @ b.parent.head_local)
                p_len = float(np.linalg.norm(p_vec))
                if p_len > 1e-5:
                    bis = axis + p_vec / p_len
                    if np.linalg.norm(bis) > 1e-5:
                        axis = bis / np.linalg.norm(bis)

            # Estimate local limb radius from vertices dominated by this bone
            valid_bones = {bn}
            if par_bn:
                valid_bones.add(par_bn)
            # Include twist/helper bones attached to this joint
            for extra in (f"{P}{s}ArmTwist", f"{P}{s}ForeArmTwist1", f"{P}{s}ForeArmTwist2",
                          f"{P}{s}ForeArmHelper", f"{P}{s}LegHelper", f"{P}{s}HandHelper"):
                if extra in B:
                    valid_bones.add(extra)
            mask_bone = np.array([db in valid_bones for db in dom_bone], dtype=bool)
            own_mask = np.array([db == bn for db in dom_bone], dtype=bool)

            rel = co - jpos
            proj = rel @ axis
            rad = np.linalg.norm(rel - np.outer(proj, axis), axis=1)

            seg_core = own_mask & (proj > 0.2 * seg_len) & (proj < 0.7 * seg_len)
            if seg_core.any():
                r_est = float(np.percentile(rad[seg_core], 35))
            elif own_mask.any():
                r_est = float(np.percentile(rad[own_mask], 35))
            else:
                r_est = 0.035 if short in ("Hand", "Foot") else 0.055
            r_est = float(np.clip(r_est, 0.015, 0.14))

            band = float(np.clip((0.8 if short in ("Hand", "Foot") else 1.0) * r_est, 0.015, 0.065))
            in_cyl = mask_bone & (np.abs(proj) <= band) & (rad <= 1.7 * r_est)
            v_count = int(in_cyl.sum())
            if v_count < 6:
                out[key] = {"loops": 0, "target": min_loops, "ok": False, "verts": v_count, "radius_m": round(r_est, 4)}
                continue

            e_in = in_cyl[ev[:, 0]] & in_cyl[ev[:, 1]] & (elen > 1e-6)
            if not e_in.any():
                out[key] = {"loops": 0, "target": min_loops, "ok": False, "verts": v_count, "radius_m": round(r_est, 4)}
                continue

            ds = np.abs(ediff[e_in] @ axis)
            cos_a = ds / elen[e_in]
            axial_mask = cos_a >= 0.45
            circ_mask = cos_a < 0.35

            if axial_mask.any():
                med_step = float(np.median(ds[axial_mask]))
                est_loops = max(1, int(round((2.0 * band) / max(med_step, 1e-5))))
            else:
                med_step = float(np.median(elen[e_in]))
                est_loops = max(1, int(round((2.0 * band) / max(med_step, 1e-5))))

            # Cross-check axial coverage: count populated axial slices across [-band, +band]
            s_vals = proj[in_cyl]
            n_bins = max(min_loops, min(12, est_loops))
            hist, _ = np.histogram(s_vals, bins=n_bins, range=(-band, band))
            active_bins = int((hist >= 4).sum())
            loops = min(est_loops, max(active_bins, 1))

            # Quad ratio in joint band
            if len(poly_centers):
                pc_rel = poly_centers - jpos
                pc_proj = pc_rel @ axis
                pc_rad = np.linalg.norm(pc_rel - np.outer(pc_proj, axis), axis=1)
                p_in = (np.abs(pc_proj) <= band) & (pc_rad <= 1.7 * r_est)
                quad_pct = round(100.0 * float(poly_is_quad[p_in].mean()), 1) if p_in.any() else 0.0
            else:
                quad_pct = 0.0

            out[key] = {
                "loops": loops,
                "target": min_loops,
                "ok": loops >= min_loops,
                "verts": v_count,
                "radius_m": round(r_est, 4),
                "band_m": round(band, 4),
                "axial_step_mm": round(med_step * 1000.0, 2),
                "circ_edges": int(circ_mask.sum()),
                "quad_pct": quad_pct,
            }
    return out


def audit_file(glb_path: Path, root: Path, ref_rots: dict[str, Matrix]) -> dict:
    """Run complete R0 audit on one .glb file inside the active Blender session."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(glb_path))
    bpy.context.view_layer.update()

    kid_stem = glb_path.stem
    kid_id = kid_stem.lower()
    char_def = load_character_def(root, kid_id)
    file_size_mb = round(glb_path.stat().st_size / (1024 * 1024), 2)

    arm = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    skinned_meshes = [o for o in meshes if o.vertex_groups]
    primary_mesh = (
        max(skinned_meshes, key=lambda o: len(o.data.vertices))
        if skinned_meshes else (max(meshes, key=lambda o: len(o.data.vertices)) if meshes else None)
    )

    if arm is not None:
        arm.data.pose_position = "REST"
        bpy.context.view_layer.update()

    # ---- Transforms & bounds
    arm_scale = [round(float(x), 5) for x in arm.matrix_world.to_scale()] if arm else None
    mesh_scale = [round(float(x), 5) for x in primary_mesh.matrix_world.to_scale()] if primary_mesh else None
    arm_rot_deg = [round(math.degrees(x), 2) for x in arm.matrix_world.to_euler()] if arm else None
    mesh_rot_deg = [round(math.degrees(x), 2) for x in primary_mesh.matrix_world.to_euler()] if primary_mesh else None
    scale_is_one = (
        arm_scale is not None
        and all(abs(s - 1.0) <= 1e-3 for s in arm_scale)
        and (mesh_scale is None or all(abs(s - 1.0) <= 1e-3 for s in mesh_scale))
    )
    rot_applied = (
        arm_rot_deg is not None
        and all(abs(a) <= 0.5 for a in arm_rot_deg)
        and (mesh_rot_deg is None or all(abs(a) <= 0.5 for a in mesh_rot_deg))
    )

    # Vertices in world space (vectorized)
    total_tris = sum(sum(len(p.vertices) - 2 for p in m.data.polygons) for m in meshes)
    total_verts = sum(len(m.data.vertices) for m in meshes)

    if primary_mesh is not None and len(primary_mesh.data.vertices) > 0:
        me = primary_mesh.data
        nv = len(me.vertices)
        flat = np.empty(nv * 3, dtype=np.float64)
        me.vertices.foreach_get("co", flat)
        local_co = flat.reshape(-1, 3)
        mw = np.array(primary_mesh.matrix_world, dtype=np.float64)
        co = local_co @ mw[:3, :3].T + mw[:3, 3]
        zmin, zmax = float(co[:, 2].min()), float(co[:, 2].max())
        height_m = round(zmax - zmin, 4)
        sole_z_m = round(zmin, 4)
        primary_tris = sum(len(p.vertices) - 2 for p in me.polygons)
        primary_verts = nv
    else:
        co = np.zeros((0, 3), dtype=np.float64)
        zmin, zmax, height_m, sole_z_m = 0.0, 0.0, 0.0, 0.0
        primary_tris, primary_verts = 0, 0

    # ---- Materials & Textures
    mats = [m.name for m in bpy.data.materials]
    embedded_textures = []
    for img in bpy.data.images:
        w, h = int(img.size[0]), int(img.size[1])
        if w == 0 and h == 0:
            continue
        embedded_textures.append({
            "name": img.name,
            "width": w,
            "height": h,
            "packed": img.packed_file is not None,
            "file_format": img.file_format,
        })
    external_textures = []
    for suffix in ("_2.png", "_normal.png", "_orm.png", "_base.png"):
        ext_p = glb_path.parent / f"{kid_stem}{suffix}"
        if not ext_p.exists():
            ext_p = glb_path.parent / f"{kid_id}{suffix}"
        if ext_p.exists():
            dims = png_dimensions(ext_p)
            external_textures.append({
                "file": ext_p.name,
                "width": dims[0] if dims else 0,
                "height": dims[1] if dims else 0,
                "size_kb": round(ext_p.stat().st_size / 1024.0, 1),
            })

    # ---- Skeleton vs R2 contract
    anat: dict[str, str] = {}
    skel_info: dict = {}
    facing_info: dict = {"yaw_err_deg": None, "left_is_plus_x": None, "facing_ok": False}
    rest_rot_info: dict = {"checked": 0, "max_err_deg": None, "worst_bone": None, "over_1deg": 0}
    digits_info: dict = {}
    extra_chains: list[dict] = []
    forbidden_bones: list[str] = []

    if arm is not None:
        B = arm.data.bones
        AM = arm.matrix_world
        anat = resolve_anatomical_bones(arm, max(height_m, 0.1), zmin)
        roots = [b.name for b in B if b.parent is None]
        has_mixamorig = any(b.name.startswith(P) for b in B)
        short_present = {b.name[len(P):] for b in B if b.name.startswith(P)}

        core_present = [n for n in CORE_BONES if n in short_present]
        core_missing = [n for n in CORE_BONES if n not in short_present]
        digit_missing = [n for n in DIGIT_BONES if n not in short_present]
        twist_present = [n for n in TWIST_BONES if n in short_present]
        twist_missing = [n for n in TWIST_BONES if n not in short_present]
        helper_present = [n for n in HELPER_BONES if n in short_present]
        helper_missing = [n for n in HELPER_BONES if n not in short_present]

        # Path mismatches among present contract bones
        path_mismatches = []
        for short in sorted(short_present & set(EXPECTED_PARENT)):
            b = B[P + short]
            exp_par = EXPECTED_PARENT[short]
            act_par = (b.parent.name[len(P):] if b.parent.name.startswith(P) else b.parent.name) if b.parent else None
            if act_par != exp_par:
                path_mismatches.append({"bone": short, "expected_parent": exp_par, "actual_parent": act_par})

        # Forbidden bones: neutral_bone, metacarpal/WristExtra carriers, roots above Hips
        hips_bn = anat.get("Hips")
        hips_ancestors = {a.name for a in B[hips_bn].parent_recursive} if hips_bn and hips_bn in B else set()
        for b in B:
            nl = b.name.lower()
            if "neutral" in nl:
                forbidden_bones.append(b.name)
            elif "wristextra" in nl or "metacarpal" in nl:
                forbidden_bones.append(b.name)
            elif b.name in hips_ancestors:
                forbidden_bones.append(b.name)
            elif has_mixamorig and not b.name.startswith(P):
                # Path-breaking non-mixamorig bone that has mixamorig descendants
                if any(c.name.startswith(P) for c in b.children_recursive):
                    forbidden_bones.append(b.name)
        forbidden_bones = sorted(set(forbidden_bones))

        # Extra leaf chains (hair/skirt/cape)
        non_contract_roots = [
            b for b in B
            if (not b.name.startswith(P) or b.name[len(P):] not in CONTRACT_ALL)
            and b.name not in forbidden_bones
            and (b.parent is None or (b.parent.name.startswith(P) and b.parent.name[len(P):] in CONTRACT_ALL))
            and not any(c.name.startswith(P) and c.name[len(P):] in CONTRACT_ALL for c in b.children_recursive)
        ]
        if has_mixamorig:
            for r in non_contract_roots:
                sub = [r] + list(r.children_recursive)
                # max depth from r
                max_depth = max(len(x.parent_recursive) - len(r.parent_recursive) + 1 for x in sub)
                extra_chains.append({
                    "root": r.name,
                    "parent": r.parent.name if r.parent else None,
                    "bones": len(sub),
                    "depth": max_depth,
                })

        # Facing & left axis check
        l_ref = anat.get("LeftUpLeg") or anat.get("LeftArm")
        r_ref = anat.get("RightUpLeg") or anat.get("RightArm")
        left_is_plus_x = None
        if l_ref and r_ref and l_ref in B and r_ref in B:
            lv = (AM @ B[l_ref].head_local) - (AM @ B[r_ref].head_local)
            left_is_plus_x = bool(lv.x > 0.01)

        fwd_vecs = []
        for s in ("Left", "Right"):
            f_bn, t_bn = anat.get(f"{s}Foot"), anat.get(f"{s}ToeBase")
            if f_bn and t_bn and f_bn in B and t_bn in B:
                v = (AM @ B[t_bn].head_local) - (AM @ B[f_bn].head_local)
                v.z = 0.0
                if v.length > 1e-4:
                    fwd_vecs.append(v.normalized())
        if fwd_vecs:
            fv = sum(fwd_vecs, Vector((0, 0, 0))).normalized()
            yaw_err = math.degrees(math.atan2(fv.x, -fv.y))
            facing_info = {
                "fwd_xy": [round(fv.x, 3), round(fv.y, 3)],
                "yaw_err_deg": round(yaw_err, 2),
                "left_is_plus_x": left_is_plus_x,
                "facing_ok": abs(yaw_err) <= 10.0 and bool(left_is_plus_x),
            }
        else:
            facing_info["left_is_plus_x"] = left_is_plus_x

        # Rest rotation comparison vs roster reference
        if ref_rots and has_mixamorig:
            errs = []
            for short, R_ref in ref_rots.items():
                if any(d in short for d in DIGIT_NAMES) or short in CONTRACT_END_SET:
                    continue
                bn = P + short
                if bn not in B:
                    continue
                R_kid = (AM @ B[bn].matrix_local).to_3x3().normalized()
                dR = R_kid.transposed() @ R_ref
                tr = max(-1.0, min(1.0, (dR[0][0] + dR[1][1] + dR[2][2] - 1.0) * 0.5))
                ang = math.degrees(math.acos(tr))
                errs.append((ang, short))
            if errs:
                errs.sort(reverse=True)
                rest_rot_info = {
                    "checked": len(errs),
                    "max_err_deg": round(errs[0][0], 2),
                    "worst_bone": errs[0][1],
                    "over_1deg": sum(1 for a, _ in errs if a > 1.0),
                }

        digits_info = count_digits(arm, anat, max(height_m, 0.1), zmin)
        skel_info = {
            "total_bones": len(B),
            "roots": roots,
            "root_ok": roots == [P + "Hips"],
            "has_mixamorig_prefix": has_mixamorig,
            "core_present": len(core_present),
            "core_total": len(CORE_BONES),
            "core_missing": core_missing,
            "digit_bones_missing_count": len(digit_missing),
            "twist_present": len(twist_present),
            "twist_missing": twist_missing,
            "helper_present": len(helper_present),
            "helper_missing": helper_missing,
            "path_mismatches": path_mismatches,
            "extra_chains": extra_chains,
            "extra_chains_ok": len(extra_chains) <= 4 and all(c["depth"] <= 4 for c in extra_chains),
            "forbidden_bones": forbidden_bones,
            "digits": digits_info,
            "rest_rot_vs_ref": rest_rot_info,
        }

    # ---- Skinning audit (primary mesh)
    skin_info: dict = {}
    joint_loops: dict = {}
    if primary_mesh is not None and arm is not None and primary_verts > 0:
        me = primary_mesh.data
        B = arm.data.bones
        AM = arm.matrix_world
        bnames = set(B.keys())
        vgs = primary_mesh.vertex_groups
        vg_names = [g.name for g in vgs]
        groups_without_bones = [n for n in vg_names if n not in bnames]
        bone_head_x = {b.name: float((AM @ b.head_local).x) for b in B}

        # Side tag per group index: +1 Left, -1 Right, 0 center/unknown
        vg_side = []
        for n in vg_names:
            nl = n.lower()
            if "left" in nl or nl.endswith(".l") or nl.endswith("_l"):
                vg_side.append(1)
            elif "right" in nl or nl.endswith(".r") or nl.endswith("_r"):
                vg_side.append(-1)
            elif n in bone_head_x and abs(bone_head_x[n]) > 0.03:
                vg_side.append(1 if bone_head_x[n] > 0 else -1)
            else:
                vg_side.append(0)

        neutral_gids = {i for i, n in enumerate(vg_names) if "neutral" in n.lower()}
        unweighted = 0
        over_4 = 0
        max_infl = 0
        cross_side = 0
        neutral_verts = 0
        deforming_gids: set[int] = set()
        dom_bone: list[str | None] = [None] * primary_verts
        weights_list: list[dict[int, float]] = []

        xs_world = co[:, 0]
        for i, v in enumerate(me.vertices):
            wmap: dict[int, float] = {}
            tot = 0.0
            best_g = -1
            best_w = 0.0
            has_neutral = False
            has_cross = False
            vx = float(xs_world[i])
            for g in v.groups:
                w = float(g.weight)
                if w <= 1e-6:
                    continue
                gi = g.group
                wmap[gi] = w
                tot += w
                if w > best_w:
                    best_w = w
                    best_g = gi
                if w > 0.001:
                    deforming_gids.add(gi)
                if gi in neutral_gids and w > 0.01:
                    has_neutral = True
                if w > 0.01 and abs(vx) > 0.03 and vg_side[gi] != 0:
                    if (vx > 0.03 and vg_side[gi] < 0) or (vx < -0.03 and vg_side[gi] > 0):
                        has_cross = True
            weights_list.append(wmap)
            if best_g >= 0:
                dom_bone[i] = vg_names[best_g]
            if tot < 1e-4:
                unweighted += 1
            n_inf = sum(1 for w in wmap.values() if w > 1e-4)
            if n_inf > max_infl:
                max_infl = n_inf
            if n_inf > 4:
                over_4 += 1
            if has_cross:
                cross_side += 1
            if has_neutral:
                neutral_verts += 1

        # Seam twins (vertices sharing position within 1e-5 m)
        qpos = np.round(co / 1e-5).astype(np.int64)
        pos_groups: dict[tuple[int, int, int], list[int]] = {}
        for idx, key_t in enumerate(map(tuple, qpos)):
            pos_groups.setdefault(key_t, []).append(idx)
        welded_verts = len(pos_groups)
        seam_twin_groups = 0
        seam_twin_verts = 0
        seam_mismatch_groups = 0
        max_seam_diff = 0.0
        for ids in pos_groups.values():
            if len(ids) <= 1:
                continue
            seam_twin_groups += 1
            seam_twin_verts += len(ids)
            ref_w = weights_list[ids[0]]
            group_diff = 0.0
            for j in ids[1:]:
                wj = weights_list[j]
                all_k = set(ref_w.keys()) | set(wj.keys())
                for k in all_k:
                    d = abs(ref_w.get(k, 0.0) - wj.get(k, 0.0))
                    if d > group_diff:
                        group_diff = d
            if group_diff > 1e-3:
                seam_mismatch_groups += 1
                if group_diff > max_seam_diff:
                    max_seam_diff = group_diff

        deforming_names = {vg_names[gi] for gi in deforming_gids if vg_names[gi] in bnames}
        deforming_bone_count = len(deforming_names)
        weighted_twists = sum(1 for n in TWIST_BONES if (P + n) in deforming_names)
        weighted_helpers = sum(1 for n in HELPER_BONES if (P + n) in deforming_names)
        shape_keys = [kb.name for kb in me.shape_keys.key_blocks if kb.name != "Basis"] if me.shape_keys else []
        corr_shapes = [n for n in shape_keys if n.startswith("corr_")]
        skin_info = {
            "deforming_bones": deforming_bone_count,
            "deforming_bones_ok": deforming_bone_count <= 75,
            "weighted_twist_bones": weighted_twists,
            "weighted_helper_bones": weighted_helpers,
            "shape_key_count": len(shape_keys),
            "corrective_shapes": corr_shapes,
            "has_kalyeah_correctives_meta": "kalyeah_correctives" in me,
            "groups_without_bones": groups_without_bones,
            "unweighted_verts": unweighted,
            "over_4_influences": over_4,
            "max_influences": max_infl,
            "cross_side_weights": cross_side,
            "neutral_bone_verts": neutral_verts,
            "welded_verts": welded_verts,
            "seam_twin_groups": seam_twin_groups,
            "seam_twin_verts": seam_twin_verts,
            "seam_twin_mismatch": seam_mismatch_groups,
            "seam_twin_max_diff": round(max_seam_diff, 4),
        }

        joint_loops = measure_joint_loops(primary_mesh, arm, anat, co, dom_bone)

    # ---- Clips
    fps = float(bpy.context.scene.render.fps) / max(float(bpy.context.scene.render.fps_base), 1e-6)
    clips_info = []
    any_scale_keys = False
    hips_name = anat.get("Hips")
    for act in sorted(bpy.data.actions, key=lambda a: a.name):
        f0, f1 = float(act.frame_range[0]), float(act.frame_range[1])
        has_scale = False
        hips_loc_fc: dict[int, list[float]] = {}
        for fc in iter_action_fcurves(act):
            dp = fc.data_path
            if dp.endswith(".scale"):
                # Check if non-unit scale keys exist
                vals = [kp.co[1] for kp in fc.keyframe_points]
                if vals and any(abs(v - 1.0) > 1e-3 for v in vals):
                    has_scale = True
            if hips_name and dp == f'pose.bones["{hips_name}"].location':
                hips_loc_fc[fc.array_index] = [kp.co[1] for kp in fc.keyframe_points]
        if has_scale:
            any_scale_keys = True
        root_xy = 0.0
        if hips_loc_fc:
            # Horizontal displacement in armature space (X and Y in Blender, or X and Z in bone-local)
            # Measure max 2D travel across non-vertical channels (0 and 2 in bone-local Hips)
            dx = (max(hips_loc_fc[0]) - min(hips_loc_fc[0])) if 0 in hips_loc_fc and hips_loc_fc[0] else 0.0
            dz = (max(hips_loc_fc[2]) - min(hips_loc_fc[2])) if 2 in hips_loc_fc and hips_loc_fc[2] else 0.0
            root_xy = round(math.hypot(dx, dz), 4)
        clips_info.append({
            "name": act.name,
            "start_frame": round(f0, 2),
            "end_frame": round(f1, 2),
            "frames": round(f1 - f0, 2),
            "fps": round(fps, 2),
            "duration_s": round((f1 - f0) / max(fps, 1e-6), 3),
            "has_scale_keys": has_scale,
            "root_motion_xy_m": root_xy,
        })

    # ---- Summary verdict flags
    loops_failed = [k for k, v in joint_loops.items() if not v.get("ok", False)]
    return {
        "kid": kid_id,
        "file": str(glb_path),
        "file_size_mb": file_size_mb,
        "character_def": {
            "found": char_def is not None,
            "display_name": char_def.get("display_name") if char_def else None,
            "body_width": char_def.get("body_width") if char_def else None,
            "target_height_m": char_def.get("height_m") if char_def else None,
        },
        "transforms": {
            "arm_scale": arm_scale,
            "mesh_scale": mesh_scale,
            "arm_rot_deg": arm_rot_deg,
            "mesh_rot_deg": mesh_rot_deg,
            "scale_is_one": scale_is_one,
            "rot_applied": rot_applied,
            "height_m": height_m,
            "sole_z_m": sole_z_m,
            "facing": facing_info,
        },
        "mesh": {
            "mesh_count": len(meshes),
            "primary_mesh": primary_mesh.name if primary_mesh else None,
            "triangles": total_tris,
            "primary_triangles": primary_tris,
            "vertices": total_verts,
            "primary_vertices": primary_verts,
            "material_count": len(mats),
            "materials": mats,
            "embedded_textures": embedded_textures,
            "external_textures": external_textures,
            "budget_match_ok": total_tris <= 25000 and len(mats) <= 2 and file_size_mb <= 6.0,
            "budget_showcase_ok": total_tris <= 60000,
        },
        "skeleton": skel_info,
        "skin": skin_info,
        "joint_loops": joint_loops,
        "joint_loops_failed": loops_failed,
        "clips": {
            "count": len(clips_info),
            "any_scale_keys": any_scale_keys,
            "items": clips_info,
        },
    }


def format_table(results: list[dict]) -> str:
    hdr = (
        f"{'Kid':<9} {'MB':>5} {'Tris':>7} {'Verts':>7} {'Mat':>3} {'H(m)':>5} {'SoleZ':>6} "
        f"{'Sc1':>3} {'Fac':>3} {'Bones':>5} {'DefB':>4} {'DigL/R':>6} {'Tw/Hp':>5} {'WTw/Hp':>6} {'Corr':>4} "
        f"{'Forb':>4} {'Unw':>4} {'>4':>4} {'XSide':>5} {'SeamM':>5} {'LoopF':>5} {'RotErr':>6} {'Clips':>5}"
    )
    lines = [hdr, "-" * len(hdr)]
    for r in results:
        t = r["transforms"]
        m = r["mesh"]
        sk = r["skeleton"]
        sn = r["skin"]
        dl = sk.get("digits", {}).get("Left", {})
        dr = sk.get("digits", {}).get("Right", {})
        d_l = dl.get("contract_count") or dl.get("geom_tip_count", 0)
        d_r = dr.get("contract_count") or dr.get("geom_tip_count", 0)
        rot_err = sk.get("rest_rot_vs_ref", {}).get("max_err_deg")
        rot_s = f"{rot_err:5.1f}°" if rot_err is not None else "   n/a"
        lines.append(
            f"{r['kid']:<9} {r['file_size_mb']:5.2f} {m['triangles']:7d} {m['vertices']:7d} "
            f"{m['material_count']:3d} {t['height_m']:5.2f} {t['sole_z_m']:+6.3f} "
            f"{('Y' if t['scale_is_one'] else 'N'):>3} {('Y' if t['facing'].get('facing_ok') else 'N'):>3} "
            f"{sk.get('total_bones', 0):5d} {sn.get('deforming_bones', 0):4d} "
            f"{d_l:2d}/{d_r:<3d} {sk.get('twist_present', 0):2d}/{sk.get('helper_present', 0):<2d} "
            f"{sn.get('weighted_twist_bones', 0):2d}/{sn.get('weighted_helper_bones', 0):<3d} "
            f"{len(sn.get('corrective_shapes', [])):4d} "
            f"{len(sk.get('forbidden_bones', [])):4d} {sn.get('unweighted_verts', 0):4d} "
            f"{sn.get('over_4_influences', 0):4d} {sn.get('cross_side_weights', 0):5d} "
            f"{sn.get('seam_twin_mismatch', 0):5d} {len(r.get('joint_loops_failed', [])):5d} "
            f"{rot_s:>6} {r['clips']['count']:5d}"
        )
    return "\n".join(lines)


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser(description="R0 quantitative intake audit for Kalyeah character .glb files")
    ap.add_argument("files", nargs="*", help="Specific .glb files or directories to audit")
    ap.add_argument("--dir", dest="dirs", action="append", default=[], help="Directory of .glb files to audit")
    ap.add_argument("--ref", default=None, help="Reference rest .glb or ref_rest.json (default: clips/roster_clips.glb)")
    ap.add_argument("--out", default=None, help="Write full JSON report to this path")
    ap.add_argument("--json", action="store_true", help="Print JSON report to stdout instead of table")
    return ap.parse_args(argv)


def main() -> int:
    args = parse_args()
    root = repo_root()
    targets: list[Path] = []
    for item in list(args.dirs) + list(args.files):
        p = Path(item)
        if not p.is_absolute():
            p = (Path.cwd() / p).resolve()
        if p.is_dir():
            targets.extend(sorted(p.glob("*.glb")))
        elif p.exists():
            targets.append(p)
        else:
            targets.extend(Path(g).resolve() for g in sorted(glob.glob(item)))

    if not targets:
        default_dir = root / "assets" / "models" / "characters"
        targets = sorted(default_dir.glob("*.glb"))

    ref_path = Path(args.ref) if args.ref else (root / "tools" / "blender" / "kids" / "clips" / "roster_clips.glb")
    ref_rots = load_reference_rotations(ref_path)

    results = []
    for glb in targets:
        # Skip LOD1/LOD2 variants unless explicitly listed by file path
        if not args.files and (glb.stem.endswith("_lod1") or glb.stem.endswith("_lod2")):
            continue
        results.append(audit_file(glb, root, ref_rots))

    report = {
        "schemaVersion": 1,
        "reference": str(ref_path) if ref_path.exists() else None,
        "count": len(results),
        "models": results,
    }
    if args.out:
        out_p = Path(args.out)
        out_p.parent.mkdir(parents=True, exist_ok=True)
        out_p.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8", newline="\n")

    if args.json:
        print(json.dumps(report, indent=2))
    else:
        print(format_table(results))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
