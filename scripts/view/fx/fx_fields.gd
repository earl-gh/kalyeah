class_name FxFields
extends RefCounted
## The placed fields of FxZones: the paper trap (tossed onto its spot, arming, breathing, springing into its slow zone),
## the slow zone (its fill popping out, fading at its end) and the jacks field (sprayed from the hand, hopping on a tick,
## sinking away). FxZones owns the zones and their nodes; this only poses them.

## The paper trap is tossed from the thrower's hand (`FxLandings.CAN_HAND_Y` m) over `TOSS_TIME` s, arcing `TOSS_PEAK` m and turning
## once, is smeared along its toss on the fast frames (FxSquash.smear_stretch), lands squashed (`FxSquash.IMPACT_S`, springing
## back through an overshoot to 1: FxSquash.landing) and, armed, breathes (`BREATHE_S` to 1 at `BREATHE_HZ`). When a kid trips it
## it springs (`SPRING_S` for `FxSquash.IMPACT_FRAMES` frames, then back through a squash to 1) into `PAPER_BURST` paper bits and `SCRAPS` scraps, and the slow
## zone's fill pops out over `FILL_POP_FRAMES` frames. In the last `SLOW_FADE` s of the slow zone its rim fades to
## `FADE_RIM` of itself and its fill to nothing.
const TOSS_TIME: float = 0.2
const TOSS_PEAK: float = 1.5
const BREATHE_S: float = 0.94
const BREATHE_HZ: float = 0.8
const SPRING_S: float = 1.35
const PAPER_BURST: int = 8
const SCRAPS: int = 6
const FILL_POP_FRAMES: int = 4
const SLOW_FADE: float = 0.3
const FADE_RIM: float = 0.42
## The jacks are dropped from the thrower's hand onto the middle of their field (`JACK_FLIGHT_FRAMES` frames, `JACK_PEAK` m high), landing
## squashed (`JACK_LAND_S`), and then scatter by ROLLING outward over the street: each of the six rolls once to its spot, except `JACK_TWICE`
## of them (picked at random for each field) that roll twice, to a stop on the way and then on to their spots. A roll takes `JACK_ROLL_FRAMES`
## frames (each of a double roller's two `JACK_ROLL_AGAIN` of that) after a random wait of up to `JACK_ROLL_WAIT` frames, and turns the jack
## over whole times (so it ends lying as it began), a little off the street (`JACK_ROLL_LIFT` of its size). It is smeared along the roll on
## its fast first frames, leaves a streak on the street (`JACK_STREAK` m wide) and squashes where it stops (owner: no dust or smoke, only the
## smears). Every spot, and every stop on the way, is inside the field's circle (the slow and damage area). A tick that hurts a kid makes the
## nearest cluster hop `JACK_HOP` m for a frame. In the last `SLOW_FADE` s they sink `JACK_SINK` m and shrink to `JACK_SHRINK` while the fill fades.
## How many of the jacks of a field roll twice (the rest roll once).
const JACK_TWICE: int = 3
const JACK_FLIGHT_FRAMES: int = 6
const JACK_PEAK: float = 1.2
const JACK_LAND_S: float = 0.75
const JACK_ROLL_FRAMES: int = 12
const JACK_ROLL_AGAIN: float = 0.8
const JACK_ROLL_WAIT: int = 4
const JACK_ROLL_PAUSE: int = 3
const JACK_ROLL_LIFT: float = 0.3
const JACK_STREAK: float = 0.1
const JACK_STREAK_LIFE: int = 6
## A jack rolls one turn over per this many of its own widths (at least one turn).
const JACK_TURN_WIDTHS: float = 2.4
## The height of the middle of a jack of one unit lying on the street (about the hub of the model), which it rolls about.
const JACK_CENTRE: float = 0.43
## The pile on the middle: each jack lies this far (of its width) out from the very middle toward its spot, so they do not hide one another.
const JACK_PILE: float = 0.2
## The second roller's stop on the way is this part (min, max) of the way out to its spot, turned off the straight line by up to `JACK_MID_TURN` rad.
const JACK_MID_OUT: Vector2 = Vector2(0.3, 0.55)
const JACK_MID_TURN: float = 0.9
const JACK_HOP: float = 0.1
const JACK_SINK: float = 0.3
const JACK_SHRINK: float = 0.4
const JACK_STREAK_TINT: Color = Color(1.0, 0.97, 0.9, 0.8)



