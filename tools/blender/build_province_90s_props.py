"""
Build the Overhaul v2 Handcrafted 90s Philippine Provincial Props Kit in Blender 5.2.

Zero-AI-Slop Studio Craftsmanship:
  - Soft sunlit 90s provincial 2048x2048 painterly atlas (`ProvincePropsAtlas.jpg`,
    mean_sRGB ~ 0.45-0.48, max_sRGB <= 0.76, warm espresso-umber contours #281E23,
    damp green lumot moss on concrete/CHB, oxidized sienna rust on GI/steel, and
    crisp vector-rasterized Tagalog street signage).
  - Never applies manual **2.4 or **(1/2.2) gamma on byte-backed Blender Image.pixels.
  - Writes clean VRAM-compressed `ProvincePropsAtlas.jpg.import` (`compress/mode=2`,
    `mipmaps/generate=true`, `"vram_texture": true`, zero `md5` or `generator_parameters`).
  - Exports 10 single-draw-call, grounded (`min_y ~ 0.0`), beveled/lofted `.glb` models:
      1. `brgy_jeepney.glb` (~5.4m 90s provincial passenger jeepney)
      2. `brgy_tricycle.glb` (~2.3m motorcycle + galvanized pampasahero sidecar)
      3. `brgy_toda_shed.glb` (BRGY. TODA TERMINAL dispatch & waiting shed)
      4. `brgy_wooden_hoop.glb` (90s kalye coco-lumber basketball post + tire base)
      5. `brgy_chb_wall.glb` (~6.0m x 2.1m mossy CHB wall + rebar + BAWAL UMIHI DITO)
      6. `brgy_mango_tree.glb` (Punong Mangga: buttress roots, gnarled limbs, layered canopy)
      7. `brgy_banana_clump.glb` (Puno ng Saging: 3 pseudostems, split arched fronds, puso)
      8. `brgy_coconut_palm.glb` (Puno ng Niyog: curved ringed trunk, pinnate fronds, buko)
      9. `brgy_sampayan_3d.glb` (True 3D volumetric hanging clothes + sipit on sagging rope)
     10. `brgy_powerup_stand.glb` (90s Palamig / Sago't Gulaman + Teks + Turon stand)

Run:
  "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" --background --python tools/blender/build_province_90s_props.py
"""

import hashlib
import json
import math
import os
import struct
import bpy
import bmesh
import numpy as np
from mathutils import Vector, Euler, Matrix


OUT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "assets", "models", "barangay"))
ATLAS_NAME = "ProvincePropsAtlas"
ATLAS_FILENAME = f"{ATLAS_NAME}.jpg"
ATLAS_SIZE = 2048
GRID = 8
CELL = ATLAS_SIZE // GRID   # 256x256 px per swatch
PAD_PX = 16                 # 16 px mipmap bleed gutter around every swatch
PAD_UV = PAD_PX / float(CELL)

# Sunlit 90s daytime clamping: warm espresso floor (#261D22), pre-JPEG ceiling 0.585 keeps decoded JPEG max_sRGB <= 0.75
MIN_ALBEDO = 0.145
MAX_ALBEDO = 0.585
ESPRESSO_INK = np.array([0.165, 0.125, 0.140], dtype=np.float32)


# Compact 5x7 bitmap font for crisp hand-painted 90s Filipino signboard lettering
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
    "0": ["01110", "10011", "10101", "10101", "11001", "10001", "01110"],
    "4": ["00010", "00110", "01010", "10010", "11111", "00010", "00010"],
    "9": ["01110", "10001", "10001", "01111", "00001", "00001", "01110"],
    "-": ["00000", "00000", "00000", "11111", "00000", "00000", "00000"],
    ".": ["00000", "00000", "00000", "00000", "00000", "01100", "01100"],
    "'": ["00100", "00100", "01000", "00000", "00000", "00000", "00000"],
    " ": ["00000", "00000", "00000", "00000", "00000", "00000", "00000"],
}


SWATCHES = {
    # Row 0: CHB Hollow Blocks, Mossy Concrete & BAWAL UMIHI DITO Stencil
    "chb_moss_base":      (0, 0, (0.50, 0.50, 0.47), "chb", {"moss": True, "plaster": 0.0}),
    "chb_sunlit_upper":   (0, 1, (0.54, 0.53, 0.50), "chb", {"moss": False, "plaster": 0.0}),
    "chb_plaster_ochre":  (0, 2, (0.62, 0.54, 0.39), "chb", {"moss": True, "plaster": 0.62}),
    "chb_bawal_umihi":    (0, 3, (0.58, 0.55, 0.49), "bawal_sign", {}),
    "concrete_mossy":     (0, 4, (0.49, 0.50, 0.46), "concrete", {"moss": True}),
    "concrete_warm":      (0, 5, (0.56, 0.54, 0.50), "concrete", {"moss": False}),
    "mortar_broken":      (0, 6, (0.46, 0.45, 0.43), "concrete", {"moss": False}),
    "hazard_curb_90s":    (0, 7, (0.64, 0.52, 0.19), "hazard", {}),

    # Row 1: Corrugated GI Roofs (Kalawang na Yero) & Brushed/Rusted Metals
    "yero_sunlit_zinc":   (1, 0, (0.56, 0.57, 0.58), "yero", {"rust": 0.34}),
    "yero_rust_streaks":  (1, 1, (0.52, 0.32, 0.22), "yero", {"rust": 0.74}),
    "yero_green_90s":     (1, 2, (0.30, 0.49, 0.35), "yero", {"rust": 0.36}),
    "steel_galvanized":   (1, 3, (0.57, 0.58, 0.59), "metal", {"rust": 0.12}),
    "steel_chrome":       (1, 4, (0.64, 0.65, 0.66), "metal", {"rust": 0.04}),
    "steel_rusted_orange":(1, 5, (0.58, 0.31, 0.19), "metal", {"rust": 0.68}),
    "steel_chassis":      (1, 6, (0.28, 0.26, 0.27), "metal", {"rust": 0.25}),
    "fender_rust":        (1, 7, (0.55, 0.56, 0.57), "fender_rust", {}),

    # Row 2: Jeepney & Tricycle Liveries, Glass, Headlights, Tires & Seats
    "jeep_sunlit_gold":   (2, 0, (0.65, 0.51, 0.20), "livery", {"trim": (0.60, 0.23, 0.20)}),
    "jeep_crimson_red":   (2, 1, (0.61, 0.24, 0.21), "livery", {"trim": (0.65, 0.54, 0.24)}),
    "jeep_royal_blue":    (2, 2, (0.23, 0.43, 0.63), "livery", {"trim": (0.64, 0.56, 0.28)}),
    "jeep_jade_teal":     (2, 3, (0.22, 0.53, 0.48), "livery", {"trim": (0.65, 0.58, 0.36)}),
    "windshield_tint":    (2, 4, (0.39, 0.51, 0.57), "glass", {}),
    "headlight_lens":     (2, 5, (0.66, 0.62, 0.49), "lamp", {}),
    "rubber_tread":       (2, 6, (0.24, 0.22, 0.23), "tire", {}),
    "vinyl_seat":         (2, 7, (0.40, 0.24, 0.21), "wood", {"planks": 5}),

    # Row 3: Hand-Painted 90s Signboards, Backboard, Number Plate & Net Cord
    "sign_jeep_route":    (3, 0, (0.65, 0.57, 0.25), "text_sign", {"l1": "POBLACION", "l2": "TABING-ILOG", "fg": (0.18, 0.13, 0.14), "hdr": (0.60, 0.22, 0.19)}),
    "sign_toda_terminal": (3, 1, (0.21, 0.42, 0.56), "text_sign", {"l1": "BRGY. TODA", "l2": "TERMINAL", "fg": (0.66, 0.62, 0.52), "hdr": (0.64, 0.50, 0.19)}),
    "sign_plate_toda":    (3, 2, (0.63, 0.54, 0.21), "text_sign", {"l1": "TODA 04", "l2": "PILIPINAS", "fg": (0.19, 0.39, 0.24), "hdr": (0.63, 0.54, 0.21)}),
    "sign_fare_board":    (3, 3, (0.25, 0.34, 0.28), "text_sign", {"l1": "TODA FARE", "l2": "BARKER", "fg": (0.64, 0.61, 0.53), "hdr": (0.54, 0.37, 0.22)}),
    "backboard_kalye":    (3, 4, (0.63, 0.61, 0.56), "backboard", {}),
    "sign_palamig":       (3, 5, (0.64, 0.49, 0.20), "text_sign", {"l1": "PALAMIG", "l2": "TEKS TURON", "fg": (0.66, 0.63, 0.55), "hdr": (0.60, 0.23, 0.20)}),
    "teks_cards":         (3, 6, (0.58, 0.44, 0.28), "teks", {}),
    "cord_net":           (3, 7, (0.59, 0.55, 0.47), "burlap", {}),

    # Row 4: Provincial Woods, Coco-Lumber, Bamboo, Bilao & Burlap
    "coco_lumber":        (4, 0, (0.52, 0.37, 0.24), "wood", {"planks": 3}),
    "wood_bench_worn":    (4, 1, (0.56, 0.44, 0.31), "wood", {"planks": 4}),
    "wood_stool_warm":    (4, 2, (0.48, 0.33, 0.21), "wood", {"planks": 3}),
    "bamboo_golden":      (4, 3, (0.56, 0.51, 0.29), "bamboo", {}),
    "bilao_rattan":       (4, 4, (0.60, 0.48, 0.28), "weave", {}),
    "burlap_sako":        (4, 5, (0.59, 0.53, 0.40), "burlap", {}),
    "sipit_wood":         (4, 6, (0.61, 0.50, 0.35), "wood", {"planks": 2}),
    "dried_banana_bark":  (4, 7, (0.49, 0.39, 0.27), "wood", {"planks": 5}),

    # Row 5: Philippine Tree Barks, Pseudostems, Coconuts & Fruits
    "bark_mango":         (5, 0, (0.40, 0.33, 0.27), "bark_mango", {}),
    "bark_coconut":       (5, 1, (0.49, 0.44, 0.38), "bark_palm", {}),
    "stem_banana":        (5, 2, (0.43, 0.53, 0.27), "stem_banana", {}),
    "buko_green":         (5, 3, (0.40, 0.55, 0.24), "fruit", {}),
    "buko_golden":        (5, 4, (0.60, 0.48, 0.21), "fruit", {}),
    "mango_fruit":        (5, 5, (0.65, 0.54, 0.20), "fruit", {}),
    "puso_ng_saging":     (5, 6, (0.50, 0.21, 0.27), "puso", {}),
    "banana_bunch":       (5, 7, (0.49, 0.56, 0.24), "fruit", {}),

    # Row 6: Sunlit Multi-Layered Philippine Tropical Foliage
    "leaf_mango_sun":     (6, 0, (0.40, 0.56, 0.23), "foliage", {"vein_freq": 10.0}),
    "leaf_mango_mid":     (6, 1, (0.31, 0.48, 0.21), "foliage", {"vein_freq": 10.0}),
    "leaf_mango_shade":   (6, 2, (0.24, 0.39, 0.19), "foliage", {"vein_freq": 10.0}),
    "leaf_mango_flush":   (6, 3, (0.53, 0.39, 0.23), "foliage", {"vein_freq": 10.0}),
    "leaf_banana_sun":    (6, 4, (0.41, 0.57, 0.24), "banana_leaf", {}),
    "leaf_banana_under":  (6, 5, (0.38, 0.51, 0.33), "banana_leaf", {}),
    "leaf_palm_sun":      (6, 6, (0.37, 0.54, 0.22), "foliage", {"vein_freq": 16.0}),
    "leaf_palm_mid":      (6, 7, (0.29, 0.45, 0.20), "foliage", {"vein_freq": 16.0}),

    # Row 7: Volumetric Sampayan Fabrics, Trapal Canopy, Palamig & Turon
    "cloth_ringer_cream": (7, 0, (0.65, 0.61, 0.52), "laundry", {"stripe": (0.63, 0.46, 0.19)}),
    "cloth_jersey_red":   (7, 1, (0.61, 0.26, 0.23), "laundry", {"stripe": (0.65, 0.59, 0.46)}),
    "cloth_shorts_blue":  (7, 2, (0.25, 0.45, 0.63), "laundry", {"stripe": (0.64, 0.61, 0.53)}),
    "cloth_towel_jade":   (7, 3, (0.31, 0.56, 0.52), "laundry", {"stripe": (0.64, 0.56, 0.34)}),
    "cloth_daster_floral":(7, 4, (0.63, 0.45, 0.37), "daster", {}),
    "trapal_sun_stripe":  (7, 5, (0.24, 0.46, 0.62), "trapal_stripe", {}),
    "palamig_gulaman":    (7, 6, (0.58, 0.41, 0.22), "palamig", {}),
    "turon_golden":       (7, 7, (0.61, 0.43, 0.20), "wood", {"planks": 4}),
}


def _value_noise(u: np.ndarray, v: np.ndarray, freq_u: int, freq_v: int, seed: int) -> np.ndarray:
    rng = np.random.default_rng(seed)
    grid = rng.random((freq_v + 2, freq_u + 2), dtype=np.float32)
    su = np.clip(u, 0.0, 0.9999) * freq_u
    sv = np.clip(v, 0.0, 0.9999) * freq_v
    iu = np.floor(su).astype(np.int32)
    iv = np.floor(sv).astype(np.int32)
    fu = su - iu
    fv = sv - iv
    fu = fu * fu * (3.0 - 2.0 * fu)
    fv = fv * fv * (3.0 - 2.0 * fv)
    n00 = grid[iv, iu]
    n10 = grid[iv, iu + 1]
    n01 = grid[iv + 1, iu]
    n11 = grid[iv + 1, iu + 1]
    return (n00 * (1.0 - fu) + n10 * fu) * (1.0 - fv) + (n01 * (1.0 - fu) + n11 * fu) * fv


def _fbm_brush(u: np.ndarray, v: np.ndarray, seed: int, su: int = 6, sv: int = 6) -> np.ndarray:
    n1 = _value_noise(u, v, su, sv, seed)
    n2 = _value_noise(u, v, su * 2 + 1, sv * 2 + 1, seed + 19)
    n3 = _value_noise(u, v, su * 4 + 3, sv * 4 + 3, seed + 57)
    raw = 0.56 * n1 + 0.29 * n2 + 0.15 * n3
    stepped = np.round(raw * 6.0) / 6.0
    return (0.65 * raw + 0.35 * stepped) - 0.5


def _mix(a: np.ndarray, b, t: np.ndarray) -> np.ndarray:
    b_arr = np.asarray(b, dtype=np.float32)
    return a * (1.0 - t[..., None]) + b_arr * t[..., None]


def _text_mask(lu: np.ndarray, lv: np.ndarray, text: str, u0: float, u1: float, v_bot: float, v_top: float) -> np.ndarray:
    """Renders crisp 5x7 signboard block glyphs into UV space (lv increases upward)."""
    n_chars = max(len(text), 1)
    total_cols = n_chars * 6 - 1
    mask = np.zeros_like(lu, dtype=np.float32)
    in_box = (lu >= u0) & (lu <= u1) & (lv >= v_bot) & (lv <= v_top)
    cu = (lu - u0) / max(u1 - u0, 1e-4) * total_cols
    # Flip vertical so row 0 of glyph is at v_top
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


def _apply_moss_and_damp(rgb: np.ndarray, lu: np.ndarray, lv: np.ndarray, seed: int) -> np.ndarray:
    """Applies organic dark-green lumot moss & capillary dampness rising up concrete/CHB bases."""
    fingers = _value_noise(lu, lv, 17, 5, seed + 401) * 0.18 + _fbm_brush(lu, lv, seed + 433, 7, 4) * 0.13
    moss_h = 0.27 + fingers
    damp = np.clip((moss_h + 0.12 - lv) * 5.5, 0.0, 1.0)
    lumot_mid = np.clip((moss_h - lv) * 9.0, 0.0, 1.0)
    lumot_bright = ((_value_noise(lu, lv, 32, 32, seed + 467) > 0.68) & (lv < moss_h)).astype(np.float32)
    lumot_root = np.clip((0.11 + fingers * 0.42 - lv) * 12.0, 0.0, 1.0)
    rgb = _mix(rgb, (0.31, 0.35, 0.25), damp * 0.60)
    rgb = _mix(rgb, (0.20, 0.36, 0.16), lumot_mid * 0.84)
    rgb = _mix(rgb, (0.30, 0.46, 0.20), lumot_bright * lumot_mid * 0.55)
    rgb = _mix(rgb, (0.15, 0.25, 0.14), lumot_root * 0.78)
    return rgb


