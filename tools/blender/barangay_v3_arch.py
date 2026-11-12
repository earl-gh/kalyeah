"""Unified Deadlock-Grade Architectural Massing, Recessed Bay Facade Builder,
Asymmetric Front/Side/Rear Anatomy, True BVH Raycast Vertex AO, and Normal-Mapped
External-Atlas GLB Exporter for Kalyeah (Blender 5.2+).
"""

from __future__ import annotations

import hashlib
import json
import math
import struct
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Dict, List, Optional, Sequence, Tuple

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree


@dataclass
class Opening:
    """Rectangular architectural opening recessed inward into a wall bay.

    `u0, u1`: horizontal distance (m) measured along wall from `p0` to `p1`.
    `z0, z1`: vertical elevation (m) in world Z.
    `depth`: inward reveal depth (m) (`0.16..0.85` m).
    `kind`: `'capiz_rehas' | 'capiz' | 'jalousie_rehas' | 'jalousie' | 'door_front' |
             'door_rear' | 'bath_vent' | 'store_sari' | 'store_karinderya' |
             'store_panaderya' | 'store_barber' | 'store_vulcanizing' |
             'silong_open' | 'chapel_portico'`.
    """

    u0: float
    u1: float
    z0: float
    z1: float
    depth: float = 0.20
    kind: str = "capiz_rehas"
    swatch: str = "capiz_window"
    trim_swatch: str = "concrete_pillar"


class MeshBuilder:
    """Clean single-surface BMesh builder with safe padded atlas UV projection."""

    def __init__(self, uv_rects: Dict[str, Tuple[float, float, float, float]]) -> None:
        self.bm = bmesh.new()
        self.uv_layer = self.bm.loops.layers.uv.new("UVMap")
        self.custom_nrm_layer = self.bm.verts.layers.float_vector.new("custom_nrm")
        self.has_custom_nrm_layer = self.bm.verts.layers.int.new("has_custom_nrm")
        self.custom_ao_layer = self.bm.verts.layers.float.new("custom_ao")
        self.uv_rects = uv_rects
        self._fallback_key = next(iter(uv_rects.keys()))
        self.weatherhead_sockets: List[Tuple[float, float, float]] = []

    def rect_uv(self, swatch: str) -> Tuple[float, float, float, float]:
        return self.uv_rects.get(swatch, self.uv_rects[self._fallback_key])

    def _assign_uv(
        self,
        face: bmesh.types.BMFace,
        uvs_local: Sequence[Tuple[float, float]],
        swatch: str,
    ) -> None:
        u0, v0, u1, v1 = self.rect_uv(swatch)
        du = u1 - u0
        dv = v1 - v0
        for loop, (lu, lv) in zip(face.loops, uvs_local):
            cu = max(0.0, min(1.0, float(lu)))
            cv = max(0.0, min(1.0, float(lv)))
            loop[self.uv_layer].uv = (u0 + cu * du, v0 + cv * dv)

    def quad(
        self,
        p0: Tuple[float, float, float] | Vector,
        p1: Tuple[float, float, float] | Vector,
        p2: Tuple[float, float, float] | Vector,
        p3: Tuple[float, float, float] | Vector,
        swatch: str,
        uv_box: Tuple[float, float, float, float] = (0.0, 0.0, 1.0, 1.0),
        smooth: bool = False,
    ) -> Optional[bmesh.types.BMFace]:
        v0 = self.bm.verts.new(p0)
        v1 = self.bm.verts.new(p1)
        v2 = self.bm.verts.new(p2)
        v3 = self.bm.verts.new(p3)
        if (v1.co - v0.co).length_squared < 1e-9 or (v3.co - v0.co).length_squared < 1e-9:
            return None
        try:
            f = self.bm.faces.new((v0, v1, v2, v3))
        except ValueError:
            return None
        f.smooth = smooth
        su0, sv0, su1, sv1 = uv_box
        self._assign_uv(f, [(su0, sv0), (su1, sv0), (su1, sv1), (su0, sv1)], swatch)
        return f

    def tri(
        self,
        p0: Tuple[float, float, float] | Vector,
        p1: Tuple[float, float, float] | Vector,
        p2: Tuple[float, float, float] | Vector,
        swatch: str,
        uvs: Sequence[Tuple[float, float]] = ((0.0, 0.0), (1.0, 0.0), (0.5, 1.0)),
        smooth: bool = False,
    ) -> Optional[bmesh.types.BMFace]:
        v0 = self.bm.verts.new(p0)
        v1 = self.bm.verts.new(p1)
        v2 = self.bm.verts.new(p2)
        if ((v1.co - v0.co).cross(v2.co - v0.co)).length_squared < 1e-10:
            return None
        try:
            f = self.bm.faces.new((v0, v1, v2))
        except ValueError:
            return None
        f.smooth = smooth
        self._assign_uv(f, uvs, swatch)
        return f

    def box(
        self,
        cx: float,
        cy: float,
        z0: float,
        z1: float,
        sx: float,
        sy: float,
        swatch_side: str,
        swatch_top: Optional[str] = None,
        swatch_bot: Optional[str] = None,
        omit_bottom: bool = False,
        omit_top: bool = False,
        yaw: float = 0.0,
    ) -> None:
        """Adds an outward-normal oriented box with zero internal geometry."""
        st = swatch_top or swatch_side
        sb = swatch_bot or swatch_side
        hx, hy = sx * 0.5, sy * 0.5
        cs, sn = math.cos(yaw), math.sin(yaw)

        def tr(lx: float, ly: float, lz: float) -> Vector:
            return Vector((cx + lx * cs - ly * sn, cy + lx * sn + ly * cs, lz))

        b0 = tr(-hx, -hy, z0)
        b1 = tr(+hx, -hy, z0)
        b2 = tr(+hx, +hy, z0)
        b3 = tr(-hx, +hy, z0)
        t0 = tr(-hx, -hy, z1)
        t1 = tr(+hx, -hy, z1)
        t2 = tr(+hx, +hy, z1)
        t3 = tr(-hx, +hy, z1)

        self.quad(b0, b1, t1, t0, swatch_side)  # -Y local (front)
        self.quad(b1, b2, t2, t1, swatch_side)  # +X local (right)
        self.quad(b2, b3, t3, t2, swatch_side)  # +Y local (rear)
        self.quad(b3, b0, t0, t3, swatch_side)  # -X local (left)
        if not omit_top:
            self.quad(t0, t1, t2, t3, st)
        if not omit_bottom:
            self.quad(b3, b2, b1, b0, sb)

    def frustum_plinth(
        self,
        cx: float,
        cy: float,
        z0: float,
        z_step: float,
        z1: float,
        w_bot: float,
        d_bot: float,
        w_top: float,
        d_top: float,
        swatch_base: str = "concrete_moss",
        swatch_bevel: str = "concrete_pillar",
    ) -> None:
        """Grounded concrete plinth with vertical damp base + beveled water-table chamfer."""
        hb_x, hb_y = w_bot * 0.5, d_bot * 0.5
        ht_x, ht_y = w_top * 0.5, d_top * 0.5

        b = [
            Vector((cx - hb_x, cy - hb_y, z0)),
            Vector((cx + hb_x, cy - hb_y, z0)),
            Vector((cx + hb_x, cy + hb_y, z0)),
            Vector((cx - hb_x, cy + hb_y, z0)),
        ]
        m = [
            Vector((cx - hb_x, cy - hb_y, z_step)),
            Vector((cx + hb_x, cy - hb_y, z_step)),
            Vector((cx + hb_x, cy + hb_y, z_step)),
            Vector((cx - hb_x, cy + hb_y, z_step)),
        ]
        t = [
            Vector((cx - ht_x, cy - ht_y, z1)),
            Vector((cx + ht_x, cy - ht_y, z1)),
            Vector((cx + ht_x, cy + ht_y, z1)),
            Vector((cx - ht_x, cy + ht_y, z1)),
        ]
        for i in range(4):
            j = (i + 1) % 4
            self.quad(b[i], b[j], m[j], m[i], swatch_base, (0.0, 0.0, 1.0, 0.45))
            self.quad(m[i], m[j], t[j], t[i], swatch_bevel)
        self.quad(t[0], t[1], t[2], t[3], swatch_bevel)
        self.quad(b[3], b[2], b[1], b[0], swatch_base)

    def cornice_ring(
        self,
        cx: float,
        cy: float,
        z0: float,
        h: float,
        w_wall: float,
        d_wall: float,
        proj: float = 0.14,
        swatch: str = "concrete_pillar",
    ) -> None:
        """Deadlock-style molded horizontal belt/eaves cornice with chamfered soffit & sloped drip top."""
        z_mid1 = z0 + h * 0.34
        z_mid2 = z0 + h * 0.80
        z_top = z0 + h
        hw0, hd0 = w_wall * 0.5, d_wall * 0.5
        hw1, hd1 = hw0 + proj, hd0 + proj
        hw2, hd2 = hw0 + proj * 0.25, hd0 + proj * 0.25

        def ring(hx: float, hy: float, z: float) -> List[Vector]:
            return [
                Vector((cx - hx, cy - hy, z)),
                Vector((cx + hx, cy - hy, z)),
                Vector((cx + hx, cy + hy, z)),
                Vector((cx - hx, cy + hy, z)),
            ]

        r0 = ring(hw0, hd0, z0)
        r1 = ring(hw1, hd1, z_mid1)
        r2 = ring(hw1, hd1, z_mid2)
        r3 = ring(hw2, hd2, z_top)
        for i in range(4):
            j = (i + 1) % 4
            self.quad(r0[i], r0[j], r1[j], r1[i], swatch, (0.0, 0.0, 1.0, 0.3))
            self.quad(r1[i], r1[j], r2[j], r2[i], swatch, (0.0, 0.3, 1.0, 0.8))
            self.quad(r2[i], r2[j], r3[j], r3[i], swatch, (0.0, 0.8, 1.0, 1.0))
        self.quad(r3[0], r3[1], r3[2], r3[3], swatch)

    def tube(
        self,
        p0: Tuple[float, float, float] | Vector,
        p1: Tuple[float, float, float] | Vector,
        r0: float,
        r1: float,
        sides: int = 8,
        swatch: str = "zinc_brushed",
        cap_start: bool = True,
        cap_end: bool = True,
    ) -> None:
        """Clean tapered cylinder/pipe along arbitrary 3D axis `p0 -> p1`."""
        v_start = Vector(p0)
        v_end = Vector(p1)
        axis = v_end - v_start
        length = axis.length
        if length < 1e-5:
            return
        z_dir = axis / length
        up_ref = Vector((0.0, 0.0, 1.0)) if abs(z_dir.z) < 0.92 else Vector((1.0, 0.0, 0.0))
        x_dir = up_ref.cross(z_dir).normalized()
        y_dir = z_dir.cross(x_dir).normalized()

        ring0: List[Vector] = []
        ring1: List[Vector] = []
        for i in range(sides):
            ang = (2.0 * math.pi * i) / sides
            cs, sn = math.cos(ang), math.sin(ang)
            radial = x_dir * cs + y_dir * sn
            ring0.append(v_start + radial * r0)
            ring1.append(v_end + radial * r1)

        use_smooth = sides >= 7
        for i in range(sides):
            j = (i + 1) % sides
            u_a = i / sides
            u_b = (i + 1) / sides
            self.quad(ring0[i], ring0[j], ring1[j], ring1[i], swatch, (u_a, 0.0, u_b, 1.0), smooth=use_smooth)

        if cap_start and r0 > 1e-4:
            for i in range(1, sides - 1):
                self.tri(ring0[0], ring0[i + 1], ring0[i], swatch)
        if cap_end and r1 > 1e-4:
            for i in range(1, sides - 1):
                self.tri(ring1[0], ring1[i], ring1[i + 1], swatch)


