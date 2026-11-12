#!/usr/bin/env python3
"""Deterministic generator forKalyeah's 21 minimalist white-outline HUD icons.

Outputs crisp 128x128 transparent RGBA8 PNGs into `assets/ui/hud_icons/` plus
clean lossless Godot 4 `.import` sidecars (`compress/mode=0`,
`mipmaps/generate=false`, LF newlines, zero `md5=` or `generator_parameters`
lines).

Visual spec:
- Pure white (`#FFFFFF`, RGB = 255, 255, 255 across ALL pixels including alpha=0
  so bilinear filtering never bleeds dark fringes).
- 8x supersampled (1024x1024 -> 128x128 exact 64-sample BOX area filter) smooth
  anti-aliased alpha silhouettes and negative-space linework.
- Zero colored fills, zero dark backgrounds.
- >= 16 px transparent margin on all four edges (exceeding >= 14 px audit floor)
  and circular safe radius so `HudStyle.draw_white_disc` rims never clip art.

Usage:
    python tools/art/build_hud_white_icons.py
"""
from __future__ import annotations

import math
from pathlib import Path
from typing import Callable, Sequence

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT_DIR = ROOT / "assets" / "ui" / "hud_icons"

ICON_SIZE = 128
SS = 8
HI_SIZE = ICON_SIZE * SS  # 1024x1024 supersampled coverage canvas
MIN_MARGIN_PX = 15  # Enforce >= 15 px transparent border on all 4 sides


# ---------------------------------------------------------------------------
# 8x Supersampled 2D Constructive Vector Canvas
# ---------------------------------------------------------------------------


def _rot_pt(x: float, y: float, cx: float, cy: float, deg: float) -> tuple[float, float]:
    if abs(deg) < 1e-7:
        return (x, y)
    rad = math.radians(deg)
    c, s = math.cos(rad), math.sin(rad)
    dx, dy = x - cx, y - cy
    return (cx + dx * c - dy * s, cy + dx * s + dy * c)


def _catmull_rom(pts: Sequence[tuple[float, float]], samples_per_seg: int = 24) -> list[tuple[float, float]]:
    n = len(pts)
    if n < 2:
        return list(pts)
    if n == 2:
        x0, y0 = pts[0]
        x1, y1 = pts[1]
        return [
            (x0 + (x1 - x0) * t / samples_per_seg, y0 + (y1 - y0) * t / samples_per_seg)
            for t in range(samples_per_seg + 1)
        ]
    ext = [pts[0]] + list(pts) + [pts[-1]]
    out: list[tuple[float, float]] = []
    for i in range(1, len(ext) - 2):
        p0, p1, p2, p3 = ext[i - 1], ext[i], ext[i + 1], ext[i + 2]
        steps = samples_per_seg
        end = steps + 1 if i == len(ext) - 3 else steps
        for s_idx in range(end):
            t = s_idx / steps
            t2 = t * t
            t3 = t2 * t
            x = 0.5 * (
                (2.0 * p1[0])
                + (-p0[0] + p2[0]) * t
                + (2.0 * p0[0] - 5.0 * p1[0] + 4.0 * p2[0] - p3[0]) * t2
                + (-p0[0] + 3.0 * p1[0] - 3.0 * p2[0] + p3[0]) * t3
            )
            y = 0.5 * (
                (2.0 * p1[1])
                + (-p0[1] + p2[1]) * t
                + (2.0 * p0[1] - 5.0 * p1[1] + 4.0 * p2[1] - p3[1]) * t2
                + (-p0[1] + 3.0 * p1[1] - 3.0 * p2[1] + p3[1]) * t3
            )
            out.append((x, y))
    return out


class IconCanvas:
    """Constructive 2D silhouette & negative-space linework builder in 0..128 coords."""

    def __init__(self) -> None:
        self.img = Image.new("L", (HI_SIZE, HI_SIZE), 0)
        self.draw = ImageDraw.Draw(self.img)

    def _s(self, v: float) -> float:
        return v * SS

    def _pts(self, pts: Sequence[tuple[float, float]]) -> list[tuple[float, float]]:
        return [(p[0] * SS, p[1] * SS) for p in pts]

    def circle(self, cx: float, cy: float, r: float, fill: int = 255) -> None:
        if r <= 0.0:
            return
        x0, y0 = self._s(cx - r), self._s(cy - r)
        x1, y1 = self._s(cx + r), self._s(cy + r)
        self.draw.ellipse([x0, y0, x1, y1], fill=fill)

    def ring(self, cx: float, cy: float, r_outer: float, r_inner: float, fill: int = 255) -> None:
        self.circle(cx, cy, r_outer, fill=fill)
        if r_inner > 0.0:
            self.circle(cx, cy, r_inner, fill=0 if fill == 255 else 255)

    def poly(self, pts: Sequence[tuple[float, float]], fill: int = 255) -> None:
        if len(pts) >= 3:
            self.draw.polygon(self._pts(pts), fill=fill)

    def ellipse(
        self,
        cx: float,
        cy: float,
        rx: float,
        ry: float,
        rot_deg: float = 0.0,
        fill: int = 255,
        steps: int = 96,
    ) -> None:
        pts: list[tuple[float, float]] = []
        for i in range(steps):
            ang = 2.0 * math.pi * i / steps
            px = cx + rx * math.cos(ang)
            py = cy + ry * math.sin(ang)
            pts.append(_rot_pt(px, py, cx, cy, rot_deg))
        self.poly(pts, fill=fill)

    def ellipse_ring(
        self,
        cx: float,
        cy: float,
        rx: float,
        ry: float,
        thickness: float,
        rot_deg: float = 0.0,
        fill: int = 255,
    ) -> None:
        self.ellipse(cx, cy, rx + thickness * 0.5, ry + thickness * 0.5, rot_deg=rot_deg, fill=fill)
        if rx - thickness * 0.5 > 0.2 and ry - thickness * 0.5 > 0.2:
            self.ellipse(cx, cy, rx - thickness * 0.5, ry - thickness * 0.5, rot_deg=rot_deg, fill=0)

    def capsule(
        self,
        x0: float,
        y0: float,
        x1: float,
        y1: float,
        r0: float,
        r1: float | None = None,
        fill: int = 255,
    ) -> None:
        """Exact tangent frustum between circle (x0, y0, r0) and (x1, y1, r1)."""
        rb = r0 if r1 is None else r1
        self.circle(x0, y0, r0, fill=fill)
        self.circle(x1, y1, rb, fill=fill)
        dx, dy = x1 - x0, y1 - y0
        dist = math.hypot(dx, dy)
        if dist <= 1e-5 or dist <= abs(r0 - rb):
            return
        # Angle of external bitangent normal
        cos_a = max(-1.0, min(1.0, (r0 - rb) / dist))
        alpha = math.acos(cos_a)
        base = math.atan2(dy, dx)
        a1 = base + alpha
        a2 = base - alpha
        quad = [
            (x0 + r0 * math.cos(a1), y0 + r0 * math.sin(a1)),
            (x1 + rb * math.cos(a1), y1 + rb * math.sin(a1)),
            (x1 + rb * math.cos(a2), y1 + rb * math.sin(a2)),
            (x0 + r0 * math.cos(a2), y0 + r0 * math.sin(a2)),
        ]
        self.poly(quad, fill=fill)

    def polyline(
        self,
        pts: Sequence[tuple[float, float]],
        width: float,
        fill: int = 255,
        closed: bool = False,
    ) -> None:
        r = width * 0.5
        seq = list(pts) + ([pts[0]] if closed and len(pts) >= 3 else [])
        for i in range(len(seq) - 1):
            self.capsule(seq[i][0], seq[i][1], seq[i + 1][0], seq[i + 1][1], r, r, fill=fill)

    def ribbon(
        self,
        ctrl_pts: Sequence[tuple[float, float]],
        w_start: float,
        w_end: float,
        w_mid: float | None = None,
        fill: int = 255,
        samples_per_seg: int = 24,
    ) -> None:
        """Smooth Catmull-Rom curve with interpolated variable stroke width."""
        dense = _catmull_rom(ctrl_pts, samples_per_seg=samples_per_seg)
        total = max(1, len(dense) - 1)

        def width_at(t: float) -> float:
            if w_mid is None:
                return w_start + (w_end - w_start) * t
            # Smooth quadratic Bezier width profile: w_start -> w_mid -> w_end
            u = 1.0 - t
            return u * u * w_start + 2.0 * u * t * w_mid + t * t * w_end

        for i in range(total):
            t0 = i / total
            t1 = (i + 1) / total
            r0 = max(0.05, width_at(t0) * 0.5)
            r1 = max(0.05, width_at(t1) * 0.5)
            self.capsule(dense[i][0], dense[i][1], dense[i + 1][0], dense[i + 1][1], r0, r1, fill=fill)

    def arc_ribbon(
        self,
        cx: float,
        cy: float,
        rx: float,
        ry: float,
        start_deg: float,
        end_deg: float,
        w_start: float,
        w_end: float,
        w_mid: float | None = None,
        rot_deg: float = 0.0,
        fill: int = 255,
        steps: int = 64,
    ) -> None:
        pts: list[tuple[float, float]] = []
        for i in range(steps + 1):
            t = i / steps
            ang = math.radians(start_deg + (end_deg - start_deg) * t)
            px = cx + rx * math.cos(ang)
            py = cy + ry * math.sin(ang)
            pts.append(_rot_pt(px, py, cx, cy, rot_deg))
        for i in range(steps):
            t0 = i / steps
            t1 = (i + 1) / steps
            if w_mid is None:
                w0 = w_start + (w_end - w_start) * t0
                w1 = w_start + (w_end - w_start) * t1
            else:
                w0 = (1 - t0) ** 2 * w_start + 2 * (1 - t0) * t0 * w_mid + t0**2 * w_end
                w1 = (1 - t1) ** 2 * w_start + 2 * (1 - t1) * t1 * w_mid + t1**2 * w_end
            self.capsule(pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], max(0.05, w0 * 0.5), max(0.05, w1 * 0.5), fill=fill)

    def star(
        self,
        cx: float,
        cy: float,
        r_outer: float,
        r_inner: float,
        points: int = 4,
        rot_deg: float = -90.0,
        fill: int = 255,
    ) -> None:
        verts: list[tuple[float, float]] = []
        for i in range(points * 2):
            ang = math.radians(rot_deg + 180.0 * i / points)
            rad = r_outer if i % 2 == 0 else r_inner
            verts.append((cx + rad * math.cos(ang), cy + rad * math.sin(ang)))
        self.poly(verts, fill=fill)

    def round_poly(self, pts: Sequence[tuple[float, float]], corner_r: float, fill: int = 255) -> None:
        self.poly(pts, fill=fill)
        if corner_r > 0.0:
            self.polyline(pts, corner_r * 2.0, fill=fill, closed=True)

    def finalize_rgba(self) -> Image.Image:
        """Downsample 8x supersampled mask to 128x128 pure-white RGBA8."""
        alpha_img = self.img.resize((ICON_SIZE, ICON_SIZE), resample=Image.Resampling.BOX)
        alpha_np = np.array(alpha_img, dtype=np.uint8)
        # Hard safety guard on outer margin (in case subpixel AA touches border)
        rgba = np.empty((ICON_SIZE, ICON_SIZE, 4), dtype=np.uint8)
        rgba[..., 0] = 255
        rgba[..., 1] = 255
        rgba[..., 2] = 255
        rgba[..., 3] = alpha_np
        return Image.fromarray(rgba, mode="RGBA")


