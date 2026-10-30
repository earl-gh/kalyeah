class_name AimIndicator
extends MeshInstance3D
## What the ground shows while a weapon button is held, kept as light as it can be so the street stays readable. One clean
## language, white: an AREA the weapon will cover (a landing circle, a trap, a field, each bounce of the marble, a wall) is a white
## tint inside a thin white outline; a LANE or a WEDGE (a thrown slipper's path, a snip's cone) is only the thin outline. Nothing joins
## the bounce rings (the marble's guide is just its rings) and no line runs from the player to a landing circle. A thin ring under every
## kid the cast would reach right now (a tap that auto-aims shows only these rings), pale grey when releasing would cast nothing (no
## cooldown spent), red over Cancel. No range circle, no pulse. Uses the sim's own aim helpers so the preview matches the real cast.
##
## A wall that changes form (the paper wall glides forward, the block of earth widens: `WallMotion`) shows BOTH forms (owner): where it
## is cast keeps the tint inside its outline, where it ends up (`second_form`, from the same functions and data as the sim) is the
## same thin outline with no tint.
##
## Cheap by construction: the whole guide is ONE triangle-strip surface (fills and outlines joined by degenerate triangles, so one
## draw call), a circle is 32 points shared by its two edges instead of six vertices a segment, and the circle points come from one
## table built once.

const HEIGHT: float = 0.06
const CIRCLE_POINTS: int = 32
## How wide the outline is on the ground (metres): thin, like the area marks the casts leave (FxKit.AREA_RIM).
const LINE_WIDTH: float = 0.07
## The ring under a kid the cast would reach is a little bolder: it has to read on top of a kid drawn twice life size.
const REACH_WIDTH: float = 0.1
const LINE_COLOR: Color = Color(1.0, 1.0, 1.0, 1.0)
const CANCEL_COLOR: Color = Color(1.0, 0.35, 0.3, 0.9)
## Releasing would cast nothing: the preview goes pale and quiet.
const NO_CAST_COLOR: Color = Color(0.8, 0.82, 0.88, 0.4)
## An area's tint is this share of its outline's alpha (the same white, the same red when cancelled, the same pale grey).
const FILL_SHARE: float = 0.35
## The ring under a kid the cast would reach: red for an enemy, green for a heal's patient.
const REACH_ENEMY: Color = Color(1.0, 0.3, 0.25, 0.9)
const REACH_FRIEND: Color = Color(0.5, 1.0, 0.5, 0.9)
const REACH_PAD: float = 0.25
## How thick the wall of a paper shield / block of earth is drawn on the ground.
const SHIELD_THICKNESS: float = 0.4
## A sharp corner of an outline sticks out at most this many half widths (a snip's 60 degree tip needs 2).
const MITER_LIMIT: float = 2.5

## The points of a circle of radius 1, closed (the last is the first), built once.
static var _unit: PackedVector2Array = PackedVector2Array()

var _mesh: ImmediateMesh = ImmediateMesh.new()
var _material: StandardMaterial3D = StandardMaterial3D.new()
## The last vertex written to the open strip (the next piece joins to it with degenerate triangles).
var _tail: Vector3 = Vector3.ZERO
var _tail_color: Color = Color.WHITE
var _has_tail: bool = false


