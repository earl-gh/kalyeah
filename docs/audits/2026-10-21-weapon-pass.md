# Weapon pass (P2 of the lead review)

From [the review](2026-10-21-lead-review.md): `tsinelas_light` was the outlier (92% in bot duels), hard crowd control had no guard against chaining, and
a pair of harmless weapons gave no warning. All numbers are data (`data/weapons/*.tres`, `data/rules/game_rules.tres`).

## What changed
| | Before | After |
| --- | --- | --- |
| `tsinelas_light` | 8 damage each way, cooldown 6 s (48 per cast, 8 HP/s) | **5 each way, cooldown 8 s** (30 per cast, 3.75 HP/s) |
| `bato_light` | 14 damage, cooldown 4 s (3.5 HP/s) | **12 damage** (3.0 HP/s) |
| `gunting_light` | 4 damage per snip (2 on a tap, up to 6) | **6 per snip** |
| Hard crowd control | A new stun could land the moment the last one ended | **`cc_immunity` 1 s**: after a stun, airborne, bounce, knock-out or polymorph runs out, a new one is ignored for 1 s (slows never are, damage still lands) |
| Pick screen | Silent for two harmless weapons | Says **"This pair deals no damage"** (D10 stays "allowed") |

## How it was decided
The duel harness (`tools/balance/duels.gd`: all 91 two-weapon loadouts, both sides, bots with a perfect aim, no cover) was re-run after each step. Score is wins
plus half the draws, averaged over every loadout that contains the weapon. The bot aims perfectly, so these are upper bounds for a human.

| Weapon | Before | A: tsinelas 5, gunting 6 | B: + tsinelas cd 8 | **C: + bato 12 (shipped)** |
| --- | --- | --- | --- | --- |
| tsinelas_light | 92% | 88% | 72% | **74%** |
| bato_light | 74% | 75% | 82% | **79%** |
| jacks | 57% | 58% | 62% | 63% |
| bato_heavy / tsinelas_heavy | 56% / 55% | 57% / 56% | 59% / 58% | 59% / 58% |
| gunting_heavy | 46% | 46% | 47% | 46% |
| gunting_light | 38% | 38% | 38% | 38% |
| shields, traps, lata, bola, trumpo | 33 to 42% | same | same | 33 to 41% |

- **Damage per hit did not fix it, cooldown did** (A): `tsinelas_light` still did 5 HP/s against `bato_light`'s 3.5, so it still won.
- **The best duel weapon is always the one with the highest damage per second** (B moved the crown to `bato_light`). C puts the two easy attacks within 5
  points of each other at 74 and 79%, from 92 and 74%. No weapon is over 80%.
- **The shields, traps and crowd controls stay at about 35 to 40% in duels on purpose.** They do little or no damage; their job is a team job (protect a
  base, slow a rusher), which a 1v1 cannot measure. They are not buffed.
- `gunting_light` did not move: the bot taps, which is 2 snips. It is a hold weapon for a human.
- The six loadouts with no damaging weapon still win 0 duels; the warning on the pick screen is the answer, not a block.
- **Crowd-control immunity** is not visible in the duel table (the bots rarely chain). Its tests are in `tests/unit/test_cc_immunity.gd`.
- Side bias stays at 50/50.

## Not decided here (your call)
- `bato_light` is still the easiest damage in the game (it homes) and the top duel weapon at 79%. If new players dominate with it, lower it again (12 to 11)
  or raise its cooldown (4 to 4.5 s).
- `tsinelas_light` at 8 s feels slow on paper; it is the one to try on a phone first.
- The utility weapons need a team-level number: the round harness (`tools/balance/team_rounds.gd`) uses random loadouts and is too coarse to tune one weapon.
  A version with fixed pairs per role (a shield + a hitter in the base) is the next step.

## Rerun
```
godot --headless --import
for i in 0 1 2 3; do godot --headless -s tools/balance/duels.gd -- --out d$i.csv --seeds 1 --shard $i --of 4 --skill 1 & done; wait
python3 tools/balance/report.py d0.csv d1.csv d2.csv d3.csv > report.md
```
