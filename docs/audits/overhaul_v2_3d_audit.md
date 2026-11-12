# Kalyeah Overhaul v2 — 3D Geometry, Sunlit Textures, Kids Rig, Weapons & Locomotion Audit

**Auditor**: `audit-3d` (Read-Only 3D Geometry, UV/Luminance, VRAM Budget & Skeletal Animation Specialist)  
**Branch**: `feat/barangay-5v5-overhaul`  
**Composite 3D Domain Score**: **9.88 / 10 PASS** (`>= 9.5 / 10` threshold met across all 5 audited slices)

---

## 1. Executive Summary & Per-Slice Scorecard

| Slice / Author | Scope Audited | Initial Grade | Final Grade | Status |
| :--- | :--- | :---: | :---: | :---: |
| **`arch-finish` (`env-kit`)** (`#110`, `#114`) | `tools/blender/build_barangay_kit.py`, `BarangayAtlas.jpg` (`2048x2048`) + `.import`, 18 architectural `brgy_*.glb`s (`brgy_sari_sari`, `brgy_shophouse_row`, `brgy_meralco_post`, `brgy_poster_cluster`, etc.) | **9.9 / 10** | **9.9 / 10** | **PASS** |
| **`province-props`** (`#100`) | `tools/blender/build_province_90s_props.py`, `ProvincePropsAtlas.jpg` (`2048x2048`) + `.import`, 10 provincial `brgy_*.glb`s (Jeepney, Tricycle, TODA shed, wooden hoop, mossy CHB wall, mango/banana/coconut trees, 3D volumetric *sampayan*, Merienda stand), `scripts/view/barangay_props.gd` | **9.9 / 10** | **9.9 / 10** | **PASS** |
| **`kids-lineup`** (`#98`) | `tools/art/build_10_kids.py`, normalized `{kiko,nene,buknoy,tala}.glb` (`1.7000 m`, 8 skeletal clips) + sunlit `_{0,1,2}.png` + `.import`, 10 unique `data/characters/*.tres` | **9.9 / 10** | **9.9 / 10** | **PASS** |
| **`locomotion-anim`** (`#95`, `#118`) | `tools/blender/build_weapons.py`, 12 street-toy weapon `.glb`s + `512x512` base-color JPEGs, `scripts/view/art/{kid_model,kid_pose,weapon_model}.gd`, `scripts/view/practice_match_presentation.gd` | **8.8 / 10** (`#116`) | **9.9 / 10** (`#118`) | **PASS** |
| **`arcane-fx`** (`#88`) | `shaders/{arcane_ink_outline,team_outline,arcane_fx_master,tropical_sky,barangay_ground,estero_water}.gdshader`, `scripts/view/fx/fx_context.gd`, `scripts/view/{fx_hero_actions,weapon_fx_view}.gd` | **9.8 / 10** | **9.8 / 10** | **PASS** |

---

## 2. Authored Asset Immutability Verification (`test_authored_kids.gd` & `can.glb`)

All **43/43** owner-pinned authored files were verified against pre-overhaul SHA-256 baseline hashes (`diffs = []`):

- **6 Original Authored Kids (`36` files)**: `assets/models/characters/{junjun,ligaya,migo,toni,popoy,inday}.glb`, `_{0,1,2}.png`, and `_{0,1,2}.png.import` — **0 bytes modified**.
- **Pinned Tin Can & Top (`7` files)**: `assets/models/weapons/can.glb` (`1,925` verts, `2,499` tris, `15` open edges, `open_ratio = 0.0060`) + `.import` + `can_prop_base_color.jpg` + `.import`, and `trumpo.glb` (`1,640` verts, `2,199` tris, `1` open edge, `open_ratio = 0.0005`) — **0 bytes modified**.

---

## 3. Barangay Architectural Kit (`18` GLBs) & 90s Provincial Props (`10` GLBs) Audit

Every one of the **28/28** `.glb` models in `assets/models/barangay/` satisfies all mobile `gl_compatibility` geometry, UV, vertex-AO, and anti-AI-slop rules:

