class_name FxZones
extends Node3D
## Draws the sim's zones keyed by id: a landing circle (the falling meteor of bato heavy, the tossed can of lata), a paper
## trap, the slow zone it springs into, and a jacks field. A zone's age is the view's own (first seen), so a delayed landing
## plays the same online, where the snapshot carries no time left: its total comes from the weapon's data and the thrower's
## position at the cast; offline the sim's time left makes it exact. The landing is the zone leaving the sim.

## Every zone says who threw it from its first frame: white for your team's, red for the other team's (`FxContext.area_color`), a
## white tint fill (`FxKit.AREA_FILL_ALPHA`) inside a thin outline (`FxKit.AREA_RIM`, `FxKit.AREA_RIM_ALPHA`), both half see-through: a landing circle, an armed trap, its slow
## zone and a jacks field alike.
## A trap still arming has only a lighter outline (its arming fill grows inside it).
const TRAP_ARMING_RIM: float = FxKit.AREA_RIM_ALPHA * 0.85
## The progress fill of a delayed area (a landing, a trap arming): an inner disc in the area's colour that grows from nothing
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
	## Each cluster's own turn about the vertical (radians), kept so a smear can lie along the flight in world axes.
	var turns: PackedFloat32Array = PackedFloat32Array()
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
## The trap, slow zone and jacks field poses (FxFields), and the meteor and can landings (FxLandings).
var fields: FxFields
var landings: FxLandings
## Kids drawn as a can right now (to puff when they turn back).


func setup(ctx: FxContext) -> void:
	_ctx = ctx
	fields = FxFields.new()
	fields.setup(ctx, self)
	landings = FxLandings.new()
	landings.setup(ctx, self)


## The zones drawn right now (FxFields reads them).
func recs() -> Array[Rec]:
	var all: Array[Rec] = []
	all.assign(_recs.values())
	return all


## A kid was hit this frame at `at` (a jacks tick makes the nearest cluster hop).
func on_tick_hit(kid_id: int, at: Vector2) -> void:
	fields.on_tick_hit(kid_id, at)


func sync(sim: MatchSim) -> void:
	var seen: Dictionary[int, bool] = {}
	for zone: WeaponSystem.Zone in sim.weapons.zones:
		seen[zone.id] = true
		var rec: Rec = _recs.get(zone.id) as Rec
		if rec != null and rec.kind != zone.kind:
			fields.spring(rec)
			_remove(zone.id)
			rec = null
		if rec == null:
			rec = _create(zone)
		rec.track.see(zone.center, _ctx.time)
		_update(rec, zone)
	for id: int in _recs.keys():
		if not seen.has(id):
			landings.land(_recs[id])
			_remove(id)
	_step_ghosts()


func live_count() -> int:
	return _recs.size()


func fading_count() -> int:
	return _ghosts.size()


func node_for(id: int) -> Node3D:
	var rec: Rec = _recs.get(id) as Rec
	return rec.node if rec != null else null


## The node a zone is drawn with: a root at the zone's centre holding its ground mark ("Mark") and its object.
func make_node(zone: WeaponSystem.Zone) -> Node3D:
	var node: Node3D = Node3D.new()
	var mark: MeshInstance3D = MeshInstance3D.new()
	mark.name = "Mark"
	mark.material_override = FxKit.overlay(FxKit.PRIO_FILL)
	mark.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	node.add_child(mark)
	var color: Color = _ctx.area_color(zone.team)
	match zone.kind:
		WeaponSystem.ZoneKind.EXPLOSION:
			# where it lands and who threw it: white (yours) or red (theirs)
			mark.mesh = FxGround.area_mark(zone.radius, color)
			node.add_child(_progress_disc(zone.radius, color))
		WeaponSystem.ZoneKind.TRAP:
			# the trigger range (exact), a lighter outline while it arms
			mark.mesh = FxGround.area_mark(zone.radius, color, 0.0, TRAP_ARMING_RIM)
			node.add_child(_progress_disc(zone.radius, color))
			var ball: Node3D = WeaponModel.rig(WeaponModel.make(&"papel_trap", true))
			ball.name = "Ball"
			ball.scale = Vector3.ONE * _ctx.view_scale
			ball.position.y = fields.ball_height() / 2.0
			node.add_child(ball)
		WeaponSystem.ZoneKind.SLOW_ZONE:
			# the exact slowing area: its rim at once, its fill popping out of the sprung trap
			mark.mesh = FxGround.area_mark(zone.radius, color, 0.0)
			var fill: MeshInstance3D = MeshInstance3D.new()
			fill.name = "Fill"
			fill.mesh = FxGround.circle_fill(zone.radius - FxKit.AREA_RIM / 2.0, color, FxKit.AREA_FILL_ALPHA)
			fill.material_override = FxKit.overlay(FxKit.PRIO_FILL)
			fill.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			node.add_child(fill)
		WeaponSystem.ZoneKind.FIELD:
			mark.mesh = FxGround.area_mark(zone.radius, color)
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
			rec.turns.append(cluster.rotation.y)
		rec.cluster_scale = rec.clusters[0].scale.x if not rec.clusters.is_empty() else 1.0
	if zone.def != null:
		var origin: Variant = _ctx.cast_origin(zone.def.id, zone.team)
		rec.origin = origin as Vector2 if origin != null else Vector2.INF
	add_child(rec.node)
	_recs[rec.id] = rec
	# the thrower's release streak toward the spot the throw lands on (not the meteor: it falls from the sky)
	if zone.def != null and zone.def.id != &"bato_heavy" and zone.kind != WeaponSystem.ZoneKind.SLOW_ZONE and rec.origin != Vector2.INF:
		FxRelease.play(_ctx, rec.origin, zone.center - rec.origin, zone.def.speed, FxLandings.CAN_HAND_Y, FxRelease.tint_for(zone.def.id))
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
			total -= FxLandings.ONLINE_LEAD
	return maxf(maxf(total, zone.time_left), MIN_TOTAL)


