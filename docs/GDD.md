# Game Design Spec (source of truth for rules + starting numbers)

All numbers live in `res://data/` resources, never hardcoded. Units: meters, seconds. Values below are **starting points** for balancing; change them in data, not code.

## Core
Kalyeah's core match is based on **Agawan Base**. `Langit Lupa` is a weapon category in the loadout, not the match ruleset.

| Key | Value |
|---|---|
| Player max HP | 100 (same for everyone) |
| Move speed | 5.0 m/s |
| Server tick | 30 Hz; snapshots to clients at 20 Hz |
| Weapon pick time | 10 s (match start); unpicked slots auto-fill with random weapons |
| Tag | No deaths: at 0 HP you are **tagged** and wait in your team's box until a teammate rescues you (see Tag); weapons can be swapped any number of times while you wait (D13) |
| Basic attack | **None.** There is no basic attack: players only use the two weapons they picked (owner decision). There are no default skills either: Takbo (dash) and the pin were removed (owner) |

## Character and weapon select (owner, like a fighting game)
One screen at the start of a match (practice and online; the 10 s timer is the same): the title plate with the countdown ring, your two weapon sockets, then **row 1: the six characters as 3:4 profile cards** (portrait, name; tap to pick, gold frame on the chosen one), **row 2: a horizontally scrolling tab per street game** (Bato-bato-pik, Tumbang preso, Jackstone, Trumpo, Langit Lupa) over that game's **weapon cards**: the painted art on the left, the name with its type tag, **its description below the name** (`description` in `data/weapons/*.tres`) and the numbers under that. The character row can also scroll on unusually narrow screens. Tap two weapons, then READY (an empty weapon slot is filled at random when the timer ends; the kid is always the selected one). The respawn swap in the box shows only the weapons: a kid is fixed for the match.

## Characters (cosmetic, chosen by the player)
- Roster (`data/characters/*.tres`, `CharacterDef`): `junjun` boy, `ligaya` girl, `migo` gay boy, `toni` lesbian girl, `popoy` chubby boy, `inday` dark-skinned girl. Outfits in `docs/PROJECT.md` §3.2.
- **You pick your kid (owner)** on the select screen at the start of every match, practice and online (see Character and weapon select). Your last pick is selected first and is also your face on the main screen. **A team never has the same kid twice, but the other team may play the same kid (once).** The server starts everyone on a random kid (seeded shuffle); picking a kid your teammate holds swaps with them, and a kid a teammate has already confirmed is greyed out ("TEAMMATE") and cannot be picked. The other team's picks never block you, so a kid can be in a match at most twice, once per team. Fixed for the whole match, including respawns and set switches.
- Purely visual: identical hitbox (circle r=0.4 m), HP, speed, animations timing. Shown in loading screen, scoreboard and kill feed.
- Team readability: team-colored bandana/armband + ground ring; enemy outline on the local client.
- Practice mode: random character for the player, dummies use the others. Practice has three training dummies on the enemy team (one stands still, one walks sideways +/-3 m around its spot at half speed, and a free one stands in the enemy's tagged box so you can see its bar and minimap dot hide) and one ally dummy that **runs to your box and rescues you** when you are tagged, then walks back to its spot. Dummies never attack or use skills. A tagged dummy walks out of the box by itself after 6 s (`dummy_release_time`) and goes back to its spot. Dummies never count for a capture, so a team of dummies can never score.

## Tag (replaces the death delay)
The match is the Filipino street game of tagging the enemy base, like the reference video:
- **Tagged, every time.** When HP reaches 0 the player is **tagged** (no limit; there is no real death and no respawn timer). They are sent at once to their team's **box**, a taped rectangle on the street. From your point of view, tagged allies (and you) go to the box at the **top left**, tagged opponents to the box at the **bottom right** (each team's box is at the far end, on the same side as its own post).
- In the box: no moving, no skills, can't be hit or healed; cooldowns keep running. A *TAGGED!* line slides in at the left under the minimap (blue for your team, red for theirs) and *Tagged* shows over the kid's bar. Your controls hide and a card says how to get out, with a **Change weapons** button (D13).
- **Rescue:** a free teammate who steps into the **rescue area** round the box (or the box itself) sets **everyone** waiting there free (data toggle `rescue_frees_all`; off = one per touch). Rescued players go straight back to **their post** at full HP (cooldowns keep running). A *RESCUED!* line shows.
- Numbers and toggles in `data/rules/game_rules.tres` (`rescue_frees_all`, `dummy_release_time`); box size and place in `data/rules/map_layout.tres` (`jail_size`, `jail_inset`).

