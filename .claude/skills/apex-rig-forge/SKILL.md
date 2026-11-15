---
name: apex-rig-forge
description: >
  Animation-ready character pipeline: intake audit, mesh prep, canonical humanoid skeleton,
  skinning and weight refinement, range-of-motion deformation QA, pose-space corrective shapes,
  and the glTF→glTFast import gate (Epic Fortnite × Riot Valorant character-tech bar). Use when a
  kid .glb arrives or changes, when fingers/joints deform badly, before retargeting clips, or when
  reviewing rig, weight or corrective work.
---

# Apex Rig Forge

Turn an owner-authored character into a rig that animates like **Epic (*Fortnite*)** and
**Riot (*Valorant*)** characters: identical skeleton contract across the roster, clean
joint volumes at full range of motion, zero cracks, mobile-safe budgets.

**Boundary:** owns the asset from intake to an import-clean `.glb` (mesh prep, skeleton, skin
weights, deformation QA, correctives, export/import gate). Defers clip authoring, retargeting and
runtime playback to `apex-anim-craft`, materials/shaders and `KalyeahResources` sanitizing to
`godot-unity-view`, and street props to `apex-street-world`.

**Owner rule (binding, `CLAUDE.md` §Assets):** the owner makes models. Repair topology, rig,
weights or correctives only when the owner asks; never re-sculpt, re-texture or regenerate a
character. If a fix needs modelling (a missing digit, fused geometry), state it, show it, and
let the owner choose between a scripted repair and their own remodel.

Tooling: Blender `bpy` scripts in `tools/blender/kids/` (`README.md` lists order and arguments).
Every step is a script, re-runnable from `assets/models/raw/`; never hand-edit outputs.

## R0 · Intake audit (report before touching anything)

Measure, don't eyeball (`dump_tree.py`, `tips.py`-style probes):
- **Transforms:** object/armature scale `1.0` (flag `0.01` cm rigs), rotation applied, bone
  heads in metres, mesh height vs `CharacterDef` height, soles at `z = 0`, facing glTF `+Z`
  (Blender `−Y`), left side at `+X` in Blender.
- **Skeleton:** root count, hips/spine/neck/arm/leg chains, finger chains per hand (count tips at
  hand height), extra chains (hair, skirt, cape), bones with scale keys, `neutral_bone`.
- **Skin:** groups without bones, unweighted vertices, influences > 4, cross-side weights,
  seam-split twins (glTF splits vertices at UV seams) with unequal weights.
- **Mesh:** triangles, vertex count after splits, materials, texture sizes, non-manifold or
  fused digits (render the hand textured from 4 sides, then coloured by chain).
- **Clips:** names, fps, ranges, root motion present.

## R1 · Mesh prep

- One skinned mesh per kid where possible (each extra mesh is a draw call and a skin pass).
- Apply transforms; armature and mesh unit scale; feet on origin; forward glTF `+Z`.
- **Rest pose:** A-pose, arms 35–45° below horizontal, elbows/knees with 3–5° pre-bend in the
  natural direction (IK and solvers need a bend hint), palms facing the thighs, fingers relaxed.
- **Joint topology:** 3 edge loops at elbows/knees (the middle loop on the crease), 2–3 at
  wrists, shoulders and hips, a fan of loops at the knuckles. Joints lacking loops cannot be
  rescued by weights; report them.
- **Budgets (defaults until the owner sets them; 10 kids on screen, Android mid-tier):** ≤ 25k triangles in a match LOD
  (main-screen showcase may use LOD0 up to 60k), ≤ 75 deforming bones, one 2048 base-colour
  atlas + optional normal/ORM at 1024 (ETC2/ASTC), ≤ 2 materials. Flag anything over budget to the
  owner with the decimation or bake plan; never decimate silently.
- Merge seam twins only in working copies (topology analysis); exports keep the authored splits.

## R2 · Skeleton contract (identical for every kid)

