class_name FxZones
extends Node3D
## Draws the sim's zones keyed by id: a landing circle (the falling meteor of bato heavy, the tossed can of lata), a paper
## trap, the slow zone it springs into, and a jacks field. A zone's age is the view's own (first seen), so a delayed landing
## plays the same online, where the snapshot carries no time left: its total comes from the weapon's data and the thrower's
## position at the cast; offline the sim's time left makes it exact. The landing is the zone leaving the sim.

## The meteor of a rock-meteor circle starts this far off its circle, up and behind (it falls in on an ease-in curve, so it
## speeds up, and never crosses in front of a face), tumbling `METEOR_TUMBLE` turns on the way down.
const METEOR_FROM: Vector3 = FxMotion.METEOR_FROM
const METEOR_TUMBLE: float = 1.5
## The meteor's shadow grows from `METEOR_SHADOW_FROM` of its size to whole as it falls (the "it lands here" cue).
const METEOR_SHADOW_FROM: float = 0.3
## From this far through the fall it glints once (two little stars).
const METEOR_GLINT_AT: float = 12.0 / 18.0
const GLINT_STARS: int = 2
## The landing: the meteor squashes flat (`METEOR_IMPACT_S`) for `FxSquash.IMPACT_FRAMES` frames, a ring of dust is thrown
## out (`IMPACT_DUST` particles) and dark cracks reach `IMPACT_CRACK` of the circle's radius, fading over `CRACK_TIME` s.
const METEOR_IMPACT_S: float = 0.65
const IMPACT_DUST: int = 12
const IMPACT_CRACK: float = 0.8
const CRACK_TIME: float = 0.5
## The tossed can (lata) flies from the thrower's hand (`CAN_HAND_Y` m) to the circle's centre, arcing `CAN_PEAK` m plus
## `CAN_PEAK_PER_M` per metre thrown over the straight line, turning `CAN_TURNS` times end over end; it lands upright on its
## base, squashed (`CAN_LAND_S`) then stretched (`CAN_REBOUND_S`) for `CAN_LAND_FRAMES` frames each, with `CAN_DUST` dust.
## Online its flight is estimated from the cast's position and speed, `ONLINE_LEAD` s early (the zone is seen late).
const CAN_HAND_Y: float = 2.2
const CAN_PEAK: float = 2.2
const CAN_PEAK_PER_M: float = 0.25
const CAN_TURNS: float = 1.5
const CAN_LAND_S: float = 0.7
const CAN_REBOUND_S: float = 1.2
const CAN_LAND_FRAMES: int = 2
const CAN_DUST: int = 6
const ONLINE_LEAD: float = 0.05
## The can's flight bows `CAN_BOW` m to the side at its middle (back on the line at both ends): thrown up the screen, a
## straight arc ending at the target's feet would cross the target's face on screen; the bow passes beside the kid and
## comes in low. Decoration only: the circle (the sim's) never moves.
const CAN_BOW: float = 1.2
## A kid turning into a can (or back) disappears in a comic smoke puff of `SMOKE_PUFF` particles.
const SMOKE_PUFF: int = 8
## The paper trap is tossed from the thrower's hand (`CAN_HAND_Y` m) over `TOSS_TIME` s, arcing `TOSS_PEAK` m and turning
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
## `JACK_FLIGHT_FRAMES` frames `JACK_PEAK` m high and landing squashed (`JACK_LAND_S`) with `JACK_DUST` dust. A tick that
## hurts a kid makes the nearest cluster hop `JACK_HOP` m for a frame. In the last `SLOW_FADE` s they sink `JACK_SINK` m
## and shrink to `JACK_SHRINK` while the fill fades.
const JACK_STAGGER: int = 1
const JACK_FLIGHT_FRAMES: int = 6
const JACK_PEAK: float = 1.2
const JACK_LAND_S: float = 0.75
const JACK_DUST: int = 2
const JACK_HOP: float = 0.1
const JACK_SINK: float = 0.3
const JACK_SHRINK: float = 0.4
## Every zone says who threw it from its first frame (the thrower's team colour, an opaque rim): the fill of a slow zone,
## a jacks field and an armed trap.
const SLOW_FILL_ALPHA: float = 0.25
const FIELD_FILL_ALPHA: float = 0.2
const TRAP_ARMED_FILL: float = 0.12
## A trap still arming has only a lighter rim (its arming fill grows inside it).
const TRAP_ARMING_RIM: float = 0.5
## The progress fill of a delayed area (a landing, a trap arming): an inner disc in the team colour that grows from nothing
## to the whole circle exactly over the delay. Full = it lands (or the trap is armed).
const PROGRESS_ALPHA: float = 0.35
## A progress disc starts this small (a true zero breaks the transform).
const PROGRESS_START: float = 0.02
## Shortest total a delayed landing is given (seconds), so an unknown flight never divides by zero.
const MIN_TOTAL: float = 0.05


