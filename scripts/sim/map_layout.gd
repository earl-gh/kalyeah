class_name MapLayout
extends Resource
## Geometry of the playground street, in meters. Pure data + math, no nodes.
## The street runs along Z: +Z is the "own" end (bottom of the portrait screen),
## -Z the enemy end. A side's post stands in its left corner as seen from that
## end (own: bottom-left, enemy: top-right), and the box its tagged players are
## sent to sits at the far end on the same left side (own: top-left, enemy:
## bottom-right). The map is point-symmetric, so both teams play the same street.
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
## The post stands this far in from both edges of its corner.
@export var post_inset: float = 0.0
## The base is a chalk circle of this radius around the post (clipped by the street).
@export var base_radius: float = 0.0
## Where a side's players start and come back to after a rescue: this far from the
## post toward the middle of the street (along each axis).
@export var spawn_offset: float = 0.0
## The tagged box (a taped rectangle on the road) and its center's inset from its corner of
## the play area; half the size puts the box flush in the corner (no margin).
## It is just big enough for its slots: columns across, rows along the street.
@export var jail_size: Vector2 = Vector2.ZERO
@export var jail_inset: Vector2 = Vector2.ZERO
@export var jail_columns: int = 1
@export var jail_rows: int = 1
## The rescue area round the tagged box: it reaches this fraction of the box size past the
## box on its two sides that face the street (0.5 = the box and its area together are 1.5x).
@export var rescue_scale: float = 0.0
## Cover on the own half of the street (x, z rects); the enemy half gets the same
## pieces turned around the center. They block walking and throws for both teams.
@export var obstacles: Array[Rect2] = []
## Cosmetic: what each obstacle is drawn as (bench, cardboard_wall, school_zone, tent).
@export var obstacle_props: Array[StringName] = []
@export var boundary_thickness: float = 0.0
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


## Center of a side's post in the XZ plane (x, z): own bottom-left, enemy top-right.
func base_center(side: int) -> Vector2:
	return Vector2(-side * (lane_width / 2.0 - post_inset), side * (lane_length / 2.0 - post_inset))


## True when `position` is inside `side`'s base circle (the other team captures there).
func in_base(side: int, position: Vector2) -> bool:
	return position.distance_to(base_center(side)) <= base_radius


## Where a side's players start, and where a rescued player is sent back to.
func spawn_center(side: int) -> Vector2:
	return base_center(side) + Vector2(side, -side) * spawn_offset


## The box a side's tagged players are sent to: own top-left, enemy bottom-right.
func jail_rect(side: int) -> Rect2:
	var center: Vector2 = Vector2(
		-side * (lane_width / 2.0 - jail_inset.x), -side * (lane_length / 2.0 - jail_inset.y)
	)
	return Rect2(center - jail_size / 2.0, jail_size)


## The rescue area of a side's box: the box grown by `rescue_scale` of its size on the two
## sides that face the street (yours, top left: bottom and right; theirs: top and left). A
## free teammate stepping into it frees the tagged kids.
func rescue_rect(side: int) -> Rect2:
	var box: Rect2 = jail_rect(side)
	var extra: Vector2 = jail_size * rescue_scale
	if side == SIDE_OWN:
		return Rect2(box.position, box.size + extra)
	return Rect2(box.position - extra, box.size + extra)


## The rescue area without the box: the two strips round it (a tinted L).
func rescue_strips(side: int) -> Array[Rect2]:
	var box: Rect2 = jail_rect(side)
	var area: Rect2 = rescue_rect(side)
	if side == SIDE_OWN:
		return [
			Rect2(box.end.x, area.position.y, area.end.x - box.end.x, area.size.y),
			Rect2(area.position.x, box.end.y, box.size.x, area.end.y - box.end.y),
		]
	return [
		Rect2(area.position.x, area.position.y, box.position.x - area.position.x, area.size.y),
		Rect2(box.position.x, area.position.y, box.size.x, box.position.y - area.position.y),
	]


## Where the `index`-th tagged player of a side stands in its box (a grid of
## `jail_columns` x `jail_rows` kids; more than that share the first slots).
func jail_slot(side: int, index: int) -> Vector2:
	var box: Rect2 = jail_rect(side)
	var column: int = index % jail_columns
	var row: int = (index / jail_columns) % jail_rows
	return box.position + Vector2(
		box.size.x * (column + 0.5) / jail_columns, box.size.y * (row + 0.5) / jail_rows
	)


func lane_rect() -> Rect2:
	return Rect2(-lane_width / 2.0, -lane_length / 2.0, lane_width, lane_length)


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
		return &"cardboard_wall"
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
