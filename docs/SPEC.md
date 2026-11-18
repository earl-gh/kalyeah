# Kalyeah spec (source of truth for what we are building)

Owner decisions from the 2026-11-18 spec interview and earlier owner calls. This file says **what** the game is and **why**;
`docs/GDD.md` holds the rules and numbers as built (data in `unity/Data/*.json`), `docs/ART.md` the look, `docs/ROADMAP.md`
the order of work. Where this file and an older doc disagree, this file wins and the older doc is due for a rewrite.

## 1. Vision
A free-to-play 5v5 team shooter-brawler on Android, themed on Filipino street games (*mga larong Pinoy sa kalye*), built as
a full commercial game for the Play Store (the earlier game-dev competition entry is dropped, and its limits with it).

- **Feel:** T3 Arena camera and pace, Valorant clarity and UI/world art (the kids keep the owner's models), a Wild Rift / Mobile Legends shell (home, store, pass, ranked).
- **Fair:** every kid has 100 HP, the same speed and hitbox; kids differ **only by their 3 skills**. Money buys kids early,
  skins and the paid pass, never stats.

## 2. Platforms and devices
- **Android first**, iOS after launch: game code stays platform-neutral (platform code behind small adapters).
- **No device floor on purpose:** install on as many phones as the engine allows (Android 8.0, API 26, is Unity 6.3's
  lowest and is already used; add `armeabi-v7a` next to `arm64-v8a` for older 32-bit phones); Low, Medium, High and Ultra presets (§10). Performance on old phones is
  improved after launch; 60 fps on a mid-range phone is the target, not a gate.
- **Size:** a Play Store base (AAB) under 200 MB; maps, skins and voices in Play Asset Delivery packs (no total cap).
- **Permissions:** only what a shipped feature needs (voice chat needs the microphone; hotspot needs the current network set).

## 3. Accounts, age and safety
- **In-game name:** one free change, then a small diamond price, at most once every 30 days; names are filtered.
- **Sign-in:** guest play first; **Google sign-in first**, then Facebook and Apple. Progress, kids, skins, pass and rank live
  on the `Kalyeah.Server` account; a guest account can be linked later. Guests get a reminder to link after 3 matches
  (at most once a week), a small barya gift for linking, and must link before buying diamonds.
- **All ages, with an age gate:** the player's birth year is asked once.
  - **Under 13 (kid mode):** guest or Google family account, quick chat, emotes and pings only, purchases need parent approval
    (Google Family Link), following Google Play's Families policy. Friends only by friend code, approved by a parent on
    Family Link (no requests from strangers); they can then party up. No Facebook sharing (the pass share task becomes
    "play 1 match").
  - **13 and up:** every feature.
- **Data rights:** Settings has **Delete account** (7-day grace, then wiped) and **Download my data**; parents can ask
  to delete a kid's account.
- **Pings and quick chat:** tap to ping what you are looking at (enemy, vendor, neutral, jail); hold for a wheel
  ("Rescue me", "Going in", "Defend base", "Wait"). Quick chat lines are spoken in your kid's voice in Taglish with English
  text.
- **Social:** quick chat, emotes, pings, text chat with a profanity filter, voice chat, report and mute.
  - **Text chat:** team chat by default; all-chat (both teams) is opt-in; the profanity filter is always on under 18 and on
    by default for adults.
  - **Voice:** team only, mic off until the player turns it on; party voice in the lobby; never in kid mode.
  - **Reports** (cheating, AFK, abuse): repeated reports trigger automatic mutes and AFK penalties; serious cases go to a
    review queue on the server for the owner.
  - **Credit score:** starts at 100; leaving, AFK and confirmed abuse lower it, good matches raise it; below thresholds a
    player loses Ranked, then text and voice chat.
- **Friends and party:** parties of 1 to 5 for Quick Match and private rooms; Ranked allows 1, 2, 3 or 5 (no 4-stacks),
  within a rank range. No clans at launch (a *barkada* system may follow).

## 4. Modes
| Mode | Win | Where |
|---|---|---|
| **Blitz** | First to 3 rounds | Quick Match queue, private rooms, Hotspot, vs AI |
| **Quick** | First to 5 rounds | Quick Match queue, private rooms, Hotspot, vs AI |
| **Standard** | First to 7 rounds | **Ranked** (the only ranked mode), private rooms |
| Practice vs AI, Practice solo | Any mode's rules | Offline; bots at easy / medium / hard; Practice solo is a training range with dummies, distance markers and a no-cooldown toggle |
| Explore | None | Offline; roam any of the 4 maps alone: one daily coin per map, hidden teks cards (a full set per map gives a spray), route practice (ghost paths of rescue routes and climb spots, with a personal-best timer only: no score, leaderboard or rewards), try any kid and skin, map callouts, photo mode; no mini-games |
| Tumbang Preso, Patintero | Own rules | Side modes; hidden now, return before launch |

**Bots** (Practice vs AI and leaver takeover) are role-aware: Strikers dive, Defenders guard and rescue, Seekers
scout, Controllers zone; they use skills, vendors and objectives (rescue, capture, flank, take neutrals). Easy, medium
and hard change aim and reaction time; easy bots pause before shooting and say their plans out loud, to teach. Bot names
are funny Filipino-style IGN puns (like "EddieWow", from "Edi wow!"), never a real person's full name.

**Online first (Mobile Legends way):** PLAY tries the server; if it cannot connect, a prompt offers offline play, which opens
the offline hub (Hotspot, Practice vs AI, Practice solo, Explore). **Kids are picked after the queue** (Valorant
agent select): 30 s, everyone at once; hovering shows teammates your choice and LOCK confirms; an unlocked player gets a
random owned kid of a missing role at 0 s. No duplicates within a team (the other team may share). The enemy team's picks stay hidden until the loading
screen (no counter-picking). Any role mix is
allowed, with a warning when a role is missing. After locking: skin, slingshot skin, spray and emote wheel. A player who
leaves during select cancels the match (the others go back to the front of the queue); the dodger gets a queue cooldown
and, in Ranked, loses a star.

**Online:**
- **Quick Match** (Blitz or Quick, unranked) and **Ranked** (Standard) fill 5v5 by skill rating; private rooms (code) stay
  for friends; Hotspot for phones on one local network.
- **Ranked:** Filipino-themed tiers (Kahoy, Bakal, Tanso, Pilak, Ginto, Brilyante, Batang Kalyeah) with divisions and a
  **star system like Mobile Legends** (win +1 star, loss −1 star, a full division promotes); seasonal resets with rewards.
  - Each tier has 3 divisions; stars per division grow with the tier: Kahoy and Bakal 3, Tanso and Pilak 4, Ginto and
    Brilyante 5. Batang Kalyeah has no divisions (stars keep counting).
  Replaces the LP system in the current code.
  - No placement matches: everyone starts at Kahoy; a hidden rating (MMR) still matches by skill.
  - Star protection: the losing team's MVP keeps the star; each loss fills protection points by your match score (tags,
    rescues, captures, support), and a full bar saves one star automatically; no demotion below a newly reached tier for
    3 games.
  - Win streaks: below Ginto, every 3rd win in a row gives +1 bonus star.
  - Top tier: stars keep counting with no divisions, a Philippine leaderboard and top-100 titles.
  - Ranked parties must be within one tier of each other.