## One drawn zone.
class Rec extends RefCounted:
	var id: int
	var def: WeaponDef
	var team: int
	var kind: WeaponSystem.ZoneKind
	var node: Node3D
	var track: FxTrack
	## Seconds from the cast to the landing (EXPLOSION), from data and the cast position.
	var total: float = 0.0
	var mark: MeshInstance3D
	## The growing progress disc (a landing, a trap arming), or null.
	var progress: MeshInstance3D
	var armed: bool = false
	## The falling / tossed object's rig (Holder > Squash > Spin > model), or null.
	var obj: Node3D
	var glinted: bool = false
	## Where a tossed can came from (the thrower at the cast), or INF when the cast was not seen.
	var origin: Vector2 = Vector2.INF
	## A slow zone's popping fill and the fade materials of its last moments (rim, fill).
	var fill: MeshInstance3D
	var fades: Array[StandardMaterial3D] = []
	## A jacks field's clusters, where each rests, and when each last hopped.
	var clusters: Array[Node3D] = []
	var homes: Array[Vector3] = []
	var hops: PackedFloat32Array = PackedFloat32Array()
	var landed: int = 0
	var cluster_scale: float = 1.0


## A removed zone's object playing its end.
class Ghost extends RefCounted:
	var node: Node3D
	var kind: StringName
	var born: float
	var life: float


var _ctx: FxContext
var _recs: Dictionary[int, Rec] = {}
var _ghosts: Array[Ghost] = []
## Kids drawn as a can right now (to puff when they turn back).
var _cans: Dictionary[int, bool] = {}


func setup(ctx: FxContext) -> void:
	_ctx = ctx


func sync(sim: MatchSim) -> void:
	var seen: Dictionary[int, bool] = {}
	for zone: WeaponSystem.Zone in sim.weapons.zones:
		seen[zone.id] = true
		var rec: Rec = _recs.get(zone.id) as Rec
		if rec != null and rec.kind != zone.kind:
			_spring(rec)
			_remove(zone.id)
			rec = null
		if rec == null:
			rec = _create(zone)
		rec.track.see(zone.center, _ctx.time)
		_update(rec, zone)
	for id: int in _recs.keys():
		if not seen.has(id):
			_land(id)
	_step_ghosts()
	_step_cans(sim)


func live_count() -> int:
	return _recs.size()


func fading_count() -> int:
	return _ghosts.size()


func node_for(id: int) -> Node3D:
	var rec: Rec = _recs.get(id) as Rec
	return rec.node if rec != null else null


## A status effect landed on kid `id`: turned into a can, the kid vanishes in a smoke puff (born whole on this frame, so it
## hides the swap).
func on_effect(id: int, type: int) -> void:
	if type != StatusEffects.Type.POLYMORPH or _ctx.sim == null or not _ctx.sim.players.has(id):
		return
	_cans[id] = true
	_smoke(_ctx.sim.players[id].position)


## A kid whose can wore off turns back in the same puff (the snapshot carries the effects, so this works online too).
func _step_cans(sim: MatchSim) -> void:
	for id: int in _cans.keys():
		var kid: PlayerState = sim.players.get(id) as PlayerState
		if kid == null or not kid.effects.has(StatusEffects.Type.POLYMORPH):
			_cans.erase(id)
			if kid != null:
				_smoke(kid.position)
	for id: int in sim.players:
		if sim.players[id].effects.has(StatusEffects.Type.POLYMORPH) and not _cans.has(id):
			_cans[id] = true