# ---------------------------------------------------------------------------
# Parametric Recessed-Bay Facade & Storefront Wall Builder
# ---------------------------------------------------------------------------

def add_recessed_bay_wall(
    mb: MeshBuilder,
    p0: Tuple[float, float],
    p1: Tuple[float, float],
    z0: float,
    z1: float,
    wall_swatch: str,
    openings: Sequence[Opening] = (),
    pilaster_ends: bool = True,
    pilaster_swatch: str = "concrete_pillar",
) -> None:
    """Builds an articulated exterior wall from `p0=(x0,y0)` to `p1=(x1,y1)` (CCW footprint order
    so `(dy, -dx)` points strictly outward!) with real recessed 3D window/door/storefront pockets."""
    dx = p1[0] - p0[0]
    dy = p1[1] - p0[1]
    L = math.hypot(dx, dy)
    if L < 1e-4 or z1 <= z0 + 1e-4:
        return
    tang = Vector((dx / L, dy / L, 0.0))
    out_n = Vector((dy / L, -dx / L, 0.0))
    origin = Vector((p0[0], p0[1], 0.0))

    def wp(u: float, z: float, out_off: float = 0.0) -> Vector:
        return origin + tang * u + out_n * out_off + Vector((0.0, 0.0, z))

    # Clamp openings strictly inside wall bounds
    valid_ops: List[Opening] = []
    for op in openings:
        u_a = max(0.08, min(L - 0.08, op.u0))
        u_b = max(0.08, min(L - 0.08, op.u1))
        z_a = max(z0, min(z1 - 0.08, op.z0))
        z_b = max(z0 + 0.08, min(z1 - 0.05, op.z1))
        if u_b - u_a > 0.12 and z_b - z_a > 0.12:
            valid_ops.append(
                Opening(u_a, u_b, z_a, z_b, op.depth, op.kind, op.swatch, op.trim_swatch)
            )

    us = sorted({0.0, L} | {op.u0 for op in valid_ops} | {op.u1 for op in valid_ops})
    zs = sorted({z0, z1} | {op.z0 for op in valid_ops} | {op.z1 for op in valid_ops})
    H = z1 - z0

    # 1. Emit non-overlapping exterior masonry quads around openings
    for iu in range(len(us) - 1):
        ua, ub = us[iu], us[iu + 1]
        if ub - ua < 1e-4:
            continue
        um = 0.5 * (ua + ub)
        for iz in range(len(zs) - 1):
            za, zb = zs[iz], zs[iz + 1]
            if zb - za < 1e-4:
                continue
            zm = 0.5 * (za + zb)
            inside_any = any(
                (op.u0 - 1e-4 <= um <= op.u1 + 1e-4) and (op.z0 - 1e-4 <= zm <= op.z1 + 1e-4)
                for op in valid_ops
            )
            if not inside_any:
                # Tile UV cleanly across wall height/width without crossing swatch borders
                va_uv = max(0.0, min(1.0, (za - z0) / max(H, 0.5)))
                vb_uv = max(0.0, min(1.0, (zb - z0) / max(H, 0.5)))
                ua_uv = (ua % 3.2) / 3.2
                ub_uv = ua_uv + min((ub - ua) / 3.2, 1.0 - ua_uv)
                mb.quad(
                    wp(ua, za, 0.0),
                    wp(ub, za, 0.0),
                    wp(ub, zb, 0.0),
                    wp(ua, zb, 0.0),
                    wall_swatch,
                    (ua_uv, va_uv, ub_uv, vb_uv),
                )

    # 2. Corner pilasters for Deadlock-style crisp vertical framing
    if pilaster_ends and L >= 2.2:
        pw = min(0.24, L * 0.08)
        proj = 0.055
        for u_start in (0.0, L - pw):
            u_end = u_start + pw
            a0, a1 = wp(u_start, z0, 0.0), wp(u_end, z0, 0.0)
            b0, b1 = wp(u_start, z0, proj), wp(u_end, z0, proj)
            a0t, a1t = wp(u_start, z1, 0.0), wp(u_end, z1, 0.0)
            b0t, b1t = wp(u_start, z1, proj), wp(u_end, z1, proj)
            mb.quad(b0, b1, b1t, b0t, pilaster_swatch)
            mb.quad(a0, b0, b0t, a0t, pilaster_swatch, (0.0, 0.0, 0.25, 1.0))
            mb.quad(b1, a1, a1t, b1t, pilaster_swatch, (0.75, 0.0, 1.0, 1.0))

    # 3. Real 3D inward reveals + architectural infill per opening
    for op in valid_ops:
        ua, ub, za, zb, dep = op.u0, op.u1, op.z0, op.z1, op.depth
        # Outer loop & recessed inner loop
        o_bl, o_br = wp(ua, za, 0.0), wp(ub, za, 0.0)
        o_tr, o_tl = wp(ub, zb, 0.0), wp(ua, zb, 0.0)
        i_bl, i_br = wp(ua, za, -dep), wp(ub, za, -dep)
        i_tr, i_tl = wp(ub, zb, -dep), wp(ua, zb, -dep)

        # Left jamb (+tang normal), Right jamb (-tang normal), Head soffit (-Z normal), Sill (+Z normal)
        mb.quad(o_bl, i_bl, i_tl, o_tl, op.trim_swatch, (0.0, 0.0, 0.35, 1.0))
        mb.quad(i_br, o_br, o_tr, i_tr, op.trim_swatch, (0.65, 0.0, 1.0, 1.0))
        mb.quad(o_tl, i_tl, i_tr, o_tr, op.trim_swatch, (0.0, 0.65, 1.0, 1.0))
        if za > z0 + 0.03:
            mb.quad(i_bl, o_bl, o_br, i_br, op.trim_swatch, (0.0, 0.0, 1.0, 0.35))

        _populate_opening_detail(mb, wp, tang, out_n, op, z0)


def _add_local_box(
    mb: MeshBuilder,
    wp_fn,
    u_mid: float,
    z_lo: float,
    z_hi: float,
    out_mid: float,
    w_u: float,
    d_out: float,
    swatch_side: str,
    swatch_top: Optional[str] = None,
    omit_bottom: bool = False,
) -> None:
    """Helper box aligned to the local `(u, z, out)` frame of a wall bay."""
    st = swatch_top or swatch_side
    hu, hd = w_u * 0.5, d_out * 0.5
    b0 = wp_fn(u_mid - hu, z_lo, out_mid + hd)  # front-left (outward)
    b1 = wp_fn(u_mid + hu, z_lo, out_mid + hd)  # front-right
    b2 = wp_fn(u_mid + hu, z_lo, out_mid - hd)  # back-right (inward)
    b3 = wp_fn(u_mid - hu, z_lo, out_mid - hd)  # back-left
    t0 = wp_fn(u_mid - hu, z_hi, out_mid + hd)
    t1 = wp_fn(u_mid + hu, z_hi, out_mid + hd)
    t2 = wp_fn(u_mid + hu, z_hi, out_mid - hd)
    t3 = wp_fn(u_mid - hu, z_hi, out_mid - hd)
    mb.quad(b0, b1, t1, t0, swatch_side)
    mb.quad(b1, b2, t2, t1, swatch_side)
    mb.quad(b2, b3, t3, t2, swatch_side)
    mb.quad(b3, b0, t0, t3, swatch_side)
    mb.quad(t0, t1, t2, t3, st)
    if not omit_bottom:
        mb.quad(b3, b2, b1, b0, st)


