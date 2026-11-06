# LUPA button art

- **Kind and why:** 2D image (a HUD icon). Flat art on the round action button at the bottom right of the street games' pad.
- **What it is:** a small brown mound of earth bursting up with two clods flying, in the same round button style as Throw, Kick and Dive (today all four are a coloured circle with a word: `StreetGamePad._draw_button`). The word LUPA stays under or over it in Bungee.
- **Where the player sees it and for how long:** all match, under the right thumb, for every kid but the taya in Langit Lupa.
- **Size and file format:** `assets/icons/lupa.png`, 256 x 256 PNG with alpha, the art inside a 200 px circle (the button is 132 px across on a 720 wide screen, `BUTTON_RADIUS` 66).
- **Silhouette test:** at 66 px (the button on a 390 px wide phone) the mound shape reads with the word.
- **Palette (tokens):** the button's own face `StreetGamePad.LUPA_FACE` (`#C78C4D`) behind it; earth near `#8A5A32`; outline `RoyaleStyle.NAVY`. Greyed by the game while it cools down or while the safe time is used up (`BUTTON_OFF`).
- **Animation beats or message:** none on the art; the game fills the ring round it as it cools down.
- **Stand-in it replaces:** the plain brown circle with "LUPA" (`StreetGamePad._draw_button`). Throw, Kick and Dive would want the same treatment (one brief each later).
- **How it is accepted:** the pad at 720x1280 and 390x844: ready, cooling down (ring half full) and greyed all read under a thumb.
- **Priority:** P3.
