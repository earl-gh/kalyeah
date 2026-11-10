# Nena store: rebuild and repaint

How `assets/models/props/nena_store.glb` was made from the owner's Meshy model (docs/ART.md, "The Nena store, polished").
Python 3 with numpy and Pillow; Blender's `bpy` for the two `build_geo.py` steps. Run from this folder.

```
git show 733a7d06:assets/models/props/nena_store.glb > meshy_nena_store.glb   # the owner's model as it was added
SRC=meshy_nena_store.glb
FONT=../../../assets/fonts/clash/Clash-Regular.otf
python build_geo.py -- $SRC prop_base_color.png geo.glb          # weld, clean, take off the floating pallet, hard edges over 30 deg
python parts.py . geo.glb piece.npy > /dev/null                   # every triangle's piece (corners joined by position)
python features.py . geo.glb piece.npy features.json              # each piece's box, facing and old colour
python classify.py features.json classes.json                     # each piece's part: wood, plaster, roof sheet, sign, goods...
python paint.py . geo.glb piece.npy classes.json $FONT out/prop_base_color.png 2048
python build_geo.py -- geo.glb out/prop_base_color.png nena_store.glb
```

The first `build_geo.py` takes any picture named `prop_base_color.png` (the image keeps that name, so Godot extracts it to
`nena_store_prop_base_color.jpg`). `classify.py` reads the old picture's colours only to tell parts apart; `paint.py` never copies it.
