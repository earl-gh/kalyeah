# Tap to move, a hand of 4 with elixir, stamina (spec and balance plan)

Owner, 2026-10-24: "make the bottom part exactly like clash royale then characters movement is tap based like League of Legends or DOTA."
Casting by dragging a card onto the street (range and position respected, hold to hover, release to cast, drop on the hand to cancel, a quick tap
auto-aims); a hand of 4 with elixir as the cooldown; stamina that exhausts the kid for 2 s at empty. Desktop: right-click to move, left-click cards.

The rules are in `docs/GDD.md`, section **Controls: tap to move, cards to cast**. This file holds the evidence for the numbers, the thresholds fixed
before anything is measured, the baseline, and the bot run the engineer's build must pass. Nothing in `data/*.tres` changes until the fields exist.

## Hypotheses (player terms) and the metric for each
1. **Elixir keeps the pace of casting.** A player casts about as often as with the shuffler. Metric: casts per player per minute; seconds to a point.
2. **Stamina makes a non-stop run a choice, not a stall.** A kid can run one full street on a full bar; running and kiting all round needs a pause.
   Metric: exhaustions per kid per round; seconds to a point; rounds with no point.
3. **A full elixir bar cannot delete a kid.** Four cheap attacks in one hand are rare and should not tag from full health before the target can react.
   Metric: share of tags where every hit on the tagged kid landed inside 3 s.
4. **Heal, trap and block stay worth a draw.** Metric: per-weapon share of casts, and share of each card's draws that end in a cast within the round.
5. **No side bias.** Metric: win share in every mirror.

## Thresholds (fixed now, before any run)
Compared with the baseline below, same seeds (80), same tool, 3v3, cards on, `bot_skill` 0.4 both teams:

| metric | keep the new rules if |
| --- | --- |
| seconds to a point, per matchup | within +/-25% of baseline |
| rounds with no point, per matchup | no more than +8 of 80 |
| mirror win share (rush vs rush, mixed vs mixed) | the larger side has at most 60% of decided rounds |
| casts per player per minute | within +/-20% of the shuffler build (engineer measures both, same seeds) |
| exhaustions per kid per round (bots) | between 0.1 and 1.5 (0 = stamina never matters, more = bots stall) |
| tags with every hit inside 3 s | at most 15% of tags |
| any weapon's share of casts | at least 3% (1/14 is 7.1%); otherwise its cost is the first number to change |

## Baseline (current rules: hand of 2, shuffler cooldowns, D-pad bots)
`godot --headless -s tools/balance/team_rounds.gd -- --seeds 80 --size 3` (capture 5 s, no timed release, contested on; 4 min 54 s):

| matchup (team 0 vs team 1) | team 0 | team 1 | no point | avg s to point | tags/round |
| --- | --- | --- | --- | --- | --- |
| rush vs rush | 35 | 29 | 16 | 49.5 | 3.0 |
| rush vs defend | 47 | 24 | 9 | 45.9 | 3.0 |
| defend vs rush | 28 | 44 | 8 | 49.1 | 3.0 |
| mixed vs mixed | 6 | 6 | 68 | 92.0 | 1.5 |
| mixed vs rush | 25 | 42 | 13 | 63.1 | 3.1 |
| defend vs defend | 0 | 0 | 80 | 0.0 | 0.0 |

Mirror rush is 35/29 (55%), inside the 60% line. The tool does not print casts per minute, per-weapon share or exhaustions yet: the engineer adds
them (see "What the harness must print").

## Elixir numbers from the cooldowns
- Current cooldowns (`data/weapons/*.tres`): 4, 12, 5, 9, 12, 14, 8, 10, 14, 11, 12, 15, 12, 14 s; sum 152, mean 10.86 s.
- With the shuffler, one card can be handed back per cooldown, so a player's sustained rate is about **one card per 10.9 s**.
- Elixir regenerates at **0.36 per second** (one point per 2.8 s, the pace Clash Royale players know). A card costs its cooldown divided by 2.8,
  rounded to a whole point. Bato (light) rounds to 1 (4 / 2.8 = 1.43); it is set to **2** instead, because a 1-cost 12-damage homing rock would be
  the best damage per elixir by twice and the cheapest way to cycle the deck.
- Costs: bato_light 2, bato_heavy 4, gunting_light 2, gunting_heavy 3, papel_trap 4, papel_shield 5, tsinelas_light 3, tsinelas_heavy 4, lata 5,
  jacks 4, bola 4, trumpo 5, heal 4, langit_lupa 5. Sum 54, mean 3.86, so **one card per 10.7 s** sustained (-1.4% against the shuffler).
- Bar: 10 points, start 5 (two cards of average cost at GO), regeneration stops at 10.

## Card cycle check (required for every cycle change)
- A played card goes to the back of the deck; the deck has 14, the hand 4, so it is back in the hand after **10 more casts** (it is NEXT after 9).
- Expected wait: 10 x (54 - cost) / 13 / 0.36 s = **107 s** for a 4-cost card; **longest possible**: the 10 dearest other cards (5,5,5,5,4,4,4,4,4,4 = 44)
  = **122 s**; shortest: the 10 cheapest (2,2,3,3,4,4,4,4,4,4 = 34, for a 5-cost card) = **94 s**.
- Shuffler build, for comparison: back after 12 casts (14 - 2), each gated by its own cooldown: about 12 x 10.9 = **130 s**. The new cycle is about
  20% shorter and every card can always be played (no empty slot).
