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
  The first download holds only Barangay and the 12 kids' default looks; the rest downloads in the background later.
  Over 50 MB on mobile data asks first; Wi-Fi downloads quietly.
- **Permissions:** only what a shipped feature needs (voice chat needs the microphone; hotspot needs the current network set).

## 3. Accounts, age and safety
- **In-game name:** 3 to 16 letters and numbers plus `_ . -`, filtered for bad words in English, Filipino and regional
  languages; one free change, then a small diamond price, at most once every 30 days; names are filtered.
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
  - **Voice:** team only, mic off until the player turns it on; party voice in the lobby; never in kid mode. A setting
    picks push-to-talk (a HUD button, the default) or always on; a "party only" option hears only your party; a speaker
    icon on portraits shows who is talking; long-pressing a teammate's portrait (or the scoreboard) mutes their voice and
    text.
  - **Reports** (cheating, AFK, abuse in chat or voice, bad name; a short optional note), from the results screen or the
    in-match scoreboard; the reporter gets a "thanks, a player you reported was penalised" message when action is taken.
    Repeated reports trigger automatic mutes and AFK penalties; serious cases go to a review queue on the server for the
    owner.
  - **Credit score:** starts at 100; a leave or AFK costs about 5, confirmed abuse 10, and each clean match gives +1 (up
    to 100). Below 90 no Ranked, below 80 no voice chat, below 70 no text chat.
  - **AFK:** no input for 30 s while free shows a warning; at 45 s a bot takes over. Waiting in jail never counts.
- **Friends and party:** parties of 1 to 5 for Quick Match and private rooms; Ranked allows 1, 2, 3 or 5 (no 4-stacks),
  within a rank range. No clans at launch (a *barkada* system may follow).
  - **Adding friends:** by IGN#tag (a short tag after the name, so names can repeat) or friend code, from the last 20
    matches' players, by showing a QR code, and Facebook friends who play (Facebook sign-in). Kid accounts use only the
    friend code with a parent's OK. Up to 200 friends.
  - **Privacy:** appear offline; hide match history (rank stays visible); party invites only from friends by default; the
    Google or Facebook name is never shown, only the IGN.
  - **Party queue:** every member taps Ready, then the leader starts the queue.

## 4. Modes
| Mode | Win | Where |
|---|---|---|
| **Blitz** | First to 3 rounds | Quick Match queue, private rooms, Hotspot, vs AI |
| **Quick** | First to 5 rounds | Quick Match queue, private rooms, Hotspot, vs AI |
| **Standard** | First to 7 rounds | **Ranked** (the only ranked mode), private rooms |
| Practice vs AI, Practice solo | Any mode's rules | Offline; every kid unlocked; bots at easy / medium / hard; Practice solo is a training range with still and moving (strafing, jumping) dummies, distance markers, damage numbers on hits, a no-cooldown toggle, rescue and capture drills with a timer, and a climb and slide course |
| Explore | None | Offline; roam any of the 4 maps alone: one daily coin per map (20 barya each, outside the weekly cap), hidden teks cards (a full set per map gives a spray), route practice (ghost paths of rescue routes and climb spots, with a personal-best timer only: no score, leaderboard or rewards), try any kid and skin, map callouts, photo mode (free camera and kid poses; save to the phone or share, not for kid accounts); no mini-games |
| Tumbang Preso, Patintero | Own rules | Side modes; hidden now, return before launch as a **weekend mode** (Friday to Sunday, alternating weeks, in Quick Match); no skills, any kid; Tumbang Preso 6 free-for-all, Patintero 4v4; pay like Blitz, plus pass XP and a "play 3 side-mode matches" weekend task |

**Bots** (Practice vs AI and leaver takeover) are role-aware: Strikers dive, Defenders guard and rescue, Seekers
scout, Controllers zone; they use skills, vendors and objectives (rescue, capture, flank, take neutrals). Easy, medium
and hard change aim and reaction time; easy bots pause before shooting and say their plans out loud, to teach. Bot names
are funny Filipino-style IGN puns (like "EddieWow", from "Edi wow!"), never a real person's full name.

