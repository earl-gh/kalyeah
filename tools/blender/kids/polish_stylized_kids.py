#!/usr/bin/env python3
"""Valorant x Arcane stylized 3D character polish pipeline (Phases 1-4) for Kalyeah kids.

Deterministic, re-runnable Blender 5.2 script operating on assets/models/characters/<kid>.glb:
  1. Preserves exact shared R2 mixamorig skeleton (0.0 deg rest rotation error) and all 20 roster clips.
  2. Surgical weight quarantine & localized LBS Idle-stretch cliff smoothing:
     - Fixes Karding's cape-vs-arm bleed (0 cape bleed on arms, 0 arm bleed on cape).
     - Fixes Tere's (and long-hair kids') back-hair-to-arm pull (smooth Head->Neck->Spine2 hair weights).
     - Removes arm<->torso/hips/legs stray leaks across all 12 kids; smooths skirt midlines & joint zones.
     - Enforces rigid cores, zero cross-side leaks, exact seam-twin parity, <= 4 influences, normalized.
  3. Chiseled stylized plane polish (mesh):
     - Smoothly pushes Karding's cape cleanly behind Rest & Idle arm capsules (zero arm/cape clipping).
     - Stabilizes wafer-thin rims (e.g. Narda's glasses frame) & removes stray loose vertices.
     - Runs volume-preserving anisotropic normal-guided bilateral mesh filtering (smooths raw lumpy
       Meshy clay noise into chiseled Valorant/Arcane planes while keeping cloth creases & hair sharp).
  4. Multi-map baking (Cycles AO + welded discrete Mean Curvature + World-Space Vertical Gradient + UV mask).
  5. Painterly stylized PBR texturing:
     - Repairs pitch-black Cycles bake-miss pinholes & hair-edge dark bleed inside UV islands.
     - Edge-preserving vectorized bilateral painterly brushwork (protects eyes, brows, shirt decals).
     - Riot-style soft top-down luminance gradient (+11% head/torso lift, -13% grounded feet/legs).
     - Baked curvature warm edge highlights + warm saturated AO cavity shading (never dirty black).
     - Clean low-noise Tangent Normal (<kid>_2.png) & semantic ORM (<kid>_orm.png: R=AO, G=Rough, B=Metal).
  6. Adds 4 real glTF facial morph targets strictly on front-face vertices:
     Blink, Smile, Determined, Talk_Open (with exact seam-twin delta parity).
  7. Exports polished Match LOD0 .glb + <kid>_2.png + <kid>_orm.png (ready for bake_correctives + build_lod_tiers).

Usage:
  blender --background --python tools/blender/kids/polish_stylized_kids.py -- --kids karding,narda,jumong,tere
  blender --background --python tools/blender/kids/polish_stylized_kids.py -- --kids all --out-metrics metrics.json
"""
from __future__ import annotations

import argparse
import json
import math
import os
import sys
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector, kdtree

P = "mixamorig:"
ALL_KIDS = (
    "domeng", "inggo", "jopay", "jumong", "karding", "kikay",
    "manoy", "narda", "oriang", "paeng", "pamela", "tere",
)
FACIAL_SHAPES = ("Blink", "Smile", "Determined", "Talk_Open")
BASE_PX = 2048
MAP_PX = 1024


def log(*args) -> None:
    print("[polish]", *args, flush=True)


def activate(obj, mode: str = "OBJECT") -> None:
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    if mode != "OBJECT":
        bpy.ops.object.mode_set(mode=mode)


def ensure_gltf_material_output_group():
    name = "glTF Material Output"
    ng = bpy.data.node_groups.get(name)
    if ng is not None:
        return ng
    ng = bpy.data.node_groups.new(name, "ShaderNodeTree")
    ng.interface.new_socket(name="Occlusion", in_out="INPUT", socket_type="NodeSocketFloat")
    ng.nodes.new("NodeGroupInput")
    return ng


def pt_seg_dist(pts: np.ndarray, a: np.ndarray, b: np.ndarray) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    """Return (t_clamped, dist, closest_point) for pts (N,3) against segment a->b."""
    ab = b - a
    denom = max(float(ab @ ab), 1e-12)
    t_raw = ((pts - a) @ ab) / denom
    t = np.clip(t_raw, 0.0, 1.0)
    proj = a + t[:, None] * ab
    return t_raw, np.linalg.norm(pts - proj, axis=1), proj


def build_welded_topology(me, world_co: np.ndarray):
    """Build welded seam-twin groups, per-vertex welded adjacency, and welded edge list."""
    nv = len(me.vertices)
    qpos = np.round(world_co / 1e-5).astype(np.int64)
    pos_groups: dict[tuple[int, int, int], list[int]] = {}
    for i, key in enumerate(map(tuple, qpos)):
        pos_groups.setdefault(key, []).append(i)

    # Canonical welded vertex id per original vertex
    v2w = np.empty(nv, dtype=np.int32)
    w_members: list[list[int]] = []
    for wid, ids in enumerate(pos_groups.values()):
        w_members.append(ids)
        for i in ids:
            v2w[i] = wid
    nw = len(w_members)

    # Welded adjacency
    w_nbr: list[set[int]] = [set() for _ in range(nw)]
    for e in me.edges:
        wa, wb = int(v2w[e.vertices[0]]), int(v2w[e.vertices[1]])
        if wa != wb:
            w_nbr[wa].add(wb)
            w_nbr[wb].add(wa)
    w_nbr_list = [sorted(s) for s in w_nbr]

    # Original-index adjacency including seam twins
    nbr: list[list[int]] = [[] for _ in range(nv)]
    for i in range(nv):
        wid = int(v2w[i])
        # All representative vertices of neighboring welded nodes
        nbr[i] = [w_members[nw_i][0] for nw_i in w_nbr_list[wid]]

    return pos_groups, v2w, w_members, w_nbr_list, nbr


def drop_loose_components(mesh_obj) -> int:
    """Remove tiny disconnected loose vertices/shards (< 15 verts) without altering main topology."""
    me = mesh_obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    # Map via spatial grid so UV seam splits stay grouped together
    qpos = {}
    for v in bm.verts:
        k = (round(v.co.x * 1e5), round(v.co.y * 1e5), round(v.co.z * 1e5))
        qpos.setdefault(k, []).append(v)
    vis = set()
    comps = []
    for v in bm.verts:
        if v.index in vis:
            continue
        stack = [v]
        comp = []
        while stack:
            c = stack.pop()
            if c.index in vis:
                continue
            vis.add(c.index)
            comp.append(c)
            for e in c.link_edges:
                ov = e.other_vert(c)
                if ov.index not in vis:
                    stack.append(ov)
            k = (round(c.co.x * 1e5), round(c.co.y * 1e5), round(c.co.z * 1e5))
            for tw in qpos[k]:
                if tw.index not in vis:
                    stack.append(tw)
        comps.append(comp)
    comps.sort(key=len, reverse=True)
    to_del = [v for comp in comps[1:] if len(comp) < 15 for v in comp]
    removed = len(to_del)
    if to_del:
        bmesh.ops.delete(bm, geom=to_del, context="VERTS")
        bm.to_mesh(me)
        me.update()
    bm.free()
    return removed


def compute_idle_bone_matrices(arm, mesh_obj) -> tuple[dict[str, np.ndarray], dict[str, tuple[np.ndarray, np.ndarray]]]:
    """Evaluate Idle frame-0 skinning matrices (4x4 in mesh-world space) and posed bone segments."""
    scn = bpy.context.scene
    AM = arm.matrix_world.copy()
    AMi = AM.inverted()
    MW = mesh_obj.matrix_world.copy()
    idle = bpy.data.actions.get("Idle")
    saved_act = arm.animation_data.action if arm.animation_data else None
    saved_pose = arm.data.pose_position

    if idle is not None:
        if arm.animation_data is None:
            arm.animation_data_create()
        arm.animation_data.action = idle
        try:
            arm.animation_data.action_slot = idle.slots[0]
        except Exception:
            pass
        arm.data.pose_position = "POSE"
        scn.frame_set(int(idle.frame_range[0]))
        bpy.context.view_layer.update()
    else:
        arm.data.pose_position = "REST"
        bpy.context.view_layer.update()

    skin_mats: dict[str, np.ndarray] = {}
    posed_segs: dict[str, tuple[np.ndarray, np.ndarray]] = {}
    for b in arm.data.bones:
        pb = arm.pose.bones[b.name]
        # World-space vertex delta transform: M_world = AM @ pb.matrix @ b.matrix_local.inv @ AMi
        M = AM @ pb.matrix @ b.matrix_local.inverted() @ AMi
        skin_mats[b.name] = np.array(M, dtype=np.float64)
        posed_segs[b.name] = (np.array(AM @ pb.head, dtype=np.float64), np.array(AM @ pb.tail, dtype=np.float64))

    # Restore rest pose
    if arm.animation_data:
        arm.animation_data.action = saved_act
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    arm.data.pose_position = saved_pose
    bpy.context.view_layer.update()
    return skin_mats, posed_segs


def eval_lbs_world(rest_w: np.ndarray, W: list[dict[int, float]], vg_names: dict[int, str],
                   skin_mats: dict[str, np.ndarray]) -> np.ndarray:
    """Fast vectorized LBS evaluation in world space for weight-cliff diagnostics."""
    nv = len(rest_w)
    out = np.zeros((nv, 3), dtype=np.float64)
    hom = np.ones((nv, 4), dtype=np.float64)
    hom[:, :3] = rest_w
    # Accumulate per bone group (<= 75 bones -> instant!)
    by_bone: dict[str, list[tuple[int, float]]] = {}
    for i, wmap in enumerate(W):
        for g, val in wmap.items():
            if val > 1e-5:
                bn = vg_names.get(g)
                if bn in skin_mats:
                    by_bone.setdefault(bn, []).append((i, val))
    for bn, pairs in by_bone.items():
        idxs = np.fromiter((p[0] for p in pairs), dtype=np.int32, count=len(pairs))
        ws = np.fromiter((p[1] for p in pairs), dtype=np.float64, count=len(pairs))
        M = skin_mats[bn]
        pts = hom[idxs] @ M.T
        out[idxs] += pts[:, :3] * ws[:, None]
    return out


def sample_base_luminance_per_vert(me, base_px_2k: np.ndarray) -> np.ndarray:
    """Sample average base texture RGB per vertex (useful for distinguishing dark hair vs skin/shirt)."""
    H_im, W_im, _ = base_px_2k.shape
    uv_lay = me.uv_layers.active.data
    nv = len(me.vertices)
    acc = np.zeros((nv, 3), dtype=np.float32)
    cnt = np.zeros(nv, dtype=np.float32)
    for p in me.polygons:
        for li in p.loop_indices:
            vi = me.loops[li].vertex_index
            u = int(np.clip(uv_lay[li].uv.x * (W_im - 1), 0, W_im - 1))
            v = int(np.clip(uv_lay[li].uv.y * (H_im - 1), 0, H_im - 1))
            acc[vi] += base_px_2k[v, u, :3]
            cnt[vi] += 1.0
    return acc / np.maximum(cnt[:, None], 1.0)