Canonical names `mixamorig:<Bone>` (the game's existing clips and `KidView` use them):

| Region | Bones (parent → child) |
|---|---|
| Core | `Hips` → `Spine` → `Spine1` → `Spine2` → `Neck` → `Head` → `HeadTop_End` |
| Arm (×L/R) | `Spine2` → `Shoulder` → `Arm` → `ForeArm` → `Hand` (optional `ForeArmTwist1..n` between) |
| Leg (×L/R) | `Hips` → `UpLeg` → `Leg` → `Foot` → `ToeBase` → `Toe_End` |
| Digits (×L/R) | `Hand` → `Hand{Thumb,Index,Middle,Ring,Pinky}{1,2,3}` → `...Tip` |

- Exactly 5 digits per hand, 3 phalanges + tip each; thumb from the wrist side, then index →
  pinky away from it. Metacarpal carriers (one bone holding two digits) stay unnamed.
- **Rolls:** finger bend axis (bone local X) faces the palm on both hands; elbow and knee hinge
  axes perpendicular to the limb plane; mirrored L/R. Wrong rolls make clips bend fingers
  backward and break mirrored clips.
- No bone scale or shear in rest; connected chains where possible; leaf `_End` bones for
  direction only (no weights).
- Secondary chains (hair, skirt, cape) keep their own names, parent to the nearest body bone,
  ≤ 4 bones per chain, ≤ 4 chains per kid.
- Twist bones when the forearm/upper arm must rotate > 90° about its axis (else candy-wrapper).
- **Half-angle helpers:** each elbow, knee and wrist gets an unanimated helper at the joint pivot
  that follows 50% of the child's rotation (`Copy Rotation` mix 0.5 in Blender; the same blend in
  `LateUpdate` at runtime). Crease-band vertices bind to it, so the joint bends round, never on one
  edge loop.

### Joint limits (single source of truth)

Bone-local axes after R2 rolls: **X = flex (+ toward palm/front)**, **Z = abduct/splay**,
**Y = twist (along the bone)**. Limits live in `unity/Data/rig__joint_limits.json` (degrees,
roster-wide, stylized-kid anatomy). The Blender control rig, runtime IK and the clip scan
(`apex-anim-craft`) all read this file; never copy the numbers anywhere else.

| Joint | Flex X (−ext … +flex) | Z (±) | Twist Y (±) |
|---|---|---|---|
| Spine, Spine1, Spine2 (each) | −15 … 25 | 15 | 20 |
| Neck / Head | −40 … 40 / −25 … 25 | 30 / 15 | 50 / 30 |
| Shoulder (clavicle) | −20 … 20 | elevate 30, depress 10 | 0 |
| Arm | −50 … 170 | −30 … 170 | 80 |
| ForeArm (hinge) | −5 … 145 | 0 | 85 (spread over twist bones) |
| Hand | −60 … 70 | radial 20, ulnar 35 | 0 |
| UpLeg | −20 … 125 | −25 … 45 | 40 |
| Leg (hinge) | −3 … 140 | 0 | 10 |
| Foot / ToeBase | −20 … 45 / −30 … 50 | 30 / 0 | 0 |
| Finger 1 / 2 / 3 | −15 … 90 / 0 … 100 / −5 … 80 | 15 (index, pinky 20) / 0 / 0 | 0 |
| Thumb 1 / 2 / 3 | −10 … 50 / 0 … 60 / −10 … 80 | 60 / 0 / 0 | 30 / 0 / 0 |

