#!/usr/bin/env python3
"""
Industry-Standard Valorant Stylized-Realism 3D Character Pipeline (Overhaul v3)
Builds all 10 90s Filipino street kids from scratch in Blender 5.2:
  junjun, ligaya, migo, toni, popoy, inday, kiko, nene, buknoy, tala

Deliverables per character:
  - 5,500 - 9,000 clean deforming triangles (zero blocky primitives, zero unsmoothed spheres)
  - Canonical rest height 1.700 m, grounded soles/bare feet at Z = 0.000 m
  - Single joined MeshInstance3D with material named "Original"
  - 1024x1024 painterly Valorant Albedo (<id>_1.png), Tangent Normal (<id>_2.png), ORM (<id>_0.png)
  - Full mixamorig: skeleton + Cape/Hair secondary bones + 20 expressive 30fps animations
  - Rim-lit Cycles studio portraits, 3:4 profile cards, minimap faces, and Valorant character cards
"""

from __future__ import annotations

import argparse
import io
import json
import math
import os
import shutil
import struct
import subprocess
import sys
import zlib
from pathlib import Path
from typing import Any, Callable

import numpy as np

try:
    import bpy
    from mathutils import Euler, Matrix, Quaternion, Vector
    IN_BLENDER = True
except ImportError:
    bpy = None  # type: ignore
    Euler = Matrix = Quaternion = Vector = lambda *a, **kw: None  # type: ignore
    IN_BLENDER = False

try:
    from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont
    HAS_PIL = True
except ImportError:
    Image = ImageDraw = ImageEnhance = ImageFilter = ImageFont = Any  # type: ignore
    HAS_PIL = False

BLENDER_EXE = Path(r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe")

REPO_ROOT = Path(__file__).resolve().parents[2]
CHAR_DIR = REPO_ROOT / "assets" / "models" / "characters"
PORTRAIT_DIR = REPO_ROOT / "assets" / "portraits"
PORTRAIT_CARDS_DIR = PORTRAIT_DIR / "cards"
PORTRAIT_FACES_DIR = PORTRAIT_DIR / "faces"
UI_CARDS_DIR = REPO_ROOT / "assets" / "ui" / "character_cards"

ALL_KIDS = [
    "junjun",
    "ligaya",
    "migo",
    "toni",
    "popoy",
    "inday",
    "kiko",
    "nene",
    "buknoy",
    "tala",
]

STATIC_FINGERS_KIDS = {"ligaya", "migo"}

# ==============================================================================
# 1. DETERMINISTIC 1024x1024 SEMANTIC UV ATLAS REGIONS
# ==============================================================================
# Normalized [u0, v0, u1, v1] in UV space (v=0 bottom in OpenGL/glTF, PIL y = (1-v)*1024).
# Every region includes internal padding so mipmaps & ETC2 blocks never bleed!
UV_TILES: dict[str, tuple[float, float, float, float]] = {
    "face":        (0.016, 0.676, 0.316, 0.984),   # High-texel head/face front+sides
    "eyes":        (0.348, 0.844, 0.484, 0.984),   # Almond eyes: iris, catchlights, sclera
    "brows_lash":  (0.348, 0.760, 0.484, 0.828),   # Dark graphic lashes & sculpted brows
    "hair":        (0.348, 0.516, 0.488, 0.744),   # Valorant hair gradient + anisotropic band
    "torso_front": (0.516, 0.676, 0.748, 0.984),   # Shirt front graphics (#08, 95, floral, plaid, paint)
    "torso_back":  (0.764, 0.676, 0.984, 0.984),   # Shirt back graphics (BARANGAY LIGA 08, etc.)
    "torso_under": (0.516, 0.516, 0.984, 0.656),   # Undershirts, striped sleeves, collars
    "bottoms":     (0.016, 0.176, 0.324, 0.484),   # Shorts / track pants / denim / side stripes
    "skin":        (0.348, 0.176, 0.488, 0.484),   # Sunlit Filipino skin gradient (arms, legs, neck, feet)
    "socks_acc":   (0.016, 0.016, 0.244, 0.152),   # Socks (polka-dot, striped) & fabric trims
    "bands_metal": (0.264, 0.016, 0.488, 0.152),   # Pulseras, lastiko rubber, brass buckles/valves/tansan
    "prop_a":      (0.516, 0.220, 0.772, 0.484),   # Hero prop A: kumot cape, hand-tsinelas, kalasag, inner-tube, etc.
    "prop_b":      (0.792, 0.220, 0.984, 0.484),   # Hero prop B: cardboard mask, visor, cap, pouches, komiks tube
    "prop_b_top":  (0.796, 0.362, 0.980, 0.480),   # Upper half of prop_b (colorful hairclips / trims)
    "prop_b_bot":  (0.796, 0.224, 0.980, 0.342),   # Lower half of prop_b (dedicated nose band-aid plaster)
    "footwear":    (0.516, 0.016, 0.760, 0.196),   # Sneaker uppers, doodles, chevrons, tsinelas straps
    "sole_alt":    (0.780, 0.016, 0.984, 0.196),   # Rubber outsoles, mismatched left tsinelas, left sock
}


def map_uv(tile_name: str, u: float, v: float) -> tuple[float, float]:
    u0, v0, u1, v1 = UV_TILES[tile_name]
    uc = max(0.0, min(1.0, float(u)))
    vc = max(0.0, min(1.0, float(v)))
    return (u0 + uc * (u1 - u0), v0 + vc * (v1 - v0))


def tile_px_box(tile_name: str, size: int = 1024, pad: int = 8) -> tuple[int, int, int, int]:
    """Return PIL pixel box (x0, y0, x1, y1) inclusive-exclusive, including bleed pad."""
    u0, v0, u1, v1 = UV_TILES[tile_name]
    x0 = max(0, int(round(u0 * size)) - pad)
    x1 = min(size, int(round(u1 * size)) + pad)
    y0 = max(0, int(round((1.0 - v1) * size)) - pad)
    y1 = min(size, int(round((1.0 - v0) * size)) + pad)
    return (x0, y0, x1, y1)


def tile_inner_px(tile_name: str, size: int = 1024) -> tuple[int, int, int, int]:
    u0, v0, u1, v1 = UV_TILES[tile_name]
    return (
        int(round(u0 * size)),
        int(round((1.0 - v1) * size)),
        int(round(u1 * size)),
        int(round((1.0 - v0) * size)),
    )


# ==============================================================================
# 2. CHARACTER ROSTER SPECIFICATIONS (ALL 10 90s FILIPINO KIDS)
# ==============================================================================
KIDS_SPECS: dict[str, dict[str, Any]] = {
    "junjun": {
        "title": "Kapitan Kumot",
        "skin": (164, 102, 64),          # Rich sunlit Filipino kayumanggi skin (zero gl_compatibility washout!)
        "skin_shadow": (116, 66, 40),
        "eye": (44, 24, 15),
        "hair": (22, 17, 16),
        "hair_hi": (58, 44, 38),
        "shirt": (156, 148, 134),        # Shaded warm cream-ecru ribbed cotton sando (#D8CEBA-#E2D8C5 in sun, 0 blowout!)
        "shirt_trim": (132, 124, 108),
        "under": (152, 144, 130),
        "bottom": (30, 44, 78),          # Faded navy basketball shorts
        "bottom_trim": (178, 174, 164),  # Twin warm off-white stripes
        "prop_a": (188, 30, 28),         # Bright red woven kumot blanket cape
        "prop_a_trim": (204, 168, 74),   # Woven gold/cream Filipino blanket border
        "prop_b": (166, 118, 70),        # Corrugated kraft cardboard superhero mask
        "footwear": (28, 72, 156),       # Classic blue/cream slide tsinelas
        "sole": (174, 170, 160),
        "shoulder_w": 0.150,
        "waist_w": 0.110,
        "hip_w": 0.118,
        "limb_bulk": 1.04,
        "head_scale": 1.00,
        "brow_tilt": 0.18,               # Heroic determined tilt
        "card_slash": (224, 46, 42),
        "card_bg": (28, 36, 58),
    },
    "kiko": {
        "title": "Tsinelas-Kamay Sprinter",
        "skin": (154, 94, 58),           # Sun-tanned street sprinter kayumanggi!
        "skin_shadow": (108, 62, 36),
        "eye": (42, 22, 14),
        "hair": (20, 16, 15),
        "hair_hi": (54, 40, 34),
        "shirt": (196, 144, 40),         # Sun-faded mustard-yellow sleeveless V-neck tank
        "shirt_trim": (38, 42, 48),      # Charcoal athletic side panels & V-neck binding
        "under": (38, 42, 48),
        "bottom": (128, 30, 30),         # Rolled dark-red sprinter shorts
        "bottom_trim": (98, 22, 22),
        "prop_a": (14, 162, 154),        # Bright teal tsinelas strap & rim ON HANDS
        "prop_a_trim": (180, 178, 170),  # Shaded off-white EVA foam footbed ON HANDS
        "prop_b": (178, 158, 122),       # Forehead woven cream bimpo sweatband
        "footwear": (12, 170, 162),      # Bright teal Y-strap on hand-tsinelas (feet are 100% BARE!)
        "sole": (144, 88, 54),
        "shoulder_w": 0.148,
        "waist_w": 0.105,
        "hip_w": 0.114,
        "limb_bulk": 1.00,
        "head_scale": 0.99,
        "brow_tilt": 0.22,               # Intense sprinter focus
        "card_slash": (26, 186, 178),
        "card_bg": (44, 28, 32),
    },
    "ligaya": {
        "title": "Kumander Teks",
        "skin": (170, 110, 72),          # Warm golden Filipino skin!
        "skin_shadow": (122, 72, 46),
        "eye": (50, 28, 16),
        "hair": (26, 18, 17),
        "hair_hi": (68, 46, 38),
        "shirt": (204, 84, 68),          # Coral-salmon sunflower spaghetti-strap 90s bestida!
        "shirt_trim": (208, 168, 44),    # Sunflower yellow motif
        "under": (178, 168, 152),
        "bottom": (18, 108, 114),        # Deep-teal cycling shorts
        "bottom_trim": (14, 82, 88),
        "prop_a": (106, 62, 34),         # Cross-body teks/pogs leather/woven bandolier
        "prop_a_trim": (208, 56, 126),   # Hot-pink snap hairclip & pogs
        "prop_b": (68, 182, 78),         # Neon-green scrunchie & hairclip
        "footwear": (202, 68, 122),      # Right foot pink tsinelas
        "sole": (204, 168, 40),          # Left foot mismatched yellow tsinelas!
        "shoulder_w": 0.140,
        "waist_w": 0.100,
        "hip_w": 0.118,
        "limb_bulk": 0.96,
        "head_scale": 1.00,
        "brow_tilt": -0.04,
        "card_slash": (242, 104, 136),
        "card_bg": (22, 54, 62),
    },
    "migo": {
        "title": "Plastik-Balloon Alchemist",
        "skin": (166, 106, 68),
        "skin_shadow": (118, 70, 44),
        "eye": (46, 26, 16),
        "hair": (22, 17, 20),
        "hair_hi": (58, 44, 52),
        "shirt": (104, 184, 168),        # Pastel mint-aqua boxy 90s graphic tee
        "shirt_trim": (148, 120, 188),   # Pastel lavender sleeves & collar
        "under": (148, 120, 188),
        "bottom": (52, 88, 138),         # Rolled puruntong denim shorts
        "bottom_trim": (136, 160, 186),  # Rolled lighter denim cuff
        "prop_a": (178, 98, 196),        # Holographic iridescent 90s belt-bag
        "prop_a_trim": (68, 186, 198),
        "prop_b": (202, 28, 118),        # Translucent hot-magenta sun-visor headband
        "footwear": (26, 36, 64),        # Chunky street navy slide tsinelas
        "sole": (174, 172, 166),
        "shoulder_w": 0.150,
        "waist_w": 0.108,
        "hip_w": 0.117,
        "limb_bulk": 1.02,
        "head_scale": 1.00,
        "brow_tilt": 0.06,
        "card_slash": (236, 48, 152),
        "card_bg": (32, 34, 68),
    },
    "toni": {
        "title": "Barangay Liga MVP",
        "skin": (156, 98, 62),
        "skin_shadow": (110, 64, 38),
        "eye": (42, 22, 14),
        "hair": (18, 15, 15),
        "hair_hi": (52, 40, 36),
        "shirt": (148, 22, 30),          # Crimson #08 Barangay Liga mesh jersey
        "shirt_trim": (204, 158, 40),    # Gold varsity trim & #08 numbers
        "under": (158, 150, 136),        # Shaded warm cream-ecru tee underneath (0 white blowout!)
        "bottom": (88, 90, 64),          # Baggy olive-drab cargo shorts
        "bottom_trim": (70, 72, 50),
        "prop_a": (34, 42, 48),          # Backwards charcoal/teal snapback cap
        "prop_a_trim": (22, 128, 122),   # Teal under-brim & strap
        "prop_b": (26, 134, 128),        # Teal snapback brim accent
        "footwear": (158, 24, 30),       # Red/black/cream chevron 90s high-tops
        "sole": (174, 170, 162),
        "shoulder_w": 0.160,
        "waist_w": 0.114,
        "hip_w": 0.122,
        "limb_bulk": 1.06,
        "head_scale": 0.99,
        "brow_tilt": 0.12,
        "card_slash": (212, 42, 52),
        "card_bg": (34, 38, 34),
    },
    "popoy": {
        "title": "Tangke ng Tanghali",
        "skin": (162, 102, 64),
        "skin_shadow": (114, 66, 40),
        "eye": (44, 24, 15),
        "hair": (22, 17, 16),
        "hair_hi": (54, 42, 34),
        "shirt": (96, 144, 182),         # Open sky-blue/cream/rust vertical-striped polo
        "shirt_trim": (182, 86, 44),     # Rust stripe accent
        "under": (158, 150, 135),        # Shaded warm cream cotton undershirt (0 white blowout!)
        "bottom": (28, 42, 68),          # Sturdy navy shorts
        "bottom_trim": (22, 32, 54),
        "prop_a": (164, 118, 70),        # Corrugated cardboard kalasag forearm shield
        "prop_a_trim": (184, 148, 94),   # Glossy tan packing-tape straps
        "prop_b": (42, 100, 162),        # Hip ice-candy pouch w/ mango/ube/melon tubes
        "footwear": (30, 38, 56),        # Wide heavy-duty tsinelas
        "sole": (172, 168, 160),
        "shoulder_w": 0.176,             # Stocky sturdy tank silhouette!
        "waist_w": 0.132,
        "hip_w": 0.136,
        "limb_bulk": 1.24,
        "head_scale": 1.02,
        "brow_tilt": 0.10,
        "card_slash": (224, 148, 56),
        "card_bg": (28, 44, 66),
    },
    "inday": {
        "title": "Reyna ng Patintero",
        "skin": (124, 72, 44),           # Rich warm deep morena skin!
        "skin_shadow": (84, 46, 26),
        "eye": (36, 18, 12),
        "hair": (16, 14, 15),
        "hair_hi": (48, 34, 30),
        "shirt": (158, 150, 135),        # Warm cream-ecru #95 PE ringer tee (#D8CEBA-#E2D8C5 in sun!)
        "shirt_trim": (198, 146, 32),    # Golden-amber collar & sleeve cuff bands
        "under": (198, 146, 32),
        "bottom": (24, 38, 82),          # Navy track pants (1 leg rolled, 1 full-length!)
        "bottom_trim": (176, 174, 166),  # Double cream-white side stripes
        "prop_a": (192, 28, 30),         # Red referee whistle on black lanyard
        "prop_a_trim": (26, 26, 30),
        "prop_b": (206, 68, 120),        # Colorful lastiko rubberband braid chains
        "footwear": (176, 168, 154),     # Shaded cream canvas PE sneakers
        "sole": (164, 112, 60),          # Classic gum-rubber toe & outsole
        "shoulder_w": 0.146,
        "waist_w": 0.102,
        "hip_w": 0.120,
        "limb_bulk": 1.00,
        "head_scale": 0.99,
        "brow_tilt": 0.14,
        "card_slash": (236, 182, 44),
        "card_bg": (24, 38, 74),
    },
    "nene": {
        "title": "Tirador Scout",
        "skin": (148, 90, 56),           # Warm sunlit morena-kayumanggi skin (per nene_concept.png)!
        "skin_shadow": (104, 58, 34),
        "eye": (44, 24, 15),
        "hair": (22, 16, 15),
        "hair_hi": (60, 44, 34),
        "shirt": (42, 68, 118),          # Indigo denim pinafore/shortall jumper
        "shirt_trim": (194, 144, 44),    # Contrast denim gold stitching & brass buttons
        "under": (196, 142, 38),         # Mustard-and-cream striped tee underneath
        "bottom": (42, 68, 118),         # Jumper shortall bottoms w/ rolled cuffs
        "bottom_trim": (116, 142, 178),
        "prop_a": (116, 68, 34),         # Carved wooden tirador slingshot + marble pouch
        "prop_a_trim": (192, 136, 48),   # Amber lastiko bands
        "prop_b": (208, 84, 148),        # Butterfly hairclips + nose band-aid
        "footwear": (90, 54, 34),        # Strapped brown/navy outdoor sandals
        "sole": (40, 28, 22),
        "shoulder_w": 0.138,
        "waist_w": 0.100,
        "hip_w": 0.114,
        "limb_bulk": 0.96,
        "head_scale": 1.01,
        "brow_tilt": 0.04,
        "card_slash": (234, 176, 52),
        "card_bg": (32, 48, 82),
    },
    "buknoy": {
        "title": "Mekaniko ng Kolong",
        "skin": (152, 94, 58),
        "skin_shadow": (108, 62, 36),
        "eye": (42, 22, 14),
        "hair": (20, 15, 14),
        "hair_hi": (52, 38, 32),
        "shirt": (156, 60, 32),          # Cutoff sleeveless rust-plaid flannel vest
        "shirt_trim": (48, 28, 22),      # Espresso plaid checks
        "under": (126, 124, 118),        # Smudged sleeveless grey tank
        "bottom": (52, 78, 110),         # Frayed maong denim shorts
        "bottom_trim": (142, 156, 170),  # Frayed raw hem threads
        "prop_a": (26, 28, 32),          # Black vulcanized rubber bicycle inner-tube!
        "prop_a_trim": (188, 148, 60),   # Brass Schrader valve stems & belt buckle
        "prop_b": (42, 54, 70),          # Backwards slate-navy mechanic cap & tool belt
        "footwear": (26, 28, 30),        # Thick treaded tire-sole tsinelas
        "sole": (20, 22, 24),
        "shoulder_w": 0.164,
        "waist_w": 0.118,
        "hip_w": 0.124,
        "limb_bulk": 1.12,
        "head_scale": 0.99,
        "brow_tilt": 0.15,
        "card_slash": (208, 88, 46),
        "card_bg": (34, 38, 46),
    },
    "tala": {
        "title": "Komiks Pintor",
        "skin": (168, 110, 72),
        "skin_shadow": (120, 72, 46),
        "eye": (46, 24, 18),
        "hair": (16, 15, 22),            # Sleek jet-black Valorant bob w/ cool blue sheen
        "hair_hi": (54, 58, 84),
        "shirt": (98, 52, 146),          # Paint-splattered boxy purple tee
        "shirt_trim": (38, 184, 198),    # Cyan/magenta/yellow paint splatters
        "under": (176, 170, 160),        # Shaded warm cream & black striped knit long sleeves
        "bottom": (144, 122, 92),        # Khaki cargo shorts
        "bottom_trim": (118, 98, 72),
        "prop_a": (154, 114, 70),        # Diagonal kraft-cardboard komiks poster tube
        "prop_a_trim": (28, 28, 32),     # Black plastic tube end-caps
        "prop_b": (164, 144, 110),       # Khaki artist waist apron w/ chalk & ZINE
        "footwear": (174, 166, 152),     # Star-doodled shaded cream canvas low-top sneakers
        "sole": (178, 172, 160),
        "shoulder_w": 0.144,
        "waist_w": 0.104,
        "hip_w": 0.118,
        "limb_bulk": 0.98,
        "head_scale": 1.00,
        "brow_tilt": 0.08,
        "card_slash": (152, 82, 224),
        "card_bg": (30, 26, 48),
    },
}


# ==============================================================================
# 3. PARAMETRIC / SCULPTED GEOMETRY BUILDER ENGINE
# ==============================================================================
class CharBuilder:
    """
    Constructs clean, smooth-shaded, non-blocky quad/tri character topology
    with deterministic semantic UV atlas coords and per-vertex skinning part tags.
    """

    def __init__(self, kid_id: str, spec: dict[str, Any]) -> None:
        self.kid_id = kid_id
        self.spec = spec
        self.verts: list[tuple[float, float, float]] = []
        self.tags: list[str] = []
        self.tris: list[tuple[int, int, int]] = []
        self.tri_uvs: list[tuple[tuple[float, float], tuple[float, float], tuple[float, float]]] = []

    def add_vert(self, x: float, y: float, z: float, tag: str) -> int:
        idx = len(self.verts)
        self.verts.append((float(x), float(y), float(z)))
        self.tags.append(tag)
        return idx

    def add_tri(
        self,
        i0: int,
        i1: int,
        i2: int,
        uv0: tuple[float, float],
        uv1: tuple[float, float],
        uv2: tuple[float, float],
    ) -> None:
        if i0 == i1 or i1 == i2 or i2 == i0:
            return
        self.tris.append((i0, i1, i2))
        self.tri_uvs.append((uv0, uv1, uv2))

    def add_quad(
        self,
        i0: int,
        i1: int,
        i2: int,
        i3: int,
        uv0: tuple[float, float],
        uv1: tuple[float, float],
        uv2: tuple[float, float],
        uv3: tuple[float, float],
    ) -> None:
        self.add_tri(i0, i1, i2, uv0, uv1, uv2)
        self.add_tri(i0, i2, i3, uv0, uv2, uv3)

    def ensure_outward_ring_winding(
        self,
        rings_xyz: list[list[tuple[float, float, float]]],
        outward_hint: Vector | None = None,
    ) -> bool:
        """Check whether (r0[j], r0[j+1], r1[j+1]) faces outward from ring centerline."""
        if len(rings_xyz) < 2 or len(rings_xyz[0]) < 3:
            return False
        r0 = rings_xyz[0]
        r1 = rings_xyz[1]
        c0 = Vector((
            sum(p[0] for p in r0) / len(r0),
            sum(p[1] for p in r0) / len(r0),
            sum(p[2] for p in r0) / len(r0),
        ))
        c1 = Vector((
            sum(p[0] for p in r1) / len(r1),
            sum(p[1] for p in r1) / len(r1),
            sum(p[2] for p in r1) / len(r1),
        ))
        dots = 0.0
        n_seg = len(r0)
        for j in range(min(n_seg, 8)):
            jn = (j + 1) % n_seg
            p00 = Vector(r0[j])
            p01 = Vector(r0[jn])
            p11 = Vector(r1[jn])
            tri_n = (p01 - p00).cross(p11 - p00)
            out_v = outward_hint if outward_hint is not None else ((p00 + p01 + p11) / 3.0 - (c0 + c1) * 0.5)
            dots += tri_n.dot(out_v)
        return dots < 0.0

    def add_loft(
        self,
        rings: list[list[tuple[float, float, float]]],
        tags: list[str] | str,
        tile: str,
        closed_ring: bool = True,
        cap_start: bool = False,
        cap_end: bool = False,
        u_offset: float = 0.0,
        u_scale: float = 1.0,
        v_range: tuple[float, float] = (0.04, 0.96),
        front_back_split_tiles: tuple[str, str] | None = None,
    ) -> list[list[int]]:
        """
        Loft smooth quad strips across N cross-sectional rings with automatic
        outward normal orientation and clean UV parameterization.
        """
        n_rings = len(rings)
        n_seg = len(rings[0])
        tag_list = [tags] * n_rings if isinstance(tags, str) else tags
        flip = self.ensure_outward_ring_winding(rings)

        idx_rings: list[list[int]] = []
        for r_i, ring in enumerate(rings):
            t_tag = tag_list[r_i]
            idx_rings.append([self.add_vert(p[0], p[1], p[2], t_tag) for p in ring])

        v0, v1 = v_range
        seg_count = n_seg if closed_ring else (n_seg - 1)
        for r_i in range(n_rings - 1):
            va = v0 + (v1 - v0) * (r_i / max(1, n_rings - 1))
            vb = v0 + (v1 - v0) * ((r_i + 1) / max(1, n_rings - 1))
            ra = idx_rings[r_i]
            rb = idx_rings[r_i + 1]
            ra_xyz = rings[r_i]
            for j in range(seg_count):
                jn = (j + 1) % n_seg
                ua = (u_offset + (j / seg_count) * u_scale) % 1.0001
                ub = u_offset + ((j + 1) / seg_count) * u_scale
                if ub > 1.0:
                    ub = min(1.0, ub)

                use_tile = tile
                if front_back_split_tiles is not None:
                    mid_y = 0.5 * (ra_xyz[j][1] + ra_xyz[jn][1])
                    mid_x = 0.5 * (ra_xyz[j][0] + ra_xyz[jn][0])
                    if mid_y <= 0.0:
                        use_tile = front_back_split_tiles[0]
                        u_loc_a = 0.5 + math.atan2(ra_xyz[j][0], -min(-1e-4, ra_xyz[j][1])) / math.pi * 0.92
                        u_loc_b = 0.5 + math.atan2(ra_xyz[jn][0], -min(-1e-4, ra_xyz[jn][1])) / math.pi * 0.92
                    else:
                        use_tile = front_back_split_tiles[1]
                        u_loc_a = 0.5 - math.atan2(ra_xyz[j][0], max(1e-4, ra_xyz[j][1])) / math.pi * 0.92
                        u_loc_b = 0.5 - math.atan2(ra_xyz[jn][0], max(1e-4, ra_xyz[jn][1])) / math.pi * 0.92
                    uv00 = map_uv(use_tile, u_loc_a, va)
                    uv01 = map_uv(use_tile, u_loc_b, va)
                    uv11 = map_uv(use_tile, u_loc_b, vb)
                    uv10 = map_uv(use_tile, u_loc_a, vb)
                else:
                    uv00 = map_uv(use_tile, ua, va)
                    uv01 = map_uv(use_tile, ub, va)
                    uv11 = map_uv(use_tile, ub, vb)
                    uv10 = map_uv(use_tile, ua, vb)

                if not flip:
                    self.add_quad(ra[j], ra[jn], rb[jn], rb[j], uv00, uv01, uv11, uv10)
                else:
                    self.add_quad(ra[j], rb[j], rb[jn], ra[jn], uv00, uv10, uv11, uv01)

        if closed_ring and cap_start:
            r0 = rings[0]
            cx = sum(p[0] for p in r0) / n_seg
            cy = sum(p[1] for p in r0) / n_seg
            cz = sum(p[2] for p in r0) / n_seg
            ci = self.add_vert(cx, cy, cz, tag_list[0])
            cuv = map_uv(tile, 0.5, v0)
            for j in range(n_seg):
                jn = (j + 1) % n_seg
                uv_a = map_uv(tile, 0.5 + 0.4 * math.cos(2 * math.pi * j / n_seg), v0)
                uv_b = map_uv(tile, 0.5 + 0.4 * math.cos(2 * math.pi * jn / n_seg), v0)
                if not flip:
                    self.add_tri(ci, idx_rings[0][jn], idx_rings[0][j], cuv, uv_b, uv_a)
                else:
                    self.add_tri(ci, idx_rings[0][j], idx_rings[0][jn], cuv, uv_a, uv_b)

        if closed_ring and cap_end:
            rn = rings[-1]
            cx = sum(p[0] for p in rn) / n_seg
            cy = sum(p[1] for p in rn) / n_seg
            cz = sum(p[2] for p in rn) / n_seg
            ci = self.add_vert(cx, cy, cz, tag_list[-1])
            cuv = map_uv(tile, 0.5, v1)
            for j in range(n_seg):
                jn = (j + 1) % n_seg
                uv_a = map_uv(tile, 0.5 + 0.4 * math.cos(2 * math.pi * j / n_seg), v1)
                uv_b = map_uv(tile, 0.5 + 0.4 * math.cos(2 * math.pi * jn / n_seg), v1)
                if not flip:
                    self.add_tri(ci, idx_rings[-1][j], idx_rings[-1][jn], cuv, uv_a, uv_b)
                else:
                    self.add_tri(ci, idx_rings[-1][jn], idx_rings[-1][j], cuv, uv_b, uv_a)

        return idx_rings

    def add_garment_shell(
        self,
        rings: list[list[tuple[float, float, float]]],
        tags: list[str] | str,
        tile: str,
        hem_thickness: float = 0.0055,
        hem_start: bool = True,
        hem_end: bool = True,
        front_back_split_tiles: tuple[str, str] | None = None,
        v_range: tuple[float, float] = (0.05, 0.95),
    ) -> None:
        """
        Adds an outer garment loft PLUS rolled 3D collar/hem lip returns at the openings
        so garments read with crisp physical fabric thickness from every camera angle!
        """
        ext_rings = list(rings)
        tag_list = [tags] * len(rings) if isinstance(tags, str) else list(tags)

        def inset_ring(ring: list[tuple[float, float, float]], dz: float) -> list[tuple[float, float, float]]:
            cx = sum(p[0] for p in ring) / len(ring)
            cy = sum(p[1] for p in ring) / len(ring)
            out = []
            for px, py, pz in ring:
                vx, vy = px - cx, py - cy
                ln = max(1e-5, math.hypot(vx, vy))
                scale = max(0.72, (ln - hem_thickness) / ln)
                out.append((cx + vx * scale, cy + vy * scale, pz + dz))
            return out

        if hem_start and len(rings) >= 2:
            dz0 = 0.003 if rings[1][0][2] > rings[0][0][2] else -0.003
            r_in0 = inset_ring(rings[0], dz0)
            ext_rings = [r_in0] + ext_rings
            tag_list = [tag_list[0]] + tag_list

        if hem_end and len(rings) >= 2:
            dz1 = -0.003 if rings[-1][0][2] > rings[-2][0][2] else 0.003
            r_in1 = inset_ring(rings[-1], dz1)
            ext_rings = ext_rings + [r_in1]
            tag_list = tag_list + [tag_list[-1]]

        self.add_loft(
            ext_rings,
            tag_list,
            tile,
            closed_ring=True,
            v_range=v_range,
            front_back_split_tiles=front_back_split_tiles,
        )

    def add_tube_along_curve(
        self,
        pts: list[tuple[float, float, float]],
        radii: list[tuple[float, float]],
        tags: list[str] | str,
        tile: str,
        n_sides: int = 8,
        keel: float = 0.0,
        cap_start: bool = True,
        cap_end: bool = True,
        up_hint: Vector = Vector((0.0, 0.0, 1.0)),
        twist_rad: float = 0.0,
        v_range: tuple[float, float] = (0.05, 0.95),
    ) -> list[list[int]]:
        """
        Sweeps an elliptical or Valorant diamond-keel cross-section cleanly along a 3D polyline
        using parallel-transport frames (zero gimbal flipping!).
        """
        n_pts = len(pts)
        vecs = [Vector(p) for p in pts]
        tangents: list[Vector] = []
        for i in range(n_pts):
            if i == 0:
                t = vecs[1] - vecs[0]
            elif i == n_pts - 1:
                t = vecs[-1] - vecs[-2]
            else:
                t = vecs[i + 1] - vecs[i - 1]
            tangents.append(t.normalized() if t.length > 1e-6 else Vector((0.0, 0.0, -1.0)))

        # Parallel transport frame initialization
        t0 = tangents[0]
        ref = up_hint if abs(t0.dot(up_hint)) < 0.92 else Vector((0.0, 1.0, 0.0))
        nrm = (ref - t0 * ref.dot(t0)).normalized()
        rings: list[list[tuple[float, float, float]]] = []
        for i in range(n_pts):
            ti = tangents[i]
            if i > 0:
                rot_q = tangents[i - 1].rotation_difference(ti)
                nrm = (rot_q @ nrm).normalized()
            binrm = ti.cross(nrm).normalized()
            rx, ry = radii[i]
            tw = twist_rad * (i / max(1, n_pts - 1))
            ct, st = math.cos(tw), math.sin(tw)
            u_ax = nrm * ct + binrm * st
            v_ax = -nrm * st + binrm * ct

            ring: list[tuple[float, float, float]] = []
            for j in range(n_sides):
                ang = 2.0 * math.pi * j / n_sides
                ca, sa = math.cos(ang), math.sin(ang)
                # Valorant keel sharpens ridge peaks for chunky graphic hair/folds
                if keel > 0.0:
                    pinch = 1.0 - keel * (abs(math.sin(2.0 * ang)) ** 0.8)
                    ca *= pinch
                    sa *= (1.0 + 0.22 * keel * max(0.0, ca))
                p = vecs[i] + u_ax * (ca * rx) + v_ax * (sa * ry)
                ring.append((p.x, p.y, p.z))
            rings.append(ring)

        return self.add_loft(
            rings,
            tags,
            tile,
            closed_ring=True,
            cap_start=cap_start,
            cap_end=cap_end,
            v_range=v_range,
        )

    def add_valorant_hair_lock(
        self,
        pts: list[tuple[float, float, float]],
        base_w: float,
        base_thick: float,
        tag: str = "head",
        tile: str = "hair",
        outward_dir: tuple[float, float, float] = (0.0, -1.0, 0.2),
        taper_power: float = 1.25,
    ) -> None:
        """Creates a signature graphic Valorant 6-sided keel-ridged hair clump tapering to a crisp tip."""
        n = len(pts)
        radii: list[tuple[float, float]] = []
        for i in range(n):
            t = i / max(1, n - 1)
            bell = math.sin(min(1.0, t * 2.8) * math.pi * 0.5) if t < 0.28 else 1.0
            falloff = max(0.06, (1.0 - t) ** taper_power)
            s = (0.68 + 0.32 * bell) * falloff
            radii.append((base_thick * s, base_w * s))
        self.add_tube_along_curve(
            pts,
            radii,
            tag,
            tile,
            n_sides=6,
            keel=0.34,
            cap_start=True,
            cap_end=True,
            up_hint=Vector(outward_dir).normalized(),
            v_range=(0.08, 0.94),
        )

    def add_rounded_box(
        self,
        center: tuple[float, float, float],
        half_extents: tuple[float, float, float],
        rot_euler: tuple[float, float, float],
        tag: str,
        tile: str,
        bevel_power: float = 0.65,
        n_ring: int = 12,
        n_stacks: int = 5,
    ) -> None:
        """Smooth superellipsoid / rounded-bevel 3D pouch, card stack, buckle, or shield pad (zero raw cubes!)."""
        cx, cy, cz = center
        hx, hy, hz = half_extents
        rot_m = Euler(rot_euler, "XYZ").to_matrix()
        rings: list[list[tuple[float, float, float]]] = []
        for si in range(n_stacks):
            v_frac = si / (n_stacks - 1)
            phi = -0.46 * math.pi + 0.92 * math.pi * v_frac
            cp = math.cos(phi)
            sp = math.sin(phi)
            z_loc = hz * math.copysign(abs(sp) ** bevel_power, sp)
            rad_scale = abs(cp) ** (bevel_power * 0.75)
            ring: list[tuple[float, float, float]] = []
            for j in range(n_ring):
                ang = 2.0 * math.pi * j / n_ring
                ca, sa = math.cos(ang), math.sin(ang)
                x_loc = hx * rad_scale * math.copysign(abs(ca) ** bevel_power, ca)
                y_loc = hy * rad_scale * math.copysign(abs(sa) ** bevel_power, sa)
                wp = rot_m @ Vector((x_loc, y_loc, z_loc))
                ring.append((cx + wp.x, cy + wp.y, cz + wp.z))
            rings.append(ring)
        self.add_loft(rings, tag, tile, closed_ring=True, cap_start=True, cap_end=True)


# ==============================================================================
# 4. ANATOMICAL VALORANT HEAD, EYES, LASHES, BROWS, EARS & BODY BUILDERS
# ==============================================================================
def build_valorant_head_and_neck(cb: CharBuilder, spec: dict[str, Any], bare_shoulders: bool = False) -> None:
    """
    Builds a youthful 12yo Valorant planar head (compact H/W ~ 1.14 appealing rounded-V silhouette!)
    + neck + seamless upper-chest/clavicle/deltoid skin bridge, crisp sculpted nose bridge/tip/alar wings,
    confident mouth/lips, recessed orbital sockets, large expressive 3D almond eyes, winged upper lashes,
    sculpted brows, and 3D ears.
    """
    hs = spec["head_scale"] * 1.14
    sw = spec["shoulder_w"]
    n_seg = 28

    # Watertight Upper-Chest / Clavicle / Trapezius Bridge (Z = 1.165..1.364):
    # Full clavicle/deltoid breadth on bare-shouldered kids (`junjun`, `kiko`, `ligaya`),
    # tucked cleanly inside sleeved shirts on sleeved kids (`sw * 0.85`)!
    if bare_shoulders:
        chest_skin_stations = [
            (1.165, sw * 0.84, 0.084,  0.000, "spine1"),
            (1.275, sw * 0.96, 0.084, -0.002, "spine2"),
            (1.330, sw * 0.88, 0.075, +0.001, "spine2"),
            (1.352, sw * 0.68, 0.062, +0.003, "spine2"),
            (1.366, 0.046,     0.046, +0.006, "neck"),
        ]
    else:
        chest_skin_stations = [
            (1.165, sw * 0.80, 0.080,  0.000, "spine1"),
            (1.275, sw * 0.85, 0.078, -0.002, "spine2"),
            (1.328, sw * 0.84, 0.073, +0.001, "spine2"),
            (1.350, sw * 0.62, 0.059, +0.003, "spine2"),
            (1.365, 0.046,     0.046, +0.006, "neck"),
        ]
    cs_rings = []
    cs_tags = []
    for zc, rxc, ryc, yoc, tgc in chest_skin_stations:
        cs_rings.append([
            (math.sin(2 * math.pi * j / 24) * rxc, yoc - math.cos(2 * math.pi * j / 24) * ryc, zc)
            for j in range(24)
        ])
        cs_tags.append(tgc)
    cb.add_loft(cs_rings, cs_tags, "skin", closed_ring=True, cap_end=True)

    # Compact 12yo Valorant Head: Chin at Z = 1.432, Crown at Z = 1.650 (H = 0.218m, W = 0.191m -> H/W = 1.14!)
    # Eyes at youthful ~40% up from chin (Z = 1.518), Nose tip at Z = 1.486, Mouth at Z = 1.462!
    # (z, rx, ry_front, ry_back, tag, jaw_pinch, nose_bump, socket_dip, lip_bump, cheek_plane)
    stations = [
        (1.335, 0.048, 0.047, 0.049, "neck", 0.00, 0.000, 0.000, 0.000, 0.00),
        (1.385, 0.039, 0.040, 0.043, "neck", 0.00, 0.000, 0.000, 0.000, 0.00),
        (1.422, 0.038, 0.041, 0.044, "neck", 0.00, 0.000, 0.000, 0.000, 0.00),
        (1.432, 0.036 * hs, 0.084 * hs, 0.056 * hs, "head", 0.44, 0.000, 0.000, 0.000, 0.00),  # Chin base
        (1.444, 0.054 * hs, 0.092 * hs, 0.068 * hs, "head", 0.34, 0.000, 0.000, 0.002, 0.02),  # Chin tip
        (1.456, 0.066 * hs, 0.092 * hs, 0.076 * hs, "head", 0.23, 0.000, 0.000, 0.008, 0.05),  # Lower lip
        (1.463, 0.071 * hs, 0.090 * hs, 0.081 * hs, "head", 0.16, 0.000, 0.000, 0.003, 0.07),  # Mouth line / smirk!
        (1.471, 0.075 * hs, 0.092 * hs, 0.085 * hs, "head", 0.10, 0.004, 0.000, 0.009, 0.10),  # Upper lip
        (1.486, 0.080 * hs, 0.093 * hs, 0.090 * hs, "head", 0.04, 0.025, 0.000, 0.000, 0.15),  # Crisp 3D Nose tip & alar wings!
        (1.502, 0.084 * hs, 0.092 * hs, 0.093 * hs, "head", 0.00, 0.016, 0.005, 0.000, 0.18),  # Zygomatic cheekbones!
        (1.511, 0.083 * hs, 0.090 * hs, 0.095 * hs, "head", 0.00, 0.011, 0.012, 0.000, 0.10),  # Lower orbital rim
        (1.519, 0.083 * hs, 0.089 * hs, 0.097 * hs, "head", 0.00, 0.009, 0.014, 0.000, 0.06),  # Eye center / nasal root
        (1.531, 0.083 * hs, 0.092 * hs, 0.098 * hs, "head", 0.00, 0.008, 0.008, 0.000, 0.05),  # Upper eyelid crease
        (1.546, 0.083 * hs, 0.096 * hs, 0.099 * hs, "head", 0.00, 0.005, -0.003, 0.000, 0.06), # Brow ridge / glabella
        (1.578, 0.081 * hs, 0.095 * hs, 0.098 * hs, "head", 0.00, 0.000, 0.000, 0.000, 0.03),  # Forehead plane
        (1.610, 0.075 * hs, 0.087 * hs, 0.091 * hs, "head", 0.00, 0.000, 0.000, 0.000, 0.00),  # Upper forehead
        (1.635, 0.056 * hs, 0.065 * hs, 0.069 * hs, "head", 0.00, 0.000, 0.000, 0.000, 0.00),  # Crown
        (1.650, 0.024 * hs, 0.028 * hs, 0.030 * hs, "head", 0.00, 0.000, 0.000, 0.000, 0.00),  # Skull apex
    ]

    rings: list[list[tuple[float, float, float]]] = []
    tags: list[str] = []
    idx_rings: list[list[int]] = []

    for z, rx, ry_f, ry_b, tag, jaw_p, nose_b, sock_d, lip_b, cheek_p in stations:
        ring_xyz: list[tuple[float, float, float]] = []
        for j in range(n_seg):
            # theta = 0 at -Y (dead center front nose!), pi/2 at +X (left), pi at +Y (back)
            th = 2.0 * math.pi * j / n_seg
            st = math.sin(th)
            ct = math.cos(th)  # +1 at front (-Y), -1 at back (+Y)

            x = rx * st
            ry = ry_f if ct >= 0.0 else ry_b
            y = -ry * ct

            # Planar Valorant angular V-jaw taper toward appealing 12yo chin (front AND 3/4 views!)
            if jaw_p > 0.0 and ct > -0.25:
                x *= (1.0 - jaw_p * (0.26 + 0.38 * max(0.0, ct) ** 2))
                gonial = math.exp(-(((abs(th) - 1.18) / 0.30) ** 2))
                x *= (1.0 + 0.05 * jaw_p * gonial)

            # Planar Valorant cheekbone facet around |th| ~ 0.68 rad
            if cheek_p > 0.0 and ct > 0.0:
                cheek = math.exp(-(((abs(th) - 0.68) / 0.28) ** 2))
                y -= cheek_p * 0.038 * cheek
                x += math.copysign(cheek_p * 0.016 * cheek, st)

            # Protruding 3D nose bridge, crisp button/angular tip & alar wings
            if nose_b > 0.0 and ct > 0.5:
                nose_w = math.exp(-((th / 0.15) ** 2))
                alar_w = math.exp(-(((abs(th) - 0.19) / 0.09) ** 2))
                y -= nose_b * nose_w + (nose_b * 0.32 * alar_w if z < 1.495 else 0.0)

            # Sculpted 3D upper & lower lips with subtle oral corner tuck
            if lip_b > 0.0 and ct > 0.6:
                lip_w = math.exp(-((th / 0.26) ** 2))
                cupid = 1.0 - 0.18 * math.exp(-((th / 0.06) ** 2)) if z > 1.465 else 1.0
                corner_tuck = math.exp(-(((abs(th) - 0.30) / 0.08) ** 2))
                y -= lip_b * lip_w * cupid - 0.003 * corner_tuck

            # Concave orbital eye sockets around |th| ~ 0.39 rad (x ~ ±0.036)
            if sock_d != 0.0 and ct > 0.4:
                eye_w = math.exp(-(((abs(th) - 0.39) / 0.16) ** 2))
                y += sock_d * eye_w

            if tag == "neck":
                y += 0.006

            ring_xyz.append((x, y, z))
        rings.append(ring_xyz)
        tags.append(tag)
        idx_rings.append([cb.add_vert(p[0], p[1], p[2], tag) for p in ring_xyz])

    # Custom cylindrical/front-focused UV mapping onto "face" tile (neck uses "skin")
    flip = cb.ensure_outward_ring_winding(rings)
    z_face_min, z_face_max = 1.432, 1.650
    for r_i in range(len(rings) - 1):
        ra = idx_rings[r_i]
        rb = idx_rings[r_i + 1]
        za = stations[r_i][0]
        zb = stations[r_i + 1][0]
        is_neck = stations[r_i][4] == "neck" and stations[r_i + 1][4] == "neck"
        use_tile = "skin" if is_neck else "face"

        if is_neck:
            va = 0.72 + 0.22 * (r_i / 3.0)
            vb = 0.72 + 0.22 * ((r_i + 1) / 3.0)
        else:
            va = max(0.03, min(0.97, (za - z_face_min) / (z_face_max - z_face_min)))
            vb = max(0.03, min(0.97, (zb - z_face_min) / (z_face_max - z_face_min)))

        for j in range(n_seg):
            jn = (j + 1) % n_seg
            th_a = (2.0 * math.pi * j / n_seg)
            th_b = (2.0 * math.pi * (j + 1) / n_seg)
            if th_a > math.pi:
                th_a -= 2.0 * math.pi
                th_b -= 2.0 * math.pi
            ua = 0.5 + (th_a / math.pi) * 0.47
            ub = 0.5 + (th_b / math.pi) * 0.47
            uv00 = map_uv(use_tile, ua, va)
            uv01 = map_uv(use_tile, ub, va)
            uv11 = map_uv(use_tile, ub, vb)
            uv10 = map_uv(use_tile, ua, vb)
            if not flip:
                cb.add_quad(ra[j], ra[jn], rb[jn], rb[j], uv00, uv01, uv11, uv10)
            else:
                cb.add_quad(ra[j], rb[j], rb[jn], ra[jn], uv00, uv10, uv11, uv01)

    # Cap skull apex
    apex_i = cb.add_vert(0.0, -0.002, 1.651, "head")
    apex_uv = map_uv("face", 0.5, 0.98)
    for j in range(n_seg):
        jn = (j + 1) % n_seg
        if not flip:
            cb.add_tri(apex_i, idx_rings[-1][j], idx_rings[-1][jn], apex_uv, apex_uv, apex_uv)
        else:
            cb.add_tri(apex_i, idx_rings[-1][jn], idx_rings[-1][j], apex_uv, apex_uv, apex_uv)

    # --- Convex 3D Almond Eyeballs + Graphic Winged Upper Lashes + 3D Eyebrows ---
    brow_tilt = spec["brow_tilt"]
    for side in (-1.0, 1.0):  # -1 = Right (-X), +1 = Left (+X)
        ex = side * 0.036 * hs
        ey = -0.0825 * hs
        ez = 1.519
        ew = 0.0190 * hs
        eh = 0.0118 * hs
        nu, nv = 7, 5
        eye_grid: list[list[int]] = []
        eye_uvs: list[list[tuple[float, float]]] = []
        for iv in range(nv):
            vf = iv / (nv - 1)
            vs = (vf - 0.5) * 2.0
            row_i: list[int] = []
            row_uv: list[tuple[float, float]] = []
            for iu in range(nu):
                uf = iu / (nu - 1)
                us = (uf - 0.5) * 2.0
                almond_h = math.cos(us * math.pi * 0.46)
                px = ex + side * (us * ew)
                pz = ez + vs * eh * almond_h + 0.0014 * us
                bulge = 0.0084 * math.cos(us * 1.15) * math.cos(vs * 1.15)
                face_curve = 0.0048 * (us + 0.35) ** 2
                py = ey - bulge + face_curve
                row_i.append(cb.add_vert(px, py, pz, "head"))
                u_tex = (0.5 + 0.44 * us * side)
                v_tex = 0.5 + 0.44 * vs * almond_h
                row_uv.append(map_uv("eyes", u_tex, v_tex))
            eye_grid.append(row_i)
            eye_uvs.append(row_uv)

        for iv in range(nv - 1):
            for iu in range(nu - 1):
                a, b = eye_grid[iv][iu], eye_grid[iv][iu + 1]
                c, d = eye_grid[iv + 1][iu + 1], eye_grid[iv + 1][iu]
                uva, uvb = eye_uvs[iv][iu], eye_uvs[iv][iu + 1]
                uvc, uvd = eye_uvs[iv + 1][iu + 1], eye_uvs[iv + 1][iu]
                if side > 0:
                    cb.add_quad(a, b, c, d, uva, uvb, uvc, uvd)
                else:
                    cb.add_quad(a, d, c, b, uva, uvd, uvc, uvb)

        # 3D Graphic Valorant Upper Eyelash Ribbon with winged outer flick
        lash_pts = []
        lash_rad = []
        for k in range(6):
            t = k / 5.0
            us = -0.92 + 1.98 * t
            almond = math.cos(min(1.0, abs(us) * 0.88) * math.pi * 0.46)
            lx = ex + side * (us * ew * 1.03)
            lz = ez + eh * almond * 1.02 + (0.0028 if k == 5 else 0.0)
            ly = ey - 0.0088 * math.cos(us * 0.95) + 0.0045 * (us + 0.35) ** 2
            lash_pts.append((lx, ly, lz))
            thick = 0.0025 * (0.45 + 0.75 * math.sin(t * math.pi * 0.85))
            lash_rad.append((thick, thick * 0.75))
        cb.add_tube_along_curve(lash_pts, lash_rad, "head", "brows_lash", n_sides=5, keel=0.2)

        # 3D Sculpted Eyebrow perched along brow ridge
        brow_pts = []
        brow_rad = []
        for k in range(6):
            t = k / 5.0
            us = -0.88 + 2.02 * t
            arch = math.sin(t * math.pi * 0.82) * 0.0052
            tilt_z = brow_tilt * (t - 0.35) * 0.016
            bx = ex + side * (us * ew * 1.16)
            bz = ez + 0.022 + arch + tilt_z
            by = ey - 0.0092 + 0.0055 * (us + 0.30) ** 2
            brow_pts.append((bx, by, bz))
            w_b = 0.0044 * (1.05 - 0.55 * (t ** 1.6))
            brow_rad.append((w_b, 0.0028))
        cb.add_tube_along_curve(brow_pts, brow_rad, "head", "brows_lash", n_sides=5, keel=0.25)

        # Sculpted 3D C-Helix Ears aligned cleanly between nose tip (1.486) & brow (1.534)
        ear_pts = []
        ear_rad = []
        ear_curve = [
            (0.080 * hs, -0.010, 1.486, 0.0055, 0.0075),
            (0.088 * hs, -0.004, 1.495, 0.0065, 0.0110),
            (0.092 * hs, +0.004, 1.512, 0.0068, 0.0135),
            (0.089 * hs, -0.002, 1.527, 0.0062, 0.0115),
            (0.080 * hs, -0.012, 1.534, 0.0048, 0.0075),
        ]
        for ex_r, ey_r, ez_r, r1, r2 in ear_curve:
            ear_pts.append((side * ex_r, ey_r, ez_r))
            ear_rad.append((r1, r2))
        cb.add_tube_along_curve(ear_pts, ear_rad, "head", "skin", n_sides=6, keel=0.15)


def build_base_arms_and_hands(
    cb: CharBuilder,
    spec: dict[str, Any],
    arm_skin_from_t: float = 0.18,
    glove_hands: bool = False,
    straight_fingers: bool = False,
) -> dict[str, Vector]:
    """
    Builds anatomically tapered Valorant arms with watertight tucked shoulder roots
    (ZERO hollow shoulder tubes!) and a CONTINUOUS WELDED WRIST-TO-PALM LOFT (ZERO wrist seam!),
    plus 5 articulated 3-joint fingers (`straight_fingers=True` extends Kiko's fingers flat on his hand-tsinelas!).
    """
    sw = spec["shoulder_w"]
    bulk = spec["limb_bulk"]
    slope = math.radians(10.0)
    cs, ss = math.cos(slope), math.sin(slope)

    sh_len = 0.455  # Shoulder to wrist center
    joints: dict[str, Vector] = {}

    for side, s_tag in ((1.0, "L"), (-1.0, "R")):
        sx, sy, sz = side * sw, 0.005, 1.338
        ex = sx + side * 0.232 * cs
        ey = sy + 0.006
        ez = sz - 0.232 * ss
        wx = sx + side * sh_len * cs
        wy = sy - 0.004
        wz = sz - sh_len * ss

        joints[f"shoulder_{s_tag}"] = Vector((sx, sy, sz))
        joints[f"elbow_{s_tag}"] = Vector((ex, ey, ez))
        joints[f"wrist_{s_tag}"] = Vector((wx, wy, wz))

        # Continuous anatomical arm stations: smooth 3-ring rounded deltoid dome blending seamlessly into clavicle!
        arm_stations = [
            (-0.24, 0.044, 0.042, "spine2"),
            (-0.09, 0.048, 0.046, f"shoulder_{s_tag}"),  # Inner acromion-clavicle blend!
            ( 0.02, 0.047, 0.045, f"shoulder_{s_tag}"),  # Rounded outer deltoid cap (rotates smoothly with shoulder/arm!)
            ( 0.18, 0.041, 0.040, f"arm_{s_tag}"),       # Upper bicep / deltoid insertion
            ( 0.44, 0.035, 0.035, f"arm_{s_tag}"),       # Pre-elbow loop
            ( 0.51, 0.036, 0.034, f"forearm_{s_tag}"),   # Elbow joint loop
            ( 0.58, 0.039, 0.037, f"forearm_{s_tag}"),   # Proximal forearm brachioradialis swell!
            ( 0.75, 0.033, 0.031, f"forearm_{s_tag}"),   # Mid forearm taper
            ( 0.90, 0.026, 0.022, f"forearm_{s_tag}"),   # Slender wrist approach
            ( 1.00, 0.025, 0.018, f"hand_{s_tag}"),      # Oval wrist joint (welded directly to palm!)
        ]

        rings: list[list[tuple[float, float, float]]] = []
        r_tags: list[str] = []
        n_arm_seg = 12
        ax_dir = Vector((side * cs, -0.018, -ss)).normalized()
        up_ax = Vector((side * ss, 0.0, cs)).normalized()
        fwd_ax = Vector((0.0, -1.0, 0.0))

        min_t = -0.26 if arm_skin_from_t <= 0.08 else (arm_skin_from_t - 0.10)
        for t, r_fb, r_ud, t_name in arm_stations:
            if t < min_t:
                continue
            pos = Vector((sx, sy, sz)).lerp(Vector((wx, wy, wz)), max(0.0, t))
            if t < 0.0:
                # Tuck root smoothly into ribcage/clavicle bridge with zero stationary Spine2 corner!
                in_frac = min(1.0, -t / 0.24)
                pos.x = side * sw * (0.95 - 0.35 * in_frac)
                pos.z = 1.336 - 0.024 * in_frac + 0.004 * (1.0 - in_frac)
            if 0.44 <= t <= 0.58:
                pos.y += 0.005 * (1.0 - abs(t - 0.51) / 0.08)
            ring: list[tuple[float, float, float]] = []
            for j in range(n_arm_seg):
                ang = 2.0 * math.pi * j / n_arm_seg
                p = pos + fwd_ax * (math.cos(ang) * r_fb * bulk) + up_ax * (math.sin(ang) * r_ud * bulk)
                ring.append((p.x, p.y, p.z))
            rings.append(ring)
            r_tags.append(t_name)

        # --- Sculpted Palm / Metacarpal Block (WELDED 100% SEAMLESSLY TO WRIST RING!) ---
        palm_stations = [
            (0.000, 0.025 * bulk, 0.018 * bulk),
            (0.032, 0.036 * bulk, 0.017 * bulk),
            (0.062, 0.041 * bulk, 0.015 * bulk),
            (0.082, 0.039 * bulk, 0.012 * bulk),
        ]
        palm_rings: list[list[tuple[float, float, float]]] = []
        for dist, r_fb, r_ud in palm_stations:
            pos = Vector((wx, wy, wz)) + ax_dir * dist + fwd_ax * (dist * 0.05)
            ring = []
            for j in range(n_arm_seg):
                ang = 2.0 * math.pi * j / n_arm_seg
                p = pos + fwd_ax * (math.cos(ang) * r_fb) + up_ax * (math.sin(ang) * r_ud)
                ring.append((p.x, p.y, p.z))
            palm_rings.append(ring)

        if not glove_hands:
            # Single unbroken watertight loft from tucked shoulder root all the way to knuckles!
            full_rings = rings + palm_rings[1:]
            full_tags = r_tags + [f"hand_{s_tag}"] * (len(palm_rings) - 1)
            cb.add_loft(full_rings, full_tags, "skin", closed_ring=True, cap_start=True, cap_end=True)
        else:
            if len(rings) >= 2:
                cb.add_loft(rings, r_tags, "skin", closed_ring=True, cap_start=True, cap_end=True)
            cb.add_loft(palm_rings, f"hand_{s_tag}", "torso_under", closed_ring=True, cap_start=True, cap_end=True)

        # --- 5 Articulated 3-Joint Fingers (Thumb + Index, Middle, Ring, Pinky) ---
        finger_defs = [
            ("Index",  +0.028, 0.078, 0.070, 0.0076),
            ("Middle", +0.009, 0.082, 0.077, 0.0080),
            ("Ring",   -0.010, 0.079, 0.071, 0.0074),
            ("Pinky",  -0.027, 0.073, 0.057, 0.0065),
        ]
        for f_name, f_y_off, palm_d, f_len, f_rad in finger_defs:
            knuckle = Vector((wx, wy, wz)) + ax_dir * palm_d + fwd_ax * (f_y_off * bulk)
            f_dir = ax_dir if straight_fingers else (ax_dir * 0.96 - up_ax * 0.24).normalized()
            pts = []
            rads = []
            f_tags = []
            for k, frac in enumerate((0.0, 0.36, 0.68, 1.0)):
                drop = 0.0 if straight_fingers else (0.004 * (frac ** 2))
                fp = knuckle + f_dir * (f_len * frac) - up_ax * drop
                pts.append((fp.x, fp.y, fp.z))
                r = f_rad * bulk * (1.0 - 0.30 * frac)
                rads.append((r, r * 0.88))
                j_num = min(3, k + 1)
                f_tags.append(f"finger_{s_tag}_{f_name}{j_num}")
                joints[f"{s_tag}_{f_name}{k+1}"] = fp
            cb.add_tube_along_curve(pts, rads, f_tags, "skin", n_sides=6, cap_start=True, cap_end=True, up_hint=up_ax)

        # Articulated 3-joint Thumb emerging from anterior-radial thenar pad
        th_base = Vector((wx, wy, wz)) + ax_dir * 0.028 + fwd_ax * (0.028 * bulk) - up_ax * (0.002 if straight_fingers else 0.006)
        th_dir = (ax_dir * 0.84 + fwd_ax * 0.54).normalized() if straight_fingers else (ax_dir * 0.72 + fwd_ax * 0.62 - up_ax * 0.28).normalized()
        th_pts = []
        th_rads = []
        th_tags = []
        for k, frac in enumerate((0.0, 0.38, 0.72, 1.0)):
            tp = th_base + th_dir * (0.064 * frac)
            th_pts.append((tp.x, tp.y, tp.z))
            r = 0.0092 * bulk * (1.0 - 0.28 * frac)
            th_rads.append((r, r * 0.88))
            th_tags.append(f"finger_{s_tag}_Thumb{min(3, k+1)}")
            joints[f"{s_tag}_Thumb{k+1}"] = tp
        cb.add_tube_along_curve(th_pts, th_rads, th_tags, "skin", n_sides=6, cap_start=True, cap_end=True, up_hint=up_ax)

    return joints


def build_base_legs(
    cb: CharBuilder,
    spec: dict[str, Any],
    z_top_L: float = 0.52,
    z_top_R: float = 0.52,
    z_bot_L: float = 0.08,
    z_bot_R: float = 0.08,
    sock_top_L: float = -1.0,
    sock_top_R: float = -1.0,
    sock_tile_L: str = "socks_acc",
    sock_tile_R: str = "socks_acc",
) -> dict[str, Vector]:
    """
    Builds anatomically sculpted Valorant legs with 3-loop knee topology,
    athletic high gastrocnemius calf taper, and slender ankles.
    """
    hw = spec["hip_w"] * 0.80
    bulk = spec["limb_bulk"]
    joints: dict[str, Vector] = {}

    full_stations = [
        # (z, rx, ry, y_off, tag_suffix)
        (0.840, 0.068, 0.072, +0.004, "upleg"),
        (0.700, 0.060, 0.064, +0.002, "upleg"),
        (0.560, 0.052, 0.055,  0.000, "upleg"),
        (0.515, 0.048, 0.051, -0.003, "upleg"),   # Pre-knee loop
        (0.490, 0.047, 0.050, -0.006, "leg"),     # Knee patella center!
        (0.462, 0.046, 0.048, -0.002, "leg"),     # Post-knee loop
        (0.390, 0.051, 0.055, +0.008, "leg"),     # High athletic calf belly!
        (0.295, 0.043, 0.046, +0.006, "leg"),     # Mid-lower calf taper
        (0.200, 0.033, 0.036, +0.003, "leg"),     # Achilles taper
        (0.115, 0.029, 0.033, +0.001, "foot"),    # Ankle malleolus (Z = 0.11!)
        (0.075, 0.030, 0.036, -0.004, "foot"),
    ]

    for side, s_tag, z_top, z_bot, s_top, s_tile in (
        (1.0, "L", z_top_L, z_bot_L, sock_top_L, sock_tile_L),
        (-1.0, "R", z_top_R, z_bot_R, sock_top_R, sock_tile_R),
    ):
        lx = side * hw
        joints[f"hip_{s_tag}"] = Vector((lx, 0.004, 0.880))
        joints[f"knee_{s_tag}"] = Vector((lx, -0.006, 0.490))
        joints[f"ankle_{s_tag}"] = Vector((lx, 0.001, 0.110))

        vis = [st for st in full_stations if z_bot - 0.015 <= st[0] <= z_top + 0.025]
        if len(vis) < 2:
            continue

        def _interp_leg_st(z_q: float) -> tuple[float, float, float, float, str]:
            # Note: full_stations is ordered from top (0.840) down to bottom (0.075)!
            if z_q >= full_stations[0][0]:
                return full_stations[0]
            if z_q <= full_stations[-1][0]:
                return full_stations[-1]
            for i_st in range(len(full_stations) - 1):
                hi_s = full_stations[i_st]
                lo_s = full_stations[i_st + 1]
                if lo_s[0] <= z_q <= hi_s[0]:
                    f_s = (z_q - lo_s[0]) / max(1e-5, hi_s[0] - lo_s[0])
                    rx_s = lo_s[1] + (hi_s[1] - lo_s[1]) * f_s
                    ry_s = lo_s[2] + (hi_s[2] - lo_s[2]) * f_s
                    yo_s = lo_s[3] + (hi_s[3] - lo_s[3]) * f_s
                    tg_s = "upleg" if z_q > 0.50 else ("leg" if z_q > 0.15 else "foot")
                    return (z_q, rx_s, ry_s, yo_s, tg_s)
            return full_stations[-1]

        # Split into bare skin section (above sock) and sock section (below sock_top) with ZERO gap!
        segments_to_build: list[tuple[list[Any], str]] = []
        if s_top > z_bot + 0.015:
            st_mid = _interp_leg_st(s_top)
            st_bot = _interp_leg_st(z_bot)
            st_top = _interp_leg_st(z_top)
            skin_st = [st_top] + [st for st in vis if s_top + 0.008 < st[0] < z_top - 0.008] + [st_mid]
            sock_top_ring = (s_top + 0.005, st_mid[1], st_mid[2], st_mid[3], st_mid[4])
            sock_st = [sock_top_ring] + [st for st in vis if z_bot + 0.008 < st[0] < s_top - 0.008] + [st_bot]
            if len(skin_st) >= 2:
                segments_to_build.append((skin_st, "skin"))
            if len(sock_st) >= 2:
                # Puff sock radius by +2.6mm so sock cuff reads crisply over bare shin!
                sock_puffed = [(z, rx + 0.0026, ry + 0.0026, yo, tg) for (z, rx, ry, yo, tg) in sock_st]
                segments_to_build.append((sock_puffed, s_tile))
        else:
            segments_to_build.append((vis, "skin"))

        n_leg_seg = 16
        for seg_list, seg_tile in segments_to_build:
            rings: list[list[tuple[float, float, float]]] = []
            r_tags: list[str] = []
            for z, rx, ry, yo, tg in seg_list:
                ring = []
                for j in range(n_leg_seg):
                    ang = 2.0 * math.pi * j / n_leg_seg
                    px = lx + math.sin(ang) * rx * bulk
                    py = yo - math.cos(ang) * ry * bulk
                    ring.append((px, py, z))
                rings.append(ring)
                r_tags.append(f"{tg}_{s_tag}")
            cb.add_loft(rings, r_tags, seg_tile, closed_ring=True)

    return joints


# ==============================================================================
# 5. FOOTWEAR & BAREFOOT BUILDERS (GROUNDED EXACTLY AT Z = 0.000!)
# ==============================================================================
def build_bare_feet(cb: CharBuilder, spec: dict[str, Any], z_sole: float = 0.000) -> None:
    """
    Builds sculpted bare Filipino feet with heel pad, arch, forefoot ball, and 5 individual bare toes!
    When z_sole == 0.000 (MANDATORY FOR KIKO!), bare heels/balls/toes rest cleanly on Z = 0.000!
    When z_sole > 0.000 (tsinelas wearers), bare feet rest cleanly on top of the tsinelas footbed!
    """
    hw = spec["hip_w"] * 0.80
    bulk = spec["limb_bulk"]
    for side, s_tag in ((1.0, "L"), (-1.0, "R")):
        lx = side * hw
        # Smooth foot loft from ankle (z_sole + 0.102) -> heel/instep -> ball of foot (y = -0.108)
        foot_stations = [
            # (y_center, z_bottom, z_top, half_w, tag)
            (+0.035, z_sole + 0.004, z_sole + 0.076, 0.029 * bulk, f"foot_{s_tag}"),  # Rounded heel back
            (+0.008, z_sole + 0.000, z_sole + 0.102, 0.031 * bulk, f"foot_{s_tag}"),  # Heel plant & seamless ankle join
            (-0.032, z_sole + 0.006, z_sole + 0.074, 0.033 * bulk, f"foot_{s_tag}"),  # Medial arch & instep
            (-0.078, z_sole + 0.000, z_sole + 0.041, 0.041 * bulk, f"toe_{s_tag}"),   # Metatarsal ball of foot
            (-0.112, z_sole + 0.001, z_sole + 0.024, 0.040 * bulk, f"toe_{s_tag}"),   # Toe web base
        ]
        n_f = 14
        rings: list[list[tuple[float, float, float]]] = []
        tags: list[str] = []
        for yc, zb, zt, w_half, tg in foot_stations:
            zc = 0.5 * (zb + zt)
            hz = 0.5 * (zt - zb)
            ring = []
            for j in range(n_f):
                ang = 2.0 * math.pi * j / n_f
                sa, ca = math.sin(ang), math.cos(ang)
                # Flatten sole bottom cleanly so foot sits planted on ground/footbed!
                z_val = zc + hz * ca
                if ca < 0.0:
                    z_val = zb + (zc - zb) * (1.0 + ca) * 0.25
                x_val = lx + sa * w_half
                y_val = yc - 0.018 * ca
                ring.append((x_val, y_val, z_val))
            rings.append(ring)
            tags.append(tg)
        cb.add_loft(rings, tags, "skin", closed_ring=True, cap_start=True, cap_end=True)

        # 5 Distinct Sculpted Bare Toes! (Big toe on medial side -> pinky toe on lateral side)
        toe_specs = [
            (-0.026, 0.038, 0.0105),  # Big toe (medial: closer to x=0)
            (-0.009, 0.035, 0.0074),  # 2nd toe
            (+0.005, 0.031, 0.0070),  # 3rd toe
            (+0.018, 0.026, 0.0065),  # 4th toe
            (+0.030, 0.021, 0.0060),  # Pinky toe (lateral)
        ]
        for med_lat_x, t_len, t_rad in toe_specs:
            # Medial is -side * x_offset, lateral is +side * x_offset
            tx = lx + side * med_lat_x * bulk
            pts = [
                (tx, -0.106, z_sole + t_rad * 0.95),
                (tx, -0.106 - t_len * 0.55, z_sole + t_rad * 0.90),
                (tx, -0.106 - t_len, z_sole + t_rad * 0.80),
            ]
            rads = [(t_rad, t_rad * 0.85), (t_rad * 0.95, t_rad * 0.82), (t_rad * 0.72, t_rad * 0.65)]
            cb.add_tube_along_curve(pts, rads, f"toe_{s_tag}", "skin", n_sides=6, cap_start=True, cap_end=True)


def build_single_tsinelas_geometry(
    cb: CharBuilder,
    xform: Matrix,
    tag_sole: str,
    tag_toe: str,
    sole_tile: str,
    strap_tile: str,
    style: str = "ystrap",
    sole_thick: float = 0.017,
    length: float = 0.225,
    width: float = 0.094,
    side_sign: float = 1.0,
    strap_scale: float = 1.0,
    arch_height: float = 0.044,
    include_strap: bool = True,
) -> None:
    """
    Builds an authentic 3D Filipino rubber tsinelas (either classic Y-strap thong or chunky street slide)
    transformed by `xform` — reusable BOTH on feet (at Z = 0.000) AND on Kiko's BOTH HANDS!
    In local tsinelas space before `xform`:
      - Sole bottom is at Z = 0.000, sole top footbed is at Z = sole_thick
      - Heel is at +Y (+length*0.36), Toe tip is at -Y (-length*0.64)
      - Strap arches over +Z above the footbed!
    """
    n_seg = 18
    # Outlined rounded peanut/slipper silhouette stations from heel (+Y) to toe (-Y)
    stations = [
        (+0.36 * length, 0.36 * width),
        (+0.26 * length, 0.44 * width),
        (+0.05 * length, 0.41 * width),  # Waist arch
        (-0.22 * length, 0.50 * width),  # Forefoot widest
        (-0.48 * length, 0.46 * width),
        (-0.61 * length, 0.32 * width),
    ]
    # Build molded sole slab: bottom face -> side rim -> top footbed
    sole_rings: list[list[tuple[float, float, float]]] = []
    # Profile from bottom center -> bottom edge -> mid rim -> top edge -> top center
    rim_levels = [
        (0.000, 0.12),
        (0.000, 0.96),
        (sole_thick * 0.50, 1.00),
        (sole_thick, 0.96),
        (sole_thick, 0.12),
    ]
    perimeter_2d: list[tuple[float, float]] = []
    # Trace clockwise perimeter around the slipper outline
    for y_st, hw_st in stations:
        perimeter_2d.append((+hw_st, y_st))
    perimeter_2d.append((0.0, -0.65 * length))
    for y_st, hw_st in reversed(stations):
        perimeter_2d.append((-hw_st, y_st))
    perimeter_2d.append((0.0, +0.39 * length))

    for z_lv, r_sc in rim_levels:
        ring: list[tuple[float, float, float]] = []
        for px, py in perimeter_2d:
            # Slight rocker toe-spring at front tip if not bottom center
            rocker = 0.003 * max(0.0, (-py / (0.65 * length)) - 0.65)
            wp = xform @ Vector((px * r_sc, py * (0.2 + 0.8 * r_sc if r_sc < 0.3 else 1.0), z_lv + rocker))
            ring.append((wp.x, wp.y, wp.z))
        sole_rings.append(ring)

    cb.add_loft(sole_rings, tag_sole, sole_tile, closed_ring=True, cap_start=True, cap_end=True)
    if not include_strap:
        return

    if style == "ystrap":
        # Authentic molded rubber Y-strap: left anchor -> bridge apex -> toe plug <- right anchor
        plug_pt = Vector((-side_sign * 0.08 * width, -0.36 * length, sole_thick - 0.002))
        apex_pt = Vector((0.0, -0.14 * length, sole_thick + arch_height))
        ss_k = strap_scale
        for s_anch in (-1.0, 1.0):
            anch = Vector((s_anch * 0.45 * width, +0.02 * length, sole_thick * 0.6))
            mid1 = Vector((s_anch * 0.36 * width, -0.06 * length, sole_thick + arch_height * 0.74))
            arm_pts_local = [anch, mid1, apex_pt, apex_pt.lerp(plug_pt, 0.55) + Vector((0, 0, 0.008)), plug_pt]
            arm_pts = [tuple(xform @ p) for p in arm_pts_local]
            rads = [
                (0.0065 * ss_k, 0.0042 * ss_k),
                (0.0072 * ss_k, 0.0045 * ss_k),
                (0.0082 * ss_k, 0.0050 * ss_k),
                (0.0064 * ss_k, 0.0046 * ss_k),
                (0.0056 * ss_k, 0.0056 * ss_k),
            ]
            cb.add_tube_along_curve(arm_pts, rads, tag_toe, strap_tile, n_sides=6, cap_start=True, cap_end=True)
    else:
        # Chunky 90s street slide arch band over instep
        band_pts_local = [
            Vector((-0.47 * width, -0.16 * length, sole_thick * 0.5)),
            Vector((-0.42 * width, -0.18 * length, sole_thick + 0.032)),
            Vector((0.0, -0.20 * length, sole_thick + 0.048)),
            Vector((+0.42 * width, -0.18 * length, sole_thick + 0.032)),
            Vector((+0.47 * width, -0.16 * length, sole_thick * 0.5)),
        ]
        band_pts = [tuple(xform @ p) for p in band_pts_local]
        rads = [(0.024, 0.0055)] * len(band_pts)
        cb.add_tube_along_curve(
            band_pts,
            rads,
            tag_sole,
            strap_tile,
            n_sides=8,
            up_hint=(xform.to_3x3() @ Vector((0.0, 1.0, 0.0))).normalized(),
        )


def build_foot_tsinelas(
    cb: CharBuilder,
    spec: dict[str, Any],
    style: str = "ystrap",
    mismatched: bool = False,
    thick_tire: bool = False,
) -> None:
    """Places 3D rubber tsinelas at Z = 0.000 + bare sculpted feet resting on top of the footbeds!"""
    sole_h = 0.022 if thick_tire else 0.017
    build_bare_feet(cb, spec, z_sole=sole_h - 0.001)
    hw = spec["hip_w"] * 0.80
    for side, s_tag in ((1.0, "L"), (-1.0, "R")):
        xf = Matrix.Translation((side * hw, 0.0, 0.0))
        if mismatched and s_tag == "L":
            s_tile, st_tile = "sole_alt", "sole_alt"
        else:
            s_tile = "sole_alt" if not mismatched else "footwear"
            st_tile = "footwear"
        build_single_tsinelas_geometry(
            cb,
            xf,
            f"foot_{s_tag}",
            f"toe_{s_tag}",
            sole_tile=s_tile,
            strap_tile=st_tile,
            style=style,
            sole_thick=sole_h,
            side_sign=side,
        )


def build_sneakers_or_sandals(
    cb: CharBuilder,
    spec: dict[str, Any],
    kind: str = "hightop",
) -> None:
    """
    Builds sculpted 90s sneakers (Toni high-top chevron, Inday PE gum-sole, Tala star-doodled canvas)
    or Nene's strapped outdoor sandals over polka-dot socks — grounded cleanly at Z = 0.000!
    """
    hw = spec["hip_w"] * 0.80
    bulk = spec["limb_bulk"]
    top_z = 0.145 if kind == "hightop" else (0.098 if kind != "sandal" else 0.092)

    for side, s_tag in ((1.0, "L"), (-1.0, "R")):
        lx = side * hw
        # Chunky 90s molded rubber cupsole (y in [-0.157, +0.059], zero heel snowshoe overhang!)
        xf = Matrix.Translation((lx, -0.022, 0.0))
        build_single_tsinelas_geometry(
            cb,
            xf,
            f"foot_{s_tag}",
            f"toe_{s_tag}",
            sole_tile="sole_alt",
            strap_tile="footwear",
            style="slide",
            sole_thick=0.024,
            length=0.208,
            width=0.100,
            side_sign=side,
            include_strap=False,
        )

        if kind == "sandal":
            # Nene's socked foot seated flush from heel (+0.052) to toe (-0.144) inside outdoor Velcro-strap sandals!
            sock_t = "sole_alt" if s_tag == "L" else "socks_acc"
            foot_rings = []
            for yc, zb, zt, wh in [
                (+0.040, 0.022, 0.090, 0.034),
                (-0.015, 0.022, 0.078, 0.038),
                (-0.082, 0.022, 0.054, 0.043),
                (-0.136, 0.024, 0.042, 0.034),
            ]:
                zc, hz = 0.5 * (zb + zt), 0.5 * (zt - zb)
                foot_rings.append([
                    (lx + math.sin(2 * math.pi * j / 14) * wh, yc - 0.014 * math.cos(2 * math.pi * j / 14), zc + math.cos(2 * math.pi * j / 14) * hz)
                    for j in range(14)
                ])
            cb.add_loft(foot_rings, f"foot_{s_tag}", sock_t, closed_ring=True, cap_start=True, cap_end=True)
            # Ankle Velcro strap + forefoot strap
            for sz_b, sy_b, sr_x, sr_y in [(0.082, 0.010, 0.037, 0.044), (0.046, -0.088, 0.046, 0.028)]:
                s_rings = []
                for dz in (-0.010, 0.010):
                    s_rings.append([
                        (lx + math.sin(2 * math.pi * j / 14) * sr_x, sy_b - math.cos(2 * math.pi * j / 14) * sr_y, sz_b + dz)
                        for j in range(14)
                    ])
                cb.add_garment_shell(s_rings, f"foot_{s_tag}", "footwear", hem_thickness=0.003)
        else:
            # Chunky horizontal-ring 90s sneaker upper seated flush inside the cupsole from heel (+0.054) to toe (-0.148)!
            levels = [
                (0.021, -0.148, +0.054, 0.047 * bulk),
                (0.044, -0.142, +0.055, 0.048 * bulk),
                (0.068, -0.092, +0.051, 0.044 * bulk),
                (0.096, -0.046, +0.045, 0.041 * bulk),
            ]
            if kind == "hightop":
                levels.extend([
                    (0.122, -0.042, +0.044, 0.042 * bulk),
                    (0.145, -0.038, +0.042, 0.040 * bulk),
                ])
            s_rings = []
            for zz, yf, yb, wh in levels:
                ym = 0.5 * (yf + yb)
                yr = 0.5 * (yb - yf)
                ring = []
                for j in range(16):
                    ang = 2.0 * math.pi * j / 16
                    sa, ca = math.sin(ang), math.cos(ang)
                    # Slightly fuller forefoot & tapered heel counter
                    w_mod = 1.04 if ca > 0.1 else 0.90
                    ring.append((lx + sa * wh * w_mod, ym - ca * yr, zz))
                s_rings.append(ring)
            cb.add_loft(s_rings, f"foot_{s_tag}", "footwear", closed_ring=True, cap_start=False, cap_end=True)
            # Padded sneaker tongue puff + molded heel counter tab
            cb.add_rounded_box(
                (lx, -0.048, top_z - 0.010),
                (0.024, 0.011, 0.024),
                (0.32, 0.0, 0.0),
                f"foot_{s_tag}",
                "footwear",
            )
            cb.add_rounded_box(
                (lx, +0.048, 0.056),
                (0.026, 0.007, 0.022),
                (0.0, 0.0, 0.0),
                f"foot_{s_tag}",
                "sole_alt",
            )


# ==============================================================================
# 6. REUSABLE LAYERED 3D GARMENTS, SLEEVES, BOTTOMS, CAPS & STRAPS
# ==============================================================================
def build_shirt_torso(
    cb: CharBuilder,
    spec: dict[str, Any],
    z_bot: float = 0.84,
    z_top: float = 1.355,
    flare_bot: float = 1.08,
    loose: float = 1.04,
    sando_cut: bool = False,
    strap_width_scale: float = 1.0,
    v_neck: bool = False,
    bestida_skirt_to: float | None = None,
    tile_front: str = "torso_front",
    tile_back: str = "torso_back",
    side_knot_left: bool = False,
) -> None:
    """
    Builds a tailored or boxy 3D shirt/sando/jersey/bestida with physical hem thickness,
    diagonal cloth tension folds, and front/back split UV mapping for crisp graphics!
    """
    sw = spec["shoulder_w"]
    ww = spec["waist_w"]
    hw = spec["hip_w"]
    n_seg = 28

    base_stations = [
        # (z, rx_base, ry_base, y_off, tag)
        (0.840, hw * 1.05 * flare_bot, 0.094 * flare_bot, +0.004, "hips"),
        (0.920, ww * 1.08 * loose,     0.088 * loose,     +0.002, "spine"),
        (1.020, ww * 1.05 * loose,     0.086 * loose,      0.000, "spine"),
        (1.130, sw * 0.84 * loose,     0.094 * loose,     -0.003, "spine1"),
        (1.240, sw * 0.96 * loose,     0.098 * loose,     -0.004, "spine2"),
        (1.338, sw * 0.98 * loose,     0.086 * loose,      0.000, "spine2"),
        (1.358, 0.062,                 0.058,             +0.004, "neck"),
    ]
    if bestida_skirt_to is not None:
        # Flared 90s A-line bestida sundress skirt down to z = bestida_skirt_to (~0.67)
        base_stations = [
            (bestida_skirt_to, hw * 1.54, 0.144, +0.005, "hips"),
            (0.740,            hw * 1.42, 0.128, +0.005, "hips"),
        ] + base_stations

    has_sando_straps = sando_cut and z_top > 1.30
    classic_u_sando = has_sando_straps and (not v_neck) and strap_width_scale >= 0.85
    eff_z_top = (1.258 if classic_u_sando else 1.245) if has_sando_straps else z_top
    if classic_u_sando:
        base_stations = [
            st if abs(st[0] - 1.240) > 0.005 else (1.258, sw * 0.96 * loose, 0.096 * loose, -0.003, "spine2")
            for st in base_stations
        ]
    stations = [st for st in base_stations if z_bot - 0.03 <= st[0] <= eff_z_top + 0.02]
    rings: list[list[tuple[float, float, float]]] = []
    tags: list[str] = []

    for idx_s, (z, rx, ry, yo, tg) in enumerate(stations):
        zf = (z - stations[0][0]) / max(0.05, stations[-1][0] - stations[0][0])
        ring: list[tuple[float, float, float]] = []
        for j in range(n_seg):
            th = 2.0 * math.pi * j / n_seg
            sa, ca = math.sin(th), math.cos(th)
            # Subtle diagonal 3D cloth drape folds across waist/hem
            pleat = 0.0038 * math.sin(5.0 * th + z * 9.0) * (1.0 - 0.65 * zf)
            if bestida_skirt_to is not None and z < 0.86:
                pleat = 0.0075 * math.sin(8.0 * th) * ((0.88 - z) / 0.25)

            rx_eff = rx + pleat
            ry_eff = ry + pleat
            z_eff = z

            # Authentic 90s U-Neck Sando / Sprinter V-Neck Tank / Spaghetti Sundress / Basketball Jersey cut!
            if sando_cut and z > 1.15:
                scoop = max(0.0, (z - 1.15) / max(0.06, eff_z_top - 1.15))
                if classic_u_sando:
                    # Deep classic Filipino U-neck scoop between |sa| <= 0.56 + deep armhole scoop beyond |sa| > 0.62!
                    u_depth = 0.076 if ca > 0.0 else 0.044
                    u_scoop = u_depth * (max(0.0, 1.0 - (abs(sa) / 0.55) ** 2) ** 1.25)
                    arm_scoop = 0.058 * (max(0.0, (abs(sa) - 0.60) / 0.40) ** 1.6)
                    z_eff -= scoop * (u_scoop + arm_scoop)
                    rx_eff *= (1.0 - 0.05 * scoop * (sa ** 2))
                else:
                    if v_neck and ca > 0.0:
                        # Sharp athletic V-neck dip at dead-center front (|sa| -> 0)!
                        front_dip = 0.062 * max(0.0, 1.0 - abs(sa) * 1.85)
                    else:
                        front_w = 0.046 if ca > 0.0 else 0.032
                        front_dip = front_w * (ca ** 4)
                    z_eff -= scoop * (front_dip + 0.052 * (sa ** 4))
                    rx_eff *= (1.0 - 0.05 * scoop * (sa ** 2))

            # Migo's gathered side-knot pull up toward left hip (+X, z ~ 0.88)
            if side_knot_left and z < 0.96:
                pull = max(0.0, sa) * max(0.0, 1.0 - (z - z_bot) / 0.12)
                z_eff += 0.042 * pull

            px = sa * rx_eff
            py = yo - ca * ry_eff
            ring.append((px, py, z_eff))
        rings.append(ring)
        tags.append(tg)

    cb.add_garment_shell(
        rings,
        tags,
        tile_front,
        hem_thickness=0.006,
        hem_start=True,
        hem_end=True,
        front_back_split_tiles=(tile_front, tile_back),
    )

    if has_sando_straps:
        # True 3D Athletic / Classic Ribbed Cotton / Spaghetti Shoulder Straps emerging 100% flush out of bodice surface!
        w_st = (0.0230 if classic_u_sando else 0.0185) * loose * strap_width_scale
        t_st = 0.0050 if classic_u_sando else 0.0048
        over_shirt = 0.018 if loose > 1.08 else 0.0
        sx_st = 0.56 if classic_u_sando else 0.53
        ca_st = math.sqrt(max(0.15, 1.0 - (sx_st / 0.96) ** 2))
        y_front_att = -0.003 - (0.095 * loose * ca_st) - over_shirt * 0.5
        y_back_att = -0.003 + (0.095 * loose * ca_st) + over_shirt * 0.5
        z_attach = 1.230 if classic_u_sando else 1.222
        for side in (-1.0, 1.0):
            strap_stations = [
                # (cx, cy, cz, nx, ny, nz, tile_s) — tucked flush into elliptical bodice surface (`ca_st`) so ZERO box step shows!
                (side * sw * sx_st * loose,          y_front_att + 0.003,                   z_attach,               0.0, -0.98, 0.18, tile_front),
                (side * sw * (sx_st + 0.01) * loose, y_front_att * 0.96 - over_shirt * 0.4, 1.292 + over_shirt * 0.3, 0.0, -0.90, 0.42, tile_front),
                (side * sw * (sx_st + 0.01),         -0.064 - over_shirt,                   1.336 + over_shirt * 0.5, 0.0, -0.66, 0.75, tile_front),
                (side * sw * sx_st,                  +0.002,                                1.356 + over_shirt * 0.7, 0.0,  0.00, 1.00, tile_front),
                (side * sw * (sx_st - 0.01),         +0.064 + over_shirt,                   1.332 + over_shirt * 0.5, 0.0, +0.66, 0.75, tile_back),
                (side * sw * sx_st * loose,          y_back_att * 0.96 + over_shirt * 0.4,  1.288 + over_shirt * 0.3, 0.0, +0.90, 0.42, tile_back),
                (side * sw * (sx_st - 0.01) * loose, y_back_att - 0.003,                    z_attach,               0.0, +0.98, 0.18, tile_back),
            ]
            st_rings: list[list[tuple[float, float, float]]] = []
            for idx_st_r, (cx, cy, cz, nx, ny, nz, _) in enumerate(strap_stations):
                n_vec = Vector((nx, ny, nz)).normalized()
                t_eff = 0.0 if idx_st_r in (0, len(strap_stations) - 1) else t_st
                p_in = Vector((cx - side * w_st, cy, cz))
                p_out = Vector((cx + side * w_st, cy, cz))
                st_rings.append([
                    tuple(p_in - n_vec * (t_st * 1.3)),
                    tuple(p_out - n_vec * (t_st * 1.3)),
                    tuple(p_out + n_vec * t_eff),
                    tuple(p_in + n_vec * t_eff),
                ])
            cb.add_loft(
                st_rings,
                "spine2",
                tile_front,
                closed_ring=True,
                cap_start=True,
                cap_end=True,
                front_back_split_tiles=(tile_front, tile_back),
            )

    if side_knot_left:
        # Migo's sculpted 3D gathered fabric knot + twin cloth tails at left hip!
        kx, ky, kz = hw * 1.12, -0.025, z_bot + 0.045
        cb.add_rounded_box((kx, ky, kz), (0.024, 0.024, 0.022), (0.2, 0.1, 0.3), "hips", tile_front)
        for dy, dz_end in ((-0.014, -0.065), (+0.016, -0.055)):
            cb.add_tube_along_curve(
                [(kx, ky, kz), (kx + 0.018, ky + dy, kz + dz_end * 0.5), (kx + 0.012, ky + dy * 1.4, kz + dz_end)],
                [(0.012, 0.006), (0.014, 0.005), (0.005, 0.003)],
                "hips",
                tile_front,
                n_sides=6,
                keel=0.25,
            )


def build_open_overshirt_or_vest(
    cb: CharBuilder,
    spec: dict[str, Any],
    tile_front: str = "torso_front",
    tile_back: str = "torso_back",
    z_bot: float = 0.82,
    has_collar: bool = True,
) -> None:
    """
    Builds an open, unbuttoned collared overshirt (Popoy's vertical-striped polo)
    or cutoff plaid flannel vest (Buknoy) with real 3D front lapels/plackets & collar!
    """
    sw = spec["shoulder_w"]
    ww = spec["waist_w"]
    hw = spec["hip_w"]
    # Open arc from front-left lapel (th = +0.34 rad) around back to front-right lapel (th = 2*pi - 0.34 rad)
    n_arc = 22
    th_start = 0.34
    th_end = 2.0 * math.pi - 0.34

    stations = [
        (z_bot, hw * 1.14, 0.104, +0.004, "hips"),
        (0.940, ww * 1.16, 0.098, +0.002, "spine"),
        (1.100, sw * 0.90, 0.102, -0.002, "spine1"),
        (1.245, sw * 1.00, 0.106, -0.003, "spine2"),
        (1.338, sw * 1.01, 0.094,  0.000, "spine2"),
        (1.352, 0.070,     0.066, +0.003, "neck"),
    ]
    outer_rings: list[list[tuple[float, float, float]]] = []
    inner_rings: list[list[tuple[float, float, float]]] = []
    tags: list[str] = []

    for z, rx, ry, yo, tg in stations:
        o_ring = []
        i_ring = []
        for j in range(n_arc):
            f = j / (n_arc - 1)
            th = th_start + (th_end - th_start) * f
            sa, ca = math.sin(th), math.cos(th)
            # Flare open plackets slightly forward near front edges
            lapel_flare = 0.008 * math.exp(-min(f, 1.0 - f) * 10.0)
            o_ring.append((sa * (rx + 0.005), yo - ca * (ry + 0.005) - lapel_flare, z))
            i_ring.append((sa * (rx - 0.002), yo - ca * (ry - 0.002) - lapel_flare * 0.5, z))
        outer_rings.append(o_ring)
        inner_rings.append(i_ring)
        tags.append(tg)

    # Stitch closed cross-section rings (outer forward + inner reversed) so lapels have true 7mm thickness!
    thick_rings: list[list[tuple[float, float, float]]] = []
    for o_r, i_r in zip(outer_rings, inner_rings):
        thick_rings.append(o_r + list(reversed(i_r)))

    cb.add_loft(
        thick_rings,
        tags,
        tile_front,
        closed_ring=True,
        cap_start=True,
        cap_end=True,
        front_back_split_tiles=(tile_front, tile_back),
    )

    if has_collar:
        # 3D Folded Shirt Collar hugging neck
        col_rings = []
        for z_c, rad_x, rad_y in [(1.342, 0.068, 0.065), (1.376, 0.074, 0.070), (1.352, 0.084, 0.078)]:
            ring = []
            for j in range(18):
                f = j / 17.0
                th = 0.42 + (2.0 * math.pi - 0.84) * f
                ring.append((math.sin(th) * rad_x, 0.004 - math.cos(th) * rad_y, z_c))
            col_rings.append(ring)
        cb.add_loft(col_rings, "neck", tile_front, closed_ring=False)


def build_shirt_sleeves(
    cb: CharBuilder,
    spec: dict[str, Any],
    t_end: float = 0.34,
    cuff_flare: float = 1.25,
    tile: str = "torso_front",
    puffed: bool = False,
) -> None:
    """
    Builds 3D sleeves along both arms with WATERTIGHT TUCKED SHOULDER ROOTS
    (`k=0` anchored deep inside `spine2` torso, `k=1` blended on `shoulder_L/R` — ZERO hollow tube gaps!).
    """
    sw = spec["shoulder_w"]
    bulk = spec["limb_bulk"]
    slope = math.radians(10.0)
    cs, ss = math.cos(slope), math.sin(slope)
    sh_len = 0.455
    ax_up_L = Vector((ss, 0.0, cs)).normalized()
    ax_up_R = Vector((-ss, 0.0, cs)).normalized()
    fwd = Vector((0.0, -1.0, 0.0))

    n_steps = 9 if t_end > 0.6 else 6
    for side, s_tag, up_ax in ((1.0, "L", ax_up_L), (-1.0, "R", ax_up_R)):
        sx, sy, sz = side * sw, 0.005, 1.338
        wx, wy, wz = sx + side * sh_len * cs, sy - 0.004, sz - sh_len * ss
        rings = []
        tags = []
        for k in range(n_steps):
            if k == 0:
                # Root ring buried deep inside torso ribcage/clavicle (`spine2`) so zero gap can ever open!
                t = -0.20
                pos = Vector((side * sw * 0.62, 0.004, 1.296))
                base_r = 0.040 * bulk
                up_r = 0.036 * bulk
                tg = "spine2"
            elif k == 1:
                # Collarbone-to-deltoid shoulder seam ring (`shoulder_L/R`)!
                t = -0.02
                pos = Vector((side * sw * 0.91, 0.005, 1.318))
                base_r = 0.045 * bulk
                up_r = 0.040 * bulk
                tg = f"shoulder_{s_tag}"
            else:
                frac = (k - 1) / (n_steps - 2)
                t = t_end * frac
                pos = Vector((sx, sy, sz)).lerp(Vector((wx, wy, wz)), t)
                pos.z -= 0.014 * max(0.0, 1.0 - frac * 2.0)
                base_r = (0.046 - 0.013 * t) * bulk
                if puffed:
                    base_r *= (1.0 + 0.14 * math.sin(frac * math.pi))
                else:
                    base_r *= (1.0 + (cuff_flare - 1.0) * (frac ** 1.35))
                up_r = base_r * (0.98 if puffed else 0.86)
                if t < 0.46:
                    tg = f"arm_{s_tag}"
                elif t < 0.92:
                    tg = f"forearm_{s_tag}"
                else:
                    tg = f"hand_{s_tag}"

            ring = []
            for j in range(14):
                ang = 2.0 * math.pi * j / 14
                p = pos + fwd * (math.cos(ang) * base_r) + up_ax * (math.sin(ang) * up_r)
                ring.append((p.x, p.y, p.z))
            rings.append(ring)
            tags.append(tg)
        cb.add_garment_shell(rings, tags, tile, hem_thickness=0.0055, hem_start=True, hem_end=True)


def build_shorts_or_pants(
    cb: CharBuilder,
    spec: dict[str, Any],
    z_hem_L: float = 0.52,
    z_hem_R: float = 0.52,
    baggy: float = 1.15,
    rolled_cuff_L: bool = False,
    rolled_cuff_R: bool = False,
    cargo_pockets: bool = False,
    tile: str = "bottoms",
    cuff_tile: str = "bottoms",
) -> None:
    """
    Builds 3D shorts, cycling shorts, denim shortalls, or asymmetric track pants
    (Inday: right leg full-length z=0.13, left leg rolled cuff below knee z=0.43!)
    with sculpted pelvis waistband + independent deforming left/right pant tubes!
    """
    ww = spec["waist_w"]
    hw = spec["hip_w"]
    bulk = spec["limb_bulk"]
    leg_x = hw * 0.80

    # Tailored pelvis / seat saddle wrapping from V-crotch arch (z=0.825 at center, z=0.775 at outer hip) up to waistband (z=0.90)!
    pelvis_stations = [
        (0.775, hw * (1.26 if baggy > 1.05 else 1.16), 0.092, +0.004, "hips", 0.050, 0.022),
        (0.820, hw * (1.24 if baggy > 1.05 else 1.14), 0.093, +0.004, "hips", 0.016, 0.006),
        (0.865, ww * 1.12,                             0.088, +0.003, "hips", 0.000, 0.000),
        (0.900, ww * 1.06,                             0.084, +0.002, "spine", 0.000, 0.000),
    ]
    p_rings = []
    p_tags = []
    for z, rx, ry, yo, tg, c_lift, c_pinch in pelvis_stations:
        ring = []
        for j in range(24):
            th = 2.0 * math.pi * j / 24
            sa, ca = math.sin(th), math.cos(th)
            gather = 0.0022 * math.sin(10.0 * th) if z > 0.86 else 0.0
            # Arch central crotch saddle UP into the inseam so ZERO box flap hangs between the thighs!
            z_sad = z + c_lift * (ca ** 2)
            ry_sad = ry - c_pinch * (ca ** 4) + gather
            ring.append((sa * (rx + gather), yo - ca * ry_sad, z_sad))
        p_rings.append(ring)
        p_tags.append(tg)
    cb.add_garment_shell(p_rings, p_tags, tile, hem_thickness=0.005, hem_start=True, hem_end=True)

    # Left & Right Independent Pant Legs (tucked cleanly into V-crotch saddle at z=0.835 -> zero step!)
    for side, s_tag, z_hem, has_cuff in (
        (1.0, "L", z_hem_L, rolled_cuff_L),
        (-1.0, "R", z_hem_R, rolled_cuff_R),
    ):
        lx = side * leg_x
        n_rings = 9 if z_hem < 0.30 else 6
        l_rings = []
        l_tags = []
        for k in range(n_rings):
            frac = k / (n_rings - 1)
            z = 0.835 + (z_hem - 0.835) * frac
            top_tuck = max(0.0, 1.0 - frac / 0.32)
            taper = 1.0 - 0.28 * frac if z_hem < 0.30 else (0.94 + 0.06 * min(1.0, frac / 0.35))
            rx = 0.069 * bulk * baggy * taper
            ry = 0.076 * bulk * baggy * taper
            ring = []
            for j in range(16):
                th = 2.0 * math.pi * j / 16
                crease = 0.003 * math.sin(4.0 * th + z * 11.0) * frac
                px = (lx - side * 0.016 * top_tuck) + math.sin(th) * (rx + crease)
                py = 0.003 - math.cos(th) * (ry + crease)
                ring.append((px, py, z))
            l_rings.append(ring)
            if z_hem >= 0.40:
                if z > 0.58:
                    l_tags.append(f"upleg_{s_tag}")
                else:
                    l_tags.append(f"shorts_{s_tag}")
            else:
                if z > 0.50:
                    l_tags.append(f"upleg_{s_tag}")
                elif z > 0.22:
                    l_tags.append(f"leg_{s_tag}")
                else:
                    l_tags.append(f"foot_{s_tag}")

        cb.add_garment_shell(l_rings, l_tags, tile, hem_thickness=0.006, hem_start=False, hem_end=True)

        if has_cuff:
            # Thick 3D Rolled Pant Cuff Torus (Inday's rolled left track pant, Migo/Kiko/Nene rolled denim cuffs!)
            cuff_tag = f"leg_{s_tag}" if z_hem < 0.45 else f"shorts_{s_tag}"
            c_rx = 0.076 * bulk * baggy * (0.86 if z_hem < 0.48 else 0.96)
            c_ry = 0.080 * bulk * baggy * (0.86 if z_hem < 0.48 else 0.96)
            c_rings = []
            for dz, dr in [(-0.004, 0.95), (0.012, 1.06), (0.030, 1.07), (0.042, 0.96)]:
                c_rings.append([
                    (lx + math.sin(2 * math.pi * j / 16) * c_rx * dr, 0.003 - math.cos(2 * math.pi * j / 16) * c_ry * dr, z_hem + dz)
                    for j in range(16)
                ])
            cb.add_loft(c_rings, cuff_tag, cuff_tile, closed_ring=True, cap_start=True, cap_end=True)

        if cargo_pockets:
            # 3D Side Bellows Cargo Pocket + Top Flap on outer thigh
            px_c = lx + side * (0.072 * bulk * baggy)
            cb.add_rounded_box((px_c, -0.004, 0.635), (0.016, 0.046, 0.056), (0.0, 0.0, -side * 0.06), f"upleg_{s_tag}", tile)
            cb.add_rounded_box((px_c + side * 0.004, -0.004, 0.686), (0.018, 0.049, 0.015), (0.0, 0.0, -side * 0.06), f"upleg_{s_tag}", tile)


def build_diagonal_chest_strap(
    cb: CharBuilder,
    spec: dict[str, Any],
    shoulder_side: float = 1.0,  # +1 = Left shoulder to Right hip
    width: float = 0.026,
    thick: float = 0.008,
    tile: str = "prop_a",
    z_hip: float = 0.92,
) -> list[tuple[float, float, float]]:
    """Wraps a continuous 3D elliptical bandolier / strap cleanly around the torso from shoulder to opposite hip!"""
    sw = spec["shoulder_w"]
    ww = spec["waist_w"]
    n_pts = 24
    loop_pts: list[tuple[float, float, float]] = []
    tags: list[str] = []
    for k in range(n_pts + 1):
        ang = 2.0 * math.pi * k / n_pts
        # Parametric closed loop tilted across torso from (shoulder_side * sw*0.72, z=1.35) to (-shoulder_side * ww*1.16, z=z_hip)
        u = math.cos(ang)  # +1 at top shoulder, -1 at opposite hip
        v = math.sin(ang)  # +1 at front chest (-Y), -1 at back (+Y)
        z = 0.5 * (1.355 + z_hip) + 0.5 * (1.355 - z_hip) * u
        x_mid = 0.5 * shoulder_side * (sw * 0.72 - ww * 1.18) + 0.5 * shoulder_side * (sw * 0.72 + ww * 1.18) * u
        ry_here = (0.098 if z > 1.10 else 0.094) * (1.06 if abs(v) > 0.2 else 1.02)
        y = -v * (ry_here + 0.010)
        loop_pts.append((x_mid, y, z))
        if z > 1.20:
            tags.append("spine2")
        elif z > 1.04:
            tags.append("spine1")
        else:
            tags.append("spine")
    rads = [(width * 0.5, thick)] * len(loop_pts)
    cb.add_tube_along_curve(loop_pts, rads, tags, tile, n_sides=6, cap_start=False, cap_end=False)
    return loop_pts


def build_backwards_cap(cb: CharBuilder, spec: dict[str, Any], tile_dome: str, tile_brim: str) -> None:
    """Builds a crisp 90s backwards snapback / mechanic cap + 90s undercut nape/temple hair cap underneath!"""
    hs = spec["head_scale"] * 1.14
    # 90s undercut dark hair taper around nape & temples underneath cap rim (z = 1.478..1.548)
    undercut_rings = []
    for z_u, rx_u, ry_b_u in [
        (1.478, 0.078 * hs, 0.094 * hs),
        (1.515, 0.083 * hs, 0.099 * hs),
        (1.548, 0.084 * hs, 0.101 * hs),
    ]:
        ring_u = []
        for j in range(20):
            th = 2.0 * math.pi * j / 20
            ca, sa = math.cos(th), math.sin(th)
            if ca > 0.25:
                ring_u.append((sa * rx_u, -0.024, 1.548))
            else:
                ring_u.append((sa * rx_u, -ca * ry_b_u, z_u))
        undercut_rings.append(ring_u)
    cb.add_loft(undercut_rings, "head", "hair", closed_ring=True, cap_end=False)

    # Snug 5-panel cap dome contoured to compact Valorant crown (z = 1.542..1.660)
    dome_stations = [
        (1.542, 0.086 * hs, 0.103 * hs),
        (1.580, 0.085 * hs, 0.101 * hs),
        (1.618, 0.076 * hs, 0.089 * hs),
        (1.646, 0.050 * hs, 0.058 * hs),
        (1.660, 0.015 * hs, 0.017 * hs),
    ]
    rings = []
    for z, rx, ry in dome_stations:
        rings.append([
            (math.sin(2 * math.pi * j / 20) * rx, 0.002 - math.cos(2 * math.pi * j / 20) * ry, z)
            for j in range(20)
        ])
    cb.add_loft(rings, "head", tile_dome, closed_ring=True, cap_end=True)
    cb.add_rounded_box((0.0, 0.002, 1.663), (0.009, 0.009, 0.005), (0, 0, 0), "head", tile_brim)

    # Backwards flat/curved snapback brim jutting horizontally out the back (+Y, slightly angled for 90s swagger!)
    brim_pts = [
        (0.0, +0.094 * hs, 1.548),
        (0.006, +0.134 * hs, 1.542),
        (0.012, +0.170 * hs, 1.534),
    ]
    cb.add_tube_along_curve(
        brim_pts,
        [(0.0048, 0.068), (0.0044, 0.064), (0.0036, 0.048)],
        "head",
        tile_brim,
        n_sides=8,
        up_hint=Vector((0.0, 0.0, 1.0)),
    )


# ==============================================================================
# 7. BESPOKE HAIR & MANDATORY 90s FILIPINO OUTFIT BUILDERS FOR ALL 10 KIDS
# ==============================================================================
def build_hair_swept_boy(
    cb: CharBuilder,
    hs: float,
    style: str = "junjun",
) -> None:
    """
    Valorant Stylized Boy Hair (`junjun`, `kiko`, `migo` — ZERO sea-urchin radial cones!):
    Builds a smooth voluminous skull cap + temple sideburns + swept asymmetric forehead bangs
    draping down across the brow/mask/bimpo/visor + long curved layered anime/Valorant crown locks!
    """
    hs_eff = hs * 1.14
    cap_rings = []
    for z, rx, ry_f, ry_b in [
        (1.472, 0.079 * hs_eff, 0.034 * hs_eff, 0.097 * hs_eff),
        (1.522, 0.085 * hs_eff, 0.084 * hs_eff, 0.103 * hs_eff),
        (1.572, 0.088 * hs_eff, 0.098 * hs_eff, 0.105 * hs_eff),
        (1.624, 0.080 * hs_eff, 0.091 * hs_eff, 0.097 * hs_eff),
        (1.662, 0.044 * hs_eff, 0.050 * hs_eff, 0.054 * hs_eff),
    ]:
        ring = []
        for j in range(22):
            th = 2.0 * math.pi * j / 22
            ca, sa = math.cos(th), math.sin(th)
            if z < 1.564 and ca > 0.24:
                y_v = -0.026 + 0.018 * ca
                z_v = 1.564 + 0.008 * ca
            else:
                ry = ry_f if ca >= 0 else ry_b
                y_v = -ca * ry
                z_v = z
            ring.append((sa * rx, y_v, z_v))
        cap_rings.append(ring)
    cb.add_loft(cap_rings, "head", "hair", closed_ring=True, cap_end=True)

    # Graphic Valorant Temple Sideburns in front of both ears
    for s_sb in (-1.0, 1.0):
        cb.add_valorant_hair_lock(
            [
                (s_sb * 0.081 * hs_eff, -0.032, 1.552),
                (s_sb * 0.085 * hs_eff, -0.038, 1.515),
                (s_sb * 0.080 * hs_eff, -0.036, 1.482),
            ],
            0.020, 0.010, tag="head", tile="hair"
        )

    # 4 Swept Asymmetric Forehead Bangs draping down over upper forehead (overlapping mask/bimpo/visor cleanly!)
    if style == "junjun":
        # Tousled heroic Filipino street-kid bangs sweeping down-right across top of cardboard domino mask!
        bang_locks = [
            ([(-0.014, -0.086 * hs_eff, 1.602), (-0.024, -0.109 * hs_eff, 1.576), (-0.034, -0.119 * hs_eff, 1.550), (-0.040, -0.118 * hs_eff, 1.532)], 0.040, 0.016),
            ([(+0.016, -0.085 * hs_eff, 1.604), (+0.005, -0.110 * hs_eff, 1.578), (-0.005, -0.121 * hs_eff, 1.552), (-0.010, -0.120 * hs_eff, 1.536)], 0.042, 0.016),
            ([(+0.042, -0.076 * hs_eff, 1.596), (+0.036, -0.104 * hs_eff, 1.572), (+0.028, -0.116 * hs_eff, 1.548), (+0.022, -0.115 * hs_eff, 1.534)], 0.038, 0.015),
            ([(-0.046, -0.072 * hs_eff, 1.592), (-0.058, -0.096 * hs_eff, 1.568), (-0.066, -0.106 * hs_eff, 1.545)], 0.034, 0.014),
        ]
    elif style == "kiko":
        # Wind-swept sprinter bangs flaring dynamically above & peeking over his woven bimpo sweatband!
        bang_locks = [
            ([(-0.018, -0.088 * hs_eff, 1.598), (-0.028, -0.112 * hs_eff, 1.584), (-0.040, -0.115 * hs_eff, 1.562)], 0.042, 0.016),
            ([(+0.018, -0.088 * hs_eff, 1.600), (+0.010, -0.113 * hs_eff, 1.586), (+0.002, -0.116 * hs_eff, 1.566)], 0.042, 0.016),
            ([(+0.048, -0.074 * hs_eff, 1.594), (+0.056, -0.098 * hs_eff, 1.576), (+0.060, -0.104 * hs_eff, 1.554)], 0.034, 0.014),
        ]
    else:
        # Migo: 90s curtain/side-swept bangs framing temples beside his magenta sun-visor!
        bang_locks = [
            ([(-0.026, -0.086 * hs_eff, 1.596), (-0.048, -0.104 * hs_eff, 1.568), (-0.064, -0.096 * hs_eff, 1.534)], 0.038, 0.015),
            ([(+0.026, -0.086 * hs_eff, 1.596), (+0.048, -0.104 * hs_eff, 1.568), (+0.064, -0.096 * hs_eff, 1.534)], 0.038, 0.015),
            ([( 0.000, -0.090 * hs_eff, 1.602), (-0.012, -0.108 * hs_eff, 1.582), (-0.020, -0.110 * hs_eff, 1.566)], 0.036, 0.014),
        ]
    for b_pts, bw, bt in bang_locks:
        cb.add_valorant_hair_lock(b_pts, bw, bt, tag="head", tile="hair", taper_power=1.15)

    # 9 Long Curved Swept Valorant Crown & Side-Fanned Locks (dynamic asymmetric street-kid silhouette per concept sheets!)
    swept_up = 0.012 if style == "kiko" else 0.004
    crown_locks = [
        # Asymmetric swept top-crown layered locks fanning up-and-out (eliminates central bun!)
        ([(-0.018, -0.042, 1.630), (-0.052, -0.005, 1.662 + swept_up), (-0.088, +0.032, 1.652 + swept_up), (-0.114, +0.062, 1.622)], 0.050, 0.019),
        ([(+0.018, -0.040, 1.632), (+0.048, -0.002, 1.664 + swept_up), (+0.082, +0.036, 1.650 + swept_up), (+0.104, +0.068, 1.616)], 0.050, 0.019),
        ([( 0.000, -0.012, 1.640), (-0.022, +0.042, 1.668 + swept_up), (-0.042, +0.096, 1.648 + swept_up), (-0.052, +0.130, 1.612)], 0.052, 0.020),
        # Left & right temple/side-swept Valorant silhouette flares (visible clearly in front & 3/4 views!)
        ([(-0.060, -0.028, 1.602), (-0.098, -0.004, 1.616), (-0.124, +0.028, 1.594), (-0.118, +0.066, 1.560)], 0.048, 0.018),
        ([(+0.060, -0.028, 1.602), (+0.096, -0.004, 1.614), (+0.118, +0.030, 1.590), (+0.112, +0.068, 1.556)], 0.048, 0.018),
        ([(-0.072, +0.012, 1.564), (-0.102, +0.046, 1.560), (-0.108, +0.088, 1.532)], 0.044, 0.017),
        ([(+0.072, +0.012, 1.564), (+0.102, +0.046, 1.560), (+0.108, +0.088, 1.532)], 0.044, 0.017),
        # Nape kicked locks above collar
        ([(-0.038, +0.086, 1.520), (-0.052, +0.114, 1.495), (-0.048, +0.122, 1.468)], 0.040, 0.016),
        ([(+0.038, +0.086, 1.520), (+0.052, +0.114, 1.495), (+0.048, +0.122, 1.468)], 0.040, 0.016),
    ]
    for c_pts, bw, bt in crown_locks:
        cb.add_valorant_hair_lock(c_pts, bw, bt, tag="head", tile="hair", taper_power=1.10)


def build_hair_sleek_parted_cap(
    cb: CharBuilder,
    hs: float,
    part_x: float = 0.0,
    dutch_braids: bool = False,
) -> None:
    """
    Valorant Sleek Parted Female Hair Cap (`ligaya`, `inday`, `nene` — ZERO sea-urchin cones!):
    Smooth parted scalp volume with soft curved hairline & temple wisps framing the cheeks,
    plus optional raised sculpted Dutch-braid crown ridges for `inday`!
    """
    hs_eff = hs * 1.14
    cap_rings = []
    for z, rx, ry_f, ry_b in [
        (1.472, 0.079 * hs_eff, 0.032 * hs_eff, 0.098 * hs_eff),
        (1.520, 0.085 * hs_eff, 0.084 * hs_eff, 0.104 * hs_eff),
        (1.572, 0.087 * hs_eff, 0.098 * hs_eff, 0.106 * hs_eff),
        (1.625, 0.079 * hs_eff, 0.090 * hs_eff, 0.098 * hs_eff),
        (1.663, 0.042 * hs_eff, 0.048 * hs_eff, 0.052 * hs_eff),
    ]:
        ring = []
        for j in range(24):
            th = 2.0 * math.pi * j / 24
            ca, sa = math.cos(th), math.sin(th)
            x_v = sa * rx
            # Subtle crown part groove along x = part_x on top of scalp (safely clearing z=1.650 skull apex!)
            part_dip = 0.003 * math.exp(-(((x_v - part_x) / 0.014) ** 2)) if z > 1.58 else 0.0
            if z < 1.566 and ca > 0.24:
                y_v = -0.026 + 0.018 * ca
                z_v = 1.566 + 0.006 * ca
            else:
                ry = ry_f if ca >= 0 else ry_b
                y_v = -ca * ry
                z_v = z - part_dip
            ring.append((x_v, y_v, z_v))
        cap_rings.append(ring)
    cb.add_loft(cap_rings, "head", "hair", closed_ring=True, cap_end=True)

    # Soft Valorant Curtain Hairline & Cheek-Framing Temple Wisps (zero spikes!)
    for side in (-1.0, 1.0):
        cb.add_valorant_hair_lock(
            [
                (part_x + side * 0.010, -0.088 * hs_eff, 1.590),
                (side * 0.054 * hs_eff, -0.094 * hs_eff, 1.572),
                (side * 0.081 * hs_eff, -0.058 * hs_eff, 1.542),
                (side * 0.083 * hs_eff, -0.040 * hs_eff, 1.492),
            ],
            0.028, 0.012, tag="head", tile="hair", taper_power=1.15
        )

    if dutch_braids:
        # Inday's twin raised sculpted Dutch-braid ridges running across her parted scalp into her shoulder braids!
        for side in (-1.0, 1.0):
            d_pts = [
                (side * 0.028 * hs_eff, -0.072 * hs_eff, 1.602),
                (side * 0.052 * hs_eff, -0.018 * hs_eff, 1.622),
                (side * 0.068 * hs_eff, +0.042 * hs_eff, 1.588),
                (side * 0.074 * hs_eff, +0.055 * hs_eff, 1.525),
            ]
            d_rads = [(0.016, 0.011), (0.021, 0.014), (0.022, 0.015), (0.020, 0.015)]
            cb.add_tube_along_curve(d_pts, d_rads, "head", "hair", n_sides=6, keel=0.25)


def build_kid_junjun(cb: CharBuilder, spec: dict[str, Any]) -> None:
    """
    JUNJUN ("Kapitan Kumot" — MANDATORY OWNER SPEC):
      - Thin white ribbed cotton sando + faded navy basketball shorts w/ twin white stripes
      - Rubber blue/white slide tsinelas on feet (Z = 0.000)
      - Flowing bright red woven blanket (kumot) knotted at his neck/shoulders as a superhero cape
      - Homemade corrugated-cardboard superhero domino eyemask with REAL 3D ALMOND EYEHOLE CUTOUTS,
        thick hand-drawn black crayon heroic eyebrows, and stretched tan lastiko rubberbands!
    """
    build_valorant_head_and_neck(cb, spec, bare_shoulders=True)
    build_hair_swept_boy(cb, spec["head_scale"], style="junjun")
    build_base_arms_and_hands(cb, spec, arm_skin_from_t=0.0)
    build_base_legs(cb, spec, z_top_L=0.53, z_top_R=0.53, z_bot_L=0.07, z_bot_R=0.07)
    build_foot_tsinelas(cb, spec, style="slide")

    # 1. Thin Off-White Ribbed Cotton Sando + Faded Navy Basketball Shorts
    build_shirt_torso(cb, spec, z_bot=0.85, z_top=1.350, sando_cut=True, loose=1.02)
    build_shorts_or_pants(cb, spec, z_hem_L=0.51, z_hem_R=0.51, baggy=1.18)

    # Colorful left-wrist pulseras + left-shin band-aid (from concept sheet!)
    for dz_p in (-0.010, 0.0, +0.010):
        w_pos = Vector((spec["shoulder_w"] + 0.415 * math.cos(math.radians(10)), 0.0, 1.338 - 0.415 * math.sin(math.radians(10)) + dz_p))
        cb.add_rounded_box((w_pos.x, w_pos.y, w_pos.z), (0.008, 0.030, 0.025), (0, 0.17, 0), "forearm_L", "bands_metal")
    cb.add_rounded_box((spec["hip_w"] * 0.80 + 0.012, -0.048, 0.345), (0.018, 0.005, 0.009), (0, 0.15, -0.2), "leg_L", "socks_acc")

    # 2. MANDATORY HOMEMADE CORRUGATED-CARDBOARD SUPERHERO DOMINO EYEMASK + LASTIKO RUBBERBANDS!
    hs = spec["head_scale"] * 1.14
    u_angles = [-0.88, -0.73, -0.57, -0.44, -0.32, -0.21, -0.122, -0.055, 0.0, +0.055, +0.122, +0.21, +0.32, +0.44, +0.57, +0.73, +0.88]
    v_heights = [1.491, 1.503, 1.514, 1.525, 1.536, 1.547, 1.557, 1.566]
    nu_m, nv_m = len(u_angles), len(v_heights)
    front_idx: list[list[int]] = []
    back_idx: list[list[int]] = []
    uv_grid: list[list[tuple[float, float]]] = []

    for iv, z_base in enumerate(v_heights):
        vf = iv / (nv_m - 1)
        f_row: list[int] = []
        b_row: list[int] = []
        uv_row: list[tuple[float, float]] = []
        for iu, ang_face in enumerate(u_angles):
            uf = iu / (nu_m - 1)
            us = ang_face / 0.88
            wing_lift = 0.013 * (abs(us) ** 1.7)
            nose_notch = -0.009 * math.exp(-((us / 0.16) ** 2)) if iv <= 1 else -0.003 * math.exp(-((us / 0.20) ** 2))
            eye_mid = math.exp(-(((abs(ang_face) - 0.34) / 0.16) ** 2))
            almond_arc = (-0.0025 * eye_mid if iv == 1 else (+0.0035 * eye_mid if iv == 4 else 0.0))
            rad_f = 0.1095 * hs
            rad_b = 0.1050 * hs
            z_m = z_base + wing_lift + nose_notch + almond_arc

            fx = math.sin(ang_face) * rad_f
            fy = -math.cos(ang_face) * rad_f - 0.003
            bx = math.sin(ang_face) * rad_b
            by = -math.cos(ang_face) * rad_b - 0.003

            f_row.append(cb.add_vert(fx, fy, z_m, "head"))
            b_row.append(cb.add_vert(bx, by, z_m, "head"))
            uv_row.append(map_uv("prop_b", 0.06 + 0.88 * uf, 0.08 + 0.84 * vf))
        front_idx.append(f_row)
        back_idx.append(b_row)
        uv_grid.append(uv_row)

    def is_eyehole_cell(iu_c: int, iv_c: int) -> bool:
        return (2 <= iu_c <= 5 or 10 <= iu_c <= 13) and (1 <= iv_c <= 3)

    for iv in range(nv_m - 1):
        for iu in range(nu_m - 1):
            if is_eyehole_cell(iu, iv):
                continue
            cb.add_quad(
                front_idx[iv][iu], front_idx[iv][iu + 1], front_idx[iv + 1][iu + 1], front_idx[iv + 1][iu],
                uv_grid[iv][iu], uv_grid[iv][iu + 1], uv_grid[iv + 1][iu + 1], uv_grid[iv + 1][iu],
            )
            cb.add_quad(
                back_idx[iv][iu], back_idx[iv + 1][iu], back_idx[iv + 1][iu + 1], back_idx[iv][iu + 1],
                uv_grid[iv][iu], uv_grid[iv + 1][iu], uv_grid[iv + 1][iu + 1], uv_grid[iv][iu + 1],
            )
            edge_uv = map_uv("prop_b", 0.50, 0.50)
            if iv == 0 or is_eyehole_cell(iu, iv - 1):
                cb.add_quad(back_idx[iv][iu], back_idx[iv][iu + 1], front_idx[iv][iu + 1], front_idx[iv][iu], edge_uv, edge_uv, edge_uv, edge_uv)
            if iv == nv_m - 2 or is_eyehole_cell(iu, iv + 1):
                cb.add_quad(front_idx[iv + 1][iu], front_idx[iv + 1][iu + 1], back_idx[iv + 1][iu + 1], back_idx[iv + 1][iu], edge_uv, edge_uv, edge_uv, edge_uv)
            if iu == 0 or is_eyehole_cell(iu - 1, iv):
                cb.add_quad(back_idx[iv][iu], front_idx[iv][iu], front_idx[iv + 1][iu], back_idx[iv + 1][iu], edge_uv, edge_uv, edge_uv, edge_uv)
            if iu == nu_m - 2 or is_eyehole_cell(iu + 1, iv):
                cb.add_quad(front_idx[iv][iu + 1], back_idx[iv][iu + 1], back_idx[iv + 1][iu + 1], front_idx[iv + 1][iu + 1], edge_uv, edge_uv, edge_uv, edge_uv)

    # Stretched Crisscrossed Tan/Dark-Rimmed Lastiko Rubberbands wrapping OVER hair around temples & back of skull (per junjun_concept.png BACK VIEW)!
    lastiko_uv = map_uv("socks_acc", 0.50, 0.78)
    for dz_slope in (+0.017, -0.017):
        band_pts = []
        n_b = 22
        for k in range(n_b + 1):
            f = k / n_b
            ang = -0.88 - f * (2.0 * math.pi - 1.76)
            back_bulge = math.sin(f * math.pi)
            rx_b = (0.096 + 0.010 * back_bulge) * hs
            ry_b = (0.106 + 0.018 * (back_bulge ** 0.72)) * hs
            # Linear opposite vertical slopes cross cleanly at f = 0.50 on the back of his head in a bold X!
            z_band = 1.526 + dz_slope * (2.0 * f - 1.0)
            band_pts.append((math.sin(ang) * rx_b, -math.cos(ang) * ry_b, z_band))
        v_before = len(cb.verts)
        t_before = len(cb.tri_uvs)
        cb.add_tube_along_curve(band_pts, [(0.0062, 0.0048)] * len(band_pts), "head", "socks_acc", n_sides=6)
        _ = v_before
        for ti in range(t_before, len(cb.tri_uvs)):
            cb.tri_uvs[ti] = (lastiko_uv, lastiko_uv, lastiko_uv)
    # Punch-hole knotted lastiko loops at both mask edges + back-center rubberband crossover knot!
    for sx_knot in (-1.0, +1.0):
        t_b = len(cb.tri_uvs)
        cb.add_rounded_box((sx_knot * math.sin(0.88) * 0.108 * hs, -math.cos(0.88) * 0.108 * hs, 1.524), (0.008, 0.009, 0.012), (0, 0, sx_knot * 0.4), "head", "socks_acc")
        for ti in range(t_b, len(cb.tri_uvs)):
            cb.tri_uvs[ti] = (lastiko_uv, lastiko_uv, lastiko_uv)
    t_bk = len(cb.tri_uvs)
    cb.add_rounded_box((0.0, 0.123 * hs, 1.524), (0.014, 0.008, 0.011), (0, 0, 0.25), "head", "socks_acc")
    for ti in range(t_bk, len(cb.tri_uvs)):
        cb.tri_uvs[ti] = (lastiko_uv, lastiko_uv, lastiko_uv)

    # 3. MANDATORY FLOWING BRIGHT RED WOVEN KUMOT BLANKET CAPE + SNUG COLLARBONE KNOT!
    # Diagonal superhero blanket collar wrapping over traps & sweeping inward to front collarbone knot
    # so BOTH cream ribbed sando shoulder straps remain 100% clearly visible framing his U-neck!
    cowl_rings = []
    for idx_cr, (z_c, rx_c, ry_c, yo_c) in enumerate([
        (1.344, spec["shoulder_w"] * 0.98, 0.104, +0.006),
        (1.366, spec["shoulder_w"] * 0.84, 0.096, +0.007),
        (1.388, 0.082,                     0.084, +0.008),
    ]):
        ring_cr = []
        for j in range(18):
            th_c = 2.0 * math.pi * j / 18
            sa_c, ca_c = math.sin(th_c), math.cos(th_c)
            # In front (ca_c > 0), sweep the collar down into a V at dead-center (|sa_c| -> 0) for the knot,
            # while lifting cleanly over |sa_c| ~ 0.56 so the cream sando straps below z=1.345 show proudly!
            front_knot_v = max(0.0, ca_c) * math.exp(-((sa_c / 0.36) ** 2))
            strap_window = max(0.0, ca_c) * math.exp(-(((abs(sa_c) - 0.58) / 0.24) ** 2))
            z_mod = -0.026 * front_knot_v * (1.0 - 0.45 * idx_cr) + 0.012 * strap_window * (1.0 - 0.4 * idx_cr)
            rx_mod = rx_c * (1.0 - 0.10 * strap_window)
            ry_mod = ry_c + 0.008 * front_knot_v
            ring_cr.append((
                sa_c * rx_mod,
                yo_c - ca_c * ry_mod,
                z_c + z_mod + 0.003 * math.sin(6.0 * th_c),
            ))
        cowl_rings.append(ring_cr)
    cb.add_garment_shell(cowl_rings, "spine2", "prop_a", hem_thickness=0.009)

    # Chunky 3D Front Kumot Knot + Two Pointed Cloth Tails nestled right at the top of his U-neck!
    cb.add_rounded_box((0.0, -0.106, 1.326), (0.032, 0.025, 0.026), (0.15, 0.0, 0.1), "spine2", "prop_a")
    for s_tail in (-1.0, 1.0):
        cb.add_tube_along_curve(
            [
                (s_tail * 0.014, -0.110, 1.316),
                (s_tail * 0.036, -0.116, 1.264),
                (s_tail * 0.048, -0.112, 1.212),
            ],
            [(0.021, 0.009), (0.024, 0.008), (0.007, 0.004)],
            "spine2",
            "prop_a",
            n_sides=6,
            keel=0.28,
        )

    # Wide Multi-Pleated Flowing Double-Sided Red Kumot Cape (Z = 1.348 down to Z = 0.420!)
    # Weighted smoothly across spine2 -> cape1 -> cape2 -> cape3, clearing back sando 100%!
    nu_c, nv_c = 13, 11
    cape_f: list[list[int]] = []
    cape_b: list[list[int]] = []
    cape_uv: list[list[tuple[float, float]]] = []
    for iv in range(nv_c):
        vf = iv / (nv_c - 1)  # 0 top shoulders -> 1 bottom border hem
        z_cp = 1.348 - vf * 0.918
        if z_cp > 1.22:
            c_tag = "spine2"
        elif z_cp > 0.96:
            c_tag = "cape1"
        elif z_cp > 0.68:
            c_tag = "cape2"
        else:
            c_tag = "cape3"

        half_w_cp = spec["shoulder_w"] * 0.95 + vf * 0.150
        y_base = 0.112 + vf * 0.082 + 0.016 * math.sin(vf * math.pi)
        f_row, b_row, u_row = [], [], []
        for iu in range(nu_c):
            uf = iu / (nu_c - 1)
            us = (uf - 0.5) * 2.0
            # Heroic vertical blanket pleats growing deeper toward bottom hem (always behind sando!)
            pleat_y = (0.005 + 0.024 * vf) * math.sin(us * math.pi * 3.5)
            arch_y = +0.008 * (1.0 - us ** 2)
            hem_curve_z = -0.028 * (1.0 - us ** 2) * vf
            px = us * half_w_cp
            py_outer = y_base + pleat_y + arch_y + 0.0038
            py_inner = y_base + pleat_y + arch_y - 0.0038
            pz = z_cp + hem_curve_z
            f_row.append(cb.add_vert(px, py_outer, pz, c_tag))
            b_row.append(cb.add_vert(px, py_inner, pz, c_tag))
            # v=0.05 at bottom hem (where golden-white Filipino woven border sits!) -> v=0.95 at shoulders
            u_row.append(map_uv("prop_a", 0.05 + 0.90 * uf, 0.95 - 0.90 * vf))
        cape_f.append(f_row)
        cape_b.append(b_row)
        cape_uv.append(u_row)

    for iv in range(nv_c - 1):
        for iu in range(nu_c - 1):
            # Back-facing outer cape (+Y) & inner cape (-Y) + stitched perimeter edges
            cb.add_quad(
                cape_f[iv][iu], cape_f[iv + 1][iu], cape_f[iv + 1][iu + 1], cape_f[iv][iu + 1],
                cape_uv[iv][iu], cape_uv[iv + 1][iu], cape_uv[iv + 1][iu + 1], cape_uv[iv][iu + 1],
            )
            cb.add_quad(
                cape_b[iv][iu], cape_b[iv][iu + 1], cape_b[iv + 1][iu + 1], cape_b[iv + 1][iu],
                cape_uv[iv][iu], cape_uv[iv][iu + 1], cape_uv[iv + 1][iu + 1], cape_uv[iv + 1][iu],
            )
            if iu == 0:
                cb.add_quad(cape_b[iv][0], cape_b[iv + 1][0], cape_f[iv + 1][0], cape_f[iv][0], cape_uv[iv][0], cape_uv[iv + 1][0], cape_uv[iv + 1][0], cape_uv[iv][0])
            if iu == nu_c - 2:
                cb.add_quad(cape_f[iv][-1], cape_f[iv + 1][-1], cape_b[iv + 1][-1], cape_b[iv][-1], cape_uv[iv][-1], cape_uv[iv + 1][-1], cape_uv[iv + 1][-1], cape_uv[iv][-1])
            if iv == nv_c - 2:
                cb.add_quad(cape_b[-1][iu], cape_b[-1][iu + 1], cape_f[-1][iu + 1], cape_f[-1][iu], cape_uv[-1][iu], cape_uv[-1][iu + 1], cape_uv[-1][iu + 1], cape_uv[-1][iu])


def build_kid_kiko(cb: CharBuilder, spec: dict[str, Any]) -> None:
    """
    KIKO ("Tsinelas-Kamay Sprinter" — MANDATORY OWNER SPEC):
      - STRICTLY BAREFOOT at Z = 0.000 (sculpted bare heels, arches & 5 toes — ZERO slippers on feet!)
      - Inserts his teal-and-white rubber tsinelas ONTO BOTH OF HIS HANDS through the teal Y-straps
        with all 5 bare fingers extended flat along the white EVA footbed under the bright-teal Y-strap!
      - Sun-faded sleeveless mustard/charcoal V-neck tank + rolled dark-red shorts + knotted forehead bimpo!
    """
    build_valorant_head_and_neck(cb, spec, bare_shoulders=True)
    build_hair_swept_boy(cb, spec["head_scale"], style="kiko")
    arm_joints = build_base_arms_and_hands(cb, spec, arm_skin_from_t=0.0, straight_fingers=True)
    build_base_legs(cb, spec, z_top_L=0.56, z_top_R=0.56, z_bot_L=0.07, z_bot_R=0.07)

    # 1. STRICTLY BAREFOOT AT Z = 0.000! (NO footwear anywhere near his feet!)
    build_bare_feet(cb, spec, z_sole=0.000)

    # 2. Sun-Faded Mustard/Charcoal V-Neck Sprinter Tank + Rolled Dark-Red Shorts
    build_shirt_torso(cb, spec, z_bot=0.85, z_top=1.350, sando_cut=True, v_neck=True, loose=1.01)
    build_shorts_or_pants(cb, spec, z_hem_L=0.55, z_hem_R=0.55, baggy=1.08, rolled_cuff_L=True, rolled_cuff_R=True)

    # 3. Forehead Woven Bimpo Sweatband + Twin Knotted Back Tails!
    hs = spec["head_scale"] * 1.14
    bimpo_rings = []
    for z_b in (1.548, 1.576):
        bimpo_rings.append([
            (math.sin(2 * math.pi * j / 22) * 0.085 * hs, -math.cos(2 * math.pi * j / 22) * 0.102 * hs, z_b)
            for j in range(22)
        ])
    cb.add_garment_shell(bimpo_rings, "head", "prop_b", hem_thickness=0.004)
    cb.add_rounded_box((0.0, 0.106 * hs, 1.560), (0.018, 0.015, 0.016), (0, 0, 0.2), "head", "prop_b")
    for s_t in (-1.0, 1.0):
        cb.add_tube_along_curve(
            [(s_t * 0.010, 0.110 * hs, 1.554), (s_t * 0.032, 0.130 * hs, 1.516), (s_t * 0.045, 0.138 * hs, 1.472)],
            [(0.012, 0.005), (0.015, 0.004), (0.005, 0.003)],
            "head",
            "prop_b",
            n_sides=6,
            keel=0.25,
        )

    # 4. MANDATORY TEAL-AND-WHITE RUBBER TSINELAS INSERTED ONTO BOTH HANDS VIA Y-STRAPS!
    # Kiko's straight palm & 5 extended bare fingers rest flat ON TOP OF the white EVA footbed
    # while the bold bright-teal Y-strap (`strap_scale=1.45`) arches cleanly OVER his bare fingers/knuckles!
    slope = math.radians(10.0)
    cs, ss = math.cos(slope), math.sin(slope)
    for side, s_tag in ((1.0, "L"), (-1.0, "R")):
        wrist_p = arm_joints[f"wrist_{s_tag}"]
        dir_out = Vector((side * cs, -0.018, -ss)).normalized()
        dir_dorsum = Vector((side * ss, 0.0, cs)).normalized()
        y_slip = -dir_out
        z_slip = dir_dorsum
        x_slip = y_slip.cross(z_slip).normalized()

        slip_origin = wrist_p + dir_out * 0.054 - dir_dorsum * 0.030
        rot4 = Matrix((
            (x_slip.x, y_slip.x, z_slip.x, slip_origin.x),
            (x_slip.y, y_slip.y, z_slip.y, slip_origin.y),
            (x_slip.z, y_slip.z, z_slip.z, slip_origin.z),
            (0.0,      0.0,      0.0,      1.0),
        ))
        build_single_tsinelas_geometry(
            cb,
            rot4,
            tag_sole=f"hand_{s_tag}",
            tag_toe=f"hand_{s_tag}",
            sole_tile="prop_a",
            strap_tile="footwear",
            style="ystrap",
            sole_thick=0.020,
            length=0.252,
            width=0.116,
            side_sign=side,
            strap_scale=1.52,
            arch_height=0.040,
        )


def build_kid_ligaya(cb: CharBuilder, spec: dict[str, Any]) -> None:
    """
    LIGAYA ("Kumander Teks" — Concept-Accurate Spaghetti-Strap Sundress & Sleek Side-Part Ponytail!):
      - Coral-salmon sunflower 90s spaghetti-strap bestida sundress (bare shoulders!) over teal cycling shorts
      - Cross-body bandolier + hip holster packed with lastiko-bound teks stacks & pogs
      - Sleek side-parted hair + 2 bright 90s snap hairclips + neon scrunchie + voluminous wavy side ponytail!
    """
    build_valorant_head_and_neck(cb, spec, bare_shoulders=True)
    hs_raw = spec["head_scale"]
    hs = hs_raw * 1.14
    build_hair_sleek_parted_cap(cb, hs_raw, part_x=-0.024)

    # Ruffled 3D neon-green scrunchie perched high on right-back crown so it reads clearly in 3/4 view!
    sc_x, sc_y, sc_z = -0.028 * hs, +0.084 * hs, 1.608
    for k in range(7):
        ang = 2.0 * math.pi * k / 7
        cb.add_rounded_box(
            (sc_x + math.cos(ang) * 0.026, sc_y + math.sin(ang) * 0.012, sc_z + math.sin(ang) * 0.022),
            (0.015, 0.012, 0.015),
            (0, ang, 0),
            "head",
            "prop_b",
            n_ring=8,
            n_stacks=4,
        )
    # Voluminous 4-lock wavy high ponytail sweeping out & over right shoulder (`hair1 -> hair2`)!
    for dx, dy_o, w_l in [(-0.025, 0.0, 0.038), (-0.048, -0.018, 0.030), (-0.005, +0.015, 0.028), (-0.036, +0.022, 0.026)]:
        pts = [
            (sc_x + dx * 0.3, sc_y + 0.010, sc_z),
            (-0.058 + dx, +0.118 + dy_o, 1.622),
            (-0.092 + dx, +0.096 + dy_o, 1.542),
            (-0.082 + dx, +0.068 + dy_o, 1.438),
            (-0.102 + dx, +0.058 + dy_o, 1.342),
        ]
        rads = [(w_l * 0.65, w_l * 0.55), (w_l, w_l * 0.82), (w_l * 0.92, w_l * 0.72), (w_l * 0.64, w_l * 0.52), (0.009, 0.009)]
        cb.add_tube_along_curve(pts, rads, ["head", "hair1", "hair1", "hair2", "hair2"], "hair", n_sides=6, keel=0.30)

    # 2 Prominent Colorful 90s Snap Hairclips (Hot-Pink & Neon-Green) on right temple!
    for dz_c, tile_c in ((0.0, "footwear"), (0.016, "prop_b")):
        cb.add_rounded_box((-0.079 * hs, -0.056 * hs, 1.556 + dz_c), (0.005, 0.022, 0.006), (0, 0.28, -0.42), "head", tile_c)

    # Bare arms & shoulders (`arm_skin_from_t=0.0`) + Spaghetti-Strap Coral Sunflower Sundress!
    build_base_arms_and_hands(cb, spec, arm_skin_from_t=0.0)
    build_base_legs(cb, spec, z_top_L=0.55, z_top_R=0.55, z_bot_L=0.07, z_bot_R=0.07)
    build_foot_tsinelas(cb, spec, style="ystrap", mismatched=True)

    build_shirt_torso(
        cb,
        spec,
        z_bot=0.67,
        z_top=1.352,
        sando_cut=True,
        strap_width_scale=0.44,
        bestida_skirt_to=0.67,
        loose=1.02,
    )
    build_shorts_or_pants(cb, spec, z_hem_L=0.53, z_hem_R=0.53, baggy=0.86)

    # Diagonal Cross-Body Bandolier + Stacked Teks Card Bundles + Circular Pogs + Right-Hip Holster!
    build_diagonal_chest_strap(cb, spec, shoulder_side=1.0, width=0.030, thick=0.008, tile="prop_a", z_hip=0.90)
    for t_pos, rot_z in [((+0.052, -0.106, 1.21), -0.55), ((+0.004, -0.108, 1.12), -0.55), ((-0.046, -0.104, 1.03), -0.55)]:
        cb.add_rounded_box(t_pos, (0.020, 0.014, 0.028), (0.08, 0.0, rot_z), "spine1", "prop_b")
        cb.add_rounded_box((t_pos[0] + 0.024, t_pos[1] - 0.006, t_pos[2] - 0.018), (0.016, 0.005, 0.016), (0, 0, 0), "spine1", "bands_metal")
    cb.add_rounded_box((-spec["hip_w"] * 1.15, -0.018, 0.88), (0.024, 0.044, 0.052), (0, 0, 0.12), "hips", "prop_a")


def build_kid_migo(cb: CharBuilder, spec: dict[str, Any]) -> None:
    """
    MIGO ("Plastik-Balloon Alchemist"):
      - Side-knotted pastel 90s graphic tee + lavender sleeves
      - Rolled puruntong denim shorts w/ patches + chunky street navy slide tsinelas
      - Diagonal holographic 90s belt-bag (fanny pack) w/ plastik-balloon tubes & straws
      - Translucent hot-magenta 90s sun-visor headband + stacked wrist pulseras!
    """
    build_valorant_head_and_neck(cb, spec)
    hs_raw = spec["head_scale"]
    hs = hs_raw * 1.14
    build_hair_swept_boy(cb, hs_raw, style="migo")

    # Hot-Magenta 90s Plastic Sun-Visor Headband!
    visor_band = []
    for z_v in (1.546, 1.568):
        visor_band.append([
            (math.sin(2 * math.pi * j / 22) * 0.085 * hs, -math.cos(2 * math.pi * j / 22) * 0.102 * hs, z_v)
            for j in range(22)
        ])
    cb.add_garment_shell(visor_band, "head", "torso_under", hem_thickness=0.004)
    v_bill = [
        (0.0, -0.096 * hs, 1.558),
        (0.0, -0.136 * hs, 1.548),
        (0.0, -0.168 * hs, 1.536),
    ]
    cb.add_tube_along_curve(
        v_bill,
        [(0.0044, 0.076), (0.0040, 0.072), (0.0032, 0.054)],
        "head",
        "prop_b",
        n_sides=8,
        up_hint=Vector((0.0, 0.0, 1.0)),
    )

    build_base_arms_and_hands(cb, spec, arm_skin_from_t=0.32)
    build_base_legs(cb, spec, z_top_L=0.49, z_top_R=0.49, z_bot_L=0.07, z_bot_R=0.07)
    build_foot_tsinelas(cb, spec, style="slide")

    # Side-knotted Pastel Mint Tee + Contrast Lavender Short Sleeves + Rolled Puruntong Denim Shorts
    build_shirt_torso(cb, spec, z_bot=0.84, z_top=1.355, loose=1.08, side_knot_left=True)
    build_shirt_sleeves(cb, spec, t_end=0.35, cuff_flare=1.22, tile="torso_under")
    build_shorts_or_pants(cb, spec, z_hem_L=0.48, z_hem_R=0.48, baggy=1.18, rolled_cuff_L=True, rolled_cuff_R=True, cuff_tile="socks_acc")

    # Diagonal Holographic 90s Fanny Pack across chest + Plastik-Balloon Tubes & Red Blowing Straws!
    build_diagonal_chest_strap(cb, spec, shoulder_side=-1.0, width=0.026, thick=0.007, tile="prop_a", z_hip=0.94)
    cb.add_rounded_box((0.008, -0.114, 1.135), (0.068, 0.032, 0.038), (0.10, 0.42, 0.0), "spine1", "prop_a")
    # Plastik-balloon tubes + tiny red straws peeking out top of fanny pack!
    for dx_t, dz_t in ((-0.022, 0.034), (0.012, 0.038)):
        cb.add_tube_along_curve(
            [(0.008 + dx_t, -0.118, 1.150), (0.014 + dx_t, -0.124, 1.150 + dz_t)],
            [(0.009, 0.007), (0.006, 0.005)],
            "spine1",
            "prop_b",
            n_sides=6,
        )
        cb.add_tube_along_curve(
            [(0.018 + dx_t, -0.122, 1.155), (0.026 + dx_t, -0.130, 1.198)],
            [(0.0025, 0.0025), (0.0025, 0.0025)],
            "spine1",
            "prop_b",
            n_sides=5,
        )

    # Stacked Colorful Pulseras Bracelets on BOTH Wrists!
    slope = math.radians(10.0)
    for side, s_tag in ((1.0, "L"), (-1.0, "R")):
        for idx_p, dist_p in enumerate((0.39, 0.41, 0.43)):
            wp = Vector((side * (spec["shoulder_w"] + dist_p * math.cos(slope)), -0.002, 1.338 - dist_p * math.sin(slope)))
            cb.add_rounded_box((wp.x, wp.y, wp.z), (0.007, 0.029, 0.024), (0, side * 0.17, 0), f"forearm_{s_tag}", "bands_metal" if idx_p % 2 == 0 else "prop_b")


def build_kid_toni(cb: CharBuilder, spec: dict[str, Any]) -> None:
    """
    TONI ("Barangay Liga MVP"):
      - Oversized #08 crimson/gold mesh Barangay Liga jersey over wide-sleeve white tee
      - Backwards charcoal/teal snapback cap over 90s undercut
      - Baggy olive cargo shorts + tucked white bimpo + carabiner & 3 crimped tansan bottlecaps
      - White athletic finger tape + striped crew socks + chevron 90s high-top sneakers!
    """
    build_valorant_head_and_neck(cb, spec)
    # 90s Undercut side/nape fade + Backwards Charcoal/Teal Snapback Cap!
    build_backwards_cap(cb, spec, tile_dome="prop_a", tile_brim="prop_b")
    # Crisp front fringe peeking through backwards snapback window
    hs_t = spec["head_scale"] * 1.14
    for dx_f in (-0.018, 0.0, +0.018):
        cb.add_valorant_hair_lock(
            [(dx_f, -0.098 * hs_t, 1.572), (dx_f * 1.2, -0.112 * hs_t, 1.556), (dx_f * 1.3, -0.108 * hs_t, 1.542)],
            0.016, 0.010, tag="head", tile="hair"
        )

    arm_j = build_base_arms_and_hands(cb, spec, arm_skin_from_t=0.40)
    build_base_legs(cb, spec, z_top_L=0.49, z_top_R=0.49, z_bot_L=0.12, z_bot_R=0.12, sock_top_L=0.25, sock_top_R=0.25)
    build_sneakers_or_sandals(cb, spec, kind="hightop")

    # Wide-Sleeve White Undershirt + Oversized #08 Barangay Liga Mesh Jersey + Baggy Olive Cargo Shorts
    build_shirt_torso(cb, spec, z_bot=0.81, z_top=1.358, loose=1.06, tile_front="torso_under", tile_back="torso_under")
    build_shirt_sleeves(cb, spec, t_end=0.43, cuff_flare=1.36, tile="torso_under")
    build_shirt_torso(cb, spec, z_bot=0.83, z_top=1.345, sando_cut=True, loose=1.12, tile_front="torso_front", tile_back="torso_back")
    build_shorts_or_pants(cb, spec, z_hem_L=0.48, z_hem_R=0.48, baggy=1.24, cargo_pockets=True)

    # Tucked White Bimpo Towel hanging from right-back waist + Carabiner & 3 Crimped Tansan Bottlecaps!
    cb.add_tube_along_curve(
        [(-0.078, +0.072, 0.855), (-0.092, +0.086, 0.765), (-0.085, +0.080, 0.680)],
        [(0.026, 0.010), (0.032, 0.010), (0.024, 0.007)],
        "hips",
        "torso_under",
        n_sides=6,
        keel=0.2,
    )
    # Carabiner + 3 tansan bottlecaps at front-left belt loop
    kx_t = +0.068
    cb.add_rounded_box((kx_t, -0.092, 0.855), (0.008, 0.005, 0.016), (0, 0, 0), "hips", "bands_metal")
    for dz_cap, dx_cap in ((-0.028, -0.005), (-0.048, +0.006), (-0.068, -0.003)):
        cb.add_rounded_box((kx_t + dx_cap, -0.095, 0.855 + dz_cap), (0.013, 0.005, 0.013), (0.1, 0, 0), "hips", "bands_metal")

    # White Athletic Finger Tape Rings on Index/Middle/Thumb!
    for s_tag in ("L", "R"):
        for f_key in (f"{s_tag}_Index2", f"{s_tag}_Middle2", f"{s_tag}_Thumb2"):
            fp = arm_j[f_key]
            cb.add_rounded_box((fp.x, fp.y, fp.z), (0.008, 0.010, 0.010), (0, 0, 0), f"hand_{s_tag}", "torso_under")


def build_kid_popoy(cb: CharBuilder, spec: dict[str, Any]) -> None:
    """
    POPOY ("Tangke ng Tanghali"):
      - Stocky sturdy tank build + 90s flat-top fade
      - Open vertical-striped collared polo over snug white cotton undershirt
      - Packing-taped corrugated-cardboard kalasag forearm/chest guard on left forearm
      - Right-hip insulated ice-candy pouch w/ 3 colorful tubes + navy shorts + wide tsinelas!
    """
    build_valorant_head_and_neck(cb, spec)
    hs = spec["head_scale"] * 1.14
    # Crisp 90s Flat-Top Fade contoured to compact Valorant head!
    ft_rings = []
    for z_f, rx_f, ry_f, sq in [
        (1.482, 0.079 * hs, 0.095 * hs, 0.90),
        (1.548, 0.085 * hs, 0.102 * hs, 0.82),
        (1.624, 0.086 * hs, 0.104 * hs, 0.65),
        (1.658, 0.079 * hs, 0.096 * hs, 0.60),
    ]:
        ring = []
        for j in range(20):
            th = 2.0 * math.pi * j / 20
            ca, sa = math.cos(th), math.sin(th)
            if z_f < 1.552 and ca > 0.25:
                ring.append((sa * rx_f, -0.025, 1.552))
            else:
                ring.append((
                    math.copysign(abs(sa) ** sq, sa) * rx_f,
                    -math.copysign(abs(ca) ** sq, ca) * ry_f,
                    z_f,
                ))
        ft_rings.append(ring)
    cb.add_loft(ft_rings, "head", "hair", closed_ring=True, cap_end=True)

    arm_j = build_base_arms_and_hands(cb, spec, arm_skin_from_t=0.34)
    build_base_legs(cb, spec, z_top_L=0.52, z_top_R=0.52, z_bot_L=0.07, z_bot_R=0.07)
    build_foot_tsinelas(cb, spec, style="ystrap")

    # White Crew Undershirt + Open Unbuttoned Vertical-Striped Collared Polo + Sturdy Navy Shorts
    build_shirt_torso(cb, spec, z_bot=0.84, z_top=1.356, loose=1.02, tile_front="torso_under", tile_back="torso_under")
    build_open_overshirt_or_vest(cb, spec, tile_front="torso_front", tile_back="torso_back", z_bot=0.82, has_collar=True)
    build_shirt_sleeves(cb, spec, t_end=0.35, cuff_flare=1.20, tile="torso_front")
    build_shorts_or_pants(cb, spec, z_hem_L=0.50, z_hem_R=0.50, baggy=1.18)

    # Corrugated-Cardboard KALASAG Forearm Shield strapped to Left Forearm w/ 3D Tan Packing-Tape Bands!
    elb_L = arm_j["elbow_L"]
    wri_L = arm_j["wrist_L"]
    mid_fa = elb_L.lerp(wri_L, 0.52)
    # Curved 8mm-thick fluted cardboard shield mounted on outer-anterior forearm
    sh_center = mid_fa + Vector((0.008, -0.046, 0.012))
    cb.add_rounded_box(
        (sh_center.x, sh_center.y, sh_center.z),
        (0.132, 0.012, 0.082),
        (0.18, 0.17, 0.0),
        "forearm_L",
        "prop_a",
        bevel_power=0.55,
    )
    # Two 3D Tan Packing-Tape Straps wrapping all the way around shield + left forearm!
    for t_tape in (0.30, 0.74):
        tp_c = elb_L.lerp(wri_L, t_tape)
        cb.add_rounded_box(
            (tp_c.x, tp_c.y - 0.018, tp_c.z + 0.004),
            (0.016, 0.056, 0.086),
            (0.18, 0.17, 0.0),
            "forearm_L",
            "socks_acc",
            bevel_power=0.60,
        )

    # Right-Hip Insulated Ice-Candy Pouch + 3 Tied Colorful Ice-Candy Tubes Sticking Out!
    px_ic = -spec["hip_w"] * 1.15
    cb.add_rounded_box((px_ic, -0.012, 0.875), (0.032, 0.048, 0.056), (0, 0, 0.10), "hips", "prop_b")
    for dy_i, tilt_x in ((-0.022, -0.18), (0.0, 0.02), (+0.022, +0.20)):
        cb.add_tube_along_curve(
            [(px_ic - 0.004, -0.012 + dy_i, 0.915), (px_ic - 0.012, -0.012 + dy_i * 1.3, 0.978)],
            [(0.011, 0.011), (0.009, 0.009)],
            "hips",
            "bands_metal",
            n_sides=7,
        )


def build_kid_inday(cb: CharBuilder, spec: dict[str, Any]) -> None:
    """
    INDAY ("Reyna ng Patintero"):
      - Rich deep morena skin + sleek Dutch-braided crown flowing into twin thick shoulder braids
        wrapped w/ colorful crisscrossed lastiko rubberband chains (ZERO spiky cones!)
      - Warm cream-ecru #95 public-school PE ringer shirt w/ golden-amber collar & sleeve cuff bands
      - Royal-navy double-stripe track pants with ONE PANT-LEG ROLLED UP BELOW KNEE (Left)
        and THE OTHER FULL-LENGTH (Right) + white/gum PE canvas sneakers!
    """
    build_valorant_head_and_neck(cb, spec)
    hs_raw = spec["head_scale"]
    hs = hs_raw * 1.14
    # Sleek center-parted Dutch-braided hair cap (ZERO spiky cones!) + TWO THICK SHOULDER BRAIDS!
    build_hair_sleek_parted_cap(cb, hs_raw, part_x=0.0, dutch_braids=True)
    for side in (-1.0, 1.0):
        braid_pts = [
            (side * 0.074 * hs, +0.028, 1.515),
            (side * 0.094,      -0.022, 1.425),
            (side * 0.102,      -0.086, 1.338),
            (side * 0.098,      -0.110, 1.250),
            (side * 0.092,      -0.114, 1.175),
            (side * 0.088,      -0.112, 1.115),
        ]
        b_tags = ["head", "neck", "spine2", "spine2", "spine1", "spine1"]
        b_rads = [(0.022, 0.020), (0.024, 0.021), (0.023, 0.020), (0.020, 0.018), (0.016, 0.014), (0.009, 0.008)]
        cb.add_tube_along_curve(braid_pts, b_rads, b_tags, "hair", n_sides=6, keel=0.25)
        # 4 Colorful Crisscrossed Lastiko Rubberband Wraps along each braid!
        for idx_b, (bp, bt) in enumerate(zip(braid_pts[1:5], b_tags[1:5])):
            cb.add_rounded_box(
                bp,
                (0.026, 0.023, 0.009),
                (0.22, 0.0, 0.18 if idx_b % 2 == 0 else -0.18),
                bt,
                "prop_b" if idx_b % 2 == 0 else "torso_under",
            )

    build_base_arms_and_hands(cb, spec, arm_skin_from_t=0.32)
    build_base_legs(cb, spec, z_top_L=0.45, z_top_R=0.16, z_bot_L=0.08, z_bot_R=0.08, sock_top_L=0.16, sock_top_R=0.15)
    build_sneakers_or_sandals(cb, spec, kind="lowtop")

    # Warm Cream-Ecru #95 Gold PE Ringer Tee + Asymmetric Navy Track Pants (Left rolled z=0.43, Right full z=0.125!)
    build_shirt_torso(cb, spec, z_bot=0.85, z_top=1.358, loose=1.03)
    build_shirt_sleeves(cb, spec, t_end=0.33, cuff_flare=1.15, tile="torso_front")
    build_shorts_or_pants(
        cb,
        spec,
        z_hem_L=0.43,
        z_hem_R=0.125,
        baggy=1.16,
        rolled_cuff_L=True,
        rolled_cuff_R=False,
    )

    # Black Braided Neck Lanyard + Bright Red Referee Whistle on Chest!
    lanyard_pts = [
        (-0.048, +0.025, 1.365),
        (-0.056, -0.068, 1.315),
        (-0.022, -0.104, 1.215),
        ( 0.000, -0.108, 1.175),
        (+0.022, -0.104, 1.215),
        (+0.056, -0.068, 1.315),
        (+0.048, +0.025, 1.365),
    ]
    cb.add_tube_along_curve(lanyard_pts, [(0.0032, 0.0032)] * len(lanyard_pts), "spine2", "bands_metal", n_sides=5)
    cb.add_rounded_box((0.0, -0.114, 1.162), (0.011, 0.014, 0.020), (0.2, 0, 0), "spine2", "prop_a")


def build_kid_nene(cb: CharBuilder, spec: dict[str, Any]) -> None:
    """
    NENE ("Tirador Scout"):
      - Indigo denim pinafore/shortall jumper over mustard-and-cream striped tee
      - Carved wooden Y-fork tirador slingshot HELD IN RIGHT HAND + plump holen marble pouch at left hip
      - Sleek center-parted hair + 2 big round space-buns w/ colorful butterfly clips + 3D nose band-aid!
    """
    build_valorant_head_and_neck(cb, spec)
    hs_raw = spec["head_scale"]
    hs = hs_raw * 1.14
    # Sleek center-parted hair cap (ZERO spiky cones!) + 2 big round high space-buns!
    build_hair_sleek_parted_cap(cb, hs_raw, part_x=0.0)

    # Twin Big Round Space-Buns + 4 Winged Colorful 90s Butterfly Hairclips + 2 Temple Snap Clips!
    for side in (-1.0, 1.0):
        bx_c, by_c, bz_c = side * 0.088 * hs, +0.010, 1.622
        cb.add_rounded_box((bx_c, by_c, bz_c), (0.052, 0.052, 0.048), (0, side * 0.25, 0), "head", "hair", bevel_power=0.88)
        for dy_clip, tile_cl in ((-0.040, "prop_b_top"), (+0.012, "bands_metal")):
            cb.add_rounded_box((bx_c - side * 0.028, by_c + dy_clip, bz_c + 0.018), (0.018, 0.008, 0.015), (0.25, -side * 0.35, 0.2), "head", tile_cl)
        cb.add_rounded_box((side * 0.078 * hs, -0.058 * hs, 1.558), (0.005, 0.018, 0.005), (0, -side * 0.25, side * 0.35), "head", "socks_acc")

    # Prominent 3D Tan Band-Aid Plaster across Nasal Bridge!
    cb.add_rounded_box((0.0, -0.106 * hs, 1.498), (0.025, 0.0055, 0.0092), (-0.18, 0.0, 0.06), "head", "prop_b_bot")

    arm_j = build_base_arms_and_hands(cb, spec, arm_skin_from_t=0.32)
    build_base_legs(
        cb, spec, z_top_L=0.54, z_top_R=0.54, z_bot_L=0.08, z_bot_R=0.08,
        sock_top_L=0.35, sock_top_R=0.31, sock_tile_L="sole_alt", sock_tile_R="socks_acc"
    )
    build_sneakers_or_sandals(cb, spec, kind="sandal")

    # Mustard Striped Tee Underneath + Indigo Denim Bib-Overall Jumper Over Top!
    build_shirt_torso(cb, spec, z_bot=0.86, z_top=1.356, loose=1.01, tile_front="torso_under", tile_back="torso_under")
    build_shirt_sleeves(cb, spec, t_end=0.32, cuff_flare=1.18, tile="torso_under")
    build_shirt_torso(cb, spec, z_bot=0.84, z_top=1.245, sando_cut=True, loose=1.06, tile_front="torso_front", tile_back="torso_back")
    for side in (-1.0, 1.0):
        strap_pts = [
            (side * 0.056, -0.098, 1.225),
            (side * 0.064, -0.072, 1.326),
            (side * 0.065, -0.032, 1.344),
            (side * 0.064, +0.008, 1.346),
            (side * 0.062, +0.066, 1.328),
            (-side * 0.035, +0.094, 1.150),
        ]
        cb.add_tube_along_curve(strap_pts, [(0.018, 0.0055)] * len(strap_pts), "spine2", "torso_front", n_sides=6)
        cb.add_rounded_box((side * 0.056, -0.104, 1.218), (0.009, 0.005, 0.009), (0, 0, 0), "spine2", "bands_metal")
    build_shorts_or_pants(cb, spec, z_hem_L=0.53, z_hem_R=0.53, baggy=1.14, rolled_cuff_L=True, rolled_cuff_R=True)

    # Plump Left-Hip Holen Marble Pouch + HANDHELD Carved Wooden Y-Fork TIRADOR Slingshot in Right Hand (`hand_R`)!
    cb.add_rounded_box((+spec["hip_w"] * 1.16, -0.015, 0.875), (0.032, 0.038, 0.042), (0, 0, -0.15), "hips", "prop_a", bevel_power=0.85)
    slope = math.radians(10.0)
    ax_r = Vector((-math.cos(slope), -0.018, -math.sin(slope))).normalized()
    up_r = Vector((-math.sin(slope), 0.0, math.cos(slope))).normalized()
    fwd_r = Vector((0.0, -1.0, 0.0))
    grip_c = arm_j["wrist_R"] + ax_r * 0.055 + fwd_r * 0.010
    # Wooden Y-handle rising along +fwd_r (out of thumb side of fist!) + wide carved Y-forks + amber lastiko band & leather pouch!
    h_bot = grip_c - fwd_r * 0.048
    h_split = grip_c + fwd_r * 0.045
    f_left = h_split + fwd_r * 0.072 + up_r * 0.044
    f_right = h_split + fwd_r * 0.072 - up_r * 0.044
    pouch_b = h_split + fwd_r * 0.015 - ax_r * 0.052
    cb.add_tube_along_curve([tuple(h_bot), tuple(h_split)], [(0.011, 0.009), (0.013, 0.011)], "hand_R", "prop_a", n_sides=6)
    cb.add_tube_along_curve([tuple(h_split), tuple(f_left)], [(0.011, 0.009), (0.009, 0.008)], "hand_R", "prop_a", n_sides=6)
    cb.add_tube_along_curve([tuple(h_split), tuple(f_right)], [(0.011, 0.009), (0.009, 0.008)], "hand_R", "prop_a", n_sides=6)
    cb.add_tube_along_curve(
        [tuple(f_left), tuple(pouch_b), tuple(f_right)],
        [(0.0045, 0.0035), (0.011, 0.006), (0.0045, 0.0035)],
        "hand_R",
        "bands_metal",
        n_sides=5,
    )
    # Jade bangle on left wrist
    w_l = Vector((spec["shoulder_w"] + 0.41 * math.cos(slope), 0.0, 1.338 - 0.41 * math.sin(slope)))
    cb.add_rounded_box((w_l.x, w_l.y, w_l.z), (0.007, 0.030, 0.025), (0, 0.17, 0), "forearm_L", "socks_acc")


def build_kid_buknoy(cb: CharBuilder, spec: dict[str, Any]) -> None:
    """
    BUKNOY ("Mekaniko ng Kolong" — Exact Concept Match to buknoy_concept.png):
      - Sleeveless frayed rust-plaid flannel vest over sleeveless smudged grey tank + 100% BARE ARMS!
      - MANDATORY PLUMP ROUND TUBULAR BLACK VULCANIZED RUBBER BICYCLE INNER-TUBE (interior ng gulong)
        crossed in a bold X-bandolier across shoulders/chest (w/ brass Schrader valve stem!)
        & coiled around his bare left forearm (w/ 2nd brass valve stem!)
      - Backwards slate-navy mechanic cap + tan tool belt w/ side pouches + frayed maong shorts + tire-sole tsinelas!
    """
    build_valorant_head_and_neck(cb, spec, bare_shoulders=True)
    build_backwards_cap(cb, spec, tile_dome="prop_b", tile_brim="prop_b")
    # Tousled dark mechanic side-locks peeking cleanly from cap temples & brow window
    hs_b = spec["head_scale"] * 1.14
    for sx_h in (-0.068 * hs_b, -0.016, +0.020, +0.068 * hs_b):
        z_tip = 1.518 if abs(sx_h) > 0.05 else 1.544
        y_mul = 0.062 * hs_b if abs(sx_h) > 0.05 else 0.110 * hs_b
        cb.add_valorant_hair_lock(
            [(sx_h * 0.92, -y_mul * 0.88, 1.568), (sx_h, -y_mul, 0.5 * (1.568 + z_tip)), (sx_h * 1.04, -y_mul * 0.96, z_tip)],
            0.018, 0.011, tag="head", tile="hair"
        )

    arm_j = build_base_arms_and_hands(cb, spec, arm_skin_from_t=0.0)
    build_base_legs(cb, spec, z_top_L=0.51, z_top_R=0.51, z_bot_L=0.07, z_bot_R=0.07)
    build_foot_tsinelas(cb, spec, style="ystrap", thick_tire=True)

    # Sleeveless Smudged Grey Tank + Sleeveless Cutoff Frayed Rust-Plaid Flannel Vest + Frayed Maong Shorts
    build_shirt_torso(cb, spec, z_bot=0.85, z_top=1.350, sando_cut=True, loose=1.01, tile_front="torso_under", tile_back="torso_under")
    build_open_overshirt_or_vest(cb, spec, tile_front="torso_front", tile_back="torso_back", z_bot=0.84, has_collar=True)
    build_shorts_or_pants(cb, spec, z_hem_L=0.50, z_hem_R=0.50, baggy=1.18, rolled_cuff_L=True, rolled_cuff_R=True, cuff_tile="socks_acc")

    # Tan Canvas Mechanic Tool Belt + Brass Buckle + Twin Side Pouches (per buknoy_concept.png!)
    belt_rings = []
    for z_bl in (0.865, 0.905):
        belt_rings.append([
            (math.sin(2 * math.pi * j / 24) * spec["waist_w"] * 1.15, 0.003 - math.cos(2 * math.pi * j / 24) * 0.095, z_bl)
            for j in range(24)
        ])
    cb.add_garment_shell(belt_rings, "hips", "socks_acc", hem_thickness=0.005)
    cb.add_rounded_box((0.0, -0.100, 0.885), (0.024, 0.009, 0.022), (0, 0, 0), "hips", "bands_metal")
    cb.add_rounded_box((-spec["hip_w"] * 1.16, -0.006, 0.850), (0.028, 0.048, 0.058), (0, 0, 0.12), "hips", "socks_acc")
    cb.add_rounded_box((+spec["hip_w"] * 1.14, +0.006, 0.855), (0.024, 0.042, 0.050), (0, 0, -0.12), "hips", "socks_acc")

    # MANDATORY PLUMP ROUND TUBULAR BLACK VULCANIZED BICYCLE INNER-TUBE X-HARNESS (per buknoy_concept.png)!
    sw_b = spec["shoulder_w"]
    ww_b = spec["waist_w"]
    for sh_s, dy_cross in ((+1.0, -0.018), (-1.0, -0.002)):
        loop_pts = []
        loop_tags = []
        n_lp = 16
        for k in range(n_lp + 1):
            ang = 2.0 * math.pi * k / n_lp
            ca, sa = math.cos(ang), math.sin(ang)
            # Tilted 3D elliptical rubber inner-tube loop from shoulder (ca=+1) across chest/back to opposite rib/waist (ca=-1)!
            z_lp = 1.175 + 0.195 * ca
            x_lp = sh_s * (0.58 * sw_b * ca - (ww_b * 1.16) * (1.0 - ca) * 0.52) + sh_s * 0.020 * abs(sa)
            y_lp = -sa * (0.122 + 0.010 * (1.0 - abs(ca))) + (dy_cross if sa > 0.0 else +0.006)
            loop_pts.append((x_lp, y_lp, z_lp))
            loop_tags.append("spine2" if z_lp > 1.20 else ("spine1" if z_lp > 1.04 else "spine"))
        cb.add_tube_along_curve(loop_pts, [(0.024, 0.019)] * len(loop_pts), loop_tags, "prop_a", n_sides=8)

    # Protruding 3D Metallic Brass Schrader Valve Stem jutting out of left-chest inner-tube!
    cb.add_tube_along_curve(
        [(+0.068, -0.124, 1.215), (+0.092, -0.148, 1.226)],
        [(0.0065, 0.0065), (0.0050, 0.0050)],
        "spine2",
        "bands_metal",
        n_sides=6,
    )
    # 2) Multi-Turn Plump Round Black Vulcanized Inner-Tube Coiled Around Bare Left Forearm + 2nd Brass Valve Stem!
    elb_L = arm_j["elbow_L"]
    wri_L = arm_j["wrist_L"]
    for frac_c in (0.48, 0.64, 0.80):
        cp = elb_L.lerp(wri_L, frac_c)
        coil_pts = []
        for k in range(13):
            ang_c = 2.0 * math.pi * k / 12
            coil_pts.append((cp.x + 0.004 * math.cos(ang_c), cp.y + 0.044 * math.cos(ang_c), cp.z + 0.042 * math.sin(ang_c)))
        cb.add_tube_along_curve(coil_pts, [(0.013, 0.011)] * len(coil_pts), "forearm_L", "prop_a", n_sides=6)
    v2 = elb_L.lerp(wri_L, 0.64)
    cb.add_tube_along_curve(
        [(v2.x, v2.y - 0.046, v2.z + 0.012), (v2.x + 0.010, v2.y - 0.074, v2.z + 0.022)],
        [(0.0055, 0.0055), (0.0045, 0.0045)],
        "forearm_L",
        "bands_metal",
        n_sides=6,
    )


def build_kid_tala(cb: CharBuilder, spec: dict[str, Any]) -> None:
    """
    TALA ("Komiks Pintor"):
      - Paint-splattered boxy purple tee over black-and-white striped knit long sleeves
      - Fingerless sock-gloves + asymmetrical jet-black hairpin bob (3 colorful pins!)
      - Diagonal kraft-cardboard komiks poster tube slung across back
      - Left-hip chalk/komiks utility waist apron + khaki shorts + star-doodled canvas sneakers!
    """
    build_valorant_head_and_neck(cb, spec)
    hs = spec["head_scale"] * 1.14
    # Sleek Voluminous Valorant Asymmetrical Chin-Length Jet-Black A-Line Bob!
    # Right side (-X) sweeps into a thick geometric jaw-length blade (z=1.418); Left side (+X) tucks w/ 3 colorful snap pins!
    bob_rings = []
    for z_b, rx_b, ry_b in [
        (1.418, 0.088 * hs, 0.098 * hs),
        (1.478, 0.094 * hs, 0.105 * hs),
        (1.540, 0.093 * hs, 0.107 * hs),
        (1.610, 0.086 * hs, 0.103 * hs),
        (1.660, 0.050 * hs, 0.062 * hs),
    ]:
        ring = []
        for j in range(24):
            th = 2.0 * math.pi * j / 24
            sa, ca = math.sin(th), math.cos(th)
            z_eff = z_b
            rx_eff = rx_b
            if z_b < 1.564 and ca > 0.30:
                z_eff = 1.562 - 0.022 * max(0.0, -sa)
                ry_use = 0.035
            else:
                ry_use = ry_b
                if sa > 0.35 and z_b < 1.462:
                    z_eff = 1.462
            ring.append((sa * rx_eff, -ca * ry_use, z_eff))
        bob_rings.append(ring)
    cb.add_loft(bob_rings, "head", "hair", closed_ring=True, cap_end=True)

    # Diagonal Side-Swept Front Bangs draping across Tala's right forehead & brow + sharp jaw-length A-line side locks!
    tala_locks = [
        ([(+0.024 * hs, -0.086 * hs, 1.594), (-0.014 * hs, -0.106 * hs, 1.568), (-0.048 * hs, -0.102 * hs, 1.536)], 0.044, 0.016),
        ([(-0.012 * hs, -0.088 * hs, 1.596), (-0.046 * hs, -0.104 * hs, 1.564), (-0.072 * hs, -0.088 * hs, 1.528)], 0.042, 0.016),
        ([(-0.070 * hs, -0.056 * hs, 1.562), (-0.086 * hs, -0.068 * hs, 1.486), (-0.072 * hs, -0.078 * hs, 1.414)], 0.034, 0.016),
        ([(+0.070 * hs, -0.050 * hs, 1.558), (+0.082 * hs, -0.058 * hs, 1.502), (+0.074 * hs, -0.062 * hs, 1.458)], 0.028, 0.014),
    ]
    for tl_pts, bw, bt in tala_locks:
        cb.add_valorant_hair_lock(tl_pts, bw, bt, tag="head", tile="hair", taper_power=1.12)

    # 3 Prominent Colorful Snap Hairpins (Yellow, Cyan, Magenta) on Left Temple!
    for dz_p, tile_p in ((0.0, "bands_metal"), (0.013, "torso_front"), (0.026, "socks_acc")):
        cb.add_rounded_box((+0.081 * hs, -0.054 * hs, 1.542 + dz_p), (0.005, 0.021, 0.005), (0, -0.22, 0.35), "head", tile_p)

    # Fingerless sock-gloves + long striped knit sleeves under boxy paint-splattered purple tee!
    build_base_arms_and_hands(cb, spec, arm_skin_from_t=0.92, glove_hands=True)
    build_base_legs(cb, spec, z_top_L=0.52, z_top_R=0.52, z_bot_L=0.08, z_bot_R=0.08, sock_top_L=0.19, sock_top_R=0.19)
    build_sneakers_or_sandals(cb, spec, kind="lowtop")

    build_shirt_sleeves(cb, spec, t_end=0.96, cuff_flare=0.92, tile="torso_under")
    build_shirt_torso(cb, spec, z_bot=0.83, z_top=1.356, loose=1.10, tile_front="torso_front", tile_back="torso_back")
    build_shirt_sleeves(cb, spec, t_end=0.36, cuff_flare=1.32, tile="torso_front")
    build_shorts_or_pants(cb, spec, z_hem_L=0.50, z_hem_R=0.50, baggy=1.16, cargo_pockets=True)

    # Diagonal Kraft-Cardboard Komiks Poster Tube slung across Back + Khaki Canvas Chest Strap!
    build_diagonal_chest_strap(cb, spec, shoulder_side=1.0, width=0.024, thick=0.006, tile="prop_b", z_hip=0.93)
    tube_p0 = (+0.115, +0.132, 1.385)
    tube_p1 = (-0.115, +0.132, 0.965)
    cb.add_tube_along_curve(
        [tube_p0, (0.0, +0.136, 1.175), tube_p1],
        [(0.038, 0.038), (0.038, 0.038), (0.038, 0.038)],
        ["spine2", "spine1", "spine"],
        "prop_a",
        n_sides=12,
    )
    # Black plastic end-caps on poster tube
    for ep, etg in ((tube_p0, "spine2"), (tube_p1, "spine")):
        cb.add_rounded_box(ep, (0.041, 0.041, 0.016), (0, -0.50, 0), etg, "bands_metal")

    # Khaki Artist Utility Waist Apron at Left Hip + Rolled "ZINE" Komiks + Pastel Chalk Sticks!
    ax_p = +spec["hip_w"] * 1.14
    cb.add_rounded_box((ax_p, -0.018, 0.845), (0.022, 0.068, 0.072), (0, 0, -0.14), "hips", "prop_b")
    # Rolled ZINE booklet + chalk sticks peeking out top pocket
    cb.add_tube_along_curve([(ax_p + 0.008, -0.032, 0.895), (ax_p + 0.016, -0.042, 0.965)], [(0.016, 0.012), (0.016, 0.012)], "hips", "socks_acc", n_sides=8)
    for dy_ch in (+0.012, +0.028, +0.042):
        cb.add_tube_along_curve([(ax_p + 0.008, dy_ch, 0.900), (ax_p + 0.014, dy_ch, 0.942)], [(0.005, 0.005), (0.005, 0.005)], "hips", "bands_metal", n_sides=5)


KID_BUILDERS: dict[str, Callable[[CharBuilder, dict[str, Any]], None]] = {
    "junjun": build_kid_junjun,
    "kiko": build_kid_kiko,
    "ligaya": build_kid_ligaya,
    "migo": build_kid_migo,
    "toni": build_kid_toni,
    "popoy": build_kid_popoy,
    "inday": build_kid_inday,
    "nene": build_kid_nene,
    "buknoy": build_kid_buknoy,
    "tala": build_kid_tala,
}


# ==============================================================================
# 8. PAINTERLY 1024x1024 VALORANT TEXTURE SYNTHESIZER (ALBEDO + NORMAL + ORM)
# ==============================================================================
def _load_bold_font(px_size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    for candidate in (
        r"C:\Windows\Fonts\impact.ttf",
        r"C:\Windows\Fonts\arialbd.ttf",
        r"C:\Windows\Fonts\segoeuib.ttf",
        r"C:\Windows\Fonts\calibrib.ttf",
    ):
        if os.path.exists(candidate):
            try:
                return ImageFont.truetype(candidate, px_size)
            except Exception:
                pass
    return ImageFont.load_default()


def _fill_tile(
    albedo: Image.Image,
    height_arr: np.ndarray,
    rough_arr: np.ndarray,
    metal_arr: np.ndarray,
    tile_name: str,
    rgb: tuple[int, int, int],
    roughness: float = 0.72,
    metallic: float = 0.0,
    weave_amp: float = 0.0,
    weave_freq: int = 24,
) -> tuple[int, int, int, int]:
    x0, y0, x1, y1 = tile_px_box(tile_name, 1024, pad=10)
    draw = ImageDraw.Draw(albedo)
    draw.rectangle([x0, y0, x1 - 1, y1 - 1], fill=rgb)
    rough_arr[y0:y1, x0:x1] = roughness
    metal_arr[y0:y1, x0:x1] = metallic
    if weave_amp > 0.0:
        yy, xx = np.mgrid[y0:y1, x0:x1]
        wv = np.sin(xx * (2.0 * math.pi / weave_freq)) * np.cos(yy * (2.0 * math.pi / weave_freq))
        height_arr[y0:y1, x0:x1] += weave_amp * wv
    return tile_inner_px(tile_name, 1024)


def _apply_valorant_gradient(albedo_arr: np.ndarray, box: tuple[int, int, int, int], top_mul: float = 1.07, bot_mul: float = 0.86) -> None:
    x0, y0, x1, y1 = box
    h = max(1, y1 - y0)
    grad = np.linspace(top_mul, bot_mul, h, dtype=np.float32)[:, None, None]
    albedo_arr[y0:y1, x0:x1] = np.clip(albedo_arr[y0:y1, x0:x1].astype(np.float32) * grad, 0, 255).astype(np.uint8)


def _height_to_normal_png(height_arr: np.ndarray, strength: float = 2.6) -> Image.Image:
    """Converts scalar bump height array [1024, 1024] into an OpenGL tangent-space Normal Map."""
    dy, dx = np.gradient(height_arr.astype(np.float32))
    nx = -dx * strength
    ny = dy * strength  # OpenGL +Y up (PIL y increases downward, so +dy -> +ny)
    nz = np.ones_like(nx)
    inv_len = 1.0 / np.maximum(1e-6, np.sqrt(nx * nx + ny * ny + nz * nz))
    rgb = np.zeros((height_arr.shape[0], height_arr.shape[1], 3), dtype=np.uint8)
    rgb[..., 0] = np.clip((nx * inv_len * 0.5 + 0.5) * 255.0, 0, 255).astype(np.uint8)
    rgb[..., 1] = np.clip((ny * inv_len * 0.5 + 0.5) * 255.0, 0, 255).astype(np.uint8)
    rgb[..., 2] = np.clip((nz * inv_len * 0.5 + 0.5) * 255.0, 128, 255).astype(np.uint8)
    return Image.fromarray(rgb, mode="RGB")


def _encode_rgb_png(rgb: np.ndarray) -> bytes:
    """Dependency-free PNG encoder (works identically inside Blender 5.2 & Host Python)."""
    h, w, _ = rgb.shape
    raw_rows = b"".join(b"\x00" + rgb[y].tobytes() for y in range(h))
    comp = zlib.compress(raw_rows, level=6)

    def chunk(tag: bytes, data: bytes) -> bytes:
        crc = zlib.crc32(tag + data) & 0xFFFFFFFF
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", crc)

    ihdr = struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", comp) + chunk(b"IEND", b"")


def _bake_vertex_ao_into_pngs(
    cb: CharBuilder,
    base_orm_path: Path,
    base_alb_path: Path,
) -> tuple[bytes, bytes]:
    """
    Runs inside Blender: loads the pre-synthesized 1024x1024 base ORM & Albedo PNGs via bpy.data.images,
    bakes smooth 3D anatomical vertex AO onto the UV atlas using pure numpy, and encodes final PNG bytes!
    """
    size = 1024
    verts_np = np.array(cb.verts, dtype=np.float32)
    z = verts_np[:, 2]
    x = verts_np[:, 0]
    y = verts_np[:, 1]

    ao_v = np.ones(len(cb.verts), dtype=np.float32) * 0.96
    chin_occ = np.exp(-(((z - 1.335) / 0.032) ** 2)) * np.clip(-y * 10.0, 0.0, 1.0)
    ao_v -= 0.24 * chin_occ
    thigh_occ = np.exp(-((x / 0.072) ** 2)) * ((z > 0.38) & (z < 0.78)).astype(np.float32)
    ao_v -= 0.20 * thigh_occ
    armpit_occ = np.exp(-(((np.abs(x) - 0.15) / 0.038) ** 2)) * np.exp(-(((z - 1.24) / 0.045) ** 2))
    ao_v -= 0.18 * armpit_occ
    ao_v -= 0.12 * np.exp(-z / 0.18)
    ao_v = np.clip(ao_v, 0.54, 1.0)

    grid_res = 128
    ao_sum = np.zeros((grid_res, grid_res), dtype=np.float32)
    ao_cnt = np.zeros((grid_res, grid_res), dtype=np.float32)
    for (i0, i1, i2), (uv0, uv1, uv2) in zip(cb.tris, cb.tri_uvs):
        val = float(ao_v[i0] + ao_v[i1] + ao_v[i2]) / 3.0
        for u, v in (uv0, uv1, uv2, ((uv0[0] + uv1[0] + uv2[0]) / 3.0, (uv0[1] + uv1[1] + uv2[1]) / 3.0)):
            gx = min(grid_res - 1, max(0, int(u * grid_res)))
            gy = min(grid_res - 1, max(0, int((1.0 - v) * grid_res)))
            ao_sum[gy, gx] += val
            ao_cnt[gy, gx] += 1.0

    ao_grid = np.where(ao_cnt > 0, ao_sum / np.maximum(1.0, ao_cnt), 0.95)
    # Separable 3x3 box blur x2 + Kronecker upsample to 1024x1024 (pure numpy, <5ms!)
    for _ in range(2):
        pad_g = np.pad(ao_grid, 1, mode="edge")
        ao_grid = (
            pad_g[:-2, :-2] + pad_g[:-2, 1:-1] + pad_g[:-2, 2:]
            + pad_g[1:-1, :-2] + pad_g[1:-1, 1:-1] + pad_g[1:-1, 2:]
            + pad_g[2:, :-2] + pad_g[2:, 1:-1] + pad_g[2:, 2:]
        ) / 9.0
    ao_map = np.repeat(np.repeat(ao_grid, size // grid_res, axis=0), size // grid_res, axis=1)

    def read_bpy_rgb(p: Path) -> np.ndarray:
        im = bpy.data.images.load(str(p))
        im.colorspace_settings.name = "Non-Color"
        px = np.empty(size * size * 4, dtype=np.float32)
        im.pixels.foreach_get(px)
        bpy.data.images.remove(im)
        # Blender image pixels are bottom-to-top; flip vertically to standard top-to-bottom PNG row order!
        rgba = px.reshape((size, size, 4))[::-1, :, :3]
        return np.clip(rgba * 255.0 + 0.5, 0, 255).astype(np.uint8)

    orm_rgb = read_bpy_rgb(base_orm_path)
    alb_rgb = read_bpy_rgb(base_alb_path)

    orm_rgb[..., 0] = np.clip(ao_map * 255.0, 0, 255).astype(np.uint8)
    ao_factor = (0.68 + 0.32 * ao_map)[:, :, None]
    alb_shaded = np.clip(alb_rgb.astype(np.float32) * ao_factor, 0, 255).astype(np.uint8)
    return _encode_rgb_png(orm_rgb), _encode_rgb_png(alb_shaded)


def synthesize_base_valorant_textures_pil(kid_id: str, spec: dict[str, Any], out_dir: Path) -> None:
    """
    Runs in Host Python (with PIL): synthesizes crisp 1024x1024 base ORM, Albedo, and Tangent Normal PNGs.
    """
    S = 1024
    skin = spec["skin"]
    skin_sh = spec["skin_shadow"]
    albedo = Image.new("RGB", (S, S), skin)
    height = np.zeros((S, S), dtype=np.float32)
    rough = np.full((S, S), 0.68, dtype=np.float32)
    metal = np.zeros((S, S), dtype=np.float32)

    # 1. Base Tile Fills + Material Micro-Normal Weaves
    f_box = _fill_tile(albedo, height, rough, metal, "face", skin, roughness=0.58)
    e_box = _fill_tile(albedo, height, rough, metal, "eyes", (222, 220, 224), roughness=0.20)
    _fill_tile(albedo, height, rough, metal, "brows_lash", (22, 16, 16), roughness=0.52)
    h_box = _fill_tile(albedo, height, rough, metal, "hair", spec["hair"], roughness=0.62)
    tf_box = _fill_tile(albedo, height, rough, metal, "torso_front", spec["shirt"], roughness=0.76, weave_amp=0.14, weave_freq=10)
    tb_box = _fill_tile(albedo, height, rough, metal, "torso_back", spec["shirt"], roughness=0.76, weave_amp=0.14, weave_freq=10)
    tu_box = _fill_tile(albedo, height, rough, metal, "torso_under", spec["under"], roughness=0.74, weave_amp=0.12, weave_freq=12)
    bt_box = _fill_tile(albedo, height, rough, metal, "bottoms", spec["bottom"], roughness=0.78, weave_amp=0.18, weave_freq=9)
    sk_box = _fill_tile(albedo, height, rough, metal, "skin", skin, roughness=0.58)
    sa_box = _fill_tile(albedo, height, rough, metal, "socks_acc", spec["shirt_trim"], roughness=0.70, weave_amp=0.10, weave_freq=8)
    bm_box = _fill_tile(albedo, height, rough, metal, "bands_metal", (208, 168, 78), roughness=0.36, metallic=0.55)
    pa_box = _fill_tile(albedo, height, rough, metal, "prop_a", spec["prop_a"], roughness=0.64, weave_amp=0.15, weave_freq=12)
    pb_box = _fill_tile(albedo, height, rough, metal, "prop_b", spec["prop_b"], roughness=0.62, weave_amp=0.12, weave_freq=14)
    fw_box = _fill_tile(albedo, height, rough, metal, "footwear", spec["footwear"], roughness=0.54)
    sl_box = _fill_tile(albedo, height, rough, metal, "sole_alt", spec["sole"], roughness=0.68)

    # 2. Planar Valorant Face Shading & Features (Aligned 1:1 with z_face_min=1.432..1.650!)
    # Exact V -> PIL Y mapping: eyes z=1.519 -> 0.601*fh, nose tip z=1.486 -> 0.752*fh, mouth z=1.463 -> 0.858*fh!
    fx0, fy0, fx1, fy1 = f_box
    fw, fh = fx1 - fx0, fy1 - fy0
    fcx = fx0 + fw // 2

    face_soft = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    sdraw = ImageDraw.Draw(face_soft)
    blush_rgba = (min(255, int(skin[0] * 1.10)), int(skin[1] * 0.66), int(skin[2] * 0.60), 108)
    sh_rgba = (max(12, int(skin_sh[0] * 0.88)), max(10, int(skin_sh[1] * 0.84)), max(8, int(skin_sh[2] * 0.82)), 135)
    hi_rgba = (min(235, int(skin[0] * 1.15)), min(225, int(skin[1] * 1.13)), min(215, int(skin[2] * 1.10)), 105)

    for s_c in (-1, 1):
        # Warm sunlit Valorant cheek blush across zygomatic planes
        cx_bl = fcx + s_c * int(fw * 0.142)
        cy_bl = fy0 + int(fh * 0.688)
        sdraw.ellipse([cx_bl - 26, cy_bl - 14, cx_bl + 26, cy_bl + 14], fill=blush_rgba)
        # Concave orbital eye-socket shading around z = 1.519
        ox = fcx + s_c * int(fw * 0.102)
        oy = fy0 + int(fh * 0.601)
        sdraw.ellipse([ox - 20, oy - 12, ox + 20, oy + 12], fill=sh_rgba)
        # Crisp planar nasal-bridge side planes so the nose reads unmistakably from front & 3/4!
        sdraw.polygon([
            (fcx + s_c * 5, fy0 + int(fh * 0.585)),
            (fcx + s_c * 13, fy0 + int(fh * 0.595)),
            (fcx + s_c * 16, fy0 + int(fh * 0.748)),
            (fcx + s_c * 7, fy0 + int(fh * 0.752)),
        ], fill=sh_rgba)

    ny_top, ny_tip = fy0 + int(fh * 0.585), fy0 + int(fh * 0.752)
    sdraw.polygon([(fcx - 4, ny_top), (fcx + 4, ny_top), (fcx + 6, ny_tip), (fcx - 6, ny_tip)], fill=hi_rgba)
    # Warm terracotta cast shadow under the 3D nose tip & alar base!
    sdraw.polygon([(fcx - 14, ny_tip + 1), (fcx + 14, ny_tip + 1), (fcx + 9, ny_tip + 12), (fcx - 9, ny_tip + 12)], fill=sh_rgba)
    face_soft = face_soft.filter(ImageFilter.GaussianBlur(radius=7))
    albedo = Image.alpha_composite(albedo.convert("RGBA"), face_soft).convert("RGB")
    draw = ImageDraw.Draw(albedo)

    # Crisp Valorant nostril accents & nose-tip plane break right at ny_tip!
    nostril_col = (max(18, int(skin_sh[0] * 0.58)), max(12, int(skin_sh[1] * 0.45)), max(10, int(skin_sh[2] * 0.42)))
    draw.line([(fcx - 9, ny_tip + 4), (fcx - 4, ny_tip + 6)], fill=nostril_col, width=2)
    draw.line([(fcx + 4, ny_tip + 6), (fcx + 9, ny_tip + 4)], fill=nostril_col, width=2)

    # Youthful Valorant two-tone lips & confident street-kid smirk aligned 1:1 with z = 1.463 (0.858*fh)!
    lip_upper = (max(24, int(skin_sh[0] * 0.82)), max(16, int(skin_sh[1] * 0.56)), max(16, int(skin_sh[2] * 0.54)))
    lip_lower = (min(225, int(skin[0] * 0.96)), int(skin[1] * 0.64), int(skin[2] * 0.62))
    lip_line = (max(18, int(skin_sh[0] * 0.52)), max(12, int(skin_sh[1] * 0.36)), max(12, int(skin_sh[2] * 0.34)))
    ly = fy0 + int(fh * 0.858)
    draw.polygon([(fcx - 17, ly), (fcx - 5, ly - 4), (fcx, ly - 3), (fcx + 5, ly - 4), (fcx + 17, ly - 1), (fcx, ly + 2)], fill=lip_upper)
    draw.ellipse([fcx - 13, ly + 1, fcx + 13, ly + 8], fill=lip_lower)
    draw.line([(fcx - 18, ly), (fcx, ly + 1), (fcx + 19, ly - 2)], fill=lip_line, width=2)

    if kid_id == "buknoy":
        # Subtle mechanic grease smudges on right cheek & nose bridge!
        draw.line([(fcx - 36, fy0 + int(fh * 0.68)), (fcx - 20, fy0 + int(fh * 0.70))], fill=(68, 56, 48), width=4)
        draw.line([(fcx - 33, fy0 + int(fh * 0.72)), (fcx - 22, fy0 + int(fh * 0.73))], fill=(82, 68, 58), width=3)

    # 3. High-Texel Valorant Almond Eyes (Iris, Warm Lower Bounce, Pupil & Dual Catchlights!)
    ex0, ey0, ex1, ey1 = e_box
    ew_px, eh_px = ex1 - ex0, ey1 - ey0
    ecx, ecy = ex0 + ew_px // 2, ey0 + eh_px // 2
    # Upper eyelid cast shadow across sclera
    draw.rectangle([ex0, ey0, ex1, ey0 + int(eh_px * 0.25)], fill=(204, 200, 210))
    ir_r = int(min(ew_px, eh_px) * 0.36)
    eye_col = spec["eye"]
    eye_rim = (max(10, eye_col[0] - 28), max(8, eye_col[1] - 18), max(8, eye_col[2] - 12))
    eye_warm = (min(255, eye_col[0] + 78), min(255, eye_col[1] + 48), min(255, eye_col[2] + 24))
    draw.ellipse([ecx - ir_r, ecy - ir_r, ecx + ir_r, ecy + ir_r], fill=eye_rim)
    draw.ellipse([ecx - ir_r + 5, ecy - ir_r + 5, ecx + ir_r - 5, ecy + ir_r - 5], fill=eye_col)
    # Warm amber crescent in lower half of iris
    draw.chord([ecx - ir_r + 8, ecy - ir_r + 10, ecx + ir_r - 8, ecy + ir_r - 6], start=15, end=165, fill=eye_warm)
    # Crisp dark pupil
    pu_r = int(ir_r * 0.48)
    draw.ellipse([ecx - pu_r, ecy - pu_r, ecx + pu_r, ecy + pu_r], fill=(12, 10, 12))
    # Signature Valorant primary & secondary specular catchlights!
    draw.ellipse([ecx - ir_r // 2 - 4, ecy - ir_r // 2 - 4, ecx - ir_r // 2 + 10, ecy - ir_r // 2 + 10], fill=(255, 255, 255))
    draw.ellipse([ecx + ir_r // 3, ecy + ir_r // 4, ecx + ir_r // 3 + 6, ecy + ir_r // 4 + 6], fill=(245, 248, 255))

    # 4. Stylized Valorant Satin Hair Crown Band + Subtle Strand Normals
    hx0, hy0, hx1, hy1 = h_box
    hh = hy1 - hy0
    hair_soft = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    hdraw = ImageDraw.Draw(hair_soft)
    band_y = hy0 + int(hh * 0.36)
    hdraw.rectangle([hx0, band_y, hx1, band_y + int(hh * 0.14)], fill=(spec["hair_hi"][0], spec["hair_hi"][1], spec["hair_hi"][2], 145))
    hair_soft = hair_soft.filter(ImageFilter.GaussianBlur(radius=6))
    albedo = Image.alpha_composite(albedo.convert("RGBA"), hair_soft).convert("RGB")
    draw = ImageDraw.Draw(albedo)
    yy_h, xx_h = np.mgrid[hy0:hy1, hx0:hx1]
    height[hy0:hy1, hx0:hx1] += 0.18 * np.sin(xx_h * (2.0 * math.pi / 8.0))

    # 5. Bespoke Per-Kid 90s Filipino Streetwear & Prop Graphics!
    tx0, ty0, tx1, ty1 = tf_box
    tw, th_px = tx1 - tx0, ty1 - ty0
    bx0, by0, bx1, by1 = tb_box
    pax0, pay0, pax1, pay1 = pa_box
    pbx0, pby0, pbx1, pby1 = pb_box
    bt_x0, bt_y0, bt_x1, bt_y1 = bt_box
    fw_x0, fw_y0, fw_x1, fw_y1 = fw_box

    if kid_id == "junjun":
        # Sando fine vertical cotton ribs (both normal bump AND warm taupe rib albedo shading so it never looks flat!)
        yy_s, xx_s = np.mgrid[ty0:ty1, tx0:tx1]
        height[ty0:ty1, tx0:tx1] += 0.26 * np.sin(xx_s * (2.0 * math.pi / 6.0))
        for x_rib in range(tx0 + 3, tx1 - 2, 6):
            draw.line([(x_rib, ty0), (x_rib, ty1)], fill=(158, 149, 132), width=1)
        for x_rib in range(bx0 + 3, bx1 - 2, 6):
            draw.line([(x_rib, by0), (x_rib, by1)], fill=(158, 149, 132), width=1)
        # Stitched cotton sando collar, armhole & waist hem rib trim!
        draw.rectangle([tx0, ty0, tx1, ty0 + 14], fill=(150, 140, 124))
        draw.rectangle([bx0, by0, bx1, by0 + 14], fill=(150, 140, 124))
        draw.rectangle([tx0, ty1 - 14, tx1, ty1], fill=(148, 138, 122))
        draw.rectangle([bx0, by1 - 14, bx1, by1], fill=(148, 138, 122))
        # Twin warm cream basketball stripes on navy shorts!
        for u_st in (0.22, 0.27, 0.73, 0.78):
            sx_p = bt_x0 + int((bt_x1 - bt_x0) * u_st)
            draw.rectangle([sx_p - 4, bt_y0, sx_p + 4, bt_y1], fill=(178, 174, 164))
        # KUMOT BLANKET CAPE: Traditional woven Filipino gold/cream geometric border along bottom!
        b_top = pay1 - int((pay1 - pay0) * 0.20)
        draw.rectangle([pax0, b_top, pax1, b_top + 10], fill=(182, 176, 164))
        draw.rectangle([pax0, b_top + 12, pax1, b_top + 28], fill=(204, 168, 68))
        for dx_d in range(pax0 + 8, pax1 - 8, 20):
            draw.polygon([(dx_d, b_top + 13), (dx_d + 8, b_top + 20), (dx_d, b_top + 27), (dx_d - 8, b_top + 20)], fill=(186, 30, 28))
        draw.rectangle([pax0, b_top + 30, pax1, b_top + 38], fill=(182, 176, 164))
        # CORRUGATED CARDBOARD DOMINO MASK (`prop_b`, mapped u=0.06..0.94, v=0.08..0.92):
        # Vertical kraft corrugated fluting lines + hand-cut dark eyehole outlines + THICK BLACK CRAYON HEROIC EYEBROWS (/ \)!
        yy_m, xx_m = np.mgrid[pby0:pby1, pbx0:pbx1]
        height[pby0:pby1, pbx0:pbx1] += 0.22 * np.sin(xx_m * (2.0 * math.pi / 7.0))
        rough[pby0:pby1, pbx0:pbx1] = 0.78
        pw, ph = pbx1 - pbx0, pby1 - pby0
        for x_fl in range(pbx0 + 6, pbx1 - 6, 7):
            draw.line([(x_fl, pby0 + 4), (x_fl, pby1 - 4)], fill=(142, 98, 56), width=1)
        # Outer hand-cut kraft mask border
        draw.rectangle([pbx0 + int(pw * 0.05), pby0 + int(ph * 0.07), pbx1 - int(pw * 0.05), pby1 - int(ph * 0.07)], outline=(34, 24, 18), width=5)
        # Hand-drawn black marker/crayon outline around BOTH almond eyeholes (uf=0.125..0.375 & 0.625..0.875, vf=0.143..0.571 -> py=0.44..0.80*ph)!
        draw.ellipse([pbx0 + int(pw * 0.145), pby0 + int(ph * 0.41), pbx0 + int(pw * 0.415), pby0 + int(ph * 0.82)], outline=(24, 18, 16), width=7)
        draw.ellipse([pbx0 + int(pw * 0.585), pby0 + int(ph * 0.41), pbx0 + int(pw * 0.855), pby0 + int(ph * 0.82)], outline=(24, 18, 16), width=7)
        # BOLD HAND-DRAWN BLACK CRAYON HEROIC ANGLED EYEBROWS (/ \) on solid upper brow band (py=0.18..0.36*ph)!
        draw.line([(pbx0 + int(pw * 0.13), pby0 + int(ph * 0.20)), (pbx0 + int(pw * 0.43), pby0 + int(ph * 0.35))], fill=(18, 15, 15), width=14)
        draw.line([(pbx0 + int(pw * 0.87), pby0 + int(ph * 0.20)), (pbx0 + int(pw * 0.57), pby0 + int(ph * 0.35))], fill=(18, 15, 15), width=14)
        draw.line([(pbx0 + int(pw * 0.14), pby0 + int(ph * 0.17)), (pbx0 + int(pw * 0.41), pby0 + int(ph * 0.32))], fill=(32, 26, 24), width=5)
        draw.line([(pbx0 + int(pw * 0.86), pby0 + int(ph * 0.17)), (pbx0 + int(pw * 0.59), pby0 + int(ph * 0.32))], fill=(32, 26, 24), width=5)
        # Pulseras rainbow stripes (`bands_metal`), tan/dark-rimmed lastiko rubberband (top half of `socks_acc`, v=0.55..0.95 -> py=sa_y0..mid), & shin band-aid (bottom half)!
        for i_c, col_p in enumerate([(198, 42, 42), (206, 168, 38), (34, 142, 76), (38, 94, 188)]):
            y_band = bm_box[1] + i_c * ((bm_box[3] - bm_box[1]) // 4)
            draw.rectangle([bm_box[0], y_band, bm_box[2], y_band + 26], fill=col_p)
        metal[bm_box[1]:bm_box[3], bm_box[0]:bm_box[2]] = 0.0
        sa_mid = (sa_box[1] + sa_box[3]) // 2
        # Top half (`v=0.50..1.00`): stretched tan/amber lastiko rubberband w/ dark rubber border!
        draw.rectangle([sa_box[0], sa_box[1], sa_box[2], sa_mid], fill=(166, 118, 62), outline=(36, 24, 18), width=6)
        # Bottom half (`v=0.00..0.50`): warm tan shin band-aid!
        draw.rectangle([sa_box[0], sa_mid, sa_box[2], sa_box[3]], fill=(186, 142, 104))

    elif kid_id == "kiko":
        # Sun-faded mustard V-neck sprinter tank with dark charcoal athletic side panels & V-neck collar binding!
        draw.rectangle([tx0, ty0, tx0 + int(tw * 0.19), ty1], fill=(38, 42, 48))
        draw.rectangle([tx1 - int(tw * 0.19), ty0, tx1, ty1], fill=(38, 42, 48))
        draw.rectangle([bx0, by0, bx0 + int((bx1 - bx0) * 0.19), by1], fill=(38, 42, 48))
        draw.rectangle([bx1 - int((bx1 - bx0) * 0.19), by0, bx1, by1], fill=(38, 42, 48))
        # Charcoal V-neck & armhole athletic trim along top of tank (`ty0..ty0+22`)
        draw.rectangle([tx0, ty0, tx1, ty0 + 20], fill=(38, 42, 48))
        draw.rectangle([bx0, by0, bx1, by0 + 18], fill=(38, 42, 48))
        # HAND-TSINELAS SOLE (`prop_a`): Loft v=0.70..0.96 (PIL top 33%) = shaded cream-white EVA footbed (0 blowout!);
        # Loft v=0.04..0.68 (PIL bottom 67%) = bright vibrant teal outer sidewall rim & bottom outsole!
        ph_pa = pay1 - pay0
        footbed_split = pay0 + int(ph_pa * 0.34)
        draw.rectangle([pax0, pay0, pax1, footbed_split], fill=(182, 180, 172))
        for x_eva in range(pax0 + 6, pax1 - 6, 12):
            draw.line([(x_eva, pay0 + 2), (x_eva, footbed_split - 2)], fill=(166, 164, 156), width=1)
        draw.rectangle([pax0, footbed_split, pax1, pay1], fill=(12, 168, 160))
        draw.line([(pax0, footbed_split), (pax1, footbed_split)], fill=(8, 118, 112), width=5)
        yy_e, xx_e = np.mgrid[pay0:pay1, pax0:pax1]
        height[pay0:pay1, pax0:pax1] += 0.32 * np.sin(xx_e * 0.8) * np.sin(yy_e * 0.8)
        rough[pay0:pay1, pax0:pax1] = 0.50
        # HAND-TSINELAS Y-STRAP (`footwear`): Bright glossy teal molded rubber!
        draw.rectangle([fw_x0, fw_y0, fw_x1, fw_y1], fill=(10, 178, 168))
        rough[fw_y0:fw_y1, fw_x0:fw_x1] = 0.30
        # Woven cream bimpo headband stripes
        draw.rectangle([pbx0, pby0 + 18, pbx1, pby0 + 28], fill=(168, 48, 42))
        draw.rectangle([pbx0, pby1 - 28, pbx1, pby1 - 18], fill=(42, 88, 146))

    elif kid_id == "ligaya":
        # Sunflower floral 90s bestida pattern across front & back!
        rng = np.random.default_rng(103)
        for box_fl in (tf_box, tb_box):
            for _ in range(22):
                fx_s = int(rng.integers(box_fl[0] + 18, box_fl[2] - 18))
                fy_s = int(rng.integers(box_fl[1] + 18, box_fl[3] - 18))
                draw.ellipse([fx_s - 12, fy_s - 6, fx_s + 4, fy_s + 10], fill=(52, 128, 66))
                for a_pet in range(0, 360, 45):
                    px_p = fx_s + int(9 * math.cos(math.radians(a_pet)))
                    py_p = fy_s + int(9 * math.sin(math.radians(a_pet)))
                    draw.ellipse([px_p - 5, py_p - 5, px_p + 5, py_p + 5], fill=(218, 182, 46))
                draw.ellipse([fx_s - 5, fy_s - 5, fx_s + 5, fy_s + 5], fill=(84, 46, 24))
        # Teks illustrated card stack + tan lastiko band on `prop_b`!
        draw.rectangle([pbx0 + 12, pby0 + 12, pbx1 - 12, pby1 - 12], fill=(186, 176, 150), outline=(38, 62, 136), width=6)
        draw.rectangle([pbx0, (pby0 + pby1) // 2 - 8, pbx1, (pby0 + pby1) // 2 + 8], fill=(176, 132, 78))

    elif kid_id == "migo":
        # 90s Pastel Memphis Graphic Tee (Triangles, Waves & Neon Accents!)
        cx_m, cy_m = tx0 + tw // 2, ty0 + int(th_px * 0.40)
        draw.polygon([(cx_m, cy_m - 48), (cx_m + 52, cy_m + 38), (cx_m - 52, cy_m + 38)], fill=(208, 48, 134))
        draw.polygon([(cx_m + 18, cy_m - 26), (cx_m + 62, cy_m + 46), (cx_m - 22, cy_m + 46)], outline=(214, 184, 52), width=6)
        draw.ellipse([cx_m - 36, cy_m - 36, cx_m - 8, cy_m - 8], fill=(148, 106, 206))
        # Iridescent Holographic Gradient on `prop_a` (fanny pack)!
        al_np = np.array(albedo)
        pw_h, ph_h = pax1 - pax0, pay1 - pay0
        uu = np.linspace(0.0, 1.0, pw_h)[None, :]
        vv = np.linspace(0.0, 1.0, ph_h)[:, None]
        phase = (uu * 2.2 + vv * 1.6) * math.pi * 2.0
        al_np[pay0:pay1, pax0:pax1, 0] = np.clip(150 + 42 * np.sin(phase), 0, 195).astype(np.uint8)
        al_np[pay0:pay1, pax0:pax1, 1] = np.clip(146 + 42 * np.sin(phase + 2.1), 0, 192).astype(np.uint8)
        al_np[pay0:pay1, pax0:pax1, 2] = np.clip(178 + 20 * np.cos(phase), 0, 198).astype(np.uint8)
        albedo = Image.fromarray(al_np, mode="RGB")
        draw = ImageDraw.Draw(albedo)
        rough[pay0:pay1, pax0:pax1] = 0.26
        rough[pby0:pby1, pbx0:pbx1] = 0.20  # Glossy hot-magenta sun visor!
        # Stitched colorful patches on puruntong denim shorts!
        draw.rectangle([bt_x0 + 34, bt_y0 + 70, bt_x0 + 82, bt_y0 + 112], fill=(206, 172, 54), outline=(202, 56, 118), width=3)
        draw.rectangle([bt_x1 - 88, bt_y0 + 120, bt_x1 - 38, bt_y0 + 162], fill=(202, 76, 136), outline=(182, 176, 166), width=3)

    elif kid_id == "toni":
        # #08 BARANGAY LIGA Crimson/Gold Varsity Mesh Jersey!
        for jb in (tf_box, tb_box):
            # Gold & cream collar/armhole rib trim
            draw.rectangle([jb[0], jb[1], jb[2], jb[1] + 18], fill=(204, 158, 42))
            draw.rectangle([jb[0], jb[1] + 18, jb[2], jb[1] + 25], fill=(182, 174, 160))
            yy_j, xx_j = np.mgrid[jb[1]:jb[3], jb[0]:jb[2]]
            height[jb[1]:jb[3], jb[0]:jb[2]] -= 0.28 * ((xx_j % 6 < 2) & (yy_j % 6 < 2)).astype(np.float32)

        fnt_sm = _load_bold_font(26)
        fnt_num = _load_bold_font(86)
        draw.text((tx0 + tw // 2, ty0 + int(th_px * 0.28)), "BARANGAY LIGA", fill=(208, 164, 46), anchor="mm", font=fnt_sm, stroke_width=2, stroke_fill=(184, 176, 162))
        draw.text((tx0 + tw // 2, ty0 + int(th_px * 0.52)), "#08", fill=(208, 164, 46), anchor="mm", font=fnt_num, stroke_width=3, stroke_fill=(184, 176, 162))
        bw_t, bh_t = bx1 - bx0, by1 - by0
        draw.text((bx0 + bw_t // 2, by0 + int(bh_t * 0.28)), "BARANGAY LIGA", fill=(208, 164, 46), anchor="mm", font=fnt_sm, stroke_width=2, stroke_fill=(184, 176, 162))
        draw.text((bx0 + bw_t // 2, by0 + int(bh_t * 0.54)), "08", fill=(208, 164, 46), anchor="mm", font=fnt_num, stroke_width=3, stroke_fill=(184, 176, 162))
        # Varsity striped crew socks (`socks_acc`)
        draw.rectangle([sa_box[0], sa_box[1], sa_box[2], sa_box[3]], fill=(178, 172, 160))
        draw.rectangle([sa_box[0], sa_box[1] + 18, sa_box[2], sa_box[1] + 28], fill=(182, 30, 38))
        draw.rectangle([sa_box[0], sa_box[1] + 34, sa_box[2], sa_box[1] + 44], fill=(34, 70, 152))
        # Clean 90s Geometric Chevron High-Top Sneaker Panels (ZERO brand swoosh, warm cream panel!)
        draw.rectangle([fw_x0, fw_y0 + 45, fw_x1, fw_y1 - 35], fill=(180, 174, 162))
        mid_fw = (fw_x0 + fw_x1) // 2
        mid_fy = (fw_y0 + fw_y1) // 2
        draw.polygon([(mid_fw - 55, mid_fy - 18), (mid_fw - 10, mid_fy + 18), (mid_fw + 55, mid_fy - 22), (mid_fw + 38, mid_fy - 34), (mid_fw - 10, mid_fy + 2), (mid_fw - 42, mid_fy - 30)], fill=(24, 26, 30))

    elif kid_id == "popoy":
        # Open Vertical-Striped Sky-Blue / Warm Cream / Rust-Orange Polo!
        for pb_s in (tf_box, tb_box):
            x_cur = pb_s[0]
            stripe_cols = [(102, 152, 188), (180, 172, 156), (186, 90, 48), (180, 172, 156)]
            idx_st = 0
            while x_cur < pb_s[2]:
                draw.rectangle([x_cur, pb_s[1], min(pb_s[2], x_cur + 16), pb_s[3]], fill=stripe_cols[idx_st % 4])
                x_cur += 16
                idx_st += 1
        # Corrugated Cardboard KALASAG Forearm Shield w/ Fluted Ridges + Hand-Drawn Star Emblem!
        yy_k, xx_k = np.mgrid[pay0:pay1, pax0:pax1]
        height[pay0:pay1, pax0:pax1] += 0.48 * np.sin(yy_k * (2.0 * math.pi / 8.0))
        kcx, kcy = (pax0 + pax1) // 2, (pay0 + pay1) // 2
        draw.rectangle([pax0 + 16, pay0 + 16, pax1 - 16, pay1 - 16], outline=(38, 30, 24), width=5)
        draw.polygon([(kcx, kcy - 44), (kcx + 38, kcy), (kcx, kcy + 44), (kcx - 38, kcy)], outline=(168, 38, 32), width=6)
        # Glossy tan packing tape (`socks_acc`) & colorful mango/ube/melon ice-candy (`bands_metal`)
        draw.rectangle([sa_box[0], sa_box[1], sa_box[2], sa_box[3]], fill=(188, 154, 100))
        rough[sa_box[1]:sa_box[3], sa_box[0]:sa_box[2]] = 0.26
        ic_w = (bm_box[2] - bm_box[0]) // 3
        for idx_ic, col_ic in enumerate([(212, 168, 40), (132, 64, 162), (206, 98, 118)]):
            draw.rectangle([bm_box[0] + idx_ic * ic_w, bm_box[1], bm_box[0] + (idx_ic + 1) * ic_w, bm_box[3]], fill=col_ic)
        metal[bm_box[1]:bm_box[3], bm_box[0]:bm_box[2]] = 0.0

    elif kid_id == "inday":
        # Warm Cream-Ecru #95 Public-School PE Ringer Tee w/ Golden-Amber Collar & Sleeve Cuff Rings + Dark Navy Stencil!
        draw.rectangle([tx0, ty0, tx1, ty0 + 24], fill=(198, 146, 32))
        draw.rectangle([bx0, by0, bx1, by0 + 24], fill=(198, 146, 32))
        fnt_pe = _load_bold_font(78)
        draw.text((tx0 + tw // 2, ty0 + int(th_px * 0.44)), "95", fill=(22, 32, 64), anchor="mm", font=fnt_pe, stroke_width=2, stroke_fill=(198, 146, 32))
        for u_tr in (0.23, 0.28, 0.72, 0.77):
            sx_t = bt_x0 + int((bt_x1 - bt_x0) * u_tr)
            draw.rectangle([sx_t - 3, bt_y0, sx_t + 3, bt_y1], fill=(176, 174, 166))
        # Colorful crisscrossed lastiko rubberband stripes (`prop_b`)!
        ph_pb = pby1 - pby0
        for i_l, col_l in enumerate([(204, 54, 118), (36, 168, 182), (72, 176, 68), (204, 162, 36)]):
            y_l = pby0 + i_l * (ph_pb // 4)
            draw.rectangle([pbx0, y_l, pbx1, y_l + ph_pb // 4], fill=col_l)
        # Shaded cream PE canvas shoes w/ red side chevron + warm cream socks
        draw.rectangle([sa_box[0], sa_box[1], sa_box[2], sa_box[3]], fill=(176, 170, 158))
        draw.polygon([(fw_x0 + 60, fw_y0 + 90), (fw_x0 + 115, fw_y0 + 50), (fw_x0 + 170, fw_y0 + 90), (fw_x0 + 150, fw_y0 + 90), (fw_x0 + 115, fw_y0 + 66), (fw_x0 + 80, fw_y0 + 90)], fill=(188, 34, 36))
        draw.rectangle([bm_box[0], bm_box[1], bm_box[2], bm_box[3]], fill=(26, 26, 30))
        metal[bm_box[1]:bm_box[3], bm_box[0]:bm_box[2]] = 0.0

    elif kid_id == "nene":
        # Mustard & Shaded-Cream Horizontal-Striped Tee (`torso_under`) + Gold Topstitched Indigo Denim Jumper!
        for y_st in range(tu_box[1], tu_box[3], 24):
            draw.rectangle([tu_box[0], y_st, tu_box[2], min(tu_box[3], y_st + 12)], fill=(182, 172, 152))
        draw.rectangle([tx0 + int(tw * 0.24), ty0 + int(th_px * 0.30), tx1 - int(tw * 0.24), ty0 + int(th_px * 0.62)], outline=(198, 150, 52), width=4)
        # Asymmetric Polka-Dot Crew Socks! Right (`socks_acc`): Sky-blue w/ orange dots; Left (`sole_alt`): Warm cream w/ orange stripe & dots!
        draw.rectangle([sa_box[0], sa_box[1], sa_box[2], sa_box[3]], fill=(68, 138, 198))
        draw.rectangle([sl_box[0], sl_box[1], sl_box[2], sl_box[3]], fill=(180, 170, 150))
        draw.rectangle([sl_box[0], sl_box[1] + 14, sl_box[2], sl_box[1] + 28], fill=(206, 102, 38))
        for bx_polka, dot_col in ((sa_box, (208, 108, 36)), (sl_box, (58, 132, 192))):
            for px_d in range(bx_polka[0] + 18, bx_polka[2] - 14, 28):
                for py_d in range(bx_polka[1] + 36, bx_polka[3] - 12, 26):
                    draw.ellipse([px_d - 6, py_d - 6, px_d + 6, py_d + 6], fill=dot_col)
        # Top half of `prop_b` (`prop_b_top`): vibrant 90s hot-magenta butterfly hairclips!
        # Bottom half of `prop_b` (`prop_b_bot`): warm tan Band-Aid plaster w/ lighter gauze center pad!
        mid_pb = (pby0 + pby1) // 2
        draw.rectangle([pbx0, pby0, pbx1, mid_pb], fill=(202, 50, 132))
        draw.rectangle([pbx0, mid_pb, pbx1, pby1], fill=(188, 144, 108))
        draw.rectangle([pbx0 + 55, mid_pb + 14, pbx1 - 55, pby1 - 14], fill=(198, 172, 146))

    elif kid_id == "buknoy":
        # Rust-Orange, Espresso & Warm-Tan Tartan Plaid Sleeveless Flannel Vest!
        for fl_b in (tf_box, tb_box):
            for x_p in range(fl_b[0], fl_b[2], 36):
                draw.rectangle([x_p, fl_b[1], min(fl_b[2], x_p + 14), fl_b[3]], fill=(58, 32, 26))
                draw.line([(x_p + 22, fl_b[1]), (x_p + 22, fl_b[3])], fill=(182, 162, 130), width=3)
            for y_p in range(fl_b[1], fl_b[3], 36):
                draw.rectangle([fl_b[0], y_p, fl_b[2], min(fl_b[3], y_p + 10)], fill=(70, 38, 30))
                draw.line([(fl_b[0], y_p + 20), (fl_b[2], y_p + 20)], fill=(182, 162, 130), width=2)
        # Woven Tan Canvas Mechanic Tool Belt & Side Pouches (`socks_acc`, per buknoy_concept.png!)
        draw.rectangle([sa_box[0], sa_box[1], sa_box[2], sa_box[3]], fill=(158, 122, 78))
        draw.rectangle([sa_box[0] + 6, sa_box[1] + 6, sa_box[2] - 6, sa_box[3] - 6], outline=(98, 72, 44), width=3)
        # Satin-Matte Black Vulcanized Rubber Bicycle Inner-Tube (`prop_a`) w/ vulcanized mold seam & satin sheen!
        rough[pay0:pay1, pax0:pax1] = 0.36
        draw.line([(pax0, (pay0 + pay1) // 2), (pax1, (pay0 + pay1) // 2)], fill=(64, 68, 76), width=4)
        draw.line([(pax0, (pay0 + pay1) // 2 - 14), (pax1, (pay0 + pay1) // 2 - 14)], fill=(48, 52, 58), width=3)
        # Shiny Brass Schrader Valve Stems & Belt Buckle (`bands_metal`)
        rough[bm_box[1]:bm_box[3], bm_box[0]:bm_box[2]] = 0.28
        metal[bm_box[1]:bm_box[3], bm_box[0]:bm_box[2]] = 0.85

    elif kid_id == "tala":
        # Black-and-cream horizontal knit stripes on long sleeves (`torso_under`)!
        for y_st in range(tu_box[1], tu_box[3], 22):
            draw.rectangle([tu_box[0], y_st, tu_box[2], min(tu_box[3], y_st + 11)], fill=(24, 24, 30))
        # Cyan, Hot-Magenta & Yellow Paint Splatters across Royal-Purple Boxy Tee!
        rng_t = np.random.default_rng(707)
        splat_cols = [(36, 192, 208), (212, 50, 138), (214, 188, 44), (184, 180, 174)]
        for sp_b in (tf_box, tb_box):
            for idx_s in range(28):
                sx_s = int(rng_t.integers(sp_b[0] + 18, sp_b[2] - 18))
                sy_s = int(rng_t.integers(sp_b[1] + 18, sp_b[3] - 24))
                sr_s = int(rng_t.integers(5, 18))
                sc = splat_cols[idx_s % len(splat_cols)]
                draw.ellipse([sx_s - sr_s, sy_s - sr_s, sx_s + sr_s, sy_s + sr_s], fill=sc)
                if idx_s % 3 == 0:
                    draw.line([(sx_s, sy_s), (sx_s, sy_s + sr_s * 2 + 12)], fill=sc, width=4)
        # Rolled "ZINE" komiks cover (`socks_acc`) & Blue Ballpoint Star Doodles on Shaded Cream Canvas Sneakers (`footwear`)!
        draw.rectangle([sa_box[0], sa_box[1], sa_box[2], sa_box[3]], fill=(184, 176, 156), outline=(28, 28, 34), width=4)
        draw.text(((sa_box[0] + sa_box[2]) // 2, (sa_box[1] + sa_box[3]) // 2), "ZINE", fill=(198, 38, 84), anchor="mm", font=_load_bold_font(36))
        for sx_d, sy_d in ((fw_x0 + 65, fw_y0 + 75), (fw_x0 + 135, fw_y0 + 95), (fw_x0 + 185, fw_y0 + 68)):
            for ang_st in range(0, 180, 45):
                dx_l = int(14 * math.cos(math.radians(ang_st)))
                dy_l = int(14 * math.sin(math.radians(ang_st)))
                draw.line([(sx_d - dx_l, sy_d - dy_l), (sx_d + dx_l, sy_d + dy_l)], fill=(38, 78, 178), width=3)

    # 5b. Painterly Valorant Cloth Tension Folds & Collar/Hem Drape Shading on ALL Shirts/Undershirts!
    cloth_soft = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    cdraw = ImageDraw.Draw(cloth_soft)
    for c_box in (tf_box, tb_box, tu_box):
        cx0, cy0, cx1, cy1 = c_box
        cw, ch = cx1 - cx0, cy1 - cy0
        # Warm taupe diagonal tension folds + collar/waist hem shadow bands so cotton reads sculpted in gl_compatibility!
        cdraw.rectangle([cx0, cy0, cx1, cy0 + int(ch * 0.09)], fill=(38, 30, 22, 42))
        cdraw.rectangle([cx0, cy1 - int(ch * 0.12), cx1, cy1], fill=(34, 26, 20, 48))
        cdraw.line([(cx0 + int(cw * 0.16), cy0 + int(ch * 0.28)), (cx0 + int(cw * 0.54), cy0 + int(ch * 0.56))], fill=(34, 26, 20, 44), width=14)
        cdraw.line([(cx0 + int(cw * 0.84), cy0 + int(ch * 0.34)), (cx0 + int(cw * 0.44), cy0 + int(ch * 0.68))], fill=(34, 26, 20, 40), width=12)
        cdraw.line([(cx0 + int(cw * 0.24), cy0 + int(ch * 0.72)), (cx0 + int(cw * 0.76), cy0 + int(ch * 0.86))], fill=(34, 26, 20, 38), width=12)
    cloth_soft = cloth_soft.filter(ImageFilter.GaussianBlur(radius=8))
    albedo = Image.alpha_composite(albedo.convert("RGBA"), cloth_soft).convert("RGB")

    # 6. Apply Subtle Valorant Vertical Top-Down Light Gradient + Non-Clipping Highlight Guard (0 #FFFFFF Blowout!)
    albedo_np = np.array(albedo, dtype=np.uint8)
    for grad_tile in ("torso_front", "torso_back", "torso_under", "bottoms", "skin", "prop_a"):
        _apply_valorant_gradient(albedo_np, tile_inner_px(grad_tile, S), top_mul=1.02, bot_mul=0.84)

    # Protect eye catchlights, while soft-capping all cloth/skin/prop albedos <= 188 sRGB so Godot sun+sky never clips to #FFFFFF!
    eye_save = albedo_np[ey0:ey1, ex0:ex1].copy()
    al_f = albedo_np.astype(np.float32)
    over = np.maximum(0.0, al_f - 172.0)
    albedo_np = np.clip(np.minimum(al_f, 172.0) + 16.0 * np.tanh(over / 28.0), 0.0, 188.0).astype(np.uint8)
    albedo_np[ey0:ey1, ex0:ex1] = eye_save

    orm_np = np.zeros((S, S, 3), dtype=np.uint8)
    orm_np[..., 0] = 242
    orm_np[..., 1] = np.clip(rough * 255.0, 25, 255).astype(np.uint8)
    orm_np[..., 2] = np.clip(metal * 255.0, 0, 255).astype(np.uint8)

    out_dir.mkdir(parents=True, exist_ok=True)
    Image.fromarray(orm_np, mode="RGB").save(out_dir / f"_base_{kid_id}_0.png", format="PNG")
    Image.fromarray(albedo_np, mode="RGB").save(out_dir / f"_base_{kid_id}_1.png", format="PNG")
    _height_to_normal_png(height, strength=2.6).save(out_dir / f"_base_{kid_id}_2.png", format="PNG")


# ==============================================================================
# 9. MIXAMORIG: ARMATURE, SEMANTIC SKINNING & BLENDER MESH CONSTRUCTION
# ==============================================================================
def _sstep(a: float, b: float, v: float) -> float:
    t = max(0.0, min(1.0, (v - a) / max(1e-6, b - a)))
    return t * t * (3.0 - 2.0 * t)


_KID_Z_KNOTS: tuple[tuple[float, float], ...] = (
    (0.000, 0.000),  # Soles / bare toes grounded flat at 0.000
    (0.115, 0.118),  # Chunky 90s footwear / ankle height
    (0.490, 0.448),  # Compact sturdy kid lower leg
    (0.880, 0.808),  # Compact sturdy kid thigh (Hips at ~0.81m > 0.50m!)
    (1.348, 1.304),  # Sturdy youthful kid torso & collarbone / kumot cape cowl
    (1.430, 1.350),  # Short sturdy 4.5cm kid neck (eliminating adult giraffe neck!)
    (1.650, 1.636),  # Crisp ~1:5.3 Valorant street-kid head height (1.23x-1.26x crown-to-chin : cheek width!)
)


def _warp_kid_z(z: float) -> float:
    """Smooth C1 monotonic vertical map delivering authentic ~1:5.5 Valorant 9-12yo kid proportions."""
    knots = _KID_Z_KNOTS
    n_k = len(knots)
    slopes = [
        (knots[i + 1][1] - knots[i][1]) / (knots[i + 1][0] - knots[i][0])
        for i in range(n_k - 1)
    ]
    h_fillet = 0.020
    for i in range(1, n_k - 1):
        zk, yk = knots[i]
        if abs(z - zk) < h_fillet:
            u = (z - (zk - h_fillet)) / (2.0 * h_fillet)
            p0 = yk - slopes[i - 1] * h_fillet
            p1 = yk
            p2 = yk + slopes[i] * h_fillet
            om = 1.0 - u
            return om * om * p0 + 2.0 * om * u * p1 + u * u * p2

    if z <= knots[0][0]:
        return knots[0][1] + (z - knots[0][0]) * slopes[0]
    for i in range(n_k - 1):
        z0, y0 = knots[i]
        z1, _ = knots[i + 1]
        if z <= z1:
            return y0 + (z - z0) * slopes[i]
    return knots[-1][1] + (z - knots[-1][0]) * slopes[-1]


def warp_kid_point(x: float, y: float, z: float, spec: dict[str, Any]) -> tuple[float, float, float]:
    """
    Smooth, watertight 3D anatomical remap applied identically to mesh vertices AND all 77 skeleton joints:
      - Enlarges head + hair + masks/caps isotropically (~1.365x) for ~1:5.5 Valorant street-kid appeal
      - Shortens & thickens neck (-44% length, +22% width) so zero skinny adult neck exists
      - Gives compact athletic kid torso/legs, chunky 90s footwear (+15%), and expressive 1.18x stylized hands!
    """
    sw = spec["shoulder_w"]
    hw_leg = spec["hip_w"] * 0.80

    # Isolate lateral rest-pose arms/hands cleanly (z in [1.08, 1.40], |x| > sw, y < 0.095 clearing back cape & hair!)
    arm_w = (
        _sstep(sw * 1.02, sw * 1.25, abs(x))
        * _sstep(1.08, 1.15, z)
        * (1.0 - _sstep(1.375, 1.408, z))
        * (1.0 - _sstep(0.062, 0.098, y))
    )

    # 1. Core Torso / Neck / Crisp Tapered Valorant V-Jaw Head (1.24x H:W!) + Seamless Lateral Clavicle!
    # Restrict neck Z-compression & head X/Y scale strictly to central neck/head column so lateral clavicles/acromions
    # (`|x| > 0.102, z < 1.40`) blend 100% smoothly into `z_arm` with ZERO shoulder V-pinch!
    neck_col_w = 1.0 - _sstep(0.060, 0.102, abs(x)) * (1.0 - _sstep(1.395, 1.432, z))
    w_head = _sstep(1.348, 1.428, z) * neck_col_w
    w_jaw_v = (1.0 - _sstep(1.432, 1.504, z)) * _sstep(1.418, 1.434, z) * neck_col_w
    sx_core = (1.12 + (1.00 - 1.12) * w_head) * (1.0 - 0.11 * w_jaw_v)
    sy_core = 1.09 + (1.07 - 1.09) * w_head

    w_foot = (1.0 - _sstep(0.020, 0.165, z)) * (1.0 - arm_w)
    leg_side = _sstep(0.015, 0.055, abs(x)) * (1.0 if x >= 0.0 else -1.0)
    x_leg_c = leg_side * hw_leg
    x_ft = x_leg_c + (x - x_leg_c) * (1.0 + 0.16 * w_foot)
    y_ft = y * (1.0 + 0.14 * w_foot)

    x_core = x_ft * sx_core
    y_core = y_ft * sy_core
    z_central = _warp_kid_z(z)
    if z > 1.338 and neck_col_w < 0.9999:
        z_lat_sh = _warp_kid_z(1.338) + (z - 1.338) * 1.08
        z_core = neck_col_w * z_central + (1.0 - neck_col_w) * z_lat_sh
    else:
        z_core = z_central

    if arm_w <= 1e-6:
        return (x_core, y_core, z_core)

    # 2. Proportional Kid Arm Reach (0.94x) + Isotropic 1.18x Stylized Hands / Hand-Tsinelas / Tirador!
    s_arm = 1.0 if x >= 0.0 else -1.0
    slope = math.radians(10.0)
    cs, ss = math.cos(slope), math.sin(slope)

    dx_s = x - s_arm * sw
    dy_s = y - 0.005
    dz_s = z - 1.338
    d_ax = s_arm * dx_s * cs - dz_s * ss
    perp_x = dx_s - d_ax * s_arm * cs
    perp_y = dy_s
    perp_z = dz_s + d_ax * ss

    # Smooth C1 axial transition from 0.94x arm reach to 1.18x hand scale across wrist (d_ax ~ 0.435)
    k_hand = 42.0
    u_hand = (d_ax - 0.435) * k_hand
    splus = (u_hand + math.log1p(math.exp(-u_hand))) / k_hand if u_hand > 0.0 else math.log1p(math.exp(u_hand)) / k_hand
    d_ax_out = 0.94 * d_ax + (1.18 - 0.94) * splus
    r_perp = 1.10 + (1.18 - 1.10) * _sstep(0.405, 0.465, d_ax)

    sh_x_out = s_arm * sw * 1.12
    sh_y_out = 0.005 * 1.09
    sh_z_out = _warp_kid_z(1.338)

    x_arm = sh_x_out + d_ax_out * s_arm * cs + perp_x * r_perp
    y_arm = sh_y_out + perp_y * r_perp
    z_arm = sh_z_out - d_ax_out * ss + perp_z * r_perp

    om_a = 1.0 - arm_w
    return (
        om_a * x_core + arm_w * x_arm,
        om_a * y_core + arm_w * y_arm,
        om_a * z_core + arm_w * z_arm,
    )


def apply_valorant_kid_proportions(cb: CharBuilder, spec: dict[str, Any]) -> None:
    """Applies the smooth ~1:5.5 Valorant street-kid anatomical proportion map to all vertices."""
    cb.verts = [warp_kid_point(vx, vy, vz, spec) for (vx, vy, vz) in cb.verts]


def normalize_height_and_ground(cb: CharBuilder, target_height: float = 1.7000) -> tuple[float, float]:
    """
    Centers soles/bare feet cleanly at Z = 0.0000 and normalizes total character height
    to exact canonical 1.7000 m so test_authored_kids.gd & test_model_scale.gd pass 100%!
    Returns `(scale, z_min)` applied around Z = z_min.
    """
    zs = [v[2] for v in cb.verts]
    z_min = min(zs)
    z_max = max(zs)
    cur_h = max(0.1, z_max - z_min)
    scale = target_height / cur_h
    cb.verts = [
        (vx * scale, vy * scale, (vz - z_min) * scale)
        for (vx, vy, vz) in cb.verts
    ]
    return scale, z_min


def create_mixamorig_armature(kid_id: str, spec: dict[str, Any], norm_scale: float, z_min: float = 0.0) -> bpy.types.Object:
    """
    Builds the full 77-bone `mixamorig:` Skeleton3D in canonical rest pose with deterministic
    bone rolls (`X x Y = +Z` -> `+rot_x` ALWAYS bends toward the anatomical roll vector!).
    """
    s = norm_scale
    sw = spec["shoulder_w"] * s
    hw = spec["hip_w"] * 0.80 * s
    bulk = spec["limb_bulk"]
    slope = math.radians(10.0)
    cs, ss = math.cos(slope), math.sin(slope)

    arm_data = bpy.data.armatures.new(f"{kid_id}_ArmatureData")
    arm_obj = bpy.data.objects.new("Armature", arm_data)
    bpy.context.scene.collection.objects.link(arm_obj)
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm_data.edit_bones

    def _map_joint(pt: tuple[float, float, float]) -> Vector:
        wx, wy, wz = warp_kid_point(pt[0] / s, pt[1] / s, pt[2] / s, spec)
        return Vector((wx * s, wy * s, (wz - z_min) * s))

    def add_b(
        name: str,
        head: tuple[float, float, float],
        tail: tuple[float, float, float],
        parent_name: str | None,
        roll_vec: tuple[float, float, float],
    ) -> bpy.types.EditBone:
        b = eb.new(name)
        b.head = _map_joint(head)
        b.tail = _map_joint(tail)
        if (b.tail - b.head).length < 0.004:
            b.tail = b.head + Vector((0.0, 0.0, 0.015))
        b.use_connect = False
        if parent_name is not None and parent_name in eb:
            b.parent = eb[parent_name]
        b.align_roll(Vector(roll_vec))
        return b

    # Torso & Head Spine Chain (+rot_x leans FORWARD -Y!)
    add_b("mixamorig:Hips",        (0.0, 0.004 * s, 0.880 * s), (0.0, 0.002 * s, 0.975 * s), None,               (0.0, -1.0, 0.0))
    add_b("mixamorig:Spine",       (0.0, 0.002 * s, 0.975 * s), (0.0, 0.000,     1.095 * s), "mixamorig:Hips",   (0.0, -1.0, 0.0))
    add_b("mixamorig:Spine1",      (0.0, 0.000,     1.095 * s), (0.0, -0.002 * s, 1.215 * s), "mixamorig:Spine",  (0.0, -1.0, 0.0))
    add_b("mixamorig:Spine2",      (0.0, -0.002 * s, 1.215 * s), (0.0, 0.002 * s, 1.338 * s), "mixamorig:Spine1", (0.0, -1.0, 0.0))
    add_b("mixamorig:Neck",        (0.0, 0.002 * s, 1.338 * s), (0.0, 0.004 * s, 1.405 * s), "mixamorig:Spine2", (0.0, -1.0, 0.0))
    add_b("mixamorig:Head",        (0.0, 0.004 * s, 1.405 * s), (0.0, 0.000,     1.645 * s), "mixamorig:Neck",   (0.0, -1.0, 0.0))
    add_b("mixamorig:HeadTop_End", (0.0, 0.000,     1.645 * s), (0.0, 0.000,     1.700 * s), "mixamorig:Head",   (0.0, -1.0, 0.0))
    add_b("headfront",             (0.0, -0.095 * s, 1.515 * s), (0.0, -0.135 * s, 1.515 * s), "mixamorig:Head", (0.0, 0.0, 1.0))

    # Secondary Cape & Ponytail Chains (+rot_x billows BACKWARD +Y!)
    add_b("Cape1", (0.0, 0.088 * s, 1.320 * s), (0.0, 0.110 * s, 1.030 * s), "mixamorig:Spine2", (0.0, 1.0, 0.0))
    add_b("Cape2", (0.0, 0.110 * s, 1.030 * s), (0.0, 0.135 * s, 0.730 * s), "Cape1",            (0.0, 1.0, 0.0))
    add_b("Cape3", (0.0, 0.135 * s, 0.730 * s), (0.0, 0.155 * s, 0.430 * s), "Cape2",            (0.0, 1.0, 0.0))
    add_b("HairBack1", (0.0, 0.104 * s, 1.600 * s), (0.0, 0.170 * s, 1.500 * s), "mixamorig:Head", (0.0, 1.0, 0.0))
    add_b("HairBack2", (0.0, 0.170 * s, 1.500 * s), (0.0, 0.185 * s, 1.355 * s), "HairBack1",      (0.0, 1.0, 0.0))

    # Left (+X) & Right (-X) Arms + 5 Articulated 3-Joint Fingers
    sh_len = 0.455 * s
    for side, side_name in ((1.0, "Left"), (-1.0, "Right")):
        sx, sy, sz = side * sw, 0.005 * s, 1.338 * s
        ex = sx + side * (0.232 * s) * cs
        ey = sy + 0.006 * s
        ez = sz - (0.232 * s) * ss
        wx = sx + side * sh_len * cs
        wy = sy - 0.004 * s
        wz = sz - sh_len * ss
        hx_end = wx + side * (0.075 * s) * cs
        hz_end = wz - (0.075 * s) * ss

        add_b(f"mixamorig:{side_name}Shoulder", (side * 0.052 * s, 0.0, 1.325 * s), (sx, sy, sz), "mixamorig:Spine2", (0.0, -1.0, 0.0))
        add_b(f"mixamorig:{side_name}Arm",      (sx, sy, sz), (ex, ey, ez), f"mixamorig:{side_name}Shoulder",         (0.0, -1.0, 0.0))
        add_b(f"mixamorig:{side_name}ForeArm",  (ex, ey, ez), (wx, wy, wz), f"mixamorig:{side_name}Arm",              (0.0, -1.0, 0.0))
        add_b(f"mixamorig:{side_name}Hand",     (wx, wy, wz), (hx_end, wy - 0.002 * s, hz_end), f"mixamorig:{side_name}ForeArm", (0.0, -1.0, 0.0))
        add_b(f"mixamorig:{side_name}HandMiddle4", (hx_end, wy, hz_end), (hx_end + side * 0.015 * s, wy, hz_end), f"mixamorig:{side_name}Hand", (0.0, 0.0, -1.0))

        ax_dir = Vector((side * cs, -0.018, -ss)).normalized()
        up_ax = Vector((side * ss, 0.0, cs)).normalized()
        fwd_ax = Vector((0.0, -1.0, 0.0))
        f_dir = (ax_dir * 0.96 - up_ax * 0.24).normalized()

        for f_name, f_y_off, palm_d, f_len in [
            ("Index",  +0.027, 0.078, 0.068),
            ("Middle", +0.009, 0.082, 0.075),
            ("Ring",   -0.010, 0.079, 0.070),
            ("Pinky",  -0.027, 0.073, 0.056),
        ]:
            kn = Vector((wx, wy, wz)) + ax_dir * (palm_d * s) + fwd_ax * (f_y_off * bulk * s)
            p0 = kn
            p1 = kn + f_dir * (f_len * s * 0.36)
            p2 = kn + f_dir * (f_len * s * 0.68)
            p3 = kn + f_dir * (f_len * s * 1.00)
            p4 = kn + f_dir * (f_len * s * 1.18)
            add_b(f"mixamorig:{side_name}Hand{f_name}1",   tuple(p0), tuple(p1), f"mixamorig:{side_name}Hand",           (0.0, 0.0, -1.0))
            add_b(f"mixamorig:{side_name}Hand{f_name}2",   tuple(p1), tuple(p2), f"mixamorig:{side_name}Hand{f_name}1",  (0.0, 0.0, -1.0))
            add_b(f"mixamorig:{side_name}Hand{f_name}3",   tuple(p2), tuple(p3), f"mixamorig:{side_name}Hand{f_name}2",  (0.0, 0.0, -1.0))
            add_b(f"mixamorig:{side_name}Hand{f_name}Tip", tuple(p3), tuple(p4), f"mixamorig:{side_name}Hand{f_name}3",  (0.0, 0.0, -1.0))

        th_base = Vector((wx, wy, wz)) + ax_dir * (0.026 * s) + fwd_ax * (0.026 * bulk * s) - up_ax * (0.006 * s)
        th_dir = (ax_dir * 0.72 + fwd_ax * 0.62 - up_ax * 0.28).normalized()
        tp0 = th_base
        tp1 = th_base + th_dir * (0.062 * s * 0.38)
        tp2 = th_base + th_dir * (0.062 * s * 0.72)
        tp3 = th_base + th_dir * (0.062 * s * 1.00)
        tp4 = th_base + th_dir * (0.062 * s * 1.20)
        add_b(f"mixamorig:{side_name}HandThumb1",   tuple(tp0), tuple(tp1), f"mixamorig:{side_name}Hand",          (0.0, 0.0, -1.0))
        add_b(f"mixamorig:{side_name}HandThumb2",   tuple(tp1), tuple(tp2), f"mixamorig:{side_name}HandThumb1",    (0.0, 0.0, -1.0))
        add_b(f"mixamorig:{side_name}HandThumb3",   tuple(tp2), tuple(tp3), f"mixamorig:{side_name}HandThumb2",    (0.0, 0.0, -1.0))
        add_b(f"mixamorig:{side_name}HandThumbTip", tuple(tp3), tuple(tp4), f"mixamorig:{side_name}HandThumb3",    (0.0, 0.0, -1.0))

    # Left (+X) & Right (-X) Legs + Shorts Drape Bones (Ankle head at Z = 0.110 * s!)
    for side, side_name in ((1.0, "Left"), (-1.0, "Right")):
        lx = side * hw
        add_b(f"mixamorig:{side_name}UpLeg",   (lx, 0.004 * s, 0.875 * s), (lx, -0.006 * s, 0.490 * s), "mixamorig:Hips",               (0.0, -1.0, 0.0))
        add_b(f"mixamorig:{side_name}Leg",     (lx, -0.006 * s, 0.490 * s), (lx, 0.001 * s, 0.110 * s), f"mixamorig:{side_name}UpLeg",  (0.0, +1.0, 0.0))
        add_b(f"mixamorig:{side_name}Foot",    (lx, 0.001 * s, 0.110 * s),  (lx, -0.088 * s, 0.024 * s), f"mixamorig:{side_name}Leg",   (0.0, 0.0, +1.0))
        add_b(f"mixamorig:{side_name}ToeBase", (lx, -0.088 * s, 0.024 * s), (lx, -0.142 * s, 0.012 * s), f"mixamorig:{side_name}Foot",  (0.0, 0.0, +1.0))
        add_b(f"mixamorig:{side_name}Toe_End", (lx, -0.142 * s, 0.012 * s), (lx, -0.165 * s, 0.012 * s), f"mixamorig:{side_name}ToeBase", (0.0, 0.0, +1.0))
        add_b(f"mixamorig:{side_name}Shorts",      (lx, 0.0, 0.840 * s), (lx, 0.0, 0.660 * s), f"mixamorig:{side_name}UpLeg",  (0.0, -1.0, 0.0))
        add_b(f"mixamorig:{side_name}ShortsDrape", (lx, 0.0, 0.660 * s), (lx, 0.0, 0.510 * s), f"mixamorig:{side_name}Shorts", (0.0, -1.0, 0.0))

    bpy.ops.object.mode_set(mode="OBJECT")
    return arm_obj


def _seg_dist_sq(p: Vector, a: Vector, b: Vector) -> float:
    ab = b - a
    ab_len_sq = ab.length_squared
    if ab_len_sq < 1e-10:
        return (p - a).length_squared
    t = max(0.0, min(1.0, (p - a).dot(ab) / ab_len_sq))
    proj = a + ab * t
    return (p - proj).length_squared


def build_skinned_mesh_object(
    kid_id: str,
    cb: CharBuilder,
    arm_obj: bpy.types.Object,
    orm_png: bytes,
    albedo_png: bytes,
    normal_png: bytes,
) -> bpy.types.Object:
    """
    Creates the single joined MeshInstance3D (`<id>_Body`) with smooth shading,
    material named `"Original"`, and clean max-4-influence semantic capsule skin weights!
    """
    mesh = bpy.data.meshes.new(f"{kid_id}_Mesh")
    mesh.from_pydata(cb.verts, [], cb.tris)
    mesh.update()

    # Smooth shade every polygon for zero faceted/blocky primitives!
    mesh.polygons.foreach_set("use_smooth", [True] * len(mesh.polygons))

    # Deterministic semantic UV layer
    uv_layer = mesh.uv_layers.new(name="UVMap")
    flat_uvs: list[float] = []
    for uv0, uv1, uv2 in cb.tri_uvs:
        flat_uvs.extend([uv0[0], uv0[1], uv1[0], uv1[1], uv2[0], uv2[1]])
    uv_layer.data.foreach_set("uv", flat_uvs)

    mesh_obj = bpy.data.objects.new(f"{kid_id}_Body", mesh)
    bpy.context.scene.collection.objects.link(mesh_obj)
    mesh_obj.parent = arm_obj
    arm_mod = mesh_obj.modifiers.new(name="Armature", type="ARMATURE")
    arm_mod.object = arm_obj

    # Extract rest-space bone segments from armature
    bone_segs: dict[str, tuple[Vector, Vector]] = {}
    for b in arm_obj.data.bones:
        bone_segs[b.name] = (b.head_local.copy(), b.tail_local.copy())
        mesh_obj.vertex_groups.new(name=b.name)

    # Allowed candidate bones per semantic part tag -> prevents cross-leg / arm-ribcage tearing 100%!
    tag_candidates: dict[str, list[tuple[str, float]]] = {
        "head":      [("mixamorig:Head", 1.0)],
        "neck":      [("mixamorig:Neck", 0.045), ("mixamorig:Head", 0.055), ("mixamorig:Spine2", 0.065)],
        "hips":      [("mixamorig:Hips", 0.080), ("mixamorig:Spine", 0.085), ("mixamorig:LeftUpLeg", 0.068), ("mixamorig:RightUpLeg", 0.068)],
        "spine":     [("mixamorig:Spine", 0.085), ("mixamorig:Hips", 0.095), ("mixamorig:Spine1", 0.095)],
        "spine1":    [("mixamorig:Spine1", 0.085), ("mixamorig:Spine", 0.095), ("mixamorig:Spine2", 0.095)],
        "spine2":    [("mixamorig:Spine2", 0.090), ("mixamorig:Spine1", 0.100), ("mixamorig:Neck", 0.085), ("mixamorig:LeftShoulder", 0.095), ("mixamorig:RightShoulder", 0.095)],
        "cape1":     [("Cape1", 0.12), ("mixamorig:Spine2", 0.14), ("Cape2", 0.14)],
        "cape2":     [("Cape2", 0.12), ("Cape1", 0.14), ("Cape3", 0.14)],
        "cape3":     [("Cape3", 0.12), ("Cape2", 0.15)],
        "hair1":      [("HairBack1", 0.08), ("mixamorig:Head", 0.09), ("HairBack2", 0.09)],
        "hair2":      [("HairBack2", 0.08), ("HairBack1", 0.09)],
        "shoulder_L": [("mixamorig:Spine2", 0.085), ("mixamorig:LeftShoulder", 0.075), ("mixamorig:LeftArm", 0.085)],
        "arm_L":      [("mixamorig:LeftArm", 0.075), ("mixamorig:LeftShoulder", 0.080), ("mixamorig:Spine2", 0.052), ("mixamorig:LeftForeArm", 0.060)],
        "forearm_L":  [("mixamorig:LeftForeArm", 0.055), ("mixamorig:LeftArm", 0.065), ("mixamorig:LeftHand", 0.055)],
        "hand_L":     [("mixamorig:LeftHand", 1.0)],
        "shoulder_R": [("mixamorig:Spine2", 0.085), ("mixamorig:RightShoulder", 0.075), ("mixamorig:RightArm", 0.085)],
        "arm_R":      [("mixamorig:RightArm", 0.075), ("mixamorig:RightShoulder", 0.080), ("mixamorig:Spine2", 0.052), ("mixamorig:RightForeArm", 0.060)],
        "forearm_R":  [("mixamorig:RightForeArm", 0.055), ("mixamorig:RightArm", 0.065), ("mixamorig:RightHand", 0.055)],
        "hand_R":     [("mixamorig:RightHand", 1.0)],
        "upleg_L":   [("mixamorig:LeftUpLeg", 0.095), ("mixamorig:Hips", 0.045), ("mixamorig:LeftLeg", 0.036)],
        "shorts_L":  [("mixamorig:LeftShorts", 0.095), ("mixamorig:LeftUpLeg", 0.030), ("mixamorig:LeftLeg", 0.040)],
        "leg_L":     [("mixamorig:LeftLeg", 0.095), ("mixamorig:LeftUpLeg", 0.036), ("mixamorig:LeftFoot", 0.026)],
        "foot_L":    [("mixamorig:LeftFoot", 0.090), ("mixamorig:LeftLeg", 0.020), ("mixamorig:LeftToeBase", 0.045)],
        "toe_L":     [("mixamorig:LeftToeBase", 0.045), ("mixamorig:LeftFoot", 0.055)],
        "upleg_R":   [("mixamorig:RightUpLeg", 0.095), ("mixamorig:Hips", 0.045), ("mixamorig:RightLeg", 0.036)],
        "shorts_R":  [("mixamorig:RightShorts", 0.095), ("mixamorig:RightUpLeg", 0.030), ("mixamorig:RightLeg", 0.040)],
        "leg_R":     [("mixamorig:RightLeg", 0.095), ("mixamorig:RightUpLeg", 0.036), ("mixamorig:RightFoot", 0.026)],
        "foot_R":    [("mixamorig:RightFoot", 0.090), ("mixamorig:RightLeg", 0.020), ("mixamorig:RightToeBase", 0.045)],
        "toe_R":     [("mixamorig:RightToeBase", 0.045), ("mixamorig:RightFoot", 0.055)],
    }
    for s_ch, s_full in (("L", "Left"), ("R", "Right")):
        for fn in ("Thumb", "Index", "Middle", "Ring", "Pinky"):
            for jn in (1, 2, 3):
                cands = [(f"mixamorig:{s_full}Hand{fn}{jn}", 0.018)]
                if jn == 1:
                    cands.append((f"mixamorig:{s_full}Hand", 0.022))
                else:
                    cands.append((f"mixamorig:{s_full}Hand{fn}{jn - 1}", 0.020))
                if jn < 3:
                    cands.append((f"mixamorig:{s_full}Hand{fn}{jn + 1}", 0.020))
                tag_candidates[f"finger_{s_ch}_{fn}{jn}"] = cands

    vg_map = {vg.name: vg for vg in mesh_obj.vertex_groups}
    for v_idx, (v_co, tag) in enumerate(zip(cb.verts, cb.tags)):
        cands = tag_candidates.get(tag, [("mixamorig:Spine1", 1.0)])
        if len(cands) == 1:
            vg_map[cands[0][0]].add([v_idx], 1.0, "REPLACE")
            continue
        p = Vector(v_co)
        weights: list[tuple[str, float]] = []
        for b_name, sigma in cands:
            a, b = bone_segs[b_name]
            d2 = _seg_dist_sq(p, a, b)
            w = math.exp(-d2 / (2.0 * sigma * sigma))
            if w > 0.015:
                weights.append((b_name, w))
        if not weights:
            weights = [(cands[0][0], 1.0)]
        weights.sort(key=lambda item: item[1], reverse=True)
        weights = weights[:4]
        w_sum = sum(w for _, w in weights)
        for b_name, w in weights:
            vg_map[b_name].add([v_idx], w / w_sum, "REPLACE")

    # Build Principled BSDF Material named EXACTLY `"Original"`!
    mat = bpy.data.materials.new(name="Original")
    mat.use_nodes = True
    mat.use_backface_culling = False
    nt = mat.node_tree
    nt.nodes.clear()

    out_node = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 1.0
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.38
    nt.links.new(bsdf.outputs["BSDF"], out_node.inputs["Surface"])

    def load_packed_img(img_name: str, raw_bytes: bytes, non_color: bool) -> bpy.types.Image:
        tmp_p = CHAR_DIR / f"_tmp_{kid_id}_{img_name}.png"
        tmp_p.write_bytes(raw_bytes)
        img = bpy.data.images.load(str(tmp_p))
        img.name = img_name
        img.colorspace_settings.name = "Non-Color" if non_color else "sRGB"
        img.pack()
        try:
            tmp_p.unlink()
        except OSError:
            pass
        return img

    img_orm = load_packed_img(f"{kid_id}_orm", orm_png, non_color=True)
    img_alb = load_packed_img(f"{kid_id}_alb", albedo_png, non_color=False)
    img_nrm = load_packed_img(f"{kid_id}_nrm", normal_png, non_color=True)

    tex_alb = nt.nodes.new("ShaderNodeTexImage")
    tex_alb.image = img_alb
    nt.links.new(tex_alb.outputs["Color"], bsdf.inputs["Base Color"])

    tex_orm = nt.nodes.new("ShaderNodeTexImage")
    tex_orm.image = img_orm
    sep_rgb = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(tex_orm.outputs["Color"], sep_rgb.inputs["Color"])
    nt.links.new(sep_rgb.outputs["Green"], bsdf.inputs["Roughness"])
    nt.links.new(sep_rgb.outputs["Blue"], bsdf.inputs["Metallic"])

    tex_nrm = nt.nodes.new("ShaderNodeTexImage")
    tex_nrm.image = img_nrm
    nrm_node = nt.nodes.new("ShaderNodeNormalMap")
    nrm_node.inputs["Strength"].default_value = 0.85
    nt.links.new(tex_nrm.outputs["Color"], nrm_node.inputs["Color"])
    nt.links.new(nrm_node.outputs["Normal"], bsdf.inputs["Normal"])

    mesh.materials.append(mat)
    return mesh_obj


# ==============================================================================
# 10. 20-CLIP BIOMECHANICAL 30 FPS KID ANIMATION SUITE (W/ ARC-LENGTH CAST CALIBRATION!)
# ==============================================================================
FINGER_ROOT_BONES = [
    f"mixamorig:{s}Hand{fn}{jn}"
    for s in ("Left", "Right")
    for fn in ("Thumb", "Index", "Middle", "Ring", "Pinky")
    for jn in (1, 2)
]


def _write_action_fast(
    arm_obj: bpy.types.Object,
    name: str,
    n_frames: int,
    hips_loc_samples: list[tuple[float, float, float]],
    bone_rot_samples: dict[str, list[Quaternion]],
) -> bpy.types.Action:
    """Writes dense 30fps quaternion/location keyframes via foreach_set in milliseconds and pushes an NLA strip."""
    act = bpy.data.actions.new(name=name)
    arm_obj.animation_data.action = act
    frames = [float(f) for f in range(n_frames + 1)]

    # Hips location track (in bone local coords: Hips Y is +Z world up, Hips Z is -Y world forward, Hips X is +X world left!)
    for idx_c in range(3):
        fc = act.fcurve_ensure_for_datablock(arm_obj, 'pose.bones["mixamorig:Hips"].location', index=idx_c)
        fc.keyframe_points.add(n_frames + 1)
        flat = []
        for fi, loc in enumerate(hips_loc_samples):
            flat.extend([frames[fi], loc[idx_c]])
        fc.keyframe_points.foreach_set("co", flat)
        for kp in fc.keyframe_points:
            kp.interpolation = "LINEAR"
        fc.update()

    # Bone quaternion tracks (sign-continuous hemisphere!)
    for b_name, quats in bone_rot_samples.items():
        if b_name not in arm_obj.pose.bones:
            continue
        clean_q: list[Quaternion] = []
        prev_q: Quaternion | None = None
        for q in quats:
            qn = q.normalized()
            if prev_q is not None and prev_q.dot(qn) < 0.0:
                qn = Quaternion((-qn.w, -qn.x, -qn.y, -qn.z))
            clean_q.append(qn)
            prev_q = qn
        dpath = f'pose.bones["{b_name}"].rotation_quaternion'
        for idx_c in range(4):
            fc = act.fcurve_ensure_for_datablock(arm_obj, dpath, index=idx_c)
            fc.keyframe_points.add(n_frames + 1)
            flat = []
            for fi, qn in enumerate(clean_q):
                flat.extend([frames[fi], qn[idx_c]])
            fc.keyframe_points.foreach_set("co", flat)
            for kp in fc.keyframe_points:
                kp.interpolation = "LINEAR"
            fc.update()

    slot = arm_obj.animation_data.action_slot
    track = arm_obj.animation_data.nla_tracks.new()
    track.name = name
    strip = track.strips.new(name, 0, act)
    strip.action_slot = slot
    strip.frame_start = 0.0
    strip.frame_end = float(n_frames)
    track.mute = False
    return act


def _q(rx: float = 0.0, ry: float = 0.0, rz: float = 0.0) -> Quaternion:
    return Euler((rx, ry, rz), "XYZ").to_quaternion()


def _w_rot(
    arm_obj: bpy.types.Object,
    bone_name: str,
    pitch_fwd: float = 0.0,
    yaw_left: float = 0.0,
    tilt_left: float = 0.0,
) -> Quaternion:
    """
    Converts character/world-space angles into the exact local pose quaternion for `bone_name`!
      - `pitch_fwd > 0`: pitches UP-pointing Spine/Head FORWARD (-Y), or lifts DOWN-hanging Cape BACKWARD (+Y)!
      - `yaw_left > 0` : twists/turns chest or head toward Character's LEFT (+X)!
      - `tilt_left > 0`: tilts head/spine sideways toward Character's LEFT (+X)!
    """
    M = arm_obj.data.bones[bone_name].matrix_local.to_3x3()
    R = Euler((pitch_fwd, tilt_left, yaw_left), "XYZ").to_matrix()
    return (M.inverted() @ R @ M).to_quaternion()


def _aim_chain2(
    arm_obj: bpy.types.Object,
    root_name: str,
    dir1: tuple[float, float, float],
    mid_name: str,
    dir2: tuple[float, float, float],
) -> tuple[Quaternion, Quaternion]:
    """
    Exact 2-bone parent-relative world direction aimer for `(LeftArm, LeftForeArm)` and `(LeftUpLeg, LeftLeg)`.
    `dir1` and `dir2` are expressed in clean character coordinates `(X=Left, -Y=Forward, +Z=Up)` —
    guaranteeing 100% left-right symmetry and zero bone-roll guesswork!
    """
    b1 = arm_obj.data.bones[root_name]
    b2 = arm_obj.data.bones[mid_name]
    M1 = b1.matrix_local.to_3x3()
    M2 = b2.matrix_local.to_3x3()
    u1 = (b1.tail_local - b1.head_local).normalized()
    u2 = (b2.tail_local - b2.head_local).normalized()
    v1 = Vector(dir1)
    v2 = Vector(dir2)
    v1_n = v1.normalized() if v1.length > 1e-6 else u1
    v2_n = v2.normalized() if v2.length > 1e-6 else u2
    R1 = u1.rotation_difference(v1_n).to_matrix()
    q1 = (M1.inverted() @ R1 @ M1).to_quaternion()
    R_elb = (R1 @ u2).rotation_difference(v2_n).to_matrix()
    RM2 = R1 @ M2
    q2 = (RM2.inverted() @ R_elb @ RM2).to_quaternion()
    return q1, q2


def _arm_dirs_sagittal(
    side: float,
    sh_fwd: float,
    abduct: float,
    elb_bend: float,
    inward: float = 0.12,
) -> tuple[tuple[float, float, float], tuple[float, float, float]]:
    """
    Clean biomechanical arm/forearm direction vectors in `(X=Left, -Y=Forward, +Z=Up)`:
      - `side`: `+1.0` for Left (+X), `-1.0` for Right (-X)
      - `sh_fwd > 0`: swings upper arm forward (-Y); `< 0` swings upper arm back (+Y)
      - `abduct > 0`: abducts upper arm away from ribs
      - `elb_bend >= 0`: bends forearm forward/up from upper arm
      - `inward`: angles forearm slightly inward (+inward > 0) or outward (-inward < 0)
    """
    ca, sa = math.cos(abduct), math.sin(abduct)
    d1 = (side * sa, -math.sin(sh_fwd) * ca, -math.cos(sh_fwd) * ca)
    fa_ang = sh_fwd + elb_bend
    ci, si = math.cos(inward), math.sin(inward)
    d2 = (-side * si, -math.sin(fa_ang) * ci, -math.cos(fa_ang) * ci)
    return d1, d2


def _leg_dirs_sagittal(
    side: float,
    hip_flex: float,
    knee_bend: float,
    abduct: float = 0.04,
) -> tuple[tuple[float, float, float], tuple[float, float, float]]:
    """
    Clean biomechanical thigh/shin direction vectors in `(X=Left, -Y=Forward, +Z=Up)`:
      - `hip_flex > 0`: lifts knee forward (-Y); `< 0` extends thigh behind hip (+Y)
      - `knee_bend >= 0`: flexes knee backward (+Y), NEVER hyperextending forward!
    """
    kb = max(0.0, knee_bend)
    shin_ang = hip_flex - kb
    d1 = (side * abduct, -math.sin(hip_flex), -math.cos(hip_flex))
    d2 = (side * abduct * 0.85, -math.sin(shin_ang), -math.cos(shin_ang))
    return d1, d2


def _calibrate_cast_gamma(
    arm_obj: bpy.types.Object,
    pose_eval_fn: Callable[[float], dict[str, Quaternion]],
    f_mid_target: float,
) -> float:
    """
    Evaluates the true 3D trajectory of `mixamorig:RightHand` along s in [0, 1] inside Blender
    and computes the exact monotonic power exponent `gamma` so 50% cumulative 3D arc length
    lands on `u = f_mid_target` (`0.675s` for Cast_OneHand, `0.975s` for Cast_Run)!
    """
    pb_map = arm_obj.pose.bones
    for pb in pb_map:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = Quaternion((1, 0, 0, 0))
        pb.location = (0, 0, 0)

    n_probe = 80
    pts: list[Vector] = []
    for k in range(n_probe + 1):
        s = k / n_probe
        rots = pose_eval_fn(s)
        for b_nm, qv in rots.items():
            if b_nm in pb_map:
                pb_map[b_nm].rotation_quaternion = qv
        bpy.context.view_layer.update()
        pts.append((arm_obj.matrix_world @ pb_map["mixamorig:RightHand"].head).copy())

    cum = [0.0]
    for k in range(1, len(pts)):
        cum.append(cum[-1] + (pts[k] - pts[k - 1]).length)
    total = max(1e-5, cum[-1])
    half = 0.5 * total
    s_half = 0.5
    for k in range(1, len(cum)):
        if cum[k] >= half:
            seg = max(1e-6, cum[k] - cum[k - 1])
            frac = (half - cum[k - 1]) / seg
            s_half = ((k - 1) + frac) / n_probe
            break

    for pb in pb_map:
        pb.rotation_quaternion = Quaternion((1, 0, 0, 0))
    bpy.context.view_layer.update()

    gamma = math.log(max(0.05, min(0.95, s_half))) / math.log(max(0.05, min(0.95, f_mid_target)))
    return max(0.35, min(2.85, gamma))


def author_all_20_animations(kid_id: str, arm_obj: bpy.types.Object) -> None:
    """
    Authors all 20 required expressive 30fps animations using exact world-direction kinematics
    (`_w_rot` & `_aim_chain2`) for 100% left-right symmetry, grounded foot contact,
    Junjun heroic cape stance, and Kiko forward-leaning slipper-hand sprinter form!
    """
    bpy.context.scene.render.fps = 30
    arm_obj.animation_data_create()
    for pb in arm_obj.pose.bones:
        pb.rotation_mode = "QUATERNION"

    is_junjun = kid_id == "junjun"
    is_kiko = kid_id == "kiko"
    static_fingers = kid_id in STATIC_FINGERS_KIDS

    def wr(b_nm: str, pitch_fwd: float = 0.0, yaw_left: float = 0.0, tilt_left: float = 0.0) -> Quaternion:
        return _w_rot(arm_obj, b_nm, pitch_fwd, yaw_left, tilt_left)

    def set_arms(
        d: dict[str, Quaternion],
        l_d1: tuple[float, float, float],
        l_d2: tuple[float, float, float],
        r_d1: tuple[float, float, float],
        r_d2: tuple[float, float, float],
    ) -> None:
        d["mixamorig:LeftArm"], d["mixamorig:LeftForeArm"] = _aim_chain2(arm_obj, "mixamorig:LeftArm", l_d1, "mixamorig:LeftForeArm", l_d2)
        d["mixamorig:RightArm"], d["mixamorig:RightForeArm"] = _aim_chain2(arm_obj, "mixamorig:RightArm", r_d1, "mixamorig:RightForeArm", r_d2)

    def set_legs(
        d: dict[str, Quaternion],
        l_d1: tuple[float, float, float],
        l_d2: tuple[float, float, float],
        r_d1: tuple[float, float, float],
        r_d2: tuple[float, float, float],
    ) -> None:
        d["mixamorig:LeftUpLeg"], d["mixamorig:LeftLeg"] = _aim_chain2(arm_obj, "mixamorig:LeftUpLeg", l_d1, "mixamorig:LeftLeg", l_d2)
        d["mixamorig:RightUpLeg"], d["mixamorig:RightLeg"] = _aim_chain2(arm_obj, "mixamorig:RightUpLeg", r_d1, "mixamorig:RightLeg", r_d2)

    def eval_clip(
        clip_name: str,
        duration_s: float,
        fn_frame: Callable[[float, float], tuple[tuple[float, float, float], dict[str, Quaternion]]],
    ) -> None:
        n_f = int(round(duration_s * 30.0))
        hips_locs: list[tuple[float, float, float]] = []
        frame_dicts: list[dict[str, Quaternion]] = []
        all_bones: set[str] = set()
        for fi in range(n_f + 1):
            t_s = fi / 30.0
            ph = fi / max(1, n_f)  # exact 0.0 -> 1.0!
            h_loc, b_rots = fn_frame(t_s, ph)
            hips_locs.append(h_loc)
            frame_dicts.append(b_rots)
            all_bones.update(b_rots.keys())
        ident_q = Quaternion((1.0, 0.0, 0.0, 0.0))
        rots_dict: dict[str, list[Quaternion]] = {
            b_nm: [fd.get(b_nm, ident_q) for fd in frame_dicts]
            for b_nm in sorted(all_bones)
        }
        _write_action_fast(arm_obj, clip_name, n_f, hips_locs, rots_dict)

    def _orient_kiko_hand_tsinelas(
        d: dict[str, Quaternion],
        side_k: float,
        u_tgt_raw: tuple[float, float, float],
        n_tgt_raw: tuple[float, float, float],
    ) -> Quaternion:
        """Exact closed-form 3x3 world-frame hand solver so Kiko's teal/cream hand-tsinelas face cleanly forward!"""
        side_nm = "Left" if side_k > 0.0 else "Right"
        b_hand = f"mixamorig:{side_nm}Hand"
        slope_k = math.radians(10.0)
        u_src = Vector((side_k * math.cos(slope_k), -0.018, -math.sin(slope_k))).normalized()
        n_src_r = Vector((side_k * math.sin(slope_k), 0.0, math.cos(slope_k))).normalized()
        x_src = u_src.cross(n_src_r).normalized()
        n_src = x_src.cross(u_src).normalized()

        u_tgt = Vector(u_tgt_raw).normalized()
        n_tgt_r = Vector(n_tgt_raw).normalized()
        x_tgt = u_tgt.cross(n_tgt_r).normalized()
        n_tgt = x_tgt.cross(u_tgt).normalized()

        M_src = Matrix(((x_src.x, u_src.x, n_src.x), (x_src.y, u_src.y, n_src.y), (x_src.z, u_src.z, n_src.z)))
        M_tgt = Matrix(((x_tgt.x, u_tgt.x, n_tgt.x), (x_tgt.y, u_tgt.y, n_tgt.y), (x_tgt.z, u_tgt.z, n_tgt.z)))
        R_world = M_tgt @ M_src.transposed()

        W_anc = Matrix.Identity(3)
        for anc_nm in (
            "mixamorig:Hips",
            "mixamorig:Spine",
            "mixamorig:Spine1",
            "mixamorig:Spine2",
            f"mixamorig:{side_nm}Shoulder",
            f"mixamorig:{side_nm}Arm",
            f"mixamorig:{side_nm}ForeArm",
        ):
            q_anc = d.get(anc_nm)
            if q_anc is not None:
                M_a = arm_obj.data.bones[anc_nm].matrix_local.to_3x3()
                W_anc = W_anc @ (M_a @ q_anc.to_matrix() @ M_a.inverted())
        M_rest = arm_obj.data.bones[b_hand].matrix_local.to_3x3()
        M_id = W_anc @ M_rest
        return (M_id.inverted() @ R_world @ M_rest).to_quaternion()

    # 1. IDLE (2.00s, exact loop; bespoke confident asymmetric Valorant stances — ZERO symmetric C-bowed arms!)
    def frame_idle(_t: float, ph: float) -> tuple[tuple[float, float, float], dict[str, Quaternion]]:
        w = 2.0 * math.pi * ph
        sway = math.sin(w)
        breath = math.sin(w * 2.0) * (1.25 if is_kiko else 1.0)

        # Character-specific contrapposto hip yaw, tilt, spine lean & head attitude!
        stance_cfg: dict[str, tuple[float, float, float, float, float]] = {
            # (hips_yaw, hips_tilt, spine_pitch, head_pitch, head_tilt)
            "junjun": (-0.16, +0.035, -0.06, -0.03, -0.03),
            "kiko":   (-0.18, +0.025, +0.08, -0.08, +0.02),
            "ligaya": (+0.18, -0.040, -0.03, -0.01, +0.04),
            "migo":   (+0.17, -0.035, -0.02, -0.02, -0.03),
            "toni":   (-0.20, +0.040, -0.04, -0.02, -0.02),
            "popoy":  (+0.16, +0.030, +0.02, -0.02, +0.02),
            "inday":  (-0.16, +0.030, +0.05, -0.04, -0.02),
            "nene":   (-0.18, +0.030, -0.02, -0.02, +0.03),
            "buknoy": (+0.18, +0.035, -0.03, -0.02, -0.02),
            "tala":   (-0.18, -0.035, -0.02, -0.01, +0.04),
        }
        h_yaw, h_tilt, sp_pitch, hd_pitch, hd_tilt = stance_cfg.get(kid_id, (-0.16, 0.03, -0.02, -0.02, 0.0))
        h_drop = -0.020 if (is_kiko or kid_id == "inday") else -0.012
        h_loc = (0.012 * sway, h_drop + 0.009 * breath, 0.0)

        d: dict[str, Quaternion] = {
            "mixamorig:Hips":      wr("mixamorig:Hips",   pitch_fwd=0.02, yaw_left=h_yaw + 0.035 * sway, tilt_left=h_tilt + 0.018 * sway),
            "mixamorig:Spine":     wr("mixamorig:Spine",  pitch_fwd=sp_pitch + 0.02 * breath, yaw_left=-h_yaw * 0.55 - 0.025 * sway, tilt_left=-h_tilt * 0.6),
            "mixamorig:Spine1":    wr("mixamorig:Spine1", pitch_fwd=sp_pitch * 0.5 - 0.02 * breath, yaw_left=-h_yaw * 0.25 - 0.02 * sway),
            "mixamorig:Spine2":    wr("mixamorig:Spine2", pitch_fwd=-0.02 * breath, yaw_left=0.02 * sway),
            "mixamorig:Neck":      wr("mixamorig:Neck",   pitch_fwd=0.015 * breath, yaw_left=-h_yaw * 0.15 + 0.025 * sway),
            "mixamorig:Head":      wr("mixamorig:Head",   pitch_fwd=hd_pitch - 0.025 * breath, yaw_left=-h_yaw * 0.60 + 0.04 * sway, tilt_left=hd_tilt + 0.02 * sway),
            "mixamorig:LeftHand":  wr("mixamorig:LeftHand",  pitch_fwd=0.05 * sway),
            "mixamorig:RightHand": wr("mixamorig:RightHand", pitch_fwd=-0.05 * sway),
            "mixamorig:LeftFoot":  wr("mixamorig:LeftFoot",  pitch_fwd=-0.04),
            "mixamorig:RightFoot": wr("mixamorig:RightFoot", pitch_fwd=-0.04),
            "Cape1":               wr("Cape1",     pitch_fwd=0.18 + 0.08 * sway, yaw_left=0.10 + 0.05 * sway),
            "Cape2":               wr("Cape2",     pitch_fwd=0.22 + 0.11 * math.sin(w - 0.6), yaw_left=0.14 + 0.06 * sway),
            "Cape3":               wr("Cape3",     pitch_fwd=0.26 + 0.14 * math.sin(w - 1.1), yaw_left=0.18 + 0.07 * sway),
            "HairBack1":           wr("HairBack1", pitch_fwd=0.10 + 0.06 * math.sin(w - 0.5), yaw_left=0.04 * sway),
            "HairBack2":           wr("HairBack2", pitch_fwd=0.12 + 0.08 * math.sin(w - 0.9), yaw_left=0.05 * sway),
        }

        # Bespoke asymmetric arm poses per kid (0% symmetric C-bowed mannequin arms, +2.2cm clean hip-fist clearance!)
        def _hip_arm(side_h: float) -> tuple[tuple[float, float, float], tuple[float, float, float]]:
            # Elbow swings cleanly out & slightly behind ribs; forearm plants cleanly outside outer hip crest (0 clipping!)
            return (
                (side_h * 0.62, +0.24 + 0.02 * sway * side_h, -0.75),
                (-side_h * 0.27, -0.16 - 0.02 * breath, -0.95),
            )

        def _relaxed_arm(side_r: float) -> tuple[tuple[float, float, float], tuple[float, float, float]]:
            # Natural straight-hanging athletic kid arm (inward=-0.05 prevents any C-bow!)
            return _arm_dirs_sagittal(side_r, -0.04 - 0.04 * sway * side_r, 0.13, 0.18 + 0.03 * breath, inward=-0.05)

        hip_fist_side = 0.0  # +1 = Left fist on hip, -1 = Right fist on hip
        if is_junjun:
            # Kapitan Kumot: Left superhero fist planted proudly on outer left hip, right arm confident & heroic!
            l_a1, l_a2 = _hip_arm(+1.0)
            r_a1, r_a2 = _relaxed_arm(-1.0)
            hip_fist_side = +1.0
        elif is_kiko:
            # Tsinelas-Kamay Sprinter: Chest/rib-height sprinter guard (~15cm below face!) showcasing BOTH teal/cream hand-tsinelas!
            l_a1 = (+0.34, +0.04 + 0.04 * sway, -0.94)
            l_a2 = (-0.16, -0.97, +0.14 + 0.04 * breath)
            r_a1 = (-0.32, -0.06 - 0.04 * sway, -0.94)
            r_a2 = (+0.14, -0.97, +0.18 + 0.04 * breath)
        elif kid_id == "ligaya":
            # Kumander Teks: Left hand on outer hip, right hand poised forward ready to flick teks!
            l_a1, l_a2 = _hip_arm(+1.0)
            r_a1, r_a2 = _arm_dirs_sagittal(-1.0, 0.24 - 0.04 * sway, 0.18, 1.12 + 0.05 * breath, inward=0.12)
            hip_fist_side = +1.0
        elif kid_id == "nene":
            # Tirador Scout: Right hand raised holding wooden Y-fork tirador clearly in front, left arm natural!
            l_a1, l_a2 = _relaxed_arm(+1.0)
            r_a1, r_a2 = _arm_dirs_sagittal(-1.0, 0.28 - 0.04 * sway, 0.22, 1.22 + 0.05 * breath, inward=0.06)
        elif kid_id == "popoy":
            # Tangke: Left forearm raised angled across chest displaying cardboard kalasag shield, right arm natural!
            l_a1, l_a2 = _arm_dirs_sagittal(+1.0, 0.26 + 0.04 * sway, 0.22, 1.16 + 0.04 * breath, inward=0.22)
            r_a1, r_a2 = _relaxed_arm(-1.0)
        elif kid_id == "buknoy":
            # Mekaniko: Bare left forearm raised across chest displaying coiled black inner-tube, right arm relaxed by tool pouch!
            l_a1, l_a2 = _arm_dirs_sagittal(+1.0, 0.30 + 0.04 * sway, 0.16, 1.24 + 0.05 * breath, inward=0.24)
            r_a1, r_a2 = _relaxed_arm(-1.0)
        elif kid_id == "inday":
            # Reyna ng Patintero: Agile asymmetric line-blocker ready guard!
            l_a1, l_a2 = _arm_dirs_sagittal(+1.0, 0.22 + 0.05 * sway, 0.36, 0.92 + 0.04 * breath, inward=-0.08)
            r_a1, r_a2 = _relaxed_arm(-1.0)
        elif kid_id == "toni":
            # Barangay Liga MVP: Right fist on outer hip swagger, left arm relaxed streetball ready!
            l_a1, l_a2 = _relaxed_arm(+1.0)
            r_a1, r_a2 = _hip_arm(-1.0)
            hip_fist_side = -1.0
        elif kid_id == "migo":
            # Plastik-Balloon Alchemist: Left hand raised near chest belt-bag, right arm relaxed asymmetric!
            l_a1, l_a2 = _arm_dirs_sagittal(+1.0, 0.24 + 0.04 * sway, 0.18, 1.10 + 0.05 * breath, inward=0.18)
            r_a1, r_a2 = _relaxed_arm(-1.0)
        else:
            # Tala: Left hand resting on outer hip artist apron, right fingerless-gloved hand poised expressively!
            l_a1, l_a2 = _hip_arm(+1.0)
            r_a1, r_a2 = _arm_dirs_sagittal(-1.0, 0.22 - 0.04 * sway, 0.18, 1.06 + 0.05 * breath, inward=0.14)
            hip_fist_side = +1.0

        set_arms(d, l_a1, l_a2, r_a1, r_a2)

        if hip_fist_side > 0.0:
            d["mixamorig:LeftHand"] = _q(0.32, 0.0, 0.0)
        elif hip_fist_side < 0.0:
            d["mixamorig:RightHand"] = _q(0.32, 0.0, 0.0)

        if is_kiko:
            # Orient BOTH chest-height hand-tsinelas ~30 deg upward & forward so teal Y-straps + 5 bare fingers pop w/ 0 face occlusion!
            d["mixamorig:LeftHand"] = _orient_kiko_hand_tsinelas(d, +1.0, (-0.16, +0.46, +0.87), (+0.22, -0.86, +0.46))
            d["mixamorig:RightHand"] = _orient_kiko_hand_tsinelas(d, -1.0, (+0.16, +0.46, +0.87), (-0.22, -0.86, +0.46))

        # Asymmetric contrapposto leg stance keeping lowest sole firmly grounded at Y=0!
        if is_kiko:
            l_l1, l_l2 = _leg_dirs_sagittal(+1.0, 0.16 + 0.02 * breath, 0.24 + 0.03 * breath, abduct=0.08)
            r_l1, r_l2 = _leg_dirs_sagittal(-1.0, -0.05 + 0.02 * breath, 0.14 + 0.03 * breath, abduct=0.08)
        elif kid_id == "inday":
            l_l1, l_l2 = _leg_dirs_sagittal(+1.0, 0.14 + 0.02 * breath, 0.24 + 0.03 * breath, abduct=0.09)
            r_l1, r_l2 = _leg_dirs_sagittal(-1.0, 0.06 + 0.02 * breath, 0.16 + 0.03 * breath, abduct=0.09)
        elif h_yaw < 0.0:
            l_l1, l_l2 = _leg_dirs_sagittal(+1.0, 0.11 + 0.02 * breath, 0.18 + 0.03 * breath, abduct=0.07)
            r_l1, r_l2 = _leg_dirs_sagittal(-1.0, 0.04 + 0.02 * breath, 0.10 + 0.02 * breath, abduct=0.07)
        else:
            l_l1, l_l2 = _leg_dirs_sagittal(+1.0, 0.04 + 0.02 * breath, 0.10 + 0.02 * breath, abduct=0.07)
            r_l1, r_l2 = _leg_dirs_sagittal(-1.0, 0.11 + 0.02 * breath, 0.18 + 0.03 * breath, abduct=0.07)
        set_legs(d, l_l1, l_l2, r_l1, r_l2)

        if not static_fingers:
            for idx_f, fb in enumerate(FINGER_ROOT_BONES):
                is_hip_hand = (hip_fist_side > 0.0 and "LeftHand" in fb) or (hip_fist_side < 0.0 and "RightHand" in fb)
                base_curl = 0.06 if is_kiko else (0.92 if is_hip_hand else 0.28)
                curl = base_curl + 0.05 * math.sin(w + idx_f * 0.25)
                d[fb] = _q(curl, 0.0, 0.0)
        return h_loc, d

    eval_clip("Idle", 2.00, frame_idle)

    # Reusable Parametric Gait Generator for Run (0.60s), walk (0.90s), sprint (0.48s), Exhausted_Run (0.90s)!
    def make_gait(
        stride: float,
        knee_lift: float,
        lean: float,
        arm_pump: float,
        elbow_bend: float,
        bob: float,
        cape_lift: float,
        head_droop: float = 0.0,
    ) -> Callable[[float, float], tuple[tuple[float, float, float], dict[str, Quaternion]]]:
        def _fn(_t: float, ph: float) -> tuple[tuple[float, float, float], dict[str, Quaternion]]:
            w = 2.0 * math.pi * ph
            sL = math.sin(w)
            sR = math.sin(w + math.pi)
            c2 = math.cos(2.0 * w)
            s2 = math.sin(2.0 * w)
            lean_eff = lean + (0.12 if is_kiko else 0.0)
            h_loc = (0.018 * math.cos(w), -0.028 + bob * c2, 0.012 * s2)
            kL = 0.16 + knee_lift * max(0.0, math.sin(w - 0.55)) + 0.14 * max(0.0, -sL)
            kR = 0.16 + knee_lift * max(0.0, math.sin(w + math.pi - 0.55)) + 0.14 * max(0.0, -sR)
            d: dict[str, Quaternion] = {
                "mixamorig:Hips":      wr("mixamorig:Hips",   pitch_fwd=lean_eff * 0.35, yaw_left=-0.14 * sL, tilt_left=0.04 * math.cos(w)),
                "mixamorig:Spine":     wr("mixamorig:Spine",  pitch_fwd=lean_eff * 0.35 + 0.03 * c2, yaw_left=+0.12 * sL, tilt_left=-0.03 * math.cos(w)),
                "mixamorig:Spine1":    wr("mixamorig:Spine1", pitch_fwd=lean_eff * 0.25, yaw_left=+0.12 * sL),
                "mixamorig:Spine2":    wr("mixamorig:Spine2", pitch_fwd=lean_eff * 0.15, yaw_left=+0.10 * sL),
                "mixamorig:Neck":      wr("mixamorig:Neck",   pitch_fwd=-lean_eff * 0.35 + head_droop * 0.5, yaw_left=-0.08 * sL),
                "mixamorig:Head":      wr("mixamorig:Head",   pitch_fwd=-lean_eff * 0.45 + head_droop * 0.5 + 0.03 * c2, yaw_left=-0.08 * sL),
                "mixamorig:LeftHand":  wr("mixamorig:LeftHand",  pitch_fwd=0.10 * sR),
                "mixamorig:RightHand": wr("mixamorig:RightHand", pitch_fwd=0.10 * sL),
                "mixamorig:LeftFoot":  wr("mixamorig:LeftFoot",  pitch_fwd=-0.18 * sL),
                "mixamorig:RightFoot": wr("mixamorig:RightFoot", pitch_fwd=-0.18 * sR),
                "Cape1":               wr("Cape1",     pitch_fwd=cape_lift + 0.10 * s2, yaw_left=0.06 * sL),
                "Cape2":               wr("Cape2",     pitch_fwd=cape_lift * 0.85 + 0.16 * math.sin(2.0 * w - 0.7), yaw_left=0.08 * sL),
                "Cape3":               wr("Cape3",     pitch_fwd=cape_lift * 0.65 + 0.22 * math.sin(2.0 * w - 1.3), yaw_left=0.10 * sL),
                "HairBack1":           wr("HairBack1", pitch_fwd=cape_lift * 0.6 + 0.14 * s2, yaw_left=0.06 * sL),
                "HairBack2":           wr("HairBack2", pitch_fwd=cape_lift * 0.5 + 0.18 * math.sin(2.0 * w - 0.8), yaw_left=0.08 * sL),
            }
            # Contralateral arm pump: when Left leg swings forward (sL > 0), Right arm pumps forward!
            l_a1, l_a2 = _arm_dirs_sagittal(+1.0, arm_pump * sR, 0.24, elbow_bend + 0.25 * max(0.0, sR), inward=0.12)
            r_a1, r_a2 = _arm_dirs_sagittal(-1.0, arm_pump * sL, 0.24, elbow_bend + 0.25 * max(0.0, sL), inward=0.12)
            set_arms(d, l_a1, l_a2, r_a1, r_a2)
            if is_kiko:
                # Keep Kiko's hand-tsinelas blade-aerodynamic & clearly visible along his pumping forearms in Run/sprint!
                d["mixamorig:LeftHand"] = _orient_kiko_hand_tsinelas(d, +1.0, l_a2, (+0.45, -0.82, +0.35))
                d["mixamorig:RightHand"] = _orient_kiko_hand_tsinelas(d, -1.0, r_a2, (-0.45, -0.82, +0.35))

            l_l1, l_l2 = _leg_dirs_sagittal(+1.0, 0.12 + stride * sL, kL, abduct=0.03)
            r_l1, r_l2 = _leg_dirs_sagittal(-1.0, 0.12 + stride * sR, kR, abduct=0.03)
            set_legs(d, l_l1, l_l2, r_l1, r_l2)

            if not static_fingers:
                for fb in FINGER_ROOT_BONES[:8]:
                    d[fb] = _q((0.18 if is_kiko else 0.56) + 0.05 * c2, 0.0, 0.0)
            return h_loc, d
        return _fn

    # 2. Run (0.60s -> exact 2 steps per 0.6s = 6 steps in 1.8s!)
    eval_clip("Run", 0.60, make_gait(stride=0.72, knee_lift=0.92, lean=0.22, arm_pump=0.78, elbow_bend=1.18, bob=0.032, cape_lift=0.58))
    # 3. Exhausted_Run (0.90s)
    eval_clip("Exhausted_Run", 0.90, make_gait(stride=0.46, knee_lift=0.58, lean=0.42, arm_pump=0.38, elbow_bend=0.62, bob=0.024, cape_lift=0.28, head_droop=0.36))
    # 4. walk (0.90s)
    eval_clip("walk", 0.90, make_gait(stride=0.42, knee_lift=0.46, lean=0.06, arm_pump=0.40, elbow_bend=0.36, bob=0.018, cape_lift=0.18))
    # 5. sprint (0.48s)
    eval_clip("sprint", 0.48, make_gait(stride=0.92, knee_lift=1.18, lean=0.36, arm_pump=1.02, elbow_bend=1.35, bob=0.042, cape_lift=0.82))

    # 6. Cast_OneHand (2.20s, exact calibrated 50% right-hand release midpoint at t = 0.675s in [0.50, 0.90]!)
    def _cast_throw_pose(s_prog: float) -> dict[str, Quaternion]:
        # Smooth sweep s_prog in [0 (cocked back behind right shoulder) -> 1 (snapped forward across chest)]
        d: dict[str, Quaternion] = {
            "mixamorig:Hips":          wr("mixamorig:Hips",   pitch_fwd=0.05 + 0.16 * s_prog, yaw_left=-0.30 + 0.64 * s_prog),
            "mixamorig:Spine":         wr("mixamorig:Spine",  pitch_fwd=-0.06 + 0.24 * s_prog, yaw_left=-0.26 + 0.60 * s_prog),
            "mixamorig:Spine1":        wr("mixamorig:Spine1", pitch_fwd=-0.05 + 0.20 * s_prog, yaw_left=-0.24 + 0.58 * s_prog),
            "mixamorig:Spine2":        wr("mixamorig:Spine2", pitch_fwd=-0.04 + 0.16 * s_prog, yaw_left=-0.22 + 0.54 * s_prog),
            "mixamorig:RightShoulder": wr("mixamorig:RightShoulder", yaw_left=-0.16 + 0.38 * s_prog),
            "mixamorig:RightHand":     wr("mixamorig:RightHand", pitch_fwd=-0.25 + 0.65 * s_prog),
            "mixamorig:Neck":          wr("mixamorig:Neck",   yaw_left=+0.22 - 0.42 * s_prog),
            "mixamorig:Head":          wr("mixamorig:Head",   pitch_fwd=-0.06, yaw_left=+0.18 - 0.38 * s_prog),
            "Cape1":                   wr("Cape1",            pitch_fwd=0.35 + 0.45 * s_prog, yaw_left=-0.25 * s_prog),
            "Cape2":                   wr("Cape2",            pitch_fwd=0.30 + 0.50 * s_prog, yaw_left=-0.30 * s_prog),
        }
        r_d1 = (-0.68 + 0.86 * s_prog, +0.62 - 1.58 * s_prog, +0.36 - 0.30 * s_prog)
        r_d2 = (-0.42 + 0.70 * s_prog, +0.16 - 1.12 * s_prog, +0.86 - 1.04 * s_prog)
        l_d1, l_d2 = _arm_dirs_sagittal(+1.0, 0.55 - 0.95 * s_prog, 0.32, 0.95, inward=0.15)
        set_arms(d, l_d1, l_d2, r_d1, r_d2)
        l_l1, l_l2 = _leg_dirs_sagittal(+1.0, 0.32 + 0.14 * s_prog, 0.38, abduct=0.06)
        r_l1, r_l2 = _leg_dirs_sagittal(-1.0, -0.18 - 0.12 * s_prog, 0.26, abduct=0.06)
        set_legs(d, l_l1, l_l2, r_l1, r_l2)
        return d

    gamma_stand = _calibrate_cast_gamma(arm_obj, _cast_throw_pose, f_mid_target=(0.675 - 0.50) / 0.40)

    def frame_cast_onehand(t_s: float, _ph: float) -> tuple[tuple[float, float, float], dict[str, Quaternion]]:
        _, base_idle = frame_idle(0.0, 0.0)
        if t_s <= 0.50:
            u = t_s / 0.50
            sm = u * u * (3.0 - 2.0 * u)
            target = _cast_throw_pose(0.0)
            out = {k: base_idle.get(k, Quaternion((1, 0, 0, 0))).slerp(v, sm) for k, v in target.items()}
            for k, v in base_idle.items():
                out.setdefault(k, v)
            return (0.0, -0.025 * sm, -0.04 * sm), out
        elif t_s <= 0.90:
            u = max(0.0, min(1.0, (t_s - 0.50) / 0.40))
            s_prog = u ** gamma_stand
            target = _cast_throw_pose(s_prog)
            for k, v in base_idle.items():
                target.setdefault(k, v)
            return (0.0, -0.025 - 0.020 * s_prog, -0.04 + 0.10 * s_prog), target
        else:
            u = min(1.0, (t_s - 0.90) / 1.30)
            sm = u * u * (3.0 - 2.0 * u)
            end_p = _cast_throw_pose(1.0)
            out = {k: end_p.get(k, base_idle.get(k, Quaternion((1, 0, 0, 0)))).slerp(base_idle.get(k, Quaternion((1, 0, 0, 0))), sm) for k in set(end_p) | set(base_idle)}
            return (0.0, -0.045 * (1.0 - sm) - 0.012 * sm, 0.06 * (1.0 - sm)), out

    eval_clip("Cast_OneHand", 2.20, frame_cast_onehand)

    # 7. Cast_Run (2.20s, exact calibrated 50% right-hand release midpoint at t = 0.975s in [0.85, 1.15]!)
    gamma_move = _calibrate_cast_gamma(arm_obj, _cast_throw_pose, f_mid_target=(0.975 - 0.85) / 0.30)
    run_base_fn = make_gait(stride=0.66, knee_lift=0.84, lean=0.22, arm_pump=0.68, elbow_bend=1.10, bob=0.028, cape_lift=0.52)

    def frame_cast_run(t_s: float, _ph: float) -> tuple[tuple[float, float, float], dict[str, Quaternion]]:
        h_run, r_run = run_base_fn(t_s, (t_s / 0.55) % 1.0)
        upper_keys = {
            "mixamorig:Spine", "mixamorig:Spine1", "mixamorig:Spine2",
            "mixamorig:RightShoulder", "mixamorig:RightArm", "mixamorig:RightForeArm", "mixamorig:RightHand",
            "mixamorig:LeftArm", "mixamorig:LeftForeArm", "mixamorig:Neck", "mixamorig:Head", "Cape1", "Cape2",
        }
        if t_s < 0.45:
            return h_run, r_run
        elif t_s <= 0.85:
            u = (t_s - 0.45) / 0.40
            sm = u * u * (3.0 - 2.0 * u)
            wind = _cast_throw_pose(0.0)
            out = dict(r_run)
            for k in upper_keys:
                if k in wind:
                    out[k] = r_run.get(k, Quaternion((1, 0, 0, 0))).slerp(wind[k], sm)
            return h_run, out
        elif t_s <= 1.15:
            u = max(0.0, min(1.0, (t_s - 0.85) / 0.30))
            s_prog = u ** gamma_move
            whip = _cast_throw_pose(s_prog)
            _, r_mid = run_base_fn(0.85, (0.85 / 0.55) % 1.0)
            out = dict(r_mid)
            for k in upper_keys | {"mixamorig:Hips"}:
                if k in whip:
                    out[k] = whip[k]
            return (0.0, -0.032, 0.04 * s_prog), out
        else:
            u = min(1.0, (t_s - 1.15) / 1.05)
            sm = u * u * (3.0 - 2.0 * u)
            end_p = _cast_throw_pose(1.0)
            out = dict(r_run)
            for k in upper_keys:
                if k in end_p:
                    out[k] = end_p[k].slerp(r_run.get(k, Quaternion((1, 0, 0, 0))), sm)
            return h_run, out

    eval_clip("Cast_Run", 2.20, frame_cast_run)

    # 8. Stunned (1.60s, exact loop: dizzy circular wobble)
    def frame_stunned(_t: float, ph: float) -> tuple[tuple[float, float, float], dict[str, Quaternion]]:
        w = 2.0 * math.pi * ph
        sx, cz = math.sin(w), math.cos(w)
        h_loc = (0.045 * sx, -0.055 + 0.015 * math.sin(2.0 * w), 0.035 * cz)
        d: dict[str, Quaternion] = {
            "mixamorig:Hips":      wr("mixamorig:Hips",   pitch_fwd=0.08 * cz, yaw_left=0.10 * sx, tilt_left=0.08 * sx),
            "mixamorig:Spine":     wr("mixamorig:Spine",  pitch_fwd=0.16 + 0.12 * cz, yaw_left=0.10 * sx, tilt_left=0.10 * sx),
            "mixamorig:Spine1":    wr("mixamorig:Spine1", pitch_fwd=0.10 * cz, yaw_left=0.08 * sx, tilt_left=0.08 * sx),
            "mixamorig:Neck":      wr("mixamorig:Neck",   pitch_fwd=0.14 * math.cos(w + 0.8), yaw_left=0.14 * sx, tilt_left=0.16 * math.sin(w + 0.6)),
            "mixamorig:Head":      wr("mixamorig:Head",   pitch_fwd=0.18 * math.cos(w + 1.1), yaw_left=0.16 * sx, tilt_left=0.20 * math.sin(w + 0.9)),
            "mixamorig:LeftFoot":  wr("mixamorig:LeftFoot",  pitch_fwd=-0.08),
            "mixamorig:RightFoot": wr("mixamorig:RightFoot", pitch_fwd=-0.08),
        }
        l_a1, l_a2 = _arm_dirs_sagittal(+1.0, 0.25 + 0.22 * cz, 0.42 + 0.12 * sx, 0.78 + 0.15 * sx, inward=0.05)
        r_a1, r_a2 = _arm_dirs_sagittal(-1.0, 0.25 - 0.22 * cz, 0.42 - 0.12 * sx, 0.78 - 0.15 * sx, inward=0.05)
        set_arms(d, l_a1, l_a2, r_a1, r_a2)
        l_l1, l_l2 = _leg_dirs_sagittal(+1.0, 0.28 + 0.06 * cz, 0.48 + 0.08 * cz, abduct=0.08)
        r_l1, r_l2 = _leg_dirs_sagittal(-1.0, 0.28 - 0.06 * cz, 0.48 - 0.08 * cz, abduct=0.08)
        set_legs(d, l_l1, l_l2, r_l1, r_l2)
        return h_loc, d

    eval_clip("Stunned", 1.60, frame_stunned)

    # 9. Airborne (1.60s, exact loop; 0.10..0.60s outstretched Patintero dive leap for KidModel.dive_step!)
    def frame_airborne(t_s: float, ph: float) -> tuple[tuple[float, float, float], dict[str, Quaternion]]:
        if t_s < 0.12:
            env = (t_s / 0.12)
        elif t_s < 0.58:
            env = 1.0
        else:
            env = max(0.0, 1.0 - (t_s - 0.58) / 1.02)
        env = env * env * (3.0 - 2.0 * env)
        flutter = math.sin(2.0 * math.pi * ph * 2.0) * 0.08
        h_loc = (0.0, 0.04 * env, 0.06 * env)
        d: dict[str, Quaternion] = {
            "mixamorig:Hips":   wr("mixamorig:Hips",   pitch_fwd=0.24 * env),
            "mixamorig:Spine":  wr("mixamorig:Spine",  pitch_fwd=0.14 - 0.24 * env),
            "mixamorig:Spine1": wr("mixamorig:Spine1", pitch_fwd=-0.16 * env),
            "mixamorig:Head":   wr("mixamorig:Head",   pitch_fwd=-0.28 * env),
            "Cape1":            wr("Cape1",            pitch_fwd=0.45 + 0.55 * env),
            "Cape2":            wr("Cape2",            pitch_fwd=0.40 + 0.65 * env),
            "Cape3":            wr("Cape3",            pitch_fwd=0.35 + 0.65 * env),
        }
        l_a1, l_a2 = _arm_dirs_sagittal(+1.0, 0.35 + 1.15 * env + flutter, 0.35, 0.45 - 0.25 * env, inward=0.05)
        r_a1, r_a2 = _arm_dirs_sagittal(-1.0, 0.35 + 1.15 * env - flutter, 0.35, 0.45 - 0.25 * env, inward=0.05)
        set_arms(d, l_a1, l_a2, r_a1, r_a2)
        l_l1, l_l2 = _leg_dirs_sagittal(+1.0, 0.50 - 0.68 * env + flutter, 0.80 - 0.45 * env, abduct=0.06)
        r_l1, r_l2 = _leg_dirs_sagittal(-1.0, 0.38 - 0.54 * env - flutter, 0.70 - 0.35 * env, abduct=0.06)
        set_legs(d, l_l1, l_l2, r_l1, r_l2)
        return h_loc, d

    eval_clip("Airborne", 1.60, frame_airborne)

    # 10. Tripped (3.60s: 0.12..1.10 stumble/catch -> 1.35..1.85 flat belly slide -> 1.85..3.50 push-up get-up!)
    def frame_tripped(t_s: float, _ph: float) -> tuple[tuple[float, float, float], dict[str, Quaternion]]:
        _, idle_r = frame_idle(0.0, 0.0)
        stumble_r: dict[str, Quaternion] = {
            "mixamorig:Hips":  wr("mixamorig:Hips",  pitch_fwd=0.68, yaw_left=0.10),
            "mixamorig:Spine": wr("mixamorig:Spine", pitch_fwd=0.32, yaw_left=-0.08),
            "mixamorig:Head":  wr("mixamorig:Head",  pitch_fwd=-0.36),
            "Cape1":           wr("Cape1",           pitch_fwd=0.85),
            "Cape2":           wr("Cape2",           pitch_fwd=0.95),
        }
        set_arms(stumble_r, *_arm_dirs_sagittal(+1.0, 1.38, 0.32, 0.35), *_arm_dirs_sagittal(-1.0, 1.44, 0.32, 0.35))
        set_legs(stumble_r, *_leg_dirs_sagittal(+1.0, 0.55, 0.65), *_leg_dirs_sagittal(-1.0, -0.45, 0.52))

        belly_r: dict[str, Quaternion] = {
            "mixamorig:Hips":   wr("mixamorig:Hips",   pitch_fwd=1.46),
            "mixamorig:Spine":  wr("mixamorig:Spine",  pitch_fwd=-0.15),
            "mixamorig:Spine1": wr("mixamorig:Spine1", pitch_fwd=-0.15),
            "mixamorig:Head":   wr("mixamorig:Head",   pitch_fwd=-0.52),
            "Cape1":            wr("Cape1",            pitch_fwd=0.18),
            "Cape2":            wr("Cape2",            pitch_fwd=0.15),
        }
        set_arms(belly_r, *_arm_dirs_sagittal(+1.0, 1.68, 0.28, 0.25), *_arm_dirs_sagittal(-1.0, 1.68, 0.28, 0.25))
        set_legs(belly_r, *_leg_dirs_sagittal(+1.0, -0.10, 0.18, 0.08), *_leg_dirs_sagittal(-1.0, -0.10, 0.18, 0.08))

        pushup_r: dict[str, Quaternion] = {
            "mixamorig:Hips":  wr("mixamorig:Hips",  pitch_fwd=0.72),
            "mixamorig:Spine": wr("mixamorig:Spine", pitch_fwd=-0.22),
            "mixamorig:Head":  wr("mixamorig:Head",  pitch_fwd=-0.28),
        }
        set_arms(pushup_r, *_arm_dirs_sagittal(+1.0, 0.78, 0.35, 0.92), *_arm_dirs_sagittal(-1.0, 0.78, 0.35, 0.92))
        set_legs(pushup_r, *_leg_dirs_sagittal(+1.0, 1.12, 1.32), *_leg_dirs_sagittal(-1.0, 0.35, 0.85))

        def blend_dicts(a: dict[str, Quaternion], b: dict[str, Quaternion], u_raw: float) -> dict[str, Quaternion]:
            u = max(0.0, min(1.0, u_raw))
            sm = u * u * (3.0 - 2.0 * u)
            keys = set(idle_r) | set(a) | set(b)
            return {
                k: a.get(k, idle_r.get(k, Quaternion((1, 0, 0, 0)))).slerp(b.get(k, idle_r.get(k, Quaternion((1, 0, 0, 0)))), sm)
                for k in keys
            }

        if t_s < 0.70:
            u = t_s / 0.70
            return (0.0, -0.18 * u, 0.14 * u), blend_dicts(idle_r, stumble_r, u)
        elif t_s < 1.35:
            u = (t_s - 0.70) / 0.65
            return (0.0, -0.18 - 0.56 * u, 0.14 + 0.16 * u), blend_dicts(stumble_r, belly_r, u)
        elif t_s <= 1.85:
            return (0.0, -0.74, 0.30), blend_dicts(belly_r, belly_r, 1.0)
        elif t_s <= 2.65:
            u = (t_s - 1.85) / 0.80
            return (0.0, -0.74 + 0.42 * u, 0.30 - 0.16 * u), blend_dicts(belly_r, pushup_r, u)
        else:
            u = min(1.0, (t_s - 2.65) / 0.85)
            return (0.0, -0.32 * (1.0 - u) - 0.012 * u, 0.14 * (1.0 - u)), blend_dicts(pushup_r, idle_r, u)

    eval_clip("Tripped", 3.60, frame_tripped)

    # 11. crouch (1.40s, exact loop: low stealthy street crouch)
    def frame_crouch(_t: float, ph: float) -> tuple[tuple[float, float, float], dict[str, Quaternion]]:
        w = 2.0 * math.pi * ph
        sw = math.sin(w)
        h_loc = (0.010 * sw, -0.34 + 0.012 * math.cos(w), 0.02)
        d: dict[str, Quaternion] = {
            "mixamorig:Hips":      wr("mixamorig:Hips",   pitch_fwd=0.28, yaw_left=0.06 * sw),
            "mixamorig:Spine":     wr("mixamorig:Spine",  pitch_fwd=0.22, yaw_left=-0.04 * sw),
            "mixamorig:Spine1":    wr("mixamorig:Spine1", pitch_fwd=0.12),
            "mixamorig:Head":      wr("mixamorig:Head",   pitch_fwd=-0.46, yaw_left=0.10 * sw),
            "mixamorig:LeftFoot":  wr("mixamorig:LeftFoot",  pitch_fwd=-0.38),
            "mixamorig:RightFoot": wr("mixamorig:RightFoot", pitch_fwd=-0.38),
        }
        set_arms(d, *_arm_dirs_sagittal(+1.0, 0.42, 0.32, 0.92), *_arm_dirs_sagittal(-1.0, 0.42, 0.32, 0.92))
        set_legs(d, *_leg_dirs_sagittal(+1.0, 0.92, 1.52, 0.08), *_leg_dirs_sagittal(-1.0, 0.92, 1.52, 0.08))
        return h_loc, d

    eval_clip("crouch", 1.40, frame_crouch)

    # 12. jump (0.90s: anticipation crouch -> upward launch -> tucked apex -> landing absorption)
    def frame_jump(_t: float, ph: float) -> tuple[tuple[float, float, float], dict[str, Quaternion]]:
        arc = math.sin(ph * math.pi)
        dip = math.sin(ph * 2.0 * math.pi) if ph < 0.2 else 0.0
        h_loc = (0.0, -0.12 * max(0.0, dip) + 0.22 * arc, 0.0)
        d: dict[str, Quaternion] = {
            "mixamorig:Hips":  wr("mixamorig:Hips",  pitch_fwd=0.12 * (1.0 - arc)),
            "mixamorig:Spine": wr("mixamorig:Spine", pitch_fwd=-0.12 * arc),
            "mixamorig:Head":  wr("mixamorig:Head",  pitch_fwd=-0.16 * arc),
            "Cape1":           wr("Cape1",           pitch_fwd=0.25 + 0.65 * arc),
            "Cape2":           wr("Cape2",           pitch_fwd=0.30 + 0.75 * arc),
        }
        set_arms(d, *_arm_dirs_sagittal(+1.0, 1.28 * arc, 0.35, 0.42 + 0.30 * arc), *_arm_dirs_sagittal(-1.0, 1.28 * arc, 0.35, 0.42 + 0.30 * arc))
        set_legs(d, *_leg_dirs_sagittal(+1.0, 0.85 * arc, 1.15 * arc, 0.05), *_leg_dirs_sagittal(-1.0, 0.65 * arc, 0.95 * arc, 0.05))
        return h_loc, d

    eval_clip("jump", 0.90, frame_jump)

    # 13. slide (1.00s: low street baseball slide — lead right leg extended, left knee tucked)
    def frame_slide(_t: float, ph: float) -> tuple[tuple[float, float, float], dict[str, Quaternion]]:
        env = math.sin(ph * math.pi) ** 0.65
        h_loc = (0.0, -0.52 * env, 0.14 * env)
        d: dict[str, Quaternion] = {
            "mixamorig:Hips":  wr("mixamorig:Hips",  pitch_fwd=-0.46 * env, yaw_left=0.20 * env, tilt_left=-0.22 * env),
            "mixamorig:Spine": wr("mixamorig:Spine", pitch_fwd=0.30 * env, yaw_left=-0.14 * env, tilt_left=0.12 * env),
            "mixamorig:Head":  wr("mixamorig:Head",  pitch_fwd=0.20 * env),
            "Cape1":           wr("Cape1",           pitch_fwd=0.75 * env),
            "Cape2":           wr("Cape2",           pitch_fwd=0.85 * env),
        }
        set_arms(d, *_arm_dirs_sagittal(+1.0, -0.35 * env, 0.42, 0.65), *_arm_dirs_sagittal(-1.0, 0.82 * env, 0.38, 0.55))
        set_legs(d, *_leg_dirs_sagittal(+1.0, 0.55 * env, 1.52 * env, 0.25 * env), *_leg_dirs_sagittal(-1.0, 1.10 * env, 0.18, 0.08))
        return h_loc, d

    eval_clip("slide", 1.00, frame_slide)

    # 14. trip (1.50s: quick forward stumble & recovery)
    def frame_quick_trip(_t: float, ph: float) -> tuple[tuple[float, float, float], dict[str, Quaternion]]:
        env = math.sin(ph * math.pi)
        h_loc = (0.0, -0.22 * env, 0.16 * env)
        d: dict[str, Quaternion] = {
            "mixamorig:Hips":  wr("mixamorig:Hips",  pitch_fwd=0.52 * env, yaw_left=0.12 * math.sin(ph * 4.0 * math.pi)),
            "mixamorig:Spine": wr("mixamorig:Spine", pitch_fwd=0.30 * env),
            "mixamorig:Head":  wr("mixamorig:Head",  pitch_fwd=-0.34 * env),
        }
        set_arms(d, *_arm_dirs_sagittal(+1.0, 1.28 * env, 0.36, 0.35), *_arm_dirs_sagittal(-1.0, 1.28 * env, 0.36, 0.35))
        set_legs(d, *_leg_dirs_sagittal(+1.0, 0.68 * env, 0.85 * env), *_leg_dirs_sagittal(-1.0, -0.35 * env, 0.45 * env))
        return h_loc, d

    eval_clip("trip", 1.50, frame_quick_trip)

    # 15-18. Snappy Cast / Throw Clips: throw, cast_release_right, cast_release_left, cast_release_twohand
    def make_hand_cast(mode: str) -> Callable[[float, float], tuple[tuple[float, float, float], dict[str, Quaternion]]]:
        def _fn(_t: float, ph: float) -> tuple[tuple[float, float, float], dict[str, Quaternion]]:
            if ph < 0.28:
                s = -(ph / 0.28)
            elif ph < 0.55:
                s = -1.0 + 2.0 * ((ph - 0.28) / 0.27)
            else:
                s = 1.0 - ((ph - 0.55) / 0.45)
            twist = 0.34 * s if mode == "right" else (-0.34 * s if mode == "left" else 0.0)
            l_swing = (1.15 * s if mode in ("left", "two") else -0.35 * s)
            r_swing = (1.15 * s if mode in ("right", "two") else -0.35 * s)
            h_loc = (0.0, -0.04 * abs(s), 0.06 * max(0.0, s))
            d: dict[str, Quaternion] = {
                "mixamorig:Hips":   wr("mixamorig:Hips",   pitch_fwd=0.14 * max(0.0, s), yaw_left=twist),
                "mixamorig:Spine":  wr("mixamorig:Spine",  pitch_fwd=0.18 * max(0.0, s), yaw_left=twist * 1.1),
                "mixamorig:Spine1": wr("mixamorig:Spine1", pitch_fwd=0.12 * max(0.0, s), yaw_left=twist * 1.1),
                "mixamorig:Head":   wr("mixamorig:Head",   pitch_fwd=-0.10 * max(0.0, s), yaw_left=-twist * 0.8),
                "Cape1":            wr("Cape1",            pitch_fwd=0.35 + 0.45 * abs(s)),
                "Cape2":            wr("Cape2",            pitch_fwd=0.40 + 0.50 * abs(s)),
            }
            l_a1, l_a2 = _arm_dirs_sagittal(+1.0, 0.35 + l_swing, 0.35 if mode in ("left", "two") else 0.26, 0.65 * (1.0 - max(0.0, s)))
            r_a1, r_a2 = _arm_dirs_sagittal(-1.0, 0.35 + r_swing, 0.35 if mode in ("right", "two") else 0.26, 0.65 * (1.0 - max(0.0, s)))
            set_arms(d, l_a1, l_a2, r_a1, r_a2)
            set_legs(d, *_leg_dirs_sagittal(+1.0, 0.28, 0.35, 0.05), *_leg_dirs_sagittal(-1.0, -0.15, 0.25, 0.05))
            return h_loc, d
        return _fn

    eval_clip("throw", 1.00, make_hand_cast("right"))
    eval_clip("cast_release_right", 1.00, make_hand_cast("right"))
    eval_clip("cast_release_left", 1.00, make_hand_cast("left"))
    eval_clip("cast_release_twohand", 1.10, make_hand_cast("two"))

    # 19. hit (0.60s: crisp flinch knockback & recovery)
    def frame_hit(_t: float, ph: float) -> tuple[tuple[float, float, float], dict[str, Quaternion]]:
        imp = math.sin(ph * math.pi) ** 0.7
        h_loc = (0.0, -0.05 * imp, -0.08 * imp)
        d: dict[str, Quaternion] = {
            "mixamorig:Hips":  wr("mixamorig:Hips",  pitch_fwd=-0.16 * imp, yaw_left=-0.14 * imp),
            "mixamorig:Spine": wr("mixamorig:Spine", pitch_fwd=-0.26 * imp, yaw_left=-0.14 * imp),
            "mixamorig:Head":  wr("mixamorig:Head",  pitch_fwd=-0.28 * imp, yaw_left=0.12 * imp, tilt_left=0.12 * imp),
        }
        set_arms(d, *_arm_dirs_sagittal(+1.0, 0.55 * imp, 0.42, 1.05 * imp), *_arm_dirs_sagittal(-1.0, 0.55 * imp, 0.42, 1.05 * imp))
        set_legs(d, *_leg_dirs_sagittal(+1.0, 0.22 * imp, 0.38 * imp, 0.05), *_leg_dirs_sagittal(-1.0, 0.22 * imp, 0.38 * imp, 0.05))
        return h_loc, d

    eval_clip("hit", 0.60, frame_hit)

    # 20. victory (2.00s: joyful 90s Filipino kid celebration; Junjun superhero fist-to-sky, Kiko slipper-hands cheer!)
    def frame_victory(_t: float, ph: float) -> tuple[tuple[float, float, float], dict[str, Quaternion]]:
        hop = abs(math.sin(ph * 2.0 * math.pi))
        pump = math.sin(ph * 4.0 * math.pi)
        h_loc = (0.0, 0.09 * hop, 0.0)
        d: dict[str, Quaternion] = {
            "mixamorig:Hips":  wr("mixamorig:Hips",  pitch_fwd=-0.06 * hop, yaw_left=0.12 * pump),
            "mixamorig:Spine": wr("mixamorig:Spine", pitch_fwd=-0.12, yaw_left=0.08 * pump),
            "mixamorig:Head":  wr("mixamorig:Head",  pitch_fwd=-0.14, yaw_left=-0.10 * pump, tilt_left=0.06 * pump),
            "Cape1":           wr("Cape1",           pitch_fwd=0.42 + 0.25 * hop, yaw_left=0.10 * pump),
            "Cape2":           wr("Cape2",           pitch_fwd=0.48 + 0.30 * hop, yaw_left=0.12 * pump),
            "Cape3":           wr("Cape3",           pitch_fwd=0.52 + 0.35 * hop, yaw_left=0.14 * pump),
        }
        if is_junjun:
            # Left hand proud on hip, Right fist raised high into the sky!
            l_a1, l_a2 = (+0.68, +0.08, -0.72), (-0.65, -0.35, -0.66)
        else:
            l_a1, l_a2 = (+0.42, -0.25, +0.86 + 0.12 * pump), (+0.22, -0.18, +0.96)
        r_a1, r_a2 = (-0.38, -0.25, +0.88 + 0.12 * pump), (-0.18, -0.15, +0.97)
        set_arms(d, l_a1, l_a2, r_a1, r_a2)
        set_legs(d, *_leg_dirs_sagittal(+1.0, 0.35 * hop, 0.55 * hop, 0.08), *_leg_dirs_sagittal(-1.0, 0.22 * hop, 0.38 * hop, 0.08))
        return h_loc, d

    eval_clip("victory", 2.00, frame_victory)

    # Clear active action so glTF exporter reads strictly from our 20 clean NLA tracks and leaves rest pose pure!
    arm_obj.animation_data.action = None
    for pb in arm_obj.pose.bones:
        pb.location = (0.0, 0.0, 0.0)
        pb.rotation_quaternion = Quaternion((1.0, 0.0, 0.0, 0.0))
        pb.scale = (1.0, 1.0, 1.0)
    bpy.context.view_layer.update()


# ==============================================================================
# 11. GLB EXPORT + CANONICAL IMAGE/MATERIAL POST-PROCESSING (<id>_0, _1, _2.png)
# ==============================================================================
def export_and_canonicalize_glb(
    kid_id: str,
    glb_path: Path,
    orm_png: bytes,
    albedo_png: bytes,
    normal_png: bytes,
) -> dict[str, Any]:
    """
    Exports `<id>.glb` and canonicalizes the glTF JSON + BIN buffer so:
      1. `materials[0]` is named `"Original"` with `metallicFactor=0`, `roughnessFactor=1`, `normalTexture.scale=0.85`
      2. `images` has EXACTLY 3 unnamed PNG entries in exact Godot sidecar order:
           index 0 -> ORM    (`<id>_0.png`)
           index 1 -> Albedo (`<id>_1.png`)
           index 2 -> Normal (`<id>_2.png`, matching `<id>_2.png.import` `compress/normal_map=1`!)
      3. Writes `<id>_0.png`, `<id>_1.png`, `<id>_2.png` directly to disk while keeping existing `.import` files untouched!
    """
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        export_yup=True,
        export_apply=False,
        export_texcoords=True,
        export_normals=True,
        export_tangents=True,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_skins=True,
        export_all_influences=False,
        export_animations=True,
        export_animation_mode="NLA_TRACKS",
        export_anim_slide_to_zero=True,
        export_optimize_animation_size=False,
        export_optimize_animation_keep_anim_armature=True,
    )

    raw = glb_path.read_bytes()
    json_len = struct.unpack_from("<I", raw, 12)[0]
    gltf = json.loads(raw[20 : 20 + json_len].decode("utf-8"))
    bin_offset = 20 + json_len
    bin_len = struct.unpack_from("<I", raw, bin_offset)[0]
    bin_data = bytearray(raw[bin_offset + 8 : bin_offset + 8 + bin_len])

    # Append our 3 exact 1024x1024 PNG buffers cleanly at 4-byte aligned offsets
    new_bvs: list[int] = []
    for png_bytes in (orm_png, albedo_png, normal_png):
        while len(bin_data) % 4 != 0:
            bin_data.append(0)
        off = len(bin_data)
        bin_data.extend(png_bytes)
        bv_idx = len(gltf["bufferViews"])
        gltf["bufferViews"].append({"buffer": 0, "byteOffset": off, "byteLength": len(png_bytes)})
        new_bvs.append(bv_idx)

    while len(bin_data) % 4 != 0:
        bin_data.append(0)
    gltf["buffers"][0]["byteLength"] = len(bin_data)

    # Canonicalize `images`, `textures`, and `materials`! (ZERO `"name"` key on images!)
    gltf["images"] = [
        {"mimeType": "image/png", "bufferView": new_bvs[0]},  # 0 -> <id>_0.png (ORM)
        {"mimeType": "image/png", "bufferView": new_bvs[1]},  # 1 -> <id>_1.png (Albedo)
        {"mimeType": "image/png", "bufferView": new_bvs[2]},  # 2 -> <id>_2.png (Normal)
    ]
    sampler_idx = 0 if gltf.get("samplers") else None
    gltf["textures"] = [
        ({"sampler": sampler_idx, "source": i} if sampler_idx is not None else {"source": i})
        for i in range(3)
    ]
    gltf["materials"] = [
        {
            "name": "Original",
            "doubleSided": True,
            "pbrMetallicRoughness": {
                "baseColorFactor": [1.0, 1.0, 1.0, 1.0],
                "metallicFactor": 0.0,
                "roughnessFactor": 1.0,
                "metallicRoughnessTexture": {"index": 0, "texCoord": 0},
                "baseColorTexture": {"index": 1, "texCoord": 0},
            },
            "normalTexture": {"index": 2, "texCoord": 0, "scale": 0.85},
            "occlusionTexture": {"index": 0, "texCoord": 0, "strength": 0.85},
        }
    ]
    for m in gltf.get("meshes", []):
        for prim in m.get("primitives", []):
            prim["material"] = 0

    # `STATIC_FINGERS` contract (`test_authored_kids.gd:181-196`):
    # `ligaya` and `migo` keep their clean modelled hand pose with 0 finger animation tracks in the GLB!
    if kid_id in ("ligaya", "migo"):
        finger_tokens = ("HandThumb", "HandIndex", "HandMiddle", "HandRing", "HandPinky")
        finger_nodes = {
            idx
            for idx, nd in enumerate(gltf.get("nodes", []))
            if any(tok in nd.get("name", "") for tok in finger_tokens)
        }
        for anim in gltf.get("animations", []):
            anim["channels"] = [
                ch for ch in anim.get("channels", [])
                if ch.get("target", {}).get("node") not in finger_nodes
            ]

    json_bytes = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    while len(json_bytes) % 4 != 0:
        json_bytes += b" "

    total_len = 12 + 8 + len(json_bytes) + 8 + len(bin_data)
    out = bytearray()
    out.extend(struct.pack("<III", 0x46546C67, 2, total_len))
    out.extend(struct.pack("<II", len(json_bytes), 0x4E4F534A))
    out.extend(json_bytes)
    out.extend(struct.pack("<II", len(bin_data), 0x004E4942))
    out.extend(bin_data)
    glb_path.write_bytes(bytes(out))

    # Write matching extracted sidecar PNGs directly so disk files match embedded images 1:1
    (CHAR_DIR / f"{kid_id}_0.png").write_bytes(orm_png)
    (CHAR_DIR / f"{kid_id}_1.png").write_bytes(albedo_png)
    (CHAR_DIR / f"{kid_id}_2.png").write_bytes(normal_png)

    # Preserve Godot's [remap]/[deps] headers on existing .import sidecars while enforcing VRAM compression!
    for slot_idx in (0, 1, 2):
        imp_p = CHAR_DIR / f"{kid_id}_{slot_idx}.png.import"
        if imp_p.exists():
            lines_imp = imp_p.read_text(encoding="utf-8").splitlines()
            norm_val = "1" if slot_idx == 2 else "0"
            patched: list[str] = []
            for ln in lines_imp:
                if ln.startswith("compress/mode="):
                    patched.append("compress/mode=2")
                elif ln.startswith("compress/normal_map="):
                    patched.append(f"compress/normal_map={norm_val}")
                elif ln.startswith("mipmaps/generate="):
                    patched.append("mipmaps/generate=true")
                elif ln.startswith("detect_3d/compress_to="):
                    patched.append("detect_3d/compress_to=0")
                else:
                    patched.append(ln)
            imp_p.write_text("\n".join(patched) + "\n", encoding="utf-8")

    return {
        "kid": kid_id,
        "glb_kb": round(len(out) / 1024.0, 1),
        "anims": [a["name"] for a in gltf.get("animations", [])],
        "joints": len(gltf["skins"][0]["joints"]) if gltf.get("skins") else 0,
    }


# ==============================================================================
# 12. STUDIO CYCLES RIM-LIT PORTRAITS, CHARACTER CARDS & AUDIT TURNAROUNDS
# ==============================================================================
def _setup_valorant_studio_lights(scene: bpy.types.Scene, slash_rgb: tuple[int, int, int]) -> list[bpy.types.Object]:
    """Sets up balanced Manila golden-hour key + cool sky fill + Valorant rim light (ZERO white blowout!)."""
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 28
    scene.cycles.use_denoising = False
    scene.render.film_transparent = True
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "Medium Contrast"

    world = bpy.data.worlds.new("StudioWorld")
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.15, 0.16, 0.21, 1.0)
        bg.inputs[1].default_value = 0.28
    scene.world = world

    created: list[bpy.types.Object] = []

    def add_area(nm: str, loc: tuple[float, float, float], rot_deg: tuple[float, float, float], energy: float, size: float, col: tuple[float, float, float]) -> None:
        ld = bpy.data.lights.new(nm, type="AREA")
        ld.energy = energy
        ld.size = size
        ld.color = col
        lo = bpy.data.objects.new(nm, ld)
        lo.location = loc
        lo.rotation_euler = tuple(math.radians(a) for a in rot_deg)
        scene.collection.objects.link(lo)
        created.append(lo)

    # Balanced warm sunlit key from front-left-above (zero highlight blowout on warm cotton!)
    add_area("KeyLight",  (-1.35, -1.95, 2.25), (58, 0, -32), 52.0, 1.8, (1.00, 0.92, 0.82))
    # Soft sky-blue fill from front-right
    add_area("FillLight", (+1.65, -1.65, 1.45), (72, 0, +42),  26.0, 2.1, (0.78, 0.88, 1.00))
    # Signature sharp Valorant colored rim light from back-left & back-right!
    rc = (slash_rgb[0] / 255.0, slash_rgb[1] / 255.0, slash_rgb[2] / 255.0)
    add_area("RimLeft",   (+1.25, +1.55, 1.95), (-62, 0, 142), 78.0, 1.1, (min(1.0, rc[0] * 0.65 + 0.35), min(1.0, rc[1] * 0.65 + 0.35), min(1.0, rc[2] * 0.65 + 0.35)))
    add_area("RimRight",  (-1.25, +1.45, 1.65), (-68, 0, -142), 54.0, 1.1, (0.52, 0.86, 1.00))
    return created


def _point_camera_at(cam_obj: bpy.types.Object, eye: tuple[float, float, float], target: tuple[float, float, float]) -> None:
    cam_obj.location = Vector(eye)
    direction = Vector(target) - Vector(eye)
    cam_obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def _apply_expressive_hero_pose(kid_id: str, arm_obj: bpy.types.Object) -> None:
    """Evaluates frame 0 of the character's bespoke Idle action so 2D Portraits, Cards & 3D Idle match 1:1!"""
    if arm_obj.animation_data is not None:
        idle_act = bpy.data.actions.get(f"{kid_id}_Idle") or bpy.data.actions.get("Idle")
        for tr in arm_obj.animation_data.nla_tracks:
            if tr.name == "Idle" and len(tr.strips) > 0:
                idle_act = tr.strips[0].action
                break
        arm_obj.animation_data.use_nla = False
        for tr in arm_obj.animation_data.nla_tracks:
            tr.mute = True
        if idle_act is not None:
            arm_obj.animation_data.action = idle_act
            if hasattr(idle_act, "slots") and len(idle_act.slots) > 0:
                arm_obj.animation_data.action_slot = idle_act.slots[0]
            bpy.context.scene.frame_set(0)
            bpy.context.view_layer.update()
            return
    bpy.context.view_layer.update()


def _compose_valorant_card_backdrop(
    fg_rgba: Image.Image,
    bg_rgb: tuple[int, int, int],
    slash_rgb: tuple[int, int, int],
    rounded_alpha_corners: bool = False,
) -> Image.Image:
    """Composes a bold Valorant graphic diagonal-slash card behind the crisp rim-lit 3D render!"""
    w, h = fg_rgba.size
    card = Image.new("RGBA", (w, h), (bg_rgb[0], bg_rgb[1], bg_rgb[2], 255))
    draw = ImageDraw.Draw(card)
    # Deep subtle vertical gradient
    hi_bg = (min(255, bg_rgb[0] + 24), min(255, bg_rgb[1] + 26), min(255, bg_rgb[2] + 34), 255)
    draw.polygon([(0, 0), (w, 0), (w, int(h * 0.55)), (0, int(h * 0.35))], fill=hi_bg)
    # Bold Valorant diagonal graphic slashes!
    s_main = (slash_rgb[0], slash_rgb[1], slash_rgb[2], 235)
    s_acc = (min(255, slash_rgb[0] + 45), min(255, slash_rgb[1] + 45), min(255, slash_rgb[2] + 45), 185)
    draw.polygon([(int(w * 0.62), 0), (int(w * 0.94), 0), (int(w * 0.36), h), (int(w * 0.04), h)], fill=s_main)
    draw.polygon([(int(w * 0.98), int(h * 0.08)), (w, int(h * 0.08)), (int(w * 0.52), h), (int(w * 0.44), h)], fill=s_acc)
    # Subtle crisp geometric border frame
    draw.rectangle([8, 8, w - 9, h - 9], outline=(255, 255, 255, 58), width=2)

    card = Image.alpha_composite(card, fg_rgba)

    if rounded_alpha_corners:
        # Rounded corner mask (radius=22px) so outer 6x6 corners have alpha == 0 (required by test_character_card_pictures.gd!)
        mask = Image.new("L", (w, h), 0)
        mdraw = ImageDraw.Draw(mask)
        mdraw.rounded_rectangle([0, 0, w - 1, h - 1], radius=24, fill=255)
        r, g, b, _ = card.split()
        card = Image.merge("RGBA", (r, g, b, mask))
    return card


def render_raw_ui_frames_blender(kid_id: str, spec: dict[str, Any], arm_obj: bpy.types.Object, scratch_dir: Path) -> None:
    """Runs inside Blender: renders crisp transparent RGBA Cycles frames for Portrait, Face & Full Card."""
    scene = bpy.context.scene
    lights = _setup_valorant_studio_lights(scene, spec["card_slash"])
    _apply_expressive_hero_pose(kid_id, arm_obj)

    cam_data = bpy.data.cameras.new("StudioCam")
    cam_obj = bpy.data.objects.new("StudioCam", cam_data)
    scene.collection.objects.link(cam_obj)
    scene.camera = cam_obj

    # Bust Portrait (600x800 RGBA, eye-level 76mm portrait lens framed for ~1:5.4 Valorant kid head!)
    cam_data.lens = 76.0
    if kid_id == "kiko":
        _point_camera_at(cam_obj, (-0.12, -1.76, 1.35), (0.0, 0.0, 1.29))
    else:
        _point_camera_at(cam_obj, (-0.14, -1.52, 1.46), (0.0, 0.0, 1.41))
    scene.render.resolution_x = 600
    scene.render.resolution_y = 800
    scene.render.filepath = str(scratch_dir / f"_raw_bust_{kid_id}.png")
    bpy.ops.render.render(write_still=True)

    # Minimap Face (192x192 -> downsampled to 96x96 in Host PIL)
    cam_data.lens = 86.0
    _point_camera_at(cam_obj, (-0.08, -1.14, 1.50), (0.0, 0.0, 1.48))
    scene.render.resolution_x = 192
    scene.render.resolution_y = 192
    scene.render.filepath = str(scratch_dir / f"_raw_face_{kid_id}.png")
    bpy.ops.render.render(write_still=True)

    # Full-Body Valorant Character Card (650x998, clean 64mm telephoto at chest height!)
    cam_data.lens = 64.0
    _point_camera_at(cam_obj, (-0.24, -3.58, 0.96), (0.0, 0.0, 0.85))
    scene.render.resolution_x = 650
    scene.render.resolution_y = 998
    scene.render.filepath = str(scratch_dir / f"_raw_full_{kid_id}.png")
    bpy.ops.render.render(write_still=True)

    bpy.data.objects.remove(cam_obj, do_unlink=True)
    bpy.data.cameras.remove(cam_data, do_unlink=True)
    for lo in lights:
        ld = lo.data
        bpy.data.objects.remove(lo, do_unlink=True)
        bpy.data.lights.remove(ld, do_unlink=True)


def render_raw_audit_frames_blender(kid_id: str, spec: dict[str, Any], arm_obj: bpy.types.Object, out_dir: Path) -> None:
    """Runs inside Blender: renders 3 Cycles turnaround views (Front, 3/4, Back) to out_dir."""
    scene = bpy.context.scene
    lights = _setup_valorant_studio_lights(scene, spec["card_slash"])
    cam_data = bpy.data.cameras.new("AuditCam")
    cam_data.lens = 64.0
    cam_obj = bpy.data.objects.new("AuditCam", cam_data)
    scene.collection.objects.link(cam_obj)
    scene.camera = cam_obj
    scene.render.resolution_x = 420
    scene.render.resolution_y = 680

    _apply_expressive_hero_pose(kid_id, arm_obj)
    for v_idx, (eye, tgt) in enumerate([
        ((0.0, -3.56, 0.96), (0.0, 0.0, 0.86)),
        ((+1.42, -3.26, 1.00), (0.0, 0.0, 0.87)),
        ((-0.98, +3.42, 0.96), (0.0, 0.0, 0.86)),
    ]):
        _point_camera_at(cam_obj, eye, tgt)
        scene.render.filepath = str(out_dir / f"_aud_{kid_id}_{v_idx}.png")
        bpy.ops.render.render(write_still=True)

    bpy.data.objects.remove(cam_obj, do_unlink=True)
    bpy.data.cameras.remove(cam_data, do_unlink=True)
    for lo in lights:
        ld = lo.data
        bpy.data.objects.remove(lo, do_unlink=True)
        bpy.data.lights.remove(ld, do_unlink=True)


def compose_post_renders_pil(kid_id: str, spec: dict[str, Any], audit_dir: Path | None, render_ui: bool, scratch_dir: Path) -> None:
    """Runs in Host Python (with PIL): composites 3-view audit sheet + Portraits, Cards & Minimap Faces!"""
    if audit_dir is not None:
        views: list[Image.Image] = []
        for v_idx in range(3):
            p_tmp = audit_dir / f"_aud_{kid_id}_{v_idx}.png"
            im = Image.open(p_tmp).convert("RGBA")
            bg = Image.new("RGBA", im.size, (232, 229, 222, 255))
            views.append(Image.alpha_composite(bg, im))
            try:
                p_tmp.unlink()
            except OSError:
                pass
        sheet = Image.new("RGB", (420 * 3, 680), (232, 229, 222))
        for i, vw in enumerate(views):
            sheet.paste(vw.convert("RGB"), (i * 420, 0))
        dr = ImageDraw.Draw(sheet)
        dr.text((18, 16), f"{kid_id.upper()} - {spec['title']}", fill=(28, 28, 34), font=_load_bold_font(28))
        sheet.save(audit_dir / f"audit_{kid_id}.png", format="PNG")

    if render_ui:
        PORTRAIT_DIR.mkdir(parents=True, exist_ok=True)
        PORTRAIT_CARDS_DIR.mkdir(parents=True, exist_ok=True)
        PORTRAIT_FACES_DIR.mkdir(parents=True, exist_ok=True)
        UI_CARDS_DIR.mkdir(parents=True, exist_ok=True)

        bust_rgba = Image.open(scratch_dir / f"_raw_bust_{kid_id}.png").convert("RGBA")
        bust_rgba.save(PORTRAIT_DIR / f"{kid_id}.png", format="PNG")
        prof_card = _compose_valorant_card_backdrop(bust_rgba, spec["card_bg"], spec["card_slash"], rounded_alpha_corners=False)
        prof_card.convert("RGB").save(PORTRAIT_CARDS_DIR / f"{kid_id}.webp", format="WEBP", quality=90)

        face_rgba = Image.open(scratch_dir / f"_raw_face_{kid_id}.png").convert("RGBA").resize((96, 96), Image.Resampling.LANCZOS)
        face_rgba.save(PORTRAIT_FACES_DIR / f"{kid_id}.png", format="PNG")

        full_rgba = Image.open(scratch_dir / f"_raw_full_{kid_id}.png").convert("RGBA")
        full_card_hi = _compose_valorant_card_backdrop(full_rgba, spec["card_bg"], spec["card_slash"], rounded_alpha_corners=True)
        full_card_hi.save(UI_CARDS_DIR / f"{kid_id}_card.png", format="PNG")
        card_480_base = _compose_valorant_card_backdrop(full_rgba, spec["card_bg"], spec["card_slash"], rounded_alpha_corners=False)
        card_480 = card_480_base.resize((480, 737), Image.Resampling.LANCZOS)
        # Apply crisp radius=22 rounded alpha mask AFTER resize and save lossless alpha so outer 6x6 corners have alpha == 0!
        mask_480 = Image.new("L", (480, 737), 0)
        ImageDraw.Draw(mask_480).rounded_rectangle([0, 0, 479, 736], radius=22, fill=255)
        r4, g4, b4, _ = card_480.split()
        card_480 = Image.merge("RGBA", (r4, g4, b4, mask_480))
        card_480.save(UI_CARDS_DIR / f"{kid_id}.webp", format="WEBP", lossless=True)


# ==============================================================================
# 13. MASTER BUILD DRIVER (HOST PYTHON PIL + BLENDER 5.2 SUBPROCESS)
# ==============================================================================
def build_single_kid_blender(
    kid_id: str,
    render_audit_dir: Path | None,
    render_ui: bool,
    scratch_dir: Path,
) -> dict[str, Any]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    spec = KIDS_SPECS[kid_id]
    cb = CharBuilder(kid_id, spec)
    KID_BUILDERS[kid_id](cb, spec)
    apply_valorant_kid_proportions(cb, spec)

    norm_scale, z_min_raw = normalize_height_and_ground(cb, target_height=1.7000)
    zs = [v[2] for v in cb.verts]
    tri_count = len(cb.tris)
    assert 5500 <= tri_count <= 9000, f"{kid_id}: tri_count {tri_count} outside mandatory [5500, 9000]!"

    orm_png, albedo_png = _bake_vertex_ao_into_pngs(
        cb,
        scratch_dir / f"_base_{kid_id}_0.png",
        scratch_dir / f"_base_{kid_id}_1.png",
    )
    normal_png = (scratch_dir / f"_base_{kid_id}_2.png").read_bytes()

    arm_obj = create_mixamorig_armature(kid_id, spec, norm_scale, z_min_raw)
    mesh_obj = build_skinned_mesh_object(kid_id, cb, arm_obj, orm_png, albedo_png, normal_png)
    author_all_20_animations(kid_id, arm_obj)

    CHAR_DIR.mkdir(parents=True, exist_ok=True)
    glb_path = CHAR_DIR / f"{kid_id}.glb"
    meta = export_and_canonicalize_glb(kid_id, glb_path, orm_png, albedo_png, normal_png)
    meta["tris"] = tri_count
    meta["verts"] = len(cb.verts)
    meta["z_min"] = round(min(zs), 5)
    meta["z_max"] = round(max(zs), 5)

    if render_audit_dir is not None:
        render_audit_dir.mkdir(parents=True, exist_ok=True)
        render_raw_audit_frames_blender(kid_id, spec, arm_obj, render_audit_dir)
    if render_ui:
        render_raw_ui_frames_blender(kid_id, spec, arm_obj, scratch_dir)

    _ = mesh_obj
    return meta


def main() -> None:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else sys.argv[1:]
    parser = argparse.ArgumentParser(description="Build Valorant 90s Filipino Street Kids v3")
    parser.add_argument("--kids", default="all", help="Comma-separated kid ids or 'all'")
    parser.add_argument("--render-audit", default="", help="Directory for 3-view studio audit PNGs")
    parser.add_argument("--render-ui", action="store_true", help="Render portraits, faces & character cards")
    parser.add_argument("--scratch", default="", help="Scratch directory for temporary render buffers")
    parser.add_argument("--blender-worker", action="store_true", help="Internal flag when running inside Blender")
    args = parser.parse_args(argv)

    target_kids = ALL_KIDS if args.kids == "all" else [k.strip() for k in args.kids.split(",") if k.strip()]
    audit_dir = Path(args.render_audit) if args.render_audit else None
    scratch_dir = Path(args.scratch) if args.scratch else (audit_dir or (REPO_ROOT / ".godot" / "valorant_v3_scratch"))
    scratch_dir.mkdir(parents=True, exist_ok=True)

    if IN_BLENDER or args.blender_worker:
        results: list[dict[str, Any]] = []
        for kid_id in target_kids:
            info = build_single_kid_blender(kid_id, render_audit_dir=audit_dir, render_ui=args.render_ui, scratch_dir=scratch_dir)
            print(f"[VALORANT_V3] {kid_id:8s} | tris={info['tris']} | z=[{info['z_min']}, {info['z_max']}] | anims={len(info['anims'])} | glb={info['glb_kb']}KB", flush=True)
            results.append(info)
        summary_path = scratch_dir / "valorant_kids_v3_summary.json"
        summary_path.write_text(json.dumps(results, indent=2), encoding="utf-8")
        return

    # Host Python Orchestrator: Stage 1 (PIL Base Textures) -> Stage 2 (Blender 5.2) -> Stage 3 (PIL Cards/Audit)
    assert HAS_PIL, "Host Python requires Pillow (PIL)!"
    print(f"[HOST] Synthesizing 1024x1024 painterly Valorant base textures for {target_kids}...", flush=True)
    for kid_id in target_kids:
        synthesize_base_valorant_textures_pil(kid_id, KIDS_SPECS[kid_id], scratch_dir)

    cmd = [
        str(BLENDER_EXE),
        "--background",
        "--python",
        str(Path(__file__).resolve()),
        "--",
        "--blender-worker",
        "--kids",
        ",".join(target_kids),
        "--scratch",
        str(scratch_dir),
    ]
    if audit_dir is not None:
        cmd.extend(["--render-audit", str(audit_dir)])
    if args.render_ui:
        cmd.append("--render-ui")

    print(f"[HOST] Launching Blender 5.2 worker...", flush=True)
    subprocess.run(cmd, check=True)

    if audit_dir is not None or args.render_ui:
        print(f"[HOST] Compositing studio audit turnarounds & Valorant UI cards...", flush=True)
        for kid_id in target_kids:
            compose_post_renders_pil(kid_id, KIDS_SPECS[kid_id], audit_dir, args.render_ui, scratch_dir)
    print(f"[HOST] All {len(target_kids)} character(s) completed cleanly!", flush=True)


if __name__ == "__main__":
    main()