## Controls (owner)

- **Stick to move (owner: back from tilt).** An on-screen joystick at the **bottom left** moves your kid: push up to run up the screen, sideways to run sideways. **Always a run (owner):** the stick only picks the direction; any drag past a small dead zone, however slight, runs at full speed (there is no slow walk). It stays in place (a see-through ring with a knob that follows your thumb); let go and you stop. Keyboard WASD / arrows still work on desktop.
- **Auto-run (owner):** at rest only the knob and the inner ring show, and a touch must land inside the inner ring to grab the knob; a second, outer ring then appears around it (so you cannot start auto-run by pressing straight on the outer ring). Drag the knob so most of it is past the inner ring and the outer ring lights up (same white and thickness as the inner ring: the unlit ring is the dimmer one, and lighting the outer ring swaps which one is dim and tints the band between the two rings): let go there and your kid keeps running that way at full speed on their own. Let go with the knob (mostly) inside the inner ring and they stop. While auto-running, touching the stick area grabs the knob at once (no unlock delay) and you steer again. Auto-run stops when the controls hide (menu, weapon pick, tagged) and when a new round starts.
- **Skills at the bottom right (owner):** weapon 1 and weapon 2 side by side, 124 px (thumb sized), weapon 2 against the right margin; the stick's area is an outline only (no tint). All three in one row near the bottom within thumb reach, the stick under the left thumb and the skills under the right. Drag a weapon button to aim, release to cast (or drag onto the cancel zone).
- There is no dash.
- Keyboard and mouse still work for testing on a computer (WASD / arrows to move, Q / E to aim and cast).

**One skill at a time (owner):** while one weapon button is held (aiming), the other can't be pressed or cast until the release.

**No precasting (owner):** a weapon on cooldown is disabled. Its button is dimmed and ignores presses, and holding a key or button through the cooldown does nothing when the cooldown ends; it needs a fresh press.

## Weapons
Status effects: `STUN` (no move/cast), `SLOW(x%)`, `AIRBORNE` (stun + vertical anim, no knockback), `POLYMORPH` (can: no cast, 50% speed), `BOUNCE` (short airborne), `KNOCKOUT` (downed, no actions).

- **Casting while moving (owner):** every weapon can be aimed while you keep moving. **Light attacks** can also be cast while moving (no rooting). **Heavy attacks** (Bato H, Gunting H, Tsinelas H; `cast_lock` in `data/weapons/*.tres`, 0.5-0.6 s) root the caster until their cast animation finishes.
- **Aim previews:** a ground circle (rock meteor and the like) is drawn alone, with no line from the player to it.
Statuses never stack: only one is active at a time and a new status of a different kind overwrites the current one. The same status re-applied keeps the longer time (SLOW keeps the stronger slow).

