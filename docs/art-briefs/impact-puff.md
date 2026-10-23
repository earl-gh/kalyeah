# Impact puff (any hit)

- **Kind and why:** Sprite sheet. Animated, keeps a painted 2D look in the 3D street.
- **Where the player sees it and for how long:** On every kid who is hit, about 10 times a minute, 0.35 s each.
- **Size and file format:** `assets/sprites/fx/impact_puff.png`, 8 frames of 256 x 256 in one row (2048 x 256), PNG with alpha.
- **Silhouette test:** Frame 1 at half size: a round burst with 6 chunky spikes; readable on grey cement and on a kid.
- **Palette (tokens):** Author white-to-warm; the game tints it toward `WeaponFxView.BURST_COLOR` with a `BURST_CORE` centre.
- **Animation beats (30 Hz) or message:** 30 Hz: f0 full-size flash, f1-f3 spikes fly out, f4-f7 fade to smoke. Reduce motion: f0 only, faded.
- **Stand-in it replaces:** `WeaponFxView._on_damaged` (sphere flash, box sparks, ground ring).
- **How it is accepted:** Frame strip (f0, f3, f6, f10) at 720x1280: the hit reads in its first 3 frames.
- **Priority:** P1.
