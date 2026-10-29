class_name WeaponFxView
extends Node3D
## Draws the sim's live weapon objects: projectiles, zones (landing warnings,
## traps, slow zones, jacks fields), paper shields, a short flash for cones, the warning on the
## ground while a delayed cone winds up, and a small burst on a kid who is hit.
## Read-only: rebuilt from WeaponSystem state every frame, keyed by object id.

const PROJECTILE_HEIGHT: float = 0.7
const ZONE_HEIGHT: float = 0.04
const SHIELD_HEIGHT: float = 1.6
const PAPER_THICKNESS: float = 0.3
const CONE_FLASH_TIME: float = 0.3
const CONE_POINTS: int = 12
## The scissors of a snip sit this far along the cone (fraction of its range), lunge forward by `SNIP_LUNGE` of it.
const SNIP_AT: float = 0.55
const SNIP_LUNGE: float = 0.2
## The meteor of a rock-meteor circle starts this high and this far off, and falls straight in.
const METEOR_FROM: Vector3 = Vector3(3.0, 11.0, -3.0)
## The green cross over a healed kid: where it starts above the ground, how far it rises, how long.
## The tossed can (lata): how high its arc goes and how high its middle rests on the ground.
const CAN_ARC: float = 2.2
const CAN_REST: float = 0.35
const CROSS_START: float = 2.9
const CROSS_RISE: float = 0.9
const CROSS_TIME: float = 0.9
## Turns per second of a thrown object (the spinning top spins fast).
const SPIN: float = 2.0
const TOP_SPIN: float = 6.0
## The warning of a delayed cone: its edge shows at once, the thrower's team colour fills out from the caster
## until the strike (the same look as a landing circle).
const WINDUP_RIM: float = 0.08
const WINDUP_FILL: float = 0.22
## Landing circles and the cone flash say who threw them (TeamColors, yours or theirs) from their first frame: an opaque rim
## (`FX_RIM` m wide) round a see-through fill.
## Visual pass: a chunkier rim and a fuller fill (were 0.12 m and 0.3) so a landing reads at phone size.
const FX_RIM: float = 0.22
const FX_RIM_ALPHA: float = 0.95
const FX_FILL_ALPHA: float = 0.4
## The burst on a kid who is hit: flying sparks and a ring on the ground, sized by the damage (a bigger hit is
## bigger, a tick of damage is small), gone in `BURST_TIME`. No flash over the kid (owner, 2026-10-29: the warm sphere covered the
## kid's body for a moment on every hit, so in a crowded base the kids looked like they were blinking).
const BURST_TIME: float = 0.35
const BURST_HEIGHT: float = 1.0
const BURST_SPARKS: int = 6
const BURST_SPARK_SPEED: float = 5.0
## A saturated warm colour (the pale yellow read weakly on the grey street).
const BURST_COLOR: Color = Color(1.0, 0.62, 0.12)
## Every weapon's hit is between these sizes (a tick of damage still reads, the biggest hit is not a screen-filler), times
## `view_scale` like the kid it lands on; the sparks fly from `BURST_HEIGHT` up the drawn kid.
const BURST_MIN: float = 0.9
const BURST_MAX: float = 1.3
## Damage that grows the burst by one whole size (a 30 damage hit is half a size bigger than a tick).
const BURST_DAMAGE_STEP: float = 60.0
const BURST_SPARK_SIZE: Vector3 = Vector3(0.2, 0.2, 0.8)
## The impact ring on the ground round a kid who is hit: it grows from `BURST_RING_FROM` to `BURST_RING_TO` m (held at its
## end size with Reduce motion), `BURST_RING_WIDTH` m wide.
const BURST_RING_FROM: float = 0.4
const BURST_RING_TO: float = 1.1
const BURST_RING_WIDTH: float = 0.12
const SOIL_HEIGHT: float = 1.7
const SOIL_RISE_TIME: float = 0.22
const SOIL_COLLAPSE_TIME: float = 0.6
const ZONE_COLORS: Dictionary[WeaponSystem.ZoneKind, Color] = {
	WeaponSystem.ZoneKind.TRAP: Color(0.95, 0.95, 0.9, 1.0),
	WeaponSystem.ZoneKind.SLOW_ZONE: Color(0.35, 0.65, 1.0, 0.35),
	WeaponSystem.ZoneKind.FIELD: Color(1.0, 0.85, 0.3, 0.35),
}

