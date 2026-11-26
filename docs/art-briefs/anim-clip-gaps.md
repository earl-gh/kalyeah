# Animation Clip Gaps — Owner Authoring Brief (A7)

Status: Active — the 20 existing clips in `tools/blender/kids/clips/roster_clips.glb` have been upgraded (cleanup, retiming, exaggeration, overlap, analytical IK foot lock `<= 2 cm`, helper/twist baking, swing-twist limit clamping, and capsule clearance) and measured in `unity/Data/anim__clip_metrics.json`. Every clip listed below is part of the `apex-anim-craft` A1 target catalogue. **Authored (2026-11-26):** all seven P1 clips, the four P2 cast archetypes and `Sit` (see "Authored Clips" below), keyed by `tools/blender/kids/author_clips.py` and added to `roster_clips.glb` beside the 20 originals (which are unchanged), so the pack now holds 32 clips. The rest are **not yet authored**; per the owner asset rule, the game keeps the listed stand-in clip working until each one is authored.

> **PR / Device-check rule (A7):** Never ship a stand-in silently. While any row below remains unauthored, note its active stand-in in the PR device-check list.

---

## Authoring Contract (All New Clips)

- **Skeleton & Rest Pose:** Author on the shared R2 roster contract (`mixamorig:Hips` root, 40° A-pose arms, 5° elbow / 3° knee pre-bend, identical bone paths & rest rotations; see `tools/blender/kids/build_reference.py`).
- **Frame Rate & In-Place:** **30 fps**, **root motion OFF** (in-place horizontal; keep vertical `Hips.z` only).
- **Zero Scale Curves:** Never keyframe bone scale (`squash/stretch` stays on root or pose-space correctives).
- **Craft Bar (Fortnite appeal × Valorant honesty):**
  - Anticipation `>= 3` frames, strike `1–2` frames, follow-through `6–12` frames, reaction peak `<= 6` frames.
  - Foot slide `<= 2 cm` per cycle on stance frames; no joint pops `> 25°/frame`; no sideways elbow/knee bends; no straight-locked limbs (`>= 3°` bend).
  - Readable black silhouette at 64 px from behind (3P shoulder camera) and side-on.
- **Validation Gate:** Run `upgrade_clips.py` and `anim_scan.py` against `unity/Data/rig__joint_limits.json` before merging any new clip. Clips keyed by `author_clips.py` are clamped to the joint limits and get their helper bones baked as they are keyed, so they need only `anim_scan.py` (do not re-run `upgrade_clips.py` on the 32-clip pack: it rebuilds the pack from the original 20).

---

## Existing 20-Clip Coverage (Kept & Upgraded)

| Role | Existing Clips in `roster_clips.glb` | Notes |
|---|---|---|
| Idle | `Idle` (61f, 2.00s, loop) | Weight shift, grounded feet, athletic ready stance |
| Move | `walk` (28f, 0.90s), `Run` (19f, 0.60s), `sprint` (15f, 0.47s), `Exhausted_Run` (28f, 0.90s) | All loops; speed-matched via `anim__clip_metrics.json` `strideSpeedMps` |
| Traversal | `jump` (28f, 0.90s, split `takeoff`/`air`/`land`), `Airborne` (49f, 1.60s, loop), `crouch` (43f, 1.40s, loop), `slide` (31f, 1.00s) | `jump` phase ranges recorded in `anim__clip_metrics.json` |
| Casts | `Cast_OneHand` (67f, 2.20s, release `0.725s`/f22), `Cast_Run` (67f, 2.20s, release `1.008s`/f30), `throw` (31f, 1.00s, release `0.500s`/f15), `cast_release_left` (31f), `cast_release_right` (31f), `cast_release_twohand` (34f, release `0.600s`/f18) | Locked to `CharacterDef` (`characters__*.json`) |
| Reactions | `hit` (19f, 0.60s, peak f5), `Stunned` (49f, 1.60s, loop), `trip` (46f, 1.50s), `Tripped` (109f, 3.60s) | Directional snap + grounded recovery |
| Front end | `victory` (61f, 2.00s, loop) | Hero pose hold |

---

## Authored Clips (2026-11-26)

