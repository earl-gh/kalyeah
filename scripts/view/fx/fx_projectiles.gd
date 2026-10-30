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
## The rock: tumbles end over end this many turns a second; it is smeared along its flight for its first `FxSquash.SMEAR_FRAMES`
## frames (crisp after); on a hit it squashes flat for `FxSquash.IMPACT_FRAMES` frames, springs back through an overshoot to
## its own size (`FxSquash.landing`) and breaks into `ROCK_CHIPS` chips.
const ROCK_TUMBLE: float = 2.5
const ROCK_CHIPS: int = 4
## The spinning top stands on its tip on the street (`TOP_SPIN` turns a second) and wobbles (FxMotion.top_tilt). It drops
## in from `TOP_DROP` m over `TOP_DROP_FRAMES` frames and lands squashed (`TOP_LAND_S`) with `TOP_LAND_DUST` dust; while it
## moves it leaves a short dust trail. A kid it hits makes it squash (`TOP_LAND_S`) and its lean kick up; the kid is thrown
## up off `TOP_LAUNCH_DUST` dust. At the end it leans over (`TOP_END_TILT` degrees, then down to `TOP_FALL_TILT`) slowing
## its spin, and shrinks away over `TOP_END_FRAMES` frames with `TOP_END_DUST` dust. A top reacts to a kid within its hit
## radius plus `FxSlippers.SLAP_REACH` m.
const TOP_DROP: float = 0.6
const TOP_DROP_FRAMES: int = 2
## The drop is smeared (stretched `TOP_DROP_STRETCH` along its fall, with a white trail `TOP_DROP_TRAIL` m long above it).
const TOP_DROP_STRETCH: float = 1.3
const TOP_DROP_TRAIL: float = 1.0
const TOP_LAND_S: float = 0.8
const TOP_LAND_DUST: int = 4
const TOP_LAUNCH_DUST: int = 6
const TOP_END_TILT: float = 35.0
const TOP_FALL_TILT: float = 80.0
const TOP_END_FRAMES: int = 8
const TOP_END_DUST: int = 4
## The top's shadow is this much of its drawn height (it is narrower than it is tall).
const TOP_SHADOW: float = 0.63
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
	## The marble: the bounce segment it is on, when it last touched down, where it was thrown from and its rings.
	var seg: int = 0
	var touched: float = -1.0
	var origin: Vector2 = Vector2.INF
	var rings: Array[MeshInstance3D] = []
	## The top: its dust trail, its last hit, and whether it has landed.
	var trail: CPUParticles3D
	var spiked: float = -1.0
	var landed: bool = false
	## View time its fast motion began (the throw, a slipper's turn back): the smear fades out over the frames after it.
	var smear_from: float = 0.0
	## View time the slipper's return trail is due (-1: none pending), and when its current reaction began.
	var tail_due: float = -1.0
	var react_from: float = -1.0


## A removed projectile playing its end.
class Ghost extends RefCounted:
	var node: Node3D
	var kind: StringName
	var born: float
	var life: float
	var from: Vector3
	var to: Vector3
	var spin: float = 0.0


var _ctx: FxContext
var _recs: Dictionary[int, Rec] = {}
var _ghosts: Array[Ghost] = []
## The marble's bounce (FxMarble) and the slippers' flight (FxSlippers).
var marble: FxMarble
var slippers: FxSlippers


func setup(ctx: FxContext) -> void:
	_ctx = ctx
	marble = FxMarble.new()
	marble.setup(ctx, self)
	slippers = FxSlippers.new()
	slippers.setup(ctx, self)


## The projectiles drawn right now (FxMarble reads them).
func recs() -> Array[Rec]:
	var all: Array[Rec] = []
	all.assign(_recs.values())
	return all


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
	for rec: Rec in _recs.values():
		if rec.kind == &"top" and rec.team != team and rec.track.last_pos.distance_to(at) <= rec.def.radius + kid_radius + FxSlippers.SLAP_REACH and not _ctx.reduced:
			# the top bumps into the kid: it squashes and its lean kicks up
			rec.spiked = _ctx.time
	slippers.slap(team, kid_radius, at)


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
		# a top stands on its tip; everything else flies centred on its height
		model = WeaponModel.make(projectile.def.id, rec.kind != &"top")
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
		rec.tilt.basis = toward_camera(deg_to_rad(FxSlippers.SLIPPER_TILT))
	add_child(rec.holder)
	_recs[rec.id] = rec
	_ctx.shadows.attach(rec.id, drawn_width(rec.def) * (TOP_SHADOW if rec.kind == &"top" else 1.0), rec.team)
	rec.smear_from = _ctx.time
	if rec.kind == &"rock" or rec.kind == &"slipper":
		_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(projectile.position.x, 0.0, projectile.position.y), THROW_DUST)
	_release(rec, projectile)
	return rec