var _nodes: Dictionary[int, Node3D] = {}
var _zone_kinds: Dictionary[int, WeaponSystem.ZoneKind] = {}
var _flashes: Array[MeshInstance3D] = []
var _flash_time: Dictionary[MeshInstance3D, float] = {}
## How much bigger than real thrown objects are drawn (CameraRules.kid_view_scale), like the kids who throw them.
var view_scale: float = 1.0
## The viewer's team: effects thrown by it are drawn in your team's colour, the rest in theirs.
var local_team: int = 0
var _sim: MatchSim
var _crosses: Array[Node3D] = []
var _cross_time: Dictionary[Node3D, float] = {}
## Delayed cones winding up: the warning node, its growing fill, the owner and the time left.
var _windups: Array[Dictionary] = []
var _bursts: Array[Dictionary] = []
var _spark_material: StandardMaterial3D
## Meshes shared by every burst (built once).
static var _spark_mesh: BoxMesh
static var _impact_ring: ArrayMesh
var _overlay_material: StandardMaterial3D


## Starts listening for cone flashes from `sim`.
func watch(sim: MatchSim) -> void:
	_sim = sim
	sim.weapons.cone_struck.connect(_on_cone_struck)
	sim.weapons.cone_winding.connect(_on_cone_winding)
	sim.weapons.healed.connect(_on_healed)
	sim.player_damaged.connect(_on_damaged)


func live_count() -> int:
	return _nodes.size()


## Delayed-cone warnings on the ground right now.
func windup_count() -> int:
	return _windups.size()


## Hit bursts playing right now.
func burst_count() -> int:
	return _bursts.size()


func sync(sim: MatchSim, delta: float) -> void:
	var seen: Dictionary[int, bool] = {}
	for projectile: WeaponSystem.Projectile in sim.weapons.projectiles:
		seen[projectile.id] = true
		var node: Node3D = _nodes.get(projectile.id) as Node3D
		if node == null:
			node = _add_node(projectile.id, _projectile_node(projectile.def))
		node.scale = Vector3.ONE * view_scale
		node.position = Vector3(projectile.position.x, PROJECTILE_HEIGHT * view_scale, projectile.position.y)
		node.rotation.y += delta * TAU * (TOP_SPIN if projectile.def.id == &"trumpo" else SPIN)
	for zone: WeaponSystem.Zone in sim.weapons.zones:
		seen[zone.id] = true
		var node: Node3D = _nodes.get(zone.id) as Node3D
		if node != null and _zone_kinds[zone.id] != zone.kind:
			_remove(zone.id)
			node = null
		if node == null:
			node = _add_node(zone.id, _zone_node(zone))
			_zone_kinds[zone.id] = zone.kind
		node.position = Vector3(zone.center.x, ZONE_HEIGHT, zone.center.y)
		if zone.kind == WeaponSystem.ZoneKind.TRAP:
			node.scale = Vector3.ONE * view_scale  # the paper ball, like the other thrown things (not the circles: those are real size)
		var meteor: Node3D = node.get_node_or_null("Meteor") as Node3D
		if meteor != null:
			# it falls onto the circle and lands as the delay ends
			var left: float = clampf(zone.time_left / maxf(zone.def.delay, 0.01), 0.0, 1.0)
			meteor.position = METEOR_FROM * left + Vector3(0.0, WeaponModel.METEOR_SIZE / 2.0, 0.0)
			meteor.rotation.x += delta * 4.0
		var can: Node3D = node.get_node_or_null("Can") as Node3D
		if can != null:
			# the tossed can arcs over and lands on the circle as the delay ends
			var flight: float = clampf(zone.time_left / maxf(zone.def.delay, 0.01), 0.0, 1.0)
			can.position.y = sin(flight * PI) * CAN_ARC + CAN_REST
			can.rotation.x += delta * 9.0
	for shield: WeaponSystem.Shield in sim.weapons.shields:
		seen[shield.id] = true
		var node: Node3D = _nodes.get(shield.id) as Node3D
		var soil: bool = shield.def != null and shield.def.look == &"soil"
		if node == null:
			if soil:
				node = _add_node(shield.id, WeaponModel.soil(shield.half_width * 2.0, SOIL_HEIGHT, shield.half_width * 2.0))
			else:
				# a standing sheet of paper (the owner's model); its base sits on the ground
				node = _add_node(shield.id, WeaponModel.shield(shield.half_width * 2.0, SHIELD_HEIGHT, PAPER_THICKNESS))
		var grown: float = 1.0
		if soil:
			# the block of earth rises, stands, then collapses back to the ground
			var age: float = shield.def.duration - shield.time_left
			grown = clampf(minf(age / SOIL_RISE_TIME, shield.time_left / SOIL_COLLAPSE_TIME), 0.04, 1.0)
		node.scale.y = grown
		node.position = Vector3(shield.center.x, 0.0, shield.center.y)
		node.rotation.y = -shield.along.angle()
	for id: int in _nodes.keys():
		if not seen.has(id):
			_remove(id)
	_step_flashes(delta)
	_step_crosses(delta)
	_step_windups(delta)
	_step_bursts(delta)