# ---------------------------------------------------------------------------
# Core & Movement Icons (7)
# ---------------------------------------------------------------------------


def draw_tirador(c: IconCanvas) -> None:
    """Y-fork wooden slingshot + taut rubber bands + street pebble."""
    dx = 8.0
    cx, cy = 66.0 + dx, 65.0
    ang = -16.0

    def rp(x: float, y: float) -> tuple[float, float]:
        return _rot_pt(x + dx, y, cx, cy, ang)

    # Wide, classic carved wooden Y-fork centered optically at (64, 64)
    bot = rp(64.0, 102.5)
    crotch = rp(64.0, 70.0)
    fork_l_mid = rp(43.5, 55.0)
    fork_l_tip = rp(38.0, 33.5)
    fork_r_mid = rp(84.5, 55.0)
    fork_r_tip = rp(90.0, 33.5)

    c.capsule(*bot, *crotch, 5.4, 6.2, fill=255)
    c.ribbon([crotch, fork_l_mid, fork_l_tip], 10.2, 8.4, w_mid=9.2, fill=255)
    c.ribbon([crotch, fork_r_mid, fork_r_tip], 10.2, 8.4, w_mid=9.2, fill=255)
    c.circle(*fork_l_tip, 4.8, fill=255)
    c.circle(*fork_r_tip, 4.8, fill=255)

    # Negative-space grip wraps on wooden handle
    for wy in (82.5, 91.5):
        c.capsule(*rp(57.0, wy), *rp(71.0, wy), 1.55, 1.55, fill=0)

    # Negative-space rubber tie grooves at fork tips
    c.capsule(*rp(33.0, 36.0), *rp(43.0, 33.0), 1.4, 1.4, fill=0)
    c.capsule(*rp(85.0, 33.0), *rp(95.0, 36.0), 1.4, 1.4, fill=0)

    # Taut V-elastic bands pulled down into the open Y-space to leather pouch + pebble
    pouch = rp(64.0, 53.5)
    c.capsule(*rp(39.0, 33.5), *pouch, 2.0, 2.3, fill=255)
    c.capsule(*rp(89.0, 33.5), *pouch, 2.0, 2.3, fill=255)

    # Crisp negative-space halo around pouch & round street pebble
    stone = rp(64.0, 48.5)
    c.circle(*stone, 8.8, fill=0)
    c.arc_ribbon(64.0 + dx, 49.5, 8.6, 7.8, 22.0, 158.0, 2.8, 2.8, rot_deg=ang, fill=255)
    c.circle(*stone, 6.4, fill=255)
    c.circle(*rp(62.0, 46.6), 1.6, fill=0)

    # 3 crisp kinetic snap rays launching out between the forks
    for sx0, sy0, sx1, sy1 in [
        (64.0, 30.5, 64.0, 20.5),
        (52.5, 32.5, 47.5, 24.0),
        (75.5, 32.5, 80.5, 24.0),
    ]:
        c.capsule(*rp(sx0, sy0), *rp(sx1, sy1), 1.9, 0.9, fill=255)


