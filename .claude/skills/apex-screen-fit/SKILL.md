---
name: apex-screen-fit
description: >
  AAA Android multi-screen responsiveness, 21:9-to-4:3 aspect-ratio scaling, physical dp/DPI
  control constancy, dynamic asymmetric cutouts and gesture bars, runtime orientation flips, 3D
  vertical-FOV fairness, and automated multi-resolution bounds gates (Wild Rift × Call of Duty:
  Mobile × Fortnite Mobile). Use when configuring viewports, CanvasScaler/stretch modes, safe areas,
  orientation transitions, camera aspect framing, or responsive mobile HUD/menu reflow.
---

# Apex Mobile Screen-Fit & Viewport Engineering

Deliver **Riot (*Wild Rift*)**, ***Call of Duty: Mobile***, and **Epic (*Fortnite Mobile*)**
viewport constancy across every Android candybar, ultra-wide gaming phone, punch-hole flagship, and
inner foldable screen—zero clipped CTAs, zero inward-drifting sticks, and zero aspect-ratio cheats.

**Boundary (Canonical Owner):** Single source of truth for mobile aspect-ratio scaling (`21:9`
ultra-wide through `4:3` inner foldables), physical-mm (`dp`/DPI) control sizing, dynamic asymmetric
cutouts and gesture bars, runtime `Portrait` ↔ `Landscape` orientation flips, 3D shoulder-camera
aspect/FOV fairness, UI reflow, and automated multi-resolution bounds verification. Defers
thumb/ability-arc/cancel/overhead-bar combat UX to `apex-mobile-hud`, Unity URP/Prefab conversion to
`godot-unity-view`, and generic desktop/gamepad screen-stack patterns to `game-ui-ux`.

## 1. Android Aspect & Form-Factor Matrix (Beyond 16:9)

- **Landscape Match (`1280×720` ref):** Verify across `21:9` ultra-tall sideways (`934×400` /
  `2520×1080`), `19.5:9–20:9` mainstream notched (`844×390` / `2400×1080`), `16:9` budget
  (`1280×720`), and `4:3`–`5:4` inner foldables/tablets (`1024×768` / `2208×1768`).
- **Portrait Menus (`720×1280` ref):** Verify across `9:21` narrow (`360×840`), `9:19.5`
  (`390×844`), `9:16` short (`720×1280`), and `3:4` foldable/tablet (`768×1024`).
- **Menu container guardrails:** Clamp card, carousel, and 3-across weapon-tile grid max-width on
  `3:4` foldables/tablets so tiles never stretch edge-to-edge; enable vertical scroll or spacing
  compression on short `9:16` screens so bottom CTAs (`Play`, `Choose`, `READY`) never clip.

## 2. Scale Mode & Physical-Millimeter (`dp`) Thumb Constancy

- **Scale-axis locking:** Configure Godot (`canvas_items` + `expand`) and Unity 6 (`CanvasScaler`
  `Scale With Screen Size`) with `Match = 1.0` height-locked in landscape matches so vertical HUD
  stacks never overflow, and `Match = 0.0` (or `0.5` width-guarded) in portrait menus so `360 px`
  narrow phones never clip horizontally.
- **Corner-pinned controls (`21:9` vs `4:3`):** Pin the left stick and right ability arc strictly to
  **safe-area bottom corners**, never center-relative percentages—on `21:9` ultra-wides, controls
  stay physically reachable at the outer bezels instead of drifting inward; on `4:3` foldables, cap
  HUD cluster height (`<= 42%` viewport height) and enforce a minimum center gap so left stick and
  right arc never collide.
- **Physical `dp` floors:** Enforce `>= 48 dp` (`~9 mm`) touch targets (`>= 72 dp` primary fire,
  `>= 8 dp` gutters) via `DisplayServer.screen_get_dpi()` / `Screen.dpi` fallback-clamped to
  `[160..640]` so bogus `0` DPI reports never collapse or blow up controls.

## 3. Asymmetric Cutouts, Gesture Pills & Runtime Orientation Flips

- **Dynamic safe-area re-query:** Re-query `DisplayServer.get_display_safe_area()` /
  `Screen.safeArea` (converted into canvas units) on **every** window resize, `Portrait` ↔
  `Landscape` scene switch, and `LandscapeLeft` ↔ `LandscapeRight` 180° sensor rotation.
- **Per-edge asymmetric insets & edge bleed:** Punch-hole cameras swap left/right sides on 180°
  flips—apply true per-edge (`left`, `top`, `right`, `bottom`) asymmetric insets instead of
  symmetric hardcoded margins, reserve `16–24 dp` bottom gesture-pill clearance so stick/aim drags
  never trigger OS Home swipes, and bleed decorative background plates (`ConsolePanel`, menu
  backdrops) edge-to-edge behind safe-inset interactive children.

## 4. 3D Camera Frustum & Cross-Aspect Competitive Fairness

- **Vertical-FOV lock:** Lock **Vertical FOV** (`keep_height` on Godot `Camera3D` / Vertical FOV
  axis on Unity URP `Camera`) in 3P shoulder matches so `16:9` and `4:3` viewports never crop the
  `1.52 m` kid's feet, overhead bar, or nearby `Papel` traps vertically.
- **Ultra-wide cap & normalized crosshair ray:** Cap horizontal FOV or pillarbox extreme `> 21.5:9`
  ratios so split-screen or ultra-wide tricks cannot peek past cover unfairly. Project crosshair aim
  rays strictly from normalized active-camera viewport center `(0.5, 0.5)`, never hardcoded
  `(640, 360)` pixel coordinates.

## 5. Reflow, Typography & Automated Bounds Gate

- **Anchor reflow & bounded Komiks type:** Ban raw pixel placement (`position = Vector2(980, 540)`);
  use normalized anchors, aspect-ratio fitters, and scroll fade masks. Scale dark-outlined Komiks
  fonts (`KomiksStyle` / `UiKit`) within bounded `[18 px .. max]` auto-fit ranges with explicit wrap
  or ellipsis rules so badges and stat labels never overflow at `360 px` width.
- **Automated 4-aspect bounds gate:** Verify UI changes with headless multi-resolution bounds tests
  asserting **zero** sibling interactive rect intersections (`Rect2.intersects` / `RectTransform`
  world bounds) and **100%** safe-rect containment across `844×390` (with simulated `44 px`
  asymmetric cutout), `1280×720` (`16:9`), `934×400` (`21:9`), and `1024×768` (`4:3`)—plus portrait
  `360×840`..`768×1024` when menus change.
