# Kalyeah Overhaul v2 — Barangay Map, Navigation, Wiring & 90s Province Landmarks Audit

- **Auditor**: `audit-map` (Read-Only Domain Audit Specialist)
- **Date**: 2026-11-12
- **Audited Slices**: `@barangay-map` (`#85`, `#102`, `#117`), `@province-props` (`#100`), `@arch-finish` / `@env-kit` (`#110`, `#114`), and `@arcane-fx` ground/water shader lane masks (`#88`)
- **Final Score**: **9.9 / 10 — PASS** (`>= 9.5 / 10` gate satisfied; `0` open defects)

---

## 1. Executive Scorecard

| Category | Weight | Score | Verdict |
| :--- | :---: | :---: | :--- |
| **1. Circular `84.0 m` 3-Pathway + 2-Alleyway Topology & Symmetry** | 20% | **10.0 / 10** | PASS — `14` `+Z` / `28` mirrored obstacles, `0` collider overlaps, `13.2 / 14.0 / 13.2 m` N–S lanes, `10.2 m` E–W alleys |
| **2. Walkable Shallow Mid Canal (`Z = 0`, Zero Mid Bridge)** | 20% | **10.0 / 10** | PASS — `water_obstacles = []`, `15.20 m` clear canal corridor, `0` bridge at `X = 0`, 3 sloped *Tabing-Ilog* wading ramps |
| **3. `BarangayNav` Deterministic A\* Connectivity & Sightlines** | 15% | **10.0 / 10** | PASS — `233 / 233` single connected component, direct wading across `X = {-34, 0, +34}`, `0` `randf()` calls |
| **4. Dynamic Perimeter Skyline & Overhead `SpaghettiWires` (`>= 4.5 m`)** | 15% | **9.9 / 10** | PASS — `76` perimeter houses (`r > 84 m`) + mossy CHB walls + 3 tree species; `43,428` batched wire verts at `Y >= 4.55 m` |
| **5. Authentic 90s Province Landmarks & 28-Model Watertight `.glb` Kit** | 20% | **9.9 / 10** | PASS — `28 / 28` `.glb`s (`408..6,436` tris, `0` open edges), sunlit `ProvincePropsAtlas.jpg` + `BarangayAtlas.jpg`, `#117` grounded posters |
| **6. GDScript Static Typing (`treat_warnings_as_errors`) & Test Alignment** | 10% | **10.0 / 10** | PASS — `0` `:=`, `0` untyped `var`/`for`/params/returns across `2,824` LOC; `test_map_layout`, `test_street_props`, `test_barangay_5v5` aligned |
| **Weighted Total** | **100%** | **9.93 / 10 (`9.9 / 10`)** | **APPROVED FOR MERGE** |

---

## 2. Spatial Topology, Colliders & Pathway Widths (`data/rules/map_layout.tres`, `scripts/sim/map_layout.gd`)

- **Circular Arena Bounds**:
  - `arena_radius = 84.0 m` (`168.0 m` playable diameter; `lane_width = 174.0 m`, `lane_length = 174.0 m`).
  - Maximum obstacle corner distance from origin: **`80.03 m <= 84.00 m`** (`3.97 m` perimeter sidewalk buffer inside the `84.0 m` boundary).
- **180° Point-Symmetric Collider Matrix**:
  - `14` authored `+Z` obstacles (`obstacle_rects()` produces `28` mirrored `Rect2` footprints via `Rect2(-rect.end, rect.size)`).
  - **Pairwise obstacle overlaps**: **`0`** (`minimum obstacle-to-obstacle clearance = 2.20 m`).
  - **Zone & Objective clearance**: **`0`** overlaps with `base_rect(SIDE_OWN)` (`0, +64`), `base_rect(SIDE_ENEMY)` (`0, -64`), `rescue_rect(SIDE_OWN)` (NW), `rescue_rect(SIDE_ENEMY)` (SE), or `powerup_spots` (`[(-34, 0), (0, 0), (+34, 0)]`).
- **Verified Clear Corridor Widths**:
  - **West Main N–S Pathway (`X = -34.0 m`, *Tabing-Ilog / Karinderya / Tindahan ni Aling Nena*)**: **`13.20 m`** clear width (`x in [-40.60, -27.40]`).
  - **Center Main N–S Pathway (`X = 0.0 m`, *Plaza – Walkable Shallow Canal – Basketball Court*)**: **`14.00 m`** clear width (`x in [-7.00, +7.00]`).
  - **East Main N–S Pathway (`X = +34.0 m`, *TODA Terminal / Barberya / Vulcanizing Shop*)**: **`13.20 m`** clear width (`x in [+27.40, +40.60]`).
  - **North Connecting E–W Alleyway (`Z = -26.0 m`)**: **`10.20 m`** clear width (`z in [-31.20, -21.00]`).
  - **South Connecting E–W Alleyway (`Z = +26.0 m`)**: **`10.20 m`** clear width (`z in [+21.00, +31.20]`).
  - **Central Walkable Shallow Canal Promenade (`Z = 0.0 m`)**: **`15.20 m`** clear width (`z in [-7.60, +7.60]`).