- **Servers:** `Kalyeah.Server` (Docker) on cloud VMs in a Singapore region first; one account and match database.
- **Private rooms:** the host picks the mode and map, fills empty slots with bots at a chosen level, can set custom rules
  (neutrals on or off, rescue time, duplicate kids allowed) and allow up to 2 spectators.
- **Hotspot:** one phone hosts up to 10 phones over its hotspot or the same Wi-Fi; bots fill empty slots; the host needs
  a mid-range or better phone. Every kid is unlocked in Hotspot so friends can play anything together.
- **Anti-cheat:** besides the server deciding hits, it validates movement speed and fire rate; Ranked requires a passing
  Play Integrity check (rooted or modded phones can still play casual modes).
- **Bad connections:** ping and packet loss on the HUD; a dropped player can reconnect within 3 minutes while a bot plays
  their kid; the queue prefers players under 120 ms. A phone call or app switch counts as a disconnect: a bot covers,
  with no penalty if the player is back within 3 minutes.
- **Long queues:** Quick Match and Ranked keep searching for real players and never start with bots, except in a new
  account's first 10 matches (below).
- **Spectating and replays:** not at launch.
- **Server-authoritative:** the server decides every hit and score; phones send inputs only (anti-cheat backbone).

## 5. Agawang Base rules
- **Teams:** 5v5. Each team has a **base** (the post) and, beside it, a **jail** where the kids it tags wait.
- **A round ends** when a team **captures the enemy base** or **tags every enemy**. That team scores a round and every kid
  resets at its own base. **No round or match timer:** a round lasts as long as the fight does, and nothing escalates over
  time. Design target: a first-to-7 match lasts about 15 to 20 minutes (rounds of 1 to 2 minutes), tuned with map size
  and rescue time.