func _smoke(at: Vector2) -> void:
	_ctx.particles.puff(FxParticles.Preset.SMOKE, Vector3(at.x, 0.0, at.y), SMOKE_PUFF)


## A kid was hit this frame at `at`: inside an enemy jacks field, the nearest cluster hops (the tick).
func on_tick_hit(kid_id: int, at: Vector2) -> void:
	if _ctx.reduced:
		return
	var team: int = _ctx.team_of(kid_id)
	for rec: Rec in _recs.values():
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


## The node a zone is drawn with: a root at the zone's centre holding its ground mark ("Mark") and its object.
func make_node(zone: WeaponSystem.Zone) -> Node3D:
	var node: Node3D = Node3D.new()
	var mark: MeshInstance3D = MeshInstance3D.new()
	mark.name = "Mark"
	mark.material_override = FxKit.overlay(FxKit.PRIO_FILL)
	mark.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	node.add_child(mark)
	var color: Color = _ctx.team_color(zone.team)
	match zone.kind:
		WeaponSystem.ZoneKind.EXPLOSION:
			# where it lands and who threw it: the thrower's team colour
			mark.mesh = FxGround.circle_mark(zone.radius, color, FxKit.FILL_ALPHA)
			node.add_child(_progress_disc(zone.radius, color))
		WeaponSystem.ZoneKind.TRAP:
			# the trigger range (exact), a light rim while it arms
			mark.mesh = FxGround.circle_mark(zone.radius, color, 0.0, TRAP_ARMING_RIM)
			node.add_child(_progress_disc(zone.radius, color))
			var ball: Node3D = WeaponModel.rig(WeaponModel.make(&"papel_trap", true))
			ball.name = "Ball"
			ball.scale = Vector3.ONE * _ctx.view_scale
			ball.position.y = ball_height() / 2.0
			node.add_child(ball)
		WeaponSystem.ZoneKind.SLOW_ZONE:
			# the exact slowing area: its rim at once, its fill popping out of the sprung trap
			mark.mesh = FxGround.circle_mark(zone.radius, color, 0.0)
			var fill: MeshInstance3D = MeshInstance3D.new()
			fill.name = "Fill"
			fill.mesh = FxGround.circle_fill(zone.radius, color, SLOW_FILL_ALPHA)
			fill.material_override = FxKit.overlay(FxKit.PRIO_FILL)
			fill.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			node.add_child(fill)
		WeaponSystem.ZoneKind.FIELD:
			mark.mesh = FxGround.circle_mark(zone.radius, color, FIELD_FILL_ALPHA)
	if zone.def != null and zone.def.id == &"bato_heavy":
		var meteor: Node3D = WeaponModel.rig(WeaponModel.meteor())
		meteor.name = "Meteor"
		node.add_child(meteor)
	elif zone.def != null and zone.def.id == &"lata":
		var can: Node3D = WeaponModel.rig(WeaponModel.make(&"lata", true))
		can.name = "Can"
		node.add_child(can)
	elif zone.def != null and zone.def.id == &"jacks" and zone.kind == WeaponSystem.ZoneKind.FIELD:
		var spikes: Node3D = WeaponModel.spikes(zone.radius)
		spikes.name = "Spikes"
		node.add_child(spikes)
	return node


## The progress disc of a circle of `radius` (scaled from nothing to whole over the delay).
func _progress_disc(radius: float, color: Color) -> MeshInstance3D:
	var disc: MeshInstance3D = MeshInstance3D.new()
	disc.name = "Progress"
	disc.mesh = FxGround.circle_fill(radius, color, PROGRESS_ALPHA)
	disc.material_override = FxKit.overlay(FxKit.PRIO_RIM)
	disc.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	disc.scale = Vector3(PROGRESS_START, 1.0, PROGRESS_START)
	return disc


## How far a trap is through its arming (0 at the cast, 1 armed): offline its arming time left, online its age.
func arming(zone: WeaponSystem.Zone, rec: Rec) -> float:
	var arm: float = maxf(zone.def.arm_time if zone.def != null else 0.0, 0.001)
	if zone.timer > 0.0 or zone.time_left > 0.0:
		return clampf(1.0 - zone.timer / arm, 0.0, 1.0)
	return clampf(rec.track.age(_ctx.time) / arm, 0.0, 1.0)