def quarantine_and_polish_weights(
    kid: str, arm, mesh_obj, rest_w: np.ndarray, pos_groups, nbr,
    skin_mats_idle: dict[str, np.ndarray], vert_rgb: np.ndarray
) -> tuple[list[dict[int, float]], dict]:
    """Phase 4 surgical weight quarantine (cape/hair/arm/torso/skirt) + LBS stretch cliff polish."""
    me = mesh_obj.data
    vgs = mesh_obj.vertex_groups
    AM = arm.matrix_world
    B = arm.data.bones
    nv = len(me.vertices)

    vg_names = {g.index: g.name for g in vgs}
    gid = {g.name: g.index for g in vgs}

    def get_gid(name: str) -> int | None:
        if name in gid:
            return gid[name]
        if name in B and B[name].use_deform:
            g = vgs.new(name=name)
            gid[name] = g.index
            vg_names[g.index] = name
            return g.index
        return None

    W: list[dict[int, float]] = [
        {g.group: float(g.weight) for g in v.groups if g.weight > 0.005 and g.group in vg_names}
        for v in me.vertices
    ]

    # Measure BEFORE Idle stretch
    edges_np = np.array([(e.vertices[0], e.vertices[1]) for e in me.edges], dtype=np.int32)
    lr = np.linalg.norm(rest_w[edges_np[:, 0]] - rest_w[edges_np[:, 1]], axis=1)
    posed_before = eval_lbs_world(rest_w, W, vg_names, skin_mats_idle)
    li_before = np.linalg.norm(posed_before[edges_np[:, 0]] - posed_before[edges_np[:, 1]], axis=1)
    ratio_before = li_before / np.maximum(lr, 1e-5)
    st_before = int((ratio_before > 2.5).sum())
    max_st_before = float(ratio_before.max())

    cape_gids = {idx for idx, nm in vg_names.items() if nm.lower().startswith("cape")}
    skirt_gids = {idx for idx, nm in vg_names.items() if nm.lower().startswith("skirt")}
    hair_gids = {idx for idx, nm in vg_names.items() if nm.lower().startswith("hair")}
    extra_gids = {idx for idx, nm in vg_names.items() if not nm.startswith(P)}

    arm_gids_by_side: dict[str, set[int]] = {"Left": set(), "Right": set()}
    for s in ("Left", "Right"):
        for k in ("Arm", "ArmTwist", "ForeArm", "ForeArmTwist1", "ForeArmTwist2", "ForeArmHelper", "Hand", "HandHelper"):
            if (P + s + k) in gid:
                arm_gids_by_side[s].add(gid[P + s + k])
        for idx, nm in vg_names.items():
            if nm.startswith(P + s + "Hand"):
                arm_gids_by_side[s].add(idx)
    all_arm_gids = arm_gids_by_side["Left"] | arm_gids_by_side["Right"]
    shoulder_gids = {gid[P + s + "Shoulder"] for s in ("Left", "Right") if (P + s + "Shoulder") in gid}
    torso_gids = {gid[P + k] for k in ("Hips", "Spine", "Spine1", "Spine2") if (P + k) in gid}
    leg_gids = {
        gid[P + s + k] for s in ("Left", "Right")
        for k in ("UpLeg", "Leg", "LegHelper", "Foot", "ToeBase") if (P + s + k) in gid
    }

    # Bone segment geometry in world space
    bhead = {b.name: np.array(AM @ b.head_local, dtype=np.float64) for b in B}
    btail = {b.name: np.array(AM @ b.tail_local, dtype=np.float64) for b in B}

    arm_segs = {}
    for s in ("Left", "Right"):
        a0 = bhead[P + s + "Arm"]
        a1 = bhead[P + s + "ForeArm"]
        a2 = bhead[P + s + "Hand"]
        t_u, d_u, p_u = pt_seg_dist(rest_w, a0, a1)
        t_f, d_f, p_f = pt_seg_dist(rest_w, a1, a2)
        arm_segs[s] = (a0, a1, a2, t_u, d_u, p_u, t_f, d_f, p_f)

    # ---- A) Karding (or any caped kid): strict Cape vs Arm quarantine -----------------------------------------------
    cape_arm_fixed = 0
    if cape_gids:
        # Build KDTree over pure arm vertices (no cape weight) so reassigned arm vertices get exact twist/helper weights
        pure_arm_by_side = {}
        for s in ("Left", "Right"):
            pure = [
                i for i in range(nv)
                if sum(W[i].get(g, 0.0) for g in arm_gids_by_side[s]) > 0.70
                and sum(W[i].get(g, 0.0) for g in cape_gids) < 0.05
            ]
            kd = kdtree.KDTree(max(len(pure), 1))
            for k, vi in enumerate(pure):
                kd.insert(tuple(rest_w[vi]), k)
            kd.balance()
            pure_arm_by_side[s] = (pure, kd)

        spine2_z_top = float(btail[P + "Spine2"][2])
        for s, sgn in (("Left", 1.0), ("Right", -1.0)):
            a0, a1, a2, t_u, d_u, p_u, t_f, d_f, p_f = arm_segs[s]
            pure, kd = pure_arm_by_side[s]
            for i in range(nv):
                # Warm brown skin near upper arm/forearm is ALWAYS arm skin, never striped pink/white cape!
                vr, vg_c, vb_c = float(vert_rgb[i, 0]), float(vert_rgb[i, 1]), float(vert_rgb[i, 2])
                is_arm_skin = (vr > vg_c + 0.03) and (vg_c > vb_c + 0.03) and ((vr - vb_c) > 0.10) and (vr < 0.85)
                # 1) Arm vertex contaminated by Cape: close to arm segment AND strictly in front of the cape plane!
                on_uarm = (
                    (d_u[i] < (0.074 if is_arm_skin else 0.066))
                    and (0.02 < t_u[i] < 1.10)
                    and (rest_w[i, 1] < p_u[i, 1] + (0.052 if is_arm_skin else 0.044))
                )
                on_farm = (
                    (d_f[i] < (0.064 if is_arm_skin else 0.058))
                    and (0.0 < t_f[i] < 1.12)
                    and (rest_w[i, 1] < p_f[i, 1] + (0.048 if is_arm_skin else 0.044))
                )
                if (on_uarm or on_farm) and any(g in cape_gids for g in W[i]):
                    for g in list(W[i]):
                        if g in cape_gids:
                            del W[i][g]
                    if sum(W[i].values()) < 0.45 and pure:
                        _, k, _ = kd.find(tuple(rest_w[i]))
                        W[i] = dict(W[pure[k]])
                    cape_arm_fixed += 1

        # 2) Cape vertex contaminated by Arm/Leg: strictly behind arm/torso rear plane and below collar knot
        arm_y_ref = 0.5 * (float(bhead[P + "LeftArm"][1]) + float(bhead[P + "RightArm"][1]))
        for i in range(nv):
            cape_w = sum(W[i].get(g, 0.0) for g in cape_gids)
            is_rear_cape = (rest_w[i, 1] > arm_y_ref + 0.052) and (rest_w[i, 2] < spine2_z_top + 0.03)
            if cape_w > 0.25 and is_rear_cape:
                removed_any = False
                for g in list(W[i]):
                    if g in all_arm_gids or g in leg_gids:
                        del W[i][g]
                        removed_any = True
                if removed_any:
                    if not W[i]:
                        c1 = get_gid("Cape_C_1")
                        if c1 is not None:
                            W[i][c1] = 1.0
                    cape_arm_fixed += 1

    # ---- B) Long Back-Hair Quarantine (Tere & similar kids without hair spring chains) ------------------------------
    hair_fixed = 0
    if not cape_gids and not hair_gids:
        spine2_y = float(bhead[P + "Spine2"][1])
        arm_y_avg = 0.5 * (float(bhead[P + "LeftArm"][1]) + float(bhead[P + "RightArm"][1]))
        back_y_plane = max(spine2_y, arm_y_avg) + 0.068
        spine_z_low = float(bhead[P + "Hips"][2]) + 0.04
        neck_z = float(bhead[P + "Neck"][2])
        head_z = float(bhead[P + "Head"][2])
        sh_x = max(abs(float(bhead[P + "LeftArm"][0])), abs(float(bhead[P + "RightArm"][0])))
        g_head = get_gid(P + "Head")
        g_neck = get_gid(P + "Neck")
        g_sp2 = get_gid(P + "Spine2")
        g_sp1 = get_gid(P + "Spine1") or g_sp2

        # Check vertex luminance to confirm dark hair sheet behind upper back/shoulders
        lum_v = 0.299 * vert_rgb[:, 0] + 0.587 * vert_rgb[:, 1] + 0.114 * vert_rgb[:, 2]
        for i in range(nv):
            z = float(rest_w[i, 2])
            y = float(rest_w[i, 1])
            ax = abs(float(rest_w[i, 0]))
            # Behind the upper torso/arms AND dark hair (or far behind back plane above hips)
            if y > back_y_plane and spine_z_low < z < head_z + 0.12 and ax < sh_x + 0.10:
                if lum_v[i] < 0.38 or y > back_y_plane + 0.045:
                    has_bad = any(g in all_arm_gids or g in shoulder_gids or g == gid.get(P + "Hips") for g in W[i])
                    if has_bad and g_head is not None and g_neck is not None and g_sp2 is not None:
                        # Smooth vertical blend: Head -> Neck -> Spine2 -> Spine1
                        if z >= head_z:
                            W[i] = {g_head: 1.0}
                        elif z >= neck_z:
                            t = (z - neck_z) / max(head_z - neck_z, 1e-4)
                            W[i] = {g_head: t, g_neck: 1.0 - t}
                        elif z >= neck_z - 0.12:
                            t = (z - (neck_z - 0.12)) / 0.12
                            W[i] = {g_neck: 0.45 * t, g_sp2: 1.0 - 0.45 * t}
                        else:
                            t = float(np.clip((z - spine_z_low) / max(neck_z - 0.12 - spine_z_low, 1e-4), 0.0, 1.0))
                            W[i] = {g_sp2: 0.55 + 0.45 * t, g_sp1: 0.45 * (1.0 - t)}
                        hair_fixed += 1

    # ---- C) Universal Arm <-> Torso/Hips/Legs Leak Quarantine -------------------------------------------------------
    leak_fixed = 0
    sh_z = 0.5 * (float(bhead[P + "LeftArm"][2]) + float(bhead[P + "RightArm"][2]))
    sh_x_min = min(abs(float(bhead[P + "LeftArm"][0])), abs(float(bhead[P + "RightArm"][0])))
    hips_z = float(bhead[P + "Hips"][2])

    for i in range(nv):
        if any(g in cape_gids or g in skirt_gids for g in W[i]):
            continue
        x, y, z = float(rest_w[i, 0]), float(rest_w[i, 1]), float(rest_w[i, 2])
        # 1) Clearly on torso / waist / legs (medial & below armpit) -> zero out arm/hand weights
        if z < sh_z - 0.075 and abs(x) < sh_x_min - 0.015:
            # Make sure we're not near the hands/wrists
            d_lh = float(np.linalg.norm(rest_w[i] - bhead[P + "LeftHand"]))
            d_rh = float(np.linalg.norm(rest_w[i] - bhead[P + "RightHand"]))
            if d_lh > 0.11 and d_rh > 0.11:
                bad = [g for g in W[i] if g in all_arm_gids]
                if bad:
                    for g in bad:
                        del W[i][g]
                    if not W[i]:
                        fb = get_gid(P + "Spine") if z > hips_z + 0.04 else get_gid(P + "Hips")
                        if fb is not None:
                            W[i][fb] = 1.0
                    leak_fixed += 1
                    continue

        # 2) Clearly on mid/lower arm -> zero out Hips/Spine/Legs weights
        s = "Left" if x >= 0 else "Right"
        a0, a1, a2, t_u, d_u, _, t_f, d_f, _ = arm_segs[s]
        if ((d_u[i] < 0.072 and t_u[i] > 0.24) or (d_f[i] < 0.064 and t_f[i] > 0.0)) and abs(x) > sh_x_min + 0.025:
            bad = [g for g in W[i] if g in torso_gids or g in leg_gids]
            if bad:
                for g in bad:
                    del W[i][g]
                if not W[i]:
                    fb = get_gid(P + s + ("ForeArm" if d_f[i] < d_u[i] else "Arm"))
                    if fb is not None:
                        W[i][fb] = 1.0
                leak_fixed += 1

    # ---- D) Skirt Midline Smooth (narda, kikay) so thighs never rip center pleats -----------------------------------
    if skirt_gids:
        skirt_mid = [
            i for i in range(nv)
            if abs(rest_w[i, 0]) < 0.095 and rest_w[i, 2] < hips_z + 0.06 and rest_w[i, 2] > 0.12
        ]
        for _ in range(4):
            nxt = {}
            for i in skirt_mid:
                if not nbr[i]:
                    continue
                acc = {}
                for j in nbr[i]:
                    for g, val in W[j].items():
                        if g not in all_arm_gids:
                            acc[g] = acc.get(g, 0.0) + val / len(nbr[i])
                if acc:
                    nxt[i] = {g: 0.55 * W[i].get(g, 0.0) + 0.45 * acc.get(g, 0.0) for g in set(acc) | set(W[i])}
            for i, nw in nxt.items():
                W[i] = nw

    # ---- E) Targeted LBS Idle-Stretch Cliff Smoothing (all 12 kids) -------------------------------------------------
    # Normalize first before LBS check
    def _renorm_inplace():
        for i in range(nv):
            tot = sum(W[i].values())
            if tot > 1e-6:
                inv = 1.0 / tot
                W[i] = {g: val * inv for g, val in W[i].items() if val * inv > 0.004}
            else:
                fb = get_gid(P + "Hips")
                W[i] = {fb: 1.0} if fb is not None else {}

    _renorm_inplace()
    for it in range(5):
        posed = eval_lbs_world(rest_w, W, vg_names, skin_mats_idle)
        li = np.linalg.norm(posed[edges_np[:, 0]] - posed[edges_np[:, 1]], axis=1)
        ratio = li / np.maximum(lr, 1e-5)
        bad_e = np.where(ratio > 1.88)[0]
        if len(bad_e) == 0:
            break
        hot = set()
        for ei in bad_e:
            a, b = int(edges_np[ei, 0]), int(edges_np[ei, 1])
            hot.add(a)
            hot.add(b)
        # Expand 1 ring gently around cliff edges for smooth falloff
        hot_ext = set(hot)
        for vi in hot:
            for j in nbr[vi]:
                hot_ext.add(j)
        nxt = {}
        for i in hot_ext:
            if not nbr[i]:
                continue
            # Never bleed cape into arm or arm into cape during cliff smoothing
            i_is_cape = bool(cape_gids) and sum(W[i].get(g, 0.0) for g in cape_gids) > 0.55
            i_is_arm = sum(W[i].get(g, 0.0) for g in all_arm_gids) > 0.55
            acc = {}
            cnt = 0
            for j in nbr[i]:
                if i_is_cape and sum(W[j].get(g, 0.0) for g in all_arm_gids) > 0.55:
                    continue
                if i_is_arm and bool(cape_gids) and sum(W[j].get(g, 0.0) for g in cape_gids) > 0.55:
                    continue
                cnt += 1
                for g, val in W[j].items():
                    acc[g] = acc.get(g, 0.0) + val
            if cnt > 0:
                alpha = 0.52 if i in hot else 0.28
                nxt[i] = {
                    g: (1.0 - alpha) * W[i].get(g, 0.0) + alpha * (acc.get(g, 0.0) / cnt)
                    for g in set(acc) | set(W[i])
                }
        for i, nw in nxt.items():
            W[i] = nw
        _renorm_inplace()

    # ---- F) Final R3 Integrity: localized core preservation, zero cross-side, seam-twin parity, top-4 normalize -----
    left_g = set()
    right_g = set()
    for idx, nm in vg_names.items():
        nl = nm.lower()
        if "left" in nl or nl.endswith(".l") or nl.endswith("_l") or "_l_" in nl or "_l2_" in nl:
            left_g.add(idx)
        elif "right" in nl or nl.endswith(".r") or nl.endswith("_r") or "_r_" in nl or "_r2_" in nl:
            right_g.add(idx)
        elif nm in bhead and abs(float(bhead[nm][0])) > 0.03:
            if float(bhead[nm][0]) > 0.03:
                left_g.add(idx)
            else:
                right_g.add(idx)
    hips_g = get_gid(P + "Hips")

    for i in range(nv):
        vx = float(rest_w[i, 0])
        if abs(vx) <= 0.028:
            continue
        bad_g = right_g if vx > 0.028 else left_g
        for g in list(W[i]):
            if g in bad_g:
                del W[i][g]
        if not W[i] and hips_g is not None:
            W[i][hips_g] = 1.0

    # Enforce exact seam-twin weight parity
    for ids in pos_groups.values():
        if len(ids) > 1:
            avg = {}
            inv_n = 1.0 / len(ids)
            for i in ids:
                for g, val in W[i].items():
                    avg[g] = avg.get(g, 0.0) + val * inv_n
            for i in ids:
                W[i] = dict(avg)

    # Prune < 0.01, cap top 4, normalize & write back to Blender vertex groups
    for i, v in enumerate(me.vertices):
        w = {g: val for g, val in W[i].items() if val >= 0.01}
        if not w and W[i]:
            g_max = max(W[i], key=W[i].get)
            w = {g_max: W[i][g_max]}
        top = dict(sorted(w.items(), key=lambda q: -q[1])[:4])
        tot = sum(top.values()) or 1.0
        W[i] = {g: val / tot for g, val in top.items()}
        for g in list(v.groups):
            vgs[g.group].remove([i])
        for g, val in W[i].items():
            vgs[g].add([i], val, "REPLACE")

    posed_after = eval_lbs_world(rest_w, W, vg_names, skin_mats_idle)
    li_after = np.linalg.norm(posed_after[edges_np[:, 0]] - posed_after[edges_np[:, 1]], axis=1)
    ratio_after = li_after / np.maximum(lr, 1e-5)
    st_after = int((ratio_after > 2.5).sum())
    max_st_after = float(ratio_after.max())

    stats = {
        "cape_arm_fixed": cape_arm_fixed,
        "hair_fixed": hair_fixed,
        "leak_fixed": leak_fixed,
        "idle_stretch_gt25_before": st_before,
        "idle_stretch_gt25_after": st_after,
        "idle_max_stretch_before": round(max_st_before, 2),
        "idle_max_stretch_after": round(max_st_after, 2),
    }
    log(
        f"{kid} weights: cape={cape_arm_fixed}, hair={hair_fixed}, leaks={leak_fixed} | "
        f"Idle stretch(>2.5x) {st_before}->{st_after} (max {max_st_before:.2f}x->{max_st_after:.2f}x)"
    )
    return W, stats


