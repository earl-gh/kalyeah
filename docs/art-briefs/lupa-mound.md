# Lupa mound (raised earth)

- **Kind and why:** Blender (3D model). It rises out of the street under a kid, and the kid stands on its top, so it needs a lit, rounded top and a real shadow.
- **What it is:** a hump of packed brown earth, like soil pushed up from under the street, with a few pebbles, two cracked chunks of cement on its sides, and a sprig of grass on top. Chunky and soft; a little cartoon *punso* (anthill mound) without the ants.
- **Where the player sees it and for how long:** when a kid taps LUPA (any kid but the taya), 4 s each (`mound_seconds`), every 12 s at most (`mound_cooldown`); with six kids, several are on the street most of the match.
- **Size and file format:** `assets/models/props/lupa_mound.glb` with `lupa_mound_prop_base_color.jpg`, the prop pipeline (one mesh, under 2k triangles, foot at y = 0, centred). **2.0 m across** at its foot (`mound_radius` 1.0 m), **0.45 m** high (`LangitLupaView.MOUND_HEIGHT`), flat enough on top (about 1.4 m) for a kid to stand.
- **Silhouette test:** at half size: a round brown hump, darker than a platform, clearly "earth" and not "wood".
- **Palette (tokens):** earth near `#8A5A32`, lighter dry soil on top, grey cement chunks; the sprig in a soft green (not a team colour).
- **Animation beats (30 Hz) or message:** the game scales it up out of the street over `MOUND_RISE` 0.25 s and back down when it sinks, with the [dirt burst](dirt-burst.md) at its foot. Model it at full size; the game does the rise.
- **Stand-in it replaces:** the owner's `sacks.glb`, fitted to the mound's size (`LangitLupaView.MOUND_MODEL`); it reads as a pale pile of sacks, not earth.
- **How it is accepted:** a render of a kid on its mound: feet on the top, the mound under the kid's shadow, readable on cement and on the asphalt patches.
- **Priority:** P1.
