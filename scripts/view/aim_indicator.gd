class_name AimIndicator
extends MeshInstance3D
## What the ground shows while a weapon button is held, kept as light as it can be so the street
## stays readable: one thin line for the shape the weapon will cover (cone, landing circle, lane,
## wall) only while you drag it by hand, a thin ring under every kid the cast would reach right now
## (a tap that auto-aims shows only these rings), pale grey when releasing would cast nothing (no
## cooldown spent), red over Cancel. No fills, no range circle, no pulse. A landing circle stands
## alone: no line runs from the player to it. Uses the sim's own aim helpers so the preview matches
## the real cast.

const HEIGHT: float = 0.06
const CIRCLE_POINTS: int = 40
## How wide the line is on the ground (metres).
const LINE_WIDTH: float = 0.07
const LINE_COLOR: Color = Color(1.0, 1.0, 1.0, 0.8)
const CANCEL_COLOR: Color = Color(1.0, 0.35, 0.3, 0.9)
## Releasing would cast nothing: the preview goes pale and quiet.
const NO_CAST_COLOR: Color = Color(0.8, 0.82, 0.88, 0.4)
## The ring under a kid the cast would reach: red for an enemy, green for a heal's patient.
const REACH_ENEMY: Color = Color(1.0, 0.3, 0.25, 0.9)
const REACH_FRIEND: Color = Color(0.5, 1.0, 0.5, 0.9)
const REACH_PAD: float = 0.25
## How thick the wall of a paper shield / block of earth is drawn on the ground.
const SHIELD_THICKNESS: float = 0.4

var _mesh: ImmediateMesh = ImmediateMesh.new()
var _material: StandardMaterial3D = StandardMaterial3D.new()


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
		WeaponDef.Shape.BOOMERANG, WeaponDef.Shape.BOUNCER, WeaponDef.Shape.SPINNER:
			var direction: Vector2 = sim.weapons.aim_direction(caster, aim)
			var half_width: float = maxf(def.projectile_radius, def.radius if def.shape == WeaponDef.Shape.SPINNER else 0.0)
			var side: Vector2 = direction.orthogonal() * maxf(half_width, 0.15)
			var end: Vector2 = origin + direction * def.max_range
			lines.append(PackedVector2Array([origin + side, end + side, end - side, origin - side, origin + side]))
			if def.shape == WeaponDef.Shape.BOUNCER:
				for i: int in def.count:
					var at: Vector2 = origin + direction * def.max_range * float(i + 1) / float(maxi(def.count, 1))
					lines.append(circle(at, def.radius))
		WeaponDef.Shape.HEAL:
			var patient: PlayerState = sim.weapons.heal_target(sim, caster, def, aim)
			if patient != null:
				lines.append(circle(patient.position, patient.radius + 0.35))
		WeaponDef.Shape.SHIELD:
			var facing: Vector2 = sim.weapons.aim_direction(caster, aim)
			var center: Vector2 = origin + facing * def.offset
			lines.append(lane(center - facing * SHIELD_THICKNESS / 2.0, facing, SHIELD_THICKNESS, def.width / 2.0))
	return lines


## A closed rectangle from `origin` along `direction` for `length`, `half_width` either side.
static func lane(origin: Vector2, direction: Vector2, length: float, half_width: float) -> PackedVector2Array:
	var along: Vector2 = direction.normalized()
	var side: Vector2 = along.orthogonal() * half_width
	var end: Vector2 = origin + along * length
	return PackedVector2Array([origin + side, end + side, end - side, origin - side, origin + side])


static func circle(center: Vector2, radius: float) -> PackedVector2Array:
	var points: PackedVector2Array = PackedVector2Array()
	for i: int in CIRCLE_POINTS + 1:
		points.append(center + Vector2.from_angle(TAU * float(i) / float(CIRCLE_POINTS)) * radius)
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
## TRIANGLES surface of `mesh`.
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
	if manual:
		shape = outline(sim, caster, def, aim, target_id)
		if def.max_range > 0.0 and not shape.is_empty():
			shape.remove_at(0)  # the range circle: the shape already shows how far it goes
		if def.shape == WeaponDef.Shape.TARGETED:
			shape.clear()  # the ring under the target says it all
	var pips: Array[PackedVector2Array] = snip_pips(caster.position, sim.weapons.aim_direction(caster, aim), def)
	show_lines(shape, color_for(castable, cancelled), rings, ring_colors, pips, def.snip_count(hold_seconds))


## Draws the shape lines, the rings under the kids reached and the snip pips (the first `pips_lit` are bright).
func show_lines(lines: Array[PackedVector2Array], color: Color, rings: Array[PackedVector2Array] = [],
		ring_colors: Array[Color] = [], pips: Array[PackedVector2Array] = [], pips_lit: int = 0) -> void:
	_mesh.clear_surfaces()
	if lines.is_empty() and rings.is_empty() and pips.is_empty():
		return
	_mesh.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
	for line: PackedVector2Array in lines:
		ribbon(_mesh, line, LINE_WIDTH, color)
	for index: int in pips.size():
		ribbon(_mesh, pips[index], LINE_WIDTH, color if index < pips_lit else Color(color, color.a * 0.35))
	for index: int in rings.size():
		ribbon(_mesh, rings[index], LINE_WIDTH, ring_colors[index])
	_mesh.surface_end()
