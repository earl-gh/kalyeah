class_name FxMarble
extends RefCounted
## The marble of bola, owned by FxProjectiles: it arcs over each bounce segment and touches the street exactly at each
## bounce point (squash and dust), rolls and stretches in the air; a team ring marks each bounce point still to come and
## flashes when it is reached; after the last bounce it runs on a little and shrinks away.

## It leaves the hand (at most a bounce's height up) and arcs `FxMotion.BOLA_ARC` m over each bounce
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



var _ctx: FxContext
var _projectiles: FxProjectiles


func setup(ctx: FxContext, projectiles: FxProjectiles) -> void:
	_ctx = ctx
	_projectiles = projectiles

## The marble bounces down the street: arcs between the bounce points, touches down on each, rolls and squashes.
func pose(rec: FxProjectiles.Rec, projectile: WeaponSystem.Projectile) -> void:
	var radius: float = _projectiles.drawn_width(rec.def) / 2.0
	var segment: float = rec.def.max_range / float(maxi(rec.def.count, 1))
	var dir: Vector3 = _projectiles.dir3(rec, projectile)
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
		# smeared along its velocity on the frames next to the street (fastest), crisp over the top of the arc
		var rise: float = FxMotion.bola_rise(seg, p, FxMotion.BOLA_HAND_Y, radius)
		var velocity: Vector3 = dir * rec.def.speed + Vector3.UP * rise * rec.def.speed / segment
		var frames: float = minf(p, 1.0 - p) * segment / rec.def.speed / FxSquash.FRAME
		rec.squash.basis = FxSquash.smear_basis(velocity, FxSquash.smear_stretch(velocity.length(), radius * 2.0, frames))
	# it pops out of the hand (not a full-size ball over the thrower)
	var pop: float = FxSquash.pop((_ctx.time - rec.born) / (FxSquash.POP_FRAMES * FxSquash.FRAME))
	if pop < 1.0 or _ctx.time - rec.born < FxSquash.POP_FRAMES * FxSquash.FRAME:
		rec.squash.basis = rec.squash.basis.scaled(Vector3.ONE * maxf(pop, 0.01))
	rec.holder.position.y = height
	var axis: Vector3 = Vector3.UP.cross(dir)
	if axis.length_squared() > 0.0001:
		rec.spin_angle += _ctx.delta * rec.def.speed / radius
		rec.spin.basis = Basis(axis.normalized(), rec.spin_angle)


## Where the marble was thrown from: offline exactly (its position less its travel), online the thrower at the cast, else
## where it was first seen.
func _bola_origin(rec: FxProjectiles.Rec, projectile: WeaponSystem.Projectile) -> Vector2:
	if projectile.direction != Vector2.ZERO:
		return projectile.position - projectile.direction * projectile.travelled
	var origin: Variant = _ctx.cast_origin(rec.def.id, rec.team)
	return origin as Vector2 if origin != null else rec.track.first_pos


## A team ring (rim only, the real bounce radius) on every bounce point still to come: the aim preview's nominal points.
func _bola_rings(rec: FxProjectiles.Rec, segment: float) -> void:
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
		_projectiles.add_child(ring)
		rec.rings.append(ring)


## Bounce `i` of the marble landed: its ring goes with a flash (the hit area, exact) and a puff of dust.
func _bounce(rec: FxProjectiles.Rec, i: int, _segment: float) -> void:
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
	for rec: FxProjectiles.Rec in _projectiles.recs():
		for ring: MeshInstance3D in rec.rings:
			live += 1 if ring != null else 0
	return live


## The marble's last bounce landed (the sim is done with it): that ring flashes, and the marble squashes on the street,
## runs on a little and shrinks away.
func gone(rec: FxProjectiles.Rec) -> void:
	for i: int in rec.rings.size():
		_bounce(rec, i, 0.0)
	if _ctx.reduced:
		rec.holder.queue_free()
		return
	var radius: float = _projectiles.drawn_width(rec.def) / 2.0
	var dir: Vector3 = Vector3(rec.track.dir.x, 0.0, rec.track.dir.y)
	rec.holder.position.y = radius * BOUNCE_S
	rec.squash.basis = Basis.from_scale(FxSquash.along(BOUNCE_S))
	var ghost: FxProjectiles.Ghost = _projectiles.ghost(rec.holder, &"runout", RUNOUT_FRAMES * FxSquash.FRAME)
	ghost.from = rec.holder.position
	ghost.to = rec.holder.position + dir * BOLA_RUNOUT + Vector3(0.0, radius * (1.0 - BOUNCE_S), 0.0)