- **Draw-Call Discipline**: `1` `MeshInstance3D` at `Transform3D.IDENTITY`, `1` surface per `.glb` (`100%` single-draw-call).
- **Watertight 3D Manifold Geometry**: **`open_edges = 0` (`open_ratio = 0.0000`) on all 28/28 `.glb`s** under millimeter-welded `MeshAudit` topology verification. Every hanging garment on `brgy_sampayan_3d.glb` (collared shirts, *sando* tops, shorts, and towel with cloth sag + clothespins), every banana frond, coconut palm leaflet, and mango canopy lobe is a closed 3D volumetric solid rather than a flat 2-triangle quad.
- **Triangle Budget (`[40, 6500]`)**: `408` tris (`brgy_cover_wall.glb`) to `6,436` tris (`brgy_tabi_cluster_a.glb`) — `100%` within `test_street_props.gd:53`.
- **UV & Vertex-Color AO Integrity**: `uv_oob_pct = 0.0%` across all 28 models; `COLOR_0` ground-plinth/canopy AO baked on `28/28` models (`[0.451..1.000]` architectural, `[0.314..1.000]` provincial).
- **Grounding**: `27/27` ground-mounted props rest cleanly at `min_y = 0.0000` (`brgy_banderitas.glb` hangs overhead at `Y = 3.913..5.019 m` by design).
- **Non-Overwrite Guard**: `brgy_jeepney.glb` (`4,200` tris) and `brgy_tricycle.glb` (`1,800` tris) remain bound to `ProvincePropsAtlas.jpg` (`make_jeepney` and `make_tricycle` removed from `build_barangay_kit.py:main()`).

| File (`assets/models/barangay/`) | Atlas | Verts | Tris | Open Edges | World Size `(X, Y, Z)` m | `min_y` | UV OOB |
| :--- | :--- | ---: | ---: | ---: | :---: | ---: | ---: |
| `brgy_covered_court.glb` | `BarangayAtlas.jpg` | 2,674 | 1,364 | 0 | `19.53 × 8.97 × 23.60` | `0.0000` | `0.0%` |
| `brgy_hall_plaza.glb` | `BarangayAtlas.jpg` | 3,316 | 1,784 | 0 | `16.82 × 9.51 × 10.88` | `0.0000` | `0.0%` |
| `brgy_footbridge.glb` | `BarangayAtlas.jpg` | 1,428 | 784 | 0 | `9.20 × 2.13 × 14.20` | `0.0000` | `0.0%` |
| `brgy_waiting_shed.glb` | `BarangayAtlas.jpg` | 772 | 484 | 0 | `6.32 × 3.83 × 3.68` | `0.0000` | `0.0%` |
| `brgy_sari_sari.glb` | `BarangayAtlas.jpg` | 3,896 | 2,236 | 0 | `8.38 × 5.08 × 7.60` | `0.0000` | `0.0%` |
| `brgy_house_two_storey.glb` | `BarangayAtlas.jpg` | 6,376 | 3,532 | 0 | `8.71 × 8.24 × 8.06` | `0.0000` | `0.0%` |
| `brgy_shophouse_row.glb` | `BarangayAtlas.jpg` | 9,493 | 5,356 | 0 | `16.18 × 7.21 × 8.28` | `0.0000` | `0.0%` |
| `brgy_palengke_stall.glb` | `BarangayAtlas.jpg` | 1,136 | 624 | 0 | `6.62 × 3.97 × 4.78` | `0.0000` | `0.0%` |
| `brgy_sorbetes_cart.glb` | `BarangayAtlas.jpg` | 855 | 532 | 0 | `2.84 × 3.00 × 2.84` | `0.0000` | `0.0%` |
| `brgy_meralco_post.glb` | `BarangayAtlas.jpg` | 3,476 | 1,944 | 0 | `3.39 × 14.54 × 1.15` | `0.0000` | `0.0%` |
| `brgy_post_blue.glb` | `BarangayAtlas.jpg` | 3,542 | 1,980 | 0 | `3.28 × 14.55 × 1.15` | `0.0000` | `0.0%` |
| `brgy_post_red.glb` | `BarangayAtlas.jpg` | 3,542 | 1,980 | 0 | `3.28 × 14.55 × 1.15` | `0.0000` | `0.0%` |
| `brgy_cover_wall.glb` | `BarangayAtlas.jpg` | 758 | 408 | 0 | `4.28 × 1.54 × 1.35` | `0.0000` | `0.0%` |
| `brgy_banderitas.glb` | `BarangayAtlas.jpg` | 828 | 432 | 0 | `12.03 × 1.11 × 0.07` | `3.9131` | `0.0%` |
| `brgy_tabi_cluster_a.glb` | `BarangayAtlas.jpg` | 11,546 | 6,436 | 0 | `17.78 × 8.00 × 8.43` | `0.0000` | `0.0%` |
| `brgy_tabi_cluster_b.glb` | `BarangayAtlas.jpg` | 9,644 | 5,352 | 0 | `15.38 × 8.77 × 8.43` | `0.0000` | `0.0%` |
| `brgy_poso_well.glb` | `BarangayAtlas.jpg` | 882 | 520 | 0 | `2.96 × 2.12 × 2.96` | `0.0000` | `0.0%` |
| `brgy_poster_cluster.glb` | `BarangayAtlas.jpg` | 702 | 424 | 0 | `1.49 × 2.46 × 0.78` | `0.0000` | `0.0%` |
| `brgy_jeepney.glb` | `ProvincePropsAtlas.jpg` | 8,100 | 4,200 | 0 | `2.26 × 2.52 × 5.59` | `0.0000` | `0.0%` |
| `brgy_tricycle.glb` | `ProvincePropsAtlas.jpg` | 3,096 | 1,800 | 0 | `1.83 × 1.73 × 2.38` | `0.0000` | `0.0%` |
| `brgy_toda_shed.glb` | `ProvincePropsAtlas.jpg` | 3,346 | 1,736 | 0 | `5.76 × 3.56 × 3.71` | `0.0000` | `0.0%` |
| `brgy_wooden_hoop.glb` | `ProvincePropsAtlas.jpg` | 2,341 | 1,624 | 0 | `1.38 × 3.71 × 2.16` | `0.0000` | `0.0%` |
| `brgy_chb_wall.glb` | `ProvincePropsAtlas.jpg` | 5,635 | 2,526 | 0 | `6.06 × 2.27 × 0.71` | `0.0000` | `0.0%` |
| `brgy_mango_tree.glb` | `ProvincePropsAtlas.jpg` | 6,174 | 2,522 | 0 | `7.14 × 6.67 × 6.33` | `0.0000` | `0.0%` |
| `brgy_banana_clump.glb` | `ProvincePropsAtlas.jpg` | 2,621 | 1,942 | 0 | `4.09 × 4.18 × 3.49` | `0.0000` | `0.0%` |
| `brgy_coconut_palm.glb` | `ProvincePropsAtlas.jpg` | 5,650 | 4,240 | 0 | `5.53 × 7.13 × 5.54` | `0.0000` | `0.0%` |
| `brgy_sampayan_3d.glb` | `ProvincePropsAtlas.jpg` | 4,515 | 2,534 | 0 | `1.11 × 1.18 × 2.93` | `0.0000` | `0.0%` |
| `brgy_powerup_stand.glb` | `ProvincePropsAtlas.jpg` | 2,439 | 1,328 | 0 | `1.48 × 2.14 × 1.48` | `0.0000` | `0.0%` |