var _ctx: FxContext
var _zones: FxZones


func setup(ctx: FxContext, zones: FxZones) -> void:
	_ctx = ctx
	_zones = zones


## A kid was hit this frame at `at`: inside an enemy jacks field, the nearest cluster hops (the tick).
func on_tick_hit(kid_id: int, at: Vector2) -> void:
	if _ctx.reduced:
		return
	var team: int = _ctx.team_of(kid_id)
	for rec: FxZones.Rec in _zones.recs():
		if rec.kind != WeaponSystem.ZoneKind.FIELD or rec.team == team or rec.clusters.is_empty():
			continue
		var center: Vector2 = Vector2(rec.node.position.x, rec.node.position.z)
		if center.distance_to(at) > rec.def.radius + 1.0:
			continue
		var nearest: int = 0
		for i: int in rec.homes.size():
			var home: Vector2 = center + Vector2(rec.homes[i].x, rec.homes[i].z)
			if home.distance_to(at) < (center + Vector2(rec.homes[nearest].x, rec.homes[nearest].z)).distance_to(at):
				nearest = i
		rec.hops[nearest] = _ctx.time


## How far a trap is through its arming (0 at the cast, 1 armed): offline its arming time left, online its age.
func arming(zone: WeaponSystem.Zone, rec: FxZones.Rec) -> float:
	var arm: float = maxf(zone.def.arm_time if zone.def != null else 0.0, 0.001)
	if zone.timer > 0.0 or zone.time_left > 0.0:
		return clampf(1.0 - zone.timer / arm, 0.0, 1.0)
	return clampf(rec.track.age(_ctx.time) / arm, 0.0, 1.0)


## How tall the paper ball is drawn (metres).
func ball_height() -> float:
	return (WeaponModel.OBJECTS[&"papel_trap"][1] as float) * _ctx.view_scale


## Seconds a zone of `total` seconds has left: offline the sim's, online from its age.
func _left(zone: WeaponSystem.Zone, rec: FxZones.Rec, total: float) -> float:
	return zone.time_left if zone.time_left > 0.0 else total - rec.track.age(_ctx.time)


## The paper ball is tossed onto its spot, lands with a squash and, armed, breathes.
func trap(rec: FxZones.Rec, zone: WeaponSystem.Zone) -> void:
	var half: float = ball_height() / 2.0
	var squash: Node3D = rec.obj.get_node("Squash") as Node3D
	var spin: Node3D = squash.get_node("Spin") as Node3D
	var age: float = rec.track.age(_ctx.time)
	rec.obj.position = Vector3(0.0, half, 0.0)
	if _ctx.reduced:
		squash.basis = Basis.IDENTITY
		spin.basis = Basis.IDENTITY
		return
	if rec.origin != Vector2.INF and age < TOSS_TIME:
		var offset: Vector2 = rec.origin - zone.center
		var p: float = age / TOSS_TIME
		rec.obj.position = FxMotion.toss(Vector3(offset.x, FxLandings.CAN_HAND_Y, offset.y), Vector3(0.0, half, 0.0), TOSS_PEAK, p)
		var dir: Vector3 = Vector3(-offset.x, 0.0, -offset.y)
		var axis: Vector3 = dir.cross(Vector3.UP) if dir.length_squared() > 0.0001 else Vector3.RIGHT
		spin.basis = Basis(axis.normalized(), TAU * p)
		var velocity: Vector3 = (Vector3(0.0, half, 0.0) - Vector3(offset.x, FxLandings.CAN_HAND_Y, offset.y)) + Vector3(0.0, TOSS_PEAK * 4.0 * (1.0 - 2.0 * p), 0.0)
		var frames: float = minf(age, TOSS_TIME - age) / FxSquash.FRAME
		squash.basis = FxSquash.smear_basis(velocity, FxSquash.smear_stretch(velocity.length() / TOSS_TIME, ball_height(), frames))
		return
	spin.basis = Basis.IDENTITY
	var landed: float = age - (TOSS_TIME if rec.origin != Vector2.INF else 0.0)
	var s: float = 1.0
	var settling: float = landed / FxSquash.FRAME
	if settling < FxSquash.landing_frames():
		s = FxSquash.landing(FxSquash.IMPACT_S, settling)
	elif rec.armed:
		s = lerpf(1.0, BREATHE_S, 0.5 - 0.5 * cos(TAU * BREATHE_HZ * age))
	squash.basis = Basis.from_scale(FxSquash.along(s))
	rec.obj.position.y = half * s


