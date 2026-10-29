class_name FxProjectiles
extends Node3D
## Draws the sim's live projectiles (the thrown rock, the slippers, the marble, the spinning top) keyed by id: ground x/z is
## always the sim's, only height, turn, squash and decoration are the view's. Works from positions alone (online snapshots
## carry no direction), using the offline extras only to be exact. A removed projectile becomes a short ghost.

## Flight height of a thrown object (times the view scale: 1.4 m drawn), and its turns per second.
const PROJECTILE_HEIGHT: float = 0.7
const SPIN: float = 2.0
const TOP_SPIN: float = 6.0

## A projectile left the sim (hit, caught, blocked or at the end of its flight) at `at` (world, drawn height).
signal gone(id: int, def: WeaponDef, team: int, at: Vector3)


## One drawn projectile.
class Rec extends RefCounted:
	var id: int
	var def: WeaponDef
	var team: int
	var holder: Node3D
	var squash: Node3D
	var spin: Node3D
	var track: FxTrack


var _ctx: FxContext
var _recs: Dictionary[int, Rec] = {}


func setup(ctx: FxContext) -> void:
	_ctx = ctx


func sync(sim: MatchSim) -> void:
	var seen: Dictionary[int, bool] = {}
	for projectile: WeaponSystem.Projectile in sim.weapons.projectiles:
		seen[projectile.id] = true
		var rec: Rec = _recs.get(projectile.id) as Rec
		if rec == null:
			rec = _create(projectile)
		rec.track.see(projectile.position, _ctx.time)
		_update(rec, projectile)
	for id: int in _recs.keys():
		if not seen.has(id):
			_remove(id)


## Sim-bound projectiles drawn right now.
func live_count() -> int:
	return _recs.size()


## Removed projectiles still playing their end.
func fading_count() -> int:
	return 0


## The node of projectile `id`, or null.
func node_for(id: int) -> Node3D:
	var rec: Rec = _recs.get(id) as Rec
	return rec.holder if rec != null else null


## A kid was hit this frame at `at`: the nearest enemy slipper or top reacts.
func react_hit(_kid_id: int, _at: Vector2) -> void:
	pass


func _create(projectile: WeaponSystem.Projectile) -> Rec:
	var rec: Rec = Rec.new()
	rec.id = projectile.id
	rec.def = projectile.def
	rec.team = projectile.team
	rec.track = FxTrack.new()
	var model: Node3D
	if WeaponModel.has_model(projectile.def.id):
		model = WeaponModel.make(projectile.def.id, true)
	else:
		var mesh: MeshInstance3D = MeshInstance3D.new()
		var size: float = projectile.def.radius if projectile.def.shape in [WeaponDef.Shape.SPINNER, WeaponDef.Shape.BOUNCER] else projectile.def.projectile_radius
		mesh.mesh = Props.weapon_object(projectile.def.id, maxf(size, 0.25))
		model = mesh
	rec.holder = WeaponModel.rig(model)
	rec.squash = rec.holder.get_node("Squash") as Node3D
	rec.spin = rec.squash.get_node("Spin") as Node3D
	add_child(rec.holder)
	_recs[rec.id] = rec
	return rec


func _update(rec: Rec, projectile: WeaponSystem.Projectile) -> void:
	rec.holder.scale = Vector3.ONE * _ctx.view_scale
	rec.holder.position = Vector3(projectile.position.x, PROJECTILE_HEIGHT * _ctx.view_scale, projectile.position.y)
	rec.spin.rotation.y += _ctx.delta * TAU * (TOP_SPIN if rec.def.id == &"trumpo" else SPIN)


func _remove(id: int) -> void:
	var rec: Rec = _recs[id]
	_recs.erase(id)
	gone.emit(id, rec.def, rec.team, rec.holder.position)
	rec.holder.queue_free()
