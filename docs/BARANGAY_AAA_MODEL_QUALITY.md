# Kalyeah — AAA Barangay Model, Art, 3C & Production Quality Specification (Overhaul v2)

> **Status**: Binding Studio Production Standard — **Overhaul v2 (Playtest-Driven 90s Province, Deadlock 3C/HUD, Grounded Locomotion & Sunlit Kalye Brand Rework)**  
> **Target Benchmark**: *Deadlock* (`image_1` left-shoulder framing, waist-left tapered vertical HP bar & airy minimalist white-outline HUD) × *Valorant / Arcane* (sunlit painterly 3D materials, fine espresso inner contour + soft team outer rim, readable VFX) × **Authentic 1990s Philippine Provincial Barangay** (sunlit shophouses, mossy CHB hollow blocks, rusted *yero*, walkable shallow canal, 10 Filipino kids, and open-licensed Kalye signboard typography).

---

## 1. Evolution & Acceptance Gate (`>= 9.5 / 10` Per Domain)

Following Overhaul v1 (`9.7/10` technical foundation, 189/189 green GUT suites) and live playtest review, **Overhaul v2** resolves key gameplay feel, readability, environment flow, and brand identity upgrades:
- **Map Flow & Landmarks**: Replaces fragmented tight alleys and the central mid bridge with **3 wider main North–South pathways (`~11–14 m`)**, **2 East–West connecting alleyways**, a **walkable shallow mid canal (`water_obstacles = []`, NO bridge at `X = 0`)**, **dynamic outer perimeter walls** (provincial houses + mossy CHB walls + mango/coconut/banana trees), **3 small Power-Up objective stands**, and handcrafted 90s provincial landmarks (Jeepney, Tricycle, TODA Terminal, Wooden Basketball Post, Nena's Sari-Sari Store, Karinderya, Barberya, Vulcanizing Shop, Tabing-Ilog embankments, pole posters, and **true 3D volumetric hanging clothes**).
- **Scale, 3C Camera & Grounded Kid Locomotion**: Scales gameplay kids to realistic child proportions (`kid_view_scale = 0.82`, `~1.51 m` in-world while keeping `KidModel.KID_HEIGHT = 1.85` raw asset invariants), adopts *Deadlock* (`image_1`) left-shoulder framing (`shoulder_distance = 4.15`, `shoulder_height = 1.88`, `shoulder_offset = 0.62`, `look_pitch_degrees = 15.5`, `shoulder_fov = 68.0`) with **default auto-behind camera follow + immediate manual look override (`1.6 s` hold)**, slower tactical run (`move_speed = 6.4 m/s`, `sprint_enabled = false`), **analog Walk (`< 0.62`) vs. Run (`>= 0.62`)**, re-enabled **deterministic running kid trip (`12%` every `6.0 s`)**, plus **Jump, Crouch, and Slide** animations & netcode sync.
- **Lively Sunlit Vibe, Dual Outlines & Minimalist HUD**: Shifts atmosphere from dark/heavy to **warm, lively, soft sunlit 90s provincial afternoon**, refines outlines to a **fine warm-espresso inner contour (`0.008–0.009`) + soft outer team rim (`0.026`)**, reworks the HUD to *Deadlock* (`image_1`) layout (waist-left vertical HP bar, top-left minimap with smaller bottom-right gear button, pure-white silhouette ability/movement discs), and replaces `Clash-Regular.otf` completely with open-licensed SIL OFL `KalyeahDisplay-Regular.ttf` + `KalyeahSans-Variable.ttf` and custom sunlit signboard branding.

**Target Acceptance Gate**: **`>= 9.5 / 10`** across all 5 audit domains (`audit-3d`, `audit-map`, `audit-combat`, `audit-hud`, `audit-brand`), `0` Godot 4.6.2 static type warnings (`treat_warnings_as_errors = true`), and `100%` passing GUT unit suite.

---

## 2. Environment, Map Flow & 90s Province 3D Model Standard

### 2.1 Circular Barangay (`R = 84.0 m`) — 3 Main Pathways, 2 Alleyways & Walkable Mid Canal
- **Circular Compound (`arena_radius = 84.0 m`, `168 m` diameter)**:
  - **3 Wider Main North–South Pathways (`~11–14 m` wide)**:
    1. **West Lane (`X ≈ -34 m`)**: *Tabing-Ilog & Karinderya / Lutong Bahay* corridor.
    2. **Center Lane (`X = 0 m`)**: *Plaza – Walkable Shallow Canal – Kalye Basketball* corridor.
    3. **East Lane (`X ≈ +34 m`)**: *TODA Tricycle Terminal, Barberya & Vulcanizing Shop* corridor.
  - **2 East–West Connecting Alleyways**: Clear cross-lanes at `Z ≈ -26 m` (North) and `Z ≈ +26 m` (South) linking all 3 pathways cleanly for 5v5 rotations and `BarangayNav` A* routing.
  - **Walkable Shallow Mid Canal (`NO` Bridge at `X = 0`)**:
    - Remove the central footbridge at `X = 0` and set `water_obstacles = Array[Rect2]([])` in `data/rules/map_layout.tres` so players and bots walk directly through the shallow turquoise-jade canal (`Y ≈ -0.28 m` bed) via sloped stone/concrete *Tabing-Ilog* embankments and wading ramps, accompanied by water splash/ripple VFX.
  - **Dynamic Outer Perimeter Ring**:
    - Breaks monotony around the `84 m` perimeter by mixing multi-storey provincial houses, mossy **Cement Hollow Block (`brgy_chb_wall.glb`)** wall segments (recessed mortar joints, rebar stubs, stenciled *"BAWAL UMIHI DITO"*), and organic Philippine provincial trees (**Punong Mangga** `brgy_mango_tree.glb`, **Puno ng Saging** `brgy_banana_clump.glb`, **Puno ng Niyog** `brgy_coconut_palm.glb`).
  - **3 Small Power-Up Objective Stands (`MapLayout.powerup_spots`)**:
    - Placed at `[Vector2(-34.0, 0.0), Vector2(0.0, 0.0), Vector2(34.0, 0.0)]` dressed with `brgy_powerup_stand.glb` (*Palamig / Sago't Gulaman* glass jug cooler + *Teks / Turon* street tray).

### 2.2 Authentic 90s Province Landmarks & Zero-AI-Slop 3D Geometry
Every `.glb` in `assets/models/barangay/` and `assets/models/weapons/` must look handcrafted with beveled edges, realistic silhouettes, and clean UVs (zero primitive-stack AI slop):
1. **Vehicles & Street Fixtures (`province-props`)**:
   - `brgy_jeepney.glb` (`~5.4 m`): Rounded pressed-steel hood, grille slats, dual headlights, *"POBLACION - TABING-ILOG"* signboard, side grab bars, rear stepboard, wheel arches & subtle fender rust.
   - `brgy_tricycle.glb` (`~2.3 m`): Classic Pinoy motorcycle + galvanized steel *kolong/pampasahero* sidecar, angled windshield tarp, roof rack, number plate.
   - `brgy_toda_shed.glb`: Corrugated rusty GI dispatch shed with *"BRGY. TODA TERMINAL"* signboard, wooden bench, and hanging tire bumper.
   - `brgy_wooden_hoop.glb`: Coco-lumber `4×4` basketball post anchored in a concrete-filled tire/block base, hand-painted white plywood backboard with faded red shooter's square, rusty orange ring & cord net.
   - `brgy_sampayan_3d.glb` + `SpaghettiWires` volumetric laundry: **True 3D extruded garments** (tubular sleeves, collars, puffed torso folds, shorts legs, draped towels & *sipit* clothespins on sagging lines — **NEVER flat 2D planes**).
2. **Shophouses, Utility Poles & Street Signage (`env-kit`)**:
   - Hand-painted signboards for **"TINDAHAN NI ALING NENA"** (Nena's Sari-Sari Store with security grille), **"KARINDERYA / LUTONG BAHAY"**, **"BARBERYA"** (with barber pole), and **"VULCANIZING SHOP"** (hanging tires & air tank).
   - Leaning utility poles (`>= 4.5 m` wire clearance) with **Election Posters (*VOTE!* tarps)**, **Siphoning Service (*MALABANAN SIPSIP POSO NEGRO*) notices**, **"NO ENTRY"** signboards, and catenary spaghetti wiring.
3. **Sunlit Soft Texture Atlases (`BarangayAtlas.jpg` & `ProvincePropsAtlas.jpg`)**:
   - VRAM-compressed (`compress/mode=2`, `<= 2048×2048`, **zero `md5=` lines** in `.import` sidecars).
   - Warm, lively sunlit daytime values (`mean_sRGB ~ 0.44–0.49`, `max_sRGB <= 0.78` so `gl_compatibility` directional sunlight never clips to white; **never** apply manual `** 2.4` gamma on 8-bit Blender `Image.pixels`), damp green *lumot* moss on concrete/CHB bases, and oxidized sienna rust on metal/GI roofs.

---

## 3. Sunlit Atmosphere, Dual-Outline Harmony & Scaled VFX (`0.82x` Kids)

1. **Harmonious Black + Team Outline Pairing (`KidModel`, `arcane_ink_outline.gdshader`, `team_outline.gdshader`)**:
   - World props wear a fine warm-espresso contour (`~0.008`) instead of harsh black cages.
   - `KidModel` pairs a fine warm deep-espresso inner ink line (`INK_OUTLINE_WIDTH = 0.009`, `Color(0.13, 0.10, 0.12, 0.92)`) with a softened harmonious outer team aura (`OUTLINE_WIDTH = 0.026`), preserving clear Ally vs. Enemy readability at smaller `0.82x` screen scale.
2. **Scaled 3D Street-Toy Weapons & UX-Balanced VFX (`WeaponModel`, `WeaponFxView`)**:
   - Custom weapon `.glb`s (*tirador* guava-branch Y-slingshot, woven/molded *tsinelas*, smooth river *bato*, steel *gunting*, folded *papel*, dented *lata*, 6-prong *jackstone*, swirled glass *holen/bola*, wooden pear *trumpo*) and `WeaponFxView` rings/projectiles are scaled down proportionally to `kid_view_scale = 0.82` while preserving `test_fx_qa.gd` pooled-node and `<= 96` material caps.

---

## 4. Deadlock (`image_1`) 3C Camera, Locomotion, Trip & Combat Balance

1. **Smaller Kid Scale & *Deadlock* (`image_1`) Camera Framing (`data/rules/camera_rules.tres`, `ArenaCamera`)**:
   - `kid_view_scale = 0.82` (`~1.51 m` world height; `KidModel.KID_HEIGHT = 1.85` constant preserved).
   - `shoulder_distance = 4.15`, `shoulder_height = 1.88`, `shoulder_offset = 0.62` (local player framed slightly left of center at `x ≈ 38%–43%`), `look_pitch_degrees = 15.5` (camera higher above kid, pitched gently downward toward crosshair), `shoulder_fov = 68.0`.
   - **Default Auto-Behind Follow + Manual Override**: `ArenaCamera` automatically aligns behind the local character's movement/facing (`sync_behind_character`) by default, while manual camera drag or mouse motion (`look(drag)`) **immediately overrides** auto-follow and holds manual control for `_manual_look_hold = 1.6 s` after drag ends. Includes smooth vertical pivot offsets for crouch/slide (`-0.42 m`) and jump (`+air_y * 0.65`).
2. **Grounded Kid Locomotion & Random Trip (`GameRules`, `PlayerInput`, `PlayerState`, `MatchSim`)**:
   - **Analog Walk vs. Slower Run**: `move_speed = 6.4 m/s`, `sprint_enabled = false`, `walk_run_threshold = 0.62`, `crouch_speed_share = 0.55`.
     - Stick deflection `0 < len < 0.62` (or PC `Alt` walk `0.45`): **Walk** (`state.walking = true`, speed proportional to deflection, **zero trip rolls**).
     - Stick deflection `>= 0.62` (or full `move_target` run): **Run** (`state.walking = false`, rolls deterministic `MatchSim._trip_rng` every `trip_interval = 6.0 s` at `trip_chance = 0.12`, applying `StatusEffects.Type.TRIPPED` for `trip_seconds = 1.05 s`).
   - **Jump, Crouch & Slide**:
     - **Jump (`BTN_JUMP = 1 << 11`)**: Grounded launch `vel_y = 5.6`, `jump_gravity = 16.5`, vertical `air_y` parabola, `jump_cooldown = 0.25 s`.
     - **Crouch (`BTN_CROUCH = 1 << 12`)**: Grounded stealth posture (`state.crouching = true`, `0.55x` walk speed, `0` trip rolls).
     - **Slide (`BTN_SLIDE = 1 << 13` or crouch-tap while running)**: `0.46 s` ground slide burst at `10.2 m/s` along `slide_dir`, `slide_cooldown = 3.2 s`.
   - **Full Kid Animation Repertoire (`KidModel.animate`)**: Smoothly drives `walk`, `run`, `jump` (`Airborne` + launch/landing knee flex), `crouch` / crouch-walk, `slide`, and `trip` (`Tripped` stumble & push-up recovery), while keeping `_walk(kid, 6.0)` with default `PlayerState.new()` playing `"run"` at `speed_scale == 1.0` for `test_authored_kids.gd`.
3. **3 Small Power-Up Objectives & 5v5 Weapon Rebalance**:
   - `MatchSim.powerups` (`25.0 s` respawn):
     - `1 = PALAMIG`: `+18 HP` instant heal & `+22%` move speed for `10.0 s`.
     - `2 = TEKS`: `+20%` weapon damage & `+25` ultimate charge for `12.0 s`.
     - `3 = TURON`: `25%` incoming damage reduction & `+4 HP/s` regen for `10.0 s`.
   - Tactical 5v5 TTK rebalance across `data/weapons/*.tres`: `tirador_bato` (`11` dmg, `0.48 s` CD, `0.24 m` radius), `ult_bagsak` (`42` dmg, `4.2 m` radius), and balanced 14 street-game superpowers.

---

## 5. The 10 Filipino Kids Lineup (`10 Bida ng Kalye`)

Cosmetic-only (`CharacterDef` — identical hitbox, HP, and speed), supporting full **5v5 matches with 10 unique kids**:
1. **`junjun`**, 2. **`ligaya`**, 3. **`migo`**, 4. **`toni`**, 5. **`popoy`**, 6. **`inday`** — **CRITICAL INVARIANT**: Never overwrite raw `{junjun,ligaya,migo,toni,popoy,inday}.glb` or their `_0/_1/_2.png` textures pinned by `tests/unit/test_authored_kids.gd`!
7. **`kiko`**, 8. **`nene`**, 9. **`buknoy`**, 10. **`tala`** — Sculpted in `tools/art/build_10_kids.py` with smooth organic child proportions, soft sunlit 90s Pinoy outfit textures, normalized `1.70 m` source rig height, all 8 required animation clips (`Idle`, `Run`, `Exhausted_Run`, `Stunned`, `Airborne`, `Cast_OneHand`, `Cast_Run`, `Tripped`), and full portrait sets.

---

## 6. Sunlit 90s Kalye Brand Typography, Deadlock (`image_1`) HUD & Tagalog Ranked

1. **Open-Licensed Brand Typography (`assets/fonts/brand/` — ZERO `Clash-Regular.otf`)**:
   - `assets/fonts/clash/Clash-Regular.otf` (+ `.import`) is **permanently deleted** with zero remaining references in code or resources.
   - Replaced by SIL OFL open-licensed fonts in `assets/fonts/brand/` (with `OFL-LondrinaSolid.txt` & `OFL-SourceSans3.txt`):
     - **Display / Signboard Font**: `KalyeahDisplay-Regular.ttf` (Londrina Solid).
     - **Readable UI / HUD Font**: `KalyeahSans-Variable.ttf` (Source Sans 3 with `FontVariation` weights `600` body, `700` buttons/labels, `800` crisp HUD numbers `ACTION_FONT`).
   - **Sunlit 90s Kalye Brand Palette & Emblem (`KalyeahTheme`, `UiSkin`, `KomiksStyle`, `TitleScreen`)**:
     - Custom hand-painted provincial wooden/tin signboard crest & wordmark (`assets/ui/brand/kalyeah_wordmark.png`, `kalyeah_crest.png`) paired with warm sunlit cream (`#FAF3E3`), soft tropical slate-espresso (`#2A2522`), mango amber (`#F5A623`), sari-sari jade teal (`#1F9D8B`), and terracotta coral (`#E2553E`).
2. **Non-Disruptive *Deadlock* (`image_1`) Match HUD (`1280 × 720`)**:
   - **`HeroVitalsPanel`**: Tapered, angled vertical HP bar positioned at the player's **left waist** (`x ≈ 215..295, y ≈ 430..630` on `1280×720`, cleanly above `MoveStick`), crisp white current-HP fill rising from bottom, coral-red damage/missing-HP band above it, bold white numeric HP breaking the right edge + `/100` + compact status/power-up pips.
   - **Top-Left `BarangayMinimap` + Compact Bottom-Right `settings_button`**: Minimap at top-left (`x = 16, y = top_inset + 12`, displaying the 3 main pathways, walkable mid canal, and 3 power-up spots; no mid bridge), with `settings_button` tucked onto the minimap's bottom-right (`>= 80×80` `UiMetrics.MIN_TARGET` hit rect, sleek `~34 px` white-rimmed gear badge).
   - **Minimalist White-Outline Action Discs (`AbilityArc` + `MoveStick`)**: Pure-white `128×128` RGBA silhouettes (`assets/ui/hud_icons/<id>.png`) rendered with thin white outline + faint translucent white fill ONLY (`HudStyle.draw_white_disc` + `draw_white_sweep`) across `Tirador`, 3 superpowers, `Bagsak` ultimate, `Vault`, **`Jump`**, and **`Crouch/Slide`**, paired with `MoveStick`'s faint `0.62 * BASE_RADIUS` inner walk-threshold ring, airy `MatchHudTop`, and delicate `Crosshair`.
   - **`test_ui_lint.gd` Invariants**: Zero banned copy (`"skill"`, `"dash"`, `"dummies"`, `"..."`, `"•"`, `"Lobby"`), zero bare font sizes or corner radii, `>= 80×80` touch targets, `UiA11y.name_it(...)` on icon buttons.
3. **7-Tier Online Tagalog Ranked System (`RankedSystem`)**:
   - **Kahoy** (`0–199 LP`), **Bakal** (`200–399 LP`), **Tanso** (`400–599 LP`), **Pilak** (`600–849 LP`), **Ginto** (`850–1149 LP`), **Brilyante** (`1150–1499 LP`), **Batang Kalyeah** (`1500+ LP`).

---

## 7. Locked Cross-Worker Interface Contracts (Overhaul v2)

- **`PlayerInput` (`scripts/sim/player_input.gd`)**:
  - `const BTN_JUMP: int = 1 << 11`, `const BTN_CROUCH: int = 1 << 12`, `const BTN_SLIDE: int = 1 << 13` (all 3 included in `KNOWN_BITS`).
  - `move: Vector2` preserves analog `0..1` magnitude (`< rules.walk_run_threshold` = Walk; `>= 0.62` = Run).
- **`PlayerState` (`scripts/sim/player_state.gd`)**:
  - `var walking: bool = false`, `var crouching: bool = false`, `var air_y: float = 0.0`, `var vel_y: float = 0.0`, `var jump_cd: float = 0.0`, `var slide_left: float = 0.0`, `var slide_cd: float = 0.0`, `var slide_dir: Vector2 = Vector2.ZERO`, `var buff_type: int = 0`, `var buff_left: float = 0.0`.
- **`MatchSim` & `MapLayout` (`combat-ai`, `barangay-map`)**:
  - `MatchSim.powerups: Array[Dictionary]` (`{"id": int, "kind": int, "pos": Vector2, "active": bool, "respawn_left": float}`), `signal powerup_taken(player_id: int, powerup_id: int, kind: int)`.
  - `MapLayout.arena_radius == 84.0`, `MapLayout.water_obstacles == []` (walkable mid canal), `@export var powerup_spots: Array[Vector2] = [Vector2(-34.0, 0.0), Vector2(0.0, 0.0), Vector2(34.0, 0.0)]`.
- **`ArenaCamera`, `MatchInput` & `PcCombatInput` (`camera-input`)**:
  - `ArenaCamera`: `sync_behind_character` default auto-follow + `_manual_look_hold = 1.6` on `look(drag)` + crouch/slide/air_y vertical pivot offsets.
  - `MatchInput`: `press_jump() -> void`, `set_crouch(held: bool) -> void`, `press_slide() -> void`, analog `stick` magnitude preserved.
- **`MatchHud` (`hud-90s`)**:
  - Signals: `primary_changed(held: bool)`, `dash_pressed`, `ultimate_pressed`, `jump_pressed`, `crouch_changed(held: bool)`, `slide_pressed` (plus all existing HUD signals & methods).
- **`Snapshot` & `NetProtocol` (`ranked-net`)**:
  - Quantized delta sync for `walking`, `crouching`, `air_y`, `slide_left`, `slide_dir`, `buff_type`, `buff_left`, and `sim.powerups` with zero extra bytes when idle/default.
- **Strict Static Typing (`qa-gatekeeper`)**:
  - `treat_warnings_as_errors = true` in Godot 4.6.2: `100%` explicit static types, `0` `:=` on `Variant`, `0` untyped `var` / `for` / parameters, deterministic `MatchSim` (`RefCounted`/`Resource` only, no `randf()`), and untouched `version.txt` / `CHANGELOG.md`.