## How far a delayed landing is (0 at the cast, 1 on the last frame before it lands): offline from the sim's time left,
## online from the zone's age on screen. It reaches 1 a frame before the zone leaves the sim, so the object touches the
## ground on the last frame it is drawn and the landing frame is the squash.
func progress(zone: WeaponSystem.Zone, rec: Rec) -> float:
	var span: float = maxf(rec.total - FxSquash.FRAME, MIN_TOTAL)
	if zone.time_left > 0.0:
		return clampf((rec.total - zone.time_left) / span, 0.0, 1.0)
	return clampf(rec.track.age(_ctx.time) / span, 0.0, 1.0)


func _create(zone: WeaponSystem.Zone) -> Rec:
	var rec: Rec = Rec.new()
	rec.id = zone.id
	rec.def = zone.def
	rec.team = zone.team
	rec.kind = zone.kind
	rec.track = FxTrack.new()
	rec.total = _total(zone)
	rec.node = make_node(zone)
	rec.mark = rec.node.get_node("Mark") as MeshInstance3D
	rec.progress = rec.node.get_node_or_null("Progress") as MeshInstance3D
	rec.obj = rec.node.get_node_or_null("Meteor") as Node3D
	if rec.obj == null:
		rec.obj = rec.node.get_node_or_null("Can") as Node3D
	if rec.obj == null:
		rec.obj = rec.node.get_node_or_null("Ball") as Node3D
	rec.fill = rec.node.get_node_or_null("Fill") as MeshInstance3D
	var spikes: Node3D = rec.node.get_node_or_null("Spikes") as Node3D
	if spikes != null:
		for child: Node in spikes.get_children():
			var cluster: Node3D = child as Node3D
			rec.clusters.append(cluster)
			rec.homes.append(cluster.position)
			rec.hops.append(-1.0)
		rec.cluster_scale = rec.clusters[0].scale.x if not rec.clusters.is_empty() else 1.0
	if zone.def != null:
		var origin: Variant = _ctx.cast_origin(zone.def.id, zone.team)
		rec.origin = origin as Vector2 if origin != null else Vector2.INF
	add_child(rec.node)
	_recs[rec.id] = rec
	return rec


## Seconds from the cast to the landing: the weapon's delay plus its flight from the thrower (when the cast was seen),
## never less than the sim's own time left (offline).
func _total(zone: WeaponSystem.Zone) -> float:
	if zone.def == null:
		return maxf(zone.time_left, MIN_TOTAL)
	var total: float = zone.def.delay
	var origin: Variant = _ctx.cast_origin(zone.def.id, zone.team)
	if zone.def.speed > 0.0:
		var from: Vector2 = origin as Vector2 if origin != null else zone.center
		total += from.distance_to(zone.center) / zone.def.speed
		if zone.time_left <= 0.0:
			# online: the zone is first seen a little after the cast
			total -= ONLINE_LEAD
	return maxf(maxf(total, zone.time_left), MIN_TOTAL)


func _update(rec: Rec, zone: WeaponSystem.Zone) -> void:
	rec.node.position = Vector3(zone.center.x, 0.0, zone.center.y)
	if rec.progress != null:
		var p: float = progress(zone, rec) if zone.kind == WeaponSystem.ZoneKind.EXPLOSION else arming(zone, rec)
		var size: float = maxf(p, PROGRESS_START)
		rec.progress.scale = Vector3(size, 1.0, size)
		if zone.kind == WeaponSystem.ZoneKind.TRAP and p >= 1.0 and not rec.armed:
			# armed: the fill gives way to a solid rim and a light fill (it is live now)
			rec.armed = true
			rec.progress.visible = false
			rec.mark.mesh = FxGround.circle_mark(zone.radius, _ctx.team_color(zone.team), TRAP_ARMED_FILL)
	if zone.def != null and zone.def.id == &"bato_heavy":
		_meteor(rec, zone)
	elif zone.def != null and zone.def.id == &"lata" and rec.obj != null:
		_can(rec, zone)
	match zone.kind:
		WeaponSystem.ZoneKind.TRAP:
			_trap(rec, zone)
		WeaponSystem.ZoneKind.SLOW_ZONE:
			_slow(rec, zone)
		WeaponSystem.ZoneKind.FIELD:
			_field(rec, zone)


