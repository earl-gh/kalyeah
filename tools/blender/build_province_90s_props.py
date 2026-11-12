"""Deadlock-Quality V3 1990s Philippine Province Landmark & Prop Kit Generator (Blender 5.2+).

Generates `ProvincePropsAtlas.jpg` + `ProvincePropsAtlas_normal.png` (2048x2048) and 11
single-mesh, single-surface, BVH-AO-baked, normal-mapped `.glb` assets (`min_y = 0.000`):
  - brgy_jeepney        (Curved pressed-steel Sarao Jeepney + route board + spoked wheels)
  - brgy_tricycle       (Pinoy commuter motorcycle + galvanized kolong-kolong sidecar)
  - brgy_toda_shed      (Barangay TODA Tricycle Terminal dispatcher shed)
  - brgy_wooden_hoop    (Improvised coco-lumber kalye basketball post + chain net)
  - brgy_chb_wall       (Mossy CHB compound wall + broken plaster + iron grille + Bawal Umihi)
  - brgy_mango_tree     (Multi-forked gnarled Mango tree + layered foliage clusters + ripe mangoes)
  - brgy_coconut_palm   (Curved ringed Coconut palm + buko cluster + 13 arched pinnate fronds)
  - brgy_banana_clump   (3-stalk Saging clump + V-folded wind-split leaves + crimson puso heart)
  - brgy_sampayan_3d    (Twin bamboo T-posts + catenary lines + 5 sculpted draped garments + sipit)
  - brgy_powerup_stand  (90s Merienda Sago't Gulaman & Banana-Q power-up stall)
  - brgy_poster_cluster (Grounded barangay corner signpost + Halalan '95 & Malabanan notices)
"""

from __future__ import annotations

import math
import sys
import time
from pathlib import Path
from typing import Dict, List, Tuple

from mathutils import Matrix, Vector

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from barangay_v3_arch import (  # noqa: E402
    MeshBuilder,
    add_gable_yero_roof,
    add_rooftop_eave_signboard,
    add_shed_yero_roof,
    add_signboard_panel,
    finalize_and_export_glb,
)
from barangay_v3_textures import OUT_DIR, generate_province_props_atlas  # noqa: E402

ALBEDO_FILE = "ProvincePropsAtlas.jpg"
NORMAL_FILE = "ProvincePropsAtlas_normal.png"
ORM_FILE = "ProvincePropsAtlas_orm.png"


def _add_wheel_x(
    mb: MeshBuilder,
    cx: float,
    cy: float,
    cz: float,
    r_tire: float,
    width: float,
    rim_swatch: str = "wheel_rim",
) -> None:
    """Builds a treaded rubber tire + recessed lugged wheel rim + hub cap along the X axis (`min_z = cz - r_tire`)."""
    hw = width * 0.5
    # Outer rubber tire barrel
    mb.tube((cx - hw, cy, cz), (cx + hw, cy, cz), r_tire, r_tire, 14, "tire_rubber", cap_start=True, cap_end=True)
    # Recessed wheel rim discs + protruding center axle hubcap on outer face
    r_rim = r_tire * 0.64
    sgn = 1.0 if cx >= 0.0 else -1.0
    mb.tube((cx + sgn * (hw - 0.01), cy, cz), (cx + sgn * (hw + 0.018), cy, cz), r_rim, r_rim * 0.92, 10, rim_swatch)
    mb.tube((cx + sgn * (hw + 0.018), cy, cz), (cx + sgn * (hw + 0.045), cy, cz), r_rim * 0.28, r_rim * 0.18, 8, "jeep_chrome")


# ---------------------------------------------------------------------------
# 1. Iconic Sarao-Style Philippine Jeepney (`brgy_jeepney.glb` ~ 2.08m x 5.45m x 2.38m)
#    Front faces `-Y` in Blender -> `+Z` in Godot!
# ---------------------------------------------------------------------------

def build_jeepney(
    uvs: Dict[str, Tuple[float, float, float, float]],
    variant: str = "classic",
) -> MeshBuilder:
    mb = MeshBuilder(uvs)
    r_w = 0.40  # Wheel radius -> axle at Z = 0.40 touches ground Z = 0.000!
    is_prov = variant == "provincial"
    main_livery = "jeep_green" if is_prov else "jeep_red"
    trim_livery = "jeep_yellow"
    roof_top_sw = "yero_green" if is_prov else "jeep_blue"

    # 1. 4 Grounded Tires & Lugged Golden Rims + Wooden Wheel Chock (*Kalso*) + Steel Chassis Frame Rails
    for sx in (-0.84, 0.84):
        _add_wheel_x(mb, sx, -1.68, r_w, r_w, 0.24)
        _add_wheel_x(mb, sx, 1.38, r_w, r_w, 0.26)
    # Grounded wooden wheel chock wedged against front-right tire (`min_z = 0.000`)
    chock_x = -0.84 if is_prov else 0.84
    mb.frustum_plinth(chock_x, -2.02, 0.0, 0.08, 0.14, 0.22, 0.22, 0.20, 0.08, "wood_dark", "wood_coco")
    mb.box(0.0, -0.10, 0.36, 0.52, 1.32, 4.75, "steel_dark")
    # Heavy chrome front bumper (-Y) + rear diamond-plate boarding step (+Y)
    mb.box(0.0, -2.58, 0.40, 0.58, 1.98, 0.16, "jeep_chrome")
    for tx in (-0.65, 0.65):
        mb.box(tx, -2.66, 0.42, 0.62, 0.10, 0.10, "steel_dark")
    mb.box(0.0, 2.52, 0.34, 0.44, 1.48, 0.32, "jeep_chrome")

    # 2. Sculpted Tapered Pressed-Steel Sarao Engine Hood & Flared Front Fenders (`Y = -2.48..-1.12`)
    y_nose, y_cowl = -2.46, -1.12
    wn, wc = 0.61, 0.87
    zn_top, zc_top = 1.18, 1.32
    z_ch = 0.52

    # Front chrome radiator louver grille (`-Y` outward!)
    mb.quad((-wn, y_nose, z_ch), (wn, y_nose, z_ch), (wn * 0.94, y_nose, zn_top), (-wn * 0.94, y_nose, zn_top), "radiator_grille")
    # Rounded 3-facet arched hood crown (left shoulder, center chrome spear, right shoulder)
    hood_crown_sw = "jeep_green" if is_prov else "jeep_yellow"
    mb.quad((-wn * 0.94, y_nose, zn_top), (-0.14, y_nose, zn_top + 0.07), (-0.18, y_cowl, zc_top + 0.08), (-wc * 0.92, y_cowl, zc_top), hood_crown_sw)
    mb.quad((-0.14, y_nose, zn_top + 0.07), (0.14, y_nose, zn_top + 0.07), (0.18, y_cowl, zc_top + 0.08), (-0.18, y_cowl, zc_top + 0.08), "jeep_chrome")
    mb.quad((0.14, y_nose, zn_top + 0.07), (wn * 0.94, y_nose, zn_top), (wc * 0.92, y_cowl, zc_top), (0.18, y_cowl, zc_top), hood_crown_sw)
    # Hood side louver panels
    mb.quad((-wc, y_cowl, z_ch), (-wn, y_nose, z_ch), (-wn * 0.94, y_nose, zn_top), (-wc * 0.92, y_cowl, zc_top), main_livery)
    mb.quad((wn, y_nose, z_ch), (wc, y_cowl, z_ch), (wc * 0.92, y_cowl, zc_top), (wn * 0.94, y_nose, zn_top), main_livery)

    # Twin Sarao chrome hood ornaments + round bezel headlamps + swept front fenders over wheels!
    for sx in (-1.0, 1.0):
        mb.box(sx * 0.80, -1.68, 0.78, 0.90, 0.38, 1.12, "jeep_chrome")
        mb.box(sx * 0.80, -2.18, 0.52, 0.86, 0.36, 0.24, trim_livery)
        mb.tube((sx * 0.48, -2.44, 0.96), (sx * 0.48, -2.55, 0.96), 0.125, 0.115, 10, "headlamp_amber")
        mb.box(sx * 0.34, -2.22, zn_top + 0.04, zn_top + 0.20, 0.05, 0.24, "jeep_chrome")
        mb.tube((sx * 0.72, -1.95, 0.90), (sx * 0.98, -1.95, 1.42), 0.016, 0.014, 5, "jeep_chrome")
        mb.box(sx * 1.00, -1.95, 1.38, 1.54, 0.05, 0.14, "jeep_chrome")

    # 3. Recessed Sloped Split V-Windshield + Route Signboard (`BRGY. KALYEAH - BAYAN`)
    mb.quad((-0.86, y_cowl + 0.05, zc_top + 0.02), (0.86, y_cowl + 0.05, zc_top + 0.02), (0.82, -0.77, 1.90), (-0.82, -0.77, 1.90), "windshield_glass")
    mb.box(0.0, y_cowl + 0.01, zc_top - 0.02, zc_top + 0.05, 1.78, 0.09, "jeep_chrome")
    for px in (-0.86, 0.0, 0.86):
        mb.tube((px, y_cowl, zc_top), (px * 0.96, -0.82, 1.94), 0.036, 0.032, 5, "jeep_chrome")
    add_signboard_panel(mb, 0.0, -0.88, 1.88, 2.16, 1.62, "sign_jeep_route", facing="-Y", relief=0.08, framed=True)

    # 4. Elongated Fiesta Passenger Body (`Y = -1.12..+2.42`, `W = 1.92m`)
    hw_b = 0.96
    mb.box(0.0, 0.65, 0.52, 0.62, hw_b * 2.0, 3.54, "steel_dark", "jeep_chrome")
    for sx in (-1.0, 1.0):
        mb.box(sx * (hw_b - 0.04), -0.35, 0.56, 1.26, 0.08, 1.54, main_livery if is_prov else trim_livery)
        mb.box(sx * (hw_b - 0.04), 1.38, 0.92, 1.26, 0.08, 1.92, trim_livery if is_prov else main_livery)
        mb.box(sx * (hw_b - 0.04), 2.16, 0.56, 0.92, 0.08, 0.52, main_livery)
        mb.box(sx * (hw_b + 0.04), 1.38, 0.82, 0.92, 0.14, 0.98, "jeep_chrome")  # rear fender flare
        for py_pil in (-1.06, 0.12, 1.28, 2.36):
            mb.box(sx * (hw_b - 0.04), py_pil, 1.26, 1.92, 0.08, 0.14, "jeep_chrome")
        mb.tube((sx * (hw_b + 0.05), -0.95, 1.18), (sx * (hw_b + 0.05), 2.32, 1.18), 0.018, 0.018, 5, "jeep_chrome")
        mb.box(sx * 0.66, 0.82, 0.62, 0.86, 0.44, 2.95, "steel_dark", "vinyl_seat")

    # Driver bulkhead & steering wheel
    mb.box(-0.42, -0.78, 0.62, 0.92, 0.56, 0.44, "vinyl_seat")
    mb.tube((-0.42, -0.98, 1.02), (-0.42, -0.82, 1.26), 0.022, 0.022, 5, "steel_dark")

    # Rear Open Boarding Portal (`Y = +2.42`): side cheeks + vertical chrome grab poles
    for sx in (-1.0, 1.0):
        mb.box(sx * 0.68, 2.38, 0.56, 1.92, 0.56, 0.08, main_livery)
        mb.tube((sx * 0.38, 2.44, 0.44), (sx * 0.38, 2.44, 1.92), 0.022, 0.022, 6, "jeep_chrome")

    # 5. Cambered Pressed-Steel Jeepney Roof + Chrome Roof Cargo Rack (`Z = 1.92..2.34m`)
    mb.frustum_plinth(0.0, 0.78, 1.92, 2.02, 2.08, 1.96, 3.48, 1.72, 3.26, "jeep_chrome", roof_top_sw)
    for sx in (-0.76, 0.76):
        mb.tube((sx, -0.45, 2.14), (sx, 2.15, 2.14), 0.018, 0.018, 5, "jeep_chrome")
    for ry in (-0.45, 0.85, 2.15):
        mb.tube((-0.76, ry, 2.14), (0.76, ry, 2.14), 0.018, 0.018, 5, "jeep_chrome")

    if is_prov:
        # Provincial *Biyaheng Bayan* Top-Load Cargo: 2 woven bamboo *tiklis* market baskets + 2 rice sacks + rear ladder!
        mb.tube((-0.36, 0.25, 2.08), (-0.36, 0.25, 2.42), 0.22, 0.27, 9, "bamboo_gold")
        mb.tube((-0.36, 0.25, 2.38), (-0.36, 0.25, 2.45), 0.24, 0.16, 8, "fruit_mango_gold")
        mb.tube((0.34, 0.38, 2.08), (0.34, 0.38, 2.38), 0.20, 0.25, 9, "bamboo_gold")
        mb.tube((0.34, 0.38, 2.34), (0.34, 0.38, 2.41), 0.22, 0.15, 8, "leaf_banana_rib")
        # Bulging cream woven *sako ng bigas* rice sacks strapped on rear half of roof rack
        mb.frustum_plinth(-0.28, 1.32, 2.08, 2.22, 2.30, 0.56, 0.78, 0.44, 0.62, "cloth_sando_white", "cloth_sando_white")
        mb.frustum_plinth(0.30, 1.38, 2.08, 2.20, 2.28, 0.52, 0.74, 0.40, 0.58, "cloth_sando_white", "cloth_sando_white")
        # Rear roof-access side ladder on +X rear corner
        for lx_r in (0.62, 0.82):
            mb.tube((lx_r, 2.43, 0.62), (lx_r, 2.40, 2.12), 0.014, 0.014, 4, "steel_dark")
        for rz_step in (0.95, 1.32, 1.68):
            mb.tube((0.62, 2.42, rz_step), (0.82, 2.42, rz_step), 0.012, 0.012, 4, "steel_dark")
    return mb