- **Round flow:** each round starts with a **5 s barrier phase** at base (walk inside the base, ping, draw arrows on the
  full map), then the barrier drops. Water,
  S1 and S2 are full each round; **ultimate cooldowns carry over** between rounds (they reset only at the side swap).
  The dragon and baron timers restart every round. After the final round, a 6 s victory scene shows the winners posing
  (skins, emotes) with the MVP in front, then the results screen.
- **Tagged, not killed:** at 0 HP a kid is tagged and waits in the enemy's jail. There is **no automatic release**. A
  tagged kid comes back only by:
  - **Rescue:** a teammate reaches the **enemy jail** (beside the enemy base) and frees them there, like the real street
    game; the freed kid reappears **at their own base**.
  - **Revenge:** when the enemy who tagged them is tagged, they reappear at their base **10 s later** with a 2 s shield that blocks
    all damage, and the feed shows "Revenge!".
- **Rescue works like Valorant's defuse:** it takes **6 s**;
  - one rescuer at a time; moving or attacking (shooting, or a skill that moves or deals damage) cancels it; a skill that
    does neither, like a self-shield, is allowed; enemies standing near the jail don't stop it, they must hit or
    stop the rescuer;
  - one teammate per rescue, first tagged first freed, then a **15 s cooldown** before that jail can be used for a rescue again;
  - a **checkpoint at the halfway line (3 s):** once the bar passed it, a cancelled rescue (by anyone on the team) restarts
    from the line, not from zero.
