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
## The marble bounces: it leaves the hand at `FxMotion.BOLA_HAND_Y` m and arcs `FxMotion.BOLA_ARC` m over each bounce
## segment, its bottom touching the street exactly at each bounce, where it squashes (`BOUNCE_S`, then `BOUNCE_EASE_S` the
## next frame); it rolls along the street and stretches in the air (to its speed's stretch at contact, 1 at the top). A
## team ring (rim only, `BOLA_RING_ALPHA`) marks each bounce point still to come and flashes when it is reached
## (`BOUNCE_FLASH_ALPHA`, rising `BOUNCE_FLASH_RISE` frames, fading `BOUNCE_FLASH_FADE`), with `BOUNCE_DUST` dust. After
## the last bounce it runs on `BOLA_RUNOUT` m and shrinks away over `RUNOUT_FRAMES` frames.
const BOUNCE_S: float = 0.7
const BOUNCE_EASE_S: float = 0.85
const BOLA_RING_ALPHA: float = 0.5
const BOUNCE_FLASH_ALPHA: float = 0.3
const BOUNCE_FLASH_RISE: int = 2
const BOUNCE_FLASH_FADE: int = 6
const BOUNCE_DUST: int = 6
const BOLA_RUNOUT: float = 0.5
const RUNOUT_FRAMES: int = 4
## The spinning top stands on its tip on the street (`TOP_SPIN` turns a second) and wobbles (FxMotion.top_tilt). It drops
## in from `TOP_DROP` m over `TOP_DROP_FRAMES` frames and lands squashed (`TOP_LAND_S`) with `TOP_LAND_DUST` dust; while it
## moves it leaves a short dust trail. A kid it hits makes it squash (`TOP_LAND_S`) and its lean kick up; the kid is thrown
## up off `TOP_LAUNCH_DUST` dust. At the end it leans over (`TOP_END_TILT` degrees, then down to `TOP_FALL_TILT`) slowing
## its spin, and shrinks away over `TOP_END_FRAMES` frames with `TOP_END_DUST` dust. A top reacts to a kid within its hit
## radius plus `SLAP_REACH` m.
const TOP_DROP: float = 0.6
const TOP_DROP_FRAMES: int = 2
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
	for rec: Rec in _recs.values():
		if rec.kind == &"top" and rec.team != team and rec.track.last_pos.distance_to(at) <= rec.def.radius + kid_radius + SLAP_REACH and not _ctx.reduced:
			# the top bumps into the kid: it squashes and its lean kicks up
			rec.spiked = _ctx.time
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
		rec.tilt.basis = _toward_camera(deg_to_rad(SLIPPER_TILT))
	add_child(rec.holder)
	_recs[rec.id] = rec
	_ctx.shadows.attach(rec.id, drawn_width(rec.def) * (TOP_SHADOW if rec.kind == &"top" else 1.0), rec.team)
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
		&"marble":
			_marble(rec, projectile)
		&"top":
			_top(rec, projectile)
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


