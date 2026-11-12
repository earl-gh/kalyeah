# Kalyeah Overhaul v3 — Brutal Architectural, Urban Layout & 3D Topology Critique

**Author**: `brutal-critic` (Barangay Architectural, Layout & 3D Topology Interrogator)  
**Target Standard**: `10.0 / 10` Valve *Deadlock* stylized painterly architectural silhouette, urban street curvature, macro-blended ground materiality, structural believability, and 90s Philippine provincial barangay authenticity (`0` floating geometry, `0` unanchored wires, `0` spreadsheet grids, `0` stretched/cloned primitive boxes).  
**Inspected Reference Benchmark**: 5 owner-uploaded Valve *Deadlock* screenshots (`media_1794440371345_fe497295.webp`, `media_1794440389686_77ae2b98.webp`, `media_1794440406695_30e0e018.jpg`, `media_1794440424903_258aff17.webp`, `media_1794440464900_86193c09.jpg`) vs. Checkpoint 4 renders (`checkpoint4_deadlock_hud.png`, `checkpoint4_walkable_canal_3lanes.png`, `checkpoint4_province_90s_landmarks.png`, `checkpoint4_locomotion_slide_jump.png`, `checkpoint4_birdseye_3pathways.png`).

---

## Executive Verdict on Checkpoint 4: **4.8 / 10 — REJECTED**

Side-by-side against the 5 Valve *Deadlock* reference frames, Checkpoint 4 reads like an axis-aligned grey graph-paper simulation room with non-uniformly stretched shoeboxes, duplicate procedural `BoxMesh` + `Label3D` signs clipping out of windows, black cartoon hull outlines flattening every wall, canal water floating `+5.2 cm` *above* the street with black-outlined rectangular Band-Aids pasted over it, and catenary wires / *sampayan* clotheslines / vertical wire loops hanging in empty sky with zero physical anchor at one or both ends.

Every flaw below has an exact file/line smoking gun and a mandatory engineering fix assigned to `@fix-map-layout`, `@fix-3d-kit`, and `@audit-10`.

---

## Part 1 — The 45 Unsparing Architectural, Urban & 3D Topology Questions

### Category I — Ruler-Linear Pathways vs. Organic Curves, Staggered Setbacks & Non-90° Parcels (Q1–Q6)

