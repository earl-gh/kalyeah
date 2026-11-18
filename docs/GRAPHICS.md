# Kalyeah gameplay graphics (owner decisions)

How the match looks: world, models, detail levels and effects. The overall look, the 4 presets and enemy readability are
in `docs/SPEC.md` §10; kid mesh rules in `docs/art-briefs/kids-retopo-spec.md`. Where `docs/ART.md` describes the current
build differently (komiks ink, Arcane), this file wins and `ART.md` is due for a rewrite. Agents never make art; each
item says which kind of asset the owner makes.

## World look
- **Surfaces:** hand-painted textures with light realism: soft real lighting and shine on metal and water, clean shapes,
  little noise (like Valorant's Bind and Breeze). Asset kind: 2D images (textures) on Blender models.
- **Outlines:** none. Only the enemy highlight (red, yellow or purple) stands out; allies and props stay plain. The
  current komiks ink outline is removed.
- **Detail placement:** clean at kid height, rich above: where kids walk and shoot is easy to read; signs, wires,
  laundry and plants sit above head height and along walls.
- **Beyond the map:** simple low-detail 3D houses, trees and mountains fading into soft haze, so the town feels real
  around you. Asset kind: Blender models (low detail).

## Light and colour
- **Time of day per map** (fixed, always bright and fair): Barangay late morning; Palengke midday under skylights; Pier
  bright afternoon; Hacienda early-morning golden sun.
- **Shadows:** soft and light, never pitch black: a kid standing in shade is always visible (hiding is only in tall
  cover).
- **Palette:** warm and sunny; streets and buildings in calm, muted warm tones so kids and effects pop. Team red and blue
  are kept out of the world's colours.

## Living world
- Laundry, flags and leaves sway in the wind, above head height.
- Water flows and sparkles (river, sea, paddies).
- Neighbours on porches, chickens and dogs move outside the play area: never in the way and can't be hit. Asset kind:
  Blender models with simple animation.

## Models and detail levels
- **Shapes:** chunky, slightly rounded and simple: bevelled edges, thick walls, a few big readable forms; small detail is
  painted, not modelled (like Valorant and Fortnite). Asset kind: Blender models.
- **Cover reads the same on every map:** solid-looking cover (walls, crates, cars, containers) blocks stones and sight;
  soft-looking cover (laundry, cane, nets, ukay-ukay racks) blocks only sight, and stones pass through.
- **Detail levels:** 3 steps for kids (full up close, about half beyond 15 m, simple beyond 35 m) and 3 for buildings
  and props, swapped with a short fade so nothing pops.
- **Map size:** up to 150 MB per map pack. Barangay comes with the install as an install-time asset pack (outside the
  200 MB base app limit); the other 3 maps are on-demand packs.

## Kids in a match
- **Shading:** soft painted shading with a light rim from behind, so kids separate from walls (like Valorant agents and
  Fortnite).
- **Hair and cloth:** ponytails, skirts and sacks swing with physics on High and Ultra; Low and Medium use the baked
  animation only.
- **Faces:** kids blink, wince when hit and grin on a tag, and their mouths open and close on voice lines. This needs the
  7 face shapes of the model spec (now required, no longer optional).
- **No wear:** kids stay clean all match; skins look as bought.

## Effects
- **Skill style:** clean painted shapes built from the street-game objects themselves (slippers, marbles, garters,
  chalk) with simple trails, swirls and bursts; short and crisp, no huge particle clouds (like Valorant). Asset kind:
  sprite sheets for painted puffs and bursts, Blender models for the objects.
- **Ground marks:** every skill area is drawn like street chalk in team colour (blue yours, red the enemy's), always the
  exact size of the real area; delayed skills (Pabitin Snare, Lupa!) show the area filling up before they land.
- **Slingshot hits:** a small chalk-dust puff and a brief flash on the target; a distinct star-burst and sound on a
  headshot; a missed stone puffs off the wall and leaves a small chip mark that fades.
- **Readability limits:** no effect covers a kid's body for long; your own effects are slightly see-through so you can
  aim through them; every preset shows effects at the same size and shape (Low only uses simpler versions).
- **Ultimates:** big but brief: about 1 s of flair with a bigger version of the skill's object (a giant pot, a huge pog)
  and one strong burst, then it settles; no camera cut.
- **Bases and jails:** a chalk outline on the ground in team colour (from your view). While a base is being captured, its
  zone fills in the capturing team's colour like CODM's Control mode; defenders only see it if they look at it (no HUD
  warning, per the secret-capture rule), and the capturing team also sees a progress bar on their HUD.
- **Tag ("Taya!"):** about 1 s pouty freeze in a puff of chalk dust, then a poof to the jail.
- **Round-start barrier:** a glowing wall like Valorant's spawn barrier, dropped when the 5 s barrier phase ends.