## How tall the paper ball is drawn (metres).
func ball_height() -> float:
	return (WeaponModel.OBJECTS[&"papel_trap"][1] as float) * _ctx.view_scale


## Seconds a zone of `total` seconds has left: offline the sim's, online from its age.
func _left(zone: WeaponSystem.Zone, rec: Rec, total: float) -> float:
	return zone.time_left if zone.time_left > 0.0 else total - rec.track.age(_ctx.time)


## The paper ball is tossed onto its spot, lands with a squash and, armed, breathes.
func _trap(rec: Rec, zone: WeaponSystem.Zone) -> void:
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
		rec.obj.position = FxMotion.toss(Vector3(offset.x, CAN_HAND_Y, offset.y), Vector3(0.0, half, 0.0), TOSS_PEAK, p)
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
func _spring(rec: Rec) -> void:
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
	add_child(ball)
	(ball.get_node("Squash") as Node3D).basis = Basis.from_scale(FxSquash.along(SPRING_S))
	ball.position = Vector3(at.x, ball_height() / 2.0 * SPRING_S, at.z)
	_ghost(ball, &"spring", FxSquash.IMPACT_FRAMES * FxSquash.FRAME)


## The slow zone's fill pops out, and in its last moments the zone fades (its rim stays faintly until it is gone).
func _slow(rec: Rec, zone: WeaponSystem.Zone) -> void:
	var age: float = rec.track.age(_ctx.time)
	var pop: float = 1.0 if _ctx.reduced else clampf(age / (FILL_POP_FRAMES * FxSquash.FRAME), 0.0, 1.0)
	rec.fill.scale = Vector3(maxf(pop, PROGRESS_START), 1.0, maxf(pop, PROGRESS_START))
	var left: float = _left(zone, rec, zone.def.duration if zone.def != null else 0.0)
	var nodes: Array[MeshInstance3D] = [rec.mark, rec.fill]
	var ends: Array[float] = [FADE_RIM, 0.0]
	_fade(rec, left, nodes, ends)


## The last `SLOW_FADE` seconds of a zone with `left` seconds to go: each node's alpha goes from 1 to its end value.
func _fade(rec: Rec, left: float, nodes: Array[MeshInstance3D], ends: Array[float]) -> void:
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
func _field(rec: Rec, zone: WeaponSystem.Zone) -> void:
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
			cluster.position = FxMotion.toss(Vector3(offset.x, CAN_HAND_Y, offset.y), home, JACK_PEAK, maxf(t, 0.0))
			cluster.visible = t >= 0.0
			continue
		cluster.visible = true
		if rec.origin != Vector2.INF and t < 1.0 + float(FxSquash.IMPACT_FRAMES) * FxSquash.FRAME / flight:
			cluster.scale = FxSquash.along(JACK_LAND_S) * scale
			if i >= rec.landed:
				rec.landed = i + 1
				var at: Vector3 = rec.node.position + home
				_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(at.x, 0.0, at.z), JACK_DUST)
		if rec.hops[i] >= 0.0 and _ctx.time - rec.hops[i] < FxSquash.FRAME:
			cluster.position.y += JACK_HOP


## The can flies from the thrower's hand to the middle of its circle, tumbling end over end and stretched along its flight,
## its shadow running along the ground under it; it lands upright on its base as the zone lands.
func _can(rec: Rec, zone: WeaponSystem.Zone) -> void:
	var p: float = progress(zone, rec)
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
	squash.basis = FxSquash.stretch_basis(velocity, FxSquash.velocity_stretch(zone.def.speed))
	var axis: Vector3 = dir.cross(Vector3.UP) if dir != Vector3.ZERO else Vector3.RIGHT
	(squash.get_node("Spin") as Node3D).basis = Basis(axis.normalized(), CAN_TURNS * TAU * p)


## How tall the tossed can is drawn (metres).
func can_height() -> float:
	return (WeaponModel.OBJECTS[&"lata"][1] as float) * _ctx.view_scale