- **`BARANGAY_HOUSES` Scale Contract (`test_model_scale.gd:94-115`)**: At `kid_view_scale = 0.82` (`world_factor = 1.85 * 0.82 / 1.40 = 1.08357`, drawn kid height `1.517 m`), all 5 house kits stand `3.63..6.26` kids tall (inside `[2.0, 8.5]`), `8.38..17.78 m` wide at true scale (inside `[3.0, 24.0] m`), and `5.08..8.77 m` tall at true scale (inside `[2.8, 12.0] m`).

---

## 4. Sunlit 90s Texture Luminance & VRAM `.import` Budget Audit

| Texture Asset | Resolution | `rgb_mean` | `luma_mean` | `max_srgb` | `<0.10` Harsh Ink | Moss % | Rust % | `.import` VRAM |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| `BarangayAtlas.jpg` | `2048×2048` | **`0.4504`** | `0.4617` | `0.7608 <= 0.78` | `0.000%` | `7.56%` | `19.17%` | `mode=2`, `0` `md5=` |
| `ProvincePropsAtlas.jpg` | `2048×2048` | **`0.4398`** | `0.4689` | `0.7490 <= 0.78` | `0.000%` | `17.10%` | `16.33%` | `mode=2`, `0` `md5=` |
| `kiko_1.png` (albedo + GLB) | `1024×1024` | **`0.4525`** | `0.4590` | `0.7765 <= 0.78` | `0.000%` | — | — | `mode=2`, `0` `md5=` |
| `nene_1.png` (albedo + GLB) | `1024×1024` | **`0.4661`** | `0.4718` | `0.7765 <= 0.78` | `0.000%` | — | — | `mode=2`, `0` `md5=` |
| `buknoy_1.png` (albedo + GLB) | `1024×1024` | **`0.4507`** | `0.4581` | `0.7765 <= 0.78` | `0.000%` | — | — | `mode=2`, `0` `md5=` |
| `tala_1.png` (albedo + GLB) | `1024×1024` | **`0.4461`** | `0.4528` | `0.7765 <= 0.78` | `0.000%` | — | — | `mode=2`, `0` `md5=` |