def push_cape_behind_arms(
    kid: str, arm, mesh_obj, rest_w: np.ndarray, W: list[dict[int, float]],
    pos_groups, nbr, posed_segs_idle: dict[str, tuple[np.ndarray, np.ndarray]]
) -> tuple[np.ndarray, dict]:
    """Smoothly push Karding's cape vertices cleanly behind BOTH Rest & Idle arm capsules (zero arm/cape clipping)."""
    vgs = mesh_obj.vertex_groups
    vg_names = {g.index: g.name for g in vgs}
    cape_gids = {idx for idx, nm in vg_names.items() if nm.lower().startswith("cape")}
    if not cape_gids:
        return rest_w, {"cape_pushed_verts": 0, "cape_idle_close_before": 0, "cape_idle_close_after": 0}

    AM = arm.matrix_world
    B = arm.data.bones
    nv = len(rest_w)
    spine2_z_top = float((AM @ B[P + "Spine2"].tail_local).z)

    # Identify cape vertices (strictly dominant Cape_C_* and below neck knot)
    cape_mask = np.zeros(nv, dtype=bool)
    arm_gids = {idx for idx, nm in vg_names.items() if nm.startswith(P) and any(k in nm for k in ("Arm", "Hand"))}
    arm_mask = np.zeros(nv, dtype=bool)
    for i in range(nv):
        cw = sum(W[i].get(g, 0.0) for g in cape_gids)
        aw = sum(W[i].get(g, 0.0) for g in arm_gids)
        if cw > 0.50 and rest_w[i, 2] < spine2_z_top - 0.015:
            cape_mask[i] = True
        if aw > 0.55:
            arm_mask[i] = True

    # Build Rest + Idle arm capsule obstacle list: (a, b, clearance_radius)
    obstacles = []
    for s in ("Left", "Right"):
        # Rest segments
        r_a0 = np.array(AM @ B[P + s + "Arm"].head_local, dtype=np.float64)
        r_a1 = np.array(AM @ B[P + s + "ForeArm"].head_local, dtype=np.float64)
        r_a2 = np.array(AM @ B[P + s + "Hand"].head_local, dtype=np.float64)
        obstacles.append((r_a0, r_a1, 0.098))
        obstacles.append((r_a1, r_a2, 0.088))
        # Idle segments
        i_a0 = posed_segs_idle[P + s + "Arm"][0]
        i_a1 = posed_segs_idle[P + s + "ForeArm"][0]
        i_a2 = posed_segs_idle[P + s + "Hand"][0]
        obstacles.append((i_a0, i_a1, 0.104))
        obstacles.append((i_a1, i_a2, 0.092))

    def count_close_to_idle_arms(pts: np.ndarray) -> int:
        c_idx = np.where(cape_mask)[0]
        if len(c_idx) == 0:
            return 0
        c_pts = pts[c_idx]
        min_d = np.full(len(c_idx), 1e9, dtype=np.float64)
        for s in ("Left", "Right"):
            for seg_a, seg_b, rad in (
                (posed_segs_idle[P + s + "Arm"][0], posed_segs_idle[P + s + "ForeArm"][0], 0.068),
                (posed_segs_idle[P + s + "ForeArm"][0], posed_segs_idle[P + s + "Hand"][0], 0.058),
            ):
                t, d, _ = pt_seg_dist(c_pts, seg_a, seg_b)
                in_seg = (t > -0.05) & (t < 1.05)
                min_d = np.where(in_seg, np.minimum(min_d, d - rad), min_d)
        return int((min_d < 0.015).sum())

    close_before = count_close_to_idle_arms(rest_w)
    out = rest_w.copy()
    pushed_idx = set()

    # Smoothly push encroaching cape vertices backward (+Y) and gently away from arm centerline
    c_indices = np.where(cape_mask)[0]
    for _ in range(3):
        c_pts = out[c_indices]
        disp = np.zeros_like(c_pts)
        for seg_a, seg_b, clear_r in obstacles:
            t, d, proj = pt_seg_dist(c_pts, seg_a, seg_b)
            # Only affect cape vertices within segment span + small taper and not far behind
            in_span = (t > -0.15) & (t < 1.15)
            # Soft falloff zone up to clear_r + 0.045m
            outer_r = clear_r + 0.045
            encroach = in_span & (d < outer_r)
            if not np.any(encroach):
                continue
            # Cosine smooth falloff from 1 at d<=clear_r to 0 at d>=outer_r
            pen = np.maximum(0.0, clear_r - d)
            soft = 0.5 * (1.0 + np.cos(math.pi * np.clip((d - clear_r) / 0.045, 0.0, 1.0)))
            push_mag = np.where(d < clear_r, pen + 0.014, 0.014 * soft)
            # Push strongly backward (+Y) and mildly radially away from arm X
            away = c_pts - proj
            away_x = np.sign(np.where(np.abs(away[:, 0]) > 1e-4, away[:, 0], c_pts[:, 0]))
            dir_vec = np.stack([0.22 * away_x, np.ones(len(c_pts)), np.zeros(len(c_pts))], axis=1)
            dir_vec /= np.maximum(np.linalg.norm(dir_vec, axis=1, keepdims=True), 1e-6)
            # Taper smoothly near collar (z > 1.21) so upper-arm cape edge clears right deltoid completely
            collar_taper = np.clip((spine2_z_top + 0.015 - c_pts[:, 2]) / 0.085, 0.0, 1.0)
            step = dir_vec * (push_mag * collar_taper)[:, None]
            disp = np.where(encroach[:, None], np.maximum(disp, step), disp)

        moved = np.linalg.norm(disp, axis=1) > 1e-4
        if np.any(moved):
            out[c_indices[moved]] += disp[moved] * 0.65
            for vi in c_indices[moved]:
                pushed_idx.add(int(vi))

    # Relax displaced cape patch (+ 2-ring cape neighbors) smoothly so folds drape naturally
    if pushed_idx:
        relax_set = set(pushed_idx)
        for _ in range(2):
            ext = set(relax_set)
            for vi in relax_set:
                for j in nbr[vi]:
                    if cape_mask[j]:
                        ext.add(j)
            relax_set = ext
        for _ in range(4):
            nxt = {}
            for vi in relax_set:
                c_nbrs = [j for j in nbr[vi] if cape_mask[j]]
                if not c_nbrs:
                    continue
                avg = out[c_nbrs].mean(axis=0)
                # Keep backward clearance (+Y >= current Y - 2mm) while relaxing X/Z smoothly
                blended = 0.62 * out[vi] + 0.38 * avg
                blended[1] = max(blended[1], out[vi, 1] - 0.002)
                nxt[vi] = blended
            for vi, p in nxt.items():
                out[vi] = p

    # Seam-twin position parity
    for ids in pos_groups.values():
        if len(ids) > 1:
            avg = out[ids].mean(axis=0)
            out[ids] = avg

    close_after = count_close_to_idle_arms(out)
    log(f"{kid} cape clearance: pushed {len(pushed_idx)} verts | Idle arm-encroach verts {close_before}->{close_after}")
    return out, {
        "cape_pushed_verts": len(pushed_idx),
        "cape_idle_close_before": close_before,
        "cape_idle_close_after": close_after,
    }