def draw_ult_bagsak(c: IconCanvas) -> None:
    """Bagsak ng Kalye: plunging street meteor + ground shockwave star & seismic ring."""
    # Expanding ground seismic shockwave ellipse at base
    c.ellipse_ring(64.0, 91.0, 39.0, 11.5, 4.2, fill=255)
    # Clear upper center of ring so star & meteor burst cleanly out of ground
    c.ellipse(64.0, 86.0, 29.0, 11.0, fill=0)

    # Asymmetric 7-spike ground impact starburst erupting from impact point (64, 86)
    impact_pts = [
        (30.0, 89.0),
        (45.0, 82.0),
        (36.0, 67.0),
        (52.0, 74.0),
        (49.0, 55.0),
        (61.0, 69.0),
        (64.0, 49.0),
        (69.0, 69.0),
        (83.0, 56.0),
        (77.0, 75.0),
        (94.0, 69.0),
        (83.0, 82.0),
        (98.0, 89.0),
        (78.0, 88.0),
        (64.0, 95.0),
        (50.0, 88.0),
    ]
    c.poly(impact_pts, fill=255)

    # Carve negative-space crater channel around descending meteor core
    mx, my = 58.0, 49.0
    c.circle(mx, my, 17.2, fill=0)
    c.ribbon([(mx, my), (72.0, 34.0), (88.0, 22.0)], 31.0, 6.0, w_mid=22.0, fill=0)

    # Blazing meteor tail tongues sweeping from upper-right (88, 22) into core (58, 49)
    c.ribbon([(58.0, 49.0), (73.0, 33.0), (90.0, 21.0)], 25.0, 2.2, w_mid=15.0, fill=255)
    c.ribbon([(51.0, 43.0), (64.0, 28.0), (77.0, 20.0)], 12.0, 1.8, w_mid=7.5, fill=255)
    c.ribbon([(66.0, 55.0), (80.0, 42.0), (92.0, 34.0)], 12.0, 1.8, w_mid=7.5, fill=255)
    # Meteor core sphere
    c.circle(mx, my, 13.4, fill=255)
    # Negative-space meteor rock crater & tail split
    c.ribbon([(65.0, 42.0), (75.0, 31.5), (84.0, 24.5)], 3.4, 1.5, fill=0)
    c.circle(53.5, 52.5, 3.0, fill=0)
    c.circle(61.5, 55.0, 2.0, fill=0)


def draw_vault(c: IconCanvas) -> None:
    """Luksong-Tinik athletic hurdling silhouette vaulting over twin spine barrier."""
    dx = 4.0
    # Twin crossed 'tinik' hurdle spines at bottom center
    c.capsule(48.0 + dx, 103.5, 68.0 + dx, 88.5, 2.6, 1.4, fill=255)
    c.capsule(80.0 + dx, 103.5, 60.0 + dx, 88.5, 2.6, 1.4, fill=255)
    c.capsule(42.0 + dx, 104.0, 86.0 + dx, 104.0, 1.8, 1.8, fill=255)

    # Sweeping vault trajectory arc under the leaping kid
    c.arc_ribbon(63.0 + dx, 91.5, 36.0, 20.0, 198.0, 336.0, 1.6, 3.8, w_mid=3.2, fill=255)

    # Athletic hurdling kid silhouette (moving left -> right, torso angled dynamically forward)
    c.circle(76.5 + dx, 30.5, 7.2, fill=255)
    c.ribbon([(70.5 + dx, 28.5), (61.5 + dx, 25.5), (54.5 + dx, 27.5)], 3.4, 1.2, fill=255)
    c.ribbon([(70.5 + dx, 31.0), (62.5 + dx, 31.5), (57.0 + dx, 34.0)], 2.6, 1.0, fill=255)

    sh = (69.5 + dx, 41.0)
    hip = (54.5 + dx, 56.5)
    c.capsule(*sh, *hip, 6.8, 5.8, fill=255)

    knee_f = (76.0 + dx, 56.5)
    foot_f = (70.0 + dx, 72.5)
    c.capsule(*hip, *knee_f, 5.2, 4.1, fill=255)
    c.capsule(*knee_f, *foot_f, 3.9, 2.9, fill=255)
    c.capsule(*foot_f, 76.0 + dx, 74.0, 2.8, 2.2, fill=255)

    knee_b = (39.5 + dx, 64.0)
    foot_b = (25.5 + dx, 69.5)
    c.capsule(*hip, *knee_b, 5.0, 3.8, fill=255)
    c.capsule(*knee_b, *foot_b, 3.7, 2.6, fill=255)

    elbow_f = (82.0 + dx, 45.5)
    hand_f = (94.5 + dx, 41.0)
    c.capsule(*sh, *elbow_f, 3.7, 3.1, fill=255)
    c.capsule(*elbow_f, *hand_f, 3.0, 2.4, fill=255)

    elbow_b = (56.5 + dx, 37.5)
    hand_b = (44.5 + dx, 41.5)
    c.capsule(*sh, *elbow_b, 3.5, 2.9, fill=255)
    c.capsule(*elbow_b, *hand_b, 2.8, 2.2, fill=255)


def draw_jump(c: IconCanvas) -> None:
    """Upward athletic kid leap crowned by a bold Deadlock kinetic chevron."""
    # Bold crisp upward chevron apex cleanly separated above the leaping athlete!
    c.capsule(43.0, 34.0, 64.0, 21.0, 3.2, 4.0, fill=255)
    c.capsule(64.0, 21.0, 85.0, 34.0, 4.0, 3.2, fill=255)

    # Athletic kid springing dynamically skyward (one arm punching up, other swinging for lift!)
    c.circle(64.5, 47.5, 6.6, fill=255)
    sh = (63.5, 58.5)
    hip = (60.5, 74.5)
    c.capsule(*sh, *hip, 6.2, 5.2, fill=255)

    # Right arm punching skyward + left arm pumping dynamically outward
    c.capsule(67.5, 57.5, 79.0, 50.0, 3.3, 2.7, fill=255)
    c.capsule(79.0, 50.0, 84.0, 41.5, 2.7, 2.2, fill=255)
    c.capsule(59.5, 58.5, 47.0, 63.5, 3.3, 2.7, fill=255)
    c.capsule(47.0, 63.5, 40.5, 57.0, 2.7, 2.2, fill=255)

    # Driven-high right knee + extended push-off left leg
    knee_r = (76.0, 74.5)
    foot_r = (72.5, 87.0)
    c.capsule(*hip, *knee_r, 4.7, 3.7, fill=255)
    c.capsule(*knee_r, *foot_r, 3.5, 2.6, fill=255)

    knee_l = (55.0, 86.5)
    foot_l = (51.0, 99.0)
    c.capsule(*hip, *knee_l, 4.7, 3.6, fill=255)
    c.capsule(*knee_l, *foot_l, 3.4, 2.4, fill=255)

    # Ground launch impulse lines & lift ring arc
    c.arc_ribbon(64.0, 105.0, 20.0, 4.2, 195.0, 345.0, 1.7, 1.7, w_mid=2.6, fill=255)
    c.capsule(38.5, 96.0, 38.5, 84.5, 1.8, 1.0, fill=255)
    c.capsule(88.5, 96.0, 88.5, 84.5, 1.8, 1.0, fill=255)


def draw_crouch(c: IconCanvas) -> None:
    """Low stealth crouch silhouette + subtle downward posture chevron."""
    dx = 4.0
    # Compact downward posture chevron above back
    c.capsule(43.0 + dx, 27.0, 54.0 + dx, 35.5, 2.8, 3.2, fill=255)
    c.capsule(54.0 + dx, 35.5, 65.0 + dx, 27.0, 3.2, 2.8, fill=255)

    # Alert low-profile stealth kid crouched close to ground
    c.circle(78.5 + dx, 49.0, 7.4, fill=255)
    c.capsule(72.0 + dx, 45.5, 65.5 + dx, 46.5, 2.8, 1.8, fill=255)

    sh = (67.5 + dx, 56.5)
    hip = (44.0 + dx, 73.5)
    c.capsule(*sh, *hip, 7.0, 6.2, fill=255)

    knee_f = (69.5 + dx, 73.0)
    foot_f = (66.5 + dx, 95.5)
    c.capsule(*hip, *knee_f, 5.4, 4.3, fill=255)
    c.capsule(*knee_f, *foot_f, 4.1, 3.1, fill=255)
    c.capsule(*foot_f, 75.5 + dx, 96.0, 3.0, 2.4, fill=255)

    knee_b = (53.5 + dx, 89.5)
    foot_b = (36.5 + dx, 94.5)
    c.capsule(*hip, *knee_b, 5.0, 4.0, fill=255)
    c.capsule(*knee_b, *foot_b, 3.8, 2.9, fill=255)

    elbow = (72.0 + dx, 68.5)
    hand = (85.5 + dx, 73.5)
    c.capsule(*sh, *elbow, 3.8, 3.2, fill=255)
    c.capsule(*elbow, *hand, 3.1, 2.5, fill=255)

    c.capsule(28.0 + dx, 103.0, 92.0 + dx, 103.0, 1.8, 1.8, fill=255)


