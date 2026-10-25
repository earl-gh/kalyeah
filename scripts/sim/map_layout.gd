class_name MapLayout
extends Resource
## Geometry of the playground street, in meters. Pure data + math, no nodes.
## The street runs along Z: +Z is the "own" end (bottom of the portrait screen),
## -Z the enemy end. A side's post stands in its left corner as seen from that
## end (own: bottom-left, enemy: top-right); the street's four corners are rounded like the bases' (`base_corner`). Its tagged players wait on the sidewalk outside the street's
## side edge at the far end, on the same left side (own: top-left, enemy: bottom-right), facing in, and the
## rescue area that frees them lies against the street's edge beside them. The map is point-symmetric, so
## both teams play the same street.
## All numbers come from data/rules/map_layout.tres.

const SIDE_OWN: int = 1
const SIDE_ENEMY: int = -1

## The street is a grid of square tiles, like Clash Royale's arena: 18 tiles across and 32 tiles long (9:16, the
## shape of a portrait phone), each tile 1.5 m. Tile (0, 0) is the top-left corner as the own team sees it (the
## far left of the enemy end); x runs right, z runs down toward the own end.
@export var tiles: Vector2i = Vector2i(18, 32)
@export var tile_size: float = 1.5
## Playable street (width along X, length along Z), from the grid.
var lane_width: float:
	get:
		return float(tiles.x) * tile_size
var lane_length: float:
	get:
		return float(tiles.y) * tile_size
## The base is a rounded rectangle: a square `base_radius` from a point `base_inset` in from both edges of its corner, cut off by the
## street's edges (so it sits wholly inside the street), with all four corners rounded by `base_corner`. The street's own four corners
## are rounded by the same `base_corner`, so a base fits its corner exactly, and the post stands at the centre of that curve (`post_position`).
@export var base_inset: float = 0.0
@export var base_radius: float = 0.0
@export var base_corner: float = 0.0
## Where a side's players start and come back to after a rescue: this far from the
## post toward the middle of the street (along each axis).
@export var spawn_offset: float = 0.0
## The rescue area: one rectangle (x across, z along the street) against the street's side edge. A free teammate standing in it frees
## the tagged players waiting on the sidewalk beside it. Along the street its outer end is level with the inner line of the other
## team's base on the same end (see `rescue_rect`).
@export var rescue_size: Vector2 = Vector2.ZERO
## How many tagged players stand in a row on the sidewalk, level with the rescue area (more than that share the first places).
@export var tagged_places: int = 1
## Cover on the own half of the street (x, z rects); the enemy half gets the same
## pieces turned around the center. They block walking and throws for both teams.
@export var obstacles: Array[Rect2] = []
## Cosmetic: what each obstacle is drawn as (bench, tent).
@export var obstacle_props: Array[StringName] = []
@export var boundary_thickness: float = 0.0
## Cosmetic, how big the scenery is drawn next to the kids (the play area, hitboxes and every rule are untouched): the houses, shops,
## vehicles, props and pets are this many times their real size (a kid is drawn about 1.5x its model's 1.85 m, so a real house of about
## 6 m would look no taller than two kids). Cover and the electric posts have their own heights below.
@export var scenery_scale: float = 1.0
## How tall the cardboard cover walls are drawn (m), and the electric posts.
@export var cover_height: float = 1.15
@export var post_height: float = 8.5
@export var overview_height: float = 0.0
@export var overview_size: float = 0.0


## Centre of the tile `tile` (see `tiles`) in world metres (x, z).
func tile_center(tile: Vector2i) -> Vector2:
	return Vector2((float(tile.x) + 0.5 - float(tiles.x) / 2.0) * tile_size, (float(tile.y) + 0.5 - float(tiles.y) / 2.0) * tile_size)


## The tile a world point (x, z) is in (it may be outside the street).
func tile_at(position: Vector2) -> Vector2i:
	return Vector2i(floori(position.x / tile_size + float(tiles.x) / 2.0), floori(position.y / tile_size + float(tiles.y) / 2.0))


## A world point (x, z) in tiles, measured from the top-left corner: (0.5, 0.5) is the middle of tile (0, 0).
func to_tiles(position: Vector2) -> Vector2:
	return Vector2(position.x / tile_size + float(tiles.x) / 2.0, position.y / tile_size + float(tiles.y) / 2.0)


## The point the base's square is built round (x, z): own bottom-left, enemy top-right.
func base_center(side: int) -> Vector2:
	return Vector2(-side * (lane_width / 2.0 - base_inset), side * (lane_length / 2.0 - base_inset))


## Where a side's post stands (x, z): in its corner, at the centre of the street's rounded corner (`base_corner` in from both edges).
func post_position(side: int) -> Vector2:
	return Vector2(-side * (lane_width / 2.0 - base_corner), side * (lane_length / 2.0 - base_corner))