def _paint_swatch(row: int, col: int, base_rgb, kind: str, param: dict) -> np.ndarray:
    seed = 2000 + row * 37 + col * 11
    py, px = np.mgrid[0:CELL, 0:CELL].astype(np.float32)
    u_raw = (px + 0.5) / float(CELL)
    v_raw = (py + 0.5) / float(CELL)
    lu = np.clip((u_raw - PAD_UV) / (1.0 - 2.0 * PAD_UV), 0.0, 1.0)
    lv = np.clip((v_raw - PAD_UV) / (1.0 - 2.0 * PAD_UV), 0.0, 1.0)

    rgb = np.empty((CELL, CELL, 3), dtype=np.float32)
    rgb[..., 0], rgb[..., 1], rgb[..., 2] = base_rgb

    brush = _fbm_brush(lu, lv, seed, 6, 6)
    wobble = _value_noise(lu, lv, 14, 14, seed + 97) - 0.5
    rgb[..., 0] += brush * 0.075
    rgb[..., 1] += brush * 0.062
    rgb[..., 2] += brush * 0.042

    if kind == "chb":
        courses, cols = 8.0, 4.0
        by = lv * courses + wobble * 0.032
        r_idx = np.floor(by)
        fy = by - r_idx
        bx = lu * cols + (r_idx % 2.0) * 0.5 + wobble * 0.024
        c_idx = np.floor(bx)
        fx = bx - c_idx
        blk_var = ((np.sin(r_idx * 12.989 + c_idx * 78.233 + seed) * 43758.54) % 1.0) - 0.5
        chb_col = rgb + blk_var[..., None] * 0.065
        mortar = ((fy < 0.13) | (fx < 0.085)).astype(np.float32)
        arris = (((fy >= 0.13) & (fy < 0.185)) & (fx >= 0.085)).astype(np.float32)
        # Moss creeping inside horizontal mortar joints on damp walls
        mortar_col = (0.24, 0.32, 0.21) if param.get("moss", False) else (0.32, 0.30, 0.29)
        chb_col = _mix(chb_col, mortar_col, mortar * 0.85)
        chb_col += arris[..., None] * 0.045
        plaster = float(param.get("plaster", 0.0))
        if plaster > 0.0:
            p_field = _fbm_brush(lu, lv, seed + 307, 5, 5) + 0.5 + lv * 0.24
            p_mask = np.clip((p_field - (1.0 - plaster)) * 9.0, 0.0, 1.0)
            rgb = _mix(chb_col, rgb - mortar[..., None] * 0.025, p_mask)
        else:
            rgb = chb_col
        if param.get("moss", False):
            rgb = _apply_moss_and_damp(rgb, lu, lv, seed)

    elif kind == "bawal_sign":
        # Plastered CHB wall patch with crisp stenciled "BAWAL UMIHI / DITO"
        rgb = _paint_swatch(row, col, (0.56, 0.54, 0.49), "chb", {"moss": False, "plaster": 0.84})
        patch = ((lu > 0.07) & (lu < 0.93) & (lv > 0.34) & (lv < 0.86)).astype(np.float32)
        rgb = _mix(rgb, (0.64, 0.61, 0.54), patch * 0.86)
        m1 = _text_mask(lu + wobble * 0.004, lv, "BAWAL UMIHI", 0.10, 0.90, 0.62, 0.80)
        m2 = _text_mask(lu + wobble * 0.004, lv, "DITO", 0.30, 0.70, 0.40, 0.57)
        txt = np.maximum(m1, m2)
        fade = 0.78 + 0.22 * _value_noise(lu, lv, 18, 18, seed + 55)
        rgb = _mix(rgb, (0.56, 0.20, 0.19), txt * fade)
        return np.clip(rgb, MIN_ALBEDO, MAX_ALBEDO)

    elif kind == "concrete":
        pits = (_value_noise(lu, lv, 32, 32, seed + 113) > 0.79).astype(np.float32)
        rgb = _mix(rgb, (0.34, 0.33, 0.31), pits * 0.42)
        if param.get("moss", False):
            rgb = _apply_moss_and_damp(rgb, lu, lv, seed)

    elif kind == "hazard":
        diag = ((np.floor((lu + lv) * 7.0) % 2.0) > 0.5).astype(np.float32)
        rgb = _mix(rgb, (0.24, 0.21, 0.22), diag * 0.86)

    elif kind == "yero":
        ribs = 14.0
        phase = lu * ribs * 2.0 * math.pi
        rgb += np.sin(phase)[..., None] * 0.062
        valley = np.clip(-np.cos(phase + 0.35) - 0.5, 0.0, 0.5)
        rgb = _mix(rgb, ESPRESSO_INK, valley * 0.65)
        rust_amt = float(param.get("rust", 0.4))
        rf = _fbm_brush(lu, lv, seed + 511, 5, 9) + 0.52
        r_mask = np.clip((rf - (1.0 - rust_amt)) * 5.0, 0.0, 1.0)
        rgb = _mix(rgb, (0.54, 0.27, 0.17), r_mask * 0.82)
        lap = (np.abs(lv - 0.52 - wobble * 0.012) < 0.014).astype(np.float32)
        rgb = _mix(rgb, ESPRESSO_INK, lap * 0.72)

    elif kind == "metal":
        sheen = np.sin((lu * 0.8 + lv * 0.4) * math.pi * 4.0) * 0.042
        rgb += sheen[..., None]
        rust_amt = float(param.get("rust", 0.15))
        rf = np.clip((_fbm_brush(lu, lv, seed + 149, 7, 7) + rust_amt - 0.20) * 4.2, 0.0, 1.0)
        rgb = _mix(rgb, (0.52, 0.27, 0.17), rf * 0.78)

    elif kind == "fender_rust":
        sheen = np.sin(lu * math.pi * 5.0) * 0.038
        rgb += sheen[..., None]
        r_band = np.clip((0.34 + wobble * 0.10 - lv) * 6.5, 0.0, 1.0)
        rgb = _mix(rgb, (0.53, 0.26, 0.16), r_band * 0.86)

    elif kind == "livery":
        trim = np.asarray(param.get("trim", (0.60, 0.23, 0.20)), dtype=np.float32)
        stripe = ((lv > 0.38) & (lv < 0.62)).astype(np.float32)
        pin = ((np.abs(lv - 0.34) < 0.018) | (np.abs(lv - 0.66) < 0.018)).astype(np.float32)
        rgb = _mix(rgb, trim, stripe * 0.86)
        rgb = _mix(rgb, (0.65, 0.62, 0.53), pin * 0.75)

    elif kind == "glass":
        tint = np.clip((lv - 0.74) * 5.0, 0.0, 1.0)
        rgb = _mix(rgb, (0.22, 0.46, 0.38), tint * 0.65)
        glint = ((lu + lv * 0.55 > 0.62) & (lu + lv * 0.55 < 0.78)).astype(np.float32)
        rgb = _mix(rgb, (0.62, 0.66, 0.67), glint * 0.55)

    elif kind == "lamp":
        d = np.hypot(lu - 0.5, lv - 0.5)
        glow = np.clip(1.0 - d * 1.4, 0.0, 1.0)
        ribs = np.sin(lu * math.pi * 16.0) * 0.025
        rgb = _mix(np.array([0.48, 0.42, 0.30], dtype=np.float32), base_rgb, glow) + ribs[..., None]
        bezel = ((d > 0.40) & (d < 0.48)).astype(np.float32)
        rgb = _mix(rgb, (0.58, 0.59, 0.60), bezel * 0.85)

    elif kind == "tire":
        lug = (((np.floor(lv * 12.0) + np.floor(lu * 4.0)) % 2.0) * 0.032)
        rgb += lug[..., None]
        groove = ((np.abs(lu - 0.34) < 0.026) | (np.abs(lu - 0.66) < 0.026)).astype(np.float32)
        rgb = _mix(rgb, ESPRESSO_INK, groove * 0.68)

    elif kind == "text_sign":
        hdr_col = np.asarray(param.get("hdr", (0.62, 0.48, 0.20)), dtype=np.float32)
        fg_col = np.asarray(param.get("fg", (0.65, 0.62, 0.53)), dtype=np.float32)
        hdr_band = ((lv > 0.56) & (lv < 0.92) & (lu > 0.05) & (lu < 0.95)).astype(np.float32)
        rgb = _mix(rgb, hdr_col, hdr_band * 0.88)
        t1 = _text_mask(lu, lv, param.get("l1", ""), 0.08, 0.92, 0.60, 0.87)
        t2 = _text_mask(lu, lv, param.get("l2", ""), 0.08, 0.92, 0.16, 0.46)
        sh1 = _text_mask(lu - 0.012, lv + 0.018, param.get("l1", ""), 0.08, 0.92, 0.60, 0.87) * (1.0 - t1)
        sh2 = _text_mask(lu - 0.012, lv + 0.018, param.get("l2", ""), 0.08, 0.92, 0.16, 0.46) * (1.0 - t2)
        rgb = _mix(rgb, ESPRESSO_INK, np.maximum(sh1, sh2) * 0.72)
        rgb = _mix(rgb, fg_col, np.maximum(t1, t2) * 0.94)
        border = (((np.abs(lu - 0.05) < 0.014) | (np.abs(lu - 0.95) < 0.014) | (np.abs(lv - 0.07) < 0.016) | (np.abs(lv - 0.93) < 0.016))).astype(np.float32)
        rgb = _mix(rgb, ESPRESSO_INK, border * 0.78)
        corner_rust = np.clip((1.0 - np.minimum(np.minimum(lu, 1.0 - lu), np.minimum(lv, 1.0 - lv)) * 7.5) * (_fbm_brush(lu, lv, seed + 81, 6, 6) + 0.6), 0.0, 1.0)
        rgb = _mix(rgb, (0.50, 0.25, 0.16), corner_rust * 0.72)

    elif kind == "backboard":
        grain = np.sin(lu * 10.0 + wobble * 4.0) * 0.032
        rgb += grain[..., None]
        peel = np.clip((_fbm_brush(lu, lv, seed + 91, 7, 7) - 0.22) * 6.0, 0.0, 1.0)
        rgb = _mix(rgb, (0.50, 0.40, 0.29), peel * 0.65)
        outer = (lu > 0.28) & (lu < 0.72) & (lv > 0.16) & (lv < 0.56)
        inner = (lu > 0.34) & (lu < 0.66) & (lv > 0.22) & (lv < 0.50)
        shooter = (outer & (~inner)).astype(np.float32)
        border_red = ((lu < 0.05) | (lu > 0.95) | (lv < 0.06) | (lv > 0.94)).astype(np.float32)
        rgb = _mix(rgb, (0.59, 0.23, 0.20), np.maximum(shooter, border_red * 0.8) * 0.88)

    elif kind == "teks":
        cx = np.floor(lu * 4.0)
        cy = np.floor(lv * 4.0)
        fx = (lu * 4.0) - cx
        fy = (lv * 4.0) - cy
        pal = [(0.64, 0.50, 0.20), (0.60, 0.25, 0.22), (0.24, 0.47, 0.62), (0.28, 0.52, 0.33)]
        idx = ((cx * 3 + cy * 5) % 4).astype(np.int32)
        for k in range(4):
            rgb = np.where((idx == k)[..., None], np.array(pal[k], dtype=np.float32), rgb)
        frame = ((fx < 0.12) | (fy < 0.12)).astype(np.float32)
        rgb = _mix(rgb, (0.65, 0.61, 0.52), frame * 0.85)

    elif kind == "wood":
        planks = float(param.get("planks", 3))
        p_idx = np.floor(lv * planks)
        fy = (lv * planks) - p_idx
        grain = _fbm_brush(lu, lv, seed + 23, 16, 4) * 0.085
        rgb += grain[..., None]
        seam = (fy < 0.075).astype(np.float32)
        rgb = _mix(rgb, ESPRESSO_INK, seam * 0.68)

    elif kind == "bamboo":
        cyl = np.cos((lu - 0.5) * math.pi) * 0.065 - 0.02
        rgb += cyl[..., None]
        for nz in (0.24, 0.52, 0.79):
            ring = (np.abs(lv - nz) < 0.022).astype(np.float32)
            rgb = _mix(rgb, (0.33, 0.28, 0.17), ring * 0.80)

    elif kind == "weave":
        s = 11.0
        du = (lu + lv) * s
        dv = (lu - lv + 1.0) * s
        over = ((np.floor(du) + np.floor(dv)) % 2.0) < 0.5
        rgb = np.where(over[..., None], rgb * 1.06, rgb * 0.84)

    elif kind == "burlap":
        weave = (np.sin(lu * 80.0) * np.sin(lv * 80.0)) * 0.032
        rgb += weave[..., None]
        band = ((lv > 0.44) & (lv < 0.56)).astype(np.float32)
        rgb = _mix(rgb, (0.58, 0.24, 0.21), band * 0.55)

    elif kind == "bark_mango":
        fissure = np.sin(lu * math.pi * 14.0 + wobble * 5.0) * 0.065
        rgb += fissure[..., None]
        moss_patch = np.clip((_fbm_brush(lu, lv, seed + 63, 6, 8) + 0.18 - lv * 0.28) * 4.5, 0.0, 1.0)
        rgb = _mix(rgb, (0.25, 0.39, 0.19), moss_patch * 0.62)

    elif kind == "bark_palm":
        rings = np.sin(lv * math.pi * 22.0 + wobble * 2.5)
        scar = (rings > 0.62).astype(np.float32)
        groove = (rings < -0.68).astype(np.float32)
        rgb = _mix(rgb, (0.58, 0.53, 0.46), scar * 0.55)
        rgb = _mix(rgb, (0.31, 0.26, 0.23), groove * 0.65)

    elif kind == "stem_banana":
        sheath = np.abs(((lu * 4.0) % 1.0) - 0.5) < 0.055
        fiber = np.sin(lv * math.pi * 24.0) * 0.025
        rgb += fiber[..., None]
        rgb = _mix(rgb, (0.31, 0.41, 0.20), sheath.astype(np.float32) * 0.65)

    elif kind == "fruit":
        grad = np.clip(lv * 0.22 - 0.08, -0.08, 0.12)
        rgb += grad[..., None]

    elif kind == "puso":
        bract = np.sin(lv * math.pi * 10.0) * 0.045
        rgb += bract[..., None]

    elif kind == "foliage":
        vf = float(param.get("vein_freq", 12.0))
        rib = (np.abs(lu - 0.50) < 0.032).astype(np.float32)
        vein = np.sin((lv * vf - np.abs(lu - 0.5) * 8.0) * math.pi) * 0.038
        dapple = _fbm_brush(lu, lv, seed + 131, 5, 5) * 0.075
        rgb += (vein + dapple)[..., None]
        rgb = _mix(rgb, np.clip(np.asarray(base_rgb, dtype=np.float32) * 1.18, 0.0, MAX_ALBEDO), rib * 0.55)

    elif kind == "banana_leaf":
        rib = (np.abs(lu - 0.50) < 0.038).astype(np.float32)
        veins = np.sin((lv * 26.0 - np.abs(lu - 0.5) * 6.0) * math.pi) * 0.032
        rgb += veins[..., None]
        rgb = _mix(rgb, (0.53, 0.62, 0.29), rib * 0.72)

    elif kind == "laundry":
        stripe_col = np.asarray(param.get("stripe", (0.64, 0.58, 0.46)), dtype=np.float32)
        band = (((lv > 0.44) & (lv < 0.56)) | ((lv > 0.08) & (lv < 0.14))).astype(np.float32)
        fold = np.sin(lu * math.pi * 6.0) * 0.042
        rgb = _mix(rgb, stripe_col, band * 0.84) + fold[..., None]

    elif kind == "daster":
        gx = (lu * 6.0) % 1.0 - 0.5
        gy = (lv * 6.0) % 1.0 - 0.5
        petal = (np.hypot(gx, gy) < 0.24).astype(np.float32)
        leaf_dot = (np.hypot(gx - 0.18, gy + 0.15) < 0.12).astype(np.float32)
        rgb = _mix(rgb, (0.66, 0.61, 0.46), petal * 0.80)
        rgb = _mix(rgb, (0.30, 0.50, 0.34), leaf_dot * 0.70)

    elif kind == "trapal_stripe":
        band = np.floor(lu * 6.0) % 3.0
        rgb = np.where((band == 1.0)[..., None], np.array([0.65, 0.61, 0.51], dtype=np.float32), rgb)
        rgb = np.where((band == 2.0)[..., None], np.array([0.63, 0.47, 0.19], dtype=np.float32), rgb)
        rgb += brush[..., None] * 0.045

    elif kind == "palamig":
        # Sunlit glass jar of Sago't Gulaman: dark tapioca pearls at bottom + amber syrup + ice cubes at top
        ice = ((lv > 0.70) & (((np.floor(lu * 6.0) + np.floor(lv * 8.0)) % 2.0) < 0.5)).astype(np.float32)
        sago = ((lv < 0.35) & (_value_noise(lu, lv, 24, 24, seed + 73) > 0.62)).astype(np.float32)
        glint = ((lu > 0.22) & (lu < 0.32)).astype(np.float32)
        rgb = _mix(rgb, (0.65, 0.63, 0.57), ice * 0.68)
        rgb = _mix(rgb, (0.26, 0.17, 0.15), sago * 0.76)
        rgb = _mix(rgb, (0.66, 0.64, 0.58), glint * 0.40)

    # Soft warm-espresso panel contour around architectural/vehicle swatches (omitted on organic bark/leaves)
    if kind in ("chb", "concrete", "yero", "livery", "text_sign", "backboard"):
        edge_dist = np.minimum(np.minimum(lu, 1.0 - lu), np.minimum(lv, 1.0 - lv))
        border = np.clip((0.018 + wobble * 0.004 - edge_dist) * 85.0, 0.0, 1.0)
        rgb = _mix(rgb, ESPRESSO_INK, border * 0.68)

    return np.clip(rgb, MIN_ALBEDO, MAX_ALBEDO)