def draw_slide(c: IconCanvas) -> None:
    """Kinetic baseball ground slide silhouette with horizontal speed lines."""
    dy = -6.0
    # 3 crisp horizontal kinetic speed streaks on the left
    c.capsule(21.0, 52.0 + dy, 42.0, 52.0 + dy, 1.2, 2.7, fill=255)
    c.capsule(19.0, 66.0 + dy, 37.0, 66.0 + dy, 1.2, 2.9, fill=255)
    c.capsule(23.0, 80.0 + dy, 45.0, 80.0 + dy, 1.2, 2.7, fill=255)

    # Reclined baseball-sliding kid moving fast left -> right
    c.circle(56.5, 48.0 + dy, 7.0, fill=255)
    c.ribbon([(50.5, 46.5 + dy), (42.0, 44.0 + dy), (35.5, 45.0 + dy)], 3.0, 1.1, fill=255)

    sh = (59.0, 59.5 + dy)
    hip = (71.0, 80.5 + dy)
    c.capsule(*sh, *hip, 6.6, 5.8, fill=255)

    knee_lead = (87.5, 83.5 + dy)
    foot_lead = (104.0, 85.5 + dy)
    c.capsule(*hip, *knee_lead, 5.1, 4.0, fill=255)
    c.capsule(*knee_lead, *foot_lead, 3.9, 2.8, fill=255)
    c.capsule(*foot_lead, 107.0, 79.0 + dy, 2.7, 2.1, fill=255)

    knee_tuck = (59.5, 90.5 + dy)
    foot_tuck = (77.0, 92.5 + dy)
    c.capsule(*hip, *knee_tuck, 4.7, 3.8, fill=255)
    c.capsule(*knee_tuck, *foot_tuck, 3.6, 2.7, fill=255)

    c.capsule(*sh, 46.5, 65.5 + dy, 3.5, 2.8, fill=255)
    c.capsule(46.5, 65.5 + dy, 37.5, 73.0 + dy, 2.8, 2.2, fill=255)
    c.capsule(*sh, 74.5, 63.0 + dy, 3.5, 2.8, fill=255)
    c.capsule(74.5, 63.0 + dy, 85.0, 60.0 + dy, 2.7, 2.2, fill=255)

    c.capsule(31.0, 99.5 + dy, 106.5, 99.5 + dy, 1.6, 2.3, fill=255)
    c.capsule(98.5, 95.0 + dy, 107.0, 90.5 + dy, 1.8, 0.9, fill=255)


def draw_gear(c: IconCanvas) -> None:
    """Sleek thin-line 6-tooth settings cog (crisp at 34px and 128px)."""
    cx, cy = 64.0, 64.0
    n_teeth = 6
    r_root = 30.5
    r_tip = 42.5
    # Build continuous 6-tooth outer contour polygon and stroke it cleanly!
    contour: list[tuple[float, float]] = []
    for i in range(n_teeth):
        base_deg = i * (360.0 / n_teeth) - 90.0
        # Tooth angular profile: valley -> rising flank -> flat crown -> falling flank -> valley
        profile = [
            (-17.5, r_root),
            (-11.0, r_tip),
            (11.0, r_tip),
            (17.5, r_root),
        ]
        # Smooth root arc samples to next tooth
        for a_off, rad in profile:
            rad_ang = math.radians(base_deg + a_off)
            contour.append((cx + rad * math.cos(rad_ang), cy + rad * math.sin(rad_ang)))
        for s in range(1, 6):
            a_arc = base_deg + 17.5 + (42.5 - 17.5) * (s / 6.0)
            rad_ang = math.radians(a_arc)
            contour.append((cx + r_root * math.cos(rad_ang), cy + r_root * math.sin(rad_ang)))

    # Sleek thin-line outer 6-tooth perimeter + crisp inner bearing ring
    c.polyline(contour, width=6.2, fill=255, closed=True)
    c.ring(cx, cy, 16.5, 10.5, fill=255)


# ---------------------------------------------------------------------------
# All 14 Street-Game Weapon Icons (`WeaponDef.id` exact filenames)
# ---------------------------------------------------------------------------


def _faceted_stone(c: IconCanvas, cx: float, cy: float, r: float, rot_deg: float = 0.0, fill: int = 255) -> None:
    ratios = [1.04, 0.92, 1.06, 0.94, 1.02, 0.90, 1.00]
    pts: list[tuple[float, float]] = []
    for i, k in enumerate(ratios):
        ang = math.radians(rot_deg + i * (360.0 / len(ratios)))
        pts.append((cx + r * k * math.cos(ang), cy + r * k * math.sin(ang)))
    c.round_poly(pts, corner_r=r * 0.16, fill=fill)


def draw_bato_light(c: IconCanvas) -> None:
    """Bato Light (Habol-Bato): 3 agile homing street pebbles along curved lock-on arcs."""
    # Homing swoosh trails curving from bottom-left toward upper-right target
    c.ribbon([(22.0, 92.0), (44.0, 74.0), (74.0, 44.0)], 2.0, 7.5, w_mid=5.0, fill=255)
    c.ribbon([(26.0, 66.0), (42.0, 50.0), (58.0, 36.0)], 1.6, 5.2, w_mid=3.6, fill=255)
    c.ribbon([(44.0, 98.0), (64.0, 82.0), (84.0, 64.0)], 1.6, 5.4, w_mid=3.6, fill=255)

    # Clear separation halos around the 3 pebbles
    for px, py, pr, rot in [(80.0, 39.0, 12.5, 18.0), (59.5, 34.0, 8.2, -20.0), (86.5, 62.5, 8.6, 35.0)]:
        c.circle(px, py, pr + 2.8, fill=0)
        _faceted_stone(c, px, py, pr, rot_deg=rot, fill=255)

    # Negative-space facet highlight on lead pebble
    c.capsule(75.5, 35.0, 82.5, 33.5, 1.5, 1.2, fill=0)

    # Homing reticle ticks locked ahead of the lead stone
    c.capsule(95.5, 23.5, 101.5, 19.5, 1.9, 1.1, fill=255)
    c.capsule(96.5, 36.0, 103.5, 36.0, 1.8, 1.0, fill=255)
    c.capsule(80.5, 22.0, 80.5, 17.5, 1.8, 1.0, fill=255)


