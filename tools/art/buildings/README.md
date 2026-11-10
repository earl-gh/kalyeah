# The street's buildings: rebuild and repaint

The same steps as the Nena store (`tools/art/nena_store/`), made general for `vulcanizing`, `barber_shop`, `house_1`, `house_2`,
`house_3` and `tricycle_terminal` (docs/ART.md "The other buildings"). The karinderya is the owner's own model and is not touched.

```
mkdir -p work/<id> && git show 4adb4887:assets/models/props/<id>.glb > work/<id>/orig.glb   # the owner's model before the repaint
BPY=/path/to/python-with-bpy PY=python3 tools/art/buildings/run_one.sh <id> work
cp work/<id>/final.glb assets/models/props/<id>.glb     # Godot extracts <id>_prop_base_color.jpg on import
```

What each step does:
- `geo_base.py`, `parts.py`, `features.py`: weld the mesh; every triangle's piece; each piece's box, facing and old colour.
- `audit_drop.py` (once, writes `config/<id>.json` "drop"): floating shards, groups of 3 triangles or fewer that touch nothing standing.
- `paint_generic.py`: the new picture in the model's own layout, piece by piece: hollow-block walls (and the upright faces of a big dull shell;
  windows and doors painted on a wall keep their own colours), roofs with ridges and rust, wood with grain, pavers, everything else its own
  colours cleaned into one to four flat tones (grime folded into the surface's colour).
- `sign_raw.py`, `sign_clean_generic.py` (`config/<id>_sign_tones.json`: the sign's tones, grime painted as the board), `sign_letter.py`
  (the tricycle terminal's sign, too blurred to clean, lettered again in its colours), `compose.py` (the picture shrunk to 84% and the sign in
  its own band), `build_bld.py` (weld, drop the shards, hard edges over 30 degrees, the picture), `uv_patch.py` (the UVs moved in place).
- `config/<id>.json`: "sign" (the sign board's pieces), "drop" and "drop_boxes" (the shards), optional "classes" overrides.
