---
name: apex-anim-craft
description: >
  Character animation at Epic Fortnite × Riot Valorant quality: clip catalogue and naming, pose-to-pose
  authoring craft, gameplay-honest timing synced to the 30 Hz sim, offline retargeting onto every
  kid, and Unity legacy-Animation runtime (layering, crossfades, speed matching, foot IK, aim
  offsets, spring bones, corrective drivers, LOD). Use when planning, reviewing, retargeting or
  wiring kid animations, or when motion looks floaty, slidey, mushy or dishonest.
---

# Apex Anim Craft

Ship motion with **Epic (*Fortnite*)** appeal — bold line of action, snappy anticipation,
squash and stretch, playful overshoot — and **Riot (*Valorant*)** honesty — every action
readable in its first frames, timing that matches the gameplay, grounded feet, no lies about
where a shot comes from.

**Boundary:** owns clip design, authoring craft, retargeting, and runtime playback/layering in
`View`. Defers rig/weights/correctives baking to `apex-rig-forge`, movement speeds and
camera/hit feel to `apex-combat-cam`, frame/GC budgets to `apex-net-frame`, and VFX smears to
`godot-unity-view`. The sim is the only authority (`CLAUDE.md` rules 1–3).

**Owner rule (binding):** the owner authors clips (Blender keyframes or mocap). Agents plan
clip lists, retarget, retime, layer, fix keys and wire runtime; they never invent final hero
motion. When a clip is missing, specify it (name, length, key poses, contact frames) and keep the
nearest existing clip as a stand-in.

## A1 · Clip catalogue (canonical names, every kid has every clip)

| Group | Clips (in place, 30 fps) | Loop |
|---|---|---|
| Locomotion | `Idle`, `Idle_Break`, `Walk`, `Run`, `Sprint`, `Run_Start`, `Run_Stop`, `Turn_180`, `Strafe_L`, `Strafe_R`, `Backpedal` | loops except start/stop/turn |
| Traversal | `Jump_Takeoff`, `Jump_Air`, `Jump_Land`, `Crouch_Idle`, `Crouch_Walk`, `Slide`, `Vault` | air/crouch loop |
| Tirador | `Aim_Hold` (upper body), `Fire`, `Reload` (7-shot magazine) | aim loops |
| Skills | `Cast_Throw`, `Cast_Place`, `Cast_Slam`, `Cast_Buff`, `Cast_Channel` (+ `_Run` variants) | channel loops |
| Reactions | `Hit_Light_{F,B,L,R}`, `Hit_Heavy`, `Stunned`, `Airborne`, `Knockdown`, `GetUp`, `Tagged`, `Rescued` | stun/air loop |
| Front end | `Lobby_Idle`, `Showcase` (per role), `Victory`, `Defeat`, `Emote_1..n` | idle loops |

Each skill in `unity/Data/*.json` names its cast archetype; the kit never needs a kid-specific
clip. Missing clips fall back by group (e.g. `Cast_Slam` → `Cast_Throw`).

## A2 · Authoring craft (pose-to-pose)

- **Workflow:** key poses → breakdowns → in-betweens on stepped → spline → polish curves
  (no flat tangents on contacts, no overshoot on planted feet).
- **Silhouette:** every key pose reads as a flat black shape at 64 px tall; strong line of
  action through spine and limbs; no twinning (L/R symmetric limbs).
- **Timing at 30 fps:** anticipation ≥ 3 frames, contact/strike 1–2 frames, follow-through and
  settle 6–12 frames, holds 2–4 frames on extremes (Fortnite snap), overshoot ≤ 10% then settle.
- **Arcs and overlap:** hands and head travel on arcs; hips lead, spine/head/arms follow with
  1–3 frame offsets; secondary chains (hair, cape) lag via spring bones (A5), not keyed.
- **Weight:** centre of mass over the support foot in idles; heavy impacts compress 2–4 frames;
  squash/stretch keeps volume (scale only on the root or via correctives, never per-bone
  non-uniform scale).