func _add(id: int, mesh: Mesh) -> MeshInstance3D:
	var node: MeshInstance3D = MeshInstance3D.new()
	node.mesh = mesh
	_add_node(id, node)
	return node


func _add_node(id: int, node: Node3D) -> Node3D:
	add_child(node)
	_nodes[id] = node
	return node


func _remove(id: int) -> void:
	_nodes[id].queue_free()
	_nodes.erase(id)
	_zone_kinds.erase(id)


## A thrown object: the owner's model when there is one, else the code-drawn one.
func _projectile_node(def: WeaponDef) -> Node3D:
	if WeaponModel.has_model(def.id):
		return WeaponModel.make(def.id, true)
	var node: MeshInstance3D = MeshInstance3D.new()
	node.mesh = _projectile_mesh(def)
	return node


func _projectile_mesh(def: WeaponDef) -> Mesh:
	var size: float = def.radius if def.shape == WeaponDef.Shape.SPINNER or def.shape == WeaponDef.Shape.BOUNCER else def.projectile_radius
	return Props.weapon_object(def.id, maxf(size, 0.25))


## A placed zone: the trap is the crumpled paper ball, the rest are flat tinted discs.
func _zone_node(zone: WeaponSystem.Zone) -> Node3D:
	if zone.kind == WeaponSystem.ZoneKind.TRAP:
		return WeaponModel.make(&"papel_trap", false)
	var node: MeshInstance3D
	if zone.kind == WeaponSystem.ZoneKind.EXPLOSION:
		# where it lands and who threw it: the thrower's team colour
		node = _marked(AimIndicator.circle(Vector2.ZERO, zone.radius), team_color(zone.team), 0.0)
	else:
		node = MeshInstance3D.new()
		node.mesh = _zone_mesh(zone)
	if zone.def != null and zone.def.id == &"bato_heavy":
		var meteor: Node3D = WeaponModel.meteor()
		meteor.name = "Meteor"
		node.add_child(meteor)
	elif zone.def != null and zone.def.id == &"lata":
		var can: Node3D = WeaponModel.make(&"lata", true)
		can.name = "Can"
		node.add_child(can)
	elif zone.def != null and zone.def.id == &"jacks":
		node.add_child(WeaponModel.spikes(zone.radius))
	return node


## Your team's colour for an effect thrown by `team`, else the other team's.
func team_color(team: int) -> Color:
	return TeamColors.world_own() if team == local_team else TeamColors.world_enemy()