| ID | Type | Shape | Range | Damage | Effect | CD |
|---|---|---|---|---|---|---|
| bato_light | ATK | targeted projectile, nearest enemy in range | 7 | 14 | — | 4 |
| bato_heavy | ATK | ground AoE r=2.0, 0.6 s delay | 8 | 22 | STUN 2 s | 12 |
| gunting_light | ATK | cone 60°, 3 m, 2–6 snips (by hold/tap) | 3 | 4/snip | — | 5 |
| gunting_heavy | ATK | cone 60°, 3.5 m, 0.5 s delay | 3.5 | 24 | — | 9 |
| papel_trap | CC | placed, 0.75 s arm; triggers r=1.5 → slow zone r=2.5, 3 s | 6 | 0 | SLOW 40% | 12 |
| papel_shield | BLOCK | wall 3 m wide in front, 2.5 s, blocks enemy projectiles | — | — | — | 14 |
| tsinelas_light | ATK | 3 boomerangs, fast, 0.15 s apart, out + back | 7 | 8 each way | — | 6 |
| tsinelas_heavy | ATK | 1 large boomerang, slow, out + back | 8 | 18 each way | — | 10 |
| lata | CC | thrown to point, r=1.5 | 7 | 5 | POLYMORPH 2 s | 14 |
| jacks | CC | scatter field r=2.0, lasts 4 s | 6 | 3 per 0.5 s | SLOW 30% | 11 |
| bola | CC | skillshot, 2 bounces (at 1/2 and full range), r=1.2 per bounce | 9 | 6 per bounce | BOUNCE 1 s | 12 |
| trumpo | CC | linear spinning projectile, stops at max range | 8 | 10 | AIRBORNE 2 s (once per target) | 15 |
| heal (Langit Lupa) | HEAL | instant: **tap heals yourself**, **drag toward a teammate** (within 8 m, 60° cone around the drag) heals them; a drag with nobody there does nothing and costs no cooldown. Never heals enemies. Aim preview: a ring around the teammate, no line to them | 30 HP (`heal`) | — | — | 12 |
| langit_lupa (Langit Lupa) | BLOCK | a **person-wide (1.2 m)** block of earth rises 2.2 m in front of you in the aimed direction, blocks enemy projectiles, then **collapses back to the ground after 3.5 s** | — | — | — | 14 |

Two copies of the same weapon cannot be equipped; two weapons of the same *type* can.

### Casting and aiming
- **Cast on release.** Hold a weapon button to aim, release to cast. Releasing over the **Cancel** zone (shown above the buttons while aiming) cancels; nothing is spent. Keyboard: hold Q / E to aim at the mouse, release to cast; Esc or right mouse while holding cancels.
- **Auto-aim:** a tap (or a drag shorter than the deadzone, `aim_deadzone` 0.1 of full stick) aims at the nearest targetable enemy in range; with nobody in range it fires straight ahead (ground shapes land at full range). Drag length scales the distance for ground shapes (lata, bato heavy, papel trap, jacks).
- **Auto-aim never follows a moving enemy while you hold (owner, fairness).** It is taken once, at the moment you press: the spot (relative to you) or, for `bato_light`, the enemy. Holding keeps that aim; if the enemy walks away the cast still goes where they were. Dragging past the deadzone aims by hand instead.
- `bato_light` throws at the enemy locked at the press. If there is none, or that enemy has left range (or is tagged) when you release, nothing is thrown and **no cooldown** is spent. Once thrown, the rock homes on that enemy (a targeted projectile, per the weapon table).
- A hard CC (or getting tagged) while aiming drops the aim; press again when it ends.
- `gunting_light` snips: 2 on a tap, +1 per 0.25 s held, max 6, all on release.
- Projectiles are stopped by obstacles (for both teams: you can hide behind cover), the street edges and enemy paper shields; they pass through their own team's shields.
- Boomerangs (tsinelas) hit each enemy once going out and once coming back, turn back at max range or when they hit an obstacle, and are caught within 0.8 m of the thrower (`boomerang_catch_distance`).
- An unarmed `papel_trap` waits on the ground for 20 s (`lifetime`) before it disappears.
- Lata travels at 14 m/s to its point before it lands; bato heavy lands 0.6 s after the cast.
- Effects are applied before damage. Tagged players are not hit by anything.

