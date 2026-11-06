# Langit Lupa sounds

- **Kind and why:** Audio (the owner's own files, like `assets/audio/bgm.mp3` and the tag toast's sounds). Not one of the three art kinds; listed here so the game's new moments are not silent.
- **What they are:**
  1. **Lights out**: a power click and a short groaning "awww" of a crowd, as when the power cuts (about 1 s).
  2. **Lights on**: a click, a lamp hum and a short cheer (about 1 s).
  3. **Mound rising**: a soft earthy rumble and a pop of soil (about 0.3 s).
  4. **Platform breaking**: a dry wood crack and planks clattering (about 0.5 s).
  5. Optional: **safe time running out**: two quick ticks in the last second on a langit.
- **Where the player hears them:** each brownout start and end (every minute), each mound (several a minute), each broken platform (a few a match), on every phone.
- **Format:** `assets/audio/sfx/<name>.ogg` or `.wav`, mono, 44.1 kHz, peaking near -3 dBFS, no silence at the start (the game plays them on the frame of the moment through `SfxView`, at the 0.8 level of the other effects).
- **Stand-in it replaces:** none: today these moments are silent (the tag toast keeps its own sounds).
- **How it is accepted:** a Practice match on a phone with Sounds on: each moment is heard once, at its moment, not louder than a tag.
- **Priority:** P2 (lights out and on, mound), P3 (break, ticks).
