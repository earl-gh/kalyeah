---
name: apex-rig-forge
description: >
  Animation-ready character pipeline: intake audit, mesh prep and rest-pose rebinding, the roster
  skeleton contract (shared hierarchy and rest rotations so one clip set drives every kid), joint
  limits with exported axes, twist and half-angle helper bones, skinning against paper folds and
  noodle limbs, cloth-vs-body, measured range-of-motion QA, pose-space correctives, face rig,
  LODs, and the glTF→glTFast import gate (Epic Fortnite × Riot Valorant character-tech bar). Use
  when a kid .glb arrives or changes, when joints deform badly, before clips are retargeted or
  shared, or when reviewing rig, weight or corrective work.
---

# Apex Rig Forge

Turn an owner-authored kid into a rig that animates like **Epic (*Fortnite*)** and **Riot
(*Valorant*)** characters: one skeleton for the whole roster, joints that bend round (never like
paper, never like noodles), anatomical limits, zero cracks, mobile budgets.

**Boundary:** owns the asset from intake to an import-clean `.glb` plus its rig data. Defers clip
work and runtime playback to `apex-anim-craft`, materials/shaders and `KalyeahResources` to
`godot-unity-view`, props to `apex-street-world`.

**Owner rule (`CLAUDE.md` §Assets):** the owner makes models. Repair rig, weights, helpers and
correctives when asked; never re-sculpt, re-texture or generate a character, face shapes included.
A fix that needs modelling (missing digit, fused geometry, missing joint loops, face shapes) is
shown with renders and offered as scripted repair *or* owner remodel.

**Tooling:** Blender `bpy` scripts in `tools/blender/kids/` (`README.md`: order and arguments),
re-runnable from `assets/models/raw/`; never hand-edit outputs. Probes: `dump_tree.py` (bone tree
with world positions), `tips.py` (digit tips per hand), `inspect_chain.py` (hand coloured per
chain), `look_hand.py`/`geo_hand.py` (textured / grey with back faces red), `pose_test.py`,
`curl_test.py`, `corr_compare.py`. A tool a step needs but `tools/blender/kids/` lacks is built
first, then the step runs.

## R0 · Intake audit (report before touching anything)

- **Transforms:** object and armature scale 1.0 (flag 0.01 cm rigs), rotations applied, mesh
  height vs `CharacterDef`, soles at z = 0, facing glTF +Z (Blender −Y), character left at
  Blender +X.
- **Skeleton:** roots above `Hips`, chains, digits per hand (tips at hand height), metacarpal
  carriers, extra chains (hair/skirt/cape), scale keys, `neutral_bone`.
- **Skin:** groups without bones, unweighted vertices, > 4 influences, cross-side weights, seam
  twins (glTF splits vertices at UV seams) with unequal weights.
- **Mesh:** triangles, vertices after splits, materials, texture sizes, edge loops per joint,
  fused or missing digits.
- **Clips:** names, fps, length, root motion.

## R1 · Mesh prep and rest pose