Keyed in Blender by `tools/blender/kids/author_clips.py` (own FK and two-bone IK, every frame clamped to `rig__joint_limits.json` with helper bones baked), 30 fps, in place, Fake User on every action, loops with the last key equal to the first. No outside motion data was used. Metrics are in `unity/Data/anim__clip_metrics.json` (`data.clips`); their `standIns` entries are removed. `anim_scan.py` on the 32-clip pack: 30/32 pass, the 20 originals measure exactly as before.

| Clip | Keys (duration) | As authored | Differs from the brief |
|---|---|---|---|
| `Strafe_L` / `Strafe_R` | `f0–24` (0.80 s), loop | Cross-step, hips facing forward; 2.0 m of travel a cycle with the stances on `Run`'s phases (L stance from 4/24, R from 16/24) so `KidView` can sync them to `Run` | **Fail `anim_scan` on capsule overlap only:** the crossing thigh passes 32 mm into the other thigh's capsule at mid-cross (owner accepted the cross-step over the side-shuffle that passes) |
| `Backpedal` | `f0–24` (0.80 s), loop | Chest forward, high knee lift, heel-to-toe reach back; same stride and phases as the strafes | Contacts follow `Run`'s phases, not `f0–11` / `f12–23` |
| `Aim_Hold` | `f0–30` (1.00 s), loop | **Left hand holds the fork** at shoulder height, right hand draws the band to the cheek, breathing sway | — |
| `Fire` | `f0–12` (0.40 s) | Release `f3`, recoil, then a short beat of seating the next stone (`f5–8`) before returning to aim | Stone beat replaces the plain follow-through hold |
| `Reload` | `f0–45` (1.50 s) | Right hand arcs round the shoulder to the pouch, pebble seated, band tug, back to aim | — |
| `Vault` | `f0–18` (**0.60 s**) | Left palm plants, legs straight and hip-width sweep over (feet clear 1.0 m), grounded absorb | **0.60 s, not 0.40 s:** the 12-key version could not stay under the 25°/frame pop gate with straight legs; play it at 1.5× for the 0.40 s vault. `KidView` does not play `Vault` yet |
| `Cast_Place` | `f0–24` (0.80 s) | Squat, palm to the street in front of the feet, spring back up | **Ground touch at `f9` (0.300 s), not `f7`** (owner) |
| `Cast_Slam` | `f0–36` (1.20 s) | Bigger heroic hop (feet 0.64 m up at the apex) with the two-hand overhead raise during the hop, slam, deep crouch hold, bounce recover | Impact `f14` as briefed; the hop is higher than briefed (owner) |
| `Cast_Buff` | `f0–27` (0.90 s) | Elbows tuck, then both arms thrust straight up and out in a V | **Peak at `f10` (0.333 s), not `f8`**, and straight raised arms instead of a chest punch (owner) |
| `Cast_Channel` | `f0–30` (1.00 s), loop | Braced forward lean, cupped hands pulsing | — |
| `Sit` | `f0–120` (4.00 s), loop | Seated on the street, knees up, with an occasional head scratch (`f42–104`); not in the brief | New: for tagged kids (`KidView` uses `Sit`, then `crouch`, then `Idle`) |

`KidView` uses `Strafe_L`, `Strafe_R`, `Backpedal` (8-way blend, synced to `Run`), `Aim_Hold`, `Fire`, `Reload` and `Sit` by name. The four `Cast_*` clips ship in every kid's model but no code plays them yet (casts still use `Cast_OneHand` / `Cast_Run`).

---

## Missing A1 Clips — Owner Specification

### Priority P1 — Core Combat & 8-Way Locomotion

