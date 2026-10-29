# Practice bots: rescues, timing, jobs and targets (2026-10-29)

Measured with `tools/balance/practice_bots.gd` (40 seeds, 180 s each, both teams the real `TeamCoach` + `TeamBrain`; your kid is a
stand-in bot that either always attacks, `--you rush`, or fights from its own base, `--you home`). Before = `main` at `efd73ef`
(after the rescue countdown, #322). After = this change with the shipped data.

## What was wrong

- **No bot ever rescued anyone: 0 rescues per match, both teams, both stand-ins.** The rescuer was the lowest-numbered free
  teammate, which on your team is you (id 1), so your allies never went; and any enemy within `bot_aggro_range` (14 m) called a rescue
  off, which on a 24 x 48 m street is nearly always. Once in the rescue area a rescuer that saw an enemy walked out to fight, though
  nothing can be cast there while a teammate waits.
- The enemies stood still for the first 25 s of every round (`enemy_start` 24.7 s) and rested 25 s between waves.

## Results

| | rush before | rush after | home before | home after |
|---|---|---|---|---|
| Enemies leave their base after | 24.7 s | **3.6 s** | 25.5 s | **3.6 s** |
| Rescues per match, your team | 0.00 | **0.78** | 0.00 | 0.05 |
| Rescues per match, enemies | 0.00 | **1.73** | 0.00 | **3.02** |
| A tagged ally waits | 18.2 s | 16.6 s | 13.7 s | 14.3 s |
| Bot standing in a counting rescue area | 0.0 s | 7.0 s | 0.0 s | 5.6 s |
| Base taken with nobody home | 0.2 s | 2.5 s | 0.0 s | 1.0 s |
| Points per match, yours : theirs | 1.93 : 0.82 | 0.80 : 1.50 | 0.90 : 1.25 | 0.20 : 2.65 |
| Matches won : lost (of 40) | 8 : 0 | 1 : 1 | 0 : 0 | 0 : 29 |

The same new brains with the old 25 s timing (`--set bot_assault_delay=25,bot_rest_time=25`): rush 2.15 : 0.25 points, 2.75 rescues
per match for your team; home 0.15 : 1.65. The old brains with the new timing: rush 1.10 : 0.93, home 1.15 : 1.80 (lost 9 of 40).

`bot_rescue_aggro_range` tried at 2.5, 4 and 6 m: your team's rescues 0.88 / 0.75 / 0.33 per match (rush), so 4 m ships.

## Reading it

- Rescues now happen, on both teams, which is what the rules are built around.
- **The enemies are clearly harder** with the new timing: they come out at once, attack often, and now free each other. A player who
  stays home loses most matches. Part of that is the map: your team's rescue area is in the enemy half (the rescuer crosses the whole
  street past their defenders) while theirs is in yours, where your defenders hold the base and do not chase. A real player who
  attacks does better than the stand-in, which aims like a bot (`bot_skill` 0.4).
- Levers if Practice should be easier: `bot_rest_time` (longer rests), `bot_assault_delay`, or a lower `bot_skill` for the enemy team.

## Follow-up: the allies come out too

Your allies only attacked while you were in the enemy half, so with you at home they first left their base after 41.2 s on average
(`ally_start`). They now follow the same clock as the enemies (out after `bot_assault_delay`, waves), and still attack whenever you
are in the enemy half.

| | rush before | rush after | home before | home after |
|---|---|---|---|---|
| Allies leave their base after | 4.8 s | **3.6 s** | 41.2 s | **3.6 s** |
| Rescues per match, yours : theirs | 0.78 : 1.73 | **2.20 : 2.05** | 0.05 : 3.02 | 0.40 : 1.62 |
| Points per match, yours : theirs | 0.80 : 1.50 | **1.35 : 0.62** | 0.20 : 2.65 | 0.25 : 1.70 |
| Matches won : lost (of 40) | 1 : 1 | 4 : 0 | 0 : 29 | **0 : 3** |

This also takes back most of the extra difficulty above: the enemies no longer face a team that only defends.
