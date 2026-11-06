# Brownout street lamp flicker

- **Kind and why:** Sprite sheet (the flicker) on the existing electric posts. A short painted animation that sells the brownout as a real Filipino *brownout* moment.
- **What it is:** the street lamp head on each of the two electric posts: lit, flickering, and dark. It flickers twice and goes out when a brownout starts, and comes back with a flicker when the lights return ("LIGHTS ON!").
- **Where the player sees it and for how long:** at each brownout (45 s into a Langit Lupa match, then every 60 s, 10 s long): about 1 s of flicker at the start and the end.
- **Size and file format:** `assets/sprites/fx/lamp_flicker.png`, 6 frames of 128 x 128 in one row (768 x 128), PNG with alpha: f0 lit with a warm glow, f1-f4 flicker (dim, lit, dim, off), f5 dark.
- **Silhouette test:** at half size the lit lamp is a warm dot with a glow and the dark one a grey shape; both sit on the post top in the street's corners.
- **Palette (tokens):** a warm lamp glow close to `RoyaleStyle.GOLD`; the dark lamp in the post's own grey.
- **Animation beats (30 Hz):** going out: f0, f1, f0, f2, f4, f5 over about 0.6 s; coming back: the reverse. Reduce motion: no flicker, just lit or dark.
- **Stand-in it replaces:** none (today the screen just fades dark, `BrownoutShade`). Optional: a 2D *BROWNOUT* sign is not needed; the banner already says it.
- **How it is accepted:** a frame strip of the start of a brownout at 720x1280: the lamps going out read before the screen is fully dark.
- **Priority:** P3.