## A trap was tripped: its ball springs up, bursts into paper and scraps settle in the new slow zone.
func spring(rec: FxZones.Rec) -> void:
	if rec.kind != WeaponSystem.ZoneKind.TRAP or rec.obj == null:
		return
	var center: Vector3 = Vector3(rec.node.position.x, 0.0, rec.node.position.z)
	_ctx.particles.puff(FxParticles.Preset.PAPER, center, PAPER_BURST)
	_ctx.particles.puff(FxParticles.Preset.SCRAPS, center, SCRAPS)
	if _ctx.reduced:
		return
	var ball: Node3D = rec.obj
	var at: Vector3 = ball.global_position
	rec.node.remove_child(ball)
	_zones.add_child(ball)
	(ball.get_node("Squash") as Node3D).basis = Basis.from_scale(FxSquash.along(SPRING_S))
	ball.position = Vector3(at.x, ball_height() / 2.0 * SPRING_S, at.z)
	_zones.ghost(ball, &"spring", (FxSquash.landing_frames() + 1) * FxSquash.FRAME)


## The slow zone's fill pops out, and in its last moments the zone fades (its rim stays faintly until it is gone).
func slow(rec: FxZones.Rec, zone: WeaponSystem.Zone) -> void:
	var age: float = rec.track.age(_ctx.time)
	var pop: float = 1.0 if _ctx.reduced else clampf(age / (FILL_POP_FRAMES * FxSquash.FRAME), 0.0, 1.0)
	rec.fill.scale = Vector3(maxf(pop, FxZones.PROGRESS_START), 1.0, maxf(pop, FxZones.PROGRESS_START))
	var left: float = _left(zone, rec, zone.def.duration if zone.def != null else 0.0)
	var nodes: Array[MeshInstance3D] = [rec.mark, rec.fill]
	var ends: Array[float] = [FADE_RIM, 0.0]
	_fade(rec, left, nodes, ends)


## The last `SLOW_FADE` seconds of a zone with `left` seconds to go: each node's alpha goes from 1 to its end value.
func _fade(rec: FxZones.Rec, left: float, nodes: Array[MeshInstance3D], ends: Array[float]) -> void:
	if left > SLOW_FADE:
		return
	if rec.fades.is_empty():
		for node: MeshInstance3D in nodes:
			var fade: StandardMaterial3D = _ctx.ground.fade_take()
			if fade == null:
				return
			rec.fades.append(fade)
			node.material_override = fade
	var t: float = clampf(1.0 - left / SLOW_FADE, 0.0, 1.0)
	for i: int in rec.fades.size():
		rec.fades[i].albedo_color = Color(1.0, 1.0, 1.0, lerpf(1.0, ends[i], t))


