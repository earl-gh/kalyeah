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

## World reactions and feedback
- Small props (buckets, cans, hanging signs) wobble when a stone or skill hits them; no gameplay change.
- Wading kids leave visible splashes and ripples.
- Tall cover does **not** rustle around a hidden kid (only sound and Seeker skills give them away).
- **Hidden kids** look see-through with a faint outline to themselves and their team; enemies see nothing beyond 6 m.
- **Sound markers** (accessibility): small direction ripples at the edge of the minimap for footsteps and skill sounds
  within hearing range.
- **Taking hits:** a tiny screen shake and a red edge pulse, both softened by reduce motion.

## How the world is built (for phones)
- **Modular kit per map:** walls, roofs, windows and stalls as reusable pieces snapped together (about 40 to 60 pieces per
  map), like Valorant's maps. Asset kind: Blender models.
- **Texture atlases:** each map's props share 2 to 4 big texture sheets, so the phone draws them in fewer passes.
- **Bevelled edges** are modelled, so edges catch light and read chunky and soft.
- **Scene budget:** about 300k to 500k triangles on screen on High, 10 kids included (10 × 15k to 25k).
- **Ground:** painted ground blending 3 to 4 surfaces (cement, soil, wood planks, tin) with soft edges, plus painted
  decals (cracks, chalk, puddle stains); footstep sounds follow the surface. Asset kind: 2D images (tiling textures and
  decals).
- **Lighting:** soft bounce light and corner shading baked into the map on every preset, plus one real-time sun for live
  shadows on the presets that have them (`docs/SPEC.md` §10: hard near the camera on Medium, soft on High and Ultra;
  Low keeps blob shadows).

## Screen effects and presets
- **Post effects** (High and Ultra, per `docs/SPEC.md` §10): soft bloom on effects only, a gentle colour grade per map
  (warmer Hacienda, cooler Pier), and soft distance haze. No depth of field or motion blur.
- **Water:** painted and stylised, see-through near the edges, with sparkles and a simple sky reflection; no real-time
  mirror, so kids standing in it stay readable.
- **Effect caps:** each effect has a cap (e.g. at most 20 stone puffs at once); extra ones replace the oldest, so busy
  5v5 fights stay smooth.
- **Low:** half the particles with the same shapes and sizes; no living-world motion (laundry and background people
  stand still).
- **Ultra:** extra ambient life (more background people, birds, dust in sun rays).

## Skins and cosmetics on screen
- **Skin shape:** skins change clothes, hair and props but stay within the kid's default silhouette size, so enemies still
  recognise the kid and where the head is (hitboxes never change).
- **Sprays** are painted onto the wall's surface, following its texture and light like real paint; they fade at round
  end.
- **Slingshot skins:** a new slingshot model and a thin coloured stone trail with team-colour edges; Epic and higher also
  change the hit puff (same size); a slingshot skin can have its own sound when its owner tags someone.
- **Showcase:** home, kid select and the kid page use the detailed showcase model (up to 60k triangles), on a warm,
  softly backlit stage like Valorant's agent select; tapping the kid plays a short reaction or voice line.

## Animation feel
- **Movement:** snappy and playful with feet planted (no sliding), honest about where the kid really is, like
  Fortnite. Three movement animations, walk, jog and run, blending smoothly between the close speeds in `docs/SPEC.md` §7.
- **Slingshot:** the band pulls back further the longer the shot is held (enemies can see a long shot coming); the upper
  body aims while the legs keep running; the reload picks a stone from the pouch, quick but readable.
- **Vault and climb:** hands land on the ledge or box, matching the 0.4 s vault and 1 s climb.
- **Skills:** every skill has a short readable wind-up, the action, then a quick recovery, so enemies can react.

## Seeing your team and the map
- **Speed cues:** walking is quiet, jogging normal and running loud (crouch-walking silent); running on soil kicks up
  small dust puffs at the heels.