def _populate_opening_detail(mb: MeshBuilder, wp, tang: Vector, out_n: Vector, op: Opening, wall_z0: float) -> None:
    ua, ub, za, zb, dep = op.u0, op.u1, op.z0, op.z1, op.depth
    w_op = ub - ua
    h_op = zb - za
    u_mid = 0.5 * (ua + ub)

    if op.kind in ("capiz_rehas", "capiz", "jalousie_rehas", "jalousie"):
        win_sw = "capiz_window" if "capiz" in op.kind else "jalousie_window"
        # Recessed window sash plane
        mb.quad(wp(ua, za, -dep), wp(ub, za, -dep), wp(ub, zb, -dep), wp(ua, zb, -dep), win_sw)
        # Timber center mullion inside reveal
        _add_local_box(mb, wp, u_mid, za, zb, -dep + 0.03, 0.06, 0.06, "wood_dark")
        # Projecting sloped concrete window sill + molded lintel hood
        _add_local_box(mb, wp, u_mid, za - 0.08, za + 0.01, 0.03, w_op + 0.16, 0.14, op.trim_swatch)
        _add_local_box(mb, wp, u_mid, zb - 0.01, zb + 0.11, 0.04, w_op + 0.20, 0.16, op.trim_swatch)
        # Wrought-iron security *rehas* cage bars inside/proud of the reveal
        if "rehas" in op.kind:
            n_bars = max(3, min(7, int(round(w_op / 0.26))))
            for bi in range(n_bars):
                bu = ua + (bi + 0.5) * (w_op / n_bars)
                _add_local_box(mb, wp, bu, za + 0.02, zb - 0.02, -0.02, 0.024, 0.028, "steel_dark")
            for bz in (za + h_op * 0.22, za + h_op * 0.78):
                _add_local_box(mb, wp, u_mid, bz - 0.014, bz + 0.014, -0.02, w_op - 0.02, 0.03, "steel_dark")

    elif op.kind in ("door_front", "door_rear"):
        mb.quad(wp(ua, za, -dep), wp(ub, za, -dep), wp(ub, zb, -dep), wp(ua, zb, -dep), "door_provincial")
        # Timber door frame & molded lintel
        _add_local_box(mb, wp, u_mid, zb - 0.01, zb + 0.12, 0.035, w_op + 0.22, 0.14, op.trim_swatch)
        # Grounded concrete stoop step at z = wall_z0 (0.0)
        step_h = max(0.14, za - wall_z0)
        _add_local_box(
            mb, wp, u_mid, 0.0, step_h, 0.20, w_op + 0.28, 0.42, "concrete_moss", "concrete_pillar"
        )

    elif op.kind == "bath_vent":
        mb.quad(wp(ua, za, -dep), wp(ub, za, -dep), wp(ub, zb, -dep), wp(ua, zb, -dep), "steel_dark")
        for s_frac in (0.33, 0.67):
            sz = za + h_op * s_frac
            _add_local_box(mb, wp, u_mid, sz - 0.018, sz + 0.018, -dep * 0.45, w_op - 0.02, dep * 0.8, op.trim_swatch)

    elif op.kind == "store_sari":
        # Deep recessed sari-sari bay: back wall, tiled counter, hanging sachets, iron grille + roll-up drum
        mb.quad(wp(ua, za, -dep), wp(ub, za, -dep), wp(ub, zb, -dep), wp(ua, zb, -dep), "sari_grill_counter")
        cnt_h = za + 0.94
        _add_local_box(
            mb, wp, u_mid, za, cnt_h, -0.22, w_op - 0.04, 0.44, "tile_teal_chipped", "concrete_pillar"
        )
        # Hanging chichirya snack strips inside recessed storefront
        for s_off in (-w_op * 0.28, w_op * 0.28):
            _add_local_box(
                mb, wp, u_mid + s_off, cnt_h + 0.18, zb - 0.24, -0.26, 0.30, 0.04, "sachet_strips"
            )
        # Roll-up steel shutter header box at top of bay
        _add_local_box(mb, wp, u_mid, zb - 0.22, zb, -0.10, w_op - 0.02, 0.24, "rollup_shutter")
        # Grounded customer step
        _add_local_box(mb, wp, u_mid, 0.0, max(0.15, za), 0.18, w_op + 0.16, 0.36, "concrete_moss", "concrete_pillar")

    elif op.kind == "store_karinderya":
        mb.quad(wp(ua, za, -dep), wp(ub, za, -dep), wp(ub, zb, -dep), wp(ua, zb, -dep), "plywood_weathered")
        cnt_h = za + 0.90
        _add_local_box(
            mb, wp, u_mid, za, cnt_h, -0.24, w_op - 0.04, 0.48, "tile_terracotta", "concrete_pillar"
        )
        # Glass estante food display cabinet on left side of counter
        _add_local_box(
            mb, wp, ua + w_op * 0.26, cnt_h, cnt_h + 0.52, -0.22, w_op * 0.38, 0.34, "jalousie_window", "zinc_brushed"
        )
        # 2 stainless kaldero serving pots on right side of counter
        for kx in (0.64, 0.82):
            p_base = wp(ua + w_op * kx, cnt_h, -0.22)
            mb.tube(p_base, p_base + Vector((0.0, 0.0, 0.22)), 0.13, 0.14, 8, "zinc_brushed")
        _add_local_box(mb, wp, u_mid, zb - 0.22, zb, -0.10, w_op - 0.02, 0.24, "rollup_shutter")
        _add_local_box(mb, wp, u_mid, 0.0, max(0.15, za), 0.18, w_op + 0.16, 0.36, "concrete_moss", "concrete_pillar")

    elif op.kind == "store_barber":
        # Left doorway + right barbershop mirror/window knee-wall inside recessed bay
        mb.quad(wp(ua, za, -dep), wp(ub, za, -dep), wp(ub, zb, -dep), wp(ua, zb, -dep), "jalousie_window")
        _add_local_box(
            mb, wp, ua + w_op * 0.64, za, za + 0.88, -0.20, w_op * 0.66, 0.36, "tile_teal_chipped", "concrete_pillar"
        )
        _add_local_box(
            mb, wp, ua + w_op * 0.18, za, zb - 0.20, -dep + 0.04, w_op * 0.32, 0.06, "door_provincial"
        )
        _add_local_box(mb, wp, u_mid, zb - 0.20, zb, -0.10, w_op - 0.02, 0.22, "rollup_shutter")
        # Classic 3D cylindrical Barber Pole mounted on right pier!
        pole_p = wp(ub + 0.12, za + 1.05, 0.14)
        mb.tube(pole_p, pole_p + Vector((0.0, 0.0, 0.68)), 0.075, 0.075, 8, "barber_pole_stripe")
        mb.tube(pole_p + Vector((0.0, 0.0, 0.68)), pole_p + Vector((0.0, 0.0, 0.76)), 0.085, 0.04, 8, "zinc_brushed")
        _add_local_box(mb, wp, u_mid, 0.0, max(0.15, za), 0.18, w_op + 0.16, 0.36, "concrete_moss", "concrete_pillar")

    elif op.kind == "store_vulcanizing":
        # Deep open garage bay with partially lowered roll-up steel shutter, air compressor & rubber tires
        mb.quad(wp(ua, za, -dep), wp(ub, za, -dep), wp(ub, zb, -dep), wp(ua, zb, -dep), "chb_raw_moss")
        _add_local_box(mb, wp, u_mid, zb - 0.42, zb, -0.10, w_op - 0.02, 0.18, "rollup_shutter")
        # Horizontal air compressor cylinder inside bay
        comp_a = wp(ua + w_op * 0.25, za + 0.34, -0.42)
        comp_b = wp(ua + w_op * 0.62, za + 0.34, -0.42)
        mb.tube(comp_a, comp_b, 0.18, 0.18, 8, "yero_red_oxide")
        # Stacked rubber tires at right front of bay
        t_pos = wp(ub - 0.34, 0.0, 0.12)
        mb.tube(t_pos, t_pos + Vector((0.0, 0.0, 0.26)), 0.31, 0.31, 10, "rubber_tire")
        mb.tube(t_pos + Vector((0.0, 0.0, 0.26)), t_pos + Vector((0.0, 0.0, 0.52)), 0.30, 0.30, 10, "rubber_tire")

    elif op.kind == "store_panaderya":
        # Recessed neighborhood panaderya bay: warm brick pugon back wall, tiled counter, glass bread estante
        mb.quad(wp(ua, za, -dep), wp(ub, za, -dep), wp(ub, zb, -dep), wp(ua, zb, -dep), "adobe_stone")
        cnt_h = za + 0.90
        _add_local_box(
            mb, wp, u_mid, za, cnt_h, -0.22, w_op - 0.04, 0.46, "tile_terracotta", "concrete_pillar"
        )
        # Glass estante bread display case with golden pandesal trays inside
        _add_local_box(
            mb, wp, ua + w_op * 0.50, cnt_h, cnt_h + 0.58, -0.20, w_op * 0.78, 0.36, "jalousie_window", "zinc_brushed"
        )
        _add_local_box(
            mb, wp, ua + w_op * 0.50, cnt_h + 0.14, cnt_h + 0.24, -0.20, w_op * 0.70, 0.28, "produce_mango"
        )
        _add_local_box(mb, wp, u_mid, zb - 0.20, zb, -0.10, w_op - 0.02, 0.22, "rollup_shutter")
        _add_local_box(mb, wp, u_mid, 0.0, max(0.15, za), 0.18, w_op + 0.16, 0.36, "concrete_moss", "concrete_pillar")

    elif op.kind == "silong_open":
        # Deep recessed civic/residential porch with paneled door & capiz parlor window on back wall
        mb.quad(wp(ua, za, -dep), wp(ub, za, -dep), wp(ub, zb, -dep), wp(ua, zb, -dep), op.swatch)
        _add_local_box(
            mb, wp, ua + w_op * 0.28, za, min(zb - 0.22, za + 2.15), -dep + 0.05, min(0.95, w_op * 0.32), 0.08, "door_provincial"
        )
        if w_op >= 2.0:
            _add_local_box(
                mb, wp, ua + w_op * 0.70, za + 0.78, min(zb - 0.25, za + 2.05), -dep + 0.05, min(1.25, w_op * 0.42), 0.08, "capiz_window"
            )
        _add_local_box(mb, wp, u_mid, 0.0, max(0.16, za), 0.22, w_op + 0.24, 0.44, "concrete_moss", "concrete_pillar")

    elif op.kind == "chapel_portico":
        # Deep recessed barrio kapilya sanctuary: warm plaster back wall, altar table, wooden cross & wrought-iron gate
        mb.quad(wp(ua, za, -dep), wp(ub, za, -dep), wp(ub, zb, -dep), wp(ua, zb, -dep), "stucco_sandstone")
        # Altar table + white/cream altar cloth + wooden cross + twin brass candle holders
        _add_local_box(mb, wp, u_mid, za, za + 0.92, -dep + 0.28, min(1.65, w_op * 0.56), 0.48, "narra_warm", "concrete_pillar")
        _add_local_box(mb, wp, u_mid, za + 1.25, za + 2.18, -dep + 0.06, 0.10, 0.08, "wood_dark")
        _add_local_box(mb, wp, u_mid, za + 1.82, za + 1.92, -dep + 0.06, 0.58, 0.08, "wood_dark")
        for cx_off in (-0.48, 0.48):
            cp = wp(u_mid + cx_off, za + 0.92, -dep + 0.28)
            mb.tube(cp, cp + Vector((0.0, 0.0, 0.24)), 0.04, 0.025, 6, "produce_mango")
        # Open half-folded wrought-iron grille gates at front of portico reveal
        for side_f in (0.14, 0.86):
            _add_local_box(mb, wp, ua + w_op * side_f, za + 0.04, zb - 0.24, -0.06, w_op * 0.24, 0.04, "sari_grill_counter")
        _add_local_box(mb, wp, u_mid, 0.0, max(0.18, za), 0.26, w_op + 0.36, 0.52, "concrete_moss", "concrete_pillar")


