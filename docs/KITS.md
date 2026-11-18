# Kalyeah kits (approved by the owner, 2026-11-18)

Rules for every kid (`docs/SPEC.md` §7): 100 HP, same speed and hitbox; the **Tirador** slingshot (7 shots, reload, hold
to aim farther); **3 skills: S1, S2 and an ultimate (ULT)**, all on cooldowns. S1 and S2 also cost **water** (Kalyeah's
mana); the ULT costs no water but has a long cooldown. Every skill is a different mechanic; a street game used twice is
used for two different things. Numbers are draft defaults for `unity/Data/*.json`, tuned later by bot-vs-bot runs.

**Water** works like Mobile Legends / Wild Rift mana (owner): 100 per kid, refills slowly in the street (2 per second)
and very fast in your own base (25 per second, like the fountain); full at every round start; each skill button shows
its water cost.

**Skill damage (owner: control first, light damage):** skills set up tags and the slingshot finishes them, like Valorant.
A damage skill deals about one stone (15 to 25); an ultimate about two (35 to 50). No skill tags a full-HP kid alone.

**Reveals depend on the skill (owner: like Valorant).** Two kinds:
- **Outline:** the enemy glows through walls and tall cover for the whole team and shows on the minimap for the time
  given; the revealed kid sees an eye icon.
- **Snapshot:** a one-time marker of where the enemy was, on the minimap and in the world; it doesn't follow them.

| Skill | Kind | Does the revealed kid know? |
|---|---|---|
| Kikay Teks Tripwire | Outline 6 s | Yes, hears the card snap |
| Kikay Saranggola | Outline while inside the circle; traps shown | Yes, sees the kite overhead |
| Karding Yoyo | Outline 5 s | Yes |
| Karding Piko | Outline while inside the grid | No |
| Karding Pogs Slam | Snapshot | Yes, hears the slam |
| Manoy Sili-Tubig | Outline 4 s | Yes (screen blurs) |
| Manoy Trumpo Bantay | Snapshot every 1 s within 8 m | Yes, hears the top spin |
| Manoy Taya! | Snapshot of every enemy, after the count | Yes, everyone hears the count |

**Shared skill rules (owner):**
- **Hard stops** (stun, knock-up, knock-down, turned into a can) block moving, shooting and skills; a **root** blocks only
  moving.
- **Neutrals:** damage skills hurt the dragon and baron; control and reveals do nothing to them.
- **Ally-only skills** stay ally-only: e.g. only teammates can vault off Paeng's Luksong Baka.

## Strikers — enter fights, burst, get out
| Kid | Skill | What it does | Feels like | Water / CD |
|---|---|---|---|---|
| **Narda** (agile duelist) | S1 **Luksong Tinik** | Vault-dash 6 m, over waist-high cover; 2 charges; the next Tirador shot within 2 s deals +50% | Jett dash, Vayne tumble | 25 / 8 s per charge |
| | S2 **Tira Holens** | A fast marble that ricochets once off a wall; a ricochet hit deals double | Ezreal Q trick shot | 30 / 9 s |
| | ULT **Tsinelas Bagsik** | 3 boomerang slippers over 2 s, each out and back; tagging an enemy hit by one refunds half the cooldown | Jett Blade Storm, Katarina reset | — / 80 s |
| **Jumong** (diver) | S1 **Syato** | Flick a stick: the first enemy hit pops up 0.5 s; recast within 2 s to dash to them and whack | Lee Sin Q | 30 / 10 s |
| | S2 **Trumpo Ikot** | Spin as a top for 2 s: damage around Jumong, immune to slows | Garen E, Darius | 30 / 11 s |
| | ULT **Pukpok Palayok** | Leap up to 10 m and smash a pot: area damage, and enemies hit are "blindfolded" (screen blurred) 2 s | Malphite ULT, Amumu | — / 90 s |
| **Inggo** (sharpshooter) | S1 **Sipa** | Kick a rattan sipa that bounces between up to 3 enemies | Miss Fortune Q bounce | 25 / 8 s |
| | S2 **Tiyakad** | On stilts for 4 s: sees over cover, +40% Tirador range, moves 20% slower | Tower-stance sniping | 35 / 14 s |
| | ULT **Bato!** (bato-bato-pik) | One long-range stone, 1 s wind-up: damage grows with the target's missing HP; tagging with it heals Inggo 30 | Jhin last shot, Garen execute | — / 85 s |

