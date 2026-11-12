"""
Build the complete Cellular-Detail 3D Provincial Filipino Barangay Environment & Prop Kit in Blender 5.2.

Art Benchmark: Valorant (readable silhouettes & painterly brush planes) x Arcane (hand-painted
gouache value shifts & grime/moss storytelling) x 90s Filipino Komiks (baked #1A1619 charcoal
ink panel/crevice contours on 3D geometry).

Outputs:
  - 1 shared 2048x2048 sunlit 90s province painterly atlas (`assets/models/barangay/BarangayAtlas.jpg`,
    mean_sRGB in [0.43, 0.49], max_sRGB <= 0.78, 0% <0.08 harsh black pixels, VRAM-compressed
    `compress/mode=2` via `BarangayAtlas.jpg.import`)
  - 18 single-draw-call architectural & street `.glb` models in `assets/models/barangay/` referencing
    `BarangayAtlas.jpg` (never touches `brgy_jeepney.glb` or `brgy_tricycle.glb`)

Run:
  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --python tools/blender/build_barangay_kit.py
"""

import hashlib
import json
import math
import os
import struct
import bpy
import bmesh
import numpy as np
from mathutils import Vector, Euler


OUT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "assets", "models", "barangay"))
ATLAS_NAME = "BarangayAtlas"
ATLAS_FILENAME = f"{ATLAS_NAME}.jpg"
ATLAS_SIZE = 2048
GRID = 8
CELL = ATLAS_SIZE // GRID  # 256x256 px per swatch
PAD_PX = 16                # 16 px mipmap bleed gutter around every 256x256 cell
PAD_UV = PAD_PX / float(CELL)

# Sunlit 90s province daytime clamping: warm espresso floor (#2A2024 >= 0.155 -> 0% <0.08 pixels after JPEG),
# pre-JPEG ceiling 0.595 keeps decoded JPEG max_sRGB <= 0.78 and mean_sRGB right inside [0.43, 0.49].
MAX_ALBEDO = 0.608
MIN_ALBEDO = 0.212
INK_RGB = np.array([0.222, 0.184, 0.196], dtype=np.float32)  # Warm sunlit espresso-umber contour ink


# Crisp 5x7 bitmap font for hand-painted 90s Philippine province signboards & pole notices
FONT_5X7 = {
    "A": ["01110", "10001", "10001", "11111", "10001", "10001", "10001"],
    "B": ["11110", "10001", "10001", "11110", "10001", "10001", "11110"],
    "C": ["01111", "10000", "10000", "10000", "10000", "10000", "01111"],
    "D": ["11110", "10001", "10001", "10001", "10001", "10001", "11110"],
    "E": ["11111", "10000", "10000", "11110", "10000", "10000", "11111"],
    "F": ["11111", "10000", "10000", "11110", "10000", "10000", "10000"],
    "G": ["01111", "10000", "10000", "10011", "10001", "10001", "01111"],
    "H": ["10001", "10001", "10001", "11111", "10001", "10001", "10001"],
    "I": ["01110", "00100", "00100", "00100", "00100", "00100", "01110"],
    "J": ["00111", "00010", "00010", "00010", "10010", "10010", "01100"],
    "K": ["10001", "10010", "10100", "11000", "10100", "10010", "10001"],
    "L": ["10000", "10000", "10000", "10000", "10000", "10000", "11111"],
    "M": ["10001", "11011", "10101", "10101", "10001", "10001", "10001"],
    "N": ["10001", "11001", "10101", "10011", "10001", "10001", "10001"],
    "O": ["01110", "10001", "10001", "10001", "10001", "10001", "01110"],
    "P": ["11110", "10001", "10001", "11110", "10000", "10000", "10000"],
    "R": ["11110", "10001", "10001", "11110", "10100", "10010", "10001"],
    "S": ["01111", "10000", "10000", "01110", "00001", "00001", "11110"],
    "T": ["11111", "00100", "00100", "00100", "00100", "00100", "00100"],
    "U": ["10001", "10001", "10001", "10001", "10001", "10001", "01110"],
    "V": ["10001", "10001", "10001", "10001", "10001", "01010", "00100"],
    "W": ["10001", "10001", "10001", "10101", "10101", "11011", "10001"],
    "Y": ["10001", "10001", "01010", "00100", "00100", "00100", "00100"],
    "Z": ["11111", "00001", "00010", "00100", "01000", "10000", "11111"],
    "0": ["01110", "10011", "10101", "10101", "11001", "10001", "01110"],
    "5": ["11111", "10000", "11110", "00001", "00001", "10001", "01110"],
    "9": ["01110", "10001", "10001", "01111", "00001", "00001", "01110"],
    "!": ["00100", "00100", "00100", "00100", "00100", "00000", "00100"],
    "/": ["00001", "00010", "00010", "00100", "01000", "01000", "10000"],
    "-": ["00000", "00000", "00000", "11111", "00000", "00000", "00000"],
    ".": ["00000", "00000", "00000", "00000", "00000", "01100", "01100"],
    "'": ["00100", "00100", "01000", "00000", "00000", "00000", "00000"],
    " ": ["00000", "00000", "00000", "00000", "00000", "00000", "00000"],
}


# 64 curated sunlit 90s provincial Pinoy Barangay swatches: name -> (row, col, base_rgb, kind, param)
SWATCHES = {
    # Row 0: Sunlit 40x20cm CHB hollow-block walls + flaking provincial paint + lumot moss + BAWAL UMIHI DITO
    "chb_raw_moss":       (0, 0, (0.50, 0.51, 0.48), "chb", {"moss": True, "plaster": 0.0, "rain": 0.45}),
    "chb_cream_moss":     (0, 1, (0.61, 0.57, 0.46), "chb", {"moss": True, "plaster": 0.62, "rain": 0.40}),
    "chb_salmon_moss":    (0, 2, (0.60, 0.39, 0.31), "chb", {"moss": True, "plaster": 0.62, "rain": 0.42}),
    "chb_mint_moss":      (0, 3, (0.31, 0.56, 0.53), "chb", {"moss": True, "plaster": 0.60, "rain": 0.42}),
    "chb_ochre_moss":     (0, 4, (0.62, 0.51, 0.29), "chb", {"moss": True, "plaster": 0.62, "rain": 0.40}),
    "chb_sky_moss":       (0, 5, (0.33, 0.52, 0.60), "chb", {"moss": True, "plaster": 0.60, "rain": 0.42}),
    "chb_bawal_umihi":    (0, 6, (0.58, 0.56, 0.50), "bawal_sign", {}),
    "chb_plaster_cracked":(0, 7, (0.60, 0.51, 0.36), "chb", {"moss": False, "plaster": 0.66, "rain": 0.38}),

    # Row 1: Sunlit concrete structural columns/aprons, Barberya sign & oxidized rusted GI (kalawang na yero)
    "concrete_moss":      (1, 0, (0.50, 0.51, 0.47), "concrete", {"moss": True, "rain": 0.45}),
    "concrete_pillar":    (1, 1, (0.57, 0.55, 0.51), "concrete", {"moss": False, "rain": 0.32}),
    "sign_barberya":      (1, 2, (0.60, 0.58, 0.53), "text_sign", {"l1": "BARBERYA", "l2": "GUPIT POGI", "fg": (0.20, 0.33, 0.54), "hdr": (0.58, 0.23, 0.21), "hdr_fg": (0.62, 0.59, 0.52)}),
    "yero_rust_heavy":    (1, 3, (0.52, 0.31, 0.21), "yero", {"rust": 0.78, "patch": False}),
    "yero_patchwork":     (1, 4, (0.54, 0.43, 0.35), "yero", {"rust": 0.56, "patch": True}),
    "yero_red_oxide":     (1, 5, (0.56, 0.29, 0.23), "yero", {"rust": 0.46, "patch": False}),
    "yero_zinc_weathered":(1, 6, (0.57, 0.58, 0.59), "yero", {"rust": 0.34, "patch": False}),
    "yero_green_faded":   (1, 7, (0.33, 0.50, 0.36), "yero", {"rust": 0.36, "patch": False}),

    # Row 2: Sunlit woven bamboo sawali, weathered plywood, coco-lumber & shanty planks
    "sawali_honey":       (2, 0, (0.60, 0.49, 0.31), "sawali", {"aged": False}),
    "sawali_aged":        (2, 1, (0.50, 0.40, 0.27), "sawali", {"aged": True}),
    "plywood_weathered":  (2, 2, (0.59, 0.50, 0.38), "plywood", {"peel": (0.46, 0.38, 0.29)}),
    "plywood_teal_peel":  (2, 3, (0.29, 0.54, 0.52), "plywood", {"peel": (0.51, 0.42, 0.31)}),
    "wood_coco":          (2, 4, (0.51, 0.36, 0.24), "wood", {"planks": 3}),
    "wood_dark":          (2, 5, (0.37, 0.27, 0.20), "wood", {"planks": 2}),
    "planks_shack":       (2, 6, (0.54, 0.44, 0.33), "wood", {"planks": 5}),
    "bamboo_pole":        (2, 7, (0.56, 0.51, 0.30), "bamboo", {}),

    # Row 3: Jalousie louvers, rusted iron rehas grills, doors, chipped barrio tiles, barber pole, burlap
    "jalousie_window":    (3, 0, (0.36, 0.49, 0.54), "jalousie", {}),
    "rehas_grill":        (3, 1, (0.41, 0.31, 0.26), "rehas", {"counter": False}),
    "sari_grill_counter": (3, 2, (0.39, 0.34, 0.31), "rehas", {"counter": True}),
    "door_provincial":    (3, 3, (0.48, 0.34, 0.24), "door", {}),
    "tile_teal_chipped":  (3, 4, (0.31, 0.55, 0.53), "tile", {"grid": 6}),
    "tile_terracotta":    (3, 5, (0.59, 0.37, 0.27), "tile", {"grid": 5}),
    "barber_pole_stripe": (3, 6, (0.61, 0.59, 0.54), "barber_pole", {}),
    "sandbag_burlap":     (3, 7, (0.57, 0.51, 0.39), "burlap", {}),

    # Row 4: Sunlit trapal tarpaulins, chichirya strips, hand-painted Nena / Vulcanizing / Hall signs, hazard curbs
    "trapal_blue":        (4, 0, (0.27, 0.47, 0.61), "trapal", {"striped": False}),
    "trapal_orange":      (4, 1, (0.61, 0.39, 0.23), "trapal", {"striped": False}),
    "trapal_striped":     (4, 2, (0.30, 0.49, 0.60), "trapal", {"striped": True}),
    "sachet_strips":      (4, 3, (0.60, 0.34, 0.32), "sachets", {}),
    "sign_sari_sari":     (4, 4, (0.61, 0.57, 0.47), "text_sign", {"l1": "TINDAHAN NI", "l2": "ALING NENA", "fg": (0.56, 0.20, 0.19), "hdr": (0.59, 0.23, 0.20), "hdr_fg": (0.62, 0.59, 0.48)}),
    "sign_talyer":        (4, 5, (0.61, 0.51, 0.22), "text_sign", {"l1": "VULCANIZING", "l2": "SHOP", "fg": (0.20, 0.17, 0.18), "hdr": (0.28, 0.31, 0.35), "hdr_fg": (0.62, 0.55, 0.25)}),
    "sign_brgy_hall":     (4, 6, (0.24, 0.39, 0.55), "text_sign", {"l1": "BARANGAY", "l2": "HALL PLAZA", "fg": (0.62, 0.59, 0.50), "hdr": (0.61, 0.49, 0.21), "hdr_fg": (0.20, 0.16, 0.17)}),
    "hazard_curb":        (4, 7, (0.61, 0.51, 0.21), "hazard", {}),

    # Row 5: Structural steel, rust iron, brushed zinc, electric meters, tires, NO ENTRY sign, drums, LPG
    "steel_dark":         (5, 0, (0.33, 0.34, 0.37), "metal", {"rust": 0.24}),
    "steel_rusted":       (5, 1, (0.49, 0.30, 0.21), "metal", {"rust": 0.72}),
    "zinc_brushed":       (5, 2, (0.57, 0.58, 0.59), "metal", {"rust": 0.14}),
    "meter_box":          (5, 3, (0.52, 0.53, 0.51), "meter", {}),
    "rubber_tire":        (5, 4, (0.26, 0.25, 0.27), "tire", {}),
    "sign_no_entry":      (5, 5, (0.59, 0.23, 0.21), "text_sign", {"l1": "NO ENTRY", "l2": "BAWAL DITO", "fg": (0.62, 0.60, 0.55), "hdr": (0.62, 0.60, 0.55), "hdr_fg": (0.58, 0.21, 0.19)}),
    "water_drum_blue":    (5, 6, (0.26, 0.46, 0.62), "drum", {}),
    "lpg_teal":           (5, 7, (0.29, 0.51, 0.49), "drum", {}),

    # Row 6: Liveries, VOTE! HALALAN poster, team banners, 3D sampayan laundry fabrics, court backboard
    "jeep_ochre":         (6, 0, (0.62, 0.51, 0.21), "livery", {"trim": (0.58, 0.25, 0.21)}),
    "jeep_crimson":       (6, 1, (0.59, 0.25, 0.22), "livery", {"trim": (0.62, 0.52, 0.24)}),
    "poster_vote_halalan":(6, 2, (0.61, 0.58, 0.49), "text_sign", {"l1": "VOTE!", "l2": "HALALAN '95", "fg": (0.21, 0.34, 0.56), "hdr": (0.59, 0.22, 0.20), "hdr_fg": (0.62, 0.58, 0.25)}),
    "team_blue":          (6, 3, (0.25, 0.47, 0.63), "banner", {"chevron": (0.59, 0.61, 0.63)}),
    "team_red":           (6, 4, (0.61, 0.25, 0.23), "banner", {"chevron": (0.62, 0.55, 0.31)}),
    "laundry_warm":       (6, 5, (0.61, 0.41, 0.36), "laundry", {"stripe": (0.62, 0.55, 0.38)}),
    "laundry_cool":       (6, 6, (0.38, 0.52, 0.60), "laundry", {"stripe": (0.59, 0.61, 0.59)}),
    "backboard_worn":     (6, 7, (0.60, 0.59, 0.55), "backboard", {}),

    # Row 7: Foliage, KARINDERYA sign, bougainvillea, mango, warm lamps, steel ochre, MALABANAN notice, warm ink
    "leaf_banana":        (7, 0, (0.35, 0.53, 0.26), "foliage", {}),
    "sign_karinderya":    (7, 1, (0.62, 0.52, 0.25), "text_sign", {"l1": "KARINDERYA", "l2": "LUTONG BAHAY", "fg": (0.22, 0.16, 0.16), "hdr": (0.58, 0.24, 0.20), "hdr_fg": (0.62, 0.59, 0.49)}),
    "bougainvillea":      (7, 2, (0.59, 0.28, 0.44), "foliage", {}),
    "produce_mango":      (7, 3, (0.62, 0.51, 0.22), "produce", {}),
    "lamp_warm":          (7, 4, (0.62, 0.57, 0.42), "lamp", {}),
    "steel_ochre":        (7, 5, (0.61, 0.50, 0.23), "metal", {"rust": 0.26}),
    "poster_malabanan":   (7, 6, (0.62, 0.54, 0.22), "malabanan_sign", {}),
    "charcoal_ink":       (7, 7, (0.23, 0.19, 0.20), "ink", {}),
}


def _value_noise(u: np.ndarray, v: np.ndarray, freq_u: int, freq_v: int, seed: int) -> np.ndarray:
    """Tile-clamped bilinear value noise for painterly gouache brush planes."""
    rng = np.random.default_rng(seed)
    grid = rng.random((freq_v + 2, freq_u + 2), dtype=np.float32)
    su = np.clip(u, 0.0, 0.9999) * freq_u
    sv = np.clip(v, 0.0, 0.9999) * freq_v
    iu = np.floor(su).astype(np.int32)
    iv = np.floor(sv).astype(np.int32)
    fu = su - iu
    fv = sv - iv
    # Smoothstep for soft brushstroke edges
    fu = fu * fu * (3.0 - 2.0 * fu)
    fv = fv * fv * (3.0 - 2.0 * fv)
    n00 = grid[iv, iu]
    n10 = grid[iv, iu + 1]
    n01 = grid[iv + 1, iu]
    n11 = grid[iv + 1, iu + 1]
    return (n00 * (1.0 - fu) + n10 * fu) * (1.0 - fv) + (n01 * (1.0 - fu) + n11 * fu) * fv


def _fbm_brush(u: np.ndarray, v: np.ndarray, seed: int, su: int = 6, sv: int = 6) -> np.ndarray:
    n1 = _value_noise(u, v, su, sv, seed)
    n2 = _value_noise(u, v, su * 2 + 1, sv * 2 + 1, seed + 17)
    n3 = _value_noise(u, v, su * 5, sv * 5, seed + 53)
    raw = 0.55 * n1 + 0.30 * n2 + 0.15 * n3
    # Arcane/Valorant stepped gouache brush feel (blend continuous + 6-step posterized)
    stepped = np.round(raw * 6.0) / 6.0
    return (0.62 * raw + 0.38 * stepped) - 0.5


def _mix(a: np.ndarray, b, t: np.ndarray) -> np.ndarray:
    b_arr = np.asarray(b, dtype=np.float32)
    return a * (1.0 - t[..., None]) + b_arr * t[..., None]


def _text_mask(lu: np.ndarray, lv: np.ndarray, text: str, u0: float, u1: float, v_bot: float, v_top: float) -> np.ndarray:
    """Renders crisp 5x7 hand-painted signboard block glyphs into UV space (lv increases upward)."""
    n_chars = max(len(text), 1)
    total_cols = n_chars * 6 - 1
    mask = np.zeros_like(lu, dtype=np.float32)
    in_box = (lu >= u0) & (lu <= u1) & (lv >= v_bot) & (lv <= v_top)
    cu = (lu - u0) / max(u1 - u0, 1e-4) * total_cols
    cv = (v_top - lv) / max(v_top - v_bot, 1e-4) * 7.0
    col_i = np.floor(cu).astype(np.int32)
    row_i = np.floor(cv).astype(np.int32)
    char_idx = col_i // 6
    sub_col = col_i % 6
    for idx, ch in enumerate(text.upper()):
        glyph = FONT_5X7.get(ch, FONT_5X7[" "])
        for r in range(7):
            row_bits = glyph[r]
            for c in range(5):
                if row_bits[c] == "1":
                    hit = in_box & (char_idx == idx) & (sub_col == c) & (row_i == r)
                    mask = np.maximum(mask, hit.astype(np.float32))
    return mask


def _apply_rain_and_moss(rgb: np.ndarray, lu: np.ndarray, lv: np.ndarray, seed: int, rain_amt: float, moss: bool) -> np.ndarray:
    """Applies soft sunlit monsoon drip streaks + organic green lumot moss rising up CHB/concrete bases."""
    if rain_amt > 0.0:
        drip_hi = _value_noise(lu, lv, 26, 3, seed + 611)
        drip_mid = _value_noise(lu, lv, 12, 2, seed + 647)
        channel = np.clip((0.65 * drip_hi + 0.35 * drip_mid - 0.56) * 4.8, 0.0, 1.0)
        top_reach = np.clip((lv - 0.14) * 1.12, 0.0, 1.0) ** 0.78
        eave_stain = np.clip((lv - 0.85) * 4.6, 0.0, 1.0) * (0.42 + 0.58 * drip_mid)
        streak = np.maximum(channel * top_reach, eave_stain * 0.65) * rain_amt
        rgb = _mix(rgb, (0.35, 0.34, 0.30), streak * 0.38)

    if moss:
        # Organic green lumot moss rising up lower courses with capillary damp halo & sunlit moss tufts
        fingers = _value_noise(lu, lv, 18, 5, seed + 409) * 0.18 + (_fbm_brush(lu, lv, seed + 431, 7, 4) * 0.13)
        moss_h = 0.27 + fingers
        damp_halo = np.clip((moss_h + 0.12 - lv) * 5.6, 0.0, 1.0)
        lumot_mat = np.clip((moss_h - lv) * 9.0, 0.0, 1.0)
        deep_root = np.clip((0.11 + fingers * 0.42 - lv) * 12.0, 0.0, 1.0)
        spore_speck = ((_value_noise(lu, lv, 34, 34, seed + 463) > 0.69) & (lv < moss_h)).astype(np.float32)
        rgb = _mix(rgb, (0.34, 0.38, 0.27), damp_halo * 0.58)
        rgb = _mix(rgb, (0.21, 0.39, 0.17), lumot_mat * 0.84)
        rgb = _mix(rgb, (0.33, 0.49, 0.21), spore_speck * lumot_mat * 0.58)
        rgb = _mix(rgb, (0.17, 0.29, 0.16), deep_root * 0.74)
    return rgb