## Map (a barangay street, 30 m wide × 40 m long)
Copied from the reference video (owner): one street. **The playing area is 1:2 (owner), 24 × 48 m**, and the minimap (100 × 200 px) has exactly the same shape. The cover wall sits 11 m from the base centre, as before.
- **No outline round the posts (owner):** the base circle's team tint (blue yours, red theirs) tells the bases apart.
- **Posts and bases:** each team's electric post stands in a corner, 2.5 m in from both edges (`post_inset`): from your point of view **your post is bottom left, theirs top right**. The **base** is a taped circle of 4.5 m round the post (`base_radius`), cut off by the street edges, tinted blue (yours) or red (theirs). Players start next to their post and come back there after a rescue (`spawn_offset`).
- **Boxes:** each team's tagged box is a **3.6 × 3.6 m square** (`jail_size`), flush in a corner of the play area at the far end, on the same side as its post (yours top left, theirs bottom right; `jail_inset` is half the size, so no margin). Room for four kids standing 2 × 2 (`jail_columns`, `jail_rows`). It has **no tint and no tape**: two cardboard walls close its two sides that face the street (yours: bottom and right; theirs: top and left), the other two are the curb and the end line. The walls are scenery only and block nothing, so a teammate walks straight in to rescue. **Rescue area (owner):** round the box is a rescue area that reaches half a box size (`rescue_scale` = 0.5, 1.8 m) past it on the two sides that face the street, so the box and its area together are 1.5x (5.4 m): a faint team tint and team tape on its outer sides (yours: bottom and right; theirs: top and left), like the base circle.
- **Cover (owner: only two cardboard walls, one each way round; they are not objectives):** only two cardboard walls kids hide behind: the left-most one against the left curb, 6 m above the bottom-left base circle, and the right-most one against the right curb, 6 m below the top-right base circle (owner: no other props inside the play area). The rows across both ends mix the owner's street models (fishball cart, bench, crates, school-zone sign, basketball hoop, truck tyres, ironing board, wooden A-frame).
- **The play area is plain, flat, dark grey cement (owner):** no tiles, texture, gutters, chalk games or manholes on it. The street edges (an invisible wall at the sidewalk: no white border, owner) and both ends (sawhorse barricades) block everyone. Outside: sidewalks, house fronts, a sari-sari store with an umbrella (no power poles along the sides and no bunting, owner); the street goes on past both ends to more houses. The look is in `docs/ART.md`.
- There are no breakable walls, no cross street, no guava and no tricycle any more (owner).

