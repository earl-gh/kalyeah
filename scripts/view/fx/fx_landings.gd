class_name FxLandings
extends RefCounted
## The delayed landings of FxZones: the rock meteor that falls from up and behind onto its circle and lands with a squash,
## dust and cracks (bato heavy), and the can tossed from the thrower's hand onto its circle that lands upright (lata).
## FxZones owns the zones and their nodes; this poses the falling objects and plays their landing.

## The meteor of a rock-meteor circle starts this far off its circle, up and behind (it falls in on an ease-in curve, so it
## speeds up, and never crosses in front of a face), tumbling `METEOR_TUMBLE` turns on the way down.
const METEOR_FROM: Vector3 = FxMotion.METEOR_FROM
const METEOR_TUMBLE: float = 1.5
## The meteor's shadow grows from `METEOR_SHADOW_FROM` of its size to whole as it falls (the "it lands here" cue).
const METEOR_SHADOW_FROM: float = 0.3
## From this far through the fall it glints once (two little stars).
const METEOR_GLINT_AT: float = 12.0 / 18.0
const GLINT_STARS: int = 2
## On its last `FxSquash.SMEAR_FRAMES` frames, its fastest, the meteor is smeared along its fall (FxSquash.smear_stretch) with a
## white trail up its path (each frame's, `METEOR_TRAIL_FRAMES` frames long); before that it is crisp.
const METEOR_TRAIL_FRAMES: int = 5
## The trail is this share of the meteor's width across.
const METEOR_TRAIL_WIDTH: float = 1.0
## The landing: the meteor squashes flat (`METEOR_IMPACT_S`) for `FxSquash.IMPACT_FRAMES` frames, springs back through an
## overshoot to its size (`FxSquash.landing`), a ring of dust is thrown out (`IMPACT_DUST` particles) and dark cracks reach
## `IMPACT_CRACK` of the circle's radius, fading over `CRACK_TIME` s.
const METEOR_IMPACT_S: float = 0.65
const IMPACT_DUST: int = 12
const IMPACT_CRACK: float = 0.8
## Soil crumbs on the landing: `FxParticles.CRUMB_COUNT` at the impact point and `METEOR_RIM_CRUMBS` in each of `METEOR_RIM_SPOTS`
## spots round the circle's rim.
const METEOR_RIM_SPOTS: int = 4
const METEOR_RIM_CRUMBS: int = 2
const CRACK_TIME: float = 0.5
## The tossed can (lata) flies from the thrower's hand (`CAN_HAND_Y` m) to the circle's centre, arcing `CAN_PEAK` m plus
## `CAN_PEAK_PER_M` per metre thrown over the straight line, turning `CAN_TURNS` times end over end; it lands upright on its
## base, squashed (`CAN_LAND_S`) for `CAN_LAND_FRAMES` frames then springing back through an overshoot to its size, with `CAN_DUST`
## dust. It is smeared along its velocity on its first and last `FxSquash.SMEAR_FRAMES` frames and crisp over the top of the arc.
## Online its flight is estimated from the cast's position and speed, `ONLINE_LEAD` s early (the zone is seen late).
const CAN_HAND_Y: float = 2.2
const CAN_PEAK: float = 2.2
const CAN_PEAK_PER_M: float = 0.25
const CAN_TURNS: float = 1.5
const CAN_LAND_S: float = 0.7
const CAN_LAND_FRAMES: int = 2
const CAN_DUST: int = 6
const ONLINE_LEAD: float = 0.05
## The can's flight bows `CAN_BOW` m to the side at its middle (back on the line at both ends): thrown up the screen, a
## straight arc ending at the target's feet would cross the target's face on screen; the bow passes beside the kid and
## comes in low. Decoration only: the circle (the sim's) never moves.
const CAN_BOW: float = 1.2



var _ctx: FxContext
var _zones: FxZones


func setup(ctx: FxContext, zones: FxZones) -> void:
	_ctx = ctx
	_zones = zones

## The can flies from the thrower's hand to the middle of its circle, tumbling end over end and stretched along its flight,
## its shadow running along the ground under it; it lands upright on its base as the zone lands.
func can(rec: FxZones.Rec, zone: WeaponSystem.Zone) -> void:
	var p: float = _zones.progress(zone, rec)
	var rest: float = can_height() / 2.0
	var to: Vector3 = Vector3(0.0, rest, 0.0)
	var from: Vector3 = Vector3(0.0, CAN_HAND_Y, 0.0)
	var peak: float = 0.0
	var dir: Vector3 = Vector3.ZERO
	if rec.origin != Vector2.INF and rec.origin.distance_to(zone.center) > 0.01:
		var offset: Vector2 = rec.origin - zone.center
		from = Vector3(offset.x, CAN_HAND_Y, offset.y)
		peak = CAN_PEAK + CAN_PEAK_PER_M * offset.length()
		dir = Vector3(-offset.x, 0.0, -offset.y).normalized()
	rec.obj.position = FxMotion.toss(from, to, peak, p)
	if dir != Vector3.ZERO:
		rec.obj.position += dir.cross(Vector3.UP).normalized() * CAN_BOW * sin(PI * p)
	var ground: Vector2 = zone.center + Vector2(rec.obj.position.x, rec.obj.position.z)
	if _ctx.shadows.attach(rec.id, can_height(), rec.team):
		_ctx.shadows.place(rec.id, ground, rec.obj.position.y)
	var squash: Node3D = rec.obj.get_node("Squash") as Node3D
	if _ctx.reduced or p >= 1.0:
		squash.basis = Basis.IDENTITY
		(squash.get_node("Spin") as Node3D).basis = Basis.IDENTITY
		return
	var velocity: Vector3 = (to - from) + Vector3(0.0, peak * 4.0 * (1.0 - 2.0 * p), 0.0)
	var flight: float = maxf(rec.total - FxSquash.FRAME, FxZones.MIN_TOTAL)
	var frames: float = minf(p, 1.0 - p) * flight / FxSquash.FRAME
	squash.basis = FxSquash.smear_basis(velocity, FxSquash.smear_stretch(velocity.length() / flight, can_height(), frames))
	var axis: Vector3 = dir.cross(Vector3.UP) if dir != Vector3.ZERO else Vector3.RIGHT
	(squash.get_node("Spin") as Node3D).basis = Basis(axis.normalized(), CAN_TURNS * TAU * p)


