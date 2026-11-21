#!/usr/bin/env python3
"""apex-rig-forge R4 measured Range-of-Motion (ROM) QA gate for Kalyeah character .glb models.

Poses a character .glb through joint limits, half-ranges, combined limb poses, and R4 contact poses
using unity/Data/rig__joint_limits.json (including per-kid overrides, helper/twist bone drivers, and
kalyeah_correctives shape drivers when present), then measures all 11 R4 metrics quantitatively.

Usage:
  blender --background --python tools/blender/kids/rom_metrics.py -- assets/models/characters/narda.glb
  blender --background --python tools/blender/kids/rom_metrics.py -- assets/models/characters/narda.glb --out report.json --render out_dir
  blender --background --python tools/blender/kids/rom_metrics.py -- --kids narda,karding,domeng,paeng --out rom_baseline.json
"""
from __future__ import annotations

import argparse
import json
import math
import sys
from collections import deque
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree

P = "mixamorig:"

# R4 pass thresholds (apex-rig-forge R4)
THRESH = {
    "fold_max_deg": 60.0,
    "stretch_max_pct": 2.0,
    "core_rigidity_max_pct": 3.0,
    "volume_loss_max_pct": 20.0,
    "volume_loss_corr_max_pct": 10.0,
    "twist_loss_max_pct": 25.0,
    "self_intersections_max": 0,
    "cloth_leg_pen_max": 0,
    "seam_crack_max_mm": 0.5,
    "flipped_faces_max": 0,
    "spike_ratio_max": 3.0,
    "capsule_excess_max_mm": 20.0,
    "layer_gap_delta_max_mm": 2.0,
    "contact_pen_max_mm": 3.0,
}

SEGMENTS = ("Arm", "ForeArm", "UpLeg", "Leg")
PRIMARY_ROM_JOINTS = [
    "Spine", "Spine1", "Spine2", "Neck", "Head",
    "LeftShoulder", "RightShoulder",
    "LeftArm", "RightArm",
    "LeftForeArm", "RightForeArm",
    "LeftHand", "RightHand",
    "LeftUpLeg", "RightUpLeg",
    "LeftLeg", "RightLeg",
    "LeftFoot", "RightFoot",
    "LeftToeBase", "RightToeBase",
]
FINGER_ROM_JOINTS = [
    f"{s}Hand{d}{j}"
    for s in ("Left", "Right")
    for d in ("Thumb", "Index", "Middle", "Ring", "Pinky")
    for j in ("1", "2", "3")
]


def repo_root() -> Path:
    return Path(__file__).resolve().parents[3]


def load_limits(path: Path) -> dict:
    raw = json.loads(path.read_text(encoding="utf-8"))
    return raw.get("data", raw)


def effective_limits(limits_data: dict, kid: str, joint: str) -> dict:
    """Merge base joint range with narrowing per-kid override (overrides never widen)."""
    base = limits_data["joints"][joint]
    ov = limits_data.get("overrides", {}).get(kid.lower(), {}).get(joint, {})
    out = {
        "flex": list(base["flex"]),
        "abduct": list(base["abduct"]),
        "twist": list(base["twist"]),
        "twistRequiresFlexDeg": float(base.get("twistRequiresFlexDeg", 0.0)),
        "axes": base["axes"],
        "kind": base.get("kind", "ball"),
    }
    for ch in ("flex", "abduct", "twist"):
        if ch in ov:
            out[ch] = [max(float(ov[ch][0]), out[ch][0]), min(float(ov[ch][1]), out[ch][1])]
    return out


