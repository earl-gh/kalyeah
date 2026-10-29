class_name FxProjectiles
extends Node3D
## Draws the sim's live projectiles (the thrown rock, the slippers, the marble, the spinning top) keyed by id: ground x/z is
## always the sim's, only height, turn, squash and decoration are the view's. Works from positions alone (online snapshots
## carry no direction), using the offline extras only to be exact. Each object moves like the toy it is (a per-weapon
## behaviour, below); a removed projectile plays a short end as a ghost (not counted as live). Reduce motion keeps the travel,
## the height and the shadow, and drops tumble, spin, squash and puffs.

## Flight height of a thrown object (times the view scale: 1.4 m drawn), and its turns per second when it has no behaviour.
const PROJECTILE_HEIGHT: float = 0.7
const SPIN: float = 2.0
const TOP_SPIN: float = 6.0
## Dust kicked up at the thrower's feet when something is thrown (at the feet, never over the kid's body).
const THROW_DUST: int = 4
## The rock: tumbles end over end this many turns a second; on a hit it squashes flat for `FxSquash.IMPACT_FRAMES` frames
## and breaks into `ROCK_CHIPS` chips.
const ROCK_TUMBLE: float = 2.5
const ROCK_CHIPS: int = 4
## Weapon id -> how its object moves.
const BEHAVIOUR: Dictionary[StringName, StringName] = {
	&"bato_light": &"rock",
	&"tsinelas_light": &"slipper",
	&"tsinelas_heavy": &"slipper",
	&"bola": &"marble",
	&"trumpo": &"top",
}

## A projectile left the sim (hit, caught, blocked or at the end of its flight) at `at` (world, drawn height).
signal gone(id: int, def: WeaponDef, team: int, at: Vector3)


## One drawn projectile.
class Rec extends RefCounted:
	var id: int
	var def: WeaponDef
	var team: int
	var kind: StringName
	var holder: Node3D
	var squash: Node3D
	var spin: Node3D
	var track: FxTrack
	var spin_angle: float = 0.0
	var born: float = 0.0


## A removed projectile playing its end.
class Ghost extends RefCounted:
	var node: Node3D
	var kind: StringName
	var born: float
	var life: float


var _ctx: FxContext
var _recs: Dictionary[int, Rec] = {}
var _ghosts: Array[Ghost] = []


func setup(ctx: FxContext) -> void:
	_ctx = ctx


func sync(sim: MatchSim) -> void:
	var seen: Dictionary[int, bool] = {}
	for projectile: WeaponSystem.Projectile in sim.weapons.projectiles:
		seen[projectile.id] = true
		var rec: Rec = _recs.get(projectile.id) as Rec
		if rec == null:
			rec = _create(projectile)
		rec.track.see(projectile.position, _ctx.time)
		_update(rec, projectile)
	for id: int in _recs.keys():
		if not seen.has(id):
			_remove(id)
	_step_ghosts()


## Sim-bound projectiles drawn right now.
func live_count() -> int:
	return _recs.size()


## Removed projectiles still playing their end.
func fading_count() -> int:
	return _ghosts.size()


## The node of projectile `id`, or null.
func node_for(id: int) -> Node3D:
	var rec: Rec = _recs.get(id) as Rec
	return rec.holder if rec != null else null


## A kid was hit this frame at `at`: the nearest enemy slipper or top reacts.
func react_hit(_kid_id: int, _at: Vector2) -> void:
	pass


## How wide `def`'s object is drawn (metres, its longest side times the view scale).
func drawn_width(def: WeaponDef) -> float:
	if WeaponModel.has_model(def.id):
		return (WeaponModel.OBJECTS[def.id][1] as float) * _ctx.view_scale
	return maxf(def.projectile_radius, 0.25) * 2.0 * _ctx.view_scale


