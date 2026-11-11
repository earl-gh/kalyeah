# Kalyeah — AAA Barangay Model, Art & Production Quality Specification

> **Status**: Binding Studio Production Standard (Pass 1 -> Pass N Iterative Audit Loop)  
> **Target Benchmark**: *Valorant* (competitive silhouette & map readability, painterly 3D materials) × *Arcane* (hand-painted brush value variation & floating kinetic sketch lines) × **90s Filipino Komiks / Kalye Illustration** (bold fake-2D charcoal ink outlines on 3D models, widescreen `1280×720` landscape UX).

---

## 1. Honest Baseline Audit (Checkpoint 1 Self-Grade: `4.2 / 10`)

Before launching the 10-specialist AAA swarm, our initial blockout render (`checkpoint1_*.png`) was audited and graded honestly:
- **Environment Topology (`3.5 / 10`)**: Used a wide rectangular highway grid with large bare blocks instead of a **circular, claustrophobic provincial Pinoy barangay** of *tabi-tabing mga bahay* (wall-to-wall cramped houses with narrow `3.6–5.8 m` *eskinitas*).
- **Material & Cellular Realism (`4.0 / 10`)**: Sidewalks clipped to white under sunlight; buildings used clean pastel blocks instead of weathered working-class provincial materials (raw hollow-block CHB mortar courses, rusted patchwork *yero* corrugated tin roofs, *sawali*/plywood upper walls, damp *lumot* moss stains, *rehas* iron grills, and chaotic electric pole **spaghetti wiring**).
- **2D-on-3D Art Style & VFX (`4.5 / 10`)**: Missing fake-2D ink outlines on 3D world models and missing *Arcane*-inspired floating kinetic sketch lines balanced for competitive UX clarity.
- **Roster, Landscape UX & Ranked (`4.8 / 10`)**: Only 6 kids (needs **10 unique Filipino kids**), menus still portrait-first (needs **full-game `1280×720` Landscape 90s Filipino Art Style**), and online play lacked the **7-tier Tagalog Ranked System** (*Kahoy* -> *Batang Kalyeah*).

**Target Acceptance Gate**: **`>= 9.0 / 10`** verified via 1280×720 PNG checkpoints, `qa-gatekeeper` lean-code audit, and 100% passing GUT suite.

---

## 2. Environment & 3D Model Quality Standard ("Cellular-Level" Provincial Pinoy Barangay)

### 2.1 Zero Tourist "Filipino Branding" — Authentic Working-Class Provincial Barangay
- **Do NOT** use theme-park fiesta cliches, neon tourist signs, or suburban American setbacks/lawns.
- **DO** build a lived-in, humble, tight provincial-urban Pinoy *looban / barangay*:
  - **Circular Barangay Footprint (`R = 84 m`, `168 m` diameter)**: The playable compound is circular, enclosed `360°` around its perimeter by an unbroken organic ring of **tabi-tabing mga bahay** (houses pressed directly wall-to-wall, zero gap or `<= 0.35 m` fire-alley slit, staggered roof heights and angles).
  - **Narrow Spaces & *Eskinitas***: Interior clusters form tight `3.6 m – 5.8 m` alleys (*eskinitas*) and curved lanes with sharp line-of-sight breaks, overhanging *yero* eaves, and 3 tactical chokepoints across a narrow provincial *sapa / estero* drainage creek (West Talipapa Footbridge, Central Rotonda / *Poso* Well Bridge, East Toda / Waiting Shed Bridge).

### 2.2 Cellular-Level 3D Geometry & Painterly `2048×2048` Atlas Requirements
Every `.glb` model in `assets/models/barangay/` must meet these structural and texture standards:
1. **Painterly 2D-Illustrated-on-3D Atlas (`2048×2048`, VRAM-compressed `compress/mode=2`)**:
   - **Raw Concrete Hollow Blocks (CHB)**: Individual `40×20 cm` block courses, troweled mortar joints, diagonal plaster cracks, exposed aggregate chips, and dark olive-green *lumot* (moss/dampness) rising `40 cm` from ground contact.
   - **Patchwork Corrugated GI Roofs (*Kalawang na Yero*)**: Multi-tone oxidized rust sienna, chalky zinc grey, faded red-oxide primer panels, dark overlap seams, nail rows, and roof weights (old rubber tires, concrete chunks, scrap lumber battens).
   - **Mixed Wood, *Sawali* & *Trapal***: Sun-bleached plywood grain, woven diagonal bamboo *sawali* slats, faded blue/orange *trapal* tarpaulin sunshades with rope grommets, jalousie louver glass strips, and rusted wrought-iron *rehas* security bars.
   - **Hand-Inked 2D Crevice & Sketch Lines**: Baked dark charcoal (`#1A1619`) comic contour lines along panel borders, window frames, and roof ribs so every surface reads like a 90s Filipino comic background painted onto 3D forms.