## The streaks of a throw on the frame its object is first seen: the thrower's release trail along the aim (a rock, a slipper,
## the marble), or the white trail above a spinning top as it drops in.
func _release(rec: Rec, projectile: WeaponSystem.Projectile) -> void:
	if _ctx.reduced:
		return
	var at: Vector2 = projectile.position
	if rec.kind == &"top":
		var head: Vector3 = Vector3(at.x, TOP_DROP + drawn_width(rec.def) * TOP_SHADOW, at.y)
		_ctx.ground.trail(head + Vector3.UP * TOP_DROP_TRAIL, head, FxRelease.WIDTH, FxRelease.TINT, FxSquash.TRAIL_FRAMES * FxSquash.FRAME)
		return
	var dir: Vector2 = projectile.direction
	var thrower: PlayerState = _ctx.sim.players.get(projectile.owner_id) as PlayerState if _ctx.sim != null else null
	if dir == Vector2.ZERO and thrower != null:
		dir = thrower.facing
	var from: Vector2 = thrower.position if thrower != null else at
	var height: float = FxMotion.BOLA_HAND_Y if rec.kind == &"marble" else PROJECTILE_HEIGHT * _ctx.view_scale
	FxRelease.play(_ctx, from, dir, rec.def.speed, height)


func _update(rec: Rec, projectile: WeaponSystem.Projectile) -> void:
	rec.holder.scale = Vector3.ONE * _ctx.view_scale
	rec.holder.position = Vector3(projectile.position.x, PROJECTILE_HEIGHT * _ctx.view_scale, projectile.position.y)
	match rec.kind:
		&"rock":
			_rock(rec, projectile)
		&"slipper":
			slippers.pose(rec, projectile)
		&"marble":
			marble.pose(rec, projectile)
		&"top":
			_top(rec, projectile)
		_:
			rec.spin.rotation.y += _ctx.delta * TAU * (TOP_SPIN if rec.def.id == &"trumpo" else SPIN)
	_ctx.shadows.place(rec.id, projectile.position, rec.holder.position.y)


## The rock pops out of the hand, tumbles end over end and is smeared along its flight on its first frames (it homes on its target).
func _rock(rec: Rec, projectile: WeaponSystem.Projectile) -> void:
	if _ctx.reduced:
		rec.squash.basis = Basis.IDENTITY
		return
	var dir: Vector3 = dir3(rec, projectile)
	var pop: float = FxSquash.pop((_ctx.time - rec.born) / (FxSquash.POP_FRAMES * FxSquash.FRAME))
	rec.squash.basis = smear_basis(rec, dir).scaled(Vector3.ONE * maxf(pop, 0.01))
	var axis: Vector3 = dir.cross(Vector3.UP)
	if axis.length_squared() > 0.0001:
		rec.spin_angle += _ctx.delta * TAU * ROCK_TUMBLE
		rec.spin.basis = Basis(axis.normalized(), rec.spin_angle)


## The top drops in on its tip, spins and wobbles as it travels, leaving a little dust, and kicks when it hits a kid.
func _top(rec: Rec, projectile: WeaponSystem.Projectile) -> void:
	var age: float = _ctx.time - rec.born
	rec.holder.position.y = 0.0
	if _ctx.reduced:
		rec.squash.basis = Basis.IDENTITY
		return
	rec.spin_angle += _ctx.delta * TAU * TOP_SPIN
	rec.spin.basis = Basis(Vector3.UP, rec.spin_angle)
	var drop: float = TOP_DROP_FRAMES * FxSquash.FRAME
	var shape: Vector3 = Vector3.ONE
	if age < drop:
		rec.holder.position.y = TOP_DROP * (1.0 - age / drop)
		shape = FxSquash.smear_scale(TOP_DROP_STRETCH)
	elif not rec.landed:
		rec.landed = true
		_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(projectile.position.x, 0.0, projectile.position.y), TOP_LAND_DUST)
		rec.trail = _ctx.particles.trail_take()
	var landed_frames: float = (age - drop) / FxSquash.FRAME
	if age >= drop and landed_frames < FxSquash.landing_frames():
		shape = FxSquash.along(FxSquash.landing(TOP_LAND_S, landed_frames))
	var spike_age: float = _ctx.time - rec.spiked if rec.spiked >= 0.0 else -1.0
	if spike_age >= 0.0 and spike_age / FxSquash.FRAME < FxSquash.landing_frames():
		shape = FxSquash.along(FxSquash.landing(TOP_LAND_S, spike_age / FxSquash.FRAME))
	rec.squash.basis = FxMotion.top_tilt(_ctx.time, spike_age) * Basis.from_scale(shape)
	if rec.trail != null:
		rec.trail.global_position = Vector3(projectile.position.x, 0.05, projectile.position.y)


## A status effect landed on kid `id`: thrown up by a top, the kid leaves a puff of dust.
func on_effect(id: int, type: int) -> void:
	if type != StatusEffects.Type.AIRBORNE or _ctx.sim == null or not _ctx.sim.players.has(id):
		return
	var at: Vector2 = _ctx.sim.players[id].position
	for rec: Rec in _recs.values():
		if rec.kind == &"top" and rec.track.last_pos.distance_to(at) <= rec.def.radius + FxSlippers.SLAP_REACH + 1.0:
			_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(at.x, 0.0, at.y), TOP_LAUNCH_DUST)
			return