def chiseled_bilateral_plane_polish(
    kid: str, arm, mesh_obj, rest_w: np.ndarray, pos_groups, v2w, w_members, w_nbr_list
) -> np.ndarray:
    """Volume-preserving anisotropic normal-guided bilateral mesh filter (Valorant x Arcane chiseled planes).

    Flattens lumpy organic Meshy clay noise into clean chiseled planes while preserving creases sharper
    than ~26 deg (cloth folds, hair clumps, collar rims, shoe soles, jawlines). Protects eyes/lips/hands
    and pins soles at z >= 0.0.
    """
    me = mesh_obj.data
    AM = arm.matrix_world
    B = arm.data.bones
    nv = len(rest_w)
    nw = len(w_members)

    # Welded rest coordinates
    w_orig = np.array([rest_w[ids[0]] for ids in w_members], dtype=np.float64)
    w_co = w_orig.copy()
    zmin = float(w_orig[:, 2].min())

    # Triangulate polygons into welded vertex index triples for fast vectorized face normals
    tris = []
    for p in me.polygons:
        vs = [int(v2w[vi]) for vi in p.vertices]
        for k in range(1, len(vs) - 1):
            a, b, c = vs[0], vs[k], vs[k + 1]
            if a != b and b != c and a != c:
                tris.append((a, b, c))
    tris_np = np.array(tris, dtype=np.int32)
    nt = len(tris_np)

    # Build vertex -> incident triangles list and triangle -> neighboring triangles (sharing a vertex)
    v_tris: list[list[int]] = [[] for _ in range(nw)]
    for ti, (a, b, c) in enumerate(tris_np):
        v_tris[a].append(ti)
        v_tris[b].append(ti)
        v_tris[c].append(ti)

    # Build edge-adjacent triangle pairs (clean 1-ring face neighborhood)
    edge_tris: dict[tuple[int, int], list[int]] = {}
    for ti, (a, b, c) in enumerate(tris_np):
        for u, v in ((a, b), (b, c), (c, a)):
            ek = (u, v) if u < v else (v, u)
            edge_tris.setdefault(ek, []).append(ti)
    f_pairs_a = []
    f_pairs_b = []
    for tlist in edge_tris.values():
        if len(tlist) == 2:
            f_pairs_a.extend((tlist[0], tlist[1]))
            f_pairs_b.extend((tlist[1], tlist[0]))
    fp_a = np.array(f_pairs_a, dtype=np.int32)
    fp_b = np.array(f_pairs_b, dtype=np.int32)

    # Per-welded-vertex protection weight (gentler on hands/digits and front eye/lip landmarks)
    strength = np.ones(nw, dtype=np.float64)
    # Protect soles (z < zmin + 0.010)
    strength[w_orig[:, 2] < zmin + 0.010] = 0.0
    # Protect hands/digits
    for s in ("Left", "Right"):
        hp = np.array(AM @ B[P + s + "Hand"].head_local, dtype=np.float64)
        d_hand = np.linalg.norm(w_orig - hp, axis=1)
        strength = np.minimum(strength, np.where(d_hand < 0.14, 0.20, 1.0))
    # Protect delicate front face features (eyes, nose tip, lips, glasses)
    head_p = np.array(AM @ B[P + "Head"].head_local, dtype=np.float64)
    in_face_detail = (
        (w_orig[:, 1] < head_p[1] - 0.065)
        & (np.abs(w_orig[:, 0] - head_p[0]) < 0.085)
        & (w_orig[:, 2] > head_p[2] + 0.025)
        & (w_orig[:, 2] < head_p[2] + 0.175)
    )
    strength[in_face_detail] = np.minimum(strength[in_face_detail], 0.28)

    # Gentle normal-direction thickening on Narda's fragile thin glasses ridge before bilateral filtering
    if kid == "narda":
        # Compute initial vertex normals
        e1 = w_co[tris_np[:, 1]] - w_co[tris_np[:, 0]]
        e2 = w_co[tris_np[:, 2]] - w_co[tris_np[:, 0]]
        cr = np.cross(e1, e2)
        vn = np.zeros((nw, 3), dtype=np.float64)
        np.add.at(vn, tris_np[:, 0], cr)
        np.add.at(vn, tris_np[:, 1], cr)
        np.add.at(vn, tris_np[:, 2], cr)
        vn /= np.maximum(np.linalg.norm(vn, axis=1, keepdims=True), 1e-8)
        glasses_zone = (
            (w_orig[:, 1] < head_p[1] - 0.105)
            & (np.abs(w_orig[:, 0] - head_p[0]) < 0.075)
            & (w_orig[:, 2] > head_p[2] + 0.065)
            & (w_orig[:, 2] < head_p[2] + 0.145)
        )
        # Slightly puff protruding glasses rim vertices outward (+0.9mm) along normal to eliminate razor-thin pinch
        if np.any(glasses_zone):
            w_co[glasses_zone] += vn[glasses_zone] * 0.0009
            strength[glasses_zone] = 0.15

    sigma_n = 0.145  # preserves creases sharper than ~26 deg!
    inv_2sig2 = 1.0 / (2.0 * sigma_n * sigma_n)

    for _ in range(4):
        p0 = w_co[tris_np[:, 0]]
        p1 = w_co[tris_np[:, 1]]
        p2 = w_co[tris_np[:, 2]]
        cr = np.cross(p1 - p0, p2 - p0)
        area = 0.5 * np.linalg.norm(cr, axis=1)
        fn = cr / np.maximum(2.0 * area[:, None], 1e-12)
        fc = (p0 + p1 + p2) / 3.0

        # Bilateral face-normal filtering across edge-adjacent faces
        dots = np.sum(fn[fp_a] * fn[fp_b], axis=1)
        diff = np.maximum(0.0, 1.0 - dots)
        w_ab = area[fp_b] * np.exp(-(diff * diff) * inv_2sig2)

        fn_acc = fn * (area[:, None] * 1.15)
        np.add.at(fn_acc, fp_a, fn[fp_b] * w_ab[:, None])
        fn_filt = fn_acc / np.maximum(np.linalg.norm(fn_acc, axis=1, keepdims=True), 1e-12)

        # Project vertices cleanly toward incident faces' filtered tangent planes (Sun et al. / Zheng)
        v_disp = np.zeros((nw, 3), dtype=np.float64)
        v_wt = np.zeros(nw, dtype=np.float64)
        for corner in range(3):
            vi = tris_np[:, corner]
            delta_dot = np.sum((fc - w_co[vi]) * fn_filt, axis=1)
            contrib = fn_filt * (delta_dot * area)[:, None]
            np.add.at(v_disp, vi, contrib)
            np.add.at(v_wt, vi, area)

        step = (v_disp / np.maximum(v_wt[:, None], 1e-12)) * (0.42 * strength[:, None])
        w_co = w_co + step

        # Clamp max deviation from original shape <= 2.2 mm (preserves exact likeness & silhouette!)
        tot_d = w_co - w_orig
        tot_len = np.linalg.norm(tot_d, axis=1, keepdims=True)
        w_co = w_orig + np.where(tot_len > 0.0022, tot_d * (0.0022 / np.maximum(tot_len, 1e-12)), tot_d)
        w_co[:, 2] = np.maximum(w_co[:, 2], zmin)

    # Scatter welded coordinates back to all original vertices (exact seam-twin parity)
    out = np.empty_like(rest_w)
    for wid, ids in enumerate(w_members):
        out[ids] = w_co[wid]
    return out


def compute_welded_curvature(me, world_co: np.ndarray, v2w, w_members, w_nbr_list) -> np.ndarray:
    """Compute robust normalized discrete mean curvature per vertex in [0..1] (0.5=flat, >0.5=convex, <0.5=concave)."""
    nw = len(w_members)
    w_co = np.array([world_co[ids[0]] for ids in w_members], dtype=np.float64)

    # Smooth vertex normals on welded mesh
    vn = np.zeros((nw, 3), dtype=np.float64)
    for p in me.polygons:
        vs = [int(v2w[vi]) for vi in p.vertices]
        for k in range(1, len(vs) - 1):
            a, b, c = vs[0], vs[k], vs[k + 1]
            cr = np.cross(w_co[b] - w_co[a], w_co[c] - w_co[a])
            vn[a] += cr
            vn[b] += cr
            vn[c] += cr
    vn /= np.maximum(np.linalg.norm(vn, axis=1, keepdims=True), 1e-8)

    # Discrete mean curvature: positive where vertex protrudes outward vs neighbor mean (convex ridge!)
    curv_w = np.zeros(nw, dtype=np.float64)
    for wi in range(nw):
        ns = w_nbr_list[wi]
        if not ns:
            continue
        lap = w_co[wi] - w_co[ns].mean(axis=0)
        curv_w[wi] = float(np.dot(lap, vn[wi]))

    # One light neighborhood smooth of curvature so ridges read painterly, not faceted
    curv_s = curv_w.copy()
    for wi in range(nw):
        ns = w_nbr_list[wi]
        if ns:
            curv_s[wi] = 0.65 * curv_w[wi] + 0.35 * float(curv_w[ns].mean())

    scale = float(np.percentile(np.abs(curv_s), 94))
    scale = max(scale, 1e-4)
    norm_w = np.clip(0.5 + 0.5 * (curv_s / (scale * 1.45)), 0.0, 1.0)

    out = np.empty(len(world_co), dtype=np.float32)
    for wi, ids in enumerate(w_members):
        out[ids] = norm_w[wi]
    return out


