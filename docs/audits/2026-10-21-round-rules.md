# Round rules (P1 of the lead review)

From [the review](2026-10-21-lead-review.md): a 5 s capture was shorter than the time it takes to tag anyone, team 0 won every tie, and a base could not
be defended. This changes the rules, the bots that test them, and records what the numbers said.

## What changed
- **Tie-break fixed.** If both teams qualify at once, nobody's countdown runs (`MatchObjectives.check_captures`). Tests: `tests/unit/test_captures.gd`.
- **Contested base** (`capture_contested`, on): a base capture needs the defending team to have no free player standing in that base. HUD: *CONTESTED!*.
- **Overtime** (`overtime_after`, `overtime_capture_time`, off) and **timed release from the box** (`tag_release_time` off, `practice_tag_release_time` 20 s).
  The capture time the clients need travels in the snapshot (`capture_needed`). Tests: `tests/unit/test_round_pace.gd`.
- **`TeamBrain`** (`scripts/sim/team_brain.gd`): a rusher walks into the enemy base, casts on the way and the lowest-numbered free rusher fetches a tagged
  teammate; a defender guards its base and may leave it to rescue. **`tools/balance/team_rounds.gd`** plays whole rounds with them.
- Small test hardening: the Home carousel size check no longer depends on the card's idle bob.

## What the harness said (3v3, bots, 30 to 60 seeds per cell, first point or 90 to 120 s)
Before the fix (review table): mirror rush, team 0 won 76 of 80; rusher beat defender 70 of 80.

After the fix, with contested bases:

| matchup (team 0 vs team 1) | capture 5 s | capture 8 s | capture 12 s |
| --- | --- | --- | --- |
| rush vs rush | 2 / 4, 54 of 60 stalled | 0 / 1, 59 stalled | 0 / 1, 59 stalled |
| rush vs guard (defender holds the base) | 29 / 1, 10 stalled | 29 / 1, 10 stalled | 29 / 1, 10 stalled |
| guard vs rush | 1 / 34 | 1 / 33 | 1 / 33 |

- **The bias is gone** (a mirror rush no longer scores for team 0; it stalls until someone is tagged).
- **The capture time barely matters any more.** With contested bases the rusher has to tag every defender, which takes far longer than 5 s, so 5, 8 and 12 s
  give the same wins. That is why `capture_time` stays 5 s (D18).
- **Rushers still win about 95% of decided rounds against a guard, and the reason is the map, not the capture time.** Each team's box is at the *enemy's*
  end of the street. A rusher who is tagged lands in a box beside teammates already there; a defender tagged at home is sent 48 m across the street and
  its rescuers have to follow. Dmg dealt was about equal both ways in the probes, the tags were not. This is D25 in the GDD: the owner's design from
  the reference video, left unchanged, but it is the biggest remaining lever on who wins a round.
- **A timed release evens it out and costs pace.** Releasing a tagged kid after 12 s: rush vs guard drops from 24 decided of 30 to 6 of 30 (rounds stall);
  20 s: 12 of 30. In a real game both teams attack, so stalls are not the same as in bot rounds, but it is a reason to keep it off online until it is played.

Limits: the bots are crude (no cover, one fixed rescue rule, defenders never attack), so read the direction and the size of a gap.

## Rerun
```
godot --headless --import
godot --headless -s tools/balance/team_rounds.gd -- --seeds 30 --seconds 90 --size 3 --capture 5,8 --release 0,12,20 --only rush-defend,mixed-mixed
```