def draw_bato_heavy(c: IconCanvas) -> None:
    """Bato Heavy (Bulalakaw): 1 massive craggy meteor boulder crashing with stun blast."""
    # Ground shatter spikes & seismic crater at bottom
    shatter = [
        (23.0, 99.0),
        (39.0, 92.0),
        (33.0, 81.0),
        (49.0, 87.0),
        (64.0, 94.0),
        (79.0, 87.0),
        (95.0, 80.0),
        (89.0, 92.0),
        (105.0, 99.0),
        (82.0, 100.0),
        (64.0, 104.0),
        (46.0, 100.0),
    ]
    c.poly(shatter, fill=255)

    # Swept diagonal meteor entry streaks & trailing debris shards (upper-left -> boulder)
    c.capsule(28.0, 21.0, 46.0, 39.0, 1.4, 3.2, fill=255)
    c.capsule(42.0, 18.0, 57.0, 33.0, 1.4, 2.8, fill=255)
    c.capsule(22.0, 35.0, 38.0, 51.0, 1.3, 2.6, fill=255)
    c.poly([(33.0, 27.0), (39.0, 25.0), (41.0, 31.0), (35.0, 33.0)], fill=255)

    # Sharp-faceted colossal 8-sided boulder polygon (unmistakable jagged rock silhouette!)
    boulder_halo = [
        (46.0, 36.0),
        (69.0, 32.0),
        (88.0, 43.0),
        (93.0, 65.0),
        (81.0, 85.0),
        (56.0, 89.0),
        (38.0, 77.0),
        (35.0, 53.0),
    ]
    c.round_poly(boulder_halo, corner_r=3.6, fill=0)

    boulder = [
        (48.0, 39.0),
        (68.0, 35.5),
        (85.0, 45.5),
        (89.5, 64.5),
        (79.0, 82.0),
        (57.0, 85.5),
        (41.0, 74.5),
        (38.5, 54.0),
    ]
    c.round_poly(boulder, corner_r=1.6, fill=255)

    # Bold negative-space rock facet ridges & tectonic fracture planes inside boulder
    c.polyline([(48.0, 53.0), (63.5, 61.5), (84.0, 47.0)], width=2.8, fill=0)
    c.polyline([(63.5, 61.5), (58.5, 84.0)], width=2.8, fill=0)
    c.polyline([(63.5, 61.5), (78.0, 69.5)], width=2.2, fill=0)

    # Crisp 4-point stun star on upper-right impact shoulder
    c.star(95.0, 31.0, 9.0, 2.8, points=4, rot_deg=0.0, fill=255)


def draw_gunting_light(c: IconCanvas) -> None:
    """Gunting Light (Gupit-Kidlat): Agile open scissors + 3 rapid cone slash arcs."""
    # 3 rapid lightning-flurry slash crescents fanning across upper-right cone
    c.arc_ribbon(46.0, 80.0, 42.0, 42.0, -80.0, -10.0, 1.3, 1.3, w_mid=4.2, fill=255)
    c.arc_ribbon(46.0, 80.0, 54.0, 54.0, -76.0, -14.0, 1.3, 1.3, w_mid=4.6, fill=255)
    c.arc_ribbon(46.0, 80.0, 66.0, 66.0, -70.0, -20.0, 1.3, 1.3, w_mid=4.0, fill=255)

    # Agile open scissors angled toward upper-right with needle-sharp tapered tips!
    px, py = 46.0, 78.0
    # Upper blade tapering cleanly to razor tip (73, 35)
    c.poly([(px - 3.8, py + 1.0), (px + 3.2, py - 3.2), (73.0, 35.0)], fill=255)
    # Lower blade tapering cleanly to razor tip (87, 56)
    c.poly([(px - 1.5, py - 3.8), (px + 3.5, py + 3.5), (87.0, 56.0)], fill=255)
    # Shanks to finger loops
    c.capsule(px, py, 32.0, 83.0, 3.4, 3.0, fill=255)
    c.capsule(px, py, 41.0, 93.0, 3.4, 3.0, fill=255)
    # Finger rings with negative-space holes
    c.ring(26.0, 84.0, 9.0, 4.4, fill=255)
    c.ring(41.0, 99.0, 9.0, 4.4, fill=255)
    # Negative-space pivot screw
    c.circle(px + 0.8, py - 0.5, 2.1, fill=0)


def draw_gunting_heavy(c: IconCanvas) -> None:
    """Gunting Heavy (Hati-Kalye): Wide heavy street shears + massive cleave shockwave."""
    # Single monumental heavy cleave wedge bursting straight upward between the wide jaws
    cleave = [
        (64.0, 18.0),
        (77.0, 41.0),
        (69.5, 39.0),
        (64.0, 56.0),
        (58.5, 39.0),
        (51.0, 41.0),
    ]
    c.poly(cleave, fill=255)

    # Heavy wide-jawed shears centered symmetrically around pivot (64, 72)
    px, py = 64.0, 72.0
    # Left heavy broad blade
    blade_l = [
        (px + 4.0, py + 3.0),
        (px - 6.0, py - 3.0),
        (29.0, 31.0),
        (27.0, 42.0),
        (48.0, 65.0),
    ]
    # Right heavy broad blade
    blade_r = [
        (px - 4.0, py + 3.0),
        (px + 6.0, py - 3.0),
        (99.0, 31.0),
        (101.0, 42.0),
        (80.0, 65.0),
    ]
    c.round_poly(blade_l, corner_r=1.5, fill=255)
    c.round_poly(blade_r, corner_r=1.5, fill=255)

    # Heavy handle shanks & bold oval grip loops
    c.capsule(px, py, 46.0, 92.0, 4.8, 4.2, fill=255)
    c.capsule(px, py, 82.0, 92.0, 4.8, 4.2, fill=255)
    c.ellipse_ring(40.5, 97.5, 11.5, 8.5, 5.2, rot_deg=-28.0, fill=255)
    c.ellipse_ring(87.5, 97.5, 11.5, 8.5, 5.2, rot_deg=28.0, fill=255)

    # Central heavy bolt collar + negative rivet hole
    c.circle(px, py, 7.0, fill=255)
    c.circle(px, py, 2.8, fill=0)


def draw_papel_trap(c: IconCanvas) -> None:
    """Papel Trap (Patibong): Segmented ground snare perimeter + origami paper caltrop."""
    cx, cy = 64.0, 70.0
    # Segmented elliptical ground trap perimeter
    for a0, a1 in [(12, 58), (74, 106), (122, 168), (192, 238), (254, 286), (302, 348)]:
        c.arc_ribbon(cx, cy, 42.0, 22.0, a0, a1, 3.6, 3.6, fill=255)

    # Inward-snapping snare teeth around perimeter
    for ang_deg in (35.0, 145.0, 215.0, 325.0):
        ar = math.radians(ang_deg)
        bx = cx + 40.0 * math.cos(ar)
        by = cy + 21.0 * math.sin(ar)
        tx = cx + 27.0 * math.cos(ar)
        ty = cy + 12.5 * math.sin(ar) - 4.0
        nx, ny = -math.sin(ar) * 4.5, math.cos(ar) * 2.6
        c.poly([(bx - nx, by - ny), (bx + nx, by + ny), (tx, ty)], fill=255)

    # Folded 4-wing origami paper snare star sitting in center (cx, cy - 8)
    ox, oy = 64.0, 60.0
    c.star(ox, oy, 22.0, 8.5, points=4, rot_deg=-18.0, fill=255)
    # Crisp negative-space origami fold creases across center
    c.capsule(ox - 13.0, oy - 4.5, ox + 13.0, oy + 4.5, 1.5, 1.5, fill=0)
    c.capsule(ox + 4.5, oy - 13.0, ox - 4.5, oy + 13.0, 1.5, 1.5, fill=0)
    c.circle(ox, oy, 3.0, fill=255)


