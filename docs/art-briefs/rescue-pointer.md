# RESCUE pointer

- **Kind and why:** 2D image.
- **Where the player sees it and for how long:** Under your team's tagged box while a teammate waits, bobbing.
- **Size and file format:** `assets/ui/hud/rescue_pointer.png`, 192 x 96 PNG with alpha (pill with an arrow up).
- **Silhouette test:** At half size: a pill with an upward arrow; the word is drawn by the game.
- **Palette (tokens):** `TeamColors.feed_own()` body, `RoyaleStyle.NAVY` outline.
- **Animation beats (30 Hz) or message:** The game bobs it (still with Reduce motion).
- **Stand-in it replaces:** `OverheadHud._draw_rescue_cue` (drawn pill and triangle).
- **How it is accepted:** Never overlaps the box's kids; readable at 390 px.
- **Priority:** P3.