## Polylines (world x/z as Vector2) for one aimed weapon. The first is the range
## circle. `aim` is the resolved aim and `target_id` the locked target (TARGETED),
## both from MatchSim, so the preview never drifts after a moving enemy.
static func outline(sim: MatchSim, caster: PlayerState, def: WeaponDef, aim: Vector2, target_id: int = -1) -> Array[PackedVector2Array]:
	var lines: Array[PackedVector2Array] = []
	var origin: Vector2 = caster.position
	if def.max_range > 0.0:
		lines.append(circle(origin, def.max_range))
	match def.shape:
		WeaponDef.Shape.TARGETED:
			var target: PlayerState = sim.players.get(target_id) as PlayerState
			if target != null and sim.is_targetable(target):
				lines.append(circle(target.position, target.radius + 0.2))
		WeaponDef.Shape.CONE:
			var direction: Vector2 = sim.weapons.aim_direction(caster, aim)
			var half: float = deg_to_rad(def.angle_degrees / 2.0)
			var wedge: PackedVector2Array = PackedVector2Array([origin])
			for i: int in CIRCLE_POINTS / 4 + 1:
				var angle: float = -half + 2.0 * half * float(i) / float(CIRCLE_POINTS / 4)
				wedge.append(origin + direction.rotated(angle) * def.max_range)
			wedge.append(origin)
			lines.append(wedge)
		WeaponDef.Shape.GROUND_AOE, WeaponDef.Shape.FIELD, WeaponDef.Shape.TRAP:
			var point: Vector2 = sim.weapons.aim_point(caster, def, aim)
			lines.append(circle(point, def.radius if def.radius > 0.0 else def.trigger_radius))
		WeaponDef.Shape.BOOMERANG, WeaponDef.Shape.SPINNER:
			var direction: Vector2 = sim.weapons.aim_direction(caster, aim)
			var half_width: float = maxf(def.projectile_radius, def.radius if def.shape == WeaponDef.Shape.SPINNER else 0.0)
			lines.append(lane(origin, direction, def.max_range, maxf(half_width, 0.15)))
		WeaponDef.Shape.BOUNCER:
			# only the rings the marble lands in: no lane between them
			var direction: Vector2 = sim.weapons.aim_direction(caster, aim)
			for i: int in def.count:
				lines.append(circle(origin + direction * def.max_range * float(i + 1) / float(maxi(def.count, 1)), def.radius))
		WeaponDef.Shape.HEAL:
			var patient: PlayerState = sim.weapons.heal_target(sim, caster, def, aim)
			if patient != null:
				lines.append(circle(patient.position, patient.radius + 0.35))
		WeaponDef.Shape.SHIELD:
			var facing: Vector2 = sim.weapons.aim_direction(caster, aim)
			lines.append(wall(origin + facing * def.offset, facing, def.width / 2.0))
	return lines


## The wall of a shield's line: `SHIELD_THICKNESS` deep along `facing`, `half_width` either side of `center`.
static func wall(center: Vector2, facing: Vector2, half_width: float) -> PackedVector2Array:
	return lane(center - facing * SHIELD_THICKNESS / 2.0, facing, SHIELD_THICKNESS, half_width)


## The second form of a wall that changes while it stands (empty for every other weapon): where the line is, and how wide, once
## its motion is over (`WallMotion` at the weapon's `duration`, the same functions the sim places the blocking line with). The
## paper wall's is `push_distance` ahead of where it is cast; the block of earth's is `expand_factor` times as wide.
static func second_form(sim: MatchSim, caster: PlayerState, def: WeaponDef, aim: Vector2) -> PackedVector2Array:
	if def.shape != WeaponDef.Shape.SHIELD:
		return PackedVector2Array()
	var pushed: float = WallMotion.push(def, def.duration)
	var widen: float = WallMotion.width_scale(def, def.duration)
	if pushed == 0.0 and widen == 1.0:
		return PackedVector2Array()
	var facing: Vector2 = sim.weapons.aim_direction(caster, aim)
	return wall(caster.position + facing * (def.offset + pushed), facing, def.width / 2.0 * widen)


## Whether the shape of `def` is an AREA (drawn with a white tint inside its outline) rather than a lane, a wedge or a ring round a
## kid (the outline alone).
static func is_area(def: WeaponDef) -> bool:
	return def.shape in [WeaponDef.Shape.GROUND_AOE, WeaponDef.Shape.FIELD, WeaponDef.Shape.TRAP, WeaponDef.Shape.BOUNCER, WeaponDef.Shape.SHIELD]


## A closed rectangle from `origin` along `direction` for `length`, `half_width` either side.
static func lane(origin: Vector2, direction: Vector2, length: float, half_width: float) -> PackedVector2Array:
	var along: Vector2 = direction.normalized()
	var side: Vector2 = along.orthogonal() * half_width
	var end: Vector2 = origin + along * length
	return PackedVector2Array([origin + side, end + side, end - side, origin - side, origin + side])


## A closed circle of `CIRCLE_POINTS` segments (`CIRCLE_POINTS + 1` points, the last is the first).
static func circle(center: Vector2, radius: float) -> PackedVector2Array:
	if _unit.is_empty():
		for i: int in CIRCLE_POINTS + 1:
			_unit.append(Vector2.from_angle(TAU * float(i % CIRCLE_POINTS) / float(CIRCLE_POINTS)))
	var points: PackedVector2Array = _unit.duplicate()
	for i: int in points.size():
		points[i] = center + points[i] * radius
	return points


