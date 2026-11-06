# Dirt burst (mound rising, platform breaking)

- **Kind and why:** Sprite sheet. A short animated puff that keeps the painted 2D look in the 3D street, like the [impact puff](impact-puff.md).
- **Where the player sees it and for how long:** at the foot of every mound that rises or sinks and every platform that breaks in Langit Lupa, several times a minute, about 0.3 s each.
- **Size and file format:** `assets/sprites/fx/dirt_burst.png`, 8 frames of 256 x 256 in one row (2048 x 256), PNG with alpha; drawn on the ground, about 2.4 m across at full size.
- **Silhouette test:** frame 1 at half size: a ring of brown clods and dust flying outward, low to the ground (wider than tall); readable on cement and on asphalt.
- **Palette (tokens):** earth `LangitLupaView.EARTH` for the clods, a paler dust; splinters of `LangitLupaView.WOOD` on the platform break if a second variant is wanted (or the game tints one sheet).
- **Animation beats (30 Hz):** f0 a flat ring at the foot, f1-f3 clods fly out and up, f4-f7 dust settles and fades. Reduce motion: f0 only, faded.
- **Stand-in it replaces:** `FxParticles.Preset.DUST` puffs in `LangitLupaView._on_spot_broke` (and none on the mound today).
- **How it is accepted:** frame strip (f0, f3, f6) at 720x1280 on a rising mound and a breaking platform: it reads in its first 3 frames and never hides the kid.
- **Priority:** P2.
