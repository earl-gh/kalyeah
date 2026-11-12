# Overhaul v2 — Combat, Deadlock 3C Camera, Locomotion, Weapons & Netcode Audit

- **Auditor**: `audit-combat` (READ-ONLY Combat, 3C Camera, Locomotion & Netcode Specialist)
- **Date**: 2026-11-12
- **Swarm Scope**: `#66`, `#72`, `#73` (Items 3, 4, 9 & Locomotion Presentation), `#79` (Phase 1 Contract), `#80` (`@camera-input`), `#82` (`@ranked-net`), `#83` (`@combat-ai`), `#95` (`@locomotion-anim`)
- **Overall Domain Grade**: **`9.9 / 10` — PASS (Approved for `@qa-gatekeeper` Test Alignment & Final Gate)**

---

## 1. Executive Scorecard

| Audited Slice | Specialist | Files Audited | Grade | Verdict |
| :--- | :--- | :--- | :---: | :---: |
| **Deadlock (`image_1`) 3C Camera, `0.82x` Scale & Input** | `@camera-input` (`#80`) | `data/rules/camera_rules.tres`, `scripts/view/{camera_rules,arena_camera,practice_match}.gd`, `scripts/input/{match_input,pc_combat_input}.gd` | **9.9 / 10** | **PASS** |
| **Analog Walk/Run, Kid Trip, Jump/Crouch/Slide, Power-Ups, AI & 16-Weapon Balance** | `@combat-ai` (`#83`) | `scripts/sim/{player_input,player_state,game_rules,match_sim,match_objectives,weapon_system,team_coach,fight_brain}.gd`, `data/rules/game_rules.tres`, `data/weapons/*.tres` (16) | **9.9 / 10** | **PASS** |
| **5v5 Quantized `Snapshot`, `NetProtocol` & Client Prediction Parity** | `@ranked-net` (`#82`) | `scripts/net/{snapshot,protocol,server_match,game_client}.gd` | **9.9 / 10** | **PASS** |
| **Kid Locomotion Postures, `0.82x` Weapon Models & Presentation Sync** | `@locomotion-anim` (`#95`) | `scripts/view/practice_match_presentation.gd`, `scripts/view/art/{kid_model,kid_pose,weapon_model}.gd` | **9.9 / 10** | **PASS** |
| **Determinism & 100% Static Typing Gate** | Cross-Slice (21 `.gd` files) | `0` executable `randf()`/`randi()`/`randomize()` in `scripts/sim/`; `0` `:=`, `0` untyped `var`/`for`/`func` returns across all 21 `.gd` files | **10.0 / 10** | **PASS** |

---

## 2. Deadlock (`image_1`) 3C Camera Framing, `0.82x` Kid Scale & Auto-Behind Override

### 2.1 Gameplay Kid Scale & Perspective Projection (`data/rules/camera_rules.tres`)
- **`kid_view_scale = 0.82`** (was `1.75`): Gameplay kids now stand **`1.85 m * 0.82 = 1.517 m`** tall (`ScaleReference.world_factor(0.82) = 1.1669`), replacing the previous `3.2375 m` giants so 10-player 5v5 fights fit naturally inside sunlit provincial lanes, sari-sari storefronts, and shallow canal banks.
- **Deadlock (`image_1`) Shoulder Parameters**:
  - `shoulder_distance = 4.15` (was `6.2`)
  - `shoulder_height = 1.88` (was `2.2`)
  - `shoulder_offset = 0.62` (was `0.78`)
  - `look_pitch_degrees = 15.5` (was `12.0`)
  - `shoulder_fov = 68.0` (was `66.0`), `shoulder_near = 0.3` (was `0.4`), `shoulder_clear_reach = 2.2` (was `2.8`).