| Clip Name | Role | Frames @ 30 fps (Duration) | Loop | Upper Mask | Key Poses & Silhouette | Contact / Release Frames | Active Stand-In |
|---|---|---|---|---|---|---|---|
| `Strafe_L` | Strafe | 24f (0.80s) | Yes | No | Hips stay facing forward (`-Y`) while legs cross-step left; torso upright for Tirador aim; elbows tucked, clear knee gap | L contact `f0–11`, R contact `f12–23`; constant lateral stride speed | **Authored** (was `walk` (turned to travel)) |
| `Strafe_R` | Strafe | 24f (0.80s) | Yes | No | Mirror of `Strafe_L`, stepping right while facing forward; knees angled slightly out to avoid thigh clip | R contact `f0–11`, L contact `f12–23`; constant lateral stride speed | **Authored** (was `walk` (turned to travel)) |
| `Backpedal` | Strafe | 24f (0.80s) | Yes | No | Slight forward chest hunch, quick reaching rear heel-to-toe steps, high knee lift so toes never drag; clear retreat silhouette | L contact `f0–11`, R contact `f12–23`; constant reverse stride speed | **Authored** (was `walk` (turned to travel)) |
| `Aim_Hold` | Tirador | 30f (1.00s) | Yes | Yes (`Spine1+`) | Left arm extended holding slingshot fork forward at shoulder height; right hand pulling band back toward cheek (stronger pull for held long shots per `docs/GRAPHICS.md`); subtle breathing sway | Hold pose at `f0` (additive-clean neutral aim start); no foot contacts (masked) | **Authored** (was `Idle`) |
| `Fire` | Tirador | 12f (0.40s) | No | Yes (`Spine1+`) | Matches `0.40s` Tirador shot cadence (`docs/SPEC.md` §7): `f0–2` micro-pull anticipation, `f3` band snap release + crisp wrist/shoulder recoil kick, `f4–6` follow-through hold, `f7–11` return to `Aim_Hold` | Release at **`f3` (0.100s)**; clean upper-body mask over running legs | **Authored** (was `throw`) |
| `Reload` | Tirador | 45f (1.50s) | No | Yes (`Spine1+`) | Matches `1.50s` Tirador reload (`docs/SPEC.md` §7): `f0–8` drop slingshot slightly & reach right hand to waist stone pouch, `f9–20` grab pebble (readable hip silhouette), `f21–33` seat pebble in leather pouch & tug band test, `f34–44` snap back into ready aim | Pouch grab `f14` (0.47s), pebble seated **`f30` (1.00s)**, ready `f44` (1.47s) | **Authored** (was `throw`) |
| `Vault` | Traversal | 12f (0.40s) | No | No | Matches `0.40s` vault (`docs/SPEC.md` §7, `docs/GRAPHICS.md`): `f0–2` crouch/spring launch toward obstacle, `f3–5` left palm plants firmly on waist-high ledge (`+0.95m`) while legs whip sideways in tucked vault arc, `f6–9` clear ledge & extend landing foot, `f10–11` snappy grounded absorb | Hand plant **`f3–6` (0.10–0.20s)**, landing foot contact **`f10` (0.33s)** | **Authored** (was `jump`) |

### Priority P2 — Locomotion Transitions & Skill Cast Archetypes

| Clip Name | Role | Frames @ 30 fps (Duration) | Loop | Upper Mask | Key Poses & Silhouette | Contact / Release Frames | Active Stand-In |
|---|---|---|---|---|---|---|---|
| `Run_Start` | Move | 12f (0.40s) | No | No | Forward lean anticipation (`f0–3`), explosive push-off off back foot (`f4–6`), first driving stride (`f7–11`) matching `Run` frame 0 pose & velocity | Push-off contact `f0–5`, first strike `f9–11` | `Run` |
| `Run_Stop` | Move | 15f (0.50s) | No | No | Heel-plant brake (`f0–4`) leaning back `12°`, dust-kick skid step (`f5–9`), weight settle forward into `Idle` (`f10–14`) | Brake plant `f2–8`, both feet planted `f10–14` | `walk` |
| `Turn_180` | Move | 14f (0.47s) | No | No | Sharp plant on outside foot (`f0–3`), hips & head whip 180° around pivot (`f4–8`) with trailing arms overlapping 2f behind, push out into `Run` (`f9–13`) | Pivot foot locked `f1–7`, exit drive `f9–13` | `Run` |
| `Cast_Place` | Casts | 24f (0.80s) | No | Optional | For ground traps (`papel_trap`, `jacks`): `f0–5` squat & gather down, `f6–9` snap palm/item to street floor in front of feet (hold 3f), `f10–23` spring upright into ready stance | Both feet planted `f0–23`; ground touch/release **`f7` (0.233s)** | **Authored** (was `Cast_OneHand`) |
| `Cast_Slam` | Casts | 36f (1.20s) | No | No | For heavy AoE / ultimate (`ult_bagsak`, `delay = 0.45s` in `weapons__ult_bagsak.json`): `f0–9` heroic hop & overhead two-hand raise (bold skyward silhouette), `f10–13` violent downward slam, `f14–18` deep ground-impact hold with knee crouch, `f19–35` bounce recover | Takeoff `f2`, impact/release **`f14` (0.467s ≈ 0.45s delay)**, feet planted `f14–35` | **Authored** (was `Cast_OneHand`) |
| `Cast_Buff` | Casts | 27f (0.90s) | No | Yes (`Spine1+`) | For self/ally buffs (`heal`, `papel_shield`, Merienda): `f0–5` tuck elbows inward to chest, `f6–10` punch chest/arms outward in confident cheer burst (3f hold), `f11–26` settle | Activation peak **`f8` (0.267s)**; upper-body safe while jogging | **Authored** (was `Cast_OneHand`) |
| `Cast_Channel` | Casts | 30f (1.00s) | Yes | Yes (`Spine1+`) | For sustained casts / rescues: forward-leaning braced stance, hands cupped/extended with rhythmic energy pulse and slight head/shoulder tension | Loop `f0 == f30`; feet planted if full-body, masked above `Spine1` if moving | **Authored** (was `Cast_OneHand`) |

