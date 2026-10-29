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
	## The falling / tossed object's rig (Holder > Squash > Spin > model), or null.
	var obj: Node3D
	var glinted: bool = false


## A removed zone's object playing its end.
class Ghost extends RefCounted:
	var node: Node3D
	var kind: StringName
	var born: float
	var life: float


var _ctx: FxContext
var _recs: Dictionary[int, Rec] = {}
var _ghosts: Array[Ghost] = []


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
			_land(id)
	_step_ghosts()


func live_count() -> int:
	return _recs.size()


func fading_count() -> int:
	return _ghosts.size()


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
		var meteor: Node3D = WeaponModel.rig(WeaponModel.meteor())
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
	if zone.def != null and zone.def.id == &"bato_heavy":
		_meteor(rec, zone)
	var can: Node3D = rec.node.get_node_or_null("Can") as Node3D
	if can != null:
		if _ctx.shadows.attach(rec.id, (WeaponModel.OBJECTS[&"lata"][1] as float) * _ctx.view_scale, rec.team):
			_ctx.shadows.place(rec.id, zone.center, can.position.y)
		can.position.y = sin(progress(zone, rec) * PI) * CAN_ARC + CAN_REST
		can.rotation.x += _ctx.delta * 9.0


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
	_remove(id)


func _remove(id: int) -> void:
	var rec: Rec = _recs[id]
	_recs.erase(id)
	_ctx.shadows.detach(id)
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
		if _ctx.time - ghost.born >= ghost.life - 0.0001:
			ghost.node.queue_free()
		else:
			still.append(ghost)
	_ghosts = still
