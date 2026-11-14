---
name: godot-unity-sim
description: >
  Port Kalyeah's deterministic 30 Hz gameplay simulation and data resources
  (scripts/sim/*.gd and data/**/*.tres) into a pure engine-free C# Kalyeah.Sim
  assembly and automated ScriptableObject/JSON SimConfig pipelines for Unity 6.
---

# Godot-to-Unity Migration — Part 1: Deterministic Sim & Data (`godot-unity-sim`)

Owns translating `scripts/sim/*.gd` and `data/{rules,weapons,characters}/**/*.tres` into pure C# simulation code and automated Unity/server data catalogs.

## Boundary (zero overlap)

- **Owns:** `Kalyeah.Sim` pure C# assembly, deterministic `30 Hz` `MatchSim.Step`, GDScript-to-C# math/float determinism, seeded `SimRng` streams, combat/tag/rescue/capture/status/`BarangayNav` bot porting, and automated `.tres` -> `ScriptableObject`/JSON `SimConfig` conversion.
- **Defers to:** `godot-unity-view` (Prefabs, URP shaders, `.glb` rigs, view puppets); `godot-unity-platform` (Bootstrap, WebSocket/Hotspot transport, `EnhancedTouch`, APK/Linux builds); `apex-net-frame` (match frame-time, reconciliation, and memory budgets).

## 1. Pure engine-free `Kalyeah.Sim` assembly

- Place all ported `RefCounted` classes from `scripts/sim/` inside `Kalyeah.Sim.asmdef` with `"noEngineReferences": true` (zero `using UnityEngine;`, `MonoBehaviour`, `Time.deltaTime`, or Unity physics/colliders).
- Execute the identical `Kalyeah.Sim.dll` across `Linux64` Dedicated Server, in-app Android Hotspot host, offline Practice vs Bots, client prediction, and NUnit `EditMode` tests.
- Keep street coordinates explicit (`readonly record struct SimVec2(float X, float Z)` in meters) and resolve cover/curb/corner bounds in pure math (`Collision.Resolve`, `MapLayout.KeepInStreet`).

| Godot 4.6 (`scripts/sim/` & `data/`) | Unity 6 C# (`Kalyeah.Sim` & `Kalyeah.Data`) |
|---|---|
| `class_name Foo extends RefCounted` | `public sealed class Foo` or `struct` in `Kalyeah.Sim` |
| `PlayerInput` bitmask + `move` / `aim_point` | `readonly record struct PlayerInput(uint Buttons, SimVec2 Move, SimVec2 MoveTarget, SimVec2 AimPoint, int Tick)` |
| `RandomNumberGenerator` (`seed`) | Match-owned deterministic `SimRng` (`ulong` state; isolated `TRIP_SEED_MIX` stream) |
| `Resource` (`GameRules`, `WeaponDef`, `BotLevel`) | Generated `ScriptableObject` / JSON catalog -> `.ToSimConfig()` immutable struct |

## 2. GDScript-to-C# math traps & cross-CPU float determinism

- **Negative modulo & wrapping:** C# `%` is a remainder preserving negative signs (`-1 % 4 == -1`), whereas GDScript `posmod`, `fposmod`, and `wrapf` wrap into `[0, m)` / `[min, max)`. Route all ring-buffer indices, deck cycles, and angle wraps through `SimMath.PosMod`, `SimMath.FPosMod`, and `SimMath.WrapF`.
- **Negative integer division:** C# integer `/` truncates toward zero (`-3 / 2 == -1`) instead of flooring; use `SimMath.FloorDiv` when mapping negative street coordinates to spatial/nav grid cells.
- **`move_toward` & `lerp_angle` edge parity:** Match GDScript `move_toward(from, to, delta)` (snap to `to` without overshoot when `MathF.Abs(to - from) <= delta`) and `lerp_angle` shortest-arc `[-π, π)` tie-breaking at `±π`.
- **x64 Linux vs ARM64 Android IL2CPP float parity:** Compile `Kalyeah.Sim` with strict IEEE-754 `float` evaluation — never enable compiler fast-math / FMA contraction reorderings, and forbid accidental `double` promotion mid-expression.
- **Transcendental safety:** Avoid platform-divergent `System.MathF` transcendentals (`Sin`, `Cos`, `Atan2`, `Pow`) on sim-critical state/branching, or wrap them in deterministic `SimMath` polynomial/LUT helpers so ARM64 IL2CPP client prediction matches x64 Linux server authority tick-for-tick.

## 3. Deterministic `30 Hz` step & C# language zero-alloc rules

- Advance sim state exclusively in `MatchSim.Step(ReadOnlySpan<PlayerInput> inputs, float dt = 1f / 30f)` in fixed slot order (`0..9`); never call `System.Random.Shared`, `DateTime`, or unordered `Dictionary`/`HashSet` iteration, and break `BarangayNav` A* f-score ties by waypoint index.
- Preallocate fixed `struct`/class pools (`MaxPlayers = 10`, projectiles/traps/zones, `BarangayNav` open/closed heaps) in `new MatchSim(config, seed)` (`apex-net-frame` owns match frame/GC ceilings).
- Ban C# hidden-alloc constructs inside `Step` and `TeamCoach` / `TeamBrain` / `FightBrain`: LINQ (`Where`, `Select`, `OrderBy`), capturing lambdas/closures, interface-boxed `IEnumerable` enumerators, `params` arrays, and string formatting.
- Preserve sim rules invariants: uniform `player_radius` (`0.45 m`) and `100` HP across all 10 cosmetic kids, `walk_run_threshold` (`0.62`) + `TRIP_SEED_MIX` rolls, `overclocked` / `exhaust_left` independent of status overwrites, NW/SE rescue hold & `rescue_cooldown`, and `no_cast_in_rescue_area` / `no_cast_in_enemy_base`.

## 4. Automated `.tres` -> `.asset`/JSON pipeline & golden-seed parity

- **Forbid hand-copying numbers** across the 14 `WeaponDef`s, 10 `CharacterDef`s, `GameRules`, `MapLayout`, and Easy/Medium/Hard `BotLevel` presets.
- Build a scripted `Kalyeah.Editor.TresImporter` that parses `data/**/*.tres` directly to emit `[CreateAssetMenu]` `ScriptableObject` `.asset` files (`Kalyeah.Data` -> `Kalyeah.Sim` one-way) AND headless server JSON catalogs, baking once at load into immutable `SimConfig` structs.
- Assert 1-to-1 field parity between every source `.tres` file and its generated `.asset` + JSON `SimConfig` in an `EditMode` test alongside schema invariants (`elixir_cost` in `1..10`, positive ranges/cooldowns, identical kid stats, preserved `card_cycle`/`elixir_enabled`/`stamina_enabled`/`rescue_frees_all` toggles).
- Port seam-by-seam (`MapLayout`/`Collision` -> locomotion/stamina/trip -> weapons/statuses -> tag/rescue/capture -> `BarangayNav`/`TeamCoach`), retiring each `scripts/sim/*.gd` file only after multi-tick golden-seed `EditMode` tests match `tests/unit/test_*.gd`.

## Anti-patterns to reject

- Referencing `UnityEngine` or mutating `ScriptableObject` fields at runtime inside `Kalyeah.Sim`.
- Using raw C# `%` or truncating `/` on negative indices/angles/cells, or raw `MathF` transcendentals in branched sim logic.
- Hand-typing `.tres` balance values into C# constants or manually edited `.asset` inspectors.
- Using LINQ, closures, `params` arrays, or boxed interfaces inside `MatchSim.Step` or bot ticks.
