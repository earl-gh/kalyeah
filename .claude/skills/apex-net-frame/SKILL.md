---
name: apex-net-frame
description: >
  Canonical engine-neutral 30 Hz server-authoritative WebSocket (TCP) prediction/reconciliation,
  <=300 B delta backpressure, and 10-kid 60 FPS Android CPU/GPU/VRAM/GC budgets (Riot Valorant ×
  Call of Duty: Mobile). Use when working on multiplayer snapshots, client prediction, WebSocket
  or Hotspot sync, VFX overdraw, pooling, or Android 60 FPS optimization.
---

# Apex Netcode & 60 FPS Android Frame Discipline

Fuse **Riot (*Valorant*)** server-authoritative peek/hit honesty with ***Call of Duty: Mobile***
rock-solid sustained 60 FPS Android frame pacing and thermal discipline across 10-player 5v5
barangay brawls.

**Boundary (Canonical Engine-Neutral Owner):** Single source of truth for `30 Hz` authoritative
prediction/reconciliation, WebSocket (TCP) payload/backpressure envelopes, and `16.6 ms` Android
CPU/GPU/VRAM/GC budgets. Defers engine socket/lifecycle APIs to `godot-unity-platform`, C# sim
struct/determinism rules to `godot-unity-sim`, asset import flags to `godot-unity-view`, and
profiler workflow to `performance-optimization`.

## 1. Lag-Honest WebSocket (TCP) Netcode (`80–150 ms` 4G/Wi-Fi + Local Hotspot)

- **Compact input-only upstream:** Clients send bitpacked `PlayerInput` frames (`<= 32 B`: `move` /
  `move_target`, `aim_point`, `buttons` bitmask, `tick`) at `30 Hz`. Never trust client-reported
  positions, hit claims, range clamps, or cooldowns.
- **WebSocket (TCP) head-of-line & backpressure discipline:** Because gameplay runs over WebSocket
  (TCP) rather than UDP, a single lost packet stalls in-order stream delivery until retransmit:
  1. **Payload ceiling:** Keep typical `20 Hz` quantized delta snapshots **`<= 300 B`** so frames
     never fragment across mobile MTU boundaries.
  2. **Zero Nagle buffering:** Require **`TCP_NODELAY`** on both dedicated/Hotspot server and
     client sockets so small `30 Hz` inputs and `20 Hz` deltas flush immediately.
  3. **Bounded send-queue backpressure:** Cap per-client outbound socket buffers; when a 4G peer
     stalls, drop superseded intermediate visual deltas and encode one fresh delta from the peer's
     last acked baseline (keeping round/score state reliable) instead of queueing multi-second TCP
     lag spirals.
  4. **Burst drain on recovery:** When delayed TCP frames arrive in a single burst, advance to the
     newest authoritative snapshot first and run **one** replay pass—never re-step stale snapshots.
- **Immediate local prediction & smooth visual reconciliation:**
  1. Step local kid locomotion and 3P look on frame `0`, storing unacked `PlayerInput` frames in a
     fixed-capacity ring buffer.
  2. On each authoritative `20 Hz` delta snapshot, replay unacked inputs deterministically at
     `dt = 1/30` from server state.
  3. Keep sim coordinates exact while exponentially decaying small View-puppet visual offsets
     (`< 0.35 m`) over `80–120 ms` so `40–80 ms` 4G/Hotspot jitter never rubber-bands.
  4. Hard-snap immediately only on genuine discontinuities (`Tagged` sidewalk placement, `Rescue`
     base-post return, round reset, or desync `> 2.0 m`).
- **Remote interpolation & server authority:** Render remote kids/projectiles `~100 ms` (`2`
  snapshot intervals) behind server tick with bounded single-drop dead-reckoning. Server enforces
  `max_range`, `elixir`/cooldowns, `no_cast_in_rescue_area`, `no_cast_in_enemy_base`, and
  *Langit Lupa* brownout culling; flush a zeroed stop + `BTN_AIM_CANCEL` frame on app pause.

## 2. Worst-Case 5v5 Stress Envelope & `16.6 ms` Android Budget

- **Mandatory 10-kid benchmark scene:** Measure **every** frame (`<= 16.6 ms`), GC (`0 B`), and
  alpha-overdraw (`<= 3` layers) ceiling during a **10-kid simultaneous mid-canal teamfight**
  (`2` overlapping `Bagsak` ults + `6` active projectiles/zones + `10` overhead bars + `9` active
  `BarangayNav` bots)—**never** in an empty 1v0 sandbox. Hold locked `60 FPS` across 3 consecutive
  matches on recommended Android (`Adreno 610` / `Mali-G52`, `4 GB` RAM) and `>= 30 FPS` on
  minimum (`Snapdragon 680` / `Helio G85`, `3 GB` RAM):

| Subsystem | Ceiling | Engine-Neutral Discipline |
|---|---|---|
| **Sim + Bots** | `<= 1.5 ms` | Fixed `dt = 1/30` step; zero-alloc `BarangayNav` A* & `TeamCoach`; stagger non-critical path refreshes deterministically |
| **View + Net + HUD** | `<= 3.0 ms` | Event-driven HUD updates; ring-buffer bitpacking; zero runtime scene-hierarchy or group scans |
| **GPU Render + VFX** | `<= 9.0 ms` | Shared trim/atlas materials, static geometry batching, `<= 3` stacked transparent overdraw layers |
| **OS / Thermal Headroom** | `~3.1 ms` | Preserves sustained clocks and prevents mid-match thermal throttling |

- **Zero hot-loop GC (`0 B` / tick & frame) & VRAM/VFX pooling:** Ban match-loop heap allocations,
  per-frame string formatting on unchanged numbers, per-hit material clones, and runtime spawns.
  Require hardware block-compressed textures (`ASTC` / `ETC2`; import flags in `godot-unity-view`),
  cap overlapping transparent VFX + `10` overhead bars to **`<= 3` alpha-overdraw layers**, and
  prewarm fixed-size pools at match load for projectiles, `FxSketch` bursts, decals, bars, and SFX.

## 3. Anti-Bloat Systems Discipline

- Build prediction rings, delta bitpackers, A* scratch buffers, and object pools with plain
  fixed-size arrays, value structs, and integer ring indices.
- Ban ECS/DOTS rewrites, reflection/JSON hot-path serializers, background gameplay threads that
  complicate Android lifecycle determinism, and premature micro-optimizations in cold menus.
