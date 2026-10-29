class_name FxWalls
extends Node3D
## Draws the sim's shields keyed by id: the paper wall (papel shield) and the block of earth (langit lupa). The wall's
## width and place are the sim's exact segment; only its height, rise and fall are the view's. Its time left comes with the
## snapshot, so online plays the same.

## The paper wall's drawn height and depth (metres).
const SHIELD_HEIGHT: float = 1.6
const PAPER_THICKNESS: float = 0.3
## The soil block's drawn height (metres), how long it takes to rise and to collapse back (seconds).
const SOIL_HEIGHT: float = 1.7
const SOIL_RISE_TIME: float = 0.22
const SOIL_COLLAPSE_TIME: float = 0.6
## The flattest a collapsing block gets before it goes (a true zero breaks the transform).
const SOIL_FLAT: float = 0.04


## One drawn wall.
class Rec extends RefCounted:
	var id: int
	var def: WeaponDef
	var team: int
	var soil: bool
	var node: Node3D
	var body: Node3D


var _ctx: FxContext
var _recs: Dictionary[int, Rec] = {}


func setup(ctx: FxContext) -> void:
	_ctx = ctx


func sync(sim: MatchSim) -> void:
	var seen: Dictionary[int, bool] = {}
	for shield: WeaponSystem.Shield in sim.weapons.shields:
		seen[shield.id] = true
		var rec: Rec = _recs.get(shield.id) as Rec
		if rec == null:
			rec = _create(shield)
		_update(rec, shield)
	for id: int in _recs.keys():
		if not seen.has(id):
			_remove(id)


func live_count() -> int:
	return _recs.size()


func fading_count() -> int:
	return 0


func node_for(id: int) -> Node3D:
	var rec: Rec = _recs.get(id) as Rec
	return rec.body if rec != null else null


## A projectile of `team` left the sim at `at`: an enemy wall it was stopped by reacts.
func on_projectile_gone(_def: WeaponDef, _team: int, _at: Vector3) -> void:
	pass


func _create(shield: WeaponSystem.Shield) -> Rec:
	var rec: Rec = Rec.new()
	rec.id = shield.id
	rec.def = shield.def
	rec.team = shield.team
	rec.soil = shield.def != null and shield.def.look == &"soil"
	rec.node = Node3D.new()
	if rec.soil:
		rec.body = WeaponModel.soil(shield.half_width * 2.0, SOIL_HEIGHT, shield.half_width * 2.0)
	else:
		# a standing sheet of paper (the owner's model); its base sits on the ground
		rec.body = WeaponModel.shield(shield.half_width * 2.0, SHIELD_HEIGHT, PAPER_THICKNESS)
	rec.node.add_child(rec.body)
	add_child(rec.node)
	_recs[rec.id] = rec
	return rec


func _update(rec: Rec, shield: WeaponSystem.Shield) -> void:
	var grown: float = 1.0
	if rec.soil:
		# the block of earth rises, stands, then collapses back to the ground
		var age: float = shield.def.duration - shield.time_left
		grown = clampf(minf(age / SOIL_RISE_TIME, shield.time_left / SOIL_COLLAPSE_TIME), SOIL_FLAT, 1.0)
	rec.body.scale.y = grown
	rec.node.position = Vector3(shield.center.x, 0.0, shield.center.y)
	rec.node.rotation.y = -shield.along.angle()


func _remove(id: int) -> void:
	var rec: Rec = _recs[id]
	_recs.erase(id)
	rec.node.queue_free()
