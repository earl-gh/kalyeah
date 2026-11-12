class_name MapLayout
extends Resource
## Geometry of the playground barangay, in meters. Pure data + math, no nodes.
## When `arena_radius > 0.0`, the playable neighborhood is a circular 90s provincial
## barangay (`position.length() <= arena_radius`) with symmetric team bases at `(0, +-64)`,
## 3 organic S-curved North-South pathways, 2 meandering East-West eskinita alleyways,
## 16+16 true-scale rotated house/shophouse parcels, and a shallow walkable *Tabing-Ilog*
## meandering creek (NO mid bridge). When `arena_radius <= 0.0`, legacy rectangular street
## bounds (`lane_rect()` with `base_corner` corners) remain active.
## All numbers come from data/rules/map_layout.tres.

const SIDE_OWN: int = 1
const SIDE_ENEMY: int = -1

## Grid of square tiles defining the outer bounding span (`lane_width` x `lane_length`).
@export var tiles: Vector2i = Vector2i(16, 32)
@export var tile_size: float = 1.5
## Circular playable barangay radius in meters (`0.0` keeps legacy rectangular bounds).
@export var arena_radius: float = 0.0
## Playable bounding span (width along X, length along Z), from the grid.
var lane_width: float:
	get:
		return float(tiles.x) * tile_size
var lane_length: float:
	get:
		return float(tiles.y) * tile_size
## Base placement inset (`arena_radius - base_inset` along Z in circular mode; corner inset in rectangular mode).
@export var base_inset: float = 0.0
@export var base_radius: float = 0.0
@export var base_corner: float = 0.0
## Where a side's players start and return after rescue relative to the base.
@export var spawn_offset: float = 0.0
## Rescue zone dimensions (width along X, length along Z).
@export var rescue_size: Vector2 = Vector2.ZERO
## Number of tagged player slots lined up on the sidewalk beside each rescue zone.
@export var tagged_places: int = 1
## World (x, z) positions of 4 dry bridgehead Merienda power-up stands overlooking the crossings.
@export var powerup_spots: Array[Vector2] = [
	Vector2(-31.5, 4.8),
	Vector2(5.8, 6.4),
	Vector2(-5.8, -6.4),
	Vector2(31.5, -4.8),
]
## Tight inscribed solid building / stall collision footprints on the +Z half,
## point-symmetrically mirrored to -Z (`obstacle_rects().size() == obstacles.size() * 2`).
@export var obstacles: Array[Rect2] = []
## Street-aligned rotation in degrees for each parcel in `obstacles` (`0` = local `+Z` Front faces
## South `+Z`, `90` = East `+X`, `-90` = West `-X`, `180` = North `-Z`).
@export var obstacle_yaws_deg: PackedFloat32Array = PackedFloat32Array()
## Uniform true-scale factor (`~0.95..1.03`) for each visual building in `obstacles`.
@export var obstacle_scales: PackedFloat32Array = PackedFloat32Array()
## Cosmetic kit prop id for each entry in `obstacles`.
@export var obstacle_props: Array[StringName] = []
## Optional non-walkable creek / channel rects on the +Z/West half (point-symmetrically mirrored).
## Empty in the walkable shallow mid-canal barangay; when populated in custom layouts, blocks walking while projectiles fly over.
@export var water_obstacles: Array[Rect2] = []
@export var boundary_thickness: float = 0.0
## Cosmetic scale multiplier for houses, shophouses, vehicles, and street props.
@export var scenery_scale: float = 1.0
## Height of low cover barriers and electric posts in meters.
@export var cover_height: float = 1.15
@export var post_height: float = 8.5
@export var post_width_share: float = 1.0
@export var overview_height: float = 0.0
@export var overview_size: float = 0.0


static func smooth_band(edge0: float, edge1: float, val: float) -> float:
	var span: float = maxf(edge1 - edge0, 0.0001)
	var t: float = clampf((val - edge0) / span, 0.0, 1.0)
	return t * t * (3.0 - 2.0 * t)


## Point-symmetric meandering *Tabing-Ilog* creek centerline `Z(x)` (`canal_z(-x) == -canal_z(x)`).
static func canal_z(x: float) -> float:
	return 2.4 * sin(x * 0.046) + 0.8 * sin(x * 0.105)