def _paint_swatch(row: int, col: int, base_rgb, kind: str, param: dict) -> np.ndarray:
    seed = 1000 + row * 31 + col * 7
    py, px = np.mgrid[0:CELL, 0:CELL].astype(np.float32)
    u_raw = (px + 0.5) / float(CELL)
    v_raw = (py + 0.5) / float(CELL)
    # Clamp local coordinates inside [PAD_UV, 1-PAD_UV] so the 16 px gutter extends border texels seamlessly!
    lu = np.clip((u_raw - PAD_UV) / (1.0 - 2.0 * PAD_UV), 0.0, 1.0)
    lv = np.clip((v_raw - PAD_UV) / (1.0 - 2.0 * PAD_UV), 0.0, 1.0)

    rgb = np.empty((CELL, CELL, 3), dtype=np.float32)
    rgb[..., 0], rgb[..., 1], rgb[..., 2] = base_rgb

    brush = _fbm_brush(lu, lv, seed, 6, 6)
    wobble = _value_noise(lu, lv, 14, 14, seed + 99) - 0.5
    # Sunlit warm-daylight painterly brush shift
    rgb[..., 0] += brush * 0.075
    rgb[..., 1] += brush * 0.062
    rgb[..., 2] += brush * 0.042

    if kind == "chb":
        # Sunlit 9-course x 4.5-column 40x20cm running-bond Concrete Hollow Blocks with mossy mortar courses
        courses = 9.0
        cols = 4.5
        by = lv * courses + wobble * 0.034
        row_idx = np.floor(by)
        fy = by - row_idx
        bx = lu * cols + (row_idx % 2.0) * 0.5 + wobble * 0.024
        col_idx = np.floor(bx)
        fx = bx - col_idx

        blk_hash = ((np.sin(row_idx * 12.9898 + col_idx * 78.233 + seed) * 43758.5453) % 1.0) - 0.5
        blk_warm = ((np.sin(row_idx * 39.346 + col_idx * 11.135 + seed) * 19283.71) % 1.0) - 0.5
        raw_chb = np.empty_like(rgb)
        raw_chb[..., 0] = 0.51 + blk_hash * 0.075 + blk_warm * 0.022 + brush * 0.055
        raw_chb[..., 1] = 0.51 + blk_hash * 0.072 + brush * 0.055
        raw_chb[..., 2] = 0.48 + blk_hash * 0.068 - blk_warm * 0.022 + brush * 0.048

        h_mortar = fy < 0.135
        v_mortar = fx < 0.085
        mortar = (h_mortar | v_mortar).astype(np.float32)
        arris_hi = (((fy >= 0.135) & (fy < 0.190)) & (~v_mortar)).astype(np.float32)
        mortar_tone = (0.25, 0.36, 0.21) if param.get("moss", False) else (0.34, 0.32, 0.30)
        raw_chb = _mix(raw_chb, mortar_tone, mortar * 0.84)
        raw_chb += arris_hi[..., None] * 0.045

        pit_noise = _value_noise(lu, lv, 40, 40, seed + 201)
        pit_dark = (pit_noise > 0.79).astype(np.float32)
        raw_chb = _mix(raw_chb, (0.33, 0.31, 0.30), pit_dark * 0.45)

        plaster_amt = float(param.get("plaster", 0.0))
        if plaster_amt > 0.0:
            cement_render = _mix(raw_chb, (0.53, 0.52, 0.49), np.full_like(lu, 0.58))
            flake_macro = _fbm_brush(lu, lv, seed + 303, 5, 5)
            flake_detail = _value_noise(lu, lv, 22, 22, seed + 337) - 0.5
            flake_field = flake_macro + flake_detail * 0.32 + 0.50 + lv * 0.26
            primer_mask = np.clip((flake_field - (0.92 - plaster_amt)) * 9.0, 0.0, 1.0)
            paint_mask = np.clip((flake_field - (1.02 - plaster_amt)) * 11.0, 0.0, 1.0)
            chip_rim = ((paint_mask > 0.08) & (paint_mask < 0.74)).astype(np.float32)

            painted = rgb - mortar[..., None] * 0.032 + arris_hi[..., None] * 0.022
            primer_col = _mix(cement_render, painted * 0.90 + 0.04, np.full_like(lu, 0.48))
            rgb = _mix(raw_chb, cement_render, np.clip(primer_mask * 1.2, 0.0, 1.0))
            rgb = _mix(rgb, primer_col, primer_mask)
            rgb = _mix(rgb, painted, paint_mask)
            rgb = _mix(rgb, INK_RGB, chip_rim * 0.48)
        else:
            rgb = raw_chb

        crack_main = np.abs((lv - 0.18) - (lu - 0.20) * 0.65 - wobble * 0.085)
        c_mask = ((crack_main < 0.010) & (lu > 0.18) & (lu < 0.78)).astype(np.float32)
        rgb = _mix(rgb, INK_RGB, c_mask * 0.62)
        rgb = _apply_rain_and_moss(rgb, lu, lv, seed, float(param.get("rain", 0.42)), bool(param.get("moss", False)))

    elif kind == "concrete":
        streak = _fbm_brush(lu, lv, seed + 11, 4, 14)
        pitting = (_value_noise(lu, lv, 34, 34, seed + 119) > 0.79).astype(np.float32)
        rgb += streak[..., None] * 0.055
        rgb = _mix(rgb, (0.35, 0.34, 0.32), pitting * 0.40)
        seam_h = (np.abs((lv * 3.0) % 1.0 - 0.5) < 0.016).astype(np.float32)
        rgb = _mix(rgb, INK_RGB, seam_h * 0.38)
        rgb = _apply_rain_and_moss(rgb, lu, lv, seed, float(param.get("rain", 0.38)), bool(param.get("moss", False)))

    elif kind == "yero":
        if param.get("patch", False):
            right_sheet = (lu > 0.48 + wobble * 0.02).astype(np.float32)
            top_sheet = (lv > 0.53 + wobble * 0.02).astype(np.float32)
            corner_patch = ((lu < 0.34) & (lv < 0.38)).astype(np.float32)
            rgb = np.broadcast_to(np.array([0.55, 0.56, 0.57], dtype=np.float32), rgb.shape).copy()
            rgb = _mix(rgb, (0.53, 0.29, 0.18), right_sheet)
            rgb = _mix(rgb, (0.55, 0.27, 0.21), top_sheet * (1.0 - right_sheet * 0.5))
            rgb = _mix(rgb, (0.32, 0.50, 0.49), corner_patch * 0.76)
            rgb += brush[..., None] * 0.055
            v_seam = (np.abs(lu - (0.48 + wobble * 0.02)) < 0.014).astype(np.float32)
            rgb = _mix(rgb, INK_RGB, v_seam * 0.65)

        # 15 sunlit corrugated GI ribs + oxidized orange-brown rust streaks
        ribs = 15.0
        phase = lu * ribs * 2.0 * math.pi
        wave = np.sin(phase)
        crown = np.clip(np.cos(phase - 0.45), 0.0, 1.0) ** 3.0
        groove = np.clip(-np.cos(phase + 0.35) - 0.48, 0.0, 0.52) / 0.52
        rgb += wave[..., None] * 0.062 + crown[..., None] * 0.042
        rgb = _mix(rgb, INK_RGB, groove * 0.52)

        rust_amt = float(param.get("rust", 0.45))
        rust_macro = _fbm_brush(lu, lv, seed + 512, 5, 9)
        rust_fine = _value_noise(lu, lv, 22, 22, seed + 547) - 0.5
        rust_field = rust_macro + rust_fine * 0.26 + 0.52
        rust_mask = np.clip((rust_field - (1.0 - rust_amt)) * 5.2, 0.0, 1.0)
        deep_rust = np.clip((rust_field - (1.14 - rust_amt)) * 7.5, 0.0, 1.0)
        rgb = _mix(rgb, (0.55, 0.28, 0.16), rust_mask * 0.84)
        rgb = _mix(rgb, (0.41, 0.21, 0.15), deep_rust * 0.72)

        lap_y = 0.52 + wobble * 0.014
        lap_line = (np.abs(lv - lap_y) < 0.014).astype(np.float32)
        rgb = _mix(rgb, INK_RGB, lap_line * 0.68)
        rib_u = (lu * ribs) % 1.0
        nail_col = (np.floor(lu * ribs) % 2.0) < 0.5
        nail_drip = ((lv < 0.56) & (lv > 0.22) & (np.abs(rib_u - 0.25) < 0.12) & nail_col).astype(np.float32) * np.clip((lv - 0.22) / 0.34, 0.0, 1.0)
        rgb = _mix(rgb, (0.52, 0.25, 0.15), nail_drip * 0.76)

    elif kind == "sawali":
        # 45-degree diagonal 2-over-2-under woven bamboo sawali slats
        s = 12.0
        du = (lu + lv) * s
        dv = (lu - lv + 1.0) * s
        iu = np.floor(du)
        iv = np.floor(dv)
        fu = du - iu
        fv = dv - iv
        over = ((iu + iv) % 2.0) < 0.5
        tone_a = np.array(base_rgb, dtype=np.float32) * 1.08
        tone_b = np.array(base_rgb, dtype=np.float32) * 0.78
        slat = np.where(over[..., None], tone_a, tone_b)
        fiber = np.where(over, np.sin(fv * math.pi * 7.0), np.sin(fu * math.pi * 7.0)) * 0.036
        rgb = slat + fiber[..., None] + brush[..., None] * 0.048
        edge = ((fu < 0.11) | (fv < 0.11)).astype(np.float32)
        rgb = _mix(rgb, INK_RGB, edge * 0.45)
        rgb = _apply_rain_and_moss(rgb, lu, lv, seed, 0.34, False)

    elif kind == "plywood":
        grain = np.sin(lu * 8.0 + _fbm_brush(lu, lv, seed + 71, 4, 14) * 6.0) * 0.045
        rgb += grain[..., None]
        if param.get("peel") is not None:
            peel_field = _fbm_brush(lu, lv, seed + 88, 6, 6) + (_value_noise(lu, lv, 20, 20, seed + 91) - 0.5) * 0.30
            peel_m = np.clip((peel_field + 0.12 - lv * 0.22) * 9.0, 0.0, 1.0)
            peel_rim = ((peel_m > 0.08) & (peel_m < 0.76)).astype(np.float32)
            raw_wood = np.asarray(param["peel"], dtype=np.float32) + grain[..., None] * 1.1
            rgb = _mix(rgb, raw_wood, peel_m * 0.88)
            rgb = _mix(rgb, INK_RGB, peel_rim * 0.45)
        # Water stain + vertical wood batten strips at thirds
        rgb = _apply_rain_and_moss(rgb, lu, lv, seed, 0.42, False)
        stain = np.clip((0.18 + wobble * 0.06 - lv) * 6.0, 0.0, 1.0)
        rgb = _mix(rgb, (0.34, 0.28, 0.22), stain * 0.52)
        for bx_pos in (0.33, 0.67):
            batten = (np.abs(lu - bx_pos) < 0.018).astype(np.float32)
            batten_ink = (np.abs(np.abs(lu - bx_pos) - 0.018) < 0.006).astype(np.float32)
            rgb = _mix(rgb, (0.40, 0.30, 0.21), batten * 0.76)
            rgb = _mix(rgb, INK_RGB, batten_ink * 0.48)

    elif kind == "wood":
        planks = float(param.get("planks", 4))
        py_idx = np.floor(lv * planks)
        fy = (lv * planks) - py_idx
        streak = _fbm_brush(lu, lv, seed + 19, 18, 4) * 0.085
        var = ((np.sin(py_idx * 37.7 + seed) * 1973.3) % 1.0 - 0.5) * 0.065
        rgb += (streak + var)[..., None]
        gap = (fy < 0.080).astype(np.float32)
        rgb = _mix(rgb, INK_RGB, gap * 0.58)

    elif kind == "bamboo":
        # Vertical segmented bamboo culm with warm buko node rings
        cyl = np.cos((lu - 0.5) * math.pi) * 0.075 - 0.02
        rgb += cyl[..., None]
        for nz in (0.25, 0.52, 0.78):
            ring = (np.abs(lv - nz) < 0.022).astype(np.float32)
            rgb = _mix(rgb, (0.34, 0.28, 0.17), ring * 0.78)

    elif kind == "jalousie":
        # Sunlit room interior + 10 horizontal overlapping glass/wood louver slats
        rgb[:] = (0.24, 0.30, 0.35)
        frame = ((lu < 0.08) | (lu > 0.92) | (lv < 0.06) | (lv > 0.94)).astype(np.float32)
        mullion = (np.abs(lu - 0.50) < 0.022).astype(np.float32)
        slats = 10.0
        sy = (lv * slats) % 1.0
        glass_tone = np.empty_like(rgb)
        glass_tone[..., 0] = 0.29 + sy * 0.16 + brush * 0.04
        glass_tone[..., 1] = 0.42 + sy * 0.17 + brush * 0.04
        glass_tone[..., 2] = 0.48 + sy * 0.16 + brush * 0.04
        lip = (sy < 0.14).astype(np.float32)
        glass_tone = _mix(glass_tone, INK_RGB, lip * 0.62)
        shine = ((sy > 0.64) & (sy < 0.86) & (lu + lv * 0.4 > 0.42) & (lu + lv * 0.4 < 0.78)).astype(np.float32)
        glass_tone = _mix(glass_tone, (0.56, 0.62, 0.63), shine * 0.58)
        rgb = _mix(glass_tone, (0.48, 0.49, 0.47), np.maximum(frame, mullion))

    elif kind == "rehas":
        # Soft shaded interior + oxidized orange-brown rusted wrought-iron security bars
        rgb[:] = (0.26, 0.23, 0.22)
        bars_u = np.abs(((lu * 6.0) % 1.0) - 0.5) < 0.10
        bars_v = (np.abs(lv - 0.18) < 0.028) | (np.abs(lv - 0.82) < 0.028) | (np.abs(lv - 0.50) < 0.025)
        if param.get("counter", False):
            # Pass-through sari-sari bentahan hole at bottom-center with warm shelf glow behind
            shelf_bg = (lv > 0.44).astype(np.float32)
            rgb = _mix(rgb, (0.38, 0.32, 0.25), shelf_bg * 0.55)
            hole = (lu > 0.34) & (lu < 0.66) & (lv > 0.10) & (lv < 0.42)
            bars_u = bars_u & (~hole)
            hole_rim = ((np.abs(lu - 0.34) < 0.024) | (np.abs(lu - 0.66) < 0.024) | (np.abs(lv - 0.42) < 0.028)) & (lu >= 0.32) & (lu <= 0.68) & (lv <= 0.44)
            bars_v = bars_v | hole_rim
        iron = (bars_u | bars_v).astype(np.float32)
        iron_col = np.empty_like(rgb)
        iron_col[..., 0] = 0.51 + brush * 0.10
        iron_col[..., 1] = 0.30 + brush * 0.06
        iron_col[..., 2] = 0.20 + brush * 0.05
        rgb = _mix(rgb, iron_col, iron)

    elif kind == "door":
        # Provincial 2-panel timber door with stiles, wood grain & moss base
        panel_x = (lu > 0.18) & (lu < 0.82)
        panel_top = panel_x & (lv > 0.54) & (lv < 0.86)
        panel_bot = panel_x & (lv > 0.14) & (lv < 0.44)
        recess = (panel_top | panel_bot).astype(np.float32)
        rgb = _mix(rgb, np.array(base_rgb, dtype=np.float32) * 0.82, recess * 0.76)
        border = (((np.abs(lu - 0.18) < 0.020) | (np.abs(lu - 0.82) < 0.020)) & (lv > 0.14) & (lv < 0.86)).astype(np.float32)
        rgb = _mix(rgb, INK_RGB, border * 0.55)
        knob = (np.hypot(lu - 0.84, lv - 0.48) < 0.034).astype(np.float32)
        rgb = _mix(rgb, (0.60, 0.50, 0.26), knob)
        rgb = _apply_rain_and_moss(rgb, lu, lv, seed, 0.36, True)

    elif kind == "tile":
        g = float(param.get("grid", 5))
        tx = (lu * g) % 1.0
        ty = (lv * g) % 1.0
        grout = ((tx < 0.09) | (ty < 0.09)).astype(np.float32)
        rgb = _mix(rgb, (0.34, 0.32, 0.29), grout * 0.72)
        chip = (_value_noise(lu, lv, 20, 20, seed + 61) > 0.82).astype(np.float32)
        rgb = _mix(rgb, (0.46, 0.44, 0.40), chip * 0.65)

    elif kind == "burlap":
        weave = (np.sin(lu * 90.0) * np.sin(lv * 90.0)) * 0.035
        rgb += weave[..., None]
        seam = (np.abs(lv - 0.5) < 0.022).astype(np.float32)
        rgb = _mix(rgb, INK_RGB, seam * 0.42)

    elif kind == "barber_pole":
        # Classic 90s Barberya diagonal red / warm-cream / cobalt-blue cylinder stripes
        stripe_phase = (lu * 2.0 + lv * 3.0) % 1.0
        red_band = (stripe_phase < 0.30).astype(np.float32)
        blue_band = ((stripe_phase >= 0.50) & (stripe_phase < 0.80)).astype(np.float32)
        rgb = _mix(rgb, (0.60, 0.23, 0.21), red_band * 0.92)
        rgb = _mix(rgb, (0.24, 0.39, 0.59), blue_band * 0.92)
        gloss = np.cos((lu - 0.42) * math.pi) * 0.055
        rgb += gloss[..., None]

    elif kind == "trapal":
        if param.get("striped", False):
            band = np.floor(lu * 6.0) % 3.0
            rgb = np.where((band == 1.0)[..., None], np.array([0.61, 0.57, 0.48], dtype=np.float32), rgb)
            rgb = np.where((band == 2.0)[..., None], np.array([0.59, 0.27, 0.22], dtype=np.float32), rgb)
            rgb += brush[..., None] * 0.05
        # Tarp scrim grid + diagonal sun-bleached fold creases + brass grommets
        scrim = (np.sin(lu * 110.0) + np.sin(lv * 110.0)) * 0.014
        crease = np.exp(-((lu * 0.8 + lv - 0.92) ** 2) * 320.0) * 0.065
        grime = np.clip((_fbm_brush(lu, lv, seed + 177, 8, 8) + 0.06) * 2.6, 0.0, 1.0)
        rgb = _mix(rgb + (scrim + crease)[..., None], (0.35, 0.33, 0.29), grime * 0.22)
        for gx in (0.12, 0.50, 0.88):
            for gy in (0.08, 0.92):
                d = np.hypot(lu - gx, lv - gy)
                ring = ((d < 0.032) & (d > 0.014)).astype(np.float32)
                hole = (d <= 0.014).astype(np.float32)
                rgb = _mix(rgb, (0.58, 0.50, 0.28), ring)
                rgb = _mix(rgb, INK_RGB, hole * 0.75)

    elif kind == "sachets":
        # Hanging sunlit colorful chichirya & shampoo packets ( Boy Bawang / Clover / Sunsilk vibes )
        cx = np.floor(lu * 4.0)
        cy = np.floor(lv * 5.0)
        fx = (lu * 4.0) - cx
        fy = (lv * 5.0) - cy
        pal = [
            (0.62, 0.50, 0.20),
            (0.60, 0.25, 0.23),
            (0.25, 0.46, 0.61),
            (0.29, 0.53, 0.31),
        ]
        idx = ((cx * 3 + cy * 5) % 4).astype(np.int32)
        for k in range(4):
            rgb = np.where((idx == k)[..., None], np.array(pal[k], dtype=np.float32), rgb)
        rgb += brush[..., None] * 0.045
        label_band = ((fx > 0.18) & (fx < 0.82) & (fy > 0.42) & (fy < 0.68)).astype(np.float32)
        rgb = _mix(rgb, (0.62, 0.59, 0.51), label_band * 0.65)
        seal = ((fx < 0.09) | (fy < 0.10)).astype(np.float32)
        rgb = _mix(rgb, INK_RGB, seal * 0.58)

    elif kind == "text_sign":
        # Hand-painted 90s Philippine province tin signboard with legible block typography & oxidized rust corners!
        hdr_col = np.asarray(param.get("hdr", (0.58, 0.23, 0.20)), dtype=np.float32)
        hdr_fg = np.asarray(param.get("hdr_fg", (0.62, 0.59, 0.49)), dtype=np.float32)
        body_fg = np.asarray(param.get("fg", (0.21, 0.17, 0.18)), dtype=np.float32)
        line1 = str(param.get("l1", "TINDAHAN"))
        line2 = str(param.get("l2", "SARI-SARI"))

        header_band = ((lv > 0.58) & (lv < 0.91) & (lu > 0.06) & (lu < 0.94)).astype(np.float32)
        rgb = _mix(rgb, hdr_col, header_band * 0.94)

        m1 = _text_mask(lu, lv, line1, 0.09, 0.91, 0.64, 0.86)
        m1_sh = _text_mask(lu - 0.008, lv + 0.012, line1, 0.09, 0.91, 0.64, 0.86) * (1.0 - m1)
        rgb = _mix(rgb, INK_RGB, m1_sh * 0.62)
        rgb = _mix(rgb, hdr_fg, m1 * 0.96)

        u_pad2 = 0.08 if len(line2) >= 9 else 0.15
        m2 = _text_mask(lu, lv, line2, u_pad2, 1.0 - u_pad2, 0.18, 0.49)
        m2_sh = _text_mask(lu - 0.009, lv + 0.014, line2, u_pad2, 1.0 - u_pad2, 0.18, 0.49) * (1.0 - m2)
        rgb = _mix(rgb, (0.34, 0.28, 0.24), m2_sh * 0.55)
        rgb = _mix(rgb, body_fg, m2 * 0.96)

        pinstripe = (
            (((np.abs(lu - 0.055) < 0.011) | (np.abs(lu - 0.945) < 0.011)) & (lv > 0.07) & (lv < 0.93))
            | (((np.abs(lv - 0.075) < 0.013) | (np.abs(lv - 0.575) < 0.013) | (np.abs(lv - 0.915) < 0.013)) & (lu > 0.05) & (lu < 0.95))
        ).astype(np.float32)
        rgb = _mix(rgb, INK_RGB, pinstripe * 0.68)

        rust_f = _fbm_brush(lu, lv, seed + 709, 7, 7) + 0.5
        edge_prox = 1.0 - np.clip(np.minimum(np.minimum(lu, 1.0 - lu), np.minimum(lv, 1.0 - lv)) * 7.0, 0.0, 1.0)
        sign_rust = np.clip((rust_f * 0.72 + edge_prox * 0.65 - 0.66) * 4.8, 0.0, 1.0)
        rgb = _mix(rgb, (0.53, 0.27, 0.16), sign_rust * 0.78)
        for nx in (0.09, 0.91):
            for ny in (0.12, 0.88):
                nh = (np.hypot(lu - nx, lv - ny) < 0.018).astype(np.float32)
                ndrip = ((np.abs(lu - nx) < 0.011) & (lv < ny) & (lv > ny - 0.18)).astype(np.float32)
                rgb = _mix(rgb, (0.50, 0.25, 0.15), ndrip * 0.65)
                rgb = _mix(rgb, INK_RGB, nh * 0.78)

    elif kind == "malabanan_sign":
        # Classic 90s electric-post yellow/orange tin notice: MALABANAN / SIPSIP POSO / NEGRO
        top_bar = ((lv > 0.69) & (lv < 0.92) & (lu > 0.06) & (lu < 0.94)).astype(np.float32)
        rgb = _mix(rgb, (0.59, 0.24, 0.19), top_bar * 0.92)
        t1 = _text_mask(lu, lv, "MALABANAN", 0.09, 0.91, 0.73, 0.89)
        t2 = _text_mask(lu, lv, "SIPSIP POSO", 0.08, 0.92, 0.44, 0.63)
        t3 = _text_mask(lu, lv, "NEGRO", 0.20, 0.80, 0.17, 0.37)
        rgb = _mix(rgb, (0.63, 0.60, 0.50), t1 * 0.96)
        rgb = _mix(rgb, (0.20, 0.17, 0.18), np.maximum(t2, t3) * 0.95)
        frame = (
            (((np.abs(lu - 0.055) < 0.012) | (np.abs(lu - 0.945) < 0.012)) & (lv > 0.07) & (lv < 0.93))
            | (((np.abs(lv - 0.08) < 0.013) | (np.abs(lv - 0.92) < 0.013)) & (lu > 0.05) & (lu < 0.95))
        ).astype(np.float32)
        rgb = _mix(rgb, INK_RGB, frame * 0.70)
        rust_corner = np.clip((1.0 - np.hypot(lu - 0.08, lv - 0.10) * 4.5), 0.0, 1.0) + np.clip((1.0 - np.hypot(lu - 0.92, lv - 0.88) * 4.5), 0.0, 1.0)
        rgb = _mix(rgb, (0.53, 0.27, 0.16), np.clip(rust_corner, 0.0, 1.0) * 0.75)

    elif kind == "bawal_sign":
        # Sunlit lime-washed CHB base wall with red stenciled "BAWAL UMIHI DITO" + green moss foot
        courses = 7.0
        fy = (lv * courses + wobble * 0.025) % 1.0
        mortar = (fy < 0.075).astype(np.float32)
        rgb = _mix(rgb, (0.44, 0.43, 0.39), mortar * 0.48)
        patch = ((lu > 0.07) & (lu < 0.93) & (lv > 0.26) & (lv < 0.88)).astype(np.float32)
        rgb = _mix(rgb, (0.62, 0.60, 0.54), patch * 0.86)
        b1 = _text_mask(lu, lv, "BAWAL UMIHI", 0.09, 0.91, 0.60, 0.81)
        b2 = _text_mask(lu, lv, "DITO", 0.26, 0.74, 0.34, 0.54)
        stencil = np.maximum(b1, b2)
        spray_halo = (_value_noise(lu, lv, 28, 28, seed + 303) > 0.35).astype(np.float32)
        rgb = _mix(rgb, (0.58, 0.21, 0.19), stencil * (0.85 + 0.15 * spray_halo))
        rgb = _apply_rain_and_moss(rgb, lu, lv, seed, 0.36, True)

    elif kind == "hazard":
        diag = ((np.floor((lu + lv) * 7.0) % 2.0) > 0.5).astype(np.float32)
        rgb = _mix(rgb, (0.24, 0.22, 0.23), diag * 0.86)
        wear = np.clip((_fbm_brush(lu, lv, seed + 77, 8, 8) + 0.12) * 2.6, 0.0, 1.0)
        rgb = _mix(rgb, (0.46, 0.45, 0.41), wear * 0.38)

    elif kind == "metal":
        sheen = np.sin((lu * 0.7 + lv * 0.3) * math.pi * 5.0) * 0.045
        rgb += sheen[..., None]
        rust_amt = float(param.get("rust", 0.25))
        rf = np.clip((_fbm_brush(lu, lv, seed + 141, 7, 7) + rust_amt - 0.18) * 4.2, 0.0, 1.0)
        rgb = _mix(rgb, (0.53, 0.28, 0.17), rf * 0.82)

    elif kind == "meter":
        # Galvanized Meralco kWh meter box with glass dial dome, cyclometer digits & rust corners
        d = np.hypot(lu - 0.50, lv - 0.58)
        glass = (d < 0.27).astype(np.float32)
        rim = ((d >= 0.24) & (d < 0.30)).astype(np.float32)
        dial = ((np.abs(lv - 0.60) < 0.052) & (np.abs(lu - 0.50) < 0.17)).astype(np.float32)
        disc = ((np.abs(lv - 0.49) < 0.018) & (np.abs(lu - 0.50) < 0.14)).astype(np.float32)
        rgb = _mix(rgb, (0.25, 0.31, 0.35), glass * 0.88)
        rgb = _mix(rgb, (0.60, 0.58, 0.52), dial * 0.92)
        rgb = _mix(rgb, (0.59, 0.26, 0.18), disc * 0.85)
        rgb = _mix(rgb, INK_RGB, rim * 0.78)
        tag = ((lu > 0.28) & (lu < 0.72) & (lv > 0.14) & (lv < 0.25)).astype(np.float32)
        rgb = _mix(rgb, (0.61, 0.50, 0.20), tag * 0.88)

    elif kind == "tire":
        tread = (((np.floor(lv * 12.0) + np.floor(lu * 4.0)) % 2.0) * 0.035)
        rgb += tread[..., None]
        groove = ((np.abs(lu - 0.33) < 0.025) | (np.abs(lu - 0.67) < 0.025)).astype(np.float32)
        rgb = _mix(rgb, INK_RGB, groove * 0.62)

    elif kind == "wire":
        strand = np.sin(lu * math.pi * 10.0) * 0.025
        rgb += strand[..., None]

    elif kind == "drum":
        for rz in (0.28, 0.68):
            rib = (np.abs(lv - rz) < 0.028).astype(np.float32)
            rgb = _mix(rgb, np.array(base_rgb, dtype=np.float32) * 0.74, rib * 0.78)
        scuff = np.clip((_fbm_brush(lu, lv, seed + 93, 8, 8) + 0.08) * 3.0, 0.0, 1.0)
        rgb = _mix(rgb, (0.38, 0.40, 0.41), scuff * 0.26)

    elif kind == "livery":
        trim = np.asarray(param.get("trim", (0.58, 0.24, 0.20)), dtype=np.float32)
        stripe = ((lv > 0.38) & (lv < 0.62)).astype(np.float32)
        pin = ((np.abs(lv - 0.35) < 0.018) | (np.abs(lv - 0.65) < 0.018)).astype(np.float32)
        rgb = _mix(rgb, trim, stripe * 0.88)
        rgb = _mix(rgb, INK_RGB, pin * 0.65)

    elif kind == "banner":
        chev_col = np.asarray(param.get("chevron", (0.61, 0.61, 0.61)), dtype=np.float32)
        chev = (np.abs((lv - 0.50) + np.abs(lu - 0.50) * 0.75) < 0.075).astype(np.float32)
        chev2 = (np.abs((lv - 0.28) + np.abs(lu - 0.50) * 0.75) < 0.055).astype(np.float32)
        rgb = _mix(rgb, chev_col, np.maximum(chev, chev2) * 0.90)
        chev_ink = (np.abs((lv - 0.50) + np.abs(lu - 0.50) * 0.75) < 0.095) & (chev < 0.5)
        rgb = _mix(rgb, INK_RGB, chev_ink.astype(np.float32) * 0.58)

    elif kind == "laundry":
        stripe_col = np.asarray(param.get("stripe", (0.61, 0.56, 0.46)), dtype=np.float32)
        band = ((lv > 0.42) & (lv < 0.60)).astype(np.float32)
        fold = np.sin(lu * math.pi * 5.0) * 0.045
        rgb = _mix(rgb, stripe_col, band * 0.85) + fold[..., None]

    elif kind == "backboard":
        # Painted red shooter's box on sunlit scuffed plywood backboard
        box_x = (lu > 0.30) & (lu < 0.70)
        box_y = (lv > 0.18) & (lv < 0.56)
        inner_x = (lu > 0.35) & (lu < 0.65)
        inner_y = (lv > 0.23) & (lv < 0.51)
        shooter = (box_x & box_y & (~(inner_x & inner_y))).astype(np.float32)
        rgb = _mix(rgb, (0.59, 0.23, 0.20), shooter * 0.92)

    elif kind == "foliage":
        rib = (np.abs(lu - 0.50) < 0.025).astype(np.float32)
        vein = np.sin((lv * 14.0 - np.abs(lu - 0.5) * 10.0) * math.pi) * 0.04
        rgb += vein[..., None]
        rgb = _mix(rgb, np.array(base_rgb, dtype=np.float32) * 1.15, rib * 0.55)

    elif kind == "produce":
        gx = (lu * 5.0) % 1.0 - 0.5
        gy = (lv * 4.0) % 1.0 - 0.5
        d = np.hypot(gx, gy)
        rim = ((d > 0.34) & (d < 0.46)).astype(np.float32)
        rgb = _mix(rgb, INK_RGB, rim * 0.55)

    elif kind == "lamp":
        glow = np.clip(1.0 - np.hypot(lu - 0.5, lv - 0.5) * 1.2, 0.0, 1.0)
        rgb = _mix(np.array([0.48, 0.40, 0.26], dtype=np.float32), base_rgb, glow)

    elif kind == "ink":
        rgb[:] = (0.24, 0.20, 0.21)

    # Soft sunlit 90s Filipino Komiks panel contour (warm espresso, never harsh black)
    if kind not in ("ink", "wire", "bamboo", "barber_pole"):
        edge_dist = np.minimum(np.minimum(lu, 1.0 - lu), np.minimum(lv, 1.0 - lv))
        ink_w = 0.015 + wobble * 0.004
        border_ink = np.clip((ink_w - edge_dist) * 95.0, 0.0, 1.0)
        rgb = _mix(rgb, INK_RGB, border_ink * 0.42)

    return np.clip(rgb, MIN_ALBEDO, MAX_ALBEDO)