- **Verified `1280x720` Screen Projection Math**:
  - Local kid center projects to **`x ≈ 44.1%–44.6%`** (`~565 px`, left shoulder `~40%`), cleanly left of the `x = 640 px` (`50%`) crosshair and right of the waist-left `HeroVitalsPanel` (`x = 212..300 px`).
  - Local kid feet project to **`y ≈ 561 px` (`77.9%`)** and head top (`1.517 m`) to **`y ≈ 402 px` (`55.8%`)**, spanning **`~159 px`** vertically, while the center crosshair (`y = 360 px`) sits slightly above the kid's head pitched downward `15.5°` onto the lane — matching `image_1`.
  - Far-perimeter visibility (`arena_radius - 4.0 = 80.0 m` ahead): heights `[0.2, 1.8, 3.4]` all project unclipped well inside `1280x720` (`y ≈ 209..227 px`).

### 2.2 Default Auto-Behind Follow + Immediate Manual Override (`scripts/view/arena_camera.gd`)
- **`CameraRules.shoulder_pose` Purity Preserved**: `CameraRules.shoulder_pose(player, yaw_deg, pitch_deg)` remains 100% pure (`pivot = Vector3(player.x, shoulder_height, player.y)`, `position = pivot + right * shoulder_offset + back * shoulder_distance`), keeping geometric identity checks in `tests/unit/test_shoulder_view.gd:63` intact.
- **Posture Pivot Tracking (`sync_posture`)**: Smoothly offsets camera pivot height (`_vertical_offset`) toward **`-0.42 m`** during crouch/slide (`CROUCH_SLIDE_PIVOT_OFFSET`) and **`+air_y * 0.65`** during jumps (`JUMP_AIR_FOLLOW_SHARE`).
- **Auto-Behind Character Camera (`sync_behind_character`) & Manual Override (`look`)**:
  - Converts `facing` (`Vector2(x, z)`) via `fposmod(rad_to_deg(atan2(-facing.x, -facing.y)), 360.0)` and rotates **`look_yaw` only** (`look_pitch` is never auto-modified).
  - Any non-zero manual drag (`look(drag)` from `LookTouch`, `AbilityArc.look_dragged`, or `PcCombatInput` mouse look) **immediately overrides** auto-behind follow and arms **`_manual_look_hold = 1.6 s`** (`MANUAL_LOOK_HOLD_SECONDS`), keeping deterministic look-drag assertions in `tests/unit/test_shoulder_view.gd` 100% stable.
  - `PracticeMatch._sync_camera_locomotion()` suppresses auto-behind spin during backpedaling (`screen_move.y > 0.35`), primary fire (`input.primary_held`), and held superpower aiming (`input.card_held()`), and refreshes `input.stick = _camera.ground_way(_stick_screen_way)` each tick so steady thumb deflection steers smoothly as the camera turns.

### 2.3 Touch & Desktop Locomotion Input (`MatchInput`, `PcCombatInput`, `PracticeMatch`, `PracticeMatchPresentation`)
- `MatchInput` preserves full analog `stick` magnitude (`0.0..1.0`) instead of normalizing to `1.0`, and encodes one-shot `BTN_JUMP` / `BTN_SLIDE` plus held `BTN_CROUCH`.
- `PcCombatInput` maps `Alt` -> `0.45` walk magnitude (`< 0.62`), `Space` -> `press_jump()`, `Ctrl`/`C` -> `press_slide()` when running (`>= 0.62`) else held `set_crouch()`, `Shift` -> `press_dash()`, and `InputEventMouseMotion` -> `_camera.look(drag)`.
- `PracticeMatchPresentation.connect_hud()` connects `hud.jump_pressed`, `hud.crouch_changed`, and `hud.slide_pressed` once, while `PracticeMatch._ensure_locomotion_hud_signals()` guards with `get_signal_connection_list(...).is_empty()` so signals never double-fire.

---

## 3. Locomotion Verbs, Trip RNG Determinism, Merienda Power-Ups & Weapon Balance

### 3.1 Locked `PlayerInput` & `PlayerState` Contracts
- **`PlayerInput` (`scripts/sim/player_input.gd`)**: `BTN_JUMP = 1 << 11`, `BTN_CROUCH = 1 << 12`, `BTN_SLIDE = 1 << 13`, all included in `KNOWN_BITS`.
- **`PlayerState` (`scripts/sim/player_state.gd`)**: `walking: bool`, `crouching: bool`, `air_y: float`, `vel_y: float`, `jump_cd: float`, `slide_left: float`, `slide_cd: float`, `slide_dir: Vector2`, `buff_type: int` (`0=NONE, 1=PALAMIG, 2=TEKS, 3=TURON`), `buff_left: float`, `buff_regen_carry: float`, all cleared cleanly on round reset and tag death.

