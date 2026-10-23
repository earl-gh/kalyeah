# Rock landing crack

- **Kind and why:** Sprite sheet (laid flat on the ground), painted look.
- **Where the player sees it and for how long:** Where a heavy rock (bato heavy) or the meteor lands, a few times a minute, 0.5 s.
- **Size and file format:** `assets/sprites/fx/rock_crack.png`, 6 frames of 256 x 256 (1536 x 256), PNG with alpha, seen top-down.
- **Silhouette test:** At half size: a star-shaped crack with debris chunks.
- **Palette (tokens):** Dark cracks `RoyaleStyle.NAVY` at 60%, chips in cement greys; the landing circle keeps the thrower's team colour.
- **Animation beats (30 Hz) or message:** f0 crack appears whole, f1-f3 debris pops out, f4-f5 fade.
- **Stand-in it replaces:** Nothing yet (the landing circle only).
- **How it is accepted:** Reads at 390 px inside the team-coloured landing circle without hiding its rim.
- **Priority:** P2.