Clamp with **swing–twist decomposition** (twist about Y first, then the swing's X/Z ellipse);
never clamp Euler channels independently (that produces gimbal pops).

### Animation control rig (Blender authoring only, never exported)

- A separate control layer drives the deform bones by constraints; the export bakes the result
  onto the deform skeleton and strips every constraint and control bone.
- **FK/IK switch** per arm and leg: FK chain plus an IK chain (2-bone `IK`, chain length 2,
  stretch 0) with a **pole target** at the elbow/knee plane; a switch property blends them, with
  snap FK↔IK so swapping never pops. Feet: reverse-foot pivots (heel, ball, toe, bank).
- Every controlled bone gets a `Limit Rotation` constraint from the limits file (local space),
  and the IK chain gets the same ranges as `ik_min/ik_max` with `lock_ik_*` on hinge axes, so
  neither FK keys nor IK solves can leave anatomical range.
- Fingers: FK only, plus a per-hand `curl`/`spread` driver.

## R3 · Skinning and weight refinement

Order is fixed; each pass is idempotent:
1. **Digit split:** assign finger skin per digit by geometry (trace each digit tip→knuckle
   slice by slice; nearest centreline), not by the auto-rig's guess. Fused digits keep their mesh;
   the joining skin blends between neighbours.
2. **Joint falloff (between paper and noodle):**
   - **Rigid cores:** the middle 50–60% of every limb segment is 100% one bone, so bone lengths
     never stretch skin (noodle/rubber-hose limbs come from weights bleeding along the segment).
   - **Blend band** width between 0.6 and 1.5 × the joint's radius, centred on the crease loop:
     narrower folds like paper, wider bends like a noodle. Hinges (elbow, knee, finger) ~1.0 ×
     radius biased 60/40 toward the parent on the outside; ball joints (shoulder, hip) ~1.5 ×
     with clavicle/spine sharing the deltoid/glute; wrist and ankle ~0.7 ×.
   - **Crease band** vertices take the half-angle helper (R2) as one of their influences.
3. **Smoothing:** neighbour-average only inside joint zones (factor 0.35–0.5, 2–3 iterations);
   never global smoothing (it melts authored detail).
4. **Integrity:** seam twins get identical weights (else cracks under bend); no influence from
   the opposite side of the body; vertices on `neutral_bone` take the nearest skinned vertex's
   weights and the bone is removed.
5. **Normalize:** prune < 0.01, keep the top **4** influences, renormalize to 1.0.
6. **Mirror check:** L/R weight maps match within tolerance for symmetric kids.

## R4 · Deformation QA (range of motion, pass/fail)

Pose each kid with world-space swings (`pose_test.py`, `curl_test.py`) and render close-ups,
textured and plain grey with back faces red (`geo_hand.py`):

Test every joint at **each limit** of `rig__joint_limits.json` and at half range, one axis at a
time, then one combined pose per limb. Measured gates (script them; renders confirm):

| Metric | Measured on | Pass |
|---|---|---|
| **Fold sharpness** (paper) | max dihedral angle between adjacent faces across the crease band | ≤ 75° at the flex limit |
| **Segment stretch** (noodle) | distance between rigid-core vertex rings of a segment, posed vs rest | changes ≤ 2% |
| **Core rigidity** | rigid-core vertices' distance to their bone axis, posed vs rest | changes ≤ 3% |
| **Volume** | cross-section area at the joint (outer side) vs rest | loss ≤ 20% (≤ 10% with correctives) |
| **Self-intersection** | faces crossing other faces of the same body part | none outside the inner crease |
| **Twist** | forearm/shin cross-section at 85° twist | no pinch (area loss ≤ 25%) |
| **Integrity** | open edges and back faces (red in `geo_hand.py`) | none new |

Visual checks per joint: shoulder (deltoid keeps volume, collar follows, no armpit tear), elbow
(outer point kept), wrist (twist spreads up the forearm), fingers (fist closes, knuckles keep
shape), hip (glute/groin keep volume, cloth follows), knee (kneecap keeps shape), spine and neck
(no shearing at waist or collar).
Order of fixes: weights (R3) → helpers (R2) → correctives (R5). Never fix two at once blind.

## R5 · Pose-space correctives

- Bake per joint at its flex limit (from `rig__joint_limits.json`): bend → `Corrective Smooth` (rest source = original
  coordinates) restores lost volume → take the posed delta → invert through the vertex's blended
  skinning matrix `Σ wᵢ Mᵢ·Bᵢ⁻¹` → shape key `corr_<Bone>` in rest space. Seam twins share
  the delta (`bake_correctives.py`).
- Tight folds (elbow inner crease) need gentle settings (≈ 5 iterations, factor 0.35) or the fold
  passes through itself; verify off/on renders (`corr_compare.py`).
- Store driver metadata as the mesh extra `kalyeah_correctives`: shape, bone, parent, rest angle,
  baked angle. Runtime weight = `clamp01((θ − θrest) / (θbaked − θrest))`.
- Budget: ≤ 8 corrective shapes per kid, deltas only on vertices > 0.5 mm, no correctives for
  joints that already pass R4.

## R6 · Export and import gate

- glTF export: skins on, morphs on, extras on, animations only if clips belong in the file,
  `export_image_format='AUTO'`, no `neutral_bone`.
- After `python tools/unity_export/sync_assets.py`, glTFast must import with zero errors; materials pass
  `KalyeahResources` sanitizing (`godot-unity-view`).
- EditMode tests pin the contract per kid: required `mixamorig:` bones exist, 5 digits per hand,
  ≤ 4 influences, height and sole at origin, `+Z` facing, triangle and texture budgets,
  `kalyeah_correctives` parse, every limited bone present in `rig__joint_limits.json`, no control
  bones or constraints left in the export.
- Hand off to `apex-anim-craft` only when R4 passes for the whole roster.
