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

## 4. Modes
| Mode | Win | Where |
|---|---|---|
| **Blitz** | First to 3 rounds | Quick Match queue, private rooms, Hotspot, vs AI |
| **Quick** | First to 5 rounds | Quick Match queue, private rooms, Hotspot, vs AI |
| **Standard** | First to 7 rounds | **Ranked** (the only ranked mode), private rooms |
| Practice vs AI, Practice solo | Any mode's rules | Offline; bots at easy / medium / hard; training sandbox |
| Explore Barangay | None | Offline; roam the map alone; one daily collectible coin; no mini-games |
| Tumbang Preso, Patintero | Own rules | Side modes; hidden now, return before launch |

**Online first (Mobile Legends way):** PLAY tries the server; if it cannot connect, a prompt offers offline play, which opens
the offline hub (Hotspot, Practice vs AI, Practice solo, Explore Barangay). **Kids are picked after the queue** (Valorant
agent select): an owned kid within a time limit, no duplicates within a team, the other team may share.

**Online:**
- **Quick Match** (Blitz or Quick, unranked) and **Ranked** (Standard) fill 5v5 by skill rating; private rooms (code) stay
  for friends; Hotspot for phones on one local network.
- **Ranked:** Filipino-themed tiers (Kahoy, Bakal, Tanso, Pilak, Ginto, Brilyante, Batang Kalyeah) with divisions and a
  **star system like Mobile Legends** (win +1 star, loss −1 star, a full division promotes); seasonal resets with rewards.
  Replaces the LP system in the current code.
- **Servers:** `Kalyeah.Server` (Docker) on cloud VMs in a Singapore region first; one account and match database.
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

## 6. Map: one circular barangay
- A circular barangay with a **main path** between the bases, **narrow side streets** (the jungle) and a **middle**.
- **The two ends are cemented** (each team's side of town); toward the centre the ground fades to **soil and a provincial
  river**: the poorest part of the barangay, riverside homes of scrap wood and tin, shown with dignity and warmth (people
  live there; it is never played as a joke).
- **Neutral vendors and creatures** (stats are data):

| What | Where | Like | Gives |
|---|---|---|---|
| Taho vendor, fishball vendor | Fixed spots in the narrow streets | Wild Rift honeyfruit | Food heals but slows the eater briefly |
| Selecta ice-cream bike | Rides a set loop through the narrow streets | Scuttle crab | Small instant heal and a speed boost |
| **Crocodile** | The river | Dragon | The whole team gets a slight damage increase |
| **Philippine Eagle** | The middle | Baron / Lord | The whole team, by role: Strikers slightly more damage, Defenders take slightly less damage, Seekers slightly faster, Controllers slightly shorter cooldowns |

- The Crocodile and the Eagle spawn on **fixed timers each round** and respawn a set time after they are beaten; their
  buffs last a set time and end with the round.

## 7. Kids and combat
- **12 kids in 4 roles:**
  - Strikers (duelists): Narda, Jumong, Inggo
  - Defenders (guard the base and teammates): Jopay, Oriang, Paeng
  - Seekers (find enemies and their traps): Kikay, Karding, Manoy
  - Controllers (crowd control and traps): Tere, Pamela, Domeng
- Every kid is written with respect: identity shows through the clothes and style a kid would choose, never through
  jokes, slurs or exaggerated features; every kid gets the same animation quality.
- **Basic attack for all:** the slingshot (*Tirador*), 7 shots and a reload button; holding the attack button aims farther,
  up to a maximum range.
- **3 fixed skills per kid: S1, S2 and an ultimate**, all on cooldowns; S1 and S2 also cost **water** (Kalyeah's mana).
  The approved kits are in `docs/KITS.md`. Each skill is a reimagined Filipino street game (bato bato pik, tumbang preso, jackstone, trumpo,
  langit lupa, holens, sili tubig, sipa, luksong baka / tinik / lubid, saranggola, kariling, teks at pogs, piko,
  tagu-taguan, bahay-bahayan, syato, yoyo, palosebo, pukpok palayok, sack race, pabitin, chinese garter, sungka, tiyakad).
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
- **English only** for now.

**First run:** choose an in-game name (IGN), then the age question, then a **tutorial** before the home screen. The
tutorial uses the real barangay map with **barricades closing the other paths**, and teaches move, jump/vault, crouch/slide,
the slingshot and reload, skills, tag, rescue and capture.

**Fair play:**
- **Leavers and AFK:** a bot plays their kid until they reconnect; repeat leavers lose stars in Ranked and get queue
  cooldowns; a team that was left behind loses less.
- **Ranked unlock:** account level 8 (about 15 matches) and at least 4 owned kids (the starters count).

## 9. Progression and store
- **New account:** 4 starter kids, one per role.
- **Currencies:** **barya** (earned by playing, tasks and the pass) and **diamonds** (real money). Kids cost barya or
  diamonds, and **every kid is earnable with barya**; skins and the paid pass cost diamonds.
- **Skins:** tiers Basic, Elite, Special, Epic, Legend; higher tiers change the model, effects and sounds, **never stats**.
  Sold in the store, given by the pass and events.
- **Battle pass, 50 levels**, filled by fixed daily and weekly tasks (play 3 games, log in, share to Facebook, …; the share
  counts when the share sheet completes). Task rewards are kept small so 50 takes the season.
  - **Free tier:** barya at checkpoints, some levels with no reward; level 50 gives a basic skin.
  - **Paid tier:** small rewards on every level; level 50 gives a high-tier skin, plus that kid if not owned.
  - The paid tier uses a test unlock until Play Billing is added.

## 10. Look
Pure Valorant: flat, confident shapes, the Ink / Paper / Red palette, painted 3D kids and street. Inspired by, never copied
(no Riot, Supercell, Valve or Moonton assets, names or logos). The owner makes all art and audio; agents only say which
kind is needed (3D model, sprite sheet or 2D image).

## 11. Open questions (agents use the default until the owner decides)
| # | Question | Default |
|---|---|---|
| Q1 | Skill numbers (damage, water, cooldowns, ranges) | Draft values in `docs/KITS.md`, tuned by bot-vs-bot runs; kariling unused until the owner explains it |
| Q2 | Stars per division, divisions per tier, season length | 3 divisions × 4 stars; 8-week seasons |
| Q3 | Kid prices (barya / diamonds) and pass price | Set with the store build |
| Q4 | Which kid each new account gets per role | Narda, Jopay, Kikay, Tere |
| Q5 | Kid heights and showcase meshes | After the Narda retopology pilot (`docs/art-briefs/kids-retopo-spec.md`) |
| Q6 | Rescue and capture times, vendor and objective numbers, Crocodile and Eagle timers | Rescue 6 s with the line at 3 s; capture 8 s; Crocodile at 1:00 (respawn 2:00), Eagle at 3:00 (respawn 3:00), buffs 90 s; role buffs +8% |

## 12. Built today vs this spec
The current build still has: 10 placeholder kids (not the 12), the old superpower loadout instead of 3 fixed skills, LP
ranks, no accounts, no store or pass, rooms without matchmaking, round timers and timed releases, a stick-and-arc HUD, `arm64-v8a` only, and an exact 12-permission manifest. `docs/ROADMAP.md` orders the work to close each gap.
