class_name AimIndicator
extends MeshInstance3D
## What the ground shows while a weapon button is held: a faint range circle, the shape the
## weapon will cover (cone, landing circle, lane, wall) as a filled, thick-rimmed area in the
## weapon's colour, a ring under every kid the cast would reach right now, and a grey "nothing to
## hit" look when releasing would cast nothing (no cooldown spent). A landing circle stands alone:
## no line runs from the player to it. Uses the sim's own aim helpers so the preview matches the
## real cast.

const HEIGHT: float = 0.06
const CIRCLE_POINTS: int = 40
## The rim of the shape (metres wide), the dark line under it that keeps it readable on any
## ground, and the thin range circle.
const RIM_WIDTH: float = 0.16
const RIM_UNDER: float = 0.3
const RANGE_WIDTH: float = 0.07
const UNDER_COLOR: Color = Color(0.05, 0.04, 0.1, 0.4)
const RANGE_ALPHA: float = 0.55
const FILL_ALPHA: float = 0.26
const CANCEL_COLOR: Color = Color(1.0, 0.3, 0.3, 1.0)
## Releasing would cast nothing: the preview goes grey.
const NO_CAST_COLOR: Color = Color(0.72, 0.74, 0.8, 1.0)
## The ring under a kid the cast would reach: red for an enemy, green for a heal's patient.
const REACH_ENEMY: Color = Color(1.0, 0.25, 0.2, 1.0)
const REACH_FRIEND: Color = Color(0.45, 1.0, 0.45, 1.0)
const REACH_PAD: float = 0.3
const REACH_FILL: float = 0.3
## The rim breathes a little so a held aim feels alive (not with Reduce motion).
const PULSE_SPEED: float = 5.0
const PULSE_DEPTH: float = 0.15
## The weapon's colour on the ground, by type.
const KIND_COLORS: Dictionary[WeaponDef.Kind, Color] = {
	WeaponDef.Kind.ATTACK: Color(1.0, 0.5, 0.22),
	WeaponDef.Kind.CROWD_CONTROL: Color(0.78, 0.55, 1.0),
	WeaponDef.Kind.BLOCK: Color(0.6, 0.85, 1.0),
	WeaponDef.Kind.HEAL: Color(0.5, 1.0, 0.5),
}

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


## The colour a weapon's preview takes on the ground.
static func color_for(def: WeaponDef, castable: bool, cancelled: bool) -> Color:
	if cancelled:
		return CANCEL_COLOR
	if not castable:
		return NO_CAST_COLOR
	return KIND_COLORS[def.kind]


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


func show_aim(sim: MatchSim, caster: PlayerState, def: WeaponDef, aim: Vector2, target_id: int, cancelled: bool, hold_seconds: float = 0.0) -> void:
	var castable: bool = sim.weapons.would_cast(sim, caster, def, aim, target_id)
	var reached: Array[PlayerState] = sim.weapons.would_reach(sim, caster, def, aim, target_id)
	var rings: Array[PackedVector2Array] = []
	var ring_colors: Array[Color] = []
	for target: PlayerState in reached:
		rings.append(circle(target.position, target.radius + REACH_PAD))
		ring_colors.append(REACH_FRIEND if target.team == caster.team else REACH_ENEMY)
	var pips: Array[PackedVector2Array] = snip_pips(caster.position, sim.weapons.aim_direction(caster, aim), def)
	show_lines(outline(sim, caster, def, aim, target_id), color_for(def, castable, cancelled), def.max_range > 0.0, rings, ring_colors, cancelled, pips, def.snip_count(hold_seconds))


## Draws the polylines: the first is the faint range circle when `has_range`, the rest are the
## shape (filled when closed, with a rim). `rings` go under the kids reached, in `ring_colors`.
func show_lines(lines: Array[PackedVector2Array], color: Color, has_range: bool = true,
		rings: Array[PackedVector2Array] = [], ring_colors: Array[Color] = [], cancelled: bool = false,
		pips: Array[PackedVector2Array] = [], pips_lit: int = 0) -> void:
	_mesh.clear_surfaces()
	var pulse: float = 1.0
	if not Settings.reduced_motion:
		pulse = 1.0 - PULSE_DEPTH * (0.5 + 0.5 * sin(float(Time.get_ticks_msec()) / 1000.0 * PULSE_SPEED))
	var rim: Color = Color(color, pulse)
	_mesh.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
	for index: int in lines.size():
		var line: PackedVector2Array = lines[index]
		if index == 0 and has_range:
			ribbon(_mesh, line, RANGE_WIDTH, Color(color, RANGE_ALPHA * (0.6 if cancelled else 1.0)))
		elif line.size() >= 4:
			fill(_mesh, line, Color(color, FILL_ALPHA * (0.6 if cancelled else 1.0)), HEIGHT)
	for index: int in lines.size():
		if index == 0 and has_range:
			continue
		ribbon(_mesh, lines[index], RIM_UNDER, UNDER_COLOR, HEIGHT + 0.002)
	for index: int in lines.size():
		if index == 0 and has_range:
			continue
		ribbon(_mesh, lines[index], RIM_WIDTH, rim, HEIGHT + 0.004)
	for index: int in pips.size():
		var lit: bool = index < pips_lit
		ribbon(_mesh, pips[index], RIM_WIDTH * 1.2, Color(UNDER_COLOR, 0.5 if lit else 0.25), HEIGHT + 0.012)
		ribbon(_mesh, pips[index], RIM_WIDTH, Color(1.0, 1.0, 1.0, 0.95) if lit else Color(color, 0.3), HEIGHT + 0.014)
	for index: int in rings.size():
		var ring_color: Color = ring_colors[index]
		fill(_mesh, rings[index], Color(ring_color, REACH_FILL), HEIGHT + 0.006)
		ribbon(_mesh, rings[index], RIM_UNDER, UNDER_COLOR, HEIGHT + 0.008)
		ribbon(_mesh, rings[index], RIM_WIDTH, ring_color, HEIGHT + 0.01)
	_mesh.surface_end()