## The rock meteor falls from up and behind onto its circle (ease-in, so it speeds up), tumbling, its shadow growing at the
## centre; its bottom touches the street on the last frame before the landing and never goes below it.
func _meteor(rec: Rec, zone: WeaponSystem.Zone) -> void:
	var p: float = progress(zone, rec)
	var radius: float = WeaponModel.METEOR_SIZE / 2.0
	rec.obj.position = FxMotion.meteor_offset(p) + Vector3(0.0, radius, 0.0)
	if _ctx.shadows.attach(rec.id, WeaponModel.METEOR_SIZE, rec.team):
		_ctx.shadows.place(rec.id, zone.center, rec.obj.position.y, lerpf(METEOR_SHADOW_FROM, 1.0, p))
	if _ctx.reduced:
		return
	var axis: Vector3 = (-METEOR_FROM).normalized().cross(Vector3.UP).normalized()
	(rec.obj.get_node("Squash/Spin") as Node3D).basis = Basis(axis, METEOR_TUMBLE * TAU * p)
	if p >= METEOR_GLINT_AT and not rec.glinted:
		rec.glinted = true
		var at: Vector3 = rec.node.position + rec.obj.position
		_ctx.particles.puff(FxParticles.Preset.STARS, at - Vector3(0.0, FxParticles.spec(FxParticles.Preset.STARS).points[0].y, 0.0), GLINT_STARS)


## The zone left the sim: a landing plays its impact (the meteor), the rest just go.
func _land(id: int) -> void:
	var rec: Rec = _recs[id]
	if rec.def != null and rec.def.id == &"bato_heavy" and rec.kind == WeaponSystem.ZoneKind.EXPLOSION:
		var center: Vector2 = Vector2(rec.node.position.x, rec.node.position.z)
		_ctx.ground.crack(center, 0.0, &"spokes", Vector2.ONE * rec.def.radius * IMPACT_CRACK, CRACK_TIME)
		if not _ctx.reduced:
			# it lands: squashed flat on the street for two frames, a ring of dust, then it is gone
			var meteor: Node3D = rec.obj
			var at: Vector3 = meteor.global_position
			rec.node.remove_child(meteor)
			add_child(meteor)
			meteor.position = Vector3(at.x, WeaponModel.METEOR_SIZE / 2.0 * METEOR_IMPACT_S, at.z)
			(meteor.get_node("Squash") as Node3D).scale = FxSquash.along(METEOR_IMPACT_S)
			_ghost(meteor, &"meteor", FxSquash.IMPACT_FRAMES * FxSquash.FRAME)
			_ctx.particles.puff(FxParticles.Preset.IMPACT, Vector3(center.x, 0.0, center.y), IMPACT_DUST)
	elif rec.def != null and rec.def.id == &"lata" and rec.obj != null and not _ctx.reduced:
		# the can lands upright: squash, stretch, then it is gone (it "became" the kids it hit)
		var can: Node3D = rec.obj
		var at: Vector3 = can.global_position
		rec.node.remove_child(can)
		add_child(can)
		can.position = Vector3(at.x, 0.0, at.z)
		var center: Vector2 = Vector2(at.x, at.z)
		_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(center.x, 0.0, center.y), CAN_DUST)
		_ghost(can, &"can", 2.0 * CAN_LAND_FRAMES * FxSquash.FRAME)
	_remove(id)


func _remove(id: int) -> void:
	var rec: Rec = _recs[id]
	_recs.erase(id)
	_ctx.shadows.detach(id)
	for fade: StandardMaterial3D in rec.fades:
		_ctx.ground.fade_give(fade)
	rec.node.queue_free()


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
		var age: float = _ctx.time - ghost.born
		if age >= ghost.life - 0.0001:
			ghost.node.queue_free()
			continue
		if ghost.kind == &"can":
			# squashed on its base for two frames, then stretched up for two (its base stays on the street)
			var s: float = CAN_LAND_S if age < CAN_LAND_FRAMES * FxSquash.FRAME else CAN_REBOUND_S
			var squash: Node3D = ghost.node.get_node("Squash") as Node3D
			squash.basis = Basis.from_scale(FxSquash.along(s))
			(squash.get_node("Spin") as Node3D).basis = Basis.IDENTITY
			ghost.node.position.y = can_height() / 2.0 * s
		still.append(ghost)
	_ghosts = still
