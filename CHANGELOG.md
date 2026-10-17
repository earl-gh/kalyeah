# Changelog

## [0.6.1](https://github.com/earl-gh/kalyeah/compare/kalyeah-v0.6.0...kalyeah-v0.6.1) (2026-10-17)


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
