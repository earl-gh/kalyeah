# Lead designer + developer review: gameplay, UI, and the plan

Scope: everything on `main` plus PR #215 (Game Boy console), and the finished work that is **not on `main` yet** (branches `feat/ui-round17` and
`feat/hud-quiet`). Method: read the rules and the sim, ran headless experiments on the real sim, took 720x1280 screenshots of every screen, read the
earlier audits. Nothing in the game was changed by this review; it is a report and a plan.

What I could **not** judge from a cloud session, and say so now: how the controls *feel* under a thumb, frame rate on a phone, audio, and the web build's
download size. Those are in the plan as device checks, not as findings.

## 1. Verdict

1. **The core loop has a rules problem, not a content problem.** A 5-second base capture is shorter than the time it takes to tag anyone (about 8 s of
   focused fire). In bot rounds the team that rushes the enemy base wins 70 to 100% of the time, and when both teams rush **team 0 wins about 95%**,
   because of a tie-break bug (section 2.1). Until this is fixed, balancing weapons or polishing UI is tuning a game whose result is decided by who runs first.
2. **A lot of finished work is stranded off `main`.** The sparring bot, the balance tooling, UI rounds 14 to 18 and the cast-feedback / quiet-HUD pass all
   live on branches that were merged into each other, not into `main`. A player on the live link does not have any of it (section 4).
3. **A solo player cannot play a real match.** On `main`, practice dummies never attack. The competition judges will most likely play alone.
4. **The UI is in good shape on the screens that were rebuilt** (Home, Book, Settings, Lobby, Loadout, Tagged, Results are consistent and readable), with a
   short list of stale words after the D-pad change, one real accessibility gap (team colour only) and a few polish items for the new console.

Plan in one line: **P0 put everything on `main` and fix the stale text, P1 fix the round rules with data, P2 weapons, P3 a real solo game, P4 UI polish,
P5 phone validation, P6 assets and release.** Details and owner decisions in section 5.

## 2. Gameplay audit

### 2.1 The round is decided by who runs first (high)

Facts from the rules: players move 5 m/s; post to post is 43 m, so **8.6 s** to cross; a capture is *every free player of a team standing in the enemy
base for 5 s* (`capture_time`); nothing blocks the capture except tagging someone; players walk through each other; weapon damage is low (best sustained
damage per kid is about 3.5 to 8 HP/s, 100 HP). Time to tag one kid is therefore about **8 s or more**, longer than the 5 s the attackers need.

I measured it with the real sim and the sparring bot (`FightBrain`, from the stranded branch): 3v3 rounds, random loadouts, 80 rounds per cell, the
"rusher" walks straight into the enemy base and only casts on the way, the "defender" holds its post and fights whoever comes within 14 m. The bots are
crude (rushers never rescue, defenders leave the base to fight), so read the *direction*, not the exact numbers.

| capture time | matchup (3v3) | team 0 wins | team 1 wins | round length |
| --- | --- | --- | --- | --- |
| 5 s (today) | rush vs rush | **76** | 3 | 14.8 s |
| 5 s | rush (team 0) vs defend | 70 | 10 | 16.1 s |
| 5 s | defend (team 0) vs rush | 9 | **71** | 16.3 s |
| 8 s | rush vs defend / defend vs rush | 64 / 16 | 15 / 64 | 20 s |
| 12 s | rush vs defend / defend vs rush | 42 / 36 | 37 / 44 | 30 to 32 s |
| 5 s | 1v1 rush vs anything | rusher wins 80 of 80 | | 13.7 s |

Reading: at 5 s a defender almost never tags anyone in time (an average of 0.5 kids tagged when the point ends). Around 12 s the rusher and the defender
are even. A round is over in about 15 s, which makes best of 3 a 1 to 2 minute match with very little to decide in it.

**Finding G1 (tie-break bug).** `MatchObjectives.check_captures` loops `for candidate in [0, 1]` and stops at the first team that qualifies, so when both
teams stand in each other's base at the same time **team 0 always scores** and team 1's timer never runs. The same loop order applies to "all enemies
tagged". Reproduced in a unit-style probe (both teams placed in each other's bases: team 0 scores at 5.03 s; both teams fully tagged: team 0 scores). In
the table above this is the 76 to 3. On a team-0 / team-1 split like host / guest it is a built-in advantage for one side.