def draw_papel_shield(c: IconCanvas) -> None:
    """Papel Shield (Harang-Hangin): 3-panel pleated origami barrier + wind-gust ribbons."""
    # Left pleated origami wing
    wing_l = [(26.0, 38.0), (47.0, 27.0), (47.0, 88.0), (29.0, 96.0)]
    # Center tall origami shield panel
    panel_c = [(51.5, 25.0), (76.5, 25.0), (76.5, 92.0), (51.5, 92.0)]
    # Right pleated origami wing
    wing_r = [(81.0, 27.0), (102.0, 38.0), (99.0, 96.0), (81.0, 88.0)]
    # Give center panel a crisp pointed origami crown & chevron bottom!
    crown_c = [(51.5, 28.0), (64.0, 20.0), (76.5, 28.0), (76.5, 90.0), (64.0, 99.0), (51.5, 90.0)]

    c.round_poly(wing_l, corner_r=1.6, fill=255)
    c.round_poly(crown_c, corner_r=1.6, fill=255)
    c.round_poly(wing_r, corner_r=1.6, fill=255)

    # Diagonal origami paper-fold crease lines carved inside panels
    c.capsule(51.5, 52.0, 64.0, 42.0, 1.5, 1.5, fill=0)
    c.capsule(64.0, 42.0, 76.5, 52.0, 1.5, 1.5, fill=0)

    # Sweeping wind-wall gust ribbons curling across the lower/front face (with negative halo)
    gust1 = [(20.0, 69.0), (50.0, 62.0), (84.0, 68.0), (106.0, 58.0)]
    gust2 = [(32.0, 83.0), (62.0, 78.0), (94.0, 83.0), (107.0, 76.0)]
    c.ribbon(gust1, 5.5, 4.5, w_mid=8.5, fill=0)
    c.ribbon(gust2, 4.8, 4.0, w_mid=7.2, fill=0)
    c.ribbon(gust1, 2.2, 1.8, w_mid=4.2, fill=255)
    c.ribbon(gust2, 1.8, 1.6, w_mid=3.6, fill=255)


def _draw_slipper(
    c: IconCanvas,
    cx: float,
    cy: float,
    scale: float,
    rot_deg: float,
    double_sole: bool = False,
) -> None:
    """Classic contoured Pinoy rubber tsinelas silhouette with negative-space Y-strap."""

    def tp(dx: float, dy: float) -> tuple[float, float]:
        return _rot_pt(cx + dx * scale, cy + dy * scale, cx, cy, rot_deg)

    # Clear thin outer halo when overlapping in multi-slipper compositions
    c.capsule(*tp(0.0, -17.0), *tp(0.0, 19.0), 15.8 * scale, 12.6 * scale, fill=0)

    # Contoured sole: wider forefoot (-y), tapered waist, rounded heel (+y)
    c.capsule(*tp(0.0, -17.0), *tp(0.0, 2.0), 13.2 * scale, 10.6 * scale, fill=255)
    c.capsule(*tp(0.0, 2.0), *tp(0.0, 19.0), 10.6 * scale, 10.2 * scale, fill=255)

    if double_sole:
        # Subtle outsole rim groove near heel edge for mega-slipper heft
        c.arc_ribbon(
            cx,
            cy + 18.0 * scale,
            8.0 * scale,
            7.2 * scale,
            25.0,
            155.0,
            1.6 * scale,
            1.6 * scale,
            rot_deg=rot_deg,
            fill=0,
        )

    # Negative-space Y-thong strap cutouts + toe-plug button
    toe = tp(0.0, -12.5)
    strap_l = tp(-11.2, 5.5)
    strap_r = tp(11.2, 5.5)
    w_cut = max(1.8, 3.8 * scale)
    c.capsule(*toe, *strap_l, w_cut * 0.55, w_cut * 0.62, fill=0)
    c.capsule(*toe, *strap_r, w_cut * 0.55, w_cut * 0.62, fill=0)
    c.circle(*toe, max(2.0, 3.1 * scale), fill=0)
    c.circle(*toe, max(1.0, 1.45 * scale), fill=255)


def draw_tsinelas_light(c: IconCanvas) -> None:
    """Tsinelas Light (Tatlong-Hirit): 3 agile boomerang slippers fanning in flight."""
    dy = 7.5
    # Curved boomerang return swoosh behind the 3 slippers
    c.arc_ribbon(64.0, 68.0 + dy, 40.0, 34.0, 150.0, 345.0, 1.5, 1.5, w_mid=4.0, fill=255)

    # 3 distinct Pinoy tsinelas in a fanned triple-shot formation
    _draw_slipper(c, 35.5, 67.0 + dy, scale=0.66, rot_deg=-36.0)
    _draw_slipper(c, 92.5, 67.0 + dy, scale=0.66, rot_deg=36.0)
    _draw_slipper(c, 64.0, 49.0 + dy, scale=0.76, rot_deg=0.0)


def draw_tsinelas_heavy(c: IconCanvas) -> None:
    """Tsinelas Heavy (Lipad-Nanay): 1 colossal returning mega-slipper + heavy boomerang arc."""
    # Heavy wide boomerang return swoosh orbiting the giant slipper
    c.arc_ribbon(64.0, 64.0, 43.5, 38.0, 128.0, 392.0, 1.8, 5.8, w_mid=4.8, fill=255)
    # Arrowhead at end of return arc (angle 392 deg = 32 deg)
    ah_ang = math.radians(32.0)
    ax = 64.0 + 43.5 * math.cos(ah_ang)
    ay = 64.0 + 38.0 * math.sin(ah_ang)
    c.poly([(ax + 3.0, ay + 8.5), (ax - 9.0, ay - 2.0), (ax + 5.5, ay - 6.5)], fill=255)

    # 1 giant heroic Pinoy tsinelas tilted dynamically at 28 deg
    _draw_slipper(c, 63.0, 63.0, scale=1.22, rot_deg=28.0, double_sole=True)

    # Heavy impact shock ticks off upper-right toe
    c.capsule(88.0, 26.0, 95.0, 20.0, 2.0, 1.0, fill=255)
    c.capsule(95.0, 35.0, 103.0, 32.0, 2.0, 1.0, fill=255)


def draw_lata(c: IconCanvas) -> None:
    """Lata (Tumbang Preso Can Hex): Tilted corrugated tin milk can + hex swirl & stars."""
    cx, cy = 64.0, 64.0
    ang = 20.0

    def rp(x: float, y: float) -> tuple[float, float]:
        return _rot_pt(x, y, cx, cy, ang)

    # Polymorph hex swirl orbiting behind & around the tin can
    c.arc_ribbon(64.0, 65.0, 43.0, 22.0, 155.0, 385.0, 1.5, 4.2, w_mid=3.4, rot_deg=-18.0, fill=255)

    # Clear halo for can body
    w_can = 20.5
    h_top, h_bot = 39.0, 89.0
    body_halo = [
        rp(cx - w_can - 2.6, h_top),
        rp(cx + w_can + 2.6, h_top),
        rp(cx + w_can + 2.6, h_bot),
        rp(cx - w_can - 2.6, h_bot),
    ]
    c.poly(body_halo, fill=0)
    c.ellipse(*rp(cx, h_top), w_can + 2.8, 8.8, rot_deg=ang, fill=0)
    c.ellipse(*rp(cx, h_bot), w_can + 2.8, 8.8, rot_deg=ang, fill=0)

    # Solid cylindrical tin can silhouette + top & bottom rolled rims
    body_quad = [
        rp(cx - w_can, h_top),
        rp(cx + w_can, h_top),
        rp(cx + w_can, h_bot),
        rp(cx - w_can, h_bot),
    ]
    c.poly(body_quad, fill=255)
    c.ellipse(*rp(cx, h_top), w_can + 1.2, 7.6, rot_deg=ang, fill=255)
    c.ellipse(*rp(cx, h_bot), w_can + 1.2, 7.6, rot_deg=ang, fill=255)

    # Recessed negative-space elliptical top lid + inner ring
    c.ellipse_ring(*rp(cx, h_top), w_can - 3.2, 5.0, 2.3, rot_deg=ang, fill=0)

    # 3 classic horizontal tin-can corrugation ribs (curved elliptical arcs)
    for ry_pos in (53.5, 65.0, 76.5):
        rc = rp(cx, ry_pos)
        c.arc_ribbon(rc[0], rc[1], w_can - 1.5, 6.2, 12.0, 168.0, 2.3, 2.3, rot_deg=ang, fill=0)

    # Hex polymorph 4-point sparkles flanking the can
    c.star(27.0, 40.0, 8.5, 2.6, points=4, rot_deg=0.0, fill=255)
    c.star(100.0, 82.0, 7.5, 2.3, points=4, rot_deg=15.0, fill=255)


