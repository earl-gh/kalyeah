# Kalyeah spec (source of truth for what we are building)

Owner decisions from the 2026-11-18 spec interview and earlier owner calls. This file says **what** the game is and **why**;
`docs/GDD.md` holds the rules and numbers as built (data in `unity/Data/*.json`), `docs/ART.md` the look, `docs/ROADMAP.md`
the order of work. Where this file and an older doc disagree, this file wins and the older doc is due for a rewrite.

## 1. Vision
A free-to-play 5v5 team shooter-brawler on Android, themed on Filipino street games (*mga larong Pinoy sa kalye*), built as
a full commercial game for the Play Store (the earlier game-dev competition entry is dropped, and its limits with it).

- **Feel:** T3 Arena camera and pace, Valorant clarity and art, a Wild Rift / Mobile Legends shell (home, store, pass, ranked).
- **Fair:** every kid has 100 HP, the same speed and hitbox; kids differ **only by their 3 skills**. Money buys kids early,
  skins and the paid pass, never stats.

## 2. Platforms and devices
- **Android first**, iOS after launch: game code stays platform-neutral (platform code behind small adapters).
- **No device floor on purpose:** install on as many phones as the engine allows (Android 8.0, API 26, is Unity 6.3's
  lowest and is already used; add `armeabi-v7a` next to `arm64-v8a` for older 32-bit phones); low / medium / high quality presets. Performance on old phones is improved after launch;
  60 fps on a mid-range phone is the target, not a gate.
- **Size:** a Play Store base (AAB) under 200 MB; maps, skins and voices in Play Asset Delivery packs (no total cap).
- **Permissions:** only what a shipped feature needs (voice chat needs the microphone; hotspot needs the current network set).

## 3. Accounts, age and safety
- **Sign-in:** guest play first; **Google sign-in first**, then Facebook and Apple. Progress, kids, skins, pass and rank live
  on the `Kalyeah.Server` account; a guest account can be linked later.
- **All ages, with an age gate:** the player's birth year is asked once.
  - **Under 13 (kid mode):** guest or Google family account, quick chat, emotes and pings only, purchases need parent approval
    (Google Family Link), following Google Play's Families policy.
  - **13 and up:** every feature.
- **Social:** quick chat, emotes, pings, text chat with a profanity filter, voice chat, report and mute.
- **Friends and party:** parties of 1 to 5 for Quick Match and private rooms; Ranked allows 1, 2, 3 or 5 (no 4-stacks),
  within a rank range. No clans at launch (a *barkada* system may follow).

## 4. Modes
| Mode | Win | Where |
|---|---|---|
| **Blitz** | First to 3 rounds | Quick Match queue, private rooms, Hotspot, vs AI |
| **Quick** | First to 5 rounds | Quick Match queue, private rooms, Hotspot, vs AI |
| **Standard** | First to 7 rounds | **Ranked** (the only ranked mode), private rooms |
| Practice vs AI, Practice solo | Any mode's rules | Offline; bots at easy / medium / hard; training sandbox |
| Explore | None | Offline; roam any of the 4 maps alone, one daily collectible coin per map, try kids and skins freely; no mini-games |
| Tumbang Preso, Patintero | Own rules | Side modes; hidden now, return before launch |

**Bots** (Practice vs AI and leaver takeover) are role-aware: Strikers dive, Defenders guard and rescue, Seekers
scout, Controllers zone; they use skills, vendors and objectives. Easy, medium and hard change aim and reaction time.

**Online first (Mobile Legends way):** PLAY tries the server; if it cannot connect, a prompt offers offline play, which opens
the offline hub (Hotspot, Practice vs AI, Practice solo, Explore). **Kids are picked after the queue** (Valorant
agent select): an owned kid within a time limit, no duplicates within a team, the other team may share.

**Online:**
- **Quick Match** (Blitz or Quick, unranked) and **Ranked** (Standard) fill 5v5 by skill rating; private rooms (code) stay
  for friends; Hotspot for phones on one local network.
- **Ranked:** Filipino-themed tiers (Kahoy, Bakal, Tanso, Pilak, Ginto, Brilyante, Batang Kalyeah) with divisions and a
  **star system like Mobile Legends** (win +1 star, loss −1 star, a full division promotes); seasonal resets with rewards.
  Replaces the LP system in the current code.