**Finding G2 (the base cannot be contested).** Standing in your own base while the enemy is in it changes nothing about their capture. The only defence is
damage, and damage is too slow for a 5 s window. A defender who has already lost the race has no play.

### 2.2 Weapons (medium)

Source: the stranded balance report (91 loadouts, 16 380 bot duels, `docs/audits/2026-10-20-balance-duels.md` on the integration branch) and the data.

- **`tsinelas_light` is the outlier.** Three boomerangs that hit on the way out and back, 8 HP each, 48 HP per cast, 6 s cooldown: **8 HP/s**, against 3.5 for
  `bato_light`, 2.7 for `gunting_heavy`, 1.3 to 1.8 for the other attacks. 92% score with a good aim, in 9 of the 10 best loadouts.
- **`bato_light` is the new-player trap.** It homes, never misses, 14 HP every 4 s: strong at any skill (82%, 74%). Fine as the easy weapon, but it should
  not also be the best one to be good at.
- **The bottom of the table is utility** (shields, traps, `lata`, `gunting_light`: 35 to 40%). That is expected in 1v1 and unmeasured in a team, where their
  job is to protect a base. **There is no team-level measurement at all**, which is why G1 was invisible until now.
- **Six loadouts have no damaging weapon** and win 0 of their duels (open decision D10). With a 5 s window and a rush meta they are also useless as
  support, because nothing slows a rusher in time.
- **Hard crowd control is long next to the round.** Stun 2 s, airborne 2 s, polymorph 2 s (no cast, half speed) on 12 to 15 s cooldowns, in a game where a
  whole capture is 5 s. Two different hard CCs overwrite instead of chaining, which helps. There is no immunity window after one, which is the usual guard
  against a 3-kid team locking one target for 6 s.
- **Heal** is 30 HP per 12 s (about 2.5 HP/s), roughly the damage of one `bato_light`. Reasonable; its team value is unmeasured.

### 2.3 Match flow (medium)

- **No stalemate breaker.** There is no round timer, no overtime and no shrinking objective. If both teams wait at their posts nothing happens. The clock
  counts up only.
- **Tagged humans wait without a timer.** Only dummy brains walk out after 6 s (`dummy_release_time`). A tagged player waits for a rescue that has to cross
  the street into the other team's half (the box is at the far end), so a single mistake can mean a long wait for one person in a 3-person team. Today the
  tagged card shows who is alive and offers Change weapons, which is a good use of the wait but not a cure.
- **Rescue is very strong**: one touch frees everyone and heals to full HP. With a 5 s capture window it is also the *only* counterplay once someone is
  tagged. After G1 is fixed, check that tagging stays worth it (a tag that costs the enemy 10 s of travel is a good trade; one that costs nothing is not).