# ---------------------------------------------------------------------------
# Overhanging Corrugated GI Roofs, Exposed Purlins, Gutters & PVC Downspouts
# ---------------------------------------------------------------------------

def add_gable_yero_roof(
    mb: MeshBuilder,
    cx: float,
    cy: float,
    z_eave: float,
    w_span: float,
    d_span: float,
    rise: float,
    ov_x: float = 0.52,
    ov_y: float = 0.56,
    roof_swatch: str = "yero_red_oxide",
    fascia_swatch: str = "wood_dark",
    wall_swatch: str = "chb_cream_moss",
    ridge_along_x: bool = True,
    downspout_corners: Sequence[Tuple[int, int]] = ((-1, -1), (1, 1)),
    ov_x_left: Optional[float] = None,
    ov_x_right: Optional[float] = None,
) -> None:
    """Builds a complete overhanging corrugated GI gable roof with solid masonry/wood gable
    tympanums, exposed coco-lumber rafter tails & purlins, painted fascia boards, galvanized
    eave box gutters, ridge flashing cap, and grounded PVC downspouts reaching `Z = 0.004`."""
    thick = 0.065
    z_peak = z_eave + rise

    if ridge_along_x:
        hx_wall = w_span * 0.5
        hy_wall = d_span * 0.5
        ox_l = ov_x if ov_x_left is None else ov_x_left
        ox_r = ov_x if ov_x_right is None else ov_x_right
        x_left = cx - hx_wall - ox_l
        x_right = cx + hx_wall + ox_r
        span_rx = x_right - x_left
        mid_rx = 0.5 * (x_left + x_right)
        hy = hy_wall + ov_y

        # 1. Gable end tympanum walls at x = -hx_wall and x = +hx_wall (flush with side firewalls)
        mb.tri(
            (cx - hx_wall, cy + hy_wall, z_eave),
            (cx - hx_wall, cy - hy_wall, z_eave),
            (cx - hx_wall, cy, z_peak),
            wall_swatch,
        )
        mb.tri(
            (cx + hx_wall, cy - hy_wall, z_eave),
            (cx + hx_wall, cy + hy_wall, z_eave),
            (cx + hx_wall, cy, z_peak),
            wall_swatch,
        )

        # 2. Front (-Y) and Rear (+Y) overhanging corrugated roof decks (top + soffit + rake/fascia)
        slope_drop = rise * (ov_y / max(hy_wall, 0.5)) * 0.55
        ze_out = z_eave - slope_drop

        # Front slope top & soffit
        f_e0 = Vector((x_left, cy - hy, ze_out + thick))
        f_e1 = Vector((x_right, cy - hy, ze_out + thick))
        r_p0 = Vector((x_left, cy, z_peak + thick))
        r_p1 = Vector((x_right, cy, z_peak + thick))
        mb.quad(f_e0, f_e1, r_p1, r_p0, roof_swatch)

        f_s0 = Vector((x_left, cy - hy, ze_out))
        f_s1 = Vector((x_right, cy - hy, ze_out))
        r_s0 = Vector((x_left, cy, z_peak))
        r_s1 = Vector((x_right, cy, z_peak))
        mb.quad(r_s0, r_s1, f_s1, f_s0, "wood_coco")

        # Rear slope (+Y) top & soffit
        b_e0 = Vector((x_right, cy + hy, ze_out + thick))
        b_e1 = Vector((x_left, cy + hy, ze_out + thick))
        mb.quad(b_e0, b_e1, r_p0, r_p1, roof_swatch)

        b_s0 = Vector((x_right, cy + hy, ze_out))
        b_s1 = Vector((x_left, cy + hy, ze_out))
        mb.quad(r_s1, r_s0, b_s1, b_s0, "wood_coco")

        # Rake bargeboards (-X and +X) & front/rear eave fascia
        mb.quad(f_s0, f_e0, r_p0, r_s0, fascia_swatch)
        mb.quad(r_s0, r_p0, b_e1, b_s1, fascia_swatch)
        mb.quad(f_e1, f_s1, r_s1, r_p1, fascia_swatch)
        mb.quad(r_p1, r_s1, b_s0, b_e0, fascia_swatch)
        mb.quad(f_s0, f_s1, f_e1, f_e0, fascia_swatch)
        mb.quad(b_s0, b_s1, b_e1, b_e0, fascia_swatch)

        # Ridge flashing cap along peak
        mb.box(mid_rx, cy, z_peak + thick - 0.01, z_peak + thick + 0.08, span_rx + 0.02, 0.22, "zinc_brushed")

        # 3. Exposed coco-lumber rafter tails under front & rear eaves
        n_rafters = max(4, min(11, int(round(span_rx / 1.35))))
        for ri in range(n_rafters):
            rx = x_left + 0.28 + ri * ((span_rx - 0.56) / max(n_rafters - 1, 1))
            mb.tube(
                (rx, cy - hy_wall + 0.05, z_eave - 0.03),
                (rx, cy - hy + 0.04, ze_out - 0.03),
                0.038, 0.032, 4, "wood_dark",
            )
            mb.tube(
                (rx, cy + hy_wall - 0.05, z_eave - 0.03),
                (rx, cy + hy - 0.04, ze_out - 0.03),
                0.038, 0.032, 4, "wood_dark",
            )

        # 4. Galvanized eave box gutters along front (-Y) and rear (+Y) eaves
        gut_z = ze_out - 0.05
        mb.box(mid_rx, cy - hy - 0.05, gut_z, gut_z + 0.11, span_rx, 0.12, "zinc_brushed")
        mb.box(mid_rx, cy + hy + 0.05, gut_z, gut_z + 0.11, span_rx, 0.12, "zinc_brushed")

        # 5. Vertical PVC downspouts from box gutter down to Z = 0.004!
        for sx_sgn, sy_sgn in downspout_corners:
            gx_end = (x_left + 0.22) if sx_sgn < 0 else (x_right - 0.22)
            add_pvc_downspout(
                mb,
                gutter_pt=(gx_end, cy + sy_sgn * (hy + 0.04), gut_z + 0.02),
                wall_xy=(cx + sx_sgn * (hx_wall - 0.16), cy + sy_sgn * (hy_wall + 0.055)),
                out_dir=(0.0, float(sy_sgn)),
                z_ground=0.004,
            )
    else:
        # Ridge along Y (gable faces street -Y and rear +Y!)
        hx_wall = w_span * 0.5
        hy_wall = d_span * 0.5
        hx = hx_wall + ov_x
        hy = hy_wall + ov_y
        slope_drop = rise * (ov_x / max(hx_wall, 0.5)) * 0.55
        ze_out = z_eave - slope_drop

        # Front (-Y) and Rear (+Y) gable tympanum triangles
        mb.tri(
            (cx - hx_wall, cy - hy_wall, z_eave),
            (cx + hx_wall, cy - hy_wall, z_eave),
            (cx, cy - hy_wall, z_peak),
            wall_swatch,
        )
        mb.tri(
            (cx + hx_wall, cy + hy_wall, z_eave),
            (cx - hx_wall, cy + hy_wall, z_eave),
            (cx, cy + hy_wall, z_peak),
            wall_swatch,
        )

        l_e0 = Vector((cx - hx, cy + hy, ze_out + thick))
        l_e1 = Vector((cx - hx, cy - hy, ze_out + thick))
        r_p0 = Vector((cx, cy + hy, z_peak + thick))
        r_p1 = Vector((cx, cy - hy, z_peak + thick))
        mb.quad(l_e0, l_e1, r_p1, r_p0, roof_swatch)

        l_s0 = Vector((cx - hx, cy + hy, ze_out))
        l_s1 = Vector((cx - hx, cy - hy, ze_out))
        r_s0 = Vector((cx, cy + hy, z_peak))
        r_s1 = Vector((cx, cy - hy, z_peak))
        mb.quad(r_s0, r_s1, l_s1, l_s0, "wood_coco")

        rt_e0 = Vector((cx + hx, cy - hy, ze_out + thick))
        rt_e1 = Vector((cx + hx, cy + hy, ze_out + thick))
        mb.quad(rt_e0, rt_e1, r_p0, r_p1, roof_swatch)

        rt_s0 = Vector((cx + hx, cy - hy, ze_out))
        rt_s1 = Vector((cx + hx, cy + hy, ze_out))
        mb.quad(r_s1, r_s0, rt_s1, rt_s0, "wood_coco")

        # Bargeboards & side gutters
        mb.quad(l_s1, rt_s0, rt_e0, l_e1, fascia_swatch)
        mb.quad(rt_s1, l_s0, l_e0, rt_e1, fascia_swatch)
        mb.box(cx, cy, z_peak + thick - 0.01, z_peak + thick + 0.08, 0.22, hy * 2.0 + 0.04, "zinc_brushed")

        gut_z = ze_out - 0.05
        mb.box(cx - hx - 0.05, cy, gut_z, gut_z + 0.11, 0.12, hy * 2.0, "zinc_brushed")
        mb.box(cx + hx + 0.05, cy, gut_z, gut_z + 0.11, 0.12, hy * 2.0, "zinc_brushed")
        for sx_sgn, sy_sgn in downspout_corners:
            add_pvc_downspout(
                mb,
                gutter_pt=(cx + sx_sgn * (hx + 0.04), cy + sy_sgn * (hy - 0.22), gut_z + 0.02),
                wall_xy=(cx + sx_sgn * (hx_wall + 0.055), cy + sy_sgn * (hy_wall - 0.16)),
                out_dir=(float(sx_sgn), 0.0),
                z_ground=0.004,
            )


