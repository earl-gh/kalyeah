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

## R3 · Skinning and weight refinement

Order is fixed; each pass is idempotent:
1. **Digit split:** assign finger skin per digit by geometry (trace each digit tip→knuckle
   slice by slice; nearest centreline), not by the auto-rig's guess. Fused digits keep their mesh;
   the joining skin blends between neighbours.
2. **Joint falloff:** hinge joints (elbow, knee, finger) blend over ~1 joint radius centred on the
   crease loop, biased 60/40 toward the parent on the outside; ball joints (shoulder, hip) blend
   over ~1.5 radius with spine/clavicle sharing the deltoid/glute volume; wrist and ankle short
   and sharp.
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

| Joint | Test angles | Pass |
|---|---|---|
| Shoulder | raise 80°, forward 90°, back 30° | deltoid keeps volume, no armpit tear, collar follows |
| Elbow | flex 110–130° | inner crease folds without self-intersection; outer keeps point |
| Wrist | flex 60°, twist 90° | no candy-wrapper; twist spreads up the forearm |
| Fingers | curl 55° × 3 phalanges, splay 15° | fist closes, knuckles keep shape, no crack |
| Hip | flex 90°, abduct 45° | glute/groin keep volume, cloth follows |
| Knee | flex 110–130° | back of knee no collapse > 25% volume; kneecap keeps shape |
| Spine/Neck | bend 30°, twist 45° | no shearing at waist and collar |

Anything red, cracked or collapsed fails; fix weights first, then correctives, never both blind.

## R5 · Pose-space correctives

- Bake per joint at its test angle: bend → `Corrective Smooth` (rest source = original
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
  `kalyeah_correctives` parse.
- Hand off to `apex-anim-craft` only when R4 passes for the whole roster.