func _update(rec: Rec, zone: WeaponSystem.Zone) -> void:
	rec.node.position = Vector3(zone.center.x, 0.0, zone.center.y)
	if rec.progress != null:
		var p: float = progress(zone, rec) if zone.kind == WeaponSystem.ZoneKind.EXPLOSION else fields.arming(zone, rec)
		var size: float = maxf(p, PROGRESS_START)
		rec.progress.scale = Vector3(size, 1.0, size)
		if zone.kind == WeaponSystem.ZoneKind.TRAP and p >= 1.0 and not rec.armed:
			# armed: the fill gives way to a solid outline and the area fill (it is live now)
			rec.armed = true
			rec.progress.visible = false
			rec.mark.mesh = FxGround.area_mark(zone.radius, _ctx.area_color(zone.team))
	if zone.def != null and zone.def.id == &"bato_heavy":
		landings.meteor(rec, zone)
	elif zone.def != null and zone.def.id == &"lata" and rec.obj != null:
		landings.can(rec, zone)
	match zone.kind:
		WeaponSystem.ZoneKind.TRAP:
			fields.trap(rec, zone)
		WeaponSystem.ZoneKind.SLOW_ZONE:
			fields.slow(rec, zone)
		WeaponSystem.ZoneKind.FIELD:
			fields.field(rec, zone)


func _remove(id: int) -> void:
	var rec: Rec = _recs[id]
	_recs.erase(id)
	_ctx.shadows.detach(id)
	for fade: StandardMaterial3D in rec.fades:
		_ctx.ground.fade_give(fade)
	rec.node.queue_free()


## Keeps `node` (a removed zone's object) playing its end for `life` seconds, then frees it.
func ghost(node: Node3D, kind: StringName, life: float) -> void:
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
			# squashed on its base for two frames, then springing back through an overshoot to its size (its base stays on the street)
			var s: float = FxSquash.landing(FxLandings.CAN_LAND_S, age / FxSquash.FRAME, FxLandings.CAN_LAND_FRAMES)
			var squash: Node3D = ghost.node.get_node("Squash") as Node3D
			squash.basis = Basis.from_scale(FxSquash.along(s))
			(squash.get_node("Spin") as Node3D).basis = Basis.IDENTITY
			ghost.node.position.y = landings.can_height() / 2.0 * s
		elif ghost.kind == &"spring":
			# the sprung trap ball: stretched up for two frames, then back through a squash to its size (its bottom stays down)
			var s: float = FxSquash.landing(FxFields.SPRING_S, age / FxSquash.FRAME)
			(ghost.node.get_node("Squash") as Node3D).basis = Basis.from_scale(FxSquash.along(s))
			ghost.node.position.y = fields.ball_height() / 2.0 * s
		elif ghost.kind == &"meteor":
			# squashed flat on the street for two frames, springing back through an overshoot (its bottom stays on the street)
			var s: float = FxSquash.landing(FxLandings.METEOR_IMPACT_S, age / FxSquash.FRAME)
			(ghost.node.get_node("Squash") as Node3D).basis = Basis.from_scale(FxSquash.along(s))
			ghost.node.position.y = WeaponModel.METEOR_SIZE / 2.0 * s
		still.append(ghost)
	_ghosts = still