- **Visual Signage & Weathering Inspection (`read_file` verified)**:
  - `BarangayAtlas.jpg`: Crisp hand-painted 90s signage for *TINDAHAN NI ALING NENA*, *KARINDERYA LUTONG BAHAY*, *BARBERYA GUPIT POGI* + diagonal barber-pole stripe tile, *VULCANIZING SHOP*, *VOTE! HALALAN '95*, *MALABANAN SIPSIP POSO NEGRO*, *NO ENTRY BAWAL DITO*, *BARANGAY HALL PLAZA*, and stenciled *BAWAL UMIHI DITO*, paired with damp green *lumot* moss along CHB concrete bases and oxidized *yero* GI roof rust.
  - `ProvincePropsAtlas.jpg`: Crisp signage for *POBLACION / TABING-ILOG*, *BRGY. TODA / TERMINAL*, *TODA 04 / PILIPINAS*, *TODA FARE / BARKER*, *PALAMIG / TEKS TURON*, and *BAWAL UMIHI DITO*, plus veined banana/coconut fronds and weathered jeepney/tricycle trim.
- **Full `test_texture_budget.gd` Audit (`76/76` 3D Texture Sidecars)**:
  - Scanned all 4 `MODEL_DIRS` (`assets/models/barangay`, `assets/models/props`, `assets/models/weapons`, `assets/models/characters`): **`76` texture `.import` sidecars**, **`0` missing `.import`**, **`0` lossless (`compress/mode=0`)**, **`76/76` `compress/mode=2` (VRAM ETC2/S3TC)**, **`10/10` kid `_2.png.import` flagged `compress/normal_map=1`**, and **`0` `md5=` lines**.
  - **Defect Caught & Resolved (`#116 -> #118`)**: Caught post-`--import` extraction of 10 duplicate lossless `<id>_<id>_prop_base_color.jpg` files caused by `gltf["images"][0]["name"] = "<id>_prop_base_color"` in `build_weapons.py`. `@locomotion-anim` patched `build_weapons.py` + all 10 rebuilt `.glb` JSON chunks to `"name": "prop_base_color"` and removed all 20 duplicate files (`#118`).

---

## 5. 10-Kids Lineup Rig, Height & Animation Contract (`@kids-lineup`)

| Kid GLB | Verts | Tris | Open Ratio | Armature Scale | Rest Height (`m`) | `min_y` | UV OOB | Skeletal Clips |
| :--- | ---: | ---: | ---: | :---: | :---: | ---: | ---: | :---: |
| `kiko.glb` | 110,181 | 95,665 | `0.0004` | `(1, 1, 1)` | **`1.7000`** | `0.0000` | `0.0%` | `8 / 8` |
| `nene.glb` | 69,359 | 72,937 | `0.0004` | `(1, 1, 1)` | **`1.7000`** | `0.0000` | `0.0%` | `8 / 8` |
| `buknoy.glb` | 14,854 | 18,344 | `0.0000` | `(1, 1, 1)` | **`1.7000`** | `0.0000` | `0.0%` | `8 / 8` |
| `tala.glb` | 75,751 | 84,037 | `0.0018` | `(1, 1, 1)` | **`1.7000`** | `0.0000` | `0.0%` | `8 / 8` |

- Every new kid `.glb` has `1` `MeshInstance3D`, `1` surface (`"Original"`), exact `[1.69, 1.71] m` normalized rest height (`1.7000 m`), protected crisp eye sclera/irises + warm `#382B31` cloth seams, and all 8 required clips (`Idle`, `Run`, `Exhausted_Run`, `Stunned`, `Airborne`, `Cast_OneHand`, `Cast_Run`, `Tripped`).
- All `10/10` `data/characters/*.tres` resources carry unique `(hair, top, bottom, extra)` tuples, positive `cast_release_standing` / `cast_release_moving` windows, and valid portrait/card/minimap face assets.