def add_shed_yero_roof(
    mb: MeshBuilder,
    cx: float,
    cy: float,
    z_low: float,
    z_high: float,
    w_span: float,
    d_span: float,
    ov_x: float = 0.36,
    ov_y: float = 0.42,
    roof_swatch: str = "yero_rust_heavy",
    high_at_rear: bool = True,
) -> None:
    """Single-pitch overhanging corrugated GI shed roof with purlins & low-eave gutter."""
    hx = w_span * 0.5 + ov_x
    hy = d_span * 0.5 + ov_y
    zf = z_low if high_at_rear else z_high
    zb = z_high if high_at_rear else z_low
    th = 0.055

    t0 = Vector((cx - hx, cy - hy, zf + th))
    t1 = Vector((cx + hx, cy - hy, zf + th))
    t2 = Vector((cx + hx, cy + hy, zb + th))
    t3 = Vector((cx - hx, cy + hy, zb + th))
    b0 = Vector((cx - hx, cy - hy, zf))
    b1 = Vector((cx + hx, cy - hy, zf))
    b2 = Vector((cx + hx, cy + hy, zb))
    b3 = Vector((cx - hx, cy + hy, zb))

    mb.quad(t0, t1, t2, t3, roof_swatch)
    mb.quad(b3, b2, b1, b0, "wood_coco")
    mb.quad(b0, b1, t1, t0, "wood_dark")
    mb.quad(b1, b2, t2, t1, "wood_dark")
    mb.quad(b2, b3, t3, t2, "wood_dark")
    mb.quad(b3, b0, t0, t3, "wood_dark")

    # Exposed purlins underneath
    for pf in (0.22, 0.52, 0.82):
        py = (cy - hy) + pf * (hy * 2.0)
        pz = zf + pf * (zb - zf) - 0.025
        mb.tube((cx - hx + 0.08, py, pz), (cx + hx - 0.08, py, pz), 0.032, 0.032, 4, "wood_dark")

    low_y = (cy - hy - 0.04) if high_at_rear else (cy + hy + 0.04)
    mb.box(cx, low_y, z_low - 0.05, z_low + 0.05, hx * 2.0, 0.10, "zinc_brushed")


def add_pvc_downspout(
    mb: MeshBuilder,
    gutter_pt: Tuple[float, float, float],
    wall_xy: Tuple[float, float],
    out_dir: Tuple[float, float],
    z_ground: float = 0.004,
) -> None:
    """Vertical PVC downspout with gooseneck elbow, wall-clamp brackets, and grounded shoe at `Z = 0.004`."""
    gx, gy, gz = gutter_pt
    wx, wy = wall_xy
    z_elbow = max(gz - 0.34, z_ground + 0.65)
    p_gut = Vector((gx, gy, gz))
    p_elb = Vector((wx, wy, z_elbow))
    p_bot = Vector((wx, wy, z_ground + 0.22))
    p_shoe = Vector((wx + out_dir[0] * 0.18, wy + out_dir[1] * 0.18, z_ground + 0.04))

    mb.tube(p_gut, p_elb, 0.042, 0.040, 6, "zinc_brushed", cap_start=False, cap_end=False)
    mb.tube(p_elb, p_bot, 0.040, 0.040, 6, "zinc_brushed", cap_start=False, cap_end=False)
    mb.tube(p_bot, p_shoe, 0.042, 0.045, 6, "zinc_brushed", cap_start=False, cap_end=True)

    # Metal wall-clamp straps
    span = z_elbow - (z_ground + 0.25)
    if span > 0.8:
        for frac in (0.22, 0.58, 0.90):
            sz = z_ground + 0.25 + span * frac
            mb.box(wx, wy, sz - 0.025, sz + 0.025, 0.11, 0.11, "steel_dark")


# ---------------------------------------------------------------------------
# Storefront Awnings, Painted Wall Murals, Side Firewalls & Rear Dirty Kitchens
# ---------------------------------------------------------------------------

def add_storefront_awning(
    mb: MeshBuilder,
    cx: float,
    front_y: float,
    z_attach: float,
    width: float,
    proj: float = 1.05,
    drop: float = 0.38,
    swatch: str = "trapal_striped",
) -> None:
    """Angled corrugated tin / *trapal* storefront awning with diagonal steel strut brackets."""
    hw = width * 0.5
    z_front = z_attach - drop
    y_out = front_y - proj
    th = 0.04

    t0 = Vector((cx - hw, y_out, z_front + th))
    t1 = Vector((cx + hw, y_out, z_front + th))
    t2 = Vector((cx + hw, front_y, z_attach + th))
    t3 = Vector((cx - hw, front_y, z_attach + th))
    b0 = Vector((cx - hw, y_out, z_front))
    b1 = Vector((cx + hw, y_out, z_front))
    b2 = Vector((cx + hw, front_y, z_attach))
    b3 = Vector((cx - hw, front_y, z_attach))

    mb.quad(t0, t1, t2, t3, swatch)
    mb.quad(b3, b2, b1, b0, "wood_dark")
    # Scalloped/valance front lip
    mb.box(cx, y_out + 0.02, z_front - 0.11, z_front + th, width, 0.04, swatch)

    # Triangulated steel angle-iron knee-brace brackets bolted to storefront piers
    n_brk = 2 if width < 4.8 else 3
    for bi in range(n_brk):
        bx = (cx - hw + 0.22) if n_brk == 1 else (cx - hw + 0.24 + bi * ((width - 0.48) / (n_brk - 1)))
        mb.tube(
            (bx, front_y - 0.02, z_attach - drop - 0.36),
            (bx, y_out + 0.14, z_front + 0.01),
            0.024, 0.020, 5, "steel_dark",
        )
        mb.tube(
            (bx, front_y - 0.02, z_front + 0.02),
            (bx, y_out + 0.16, z_front + 0.02),
            0.018, 0.018, 4, "steel_dark",
        )


def add_signboard_panel(
    mb: MeshBuilder,
    cx: float,
    cy: float,
    z0: float,
    z1: float,
    width: float,
    swatch: str,
    facing: str = "-Y",
    relief: float = 0.055,
    framed: bool = True,
) -> None:
    """Mounts a crisp 2:1 aspect painted wall mural or framed signboard on `-Y`, `+Y`, `-X`, or `+X`."""
    hw = width * 0.5
    frame_sw = "wood_dark" if framed else "concrete_pillar"
    if facing == "-Y":
        y_out = cy - relief
        mb.quad(
            (cx - hw, y_out, z0),
            (cx + hw, y_out, z0),
            (cx + hw, y_out, z1),
            (cx - hw, y_out, z1),
            swatch,
        )
        # Perimeter return bevels (zero floating plane!)
        mb.quad((cx - hw, cy, z0), (cx - hw, y_out, z0), (cx - hw, y_out, z1), (cx - hw, cy, z1), frame_sw)
        mb.quad((cx + hw, y_out, z0), (cx + hw, cy, z0), (cx + hw, cy, z1), (cx + hw, y_out, z1), frame_sw)
        mb.quad((cx - hw, y_out, z1), (cx + hw, y_out, z1), (cx + hw, cy, z1), (cx - hw, cy, z1), frame_sw)
        mb.quad((cx - hw, cy, z0), (cx + hw, cy, z0), (cx + hw, y_out, z0), (cx - hw, y_out, z0), frame_sw)
    elif facing == "+Y":
        y_out = cy + relief
        mb.quad(
            (cx + hw, y_out, z0),
            (cx - hw, y_out, z0),
            (cx - hw, y_out, z1),
            (cx + hw, y_out, z1),
            swatch,
        )
        mb.quad((cx + hw, cy, z0), (cx + hw, y_out, z0), (cx + hw, y_out, z1), (cx + hw, cy, z1), frame_sw)
        mb.quad((cx - hw, y_out, z0), (cx - hw, cy, z0), (cx - hw, cy, z1), (cx - hw, y_out, z1), frame_sw)
        mb.quad((cx + hw, y_out, z1), (cx - hw, y_out, z1), (cx - hw, cy, z1), (cx + hw, cy, z1), frame_sw)
    elif facing == "-X":
        x_out = cx - relief
        mb.quad(
            (x_out, cy + hw, z0),
            (x_out, cy - hw, z0),
            (x_out, cy - hw, z1),
            (x_out, cy + hw, z1),
            swatch,
        )
        mb.quad((cx, cy + hw, z0), (x_out, cy + hw, z0), (x_out, cy + hw, z1), (cx, cy + hw, z1), frame_sw)
        mb.quad((x_out, cy - hw, z0), (cx, cy - hw, z0), (cx, cy - hw, z1), (x_out, cy - hw, z1), frame_sw)
        mb.quad((x_out, cy + hw, z1), (x_out, cy - hw, z1), (cx, cy - hw, z1), (cx, cy + hw, z1), frame_sw)
    elif facing == "+X":
        x_out = cx + relief
        mb.quad(
            (x_out, cy - hw, z0),
            (x_out, cy + hw, z0),
            (x_out, cy + hw, z1),
            (x_out, cy - hw, z1),
            swatch,
        )
        mb.quad((cx, cy - hw, z0), (x_out, cy - hw, z0), (x_out, cy - hw, z1), (cx, cy - hw, z1), frame_sw)
        mb.quad((x_out, cy + hw, z0), (cx, cy + hw, z0), (cx, cy + hw, z1), (x_out, cy + hw, z1), frame_sw)
        mb.quad((x_out, cy - hw, z1), (x_out, cy + hw, z1), (cx, cy + hw, z1), (cx, cy - hw, z1), frame_sw)


