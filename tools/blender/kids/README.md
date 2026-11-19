# Kid rig repair (work in progress)

Blender 5.x `bpy` scripts that repair the auto-rigged kids in `assets/models/raw/` (run with a Python that has
the `bpy` module: `pip install bpy`). Outputs are not committed yet.

| Kid | Problem | Steps |
|---|---|---|
| Paeng | 4-digit hand mesh | `tube_fingers.py raw/Paeng.glb out/Paeng.glb Bone_019,Bone_024 pinky` (pinky in the finger row, welded into the hand) |
| Domeng | fused fingers, 2 chains | `fix_fingers.py raw/Domeng.glb out/Domeng.glb Bone_019,Bone_024` (mesh kept, weights rebuilt) |
| Oriang | fused fingers, 3 chains | `fix_fingers.py raw/Oriang.glb out/Oriang.glb Bone_019,Bone_024` |
| Jopay | fused fingers, messy weights on 5 chains | `fix_fingers.py raw/Jopay.glb out/Jopay.glb Bone_019,Bone_024 force` |
| Manoy | 4 chains (one on two fingers) | `fix_fingers.py raw/Manoy.glb out/Manoy.glb Bone_018,Bone_023` |
| Tere | humanoid rig, no finger bones | `fingers_from_scratch.py raw/Tere.glb out/Tere.glb LeftHand,RightHand` |
| others | 5 chains per hand already | — |

`fix_fingers` keeps the authored mesh: it splits the finger skin among four chains traced from the fingertips,
then smooths the weights across finger borders, gives seam-split twin vertices identical weights (no cracks
when bending), normalizes, keeps at most 4 bones per vertex, and turns every finger bone's bend axis to the
palm on both hands. `tube_fingers ... pinky` welds a new finger with an exact boolean (`rebuild` mode is
unused: the owner kept the authored fused fingers).

Then, for every kid (fixed file or raw): `normalize_kid.py in.glb norm/<Kid>.glb`. It renames the skeleton to
the shared humanoid names the game's kids use (`mixamorig:Hips`, `mixamorig:LeftForeArm`, `mixamorig:LeftHandIndex1`,
...; hair, skirt and cloth chains keep their own names), gives vertices tied to the exporter's `neutral_bone` their
neighbours' weights, smooths weights around the shoulders, elbows, wrists, hips, knees, spine and neck, gives
seam-split twin vertices identical weights, prunes weights under 1%, normalizes, and keeps at most 4 bones a vertex.
`pose_test.py norm/<Kid>.glb pose/<Kid>` poses the arm, wrist, fist and knee and renders the joints close up.

Last: `bake_correctives.py norm/<Kid>.glb corr/<Kid>.glb` adds pose-space corrective shape keys `corr_LeftForeArm`,
`corr_RightForeArm` (elbows, gentle), `corr_LeftLeg`, `corr_RightLeg` (knees) and `corr_LeftArm`, `corr_RightArm`
(shoulders). Each joint is bent (110 degrees, shoulders raised 80), Blender's Corrective Smooth restores the volume the
skinning loses relative to the rest shape, the fix is carried back to rest space through the skinning matrices, and
seam twins share it. The mesh extra `kalyeah_correctives` lists every shape's bone, its parent and the rest and
baked bend angles, so the game can drive each shape's weight from the joint's bend (not wired yet).
`corr_compare.py` renders a bent elbow and knee with the shape off and on.

Checks: `inspect_chain.py <glb> <hand bone> out.png` colours the hand by chain (chains are listed
alphabetically); `look_hand.py` renders the textured hand from four sides; `geo_hand.py` renders plain grey
with back faces in red (holes and flipped faces show up red); `curl_test.py` curls one hand into a fist.

## Shared rest pose and real-clip check (current path)

1. `build_reference.py characters/junjun.glb ref_rest.json`: the roster's reference rest frames.
2. `SKIP=hinge,helper,twist,stray contract_kid.py norm/<Kid>.glb ref_rest.json contract/<Kid>.glb contract/<Kid>_rig.json`:
   strips path-breaking bones, lowers the arms to the reference A-pose, copies the reference rest frames and adds
   (unweighted) twist/helper bones. The `SKIP` list keeps the authored weights: the extra weight passes in the
   script tore skirts under a real run and stay off until they pass the clip check.
3. `clip_preview.py characters/junjun.glb contract/<Kid>.glb Run out/<Kid> 3`: plays a real game clip on the kid
   and renders a frame strip. Judge deformation with real clips, never with one-joint stress poses.
   Run one kid at a time (two Blender renders in parallel exhaust the session's memory).
   `clip_preview.py` reads `ref_rest.json` (same folder, or `REF=<path>`): clip rotations are measured from the
   roster reference pose. Measuring them from the source's own rest left every arm 30 degrees too low (arms
   inside the body, no swing, stretched sleeves). `FRONT=1` renders from straight ahead.
   `contract_kid.py` also fixes bend directions in the rest pose: it straightens the authored elbow pre-bend,
   rolls each arm until its elbow crease folds the forearm forward (as the clips expect) and turns each palm to
   the reference (at most 30 degrees at the wrist; the log prints what was left over). The `core` pass is on:
   limb middles ease toward 100% their bone, cloth untouched. `anat_measure.py norm.glb contract.glb ref_rest.json`
   prints palm roll, elbow plane and foot yaw mismatch; `CLOSE=LeftForeArm clip_preview.py ...` renders a close-up.

## Game-ready kids (current path)

`PY=<python with bpy 5.x> build_all_kids.sh <work dir> [Kid ...]` builds every kid in `assets/models/raw/` into
`<work>/out/<kid>.glb`, then copy the `.glb` and its `<kid>_normal.png` (as `<kid>_2.png`) into
`assets/models/characters/`. Per kid: the hand repairs above, `normalize_kid.py`, `contract_kid.py`, then
`retopo_kid.py`, which does what a character modeller would:

1. cleans the source (welds duplicates, drops loose geometry, outward normals); it stays the bake source;
2. retopology: a watertight voxel shell, then QuadriFlow quads with X symmetry at the 20k-triangle budget, shrinkwrapped
   back onto the source and relaxed (QuadriFlow rejects edges under 1e-4, so it runs at 100x scale with the degenerate
   edges collapsed first);
3. UVs by angle with padding, the head and face at 1.8x the body's texel density;
4. bakes colour and a tangent-space normal map from the source (cage offset and ray reach sized to the kid);
5. transfers the source's repaired skin weights, smooths them, prunes under 1%, caps at 4 bones, normalizes;
6. retargets the 20 roster clips from `clips/roster_clips.glb` (the old code-made kid that carried them) onto the kid;
7. exports one `.glb`: low mesh, skeleton, clips, base colour JPEG 2048 and normal PNG 1024.

`render_kid_art.py <kid>.glb <dir> <id>` renders the portrait, face and full body; `tools/art/compose_kid_cards.py`
turns them into the portraits, HUD faces, cards, character cards and avatars the UI loads.
