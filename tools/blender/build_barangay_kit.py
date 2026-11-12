"""Deadlock-Quality V3 Barangay Architectural & Street Landmark Kit Generator (Blender 5.2+).

Generates `BarangayAtlas.jpg` + `BarangayAtlas_normal.png` (2048x2048) and 17 single-mesh,
single-surface, BVH-AO-baked, normal-mapped `.glb` assets in `assets/models/barangay/`:
  9 Asymmetric Front/Side/Rear Architectural Buildings:
    - brgy_sari_sari, brgy_shophouse_row, brgy_tabi_cluster_a, brgy_tabi_cluster_b,
      brgy_house_two_storey, brgy_hall_plaza, brgy_covered_court, brgy_waiting_shed,
      brgy_palengke_stall
  8 Street, Canal & Base Utility Assets:
    - brgy_footbridge, brgy_sorbetes_cart, brgy_meralco_post, brgy_post_blue,
      brgy_post_red, brgy_cover_wall, brgy_poso_well, brgy_banderitas

Coordinate convention (Blender Z-up -> Godot Y-up via `export_yup=True`):
  - Blender `-Y` -> Godot `+Z` = FRONT FACADE (street-facing: recessed storefronts, awnings, murals)
  - Blender `+Y` -> Godot `-Z` = REAR FACADE (dirty kitchen, lababo sink, blue drum, back door)
  - Blender `-X / +X` = SIDE FIREWALLS (raw/patched CHB, Meralco meter + PVC weatherhead riser, bath vents)
  - Blender `Z = 0.000` -> Godot `Y = 0.000` = Ground Plinth Contact
"""

from __future__ import annotations

import math
import sys
import time
from pathlib import Path
from typing import Dict, List, Sequence, Tuple

from mathutils import Vector

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from barangay_v3_arch import (  # noqa: E402
    MeshBuilder,
    Opening,
    add_firewall_parapet,
    add_gable_yero_roof,
    add_meralco_side_meter_and_weatherhead,
    add_pvc_downspout,
    add_rear_dirty_kitchen,
    add_recessed_bay_wall,
    add_rooftop_eave_signboard,
    add_shed_yero_roof,
    add_signboard_panel,
    add_storefront_awning,
    finalize_and_export_glb,
)
from barangay_v3_textures import OUT_DIR, generate_barangay_atlas  # noqa: E402

ALBEDO_FILE = "BarangayAtlas.jpg"
NORMAL_FILE = "BarangayAtlas_normal.png"
ORM_FILE = "BarangayAtlas_orm.png"


def _add_four_walls(
    mb: MeshBuilder,
    cx: float,
    cy: float,
    w: float,
    d: float,
    z0: float,
    z1: float,
    sw_front: str,
    sw_right: str,
    sw_rear: str,
    sw_left: str,
    ops_front: Sequence[Opening] = (),
    ops_right: Sequence[Opening] = (),
    ops_rear: Sequence[Opening] = (),
    ops_left: Sequence[Opening] = (),
    pilasters: bool = True,
) -> None:
    """Constructs 4 non-overlapping CCW exterior walls around `(cx, cy)` (`w x d`)."""
    hx, hy = w * 0.5, d * 0.5
    p_fl = (cx - hx, cy - hy)
    p_fr = (cx + hx, cy - hy)
    p_br = (cx + hx, cy + hy)
    p_bl = (cx - hx, cy + hy)

    add_recessed_bay_wall(mb, p_fl, p_fr, z0, z1, sw_front, ops_front, pilaster_ends=pilasters)
    add_recessed_bay_wall(mb, p_fr, p_br, z0, z1, sw_right, ops_right, pilaster_ends=pilasters)
    add_recessed_bay_wall(mb, p_br, p_bl, z0, z1, sw_rear, ops_rear, pilaster_ends=pilasters)
    add_recessed_bay_wall(mb, p_bl, p_fl, z0, z1, sw_left, ops_left, pilaster_ends=pilasters)


# ---------------------------------------------------------------------------
# 1. Nena's Sari-Sari Store (`brgy_sari_sari.glb` ~ 7.8m x 6.8m x 4.9m)
# ---------------------------------------------------------------------------

