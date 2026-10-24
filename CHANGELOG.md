# Changelog

## [0.8.0](https://github.com/earl-gh/kalyeah/compare/kalyeah-v0.7.0...kalyeah-v0.8.0) (2026-10-24)


### ⚠ BREAKING CHANGES

* **map:** playing area is 1:2, 24 x 48 m, minimap 100 x 200

### Features

* **art,ui:** kids 2.0x with the world scaled to match; score pips and a status word kept on screen ([8d0199d](https://github.com/earl-gh/kalyeah/commit/8d0199d5e68dc06c6a364e11479f02f47de9906a))
* **art:** afternoon light in a cool tone instead of warm golden hour ([94cbff7](https://github.com/earl-gh/kalyeah/commit/94cbff7a43d7901e83cc6200fc382936fb7cfafc))
* **art:** golden-hour lighting and tone from the key art ([215d13b](https://github.com/earl-gh/kalyeah/commit/215d13bc76fb5e0b6aa9726497ccba91bd4846da))
* **art:** realistic proportions: kids 1.5x, scenery 1.7x, cover 2.4 m, posts 11 m ([2432ecd](https://github.com/earl-gh/kalyeah/commit/2432ecdd9ca068868336040d0a5a34006ed7e733))
* **balance:** bot-versus-bot duels and the first weapon numbers ([6c6e95a](https://github.com/earl-gh/kalyeah/commit/6c6e95ad849a719fb997856ab69d1f7313977cda))
* **boot:** a shorter loading base with the percentage centred on its top edge ([1f54153](https://github.com/earl-gh/kalyeah/commit/1f541538b6cdcd330c75d1fc027e57e8a191b995))
* **boot:** Clash Royale style loading screen with a black band, cardboard bar, Dagsen percent and a tip ([c2eaf01](https://github.com/earl-gh/kalyeah/commit/c2eaf014951c4d0ce3a1a455ea17d7a9fe25e13e))
* **boot:** matte black base, uncurved cardboard at the base's scale, Lost Tumbler percentage ([0ab33fc](https://github.com/earl-gh/kalyeah/commit/0ab33fc6f509a423cd7ef2b1783285494e71e55b))
* **boot:** no white bar, a cement-coloured base with the cardboard filling it ([abafbcb](https://github.com/earl-gh/kalyeah/commit/abafbcbddb281b9e7d2f41b997a6b1e81312c36a))
* **boot:** the loading screen uses the owner's new key art ([f580524](https://github.com/earl-gh/kalyeah/commit/f580524cc177c55e8dcb3f99b94793c6dbba729c))
* **boot:** the tip uses the Lost Tumbler font too ([b3e9107](https://github.com/earl-gh/kalyeah/commit/b3e9107388d0e6182083575ef4ce24294296a1dc))
* **bots:** aim at the enemy's distance, lead a moving target, never throw into cover ([19298e0](https://github.com/earl-gh/kalyeah/commit/19298e0122edb1de5d78f81fb861578fed0ed166))
* **bots:** defenders guard their base; harness can pick matchups (wip) ([75dc354](https://github.com/earl-gh/kalyeah/commit/75dc3549503812bcf308a19f1d68416ee8b9b6f5))
* **bots:** defenders may leave the base to rescue; harness sweeps tag release times (wip) ([7a4862a](https://github.com/earl-gh/kalyeah/commit/7a4862ab39b3528206b12635fb7e92d5fd2f78c1))
* **bots:** fight first, then hold the enemy base, then rescue ([a5c8613](https://github.com/earl-gh/kalyeah/commit/a5c8613f30f8317be335909fd7a06d7d8310e579))
* **brand:** Junjun's face is the app icon and the web favicon ([3d476e2](https://github.com/earl-gh/kalyeah/commit/3d476e2a2b4454637935b29c6a7f16cf7f821043))
* **camera:** 3:4 play area, higher forward-pitched camera and wide curve lens ([71a09ed](https://github.com/earl-gh/kalyeah/commit/71a09ed4054c33a84d186715710c845076c3dbc2))
* **camera:** a slight 8 degree yaw so the street stops looking like a corridor ([c6ed457](https://github.com/earl-gh/kalyeah/commit/c6ed45764d17b8d5f53095a597049f8f63c85c9f))
* **camera:** higher and pitched forward for a MOBA view ([36cf261](https://github.com/earl-gh/kalyeah/commit/36cf2619f800496e56f5f779392326b80bcca98c))
* **camera:** lean the yaw toward the hero's side, -8 at the left curb to +8 at the right ([dd18fbc](https://github.com/earl-gh/kalyeah/commit/dd18fbcc70d787da799d855adbd8b795f6337bd2))
* **camera:** optimise the camera for a vertical MOBA (42 deg, 60 deg lens, 10.6 m) ([9e9c448](https://github.com/earl-gh/kalyeah/commit/9e9c448eaff902fe40f6eacde0a27c3af9a8d838))
* **camera:** phone-style ultra-wide view, 75 degree fov with an almost invisible curve ([b1ff0be](https://github.com/earl-gh/kalyeah/commit/b1ff0be0259ad42a8feb716652b8a5d6883f8ca5))
* **camera:** remove the far blur; camera higher and farther from the kid ([97e95ff](https://github.com/earl-gh/kalyeah/commit/97e95ff2b3562601ae4f9e277f8255d284b256eb))
* **camera:** see the sides: kid at 52%, 13.5 m across, kids drawn 1.35x, aim peek, threat ticks ([14215e8](https://github.com/earl-gh/kalyeah/commit/14215e8ac34aa3709332e7da0fa89d0222eb190c))
* **camera:** soft focus beyond the far and near ends of the play area ([03b26dd](https://github.com/earl-gh/kalyeah/commit/03b26dd1b64011baf88faa10e783202dc6fe0fdc))
* **camera:** the Classic camera, a fixed view of the whole street like Clash Royale ([e079514](https://github.com/earl-gh/kalyeah/commit/e079514b826a1b999fbb0b24e8cb4e7357711ed8))
* **camera:** the gameplay camera is orthographic, tilted 45 degrees ([ad8ced5](https://github.com/earl-gh/kalyeah/commit/ad8ced5c9546a3d0d1e6f5b22940cb116d0bee8c))
* **camera:** tilt the fixed camera to 56 degrees like the Clash Royale reference ([e62eb1c](https://github.com/earl-gh/kalyeah/commit/e62eb1cfc8ee7682e250c0409816480eec70fc41))
* **camera:** try -8 degrees yaw (camera a little to the left) ([c4a2388](https://github.com/earl-gh/kalyeah/commit/c4a238859537ab5cbb1611a9d430650ec9441972))
* **hud,map:** no RESCUE bubble, no health for tagged kids, plain status font, 1:2 tagged box facing the enemy post, no environment shadows ([f5a4900](https://github.com/earl-gh/kalyeah/commit/f5a49005bb0acfac014b4104c211e18de1b442ae))
* **hud,map:** rounded bases, two-part rescue area with outlines in fixed team colours; no feed, no first-to text, health number, home gear ([dbf7fa1](https://github.com/earl-gh/kalyeah/commit/dbf7fa16af1e5e21342a3ddd77ab1e43817764b3))
* **hud:** Clash Royale bottom panel, tap to move, drag a card onto the street ([63c1589](https://github.com/earl-gh/kalyeah/commit/63c1589034862970a48f09682c3ff1ec39dc2efa))
* **hud:** ice water, a blinking overclock bar, rescue and capture feedback ([a5e3d9e](https://github.com/earl-gh/kalyeah/commit/a5e3d9e00dc8f0ba34a7466f32242323003d21f0))
* **kids:** Ligaya and Migo use the owner's rebuilt rigs; keep_uv option for the kid build ([21b24ae](https://github.com/earl-gh/kalyeah/commit/21b24aebe1e810e334cb9697e37ff8265af1ec53))
* **lobby:** 2:1 map card that opens a map carousel over a tint; one row for 1v1/2v2/3v3 ([61e20ad](https://github.com/earl-gh/kalyeah/commit/61e20ad5731032fbf88e01ad68f7b40dbf241b53))
* **lobby:** Back button then Lobby | code; frosted empty slots with a hint ([48cdd7e](https://github.com/earl-gh/kalyeah/commit/48cdd7e91b08e82992182b5aedf0e22ce316862a))
* **lobby:** hotspot create/join dialogs and the new hotspot room screen ([348b771](https://github.com/earl-gh/kalyeah/commit/348b771fa219137a73e528f60ac324dad6cc7abb))
* **lobby:** Lobby | code, Agawan Base, 3:4 map cards, hollow empty slots, rounded kid cards with shadow ([50a02d9](https://github.com/earl-gh/kalyeah/commit/50a02d996727779a6df63a8c42421dcb9a82c48c))
* **lobby:** numbered picture-less player cards; kid cards use the loading-screen faces ([2fe2bad](https://github.com/earl-gh/kalyeah/commit/2fe2badde9f324590313ec106189a6bbbbcb14ea))
* **lobby:** slimmer player rows, one spacing and type scale, Tumbang Preso and Patintero in the map list ([10ca933](https://github.com/earl-gh/kalyeah/commit/10ca93379d52a59a2920382091379b09534e1fc1))
* **lobby:** white player bars like the mock-up and the owner's six face pictures on the kid cards ([ab97ad8](https://github.com/earl-gh/kalyeah/commit/ab97ad8c5c910d4275dba7ee5d73ebd2dca215cc))
* **map,hud:** portrait tagged box, gentler corners, clear box inside the rescue ring, base rounded on all sides, smaller match gear ([778878d](https://github.com/earl-gh/kalyeah/commit/778878de3554c787161edebc718622cf3d7bc8ec))
* **map,hud:** tag boxes level with the base lines, balanced rescue ring, no hiding in the box ([4f3deed](https://github.com/earl-gh/kalyeah/commit/4f3deede9d727dc6e110dbd429eb32852c3ea5ea))
* **map:** flat sidewalks, level with the street ([7656edd](https://github.com/earl-gh/kalyeah/commit/7656edd642eea03154b69415d2c59ac0ebc2cee0))
* **map:** house 3 between the side buildings, plank fence and two roof walls on the end rows, narrower sidewalk ([e811609](https://github.com/earl-gh/kalyeah/commit/e8116092f93dfba85b28d6c573d677bc9c140bc6))
* **map:** lay the owner's tiled floor over the play area ([0ca20dc](https://github.com/earl-gh/kalyeah/commit/0ca20dc6f9f06ee019a7bddb78bbfe0146aeea19))
* **map:** no buildings across the ends; barrier props scaled to the kids ([5d8ff2b](https://github.com/earl-gh/kalyeah/commit/5d8ff2b4f7444af530c96f51627eb03787af3c13))
* **map:** playing area is 1:2, 24 x 48 m, minimap 100 x 200 ([a485fc9](https://github.com/earl-gh/kalyeah/commit/a485fc921c3cb03cf99e9bae3cfa34278bab20e6))
* **map:** road and sidewalks continue past the ends; the basketball pole lies across the row ([eb45441](https://github.com/earl-gh/kalyeah/commit/eb454418f979fd8afe30f5233cc97bdc08cdf88c))
* **map:** sidewalk cat, dog, chicken cage and drainage covers; tricycle terminal in the left row ([d5c9b9b](https://github.com/earl-gh/kalyeah/commit/d5c9b9b848f074ee9113d0a7503114302a827d6a))
* **map:** spaced, rescaled boundary props; leftovers on the far road; houses repeat beyond the ends ([139d8e8](https://github.com/earl-gh/kalyeah/commit/139d8e80512b2878a561e26fc00b198006dc93a4))
* **map:** the karinderya fills in between the side buildings ([030efb8](https://github.com/earl-gh/kalyeah/commit/030efb8c08b6c53859541d5623d8243d442eab33))
* **map:** the owner's floor covers all the ground, past the barriers and under the sides ([e6fa8f1](https://github.com/earl-gh/kalyeah/commit/e6fa8f1483a0670ad3928673bfc7cfed4af8bd40))
* **map:** the owner's poste, Nena store, vulcanizing and barber shop models replace the pole and the side buildings ([9892866](https://github.com/earl-gh/kalyeah/commit/9892866c9f87323b41236295dc4a0337ae783447))
* **map:** the owner's two houses fill in between the shopfronts ([0ff709a](https://github.com/earl-gh/kalyeah/commit/0ff709a43bde17e685b99622e083b6e0198975d2))
* **map:** the street is an 18 x 32 tile grid of 1.5 m tiles, Clash Royale's arena ([4e8a380](https://github.com/earl-gh/kalyeah/commit/4e8a38051102b0a5bd3f046db65be16d139a89c6))
* **map:** the tagged box is a rounded tinted square, set in from the edges, no cardboard ([922df08](https://github.com/earl-gh/kalyeah/commit/922df081f41813cfcc7f1a41601377c25f6eeebc))
* **menu:** 2:1 cards with heading top left and name bottom left, all modes visible, Inter for regular text ([eb9e0ac](https://github.com/earl-gh/kalyeah/commit/eb9e0ac98868b3b4182dab1e1ad0753cda169b89))
* **menu:** action-phrase details, details level with the chosen card ([d371577](https://github.com/earl-gh/kalyeah/commit/d371577ab213864af2431b8a92da1e6ea95e73c3))
* **menu:** bottom nav bar with characters, weapons, battle, history and menu tabs ([b1c30aa](https://github.com/earl-gh/kalyeah/commit/b1c30aac4a4b0e2912c673308f1e4ec42dd428a5))
* **menu:** dark blue cement bottom bar like the reference, big open-tab icon rising past the bar ([817338c](https://github.com/earl-gh/kalyeah/commit/817338ceb63cf69ba3d00da3c20ec225db28d65a))
* **menu:** darker matte blue platform, rounded corners, map title on the platform's front ([560f1f1](https://github.com/earl-gh/kalyeah/commit/560f1f1163bc5e83ebf25b71573c1d152171020e))
* **menu:** flat gray edge-to-edge bottom bar with icon tabs, only the open tab named ([a3955e4](https://github.com/earl-gh/kalyeah/commit/a3955e4fb8c3e32dc26241b38b2b2fef17d28e92))
* **menu:** horizontal 3:4 mode carousel, a section of page buttons, no bottom nav bar ([709f852](https://github.com/earl-gh/kalyeah/commit/709f8521957c5726090f454355e32a573411860a))
* **menu:** larger 3:4 cards with depth (shaded, higher cards behind) ([36439bb](https://github.com/earl-gh/kalyeah/commit/36439bbe4c3e281393a1455e62db28b2d7f4d791))
* **menu:** larger cards; cards behind are clearly darker and slightly see-through ([b89f94b](https://github.com/earl-gh/kalyeah/commit/b89f94bdcfc5e993dfb277ac4787c12afcea6253))
* **menu:** logo top left, book and history left of the cards, cards clipped so they never cover them ([e4c4b0f](https://github.com/earl-gh/kalyeah/commit/e4c4b0fbc9aae689a3c507a4b6360232108641f2))
* **menu:** mode and map cards with a carousel and START at the right ([d67dd46](https://github.com/earl-gh/kalyeah/commit/d67dd4622ea16f1b46e22e6e85f3598426b41f52))
* **menu:** mode cards are 2.5:4 ([c8e1c55](https://github.com/earl-gh/kalyeah/commit/c8e1c55859a7f71298b6a4cf008abf4b487aab5f))
* **menu:** mode carousel (hotspot, online, practice) at the left edge, loading-screen K! logo ([98c6c54](https://github.com/earl-gh/kalyeah/commit/98c6c54d97ee5bc313a6f7d10c8c016a2d029eb5))
* **menu:** new main screen with K! logo, settings, vertical map carousel and details with action buttons ([2f0eeba](https://github.com/earl-gh/kalyeah/commit/2f0eeba390a007232c0a84a424df40248ed66771))
* **menu:** online subtitle reads Invite friends online. ([89bd658](https://github.com/earl-gh/kalyeah/commit/89bd6587a52e7679905829374fd04cd5c9cf9a96))
* **menu:** plain mode cards, one-line details, create and join in one row, no map button ([47a7219](https://github.com/earl-gh/kalyeah/commit/47a721957d703e29b9fe1218fc2ab1253cf3eb00))
* **menu:** remove the K! logo and settings icon from the top of every page ([7533d53](https://github.com/earl-gh/kalyeah/commit/7533d539b7415c1685b889e4bc556d0369a8bc3d))
* **menu:** the bottom bar is a castle stone slab wall, one slab per tab ([9368054](https://github.com/earl-gh/kalyeah/commit/93680542f5e7da7ee13961e1cc6dbd7c8025b854))
* **menu:** the bottom bar is light gray cement (code-drawn shader) ([9bdf3d7](https://github.com/earl-gh/kalyeah/commit/9bdf3d7bfaab10b4cdc58e6c5d3b124982a079be))
* **menu:** the bottom bar's tabs respect the side margins with a partition at each end ([69ef5d0](https://github.com/earl-gh/kalyeah/commit/69ef5d02bb1bea216e22d31cff3b5bb686ba5065))
* **menu:** the Kalyeah logomark at the centre top of the home page ([c2b7212](https://github.com/earl-gh/kalyeah/commit/c2b72122c0214469778e1a92d76b127b8c67df73))
* **menu:** the main-screen map model is the top half only, a 1:1 square ([327b499](https://github.com/earl-gh/kalyeah/commit/327b4991e4da0e725a23a852a8a48093abdeff3a))
* **menu:** the map model is thick underneath and sits on a thicker blue platform ([73c036c](https://github.com/earl-gh/kalyeah/commit/73c036c671ed974a11c5526f05a28356750d1d19))
* **menu:** the Menu tab is now Settings, with the settings in the page ([c7e38c3](https://github.com/earl-gh/kalyeah/commit/c7e38c3ea057950ef0aee1907a84c00b256d14b4))
* **menu:** the mode name and phrase are centred between the cards and the action buttons ([acfa36b](https://github.com/earl-gh/kalyeah/commit/acfa36b2ce637a5a84761c8434eab09ab77d368e))
* **menu:** the owner's KALYEAH! logo at the centre top instead of the K! ([b8dbc32](https://github.com/earl-gh/kalyeah/commit/b8dbc32b052ea9cc3ec5281ef3dc0f18cc191651))
* **menu:** the owner's three pictures are the mode cards, with no text ([bbcc775](https://github.com/earl-gh/kalyeah/commit/bbcc77572bf7e058b305efc957c739d0539b933d))
* **menu:** top row with book, logo, history and settings; larger always-visible stacked cards; actions low ([1c85a8c](https://github.com/earl-gh/kalyeah/commit/1c85a8cec8e9ac95dcefea053f97ce6693f530ec))
* **net:** hotspot join needs no room code, the app finds the host on the hotspot ([08d706f](https://github.com/earl-gh/kalyeah/commit/08d706fc987887637075d941b3ea22fc624800d4))
* **net:** hotspot rooms hosted by the phone, joined with the room code alone ([cbde229](https://github.com/earl-gh/kalyeah/commit/cbde229c98fd6923b388a6887f01fdffe59d4e03))
* **net:** protocol 3 for tap to move, hand of 4, elixir and stamina ([715f0d7](https://github.com/earl-gh/kalyeah/commit/715f0d787361631c5ab58bf1cf219c1e23d33708))
* **practice:** a 3v3 match against bots (TeamCoach), Play vs bots and Training on the Practice card ([59bc4ac](https://github.com/earl-gh/kalyeah/commit/59bc4ace0d7157d2eb1dcf2e6ed3a1920b2ef8e3))
* **practice:** a sparring partner that fights back (FightBrain) ([82342eb](https://github.com/earl-gh/kalyeah/commit/82342ebce9374a98eb5788168619314dacd512fb))
* **practice:** results card says how each point was won; enemy bots attack in waves; docs ([0e2aa9e](https://github.com/earl-gh/kalyeah/commit/0e2aa9e4ae69c3e6edc3372c33457c0ad8db6348))
* **props:** rebuild the shopfronts, houses, fences and post smoother and sharper ([3ec103a](https://github.com/earl-gh/kalyeah/commit/3ec103a91ba5e767fba956772d94bcd15a3eb573))
* **rules:** contested bases, no team-0 tie-break, overtime and timed box release (wip, tuning pending) ([cc96cf7](https://github.com/earl-gh/kalyeah/commit/cc96cf7edd667afedf48b862fe99f769cd0af422))
* **rules:** contested bases, no team-0 tie-break, overtime, timed box release, TeamBrain and the round harness ([70a3b72](https://github.com/earl-gh/kalyeah/commit/70a3b729de1d79d8adb21d549185b1aac8c6603b))
* **rules:** elixir refills at 0.6 a second, tuned in bot rounds ([8424e38](https://github.com/earl-gh/kalyeah/commit/8424e385a638af2cf0a4142816e092e685f2d71f))
* **rules:** hard-CC immunity and a no-damage warning on the weapon pick (wip, weapon tuning pending) ([bacd214](https://github.com/earl-gh/kalyeah/commit/bacd214e59cada26c5a9c6d5eb5c54d7d27fdb81))
* **rules:** the match is first to 3, 5 or 7 points instead of best of 3, 5 or 7 ([cdf0c95](https://github.com/earl-gh/kalyeah/commit/cdf0c9520460982158ba7b37b165c63de37501e2))
* **settings:** no server address anywhere players can type it ([288b0df](https://github.com/earl-gh/kalyeah/commit/288b0df2866cabca4ee2f8bd4d35dc318e029606))
* **sim:** bots walk by move targets, cast affordable cards at points, rest to refill stamina ([b49972d](https://github.com/earl-gh/kalyeah/commit/b49972d3ea12ed6603dddf3dd381e2f687596ca7))
* **sim:** overclock walking, a 5 s rescue, a solo-or-team capture, no casting in the enemy base or rescue area ([7504214](https://github.com/earl-gh/kalyeah/commit/7504214e02ac1e98aff590b2be2295b87d1e5171))
* **sim:** tap to move, a hand of 4 paid with elixir, stamina ([8d0d717](https://github.com/earl-gh/kalyeah/commit/8d0d717fba1d077933b82683cf9dd51508d5dead))
* **ui:** a balanced header with the clock chip and keyed score cells, and a health plate on the deck ([b89e42d](https://github.com/earl-gh/kalyeah/commit/b89e42ddfde8c4efc2384efbdda955588a638bf0))
* **ui:** a direction tick on the D-pad and your health in the console (U9, U3) ([16c6162](https://github.com/earl-gh/kalyeah/commit/16c6162374570dbfa24a75a2a6e6a28272fd9705))
* **ui:** a Home that fits every phone, and a faster skin (round 9) ([18187c8](https://github.com/earl-gh/kalyeah/commit/18187c8bcac744fe19df252023bf4990de71413e))
* **ui:** a modern controller deck and an Xbox Series style D-pad ([d1f0320](https://github.com/earl-gh/kalyeah/commit/d1f03206b6a49d56bb2452542823bb8cc1d52910))
* **ui:** a modern header deck at the top of the match, matching the controller deck ([3b6cebe](https://github.com/earl-gh/kalyeah/commit/3b6cebe9d3edfd35885567cecac2d5343480bc36))
* **ui:** a readable minimap with three sizes, kinds in words, labels on a pill (round 17) ([e70fd64](https://github.com/earl-gh/kalyeah/commit/e70fd64230c495c20d6e2d3f773184aa0cd20083))
* **ui:** a results card at the end of the match ([e0be217](https://github.com/earl-gh/kalyeah/commit/e0be2174afcf6200d8789318640a34774a746546))
* **ui:** a wider score block so FIRST TO has room ([818ca97](https://github.com/earl-gh/kalyeah/commit/818ca976a9d571e47e61176bb61265aeeb1235e8))
* **ui:** add reading size and reduced motion preferences ([0361931](https://github.com/earl-gh/kalyeah/commit/036193158d69e34cc5b2b94ae51c0f9e208f1930))
* **ui:** audit P0 and P1: fitting room and loadout, Practice first on web, pause and confirm in Practice, no create form ([9237f5d](https://github.com/earl-gh/kalyeah/commit/9237f5df780dda721e861c5c20cc5deb0afd4595))
* **ui:** audit P2: skippable intro, clearer room status, host toast, code Clear, How to play Back, History play button, one status name, separate skill wells ([79f1f50](https://github.com/earl-gh/kalyeah/commit/79f1f500e104ef891977fe74001a00c1ae7db613))
* **ui:** both fonts enlarged about 20% for readability ([b8f3928](https://github.com/earl-gh/kalyeah/commit/b8f39284aee3fe9a1f6449d778162b94afb3a072))
* **ui:** bottom nav with Street, Play and Settings, Street page, sentence-case typeface ([922aa82](https://github.com/earl-gh/kalyeah/commit/922aa821057fbef08b83209ede8f37d30a537b1e))
* **ui:** Bungee for everything ([48530e5](https://github.com/earl-gh/kalyeah/commit/48530e51da181fb9d04cc6ced497acaa9d48033a))
* **ui:** Bungee replaces Londrina Solid as the display font ([2515e5e](https://github.com/earl-gh/kalyeah/commit/2515e5e483e93628d2792981c7d7e51fb855b08b))
* **ui:** card tray feedback: cards rise into the hand, the used card flies to the shuffler, the shuffler card fills, empty slots show a card back with progress ([7c33265](https://github.com/earl-gh/kalyeah/commit/7c33265861f2c7ce4059f4584023b61aef8e0f4b))
* **ui:** code boxes, a first-run walkthrough and How to play (round 8) ([77cf315](https://github.com/earl-gh/kalyeah/commit/77cf31540b3d7f55d8e5554e2115cfe245a90608))
* **ui:** colour-blind palette and team shapes on the health bars (U2) ([25be8e4](https://github.com/earl-gh/kalyeah/commit/25be8e43cd08f402c8edb09551d9b91af597a98a))
* **ui:** direction arrows, a results moment and a tagged hint (round 12) ([2c39028](https://github.com/earl-gh/kalyeah/commit/2c39028c02212e8758af9dc96c98b55a8d9a35ff))
* **ui:** feedback, a profile sheet and a UI gallery (round 7) ([5c14cf1](https://github.com/earl-gh/kalyeah/commit/5c14cf1f6b93a6a58415825524c2f10f5f213368))
* **ui:** Game Boy console with a circular 8-way D-pad at the bottom ([7e8168a](https://github.com/earl-gh/kalyeah/commit/7e8168a2184f1e65079f510f9523ba3f224aa43c))
* **ui:** group the weapons by street game or by type ([b44c30d](https://github.com/earl-gh/kalyeah/commit/b44c30dc4deeaee0214a262d9021780b865cf914))
* **ui:** hit glow, ready swell, blocked-button reason and control fit (round 13) ([ecc8ad1](https://github.com/earl-gh/kalyeah/commit/ecc8ad1d03f41f5ee061d86779d809bb894ea201))
* **ui:** in-match chrome on the skin, loadout bars and a suggested pair (round 11) ([0de692d](https://github.com/earl-gh/kalyeah/commit/0de692d533a10b141e975435f8d307312a1558ed))
* **ui:** Londrina Solid for display text and Source Sans 3 for regular text; old fonts removed ([2014d5e](https://github.com/earl-gh/kalyeah/commit/2014d5e8cdb6740e2a860059867a0813c2ed1d48))
* **ui:** Lost Tumbler replaces Dagsen everywhere; the Dagsen files are removed ([ee3b771](https://github.com/earl-gh/kalyeah/commit/ee3b771673692c06cdad1e2c0acea66511eeb6e6))
* **ui:** low-health breath, capture bar, rescue marker and minimap rings (round 14) ([3051cc3](https://github.com/earl-gh/kalyeah/commit/3051cc39daeda0710dae0ea375815f2ebcba2095))
* **ui:** no auto-run: the D-pad has no outer ring and letting go always stops ([5ec4d52](https://github.com/earl-gh/kalyeah/commit/5ec4d52fb73c0e85324481c3b5c22bccb90ab0f7))
* **ui:** no face selection at the main screen; docs: options for picking and a card cycle ([674509d](https://github.com/earl-gh/kalyeah/commit/674509dbeb2237b3b39a3269b65d902862abca2e))
* **ui:** no team showcase before the match; the kid screen is only the kid row, the portrait and Play ([5924744](https://github.com/earl-gh/kalyeah/commit/59247445397b3ad0f0ef8e8e4c2b67a2bb3d168f))
* **ui:** one corner and type scale on every screen, a shared press feel and one breath on Home ([0c9819f](https://github.com/earl-gh/kalyeah/commit/0c9819f27b9cd52cb46230a7714a7f7f2d01b95f))
* **ui:** one corner scale, one focus ring and the display letter tokens ([0e5b181](https://github.com/earl-gh/kalyeah/commit/0e5b18185fa605bf2eef5587199ea6dc0d80b9bf))
* **ui:** one weapon naming scheme, a developer switch, screen-reader names and a string table (round 18) ([70170c2](https://github.com/earl-gh/kalyeah/commit/70170c2eafae14ed4d3f03c0d9f5f14d3eb84a66))
* **ui:** polish loadouts and match feedback ([2ed90d6](https://github.com/earl-gh/kalyeah/commit/2ed90d6a1662770168eabb683cdfddef78cb3e42))
* **ui:** Poppins Bold replaces Inter for regular text ([8ba35dd](https://github.com/earl-gh/kalyeah/commit/8ba35ddab7f56c794837f9a46bcce3497c5bfdc0))
* **ui:** practice flow polish: the deck on the kid screen, controls on the deck during the intro ([081df90](https://github.com/earl-gh/kalyeah/commit/081df90b510503e37597a738c4b1a9096f5a8167))
* **ui:** quiet HUD, a thin aim guide, a chevron for the base and no needless containers ([5debbdc](https://github.com/earl-gh/kalyeah/commit/5debbdc6d444314eed22bcd8da0d2f7452830cb2))
* **ui:** rebuild the interface with a code-drawn game skin (phase 1) ([2a93832](https://github.com/earl-gh/kalyeah/commit/2a938324e155ae2982858ad65dfe040f9241bf64))
* **ui:** rebuild the loadout and tighten Home (UI rebuild round 2) ([7555e5a](https://github.com/earl-gh/kalyeah/commit/7555e5a5803b56ee11a535b284ce65aac9ce2db8))
* **ui:** RESCUE marker over your team's tagged box; the feed no longer covers it ([8477707](https://github.com/earl-gh/kalyeah/commit/847770748bea2e3d621a01e578c31cff50a4202e))
* **ui:** streamline hotspot rooms and character selection ([d717c1b](https://github.com/earl-gh/kalyeah/commit/d717c1be2e60f381d46be5d1b97310bb83ab9c1e))
* **ui:** switch the typeface to the owner's Clash, smaller nav label and arrows ([2ac5f43](https://github.com/earl-gh/kalyeah/commit/2ac5f433e26caccbd1d92655fa1d882384ef9c4d))
* **ui:** the Book is display only; characters get a gallery and a sheet ([51dfc16](https://github.com/earl-gh/kalyeah/commit/51dfc1667181cffcf572759deea6f5f658aa0005))
* **ui:** the Book with depth (round 10) ([c232a6d](https://github.com/earl-gh/kalyeah/commit/c232a6d03ead99f9deb73c238961f50c7556cead))
* **ui:** the console and controls ride above the home bar (U4) ([be6b84a](https://github.com/earl-gh/kalyeah/commit/be6b84acd44cbd1182e5d3aeb0440858e57641a3))
* **ui:** the home bottom bar runs edge to edge, flush with the bottom of the screen ([f7cd78a](https://github.com/earl-gh/kalyeah/commit/f7cd78a952530bc7142207cdca2a2a27db2a8bc8))
* **ui:** the loadout as a grid of weapon tiles with one info panel (round 16) ([c1b1686](https://github.com/earl-gh/kalyeah/commit/c1b1686b9cebf287a960d580b0d3bb8c6bdcf3fc))
* **ui:** the match chrome in Clash Royale style: blue deck, floating header, navy and gold D-pad ([e8772c4](https://github.com/earl-gh/kalyeah/commit/e8772c4358f0e67c4d23915316ee16b50f52a2ed))
* **ui:** the tagged message sits on the controller deck at the console's size; no player cards ([0d7bada](https://github.com/earl-gh/kalyeah/commit/0d7bada4d0aba6f3a49e9449d6dcb082fe6e006a))
* **ui:** typography roles for an all-Bungee interface ([42ac007](https://github.com/earl-gh/kalyeah/commit/42ac0079e364d0c982f85dc361e50cedc3c11c6f))
* **ui:** unify rooms and apply HIG-informed hierarchy ([715c85d](https://github.com/earl-gh/kalyeah/commit/715c85d98ac97c4d478046923f061e8ea5ad359a))
* **ui:** weapons page as a filtered grid with a detail sheet ([4ac4e21](https://github.com/earl-gh/kalyeah/commit/4ac4e21072d7726bea16112ba39a1b63000e97c4))
* **ui:** wire the shared press feel once from style_button ([057c08e](https://github.com/earl-gh/kalyeah/commit/057c08e2b940e2dec5da8a5813b48d802a6df820))
* **ui:** words and floors, a UI lint test, a focus ring only after a key (round 15) ([d0d85f4](https://github.com/earl-gh/kalyeah/commit/d0d85f4666da8232f05e909851df891ceeac7bff))
* **view:** cast feedback, readable aim previews, big-snip warning and hit bursts ([240e9d9](https://github.com/earl-gh/kalyeah/commit/240e9d9a7fb303200522df5e6cc89505923c135f))
* **weapons:** balance pass (tsinelas light, bato light, gunting light), hard-CC immunity, no-damage warning ([c0319f2](https://github.com/earl-gh/kalyeah/commit/c0319f292cbfce98c47a3e931088544123b16872))
* **weapons:** hand and shuffler: used card plays its cooldown in the shuffler, hand cards stay usable; Clash Royale card tray at the bottom ([b5ca1c1](https://github.com/earl-gh/kalyeah/commit/b5ca1c145bccde57e05bd24a563e853f52357e41))
* **weapons:** one-word card names (Snip, Cut, Toss, Crash, Flick, Slap, Heal, Block, Shield, Trap) ([198afc2](https://github.com/earl-gh/kalyeah/commit/198afc2715f1f7ed0b508d164edaf215f6ecacd4))
* **weapons:** try bato light 12 damage (variant C, wip) ([1fbca0b](https://github.com/earl-gh/kalyeah/commit/1fbca0b1ed2735a20d56957cb9611b8ba5090151))
* **weapons:** try tsinelas light 5 damage and gunting light 6 (variant A, wip) ([30fc87f](https://github.com/earl-gh/kalyeah/commit/30fc87fc24103e9d9b1960a8eb7bc37de30056ab))
* **weapons:** try tsinelas light cooldown 8 s (variant B, wip) ([21e8585](https://github.com/earl-gh/kalyeah/commit/21e85859fc332dd1bc8a2db0c3e55b25b59030a5))
* **weapons:** weapon cards: a shuffled deck of every weapon and a hand of two, no loadout and no swap ([6d9cc0c](https://github.com/earl-gh/kalyeah/commit/6d9cc0c5672d3a57d5d8815ec600a4a23c02c86c))


### Bug Fixes

* **art:** hold every model to one scale: audit, nine props corrected, exact cover height, a scale test ([50693f5](https://github.com/earl-gh/kalyeah/commit/50693f505a0f25474ea478e53238b21992c3577a))
* **art:** no blur in gameplay: lens re-sampling and bloom off, 8x anisotropic filtering ([1ddfef3](https://github.com/earl-gh/kalyeah/commit/1ddfef3cc1434498efc6157cb47d4f1e39a98b05))
* **art:** readable match screen: team rings, bases, bars, impacts, banners and tray ([9c46872](https://github.com/earl-gh/kalyeah/commit/9c46872dcafddcfdb9a60c52cd5f5f53807d12de))
* **art:** team-coloured effects from frame one and clearer colour-blind colours ([b2d1a64](https://github.com/earl-gh/kalyeah/commit/b2d1a64ef7b2d526626caaa28680ca5d513459a4))
* **boot:** the tip is sentence case again in the body font ([dbfb9a2](https://github.com/earl-gh/kalyeah/commit/dbfb9a265014363004ca53501326a75e573f41bb))
* **camera:** concave lens, a little weaker (lens_curve -0.3) ([cce6c1d](https://github.com/earl-gh/kalyeah/commit/cce6c1d823d7f4b8d9b1bbd901a02fb11aff6d06))
* **camera:** make the far blur start right at the ends and grow gradually ([fb29667](https://github.com/earl-gh/kalyeah/commit/fb296678892981b8f9cd028e9057097e93c6fb77))
* **hud:** clear the card tray audit findings ([596d0f6](https://github.com/earl-gh/kalyeah/commit/596d0f66ad5f3872ab5e047d5653cf0a6a98d8f0))
* **input:** cancel player intent on application interruptions ([fc33cf2](https://github.com/earl-gh/kalyeah/commit/fc33cf20859e4396672a81c9e5ecc50f45c2a6a5))
* **kids:** close the looping clips so Junjun's run stops snapping every cycle ([8944d4f](https://github.com/earl-gh/kalyeah/commit/8944d4fb8aeea1546737797f802d8189f7e5441f))
* **map:** the street keeps its normal brightness with no shadows ([1f6524a](https://github.com/earl-gh/kalyeah/commit/1f6524a1a1e105d638bca0fea60178d799b65a04))
* **menu:** the centre card stays centred on the screen; book and history sit above the cards ([86d4106](https://github.com/earl-gh/kalyeah/commit/86d4106b997d3af9c9c9744b904a6b675cff1915))
* **menu:** the map model is a centred 1:1 square with the plate on its front edge ([892155d](https://github.com/earl-gh/kalyeah/commit/892155d5625dae6360bf50794bf4dda76d612e5a))
* **menu:** the map model is a real 24 x 24 m slab, exactly 1:1 ([b56a977](https://github.com/earl-gh/kalyeah/commit/b56a9777f63f59d4c235938cce0c4586acc23e7c))
* **net:** refuse other protocol versions and harden the card hand on the wire ([dbfda17](https://github.com/earl-gh/kalyeah/commit/dbfda17e26b351e27c259ad3b152529bd013f40f))
* **net:** the tapped street point and the dragged card's point cross the wire ([3b4c0e9](https://github.com/earl-gh/kalyeah/commit/3b4c0e90416f01d7ab35a485a6f5eb95f7ded4f1))
* **props:** keep the owner's UVs and texture for the buildings, fences and post (sharp, like the editor) ([46fde48](https://github.com/earl-gh/kalyeah/commit/46fde48727574254c08389ea9b343f0b1e91166f))
* **props:** rebuild every street prop with the owner's UVs and texture; check all for cracks ([e2d71a4](https://github.com/earl-gh/kalyeah/commit/e2d71a4ddad426668ecd962e595600a8de8b2e5e))
* **ui:** bring back the containers that carry small text, drop the redundant ones ([7e059ab](https://github.com/earl-gh/kalyeah/commit/7e059aba8a6577fb3734fabc533cdf69a1b7d322))
* **ui:** improve short-phone loadout hierarchy and lobby identity ([2760eac](https://github.com/earl-gh/kalyeah/commit/2760eacbd2bf70a0fa1568f108c26a6185beae4e))
* **ui:** keep the pressed button's corners in its nine-slice corners ([94486c2](https://github.com/earl-gh/kalyeah/commit/94486c29fdf479f12e864c4104107bb6ab6f6ed7))
* **ui:** modals and tints draw above every component, including the mode cards ([abbca01](https://github.com/earl-gh/kalyeah/commit/abbca01f8db773fa292d7ba7ae541c3b818ec7dc))
* **ui:** name the loadout cards' and socket's font sizes from the type scale ([9cd3383](https://github.com/earl-gh/kalyeah/commit/9cd33833b380321967c6ebfde941feebfec238fd))
* **ui:** rebuild interface hierarchy ([2275907](https://github.com/earl-gh/kalyeah/commit/2275907d245f51bedeaa520d7e84f07765aa3d83))
* **ui:** regular-font text is sentence case ([a40dee3](https://github.com/earl-gh/kalyeah/commit/a40dee391f28b2329b7778c81d9a2726475c7680))
* **ui:** simplify settings and unify control density ([fe6234c](https://github.com/earl-gh/kalyeah/commit/fe6234c354ef3c19a1eba4bfc2d0bff2fca4eaec))
* **ui:** take every button, label and drawn font size from the type scale ([c6351ca](https://github.com/earl-gh/kalyeah/commit/c6351ca80ef40a3e9bc6d66872b1e63ff59847a5))
* **weapons:** the tossed can (lata) shows its model; audit every weapon's cast ([b68f070](https://github.com/earl-gh/kalyeah/commit/b68f070f3013a5aa42f93ba900828e905c8c212f))


### Performance Improvements

* **sim:** bots scan their hand only when they could cast, cheapest test first ([5629efe](https://github.com/earl-gh/kalyeah/commit/5629efe37a3075dbed1e803b9e4310ad571488b8))

## [0.7.0](https://github.com/earl-gh/kalyeah/compare/kalyeah-v0.6.0...kalyeah-v0.7.0) (2026-10-18)


### Features

* **kids:** Junjun plays his model's own clips at their own speed; practice menu plays hit animations ([935fc0f](https://github.com/earl-gh/kalyeah/commit/935fc0f279f3fcff3342dea134290477fa71fd27))
* **ui:** establish branded interface foundation ([b7fed90](https://github.com/earl-gh/kalyeah/commit/b7fed901402f0252574944694cf8bb3db0a142a3))
* **ui:** polish lobby and match presentation ([a94e26f](https://github.com/earl-gh/kalyeah/commit/a94e26fefd9dceb6e1a41acce685165f5cb8ae74))
* **ui:** refine home flow and typography ([5b29800](https://github.com/earl-gh/kalyeah/commit/5b29800007931112725307cda1561dee9d5b4274))


### Bug Fixes

* **lobby:** use eight-character room codes ([be91322](https://github.com/earl-gh/kalyeah/commit/be9132213015c048920089caddd67b0cb3704120))
* **multiplayer:** preserve authority across interruptions ([e934bd9](https://github.com/earl-gh/kalyeah/commit/e934bd941bce6e624aaf0385f67c5af1a482a8ae))

## [0.6.0](https://github.com/earl-gh/kalyeah/compare/kalyeah-v0.5.1...kalyeah-v0.6.0) (2026-10-16)


### ⚠ BREAKING CHANGES

* **map:** playing area is square, 24 x 24 m, minimap 140 x 140
* **map:** playing area is 3:4 (24 x 32 m), minimap 120 x 160

### Features

* **art:** real 3D models for the slipper, can, marble and paper skills; new menu background ([78f69b1](https://github.com/earl-gh/kalyeah/commit/78f69b101018ae17660cad28c3fa7ec7fb2ca0ca))
* **art:** the nanay on the sidewalk and five more street things in the end barrier rows ([0099205](https://github.com/earl-gh/kalyeah/commit/00992054ee898014d82a62249bbf5c1930b125c4))
* **hud:** solid health bars, no partitions ([f014b73](https://github.com/earl-gh/kalyeah/commit/f014b737598ce05e4fc8c5f75edc7ace19d8de74))
* **input:** the stick only picks the direction, any drag past the dead zone is a full-speed run ([f0575dc](https://github.com/earl-gh/kalyeah/commit/f0575dc433a43524fb7584983ca42a9f41017f81))
* **kids:** Junjun is the owner's rebuilt-rig boy ([a710424](https://github.com/earl-gh/kalyeah/commit/a7104248d1699915e28d159d78f2e919fb81f8a3))
* **map:** code-drawn realistic cement on the street and raised sidewalks ([0c5322b](https://github.com/earl-gh/kalyeah/commit/0c5322ba921a82ef78cfb9c55b95766a05ba0068))
* **map:** lay the owner's ground art on the sidewalk tops and raised faces ([659a41d](https://github.com/earl-gh/kalyeah/commit/659a41d1ebb5515c58e1626a40602262309747ef))
* **map:** no white border along the left and right of the play area ([48581bb](https://github.com/earl-gh/kalyeah/commit/48581bbbe7926ced19648276a31214a32c081af6))
* **map:** playing area is 3:4 (24 x 32 m), minimap 120 x 160 ([446c1d4](https://github.com/earl-gh/kalyeah/commit/446c1d47ba8126b24f98d315d8cd970d85496316))
* **map:** playing area is square, 24 x 24 m, minimap 140 x 140 ([492481f](https://github.com/earl-gh/kalyeah/commit/492481f168a41b672981f39c4a7d76a674e07b6e))
* **map:** raised gray cement sidewalks (0.4 m, was 0.18 m brick pavers) ([b1c5a43](https://github.com/earl-gh/kalyeah/commit/b1c5a43eb9f0d9fcd0b770c43f991276b2429a4f))
* **practice:** a free enemy dummy stands in the enemy's tagged box ([5ef3be1](https://github.com/earl-gh/kalyeah/commit/5ef3be1d7156baf6a3bdcce3aba4b83cf87441cd))
* **select:** a team never has the same kid twice, the other team may have it once ([6fc23cf](https://github.com/earl-gh/kalyeah/commit/6fc23cf997ae215ba9813d464cbd65b2102ebb65))
* **select:** pick your kid and weapons on one fighting-game style screen ([16d2245](https://github.com/earl-gh/kalyeah/commit/16d2245030f0f983bc3f41bd3b7e39cfb644a811))
* **ui:** main-screen model in the game's exact ratio with corner props only ([d42335b](https://github.com/earl-gh/kalyeah/commit/d42335b79dc42200ae6afc91a58bd21d42eec058))
* **ui:** rebuilt main screen on the owner's blue diamond background ([ec1ca1b](https://github.com/earl-gh/kalyeah/commit/ec1ca1bcce1913666af841030fecfc93c666e1fa))
* **ui:** scale the main-screen map model up by looking down at it more steeply ([fe38c92](https://github.com/earl-gh/kalyeah/commit/fe38c92f06a6203bbe6d80d20b14469295b57aa1))
* **ui:** simpler main screen with a still play-area model and 2x2 buttons ([1c9c33e](https://github.com/earl-gh/kalyeah/commit/1c9c33e30cde1114178520470ab80e2f106996b6))


### Bug Fixes

* **map:** remove the thin white line along the street edges ([02756a6](https://github.com/earl-gh/kalyeah/commit/02756a6eab61c8b4c3245f683f704f5804f9f13f))

## [0.5.1](https://github.com/earl-gh/kalyeah/compare/kalyeah-v0.5.0...kalyeah-v0.5.1) (2026-10-15)


### Bug Fixes

* **art:** kid model build welds before decimating; audit test reports the torn kids ([3c054f6](https://github.com/earl-gh/kalyeah/commit/3c054f64f7834f1e296bf64408af4e9d883e2b54))
* **art:** rebuild the street props whole: weld before decimating, extended bake margin ([8439124](https://github.com/earl-gh/kalyeah/commit/8439124f1bf88d1ed8f3c83460694916a4080a9b))
* **camera:** near plane 0.4 m (was 4 m) so models near the camera are not sliced ([4413f9b](https://github.com/earl-gh/kalyeah/commit/4413f9b884e5cf534b360c6515da1196e31af4eb))

## [0.5.0](https://github.com/earl-gh/kalyeah/compare/kalyeah-v0.4.0...kalyeah-v0.5.0) (2026-10-14)


### Features

* **hud:** crop the minimap to 12 px rounded corners and make it smooth ([f66334a](https://github.com/earl-gh/kalyeah/commit/f66334a977f56561bf22c645e025b344b9484c07))
* **hud:** lighter, see-through minimap background (55% opaque, was 95%) ([5b71f12](https://github.com/earl-gh/kalyeah/commit/5b71f12f81d74abdc4d9b222cf1b268a77686f1d))
* **hud:** no frame on the minimap; faces cropped from the boot screen art ([cc54b07](https://github.com/earl-gh/kalyeah/commit/cc54b076b25a1e79b99e648b221a83587aef7f7b))
* **hud:** smooth minimap (3x supersampled) and one thin team-colour ring ([049420f](https://github.com/earl-gh/kalyeah/commit/049420f4aac608d661608f224b9101b3b15656a5))
* **hud:** the minimap has no dark panel round it, only small rounded corners ([943287d](https://github.com/earl-gh/kalyeah/commit/943287dabeeb01f723e7b1ec678f6320ca3ff42c))
* **hud:** the minimap shows each player's character face in a circle ([a867760](https://github.com/earl-gh/kalyeah/commit/a8677606395640d390f1274b73f3fbfe8b636951))
* **hud:** thinner minimap ring (0.6 px, was 1.2 px) ([174d174](https://github.com/earl-gh/kalyeah/commit/174d17470dac5b26f8b97104ac3a29f71ddb6d30))

## [0.4.0](https://github.com/earl-gh/kalyeah/compare/kalyeah-v0.3.0...kalyeah-v0.4.0) (2026-10-14)


### Features

* **camera,hud,map:** free-following camera, head-height bars, boxes flush in the corners ([2ee9310](https://github.com/earl-gh/kalyeah/commit/2ee93109bca95e1753e310fcd7dc1743ac5d916d))
* **camera:** 20 degree pitch and a bit lower (10 m visible width) ([880780a](https://github.com/earl-gh/kalyeah/commit/880780a7dbb8a367dcc43e93e1af1eb1ccf99657))
* **hud,map:** free kids hide their bar in their own box; no outline round the posts ([4a100b9](https://github.com/earl-gh/kalyeah/commit/4a100b9d1a786a7430aeb4cf899e78cbaf56478b))
* **map:** a rescue area round each tagged box, only two cardboard walls on the street ([bfe7304](https://github.com/earl-gh/kalyeah/commit/bfe73047143104446046be8148022999fa63fe63))
* **map:** square tagged boxes closed by cardboard walls, no tint or tape ([c2aa6f9](https://github.com/earl-gh/kalyeah/commit/c2aa6f99212ce61082ae981cb073619d786c296f))
* **map:** tagged boxes keep only their inner corner rounded and have no tape on the street edges ([821b2e2](https://github.com/earl-gh/kalyeah/commit/821b2e278e6febb114bc51ba86ec751b977b99dd))
* **map:** the only cover walls are one by each base, against the side curbs ([6d0412c](https://github.com/earl-gh/kalyeah/commit/6d0412cf64cd7307eade4ac9f487fb4b17530d68))
* **map:** the side cover walls stand 6 m from the base circles (was 2 m) ([9e3240d](https://github.com/earl-gh/kalyeah/commit/9e3240ddbba1baca88ab47d4b5d214946de0aee9))


### Bug Fixes

* **web:** keep the browser-tab icon after the game boots ([517e336](https://github.com/earl-gh/kalyeah/commit/517e3363434aad5fe52216eb3eba4a1d55bc82c9))

## [0.3.0](https://github.com/earl-gh/kalyeah/compare/kalyeah-v0.2.0...kalyeah-v0.3.0) (2026-10-14)


### ⚠ BREAKING CHANGES

* **map:** map_layout.tres sizes changed (server and clients must match).

### Features

* **art:** street props are the owner's Meshy models ([158a5ba](https://github.com/earl-gh/kalyeah/commit/158a5bafb1b92a9880926172ef1e4a2ef758fa6f))
* **controls,map:** knob-sized skills, outlined stick, cardboard-only cover, mixed end barricades ([a42a961](https://github.com/earl-gh/kalyeah/commit/a42a9611b0d60007ab26ae5e671bc95ef3007770))
* **controls:** auto-run ring around the stick ([e18f081](https://github.com/earl-gh/kalyeah/commit/e18f081fd8d4d7f3ffc4e12943a842ee9c780915))
* **controls:** movement stick back at the bottom centre, skills on its left and right ([83aee78](https://github.com/earl-gh/kalyeah/commit/83aee784f806bdc6c7108926283f8b78edbb0e0f))
* **controls:** outer ring as thick and white as the inner one, the lit ring is the bright one ([f381b5e](https://github.com/earl-gh/kalyeah/commit/f381b5e98eb29c27ba48da4b83b89ec92554432e))
* **controls:** the outer ring appears only once the knob is held ([819ec20](https://github.com/earl-gh/kalyeah/commit/819ec207853099239b450421a50700be3af92d69))
* **controls:** tint the band between the rings while the outer ring is lit ([8f14900](https://github.com/earl-gh/kalyeah/commit/8f1490039fa7f41e8adcc20c58118ed59a7b0b69))
* **kids:** a portrait for every kid ([fbdfdb1](https://github.com/earl-gh/kalyeah/commit/fbdfdb1e1fe98cc878eee5091f8eea4c19a81aea))
* **kids:** play each kid's own clips in game ([7e79134](https://github.com/earl-gh/kalyeah/commit/7e79134214f5d8384e657fc429b89f2b804f9646))
* **kids:** rigged, animated models for all six kids ([e7fa74d](https://github.com/earl-gh/kalyeah/commit/e7fa74d52890ce69f7f8d3add56a7d443ac57312))
* **map,camera:** no bunting, a closer camera, bigger base posts ([cf2205e](https://github.com/earl-gh/kalyeah/commit/cf2205e677ffbc4f491ef76ade8f3d14d5907b2b))
* **map:** a team-colour outline round each base post ([a252ca6](https://github.com/earl-gh/kalyeah/commit/a252ca64f2a0e770f781efd3d9788d64d3a81172))
* **map:** barrier rows set down naturally, never stretched ([ed460f5](https://github.com/earl-gh/kalyeah/commit/ed460f5ad4c440cb716df02d447bedba68935e99))
* **map:** give the base the tagged box look and round the box corners ([bea09ff](https://github.com/earl-gh/kalyeah/commit/bea09ffbbc0fb387c73a36f601d2a6e55c7c8b67))
* **map:** no Pillow-painted posters ([e20ad95](https://github.com/earl-gh/kalyeah/commit/e20ad959fac47ff3555a5290d69ca508b055c000))
* **map:** no power poles along the left and right sides ([1801273](https://github.com/earl-gh/kalyeah/commit/180127395dc6f96da99a83b04ec894c3a10a1793))
* **map:** scale the tagged box 1.5x for padding (2.4 x 6 m) ([0aab100](https://github.com/earl-gh/kalyeah/commit/0aab100a860d0b4598efe399fc25df98890d40a4))
* **map:** the base posts' flyer side faces the middle of the play area ([881f002](https://github.com/earl-gh/kalyeah/commit/881f00214cbafa54f1a65f89cd333fb51add5fdd))
* **map:** the end barrier rows mix eight of the owner's street models ([f199644](https://github.com/earl-gh/kalyeah/commit/f199644852756b760397b9205d82889aadf431d5))
* **map:** the playing area is 1:2, 24 x 48 m (was 30 x 40, 3:4) ([8471e83](https://github.com/earl-gh/kalyeah/commit/8471e83ac5473ab4dc6d77c3ea70371010a2261e))
* **map:** the tagged box fits four kids (3 x 2.4 m, 2 x 2 slots) ([6348f4c](https://github.com/earl-gh/kalyeah/commit/6348f4cb20c3386acf51dd4cbc3b29aabfecca04))
* **map:** the tagged box is portrait, four kids in single file ([73c152a](https://github.com/earl-gh/kalyeah/commit/73c152a8ce534a94a2b16173994d8d6d44563fed))
* **map:** your own (blue) base post is turned 180 degrees ([8e6768a](https://github.com/earl-gh/kalyeah/commit/8e6768a957a0ad4d3f96b68bbd8d23a1ccc2f18a))


### Bug Fixes

* **map:** smooth post outline from a remeshed shell, no jagged bits ([d654376](https://github.com/earl-gh/kalyeah/commit/d654376b8cee16f681aa830ca582b358050e17d4))

## [0.2.0](https://github.com/earl-gh/kalyeah/compare/kalyeah-v0.1.0...kalyeah-v0.2.0) (2026-10-12)


### ⚠ BREAKING CHANGES

* **art:** Junjun is a real 3D model, no sprites
* PlayerInput has no dash or pin buttons; MatchScore has no sets; snapshots and the lobby protocol changed (SET_BEST_OF).
* the guava, tricycle, breakable walls and death delay are gone; snapshots carry tag and capture state instead.

### Features

* **art:** Junjun is a real 3D model, no sprites ([0b557e3](https://github.com/earl-gh/kalyeah/commit/0b557e3a312cfbc5084880846955ef8f33bbade9))
* **art:** Junjun sprites rendered from the rigged 3D boy model ([91b2021](https://github.com/earl-gh/kalyeah/commit/91b2021312741c7740f0f85eea8e20e5c9081d4e))
* **art:** one narrow crossing through the middle of the road; signal gantries with a horizontal light facing the drivers ([e9d4a01](https://github.com/earl-gh/kalyeah/commit/e9d4a01e18fd80912a06218447446fb485c9ff35))
* **art:** painted skill icons on a cardboard disc for every skill button ([eef859d](https://github.com/earl-gh/kalyeah/commit/eef859dd96891ec3bd802c40d654e4e0a9be3602))
* **art:** pedestrian crossing bars between the alleys and the road; stop lights instead of plants ([ec6203a](https://github.com/earl-gh/kalyeah/commit/ec6203a9a67212d34fcb50c3944561ff46f58265))
* **art:** softer environment colours to ease eye strain ([ad3d136](https://github.com/earl-gh/kalyeah/commit/ad3d1360c0fa2bca8722ec05da1e9bbd2e224e1c))
* **art:** the lane is a concrete alley lined with tall house walls; only the cross street is a road ([0361e2b](https://github.com/earl-gh/kalyeah/commit/0361e2b703880689abc16ffc76b74799a8d87459))
* **art:** warm brick-tan pavers so the pedestrian lanes stand out ([650f14a](https://github.com/earl-gh/kalyeah/commit/650f14af2c5fd456fb668af20aaffd9358af796e))
* **brand:** app icon for the browser tab, iPhone home screen and Android launcher ([67347cb](https://github.com/earl-gh/kalyeah/commit/67347cb9f505b3d7f0d1bef4ace16e550396f970))
* **camera:** portrait 9:16 framing at 50 degrees with the hero low on screen, and camera bounds from the map ([fad5306](https://github.com/earl-gh/kalyeah/commit/fad5306e457ec08f554a3f43dd76b36a7dc94c19))
* **camera:** straight behind the hero (yaw 0) ([6622aed](https://github.com/earl-gh/kalyeah/commit/6622aed607a37e03bd3aacc8e6a044b6bc51a53d))
* **camera:** ultra-wide lens camera turned 4 degrees, bigger kids, stronger vignette ([b8bfa1c](https://github.com/earl-gh/kalyeah/commit/b8bfa1cf44ae66ee0c5743e18ebd45c92918d0a4))
* cinematic morning light, fixed see-through joystick circle with a moving knob, longer pin blink ([bf45aac](https://github.com/earl-gh/kalyeah/commit/bf45aac2435aefb81f4490558eb6471faf118a2a))
* four-lane cross street and a longer middle, no floating dots, spinner in the enemy yard, longer pin dash with a small end circle, enemy walls toggle ([6d809d0](https://github.com/earl-gh/kalyeah/commit/6d809d020529d0d3fc3858fa2c8b6306c20a37ff))
* **guava:** tap to bite and heal a quarter, throw to deal a quarter or an eighth ([dfeeddb](https://github.com/earl-gh/kalyeah/commit/dfeeddb8d897fffcd3e28858ccdd2e55a87564fd))
* **hud:** fixed bottom-centre controls, no handedness setting; larger, optically centred skill icons ([2431c11](https://github.com/earl-gh/kalyeah/commit/2431c11430ec636b6a574abfb08e2d669afcb203))
* **map:** 3:4 playing area (30 x 40 m) with a minimap at exactly 3:4 ([8887a36](https://github.com/earl-gh/kalyeah/commit/8887a36ecd767d9969b7d547d00209870353069f))
* **map:** a team's two wall rows as far apart as the middle, post in the inner wall, scoring behind the inner wall ([35623b1](https://github.com/earl-gh/kalyeah/commit/35623b181dd68b566eaa6f754067a7c12e87d518))
* **map:** a wider four-lane road, pedestrian lanes between the alleys and the road, no yellow bars at the road sides ([42c9de7](https://github.com/earl-gh/kalyeah/commit/42c9de718960d2cfb57ea8e4bd2bf50e8798da32))
* **map:** flat dark grey cement play area and fewer obstacles ([05b20be](https://github.com/earl-gh/kalyeah/commit/05b20be774df51ac72b06d2de0e18d29108fda06))
* **map:** remove the white pad under the post; scoring needs 2 s behind the inner wall ([7942167](https://github.com/earl-gh/kalyeah/commit/7942167b22dadee43db135d47ce00ec4ff13c461))
* **map:** space the wall rows and electric post like MOBA towers and nexus, lane 105 m ([1cf3be2](https://github.com/earl-gh/kalyeah/commit/1cf3be25e95637a9154aff8154a56e98093f08cc))
* **match:** opening show, match clock, tagged spectator cards ([37e4b12](https://github.com/earl-gh/kalyeah/commit/37e4b12f065b1002dcdc151d938ac3ab3c4fdf57))
* one-handed tilt controls, no dash or pin, best of 3/5/7 scoring ([a05bffd](https://github.com/earl-gh/kalyeah/commit/a05bffdb94cc86fd122c6f16a420bc5b10e7e70d))
* **skills:** the pin dash is an aimed skill: tap dashes the way you face, drag chooses the direction ([62739cc](https://github.com/earl-gh/kalyeah/commit/62739cc82a875afbe25e57d8ccceeb495dbd34d1))
* softer walls, no rings under posts, stronger yard colour, grey knob, no aim connector lines, thick guava lane, tumble fall pose, heavy attacks root the caster ([8478aab](https://github.com/earl-gh/kalyeah/commit/8478aabe9f627ee3d09ed28c2a4775a383335e1a))
* tag mode on a barangay street, like the reference video ([86bb01c](https://github.com/earl-gh/kalyeah/commit/86bb01c26a815b495db9d5c74a9054a89bb2dc64))
* **ui:** boot screen with key art and a cardboard loading bar ([68349d2](https://github.com/earl-gh/kalyeah/commit/68349d2210fb06567bd57f3ae00903ee8f61d236))
* **weapons:** Langit Lupa pair: heal (tap self, drag a teammate) and the soil block, in the weapon pick ([f0d2d2c](https://github.com/earl-gh/kalyeah/commit/f0d2d2c99bdaaef8e53b9aeec32b1a810f05f85d))


### Bug Fixes

* **art:** drop the coloured ring and show whole skill icons on the cardboard buttons ([a483afd](https://github.com/earl-gh/kalyeah/commit/a483afd3c65be24ecb242383819617e11699993e))
* **hud:** hide the guava button until you hold the guava ([8409a43](https://github.com/earl-gh/kalyeah/commit/8409a43638023414cbb5050f25878da5ea585a45))
* **hud:** the ready pin button shows the dash arrow, the return arrow only while out on the pin ([08faf79](https://github.com/earl-gh/kalyeah/commit/08faf799308712154cc4a86387a4fd3ab9ed48dc))
* **kids:** swap run feet only on the full front and back views ([83059d1](https://github.com/earl-gh/kalyeah/commit/83059d11deaf66357553b0e819a9d09e84ec9fa8))
* **ui:** cut the cardboard out of the loading bar and centre it in the empty track ([3fadbd1](https://github.com/earl-gh/kalyeah/commit/3fadbd1a61f35d0e5a6b1663f830cfb697a4b75f))
* unblock the Pages deploy and the release workflow ([a853b49](https://github.com/earl-gh/kalyeah/commit/a853b49201ef5e299ecc857fd296d91d97f8ec00))

## [0.1.0](https://github.com/earl-gh/kalyeah/compare/kalyeah-v0.1.0...kalyeah-v0.1.0) (2026-10-11)


### ⚠ BREAKING CHANGES

* PlayerInput has no dash or pin buttons; MatchScore has no sets; snapshots and the lobby protocol changed (SET_BEST_OF).
* the guava, tricycle, breakable walls and death delay are gone; snapshots carry tag and capture state instead.

### Features

* **art:** one narrow crossing through the middle of the road; signal gantries with a horizontal light facing the drivers ([e9d4a01](https://github.com/earl-gh/kalyeah/commit/e9d4a01e18fd80912a06218447446fb485c9ff35))
* **art:** painted skill icons on a cardboard disc for every skill button ([eef859d](https://github.com/earl-gh/kalyeah/commit/eef859dd96891ec3bd802c40d654e4e0a9be3602))
* **art:** pedestrian crossing bars between the alleys and the road; stop lights instead of plants ([ec6203a](https://github.com/earl-gh/kalyeah/commit/ec6203a9a67212d34fcb50c3944561ff46f58265))
* **art:** softer environment colours to ease eye strain ([ad3d136](https://github.com/earl-gh/kalyeah/commit/ad3d1360c0fa2bca8722ec05da1e9bbd2e224e1c))
* **art:** the lane is a concrete alley lined with tall house walls; only the cross street is a road ([0361e2b](https://github.com/earl-gh/kalyeah/commit/0361e2b703880689abc16ffc76b74799a8d87459))
* **art:** warm brick-tan pavers so the pedestrian lanes stand out ([650f14a](https://github.com/earl-gh/kalyeah/commit/650f14af2c5fd456fb668af20aaffd9358af796e))
* **camera:** portrait 9:16 framing at 50 degrees with the hero low on screen, and camera bounds from the map ([fad5306](https://github.com/earl-gh/kalyeah/commit/fad5306e457ec08f554a3f43dd76b36a7dc94c19))
* **camera:** straight behind the hero (yaw 0) ([6622aed](https://github.com/earl-gh/kalyeah/commit/6622aed607a37e03bd3aacc8e6a044b6bc51a53d))
* **camera:** ultra-wide lens camera turned 4 degrees, bigger kids, stronger vignette ([b8bfa1c](https://github.com/earl-gh/kalyeah/commit/b8bfa1cf44ae66ee0c5743e18ebd45c92918d0a4))
* cinematic morning light, fixed see-through joystick circle with a moving knob, longer pin blink ([bf45aac](https://github.com/earl-gh/kalyeah/commit/bf45aac2435aefb81f4490558eb6471faf118a2a))
* four-lane cross street and a longer middle, no floating dots, spinner in the enemy yard, longer pin dash with a small end circle, enemy walls toggle ([6d809d0](https://github.com/earl-gh/kalyeah/commit/6d809d020529d0d3fc3858fa2c8b6306c20a37ff))
* **guava:** tap to bite and heal a quarter, throw to deal a quarter or an eighth ([dfeeddb](https://github.com/earl-gh/kalyeah/commit/dfeeddb8d897fffcd3e28858ccdd2e55a87564fd))
* **hud:** fixed bottom-centre controls, no handedness setting; larger, optically centred skill icons ([2431c11](https://github.com/earl-gh/kalyeah/commit/2431c11430ec636b6a574abfb08e2d669afcb203))
* **map:** 3:4 playing area (30 x 40 m) with a minimap at exactly 3:4 ([8887a36](https://github.com/earl-gh/kalyeah/commit/8887a36ecd767d9969b7d547d00209870353069f))
* **map:** a team's two wall rows as far apart as the middle, post in the inner wall, scoring behind the inner wall ([35623b1](https://github.com/earl-gh/kalyeah/commit/35623b181dd68b566eaa6f754067a7c12e87d518))
* **map:** a wider four-lane road, pedestrian lanes between the alleys and the road, no yellow bars at the road sides ([42c9de7](https://github.com/earl-gh/kalyeah/commit/42c9de718960d2cfb57ea8e4bd2bf50e8798da32))
* **map:** flat dark grey cement play area and fewer obstacles ([05b20be](https://github.com/earl-gh/kalyeah/commit/05b20be774df51ac72b06d2de0e18d29108fda06))
* **map:** remove the white pad under the post; scoring needs 2 s behind the inner wall ([7942167](https://github.com/earl-gh/kalyeah/commit/7942167b22dadee43db135d47ce00ec4ff13c461))
* **map:** space the wall rows and electric post like MOBA towers and nexus, lane 105 m ([1cf3be2](https://github.com/earl-gh/kalyeah/commit/1cf3be25e95637a9154aff8154a56e98093f08cc))
* **match:** opening show, match clock, tagged spectator cards ([37e4b12](https://github.com/earl-gh/kalyeah/commit/37e4b12f065b1002dcdc151d938ac3ab3c4fdf57))
* one-handed tilt controls, no dash or pin, best of 3/5/7 scoring ([a05bffd](https://github.com/earl-gh/kalyeah/commit/a05bffdb94cc86fd122c6f16a420bc5b10e7e70d))
* **skills:** the pin dash is an aimed skill: tap dashes the way you face, drag chooses the direction ([62739cc](https://github.com/earl-gh/kalyeah/commit/62739cc82a875afbe25e57d8ccceeb495dbd34d1))
* softer walls, no rings under posts, stronger yard colour, grey knob, no aim connector lines, thick guava lane, tumble fall pose, heavy attacks root the caster ([8478aab](https://github.com/earl-gh/kalyeah/commit/8478aabe9f627ee3d09ed28c2a4775a383335e1a))
* tag mode on a barangay street, like the reference video ([86bb01c](https://github.com/earl-gh/kalyeah/commit/86bb01c26a815b495db9d5c74a9054a89bb2dc64))
* **ui:** boot screen with key art and a cardboard loading bar ([68349d2](https://github.com/earl-gh/kalyeah/commit/68349d2210fb06567bd57f3ae00903ee8f61d236))
* **weapons:** Langit Lupa pair: heal (tap self, drag a teammate) and the soil block, in the weapon pick ([f0d2d2c](https://github.com/earl-gh/kalyeah/commit/f0d2d2c99bdaaef8e53b9aeec32b1a810f05f85d))


### Bug Fixes

* **art:** drop the coloured ring and show whole skill icons on the cardboard buttons ([a483afd](https://github.com/earl-gh/kalyeah/commit/a483afd3c65be24ecb242383819617e11699993e))
* **hud:** hide the guava button until you hold the guava ([8409a43](https://github.com/earl-gh/kalyeah/commit/8409a43638023414cbb5050f25878da5ea585a45))
* **hud:** the ready pin button shows the dash arrow, the return arrow only while out on the pin ([08faf79](https://github.com/earl-gh/kalyeah/commit/08faf799308712154cc4a86387a4fd3ab9ed48dc))
* **kids:** swap run feet only on the full front and back views ([83059d1](https://github.com/earl-gh/kalyeah/commit/83059d11deaf66357553b0e819a9d09e84ec9fa8))
* **ui:** cut the cardboard out of the loading bar and centre it in the empty track ([3fadbd1](https://github.com/earl-gh/kalyeah/commit/3fadbd1a61f35d0e5a6b1663f830cfb697a4b75f))

## [0.1.0](https://github.com/earl-gh/kalyeah/compare/kalyeah-v0.1.0...kalyeah-v0.1.0) (2026-10-11)


### Features

* **art:** one narrow crossing through the middle of the road; signal gantries with a horizontal light facing the drivers ([e9d4a01](https://github.com/earl-gh/kalyeah/commit/e9d4a01e18fd80912a06218447446fb485c9ff35))
* **art:** painted skill icons on a cardboard disc for every skill button ([eef859d](https://github.com/earl-gh/kalyeah/commit/eef859dd96891ec3bd802c40d654e4e0a9be3602))
* **art:** pedestrian crossing bars between the alleys and the road; stop lights instead of plants ([ec6203a](https://github.com/earl-gh/kalyeah/commit/ec6203a9a67212d34fcb50c3944561ff46f58265))
* **art:** softer environment colours to ease eye strain ([ad3d136](https://github.com/earl-gh/kalyeah/commit/ad3d1360c0fa2bca8722ec05da1e9bbd2e224e1c))
* **art:** the lane is a concrete alley lined with tall house walls; only the cross street is a road ([0361e2b](https://github.com/earl-gh/kalyeah/commit/0361e2b703880689abc16ffc76b74799a8d87459))
* **art:** warm brick-tan pavers so the pedestrian lanes stand out ([650f14a](https://github.com/earl-gh/kalyeah/commit/650f14af2c5fd456fb668af20aaffd9358af796e))
* **camera:** straight behind the hero (yaw 0) ([6622aed](https://github.com/earl-gh/kalyeah/commit/6622aed607a37e03bd3aacc8e6a044b6bc51a53d))
* **camera:** ultra-wide lens camera turned 4 degrees, bigger kids, stronger vignette ([b8bfa1c](https://github.com/earl-gh/kalyeah/commit/b8bfa1cf44ae66ee0c5743e18ebd45c92918d0a4))
* cinematic morning light, fixed see-through joystick circle with a moving knob, longer pin blink ([bf45aac](https://github.com/earl-gh/kalyeah/commit/bf45aac2435aefb81f4490558eb6471faf118a2a))
* four-lane cross street and a longer middle, no floating dots, spinner in the enemy yard, longer pin dash with a small end circle, enemy walls toggle ([6d809d0](https://github.com/earl-gh/kalyeah/commit/6d809d020529d0d3fc3858fa2c8b6306c20a37ff))
* **guava:** tap to bite and heal a quarter, throw to deal a quarter or an eighth ([dfeeddb](https://github.com/earl-gh/kalyeah/commit/dfeeddb8d897fffcd3e28858ccdd2e55a87564fd))
* **hud:** fixed bottom-centre controls, no handedness setting; larger, optically centred skill icons ([2431c11](https://github.com/earl-gh/kalyeah/commit/2431c11430ec636b6a574abfb08e2d669afcb203))
* **map:** a team's two wall rows as far apart as the middle, post in the inner wall, scoring behind the inner wall ([35623b1](https://github.com/earl-gh/kalyeah/commit/35623b181dd68b566eaa6f754067a7c12e87d518))
* **map:** a wider four-lane road, pedestrian lanes between the alleys and the road, no yellow bars at the road sides ([42c9de7](https://github.com/earl-gh/kalyeah/commit/42c9de718960d2cfb57ea8e4bd2bf50e8798da32))
* **map:** remove the white pad under the post; scoring needs 2 s behind the inner wall ([7942167](https://github.com/earl-gh/kalyeah/commit/7942167b22dadee43db135d47ce00ec4ff13c461))
* **map:** space the wall rows and electric post like MOBA towers and nexus, lane 105 m ([1cf3be2](https://github.com/earl-gh/kalyeah/commit/1cf3be25e95637a9154aff8154a56e98093f08cc))
* **skills:** the pin dash is an aimed skill: tap dashes the way you face, drag chooses the direction ([62739cc](https://github.com/earl-gh/kalyeah/commit/62739cc82a875afbe25e57d8ccceeb495dbd34d1))
* softer walls, no rings under posts, stronger yard colour, grey knob, no aim connector lines, thick guava lane, tumble fall pose, heavy attacks root the caster ([8478aab](https://github.com/earl-gh/kalyeah/commit/8478aabe9f627ee3d09ed28c2a4775a383335e1a))
* **ui:** boot screen with key art and a cardboard loading bar ([68349d2](https://github.com/earl-gh/kalyeah/commit/68349d2210fb06567bd57f3ae00903ee8f61d236))
* **weapons:** Langit Lupa pair: heal (tap self, drag a teammate) and the soil block, in the weapon pick ([f0d2d2c](https://github.com/earl-gh/kalyeah/commit/f0d2d2c99bdaaef8e53b9aeec32b1a810f05f85d))


### Bug Fixes

* **art:** drop the coloured ring and show whole skill icons on the cardboard buttons ([a483afd](https://github.com/earl-gh/kalyeah/commit/a483afd3c65be24ecb242383819617e11699993e))
* **hud:** hide the guava button until you hold the guava ([8409a43](https://github.com/earl-gh/kalyeah/commit/8409a43638023414cbb5050f25878da5ea585a45))
* **hud:** the ready pin button shows the dash arrow, the return arrow only while out on the pin ([08faf79](https://github.com/earl-gh/kalyeah/commit/08faf799308712154cc4a86387a4fd3ab9ed48dc))
* **kids:** swap run feet only on the full front and back views ([83059d1](https://github.com/earl-gh/kalyeah/commit/83059d11deaf66357553b0e819a9d09e84ec9fa8))
* **ui:** cut the cardboard out of the loading bar and centre it in the empty track ([3fadbd1](https://github.com/earl-gh/kalyeah/commit/3fadbd1a61f35d0e5a6b1663f830cfb697a4b75f))

## [0.1.0](https://github.com/earl-gh/kalyeah/compare/kalyeah-v0.1.0...kalyeah-v0.1.0) (2026-10-11)


### Features

* **art:** painted skill icons on a cardboard disc for every skill button ([eef859d](https://github.com/earl-gh/kalyeah/commit/eef859dd96891ec3bd802c40d654e4e0a9be3602))
* **art:** the lane is a concrete alley lined with tall house walls; only the cross street is a road ([0361e2b](https://github.com/earl-gh/kalyeah/commit/0361e2b703880689abc16ffc76b74799a8d87459))
* **camera:** straight behind the hero (yaw 0) ([6622aed](https://github.com/earl-gh/kalyeah/commit/6622aed607a37e03bd3aacc8e6a044b6bc51a53d))
* **camera:** ultra-wide lens camera turned 4 degrees, bigger kids, stronger vignette ([b8bfa1c](https://github.com/earl-gh/kalyeah/commit/b8bfa1cf44ae66ee0c5743e18ebd45c92918d0a4))
* cinematic morning light, fixed see-through joystick circle with a moving knob, longer pin blink ([bf45aac](https://github.com/earl-gh/kalyeah/commit/bf45aac2435aefb81f4490558eb6471faf118a2a))
* **guava:** tap to bite and heal a quarter, throw to deal a quarter or an eighth ([dfeeddb](https://github.com/earl-gh/kalyeah/commit/dfeeddb8d897fffcd3e28858ccdd2e55a87564fd))
* **hud:** fixed bottom-centre controls, no handedness setting; larger, optically centred skill icons ([2431c11](https://github.com/earl-gh/kalyeah/commit/2431c11430ec636b6a574abfb08e2d669afcb203))
* **map:** a team's two wall rows as far apart as the middle, post in the inner wall, scoring behind the inner wall ([35623b1](https://github.com/earl-gh/kalyeah/commit/35623b181dd68b566eaa6f754067a7c12e87d518))
* **map:** remove the white pad under the post; scoring needs 2 s behind the inner wall ([7942167](https://github.com/earl-gh/kalyeah/commit/7942167b22dadee43db135d47ce00ec4ff13c461))
* **map:** space the wall rows and electric post like MOBA towers and nexus, lane 105 m ([1cf3be2](https://github.com/earl-gh/kalyeah/commit/1cf3be25e95637a9154aff8154a56e98093f08cc))
* **skills:** the pin dash is an aimed skill: tap dashes the way you face, drag chooses the direction ([62739cc](https://github.com/earl-gh/kalyeah/commit/62739cc82a875afbe25e57d8ccceeb495dbd34d1))
* softer walls, no rings under posts, stronger yard colour, grey knob, no aim connector lines, thick guava lane, tumble fall pose, heavy attacks root the caster ([8478aab](https://github.com/earl-gh/kalyeah/commit/8478aabe9f627ee3d09ed28c2a4775a383335e1a))
* **ui:** boot screen with key art and a cardboard loading bar ([68349d2](https://github.com/earl-gh/kalyeah/commit/68349d2210fb06567bd57f3ae00903ee8f61d236))
* **weapons:** Langit Lupa pair: heal (tap self, drag a teammate) and the soil block, in the weapon pick ([f0d2d2c](https://github.com/earl-gh/kalyeah/commit/f0d2d2c99bdaaef8e53b9aeec32b1a810f05f85d))


### Bug Fixes

* **art:** drop the coloured ring and show whole skill icons on the cardboard buttons ([a483afd](https://github.com/earl-gh/kalyeah/commit/a483afd3c65be24ecb242383819617e11699993e))
* **hud:** hide the guava button until you hold the guava ([8409a43](https://github.com/earl-gh/kalyeah/commit/8409a43638023414cbb5050f25878da5ea585a45))
* **hud:** the ready pin button shows the dash arrow, the return arrow only while out on the pin ([08faf79](https://github.com/earl-gh/kalyeah/commit/08faf799308712154cc4a86387a4fd3ab9ed48dc))
* **kids:** swap run feet only on the full front and back views ([83059d1](https://github.com/earl-gh/kalyeah/commit/83059d11deaf66357553b0e819a9d09e84ec9fa8))
* **ui:** cut the cardboard out of the loading bar and centre it in the empty track ([3fadbd1](https://github.com/earl-gh/kalyeah/commit/3fadbd1a61f35d0e5a6b1663f830cfb697a4b75f))

## [0.1.0](https://github.com/earl-gh/kalyeah/compare/kalyeah-v0.1.0...kalyeah-v0.1.0) (2026-10-11)


### Features

* **art:** painted skill icons on a cardboard disc for every skill button ([eef859d](https://github.com/earl-gh/kalyeah/commit/eef859dd96891ec3bd802c40d654e4e0a9be3602))
* **camera:** straight behind the hero (yaw 0) ([6622aed](https://github.com/earl-gh/kalyeah/commit/6622aed607a37e03bd3aacc8e6a044b6bc51a53d))
* **camera:** ultra-wide lens camera turned 4 degrees, bigger kids, stronger vignette ([b8bfa1c](https://github.com/earl-gh/kalyeah/commit/b8bfa1cf44ae66ee0c5743e18ebd45c92918d0a4))
* cinematic morning light, fixed see-through joystick circle with a moving knob, longer pin blink ([bf45aac](https://github.com/earl-gh/kalyeah/commit/bf45aac2435aefb81f4490558eb6471faf118a2a))
* **guava:** tap to bite and heal a quarter, throw to deal a quarter or an eighth ([dfeeddb](https://github.com/earl-gh/kalyeah/commit/dfeeddb8d897fffcd3e28858ccdd2e55a87564fd))
* **hud:** fixed bottom-centre controls, no handedness setting; larger, optically centred skill icons ([2431c11](https://github.com/earl-gh/kalyeah/commit/2431c11430ec636b6a574abfb08e2d669afcb203))
* **map:** a team's two wall rows as far apart as the middle, post in the inner wall, scoring behind the inner wall ([35623b1](https://github.com/earl-gh/kalyeah/commit/35623b181dd68b566eaa6f754067a7c12e87d518))
* **map:** remove the white pad under the post; scoring needs 2 s behind the inner wall ([7942167](https://github.com/earl-gh/kalyeah/commit/7942167b22dadee43db135d47ce00ec4ff13c461))
* **map:** space the wall rows and electric post like MOBA towers and nexus, lane 105 m ([1cf3be2](https://github.com/earl-gh/kalyeah/commit/1cf3be25e95637a9154aff8154a56e98093f08cc))
* **ui:** boot screen with key art and a cardboard loading bar ([68349d2](https://github.com/earl-gh/kalyeah/commit/68349d2210fb06567bd57f3ae00903ee8f61d236))


### Bug Fixes

* **art:** drop the coloured ring and show whole skill icons on the cardboard buttons ([a483afd](https://github.com/earl-gh/kalyeah/commit/a483afd3c65be24ecb242383819617e11699993e))
* **hud:** hide the guava button until you hold the guava ([8409a43](https://github.com/earl-gh/kalyeah/commit/8409a43638023414cbb5050f25878da5ea585a45))
* **hud:** the ready pin button shows the dash arrow, the return arrow only while out on the pin ([08faf79](https://github.com/earl-gh/kalyeah/commit/08faf799308712154cc4a86387a4fd3ab9ed48dc))
* **kids:** swap run feet only on the full front and back views ([83059d1](https://github.com/earl-gh/kalyeah/commit/83059d11deaf66357553b0e819a9d09e84ec9fa8))
* **ui:** cut the cardboard out of the loading bar and centre it in the empty track ([3fadbd1](https://github.com/earl-gh/kalyeah/commit/3fadbd1a61f35d0e5a6b1663f830cfb697a4b75f))

## 0.1.0 (2026-10-10)


### Features

* **art:** Blender generator and six skinned kid models ([1b58bb5](https://github.com/earl-gh/kalyeah/commit/1b58bb50e7bc57aa8d8ae5fa302be3fb91274acc))
* **art:** low-poly Pinoy street, six kids, props, chunky UI theme, icons and synthesized audio ([c21353f](https://github.com/earl-gh/kalyeah/commit/c21353fddcc88d7f070ecb860282a51dadf8e1f1))
* **art:** Pillow painter for skill icons, button face and street textures ([4cf0617](https://github.com/earl-gh/kalyeah/commit/4cf0617308fb9424764fe96d09939e16914d4e83))
* **art:** soft CoC look, ML Brawl camera, rigged kids, guava heal, flat cardboard, sound and copy pass ([09e20be](https://github.com/earl-gh/kalyeah/commit/09e20be24446075c58a65c892a6db1012f553856))
* **camera:** bird's-eye view from the back right; controls follow the screen ([a17fba3](https://github.com/earl-gh/kalyeah/commit/a17fba3fa695e81fb932936822dec8ddcde96397))
* **camera:** higher gameplay camera; paving all around the map ([1a125c2](https://github.com/earl-gh/kalyeah/commit/1a125c232457a71c81d3522d3b0bf77a0a6c48e3))
* **characters:** outfits and hair match the reference art ([fa9267f](https://github.com/earl-gh/kalyeah/commit/fa9267f7800abac6fdb4f49b87070f5fe906feed))
* **core:** create Godot 4.6 project (M0 task 1) ([#1](https://github.com/earl-gh/kalyeah/issues/1)) ([66bf6f6](https://github.com/earl-gh/kalyeah/commit/66bf6f6f94cfb330f5446e4eb06aaf86cf931b7a))
* **core:** folder layout and autoload stubs (M0 task 2) ([#2](https://github.com/earl-gh/kalyeah/issues/2)) ([4b5ecf6](https://github.com/earl-gh/kalyeah/commit/4b5ecf627dc51b0cd75782b9ddf00361525193b9))
* **export:** Web, Android and Server export presets (M0 task 3) ([#3](https://github.com/earl-gh/kalyeah/issues/3)) ([6215e62](https://github.com/earl-gh/kalyeah/commit/6215e628b370d771af9042ab589f1b7cac3844c5))
* **hud,camera:** League of Legends camera and the hero overhead spec ([7188062](https://github.com/earl-gh/kalyeah/commit/7188062586c6752f07b63f6dba5c2f252b13bac0))
* **hud:** knocked-out card with countdown ring, loadout and change weapons button ([c19bce9](https://github.com/earl-gh/kalyeah/commit/c19bce972d0087c4cc7be9bc63ca6a04c4dd5541))
* **hud:** minimap and settings top left, score top right, settings menu ([b6638a4](https://github.com/earl-gh/kalyeah/commit/b6638a48ee9217476896b80436a2e411f6aed2bb))
* **hud:** one-thumb controls around a corner joystick, square slanted minimap, centred timers ([84ed1b2](https://github.com/earl-gh/kalyeah/commit/84ed1b2e1f19017a6a7ed62bbd0e96388e9f722d))
* **hud:** painted skill icons and gold-rimmed button faces ([3820ea0](https://github.com/earl-gh/kalyeah/commit/3820ea0f284c207ac921d9eb2c4a9dda0246a4ca))
* **hud:** settings modal with a left or right hand switch ([ac5d010](https://github.com/earl-gh/kalyeah/commit/ac5d01019323e0d6b6159c08dcc841653fdedd02))
* **hud:** type-coloured painted skill buttons with the art cropped inside ([64dceec](https://github.com/earl-gh/kalyeah/commit/64dceecdf6f70a6959237e00e92d765040018946))
* **map,camera:** 3:4 middle between the inner walls; camera tuned to the reference ([4e1d2e1](https://github.com/earl-gh/kalyeah/commit/4e1d2e1450134f94ab810f0c525ac23b4ba95bbc))
* **map:** asphalt, pavers, wall doodles, store sign, posters and road chalk ([cb75d5a](https://github.com/earl-gh/kalyeah/commit/cb75d5acaf4cd0af40cede08c92998d2ac2dc44d))
* **map:** greybox lane map with bases, boundary walls and 2x3 wall columns per side ([9879f13](https://github.com/earl-gh/kalyeah/commit/9879f13f2d4f6fd785cc8008176d8af1dddc6ba1))
* **net:** online multiplayer with rooms, headless server and synced matches ([d4d2035](https://github.com/earl-gh/kalyeah/commit/d4d20358bc020fffb80bd74470ead4b75ff497a9))
* **objectives:** breakable walls, base scoring with sets and side switch, rubber ball, tricycle ([046b1c9](https://github.com/earl-gh/kalyeah/commit/046b1c9dfbc720486837ae20072e10852cfc995b))
* one pin skill replaces Dash and Bookmark; cool bright light; no blur or damage text; cropped minimap; camera step back ([eb112b9](https://github.com/earl-gh/kalyeah/commit/eb112b9632c6b285668e30b0b4fd29a7863a22c0))
* **practice:** dummies, ally dummy, gray HP HUD and locked skill buttons; docs for death delay and no basic attack ([9f8b641](https://github.com/earl-gh/kalyeah/commit/9f8b6416eae65e464b59750c4104ae696aa3f6e7))
* **practice:** offline practice scene with follow camera, on-screen joystick and WASD ([6a2e9a1](https://github.com/earl-gh/kalyeah/commit/6a2e9a1dd9d0bff733717ae41b11f457516b09c6))
* **sim:** circle-vs-AABB movement collision with walls and lane bounds ([653f941](https://github.com/earl-gh/kalyeah/commit/653f941b5d89d43ce3ecbd93e52d04a88f946217))
* **sim:** death delay (gray HP, teammate/post revive, once per respawn), no basic attack, training dummies ([c198eb4](https://github.com/earl-gh/kalyeah/commit/c198eb48295c16cac05ba1b0b3ae53310580815d))
* **sim:** HP, death and respawn, dash with stumble, and bookmark; practice HUD and touch skill buttons ([3c7f998](https://github.com/earl-gh/kalyeah/commit/3c7f9986e5b6c1423a1d3a2752ed3544869aa48a))
* **sim:** MatchSim core with PlayerState, PlayerInput, GameRules and seeded RNG ([1c8945a](https://github.com/earl-gh/kalyeah/commit/1c8945a744167b4eda6a7ddc292e9582e4a12ff6))
* **sprites:** Junjun knockout stagger and lying poses ([04a3223](https://github.com/earl-gh/kalyeah/commit/04a3223d7a056a9cbce9e01f1ff6eb02c07028f1))
* **sprites:** slice and key the Junjun sheets ([a8eec33](https://github.com/earl-gh/kalyeah/commit/a8eec33a25417ba14d49a40c7f103bfc2c6333a1))
* **ui:** MOBA camera, shadows and outlines, richer street, ML-style HUD ([e0f41d1](https://github.com/earl-gh/kalyeah/commit/e0f41d12729ad3c0cc0dc999a09a8d966312aa49))
* **ui:** painted bevelled 9-slice frames for buttons, panels and pills ([2015c85](https://github.com/earl-gh/kalyeah/commit/2015c85ce502eed522ec47612091f9f521723e2d))
* **ui:** show a rotate-your-phone prompt when held in landscape ([3d0aa2d](https://github.com/earl-gh/kalyeah/commit/3d0aa2d415035c832c31d5cbadf795f4aeada014))
* **ui:** title screen with stub buttons (M0 task 5) ([#5](https://github.com/earl-gh/kalyeah/issues/5)) ([3b09f38](https://github.com/earl-gh/kalyeah/commit/3b09f3815814317f2566641e934c99d494f22fac))
* **ui:** weapon pick with loadout sockets, painted cards, stats and a countdown ring ([81b142e](https://github.com/earl-gh/kalyeah/commit/81b142e0ad0490923cce5b12b1bb98af52ebdf06))
* **view:** cinematic daylight lighting and a film finish with depth of field ([2ecb2f5](https://github.com/earl-gh/kalyeah/commit/2ecb2f5d70ded439c93ee2cb8bb5b51061f03f55))
* **view:** kids are drawn from sprite sheets ([3d4c28e](https://github.com/earl-gh/kalyeah/commit/3d4c28e9db2f151beb91063318d0a24d82bacc7b))
* **view:** kids use the Blender skinned models; docs ([b90878a](https://github.com/earl-gh/kalyeah/commit/b90878a90be35908878bfe19c0a8d9b720c80570))
* **walls:** visible damage: flash, wobble, chips, numbers, health bar and rips ([56380bb](https://github.com/earl-gh/kalyeah/commit/56380bb63556219d40d460afbb296685f6584c45))
* **weapons:** add the 12 street-game weapons, status effects, weapon pick and random characters ([d5d5fd2](https://github.com/earl-gh/kalyeah/commit/d5d5fd2ae08b3190fedae2ba5e8fb852abb5a58e))
* **weapons:** unlimited weapon swaps while dead (D13) ([da6b5ec](https://github.com/earl-gh/kalyeah/commit/da6b5ec1fc9f72d86266cb56cf8f5f1c4039aa33))


### Bug Fixes

* **hud:** hold one skill at a time; respawn countdown and button at the bottom ([964694f](https://github.com/earl-gh/kalyeah/commit/964694f47fb52bb83f5307593a727897357ce800))
* **kids:** keep the run pose between sim ticks and stop the left-right flicker ([621554a](https://github.com/earl-gh/kalyeah/commit/621554a8e9bd86c81071b6a1d76a2208707aff14))
* **kids:** mirror the run frame every step so the forward foot alternates ([25a5896](https://github.com/earl-gh/kalyeah/commit/25a589654a7954f5030803f04ac7395865be57e8))
* **sim:** build blocking rect list without untyped duplicate() ([c7dd8fc](https://github.com/earl-gh/kalyeah/commit/c7dd8fcfaac38fe01303d31e00e1462d42a5f1e2))
* **sim:** teams pass through their own cardboard walls; only the enemy is blocked ([3b2e143](https://github.com/earl-gh/kalyeah/commit/3b2e143f57ff8f2aea5d0ddd1f3fd51280a594ef))
* **skills:** bookmark cooldown starts on return, no precasting, auto-aim locks at the press ([2c78a72](https://github.com/earl-gh/kalyeah/commit/2c78a724bd4267cc49bb30e90d96e109a2f1dae9))
* **status:** a new status overwrites the current one ([e7ea417](https://github.com/earl-gh/kalyeah/commit/e7ea417676dcb839144257a2246b32f1d3f1ed9f))
* **weapons:** with no enemy in range the targeted rock hits the nearest enemy wall ([5834001](https://github.com/earl-gh/kalyeah/commit/583400134a83540f6994797feed256b803dcb760))
