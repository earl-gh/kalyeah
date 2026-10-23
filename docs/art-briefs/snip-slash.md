# Scissors snip slash

- **Kind and why:** Sprite sheet, laid on the ground along the cone.
- **Where the player sees it and for how long:** Every gunting cast, 0.3 s.
- **Size and file format:** `assets/sprites/fx/snip_slash.png`, 6 frames of 512 x 256 (3072 x 256), PNG with alpha, the cone pointing right.
- **Silhouette test:** At half size: two crossing blade arcs filling a 90 degree wedge.
- **Palette (tokens):** White blades, tinted at runtime by the thrower's `TeamColors.world_own()` / `world_enemy()`.
- **Animation beats (30 Hz) or message:** f0 both arcs at full length, f1-f3 they close, f4-f5 fade.
- **Stand-in it replaces:** The team-coloured wedge flash in `WeaponFxView._on_cone_struck` (the scissors model stays).
- **How it is accepted:** Frame strip: who threw it and where it hit in frames 0-2.
- **Priority:** P2.
