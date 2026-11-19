# Kalyeah maps (layouts)

Where things sit on the 4 launch maps. Rules for every map are in `docs/SPEC.md` §6 (halves, routes, terrain, hiding,
climb spots, neutral roles and numbers). Bases and jails are the owner's picks; neutral and climb-spot placement is the
agents' design (spec §6), changed freely after bot play-tests. Sizes and exact positions go into `unity/Data/*.json` in
each map's blockout.

## Rules for every layout
- **Scale:** about 20 s of running (about 120 m) from base to enemy base on the main route.
- **Bases and jails:** a base is about 8 m wide and fits a whole team with some cover inside; a jail is a small fenced
  spot about 4 m wide, about 15 m from its own base, off to the side, so defenders can't guard both at once.
- **Jails sit beside their base, off to the side,** so a rescue is a flank, not a straight run (like Valorant's offset
  spawns).
- **Fair side swap:** the walk from each base to the enemy jail is the same length on both halves (within a few steps,
  checked with bots), and each half has one open route, one tight route and one mixed route.
- **Neutrals:** the 2 vendors are equally far from both bases; the dragon and baron sit where the map's plan wants a fight.
- **Climb spots:** 3 per map: one per half and one mid-map.

## Barangay (closed beta map)
**Uptown** (cement: barangay hall, plaza, basketball court; open, long lines) vs **Riverside** (market and chapel,
sari-sari row; tight streets). The centre is soil and a provincial river with homes of scrap wood and tin.
- **Uptown:** base on the barangay hall steps; jail in the covered court beside the hall.
- **Riverside:** base in the chapel yard; jail in the back lot of the sari-sari row.
- **Routes:** main path over the river bridge (mixed); left alley through the riverside homes (tight); creek bank (open);
  side connectors.
- **Crocodile (dragon):** a sandbar under the main bridge, mid-map: both teams fight over it.
- **Philippine Eagle (baron):** perched on the water tower on the creek-bank flank, fought from below; taking it means
  leaving the main fight.
- **Vendors:** taho on the left alley and fishball on the creek bank, both just short of the river.
- **Selecta bike:** rides a loop on the river road between the bridge and both flanks.
- **Climb spots:** basketball backboard stand (Uptown), sari-sari store roof (Riverside), parked jeepney top (by the
  bridge).

## Palengke
**Wet section** (fish and meat stalls, slippery floors, tight aisles) vs **Dry section** (dry goods, ukay-ukay racks as soft
cover). Plays close, with ambushes, like Valorant's Bind.
- **Wet:** base at the fish counter; jail in the ice room beside it.
- **Dry:** base at the ukay-ukay stall; jail in the rice storeroom beside it.
- **Routes:** main aisle (mixed); loading-bay road (open); rooftop catwalk (tight, climb up).
- **Giant bangus (dragon):** the big fish tank at the centre crossing of the main aisle.
- **Boss Aspin (baron):** sleeps by the loading-bay gate, on the open flank.
- **Vendors:** buko juice at the catwalk ladder, banana cue by the loading bay.
- **Escaped lechon pig:** runs between the aisles through the middle of the market.
- **Climb spots:** fish-stall awning (Wet), stacked rice sacks (Dry), catwalk ladder at the centre.

## Pier
**Cargo yard** (stacked containers, crane; long lanes, high ground) vs **Ferry terminal** (fishing boats, piers over water,
waiting shed). Long sightlines; Strikers with range and Seekers shine.
- **Cargo:** base under the crane; jail in an open container next to it.
- **Ferry:** base at the waiting shed; jail on a moored boat by the dock.
- **Routes:** dock road (open); container maze (tight); boardwalk over the water (mixed; the sea is a wall).
- **Pawikan (dragon):** a sandy cove at the end of the boardwalk, mid-map.
- **Butanding (baron):** surfaces at the far pier on the dock-road flank.
- **Vendors:** balut at the container maze's mouth, inihaw on the boardwalk.
- **Seagull flock:** lands in turn on the dock road, the containers and the boardwalk.
- **Climb spots:** container top (Cargo), boat cabin roof (Ferry), the fish-crate stack at the centre.

## Hacienda
**Rice terraces** (paddies, dikes, nipa hut; open, low cover, water slows) vs **Sugarcane mill** (tall cane that hides kids,
old mill, bahay na bato). Open fields with tall cover.
- **Terraces:** base at the nipa hut; jail in the carabao pen beside it.
- **Mill:** base in the mill yard; jail in the courtyard of the bahay na bato.
- **Routes:** dirt road (open); paddy dikes (mixed); through the cane (tight, hidden).
- **Carabao (dragon):** the mud wallow where the dirt road meets the dikes, mid-map.
- **Tamaraw (baron):** the edge of the cane on the far flank, so Seekers matter to take it safely.
- **Vendors:** halo-halo by the dirt road, kakanin at the cane's edge.
- **Native chicken:** wanders the dikes and the cane edge.
- **Climb spots:** irrigation water tank (Terraces), mill roof (Mill), hay cart at the centre.

## Look per map (owner, see also `docs/GRAPHICS.md`)
- **Barangay:** **Uptown** in cement and painted walls with muted blue-grey trims (kept clearly apart from team blue),
  cement ground, the hall's flag and court lines. **Riverside** in wood, tin and warm colours with potted plants, soil
  and plank ground; the riverside homes shown with warmth and dignity. Landmarks seen from anywhere: the chapel bell
  tower (Riverside) and the hall's water tower (the Eagle's perch on the creek-bank flank).
- **Palengke:** a big tin roof with skylights and sun shafts: bright patches under the skylights, softer shade elsewhere,
  never dark; the catwalk runs just under the roof.
- **Pier:** the sea is a clear blue edge that reads as "can't go there", with boats bobbing gently.
- **Hacienda:** cane in tall soft clumps with gaps and visible edges (not a solid wall); the terraces step down in clear
  levels with clean edges, so you always know where you can walk.
- **Callouts:** real painted signs in the world double as callout names ("Aling Nena's Sari-Sari", "Covered Court"),
  and the full map shows the names, like Valorant. Asset kind: 2D images (sign art).