def _draw_jackstone_3d(c: IconCanvas, cx: float, cy: float, r: float, rot_deg: float) -> None:
    """6-pronged 3D jackstone star (4 ball-tipped arms + 2 pointed axis spikes + 3D overlap)."""

    def jp(dx: float, dy: float) -> tuple[float, float]:
        return _rot_pt(cx + dx * r, cy + dy * r, cx, cy, rot_deg)

    # Back diagonal bar (ball-tipped)
    b0, b1 = jp(-0.88, -0.52), jp(0.88, 0.52)
    c.capsule(*b0, *b1, r * 0.16, r * 0.16, fill=255)
    c.circle(*b0, r * 0.27, fill=255)
    c.circle(*b1, r * 0.27, fill=255)

    # Pointed vertical/3D spike axis (characteristic 2 pointed prongs of a real jackstone!)
    s0, s1 = jp(0.08, -0.98), jp(-0.08, 0.98)
    c.capsule(cx, cy, *s0, r * 0.19, r * 0.06, fill=255)
    c.capsule(cx, cy, *s1, r * 0.19, r * 0.06, fill=255)

    # Subtle negative-space 3D collar before front cross-bar so crossing reads in 3D
    f0, f1 = jp(-0.82, 0.58), jp(0.82, -0.58)
    c.capsule(*jp(-0.42, 0.30), *jp(0.42, -0.30), r * 0.25, r * 0.25, fill=0)

    # Front diagonal bar (ball-tipped) + central nexus hub
    c.capsule(*f0, *f1, r * 0.17, r * 0.17, fill=255)
    c.circle(*f0, r * 0.28, fill=255)
    c.circle(*f1, r * 0.28, fill=255)
    c.circle(cx, cy, r * 0.22, fill=255)


def draw_jacks(c: IconCanvas) -> None:
    """Jacks (Tinik-Kalye): Scattered spiked 3D jackstones with clean air separation."""
    # Main foreground 6-pronged jackstone (lower-left) + cleanly separated upper-right jackstone
    _draw_jackstone_3d(c, 46.5, 72.5, r=24.5, rot_deg=-10.0)
    _draw_jackstone_3d(c, 87.5, 36.5, r=14.8, rot_deg=24.0)
    # Spiky caltrop spark at lower-right + upper-left glint
    c.star(91.0, 83.5, 8.0, 2.4, points=4, rot_deg=0.0, fill=255)
    c.star(34.0, 33.0, 6.0, 1.9, points=4, rot_deg=15.0, fill=255)


def draw_bola(c: IconCanvas) -> None:
    """Bola (Talbog-Bola): Glossy rubber ball + double-ricochet bounce trajectory."""
    # Ground line ticks & V-splash impacts at bounce 1 (29, 96) and bounce 2 (62, 96)
    b1 = (28.0, 95.0)
    b2 = (60.0, 95.0)
    ball = (89.0, 40.0)

    # Bounce 1 -> Bounce 2 parabolic arc
    c.ribbon([b1, (44.0, 63.0), b2], 2.4, 3.2, w_mid=3.6, fill=255)
    # Bounce 2 -> High ricochet into ball
    c.ribbon([b2, (69.0, 52.0), ball], 3.4, 7.2, w_mid=5.4, fill=255)
    # Entry tail before bounce 1
    c.ribbon([(19.0, 76.0), (23.5, 86.0), b1], 1.6, 2.4, fill=255)

    # Crisp ground bounce splashes
    for bx, by in (b1, b2):
        c.capsule(bx - 6.5, by + 3.5, bx + 6.5, by + 3.5, 1.6, 1.6, fill=255)
        c.capsule(bx, by, bx - 7.0, by - 5.5, 1.5, 0.8, fill=255)
        c.capsule(bx, by, bx + 7.0, by - 5.5, 1.5, 0.8, fill=255)

    # Separation halo + super-bouncing rubber ball
    c.circle(ball[0], ball[1], 18.8, fill=0)
    c.circle(ball[0], ball[1], 16.0, fill=255)
    # Glossy crescent highlight & seam curve carved inside ball
    c.arc_ribbon(ball[0], ball[1], 10.5, 10.5, 200.0, 290.0, 1.5, 1.5, w_mid=3.0, fill=0)


def draw_trumpo(c: IconCanvas) -> None:
    """Trumpo (Buhawi): Classic pear-shaped Filipino spinning top + cyclone vortex ring."""
    cx, cy = 64.0, 63.0
    ang = 15.0

    def rp(x: float, y: float) -> tuple[float, float]:
        return _rot_pt(x, y, cx, cy, ang)

    # Back half of cyclone vortex ellipse
    vortex_c = rp(cx, cy - 2.0)
    c.arc_ribbon(vortex_c[0], vortex_c[1], 42.5, 14.0, 185.0, 355.0, 2.2, 2.2, w_mid=3.6, rot_deg=ang, fill=255)

    # Smooth spline-revolved pear-shaped Filipino trumpo body + rounded 'ulo' crown
    body_ctrl = [
        (-28.5, 4.5),
        (-24.5, 15.0),
        (-16.0, 23.0),
        (-5.5, 21.8),
        (7.5, 15.0),
        (18.5, 7.6),
        (23.5, 4.0),
    ]
    smooth_right = _catmull_rom(body_ctrl, samples_per_seg=12)
    poly_pts: list[tuple[float, float]] = [rp(cx + w, cy + y) for (y, w) in smooth_right]
    poly_pts += [rp(cx - w, cy + y) for (y, w) in reversed(smooth_right)]
    c.poly(poly_pts, fill=255)

    # Distinct turned wooden 'ulo' crown knob at top
    c.capsule(*rp(cx - 5.2, cy - 35.0), *rp(cx + 5.2, cy - 35.0), 4.0, 4.0, fill=255)
    c.capsule(*rp(cx, cy - 35.0), *rp(cx, cy - 27.5), 4.2, 4.2, fill=255)

    # Sharp conical steel nail spike ('pako') at bottom tip
    c.capsule(*rp(cx, cy + 23.0), *rp(cx, cy + 39.0), 3.6, 1.0, fill=255)

    # 2 classic curved lathe-carved stripe grooves across the trumpo body
    for gy, gw in [(-14.5, 21.5), (-2.5, 19.2)]:
        gc = rp(cx, cy + gy)
        c.arc_ribbon(gc[0], gc[1], gw, 4.8, 12.0, 168.0, 2.3, 2.3, rot_deg=ang, fill=0)

    # Front half of cyclone vortex ribbon cutting dynamically across with negative-space depth!
    c.arc_ribbon(vortex_c[0], vortex_c[1], 42.5, 14.0, -8.0, 172.0, 4.8, 4.8, w_mid=8.2, rot_deg=ang, fill=0)
    c.arc_ribbon(vortex_c[0], vortex_c[1], 42.5, 14.0, -12.0, 176.0, 1.8, 1.8, w_mid=4.4, rot_deg=ang, fill=255)

    # Secondary lower mini vortex swirl around the spinning nail tip
    c.arc_ribbon(*rp(cx, cy + 36.0), 15.0, 5.0, -20.0, 215.0, 1.2, 2.3, w_mid=2.1, rot_deg=ang, fill=255)