1. **Why are all 3 North-South streets (`X = -34, 0, +34`) and both East-West cross-streets (`Z = ±26`) dead-straight spreadsheet corridors (`data/rules/map_layout.tres:16-18`, `checkpoint4_birdseye_3pathways.png`)?**
   - **Smoking Gun**: All 14 half-map `obstacles` in [`map_layout.tres`](file:///c:/Users/Den/Downloads/kalyeah/data/rules/map_layout.tres#L16-L18) are aligned on three rigid horizontal rows (`Z = 7.6`, `Z = 31.2..31.4`, `Z = 50.4..52.8`) and six rigid vertical edges (`X = -40.6, -27.4, -7.0, +7.0, +27.4, +40.6`). Look at *Deadlock* screenshots #1, #2, #3, and #5: streets gently bend (`8°–16°` S-curves and Y-splays) so sightlines peel open organically around cornices and corner storefronts instead of staring down an infinite bowling lane from `Z = +68` to `Z = -68`.
2. **Why is every single building locked to `0°`, `±90°`, or `180°` (`scripts/view/street_map.gd:813-825`) with a pathetic `±2.2°` sine wobble instead of real `4°–18°` provincial parcel rotations?**
   - **Smoking Gun**: [`StreetMap._add_obstacle`](file:///c:/Users/Den/Downloads/kalyeah/scripts/view/street_map.gd#L813-L825) computes `yaw = PI if center.y > 0 else 0.0` (or `±PI/2` when `target_d > target_w * 1.25`) plus `deg_to_rad(sin(...) * 2.2)`. Real 90s Philippine barangays grew organically along old *pilapil* rice-paddy dikes, creek banks, and provincial feeder roads — parcels sit at `4°–18°` relative to global axes and step forward/back by `0.8m–3.2m` staggered setbacks!
3. **Why is [`StreetMap._add_obstacle`](file:///c:/Users/Den/Downloads/kalyeah/scripts/view/street_map.gd#L835-L841) stretching buildings non-uniformly up to `2.55×` wide and `3.8×` deep?!**
   - **Smoking Gun**: Lines `835–841` compute `sx = (rect.size.x * fill_ratio) / nat.x` and `sz = (rect.size.y * fill_ratio) / nat.z`, while `sy = clampf((sx + sz) * 0.48, 0.85, 1.38)`! Look at what that does to real assets:
     - `Rect2(-27.4, 7.6, 20.4, 13.4)` receives `brgy_sari_sari` (`8.0m × 6.8m` native) stretched **`2.50×` in X and `1.93×` in Z** — turning human doors into `2.4m`-wide garage portals and bricks into `1m` slabs!
     - `Rect2(40.6, 7.6, 11.8, 13.2)` receives `brgy_toda_shed` (`5.6m × 3.8m` native) stretched **`2.06×` in X and `3.40×` in Z** — turning its `0.36m` roof tire weight into a monstrous **`2.5m` giant truck-tire UFO** visible on the right of `checkpoint4_province_90s_landmarks.png`!
   - **Rule**: **NEVER non-uniformly stretch architecture beyond `[0.92, 1.12]` aspect ratio!** Either size `MapLayout` parcels to true lot dimensions (`8m–15m × 6.5m–10.5m`) or populate large compound lots with a multi-building compound cluster (main shophouse + lean-to *karinderya* / *sari-sari* + courtyard CHB wall + tree) at uniform `1:1` architectural scale!
4. **Why does `if index % 3 == 1: yaw += PI` (`scripts/view/street_map.gd:821-822`) blindly spin every 3rd building `180°` so its storefront faces into a neighbor's firewall or narrow rear gap?**
   - **Smoking Gun**: Parcel yaw must ALWAYS be computed from the **nearest primary street/plaza centerline normal** (`atan2` toward the adjacent curved street or corner plaza), NEVER `index % 3 == 1`!
5. **Why is the Tabing-Ilog canal (`Z = 0`) a dead-straight ruler slot (`abs(xz.y) < 3.4` in `barangay_ground.gdshader:44`, `PlaneMesh(166.0, 6.2)` in `street_map.gd:425`) instead of meandering naturally?**
   - **Smoking Gun**: Real provincial creeks (*sapa / estero*) curve! A point-symmetric odd-function meander `z_creek(x) = 2.6 * sin(x * 0.045) + 0.9 * sin(x * 0.11)` preserves exact `180°` competitive team fairness (`z_creek(-x) == -z_creek(x)`) while breaking the sterile horizontal ruler bisecting the map in `checkpoint4_birdseye_3pathways.png`!

### Category II — Street & Eskinita Widths, Curbs, Drainage Gutters, Plazas & Bottlenecks (Q7–Q11)

6. **Where are the streets barren, oversized airport runways vs. genuine barangay streets and *eskinita* alleys?**
   - **Smoking Gun**: Look at `checkpoint4_locomotion_slide_jump.png` and `checkpoint4_birdseye_3pathways.png`:
     - The Center lane (`X = -7.0..+7.0`, `14.0m` constant width for `136m` straight) and outer perimeter ring (`R = 56m..84m`, up to `28m` of empty grey concrete slab!) feel barren and exposed.
     - Conversely, *Deadlock* (#1, #2, #4, #5) varies street widths organically: **tight `7.5m–9.0m` shophouse bottlenecks** framed by overhanging awnings/balconies open into **`15m–19m` sun-drenched corner plazas** (fountain/poso/waiting-shed/basketball-apron pockets), flanked by `3.6m–4.8m` *eskinita* shortcut cut-throughs!
7. **Where are the 3D raised concrete sidewalk curbs, beveled driveway aprons, and sunken storm-drain gutters?**
   - **Smoking Gun**: Every single *Deadlock* screenshot (#1–#5) separates the dark asphalt/cobble carriageway from building fronts with a **crisp 3D raised concrete sidewalk (`height = 0.13m`, width = `1.4m–2.2m`)**, beveled `45°` granite/concrete curb stones, and sunken iron storm-drain grates in the gutter! In Checkpoint 4 (`street_map.gd`), buildings sit directly on `Y = 0` flat grey paper with zero raised sidewalks, zero curbs, and zero gutters!
8. **Why do outer zones (`|Z| = 53..82m` and `|X| = 58..82m`) turn into empty grey concrete parking lots scattered with random tiny barricades (`Rect2(-24.2, 52.8, 5.4, 2.2)`)?**
   - **Smoking Gun**: In `checkpoint4_birdseye_3pathways.png`, the North and South base approaches have huge barren expanses around `(±22, ±62)` and `(±65, ±30)`. Real barangays frame the *plaza / basketballan* with continuous compound walls, *waiting sheds*, *tindahan* corners, tricycle *pilahan*, and lush shaded roadside planting strips.
9. **Why do `cover_wall` barricades (`Rect2(-24.2, 52.8, 5.4, 2.2)` and `Rect2(18.8, 52.8, 5.4, 2.2)`) look like paintball arena bunkers dropped randomly in the middle of a street?**
   - **Smoking Gun**: Real 90s barangays don't have sandbag military trenches (`make_cover_wall` in `build_barangay_kit.py:2299-2310` literally builds `sandbag_burlap` + exposed rebar military bunkers!). Replace military sandbag bunkers with **authentic barangay street cover**: concrete planter boxes with bougainvillea/gumamela, stacked softdrink *kaha* crates + wooden *bangko* + water drums outside a store, DPWH concrete road-repair culvert pipes, or a parked *kariton* / *sorbetes* / tricycle!
10. **Why are the Merienda power-up stands (`(-34, 0)`, `(0, 0)`, `(34, 0)` in `map_layout.tres:25`) sitting smack in the wet middle of the canal fords instead of on dry raised corner plazas / bridgehead aprons?**
    - **Smoking Gun**: Nobody sets up a wooden *halo-halo / banana-cue / palamig* merienda table (`brgy_merienda_stand`) **submerged in the middle of a creek crossing** (`Z = 0.0`, `checkpoint4_walkable_canal_3lanes.png` center)! Place **4 point-symmetric Merienda stands** on dry cobblestone/concrete bridgehead corner plazas overlooking the crossings (e.g., `(±33.5, ±6.2)` and `(∓5.8, ±5.6)` or symmetric riverside *tambayan* aprons)!
11. **Why do Patintero chalk base rings (`Z = ±68`) float over uniform grey grid tiles instead of an authentic Barangay Plaza / Basketball Half-Court / Hall Forecourt paving apron?**
    - **Smoking Gun**: Team bases (`(0, +68)` and `(0, -68)`) are the heart of each side's barangay compound (*Agawang Base* home post + rescue sidewalk). The ground shader and surrounding 3D curb layout must frame each base as a real painted barangay *plaza* court/forecourt with curb seating, shade trees in soil planters, and clear sightlines down all 3 curved lanes.

### Category III — Floating, Clipping, Sunken & Slope-Mismatched Geometry (Q12–Q18)

12. **Why is `EsteroWater` (`scripts/view/street_map.gd:424-432`) floating at `Y = +0.052m` *ABOVE* the `Y = 0.0` street elevation?!**
    - **Smoking Gun**: Line `430` sets `water_mi.position = Vector3(0.0, 0.052, 0.0)` over a flat `Y = 0` ground plane! Water does not hover `5.2 cm` on top of dry asphalt like a spilled mercury sheet! Either depress the ground mesh vertices along `z_creek(x)` into a real **sunken `-0.26m` canal channel** (`water_y = -0.14m` across deep stretches, rising gently to `-0.02m` shallow ankle- splash pebble/stone cobbled wades at the 3 pathway fords) OR build continuous 3D concrete/riprap canal embankment walls (`height = 0.32m`) along the non-ford creek stretches with smooth ramped stone cobble fords at the 3 crossings!
13. **What are those hideous cartoon black-outlined grey rectangles pasted over the water in `checkpoint4_walkable_canal_3lanes.png` and `checkpoint4_deadlock_hud.png`?**
    - **Smoking Gun**: [`StreetMap._build_estero_canal_and_bridges`](file:///c:/Users/Den/Downloads/kalyeah/scripts/view/street_map.gd#L442-L518) spawns `LowPoly` rectangular boxes (`Vector3(ford_w, 0.14, 2.2)` at `Y = 0.02`) plus 12 rounded `0.06m` rectangular pads (`L489-504`) and assigns `bank_mat.next_pass = outline` (`L515`)! Because `arcane_ink_outline.gdshader` extrudes backfaces along normals, every flat slab gets a thick black marker border floating over the cyan water shader! Delete `TabingIlogBanks`'s primitive outlined boxes completely and build real **beveled stone-paved causeway fords + 3D mossy concrete/stone canal embankments** without hull outlines!
14. **Why are `TabingIlogSteps` (`brgy_ilog_steps` at `(-20, 0, ±2.6)` and `(20, 0, ∓2.6)`, `street_map.gd:591-600`) sitting ON TOP of `Y = 0` dry flat ground instead of descending INTO the water?**
    - **Smoking Gun**: `make_ilog_steps` (`build_province_90s_props.py:1810-1851`) builds steps rising from `Z = 0.0` UP to `Z = +0.86m`! When placed at `Y = 0` (`street_map.gd:598`), a person walking from the street (`Y = 0`) has to climb UP `0.86m` in the air and then jump off a `0.86m` cliff to touch the water! `brgy_ilog_steps` must connect the street/embankment crest (`Y = +0.28m` or `0.0m`) **downward** to the water edge (`Y = -0.14m`), with `brgy_laundry_stones` anchored directly at the wet water margin (`Y = water_y - 0.02m`), never floating dry on flat concrete!
15. **Why are procedural `LowPoly` shopfront boxes and floating `Label3D` signs (`street_map.gd:647-745`) colliding with and clipping directly through `.glb` buildings?!**
    - **Smoking Gun**: Look at `checkpoint4_province_90s_landmarks.png` right side! [`_build_90s_shopfront_vignettes_and_gates`](file:///c:/Users/Den/Downloads/kalyeah/scripts/view/street_map.gd#L647-L745) spawns primitive `BoxMesh` counters, benches, awnings, and bright unshaded `Label3D` texts (`TINDAHAN NI ALING NENA`, `KARINDERYA`, `BARBERYA GUPIT BINATA`, `VULCANIZING SHOP`) at hardcoded coordinates `(-27.55, 12.6)`, `(-40.48, 14.2)`, `(27.55, 11.2)`, `(27.55, 17.6)` — slamming straight through the side walls, grilled windows, and `BAWAL UMIHI DITO` stencils of the stretched `.glb` buildings! **Abolish `_build_90s_shopfront_vignettes_and_gates`'s glued-on primitive boxes & floating `Label3D`s!** Every shopfront (`sari_sari`, `karinderya`, `barberya`, `vulcanizing`, `bakery/palengke`) must be a **genuine 3D `.glb` building** oriented with its real recessed `-Y` storefront facing the street!
16. **Why do trees (`street_map.gd:1172-1228`) burst straight out of solid grey concrete with a random blue cylinder (`kit.cylinder` drum at `L1194`) glued next to every single tree trunk?**
    - **Smoking Gun**: Look at `checkpoint4_province_90s_landmarks.png` bottom-left (`(-54.8, 0, 6.2)`) and `checkpoint4_birdseye_3pathways.png`: `BarangayProps.tree_spots` and `perimeter_trees` plant `acacia_narra`, `banana_clump`, `coconut_palm`, and `mango_tree` straight through flat grey pavement with zero soil/curb planter ring, and lines `1192–1194` spawn a bright blue cylinder (`kit.drum`) next to every tree (`i % 2 == 0`)! Real barangay trees grow inside **raised mossy CHB/concrete tree-pit rings filled with dark soil/roots** or unpaved roadside *lupang berm* strips!
17. **Why does `ElectricPole_0` (`(-40.8, 0, 7.2)`, `checkpoint4_province_90s_landmarks.png` left) have `brgy_poster_cluster` colliding into its `1.10m` concrete base collar so it looks like a weird freestanding guillotine sign?**
    - **Smoking Gun**: `street_map.gd:564-567` spawns `brgy_poster_cluster` at the exact same `(p.x, 0, p.z)` origin as `brgy_meralco_post`! `brgy_meralco_post` already has its own `1.10m × 1.10m` concrete base (`build_barangay_kit.py:2188`) AND its own curved `MALABANAN` + `VOTE! HALALAN '95` notices (`L2256-2259`), while `brgy_poster_cluster` (`L2337-2355`) adds *another* `1.06m` CHB block + two wooden posts + another `MALABANAN` sign + another `VOTE` sign at `(0, -0.32)` clipping straight through the pole collar! Separate `brgy_poster_cluster` into a clean **corner wall/alley wayfinding signpost** (`NO ENTRY / ESKINITA / BRGY. KALYEAH`) placed at street corners, never double-spawned inside `brgy_meralco_post`!
18. **Why are `PerimeterGateArch` (`(0, 0, ±83.2)`, `street_map.gd:748-799`) and perimeter `PerimeterWall_*` segments (`R = 85.4m`, `street_map.gd:1032-1046`) built out of crude `LowPoly` primitive cylinders/boxes that sit at two mismatched radii (`83.2m` vs `85.4m`) with open `2.2m` gaps?**
    - **Smoking Gun**: The welcome arch sits at `Z = ±83.2` (`width = 12m`), while the perimeter wall ring sits `2.2m` behind it at `R = 85.4m` (`seg_len = 13.0m`) — leaving detached floating pillars unconnected to the perimeter CHB wall! The Barangay Welcome Arch must be an authentic beveled `.glb` (`brgy_welcome_arch` or integrated gate) whose side masonry piers tie seamlessly (`0.0m` gap) into the continuous perimeter compound CHB wall at `R = 84.2m`!

### Category IV — Structural & Architectural Nonsense on Buildings (Q19–Q25)

19. **Where are the structural columns/corbels supporting cantilevered 2nd storeys and `1.4m` canopies?**
    - **Smoking Gun**:
      - `brgy_house_two_storey` (`build_barangay_kit.py:1901`) cantilevers a `7.86m × 7.26m × 3.25m` upper timber floor `0.38m` out on all 4 sides with zero exposed floor joists/corbel brackets underneath (`Z = 3.38m`)!
      - `brgy_tabi_cluster_a` Unit 2 (`L1631`) cantilevers `-0.42m` forward with zero corbels.
      - `brgy_sari_sari` (`L1855`) projects a `1.45m` front awning (`y = -3.72`) held up by two paper-thin `4cm` sticks (`L1857`) with no diagonal knee braces tied back to the wall pilasters!
      - `brgy_covered_court` (`L1970-1988`) spans `16.4m` across X with `5cm`-thin flat boxes (`add_box((half_span, 0.12, 0.12))`) instead of triangulated steel Warren/Howe roof trusses with bottom chords and gusset plates!
20. **Where are the roof fascia boards, *alulod* box gutters, and PVC/GI downspouts reaching `Y = 0`?**
    - **Smoking Gun**: Every single roof in `build_barangay_kit.py` (`add_shed_yero_roof` `L1183`, `add_gable_yero_roof` `L1261`) and `build_province_90s_props.py` (`toda_shed`, `kapilya`, `waiting_shed`) ends in raw floating corrugated ripples with **zero *alulod* sheet-metal gutter** at the low eave and **zero vertical downspout pipe** strapped down the corner column to `Y = 0`! Look at *Deadlock* #1–#5: vertical drainpipes with wall straps every `1.8m` and flared bottom shoes at the sidewalk gutter are one of the strongest vertical rhythm details on every building!
21. **Why do doors hover `20 cm – 42 cm` up a concrete plinth face with ZERO protruding threshold step?**
    - **Smoking Gun**:
      - `brgy_tabi_cluster_a`: plinth height `0.34m`, door bottom at `Z = 0.34m` flush with vertical wall (`L1606, 1616`) — a `34 cm` shin-breaker cliff with no step!
      - `brgy_house_two_storey`: plinth height `0.40m`, door bottom at `Z = 0.42m` (`L1900, 1904`) — a `42 cm` vertical drop with zero entry stoop!
      - `brgy_hall_plaza`: plinth height `0.48m` (`L1997`), front steps only cover `3.2m` in center while side bay doors have none.
      - **Rule**: Every exterior door MUST have a `0.35m–0.55m` deep × `1.25m` wide concrete threshold step (`1–2` risers of `0.17m`) grounded cleanly to `Z = 0.0`!
22. **Why are windows, doors, and shop counters GLUED ONTO THE OUTSIDE of solid cubes (`protruding +4cm to +8cm`) instead of RECESSED INWARD (`-15cm to -28cm`)?!**
    - **Smoking Gun**: Look at `build_barangay_kit.py:1311` (`add_rehas_window`), `L1616` (door), `L1844` (`sari_sari` counter): `add_walled_room` (`L1119`) builds an uncut rectangular box, and then windows and doors are literally `add_box` slabs stuck onto the outer skin! Compare *Deadlock* screenshots #1, #2, #3, #5: **every window, doorway, and shopfront sits inside a deep `15cm–30cm` recessed masonry/timber reveal** with a projecting concrete sill at the bottom and a lintel/brow/hood at the top, casting crisp self-shadows and ambient occlusion!
23. **Why does `brgy_footbridge` (`build_barangay_kit.py:2039-2065`) have no intermediate mid-span support piers or steel I-beam stringers, and why isn't it even used coherently with the canal banks?**
    - **Smoking Gun**: If the mid-canal is ankle-deep walkable across the 3 main lanes, narrow *eskinita* footbridges (`brgy_footbridge` or wooden/steel barrio footbridges) can span the deeper non-lane canal stretches as flanking high-risk narrow shortcuts OR flank the causeway piers — and any bridge MUST have steel C-channel stringers, bolted hand-rail stanchions, and concrete bank abutments seated into the canal walls (`min_y <= bank_bed`).
24. **Why does `brgy_kapilya` (`build_province_90s_props.py:1735-1807`) look like a closed solid white box with two tiny stuck-on squares instead of an open-front barrio chapel (*bisita*) with arched steel grilles, bell bracket, and recessed altar niche?**
    - **Smoking Gun**: Real 90s barrio *kapilyas* have an arched front portico/grille gate, recessed *kampana* bell belfry, tiled dado wainscot, concrete flower boxes, and pitched GI roof with cross & guttering.
25. **Why does `brgy_bakery_oven` (`build_province_90s_props.py:1899-1953`) sit outdoors on open pavement (`(25.2, 0, 29.2)`, `street_map.gd:606`) like a freestanding Pizza Hut brick kiln in the middle of the street?!**
    - **Smoking Gun**: No Filipino *panaderya* puts its brick *pugon* oven loose in the middle of an intersection! A *panaderya* (`PANADERYA / MAINIT NA PANDESAL`) is a **corner shophouse storefront** with glass bread *estante* display cases (`pandesal`, `spanish bread`, `kababayan`), red/yellow hand-painted signage, a recessed service counter, and an exhaust chimney stack rising from its rear bakehouse roof!

### Category V — Two-Ended Physical Connections for ALL Wires, Service Drops, Clotheslines, Pipes & Fences (Q26–Q31)

26. **Why do `SpaghettiWires` `alley_corridors` (`scripts/view/spaghetti_wires.gd:54-90`) hang 60+ black wires and floating wall brackets in EMPTY SKY at `Y = 5.65m–6.60m`?!**
    - **Smoking Gun**: Look at `checkpoint4_province_90s_landmarks.png` (top right & center) and `checkpoint4_locomotion_slide_jump.png`! Lines `54–62` hardcode `Vector4(-7.0, 7.0, 9.2, 20.0)` and `Vector4(-40.6, -27.4, 9.0, 20.0)` and spawn `_append_wall_bracket` + 5 catenary wires at **`Y = 5.65m..6.60m`**! At `(±7.0, 9.2..20.0)` and `(±40.6, 9.0..20.0)`, 1-storey buildings (`sari_sari`, `toda_shed`, `palengke_stall`) are only **`3.4m–4.3m` tall**! Those brackets and wires are literally **floating `1.5m–2.5m` in thin air above the roofs**!
27. **Why do `SpaghettiWires` service drops (`scripts/view/spaghetti_wires.gd:115-131`) shoot wires into empty air (`ring_r + 5.8`, `L129`) or unanchored mid-air AABB corners (`eave_h = 5.20..6.00m`, `L121`)?**
    - **Smoking Gun**:
      - Lines `117–124` clamp `junction_p` to the 2D `Rect2` obstacle box + `0.35m` *outward* into empty air (`hx = clamp(...) - sign * 0.35`? Wait: when `junction_p` is outside `r_obs`, `hx == r_obs.position.x` and subtracting `0.35` pushes it **further away from the building** into the street!) at hardcoded `Y = 5.20..6.00m` regardless of actual roof height or parcel rotation!
      - Lines `126–131` (`else: # Outer ring poles`) blindly fire two wires from every outer pole to `rad_dir * (ring_r + 5.8) ± tang * 3.8` at `Y = 5.30m` — straight into empty sky between perimeter trees!
28. **What are those bizarre black vertical hula-hoops (`_append_wire_coil`, `spaghetti_wires.gd:48-51, 86-90, 92-104`) levitating above mid-span wires in `checkpoint4_province_90s_landmarks.png`?!**
    - **Smoking Gun**: `_append_wire_coil` draws `0.44m × 0.56m` vertical ellipses centered **on** `mid + Vector3(0, +0.06, 0)` so half the loop sticks `34 cm` UP into thin air above a sagging wire with zero tie-wraps! Real Meralco/PLDT "dead coils" hang **below** messenger cables lashed with tie-wire next to poles (`0.6m–1.2m` from the pole crossarm), NEVER sticking upright like rabbit ears in the middle of a street span! And `MIN_WIRE_Y = 4.55` (`L392`) artificially flattens catenaries onto an invisible ceiling plane instead of computing proper anchor heights (`pole crossarm Z = 7.45m / 9.2m` to `house weatherhead Z = 4.6m–5.8m`).
29. **Why are `sampayan_spans` (`scripts/view/street_map.gd:1114-1157`) hanging laundry at `Y = 5.15m–5.30m` in thin air above `3.8m` market stalls and parallel to empty streets?**
    - **Smoking Gun**:
      - Span 0: `(-55.6, 5.15, 11.2) -> (-53.4, 5.30, 11.8)` — `Rect2(-53.6, 7.6, 13.0, 13.2)` holds `palengke_stall` (`3.8m` tall), so `Y = 5.2m` is `1.4m` **in empty air above the roof**!
      - Span 1: `(-39.6, 5.15, 10.6) -> (-39.6, 5.20, 14.0)` — runs North-South at `X = -39.6` (`1.0m` out in the West lane!), anchored to nothing at either end!
      - **Rule**: Every `brgy_sampayan_3d` MUST Either:
        1. Stand on two grounded bamboo/GI T-poles (`min_y = 0.0`) inside a side/rear yard or riverbank laundry zone (`height = 2.15m`), OR
        2. Span strictly between two verified **2-storey building upper-window/balcony steel cleats** computed from actual placed building transforms (`to_global(local_cleat_socket)`), with steel wall eye-plate brackets rendered at BOTH ends!
30. **Why do `banderita_spans` (`scripts/view/street_map.gd:635-645`) float with `yaw = 0.0` at `(0, 0.6, ±48)` across `12.0m` without touching building walls at `X = ±6.0m`?**
    - **Smoking Gun**: At `(0, 0.6, ±48)`, the center pathway walls are at `X = ±7.0m` (`14.0m` wide), while `brgy_banderitas` is fixed at `12.0m` (`X = -6.0..+6.0`) — leaving **`1.0m` of empty air at both ends**! Banderitas must be procedurally catenary-spanned (or exact-span scaled along string axis) from **exact left-building eave/pole anchor point `A`** to **exact right-building eave/pole anchor point `B`**, with `0.00m` air gap!
31. **Where do electric meters, water pipes, and roof downspouts connect?**
    - **Smoking Gun**: Currently `meter_box` in `build_barangay_kit.py` is only placed on poles (`L2208`), never on house side walls with a gray PVC riser pipe going UP to a porcelain/steel weatherhead (`service entrance cap`) where the pole's service drop wire lands! Every house/shophouse `.glb` must include a **Side-wall Meralco kWh meter + vertical PVC conduit riser + eave weatherhead bracket at a known local socket** so `SpaghettiWires` lands the service drop wire **precisely on that weatherhead**!

### Category VI — 90s Barangay Urban Zoning & Landmark Placement Logic (Q32–Q36)

32. **Why is `TodaTerminal` at `(±53.5, 0, ±21.6)` while `TodaTricycle` is 15.8 meters away at `(±53.4, 0, ±5.8)` (`street_map.gd:582-589`) and `toda_shed` is stretched into a giant `11.8m × 13.2m` monster at `Rect2(40.6, 7.6, 11.8, 13.2)`?!**
    - **Smoking Gun**: A TODA (*Tricycle Operators and Drivers Association*) terminal (`PILAHAN NG TRICYCLE`) only makes urban sense **as one cohesive roadside terminal pocket** near a main road / cross-street junction: a true-scale `5.6m × 3.8m` `brgy_toda_shed` dispatcher bench + route signboard, with **2–3 `brgy_tricycle` units parked in an angled queue (`12°–22°`) right beside/in front of it** on a concrete pull-off apron!
33. **Why is every building assigned by `index % 5` modulo arithmetic (`street_map.gd:854-882`) instead of real 90s Philippine barangay urban zoning?**
    - **Smoking Gun**: Modulo assignment dumps `palengke_stall` and `toda_shed` on massive residential lots and puts residential houses on prime commercial intersections! Real 90s barangay urbanism follows strict social geography:
      - **Plaza / Base Forecourt (`Z ≈ ±56..±72`, Center)**: **Barangay Hall (`brgy_hall_plaza`)**, **Covered Basketball Half-Court / Liga Stage (`brgy_covered_court`)**, **Barrio *Kapilya* (`brgy_kapilya`)**, and **Waiting Shed (`brgy_waiting_shed`)** framing the communal plaza where kids play *Agawang Base* and *Patintero*.
      - **Prime Foot-Traffic Street Corners (Center × Cross-Eskinita intersections, `Z ≈ ±18..±28`)**: **Corner *Sari-Sari* Store (`brgy_sari_sari`)** with wooden *tambayan* bench + softdrink *kaha* crates, **Aling Nena's *Karinderya* (`brgy_shophouse_row` / `karinderya`)** with *kaldero* counter under front awning, and **Corner *Panaderya*** with recessed glass bread cases.
      - **Outer Feeder Roads (West & East avenues, `|X| ≈ 30..42`)**: **TODA Tricycle Terminal + 2 queued tricycles** at a pull-off bay, **Vulcanizing & Kalye Talyer** with compressor, hanging *interior* inner-tubes, and parked **90s Owner-Type Jeep / Passenger Jeepney** on roadside gravel/concrete apron, **Barberya ("Gupit Binata")**, and **Palengke Talipapa wet/produce stalls** clustered together under shared *trapal* canopies.
      - **Inner Residential *Looban / Eskinita* Lots**: **2-Storey Bahay (`brgy_house_two_storey`)**, **Tabi-Tabi Shophouses (`brgy_tabi_cluster_a`, `brgy_tabi_cluster_b`)** with narrow alley *poso* hand-pump courtyard (`brgy_poso_well`) and grounded *sampayan* laundry yard.
      - **Tabing-Ilog Riverfront (`Z ≈ -5..+5`)**: Stone causeway crossings, **descending `brgy_ilog_steps`** into the creek edge, **`brgy_laundry_stones` + *batya* basins** at the wet margin, riverside bamboo/narra shade, and **4 dry bridgehead Merienda stands** (`halo-halo / banana cue / sago't gulaman`).
34. **Why are vehicles (`PassengerJeepney` at `(±44.8, 0, ±25.8)`, `OwnerJeep` at `(±19.5, 0, ∓26.2)`) parked dead-parallel (`0°` / `90°`) in the middle of cross-street intersections instead of pulled into realistic curbside lay-bys or vulcanizing bays?**
    - **Smoking Gun**: Park the passenger jeepney angled (`8°–14°`) along a widened outer avenue curbside pull-off near the `Waiting Shed` / `Talipapa`, and park the `OwnerJeep` angled (`15°`) onto the concrete apron right outside the `Vulcanizing Shop` (as if getting a tire patched)!
35. **Why are `brgy_sorbetes_cart` and `brgy_merienda_stand` isolated from shade and foot-traffic corners?**
    - **Smoking Gun**: *Mamang Sorbetero* parks his colorful wooden pushcart in the shade of an acacia/mango tree at the corner of the Barangay Plaza / Basketball Court, with wooden *bangko* benches nearby — never marooned randomly in blank concrete.
36. **Why do side/perimeter streets abruptly end at a raw concrete circle wall instead of visually continuing through gated barrio compound entries / *eskinita* mouths?**
    - **Smoking Gun**: Look at *Deadlock* #2 and #5: arena boundaries read as real urban architecture (wrought-iron/steel gates, archways, closed roll-up storefronts, compound CHB walls topped with bougainvillea and broken-glass mortar cresting) rather than a bare extruded cylinder ring!

### Category VII — Repeating `3m` Simulation Grid Shader vs. *Deadlock* Macro-Blended Ground & Curbs (Q37–Q40)

37. **Why does the entire ground look like a `3.0m × 3.0m` grey Concrete Simulation Grid (`shaders/barangay_ground.gdshader:131, 139`)?!**
    - **Smoking Gun**: Look at lines `130–141` of [`barangay_ground.gdshader`](file:///c:/Users/Den/Downloads/kalyeah/shaders/barangay_ground.gdshader#L130-L141):
      ```glsl
      float pad_j = grid_line(xz / 3.0, 1.05);
      concrete_col = mix(concrete_col, concrete_col * 0.78, pad_j * 0.55);
      ...
      float j = grid_line(xz / 3.0, 0.95);
      road_col = mix(road_col, road_col * 0.74, j * 0.65);
      ```
      It literally draws a dark **`3.0m × 3.0m` Cartesian graph-paper grid** across EVERY square meter of road AND concrete pad across the entire `168m` arena (`checkpoint4_locomotion_slide_jump.png`, `checkpoint4_birdseye_3pathways.png`)!
    - **Compare *Deadlock* #1–#5**: ZERO Cartesian grid lines! *Deadlock* carriageways are **warm charcoal-taupe weathered asphalt (`vec3(0.22, 0.21, 0.20)` to `vec3(0.32, 0.30, 0.28)`)** blended macro-organically (`18m–45m` low-frequency Simplex/FBM masks, zero tiling!) into:
      1. **Warm terracotta/ochre brick & cobble intersection aprons** where streets meet;
      2. **Crisp painted white crosswalk zebra bars** at major intersections and faded dashed lane/edge markings following the **curved** street tangent;
      3. **Poured concrete sidewalk & storefront apron slabs** (with joints aligned to the local street curve, NEVER global `xz / 3.0`!) along building frontages;
      4. **Dark wet storm-drain gutter bands** (`18cm` wide) along curb edges + **dark contact AO halos** (`0.45m–1.2m` soft shadow falloff) beneath building footprints;
      5. **Compacted warm laterite earth / mossy stone / wet river silt & pebbles** along tree strips, *looban* yards, and the meandering Tabing-Ilog banks!
38. **Why does `barangay_ground.gdshader` (`L42-50`) hardcode straight roads at `abs(xz.x) - 34.0`, `xz.x = 0`, and `abs(xz.y) - 26.0`?**
    - **Smoking Gun**: Even if buildings move, `barangay_ground.gdshader` paints straight crosshairs at `X = ±34, 0` and `Z = ±26`! The ground shader MUST evaluate the **exact same analytical S-curve street centerline functions** (`x_west(z)`, `x_center(z)`, `x_east(z)`, `z_eskinita(x)`, `z_creek(x)`) used by `MapLayout` and `StreetMap`, so asphalt carriageways, curb gutters, crosswalks, and canal banks curve in `100%` lockstep with the 3D sidewalks and buildings!
39. **Why does `estero_water.gdshader` (`checkpoint4_walkable_canal_3lanes.png`) look like radioactive swimming-pool cyan (`vec3(0.11, 0.30, 0.31)` + `vec3(0.16, 0.42, 0.40)`) with sharp hard straight edges?**
    - **Smoking Gun**: Real 90s provincial *tabing-ilog / sapa* water is **olive-jade / warm tea-tinted shallow creek water** (`deep = vec3(0.10, 0.20, 0.17)`, `shallow = vec3(0.24, 0.31, 0.22)`) where submerged mossy river pebbles/cobbles show through shallow ankle-deep fords, with soft foam ripples around stepping stones and bridge piers and feathered wet-silt bank transitions!
40. **Why is `clamp(col, vec3(0.08), vec3(0.65))` (`barangay_ground.gdshader:264`) compressing everything into mid-grey mush (`0.38–0.49`) instead of rich *Deadlock* tonal separation (`0.14` dark wet gutters/AO vs `0.23` warm asphalt vs `0.48` sunlit concrete curbs vs `0.58` worn chalk/crosswalk paint)?**
    - **Smoking Gun**: Right now both `road_col` (`0.44`) and `concrete_col` (`0.45`) have nearly identical luminance (`~0.44`)! Separating carriageway asphalt (`~0.22–0.28`), warm cobble intersections (`~0.32–0.38`), sunlit raised concrete sidewalks (`~0.46–0.54`), and worn cream crosswalk bars (`~0.56–0.63`) — while strictly keeping the mandatory `clamp(col, vec3(0.08), vec3(0.65))` ceiling (`test_visual_polish.gd:59`) — will instantly give the *Deadlock* Figure/Ground read where heroes and buildings pop with `10/10` clarity!

### Category VIII — Front / Side / Rear Building Asymmetry vs. Symmetrical Back-to-Back Clones (Q41–Q42)

41. **Why do buildings in `build_barangay_kit.py` look like 4-sided symmetrical clones with front windows/textures pasted onto side firewalls and back walls?**
    - **Smoking Gun**: Inspect `make_tabi_cluster_a` (`L1600-1695`), `make_tabi_cluster_b` (`L1698-1748`), `make_shophouse_row` (`L1751-1834`), `make_sari_sari` (`L1837-1895`), and `make_house_two_storey` (`L1898-1944`): `add_walled_room` paints all 4 exterior faces with the same clean facade swatch, and the rear (`+Y`) and side (`±X`) walls either repeat identical windows or sit blank!
    - **Mandatory 90s Philippine Barangay 3-Face Architectural Law**:
      - **FRONT FACE (`-Y`, facing street/plaza)**:
        - Deep **recessed (`-0.22m` to `-0.35m`) ground-floor storefront / *silong* porch**;
        - Grounded concrete entry threshold step(s) + raised concrete storefront apron;
        - Storefront iron *rehas* grille + wooden serving *pasamano* counter (`sari-sari` / `karinderya` / `panaderya`), OR roll-up corrugated steel shutter header box (`barberya` / `vulcanizing`), OR residential capiz/jalousie windows with window-type AC box protruding (`0.48m × 0.36m`) on brackets;
        - Sloped *yero* / striped *trapal* front sun-awning supported by triangulated steel angle-iron knee brackets;
        - Hand-painted 90s provincial signboard / wall mural integrated into the upper fascia or parapet.
      - **SIDE FACES (`-X` and `+X`, facing *eskinita* or neighbor lot)**:
        - **Unpainted raw / patch-plastered gray hollow-block (`chb_raw_moss` / `stucco_aged`) firewall (*pader*)** rising `0.25m` above the roof slope as a stepped concrete parapet with sheet-metal flashing;
        - **Meralco round glass kWh electric meter box** (`Z = 1.75m`) + vertical gray **PVC conduit pipe** strapped up the wall to a **steel/porcelain service-drop weatherhead (`Z = 4.6m–5.4m`)** (exact anchor for `SpaghettiWires`!);
        - Small high bathroom louver vent windows (`0.55m × 0.42m` at `Z = 2.15m`) instead of full parlor windows, plus stenciled `"BAWAL UMIHI DITO"` / `"POST NO BILL"` on alley-facing lower plaster.
      - **REAR FACE (`+Y`, facing *likod-bahay* / back yard)**:
        - Narrow recessed **back kitchen wooden door** with concrete threshold step;
        - Lean-to corrugated tin **dirty kitchen (*kusina sa likod*)** overhang with tiled/concrete ***lababo* wash sink**, stacked clay pots/*kaldero* or firewood rack, and **200L blue plastic water drum** fed by the rear roof downspout;
        - Kitchen smoke/grease **louvered exhaust hood / vent pipe** and gray PVC waste drain pipe descending cleanly to `Z = 0.0`.
42. **Why do props (`brgy_merienda_stand`, `brgy_toda_shed`, `brgy_palengke_stall`, `brgy_waiting_shed`) lack clear Customer Front (`-Y`) vs. Vendor/Back (`+Y`) storytelling?**
    - **Smoking Gun**: A real *merienda* or *palengke* stall has colorful glass *garapon* jars (`sago't gulaman`, `buko pandan`), banana-cue skewers, and painted price board facing the street (`-Y`), while the vendor side (`+Y`) has the open storage shelf, blue water jug, ice chest (*styro/cooler*), and wooden stool!

### Category IX — Blurry `224px` Swatches, Missing Normal Maps & Cartoon Black Hull Outlines (Q43–Q45)

43. **Why does every building and prop have a heavy black cartoon marker outline (`arcane_ink_outline.gdshader`, `street_map.gd:515, 741, 1280` & `barangay_props.gd:132`) flattening the 3D architecture into 2D clip-art?!**
    - **Smoking Gun**: Look at all 5 owner-uploaded Valve *Deadlock* screenshots: **THERE ARE ZERO BLACK HULL OUTLINES ON BUILDINGS, ROADS, WALLS, OR PROPS!** *Deadlock* reserves crisp silhouette readability for **heroes/combatants** against painterly, un-outlined 3D environment architecture whose edges read via **3D bevels (`1.5cm–4cm`), deep recessed reveals, baked vertex AO (`COLOR_0`), tangent-space normal relief, and warm golden-hour directional sunlight vs. cool sky-fill shadows**! Removing `arcane_ink_outline.gdshader` (`next_pass`) from `StreetMap` buildings, canal banks, shopfronts, and `BarangayProps` immediately eliminates the cheap cartoon outline clutter (including the double-outlined window boxes and black-bordered canal slabs)! *(Keep hero silhouette clarity on `KidModel` only!)*
44. **Why do wall textures and painted 90s signs (`TINDAHAN`, `HALALAN '95`, `MALABANAN`, `BAWAL UMIHI DITO`) look blurry and blocky up close (`build_barangay_kit.py:34-37, 294-352`)?**
    - **Smoking Gun**:
      1. `build_barangay_kit.py` packs 64 cells (`8 × 8`) into `2048 × 2048` (`256 × 256` per cell minus `32px` padding = **`224 × 224` usable pixels** stretched across an `8m` wall!).
      2. `_FONT_5X7` (`L294-352`) draws text using a tiny **`5 × 7` pixel bitmap font** scaled up into fat blurry stair-stepped blocks!
      3. `build_shared_material` (`L915-945`) exports **only flat Base Color (`Roughness = 0.86`) with ZERO tangent-space Normal Map and ZERO Roughness variation** (so glass windows, brushed galvanized *yero* roofs, wet mossy concrete, and matte plaster all have the exact same dead `0.86` chalk roughness!).
    - **Fix**:
      1. Author **high-resolution anti-aliased stroke/polygon typography** (clean bold 90s signboard block serifs/sans + drop-shadows + borders, never `5×7` pixel blocks!) and dedicate larger double-wide (`512 × 256` or `512 × 512`) atlas regions in a `2048 × 2048` (or `4096 × 2048` VRAM-compressed ETC2) atlas for primary signage & hero wall materials, OR tile brick/CHB/yero cleanly within generous padded bands.
      2. Generate and hook up a **shared tangent-space Normal Map (`BarangayAtlas_normal.png` & `ProvincePropsAtlas_normal.png`)** + baked **Metallic/Roughness channel** (shiny `0.28` glass/tin roofs/water drums vs `0.88` raw CHB/stucco!) so sunlight rakes across every hollow-block mortar joint, *yero* corrugation, wood grain, and capiz shell! *(Ensure `.png.import` sets `compress/mode=2` and `compress/normal_map=1` per `test_texture_budget.gd`!)*
45. **Why does the sky/horizon in `checkpoint4_walkable_canal_3lanes.png` and `checkpoint4_locomotion_slide_jump.png` look washed-out cyan-grey compared to *Deadlock*'s warm painterly cream-gold sky and atmospheric building depth?**
    - **Smoking Gun**: Look at *Deadlock* #1–#5: the sky (`tropical_sky.gdshader` + `StreetMap.make_environment`) is a **warm painterly cream-apricot horizon (`#F2E2C6`) rising into soft desaturatedcerulean-slate zenith (`#7FA6C4`)** with soft sunlit cumulus silhouettes and gentle warm aerial haze (`fog_light_color` warm golden cream, low density `0.0018`) that separates foreground street corners from distant plaza landmarks without washing out midtones!

---

## Part 2 — Actionable Engineering Specification for `@fix-map-layout`

**Owned Files**: `scripts/sim/map_layout.gd`, `scripts/sim/barangay_nav.gd`, `data/rules/map_layout.tres`, `scripts/view/street_map.gd`, `scripts/view/spaghetti_wires.gd`, `scripts/view/barangay_props.gd`, `scripts/view/arena_environment.gd` (if extracted), `shaders/barangay_ground.gdshader`, `shaders/estero_water.gdshader`, `shaders/tropical_sky.gdshader`.

### A1. Organic Curved 3-Pathway Street Network & Point-Symmetric Meandering Creek
Preserve `100%` competitive `180°` point symmetry (`f(-x, -z) == -f(x, z)`) across `arena_radius = 84.0` while abolishing every ruler-straight corridor:
1. **Shared Analytical Centerline Curves** (implement identically as static helpers in `MapLayout` AND functions in `barangay_ground.gdshader`):
   - **Center Avenue (`x_center(z)`)**: Gentle S-curve `x_center(z) = 4.2 * sin(z * 0.048)` (curves `±4.2m`, tangent angle up to `11.5°`, passes cleanly through `(0, 0)` and `(0, ±65.4)` near base plazas!).
   - **West & East Feeder Roads (`x_west(z)`, `x_east(z)`)**: Point-symmetric curved avenues `x_east(z) = 34.5 + 3.8 * sin(z * 0.052 + 0.55) - 0.055 * abs(z)` and `x_west(z) = -x_east(-z)` — bowing organically between `30m` and `38m` and curving inward toward the North/South Barangay Plazas!
   - **North & South Cross-Eskinitas (`z_south_cross(x)`, `z_north_cross(x)`)**: `z_south_cross(x) = 26.5 + 3.2 * sin(x * 0.058)` and `z_north_cross(x) = -z_south_cross(-x)`.
   - **Meandering Tabing-Ilog Creek (`z_creek(x)`)**: Odd function `z_creek(x) = 2.4 * sin(x * 0.046) + 0.8 * sin(x * 0.105)` (`z_creek(0) = 0`, `z_creek(-x) == -z_creek(x)`).
2. **Variable Street Widths, Corner Plazas & Bottlenecks**:
   - Vary carriageway + sidewalk widths along Z: tighten residential shophouse mid-blocks to **`8.2m–9.5m`** (intimate overhanging *eskinita* feel), and widen major intersections (`Center × Cross-Eskinita` at `Z ≈ ±26.5`, `Bridgehead Plazas` at `Z ≈ ±6`, and `Barangay Base Plazas` at `Z ≈ ±66`) to **`14.5m–18.5m`** sun-lit plazas!

### A2. True-Scale Rotated Parcels (`4°–18°`), Staggered Setbacks & Cohesive Urban Zoning
1. **Stop Stretching Models (`StreetMap._add_obstacle`)**:
   - Export `@export var obstacle_yaws_deg: PackedFloat32Array = []` and `@export var obstacle_kinds: PackedStringArray = []` on `MapLayout` (1-to-1 with `obstacles`, point-symmetric: mirrored parcel `i + half` gets `yaw_deg[i] + 180.0` and the matching mirrored `kind`).
   - Size every `Rect2` in `data/rules/map_layout.tres` (`16–20` half-map parcels, `32–40` mirrored total) to **true architectural lot dimensions** (`7.5m–14.5m × 6.2m–10.2m`) inset slightly (`0.35m`) inside the visual rotated footprint so `Collision.resolve` (`Array[Rect2]`) matches the 3D walls tightly with zero invisible AABB corners jutting into streets!
   - Rotate every visual building by its parcel yaw **`4°–18°`** (`deg_to_rad(yaw_deg)`, aligned to the local street tangent + staggered setback `0.8m–2.8m`) and scale **uniformly** (`sx ≈ sz` within `[0.94, 1.08]`, NEVER `2.5×`!).
   - On larger compound lots, place a **primary `.glb` building + flanking courtyard CHB wall (`0.0m` gap) + backyard/side-alley prop** (`brgy_poso_well`, grounded `brgy_sampayan_3d`, `brgy_water_drum` / crates) instead of stretching one house!
2. **Authentic 90s Barangay Urban Zoning Map (South Half `+Z`, mirrored `180°` to North `-Z`)**:
   - **Base Forecourt / Plaza (`Z ≈ +54..+74`)**:
     - `brgy_hall_plaza` facing the Base Plaza (`(0, +68)`) at an angled civic setback (`~6°`) with flagpole & bench apron;
     - `brgy_covered_court` flanking one side of the plaza approach (`~8°` yaw);
     - `brgy_kapilya` + small bell/plaza pocket flanking the opposite side (`~-9°` yaw);
     - `brgy_waiting_shed` + `brgy_sorbetes_cart` under an acacia tree in a raised soil planter at the plaza feeder throat.
   - **Commercial Cross-Road Corners (`Z ≈ +19..+35`)**:
     - **Prime Center-West Corner (`(-11.5, +21.5)`, yaw `+11°`)**: `brgy_sari_sari` ("TINDAHAN NI ALING NENA") facing the corner intersection apron, with wooden *tambayan* bench + softdrink crates on its concrete storefront apron.
     - **Prime Center-East Corner (`(+11.8, +22.0)`, yaw `-12°`)**: `brgy_shophouse_row` / `karinderya` ("KARINDERYA & PANADERYA") facing the corner plaza.
     - **West Feeder Junction (`(-44.5, +21.0)`, yaw `+14°`)**: **Cohesive TODA Terminal** — true-scale `brgy_toda_shed` (`1.0×` scale!) + **2 `brgy_tricycle` units parked in an angled `18°` queue right beside it** (`<= 3.5m` apart, NOT `15.8m` away!).
     - **East Feeder Roadside Bay (`(+44.2, +19.5)`, yaw `-10°`)**: **Vulcanizing & Kalye Talyer (`brgy_tabi_cluster_b` / workshop)** with `brgy_owner_jeep` pulled onto its concrete roadside apron (`yaw = -24°`) + `brgy_passenger_jeepney` parked along the widened outer lay-by near `brgy_palengke_stall` talipapa row!
   - **Inner *Looban* & Residential Blocks (`Z ≈ +8..+18` and `+34..+48`)**:
     - `brgy_house_two_storey`, `brgy_tabi_cluster_a`, `brgy_tabi_cluster_b` rotated `5°–16°` with staggered `1.0m–2.6m` setbacks, framing an intimate residential *eskinita* pocket with `brgy_poso_well` on a mossy wet wash apron and grounded `brgy_sampayan_3d` clotheslines in the side yard!
   - **Tabing-Ilog Riverfront & 4 Dry Bridgehead Merienda Stands (`Z ≈ -6..+6`)**:
     - Place **4 point-symmetric `powerup_spots`** on **dry raised cobblestone bridgehead aprons** (e.g., `(-31.5, +5.4)`, `(+31.5, -5.4)`, `(-5.6, -5.2)`, `(+5.6, +5.2)` — exact `180°` symmetry!), each dressed with `brgy_merienda_stand` outside the traffic choke.
     - Place `brgy_ilog_steps` descending cleanly from the bank crest **into** `z_creek(x)` water margin (`Y = -0.14m`), with `brgy_laundry_stones` seated IN the shallow water edge (`Y = -0.15m`) right at the bottom step!
   - **Abolish Military Bunkers & Duplicate Primitive Overlays**:
     - Delete `_build_90s_shopfront_vignettes_and_gates()`'s glued-on `LowPoly` wall boxes and floating `Label3D`s (`street_map.gd:647-745`)!
     - Do NOT double-spawn `brgy_poster_cluster` on top of `brgy_meralco_post` (`street_map.gd:564-567`); place `brgy_poster_cluster` standalone at street-corner curb plinths (`min_y = 0`).
     - Tie the North/South Barangay Welcome Arch (`Z ≈ ±83.8m`) directly into the continuous perimeter compound CHB wall (`R = 84.0m`) with `0.00m` gap, topped with bougainvillea planters and iron compound gates closing off visually continuing exterior road stubs!

### A3. Sunken Tabing-Ilog Channel, 3D Raised Sidewalks & Grounded Tree Planters
1. **Depressed Canal Bed (Zero Floating Water, Zero Black-Outlined Band-Aids)**:
   - Replace the flat `PlaneMesh` ground with a subdivided radial/grid `ArrayMesh` (`~0.8m–1.2m` vertex spacing near `z_creek(x)`) that **depresses `Y` smoothly along `z_creek(x)`**:
     - Deep creek stretches (between pathways): bed `Y = -0.30m`, `EsteroWater` surface curved/fitted at `Y = -0.12m`, flanked by **3D sloped mossy stone/concrete riprap embankment walls** (`Y = -0.30m .. +0.12m`, ZERO hull outline!).
     - 3 Pathway Shallow Fords (`x_west`, `x_center`, `x_east`): gentle `6°` stone-cobbled causeway depression (`Y = 0.00m` road ramping smoothly to `Y = -0.09m` across the ford so `Y = -0.06m` shallow water laps `3 cm` ankle-deep over visible submerged river cobbles!).
   - Delete `TabingIlogBanks`'s primitive black-outlined `BoxMesh` slabs (`street_map.gd:442-518`).
2. **3D Raised Concrete Sidewalks & Tree Planter Borders**:
   - Build continuous **beveled 3D concrete sidewalk ribbons (`height = 0.13m`, beveled `45°` curb face + recessed gutter line)** hugging each building block perimeter along the curved carriageways, with sloped wheelchair/tricycle curb-cut aprons at intersections and TODA/talyer pull-offs!
   - Every tree (`BarangayProps` & perimeter trees) MUST sit inside a **grounded 3D octagonal/square mossy concrete/CHB tree-pit curb (`height = 0.18m`) filled with dark brown soil mesh** (and DELETE the random blue cylinder `kit.drum` spawned next to every tree at `street_map.gd:1192-1194`!).

### A4. *Deadlock*-Grade Macro-Blended `barangay_ground.gdshader`, `estero_water.gdshader` & Daylight
1. **`shaders/barangay_ground.gdshader` — ABOLISH THE `3.0m` GRID**:
   - **Delete `grid_line(xz / 3.0, ...)` on roads and concrete pads completely!**
   - Follow the exact curved street SDFs (`d_west`, `d_center`, `d_east`, `d_eskinita`, `d_creek`):
     - **Carriageways**: Rich warm dark weathered provincial asphalt (`vec3(0.20, 0.19, 0.18)` to `vec3(0.29, 0.27, 0.25)`) with macro aggregate patch repairs (`14m–32m` FBM), subtle dusty tire-track wear along lane centers, **terracotta/ochre brick-cobble paving aprons** at major intersections and plaza mouths, **worn cream painted crosswalk zebra bars** (`vec3(0.56, 0.53, 0.47)`) perpendicular to street tangents before intersections, and **dark recessed wet gutter seams (`vec3(0.11, 0.11, 0.10)`)** along curb edges!
     - **Lot & Storefront Aprons**: Warm aged poured concrete (`vec3(0.44, 0.42, 0.38)`) with sparse irregular crack lines and **soft dark building-footprint contact AO** (`0.55m–1.3m` falloff), transitioning into compacted warm laterite soil (`vec3(0.34, 0.27, 0.20)`) and lush tropical berm grass patches in backyards/perimeter strips.
     - **Canal Bed & Banks**: Wet dark river pebbles, olive moss, and damp silt along `z_creek(x)`.
     - **Preserve Test Contract**: Keep exact string `clamp(col, vec3(0.08), vec3(0.65))` (`test_visual_polish.gd:59`)!
2. **`shaders/estero_water.gdshader`**:
   - Shift from neon cyan to realistic **olive-jade / warm tea shallow creek water** (`deep = vec3(0.09, 0.19, 0.16)`, `shallow = vec3(0.23, 0.30, 0.21)`), soft pebble refraction/caustics on shallow fords, subtle sun specular glints, and soft shore foam around rocks/steps.
3. **Remove Black Hull Outlines From Environment & Tune *Deadlock* Painterly Sky**:
   - Remove `next_pass = _outline_material()` from `StreetMap._instantiate_kit` (`L1280`), `TabingIlogBanks` (`L515`), shopfronts (`L741`), and `BarangayProps.make_kit` (`L132`).
   - Tune `tropical_sky.gdshader` and `StreetMap.make_environment()` to *Deadlock*'s warm cream-gold horizon + soft slate-cerulean upper sky + subtle warm atmospheric fog while strictly keeping `StreetMap.AMBIENT_ENERGY`, `SUN_ENERGY`, `SUN_ANGLES`, `shadow_opacity = 0.66`, and `SkyFill` within `test_visual_polish.gd:60-73` bounds!

### A5. 100% Two-Ended Anchored `SpaghettiWires`, `Sampayan` & `Banderitas` (ZERO Floating Endpoints!)
1. **`SpaghettiWires` (`scripts/view/spaghetti_wires.gd`)**:
   - **DELETE `alley_corridors` (`L54-90`), DELETE blind outer `ring_r + 5.8` drops (`L126-131`), DELETE mid-wire vertical `_append_wire_coil` rabbit ears (`L48-51, 86-104`), and DELETE `MIN_WIRE_Y = 4.55` clamping (`L392`)!**
   - Pass `StreetMap`'s **actual placed pole transforms AND actual placed buildingweatherhead world sockets** (`Array[Vector3]` computed from each placed building's `global_transform * local_weatherhead_offset`) directly into `SpaghettiWires.build_wires(poles, building_weatherheads)`!
   - **Pole-to-Pole Trunk Spans**: Connect ONLY actual insulator tips on `ElectricPole_i` crossarms (`pole_transform * local_insulator_pos`, `Z = 7.45m, 11.65m, 13.25m`) to neighboring `ElectricPole_j` crossarm insulators (`distance <= 46m`), with realistic catenary sag! Any figure-8 slack coil is hung **underneath** the telco crossarm (`Z = 7.1m`) within `0.65m` of the pole trunk, NEVER floating mid-street!
   - **Pole-to-House Service Drops**: For each placed building with a valid `weatherhead_world_pos`, connect the nearest pole's secondary/telco rack (`Z = 7.45m`) **directly to `weatherhead_world_pos`**, rendering a small porcelain strain-insulator clevis right at `weatherhead_world_pos`! If a pole has no nearby house within `26m`, it emits **zero** blind outward drops!
2. **`Sampayan` (`brgy_sampayan_3d`) & `Banderitas` (`brgy_banderitas`)**:
   - Place `brgy_sampayan_3d` ONLY as **grounded yard/riverbank T-pole clotheslines (`min_y = 0.0`)** in residential side yards / Tabing-Ilog laundry zones OR stretched strictly between two verified 2-storey upper wall cleats (`to_global` endpoints, with wall brackets touching masonry at both ends).
   - Span `brgy_banderitas` strictly between two verified world anchor points `A` and `B` (pole collar at `Y = 5.2m` `<->` building fascia anchor at `Y = 4.6m–5.4m`), setting position = `(A + B) * 0.5`, yaw = `atan2(B.z - A.z, B.x - A.x)`, and X-scale = `A.distance_to(B) / 12.0` so **both string ends touch solid geometry (`0.00m` air gap)**!

---

## Part 3 — Actionable Engineering Specification for `@fix-3d-kit`

**Owned Files**: `tools/blender/build_barangay_kit.py`, `tools/blender/build_province_90s_props.py` (plus modular helpers `tools/blender/barangay_v3_*.py` if desired), `assets/models/barangay/**`.

### B1. Mandatory Global Kit Rules Across All 28 `brgy_*.glb` Assets
1. **Cut Real Recessed Architectural Reveals (`-0.16m` to `-0.32m` Inward) — STOP Gluing Flat Boxes Onto Walls!**:
   - Upgrade `KitBuilder.add_walled_room` / wall builders so **every storefront opening, doorway, porch (*silong*), and window sits inside an inward-extruded `16cm–30cm` recessed wall reveal** framed by projecting `4cm–7cm` concrete sills, lintel hoods, and corner pilasters (`0.22m × 0.22m` columns at building corners)!
2. **True 3-Face Barangay Asymmetry on ALL Buildings (`tabi_cluster_a`, `tabi_cluster_b`, `shophouse_row`, `sari_sari`, `house_two_storey`, `hall_plaza`)**:
   - **`-Y` Front Face**: Recessed storefront / *silong* porch, grounded concrete threshold step(s) (`min_z = 0.0`), iron *rehas* / serving *pasamano* counter / jalousie-capiz windows, sloped *yero* or striped *trapal* awning on **triangulated steel knee-brace struts**, and crisp hand-painted 90s Tagalog signage (`TINDAHAN NI ALING NENA`, `KARINDERYA`, `PANADERYA`, `BARBERYA • GUPIT BINATA`, `VULCANIZING SHOP`, `BARANGAY HALL`).
   - **`±X` Side Faces (Firewall / Eskinita Anatomy)**: Unpainted/patch-plastered gray hollow-block (`chb_raw_moss` / `stucco_aged`) party firewall with stepped parapet flashing (`+0.22m` above roof), **round glass Meralco kWh electric meter (`Z = 1.75m`) + vertical gray PVC conduit pipe (`Ø 4cm`) rising to an explicit steel/porcelain Service-Drop Weatherhead bracket near the front-side eave (`Z = 4.4m` on 1-storey, `Z = 6.2m` on 2-storey)**, high small bathroom louver vents (`Z = 2.15m`), and lower alley stencil (`BAWAL UMIHI DITO`). **Document the exact `(x, y, z)` weatherhead socket offset per building so `@fix-map-layout` connects `SpaghettiWires` with `0.00m` error!**
   - **`+Y` Rear Face (*Likod-Bahay* Dirty Kitchen Anatomy)**: Recessed wooden back door + concrete threshold step (`min_z = 0.0`), corrugated tin lean-to shed over a **concrete/tiled *lababo* sink counter**, **200L blue plastic water drum** positioned right under the rear roof downspout shoe, kitchen smoke louver/exhaust hood, and PVC drain pipe to `Z = 0.0`.
3. **Structural Integrity on Every Roof, Overhang & Upper Floor**:
   - Add **timber/concrete corbel brackets & exposed floor beam joists** under every cantilevered 2nd storey (`house_two_storey`, `tabi_cluster_a`, `shophouse_row`).
   - Add **fascia boards + U-profile sheet-metal *alulod* box gutters** along every low roof eave + **vertical PVC/GI downspout pipes (`Ø 7.5cm`) with wall straps reaching `Z = 0.0`!**
   - Replace `covered_court`'s thin sticks with real **triangulated steel roof trusses** (top chord, horizontal bottom tie-chord, diagonal web struts), thick concrete columns with chamfered plinth bases, painted backboard + braced rim, and stage fascia banner.
4. **Crisp High-Res Atlas + Tangent-Space Normal Maps + Material Roughness Separation**:
   - Replace blurry `5×7` bitmap text (`_FONT_5X7`) with **crisp high-resolution vector/polygon-rasterized 90s Filipino signboard typography** (clean diagonal/curved strokes, drop shadows, painted borders) in `512px`-wide dedicated signboard strips so `TINDAHAN NI ALING NENA`, `KARINDERYA`, `PANDESAL MAINIT PA`, `TODA TERMINAL`, `VULCANIZING`, `BARANGAY KALYEAH`, and `HALALAN '95` read razor-sharp at player camera distance!
   - Bake and connect **tangent-space Normal Maps** (`BarangayAtlas_normal.png` & `ProvincePropsAtlas_normal.png`) + **Roughness/Metallic texture or per-swatch ORM** (corrugated *yero* ridges, CHB mortar grooves, brick bonds, wood planks, capiz lattice, tire treads; shiny `0.25–0.35` glass/galvanized tin vs `0.86` plaster/CHB) AND rich `COLOR_0` vertex AO (darkening recessed window/doorway interiors, under-eave soffits, and `Z = 0..0.35m` ground contact)!
   - Ensure every `.png.import` / `.jpg.import` written in `assets/models/barangay/` has `compress/mode=2`, `mipmaps/generate=true`, `"vram_texture": true`, `compress/normal_map=1` on normal maps, and **zero** `md5` or `generator_parameters` lines (`test_texture_budget.gd`)!

### B2. Specific Prop & Vehicle Fixes in `build_barangay_kit.py` & `build_province_90s_props.py`
- **`brgy_cover_wall`**: Replace the out-of-place military sandbag bunker (`sandbag_burlap` + rebar spikes) with an **authentic waist-high Barangay Corner Planter / Tambayan Barrier** (`4.2m × 0.95m × 1.32m`): plastered/CHB low wall with decorative concrete breeze-block (*ventanilla*) perforations, terracotta bougainvillea/gumamela planter trough on top, and stacked wooden *kaha* crates / blue water jug at one end!
- **`brgy_sampayan_3d`**: Include **two grounded bamboo/GI T-posts (`min_z = 0.0`, height `2.28m`) with concrete-filled paint-can/tire bases + diagonal guy braces** so every standalone yard/riverbank `sampayan` stands physically on the ground (`min_y == 0.0`) with zero floating wires!
- **`brgy_ilog_steps` & `brgy_laundry_stones`**: Widen `brgy_ilog_steps` side stone cheek-walls with mossy river-stone coping and ensure `min_z == 0.0` cleanly seats into the sloped creek bank; enrich `brgy_laundry_stones` with flat wet basalt *pukpukan* washing boulders, wooden *palo-palo* paddle, colorful plastic *batya* & *timba*, and soap dish.
- **`brgy_kapilya`**: Carve an **arched recessed portico entrance** with decorative wrought-iron grille gates, recessed altar cross + candles inside, front roof *kampana* bell belfry, concrete flower boxes, and roof gutters + downspouts to `Z = 0`.
- **`brgy_bakery_oven`**: Upgrade into a complete **Panaderya Corner Kiosk / Pugon Bakehouse Annex** with red-brick arched oven mouth, glass *estante* bread display counter (`MAINIT NA PANDESAL`), sheet-metal hood, and vertical brick/GI chimney stack (`min_z == 0.0`).
- **`brgy_toda_shed`, `brgy_tricycle`, `brgy_passenger_jeepney`, `brgy_owner_jeep`**: Keep all wheels grounded at `min_z == 0.0` with wheel chocks, recessed windshields/windows (not stuck-on grey boxes!), crisp route signboards (`BRGY. KALYEAH - BAYAN`), side-mirror stalks, and realistic tire-to-fender proportions (`brgy_toda_shed` roof tire `radius = 0.34m`, never giant!).

---

## Part 4 — Test & Verification Synchronization for `@audit-10`

1. **`tests/unit/test_barangay_5v5.gd` Synchronization**:
   - Line `167`: Update `assert_eq(half_obs, 14, ...)` to match Overhaul v3's true-scale parcel count (`16–22` half-map parcels, still verifying `obs.size() == half_obs * 2` and exact `Rect2(-obs[i].end, obs[i].size)` point symmetry!).
   - Lines `176–180` & `423`: Update `powerup_spots` assertion from `3` mid-water spots to **4 dry bridgehead plaza Merienda stands** (`sim.powerups.size() == 4`, point-symmetric pairs).
   - Lines `228–233`: Replace straight-ruler `is_clear(Vector2(lane_x, 10.0), Vector2(lane_x, -10.0))` with curved-pathway navigation/ford reachability along `MapLayout`'s West, Center, and East curved S-bend fords!
2. **Mandatory Preserved Invariants (Must Pass Unchanged)**:
   - `tests/unit/test_visual_polish.gd`: `clamp(col, vec3(0.08), vec3(0.65))` in `barangay_ground.gdshader`, `AMBIENT_ENERGY` in `[0.35, 0.55]`, `upward` within `1.0 ± 0.12`, `shadow_opacity ≈ 0.66`, `SkyFill` active.
   - `tests/unit/test_model_scale.gd`: All 5 `BARANGAY_HOUSES` + `>= 12` distinct `brgy_*` kits under `%Geometry`, true-scale widths `[3.0, 24.0]`, heights `[2.8, 12.0]` (`2.0–8.5` kids tall).
   - `tests/unit/test_texture_budget.gd`: Every `.png.import` / `.jpg.import` in `assets/models/barangay` uses `compress/mode=2` (`COMPRESS_VRAM`), normal maps set `compress/normal_map=1`, zero `md5=` lines.
3. **Checkpoint 5 Re-Interrogation Gate (`brutal-critic` Sign-Off Required)**:
   - `@audit-10` must capture `1280×720` windowed Vulkan/GL renders covering: (a) Bird's-Eye 3 Curved Pathways + Meandering Creek, (b) Street-Level Corner Shophouse Plaza (showing Front/Side/Rear asymmetry, 3D curbs, asphalt/crosswalk ground, and two-ended anchored wires), (c) Tabing-Ilog Depressed Ford & Descending Steps + Laundry Stones + Dry Bridgehead Merienda Stand, (d) TODA Terminal + Queued Tricycles + Vulcanizing Talyer + Jeepney Bay, and (e) Full Deadlock HUD + Rigged 3D Kid in Combat.
   - `brutal-critic` will inspect every pixel of Checkpoint 5 against all 45 questions above and reject anything below an honest **`10.0 / 10`**.


---

## Part 5 — Checkpoint 5 (Round 1) Pixel-by-Pixel Re-Interrogation (`7.1 / 10 — REJECTED`)

All 9 live `1280×720` `gl_compatibility` renders from Checkpoint 5 Round 1 (`checkpoint5_*.png`) were inspected pixel-by-pixel against the 5 owner-uploaded Valve *Deadlock* reference screenshots and the 10 character concept sheets (`assets/concepts/*_concept.png`).

### Round 1 Honest Scorecard (`7.1 / 10 — REJECTED`, Target: `10.0 / 10`)

| Domain | Round 1 Score | Status | Summary Verdict |
| :--- | :---: | :---: | :--- |
| **1. Map Layout, Zoning & Ground Shader (`@fix-map-layout`)** | **7.6 / 10** | **REJECTED** | Curved S-bend pathways, staggered parcel rotations, dry creek ford layout, and Meralco network are a massive leap from `4.8`, **but** foreground asphalt has diagonal shadow-acne comb stripes (`////////`), road shader looks smooth vinyl-grey instead of coarse aggregate + crisp crosswalks, 1 wire clips through the TODA 2-storey roof eave, creek banks have highway-dash neon-green stripes + rigid parallel surfboard ford stones, 1 Merienda stand perches half-off the sloped creek bank, and TODA shed sits on 3 stacked beige slabs. |
| **2. Barangay 3D Architecture & Props (`@fix-3d-kit`)** | **8.4 / 10** | **REJECTED** | Recessed reveals, 3-face Front/Side/Rear asymmetry, crisp signboards, normal-mapped atlases, and grounded plinths look strong, **but** `brgy_sari_sari`'s wooden front bench blocks half the front door, rear dirty-kitchen lean-to posts are skinny unbraced sticks without an eave header beam, and `brgy_mango_tree` canopies read as faceted low-poly icosphere golf balls. |
| **3. Rigged 3D Hero Roster (`@fix-kids-3d` + `@audit-10`)** | **5.8 / 10** | **REJECTED** | Shots 6 & 7 (`valorant_10kids_lineup`, `junjun_kiko_closeup`) captured models facing `-Z` away from the `+Z` camera (10 backs of heads). Forward view in `wildrift_main_screen` exposes `~1:7.7` adult mannequin head-to-rig-height proportions, elongated cylindrical neck, symmetric C-bowed `Idle` arms, and unshaded blown-out `#FFFFFF` white shirt albedo. |
| **4. Deadlock Camera Framing & Combat VFX (`@fix-cam-vfx`)** | **6.6 / 10** | **REJECTED** | `deadlock_street_combat` frames the hero tiny (`~20%` screen height at `X ≈ 44%`) pitched down `15.5°` at empty road instead of *Deadlock*'s close low-shoulder street-canyon framing (`~38–42%` height at `X ≈ 38%` beside the waist-left HP bar, `9.5°` pitch). `BUFF_TURON` and `Bagsak` render opaque yellow cheese-wheel discs hiding the street, and `Langit-Lupa` spawns a tall boxy brown block. |
| **5. Wild Rift Main Screen UI (`@fix-main-ui`)** | **7.2 / 10** | **REJECTED** | Layout hierarchy is clean, **but** 3 bottom CTA labels clip/truncate (`"PALITAN · 1/1"` + clipped `"MGA BATA"`, `"VS BARANGAY BOTS · CH"`, sliced `"PLAY · AGAWANG BASE"`) and top-left `"GABO / KALYEAH"` collides with bright background shop signage without a soft dark gradient scrim. |
| **OVERALL CHECKPOINT 5 (ROUND 1)** | **7.1 / 10** | **REJECTED** | **Must execute all Round 2 surgical specs below and re-render until `10.0 / 10`.** |

---

### Round 2 Surgical Work Orders (Zero Tolerance — Every Item Mandatory for `10.0 / 10`)

#### A. `@fix-map-layout` — 7 Exact Arena, Shader, Shadow & Wire Fixes
1. **Eliminate Directional Shadow-Acne Comb Stripes (`////////`) on Roads (`scripts/view/arena_environment.gd:60-65`)**:
   - Observed in `checkpoint5_toda_talyer_zoning.png` bottom foreground: `SHADOW_DISTANCE = 185.0` across 2 splits with `shadow_bias = 0.035` and `shadow_normal_bias = 1.35` produces diagonal shadow-acne comb bands across horizontal road planes in `gl_compatibility`.
   - **Fix**: Tighten `SHADOW_DISTANCE` to `135.0`, set `sun.directional_shadow_split_1 = 0.22`, raise `sun.shadow_bias = 0.062`, and raise `sun.shadow_normal_bias = 2.15` (preserving `sun.shadow_opacity = 0.66` and `sun.shadow_enabled = true` for `test_visual_polish.gd`).
2. **Coarse Weathered Deadlock Asphalt, Tar Cracks, Manholes & High-Contrast Crosswalks (`shaders/barangay_ground.gdshader`)**:
   - Observed in `plaza_canal.png`, `toda_talyer_zoning.png`, and `vfx_visual_shaders.png`: road surface reads like smooth airbrushed grey vinyl (`grit * 0.024` at `L213` is invisible at `720p`) and crosswalk bars are barely perceptible.
   - **Fix**: Boost multi-frequency aggregate stone-chip stipple (`grit` amplitude `0.065–0.085`), add darker wet-look branched tar-seal repair cracks + subtle wheel-rut oil darkening along lane centers, add procedural circular cast-iron DPWH storm-manhole lids (`r = 0.48m` scored concentric rings at major intersections), and sharpen worn cream pedestrian zebra crosswalk bars (`col` contrast up to `0.56–0.62`, strictly respecting `clamp(col, vec3(0.08), vec3(0.65))`) at all 6 road intersections AND immediately north/south of all 3 creek ford crossings!
3. **Zero Roof-Clipping Overhead Wires (`scripts/view/spaghetti_wires.gd:71-101`)**:
   - Observed in `checkpoint5_toda_talyer_zoning.png` upper-center: a service-drop wire slices straight through the overhanging corrugated *yero* roof eave of the 2-storey house behind `BRGY. TODA TERMINAL` because `to_pole.normalized().dot(out_n_xz) >= 0.08` accepts near-parallel grazing rays without checking 3D OBB clearance against overhanging eaves or neighboring buildings.
   - **Fix**: Raise built-in socket acceptance to `dot >= 0.38` AND add `_segment_hits_building_obb(pin_start, spool_tip, buildings)` (inflating building top half-extents by `+0.55m` for roof overhang eaves); when a built-in side socket is obstructed by its own eave or an adjacent building, mount the 3D pole-facing corner/front-eave bracket (`builtin: false` pushed to the clear pole-facing corner) or skip/reroute to another pole so **zero** wires intersect any roof or wall!
4. **Organic Mossy Riverbank Stones & Natural Clustered Creek Ford Cobbles (`scripts/view/street_map.gd:495-590`)**:
   - Observed in `checkpoint5_deadlock_plaza_canal.png` and `birdseye_organic_lanes.png`: `s_i % 3 == 0` / `% 2 == 0` (`street_map.gd:501, 510`) alternates neon-green and grey-white rectangular bank segments like a Formula-1 racetrack curb, and the 8 ford stepping cobbles (`street_map.gd:571-584`) float in two rigid parallel rows like grey surfboards (`Y = -0.075`).
   - **Fix**: Replace modulo-striped `moss_top` bank segments with smooth organic earthy olive-brown / weathered mossy river-stone variation (`s <= 0.38`, zero periodic green/grey alternation!), and scatter 11–14 irregular broad flat basalt ford boulders per crossing with jittered `(x, z)` offsets, varied radii (`0.55m–1.05m`), non-uniform yaw, and deeper seating (`Y = -0.14..-0.105`) with wet dark rims so they look embedded in the creek bed.
5. **Enforce Dry Flat Plaza Placement for ALL 4 Merienda `PowerupStand`s (`scripts/view/street_map.gd:669-678`, `data/rules/map_layout.tres`)**:
   - Observed in `checkpoint5_deadlock_plaza_canal.png` center-right: one Merienda `PowerupStand` sits right on the green sloped creek bank (`Z ≈ 4m`), jutting over the water!
   - **Fix**: Ensure every `powerup_spots` coordinate and `StreetMap` stand placement enforces `absf(stand_pos.z - MapLayout.canal_z(stand_pos.x)) >= 6.8` on dry flat bridgehead sidewalk/plaza concrete (`0` overhang onto sloped creek banks!).
6. **Eliminate Triple-Stacked Wedding-Cake Concrete Pads Under `toda_shed` / `palengke_stall` (`scripts/view/street_map.gd:1145-1168`)**:
   - Observed in `checkpoint5_toda_talyer_zoning.png` foreground: `BRGY. TODA TERMINAL` has its own `.glb` slab sitting on a bright beige `curb_top` rounded box PLUS a protruding front tongue slab (`3` stacked concrete tiers!).
   - **Fix**: Darken `curb_top` from `Color(0.50, 0.47, 0.42)` to `Color(0.41, 0.39, 0.35)` so 3D parcel curbs blend seamlessly into `barangay_ground.gdshader` sidewalks, and skip the extra protruding front tongue slab (`street_map.gd:1158-1164`) whenever `kit_id` already includes a built-in slab/step (`toda_shed`, `palengke_stall`, `bakery_oven`, `kapilya`).
7. **Bird's-Eye Exterior Creek Blend & Harmonized South Base Plaza (`shaders/barangay_ground.gdshader`)**:
   - Observed in `checkpoint5_birdseye_organic_lanes.png`: a stark grey concrete stripe extends outside the `84m` curved perimeter wall to `X = ±150m`, and South Base `(0, +64)` draws a stark grey rectangular court slab sticking out of the circular blue `Agawang Base` chalk ring into brown dirt.
   - **Fix**: Beyond `r_dist > arena_radius + 2.5`, fade the exterior creek into lush natural green-brown provincial stream earth/foliage (zero grey concrete outside the wall!), and blend South Base `(0, +64)` into the same warm sun-baked terracotta/ochre circular plaza stone as North Base (`enemy_base_center`), keeping subtle faded court key markings strictly *inside* the circular base plaza radius!

#### B. `@fix-3d-kit` — 3 Targeted Barangay 3D Asset Fixes
1. **Unblock `brgy_sari_sari` Front Door (`tools/blender/build_barangay_kit.py:133-136`)**:
   - Observed in `checkpoint5_barangay_front_side_rear.png` bottom-left: the front wooden *tambay* bench (`X = -2.55..-1.35`) sits directly across the recessed wooden front door (`X = -3.05..-1.95`), blocking entry!
   - **Fix**: Move the wooden bench rightward to `X = +0.35..+1.75` (`center_x = +1.05`) directly underneath the striped *sari-sari* serving counter window so the front door and threshold step are `100%` clear.
2. **Structural Timber Framing on Rear Dirty-Kitchen Sheds (`tools/blender/barangay_v3_arch.py` `add_rear_dirty_kitchen`)**:
   - Observed in `checkpoint5_barangay_front_side_rear.png` right showcase house: the rear lean-to tin roof rests on two thin toothpick-like vertical sticks with no horizontal front eave beam or diagonal knee braces.
   - **Fix**: Upgrade rear shed posts to sturdy `0.095m × 0.095m` timber posts on concrete foot blocks, add a `0.09m × 0.11m` horizontal front eave header beam + side rafters under the corrugated tin, and add `45°` diagonal timber knee braces anchoring the front posts back to the rear wall!
3. **Painterly Multi-Tone Normal-Smoothed `brgy_mango_tree` Canopy (`tools/blender/build_province_90s_props.py` `build_mango_tree`)**:
   - Observed in `checkpoint5_toda_talyer_zoning.png` far-left: the mango tree crown looks like 4 flat-shaded low-poly green golf balls / icospheres.
   - **Fix**: Subdivide/sculpt organic overlapping foliage cloud lobes with **outward radial smooth vertex normals** (baked from canopy center so individual triangles never facet!) + dark-olive interior shadow leaf clusters under warm sunlit top leaf clusters (`COLOR_0` canopy AO from `0.55` inner/underside to `1.0` crown top), matching *Deadlock*'s lush painterly street trees!

#### C. `@fix-kids-3d` & `@audit-10` — Stylized Hero Proportions, Shaded Cotton Albedo & Dynamic Front-Facing Lineups
1. **Fix Backwards `-Z` Facing in `capture_checkpoint5.gd` Shots 6 & 7 (`@audit-10`)**:
   - Observed in `valorant_10kids_lineup.png` and `junjun_kiko_closeup.png`: models face `-Z` away from the `+Z` camera, showing 10 backs of heads!
   - **Fix**: Rotate lineup models `rotation.y = PI` (facing `+Z` directly toward the studio camera + 3-point front key/rim lighting), and in Shot 7 (`junjun_kiko_closeup.png`) show front `3/4` hero angles (`rotation.y = PI - 0.32` / `PI + 0.32`) plus compact angled rear/side callouts so both facial craftsmanship AND back silhouette accessories (`kumot` cape, backpack, headband tails) are crystal-clear in one frame! Also move Shot 3 (`barangay_front_side_rear.png`) off `Z = 0` onto dry paved ground (`Z = +26m` plaza or dry shader offset) so creek water never renders under the 3 showcase houses!
2. **Stylized `1:5.5` Hero Proportions, Shaded `#DDD3C0` Fabric & Asymmetric `Idle` Poses (`@fix-kids-3d` in `tools/blender/build_valorant_kids_v3.py`)**:
   - Observed on forward-facing `junjun` in `wildrift_main_screen.png`: head-to-rig-height ratio reads `~1:7.7` like an elongated adult mannequin with a long narrow neck cylinder, symmetric C-curved tube arms, and blown-out `#FFFFFF` white shirt albedo.
   - **Fix**: Within the exact `1.70m` normalized rig envelope, re-proportion all 10 hero meshes to crisp stylized *Valorant* hero proportions (`~1:5.4–1:5.6` head-to-rig-height ratio, expressive sculpted head/hair silhouette, shorter sturdy neck column, tapered stylized forearms/hands and chunky *tsinelas*/sneakers), shade light cotton fabrics to warm cream `#DDD3C0`–`#E2D8C5` with painted vertical ribbing, cloth fold gradients, and collar/armhole AO (`0` pure `#FFFFFF` blowout!), and author distinct confident asymmetric `Idle` bone poses (weight shifted onto one hip, relaxed natural elbow/wrist angles, signature prop/stance personality).

#### D. `@fix-cam-vfx` (+ `@audit-10` Test Sync) — True *Deadlock* Street-Canyon Framing & Translucent Ground VFX
1. **Close Low-Shoulder *Deadlock* Camera Framing (`data/rules/camera_rules.tres`, `scripts/view/arena_camera.gd`, synced with `tests/unit/test_shoulder_view.gd:46-50`)**:
   - Observed in `checkpoint5_deadlock_street_combat.png`: `shoulder_distance = 4.15`, `shoulder_height = 1.88`, `shoulder_offset = 0.62`, `look_pitch_degrees = 15.5`, `kid_view_scale = 0.82` shrinks the hero to `~20%` screen height near center (`X ≈ 44%`) while pitching down at empty asphalt.
   - **Fix**: Set `kid_view_scale = 0.96`, `shoulder_distance = 2.85`, `shoulder_height = 1.56`, `shoulder_offset = 0.82`, `look_pitch_degrees = 9.5`, `shoulder_fov = 66.0`! Geometrically verified: this projects the hero at `~38–42%` screen height at `X ≈ 38.1%` right beside the angled waist-left HP bar (`on_screen.x < size.x / 2.0` and `on_screen.y > size.y / 2.0` both stay `true`!), while lifting the horizon so 2-storey shophouses, awnings, signboards, and overhead Meralco wires tower across the upper frame identically to Valve's *Deadlock*! (`@audit-10` updates the 5 exact float assertions at `tests/unit/test_shoulder_view.gd:46-50`.)
2. **Translucent Ground Zone / Buff Shaders & Sculpted Low `Langit-Lupa` Platform (`scripts/view/fx_kit.gd`, `scripts/view/street_mode_view.gd`, `shaders/vfx_*.gdshader`)**:
   - Observed in `deadlock_street_combat.png` and `vfx_visual_shaders.png`: `BUFF_TURON` and `Bagsak` paint opaque bright-yellow cheese-wheel discs that completely obliterate the asphalt beneath them, and `Langit-Lupa` spawns a tall boxy brown chocolate-bar block.
   - **Fix**: Cap interior fill opacity on all ground discs/buffs/zones to **`alpha <= 0.10`** (`90%+` see-through interior!) with crisp glowing Fresnel perimeter rings + thin scrolling shockwave arcs so road grain/chalk stays 100% visible underneath, and replace the tall boxy brown `Langit-Lupa` block with a low beveled stone/earth curb step (`height <= 0.36m`) with warm rim highlight so it reads as playable street elevation rather than a solid brown cube!

#### E. `@fix-main-ui` — Zero Text Clipping & Wild Rift Readability Scrims (`scripts/ui/title_screen.gd`)
1. **Bottom-Left Roster Switch Button (`"PALITAN · 1/10 / MGA BATA"`)**:
   - Bind total roster count dynamically (`1/10` instead of `1/1`!) and increase button `custom_minimum_size.y` / reduce internal vertical padding so `"MGA BATA"` has `>= 8px` bottom breathing room (`0` pixel clipping!).
2. **Bottom-Right Mode Subtitle (`"VS BARANGAY BOTS · CHALLENGE"`)**:
   - Eliminate `"VS BARANGAY BOTS · CH"` mid-word truncation by widening the action cluster or tightening subtitle font/tracking so the entire string renders cleanly with `>= 12px` horizontal margin.
3. **Bottom-Right Primary Gold CTA (`"MAGLARO / PLAY · AGAWANG BASE"`)**:
   - Fix the gold button slicing off the bottom `35%` of `"PLAY · AGAWANG BASE"` — increase button height (`>= 84px`) and tighten internal VBox separation (`2px`) + top/bottom content margins (`6px`) so both lines sit centered inside the gold bevel with generous clearance.
4. **Top Header & Left Nav Scrim + Clean Hero Stage Backdrop**:
   - Add a subtle soft dark warm-charcoal gradient scrim (`alpha 0.42 -> 0.0`) behind the Top-Left header (`GABO / KALYEAH`) and Left Nav Rail (`LARO / BATA / BARANGAY / TROPA`), and nudge the 3D hero stage camera yaw/framing so bright background shop signboards (`TINDAHAN NI ALING NENA`) frame cleanly to the right of the hero instead of colliding directly behind white header typography!


---

## Part 6 — Checkpoint 5 (Round 2) Pixel-by-Pixel Re-Interrogation (`9.1 / 10 — REJECTED`, Final Micro-Polish to `10.0 / 10`)

All 9 live `1280×720` `gl_compatibility` renders from Checkpoint 5 Round 2 (`checkpoint5_*.png`) were re-inspected pixel-by-pixel against the 5 Valve *Deadlock* reference screenshots and the 10 character concept sheets (`assets/concepts/*_concept.png`).

### Round 2 Honest Scorecard (`9.1 / 10 — REJECTED`, Target: `10.0 / 10`)

| Domain | Round 1 | Round 2 | Status | Summary Verdict |
| :--- | :---: | :---: | :---: | :--- |
| **1. Map Layout, Zoning & Ground Shader (`@fix-map-layout`)** | `7.6` | **9.3 / 10** | **NEEDS 3 MICRO-FIXES** | Curved streets, brick cobbles, DPWH manholes, zebra crosswalks, volcanic-tuff creek banks, clustered basalt ford boulders, dry Merienda stands, flush TODA slab, and circular South Base plaza look genuinely AAA, **but**: (a) flat ground planes still cast self-shadows (`cast_shadow = ON` causing faint `////////` shadow-acne comb stripes in `toda_talyer_zoning` & `plaza_canal`), (b) `tar_seam` lowers `ROUGHNESS` to `0.56` with default `SPECULAR = 0.5`, reflecting cerulean `SkyFill` as pale blue-grey streaks in `street_combat` (`detail_vis` also fades too early along foreshortened `fwidth`), and (c) 1 thin wire still grazes the upper brown wall behind `BRGY. TODA TERMINAL`. |
| **2. Barangay 3D Architecture & Props (`@fix-3d-kit`)** | `8.4` | **9.7 / 10** | **NEEDS 1 MICRO-FIX** | `brgy_sari_sari` door is 100% clear, rear dirty-kitchen timber posts + header beam + `45°` knee braces look rock-solid, and *yero* roofs are crisp and matte. **1 remaining micro-fix**: `brgy_mango_tree`'s lower foliage lobe (`toda_talyer_zoning` far-left) has a near-black horizontal shadow band (`AO 0.55 × downward normal`). |
| **3. Rigged 3D Hero Roster (`@fix-kids-3d` + `@audit-10`)** | `5.8` | **8.2 / 10** | **NEEDS 3 MICRO-FIXES** | Warm `#DDD3C0` cotton shading, asymmetric contrapposto `Idle` poses, `junjun` back-head X-rubberbands + red *kumot* cape, `kiko` bare toes + forward handheld *tsinelas*, and `buknoy` sleeveless vest + inner-tubes are all verified, **but**: (a) Round-2 head `X` scaling made faces horizontally wide/oblate (`height:width ≈ 0.88:1`) instead of crisp *Valorant* tapered V-jaw proportions (`crown-to-chin height ≈ 1.22× cheek width`), (b) `kiko`/`ligaya` show an inward V-pinch at the clavicle/deltoid shoulder join, and (c) `lineup` hides `buknoy` behind `toni` & clips `nene`'s right elbow while `closeup` clips `4px` of top hair. |
| **4. Deadlock Camera Framing & Combat VFX (`@fix-cam-vfx`)** | `6.6` | **9.5 / 10** | **NEEDS 1 MICRO-FIX** | `2.55m / 1.42m / 0.68m / 9.5°` low-shoulder *Deadlock* framing + translucent ground shockwaves (`0` yellow pancake!) are transformative. **1 micro-fix**: projectile slipstream ribbon in `vfx_visual_shaders` reads as a wide opaque white pipe instead of a sleek tapered translucent energy ribbon. |
| **5. Wild Rift Main Screen UI (`@fix-main-ui`)** | `7.2` | **9.2 / 10** | **NEEDS 2 MICRO-FIXES** | `PALITAN · 1/10`, `MAGLARO / PLAY · AGAWANG BASE`, and top/left warm-charcoal scrims are razor-sharp. **2 micro-fixes**: (a) right chevron `⌄` in `ModeSelectorCard` overlaps the final **`E`** of `"VS BARANGAY BOTS · CHALLENGE"`, and (b) far-right 3D backdrop (`x = 1045..1280` right of Jeepney) has an empty flat horizon gap. |
| **OVERALL CHECKPOINT 5 (ROUND 2)** | `7.1` | **9.1 / 10** | **FINAL POLISH** | **Execute the surgical Round-3 micro-fixes below for `10.0 / 10` sign-off.** |

---

### Round 3 Final Surgical Micro-Polish Specifications (Path to `10.0 / 10`)

#### 1. `@fix-map-layout` (4 Surgical Edits)
1. **100% Eliminate Ground Self-Shadow Acne Comb Stripes (`scripts/view/street_map.gd`)**:
   - Root cause of the remaining `////////` stripes in `toda_talyer_zoning.png` (`y = 650..720`) and `plaza_canal.png` (`x = 960..1280`): the flat horizontal arena ground `MeshInstance3D` (`StreetFloor` / `CreekBedFloor`) still has default `cast_shadow = SHADOW_CASTING_SETTING_ON`, causing it to self-shadow against its own quantized GLES3 depth map!
   - **Fix**: Set `cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF` on the flat horizontal ground plane meshes in `street_map.gd` (a flat floor at `Y <= 0` casts nothing onto objects above it, while still *receiving* 100% crisp building/pole/tree/character shadows with `0.000%` self-shadow comb stripes!).
2. **Dark Matte Bitumen Tar Cracks & Full-Street Aggregate Crispness (`shaders/barangay_ground.gdshader`)**:
   - Root cause of pale blue-grey cracks on the left of `deadlock_street_combat.png`: `road_rough = 0.56` on `tar_seam` combined with Godot's default `SPECULAR = 0.5` reflects the cerulean `SkyFill` directional light at `9.5°` grazing pitch! Meanwhile `px_fw = length(fwidth(xz))` kills `detail_vis` past `6m` due to `dFdy` foreshortening.
   - **Fix**: Add `SPECULAR = 0.14;` at the end of `fragment()`, keep `tar_seam` matte-dark (`vec3(0.08, 0.08, 0.085)`, `rough >= 0.80` — remove `tar_seam * 0.65` from shiny roughness reduction!), narrow crack width (`smoothstep(0.0025, 0.0095, crack_a)`), and compute `float px_fw = min(length(dFdx(xz)), length(dFdy(xz)));` (`smoothstep(0.055, 0.22, px_fw)`) so crushed-stone asphalt grain stays crisp `25m+` down the street canyon! Also trim `keep_forecourt` (`abs(xz.y) < 98.0` rounded by `lm_mask`) so no square beige tab peeks at the bottom edge of `birdseye_organic_lanes.png`.
3. **Cull Final Grazing Wire Across `tabi_cluster_a` Upper Wall (`scripts/view/spaghetti_wires.gd`)**:
   - Observed in `toda_talyer_zoning.png` (`x = 435..485, y = 330..340`): 1 thin black wire crosses the upper dark-wood wall behind `BRGY. TODA TERMINAL`.
   - **Fix**: In `_catenary_hits_building_obb`, also test interior OBB (`half - Vector2(0.10, 0.10)`, height `+0.35m`) for `t in [0.08..0.92]` even on endpoint buildings (and inflate non-endpoint building `half` by `+0.35m`) so no service drop or trunk span can graze across a stepped 2-storey upper wall!

#### 2. `@fix-3d-kit` (1 Surgical Edit)
1. **Eliminate Pitch-Black Underside Band on `brgy_mango_tree` (`tools/blender/build_province_90s_props.py` `build_mango_tree`)**:
   - Observed in `toda_talyer_zoning.png` far-left (`x = 0..95, y = 345..400`): downward-pointing lower lobe normals (`dot(N, sun) == 0`) multiplied by `COLOR_0 = 0.55` crush the lower canopy half to near-black.
   - **Fix**: Bias custom radial canopy normals upward (`normalize(radial_out + Vector3((0.0, 0.0, 0.48)))` in Blender Z-up) and raise interior/lower `COLOR_0` AO floor from `0.55` to `0.74` so the underside of every mango tree glows with soft warm-olive sky bounce like Valve's *Deadlock* street trees!

#### 3. `@fix-kids-3d` (2 Surgical Rig/Mesh Edits + Re-Bake Portraits)
1. **Crisp *Valorant* Tapered V-Jaw Head Aspect Ratio (`tools/blender/build_valorant_kids_v3.py`)**:
   - Observed in `valorant_10kids_lineup.png`, `junjun_kiko_closeup.png`, and `wildrift_main_screen.png`: Round-2 head `X` width is too wide relative to `Y` height (`height:width ≈ 0.88:1`), making faces look horizontally squashed/wide-cheeked.
   - **Fix**: Narrow head/face `X` width by `~0.83x` (with progressive V-jaw taper `0.76x` from cheekbone down to chin tip) and add `+8%` upper cranium/hair crown `Y` loft within the exact `1.7000m` total rig bound so every hero head achieves crisp *Valorant* proportions (**`crown-to-chin height ≈ 1.20×–1.25× cheek width`**) matching `assets/concepts/*_concept.png`!
2. **Smooth Deltoid-Clavicle Shoulder Cap Join (`tools/blender/build_valorant_kids_v3.py`)**:
   - Observed on `kiko` (`closeup.png` #3) and `ligaya` (`lineup.png` center): inward V-pinch notch between upper torso and upper arm when posed.
   - **Fix**: Expand upper-arm deltoid cap socket overlap (`+18%` radius/inset into acromion) and smooth skinning weight falloff across `Shoulder` (`clavicle`) and `UpperArm` vertices so posed shoulders stay 100% continuous with zero crease/pinch; re-export all 10 `.glb`s + Cycles 2D portraits/cards preserving `compress/mode=2`.

#### 4. `@fix-cam-vfx` (1 Surgical VFX Edit Within Strict Owned Scope)
1. **Sleek Tapered Translucent Projectile Slipstream Ribbon (`shaders/visual/vs_projectile_trail.tres` / owned `fx_*` trail builder)**:
   - Observed in `checkpoint5_vfx_visual_shaders.png` center: projectile trail renders as a thick opaque white cylinder (`~1.0m` wide).
   - **Fix**: Taper trail ribbon width cleanly from head to tail and soften core alpha (`<= 0.65` fading smoothly to `0.0` at lateral edges and tail with warm golden-cyan edge tint) so it reads as an agile aerodynamic slipstream ribbon without hiding combatants behind a solid white pipe.

#### 5. `@fix-main-ui` (2 Surgical Edits in `scripts/ui/title_screen.gd` & `scripts/ui/kalyeah_theme.gd`)
1. **Fix `ModeSelectorCard` Right Chevron Overlap (`"VS BARANGAY BOTS · CHALLENGE"`) While Preserving `<= 0.35` Zone Budget**:
   - Observed in `wildrift_main_screen.png` bottom-right (`x = 1235..1252, y = 582..602`): the dropdown chevron `⌄` overlaps the final **`E`** of `"CHALLENGE"`.
   - **Fix**: Keep `font_size >= UiMetrics.MIN_TEXT` (`22px`) and `total_zone_area <= 0.3500` intact by tightening `STAGE_SUB_FONT` horizontal width/spacing (`width = 81%`, `spacing_glyph = -1` in `kalyeah_theme.gd`) AND reserving a `30px` right margin before the chevron `⌄` (or shifting `⌄` `6px` right inside the `412px` card) so `"VS BARANGAY BOTS · CHALLENGE"` has `>= 18px` clean gap before `⌄`!
2. **Fill Far-Right 3D Plaza Horizon Gap (`x = 1045..1280` Behind Jeepney)**:
   - Observed in `wildrift_main_screen.png` right of the Jeepney: empty flat beige horizon.
   - **Fix**: Place an angled 2-storey Barangay house (`brgy_house_two_storey` or `brgy_tabi_cluster_b`) + `brgy_mango_tree` foliage behind and right of the Jeepney (`x > +4.8m` in stage space) + extend overhead *banderitas* across to its eave so the 3D Barangay streetscape fills `100%` of the widescreen backdrop!

#### 6. `@audit-10` (`capture_checkpoint5_impl.gd` Studio Framing Polish)
1. **Shot 6 (`valorant_10kids_lineup.png`)**: Checkerboard-stagger the 5 back-row `X` positions into the midpoints (`+0.5 * x_step`) between the 5 front-row heroes so `buknoy` and `inday` are `100%` unobstructed head-to-toe (`0` overlap behind `toni`/`ligaya`!), and widen camera distance `+12%` so `junjun` (far left) and `nene` (far right) have `>= 48px` border margin.
2. **Shot 7 (`junjun_kiko_closeup.png`)**: Pull studio camera back `+10%` (`z + 0.38m`, `target_y = 0.86m`) so top hair spikes and bottom toes have `>= 28px` clean margin inside `1280×720`.