## An opaque rim along `outline` round a see-through fill, in `color`, drawn on the ground at `height` (both sides, no culling).
func _marked(outline: PackedVector2Array, color: Color, height: float) -> MeshInstance3D:
	var mesh: ImmediateMesh = ImmediateMesh.new()
	mesh.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
	AimIndicator.fill(mesh, outline, Color(color, FX_FILL_ALPHA), height)
	AimIndicator.ribbon(mesh, outline, FX_RIM, Color(color, FX_RIM_ALPHA), height + ZONE_HEIGHT)
	mesh.surface_end()
	return _overlay(mesh)


func _zone_mesh(zone: WeaponSystem.Zone) -> Mesh:
	var color: Color = ZONE_COLORS[zone.kind]
	var disc: CylinderMesh = CylinderMesh.new()
	disc.top_radius = zone.radius
	disc.bottom_radius = zone.radius
	disc.height = ZONE_HEIGHT
	disc.material = _material(color)
	return disc


func _material(color: Color) -> StandardMaterial3D:
	var material: StandardMaterial3D = StandardMaterial3D.new()
	material.albedo_color = color
	if color.a < 1.0:
		material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	return material


## The cone's strike: the wedge in the thrower's team colour, whole from the first frame until it goes (it used to be a one-sided
## mesh seen from below, so only the scissors showed).
func _on_cone_struck(owner_id: int, def: WeaponDef, origin: Vector2, direction: Vector2) -> void:
	var node: MeshInstance3D = _marked(_wedge(origin, direction, def), team_color(_team_of(owner_id)), ZONE_HEIGHT * 2.0)
	add_child(node)
	if def.id == &"gunting_light" or def.id == &"gunting_heavy":
		var scissors: Node3D = WeaponModel.scissors(def.max_range * SNIP_AT * 1.2)
		scissors.name = "Scissors"
		scissors.rotation.y = atan2(-direction.x, -direction.y)
		scissors.set_meta("start", Vector3(origin.x, 0.0, origin.y) + Vector3(direction.x, 0.0, direction.y) * def.max_range * SNIP_AT)
		scissors.set_meta("lunge", Vector3(direction.x, 0.0, direction.y) * def.max_range * SNIP_LUNGE)
		scissors.position = scissors.get_meta("start") as Vector3
		node.add_child(scissors)
	_flashes.append(node)
	_flash_time[node] = CONE_FLASH_TIME


func _step_flashes(delta: float) -> void:
	var still: Array[MeshInstance3D] = []
	for node: MeshInstance3D in _flashes:
		_flash_time[node] -= delta
		var scissors: Node3D = node.get_node_or_null("Scissors") as Node3D
		if scissors != null:
			var progress: float = clampf(1.0 - _flash_time[node] / CONE_FLASH_TIME, 0.0, 1.0)
			scissors.position = (scissors.get_meta("start") as Vector3) + (scissors.get_meta("lunge") as Vector3) * progress
		if _flash_time[node] > 0.0:
			still.append(node)
		else:
			_flash_time.erase(node)
			node.queue_free()
	_flashes = still


## The outline of `def`'s cone from `origin` along `direction`: the tip, the arc, back to the tip.
func _wedge(origin: Vector2, direction: Vector2, def: WeaponDef) -> PackedVector2Array:
	var wedge: PackedVector2Array = PackedVector2Array([origin])
	var half: float = deg_to_rad(def.angle_degrees / 2.0)
	for i: int in CONE_POINTS + 1:
		wedge.append(origin + direction.rotated(-half + 2.0 * half * float(i) / float(CONE_POINTS)) * def.max_range)
	wedge.append(origin)
	return wedge


func _team_of(id: int) -> int:
	var state: PlayerState = _sim.players.get(id) as PlayerState if _sim != null else null
	return state.team if state != null else local_team


