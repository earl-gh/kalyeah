class_name WeaponFxView
extends Node3D
## Draws the sim's live weapon objects: projectiles, zones (landing warnings,
## traps, slow zones, jacks fields), paper shields, plus a short flash for cones.
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
const CROSS_START: float = 2.9
const CROSS_RISE: float = 0.9
const CROSS_TIME: float = 0.9
## Turns per second of a thrown object (the spinning top spins fast).
const SPIN: float = 2.0
const TOP_SPIN: float = 6.0
const KIND_COLORS: Dictionary[WeaponDef.Kind, Color] = {
	WeaponDef.Kind.ATTACK: Color(1.0, 0.55, 0.25),
	WeaponDef.Kind.CROWD_CONTROL: Color(0.7, 0.45, 1.0),
	WeaponDef.Kind.BLOCK: Color(0.92, 0.92, 0.85),
	WeaponDef.Kind.HEAL: Color(0.45, 0.9, 0.4),
}
const SOIL_HEIGHT: float = 1.7
const SOIL_RISE_TIME: float = 0.22
const SOIL_COLLAPSE_TIME: float = 0.6
const ZONE_COLORS: Dictionary[WeaponSystem.ZoneKind, Color] = {
	WeaponSystem.ZoneKind.EXPLOSION: Color(1.0, 0.2, 0.15, 0.35),
	WeaponSystem.ZoneKind.TRAP: Color(0.95, 0.95, 0.9, 1.0),
	WeaponSystem.ZoneKind.SLOW_ZONE: Color(0.35, 0.65, 1.0, 0.35),
	WeaponSystem.ZoneKind.FIELD: Color(1.0, 0.85, 0.3, 0.35),
}

var _nodes: Dictionary[int, Node3D] = {}
var _zone_kinds: Dictionary[int, WeaponSystem.ZoneKind] = {}
var _flashes: Array[MeshInstance3D] = []
var _flash_time: Dictionary[MeshInstance3D, float] = {}
var _sim: MatchSim
var _crosses: Array[Node3D] = []
var _cross_time: Dictionary[Node3D, float] = {}


## Starts listening for cone flashes from `sim`.
func watch(sim: MatchSim) -> void:
	_sim = sim
	sim.weapons.cone_struck.connect(_on_cone_struck)
	sim.weapons.healed.connect(_on_healed)


func live_count() -> int:
	return _nodes.size()


func sync(sim: MatchSim, delta: float) -> void:
	var seen: Dictionary[int, bool] = {}
	for projectile: WeaponSystem.Projectile in sim.weapons.projectiles:
		seen[projectile.id] = true
		var node: Node3D = _nodes.get(projectile.id) as Node3D
		if node == null:
			node = _add_node(projectile.id, _projectile_node(projectile.def))
		node.position = Vector3(projectile.position.x, PROJECTILE_HEIGHT, projectile.position.y)
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
		var meteor: Node3D = node.get_node_or_null("Meteor") as Node3D
		if meteor != null:
			# it falls onto the circle and lands as the delay ends
			var left: float = clampf(zone.time_left / maxf(zone.def.delay, 0.01), 0.0, 1.0)
			meteor.position = METEOR_FROM * left + Vector3(0.0, WeaponModel.METEOR_SIZE / 2.0, 0.0)
			meteor.rotation.x += delta * 4.0
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
	var node: MeshInstance3D = MeshInstance3D.new()
	node.mesh = _zone_mesh(zone)
	if zone.def != null and zone.def.id == &"bato_heavy":
		var meteor: Node3D = WeaponModel.meteor()
		meteor.name = "Meteor"
		node.add_child(meteor)
	elif zone.def != null and zone.def.id == &"jacks":
		node.add_child(WeaponModel.spikes(zone.radius))
	return node


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


func _on_cone_struck(_owner_id: int, def: WeaponDef, origin: Vector2, direction: Vector2) -> void:
	var wedge: ImmediateMesh = ImmediateMesh.new()
	var half: float = deg_to_rad(def.angle_degrees / 2.0)
	wedge.surface_begin(Mesh.PRIMITIVE_TRIANGLES, _material(Color(KIND_COLORS[def.kind], 0.5)))
	for i: int in CONE_POINTS:
		var a: Vector2 = origin + direction.rotated(-half + 2.0 * half * float(i) / float(CONE_POINTS)) * def.max_range
		var b: Vector2 = origin + direction.rotated(-half + 2.0 * half * float(i + 1) / float(CONE_POINTS)) * def.max_range
		wedge.surface_add_vertex(Vector3(origin.x, ZONE_HEIGHT * 2.0, origin.y))
		wedge.surface_add_vertex(Vector3(b.x, ZONE_HEIGHT * 2.0, b.y))
		wedge.surface_add_vertex(Vector3(a.x, ZONE_HEIGHT * 2.0, a.y))
	wedge.surface_end()
	var node: MeshInstance3D = MeshInstance3D.new()
	node.mesh = wedge
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


## A green cross floats up over a healed kid and fades away.
func _on_healed(_owner_id: int, target_id: int, _def: WeaponDef) -> void:
	if _sim == null or not _sim.players.has(target_id):
		return
	var cross: Node3D = WeaponModel.cross()
	cross.name = "HealCross"
	var at: Vector2 = _sim.players[target_id].position
	cross.position = Vector3(at.x, CROSS_START, at.y)
	add_child(cross)
	_crosses.append(cross)
	_cross_time[cross] = CROSS_TIME


func _step_crosses(delta: float) -> void:
	var still: Array[Node3D] = []
	for cross: Node3D in _crosses:
		_cross_time[cross] -= delta
		var left: float = _cross_time[cross] / CROSS_TIME
		if left > 0.0:
			cross.position.y = CROSS_START + CROSS_RISE * (1.0 - left)
			cross.rotation.y += delta * TAU
			# it pops in, then shrinks away
			cross.scale = Vector3.ONE * clampf(minf((1.0 - left) * 8.0, left * 2.5), 0.01, 1.0)
			still.append(cross)
		else:
			_cross_time.erase(cross)
			cross.queue_free()
	_crosses = still
