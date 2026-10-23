# D-pad art

- **Kind and why:** 2D image. A static control face with a pressed state.
- **Where the player sees it and for how long:** Bottom of the screen for the whole match, under the thumb.
- **Size and file format:** `assets/ui/controls/dpad.png` (400 x 400) and `dpad_arm_pressed.png` (one arm, 140 x 140, rotated by the game), PNG with alpha.
- **Silhouette test:** At half size: a bevelled cross in a round dish, four arrow notches.
- **Palette (tokens):** Dish `RoyaleStyle.NAVY` / `BLUE_WELL`, gold rim `RoyaleStyle.GOLD`, cross `RoyaleStyle.CREAM`, pressed `DPad.PRESSED_COLOR`.
- **Animation beats (30 Hz) or message:** Pressed arm swaps instantly (no animation).
- **Stand-in it replaces:** `DPad._draw_dish` / `_draw_cross` (code-drawn bevel).
- **How it is accepted:** Same hit area (`DPad.MAX_RADIUS`), left-handed mirror correct, 720 and 390 renders.
- **Priority:** P2.