## The charge pips of a cone that snips more the longer you hold (the little scissors): one short
## tick across the wedge per possible snip, drawn near the far edge.
static func snip_pips(origin: Vector2, direction: Vector2, def: WeaponDef) -> Array[PackedVector2Array]:
	var pips: Array[PackedVector2Array] = []
	if def.shape != WeaponDef.Shape.CONE or def.snips_max <= def.snips_min:
		return pips
	var spread: float = deg_to_rad(def.angle_degrees / 2.0) * 0.8
	for i: int in def.snips_max:
		var angle: float = -spread + 2.0 * spread * float(i) / float(maxi(def.snips_max - 1, 1))
		var ray: Vector2 = direction.rotated(angle)
		pips.append(PackedVector2Array([origin + ray * def.max_range * 0.62, origin + ray * def.max_range * 0.9]))
	return pips


## Thick line along `points` (metres wide, as flat triangles on the ground) added to a
## TRIANGLES surface of `mesh`. (The cached marks of the effects use this; the guide itself draws strips.)
static func ribbon(mesh: ImmediateMesh, points: PackedVector2Array, width: float, color: Color, height: float = HEIGHT) -> void:
	var half: float = width / 2.0
	for i: int in points.size() - 1:
		var a: Vector2 = points[i]
		var b: Vector2 = points[i + 1]
		if a.is_equal_approx(b):
			continue
		var side: Vector2 = (b - a).normalized().orthogonal() * half
		var corners: Array[Vector2] = [a + side, b + side, b - side, a + side, b - side, a - side]
		for corner: Vector2 in corners:
			mesh.surface_set_color(color)
			mesh.surface_add_vertex(Vector3(corner.x, height, corner.y))


## A filled convex polygon (a fan from its first point) added to a TRIANGLES surface of `mesh`.
static func fill(mesh: ImmediateMesh, polygon: PackedVector2Array, color: Color, height: float = HEIGHT) -> void:
	for i: int in range(1, polygon.size() - 2):
		for point: Vector2 in [polygon[0], polygon[i], polygon[i + 1]]:
			mesh.surface_set_color(color)
			mesh.surface_add_vertex(Vector3(point.x, height, point.y))


## The colour of the preview line.
static func color_for(castable: bool, cancelled: bool) -> Color:
	if cancelled:
		return CANCEL_COLOR
	return LINE_COLOR if castable else NO_CAST_COLOR


## The tint of an area whose outline is `color`.
static func tint_of(color: Color) -> Color:
	return Color(color, color.a * FILL_SHARE)


func _ready() -> void:
	mesh = _mesh
	_material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_material.vertex_color_use_as_albedo = true
	_material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_material.no_depth_test = true
	_material.cull_mode = BaseMaterial3D.CULL_DISABLED
	material_override = _material


func clear() -> void:
	_mesh.clear_surfaces()


## `manual`: the finger is dragging the aim (the shape is drawn); a tap that auto-aims shows only the
## rings under the kids it will reach.
func show_aim(sim: MatchSim, caster: PlayerState, def: WeaponDef, aim: Vector2, target_id: int, cancelled: bool,
		hold_seconds: float = 0.0, manual: bool = true) -> void:
	if not Settings.aim_guide:
		_mesh.clear_surfaces()
		return
	var castable: bool = sim.weapons.would_cast(sim, caster, def, aim, target_id)
	var rings: Array[PackedVector2Array] = []
	var ring_colors: Array[Color] = []
	for target: PlayerState in sim.weapons.would_reach(sim, caster, def, aim, target_id):
		rings.append(circle(target.position, target.radius + REACH_PAD))
		ring_colors.append(REACH_FRIEND if target.team == caster.team else REACH_ENEMY)
	var shape: Array[PackedVector2Array] = []
	var after: Array[PackedVector2Array] = []
	if manual:
		shape = outline(sim, caster, def, aim, target_id)
		if def.max_range > 0.0 and not shape.is_empty():
			shape.remove_at(0)  # the range circle: the shape already shows how far it goes
		if def.shape == WeaponDef.Shape.TARGETED:
			shape.clear()  # the ring under the target says it all
		var second: PackedVector2Array = second_form(sim, caster, def, aim)
		if not second.is_empty():
			after.append(second)
	var pips: Array[PackedVector2Array] = snip_pips(caster.position, sim.weapons.aim_direction(caster, aim), def)
	show_lines(shape, color_for(castable, cancelled), rings, ring_colors, pips, def.snip_count(hold_seconds), is_area(def), after)


