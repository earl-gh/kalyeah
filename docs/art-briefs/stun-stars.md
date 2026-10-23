# Stun stars

- **Kind and why:** Sprite sheet (billboard over the head).
- **Where the player sees it and for how long:** Over a stunned, airborne or knocked-down kid, 1 to 3 s.
- **Size and file format:** `assets/sprites/fx/stun_stars.png`, 12 frames of 128 x 64 (1536 x 64), looping, PNG with alpha.
- **Silhouette test:** At half size: three chunky five-point stars in a ring.
- **Palette (tokens):** `RoyaleStyle.GOLD` stars with a `RoyaleStyle.NAVY` outline.
- **Animation beats (30 Hz) or message:** One full turn over 12 frames (0.4 s), looping; with Reduce motion frame 0 held.
- **Stand-in it replaces:** The little 3D stars in `KidModel._build_stars`.
- **How it is accepted:** Readable at the 53 px kid height without covering the health bar.
- **Priority:** P3.
