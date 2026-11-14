---
name: godot-unity-sim
description: >
  Port Kalyeah's deterministic 30 Hz gameplay simulation and data resources
  (scripts/sim/*.gd and data/**/*.tres) into a pure engine-free C# Kalyeah.Sim
  assembly and ScriptableObject-backed immutable SimConfig structs for Unity 6.
---

# Godot-to-Unity Migration — Part 1: Deterministic Sim & Data (`godot-unity-sim`)

Owns porting `scripts/sim/*.gd` and `data/{rules,weapons,characters}/**/*.tres` into pure C# simulation logic and Unity data assets.

## Boundary (zero overlap)

- **Owns:** `Kalyeah.Sim` C# assembly, deterministic `30 Hz` `Step`, seeded RNG streams, `PlayerInput` / `PlayerState` / `MatchState` structs, combat/tag/rescue/capture/status rules, `BarangayNav` A* + `TeamCoach` / `TeamBrain` / `FightBrain` bots, and `ScriptableObject` -> `SimConfig` baking.
- **Defers to:** `godot-unity-view` for `.tscn` scenes, URP materials/shaders, `.glb` rigs, Prefabs, and view puppets; `godot-unity-platform` for bootstrap autoloads, WebSocket/Hotspot transport, `EnhancedTouch` input, UTF runner setup, and Android APK builds.

## 1. Pure engine-free `Kalyeah.Sim` assembly

- Place all ported `RefCounted` classes from `scripts/sim/` inside `Kalyeah.Sim.asmdef` with `"noEngineReferences": true` (zero `using UnityEngine;`, zero `MonoBehaviour`, zero Unity physics/colliders).
- Execute the exact same `Kalyeah.Sim.dll` across the Linux headless dedicated server, in-app Hotspot host, offline Practice vs Bots, client prediction, and fast NUnit `EditMode` tests.
- Keep 2D street coordinates explicit (`SimVec2` with `float X, Z` in meters) and resolve cover/curb/corner bounds in pure math (`Collision.Resolve`, `MapLayout.KeepInStreet`) independent of frame rate.

| Godot (`scripts/sim/` & `data/`) | Unity C# (`Kalyeah.Sim` & `Kalyeah.Data`) |
|---|---|
| `class_name Foo extends RefCounted` | `public sealed class Foo` or `struct` in `Kalyeah.Sim` |
| `PlayerInput` bitmask + `move` / `aim_point` | `readonly record struct PlayerInput` (`uint Buttons`, `SimVec2 Move`, `SimVec2 MoveTarget`, `SimVec2 AimPoint`, `int Tick`) |
| `RandomNumberGenerator` (`seed`) | Match-owned deterministic `SimRng` (`ulong` state; isolated `TRIP_SEED_MIX` stream) |
| `Resource` (`GameRules`, `WeaponDef`, `BotLevel`) | `ScriptableObject` authoring asset -> `.ToSimConfig()` readonly C# data struct |

## 2. Deterministic `30 Hz` step & zero per-tick GC

- Advance gameplay exclusively in `MatchSim.Step(ReadOnlySpan<PlayerInput> inputs, float dt = 1f / 30f)` iterating players and entities in deterministic slot order (`0..9`).
- Never call `System.Random.Shared`, `UnityEngine.Random`, `DateTime.UtcNow`, or unordered hash-set/dictionary iteration inside `Kalyeah.Sim`; break `BarangayNav` A* f-score ties deterministically by waypoint index.
- **Zero heap allocations per tick:** preallocate fixed player arrays (`MaxPlayers = 10`), projectile/trap/zone pools, and `BarangayNav` open/closed buffers at match construction.
- Ban LINQ (`Where`, `Select`, `OrderBy`), closures/lambdas, interface-boxed enumerators, and string formatting inside `Step` and `TeamCoach` / `TeamBrain` / `FightBrain` ticks.
- Preserve exact gameplay contracts: identical `player_radius` (`0.45 m`) and `100` HP across all 10 cosmetic kids, `walk_run_threshold` (`0.62`) + `TRIP_SEED_MIX` rolls, `overclocked` / `exhaust_left` independent of status overwrites, NW/SE rescue hold & `rescue_cooldown`, and `no_cast_in_rescue_area` / `no_cast_in_enemy_base`.

## 3. Data-driven migration (`data/*.tres` -> `ScriptableObject` -> `SimConfig`)

- Map every `.tres` schema (`GameRules`, `WeaponDef`, `CharacterDef`, `MapLayout`, `BotLevel`) 1-to-1 into `[CreateAssetMenu]` `ScriptableObject` classes in `Kalyeah.Data` (`Kalyeah.Data` references `Kalyeah.Sim`, never vice versa).
- Keep every balance number from `docs/GDD.md` in `.asset` files — zero magic numbers in C# rules. Preserve data toggles (`card_cycle`, `elixir_enabled`, `stamina_enabled`, `rescue_frees_all`) and Easy/Medium/Hard `BotLevel` presets.
- Bake or convert `ScriptableObject` assets once at match load (or JSON/catalog load on headless server) into immutable `SimConfig` / `WeaponSpec[]` structs passed into `new MatchSim(config, seed)`.
- Pin imported `.asset` invariants in an `EditMode` data test (`elixir_cost` in `1..10`, positive ranges/cooldowns, all 10 `CharacterDef` IDs present with identical hitbox/stats).

## 4. Golden-seed parity before retirement

- Port seam-by-seam: `MapLayout` & `Collision` -> movement/stamina/jump/slide/trip -> `WeaponDef` shapes & statuses -> tag/rescue/capture/powerups -> `BarangayNav` & `TeamCoach`.
- For each seam, write deterministic multi-tick seed-replay NUnit `EditMode` tests verifying exact positions, HP, cooldowns/elixir, tag queues, and capture timers against `docs/GDD.md` and `tests/unit/test_*.gd`.
- Never delete a `scripts/sim/*.gd` file until its C# counterpart passes parity tests and its view/platform consumers are wired.

## Anti-patterns to reject

- Referencing `UnityEngine`, `Time.deltaTime`, `Physics`, or `MonoBehaviour` anywhere inside `Kalyeah.Sim`.
- Mutating `ScriptableObject` fields at runtime instead of reading immutable `SimConfig` structs.
- Allocating `List<T>`, arrays, closures, or strings during `MatchSim.Step` or bot pathfinding.
- Letting character selection (`junjun..tala`) alter hitbox radius, speed, HP, or cast timing.