## The jacks drop onto the middle of the field, then roll outward to their spots (some of them roll twice), hop on a tick and sink away at the end.
func field(rec: FxZones.Rec, zone: WeaponSystem.Zone) -> void:
	var age: float = rec.track.age(_ctx.time)
	var left: float = _left(zone, rec, zone.def.duration if zone.def != null else 0.0)
	var nodes: Array[MeshInstance3D] = [rec.mark]
	var ends: Array[float] = [FADE_RIM]
	_fade(rec, left, nodes, ends)
	var sink: float = 0.0 if _ctx.reduced else clampf(1.0 - left / SLOW_FADE, 0.0, 1.0)
	var centre: Vector3 = Vector3(rec.node.position.x, 0.0, rec.node.position.z)
	var frame: float = FxSquash.FRAME
	var flight: float = JACK_FLIGHT_FRAMES * frame if rec.origin != Vector2.INF else 0.0
	for i: int in rec.clusters.size():
		var cluster: Node3D = rec.clusters[i]
		var scale: float = rec.cluster_scale * lerpf(1.0, JACK_SHRINK, sink)
		var turn: Basis = Basis(Vector3.UP, rec.turns[i])
		var lower: Vector3 = Vector3(0.0, JACK_SINK * sink, 0.0)
		cluster.basis = turn * Basis.from_scale(Vector3.ONE * scale)
		cluster.position = rec.homes[i] - lower
		if _ctx.reduced:
			continue
		cluster.visible = true
		var pile: Vector3 = rec.piles[i]
		if age < flight:
			# the drop: the whole handful from the thrower's hand onto the middle
			var t: float = age / flight
			var offset: Vector2 = rec.origin - zone.center
			var from: Vector3 = Vector3(offset.x, FxLandings.CAN_HAND_Y, offset.y)
			cluster.position = FxMotion.toss(from, pile, JACK_PEAK, t)
			var velocity: Vector3 = (pile - from) + Vector3(0.0, JACK_PEAK * 4.0 * (1.0 - 2.0 * t), 0.0)
			var fast: float = minf(t, 1.0 - t) * JACK_FLIGHT_FRAMES
			cluster.basis = FxSquash.smear_basis(velocity, FxSquash.smear_stretch(velocity.length() / flight, WeaponModel.SPIKE_WIDTH, fast)) * cluster.basis
			continue
		var clock: float = age - flight
		var wait: float = (float(FxSquash.landing_frames()) + rec.delays[i]) * frame
		if clock < wait:
			# lying in the pile on the middle (landing squashed), until its own turn to roll
			cluster.position = pile - lower
			if rec.origin != Vector2.INF and clock / frame < float(FxSquash.landing_frames()):
				cluster.basis = turn * Basis.from_scale(FxSquash.along(FxSquash.landing(JACK_LAND_S, clock / frame)) * scale)
			continue
		var pose: Array = _roll_pose(rec, i, clock - wait, centre, scale, turn)
		cluster.position = (pose[0] as Vector3) - lower
		cluster.basis = pose[1] as Basis
		if rec.hops[i] >= 0.0 and _ctx.time - rec.hops[i] < frame:
			cluster.position.y += JACK_HOP


## Where jack `i` is, and how it is turned, `seconds` after its first roll began: [position, basis]. Starts the roll's streak and counts the
## stops, once each.
func _roll_pose(rec: FxZones.Rec, i: int, seconds: float, centre: Vector3, scale: float, turn: Basis) -> Array:
	var frame: float = FxSquash.FRAME
	var rolls: Array[Vector3] = [rec.piles[i]]
	if rec.twice.has(i):
		rolls.append(rec.mids[i])
	rolls.append(rec.homes[i])
	var segments: int = rolls.size() - 1
	var span: float = float(JACK_ROLL_FRAMES) * (JACK_ROLL_AGAIN if segments > 1 else 1.0) * frame
	var cursor: float = 0.0
	for k: int in segments:
		var from_spot: Vector3 = rolls[k]
		var to_spot: Vector3 = rolls[k + 1]
		var run: Vector3 = to_spot - from_spot
		if seconds < cursor + span:
			# rolling: out fast and slowing, turning over whole times about its middle, a little off the street
			if rec.starts[i] <= k:
				rec.starts[i] = k + 1
				_roll_streak(centre + from_spot, centre + to_spot)
			var p: float = clampf((seconds - cursor) / span, 0.0, 1.0)
			var eased: float = 1.0 - (1.0 - p) * (1.0 - p)
			var turns: float = maxf(1.0, roundf(run.length() / (JACK_TURN_WIDTHS * WeaponModel.SPIKE_WIDTH)))
			var around: Basis = Basis(run.normalized().cross(Vector3.UP).normalized(), TAU * turns * eased)
			var middle: Vector3 = Vector3(0.0, JACK_CENTRE * scale, 0.0)
			var at: Vector3 = from_spot.lerp(to_spot, eased) + middle - around * middle + Vector3(0.0, JACK_ROLL_LIFT * scale * sin(PI * p), 0.0)
			var speed: float = run.length() / span * 2.0 * (1.0 - p)
			var smear: Basis = FxSquash.smear_basis(run, FxSquash.smear_stretch(speed, WeaponModel.SPIKE_WIDTH, p * span / frame))
			return [at, smear * (around * turn * Basis.from_scale(Vector3.ONE * scale))]
		# the roll is over: a squash where it stops, a short pause, then the next roll
		if rec.stops[i] <= k:
			rec.stops[i] = k + 1
		var since: float = (seconds - cursor - span) / frame
		var last: bool = k == segments - 1
		if last or seconds < cursor + span + float(JACK_ROLL_PAUSE) * frame:
			var squash: Vector3 = FxSquash.along(FxSquash.landing(JACK_LAND_S, since)) if since < float(FxSquash.landing_frames()) else Vector3.ONE
			return [to_spot, turn * Basis.from_scale(squash * scale)]
		cursor += span + float(JACK_ROLL_PAUSE) * frame
	return [rolls[segments], turn * Basis.from_scale(Vector3.ONE * scale)]