- **Locomotion:** feet plant with zero slide across contact; one stride = one authored distance
  (store it); run cycle 16–20 frames, walk 28–32; lean into acceleration.
- **Reactions:** directional, ≤ 6 frames to the peak pose, readable from behind.

## A3 · Gameplay honesty (sim ↔ view sync)

- **In place only:** root motion off; the sim's position and facing drive the transform.
- **Speed matching:** playback rate = sim ground speed ÷ the clip's authored stride speed
  (data per clip, not code constants); clamp 0.7–1.35, past that swap clip (walk ↔ run).
- **Telegraph frames:** the frame the projectile leaves the hand equals the sim's cast tick;
  start the clip at `CharacterDef` release time minus elapsed ticks (as `Cast_OneHand` does) so
  hand and projectile meet within one tick. Wind-up shown only if the sim has a wind-up.
- **State, not events:** derive the clip and its phase from the replicated sim state and ticks
  since it began, so prediction, rollback and late joiners rebuild the same pose.
- **Never** let animation move hitboxes, gate cooldowns or decide hits.

## A4 · Retargeting (offline, Blender)

- Source rig = canonical `mixamorig:` skeleton (`apex-rig-forge` R2). Bake every clip onto each
  kid and embed it in the kid's `.glb` under the canonical name (glTFast imports legacy clips per
  model; Mecanim Humanoid is not used).
- Transfer **world-space rotations** with rest-pose compensation
  `Rtarget = Rsrc · Rrest_src⁻¹ · Rrest_tgt` per bone, top-down; scale hips translation by the
  leg-length ratio; re-plant feet with a foot-lock pass on contact frames (IK to the source
  contact height); fingers copy local rotations about the palm-facing roll axis.
- Unmapped bones (twist, cloth) keep rest; mirrored clips use the mirrored name map.
- Verify per kid: clip names and durations, release-time measurement (hand path midpoint),
  feet slide ≤ 2 cm per cycle, no limb flips, finger curls bend toward the palm.

## A5 · Runtime (Unity legacy `Animation`, View only)

- **Crossfade table (seconds):** locomotion ↔ locomotion 0.15, into cast 0.05, into hit 0.03,
  out of hit/stun 0.2, into airborne 0.08; never fade from T-pose or a missing clip.
- **Sync groups:** walk/run/sprint share normalized phase on swap (carry `normalizedTime`).
- **Layers:** upper-body masks via `AddMixingTransform(Spine1, recursive)` for `Aim_Hold`,
  `Fire`, `Reload`, casts while legs keep locomotion; additive layers
  (`AnimationBlendMode.Additive`) for breathing and hit flinches.
- **LateUpdate order (after sampling):** aim offset (spine/neck yaw-pitch split 40/30/30 toward
  the `AimSolver` point) → two-bone foot IK to ground/stairs (pelvis drops to the lower foot)
  → hand IK for held props → spring bones on hair/cloth chains (verlet, 2 iterations,
  stiffness/damping in data) → corrective weights from `kalyeah_correctives`
  (`apex-rig-forge` R5) → overhead bar anchor.
- **Cost:** `cullingType = BasedOnRenderers`; kids beyond the near ring update every 2nd frame;
  spring bones and IK off for kids beyond ~20 m or off-screen; no per-frame allocations, cached
  `AnimationState` refs, no string lookups per frame.

## A6 · Acceptance

Review captures as frame strips (every 2nd frame) at 1280×720 and 844×390, plus a side-on strip
for feet:
- No pops (frame-to-frame joint jump > 25°), no foot slide > 2 cm per cycle, no T-pose frames,
  no clipping through own body at any key pose.
- Every attack identifiable within its first 4 frames from behind and from the side.
- Fortnite bar: clear anticipation and overshoot on every action, holds on extremes, appealing
  silhouettes. Valorant bar: honest release timing, grounded idles, readable direction.
- 10 kids animating ≤ 1.5 ms CPU on the target phone, inside the 3.0 ms View budget of
  `apex-net-frame` §2 (profile with `performance-optimization`).