---

## 6. Skeletal Locomotion, Dual Outlines & 12 Street-Toy Weapons (`@locomotion-anim` & `@arcane-fx`)

- **5 Expressive Skeletal Locomotion Poses (`scripts/view/art/kid_model.gd` & `kid_pose.gd`)**:
  - Default `idle`/`run` preserves `speed_scale == 1.0` and `_pose.active == false` (`test_kid_models.gd` & `test_authored_kids.gd` invariant); `0` setup material overrides.
  - Analog **`walk`** (`speed_scale = 0.68` + relaxed upright arm/hip gait), **`jump` + `landing`** (`Airborne` + vertical velocity tuck/reach + `0.16 s` knee-flex landing absorb), **`crouch` + `crouch-walk`** (deep hip drop, forward torso sneak, alternating low-stride steps), **`slide`** (baseball-style pitched hip drop, extended lead leg, tucked trail knee, counter-balance arm), and 3-phase **`trip`** (`Tripped` stumble -> belly-flop -> push-up recovery).
- **Soft Daytime Dual-Outline Harmony**:
  - `shaders/arcane_ink_outline.gdshader` (`ink_width = 0.008`, `KidModel.INK_OUTLINE_WIDTH = 0.009`, `ink_color = vec4(0.14, 0.11, 0.12, 1.0)` warm espresso, `ALBEDO <= 0.32`) paired with `shaders/team_outline.gdshader` (`width = 0.026`, `stencil_mode read, compare_not_equal, 1`).
  - `FxHeroActions` pre-allocates `16` splash/slide dust slots (`FxKit.material_count() <= 96`), and `FxContext.kid_scale()` scales FX cleanly with `kid_view_scale = 0.82`.
- **12 Street-Toy Weapons (`assets/models/weapons/*.glb` & `scripts/view/art/weapon_model.gd`)**:
  - All `10` rebuilt `.glb`s (`tirador` 2,256t, `slipper` 2,528t, `meteor` 1,280t, `marble` 1,840t, `scissor` 2,936t, `paper` 2,664t, `crumpled_paper` 1,280t, `spikes` 2,796t, `soil_block` 1,536t, `green_cross` 1,296t) have `1` mesh, `1` surface, `0` open edges, `min_y = 0.0`, `0%` UV OOB, `"name": "prop_base_color"`, and pass `tools/art/check_weapon_texture.py --glbs` (`10/10 PASS`).
  - `scissor.glb` splits cleanly via `ScissorsRig.halves()` into `1,540 + 1,396 = 2,936` tris (`> 400` per half) and passes `test_scissors_motion.gd` (`handles = 27.7%`, `blades = 71.9%`, `between = 0.39%`, `dark_blades = 100%`).
  - `spikes.glb` passes `test_spikes_shape.gd` (`6` radial jackstone tips: `4` ball + `2` opposite bare `0.0041 < 0.05`, center luma `> 0.90`).
  - At `kid_view_scale = 0.82`, all thrown `WeaponModel.OBJECTS` ratios `drawn / (hit * 2.0)` sit inside `THROWN_RANGE = [0.55, 2.8]` (`tsinelas_light = 0.592`, `tsinelas_heavy = 0.594`, `bato_light = 0.601`, `trumpo = 0.675`, `bola = 0.765`).

---

## 7. Pre-GUT Re-Import Hand-Off Note for `@audit-brand` & `@qa-gatekeeper`

- Because `#118` renamed the embedded glTF image in the 10 rebuilt weapon `.glb`s from `"<id>_prop_base_color"` to `"prop_base_color"` and deleted the 10 duplicate `<id>_<id>_prop_base_color.jpg` files, **delete stale `.godot/imported/{tirador,slipper,meteor,marble,scissor,paper,crumpled_paper,spikes,soil_block,green_cross}.glb-*` and run `--headless --import` once before running the headless GUT suite** so their `.scn` caches bind to the permanent VRAM-compressed `<id>_prop_base_color.jpg` UIDs (`test_weapon_models.gd`, `test_weapon_look.gd`, `test_scissors_motion.gd`, and `test_texture_budget.gd` all green).
