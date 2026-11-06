# Langit platform (Langit Lupa's safe spot)

- **Kind and why:** Blender (3D model). Kids stand on it, and the 45 degree camera sees its top, front and the shadow it throws on the street, so it needs real depth. This is advice for the owner's artist; nothing here is generated.
- **What it is:** a low, chunky Filipino street platform a kid would climb onto in *langit lupa*: a wooden *papag* (slatted bench-bed) or two stacked fruit crates with a plank lid. Soft, painted, Clash of Clans language: thick planks, rounded edges, a few nails and one tied rope, no fine grain.
- **Where the player sees it and for how long:** eight of them on the street for the whole 5-minute Langit Lupa match (`StreetModeRules.langit_spots` in `data/rules/langit_lupa.tres`); it is the thing every kid runs to.
- **Size and file format:** `assets/models/props/langit_platform.glb` with `langit_platform_prop_base_color.jpg`, through `tools/blender/build_prop_model.py` like the other props (one mesh, one material, 0.7 to 4k triangles, foot at y = 0, centred on the origin). Footprint **2.4 x 2.4 m**, top **0.35 m** above the street (`LangitLupaView.PLATFORM_HEIGHT`): a kid's knee. The game drawing kids at twice size (`kid_view_scale` 2.0) makes it about one kid wide on screen.
- **Silhouette test:** at half size (a 390 px wide phone) it reads as "a box I can stand on", lighter than the street, with a clear top edge. The chalk rim the game draws round it stays visible at its foot.
- **Palette (tokens):** warm sun-faded wood near `LangitLupaView.WOOD` (`#C68A4A`) on top and a shade darker on the sides; rope in `RoyaleStyle.CREAM`. No blue, no red: those are team colours.
- **Animation beats or message:** none on the model. The game sinks it (height scale to `WORN_HEIGHT_SHARE` 0.55) and darkens it toward `WORN` as it wears; see [langit-platform-worn.md](langit-platform-worn.md) for the worn look.
- **Stand-in it replaces:** `LangitLupaView._build_platform` (a plain box with a white chalk rim).
- **Drop it in:** add the `.glb` and its picture, then ask Claude to load it in `_build_platform` (keep the holder node, so sinking still works) and to pin it in `test_langit_lupa_screen.gd`.
- **How it is accepted:** a 720x1280 render of a Langit Lupa practice: eight platforms read as wood, a kid standing on one has its feet on the top (not sunk in, not floating), and the sinking from wear does not tear the model.
- **Priority:** P1 (seen all match).