- **Teammates through walls:** a faint outline only, no name tag; a "teammate outline" setting turns it off (like CODM's
  teammate transparency option).
- **Full map** (tap the minimap): a clean, flat top-down drawing with short callout names, bases, jails, neutrals and
  teammates; enemies only when seen or revealed (like Valorant).
- **Jail marker:** a small icon with the count of kids inside over the enemy jail, seen through walls by the tagged
  kids' team only.

## Aiming and hits
- **Crosshair:** spreads a little when hip-firing and tightens to a dot in ADS; turns red over an enemy (no aim help
  beyond the aim-assist slow-down); while fire is held, a small marker shows how far the stone will fly (18 to 30 m).
- **Stone flight:** a visible stone with a thin trail, team-coloured at the edges, so both sides can read where shots
  come from.
- **ADS:** the camera slides closer behind the kid and the slingshot's fork lightly frames the view; no black scope, no
  first-person view.
- **Hit feedback:** an X marker on the crosshair (white for body, gold for a headshot), a bigger stamp when your hit tags
  someone, and different hit sounds for body and head.

## Neutrals, vendors and buffs
- **Dragon and baron animals:** friendly, cute and stylised while idle; when attacked they turn angry and grow into a
  bigger, scarier body. When beaten they shrink back and run off (no gore, never dying on screen). Asset kind: Blender
  models with idle, angry and flee animations.
- **Vendors:** a vendor character with a cart (Mang Taho with his yoke, the fishball cart with its stove) who can't be
  hit; the cart shows empty with a small timer during the 30 s restock; the kid plays a short eating animation while
  slowed, so enemies see who is healing.
- **Selecta bike:** an ice-cream cart pedalled by a vendor, bell ringing; can't be hit, caught by touching it.
- **Buffs:** an icon in your own HUD's buff row (like Wild Rift) and a circle aura at the kid's feet (like Wild Rift and
  Mobile Legends), so enemies can see who is buffed.

## Events, transitions and HUD style
- **Events show only through skins and cosmetics:** maps, light and cover never change for an event.
- **Round reset:** a quick fade with every kid ready at its base; no long animation between rounds. The side swap uses the
  10 s swap screen with the score and both teams' kids.
- **HUD style:** pure Valorant: clean flat shapes in the Ink / Paper / Red palette, no street or komiks flavour.
- **Jailed kids** sit pouting and slightly faded, so they read as "out" at a glance.

## Smoothness and camera handling
- **Busy fights:** when the phone struggles, far effects and background life reduce first, never the kids or anything
  that matters to gameplay; kids beyond 35 m update their animation less often. No dynamic resolution.
- **Loading:** all 10 kids' skins and the map load on the loading screen, so nothing hitches or pops in mid-match.
- **Walls behind the kid:** in tight spots the camera slides in smoothly; if it gets very close, your own kid turns
  see-through so the crosshair stays clear (walls never fade, so nothing behind them is revealed).
- **Follow:** tight with a tiny smooth lag: aiming feels direct and running still looks smooth.

## Map edges and spacing
- **Edges:** natural blockers only (fences, parked jeepneys, house walls, the river or sea, cane too thick to enter); no
  invisible walls without a visible reason.
- **Climb spots:** no special marking in the world; the jump button changes its icon when a climb is possible.
- **Tutorial barricades:** wooden barangay barricades (sawhorses and planks) with "Bawal Dumaan" signs. Asset kind:
  Blender model and a 2D image for the sign.
- **Spacing:** doors and alleys at least about 2 m wide, so two kids fit side by side and don't get stuck on each other.

## Team colour, skin tones and image quality
- **Team ring:** a thin team-colour ring under every kid's feet (blue allies, red enemies, from your view), kept subtle;
  skins stay untouched. A buff aura is a wider soft glow around it, so the two never look alike.
- **Skin tones:** lighting and colour grading keep every kid's Filipino skin tone warm and true, never grey, orange or
  washed out, on every map and preset; checked on screenshots of all 12 kids.
