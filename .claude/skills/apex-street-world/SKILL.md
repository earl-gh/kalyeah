---
name: apex-street-world
description: >
  Stylized 3D Filipino barangay arena art direction, three-band vertical value/frequency
  hierarchy, 40 m phone sightline clarity, and grounded urban chunkiness (Riot Valorant × Epic
  Fortnite × Valve Deadlock). Use when building or reviewing 3D street layouts, props, cover,
  materials, shaders, or arena lighting.
---

# Apex Stylized Street World

Fuse **Riot (*Valorant*)** chest/head-height value discipline and sightline honesty, **Epic
(*Fortnite*)** chunky stylized 3D forms and material separation, and **Valve (*Deadlock*)**
readable urban lanes and neighborhood landmarks for Kalyeah's Filipino barangay arenas.

**Boundary:** Owns 3D barangay layout, sightlines, cover readability, vertical value/frequency
hierarchy, material roughness contrast, and arena lighting. Leaves 3P camera/combat feel to
`apex-combat-cam`, 2D HUD/menus to `apex-mobile-hud`, and frame/VRAM/GC budgets to
`apex-net-frame`.

## 1. Three-Band Vertical Value & Frequency Hierarchy

Phone screens (`5"–6.5"`) turn uniform texture detail into muddy noise. Enforce strict vertical
value and detail separation from street level to rooftops:

| Band | Height Zone | Albedo Value | Surface & Detail Rule |
|---|---|---|---|
| **1. Ground** | `0 m` street, bridges, shallow canal | `30–45%` warm mid-dark | Calm painted asphalt/concrete (`barangay_ground`, `estero_water`), minimal micro-grit so AoE rims, `MoveMarker`, court chalk, `Merienda` stands (`(-34,0)`, `(0,0)`, `(+34,0)`), base circles, and half-alpha `Rescue area` countdowns pop in noon sun |
| **2. Combat Band** | `0.4 m–1.8 m` kid knee-to-head | `45–62%` clean mid-tone | Broad low-noise plaster, weathered wood, and cardboard behind players; ban brick grids, dark window bars, or loud posters here so `blue`/`red`-outlined `1.52 m` kids (`arcane_ink_outline`) and flying slippers/cans/rocks read in `< 100 ms` at `40 m` |
| **3. Pinoy Canopy** | `> 2.4 m` & beyond sidewalk curbs | Full chroma & contrast | Concentrate *sari-sari* tarpaulins, jeepney paint motifs, fiesta *banderitas*, *sampayan* laundry, *capiz* upper windows, GI/bahay-kubo roofs, and `SpaghettiWires` overhead—unmistakable *kalye* soul up high, tournament-clean combat lane below |

## 2. Sightlines, Landmarks & Stylized Chunkiness

- **Sub-`0.5 s` camera orientation:** Across the `168 m`-diameter (`arena_radius = 84.0 m`)
  barangay—and the `24×48 m` classic street—anchor opposing halves and lanes with distinct
  architectural silhouettes and palette temperatures (`South Covered Court` warm ochre/terracotta
  vs `North Barangay Hall Plaza` cool teal/cream, unmistakable NW vs SE waiting-shed rescue
  zones, and flyered concrete electric-post bases). Spinning the 3P shoulder camera must orient
  the player in `< 0.5 s` without checking the minimap.
- **Chunky beveled forms & material separation:** Author bold, toy-chunky silhouettes with
  generous `3–5 cm` bevels that catch clean specular rims; ban `< 2 cm` razor-thin geometry or
  raw `1 px` edges that shimmer and alias on mobile GPUs. Separate matte plaster/concrete,
  satin painted wood/cardboard, and crisp metallic roofs/cans via distinct roughness steps
  rather than noisy diffuse grunge.
- **Honest cover footprints:** Cover props (crates, benches, sawhorses, cardboard walls, waiting
  sheds) must match authoritative sim collision (`player_radius = 0.45 m` clearance) with zero
  invisible snags or misleading sightline gaps.
- **Grounded barangay anatomy (no "parking-lot slabs"):** Seat every building and prop into real
  street structure—raised sidewalks (where `Tagged` kids wait facing inward), painted curbs,
  shallow drainage gutters, bridge ramps, and soft contact AO/decals. Never drop isolated boxes
  onto a featureless grey slab.
- **Tropical daylight without crushed blacks:** Pair warm golden directional sunlight with cool
  sky-blue hemisphere fill so shadowed walls stay readable at `18–22%+` luminance—never
  `#000000` black holes that hide dark outfits or `Papel` traps, and never blown-out `#FFFFFF`
  concrete glare that erases white `Rescue area` ground timers.

## 3. Anti-Bloat & Owner Art Rule

- Construct arenas from modular *tabi-tabing bahay* footprints, shared trim sheets/texture
  atlases, baked/single-directional lighting, and pre-merged static `ArrayMesh` batches
  (`SpaghettiWires`). Ban real-time GI, screen-space reflections, runtime city generators, and
  bespoke per-prop shaders.
- **Advise, never generate art assets (`CLAUDE.md` binding rule):** Whenever a prop, building,
  ground decal, or backdrop is needed, specify its exact asset class (**Blender 3D `.glb`**,
  **2D sprite sheet**, or **flat 2D image**), real-world meter scale against `1.52 m` kids, and
  target vertical band (`Band 1/2/3`) for the owner to author, keeping clean code-drawn or
  procedural stand-ins working until replaced.