**Online first, checked at launch (owner):** the app checks the server before home; with no connection it offers RETRY
or PLAY OFFLINE, which opens the offline hub (Hotspot, Practice vs AI, Practice solo, Explore) with TRY ONLINE on top.
Offline is never a mode on home; if the connection drops later, PLAY shows the same check. **Kids are picked after the queue** (Valorant
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
- **Servers:** `Kalyeah.Server` (Docker) on cloud VMs in a Singapore region first; one account and match database. When
  more regions open, the game picks the lowest-ping one and players can switch in Settings; Ranked counts per region.
- **Private rooms:** the host picks the mode and map, fills empty slots with bots at a chosen level, can set custom rules
  (neutrals on or off, rescue time, duplicate kids allowed, a first-to count from 1 to 9, skills off for a slingshot-only
  match) and allow up to 2 spectators. Players join by room code, invite or QR code; the host can move players between
  teams, lock teams and kick. Private rooms give no rewards. A bracket tool for school and barangay cups may come after
  launch with the barkada system.
- **Hotspot:** one phone hosts up to 10 phones over its hotspot or the same Wi-Fi; bots fill empty slots; the host needs
  a mid-range or better phone. Every kid is unlocked in Hotspot so friends can play anything together. The host sets the
  same custom rules as a private room, bot levels for empty slots, and any map every phone has downloaded. If the host
  leaves, the match ends for everyone with no penalty. Hotspot works fully offline: a phone that never went online plays
  as a local guest (after the first-run steps), and kid accounts play with quick chat only.
- **Anti-cheat:** besides the server deciding hits, it validates movement speed and fire rate; Ranked requires a passing
  Play Integrity check (rooted or modded phones can still play casual modes).
- **Bad connections:** ping and packet loss on the HUD; a dropped player can reconnect within 3 minutes while a bot plays
  their kid; the queue prefers players under 120 ms. A phone call or app switch counts as a disconnect: a bot covers,
  with no penalty if the player is back within 3 minutes.
- **Long queues:** Quick Match and Ranked keep searching for real players and never start with bots, except in a new
  account's first 10 matches (below).
- **Spectating and replays:** only private-room spectators (up to 2, on a short delay, for casters and referees) at
  launch; no watching friends' Quick Match or Ranked, and no replays.
- **Server-authoritative:** the server decides every hit and score; phones send inputs only (anti-cheat backbone).

## 5. Agawang Base rules
- **Teams:** 5v5. Each team has a **base** (the post) and, beside it, a **jail** where the kids it tags wait.
- **A round ends** when a team **captures the enemy base** or **tags every enemy**. That team scores a round and every kid
  resets at its own base. **No round or match timer:** a round lasts as long as the fight does, and nothing escalates over
  time. Design target: a first-to-7 match lasts about 15 to 20 minutes (rounds of 1 to 2 minutes), tuned with map size
  and rescue time.
- **Round end:** a 3 s banner ("Captured!" or "All tagged!") with the round score, then every kid resets into the
  barrier phase.
- **Round flow:** each round starts with a **5 s barrier phase** at base (walk inside the base, ping, draw arrows on the
  full map), then the barrier drops. Water,
  S1 and S2 are full each round; **ultimate cooldowns carry over** between rounds (they reset only at the side swap).
  The dragon and baron timers restart every round. After the final round, a 6 s victory scene shows the 5 winners in their
  skins doing their victory poses (or a chosen emote) with the MVP front and centre, then the results screen.
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
- **Skills and jail:** a tagged kid's cooldowns keep counting in jail, and their walls, traps and sentries stay until
  they run out.
- **In jail:** a tagged kid sits visibly in the jail (pouting, slightly faded), watches a teammate's view (switchable), and can ping and chat. Tapping a free teammate's portrait **cheers** them (they
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
- **Map choice:** before kid select, players vote between **2 maps** drawn from the maps every player in the match has
  downloaded; private rooms let the host pick.
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
- **Heights:** kids look different heights, within about ±10% of the average, but share one hitbox and headshot zone.
  **Narda is the pilot kid**, built fully first (model, animations, skills) to set the quality bar.
- **Heavy lore:** grounded **barangay rivalry**: kids aged **8 to 12** from rival puroks and barangays settle disputes
  through street-game showdowns every summer; the skills are their imagination made real on screen. Every kid has a deep
  backstory (family, barangay, friends, rivals) shown in a **codex per kid** (chapters unlock with mastery) and in
  **in-match voice banter** between allies and rivals. Agents draft the text in docs, keeping the respect rule; the owner
  edits and approves. Draft: [`docs/LORE.md`](LORE.md) (4 puroks of 3 kids, friends and rivals,
  playful never-mean Taglish banter, a personality, idle and victory pose per kid).
- **Basic attack for all:** the slingshot (*Tirador*), 7 shots and a reload button (it also reloads by itself when
  empty); full accuracy while jumping, vaulting or sliding; holding the attack button aims farther,
  up to a maximum range.
- **3 fixed skills per kid: S1, S2 and an ultimate**, all on cooldowns; S1 and S2 also cost **water** (Kalyeah's mana, like Mobile Legends and Wild Rift: slow refill in the street, very fast
  in your own base).
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
- **Skill aiming (Wild Rift, with a per-skill view like Valorant: normal, ADS, zoom-out or map cast, see `docs/KITS.md`):** tap to quick-cast at the crosshair or nearest target; drag from the button to aim with a
  ground or line indicator; drag to the cancel zone to cancel.
- **Hits:** slingshot headshots deal +50%. **Aim assist** (a setting) only slows the crosshair slightly over enemies; no
  pull, no snapping, no auto-fire. **No friendly fire:** damage and crowd control never touch allies.
- **Tag credit:** the last hit tags; anyone who damaged, slowed, stunned or revealed that kid in the last 10 s gets an
  assist (both count for MVP). Revenge release follows the kid who landed the last hit.
- **Chained control:** after a hard stop (stun, root, knock-up, knock-down, turned into a can) a kid can't be
  hard-stopped again for 1 s; slows still apply.
- **Healing:** **5 HP a second** once a kid has gone 5 s without taking damage, attacking or being under crowd control; vendors and skills are the fast heals.
- **Movement (CODM-like), 3 close speeds:** **walk 4.5 m/s** (stick partly pushed), **jog 5.25 m/s** (stick fully
  pushed), and **run 6 m/s** after 1.5 s of steady jogging (stopping, turning sharply, shooting or aiming drops back to a
  jog); the steps stay small so speed changes never feel jumpy; **one jump button** does jump, running jump, vault (small obstacles) and climb
  (high obstacles) by context; **one crouch button** crouches when walking and slides when running (slide jump and back
  slide included). Crouch-walking is silent and 40% slower. A slide is a 3 m burst that drops the head below most shots
  for 0.3 s, then a 2 s cooldown. A vault takes 0.4 s and a climb about 1 s; a hit while climbing knocks the kid down.
- **Aim down sight:** shots land exactly at the crosshair (hip-fire spreads a little) and the kid walks 30% slower; a
  setting picks tap-to-toggle or hold.
- **Camera:** the same perspective as T3 Arena and Deadlock: **slightly far behind** the kid (about 5 to 6 m back and a
  little above), offset a little to the right so the kid stands just left of centre and the crosshair has a clear view
  past them; not tight on the shoulder; no shoulder swap; **aim down sight** moves in close, some attacks zoom out. FOV slider in a fair range (70 to 90)
  that is the same for everyone.
- **Feel:** crisp and readable like Valorant: hit markers, a headshot sound, a small kick on firing, a brief flash on the
  target, no screen-filling effects.
- **Combat read-outs:** damage numbers off by default (a setting turns them on); a red arrow around the crosshair
  points at whoever hit you for 1 s, and an attacker within 15 m flashes briefly on your own minimap. Enemy overhead bars
  show HP and a small status icon (Slowed, Rooted, …). When tagged, a 2 s card shows the tagger's kid, the stone or skill
  and their HP left, with your damage dealt this round.
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
  - right edge: a small feed of tags and rescues with the skill used (a rescue appears only once it completes); big centre banners only for team events (match point, capture,
    all tagged, dragon or baron taken, side swap);
  - bottom centre between the thumbs: your HP and water; the ammo count (of 7) on the slingshot button; overhead bars on
    every kid.
- **Screens at launch** (familiar from Mobile Legends, Wild Rift, CODM and Valorant): home (one PLAY, online first), lobby
  with party and invites, kid select after queue, kids and skins collection, store, battle pass and tasks, profile with rank and match history,
  friends and party, settings.
- **Kids (bottom navigation):** all 12 kids as a grid with a role filter; owned kids show mastery, others their price.
- **Kid page (collection),** tabs like Wild Rift and Mobile Legends: **Skills** (a clip of each skill with a combo tip),
  **Skins** (every skin to try, and the victory pose), **Story** (purok, home, dream job, friends, rival and the codex
  chapters unlocked by mastery), **Mastery** (level, win rate and recent balance changes); the kid in 3D to rotate, and
  SET AS HOME KID or the unlock price. Lore and mastery appear only here, never on home.
- **Home** (owner layout, read like CODM and Wild Rift): profile top left with name and level only (match history inside
  it, the Mobile Legends way); barya, diamonds and settings top right; **left column** for the pass with today's task, the
  live event and the daily gift; the favourite kid in 3D in the centre with a small name and role plate (tap for the kid
  page); above **one big PLAY** a mode card with the mode name and a short line on how it feels (tap to switch Blitz,
  Quick, Ranked or a private room); a **bottom navigation bar**: Kids, Store, Friends. Home never shows rank, team size,
  scoring, map rules, lore or mastery, and no store banners.
- **Lobby** (separate from home, like Mobile Legends and Wild Rift): PLAY opens it for the chosen mode; your party as 5
  cards (you first, empty ones say INVITE), a friends list with online status and INVITE, and FIND MATCH. Matchmaking runs
  here with the timer and estimated wait; leaving the lobby keeps searching and home shows a pill back to it. **Rank shows
  only where Ranked is chosen:** on the Ranked row of the mode picker and in the Ranked lobby (plus profile and loading).
- **Kid select:** the 12 kids in 4 role rows (not-owned greyed, trial-card kids marked); a team bar with hovers, locks
  and a missing-role warning; tapping a kid previews its 3 skills with short clips.
- **Loading:** every player's kid in their skin, with name, rank and honor badge, and a tip about the map.
- **Results actions:** add any player from the match as a friend (not in kid mode) and share a result card to Facebook
  (counts for the share task).
- **Match menu (Back):** online matches keep running (your kid stands still; Practice pauses); quick settings only
  (sensitivity, volumes, graphics, layout editor), the scoreboard with mute and report, and Leave match, which warns of
  the penalty (credit score, queue cooldown, a Ranked star) and then counts as a leave with a bot taking over; no penalty
  in Practice or Hotspot.
- **Only real content:** a screen, tab or button exists only when the game has the thing behind it; no filler
  components copied from other games.
- **English only** for text; kid voice lines are short **Taglish** barks on skills, tags, rescues and wins.
- **Home kid:** the player's **favourite kid** (pinned in the collection) stands in 3D on the home screen.
- **Accessibility:** colour-blind enemy highlight, visual markers for footsteps and skill sounds, reduce motion and flashes,
  and crouch and ADS as toggles instead of holds.
- **Settings at launch:** a **Donate** tab (behind the grown-up check; gifts give nothing in the game); graphics presets and an FPS cap (30 / 60 / 90 / 120 where the phone allows), a control layout
  editor (move and resize every HUD button, saved layouts), camera and ADS sensitivity, aim assist on or off, crosshair
  style and colour, vibration on hits and tags, and a ping and FPS counter.

**First run:** a one-tap terms and privacy notice (the graphics device check runs quietly meanwhile), the age question,
guest or Google sign-in (guest first and biggest), an in-game name (IGN), then a **tutorial** before the home screen.
**Returning players** go straight to home: the login reward is a small card there, and after an update one "what's new"
card shows; no launch popups. **While queueing** the player can leave the lobby to browse home, the collection or the
store; the search keeps running and a pill with the timer leads back to the lobby. The
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
- **Profile stats** (tabs History and Overview): win rate per mode and per kid, the last 20 matches (result, kid, tags, rescues, captures, MVP marks),
  a favourite-role chart and the highest rank of each past season.
- **MVP reward:** +20 barya (inside the weekly cap) and bonus mastery XP for the kid played; the losing team's MVP also
  keeps their Ranked star.
- **Honor:** one honor per match for any player (teammate or rival); being honored raises your credit score a little,
  and your honor level shows on your profile and in kid select.

## 9. Progression and store
- **New account:** after the tutorial the player **picks 4 starter kids, one per role**, from all 12.
- **Daily login:** a 7-day loop: 50, 50, 100, 50, 100 and 150 barya on days 1 to 6, then on day 7 a 3-day trial card
  (odd weeks) or a spray (even weeks); it also counts for the pass's login task.
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
- **Offline rewards:** vs AI gives a little barya and pass XP (capped daily); Explore's coins sync to the account when
  online; Hotspot gives no rewards (every kid is unlocked there and it is just for fun). Offline results can't be verified by the server, so their caps stay small.
- **Account level:** earned by playing; unlocks text chat (level 3), voice chat (level 5), Ranked (level 8), sprays and
  emote slots, with a little barya per level;
  a prestige border after level 30.
- **Diamonds:** ₱1 = 1 diamond, so prices read like pesos (a kid 149 💎; skins 49 / 149 / 299 / 499 / 899 💎). 6 packs:
  ₱20 = 20, ₱50 = 50, ₱100 = 105, ₱250 = 270, ₱500 = 550, ₱2,000 = 2,300 (small bonuses on bigger packs) and double diamonds
  on each pack's first buy, plus a ₱50 weekly diamond card (50 💎 at once and 10 a day for 7 days, 120 in all); no limited-time pressure offers in kid mode.
- **Bundles:** a once-per-account **starter bundle** for ₱49 (a kid chosen from 3 not yet owned, one per missing role
  if possible, an Elite skin and 50 💎); kid + launch
  skin bundles at a discount; a season set (all the season's skins with a spray and a border, cheaper than one by one).
- **What each skin tier adds:** Basic new colours; Elite a new outfit; Special also the slingshot look; Epic also skill
  effects and sounds; Legend also voice lines, a victory pose and a home-screen intro.
- **Fair skin effects:** a skin restyles and recolours a skill but never changes its size, shape or sound timing, and
  enemy effects always keep their team colour.
- **Emotes and sprays** come from the pass, events, the mastery emote per kid, and cheap store items for diamonds.
- **No free starter skins:** kids start in their default look.
- **Skin themes:** season 1 is **summer fiesta** (banderitas, parade costumes, higante masks, sorbetes colours; Narda headlines
  it with a fiesta parade costume as the pass's level-50 skin), matching
  the lore's summer showdowns. Lines that return: **Filipino holidays** (Pasko, Undas, fiesta, summer, yearly with the
  events) and **dream jobs** (what each kid wants to be, tied to their lore).
- **Store tabs:** Featured (the season's skins and bundles), Kids, Skins, Daily (3 items you don't own at a small discount, never kids, plus one free small gift; resets at midnight PH time), Diamonds; every item
  can be previewed on the 3D kid before buying.
- **No gacha or loot boxes:** everything is bought directly at a shown price.
- **Battle pass, 50 levels**, filled by fixed daily and weekly tasks; the share counts when the share sheet completes:
  - **Daily:** log in, play 3 matches (vs AI counts for 1), do 1 rescue or capture, win 1 match.
  - **Weekly:** play 15 matches, play 2 different roles, share to Facebook (kid accounts: play 1 match), take 3 dragons
    or barons.
  - Task rewards are kept small so 50 levels take the season.
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
- **Maintenance:** a notice on home a day ahead (PH time); the offline hub keeps working; some barya as a gift if it ran
  over 2 hours.
- **Required updates:** online play needs the latest version (home sends you to the Play Store); Practice, Explore and
  Hotspot (between phones on the same version) keep working.
- **Help:** a "How to play" page with short animated rule cards (tag, rescue, capture, neutrals, water), and an FAQ and a
  contact form in Settings that attaches the account id and device info; replies by email.
- **Community:** an official Facebook page at launch (it fits the share task), Discord after the soft launch, a creator
  program later.

**After launch:**
- **New kids** arrive when ready (no fixed pace); kariling is saved for the first one. A new kid launches with 30% off
  in barya and diamonds for 7 days, a free 1-day trial card for every player and a kid + skin bundle.
- **Balance:** numbers live in data, so the server pushes small balance hotfixes without an app update; bigger changes
  land with each season. Patch notes appear as a short card on the next login (which kids changed, with stronger or
  weaker arrows), and each kid's page lists its recent changes.
- **New maps, skins and voices** download as asset packs; the app updates only for new code.
- **Admin page:** a phone-friendly web page on `Kalyeah.Server` behind a password and a second login code, for the owner
  to change balance values (one-tap undo), store items and events; work the report queue (each report with chat logs and
  the match's stats); ban, mute or give barya to a player with a reason the player sees; and watch players online, queue
  times and win rates per kid and map. Every change is logged.
- **Bans:** abuse and AFK climb 1 day, 7 days, then permanent; confirmed cheating is permanent at once. One appeal per
  ban through the contact form, landing in the admin queue.

## 10. Look
Pure Valorant for the UI and the world: flat, confident shapes, the Ink / Paper / Red palette, a painted 3D street; the
kids keep the owner's models. Inspired by, never copied
(no Riot, Supercell, Valve or Moonton assets, names or logos). The owner makes all art and audio; agents only say which
kind is needed (3D model, sprite sheet or 2D image). **Art source:** the owner generates models with **Meshy** (the 12 kids already are), and agents may use
**licence-checked free assets** (CC0 from Poly Haven, ambientCG, KayKit and Quaternius; CC-BY from Sketchfab with a credit
line in a Credits screen), logged in `assets/CREDITS.md`. Agents make every model game-ready by script (budget, scale,
facing, texture layout, painted materials) and the owner approves each from screenshots.

**Kids' look:** the owner's uploaded models (`assets/models/raw/`) define each kid's look; they are only made game-ready
(`docs/art-briefs/kids-retopo-spec.md`), not restyled. **Role icons** (Striker, Defender, Seeker, Controller) appear in
select, portraits and profile; there are no role colours, so red and blue stay the team colours.

**Graphics** (details in [`docs/GRAPHICS.md`](GRAPHICS.md)): the top preset aims at Valorant Mobile: clean stylised PBR, real-time sun shadows, soft ambient light,
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
- **Music in a match** only at key moments, so footsteps stay clear: a short track in the barrier phase and stingers at
  round end, side swap and match point. Fights have ambience only.
- **Map ambience:** distant people and radios (a far karaoke, a passing tricycle, a vendor calling "taho!") and animals
  (roosters, aspin barks, seagulls at the pier, a carabao on the farm), quiet enough not to hide footsteps.
- **Match point:** the announcer calls "Match point!" with a music sting, and both teams see a MATCH POINT banner in the
  barrier phase.
- **Audio settings:** separate volumes for music, effects, kid voices, announcer and voice chat, and a switch that mutes
  friend and rival banter but keeps skill callouts.
- **Announcer voice:** a Taglish barangay fiesta emcee with English text ("Na-tag na lahat! Panalo ang Uptown!"); calls tags, the score, victory and defeat. When a teammate starts a rescue, only their team's
  announcer says so (to cover the rescuer). The enemy never hears a rescue or capture in progress, and defenders get no
  warning that their base is being captured, so a flank to rescue or capture can stay secret.
- **Cues both teams hear:** a short call when the Crocodile or Eagle appears; every ultimate has a voice line.

**Live events:** limited-time events on the Filipino calendar (Pasko, fiesta, Undas, summer) with themed tasks and event
skins; **maps never change for events** (the theme shows only through skins and cosmetics); an event mini-mode can come
later. **First year:** a **Summer showdown** at launch (themed tasks, a free summer spray, an earnable fiesta skin),
**Undas** (kids in kind folk-costume skins, respectful of the day) and **Pasko** (Pasko skins, a gift-to-a-friend task, a
free Pasko emote).

## 11. Open questions (agents use the default until the owner decides)
| # | Question | Default |
|---|---|---|
| Q1 | Skill numbers (damage, water, cooldowns, ranges) | Draft values and the damage bands in `docs/KITS.md`, tuned by bot-vs-bot runs; kariling saved for a later kid |
| Q3 | Final pack bonuses | Set with the store build (kid ≈ two weeks of play or ≈ ₱150) |
| Q4 | Exact kid heights and showcase meshes | Heights within about ±10% of the average with one shared hitbox and headshot zone; exact values after the Narda pilot (`docs/art-briefs/kids-retopo-spec.md`) |
| Q6 | Exact map sizes | Scale and layouts in `docs/MAPS.md`; exact shapes set in each blockout, play-tested with bots |
| Q7 | Prints on two kid models that copy real brands: Inggo's shirt has the Superman "S" logo and Narda's shirt shows *Mulawin* (a GMA TV series) art | Kept as the owner made them for now; replace before any public test (a Meshy re-texture or a new print) |

## 12. Built today vs this spec
**Done toward this spec:** the 12 kids from the owner's Meshy models, made game-ready by script (about 16k to 20k
triangles each, baked textures, the shared rig and its 20 clips; `tools/blender/kids/README.md`) with rendered portraits
and cards; Home's PLAY with a Blitz / Quick / Offline switch and no filler (EVENTS and the Explore placeholder removed);
the match HUD's HP chip at the bottom centre and the minimap on in the top-left corner; the script-made Barangay
models retired for grey-box blocks, with the first licence-checked CC0 props (`assets/CREDITS.md`). The §5 Agawang Base rules in the sim, bots and match view (enemy jail, defuse-style rescue with its checkpoint, revenge release, contested capture with drain, the round-start barrier, no round clock).

**Still to build** (`docs/ROADMAP.md` orders it): the old superpower loadout instead of the 12 kits' 3 fixed skills; the
slingshot's 7 shots, reload and water (so no ammo count or water bar on the HUD yet); LP ranks instead of stars; no
accounts, store, pass, party or friends (so no party slots or pass card on Home yet); rooms without matchmaking; no neutrals (so no dragon and baron timers on the minimap yet); `arm64-v8a` only and an exact
12-permission manifest; real Barangay buildings, vehicles and trees.