### Priority P3 — Heavy Reactions & Front-End Personality

| Clip Name | Role | Frames @ 30 fps (Duration) | Loop | Upper Mask | Key Poses & Silhouette | Contact / Release Frames | Active Stand-In |
|---|---|---|---|---|---|---|---|
| `Hit_Heavy` | Reactions | 24f (0.80s) | No | No | Hard knockback flinch: `f0–4` sharp torso recoil + head whip back (peak `<= 6f`!), `f5–9` stagger step back with arms out for balance, `f10–23` brace and recover | Peak recoil **`f5` (0.167s)**, stagger foot plant `f9–23` | `hit` |
| `Knockdown` | Reactions | 27f (0.90s) | No | No | For climb-interrupted knockdowns (`docs/SPEC.md` §7) and hard CC: `f0–5` feet swept out / comic airborne flip arc, `f6–12` land flat on seat/back with bounce squash, `f13–26` dizzy sprawl hold | Ground impact **`f10` (0.333s)**, settled sprawl `f14–26` (zero underground clipping) | `Tripped` |
| `GetUp` | Reactions | 24f (0.80s) | No | No | Continues cleanly from `Knockdown` `f26`: `f0–6` roll onto hands & one knee, `f7–14` push off street with snappy dust-brush energy, `f15–23` pop into `Idle` | Hand push `f5–11`, feet planted `f12–23` | `Tripped` |
| `Idle_Break` | Idle | 75f (2.50s) | No | No | Playful barangay fidget played occasionally over `Idle`: `f0 == f74 == Idle f0`; stretch slingshot band / adjust cap / hop weight foot-to-foot with big readable silhouette, settle back to `Idle` | Feet planted (optional quick hop `f30–38`) | `Idle` |
| `Lobby_Idle` | Front end | 90f (3.00s) | Yes | No | Relaxed heroic stance for Home / Lobby / Kid Select stage (warm backlit Valorant agent-select vibe per `docs/GRAPHICS.md`): confident hip-shift, breathing, look around, subtle slingshot twirl | Feet firmly planted `f0–89` (`<= 0.5 cm` drift) | `Idle` |
| `Showcase` | Front end | 60f (2.00s) | No | No | **1 clip per role** (`Showcase_Striker`, `Showcase_Defender`, `Showcase_Seeker`, `Showcase_Controller`) for Kid Select lock-in: snappy 6f anticipation -> signature street-game flourish -> proud hero hold (`f32–59`) | Hero pose lock **`f32` (1.067s)**; feet planted on hero pedestal | `victory` |
| `Defeat` | Front end | 60f (2.00s) | Yes | No | Playful "Taya!" pout for post-match loss & jail bench (`docs/SPEC.md` §7: never grim or violent): crossed arms, puffed cheeks/head droop, toe-tap fidget | Feet planted `f0–59` (toe tap `f20–26`, `f44–50`) | `Idle` |

---

## Stand-In Reference (Active Metrics)

All stand-in mappings above are stored in `unity/Data/anim__clip_metrics.json` under `data.standIns` so `KidView` resolves the nearest upgraded clip deterministically without silent missing-clip pops. Once an owner-authored clip lands in `tools/blender/kids/clips/roster_clips.glb`, run `anim_scan.py`, add its metrics and remove its entry from `standIns` (and from `STAND_INS` in `upgrade_clips.py`).