## Draws the shape lines (each an area with its tint when `areas`), the second forms of a wall (`after`: the outline alone, never a
## tint), the rings under the kids reached and the snip pips (the first `pips_lit` are bright), all in one strip surface. Tints go
## first so no outline is ever covered.
func show_lines(lines: Array[PackedVector2Array], color: Color, rings: Array[PackedVector2Array] = [],
		ring_colors: Array[Color] = [], pips: Array[PackedVector2Array] = [], pips_lit: int = 0, areas: bool = false,
		after: Array[PackedVector2Array] = []) -> void:
	_mesh.clear_surfaces()
	if lines.is_empty() and rings.is_empty() and pips.is_empty() and after.is_empty():
		return
	_has_tail = false
	_mesh.surface_begin(Mesh.PRIMITIVE_TRIANGLE_STRIP)
	if areas:
		var tint: Color = tint_of(color)
		for line: PackedVector2Array in lines:
			_strip_fill(line, tint)
	for line: PackedVector2Array in lines:
		_strip_outline(line, LINE_WIDTH, color)
	for line: PackedVector2Array in after:
		_strip_outline(line, LINE_WIDTH, color)
	for index: int in pips.size():
		_strip_outline(pips[index], LINE_WIDTH, color if index < pips_lit else Color(color, color.a * 0.35))
	for index: int in rings.size():
		_strip_outline(rings[index], REACH_WIDTH, ring_colors[index])
	_mesh.surface_end()


## One vertex of the open strip.
func _put(at: Vector3, color: Color) -> void:
	_mesh.surface_set_color(color)
	_mesh.surface_add_vertex(at)
	_tail = at
	_tail_color = color
	_has_tail = true


## Starts a new piece at `first`: two degenerate triangles join it to the strip so far (the caller then writes `first` itself).
func _join(first: Vector3, color: Color) -> void:
	if _has_tail:
		_put(_tail, _tail_color)
		_put(first, color)


## A convex polygon as one strip (its points in the order 0, last, 1, last - 1, 2 ...: no vertex is written twice).
func _strip_fill(polygon: PackedVector2Array, color: Color) -> void:
	var count: int = polygon.size()
	if count > 1 and polygon[count - 1].is_equal_approx(polygon[0]):
		count -= 1
	if count < 3:
		return
	var low: int = 1
	var high: int = count - 1
	var first: Vector3 = Vector3(polygon[0].x, HEIGHT, polygon[0].y)
	_join(first, color)
	_put(first, color)
	var from_high: bool = true
	while low <= high:
		var point: Vector2 = polygon[high] if from_high else polygon[low]
		if from_high:
			high -= 1
		else:
			low += 1
		from_high = not from_high
		_put(Vector3(point.x, HEIGHT, point.y), color)


## A line `width` metres wide along `points` as one strip: two vertices a point, set apart along the corner's bisector so the line
## keeps its width round the bends (a closed outline, whose last point is its first, joins up).
func _strip_outline(points: PackedVector2Array, width: float, color: Color) -> void:
	var count: int = points.size()
	if count < 2:
		return
	var closed: bool = count > 2 and points[0].is_equal_approx(points[count - 1])
	var half: float = width / 2.0
	for i: int in count:
		var back: Vector2 = Vector2.ZERO
		var ahead: Vector2 = Vector2.ZERO
		if i > 0:
			back = points[i] - points[i - 1]
		elif closed:
			back = points[count - 1] - points[count - 2]
		if i < count - 1:
			ahead = points[i + 1] - points[i]
		elif closed:
			ahead = points[1] - points[0]
		var offset: Vector2 = _offset(back, ahead, half)
		var left: Vector3 = Vector3(points[i].x + offset.x, HEIGHT, points[i].y + offset.y)
		var right: Vector3 = Vector3(points[i].x - offset.x, HEIGHT, points[i].y - offset.y)
		if i == 0:
			_join(left, color)
		_put(left, color)
		_put(right, color)


## How far a line of half width `half` sits to the side of a point where the line comes in along `back` and leaves along `ahead`
## (either may be zero at an open end): the bisector of the two sides, lengthened to keep the width (`MITER_LIMIT` at most).
static func _offset(back: Vector2, ahead: Vector2, half: float) -> Vector2:
	var has_back: bool = back.length_squared() > 0.000001
	var has_ahead: bool = ahead.length_squared() > 0.000001
	if not has_back and not has_ahead:
		return Vector2.ZERO
	if not has_back:
		return ahead.normalized().orthogonal() * half
	if not has_ahead:
		return back.normalized().orthogonal() * half
	var side_back: Vector2 = back.normalized().orthogonal()
	var side_ahead: Vector2 = ahead.normalized().orthogonal()
	var bisector: Vector2 = side_back + side_ahead
	if bisector.length_squared() < 0.000001:
		return side_ahead * half
	bisector = bisector.normalized()
	return bisector * (half / maxf(bisector.dot(side_ahead), 1.0 / MITER_LIMIT))
