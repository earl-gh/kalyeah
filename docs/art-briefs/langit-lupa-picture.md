# Langit Lupa game picture

- **Kind and why:** 2D image. Flat art on the Games page card, the Practice game picker and the room's game carousel, like the other three games' pictures.
- **What it shows:** the game in one glance: a kid standing proudly on a wooden platform (the *langit*), arms up, while the taya reaches up from the street (the *lupa*) just short of tagging; a second kid jumping onto a mound of earth. Filipino street at dusk with one street lamp, a hint of the brownout. Original kids in the game's style (the owner's six kids, not another game's).
- **Where the player sees it and for how long:** every visit to the Games page and the Practice picker, and each time a host picks a game in a room.
- **Size and file format:** `assets/ui/games/langit_lupa.webp`, about **512 x 512** (the others are 335 to 512 px, square-ish; the cards crop to their own shape, so keep the kids in the middle 70 %), WebP with alpha if it has a cut-out edge like the others.
- **Silhouette test:** at 96 px (the picker button icon) the raised kid and the reaching taya still read.
- **Palette (tokens):** a sky-over-earth split (the stand-in's `StreetPage.STAND_IN_SKY` `#62BAFF` above, `STAND_IN_EARTH` `#8A5A32` below) gives the game its own colour; the room card's colour is `red` (`HotspotRoomView.MAP_COLORS`).
- **Animation beats or message:** static.
- **Stand-in it replaces:** `StreetPage._stand_in_picture` (a blue-to-brown gradient).
- **Drop it in:** add the file, then set its preload as the `"picture"` of the Langit Lupa entry in `StreetPage.STREETS` (Claude can do this).
- **How it is accepted:** the Games page and the room picker at 720x1280 and 390x844: it sits with the other three pictures without looking out of place.
- **Priority:** P1.