- One skinned mesh per kid where possible (each extra mesh is another draw call and skin pass).
- **Rest pose (the roster's shared bind pose):** A-pose, upper arms 40° below horizontal, palms to
  thighs, elbows 5° and knees 3° pre-bent in their bend direction, fingers relaxed and slightly
  curled, feet straight forward at hip width.
- **Rebinding to a new rest pose:** pose the armature → duplicate the mesh and apply its Armature
  modifier (the shape in the new pose) → `Apply Pose as Rest Pose` → re-parent with the original
  weights (`Data Transfer` of vertex groups from a pre-pose copy) → regenerate inverse bind
  matrices on export → verify every bone head sits inside its joint volume (offset joints stretch
  limbs). Shape keys must be re-baked after a rebind.
- **Joint topology:** 3 loops at elbows/knees (middle loop on the crease), 2–3 at wrists,
  shoulders, hips, a fan at the knuckles. Missing loops cannot be fixed by weights; report.
- **Budgets (defaults until the owner sets them; 10 kids on screen, mid-tier Android):** match
  LOD ≤ 25k triangles, showcase LOD0 ≤ 60k, ≤ 75 deforming bones, one 2048 base-colour atlas
  + optional normal/ORM 1024 (ASTC/ETC2), ≤ 2 materials. Over budget → plan to the owner, never
  silent decimation.
- **LODs:** LOD1 at ~50% triangles (`Decimate` collapse on a seam-welded copy, UV seams locked),
  LOD2 at ~25%; weights carried by `Data Transfer` (nearest face interpolated) then R3 steps 4–5;
  LOD1 drops finger chains 2–3 (merged into chain 1), LOD2 keeps one bone per digit group
  (thumb, fingers); same bone names so clips still bind.

## R2 · Roster skeleton contract

**Hierarchy (identical paths for every kid, so one clip set binds to all):**

| Region | Path |
|---|---|
| Root | `Armature` → `mixamorig:Hips` (no other bones between) |
| Core | `Hips` → `Spine` → `Spine1` → `Spine2` → `Neck` → `Head` → `HeadTop_End` |
| Arm ×L/R | `Spine2` → `Shoulder` → `Arm` → `ForeArm` → `Hand` |
| Twist ×L/R | `ArmTwist` (child of `Arm`), `ForeArmTwist1`, `ForeArmTwist2` (children of `ForeArm`) |
| Helpers ×L/R | `ForeArmHelper` (elbow), `LegHelper` (knee), `HandHelper` (wrist), parented to the joint's parent bone |
| Leg ×L/R | `Hips` → `UpLeg` → `Leg` → `Foot` → `ToeBase` → `Toe_End` |
| Digits ×L/R | `Hand` → `Hand{Thumb,Index,Middle,Ring,Pinky}{1,2,3}` → `…Tip` (exactly 5, thumb from the wrist side) |
| Extras | hair/skirt/cape chains hang as leaf branches under their body bone; ≤ 4 chains × ≤ 4 bones |

- Metacarpal carriers and auto-rig roots are removed after their weights move to the nearest
  contract bone (paths must match exactly; an extra parent breaks shared clips).
- **Rest rotations:** every contract bone's rest orientation is the same across the roster (only
  lengths and positions differ), so a clip's local rotations mean the same pose on every kid.
  Set heads from the kid's joints, then copy roll and direction from the reference rig.
- **Rolls:** finger bend axis faces the palm on both hands; elbow/knee hinge axis perpendicular to
  the limb plane; L/R mirrored. No scale or shear in rest; `_End` bones carry no weights.
- **Twist bones** take a share of the parent's twist about the bone axis: `ArmTwist` 50% of
  `Arm`, `ForeArmTwist1` 33%, `ForeArmTwist2` 66% of the hand's twist; the hand keeps its
  swing. Weights ramp along the forearm 0 → 33 → 66 → 100% toward the wrist.
- **Half-angle helpers** follow 50% of their joint's swing (`Copy Rotation` mix 0.5). They are
  baked into every clip; at runtime only IK-modified joints re-evaluate them (`apex-anim-craft`).

### Joint limits (`unity/Data/rig__joint_limits.json`, the single source)

Ranges in degrees on three axes per bone: **flex** (+ toward palm/front), **abduct/splay**,
**twist** (about the bone). The export script writes, per bone, these axes as unit vectors in the
model's rest space next to the ranges, so Blender, Unity (after glTFast's handedness change) and
the clip scan use the same axes; nothing hard-codes a local X/Y/Z.

| Joint | Flex (−ext … +flex) | Abduct | Twist |
|---|---|---|---|
| Spine, Spine1, Spine2 (each) | −15 … 25 | ±15 | ±20 |
| Neck / Head | −40 … 40 / −25 … 25 | ±30 / ±15 | ±50 / ±30 |
| Shoulder (clavicle) | −20 … 20 | −10 … 30 | 0 |
| Arm | −50 … 160 | −30 … 140 (clavicle adds the rest) | ±80 |
| ForeArm (hinge) | −5 … 145 | 0 | ±85 (shared with twist bones) |
| Hand | −60 … 70 | −20 … 35 (radial … ulnar) | ±80 at the source, redistributed to forearm twist bones |
| UpLeg | −20 … 125 | −25 … 45 | ±40 |
| Leg (hinge) | −3 … 140 | 0 | ±10, only while flexed > 20° |
| Foot / ToeBase | −20 … 45 / −30 … 50 | ±30 / 0 | 0 |
| Finger 1 / 2 / 3 | −15 … 90 / 0 … 100 / −5 … 80 | ±15 (index, pinky ±20) / 0 / 0 | 0 |
| Thumb 1 / 2 / 3 | −10 … 50 / 0 … 60 / −10 … 80 | ±60 / 0 / 0 | ±30 / 0 / 0 |