def build_atlas_image() -> bpy.types.Image:
    """Generates the 2048x2048 sunlit 90s province painterly atlas, writes BarangayAtlas.jpg + .jpg.import."""
    if ATLAS_NAME in bpy.data.images:
        bpy.data.images.remove(bpy.data.images[ATLAS_NAME])

    # Blender image rows are bottom-to-top (v=0 at bottom, v=1 at top)
    atlas = np.full((ATLAS_SIZE, ATLAS_SIZE, 4), 0.46, dtype=np.float32)
    atlas[..., 3] = 1.0

    for _name, (row, col, base_rgb, kind, param) in SWATCHES.items():
        tile_rgb = _paint_swatch(row, col, base_rgb, kind, param)
        y0 = row * CELL
        x0 = col * CELL
        atlas[y0:y0 + CELL, x0:x0 + CELL, :3] = tile_rgb

    # Calibrate warm sunlit daytime exposure gently if needed so mean_sRGB lands cleanly around ~0.46 in [0.43, 0.49]
    pre_mean = float(atlas[..., :3].mean())
    if pre_mean < 0.442 or pre_mean > 0.478:
        shift = 0.460 - pre_mean
        atlas[..., :3] = np.clip(atlas[..., :3] + shift, MIN_ALBEDO, MAX_ALBEDO)

    max_pre = float(atlas[..., :3].max())
    assert max_pre <= 0.74, f"Atlas pre-JPEG max RGB exceeded 0.74: {max_pre:.4f}"

    os.makedirs(OUT_DIR, exist_ok=True)
    img = bpy.data.images.new(ATLAS_NAME, width=ATLAS_SIZE, height=ATLAS_SIZE, alpha=False)
    # 8-bit byte-backed bpy.types.Image (float_buffer=False) reads & writes sRGB [0..1] directly in img.pixels!
    img.pixels.foreach_set(atlas.ravel())

    jpg_path = os.path.join(OUT_DIR, ATLAS_FILENAME)
    img.filepath_raw = jpg_path
    img.file_format = "JPEG"
    bpy.context.scene.render.image_settings.quality = 95
    img.save()

    # Verify raw decoded JPEG sRGB pixels on disk (check_img is 8-bit byte-backed -> pixels are direct sRGB byte/255.0!)
    check_img = bpy.data.images.load(jpg_path, check_existing=False)
    chk_raw = np.empty(ATLAS_SIZE * ATLAS_SIZE * 4, dtype=np.float32)
    check_img.pixels.foreach_get(chk_raw)
    bpy.data.images.remove(check_img)
    chk_srgb = chk_raw.reshape((-1, 4))[:, :3]
    max_srgb = float(chk_srgb.max())
    mean_srgb = float(chk_srgb.mean())
    min_srgb = float(chk_srgb.min())
    pct_dark = float((chk_srgb < 0.08).mean()) * 100.0
    print(
        f"Saved {jpg_path} ({ATLAS_SIZE}x{ATLAS_SIZE}, "
        f"mean_sRGB={mean_srgb:.4f} [0.43..0.49], max_sRGB={max_srgb:.4f} <=0.78, "
        f"min_sRGB={min_srgb:.4f}, <0.08={pct_dark:.3f}%)"
    )
    assert 0.43 <= mean_srgb <= 0.49, f"Decoded JPEG mean_sRGB out of [0.43, 0.49]: {mean_srgb:.4f}"
    assert max_srgb <= 0.78, f"Decoded JPEG max_sRGB > 0.78: {max_srgb:.4f}"
    assert pct_dark == 0.0, f"Decoded JPEG has harsh <0.08 black pixels: {pct_dark:.4f}%"

    _write_godot_jpg_import(jpg_path)
    _clean_legacy_duplicate_jpegs()
    return img


