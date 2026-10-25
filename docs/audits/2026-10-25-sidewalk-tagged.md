# Tagged kids on the sidewalk (2026-10-25)

Owner: tagged players wait outside the play area, on the sidewalk, facing in, level with where their box was; the rescue area loses its inner box and sits against the play area's side edge; thinner outlines for the rescue area and the bases.

## Hypothesis (player terms)
Moving the rescue spot from "round a box 3 m in from the edge" to "a strip against the edge" should not change how often a tagged kid is freed or how long a round lasts: the same distance along the street, 3 m less across. If a rescuer has to stand further from the fight, rescues get rarer and rounds stall; if the zone is easier to reach, rescues get cheaper and rushing gets stronger.

## Thresholds (written before measuring)
Run: `tools/balance/team_rounds.gd -- --seeds 60 --size 3 --seconds 90 --only rush-defend,mixed-mixed`, same seeds (1000..1059) on `main` and on the branch. Bots are crude, so only these gaps count:

| Metric | Pass if |
|---|---|
| Rounds with no point (`none`) | at most 10 points of 60 higher than main (rounded up to 6 rounds), per matchup |
| Seconds to a point (`avg`) | within +-25% of main |
| Rescues per round | at least 60% of main's, and above 0 in both matchups (bots still rescue) |
| Rush vs defend: the rusher's share of decided rounds | within +-15 points of main (rushing does not become dominant) |
| Mixed vs mixed: team 0's share of decided rounds | within +-15 points of main (no side bias) |

If a threshold fails: change the zone size or place in `data/rules/map_layout.tres` once and re-measure; no new rule.

## Change measured
Rescue area: one 3 x 6 m rectangle (`rescue_size`) against the street's side edge, its outer end level with the other team's base as before (the old area was 4 x 6.8 m, 3 m in from the edge). Tagged kids on the sidewalk, three places, 2 m apart along the area. Bots keep their rule (walk to the area's centre, stay for `rescue_hold_time`).

## Results (60 seeds 1000..1059, 3 v 3, 90 s, `--only rush-defend,mixed-mixed`)
| Matchup | | main | branch |
|---|---|---|---|
| rush vs defend | team 0 / team 1 / none | 31 / 29 / 0 | 31 / 29 / 0 |
| | seconds to a point | 45.0 | 45.0 |
| | tags per round | 3.5 | 3.5 |
| | rescues per round | 0.13 | 0.10 |
| mixed vs mixed | team 0 / team 1 / none | 0 / 0 / 60 | 0 / 0 / 60 |
| | tags per round | 0.6 | 0.7 |
| | rescues per round | 1.42 | 1.33 |

Thresholds: none per matchup unchanged (pass); seconds to a point unchanged (pass); rescues per round 77% and 94% of main, above 0 (pass); rusher's share of decided rounds 31 of 60 on both (pass); mixed vs mixed has no decided rounds on either side, so the side-bias check has nothing to read (it is the same 60 of 60 stall as on main: the bots' mixed plan never settles a round in 90 s, which this change neither causes nor cures).

Verdict: no number moved past noise, so the zone's size and place stay as first set (no second adjustment needed).
