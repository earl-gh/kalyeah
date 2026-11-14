---
name: apex-net-frame
description: >
  Lag-honest 30 Hz server-authoritative mobile netcode prediction/reconciliation and sustained
  60 FPS Android CPU/GPU/VRAM/GC frame-budget discipline (Riot Valorant × Call of Duty: Mobile).
  Use when working on multiplayer snapshots, client prediction, WebSocket/Hotspot sync, VFX
  overdraw, pooling, or Android 60 FPS optimization.
---

# Apex Netcode & 60 FPS Android Frame Discipline

Fuse **Riot (*Valorant*)** server-authoritative peek/hit honesty with ***Call of Duty: Mobile***
rock-solid sustained 60 FPS Android frame pacing and thermal discipline across 10-player 5v5
barangay brawls.

**Boundary:** Owns `30 Hz` authoritative netcode responsiveness over mobile Wi-Fi/4G/Local
Hotspot and `16.6 ms` Android CPU/GPU/VRAM/GC budgets. Leaves camera/aim feel to
`apex-combat-cam`, 3D world value hierarchy to `apex-street-world`, and HUD layout/touch zones
to `apex-mobile-hud`.

## 1. Lag-Honest Authoritative Netcode (`80–150 ms` 4G/Wi-Fi + Local Hotspot)

- **Compact input-only upstream:** Clients send bitpacked `PlayerInput` frames (`move` /
  `move_target`, `aim_point`, `buttons` bitmask, `tick`) over `WebSocketMultiplayerPeer` /
  Unity WebSocket transport. Never trust client-reported positions, hit claims, range clamps,
  or cooldowns.
- **Immediate local prediction & smooth visual reconciliation:**
  1. Step local kid locomotion (walk/run/jump/slide/`Luksong-Tinik`) and 3P shoulder look on
     input frame `0`, recording unacknowledged `PlayerInput` structs in a fixed ring buffer.
  2. On receiving each `20 Hz` quantized delta server snapshot, replay unacknowledged inputs
     deterministically at `dt = 1/30` from authoritative state.
  3. Keep sim coordinates exact while exponentially decaying small View-puppet visual offsets
     (`< 0.35 m`) over `80–120 ms` so `40–80 ms` 4G/Hotspot jitter never rubber-bands.
  4. Hard-snap immediately only on genuine discontinuities (`Tagged` sidewalk placement,
     `Rescue` return to base post, round reset, or desync `> 2.0 m`).
- **Remote interpolation & server authority:** Render remote kids and projectiles `~100 ms`
  (`2` snapshot intervals) behind server tick with bounded short-window dead-reckoning across a
  single dropped packet. Server deterministically enforces `max_range`, `elixir`/cooldowns,
  `no_cast_in_rescue_area`, `no_cast_in_enemy_base`, and visibility culling (e.g., *Langit Lupa*
  brownouts). On Android app pause or focus loss, flush a zeroed stop + `BTN_AIM_CANCEL` frame
  immediately before suspending.

## 2. Sustained 60 FPS Android Budget (`<= 16.6 ms`)

Hold locked `60 FPS` (`<= 16.6 ms`, zero thermal cliff across 3 consecutive 5v5 matches) on
recommended Android (`Adreno 610` / `Mali-G52`, `4 GB` RAM) and `>= 30 FPS` on minimum
(`Snapdragon 680` / `Helio G85`, `3 GB` RAM, OpenGL ES 3 / URP Mobile):

| Subsystem | Ceiling | Mandatory Discipline |
|---|---|---|
| **Sim + Bots** | `<= 1.5 ms` | Fixed `dt = 1/30` step; zero-alloc `BarangayNav` A* & `TeamCoach`; stagger non-critical path refreshes deterministically |
| **View + Net + HUD** | `<= 3.0 ms` | Event-driven HUD diffs; ring-buffer bitpacking; zero scene-tree scans (`get_nodes_in_group` / `FindObjectsByType`) |
| **GPU Render + VFX** | `<= 9.0 ms` | Shared trim/atlas materials (SRP Batcher / Godot batching), merged static `ArrayMesh`, `<= 3` alpha-overdraw layers |
| **OS / Thermal Headroom** | `~3.1 ms` | Keeps skin temperature stable and prevents driver clock-down mid-match |

- **Zero hot-loop GC allocations:** Enforce **0 bytes** of heap churn per tick and per rendered
  frame in matches—no per-frame `Array`/`List`/LINQ/closures, temporary string formatting on
  unchanged numbers, boxed structs, per-hit material cloning, or runtime instantiation.
- **VRAM & 10-player VFX pooling:** Enforce VRAM-compressed textures (`ETC2` / `ASTC`,
  `compress/mode=2`, normal maps flagged), cap overlapping transparent VFX (`Bagsak` shockwaves,
  dust rings, `Papel` zones, 10 overhead bars) to **`<= 3` stacked overdraw layers**, and
  prewarm fixed-capacity pools at match load for projectiles, `FxSketch` bursts, ground decals,
  overhead bars, and SFX voices.

## 3. Anti-Bloat Systems Discipline

- Implement prediction history, delta bitpacking, A* scratch buffers, and object pools with
  plain fixed-size arrays, value structs, and integer ring indices.
- Ban ECS/DOTS rewrites, reflection/JSON hot-path serializers, background gameplay threads that
  complicate Android lifecycle determinism, and premature micro-optimizations in cold menus.
