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
## The slipper flies like a frisbee: its spin plane tipped `SLIPPER_TILT` degrees toward the camera so the sole reads, light
## `SLIPPER_SPIN_LIGHT` and heavy `SLIPPER_SPIN_HEAVY` turns a second. On its way back it hangs squashed (`HANG_S`) for
## `HANG_FRAMES` frames and spins the other way. A slap (a kid it hits) stretches it to `SLAP_S` along its flight for
## `FxSquash.IMPACT_FRAMES` frames with a white `SLAP_STREAK` m smack streak across its path at `SLAP_Y` m for
## `SLAP_FRAMES` frames; a slipper reacts to a hit on a kid within its hit reach plus `SLAP_REACH` m.
const SLIPPER_TILT: float = 25.0
const SLIPPER_SPIN_LIGHT: float = 3.0
const SLIPPER_SPIN_HEAVY: float = 2.0
const HANG_S: float = 0.8
const HANG_FRAMES: int = 3
const SLAP_S: float = 1.3
const SLAP_STREAK: float = 0.8
const SLAP_Y: float = 1.3
const SLAP_FRAMES: int = 3
const SLAP_REACH: float = 0.6
## Caught (gone within the catch distance plus `CATCH_SLACK` m of its thrower): it shrinks into the hand over `CATCH_FRAMES`
## frames with `CATCH_DUST` dust; gone anywhere else (a wall, its thrower tagged): `DROP_DUST` dust where it fell.
const CATCH_SLACK: float = 0.3
const CATCH_FRAMES: int = 3
const CATCH_DUST: int = 2
const DROP_DUST: int = 4
## Where a caught slipper goes: the thrower's hand height (metres).
const HAND_Y: float = 1.4
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
	var tilt: Node3D
	var track: FxTrack
	var spin_angle: float = 0.0
	var spin_sign: float = 1.0
	var born: float = 0.0
	## View time until which it plays a reaction (hang, slap), and which.
	var react_until: float = -1.0
	var react: StringName = &""
	var returning: bool = false
	var owner_id: int = -1


## A removed projectile playing its end.
class Ghost extends RefCounted:
	var node: Node3D
	var kind: StringName
	var born: float
	var life: float
	var from: Vector3
	var to: Vector3


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


## A kid was hit this frame at `at`: the nearest enemy slipper (or top) close enough to have hit it reacts.
func react_hit(kid_id: int, at: Vector2) -> void:
	var team: int = _ctx.team_of(kid_id)
	var kid: PlayerState = _ctx.sim.players.get(kid_id) as PlayerState if _ctx.sim != null else null
	var kid_radius: float = kid.radius if kid != null else 0.0
	var best: Rec = null
	var best_d: float = INF
	for rec: Rec in _recs.values():
		if rec.team == team or rec.kind != &"slipper":
			continue
		var d: float = rec.track.last_pos.distance_to(at)
		if d <= rec.def.projectile_radius + kid_radius + SLAP_REACH and d < best_d:
			best = rec
			best_d = d
	if best == null or _ctx.reduced:
		return
	# a slap: the slipper stretches on the hit and a white smack streak crosses its path
	best.react = &"slap"
	best.react_until = _ctx.time + FxSquash.IMPACT_FRAMES * FxSquash.FRAME
	var dir: Vector3 = Vector3(best.track.dir.x, 0.0, best.track.dir.y)
	best.squash.basis = FxSquash.stretch_basis(dir, SLAP_S)
	var across: Vector3 = Vector3(-dir.z, 0.0, dir.x)
	var place: Vector3 = Vector3(best.holder.position.x, SLAP_Y, best.holder.position.z)
	_ctx.ground.smear(Transform3D(Basis(Vector3.UP, atan2(-across.z, across.x)), place), &"streak", SLAP_STREAK, Color.WHITE, SLAP_FRAMES * FxSquash.FRAME)


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
	rec.owner_id = projectile.owner_id
	rec.holder = WeaponModel.rig(model, rec.kind == &"slipper")
	rec.squash = rec.holder.get_node("Squash") as Node3D
	rec.tilt = rec.squash.get_node_or_null("Tilt") as Node3D
	rec.spin = (rec.tilt if rec.tilt != null else rec.squash).get_node("Spin") as Node3D
	if rec.tilt != null:
		rec.tilt.basis = _toward_camera(deg_to_rad(SLIPPER_TILT))
	add_child(rec.holder)
	_recs[rec.id] = rec
	_ctx.shadows.attach(rec.id, drawn_width(rec.def), rec.team)
	if rec.kind == &"rock" or rec.kind == &"slipper":
		_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(projectile.position.x, 0.0, projectile.position.y), THROW_DUST)
	return rec


func _update(rec: Rec, projectile: WeaponSystem.Projectile) -> void:
	rec.holder.scale = Vector3.ONE * _ctx.view_scale
	rec.holder.position = Vector3(projectile.position.x, PROJECTILE_HEIGHT * _ctx.view_scale, projectile.position.y)
	match rec.kind:
		&"rock":
			_rock(rec, projectile)
		&"slipper":
			_slipper(rec, projectile)
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