## The marble bounces down the street: arcs between the bounce points, touches down on each, rolls and squashes.
func _marble(rec: Rec, projectile: WeaponSystem.Projectile) -> void:
	var radius: float = drawn_width(rec.def) / 2.0
	var segment: float = rec.def.max_range / float(maxi(rec.def.count, 1))
	var dir: Vector3 = _dir3(rec, projectile)
	if rec.origin == Vector2.INF and dir != Vector3.ZERO:
		rec.origin = _bola_origin(rec, projectile)
		_bola_rings(rec, segment)
	var travelled: float = projectile.travelled if projectile.direction != Vector2.ZERO else (rec.track.last_pos.distance_to(rec.origin) if rec.origin != Vector2.INF else 0.0)
	var seg: int = projectile.bounces if projectile.direction != Vector2.ZERO else mini(floori(travelled / segment), rec.def.count - 1)
	if seg > rec.seg:
		for i: int in range(rec.seg, seg):
			_bounce(rec, i, segment)
		rec.seg = seg
		rec.touched = _ctx.time
	var p: float = clampf((travelled - float(seg) * segment) / segment, 0.0, 1.0)
	if _ctx.reduced:
		rec.holder.position.y = radius
		rec.squash.basis = Basis.IDENTITY
		return
	var since: float = _ctx.time - rec.touched
	var height: float = FxMotion.bola_height(seg, p, FxMotion.BOLA_HAND_Y, radius)
	if rec.touched >= 0.0 and since < FxSquash.FRAME * 0.5:
		# the bounce: squashed flat on the street, bottom exactly on it
		rec.squash.basis = Basis.from_scale(FxSquash.along(BOUNCE_S))
		height = radius * BOUNCE_S
	elif rec.touched >= 0.0 and since < FxSquash.FRAME * 1.5:
		rec.squash.basis = Basis.from_scale(FxSquash.along(BOUNCE_EASE_S))
		height = maxf(height, radius * BOUNCE_EASE_S)
	else:
		# stretched along its velocity, most at the street, none at the top of the arc
		var rise: float = FxMotion.BOLA_ARC * 4.0 * (1.0 - 2.0 * p) - (FxMotion.BOLA_HAND_Y - radius if seg == 0 else 0.0)
		var velocity: Vector3 = dir * rec.def.speed + Vector3.UP * rise * rec.def.speed / segment
		var stretch: float = lerpf(1.0, FxSquash.velocity_stretch(rec.def.speed), absf(1.0 - 2.0 * p))
		rec.squash.basis = FxSquash.stretch_basis(velocity, stretch)
	# it pops out of the hand (not a full-size ball over the thrower)
	var pop: float = FxSquash.pop((_ctx.time - rec.born) / (FxSquash.POP_FRAMES * FxSquash.FRAME))
	if pop < 1.0 or _ctx.time - rec.born < FxSquash.POP_FRAMES * FxSquash.FRAME:
		rec.squash.basis = rec.squash.basis.scaled(Vector3.ONE * maxf(pop, 0.01))
	rec.holder.position.y = height
	var axis: Vector3 = Vector3.UP.cross(dir)
	if axis.length_squared() > 0.0001:
		rec.spin_angle += _ctx.delta * rec.def.speed / radius
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
	elif not rec.landed:
		rec.landed = true
		_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(projectile.position.x, 0.0, projectile.position.y), TOP_LAND_DUST)
		rec.trail = _ctx.particles.trail_take()
	if age >= drop and age < drop + FxSquash.IMPACT_FRAMES * FxSquash.FRAME:
		shape = FxSquash.along(TOP_LAND_S)
	var spike_age: float = _ctx.time - rec.spiked if rec.spiked >= 0.0 else -1.0
	if spike_age >= 0.0 and spike_age < FxSquash.IMPACT_FRAMES * FxSquash.FRAME:
		shape = FxSquash.along(TOP_LAND_S)
	rec.squash.basis = FxMotion.top_tilt(_ctx.time, spike_age) * Basis.from_scale(shape)
	if rec.trail != null:
		rec.trail.global_position = Vector3(projectile.position.x, 0.05, projectile.position.y)


## A status effect landed on kid `id`: thrown up by a top, the kid leaves a puff of dust.
func on_effect(id: int, type: int) -> void:
	if type != StatusEffects.Type.AIRBORNE or _ctx.sim == null or not _ctx.sim.players.has(id):
		return
	var at: Vector2 = _ctx.sim.players[id].position
	for rec: Rec in _recs.values():
		if rec.kind == &"top" and rec.track.last_pos.distance_to(at) <= rec.def.radius + SLAP_REACH + 1.0:
			_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(at.x, 0.0, at.y), TOP_LAUNCH_DUST)
			return


## Where the marble was thrown from: offline exactly (its position less its travel), online the thrower at the cast, else
## where it was first seen.
func _bola_origin(rec: Rec, projectile: WeaponSystem.Projectile) -> Vector2:
	if projectile.direction != Vector2.ZERO:
		return projectile.position - projectile.direction * projectile.travelled
	var origin: Variant = _ctx.cast_origin(rec.def.id, rec.team)
	return origin as Vector2 if origin != null else rec.track.first_pos