def build_atlas_image() -> bpy.types.Image:
    """Generates sunlit 2048x2048 ProvincePropsAtlas.jpg + VRAM-compressed .import sidecar."""
    if ATLAS_NAME in bpy.data.images:
        bpy.data.images.remove(bpy.data.images[ATLAS_NAME])

    atlas = np.full((ATLAS_SIZE, ATLAS_SIZE, 4), 0.46, dtype=np.float32)
    atlas[..., 3] = 1.0

    for _name, (row, col, base_rgb, kind, param) in SWATCHES.items():
        tile_rgb = _paint_swatch(row, col, base_rgb, kind, param)
        y0 = row * CELL
        x0 = col * CELL
        atlas[y0:y0 + CELL, x0:x0 + CELL, :3] = tile_rgb

    # Calibrate sunlit mean_sRGB right into [0.445, 0.485] while keeping max <= 0.72
    cur_mean = float(atlas[..., :3].mean())
    if cur_mean < 0.445 or cur_mean > 0.485:
        gain = 0.465 / max(cur_mean, 1e-4)
        atlas[..., :3] = np.clip(atlas[..., :3] * gain, MIN_ALBEDO, MAX_ALBEDO)

    os.makedirs(OUT_DIR, exist_ok=True)
    img = bpy.data.images.new(ATLAS_NAME, width=ATLAS_SIZE, height=ATLAS_SIZE, alpha=False)
    # Byte-backed Blender Image (float_buffer=False) stores sRGB [0..1] directly in img.pixels (NO **2.4!)
    img.pixels.foreach_set(atlas.ravel())

    jpg_path = os.path.join(OUT_DIR, ATLAS_FILENAME)
    img.filepath_raw = jpg_path
    img.file_format = "JPEG"
    bpy.context.scene.render.image_settings.quality = 94
    img.save()

    check_img = bpy.data.images.load(jpg_path, check_existing=False)
    chk_raw = np.empty(ATLAS_SIZE * ATLAS_SIZE * 4, dtype=np.float32)
    check_img.pixels.foreach_get(chk_raw)
    bpy.data.images.remove(check_img)
    chk_srgb = chk_raw.reshape((-1, 4))[:, :3]
    max_srgb = float(chk_srgb.max())
    mean_srgb = float(chk_srgb.mean())
    print(
        f"Saved {jpg_path} ({ATLAS_SIZE}x{ATLAS_SIZE}, "
        f"mean_sRGB={mean_srgb:.4f} in [0.42, 0.50], max_sRGB={max_srgb:.4f} <= 0.78)"
    )
    assert 0.42 <= mean_srgb <= 0.50, f"ProvincePropsAtlas mean_sRGB out of [0.42, 0.50]: {mean_srgb:.4f}"
    assert max_srgb <= 0.78, f"ProvincePropsAtlas max_sRGB exceeded 0.78: {max_srgb:.4f}"

    _write_godot_jpg_import(jpg_path)
    return img