- **In jail:** a tagged kid stands visibly in the jail, watches a teammate's view (switchable), and can ping and chat. Tapping a free teammate's portrait **cheers** them (they
  hear your kid's voice line). The tagged kid's team always sees a marker on the enemy jail with how many are inside.
- **Last free kid:** no help or penalty; a lone kid can still win the round by capturing or rescuing (a clutch).
- **Capture:** standing in the enemy base fills a capture bar in **8 s** for one kid; each extra teammate inside adds 50%
  speed. A free defender
  standing in the base **pauses** it; stepping out keeps the progress but it **drains slowly** (a full bar empties in 16 s, half the fill speed).
- **Giving up:** from the side swap on, a team can vote to give up; 4 of 5 must agree (every teammate still in, if one
  left). A team down a player for good can vote before the side swap too.
- **Ultimates that stop a capture or a tag** (Palosebo, Langit!) have no extra limit: both teams see the area clearly and
  hear the ultimate's voice line.
- **Same-moment finish:** if both teams win a round in the same tick (two captures, or each tags the other's last kid),
  nobody scores and the round is replayed.
- **Interrupts:** being tagged or stunned stops a rescue or a capture; a rescue keeps its checkpoint, a capture drains.

## 6. Maps: 4 at launch
- **Valorant-sized, asymmetric, sides swap at half:** each map is built for different fights and outcomes, the two
  halves differ, and teams swap sides at the halfway round (Blitz after round 2, Quick after round 4, Standard after
  round 6). Each base has its team's jail beside it; rescues happen at the enemy's jail.
- **Map choice:** before kid select, players vote between **2 maps** drawn from the pool; private rooms let the host pick.
- **One fixed time and weather per map**, bright and sunny like Valorant's Bind and Breeze, so visibility is fair.

| Map | Halves (asymmetric) | Routes (3 each) | Plays like |
|---|---|---|---|
| **Barangay** | **Uptown:** cemented side with the barangay hall, plaza and basketball court (open, long lines). **Riverside:** market and chapel side, tight streets, a sari-sari row (close range). The centre fades to soil and a provincial river with homes of scrap wood and tin, shown with dignity and warmth (people live there; never a joke) | Main path over the river bridge; left alley through the riverside homes; right route along the creek bank; side connectors | Balanced: a contested river centre, flanks through the alleys |
| **Palengke** | **Wet section:** fish and meat stalls, slippery floors, tight aisles. **Dry section:** dry goods and ukay-ukay racks (soft cover) | Main aisle; loading-bay road; rooftop catwalk (climb) | Close range, ambushes, tight corners |
| **Pier** | **Cargo yard:** stacked containers and a crane (long lanes, high ground). **Ferry terminal:** fishing boats, piers over water, waiting shed | Dock road; container maze; boardwalk over the water | Long sightlines; sharpshooters and seekers shine |
| **Hacienda** | **Rice terraces:** paddies, dikes, a nipa hut (open, low cover; water slows). **Sugarcane mill:** tall cane that hides kids, an old mill and a bahay na bato | Dirt road; paddy dikes; through the cane | Open fields with tall cover; flanks through the cane |

- **Terrain (light, data-driven):** river and paddy water slow by 15% and make footsteps loud; wet market floors make
  slides longer. Water is wade-able only; deep water (the Pier sea) is a wall, and falling in puts you back on the dock
  with a small HP loss.
- **Hiding:** inside tall cover (cane, laundry lines) a kid is invisible beyond 6 m until they attack or are revealed.
  Starting a rescue or standing in the enemy base to capture also ends any hiding or invisibility (tall cover, Tago!).
- **Side swap at half:** teams trade halves, rounds won stay, water and ultimate cooldowns reset, a 10 s swap screen
  shows the score.
- **Height:** some climbable spots per map (low roofs, water tanks, jeepney and container tops) give a few high-ground
  angles with clear counters; not every roof.

- **Neutrals on every map, themed to the place** (same roles, stats in data; positions shape each map's strategy):

| Role | Like | Gives | Barangay | Palengke | Pier | Hacienda |
|---|---|---|---|---|---|---|
| 2 healing vendors (fixed) | Wild Rift honeyfruit | Food heals but slows the eater briefly | Taho, fishball | Buko juice, banana cue | Balut, inihaw | Halo-halo, kakanin |
| Roaming buff | Scuttle crab | Small instant heal and a speed boost | Selecta ice-cream bike | Escaped lechon pig | Seagull flock | Native chicken |
| Dragon | Dragon | The whole team gets a slight damage increase | Crocodile (river) | Giant bangus (fish tank) | Pawikan sea turtle | Carabao |
| Baron | Baron / Lord | By role: Strikers more damage, Defenders take less damage, Seekers faster, Controllers shorter cooldowns (all slight) | Philippine Eagle | Boss Aspin (giant street dog) | Butanding whale shark | Tamaraw |

- **Placement is different on every map** and chosen for that map's strategy (agents design it with each layout).
  Layouts: [`docs/MAPS.md`](MAPS.md).
- The dragon and baron spawn on **fixed timers each round** and respawn a set time after they are beaten; their buffs last
  a set time and end with the round.
- **Fighting neutrals:** the team that lands the last hit gets the buff (steals are possible). The dragon and baron hit
  back lightly at whoever attacks them and have about 600 HP (a team takes about 10 s).
- **Neutral numbers (Barangay names; every map uses the same):**
  - **Vendors:** 40 HP over 2 s while the eater walks 30% slower; one kid per serving, restocks in 30 s.
  - **Roaming buff (Selecta bike):** appears at 0:30 and 60 s after it is caught; the catcher gets 20 HP at once and +20%
    speed for 10 s.
  - **Dragon (Crocodile):** appears at 1:00, back 2:00 after it is beaten; the whole team deals +8% damage for 90 s.
  - **Baron (Philippine Eagle):** appears at 3:00, back 3:00 after it is beaten; role buffs of 8% for 90 s.
- **Two equal ways to win a round:** capture or full tag, one round either way, so each round the team picks a plan.

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
  edits and approves. Draft: [`docs/LORE.md`](LORE.md) (4 puroks of 3 kids, friends and rivals,
  playful never-mean Taglish banter, a personality, idle and victory pose per kid).
- **Basic attack for all:** the slingshot (*Tirador*), 7 shots and a reload button (it also reloads by itself when
  empty); full accuracy while jumping, vaulting or sliding; holding the attack button aims farther,
  up to a maximum range.
- **3 fixed skills per kid: S1, S2 and an ultimate**, all on cooldowns; S1 and S2 also cost **water** (Kalyeah's mana).
  The approved kits are in `docs/KITS.md`. Each skill is a reimagined Filipino street game (bato bato pik, tumbang preso, jackstone, trumpo,
  langit lupa, holens, sili tubig, sipa, luksong baka / tinik / lubid, saranggola, kariling, teks at pogs, piko,
  tagu-taguan, bahay-bahayan, syato, yoyo, palosebo, pukpok palayok, sack race, pabitin, chinese garter, sungka, tiyakad).
- **Vision (Valorant):** you see enemies in your line of sight; the minimap shows only enemies a teammate sees or that
  are revealed. Seeker reveals show enemies to the whole team; what each reveal shows depends on the skill, like Valorant
  (outline through walls or a one-time snapshot, see `docs/KITS.md`).
- **Slingshot shots** are fast visible stones with a slight arc and travel time (leading moving targets matters a little);
  holding the button only adds range, never damage; ADS tightens accuracy. A stone deals **20** (body) or **30** (head):
  5 body hits or 4 with a headshot tag a kid, so a full load of 7 leaves room for 2 misses. One stone every **0.4 s**,
  reload **1.5 s**; range **18 m** on a tap, up to **30 m** when held 1 s, and a stone drops past its range. The server checks hits with lag compensation.
- **Skill aiming (Wild Rift):** tap to quick-cast at the crosshair or nearest target; drag from the button to aim with a
  ground or line indicator; drag to the cancel zone to cancel.
- **Hits:** slingshot headshots deal +50%. **Aim assist** (a setting) only slows the crosshair slightly over enemies; no
  pull, no snapping, no auto-fire. **No friendly fire:** damage and crowd control never touch allies.
- **Chained control:** after a hard stop (stun, root, knock-up, knock-down, turned into a can) a kid can't be
  hard-stopped again for 1 s; slows still apply.
- **Healing:** **5 HP a second** once a kid has gone 5 s without taking damage, attacking or being under crowd control; vendors and skills are the fast heals.
- **Movement (CODM-like):** walk **4.5 m/s** and run **6 m/s** (run when the stick is pushed fully); **one jump button** does jump, running jump, vault (small obstacles) and climb
  (high obstacles) by context; **one crouch button** crouches when walking and slides when running (slide jump and back
  slide included). Crouch-walking is silent and 40% slower. A slide is a 3 m burst that drops the head below most shots
  for 0.3 s, then a 2 s cooldown. A vault takes 0.4 s and a climb about 1 s; a hit while climbing knocks the kid down.
- **Aim down sight:** shots land exactly at the crosshair (hip-fire spreads a little) and the kid walks 30% slower; a
  setting picks tap-to-toggle or hold.
- **Camera:** T3 Arena style, **centred** behind the kid at mid distance (about 4 m behind, raised so the crosshair sits
  above the kid's head); no shoulder offset and no shoulder swap; **aim down sight** moves in close, some attacks zoom out. FOV slider in a fair range (70 to 90)
  that is the same for everyone.
- **Feel:** crisp and readable like Valorant: hit markers, a headshot sound, a small kick on firing, a brief flash on the
  target, no screen-filling effects.
- **Low HP (under 30):** a soft red edge on your screen (softer with reduce flashes), and your portrait at the top shows
  "low" to your team.
- **Emotes:** a wheel of 6, usable anytime: during a fight a short version plays (you can move to cancel it); the full
  one plays after a tag, a rescue or at round end. A setting mutes enemy emotes and their sounds.
- **Being tagged:** no death; the kid freezes in a pouty "Taya!" pose in a puff of chalk dust, then poofs to the jail.

## 8. Controls and screens
- **4-finger match layout:**
  - left bottom: move stick;
  - left top: camera drag and the **aim-down-sight** button;
  - right bottom: Wild Rift cluster (big slingshot button, 3 skills around it), with **reload** where Wild Rift puts the
    summoner spells (Flash, Heal);
  - right top: jump and crouch/slide.
  - Holding the slingshot button and sliding the thumb turns the camera while shooting (like CODM and Wild Rift).
  - Ready-made layouts: this **default 4-finger** one and a **2-thumb simple** one with bigger buttons for new and casual
    players (it keeps a small ADS button next to fire); the layout editor saves custom ones.
- **HUD read-outs:**
  - top-left corner: a small minimap (tap for the full map) with the dragon and baron on their spots, each showing a
    countdown until it appears (or that it is up); the camera and ADS zone below it;
  - top centre: rounds won per team and each team's 5 kid portraits (greyed when tagged, with the jail count);
  - right edge: a small feed of tags and rescues with the skill used (a rescue appears only once it completes); big centre banners only for team events (capture,
    all tagged, dragon or baron taken, side swap);
  - bottom centre between the thumbs: your HP and water; the ammo count (of 7) on the slingshot button; overhead bars on
    every kid.
- **Screens at launch** (familiar from Mobile Legends, Wild Rift, CODM and Valorant): home (one PLAY, online first), kid
  select after queue, kids and skins collection, store, battle pass and tasks, profile with rank and match history,
  friends and party, settings.
- **Kid page (collection):** the kid in 3D to rotate, every skin to try and the victory pose; a clip of each skill with a
  combo tip; purok, friends, rivals and unlocked codex chapters; mastery level, win rate and recent balance changes.
- **Home:** the favourite kid in 3D; one big PLAY with the mode name above it (tap to switch Blitz, Quick, Ranked or
  offline); party slots beside the kid with an online-friends list; a small card with pass level and today's tasks. No
  event or store banners.
- **Kid select:** the 12 kids in 4 role rows (not-owned greyed, trial-card kids marked); a team bar with hovers, locks
  and a missing-role warning; tapping a kid previews its 3 skills with short clips.
- **Loading:** every player's kid in their skin, with name, rank and honor badge, and a tip about the map.
- **Results actions:** add any player from the match as a friend (not in kid mode) and share a result card to Facebook
  (counts for the share task).
- **Only real content:** a screen, tab or button exists only when the game has the thing behind it; no filler
  components copied from other games.
- **English only** for text; kid voice lines are short **Taglish** barks on skills, tags, rescues and wins.
- **Home kid:** the player's **favourite kid** (pinned in the collection) stands in 3D on the home screen.
- **Accessibility:** colour-blind enemy highlight, visual markers for footsteps and skill sounds, reduce motion and flashes.
- **Settings at launch:** graphics presets and an FPS cap (30 / 60 / 90 / 120 where the phone allows), a control layout
  editor (move and resize every HUD button, saved layouts), camera and ADS sensitivity, aim assist on or off.

**First run:** choose an in-game name (IGN), then the age question, then a **tutorial** before the home screen. The
tutorial uses the real barangay map with **barricades closing the other paths**, and teaches move, jump/vault, crouch/slide,
the slingshot and reload, skills, tag, rescue and capture. Each step lends a kid of a different role, so the player tries
all 4 roles before picking starters. A friendly older neighbour, **Ate Lorna** (the sari-sari store owner every kid buys snacks from; warm, teasing, a little
strict; not playable), guides it in Taglish voice with
English text and later narrates the codex. The tutorial can be replayed from Settings.

**Tutorial steps** (about 6 to 8 minutes, like Valorant's basic training):
1. **Narda (Striker):** move, jump, vault, slide, the slingshot and reload.
2. **Kikay (Seeker):** the 3 skills and finding a hidden bot.
3. **Jopay (Defender):** get tagged, a bot rescues you, then you rescue it.
4. **Tere (Controller):** slow the defenders and capture their base.

**After the tutorial:** pick 4 starter kids (one per role), then a Blitz match against easy bots with them, then the home
screen. A 7-day **new-player task list** (a match of each mode, rescue 3 teammates, try every role) pays barya and a
trial card.

**Easing into online play:** for the first 10 matches Quick Match puts new accounts together; every 90 s of waiting it offers to start now with bots,
showing how many slots bots would fill (the player can keep waiting);
loading screens show a tip about the map and your kid; the first 3 times you play a kid, a card shows its 3 skills and
a combo idea.

**Fair play:**
- **Leavers and AFK:** a bot plays their kid until they reconnect; repeat leavers lose stars in Ranked and get queue
  cooldowns; a team that was left behind loses less (in Ranked the players who stayed keep their star on a loss). The
  bot that takes over plays like a medium bot, stays with the team and helps rescues; the player takes control back the
  moment they reconnect.
- **Ranked unlock:** account level 8 (about 15 matches) and at least 4 owned kids (the starters count).

**After a match:** win or loss and the rounds, an MVP per team (scored from tags and assists, rescues and captures, and
support work that led to a tag: heals, shields, reveals, slows and roots; raw damage does not count, so every role can
be MVP), each kid's tags, rescues, captures and damage, stars
gained or lost, pass XP and barya earned, and honor and report buttons.
- **Profile stats:** win rate per mode and per kid, the last 20 matches (result, kid, tags, rescues, captures, MVP marks),
  a favourite-role chart and the highest rank of each past season.
- **Honor:** one honor per match for any player (teammate or rival); being honored raises your credit score a little,
  and your honor level shows on your profile and in kid select.

## 9. Progression and store
- **New account:** after the tutorial the player **picks 4 starter kids, one per role**.
- **Daily login:** a 7-day loop of small barya with a bigger day-7 reward; it also counts for the pass's login task.
- **Getting kids:** buy with barya or diamonds; **trial cards** (from tasks and the pass) lend a kid for 1 to 3 days; the
  paid pass's last level and some events give a kid. No free weekly rotation.
- **Currencies:** **barya** (earned by playing, tasks and the pass) and **diamonds** (real money). Kids cost barya or
  diamonds, and **every kid is earnable with barya** (a kid costs 6,000 barya, about two weeks of normal play, or diamonds worth about
  ₱150); skins and the paid pass cost diamonds.
- **Skins:** tiers Basic, Elite, Special, Epic, Legend at diamonds worth about ₱50, ₱150, ₱300, ₱500 and ₱900 (some
  Basic skins also for barya); higher tiers change the model, effects and sounds, **never stats**.
  Sold in the store, given by the pass and events.
- **Other cosmetics:** emotes, sprays (one sticker per round painted on a wall; your team sees it, and a setting hides other players' sprays), profile banners and borders (rank borders from
  seasons), slingshot skins for any kid.
- **Kid mastery:** each kid gains mastery XP over 7 levels (about 50 matches to max): a badge at level 3, an emote at 5,
  a title at 7, with codex chapters along the way; never power.
- **Seasons: 8 weeks.** Each brings a new 50-level pass, a soft rank reset (down one tier, same division: Ginto II becomes Pilak II; Kahoy and Bakal
  don't drop) and end-of-season
  rewards by rank (a border and barya; Ginto or higher also gets an Elite skin for that season's featured kid).
- **Barya from matches** (loss / win): Blitz 60 / 100, Quick 90 / 150, Standard 120 / 200, plus 300 for the first win of
  the day, capped at 3,000 a week, so a 6,000-barya kid takes about two weeks.
- **Offline rewards:** vs AI gives a little barya and pass XP (capped daily); Hotspot and Explore rewards sync to the
  account when online. Offline results can't be verified by the server, so their caps stay small.
- **Account level:** earned by playing; unlocks Ranked (level 8), sprays and emote slots, with a little barya per level;
  a prestige border after level 30.
- **Diamonds:** 6 packs at ₱20, ₱50, ₱100, ₱250, ₱500 and ₱2,000, with small bonuses on bigger packs and double diamonds
  on each pack's first buy, plus a ₱50 weekly diamond card; no limited-time pressure offers in kid mode.
- **Bundles:** a once-per-account starter bundle (a kid, an Elite skin and diamonds at a very low price); kid + launch
  skin bundles at a discount; a season set (all the season's skins with a spray and a border, cheaper than one by one).
- **No free starter skins:** kids start in their default look.
- **Skin themes:** season 1 is **summer fiesta** (banderitas, parade costumes, higante masks, sorbetes colours; Narda headlines
  it with a fiesta parade costume as the pass's level-50 skin), matching
  the lore's summer showdowns. Lines that return: **Filipino holidays** (Pasko, Undas, fiesta, summer, yearly with the
  events) and **dream jobs** (what each kid wants to be, tied to their lore).
- **Store tabs:** Featured (the season's skins and bundles), Kids, Skins, Daily (3 items you don't own at a small discount, never kids, plus one free small gift; resets at midnight PH time), Diamonds; every item
  can be previewed on the 3D kid before buying.
- **No gacha or loot boxes:** everything is bought directly at a shown price.
- **Battle pass, 50 levels**, filled by fixed daily and weekly tasks (play 3 games, log in, share to Facebook, …; the share
  counts when the share sheet completes). Task rewards are kept small so 50 takes the season.
  - **Free tier:** barya at checkpoints, some levels with no reward; level 50 gives a basic skin.
  - **Paid tier:** small rewards on every level; level 50 gives a high-tier skin, plus that kid if not owned.
  - Paid in diamonds: the paid tier costs about ₱250 of diamonds a season, with a premium-plus tier (+10 levels and an extra skin) near ₱500; it uses a
    test unlock until Play Billing is added.

## 9b. Launch and data
- **Closed beta scope:** the core match with all 12 kids, the Barangay map only, accounts and Quick Match, the store and
  pass with test diamonds.
  - **Testers:** about 200, invited friends plus sign-ups through a form on the Facebook page; English text only.
  - **Filling queues:** Quick Match opens at scheduled evening hours (PH time, announced on the Facebook page); private
    rooms with bots stay open at any time.
  - **Reward:** progress resets at launch, but testers keep a beta badge and border.
  - **Feedback:** a bug report button (Settings and the match menu: a note, a screenshot and device info to the server)
    and, at most once a day, a one-question survey after a match ("Was that fun?" 1 to 5).
- **Launch:** closed beta (Play testing track, invites), then a Philippines-only soft launch to tune servers and the
  economy, then Southeast Asia.
- **Data:** match stats on `Kalyeah.Server` for balance, plus a crash reporter. No ad tracking; a privacy policy and
  kid-safe defaults.

**Branding, notices and help:**
- The name and logo show only on the start-up splash after the publisher screen (like Mobile Legends); no logo or tagline
  anywhere in the game's screens.
- **Push notifications:** few and opt-in (friend invites, party ready, pass ending, new season), asked once after the
  first match; kid accounts get party invites only.
- **Help:** an FAQ and a contact form in Settings that attaches the account id and device info; replies by email.
- **Community:** an official Facebook page at launch (it fits the share task), Discord after the soft launch, a creator
  program later.

**After launch:**
- **New kids** arrive when ready (no fixed pace); kariling is saved for the first one.
- **Balance:** numbers live in data, so the server pushes small balance hotfixes without an app update; bigger changes
  land with each season. Patch notes appear as a short card on the next login (which kids changed, with stronger or
  weaker arrows), and each kid's page lists its recent changes.
- **New maps, skins and voices** download as asset packs; the app updates only for new code.
- **Admin page:** a password-protected page on `Kalyeah.Server` for the owner to change balance values, store items and
  events and to work the report queue; every change is logged.

## 10. Look
Pure Valorant for the UI and the world: flat, confident shapes, the Ink / Paper / Red palette, a painted 3D street; the
kids keep the owner's models. Inspired by, never copied
(no Riot, Supercell, Valve or Moonton assets, names or logos). The owner makes all art and audio; agents only say which
kind is needed (3D model, sprite sheet or 2D image).

**Kids' look:** the owner's uploaded models (`assets/models/raw/`) define each kid's look; they are only made game-ready
(`docs/art-briefs/kids-retopo-spec.md`), not restyled. **Role icons** (Striker, Defender, Seeker, Controller) appear in
select, portraits and profile; there are no role colours, so red and blue stay the team colours.

**Graphics:** the top preset aims at Valorant Mobile: clean stylised PBR, real-time sun shadows, soft ambient light,
bloom on effects. Lower presets drop cost, never the art or the readability:

| Preset | FPS cap | Render scale | Shadows | Effects | Post (bloom, grading) |
|---|---|---|---|---|---|
| Low | 30 | 70% | Blob shadows only | Reduced | Off |
| Medium | 60 | 85% | Hard, near the camera | Normal | Off |
| High | 60 | 100% | Soft | Full | On |
| Ultra | 60 / 90 / 120 (phone limits) | 100% + 4× MSAA | Soft, longer distance | Full | On |

- The first launch picks a preset from a short device check; players can change it. When the phone runs hot or the frame
  rate stays low, a prompt after the match (never during) offers a lower preset. A **battery saver** setting caps at 30
  fps and dims effects outside matches.
- **Team colours:** always from your own view: your team blue, enemies red, whichever side you play.
- **Readability is the same on every preset:** enemies wear a **Valorant-style highlight** (red, yellow or purple, a
  setting); allies stay plain; overhead HP bars and team-coloured effects.

**Audio:** OPM-flavoured electronic and hip-hop music with Filipino instruments (kulintang, bandurria, tambol) and street
sounds: a menu theme, ambience per map, round-end stingers. **Positional sound is gameplay (Valorant level):** footsteps
by surface (cement, soil, wood, tin), directional skill cues; crouch-walking is silent.
- **Audio settings:** separate volumes for music, effects, kid voices, announcer and voice chat, and a switch that mutes
  friend and rival banter but keeps skill callouts.
- **Announcer voice:** calls tags, the score, victory and defeat. When a teammate starts a rescue, only their team's
  announcer says so (to cover the rescuer). The enemy never hears a rescue or capture in progress, and defenders get no
  warning that their base is being captured, so a flank to rescue or capture can stay secret.
- **Cues both teams hear:** a short call when the Crocodile or Eagle appears; every ultimate has a voice line.

**Live events:** limited-time events on the Filipino calendar (Pasko, fiesta, Undas, summer) with themed tasks, event
skins and map decorations; an event mini-mode can come later. **First year:** a **Summer showdown** at launch (themed
tasks, banderitas on the maps, a free summer spray, an earnable fiesta skin), **Undas** (maps at dusk with candles and
lanterns, kids in kind folk costumes, respectful of the day) and **Pasko** (paról and Christmas lights on every map, a
gift-to-a-friend task, a free Pasko emote).

## 11. Open questions (agents use the default until the owner decides)
| # | Question | Default |
|---|---|---|
| Q1 | Skill numbers (damage, water, cooldowns, ranges) | Draft values and the damage bands in `docs/KITS.md`, tuned by bot-vs-bot runs; kariling saved for a later kid |
| Q3 | Diamonds per pack | Set with the store build (kid ≈ two weeks of play or ≈ ₱150) |
| Q4 | Kid heights and showcase meshes | After the Narda retopology pilot (`docs/art-briefs/kids-retopo-spec.md`) |
| Q6 | Exact map sizes | Layouts in `docs/MAPS.md`; sizes set in each blockout, play-tested with bots |

## 12. Built today vs this spec
The current build still has: 10 placeholder kids (not the 12), the old superpower loadout instead of 3 fixed skills, LP
ranks, no accounts, no store or pass, rooms without matchmaking, round timers and timed releases, a stick-and-arc HUD, `arm64-v8a` only, and an exact 12-permission manifest. `docs/ROADMAP.md` orders the work to close each gap.