def add_meralco_side_meter_and_weatherhead(
    mb: MeshBuilder,
    side_x: float,
    y_pos: float,
    z_eave: float,
    is_right_side: bool = True,
) -> Tuple[float, float, float]:
    """Meralco round glass kWh meter socket + gray PVC conduit riser + porcelain weatherhead anchor!"""
    sgn = 1.0 if is_right_side else -1.0
    # Square gray meter socket box at Z = 1.55..1.85m
    mb.box(side_x + sgn * 0.055, y_pos, 1.52, 1.84, 0.11, 0.26, "zinc_brushed")
    # Round glass kWh dial dome protruding outward
    p_a = Vector((side_x + sgn * 0.10, y_pos, 1.68))
    p_b = Vector((side_x + sgn * 0.19, y_pos, 1.68))
    mb.tube(p_a, p_b, 0.105, 0.095, 8, "meter_box", cap_start=False, cap_end=True)

    # Vertical gray PVC service-entrance riser pipe up to roof eave + curved weatherhead hood
    rx = side_x + sgn * 0.055
    z_top = z_eave + 0.22
    mb.tube((rx, y_pos, 1.84), (rx, y_pos, z_top), 0.028, 0.028, 6, "zinc_brushed")
    # Pipe wall-clamp straps along the riser
    for frac in (0.30, 0.62, 0.88):
        sz = 1.84 + (z_top - 1.84) * frac
        mb.box(side_x + sgn * 0.04, y_pos, sz - 0.02, sz + 0.02, 0.09, 0.09, "steel_dark")
    # Weatherhead gooseneck drip hood & porcelain clevis spool wire anchor
    wh_tip = (rx + sgn * 0.15, y_pos, z_top - 0.06)
    mb.tube(
        (rx, y_pos, z_top),
        wh_tip,
        0.044, 0.034, 6, "steel_dark",
    )
    # Ceramic strain-insulator spool right below weatherhead hood (exact wire tie-in!)
    spool_pt = (side_x + sgn * 0.14, y_pos, z_top - 0.18)
    mb.box(side_x + sgn * 0.07, y_pos, z_top - 0.23, z_top - 0.13, 0.14, 0.07, "steel_dark")
    mb.tube(
        (spool_pt[0], spool_pt[1] - 0.045, spool_pt[2]),
        (spool_pt[0], spool_pt[1] + 0.045, spool_pt[2]),
        0.042, 0.042, 7, "concrete_pillar",
    )
    # Grounding conduit down to plinth
    mb.tube((rx, y_pos + 0.06, 0.02), (rx, y_pos + 0.06, 1.52), 0.018, 0.018, 5, "zinc_brushed")
    mb.weatherhead_sockets.append(spool_pt)
    return spool_pt


def add_rooftop_eave_signboard(
    mb: MeshBuilder,
    cx: float,
    front_eave_y: float,
    z_eave_front: float,
    height: float,
    width: float,
    swatch: str,
) -> None:
    """Mounts an iconic Philippine neighborhood shop signboard proud at the front roof eave
    (`front_eave_y`) with a solid wood/angle-iron backer board & twin diagonal steel L-struts
    back to the roof slope — 100% unobstructed from both street level and elevated cameras!"""
    z0 = z_eave_front + 0.04
    z1 = z0 + height
    # Solid painted angle-iron/plywood sign backing box (`0.08m` thick)
    mb.box(cx, front_eave_y + 0.04, z0 - 0.02, z1 + 0.02, width + 0.06, 0.08, "wood_dark")
    add_signboard_panel(mb, cx, front_eave_y, z0, z1, width, swatch, facing="-Y", relief=0.035, framed=True)
    # Twin diagonal galvanized/angle-iron struts bolted back into the roof purlins
    hw = width * 0.36
    for sx in (-hw, hw):
        mb.tube(
            (cx + sx, front_eave_y + 0.06, z0 + height * 0.78),
            (cx + sx, front_eave_y + 0.68, z0 + 0.08),
            0.022, 0.020, 4, "steel_dark",
        )
        mb.tube(
            (cx + sx, front_eave_y + 0.05, z0 - 0.04),
            (cx + sx, front_eave_y + 0.05, z1 + 0.02),
            0.020, 0.020, 4, "steel_dark",
        )


def add_firewall_parapet(
    mb: MeshBuilder,
    side_x: float,
    cy: float,
    d_wall: float,
    z_eave: float,
    z_peak: float,
    swatch: str = "chb_raw_moss",
) -> None:
    """Seamless 5-tier stepped Philippine CHB side firewall parapet hugging the gable roof slope (`+0.24m`)."""
    hy = d_wall * 0.5 + 0.48  # extends flush to front & rear roof eaves so zero roof overhang pokes past!
    th = 0.20
    rise = max(z_peak - z_eave, 0.25)
    # 5 contiguous stepped CHB parapet tiers from front eave (-1.0) -> apex (0.0) -> rear eave (+1.0)
    tiers = [
        (-1.00, -0.60, z_eave + rise * 0.34 + 0.20),
        (-0.60, -0.22, z_eave + rise * 0.72 + 0.22),
        (-0.22,  0.22, z_peak + 0.26),
        ( 0.22,  0.60, z_eave + rise * 0.72 + 0.22),
        ( 0.60,  1.00, z_eave + rise * 0.34 + 0.20),
    ]
    for f0, f1, z_top_step in tiers:
        ya = cy + f0 * hy
        yb = cy + f1 * hy
        ty = 0.5 * (ya + yb)
        td = (yb - ya) + 0.015  # 1.5cm overlap prevents sub-millimeter seam cracks
        mb.box(side_x, ty, z_eave - 0.08, z_top_step, th, td, swatch, "zinc_brushed")
        # Galvanized sheet-metal parapet flashing cap
        mb.box(side_x, ty, z_top_step, z_top_step + 0.045, th + 0.04, td + 0.02, "zinc_brushed")