## Center Barangay Avenue S-bend centerline `X(z)` (`lane_center_x(-z) == -lane_center_x(z)`).
static func lane_center_x(z: float) -> float:
	var taper: float = 1.0 - smooth_band(50.0, 62.0, absf(z))
	return 4.2 * sin(z * 0.048) * taper


## East *TODA / Talipapa* Feeder Road curved centerline `X(z)`.
static func lane_east_x(z: float) -> float:
	var taper: float = 1.0 - smooth_band(56.0, 74.0, absf(z))
	return 34.5 + (3.8 * sin(z * 0.052 + 0.55) - 0.055 * absf(z)) * taper


## West *Tabing-Ilog* Feeder Road curved centerline `X(z)` (`lane_west_x(-z) == -lane_east_x(z)`).
static func lane_west_x(z: float) -> float:
	return -lane_east_x(-z)


## South meandering *Eskinita* cross-street centerline `Z(x)`.
static func eskinita_south_z(x: float) -> float:
	return 28.0 + 3.0 * sin(x * 0.058)


## North meandering *Eskinita* cross-street centerline `Z(x)` (`eskinita_north_z(-x) == -eskinita_south_z(x)`).
static func eskinita_north_z(x: float) -> float:
	return -eskinita_south_z(-x)


## Variable asphalt carriageway half-width along Center Avenue (tight mid-blocks -> sunlit plazas).
static func lane_center_half_width(z: float) -> float:
	var az: float = absf(z)
	var dz_esk: float = (az - 28.0) / 8.5
	var dz_ford: float = z / 8.5
	var dz_plaza: float = (az - 62.0) / 10.0
	return 4.45 + 2.15 * exp(-dz_esk * dz_esk) + 1.85 * exp(-dz_ford * dz_ford) + 2.40 * exp(-dz_plaza * dz_plaza)


## Variable asphalt carriageway half-width along West & East Feeder Roads.
static func lane_side_half_width(z: float) -> float:
	var az: float = absf(z)
	var dz_esk: float = (az - 28.0) / 9.0
	var dz_ford: float = z / 8.5
	return 4.25 + 1.95 * exp(-dz_esk * dz_esk) + 1.65 * exp(-dz_ford * dz_ford)


## Variable width along North & South *Eskinita* cross-streets.
static func eskinita_half_width(x: float) -> float:
	var ax: float = absf(x)
	var dx_mid: float = ax / 9.0
	var dx_side: float = (ax - 34.5) / 8.5
	return 3.55 + 1.55 * exp(-dx_mid * dx_mid) + 1.45 * exp(-dx_side * dx_side)


## Centre of the tile `tile` in world metres (x, z).
func tile_center(tile: Vector2i) -> Vector2:
	return Vector2((float(tile.x) + 0.5 - float(tiles.x) / 2.0) * tile_size, (float(tile.y) + 0.5 - float(tiles.y) / 2.0) * tile_size)


## The tile a world point (x, z) is in (it may be outside the arena).
func tile_at(position: Vector2) -> Vector2i:
	return Vector2i(floori(position.x / tile_size + float(tiles.x) / 2.0), floori(position.y / tile_size + float(tiles.y) / 2.0))


## A world point (x, z) in tiles, measured from the top-left corner.
func to_tiles(position: Vector2) -> Vector2:
	return Vector2(position.x / tile_size + float(tiles.x) / 2.0, position.y / tile_size + float(tiles.y) / 2.0)


## True when this layout uses circular barangay bounds (`arena_radius > 0.0`).
func is_circular() -> bool:
	return arena_radius > 0.0


## True when `point` fits inside the playable arena with `pad` clearance from the outer wall.
func in_arena(point: Vector2, pad: float = 0.0) -> bool:
	if arena_radius > 0.0:
		var limit: float = maxf(arena_radius - pad, 0.0)
		return point.length_squared() <= limit * limit
	return rounded_has_point(lane_rect().grow(-pad), maxf(base_corner - pad, 0.0), point)


## Clamps `point` to the nearest spot at least `pad` inside the playable arena boundary.
func clamp_to_arena(point: Vector2, pad: float = 0.0) -> Vector2:
	if arena_radius > 0.0:
		var limit: float = maxf(arena_radius - pad, 0.0)
		var len_sq: float = point.length_squared()
		if len_sq > limit * limit and len_sq > 0.0:
			return point * (limit / sqrt(len_sq))
		return point
	return _keep_in_rect_street(point, pad)