def bake_auxiliary_maps(mesh_obj, H: float, curv_v: np.ndarray, vgrad_v: np.ndarray) -> tuple[np.ndarray, np.ndarray, np.ndarray, np.ndarray]:
    """Bake 1024 AO, 1024 Curvature, 1024 Vertical Gradient, and 2048 UV Island Mask in Blender 5.2 Cycles."""
    me = mesh_obj.data
    scn = bpy.context.scene
    scn.render.engine = "CYCLES"
    scn.cycles.device = "CPU"
    bake = scn.render.bake
    bake.use_selected_to_active = False
    bake.margin = 12
    bake.margin_type = "EXTEND"

    # Store curvature & vertical gradient in temporary vertex color attributes
    for attr_name, vals in (("__curv__", curv_v), ("__vgrad__", vgrad_v)):
        if attr_name in me.color_attributes:
            me.color_attributes.remove(me.color_attributes[attr_name])
        ca = me.color_attributes.new(name=attr_name, type="BYTE_COLOR", domain="POINT")
        rgba = np.ones((len(vals), 4), dtype=np.float32)
        rgba[:, 0] = vals
        rgba[:, 1] = vals
        rgba[:, 2] = vals
        ca.data.foreach_set("color", rgba.ravel())

    saved_mats = list(me.materials)
    tmp_mat = bpy.data.materials.new("__PolishBakeMat__")
    if tmp_mat.node_tree is None:
        tmp_mat.use_nodes = True
    nt = tmp_mat.node_tree
    nt.nodes.clear()
    out_n = nt.nodes.new("ShaderNodeOutputMaterial")
    emit_n = nt.nodes.new("ShaderNodeEmission")
    attr_n = nt.nodes.new("ShaderNodeAttribute")
    tex_n = nt.nodes.new("ShaderNodeTexImage")
    nt.links.new(emit_n.outputs["Emission"], out_n.inputs["Surface"])

    me.materials.clear()
    me.materials.append(tmp_mat)
    activate(mesh_obj, "OBJECT")

    def _bake_emit(attr_name: str | None, px_size: int, margin: int) -> np.ndarray:
        img = bpy.data.images.new("__bk__", px_size, px_size, alpha=False)
        img.colorspace_settings.name = "Non-Color"
        img.pixels.foreach_set(np.zeros(px_size * px_size * 4, dtype=np.float32))
        tex_n.image = img
        nt.nodes.active = tex_n
        for lnk in list(emit_n.inputs["Color"].links):
            nt.links.remove(lnk)
        if attr_name is not None:
            attr_n.attribute_name = attr_name
            nt.links.new(attr_n.outputs["Color"], emit_n.inputs["Color"])
        else:
            emit_n.inputs["Color"].default_value = (1.0, 1.0, 1.0, 1.0)
        scn.cycles.samples = 1
        bake.margin = margin
        bpy.ops.object.bake(type="EMIT")
        arr = np.array(img.pixels[:], dtype=np.float32).reshape(px_size, px_size, 4)[:, :, 0].copy()
        bpy.data.images.remove(img)
        return arr

    # 1) Curvature (1024)
    curv_1k = _bake_emit("__curv__", MAP_PX, margin=12)
    # 2) Vertical Gradient (1024)
    vgrad_1k = _bake_emit("__vgrad__", MAP_PX, margin=12)
    # 3) Exact UV Island Occupancy Mask (2048, margin=0)
    mask_2k = _bake_emit(None, BASE_PX, margin=0)

    # 4) Ambient Occlusion (1024, 16 samples, smooth Cycles AO from polished geometry)
    if scn.world is None:
        scn.world = bpy.data.worlds.new("__BakeWorld__")
    if hasattr(scn.world, "light_settings"):
        scn.world.light_settings.distance = H * 0.14
    ao_img = bpy.data.images.new("__bk_ao__", MAP_PX, MAP_PX, alpha=False)
    ao_img.colorspace_settings.name = "Non-Color"
    tex_n.image = ao_img
    nt.nodes.active = tex_n
    scn.cycles.samples = 16
    bake.margin = 12
    bpy.ops.object.bake(type="AO")
    ao_1k = np.array(ao_img.pixels[:], dtype=np.float32).reshape(MAP_PX, MAP_PX, 4)[:, :, 0].copy()
    bpy.data.images.remove(ao_img)

    # Restore materials & delete temp color attributes
    me.materials.clear()
    for m in saved_mats:
        me.materials.append(m)
    bpy.data.materials.remove(tmp_mat)
    for attr_name in ("__curv__", "__vgrad__"):
        if attr_name in me.color_attributes:
            me.color_attributes.remove(me.color_attributes[attr_name])

    return ao_1k, curv_1k, vgrad_1k, mask_2k


# ---- Vectorized NumPy Image Processing (zero external deps inside Blender!) -----------------------------------------

def box_blur_2d(arr: np.ndarray, radius: int) -> np.ndarray:
    """Fast separable box blur on (H, W) or (H, W, C) float32 array via cumulative sums."""
    if radius <= 0:
        return arr.copy()
    k = 2 * radius + 1
    # Horizontal
    pad_x = np.pad(arr, ((0, 0), (radius, radius)) + ((0, 0),) * (arr.ndim - 2), mode="edge")
    csx = np.cumsum(pad_x, axis=1, dtype=np.float64)
    csx = np.concatenate([np.zeros_like(csx[:, :1]), csx], axis=1)
    tmp = (csx[:, k:] - csx[:, :-k]) / float(k)
    # Vertical
    pad_y = np.pad(tmp, ((radius, radius), (0, 0)) + ((0, 0),) * (arr.ndim - 2), mode="edge")
    csy = np.cumsum(pad_y, axis=0, dtype=np.float64)
    csy = np.concatenate([np.zeros_like(csy[:1, :]), csy], axis=0)
    out = (csy[k:, :] - csy[:-k, :]) / float(k)
    return out.astype(np.float32)


