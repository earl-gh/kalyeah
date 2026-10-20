# Balance duels: the first weapon numbers

From [the audit](2026-10-20-next-audit-3.md): the weapons had never been compared by playing, so D6, D10, D14 and D15
were guesses. This is the first data, from [the sparring bot](2026-10-20-practice-bots.md) playing itself.

**What it is.** Every one of the 91 two-weapon loadouts plays every other, 1v1, on the street *without its cover*,
starting 10 m apart. First to be tagged loses; nobody tagged in 60 s is a draw. Each pairing is played with 2 seeds and
both sides, so 4 duels per pairing, 16 380 per run. A run takes about nine minutes on four cores. It was run twice:
with a **simple aim** (the bot throws at where you are, with 10 degrees of error) and with a **good aim** (it leads a
moving target and its error is a third as big). The truth about a human is somewhere between.

**What it is not.** Both players are the same bot. It never uses cover on purpose and uses a trap, a field or a shield
only by a fixed rule. Nothing here changed a weapon number, and nothing should until you have played the ones that stand out.

## A correction

The first version of this page said the heavy weapons were weak because the bot "does not predict". **That was my bug.**
A ground circle, trap or field lands at `aim * max_range`, so the aim's *length* is the distance, and the bot always
aimed at full length: it threw its rock past you. Fixed (and tested), `bato_heavy` went from 14% wins to 34%, and the
heavies are now in the middle of the table where they belong. The 40% draw rate fell to 35% for the same reason.

## Results

Run 1, simple aim: 16380 duels between 91 loadouts; 35% were draws (nobody tagged in time); the average duel lasted 42.6 s.  
Run 2, good aim: 16380 duels between 91 loadouts; 27% were draws (nobody tagged in time); the average duel lasted 37.0 s.

### By weapon

Average over every loadout that contains the weapon (13 each). Score = wins plus half the draws. Sorted by the two
scores together.

| Weapon | Kind | Damage | Cooldown | Score, simple aim | Score, good aim | Wins, simple | Wins, good |
| --- | --- | --- | --- | --- | --- | --- | --- |
| tsinelas_light | Attack | 8 | 6 s | 75% | 92% | 70% | 91% |
| bato_light | Attack | 14 | 4 s | 82% | 74% | 78% | 71% |
| jacks | CC | 3 | 11 s | 64% | 57% | 56% | 50% |
| heal | Heal | 0 | 12 s | 57% | 54% | 27% | 28% |
| bato_heavy | Attack | 22 | 12 s | 51% | 56% | 34% | 48% |
| tsinelas_heavy | Attack | 18 | 10 s | 49% | 55% | 32% | 43% |
| gunting_heavy | Attack | 24 | 9 s | 48% | 46% | 34% | 36% |
| bola | CC | 6 | 12 s | 40% | 42% | 13% | 20% |
| lata | CC | 5 | 14 s | 42% | 40% | 21% | 24% |
| trumpo | CC | 10 | 15 s | 39% | 40% | 15% | 22% |
| gunting_light | Attack | 4 | 5 s | 39% | 38% | 20% | 22% |
| langit_lupa | Block | 0 | 14 s | 39% | 36% | 18% | 20% |
| papel_shield | Block | 0 | 14 s | 38% | 36% | 18% | 20% |
| papel_trap | CC | 0 | 12 s | 37% | 35% | 16% | 18% |

### Best and worst loadouts (good aim)

| Loadout | Score | Wins | Draws |
| --- | --- | --- | --- |
| tsinelas_light + jacks | 97% | 96% | 1% |
| bato_heavy + tsinelas_light | 96% | 96% | 0% |
| tsinelas_light + tsinelas_heavy | 96% | 95% | 1% |
| bato_light + tsinelas_light | 93% | 93% | 0% |
| tsinelas_light + heal | 92% | 91% | 3% |
| tsinelas_light + langit_lupa | 92% | 92% | 0% |
| tsinelas_light + lata | 92% | 92% | 0% |
| papel_shield + tsinelas_light | 91% | 91% | 0% |
| tsinelas_light + trumpo | 90% | 90% | 1% |
| bato_light + bato_heavy | 89% | 89% | 0% |
| ... | | | |
| lata + langit_lupa | 21% | 0% | 41% |
| papel_shield + lata | 20% | 0% | 40% |
| papel_trap + lata | 19% | 0% | 38% |
| papel_trap + langit_lupa | 19% | 0% | 37% |
| gunting_light + papel_trap | 18% | 0% | 37% |
| papel_trap + papel_shield | 18% | 0% | 37% |
| gunting_light + lata | 18% | 0% | 36% |
| papel_shield + langit_lupa | 17% | 0% | 34% |
| gunting_light + langit_lupa | 16% | 0% | 31% |
| gunting_light + papel_shield | 15% | 0% | 31% |

