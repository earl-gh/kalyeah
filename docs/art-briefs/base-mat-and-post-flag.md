# Base mat and team post with flag

- **Kind and why:** Blender 3D model. Seen from the 56 degree camera at both ends of the street, with real depth and shadow.
- **Where the player sees it and for how long:** Both bases, every second of play; the opening show looks at each one up close for about 2 s.
- **Size and file format:** `assets/models/props/base_mat.glb` (flat painted mat, 4.5 m radius circle cut by the street edges, under 600 triangles) and a flag that clips to the owner's `electric_pole.glb` (`assets/models/props/post_flag.glb`, under 800 triangles, one 512 px texture). Built with `tools/blender/build_prop_model.py`.
- **Silhouette test:** At half size: which base is whose from the flag's colour AND shape (yours a pennant, theirs a square flag, so colour-blind players read it too).
- **Palette (tokens):** Flag cloth tinted at runtime from `TeamColors.world_own()` / `world_enemy()` (author it white); mat edge chalky off-white, mat body neutral so the tint shows.
- **Animation beats (30 Hz) or message:** Flag sways 4 degrees over 3 s (the game drives it; still with Reduce motion).
- **Stand-in it replaces:** The code-drawn base circle fill and tape in `StreetMap._add_base_zone`.
- **How it is accepted:** Bases read second after the kids on the 720 and 390 renders; no new outline (owner); triangle budget test passes.
- **Priority:** P1.