## The base of a side as a rectangle (the other team captures there): the square round its post, cut off by the street.
func base_rect(side: int) -> Rect2:
	var reach: Vector2 = Vector2.ONE * base_radius
	return Rect2(base_center(side) - reach, reach * 2.0).intersection(lane_rect())


## True when `position` is inside `side`'s base (its corners are rounded).
func in_base(side: int, position: Vector2) -> bool:
	return rounded_has_point(base_rect(side), base_corner, position)


## True when `position` is inside `rect` with its four corners rounded to `radius`.
static func rounded_has_point(rect: Rect2, radius: float, position: Vector2) -> bool:
	var half: Vector2 = rect.size / 2.0
	var apart: Vector2 = (position - rect.get_center()).abs()
	var straight: Vector2 = half - Vector2.ONE * radius
	return Vector2(maxf(apart.x - straight.x, 0.0), maxf(apart.y - straight.y, 0.0)).length() <= radius


## Where a side's players start, and where a rescued player is sent back to.
func spawn_center(side: int) -> Vector2:
	return base_center(side) + Vector2(side, -side) * spawn_offset


## The rescue area of a side: against the street's left edge for own, the right for enemy, at the far end. Along the street its outer
## end is level with the inner line of the other team's base on the same end (the top end of yours with the bottom line of the base
## at the top right; the bottom end of theirs with the top line of the base at the bottom left).
func rescue_rect(side: int) -> Rect2:
	var other_base: Rect2 = base_rect(-side)
	var edge: float = other_base.end.y if side == SIDE_OWN else other_base.position.y
	var top: float = edge if side == SIDE_OWN else edge - rescue_size.y
	var left: float = -lane_width / 2.0 if side == SIDE_OWN else lane_width / 2.0 - rescue_size.x
	return Rect2(left, top, rescue_size.x, rescue_size.y)


## Where the `index`-th tagged player of a side stands: on the sidewalk just outside the street's side edge (where the invisible wall
## is), level with the rescue area and spread evenly along it.
func tagged_place(side: int, index: int) -> Vector2:
	var area: Rect2 = rescue_rect(side)
	var row: int = index % tagged_places
	return Vector2(-side * (lane_width / 2.0 + boundary_thickness), area.position.y + area.size.y * (float(row) + 0.5) / float(tagged_places))


## The way a side's tagged players face: in, toward the street.
func tagged_facing(side: int) -> Vector2:
	return Vector2(side, 0.0)


func lane_rect() -> Rect2:
	return Rect2(-lane_width / 2.0, -lane_length / 2.0, lane_width, lane_length)


## `point` pulled to the nearest place where a circle of `radius` fits inside the street, whose four corners are rounded by `base_corner`
## (a kid pushed into a corner slides along the curve).
func keep_in_street(point: Vector2, radius: float) -> Vector2:
	var inner: Rect2 = lane_rect().grow(-radius)
	var curve: float = maxf(base_corner - radius, 0.0)
	var inside: Vector2 = point.clamp(inner.position, inner.end)
	var straight: Vector2 = inner.size / 2.0 - Vector2.ONE * curve
	var from_centre: Vector2 = inside - inner.get_center()
	# the curve's centre nearest the point; past `curve` from it the point is outside the rounded corner
	var arc_centre: Vector2 = from_centre.clamp(-straight, straight)
	var out: Vector2 = from_centre - arc_centre
	if out.length() <= curve:
		return inside
	return inner.get_center() + arc_centre + out.normalized() * curve


## Every obstacle on the street: the own-half pieces plus the same pieces turned
## around the center for the enemy half. Index i (own) pairs with i + count (enemy).
func obstacle_rects() -> Array[Rect2]:
	var result: Array[Rect2] = []
	for rect: Rect2 in obstacles:
		result.append(rect)
	for rect: Rect2 in obstacles:
		result.append(Rect2(-rect.end, rect.size))
	return result


## The prop an obstacle is drawn as (same order as obstacle_rects()).
func obstacle_prop(index: int) -> StringName:
	if obstacle_props.is_empty():
		return &"bench"
	return obstacle_props[index % obstacles.size() % obstacle_props.size()]


## Left, right and the two end walls enclosing the street.
func boundary_rects() -> Array[Rect2]:
	var t: float = boundary_thickness
	var result: Array[Rect2] = []
	result.append(Rect2(-lane_width / 2.0 - t, -lane_length / 2.0, t, lane_length))
	result.append(Rect2(lane_width / 2.0, -lane_length / 2.0, t, lane_length))
	result.append(Rect2(-lane_width / 2.0 - t, -lane_length / 2.0 - t, lane_width + 2.0 * t, t))
	result.append(Rect2(-lane_width / 2.0 - t, lane_length / 2.0, lane_width + 2.0 * t, t))
	return result