---

## 3. Walkable Mid Canal & `BarangayNav` Deterministic A\* Verification (`scripts/sim/barangay_nav.gd`)

- **Shallow Walkable Canal (`0` Mid Bridge)**:
  - `water_obstacles = Array[Rect2]([])` in `data/rules/map_layout.tres`; `LAYOUT.water_rects()` returns `0` blocking rects, and `LAYOUT.walk_obstacle_rects()` equals the `28` solid building/cover footprints.
  - `StreetMap._build_estero_creek()` renders a continuous shallow turquoise-jade `EsteroWater` surface (`Z in [-3.7, +3.7]`), sloped mossy *Tabing-Ilog* embankments, and **3 wide `12.6 m` river-cobble wading ramps** at `X = {-34.0, 0.0, +34.0}`.
  - **Zero mid bridge at `X = 0`**: Visual river-mouth footbridges (`RiverMouthFootbridgeWest`, `RiverMouthFootbridgeEast`) sit strictly at the outer perimeter canal mouths (`X = +-81.2 m`).
  - `shaders/barangay_ground.gdshader` and `shaders/estero_water.gdshader` align 1:1 with `X = {-34, 0, +34}` wading fords, `Z = +-26` E–W alleys, and carved stone rondels beneath the 3 Merienda power-up spots (`(-34, 0)`, `(0, 0)`, `(+34, 0)`).
- **`BarangayNav` Graph Audit**:
  - Deterministic candidate seeding (pathway/alley/canal-ford intersections + `4`-corner inflated obstacle offsets) generates **`233` valid waypoints**.
  - **Graph connectivity**: **`233 / 233` (`100%`)** reachable in a single connected component from `waypoints[0]`.
  - **Direct Wading & Alley Sightlines**:
    - `is_clear((-34, 6), (-34, -6)) == true` (West ford)
    - `is_clear((0, 6), (0, -6)) == true` (Center mid canal — no bridge choke)
    - `is_clear((34, 6), (34, -6)) == true` (East ford)
    - `is_clear((0, 60), (0, -60)) == true` (South Court base to North Plaza base)
    - `is_clear((-70, +-26), (70, +-26)) == true` (North & South E–W alleyways)
  - Custom deep-water `water_obstacles` detour logic remains 100% intact when non-empty `water_obstacles` are injected in unit tests.

---

## 4. Dynamic Perimeter, Overhead `SpaghettiWires` & Volumetric *Sampayan* (`scripts/view/street_map.gd`, `scripts/view/spaghetti_wires.gd`)

- **Dynamic Outer Perimeter (`_build_perimeter_skyline`)**:
  - Inner boundary ring (`r = 85.45 m`) alternates mossy Cement Hollow Block walls (`brgy_chb_wall.glb`, including stenciled `"BAWAL UMIHI DITO"` bays), lush Philippine provincial trees (`brgy_mango_tree.glb`, `brgy_coconut_palm.glb`, `brgy_banana_clump.glb` at `r = 88.2..90.6 m`), and varied provincial houses (`r = 90.0..106.0 m`), leaving clean openings at canal mouths, rescue sheds, and `NO ENTRY` road gates.
  - Populates `far_props.size() == 76` (`>= 70` floor in `test_street_props.gd`) and `house_rows[-1].size() == 38`, `house_rows[1].size() == 38` (`> 20` per side, `100%` strictly `r > 84.0 m`).
- **Overhead Wiring Height & Draw-Call Budget**:
  - `SpaghettiWiring` batches `71` primary catenary spans (`3` strands each), `20` pole slack coils, `72` cross-alley utility rungs + brackets, and `28` building service drops into **`1` `ArrayMesh` surface** (`SHADOW_CASTING_SETTING_OFF`) totaling **`43,428` vertices** (`> 6,000` floor in `test_street_props.gd`).
  - `SpaghettiWires.MIN_WIRE_Y = 4.55 m` clamps every wire quad, catenary sag point, slack loop, and wall bracket strictly **`>= 4.55 m`** (exceeding the `>= 4.50 m` clearance requirement). Fiesta `brgy_banderitas.glb` spans clear `Y = 4.59 m`.