### 3.2 Slower Run, Analog Walk vs. Run, Crouch, Jump, Slide & Deterministic Kid Trip
- **Pacing (`data/rules/game_rules.tres`)**: `move_speed = 6.4` (was `8.2`), `sprint_enabled = false` (was `true`), `walk_run_threshold = 0.62`.
- **Analog Walk (`0.0 < move.length() < 0.62`)**: Sets `state.walking = true`, moves proportionally (`move * speed * dt` -> `0.5` deflection moves at `3.2 m/s`), resets `state.run_time = 0.0`, and performs **zero** trip RNG rolls.
- **Full Run (`>= 0.62` or `move_target` when uncrouched & not overclocked)**: Sets `state.walking = false` and accumulates `run_time`; every **`trip_interval = 6.0 s`**, `_tick_trip` rolls **`_trip_rng.randf()` ONLY** (isolated from `sim.rng`) against **`trip_chance = 0.12`**, applying `StatusEffects.Type.TRIPPED` for **`trip_seconds = 1.05 s`**.
- **Crouch (`BTN_CROUCH`)**: Grounded stealth posture (`state.crouching = true`) at **`crouch_speed_share = 0.55`** (`3.52 m/s`), `0` trip rolls.
- **Jump (`BTN_JUMP`)**: Grounded launch (`vel_y = 5.6 m/s`, `jump_gravity = 16.5 m/s²`, peak `air_y = 0.950 m`, duration `0.679 s`, `jump_cooldown = 0.25 s`). Ground-level `TRAP`, `SLOW_ZONE`, and `FIELD` hazards check `target.air_y < MatchSim.JUMP_CLEAR_HEIGHT` (`0.45 m`) so well-timed jumps clear floor traps/jacks while mid-air projectiles/cones still hit normally.
- **Slide (`BTN_SLIDE` or fresh `BTN_CROUCH` while running `>= 0.62` on ground)**: **`0.46 s`** burst (`slide_duration`) at **`10.2 m/s`** (`slide_speed`) along movement/facing with **`3.2 s`** cooldown (`slide_cooldown`) and `0` trip rolls.

### 3.3 Barangay Merienda Power-Up Objectives (`MatchSim.powerups`)
- Spawned only when `layout.is_circular() and mode == null` at `layout.powerup_spots` (`[(-34.0, 0.0), (0.0, 0.0), (34.0, 0.0)]`) and cleared in `start_mode(p_mode)` so street games remain untouched (`respawn = 25.0 s`, pickup radius `1.65 m`, emits `powerup_taken(player_id, powerup_id, kind)`):
  1. **`BUFF_PALAMIG` (`1`)**: Instant **`+18 HP`** + **`+22%` move speed** for **`10.0 s`**.
  2. **`BUFF_TEKS` (`2`)**: Instant **`+25` Ultimate charge** + **`+20%` outgoing weapon damage** for **`12.0 s`**.
  3. **`BUFF_TURON` (`3`)**: **`25%` incoming damage reduction** (`maxi(1, roundi(amount * 0.75))`) + **`+4 HP/s` regeneration** for **`10.0 s`**.
- `TeamCoach` and `FightBrain` deterministically contest active nearby Merienda stands, jump ground traps/fields, and trigger combat slides without any global RNG calls.

### 3.4 Complete 16-Weapon Balance & `0.82x` Visual Scale Ratio Matrix

All thrown models in `WeaponModel.OBJECTS` (`scripts/view/art/weapon_model.gd`) were verified against `tests/unit/test_model_scale.gd:117-128` (`THROWN_RANGE = [0.55, 2.8]`) at `CAMERA.kid_view_scale = 0.82`:

| Weapon ID | Kind / Shape | Damage / Heal | Cooldown (s) | Speed (m/s) | Sim Hit Radius / Width | `WeaponModel` Spec (m) | Drawn @ `0.82x` (m) | Drawn / Hit Diameter |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| `tirador_bato` *(Primary)* | `ATTACK` / `LINE` | `11` | `0.48` | `26.0` | `proj_r = 0.24` (`0.48 m` dia) | `0.34` (`meteor`) | `0.279` | `0.581` ✅ |
| `ult_bagsak` *(Ultimate)* | `ATTACK` / `CIRCLE` | `42` | `0.50` *(100% Ult)* | — | `radius = 4.2` | VFX Landing | — | — |
| `bato_light` | `ATTACK` / `TARGETED` | `18` | `4.8` | `20.0` | `proj_r = 0.25` (`0.50 m` dia) | `0.44` (`meteor`) | `0.361` | `0.722` ✅ |
| `bato_heavy` | `ATTACK` / `CIRCLE` | `36` | `10.5` | `24.0` | `radius = 2.65`, `delay = 0.78` | `METEOR_SIZE = 1.05` | `1.050` | Ground Meteor ✅ |
| `gunting_light` | `ATTACK` / `LINE` (x5) | `9` *(x5 = 45)* | `5.4` | `7.5` | `proj_r = 0.30` | `scissor` volley | — | — |
| `gunting_heavy` | `ATTACK` / `CONE` | `38` | `9.0` | `9.0` | `max_range = 3.4`, `delay = 0.35` | `scissor` rig | — | — |
| `papel_trap` | `CC` / `TRAP` | `0` *(1.5s Root)* | `9.5` | `18.0` | `radius = 1.35` | `0.54` (`crumpled_paper`) | `0.443` | Placed Trap ✅ |
| `papel_shield` | `BLOCK` / `SHIELD` | `0` *(3.0s Wall)* | `11.0` | `0.0` | `width = 3.6` | `paper` stretched | — | Wall ✅ |
| `tsinelas_light` | `ATTACK` / `BOOMERANG` (x3) | `9` *(out+back)* | `7.0` | `21.0` | `proj_r = 0.365` (`0.73 m` dia) | `0.52` (`slipper`) | `0.426` | `0.584` ✅ |
| `tsinelas_heavy` | `ATTACK` / `BOOMERANG` | `25` *(out+back)* | `9.0` | `24.0` | `proj_r = 0.58` (`1.16 m` dia) | `0.84` (`slipper`) | `0.689` | `0.594` ✅ |
| `lata` | `CC` / `SLOW_ZONE` | `14` *(1.8s Poly)* | `12.0` | `20.0` | `radius = 2.2` | `0.70` (`can`) | `0.574` | Zone Toss ✅ |
| `jacks` | `CC` / `FIELD` | `7` / `0.5s` *(4.5s)* | `10.5` | `19.0` | `radius = 2.65` | `SPIKE_WIDTH = 0.551` | — | 6-Cluster Field ✅ |
| `bola` | `CC` / `BOUNCER` | `14` *(x3 bounce)* | `9.8` | `23.0` | `proj_r = 0.30` (`0.60 m` dia) | `0.56` (`marble`) | `0.459` | `0.765` ✅ |
| `trumpo` | `CC` / `SPINNER` | `22` *(+3.2m pull)* | `11.5` | `22.0` | `radius = 0.68` (`1.36 m` dia) | `1.12` (`trumpo`) | `0.918` | `0.675` ✅ |
| `heal` | `HEAL` / `TARGETED` | `+34 HP` | `12.5` | `0.0` | `max_range = 10.0` | `CROSS_WIDTH = 0.38` | `0.312` | Heal Cross ✅ |
| `langit_lupa` | `BLOCK` / `WALL` | `0` *(3.4s Wall)* | `12.0` | `0.0` | `width = 4.2` | `soil_block` stretched | — | Earth Wall ✅ |

---

## 4. Client-Server `Snapshot` & `NetProtocol` Parity