def gaussian_smooth_2d(arr: np.ndarray, radius: int = 2) -> np.ndarray:
    """Two-pass separable box blur approximating a crisp Gaussian kernel."""
    return box_blur_2d(box_blur_2d(arr, radius), max(1, radius // 2))


def dilate_mask_2d(mask: np.ndarray, radius: int) -> np.ndarray:
    return box_blur_2d(mask.astype(np.float32), radius) > 1e-6


def bilateral_filter_rgb(
    img: np.ndarray, radius: int, sigma_s: float, sigma_r_map: np.ndarray
) -> np.ndarray:
    """Vectorized edge-preserving bilateral filter supporting per-pixel adaptive sigma_r."""
    H, W, C = img.shape
    pad = np.pad(img, ((radius, radius), (radius, radius), (0, 0)), mode="edge")
    acc = np.zeros_like(img, dtype=np.float64)
    wsum = np.zeros((H, W, 1), dtype=np.float64)
    inv_2ss = 1.0 / (2.0 * sigma_s * sigma_s)
    inv_2sr = 1.0 / (2.0 * np.maximum(sigma_r_map, 0.015) ** 2)
    for dy in range(-radius, radius + 1):
        for dx in range(-radius, radius + 1):
            ws = math.exp(-(dx * dx + dy * dy) * inv_2ss)
            nb = pad[radius + dy:radius + dy + H, radius + dx:radius + dx + W]
            diff = nb - img
            dist2 = diff[:, :, 0] ** 2 + diff[:, :, 1] ** 2 + diff[:, :, 2] ** 2
            wr = np.exp(-dist2 * inv_2sr)
            w = (ws * wr)[:, :, None]
            acc += nb * w
            wsum += w
    return (acc / np.maximum(wsum, 1e-8)).astype(np.float32)


def inpaint_masked_rgb(rgb: np.ndarray, valid_mask: np.ndarray, iters: int = 16) -> np.ndarray:
    """Iteratively fill invalid pixels from valid neighbors (for pinhole repair & UV gutter dilation)."""
    out = rgb.copy()
    known = valid_mask.copy()
    offsets = ((-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (-1, 1), (1, -1), (1, 1))
    weights = (1.0, 1.0, 1.0, 1.0, 0.7, 0.7, 0.7, 0.7)
    H, W, _ = out.shape
    for _ in range(iters):
        missing = ~known
        if not np.any(missing):
            break
        pad_c = np.pad(out, ((1, 1), (1, 1), (0, 0)), mode="edge")
        pad_k = np.pad(known.astype(np.float32), ((1, 1), (1, 1)), mode="constant", constant_values=0.0)
        acc = np.zeros_like(out, dtype=np.float32)
        wsum = np.zeros((H, W), dtype=np.float32)
        for (dy, dx), wt in zip(offsets, weights):
            nk = pad_k[1 + dy:1 + dy + H, 1 + dx:1 + dx + W] * wt
            nc = pad_c[1 + dy:1 + dy + H, 1 + dx:1 + dx + W]
            acc += nc * nk[:, :, None]
            wsum += nk
        fill = missing & (wsum > 1e-5)
        if not np.any(fill):
            break
        out[fill] = acc[fill] / wsum[fill, None]
        known[fill] = True
    return out


def process_painterly_pbr_maps(
    kid: str,
    base_rgb_2k: np.ndarray,
    nrm_rgb_1k: np.ndarray,
    ao_1k: np.ndarray,
    curv_1k: np.ndarray,
    vgrad_1k: np.ndarray,
    mask_2k: np.ndarray,
) -> tuple[np.ndarray, np.ndarray, np.ndarray, dict]:
    """Phase 3 Valorant x Arcane painterly Base Color (2048), clean Normal (1024), semantic ORM (1024)."""
    # Upsample 1024 auxiliary maps smoothly to 2048 for Base Color compositing
    ao_s1 = gaussian_smooth_2d(np.clip(ao_1k, 0.0, 1.0), radius=2)
    curv_s1 = gaussian_smooth_2d(np.clip(curv_1k, 0.0, 1.0), radius=1)
    vgrad_s1 = np.clip(vgrad_1k, 0.0, 1.0)

    ao_2k = np.repeat(np.repeat(ao_s1, 2, axis=0), 2, axis=1)
    curv_2k = np.repeat(np.repeat(curv_s1, 2, axis=0), 2, axis=1)
    vgrad_2k = np.repeat(np.repeat(vgrad_s1, 2, axis=0), 2, axis=1)
    in_island_2k = mask_2k > 0.5
    in_island_1k = (mask_2k[0::2, 0::2] + mask_2k[1::2, 1::2]) > 0.5

    # ---- 1) Base Color: Repair Cycles ray-miss black pinholes & dark hair-edge bleed --------------------------------
    rgb = np.clip(base_rgb_2k, 0.0, 1.0).copy()
    lum0 = 0.299 * rgb[:, :, 0] + 0.587 * rgb[:, :, 1] + 0.114 * rgb[:, :, 2]

    # Pure pitch-black ray-miss holes inside UV islands (real irises/hair are >= 0.035 lum)
    pitch_black = in_island_2k & (lum0 < 0.030)
    # Dark halo pixels bordering pitch-black bake holes (e.g. Tere's white tie-dye collarbone spots & Jumong's armpit)
    local_mean_lum = box_blur_2d(lum0, radius=11)
    is_eye_band = (vgrad_2k > 0.80) & (vgrad_2k < 0.94)
    halo = (
        dilate_mask_2d(pitch_black, radius=8)
        & in_island_2k
        & (~is_eye_band)
        & (lum0 < 0.28)
        & (local_mean_lum > 0.24)
    )
    # Also catch isolated dark hair-bleed flecks sitting inside bright clothing/skin (strictly outside eye/hair zones)
    isolated_speck = (
        in_island_2k
        & (~is_eye_band)
        & (vgrad_2k < 0.80)
        & (lum0 < 0.12)
        & (local_mean_lum > 0.36)
    )
    corrupted = pitch_black | halo | isolated_speck
    repaired_px = int(corrupted.sum())

    valid_base = in_island_2k & (~corrupted)
    if repaired_px > 0:
        rgb = inpaint_masked_rgb(rgb, valid_base, iters=24)

    # First gutter-dilate cleanly before bilateral so UV edges never pull black gutter into borders!
    rgb = inpaint_masked_rgb(rgb, in_island_2k, iters=10)

    # ---- 2) Edge-Preserving Painterly Brushwork Pass (eliminates Meshy AI speckle!) ---------------------------------
    # Tighter range sigma on face/head (vgrad > 0.76) so eyes, pupils, lashes, lips stay razor-sharp;
    # broader painterly brushwork on clothes/limbs/hair clumps!
    sr_map = np.where(vgrad_2k > 0.76, 0.052, 0.088).astype(np.float32)
    rgb_paint = bilateral_filter_rgb(rgb, radius=2, sigma_s=1.8, sigma_r_map=sr_map)
    rgb_paint = bilateral_filter_rgb(rgb_paint, radius=2, sigma_s=1.5, sigma_r_map=sr_map * 0.85)
    # Blend 85% painterly + 15% original micro-crispness on face/decals so painted logos stay crisp
    face_weight = np.where(vgrad_2k > 0.77, 0.72, 0.88).astype(np.float32)[:, :, None]
    rgb = rgb_paint * face_weight + rgb * (1.0 - face_weight)

    # ---- 3) Riot-Style Top-Down Focal Luminance Gradient (feet -13% -> head/upper torso +11%) -----------------------
    # Smooth S-curve anchored at waist (vgrad ~ 0.48 -> 1.00)
    top_t = np.clip((vgrad_2k - 0.48) / 0.42, 0.0, 1.0)
    top_t = top_t * top_t * (3.0 - 2.0 * top_t)
    bot_t = np.clip((0.46 - vgrad_2k) / 0.42, 0.0, 1.0)
    bot_t = bot_t * bot_t * (3.0 - 2.0 * bot_t)

    # Slight warm lift at head/upper torso (+13%), grounded slightly cool shadow weight at feet/legs (-15%)
    gain_r = 1.0 + 0.130 * top_t - 0.160 * bot_t
    gain_g = 1.0 + 0.125 * top_t - 0.152 * bot_t
    gain_b = 1.0 + 0.108 * top_t - 0.130 * bot_t
    rgb = rgb * np.stack([gain_r, gain_g, gain_b], axis=-1)

    # Soft-knee highlight roll-off above 0.85 so white shirts keep sculpted fold readability instead of clipping!
    hi_ex = np.maximum(rgb - 0.85, 0.0)
    rgb = np.where(rgb > 0.85, 0.85 + hi_ex / (1.0 + 2.6 * hi_ex), rgb)

    # ---- 4) Baked Curvature Edge Highlights + Warm AO Cavity Shading ------------------------------------------------
    # Convex chiseled ridges: crisp painterly warm-ivory highlight (stronger on cloth/hair clumps, softened on face)
    ridge = np.clip((curv_2k - 0.53) / 0.32, 0.0, 1.0)
    ridge_amp = np.where(vgrad_2k > 0.78, 0.036, 0.072) * (0.45 + 0.55 * vgrad_2k)
    lum_cur = 0.299 * rgb[:, :, 0] + 0.587 * rgb[:, :, 1] + 0.114 * rgb[:, :, 2]
    # Reduce highlight lift on already near-white shirts so creases stay crisp
    white_guard = np.clip((0.94 - lum_cur) / 0.22, 0.18, 1.0)
    edge_lift = (ridge * ridge_amp * white_guard)[:, :, None]
    rgb = rgb + edge_lift * np.array([1.00, 0.96, 0.88], dtype=np.float32)

    # Concave AO cavities: rich warm saturated shadow shading (Riot/Arcane craft — NEVER muddy grey-black!)
    cavity = np.clip((0.88 - ao_2k) / 0.55, 0.0, 1.0)
    # Soften cavity darkening slightly on face skin so face stays luminous & hero-readable
    cav_str = np.where(vgrad_2k > 0.78, 0.65, 1.0) * cavity
    warm_ao = np.stack([
        1.0 - 0.13 * cav_str,
        1.0 - 0.19 * cav_str,
        1.0 - 0.24 * cav_str,
    ], axis=-1)
    rgb = rgb * warm_ao

    # Gentle midtone saturation boost (+9%) for crisp silhouette figure-ground contrast against Barangay stucco
    lum_f = (0.299 * rgb[:, :, 0] + 0.587 * rgb[:, :, 1] + 0.114 * rgb[:, :, 2])[:, :, None]
    mid_mask = np.clip(1.0 - np.abs(lum_f - 0.45) * 1.9, 0.18, 1.0)
    rgb = lum_f + (rgb - lum_f) * (1.0 + 0.090 * mid_mask)
    rgb = np.clip(rgb, 0.0, 1.0).astype(np.float32)

    # Final UV gutter dilation (12px) for mipmap-clean 30m/50m reads
    rgb = inpaint_masked_rgb(rgb, in_island_2k, iters=12)

    # ---- 5) Clean Low-Noise Tangent Normal (1024x1024) --------------------------------------------------------------
    nrm = np.clip(nrm_rgb_1k, 0.0, 1.0).copy()
    nrm = inpaint_masked_rgb(nrm, in_island_1k, iters=10)
    # Edge-preserving bilateral denoise on tangent normal to remove lumpy Meshy micro-grain
    sr_nrm = np.full((MAP_PX, MAP_PX), 0.13, dtype=np.float32)
    nrm_clean = bilateral_filter_rgb(nrm, radius=2, sigma_s=1.6, sigma_r_map=sr_nrm)
    # Map to [-1..1], slightly calm noisy micro-relief (0.78x) so chiseled planes read clean and velvety
    nx = (nrm_clean[:, :, 0] * 2.0 - 1.0) * 0.78
    ny = (nrm_clean[:, :, 1] * 2.0 - 1.0) * 0.78
    nz = np.sqrt(np.clip(1.0 - nx * nx - ny * ny, 0.04, 1.0))
    nlen = np.sqrt(nx * nx + ny * ny + nz * nz)
    nrm_out = np.stack([
        0.5 * (nx / nlen) + 0.5,
        0.5 * (ny / nlen) + 0.5,
        0.5 * (nz / nlen) + 0.5,
    ], axis=-1).astype(np.float32)
    nrm_out = np.clip(inpaint_masked_rgb(nrm_out, in_island_1k, iters=10), 0.0, 1.0)

    # ---- 6) Clean Low-Noise Semantic ORM (1024x1024: R=AO, G=Roughness, B=Metallic) ---------------------------------
    # Downsample polished base color cleanly to 1024 for semantic material classification
    base_1k = 0.25 * (rgb[0::2, 0::2] + rgb[0::2, 1::2] + rgb[1::2, 0::2] + rgb[1::2, 1::2])
    r1, g1, b1 = base_1k[:, :, 0], base_1k[:, :, 1], base_1k[:, :, 2]
    maxc = np.maximum(np.maximum(r1, g1), b1)
    minc = np.minimum(np.minimum(r1, g1), b1)
    sat = (maxc - minc) / np.maximum(maxc, 1e-4)
    lum1 = 0.299 * r1 + 0.587 * g1 + 0.114 * b1

    # R: Smooth baked Cycles AO mapped cleanly into [0.42..1.00]
    orm_r = np.clip(0.42 + 0.58 * ao_s1, 0.42, 1.0)

    # G: Clean semantic stylized roughness (zero greasy AI plastic!)
    # Base default = matte stylized cloth (0.82, gently modulated by AO/curvature)
    orm_g = np.clip(0.82 + 0.04 * (1.0 - ao_s1) - 0.03 * np.clip(curv_s1 - 0.5, 0.0, 0.5), 0.76, 0.87)

    # Warm Filipino skin tones -> smooth satin-matte skin ~0.70 (0.67 on face highlight zones)
    skin_ratio = (g1 - b1) / np.maximum(r1 - b1, 1e-4)
    is_skin = (
        (r1 > g1 + 0.02)
        & (g1 > b1 + 0.02)
        & ((r1 - b1) > 0.07) & ((r1 - b1) < 0.46)
        & (skin_ratio > 0.22) & (skin_ratio < 0.72)
        & (lum1 > 0.16) & (lum1 < 0.84)
        & (vgrad_s1 > 0.04)
    )
    orm_g = np.where(is_skin, np.where(vgrad_s1 > 0.76, 0.68, 0.71), orm_g)

    # Dark hair clumps -> sculpted stylized hair sheen ~0.46 (crisp Valorant rim-light read)
    is_hair = (vgrad_s1 > 0.62) & (lum1 < 0.30) & (sat < 0.36) & (~is_skin)
    orm_g = np.where(is_hair, 0.46 + 0.06 * (1.0 - curv_s1), orm_g)

    # Footwear / flip-flops / straps (bottom 7% height) -> crisp satin rubber/leather ~0.36
    is_shoe = (vgrad_s1 < 0.075) & (~is_skin)
    orm_g = np.where(is_shoe, 0.36, orm_g)

    # Glasses / metallic accessories (B channel)
    orm_b = np.zeros((MAP_PX, MAP_PX), dtype=np.float32)
    if kid == "narda":
        # Narda's glasses frame & silver necklace pendant: crisp neutral silver/grey high-curvature accents on head/upper chest
        is_metal = (
            (vgrad_s1 > 0.65) & (vgrad_s1 < 0.93)
            & (sat < 0.18) & (lum1 > 0.22) & (lum1 < 0.78)
            & (curv_s1 > 0.56) & (~is_skin) & (~is_hair)
        )
        orm_g = np.where(is_metal, 0.34, orm_g)
        orm_b = np.where(is_metal, 0.78, orm_b)
    elif kid in ("karding", "jumong", "tere"):
        # Mask / wristband / cap buckle satin-accent contrast (non-metallic crisp satin 0.44)
        if kid == "karding":
            is_mask = (vgrad_s1 > 0.82) & (vgrad_s1 < 0.93) & (lum1 > 0.58) & (sat < 0.32) & (~is_skin)
            orm_g = np.where(is_mask, 0.44, orm_g)
        elif kid == "jumong":
            # Colorful rubber wrist bands / headband
            is_band = (vgrad_s1 > 0.46) & (vgrad_s1 < 0.64) & (sat > 0.48) & (~is_skin)
            orm_g = np.where(is_band, 0.38, orm_g)

    # Smooth ORM channels spatially so transitions are buttery and noise-free, then gutter-dilate!
    orm_r = gaussian_smooth_2d(orm_r, radius=1)
    orm_g = gaussian_smooth_2d(orm_g, radius=2)
    orm_b = gaussian_smooth_2d(orm_b, radius=1)
    orm_out = np.stack([
        np.clip(orm_r, 0.40, 1.0),
        np.clip(orm_g, 0.32, 0.88),
        np.clip(orm_b, 0.0, 0.85),
    ], axis=-1).astype(np.float32)
    orm_out = np.clip(inpaint_masked_rgb(orm_out, in_island_1k, iters=10), 0.0, 1.0)

    tex_stats = {
        "repaired_black_px": repaired_px,
        "orm_ao_mean": round(float(orm_out[:, :, 0][in_island_1k].mean()), 3),
        "orm_rough_mean": round(float(orm_out[:, :, 1][in_island_1k].mean()), 3),
        "orm_rough_std": round(float(orm_out[:, :, 1][in_island_1k].std()), 3),
        "orm_metal_max": round(float(orm_out[:, :, 2][in_island_1k].max()), 3),
    }
    log(
        f"{kid} PBR: repaired {repaired_px} dark bake-miss px | "
        f"AO={tex_stats['orm_ao_mean']:.3f} Rough={tex_stats['orm_rough_mean']:.3f}±{tex_stats['orm_rough_std']:.3f} "
        f"MetalMax={tex_stats['orm_metal_max']:.2f}"
    )
    return rgb, nrm_out, orm_out, tex_stats


def add_facial_morph_targets(kid: str, arm, mesh_obj, rest_w: np.ndarray, pos_groups, H: float) -> dict:
    """Phase 4: Add 4 real glTF facial shape keys (Blink, Smile, Determined, Talk_Open) strictly on head/face verts."""
    me = mesh_obj.data
    AM = arm.matrix_world
    MW = mesh_obj.matrix_world
    MWi_rot = MW.inverted().to_3x3()
    B = arm.data.bones
    nv = len(rest_w)

    if me.shape_keys is None:
        mesh_obj.shape_key_add(name="Basis", from_mix=False)
    for nm in FACIAL_SHAPES:
        if nm in me.shape_keys.key_blocks:
            mesh_obj.shape_key_remove(me.shape_keys.key_blocks[nm])

    # Identify strictly front-face vertices weighted primarily to mixamorig:Head (or upper Neck at chin)
    vgs = mesh_obj.vertex_groups
    vg_names = {g.index: g.name for g in vgs}
    head_gid = vgs.get(P + "Head").index if vgs.get(P + "Head") else -1
    neck_gid = vgs.get(P + "Neck").index if vgs.get(P + "Neck") else -1

    head_b = B[P + "Head"]
    head_origin = np.array(AM @ head_b.head_local, dtype=np.float64)
    scale = H / 1.70

    head_weight = np.zeros(nv, dtype=np.float64)
    for i, v in enumerate(me.vertices):
        hw = 0.0
        for g in v.groups:
            if g.group == head_gid:
                hw += g.weight
            elif g.group == neck_gid:
                hw += 0.5 * g.weight
        head_weight[i] = hw

    # Auto-locate anatomical nose tip: most forward (-Y) vertex near face midline on front head
    mid_face_mask = (
        (head_weight > 0.55)
        & (np.abs(rest_w[:, 0] - head_origin[0]) < 0.022 * scale)
        & (rest_w[:, 2] > head_origin[2] + 0.035 * scale)
        & (rest_w[:, 2] < head_origin[2] + 0.165 * scale)
        & (rest_w[:, 1] < head_origin[1] - 0.050 * scale)
    )
    if np.any(mid_face_mask):
        cand = np.where(mid_face_mask)[0]
        nose_idx = cand[int(np.argmin(rest_w[cand, 1]))]
        nose = rest_w[nose_idx].copy()
        nose[0] = head_origin[0]  # keep exact midline symmetry
    else:
        nose = head_origin + np.array([0.0, -0.115 * scale, 0.105 * scale])

    # Facial anatomical landmarks relative to nose tip (verified for stylized 1.70m Filipino kids!)
    eye_x = 0.038 * scale
    eye_z = nose[2] + 0.028 * scale
    brow_z = eye_z + 0.025 * scale
    mouth_z = nose[2] - 0.032 * scale
    mouth_x = 0.028 * scale
    jaw_pivot = np.array([head_origin[0], head_origin[1] - 0.015 * scale, nose[2] - 0.010 * scale])

    # Front-face mask: strictly head/chin front hemisphere (never moves back of head, ears, shoulders, or neck!)
    front_mask = (
        (head_weight > 0.45)
        & (rest_w[:, 1] < head_origin[1] - 0.032 * scale)
        & (np.abs(rest_w[:, 0] - nose[0]) < 0.105 * scale)
        & (rest_w[:, 2] > head_origin[2] - 0.015 * scale)
        & (rest_w[:, 2] < nose[2] + 0.115 * scale)
    )
    # Smooth falloff at mask boundary
    y_fade = np.clip(((head_origin[1] - 0.032 * scale) - rest_w[:, 1]) / (0.035 * scale), 0.0, 1.0)
    mask_w = np.where(front_mask, y_fade * np.clip(head_weight, 0.0, 1.0), 0.0)

    dx = rest_w[:, 0] - nose[0]
    dy = rest_w[:, 1] - nose[1]
    dz = rest_w[:, 2] - nose[2]

    def _gauss(ax, az, sx, sz):
        return np.exp(-0.5 * ((ax / sx) ** 2 + (az / sz) ** 2))

    # 1) Blink: smooth upper-eyelid descent + tiny lower-lid lift on both left & right eyes
    blink_d = np.zeros((nv, 3), dtype=np.float64)
    for sgn in (-1.0, 1.0):
        ex = dx - sgn * eye_x
        ez_up = (rest_w[:, 2] - (eye_z + 0.008 * scale))
        ez_lo = (rest_w[:, 2] - (eye_z - 0.010 * scale))
        w_up = _gauss(ex, ez_up, 0.022 * scale, 0.011 * scale) * mask_w
        w_lo = _gauss(ex, ez_lo, 0.020 * scale, 0.008 * scale) * mask_w
        blink_d[:, 2] += (-0.0135 * w_up + 0.0032 * w_lo) * scale
        blink_d[:, 1] += (-0.0022 * w_up) * scale

    # 2) Smile: warm upward + outward mouth corner pull + gentle cheek apple lift + subtle lower-lid crinkle
    smile_d = np.zeros((nv, 3), dtype=np.float64)
    for sgn in (-1.0, 1.0):
        mx = dx - sgn * mouth_x
        mz = rest_w[:, 2] - mouth_z
        w_mc = _gauss(mx, mz, 0.022 * scale, 0.016 * scale) * mask_w
        smile_d[:, 0] += sgn * (0.0065 * w_mc) * scale
        smile_d[:, 1] += (0.0045 * w_mc) * scale   # follows cheek wrap backward
        smile_d[:, 2] += (0.0105 * w_mc) * scale   # joyful corner lift

        cx = dx - sgn * (eye_x * 1.05)
        cz = rest_w[:, 2] - (eye_z - 0.024 * scale)
        w_ch = _gauss(cx, cz, 0.028 * scale, 0.022 * scale) * mask_w
        smile_d[:, 1] += (-0.0035 * w_ch) * scale  # fuller cheek apples forward
        smile_d[:, 2] += (0.0045 * w_ch) * scale

    # 3) Determined: heroic Valorant brow furrow (inner brows down+in, outer brows angled up, firm mouth)
    det_d = np.zeros((nv, 3), dtype=np.float64)
    for sgn in (-1.0, 1.0):
        bx_in = dx - sgn * (0.018 * scale)
        bz_in = rest_w[:, 2] - brow_z
        w_in = _gauss(bx_in, bz_in, 0.018 * scale, 0.014 * scale) * mask_w
        det_d[:, 0] += -sgn * (0.0032 * w_in) * scale
        det_d[:, 1] += (-0.0035 * w_in) * scale
        det_d[:, 2] += (-0.0085 * w_in) * scale

        bx_out = dx - sgn * (0.056 * scale)
        bz_out = rest_w[:, 2] - (brow_z + 0.003 * scale)
        w_out = _gauss(bx_out, bz_out, 0.018 * scale, 0.013 * scale) * mask_w
        det_d[:, 2] += (0.0042 * w_out) * scale
    # Subtle lip press / chin set
    w_lip = _gauss(dx, rest_w[:, 2] - mouth_z, 0.032 * scale, 0.011 * scale) * mask_w
    det_d[:, 1] += (0.0020 * w_lip) * scale
    det_d[:, 2] += (0.0018 * w_lip) * scale

    # 4) Talk_Open: clean hinged lower-jaw / chin / lower-lip rotation downward around jaw pivot
    talk_d = np.zeros((nv, 3), dtype=np.float64)
    # Only vertices below upper lip (smooth transition right across mouth_z)
    below_lip = np.clip((mouth_z + 0.004 * scale - rest_w[:, 2]) / (0.016 * scale), 0.0, 1.0)
    below_lip = below_lip * below_lip * (3.0 - 2.0 * below_lip)
    # Taper laterally outside jaw width and near bottom neck
    jaw_x = np.exp(-0.5 * (dx / (0.054 * scale)) ** 2)
    jaw_z_bot = np.clip((rest_w[:, 2] - (head_origin[2] - 0.010 * scale)) / (0.035 * scale), 0.0, 1.0)
    w_jaw = below_lip * jaw_x * jaw_z_bot * mask_w
    ang = math.radians(-8.5)  # natural 8.5 deg jaw drop (~10-13mm chin travel)
    ca, sa = math.cos(ang), math.sin(ang)
    rel = rest_w - jaw_pivot
    ry_rot = rel[:, 1] * ca - rel[:, 2] * sa
    rz_rot = rel[:, 1] * sa + rel[:, 2] * ca
    talk_d[:, 1] = (ry_rot - rel[:, 1]) * w_jaw
    talk_d[:, 2] = (rz_rot - rel[:, 2]) * w_jaw

    shape_stats = {}
    rest_local = np.array([tuple(v.co) for v in me.vertices], dtype=np.float64)
    for name, world_delta in (
        ("Blink", blink_d),
        ("Smile", smile_d),
        ("Determined", det_d),
        ("Talk_Open", talk_d),
    ):
        # Cap per-vertex displacement <= 14 mm for clean Pixar/Valorant expression bounds
        mags_raw = np.linalg.norm(world_delta, axis=1, keepdims=True)
        d_clean = np.where(mags_raw > 0.014 * scale, world_delta * ((0.014 * scale) / np.maximum(mags_raw, 1e-8)), world_delta)
        # Enforce exact welded seam-twin delta parity
        for ids in pos_groups.values():
            if len(ids) > 1:
                avg = d_clean[ids].mean(axis=0)
                d_clean[ids] = avg
        mags = np.linalg.norm(d_clean, axis=1)
        active = mags > 0.0003
        kb = mesh_obj.shape_key_add(name=name, from_mix=False)
        kb.value = 0.0  # CRITICAL in Blender 5.2: shape_key_add defaults value to 1.0!
        for i in range(nv):
            if active[i]:
                d_loc = MWi_rot @ Vector(tuple(d_clean[i]))
                kb.data[i].co = Vector(tuple(rest_local[i])) + d_loc
            else:
                kb.data[i].co = Vector(tuple(rest_local[i]))
        shape_stats[name] = {
            "verts": int(active.sum()),
            "peak_mm": round(float(mags.max() * 1000.0), 2),
        }

    log(
        f"{kid} facial shapes: "
        + ", ".join(f"{k}({v['verts']}v/{v['peak_mm']}mm)" for k, v in shape_stats.items())
    )
    return shape_stats


def polish_one_kid(kid: str, models_dir: Path) -> dict:
    glb_path = models_dir / f"{kid}.glb"
    if not glb_path.is_file():
        raise FileNotFoundError(f"Missing {glb_path}")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = 30
    bpy.context.scene.render.fps_base = 1.0
    bpy.ops.import_scene.gltf(filepath=str(glb_path))

    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    mesh = max(
        (o for o in bpy.data.objects if o.type == "MESH" and o.vertex_groups),
        key=lambda o: len(o.data.vertices),
    )
    # Remove stray helper meshes (e.g. glTF bone display Icosphere)
    for o in list(bpy.data.objects):
        if o.type == "MESH" and o is not mesh:
            bpy.data.objects.remove(o, do_unlink=True)

    # Preserve current action pointer, switch armature to REST for clean surgery
    saved_action = arm.animation_data.action if arm.animation_data else None
    if arm.animation_data:
        arm.animation_data.action = None
    arm.data.pose_position = "REST"
    for b in arm.data.bones:
        if b.name.startswith(P) and (b.name.endswith("_End") or b.name.endswith("Tip")):
            b.use_deform = False
    bpy.context.view_layer.update()

    # Clear old corr_* shape keys before rest-geometry polish (bake_correctives.py will re-bake them fresh!)
    if mesh.data.shape_keys is not None:
        for kb in list(mesh.data.shape_keys.key_blocks):
            mesh.shape_key_remove(kb)

    loose_removed = drop_loose_components(mesh)
    me = mesh.data
    MW = mesh.matrix_world.copy()
    MWi = MW.inverted()

    # Extract existing base (2048) & normal (1024) maps
    base_img_src = next((im for im in bpy.data.images if "base" in im.name.lower()), None)
    if base_img_src is None:
        raise RuntimeError(f"{kid}: no embedded base texture found")
    if base_img_src.size[0] != BASE_PX or base_img_src.size[1] != BASE_PX:
        base_img_src.scale(BASE_PX, BASE_PX)
    base_px = np.array(base_img_src.pixels[:], dtype=np.float32).reshape(BASE_PX, BASE_PX, 4)[:, :, :3].copy()

    nrm_disk = models_dir / f"{kid}_2.png"
    if nrm_disk.is_file():
        nrm_img_src = bpy.data.images.load(str(nrm_disk), check_existing=False)
        nrm_img_src.colorspace_settings.name = "Non-Color"
    else:
        nrm_img_src = next((im for im in bpy.data.images if im.name.lower().endswith("_2") or "normal" in im.name.lower()), None)
    if nrm_img_src is not None:
        if nrm_img_src.size[0] != MAP_PX or nrm_img_src.size[1] != MAP_PX:
            nrm_img_src.scale(MAP_PX, MAP_PX)
        nrm_px = np.array(nrm_img_src.pixels[:], dtype=np.float32).reshape(MAP_PX, MAP_PX, 4)[:, :, :3].copy()
    else:
        nrm_px = np.dstack([np.full((MAP_PX, MAP_PX), 0.5), np.full((MAP_PX, MAP_PX), 0.5), np.ones((MAP_PX, MAP_PX))]).astype(np.float32)

    rest_w = np.array([tuple(MW @ v.co) for v in me.vertices], dtype=np.float64)
    zmin_0, zmax_0 = float(rest_w[:, 2].min()), float(rest_w[:, 2].max())
    H = max(zmax_0 - zmin_0, 1.0)
    tris_before = sum(len(p.vertices) - 2 for p in me.polygons)

    pos_groups, v2w, w_members, w_nbr_list, nbr = build_welded_topology(me, rest_w)
    vert_rgb = sample_base_luminance_per_vert(me, base_px)

    # Compute Idle frame-0 skinning matrices & posed bone segments (for weight & cape clearance!)
    skin_mats_idle, posed_segs_idle = compute_idle_bone_matrices(arm, mesh)

    # ---- Stage 2: Weight quarantine & LBS Idle-stretch polish -------------------------------------------------------
    W, weight_stats = quarantine_and_polish_weights(
        kid, arm, mesh, rest_w, pos_groups, nbr, skin_mats_idle, vert_rgb
    )

    # ---- Stage 3: Cape clearance push + Chiseled stylized bilateral plane polish ------------------------------------
    rest_w, cape_stats = push_cape_behind_arms(
        kid, arm, mesh, rest_w, W, pos_groups, nbr, posed_segs_idle
    )
    rest_w = chiseled_bilateral_plane_polish(
        kid, arm, mesh, rest_w, pos_groups, v2w, w_members, w_nbr_list
    )

    # Write polished rest coordinates back into mesh & refresh normals
    for i, v in enumerate(me.vertices):
        v.co = MWi @ Vector(tuple(rest_w[i]))
    for p in me.polygons:
        p.use_smooth = True
    me.update()

    # Re-measure height & vertical gradient after polish
    zmin = float(rest_w[:, 2].min())
    zmax = float(rest_w[:, 2].max())
    H = max(zmax - zmin, 1.0)
    vgrad_v = np.clip((rest_w[:, 2] - zmin) / H, 0.0, 1.0).astype(np.float32)
    curv_v = compute_welded_curvature(me, rest_w, v2w, w_members, w_nbr_list)

    # ---- Stage 4: Multi-Map Baking (AO + Curvature + Vertical Gradient + UV Mask) -----------------------------------
    ao_1k, curv_1k, vgrad_1k, mask_2k = bake_auxiliary_maps(mesh, H, curv_v, vgrad_v)

    # ---- Stage 5: Painterly Stylized PBR Texturing ------------------------------------------------------------------
    base_out, nrm_out, orm_out, tex_stats = process_painterly_pbr_maps(
        kid, base_px, nrm_px, ao_1k, curv_1k, vgrad_1k, mask_2k
    )

    # ---- Stage 6: Real glTF Facial Morph Targets (Blink, Smile, Determined, Talk_Open) ------------------------------
    face_stats = add_facial_morph_targets(kid, arm, mesh, rest_w, pos_groups, H)

    # ---- Stage 7: Save disk maps & build clean PBR glTF Material -> Export Match LOD0 .glb --------------------------
    # Write clean disk maps (<kid>_2.png normal, <kid>_orm.png ORM, temporary <kid>_base.jpg for compact GLB packing)
    base_tmp = models_dir / f"{kid}_base.jpg"
    nrm_out_path = models_dir / f"{kid}_2.png"
    orm_out_path = models_dir / f"{kid}_orm.png"

    scn = bpy.context.scene
    img_base = bpy.data.images.new(f"{kid}_base", BASE_PX, BASE_PX, alpha=False)
    img_base.colorspace_settings.name = "sRGB"
    rgba_b = np.concatenate([base_out, np.ones((BASE_PX, BASE_PX, 1), dtype=np.float32)], axis=-1)
    img_base.pixels.foreach_set(rgba_b.ravel())
    img_base.filepath_raw = str(base_tmp)
    img_base.file_format = "JPEG"
    scn.render.image_settings.quality = 90
    img_base.save()
    img_base.reload()

    img_nrm = bpy.data.images.new(f"{kid}_2", MAP_PX, MAP_PX, alpha=False)
    img_nrm.colorspace_settings.name = "Non-Color"
    rgba_n = np.concatenate([nrm_out, np.ones((MAP_PX, MAP_PX, 1), dtype=np.float32)], axis=-1)
    img_nrm.pixels.foreach_set(rgba_n.ravel())
    img_nrm.filepath_raw = str(nrm_out_path)
    img_nrm.file_format = "PNG"
    img_nrm.save()
    img_nrm.reload()

    img_orm = bpy.data.images.new(f"{kid}_orm", MAP_PX, MAP_PX, alpha=False)
    img_orm.colorspace_settings.name = "Non-Color"
    rgba_o = np.concatenate([orm_out, np.ones((MAP_PX, MAP_PX, 1), dtype=np.float32)], axis=-1)
    img_orm.pixels.foreach_set(rgba_o.ravel())
    img_orm.filepath_raw = str(orm_out_path)
    img_orm.file_format = "PNG"
    img_orm.save()
    img_orm.reload()

    # Rebuild clean single KidMaterial wired for glTF 2.0 (Base Color + Normal + ORM + Occlusion)
    for m in list(me.materials):
        me.materials.clear()
    mat = bpy.data.materials.new("KidMaterial")
    if mat.node_tree is None:
        mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    nodes.clear()
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    mat_out = nodes.new("ShaderNodeOutputMaterial")
    links.new(bsdf.outputs["BSDF"], mat_out.inputs["Surface"])

    tex_b = nodes.new("ShaderNodeTexImage")
    tex_b.image = img_base
    tex_n = nodes.new("ShaderNodeTexImage")
    tex_n.image = img_nrm
    nmap = nodes.new("ShaderNodeNormalMap")
    tex_o = nodes.new("ShaderNodeTexImage")
    tex_o.image = img_orm
    sep_o = nodes.new("ShaderNodeSeparateColor")
    gltf_o = nodes.new("ShaderNodeGroup")
    gltf_o.node_tree = ensure_gltf_material_output_group()

    links.new(tex_b.outputs["Color"], bsdf.inputs["Base Color"])
    links.new(tex_n.outputs["Color"], nmap.inputs["Color"])
    links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    links.new(tex_o.outputs["Color"], sep_o.inputs["Color"])
    links.new(sep_o.outputs["Red"], gltf_o.inputs["Occlusion"])
    links.new(sep_o.outputs["Green"], bsdf.inputs["Roughness"])
    links.new(sep_o.outputs["Blue"], bsdf.inputs["Metallic"])
    me.materials.append(mat)

    # Restore pose mode & action reference, export glTF with all 20 clips, skins, morphs, extras
    arm.data.pose_position = "POSE"
    if arm.animation_data and saved_action is not None:
        arm.animation_data.action = saved_action
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)

    for o in bpy.context.view_layer.objects:
        o.select_set(o in (arm, mesh))
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path), export_format="GLB", use_selection=True, export_yup=True,
        export_animations=True, export_animation_mode="ACTIONS",
        export_force_sampling=False, export_bake_animation=False,
        export_skins=True, export_all_influences=False, export_def_bones=False,
        export_image_format="AUTO", export_morph=True, export_extras=True,
        export_apply=False, export_texcoords=True, export_normals=True, export_tangents=True,
    )
    if base_tmp.is_file():
        base_tmp.unlink()

    # Update <kid>_rig.json facialShapes + texture metadata
    rig_json = models_dir / f"{kid}_rig.json"
    if rig_json.is_file():
        rig = json.loads(rig_json.read_text(encoding="utf-8"))
        rig["facialShapes"] = list(FACIAL_SHAPES)
        rig.setdefault("textures", {}).update({
            "baseColorEmbedded": True,
            "normal": f"models/characters/{kid}_2",
            "orm": f"models/characters/{kid}_orm",
            "ormChannels": "R=AO,G=Roughness,B=Metallic",
            "stylizedPolishVersion": 2,
        })
        rig_json.write_text(json.dumps(rig, indent=2) + "\n", encoding="utf-8", newline="\n")

    tris_after = sum(len(p.vertices) - 2 for p in me.polygons)
    kb_size = os.path.getsize(glb_path) // 1024
    log(f"{kid} exported: {tris_before}->{tris_after} tris, loose_dropped={loose_removed}, {len(bpy.data.actions)} clips, {kb_size} KB")
    return {
        "kid": kid,
        "tris_before": tris_before,
        "tris_after": tris_after,
        "loose_removed": loose_removed,
        "height_m": round(H, 3),
        "sole_z_m": round(zmin, 4),
        "file_kb": kb_size,
        "clips": len(bpy.data.actions),
        "weights": weight_stats,
        "cape": cape_stats,
        "textures": tex_stats,
        "facial_shapes": face_stats,
    }


def main() -> int:
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser(description="Valorant x Arcane 3D character polish pass for Kalyeah kids")
    ap.add_argument("--kids", default="all", help="Comma-separated kid ids or 'all'")
    ap.add_argument("--models-dir", default="assets/models/characters")
    ap.add_argument("--out-metrics", default="")
    args = ap.parse_args(argv)

    models_dir = Path(args.models_dir).resolve()
    kids = list(ALL_KIDS) if args.kids.strip().lower() == "all" else [
        k.strip().lower() for k in args.kids.split(",") if k.strip()
    ]
    results = []
    for kid in kids:
        results.append(polish_one_kid(kid, models_dir))

    if args.out_metrics:
        out_p = Path(args.out_metrics)
        out_p.parent.mkdir(parents=True, exist_ok=True)
        out_p.write_text(json.dumps({"kids": results}, indent=2) + "\n", encoding="utf-8", newline="\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