## Defenders — hold the base, keep teammates alive
| Kid | Skill | What it does | Feels like | Water / CD |
|---|---|---|---|---|
| **Jopay** (builder) | S1 **Bahay-Bahayan** | Appliance-box wall, 6 m wide, has HP, blocks shots and movement | Sage wall | 35 / 16 s |
| | S2 **Pabitin** | Hang a treat rack: allies under it heal over 4 s | Soraka / Sage heal area | 35 / 14 s |
| | ULT **Palosebo** | A greased pole in your base for 8 s: enemies can't advance a capture, and slip (slowed) near it | Killjoy Lockdown (defensive) | — / 90 s |
| **Oriang** (guardian) | S1 **Chinese Garter** | Tether an ally up to 12 m: 30% of the damage they take goes to Oriang; stretching it too far snaps it and speeds both | Taric / Karma link | 25 / 10 s |
| | S2 **Sungka** | For 3 s Oriang stores damage taken as shells, then releases them as a shield shared by nearby allies | Braum / Galio absorb | 30 / 12 s |
| | ULT **Langit!** (langit-lupa) | Allies in 8 m can't drop below 1 HP (can't be tagged) for 3 s | Kayle / Taric ULT | — / 100 s |
| **Paeng** (frontline) | S1 **Sack Race** | Hop in a sack 3 s: 40% less damage; bumping an enemy knocks them back | Rammus Powerball | 30 / 12 s |
| | S2 **Luksong Baka** | Crouch as the "baka" for 4 s: an ally who vaults off Paeng leaps 10 m (rescue runs, escapes) | Bard tunnel (team mobility) | 20 / 14 s |
| | ULT **Tinik Wall** (luksong tinik) | A row of "tinik" hands across a path for 4 s blocks enemies; enemies on it when it rises are knocked up | Anivia wall, Trundle pillar | — / 85 s |

## Seekers — find enemies and their traps
| Kid | Skill | What it does | Feels like | Water / CD |
|---|---|---|---|---|
| **Kikay** (trapper scout) | S1 **Teks Tripwire** | Drop a teks card; the next enemy who crosses it is revealed 6 s and slowed 20% | Cypher wire, Teemo shroom | 20 / 10 s |
| | S2 **Tago!** (tagu-taguan) | Invisible for 4 s (breaks on attack); the first shot after deals +40% | Akali shroud, Twitch | 35 / 16 s |
| | ULT **Saranggola** | Fly a kite over an area she aims: reveals enemies and traps in a 15 m circle for 6 s | Sova Owl Drone | — / 80 s |
| **Karding** (tracker) | S1 **Yoyo** | Yoyo shot: marks the enemy hit (revealed 5 s) and tugs them 2 m | Blitzcrank hook (light) | 25 / 10 s |
| | S2 **Piko** | Draw a chalk grid: enemy traps inside are revealed and disarmed; enemies inside are visible to the team | Vision ward + sweeper | 30 / 14 s |
| | ULT **Pogs Slam** (teks at pogs) | Slam a giant pog: destroys enemy traps and walls in 6 m, reveals enemies in 14 m for 4 s | Fade Nightfall | — / 85 s |
| **Manoy** (hunter) | S1 **Sili-Tubig** | Chili-water spray cone: enemies hit are revealed 4 s and their screen blurs 1.5 s | Skye / Fade flash-reveal | 30 / 11 s |
| | S2 **Trumpo Bantay** | A spinning top sentry for 20 s: pings enemies within 8 m | Killjoy Alarmbot, ward | 20 / 15 s |
| | ULT **Taya!** (tagu-taguan) | Counts to 3 out loud, then reveals every enemy on the map for 3 s | Raven-style map reveal | — / 100 s |

## Controllers — slow, root and split fights
| Kid | Skill | What it does | Feels like | Water / CD |
|---|---|---|---|---|
| **Tere** (zoner) | S1 **Jackstone** | Scatter jacks in a circle for 5 s: slow + light damage | Caltrops, Viper zone | 25 / 10 s |
| | S2 **Luksong Lubid** | Stretch a rope between two points: the first enemy across is tripped (knocked down 1 s) | Cypher tripwire, Jinx chompers | 30 / 13 s |
| | ULT **Garter Net** (chinese garter) | A 6 m net: enemies inside are rooted 1.25 s, then slowed 2 s | Morgana / Neeko ULT | — / 90 s |
| **Pamela** (disruptor) | S1 **Holens Spill** | Spill marbles in a cone for 3 s: enemies slip (no jump, no sprint, slowed) | Nami bubble zone | 25 / 10 s |
| | S2 **Lata!** (tumbang preso) | Throw a can: the first enemy hit becomes a can for 1.5 s (can't attack or use skills) | Lulu Whimsy | 35 / 15 s |
| | ULT **Patintero!** | Draw 3 chalk lines for 5 s: an enemy crossing a line is stunned 0.8 s (once per line) | Veigar Event Horizon | — / 90 s |
| **Domeng** (initiator) | S1 **Pabitin Snare** | After 0.5 s a rack drops on an area: enemies under it are rooted 1.2 s | Lux / Morgana root | 30 / 11 s |
| | S2 **Sako Trap** (sack race) | Hidden sack trap: the enemy who steps in can only hop (50% slower) for 3 s | Caitlyn trap | 20 / 12 s |
| | ULT **Lupa!** (langit-lupa) | After 0.6 s, every enemy **on the ground** within 8 m is knocked up and stunned 1 s; enemies on raised spots are safe | Malphite / Amumu ULT | — / 95 s |

## Coverage
Used once or more: bato-bato-pik, tumbang preso (tsinelas, lata), jackstone, trumpo (spin, sentry), langit-lupa (Langit,
Lupa), holens (marble shot, spill), sili-tubig, sipa, luksong tinik (dash, wall), luksong baka, luksong lubid,
saranggola, teks (tripwire), pogs, piko, tagu-taguan (stealth, Taya), bahay-bahayan, syato, yoyo, palosebo, pukpok
palayok, sack race (sack, trap), pabitin (heal, snare), chinese garter (tether, net), sungka, tiyakad, patintero.
Not used yet: **kariling** (rolling an old wheel rim along with a stick): saved for the first kid added after launch.