- **Cover is two walls on a 27 x 48 m field** (the owner's choice). The street is an open dodge-and-throw field; the only answer to a homing throw is
  a shield. This is fine as a deliberate choice, but it makes shields and `tsinelas` aim the whole game. Revisit after the round rules are fixed.
- **No body collision.** Kids walk through each other, so nobody can block a doorway or a lane. Deliberate and simple; it also means the capture circle can
  never be physically defended.
- **Good**: tagging with the enemy box near your own base (a real risk and reward), free kids hiding in their own box (a bluff the other team cannot read),
  one tag state instead of death and respawn timers, no basic attack so every press is a decision, one-skill-at-a-time, no precasting, auto-aim locked at
  the press (fair), cast-on-release with a cancel zone. Keep all of these.

### 2.4 Solo and learning (high for the competition)

- **Practice on `main`** has three dummies that never attack and one ally that rescues you. It teaches movement and aiming, not the game. The sparring
  partner (`FightBrain`) exists on the stranded branch and fights back, but it is one bot, not a team that rushes, defends and rescues.
- **The first-run walkthrough** (four steps) says "Drag the circle on the left to run"; with the D-pad it should say to press the direction to run (U1).
- A judge or a friend with one phone has no way to see a full round with a rescue and a capture. A **3v3 practice against bots with real team behaviour**
  is the single most useful feature for a competition entry, and the round harness in P1 is the same code.

## 3. UI audit

Screens reviewed at 720x1280 (boot, Home, Book, History, Settings, Lobby, Loadout, intro, match, tagged, results).

### 3.1 What works (keep)
Consistent skin and type scale; gold only for the main action; the Home carousel and tab bar; the Loadout's single info panel; the lobby's numbered team
rows with You / Host / Ready tags; the Tagged sheet (what to do, who is alive, one action); results card; large hit areas (weapon buttons 124 px, pad 280 px);
reduced motion; left-handed and control size; hit glow and the ready swell.

### 3.2 Findings

| # | Sev | Where | Finding | Fix |
| --- | --- | --- | --- | --- |
| U1 | med | Boot, Settings, match hint | **Stale words after the D-pad and the "no skills" change**: boot tip "Two weapons, two skills", Settings "Stick on the right", "The stick and the weapon buttons", the hint "Auto-run · tap stick to stop". Also the walkthrough step "Drag the circle on the left to run". | One string pass: "D-pad", "weapons". Add the lint test from the stranded branch so words cannot drift again. |
| U2 | med | Match, whole HUD | **Team identity is colour only** (blue and red bars, bases, score). About 1 in 12 men cannot tell them apart. The bandana and ring are also colour. | Add a shape cue per team (a notch or an icon on the bar, a stripe pattern on the base tape) and a colour-blind palette toggle in Settings. |
| U3 | med | Console | **The console is 23% of the screen and mostly empty** (a grey slab with two speaker marks). It earns its place by making the controls feel like one object, but the dead grey is a lot of pixels on a 5-inch phone. | Keep the height (the camera needs it), use the space: cooldown or weapon name strip, a pressed-state look on the pad, a small status (HP) readout. Painted skin is an owner asset (2D image). |
| U4 | med | Console, Android | **Home bar and notch.** The console is drawn to the screen edge; a gesture bar sits on grey, and a thumb that rests on the bar edge can swipe the OS. The pad's centre is 170 px in. Not checked on a device. | Read the safe area, pad the console's bottom by it, keep controls above it. Device check in P5. |
| U5 | med | Aim | The aim guide line is 0.07 m wide, built for the old close camera. With the Classic camera it is nearly invisible on a phone (cast-feedback PR measured about 0.18 m as right). | Set in the integration PR (P0). |
| U6 | low | Intro | The "TAG THEIR BASE!" banner sits over the console's sockets, so the console looks broken for 6 s. | Anchor the banner above the console, or draw it on the shell. |
| U7 | low | Tagged | The tagged card covers the whole console and the pad. Correct (controls are off), but the arena above it is cropped to a thin strip on short phones. | Check at 390x844; collapse the card's character row on short screens. |
| U8 | low | Pad | A touch must land on the disc, and the disc is 200 px wide inside a 320 px zone; the zone is bigger than what it draws. | Show the zone as a faint ring while held (already drawn as the outer ring) or grow the disc to the zone. Playtest first. |
| U9 | low | Haptics, audio | Press feedback on the pad (a tick when the direction changes) is missing; the audio and haptics were not reviewed. | One short tick on direction change (Android only); an audio pass with the owner. |
| U10 | low | Lobby | The web build shows "Preview only"; the owner's test channel cannot test rooms from the iPhone web link. | Make Online rooms work in the browser build (the server is WebSocket already) so rooms can be tested on the live link. |

### 3.3 Not reviewed
Audio, haptics, real performance and thermal behaviour, Android back and app-switch behaviour on a device (covered by tests, not by hands), the online
flow against a real server.

## 4. Engineering and process audit

- **Stranded work (high).** `feat/ui-round17` (UI rounds 14 to 18, FightBrain, balance duels, string table, UI lint tests: 117 files, 3 600 lines) and
  `feat/hud-quiet` (cast feedback, quiet HUD, containers) were each merged into their *parent branch*, never into `main`. A trial merge onto `main` +
  PR #215 has **6 conflicts**: three modules the Classic camera deleted (minimap and direction arrows: take the deletion), `match_hud.gd`,
  `match_hud_top.gd` (take main, keep the new D-pad and pulse), and the GDD. After that the minimap *setting*, its control option and
  `test_ui_round17.gd` still refer to a minimap that no longer exists and must go. Root cause: stacked PRs merged top-down. Rule going forward: **every PR
  targets `main`; if it depends on another, say so and merge in order, never into the parent branch.**
- **Docs drift.** The GDD still has headings from earlier eras ("30 m wide x 40 m long"), and sentences about the stick. The ROADMAP
  still lists the League-of-Legends camera as the spec. Plan: one pass, plus a test-free "docs match the data" script for the numbers.
- **Tests: healthy.** 593 passing, about 45 s, plus the stranded branch's UI lint and bot tests. Gaps: no test for simultaneous captures (G1 was
  untested), no team-round test, no Android input test on a device.