- A round lasts 46 to 92 s in the baseline: a player casts about 5 + 0.36 x 50 = 23 elixir = **6 cards in a 50 s round**, so a card is seen about once
  per round and the whole deck about once a match. Same as now.
- **Dead-weight risk:** the two blocks (papel_shield, langit_lupa, 5 each: 14 s of elixir for a 2.5 or 3.5 s wall) were near the bottom of the bot
  duels already ([balance duels](2026-10-20-balance-duels.md)). They keep their old cadence, but now compete with attacks for the same bar. They are
  the **first change to test** if their cast share is under 3%: 5 -> 3. Heal (30 HP for 4 = 7.5 HP per point) sits among the attacks' value per point (bato_heavy 5.5, bato_light 6,
  gunting_heavy 8, tsinelas_light up to 10, gunting_light up to 18 at six snips), so heal should not be dead weight. Papel trap (4) is next to watch.
- **Burst risk:** bato_light 2 + gunting_light 2 + gunting_heavy 3 + tsinelas_light 3 = 10, up to 102 damage on a 100 HP kid. Needs those four in one
  hand at a full bar and all of them to land. If tags inside 3 s exceed 15%: raise gunting_heavy and tsinelas_light to 4 first (bar 10 then fits
  only three of them).

## Stamina numbers
- Run speed 5 m/s; post to post is 48.3 m (posts 2.5 m in from both edges of a 27 x 48 m street), 9.7 s.
- **Max 100, drain 10 per second of moving (10 s = 50 m), regen 25 per second (4 s from empty to full) after 0.5 s standing still.** One full
  street fits on a full bar with 0.3 s to spare, so a rush is never stopped by stamina alone; a rusher arrives nearly empty and has to stand
  (which a capture asks for anyway) or be exhausted when dodging. A fight spent strafing (bots move all the time) runs out in 10 s.
- The 0.5 s regen delay stops a stutter-step (move a tick, stand a tick) from running for ever with no drain.
- Exhaustion: 2.0 s with no movement (owner); stamina refills at the normal rate meanwhile (no delay), so the kid starts again with 50.
- To check in the bot run: exhaustions per kid per round inside 0.1-1.5, and seconds to a point within +/-25%. If bots stall (more than 1.5),
  first raise `stamina_max` to 120, not the drain.

## Movement numbers
- `move_arrival_radius` 0.1 m. One tick at 5 m/s is 0.167 m, and the last step is cut to the distance left, so a kid stops on the point.
- Walls: the existing slide (`Collision.resolve`) is kept. A move ends by itself if the kid made less than a quarter of its intended progress for
  0.2 s (`move_stuck_share`, `move_stuck_time`): head-on into the cover wall stops, a shallow angle slides along it.
- No pathfinding: the street has only two cover walls (3 m, each against a curb) and the edges, so any point is reached in a straight line or by
  sliding along at most one wall; the player re-taps past a wall. Revisit if the map gets more props inside the play area.

## What the harness must print (engineer, with the new rules)
Per matchup, besides today's columns: casts per player per minute, exhaustions per kid per round, share of tags with every hit inside 3 s, and
per-weapon share of casts. Run with `--seeds 80 --size 3`, the same as the baseline, once with `elixir_enabled` on and once off (shuffler build).

## After the build (2026-10-24, `team_rounds --seeds 80 --size 3`, elixir on, hand 4, stamina on, `elixir_regen_per_s` 0.6)
| matchup | team 0 / team 1 | no point | seconds to a point | tags | casts / player / min | exhaustions / kid / round | burst tags |
|---|---|---|---|---|---|---|---|
| rush v rush | 45 / 33 | 2 | 67.2 | 3.5 | 7.69 | 5.03 | 2.7% |
| rush v defend | 55 / 25 | 0 | 46.3 | 3.8 | 7.63 | 1.63 | 2.6% |
| defend v rush | 21 / 57 | 2 | 46.2 | 3.6 | 7.34 | 1.80 | 5.1% |
| mixed v mixed | 0 / 0 | 80 | none | 0.9 | 4.06 | 3.25 | 0.0% |
| mixed v rush | 26 / 52 | 2 | 74.6 | 3.7 | 7.05 | 3.76 | 1.7% |
| defend v defend | 0 / 0 | 80 | none | 0 | 0 | 0 | 0 |

How it got here: at the first spec value (0.36) the same build took 86.2 s a point in the rush mirror with 48 of 80 rounds ending with no point (the elixir-off, hand-2 control: 72.8 s, 16 of 80); stamina off barely changed it, so the slowdown was the elixir rate. Raising it to 0.6 fixes the round length.
Against the thresholds: seconds to a point (rush mirror 67.2 s against 72.8 s with the shuffler) and rounds with no point (2) pass; burst tags (at most 5.1%) pass. **Fail:** casts per player per minute are +37% against the shuffler build (7.7 against 5.6; a feel question: a faster game is closer to Clash Royale), exhaustions per kid per round are 1.6-5.0 (allowed 0.1-1.5), and gunting_light and gunting_heavy are 1-4% of casts in most matchups (bots stand off and rarely reach 3 m). Mixed v mixed and defend v defend never score with these bots, as before.
Next, by owner: a tired bot stops strafing in a fight and the scissors need closing-in behaviour (gameplay-engineer, bot code); then re-run and, if the casts-per-minute overshoot matters, try 0.45-0.5.

## Status
BUILT. First after-table above; three thresholds still fail (bot behaviour and the cast rate). Costs kept as specified.
