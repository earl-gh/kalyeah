# Trumpo (spinning top), the Filipino rope-and-wood one

- **Kind and why:** Blender (3D model). It is thrown, spins about its vertical axis and is seen from the 45 degree camera, so it needs real depth, a rope cone that shows spiral turns and a wood dome that shows grain. This is advice for the owner's artist; nothing here is generated.
- **What it is today:** `assets/models/weapons/trumpo.glb` is the owner's earlier Meshy model, reshaped by instruction (vertices moved only: a flattened dome over a tapering cone) and its colours swapped to wood and cream. It cannot show spiral rope turns: the mesh has too few rings in the cone. A full match to the real object needs a proper model.
- **Reference:** the owner's photo of a pinoy trumpo (pale wooden dome, thick cream cotton rope wound in tight spiral turns on the cone, a free rope tail).
- **Shape (proportions measured on the photo, as fractions of the whole height):**
  - Dome: a flattened sphere, widest radius about 0.35 of the height (width 0.70), standing about 0.40 above the rope's rim, rounded top (no knob or peg).
  - Rope cone: from a blunt tip (radius about 0.045) up to a mouth about 0.27 in radius at 0.60 of the height; nearly straight sides. The dome overhangs the rope's top turn.
  - Rope: about 10 to 12 turns from tip to rim, each a thick rounded cord (a gentle helical ridge, 0.012 to 0.02 of the height proud), with the cord's strands slanting across each turn. Optional: the free end as a short thick tail hanging from the rim (about 0.3 long); the thrower's bottle cap in the photo is NOT part of the top.
- **Colours:** wood dome pale orange-tan `#D19454` with darker grain streaks `#9E6330` along the dome's meridians and a few tiny dark scratches; rope cream `#EDE3C7` with darker twist lines `#B8A88C`. No paint, no stripes. Flat and painted, not photographic (see `weapon-textures.md`: 3 to 6 flat colours, painted light, no gloss).
- **Technical:** one mesh, one material, a 1024 x 1024 base colour texture with a clean UV layout (few big islands), at most 3,000 triangles, axis vertical, the tip on the ground at the origin, one object (the game scales it to a 1.12 m longest side and spins it about Y).
- **Drop it in:** `python3` `tools/blender/build_prop_model.py in.glb out.glb <triangles> 1024 keep_uv`, then replace `assets/models/weapons/trumpo.glb` (same name, same rules as today). Check the picture first with `python3 tools/art/check_weapon_texture.py picture.png`.
- **How it is accepted:** at 390 px wide, from the game camera, the cone's turns and the wood dome read as a Filipino top and not a generic one; one turn of the spin (6 turns a second) shows the rope spiral moving.
- **Priority:** P2.
