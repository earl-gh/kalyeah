class_name WeaponFxView
extends Node3D
## Draws the sim's weapon effects: it owns the shared pools and context and hands each kind of sim object to one controller
## (projectiles, zones, walls, cones, heals, hit bursts). Read-only: every frame it reads WeaponSystem state and the relayed
## signals; nothing here changes a rule, a timing or a hit (docs/ART.md "Skill effects").

## Landing circles and the cone flash say who threw them (TeamColors, yours or theirs) from their first frame: an opaque rim
## (`FX_RIM` m wide) round a see-through fill.
const FX_RIM: float = FxKit.RIM
const FX_RIM_ALPHA: float = FxKit.RIM_ALPHA
const FX_FILL_ALPHA: float = FxKit.FILL_ALPHA
const CONE_FLASH_TIME: float = FxCones.CONE_FLASH_TIME
## Turns per second of a thrown object (the spinning top spins fast).
const SPIN: float = FxProjectiles.SPIN
const TOP_SPIN: float = FxProjectiles.TOP_SPIN
## The burst on a kid who is hit: flying sparks and a ring on the ground, sized by the damage, gone in `BURST_TIME`.
const BURST_TIME: float = 0.35
const BURST_HEIGHT: float = 1.0
const BURST_SPARKS: int = 6
const BURST_SPARK_SPEED: float = 5.0
const BURST_COLOR: Color = Color(1.0, 0.62, 0.12)
const BURST_MIN: float = 0.9
const BURST_MAX: float = 1.3
const BURST_DAMAGE_STEP: float = 60.0
const BURST_SPARK_SIZE: Vector3 = Vector3(0.2, 0.2, 0.8)
const BURST_RING_FROM: float = 0.4
const BURST_RING_TO: float = 1.1
const BURST_RING_WIDTH: float = 0.12

## How much bigger than real thrown objects are drawn (CameraRules.kid_view_scale), like the kids who throw them.
var view_scale: float = 1.0
## The viewer's team: effects thrown by it are drawn in your team's colour, the rest in theirs.
var local_team: int = 0
var projectiles: FxProjectiles
var zones: FxZones
var walls: FxWalls
var cones: FxCones
var heals: FxHeals
var _ctx: FxContext
var _bursts: Array[Dictionary] = []
var _spark_material: StandardMaterial3D
static var _spark_mesh: BoxMesh
static var _impact_ring: ArrayMesh


func _init() -> void:
	_ctx = FxContext.new()
	_ctx.ground = FxGround.new()
	_ctx.ground.name = "Ground"
	_ctx.shadows = FxShadows.new()
	_ctx.shadows.name = "Shadows"
	_ctx.particles = FxParticles.new()
	_ctx.particles.name = "Particles"
	projectiles = FxProjectiles.new()
	zones = FxZones.new()
	walls = FxWalls.new()
	cones = FxCones.new()
	heals = FxHeals.new()
	for node: Node3D in [_ctx.ground, _ctx.shadows, _ctx.particles, projectiles, zones, walls, cones, heals]:
		add_child(node)
	_ctx.ground.setup(_ctx)
	_ctx.shadows.setup(_ctx)
	_ctx.particles.setup(_ctx)
	projectiles.setup(_ctx)
	zones.setup(_ctx)
	walls.setup(_ctx)
	cones.setup(_ctx)
	heals.setup(_ctx)


## Starts listening to `sim`'s relayed weapon signals (the same offline and online).
func watch(sim: MatchSim) -> void:
	_ctx.sim = sim
	_copy_settings()
	sim.weapons.cone_struck.connect(cones.on_struck)
	sim.weapons.cone_winding.connect(cones.on_winding)
	sim.player_damaged.connect(_on_damaged)
	sim.player_healed.connect(heals.on_healed)
	sim.weapon_cast.connect(_on_cast)
	sim.effect_applied.connect(zones.on_effect)


## What every controller reads (tests and tools).
func context() -> FxContext:
	return _ctx


## Sim-bound objects drawn right now (projectiles, zones, walls).
func live_count() -> int:
	return projectiles.live_count() + zones.live_count() + walls.live_count()


## Delayed-cone warnings on the ground right now.
func windup_count() -> int:
	return cones.windup_count()


## Hit bursts playing right now.
func burst_count() -> int:
	return _bursts.size()


## Effects still playing after their sim object (ghosts, flashes, crosses).
func fading_count() -> int:
	return projectiles.fading_count() + zones.fading_count() + walls.fading_count() + cones.fading_count() + heals.fading_count()


## Particles alive right now.
func particle_count() -> int:
	return _ctx.particles.live_particles()


## Your team's colour for an effect thrown by `team`, else the other team's.
func team_color(team: int) -> Color:
	return _ctx.team_color(team)


func sync(sim: MatchSim, delta: float) -> void:
	_ctx.sim = sim
	_ctx.delta = delta
	_ctx.time += delta
	_copy_settings()
	projectiles.sync(sim)
	zones.sync(sim)
	walls.sync(sim)
	heals.flush()
	cones.step()
	heals.step()
	_step_bursts(delta)
	_ctx.particles.step()
	_ctx.ground.step()
	_ctx.forget_old()


func _copy_settings() -> void:
	_ctx.view_scale = view_scale
	_ctx.local_team = local_team
	_ctx.reduced = Settings.reduced_motion


func _on_cast(id: int, weapon_id: StringName) -> void:
	_ctx.note_cast(id, weapon_id)
	heals.on_cast(id, weapon_id)


## A burst on a kid who is hit: a ring on the ground and sparks flying out (the sparks not with Reduce motion, and the ring does
## not grow then). Nothing is drawn over the kid itself.
func _on_damaged(id: int, amount: int) -> void:
	if _ctx.sim == null or not _ctx.sim.players.has(id):
		return
	_burst_meshes()
	var at: Vector2 = _ctx.sim.players[id].position
	var size: float = clampf(BURST_MIN + float(amount) / BURST_DAMAGE_STEP, BURST_MIN, BURST_MAX) * view_scale
	var node: Node3D = Node3D.new()
	node.name = "Burst"
	node.position = Vector3(at.x, 0.0, at.y)
	var ring: MeshInstance3D = MeshInstance3D.new()
	ring.name = "ImpactRing"
	ring.mesh = _impact_ring
	ring.position.y = FxKit.Y_DECAL
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
	var mesh: ImmediateMesh = ImmediateMesh.new()
	mesh.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
	AimIndicator.ribbon(mesh, AimIndicator.circle(Vector2.ZERO, 1.0), BURST_RING_WIDTH / BURST_RING_TO, Color.WHITE, 0.0)
	_impact_ring = FxKit.bake(mesh)


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
