# Kid rig repair (work in progress)

Blender 5.x `bpy` scripts that repair the auto-rigged kids in `assets/models/raw/` (run with a Python that has
the `bpy` module: `pip install bpy`). Outputs are not committed yet.

| Kid | Problem | Script and arguments |
|---|---|---|
| Domeng | 2 finger chains per hand (fused fingers) | `fix_fingers.py raw/Domeng.glb out/Domeng.glb Bone_019,Bone_024` |
| Oriang | 3 chains | `fix_fingers.py raw/Oriang.glb out/Oriang.glb Bone_019,Bone_024` |
| Manoy | 4 chains (one chain on two fingers) | `fix_fingers.py raw/Manoy.glb out/Manoy.glb Bone_018,Bone_023` |
| Paeng | 4-digit hand mesh | `make_pinky.py raw/Paeng.glb out/Paeng.glb Bone_019,Bone_024` (grows a textured pinky + chain) |
| Tere | humanoid rig, no finger bones | `fingers_from_scratch.py raw/Tere.glb out/Tere.glb LeftHand,RightHand` |
| others | 5 chains per hand already | — |

Checks: `inspect_chain.py <glb> <hand bone> out.png` colours the hand by chain (chains are listed
alphabetically); `look_hand.py` renders the textured hand from four sides.
`separate_fingers.py` (cut joined finger skin + fan the fingers) is experimental: its cut faces still pick up
dark texture.