- **Servers:** `Kalyeah.Server` (Docker) on cloud VMs in a Singapore region first; one account and match database.
- **Bad connections:** ping and packet loss on the HUD; a dropped player can reconnect within 3 minutes while a bot plays
  their kid; the queue prefers players under 120 ms.
- **Spectating and replays:** not at launch.
- **Server-authoritative:** the server decides every hit and score; phones send inputs only (anti-cheat backbone).

## 5. Agawang Base rules
- **Teams:** 5v5. Each team has a **base** (the post) and a **rescue area** at its end of the map.
- **A round ends** when a team **captures the enemy base** or **tags every enemy**. That team scores a round and every kid
  resets at its own base. **No round or match timer:** a round lasts as long as the fight does, and nothing escalates over
  time.
- **Tagged, not killed:** at 0 HP a kid is tagged and waits in the enemy's jail. There is **no automatic release**. A
  tagged kid comes back only by:
  - **Rescue:** a teammate frees them at the team's rescue area; the freed kid reappears **at their base**.
  - **Revenge:** when the enemy who tagged them is tagged, they reappear at their base **10 s later**.
- **Rescue works like Valorant's defuse:**
  - one rescuer at a time; moving or attacking cancels it;
  - one teammate per rescue, first tagged first freed, then a **15 s cooldown** before the area can rescue again;
  - a **checkpoint at the halfway line:** once the bar passed it, a cancelled rescue (by anyone on the team) restarts
    from the line, not from zero.
- **Capture:** standing in the enemy base without being interrupted captures it (time in data).

## 6. Maps: 4 at launch
- **Valorant-sized, asymmetric, sides swap at half:** each map is built for different fights and outcomes, the two
  halves differ, and teams swap sides at the halfway round (Blitz after round 2, Quick after round 4, Standard after
  round 6). Each map has its base, rescue area and jail **beside each base**.
- **Map choice:** before kid select, players vote between **2 maps** drawn from the pool; private rooms let the host pick.
- **One fixed time and weather per map**, bright and sunny like Valorant's Bind and Breeze, so visibility is fair.

| Map | Setting | Plays like |
|---|---|---|
| **Barangay** | Circular barangay: cemented ends, the centre fading to soil and a provincial river, riverside homes of scrap wood and tin shown with dignity and warmth (people live there; never a joke); main path, narrow side streets, a middle | Balanced: a contested river centre, flanks through the side streets |
| **Palengke** | City wet market: stalls, tarps, crates | Close range, tight corners, ambushes |
| **Pier** | Port: cargo containers, cranes, boats | Long sightlines, sharpshooters and seekers shine |
| **Hacienda** | Province: rice paddies, sugarcane, a bahay na bato | Open fields with tall cover, flanks through the cane |