- **Texture size:** half size on Low (saves memory on old phones), full size on Medium, High and Ultra; shapes and colours
  identical.
- **Edge smoothing:** none on Low, a light cheap smoother on Medium and High, 4× MSAA on Ultra.

## Recognising kids
- **Who is who at about 30 m:** each kid has a distinct silhouette and one signature colour or prop (e.g. Narda's skirt,
  Paeng's sack) that every skin keeps.
- **Enemy highlight:** thin up close and a little brighter at distance, so far enemies don't vanish into the street
  (like Valorant).
- **Far kids (beyond 40 m):** the simplest detail level keeps the true size and main colours, never shrinking or hiding,
  so long shots stay fair.
- **Colour-blind options:** the enemy highlight colour (red, yellow or purple); team colours can swap from blue/red to
  blue/orange (rings, effects, bars); enemy skill edges are always dashed and ally edges solid, so colour is never the
  only signal.

## Materials, wear and signs
- **Materials:** a small fixed set per map, about 8 types (painted cement, raw wood, rusty tin, plastic, fabric, water,
  soil, glass), each with one clear look; light realism, no wet sheen or extra noise.
- **Wear:** lived-in but cared for: some rust on tin, paint chips and patched walls, painted by hand; never dirty or
  run-down (homes look loved, per the respect rule).
- **Glass and windows:** painted glass, shutters or grilles; never see-through or shoot-through, so cover stays clear.
- **Signs and text in the world:** made-up Filipino shop names ("Aling Nena's", "Bawal Umihi Dito"), hand-painted murals
  of fiestas and sports above kid height, parody brand ads and election-style posters. Parodies use invented names and
  logos only (never a real brand's name, logo or look-alike packaging), and posters show invented candidates and slogans
  only (never real people or parties). Asset kind: 2D images.

## Height, interiors, foliage and footing
- **Height:** roof, platform and container edges have a lighter trim so you see where you can stand and fall; a kid up
  high or mid-jump casts a soft drop shadow below, so height is easy to judge.
- **Interiors** (covered court, palengke stalls, nipa hut): slightly cooler and shaded, but kids keep their rim light and
  stay readable; no hiding in the dark.
- **Foliage:** chunky painted leaf clumps (like Valorant's Breeze) with few see-through cards; no thin noisy leaves.
- **Footing:** splashes when landing in water and puddles; a dust puff on soil when landing or sliding (none on cement or
  wood); no footprints.

## Building and checking the art
- **Source:** the owner makes models with **Meshy** (the 12 kids came from it), and agents may use licence-checked free
  assets (CC0: Poly Haven, ambientCG, KayKit, Quaternius; CC-BY with credit: Sketchfab), logged in `assets/CREDITS.md`.
  Agents make every model game-ready by script: cut it to the phone budget, fix scale, pivot and `+Z` facing, rebuild the
  texture layout into the map's atlas, and set the painted materials; the look itself is not restyled. Meshy meshes are
  dense and their textures carry painted-in light, so every one goes through this step before it enters the game.
- **Stand-ins:** until the owner's art arrives, matches use well-drawn grey-box blocks (code-drawn): clean, readable
  silhouettes at true size in grey and white faces only (like a level designer's blockout), so gameplay and lighting can
  be tested. They replace the script-made Barangay models (`assets/models/barangay/`, built from boxes by
  `tools/blender/build_barangay_kit.py`), which are retired; each block is swapped as its real model arrives.
- **Every asset brief** (in `docs/art-briefs/`) holds real Filipino reference photos, size in metres, the triangle and
  texture budget, pivot and facing rules (origin at the base, `+Z` front), style references (Valorant's Bind and Breeze
  shapes, Fortnite's chunky props, never copied) and a done checklist (script checks plus an in-game screenshot).
- **Order after the Narda pilot:** the Barangay modular kit and lighting first (the closed beta map), then the other
  kids in pairs, then the effects.
- **Each build's visual gate:** CI screenshots at `1280×720` and `844×390`; the same 6 to 8 fixed camera spots per map
  for easy before/after comparison; a lineup of all 12 kids side by side (sizes, skin tones, silhouettes).
- **When the owner's art arrives:** a script checks size, facing and materials and the triangle and texture budgets
  (this doc and `docs/art-briefs/kids-retopo-spec.md`), then an in-game screenshot is shown beside the owner's
  reference picture.

## Shader clarity architecture (VALORANT lessons)
Kalyeah's URP Mobile shaders (`Kalyeah/Kid`, `Kalyeah/TeamOutline`, `Kalyeah/BarangayGround`, `Kalyeah/EsteroWater`,
`Kalyeah/TropicalSky`, `Kalyeah/ArcaneFx`) apply Riot's gameplay-clarity shader pipeline while preserving URP SRP Batcher
compatibility (`UnityPerMaterial` CBUFFER parity across passes) and atomic Low-tier scaling via the global
`KALYEAH_QUALITY_LOW` keyword toggled by `MatchBoot.ApplyGraphicsPreset`:

- **Gradient Lambert single-sun diffuse (`Kalyeah/Kid`):** `NdotL * 0.5 + 0.5` is remapped through a 3-zone highlight /
  mid-tone / lifted core-shadow curve on the primary sun light only, preventing crushed black shadows without multi-light cost.
- **Fake skin SSS + warm shadow shift (`Kalyeah/Kid`):** darker shadow zones shift subtly warmer/redder and add a cheap
  grazing-shadow ZBrush-wax underglow away from direct sunlight so Filipino skin tones stay warm and readable in shade without
  extra texture taps.
- **Panoramic sun-hotspot specular (dropped on Low):** Medium/High/Ultra characters derive stylized specular from an analytical
  sky/ground hemisphere plus a sun hotspot aligned with `_KalyeahSunDir`, roughness-blurred from the ORM roughness channel
  (inverted roughness as specular control) with split diffuse vs. stronger specular indirect attenuation in shade. Under
  `KALYEAH_QUALITY_LOW`, character specular and normal/ORM texture taps drop completely while 100% of diffuse, ambient,
  Fresnel, and depth readability stays intact.
- **Clamped ambient bounds (`_KalyeahAmbientMin` / `_KalyeahAmbientMax`):** indirect SH/ambient lighting is clamped globally
  between safe floor and ceiling bounds so shaded interiors never hide a kid and bright sun never blows out silhouettes.
- **Up-facing + upper-body Friend-or-Foe Fresnel & clamped depth scaling (`Kalyeah/Kid`, `Kalyeah/TeamOutline`):** Fresnel rim
  highlighting is weighted upward (`N.y`) and toward the head/shoulders so targets pop from walls; enemies use the player's
  colorblind-safe `_KalyeahEnemyHighlightColor` (Red, Yellow, or Purple) while allies keep a subtle neutral-blue rim. Clamped
  depth brightening and distance Fresnel enlargement across 12 m–45 m keep distant kids readable without washing them out.
- **Aggressive environment Low scaling (`BarangayGround`, `EsteroWater`, `TropicalSky`):** because world geometry fills most
  pixels, `KALYEAH_QUALITY_LOW` aggressively strips multi-octave ground noise/specular layers, multi-wave water normals/foam,
  and extra sky cloud octaves while keeping clean matte lane/chalk readability, panoramic sun-aligned water reflections on
  Med–Ultra, and a serene 0°–25° shoulder horizon.
- **Hard-edge geometric VFX boundaries (`ArcaneFx`, zones, aim telegraphs, `FxPool`):** every skill perimeter renders with a
  razor-sharp, angle-independent geometric edge (solid for allies, dashed for enemies) and avoids stacked translucent smoke
  cards that break depth sorting. On Low, purely cosmetic secondary puffs halve in count and interior wisps strip away, while
  every gameplay radius, edge, and timing remains pixel-identical across all four presets.