## How tall the tossed can is drawn (metres).
func can_height() -> float:
	return (WeaponModel.OBJECTS[&"lata"][1] as float) * _ctx.view_scale


## The rock meteor falls from up and behind onto its circle (ease-in, so it speeds up), tumbling, its shadow growing at the
## centre; its bottom touches the street on the last frame before the landing and never goes below it.
func meteor(rec: FxZones.Rec, zone: WeaponSystem.Zone) -> void:
	var p: float = _zones.progress(zone, rec)
	var radius: float = WeaponModel.METEOR_SIZE / 2.0
	rec.obj.position = FxMotion.meteor_offset(p) + Vector3(0.0, radius, 0.0)
	if _ctx.shadows.attach(rec.id, WeaponModel.METEOR_SIZE, rec.team):
		_ctx.shadows.place(rec.id, zone.center, rec.obj.position.y, lerpf(METEOR_SHADOW_FROM, 1.0, p))
	if _ctx.reduced:
		return
	var fall: Vector3 = (-METEOR_FROM).normalized()
	var axis: Vector3 = fall.cross(Vector3.UP).normalized()
	(rec.obj.get_node("Squash/Spin") as Node3D).basis = Basis(axis, METEOR_TUMBLE * TAU * p)
	# its fastest frames, the last ones: smeared along the fall with a trail up its path; crisp before
	var flight: float = maxf(rec.total - FxSquash.FRAME, FxZones.MIN_TOTAL)
	var frames: float = (1.0 - p) * flight / FxSquash.FRAME
	var speed: float = METEOR_FROM.length() * 2.0 * p / flight
	var stretch: float = FxSquash.smear_stretch(speed, WeaponModel.METEOR_SIZE, frames)
	(rec.obj.get_node("Squash") as Node3D).basis = FxSquash.smear_basis(fall, stretch)
	if stretch > 1.0 and p < 1.0:
		FxRelease.behind(_ctx, rec.node.position + rec.obj.position, fall, speed, METEOR_TRAIL_FRAMES, WeaponModel.METEOR_SIZE * METEOR_TRAIL_WIDTH, FxRelease.TINT_METEOR)
	if p >= METEOR_GLINT_AT and not rec.glinted:
		rec.glinted = true
		var at: Vector3 = rec.node.position + rec.obj.position
		_ctx.particles.puff(FxParticles.Preset.STARS, at - Vector3(0.0, FxParticles.spec(FxParticles.Preset.STARS).points[0].y, 0.0), GLINT_STARS)


## A zone left the sim: a landing plays its impact (the meteor) or its touch-down (the can); the rest just go.
func land(rec: FxZones.Rec) -> void:
	if rec.def != null and rec.def.id == &"bato_heavy" and rec.kind == WeaponSystem.ZoneKind.EXPLOSION:
		var center: Vector2 = Vector2(rec.node.position.x, rec.node.position.z)
		_ctx.ground.crack(center, 0.0, &"spokes", Vector2.ONE * rec.def.radius * IMPACT_CRACK, CRACK_TIME)
		if not _ctx.reduced:
			# it lands: squashed flat on the street for two frames, a ring of dust, then it is gone
			var meteor: Node3D = rec.obj
			var at: Vector3 = meteor.global_position
			rec.node.remove_child(meteor)
			_zones.add_child(meteor)
			meteor.position = Vector3(at.x, WeaponModel.METEOR_SIZE / 2.0 * METEOR_IMPACT_S, at.z)
			(meteor.get_node("Squash") as Node3D).basis = Basis.from_scale(FxSquash.along(METEOR_IMPACT_S))
			_zones.ghost(meteor, &"meteor", (FxSquash.landing_frames() + 1) * FxSquash.FRAME)
			_ctx.particles.puff(FxParticles.Preset.IMPACT, Vector3(center.x, 0.0, center.y), IMPACT_DUST)
			_ctx.particles.puff(FxParticles.Preset.CRUMBS, Vector3(center.x, 0.0, center.y), FxParticles.CRUMB_COUNT)
			for i: int in METEOR_RIM_SPOTS:
				var angle: float = TAU * (float(i) + 0.5) / float(METEOR_RIM_SPOTS)
				var rim: Vector2 = center + Vector2(cos(angle), sin(angle)) * rec.def.radius
				_ctx.particles.puff(FxParticles.Preset.CRUMBS, Vector3(rim.x, 0.0, rim.y), METEOR_RIM_CRUMBS)
	elif rec.def != null and rec.def.id == &"lata" and rec.obj != null and not _ctx.reduced:
		# the can lands upright: squash, stretch, then it is gone (it "became" the kids it hit)
		var can: Node3D = rec.obj
		var at: Vector3 = can.global_position
		rec.node.remove_child(can)
		_zones.add_child(can)
		can.position = Vector3(at.x, 0.0, at.z)
		var center: Vector2 = Vector2(at.x, at.z)
		_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(center.x, 0.0, center.y), CAN_DUST)
		_zones.ghost(can, &"can", (FxSquash.landing_frames(CAN_LAND_FRAMES) + 1) * FxSquash.FRAME)