## Vertical layout (portrait game)
- The street runs **along the screen's long axis**: your end at the bottom, the enemy end at the top. The server/sim map is orientation-agnostic (lane axis = Z); only the camera and UI know about portrait.
- Camera (data, tunable in `data/rules/camera_rules.tres`), **copied from League of Legends** (owner):
  - **Portrait 9:16 mobile camera (owner, like the reference video):** a perspective camera above and behind the hero (straight behind, yaw 0) at a **fixed 26° downward angle** (owner: a bit higher and pitched slightly forward, was 20°) with an **ultra-wide 75° vertical field of view** (owner: like a phone's 0.5x camera, wider with straight lines, not a fisheye), so you see far up the street. The distance is derived so about **10 m** (owner: closer and a bit lower, was 12 m) is visible across the screen at the aim point on any phone shape (about 11.6 m away, 5.1 m up on 720x1280: closer with a wider lens, so more perspective and more street at the sides). **Lens curve (owner):** `lens_curve` -0.05, a barely visible concave bend (`shaders/lens_curve.gdshader`) that only softens the edge stretch of the wide view, the way a phone's ultra-wide camera corrects its distortion; health bars and mouse aim go through the same curve (`LensCurve`); 0 turns it off; the camera follows the hero everywhere (see below). The hero is held a bit below the middle of the screen (62% of the height from the top). The reference video is landscape; Kalyeah stays portrait (binding), so this is the same angle and lens on a tall screen. Tilt, drag-aim and keyboard follow the screen: up is toward the top of the screen. All numbers live in `data/rules/camera_rules.tres` (`pitch_degrees`, `vfov_degrees`, `visible_width`, `player_screen_y`, `follow_rate`).
  - **Smooth tracking, never held back by the map (owner):** the camera eases after the hero (7/s) and **always follows them**, up to every edge and corner of the street. It is the **hero** (the sim) that is kept inside the play area, by the curbs and the end barricades, so the camera never gets stuck while the hero walks toward an edge. Past the play area the player sees the sidewalks, house fronts and the street going on.
  - The minimap shows the whole street.
  - Team-relative: both teams see their own base at the bottom (the view flips for the other team).
- Offscreen awareness: edge indicators for allies/enemies plus the street minimap. Aim previews and skill ranges must remain readable within the visible area (long-range weapons rely on drag-to-aim and edge indicators).
- Ranges and speeds stay as specified; balance is re-checked in M6 with real play on phones.

## Main screen (owner)
- **Mode and map cards (owner):** under the map model sits one row: a **MODE** card and a **MAP** card (each 2:1) and **START** at the right-most side. Defaults: mode **Hotspot**, map **Agawan Base** (hotspot first, not online). Each card shows MODE or MAP small at its top left and the choice big at its bottom left (no subtitle). Tapping a card opens a carousel of the same 2:1 cards over a background tint, all three visible at once: modes Hotspot, Online, Practice; maps Agawan Base, Tumbang Preso, Patintero. START opens that mode (Hotspot / Online: Create or Join; Practice: starts at once). Tumbang Preso and Patintero have no game yet: selectable, START reads SOON and is disabled. (Replaces the old four buttons.)
- **Background:** the owner's blue diamond picture. **Top:** your profile (the face of a random character, picked once, and your name: tap to rename, 1 to 12 characters, a default like "Kalye482" for a new player) and **Settings**. The game name is omitted. **AGAWAN BASE** is attached to **the map model** (owner: nothing moves, no kids, no team colours, no sidewalks, no extra street past the ends): the far half of the play area as a real, exactly 1:1 (24 × 24 m) model (like Clash Royale's main screen; cement on a thick stone base with rounded corners that sits on a thicker, darker, matte blue platform, no overhang; the map title is written on the platform's front face) with only its corner props: the far base's electric post and the cardboard walls of your tagged box.
- **Actions at the bottom:** one large amber **ONLINE** primary action, then **HOTSPOT** and **PRACTICE** in a smaller row. ONLINE opens Create / Join on the game server. HOTSPOT opens the same choice for a server on the same phone hotspot or Wi-Fi and asks for its saved address (e.g. 192.168.43.1:9080; private addresses use plain `ws://`, so it works in Android; an HTTPS browser page cannot open it). **QUIT GAME** lives in Settings and Android Back opens the same concise confirmation. No version text, ONLINE caption or server line appears on the main screen.
- **Online private rooms:** the host creates a room and the server returns an **8-character alphanumeric code**. Friends join that exact room by entering the code.
- **Settings:** sound volume (0 to 100%, saved; 0 is silent), vibration on button presses (only on phones), and the online server address (saved; used by Create / Join). Back / Escape closes a dialog, or asks to quit when none is open.

## HUD
- **Over each hero:** a solid health bar with no partitions (owner; green you, blue ally, red enemy) **right above the head** (owner: not high up). **Hiding in the box (owner):** a free kid who walks into its own team's tagged box hides its bar (and status) from the **other team**, and its minimap dot looks tagged too, so it blends in with the tagged kids and the other team cannot tell who is free to rescue. Its own team always sees it. Tagged enemies there show no bar either, so they look the same. **No names.** A status (*Stunned*, *Tagged*, ...) is shown in *italic* above the bar. **No damage numbers**; heals pop a green +N. While a team holds the enemy base, a **spinner** over each of its kids fills as the 5 s run out.
- **Skill buttons:** art only (weapon icons), the weapon's type as a small tag (ATK, CC or BLK), a cooldown number, a ready flash. Buttons on cooldown are disabled.
- **Top:** a **minimap** (exactly 1:2 like the playing area; no frame, no dark panel round it: the street fills it, light and see-through (55% opaque), and everything is cropped to smooth rounded corners (12 px, `shaders/minimap_crop.gdshader`); the whole minimap is drawn 3x bigger in a SubViewport and scaled down smoothly, so no element is pixelated) at the top left showing the whole street with your end at the bottom: your base the blue corner bottom left, theirs the red corner top right, the two taped boxes (yours top left, theirs bottom right), the cover as dark blocks and every player as **the face of their character in a circle** (owner: not colour dots; the faces are cropped from the boot screen key art, `assets/portraits/faces/`, made by `tools/art/crop_minimap_faces.py`) with **one thin ring** in the team colour (blue yours, red theirs, no second outline; you are a little bigger; tagged kids dimmed). The settings button sits beside it, top aligned. The score is at the top right (see below). The settings button opens a compact see-through menu: Take 30 Damage in practice, Resume and Exit Match. There is no health panel in the corner.
- **Feed:** *TAGGED!* and *RESCUED!* lines slide in under the minimap with the kid's name (blue for your team, red for theirs) and fade after 2.6 s; at most three show.
- **Capture countdown:** while a capture runs, big text in the middle says *HOLD THE BASE! 4* (or *ALL TAGGED! 4*) for your team and *DEFEND YOUR BASE! 4* (or *ALL OF YOU TAGGED! 4*) against you.
- **Controls:** the stick at the bottom left and weapon 1 and weapon 2 at the bottom right (see Controls), no handedness setting. Each skill button is a cardboard disc with the painted icon whole on it; the icon's own glow shows the type (attack red, crowd control violet, block blue, heal green).
- **Score card (owner): top right.** Your points (blue, left) and theirs (red) with the **match clock** and current BO3 / BO5 / BO7 between them, counting up from 0:00 like the video (it stops during the opening show and the freezes after points). Nothing sits under the card.
- **Tagged view (owner):** while you wait in the box, a grey band covers the bottom of the screen with "TAGGED!", a row of up to five **2:3 cards** (everyone else in the match, teammates first: portrait, name, TAGGED when they are) and Change weapons. The camera follows a free teammate first (the one who can rescue you); tap any card to follow that player. When you are rescued the camera **snaps straight back** to you at your post.

## Opening show (owner, like the video)
Before the first round (6 s, `intro_time`; the server runs it too, nobody moves and the clock waits): the camera first shows the **enemy post with their team** in the red base, then **your post with your team** in the blue base, with "TAG THEIR BASE! / BO3 • FIRST TO 2" along the bottom. Then the game camera takes over and **GO!** pops up.

## Scoring (one tier: best of 3, 5 or 7)
- **A point** is won by either capture, held for **5 s** (`capture_time`):
  - **Base capture:** every player of your team is free and standing in the enemy base. The countdown starts when the last one steps in and restarts if anyone steps out or gets tagged (so a tagged teammate has to be rescued first).
  - **All tagged:** every player of the other team is waiting in their box. The countdown starts when the last one is tagged and restarts if one is rescued.
- **Every point is a new round (owner):** 3 s freeze (nothing moves), then everyone is back at their own post at full HP, out of the boxes, with cooldowns, status effects and weapon objects cleared. "SCORED! 1-0" (or "THEY SCORED") shows.
- **Match (owner):** one tier, points only: no sets and no switching ends. The match is **best of 3, 5 or 7 points**, picked by the **host in the lobby** (everyone in the room sees the choice): the first team to win a majority (2, 3 or 4 points) wins. Practice is best of 3. After the last point the game stops and shows the result. Numbers in `data/rules/game_rules.tres` (`capture_time`, `point_freeze_time`, `best_of`, `best_of_choices`).
- Training dummies never count for a capture: in practice you score alone even though the ally dummy stays home, and the dummy team can't score.

## Lobby rules
- The host picks **Best of 3 / 5 / 7** in the room (default 3); only the host can change it, only before the start.
- Modes: 1v1, 2v2, 3v3. Passcode: 8 characters, A–Z/2–9, no ambiguous characters.
- Start allowed when: every present player is Ready AND one team has N players AND the other has ≥ N−1 (N=1 requires both).
- Host leaves in lobby → next player becomes host. Player disconnect in match → slot stays, 60 s to reconnect.
- Implementation defaults (M4):
  - A new player joins the smaller team; tap **Join** on the other team to switch, which un-readies you.
  - Names are up to 12 characters ("Player" if empty).
  - Loading waits up to 15 s for every client, then starts anyway.
  - The 10 s weapon pick has 1 s of grace on the server; empty picks are filled at random.
  - A dropped player stands still in the match.
  - The match screen reconnects by itself, and **Rejoin last match** in the lobby (or reloading the page) works within the 60 s.
  - After the window the slot just stays idle.
  - Rooms live in server memory only.
  - Numbers are in `data/rules/net_rules.tres`.
- Netcode:
  - Clients send their input every sim tick (30 Hz). The server applies one input per tick per player, oldest first, so a quick tap is never lost.
  - Snapshots go out at 20 Hz.
  - Your own movement is predicted and corrected; other players are drawn 0.1 s in the past between snapshots.
  - Server-side events (hits, tags, rescues, points) are replayed on the client as the same signals the offline game uses.

## Open decisions (defaults are implemented; change if the designer says so)
- **D1** ~~Basic attack~~ **Decided: no basic attack** (owner). See D10.
- **D2 / D3 / D4 / D5 / D11** Retired: the guava, the tricycle and the breakable walls are gone (owner).
- **D7** Retired with the pin (owner removed dash and the pin).
- **D8 / D9** Retired with the death delay (replaced by Tag).
- **D16** Tag defaults not given by the owner: one teammate in the box rescues **everyone** in it (`rescue_frees_all`, off = one per touch); rescued players keep their cooldowns; the tagged can't move inside the box; the capture counts **every** player of the team (so rescue first). Each capture wins one point; the match is best of 3, 5 or 7 (owner). Confirm or change.
- **D10** With no basic attack, a loadout of only non-damaging weapons (e.g. `papel_trap` + `papel_shield`) deals no damage. Default: allowed; consider requiring at least one damaging weapon at pick time.
- **D12** Unarmed papel trap lifetime (not in the brief): default 20 s.
- **D13** ~~Respawn weapon swap~~ **Decided (owner): swap with no limit while waiting.** Now: while tagged (in the box) you can change weapons as often as you like. The **Change weapons** button on the tagged card opens the swap screen with your current weapons pre-picked (it no longer opens by itself, so you can watch your team come for you); every full pair applies at once; **Done** closes it. It closes when you are rescued. No swapping while free. A weapon kept across a swap keeps its cooldown.
- **D14** *Heal* numbers were not given by the owner: defaults 30 HP, 12 s cooldown, 8 m range, 60° pick cone, tap = self (all in `data/weapons/heal.tres`). Confirm or change.
- **D15** *Lupa (soil block)* numbers were not given by the owner: defaults 1.2 m wide, 2.2 m in front, stands 3.5 s then collapses, 14 s cooldown, blocks projectiles like the paper shield (`data/weapons/langit_lupa.tres`). Confirm or change; say if it should also block movement.
- **D6** Only one Block weapon exists — consider a second one for loadout variety.