## A tilt of `angle` radians that leans an object's top toward the camera (so a flat thing's face reads from above).
func toward_camera(angle: float) -> Basis:
	var camera: Camera3D = get_viewport().get_camera_3d() if is_inside_tree() else null
	var back: Vector3 = camera.global_basis.z if camera != null else FxKit.CAMERA_BACK
	var axis: Vector3 = Vector3.UP.cross(Vector3(back.x, 0.0, back.z).normalized())
	return Basis(axis.normalized(), angle) if axis.length_squared() > 0.0001 else Basis.IDENTITY


## The smear of `rec`'s object along `dir` right now: stretched along its flight over its first frames after
## `rec.smear_from`, a plain basis (crisp) after.
func smear_basis(rec: Rec, dir: Vector3) -> Basis:
	var frames: float = (_ctx.time - rec.smear_from) / FxSquash.FRAME
	return FxSquash.smear_basis(dir, FxSquash.smear_stretch(rec.def.speed, drawn_width(rec.def), frames))


## The direction of travel in the world (offline the sim's, online from the moves seen).
func dir3(rec: Rec, projectile: WeaponSystem.Projectile) -> Vector3:
	var dir: Vector2 = projectile.direction if projectile.direction != Vector2.ZERO else rec.track.dir
	return Vector3(dir.x, 0.0, dir.y)


func _remove(id: int) -> void:
	var rec: Rec = _recs[id]
	_recs.erase(id)
	_ctx.shadows.detach(id)
	gone.emit(id, rec.def, rec.team, rec.holder.position)
	if rec.kind == &"slipper":
		slippers.gone(rec)
		return
	if rec.kind == &"marble":
		marble.gone(rec)
		return
	if rec.kind == &"top":
		if rec.trail != null:
			_ctx.particles.trail_give(rec.trail)
		if _ctx.reduced:
			rec.holder.queue_free()
			return
		# the end of its run: it leans over, slows, tips and is gone
		_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(rec.holder.position.x, 0.0, rec.holder.position.z), TOP_END_DUST)
		var ghost: Ghost = ghost(rec.holder, &"tip_over", TOP_END_FRAMES * FxSquash.FRAME)
		ghost.spin = rec.spin_angle
		ghost.to = Vector3(rec.track.dir.x, 0.0, rec.track.dir.y)
		return
	if _ctx.reduced or rec.kind != &"rock":
		rec.holder.queue_free()
		return
	# the rock smacks into its target: a flat squash for two frames, a spring back through an overshoot to its size (one
	# crisp frame), and it breaks into chips
	rec.squash.basis = Basis.from_scale(FxSquash.along(FxSquash.IMPACT_S))
	_ctx.particles.puff(FxParticles.Preset.CHIPS, rec.holder.position, ROCK_CHIPS)
	ghost(rec.holder, &"rock", (FxSquash.landing_frames() + 1) * FxSquash.FRAME)


func ghost(node: Node3D, kind: StringName, life: float) -> Ghost:
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
		elif ghost.kind == &"rock":
			var frames: float = (_ctx.time - ghost.born) / FxSquash.FRAME
			(ghost.node.get_node("Squash") as Node3D).basis = Basis.from_scale(FxSquash.along(FxSquash.landing(FxSquash.IMPACT_S, frames)))
		elif ghost.kind == &"tip_over":
			# lean 8 -> 35 degrees over the first 6 frames, then fall over to 80, spinning slower, shrinking away
			var lean: float = lerpf(FxMotion.TOP_TILT, TOP_END_TILT, minf(t / 0.75, 1.0)) if t < 0.75 else lerpf(TOP_END_TILT, TOP_FALL_TILT, (t - 0.75) / 0.25)
			var axis: Vector3 = ghost.to.cross(Vector3.UP) if ghost.to != Vector3.ZERO else Vector3.RIGHT
			var squash: Node3D = ghost.node.get_node("Squash") as Node3D
			squash.basis = Basis(-axis.normalized(), deg_to_rad(lean))
			ghost.spin += _ctx.delta * TAU * TOP_SPIN * (1.0 - t)
			(squash.get_node("Spin") as Node3D).basis = Basis(Vector3.UP, ghost.spin)
			ghost.node.scale = Vector3.ONE * _ctx.view_scale * (1.0 if t < 0.5 else maxf(1.0 - (t - 0.5) / 0.5, 0.01))
		elif ghost.kind == &"runout" and t > 0.25:
			ghost.node.position = ghost.from.lerp(ghost.to, t)
			(ghost.node.get_node("Squash") as Node3D).basis = Basis.IDENTITY
			ghost.node.scale = Vector3.ONE * _ctx.view_scale * maxf(1.0 - t, 0.01)
		still.append(ghost)
	_ghosts = still
