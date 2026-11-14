---
name: apex-combat-cam
description: >
  AAA 3P over-the-shoulder camera rig, crosshair-to-origin parallax aim solver, kinetic street
  locomotion feel, and <150 ms telegraph/hit-confirm honesty (Valve Deadlock × Riot Valorant ×
  Call of Duty: Mobile). Use when building or tuning shoulder camera framing, wall whisker
  springs, walk/run/jump/slide/vault movement feel, projectile telegraphs, or hit feedback.
---

# Apex Combat & Shoulder Camera

Fuse **Valve (*Deadlock*)** 3P shoulder framing and kinetic street flow, **Riot (*Valorant*)**
peek/telegraph honesty, and ***Call of Duty: Mobile*** crisp hit confirmation for Kalyeah's 3D
barangay street brawls.

**Boundary:** Owns 3P over-the-shoulder camera framing, crosshair parallax reconciliation,
kinetic locomotion feel, cast telegraphs, and hit feedback. Leaves 3D world/materials/lighting
to `apex-street-world`, 2D HUD/touch zones to `apex-mobile-hud`, and netcode/60 FPS budgets to
`apex-net-frame`. Respect Kalyeah's strict sim/view split (`scripts/sim/` vs `scripts/view/`
or pure `Kalyeah.Sim` vs Unity View).

## 1. Right-Shoulder Framing & Parallax Solver

- **Lower-left hero framing:** Anchor the `1.52 m` kid (`kid_view_scale = 0.82`) in the
  lower-left third of the `1280×720` / `844×390` frame (behind and right of the shoulder) so
  the center crosshair and forward barangay horizon stay open. Keep `CameraRules.third_person`
  and `CameraRules.follow` fallback strictly data-driven.
- **Dual-ray parallax reconciliation:**
  1. Cast a primary camera ray through the screen crosshair to locate the world aim point.
  2. Clamp its XZ ground target to `WeaponDef.max_range` from the kid's authoritative sim XZ.
  3. Run a secondary muzzle/hand-to-target obstruction check so `Tirador` shots, cards, and
     thrown slippers/cans/rocks never clip unintended behind-the-back corners or low cover
     beside the camera.
  4. Inside `aim_no_direction_radius` (`0.1 m`), fall back cleanly to the kid's facing vector.
- **Whisker-spring wall collision:** Sweep a sphere-cast (`r >= 0.22 m`) from the kid's
  upper-back pivot to the camera socket. Pull in fast (`<= 40 ms`) on wall/post contact with
  zero inside-geometry clipping, ease out critically damped (`140–180 ms`), dither/fade the
  local kid mesh if compressed `< 0.75 m`, and ban FOV surges that cause phone motion sickness.

## 2. Kinetic Street Flow

- **Input buffering (`100–130 ms`):** Queue `BTN_JUMP`, `BTN_SLIDE`, `BTN_DASH`
  (`Luksong-Tinik`), and `BTN_ULTIMATE` (`Bagsak ng Kalye`) across landing/recovery frames so
  thumb presses never drop across `30 Hz` authoritative ticks.
- **Distinct velocity gears (drive stride playback from real horizontal speed, zero skating):**

| Gear | Sim Contract | Camera & View Read |
|---|---|---|
| **Steady Walk** | `stick < 0.62`, `0` trip rolls | Upright composed cadence, calm footstep audio |
| **Fast Run** | `stick >= 0.62`, `6.4 m/s`, `6.0 s` / `12%` trip | Athletic forward lean, dust kicks; `1.05 s` 3-phase stumble/belly-drop/push-up on trip |
| **Jump** | `5.6 m/s`, `16.5 m/s²` grav, `0.25 s` CD | Snappy takeoff/land squash; clearly shows `air_y >= 0.45 m` low-snare clearance |
| **Crouch** | `0.55×` speed, `0` trip rolls | Compact low tuck, quiet steps, smooth vertical camera settle |
| **Ground Slide** | `10.2 m/s` for `0.46 s`, `3.2 s` CD | Low street skid + dust wake without snapping crosshair pitch off-target |
| **`Luksong-Tinik`** | `18.5 m/s` for `0.24 s`, `4.2 s` CD | Explosive street vault + `SLOW`-cleanse flash and clean exit velocity handoff |

- **Aim stability:** Decouple cosmetic step bob and landing/trip vertical springs from camera
  yaw/pitch so crosshair tracking stays rock-steady mid-slide or mid-vault.

## 3. `<150 ms` Telegraph & Hit Honesty

- **Mandatory 3-channel cast read:** Every weapon and kit skill (`Tirador`, `Bato` light/heavy,
  `Gunting` light/heavy, `Papel` trap/shield, `Tsinelas` light/heavy, `Lata`, `Jacks`, `Bola`,
  `Trumpo`, `Bagsak` ult) must expose within `< 150 ms`:
  1. **Silhouette:** Distinct upper-body windup pose readable through team outlines at `35 m`.
  2. **Audio:** Sharp weapon-specific transient (`Tirador` rubber snap, `Lata` tin clatter,
     `Trumpo` cord rip) mixed above street ambience.
  3. **World boundary:** Team-colored (`blue` ally / `red` enemy, colorblind-safe local) cone,
     flight line, or ground landing rim (`arcane_fx_master` / `FxSketch`) matching exact sim
     hit bounds.
- **CoD Mobile hit-confirm & damage arcs:** Fire an instant `60–90 ms` crosshair tick + pitched
  click on confirmed hit (plus a distinct heavier sting when tagging an enemy at `0 HP`). Show
  incoming hits via a clean directional arc around the crosshair—never camera roll, red-jelly
  edge vignette blindness, or heavy screen shake (`Reduce motion` zeroes camera impulse).
- **Cosmetic-only hitbox parity:** All 10 kids (`junjun..tala`) share the exact
  `player_radius = 0.45 m` sim cylinder/circle, eye height, and cast frame windows. Never bind
  hurtboxes, cast origins, or camera height to cosmetic bone proportions.

## 4. Anti-Bloat Discipline

- Express camera offsets, whisker springs, look sensitivity, and telegraph timings as plain
  tunable floats/curves in data resources (`CameraRules`, `GameRules`, `WeaponDef`).
- Ban multi-layer virtual-camera director frameworks, procedural full-body IK state graphs, or
  cinematic timeline rigs inside match gameplay.
