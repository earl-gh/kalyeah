---
name: apex-street-world
description: >
  Stylized 3D Filipino barangay arena art direction, three-band vertical value/frequency
  hierarchy, 0°-25° shoulder-horizon clarity, and grounded urban chunkiness (Riot Valorant × Epic
  Fortnite × Valve Deadlock). Use when building or reviewing 3D street layouts, props, cover,
  materials, shaders, or arena lighting.
---

# Apex Stylized Street World

Fuse **Riot (*Valorant*)** chest/head-height value discipline and sightline honesty, **Epic
(*Fortnite*)** chunky stylized 3D forms and material separation, and **Valve (*Deadlock*)**
readable urban lanes and neighborhood landmarks for Kalyeah's Filipino barangay arenas.

**Boundary:** Owns 3D barangay layout, `40 m` sightlines, `0°–25°` horizon backdrops, vertical
value/frequency hierarchy, material roughness contrast, and arena lighting. Defers 3P camera rig
and telegraphs to `apex-combat-cam`, HUD to `apex-mobile-hud`, GPU/VRAM budgets to
`apex-net-frame`, engine shaders/imports to `godot-unity-view`, and asset rules to `CLAUDE.md`.

## 1. Three-Band Vertical Value & Frequency Hierarchy

Phone screens (`5"–6.5"`) turn uniform texture detail into muddy noise. Enforce strict vertical
value and detail separation from street level to rooftops:

| Band | Height Zone | Albedo Value | Surface & Detail Rule |
|---|---|---|---|
| **1. Ground** | `0 m` street, bridges, shallow canal | `30–45%` warm mid-dark | Calm painted asphalt/concrete (`barangay_ground`, `estero_water`) with minimal micro-grit so AoE rims, `MoveMarker`, court chalk, `Merienda` stands (`(-34,0)`, `(0,0)`, `(+34,0)`), base circles, and half-alpha `Rescue area` timers pop in noon sun |
| **2. Combat Band** | `0.4 m–1.8 m` kid knee-to-head | `45–62%` clean mid-tone | Broad low-noise plaster, weathered wood, and cardboard behind players; ban brick grids, dark window bars, or loud posters here so outlined `1.52 m` kids (`arcane_ink_outline`) and thrown `Tsinelas`/`Lata`/`Bato` read in `< 100 ms` at `40 m` |
| **3. Pinoy Canopy** | `> 2.4 m` upper facades & eaves | Full chroma & contrast | Concentrate *sari-sari* tarpaulins, jeepney motifs, fiesta *banderitas*, *sampayan* laundry, *capiz* upper windows, and GI/bahay-kubo eaves along upper walls—unmistakable *kalye* soul above the clean combat lane |

## 2. `0°–25°` Horizon Clarity, Landmarks & Stylized Chunkiness

- **3P shoulder horizon & sky silhouette discipline (`0°–25°`):** Unlike the legacy 45° top-down
  view, the 3P over-the-shoulder camera looks across the `0°–25°` elevation band into upper
  rooftops and sky. Keep sky gradients and distant roof backdrops calm, bright, and low-frequency:
  - Route `SpaghettiWires` sparingly along lane margins rather than dense cross-sky webs.
  - Ban dark rooftop clutter, high-frequency truss cages, or loud billboards behind open throw
    lanes so airborne arcs (`Bato` heavy meteor, `Bola` bounces, `Luksong-Tinik` vaults, and
    `Bagsak` leaps) read cleanly at apex against the sky.
- **Sub-`0.5 s` camera orientation:** Across the `168 m`-diameter (`arena_radius = 84.0 m`)
  barangay and `24×48 m` classic street, oppose halves and lanes with distinct silhouettes and
  palette temperatures (`South Covered Court` warm ochre/terracotta vs `North Barangay Hall Plaza`
  cool teal/cream, unmistakable NW vs SE waiting-shed rescue zones, and flyered electric-post
  bases). Spinning the 3P camera must orient the player in `< 0.5 s` without checking the minimap.
- **Chunky `3–5 cm` bevels & roughness separation:** Author bold, toy-chunky silhouettes with
  `3–5 cm` bevels that catch clean specular rims; ban `< 2 cm` razor-thin geometry or `1 px` raw
  edges that shimmer on mobile GPUs. Separate matte plaster/concrete, satin painted wood/cardboard,
  and crisp metallic roofs/cans via distinct roughness steps rather than noisy diffuse grunge.
- **Honest cover & grounded street anatomy:** Cover footprints (crates, benches, sawhorses,
  cardboard walls, waiting sheds) must match sim collision (`player_radius = 0.45 m` clearance)
  with zero invisible snags or misleading gaps. Seat buildings and props into raised sidewalks
  (where `Tagged` kids wait facing inward), painted curbs, gutters, bridge ramps, and soft contact
  AO/decals—never drop isolated boxes onto a flat "parking-lot" slab.
- **Tropical daylight without crushed blacks:** Pair warm directional sun with cool sky-blue
  hemisphere fill so shadowed walls hold `18–22%+` luminance (never `#000000` pits hiding dark
  outfits or `Papel` traps) and sunlit concrete avoids `#FFFFFF` glare over white ground timers.

## 3. Anti-Bloat & World Art-Brief Specification

- Assemble arenas from modular *tabi-tabing bahay* footprints, shared trim sheets/atlases, and
  single-directional + baked fill lighting. Ban real-time GI, screen-space reflections, runtime
  city generators, and bespoke per-prop shaders.
- **World art-brief contract (deferring asset-creation bans/types to `CLAUDE.md` §Assets):**
  Every world prop, facade, decal, or backdrop advisory for the owner must specify:
  1. Real-world meter bounds (`W × H × D`) scaled against `1.52 m` kids and `0.45 m` cover radius.
  2. Target vertical zone (`Band 1`, `Band 2`, `Band 3`, or `0°–25°` horizon backdrop).
  3. Target albedo value range (`%`), primary material roughness tier, and `3–5 cm` bevel target,
     while keeping clean procedural stand-ins working until owner-authored assets land.
