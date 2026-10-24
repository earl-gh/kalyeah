# Painted street ground and mid-street wear

> Owner rule (round 11): the play area is the owner's tiled floor, `assets/textures/cement.png` ("use this as floor for gameplay"), soft beige-grey square tiles, no other markings. Delivered and accepted; this brief now only guides a replacement.

- **Kind and why:** 2D image. Flat, static art laid on the ground plane; the game lights it, so no depth or animation is needed.
- **Where the player sees it and for how long:** The whole match: the play area fills about 70% of the screen for every second of play.
- **Size and file format:** `assets/textures/cement.png`, laid once over the 27 x 48 m play area (18 x 32 tiles of 1.5 m), never repeated. The owner's file is 941 x 1672 px (9:16). A replacement that should sit on the 1.5 m grid: 18 x 32 square tiles, e.g. 1152 x 2048 px PNG (64 px a tile). Imported lossy WebP 0.7 with mipmaps (69 KB today). Optional `assets/textures/street_wear.png`, 1024 x 1024 PNG with alpha: stains, patched squares and hairline cracks only.
- **Silhouette test:** At half size the street must read as one calm, low-contrast surface (rendered luminance std about 4 to 7 out of 255); tile joints soft; nothing on it may look like a line, an arrow or a zone (those are game marks).
- **Palette (tokens):** Beige-greys rendered through `StreetArt.CEMENT_TINT` (0.6, 0.56, 0.51) to about (147, 137, 127) on screen; no team colours (`TeamColors.world_*` are reserved for bases and rings).
- **Animation beats (30 Hz) or message:** Static. Must not shimmer at 390 px wide: no high-frequency grain, soft hand-painted strokes.
- **Stand-in it replaces:** `shaders/street_cement.gdshader` on the play area (it still draws the sidewalks and the street past both ends).
- **How it is accepted:** Rendered at 720x1280 and 390x844: the kids, their rings, the bases and the landing circles read at least as well as on the old slab; no visible repeat; covers the play area exactly; web build grows by under 2 MB. The owner's floor passed (2026-10-24).
- **Priority:** P1 (seen all match).
