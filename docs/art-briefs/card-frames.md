# Card frames per kind and the card back

- **Kind and why:** 2D image. Flat, static card art.
- **Where the player sees it and for how long:** The hand (2 cards) and the shuffler window, the whole match.
- **Size and file format:** `assets/ui/cards/frame_attack.png`, `frame_control.png`, `frame_block.png`, `frame_heal.png`, `card_back.png`; 264 x 356 px (2x `MatchHud.CARD_SIZE`), PNG with alpha, 9-slice margins 40 px.
- **Silhouette test:** At half size the kind reads from the frame's shape mark (spike, swirl, shield, cross) and not only its colour.
- **Palette (tokens):** Kind colours from `WeaponPickScreen.KIND_COLORS`; outline `RoyaleStyle.NAVY`; name plate navy.
- **Animation beats (30 Hz) or message:** Static (the game draws the cooldown and the lift).
- **Stand-in it replaces:** `CardStyle.draw` / `draw_back`.
- **How it is accepted:** Card names keep 22 px; colour-blind render readable.
- **Priority:** P1.