## Centre of a side's base (`(0, +-64)` in the circular barangay; opposite corners in legacy rectangular mode).
func base_center(side: int) -> Vector2:
	if arena_radius > 0.0:
		return Vector2(0.0, float(side) * maxf(arena_radius - base_inset, 0.0))
	return Vector2(-side * (lane_width / 2.0 - base_inset), side * (lane_length / 2.0 - base_inset))


## Where a side's home post stands inside its base.
func post_position(side: int) -> Vector2:
	if arena_radius > 0.0:
		var z_center: float = maxf(arena_radius - base_inset, 0.0)
		var post_shift: float = minf(maxf(base_corner, 3.5), maxf(base_radius - 2.0, 0.0))
		return Vector2(0.0, float(side) * (z_center + post_shift))
	return Vector2(-side * (lane_width / 2.0 - base_corner), side * (lane_length / 2.0 - base_corner))


## The base of a side as a rectangle (the other team captures there).
func base_rect(side: int) -> Rect2:
	var reach: Vector2 = Vector2.ONE * base_radius
	return Rect2(base_center(side) - reach, reach * 2.0).intersection(lane_rect())


## True when `position` is inside `side`'s rounded-rectangle base.
func in_base(side: int, position: Vector2) -> bool:
	return rounded_has_point(base_rect(side), base_corner, position)


## True when `position` is inside `rect` with its four corners rounded to `radius`.
static func rounded_has_point(rect: Rect2, radius: float, position: Vector2) -> bool:
	var half: Vector2 = rect.size / 2.0
	var apart: Vector2 = (position - rect.get_center()).abs()
	var straight: Vector2 = half - Vector2.ONE * radius
	return Vector2(maxf(apart.x - straight.x, 0.0), maxf(apart.y - straight.y, 0.0)).length() <= radius


## Where a side's players start, and where a rescued player returns.
func spawn_center(side: int) -> Vector2:
	if arena_radius > 0.0:
		return base_center(side) - Vector2(0.0, float(side) * spawn_offset)
	return base_center(side) + Vector2(side, -side) * spawn_offset


## Point-symmetric rescue zone where a free teammate stands to free tagged allies.
func rescue_rect(side: int) -> Rect2:
	if arena_radius > 0.0:
		var z_mid: float = -float(side) * (arena_radius * 0.375)
		var z_far: float = absf(z_mid) + rescue_size.y * 0.5
		var inner_r: float = maxf(arena_radius - 2.2, rescue_size.x)
		var x_edge: float = sqrt(maxf(inner_r * inner_r - z_far * z_far, 0.0))
		var left_c: float = -x_edge if side == SIDE_OWN else x_edge - rescue_size.x
		return Rect2(left_c, z_mid - rescue_size.y * 0.5, rescue_size.x, rescue_size.y)
	var other_base: Rect2 = base_rect(-side)
	var edge: float = other_base.end.y if side == SIDE_OWN else other_base.position.y
	var top: float = edge if side == SIDE_OWN else edge - rescue_size.y
	var left: float = -lane_width / 2.0 if side == SIDE_OWN else lane_width / 2.0 - rescue_size.x
	return Rect2(left, top, rescue_size.x, rescue_size.y)


## Where the `index`-th tagged player of `side` waits on the perimeter sidewalk beside `rescue_rect(side)`.
func tagged_place(side: int, index: int) -> Vector2:
	var slots: int = maxi(tagged_places, 1)
	var area: Rect2 = rescue_rect(side)
	var row: int = posmod(index, slots)
	var y: float = area.position.y + area.size.y * (float(row) + 0.5) / float(slots)
	if arena_radius > 0.0:
		var curb_r: float = arena_radius + maxf(boundary_thickness, 1.0)
		var x_curb: float = -float(side) * sqrt(maxf(curb_r * curb_r - y * y, 0.0))
		return Vector2(x_curb, y)
	return Vector2(-side * (lane_width / 2.0 + boundary_thickness), y)


## Alias for `tagged_place(side, slot)`.
func tagged_spot(side: int, slot: int) -> Vector2:
	return tagged_place(side, slot)


## The way a side's tagged players face: inward toward the street.
func tagged_facing(side: int) -> Vector2:
	return Vector2(float(side), 0.0)


