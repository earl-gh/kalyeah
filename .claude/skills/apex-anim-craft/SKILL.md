---
name: apex-anim-craft
description: >
  Character animation at Epic Fortnite × Riot Valorant quality, built on the game's existing clips:
  the clip catalogue KidView plays, an upgrade pass that lifts old clips (cleanup, retiming,
  exaggeration, overlap, foot lock), one shared clip set over the roster's shared rest pose,
  gameplay-honest timing on the 30 Hz sim, and the Unity legacy-Animation runtime (crossfades,
  8-way blending, masks, additive layers, FK→IK with limits, spring bones, corrective and face
  drivers, update-rate LOD, clip compression). Use when planning, upgrading, reviewing or wiring kid
  animation, or when motion looks floaty, slidey, mushy, rubbery or dishonest.
---

# Apex Anim Craft

Motion with **Epic (*Fortnite*)** appeal — bold line of action, snappy anticipation, holds,
squash and stretch, playful overshoot — and **Riot (*Valorant*)** honesty — readable in the first
frames, timing that matches the gameplay, grounded feet, no lie about where a shot starts.

**Boundary:** owns clips (catalogue, upgrade, sharing, retargeting) and runtime playback in `View`.
Defers rig, weights, helpers, limits and correctives to `apex-rig-forge`, movement speeds and
camera/hit feel to `apex-combat-cam`, frame/GC budgets to `apex-net-frame`, VFX smears to
`godot-unity-view`. The sim is the only authority (`CLAUDE.md` rules 1–3).

**Owner rule:** the owner authors new clips (Blender keys or mocap). Agents upgrade existing clips
when the owner asks (cleanup, retime, exaggerate, overlap, foot lock, loop fixes), retarget, and
wire runtime; they never invent new hero motion. A missing clip is specified for the owner (name,
frames, key poses, contacts) and the nearest existing clip stands in.

## A1 · Clip catalogue (the names KidView plays)

Base set = the clips already in the kid `.glb` files (`junjun` is `KidView.FallbackSet`). Keep
these names; KidView's lookup arrays (`IdleNames`, `WalkNames`, …) must change in the same commit
as any rename.

| Role | Existing clip (keep) | Upgrade target | New, owner-made (stand-in) |
|---|---|---|---|
| Idle | `Idle` | weight shift, breathing, blink cadence | `Idle_Break` (`Idle`) |
| Move | `walk`, `Run`, `sprint`, `Exhausted_Run` | lean, arcs, foot lock, stride data | `Run_Start`, `Run_Stop`, `Turn_180` (`Run`) |
| Strafe | — | — | `Strafe_L`, `Strafe_R`, `Backpedal` (`walk` turned to travel) |
| Traversal | `jump`, `Airborne`, `crouch`, `slide` | split jump into takeoff/air/land by frame ranges | `Vault` (`jump`) |
| Casts | `Cast_OneHand`, `Cast_Run`, `throw`, `cast_release_left/right/twohand` | anticipation ≥ 3 f, release frame = `CharacterDef` | `Cast_Place`, `Cast_Slam`, `Cast_Buff`, `Cast_Channel` (`Cast_OneHand`) |
| Tirador | — | — | `Aim_Hold` (upper body), `Fire`, `Reload` (`throw`) |
| Reactions | `hit`, `Stunned`, `trip`, `Tripped` | directional read, ≤ 6 f to peak | `Hit_Heavy`, `Knockdown`, `GetUp` (`Tripped`) |
| Front end | `victory` | hold on the hero pose | `Lobby_Idle`, `Showcase` per role, `Defeat` (`Idle`) |

Skills in `unity/Data/*.json` name a cast archetype, never a kid-specific clip.

## A2 · Upgrade pass on existing clips (in order, per clip)

1. **Clean:** Euler filter (no 360° flips), drop jitter (keys whose removal moves no bone > 0.5°),
   fix loop seams (first = last pose, matched tangents).
2. **Retime for snap:** compress anticipation-to-strike, add 2–4 frame holds on extremes, ease
   into contacts, fast out of strikes; never move a gameplay frame (release, landing) — retime
   around it or update `CharacterDef` and its tests in the same change.
3. **Exaggerate:** additive layers on key poses only — deeper spine arc along the line of action,
   bigger crouch before jumps, wider arm arcs in runs; overshoot ≤ 10% then settle over 3–6 frames.
4. **Overlap:** offset head, spine and arms 1–3 frames behind the hips; secondary chains via spring
   bones (A6), not keys.
5. **Arcs:** hands and head on clean arcs (no zig-zag in the motion trail); fix with spacing, not
   extra poses.
6. **Feet:** foot lock on contact frames (zero slide), toe roll on push-off, hips settle on landing.
7. **Measure and store:** stride distance per locomotion cycle and the release frame per cast,
   written to data (A4); before/after frame strips for review (A8).

Craft numbers at 30 fps: anticipation ≥ 3 frames, strike 1–2, follow-through 6–12, run cycle 16–20,
walk 28–32, reaction peak ≤ 6. Every key pose reads as a flat black silhouette at 64 px with a clear
line of action and no twinning. Squash/stretch only on the root or through correctives, never
non-uniform bone scale.

## A3 · One shared clip set