- **Code shape.** Largest files: `title_screen.gd` 748 lines, `street_map.gd` 747, `match_hud.gd` 479 (after this week's growth), `weapon_system.gd` 478,
  `lobby_screen.gd` 462. None is urgent. `match_hud.gd` should lose its layout code to a `MatchHudLayout` helper next time it is touched.
- **Budgets unmeasured.** `assets/` is 62 MB (49 MB of models); the roadmap budget is web under 50 MB and APK under 150 MB, 60 fps on a recommended phone.
  Read the size from the Pages run and the APK artifact; measure fps on a device.
- **Release.** Release PR #137 is still 0.8.0 pending (owner's to merge). Many of the last PRs add `feat` commits, so merging it will also produce a long
  changelog; consider merging it right after the integration PR so the version line matches what is live.

## 5. Plan

Ordered by what unblocks what. Every item keeps the repo rules: a branch per task, tests first for sim rules, data in `.tres`, one PR per chunk, the owner
merges, every PR targets `main`.

### P0. One truth on `main` (1 to 2 PRs, mostly mechanical)
1. Owner merges #215 (Game Boy console).
2. **Integration PR** (me): `feat/ui-round17` + `feat/hud-quiet` onto the new `main`: take the Classic camera's deletions, keep the D-pad and console, drop
   the minimap setting and its tests, aim line to 0.18 m, GDD merged by hand. Full suite must pass; screenshots of match, tagged, aiming.
3. String pass (U1) and the lint test; GDD and ROADMAP drift pass; remove the superseded camera sections.
*Done when:* the live link has the sparring bot, quiet HUD and cast feedback, and no text names a stick or a skill that does not exist.

### P1. Make the round fair and decidable (the most important PR)
Tests first, all numbers in `game_rules.tres`.
1. **Fix the tie-break (G1).** When both teams qualify at once, neither captures (or the older timer continues). A test for each of base and all-tagged.
2. **Contested base (G2).** New data toggle `capture_contested` (default on): the capture timer pauses while any free enemy stands in the base. Optional
   `capture_decay` (progress drains instead of snapping to zero) as a second toggle. The HUD already shows a capture countdown; add "CONTESTED" in its place.
3. **Commit the team-round harness** (`tools/balance/team_rounds.gd`, with a rusher / defender / rescuer bot set): reproduce the table in 2.1, then tune
   `capture_time` (probably 8 to 10 s) and the contest rule until: rush vs rush about 50/50, rush vs defend 40 to 60% for the rusher, a round 25 to 60 s,
   at least one tag per round on average.
4. **Overtime** (owner decision): after 60 s with no point the capture time shrinks, or the base grows, so a round always ends. Default off until you pick.
5. **Tagged downtime** (owner decision): options are *(a)* keep as is, *(b)* auto-release after 20 s (data `tag_release_time`, 0 = off), *(c)* teammates
   free you faster the closer they are. Recommendation: (b) at 20 s as a toggle, on for Practice only until you have played it.
*Done when:* the harness table is inside the bands above, a new GDD section "Round pace" states them, and `test_captures` covers every tie.

### P2. Weapon pass (after P1, because P1 changes every number's meaning)
1. Re-run the duel and team harnesses; then change *data only*, a few at a time. Starting hypotheses, each validated by the harness before it lands:
   `tsinelas_light` damage 8 to 6 or cooldown 6 to 8; `bato_light` unchanged (it is the easy weapon); hard CC gets a 1.0 s immunity after it ends
   (`cc_immunity`); `papel_trap` slow 40% for 0.25 s refresh feels too short, test 0.5 s; shields and traps measured as base defenders, not duelists.
2. Pick screen: warn (not block) when a loadout deals no damage (D10); suggest a pair that covers each other (the boot tip already promises this).
3. Update the weapon cards' numbers and D-list in the GDD.
*Done when:* every weapon scores 35 to 65% in the duel table *and* the team table, and no loadout of the 91 is over 70%.

### P3. A real game for one phone
1. **Practice 3v3 vs bots** (rusher, defender, rescuer roles, the P1 harness bots, with `bot_skill` easy / normal): a full best-of-3 with rescues and
   captures. The existing sparring partner stays as "Training".
2. Walkthrough updated for the D-pad and the tag, rescue and capture steps shown in a real mini-round.
3. Result card shows what decided each point (rushed, tagged all, defended) so a new player learns the rules from losing.
*Done when:* a person with no friends online can play a full match and understand why they won or lost.

### P4. UI polish (parallel to P2 and P3)
1. U2: team shape cues and a colour-blind palette toggle.
2. U3, U4, U6, U8, U9: console content, safe areas, banner placement, pad zone, direction tick.
3. U7: short-phone tagged card at 390x844 and 360x640.
4. U10: Online rooms in the browser build so the iPhone link can test multiplayer.
5. Split `match_hud.gd`'s layout into a helper.
*Done when:* screenshots at 390x844, 360x640, 720x1280 and 1080x2400 pass the UI lint and look right; the colour-blind view is checked with a simulated palette.

### P5. Validation on devices (owner and a phone)
Install the debug APK from Actions and check: pad feel and diagonal snap, thumb overlap with the weapon buttons, the home bar, a 10-minute match on a
mid-range phone for fps and heat, the hotspot flow, the web link on iPhone Safari, the download size. Each failure becomes a numbered fix in the next PR.

### P6. Assets and release
Assets are the owner's to make; this is what the game now needs, by kind (no assets generated by me):
- **2D image:** the console shell with the D-pad and two round buttons (painted, 720 wide, the top 294 px of the match screen); a "CONTESTED" base marker;
  team shape badges.
- **Sprite sheet:** pad press states; hit bursts and the capture pulse.
- **Blender:** nothing new for the rules; the posts and base marks if you want the capture state readable on the street itself.
Release: merge #137 right after P0 so the version matches what is live; the signed Android release build (M6) once P5 is clean.

### Owner decisions this plan needs
| # | Question | My default |
| --- | --- | --- |
| D17 | Contested base: pause the capture while an enemy is in the base? | Yes, data toggle on |
| D18 | Capture time after the fix | 8 s, then tune with the harness |
| D19 | Overtime so a round always ends | Off until you pick one of: shrink time, grow base |
| D20 | Tagged waiting time for humans | Off online, 20 s in Practice |
| D21 | Hard-CC immunity window after a stun | 1.0 s |
| D22 | Practice 3v3 vs bots as a mode on Home | Yes, as the Practice card's second button |
| D23 | Colour-blind palette toggle | Yes |
| D24 | Merge order | #215, integration PR, #137, then P1 |

## 6. How the experiments were run
- Capture and tie probes: a headless script that builds a `MatchSim` of 3v3, puts every kid in the other team's base, and steps 10 s. Team 0 scores at 5.03 s;
  with both teams fully tagged, team 0 scores.
- Round table: 3v3 and 1v1, 80 seeds per cell, a `RushBrain` (a `FightBrain` that always walks to its target spot and only casts) against the standing
  `FightBrain`, random loadouts, `skill` 0.5, capture time 5, 8 and 12 s, ended at the first point or 120 s. The script and CSVs are kept in the session and
  are committed in P1 as the team harness.
- Duel table: the stranded branch's `tools/balance/duels.gd`, summarised in its audit.
- Limits: bots are crude and rushers never rescue; the numbers show direction and the size of the gap, not the final tuning.