**Per-kid overrides** (`overrides.<Kid>` in the same file) only narrow a range, never widen it:
a joint's flex limit ends where its parts first touch plus the squash allowance (R4 contact gate),
so a chubby kid's elbow or knee stops earlier than a slim one's.

Clamp by **swing–twist decomposition** (extract twist about the twist axis, clamp; clamp the
swing inside the flex×abduct ellipse); never clamp Euler channels separately.

### Animation control rig (Blender authoring only, stripped on export)

- Control bones drive the deform bones by constraints; export bakes the deform bones and removes
  every control bone and constraint.
- **FK/IK** per arm and leg: 2-bone `IK` (chain length 2, stretch 0) with a **pole target** on the
  elbow/knee plane, an FK/IK blend property with snap both ways (no pop on switch); reverse-foot
  pivots (heel, ball, toe, bank).
- `Limit Rotation` (local space) on every controlled bone and matching `ik_min/ik_max` plus
  `lock_ik_*` on hinge axes, all from the limits file.
- Fingers FK only, with per-hand `curl`/`spread` drivers.

## R3 · Skinning (fixed order, each pass idempotent)

1. **Digit split** by geometry (trace each digit tip→knuckle; nearest centreline); fused digits keep
   their mesh and the joining skin blends between neighbours.
2. **Rigid cores:** the middle 50–60% of every limb segment is 100% its bone (weights bleeding
   along a segment make noodle limbs).
3. **Blend bands** centred on the crease loop, 0.6–1.5 × the joint radius (narrower folds like paper,
   wider bends like a noodle): hinges ~1.0 × biased 60/40 to the parent on the outside; ball joints
   ~1.5 × with clavicle/spine sharing deltoid/glute; wrist/ankle ~0.7 ×. Crease-band vertices
   take the half-angle helper as one influence; forearm vertices take the twist ramp (R2).