## The streak a rolling jack leaves on the street from `from` to `to` (world): a thin warm-white band that fades in a few frames.
func _roll_streak(from: Vector3, to: Vector3) -> void:
	_ctx.ground.trail(from + Vector3(0.0, 0.04, 0.0), to + Vector3(0.0, 0.04, 0.0), JACK_STREAK, JACK_STREAK_TINT, float(JACK_STREAK_LIFE) * FxSquash.FRAME)


## How the jacks of field `id` scatter, the same on every screen (it only reads the zone's id): the piles they start on the middle, the one that
## roll twice (`JACK_TWICE` random ones among the `homes`) and their stops on the way, and each jack's wait (frames) before it rolls. Every point is inside the
## circle through the farthest home, so inside the slow and damage area.
static func scatter_plan(id: int, homes: Array[Vector3]) -> Dictionary:
	var piles: Array[Vector3] = []
	var mids: Array[Vector3] = []
	var delays: PackedFloat32Array = PackedFloat32Array()
	# three of the jacks (picked at random for each field) roll twice: the three with the lowest hash
	var order: Array[int] = []
	for index: int in homes.size():
		order.append(index)
	order.sort_custom(func(a: int, b: int) -> bool: return _unit(id, 100 + a) < _unit(id, 100 + b))
	var twice: Array[int] = []
	for pick: int in mini(JACK_TWICE, order.size()):
		twice.append(order[pick])
	for index: int in homes.size():
		var home: Vector3 = homes[index]
		var way: Vector2 = Vector2(home.x, home.z)
		var dir: Vector2 = way.normalized() if way.length() > 0.001 else Vector2.RIGHT
		piles.append(Vector3(dir.x, 0.0, dir.y) * JACK_PILE * WeaponModel.SPIKE_WIDTH)
		var turn: float = lerpf(0.3, JACK_MID_TURN, _unit(id, 10 + index)) * (1.0 if _unit(id, 20 + index) < 0.5 else -1.0)
		var bent: Vector2 = dir.rotated(turn) * way.length() * lerpf(JACK_MID_OUT.x, JACK_MID_OUT.y, _unit(id, 30 + index))
		mids.append(Vector3(bent.x, 0.0, bent.y))
		delays.append(floorf(_unit(id, 40 + index) * float(JACK_ROLL_WAIT + 1)))
	return {"twice": twice, "piles": piles, "mids": mids, "delays": delays}


## A number from 0 up to (not including) 1 that depends only on the field `id` and the `salt` (a hash, not a dice roll: every screen gets the same).
static func _unit(id: int, salt: int) -> float:
	return float(hash(Vector2i(id, salt)) & 0xFFFFF) / 1048576.0
