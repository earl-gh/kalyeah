class_name FxZones
extends Node3D
## Draws the sim's zones keyed by id: a landing circle (the falling meteor of bato heavy, the tossed can of lata), a paper
## trap, the slow zone it springs into, and a jacks field. A zone's age is the view's own (first seen), so a delayed landing
## plays the same online, where the snapshot carries no time left: its total comes from the weapon's data and the thrower's
## position at the cast; offline the sim's time left makes it exact. The landing is the zone leaving the sim.

## The meteor of a rock-meteor circle starts this far off its circle (it falls in on an ease-in curve).
const METEOR_FROM: Vector3 = Vector3(3.0, 11.0, -3.0)
## The tossed can: how high its arc goes and how high its middle rests.
const CAN_ARC: float = 2.2
const CAN_REST: float = 0.35
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


var _ctx: FxContext
var _recs: Dictionary[int, Rec] = {}


func setup(ctx: FxContext) -> void:
	_ctx = ctx


func sync(sim: MatchSim) -> void:
	var seen: Dictionary[int, bool] = {}
	for zone: WeaponSystem.Zone in sim.weapons.zones:
		seen[zone.id] = true
		var rec: Rec = _recs.get(zone.id) as Rec
		if rec != null and rec.kind != zone.kind:
			_remove(zone.id)
			rec = null
		if rec == null:
			rec = _create(zone)
		rec.track.see(zone.center, _ctx.time)
		_update(rec, zone)
	for id: int in _recs.keys():
		if not seen.has(id):
			_remove(id)


func live_count() -> int:
	return _recs.size()


func fading_count() -> int:
	return 0


func node_for(id: int) -> Node3D:
	var rec: Rec = _recs.get(id) as Rec
	return rec.node if rec != null else null


## A status effect landed on kid `id` (the polymorph puff).
func on_effect(_id: int, _type: int) -> void:
	pass


## A kid was hit this frame at `at` (a jacks tick makes the nearest cluster hop).
func on_tick_hit(_kid_id: int, _at: Vector2) -> void:
	pass


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
			var ball: Node3D = WeaponModel.make(&"papel_trap", false)
			ball.name = "Ball"
			ball.scale = Vector3.ONE * _ctx.view_scale
			node.add_child(ball)
		WeaponSystem.ZoneKind.SLOW_ZONE:
			mark.mesh = FxGround.circle_mark(zone.radius, color, SLOW_FILL_ALPHA)
		WeaponSystem.ZoneKind.FIELD:
			mark.mesh = FxGround.circle_mark(zone.radius, color, FIELD_FILL_ALPHA)
	if zone.def != null and zone.def.id == &"bato_heavy":
		var meteor: Node3D = WeaponModel.meteor()
		meteor.name = "Meteor"
		node.add_child(meteor)
	elif zone.def != null and zone.def.id == &"lata":
		var can: Node3D = WeaponModel.make(&"lata", true)
		can.name = "Can"
		node.add_child(can)
	elif zone.def != null and zone.def.id == &"jacks":
		node.add_child(WeaponModel.spikes(zone.radius))
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


## How far a delayed landing is (0 at the cast, 1 at the landing): offline from the sim's time left, online from the
## zone's age on screen.
func progress(zone: WeaponSystem.Zone, rec: Rec) -> float:
	if zone.time_left > 0.0:
		return clampf(1.0 - zone.time_left / rec.total, 0.0, 1.0)
	return clampf(rec.track.age(_ctx.time) / rec.total, 0.0, 1.0)


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
	var meteor: Node3D = rec.node.get_node_or_null("Meteor") as Node3D
	var can: Node3D = rec.node.get_node_or_null("Can") as Node3D
	var flying: Node3D = meteor if meteor != null else can
	if flying != null:
		# who threw it and where it lands: a team-rimmed shadow on the circle
		if _ctx.shadows.attach(rec.id, WeaponModel.METEOR_SIZE if meteor != null else (WeaponModel.OBJECTS[&"lata"][1] as float) * _ctx.view_scale, rec.team):
			_ctx.shadows.place(rec.id, zone.center, flying.position.y)
	if meteor != null:
		# it falls onto the circle and lands as the delay ends
		meteor.position = METEOR_FROM * (1.0 - progress(zone, rec)) + Vector3(0.0, WeaponModel.METEOR_SIZE / 2.0, 0.0)
		meteor.rotation.x += _ctx.delta * 4.0
	if can != null:
		can.position.y = sin(progress(zone, rec) * PI) * CAN_ARC + CAN_REST
		can.rotation.x += _ctx.delta * 9.0


func _remove(id: int) -> void:
	var rec: Rec = _recs[id]
	_recs.erase(id)
	_ctx.shadows.detach(id)
	rec.node.queue_free()