def add_rear_dirty_kitchen(
    mb: MeshBuilder,
    cx: float,
    rear_y: float,
    width: float = 2.9,
    depth: float = 1.08,
    z_attach: float = 2.68,
) -> None:
    """Asymmetric Rear Face (`+Y` Blender -> `-Z` Godot) *kusinang labas* dirty kitchen:
    lean-to corrugated tin shed on `0.095m x 0.095m` timber posts + concrete foot blocks,
    `0.09m x 0.11m` horizontal front eave header beam, sloped rafters, `45°` diagonal timber
    knee braces to rear wall, tiled *lababo* sink counter, PVC drainpipe, and 200L blue water drum!"""
    hw = width * 0.5
    y_out = rear_y + depth
    ov_x_shed = 0.18
    ov_y_shed = 0.14

    # 1. Lean-to corrugated tin shed roof sloping from rear wall (z_attach) down to y_out (z_attach - 0.46)
    add_shed_yero_roof(
        mb,
        cx=cx,
        cy=rear_y + depth * 0.5,
        z_low=z_attach - 0.46,
        z_high=z_attach,
        w_span=width,
        d_span=depth,
        ov_x=ov_x_shed,
        ov_y=ov_y_shed,
        roof_swatch="yero_patchwork",
        high_at_rear=False,  # high at rear_y (smaller Y), low at y_out (larger Y)!
    )

    # 2. Structural timber framing: `0.095m x 0.095m` posts on concrete foot blocks, `0.09m x 0.11m`
    #    horizontal front eave header beam, rear wall ledger, sloped rafters, and `45°` knee braces!
    y_post = y_out - 0.10
    z_roof_post = z_attach - 0.46 * ((depth - 0.10 + ov_y_shed) / (depth + 2.0 * ov_y_shed))
    z_beam_top = z_roof_post + 0.005
    z_beam_bot = z_beam_top - 0.11

    # Horizontal front eave header beam (`0.09m` deep x `0.11m` tall) + rear wall ledger plate
    mb.box(cx, y_post, z_beam_bot, z_beam_top, width + 0.24, 0.09, "wood_dark")
    mb.box(cx, rear_y + 0.035, z_attach - 0.15, z_attach - 0.04, width + 0.16, 0.07, "wood_dark")

    # Sloped side & intermediate rafters directly beneath the corrugated tin sheet
    n_rafters = 4 if width >= 3.3 else 3
    for ri in range(n_rafters):
        rx = (cx - hw - 0.06) + ri * ((width + 0.12) / (n_rafters - 1))
        mb.tube(
            (rx, rear_y + 0.03, z_attach - 0.05),
            (rx, y_out + 0.08, z_attach - 0.45),
            0.036, 0.034, 4, "wood_dark",
        )

    post_xs = [cx - hw + 0.12, cx + hw - 0.12]
    for idx_p, px in enumerate(post_xs):
        # Grounded concrete foot block (`min_z = 0.000`) + sturdy `0.095m x 0.095m` timber post
        mb.frustum_plinth(px, y_post, 0.0, 0.14, 0.22, 0.22, 0.22, 0.16, 0.16, "concrete_moss", "concrete_pillar")
        mb.box(px, y_post, 0.22, z_beam_bot + 0.02, 0.095, 0.095, "wood_dark")

        # 45-degree diagonal timber knee brace anchoring front post back to the rear masonry wall!
        brace_span_y = max(0.55, (y_post - 0.03) - (rear_y + 0.035))
        z_brace_front = z_beam_bot - 0.04
        z_brace_wall = z_brace_front - brace_span_y  # exact 45-degree slope (`dz == dy`)!
        mb.box(px, rear_y + 0.03, z_brace_wall - 0.08, z_brace_wall + 0.08, 0.11, 0.06, "wood_dark")
        mb.tube(
            (px, y_post - 0.02, z_brace_front),
            (px, rear_y + 0.035, z_brace_wall),
            0.038, 0.038, 4, "wood_dark",
        )

        # Lateral 45-degree corner knee brace from post into horizontal front eave header beam
        inward_x = 1.0 if idx_p == 0 else -1.0
        mb.tube(
            (px, y_post, z_beam_bot - 0.38),
            (px + inward_x * 0.38, y_post, z_beam_bot + 0.02),
            0.032, 0.032, 4, "wood_dark",
        )

    # 3. Tiled concrete *lababo* sink counter + raised rim + PVC drainpipe to Z = 0
    lab_w = min(1.48, width * 0.56)
    lab_x = cx - hw + lab_w * 0.5 + 0.18
    lab_y = rear_y + 0.32
    mb.box(lab_x, lab_y, 0.0, 0.84, lab_w, 0.58, "chb_raw_moss", "tile_teal_chipped")
    # Sink splashback & rimmed basin walls
    mb.box(lab_x, rear_y + 0.06, 0.84, 1.08, lab_w, 0.10, "tile_teal_chipped")
    mb.box(lab_x, lab_y + 0.24, 0.84, 0.94, lab_w, 0.08, "tile_teal_chipped")
    # Faucet & drainpipe reaching Z = 0.004
    mb.tube((lab_x, rear_y + 0.08, 1.04), (lab_x, rear_y + 0.24, 1.06), 0.02, 0.018, 5, "zinc_brushed")
    mb.tube((lab_x + 0.22, rear_y + 0.28, 0.004), (lab_x + 0.22, rear_y + 0.28, 0.80), 0.032, 0.032, 6, "zinc_brushed")

    # 4. Blue 200-liter plastic water drum grounded at Z = 0.000
    drum_x = cx + hw - 0.42
    drum_y = rear_y + 0.44
    mb.tube((drum_x, drum_y, 0.0), (drum_x, drum_y, 0.86), 0.27, 0.28, 10, "water_drum_blue")
    mb.tube((drum_x, drum_y, 0.86), (drum_x, drum_y, 0.90), 0.29, 0.27, 10, "steel_dark")

    # 5. Sheet-metal kitchen exhaust vent hood above shed on rear wall
    mb.box(lab_x, rear_y + 0.14, z_attach + 0.22, z_attach + 0.58, 0.56, 0.28, "zinc_brushed")


# ---------------------------------------------------------------------------
# True 3D BVHTree Raycast Ambient Occlusion (`COLOR_0`) + External GLB Exporter
# ---------------------------------------------------------------------------

def _hemisphere_dirs() -> List[Vector]:
    """14 stratified directions biased upward/outward for sunlit Deadlock cavity & eave AO."""
    dirs: List[Vector] = [Vector((0.0, 0.0, 1.0))]
    for ring_elev, count in ((0.36, 7), (0.78, 6)):
        cz = math.sin(ring_elev)
        cr = math.cos(ring_elev)
        for i in range(count):
            ang = (2.0 * math.pi * i) / count + ring_elev
            dirs.append(Vector((cr * math.cos(ang), cr * math.sin(ang), cz)).normalized())
    return dirs


_AO_DIRS = _hemisphere_dirs()


def bake_bvh_vertex_ao(bm: bmesh.types.BMesh, max_dist: float = 2.15, ground_ao: bool = True) -> None:
    """Computes fast C++ `BVHTree` 3D ambient occlusion inside recessed windows, eaves,
    awnings, and ground plinth contact, storing sunlit warm-neutral AO into `COLOR_0`."""
    bm.verts.ensure_lookup_table()
    bm.faces.ensure_lookup_table()
    bm.normal_update()

    color_layer = bm.loops.layers.color.get("Color")
    if color_layer is None:
        color_layer = bm.loops.layers.color.new("Color")
    custom_ao_layer = bm.verts.layers.float.get("custom_ao")
    has_custom_nrm_layer = bm.verts.layers.int.get("has_custom_nrm")
    custom_nrm_layer = bm.verts.layers.float_vector.get("custom_nrm")

    bvh = BVHTree.FromBMesh(bm, epsilon=0.0005)
    for face in bm.faces:
        fn = face.normal
        for loop in face.loops:
            v = loop.vert
            if has_custom_nrm_layer is not None and custom_nrm_layer is not None and v[has_custom_nrm_layer] == 1:
                vn = Vector(v[custom_nrm_layer])
            else:
                vn = (fn * 0.65 + v.normal * 0.35)
            if vn.length_squared > 1e-6:
                vn.normalize()
            else:
                vn = Vector((0.0, 0.0, 1.0))

            origin = v.co + vn * 0.032
            occ = 0.0
            total_w = 0.0
            for d in _AO_DIRS:
                nd = vn.dot(d)
                if nd <= -0.15:
                    continue
                w = max(0.20, nd + 0.25)
                total_w += w
                hit_loc, _, _, hit_dist = bvh.ray_cast(origin, d, max_dist)
                if hit_loc is not None and hit_dist is not None:
                    occ += w * (1.0 - min(hit_dist / max_dist, 1.0)) ** 1.35

            ao_ray = 1.0 - (occ / max(total_w, 1e-4)) * 0.34
            c_ao = v[custom_ao_layer] if custom_ao_layer is not None else 0.0
            if c_ao > 0.01:
                # Pure continuous 3D spatial canopy AO (`0.74` linear glTF COLOR_0 floor -> `0.872` sRGB pre-export!)
                # Avoids BVH ray hits against overlapping internal puff faces so bough clouds blend seamlessly!
                lin_ao = max(0.74, min(1.0, c_ao))
                t_sun = (lin_ao - 0.74) / 0.26
                lin_r = lin_ao * (0.95 + 0.05 * t_sun)
                lin_g = lin_ao * (1.00 + 0.00 * t_sun)
                lin_b = lin_ao * (0.90 + 0.07 * t_sun)
                # Convert target linear COLOR_0 -> sRGB loop color so Blender's glTF exporter writes exact [0.74..1.00] linear!
                r_c = min(1.0, lin_r ** (1.0 / 2.2))
                g_c = min(1.0, lin_g ** (1.0 / 2.2))
                b_c = min(1.0, lin_b ** (1.0 / 2.2))
                loop[color_layer] = (r_c, g_c, b_c, 1.0)
                continue

            ao_gnd = 1.0
            if ground_ao:
                ao_gnd = 0.86 + 0.14 * min(max(v.co.z / 0.55, 0.0), 1.0)
            # Downward-facing soffits get gentle warm bounce shading
            soffit = 0.92 if fn.z < -0.45 else 1.0
            ao = max(0.72, min(1.0, ao_ray * ao_gnd * soffit))
            # Warm Deadlock shadow bounce (never cold gray/black; 0.72^2.2 = 0.48 linear COLOR_0!)
            r_c = ao
            g_c = ao * (0.988 + 0.012 * ao)
            b_c = ao * (0.972 + 0.028 * ao)
            loop[color_layer] = (r_c, g_c, b_c, 1.0)


def ensure_glb_import_sidecar(glb_path: Path) -> None:
    """Preserves existing `.glb.import` untouched; writes a clean Godot 4 `.glb.import` ONLY for brand-new `.glb`s."""
    sidecar = glb_path.with_name(glb_path.name + ".import")
    if sidecar.exists():
        return
    res_path = f"res://assets/models/barangay/{glb_path.name}"
    h_md5 = hashlib.md5(res_path.encode("utf-8")).hexdigest()
    uid_seed = hashlib.sha1(res_path.encode("utf-8")).hexdigest()[:13]
    alphabet = "0123456789abcdefghijklmnopqrstuvwxyz"
    val = int(uid_seed, 16)
    uid_chars = ["c"]
    for _ in range(12):
        uid_chars.append(alphabet[val % 36])
        val //= 36
    uid_str = "uid://" + "".join(uid_chars)
    scn_path = f"res://.godot/imported/{glb_path.name}-{h_md5}.scn"
    content = f"""[remap]

importer="scene"
importer_version=1
type="PackedScene"
uid="{uid_str}"
path="{scn_path}"

[deps]

source_file="{res_path}"
dest_files=["{scn_path}"]

[params]

nodes/root_type=""
nodes/root_name=""
nodes/root_script=null
nodes/apply_root_scale=true
nodes/root_scale=1.0
nodes/import_as_skeleton_bones=false
nodes/use_name_suffixes=true
nodes/use_node_type_suffixes=true
meshes/ensure_tangents=true
meshes/generate_lods=true
meshes/create_shadow_meshes=true
meshes/light_baking=1
meshes/lightmap_texel_size=0.2
meshes/force_disable_compression=false
skins/use_named_skins=true
animation/import=true
animation/fps=30
animation/trimming=false
animation/remove_immutable_tracks=true
animation/import_rest_as_RESET=false
import_script/path=""
materials/extract=0
materials/extract_format=0
materials/extract_path=""
_subresources={{}}
gltf/naming_version=2
gltf/embedded_image_handling=1
"""
    sidecar.write_text(content, encoding="utf-8", newline="\n")


