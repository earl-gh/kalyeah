class_name FxSlippers
extends RefCounted
## The slippers of tsinelas, owned by FxProjectiles: they frisbee out tipped toward the camera, hang and turn back spinning
## the other way, slap the kids they hit (a stretch and a smack streak), and shrink into the thrower's hand on the catch.

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



var _ctx: FxContext
var _projectiles: FxProjectiles


func setup(ctx: FxContext, projectiles: FxProjectiles) -> void:
	_ctx = ctx
	_projectiles = projectiles


## The nearest slipper of the other team close enough to have hit a kid of `team` (radius `kid_radius`) at `at` slaps:
## it stretches on the hit and a white smack streak crosses its path.
func slap(team: int, kid_radius: float, at: Vector2) -> void:
	var best: FxProjectiles.Rec = null
	var best_d: float = INF
	for rec: FxProjectiles.Rec in _projectiles.recs():
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

## The slipper frisbees out, hangs and turns back spinning the other way, and slaps what it hits.
func pose(rec: FxProjectiles.Rec, projectile: WeaponSystem.Projectile) -> void:
	if _ctx.reduced:
		rec.squash.basis = Basis.IDENTITY
		# a fixed tilt pointing along its travel
		var still: Vector3 = _projectiles.dir3(rec, projectile)
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
	var dir: Vector3 = _projectiles.dir3(rec, projectile)
	var pop: float = FxSquash.pop((_ctx.time - rec.born) / (FxSquash.POP_FRAMES * FxSquash.FRAME))
	var shape: Basis = FxSquash.stretch_basis(dir, FxSquash.velocity_stretch(rec.def.speed))
	if _ctx.time < rec.react_until:
		shape = Basis.from_scale(FxSquash.along(HANG_S)) if rec.react == &"hang" else FxSquash.stretch_basis(dir, SLAP_S)
	rec.squash.basis = shape.scaled(Vector3.ONE * maxf(pop, 0.01))


## A slipper left the sim: caught near its thrower it shrinks into the hand; dropped anywhere else it puffs dust.
func gone(rec: FxProjectiles.Rec) -> void:
	var at: Vector3 = rec.holder.position
	var thrower: PlayerState = _thrower(rec)
	var reach: float = (_ctx.sim.rules.boomerang_catch_distance if _ctx.sim != null else 0.8) + CATCH_SLACK
	var ground: Vector3 = Vector3(at.x, 0.0, at.z)
	if thrower != null and thrower.position.distance_to(rec.track.last_pos) <= reach and not thrower.tagged:
		_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(thrower.position.x, 0.0, thrower.position.y), CATCH_DUST)
		if _ctx.reduced:
			rec.holder.queue_free()
			return
		var ghost: FxProjectiles.Ghost = _projectiles.ghost(rec.holder, &"catch", CATCH_FRAMES * FxSquash.FRAME)
		ghost.from = at
		ghost.to = Vector3(thrower.position.x, HAND_Y * _ctx.view_scale, thrower.position.y)
		return
	_ctx.particles.puff(FxParticles.Preset.DUST, ground, DROP_DUST)
	rec.holder.queue_free()


## Who threw `rec`: offline the sim's owner, online the nearest kid of its team (snapshots carry no owner).
func _thrower(rec: FxProjectiles.Rec) -> PlayerState:
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
