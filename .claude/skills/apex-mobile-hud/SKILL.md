---
name: apex-mobile-hud
description: >
  Canonical AAA mobile thumb-zone ergonomics, multi-touch fingerId locking, Wild Rift radial
  ability arc, drag/tap/cancel UX, and 10-kid overhead bars (Riot Wild Rift × Call of Duty: Mobile
  × Epic Fortnite). Use when designing or building match HUDs, touch controls, ability arcs, or
  overhead health bars.
---

# Apex Mobile HUD & Thumb UX

Fuse **Riot (*Wild Rift*)** radial ability arcs, cast/cancel clarity, and 5v5 HUD hierarchy,
***Call of Duty: Mobile*** zero-ms multi-touch ergonomics, and **Epic (*Fortnite*)** bold Komiks
legibility for Kalyeah on Android phones.

**Boundary (Canonical Owner):** Single source of truth for touch-zone combat layout, multi-touch
`fingerId` ownership, match HUD (`MatchHud`, `AbilityArc`, `HeroVitalsPanel`, `BarangayMinimap`),
cast/aim/cancel UX, and world-space 10-kid overhead bar hierarchy/stacking. Defers aspect-ratio
scaling, DPI/`dp` math, cutouts, orientation flips, and multi-aspect bounds matrices to
`apex-screen-fit`, 3P aim raycasting/gear physics to `apex-combat-cam`, coordinate/API translation
to `godot-unity-view`/`godot-unity-platform`, and frame/GC budgets to `apex-net-frame`.

## 1. Ergonomic Thumb Partitioning & Multi-Touch Concurrency

- **Touch-only discipline:** Ban hover-only states and desktop cursor chrome; delegate viewport
  aspect matrices, corner-pinned scaling, and physical `dp`/DPI sizing to `apex-screen-fit`.
- **Left-thumb locomotion zone:** Dedicate the lower-left quadrant to the floating/anchored stick
  (honoring `Settings` left-handed swap) with a crisp visual/haptic threshold step at
  `walk_run_threshold` (`0.62`, gear feel in `apex-combat-cam`): calm inner ring for **Walk**
  (`< 0.62`) and energized outer collar for **Run** (`>= 0.62`).
- **Right-half camera drag & radial ability arc:** Keep the upper/inner right half open for 3P
  camera look and arrange combat buttons along the natural right-thumb pivot arc:
  - **Primary anchor (`>= 72 dp`):** `Tirador` slingshot under the resting right thumb pad.
  - **Inner ability arc:** 3 chosen Superpowers—or the 4-card `Hand` + `NEXT` preview
    (`ConsolePanel` / `CardHand`) when `card_cycle` is active.
  - **Outer/satellite arc:** `Luksong-Tinik` vault, `Bagsak ng Kalye` ultimate (lit at `100%`
    `ult_charge`), plus claw-reachable `Jump` and `Crouch/Slide` satellites.
- **Multi-finger `fingerId` locking (2-thumb + 3-finger claw concurrency):** Keep inter-button
  dead-zone gutters (`apex-screen-fit` §2) and track every active contact strictly by persistent
  `touchId` / `fingerId`—**never `Input.GetTouch(0)` or array-index order**:
  1. Lock each `fingerId` on touch-down to one exclusive role (`Stick`, `CameraLook`, or a single
     `Ability/Satellite` control) until that exact `fingerId` lifts or cancels.
  2. Support simultaneous left-thumb stick + right-thumb camera drag + quick thumb transition or
     3-finger index-claw taps on `Jump`/`Slide` without stealing active drags.
  3. Handle OS `fingerId` recycling and out-of-order touch-up cleanly—drive camera yaw/pitch from
     that locked `fingerId`'s per-event delta (never raw pointer position) so pressing or lifting a
     claw/ability finger never snaps stick deflection or jerks camera aim.

## 2. Wild Rift Cast, Aim & Cancel Clarity

- **Quick-tap vs drag-to-aim:** Releasing on the button within `card_tap_time` (`<= 0.3 s`) fires
  auto-aim locked at press time (never tracking targets post-press). Dragging past the button dead
  zone projects the world aim guide (`MatchInput`), clamped to `WeaponDef.max_range` from the kid.
- **Zero-penalty cancel & OS-gesture safety:** Provide a clear red cancel target (`Release here to
  cancel` / drop-on-hand / out-of-bounds cancel) that turns the world aim guide red. Releasing in
  cancel—or losing touch to an Android back/home swipe, notification shade, app pause, or hard CC—
  aborts cleanly and spends **zero** cooldown or `Ice water` (`elixir`).
- **Truthful lockouts & zero precasting:** Render unaffordable (`elixir < elixir_cost`), cooldown,
  `no_cast_in_rescue_area`, `no_cast_in_enemy_base`, `Tagged`, or stunned/tripped buttons clearly
  greyed/badged. Holding an unavailable button across refill does nothing—require a fresh press.

## 3. Sacred Clear Center & Canonical Overhead Bars

- **Sacred `45% × 50%` center sightbox:** Keep the central crosshair and kid sightbox free of
  persistent HUD chrome or mid-fight banners. Pin `BarangayMinimap`, 5v5 Squad Wings, score/clock
  card, and `TakedownFeed` to safe outer corners; hold compact round/capture callouts `0.4 s` high
  above center. While `Tagged`, replace combat buttons cleanly with the rescue countdown ring and
  swap action (`D13`).
- **Canonical 10-kid overhead bar specification:** Render compact rounded-rect bars
  (`UiSkin.bar_radius`, `<= 8 px` height, `2 px` dark outline) with team-colored number badge
  (`blue` ally / `red` enemy, colorblind-safe local highlight; **ban ground rings under feet**),
  `You` tag on the local kid (never player names), gapless local stamina bar underneath, and **at
  most one** italic priority status word above (`Stunned` / `Tripped` overrides `Exhausted`).
  Vertically stack overlapping bars cleanly when kids clump in teamfights.
- **Safe-area, typography & motion handoff:** Apply dynamic asymmetric safe-area insets and bounded
  dark-outlined Komiks type per `apex-screen-fit`, and honor `Reduce motion` on UI transitions.

## 4. Anti-Bloat UI Architecture

- Build flat anchor + container trees, mutate HUD widgets strictly on state-change events/signals
  (never per-frame polling; frame GC rules in `apex-net-frame`), run menus on an explicit push/pop
  stack (Android Back closes modal -> page -> match settings/quit prompt; see `game-ui-ux`), and
  share centralized tokens (`UiSkin`, `UiMetrics`, `KomiksStyle`). Ban deep layout soup and MVVM.