- **True 3D Volumetric Hanging Clothes (*Sampayan*)**:
  - Zero flat 2D laundry quads: combines watertight `brgy_sampayan_3d.glb` (`2,534` tris, `0` open edges, `1.11 × 1.18 × 2.93 m` with 3D puffed tee, *sando*, 2-legged *puruntong* shorts, fluted *daster*, folded towel, wooden *sipit* pins, and *kalo* pulleys at `Y >= 4.65 m`) on eave-to-eave spans with `SpaghettiWires.append_volumetric_sampayan_span` (`rounded_slab` + `append_3d_tapered_tube` sleeves/legs/collars) on cross-spans (`0` duplicate span overlap verified in `#102`).

---

## 5. Complete 90s Province Landmarks & 28-Model `.glb` Kit Audit (`assets/models/barangay/`, `scripts/view/barangay_props.gd`)

All **28** `.glb` files in `assets/models/barangay/` were binary-parsed and verified:
- **100% Single-Surface & Mobile Budget Compliant**: Every `.glb` has `meshes == 1`, `primitives == 1`, **`408 <= tris <= 6,436`** (all `28/28` strictly `<= 6,500` tris), **`0` open edges** (watertight 3D manifolds), `0` UV OOB, baked `COLOR_0` vertex AO, and grounded `min_y = 0.000` (`3.913 m` overhead on `brgy_banderitas.glb`).
- **Cross-Owner Non-Overwrite Invariant Verified**: `brgy_jeepney.glb` (`4,200` tris) and `brgy_tricycle.glb` (`1,800` tris) reference `ProvincePropsAtlas.jpg` and were not overwritten by `build_barangay_kit.py` (`18` architectural exports).
- **Sunlit Atlases & VRAM `.import` Sidecars**:
  - `ProvincePropsAtlas.jpg` (`2048x2048`, `mean_sRGB = 0.4398`, `max_sRGB = 0.7490 <= 0.78`) + `ProvincePropsAtlas.jpg.import` (`compress/mode=2`, `mipmaps/generate=true`, `"vram_texture": true`, `0` `md5=` lines).
  - `BarangayAtlas.jpg` (`2048x2048`, `mean_sRGB = 0.4504`, `max_sRGB = 0.7608 <= 0.78`) + `BarangayAtlas.jpg.import` (`compress/mode=2`, `mipmaps/generate=true`, `"vram_texture": true`, `0` `md5=` lines).

### Verified 90s Philippine Province Landmarks Checklist

| Required Landmark | 3D Asset / Vignette | World Placement & Verification |
| :--- | :--- | :--- |
| **Provincial Jeepneys** | `brgy_jeepney.glb` (`4,200` tris, `"POBLACION / TABING-ILOG"`) | `ProvincialJeepney_0..3` at `(+-69.5, +-14.5)` bays & `(+-34.0, +-86.8)` road gates |
| **Tricycles & TODA Terminal** | `brgy_toda_shed.glb` (`1,736` tris) + `brgy_tricycle.glb` (`1,800` tris) | Obstacle `[4]` (`toda_shed`), `TodaTerminalEast/West` `(+-53.5, +-21.6)`, `TodaTricycle_0..5` |
| **Nena's Sari-Sari Store** | `brgy_sari_sari.glb` (`2,236` tris, `"TINDAHAN NI ALING NENA"`) + vignette | Obstacles `[2]` & `[13]` + sidewalk counter/cooler/sign at `(+-27.55, -+12.60)` |
| **Karinderya / Eatery** | `brgy_shophouse_row.glb` Bay 1 (`"KARINDERYA LUTONG BAHAY"`, 4 *kaldero* pots) + vignette | Obstacles `[3]` & `[6]` + turo-turo counter/pots facing West `-34 m` lane at `(+-40.48, -+14.20)` |
| **Barbershop (*Barberya*)** | `brgy_shophouse_row.glb` Bay 2 (`"BARBERYA GUPIT POGI"`, 3D barber pole) + vignette | Obstacles `[3]` & `[6]` + 3D striped barber pole facing East `+34 m` lane at `(+-27.55, +-11.20)` |
| **Vulcanizing Shop** | `brgy_shophouse_row.glb` Bay 3 (`"VULCANIZING SHOP"`, 3D hanging/stacked tires, air tank) + vignette | Obstacles `[3]` & `[6]` + stacked/leaning tires & air tank at `(+-27.55, +-17.60)` |
| **Tabing-Ilog Riverbank & Trees** | `EsteroWater`, stone wading ramps, `brgy_mango_tree`, `brgy_banana_clump`, `brgy_coconut_palm` | `Z = 0` shallow canal + `BarangayTree_0..11` + `PerimeterTree_*` |
| **Wooden Basketball Posts** | `brgy_wooden_hoop.glb` (`1,624` tris, tire/CHB base, coco-lumber post, 3D net) + `brgy_covered_court.glb` | `CoveredBasketballCourt` at `(0, +64)` + `WoodenBasketballPostSouth/North` at `(+-10.6, +-56.4)` |
| **Election & Siphoning Pole Posters** | `brgy_meralco_post.glb` + grounded `brgy_poster_cluster.glb` (`"VOTE! HALALAN '95"`, `"MALABANAN SIPSIP POSO NEGRO"`) | `ElectricPole_0..39` + `PolePosterCluster_0..15` grounded flush on `Y = 0.0` (`#117` fix verified) |
| **NO ENTRY & BAWAL UMIHI DITO** | `brgy_chb_wall.glb` + `brgy_poster_cluster.glb` + 6 perimeter `NO ENTRY` wooden road barriers | `ChbPerimeterWall_*`, `brgy_shophouse_row` base plaques, and 6 road-mouth gates (`(+-34, +-77.8)`, `(+-80.6, -+26)`) |
| **3 Power-Up Objective Stands** | `brgy_powerup_stand.glb` (`1,328` tris, `"PALAMIG / TEKS TURON"`, jars, *bilao*, umbrella) | `PowerupStand_0..2` beside `layout.powerup_spots` `(-34, 0)`, `(0, 0)`, `(+34, 0)` |