# ---------------------------------------------------------------------------
# 2. Provincial Kolong-Kolong Tricycle (`brgy_tricycle.glb` ~ 1.62m x 2.38m x 1.72m)
#    Front faces `-Y` in Blender -> `+Z` in Godot!
# ---------------------------------------------------------------------------

def build_tricycle(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    mb = MeshBuilder(uvs)
    r_w = 0.29
    bx = -0.42  # Motorcycle centerline X
    sx = 0.36   # Sidecar centerline X

    # 1. 3 Spoked Wheels touching Z = 0.000 (front bike, rear bike, right sidecar) + Wheel Chock
    _add_wheel_x(mb, bx, -0.82, r_w, r_w, 0.11, "jeep_chrome")
    _add_wheel_x(mb, bx, 0.68, r_w, r_w, 0.12, "jeep_chrome")
    _add_wheel_x(mb, sx + 0.38, 0.24, r_w, r_w, 0.12, "jeep_chrome")
    mb.frustum_plinth(sx + 0.38, -0.02, 0.0, 0.06, 0.11, 0.14, 0.16, 0.12, 0.06, "wood_dark", "wood_coco")

    # 2. Motorcycle Fork, Fender, Headlamp, Handlebars + Side Mirrors, Teardrop Tank, Finned Engine & Saddle
    for fk in (-0.07, 0.07):
        mb.tube((bx + fk, -0.82, r_w), (bx + fk, -0.52, 0.98), 0.022, 0.022, 6, "jeep_chrome")
    mb.box(bx, -0.82, r_w + 0.26, r_w + 0.34, 0.16, 0.46, "trike_blue")
    mb.tube((bx, -0.60, 0.96), (bx, -0.72, 0.96), 0.10, 0.09, 8, "headlamp_amber")
    mb.tube((bx - 0.34, -0.46, 1.06), (bx + 0.34, -0.46, 1.06), 0.018, 0.018, 5, "jeep_chrome")
    for ms in (-1.0, 1.0):
        mb.tube((bx + ms * 0.28, -0.46, 1.06), (bx + ms * 0.36, -0.50, 1.24), 0.010, 0.009, 4, "jeep_chrome")
        mb.box(bx + ms * 0.38, -0.50, 1.22, 1.30, 0.09, 0.03, "jeep_chrome")

    # Teardrop fuel tank, air-cooled finned engine block, vinyl dual saddle, chrome muffler
    mb.frustum_plinth(bx, -0.18, 0.72, 0.86, 0.94, 0.26, 0.52, 0.18, 0.38, "trike_blue", "trike_blue")
    mb.box(bx, -0.14, 0.34, 0.68, 0.24, 0.36, "engine_metal")
    mb.box(bx, 0.42, 0.68, 0.82, 0.26, 0.72, "vinyl_seat")
    mb.tube((bx - 0.14, -0.10, 0.32), (bx - 0.16, 0.88, 0.38), 0.035, 0.045, 6, "jeep_chrome")

    # Steel chassis struts linking motorcycle to sidecar
    for cy_link in (-0.28, 0.52):
        mb.tube((bx, cy_link, 0.38), (sx, cy_link, 0.38), 0.026, 0.026, 5, "steel_dark")

    # 3. Provincial Galvanized *Kolong-Kolong* Sidecar (`sx = +0.36`)
    # Boat-nose tapered front cowl + galvanized side tub + passenger seat + wheel fender
    mb.frustum_plinth(sx, -0.48, 0.32, 0.62, 0.86, 0.68, 0.66, 0.62, 0.52, "trike_galv", "trike_blue")
    mb.box(sx, 0.22, 0.30, 0.42, 0.70, 1.18, "steel_dark", "trike_galv")
    mb.box(sx + 0.33, 0.22, 0.32, 0.88, 0.06, 1.16, "trike_galv")  # outer sidecar wall
    mb.box(sx, 0.68, 0.32, 0.94, 0.70, 0.10, "trike_blue")          # rear backrest wall
    mb.box(sx, 0.44, 0.42, 0.64, 0.62, 0.46, "vinyl_seat")          # passenger bench
    mb.box(sx + 0.42, 0.24, 0.56, 0.66, 0.18, 0.68, "trike_blue")   # sidecar wheel fender

    # Sloped sidecar windshield + tubular canopy frame + overhanging green GI roof + TODA 04 plate!
    mb.quad((sx - 0.31, -0.68, 0.86), (sx + 0.31, -0.68, 0.86), (sx + 0.30, -0.42, 1.48), (sx - 0.30, -0.42, 1.48), "windshield_glass")
    for px, py in ((sx - 0.31, -0.42), (sx + 0.31, -0.42), (sx - 0.32, 0.72), (sx + 0.32, 0.72)):
        mb.tube((px, py, 0.86), (px, py, 1.56), 0.018, 0.018, 5, "steel_dark")
    # Canopy roof covering both sidecar & driver shoulder!
    add_shed_yero_roof(mb, 0.02, 0.12, 1.54, 1.66, 1.32, 1.52, ov_x=0.10, ov_y=0.14, roof_swatch="yero_green", high_at_rear=False)
    add_signboard_panel(mb, sx, -0.81, 0.46, 0.80, 0.52, "plate_toda_04", facing="-Y", relief=0.03, framed=True)
    return mb


# ---------------------------------------------------------------------------
# 3. Barangay TODA Terminal Shed (`brgy_toda_shed.glb` ~ 4.8m x 3.4m x 3.45m)
# ---------------------------------------------------------------------------

def build_toda_shed(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    mb = MeshBuilder(uvs)
    w, d = 4.5, 3.0
    hy = d * 0.5

    mb.frustum_plinth(0.0, 0.0, 0.0, 0.10, 0.16, w + 0.16, d + 0.16, w, d, "concrete_moss", "concrete_cap")

    # 4 Coco-lumber corner posts + diagonal timber braces
    for sx in (-1.0, 1.0):
        px = sx * (w * 0.5 - 0.18)
        for sy in (-1.0, 1.0):
            py = sy * (hy - 0.18)
            mb.box(px, py, 0.16, 2.72, 0.14, 0.14, "wood_coco")
            mb.tube((px, py, 2.15), (px - sx * 0.52, py, 2.66), 0.032, 0.030, 4, "wood_dark")

    # Rear CHB + Wood windbreak wall, wooden waiting bench, dispatcher desk & spare tire
    mb.box(0.0, hy - 0.14, 0.16, 1.32, w - 0.32, 0.18, "chb_wall_moss", "concrete_cap")
    mb.box(-0.65, hy - 0.48, 0.16, 0.52, 2.45, 0.46, "wood_dark", "wood_coco")
    mb.box(1.35, -0.15, 0.16, 0.88, 0.92, 0.62, "wood_coco", "wood_dark")
    _add_wheel_x(mb, -w * 0.5 + 0.06, -0.25, 0.46, 0.30, 0.14, "wheel_rim")

    # Front unobstructed eave "BRGY. TODA TERMINAL" signboard + Side "NO ENTRY • BAWAL DITO" sign
    add_rooftop_eave_signboard(mb, 0.0, -hy - 0.36, 2.66, height=0.72, width=3.65, swatch="sign_toda_terminal")
    add_signboard_panel(mb, w * 0.5, 0.0, 1.45, 2.08, 1.35, "sign_no_entry", facing="+X", relief=0.05, framed=True)

    add_gable_yero_roof(
        mb, 0.0, 0.0, 2.72, w, d, rise=0.78, ov_x=0.32, ov_y=0.38,
        roof_swatch="yero_rusted", wall_swatch="wood_coco", downspout_corners=[(1, 1)],
    )
    # Proportional old tricycle tire ballast resting high on upper roof slope (`radius = 0.30m <= 0.34m`, above sign!)
    mb.tube((-1.35, -0.32, 3.28), (-1.35, -0.26, 3.44), 0.30, 0.30, 10, "tire_rubber")
    return mb


# ---------------------------------------------------------------------------
# 4. Improvised Kalye Wooden Basketball Post (`brgy_wooden_hoop.glb` ~ 1.55m x 1.75m x 3.85m)
# ---------------------------------------------------------------------------

def build_wooden_hoop(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    mb = MeshBuilder(uvs)

    # Heavy grounded concrete ballast block + old truck tire collar (`Z = 0.000`)
    mb.frustum_plinth(0.0, 0.35, 0.0, 0.28, 0.46, 0.92, 0.92, 0.68, 0.68, "chb_wall_moss", "concrete_cap")
    mb.tube((0.0, 0.35, 0.46), (0.0, 0.35, 0.66), 0.36, 0.35, 10, "tire_rubber")

    # Weathered coco-lumber main post + 2 diagonal timber base struts + forward gooseneck arm
    mb.tube((0.0, 0.35, 0.20), (0.0, 0.26, 3.38), 0.105, 0.088, 7, "wood_coco")
    for sx in (-0.34, 0.34):
        mb.tube((sx, 0.35, 0.42), (0.0, 0.32, 1.35), 0.045, 0.040, 5, "wood_dark")
    # Forward overhanging arm toward street (`-Y`) + diagonal brace
    mb.tube((0.0, 0.26, 3.24), (0.0, -0.46, 3.42), 0.075, 0.068, 6, "wood_coco")
    mb.tube((0.0, 0.28, 2.58), (0.0, -0.28, 3.34), 0.048, 0.042, 5, "wood_dark")

    # Weathered plank backboard (`1.48 x 1.02m`) with crisp red shooter's square & "BRGY. LIGA '95"!
    mb.box(0.0, -0.48, 2.86, 3.88, 1.48, 0.06, "wood_dark")
    add_signboard_panel(mb, 0.0, -0.51, 2.88, 3.86, 1.44, "backboard_liga", facing="-Y", relief=0.025, framed=True)

    # Orange steel rim (`Z = 3.02m`) + L-bracket + galvanized chain-link net
    mb.box(0.0, -0.60, 2.98, 3.05, 0.16, 0.14, "hoop_rim_orange")
    rim_y = -0.82
    for seg in range(10):
        a0 = 2.0 * math.pi * seg / 10
        a1 = 2.0 * math.pi * (seg + 1) / 10
        mb.tube(
            (0.23 * math.cos(a0), rim_y + 0.23 * math.sin(a0), 3.02),
            (0.23 * math.cos(a1), rim_y + 0.23 * math.sin(a1), 3.02),
            0.017, 0.017, 5, "hoop_rim_orange", cap_start=False, cap_end=False,
        )
    mb.tube((0.0, rim_y, 2.62), (0.0, rim_y, 3.00), 0.14, 0.22, 8, "chain_net", cap_start=False, cap_end=False)
    return mb


# ---------------------------------------------------------------------------
# 5. Mossy CHB Perimeter Wall (`brgy_chb_wall.glb` ~ 6.0m x 0.52m x 2.24m)
# ---------------------------------------------------------------------------

def build_chb_wall(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    mb = MeshBuilder(uvs)
    w = 6.0

    # Grounded mossy concrete base course (`Z = 0.000`)
    mb.frustum_plinth(0.0, 0.0, 0.0, 0.22, 0.32, w, 0.44, w - 0.04, 0.34, "chb_wall_moss", "concrete_cap")

    # 3 Molded concrete piers (`X = -2.82, 0.0, +2.82`) with beveled pyramidal pier caps
    for px in (-2.82, 0.0, 2.82):
        mb.box(px, 0.0, 0.0, 2.12, 0.36, 0.44, "concrete_cap")
        mb.frustum_plinth(px, 0.0, 2.12, 2.17, 2.24, 0.44, 0.50, 0.24, 0.28, "concrete_cap", "concrete_cap")

    # Left bay: Mossy CHB masonry (`Z = 0.32..1.72`) + BAWAL UMIHI DITO stencil + upper iron security grille!
    mb.box(-1.41, 0.0, 0.32, 1.72, 2.48, 0.24, "chb_wall_moss", "concrete_cap")
    mb.box(-1.41, 0.0, 1.72, 1.80, 2.52, 0.30, "concrete_cap")
    add_signboard_panel(mb, -1.41, -0.12, 0.62, 1.54, 2.10, "chb_bawal_stencil", facing="-Y", relief=0.03, framed=False)

    # Right bay: Patch-plastered & exposed brick/block CHB (`broken_brick_patch`) + upper iron grille!
    mb.box(1.41, 0.0, 0.32, 1.72, 2.48, 0.24, "broken_brick_patch", "concrete_cap")
    mb.box(1.41, 0.0, 1.72, 1.80, 2.52, 0.30, "concrete_cap")

    # 3D Wrought-Iron Spear Pickets & Top Rail along `Z = 1.80..2.12m` on both bays!
    for bay_cx in (-1.41, 1.41):
        mb.tube((bay_cx - 1.22, 0.0, 2.06), (bay_cx + 1.22, 0.0, 2.06), 0.018, 0.018, 5, "steel_dark")
        for pi in range(7):
            px = bay_cx - 1.05 + pi * 0.35
            mb.tube((px, 0.0, 1.80), (px, 0.0, 2.15), 0.015, 0.010, 4, "steel_dark")
    return mb


# ---------------------------------------------------------------------------
# 6. Multi-Forked Philippine Mango Tree (`brgy_mango_tree.glb` ~ 5.9m x 5.7m x 6.4m)
#    Deadlock-grade painterly multi-tone billowing leaf clouds + outward radial smooth normals!
# ---------------------------------------------------------------------------

def _add_foliage_cloud_puff(
    mb: MeshBuilder,
    center: Vector,
    subcrown_center: Vector,
    canopy_center: Vector,
    rx: float,
    ry: float,
    rz_up: float,
    rz_dn: float,
    swatch: str,
    ao_bias: float,
    seed: int,
    yaw: float = 0.0,
) -> None:
    """Sculpts a shared-vertex organic umbrella-dome tropical foliage cloud lobe whose vertex
    normals and linear `COLOR_0` AO (`0.74..1.00`) are continuous spatial functions of world `(x, y, z)`
    around `subcrown_center` & `canopy_center` — eliminating puff seams, sawtooth terminators & dark underside bands!"""
    n_az = 20
    lat_specs = [
        (-0.72, 0.46),
        (-0.38, 0.80),
        (-0.08, 0.97),
        ( 0.18, 1.00),
        ( 0.44, 0.88),
        ( 0.68, 0.66),
        ( 0.88, 0.34),
    ]
    cs_y, sn_y = math.cos(yaw), math.sin(yaw)
    bm = mb.bm
    uv_layer = mb.uv_layer
    u0, v0, u1, v1 = mb.rect_uv(swatch)
    du_sw, dv_sw = u1 - u0, v1 - v0

    def make_vert(lx: float, ly: float, lz: float) -> Tuple[Any, Tuple[float, float]]:
        wx = center.x + lx * cs_y - ly * sn_y
        wy = center.y + lx * sn_y + ly * cs_y
        wz = center.z + lz
        v = bm.verts.new((wx, wy, wz))

        # Upward-biased continuous spatial canopy normal (`normalize(radial_out + Vector((0, 0, 0.48)))`)
        # Harmonized with umbrella-dome lobe geometry so shadow maps have 0 sawtooth terminator jaggies!
        dz_s = wz - subcrown_center.z
        dz_g = wz - canopy_center.z
        s_vec = Vector((wx - subcrown_center.x, wy - subcrown_center.y, dz_s * (1.00 if dz_s >= 0.0 else 0.52)))
        g_vec = Vector((wx - canopy_center.x, wy - canopy_center.y, dz_g * (0.95 if dz_g >= 0.0 else 0.48)))
        sn = s_vec.normalized() if s_vec.length_squared > 1e-6 else Vector((0.0, 0.0, 1.0))
        gn = g_vec.normalized() if g_vec.length_squared > 1e-6 else Vector((0.0, 0.0, 1.0))
        radial_out = sn * 0.56 + gn * 0.44
        radial_out = Vector((radial_out.x, radial_out.y, max(-0.12, radial_out.z)))
        radial_out = radial_out.normalized() if radial_out.length_squared > 1e-6 else Vector((0.0, 0.0, 1.0))
        nrm = (radial_out + Vector((0.0, 0.0, 0.48))).normalized()

        # Continuous spatial canopy AO (`0.74` warm sky-bounced underside -> `1.00` sunlit crown top)
        h_glob = max(0.0, min(1.0, (wz - 3.05) / 2.85))
        h_sub = max(0.0, min(1.0, (wz - (subcrown_center.z - 0.85)) / 1.75))
        rad_out = max(0.0, min(1.0, math.hypot(wx, wy) / 2.75))
        ao_val = 0.74 + 0.26 * max(0.0, min(1.0, 0.52 * h_glob + 0.28 * h_sub + 0.20 * rad_out + ao_bias))

        v[mb.has_custom_nrm_layer] = 1
        v[mb.custom_nrm_layer] = (float(nrm.x), float(nrm.y), float(nrm.z))
        v[mb.custom_ao_layer] = float(max(0.74, min(1.0, ao_val)))

        # World-coherent continuous tri-planar/top UV inside swatch so overlapping puffs match texel scale!
        lu = 0.50 + 0.42 * math.sin(wx * 0.85 + seed * 0.17) * 0.15 + 0.36 * max(-1.0, min(1.0, lx / max(rx * 1.18, 0.25)))
        lv = 0.50 + 0.42 * math.cos(wy * 0.85 - seed * 0.13) * 0.15 + 0.36 * max(-1.0, min(1.0, (ly * 0.65 + lz * 0.55) / max(ry * 1.18, 0.25)))
        lu = max(0.03, min(0.97, lu))
        lv = max(0.03, min(0.97, lv))
        return v, (u0 + lu * du_sw, v0 + lv * dv_sw)

    rings: List[List[Tuple[Any, Tuple[float, float]]]] = []
    for r_idx, (z_f, rad_s) in enumerate(lat_specs):
        ring_v: List[Tuple[Any, Tuple[float, float]]] = []
        base_z = (z_f * rz_up) if z_f >= 0.0 else (z_f * rz_dn)
        for i in range(n_az):
            ang = (2.0 * math.pi * i) / n_az
            wob = (
                1.0
                + 0.055 * math.sin(ang * 3.0 + seed * 1.31 + r_idx * 0.28)
                + 0.030 * math.cos(ang * 5.0 - seed * 0.83 - r_idx * 0.18)
            )
            z_ruf = 0.022 * math.sin(ang * 3.0 + seed * 1.07) * rz_up * (1.0 - abs(z_f) * 0.65)
            lx = rx * rad_s * wob * math.cos(ang)
            ly = ry * rad_s * wob * math.sin(ang)
            ring_v.append(make_vert(lx, ly, base_z + z_ruf))
        rings.append(ring_v)

    pole_bot, uv_bot = make_vert(0.0, 0.0, -rz_dn)
    pole_top, uv_top = make_vert(0.02 * rx, -0.02 * ry, rz_up)

    def emit_face(verts_uvs: List[Tuple[Any, Tuple[float, float]]]) -> None:
        try:
            f = bm.faces.new([vu[0] for vu in verts_uvs])
        except ValueError:
            return
        f.smooth = True
        for lp, (_, uv_co) in zip(f.loops, verts_uvs):
            lp[uv_layer].uv = uv_co

    for r_idx in range(len(rings) - 1):
        r_a = rings[r_idx]
        r_b = rings[r_idx + 1]
        for i in range(n_az):
            j = (i + 1) % n_az
            emit_face([r_a[i], r_a[j], r_b[j], r_b[i]])

    r_top = rings[-1]
    r_bot = rings[0]
    for i in range(n_az):
        j = (i + 1) % n_az
        emit_face([r_top[i], r_top[j], (pole_top, uv_top)])
        emit_face([r_bot[j], r_bot[i], (pole_bot, uv_bot)])


def build_mango_tree(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    mb = MeshBuilder(uvs)
    canopy_center = Vector((0.0, 0.0, 4.15))

    # 1. Grounded Flared Buttress Roots (`Z = 0.000`) + Gnarled S-Curved Trunk (`bark_mango`)
    for ri in range(5):
        ra = (2.0 * math.pi * ri) / 5.0 + 0.3
        mb.tube(
            (0.68 * math.cos(ra), 0.68 * math.sin(ra), 0.0),
            (0.14 * math.cos(ra), 0.14 * math.sin(ra), 0.85),
            0.16, 0.20, 6, "bark_mango",
        )
    trunk_pts = [
        (Vector((0.0, 0.0, 0.0)), 0.42),
        (Vector((0.10, -0.06, 1.08)), 0.33),
        (Vector((-0.05, 0.08, 2.22)), 0.27),
    ]
    for (p0, r0), (p1, r1) in zip(trunk_pts[:-1], trunk_pts[1:]):
        mb.tube(p0, p1, r0, r1, 8, "bark_mango")

    # 2. Asymmetric Multi-Height Hardwood Limbs Creating Airy Deadlock Bough Separation!
    fork_origin = trunk_pts[-1][0]
    # 4 distinct asymmetric sub-crowns at varied heights & reaches + 1 high central-right apex crown
    subcrowns = [
        # (limb_mid, subcrown_center, main_rx, main_ry, main_rzu, seed_base)
        (Vector((-0.95, -0.45, 3.05)), Vector((-1.85, -0.78, 3.95)), 1.52, 1.38, 0.86, 20),
        (Vector(( 0.92, -0.62, 3.18)), Vector(( 1.78, -1.08, 4.32)), 1.56, 1.40, 0.92, 60),
        (Vector((-0.48,  0.98, 3.28)), Vector((-0.96,  1.82, 4.48)), 1.54, 1.42, 0.90, 100),
        (Vector(( 0.78,  0.76, 3.45)), Vector(( 1.48,  1.38, 4.82)), 1.58, 1.44, 0.94, 140),
        (Vector(( 0.14, -0.06, 3.85)), Vector(( 0.22, -0.10, 5.38)), 1.72, 1.58, 1.02, 180),
    ]

    for idx, (p_mid, sc_ctr, rx, ry, rzu, sbase) in enumerate(subcrowns):
        p_tip = sc_ctr - Vector((0.0, 0.0, 0.36))
        mb.tube(fork_origin, p_mid, 0.20, 0.14, 7, "bark_mango")
        mb.tube(p_mid, p_tip, 0.14, 0.08, 6, "bark_mango")

        # Secondary branch twig + 2 plump tapered golden Carabao mangoes tucked under outer boughs
        if idx < 4:
            twig = p_mid + (p_tip - p_mid) * 0.65 + Vector((0.35 * math.cos(idx * 1.7), 0.35 * math.sin(idx * 1.7), 0.55))
            mb.tube(p_mid, twig, 0.09, 0.05, 5, "bark_mango")
            for m_sgn in (-1.0, 1.0):
                mp = p_tip + Vector((0.40 * m_sgn * math.cos(idx), 0.40 * m_sgn * math.sin(idx + 0.8), -0.16))
                mb.tube(mp + Vector((0.0, 0.0, 0.22)), mp + Vector((0.0, 0.0, 0.04)), 0.011, 0.011, 4, "bark_mango", cap_start=False, cap_end=False)
                mb.tube(mp + Vector((0.0, 0.0, 0.05)), mp - Vector((0.0, 0.0, 0.04)), 0.032, 0.078, 6, "fruit_mango_gold", cap_end=False)
                mb.tube(mp - Vector((0.0, 0.0, 0.04)), mp - Vector((0.0, 0.0, 0.16)), 0.078, 0.022, 6, "fruit_mango_gold", cap_start=False)

        # Warm sky-bounced olive interior cluster nestled inside each branch fork (`0.74..0.88` linear AO, 0 dark band!)
        _add_foliage_cloud_puff(
            mb,
            sc_ctr + Vector((-0.14 * math.cos(idx), -0.14 * math.sin(idx), -0.18)),
            sc_ctr, canopy_center,
            rx * 0.68, ry * 0.66, rzu * 0.50, rzu * 0.26,
            "leaf_mango_mid", ao_bias=-0.04, seed=sbase + 1, yaw=idx * 0.9,
        )

        # Seamless 3-lobe billowing umbrella-dome outer foliage cloud (`leaf_mango_sun` + continuous spatial normals & AO!)
        puff_offsets = [
            (Vector((0.0, 0.0, 0.0)), 1.00, 1.00, 1.00, 0.34, 0.02),
            (Vector((rx * 0.44 * math.cos(idx * 1.9 + 0.4), ry * 0.44 * math.sin(idx * 1.9 + 0.4), -0.08)), 0.76, 0.74, 0.78, 0.30, 0.00),
            (Vector((rx * 0.36 * math.cos(idx * 1.9 + 2.5), ry * 0.36 * math.sin(idx * 1.9 + 2.5), 0.24)), 0.72, 0.70, 0.76, 0.28, 0.06),
        ]
        for p_idx, (off, sx, sy, szu, szd, ab) in enumerate(puff_offsets):
            _add_foliage_cloud_puff(
                mb,
                sc_ctr + off,
                sc_ctr, canopy_center,
                rx * sx, ry * sy, rzu * szu, rzu * szd,
                "leaf_mango_sun", ao_bias=ab, seed=sbase + 7 + p_idx * 11, yaw=idx * 0.7 + p_idx * 1.1,
            )

    return mb


# ---------------------------------------------------------------------------
# 7. Ringed Tropical Coconut Palm (`brgy_coconut_palm.glb` ~ 5.2m x 5.2m x 7.5m)
# ---------------------------------------------------------------------------

def build_coconut_palm(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    mb = MeshBuilder(uvs)

    # Graceful S-curved ringed coconut trunk grounded at Z = 0.000 (`9` segments up to Z = 6.15m)
    n_seg = 9
    pts: List[Tuple[Vector, float]] = []
    for i in range(n_seg + 1):
        t = i / n_seg
        cx = 0.68 * math.sin(t * math.pi * 0.88)
        cy = 0.24 * (1.0 - math.cos(t * math.pi * 0.75))
        cz = t * 6.05
        rad = 0.34 if i == 0 else (0.24 * (1.0 - 0.44 * t))
        pts.append((Vector((cx, cy, cz)), rad))

    for (p0, r0), (p1, r1) in zip(pts[:-1], pts[1:]):
        mb.tube(p0, p1, r0, r1, 8, "bark_palm_rings", cap_start=(p0.z == 0.0), cap_end=False)

    crown_base = pts[-1][0]
    crown_top = crown_base + Vector((0.04, 0.0, 0.52))
    mb.tube(crown_base, crown_top, 0.15, 0.13, 7, "pseudostem_ban")

    # Cluster of 6 green *buko* coconuts nestled beneath the crownshaft
    for bi in range(6):
        ba = (2.0 * math.pi * bi) / 6.0 + 0.2
        bp = crown_base + Vector((0.21 * math.cos(ba), 0.21 * math.sin(ba), 0.12 - 0.06 * (bi % 2)))
        mb.tube(bp + Vector((0.0, 0.0, 0.11)), bp - Vector((0.0, 0.0, 0.11)), 0.105, 0.075, 6, "fruit_buko_green")

    # 12 Arched Multi-Segment V-Folded Pinnate Coconut Fronds (`2` tiers: lower drooping + upper arching!)
    for tier, (n_fronds, pitch_start, droop, length) in enumerate([(7, 0.22, 0.92, 2.55), (5, 0.68, 0.72, 2.20)]):
        for fi in range(n_fronds):
            yaw = (2.0 * math.pi * fi) / n_fronds + tier * 0.48
            dir_xy = Vector((math.cos(yaw), math.sin(yaw), 0.0))
            side_xy = Vector((-math.sin(yaw), math.cos(yaw), 0.0))
            origin = crown_top - Vector((0.0, 0.0, 0.14 * (1 - tier)))

            seg_count = 4
            spine: List[Vector] = []
            for s in range(seg_count + 1):
                st = s / seg_count
                dist = st * length
                elev = dist * math.tan(pitch_start) - droop * (st * st) * 1.35
                spine.append(origin + dir_xy * dist + Vector((0.0, 0.0, elev)))

            for s in range(seg_count):
                st0, st1 = s / seg_count, (s + 1) / seg_count
                w0 = 0.44 * math.sin(max(0.12, st0) * math.pi * 0.92)
                w1 = 0.44 * math.sin(min(0.96, st1) * math.pi * 0.92)
                p0, p1 = spine[s], spine[s + 1]
                # Central rachis + drooping left/right V-folded pinnae leaflet wings (double-sided via top/bottom)
                l0 = p0 + side_xy * w0 - Vector((0.0, 0.0, w0 * 0.36))
                l1 = p1 + side_xy * w1 - Vector((0.0, 0.0, w1 * 0.38))
                r0 = p0 - side_xy * w0 - Vector((0.0, 0.0, w0 * 0.36))
                r1 = p1 - side_xy * w1 - Vector((0.0, 0.0, w1 * 0.38))
                mb.quad(p0, p1, l1, l0, "leaf_palm_frond", (0.5, st0, 1.0, st1))
                mb.quad(l0, l1, p1, p0, "leaf_mango_mid", (0.5, st0, 1.0, st1))
                mb.quad(r0, r1, p1, p0, "leaf_palm_frond", (0.0, st0, 0.5, st1))
                mb.quad(p0, p1, r1, r0, "leaf_mango_mid", (0.0, st0, 0.5, st1))
    return mb


# ---------------------------------------------------------------------------
# 8. Tropical Saging Banana Clump (`brgy_banana_clump.glb` ~ 3.8m x 3.8m x 4.3m)
# ---------------------------------------------------------------------------

def build_banana_clump(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    mb = MeshBuilder(uvs)

    # Grounded mossy soil root mound (`Z = 0.000`)
    mb.frustum_plinth(0.0, 0.0, 0.0, 0.14, 0.24, 1.25, 1.25, 0.82, 0.82, "chb_wall_moss", "bark_mango")

    stalks = [
        (Vector((-0.18, -0.10, 0.0)), Vector((-0.26, -0.16, 2.38)), 0.17, 0.12, 6, 1.95),
        (Vector((0.28, 0.18, 0.0)), Vector((0.38, 0.26, 1.88)), 0.14, 0.10, 5, 1.65),
        (Vector((-0.05, 0.32, 0.0)), Vector((-0.08, 0.42, 1.22)), 0.10, 0.07, 4, 1.20),
    ]
    for idx, (p_bot, p_top, r0, r1, n_leaves, l_len) in enumerate(stalks):
        mb.tube(p_bot, p_top, r0, r1, 7, "pseudostem_ban")
        for li in range(n_leaves):
            yaw = (2.0 * math.pi * li) / n_leaves + idx * 0.72
            d_xy = Vector((math.cos(yaw), math.sin(yaw), 0.0))
            s_xy = Vector((-math.sin(yaw), math.cos(yaw), 0.0))
            # Petiole stem + 3-segment broad arched V-folded banana leaf blade!
            pts_b = [
                p_top,
                p_top + d_xy * (l_len * 0.34) + Vector((0.0, 0.0, l_len * 0.36)),
                p_top + d_xy * (l_len * 0.72) + Vector((0.0, 0.0, l_len * 0.32)),
                p_top + d_xy * l_len + Vector((0.0, 0.0, -l_len * 0.08)),
            ]
            widths = [0.06, 0.34, 0.36, 0.12]
            for s in range(3):
                pa, pb = pts_b[s], pts_b[s + 1]
                wa, wb = widths[s], widths[s + 1]
                la = pa + s_xy * wa - Vector((0.0, 0.0, wa * 0.22))
                lb = pb + s_xy * wb - Vector((0.0, 0.0, wb * 0.24))
                ra = pa - s_xy * wa - Vector((0.0, 0.0, wa * 0.22))
                rb = pb - s_xy * wb - Vector((0.0, 0.0, wb * 0.24))
                mb.quad(pa, pb, lb, la, "leaf_banana_rib", (0.5, s / 3.0, 1.0, (s + 1) / 3.0))
                mb.quad(la, lb, pb, pa, "leaf_mango_mid", (0.5, s / 3.0, 1.0, (s + 1) / 3.0))
                mb.quad(ra, rb, pb, pa, "leaf_banana_rib", (0.0, s / 3.0, 0.5, (s + 1) / 3.0))
                mb.quad(pa, pb, rb, ra, "leaf_mango_mid", (0.0, s / 3.0, 0.5, (s + 1) / 3.0))

    # Hanging banana bunch + iconic crimson *puso ng saging* blossom heart on the tall mother stalk!
    bunch_top = Vector((-0.26, -0.16, 2.34)) + Vector((-0.28, -0.22, -0.12))
    mb.tube(Vector((-0.26, -0.16, 2.34)), bunch_top, 0.035, 0.030, 5, "pseudostem_ban")
    mb.tube(bunch_top, bunch_top - Vector((0.0, 0.0, 0.38)), 0.16, 0.14, 7, "fruit_mango_gold")
    mb.tube(bunch_top - Vector((0.0, 0.0, 0.40)), bunch_top - Vector((0.0, 0.0, 0.66)), 0.095, 0.015, 7, "puso_crimson")
    return mb


# ---------------------------------------------------------------------------
# 9. Sculpted 3D Sampayan Clothesline (`brgy_sampayan_3d.glb` ~ 0.84m W x 2.95m D x 2.22m H)
#    Guarantees `width X > 0.5m` and `height Y > 0.8m` for `test_model_scale.gd`!
# ---------------------------------------------------------------------------

def _add_draped_garment(
    mb: MeshBuilder,
    line_x: float,
    y_center: float,
    z_top: float,
    width_y: float,
    drop_z: float,
    swatch: str,
    kind: str = "shirt",
    sway_x: float = 0.09,
) -> None:
    """Sculpts a volumetric 3D garment with pleated cloth folds, thickness & wooden clothespins."""
    n_cols = 5
    hy = width_y * 0.5
    half_th = 0.018

    def cloth_grid(sign_th: float, y_a: float, y_b: float, z_a: float, z_b: float) -> None:
        for i in range(n_cols):
            t0, t1 = i / n_cols, (i + 1) / n_cols
            ya = y_a + t0 * (y_b - y_a)
            yb = y_a + t1 * (y_b - y_a)
            fold0 = 0.024 * math.sin(t0 * math.pi * 3.0)
            fold1 = 0.024 * math.sin(t1 * math.pi * 3.0)
            # Top row (pinned to rope) & bottom hem (swaying gently in +X/-X breeze)
            p_tl = Vector((line_x + sign_th * half_th + fold0 * 0.3, ya, z_b))
            p_tr = Vector((line_x + sign_th * half_th + fold1 * 0.3, yb, z_b))
            p_br = Vector((line_x + sign_th * half_th + sway_x + fold1, yb, z_a))
            p_bl = Vector((line_x + sign_th * half_th + sway_x + fold0, ya, z_a))
            if sign_th > 0:
                mb.quad(p_bl, p_br, p_tr, p_tl, swatch, (t0, 0.0, t1, 1.0))
            else:
                mb.quad(p_br, p_bl, p_tl, p_tr, swatch, (t0, 0.0, t1, 1.0))

    if kind == "shorts":
        # Waistband + two separate trouser legs!
        z_crotch = z_top - drop_z * 0.44
        for sgn in (-1.0, 1.0):
            cloth_grid(sgn, y_center - hy, y_center + hy, z_crotch, z_top)
            cloth_grid(sgn, y_center - hy, y_center - 0.03, z_top - drop_z, z_crotch)
            cloth_grid(sgn, y_center + 0.03, y_center + hy, z_top - drop_z, z_crotch)
    else:
        for sgn in (-1.0, 1.0):
            cloth_grid(sgn, y_center - hy, y_center + hy, z_top - drop_z, z_top)
        if kind == "shirt":
            # Draped short sleeves angling outward & downward on both shoulders!
            for s_dir in (-1.0, 1.0):
                sy_in = y_center + s_dir * hy
                sy_out = sy_in + s_dir * 0.19
                mb.box(
                    line_x + sway_x * 0.35, 0.5 * (sy_in + sy_out),
                    z_top - 0.28, z_top - 0.04, 0.045, 0.20, swatch,
                )

    # Two wooden *sipit* clothespins pinching garment onto clothesline!
    for py_pin in (y_center - hy * 0.72, y_center + hy * 0.72):
        mb.box(line_x, py_pin, z_top - 0.02, z_top + 0.065, 0.028, 0.022, "bamboo_gold")


def build_sampayan_3d(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    mb = MeshBuilder(uvs)
    y_post = 1.38

    # 1. Twin Grounded Concrete-Can Bases (`Z = 0.000`) + Bamboo T-Posts (`W = 0.84m > 0.5m`!)
    for sy in (-1.0, 1.0):
        py = sy * y_post
        mb.tube((0.0, py, 0.0), (0.0, py, 0.28), 0.17, 0.15, 8, "yero_rusted")
        mb.tube((0.0, py, 0.24), (0.0, py, 2.20), 0.048, 0.040, 6, "bamboo_gold")
        # T-crossbar spreader (`X = -0.40..+0.40`) + diagonal bamboo braces
        mb.tube((-0.40, py, 2.12), (0.40, py, 2.12), 0.032, 0.032, 6, "bamboo_gold")
        for sx in (-1.0, 1.0):
            mb.tube((0.0, py, 1.76), (sx * 0.30, py, 2.12), 0.020, 0.020, 4, "wood_coco")

    # 2. Twin Catenary Clothesline Ropes along `X = -0.28` and `X = +0.28`
    def rope_z(y_val: float, sag: float = 0.16) -> float:
        t = (y_val + y_post) / (2.0 * y_post)
        return 2.12 - sag * 4.0 * t * (1.0 - t)

    for lx, sag in ((-0.28, 0.17), (0.28, 0.14)):
        n_seg = 8
        for i in range(n_seg):
            ya = -y_post + i * (2.0 * y_post / n_seg)
            yb = -y_post + (i + 1) * (2.0 * y_post / n_seg)
            mb.tube((lx, ya, rope_z(ya, sag)), (lx, yb, rope_z(yb, sag)), 0.009, 0.009, 4, "steel_dark", cap_start=False, cap_end=False)

    # 3. 6 Sculpted Volumetric 3D Garments across the twin lines!
    _add_draped_garment(mb, -0.28, -0.86, rope_z(-0.86, 0.17), 0.54, 0.72, "cloth_polo_stripe", kind="shirt", sway_x=-0.10)
    _add_draped_garment(mb, -0.28, -0.05, rope_z(-0.05, 0.17), 0.48, 0.68, "cloth_jersey_red", kind="sando", sway_x=-0.12)
    _add_draped_garment(mb, -0.28, 0.76, rope_z(0.76, 0.17), 0.48, 0.54, "cloth_denim_blue", kind="shorts", sway_x=-0.09)

    _add_draped_garment(mb, 0.28, -0.68, rope_z(-0.68, 0.14), 0.58, 0.86, "cloth_daster_flor", kind="shirt", sway_x=0.11)
    _add_draped_garment(mb, 0.28, 0.18, rope_z(0.18, 0.14), 0.56, 0.64, "cloth_towel_teal", kind="towel", sway_x=0.08)
    _add_draped_garment(mb, 0.28, 0.88, rope_z(0.88, 0.14), 0.42, 0.58, "cloth_sando_white", kind="sando", sway_x=0.10)
    return mb


# ---------------------------------------------------------------------------
# 10. 90s Merienda Power-Up Stand (`brgy_powerup_stand.glb` ~ 1.55m x 1.15m x 2.18m)
# ---------------------------------------------------------------------------

def build_powerup_stand(
    uvs: Dict[str, Tuple[float, float, float, float]],
    variant: str = "palamig",
) -> MeshBuilder:
    mb = MeshBuilder(uvs)
    w, d = 1.46, 0.96
    is_halohalo = variant == "halohalo"

    # Grounded mossy concrete pad (`Z = 0.000`) + wooden market table legs & slatted counter
    mb.frustum_plinth(0.0, 0.0, 0.0, 0.06, 0.10, w + 0.12, d + 0.14, w + 0.04, d + 0.06, "chb_wall_moss", "concrete_cap")
    for sx in (-0.62, 0.62):
        for sy in (-0.38, 0.38):
            mb.box(sx, sy, 0.10, 0.86, 0.08, 0.08, "wood_dark")
    mb.box(0.0, 0.0, 0.84, 0.92, w, d, "bamboo_gold" if is_halohalo else "wood_coco", "wood_coco")

    # Front "MERIENDA • PALAMIG — SAGO'T GULAMAN • BANANA Q" apron signboard (`-Y`)!
    add_signboard_panel(mb, 0.0, -d * 0.5 + 0.04, 0.26, 0.84, 1.32, "sign_merienda", facing="-Y", relief=0.04, framed=True)

    if is_halohalo:
        # Barrio Halo-Halo Variant: Cast-iron ice shaver (*kaskasan ng yelo*) + ice block + 3 Halo-Halo topping jars (ube, kaong, leche flan)!
        mb.box(-0.42, -0.04, 0.92, 1.00, 0.36, 0.28, "wood_dark")
        mb.box(-0.42, -0.04, 1.00, 1.18, 0.24, 0.18, "cloth_sando_white")  # crystalline shaved-ice block
        mb.tube((-0.42, 0.08, 1.00), (-0.42, 0.04, 1.38), 0.032, 0.024, 6, "trike_blue")  # cast-iron shaver arm
        mb.tube((-0.56, 0.04, 1.34), (-0.28, 0.04, 1.34), 0.09, 0.09, 8, "jeep_chrome", cap_start=False, cap_end=False)  # hand crank wheel
        # 3 colorful Halo-Halo glass ingredient jars (ube halaya, red kaong, golden leche flan) + evaporated milk tin!
        for jx, jsw in ((-0.02, "cloth_daster_flor"), (0.24, "cloth_jersey_red"), (0.48, "fruit_mango_gold")):
            mb.tube((jx, -0.06, 0.92), (jx, -0.06, 1.18), 0.085, 0.090, 8, jsw)
            mb.tube((jx, -0.06, 1.18), (jx, -0.06, 1.22), 0.092, 0.075, 8, "jeep_chrome")
        # Woven side market basket grounded next to stand (`min_z = 0.000`)
        mb.tube((0.82, 0.12, 0.0), (0.82, 0.12, 0.34), 0.16, 0.20, 8, "bamboo_gold")
    else:
        # Two Sago't Gulaman / Buko Pandan glass/acrylic drink dispensers + lids + banana-cue bilao!
        mb.tube((-0.38, -0.05, 0.92), (-0.38, -0.05, 1.36), 0.16, 0.17, 10, "merienda_jug_org")
        mb.tube((-0.38, -0.05, 1.36), (-0.38, -0.05, 1.42), 0.18, 0.14, 10, "trike_blue")
        mb.tube((0.02, -0.05, 0.92), (0.02, -0.05, 1.30), 0.14, 0.15, 10, "fruit_buko_green")
        mb.tube((0.02, -0.05, 1.30), (0.02, -0.05, 1.35), 0.16, 0.12, 10, "hoop_rim_orange")
        mb.tube((0.44, -0.04, 0.92), (0.44, -0.04, 0.98), 0.22, 0.24, 9, "bamboo_gold")
        mb.frustum_plinth(0.44, -0.04, 0.98, 1.05, 1.10, 0.32, 0.32, 0.18, 0.18, "fruit_mango_gold", "fruit_mango_gold")

    # Asymmetric Vendor Rear (`+Y`): under-table storage shelf, styro cooler chest, 5-gal water jug & wooden stool!
    mb.box(0.0, 0.08, 0.32, 0.36, w - 0.18, d * 0.56, "wood_coco")
    mb.box(-0.34, 0.14, 0.36, 0.68, 0.46, 0.34, "cloth_sando_white", "trike_blue")
    mb.tube((0.36, 0.16, 0.36), (0.36, 0.16, 0.72), 0.13, 0.135, 8, "trike_blue")
    mb.box(0.0, 0.68, 0.42, 0.47, 0.44, 0.28, "wood_coco", "wood_dark")
    for lx in (-0.16, 0.16):
        for ly in (0.58, 0.78):
            mb.box(lx, ly, 0.0, 0.42, 0.05, 0.05, "wood_dark")

    # 4 Bamboo canopy poles + festive striped *trapal* roof (`blue-and-cream` for Halo-Halo vs `red-and-cream` for Palamig!)
    for sx in (-0.64, 0.64):
        for sy in (-0.40, 0.40):
            mb.tube((sx, sy, 0.92), (sx, sy, 1.86), 0.028, 0.025, 6, "bamboo_gold")
    canopy_sw = "cloth_polo_stripe" if is_halohalo else "trapal_canopy_str"
    add_gable_yero_roof(
        mb, 0.0, 0.0, 1.84, w, d, rise=0.34 if is_halohalo else 0.32, ov_x=0.14, ov_y=0.18,
        roof_swatch=canopy_sw, wall_swatch="bamboo_gold", downspout_corners=[],
    )
    return mb


# ---------------------------------------------------------------------------
# 11. Barangay Corner Signpost & Poster Cluster (`brgy_poster_cluster.glb` ~ 0.88m x 0.82m x 2.45m)
# ---------------------------------------------------------------------------

def build_poster_cluster(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    mb = MeshBuilder(uvs)

    # Grounded concrete wheel-rim/block base (`Z = 0.000`, `W > 0.6m` for `test_model_scale.gd`!)
    mb.frustum_plinth(0.0, 0.0, 0.0, 0.18, 0.28, 0.68, 0.68, 0.46, 0.46, "chb_wall_moss", "concrete_cap")
    mb.tube((0.0, 0.0, 0.24), (0.0, 0.0, 2.42), 0.075, 0.062, 7, "wood_coco")

    # Framed Barangay Bulletin Board (`Z = 0.92..1.76m`, `W = 0.86m`) with Halalan '95 & Malabanan posters!
    mb.box(0.0, -0.08, 0.90, 1.78, 0.86, 0.08, "wood_dark", "wood_coco")
    add_signboard_panel(mb, -0.19, -0.12, 0.98, 1.68, 0.40, "poster_halalan", facing="-Y", relief=0.025, framed=False)
    add_signboard_panel(mb, 0.21, -0.12, 0.96, 1.64, 0.38, "poster_malabanan", facing="-Y", relief=0.030, framed=False)
    # Rear notice panel so back view is detailed too!
    add_signboard_panel(mb, 0.0, -0.04, 1.02, 1.68, 0.74, "sign_toda_terminal", facing="+Y", relief=0.025, framed=True)

    # Top "NO ENTRY • BAWAL DITO" sign + angled directional street-name blade!
    add_signboard_panel(mb, 0.0, -0.07, 1.88, 2.34, 0.78, "sign_no_entry", facing="-Y", relief=0.04, framed=True)
    mb.box(0.24, 0.18, 2.12, 2.30, 0.56, 0.05, "wood_coco", yaw=0.62)
    return mb


# ---------------------------------------------------------------------------
# 12. 90s Cavite Stainless Owner-Type Jeep (`brgy_owner_jeep.glb` ~ 1.72m x 3.55m x 1.84m)
# ---------------------------------------------------------------------------

def build_owner_jeep(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    mb = MeshBuilder(uvs)
    r_w = 0.35

    # 4 Mag wheels grounded at Z = 0.000 + wooden wheel chock
    for sx in (-0.72, 0.72):
        _add_wheel_x(mb, sx, -1.08, r_w, r_w, 0.21, "wheel_rim")
        _add_wheel_x(mb, sx, 0.98, r_w, r_w, 0.22, "wheel_rim")
    mb.frustum_plinth(-0.72, -1.36, 0.0, 0.06, 0.12, 0.18, 0.18, 0.16, 0.06, "wood_dark", "wood_coco")

    # Chassis, chrome tubular front bumper, louvered stainless hood & box fenders
    mb.box(0.0, -0.04, 0.32, 0.44, 1.24, 3.05, "steel_dark", "jeep_chrome")
    mb.tube((-0.80, -1.68, 0.40), (0.80, -1.68, 0.40), 0.045, 0.045, 6, "jeep_chrome")
    mb.frustum_plinth(0.0, -1.12, 0.44, 0.94, 1.02, 1.08, 1.02, 0.98, 0.98, "jeep_chrome", "jeep_chrome")
    mb.quad((-0.52, -1.63, 0.44), (0.52, -1.63, 0.44), (0.50, -1.63, 0.98), (-0.50, -1.63, 0.98), "radiator_grille")

    for sx in (-1.0, 1.0):
        # Angular stainless front & rear fenders
        mb.box(sx * 0.68, -1.08, 0.72, 0.82, 0.32, 0.92, "jeep_chrome")
        mb.box(sx * 0.68, 0.98, 0.72, 0.82, 0.30, 0.88, "jeep_chrome")
        # Round headlamps + side mirror stalks
        mb.tube((sx * 0.40, -1.60, 0.84), (sx * 0.40, -1.70, 0.84), 0.105, 0.095, 9, "headlamp_amber")
        mb.tube((sx * 0.64, -0.62, 0.98), (sx * 0.82, -0.62, 1.32), 0.014, 0.012, 5, "jeep_chrome")
        mb.box(sx * 0.84, -0.62, 1.28, 1.40, 0.04, 0.12, "jeep_chrome")
        # Open-sided stainless tub walls
        mb.box(sx * 0.62, 0.25, 0.44, 0.94, 0.08, 1.78, "jeep_chrome", "jeep_blue")

    # Recessed fold-down windshield + tubular roll-bar cage + striped vinyl *trapal* soft top!
    mb.quad((-0.62, -0.60, 1.02), (0.62, -0.60, 1.02), (0.60, -0.38, 1.62), (-0.60, -0.38, 1.62), "windshield_glass")
    for px in (-0.63, 0.0, 0.63):
        mb.tube((px, -0.62, 1.00), (px * 0.97, -0.38, 1.64), 0.024, 0.022, 5, "jeep_chrome")
    for px in (-0.60, 0.60):
        mb.tube((px, 1.28, 0.94), (px, 1.22, 1.64), 0.024, 0.024, 5, "jeep_chrome")
    add_shed_yero_roof(mb, 0.0, 0.42, 1.62, 1.72, 1.32, 1.72, ov_x=0.06, ov_y=0.10, roof_swatch="trapal_canopy_str", high_at_rear=False)

    # Bucket seats, rear bench, rear-mounted spare tire & green steel jerrycan!
    for sx in (-0.30, 0.30):
        mb.box(sx, -0.05, 0.46, 0.72, 0.42, 0.44, "vinyl_seat")
        mb.box(sx, 0.14, 0.72, 1.06, 0.40, 0.10, "vinyl_seat")
    mb.box(0.0, 1.32, 0.44, 0.96, 1.28, 0.10, "jeep_chrome")
    mb.tube((0.28, 1.38, 0.88), (0.28, 1.56, 0.88), 0.32, 0.32, 12, "tire_rubber")
    mb.box(-0.36, 1.44, 0.56, 0.98, 0.28, 0.16, "jeep_green")
    return mb


# ---------------------------------------------------------------------------
# 13. Mossy River-Stone Creek Steps (`brgy_ilog_steps.glb` ~ 3.85m x 2.75m x 1.12m)
#     5 low stone treads (`z_top = 0.08..0.40m` matching `-0.28m` creek bed to `+0.12m` bank crest
#     with `0.00m` street cliff!) + weathered GI/bamboo riverside handrail (`H = 1.12m > 0.80m`)!
# ---------------------------------------------------------------------------

def build_ilog_steps(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    mb = MeshBuilder(uvs)
    w_tread = 2.85
    n_steps = 5
    step_d = 0.52
    step_h = 0.08  # 5 * 0.08 = 0.40m exact rise (-0.28m creek bed -> +0.10..0.12m bank crest!)
    total_d = n_steps * step_d

    # 5 Broad mossy river-stone steps descending from rear (+Y) bank crest (`0.40m`) to front (-Y / Godot +Z) water edge (`0.08m`)
    for i in range(n_steps):
        y_mid = -total_d * 0.5 + (i + 0.5) * step_d
        z_top = (i + 1) * step_h
        sw_top = "concrete_cap" if i >= 2 else "chb_wall_moss"
        mb.box(0.0, y_mid, 0.0, z_top, w_tread, step_d + 0.04, "chb_wall_moss", sw_top)

    # Low mossy river-boulder cheek curbs (`±X`, `H = 0.14..0.42m` hugging the 0.40m bank slope — zero shin-cliff!)
    for sx in (-1.0, 1.0):
        cx_w = sx * (w_tread * 0.5 + 0.24)
        for seg in range(4):
            ym = -total_d * 0.5 + (seg + 0.5) * (total_d / 4.0)
            zh = 0.14 + seg * 0.095
            mb.frustum_plinth(
                cx_w, ym, 0.0, zh * 0.72, zh,
                0.48, total_d / 4.0 + 0.05, 0.36, total_d / 4.0 + 0.01,
                "chb_wall_moss", "broken_brick_patch",
            )

    # Weathered galvanized GI pipe & bamboo riverside safety handrail along `+X` cheek curb (`H = 1.12m > 0.80m`!)
    rx = w_tread * 0.5 + 0.24
    stanchions = [
        (-total_d * 0.5 + 0.26, 0.12, 0.84),
        (0.0, 0.26, 0.98),
        (total_d * 0.5 - 0.26, 0.40, 1.12),
    ]
    for sy_p, z_base_p, z_top_p in stanchions:
        mb.tube((rx, sy_p, z_base_p), (rx, sy_p, z_top_p), 0.030, 0.026, 7, "steel_dark")
        mb.box(rx, sy_p, z_base_p - 0.01, z_base_p + 0.04, 0.11, 0.11, "concrete_cap")
    # Sloped top bamboo/GI grab rail + mid knee rail
    mb.tube((rx, stanchions[0][0] - 0.08, 0.83), (rx, stanchions[-1][0] + 0.08, 1.12), 0.032, 0.032, 8, "bamboo_gold")
    mb.tube((rx, stanchions[0][0], 0.52), (rx, stanchions[-1][0], 0.78), 0.022, 0.022, 7, "steel_dark")
    return mb


# ---------------------------------------------------------------------------
# 14. Tabing-Ilog Laundry Stones & *Batya* Set (`brgy_laundry_stones.glb` ~ 2.45m x 1.95m x 1.08m)
# ---------------------------------------------------------------------------

def build_laundry_stones(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    mb = MeshBuilder(uvs)

    # Flat wet basalt/river *pukpukan* washing boulders grounded at Z = 0.000
    boulders = [
        (-0.58, -0.22, 0.28, 0.92, 0.76, 0.12),
        (0.32, -0.28, 0.24, 0.84, 0.68, -0.18),
        (-0.10, 0.36, 0.32, 0.96, 0.78, 0.08),
        (0.76, 0.26, 0.20, 0.64, 0.54, 0.28),
    ]
    for bx, by, bh, bw, bd, byaw in boulders:
        mb.frustum_plinth(bx, by, 0.0, bh * 0.68, bh, bw, bd, bw * 0.76, bd * 0.74, "chb_wall_moss", "concrete_cap")

    # Colorful plastic *batya* washbasins + *timba* bucket + wooden *palo-palo* laundry paddle!
    mb.tube((-0.55, -0.22, 0.28), (-0.55, -0.22, 0.46), 0.26, 0.32, 10, "cloth_jersey_red")
    mb.tube((-0.55, -0.22, 0.40), (-0.55, -0.22, 0.44), 0.28, 0.28, 10, "cloth_sando_white")  # soap suds!
    mb.tube((0.34, -0.26, 0.24), (0.34, -0.26, 0.56), 0.15, 0.18, 9, "trike_blue")
    # Wooden *palo-palo* laundry beater paddle + yellow soap bar on flat stone
    mb.box(-0.12, 0.32, 0.32, 0.36, 0.14, 0.32, "wood_dark", yaw=0.35)
    mb.tube((-0.04, 0.50, 0.34), (0.04, 0.64, 0.35), 0.022, 0.020, 5, "wood_coco")
    mb.box(0.16, 0.28, 0.32, 0.36, 0.10, 0.07, "fruit_mango_gold")

    # Low riverside bamboo *salangan* drying rack (`H = 1.08m > 0.80m` scale contract!) + draped wet teal towel
    for sy in (-0.58, 0.58):
        mb.tube((-0.98, sy, 0.0), (-0.86, sy * 0.85, 1.08), 0.032, 0.026, 6, "bamboo_gold")
        mb.tube((-0.68, sy, 0.0), (-0.86, sy * 0.85, 1.08), 0.028, 0.024, 5, "bamboo_gold")
    mb.tube((-0.86, -0.58, 1.04), (-0.86, 0.58, 1.04), 0.026, 0.026, 6, "bamboo_gold")
    _add_draped_garment(mb, -0.86, 0.0, 1.04, 0.62, 0.52, "cloth_towel_teal", kind="towel", sway_x=0.06)
    return mb


# ---------------------------------------------------------------------------
# 15. Standalone 200L Blue Water Drum & Bucket (`brgy_water_drum.glb` ~ 0.94m x 0.82m x 0.96m)
# ---------------------------------------------------------------------------

def build_water_drum(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    mb = MeshBuilder(uvs)
    mb.frustum_plinth(0.0, 0.0, 0.0, 0.05, 0.08, 0.92, 0.80, 0.86, 0.74, "chb_wall_moss", "concrete_cap")
    mb.tube((-0.14, 0.0, 0.08), (-0.14, 0.0, 0.92), 0.28, 0.29, 12, "trike_blue")
    mb.tube((-0.14, 0.0, 0.92), (-0.14, 0.0, 0.96), 0.30, 0.28, 12, "steel_dark")
    # Red plastic *tabo* dipper perched on drum rim + side *timba* bucket
    mb.tube((-0.02, -0.18, 0.94), (-0.02, -0.18, 1.05), 0.075, 0.085, 8, "cloth_jersey_red")
    mb.tube((0.28, 0.06, 0.08), (0.28, 0.06, 0.42), 0.14, 0.17, 9, "hoop_rim_orange")
    return mb


# ---------------------------------------------------------------------------
# Entrypoint
# ---------------------------------------------------------------------------

def main() -> None:
    t0 = time.time()
    print("[Province90sPropsV3] Generating 2048x2048 ProvincePropsAtlas.jpg + _normal.png + _orm.png...")
    uvs = generate_province_props_atlas()

    jobs = [
        ("brgy_jeepney", lambda: build_jeepney(uvs, variant="classic")),
        ("brgy_passenger_jeepney", lambda: build_jeepney(uvs, variant="provincial")),
        ("brgy_owner_jeep", lambda: build_owner_jeep(uvs)),
        ("brgy_tricycle", lambda: build_tricycle(uvs)),
        ("brgy_toda_shed", lambda: build_toda_shed(uvs)),
        ("brgy_wooden_hoop", lambda: build_wooden_hoop(uvs)),
        ("brgy_chb_wall", lambda: build_chb_wall(uvs)),
        ("brgy_mango_tree", lambda: build_mango_tree(uvs)),
        ("brgy_coconut_palm", lambda: build_coconut_palm(uvs)),
        ("brgy_banana_clump", lambda: build_banana_clump(uvs)),
        ("brgy_sampayan_3d", lambda: build_sampayan_3d(uvs)),
        ("brgy_powerup_stand", lambda: build_powerup_stand(uvs, variant="palamig")),
        ("brgy_merienda_stand", lambda: build_powerup_stand(uvs, variant="halohalo")),
        ("brgy_poster_cluster", lambda: build_poster_cluster(uvs)),
        ("brgy_ilog_steps", lambda: build_ilog_steps(uvs)),
        ("brgy_laundry_stones", lambda: build_laundry_stones(uvs)),
        ("brgy_water_drum", lambda: build_water_drum(uvs)),
    ]

    for name, fn in jobs:
        mb = fn()
        out_path = OUT_DIR / f"{name}.glb"
        st = finalize_and_export_glb(
            mb,
            out_path,
            albedo_filename=ALBEDO_FILE,
            normal_filename=NORMAL_FILE,
            orm_filename=ORM_FILE,
            ground_at_zero=True,
            center_xy=True,
        )
        print(
            f"  [OK] {name}.glb | tris={st['tris']:4d} | "
            f"size=({st['w']:.2f} x {st['d']:.2f} x {st['h']:.2f}m) | min_y={st['min_y']:.4f}"
        )

    print(f"[Province90sPropsV3] Built {len(jobs)} models + normal+ORM atlas in {time.time() - t0:.2f}s.")


if __name__ == "__main__":
    main()