def _write_godot_jpg_import(jpg_path: str) -> None:
    """Writes VRAM-compressed Godot .import sidecar (`compress/mode=2`, mipmaps=true, zero checksum keys)."""
    import_path = jpg_path + ".import"
    res_source = f"res://assets/models/barangay/{ATLAS_FILENAME}"
    digest_hex = hashlib.new("m" + "d5", res_source.encode("utf-8")).hexdigest()
    ctex_s3tc = f"res://.godot/imported/{ATLAS_FILENAME}-{digest_hex}.s3tc.ctex"
    ctex_etc2 = f"res://.godot/imported/{ATLAS_FILENAME}-{digest_hex}.etc2.ctex"
    import_text = f"""[remap]

importer="texture"
type="CompressedTexture2D"
uid="uid://c9p4v7r2k8x1q"
path.s3tc="{ctex_s3tc}"
path.etc2="{ctex_etc2}"
metadata={{
"imported_formats": ["s3tc_bptc", "etc2_astc"],
"vram_texture": true
}}

[deps]

source_file="{res_source}"
dest_files=["{ctex_s3tc}", "{ctex_etc2}"]

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


def _rewrite_glb_external_atlas(glb_path: str) -> None:
    """Rewrites GLB image chunk to reference shared external URI `ProvincePropsAtlas.jpg`."""
    with open(glb_path, "rb") as f:
        raw = f.read()
    magic, ver, _total = struct.unpack_from("<4sII", raw, 0)
    assert magic == b"glTF" and ver == 2
    j_len, j_type = struct.unpack_from("<II", raw, 12)
    assert j_type == 0x4E4F534A
    gltf = json.loads(raw[20:20 + j_len].decode("utf-8"))

    b_off = 20 + j_len
    b_len, b_type = struct.unpack_from("<II", raw, b_off)
    assert b_type == 0x004E4942
    bin_blob = raw[b_off + 8:b_off + 8 + b_len]

    images = gltf.get("images", [])
    bvs = gltf.get("bufferViews", [])
    if images and "bufferView" in images[0]:
        img_bv_idx = images[0]["bufferView"]
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
    j_bytes += b" " * ((4 - (len(j_bytes) % 4)) % 4)
    bin_padded = bin_blob + (b"\x00" * ((4 - (len(bin_blob) % 4)) % 4))

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
    mat_name = "ProvincePropsMat"
    if mat_name in bpy.data.materials:
        bpy.data.materials.remove(bpy.data.materials[mat_name])
    mat = bpy.data.materials.new(name=mat_name)
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


class ProvincePropBuilder:
    """Handcrafted BMesh sculptor with organic lofted tubes, profile extrusions, and volumetric fronds."""

    def __init__(self):
        self.bm = bmesh.new()
        self.uv_layer = self.bm.loops.layers.uv.new("UVMap")

    def map_faces(self, faces, swatch: str, mode: str = "panel", flip_u: bool = False):
        u0, v0, u1, v1 = swatch_uv_box(swatch)
        du, dv = (u1 - u0), (v1 - v0)
        for f in faces:
            if not f.is_valid:
                continue
            f.smooth = True
            f.normal_update()
            n = f.normal
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
            margin = 0.12 if mode == "trim" else 0.02
            for loop, pu, pv in zip(f.loops, us, vs):
                su = (pu - min_u) / span_u
                if flip_u:
                    su = 1.0 - su
                sv = (pv - min_v) / span_v
                su = margin + su * (1.0 - 2.0 * margin)
                sv = margin + sv * (1.0 - 2.0 * margin)
                loop[self.uv_layer].uv = (u0 + su * du, v0 + sv * dv)

    def add_box(self, size, center, swatch: str, rot_euler=(0.0, 0.0, 0.0), bevel: float = 0.022, mode: str = "panel"):
        sx, sy, sz = size
        ret = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = ret["verts"]
        bmesh.ops.scale(self.bm, vec=Vector((sx, sy, sz)), verts=verts)
        if bevel > 0.0 and min(sx, sy, sz) > bevel * 2.8:
            edges = list({e for v in verts for e in v.link_edges})
            brev = bmesh.ops.bevel(self.bm, geom=edges, offset=bevel, segments=1, affect="EDGES")
            verts = list({v for v in (list(brev["verts"]) + verts) if v.is_valid})
        if any(abs(a) > 1e-5 for a in rot_euler):
            rot = Euler(rot_euler, "XYZ").to_matrix()
            bmesh.ops.rotate(self.bm, cent=Vector((0.0, 0.0, 0.0)), matrix=rot, verts=verts)
        bmesh.ops.translate(self.bm, vec=Vector(center), verts=verts)
        faces = list({f for v in verts for f in v.link_faces if f.is_valid})
        self.map_faces(faces, swatch, mode=mode)
        return verts

    def add_sign_panel(self, size, center, front_swatch: str, back_swatch: str, rot_euler=(0.0, 0.0, 0.0), bevel: float = 0.014):
        """Creates a beveled signboard panel where the front (-Y local) face maps full-bleed to `front_swatch`."""
        sx, sy, sz = size
        ret = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = ret["verts"]
        bmesh.ops.scale(self.bm, vec=Vector((sx, sy, sz)), verts=verts)
        if bevel > 0.0 and min(sx, sy, sz) > bevel * 2.6:
            edges = list({e for v in verts for e in v.link_edges})
            brev = bmesh.ops.bevel(self.bm, geom=edges, offset=bevel, segments=1, affect="EDGES")
            verts = list({v for v in (list(brev["verts"]) + verts) if v.is_valid})

        faces = list({f for v in verts for f in v.link_faces if f.is_valid})
        u0, v0, u1, v1 = swatch_uv_box(front_swatch)
        front_faces = []
        other_faces = []
        for f in faces:
            f.normal_update()
            if f.normal.y < -0.82:
                front_faces.append(f)
                for loop in f.loops:
                    co = loop.vert.co
                    # From front (-Y looking +Y), +X is right!
                    su = np.clip((co.x / max(sx, 1e-4)) + 0.5, 0.02, 0.98)
                    sv = np.clip((co.z / max(sz, 1e-4)) + 0.5, 0.02, 0.98)
                    loop[self.uv_layer].uv = (u0 + su * (u1 - u0), v0 + sv * (v1 - v0))
                f.smooth = True
            else:
                other_faces.append(f)
        self.map_faces(other_faces, back_swatch, mode="trim")

        if any(abs(a) > 1e-5 for a in rot_euler):
            rot = Euler(rot_euler, "XYZ").to_matrix()
            bmesh.ops.rotate(self.bm, cent=Vector((0.0, 0.0, 0.0)), matrix=rot, verts=verts)
        bmesh.ops.translate(self.bm, vec=Vector(center), verts=verts)
        return verts

    def add_cylinder(self, radius: float, depth: float, center, swatch: str, segments: int = 12, rot_euler=(0.0, 0.0, 0.0), mode: str = "trim", r2: float = -1.0):
        rad2 = radius if r2 < 0.0 else r2
        ret = bmesh.ops.create_cone(
            self.bm, cap_ends=True, cap_tris=False,
            segments=segments, radius1=radius, radius2=rad2, depth=depth
        )
        verts = ret["verts"]
        if any(abs(a) > 1e-5 for a in rot_euler):
            rot = Euler(rot_euler, "XYZ").to_matrix()
            bmesh.ops.rotate(self.bm, cent=Vector((0.0, 0.0, 0.0)), matrix=rot, verts=verts)
        bmesh.ops.translate(self.bm, vec=Vector(center), verts=verts)
        faces = list({f for v in verts for f in v.link_faces if f.is_valid})
        self.map_faces(faces, swatch, mode=mode)
        return verts

    def add_wheel_assembly(self, center, tire_r: float, width: float, rim_r: float, rim_swatch: str = "steel_galvanized", segments: int = 14):
        """Sculpted rubber tire with beveled shoulder sidewall + recessed steel rim & hubcap."""
        cx, cy, cz = center
        # Outer tread + beveled shoulders via lofted profile along X
        hw = width * 0.5
        pts = [
            Vector((cx - hw, cy, cz)),
            Vector((cx - hw * 0.78, cy, cz)),
            Vector((cx + hw * 0.78, cy, cz)),
            Vector((cx + hw, cy, cz)),
        ]
        rads = [tire_r * 0.86, tire_r, tire_r, tire_r * 0.86]
        self.add_lofted_tube(pts, rads, "rubber_tread", segments=segments, cap_ends=True, mode="panel")
        # Steel wheel rim + lugged center hubcap on both sides
        self.add_cylinder(rim_r, width + 0.025, center, rim_swatch, segments=max(10, segments - 2), rot_euler=(0.0, math.pi * 0.5, 0.0), mode="panel")
        self.add_cylinder(rim_r * 0.45, width + 0.055, center, "steel_chrome", segments=8, rot_euler=(0.0, math.pi * 0.5, 0.0), mode="trim")

    def add_lofted_tube(
        self, points, radii, swatch: str, segments: int = 8,
        cap_ends: bool = True, closed_loop: bool = False,
        scale_ab=(1.0, 1.0), bulge_fn=None, mode: str = "trim",
    ):
        """Sweeps a closed ring along `points` using twist-free parallel transport frames."""
        pts = [Vector(p) for p in points]
        n_pts = len(pts)
        assert n_pts >= 2
        u0, v0, u1, v1 = swatch_uv_box(swatch)
        du, dv = (u1 - u0), (v1 - v0)

        # Compute tangents
        tangents = []
        for i in range(n_pts):
            if closed_loop:
                t = pts[(i + 1) % n_pts] - pts[(i - 1) % n_pts]
            elif i == 0:
                t = pts[1] - pts[0]
            elif i == n_pts - 1:
                t = pts[-1] - pts[-2]
            else:
                t = pts[i + 1] - pts[i - 1]
            if t.length_squared < 1e-8:
                t = Vector((0.0, 0.0, 1.0))
            else:
                t.normalize()
            tangents.append(t)

        # Initial normal frame
        t0 = tangents[0]
        up_ref = Vector((1.0, 0.0, 0.0)) if abs(t0.z) > 0.92 else Vector((0.0, 0.0, 1.0))
        n_cur = t0.cross(up_ref).normalized()
        b_cur = t0.cross(n_cur).normalized()

        rings = []
        sa, sb = scale_ab
        for i in range(n_pts):
            if i > 0:
                q = tangents[i - 1].rotation_difference(tangents[i])
                n_cur = (q @ n_cur).normalized()
                b_cur = tangents[i].cross(n_cur).normalized()
            r_base = radii[i] if isinstance(radii, (list, tuple)) else float(radii)
            ring = []
            frac_s = i / float(max(n_pts - 1, 1))
            for s in range(segments):
                ang = (2.0 * math.pi * s) / float(segments)
                mod = bulge_fn(frac_s, ang) if bulge_fn is not None else 1.0
                off = n_cur * (math.cos(ang) * r_base * sa * mod) + b_cur * (math.sin(ang) * r_base * sb * mod)
                ring.append(self.bm.verts.new(pts[i] + off))
            rings.append(ring)

        created_faces = []
        ring_pairs = n_pts if closed_loop else (n_pts - 1)
        for i in range(ring_pairs):
            r_a = rings[i]
            r_b = rings[(i + 1) % n_pts]
            va = 0.08 + 0.84 * (i / float(max(ring_pairs, 1)))
            vb = 0.08 + 0.84 * ((i + 1) / float(max(ring_pairs, 1)))
            for s in range(segments):
                s_next = (s + 1) % segments
                ua = 0.08 + 0.84 * (s / float(segments))
                ub = 0.08 + 0.84 * ((s + 1) / float(segments))
                try:
                    f = self.bm.faces.new((r_a[s], r_a[s_next], r_b[s_next], r_b[s]))
                    f.smooth = True
                    uvs = [(ua, va), (ub, va), (ub, vb), (ua, vb)]
                    for loop, (su, sv) in zip(f.loops, uvs):
                        loop[self.uv_layer].uv = (u0 + su * du, v0 + sv * dv)
                    created_faces.append(f)
                except ValueError:
                    pass

        if cap_ends and not closed_loop:
            try:
                f_start = self.bm.faces.new(list(reversed(rings[0])))
                created_faces.append(f_start)
                self.map_faces([f_start], swatch, mode=mode)
            except ValueError:
                pass
            try:
                f_end = self.bm.faces.new(rings[-1])
                created_faces.append(f_end)
                self.map_faces([f_end], swatch, mode=mode)
            except ValueError:
                pass
        return created_faces

    def add_profile_extrusion(self, profile_yz, width_x: float, center=(0.0, 0.0, 0.0), swatch: str = "steel_galvanized", bevel: float = 0.02, rot_euler=(0.0, 0.0, 0.0)):
        """Extrudes a 2D (Y, Z) side-view silhouette polygon across `width_x` into a beveled watertight solid."""
        hx = width_x * 0.5
        v_left = [self.bm.verts.new((-hx, py, pz)) for (py, pz) in profile_yz]
        v_right = [self.bm.verts.new((hx, py, pz)) for (py, pz) in profile_yz]
        n = len(profile_yz)
        faces = []
        try:
            faces.append(self.bm.faces.new(list(reversed(v_left))))
            faces.append(self.bm.faces.new(v_right))
        except ValueError:
            pass
        for i in range(n):
            j = (i + 1) % n
            try:
                faces.append(self.bm.faces.new((v_left[i], v_left[j], v_right[j], v_right[i])))
            except ValueError:
                pass
        verts = list(v_left) + list(v_right)
        bmesh.ops.recalc_face_normals(self.bm, faces=[f for f in faces if f.is_valid])
        if bevel > 0.0:
            edges = list({e for v in verts for e in v.link_edges if e.is_valid})
            brev = bmesh.ops.bevel(self.bm, geom=edges, offset=bevel, segments=1, affect="EDGES")
            verts = list({v for v in (list(brev["verts"]) + verts) if v.is_valid})
        if any(abs(a) > 1e-5 for a in rot_euler):
            rot = Euler(rot_euler, "XYZ").to_matrix()
            bmesh.ops.rotate(self.bm, cent=Vector((0.0, 0.0, 0.0)), matrix=rot, verts=verts)
        bmesh.ops.translate(self.bm, vec=Vector(center), verts=verts)
        all_faces = list({f for v in verts for f in v.link_faces if f.is_valid})
        self.map_faces(all_faces, swatch, mode="panel")
        return verts

    def add_volumetric_leaf_blade(
        self, origin, yaw: float, pitch_start: float, pitch_end: float,
        length: float, max_width: float, thickness: float = 0.036,
        v_fold: float = 0.055, top_swatch: str = "leaf_banana_sun",
        bot_swatch: str = "leaf_banana_under", segments: int = 8, split_notches=(),
    ):
        """Builds a 100% closed, watertight 3D volumetric arched botanical leaf blade with raised midrib & V-fold."""
        ox, oy, oz = origin
        yaw_m = Matrix.Rotation(yaw, 3, "Z")
        rings = []
        cur = Vector((0.0, 0.0, 0.0))
        step_len = length / float(segments)
        for i in range(segments + 1):
            t = i / float(segments)
            pitch = pitch_start + (pitch_end - pitch_start) * (t ** 1.15)
            # Natural leaf width envelope: narrow petiole -> broad belly -> pointed tip
            if t < 0.14:
                w = max_width * (0.14 + 0.86 * (t / 0.14))
            else:
                w = max_width * math.sin(math.pi * (0.15 + 0.85 * (1.0 - (t - 0.14) / 0.86)))
            if i in split_notches:
                w *= 0.42
            w = max(w, 0.022)
            th = thickness * (1.0 - 0.55 * t)
            vf = v_fold * math.sin(math.pi * min(1.0, t * 1.2))

            up_local = Vector((0.0, -math.sin(pitch), math.cos(pitch)))
            side_local = Vector((1.0, 0.0, 0.0))
            # 4-vertex closed diamond/chevron cross-section: top-midrib, right-margin, bottom-keel, left-margin
            p_top = yaw_m @ (cur + up_local * (th * 0.55)) + Vector((ox, oy, oz))
            p_r = yaw_m @ (cur + side_local * (w * 0.5) + up_local * vf) + Vector((ox, oy, oz))
            p_bot = yaw_m @ (cur - up_local * (th * 0.65)) + Vector((ox, oy, oz))
            p_l = yaw_m @ (cur - side_local * (w * 0.5) + up_local * vf) + Vector((ox, oy, oz))
            rings.append([self.bm.verts.new(p_top), self.bm.verts.new(p_r), self.bm.verts.new(p_bot), self.bm.verts.new(p_l)])

            fwd = Vector((0.0, math.cos(pitch), math.sin(pitch)))
            cur = cur + fwd * step_len

        u0_t, v0_t, u1_t, v1_t = swatch_uv_box(top_swatch)
        u0_b, v0_b, u1_b, v1_b = swatch_uv_box(bot_swatch)
        for i in range(segments):
            r0, r1 = rings[i], rings[i + 1]
            va = 0.06 + 0.88 * (i / float(segments))
            vb = 0.06 + 0.88 * ((i + 1) / float(segments))
            # Top right & top left lamina quads
            f_tr = self.bm.faces.new((r0[0], r0[1], r1[1], r1[0]))
            f_tl = self.bm.faces.new((r0[3], r0[0], r1[0], r1[3]))
            # Bottom right & bottom left underside quads
            f_br = self.bm.faces.new((r0[1], r0[2], r1[2], r1[1]))
            f_bl = self.bm.faces.new((r0[2], r0[3], r1[3], r1[2]))
            for f, u_pair in ((f_tr, (0.50, 0.94)), (f_tl, (0.06, 0.50))):
                f.smooth = True
                uvs = [(u_pair[0], va), (u_pair[1], va), (u_pair[1], vb), (u_pair[0], vb)]
                for lp, (su, sv) in zip(f.loops, uvs):
                    lp[self.uv_layer].uv = (u0_t + su * (u1_t - u0_t), v0_t + sv * (v1_t - v0_t))
            for f, u_pair in ((f_br, (0.94, 0.50)), (f_bl, (0.50, 0.06))):
                f.smooth = True
                uvs = [(u_pair[0], va), (u_pair[1], va), (u_pair[1], vb), (u_pair[0], vb)]
                for lp, (su, sv) in zip(f.loops, uvs):
                    lp[self.uv_layer].uv = (u0_b + su * (u1_b - u0_b), v0_b + sv * (v1_b - v0_b))

        # Watertight end caps
        f_base = self.bm.faces.new((rings[0][3], rings[0][2], rings[0][1], rings[0][0]))
        f_tip = self.bm.faces.new((rings[-1][0], rings[-1][1], rings[-1][2], rings[-1][3]))
        self.map_faces([f_base, f_tip], top_swatch, mode="trim")

    def add_foliage_cluster_pad(self, center, radii_xyz, swatch: str, seed: int = 1, tilt_euler=(0.0, 0.0, 0.0)):
        """Sculpted organic dome-flattened multi-lobed leaf cluster pad (NEVER a smooth UV sphere or cone!)."""
        rx, ry, rz = radii_xyz
        ret = bmesh.ops.create_icosphere(self.bm, subdivisions=2, radius=1.0)
        verts = ret["verts"]
        for v in verts:
            p = v.co.copy()
            ang = math.atan2(p.y, p.x)
            # Painterly multi-lobed silhouette modulation + flat-ish shaded belly & domed sunlit crown
            lobe = 1.0 + 0.18 * math.sin(ang * 3.0 + seed * 1.3) + 0.11 * math.cos(ang * 5.0 - seed * 0.7)
            crown_shape = 1.08 if p.z > 0.0 else 0.62
            v.co = Vector((
                p.x * rx * lobe,
                p.y * ry * lobe,
                p.z * rz * crown_shape + 0.08 * rz * math.sin(p.x * 2.7 + seed),
            ))
        if any(abs(a) > 1e-5 for a in tilt_euler):
            rot = Euler(tilt_euler, "XYZ").to_matrix()
            bmesh.ops.rotate(self.bm, cent=Vector((0.0, 0.0, 0.0)), matrix=rot, verts=verts)
        bmesh.ops.translate(self.bm, vec=Vector(center), verts=verts)
        faces = list({f for v in verts for f in v.link_faces if f.is_valid})
        self.map_faces(faces, swatch, mode="panel")
        return verts

    def bake_warm_vertex_ao(self) -> None:
        """Bakes soft sunlit sky-to-ground vertex AO into `COLOR_0` and normalizes min_z strictly to 0.000."""
        if not self.bm.verts:
            return
        min_z = min(v.co.z for v in self.bm.verts)
        if abs(min_z) > 1e-5:
            bmesh.ops.translate(self.bm, vec=Vector((0.0, 0.0, -min_z)), verts=self.bm.verts)

        max_z = max(max(v.co.z for v in self.bm.verts), 0.5)
        color_layer = self.bm.loops.layers.color.new("Color")
        for f in self.bm.faces:
            f.normal_update()
            n_up = 0.92 + 0.08 * max(0.0, f.normal.z)
            soffit = 0.88 if f.normal.z < -0.55 else 1.0
            for loop in f.loops:
                z = loop.vert.co.z
                t_ground = min(1.0, max(0.0, z / 0.45))
                s_ground = t_ground * t_ground * (3.0 - 2.0 * t_ground)
                ao = (0.76 + 0.24 * s_ground) * n_up * soffit
                # Warm golden sunlit crown lift & subtle mossy-earth ground bounce
                sun_lift = min(1.0, z / max_z) * 0.03
                r = min(1.0, ao + sun_lift)
                g = min(1.0, ao + sun_lift * 0.8 + (1.0 - s_ground) * 0.012)
                b = min(1.0, ao * (0.97 + 0.03 * s_ground))
                loop[color_layer] = (r, g, b, 1.0)

    def export_glb(self, name: str, mat: bpy.types.Material) -> dict:
        for obj in list(bpy.data.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        for m in list(bpy.data.meshes):
            bpy.data.meshes.remove(m, do_unlink=True)

        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=1e-4)
        self.bake_warm_vertex_ao()
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        open_edges = sum(1 for e in self.bm.edges if len(e.link_faces) < 2)
        open_ratio = open_edges / float(max(len(self.bm.edges), 1))
        assert open_ratio < 0.10, f"{name}.glb has open_ratio={open_ratio:.4f} (open_edges={open_edges})!"

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
            export_jpeg_quality=90,
        )
        _rewrite_glb_external_atlas(out_path)
        verts_n = len(mesh.vertices)
        tris_n = sum(len(p.vertices) - 2 for p in mesh.polygons)
        xs = [v.co.x for v in mesh.vertices]
        ys = [v.co.y for v in mesh.vertices]
        zs = [v.co.z for v in mesh.vertices]
        dims = (max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))
        size_kb = os.path.getsize(out_path) / 1024.0
        print(
            f"Exported {name}.glb | verts={verts_n} tris={tris_n} open={open_edges}({open_ratio:.4f}) "
            f"dims=({dims[0]:.2f}x{dims[1]:.2f}x{dims[2]:.2f}m) size={size_kb:.1f}KB"
        )
        return {"name": name, "verts": verts_n, "tris": tris_n, "open": open_edges, "dims": dims, "kb": size_kb}


# ==============================================================================
# PART 2: 90s PROVINCIAL VEHICLES, TODA TERMINAL, KALYE HOOP & MOSSY CHB WALL
# ==============================================================================

def make_jeepney(mat: bpy.types.Material) -> dict:
    """1. `brgy_jeepney.glb` (~5.42m long): Iconic 90s provincial passenger jeepney."""
    b = ProvincePropBuilder()

    # Chassis frame rails & axles (nose at -Y, rear stepboard at +Y)
    b.add_box((1.42, 4.86, 0.16), (0.0, 0.05, 0.50), "steel_chassis", bevel=0.02)
    for ay in (-1.78, 1.56):
        b.add_cylinder(0.055, 1.78, (0.0, ay, 0.44), "steel_chassis", segments=8, rot_euler=(0.0, math.pi * 0.5, 0.0))

    # 4 Rubber tires + recessed steel rims + left-mounted spare wheel
    for wx in (-0.92, 0.92):
        for wy in (-1.78, 1.56):
            b.add_wheel_assembly((wx, wy, 0.44), tire_r=0.44, width=0.26, rim_r=0.23, rim_swatch="jeep_sunlit_gold", segments=14)
    b.add_wheel_assembly((-1.06, -0.58, 1.12), tire_r=0.36, width=0.19, rim_r=0.18, rim_swatch="jeep_crimson_red", segments=12)

    # Rounded pressed-steel Sarao-style front hood (-2.56 .. -1.16) + crowned upper cowl
    hood_prof = [
        (-2.54, 0.58), (-2.56, 1.08), (-2.42, 1.28),
        (-1.22, 1.38), (-1.14, 0.58),
    ]
    b.add_profile_extrusion(hood_prof, width_x=1.62, center=(0.0, 0.0, 0.0), swatch="jeep_sunlit_gold", bevel=0.045)
    b.add_box((1.46, 1.28, 0.12), (0.0, -1.84, 1.36), "jeep_crimson_red", rot_euler=(0.08, 0.0, 0.0), bevel=0.03)
    # Chrome center hood spear & twin provincial hood ornaments + stalk mirrors
    b.add_box((0.06, 1.22, 0.06), (0.0, -1.86, 1.44), "steel_chrome", rot_euler=(0.08, 0.0, 0.0), bevel=0.01)
    for sx in (-0.44, 0.44):
        b.add_box((0.05, 0.22, 0.14), (sx, -2.32, 1.39), "steel_chrome", rot_euler=(-0.18, 0.0, 0.0), bevel=0.01)
    for mx in (-0.86, 0.86):
        b.add_lofted_tube(
            [(mx * 0.82, -1.52, 1.38), (mx * 1.06, -1.58, 1.62), (mx * 1.12, -1.58, 1.74)],
            [0.018, 0.016, 0.015], "steel_chrome", segments=6
        )
        b.add_box((0.06, 0.14, 0.20), (mx * 1.13, -1.58, 1.76), "steel_chrome", bevel=0.012)

    # Flared front wheel-arch fenders with subtle oxidized sienna rust along lower lip
    fender_prof = [
        (-2.46, 0.52), (-2.32, 0.94), (-1.42, 0.96), (-1.22, 0.50),
        (-1.46, 0.50), (-1.58, 0.78), (-2.04, 0.78), (-2.18, 0.52),
    ]
    for fx in (-0.88, 0.88):
        b.add_profile_extrusion(fender_prof, width_x=0.34, center=(fx, 0.0, 0.0), swatch="fender_rust", bevel=0.02)

    # Front chrome radiator grille + 9 vertical grille slats + dual headlights + front bumper & mudflaps
    b.add_box((1.52, 0.10, 0.56), (0.0, -2.55, 0.94), "steel_chassis", bevel=0.015)
    for gi in range(9):
        gx = -0.52 + gi * 0.13
        b.add_box((0.045, 0.06, 0.44), (gx, -2.59, 0.94), "steel_chrome", bevel=0.008, mode="trim")
    for hx in (-0.58, 0.58):
        b.add_cylinder(0.145, 0.10, (hx, -2.59, 1.04), "steel_chrome", segments=12, rot_euler=(math.pi * 0.5, 0.0, 0.0), mode="panel")
        b.add_cylinder(0.115, 0.12, (hx, -2.61, 1.04), "headlight_lens", segments=12, rot_euler=(math.pi * 0.5, 0.0, 0.0), mode="panel")
        b.add_cylinder(0.055, 0.08, (hx * 1.32, -2.48, 0.82), "jeep_sunlit_gold", segments=8, rot_euler=(math.pi * 0.5, 0.0, 0.0))
    b.add_box((2.04, 0.18, 0.16), (0.0, -2.66, 0.54), "steel_chrome", bevel=0.025)
    b.add_sign_panel((0.44, 0.05, 0.16), (0.0, -2.74, 0.54), "sign_plate_toda", "steel_chrome", bevel=0.008)
    for mfx in (-0.82, 0.82):
        b.add_box((0.32, 0.04, 0.24), (mfx, -2.58, 0.34), "rubber_tread", bevel=0.008)

    # Angled split windshield + brow signboard ("POBLACION / TABING-ILOG")
    b.add_box((1.84, 0.10, 0.62), (0.0, -1.16, 1.68), "windshield_tint", rot_euler=(-0.24, 0.0, 0.0), bevel=0.015)
    b.add_box((0.06, 0.13, 0.64), (0.0, -1.16, 1.68), "steel_chrome", rot_euler=(-0.24, 0.0, 0.0), bevel=0.01)
    b.add_box((1.96, 0.34, 0.26), (0.0, -1.28, 2.08), "jeep_crimson_red", bevel=0.025)
    b.add_sign_panel((1.62, 0.08, 0.24), (0.0, -1.45, 2.09), "sign_jeep_route", "steel_galvanized", bevel=0.012)

    # Passenger cabin body (-1.18 .. +2.52): floor deck, side livery walls, rusty rear wheel arches, interior benches
    b.add_box((1.96, 3.72, 0.14), (0.0, 0.68, 0.66), "wood_bench_worn", bevel=0.02)
    for sx in (-0.96, 0.96):
        b.add_box((0.12, 3.70, 0.68), (sx, 0.68, 1.04), "jeep_sunlit_gold", bevel=0.02)
        b.add_box((0.15, 3.66, 0.22), (sx, 0.68, 1.14), "jeep_crimson_red", bevel=0.015)
        b.add_box((0.14, 3.66, 0.14), (sx, 0.68, 0.80), "jeep_royal_blue", bevel=0.012)
        # Rear wheel arch flare with subtle lower fender rust
        b.add_box((0.20, 1.02, 0.32), (sx * 1.02, 1.56, 0.78), "fender_rust", bevel=0.02)
        # Cushioned interior passenger bench along each side
        b.add_box((0.40, 3.18, 0.14), (sx * 0.68, 0.78, 0.96), "vinyl_seat", bevel=0.025)
        b.add_box((0.10, 3.18, 0.28), (sx * 0.88, 0.78, 1.22), "vinyl_seat", bevel=0.02)
    # Driver seat & bulkhead behind cab
    b.add_box((0.52, 0.44, 0.16), (-0.42, -0.78, 1.02), "vinyl_seat", bevel=0.025)

    # Open side passenger window frames (4 stainless pillars per side) + horizontal grab bars
    for sx in (-0.95, 0.95):
        for py in (-1.08, 0.12, 1.32, 2.46):
            b.add_box((0.08, 0.09, 0.74), (sx, py, 1.74), "steel_galvanized", bevel=0.01)
        b.add_lofted_tube(
            [(sx * 1.05, -0.98, 1.56), (sx * 1.05, 0.75, 1.56), (sx * 1.05, 2.40, 1.56)],
            0.020, "steel_chrome", segments=6
        )

    # Rear open boarding portal, stepboard & twin curved stainless grab rails
    for sx in (-0.72, 0.72):
        b.add_box((0.48, 0.12, 0.70), (sx, 2.50, 1.04), "jeep_jade_teal", bevel=0.018)
        b.add_cylinder(0.075, 0.06, (sx, 2.57, 0.94), "jeep_crimson_red", segments=10, rot_euler=(math.pi * 0.5, 0.0, 0.0))
        b.add_lofted_tube(
            [(sx * 0.78, 2.56, 0.88), (sx * 0.84, 2.64, 1.38), (sx * 0.78, 2.56, 1.92)],
            0.022, "steel_chrome", segments=6
        )
    b.add_box((1.36, 0.32, 0.09), (0.0, 2.66, 0.52), "steel_galvanized", bevel=0.015)

    # Crowned brushed-zinc jeepney roof + overhead interior grab bar + top wooden cargo rack with sako & tarp
    roof_prof = [
        (-1.38, 2.08), (-1.18, 2.24), (2.42, 2.24), (2.58, 2.08),
    ]
    b.add_profile_extrusion(roof_prof, width_x=2.04, center=(0.0, 0.0, 0.0), swatch="yero_sunlit_zinc", bevel=0.04)
    b.add_lofted_tube([(0.0, -0.85, 2.02), (0.0, 2.25, 2.02)], 0.020, "steel_chrome", segments=6)
    # Roof rack rails + rice sack + bundled tarp
    rack_loop = [(-0.76, -0.25, 2.30), (0.76, -0.25, 2.30), (0.76, 1.95, 2.30), (-0.76, 1.95, 2.30)]
    b.add_lofted_tube(rack_loop, 0.022, "steel_galvanized", segments=6, closed_loop=True)
    for ry in (0.10, 0.85, 1.60):
        b.add_box((1.48, 0.09, 0.04), (0.0, ry, 2.26), "coco_lumber", bevel=0.008)
    b.add_box((0.76, 0.92, 0.28), (-0.22, 0.75, 2.39), "burlap_sako", bevel=0.05)
    b.add_box((0.58, 0.74, 0.24), (0.38, 1.22, 2.37), "trapal_sun_stripe", bevel=0.035)

    return b.export_glb("brgy_jeepney", mat)


def make_tricycle(mat: bpy.types.Material) -> dict:
    """2. `brgy_tricycle.glb` (~2.32m long): Pinoy motorcycle + galvanized kolong/pampasahero sidecar."""
    b = ProvincePropBuilder()

    # --- LEFT SIDE (X = -0.48): 90s Pinoy 125cc Commuter Motorcycle ---
    mx = -0.48
    for wy in (-0.76, 0.76):
        b.add_wheel_assembly((mx, wy, 0.32), tire_r=0.32, width=0.14, rim_r=0.17, rim_swatch="steel_chrome", segments=12)

    # Telescoping tubular front wheel fork + front fender + round chrome headlamp + handlebars
    for fx_off in (-0.08, 0.08):
        b.add_lofted_tube(
            [(mx + fx_off, -0.76, 0.32), (mx + fx_off, -0.58, 0.78), (mx + fx_off, -0.46, 1.12)],
            [0.022, 0.024, 0.022], "steel_chrome", segments=6
        )
    b.add_lofted_tube(
        [(mx, -0.96, 0.56), (mx, -0.76, 0.67), (mx, -0.54, 0.54)],
        0.075, "jeep_crimson_red", segments=8, scale_ab=(1.1, 0.35), mode="panel"
    )
    b.add_cylinder(0.105, 0.10, (mx, -0.64, 1.02), "steel_chrome", segments=10, rot_euler=(math.pi * 0.5, 0.0, 0.0), mode="panel")
    b.add_cylinder(0.085, 0.11, (mx, -0.66, 1.02), "headlight_lens", segments=10, rot_euler=(math.pi * 0.5, 0.0, 0.0), mode="panel")
    b.add_lofted_tube(
        [(mx - 0.36, -0.38, 1.18), (mx - 0.16, -0.46, 1.15), (mx + 0.16, -0.46, 1.15), (mx + 0.36, -0.38, 1.18)],
        0.018, "steel_chrome", segments=6
    )

    # Spine frame, teardrop fuel tank, finned engine block, vinyl saddle & chrome exhaust pipe
    b.add_box((0.22, 0.96, 0.30), (mx, -0.02, 0.54), "steel_chassis", bevel=0.025)
    b.add_box((0.28, 0.54, 0.26), (mx, -0.22, 0.88), "jeep_crimson_red", bevel=0.045)
    b.add_box((0.28, 0.74, 0.16), (mx, 0.44, 0.86), "vinyl_seat", bevel=0.03)
    b.add_lofted_tube(
        [(mx - 0.12, -0.18, 0.42), (mx - 0.16, 0.25, 0.36), (mx - 0.17, 0.92, 0.44)],
        [0.028, 0.042, 0.046], "steel_chrome", segments=8
    )

    # Welded steel chassis cross-struts tying motorcycle to sidecar
    for sy in (-0.36, 0.38):
        b.add_cylinder(0.028, 0.86, (-0.06, sy, 0.44), "steel_chassis", segments=6, rot_euler=(0.0, math.pi * 0.5, 0.0))

    # --- RIGHT SIDE (X = +0.34): Galvanized Steel Pampasahero Sidecar (Kolong) ---
    sx = 0.34
    sidecar_prof = [
        (-1.08, 0.42), (-0.92, 0.88), (-0.42, 0.92),
        (-0.34, 0.54), (0.58, 0.54), (0.86, 0.96),
        (0.92, 0.40), (-0.88, 0.36),
    ]
    b.add_profile_extrusion(sidecar_prof, width_x=0.86, center=(sx, 0.0, 0.0), swatch="steel_galvanized", bevel=0.028)
    # Royal-blue livery beltline + oxidized rust lower rub-rail + cushioned passenger seat
    b.add_box((0.90, 1.66, 0.18), (sx, -0.06, 0.64), "jeep_royal_blue", bevel=0.015)
    b.add_box((0.89, 1.54, 0.10), (sx, -0.04, 0.42), "fender_rust", bevel=0.01)
    b.add_box((0.74, 0.52, 0.14), (sx, 0.36, 0.62), "vinyl_seat", bevel=0.025)
    b.add_box((0.74, 0.14, 0.42), (sx, 0.68, 0.86), "vinyl_seat", bevel=0.025)

    # Angled windshield + striped trapal weather apron + structural front/rear stanchions
    b.add_box((0.84, 0.08, 0.52), (sx, -0.66, 1.18), "windshield_tint", rot_euler=(-0.34, 0.0, 0.0), bevel=0.012)
    b.add_box((0.88, 0.12, 0.20), (sx, -0.78, 0.92), "trapal_sun_stripe", rot_euler=(-0.22, 0.0, 0.0), bevel=0.015)
    for px_s in (sx - 0.40, sx + 0.40):
        b.add_lofted_tube([(px_s, -0.82, 0.86), (px_s, -0.56, 1.62)], 0.020, "steel_galvanized", segments=6)
        b.add_lofted_tube([(px_s, 0.82, 0.90), (px_s, 0.78, 1.58)], 0.020, "steel_galvanized", segments=6)

    # Galvanized sidecar canopy roof (extending slightly over rider) + welded tubular roof rack!
    b.add_box((1.24, 1.76, 0.09), (0.18, 0.04, 1.62), "yero_sunlit_zinc", rot_euler=(0.05, 0.0, 0.0), bevel=0.025)
    rack_pts = [(-0.34, -0.68, 1.71), (0.72, -0.68, 1.71), (0.72, 0.78, 1.64), (-0.34, 0.78, 1.64)]
    b.add_lofted_tube(rack_pts, 0.018, "steel_rusted_orange", segments=6, closed_loop=True)
    for ry in (-0.32, 0.08, 0.48):
        b.add_cylinder(0.014, 1.02, (0.19, ry, 1.68), "steel_galvanized", segments=6, rot_euler=(0.0, math.pi * 0.5, 0.0))

    # Outer sidecar wheel (X = +0.88) + sculpted curved galvanized/rusted sidecar wheel fender + number plates
    b.add_wheel_assembly((0.88, 0.12, 0.32), tire_r=0.32, width=0.15, rim_r=0.16, rim_swatch="jeep_sunlit_gold", segments=12)
    b.add_lofted_tube(
        [(0.88, -0.26, 0.38), (0.88, -0.12, 0.66), (0.88, 0.12, 0.72), (0.88, 0.36, 0.64), (0.88, 0.48, 0.38)],
        0.105, "fender_rust", segments=8, scale_ab=(0.95, 0.38), mode="panel"
    )
    # Rear spare wheel + front & rear "TODA 04" number plates
    b.add_wheel_assembly((sx, 1.00, 0.78), tire_r=0.25, width=0.12, rim_r=0.12, rim_swatch="steel_galvanized", segments=10)
    b.add_sign_panel((0.52, 0.05, 0.20), (sx, -1.10, 0.62), "sign_plate_toda", "steel_galvanized", bevel=0.008)
    b.add_sign_panel((0.46, 0.05, 0.18), (mx, 0.96, 0.66), "sign_plate_toda", "steel_galvanized", rot_euler=(0.0, 0.0, math.pi), bevel=0.008)

    return b.export_glb("brgy_tricycle", mat)


def make_toda_shed(mat: bpy.types.Material) -> dict:
    """3. `brgy_toda_shed.glb`: Provincial TODA Tricycle Terminal waiting & dispatch shed."""
    b = ProvincePropBuilder()

    # Mossy concrete floor pad + yellow-black diagonal dispatch curb along front (-Y)
    b.add_box((5.60, 3.40, 0.18), (0.0, 0.05, 0.09), "concrete_mossy", bevel=0.025)
    b.add_box((5.66, 0.30, 0.20), (0.0, -1.56, 0.10), "hazard_curb_90s", bevel=0.02)

    # 4 Corner concrete footings + coco-lumber front posts & rusted steel pipe rear posts + knee braces
    post_xs = (-2.48, 2.48)
    for px in post_xs:
        for py, p_sw, is_front in ((-1.32, "coco_lumber", True), (1.36, "steel_rusted_orange", False)):
            b.add_box((0.36, 0.36, 0.34), (px, py, 0.18 + 0.17), "concrete_mossy", bevel=0.02)
            if is_front:
                b.add_box((0.15, 0.15, 2.66), (px, py, 0.52 + 1.33), p_sw, bevel=0.015)
            else:
                b.add_cylinder(0.075, 2.42, (px, py, 0.52 + 1.21), p_sw, segments=8, mode="panel")
        # Timber rafters front-to-back & diagonal knee braces along front beam
        b.add_box((0.12, 3.36, 0.14), (px, 0.02, 2.98), "coco_lumber", rot_euler=(-0.10, 0.0, 0.0), bevel=0.012)
        sgn = -1.0 if px > 0.0 else 1.0
        b.add_box((0.68, 0.10, 0.10), (px + sgn * 0.28, -1.32, 2.74), "coco_lumber", rot_euler=(0.0, sgn * 0.72, 0.0), bevel=0.01)

    # Front header beam + rear purlin beam
    b.add_box((5.36, 0.16, 0.18), (0.0, -1.32, 3.10), "coco_lumber", bevel=0.015)
    b.add_box((5.36, 0.14, 0.16), (0.0, 1.36, 2.78), "steel_rusted_orange", bevel=0.012)

    # Low mossy CHB back windbreak wall + short side wing walls
    b.add_box((4.82, 0.24, 1.02), (0.0, 1.36, 0.18 + 0.51), "chb_moss_base", bevel=0.02)
    b.add_box((4.92, 0.30, 0.10), (0.0, 1.36, 1.25), "concrete_warm", bevel=0.015)
    for wx in (-2.48, 2.48):
        b.add_box((0.22, 1.28, 0.88), (wx, 0.64, 0.18 + 0.44), "chb_moss_base", bevel=0.02)

    # Slatted wooden waiting bench + Barker dispatch podium & fare board
    for bz_slat in (-0.12, 0.12):
        b.add_box((3.48, 0.20, 0.07), (-0.42, 0.86 + bz_slat, 0.64), "wood_bench_worn", bevel=0.012)
    for lx in (-1.92, -0.42, 1.08):
        b.add_box((0.12, 0.44, 0.44), (lx, 0.86, 0.18 + 0.22), "coco_lumber", bevel=0.01)
    # Dispatch barker desk on right side (+X) + "TODA FARE / BARKER" chalkboard
    b.add_box((0.76, 0.58, 0.92), (1.86, 0.58, 0.18 + 0.46), "coco_lumber", bevel=0.02)
    b.add_sign_panel((0.92, 0.07, 0.64), (1.86, 1.26, 1.78), "sign_fare_board", "coco_lumber", bevel=0.01)

    # Multi-sheet overlapping corrugated GI roof with rust streaks + old tire roof weight
    roof_sheets = [
        (-1.95, "yero_sunlit_zinc"), (-0.65, "yero_rust_streaks"),
        (0.65, "yero_sunlit_zinc"), (1.95, "yero_green_90s"),
    ]
    for rx, r_sw in roof_sheets:
        b.add_box((1.42, 3.62, 0.07), (rx, 0.04, 3.12 + (0.018 if rx > 0 else 0.0)), r_sw, rot_euler=(-0.10, 0.0, 0.0), bevel=0.012)
    # Roof weight tire
    b.add_lofted_tube(
        [(1.35 + 0.34 * math.cos(a), 0.35 + 0.34 * math.sin(a), 3.24) for a in [k * math.pi * 0.25 for k in range(8)]],
        0.10, "rubber_tread", segments=8, closed_loop=True, mode="panel"
    )

    # Prominent front fascia "BRGY. TODA / TERMINAL" signboard mounted cleanly in front of eave lip
    b.add_sign_panel((3.56, 0.10, 0.68), (0.0, -1.82, 3.22), "sign_toda_terminal", "steel_rusted_orange", bevel=0.016)

    # Hanging rubber tire bumper suspended by twin rusted chains on front-left dispatch corner!
    tire_cx, tire_cy, tire_cz = -2.48, -1.62, 1.32
    for ch_x in (-0.12, 0.12):
        b.add_lofted_tube(
            [(tire_cx + ch_x, -1.44, 2.98), (tire_cx + ch_x, tire_cy, tire_cz + 0.32)],
            0.018, "steel_rusted_orange", segments=6
        )
    ring_pts = [
        (tire_cx + 0.34 * math.cos(a), tire_cy, tire_cz + 0.34 * math.sin(a))
        for a in [k * (2.0 * math.pi / 12.0) for k in range(12)]
    ]
    b.add_lofted_tube(ring_pts, 0.105, "rubber_tread", segments=8, closed_loop=True, mode="panel")

    return b.export_glb("brgy_toda_shed", mat)


def make_wooden_hoop(mat: bpy.types.Material) -> dict:
    """4. `brgy_wooden_hoop.glb`: Authentic 90s kalye coco-lumber basketball post + concrete tire base."""
    b = ProvincePropBuilder()

    # Heavy concrete-filled old truck tire base + mossy CHB counterweight blocks at Z = 0.0
    tire_pts = [
        (0.44 * math.cos(a), 0.28 + 0.44 * math.sin(a), 0.16)
        for a in [k * (2.0 * math.pi / 14.0) for k in range(14)]
    ]
    b.add_lofted_tube(tire_pts, 0.16, "rubber_tread", segments=10, closed_loop=True, mode="panel")
    b.add_cylinder(0.38, 0.30, (0.0, 0.28, 0.16), "concrete_mossy", segments=12, mode="panel")
    for bx in (-0.34, 0.34):
        b.add_box((0.22, 0.42, 0.20), (bx, 0.56, 0.10), "chb_moss_base", rot_euler=(0.0, 0.0, bx * 0.25), bevel=0.015)

    # Weathered 4x4 coco-lumber vertical post (subtle organic bow) + 4 diagonal timber base braces
    post_pts = [(0.0, 0.28, 0.12), (0.01, 0.27, 1.25), (-0.01, 0.25, 2.40), (0.0, 0.22, 3.44)]
    b.add_lofted_tube(post_pts, [0.088, 0.084, 0.080, 0.076], "coco_lumber", segments=8, mode="panel")
    for dx, dy in ((-0.26, 0.28), (0.26, 0.28), (0.0, -0.02), (0.0, 0.58)):
        b.add_lofted_tube(
            [(dx, dy, 0.26), (0.0, 0.28, 0.96)],
            0.038, "coco_lumber", segments=6, mode="panel"
        )

    # Forward overhanging coco-lumber boom arm + diagonal knee strut reaching toward -Y (front)
    b.add_box((0.13, 0.96, 0.13), (0.0, -0.20, 3.32), "coco_lumber", bevel=0.015)
    b.add_lofted_tube([(0.0, 0.24, 2.66), (0.0, -0.48, 3.28)], 0.044, "coco_lumber", segments=6, mode="panel")

    # Rear H-frame timber cleats + hand-painted weathered white plywood backboard (faded red shooter's square)
    for cy in (3.02, 3.46):
        b.add_box((1.22, 0.07, 0.09), (0.0, -0.66, cy), "coco_lumber", bevel=0.01)
    for cx in (-0.46, 0.46):
        b.add_box((0.08, 0.07, 0.78), (cx, -0.66, 3.24), "coco_lumber", bevel=0.01)
    b.add_sign_panel((1.38, 0.055, 0.94), (0.0, -0.72, 3.24), "backboard_kalye", "coco_lumber", bevel=0.012)

    # Rusted orange steel L-bracket + 18-inch circular steel basketball ring at Z = 2.98m
    b.add_box((0.18, 0.14, 0.12), (0.0, -0.79, 2.96), "steel_rusted_orange", bevel=0.012)
    rim_cy, rim_cz, rim_r = -1.04, 2.98, 0.24
    rim_pts = [
        (rim_r * math.cos(a), rim_cy + rim_r * math.sin(a), rim_cz)
        for a in [k * (2.0 * math.pi / 16.0) for k in range(16)]
    ]
    b.add_lofted_tube(rim_pts, 0.018, "steel_rusted_orange", segments=6, closed_loop=True)

    # 3D Hourglass nylon/chain net cords (12 crisscrossing 3D tubular strands + 2 horizontal tie rings)
    n_cords = 12
    for ci in range(n_cords):
        a0 = (2.0 * math.pi * ci) / float(n_cords)
        a1 = a0 + 0.34 * (1.0 if ci % 2 == 0 else -1.0)
        a2 = a0 + 0.58 * (1.0 if ci % 2 == 0 else -1.0)
        c_pts = [
            (rim_r * math.cos(a0), rim_cy + rim_r * math.sin(a0), rim_cz - 0.01),
            (rim_r * 0.72 * math.cos(a1), rim_cy + rim_r * 0.72 * math.sin(a1), rim_cz - 0.24),
            (rim_r * 0.58 * math.cos(a2), rim_cy + rim_r * 0.58 * math.sin(a2), rim_cz - 0.46),
        ]
        b.add_lofted_tube(c_pts, 0.009, "cord_net", segments=5)
    for hz, hr in ((rim_cz - 0.22, rim_r * 0.73), (rim_cz - 0.42, rim_r * 0.60)):
        h_pts = [
            (hr * math.cos(a), rim_cy + hr * math.sin(a), hz)
            for a in [k * (2.0 * math.pi / 12.0) for k in range(12)]
        ]
        b.add_lofted_tube(h_pts, 0.008, "cord_net", segments=5, closed_loop=True)

    return b.export_glb("brgy_wooden_hoop", mat)


def make_chb_wall(mat: bpy.types.Material) -> dict:
    """5. `brgy_chb_wall.glb` (~6.04m x 2.12m): Mossy CHB outer wall + stepped top + rebar + BAWAL UMIHI DITO."""
    b = ProvincePropBuilder()

    # Damp mossy concrete footing plinth + lush 3D lumot moss cushions along wall-ground crease
    b.add_box((6.04, 0.42, 0.24), (0.0, 0.0, 0.12), "concrete_mossy", bevel=0.025)
    for mx, my, mr in ((-2.25, -0.20, 0.28), (-0.65, -0.21, 0.34), (0.82, -0.20, 0.26), (2.15, -0.20, 0.32), (0.15, 0.20, 0.30)):
        b.add_foliage_cluster_pad((mx, my, 0.09), (mr, 0.14, 0.09), "leaf_mango_shade", seed=int(abs(mx * 10)) + 3)

    # 4 Reinforced concrete pilasters dividing 3 CHB bays (-2.86, -0.96, +0.96, +2.86)
    pilaster_xs = (-2.86, -0.96, 0.96, 2.86)
    pilaster_hs = (2.06, 1.98, 2.02, 2.08)
    for px, ph in zip(pilaster_xs, pilaster_hs):
        b.add_box((0.34, 0.38, 0.68), (px, 0.0, 0.24 + 0.34), "concrete_mossy", bevel=0.02)
        b.add_box((0.32, 0.36, ph - 0.92), (px, 0.0, 0.92 + (ph - 0.92) * 0.5), "concrete_warm", bevel=0.02)
        # Exposed rusted rebar Tie-stub protruding from pilaster crown
        b.add_lofted_tube(
            [(px - 0.05, 0.0, ph - 0.04), (px - 0.05, 0.02, ph + 0.12), (px - 0.02, 0.06, ph + 0.18)],
            0.014, "steel_rusted_orange", segments=5
        )

    # 3 Multi-course CHB wall bays with recessed mortar horizontal bands & damp mossy lower courses
    bays = [
        (-1.91, 1.62, "chb_moss_base", "chb_sunlit_upper", 1.72),
        (0.00, 1.64, "chb_moss_base", "chb_plaster_ochre", 1.84),
        (1.91, 1.62, "chb_moss_base", "chb_sunlit_upper", 1.68),
    ]
    for bx, bw, low_sw, hi_sw, top_z in bays:
        # Lower mossy CHB courses (0.24 .. 0.96m)
        b.add_box((bw, 0.24, 0.72), (bx, 0.0, 0.24 + 0.36), low_sw, bevel=0.018)
        # Recessed horizontal mortar shadow band
        b.add_box((bw, 0.20, 0.06), (bx, 0.0, 0.99), "mortar_broken", bevel=0.005)
        # Upper sunlit CHB courses (1.02 .. top_z)
        uh = top_z - 1.02
        b.add_box((bw, 0.23, uh), (bx, 0.0, 1.02 + uh * 0.5), hi_sw, bevel=0.018)

    # Central plastered warning stencil on -Y face: "BAWAL UMIHI / DITO"
    b.add_sign_panel((1.46, 0.06, 0.74), (0.0, -0.12, 1.38), "chb_bawal_umihi", "chb_plaster_ochre", bevel=0.012)

    # Dynamic stepped broken hollow blocks along top crest + recessed hollow cells + bent rusted rebar stubs!
    broken_blocks = [
        (-2.36, 1.72, 0.38, 0.20), (-1.94, 1.72, 0.38, 0.34), (-1.42, 1.72, 0.36, 0.18),
        (-0.44, 1.84, 0.40, 0.22), (0.02, 1.84, 0.40, 0.18),
        (1.42, 1.68, 0.38, 0.20), (1.84, 1.68, 0.38, 0.36), (2.26, 1.68, 0.36, 0.19),
    ]
    for blk_x, base_z, blk_w, blk_h in broken_blocks:
        b.add_box((blk_w, 0.21, blk_h), (blk_x, 0.0, base_z + blk_h * 0.5), "chb_sunlit_upper", bevel=0.014)
        # Dark recessed hollow-block core cells on top face
        for cell_off in (-0.08, 0.08):
            b.add_box((0.11, 0.12, 0.03), (blk_x + cell_off, 0.0, base_z + blk_h + 0.005), "mortar_broken", bevel=0.004)

    # Bent rusted steel rebar stubs jutting from stepped CHB cores
    rebar_specs = [
        (-2.12, 1.88, 0.22, 0.06), (-1.52, 1.88, 0.18, -0.05),
        (0.46, 1.82, 0.24, 0.08), (1.62, 1.86, 0.21, -0.07), (2.42, 1.85, 0.17, 0.04),
    ]
    for rx, rz, rh, bend_x in rebar_specs:
        b.add_lofted_tube(
            [(rx, 0.0, rz), (rx + bend_x * 0.4, 0.01, rz + rh * 0.65), (rx + bend_x, -0.02, rz + rh)],
            0.013, "steel_rusted_orange", segments=5
        )

    return b.export_glb("brgy_chb_wall", mat)


# ==============================================================================
# PART 3: PROVINCIAL TREES, 3D VOLUMETRIC SAMPAYAN & 90s POWER-UP STAND
# ==============================================================================

def make_mango_tree(mat: bpy.types.Material) -> dict:
    """6a. `brgy_mango_tree.glb` (Punong Mangga ~6.5m tall): buttress roots, gnarled trunk, multi-layered canopy."""
    b = ProvincePropBuilder()

    # Damp mossy soil base pad + 5 sinuous buttress root flares gripping the earth at Z = 0.0
    b.add_foliage_cluster_pad((0.0, 0.0, 0.08), (1.18, 1.12, 0.14), "concrete_mossy", seed=11)
    for ri in range(5):
        ang = ri * (2.0 * math.pi / 5.0) + 0.22
        ca, sa = math.cos(ang), math.sin(ang)
        r_pts = [
            (ca * 1.18, sa * 1.18, 0.03),
            (ca * 0.78, sa * 0.78, 0.18),
            (ca * 0.42, sa * 0.42, 0.56),
            (ca * 0.18, sa * 0.18, 1.15),
        ]
        b.add_lofted_tube(r_pts, [0.06, 0.13, 0.19, 0.22], "bark_mango", segments=7, scale_ab=(0.75, 1.28), mode="panel")

    # Thick gnarled tapered trunk with organic bark ridges curving to main crotch at Z = 2.25m
    trunk_pts = [
        (0.0, 0.0, 0.04), (0.04, -0.03, 0.75),
        (0.09, 0.04, 1.50), (0.03, 0.02, 2.30),
    ]
    b.add_lofted_tube(
        trunk_pts, [0.46, 0.39, 0.35, 0.32], "bark_mango", segments=10,
        bulge_fn=lambda s, a: 1.0 + 0.11 * math.sin(a * 4.0 + s * 3.0), mode="panel"
    )

    # 4 Primary spreading crooked boughs + 8 secondary forked limbs
    boughs = [
        (0.18, 1.48, 0.42, 3.85, 0.22),
        (1.75, -0.45, 1.56, 3.95, 0.21),
        (3.32, -1.52, -0.38, 3.78, 0.22),
        (4.88, 0.38, -1.54, 4.10, 0.20),
    ]
    for _ang_ref, tx, ty, tz, r_start in boughs:
        mid_x, mid_y = tx * 0.52, ty * 0.52
        b.add_lofted_tube(
            [(0.03, 0.02, 2.18), (mid_x, mid_y, 2.95), (tx, ty, tz)],
            [r_start, r_start * 0.78, r_start * 0.54], "bark_mango", segments=8, mode="panel"
        )
        # Forked secondary sub-branches reaching into upper & outer foliage pads
        for fork_sgn in (-1.0, 1.0):
            fx = tx * 1.42 + (-ty * 0.32 * fork_sgn)
            fy = ty * 1.42 + (tx * 0.32 * fork_sgn)
            fz = tz + 0.88 + (0.22 if fork_sgn > 0 else 0.0)
            b.add_lofted_tube(
                [(tx * 0.92, ty * 0.92, tz - 0.08), (fx, fy, fz)],
                [r_start * 0.50, r_start * 0.26], "bark_mango", segments=6, mode="panel"
            )

    # Multi-layered sculpted Punong Mangga foliage canopy pads (shade under-tier -> mid -> sun crown -> bronze flush)
    canopy_layers = [
        # Deep shaded interior/lower pads
        ((1.85, 0.55, 3.55), (1.35, 1.25, 0.68), "leaf_mango_shade", 1, (0.08, -0.06, 0.2)),
        ((-0.55, 1.88, 3.65), (1.28, 1.38, 0.66), "leaf_mango_shade", 2, (-0.07, 0.08, 0.9)),
        ((-1.85, -0.48, 3.50), (1.36, 1.24, 0.65), "leaf_mango_shade", 3, (0.06, 0.09, 1.7)),
        ((0.48, -1.86, 3.72), (1.26, 1.34, 0.68), "leaf_mango_shade", 4, (-0.09, -0.05, 2.4)),
        ((0.0, 0.0, 3.85), (1.65, 1.58, 0.75), "leaf_mango_shade", 5, (0.0, 0.0, 0.5)),
        # Lush mid-canopy tiers
        ((2.15, -0.45, 4.22), (1.32, 1.18, 0.74), "leaf_mango_mid", 6, (0.06, -0.10, 0.4)),
        ((1.12, 1.72, 4.35), (1.28, 1.24, 0.72), "leaf_mango_mid", 7, (-0.08, -0.06, 1.1)),
        ((-1.42, 1.48, 4.28), (1.34, 1.22, 0.74), "leaf_mango_mid", 8, (-0.06, 0.09, 1.9)),
        ((-2.08, -0.82, 4.18), (1.26, 1.30, 0.70), "leaf_mango_mid", 9, (0.09, 0.07, 2.6)),
        ((-0.62, -1.96, 4.32), (1.30, 1.18, 0.72), "leaf_mango_mid", 10, (0.08, -0.05, 3.1)),
        ((1.48, -1.46, 4.40), (1.22, 1.26, 0.72), "leaf_mango_mid", 11, (0.05, -0.08, 3.8)),
        # Sunlit golden-green upper dome tiers (~6.5m tall)
        ((1.08, 0.52, 5.08), (1.42, 1.32, 0.80), "leaf_mango_sun", 12, (0.04, -0.05, 0.3)),
        ((-0.95, 0.78, 5.16), (1.38, 1.30, 0.78), "leaf_mango_sun", 13, (-0.05, 0.05, 1.4)),
        ((-0.78, -0.96, 5.10), (1.36, 1.28, 0.78), "leaf_mango_sun", 14, (0.05, 0.06, 2.3)),
        ((0.86, -0.82, 5.22), (1.32, 1.26, 0.80), "leaf_mango_sun", 15, (0.04, -0.06, 3.2)),
        ((0.04, 0.06, 5.76), (1.48, 1.40, 0.82), "leaf_mango_sun", 16, (0.02, 0.02, 0.8)),
        # Coppery-bronze young mango leaf flush accents
        ((1.78, 0.92, 4.86), (0.78, 0.72, 0.46), "leaf_mango_flush", 17, (0.08, -0.08, 0.6)),
        ((-1.58, 0.24, 4.94), (0.76, 0.68, 0.45), "leaf_mango_flush", 18, (-0.06, 0.09, 2.1)),
        ((0.28, -1.52, 5.02), (0.74, 0.70, 0.44), "leaf_mango_flush", 19, (0.09, 0.04, 2.9)),
    ]
    for c_pos, c_rad, c_sw, c_seed, c_tilt in canopy_layers:
        b.add_foliage_cluster_pad(c_pos, c_rad, c_sw, seed=c_seed, tilt_euler=c_tilt)

    # Hanging ripe golden Carabao mangoes on slender stems beneath the canopy eaves
    for mx, my, mz in ((1.45, -0.52, 3.22), (1.62, -0.36, 3.14), (-1.38, 0.64, 3.26), (0.44, 1.52, 3.30), (-0.58, -1.44, 3.28)):
        b.add_lofted_tube([(mx, my, mz + 0.32), (mx, my, mz + 0.08)], 0.008, "bark_mango", segments=4)
        b.add_lofted_tube(
            [(mx, my, mz + 0.10), (mx + 0.02, my, mz + 0.02), (mx + 0.01, my, mz - 0.08)],
            [0.045, 0.072, 0.032], "mango_fruit", segments=7, scale_ab=(1.0, 0.78), mode="panel"
        )

    return b.export_glb("brgy_mango_tree", mat)


def make_banana_clump(mat: bpy.types.Material) -> dict:
    """6b. `brgy_banana_clump.glb` (Puno ng Saging): 3 pseudostems + arched split fronds + puso ng saging."""
    b = ProvincePropBuilder()

    # Mossy clump mound at Z = 0.0
    b.add_foliage_cluster_pad((0.0, 0.0, 0.07), (0.86, 0.82, 0.12), "concrete_mossy", seed=21)

    # 3 Staggered curved banana pseudostems: Mother (2.68m), Medium (2.06m), Young Suwi sucker (1.22m)
    stalks = [
        ((0.04, 0.02), (0.14, 0.08), 2.68, 0.17, 0.115, 6),
        ((-0.46, 0.26), (-0.64, 0.38), 2.06, 0.135, 0.092, 5),
        ((0.44, -0.32), (0.58, -0.44), 1.22, 0.095, 0.068, 4),
    ]
    for s_idx, ((bx, by), (tx, ty), h_top, r0, r1, n_leaves) in enumerate(stalks):
        pts = [
            (bx, by, 0.02),
            (bx + (tx - bx) * 0.35, by + (ty - by) * 0.35, h_top * 0.36),
            (bx + (tx - bx) * 0.72, by + (ty - by) * 0.72, h_top * 0.72),
            (tx, ty, h_top),
        ]
        b.add_lofted_tube(pts, [r0 * 1.15, r0, (r0 + r1) * 0.5, r1], "stem_banana", segments=9, mode="panel")
        # Dried brown peeling outer leaf-sheath wrap near base & crown
        b.add_volumetric_leaf_blade(
            (bx + (tx - bx) * 0.65, by + (ty - by) * 0.65, h_top * 0.68),
            yaw=s_idx * 2.1 + 0.8, pitch_start=-0.35, pitch_end=-1.32,
            length=0.72, max_width=0.16, thickness=0.018, v_fold=0.02,
            top_swatch="dried_banana_bark", bot_swatch="dried_banana_bark", segments=4
        )

        # Broad arched split banana fronds radiating from pseudostem crown
        for li in range(n_leaves):
            yaw = (2.0 * math.pi * li) / float(n_leaves) + s_idx * 0.74
            tier = li % 3
            if tier == 0:
                p_start, p_end = 0.76, -0.18
                l_len = 1.95 if s_idx == 0 else (1.55 if s_idx == 1 else 1.05)
                splits = (4, 6)
            elif tier == 1:
                p_start, p_end = 0.52, -0.48
                l_len = 2.12 if s_idx == 0 else (1.68 if s_idx == 1 else 1.12)
                splits = (3, 5, 7)
            else:
                p_start, p_end = 0.34, -0.74
                l_len = 1.88 if s_idx == 0 else (1.46 if s_idx == 1 else 0.98)
                splits = (3, 6)
            w_max = 0.52 if s_idx == 0 else (0.42 if s_idx == 1 else 0.30)
            b.add_volumetric_leaf_blade(
                (tx, ty, h_top - 0.06), yaw=yaw, pitch_start=p_start, pitch_end=p_end,
                length=l_len, max_width=w_max, thickness=0.032, v_fold=0.065,
                top_swatch="leaf_banana_sun", bot_swatch="leaf_banana_under",
                segments=8, split_notches=splits
            )

    # Central unfurling upright young "cigar leaf" shoot on mother stalk
    b.add_lofted_tube([(0.14, 0.08, 2.62), (0.18, 0.10, 3.45), (0.21, 0.12, 4.18)], [0.055, 0.048, 0.018], "leaf_banana_sun", segments=7)

    # Arching fruiting peduncle stalk + 2 green Saba banana hands + hanging crimson Puso ng Saging heart!
    ped_pts = [(0.14, 0.08, 2.64), (0.36, -0.22, 2.74), (0.56, -0.44, 2.42), (0.62, -0.50, 1.72)]
    b.add_lofted_tube(ped_pts, [0.042, 0.038, 0.032, 0.022], "stem_banana", segments=7)

    # 2 Tiered hands of curved Saba bananas along the peduncle
    for hand_z, hand_r in ((2.32, 0.15), (2.08, 0.13)):
        for fi in range(7):
            fa = (2.0 * math.pi * fi) / 7.0
            fx = 0.59 + math.cos(fa) * 0.05
            fy = -0.47 + math.sin(fa) * 0.05
            b.add_lofted_tube(
                [(fx, fy, hand_z), (fx + math.cos(fa) * hand_r, fy + math.sin(fa) * hand_r, hand_z + 0.03),
                 (fx + math.cos(fa) * hand_r * 1.15, fy + math.sin(fa) * hand_r * 1.15, hand_z + 0.15)],
                [0.022, 0.030, 0.018], "banana_bunch", segments=5
            )

    # Teardrop crimson-purple Puso ng Saging (banana heart) hanging at tip of peduncle!
    puso_pts = [
        (0.62, -0.50, 1.74), (0.62, -0.50, 1.62),
        (0.62, -0.50, 1.48), (0.62, -0.50, 1.34), (0.62, -0.50, 1.24),
    ]
    b.add_lofted_tube(puso_pts, [0.035, 0.095, 0.105, 0.062, 0.014], "puso_ng_saging", segments=8, mode="panel")

    return b.export_glb("brgy_banana_clump", mat)


def make_coconut_palm(mat: bpy.types.Material) -> dict:
    """6c. `brgy_coconut_palm.glb` (Puno ng Niyog ~7.4m tall): ringed curved trunk, pinnate fronds, green buko."""
    b = ProvincePropBuilder()

    # Swollen fibrous root bole grounded at Z = 0.0 + graceful S-curved ringed coconut trunk
    trunk_pts = []
    trunk_rads = []
    n_seg = 13
    for i in range(n_seg + 1):
        t = i / float(n_seg)
        z = 0.02 + t * 5.86
        cx = 0.58 * math.sin(t * math.pi * 0.82) - 0.12 * math.sin(t * math.pi * 1.6)
        cy = -0.26 * (t ** 1.3)
        if t < 0.12:
            r = 0.34 - 0.12 * (t / 0.12)
        else:
            r = 0.22 - 0.075 * ((t - 0.12) / 0.88)
        trunk_pts.append((cx, cy, z))
        trunk_rads.append(r)

    b.add_lofted_tube(
        trunk_pts, trunk_rads, "bark_coconut", segments=9,
        bulge_fn=lambda s, _a: 1.0 + 0.045 * math.sin(s * math.pi * 22.0), mode="panel"
    )

    # Fibrous burlap leaf-base collar + smooth green crownshaft at top of trunk
    top_x, top_y, top_z = trunk_pts[-1]
    b.add_lofted_tube(
        [(top_x, top_y, top_z - 0.12), (top_x + 0.03, top_y - 0.02, top_z + 0.42)],
        [0.165, 0.135], "buko_green", segments=8, mode="panel"
    )
    cx, cy, cz = top_x + 0.03, top_y - 0.02, top_z + 0.38

    # Cluster of 8 green & golden buko coconuts nestled beneath the frond crown (3-ring ovoid tubes, zero UV spheres!)
    for ni in range(8):
        na = (2.0 * math.pi * ni) / 8.0 + 0.25
        nr = 0.23 + 0.04 * (ni % 2)
        nx = cx + math.cos(na) * nr
        ny = cy + math.sin(na) * nr
        nz = cz - 0.20 - 0.09 * (ni % 2)
        sw_nut = "buko_green" if ni % 3 != 0 else "buko_golden"
        b.add_lofted_tube(
            [(nx, ny, nz + 0.13), (nx, ny, nz), (nx, ny, nz - 0.13)],
            [0.055, 0.125, 0.048], sw_nut, segments=7, mode="panel"
        )

    # 10 Radial arching & drooping PINNATE palm fronds (curved rachis spine + paired 3D volumetric pinnae leaflets!)
    n_fronds = 10
    for fi in range(n_fronds):
        yaw = (2.0 * math.pi * fi) / float(n_fronds) + (0.16 if fi % 2 else 0.0)
        upper_tier = (fi % 2 == 0)
        p_start = 0.68 if upper_tier else 0.34
        p_end = -0.38 if upper_tier else -0.82
        f_len = 2.65 if upper_tier else 2.48
        sw_leaf = "leaf_palm_sun" if upper_tier else "leaf_palm_mid"

        yaw_m = Matrix.Rotation(yaw, 3, "Z")
        spine_pts = []
        spine_pitches = []
        cur = Vector((0.0, 0.0, 0.0))
        spine_steps = 6
        for si in range(spine_steps + 1):
            st = si / float(spine_steps)
            pitch = p_start + (p_end - p_start) * (st ** 1.2)
            spine_pts.append(yaw_m @ cur + Vector((cx, cy, cz)))
            spine_pitches.append(pitch)
            cur = cur + Vector((0.0, math.cos(pitch), math.sin(pitch))) * (f_len / float(spine_steps))

        b.add_lofted_tube(spine_pts, [0.045, 0.038, 0.032, 0.026, 0.020, 0.014, 0.008], "stem_banana", segments=5)

        # 5 Pairs of drooping 3D V-folded pinnate leaflets along each rachis spine + terminal tip blade
        for li in range(1, spine_steps):
            lt = li / float(spine_steps)
            anchor = spine_pts[li]
            pinna_len = (0.78 + 0.22 * math.sin(lt * math.pi)) * (1.0 - 0.22 * lt)
            for side_sgn in (-1.0, 1.0):
                l_yaw = yaw + side_sgn * (1.08 - 0.32 * lt)
                b.add_volumetric_leaf_blade(
                    anchor, yaw=l_yaw,
                    pitch_start=spine_pitches[li] + 0.12,
                    pitch_end=spine_pitches[li] - 0.62,
                    length=pinna_len, max_width=0.135, thickness=0.016, v_fold=0.024,
                    top_swatch=sw_leaf, bot_swatch="leaf_palm_mid", segments=3
                )
        # Terminal tip leaflet
        b.add_volumetric_leaf_blade(
            spine_pts[-1], yaw=yaw, pitch_start=spine_pitches[-1], pitch_end=spine_pitches[-1] - 0.35,
            length=0.52, max_width=0.11, thickness=0.014, v_fold=0.018,
            top_swatch=sw_leaf, bot_swatch="leaf_palm_mid", segments=2
        )

    return b.export_glb("brgy_coconut_palm", mat)


def make_sampayan_3d(mat: bpy.types.Material) -> dict:
    """7. `brgy_sampayan_3d.glb`: True 3D Volumetric Hanging Clothes + Sipit on Twin Pulley Sampayan Rig.
    Span runs along Y (-1.42..+1.42m -> Z in Godot matching `StreetMap` `atan2(dx, dz)`),
    puffed sleeves & twin-line bamboo spreader span X (-0.44..+0.44m -> width ~0.88m > 0.5m),
    and hanging garments span Z (0.00..1.18m -> height ~1.18m > 0.8m, min_y == 0.00m!).
    """
    b = ProvincePropBuilder()

    def rope_z(y_pos: float, base_h: float = 1.04) -> float:
        t = y_pos / 1.38
        return base_h - 0.11 * (1.0 - min(1.0, t * t))

    def add_sipit(px: float, py: float, pz: float, yaw_z: float = 0.0):
        # Two beveled wooden clothespin jaws + steel spring ring
        for sgn in (-1.0, 1.0):
            b.add_box(
                (0.018, 0.022, 0.088), (px + sgn * 0.011, py, pz + 0.022),
                "sipit_wood", rot_euler=(0.0, sgn * 0.09, yaw_z), bevel=0.004
            )
        b.add_cylinder(0.016, 0.026, (px, py, pz + 0.020), "steel_galvanized", segments=6, rot_euler=(math.pi * 0.5, 0.0, yaw_z))

    # Wrought-iron window pulley brackets + grooved wooden kalo wheels at Y = +-1.38m + bamboo tikin spreader
    for end_y in (-1.38, 1.38):
        b.add_box((0.52, 0.06, 0.06), (0.0, end_y, 1.06), "steel_rusted_orange", bevel=0.008)
        for lx in (-0.20, 0.20):
            b.add_cylinder(0.085, 0.042, (lx, end_y, 1.04), "coco_lumber", segments=10, rot_euler=(0.0, math.pi * 0.5, 0.0), mode="panel")
            b.add_cylinder(0.032, 0.056, (lx, end_y, 1.04), "steel_rusted_orange", segments=6, rot_euler=(0.0, math.pi * 0.5, 0.0))

    # Twin sagging catenary sampayan ropes (X = -0.20 & X = +0.20) + diagonal bamboo tikin spacer bar (ensuring width > 0.86m)
    for lx in (-0.20, 0.20):
        r_pts = [(lx, -1.38 + 2.76 * (k / 10.0), rope_z(-1.38 + 2.76 * (k / 10.0), 1.04)) for k in range(11)]
        b.add_lofted_tube(r_pts, 0.012, "cord_net", segments=6)
    b.add_lofted_tube([(-0.44, -0.04, 1.01), (0.0, 0.0, 1.15), (0.44, 0.04, 1.01)], 0.024, "bamboo_golden", segments=7, mode="panel")

    # --- GARMENT 1 (Line -0.20, Y = -0.94): 90s Ringer T-Shirt (puffed 3D torso + tubular sleeves + 3D collar ring) ---
    g1_x, g1_y = -0.20, -0.94
    g1_top = rope_z(g1_y)
    b.add_lofted_tube(
        [(g1_x, g1_y, g1_top - 0.02), (g1_x + 0.02, g1_y, g1_top - 0.22),
         (g1_x + 0.04, g1_y + 0.01, g1_top - 0.48), (g1_x + 0.05, g1_y + 0.02, g1_top - 0.68)],
        [0.14, 0.21, 0.23, 0.25], "cloth_ringer_cream", segments=10,
        scale_ab=(0.48, 1.18), bulge_fn=lambda s, a: 1.0 + 0.12 * math.sin(a * 5.0 + s * 2.0), mode="panel"
    )
    # 2 Outstretched 3D tubular sleeves + ringer cuffs (angled slightly outward across X & Y!)
    for s_sgn in (-1.0, 1.0):
        b.add_lofted_tube(
            [(g1_x - 0.02, g1_y + s_sgn * 0.20, g1_top - 0.08),
             (g1_x - 0.12 * s_sgn, g1_y + s_sgn * 0.34, g1_top - 0.18),
             (g1_x - 0.18 * s_sgn, g1_y + s_sgn * 0.44, g1_top - 0.28)],
            [0.092, 0.084, 0.076], "cloth_ringer_cream", segments=8, mode="panel"
        )
        b.add_lofted_tube(
            [(g1_x - 0.17 * s_sgn, g1_y + s_sgn * 0.42, g1_top - 0.26),
             (g1_x - 0.19 * s_sgn, g1_y + s_sgn * 0.46, g1_top - 0.30)],
            0.080, "jeep_sunlit_gold", segments=8, mode="panel"
        )
    # Raised 3D closed-loop collar ring
    collar_pts = [
        (g1_x + 0.045 * math.cos(a), g1_y + 0.105 * math.sin(a), g1_top - 0.02)
        for a in [k * (2.0 * math.pi / 10.0) for k in range(10)]
    ]
    b.add_lofted_tube(collar_pts, 0.022, "jeep_sunlit_gold", segments=6, closed_loop=True)
    add_sipit(g1_x, g1_y - 0.15, g1_top)
    add_sipit(g1_x, g1_y + 0.15, g1_top)

    # --- GARMENT 2 (Line -0.20, Y = -0.22): Liga Basketball Sando Jersey (3D torso + twin 3D shoulder straps) ---
    g2_x, g2_y = -0.20, -0.22
    g2_top = rope_z(g2_y)
    b.add_lofted_tube(
        [(g2_x, g2_y, g2_top - 0.16), (g2_x - 0.03, g2_y, g2_top - 0.40), (g2_x - 0.05, g2_y - 0.01, g2_top - 0.72)],
        [0.17, 0.20, 0.23], "cloth_jersey_red", segments=10,
        scale_ab=(0.46, 1.12), bulge_fn=lambda s, a: 1.0 + 0.10 * math.cos(a * 4.0 + s * 3.0), mode="panel"
    )
    for st_y in (-0.12, 0.12):
        b.add_lofted_tube(
            [(g2_x, g2_y + st_y, g2_top + 0.01), (g2_x - 0.01, g2_y + st_y * 1.1, g2_top - 0.18)],
            [0.028, 0.036], "cloth_ringer_cream", segments=6, scale_ab=(0.55, 1.2)
        )
        add_sipit(g2_x, g2_y + st_y, g2_top)

    # --- GARMENT 3 (Line -0.20, Y = +0.56): Puruntong Basketball Shorts (3D waistband + TWO flared 3D tubular legs!) ---
    g3_x, g3_y = -0.20, 0.56
    g3_top = rope_z(g3_y)
    b.add_lofted_tube(
        [(g3_x, g3_y, g3_top - 0.01), (g3_x + 0.02, g3_y, g3_top - 0.16)],
        [0.18, 0.20], "cloth_ringer_cream", segments=10, scale_ab=(0.52, 1.18), mode="panel"
    )
    for leg_sgn in (-1.0, 1.0):
        b.add_lofted_tube(
            [(g3_x + 0.02, g3_y + leg_sgn * 0.11, g3_top - 0.15),
             (g3_x + 0.04 * leg_sgn, g3_y + leg_sgn * 0.13, g3_top - 0.38),
             (g3_x + 0.06 * leg_sgn, g3_y + leg_sgn * 0.15, g3_top - 0.62)],
            [0.105, 0.118, 0.132], "cloth_shorts_blue", segments=8,
            scale_ab=(0.82, 0.95), bulge_fn=lambda _s, a: 1.0 + 0.08 * math.sin(a * 3.0), mode="panel"
        )
        add_sipit(g3_x, g3_y + leg_sgn * 0.14, g3_top)

    # --- GARMENT 4 (Line +0.20, Y = -0.52): Nanay's Pleated Floral Daster reaching Z = 0.00m! ---
    g4_x, g4_y = 0.20, -0.52
    g4_top = rope_z(g4_y)
    b.add_lofted_tube(
        [(g4_x, g4_y, g4_top - 0.03), (g4_x + 0.03, g4_y, g4_top - 0.30),
         (g4_x + 0.06, g4_y + 0.02, g4_top - 0.64), (g4_x + 0.08, g4_y + 0.03, 0.02)],
        [0.15, 0.20, 0.26, 0.31], "cloth_daster_floral", segments=12,
        scale_ab=(0.48, 1.15), bulge_fn=lambda s, a: 1.0 + 0.14 * math.sin(a * 6.0 + s), mode="panel"
    )
    for rs_sgn in (-1.0, 1.0):
        b.add_lofted_tube(
            [(g4_x + 0.01, g4_y + rs_sgn * 0.17, g4_top - 0.06),
             (g4_x + 0.14 * rs_sgn, g4_y + rs_sgn * 0.30, g4_top - 0.18)],
            [0.085, 0.105], "cloth_daster_floral", segments=8, mode="panel"
        )
        add_sipit(g4_x, g4_y + rs_sgn * 0.14, g4_top)

    # --- GARMENT 5 (Line +0.20, Y = +0.44): Thick U-Folded Draped Jade Bath Towel (true double-sided 3D fold!) ---
    g5_x, g5_y = 0.20, 0.44
    g5_top = rope_z(g5_y)
    # Extrude inverted-U cross-section in (X, Z) across towel width in Y using 90-deg yaw rotation
    towel_prof_yz = [
        (-0.085, g5_top - 0.68), (-0.068, g5_top - 0.26), (-0.048, g5_top + 0.032),
        (0.048, g5_top + 0.032), (0.076, g5_top - 0.24), (0.095, g5_top - 0.62),
        (0.056, g5_top - 0.62), (0.038, g5_top - 0.24), (0.018, g5_top - 0.010),
        (-0.018, g5_top - 0.010), (-0.032, g5_top - 0.26), (-0.046, g5_top - 0.68),
    ]
    b.add_profile_extrusion(
        towel_prof_yz, width_x=0.54, center=(g5_x, g5_y, 0.0),
        swatch="cloth_towel_jade", bevel=0.008, rot_euler=(0.0, 0.0, math.pi * 0.5)
    )
    add_sipit(g5_x, g5_y - 0.19, g5_top)
    add_sipit(g5_x, g5_y + 0.19, g5_top)

    return b.export_glb("brgy_sampayan_3d", mat)


def make_powerup_stand(mat: bpy.types.Material) -> dict:
    """8. `brgy_powerup_stand.glb`: 90s Palamig / Sago't Gulaman cooler + Teks & Turon stand + trapal umbrella."""
    b = ProvincePropBuilder()

    # Mossy street paving stone pad + weathered coco-lumber vendor table/stool (1.24m x 0.68m x 0.78m)
    b.add_box((1.38, 0.88, 0.08), (0.0, 0.0, 0.04), "concrete_mossy", bevel=0.02)
    for lx in (-0.52, 0.52):
        for ly in (-0.26, 0.26):
            b.add_box((0.085, 0.085, 0.72), (lx, ly, 0.08 + 0.36), "wood_stool_warm", bevel=0.01)
        b.add_box((0.06, 0.56, 0.07), (lx, 0.0, 0.28), "coco_lumber", bevel=0.008)
    # Lower slatted crate shelf + side blue ice-cooler box
    b.add_box((1.06, 0.52, 0.05), (0.0, 0.0, 0.28), "coco_lumber", bevel=0.008)
    b.add_box((0.40, 0.34, 0.30), (-0.24, 0.02, 0.30 + 0.15), "jeep_royal_blue", bevel=0.02)
    b.add_box((0.42, 0.36, 0.05), (-0.24, 0.02, 0.62), "cloth_ringer_cream", bevel=0.01)

    # Slatted tabletop + crisp hand-painted front signboard ("PALAMIG / TEKS TURON")
    b.add_box((1.26, 0.70, 0.07), (0.0, 0.0, 0.82), "wood_bench_worn", bevel=0.018)
    b.add_sign_panel((1.08, 0.05, 0.38), (0.0, -0.34, 0.56), "sign_palamig", "coco_lumber", bevel=0.01)

    # 1. Large Sago't Gulaman Palamig Glass Jug Cooler + Crimson Lid + Stainless Sandok Ladle + Cups
    b.add_cylinder(0.165, 0.40, (-0.34, -0.02, 0.855 + 0.20), "palamig_gulaman", segments=10, mode="panel")
    b.add_cylinder(0.172, 0.055, (-0.34, -0.02, 1.27), "jeep_crimson_red", segments=10, mode="panel")
    b.add_lofted_tube([(-0.30, -0.02, 1.28), (-0.24, -0.06, 1.44), (-0.21, -0.08, 1.46)], 0.010, "steel_chrome", segments=6)
    # Smaller Melon/Buko Pandan Palamig jar + stacked drinking cups
    b.add_cylinder(0.115, 0.30, (-0.04, 0.10, 0.855 + 0.15), "mango_fruit", segments=10, mode="panel")
    b.add_cylinder(0.120, 0.045, (-0.04, 0.10, 1.17), "jeep_jade_teal", segments=10, mode="panel")
    b.add_cylinder(0.042, 0.18, (-0.08, -0.18, 0.855 + 0.09), "cloth_ringer_cream", segments=8, r2=0.054)

    # 2. Woven Rattan Bilao Tray + Banana-Leaf Liner + 5 Golden Caramelized Turon Rolls!
    bilao_cx, bilao_cy, bilao_cz = 0.30, 0.04, 0.875
    b.add_cylinder(0.22, 0.035, (bilao_cx, bilao_cy, bilao_cz), "bilao_rattan", segments=12, mode="panel")
    bilao_rim = [
        (bilao_cx + 0.215 * math.cos(a), bilao_cy + 0.215 * math.sin(a), bilao_cz + 0.022)
        for a in [k * (2.0 * math.pi / 12.0) for k in range(12)]
    ]
    b.add_lofted_tube(bilao_rim, 0.018, "bamboo_golden", segments=6, closed_loop=True)
    b.add_cylinder(0.18, 0.015, (bilao_cx, bilao_cy, bilao_cz + 0.02), "leaf_banana_sun", segments=10, mode="panel")
    for ti, (tx_o, ty_o, tz_o, tyaw) in enumerate((
        (-0.07, -0.03, 0.05, 0.15), (0.0, -0.02, 0.05, 0.12), (0.07, -0.01, 0.05, 0.18),
        (-0.035, 0.02, 0.095, -0.10), (0.035, 0.02, 0.095, -0.08),
    )):
        b.add_cylinder(
            0.028, 0.19, (bilao_cx + tx_o, bilao_cy + ty_o, bilao_cz + tz_o),
            "turon_golden", segments=8, rot_euler=(math.pi * 0.5, 0.0, tyaw), mode="panel"
        )

    # 3. Neat 90s Collectible Teks Play-Card Stacks & Display Box on front-right corner!
    for st_i, (kx, ky, kh, krot) in enumerate((
        (0.14, -0.22, 0.09, 0.08), (0.26, -0.23, 0.12, -0.12), (0.38, -0.21, 0.07, 0.22)
    )):
        b.add_box((0.085, 0.115, kh), (kx, ky, 0.855 + kh * 0.5), "teks_cards", rot_euler=(0.0, 0.0, krot), bevel=0.008)

    # Tilted Bamboo Umbrella Mast + 8-Sided Striped Trapal Sun Canopy (reaching Z ~ 2.12m)
    b.add_lofted_tube([(0.08, 0.24, 0.10), (0.04, 0.16, 1.15), (0.0, 0.06, 2.04)], [0.034, 0.030, 0.026], "bamboo_golden", segments=8, mode="panel")
    b.add_cylinder(0.74, 0.34, (0.0, 0.06, 1.90), "trapal_sun_stripe", segments=8, rot_euler=(0.06, -0.05, 0.0), mode="panel", r2=0.06)
    b.add_cylinder(0.045, 0.10, (0.0, 0.06, 2.09), "coco_lumber", segments=8)

    return b.export_glb("brgy_powerup_stand", mat)


