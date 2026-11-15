# Kid rig repair (work in progress)

Blender 5.x `bpy` scripts that repair the auto-rigged kids in `assets/models/raw/` (run with a Python that has
the `bpy` module: `pip install bpy`). Outputs are not committed yet.

| Kid | Problem | Steps |
|---|---|---|
| Paeng | 4-digit hand mesh | `tube_fingers.py raw/Paeng.glb out/Paeng.glb Bone_019,Bone_024 pinky` (pinky in the finger row, welded into the hand) |
| Domeng | fused fingers, 2 chains | `fix_fingers.py raw/Domeng.glb tmp/Domeng.glb Bone_019,Bone_024` then `tube_fingers.py tmp/Domeng.glb out/Domeng.glb Bone_019,Bone_024 rebuild` |
| Oriang | fused fingers, 3 chains | same two steps as Domeng |
| Jopay | fused fingers | `tube_fingers.py raw/Jopay.glb out/Jopay.glb Bone_019,Bone_024 rebuild` |
| Manoy | 4 chains (one on two fingers) | `fix_fingers.py raw/Manoy.glb out/Manoy.glb Bone_018,Bone_023` |
| Tere | humanoid rig, no finger bones | `fingers_from_scratch.py raw/Tere.glb out/Tere.glb LeftHand,RightHand` |
| others | 5 chains per hand already | — |

`rebuild` removes fused finger skin past the knuckles, caps the knuckles and grows clean fingers along each
chain (buried in the palm, one even skin tone each). `pinky` welds the new finger with an exact boolean.

Checks: `inspect_chain.py <glb> <hand bone> out.png` colours the hand by chain (chains are listed
alphabetically); `look_hand.py` renders the textured hand from four sides; `geo_hand.py` renders plain grey
with back faces in red (holes and flipped faces show up red).
