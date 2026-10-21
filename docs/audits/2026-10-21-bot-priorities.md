# Bot priorities in Practice

Owner: bots should prioritise, in this order: **1. tag enemies by killing them, 2. stay at the enemy base, 3. rescue an ally at the tagged box.**

## Before
- A rusher only cast at enemies it passed and kept walking: two rushing teams went through each other (0 tags a round in a 3v3 mirror).
- Any bot (fighters and defenders) dropped a fight to fetch a tagged teammate when the enemy was more than 7 m away, and a rusher left for the box
  the moment a teammate was tagged: rescue came before both fighting and holding the base.

## Now (`FightBrain`, `TeamBrain`)
1. **Kill.** An enemy within `bot_aggro_range` is fought: close to a standoff, strafe, cast. This holds for rushers too, and nothing else (base, rescue) overrides it.
   A defender still fights from inside its base and does not chase out of it.
2. **Stay at the base.** With no enemy in reach an attacker walks into the enemy base and holds its spot; a defender holds its own base.
3. **Rescue.** Only with no enemy in reach, only from its post (an attacker standing in the enemy base, where its team's box is; a defender in its own base), and only
   the lowest-numbered free teammate. Once set off it keeps going until the teammate is free or an enemy comes into reach (no turning back halfway).

The sim already frees a tagged teammate on its own after `practice_tag_release_time`, so a bot that never gets to rescue does not strand anyone.

## Effect (tools/balance/team_rounds.gd, 3v3, 20 seeds, 20 s box release)
| Matchup | Before: points / none, avg round, tags | Now |
| --- | --- | --- |
| rush vs rush | 1 / 19, 17 s, 0.0 | 6 / 14, 50 s, 2.1 |
| rush vs defend | 11 v 0 / 9, 66 s, 2.1 | 10 v 3 / 7, 43 s, 2.5 |
| defend vs rush | 1 v 10 / 9, 68 s, 2.1 | 6 v 6 / 8, 46 s, 2.5 |
| mixed vs mixed | 1 / 19, 83 s, 0.8 | 0 / 20, -, 0.5 |

Rounds now involve fights and the rush/defend matchups are closer to even. Bots are crude (read the direction, not the exact numbers).