- **Neutrals on every map, themed to the place** (same roles, stats in data; positions shape each map's strategy):

| Role | Barangay version | Like | Gives |
|---|---|---|---|
| 2 healing vendors | Taho and fishball vendors, fixed in the side streets | Wild Rift honeyfruit | Food heals but slows the eater briefly |
| Roaming buff | Selecta ice-cream bike on a loop | Scuttle crab | Small instant heal and a speed boost |
| Dragon | **Crocodile** in the river | Dragon | The whole team gets a slight damage increase |
| Baron | **Philippine Eagle** in the middle | Baron / Lord | By role: Strikers slightly more damage, Defenders take slightly less damage, Seekers slightly faster, Controllers slightly shorter cooldowns |

- The dragon and baron spawn on **fixed timers each round** and respawn a set time after they are beaten; their buffs last
  a set time and end with the round. The other three maps' themed versions are designed with each map.

## 7. Kids and combat
- **12 kids in 4 roles:**
  - Strikers (duelists): Narda, Jumong, Inggo
  - Defenders (guard the base and teammates): Jopay, Oriang, Paeng
  - Seekers (find enemies and their traps): Kikay, Karding, Manoy
  - Controllers (crowd control and traps): Tere, Pamela, Domeng
- Every kid is written with respect: identity shows through the clothes and style a kid would choose, never through
  jokes, slurs or exaggerated features; every kid gets the same animation quality.
- **Heavy lore:** grounded **barangay rivalry**: kids aged **8 to 12** from rival puroks and barangays settle disputes
  through street-game showdowns every summer; the skills are their imagination made real on screen. Every kid has a deep
  backstory (family, barangay, friends, rivals) shown in a **codex per kid** (chapters unlock with mastery) and in
  **in-match voice banter** between allies and rivals. Agents draft the text in docs, keeping the respect rule; the owner
  edits and approves.
- **Basic attack for all:** the slingshot (*Tirador*), 7 shots and a reload button; holding the attack button aims farther,
  up to a maximum range.
- **3 fixed skills per kid: S1, S2 and an ultimate**, all on cooldowns; S1 and S2 also cost **water** (Kalyeah's mana).
  The approved kits are in `docs/KITS.md`. Each skill is a reimagined Filipino street game (bato bato pik, tumbang preso, jackstone, trumpo,
  langit lupa, holens, sili tubig, sipa, luksong baka / tinik / lubid, saranggola, kariling, teks at pogs, piko,
  tagu-taguan, bahay-bahayan, syato, yoyo, palosebo, pukpok palayok, sack race, pabitin, chinese garter, sungka, tiyakad).
- **Vision (Valorant):** you see enemies in your line of sight; the minimap shows only enemies a teammate sees or that
  are revealed. Seeker reveals show enemies to the whole team through walls.
- **Hits:** slingshot headshots deal +50%; aim assist only nudges, never snaps to heads. **No friendly fire:** damage and
  crowd control never touch allies.
- **Healing:** slow regeneration after 5 s without taking damage; vendors and skills are the fast heals.
- **Movement (CODM-like):** walk and run; **one jump button** does jump, running jump, vault (small obstacles) and climb
  (high obstacles) by context; **one crouch button** crouches when walking and slides when running (slide jump and back
  slide included).
- **Camera:** T3 Arena style third person; **aim down sight** zooms in, some attacks zoom out.

## 8. Controls and screens
- **4-finger match layout:**
  - left bottom: move stick;
  - left top: camera drag and the **aim-down-sight** button;
  - right bottom: Wild Rift cluster (big slingshot button, 3 skills around it), with **reload** where Wild Rift puts the
    summoner spells (Flash, Heal);
  - right top: jump and crouch/slide.
- **Screens at launch** (familiar from Mobile Legends, Wild Rift, CODM and Valorant): home (one PLAY, online first), kid
  select after queue, kids and skins collection, store, battle pass and tasks, profile with rank and match history,
  friends and party, settings.
- **Only real content:** a screen, tab or button exists only when the game has the thing behind it; no filler
  components copied from other games.
- **English only** for text; kid voice lines are short **Taglish** barks on skills, tags, rescues and wins.
- **Home kid:** the player's **favourite kid** (pinned in the collection) stands in 3D on the home screen.
- **Accessibility:** colour-blind enemy highlight, visual markers for footsteps and skill sounds, reduce motion and flashes.
- **Settings at launch:** graphics presets and an FPS cap (30 / 60 / 90 / 120 where the phone allows), a control layout
  editor (move and resize every HUD button, saved layouts), camera and ADS sensitivity, aim assist on or off.

**First run:** choose an in-game name (IGN), then the age question, then a **tutorial** before the home screen. The
tutorial uses the real barangay map with **barricades closing the other paths**, and teaches move, jump/vault, crouch/slide,
the slingshot and reload, skills, tag, rescue and capture.

**Fair play:**
- **Leavers and AFK:** a bot plays their kid until they reconnect; repeat leavers lose stars in Ranked and get queue
  cooldowns; a team that was left behind loses less.
- **Ranked unlock:** account level 8 (about 15 matches) and at least 4 owned kids (the starters count).

**After a match:** win or loss and the rounds, an MVP per team, each kid's tags, rescues, captures and damage, stars
gained or lost, pass XP and barya earned, and honor and report buttons.

## 9. Progression and store
- **New account:** after the tutorial the player **picks 4 starter kids, one per role**.
- **Daily login:** a 7-day loop of small barya with a bigger day-7 reward; it also counts for the pass's login task.
- **Currencies:** **barya** (earned by playing, tasks and the pass) and **diamonds** (real money). Kids cost barya or
  diamonds, and **every kid is earnable with barya** (a kid costs about two weeks of normal play, or a small
  diamond pack); skins and the paid pass cost diamonds.
- **Skins:** tiers Basic, Elite, Special, Epic, Legend; higher tiers change the model, effects and sounds, **never stats**.
  Sold in the store, given by the pass and events.
- **Other cosmetics:** emotes, sprays (stickers painted on walls in a match), profile banners and borders (rank borders from
  seasons), slingshot skins for any kid.
- **Kid mastery:** each kid gains mastery XP; levels give a badge, a mastery emote and a title, never power.
- **Seasons: 8 weeks.** Each brings a new 50-level pass, a soft rank reset (down a few divisions) and end-of-season
  rewards by rank (a border and barya).
- **No gacha or loot boxes:** everything is bought directly at a shown price.
- **Battle pass, 50 levels**, filled by fixed daily and weekly tasks (play 3 games, log in, share to Facebook, …; the share
  counts when the share sheet completes). Task rewards are kept small so 50 takes the season.
  - **Free tier:** barya at checkpoints, some levels with no reward; level 50 gives a basic skin.
  - **Paid tier:** small rewards on every level; level 50 gives a high-tier skin, plus that kid if not owned.
  - The paid tier uses a test unlock until Play Billing is added.

## 9b. Launch and data
- **Launch:** closed beta (Play testing track, invites), then a Philippines-only soft launch to tune servers and the
  economy, then Southeast Asia.
- **Data:** match stats on `Kalyeah.Server` for balance, plus a crash reporter. No ad tracking; a privacy policy and
  kid-safe defaults.

## 10. Look
Pure Valorant: flat, confident shapes, the Ink / Paper / Red palette, painted 3D kids and street. Inspired by, never copied
(no Riot, Supercell, Valve or Moonton assets, names or logos). The owner makes all art and audio; agents only say which
kind is needed (3D model, sprite sheet or 2D image).

**Graphics:** the top preset aims at Valorant Mobile: clean stylised PBR, real-time sun shadows, soft ambient light,
bloom on effects. Lower presets drop cost, never the art or the readability:

| Preset | FPS cap | Render scale | Shadows | Effects | Post (bloom, grading) |
|---|---|---|---|---|---|
| Low | 30 | 70% | Blob shadows only | Reduced | Off |
| Medium | 60 | 85% | Hard, near the camera | Normal | Off |
| High | 60 | 100% | Soft | Full | On |
| Ultra | 60 / 90 / 120 (phone limits) | 100% + 4× MSAA | Soft, longer distance | Full | On |

- The first launch picks a preset from a short device check; players can change it.
- **Readability is the same on every preset:** enemies wear a **Valorant-style highlight** (red, yellow or purple, a
  setting); allies stay plain; overhead HP bars and team-coloured effects.

**Audio:** OPM-flavoured electronic and hip-hop music with Filipino instruments (kulintang, bandurria, tambol) and street
sounds: a menu theme, ambience per map, round-end stingers. **Positional sound is gameplay (Valorant level):** footsteps
by surface (cement, soil, wood, tin), directional skill cues; crouch-walking is silent.

**Live events:** limited-time events on the Filipino calendar (Pasko, fiesta, Undas, summer) with themed tasks, event
skins and map decorations; an event mini-mode can come later.

## 11. Open questions (agents use the default until the owner decides)
| # | Question | Default |
|---|---|---|
| Q1 | Skill numbers (damage, water, cooldowns, ranges) | Draft values in `docs/KITS.md`, tuned by bot-vs-bot runs; kariling saved for a later kid |
| Q2 | Stars per division, divisions per tier | 3 divisions × 4 stars |
| Q3 | Kid prices (barya / diamonds) and pass price | Set with the store build |
| Q5 | Kid heights and showcase meshes | After the Narda retopology pilot (`docs/art-briefs/kids-retopo-spec.md`) |
| Q6 | Rescue and capture times, vendor and objective numbers, Crocodile and Eagle timers | Rescue 6 s with the line at 3 s; capture 8 s; Crocodile at 1:00 (respawn 2:00), Eagle at 3:00 (respawn 3:00), buffs 90 s; role buffs +8% |
| Q7 | The 4 maps' layouts, the themed neutrals of Palengke, Pier and Hacienda | Designed map by map, Barangay first |

## 12. Built today vs this spec
The current build still has: 10 placeholder kids (not the 12), the old superpower loadout instead of 3 fixed skills, LP
ranks, no accounts, no store or pass, rooms without matchmaking, round timers and timed releases, a stick-and-arc HUD, `arm64-v8a` only, and an exact 12-permission manifest. `docs/ROADMAP.md` orders the work to close each gap.