---

## 6. Defects Caught & Resolved During Phase 2 Audit

1. **`test_street_props.gd` Walkable Canal Water Count Mismatch (`#92` -> Fixed in `#106`)**:
   - Flagged `assert_eq(water_nodes.size(), map.layout.water_rects().size())` where Overhaul v2 sets `water_obstacles = []` (`0` blocking rects) while rendering `1` visual `EsteroWater` plane. Aligned cleanly by `@qa-gatekeeper` in `tests/unit/test_street_props.gd:82`.
2. **Duplicate Procedural *Sampayan* / `Label3D` Overlap on New `.glb`s (`#100` -> Fixed in `#102`)**:
   - Verified `@barangay-map`'s `#102` update gating off fallback `Label3D`s where `brgy_toda_shed.glb` / `brgy_chb_wall.glb` carry baked Tagalog atlas plaques and skipping procedural `SpaghettiWires` laundry on `brgy_sampayan_3d.glb` spans.
3. **Floating `+2.2 m` `PolePosterCluster` Grounding Defect (`#115` -> Fixed in `#117`)**:
   - Caught `poster_node.position = spot + Vector3(0.0, 2.2, 0.0)` in `scripts/view/street_map.gd:1037` floating 16 grounded `brgy_poster_cluster.glb` mossy CHB bases & `NO ENTRY` posts `2.2 m` in mid-air, plus overlapping procedural pole posters. Resolved cleanly in `#117` (`_place_walkthrough(poster_node, spot, yaw)` on `Y = 0.0` + `has_poster_glb` guard).

---

## 7. GDScript Static Typing & Unit-Test Readiness Summary

- **100% Static Typing Verified (`treat_warnings_as_errors = true`)**:
  - `scripts/sim/map_layout.gd` (`244` lines): `0` `:=`, `0` untyped `var`/`for`/params/returns, `0` `rand*()`.
  - `scripts/sim/barangay_nav.gd` (`319` lines): `0` `:=`, `0` untyped `var`/`for`/params/returns, `0` `rand*()`.
  - `scripts/view/street_map.gd` (`1,621` lines): `0` `:=`, `0` untyped `var`/`for`/params/returns, `0` banned `test_ui_lint.gd` strings.
  - `scripts/view/spaghetti_wires.gd` (`399` lines): `0` `:=`, `0` untyped `var`/`for`/params/returns.
  - `scripts/view/barangay_props.gd` (`241` lines): `0` `:=`, `0` untyped `var`/`for`/params/returns; all `10` `PROVINCE_90S_KEYS` registered.
- **Unit Test Suites Verified Ready**: `tests/unit/test_map_layout.gd`, `tests/unit/test_street_props.gd` (`18` `BARANGAY_PROPS` all `408..4,240` tris `<= 6,500`), and `tests/unit/test_barangay_5v5.gd`.
