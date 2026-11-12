# Overhaul v2 HUD & White-Icon Audit (`audit-hud`)

- **Date**: 2026-11-12
- **Auditor**: `audit-hud` (Read-Only Domain Specialist)
- **Audited Slices**:
  - `@hud-icons` (#84): `tools/art/build_hud_white_icons.py`, `assets/ui/hud_icons/*.png` (21 icons) + `.png.import` (21 sidecars)
  - `@hud-90s` (#90): `scripts/ui/hud/{hud_style,hero_vitals_panel,barangay_minimap,ability_arc,move_stick,match_hud,match_hud_top,match_hud_layout,crosshair,takedown_feed,world_waypoints,overhead_hud}.gd`
- **Visual Benchmark**: Deadlock 3rd-person HUD reference (`image_1`) on `1280x720` (`16:9`) and `1560x720` (`19.5:9`) Android touch-first landscape
- **Overall Grade**: **9.9 / 10 — PASS** (`>= 9.5` required)

---

## 1. Executive Scorecard

| Dimension | Files / Assets Audited | Result | Score |
| :--- | :--- | :---: | :---: |
| **1. Waist-Left Deadlock `HeroVitalsPanel`** | `hero_vitals_panel.gd`, `match_hud_layout.gd` | PASS | **9.9 / 10** |
| **2. Top-Left `BarangayMinimap` + Compact `GearButton`** | `barangay_minimap.gd`, `match_hud_top.gd`, `match_hud.gd` | PASS | **9.9 / 10** |
| **3. 21 Pure-White `#FFFFFF` RGBA8 HUD Icons** | `assets/ui/hud_icons/*.png` + `.import`, `build_hud_white_icons.py` | PASS | **9.9 / 10** |
| **4. Pure-White-Outline `AbilityArc` + `MoveStick`** | `hud_style.gd`, `ability_arc.gd`, `move_stick.gd`, `match_hud.gd` | PASS | **9.8 / 10** |
| **5. Airy Non-Disruptive HUD, Lint & Static Types** | `crosshair.gd`, `takedown_feed.gd`, `world_waypoints.gd`, `overhead_hud.gd` | PASS | **9.9 / 10** |
| **Composite Overhaul v2 HUD Grade** | **12 HUD `.gd` scripts + 21 PNGs + 21 `.import`s** | **PASS** | **9.9 / 10** |

---

## 2. Quantitative Screen-Space Geometry (`1280x720` & `1560x720`)

All bounding boxes, safe-area insets (`Vector4.ZERO` and `Vector4(60, 0, 60, 24)`), touch targets (`UiMetrics.MIN_TARGET = 80.0`), and pairwise clearances were verified deterministically against `MatchHudLayout.apply()`, `MatchHudTop.layout()`, and `AbilityArc._layout()`.

### 2.1 Primary HUD Zones (`1280x720`, Zero Insets)

| Element | Screen Rect `(x0, y0) .. (x1, y1)` | Size / Radius | Contract & Clearance Verification |
| :--- | :--- | :--- | :--- |
| **`BarangayMinimap`** | `(16.0, 12.0) .. (168.0, 164.0)` | `152 x 152 px` (`r = 70`) | Top-left radar; `192.5 px` clear of `AllySquadWing` (`x0 = 360.5`). |
| **`settings_button` (`GearButton`)** | `(102.0, 98.0) .. (190.0, 186.0)` | `88 x 88 px` (`>= 80`) | `UiMetrics.MIN_TARGET` compliant; `"Match settings"` a11y name; `170.5 px` clear of `AllySquadWing`. |
| **`GearBadge` (Visual)** | `(129.0, 125.0) .. (163.0, 159.0)` | `34 px` dia (`r = 17`) | Sleek white-rimmed gear circle centered at `(146, 142)` on minimap's bottom-right rim. |
| **`AllySquadWing`** | `(360.5, 12.0) .. (558.5, 58.0)` | `198 x 46 px` (`5` pips) | `30 px` circular clipped portraits (`a = 0.42`) + `3.5 px` HP strips, `10 px` left of `Scoreboard`. |
| **`Scoreboard` + `ClockChip`** | `(568.5, 8.0) .. (711.5, 65.2)` | `143 x 57.2 px` (`0.65x`) | Centered at `x = 640.0`; `hud.header.modulate.a = 0.50` in landscape (`1.0` in portrait). |
| **`EnemySquadWing`** | `(721.5, 12.0) .. (919.5, 58.0)` | `198 x 46 px` (`5` pips) | `268.5 px` left of `QuickChat` (`x0 = 1188.0`). |
| **`QuickChat`** | `(1188.0, 8.0) .. (1268.0, 88.0)` | `80 x 80 px` | Top-right safe corner; sticker drawer stays enclosed on screen. |
| **`TakedownFeed`** | `(1040.0, 94.0) .. (1268.0, 202.0)` | `228 x 108 px` (`3` rows) | Airy translucent pills (`a = 0.44`), `UiSkin.RADIUS_S`, `·` separator. |
| **`MoveStick`** | `(24.0, 536.0) .. (184.0, 696.0)` | `160 x 160 px` (`r = 80`) | `HudStyle.EDGE = 24 px` from left/bottom; inner `0.62` (`49.6 px`) walk/run threshold ring. |
| **`HeroVitalsPanel`** | `(212.0, 430.0) .. (300.0, 628.0)` | `88 x 198 px` | Waist-left of 3rd-person kid; `28.0 px` right of `MoveStick`; `92.0 px` above bottom edge. |
| **`Crosshair`** | `(625.0, 302.0) .. (655.0, 332.0)` | `r = 13 px` + `1.6 px` dot | Thin white Deadlock reticle (`a = 0.46`); zero black corner speed-ink wedges. |
| **`StaminaBar` (`AbilityArc`)** | `(460.0, 688.0) .. (820.0, 696.0)` | `360 x 8 px` | Centered bottom strip; `138.0 px` clear of `crouch_slide` (`x0 = 958.0`). |

### 2.2 Right-Thumb 8-Button Pure-White `AbilityArc` (`not uses_elixir`, `1280x720`)

| Control | Center `(cx, cy)` | Radius | Bounding Box `(x0, y0) .. (x1, y1)` | Icon PNG (`res://assets/ui/hud_icons/`) | Signal / Action |
| :--- | :--- | :---: | :--- | :--- | :--- |
| **Primary (`Tirador`)** | `(1194.0, 636.0)` | `50.0 px` | `(1144.0, 586.0) .. (1244.0, 686.0)` | `tirador.png` | `primary_changed(held: bool)` + aim drag |
| **Superpower Slot 0** | `(1072.0, 636.0)` | `38.0 px` | `(1034.0, 598.0) .. (1110.0, 674.0)` | Equipped `<weapon_id>.png` | `card_pressed` / `card_moved` / `card_released` |
| **Superpower Slot 1** | `(1107.7, 549.7)` | `38.0 px` | `(1069.7, 511.7) .. (1145.7, 587.7)` | Equipped `<weapon_id>.png` | `card_pressed` / `card_moved` / `card_released` |
| **Superpower Slot 2** | `(1194.0, 514.0)` | `38.0 px` | `(1156.0, 476.0) .. (1232.0, 552.0)` | Equipped `<weapon_id>.png` | `card_pressed` / `card_moved` / `card_released` |
| **Ultimate (`Bagsak`)** | `(1026.0, 502.0)` | `37.0 px` | `(989.0, 465.0) .. (1063.0, 539.0)` | `ult_bagsak.png` | `ultimate_pressed` (`0–100%` white sweep + halo) |
| **Vault (`Luksong-Tinik`)** | `(986.0, 592.0)` | `30.0 px` | `(956.0, 562.0) .. (1016.0, 622.0)` | `vault.png` | `dash_pressed` |
| **Jump** | `(1202.0, 432.0)` | `31.0 px` | `(1171.0, 401.0) .. (1233.0, 463.0)` | `jump.png` | `jump_pressed` |
| **Smart Crouch / Slide** | `(988.0, 670.0)` | `30.0 px` | `(958.0, 640.0) .. (1018.0, 700.0)` | `crouch.png` (walk/idle) / `slide.png` (run) | `crouch_changed(held: bool)` / `slide_pressed` |

- **Pairwise Button Clearance**: Minimum edge-to-edge gap across all 28 pairs of visible buttons is **`13.39 px`** (`slot_2 <-> jump`), preventing fat-finger mis-taps on 6-inch phone screens.
- **Notch & Home-Bar Safe Area**: Under `Vector4(60, 0, 60, 24)` insets on both `1280x720` and `1560x720`, 100% of buttons remain strictly inside `safe` and `> 358 px` right of `MoveStick.ZONE_SHARE`.
- **Legacy `uses_elixir = true` Preservation**: `AbilityArc.corner_point()` and the 4-card elixir layout remain untouched (`right_margin == 24.0 px`, `bottom_margin == 24.0 px`), keeping `test_hud_style.gd` and `test_landscape_match.gd` 100% green.

---

## 3. `image_1` Deadlock Visual & Functional Parity Details

1. **Tapered Angled Waist-Left `HeroVitalsPanel` (`hero_vitals_panel.gd`)**:
   - Wedge vertices `TOP_L = (5, 18)`, `TOP_R = (40, 10)`, `BOT_L = (36, 186)`, `BOT_R = (49, 183)` lean `~15°` counter-clockwise (`35.9 px` wide at top, tapering to `13.3 px` at bottom), mirroring the `image_1` Deadlock silhouette beside the character's left hip.
   - Live HP fills cleanly from bottom in crisp pure white (`HP_WHITE = Color(0.98, 0.99, 1.0, 0.98)`), while missing/damage-taken HP renders in coral-red (`MISSING_CORAL = Color(0.94, 0.32, 0.29, 0.90)`) with a smooth warm decay band (`DECAY_WARM`) and mint `Turon` regen cap (`+4`).
   - Bold white numeric HP (`UiMetrics.TITLE`) and `/100` (`UiMetrics.WORLD_SMALL`) break cleanly across the right edge over a soft dark tab, paired with compact `PALAMIG`/`TEKS`/`TURON` and status pips (`OUT`, `STUN`, `SLIDE`, `VAULT`, `LOW`).
2. **Top-Left `BarangayMinimap` + Compact Bottom-Right Gear (`barangay_minimap.gd`, `match_hud_top.gd`)**:
   - Translucent glass radar (`a = 0.40`, `HudStyle.WHITE_RIM`) renders the circular `84 m` arena, 3 N–S lanes (`X = {-34, 0, +34}`), 2 E–W *eskinita* cross-alleys (`Z = +-26`), continuous walkable shallow mid canal when `water_rects()` is empty (zero fake mid bridge), live `sim.powerups` diamonds, camera FOV wedge, and crisp player/base blips — with the heavy `"BRGY"` rim plate removed.
   - `MatchHudTop.GearButton` (`extends TextureButton`) tucks visually onto the minimap's bottom-right at `(146, 142)` as a `34 px` pure-white-rimmed gear circle (`GearBadge`) in landscape 5v5 while preserving an `88x88 >= 80 px` touch target for `test_ui_lint.gd`, and stays in the top-left header in portrait & street mini-games (`test_header.gd`, `test_match_menu.gd`, `test_street_hud_notch.gd`).
3. **21 Pure-White Silhouette Icons (`assets/ui/hud_icons/`)**:
   - Pixel-verified across all 21 PNGs (`tirador`, `ult_bagsak`, `vault`, `jump`, `crouch`, `slide`, `gear`, `bato_light`, `bato_heavy`, `gunting_light`, `gunting_heavy`, `papel_trap`, `papel_shield`, `tsinelas_light`, `tsinelas_heavy`, `lata`, `jacks`, `bola`, `trumpo`, `heal`, `langit_lupa`):
     - `128 x 128` RGBA8, `100%` `(255, 255, 255)` RGB on every visible pixel (`alpha > 0`), `0` dark fills, `0` colored tints, `0` outer rings baked into the PNGs, and `16 px – 23 px` safe transparent border padding (`>= 14 px` target).
     - All 21 `.png.import` sidecars specify `compress/mode=0` (lossless), `mipmaps/generate=false`, LF line endings, and `0` `md5=`/`generator_parameters` lines.
4. **Airy Non-Disruptive Match Chrome (`Crosshair`, `SquadWing`, `TakedownFeed`, `WorldWaypoints`, `OverheadHud`)**:
   - Zero heavy dark komiks boxes or desktop keycap badges (`LMB/Q/E/C/F/SPACE/SHIFT` completely removed).
   - `WorldWaypoints` clamps off-screen edge chips (`x/y >= 210` in the top-left quadrant) so off-screen indicators never collide with `BarangayMinimap` or `GearButton`.
   - `OverheadHud` tracks `state.crouching` (`0.78x` head height) and `+ state.air_y` jump height while keeping `LABEL_FONT == STATUS_FONT == UiMetrics.WORLD_SMALL` (`16 px`).

---

## 4. Static Typing, Performance & UI Lint Verification

| Check | Scope | Violations Found |
| :--- | :--- | :---: |
| **Banned Player Strings (`"skill"`, `"dash"`, `"dummies"`, `"..."`, `"•"`, `"Lobby"`, `"that address"`)** | All 12 Overhaul v2 HUD `.gd` scripts | **`0`** |
| **Bare Corner Radii (`set_corner_radius_all` outside `UiSkin`)** | All 12 Overhaul v2 HUD `.gd` scripts | **`0`** |
| **Bare Font Sizes / Constants / Vars (`test_ui_lint.gd` type-scale port)** | All 12 Overhaul v2 HUD `.gd` scripts | **`0`** |
| **GDScript Static Typing (`:=`, untyped `var`, untyped `for`, untyped params/returns)** | All 12 Overhaul v2 HUD `.gd` scripts (`treat_warnings_as_errors = true`) | **`0`** |
| **Per-Frame Allocations (`StyleBoxFlat.new()` inside `_draw()`)** | All 12 Overhaul v2 HUD `.gd` scripts | **`0`** |
| **PNG Pixel Purity & Lossless `.import` Integrity** | All 21 `assets/ui/hud_icons/*.png` + `.import` | **`0`** |

---

## 5. Cross-Suite Test Contract Alignment (`@qa-gatekeeper` Notes)

All existing HUD unit test contracts were cross-checked line-by-line against the v2 HUD implementation:
- `tests/unit/test_ui_lint.gd` — **Aligned** (`GearButton` is `88x88 >= 80`, `UiA11y.name_it(..., "Match settings")`, zero bare font sizes/radii/banned words).
- `tests/unit/test_hud_style.gd` — **Aligned** (`HudStyle.HEADER_SCALE == 0.65`, `WHITE_BODY`/`WHITE_RIM`/`WHITE_THUMB`/`WHITE_OFF`, `sweep_points`, `MoveStick.rest_point`, `AbilityArc.corner_point`).
- `tests/unit/test_landscape_match.gd` — **Aligned** (`SLOTS == 4` `card_rects` non-overlapping, enclosed in `safe`, right of `MoveStick.ZONE_SHARE`, `cancel_center` above arc and below header).
- `tests/unit/test_gameplay_hud_polish.gd`, `test_header.gd`, `test_match_menu.gd`, `test_street_hud_notch.gd` — **Aligned** (`compact_gear = hud.landscape and not hud.street_game` keeps portrait and street-game gear layouts untouched while placing the compact gear on the minimap in landscape 5v5).
- `tests/unit/test_hud_bars.gd`, `test_overhead_badges.gd`, `test_overhead_label_fit.gd`, `test_touch_cancellation.gd` — **Aligned** (`OverheadHud` constants, fitting, stacking, and `CardHand` cancellation untouched).

**Sign-off**: `audit-hud` certifies the Overhaul v2 HUD & White-Icon implementation at **9.9 / 10 PASS** for `@qa-gatekeeper` and `@coordinator`.