- **Zero-Overhead Default Omission (`scripts/net/snapshot.gd`)**:
  - Per-player keys (`"wlk"`, `"cr"`, `"ay"`, `"vy"`, `"jcd"`, `"sll"`, `"sld"`, `"scd"`, `"bt"`, `"bl"`) are omitted whenever false/zero and quantized to `0.01` when active, adding **0 bytes** to idle/running packets.
  - `Snapshot.apply()` explicitly resets all 10 fields (plus `buff_regen_carry = 0.0`) to defaults when omitted, preventing stale crouch/slide/jump/buff state across reconciliation rollbacks.
  - Top-level `"pu"` serializes `sim.powerups` (`id`, `k`, `x`, `y`, `a`, `rl`) and falls back to `sim.reset_powerups()` if absent on legacy payloads.
  - `Snapshot.hide_for()` strips `"wlk"`, `"cr"`, `"ay"`, `"vy"`, `"sll"`, and `"sld"` alongside position/facing during Langit Lupa brownouts (`tests/unit/test_net_hardening.gd` privacy contract preserved).
- **Wire Precision & Server Tick Handling (`protocol.gd`, `server_match.gd`, `game_client.gd`)**:
  - `NetProtocol` quantizes unit-disc `m` and `a` vectors at **`0.001`** (`ROUND = 0.01` kept for world coords) so `0.45` (`Alt` walk), `0.50` (half-stick), and `0.62` (`walk_run_threshold`) survive network round-trips with `< 0.0005` error.
  - `ServerMatch.PULSE_BITS` clears one-shot `BTN_JUMP` (`1 << 11`) and `BTN_SLIDE` (`1 << 13`) when repeating last input on an empty client queue while keeping `BTN_CROUCH` (`1 << 12`) held.
  - `GameClient._on_stage()` uses `mirror.set_loadout_list(loadout)` so 3-superpower kits predict identically to the server, and `GameClient.interpolate()` smoothly blends remote `air_y`.

---

## 5. Kid Locomotion Postures & Presentation Sync (`@locomotion-anim`)

- **Preserved Invariants (`scripts/view/art/kid_model.gd`)**: `KID_HEIGHT = 1.85`; default `PlayerState.new()` at `speed = 5.0..6.0` keeps `current_animation() == "run"`, `_player.speed_scale == 1.0`, `_pose.active == false`, and `_body` at identity (`test_authored_kids.gd`, `test_native_animation.gd`, `test_kid_can_acts.gd`, `test_trip.gd`).
- **5 New Locomotion Animations (`KidModel` + `KidPose`)**:
  - `walk` (`state.walking`): upright child stride (`0.62..0.86` cadence, `0.72` stride scale, `0.52` foot lift, hip/shoulder counter-sway).
  - `airborne` jump + `0.18 s` soft knee-flex landing (`_body.position.y = state.air_y`, upward knee tuck on `vel_y > 0`, planted-ankle landing compression).
  - `crouch` (`CROUCH_PELVIS_DROP = -0.46 m` ~`0.70x` height, bent knees over planted/sneaking feet, `0.52..0.78` step cycle).
  - `slide` (`SLIDE_PELVIS_DROP = -0.60 m` street baseball-slide along `state.slide_dir`, extended lead leg `-0.80 m`, tucked trail leg).
  - `tripped` (`StatusEffects.Type.TRIPPED`): 3-phase forward stumble, arms-out catch, belly drop, and hands push-up recovery (`trip_step`).
- **Dual-Outline Harmony**: `INK_OUTLINE_COLOR = Color(0.13, 0.10, 0.12, 0.92)` at `INK_OUTLINE_WIDTH = 0.009` inside `OUTLINE_WIDTH = 0.026` team stencil rim (`STENCIL = 1`).
- **Locomotion FX (`PracticeMatchPresentation._sync_locomotion_fx`)**: Emits jump takeoff/landing dust (`JUMP_DUST = 4`), slide dust + ground streak (`SLIDE_STREAK_WIDTH = 0.48`), subtle local landing/slide shakes, and turquoise shallow-canal (`|z| <= 4.6`) wading/landing splash rings while respecting `Settings.reduced_motion`.

---

## 6. Automated Determinism & Static Typing Verification Receipt