def _write_godot_jpg_import(jpg_path: str) -> None:
    """Ensures BarangayAtlas.jpg.import preserves Godot's [remap]/[deps], sets vram_texture + compress/mode=2 + mipmaps, and never writes md5."""
    import_path = jpg_path + ".import"
    if os.path.isfile(import_path):
        with open(import_path, "r", encoding="utf-8") as ef:
            raw_lines = ef.read().splitlines()
        cleaned_lines = []
        skip_gen_params = False
        for line in raw_lines:
            stripped = line.strip()
            if stripped.startswith("generator_parameters="):
                skip_gen_params = not stripped.endswith("}")
                continue
            if skip_gen_params:
                if stripped.endswith("}"):
                    skip_gen_params = False
                continue
            if stripped.startswith("md5=") or stripped.startswith('"md5"'):
                continue
            if stripped == '"vram_texture": false':
                line = '"vram_texture": true'
            elif stripped.startswith("compress/mode="):
                line = "compress/mode=2"
            elif stripped.startswith("mipmaps/generate="):
                line = "mipmaps/generate=true"
            cleaned_lines.append(line)
        import_text = "\n".join(cleaned_lines) + "\n"
        if "[remap]" in import_text and "[deps]" in import_text and "compress/mode=2" in import_text:
            with open(import_path, "w", encoding="utf-8", newline="\n") as f:
                f.write(import_text)
            return

    res_source = f"res://assets/models/barangay/{ATLAS_FILENAME}"
    path_hash = hashlib.md5(res_source.encode("utf-8")).hexdigest()
    ctex = f"res://.godot/imported/{ATLAS_FILENAME}-{path_hash}.ctex"
    import_text = f"""[remap]

importer="texture"
type="CompressedTexture2D"
uid="uid://4ffjyw6ndx58"
path="{ctex}"
metadata={{
"vram_texture": true
}}

[deps]

source_file="{res_source}"
dest_files=["{ctex}"]

[params]

compress/mode=2
compress/high_quality=false
compress/lossy_quality=0.7
compress/uastc_level=0
compress/rdo_quality_loss=0.0
compress/hdr_compression=1
compress/normal_map=0
compress/channel_pack=0
mipmaps/generate=true
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
    with open(import_path, "w", encoding="utf-8", newline="\n") as f:
        f.write(import_text)


def _clean_legacy_duplicate_jpegs() -> None:
    """Removes old per-model 512x512 *_BarangayAtlas.jpg duplicates now that all GLBs share BarangayAtlas.jpg."""
    for fname in os.listdir(OUT_DIR):
        if fname.endswith("_BarangayAtlas.jpg") or fname.endswith("_BarangayAtlas.jpg.import"):
            os.remove(os.path.join(OUT_DIR, fname))


def _rewrite_glb_external_atlas(glb_path: str) -> None:
    """Rewrites GLB image chunk to reference shared external URI `BarangayAtlas.jpg` (saves 50+ MB VRAM/APK)."""
    with open(glb_path, "rb") as f:
        raw = f.read()
    magic, ver, _total = struct.unpack_from("<4sII", raw, 0)
    assert magic == b"glTF" and ver == 2
    j_len, j_type = struct.unpack_from("<II", raw, 12)
    assert j_type == 0x4E4F534A  # JSON
    gltf = json.loads(raw[20:20 + j_len].decode("utf-8"))

    b_off = 20 + j_len
    b_len, b_type = struct.unpack_from("<II", raw, b_off)
    assert b_type == 0x004E4942  # BIN
    bin_blob = raw[b_off + 8:b_off + 8 + b_len]

    images = gltf.get("images", [])
    bvs = gltf.get("bufferViews", [])
    if images and "bufferView" in images[0]:
        img_bv_idx = images[0]["bufferView"]
        # If the embedded image is the final bufferView (standard Blender GLB layout), trim BIN cleanly!
        if img_bv_idx == len(bvs) - 1:
            new_bin_len = bvs[img_bv_idx]["byteOffset"]
            bvs.pop()
            bin_blob = bin_blob[:new_bin_len]
            gltf["buffers"][0]["byteLength"] = new_bin_len
        images[0] = {
            "name": ATLAS_NAME,
            "uri": ATLAS_FILENAME,
            "mimeType": "image/jpeg",
        }

    j_bytes = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    j_pad = (4 - (len(j_bytes) % 4)) % 4
    j_bytes += b" " * j_pad

    b_pad = (4 - (len(bin_blob) % 4)) % 4
    bin_padded = bin_blob + (b"\x00" * b_pad)

    total_len = 12 + 8 + len(j_bytes) + 8 + len(bin_padded)
    out = bytearray()
    out += struct.pack("<4sII", b"glTF", 2, total_len)
    out += struct.pack("<II", len(j_bytes), 0x4E4F534A)
    out += j_bytes
    out += struct.pack("<II", len(bin_padded), 0x004E4942)
    out += bin_padded
    with open(glb_path, "wb") as f:
        f.write(out)


def build_shared_material(atlas_img: bpy.types.Image) -> bpy.types.Material:
    if "BarangayMat" in bpy.data.materials:
        bpy.data.materials.remove(bpy.data.materials["BarangayMat"])
    mat = bpy.data.materials.new(name="BarangayMat")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()

    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = 0.86
    bsdf.inputs["Metallic"].default_value = 0.0

    tex = nodes.new("ShaderNodeTexImage")
    tex.image = atlas_img
    tex.interpolation = "Linear"

    vc = nodes.new("ShaderNodeVertexColor")
    vc.layer_name = "Color"

    mix = nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.blend_type = "MULTIPLY"
    mix.inputs["Factor"].default_value = 1.0

    links.new(tex.outputs["Color"], mix.inputs[6])
    links.new(vc.outputs["Color"], mix.inputs[7])
    links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def swatch_uv_box(swatch_name: str):
    row, col, *_ = SWATCHES[swatch_name]
    u0 = (col + PAD_UV) / float(GRID)
    u1 = (col + 1.0 - PAD_UV) / float(GRID)
    v0 = (row + PAD_UV) / float(GRID)
    v1 = (row + 1.0 - PAD_UV) / float(GRID)
    return u0, v0, u1, v1


class KitBuilder:
    """Accumulates cellular-detail Barangay architecture into a single-draw-call mesh."""

    def __init__(self):
        self.bm = bmesh.new()
        self.uv_layer = self.bm.loops.layers.uv.new("UVMap")

    def _map_faces(self, faces, swatch: str, mode: str = "panel", bevel_thresh: float = 0.065):
        u0, v0, u1, v1 = swatch_uv_box(swatch)
        du = u1 - u0
        dv = v1 - v0
        for f in faces:
            if not f.is_valid:
                continue
            f.smooth = True
            f.normal_update()
            n = f.normal
            # Local orthonormal 2D frame on face so V always points upward / up-slope!
            if abs(n.z) > 0.92:
                tu = Vector((1.0, 0.0, 0.0))
                tv = Vector((0.0, 1.0, 0.0))
            else:
                tu = Vector((-n.y, n.x, 0.0))
                if tu.length_squared < 1e-6:
                    tu = Vector((1.0, 0.0, 0.0))
                else:
                    tu.normalize()
                tv = tu.cross(n)
                if tv.z < 0.0:
                    tv = -tv
                tv.normalize()

            us = [loop.vert.co.dot(tu) for loop in f.loops]
            vs = [loop.vert.co.dot(tv) for loop in f.loops]
            min_u, max_u = min(us), max(us)
            min_v, max_v = min(vs), max(vs)
            span_u = max(max_u - min_u, 1e-4)
            span_v = max(max_v - min_v, 1e-4)

            if mode == "trim":
                # Keep interior of swatch without heavy frame ink for thin pipes/poles/wires
                m = 0.10
                for loop, pu, pv in zip(f.loops, us, vs):
                    su = m + ((pu - min_u) / span_u) * (1.0 - 2.0 * m)
                    sv = m + ((pv - min_v) / span_v) * (1.0 - 2.0 * m)
                    loop[self.uv_layer].uv = (u0 + su * du, v0 + sv * dv)
            elif min(span_u, span_v) < bevel_thresh:
                # Bevel chamfer strip -> sample near the inked border of the swatch for crisp komiks edges!
                for loop, pu, pv in zip(f.loops, us, vs):
                    su = ((pu - min_u) / span_u) if span_u >= span_v else (0.012 * ((pu - min_u) / span_u))
                    sv = ((pv - min_v) / span_v) if span_v > span_u else (0.012 * ((pv - min_v) / span_v))
                    loop[self.uv_layer].uv = (u0 + su * du, v0 + sv * dv)
            else:
                for loop, pu, pv in zip(f.loops, us, vs):
                    su = (pu - min_u) / span_u
                    sv = (pv - min_v) / span_v
                    loop[self.uv_layer].uv = (u0 + su * du, v0 + sv * dv)

    def add_box(self, size, center, swatch: str, rot_euler=(0.0, 0.0, 0.0), bevel: float = 0.024, mode: str = "panel"):
        sx, sy, sz = size
        ret = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = ret["verts"]
        bmesh.ops.scale(self.bm, vec=Vector((sx, sy, sz)), verts=verts)
        if bevel > 0.0 and min(sx, sy, sz) >= max(bevel * 3.1, 0.28):
            edges = list({e for v in verts for e in v.link_edges})
            brev = bmesh.ops.bevel(self.bm, geom=edges, offset=bevel, segments=1, affect="EDGES")
            verts = list({v for v in (list(brev["verts"]) + verts) if v.is_valid})
        if any(abs(a) > 1e-5 for a in rot_euler):
            rot = Euler(rot_euler, "XYZ").to_matrix()
            bmesh.ops.rotate(self.bm, cent=Vector((0.0, 0.0, 0.0)), matrix=rot, verts=verts)
        bmesh.ops.translate(self.bm, vec=Vector(center), verts=verts)
        faces = list({f for v in verts for f in v.link_faces if f.is_valid})
        self._map_faces(faces, swatch, mode=mode, bevel_thresh=bevel * 2.3)
        return verts

    def add_subdivided_box(self, size, center, swatch: str, bay_max: float = 5.4, bevel: float = 0.024):
        """Creates an axis-aligned room volume sliced into crisp CHB/sawali bays while keeping low-poly triangle budgets."""
        sx, sy, sz = size
        cx, cy, cz = center
        prev_v = set(self.bm.verts)
        prev_e = set(self.bm.edges)
        prev_f = set(self.bm.faces)

        ret = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = ret["verts"]
        bmesh.ops.scale(self.bm, vec=Vector((sx, sy, sz)), verts=verts)
        if bevel > 0.0 and min(sx, sy, sz) >= max(bevel * 3.1, 0.28):
            edges = list({e for v in verts for e in v.link_edges})
            bmesh.ops.bevel(self.bm, geom=edges, offset=bevel, segments=1, affect="EDGES")
        bmesh.ops.translate(self.bm, vec=Vector((cx, cy, cz)), verts=[v for v in self.bm.verts if v not in prev_v])

        nx = max(1, int(round(sx / bay_max)))
        ny = max(1, int(round(sy / bay_max)))
        for k in range(1, nx):
            px = (cx - sx * 0.5) + k * (sx / float(nx))
            cur_geom = (
                [v for v in self.bm.verts if v not in prev_v]
                + [e for e in self.bm.edges if e not in prev_e]
                + [f for f in self.bm.faces if f not in prev_f]
            )
            bmesh.ops.bisect_plane(
                self.bm, geom=cur_geom, dist=1e-4,
                plane_co=Vector((px, cy, cz)), plane_no=Vector((1.0, 0.0, 0.0)),
            )
        for k in range(1, ny):
            py = (cy - sy * 0.5) + k * (sy / float(ny))
            cur_geom = (
                [v for v in self.bm.verts if v not in prev_v]
                + [e for e in self.bm.edges if e not in prev_e]
                + [f for f in self.bm.faces if f not in prev_f]
            )
            bmesh.ops.bisect_plane(
                self.bm, geom=cur_geom, dist=1e-4,
                plane_co=Vector((cx, py, cz)), plane_no=Vector((0.0, 1.0, 0.0)),
            )

        new_faces = [f for f in self.bm.faces if f not in prev_f and f.is_valid]
        self._map_faces(new_faces, swatch, mode="panel", bevel_thresh=bevel * 2.3)
        return [v for v in self.bm.verts if v not in prev_v and v.is_valid]

    def add_cylinder(self, radius: float, depth: float, center, swatch: str, segments: int = 8, rot_euler=(0.0, 0.0, 0.0), mode: str = "trim"):
        ret = bmesh.ops.create_cone(
            self.bm, cap_ends=True, cap_tris=False,
            segments=segments, radius1=radius, radius2=radius, depth=depth
        )
        verts = ret["verts"]
        if any(abs(a) > 1e-5 for a in rot_euler):
            rot = Euler(rot_euler, "XYZ").to_matrix()
            bmesh.ops.rotate(self.bm, cent=Vector((0.0, 0.0, 0.0)), matrix=rot, verts=verts)
        bmesh.ops.translate(self.bm, vec=Vector(center), verts=verts)
        faces = list({f for v in verts for f in v.link_faces if f.is_valid})
        self._map_faces(faces, swatch, mode=mode)
        return verts

    def add_tapered_pole(self, r_bottom: float, r_top: float, height: float, base_pos, swatch: str, segments: int = 10, tilt_euler=(0.0, 0.0, 0.0)):
        bx, by, bz = base_pos
        ret = bmesh.ops.create_cone(
            self.bm, cap_ends=True, cap_tris=False,
            segments=segments, radius1=r_bottom, radius2=r_top, depth=height
        )
        verts = ret["verts"]
        bmesh.ops.translate(self.bm, vec=Vector((0.0, 0.0, height * 0.5)), verts=verts)
        if any(abs(a) > 1e-5 for a in tilt_euler):
            rot = Euler(tilt_euler, "XYZ").to_matrix()
            bmesh.ops.rotate(self.bm, cent=Vector((0.0, 0.0, 0.0)), matrix=rot, verts=verts)
        bmesh.ops.translate(self.bm, vec=Vector((bx, by, bz)), verts=verts)
        faces = list({f for v in verts for f in v.link_faces if f.is_valid})
        self._map_faces(faces, swatch, mode="trim")

    # ---- Cellular Provincial Barangay Architectural Helpers ----

    def add_walled_room(
        self, x0: float, x1: float, y0: float, y1: float, z0: float, z1: float,
        wall_swatch: str, pillar_swatch: str = "concrete_pillar", plinth: bool = True,
        exposed_sides=("-Y", "+Y", "-X", "+X"), mid_beam: bool = True,
    ):
        """Builds a subdivided structural CHB room with 3D beveled corner & bay pilasters (haligi) + lintel/waist beams."""
        cx, cy = (x0 + x1) * 0.5, (y0 + y1) * 0.5
        w, d, h = (x1 - x0), (y1 - y0), (z1 - z0)
        if plinth and z0 < 0.05:
            self.add_box((w + 0.14, d + 0.14, 0.24), (cx, cy, 0.12), "concrete_moss", bevel=0.02)

        self.add_subdivided_box((w, d, h), (cx, cy, z0 + h * 0.5), wall_swatch, bay_max=5.4, bevel=0.024)

        # 4 beveled corner concrete haligi columns
        pw = 0.20
        for px in (x0 + pw * 0.35, x1 - pw * 0.35):
            for py in (y0 + pw * 0.35, y1 - pw * 0.35):
                self.add_box((pw, pw, h + 0.02), (px, py, z0 + h * 0.5), pillar_swatch, bevel=0.018)

        # Intermediate 3D pilasters on front (-Y), back (+Y), and eskinita side (-X/+X) walls
        nx_bays = max(1, int(round(w / 3.4)))
        for k in range(1, nx_bays):
            bx = x0 + k * (w / float(nx_bays))
            if "-Y" in exposed_sides:
                self.add_box((0.17, 0.10, h), (bx, y0 - 0.02, z0 + h * 0.5), pillar_swatch, bevel=0.0)
            if "+Y" in exposed_sides:
                self.add_box((0.17, 0.10, h), (bx, y1 + 0.02, z0 + h * 0.5), pillar_swatch, bevel=0.0)

        ny_bays = max(1, int(round(d / 3.4)))
        for k in range(1, ny_bays):
            by = y0 + k * (d / float(ny_bays))
            if "-X" in exposed_sides:
                self.add_box((0.10, 0.17, h), (x0 - 0.02, by, z0 + h * 0.5), pillar_swatch, bevel=0.0)
            if "+X" in exposed_sides:
                self.add_box((0.10, 0.17, h), (x1 + 0.02, by, z0 + h * 0.5), pillar_swatch, bevel=0.0)

        # Beveled top concrete biga tie-beam + waist-height sill band on exposed side/back walls
        self.add_box((w + 0.09, d + 0.09, 0.18), (cx, cy, z1 - 0.08), pillar_swatch, bevel=0.02)
        if mid_beam and h >= 2.75:
            z_band = z0 + 0.88
            if "+Y" in exposed_sides:
                self.add_box((w + 0.06, 0.07, 0.10), (cx, y1 + 0.02, z_band), pillar_swatch, bevel=0.0)
            if "-X" in exposed_sides:
                self.add_box((0.07, d + 0.06, 0.10), (x0 - 0.02, cy, z_band), pillar_swatch, bevel=0.0)
            if "+X" in exposed_sides:
                self.add_box((0.07, d + 0.06, 0.10), (x1 + 0.02, cy, z_band), pillar_swatch, bevel=0.0)

    def add_upper_story_volume(
        self, x0: float, x1: float, y0: float, y1: float, z0: float, z1: float,
        wall_swatch: str, trim_swatch: str = "wood_coco", exposed_sides=("-Y", "+Y", "-X", "+X"),
    ):
        """Builds a subdivided 2nd-storey sawali/plywood/plank room with beveled corner posts & exterior studs."""
        cx, cy = (x0 + x1) * 0.5, (y0 + y1) * 0.5
        w, d, h = (x1 - x0), (y1 - y0), (z1 - z0)
        self.add_subdivided_box((w, d, h), (cx, cy, z0 + h * 0.5), wall_swatch, bay_max=5.4, bevel=0.022)
        # Beveled corner timber posts
        for px in (x0 + 0.05, x1 - 0.05):
            for py in (y0 + 0.05, y1 - 0.05):
                self.add_box((0.15, 0.15, h + 0.02), (px, py, z0 + h * 0.5), trim_swatch, bevel=0.014)
        # Exterior vertical bay studs along exposed eskinita walls
        nx_bays = max(1, int(round(w / 3.4)))
        for k in range(1, nx_bays):
            bx = x0 + k * (w / float(nx_bays))
            if "-Y" in exposed_sides:
                self.add_box((0.10, 0.07, h), (bx, y0 - 0.015, z0 + h * 0.5), trim_swatch, bevel=0.0)
            if "+Y" in exposed_sides:
                self.add_box((0.10, 0.07, h), (bx, y1 + 0.015, z0 + h * 0.5), trim_swatch, bevel=0.0)
        ny_bays = max(1, int(round(d / 3.4)))
        for k in range(1, ny_bays):
            by = y0 + k * (d / float(ny_bays))
            if "-X" in exposed_sides:
                self.add_box((0.07, 0.10, h), (x0 - 0.015, by, z0 + h * 0.5), trim_swatch, bevel=0.0)
            if "+X" in exposed_sides:
                self.add_box((0.07, 0.10, h), (x1 + 0.015, by, z0 + h * 0.5), trim_swatch, bevel=0.0)

    def add_shed_yero_roof(self, x0: float, x1: float, y0: float, y1: float, z_low: float, z_high: float, swatches, slope_front_to_back: bool = True, weights_seed: int = 0):
        """Multi-sheet overlapping corrugated GI (kalawang na yero) lean-to roof + coco-lumber rafters & tire/stone weights."""
        w = x1 - x0
        d = y1 - y0
        cx, cy = (x0 + x1) * 0.5, (y0 + y1) * 0.5
        rise = z_high - z_low
        n_sheets = max(2, int(round(w / 1.75)))
        sheet_w = (w / float(n_sheets)) + 0.08
        if slope_front_to_back:
            slope_len = math.hypot(d, rise)
            pitch = math.atan2(rise, d)
            rot = (pitch, 0.0, 0.0)
        else:
            slope_len = d
            pitch = math.atan2(rise, w)
            rot = (0.0, pitch, 0.0)

        # Coco-lumber purlins under eaves
        self.add_box((w - 0.08, 0.12, 0.12), (cx, y0 + 0.18, z_low - 0.04), "wood_coco", bevel=0.0)
        self.add_box((w - 0.08, 0.12, 0.12), (cx, y1 - 0.18, (z_high if slope_front_to_back else z_low) - 0.04), "wood_coco", bevel=0.0)

        for i in range(n_sheets):
            sx = x0 + (i + 0.5) * (w / float(n_sheets))
            step_z = (i % 2) * 0.022
            sw = swatches[i % len(swatches)]
            if slope_front_to_back:
                mz = (z_low + z_high) * 0.5 + 0.06 + step_z
                self.add_box((sheet_w, slope_len, 0.07), (sx, cy, mz), sw, rot_euler=rot, bevel=0.0)
            else:
                t = (i + 0.5) / float(n_sheets)
                mz = z_low + rise * t + 0.06 + step_z
                seg_l = math.hypot(sheet_w, rise / float(n_sheets))
                self.add_box((seg_l, d, 0.07), (sx, cy, mz), sw, rot_euler=(0.0, -pitch, 0.0), bevel=0.0)

        if weights_seed > 0:
            self._place_roof_weights(x0 + 0.6, x1 - 0.6, y0 + 0.7, y1 - 0.7, z_low, z_high, slope_front_to_back, rot, weights_seed)

    def _place_roof_weights(self, x0, x1, y0, y1, z_low, z_high, slope_fb, rot, seed: int):
        """Places authentic typhoon roof weights: old rubber tires, concrete stones & scrap lumber battens."""
        tx = x0 + (x1 - x0) * (0.28 if (seed % 2 == 0) else 0.70)
        ty = y0 + (y1 - y0) * 0.42
        tz = (z_low + (z_high - z_low) * ((ty - y0) / max(y1 - y0, 0.1))) if slope_fb else ((z_low + z_high) * 0.5)
        self.add_tire_flat((tx, ty, tz + 0.13), radius=0.40, rot_euler=rot)
        bx = x0 + (x1 - x0) * (0.72 if (seed % 2 == 0) else 0.32)
        by = y0 + (y1 - y0) * 0.64
        bz = (z_low + (z_high - z_low) * ((by - y0) / max(y1 - y0, 0.1))) if slope_fb else ((z_low + z_high) * 0.5)
        self.add_box((0.42, 0.24, 0.18), (bx, by, bz + 0.12), "concrete_pillar", rot_euler=rot, bevel=0.02)
        my = (y0 + y1) * 0.5
        mz = (z_low + z_high) * 0.5 + 0.11
        self.add_box((min(x1 - x0, 2.6), 0.11, 0.07), ((x0 + x1) * 0.5, my, mz), "wood_coco", rot_euler=rot, bevel=0.0)

    def add_gable_prism(self, width_x: float, depth_y: float, base_z: float, rise_z: float, swatch: str, center_xy=(0.0, 0.0)):
        """Watertight triangular attic/pediment prism closing the space under a pitched gable roof."""
        cx, cy = center_xy
        hx = max(width_x * 0.5 - 0.16, 0.2)
        hy = max(depth_y * 0.5 - 0.22, 0.2)
        z0 = base_z - 0.04
        z1 = base_z + rise_z - 0.02
        v_fl = self.bm.verts.new((cx - hx, cy - hy, z0))
        v_fr = self.bm.verts.new((cx + hx, cy - hy, z0))
        v_ft = self.bm.verts.new((cx, cy - hy, z1))
        v_bl = self.bm.verts.new((cx - hx, cy + hy, z0))
        v_br = self.bm.verts.new((cx + hx, cy + hy, z0))
        v_bt = self.bm.verts.new((cx, cy + hy, z1))
        faces = [
            self.bm.faces.new((v_fl, v_fr, v_ft)),          # Front -Y pediment
            self.bm.faces.new((v_br, v_bl, v_bt)),          # Back +Y pediment
            self.bm.faces.new((v_fl, v_ft, v_bt, v_bl)),    # Left slope
            self.bm.faces.new((v_fr, v_br, v_bt, v_ft)),    # Right slope
            self.bm.faces.new((v_fl, v_bl, v_br, v_fr)),    # Base
        ]
        self._map_faces(faces, swatch, mode="panel", bevel_thresh=0.02)
        if rise_z >= 0.95 and hx >= 1.2:
            vw = min(hx * 0.52, 0.95)
            vh = min(rise_z * 0.36, 0.48)
            for sy in (cy - hy - 0.03, cy + hy + 0.03):
                self.add_box((vw, 0.08, vh), (cx, sy, z0 + rise_z * 0.42), "jalousie_window", bevel=0.0)

    def add_gable_yero_roof(self, width_x: float, depth_y: float, base_z: float, rise_z: float, swatches, ridge_swatch: str = "yero_zinc_weathered", center_xy=(0.0, 0.0), weights_seed: int = 0, gable_swatch: str = "sawali_aged"):
        """Multi-panel patchwork gable GI roof with closed pediment prism, wood bargeboards, ridge flashing & weights."""
        if gable_swatch:
            self.add_gable_prism(width_x - 0.35, depth_y - 0.35, base_z, rise_z, gable_swatch, center_xy=center_xy)
        cx, cy = center_xy
        half_x = width_x * 0.5
        slope_len = math.hypot(half_x, rise_z)
        angle = math.atan2(rise_z, half_x)
        n_panels = max(2, int(round(depth_y / 2.2)))
        panel_d = (depth_y / float(n_panels)) + 0.07
        y0 = cy - depth_y * 0.5

        for side_idx, sign in enumerate((-1.0, 1.0)):
            rot_y = sign * angle
            px = cx + sign * half_x * 0.5
            pz = base_z + rise_z * 0.5
            for j in range(n_panels):
                py = y0 + (j + 0.5) * (depth_y / float(n_panels))
                step = ((j + side_idx) % 2) * 0.022
                sw = swatches[(j + side_idx * 2) % len(swatches)]
                self.add_box((slope_len, panel_d, 0.08), (px, py, pz + step), sw, rot_euler=(0.0, rot_y, 0.0), bevel=0.0)

        # Ridge cap & front/back timber fascia
        self.add_box((0.34, depth_y + 0.08, 0.14), (cx, cy, base_z + rise_z + 0.04), ridge_swatch, bevel=0.02)
        for fy in (cy - depth_y * 0.5 + 0.06, cy + depth_y * 0.5 - 0.06):
            self.add_box((width_x - 0.10, 0.08, 0.16), (cx, fy, base_z + 0.02), "wood_dark", bevel=0.0)

        if weights_seed > 0:
            for sign, frac_y in ((-1.0, -0.22), (1.0, 0.25)):
                rot = (0.0, sign * angle, 0.0)
                tx = cx + sign * half_x * 0.52
                tz = base_z + rise_z * 0.48 + 0.12
                if sign < 0 or (weights_seed % 2 == 1):
                    self.add_tire_flat((tx, cy + depth_y * frac_y, tz), radius=0.39, rot_euler=rot)
                else:
                    self.add_box((0.40, 0.22, 0.18), (tx, cy + depth_y * frac_y, tz), "concrete_pillar", rot_euler=rot, bevel=0.02)

    def add_tire_flat(self, center, radius: float = 0.42, thickness: float = 0.20, rot_euler=(0.0, 0.0, 0.0)):
        """3D old black rubber tire with dark recessed wheel hub."""
        self.add_cylinder(radius, thickness, center, "rubber_tire", segments=8, rot_euler=rot_euler, mode="panel")
        self.add_cylinder(radius * 0.50, thickness + 0.02, center, "charcoal_ink", segments=6, rot_euler=rot_euler, mode="trim")

    def add_rehas_window(
        self, center, width: float, height: float, facing: str = "-Y",
        box_grill: bool = True, hood_swatch: str = "", shutters: bool = False,
    ):
        """Jalousie louver window + concrete sill/lintel + 3D protruding rusted rehas cage + angled jalousie shutters."""
        cx, cy, cz = center
        if facing in ("-Y", "+Y"):
            sgn = -1.0 if facing == "-Y" else 1.0
            self.add_box((width, 0.12, height), (cx, cy + sgn * 0.02, cz), "jalousie_window", bevel=0.0)
            self.add_box((width + 0.18, 0.22, 0.10), (cx, cy + sgn * 0.09, cz - height * 0.5 - 0.04), "concrete_pillar", bevel=0.0)
            self.add_box((width + 0.14, 0.16, 0.08), (cx, cy + sgn * 0.06, cz + height * 0.5 + 0.04), "concrete_pillar", bevel=0.0)
            if box_grill:
                gy = cy + sgn * 0.16
                for bz in (cz - height * 0.46, cz + height * 0.46):
                    self.add_box((width + 0.08, 0.20, 0.04), (cx, cy + sgn * 0.10, bz), "steel_rusted", bevel=0.0, mode="trim")
                self.add_box((width + 0.06, 0.035, 0.035), (cx, gy, cz), "steel_rusted", bevel=0.0, mode="trim")
                n_bars = max(2, int(round(width / 0.62)))
                for bi in range(n_bars + 1):
                    bx = cx - width * 0.5 + bi * (width / float(n_bars))
                    self.add_box((0.034, 0.034, height + 0.04), (bx, gy, cz), "steel_dark", bevel=0.0, mode="trim")
            if shutters:
                sh_w = min(width * 0.44, 0.58)
                for side_s in (-1.0, 1.0):
                    hx = cx + side_s * (width * 0.5 + 0.08 + sh_w * 0.38)
                    hy = cy + sgn * (0.09 + sh_w * 0.22)
                    yaw = sgn * side_s * 0.42
                    self.add_box((sh_w, 0.045, height * 0.94), (hx, hy, cz), "jalousie_window", rot_euler=(0.0, 0.0, yaw), bevel=0.0)
            if hood_swatch:
                self.add_box((width + 0.34, 0.62, 0.06), (cx, cy + sgn * 0.29, cz + height * 0.5 + 0.16), hood_swatch, rot_euler=(-sgn * 0.28, 0.0, 0.0), bevel=0.0)
        else:
            sgn = -1.0 if facing == "-X" else 1.0
            self.add_box((0.12, width, height), (cx + sgn * 0.02, cy, cz), "jalousie_window", bevel=0.0)
            self.add_box((0.20, width + 0.18, 0.10), (cx + sgn * 0.08, cy, cz - height * 0.5 - 0.04), "concrete_pillar", bevel=0.0)
            self.add_box((0.15, width + 0.14, 0.08), (cx + sgn * 0.06, cy, cz + height * 0.5 + 0.04), "concrete_pillar", bevel=0.0)
            if box_grill:
                gx = cx + sgn * 0.15
                for bz in (cz - height * 0.46, cz + height * 0.46):
                    self.add_box((0.18, width + 0.08, 0.04), (cx + sgn * 0.09, cy, bz), "steel_rusted", bevel=0.0, mode="trim")
                self.add_box((0.035, width + 0.06, 0.035), (gx, cy, cz), "steel_rusted", bevel=0.0, mode="trim")
                n_bars = max(2, int(round(width / 0.62)))
                for bi in range(n_bars + 1):
                    by = cy - width * 0.5 + bi * (width / float(n_bars))
                    self.add_box((0.034, 0.034, height + 0.04), (gx, by, cz), "steel_dark", bevel=0.0, mode="trim")
            if shutters:
                sh_w = min(width * 0.44, 0.56)
                for side_s in (-1.0, 1.0):
                    hy = cy + side_s * (width * 0.5 + 0.08 + sh_w * 0.38)
                    hx = cx + sgn * (0.08 + sh_w * 0.20)
                    yaw = -sgn * side_s * 0.40
                    self.add_box((0.045, sh_w, height * 0.94), (hx, hy, cz), "jalousie_window", rot_euler=(0.0, 0.0, yaw), bevel=0.0)
            if hood_swatch:
                self.add_box((0.50, width + 0.32, 0.06), (cx + sgn * 0.23, cy, cz + height * 0.5 + 0.15), hood_swatch, rot_euler=(0.0, sgn * 0.28, 0.0), bevel=0.0)

    def add_trapal_awning(self, center_top, width: float, reach_y: float, drop_z: float, swatch: str = "trapal_blue", props: bool = True):
        """Sloped faded tarpaulin or scrap-yero eskinita sunshade + diagonal bamboo/coco struts."""
        cx, y_wall, z_wall = center_top
        length = math.hypot(reach_y, drop_z)
        ang = math.atan2(drop_z, reach_y)
        self.add_box((width, length, 0.06), (cx, y_wall - reach_y * 0.5, z_wall - drop_z * 0.5), swatch, rot_euler=(ang, 0.0, 0.0), bevel=0.0)
        self.add_cylinder(0.035, width + 0.08, (cx, y_wall - reach_y, z_wall - drop_z), "bamboo_pole", segments=6, rot_euler=(0.0, math.pi * 0.5, 0.0))
        if props:
            for sx in (cx - width * 0.44, cx + width * 0.44):
                self.add_box((0.05, 0.05, reach_y * 1.15), (sx, y_wall - reach_y * 0.48, z_wall - drop_z - 0.35), "wood_coco", rot_euler=(-0.72, 0.0, 0.0), bevel=0.0, mode="trim")

    def add_sampayan(self, center, span_x: float = 2.4, seed: int = 1):
        """Hanging bamboo laundry pole with volumetric 3D puffed shirts (sleeves + collar) & 3D two-legged shorts."""
        cx, cy, cz = center
        self.add_cylinder(0.032, span_x, (cx, cy, cz), "bamboo_pole", segments=6, rot_euler=(0.0, math.pi * 0.5, 0.0))
        fabrics = ["laundry_warm", "laundry_cool", "trapal_orange", "trapal_blue"]
        count = max(3, min(4, int(round(span_x / 0.72))))
        for i in range(count):
            lx = cx - span_x * 0.38 + i * (span_x * 0.76 / float(max(count - 1, 1)))
            sw = fabrics[(i + seed) % len(fabrics)]
            sway = 0.07 * ((i % 2) * 2 - 1)
            # 3D wooden sipit clothespins gripping the bamboo pole
            for px_off in (-0.11, 0.11):
                self.add_box((0.026, 0.042, 0.065), (lx + px_off, cy, cz + 0.022), "wood_coco", bevel=0.0, mode="trim")
            if (i + seed) % 2 == 0:
                # Volumetric 3D puffed T-shirt: beveled 14cm-thick chest torso + 2 angled tubular sleeves + collar ring
                self.add_box((0.35, 0.14, 0.46), (lx, cy, cz - 0.25), sw, rot_euler=(sway, 0.0, 0.0), bevel=0.016)
                self.add_cylinder(0.062, 0.05, (lx, cy, cz - 0.03), "concrete_pillar", segments=6, mode="trim")
                for s_side in (-1.0, 1.0):
                    self.add_cylinder(
                        0.056, 0.20,
                        (lx + s_side * 0.22, cy, cz - 0.13),
                        sw, segments=6,
                        rot_euler=(sway, s_side * 0.92, 0.0),
                        mode="panel",
                    )
            else:
                # Volumetric 3D basketball shorts: beveled 14cm waistband block + 2 distinct tubular leg cylinders
                self.add_box((0.34, 0.14, 0.17), (lx, cy, cz - 0.10), sw, rot_euler=(sway, 0.0, 0.0), bevel=0.016)
                for l_side in (-1.0, 1.0):
                    self.add_cylinder(
                        0.074, 0.25,
                        (lx + l_side * 0.095, cy, cz - 0.28),
                        sw, segments=6,
                        rot_euler=(sway, l_side * 0.08, 0.0),
                        mode="panel",
                    )

    def add_water_drum(self, base_pos, swatch: str = "water_drum_blue"):
        """200L blue plastic barangay water drum + dark lid."""
        bx, by, bz = base_pos
        self.add_cylinder(0.29, 0.88, (bx, by, bz + 0.44), swatch, segments=8, mode="panel")
        self.add_cylinder(0.305, 0.06, (bx, by, bz + 0.89), "steel_dark", segments=8, mode="trim")

    def add_electric_meter(self, pos, facing_y: float = -1.0, riser_h: float = 1.6):
        """Galvanized Meralco kWh meter box + glass dome + service conduit pipe."""
        self.add_wall_meter_cluster(pos, facing=("-Y" if facing_y < 0.0 else "+Y"), count=1, pvc_up=riser_h, pvc_side=0.55)

    def add_wall_meter_cluster(self, pos, facing: str = "-Y", count: int = 1, pvc_up: float = 1.65, pvc_side: float = 0.65):
        """Wall-mounted galvanized kWh meter(s) + round glass cyclometer dome + vertical/horizontal PVC conduits."""
        mx, my, mz = pos
        if facing in ("-Y", "+Y"):
            sgn = -1.0 if facing == "-Y" else 1.0
            wy = my + sgn * 0.05
            for i in range(count):
                ox = (i - (count - 1) * 0.5) * 0.31
                oz = (i % 2) * 0.09
                self.add_box((0.26, 0.14, 0.35), (mx + ox, wy, mz + oz), "meter_box", bevel=0.014)
                self.add_cylinder(0.086, 0.09, (mx + ox, wy + sgn * 0.08, mz + oz + 0.04), "charcoal_ink", segments=6, rot_euler=(math.pi * 0.5, 0.0, 0.0))
            if pvc_up > 0.2:
                rx = mx + (count - 1) * 0.15 + 0.07
                self.add_cylinder(0.024, pvc_up, (rx, wy, mz + 0.17 + pvc_up * 0.5), "zinc_brushed", segments=6)
                self.add_box((0.10, 0.12, 0.09), (rx, wy + sgn * 0.03, mz + 0.17 + pvc_up), "steel_rusted", bevel=0.0)
                if pvc_side > 0.15:
                    self.add_cylinder(0.019, pvc_side, (mx - pvc_side * 0.5, wy, mz - 0.22), "trapal_orange", segments=6, rot_euler=(0.0, math.pi * 0.5, 0.0))
                    self.add_cylinder(0.019, 0.95, (mx - pvc_side, wy, mz - 0.22 + 0.475), "trapal_orange", segments=6)
                    self.add_box((0.09, 0.08, 0.09), (mx - pvc_side, wy + sgn * 0.02, mz - 0.22), "steel_dark", bevel=0.0)
        else:
            sgn = -1.0 if facing == "-X" else 1.0
            wx = mx + sgn * 0.05
            for i in range(count):
                oy = (i - (count - 1) * 0.5) * 0.31
                oz = (i % 2) * 0.09
                self.add_box((0.14, 0.26, 0.35), (wx, my + oy, mz + oz), "meter_box", bevel=0.014)
                self.add_cylinder(0.086, 0.09, (wx + sgn * 0.08, my + oy, mz + oz + 0.04), "charcoal_ink", segments=6, rot_euler=(0.0, math.pi * 0.5, 0.0))
            if pvc_up > 0.2:
                ry = my + (count - 1) * 0.15 + 0.07
                self.add_cylinder(0.024, pvc_up, (wx, ry, mz + 0.17 + pvc_up * 0.5), "zinc_brushed", segments=6)
                self.add_box((0.12, 0.10, 0.09), (wx + sgn * 0.03, ry, mz + 0.17 + pvc_up), "steel_rusted", bevel=0.0)
                if pvc_side > 0.15:
                    self.add_cylinder(0.019, pvc_side, (wx, my - pvc_side * 0.5, mz - 0.22), "trapal_orange", segments=6, rot_euler=(math.pi * 0.5, 0.0, 0.0))
                    self.add_cylinder(0.019, 0.95, (wx, my - pvc_side, mz - 0.22 + 0.475), "trapal_orange", segments=6)
                    self.add_box((0.08, 0.09, 0.09), (wx + sgn * 0.02, my - pvc_side, mz - 0.22), "steel_dark", bevel=0.0)

    def add_lpg_tank(self, base_pos):
        """11kg steel LPG cylinder + valve collar guard."""
        bx, by, bz = base_pos
        self.add_cylinder(0.17, 0.52, (bx, by, bz + 0.26), "lpg_teal", segments=8, mode="panel")
        self.add_cylinder(0.09, 0.14, (bx, by, bz + 0.56), "steel_rusted", segments=8, mode="trim")

    def add_tv_antenna(self, base_pos, height: float = 0.82):
        """Compact barrio rooftop bamboo/aluminum Yagi TV aerial."""
        bx, by, bz = base_pos
        self.add_cylinder(0.025, height, (bx, by, bz + height * 0.5), "bamboo_pole", segments=6)
        self.add_box((0.72, 0.03, 0.03), (bx, by, bz + height - 0.06), "zinc_brushed", bevel=0.0, mode="trim")
        for dy in (-0.20, 0.0, 0.20):
            self.add_box((0.03, 0.48, 0.025), (bx + dy, by, bz + height - 0.06), "zinc_brushed", bevel=0.0, mode="trim")

    def add_wire_coil(self, center, rx: float = 0.28, rz: float = 0.44, rot_yaw: float = 0.0, figure_eight: bool = False):
        """3D coiled spaghetti-wire slack loop ('dead coil') hung on utility poles."""
        cx, cy, cz = center
        segs = 8
        pts = []
        for i in range(segs):
            t = (2.0 * math.pi * i) / float(segs)
            if figure_eight:
                px = math.sin(t) * rx
                pz = math.sin(2.0 * t) * rz * 0.65
                py = math.cos(t) * 0.04
            else:
                px = math.cos(t) * rx
                pz = math.sin(t) * rz
                py = math.sin(2.0 * t) * 0.03
            rx_w = px * math.cos(rot_yaw) - py * math.sin(rot_yaw)
            ry_w = px * math.sin(rot_yaw) + py * math.cos(rot_yaw)
            pts.append(Vector((cx + rx_w, cy + ry_w, cz + pz)))

        for i in range(segs):
            p0 = pts[i]
            p1 = pts[(i + 1) % segs]
            seg_v = p1 - p0
            seg_len = seg_v.length
            if seg_len < 1e-4:
                continue
            mid = (p0 + p1) * 0.5
            quat = Vector((0.0, 0.0, 1.0)).rotation_difference(seg_v.normalized())
            self.add_cylinder(0.034, seg_len + 0.02, mid, "rubber_tire", segments=6, rot_euler=quat.to_euler("XYZ"), mode="trim")

    def add_curved_pole_notice(self, pole_xy, z_center: float, radius: float, arc_span: float, height: float, swatch: str, yaw_center: float = -math.pi * 0.5):
        """3-panel curved tin/tarpaulin notice board wrapped around a round utility post + rusted wire ties."""
        px, py = pole_xy
        n_seg = 3
        seg_ang = arc_span / float(n_seg)
        panel_w = 2.0 * radius * math.sin(seg_ang * 0.5) + 0.012
        for i in range(n_seg):
            ang = yaw_center - arc_span * 0.5 + (i + 0.5) * seg_ang
            cx = px + math.cos(ang) * radius
            cy = py + math.sin(ang) * radius
            # Local -Y of add_box should point outward along (cos(ang), sin(ang)) -> yaw = ang + pi/2
            yaw = ang + math.pi * 0.5
            self.add_box((panel_w, 0.026, height), (cx, cy, z_center), swatch, rot_euler=(0.0, 0.0, yaw), bevel=0.0)
        for wz in (z_center - height * 0.40, z_center + height * 0.40):
            self.add_cylinder(radius + 0.016, 0.020, (px, py, wz), "steel_rusted", segments=10, mode="trim")

    def _bake_ground_plinth_ao(self) -> None:
        """Slices one clean horizontal ring at Z=0.42m and bakes soft sunlit ground-plinth AO into COLOR_0."""
        if not self.bm.verts:
            return
        min_z = min(v.co.z for v in self.bm.verts)
        max_z = max(v.co.z for v in self.bm.verts)
        if min_z < 0.38 and max_z > 0.46:
            geom_all = list(self.bm.verts) + list(self.bm.edges) + list(self.bm.faces)
            bmesh.ops.bisect_plane(
                self.bm, geom=geom_all, dist=1e-4,
                plane_co=Vector((0.0, 0.0, 0.42)),
                plane_no=Vector((0.0, 0.0, 1.0)),
            )

        color_layer = self.bm.loops.layers.color.new("Color")
        for f in self.bm.faces:
            f.normal_update()
            soffit = 0.91 if (f.normal.z < -0.58) else 1.0
            for loop in f.loops:
                z = loop.vert.co.z
                if z >= 0.42:
                    ao = 1.0
                elif z <= 0.0:
                    ao = 0.72
                else:
                    t = z / 0.42
                    s = t * t * (3.0 - 2.0 * t)
                    ao = 0.72 + 0.28 * s
                if z > 1.45:
                    ao *= soffit
                ground_t = max(0.0, 1.0 - max(z, 0.0) / 0.42)
                r = min(1.0, ao * (1.0 - 0.012 * ground_t))
                g = min(1.0, ao * (1.0 + 0.010 * ground_t))
                b = min(1.0, ao * (1.0 - 0.025 * ground_t))
                loop[color_layer] = (r, g, b, 1.0)

    def export_glb(self, name: str, mat: bpy.types.Material) -> dict:
        for obj in list(bpy.data.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        for m in list(bpy.data.meshes):
            bpy.data.meshes.remove(m, do_unlink=True)

        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=1e-4)
        if name != "brgy_banderitas" and self.bm.verts:
            cur_min_z = min(v.co.z for v in self.bm.verts)
            if abs(cur_min_z) > 1e-5:
                bmesh.ops.translate(self.bm, vec=Vector((0.0, 0.0, -cur_min_z)), verts=list(self.bm.verts))
        self._bake_ground_plinth_ao()
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        open_edges = sum(1 for e in self.bm.edges if len(e.link_faces) == 1)
        min_z = min((v.co.z for v in self.bm.verts), default=0.0)
        max_z = max((v.co.z for v in self.bm.verts), default=0.0)

        mesh = bpy.data.meshes.new(name)
        self.bm.to_mesh(mesh)
        self.bm.free()

        mesh.materials.append(mat)
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(obj)
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)

        os.makedirs(OUT_DIR, exist_ok=True)
        out_path = os.path.join(OUT_DIR, f"{name}.glb")
        bpy.ops.export_scene.gltf(
            filepath=out_path,
            export_format="GLB",
            use_selection=True,
            export_yup=True,
            export_apply=True,
            export_texcoords=True,
            export_normals=True,
            export_vertex_color="MATERIAL",
            export_materials="EXPORT",
            export_image_format="JPEG",
            export_jpeg_quality=88,
        )
        _rewrite_glb_external_atlas(out_path)
        verts_n = len(mesh.vertices)
        faces_n = len(mesh.polygons)
        tris_n = sum(len(p.vertices) - 2 for p in mesh.polygons)
        size_kb = os.path.getsize(out_path) / 1024.0
        print(
            f"Exported {name}.glb | verts={verts_n} tris={tris_n} open_edges={open_edges} "
            f"z=[{min_z:.3f},{max_z:.3f}] size={size_kb:.1f}KB"
        )
        return {
            "name": name, "verts": verts_n, "faces": faces_n, "tris": tris_n,
            "open_edges": open_edges, "min_z": min_z, "max_z": max_z, "kb": size_kb,
        }


# ==============================================================================
# CELLULAR BARANGAY MODEL GENERATORS (Blender Z-up, -Y front facade, foot Z=0)
# ==============================================================================

def make_tabi_cluster_a(mat) -> dict:
    """4 cramped working-class provincial houses pressed directly wall-to-wall (zero gaps!)."""
    kb = KitBuilder()
    # Continuous mossy concrete kanal curb along the 16.8m eskinita frontage & back alley
    kb.add_box((16.9, 7.6, 0.20), (0.0, 0.0, 0.10), "concrete_moss", bevel=0.02)

    # --- UNIT 1 (-8.4 .. -4.2): 1.5-Storey Raw CHB & Scrap-Tin Barong-Barong (Exposed -X, -Y, +Y) ---
    kb.add_walled_room(-8.4, -4.2, -3.2, 3.3, 0.20, 3.35, "chb_raw_moss", plinth=False, exposed_sides=("-Y", "+Y", "-X"))
    kb.add_upper_story_volume(-8.36, -4.24, -2.7, 3.15, 3.35, 4.70, "planks_shack", trim_swatch="wood_coco", exposed_sides=("-Y", "+Y", "-X"))
    kb.add_box((0.98, 0.14, 2.05), (-7.2, -3.22, 0.20 + 1.025), "door_provincial", bevel=0.015)
    kb.add_rehas_window((-5.35, -3.22, 1.85), 1.35, 1.05, facing="-Y", box_grill=True, shutters=True)
    # Eskinita -X side & +Y back walls: protruding rusted rehas cages + jalousie shutters + kWh meters & PVC conduits
    kb.add_rehas_window((-8.41, -0.95, 1.92), 1.35, 1.02, facing="-X", box_grill=True, hood_swatch="yero_rust_heavy", shutters=True)
    kb.add_rehas_window((-8.37, 1.35, 3.98), 1.15, 0.78, facing="-X", box_grill=True, shutters=False)
    kb.add_wall_meter_cluster((-8.41, 1.95, 1.88), facing="-X", count=2, pvc_up=1.75, pvc_side=0.70)
    kb.add_rehas_window((-6.35, 3.31, 1.92), 1.40, 1.02, facing="+Y", box_grill=True, hood_swatch="yero_patchwork", shutters=True)
    kb.add_wall_meter_cluster((-4.75, 3.31, 1.85), facing="+Y", count=1, pvc_up=1.65, pvc_side=0.55)
    kb.add_trapal_awning((-6.3, -3.22, 3.05), 3.6, 1.15, 0.42, "trapal_orange", props=True)
    kb.add_water_drum((-8.0, -3.55, 0.20))
    kb.add_electric_meter((-4.35, -3.28, 1.85), riser_h=1.5)
    kb.add_shed_yero_roof(-8.65, -4.05, -3.65, 3.65, 4.55, 5.25, ["yero_rust_heavy", "yero_patchwork", "yero_zinc_weathered"], slope_front_to_back=True, weights_seed=1)

    # --- UNIT 2 (-4.2 .. +0.2): 2-Storey Cantilevered Sawali & Flaking Plywood Bahay (Exposed -Y, +Y) ---
    kb.add_walled_room(-4.2, 0.2, -3.1, 3.4, 0.20, 3.20, "chb_cream_moss", plinth=False, exposed_sides=("-Y", "+Y"))
    kb.add_box((4.44, 6.95, 0.18), (-2.0, -0.02, 3.28), "wood_coco", bevel=0.015)
    kb.add_upper_story_volume(-4.20, 0.20, -3.44, 3.41, 3.37, 6.12, "sawali_honey", trim_swatch="wood_dark", exposed_sides=("-Y", "+Y"))
    kb.add_box((2.10, 6.89, 2.65), (-3.05, -0.02, 3.37 + 1.35), "plywood_weathered", bevel=0.02)
    kb.add_box((1.05, 0.14, 2.10), (-3.1, -3.12, 0.20 + 1.05), "door_provincial", bevel=0.015)
    kb.add_rehas_window((-1.05, -3.12, 1.85), 1.45, 1.10, facing="-Y", box_grill=True, shutters=True)
    kb.add_rehas_window((-3.05, -3.46, 4.75), 1.30, 1.15, facing="-Y", box_grill=False, hood_swatch="yero_rust_heavy", shutters=True)
    kb.add_rehas_window((-0.95, -3.46, 4.75), 1.30, 1.15, facing="-Y", box_grill=True)
    # +Y back alley details on Unit 2
    kb.add_rehas_window((-2.95, 3.41, 1.88), 1.30, 1.00, facing="+Y", box_grill=True, shutters=True)
    kb.add_rehas_window((-2.0, 3.42, 4.75), 1.55, 1.10, facing="+Y", box_grill=True, hood_swatch="yero_red_oxide", shutters=True)
    kb.add_wall_meter_cluster((-0.55, 3.41, 1.90), facing="+Y", count=2, pvc_up=1.80, pvc_side=0.65)
    kb.add_sampayan((-2.0, -3.62, 3.12), span_x=2.6, seed=1)
    kb.add_sampayan((-2.0, 3.60, 3.18), span_x=2.2, seed=4)
    kb.add_gable_yero_roof(4.85, 7.45, 6.12, 1.25, ["yero_patchwork", "yero_rust_heavy", "yero_red_oxide"], ridge_swatch="yero_zinc_weathered", center_xy=(-2.0, -0.02), weights_seed=2)
    kb.add_tv_antenna((-1.1, -0.5, 7.25), height=0.75)

    # --- UNIT 3 (+0.2 .. +4.4): 1-Storey Flaking Ochre Eskinita Lugawan / Karinderya (Exposed -Y, +Y) ---
    kb.add_walled_room(0.2, 4.4, -3.0, 3.3, 0.20, 3.45, "chb_ochre_moss", plinth=False, exposed_sides=("-Y", "+Y"))
    kb.add_box((3.2, 0.78, 0.98), (2.3, -3.15, 0.20 + 0.49), "tile_teal_chipped", bevel=0.02)
    kb.add_box((3.0, 0.12, 1.25), (2.3, -3.02, 1.82), "sari_grill_counter", bevel=0.015)
    for kx in (1.35, 2.25, 3.15):
        kb.add_cylinder(0.22, 0.28, (kx, -3.15, 1.32), "zinc_brushed", segments=8, mode="panel")
    kb.add_box((2.2, 0.38, 0.44), (2.3, -3.72, 0.20 + 0.22), "wood_coco", bevel=0.015)
    kb.add_lpg_tank((0.55, -3.40, 0.20))
    # +Y back kitchen exhaust rehas window + kWh meter + back water drum
    kb.add_rehas_window((1.65, 3.31, 1.95), 1.45, 0.98, facing="+Y", box_grill=True, hood_swatch="trapal_blue", shutters=True)
    kb.add_wall_meter_cluster((3.65, 3.31, 1.88), facing="+Y", count=1, pvc_up=1.45, pvc_side=0.60)
    kb.add_water_drum((3.95, 3.52, 0.20))
    kb.add_trapal_awning((2.3, -3.02, 3.18), 3.85, 1.25, 0.46, "trapal_blue", props=True)
    kb.add_shed_yero_roof(0.05, 4.55, -3.55, 3.65, 3.45, 4.35, ["yero_red_oxide", "yero_rust_heavy", "yero_patchwork"], slope_front_to_back=True, weights_seed=3)

    # --- UNIT 4 (+4.4 .. +8.4): 2-Storey Flaking Turquoise CHB & Teal-Peel Corner House (Exposed +X, -Y, +Y) ---
    kb.add_walled_room(4.4, 8.4, -3.2, 3.4, 0.20, 3.15, "chb_mint_moss", plinth=False, exposed_sides=("-Y", "+Y", "+X"))
    kb.add_upper_story_volume(4.36, 8.44, -3.32, 3.42, 3.15, 5.80, "plywood_teal_peel", trim_swatch="wood_coco", exposed_sides=("-Y", "+Y", "+X"))
    kb.add_box((0.09, 2.8, 1.9), (8.46, 1.35, 4.55), "yero_patchwork", bevel=0.01)
    kb.add_box((0.96, 0.14, 2.05), (5.25, -3.22, 0.20 + 1.025), "door_provincial", bevel=0.015)
    kb.add_rehas_window((7.05, -3.22, 1.82), 1.35, 1.05, facing="-Y", box_grill=True, shutters=True)
    kb.add_rehas_window((6.4, -3.34, 4.55), 2.35, 1.20, facing="-Y", box_grill=True, hood_swatch="yero_zinc_weathered")
    kb.add_box((0.65, 0.24, 0.22), (5.8, -3.50, 3.88), "chb_salmon_moss", bevel=0.02)
    # Exposed +X & +Y corner eskinita walls: 3D rehas cages + jalousie shutters + kWh meters + PVC conduits
    kb.add_rehas_window((8.41, -1.15, 1.95), 1.30, 1.02, facing="+X", box_grill=True, hood_swatch="yero_rust_heavy", shutters=True)
    kb.add_rehas_window((8.45, -1.25, 4.55), 1.25, 1.02, facing="+X", box_grill=True, shutters=True)
    kb.add_wall_meter_cluster((8.41, 1.65, 1.88), facing="+X", count=2, pvc_up=1.95, pvc_side=0.70)
    kb.add_rehas_window((6.45, 3.41, 1.90), 1.40, 1.02, facing="+Y", box_grill=True, shutters=True)
    kb.add_rehas_window((6.45, 3.43, 4.55), 1.45, 1.05, facing="+Y", box_grill=True, hood_swatch="yero_zinc_weathered", shutters=True)
    kb.add_water_drum((7.95, -3.58, 0.20))
    kb.add_electric_meter((4.52, -3.28, 1.90), riser_h=1.8)
    kb.add_sampayan((6.4, -3.56, 5.62), span_x=2.2, seed=3)
    kb.add_gable_yero_roof(4.45, 7.25, 5.80, 1.18, ["yero_rust_heavy", "yero_zinc_weathered", "yero_patchwork"], ridge_swatch="yero_rust_heavy", center_xy=(6.4, 0.05), weights_seed=4)

    return kb.export_glb("brgy_tabi_cluster_a", mat)


def make_tabi_cluster_b(mat) -> dict:
    """3 asymmetric cramped looban houses pressed wall-to-wall with rooftop bubong sampayan terrace."""
    kb = KitBuilder()
    kb.add_box((14.5, 7.8, 0.20), (0.0, 0.0, 0.10), "concrete_moss", bevel=0.02)

    # --- UNIT 1 (-7.2 .. -2.2): Wide 2-Storey Flaking Terracotta CHB + Aged Sawali Dwelling (Exposed -X, -Y, +Y) ---
    kb.add_walled_room(-7.2, -2.2, -3.3, 3.4, 0.20, 3.25, "chb_salmon_moss", plinth=False, exposed_sides=("-Y", "+Y", "-X"))
    kb.add_box((5.08, 7.05, 0.18), (-4.7, -0.05, 3.33), "wood_dark", bevel=0.015)
    kb.add_upper_story_volume(-7.21, -2.19, -3.52, 3.42, 3.42, 6.22, "sawali_aged", trim_swatch="wood_dark", exposed_sides=("-Y", "+Y", "-X"))
    kb.add_box((1.05, 0.14, 2.10), (-6.1, -3.32, 0.20 + 1.05), "door_provincial", bevel=0.015)
    kb.add_rehas_window((-3.8, -3.32, 1.88), 1.65, 1.10, facing="-Y", box_grill=True, hood_swatch="trapal_orange", shutters=True)
    kb.add_rehas_window((-5.8, -3.54, 4.82), 1.45, 1.15, facing="-Y", box_grill=False, hood_swatch="yero_red_oxide", shutters=True)
    kb.add_rehas_window((-3.5, -3.54, 4.82), 1.45, 1.15, facing="-Y", box_grill=True)
    kb.add_rehas_window((-7.21, -1.15, 1.92), 1.40, 1.05, facing="-X", box_grill=True, hood_swatch="yero_rust_heavy", shutters=True)
    kb.add_rehas_window((-7.22, 0.55, 4.82), 1.45, 1.08, facing="-X", box_grill=True, shutters=True)
    kb.add_wall_meter_cluster((-7.21, 1.85, 1.88), facing="-X", count=2, pvc_up=1.85, pvc_side=0.65)
    kb.add_rehas_window((-5.65, 3.41, 1.90), 1.40, 1.02, facing="+Y", box_grill=True, shutters=True)
    kb.add_rehas_window((-4.65, 3.43, 4.80), 1.65, 1.08, facing="+Y", box_grill=True, hood_swatch="yero_patchwork", shutters=True)
    kb.add_wall_meter_cluster((-3.05, 3.41, 1.88), facing="+Y", count=1, pvc_up=1.60, pvc_side=0.55)
    kb.add_water_drum((-6.85, -3.65, 0.20))
    kb.add_sampayan((-4.6, -3.72, 3.18), span_x=2.8, seed=2)
    kb.add_gable_yero_roof(5.45, 7.55, 6.22, 1.32, ["yero_red_oxide", "yero_patchwork", "yero_rust_heavy"], ridge_swatch="yero_zinc_weathered", center_xy=(-4.7, -0.05), weights_seed=1)

    # --- UNIT 2 (-2.2 .. +2.4): Low Squeezed 1-Storey Sari-Sari / Yelo Shack (Exposed -Y, +Y) ---
    kb.add_walled_room(-2.2, 2.4, -3.1, 3.2, 0.20, 3.30, "chb_raw_moss", plinth=False, exposed_sides=("-Y", "+Y"))
    kb.add_box((2.9, 0.65, 0.96), (0.1, -3.28, 0.20 + 0.48), "tile_terracotta", bevel=0.02)
    kb.add_box((2.6, 0.15, 1.35), (0.1, -3.12, 1.86), "sari_grill_counter", bevel=0.015)
    for sx in (-0.85, -0.22, 0.42, 1.02):
        kb.add_box((0.40, 0.14, 0.95), (sx, -3.02, 2.02), "sachet_strips", bevel=0.015)
    kb.add_box((2.6, 0.15, 0.62), (0.1, -3.18, 2.92), "sign_sari_sari", bevel=0.018)
    kb.add_trapal_awning((0.1, -3.12, 3.18), 4.1, 1.25, 0.48, "trapal_striped", props=True)
    kb.add_rehas_window((-0.25, 3.21, 1.92), 1.45, 1.00, facing="+Y", box_grill=True, hood_swatch="yero_rust_heavy", shutters=True)
    kb.add_wall_meter_cluster((1.55, 3.21, 1.85), facing="+Y", count=1, pvc_up=1.35, pvc_side=0.55)
    kb.add_electric_meter((-2.02, -3.18, 1.95), riser_h=1.4)
    kb.add_lpg_tank((1.88, -3.42, 0.20))
    kb.add_shed_yero_roof(-2.3, 2.5, -3.55, 3.55, 3.30, 4.05, ["yero_rust_heavy", "yero_patchwork"], slope_front_to_back=True, weights_seed=2)

    # --- UNIT 3 (+2.4 .. +7.2): 2.5-Storey Narrow Looban Tower + Rooftop Bubong Sampayan Deck (Exposed +X, -Y, +Y) ---
    kb.add_walled_room(2.4, 7.2, -3.2, 3.4, 0.20, 3.15, "chb_sky_moss", plinth=False, exposed_sides=("-Y", "+Y", "+X"))
    kb.add_walled_room(2.4, 7.2, -3.35, 3.4, 3.15, 5.80, "chb_plaster_cracked", plinth=False, exposed_sides=("-Y", "+Y", "+X"), mid_beam=False)
    kb.add_box((1.02, 0.14, 2.08), (3.55, -3.22, 0.20 + 1.04), "door_provincial", bevel=0.015)
    kb.add_rehas_window((5.65, -3.22, 1.82), 1.55, 1.08, facing="-Y", box_grill=True, shutters=True)
    kb.add_rehas_window((3.65, -3.38, 4.50), 1.35, 1.12, facing="-Y", box_grill=True)
    kb.add_rehas_window((5.85, -3.38, 4.50), 1.35, 1.12, facing="-Y", box_grill=False, hood_swatch="yero_zinc_weathered", shutters=True)
    kb.add_rehas_window((7.21, -1.05, 1.90), 1.35, 1.02, facing="+X", box_grill=True, hood_swatch="trapal_blue", shutters=True)
    kb.add_rehas_window((7.21, -0.25, 4.50), 1.45, 1.10, facing="+X", box_grill=True, shutters=True)
    kb.add_wall_meter_cluster((7.21, 1.85, 1.92), facing="+X", count=2, pvc_up=2.65, pvc_side=0.75)
    kb.add_rehas_window((4.85, 3.41, 1.90), 1.45, 1.02, facing="+Y", box_grill=True, shutters=True)
    kb.add_rehas_window((4.85, 3.41, 4.50), 1.50, 1.08, facing="+Y", box_grill=True, hood_swatch="yero_red_oxide", shutters=True)
    kb.add_wall_meter_cluster((3.15, 3.41, 1.88), facing="+Y", count=1, pvc_up=2.20, pvc_side=0.55)
    kb.add_subdivided_box((4.84, 6.80, 0.92), (4.8, 0.02, 5.80 + 0.46), "chb_cream_moss", bay_max=5.4, bevel=0.02)
    kb.add_upper_story_volume(2.50, 7.10, 0.05, 3.25, 5.80, 7.85, "plywood_weathered", trim_swatch="wood_coco", exposed_sides=("-Y", "+Y", "+X"))
    for px in (2.65, 6.95):
        kb.add_box((0.09, 0.09, 1.95), (px, -3.10, 5.80 + 0.975), "steel_rusted", bevel=0.0, mode="trim")
    kb.add_sampayan((4.8, -2.55, 7.35), span_x=3.0, seed=4)
    kb.add_water_drum((6.45, -2.65, 5.85))
    kb.add_shed_yero_roof(2.25, 7.35, -3.55, 3.65, 7.55, 8.15, ["yero_patchwork", "yero_zinc_weathered", "yero_rust_heavy"], slope_front_to_back=True, weights_seed=3)
    kb.add_tv_antenna((3.1, 2.2, 8.05), height=0.72)

    return kb.export_glb("brgy_tabi_cluster_b", mat)


def make_shophouse_row(mat) -> dict:
    """3-Bay 90s Provincial Commercial Shophouse Strip: Karinderya, Barberya & Vulcanizing Shop (15.2m x 7.6m)."""
    kb = KitBuilder()
    kb.add_box((15.3, 7.6, 0.20), (0.0, 0.0, 0.10), "concrete_moss", bevel=0.02)

    # --- Bay 1 (-7.6 .. -2.5): Karinderya / Lutong Bahay (Kaldero Pots, Tiled Counter, Stools, Awning & Sign) ---
    kb.add_walled_room(-7.6, -2.5, -3.0, 3.3, 0.20, 3.55, "chb_ochre_moss", plinth=False, exposed_sides=("-Y", "+Y", "-X"))
    kb.add_box((4.1, 0.86, 1.02), (-5.05, -3.16, 0.20 + 0.51), "tile_terracotta", bevel=0.02)
    kb.add_box((3.9, 0.15, 1.26), (-5.05, -3.02, 1.88), "sari_grill_counter", bevel=0.015)
    # 4 3D brushed-aluminum kaldero cooking pots + domed lids & top handles on the turo-turo counter
    for kx in (-6.55, -5.55, -4.55, -3.60):
        kb.add_cylinder(0.23, 0.28, (kx, -3.22, 1.36), "zinc_brushed", segments=8, mode="panel")
        kb.add_cylinder(0.245, 0.045, (kx, -3.22, 1.52), "steel_dark", segments=8, mode="trim")
        kb.add_box((0.08, 0.05, 0.05), (kx, -3.22, 1.56), "charcoal_ink", bevel=0.0, mode="trim")
    # 3 wooden/orange bangko stools along the counter + teal LPG tank
    for sx in (-6.35, -5.05, -3.75):
        kb.add_box((0.44, 0.44, 0.46), (sx, -3.70, 0.20 + 0.23), "trapal_orange", bevel=0.02)
    kb.add_lpg_tank((-7.22, -3.42, 0.20))
    # Prominent beveled "KARINDERYA / LUTONG BAHAY" fascia signboard + striped trapal awning
    kb.add_box((4.15, 0.16, 0.80), (-5.05, -3.12, 3.52), "sign_karinderya", bevel=0.02)
    kb.add_trapal_awning((-5.05, -3.02, 3.08), 4.45, 1.18, 0.44, "trapal_striped", props=True)
    # Stenciled "BAWAL UMIHI DITO" mossy side-alley CHB base plaque + eskinita windows/meters
    kb.add_box((0.14, 1.65, 0.76), (-7.62, -1.85, 0.20 + 0.38), "chb_bawal_umihi", bevel=0.016)
    kb.add_rehas_window((-7.61, 0.15, 1.95), 1.35, 1.02, facing="-X", box_grill=True, hood_swatch="yero_red_oxide", shutters=True)
    kb.add_wall_meter_cluster((-7.61, 1.95, 1.88), facing="-X", count=2, pvc_up=1.55, pvc_side=0.65)
    kb.add_rehas_window((-5.05, 3.31, 1.95), 1.55, 1.02, facing="+Y", box_grill=True, hood_swatch="yero_rust_heavy", shutters=True)
    kb.add_gable_yero_roof(5.30, 6.95, 3.55, 1.15, ["yero_red_oxide", "yero_rust_heavy", "yero_patchwork"], ridge_swatch="yero_zinc_weathered", center_xy=(-5.05, 0.15), weights_seed=1)

    # --- Bay 2 (-2.5 .. +2.5): 2-Storey 90s Barberya + Striped 3D Barber Pole Cylinder + Sawali Upstairs ---
    kb.add_walled_room(-2.5, 2.5, -3.1, 3.4, 0.20, 3.25, "chb_sky_moss", plinth=False, exposed_sides=("-Y", "+Y"))
    kb.add_upper_story_volume(-2.52, 2.52, -3.36, 3.42, 3.25, 5.88, "plywood_weathered", trim_swatch="wood_coco", exposed_sides=("-Y", "+Y"))
    kb.add_box((2.40, 6.82, 2.55), (1.15, 0.03, 3.25 + 1.30), "sawali_honey", bevel=0.02)
    kb.add_box((1.02, 0.15, 2.10), (-1.55, -3.12, 0.20 + 1.05), "door_provincial", bevel=0.016)
    kb.add_rehas_window((0.85, -3.12, 1.84), 2.35, 1.28, facing="-Y", box_grill=True)
    # 3D Striped Rotating Barber Pole Cylinder + Brushed-Zinc Chrome End Caps & Iron Wall Brackets!
    bp_x, bp_y, bp_z = -2.28, -3.38, 2.15
    kb.add_cylinder(0.155, 0.98, (bp_x, bp_y, bp_z), "barber_pole_stripe", segments=10, mode="panel")
    for cap_dz in (-0.52, 0.52):
        kb.add_cylinder(0.18, 0.11, (bp_x, bp_y, bp_z + cap_dz), "zinc_brushed", segments=10, mode="panel")
        kb.add_box((0.08, 0.30, 0.06), (bp_x, bp_y + 0.16, bp_z + cap_dz * 0.88), "steel_dark", bevel=0.0, mode="trim")
    kb.add_cylinder(0.09, 0.08, (bp_x, bp_y, bp_z + 0.61), "lamp_warm", segments=8, mode="panel")
    # Prominent beveled "BARBERYA / GUPIT POGI" fascia signboard
    kb.add_box((4.15, 0.16, 0.78), (0.0, -3.20, 3.08), "sign_barberya", bevel=0.02)
    kb.add_rehas_window((-1.15, -3.38, 4.58), 1.35, 1.10, facing="-Y", box_grill=False, hood_swatch="yero_rust_heavy", shutters=True)
    kb.add_rehas_window((1.15, -3.38, 4.58), 1.35, 1.10, facing="-Y", box_grill=True)
    # +Y back alley details + volumetric 3D sampayan laundry
    kb.add_box((0.94, 0.14, 2.02), (-1.45, 3.41, 0.20 + 1.01), "door_provincial", bevel=0.015)
    kb.add_rehas_window((0.75, 3.41, 1.90), 1.45, 1.02, facing="+Y", box_grill=True, shutters=True)
    kb.add_rehas_window((0.0, 3.43, 4.58), 1.65, 1.08, facing="+Y", box_grill=True, hood_swatch="yero_zinc_weathered", shutters=True)
    kb.add_wall_meter_cluster((2.05, 3.41, 1.88), facing="+Y", count=2, pvc_up=1.75, pvc_side=0.60)
    kb.add_sampayan((0.0, -3.58, 5.58), span_x=2.6, seed=2)
    kb.add_gable_yero_roof(5.30, 7.35, 5.88, 1.22, ["yero_zinc_weathered", "yero_patchwork", "yero_rust_heavy"], ridge_swatch="yero_red_oxide", center_xy=(0.0, 0.05), weights_seed=2)

    # --- Bay 3 (+2.5 .. +7.6): Vulcanizing Shop (3D Hanging Tires + Horizontal Air Compressor Tank + BAWAL UMIHI DITO) ---
    kb.add_walled_room(2.5, 7.6, -3.1, 3.4, 0.20, 3.35, "chb_raw_moss", plinth=False, exposed_sides=("-Y", "+Y", "+X"))
    kb.add_upper_story_volume(2.48, 7.62, -3.20, 3.44, 3.35, 5.68, "planks_shack", trim_swatch="wood_dark", exposed_sides=("-Y", "+Y", "+X"))
    # Open recessed talyer service bay + rolled iron shutter header
    kb.add_box((2.75, 0.16, 2.22), (4.35, -3.12, 0.20 + 1.11), "charcoal_ink", bevel=0.016)
    kb.add_box((2.85, 0.24, 0.26), (4.35, -3.20, 2.50), "steel_rusted", bevel=0.02)
    # Prominent beveled "VULCANIZING / SHOP" fascia signboard
    kb.add_box((4.25, 0.16, 0.80), (5.05, -3.20, 3.10), "sign_talyer", bevel=0.02)
    # Stenciled "BAWAL UMIHI DITO" base wall panel along the front-right mossy CHB apron!
    kb.add_box((1.65, 0.15, 0.78), (6.62, -3.14, 0.20 + 0.39), "chb_bawal_umihi", bevel=0.016)
    # Horizontal painted crimson-steel Air Compressor Tank + motor block + pressure manifold + steel support legs
    comp_x, comp_y, comp_z = 3.95, -3.52, 0.62
    kb.add_cylinder(0.26, 1.24, (comp_x, comp_y, comp_z), "jeep_crimson", segments=10, rot_euler=(0.0, math.pi * 0.5, 0.0), mode="panel")
    for leg_dx in (-0.42, 0.42):
        kb.add_box((0.10, 0.36, 0.26), (comp_x + leg_dx, comp_y, 0.20 + 0.13), "steel_dark", bevel=0.0, mode="trim")
    kb.add_box((0.36, 0.28, 0.24), (comp_x - 0.24, comp_y, comp_z + 0.34), "steel_dark", bevel=0.02)
    kb.add_cylinder(0.11, 0.26, (comp_x + 0.24, comp_y, comp_z + 0.34), "steel_ochre", segments=8, rot_euler=(0.0, math.pi * 0.5, 0.0), mode="panel")
    # Overhead rusted iron tire-rack arm + 2 3D Hanging Rubber Tires + 3 stacked sidewalk tires!
    kb.add_box((0.10, 0.92, 0.10), (6.95, -3.48, 2.68), "steel_rusted", bevel=0.0, mode="trim")
    for h_idx, (hy, hz, yaw_t) in enumerate(((-3.46, 2.04, 0.25), (-3.82, 1.88, -0.35))):
        kb.add_cylinder(0.022, 0.52, (6.95, hy, 2.40 - h_idx * 0.08), "steel_dark", segments=6)
        kb.add_tire_flat((6.95, hy, hz), radius=0.38, thickness=0.19, rot_euler=(math.pi * 0.5, 0.0, yaw_t))
    for tz in range(3):
        kb.add_tire_flat((5.55, -3.52, 0.30 + tz * 0.20), radius=0.41, thickness=0.19)
    # 2nd-storey & +X/+Y eskinita details on Bay 3
    kb.add_rehas_window((5.05, -3.24, 4.50), 1.85, 1.05, facing="-Y", box_grill=True, hood_swatch="trapal_orange", shutters=True)
    kb.add_rehas_window((7.61, -0.95, 1.92), 1.35, 1.02, facing="+X", box_grill=True, hood_swatch="yero_patchwork", shutters=True)
    kb.add_rehas_window((7.63, 0.65, 4.50), 1.30, 0.98, facing="+X", box_grill=True, shutters=True)
    kb.add_wall_meter_cluster((7.61, 1.85, 1.88), facing="+X", count=2, pvc_up=1.90, pvc_side=0.65)
    kb.add_rehas_window((5.05, 3.41, 1.92), 1.45, 1.00, facing="+Y", box_grill=True, shutters=True)
    kb.add_rehas_window((5.05, 3.45, 4.50), 1.50, 0.98, facing="+Y", box_grill=True, hood_swatch="yero_rust_heavy", shutters=True)
    kb.add_shed_yero_roof(2.40, 7.80, -3.55, 3.65, 5.58, 6.38, ["yero_patchwork", "yero_rust_heavy", "yero_zinc_weathered"], slope_front_to_back=True, weights_seed=4)
    kb.add_tv_antenna((6.8, 1.4, 6.28), height=0.76)

    return kb.export_glb("brgy_shophouse_row", mat)


def make_sari_sari(mat) -> dict:
    """90s Province Corner Sari-Sari Store — Tindahan ni Aling Nena (8.0m x 6.8m x 5.6m)."""
    kb = KitBuilder()
    kb.add_box((8.0, 6.8, 0.22), (0.0, 0.0, 0.11), "concrete_moss", bevel=0.02)
    kb.add_walled_room(-3.7, 3.7, -2.5, 3.1, 0.22, 3.55, "chb_mint_moss", plinth=False, exposed_sides=("-Y", "+Y", "-X", "+X"))

    # Chipped teal-tile front counter + 3D iron rehas security grille cage with bentahan pass-through opening
    kb.add_box((4.65, 0.84, 1.06), (-0.35, -2.78, 0.22 + 0.53), "tile_teal_chipped", bevel=0.025)
    kb.add_box((4.30, 0.16, 1.58), (-0.35, -2.56, 2.08), "sari_grill_counter", bevel=0.016)
    # Real 3D protruding rusted iron security bars framing the sari-sari counter cage
    for bx in (-2.42, -1.55, -0.88, 0.18, 0.85, 1.72):
        kb.add_box((0.038, 0.18, 1.56), (bx, -2.68, 2.08), "steel_rusted", bevel=0.0, mode="trim")
    for bz in (1.34, 2.82):
        kb.add_box((4.34, 0.20, 0.045), (-0.35, -2.68, bz), "steel_rusted", bevel=0.0, mode="trim")

    # Bamboo display rod + 5 volumetric beveled 3D hanging chichirya & shampoo packet strips!
    kb.add_cylinder(0.028, 4.30, (-0.35, -2.84, 2.78), "bamboo_pole", segments=6, rot_euler=(0.0, math.pi * 0.5, 0.0))
    for i, sx in enumerate((-2.15, -1.42, -0.70, 0.62, 1.35)):
        kb.add_box(
            (0.44, 0.14, 1.16),
            (sx, -2.84, 2.16),
            "sachet_strips",
            rot_euler=(0.05 * ((i % 2) * 2 - 1), 0.0, 0.04 * (i - 2)),
            bevel=0.016,
        )

    # Front timber entrance door + stenciled "BAWAL UMIHI DITO" side plaque + eskinita 3D rehas cages & meters
    kb.add_box((0.98, 0.15, 2.08), (2.65, -2.52, 0.22 + 1.04), "door_provincial", bevel=0.016)
    kb.add_box((0.15, 1.65, 0.78), (-3.74, -1.65, 0.22 + 0.39), "chb_bawal_umihi", bevel=0.016)
    kb.add_rehas_window((-3.71, 0.15, 1.95), 1.45, 1.08, facing="-X", box_grill=True, hood_swatch="yero_rust_heavy", shutters=True)
    kb.add_wall_meter_cluster((-3.71, 1.95, 1.90), facing="-X", count=1, pvc_up=1.55, pvc_side=0.65)
    kb.add_rehas_window((3.71, -0.25, 1.95), 1.45, 1.08, facing="+X", box_grill=True, hood_swatch="yero_zinc_weathered", shutters=True)
    kb.add_wall_meter_cluster((3.71, 1.90, 1.92), facing="+X", count=2, pvc_up=1.55, pvc_side=0.65)
    kb.add_rehas_window((-1.75, 3.11, 1.98), 1.40, 1.05, facing="+Y", box_grill=True, hood_swatch="yero_patchwork", shutters=True)
    kb.add_rehas_window((1.55, 3.11, 1.98), 1.40, 1.05, facing="+Y", box_grill=True, shutters=True)
    kb.add_wall_meter_cluster((0.0, 3.11, 1.92), facing="+Y", count=1, pvc_up=1.50, pvc_side=0.55)
    kb.add_lpg_tank((-3.15, 3.26, 0.22))

    # Sloped oxidized red-oxide yero & striped trapal front awning propped on coco-lumber posts
    kb.add_trapal_awning((-0.35, -2.52, 3.22), 5.4, 1.34, 0.48, "yero_red_oxide", props=False)
    for px in (-2.90, 2.20):
        kb.add_box((0.14, 0.14, 2.75), (px, -3.74, 0.22 + 1.375), "wood_coco", bevel=0.014)
    # Prominent beveled hand-painted fascia signboard: "TINDAHAN NI ALING NENA" + timber frame backing
    kb.add_box((4.92, 0.14, 0.94), (-0.35, -2.66, 3.72), "wood_coco", bevel=0.016)
    kb.add_box((4.76, 0.16, 0.84), (-0.35, -2.72, 3.72), "sign_sari_sari", bevel=0.02)

    # Tambay wooden bangko bench + blue ice-tubig cooler + water drum + Meralco meter
    kb.add_box((2.15, 0.46, 0.46), (-1.45, -3.32, 0.22 + 0.23), "wood_coco", bevel=0.02)
    kb.add_box((0.95, 0.62, 0.68), (1.25, -3.26, 0.22 + 0.34), "water_drum_blue", bevel=0.025)
    kb.add_box((0.99, 0.66, 0.14), (1.25, -3.26, 0.22 + 0.72), "plywood_weathered", bevel=0.015)
    kb.add_water_drum((3.25, -3.05, 0.22))
    kb.add_electric_meter((3.45, -2.56, 1.95), riser_h=1.55)

    # Sawali gable pediment & patchwork oxidized rusted yero roof with tire + stone weights
    kb.add_gable_yero_roof(8.0, 6.3, 3.55, 1.42, ["yero_red_oxide", "yero_patchwork", "yero_rust_heavy"], ridge_swatch="yero_zinc_weathered", center_xy=(0.0, 0.3), weights_seed=1, gable_swatch="sawali_honey")
    return kb.export_glb("brgy_sari_sari", mat)


def make_house_two_storey(mat) -> dict:
    """Authentic 2-Storey Provincial CHB + Cantilevered Sawali/Plywood Bahay + Water Tank Tower."""
    kb = KitBuilder()
    kb.add_box((8.4, 7.8, 0.24), (0.0, 0.0, 0.12), "concrete_moss", bevel=0.02)

    kb.add_walled_room(-3.8, 2.8, -3.1, 3.2, 0.24, 3.28, "chb_salmon_moss", plinth=False, exposed_sides=("-Y", "+Y", "-X", "+X"))
    kb.add_box((7.15, 6.95, 0.20), (-0.5, -0.05, 3.36), "wood_coco", bevel=0.015)
    kb.add_upper_story_volume(-4.02, 3.02, -3.45, 3.35, 3.46, 6.31, "plywood_teal_peel", trim_swatch="wood_dark", exposed_sides=("-Y", "+Y", "-X", "+X"))
    kb.add_box((3.20, 6.84, 2.75), (-2.1, -0.05, 3.46 + 1.38), "sawali_honey", bevel=0.02)

    kb.add_box((1.10, 0.14, 2.18), (-0.5, -3.12, 0.24 + 1.09), "door_provincial", bevel=0.015)
    kb.add_trapal_awning((-0.5, -3.12, 2.85), 2.15, 0.95, 0.32, "yero_zinc_weathered", props=True)
    for wx in (-2.45, 1.35):
        kb.add_rehas_window((wx, -3.12, 1.92), 1.45, 1.12, facing="-Y", box_grill=True, shutters=True)
        kb.add_rehas_window((wx, -3.46, 4.88), 1.60, 1.18, facing="-Y", box_grill=False, hood_swatch="yero_rust_heavy", shutters=True)
        kb.add_box((1.68, 0.36, 0.28), (wx, -3.66, 4.16), "chb_salmon_moss", bevel=0.02)
        kb.add_box((1.55, 0.34, 0.30), (wx, -3.68, 4.42), "bougainvillea", bevel=0.03)

    # Eskinita side (-X/+X) & rear (+Y) walls: 3D pilasters, protruding rusted rehas cages, jalousie shutters, kWh meters & PVC conduits!
    kb.add_rehas_window((-3.81, -0.85, 1.92), 1.40, 1.05, facing="-X", box_grill=True, hood_swatch="yero_rust_heavy", shutters=True)
    kb.add_rehas_window((-4.03, 0.35, 4.88), 1.50, 1.10, facing="-X", box_grill=True, hood_swatch="yero_patchwork", shutters=True)
    kb.add_wall_meter_cluster((-3.81, 1.85, 1.92), facing="-X", count=2, pvc_up=1.95, pvc_side=0.70)

    kb.add_rehas_window((2.81, -1.25, 1.92), 1.35, 1.05, facing="+X", box_grill=True, shutters=True)
    kb.add_rehas_window((3.03, -1.25, 4.88), 1.40, 1.08, facing="+X", box_grill=True, hood_swatch="yero_zinc_weathered", shutters=True)
    kb.add_wall_meter_cluster((2.81, 0.15, 1.90), facing="+X", count=1, pvc_up=1.80, pvc_side=0.55)

    kb.add_box((0.96, 0.14, 2.08), (-2.35, 3.21, 0.24 + 1.04), "door_provincial", bevel=0.015)
    kb.add_rehas_window((0.85, 3.21, 1.92), 1.45, 1.06, facing="+Y", box_grill=True, hood_swatch="trapal_orange", shutters=True)
    kb.add_rehas_window((-2.15, 3.36, 4.88), 1.45, 1.10, facing="+Y", box_grill=True, shutters=True)
    kb.add_rehas_window((1.15, 3.36, 4.88), 1.45, 1.10, facing="+Y", box_grill=True, hood_swatch="yero_red_oxide", shutters=True)
    kb.add_wall_meter_cluster((-0.85, 3.21, 1.92), facing="+Y", count=2, pvc_up=1.75, pvc_side=0.60)

    kb.add_sampayan((-0.5, -3.68, 3.22), span_x=2.6, seed=3)
    kb.add_electric_meter((2.45, -3.16, 1.92), riser_h=1.65)
    kb.add_water_drum((-3.55, -3.45, 0.24))

    # Gable patchwork GI roof with old tire & stone weights
    kb.add_gable_yero_roof(7.65, 7.45, 6.31, 1.38, ["yero_patchwork", "yero_zinc_weathered", "yero_rust_heavy"], ridge_swatch="yero_red_oxide", center_xy=(-0.5, -0.05), weights_seed=2)

    # Side welded angle-iron water tower + blue plastic tank
    tx, ty = 3.45, 1.25
    for lx in (-0.50, 0.50):
        for ly in (-0.50, 0.50):
            kb.add_box((0.11, 0.11, 5.55), (tx + lx, ty + ly, 0.24 + 2.775), "steel_rusted", bevel=0.0, mode="trim")
    for bz in (2.2, 4.1):
        kb.add_box((1.08, 1.08, 0.08), (tx, ty, bz), "steel_dark", bevel=0.0, mode="trim")
    kb.add_box((1.36, 1.36, 0.14), (tx, ty, 5.84), "steel_dark", bevel=0.015)
    kb.add_cylinder(0.62, 1.58, (tx, ty, 6.70), "water_drum_blue", segments=12, mode="panel")
    kb.add_tv_antenna((-2.2, 1.1, 7.52), height=0.72)
    return kb.export_glb("brgy_house_two_storey", mat)


def make_covered_court(mat) -> dict:
    """Walk-Through Provincial Barangay Covered Basketball Court (18m x 22m x 8.8m)."""
    kb = KitBuilder()
    w, l = 18.0, 22.0
    col_h = 6.2
    # 6 sturdy concrete columns with chipped ochre/hazard bumper plinths (open court interior!)
    for sx in (-w * 0.5 + 0.6, w * 0.5 - 0.6):
        for sy in (-l * 0.5 + 1.2, 0.0, l * 0.5 - 1.2):
            kb.add_box((0.92, 0.92, 0.35), (sx, sy, 0.175), "hazard_curb", bevel=0.03)
            kb.add_box((0.82, 0.82, 1.10), (sx, sy, 0.35 + 0.55), "steel_ochre", bevel=0.03)
            kb.add_box((0.66, 0.66, col_h - 1.45), (sx, sy, 1.45 + (col_h - 1.45) * 0.5), "concrete_pillar", bevel=0.025)

    # Longitudinal steel eaves beams & riveted tie-trusses
    for sx in (-w * 0.5 + 0.6, w * 0.5 - 0.6):
        kb.add_box((0.32, l + 0.8, 0.42), (sx, 0.0, col_h + 0.16), "steel_dark", bevel=0.02)
    for sy in (-l * 0.5 + 1.2, 0.0, l * 0.5 - 1.2):
        kb.add_box((w - 0.8, 0.24, 0.30), (0.0, sy, col_h + 0.12), "steel_rusted", bevel=0.015)
        for lx in (-4.4, 4.4):
            kb.add_box((0.58, 0.40, 0.24), (lx, sy, col_h - 0.12), "lamp_warm", bevel=0.02)

    # Multi-segment barrel arch roof of weathered barrio green & oxidized zinc yero sheets
    span_x = w + 1.4
    len_y = l + 1.6
    panels = 10
    arch_rise = 2.45
    swatches = ["yero_green_faded", "yero_zinc_weathered", "yero_green_faded", "yero_patchwork"]
    for i in range(panels):
        t0 = -1.0 + 2.0 * i / float(panels)
        t1 = -1.0 + 2.0 * (i + 1) / float(panels)
        x0, x1 = t0 * (span_x * 0.5), t1 * (span_x * 0.5)
        z0 = col_h + 0.25 + arch_rise * (1.0 - t0 * t0)
        z1 = col_h + 0.25 + arch_rise * (1.0 - t1 * t1)
        seg_len = math.hypot(x1 - x0, z1 - z0) + 0.08
        ang = math.atan2(z1 - z0, x1 - x0)
        kb.add_box((seg_len, len_y, 0.14), ((x0 + x1) * 0.5, 0.0, (z0 + z1) * 0.5), swatches[i % len(swatches)], rot_euler=(0.0, -ang, 0.0), bevel=0.015)

    # Front & Rear Barangay Liga Arch Fascia Signboards
    for sy in (-l * 0.5 - 0.35, l * 0.5 + 0.35):
        kb.add_box((10.4, 0.26, 1.10), (0.0, sy, col_h + 1.42), "sign_brgy_hall", bevel=0.02)

    # Twin suspended steel-frame plywood basketball backboards & rusted orange hoops
    for end_sign in (-1.0, 1.0):
        hy = end_sign * (l * 0.5 - 2.2)
        kb.add_box((0.20, 0.20, 2.65), (0.0, hy, col_h - 1.12), "steel_dark", bevel=0.015)
        kb.add_box((1.82, 0.12, 1.12), (0.0, hy - end_sign * 0.28, 3.55), "backboard_worn", bevel=0.015)
        kb.add_cylinder(0.27, 0.06, (0.0, hy - end_sign * 0.62, 3.05), "trapal_orange", segments=12, mode="trim")
    return kb.export_glb("brgy_covered_court", mat)


def make_hall_plaza(mat) -> dict:
    """2-Storey Provincial Barangay Hall Outpost + Balcony + Trompa PA Speakers (16m x 10.2m x 9.5m)."""
    kb = KitBuilder()
    kb.add_box((16.2, 10.2, 0.42), (0.0, 0.0, 0.21), "concrete_moss", bevel=0.03)
    kb.add_walled_room(-7.5, 7.5, -3.4, 4.5, 0.42, 4.15, "chb_cream_moss", plinth=False)
    kb.add_walled_room(-7.8, 7.8, -3.7, 4.6, 4.15, 7.45, "chb_ochre_moss", plinth=False)

    # Front portico columns + concrete balcony deck & terracotta balustrade
    for px in (-5.4, -2.0, 2.0, 5.4):
        kb.add_box((0.46, 0.46, 3.75), (px, -4.45, 0.42 + 1.875), "concrete_pillar", bevel=0.025)
    kb.add_box((12.4, 1.85, 0.30), (0.0, -4.15, 4.18), "concrete_pillar", bevel=0.02)
    kb.add_box((12.4, 0.22, 0.92), (0.0, -4.95, 4.78), "tile_terracotta", bevel=0.02)

    # Double wooden hall doors + rehas & jalousie windows + Barangay signboard
    kb.add_box((2.55, 0.22, 2.65), (0.0, -3.44, 0.42 + 1.325), "door_provincial", bevel=0.02)
    for wx in (-4.9, 4.9):
        kb.add_rehas_window((wx, -3.42, 2.30), 2.25, 1.35, facing="-Y", box_grill=True)
        kb.add_rehas_window((wx, -3.72, 5.75), 2.25, 1.35, facing="-Y", box_grill=False, hood_swatch="yero_red_oxide")
        kb.add_rehas_window((wx, 4.62, 5.75), 2.10, 1.25, facing="+Y", box_grill=False)
    kb.add_box((4.4, 0.22, 1.15), (0.0, -3.78, 5.85), "sign_brgy_hall", bevel=0.02)

    # Patchwork oxidized red-oxide GI gable roof + front portico gable
    kb.add_gable_yero_roof(16.8, 9.6, 7.45, 1.95, ["yero_red_oxide", "yero_patchwork", "yero_rust_heavy"], ridge_swatch="yero_zinc_weathered", center_xy=(0.0, 0.45), weights_seed=2, gable_swatch="chb_plaster_cracked")
    kb.add_gable_yero_roof(7.2, 2.8, 6.65, 1.15, ["yero_red_oxide", "yero_zinc_weathered"], ridge_swatch="concrete_pillar", center_xy=(0.0, -4.15), weights_seed=0, gable_swatch="sign_brgy_hall")

    # Rooftop Barangay PA horn speakers (trompa) + front flagpole
    for sx in (-0.45, 0.45):
        kb.add_tapered_pole(0.08, 0.26, 0.55, (sx, -4.85, 7.85), "zinc_brushed", segments=8, tilt_euler=(0.95, sx * 0.4, 0.0))
    kb.add_tapered_pole(0.11, 0.05, 7.2, (0.0, -4.95, 0.42), "zinc_brushed", segments=8)
    kb.add_box((1.22, 0.06, 0.70), (0.62, -4.95, 7.05), "trapal_striped", bevel=0.008)
    return kb.export_glb("brgy_hall_plaza", mat)


def make_palengke_stall(mat) -> dict:
    """Provincial Talipapa / Palengke Wet-Market Canopy Stall (6.4m x 4.4m x 3.8m)."""
    kb = KitBuilder()
    kb.add_box((6.2, 4.2, 0.16), (0.0, 0.0, 0.08), "concrete_moss", bevel=0.02)
    for sx in (-2.85, 2.85):
        for sy in (-1.85, 1.85):
            kb.add_box((0.13, 0.13, 3.05), (sx, sy, 0.16 + 1.525), "wood_coco", bevel=0.01)
    # Mossy CHB counter base + chipped teal-tile wet-market slab
    kb.add_box((5.4, 2.0, 0.72), (0.0, 0.0, 0.16 + 0.36), "chb_raw_moss", bevel=0.02)
    kb.add_box((5.65, 2.25, 0.18), (0.0, 0.0, 0.97), "tile_teal_chipped", bevel=0.02)
    # Wooden produce crates + woven rattan bilao baskets
    items = [(-1.95, "produce_mango"), (-0.65, "leaf_banana"), (0.65, "sawali_aged"), (1.95, "sachet_strips")]
    for bx, sw in items:
        kb.add_box((1.05, 1.35, 0.30), (bx, 0.0, 1.21), "wood_coco", bevel=0.02)
        kb.add_box((0.90, 1.18, 0.20), (bx, 0.0, 1.42), sw, bevel=0.025)
    # Hanging market pan scale (timbangan)
    kb.add_cylinder(0.02, 0.65, (0.0, -0.85, 2.55), "steel_dark", segments=6)
    kb.add_cylinder(0.22, 0.06, (0.0, -0.85, 2.20), "zinc_brushed", segments=10, mode="panel")
    # Patched faded trapal tarpaulin + scrap yero gable canopy
    kb.add_gable_yero_roof(6.6, 4.7, 2.98, 0.88, ["trapal_blue", "trapal_striped", "trapal_orange", "yero_rust_heavy"], ridge_swatch="wood_coco", weights_seed=1, gable_swatch="")
    return kb.export_glb("brgy_palengke_stall", mat)


def make_waiting_shed(mat) -> dict:
    """Provincial Barangay TODA / Roadside Waiting Shed (5.8m x 3.0m x 3.5m)."""
    kb = KitBuilder()
    kb.add_box((6.0, 3.2, 0.20), (0.0, 0.0, 0.10), "concrete_moss", bevel=0.02)
    kb.add_box((6.04, 0.28, 0.22), (0.0, -1.48, 0.11), "hazard_curb", bevel=0.02)
    # Waist-high mossy CHB back cover wall + terracotta bench
    kb.add_box((5.25, 0.26, 1.22), (0.0, 1.15, 0.20 + 0.61), "chb_cream_moss", bevel=0.025)
    kb.add_box((4.80, 0.58, 0.14), (0.0, 0.72, 0.62), "tile_terracotta", bevel=0.015)
    for bx in (-1.8, 0.0, 1.8):
        kb.add_box((0.24, 0.52, 0.40), (bx, 0.72, 0.20 + 0.20), "chb_raw_moss", bevel=0.015)
    for sx in (-2.55, 2.55):
        for sy in (-1.12, 1.12):
            kb.add_cylinder(0.085, 2.92, (sx, sy, 0.20 + 1.46), "steel_ochre", segments=8)
    kb.add_box((3.2, 0.10, 0.48), (0.0, -1.22, 2.96), "sign_talyer", bevel=0.01)
    kb.add_gable_yero_roof(6.3, 3.6, 3.08, 0.64, ["yero_green_faded", "yero_patchwork", "yero_rust_heavy"], ridge_swatch="yero_zinc_weathered", weights_seed=1, gable_swatch="")
    return kb.export_glb("brgy_waiting_shed", mat)


def make_footbridge(mat) -> dict:
    """Estero / Sapa Provincial Concrete & Rusted-Pipe Crossing Bridge (9.2m wide x 14.2m span)."""
    kb = KitBuilder()
    # Underside concrete girders rest cleanly at Z=0.0 (glTF min_y == 0.0); mossy concrete deck sits above
    for gx in (-3.2, 0.0, 3.2):
        kb.add_box((0.42, 14.0, 0.32), (gx, 0.0, 0.16), "concrete_moss", bevel=0.02)
    kb.add_box((9.2, 14.2, 0.52), (0.0, 0.0, 0.50), "concrete_moss", bevel=0.03)
    # Raised weathered hazard curbs & rusted iron pipe + rebar railings on -X and +X flanks
    for sx in (-4.35, 4.35):
        kb.add_box((0.50, 14.2, 0.30), (sx, 0.0, 0.90), "hazard_curb", bevel=0.02)
        for py in (-6.5, -3.9, -1.3, 1.3, 3.9, 6.5):
            kb.add_box((0.18, 0.18, 1.08), (sx, py, 1.05 + 0.54), "concrete_pillar", bevel=0.015)
        for ry in range(-6, 7):
            kb.add_box((0.04, 0.04, 0.92), (sx, ry * 1.0, 1.52), "steel_rusted", bevel=0.0, mode="trim")
        kb.add_box((0.11, 13.6, 0.09), (sx, 0.0, 2.08), "steel_ochre", bevel=0.01)
        kb.add_box((0.09, 13.6, 0.07), (sx, 0.0, 1.60), "steel_rusted", bevel=0.01)
    return kb.export_glb("brgy_footbridge", mat)


def make_jeepney(mat) -> dict:
    """Weathered Provincial Sarao-Style Pinoy Jeepney + Top-Load Cargo Rack (2.5m x 7.2m x 2.85m)."""
    kb = KitBuilder()
    # Chassis & front bumper (-Y front)
    kb.add_box((2.22, 6.75, 0.32), (0.0, 0.0, 0.54), "steel_dark", bevel=0.025)
    kb.add_box((2.28, 0.28, 0.28), (0.0, -3.55, 0.56), "zinc_brushed", bevel=0.025)
    for mx in (-0.88, 0.88):
        kb.add_box((0.36, 0.05, 0.28), (mx, -3.45, 0.30), "rubber_tire", bevel=0.005)
    # Rounded pressed-zinc engine hood & radiator grille
    kb.add_box((1.84, 1.92, 0.90), (0.0, -2.52, 1.10), "zinc_brushed", bevel=0.05)
    kb.add_box((1.88, 0.22, 0.78), (0.0, -3.48, 1.04), "rehas_grill", bevel=0.02)
    for hx in (-0.64, 0.64):
        kb.add_cylinder(0.17, 0.12, (hx, -3.58, 1.12), "lamp_warm", segments=10, rot_euler=(math.pi * 0.5, 0.0, 0.0), mode="panel")
        kb.add_box((0.09, 0.30, 0.20), (hx * 0.58, -3.18, 1.64), "zinc_brushed", bevel=0.015)

    # Passenger cabin body (ochre + crimson + cobalt provincial livery bands, zero white clip!)
    kb.add_box((2.32, 4.92, 1.04), (0.0, 0.86, 1.16), "jeep_ochre", bevel=0.035)
    kb.add_box((2.36, 4.84, 0.30), (0.0, 0.86, 1.22), "jeep_crimson", bevel=0.015)
    kb.add_box((2.35, 4.84, 0.18), (0.0, 0.86, 0.80), "trapal_blue", bevel=0.015)
    # Interior benches & dark passenger bay recess
    kb.add_box((1.95, 4.55, 0.55), (0.0, 0.92, 1.82), "charcoal_ink", bevel=0.01)
    # Split windshield + route signboard + cabin window pillars
    kb.add_box((2.12, 0.14, 0.78), (0.0, -1.50, 2.00), "jalousie_window", rot_euler=(-0.22, 0.0, 0.0), bevel=0.015)
    for px in (-1.06, 1.06):
        for py in (-1.45, -0.15, 1.35, 3.12):
            kb.add_box((0.10, 0.10, 0.86), (px, py, 2.04), "zinc_brushed", bevel=0.01)
        kb.add_cylinder(0.025, 4.4, (px, 0.95, 1.76), "zinc_brushed", segments=6, rot_euler=(math.pi * 0.5, 0.0, 0.0))

    # Brushed-zinc curved roof + front route destination header + top-load cargo rack!
    kb.add_box((2.38, 5.35, 0.18), (0.0, 0.95, 2.50), "zinc_brushed", bevel=0.04)
    kb.add_box((1.72, 0.28, 0.28), (0.0, -1.60, 2.64), "sign_brgy_hall", bevel=0.02)
    kb.add_box((1.75, 2.85, 0.14), (0.0, 1.25, 2.66), "wood_coco", bevel=0.015)
    kb.add_box((0.95, 1.15, 0.34), (-0.25, 1.15, 2.86), "sandbag_burlap", bevel=0.04)
    kb.add_box((0.68, 0.85, 0.30), (0.48, 1.55, 2.84), "trapal_blue", bevel=0.02)

    # Side-mounted spare tire + rear stepboard + 4 wheels
    kb.add_tire_flat((-1.22, -1.05, 1.18), radius=0.40, thickness=0.20, rot_euler=(0.0, math.pi * 0.5, 0.0))
    kb.add_box((1.45, 0.34, 0.10), (0.0, 3.45, 0.56), "zinc_brushed", bevel=0.015)
    for wx in (-1.10, 1.10):
        for wy in (-2.35, 2.05):
            kb.add_cylinder(0.47, 0.30, (wx, wy, 0.47), "rubber_tire", segments=12, rot_euler=(0.0, math.pi * 0.5, 0.0), mode="panel")
            kb.add_cylinder(0.23, 0.33, (wx, wy, 0.47), "zinc_brushed", segments=10, rot_euler=(0.0, math.pi * 0.5, 0.0), mode="panel")
    return kb.export_glb("brgy_jeepney", mat)


def make_tricycle(mat) -> dict:
    """Provincial Barangay TODA Motorcycle + Welded Sheet-Metal Sidecar (2.0m x 2.8m x 1.95m)."""
    kb = KitBuilder()
    # Motorcycle left side (-0.55)
    kb.add_box((0.32, 2.15, 0.56), (-0.55, 0.0, 0.68), "steel_dark", bevel=0.025)
    kb.add_box((0.36, 0.68, 0.32), (-0.55, -0.25, 1.05), "jeep_crimson", bevel=0.03)
    kb.add_box((0.34, 0.78, 0.18), (-0.55, 0.42, 1.02), "rubber_tire", bevel=0.025)
    kb.add_box((0.88, 0.09, 0.09), (-0.55, -0.64, 1.28), "zinc_brushed", bevel=0.01)
    kb.add_cylinder(0.11, 0.10, (-0.55, -0.82, 1.14), "lamp_warm", segments=8, rot_euler=(math.pi * 0.5, 0.0, 0.0), mode="panel")
    kb.add_cylinder(0.055, 1.15, (-0.72, 0.35, 0.48), "zinc_brushed", segments=6, rot_euler=(math.pi * 0.47, 0.0, 0.0))
    for wy in (-0.88, 0.88):
        kb.add_cylinder(0.35, 0.16, (-0.55, wy, 0.35), "rubber_tire", segments=12, rot_euler=(0.0, math.pi * 0.5, 0.0), mode="panel")
        kb.add_cylinder(0.17, 0.18, (-0.55, wy, 0.35), "zinc_brushed", segments=8, rot_euler=(0.0, math.pi * 0.5, 0.0), mode="trim")

    # Barrio sidecar right side (+0.42)
    kb.add_box((1.06, 1.85, 0.76), (0.42, 0.05, 0.68), "trapal_blue", bevel=0.035)
    kb.add_box((1.09, 1.20, 0.28), (0.42, -0.18, 0.66), "zinc_brushed", bevel=0.02)
    kb.add_box((1.04, 0.12, 0.58), (0.42, -0.72, 1.28), "jalousie_window", rot_euler=(-0.30, 0.0, 0.0), bevel=0.015)
    kb.add_box((1.22, 2.08, 0.11), (0.32, 0.05, 1.76), "trapal_orange", rot_euler=(0.05, 0.0, 0.0), bevel=0.02)
    kb.add_box((0.68, 0.08, 0.24), (0.42, -0.96, 1.68), "sign_talyer", bevel=0.01)
    for py in (-0.75, 0.85):
        kb.add_box((0.06, 0.06, 0.76), (0.90, py, 1.38), "zinc_brushed", bevel=0.0, mode="trim")
    kb.add_cylinder(0.35, 0.18, (1.02, 0.15, 0.35), "rubber_tire", segments=12, rot_euler=(0.0, math.pi * 0.5, 0.0), mode="panel")
    kb.add_tire_flat((0.42, 1.06, 0.82), radius=0.30, thickness=0.14, rot_euler=(math.pi * 0.5, 0.0, 0.0))
    return kb.export_glb("brgy_tricycle", mat)


def make_sorbetes_cart(mat) -> dict:
    """Traditional Hand-Painted Wooden Kalye Sorbetes Pushcart + Trapal Umbrella."""
    kb = KitBuilder()
    kb.add_box((1.02, 1.62, 0.96), (0.0, 0.0, 0.85), "jeep_ochre", bevel=0.035)
    kb.add_box((1.06, 1.66, 0.26), (0.0, 0.0, 0.85), "jeep_crimson", bevel=0.02)
    kb.add_box((1.05, 1.65, 0.16), (0.0, 0.0, 0.46), "trapal_blue", bevel=0.015)
    # Push-handle bar & brass sorbetes handbell at +Y
    kb.add_cylinder(0.03, 0.96, (0.0, 0.98, 1.24), "wood_dark", segments=6, rot_euler=(0.0, math.pi * 0.5, 0.0))
    kb.add_tapered_pole(0.07, 0.02, 0.14, (0.36, 0.98, 1.26), "steel_ochre", segments=8)
    # 3 Stainless canister lids
    for cy in (-0.46, 0.0, 0.46):
        kb.add_cylinder(0.17, 0.14, (0.0, cy, 1.40), "zinc_brushed", segments=10, mode="panel")
    # Spoked wooden side wheels + front iron caster
    for wx in (-0.58, 0.58):
        kb.add_cylinder(0.45, 0.10, (wx, -0.05, 0.45), "wood_coco", segments=12, rot_euler=(0.0, math.pi * 0.5, 0.0), mode="panel")
        kb.add_cylinder(0.14, 0.13, (wx, -0.05, 0.45), "steel_rusted", segments=8, rot_euler=(0.0, math.pi * 0.5, 0.0), mode="trim")
    kb.add_cylinder(0.19, 0.09, (0.0, -0.80, 0.19), "rubber_tire", segments=10, rot_euler=(0.0, math.pi * 0.5, 0.0), mode="panel")
    # Bamboo umbrella mast + 8-sided faded striped trapal canopy
    kb.add_cylinder(0.045, 2.50, (0.0, -0.12, 1.62), "bamboo_pole", segments=8)
    kb.add_tapered_pole(1.42, 0.10, 0.52, (0.0, -0.12, 2.48), "trapal_striped", segments=8)
    return kb.export_glb("brgy_sorbetes_cart", mat)


def make_meralco_post(mat, name: str, banner_swatch: str, lean_rad: float = 0.056) -> dict:
    """Weathered Leaning Provincial Electric Post + Transformer + 4 Meter Boxes + Figure-8 Spaghetti Wire Coils!"""
    kb = KitBuilder()
    # Mossy cracked concrete curb collar + faded hazard band
    kb.add_box((1.10, 1.10, 0.34), (0.0, 0.0, 0.17), "concrete_moss", bevel=0.03)
    kb.add_box((0.92, 0.92, 0.36), (0.0, 0.0, 0.52), "hazard_curb", bevel=0.025)

    # Leaning weathered concrete pole (tilts along +X by lean_rad so top shifts ~0.78m!)
    pole_h = 13.9
    kb.add_tapered_pole(0.34, 0.19, pole_h, (0.0, 0.0, 0.65), "concrete_pillar", segments=10, tilt_euler=(0.0, lean_rad, -0.018))

    def pole_pt(z_h: float):
        dz = max(z_h - 0.65, 0.0)
        return (dz * math.sin(lean_rad), dz * 0.018, z_h)

    # 4 tilted galvanized kWh meter / telco junction boxes clustered down mid-pole (Z = 2.3 .. 4.9m)
    meter_specs = [
        (2.35, -0.28, -0.08, 0.09),
        (2.95, 0.26, 0.12, -0.11),
        (3.65, -0.25, 0.16, 0.07),
        (4.45, 0.24, -0.14, -0.08),
    ]
    for mz, ox, oy, tilt_z in meter_specs:
        px, py, pz = pole_pt(mz)
        kb.add_box((0.30, 0.20, 0.40), (px + ox, py + oy, pz), "meter_box", rot_euler=(tilt_z * 0.5, tilt_z, oy), bevel=0.015)
        kb.add_cylinder(0.085, 0.08, (px + ox, py + oy - 0.11, pz + 0.05), "charcoal_ink", segments=8, rot_euler=(math.pi * 0.5, 0.0, 0.0))
    # Bundled steel riser conduits strapped along pole trunk
    px_m, py_m, _ = pole_pt(5.2)
    kb.add_cylinder(0.045, 7.6, (px_m - 0.22, py_m - 0.12, 4.8), "steel_rusted", segments=6, rot_euler=(0.0, lean_rad, 0.0))

    # Heavy weathered cylindrical pole-mounted transformer drum + HV porcelain bushings (Z = 8.7m)
    tx, ty, tz = pole_pt(8.75)
    kb.add_box((0.55, 0.16, 0.16), (tx + 0.30, ty, tz), "steel_dark", bevel=0.01)
    kb.add_cylinder(0.43, 1.32, (tx + 0.62, ty, tz), "steel_rusted", segments=12, mode="panel")
    kb.add_cylinder(0.45, 0.10, (tx + 0.62, ty, tz + 0.64), "steel_dark", segments=12, mode="trim")
    for bx in (0.46, 0.76):
        kb.add_cylinder(0.06, 0.34, (tx + bx, ty, tz + 0.84), "concrete_pillar", segments=6)

    # 3 Staggered Tilted Crossarms (Telco rack + Secondary LV + Primary HV) & Ceramic Insulators
    crossarms = [
        (7.45, 2.15, "steel_rusted", -0.06, 4),
        (11.65, 2.75, "wood_dark", 0.05, 4),
        (13.25, 2.95, "wood_dark", -0.04, 4),
    ]
    for cz, span, sw, arm_tilt, n_ins in crossarms:
        cx, cy, _ = pole_pt(cz)
        kb.add_box((span, 0.18, 0.18), (cx, cy, cz), sw, rot_euler=(0.0, arm_tilt, 0.04), bevel=0.015)
        # Diagonal steel V-brace under crossarm
        for sgn in (-1.0, 1.0):
            kb.add_box((0.85, 0.05, 0.05), (cx + sgn * 0.42, cy, cz - 0.28), "steel_rusted", rot_euler=(0.0, sgn * 0.58, 0.0), bevel=0.0, mode="trim")
        for ii in range(n_ins):
            ix = cx + (-0.42 + 0.84 * (ii / float(n_ins - 1))) * span
            kb.add_cylinder(0.075, 0.24, (ix, cy, cz + 0.19), "zinc_brushed", segments=8, mode="panel")

    # Coiled Figure-8 & Oval Spaghetti-Wire Slack Loops ("Dead Coils") overloaded on the pole!
    coil_spots = [
        (6.45, -0.34, 0.10, 0.34, 0.56, 0.2, True),
        (7.25, 0.48, -0.08, 0.38, 0.62, -0.3, True),
        (7.50, -0.78, 0.05, 0.32, 0.48, 0.1, False),
        (10.35, -0.32, -0.12, 0.36, 0.58, 0.4, True),
        (11.55, 0.82, 0.06, 0.30, 0.46, -0.2, False),
    ]
    for cz, ox, oy, rx, rz, yaw, fig8 in coil_spots:
        px, py, _ = pole_pt(cz)
        kb.add_wire_coil((px + ox, py + oy, cz), rx=rx, rz=rz, rot_yaw=yaw, figure_eight=fig8)

    # Rusted Gooseneck Barrio Streetlamp Arm + Warm Amber Lens
    lx, ly, lz = pole_pt(9.95)
    kb.add_box((1.55, 0.10, 0.10), (lx - 0.72, ly, lz), "steel_rusted", rot_euler=(0.0, -0.24, 0.0), bevel=0.01)
    kb.add_box((0.54, 0.34, 0.20), (lx - 1.46, ly, lz + 0.14), "lamp_warm", bevel=0.025)

    # Pole-mounted 90s election ("VOTE! HALALAN '95") & siphoning ("MALABANAN") curved notice boards (Z = 1.85..2.85m)
    nx1, ny1, _ = pole_pt(2.08)
    kb.add_curved_pole_notice((nx1, ny1), 2.08, 0.345, 1.32, 0.46, "poster_malabanan", yaw_center=-math.pi * 0.50)
    nx2, ny2, _ = pole_pt(2.62)
    kb.add_curved_pole_notice((nx2, ny2), 2.62, 0.338, 1.32, 0.46, "poster_vote_halalan", yaw_center=-math.pi * 0.28)

    # Team Base Banner (for Agawang Base brgy_post_blue / brgy_post_red)
    if banner_swatch:
        bx, by, bz = pole_pt(5.55)
        kb.add_box((1.38, 0.09, 3.45), (bx + 0.82, by, bz), banner_swatch, bevel=0.015)
        kb.add_box((1.50, 0.13, 0.13), (bx + 0.82, by, bz + 1.78), "steel_ochre", bevel=0.015)
        kb.add_box((1.50, 0.11, 0.11), (bx + 0.82, by, bz - 1.76), "steel_dark", bevel=0.01)

    return kb.export_glb(name, mat)


def make_poso_well(mat) -> dict:
    """Communal Provincial Barangay Deep-Well Hand Pump (Poso) + Mossy Wash Apron + Timba & Batya."""
    kb = KitBuilder()
    # Cracked mossy octagonal concrete wash apron + raised wet drainage rim
    kb.add_cylinder(1.48, 0.18, (0.0, 0.0, 0.09), "concrete_moss", segments=8, mode="panel")
    for ang_i in range(8):
        a = ang_i * (math.pi / 4.0)
        kb.add_box((1.12, 0.16, 0.10), (math.cos(a) * 1.34, math.sin(a) * 1.34, 0.21), "concrete_pillar", rot_euler=(0.0, 0.0, a + math.pi * 0.5), bevel=0.015)

    # Central concrete pump block + cast-iron poso barrel, top flange, spout & long handle (bomba)
    kb.add_box((0.58, 0.58, 0.34), (0.0, 0.0, 0.18 + 0.17), "chb_raw_moss", bevel=0.02)
    kb.add_cylinder(0.14, 0.92, (0.0, 0.0, 0.52 + 0.46), "lpg_teal", segments=10, mode="panel")
    kb.add_cylinder(0.20, 0.28, (0.0, 0.0, 1.48), "lpg_teal", segments=10, mode="panel")
    kb.add_cylinder(0.23, 0.08, (0.0, 0.0, 1.64), "steel_rusted", segments=10, mode="trim")
    # Curved cast-iron water spout (-Y front)
    kb.add_box((0.11, 0.42, 0.11), (0.0, -0.26, 1.34), "steel_rusted", rot_euler=(0.25, 0.0, 0.0), bevel=0.015)
    kb.add_cylinder(0.06, 0.16, (0.0, -0.44, 1.24), "charcoal_ink", segments=8)
    # Pivoted long iron pump handle (+Y side, angled up ready to pump!)
    kb.add_box((0.06, 1.08, 0.07), (0.0, 0.44, 1.84), "steel_rusted", rot_euler=(-0.48, 0.0, 0.0), bevel=0.01)
    kb.add_cylinder(0.03, 0.38, (0.0, 0.0, 1.72), "steel_dark", segments=6)

    # Blue plastic timba pail under spout + galvanized bucket + wide laundry batya basin
    kb.add_tapered_pole(0.15, 0.19, 0.36, (0.0, -0.52, 0.18), "water_drum_blue", segments=10)
    kb.add_tapered_pole(0.14, 0.18, 0.34, (-0.58, -0.28, 0.18), "zinc_brushed", segments=10)
    kb.add_tapered_pole(0.36, 0.44, 0.22, (0.68, -0.25, 0.18), "trapal_orange", segments=12)
    return kb.export_glb("brgy_poso_well", mat)


def make_cover_wall(mat) -> dict:
    """Waist-High Tactical Hollow-Block Barricade + Exposed Rebar + Sandbags + Tires (4.2m x 1.0m x 1.42m)."""
    kb = KitBuilder()
    kb.add_box((4.2, 0.48, 1.16), (0.0, 0.0, 0.58), "chb_raw_moss", bevel=0.025)
    kb.add_box((4.28, 0.56, 0.20), (0.0, 0.0, 1.26), "hazard_curb", bevel=0.02)
    for rx in (-1.6, -0.5, 0.7, 1.7):
        kb.add_cylinder(0.016, 0.22, (rx, 0.0, 1.41), "steel_rusted", segments=5)
    for sx in (-1.42, 1.42):
        kb.add_box((0.94, 0.48, 0.32), (sx, -0.44, 0.16), "sandbag_burlap", bevel=0.05)
        kb.add_box((0.82, 0.44, 0.28), (sx, -0.40, 0.45), "sandbag_burlap", bevel=0.05)
    kb.add_tire_flat((0.0, 0.44, 0.44), radius=0.44, thickness=0.24, rot_euler=(math.pi * 0.42, 0.0, 0.0))
    return kb.export_glb("brgy_cover_wall", mat)


def make_banderitas(mat) -> dict:
    """Weathered Barrio Kalye Banderitas Span (12.0m across X, hung at Z = 4.35..5.0m with catenary sag)."""
    kb = KitBuilder()
    span = 12.0
    flags = 18
    colors = ["jeep_ochre", "jeep_crimson", "trapal_blue", "trapal_orange", "laundry_warm", "yero_green_faded"]
    prev_pt = None
    for i in range(flags + 1):
        t = i / float(flags)
        x = (t - 0.5) * span
        sag = 0.65 * (1.0 - (2.0 * t - 1.0) ** 2)
        pt = Vector((x, 0.0, 5.0 - sag))
        if prev_pt is not None:
            seg = pt - prev_pt
            mid = (pt + prev_pt) * 0.5
            ang = math.atan2(seg.z, seg.x)
            kb.add_box((seg.length + 0.02, 0.035, 0.035), mid, "charcoal_ink", rot_euler=(0.0, -ang, 0.0), bevel=0.0, mode="trim")
            # Triangular-look tapered pennant hanging from wire segment midpoint
            kb.add_box((0.32, 0.04, 0.42), (mid.x, 0.0, mid.z - 0.23), colors[(i - 1) % len(colors)], rot_euler=(0.08 * ((i % 2) * 2 - 1), 0.0, 0.0), bevel=0.006)
        prev_pt = pt
    return kb.export_glb("brgy_banderitas", mat)


def make_poster_cluster(mat) -> dict:
    """90s Province Pole/Corner Notice & Signage Cluster (~1.68m x 0.84m x 2.48m, min_z=0)."""
    kb = KitBuilder()
    # Rusted iron pole-collar brackets & coco-lumber vertical mounting battens grounded at Z=0
    for bz in (0.04, 0.62, 1.24, 1.86):
        kb.add_cylinder(0.28, 0.06, (0.0, 0.0, bz), "steel_rusted", segments=10, mode="trim")
    for sx in (-0.30, 0.30):
        kb.add_box((0.07, 0.07, 2.02), (sx, -0.26, 1.01), "wood_coco", bevel=0.0, mode="trim")
    # Weathered mossy CHB / plaster base curb plaque with stenciled "BAWAL UMIHI DITO"
    kb.add_box((1.06, 0.20, 0.50), (-0.08, -0.32, 0.25), "chb_raw_moss", bevel=0.018)
    kb.add_box((0.98, 0.14, 0.44), (-0.08, -0.38, 0.26), "chb_bawal_umihi", bevel=0.016)
    # Curved "MALABANAN" siphoning tin notice + angled "VOTE! HALALAN '95" campaign tarpaulin board
    kb.add_curved_pole_notice((0.0, 0.0), 0.86, 0.30, 1.45, 0.52, "poster_malabanan", yaw_center=-math.pi * 0.46)
    kb.add_box((0.96, 0.10, 0.62), (-0.12, -0.33, 1.52), "wood_coco", rot_euler=(0.04, 0.0, -0.08), bevel=0.014)
    kb.add_box((0.90, 0.14, 0.56), (-0.12, -0.35, 1.52), "poster_vote_halalan", rot_euler=(0.04, 0.0, -0.08), bevel=0.016)
    # Side steel sign-post arm + beveled "NO ENTRY / BAWAL DITO" street sign plate
    kb.add_cylinder(0.038, 2.42, (0.54, -0.10, 1.21), "steel_dark", segments=8)
    kb.add_box((0.58, 0.07, 0.07), (0.28, -0.10, 1.92), "steel_rusted", bevel=0.0, mode="trim")
    kb.add_box((0.68, 0.14, 0.60), (0.54, -0.14, 2.16), "sign_no_entry", rot_euler=(0.0, 0.0, 0.06), bevel=0.018)
    return kb.export_glb("brgy_poster_cluster", mat)


def _verify_exports(stats: list) -> None:
    """Verifies all 18 GLBs exist, stay <=6500 tris, carry COLOR_0 vertex AO + external BarangayAtlas.jpg URI, and checks .jpg.import."""
    assert len(stats) == 18, f"Expected 18 models, got {len(stats)}"
    for item in stats:
        assert 40 <= item["tris"] <= 6500, f"{item['name']}.glb tris={item['tris']} outside [40, 6500]!"
        assert item["open_edges"] == 0, f"{item['name']}.glb has {item['open_edges']} open edges (must be 0 watertight)!"
        p = os.path.join(OUT_DIR, f"{item['name']}.glb")
        assert os.path.isfile(p) and os.path.getsize(p) > 1024, f"Missing or empty GLB: {p}"
        with open(p, "rb") as gf:
            raw = gf.read()
        j_len, _ = struct.unpack_from("<II", raw, 12)
        gltf = json.loads(raw[20:20 + j_len].decode("utf-8"))
        attrs = gltf["meshes"][0]["primitives"][0]["attributes"]
        assert "COLOR_0" in attrs, f"{item['name']}.glb missing baked COLOR_0 vertex AO!"
        assert gltf["images"][0].get("uri") == ATLAS_FILENAME, f"{item['name']}.glb missing external {ATLAS_FILENAME} URI!"

    # Verify every .jpg.import in assets/models/barangay/ has compress/mode=2, mipmaps, vram_texture, and zero md5 lines
    jpg_imports = [f for f in os.listdir(OUT_DIR) if f.endswith(".jpg.import")]
    assert len(jpg_imports) >= 1, "Expected BarangayAtlas.jpg.import in assets/models/barangay/"
    for imp in jpg_imports:
        txt = open(os.path.join(OUT_DIR, imp), "r", encoding="utf-8").read()
        assert "compress/mode=2" in txt, f"{imp} missing compress/mode=2!"
        assert "mipmaps/generate=true" in txt, f"{imp} missing mipmaps/generate=true!"
        assert '"vram_texture": true' in txt, f'{imp} missing "vram_texture": true!'
        assert "md5" not in txt.lower() and "generator_parameters" not in txt, f"{imp} must never contain md5/generator_parameters!"

    # Round-trip test importing brgy_tabi_cluster_a.glb in Blender 5.2
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(OUT_DIR, "brgy_tabi_cluster_a.glb"))
    imported_meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    assert len(imported_meshes) == 1, f"Expected 1 single-draw-call mesh, got {len(imported_meshes)}"
    assert len(imported_meshes[0].data.color_attributes) >= 1, "Expected COLOR_0 attribute on re-imported mesh!"

    total_verts = sum(s["verts"] for s in stats)
    total_tris = sum(s["tris"] for s in stats)
    total_kb = sum(s["kb"] for s in stats)
    atlas_kb = os.path.getsize(os.path.join(OUT_DIR, ATLAS_FILENAME)) / 1024.0
    print("=" * 78)
    print(f"VERIFIED ALL 18 BARANGAY GLB MODELS + COLOR_0 AO + 1 SHARED 2048x2048 ATLAS ({atlas_kb:.1f} KB)")
    print(f"Total Kit Geometry: {total_verts} verts | {total_tris} tris | {total_kb:.1f} KB across 18 GLBs")
    print("=" * 78)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    atlas = build_atlas_image()
    mat = build_shared_material(atlas)

    stats = [
        make_covered_court(mat),
        make_hall_plaza(mat),
        make_footbridge(mat),
        make_waiting_shed(mat),
        make_sari_sari(mat),
        make_house_two_storey(mat),
        make_shophouse_row(mat),
        make_palengke_stall(mat),
        make_sorbetes_cart(mat),
        make_meralco_post(mat, "brgy_meralco_post", "", lean_rad=0.056),
        make_meralco_post(mat, "brgy_post_blue", "team_blue", lean_rad=0.022),
        make_meralco_post(mat, "brgy_post_red", "team_red", lean_rad=0.022),
        make_cover_wall(mat),
        make_banderitas(mat),
        make_tabi_cluster_a(mat),
        make_tabi_cluster_b(mat),
        make_poso_well(mat),
        make_poster_cluster(mat),
    ]
    _verify_exports(stats)


if __name__ == "__main__":
    main()
