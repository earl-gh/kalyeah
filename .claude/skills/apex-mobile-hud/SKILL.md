---
name: apex-mobile-hud
description: >
  AAA mobile thumb-zone ergonomics, Wild Rift radial ability arc, drag/tap/cancel casting UX,
  sacred clear center sightbox, and safe-area HUD readability (Riot Wild Rift × Call of Duty:
  Mobile × Epic Fortnite). Use when designing or building match HUDs, touch controls, overhead
  health bars, or responsive mobile menus.
---

# Apex Mobile HUD & Thumb UX

Fuse **Riot (*Wild Rift*)** radial ability arcs, cast/cancel clarity, and 5v5 HUD hierarchy,
***Call of Duty: Mobile*** zero-ms thumb ergonomics, and **Epic (*Fortnite*)** bold stylized
Komiks legibility for Kalyeah on Android phones.

**Boundary:** Owns touch-zone geometry, in-match HUD (`MatchHud`, `AbilityArc`,
`HeroVitalsPanel`, `BarangayMinimap`), world-space overhead player bars, menu screen stacks,
typography, and safe-area insets. Leaves 3P camera raycasting to `apex-combat-cam`, 3D world
value bands to `apex-street-world`, and frame/GC budgets to `apex-net-frame`.

## 1. Ergonomic Thumb Partitioning

- **Target viewports & touch-only discipline:** Build match HUDs landscape-native at `1280×720`
  and verify down to narrow/notched `844×390`; build portrait menus at `720×1280` verified at
  `390×844` until landscape menu parity flips the manifest. Never depend on hover states or
  desktop cursor chrome.
- **Left-thumb locomotion zone:** Dedicate the lower-left quadrant to the analog stick
  (honoring `Settings` left-handed swap) with an unmistakable visual/haptic threshold step at
  `walk_run_threshold` (`0.62`): calm inner ring for steady **Walk** (`< 0.62`, `0` trip risk)
  and energized outer collar for **Run** (`>= 0.62`).
- **Right-half camera drag & radial ability arc:** Keep the upper/inner right screen half open
  for 3P shoulder camera look. Lay out right-thumb combat buttons along a natural thumb-pivot arc:
  - **Primary anchor (`>= 72 dp`):** `Tirador` slingshot under the resting right thumb pad.
  - **Inner ability arc:** 3 chosen Superpowers—or the 4-card `Hand` + `NEXT` preview
    (`ConsolePanel` / `CardHand`) when `card_cycle` is active.
  - **Outer/satellite buttons:** `Luksong-Tinik` vault, `Bagsak ng Kalye` ultimate (lit at
    `100%` `ult_charge`), plus `Jump` and `Crouch/Slide` satellites.
- **Misfire-proof touch locking:** Enforce `>= 48 dp` (`~9 mm`) hit rects, `>= 8 dp` inter-button
  dead-zone gutters, and strict finger-ID ownership at touch start: a drag starting on open
  screen owns camera look and never triggers an ability; a press starting on an ability button
  owns aim/cast and never jerks camera yaw.

## 2. Wild Rift Cast, Aim & Cancel Clarity

- **Quick-tap vs drag-to-aim:** Releasing on the button within `card_tap_time` (`<= 0.3 s`)
  fires auto-aim locked at press time (never tracking moving enemies after press). Dragging
  outside the button dead zone projects the world aim guide (`MatchInput`), continuously
  clamped to `WeaponDef.max_range` from the moving kid.
- **Zero-penalty cancel & OS-gesture safety:** Provide an unmistakable red cancel zone
  (`Release here to cancel` / drop-on-hand / out-of-street-bounds cancel) that turns the world
  aim guide red. Releasing in cancel—or losing touch to an Android back/home swipe, notification
  shade, app pause, or hard CC—must abort cleanly and spend **zero** cooldown or `Ice water`
  (`elixir`).
- **Truthful lockouts & zero precasting:** Render unaffordable (`elixir < elixir_cost`),
  on-cooldown, `no_cast_in_rescue_area`, `no_cast_in_enemy_base`, `Tagged`, or stunned/tripped
  buttons unmistakably greyed/badged. Holding an unavailable button while it refills does
  nothing—require a fresh touch once ready.

## 3. Sacred Clear Center, Overhead Bars & Safe Areas

- **Sacred `45% × 50%` center sightbox:** Keep the central crosshair and kid sightbox completely
  free of persistent HUD chrome or giant mid-fight banners. Pin `BarangayMinimap`, 5v5 Squad
  Wings, score/clock card, and `TakedownFeed` to safe outer corners; keep capture/round callouts
  high, compact, and held `0.4 s` against flicker. While `Tagged`, hide combat controls cleanly
  in favor of the rescue countdown ring and swap action (`D13`).
- **10-kid overhead bar discipline:** Render compact rounded-rect bars (`UiSkin.bar_radius`,
  `<= 8 px`, thin `2 px` dark outline) with team-colored number badge (`blue` ally / `red`
  enemy, colorblind-safe self highlight; **never ground rings under feet**), `You` tag on the
  local kid (no player names), gapless local stamina bar underneath, and **at most one** italic
  priority status word above (`Stunned` / `Tripped` overrides `Exhausted`). Vertically stack
  overlapping bars when players clump.
- **Dynamic safe areas & font floors:** Inset interactive controls and critical readouts inside
  `DisplayServer.get_display_safe_area()` / `Screen.safeArea` (re-applied on resize/orientation;
  decorative plates may bleed to edges). Enforce dark-outlined Komiks typography (`KomiksStyle`
  / `UiKit`) at `>= 18–22 px` reference floor and respect `Reduce motion`.

## 4. Anti-Bloat UI Architecture

- Use flat anchor + container hierarchies (`Control` presets / `RectTransform` anchors), drive
  HUD mutations strictly from signals/C# events on state changes (zero per-frame `_process` /
  `Update` polling), model menus as an explicit push/pop screen stack (Android Back closes
  modal -> page -> match settings/quit prompt), and share centralized tokens (`UiSkin`,
  `UiMetrics`, `KomiksStyle`). Ban deep layout-group soup and MVVM/reactive UI frameworks.