Checked across all **21 `.gd` files** in the Combat / 3C / Net / Presentation perimeter:
- `scripts/view/{camera_rules,arena_camera,practice_match,practice_match_presentation}.gd`
- `scripts/view/art/{kid_model,kid_pose,weapon_model}.gd`
- `scripts/input/{match_input,pc_combat_input}.gd`
- `scripts/sim/{player_input,player_state,game_rules,match_sim,match_objectives,weapon_system,team_coach,fight_brain}.gd`
- `scripts/net/{snapshot,protocol,server_match,game_client}.gd`

| Check | Result |
| :--- | :---: |
| `:=` inferred assignments (`treat_warnings_as_errors = true` Variant hazard) | **`0`** |
| Untyped `var` declarations | **`0`** |
| Untyped `for` loop variables | **`0`** |
| Untyped `func` return signatures | **`0`** |
| Executable global `randf()` / `randi()` / `randf_range()` / `randi_range()` / `randomize()` in `scripts/sim/*.gd` | **`0`** *(only `_trip_rng.randf()` & `rng.`/`sim.rng.`)* |

---

## 7. Exact Unit-Test Impact Matrix for `@qa-gatekeeper`

The following existing unit tests pin pre-v2 data constants and should be updated by `@qa-gatekeeper` (with 100% explicit static typing):

1. **`tests/unit/test_camera_rules.gd`**:
   - **Line 77**: Update `CAMERA.kid_view_scale` pin from `1.75` to **`0.82`**.
   - **Lines 87 & 89** (`test_a_kid_is_still_big_enough_to_read_on_the_bigger_street` — legacy portrait whole-street projection): At `kid_view_scale = 0.82`, `tall ≈ 29.5 px` and `wide ≈ 15.8 px`; update `assert_gt(tall, 40.0, ...)` -> **`assert_gt(tall, 24.0, ...)`** and `assert_gt(wide, 24.0, ...)` -> **`assert_gt(wide, 12.0, ...)`**.
2. **`tests/unit/test_shoulder_view.gd`**:
   - **Line 46**: Update `rules.shoulder_distance` pin from `6.2` to **`4.15`**. *(Also recommend adding assertions for `shoulder_height == 1.88`, `shoulder_offset == 0.62`, `look_pitch_degrees == 15.5`, `shoulder_fov == 68.0`, and `sync_behind_character` overridden by `look(drag)` for `1.6 s`.)*
3. **`tests/unit/test_match_sim.gd`**:
   - **Line 22**: Update `RULES.move_speed` pin from `8.2` to **`6.4`** (`test_walk_at_half_tilt_moves_at_half_speed` at line 67 remains automatically green because `0.5 < 0.62` walks proportionally at `0.5 * 6.4 = 3.2 m/s`).
4. **`tests/unit/test_trip.gd`**:
   - **Lines 43–45** (`test_the_shipped_rules_turn_random_tripping_off` -> rename/align for re-enabled Overhaul v2 kid trip): Update `trip_chance` -> **`0.12`** (was `0.0`), `trip_interval` -> **`6.0`** (was `5.0`), `trip_seconds` -> **`1.05`** (was `1.2`).
   - Add regression checks verifying **analog walk (`Vector2(0.5, 0.0)`)** and **crouch (`BTN_CROUCH`)** never increment `run_time` or trigger `TRIPPED` even at `trip_chance = 1.0`.
5. **`tests/unit/test_stamina.gd`**:
   - **`before_each()` (Lines 12–16)**: Set **`_rules.trip_chance = 0.0`** on the duplicated `_rules` so 12-second continuous-run stamina drain assertions (`_walk(sim, 30 * 12)`) stay isolated from the newly re-enabled `0.12` random kid trip.
