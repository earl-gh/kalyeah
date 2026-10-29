class_name FxFields
extends RefCounted
## The placed fields of FxZones: the paper trap (tossed onto its spot, arming, breathing, springing into its slow zone),
## the slow zone (its fill popping out, fading at its end) and the jacks field (sprayed from the hand, hopping on a tick,
## sinking away). FxZones owns the zones and their nodes; this only poses them.

## The paper trap is tossed from the thrower's hand (`FxLandings.CAN_HAND_Y` m) over `TOSS_TIME` s, arcing `TOSS_PEAK` m and turning
## once, lands squashed (`FxSquash.IMPACT_S`) and, armed, breathes (`BREATHE_S` to 1 at `BREATHE_HZ`). When a kid trips it
## it springs (`SPRING_S` for `FxSquash.IMPACT_FRAMES` frames) into `PAPER_BURST` paper bits and `SCRAPS` scraps, and the slow
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
## The jacks are tossed from the thrower's hand in a spray, one cluster a frame (`JACK_STAGGER`), each flying
## `JACK_FLIGHT_FRAMES` frames `JACK_PEAK` m high and landing squashed (`JACK_LAND_S`), the spray throwing `JACK_DUST` dust
## (one shared puff). A tick that hurts a kid makes the nearest cluster hop `JACK_HOP` m for a frame. In the last `SLOW_FADE` s they sink `JACK_SINK` m
## and shrink to `JACK_SHRINK` while the fill fades.
const JACK_STAGGER: int = 1
const JACK_FLIGHT_FRAMES: int = 6
const JACK_PEAK: float = 1.2
const JACK_LAND_S: float = 0.75
const JACK_DUST: int = 6
const JACK_HOP: float = 0.1
const JACK_SINK: float = 0.3
const JACK_SHRINK: float = 0.4



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
		squash.basis = Basis.IDENTITY
		return
	spin.basis = Basis.IDENTITY
	var landed: float = age - (TOSS_TIME if rec.origin != Vector2.INF else 0.0)
	var s: float = 1.0
	if landed < FxSquash.IMPACT_FRAMES * FxSquash.FRAME:
		s = FxSquash.IMPACT_S
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
	_zones.ghost(ball, &"spring", FxSquash.IMPACT_FRAMES * FxSquash.FRAME)


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


## The jacks are sprayed from the thrower's hand onto their spots, hop on a tick and sink away at the end.
func field(rec: FxZones.Rec, zone: WeaponSystem.Zone) -> void:
	var age: float = rec.track.age(_ctx.time)
	var left: float = _left(zone, rec, zone.def.duration if zone.def != null else 0.0)
	var nodes: Array[MeshInstance3D] = [rec.mark]
	var ends: Array[float] = [FADE_RIM]
	_fade(rec, left, nodes, ends)
	var sink: float = 0.0 if _ctx.reduced else clampf(1.0 - left / SLOW_FADE, 0.0, 1.0)
	for i: int in rec.clusters.size():
		var cluster: Node3D = rec.clusters[i]
		var home: Vector3 = rec.homes[i]
		var scale: float = rec.cluster_scale * lerpf(1.0, JACK_SHRINK, sink)
		cluster.scale = Vector3.ONE * scale
		cluster.position = home - Vector3(0.0, JACK_SINK * sink, 0.0)
		if _ctx.reduced:
			continue
		var start: float = float(i * JACK_STAGGER) * FxSquash.FRAME
		var flight: float = JACK_FLIGHT_FRAMES * FxSquash.FRAME
		var t: float = (age - start) / flight
		if rec.origin != Vector2.INF and t < 1.0:
			var offset: Vector2 = rec.origin - zone.center
			cluster.position = FxMotion.toss(Vector3(offset.x, FxLandings.CAN_HAND_Y, offset.y), home, JACK_PEAK, maxf(t, 0.0))
			cluster.visible = t >= 0.0
			continue
		cluster.visible = true
		if rec.origin != Vector2.INF and t < 1.0 + float(FxSquash.IMPACT_FRAMES) * FxSquash.FRAME / flight:
			cluster.scale = FxSquash.along(JACK_LAND_S) * scale
			if rec.landed == 0:
				# one shared puff of dust for the whole spray (one emitter, not one per cluster)
				rec.landed = 1
				_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(rec.node.position.x, 0.0, rec.node.position.z), JACK_DUST)
		if rec.hops[i] >= 0.0 and _ctx.time - rec.hops[i] < FxSquash.FRAME:
			cluster.position.y += JACK_HOP
