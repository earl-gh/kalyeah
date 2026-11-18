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
| Mode | Where | Notes |
|---|---|---|
| **Agawang Base 5v5** | Quick Match, Ranked, private rooms, Hotspot, vs AI | The main mode |
| Practice vs AI | Offline | Bots at easy / medium / hard |
| Practice solo | Offline | Training sandbox |
| Explore Barangay | Offline | Roam the map alone; one daily collectible coin; no mini-games |
| Tumbang Preso, Patintero | Side modes | Hidden now; return before launch |

**Online first (Mobile Legends way):** PLAY tries the server; if it cannot connect, a prompt offers offline play, which opens
the offline hub (Hotspot, Practice vs AI, Practice solo, Explore Barangay).

## 5. Online
- **Quick Match** (unranked) and **Ranked** fill 5v5 by skill rating; private rooms (code) stay for friends; Hotspot for
  phones on one local network.
- **Ranked:** Filipino-themed tiers (Kahoy, Bakal, Tanso, Pilak, Ginto, Brilyante, Batang Kalyeah) with divisions and a
  **star system like Mobile Legends** (win +1 star, loss −1 star, a full division promotes); seasonal resets with rewards.
  Replaces the LP system in the current code.
- **Servers:** `Kalyeah.Server` (Docker) on cloud VMs in a Singapore region first; one account and match database.
- **Server-authoritative:** the server decides every hit and score; phones send inputs only (anti-cheat backbone).

## 6. Kids and combat
- **12 kids in 4 roles:**
  - Strikers (duelists): Narda, Jumong, Inggo
  - Defenders (guard the base and teammates): Jopay, Oriang, Paeng
  - Seekers (find enemies and their traps): Kikay, Karding, Manoy
  - Controllers (crowd control and traps): Tere, Pamela, Domeng
- Every kid is written with respect: identity shows through the clothes and style a kid would choose, never through
  jokes, slurs or exaggerated features; every kid gets the same animation quality.
- **Basic attack for all:** the slingshot (*Tirador*), 7 shots and a reload button; holding the attack button aims farther,
  up to a maximum range.
- **3 fixed skills per kid**, each a reimagined Filipino street game (bato bato pik, tumbang preso, jackstone, trumpo,
  langit lupa, holens, sili tubig, sipa, luksong baka / tinik / lubid, saranggola, kariling, teks at pogs, piko,
  tagu-taguan, bahay-bahayan, syato, yoyo, palosebo, pukpok palayok, sack race, pabitin, chinese garter, sungka, tiyakad).
- **Camera:** T3 Arena style third person; some attacks use **aim down sight** (zoom in) or **zoom out**.

## 7. Progression and store
- **New account:** 4 starter kids, one per role.
- **Currencies:** **barya** (earned by playing, tasks and the pass) and **diamonds** (real money). Kids cost barya or
  diamonds, and **every kid is earnable with barya**; skins and the paid pass cost diamonds.
- **Battle pass, 50 levels**, filled by fixed daily and weekly tasks (play 3 games, log in, share to Facebook, …; the share
  counts when the share sheet completes). Task rewards are kept small so 50 takes the season.
  - **Free tier:** barya at checkpoints, some levels with no reward; level 50 gives a basic skin.
  - **Paid tier:** small rewards on every level; level 50 gives a high-tier skin, plus that kid if not owned.
  - The paid tier uses a test unlock until Play Billing is added.

## 8. Look
Pure Valorant: flat, confident shapes, the Ink / Paper / Red palette, painted 3D kids and street. Inspired by, never copied
(no Riot, Supercell, Valve or Moonton assets, names or logos). The owner makes all art and audio; agents only say which
kind is needed (3D model, sprite sheet or 2D image).

## 9. Open questions (agents use the default until the owner decides)
| # | Question | Default |
|---|---|---|
| Q1 | The 36 skills (3 × 12 kids) and their numbers | Draft in `docs/GDD.md`, owner to approve |
| Q2 | Stars per division, divisions per tier, season length | 3 divisions × 4 stars; 8-week seasons |
| Q3 | Kid prices (barya / diamonds) and pass price | Set with the store build |
| Q4 | Which kid each new account gets per role | Narda, Jopay, Kikay, Tere |
| Q5 | Kid heights and showcase meshes | After the Narda retopology pilot (`docs/art-briefs/kids-retopo-spec.md`) |

## 10. Built today vs this spec
The current build still has: 10 placeholder kids (not the 12), the old superpower loadout instead of 3 fixed skills, LP
ranks, no accounts, no store or pass, rooms without matchmaking, `arm64-v8a` only, and an exact 12-permission manifest. `docs/ROADMAP.md` orders the work to close each gap.