2. **Silhouette & Micro-Detail Geometry (Budgeted for Mobile + PC 60 FPS)**:
   - Every building cluster (`brgy_tabi_row_a.glb`, `brgy_tabi_row_b.glb`, `brgy_shophouse_row.glb`, `brgy_house_two_storey.glb`, `brgy_sari_sari.glb`) must break boxiness with staggered 1- and 2-storey volumes, pitched/lean-to *yero* roofs with visible overhang thickness, window awnings, exterior hollow-block ledges, water drums, galvanized electric meters, and rooftop TV antennas.
   - **Electric Poles with Spaghetti Wiring (`brgy_meralco_post.glb` + `StreetMap` Catenary Wire Mesh)**:
     - Leaning weathered concrete/wood utility poles with rusted crossarms, cylindrical pole-mounted transformer, 3–4 junction boxes, coiled figure-8 slack cable loops ("dead coils") hanging on the pole, and dense multi-strand sagging catenary black cable bundles crisscrossing between poles and fanning out as service drop-wires directly to house eaves!
3. **Lighting & Albedo Discipline (`gl_compatibility`)**:
   - Maximum diffuse surface albedo `<= 0.72` (never pure white `1.0`) so golden-hour directional sunlight (`light_energy ~ 0.85–0.92`) never clips sidewalks or walls to flat white.

---

## 3. Fake-2D Ink Outlines & Valorant × Arcane Gameplay VFX Standard

1. **Fake-2D Outline Shader (`shaders/arcane_ink_outline.gdshader` & `shaders/team_outline.gdshader`)**:
   - All 3D kid models (`KidModel`) and key 3D barangay props/landmarks wear crisp dark charcoal-ink silhouettes (`#171318`) so 3D geometry reads immediately as 2D-illustrated art in motion.
   - Kids retain unambiguous team readability (Ally Cobalt vs. Enemy Crimson/Orange rim/badge) without obscuring their 90s Filipino outfit textures.
2. **Arcane Floating Kinetic Sketch Lines + Valorant UX Clarity**:
   - Gameplay VFX (`FxHeroActions`, `WeaponFxView`) combine **clean geometric hit-zone boundaries** (Valorant rule: exact radius/range ring on the ground, low center opacity `<= 0.22` so floor hazards and enemies remain 100% readable) with **brief, expressive floating ink/chalk sketch arcs** (2–4 frame hand-drawn speed strokes around dashes, slingshot releases, and ultimate slams).
   - Zero opaque smoke walls over the camera; respect `Settings.reduced_motion` and `Settings.performance == &"turbo"`.

---

## 4. The 10 Filipino Kids Lineup (`10 Bida ng Kalye`)

Cosmetic-only (`CharacterDef` — identical hitbox, HP, and speed for competitive fairness), allowing a full **5v5 match with 10 unique kids**:
1. **`junjun`** — White *sando*, red basketball shorts, *bimpo* towel at the neck, rubber *tsinelas*, spiky bangs.
2. **`ligaya`** — Sun-yellow knotted tee, maong shorts, colorful hair clips, barrio patintero captain.
3. **`migo`** — 90s striped oversized polo, cargo shorts, backward cap, chest *belt bag*.
4. **`toni`** — Loose barangay liga jersey, track pants, wristbands, cropped tousled hair.
5. **`popoy`** — Stocky build (`body_width = 1.14`), public-school PE shirt, athletic shorts, fearless blocker.
6. **`inday`** — Provincial floral cotton *bestida* over shorts, twin pigtails with bright *panali* bands.
7. **`kiko`** *(New)* — Sleeveless faded maong vest over tee, rolled pants, forehead bandana, *trumpo* craftsman.
8. **`nene`** *(New)* — 90s pinafore jumper over white blouse, high scrunchie ponytail, knee socks, *garter* ace.
9. **`buknoy`** *(New)* — Sun-tanned river kid, open checkered *camisa*, frayed shorts, matchbox *gagamba* pouch.
10. **`tala`** *(New)* — Barangay sprinter, maroon singlet, cycling shorts, woven friendship bracelets & headband.

Every kid MUST have:
- `data/characters/<id>.tres` wired into `data/rules/game_rules.tres`
- Rigged `assets/models/characters/<id>.glb` with all required animation clips (`Idle`, `Run`, `Exhausted_Run`, `Stunned`, `Airborne`, `Cast_OneHand`, `Cast_Run`, `Tripped`)
- Cohesive 90s Filipino Komiks `assets/portraits/<id>.png`, `assets/portraits/faces/<id>.png`, and `assets/portraits/cards/<id>.webp`.

---

## 5. Full Landscape 90s Filipino Art Style UI & Tagalog Ranked System

1. **Full-Game Widescreen Landscape (`1280 × 720`)**:
   - `project.godot` uses `viewport_width = 1280`, `viewport_height = 720` (keeping `window/handheld/orientation = 1` in `project.godot` so Android APK manifest CI inspection stays unchanged, while `Settings._ready()` activates `set_landscape(true)` for runtime `SCREEN_SENSOR_LANDSCAPE` across all screens!).
   - Every screen (`TitleScreen`, `WeaponPickScreen`, `HistoryPage`, `MatchResults`, `MatchHud`) fits `1280×720` cleanly without vertical overflow, styled with **90s Filipino Komiks** inked borders, warm sun-aged paper/gold plates, and punchy street typography.
