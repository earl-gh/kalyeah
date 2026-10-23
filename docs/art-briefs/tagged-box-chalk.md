# Chalk rescue-area decal round the tagged box

> Owner rule: the box itself has no tint and no tape; this decal is only the rescue area round it.

- **Kind and why:** 2D image. A flat mark on the ground.
- **Where the player sees it and for how long:** Both corners with a tagged box, all match; the player looks for it whenever a teammate is tagged.
- **Size and file format:** `assets/textures/rescue_chalk.png`, 512 x 512 PNG with alpha, a 9-slice-friendly L-shaped band (the two street sides of the box).
- **Silhouette test:** At half size: a thick hand-drawn chalk band with a few arrows pointing into the box.
- **Palette (tokens):** White chalk, tinted at runtime by `TeamColors.world_own()` / `world_enemy()`.
- **Animation beats (30 Hz) or message:** Static.
- **Stand-in it replaces:** The rescue tape rectangles in `StreetMap._add_rescue_area` (`TAPE_WIDTH`).
- **How it is accepted:** The box reads as a place to run to at 390 px; it never covers a kid or a bar.
- **Priority:** P2.
