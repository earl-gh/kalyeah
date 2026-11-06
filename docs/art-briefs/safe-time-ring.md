# Safe-time ring (under your kid on a langit)

- **Kind and why:** 2D image (a ground decal). Flat on the street under the kid; the game animates how much of it is filled.
- **What it is:** a chalky ring under your own kid while it stands on a langit or a mound, showing its safe time (`langit_seconds` 3 s in all) draining round the ring like a clock; it turns from white to red in the last second. Only your own kid has it (the others' safe time is not yours to read at a glance).
- **Where the player sees it and for how long:** every time you climb a langit, up to 3 s; many times a match.
- **Size and file format:** `assets/textures/decals/safe_ring.png`, 256 x 256 PNG with alpha: a ring 1.6 m across at full size, about 0.15 m thick, hand-drawn chalk edge (like the chalk lines of `shaders/chalk_line.gdshader`). The fill is drawn by the game (one picture, cut round by angle in a shader).
- **Silhouette test:** at half size, the ring and its gap read under a kid's feet on top of a light platform (it needs a thin dark outline to show on the pale wood).
- **Palette (tokens):** chalk white (`StreetModeView.CHALK`), the last second in `TeamColors` enemy red; no other colour.
- **Animation beats or message:** the game drains it clockwise from full over the safe time; a short pulse at 1 s left. Reduce motion: no pulse.
- **Stand-in it replaces:** none yet (only the pad's "SAFE! 2 s" line); Claude wires the ring once the picture is in.
- **How it is accepted:** a render of a kid on a platform with 2 s and 0.5 s left: the amount left is readable without the pad text.
- **Priority:** P2.