func lane_rect() -> Rect2:
	return Rect2(-lane_width / 2.0, -lane_length / 2.0, lane_width, lane_length)


## Pulls `point` inside the playable arena (circular `arena_radius` when set, else rounded street rectangle).
func keep_in_street(point: Vector2, radius: float) -> Vector2:
	if arena_radius > 0.0:
		return clamp_to_arena(point, radius)
	return _keep_in_rect_street(point, radius)


func _keep_in_rect_street(point: Vector2, radius: float) -> Vector2:
	var inner: Rect2 = lane_rect().grow(-radius)
	var curve: float = maxf(base_corner - radius, 0.0)
	var inside: Vector2 = point.clamp(inner.position, inner.end)
	var straight: Vector2 = inner.size / 2.0 - Vector2.ONE * curve
	var from_centre: Vector2 = inside - inner.get_center()
	var arc_centre: Vector2 = from_centre.clamp(-straight, straight)
	var out: Vector2 = from_centre - arc_centre
	if out.length() <= curve:
		return inside
	return inner.get_center() + arc_centre + out.normalized() * curve


func has_oriented_parcels() -> bool:
	return not obstacle_yaws_deg.is_empty() and obstacle_yaws_deg.size() == obstacles.size()


func parcel_count() -> int:
	return obstacles.size() * 2


func parcel_rect(index: int) -> Rect2:
	var half: int = obstacles.size()
	if half == 0:
		return Rect2()
	if index < half:
		return obstacles[index]
	var src: Rect2 = obstacles[index - half]
	return Rect2(-src.end, src.size)


func parcel_yaw_rad(index: int) -> float:
	var half: int = obstacles.size()
	if half == 0 or obstacle_yaws_deg.is_empty():
		return 0.0
	var base_idx: int = index % half
	var deg: float = obstacle_yaws_deg[base_idx] if base_idx < obstacle_yaws_deg.size() else 0.0
	var rad: float = deg_to_rad(deg)
	if index >= half:
		rad = wrapf(rad + PI, -PI, PI)
	return rad


func parcel_scale(index: int) -> float:
	var half: int = obstacles.size()
	if half == 0 or obstacle_scales.is_empty():
		return 1.0
	var base_idx: int = index % half
	return obstacle_scales[base_idx] if base_idx < obstacle_scales.size() else 1.0


func parcel_prop(index: int) -> StringName:
	return obstacle_prop(index)


## Every solid obstacle on the map: own-half pieces plus point-symmetric enemy-half mirrors.
func obstacle_rects() -> Array[Rect2]:
	var result: Array[Rect2] = []
	for rect: Rect2 in obstacles:
		result.append(rect)
	for rect: Rect2 in obstacles:
		result.append(Rect2(-rect.end, rect.size))
	return result


## Every Estero creek channel rect on the map (own half + point-symmetric mirror).
## Blocks walking, never projectiles.
func water_rects() -> Array[Rect2]:
	var result: Array[Rect2] = []
	for rect: Rect2 in water_obstacles:
		result.append(rect)
	for rect: Rect2 in water_obstacles:
		result.append(Rect2(-rect.end, rect.size))
	return result


## Every interior rect that blocks player movement (solid buildings/cover + Estero creek channels).
func walk_obstacle_rects() -> Array[Rect2]:
	var result: Array[Rect2] = obstacle_rects()
	result.append_array(water_rects())
	return result


## The kit prop id an obstacle is drawn as (same order as `obstacle_rects()`).
func obstacle_prop(index: int) -> StringName:
	if obstacle_props.is_empty() or obstacles.is_empty():
		return &"bench"
	return obstacle_props[posmod(index, obstacles.size()) % obstacle_props.size()]


## Outer enclosing wall rects around `lane_rect()`.
func boundary_rects() -> Array[Rect2]:
	var t: float = boundary_thickness
	var result: Array[Rect2] = []
	result.append(Rect2(-lane_width / 2.0 - t, -lane_length / 2.0, t, lane_length))
	result.append(Rect2(lane_width / 2.0, -lane_length / 2.0, t, lane_length))
	result.append(Rect2(-lane_width / 2.0 - t, -lane_length / 2.0 - t, lane_width + 2.0 * t, t))
	result.append(Rect2(-lane_width / 2.0 - t, lane_length / 2.0, lane_width + 2.0 * t, t))
	return result
