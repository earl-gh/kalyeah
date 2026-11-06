# Langit platform, worn and broken

- **Kind and why:** Blender (3D model), two variants of [the platform](langit-platform.md). The cracks and sag must catch the light from the 45 degree camera to say "this is about to give way".
- **What it is:** (1) **worn**: the same platform with a split plank, a sagging middle and one board sticking up; (2) **broken**: a low pile of snapped planks and a tipped crate, lying flat (nobody stands on it).
- **Where the player sees it and for how long:** a platform wears after `langit_wear_seconds` 10 kid-seconds of standing and breaks until the next round (`data/rules/langit_lupa.tres`); in a match most platforms reach worn and a few break, so both are seen several times a minute.
- **Size and file format:** `assets/models/props/langit_platform_worn.glb` and `langit_platform_broken.glb`, each with its `_prop_base_color.jpg`, same pipeline and footprint as the platform (2.4 x 2.4 m). Worn top at about 0.25 m; broken at most 0.15 m.
- **Silhouette test:** at half size the three (new, worn, broken) are told apart by shape alone: flat top, dipped top with a sticking-up board, a low messy pile.
- **Palette (tokens):** the platform's wood, darker toward `#6B4A2C`; bare pale wood in the fresh splits.
- **Animation beats or message:** the game swaps the model at half wear (worn) and at 0 (broken) and plays the [dirt burst](dirt-burst.md) on the break. Reduce motion: the swap only.
- **Stand-in it replaces:** today the game sinks the crates and turns their rim red, then hides them when they break (`LangitLupaView.sync`).
- **How it is accepted:** a frame strip of one platform wearing out (new, worn, the break frame, broken) at 720x1280: each state reads at once.
- **Priority:** P2.
