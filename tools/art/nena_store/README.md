> Superseded by `tools/art/buildings/` (2026-11-10): the store is rebuilt there with a real roof and painted from its materials. Kept for `sign_clean.py`, which made its cleaned sign.

# Nena store: rebuild and repaint

How `assets/models/props/nena_store.glb` is made from the owner's Meshy model (docs/ART.md, "The Nena store, polished").
Python 3 with numpy and Pillow; Blender's `bpy` for the `build_geo.py` steps. Run from this folder.

```
git show 733a7d06:assets/models/props/nena_store.glb > meshy_nena_store.glb   # the owner's model as it was added
git show 9e2df1a4:assets/models/props/nena_store_prop_base_color.jpg > old.jpg  # its picture, gutters refilled and leaks cleaned
FONT=../../../assets/fonts/clash/Clash-Regular.otf
mkdir -p a b && cp old.jpg a/prop_base_color.png   # (any format; the image keeps the name prop_base_color)
# 1. the model with its old UVs: tells the parts apart and holds the sign as it was
python build_geo.py -- meshy_nena_store.glb a/prop_base_color.png geo.glb
python parts.py . geo.glb piece.npy > /dev/null
python features.py . geo.glb piece.npy features.json
python classify.py features.json classes.json                     # wood, hollow-block wall, roof sheet, sign, goods...
python sign_clean.py . geo.glb old.jpg 1425 303 sign_raw.png sign_clean.png
# 2. the model with the sign's front given its own band of the picture (everything else shrinks to 84%)
python build_geo.py -- meshy_nena_store.glb a/prop_base_color.png geo2.glb --remap
python parts.py . geo2.glb piece2.npy > /dev/null
python features.py . geo2.glb piece2.npy features2.json
python match_classes.py features.json classes.json features2.json classes2.json
python paint.py . geo2.glb piece2.npy classes2.json $FONT b/prop_base_color.png 2048 sign_clean.png
python build_geo.py -- geo2.glb b/prop_base_color.png nena_store.glb
```

Godot extracts the picture to `nena_store_prop_base_color.jpg` on import. `paint.py` paints every texel from its 3D place on the model;
only the sign's front comes from the old picture (cleaned by `sign_clean.py`).