def point_seg_dist(pts: np.ndarray, a: np.ndarray, b: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    ab = b - a
    denom = max(float(ab @ ab), 1e-12)
    t = ((pts - a) @ ab) / denom
    proj = a + np.clip(t, 0.0, 1.0)[:, None] * ab
    return t, np.linalg.norm(pts - proj, axis=1)


def cross_section_area(pts: np.ndarray, center: np.ndarray, axis_u: np.ndarray, axis_v: np.ndarray,
                       outer_only: bool = False) -> float:
    """2D angular-sector cross-section area around center projected onto (axis_u, axis_v).

    When outer_only=True, restricts to axis_u >= 0 (outer half of joint bend).
    """
    if len(pts) < 4:
        return 0.0
    rel = pts - center
    u = rel @ axis_u
    v = rel @ axis_v
    if outer_only:
        m = u >= -1e-4
        u, v = u[m], v[m]
        if len(u) < 4:
            return 0.0
        ang = np.arctan2(v, u)  # [-pi/2, +pi/2]
        rad = np.hypot(u, v)
        bins = np.linspace(-math.pi * 0.5, math.pi * 0.5, 13)
    else:
        ang = np.arctan2(v, u)  # [-pi, +pi]
        rad = np.hypot(u, v)
        bins = np.linspace(-math.pi, math.pi, 17)
    r_prof = []
    a_prof = []
    for i in range(len(bins) - 1):
        bm = (ang >= bins[i]) & (ang < bins[i + 1])
        if bm.any():
            r_prof.append(float(np.percentile(rad[bm], 75)))
            a_prof.append(0.5 * (bins[i] + bins[i + 1]))
    if len(r_prof) < 3:
        return 0.0
    area = 0.0
    n = len(r_prof)
    end = n - 1 if outer_only else n
    for i in range(end):
        j = (i + 1) % n
        da = a_prof[j] - a_prof[i]
        if not outer_only and da <= 0:
            da += 2.0 * math.pi
        if da > 0:
            area += 0.5 * r_prof[i] * r_prof[j] * math.sin(min(da, math.pi * 0.9))
    return max(area, 0.0)


def _to_plain(val):
    """Convert Blender IDPropertyGroup / IDPropertyArray recursively into plain Python types."""
    if hasattr(val, "to_dict"):
        return {k: _to_plain(v) for k, v in val.to_dict().items()}
    if hasattr(val, "to_list"):
        return [_to_plain(v) for v in val.to_list()]
    if isinstance(val, dict):
        return {k: _to_plain(v) for k, v in val.items()}
    if isinstance(val, (list, tuple)):
        return [_to_plain(v) for v in val]
    return val


def _dir_to_blender(vec, coord_space: str) -> Vector:
    """Convert a 3D direction vector from gltf_yup (+X L, +Y up, +Z fwd) to Blender Z-up (+X L, -Y fwd, +Z up)."""
    v = [float(c) for c in vec]
    if coord_space == "gltf_yup":
        return Vector((v[0], -v[2], v[1])).normalized()
    return Vector((v[0], v[1], v[2])).normalized()


def swing_twist_decompose(q: Quaternion, twist_axis: Vector) -> tuple[Quaternion, Quaternion, float]:
    """Decompose q = q_swing * q_twist around unit twist_axis; returns (q_swing, q_twist, twist_deg)."""
    ax = twist_axis.normalized()
    v = Vector((q.x, q.y, q.z))
    proj = ax * v.dot(ax)
    qt = Quaternion((q.w, proj.x, proj.y, proj.z))
    if qt.magnitude < 1e-8:
        qt = Quaternion((1.0, 0.0, 0.0, 0.0))
    else:
        qt.normalize()
    qs = q @ qt.inverted()
    qs.normalize()
    # Signed twist angle
    tw_rad = 2.0 * math.atan2(Vector((qt.x, qt.y, qt.z)).dot(ax), qt.w)
    if tw_rad > math.pi:
        tw_rad -= 2.0 * math.pi
    elif tw_rad < -math.pi:
        tw_rad += 2.0 * math.pi
    return qs, qt, math.degrees(tw_rad)


def render_closeup(mesh, arm, focus_world: Vector, radius: float, out_prefix: Path) -> list[str]:
    """Render textured + grey/red backface closeup of a problem joint."""
    scn = bpy.context.scene
    # Ensure lights & camera
    for o in list(bpy.data.objects):
        if o.type in ("LIGHT", "CAMERA"):
            bpy.data.objects.remove(o, do_unlink=True)
    for loc in ((2.0, -3.0, 3.0), (-2.0, -3.0, 2.0), (0.0, 3.0, -1.5)):
        ld = bpy.data.lights.new("rom_l", "SUN")
        ld.energy = 2.5
        lo = bpy.data.objects.new("rom_l", ld)
        lo.rotation_euler = (Vector((0, 0, 0)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        scn.collection.objects.link(lo)
    cam_d = bpy.data.cameras.new("rom_cam")
    cam_d.lens = 50.0
    cam = bpy.data.objects.new("rom_cam", cam_d)
    scn.collection.objects.link(cam)
    scn.camera = cam
    scn.render.engine = "CYCLES"
    scn.cycles.samples = 16
    scn.render.resolution_x = 384
    scn.render.resolution_y = 384
    scn.world = bpy.data.worlds.new("rom_w")
    scn.world.color = (0.35, 0.37, 0.42)

    view_dir = Vector((0.55 if focus_world.x >= 0 else -0.55, -1.0, 0.25)).normalized()
    cam.location = focus_world + view_dir * max(radius * 5.2, 0.35)
    cam.rotation_euler = (focus_world - cam.location).to_track_quat("-Z", "Y").to_euler()

    out_prefix.parent.mkdir(parents=True, exist_ok=True)
    tex_path = str(out_prefix) + "_tex.png"
    scn.render.filepath = tex_path
    bpy.ops.render.render(write_still=True)

    # Save materials, apply grey front / red backface shader
    orig_mats = list(mesh.data.materials)
    mat = bpy.data.materials.new("rom_geo")
    if mat.node_tree is None:
        mat.use_nodes = True
    nt = mat.node_tree
    bs = nt.nodes["Principled BSDF"]
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.inputs["A"].default_value = (0.72, 0.72, 0.72, 1.0)
    mix.inputs["B"].default_value = (1.0, 0.05, 0.05, 1.0)
    nt.links.new(geo.outputs["Backfacing"], mix.inputs["Factor"])
    nt.links.new(mix.outputs["Result"], bs.inputs["Base Color"])
    mesh.data.materials.clear()
    mesh.data.materials.append(mat)

    geo_path = str(out_prefix) + "_geo.png"
    scn.render.filepath = geo_path
    bpy.ops.render.render(write_still=True)

    mesh.data.materials.clear()
    for m in orig_mats:
        mesh.data.materials.append(m)
    bpy.data.materials.remove(mat)
    return [tex_path, geo_path]


class RomEvaluator:
    def __init__(self, glb_path: Path, kid: str, limits_data: dict):
        self.glb_path = glb_path
        self.kid = kid.lower()
        self.limits = limits_data

        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=str(glb_path))
        for a in list(bpy.data.actions):
            bpy.data.actions.remove(a)
        self.arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
        if self.arm.animation_data:
            self.arm.animation_data_clear()
        self.arm.data.pose_position = "POSE"
        self.mesh = max(
            (o for o in bpy.data.objects if o.type == "MESH" and o.vertex_groups),
            key=lambda o: len(o.data.vertices),
        )
        for o in [o for o in bpy.data.objects if o.type == "MESH" and o is not self.mesh]:
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.context.view_layer.update()

        self.AM = self.arm.matrix_world.copy()
        self.AMi = self.AM.inverted()
        self.MW = self.mesh.matrix_world.copy()
        self.mw_np = np.array(self.MW, dtype=np.float64)
        self.B = self.arm.data.bones
        self.pb = self.arm.pose.bones
        self.me = self.mesh.data
        self.nv = len(self.me.vertices)

        # Parse kalyeah_correctives metadata if present (supports JSON str or Blender IDPropertyArray/list)
        self.correctives_meta = []
        raw_corr = self.me.get("kalyeah_correctives")
        if isinstance(raw_corr, str):
            try:
                parsed = json.loads(raw_corr)
                if isinstance(parsed, list):
                    self.correctives_meta = parsed
            except json.JSONDecodeError:
                self.correctives_meta = []
        elif raw_corr is not None:
            plain = _to_plain(raw_corr)
            if isinstance(plain, list):
                self.correctives_meta = [c for c in plain if isinstance(c, dict)]
        self.corrective_bones = {c.get("bone", "").replace(P, "") for c in self.correctives_meta if isinstance(c, dict)}
        if self.me.shape_keys:
            for kb in self.me.shape_keys.key_blocks:
                if kb.name.startswith("corr_"):
                    self.corrective_bones.add(kb.name[5:].split("_")[0])

        self._rest_ignore_cache: dict[tuple[int, str], set[int]] = {}
        self._precompute_rest()

    def _precompute_rest(self) -> None:
        me = self.me
        nv = self.nv
        flat = np.empty(nv * 3, dtype=np.float64)
        me.vertices.foreach_get("co", flat)
        self.rest_co = flat.reshape(-1, 3) @ self.mw_np[:3, :3].T + self.mw_np[:3, 3]

        # Dominant bone, weighted bone set, & cloth classification
        vg_names = [g.name for g in self.mesh.vertex_groups]
        self.dom_bone: list[str | None] = [None] * nv
        self.weighted_bones: set[str] = set()
        self.is_extra_cloth = np.zeros(nv, dtype=bool)
        extra_gids = {i for i, n in enumerate(vg_names) if not n.startswith(P)}
        for i, v in enumerate(me.vertices):
            best_g, best_w = None, 0.0
            for g in v.groups:
                if g.weight > 1e-4:
                    self.weighted_bones.add(vg_names[g.group])
                if g.weight > best_w:
                    best_w = g.weight
                    best_g = vg_names[g.group]
                if g.group in extra_gids and g.weight > 0.05:
                    self.is_extra_cloth[i] = True
            self.dom_bone[i] = best_g

        # Triangulation + adjacency
        me.calc_loop_triangles()
        self.tris = np.array([tuple(lt.vertices) for lt in me.loop_triangles], dtype=np.int64)
        self.ntris = len(self.tris)

        # Rest triangle normals
        self.rest_tri_normals = self._tri_normals(self.rest_co)
        # Dominant bone per triangle (from first vertex)
        self.tri_dom = [self.dom_bone[int(t[0])] for t in self.tris]

        # Edge-to-triangle adjacency (for fold dihedral angle)
        edge_tris: dict[tuple[int, int], list[int]] = {}
        adj_nbrs: list[set[int]] = [set() for _ in range(nv)]
        for ti, (a, b, c) in enumerate(self.tris):
            ia, ib, ic = int(a), int(b), int(c)
            adj_nbrs[ia].update((ib, ic))
            adj_nbrs[ib].update((ia, ic))
            adj_nbrs[ic].update((ia, ib))
            for u, v in ((ia, ib), (ib, ic), (ic, ia)):
                ek = (u, v) if u < v else (v, u)
                edge_tris.setdefault(ek, []).append(ti)

        e_verts = []
        e_t0 = []
        e_t1 = []
        for (u, v), tlist in edge_tris.items():
            if len(tlist) == 2:
                e_verts.append((u, v))
                e_t0.append(tlist[0])
                e_t1.append(tlist[1])
        self.int_edge_v = np.array(e_verts, dtype=np.int64) if e_verts else np.zeros((0, 2), dtype=np.int64)
        self.int_edge_t0 = np.array(e_t0, dtype=np.int64)
        self.int_edge_t1 = np.array(e_t1, dtype=np.int64)
        self.rest_edge_dihedral = self._edge_dihedrals(self.rest_tri_normals)
        self.smooth_at_rest_edges = self.rest_edge_dihedral < 45.0

        # 1-ring neighbor Laplacian CSR arrays (for spike detection)
        self.nbr_offsets = np.zeros(nv + 1, dtype=np.int64)
        flat_nbrs = []
        for i in range(nv):
            nb = list(adj_nbrs[i])
            flat_nbrs.extend(nb)
            self.nbr_offsets[i + 1] = len(flat_nbrs)
        self.nbr_idx = np.array(flat_nbrs, dtype=np.int64)
        self.nbr_deg = np.maximum(self.nbr_offsets[1:] - self.nbr_offsets[:-1], 1)
        self.rest_lap_off = self._laplacian_offsets(self.rest_co)
        nz = self.rest_lap_off[self.rest_lap_off > 1e-6]
        self.rest_lap_median = max(float(np.median(nz)) if len(nz) else 0.002, 0.001)

        # Seam twin vertex groups
        qpos = np.round(self.rest_co / 1e-5).astype(np.int64)
        pos_map: dict[tuple[int, int, int], list[int]] = {}
        for i, k in enumerate(map(tuple, qpos)):
            pos_map.setdefault(k, []).append(i)
        self.twin_pairs = []
        for ids in pos_map.values():
            if len(ids) > 1:
                a0 = ids[0]
                for b0 in ids[1:]:
                    self.twin_pairs.append((a0, b0))
        self.twin_pairs_np = np.array(self.twin_pairs, dtype=np.int64) if self.twin_pairs else np.zeros((0, 2), dtype=np.int64)

        # Connected shell components (for layer gap metric on separated clothing/body layers)
        comp = np.full(nv, -1, dtype=np.int64)
        cid = 0
        for i in range(nv):
            if comp[i] != -1:
                continue
            q = deque([i])
            comp[i] = cid
            while q:
                u = q.popleft()
                for nb in adj_nbrs[u]:
                    if comp[nb] == -1:
                        comp[nb] = cid
                        q.append(nb)
            cid += 1
        self.num_components = cid
        self.layer_pairs = []  # (layer_vert_idx, skin_tri_idx, rest_gap_m)
        if cid > 1:
            # Largest component = primary skin/body; check smaller shells within 15mm as stacked layers
            counts = np.bincount(comp)
            body_cid = int(np.argmax(counts))
            body_tri_mask = (comp[self.tris[:, 0]] == body_cid)
            body_tri_indices = np.where(body_tri_mask)[0]
            if len(body_tri_indices) > 0:
                bvh_body = BVHTree.FromPolygons(
                    [Vector(tuple(p)) for p in self.rest_co],
                    [tuple(map(int, self.tris[ti])) for ti in body_tri_indices],
                    all_triangles=True,
                )
                other_verts = np.where(comp != body_cid)[0]
                # Sample up to 800 layer vertices uniformly for speed
                if len(other_verts) > 800:
                    other_verts = other_verts[np.linspace(0, len(other_verts) - 1, 800, dtype=int)]
                for vi in other_verts:
                    p = Vector(tuple(self.rest_co[vi]))
                    loc, norm, fi, dist = bvh_body.find_nearest(p, 0.015)
                    if loc is not None and norm is not None:
                        gap = float((p - loc).dot(norm))
                        if 0.0 <= gap <= 0.015:
                            self.layer_pairs.append((int(vi), int(body_tri_indices[fi]), gap))

        # Rest bone segments & segment core/capsule caches
        self.rest_bone_rot = {
            b.name: (self.AM @ b.matrix_local).to_3x3().normalized()
            for b in self.B
        }
        self.seg_info: dict[str, dict] = {}
        for s in ("Left", "Right"):
            for seg in SEGMENTS:
                key = s + seg
                if P + key not in self.B:
                    continue
                b = self.B[P + key]
                a = np.array(self.AM @ b.head_local, dtype=np.float64)
                bt = np.array(self.AM @ b.tail_local, dtype=np.float64)
                grp = {P + key}
                if seg == "Arm":
                    grp.add(P + s + "ArmTwist")
                elif seg == "ForeArm":
                    grp.update((P + s + "ForeArmTwist1", P + s + "ForeArmTwist2"))
                mask = np.array([d in grp for d in self.dom_bone], dtype=bool) & (~self.is_extra_cloth)
                idx = np.where(mask)[0]
                if len(idx) < 8:
                    continue
                t, d = point_seg_dist(self.rest_co[idx], a, bt)
                core_m = (t > 0.25) & (t < 0.75)
                dc = d[core_m] if core_m.any() else d
                r_core = float(np.percentile(dc, 35))
                r_cap = float(np.percentile(dc, 75))
                tight = idx[core_m & (d <= 1.35 * max(r_core, 0.02))]
                prox = idx[(t > 0.20) & (t < 0.38) & (d <= 1.35 * max(r_core, 0.02))]
                dist_ring = idx[(t > 0.62) & (t < 0.80) & (d <= 1.35 * max(r_core, 0.02))]
                mid_slab = idx[(t > 0.38) & (t < 0.62) & (d <= 1.45 * max(r_core, 0.02))]
                axis = (bt - a) / max(float(np.linalg.norm(bt - a)), 1e-6)
                u0 = np.array([1.0, 0.0, 0.0]) if abs(axis[0]) < 0.8 else np.array([0.0, 1.0, 0.0])
                u0 = u0 - (u0 @ axis) * axis
                u0 /= np.linalg.norm(u0)
                v0 = np.cross(axis, u0)
                rest_mid_area = cross_section_area(self.rest_co[mid_slab], 0.5 * (a + bt), u0, v0, outer_only=False)
                rest_ring_len = float(np.linalg.norm(self.rest_co[dist_ring].mean(axis=0) - self.rest_co[prox].mean(axis=0))) if (len(prox) >= 3 and len(dist_ring) >= 3) else 0.0
                _, rest_core_d = point_seg_dist(self.rest_co[tight], a, bt) if len(tight) else (np.array([]), np.array([]))
                self.seg_info[key] = {
                    "idx": idx,
                    "tight": tight,
                    "prox": prox,
                    "dist": dist_ring,
                    "mid": mid_slab,
                    "r_core": r_core,
                    "r_cap": r_cap,
                    "rest_d_all": d,
                    "rest_core_d": rest_core_d,
                    "rest_ring_len": rest_ring_len,
                    "rest_mid_area": rest_mid_area,
                }

        # Precompute anatomical body-part vertex/face sets for contact penetration & cloth-vs-leg
        self.part_verts: dict[str, np.ndarray] = {}
        self.part_tris: dict[str, np.ndarray] = {}
        self._build_part("Torso", ("Hips", "Spine", "Spine1", "Spine2"))
        for s in ("Left", "Right"):
            self._build_part(s + "Arm", (s + "Arm", s + "ArmTwist"), s + "Arm", 0.25, 0.85)
            self._build_part(s + "ForeArm", (s + "ForeArm", s + "ForeArmTwist1", s + "ForeArmTwist2", s + "Hand"), s + "ForeArm", 0.25, 1.25)
            self._build_part(s + "Hand", (s + "Hand",) + tuple(f"{s}Hand{d}{j}" for d in ("Thumb", "Index", "Middle", "Ring", "Pinky") for j in ("1", "2", "3")))
            self._build_part(s + "UpLeg", (s + "UpLeg",), s + "UpLeg", 0.20, 0.85, exclude_cloth=True)
            self._build_part(s + "Leg", (s + "Leg", s + "Foot"), s + "Leg", 0.22, 1.15, exclude_cloth=True)

        # Cloth/hem vertices (extra hip/skirt bones OR flared skirt/shorts shell around UpLeg > 1.38x core radius)
        cloth_mask = np.zeros(nv, dtype=bool)
        hip_extra_bones = {
            b.name for b in self.B
            if not b.name.startswith(P) and any(a.name in (P + "Hips", P + "LeftUpLeg", P + "RightUpLeg") for a in b.parent_recursive)
        }
        for i, db in enumerate(self.dom_bone):
            if db in hip_extra_bones:
                cloth_mask[i] = True
        for s in ("Left", "Right"):
            ul = s + "UpLeg"
            if ul in self.seg_info and P + ul in self.B:
                b = self.B[P + ul]
                a = np.array(self.AM @ b.head_local, dtype=np.float64)
                bt = np.array(self.AM @ b.tail_local, dtype=np.float64)
                t, d = point_seg_dist(self.rest_co, a, bt)
                r_core = self.seg_info[ul]["r_core"]
                ul_m = np.array([db in (P + ul, P + "Hips") for db in self.dom_bone], dtype=bool)
                # Skirt flare vertices hang further out from the thigh core
                flare = ul_m & (t > 0.05) & (t < 0.70) & (d > 1.45 * max(r_core, 0.05))
                cloth_mask |= flare
        self.cloth_verts = np.where(cloth_mask)[0]
        c_tri_m = cloth_mask[self.tris[:, 0]] & cloth_mask[self.tris[:, 1]] & cloth_mask[self.tris[:, 2]]
        self.cloth_tris = self.tris[c_tri_m]

    def _build_part(self, name: str, shorts: tuple[str, ...], seg: str | None = None,
                    t_min: float = 0.0, t_max: float = 1.0, exclude_cloth: bool = False) -> None:
        s_set = {P + x for x in shorts}
        m = np.array([d in s_set for d in self.dom_bone], dtype=bool)
        if exclude_cloth:
            m &= (~self.is_extra_cloth)
        if seg is not None and P + seg in self.B:
            b = self.B[P + seg]
            a = np.array(self.AM @ b.head_local, dtype=np.float64)
            bt = np.array(self.AM @ b.tail_local, dtype=np.float64)
            t, d = point_seg_dist(self.rest_co, a, bt)
            m &= (t >= t_min) & (t <= t_max)
            if exclude_cloth and seg in self.seg_info:
                m &= (d <= 1.38 * max(self.seg_info[seg]["r_core"], 0.05))
        self.part_verts[name] = np.where(m)[0]
        tm = m[self.tris[:, 0]] & m[self.tris[:, 1]] & m[self.tris[:, 2]]
        self.part_tris[name] = self.tris[tm]

    def _tri_normals(self, pts: np.ndarray) -> np.ndarray:
        v0 = pts[self.tris[:, 0]]
        v1 = pts[self.tris[:, 1]]
        v2 = pts[self.tris[:, 2]]
        cr = np.cross(v1 - v0, v2 - v0)
        ln = np.linalg.norm(cr, axis=1, keepdims=True)
        return cr / np.maximum(ln, 1e-12)

    def _edge_dihedrals(self, tri_normals: np.ndarray) -> np.ndarray:
        if len(self.int_edge_t0) == 0:
            return np.zeros(0, dtype=np.float64)
        dots = np.sum(tri_normals[self.int_edge_t0] * tri_normals[self.int_edge_t1], axis=1)
        return np.degrees(np.arccos(np.clip(dots, -1.0, 1.0)))

    def _laplacian_offsets(self, pts: np.ndarray) -> np.ndarray:
        if len(self.nbr_idx) == 0:
            return np.zeros(self.nv, dtype=np.float64)
        nbr_pts = pts[self.nbr_idx]
        sums = np.add.reduceat(nbr_pts, self.nbr_offsets[:-1], axis=0)
        means = sums / self.nbr_deg[:, None]
        return np.linalg.norm(pts - means, axis=1)

    def reset_pose(self) -> None:
        for b in self.pb:
            b.matrix_basis = Matrix.Identity(4)
        if self.me.shape_keys:
            for kb in self.me.shape_keys.key_blocks:
                if kb.name != "Basis":
                    kb.value = 0.0
        bpy.context.view_layer.update()

    def apply_pose(self, bone_rotations: list[tuple[str, str, float]]) -> None:
        """Apply list of (joint_short, axis_channel ['flex'|'abduct'|'twist'], degrees) in hierarchy order."""
        self.reset_pose()
        # Group rotations by joint
        per_joint: dict[str, list[tuple[str, float]]] = {}
        for j, ch, deg in bone_rotations:
            if abs(deg) > 1e-5:
                per_joint.setdefault(j, []).append((ch, deg))

        # Sort joints shallowest to deepest so parent world transforms settle before child
        ordered = sorted(
            [j for j in per_joint if P + j in self.pb and j in self.limits["joints"]],
            key=lambda j: len(self.B[P + j].parent_recursive),
        )
        for j in ordered:
            bn = P + j
            b = self.pb[bn]
            axes_local = self.limits["joints"][j]["axes"]["local_blender"]
            q_total = Quaternion((1.0, 0.0, 0.0, 0.0))
            for ch, deg in per_joint[j]:
                ax_l = Vector(axes_local[ch]).normalized()
                q_total = q_total @ Quaternion(ax_l, math.radians(deg))
            b.rotation_mode = "QUATERNION"
            b.rotation_quaternion = q_total
            bpy.context.view_layer.update()

        # Drive R2 half-angle helpers & twist bones (exact #21 sign lock!)
        for s in ("Left", "Right"):
            # ArmTwist: -50% of Arm twist about local twist axis
            if P + s + "Arm" in self.pb and P + s + "ArmTwist" in self.pb:
                ar_q = self.pb[P + s + "Arm"].rotation_quaternion
                tw_ax = Vector(self.limits["joints"][s + "Arm"]["axes"]["local_blender"]["twist"])
                _, _, tw_deg = swing_twist_decompose(ar_q, tw_ax)
                at = self.pb[P + s + "ArmTwist"]
                at.rotation_mode = "QUATERNION"
                at.rotation_quaternion = Quaternion(tw_ax, math.radians(-0.5 * tw_deg))
            # ForeArmTwist1/2: 0 / 33 / 66 / 100% forearm twist ramp without double-stacking onto ForeArm
            if P + s + "ForeArm" in self.pb and P + s + "Hand" in self.pb:
                fa_pb = self.pb[P + s + "ForeArm"]
                ha_pb = self.pb[P + s + "Hand"]
                fa_tw_ax = Vector(self.limits["joints"][s + "ForeArm"]["axes"]["local_blender"]["twist"])
                ha_tw_ax = Vector(self.limits["joints"][s + "Hand"]["axes"]["local_blender"]["twist"])
                fa_swing, _fa_tw_q, fa_tw = swing_twist_decompose(fa_pb.rotation_quaternion, fa_tw_ax)
                _, _, ha_tw = swing_twist_decompose(ha_pb.rotation_quaternion, ha_tw_ax)
                has_weighted_twists = (
                    (P + s + "ForeArmTwist1" in self.weighted_bones)
                    or (P + s + "ForeArmTwist2" in self.weighted_bones)
                )
                if has_weighted_twists:
                    # Upgraded R2 rig: 0% elbow twist, 33/66% ForeArmTwist1/2, 100% at wrist (Hand)
                    tot_tw = fa_tw + ha_tw
                    fa_pb.rotation_mode = "QUATERNION"
                    fa_pb.rotation_quaternion = fa_swing
                    if abs(fa_tw) > 1e-5:
                        ha_pb.rotation_mode = "QUATERNION"
                        ha_pb.rotation_quaternion = ha_pb.rotation_quaternion @ Quaternion(ha_tw_ax, math.radians(fa_tw))
                    for tname, share in (("ForeArmTwist1", 0.33), ("ForeArmTwist2", 0.66)):
                        if P + s + tname in self.pb:
                            tb = self.pb[P + s + tname]
                            tb.rotation_mode = "QUATERNION"
                            tb.rotation_quaternion = Quaternion(fa_tw_ax, math.radians(share * tot_tw))
                else:
                    # Legacy unweighted rig: leave ForeArm/Hand untouched so baseline candy-wrapper registers
                    for tname, share in (("ForeArmTwist1", 0.33), ("ForeArmTwist2", 0.66)):
                        if P + s + tname in self.pb:
                            tb = self.pb[P + s + tname]
                            tb.rotation_mode = "QUATERNION"
                            tb.rotation_quaternion = Quaternion(fa_tw_ax, math.radians(share * ha_tw))
            # Half-angle helpers: 50% of child rotation
            for helper, src in (("ForeArmHelper", "ForeArm"), ("LegHelper", "Leg"), ("HandHelper", "Hand")):
                if P + s + helper in self.pb and P + s + src in self.pb:
                    hb = self.pb[P + s + helper]
                    hb.rotation_mode = "QUATERNION"
                    hb.rotation_quaternion = Quaternion((1, 0, 0, 0)).slerp(self.pb[P + s + src].rotation_quaternion, 0.5)

        bpy.context.view_layer.update()

        # Drive kalyeah_correctives shape keys when present
        if self.me.shape_keys and self.correctives_meta:
            kbs = self.me.shape_keys.key_blocks
            driven_w: dict[str, float] = {}
            for entry in self.correctives_meta:
                if not isinstance(entry, dict):
                    continue
                sh = entry.get("shape")
                if not sh or sh not in kbs:
                    continue
                kind = entry.get("kind", "hinge")
                bn = entry.get("bone")
                if kind == "combo":
                    continue
                if not bn or bn not in self.pb:
                    continue
                par = self.pb[bn].parent
                if par is None:
                    continue
                start_a = float(entry.get("startAngleDeg", entry.get("rest_angle", 0.0)))
                full_a = float(entry.get("fullAngleDeg", entry.get("full_angle", 110.0)))
                if kind == "cone" and "targetDir" in entry:
                    coord_space = entry.get("coordSpace", "gltf_yup")
                    cur_d = (self.AM @ self.pb[bn].tail - self.AM @ self.pb[bn].head).normalized()
                    td = _dir_to_blender(entry["targetDir"], coord_space)
                    if "boneRestDir" in entry:
                        rest_d = _dir_to_blender(entry["boneRestDir"], coord_space)
                    else:
                        rest_d = (self.AM @ self.B[bn].tail_local - self.AM @ self.B[bn].head_local).normalized()
                    rest_to_target = math.degrees(rest_d.angle(td))
                    cur_to_target = math.degrees(cur_d.angle(td))
                    ang = max(0.0, rest_to_target - cur_to_target)
                else:
                    a1 = (par.tail - par.head).normalized()
                    a2 = (self.pb[bn].tail - self.pb[bn].head).normalized()
                    ang = math.degrees(a1.angle(a2))
                w = max(0.0, min(1.0, (ang - start_a) / max(full_a - start_a, 1e-3)))
                kbs[sh].value = w
                driven_w[sh] = w
            for entry in self.correctives_meta:
                if isinstance(entry, dict) and entry.get("kind") == "combo":
                    sh = entry.get("shape")
                    if sh in kbs:
                        prod = 1.0
                        for drv in entry.get("drivers", []):
                            prod *= driven_w.get(drv, 0.0)
                        kbs[sh].value = prod
            bpy.context.view_layer.update()

    def eval_posed_verts(self) -> np.ndarray:
        dg = bpy.context.evaluated_depsgraph_get()
        ev = self.mesh.evaluated_get(dg)
        em = ev.to_mesh()
        buf = np.empty(len(em.vertices) * 3, dtype=np.float64)
        em.vertices.foreach_get("co", buf)
        pts = buf.reshape(-1, 3) @ self.mw_np[:3, :3].T + self.mw_np[:3, 3]
        ev.to_mesh_clear()
        return pts

    def _posed_part_seg(self, part_name: str) -> tuple[np.ndarray, np.ndarray, float, float] | None:
        if part_name == "Torso" and P + "Hips" in self.pb and P + "Spine2" in self.pb:
            return (
                np.array(self.AM @ self.pb[P + "Hips"].head, dtype=np.float64),
                np.array(self.AM @ self.pb[P + "Spine2"].tail, dtype=np.float64),
                0.08, 0.92,
            )
        if P + part_name in self.pb:
            return (
                np.array(self.AM @ self.pb[P + part_name].head, dtype=np.float64),
                np.array(self.AM @ self.pb[P + part_name].tail, dtype=np.float64),
                0.24, 0.86,
            )
        return None

    def bvh_penetration_mm(self, pts: np.ndarray, verts_a: np.ndarray, part_b_name: str,
                           tris_b: np.ndarray, ignore_rest: bool = True) -> float:
        """True interior penetration depth (mm) of part A inside part B, rejecting open-rim grazing hits."""
        if len(verts_a) == 0 or len(tris_b) == 0:
            return 0.0
        seg_b = self._posed_part_seg(part_b_name)
        bvh = BVHTree.FromPolygons([Vector(tuple(p)) for p in pts], [tuple(map(int, t)) for t in tris_b], all_triangles=True)
        # Optional rest-pose fused-seam ignore set (cached per (len(verts_a), part_b_name))
        ign: set[int] = set()
        if ignore_rest:
            ck = (id(verts_a), part_b_name)
            if ck not in self._rest_ignore_cache:
                bvh0 = BVHTree.FromPolygons([Vector(tuple(p)) for p in self.rest_co], [tuple(map(int, t)) for t in tris_b], all_triangles=True)
                s0 = set()
                for vi in verts_a:
                    p0 = Vector(tuple(self.rest_co[vi]))
                    loc0, n0, _f0, d0 = bvh0.find_nearest(p0, 0.075)
                    if loc0 is not None and n0 is not None and d0 > 1e-6 and (p0 - loc0).dot(n0) / d0 <= -0.45:
                        s0.add(int(vi))
                self._rest_ignore_cache[ck] = s0
            ign = self._rest_ignore_cache[ck]

        depths = []
        for vi in verts_a:
            if int(vi) in ign:
                continue
            p_np = pts[vi]
            if seg_b is not None:
                a_b, b_b, t_lo, t_hi = seg_b
                ab = b_b - a_b
                tb = float(((p_np - a_b) @ ab) / max(float(ab @ ab), 1e-12))
                if tb < t_lo or tb > t_hi:
                    continue
            p = Vector(tuple(p_np))
            loc, norm, _fi, dist = bvh.find_nearest(p, 0.07)
            if loc is None or norm is None or dist < 1e-6 or dist > 0.06:
                continue
            signed = (p - loc).dot(norm)
            if signed / dist <= -0.78:
                depths.append(-signed * 1000.0)
        if len(depths) < 4:
            return 0.0
        return float(np.percentile(depths, 85))

    def measure_pose(self, pose_name: str, joint_focus: str | None, channel: str | None,
                     deg: float, check_contacts: list[str] | None = None,
                     check_cloth: bool = False, check_self_isect: bool = False) -> dict:
        pts = self.eval_posed_verts()
        tri_n = self._tri_normals(pts)
        metrics: dict = {}
        failures: list[str] = []

        # 1. Integrity: seam-twin cracks + flipped faces
        seam_crack_mm = 0.0
        if len(self.twin_pairs_np) > 0:
            diffs = np.linalg.norm(pts[self.twin_pairs_np[:, 0]] - pts[self.twin_pairs_np[:, 1]], axis=1)
            seam_crack_mm = float(diffs.max() * 1000.0)
        metrics["seam_crack_mm"] = round(seam_crack_mm, 3)
        if seam_crack_mm > THRESH["seam_crack_max_mm"]:
            failures.append(f"seam_crack={seam_crack_mm:.2f}mm>{THRESH['seam_crack_max_mm']}")

        # Flipped faces vs expected dominant-bone rotation
        bone_dR = {}
        for b in self.B:
            Rp = (self.AM @ self.pb[b.name].matrix).to_3x3().normalized()
            bone_dR[b.name] = np.array(Rp @ self.rest_bone_rot[b.name].transposed(), dtype=np.float64)
        flipped = 0
        # Check faces whose vertices moved > 2mm
        disp = np.linalg.norm(pts - self.rest_co, axis=1)
        active_tris = np.where((disp[self.tris[:, 0]] > 0.002) | (disp[self.tris[:, 1]] > 0.002) | (disp[self.tris[:, 2]] > 0.002))[0]
        for ti in active_tris:
            db = self.tri_dom[ti]
            if db is None or db not in bone_dR:
                continue
            exp_n = bone_dR[db] @ self.rest_tri_normals[ti]
            if float(tri_n[ti] @ exp_n) < -0.35:
                flipped += 1
        metrics["flipped_faces"] = flipped
        if flipped > THRESH["flipped_faces_max"]:
            failures.append(f"flipped_faces={flipped}")

        # 2. Spikes: neighbor Laplacian delta vs mesh median + capsule + 2cm excursion
        posed_lap = self._laplacian_offsets(pts)
        lap_delta = np.abs(posed_lap - self.rest_lap_off)
        spike_ratio = float(lap_delta.max() / self.rest_lap_median) if len(lap_delta) else 0.0
        metrics["spike_ratio"] = round(spike_ratio, 2)
        if spike_ratio > THRESH["spike_ratio_max"]:
            failures.append(f"spike_ratio={spike_ratio:.2f}x>{THRESH['spike_ratio_max']}x")

        # Segment stretch, core rigidity, and capsule excursion
        max_stretch = 0.0
        worst_stretch_seg = None
        max_rigidity = 0.0
        worst_rigid_seg = None
        max_cap_excess_mm = 0.0
        worst_cap_seg = None
        for seg_key, info in self.seg_info.items():
            bn = P + seg_key
            a_p = np.array(self.AM @ self.pb[bn].head, dtype=np.float64)
            b_p = np.array(self.AM @ self.pb[bn].tail, dtype=np.float64)
            # Stretch between rigid-core rings
            if info["rest_ring_len"] > 1e-4:
                p_len = float(np.linalg.norm(pts[info["dist"]].mean(axis=0) - pts[info["prox"]].mean(axis=0)))
                st = abs(p_len - info["rest_ring_len"]) / info["rest_ring_len"] * 100.0
                if st > max_stretch:
                    max_stretch = st
                    worst_stretch_seg = seg_key
            # Core rigidity
            if len(info["tight"]) >= 4:
                _, d_p = point_seg_dist(pts[info["tight"]], a_p, b_p)
                rel_ch = float(np.mean(np.abs(d_p - info["rest_core_d"])) / max(float(np.mean(info["rest_core_d"])), 1e-4) * 100.0)
                if rel_ch > max_rigidity:
                    max_rigidity = rel_ch
                    worst_rigid_seg = seg_key
            # Capsule + 2cm excursion (check against max(rest_d, r_cap) + 0.02)
            _, d_all = point_seg_dist(pts[info["idx"]], a_p, b_p)
            allowed = np.maximum(info["rest_d_all"], info["r_cap"]) + 0.02
            excess = float(np.max(d_all - allowed) * 1000.0) if len(d_all) else 0.0
            if excess > max_cap_excess_mm:
                max_cap_excess_mm = excess
                worst_cap_seg = seg_key

        metrics["segment_stretch_pct"] = round(max_stretch, 2)
        metrics["segment_stretch_seg"] = worst_stretch_seg
        if max_stretch > THRESH["stretch_max_pct"]:
            failures.append(f"stretch={max_stretch:.2f}%({worst_stretch_seg})>{THRESH['stretch_max_pct']}%")

        metrics["core_rigidity_pct"] = round(max_rigidity, 2)
        metrics["core_rigidity_seg"] = worst_rigid_seg
        if max_rigidity > THRESH["core_rigidity_max_pct"]:
            failures.append(f"core_rigidity={max_rigidity:.2f}%({worst_rigid_seg})>{THRESH['core_rigidity_max_pct']}%")

        metrics["capsule_excess_mm"] = round(max(0.0, max_cap_excess_mm), 2)
        if max_cap_excess_mm > 0.0:
            failures.append(f"capsule_spike=+{max_cap_excess_mm:.1f}mm({worst_cap_seg})")

        # 3. Fold sharpness + outer volume loss on flexed joints
        if joint_focus and P + joint_focus in self.B and channel == "flex" and abs(deg) >= 35.0:
            bn = P + joint_focus
            b = self.B[bn]
            pb_b = self.pb[bn]
            j_rest = np.array(self.AM @ b.head_local, dtype=np.float64)
            j_pose = np.array(self.AM @ pb_b.head, dtype=np.float64)
            c_rest = np.array(self.AM @ b.tail_local, dtype=np.float64) - j_rest
            c_pose = np.array(self.AM @ pb_b.tail, dtype=np.float64) - j_pose
            c_rest /= max(np.linalg.norm(c_rest), 1e-6)
            c_pose /= max(np.linalg.norm(c_pose), 1e-6)
            par_b = b.parent
            if par_b is not None:
                p_rest = np.array(self.AM @ par_b.head_local, dtype=np.float64) - j_rest
                p_pose = np.array(self.AM @ self.pb[par_b.name].head, dtype=np.float64) - j_pose
                p_rest /= max(np.linalg.norm(p_rest), 1e-6)
                p_pose /= max(np.linalg.norm(p_pose), 1e-6)
            else:
                p_rest, p_pose = -c_rest, -c_pose

            # Outward bend bisector (points away from the inside of the crease!)
            inner_pose = p_pose + c_pose
            if np.linalg.norm(inner_pose) > 1e-4:
                n_outer_pose = -inner_pose / np.linalg.norm(inner_pose)
            else:
                # Near-straight: use flex axis cross child dir
                fl_w = np.array(self.limits["joints"][joint_focus]["axes"]["model_blender"]["flex"], dtype=np.float64)
                n_outer_pose = np.cross(c_pose, fl_w)
                n_outer_pose /= max(np.linalg.norm(n_outer_pose), 1e-6)
            # Corresponding rest outward direction
            fl_w = np.array(self.limits["joints"][joint_focus]["axes"]["model_blender"]["flex"], dtype=np.float64)
            n_outer_rest = np.cross(c_rest, fl_w) if deg > 0 else np.cross(fl_w, c_rest)
            if np.linalg.norm(n_outer_rest) > 1e-6:
                n_outer_rest /= np.linalg.norm(n_outer_rest)
            else:
                n_outer_rest = n_outer_pose

            r_j = self.seg_info.get(joint_focus, {}).get("r_core", 0.05)
            r_j = float(np.clip(r_j, 0.025, 0.12))

            # Fold sharpness on smooth-at-rest edges in joint outer/crease zone (excluding inner skin-contact pinch)
            if len(self.int_edge_v) > 0:
                emid = 0.5 * (pts[self.int_edge_v[:, 0]] + pts[self.int_edge_v[:, 1]])
                ed_dist = np.linalg.norm(emid - j_pose, axis=1)
                e_dir = (emid - j_pose) / np.maximum(ed_dist[:, None], 1e-6)
                # Exclude deep inner skin-to-skin contact zone (dot with outer normal < -0.45)
                in_zone = (ed_dist <= 1.35 * r_j) & (e_dir @ n_outer_pose >= -0.45) & self.smooth_at_rest_edges
                if in_zone.any():
                    dih = self._edge_dihedrals(tri_n)
                    fold_deg = float(np.percentile(dih[in_zone], 98))
                    fold_max = float(np.max(dih[in_zone]))
                    metrics["fold_p98_deg"] = round(fold_deg, 1)
                    metrics["fold_max_deg"] = round(fold_max, 1)
                    if fold_max > THRESH["fold_max_deg"]:
                        failures.append(f"fold={fold_max:.1f}deg>{THRESH['fold_max_deg']}deg")

            # Outer cross-section volume loss (major joints only; tiny finger/toe phalanges lack slab density)
            is_tiny_digit = ("Hand" in joint_focus and joint_focus not in ("LeftHand", "RightHand")) or "Toe" in joint_focus
            if not is_tiny_digit:
                bis_pose = np.cross(fl_w, n_outer_pose)
                bis_pose /= max(np.linalg.norm(bis_pose), 1e-6)
                bis_rest = c_rest
                valid_b = {bn, par_b.name if par_b else bn}
                for s in ("Left", "Right"):
                    for h in ("ForeArmHelper", "LegHelper", "HandHelper", "ArmTwist", "ForeArmTwist1", "ForeArmTwist2"):
                        if joint_focus.startswith(s):
                            valid_b.add(P + s + h)
                vmask = np.array([d in valid_b for d in self.dom_bone], dtype=bool) & (~self.is_extra_cloth)
                rel_r = self.rest_co - j_rest
                slab_r = vmask & (np.abs(rel_r @ bis_rest) <= 0.45 * r_j) & (np.linalg.norm(rel_r, axis=1) <= 1.8 * r_j)
                rel_p = pts - j_pose
                slab_p = vmask & (np.abs(rel_p @ bis_pose) <= 0.45 * r_j) & (np.linalg.norm(rel_p, axis=1) <= 1.8 * r_j)
                if int(slab_r.sum()) >= 8 and int(slab_p.sum()) >= 6:
                    a_rest = cross_section_area(self.rest_co[slab_r], j_rest, n_outer_rest, fl_w, outer_only=True)
                    a_pose = cross_section_area(pts[slab_p], j_pose, n_outer_pose, fl_w, outer_only=True)
                    if a_rest > 1e-6:
                        vol_loss = max(0.0, (a_rest - a_pose) / a_rest * 100.0)
                        has_corr = joint_focus in self.corrective_bones
                        lim_vol = THRESH["volume_loss_corr_max_pct"] if has_corr else THRESH["volume_loss_max_pct"]
                        metrics["volume_loss_pct"] = round(vol_loss, 1)
                        metrics["has_corrective"] = has_corr
                        if vol_loss > lim_vol:
                            failures.append(f"vol_loss={vol_loss:.1f}%>{lim_vol:.0f}%")

        # 4. Twist cross-section area loss (ForeArm / Arm / Leg)
        if joint_focus and channel == "twist" and abs(deg) >= 10.0:
            seg_key = joint_focus
            if seg_key.endswith("Hand"):
                seg_key = seg_key.replace("Hand", "ForeArm")
            if seg_key in self.seg_info and P + seg_key in self.pb:
                info = self.seg_info[seg_key]
                a_p = np.array(self.AM @ self.pb[P + seg_key].head, dtype=np.float64)
                b_p = np.array(self.AM @ self.pb[P + seg_key].tail, dtype=np.float64)
                ax = b_p - a_p
                ax /= max(np.linalg.norm(ax), 1e-6)
                u = np.array([1.0, 0.0, 0.0]) if abs(ax[0]) < 0.8 else np.array([0.0, 1.0, 0.0])
                u = u - (u @ ax) * ax
                u /= np.linalg.norm(u)
                v = np.cross(ax, u)
                area_p = cross_section_area(pts[info["mid"]], 0.5 * (a_p + b_p), u, v, outer_only=False)
                if info["rest_mid_area"] > 1e-6:
                    tw_loss = max(0.0, (info["rest_mid_area"] - area_p) / info["rest_mid_area"] * 100.0)
                    metrics["twist_loss_pct"] = round(tw_loss, 1)
                    if tw_loss > THRESH["twist_loss_max_pct"]:
                        failures.append(f"twist_loss={tw_loss:.1f}%>{THRESH['twist_loss_max_pct']}%")

        # 5. Self-intersection (same body part outside contact crease)
        if check_self_isect and joint_focus and P + joint_focus in self.B:
            bn = P + joint_focus
            par_bn = self.B[bn].parent.name if self.B[bn].parent else bn
            local_set = {bn, par_bn}
            tm = np.array([(d in local_set) for d in self.tri_dom], dtype=bool)
            sub_tris = self.tris[tm]
            if len(sub_tris) > 0:
                bvh = BVHTree.FromPolygons([Vector(tuple(p)) for p in pts], [tuple(map(int, t)) for t in sub_tris], all_triangles=True)
                overlaps = bvh.overlap(bvh)
                j_pose = np.array(self.AM @ self.pb[bn].head, dtype=np.float64)
                r_j = self.seg_info.get(joint_focus, {}).get("r_core", 0.05)
                non_adj = 0
                for f1, f2 in overlaps:
                    if f1 >= f2:
                        continue
                    t1, t2 = sub_tris[f1], sub_tris[f2]
                    if set(map(int, t1)).intersection(map(int, t2)):
                        continue
                    # Exclude inner crease contact zone right at the joint center if heavily flexed
                    c1 = pts[t1].mean(axis=0)
                    if abs(deg) >= 90.0 and np.linalg.norm(c1 - j_pose) < 0.75 * r_j:
                        continue
                    non_adj += 1
                metrics["self_intersections"] = non_adj
                if non_adj > THRESH["self_intersections_max"]:
                    failures.append(f"self_isect={non_adj}")

        # 6. Cloth-vs-leg penetration
        if check_cloth and len(self.cloth_verts) > 0:
            pen_l = self.bvh_penetration_mm(pts, self.cloth_verts, "LeftUpLeg", self.part_tris.get("LeftUpLeg", np.zeros((0, 3), dtype=np.int64)))
            pen_r = self.bvh_penetration_mm(pts, self.cloth_verts, "RightUpLeg", self.part_tris.get("RightUpLeg", np.zeros((0, 3), dtype=np.int64)))
            pen_ll = self.bvh_penetration_mm(pts, self.cloth_verts, "LeftLeg", self.part_tris.get("LeftLeg", np.zeros((0, 3), dtype=np.int64)))
            pen_rl = self.bvh_penetration_mm(pts, self.cloth_verts, "RightLeg", self.part_tris.get("RightLeg", np.zeros((0, 3), dtype=np.int64)))
            cloth_pen_mm = max(pen_l, pen_r, pen_ll, pen_rl)
            metrics["cloth_leg_pen_mm"] = round(cloth_pen_mm, 2)
            if cloth_pen_mm > 1.0:
                failures.append(f"cloth_vs_leg={cloth_pen_mm:.1f}mm")

        # 7. Layer gap (if multi-shell)
        if self.layer_pairs:
            max_gap_delta_mm = 0.0
            min_gap_mm = 999.0
            for vi, ti, rest_g in self.layer_pairs:
                tri = self.tris[ti]
                c = pts[tri].mean(axis=0)
                n = tri_n[ti]
                posed_g = float((pts[vi] - c) @ n)
                delta_mm = abs(posed_g - rest_g) * 1000.0
                if delta_mm > max_gap_delta_mm:
                    max_gap_delta_mm = delta_mm
                if posed_g * 1000.0 < min_gap_mm:
                    min_gap_mm = posed_g * 1000.0
            metrics["layer_gap_delta_mm"] = round(max_gap_delta_mm, 2)
            metrics["layer_min_gap_mm"] = round(min_gap_mm, 2)
            if max_gap_delta_mm > THRESH["layer_gap_delta_max_mm"] or min_gap_mm < -0.5:
                failures.append(f"layer_gap_delta={max_gap_delta_mm:.1f}mm,min={min_gap_mm:.1f}mm")

        # 8. Contact penetration between different body parts
        if check_contacts:
            contact_pairs = {
                "forearm_upperarm_L": ("LeftForeArm", "LeftArm"),
                "forearm_upperarm_R": ("RightForeArm", "RightArm"),
                "calf_thigh_L": ("LeftLeg", "LeftUpLeg"),
                "calf_thigh_R": ("RightLeg", "RightUpLeg"),
                "arm_torso_L": ("LeftArm", "Torso"),
                "arm_torso_R": ("RightArm", "Torso"),
                "forearm_torso_L": ("LeftForeArm", "Torso"),
                "forearm_torso_R": ("RightForeArm", "Torso"),
                "thigh_thigh": ("LeftUpLeg", "RightUpLeg"),
                "thigh_torso_L": ("LeftUpLeg", "Torso"),
                "thigh_torso_R": ("RightUpLeg", "Torso"),
                "hand_thigh_L": ("LeftHand", "LeftUpLeg"),
                "hand_thigh_R": ("RightHand", "RightUpLeg"),
            }
            contacts_out = {}
            worst_pen = 0.0
            for cname in check_contacts:
                pa, pb_name = contact_pairs[cname]
                is_rest_pose = (pose_name == "Contact_Rest_APose")
                pen = max(
                    self.bvh_penetration_mm(pts, self.part_verts.get(pa, np.zeros(0, dtype=np.int64)), pb_name, self.part_tris.get(pb_name, np.zeros((0, 3), dtype=np.int64)), ignore_rest=not is_rest_pose),
                    self.bvh_penetration_mm(pts, self.part_verts.get(pb_name, np.zeros(0, dtype=np.int64)), pa, self.part_tris.get(pa, np.zeros((0, 3), dtype=np.int64)), ignore_rest=not is_rest_pose),
                )
                contacts_out[cname] = round(pen, 2)
                if pen > worst_pen:
                    worst_pen = pen
                if pen > THRESH["contact_pen_max_mm"]:
                    failures.append(f"contact({cname})={pen:.1f}mm>{THRESH['contact_pen_max_mm']}mm")
            metrics["contacts_mm"] = contacts_out
            metrics["contact_max_mm"] = round(worst_pen, 2)

        return {
            "pose": pose_name,
            "joint": joint_focus,
            "channel": channel,
            "deg": round(deg, 1),
            "pass": len(failures) == 0,
            "failures": failures,
            "metrics": metrics,
        }

    def run_suite(self, include_fingers: bool = True, render_dir: Path | None = None, max_renders: int = 4) -> dict:
        pose_results: list[dict] = []
        render_candidates: list[tuple[int, str, list[tuple[str, str, float]], str]] = []

        # 1. Single-axis limit + half-range sweep for every joint
        joint_list = PRIMARY_ROM_JOINTS + (FINGER_ROM_JOINTS if include_fingers else [])
        for j in joint_list:
            if P + j not in self.B or j not in self.limits["joints"]:
                continue
            lim = effective_limits(self.limits, self.kid, j)
            is_finger = "Hand" in j and j not in ("LeftHand", "RightHand")
            for ch in ("flex", "abduct", "twist"):
                lo, hi = lim[ch]
                if abs(lo) < 1e-3 and abs(hi) < 1e-3:
                    continue
                # Full limits + half range (for non-finger joints; fingers test limits + half flex)
                angles = []
                if hi > 1e-3:
                    angles.append(("max", hi))
                    if not is_finger or ch == "flex":
                        angles.append(("half_max", 0.5 * hi))
                if lo < -1e-3:
                    angles.append(("min", lo))
                    if not is_finger:
                        angles.append(("half_min", 0.5 * lo))
                for tag, deg in angles:
                    rots = [(j, ch, deg)]
                    # R2: Knee twist only allowed while flexed > 20 deg
                    if ch == "twist" and lim["twistRequiresFlexDeg"] > 0.0:
                        rots.insert(0, (j, "flex", max(45.0, lim["twistRequiresFlexDeg"] + 15.0)))
                    self.apply_pose(rots)
                    chk_cloth = j.endswith("UpLeg") or j.endswith("Leg")
                    chk_self = (not is_finger) and tag in ("max", "min")
                    chk_contacts = None
                    if tag in ("max", "min"):
                        if j == "LeftForeArm" and ch == "flex" and deg > 0:
                            chk_contacts = ["forearm_upperarm_L"]
                        elif j == "RightForeArm" and ch == "flex" and deg > 0:
                            chk_contacts = ["forearm_upperarm_R"]
                        elif j == "LeftLeg" and ch == "flex" and deg > 0:
                            chk_contacts = ["calf_thigh_L"]
                        elif j == "RightLeg" and ch == "flex" and deg > 0:
                            chk_contacts = ["calf_thigh_R"]
                        elif j == "LeftArm" and ch == "abduct" and deg < 0:
                            chk_contacts = ["arm_torso_L", "forearm_torso_L"]
                        elif j == "RightArm" and ch == "abduct" and deg < 0:
                            chk_contacts = ["arm_torso_R", "forearm_torso_R"]
                        elif j == "LeftUpLeg" and ch == "flex" and deg > 0:
                            chk_contacts = ["thigh_torso_L"]
                        elif j == "RightUpLeg" and ch == "flex" and deg > 0:
                            chk_contacts = ["thigh_torso_R"]
                        elif j == "LeftUpLeg" and ch == "abduct" and deg < 0:
                            chk_contacts = ["thigh_thigh"]
                        elif j == "RightUpLeg" and ch == "abduct" and deg < 0:
                            chk_contacts = ["thigh_thigh"]
                    res = self.measure_pose(
                        f"{j}_{ch}_{tag}", j, ch, deg,
                        check_contacts=chk_contacts,
                        check_cloth=chk_cloth and tag in ("max", "min"),
                        check_self_isect=chk_self,
                    )
                    pose_results.append(res)
                    if not res["pass"] and not is_finger and tag in ("max", "min"):
                        render_candidates.append((len(res["failures"]), res["pose"], rots, j))

        # 2. Combined limb poses
        combos = [
            ("Combo_LeftArm", "LeftForeArm", [
                ("LeftArm", "flex", 85.0), ("LeftArm", "abduct", 55.0), ("LeftArm", "twist", 30.0),
                ("LeftForeArm", "flex", min(115.0, effective_limits(self.limits, self.kid, "LeftForeArm")["flex"][1])),
                ("LeftForeArm", "twist", 45.0), ("LeftHand", "flex", 45.0),
            ], ["forearm_upperarm_L", "arm_torso_L"], False),
            ("Combo_RightArm", "RightForeArm", [
                ("RightArm", "flex", 85.0), ("RightArm", "abduct", 55.0), ("RightArm", "twist", 30.0),
                ("RightForeArm", "flex", min(115.0, effective_limits(self.limits, self.kid, "RightForeArm")["flex"][1])),
                ("RightForeArm", "twist", 45.0), ("RightHand", "flex", 45.0),
            ], ["forearm_upperarm_R", "arm_torso_R"], False),
            ("Combo_LeftLeg", "LeftLeg", [
                ("LeftUpLeg", "flex", min(90.0, effective_limits(self.limits, self.kid, "LeftUpLeg")["flex"][1])),
                ("LeftUpLeg", "abduct", 20.0),
                ("LeftLeg", "flex", min(115.0, effective_limits(self.limits, self.kid, "LeftLeg")["flex"][1])),
                ("LeftFoot", "flex", -15.0),
            ], ["calf_thigh_L", "thigh_torso_L"], True),
            ("Combo_RightLeg", "RightLeg", [
                ("RightUpLeg", "flex", min(90.0, effective_limits(self.limits, self.kid, "RightUpLeg")["flex"][1])),
                ("RightUpLeg", "abduct", 20.0),
                ("RightLeg", "flex", min(115.0, effective_limits(self.limits, self.kid, "RightLeg")["flex"][1])),
                ("RightFoot", "flex", -15.0),
            ], ["calf_thigh_R", "thigh_torso_R"], True),
        ]
        for cname, focus, rots, contacts, chk_cloth in combos:
            self.apply_pose(rots)
            res = self.measure_pose(cname, focus, "flex", 115.0, check_contacts=contacts, check_cloth=chk_cloth, check_self_isect=True)
            pose_results.append(res)
            if not res["pass"]:
                render_candidates.append((len(res["failures"]) + 1, cname, rots, focus))

        # 3. Dedicated R4 Contact poses
        la_abd = effective_limits(self.limits, self.kid, "LeftArm")["abduct"][0]
        ra_abd = effective_limits(self.limits, self.kid, "RightArm")["abduct"][0]
        lu_flx = effective_limits(self.limits, self.kid, "LeftUpLeg")["flex"][1]
        ru_flx = effective_limits(self.limits, self.kid, "RightUpLeg")["flex"][1]
        lu_abd = effective_limits(self.limits, self.kid, "LeftUpLeg")["abduct"][0]
        ru_abd = effective_limits(self.limits, self.kid, "RightUpLeg")["abduct"][0]
        contact_poses = [
            ("Contact_Rest_APose", None, [], ["arm_torso_L", "arm_torso_R", "thigh_thigh"], False),
            ("Contact_ArmsDown", "LeftArm", [("LeftArm", "abduct", la_abd), ("RightArm", "abduct", ra_abd)],
             ["arm_torso_L", "arm_torso_R", "forearm_torso_L", "forearm_torso_R"], False),
            ("Contact_HandOnThigh", "LeftHand", [
                ("LeftArm", "abduct", la_abd), ("LeftForeArm", "flex", 20.0),
                ("RightArm", "abduct", ra_abd), ("RightForeArm", "flex", 20.0),
            ], ["hand_thigh_L", "hand_thigh_R", "forearm_torso_L", "forearm_torso_R"], False),
            ("Contact_FeetTogether", "LeftUpLeg", [
                ("LeftUpLeg", "abduct", max(lu_abd, -12.0)), ("RightUpLeg", "abduct", max(ru_abd, -12.0)),
            ], ["thigh_thigh"], True),
            ("Contact_BothLegsFullFlex", "LeftUpLeg", [
                ("LeftUpLeg", "flex", lu_flx), ("RightUpLeg", "flex", ru_flx),
                ("LeftLeg", "flex", 90.0), ("RightLeg", "flex", 90.0),
            ], ["thigh_torso_L", "thigh_torso_R", "thigh_thigh"], True),
        ]
        for cname, focus, rots, contacts, chk_cloth in contact_poses:
            self.apply_pose(rots)
            res = self.measure_pose(cname, focus, None, 0.0, check_contacts=contacts, check_cloth=chk_cloth, check_self_isect=False)
            pose_results.append(res)

        # Optional diagnostic renders of top failing joints
        rendered_files = []
        if render_dir is not None and render_candidates:
            seen_joints: set[str] = set()
            render_candidates.sort(key=lambda x: -x[0])
            for _sev, pname, rots, jfocus in render_candidates:
                if jfocus in seen_joints:
                    continue
                seen_joints.add(jfocus)
                self.apply_pose(rots)
                fpos = self.AM @ self.pb[P + jfocus].head
                rad = self.seg_info.get(jfocus, {}).get("r_cap", 0.06)
                out_pref = render_dir / f"{self.kid}_{pname}"
                rendered_files.extend(render_closeup(self.mesh, self.arm, fpos, rad, out_pref))
                if len(seen_joints) >= max_renders:
                    break

        self.reset_pose()

        # Aggregate worst numbers across all poses
        def worst(key: str, default: float = 0.0) -> float:
            vals = [r["metrics"][key] for r in pose_results if key in r["metrics"]]
            return max(vals) if vals else default

        failed_poses = [r for r in pose_results if not r["pass"]]
        summary = {
            "poses_tested": len(pose_results),
            "poses_passed": len(pose_results) - len(failed_poses),
            "poses_failed": len(failed_poses),
            "pass": len(failed_poses) == 0,
            "num_shell_components": self.num_components,
            "layer_pairs_checked": len(self.layer_pairs),
            "corrective_bones": sorted(self.corrective_bones),
            "worst": {
                "fold_max_deg": round(worst("fold_max_deg"), 1),
                "segment_stretch_pct": round(worst("segment_stretch_pct"), 2),
                "core_rigidity_pct": round(worst("core_rigidity_pct"), 2),
                "volume_loss_pct": round(worst("volume_loss_pct"), 1),
                "twist_loss_pct": round(worst("twist_loss_pct"), 1),
                "self_intersections": int(worst("self_intersections")),
                "cloth_leg_pen_mm": round(worst("cloth_leg_pen_mm"), 2),
                "seam_crack_mm": round(worst("seam_crack_mm"), 3),
                "flipped_faces": int(worst("flipped_faces")),
                "spike_ratio": round(worst("spike_ratio"), 2),
                "capsule_excess_mm": round(worst("capsule_excess_mm"), 2),
                "layer_gap_delta_mm": round(worst("layer_gap_delta_mm"), 2),
                "contact_max_mm": round(worst("contact_max_mm"), 2),
            },
            "failing_poses": [
                {"pose": r["pose"], "failures": r["failures"]} for r in failed_poses
            ],
            "rendered_files": rendered_files,
        }
        return {
            "kid": self.kid,
            "file": str(self.glb_path),
            "summary": summary,
            "poses": pose_results,
        }


def format_table(reports: list[dict]) -> str:
    hdr = (
        f"{'Kid':<9} {'Gate':>4} {'Fail/Tot':>8} {'Fold°':>6} {'Str%':>5} {'Rig%':>5} "
        f"{'Vol%':>5} {'Tw%':>5} {'SI':>4} {'Clth':>5} {'Crk':>5} {'Flp':>4} {'Spk':>5} {'Cap':>5} {'Pen':>5}"
    )
    lines = [hdr, "-" * len(hdr)]
    for rep in reports:
        s = rep["summary"]
        w = s["worst"]
        gate = "PASS" if s["pass"] else "FAIL"
        ft = f"{s['poses_failed']}/{s['poses_tested']}"
        lines.append(
            f"{rep['kid']:<9} {gate:>4} {ft:>8} {w['fold_max_deg']:6.1f} {w['segment_stretch_pct']:5.2f} "
            f"{w['core_rigidity_pct']:5.2f} {w['volume_loss_pct']:5.1f} {w['twist_loss_pct']:5.1f} "
            f"{w['self_intersections']:4d} {w['cloth_leg_pen_mm']:5.1f} {w['seam_crack_mm']:5.2f} "
            f"{w['flipped_faces']:4d} {w['spike_ratio']:5.1f} {w['capsule_excess_mm']:5.1f} {w['contact_max_mm']:5.1f}"
        )
    return "\n".join(lines)


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser(description="R4 measured ROM QA gate for Kalyeah character .glb files")
    ap.add_argument("files", nargs="*", help="One or more .glb files to evaluate")
    ap.add_argument("--kids", default=None, help="Comma-separated kid ids in assets/models/characters/")
    ap.add_argument("--kid", default=None, help="Override kid id for per-kid limits lookup (single-file mode)")
    ap.add_argument("--limits", default=None, help="Path to unity/Data/rig__joint_limits.json")
    ap.add_argument("--no-fingers", action="store_true", help="Skip 30 finger joints (faster core-body sweep)")
    ap.add_argument("--render", default=None, help="Output directory for diagnostic textured + grey/red closeups")
    ap.add_argument("--max-renders", type=int, default=3, help="Max problem joints rendered per kid (default 3)")
    ap.add_argument("--out", default=None, help="Write full JSON report to this path")
    ap.add_argument("--json", action="store_true", help="Print JSON report to stdout")
    return ap.parse_args(argv)


def main() -> int:
    args = parse_args()
    root = repo_root()
    limits_path = Path(args.limits) if args.limits else (root / "unity" / "Data" / "rig__joint_limits.json")
    limits_data = load_limits(limits_path)

    targets: list[tuple[Path, str]] = []
    for f in args.files:
        p = Path(f)
        if not p.is_absolute():
            p = (Path.cwd() / p).resolve()
        kid = args.kid or p.stem.lower()
        targets.append((p, kid))
    if args.kids:
        for k in [x.strip().lower() for x in args.kids.split(",") if x.strip()]:
            targets.append((root / "assets" / "models" / "characters" / f"{k}.glb", k))
    if not targets:
        print("No input .glb files specified (pass file paths or --kids narda,karding,...)", file=sys.stderr)
        return 2

    render_dir = Path(args.render).resolve() if args.render else None
    reports = []
    for glb, kid in targets:
        ev = RomEvaluator(glb, kid, limits_data)
        reports.append(ev.run_suite(include_fingers=not args.no_fingers, render_dir=render_dir, max_renders=args.max_renders))

    out_doc = {
        "schemaVersion": 1,
        "limitsFile": str(limits_path),
        "thresholds": THRESH,
        "allPass": all(r["summary"]["pass"] for r in reports),
        "kids": reports,
    }
    if args.out:
        out_p = Path(args.out)
        out_p.parent.mkdir(parents=True, exist_ok=True)
        out_p.write_text(json.dumps(out_doc, indent=2) + "\n", encoding="utf-8", newline="\n")

    if args.json:
        print(json.dumps(out_doc, indent=2))
    else:
        print(format_table(reports))
        for r in reports:
            fp = r["summary"]["failing_poses"]
            if fp:
                sample = "; ".join(f"{x['pose']}[{','.join(x['failures'][:3])}]" for x in fp[:6])
                more = f" (+{len(fp) - 6} more)" if len(fp) > 6 else ""
                print(f"  {r['kid']} top fails: {sample}{more}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