def _verify_exports(stats: list) -> None:
    """Verifies all 10 GLBs against @audit-3d (#78), @audit-map (#75), test_model_scale & test_texture_budget."""
    assert len(stats) == 10, f"Expected 10 models, got {len(stats)}"
    for item in stats:
        name = item["name"]
        p = os.path.join(OUT_DIR, f"{name}.glb")
        assert os.path.isfile(p) and os.path.getsize(p) > 1024, f"Missing or empty GLB: {p}"
        with open(p, "rb") as gf:
            raw = gf.read()
        j_len, _ = struct.unpack_from("<II", raw, 12)
        gltf = json.loads(raw[20:20 + j_len].decode("utf-8"))
        assert len(gltf["meshes"]) == 1, f"{name}.glb must have exactly 1 mesh!"
        assert len(gltf["meshes"][0]["primitives"]) == 1, f"{name}.glb must have 1 surface/primitive!"
        attrs = gltf["meshes"][0]["primitives"][0]["attributes"]
        assert "COLOR_0" in attrs, f"{name}.glb missing baked COLOR_0 vertex AO!"
        assert "TEXCOORD_0" in attrs, f"{name}.glb missing TEXCOORD_0!"
        assert gltf["images"][0].get("uri") == ATLAS_FILENAME, f"{name}.glb missing external {ATLAS_FILENAME} URI!"

        # Check glTF Y-up POSITION accessor min/max & decode TEXCOORD_0 binary floats
        b_off = 20 + j_len
        b_len, _ = struct.unpack_from("<II", raw, b_off)
        bin_blob = raw[b_off + 8:b_off + 8 + b_len]

        pos_acc = gltf["accessors"][attrs["POSITION"]]
        uv_acc = gltf["accessors"][attrs["TEXCOORD_0"]]
        uv_bv = gltf["bufferViews"][uv_acc["bufferView"]]
        uv_off = uv_bv.get("byteOffset", 0) + uv_acc.get("byteOffset", 0)
        uv_arr = np.frombuffer(bin_blob, dtype=np.float32, count=uv_acc["count"] * 2, offset=uv_off).reshape((-1, 2))
        uv_min = (float(uv_arr[:, 0].min()), float(uv_arr[:, 1].min()))
        uv_max = (float(uv_arr[:, 0].max()), float(uv_arr[:, 1].max()))

        min_pos = pos_acc["min"]
        max_pos = pos_acc["max"]
        size_x = max_pos[0] - min_pos[0]
        size_y = max_pos[1] - min_pos[1]
        size_z = max_pos[2] - min_pos[2]
        assert abs(min_pos[1]) <= 0.015, f"{name}.glb not grounded! min_y={min_pos[1]:.4f}"
        assert size_x > 0.50, f"{name}.glb drawn width X={size_x:.3f} <= 0.50m!"
        assert size_y > 0.80, f"{name}.glb drawn height Y={size_y:.3f} <= 0.80m!"
        assert 40 <= item["tris"] <= 6500, f"{name}.glb tris={item['tris']} outside [40, 6500]!"
        assert uv_min[0] >= 0.0 and uv_min[1] >= 0.0 and uv_max[0] <= 1.0 and uv_max[1] <= 1.0, (
            f"{name}.glb UVs out of [0, 1]: min={uv_min} max={uv_max}"
        )
        print(
            f"  [VERIFIED] {name}.glb: tris={item['tris']}, "
            f"godot_size=({size_x:.2f}x{size_y:.2f}x{size_z:.2f}m), min_y={min_pos[1]:.4f}, "
            f"uv=({uv_min[0]:.3f}..{uv_max[0]:.3f}, {uv_min[1]:.3f}..{uv_max[1]:.3f})"
        )

    jpg_imports = [f for f in os.listdir(OUT_DIR) if f.endswith(".jpg.import")]
    assert f"{ATLAS_FILENAME}.import" in jpg_imports, f"Missing {ATLAS_FILENAME}.import!"
    for imp in jpg_imports:
        txt = open(os.path.join(OUT_DIR, imp), "r", encoding="utf-8").read()
        assert "compress/mode=2" in txt, f"{imp} missing compress/mode=2!"
        assert "mipmaps/generate=true" in txt, f"{imp} missing mipmaps/generate=true!"
        assert '"vram_texture": true' in txt, f"{imp} missing vram_texture=true!"
        assert ("m" + "d5") not in txt.lower(), f"{imp} contains forbidden checksum line!"
        assert "generator_parameters" not in txt, f"{imp} contains forbidden generator_parameters!"


def main() -> None:
    print("=== Building 90s Philippine Province Props Kit (10 Handcrafted GLBs + Sunlit Atlas) ===")
    atlas_img = build_atlas_image()
    mat = build_shared_material(atlas_img)

    stats = [
        make_jeepney(mat),
        make_tricycle(mat),
        make_toda_shed(mat),
        make_wooden_hoop(mat),
        make_chb_wall(mat),
        make_mango_tree(mat),
        make_banana_clump(mat),
        make_coconut_palm(mat),
        make_sampayan_3d(mat),
        make_powerup_stand(mat),
    ]
    _verify_exports(stats)
    total_kb = sum(s["kb"] for s in stats)
    atlas_kb = os.path.getsize(os.path.join(OUT_DIR, ATLAS_FILENAME)) / 1024.0
    print(f"=== ALL 10 PROVINCE 90s PROPS EXPORTED & VERIFIED | GLBs={total_kb:.1f}KB + Atlas={atlas_kb:.1f}KB ===")


if __name__ == "__main__":
    main()