2. **Online Tagalog Ranked Tiers (`RankedSystem`)**:
   - 7 competitive tiers based on Tagalog materials & street legend status:
     1. **Kahoy** (`0 – 199 LP`)
     2. **Bakal** (`200 – 399 LP`)
     3. **Tanso** (`400 – 599 LP`)
     4. **Pilak** (`600 – 849 LP`)
     5. **Ginto** (`850 – 1149 LP`)
     6. **Brilyante** (`1150 – 1499 LP`)
     7. **Batang Kalyeah** (`1500+ LP`)

---

## 6. Offline & Online Optimization Budgets + Anti-Over-Engineering Rules

1. **Strict GDScript Static Typing**: `treat_warnings_as_errors = true` in `project.godot`. Every `var`, loop iterator (`for item: Type in list`), parameter, and return type MUST be explicitly typed. Never use `:=` on `Variant`, `Dictionary.get()`, or untyped arrays.
2. **Performance Budgets**:
   - Batch static wires/props into combined `ArrayMesh` or `MultiMeshInstance3D` (keep total draw calls low for budget Android GPUs).
   - Zero `StyleBoxFlat.new()` or `Font` allocations inside per-frame `_draw()` loops — cache styleboxes/arrays as member fields.
   - Keep `scripts/sim/` 100% deterministic (`RefCounted`/`Resource` only, `MatchSim.rng` only, 30 Hz fixed tick, compact `Snapshot` dicts).
3. **Anti-Over-Engineering Mandate (`qa-gatekeeper`)**:
   - No speculative abstractions, no unused config layers, no extra autoloads (strictly `Net`, `Session`, `Settings`), no touching `version.txt` or `CHANGELOG.md`.

---

## 7. Cross-Worker Interface Contracts (Must Match Exactly)

- **`MapLayout` (`barangay-map`)**:
  - `@export var arena_radius: float = 84.0` (0.0 disables circular clamp for legacy rectangular tests; `> 0.0` clamps players inside `position.length() <= arena_radius - player_radius`!).
  - Keep `water_rects()`, `obstacle_rects()`, `walk_obstacle_rects()`, `lane_rect()`, `base_center(side)`, `rescue_rect(side)`, `tagged_spot(side, slot)`.
- **`ArenaCamera` (`camera-input`)**:
  - `func set_speed_boost(boosting: bool) -> void`
  - Automatic spring-arm obstacle clamping inside `_apply_pose()` when `rules.third_person and layout != null`.
- **`MatchInput` & `PcCombatInput` (`camera-input`)**:
  - `MatchInput.set_primary(held: bool, aim_pt: Vector2) -> void`
  - `MatchInput.press_dash() -> void`
  - `MatchInput.press_ultimate(aim_pt: Vector2) -> void`
  - `PcCombatInput` (`class_name PcCombatInput extends RefCounted`):
    - `func handle_event(event: InputEvent, input: MatchInput, aim_pt: Vector2, active: bool) -> Vector2` (returns look delta `Vector2` for `ArenaCamera.look`)
    - `func poll_frame(input: MatchInput, ground_way_fn: Callable, aim_pt: Vector2, active: bool) -> void`
- **`MatchHud` & Sub-Widgets (`hud-90s`)**:
  - Signals on `MatchHud` (and `AbilityArc`): `primary_changed(held: bool)`, `dash_pressed`, `ultimate_pressed` (plus all existing signals `swap_pressed`, `again_pressed`, `exit_pressed`, etc.).
  - Methods on `MatchHud`:
    - `func sync_world_hud(sim: MatchSim, camera: Camera3D, player: PlayerState, local_team: int, cam_yaw_deg: float) -> void`
    - `func notify_hit(lethal: bool = false) -> void`
    - `func notify_takedown(tagger_name: String, victim_name: String, ally_scored: bool, detail: String = "") -> void`
    - `func notify_rescue(rescuer_name: String, saved_name: String, ours: bool) -> void`
- **`RankedSystem` (`ranked-net`)**:
  - `const TIERS: Array[Dictionary]` (`Kahoy`, `Bakal`, `Tanso`, `Pilak`, `Ginto`, `Brilyante`, `Batang Kalyeah`)
  - `static func tier_for_lp(lp: int) -> Dictionary`
  - `static func tier_name(lp: int) -> String`
  - `static func record_match(mode: String, won: bool, my_score: int, their_score: int) -> Dictionary` (updates saved LP in `MatchHistory` / `user://ranked.cfg` and returns `{"old_lp": int, "new_lp": int, "delta": int, "tier": String, "promoted": bool}`)
  - `static func current_lp() -> int`
  - `static func draw_emblem(canvas: CanvasItem, center: Vector2, radius: float, lp: int) -> void`
