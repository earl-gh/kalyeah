# Themed UI texture set (tarpaulin, jeepney trim, wood)

- **Kind and why:** 2D image. Flat, static surfaces for panels and buttons.
- **Where the player sees it and for how long:** Every menu and the match tray: tarpaulin for panels, jeepney chrome trim for the tray's top edge, painted wood for secondary buttons.
- **Size and file format:** `assets/ui/skin/tarpaulin.png`, `jeepney_trim.png`, `wood.png`; each 256 x 256, 9-slice with 48 px margins, PNG.
- **Silhouette test:** At half size each reads as its material, never as noise; text on it keeps 4.5:1 contrast.
- **Palette (tokens):** Tarpaulin: `UiSkin.PLATES` raised and night blues; trim: `RoyaleStyle.GOLD` / `GOLD_DARK`; wood: warm browns under `UiSkin.BUTTONS` slate.
- **Animation beats (30 Hz) or message:** Static.
- **Stand-in it replaces:** `UiSkin._render_plate` and the slate button face (code-drawn gradients).
- **How it is accepted:** UI gallery (Settings, developer) shows every component on the new skin with the measured contrast unchanged or better; no layout size changes.
- **Priority:** P2.
