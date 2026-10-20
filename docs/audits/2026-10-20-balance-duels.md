# Balance duels: the first weapon numbers

From [the audit](2026-10-20-next-audit-3.md): the weapons had never been compared by playing, so D6, D10, D14 and D15
were guesses. This is the first data, from [the sparring bot](2026-10-20-practice-bots.md) playing itself.

**What it is.** Every one of the 91 two-weapon loadouts plays every other, 1v1, on the street *without its cover*,
starting 10 m apart. First to be tagged loses; nobody tagged in 60 s is a draw. Each pairing is played with 2 seeds and
both sides, so 4 duels per pairing, 16 380 in all. It runs headless in about nine minutes on four cores.

**What it is not.** Both players are the same simple bot. It never leads a moving target, never uses cover, and uses a
trap, a field or a shield only by a fixed rule. A homing weapon (it cannot miss) is therefore flattered, and a weapon that
rewards aim or prediction is punished. It says how a weapon does in the hands of a simple player, **not how strong it is**.
Nothing here changed a weapon number, and nothing should until you have played the ones that stand out.

## Results

16380 duels between 91 loadouts; 40% were draws (nobody tagged in time); the average duel lasted 44.2 s.
Side bias: team 0 won 50.0% of the decided duels, team 1 50.0%.

### By weapon

Average over every loadout that contains it (13 loadouts each). Score = wins + half the draws.

| Weapon | Kind | Damage | Cooldown | Score | Wins | Draws |
| --- | --- | --- | --- | --- | --- | --- |
| bato_light | Attack | 14 | 4 s | 83% | 78% | 9% |
| tsinelas_light | Attack | 8 | 6 s | 75% | 70% | 10% |
| jacks | CC | 3 | 11 s | 61% | 50% | 22% |
| heal | Heal | 0 | 12 s | 58% | 27% | 63% |
| gunting_heavy | Attack | 24 | 9 s | 50% | 35% | 31% |
| tsinelas_heavy | Attack | 18 | 10 s | 48% | 29% | 38% |
| gunting_light | Attack | 4 | 5 s | 42% | 21% | 44% |
| langit_lupa | Block | 0 | 14 s | 42% | 19% | 46% |
| papel_shield | Block | 0 | 14 s | 41% | 19% | 46% |
| bola | CC | 6 | 12 s | 40% | 12% | 57% |
| trumpo | CC | 10 | 15 s | 40% | 14% | 52% |
| lata | CC | 5 | 14 s | 40% | 17% | 47% |
| bato_heavy | Attack | 22 | 12 s | 39% | 14% | 49% |
| papel_trap | CC | 0 | 12 s | 39% | 16% | 46% |

### Best and worst loadouts

| Loadout | Score | Wins | Draws |
| --- | --- | --- | --- |
| bato_light + tsinelas_heavy | 96% | 96% | 0% |
| bato_light + jacks | 95% | 93% | 3% |
| bato_light + tsinelas_light | 92% | 92% | 0% |
| tsinelas_light + jacks | 92% | 91% | 2% |
| tsinelas_light + heal | 89% | 84% | 11% |
| bato_light + heal | 89% | 83% | 12% |
| jacks + heal | 84% | 74% | 18% |
| gunting_heavy + heal | 81% | 74% | 15% |
| bato_light + trumpo | 81% | 77% | 7% |
| bato_light + gunting_heavy | 79% | 74% | 10% |
| ... | | | |
| bato_heavy + papel_trap | 28% | 0% | 56% |
| gunting_heavy + papel_trap | 28% | 4% | 49% |
| gunting_light + papel_shield | 28% | 0% | 56% |
| gunting_light + lata | 28% | 0% | 56% |
| papel_trap + lata | 28% | 0% | 56% |
| lata + langit_lupa | 27% | 0% | 54% |
| papel_shield + lata | 27% | 0% | 54% |
| papel_trap + langit_lupa | 26% | 0% | 52% |
| gunting_light + papel_trap | 25% | 0% | 49% |
| papel_trap + papel_shield | 24% | 0% | 49% |

### Loadouts with no damaging weapon (6)

| Loadout | Score | Wins | Draws |
| --- | --- | --- | --- |
| heal + langit_lupa | 47% | 0% | 94% |
| papel_shield + heal | 47% | 0% | 93% |
| papel_trap + heal | 44% | 0% | 87% |
| papel_shield + langit_lupa | 31% | 0% | 62% |
| papel_trap + langit_lupa | 26% | 0% | 52% |
| papel_trap + papel_shield | 24% | 0% | 49% |


## What stands out (and what I would not conclude)

1. **`bato_light` and `tsinelas_light` win most of their duels** (83% and 75% score; all 13 loadouts with `bato_light`
   rank in the top 18 of 91). `bato_light` is a homing throw (14 damage, 4 s cooldown, 7 m): it cannot miss, so the
   bot uses it perfectly. A human will not, but the same rule makes it the easiest damage in the game to land. Worth
   a look when you play: is it too strong, or are the heavy ones too weak?
2. **The heavy versions score lower than the light ones** (`bato_heavy` 39%, `tsinelas_heavy` 48%, `gunting_heavy` 50%) though they
   hit harder per cast. They are ground circles and delayed cones that need the target to stand still; the bot does not
   predict. This is the clearest case of the bot, not the weapon, being the limit: do not nerf or buff them from this table.
3. **Loadouts with no damaging weapon never win a duel** (6 loadouts, 0 wins in all of them). That settles what D10
   asked ("consider requiring at least one damaging weapon at pick time"): alone they cannot score, and in a team
   they only help if a teammate does the damage. Default stays "allowed" (a team game can use a healer), but the
   pick screen could say so. Your call.
4. **Heal (D14) is only strong next to a weapon that hurts.** Alone it has 27% wins and 63% draws. Paired with
   `bato_light`, `tsinelas_light`, `jacks` or `gunting_heavy` it scores 81 to 89%, but paired with the other nine
   it scores 43 to 54%, which is the middle of the table. So it is not a clear outlier; in 1v1 it is a self-heal
   (30 HP per 12 s), and the team game, where it heals a teammate, is not measured here.
5. **D6 is out of date.** The GDD says only one Block weapon exists; there are now two (`papel_shield` and
   `langit_lupa`), and they score about the same (41% and 42%, in the lower half but not at the bottom).
6. **Draws are common (40%)**, mostly between blocks, traps, heals and slow-and-wait weapons. A real match is not
   1v1 with a 60 s limit, so I would not read a draw as a flaw.
7. **No side bias** (team 0 and team 1 each won 50.0% of decided duels), which is what you want from a test of this kind.

## Rerun it

```
godot --headless --import
godot --headless -s tools/balance/duels.gd -- --out duels.csv --seeds 2 --shard 0 --of 4   # shards 0..3, in parallel
python3 tools/balance/report.py duels0.csv duels1.csv duels2.csv duels3.csv > report.md
```

`--seconds` changes the time limit and `--seeds` the number of seeds. The duel itself is `BalanceDuel`
(`scripts/sim/balance_duel.gd`), tested in `tests/unit/test_balance_duel.gd`.

## Next, if you want it

- **Bots that lead a target and take cover**, so the heavy weapons are not judged by a bot that cannot aim them.
- **2v2 and 3v3 duels with the capture rules**, where Heal and the shields are worth more than in 1v1.
- **Your own read.** Play `bato_light + tsinelas_light` against the new sparring partner and say whether it feels too
  strong; that is worth more than this table.