- The roster shares bone paths and rest rotations (`apex-rig-forge` R2), so one upgraded clip set
  (stored once as Unity `AnimationClip` assets, legacy, bound by transform path) drives all 12 kids;
  the hips' height track is scaled per kid by leg length at load (`Hips` local position ×
  `kidLegLength / referenceLegLength`).
- A kid whose rest rotations can't match gets its own baked copy (A5); the scan in A8 flags it.
- Clips import with keyframe reduction (rotation error 0.5°, position 0.5 mm) and compression;
  no scale curves; one clip set in memory regardless of roster size.

## A4 · Gameplay honesty (sim ↔ view)

- **In place:** root motion off; the sim's position and facing drive the transform.
- **Speed matching:** rate = sim ground speed ÷ the clip's stride speed (data per clip); clamp
  0.7–1.35, past that swap clip (walk ↔ run ↔ sprint).
- **Release frames:** the hand reaches its release on the sim's cast tick; the clip starts at
  `CharacterDef` release time minus the ticks already elapsed (as `Cast_OneHand` does today).
  Wind-up only where the sim has one.
- **State, not events:** clip and phase derive from replicated sim state and ticks since the state
  began, so prediction, rollback and late joiners rebuild the same pose.
- Animation never moves hitboxes, gates cooldowns or decides hits.

## A5 · Retargeting (offline, Blender; for new owner clips and fallback copies)

- Per frame, top-down, in **world space**: `Wtgt = (Wsrc · Wsrc_rest⁻¹) · Wtgt_rest`, then local
  `Ltgt = Wparent_tgt⁻¹ · Wtgt`. Hips translation × leg-length ratio. With shared rest rotations
  this reduces to copying local rotations.
- Foot lock on the source's contact frames by IK to the source contact height; fingers copy local
  rotations about the palm-facing axis.
- Swing–twist clamp to `rig__joint_limits.json` each frame; log bone, frame and overshoot; > 10°
  means the source pose doesn't fit — fix it on the control rig (FK/IK), never widen limits.
- Helpers and twist bones are baked from their rules (`apex-rig-forge` R2), not copied.

## A6 · Runtime (Unity legacy `Animation`, View only)

- **Crossfades (s):** locomotion ↔ locomotion 0.15, into cast 0.05, into hit 0.03, out of hit or stun
  0.2, into air 0.08; never from T-pose or a missing clip.
- **8-way locomotion:** legacy has no blend trees — play `walk`/`Run`, `Strafe_L/R`, `Backpedal`
  together and set each `AnimationState.weight` from the move direction relative to facing
  (cosine weights of the two nearest directions, normalized), with a shared `normalizedTime`.
- **Sync:** walk/run/sprint carry `normalizedTime` across swaps.
- **Masks:** `AddMixingTransform(Spine1, recursive)` for aim, fire, reload and casts over running legs.
- **Additive:** `AnimationBlendMode.Additive` uses each clip's first frame as its reference pose;
  author breathing/flinch clips to start at the neutral pose, or they add the wrong offset.
- **LateUpdate order (after sampling, the FK pose):**
  1. Aim offset: spine/neck yaw-pitch split 40/30/30 toward the `AimSolver` point, each share
     clamped to its limits.
  2. Foot IK: analytic two-bone solve to the ground/stair hit, knee on the clip's pole plane; pelvis
     drops to the lower foot; ankle to the ground normal within limits.
  3. Hand IK for held props, same solver with an elbow pole.
  4. Swing–twist clamp every IK-touched bone to the limits (axes from the exported limits data); out
     of reach stops at full extension minus 1°.
  5. Re-evaluate half-angle helpers and twist bones only for joints IK changed.
  6. Spring bones on hair/cloth (verlet, 2 iterations, stiffness/damping in data, capsule colliders
     on head, torso, `UpLeg`, `Leg`).
  7. Correctives from `kalyeah_correctives` (hinge angle, cone readers, combination products), then
     face (blink every 2–6 s randomized in the view, expression per state), then the overhead bar
     anchor.
- **Update-rate LOD:** near kids sample every frame; far kids disable the `Animation` component and
  call `Sample()` themselves every 2nd frame; off-screen kids sample only on state changes; spring
  bones, IK and correctives off beyond ~20 m.
- No per-frame allocations, cached `AnimationState` refs, no string lookups per frame.

## A7 · Clip gaps for the owner

For each missing clip, hand over: name, frame count, loop or not, key poses (silhouette sketch or
reference), contact/release frames, which existing clip stands in. Never ship a stand-in silently:
list it in the PR's device checks.

## A8 · Acceptance

- Frame strips (every 2nd frame) at 1280×720 and 844×390, plus a side-on strip for feet; before/after
  strips for every upgraded clip.
- No pops (> 25° joint jump between frames), no foot slide > 2 cm per cycle, no T-pose frames, no
  self-clipping at key poses, every attack readable in its first 4 frames from behind and the side.
- **Limit scan:** every frame of every clip on every kid, plus live IK in a stress scene: zero bones
  outside limits, zero sideways hinges, no straight-locked limbs; also flags kids whose rest
  rotations miss the shared set (A3).
- Fortnite bar: anticipation, holds and overshoot on every action, appealing silhouettes. Valorant
  bar: honest release timing, grounded idles, readable direction.
- 10 kids ≤ 1.5 ms CPU on the target phone, inside the 3.0 ms View budget of `apex-net-frame` §2;
  one clip set's memory (A3) whatever the roster size.