## A team ring (rim only, the real bounce radius) on every bounce point still to come: the aim preview's nominal points.
func _bola_rings(rec: Rec, segment: float) -> void:
	var dir: Vector2 = rec.track.dir if rec.track.dir != Vector2.ZERO else Vector2.ZERO
	if _ctx.sim != null:
		for projectile: WeaponSystem.Projectile in _ctx.sim.weapons.projectiles:
			if projectile.id == rec.id and projectile.direction != Vector2.ZERO:
				dir = projectile.direction
	for i: int in rec.def.count:
		var at: Vector2 = rec.origin + dir * segment * float(i + 1)
		var ring: MeshInstance3D = MeshInstance3D.new()
		ring.name = "BounceRing"
		ring.mesh = FxGround.circle_mark(rec.def.radius, _ctx.team_color(rec.team), 0.0, BOLA_RING_ALPHA)
		ring.material_override = FxKit.overlay(FxKit.PRIO_FILL)
		ring.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		ring.position = Vector3(at.x, 0.0, at.y)
		add_child(ring)
		rec.rings.append(ring)


## Bounce `i` of the marble landed: its ring goes with a flash (the hit area, exact) and a puff of dust.
func _bounce(rec: Rec, i: int, _segment: float) -> void:
	if i >= rec.rings.size() or rec.rings[i] == null:
		return
	var ring: MeshInstance3D = rec.rings[i]
	var at: Vector2 = Vector2(ring.position.x, ring.position.z)
	_ctx.ground.flash_disc(at, rec.def.radius, _ctx.team_color(rec.team), BOUNCE_FLASH_ALPHA, BOUNCE_FLASH_RISE * FxSquash.FRAME, BOUNCE_FLASH_FADE * FxSquash.FRAME)
	_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(at.x, 0.0, at.y), BOUNCE_DUST)
	ring.queue_free()
	rec.rings[i] = null


## The bounce rings still on the ground (tests).
func bounce_rings() -> int:
	var live: int = 0
	for rec: Rec in _recs.values():
		for ring: MeshInstance3D in rec.rings:
			live += 1 if ring != null else 0
	return live


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
	if rec.kind == &"marble":
		_marble_gone(rec)
		return
	if rec.kind == &"top":
		if rec.trail != null:
			_ctx.particles.trail_give(rec.trail)
		if _ctx.reduced:
			rec.holder.queue_free()
			return
		# the end of its run: it leans over, slows, tips and is gone
		_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(rec.holder.position.x, 0.0, rec.holder.position.z), TOP_END_DUST)
		var ghost: Ghost = _ghost(rec.holder, &"tip_over", TOP_END_FRAMES * FxSquash.FRAME)
		ghost.spin = rec.spin_angle
		ghost.to = Vector3(rec.track.dir.x, 0.0, rec.track.dir.y)
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


## The marble's last bounce landed (the sim is done with it): that ring flashes, and the marble squashes on the street,
## runs on a little and shrinks away.
func _marble_gone(rec: Rec) -> void:
	for i: int in rec.rings.size():
		_bounce(rec, i, 0.0)
	if _ctx.reduced:
		rec.holder.queue_free()
		return
	var radius: float = drawn_width(rec.def) / 2.0
	var dir: Vector3 = Vector3(rec.track.dir.x, 0.0, rec.track.dir.y)
	rec.holder.position.y = radius * BOUNCE_S
	rec.squash.basis = Basis.from_scale(FxSquash.along(BOUNCE_S))
	var ghost: Ghost = _ghost(rec.holder, &"runout", RUNOUT_FRAMES * FxSquash.FRAME)
	ghost.from = rec.holder.position
	ghost.to = rec.holder.position + dir * BOLA_RUNOUT + Vector3(0.0, radius * (1.0 - BOUNCE_S), 0.0)


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
