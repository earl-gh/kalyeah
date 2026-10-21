# Practice vs bots (P3 of the lead review)

From [the review](2026-10-21-lead-review.md): a person with no friends online could not play a real round. Practice on `main` was a sandbox with dummies and
one sparring partner.

## What changed
- **Play vs bots**, a full 3 v 3 best of 3 on the Practice card (gold); **Training** (grey) is the old sandbox.
- **Bots are real players of the sim** (not training dummies), so they count for captures, can be tagged and rescued. `TeamBrain` (a rusher or a defender) plays
  one bot, `TeamCoach` plays a team: your allies back you up, the enemies wait 25 s and then attack in waves (all numbers in `game_rules.tres`).
- **The results card says how each point was won** (held their base, tagged them all), one chip per point.
- The first-run walkthrough's *Score a point* step now says the base must have no defender left in it, and the D-pad wording is current.

## What the bots did (headless, real sim)
- With **nobody at the controls** (you AFK at your post, two allies defending, three enemies in waves), 24 matches ran up to 4 minutes: 13 produced a point
  (8 for the enemies, 5 for your allies, who do defend and tag), 11 had none. First points came between 50 and 195 s. So the enemy is not an instant loss and
  not a pushover.
- **Aim skill hardly mattered** in the round harness: defenders against an all-in rush lost 31 of 31, 30 of 31 and 33 of 34 with the attackers' aim at 0, 0.2 and 0.4. What decides it is the
  map's box placement (D25), which is why the enemies attack in **waves** and not in one endless rush: a human needs windows to counter-attack.

## Limits and what to feel on a phone
- The enemy plan is one rule (wait, wave, retreat); it does not use cover or play shields cleverly. It is a sparring partner for a first match, not a rival.
- No difficulty choice yet; `bot_skill`, `bot_assault_delay`, `bot_assault_window` and `bot_rest_time` are the four dials. Say what feels too hard or too easy.
- Bots pile on one point when they defend (no body collision), which looks odd but is the same rule as for players.
- Online and Hotspot rooms are unchanged.
