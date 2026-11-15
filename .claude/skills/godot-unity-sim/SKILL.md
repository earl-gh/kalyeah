---
name: godot-unity-sim
description: >
  Own Kalyeah's deterministic 30 Hz pure C# gameplay simulation (unity/Kalyeah.Sim)
  and canonical JSON data catalogs (unity/Data/*.json) across Unity 6, LAN Hotspot,
  and the .NET 8 dedicated server.
---

# Unity 6 & .NET 8 Architecture — Part 1: Deterministic Sim & Data (`godot-unity-sim`)

Owns `unity/Kalyeah.Sim/` (pure engine-free C# `30 Hz` simulation, bots, and *Larong Kalye* street modes) and `unity/Data/*.json` (`SimData` / `GameData` catalogs).

## Boundary (zero overlap)

- **Owns:** `Kalyeah.Sim` pure C# assembly (`noEngineReferences: true`), deterministic `30 Hz` `MatchSim.Step`, cross-CPU IEEE-754 float determinism, seeded `GodotRng` streams, combat/tag/rescue/capture/status/`BarangayNav` A* bots/street modes, and `unity/Data/*.json` schema & invariants.
- **Defers to:** `godot-unity-view` (URP Mobile views, shaders, `.glb` rigs, pooled VFX/SFX); `godot-unity-platform` (`AppSession`/`SettingsStore`, WebSocket/Hotspot transport, `EnhancedTouch`, APK & `Kalyeah.Server` builds); `apex-net-frame` (match frame-time, reconciliation, and memory budgets).

## 1. Pure engine-free `Kalyeah.Sim` assembly

- Keep every gameplay class/struct inside `unity/Kalyeah.Sim/` (`Kalyeah.Sim.asmdef` with `"noEngineReferences": true`: zero `using UnityEngine;`, `MonoBehaviour`, `Time.deltaTime`, or Unity physics/colliders).
- Execute the identical `Kalyeah.Sim` source across `unity/Kalyeah.Server` (`.NET 8` Linux container), in-app Android Hotspot host (`HotspotNet`), offline Practice vs Bots (`SimBridge`), client prediction (`GameClient`), and NUnit tests (`Kalyeah.Sim.Tests`).
- Keep street coordinates explicit (`Vec2(float X, float Y)` representing street `(x, z)` in meters) and resolve cover/curb/arena bounds in pure math (`Collision`, `MapLayout`).

## 2. Deterministic math & cross-CPU float parity

- **Modulo & wrapping:** C# `%` preserves negative signs (`-1 % 4 == -1`), whereas ring-buffer indices, deck cycles, and angle wraps must wrap cleanly into `[0, m)` / `[min, max)` via `SimMath` helpers (`PosMod`, `FPosMod`, `WrapF`).
- **Negative integer division:** C# integer `/` truncates toward zero (`-3 / 2 == -1`); use floor division when mapping negative street coordinates to `BarangayNav` or spatial grid cells.
- **`MoveToward` & `LerpAngle` edge parity:** Snap to target without overshoot when `MathF.Abs(to - from) <= delta`, and break `±π` shortest-arc ties deterministically.
- **x64 Linux (`.NET 8`) vs ARM64 Android (IL2CPP) float parity:** Maintain strict `float` evaluation — never enable fast-math/FMA reorderings or introduce accidental `double` promotions on branched sim state.

## 3. Deterministic `30 Hz` step & zero-alloc hot loops

- Advance sim state exclusively in `Step(dt = 1f / 30f)` in fixed player slot order (`0..9`); never call `System.Random.Shared`, `DateTime`, or unordered `Dictionary`/`HashSet` iteration, and break `BarangayNav` A* f-score ties deterministically.
- Preallocate fixed arrays/pools (`MaxPlayers = 10`, projectiles/traps/zones, `BarangayNav` buffers) at match construction (`apex-net-frame` owns match frame/GC ceilings).
- Ban hidden C# allocations inside `MatchSim.Step` and `TeamCoach` / `TeamBrain` / `FightBrain` / `StreetModeCoach`: LINQ (`Where`, `Select`, `OrderBy`), capturing lambdas/closures, boxed `IEnumerable` enumerators, `params` arrays, and per-tick string formatting.
- Preserve GDD invariants (`docs/GDD.md`): uniform `player_radius` (`0.45 m`) and `100` HP across all 10 cosmetic kids, `walk_run_threshold` (`0.62`) + isolated `TRIP_SEED_MIX` RNG stream, `overclocked` / `exhaust_left` independent of status overwrites, NW/SE rescue hold & `rescue_cooldown`, and `no_cast_in_rescue_area` / `no_cast_in_enemy_base`.

## 4. Canonical `unity/Data/*.json` pipeline & verification

- Store every gameplay, weapon, character, bot, camera, network, and street-mode number in `unity/Data/*.json` (synced into `Assets/Imported/Resources/Data/` by `python tools/unity_export/sync_assets.py` and loaded by `SimData` / `GameData`).
- Pin schema and fairness invariants in `unity/Kalyeah.Sim.Tests` (`dotnet test unity/Kalyeah.Sim.Tests`): positive ranges/cooldowns, `elixir_cost` in `1..10`, identical `Hp` and `player_radius` across all 10 kids, and preserved data toggles (`card_cycle`, `elixir_enabled`, `stamina_enabled`, `rescue_frees_all`).

## Anti-patterns to reject

- Referencing `UnityEngine` inside `Kalyeah.Sim` or `Kalyeah.Game.Core`.
- Using raw C# `%` or truncating `/` on negative indices/angles/cells in sim math.
- Hardcoding gameplay balance numbers in C# instead of `unity/Data/*.json`.
- Using LINQ, closures, `params` arrays, or boxed interfaces inside `Step` or bot ticks.
