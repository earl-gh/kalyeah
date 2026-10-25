# Rounded street corners and posts in the corner (2026-10-25)

Owner: "base poles must be placed at corner edge of the map. the map area should have same radius to match the radius of capture base area."

## Hypothesis (player terms)
Rounding the street's four corners with the base's own corner radius (`base_corner`, 1 m) takes about 0.2 m2 off each corner (the base area is unchanged: its outer corner was already rounded and now lies exactly on the street's edge). A kid can no longer stand in the sliver, and a kid pushing into a corner slides along the curve instead of stopping square. That should not change how long a round takes, how often a base is held or how often a tagged kid is freed. If bots get caught in a corner, rounds stall (more rounds with no point) and the spawn / base-holding bots stop reaching the base. The posts move to the corner and are not part of the sim: bots still walk to the base's centre.

## Thresholds (written before measuring)
Run: `tools/balance/team_rounds.gd -- --seeds 60 --size 3 --seconds 90 --only rush-defend,mixed-mixed`, seeds 1000..1059, on `feat/corner-bases` at its start (HEAD 8dcd907) and after the change.

| Metric | Pass if |
|---|---|
| Rounds with no point (`none`) | at most 6 rounds (10 points of 60) higher than before, per matchup |
| Seconds to a point (`avg`) | within +-25% of before |
| Rescues per round | at least 60% of before, and above 0 in both matchups |
| Rush vs defend: the rusher's share of decided rounds | within +-15 points of before |
| Mixed vs mixed: team 0's share of decided rounds | within +-15 points of before (if a matchup has no decided round on either run it has nothing to read) |

If a threshold fails: change `base_corner` or the post's place in `data/rules/map_layout.tres` once and re-measure; no new rule.

## Results (60 seeds 1000..1059, 3 v 3, 90 s, `--only rush-defend,mixed-mixed`)
| Matchup | | before (8dcd907) | after |
|---|---|---|---|
| rush vs defend | team 0 / team 1 / none | 31 / 29 / 0 | 31 / 29 / 0 |
| | seconds to a point | 45.0 | 45.0 |
| | tags per round | 3.5 | 3.5 |
| | rescues per round | 0.10 | 0.10 |
| mixed vs mixed | team 0 / team 1 / none | 0 / 0 / 60 | 0 / 0 / 60 |
| | tags per round | 0.7 | 0.7 |
| | rescues per round | 1.33 | 1.27 |

Thresholds: none per matchup unchanged (pass); seconds to a point unchanged (pass); rescues per round 100% and 95% of before, above 0 (pass); the rusher's share of decided rounds 31 of 60 on both (pass); mixed vs mixed has no decided round on either run (the same 60 of 60 stall as before: the bots' mixed plan never settles a round in 90 s, which this change neither causes nor cures), so its side-bias check has nothing to read.

Verdict: no number moved past noise; `base_corner` and the post's place stay as first set.