6. **`tests/unit/test_barangay_5v5.gd`**:
   - **Lines 352–366** (`test_tirador_primary_luksong_tinik_dash_bagsak_ultimate_and_street_sprint`): Replace the legacy `RULES.sprint_enabled == true` / `hero.sprinting` block with Overhaul v2 locomotion & objective checks: `RULES.sprint_enabled == false`, analog walk (`0.45` -> `hero.walking == true`, `0` `run_time`) vs. run (`1.0` -> `hero.walking == false`), `BTN_JUMP` (`hero.air_y > 0.0`), `BTN_CROUCH` (`0.55x` speed), `BTN_SLIDE` (`10.2 m/s` burst), and 3 `sim.powerups` (`PALAMIG`, `TEKS`, `TURON`). *(Note: also align lines 162–186 & 237–244 for `water_obstacles = []` walkable mid canal per `@audit-map`.)*
7. **`tests/unit/test_weapons.gd`**:
   - **Lines 68–82 (`expected` table)**:
     - `bato_light`: `[WeaponDef.Kind.ATTACK, 4.8, 20.0, 18]`
     - `bato_heavy`: `[WeaponDef.Kind.ATTACK, 10.5, 24.0, 36]`
     - `gunting_light`: `[WeaponDef.Kind.ATTACK, 5.4, 7.5, 9]`
     - `gunting_heavy`: `[WeaponDef.Kind.ATTACK, 9.0, 9.0, 38]`
     - `papel_trap`: `[WeaponDef.Kind.CROWD_CONTROL, 9.5, 18.0, 0]`
     - `papel_shield`: `[WeaponDef.Kind.BLOCK, 11.0, 0.0, 0]`
     - `tsinelas_light`: `[WeaponDef.Kind.ATTACK, 7.0, 21.0, 9]`
     - `tsinelas_heavy`: `[WeaponDef.Kind.ATTACK, 9.0, 24.0, 25]`
     - `lata`: `[WeaponDef.Kind.CROWD_CONTROL, 12.0, 20.0, 14]`
     - `jacks`: `[WeaponDef.Kind.CROWD_CONTROL, 10.5, 19.0, 7]`
     - `bola`: `[WeaponDef.Kind.CROWD_CONTROL, 9.8, 23.0, 14]`
     - `trumpo`: `[WeaponDef.Kind.CROWD_CONTROL, 11.5, 22.0, 22]`
   - **Individual HP, Cooldown & Timing Assertions**:
     - Line 135: `4.8` (was `4.5`); Line 137: `82` (was `80`)
     - Line 158: `82` (was `80`); Line 162: `64` (was `60`)
     - Line 245: `82` (was `80`)
     - Line 258: `64` (was `62`); Line 260 comment (`r = 3.4 + 0.45`); Line 269: `64` (was `62`)
     - Line 279: `82` (`100 - 2*9`, was `78`); **Line 282: `_wait(sim, 5.6)` (CRITICAL: was `5.2`, which is shorter than `gunting_light`'s new `5.4 s` cooldown)**; Line 284: `37` (`82 - 5*9`, was `23`)
     - Line 302: `62` (`100 - 38`, was `58`)
     - Line 342: `82` (was `80`)
     - Line 353: `100 - 3 * 2 * 9` (`46`, was `100 - 3 * 2 * 11`)
     - Line 363: `100 - 2 * 25` (`50`, was `100 - 2 * 32`)
     - Line 373: `86` (`100 - 14`, was `84`)
     - **Line 386: `assert_between(enemy.hp, 28, 44, ...)` (CRITICAL: 9 ticks of `7` leaves `37 HP`, outside old `20..36`)**
     - Line 399: `86` (was `84`); Line 402: `86` (was `84`)
     - Line 418: `78` (`100 - 22`, was `74`); Line 419: `78` (was `74`)
     - Line 462: `82` (was `80`); Line 466: `82` (was `80`); Line 497: `82` (was `80`)
     - Line 586: `34` (`heal.heal`, was `42`); Line 600: `74` (`40 + 34`, was `82`); Line 615: `84` (`50 + 34`, was `92`)
8. **`tests/unit/test_tap_move_net.gd` (Optional Coverage Addition)**:
   - Add round-trip `Snapshot.capture` -> `Snapshot.apply` assertion covering `walking`, `crouching`, `air_y`, `vel_y`, `slide_left`, `slide_dir`, `buff_type`, `buff_left`, and `sim.powerups` + brownout `Snapshot.hide_for` stripping.