4. **Layer binding (no gaps, no poke-through):** every stacked layer (shirt, shorts, bracelet,
   strap, hair over the neck) copies its weights from the surface directly beneath it (`Data
   Transfer`, nearest face interpolated, then the layer's own extra chains), so layer and skin
   move by the same matrices and their rest gap never changes. Skin that sits fully under a
   layer and can never be seen is offered to the owner for removal or a 2–3 mm inset (modelling
   change, owner's call); until then it stays bound to the layer's weights.
5. **Cloth vs body:** skirt/shorts hems follow the thigh they cover (≥ 70% `UpLeg` near the hem,
   the hips above) with the skirt chain for the rest; loose cloth gets capsule colliders on
   `UpLeg`/`Leg` for spring bones (`apex-anim-craft`); clipping at the R4 hip/knee limits fails.
6. **Smoothing** only inside joint zones (factor 0.35–0.5, 2–3 iterations); never global.
7. **Integrity (no exploding skin):** seam twins identical; no opposite-side influence; remove any
   influence whose bone segment is farther from the vertex than 1.5 × the local limb radius (stray
   influences throw vertices out as spikes when that bone moves); `neutral_bone` vertices take the
   nearest skinned vertex's weights and the bone is removed.
8. **Normalize:** prune < 0.01, top **4** influences (helpers and twist bones count), renormalize.
9. **Mirror check:** L/R weight maps match within tolerance on symmetric kids.

## R4 · Range-of-motion QA (measured, `rom_metrics.py`)

Pose every joint at each limit and half range, one axis at a time, then one combined pose per limb;
measure, then confirm with renders (textured and grey/red):

| Metric | Measured on | Pass |
|---|---|---|
| Fold sharpness (paper) | angle between face normals of adjacent faces, outer side and crease edges (skin-to-skin contact zone excluded) | ≤ 60° at the flex limit |
| Segment stretch (noodle) | distance between the rigid-core vertex rings of a segment | ≤ 2% change |
| Core rigidity | rigid-core vertices' distance to their bone axis | ≤ 3% change |
| Volume | joint cross-section area, outer half | ≤ 20% loss (≤ 10% with correctives) |
| Twist | forearm/shin cross-section at full twist | ≤ 25% area loss |
| Self-intersection | faces crossing faces of the same body part | none outside the contact zone |
| Cloth | hem/skirt faces inside the leg | none at hip/knee limits |
| Integrity | open edges, back faces | none new |
| Spikes (exploding) | each vertex's posed offset from its neighbours' average vs the same at rest | ≤ 3 × the mesh median; no vertex outside the posed limb's capsule + 2 cm |
| Layer gap | distance from each layer vertex to the skin beneath, along the skin normal | stays within the rest gap ± 2 mm; skin never in front of a layer |
| Contact between parts | penetration depth between different parts (forearm↔upper arm, calf↔thigh, arm↔torso, thigh↔thigh, hand↔thigh) | touching allowed; ≤ 3 mm deep (squash zone), else narrow the per-kid limit or add a squash shape |

Contact poses join the limit poses: elbow and knee at full flex, arms down in the rest A-pose and
in the widest clip pose, hand on hip and thigh, feet together, both legs at full hip flex.

Fix order: weights (R3) → helpers and twist (R2) → correctives (R5), one at a time.

## R5 · Correctives

- Bake each at its joint's limit: bend → `Corrective Smooth` (rest source = original coordinates)
  → posed delta → back to rest through the vertex's blended skinning matrix `Σ wᵢ·Mᵢ·Bᵢ⁻¹` →
  shape key `corr_<Bone>[_<Direction>]`; seam twins share the delta. Tight folds use gentle settings
  (≈ 5 iterations, factor 0.35) so the inner crease never passes through itself.
- **Drivers** (mesh extra `kalyeah_correctives`, one entry per shape): hinge shapes read the bend
  about the stored hinge axis; ball joints get one shape per direction (`_Up`, `_Fwd`, `_Back`) read
  as the swing's angle toward that stored direction (cone reader); weight =
  `clamp01((θ − θstart) / (θfull − θstart))`.
- **Combinations:** where two shapes of one limb overlap (shoulder `_Up` + elbow), bake a combination
  shape at both limits, driven by the product of both weights, holding the difference between the
  combined fix and the sum of the singles. Per-vertex total corrective offset is capped at the
  largest single delta.
- **Squash shapes** for contact: at the contact pose, vertices of both parts that penetrate are
  pushed back to the shared contact surface (the mid-plane between the parts) and their
  neighbours relaxed, giving a flattened, compressed look like real skin pressing on skin; baked
  and driven like any corrective (`corr_<Bone>_Squash`).
- Budget ≤ 10 shapes per kid; deltas only where > 0.5 mm; none on joints that pass R4 without them.

## R6 · Face rig (owner-made shapes, wired here)

- Minimum set for Fortnite-grade readability: `blink_L`, `blink_R`, `smile`, `open`, `angry`,
  `surprised`, `hurt` shape keys (owner sculpts them), or jaw/eye bones if the owner prefers
  bones. Eyes, if separate meshes, parent to `Head`.
- Shapes are sculpted on the rest mesh, only on face vertices, no seam splits left unwelded.

## R7 · Export and import gate

- glTF: skins, morphs, extras on; the contract hierarchy only (no control bones, constraints or
  `neutral_bone`); `export_image_format='AUTO'`; the limits-axis data written beside the model.
- Unity: after `python tools/unity_export/sync_assets.py`, glTFast imports with zero errors;
  `QualitySettings.skinWeights = FourBones`; GPU skinning on; materials pass `KalyeahResources`.
- EditMode tests per kid: exact contract paths and rest rotations within 1° of the reference, 5
  digits per hand, ≤ 4 influences, height and soles, +Z facing, budgets, LOD bone sets,
  `kalyeah_correctives` and limits-axis data parse, no control bones left.
- Hand off to `apex-anim-craft` only when R4 passes for the whole roster.
