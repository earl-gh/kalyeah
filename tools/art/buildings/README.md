# The street's buildings: real roofs, sound structure, painted from their materials

How `assets/models/props/{nena_store,vulcanizing,barber_shop,house_1,house_2,house_3,tricycle_terminal}.glb` are made from the owner's
Meshy models (docs/ART.md "The street's buildings"). The karinderya is the owner's own model and is not touched.

```
mkdir -p work/<id>
git show 733a7d06:assets/models/props/nena_store.glb > work/nena_store/orig.glb   # the Nena store as the owner added it
git show 4adb4887:assets/models/props/<id>.glb > work/<id>/orig.glb               # the others before this rebuild
BPY=/path/to/python-with-bpy PY=python3 tools/art/buildings/run.sh <id> work
cp work/<id>/final.glb assets/models/props/<id>.glb                              # Godot extracts <id>_prop_base_color.jpg on import
```
`run.sh` rebuilds a building byte for byte from `config/<id>.json` and `signs/<id>.png`.

What the steps do:
- `geo_base.py`, `parts.py`, `features.py`: weld; every triangle's piece; each piece's box, facing and old colour (only to tell parts apart).
- `materials.py`: what each piece is made of (hollow block, concrete, coco lumber, plywood, GI sheet, steel, rubber, plastic, plant, ...).
- `roof_build.py`: takes off the floating shards, the old roof covering and loose framing ("drop_boxes"), and builds the roof as one is built:
  beams on the walls and posts, a ridge board, rafters every ~11 cm, fascia and barge boards, plywood gable ends, continuous corrugated GI
  sheets (real waves) with a ridge cap; straight hollow-block walls where the model had stacked boards ("walls"); posts ("posts").
- `uv_patch.py` / `shrink_only.py`: the old layout shrinks to 78% of the picture, the new parts take the right-hand column, the sign its
  band along the bottom. `paint3.py`: every texel painted from its piece's material in the building's palette; only the sign comes from the
  old picture (cleaned: `signs/<id>.png`, made with `sign_raw.py` + `sign_clean_generic.py` and `config/<id>_sign_tones.json`, or
  `sign_letter.py` for the tricycle terminal; the Nena store's with `tools/art/nena_store/sign_clean.py`).

How the configs were made (to change a building, edit its config or rerun these in the work dir, then `run.sh`):
`audit_drop.py` (floating shards), `roof_cfg.py` (each roof measured from side and front profiles, and the cut above which the old roof
comes off), `reapply.py` (palettes and the per-building fixes found by looking), `poke.py` (old parts that would pierce the new roof).