def _patch_glb_external_textures(
    glb_path: Path,
    albedo_uri: str,
    normal_uri: str,
    orm_uri: Optional[str] = None,
    normal_scale: float = 0.82,
    roughness: float = 0.86,
    alpha_cutoff: Optional[float] = None,
) -> None:
    """Rewrites a GLB's JSON chunk so `baseColorTexture`, `normalTexture`, and optional
    `metallicRoughnessTexture` reference shared external files with zero duplicated embedded bytes!"""
    raw = glb_path.read_bytes()
    magic, version, _ = struct.unpack_from("<III", raw, 0)
    assert magic == 0x46546C67 and version == 2, f"Invalid GLB header in {glb_path}"

    json_len, json_type = struct.unpack_from("<II", raw, 12)
    assert json_type == 0x4E4F534A
    gltf = json.loads(raw[20 : 20 + json_len].decode("utf-8"))

    bin_offset = 20 + json_len
    bin_bytes = b""
    if bin_offset + 8 <= len(raw):
        bin_len, bin_type = struct.unpack_from("<II", raw, bin_offset)
        if bin_type == 0x004E4942:
            bin_bytes = raw[bin_offset + 8 : bin_offset + 8 + bin_len]

    images = [
        {"name": Path(albedo_uri).stem, "uri": albedo_uri},
        {"name": Path(normal_uri).stem, "uri": normal_uri},
    ]
    textures = [
        {"sampler": 0, "source": 0},
        {"sampler": 0, "source": 1},
    ]
    if orm_uri is not None:
        images.append({"name": Path(orm_uri).stem, "uri": orm_uri})
        textures.append({"sampler": 0, "source": 2})

    gltf["images"] = images
    gltf["samplers"] = [
        {
            "magFilter": 9729,  # LINEAR
            "minFilter": 9987,  # LINEAR_MIPMAP_LINEAR
            "wrapS": 10497,
            "wrapT": 10497,
        }
    ]
    gltf["textures"] = textures
    pbr: Dict[str, Any] = {
        "baseColorFactor": [1.0, 1.0, 1.0, 1.0],
        "baseColorTexture": {"index": 0},
        "metallicFactor": 1.0 if orm_uri is not None else 0.0,
        "roughnessFactor": 1.0 if orm_uri is not None else roughness,
    }
    if orm_uri is not None:
        pbr["metallicRoughnessTexture"] = {"index": 2}

    mat_def: Dict[str, Any] = {
        "name": Path(albedo_uri).stem + "Mat",
        "doubleSided": alpha_cutoff is not None,
        "pbrMetallicRoughness": pbr,
        "normalTexture": {"index": 1, "scale": normal_scale},
    }
    if orm_uri is not None:
        mat_def["occlusionTexture"] = {"index": 2, "strength": 0.65}
    if alpha_cutoff is not None:
        mat_def["alphaMode"] = "MASK"
        mat_def["alphaCutoff"] = alpha_cutoff
    gltf["materials"] = [mat_def]
    for m in gltf.get("meshes", []):
        for prim in m.get("primitives", []):
            prim["material"] = 0

    new_json = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    pad_json = (4 - (len(new_json) % 4)) % 4
    new_json += b" " * pad_json

    pad_bin = (4 - (len(bin_bytes) % 4)) % 4
    bin_padded = bin_bytes + (b"\x00" * pad_bin)

    total_len = 12 + 8 + len(new_json) + (8 + len(bin_padded) if bin_padded else 0)
    out = bytearray()
    out += struct.pack("<III", 0x46546C67, 2, total_len)
    out += struct.pack("<II", len(new_json), 0x4E4F534A)
    out += new_json
    if bin_padded:
        out += struct.pack("<II", len(bin_padded), 0x004E4942)
        out += bin_padded
    glb_path.write_bytes(bytes(out))


def finalize_and_export_glb(
    mb: MeshBuilder,
    out_glb_path: Path,
    albedo_filename: str,
    normal_filename: str,
    orm_filename: Optional[str] = None,
    ground_at_zero: bool = True,
    center_xy: bool = True,
    ao_max_dist: float = 2.15,
    normal_scale: float = 0.82,
    roughness: float = 0.86,
) -> Dict[str, Any]:
    """Finalizes a `MeshBuilder` into a single-mesh, single-surface, tangent-mapped,
    BVH-AO-baked `.glb` referencing external shared Albedo + Normal (+ ORM) textures."""
    bm = mb.bm
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0004)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.verts.ensure_lookup_table()

    if len(bm.verts) == 0:
        raise RuntimeError(f"Empty mesh for {out_glb_path.name}")

    xs = [v.co.x for v in bm.verts]
    ys = [v.co.y for v in bm.verts]
    zs = [v.co.z for v in bm.verts]
    shift_x = -0.5 * (min(xs) + max(xs)) if center_xy else 0.0
    shift_y = -0.5 * (min(ys) + max(ys)) if center_xy else 0.0
    shift_z = -min(zs) if ground_at_zero else 0.0

    if abs(shift_x) > 1e-6 or abs(shift_y) > 1e-6 or abs(shift_z) > 1e-6:
        bmesh.ops.translate(bm, verts=bm.verts, vec=(shift_x, shift_y, shift_z))

    # Convert registered Blender weatherhead sockets to exact post-centering Godot local Vector3!
    # Blender `(bx', by', bz')` with `export_yup=True` -> Godot `(bx', bz', -by')`.
    weatherheads_godot: List[Tuple[float, float, float]] = [
        (
            round(wx + shift_x, 4),
            round(wz + shift_z, 4),
            round(-(wy + shift_y), 4),
        )
        for (wx, wy, wz) in mb.weatherhead_sockets
    ]

    bake_bvh_vertex_ao(bm, max_dist=ao_max_dist, ground_ao=ground_at_zero)

    # Extract optional custom split loop normals (e.g. outward-radial botanical canopy normals!)
    has_cn_layer = bm.verts.layers.int.get("has_custom_nrm")
    cn_layer = bm.verts.layers.float_vector.get("custom_nrm")
    loop_custom_normals: Optional[List[Tuple[float, float, float]]] = None
    if has_cn_layer is not None and cn_layer is not None and any(v[has_cn_layer] == 1 for v in bm.verts):
        bm.normal_update()
        loop_custom_normals = []
        for f in bm.faces:
            fn = f.normal
            for lp in f.loops:
                vv = lp.vert
                if vv[has_cn_layer] == 1:
                    nv = Vector(vv[cn_layer])
                elif f.smooth:
                    nv = vv.normal
                else:
                    nv = fn
                if nv.length_squared > 1e-8:
                    nv = nv.normalized()
                else:
                    nv = Vector((0.0, 0.0, 1.0))
                loop_custom_normals.append((float(nv.x), float(nv.y), float(nv.z)))

    mesh_name = out_glb_path.stem
    me = bpy.data.meshes.new(mesh_name)
    bm.to_mesh(me)
    bm.free()

    if loop_custom_normals is not None and len(loop_custom_normals) == len(me.loops):
        me.normals_split_custom_set(loop_custom_normals)

    if len(me.color_attributes) > 0:
        me.color_attributes.active_color_index = 0
        me.color_attributes.render_color_index = 0

    mat = bpy.data.materials.new(name=f"{mesh_name}_mat")
    mat.use_nodes = True
    me.materials.append(mat)

    me.calc_tangents(uvmap="UVMap")

    # Purge ALL existing objects across all collections so GLB has strictly 1 MeshInstance!
    for old_ob in list(bpy.data.objects):
        bpy.data.objects.remove(old_ob, do_unlink=True)

    ob = bpy.data.objects.new(mesh_name, me)
    bpy.context.scene.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)

    out_glb_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=str(out_glb_path),
        export_format="GLB",
        use_selection=True,
        export_yup=True,
        export_apply=True,
        export_texcoords=True,
        export_normals=True,
        export_tangents=True,
        export_vertex_color="ACTIVE",
        export_materials="EXPORT",
    )

    _patch_glb_external_textures(
        out_glb_path,
        albedo_uri=albedo_filename,
        normal_uri=normal_filename,
        orm_uri=orm_filename,
        normal_scale=normal_scale,
        roughness=roughness,
    )
    ensure_glb_import_sidecar(out_glb_path)

    n_tris = len(me.polygons)
    xs2 = [v.co.x for v in me.vertices]
    ys2 = [v.co.y for v in me.vertices]
    zs2 = [v.co.z for v in me.vertices]
    stats: Dict[str, Any] = {
        "tris": n_tris,
        "verts": len(me.vertices),
        "w": max(xs2) - min(xs2),
        "d": max(ys2) - min(ys2),
        "h": max(zs2) - min(zs2),
        "min_y": min(zs2),
        "weatherheads": weatherheads_godot,
    }

    bpy.context.scene.collection.objects.unlink(ob)
    bpy.data.objects.remove(ob, do_unlink=True)
    bpy.data.meshes.remove(me, do_unlink=True)
    bpy.data.materials.remove(mat, do_unlink=True)
    return stats