### Loadouts with no damaging weapon (good aim) (6)

| Loadout | Score | Wins | Draws |
| --- | --- | --- | --- |
| heal + langit_lupa | 40% | 0% | 79% |
| papel_shield + heal | 38% | 0% | 77% |
| papel_trap + heal | 37% | 0% | 75% |
| papel_trap + langit_lupa | 19% | 0% | 37% |
| papel_trap + papel_shield | 18% | 0% | 37% |
| papel_shield + langit_lupa | 17% | 0% | 34% |


## What stands out (and what I would not conclude)

1. **`tsinelas_light` is the one to watch.** 75% with a simple aim, **92% with a good one**, and 9 of the 10 best
   loadouts contain it. It throws three boomerangs that each hit on the way out and back (8 damage a hit, up to 48 a
   cast, 6 s cooldown), and a bot that leads its target lands nearly all of them. A human has to aim it (it does not
   home), so the good-aim column is an upper bound, but it is the weapon whose strength depends most on skill.
2. **`bato_light` is strong at any skill** (82% and 74%): a homing throw, 14 damage every 4 s, that cannot miss.
   That is the easiest damage in the game to land, which is what a new player will pick.
3. **The heavies are fair.** `bato_heavy` 51% and 56%, `tsinelas_heavy` 49% and 55%, `gunting_heavy` 48% and 46%:
   the middle of the table at both skills. Nothing to change.
4. **The bottom is the Blocks, the traps and `gunting_light`** (35 to 39% at both skills). `gunting_light` (4 damage,
   a 3 m cone) loses ranged duels, and the shields and traps do no damage. In 1v1 that is expected; in a team they have a
   job that a duel does not measure.
5. **Loadouts with no damaging weapon never win a duel** (6 loadouts, 0 wins in all of them, at both skills). That
   settles what D10 asked, but a team can still use a healer or a shield, so the default stays "allowed"; the pick
   screen could say so. Your call.
6. **Heal (D14) is only strong next to a weapon that hurts.** Alone it has 27% wins and about half its duels end as
   draws. In 1v1 it is a self-heal (30 HP per 12 s); the team game, where it heals a teammate, is not measured here.
7. **D6 is out of date.** The GDD said only one Block weapon exists; there are now two (`papel_shield` and
   `langit_lupa`), and they score the same as each other, near the bottom.
8. **No side bias** (each team won 49.9 to 50.1% of decided duels in both runs).
9. **Cover.** The bot now never throws a projectile into cover and steps aside to find a clear line (unit-tested with a
   wall between two players), but the duel start points at the middle of the street had no cover in the way, so a
   run with the street's cover gave the same numbers. No claim about cover is made here.

## Rerun it

```
godot --headless --import
godot --headless -s tools/balance/duels.gd -- --out duels0.csv --seeds 2 --shard 0 --of 4 --skill 1   # shards 0..3, in parallel
python3 tools/balance/report.py duels0.csv duels1.csv duels2.csv duels3.csv > report.md
```

`--skill` is how well both bots aim (0 to 1), `--cover 1` keeps the street's cover, `--seconds` is the time limit and
`--seeds` the number of seeds. The duel itself is `BalanceDuel` (`scripts/sim/balance_duel.gd`), tested in
`tests/unit/test_balance_duel.gd`.

## Next, if you want it

- **Your own read.** Play `tsinelas_light + jacks` and `bato_light + tsinelas_light` against the new sparring partner
  and say whether they feel too strong; that is worth more than this table.
- **2v2 and 3v3 duels with the capture rules**, where Heal and the shields are worth more than in 1v1.
- **Duel start points that put cover in the way**, to measure how much cover changes a weapon.