func _create(projectile: WeaponSystem.Projectile) -> Rec:
	var rec: Rec = Rec.new()
	rec.id = projectile.id
	rec.def = projectile.def
	rec.team = projectile.team
	rec.kind = BEHAVIOUR.get(projectile.def.id, &"") as StringName
	rec.track = FxTrack.new()
	rec.born = _ctx.time
	var model: Node3D
	if WeaponModel.has_model(projectile.def.id):
		model = WeaponModel.make(projectile.def.id, true)
	else:
		var mesh: MeshInstance3D = MeshInstance3D.new()
		var size: float = projectile.def.radius if projectile.def.shape in [WeaponDef.Shape.SPINNER, WeaponDef.Shape.BOUNCER] else projectile.def.projectile_radius
		mesh.mesh = Props.weapon_object(projectile.def.id, maxf(size, 0.25))
		model = mesh
	rec.holder = WeaponModel.rig(model)
	rec.squash = rec.holder.get_node("Squash") as Node3D
	rec.spin = rec.squash.get_node("Spin") as Node3D
	add_child(rec.holder)
	_recs[rec.id] = rec
	_ctx.shadows.attach(rec.id, drawn_width(rec.def), rec.team)
	if rec.kind == &"rock":
		_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(projectile.position.x, 0.0, projectile.position.y), THROW_DUST)
	return rec


func _update(rec: Rec, projectile: WeaponSystem.Projectile) -> void:
	rec.holder.scale = Vector3.ONE * _ctx.view_scale
	rec.holder.position = Vector3(projectile.position.x, PROJECTILE_HEIGHT * _ctx.view_scale, projectile.position.y)
	match rec.kind:
		&"rock":
			_rock(rec, projectile)
		_:
			rec.spin.rotation.y += _ctx.delta * TAU * (TOP_SPIN if rec.def.id == &"trumpo" else SPIN)
	_ctx.shadows.place(rec.id, projectile.position, rec.holder.position.y)


## The rock pops out of the hand, tumbles end over end and stretches along its flight (it homes on its target).
func _rock(rec: Rec, projectile: WeaponSystem.Projectile) -> void:
	if _ctx.reduced:
		rec.squash.basis = Basis.IDENTITY
		return
	var dir: Vector3 = _dir3(rec, projectile)
	var pop: float = FxSquash.pop((_ctx.time - rec.born) / (FxSquash.POP_FRAMES * FxSquash.FRAME))
	rec.squash.basis = FxSquash.stretch_basis(dir, FxSquash.velocity_stretch(rec.def.speed)).scaled(Vector3.ONE * maxf(pop, 0.01))
	var axis: Vector3 = dir.cross(Vector3.UP)
	if axis.length_squared() > 0.0001:
		rec.spin_angle += _ctx.delta * TAU * ROCK_TUMBLE
		rec.spin.basis = Basis(axis.normalized(), rec.spin_angle)


## The direction of travel in the world (offline the sim's, online from the moves seen).
func _dir3(rec: Rec, projectile: WeaponSystem.Projectile) -> Vector3:
	var dir: Vector2 = projectile.direction if projectile.direction != Vector2.ZERO else rec.track.dir
	return Vector3(dir.x, 0.0, dir.y)


func _remove(id: int) -> void:
	var rec: Rec = _recs[id]
	_recs.erase(id)
	_ctx.shadows.detach(id)
	gone.emit(id, rec.def, rec.team, rec.holder.position)
	if _ctx.reduced or rec.kind != &"rock":
		rec.holder.queue_free()
		return
	# the rock smacks into its target: a flat squash for two frames, then it breaks into chips
	rec.squash.basis = Basis.from_scale(FxSquash.along(FxSquash.IMPACT_S))
	_ctx.particles.puff(FxParticles.Preset.CHIPS, rec.holder.position, ROCK_CHIPS)
	_ghost(rec.holder, &"rock", FxSquash.IMPACT_FRAMES * FxSquash.FRAME)


func _ghost(node: Node3D, kind: StringName, life: float) -> void:
	var ghost: Ghost = Ghost.new()
	ghost.node = node
	ghost.kind = kind
	ghost.born = _ctx.time
	ghost.life = life
	_ghosts.append(ghost)


func _step_ghosts() -> void:
	var still: Array[Ghost] = []
	for ghost: Ghost in _ghosts:
		if _ctx.time - ghost.born >= ghost.life - 0.0001:
			ghost.node.queue_free()
		else:
			still.append(ghost)
	_ghosts = still
