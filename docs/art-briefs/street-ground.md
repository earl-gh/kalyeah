# Painted street ground and mid-street wear

> Owner rule: the play area is plain cement with no tiles or markings. Wear only (stains, patches, cracks); any painted marking in the play area is the owner's call.

- **Kind and why:** 2D image. Flat, static art laid on the ground plane; the game lights it, so no depth or animation is needed.
- **Where the player sees it and for how long:** The whole match: the play area fills about 70% of the screen for every second of play.
- **Size and file format:** `assets/textures/cement.png`, 1024 x 2048 px PNG (1:2, laid once over the 24 x 48 m street, never tiled; 2048 x 4096 at most). Optional `assets/textures/street_wear.png`, 1024 x 1024 PNG with alpha: stains, patched squares and hairline cracks only.
- **Silhouette test:** At half size the street must read as one calm, dark grey surface; nothing on it may look like a line, an arrow or a zone (those are game marks).
- **Palette (tokens):** Greys around `StreetMap.CEMENT` (0.30, 0.30, 0.31); patches within +-8% of it; no team colours (`TeamColors.world_*` are reserved for bases and rings).
- **Animation beats (30 Hz) or message:** Static. Must not shimmer at 390 px wide: no high-frequency grain, soft hand-painted strokes.
- **Stand-in it replaces:** `shaders/street_cement.gdshader` (code-drawn blotches, patches and cracks).
- **How it is accepted:** Rendered at 720x1280 and 390x844: the kids, their rings, the bases and the landing circles are all easier to see than before; no visible repeat; web build grows by under 2 MB.
- **Priority:** P1 (seen all match).
