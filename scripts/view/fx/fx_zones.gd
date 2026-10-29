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
## The old flat tints of a slow zone and a jacks field.
const ZONE_COLORS: Dictionary[WeaponSystem.ZoneKind, Color] = {
	WeaponSystem.ZoneKind.SLOW_ZONE: Color(0.35, 0.65, 1.0, 0.35),
	WeaponSystem.ZoneKind.FIELD: Color(1.0, 0.85, 0.3, 0.35),
}
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
	match zone.kind:
		WeaponSystem.ZoneKind.EXPLOSION:
			# where it lands and who threw it: the thrower's team colour
			mark.mesh = FxGround.circle_mark(zone.radius, _ctx.team_color(zone.team), FxKit.FILL_ALPHA)
		WeaponSystem.ZoneKind.TRAP:
			mark.visible = false
			var ball: Node3D = WeaponModel.make(&"papel_trap", false)
			ball.name = "Ball"
			ball.scale = Vector3.ONE * _ctx.view_scale
			node.add_child(ball)
		_:
			mark.mesh = FxGround.circle_fill(zone.radius, ZONE_COLORS[zone.kind], 1.0)
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
	var meteor: Node3D = rec.node.get_node_or_null("Meteor") as Node3D
	if meteor != null:
		# it falls onto the circle and lands as the delay ends
		meteor.position = METEOR_FROM * (1.0 - progress(zone, rec)) + Vector3(0.0, WeaponModel.METEOR_SIZE / 2.0, 0.0)
		meteor.rotation.x += _ctx.delta * 4.0
	var can: Node3D = rec.node.get_node_or_null("Can") as Node3D
	if can != null:
		can.position.y = sin(progress(zone, rec) * PI) * CAN_ARC + CAN_REST
		can.rotation.x += _ctx.delta * 9.0


func _remove(id: int) -> void:
	var rec: Rec = _recs[id]
	_recs.erase(id)
	rec.node.queue_free()