## The warning of a delayed cone, from the cast to the strike: the wedge's edge on the ground, and
## a wedge in the thrower's team colour that fills outward from the caster as the time runs out.
func _on_cone_winding(owner_id: int, def: WeaponDef, origin: Vector2, direction: Vector2) -> void:
	var wedge: PackedVector2Array = _wedge(Vector2.ZERO, direction, def)
	var color: Color = team_color(_team_of(owner_id))
	var node: Node3D = Node3D.new()
	node.position = Vector3(origin.x, 0.0, origin.y)
	var rim: ImmediateMesh = ImmediateMesh.new()
	rim.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
	AimIndicator.fill(rim, wedge, Color(color, 0.06), ZONE_HEIGHT * 3.0)
	AimIndicator.ribbon(rim, wedge, WINDUP_RIM, Color(color, 0.8), ZONE_HEIGHT * 4.0)
	rim.surface_end()
	node.add_child(_overlay(rim))
	var growing: ImmediateMesh = ImmediateMesh.new()
	growing.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
	AimIndicator.fill(growing, wedge, Color(color, WINDUP_FILL), ZONE_HEIGHT * 3.5)
	growing.surface_end()
	var fill: MeshInstance3D = _overlay(growing)
	fill.scale = Vector3(0.05, 1.0, 0.05)
	node.add_child(fill)
	add_child(node)
	_windups.append({"node": node, "fill": fill, "owner": owner_id, "left": def.delay, "total": maxf(def.delay, 0.01)})


func _overlay(shape: Mesh) -> MeshInstance3D:
	if _overlay_material == null:
		_overlay_material = StandardMaterial3D.new()
		_overlay_material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		_overlay_material.vertex_color_use_as_albedo = true
		_overlay_material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		_overlay_material.cull_mode = BaseMaterial3D.CULL_DISABLED
	var instance: MeshInstance3D = MeshInstance3D.new()
	instance.mesh = shape
	instance.material_override = _overlay_material
	return instance


func _step_windups(delta: float) -> void:
	var still: Array[Dictionary] = []
	for windup: Dictionary in _windups:
		windup["left"] = (windup["left"] as float) - delta
		var caster: PlayerState = _sim.players.get(windup["owner"] as int) as PlayerState if _sim != null else null
		# the strike lands (or the caster was tagged first and it never will): the warning goes
		if (windup["left"] as float) <= 0.0 or caster == null or caster.tagged:
			(windup["node"] as Node3D).queue_free()
			continue
		var progress: float = 1.0 - (windup["left"] as float) / (windup["total"] as float)
		var size: float = lerpf(0.05, 1.0, progress)
		(windup["fill"] as Node3D).scale = Vector3(size, 1.0, size)
		still.append(windup)
	_windups = still


## A burst on a kid who is hit: a ring on the ground and sparks flying out (the sparks not with Reduce motion, and the ring does
## not grow then). Nothing is drawn over the kid itself.
func _on_damaged(id: int, amount: int) -> void:
	if _sim == null or not _sim.players.has(id):
		return
	_burst_meshes()
	var at: Vector2 = _sim.players[id].position
	var size: float = clampf(BURST_MIN + float(amount) / BURST_DAMAGE_STEP, BURST_MIN, BURST_MAX) * view_scale
	var node: Node3D = Node3D.new()
	node.name = "Burst"
	node.position = Vector3(at.x, 0.0, at.y)
	var ring: MeshInstance3D = MeshInstance3D.new()
	ring.name = "ImpactRing"
	ring.mesh = _impact_ring
	ring.position.y = ZONE_HEIGHT * 2.0
	var ring_material: StandardMaterial3D = _unshaded(Color(BURST_COLOR, 0.95))
	ring_material.cull_mode = BaseMaterial3D.CULL_DISABLED
	ring.material_override = ring_material
	ring.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	node.add_child(ring)
	var sparks: Array[Node3D] = []
	if not Settings.reduced_motion:
		if _spark_material == null:
			_spark_material = _unshaded(BURST_COLOR)
		for i: int in BURST_SPARKS:
			var spark: MeshInstance3D = MeshInstance3D.new()
			spark.mesh = _spark_mesh
			spark.material_override = _spark_material
			var angle: float = TAU * (float(i) + 0.5 * float(id % 2)) / float(BURST_SPARKS)
			spark.set_meta("dir", Vector3(cos(angle), 0.5 + 0.35 * float(i % 3), sin(angle)).normalized())
			spark.rotation.y = -angle + PI / 2.0
			node.add_child(spark)
			sparks.append(spark)
	add_child(node)
	_bursts.append({"node": node, "ring": ring,
		"ring_material": ring_material, "sparks": sparks, "age": 0.0, "size": size})


