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

## Results
See below (filled in after measuring).