def build_sari_sari(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    mb = MeshBuilder(uvs)
    w, d = 7.4, 5.6
    z_p, z_eave = 0.28, 3.38

    # Grounded beveled concrete plinth
    mb.frustum_plinth(0.0, 0.0, 0.0, 0.19, z_p, w + 0.28, d + 0.28, w + 0.06, d + 0.06)

    # Asymmetric Front (-Y), Side Firewalls (-X/+X), and Rear (+Y)
    ops_front = [
        Opening(0.65, 1.75, z_p, 2.42, 0.22, "door_front"),
        Opening(2.35, 6.75, z_p, 2.68, 0.68, "store_sari"),
    ]
    ops_right = [
        Opening(1.4, 2.05, 2.35, 2.75, 0.15, "bath_vent"),
        Opening(3.4, 4.55, 1.15, 2.25, 0.18, "jalousie_rehas"),
    ]
    ops_left = [
        Opening(2.1, 2.75, 2.40, 2.78, 0.15, "bath_vent"),
    ]
    ops_rear = [
        Opening(1.15, 2.10, z_p, 2.32, 0.18, "door_rear"),
        Opening(3.60, 5.15, 1.18, 2.26, 0.18, "jalousie"),
    ]
    _add_four_walls(
        mb, 0.0, 0.0, w, d, z_p, z_eave,
        sw_front="chb_cream_moss",
        sw_right="chb_plaster_cracked",
        sw_rear="chb_salmon_moss",
        sw_left="chb_raw_moss",
        ops_front=ops_front,
        ops_right=ops_right,
        ops_rear=ops_rear,
        ops_left=ops_left,
    )

    # Beveled eaves cornice band
    mb.cornice_ring(0.0, 0.0, z_eave - 0.16, 0.18, w, d, proj=0.12)

    # Front awning + iconic front-eave rooftop "TINDAHAN NI ALING NENA" signboard + customer wooden bench
    add_storefront_awning(mb, 0.85, -d * 0.5, 2.74, 4.75, proj=1.15, drop=0.38, swatch="trapal_striped")
    add_rooftop_eave_signboard(mb, 0.85, -d * 0.5 - 0.52, z_eave - 0.08, height=0.82, width=4.05, swatch="sign_sari_sari")
    # Wooden tambay bench under serving counter (`X = +0.35..+1.75`, leaving front door `X = -3.05..-1.95` 100% clear!)
    mb.box(0.47, -d * 0.5 - 0.36, 0.0, 0.44, 0.11, 0.36, "wood_dark")
    mb.box(1.63, -d * 0.5 - 0.36, 0.0, 0.44, 0.11, 0.36, "wood_dark")
    mb.box(1.05, -d * 0.5 - 0.36, 0.16, 0.22, 1.14, 0.06, "wood_dark")
    mb.box(1.05, -d * 0.5 - 0.36, 0.44, 0.50, 1.40, 0.40, "narra_warm")

    # Overhanging rusted corrugated GI gable roof (flush against -X firewall!) + purlins + gutters + downspouts
    add_gable_yero_roof(
        mb, 0.0, 0.0, z_eave, w, d, rise=1.35,
        ov_x=0.42, ov_y=0.54, roof_swatch="yero_red_oxide", wall_swatch="chb_cream_moss",
        downspout_corners=[(1, -1), (1, 1)], ov_x_left=0.0,
    )
    # Left stepped CHB firewall parapet & Right Meralco meter + PVC weatherhead conduit!
    add_firewall_parapet(mb, -w * 0.5 - 0.08, 0.0, d, z_eave, z_eave + 1.35, "chb_raw_moss")
    add_meralco_side_meter_and_weatherhead(mb, w * 0.5, -1.15, z_eave, is_right_side=True)

    # Asymmetric Rear: lean-to dirty kitchen centered under kitchen jalousie (`X = -2.50..+0.60`), leaving rear door (`X = +1.60..+2.55`) 100% clear!
    add_rear_dirty_kitchen(mb, -0.95, d * 0.5, width=3.1, depth=1.05, z_attach=2.62)
    return mb


# ---------------------------------------------------------------------------
# 2. 3-Bay Commercial Shophouse Row (`brgy_shophouse_row.glb` ~ 15.2m x 7.6m x 7.3m)
#    Bay 1: Karinderya | Bay 2: Gupitan / Barbershop | Bay 3: Vulcanizing & Hangin
# ---------------------------------------------------------------------------

def build_shophouse_row(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    mb = MeshBuilder(uvs)
    hy = 3.25  # core depth 6.5m (+ front awnings & rear dirty kitchen -> ~7.8m total)
    d = hy * 2.0
    z_p = 0.28

    # --- BAY 1 (Left: X = -7.3 .. -2.3, W = 5.0m): 2-Storey Karinderya ---
    cx1, w1, z1_mid, z1_top = -4.8, 5.0, 3.45, 6.25
    mb.frustum_plinth(cx1, 0.0, 0.0, 0.18, z_p, w1 + 0.16, d + 0.24, w1 + 0.04, d + 0.04)
    _add_four_walls(
        mb, cx1, 0.0, w1, d, z_p, z1_mid,
        sw_front="chb_ochre_moss", sw_right="chb_raw_moss", sw_rear="chb_cream_moss", sw_left="chb_raw_moss",
        ops_front=[Opening(0.45, 4.55, z_p, 2.68, 0.74, "store_karinderya")],
        ops_left=[Opening(1.6, 2.25, 2.35, 2.75, 0.15, "bath_vent")],
        ops_rear=[Opening(0.8, 1.75, z_p, 2.30, 0.18, "door_rear"), Opening(2.6, 4.1, 1.15, 2.2, 0.18, "jalousie")],
    )
    mb.cornice_ring(cx1, 0.0, z1_mid - 0.12, 0.22, w1, d, proj=0.14)
    _add_four_walls(
        mb, cx1, 0.0, w1, d, z1_mid + 0.10, z1_top,
        sw_front="chb_ochre_moss", sw_right="chb_raw_moss", sw_rear="chb_cream_moss", sw_left="chb_raw_moss",
        ops_front=[
            Opening(0.65, 2.15, z1_mid + 0.75, z1_top - 0.55, 0.20, "capiz_rehas"),
            Opening(2.85, 4.35, z1_mid + 0.75, z1_top - 0.55, 0.20, "capiz_rehas"),
        ],
        ops_left=[Opening(3.8, 4.45, z1_mid + 1.55, z1_mid + 1.95, 0.15, "bath_vent")],
        ops_rear=[Opening(1.6, 3.4, z1_mid + 0.85, z1_top - 0.65, 0.18, "jalousie")],
    )
    mb.cornice_ring(cx1, 0.0, z1_top - 0.16, 0.18, w1, d, proj=0.14)
    add_storefront_awning(mb, cx1, -hy, 2.62, 4.5, proj=1.05, drop=0.34, swatch="trapal_orange")
    add_signboard_panel(mb, cx1, -hy, 2.66, 3.28, 4.15, "sign_karinderya", facing="-Y", relief=0.06, framed=False)
    add_gable_yero_roof(
        mb, cx1, 0.0, z1_top, w1, d, rise=1.15, ov_x=0.20, ov_y=0.52,
        roof_swatch="yero_rust_heavy", wall_swatch="chb_ochre_moss", downspout_corners=[(1, -1)], ov_x_left=0.0,
    )
    add_firewall_parapet(mb, cx1 - w1 * 0.5 - 0.08, 0.0, d, z1_top, z1_top + 1.15, "chb_raw_moss")

    # --- BAY 2 (Center: X = -2.3 .. +2.5, W = 4.8m): Taller 2-Storey Gupitan / Barbershop ---
    cx2, w2, z2_mid, z2_top = 0.1, 4.8, 3.55, 6.65
    mb.frustum_plinth(cx2, 0.0, 0.0, 0.18, z_p, w2 + 0.12, d + 0.28, w2 + 0.02, d + 0.06)
    _add_four_walls(
        mb, cx2, 0.0, w2, d, z_p, z2_mid,
        sw_front="stucco_sandstone", sw_right="chb_raw_moss", sw_rear="chb_plaster_cracked", sw_left="chb_raw_moss",
        ops_front=[Opening(0.42, 4.25, z_p, 2.64, 0.62, "store_barber")],
        ops_rear=[Opening(1.8, 3.2, 1.20, 2.25, 0.18, "jalousie")],
    )
    # 2nd floor cantilevered balcony slab + concrete corbel brackets & wrought-iron railing on Front (-Y)
    mb.box(cx2, -hy - 0.34, z2_mid - 0.08, z2_mid + 0.08, w2 - 0.16, 0.72, "concrete_pillar")
    for c_off in (-1.75, 0.0, 1.75):
        mb.tube((cx2 + c_off, -hy + 0.02, z2_mid - 0.42), (cx2 + c_off, -hy - 0.58, z2_mid - 0.06), 0.055, 0.045, 4, "concrete_pillar")
    mb.box(cx2, -hy - 0.66, z2_mid + 0.08, z2_mid + 0.96, w2 - 0.24, 0.06, "sari_grill_counter")
    mb.box(cx2, -hy - 0.66, z2_mid + 0.94, z2_mid + 1.02, w2 - 0.20, 0.10, "wood_dark")
    _add_four_walls(
        mb, cx2, 0.0, w2, d, z2_mid + 0.08, z2_top,
        sw_front="chb_sky_moss", sw_right="chb_raw_moss", sw_rear="chb_plaster_cracked", sw_left="chb_raw_moss",
        ops_front=[
            Opening(0.65, 1.75, z2_mid + 0.10, z2_top - 0.68, 0.20, "door_front"),
            Opening(2.25, 4.15, z2_mid + 0.80, z2_top - 0.62, 0.20, "capiz"),
        ],
        ops_rear=[Opening(1.4, 3.4, z2_mid + 0.88, z2_top - 0.65, 0.18, "jalousie")],
    )
    mb.cornice_ring(cx2, 0.0, z2_top - 0.16, 0.18, w2, d, proj=0.15)
    add_signboard_panel(mb, cx2, -hy, 2.66, 3.30, 4.05, "sign_barberya", facing="-Y", relief=0.06, framed=False)
    add_gable_yero_roof(
        mb, cx2, 0.0, z2_top, w2, d, rise=1.22, ov_x=0.22, ov_y=0.56,
        roof_swatch="yero_zinc_weathered", wall_swatch="chb_sky_moss", downspout_corners=[],
    )

    # --- BAY 3 (Right: X = +2.5 .. +7.4, W = 4.9m): Vulcanizing & Hangin Talyer ---
    cx3, w3, z3_top = 4.95, 4.9, 4.65
    mb.frustum_plinth(cx3, 0.0, 0.0, 0.16, z_p, w3 + 0.16, d + 0.22, w3 + 0.04, d + 0.04)
    _add_four_walls(
        mb, cx3, 0.0, w3, d, z_p, z3_top,
        sw_front="chb_plaster_cracked", sw_right="chb_raw_moss", sw_rear="chb_raw_moss", sw_left="chb_raw_moss",
        ops_front=[
            Opening(0.45, 4.45, z_p, 2.88, 0.85, "store_vulcanizing"),
            Opening(1.10, 3.80, 3.65, 4.22, 0.16, "bath_vent"),
        ],
        ops_right=[
            Opening(1.4, 2.1, 2.55, 2.95, 0.15, "bath_vent"),
            Opening(3.6, 4.8, 1.35, 2.35, 0.18, "jalousie_rehas"),
        ],
        ops_rear=[Opening(1.5, 2.5, z_p, 2.30, 0.18, "door_rear")],
    )
    mb.cornice_ring(cx3, 0.0, z3_top - 0.16, 0.18, w3, d, proj=0.13)
    add_storefront_awning(mb, cx3, -hy, 2.92, 4.4, proj=0.95, drop=0.30, swatch="yero_patchwork")
    add_signboard_panel(mb, cx3, -hy, 2.98, 3.74, 4.15, "sign_talyer", facing="-Y", relief=0.08, framed=True)
    # Right side wall: BAWAL UMIHI DITO painted stencil + Meralco meter & PVC weatherhead riser!
    add_signboard_panel(mb, cx3 + w3 * 0.5, -1.35, 1.05, 1.95, 2.4, "chb_bawal_umihi", facing="+X", relief=0.035, framed=False)
    add_meralco_side_meter_and_weatherhead(mb, cx3 + w3 * 0.5, 1.45, z3_top, is_right_side=True)
    add_shed_yero_roof(mb, cx3, 0.0, z3_top - 0.10, z3_top + 0.65, w3, d, ov_x=0.36, ov_y=0.46, roof_swatch="yero_patchwork", high_at_rear=False)
    add_pvc_downspout(mb, (cx3 + w3 * 0.5, hy + 0.35, z3_top - 0.12), (cx3 + w3 * 0.5 - 0.18, hy + 0.05), (0.0, 1.0), 0.004)

    # Shared Rear Dirty-Kitchen Yard behind Bay 2 kitchen jalousie (clear of Bay 1 & Bay 3 rear doors!)
    add_rear_dirty_kitchen(mb, 0.10, hy, width=3.8, depth=1.08, z_attach=2.68)
    return mb


# ---------------------------------------------------------------------------
# 3. Tabi-Tabing Bahay Cluster A (`brgy_tabi_cluster_a.glb` ~ 16.4m x 7.6m x 7.2m)
# ---------------------------------------------------------------------------

def build_tabi_cluster_a(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    mb = MeshBuilder(uvs)
    d = 6.4
    hy = d * 0.5
    z_p = 0.28

    # Unit 1 (Left, X = -7.8..-2.2, W = 5.6m): Bahay-na-Bato Ancestral Home (stone base + overhanging wood 2F)
    cx1, w1, z1_mid, z1_top = -5.0, 5.6, 3.25, 6.20
    mb.frustum_plinth(cx1, 0.0, 0.0, 0.18, z_p, w1 + 0.20, d + 0.24, w1 + 0.04, d + 0.04)
    _add_four_walls(
        mb, cx1, 0.0, w1, d, z_p, z1_mid,
        sw_front="adobe_stone", sw_right="chb_raw_moss", sw_rear="adobe_stone", sw_left="chb_raw_moss",
        ops_front=[
            Opening(0.55, 3.35, z_p, 2.65, 0.72, "silong_open", swatch="chb_cream_moss"),
            Opening(3.75, 5.05, 1.05, 2.25, 0.20, "capiz_rehas"),
        ],
        ops_left=[Opening(2.2, 2.85, 2.35, 2.72, 0.15, "bath_vent")],
        ops_rear=[Opening(1.2, 2.15, z_p, 2.28, 0.18, "door_rear")],
    )
    # Overhanging 2nd floor (+0.42m forward over street, supported by timber floor joists!)
    w1_u, d1_u, cy1_u = w1 + 0.12, d + 0.44, -0.22
    mb.box(cx1, cy1_u, z1_mid - 0.06, z1_mid + 0.12, w1_u + 0.08, d1_u + 0.08, "wood_dark")
    for jx in range(5):
        bx = cx1 - w1 * 0.42 + jx * (w1 * 0.84 / 4.0)
        mb.tube((bx, -hy + 0.05, z1_mid - 0.36), (bx, -hy - 0.40, z1_mid - 0.04), 0.04, 0.035, 4, "wood_dark")
    _add_four_walls(
        mb, cx1, cy1_u, w1_u, d1_u, z1_mid + 0.12, z1_top,
        sw_front="planks_vertical", sw_right="narra_warm", sw_rear="narra_warm", sw_left="planks_vertical",
        ops_front=[
            Opening(0.55, 2.55, z1_mid + 0.72, z1_top - 0.48, 0.18, "capiz"),
            Opening(3.15, 5.15, z1_mid + 0.72, z1_top - 0.48, 0.18, "capiz"),
        ],
        ops_left=[Opening(2.2, 3.8, z1_mid + 0.85, z1_top - 0.55, 0.16, "capiz")],
        ops_rear=[Opening(1.8, 3.8, z1_mid + 0.85, z1_top - 0.55, 0.16, "jalousie")],
        pilasters=False,
    )
    add_gable_yero_roof(
        mb, cx1, cy1_u, z1_top, w1_u, d1_u, rise=1.35, ov_x=0.38, ov_y=0.52,
        roof_swatch="yero_red_oxide", wall_swatch="planks_vertical", downspout_corners=[(-1, -1)],
    )
    add_meralco_side_meter_and_weatherhead(mb, cx1 - w1 * 0.5, 0.8, z1_top, is_right_side=False)

    # Unit 2 (Center, X = -2.2..+3.1, W = 5.3m): 2-Storey Salmon Concrete Townhouse
    cx2, w2, z2_mid, z2_top = 0.45, 5.3, 3.35, 6.05
    mb.frustum_plinth(cx2, 0.12, 0.0, 0.18, z_p, w2 + 0.12, d + 0.20, w2 + 0.02, d + 0.02)
    _add_four_walls(
        mb, cx2, 0.12, w2, d, z_p, z2_mid,
        sw_front="chb_salmon_moss", sw_right="chb_raw_moss", sw_rear="chb_salmon_moss", sw_left="chb_raw_moss",
        ops_front=[
            Opening(0.55, 1.60, z_p, 2.40, 0.22, "door_front"),
            Opening(2.15, 4.65, 1.00, 2.30, 0.22, "jalousie_rehas"),
        ],
        ops_rear=[Opening(1.8, 3.5, 1.15, 2.25, 0.18, "jalousie")],
    )
    mb.cornice_ring(cx2, 0.12, z2_mid - 0.12, 0.20, w2, d, proj=0.14)
    _add_four_walls(
        mb, cx2, 0.12, w2, d, z2_mid + 0.08, z2_top,
        sw_front="chb_salmon_moss", sw_right="chb_raw_moss", sw_rear="chb_salmon_moss", sw_left="chb_raw_moss",
        ops_front=[
            Opening(0.65, 2.35, z2_mid + 0.72, z2_top - 0.50, 0.20, "jalousie"),
            Opening(2.95, 4.65, z2_mid + 0.72, z2_top - 0.50, 0.20, "jalousie"),
        ],
        ops_rear=[Opening(1.6, 3.6, z2_mid + 0.80, z2_top - 0.55, 0.18, "jalousie")],
    )
    mb.cornice_ring(cx2, 0.12, z2_top - 0.15, 0.17, w2, d, proj=0.13)
    add_storefront_awning(mb, cx2 + 0.6, -hy + 0.12, 2.55, 3.2, proj=0.85, drop=0.28, swatch="trapal_blue")
    add_gable_yero_roof(
        mb, cx2, 0.12, z2_top, w2, d, rise=1.08, ov_x=0.20, ov_y=0.50,
        roof_swatch="yero_patchwork", wall_swatch="chb_salmon_moss", downspout_corners=[],
    )

    # Unit 3 (Right, X = +3.1..+8.0, W = 4.9m): Mint Corner Sari-Sari + Attic Loft
    cx3, w3, z3_top = 5.55, 4.9, 4.35
    mb.frustum_plinth(cx3, -0.10, 0.0, 0.18, z_p, w3 + 0.18, d + 0.22, w3 + 0.04, d + 0.04)
    _add_four_walls(
        mb, cx3, -0.10, w3, d, z_p, z3_top,
        sw_front="chb_mint_moss", sw_right="chb_plaster_cracked", sw_rear="chb_mint_moss", sw_left="chb_raw_moss",
        ops_front=[Opening(0.45, 4.35, z_p, 2.65, 0.65, "store_sari")],
        ops_right=[
            Opening(1.2, 1.85, 2.65, 3.05, 0.15, "bath_vent"),
            Opening(3.2, 4.6, 1.15, 2.25, 0.18, "capiz_rehas"),
        ],
        ops_rear=[Opening(1.1, 2.05, z_p, 2.28, 0.18, "door_rear")],
    )
    mb.cornice_ring(cx3, -0.10, z3_top - 0.15, 0.17, w3, d, proj=0.12)
    add_storefront_awning(mb, cx3, -hy - 0.10, 2.72, 4.3, proj=1.05, drop=0.34, swatch="trapal_striped")
    add_signboard_panel(mb, cx3, -hy - 0.10, 2.84, 3.68, 3.9, "sign_sari_sari", facing="-Y", relief=0.07, framed=True)
    add_gable_yero_roof(
        mb, cx3, -0.10, z3_top, w3, d, rise=1.05, ov_x=0.38, ov_y=0.48,
        roof_swatch="yero_green_faded", wall_swatch="chb_mint_moss", downspout_corners=[(-1, 1)], ov_x_right=0.0,
    )
    add_firewall_parapet(mb, cx3 + w3 * 0.5 + 0.08, -0.10, d, z3_top, z3_top + 1.05, "chb_raw_moss")
    add_meralco_side_meter_and_weatherhead(mb, cx3 + w3 * 0.5, -1.4, z3_top, is_right_side=True)

    add_rear_dirty_kitchen(mb, 0.45, hy + 0.12, width=3.8, depth=1.05, z_attach=2.62)
    return mb


# ---------------------------------------------------------------------------
# 4. Tabi-Tabing Bahay Cluster B (`brgy_tabi_cluster_b.glb` ~ 14.4m x 7.8m x 8.0m)
# ---------------------------------------------------------------------------

def build_tabi_cluster_b(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    mb = MeshBuilder(uvs)
    d = 6.6
    hy = d * 0.5
    z_p = 0.30

    # Unit 1 (Left, X = -7.0..+0.4, W = 7.4m): Tall 2.5-Storey Street-Gable Corner House + Colonnade
    cx1, w1, z1_mid, z1_top = -3.3, 7.4, 3.48, 6.45
    mb.frustum_plinth(cx1, 0.0, 0.0, 0.20, z_p, w1 + 0.22, d + 0.26, w1 + 0.04, d + 0.04)
    _add_four_walls(
        mb, cx1, 0.0, w1, d, z_p, z1_mid,
        sw_front="stucco_sandstone", sw_right="chb_raw_moss", sw_rear="chb_cream_moss", sw_left="chb_plaster_cracked",
        ops_front=[
            Opening(0.55, 4.55, z_p, 2.72, 0.76, "store_karinderya"),
            Opening(5.05, 6.75, z_p, 2.65, 0.65, "silong_open", swatch="chb_cream_moss"),
        ],
        ops_left=[
            Opening(1.4, 2.1, 2.45, 2.85, 0.15, "bath_vent"),
            Opening(3.5, 5.1, 1.15, 2.30, 0.20, "capiz_rehas"),
        ],
        ops_rear=[Opening(1.2, 2.2, z_p, 2.32, 0.18, "door_rear"), Opening(3.8, 5.8, 1.18, 2.25, 0.18, "jalousie")],
    )
    mb.cornice_ring(cx1, 0.0, z1_mid - 0.12, 0.22, w1, d, proj=0.16)
    _add_four_walls(
        mb, cx1, 0.0, w1, d, z1_mid + 0.10, z1_top,
        sw_front="chb_ochre_moss", sw_right="chb_raw_moss", sw_rear="chb_cream_moss", sw_left="chb_plaster_cracked",
        ops_front=[
            Opening(0.65, 2.35, z1_mid + 0.72, z1_top - 0.52, 0.22, "capiz_rehas"),
            Opening(2.85, 4.55, z1_mid + 0.72, z1_top - 0.52, 0.22, "capiz_rehas"),
            Opening(5.05, 6.75, z1_mid + 0.72, z1_top - 0.52, 0.22, "capiz_rehas"),
        ],
        ops_left=[Opening(2.2, 4.4, z1_mid + 0.80, z1_top - 0.55, 0.18, "capiz")],
        ops_rear=[Opening(2.0, 5.2, z1_mid + 0.85, z1_top - 0.60, 0.18, "jalousie")],
    )
    mb.cornice_ring(cx1, 0.0, z1_top - 0.16, 0.18, w1, d, proj=0.15)
    add_signboard_panel(mb, cx1 - 0.8, -hy, 2.66, 3.30, 4.15, "sign_karinderya", facing="-Y", relief=0.06, framed=False)
    add_storefront_awning(mb, cx1 - 0.8, -hy, 2.62, 4.3, proj=1.08, drop=0.35, swatch="trapal_orange")
    # Street-facing Deadlock gable pediment (`ridge_along_x=False`)!
    add_gable_yero_roof(
        mb, cx1, 0.0, z1_top, w1, d, rise=1.48, ov_x=0.45, ov_y=0.55,
        roof_swatch="yero_red_oxide", wall_swatch="chb_ochre_moss", ridge_along_x=False,
        downspout_corners=[(-1, -1)],
    )
    add_meralco_side_meter_and_weatherhead(mb, cx1 - w1 * 0.5, -1.6, z1_top, is_right_side=False)

    # Unit 2 (Right, X = +0.4..+6.8, W = 6.4m): 2-Storey Panaderya + Hardwood/Sawali Upper Floor
    cx2, w2, z2_mid, z2_top = 3.6, 6.4, 3.38, 6.02
    mb.frustum_plinth(cx2, 0.10, 0.0, 0.18, z_p, w2 + 0.16, d + 0.22, w2 + 0.04, d + 0.04)
    _add_four_walls(
        mb, cx2, 0.10, w2, d, z_p, z2_mid,
        sw_front="chb_sky_moss", sw_right="chb_raw_moss", sw_rear="chb_raw_moss", sw_left="chb_raw_moss",
        ops_front=[
            Opening(0.55, 4.25, z_p, 2.60, 0.64, "store_panaderya"),
            Opening(4.65, 5.85, z_p, 2.38, 0.22, "door_front"),
        ],
        ops_right=[Opening(2.0, 2.65, 2.40, 2.78, 0.15, "bath_vent")],
        ops_rear=[Opening(1.4, 2.35, z_p, 2.28, 0.18, "door_rear")],
    )
    mb.box(cx2, -0.10, z2_mid - 0.06, z2_mid + 0.10, w2 + 0.16, d + 0.44, "wood_dark")
    # Timber corbel brackets supporting overhanging sawali 2nd storey
    for jx in range(5):
        bx = cx2 - w2 * 0.40 + jx * (w2 * 0.80 / 4.0)
        mb.tube((bx, -hy + 0.14, z2_mid - 0.36), (bx, -hy - 0.28, z2_mid - 0.04), 0.040, 0.034, 4, "wood_dark")
    _add_four_walls(
        mb, cx2, -0.10, w2, d + 0.36, z2_mid + 0.10, z2_top,
        sw_front="sawali_honey", sw_right="chb_raw_moss", sw_rear="sawali_aged", sw_left="narra_warm",
        ops_front=[
            Opening(0.65, 2.75, z2_mid + 0.68, z2_top - 0.46, 0.18, "capiz"),
            Opening(3.55, 5.65, z2_mid + 0.68, z2_top - 0.46, 0.18, "capiz"),
        ],
        ops_rear=[Opening(1.8, 4.4, z2_mid + 0.78, z2_top - 0.52, 0.16, "jalousie")],
        pilasters=False,
    )
    add_signboard_panel(mb, cx2 - 0.6, -hy + 0.10, 2.64, 3.26, 3.85, "sign_panaderya", facing="-Y", relief=0.06, framed=True)
    add_signboard_panel(mb, cx2 + w2 * 0.5, -1.2, 1.05, 1.92, 2.4, "chb_bawal_umihi", facing="+X", relief=0.035, framed=False)
    add_gable_yero_roof(
        mb, cx2, -0.10, z2_top, w2, d + 0.36, rise=1.18, ov_x=0.38, ov_y=0.50,
        roof_swatch="yero_zinc_weathered", wall_swatch="chb_raw_moss", downspout_corners=[(-1, 1)], ov_x_right=0.0,
    )
    add_firewall_parapet(mb, cx2 + w2 * 0.5 + 0.08, -0.10, d + 0.36, z2_top, z2_top + 1.18, "chb_raw_moss")

    add_rear_dirty_kitchen(mb, -4.2, hy, width=3.4, depth=1.08, z_attach=2.68)
    return mb


# ---------------------------------------------------------------------------
# 5. Two-Storey Provincial Family Home (`brgy_house_two_storey.glb` ~ 8.4m x 7.8m x 7.6m)
# ---------------------------------------------------------------------------

def build_house_two_storey(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    mb = MeshBuilder(uvs)
    w, d = 7.8, 6.4
    hy = d * 0.5
    z_p, z_mid, z_top = 0.32, 3.38, 6.18

    mb.frustum_plinth(0.0, 0.0, 0.0, 0.20, z_p, w + 0.30, d + 0.30, w + 0.06, d + 0.06)

    # Ground Floor: Adobe/CHB base with recessed front silong porch & bougainvillea planter
    _add_four_walls(
        mb, 0.0, 0.0, w, d, z_p, z_mid,
        sw_front="adobe_stone", sw_right="chb_plaster_cracked", sw_rear="chb_cream_moss", sw_left="chb_raw_moss",
        ops_front=[
            Opening(0.65, 4.35, z_p, 2.75, 0.82, "silong_open", swatch="chb_cream_moss"),
            Opening(4.95, 7.05, 0.98, 2.32, 0.22, "capiz_rehas"),
        ],
        ops_right=[
            Opening(1.4, 2.05, 2.42, 2.80, 0.15, "bath_vent"),
            Opening(3.5, 5.0, 1.12, 2.28, 0.20, "jalousie_rehas"),
        ],
        ops_left=[Opening(2.4, 3.1, 2.42, 2.80, 0.15, "bath_vent")],
        ops_rear=[
            Opening(1.1, 2.1, z_p, 2.32, 0.18, "door_rear"),
            Opening(4.2, 6.2, 1.15, 2.25, 0.18, "jalousie"),
        ],
    )
    # Front Silong Porch Columns + Bougainvillea Masonry Planter Box (`min_z = 0.0`)
    for px in (-3.25, 0.60):
        mb.box(px, -hy - 0.04, 0.0, 0.42, 0.36, 0.36, "concrete_moss", "concrete_pillar")
        mb.box(px, -hy - 0.04, 0.42, z_mid - 0.06, 0.26, 0.26, "concrete_pillar")
    mb.box(2.15, -hy - 0.28, 0.0, 0.56, 2.25, 0.52, "adobe_stone", "concrete_pillar")
    mb.box(2.15, -hy - 0.28, 0.56, 0.92, 2.05, 0.40, "bougainvillea")

    # Overhanging 2nd Floor Hardwood & Capiz Gallery (`+0.48m` over front street!)
    w_u, d_u, cy_u = w + 0.24, d + 0.52, -0.26
    mb.cornice_ring(0.0, cy_u, z_mid - 0.08, 0.20, w_u, d_u, proj=0.12, swatch="wood_dark")
    for jx in range(6):
        bx = -w * 0.44 + jx * (w * 0.88 / 5.0)
        mb.tube((bx, -hy + 0.04, z_mid - 0.38), (bx, -hy - 0.46, z_mid - 0.04), 0.042, 0.036, 4, "wood_dark")

    _add_four_walls(
        mb, 0.0, cy_u, w_u, d_u, z_mid + 0.12, z_top,
        sw_front="narra_warm", sw_right="planks_vertical", sw_rear="narra_warm", sw_left="chb_raw_moss",
        ops_front=[
            Opening(0.55, 2.55, z_mid + 0.72, z_top - 0.48, 0.20, "capiz"),
            Opening(3.00, 5.00, z_mid + 0.72, z_top - 0.48, 0.20, "capiz"),
            Opening(5.45, 7.45, z_mid + 0.72, z_top - 0.48, 0.20, "capiz"),
        ],
        ops_right=[Opening(1.8, 4.6, z_mid + 0.82, z_top - 0.52, 0.18, "capiz")],
        ops_left=[Opening(2.8, 3.6, z_mid + 1.55, z_mid + 1.95, 0.15, "bath_vent")],
        ops_rear=[
            Opening(1.2, 3.4, z_mid + 0.82, z_top - 0.52, 0.18, "jalousie"),
            Opening(4.6, 6.8, z_mid + 0.82, z_top - 0.52, 0.18, "jalousie"),
        ],
        pilasters=False,
    )
    mb.cornice_ring(0.0, cy_u, z_top - 0.14, 0.16, w_u, d_u, proj=0.10, swatch="wood_dark")

    add_gable_yero_roof(
        mb, 0.0, cy_u, z_top, w_u, d_u, rise=1.42, ov_x=0.46, ov_y=0.56,
        roof_swatch="yero_red_oxide", wall_swatch="narra_warm",
        downspout_corners=[(1, -1), (1, 1)], ov_x_left=0.0,
    )
    add_firewall_parapet(mb, -w_u * 0.5 - 0.08, cy_u, d_u, z_top, z_top + 1.42, "chb_raw_moss")
    add_meralco_side_meter_and_weatherhead(mb, w * 0.5, -1.25, z_top, is_right_side=True)
    add_rear_dirty_kitchen(mb, -1.15, hy, width=3.3, depth=1.08, z_attach=2.68)
    return mb


# ---------------------------------------------------------------------------
# 6. Civic Barangay Hall (`brgy_hall_plaza.glb` ~ 16.0m x 10.0m x 9.3m)
# ---------------------------------------------------------------------------

def build_hall_plaza(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    mb = MeshBuilder(uvs)
    w, d = 15.2, 8.6
    hy = d * 0.5
    z_p, z_mid, z_top = 0.42, 3.85, 7.05

    mb.frustum_plinth(0.0, 0.0, 0.0, 0.26, z_p, w + 0.44, d + 0.44, w + 0.10, d + 0.10)

    # Wide 3-step civic entrance stair on Front (-Y) (`Z = 0.000`)
    mb.box(0.0, -hy - 0.72, 0.0, 0.14, 6.8, 1.45, "concrete_moss", "concrete_pillar")
    mb.box(0.0, -hy - 0.48, 0.14, 0.28, 6.4, 0.96, "concrete_pillar")
    mb.box(0.0, -hy - 0.24, 0.28, z_p, 6.0, 0.48, "concrete_pillar")

    # Ground Floor Civic Walls (deep central portico + side transaction windows)
    _add_four_walls(
        mb, 0.0, 0.0, w, d, z_p, z_mid,
        sw_front="chb_cream_moss", sw_right="stucco_sandstone", sw_rear="chb_cream_moss", sw_left="stucco_sandstone",
        ops_front=[
            Opening(1.1, 3.6, 1.15, 2.65, 0.24, "capiz_rehas"),
            Opening(4.5, 10.7, z_p, 3.15, 0.92, "silong_open", swatch="stucco_sandstone"),
            Opening(11.6, 14.1, 1.15, 2.65, 0.24, "capiz_rehas"),
        ],
        ops_right=[
            Opening(1.5, 3.6, 1.20, 2.65, 0.22, "capiz_rehas"),
            Opening(5.0, 7.1, 1.20, 2.65, 0.22, "capiz_rehas"),
        ],
        ops_left=[
            Opening(1.5, 3.6, 1.20, 2.65, 0.22, "capiz_rehas"),
            Opening(5.0, 7.1, 1.20, 2.65, 0.22, "capiz_rehas"),
        ],
        ops_rear=[
            Opening(2.2, 4.4, 1.25, 2.60, 0.20, "jalousie_rehas"),
            Opening(6.4, 8.8, z_p, 2.75, 0.26, "door_rear"),
            Opening(10.8, 13.0, 1.25, 2.60, 0.20, "jalousie_rehas"),
        ],
    )

    # 4 Grand Civic Portico Columns + Cantilevered 2F Council Balcony
    for px in (-3.1, -1.05, 1.05, 3.1):
        mb.box(px, -hy - 0.85, 0.0, z_p + 0.08, 0.46, 0.46, "concrete_moss", "concrete_pillar")
        mb.box(px, -hy - 0.85, z_p + 0.08, z_mid - 0.06, 0.34, 0.34, "concrete_pillar")
    mb.box(0.0, -hy - 0.56, z_mid - 0.08, z_mid + 0.16, 7.2, 1.18, "concrete_pillar")
    # Council balcony concrete balustrade
    mb.box(0.0, -hy - 1.08, z_mid + 0.16, z_mid + 1.06, 7.0, 0.14, "concrete_pillar")
    mb.box(-3.43, -hy - 0.56, z_mid + 0.16, z_mid + 1.06, 0.14, 1.08, "concrete_pillar")
    mb.box(3.43, -hy - 0.56, z_mid + 0.16, z_mid + 1.06, 0.14, 1.08, "concrete_pillar")

    mb.cornice_ring(0.0, 0.0, z_mid - 0.10, 0.24, w, d, proj=0.18)

    # 2nd Floor Civic Session Hall
    _add_four_walls(
        mb, 0.0, 0.0, w, d, z_mid + 0.14, z_top,
        sw_front="stucco_sandstone", sw_right="stucco_sandstone", sw_rear="chb_cream_moss", sw_left="stucco_sandstone",
        ops_front=[
            Opening(1.1, 3.6, z_mid + 0.88, z_top - 0.58, 0.22, "capiz"),
            Opening(4.8, 6.9, z_mid + 0.20, z_top - 0.62, 0.24, "door_front"),
            Opening(8.3, 10.4, z_mid + 0.20, z_top - 0.62, 0.24, "door_front"),
            Opening(11.6, 14.1, z_mid + 0.88, z_top - 0.58, 0.22, "capiz"),
        ],
        ops_right=[
            Opening(1.6, 3.8, z_mid + 0.90, z_top - 0.60, 0.20, "capiz"),
            Opening(4.8, 7.0, z_mid + 0.90, z_top - 0.60, 0.20, "capiz"),
        ],
        ops_left=[
            Opening(1.6, 3.8, z_mid + 0.90, z_top - 0.60, 0.20, "capiz"),
            Opening(4.8, 7.0, z_mid + 0.90, z_top - 0.60, 0.20, "capiz"),
        ],
        ops_rear=[
            Opening(2.4, 5.2, z_mid + 0.95, z_top - 0.65, 0.20, "jalousie"),
            Opening(10.0, 12.8, z_mid + 0.95, z_top - 0.65, 0.20, "jalousie"),
        ],
    )
    mb.cornice_ring(0.0, 0.0, z_top - 0.18, 0.22, w, d, proj=0.20)

    # Central Deadlock-style Civic Pediment + "BARANGAY HALL • SAN ROQUE" Frieze Signboard!
    mb.box(0.0, -hy - 0.12, z_top, z_top + 1.05, 7.4, 0.36, "concrete_pillar")
    mb.tri((-3.85, -hy - 0.30, z_top + 1.05), (3.85, -hy - 0.30, z_top + 1.05), (0.0, -hy - 0.30, z_top + 1.95), "concrete_pillar")
    add_signboard_panel(mb, 0.0, -hy - 0.30, z_top + 0.08, z_top + 0.98, 6.2, "sign_brgy_hall", facing="-Y", relief=0.07, framed=True)

    # Civic Flagpole rising from balcony pedestal
    mb.tube((0.0, -hy - 1.08, z_mid + 1.06), (0.0, -hy - 1.08, z_top + 2.45), 0.045, 0.028, 6, "zinc_brushed")
    mb.box(0.46, -hy - 1.08, z_top + 1.85, z_top + 2.38, 0.88, 0.04, "team_blue")

    add_gable_yero_roof(
        mb, 0.0, 0.0, z_top, w, d, rise=1.55, ov_x=0.58, ov_y=0.62,
        roof_swatch="yero_green_faded", wall_swatch="stucco_sandstone",
        downspout_corners=[(-1, -1), (1, -1), (-1, 1), (1, 1)],
    )
    add_meralco_side_meter_and_weatherhead(mb, w * 0.5, -1.8, z_top, is_right_side=True)
    add_rear_dirty_kitchen(mb, -4.1, hy, width=3.8, depth=1.10, z_attach=2.85)
    return mb


# ---------------------------------------------------------------------------
# 7. Walk-Through Covered Basketball Court (`brgy_covered_court.glb` ~ 18.0m x 22.0m x 8.8m)
# ---------------------------------------------------------------------------

def build_covered_court(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    mb = MeshBuilder(uvs)
    col_x = 8.25
    col_ys = [-9.6, -3.2, 3.2, 9.6]
    z_eave = 5.45
    z_peak = 8.45

    # 1. 8 Grounded chamfered concrete pedestals (`Z = 0.000`) + painted steel I-columns + knee braces
    for sx in (-1.0, 1.0):
        x = sx * col_x
        for y in col_ys:
            mb.frustum_plinth(x, y, 0.0, 0.65, 1.12, 0.72, 0.72, 0.48, 0.48, "concrete_moss", "concrete_pillar")
            mb.box(x, y, 1.12, z_eave, 0.30, 0.30, "steel_dark")
            # Inward diagonal steel knee brace meeting bottom truss chord cleanly (100% open player clearance!)
            mb.tube((x, y, 4.25), (x - sx * 1.18, y, z_eave - 0.01), 0.055, 0.05, 5, "steel_dark")

        # Low 2-tier concrete bleachers strictly along outer column line between bays
        for by in (-6.4, 0.0, 6.4):
            mb.box(x - sx * 0.15, by, 0.0, 0.38, 0.92, 5.2, "concrete_moss", "concrete_pillar")
            mb.box(x + sx * 0.15, by, 0.38, 0.74, 0.46, 5.2, "concrete_pillar", "tile_terracotta")

        # Longitudinal steel eaves tie-beam + galvanized box gutter (`Y = -10.8..+10.8`)
        mb.box(x, 0.0, z_eave - 0.24, z_eave + 0.08, 0.28, 21.4, "steel_dark")
        mb.box(x + sx * 0.56, 0.0, z_eave - 0.18, z_eave - 0.02, 0.16, 21.8, "zinc_brushed")

    # 2. Curved Arched Corrugated GI Vault Roof (`7` segments across X = -8.85..+8.85, Y = -10.9..+10.9)
    n_arch = 8
    half_span = col_x + 0.58
    hy_roof = 10.9
    th = 0.07

    def arch_pt(idx: int) -> Tuple[float, float]:
        t = -1.0 + 2.0 * (idx / n_arch)
        ax = t * half_span
        az = z_eave - 0.12 + (z_peak - (z_eave - 0.12)) * (1.0 - t * t)
        return ax, az

    for i in range(n_arch):
        x0, z0 = arch_pt(i)
        x1, z1 = arch_pt(i + 1)
        mb.quad((x0, -hy_roof, z0 + th), (x1, -hy_roof, z1 + th), (x1, hy_roof, z1 + th), (x0, hy_roof, z0 + th), "yero_green_faded")
        mb.quad((x0, hy_roof, z0), (x1, hy_roof, z1), (x1, -hy_roof, z1), (x0, -hy_roof, z0), "zinc_brushed")
        # Front & Rear arch fascia trim
        mb.quad((x0, -hy_roof, z0), (x1, -hy_roof, z1), (x1, -hy_roof, z1 + th), (x0, -hy_roof, z0 + th), "steel_dark")
        mb.quad((x1, hy_roof, z1), (x0, hy_roof, z0), (x0, hy_roof, z0 + th), (x1, hy_roof, z1 + th), "steel_dark")

    # 3. Triangulated Steel Bowstring Roof Trusses above each column pair (`Y = -9.6, -3.2, +3.2, +9.6`)
    for ty in col_ys:
        # Horizontal bottom tie-chord at Z = z_eave
        mb.tube((-col_x, ty, z_eave), (col_x, ty, z_eave), 0.058, 0.058, 5, "steel_dark")
        # Vertical king-post, queen-posts & diagonal Warren web struts!
        nodes_f = [-0.88, -0.56, -0.28, 0.0, 0.28, 0.56, 0.88]
        for idx_n, tx_f in enumerate(nodes_f):
            tx = tx_f * half_span
            tz = z_eave - 0.12 + (z_peak - (z_eave - 0.12)) * (1.0 - tx_f * tx_f)
            if abs(tx_f) < 0.85:
                mb.tube((tx, ty, z_eave), (tx, ty, tz), 0.042, 0.042, 4, "steel_dark")
            if idx_n + 1 < len(nodes_f):
                nx_f = nodes_f[idx_n + 1]
                nx = nx_f * half_span
                nz = z_eave - 0.12 + (z_peak - (z_eave - 0.12)) * (1.0 - nx_f * nx_f)
                # Alternating Warren diagonal strut between bottom tie-chord and arched top chord
                if idx_n % 2 == 0:
                    mb.tube((tx, ty, z_eave), (nx, ty, nz), 0.036, 0.036, 4, "steel_dark")
                else:
                    mb.tube((tx, ty, tz), (nx, ty, z_eave), 0.036, 0.036, 4, "steel_dark")

    # 4. Front & Rear Civic Entrance Arch Banners (`BRGY. SAN ROQUE COVERED COURT`)
    for sy_sgn, face_dir in ((-1.0, "-Y"), (1.0, "+Y")):
        by = sy_sgn * 10.2
        mb.box(0.0, by, z_eave - 0.25, z_eave + 0.78, 9.6, 0.18, "steel_dark")
        add_signboard_panel(mb, 0.0, by, z_eave - 0.20, z_eave + 0.74, 8.8, "sign_covered_court", facing=face_dir, relief=0.11, framed=True)

    # 5. Twin Overhead Basketball Goals suspended from end trusses (`Z >= 2.88m` -> full walkthrough clearance!)
    for sy_sgn in (-1.0, 1.0):
        gy = sy_sgn * 9.15
        inward = -sy_sgn
        # Dual steel drop-hangers from truss down to backboard
        for gx in (-0.52, 0.52):
            mb.tube((gx, gy, 3.65), (gx, gy, z_eave + 0.65), 0.038, 0.038, 5, "steel_dark")
        # Backboard (`1.80 x 1.08m`) + orange steel rim + chain net
        mb.box(0.0, gy + inward * 0.08, 2.92, 3.98, 1.80, 0.07, "sign_covered_court")
        rim_y = gy + inward * 0.42
        for seg in range(10):
            a0 = 2.0 * math.pi * seg / 10
            a1 = 2.0 * math.pi * (seg + 1) / 10
            mb.tube(
                (0.24 * math.cos(a0), rim_y + 0.24 * math.sin(a0), 3.05),
                (0.24 * math.cos(a1), rim_y + 0.24 * math.sin(a1), 3.05),
                0.018, 0.018, 5, "team_red", cap_start=False, cap_end=False,
            )
        mb.tube((0.0, rim_y, 2.66), (0.0, rim_y, 3.03), 0.15, 0.23, 8, "sari_grill_counter", cap_start=False, cap_end=False)

    # 6. 4 Corner Grounded PVC Downspouts (`Z = 0.004`)
    for sx_sgn in (-1, 1):
        for sy_sgn in (-1, 1):
            add_pvc_downspout(
                mb,
                gutter_pt=(sx_sgn * (col_x + 0.54), sy_sgn * 9.8, z_eave - 0.20),
                wall_xy=(sx_sgn * (col_x + 0.22), sy_sgn * 9.6),
                out_dir=(float(sx_sgn), 0.0),
                z_ground=0.004,
            )
    return mb


# ---------------------------------------------------------------------------
# 8. Barangay Waiting Shed (`brgy_waiting_shed.glb` ~ 6.0m x 3.2m x 3.6m)
# ---------------------------------------------------------------------------

def build_waiting_shed(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    mb = MeshBuilder(uvs)
    w, d = 5.6, 2.7
    hy = d * 0.5

    # Grounded concrete floor slab with hazard-painted front curb (`Z = 0.000`)
    mb.box(0.0, 0.0, 0.0, 0.15, w, d, "hazard_curb", "concrete_pillar")

    # Rear CHB windbreak wall + side half-walls + civic mural signboard
    mb.box(0.0, hy - 0.11, 0.15, 2.38, w - 0.24, 0.18, "chb_cream_moss", "concrete_pillar")
    for sx in (-1.0, 1.0):
        mb.box(sx * (w * 0.5 - 0.16), 0.0, 0.15, 1.12, 0.16, d - 0.24, "chb_salmon_moss", "concrete_pillar")
    add_signboard_panel(mb, 0.0, hy - 0.20, 1.28, 2.18, 3.6, "sign_brgy_hall", facing="-Y", relief=0.05, framed=True)

    # Built-in tiled concrete bench along rear & side walls
    mb.box(0.0, hy - 0.46, 0.15, 0.52, w - 0.64, 0.52, "concrete_moss", "tile_terracotta")

    # 4 Tubular steel posts + diagonal gussets
    for px in (-w * 0.5 + 0.18, -0.95, 0.95, w * 0.5 - 0.18):
        mb.tube((px, -hy + 0.18, 0.15), (px, -hy + 0.18, 2.98), 0.048, 0.045, 6, "steel_dark")
        mb.tube((px, -hy + 0.18, 2.35), (px, -hy + 0.62, 2.86), 0.028, 0.025, 5, "steel_dark")
    for px in (-w * 0.5 + 0.18, w * 0.5 - 0.18):
        mb.tube((px, hy - 0.12, 1.12), (px, hy - 0.12, 2.62), 0.048, 0.045, 6, "steel_dark")

    # Overhanging corrugated GI shed roof sloping toward rear gutter + grounded PVC downspout
    add_shed_yero_roof(mb, 0.0, 0.0, 2.62, 3.06, w, d, ov_x=0.34, ov_y=0.38, roof_swatch="yero_green_faded", high_at_rear=False)
    add_pvc_downspout(mb, (w * 0.5 + 0.12, hy + 0.35, 2.58), (w * 0.5 - 0.14, hy + 0.04), (0.0, 1.0), 0.004)
    return mb


# ---------------------------------------------------------------------------
# 9. Palengke Wet/Dry Market Stall (`brgy_palengke_stall.glb` ~ 6.4m x 4.4m x 3.9m)
# ---------------------------------------------------------------------------

def build_palengke_stall(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    mb = MeshBuilder(uvs)
    w, d = 5.8, 3.6
    hy = d * 0.5

    mb.frustum_plinth(0.0, 0.0, 0.0, 0.12, 0.18, w + 0.16, d + 0.16, w, d)

    # 6 Coco-lumber corner/mid posts + knee braces
    for px in (-w * 0.5 + 0.16, 0.0, w * 0.5 - 0.16):
        for py in (-hy + 0.16, hy - 0.16):
            mb.box(px, py, 0.18, 2.78, 0.13, 0.13, "wood_coco")

    # Front (-Y): Tiled market counter + 4 sloped produce crates filled with mangoes & greens
    mb.box(0.0, -hy + 0.52, 0.18, 0.94, w - 0.36, 0.88, "chb_cream_moss", "tile_teal_chipped")
    for bi in range(4):
        bx = -2.02 + bi * 1.35
        mb.box(bx, -hy + 0.48, 0.94, 1.16, 1.18, 0.72, "wood_dark", "produce_mango" if bi % 2 == 0 else "leaf_banana")
        # Sculpted produce mound
        mb.frustum_plinth(bx, -hy + 0.48, 1.14, 1.24, 1.32, 1.02, 0.56, 0.62, 0.32, "produce_mango" if bi % 2 == 0 else "leaf_banana", "produce_mango" if bi % 2 == 0 else "leaf_banana")

    # Hanging pan scale + sachet strip on front right
    mb.tube((1.75, -hy + 0.35, 1.85), (1.75, -hy + 0.35, 2.65), 0.014, 0.014, 5, "steel_dark")
    mb.tube((1.75, -hy + 0.35, 1.76), (1.75, -hy + 0.35, 1.84), 0.22, 0.16, 8, "hazard_curb")
    mb.box(-2.25, -hy + 0.20, 1.48, 2.52, 0.38, 0.05, "sachet_strips")

    # Rear (+Y) asymmetric half-wall, wooden storage crates & blue water drum
    mb.box(0.0, hy - 0.14, 0.18, 1.55, w - 0.32, 0.16, "sawali_aged", "wood_dark")
    mb.box(-1.6, hy - 0.55, 0.18, 0.78, 0.85, 0.62, "plywood_weathered", "wood_coco")
    mb.tube((1.95, hy - 0.52, 0.18), (1.95, hy - 0.52, 0.98), 0.25, 0.26, 8, "water_drum_blue")

    add_storefront_awning(mb, 0.0, -hy, 2.62, w - 0.20, proj=0.88, drop=0.32, swatch="trapal_striped")
    add_rooftop_eave_signboard(mb, 0.0, -hy - 0.40, 2.72, height=0.72, width=3.9, swatch="sign_sari_sari")
    add_gable_yero_roof(
        mb, 0.0, 0.0, 2.78, w, d, rise=0.96, ov_x=0.34, ov_y=0.42,
        roof_swatch="yero_patchwork", wall_swatch="sawali_honey", downspout_corners=[(1, 1)],
    )
    return mb


# ---------------------------------------------------------------------------
# 10–17. Street, Canal & Base Utility Assets (`BarangayAtlas`)
# ---------------------------------------------------------------------------

def build_footbridge(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    """Arched concrete & rusted GI pipe Estero footbridge (`~9.0m W x 13.8m D x 1.85m H`, `min_y = 0`)."""
    mb = MeshBuilder(uvs)
    w, span_y = 8.8, 13.6
    n_seg = 6
    hy = span_y * 0.5

    # Grounded concrete abutment piers at both canal banks & mid-span (`Z = 0.000`)
    for py in (-hy + 0.45, 0.0, hy - 0.45):
        for px in (-w * 0.5 + 0.38, 0.0, w * 0.5 - 0.38):
            h_pier = 0.38 if abs(py) > 2.0 else 0.62
            mb.frustum_plinth(px, py, 0.0, h_pier * 0.6, h_pier, 0.68, 0.78, 0.52, 0.62, "concrete_moss", "concrete_pillar")

    # Cambered arched concrete deck + yellow/black hazard curbs + welded steel pipe railings
    def deck_z(y_val: float) -> float:
        t = y_val / hy
        return 0.62 * (1.0 - 0.68 * t * t)

    for i in range(n_seg):
        ya = -hy + i * (span_y / n_seg)
        yb = -hy + (i + 1) * (span_y / n_seg)
        za, zb = deck_z(ya), deck_z(yb)
        th = 0.22
        # Deck top, soffit, and side girders
        mb.quad((-w * 0.5, ya, za + th), (w * 0.5, ya, za + th), (w * 0.5, yb, zb + th), (-w * 0.5, yb, zb + th), "concrete_pillar")
        mb.quad((-w * 0.5, yb, zb), (w * 0.5, yb, zb), (w * 0.5, ya, za), (-w * 0.5, ya, za), "concrete_moss")
        for sx in (-1.0, 1.0):
            curb_x = sx * (w * 0.5 - 0.18)
            ym = 0.5 * (ya + yb)
            zm = 0.5 * (za + zb) + th
            mb.box(curb_x, ym, zm - 0.04, zm + 0.16, 0.34, (yb - ya) + 0.02, "hazard_curb", "hazard_curb")
            # Railing posts & top/mid welded GI pipes
            mb.tube((curb_x, ya + 0.08, za + th + 0.14), (curb_x, ya + 0.08, za + th + 1.12), 0.038, 0.035, 6, "steel_rusted")
            mb.tube((curb_x, ya, za + th + 1.10), (curb_x, yb, zb + th + 1.10), 0.040, 0.040, 6, "steel_rusted")
            mb.tube((curb_x, ya, za + th + 0.62), (curb_x, yb, zb + th + 0.62), 0.030, 0.030, 5, "steel_dark")
    return mb


def build_sorbetes_cart(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    """Festive Pinoy Dirty Ice Cream cart (`~1.95m x 1.15m x 2.18m`, `min_y = 0`)."""
    mb = MeshBuilder(uvs)
    r_wheel = 0.41

    # Two spoked wooden side wheels touching Z = 0.000 + front iron rest leg at Z = 0.000
    for sx in (-0.52, 0.52):
        # Horizontal wheel hub & rim along X axis
        mb.tube((sx - 0.04, -0.08, r_wheel), (sx + 0.04, -0.08, r_wheel), r_wheel, r_wheel, 14, "wood_dark")
        mb.tube((sx - 0.05, -0.08, r_wheel), (sx + 0.05, -0.08, r_wheel), 0.09, 0.09, 8, "zinc_brushed")
    mb.tube((-0.54, -0.08, r_wheel), (0.54, -0.08, r_wheel), 0.028, 0.028, 6, "steel_dark")
    # Front rest prop leg touching Z = 0.000
    mb.tube((0.0, -0.62, 0.0), (0.0, -0.52, 0.36), 0.025, 0.028, 6, "steel_dark")
    mb.box(0.0, -0.62, 0.0, 0.03, 0.16, 0.10, "steel_dark")

    # Ornate painted wooden cart chest (`Z = 0.34..1.14m`) with beveled trim & dedicated Sorbetes signboard!
    mb.box(0.0, -0.05, 0.34, 1.12, 0.92, 1.28, "hazard_curb", "zinc_brushed")
    mb.cornice_ring(0.0, -0.05, 1.06, 0.09, 0.92, 1.28, proj=0.05, swatch="team_red")
    add_signboard_panel(mb, 0.0, -0.69, 0.44, 1.00, 0.78, "sign_sorbetes", facing="-Y", relief=0.03, framed=True)

    # 3 Stainless sorbetes ice-cream canister lids on top deck
    for cy in (-0.38, -0.04, 0.30):
        mb.tube((0.0, cy, 1.12), (0.0, cy, 1.22), 0.13, 0.12, 8, "zinc_brushed")
        mb.tube((0.0, cy, 1.22), (0.0, cy, 1.27), 0.035, 0.025, 6, "wood_dark")

    # Rear push handlebar + brass hand-bell (`tililing!`)
    for sx in (-0.38, 0.38):
        mb.tube((sx, 0.59, 0.98), (sx, 0.90, 1.08), 0.022, 0.022, 5, "wood_dark")
    mb.tube((-0.40, 0.90, 1.08), (0.40, 0.90, 1.08), 0.024, 0.024, 6, "wood_dark")
    mb.tube((0.28, 0.86, 1.10), (0.28, 0.86, 1.21), 0.055, 0.018, 7, "produce_mango")

    # Fiesta sun umbrella pole + 8-ribbed striped canopy (`Z = 1.12..2.18m`)
    mb.tube((0.0, 0.05, 1.12), (0.0, 0.05, 2.18), 0.024, 0.022, 6, "wood_coco")
    apex = Vector((0.0, 0.05, 2.18))
    n_u = 8
    r_umb = 0.96
    for i in range(n_u):
        a0 = 2.0 * math.pi * i / n_u
        a1 = 2.0 * math.pi * (i + 1) / n_u
        p0 = Vector((r_umb * math.cos(a0), 0.05 + r_umb * math.sin(a0), 1.86))
        p1 = Vector((r_umb * math.cos(a1), 0.05 + r_umb * math.sin(a1), 1.86))
        sw = "team_red" if i % 2 == 0 else "trapal_striped"
        mb.tri(p0, p1, apex, sw)
        mb.tri(p1, p0, apex - Vector((0.0, 0.0, 0.02)), "wood_coco")
    return mb


def _build_utility_pole(
    uvs: Dict[str, Tuple[float, float, float, float]],
    height: float = 13.6,
    team_swatch: str | None = None,
) -> MeshBuilder:
    """Shared detailed builder for `brgy_meralco_post` (`13.6m`) and `brgy_post_blue/red` (`10.5m`)."""
    mb = MeshBuilder(uvs)

    # Grounded octagonal mossy concrete base collar (`Z = 0.000`)
    mb.tube((0.0, 0.0, 0.0), (0.0, 0.0, 0.85), 0.27, 0.22, 8, "concrete_moss")
    # Tapered concrete/treated-timber utility pole shaft
    mb.tube((0.0, 0.0, 0.85), (0.0, 0.0, height), 0.20, 0.115, 8, "concrete_pillar")

    if team_swatch is not None:
        # Crisp team-colored base collars & fluttering home-post banner!
        mb.tube((0.0, 0.0, 0.95), (0.0, 0.0, 2.45), 0.205, 0.19, 8, team_swatch)
        mb.tube((0.0, 0.0, 4.20), (0.0, 0.0, 5.10), 0.175, 0.165, 8, team_swatch)
        mb.box(0.44, 0.0, height - 2.65, height - 1.55, 0.72, 0.05, team_swatch)
    else:
        # Authentic 90s Halalan & Malabanan Poso Negro notices stapled around eye level!
        add_signboard_panel(mb, 0.0, -0.19, 1.35, 2.02, 0.34, "poster_vote_halalan", facing="-Y", relief=0.025, framed=False)
        add_signboard_panel(mb, 0.19, 0.0, 1.85, 2.48, 0.34, "poster_malabanan", facing="+X", relief=0.025, framed=False)
        # Cylindrical gray pole-mounted Meralco distribution transformer (`Z = 7.6..8.75m`)
        mb.tube((0.0, -0.44, 7.65), (0.0, -0.44, 8.75), 0.26, 0.26, 10, "zinc_brushed")
        mb.box(0.0, -0.22, 7.85, 8.55, 0.18, 0.22, "steel_dark")
        for bx in (-0.11, 0.0, 0.11):
            mb.tube((bx, -0.44, 8.75), (bx, -0.44, 9.04), 0.038, 0.024, 6, "steel_dark")

    # Double wooden crossarms + steel diagonal braces + ceramic pin insulators
    arm_zs = [height - 2.2, height - 0.55] if team_swatch is None else [height - 0.65]
    for az in arm_zs:
        arm_w = 1.75 if team_swatch is None else 1.35
        mb.box(0.0, -0.14, az, az + 0.11, arm_w, 0.11, "wood_dark")
        for sx in (-1.0, 1.0):
            mb.tube((0.0, -0.10, az - 0.42), (sx * arm_w * 0.36, -0.14, az), 0.018, 0.018, 4, "steel_dark")
        for ix in (-arm_w * 0.42, -arm_w * 0.16, arm_w * 0.16, arm_w * 0.42):
            mb.tube((ix, -0.14, az + 0.11), (ix, -0.14, az + 0.24), 0.042, 0.028, 6, "concrete_pillar")

    # Curved galvanized streetlamp gooseneck arm + cobra-head luminaire
    lamp_z = height * 0.54
    mb.tube((0.0, -0.14, lamp_z), (0.0, -0.95, lamp_z + 0.42), 0.030, 0.025, 6, "zinc_brushed")
    mb.box(0.0, -1.08, lamp_z + 0.36, lamp_z + 0.48, 0.24, 0.42, "zinc_brushed", "zinc_brushed", "produce_mango")
    return mb


def build_cover_wall(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    """Authentic Barangay Breeze-Block (*Ventanilla*) & Bougainvillea Planter Wall + Softdrink *Kaha* Crates
    (`~4.18m x 0.90m x 1.34m`, `min_y = 0`, zero military sandbags/rebar!)."""
    mb = MeshBuilder(uvs)
    w = 3.85

    # Grounded mossy concrete curb foundation (`Z = 0.000`)
    mb.frustum_plinth(-0.18, 0.0, 0.0, 0.14, 0.22, w, 0.56, w - 0.06, 0.44, "concrete_moss", "concrete_pillar")

    # 3 Molded plastered concrete piers (`X = -1.96, -0.18, +1.60`)
    for px in (-1.96, -0.18, 1.60):
        mb.box(px, 0.0, 0.22, 1.24, 0.30, 0.42, "concrete_pillar")
        mb.frustum_plinth(px, 0.0, 1.24, 1.27, 1.31, 0.36, 0.48, 0.26, 0.34, "concrete_pillar", "concrete_pillar")

    # Lower plastered CHB masonry wall across both bays (`Z = 0.22..0.78m`) + BAWAL UMIHI stencil on left bay
    for bay_x in (-1.07, 0.71):
        mb.box(bay_x, 0.0, 0.22, 0.78, 1.48, 0.24, "chb_cream_moss", "concrete_pillar")
    add_signboard_panel(mb, -1.07, -0.12, 0.28, 0.74, 1.28, "chb_bawal_umihi", facing="-Y", relief=0.025, framed=False)

    # Decorative 3D concrete Breeze-Block (*Ventanilla*) pierced screen band (`Z = 0.78..1.06m`)!
    for bay_x in (-1.07, 0.71):
        # Horizontal sill & top lintel of breeze-block course
        mb.box(bay_x, 0.0, 0.76, 0.81, 1.48, 0.26, "concrete_pillar")
        mb.box(bay_x, 0.0, 1.03, 1.08, 1.48, 0.28, "concrete_pillar")
        # Vertical & diamond concrete dividers creating real see-through ventanilla apertures!
        for vi in range(5):
            vx = bay_x - 0.60 + vi * 0.30
            mb.box(vx, 0.0, 0.81, 1.03, 0.055, 0.22, "stucco_sandstone")
            if vi < 4:
                mx = vx + 0.15
                mb.tube((vx, 0.0, 0.92), (mx, 0.0, 1.03), 0.022, 0.022, 4, "stucco_sandstone")
                mb.tube((mx, 0.0, 1.03), (vx + 0.30, 0.0, 0.92), 0.022, 0.022, 4, "stucco_sandstone")
                mb.tube((vx, 0.0, 0.92), (mx, 0.0, 0.81), 0.022, 0.022, 4, "stucco_sandstone")
                mb.tube((mx, 0.0, 0.81), (vx + 0.30, 0.0, 0.92), 0.022, 0.022, 4, "stucco_sandstone")

    # Terracotta planter trough on top (`Z = 1.08..1.22m`) + lush Bougainvillea floral cushions (`Z = 1.18..1.34m`)!
    for bay_x in (-1.07, 0.71):
        mb.box(bay_x, 0.0, 1.08, 1.22, 1.42, 0.34, "tile_terracotta", "adobe_stone")
        for off_x in (-0.38, 0.0, 0.38):
            mb.frustum_plinth(
                bay_x + off_x, 0.0, 1.18, 1.28, 1.34,
                0.56, 0.42, 0.30, 0.24, "bougainvillea", "bougainvillea",
            )

    # Stacked wooden softdrink *kaha* bottle crates + 5-gallon blue water jug grounded at right end (`Z = 0.000`)!
    mb.box(1.94, -0.14, 0.0, 0.32, 0.44, 0.34, "narra_warm", "team_red")
    mb.box(1.92, -0.12, 0.32, 0.64, 0.44, 0.34, "wood_coco", "hazard_curb", yaw=0.14)
    mb.tube((1.96, 0.24, 0.0), (1.96, 0.24, 0.46), 0.14, 0.145, 8, "water_drum_blue")
    mb.tube((1.96, 0.24, 0.46), (1.96, 0.24, 0.54), 0.06, 0.045, 7, "water_drum_blue")
    return mb


def build_kapilya(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    """Barrio San Roque Chapel (`brgy_kapilya.glb` ~ 6.4m x 7.8m x 6.5m, `min_y = 0`):
    recessed sanctuary portico with wrought-iron grille gates, interior altar cross & candles,
    front roof *kampana* bell belfry, bougainvillea flower boxes, gutters & PVC downspouts."""
    mb = MeshBuilder(uvs)
    w, d = 5.8, 6.8
    hy = d * 0.5
    z_p, z_eave = 0.34, 4.15

    # Grounded beveled plinth + 2-step front plaza forecourt stair
    mb.frustum_plinth(0.0, 0.0, 0.0, 0.20, z_p, w + 0.32, d + 0.32, w + 0.08, d + 0.08)
    mb.box(0.0, -hy - 0.46, 0.0, 0.17, 3.6, 0.88, "concrete_moss", "concrete_pillar")
    mb.box(0.0, -hy - 0.22, 0.17, z_p, 3.2, 0.44, "concrete_pillar")

    # Twin front bougainvillea flower boxes flanking the chapel entrance (`min_z = 0.0`)
    for sx in (-1.0, 1.0):
        mb.box(sx * 2.25, -hy - 0.26, 0.0, 0.54, 1.15, 0.48, "tile_terracotta", "concrete_pillar")
        mb.frustum_plinth(sx * 2.25, -hy - 0.26, 0.50, 0.72, 0.84, 1.05, 0.42, 0.68, 0.26, "bougainvillea", "bougainvillea")

    # 3-Face Asymmetric Chapel Walls (deep recessed altar portico on -Y, capiz nave windows on ±X, sacristy door on +Y)
    _add_four_walls(
        mb, 0.0, 0.0, w, d, z_p, z_eave,
        sw_front="chb_cream_moss",
        sw_right="stucco_sandstone",
        sw_rear="chb_cream_moss",
        sw_left="stucco_sandstone",
        ops_front=[
            Opening(0.45, 1.25, 1.35, 2.65, 0.20, "capiz_rehas"),
            Opening(1.65, 4.15, z_p, 3.25, 0.88, "chapel_portico"),
            Opening(4.55, 5.35, 1.35, 2.65, 0.20, "capiz_rehas"),
        ],
        ops_right=[
            Opening(1.2, 2.7, 1.25, 2.75, 0.22, "capiz_rehas"),
            Opening(4.1, 5.6, 1.25, 2.75, 0.22, "capiz_rehas"),
        ],
        ops_left=[
            Opening(1.2, 2.7, 1.25, 2.75, 0.22, "capiz_rehas"),
            Opening(4.1, 5.6, 1.25, 2.75, 0.22, "capiz_rehas"),
        ],
        ops_rear=[
            Opening(1.4, 2.35, z_p, 2.38, 0.20, "door_rear"),
            Opening(3.5, 4.8, 1.45, 2.55, 0.18, "jalousie_rehas"),
        ],
    )
    mb.cornice_ring(0.0, 0.0, z_eave - 0.18, 0.20, w, d, proj=0.15)

    # Steep street-facing gable roof (`ridge_along_x=False`) + front *Kampana* Bell Belfry & Cross!
    add_gable_yero_roof(
        mb, 0.0, 0.0, z_eave, w, d, rise=1.45, ov_x=0.46, ov_y=0.52,
        roof_swatch="yero_green_faded", wall_swatch="chb_cream_moss", ridge_along_x=False,
        downspout_corners=[(-1, -1), (1, -1), (-1, 1), (1, 1)],
    )
    # Open concrete 4-pillar *kampana* belfry turret + brass bell + rooftop cross
    by = -hy + 0.45
    bz0 = z_eave + 1.18
    mb.box(0.0, by, bz0, bz0 + 0.16, 1.12, 1.12, "concrete_pillar")
    for px in (-0.42, 0.42):
        for py in (by - 0.42, by + 0.42):
            mb.box(px, py, bz0 + 0.16, bz0 + 1.05, 0.14, 0.14, "concrete_pillar")
    # Hanging brass kampana bell inside belfry!
    mb.tube((0.0, by, bz0 + 0.48), (0.0, by, bz0 + 0.88), 0.24, 0.08, 8, "produce_mango")
    mb.frustum_plinth(0.0, by, bz0 + 1.05, bz0 + 1.22, bz0 + 1.38, 1.24, 1.24, 0.18, 0.18, "yero_red_oxide", "yero_red_oxide")
    # Iron cross atop belfry
    mb.box(0.0, by, bz0 + 1.38, bz0 + 1.96, 0.07, 0.07, "steel_dark")
    mb.box(0.0, by, bz0 + 1.72, bz0 + 1.79, 0.42, 0.07, "steel_dark")

    add_meralco_side_meter_and_weatherhead(mb, w * 0.5, -1.5, z_eave, is_right_side=True)
    return mb


def build_bakery_oven(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    """Panaderya Corner Kiosk & Wood-Fired Brick *Pugon* Bakehouse (`brgy_bakery_oven.glb` ~ 4.8m x 4.0m x 4.45m, `min_y = 0`):
    recessed glass *estante* bread counter (`PANADERYA • PANDESAL`), arched brick *pugon* oven dome,
    sheet-metal exhaust hood, firewood rack, and tall brick/GI chimney stack."""
    mb = MeshBuilder(uvs)
    w, d = 4.3, 3.4
    hy = d * 0.5
    z_p, z_eave = 0.26, 3.18

    mb.frustum_plinth(0.0, 0.0, 0.0, 0.16, z_p, w + 0.24, d + 0.24, w + 0.06, d + 0.06)
    _add_four_walls(
        mb, 0.0, 0.0, w, d, z_p, z_eave,
        sw_front="chb_ochre_moss",
        sw_right="adobe_stone",
        sw_rear="chb_plaster_cracked",
        sw_left="chb_raw_moss",
        ops_front=[Opening(0.40, 3.90, z_p, 2.52, 0.66, "store_panaderya")],
        ops_left=[Opening(1.1, 1.75, 2.25, 2.65, 0.15, "bath_vent")],
        ops_rear=[Opening(0.85, 1.80, z_p, 2.24, 0.18, "door_rear")],
    )
    mb.cornice_ring(0.0, 0.0, z_eave - 0.14, 0.16, w, d, proj=0.12)

    # Front striped awning + unobstructed front-eave rooftop "PANADERYA • PANDESAL" signboard!
    add_storefront_awning(mb, 0.0, -hy, 2.54, 3.85, proj=0.98, drop=0.32, swatch="trapal_orange")
    add_rooftop_eave_signboard(mb, 0.0, -hy - 0.42, z_eave - 0.08, height=0.76, width=3.75, swatch="sign_panaderya")

    # Side Brick Wood-Fired *Pugon* Oven Annex (`+X` side, `Z = 0.0..4.42m` with chimney!)
    ox = w * 0.5 + 0.52
    mb.box(ox, 0.15, 0.0, 1.02, 1.04, 1.85, "adobe_stone", "concrete_pillar")
    # Firewood storage logs stacked under pugon base
    for lz in (0.14, 0.30):
        for ly in (-0.28, 0.0, 0.28):
            mb.tube((ox - 0.38, 0.15 + ly, lz), (ox + 0.44, 0.15 + ly, lz), 0.065, 0.060, 6, "wood_coco")
    # Vaulted red-brick *pugon* baking dome + arched iron oven mouth + sheet-metal smoke hood
    mb.frustum_plinth(ox, 0.15, 1.02, 1.48, 1.76, 1.02, 1.78, 0.64, 1.24, "tile_terracotta", "tile_terracotta")
    mb.box(ox, -0.76, 1.06, 1.48, 0.54, 0.12, "steel_dark")  # cast-iron oven door on front of pugon
    mb.box(ox, -0.78, 1.48, 1.72, 0.66, 0.26, "zinc_brushed")  # sheet-metal smoke hood
    # Tall brick chimney base + galvanized steel flue pipe + rain cap (`H = 4.42m`)!
    mb.box(ox, 0.38, 1.72, 2.95, 0.46, 0.46, "tile_terracotta", "concrete_pillar")
    mb.tube((ox, 0.38, 2.95), (ox, 0.38, 4.30), 0.13, 0.12, 8, "zinc_brushed")
    mb.frustum_plinth(ox, 0.38, 4.34, 4.40, 4.44, 0.42, 0.42, 0.08, 0.08, "zinc_brushed", "zinc_brushed")

    add_gable_yero_roof(
        mb, 0.0, 0.0, z_eave, w, d, rise=0.92, ov_x=0.34, ov_y=0.44,
        roof_swatch="yero_red_oxide", wall_swatch="chb_ochre_moss", downspout_corners=[(-1, 1)],
    )
    add_meralco_side_meter_and_weatherhead(mb, -w * 0.5, -0.95, z_eave, is_right_side=False)
    return mb


def build_poso_well(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    """Barrio cast-iron pitcher hand pump (*poso*) on mossy octagonal apron (`~1.85m x 1.85m x 1.68m`)."""
    mb = MeshBuilder(uvs)

    # Grounded mossy octagonal concrete well apron (`Z = 0.000`) + raised drainage rim
    mb.tube((0.0, 0.0, 0.0), (0.0, 0.0, 0.16), 0.92, 0.88, 8, "concrete_moss")
    mb.tube((0.0, 0.0, 0.16), (0.0, 0.0, 0.34), 0.34, 0.26, 8, "concrete_pillar")

    # Ribbed cast-iron pump column + flanged cylinder head + curved water spout
    mb.tube((0.0, 0.0, 0.34), (0.0, 0.0, 1.18), 0.105, 0.088, 8, "yero_green_faded")
    mb.tube((0.0, 0.0, 1.18), (0.0, 0.0, 1.42), 0.145, 0.135, 8, "yero_green_faded")
    mb.tube((0.0, -0.12, 1.10), (0.0, -0.38, 0.98), 0.052, 0.042, 6, "steel_rusted")

    # Pivoted long iron pump handle angled upward (`+Y`)
    mb.tube((0.0, 0.06, 1.38), (0.0, 0.72, 1.68), 0.028, 0.020, 6, "steel_dark")
    mb.tube((0.0, 0.0, 1.42), (0.0, 0.0, 1.54), 0.022, 0.022, 5, "zinc_brushed")

    # Blue plastic water bucket (*timba*) resting under spout
    mb.tube((0.0, -0.42, 0.16), (0.0, -0.42, 0.52), 0.15, 0.19, 8, "water_drum_blue")
    return mb


def build_banderitas(uvs: Dict[str, Tuple[float, float, float, float]]) -> MeshBuilder:
    """12m overhead fiesta catenary bunting (`Z = 4.60..5.42m`, `ground_at_zero=False`)."""
    mb = MeshBuilder(uvs)
    span_x = 11.8
    n_flags = 20
    swatches = ["team_red", "hazard_curb", "trapal_blue", "trapal_orange", "yero_green_faded"]

    for strand, (y_s, y_e, sag) in enumerate([(-0.45, 0.45, 0.52), (0.45, -0.45, 0.44)]):
        pts: List[Vector] = []
        for i in range(n_flags + 1):
            t = i / n_flags
            px = -span_x * 0.5 + t * span_x
            py = y_s + t * (y_e - y_s)
            pz = 5.38 - sag * 4.0 * t * (1.0 - t)
            pts.append(Vector((px, py, pz)))

        for i in range(n_flags):
            p0, p1 = pts[i], pts[i + 1]
            mb.tube(p0, p1, 0.012, 0.012, 4, "wood_coco", cap_start=False, cap_end=False)
            pm = (p0 + p1) * 0.5
            tip = pm + Vector((0.03 * math.sin(i + strand), 0.04 * (1 if i % 2 == 0 else -1), -0.34))
            sw = swatches[(i + strand * 2) % len(swatches)]
            # Double-sided triangular pennant so visible from both street directions
            mb.tri(p0, p1, tip, sw)
            mb.tri(p1, p0, tip, sw)
    return mb


# ---------------------------------------------------------------------------
# Entrypoint
# ---------------------------------------------------------------------------

def main() -> None:
    t0 = time.time()
    print("[BarangayKitV3] Generating 2048x2048 BarangayAtlas.jpg + BarangayAtlas_normal.png...")
    uvs = generate_barangay_atlas()

    jobs = [
        ("brgy_sari_sari", lambda: build_sari_sari(uvs), True),
        ("brgy_shophouse_row", lambda: build_shophouse_row(uvs), True),
        ("brgy_tabi_cluster_a", lambda: build_tabi_cluster_a(uvs), True),
        ("brgy_tabi_cluster_b", lambda: build_tabi_cluster_b(uvs), True),
        ("brgy_house_two_storey", lambda: build_house_two_storey(uvs), True),
        ("brgy_hall_plaza", lambda: build_hall_plaza(uvs), True),
        ("brgy_kapilya", lambda: build_kapilya(uvs), True),
        ("brgy_bakery_oven", lambda: build_bakery_oven(uvs), True),
        ("brgy_covered_court", lambda: build_covered_court(uvs), True),
        ("brgy_waiting_shed", lambda: build_waiting_shed(uvs), True),
        ("brgy_palengke_stall", lambda: build_palengke_stall(uvs), True),
        ("brgy_footbridge", lambda: build_footbridge(uvs), True),
        ("brgy_sorbetes_cart", lambda: build_sorbetes_cart(uvs), True),
        ("brgy_meralco_post", lambda: _build_utility_pole(uvs, height=13.6, team_swatch=None), True),
        ("brgy_post_blue", lambda: _build_utility_pole(uvs, height=10.5, team_swatch="team_blue"), True),
        ("brgy_post_red", lambda: _build_utility_pole(uvs, height=10.5, team_swatch="team_red"), True),
        ("brgy_cover_wall", lambda: build_cover_wall(uvs), True),
        ("brgy_poso_well", lambda: build_poso_well(uvs), True),
        ("brgy_banderitas", lambda: build_banderitas(uvs), False),
    ]

    for name, fn, grounded in jobs:
        mb = fn()
        out_path = OUT_DIR / f"{name}.glb"
        st = finalize_and_export_glb(
            mb,
            out_path,
            albedo_filename=ALBEDO_FILE,
            normal_filename=NORMAL_FILE,
            orm_filename=ORM_FILE,
            ground_at_zero=grounded,
            center_xy=True,
        )
        wh_str = ""
        if st.get("weatherheads"):
            wh_fmt = ", ".join(f"Vector3({x:.3f}, {y:.3f}, {z:.3f})" for (x, y, z) in st["weatherheads"])
            wh_str = f" | weatherhead={wh_fmt}"
        print(
            f"  [OK] {name}.glb | tris={st['tris']:4d} | "
            f"size=({st['w']:.2f} x {st['d']:.2f} x {st['h']:.2f}m) | min_y={st['min_y']:.4f}{wh_str}"
        )

    print(f"[BarangayKitV3] Built {len(jobs)} models + normal+ORM atlas in {time.time() - t0:.2f}s.")


if __name__ == "__main__":
    main()