static func _burst_meshes() -> void:
	if _spark_mesh != null:
		return
	_spark_mesh = BoxMesh.new()
	_spark_mesh.size = BURST_SPARK_SIZE
	# a ring of radius 1 (scaled per frame), BURST_RING_WIDTH / BURST_RING_TO of it wide at its end size
	var outline: PackedVector2Array = AimIndicator.circle(Vector2.ZERO, 1.0)
	var mesh: ImmediateMesh = ImmediateMesh.new()
	mesh.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
	AimIndicator.ribbon(mesh, outline, BURST_RING_WIDTH / BURST_RING_TO, Color.WHITE, 0.0)
	mesh.surface_end()
	_impact_ring = ArrayMesh.new()
	_impact_ring.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, mesh.surface_get_arrays(0))


func _unshaded(color: Color) -> StandardMaterial3D:
	var material: StandardMaterial3D = StandardMaterial3D.new()
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	material.albedo_color = color
	return material


func _step_bursts(delta: float) -> void:
	var still: Array[Dictionary] = []
	for burst: Dictionary in _bursts:
		burst["age"] = (burst["age"] as float) + delta
		var progress: float = (burst["age"] as float) / BURST_TIME
		if progress >= 1.0:
			(burst["node"] as Node3D).queue_free()
			continue
		var size: float = burst["size"] as float
		var grow: float = 1.0 if Settings.reduced_motion else progress
		(burst["ring"] as Node3D).scale = Vector3.ONE * size * lerpf(BURST_RING_FROM, BURST_RING_TO, grow)
		(burst["ring_material"] as StandardMaterial3D).albedo_color.a = 0.95 * (1.0 - progress)
		for spark: Node3D in burst["sparks"] as Array[Node3D]:
			var dir: Vector3 = spark.get_meta("dir") as Vector3
			spark.position = Vector3(0.0, BURST_HEIGHT * view_scale, 0.0) + dir * BURST_SPARK_SPEED * size * progress * (1.0 - 0.4 * progress)
			spark.scale = Vector3.ONE * (1.0 - progress)
		still.append(burst)
	_bursts = still


## A green cross floats up over a healed kid and fades away.
func _on_healed(_owner_id: int, target_id: int, _def: WeaponDef) -> void:
	if _sim == null or not _sim.players.has(target_id):
		return
	var cross: Node3D = WeaponModel.cross()
	cross.name = "HealCross"
	var at: Vector2 = _sim.players[target_id].position
	cross.position = Vector3(at.x, CROSS_START * view_scale, at.y)
	add_child(cross)
	_crosses.append(cross)
	_cross_time[cross] = CROSS_TIME


func _step_crosses(delta: float) -> void:
	var still: Array[Node3D] = []
	for cross: Node3D in _crosses:
		_cross_time[cross] -= delta
		var left: float = _cross_time[cross] / CROSS_TIME
		if left > 0.0:
			cross.position.y = (CROSS_START + CROSS_RISE * (1.0 - left)) * view_scale
			cross.rotation.y += delta * TAU
			# it pops in, then shrinks away
			cross.scale = Vector3.ONE * clampf(minf((1.0 - left) * 8.0, left * 2.5), 0.01, 1.0)
			still.append(cross)
		else:
			_cross_time.erase(cross)
			cross.queue_free()
	_crosses = still