def draw_heal(c: IconCanvas) -> None:
    """Heal (Basbas-Araw): Iconic 8-ray Philippine sun-burst framing a bold vitality cross."""
    cx, cy = 64.0, 64.0
    # 8 classic Philippine sun ray clusters (center tall spear + 2 flanking shorter spears per ray)
    for i in range(8):
        base_deg = i * 45.0 - 90.0
        for sub_off, r_in, r_out, half_w in [
            (0.0, 26.5, 45.0, 3.4),
            (-7.5, 27.0, 37.5, 2.0),
            (7.5, 27.0, 37.5, 2.0),
        ]:
            ang = math.radians(base_deg + sub_off)
            nx, ny = -math.sin(ang) * half_w, math.cos(ang) * half_w
            bx, by = cx + r_in * math.cos(ang), cy + r_in * math.sin(ang)
            tx, ty = cx + r_out * math.cos(ang), cy + r_out * math.sin(ang)
            c.poly([(bx - nx, by - ny), (bx + nx, by + ny), (tx, ty)], fill=255)

    # Radiant sun disc separated from rays by crisp negative-space corona ring
    c.circle(cx, cy, 27.0, fill=0)
    c.circle(cx, cy, 23.5, fill=255)

    # Bold central negative-space vitality/heal cross ('+') inside the sun disc
    arm_len = 13.0
    arm_r = 4.4
    c.capsule(cx - arm_len, cy, cx + arm_len, cy, arm_r, arm_r, fill=0)
    c.capsule(cx, cy - arm_len, cx, cy + arm_len, arm_r, arm_r, fill=0)


def draw_langit_lupa(c: IconCanvas) -> None:
    """Langit Lupa (Pader-Bato): 3 rugged stone/earth monolith pillars erupting from ground."""
    # Left angled stone slab
    slab_l = [
        (25.0, 95.0),
        (27.5, 49.0),
        (37.5, 38.0),
        (46.5, 44.5),
        (46.5, 95.0),
    ]
    # Tall central rugged earth monolith
    slab_c = [
        (50.0, 95.0),
        (50.0, 34.5),
        (59.0, 22.0),
        (69.5, 25.0),
        (78.0, 35.5),
        (78.0, 95.0),
    ]
    # Right angled stone slab
    slab_r = [
        (81.5, 95.0),
        (81.5, 46.5),
        (91.5, 39.0),
        (100.5, 50.5),
        (103.0, 95.0),
    ]
    c.round_poly(slab_l, corner_r=1.4, fill=255)
    c.round_poly(slab_c, corner_r=1.4, fill=255)
    c.round_poly(slab_r, corner_r=1.4, fill=255)

    # Ground upheaval base bar + negative-space tectonic crack fissures (comfortably >= 18px margin!)
    c.capsule(22.5, 97.5, 105.5, 97.5, 3.6, 3.6, fill=255)
    c.capsule(21.0, 92.2, 107.0, 92.2, 1.5, 1.5, fill=0)

    # Jagged rock-strata cracks inside monoliths
    c.polyline([(59.0, 40.5), (65.5, 53.0), (60.5, 69.0)], width=2.5, fill=0)
    c.polyline([(34.5, 56.5), (39.0, 68.0)], width=2.2, fill=0)
    c.polyline([(92.5, 55.5), (88.5, 69.0)], width=2.2, fill=0)

    # Upward erupting earth pebbles flanking crown
    _faceted_stone(c, 38.0, 27.5, 3.9, rot_deg=20.0, fill=255)
    _faceted_stone(c, 90.5, 28.5, 3.7, rot_deg=-25.0, fill=255)


# ---------------------------------------------------------------------------
# Registry (Exact 21 Required Icons) & Godot .import Writer
# ---------------------------------------------------------------------------

ICONS: list[tuple[str, Callable[[IconCanvas], None]]] = [
    # Core & Movement (7)
    ("tirador", draw_tirador),
    ("ult_bagsak", draw_ult_bagsak),
    ("vault", draw_vault),
    ("jump", draw_jump),
    ("crouch", draw_crouch),
    ("slide", draw_slide),
    ("gear", draw_gear),
    # All 14 Street-Game Weapons (`WeaponDef.id` exact match)
    ("bato_light", draw_bato_light),
    ("bato_heavy", draw_bato_heavy),
    ("gunting_light", draw_gunting_light),
    ("gunting_heavy", draw_gunting_heavy),
    ("papel_trap", draw_papel_trap),
    ("papel_shield", draw_papel_shield),
    ("tsinelas_light", draw_tsinelas_light),
    ("tsinelas_heavy", draw_tsinelas_heavy),
    ("lata", draw_lata),
    ("jacks", draw_jacks),
    ("bola", draw_bola),
    ("trumpo", draw_trumpo),
    ("heal", draw_heal),
    ("langit_lupa", draw_langit_lupa),
]


def write_lossless_import(png_path: Path, res_path: str) -> None:
    """Write lossless Texture2D .import sidecar without md5= or generator_parameters."""
    content = f"""[remap]

importer="texture"
type="CompressedTexture2D"

[deps]

source_file="{res_path}"

[params]

compress/mode=0
compress/high_quality=false
compress/lossy_quality=0.7
compress/uastc_level=0
compress/rdo_quality_loss=0.0
compress/hdr_compression=1
compress/normal_map=0
compress/channel_pack=0
mipmaps/generate=false
mipmaps/limit=-1
roughness/mode=0
roughness/src_normal=""
process/channel_remap/red=0
process/channel_remap/green=1
process/channel_remap/blue=2
process/channel_remap/alpha=3
process/fix_alpha_border=true
process/premult_alpha=false
process/normal_map_invert_y=false
process/hdr_as_srgb=false
process/hdr_clamp_exposure=false
process/size_limit=0
detect_3d/compress_to=1
"""
    import_path = png_path.with_name(png_path.name + ".import")
    import_path.write_text(content, encoding="utf-8", newline="\n")


def validate_icon(name: str, img: Image.Image) -> tuple[int, tuple[int, int, int, int]]:
    if img.mode != "RGBA" or img.size != (ICON_SIZE, ICON_SIZE):
        raise ValueError(f"{name}: expected 128x128 RGBA, got {img.size} {img.mode}")
    arr = np.array(img, dtype=np.uint8)
    rgb = arr[..., :3]
    alpha = arr[..., 3]
    if not np.all(rgb == 255):
        bad = np.argwhere(np.any(rgb != 255, axis=-1))
        raise ValueError(f"{name}: non-white RGB pixel at {tuple(bad[0])}")
    ys, xs = np.nonzero(alpha > 0)
    if len(xs) == 0:
        raise ValueError(f"{name}: icon is completely blank!")
    x0, x1 = int(xs.min()), int(xs.max())
    y0, y1 = int(ys.min()), int(ys.max())
    margin = min(x0, y0, ICON_SIZE - 1 - x1, ICON_SIZE - 1 - y1)
    if margin < MIN_MARGIN_PX:
        raise ValueError(
            f"{name}: margin {margin}px < {MIN_MARGIN_PX}px (bbox=({x0},{y0})-({x1},{y1}))"
        )
    return margin, (x0, y0, x1, y1)


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    assert len(ICONS) == 21, f"Expected exact 21 icons, got {len(ICONS)}"

    expected_pngs = {f"{name}.png" for name, _ in ICONS}
    expected_imports = {f"{name}.png.import" for name, _ in ICONS}
    for existing in OUT_DIR.iterdir():
        if existing.is_file() and existing.name not in expected_pngs and existing.name not in expected_imports:
            existing.unlink()

    print(f"Building {len(ICONS)} pure-white 128x128 HUD icons into {OUT_DIR}...")
    for name, builder in ICONS:
        canvas = IconCanvas()
        builder(canvas)
        rgba_img = canvas.finalize_rgba()
        margin, bbox = validate_icon(name, rgba_img)

        png_path = OUT_DIR / f"{name}.png"
        res_path = f"res://assets/ui/hud_icons/{name}.png"
        rgba_img.save(png_path, format="PNG", compress_level=9, optimize=False)
        write_lossless_import(png_path, res_path)
        print(f"  [OK] {res_path:<42} margin={margin:2d}px bbox={bbox}")

    print("All 21 white HUD icons and lossless .import sidecars generated cleanly.")


if __name__ == "__main__":
    main()