## The slipper frisbees out, hangs and turns back spinning the other way, and slaps what it hits.
func _slipper(rec: Rec, projectile: WeaponSystem.Projectile) -> void:
	if _ctx.reduced:
		rec.squash.basis = Basis.IDENTITY
		# a fixed tilt pointing along its travel
		var still: Vector3 = _dir3(rec, projectile)
		if still != Vector3.ZERO:
			rec.spin.basis = Basis(Vector3.UP, atan2(-still.x, -still.z))
		return
	var back: bool = projectile.returning if projectile.direction != Vector2.ZERO else rec.returning or rec.track.flipped
	if back and not rec.returning:
		rec.returning = true
		rec.spin_sign = -rec.spin_sign
		rec.react = &"hang"
		rec.react_until = _ctx.time + HANG_FRAMES * FxSquash.FRAME
	var rate: float = SLIPPER_SPIN_LIGHT if rec.def.id == &"tsinelas_light" else SLIPPER_SPIN_HEAVY
	rec.spin_angle += rec.spin_sign * _ctx.delta * TAU * rate
	rec.spin.basis = Basis(Vector3.UP, rec.spin_angle)
	var dir: Vector3 = _dir3(rec, projectile)
	var pop: float = FxSquash.pop((_ctx.time - rec.born) / (FxSquash.POP_FRAMES * FxSquash.FRAME))
	var shape: Basis = FxSquash.stretch_basis(dir, FxSquash.velocity_stretch(rec.def.speed))
	if _ctx.time < rec.react_until:
		shape = Basis.from_scale(FxSquash.along(HANG_S)) if rec.react == &"hang" else FxSquash.stretch_basis(dir, SLAP_S)
	rec.squash.basis = shape.scaled(Vector3.ONE * maxf(pop, 0.01))


## A tilt of `angle` radians that leans an object's top toward the camera (so a flat thing's face reads from above).
func _toward_camera(angle: float) -> Basis:
	var camera: Camera3D = get_viewport().get_camera_3d() if is_inside_tree() else null
	var back: Vector3 = camera.global_basis.z if camera != null else FxCones.CAMERA_BACK
	var axis: Vector3 = Vector3.UP.cross(Vector3(back.x, 0.0, back.z).normalized())
	return Basis(axis.normalized(), angle) if axis.length_squared() > 0.0001 else Basis.IDENTITY


## The direction of travel in the world (offline the sim's, online from the moves seen).
func _dir3(rec: Rec, projectile: WeaponSystem.Projectile) -> Vector3:
	var dir: Vector2 = projectile.direction if projectile.direction != Vector2.ZERO else rec.track.dir
	return Vector3(dir.x, 0.0, dir.y)


func _remove(id: int) -> void:
	var rec: Rec = _recs[id]
	_recs.erase(id)
	_ctx.shadows.detach(id)
	gone.emit(id, rec.def, rec.team, rec.holder.position)
	if rec.kind == &"slipper":
		_slipper_gone(rec)
		return
	if _ctx.reduced or rec.kind != &"rock":
		rec.holder.queue_free()
		return
	# the rock smacks into its target: a flat squash for two frames, then it breaks into chips
	rec.squash.basis = Basis.from_scale(FxSquash.along(FxSquash.IMPACT_S))
	_ctx.particles.puff(FxParticles.Preset.CHIPS, rec.holder.position, ROCK_CHIPS)
	_ghost(rec.holder, &"rock", FxSquash.IMPACT_FRAMES * FxSquash.FRAME)


## A slipper left the sim: caught near its thrower it shrinks into the hand; dropped anywhere else it puffs dust.
func _slipper_gone(rec: Rec) -> void:
	var at: Vector3 = rec.holder.position
	var thrower: PlayerState = _thrower(rec)
	var reach: float = (_ctx.sim.rules.boomerang_catch_distance if _ctx.sim != null else 0.8) + CATCH_SLACK
	var ground: Vector3 = Vector3(at.x, 0.0, at.z)
	if thrower != null and thrower.position.distance_to(rec.track.last_pos) <= reach and not thrower.tagged:
		_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(thrower.position.x, 0.0, thrower.position.y), CATCH_DUST)
		if _ctx.reduced:
			rec.holder.queue_free()
			return
		var ghost: Ghost = _ghost(rec.holder, &"catch", CATCH_FRAMES * FxSquash.FRAME)
		ghost.from = at
		ghost.to = Vector3(thrower.position.x, HAND_Y * _ctx.view_scale, thrower.position.y)
		return
	_ctx.particles.puff(FxParticles.Preset.DUST, ground, DROP_DUST)
	rec.holder.queue_free()


## Who threw `rec`: offline the sim's owner, online the nearest kid of its team (snapshots carry no owner).
func _thrower(rec: Rec) -> PlayerState:
	if _ctx.sim == null:
		return null
	var owner: PlayerState = _ctx.sim.players.get(rec.owner_id) as PlayerState
	if owner != null and owner.team == rec.team:
		return owner
	var best: PlayerState = null
	for id: int in _ctx.sim.players:
		var kid: PlayerState = _ctx.sim.players[id]
		if kid.team == rec.team and (best == null or kid.position.distance_to(rec.track.last_pos) < best.position.distance_to(rec.track.last_pos)):
			best = kid
	return best


func _ghost(node: Node3D, kind: StringName, life: float) -> Ghost:
	var ghost: Ghost = Ghost.new()
	ghost.node = node
	ghost.kind = kind
	ghost.born = _ctx.time
	ghost.life = life
	_ghosts.append(ghost)
	return ghost


func _step_ghosts() -> void:
	var still: Array[Ghost] = []
	for ghost: Ghost in _ghosts:
		var t: float = (_ctx.time - ghost.born) / ghost.life
		if t >= 1.0 - 0.0001:
			ghost.node.queue_free()
			continue
		if ghost.kind == &"catch":
			ghost.node.position = ghost.from.lerp(ghost.to, t)
			ghost.node.scale = Vector3.ONE * _ctx.view_scale * maxf(1.0 - t, 0.01)
		still.append(ghost)
	_ghosts = still
