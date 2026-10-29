class_name FxHeals
extends Node3D
## The heal: a green cross on the healed kid. Driven by the relayed `player_healed` (and `weapon_cast` for the caster), never
## by the offline-only `weapons.healed`, so it shows online too and offline only once.

## Where the cross starts above the ground, how far it rises and how long it lives (times the view scale).
const CROSS_START: float = 2.9
const CROSS_RISE: float = 0.9
const CROSS_TIME: float = 0.9


## One cross playing.
class Cross extends RefCounted:
	var node: Node3D
	var left: float


var _ctx: FxContext
var _crosses: Array[Cross] = []
var _healed: Array[int] = []
## Kids who cast a heal this frame.
var _casters: Array[int] = []


func setup(ctx: FxContext) -> void:
	_ctx = ctx


## Kid `id` gained `gained` HP this frame (queued; drawn at `flush`).
func on_healed(id: int, _gained: int) -> void:
	if not _healed.has(id):
		_healed.append(id)


## Kid `id` cast `weapon_id` this frame.
func on_cast(id: int, weapon_id: StringName) -> void:
	if _ctx.sim == null or not _ctx.sim.weapon_defs.has(weapon_id):
		return
	if _ctx.sim.weapon_defs[weapon_id].shape == WeaponDef.Shape.HEAL and not _casters.has(id):
		_casters.append(id)


## Draws this frame's heals. A heal cast that healed nobody (everyone at full HP, so no `player_healed`) still shows on its
## caster, so a cast never looks like nothing happened.
func flush() -> void:
	if _healed.is_empty():
		_healed.append_array(_casters)
	_casters.clear()
	for id: int in _healed:
		if _ctx.sim == null or not _ctx.sim.players.has(id):
			continue
		var cross: Cross = Cross.new()
		cross.node = WeaponModel.cross()
		cross.node.name = "HealCross"
		var at: Vector2 = _ctx.sim.players[id].position
		cross.node.position = Vector3(at.x, CROSS_START * _ctx.view_scale, at.y)
		cross.left = CROSS_TIME
		add_child(cross.node)
		_crosses.append(cross)
	_healed.clear()


func step() -> void:
	var still: Array[Cross] = []
	for cross: Cross in _crosses:
		cross.left -= _ctx.delta
		var left: float = cross.left / CROSS_TIME
		if left > 0.0:
			cross.node.position.y = (CROSS_START + CROSS_RISE * (1.0 - left)) * _ctx.view_scale
			cross.node.rotation.y += _ctx.delta * TAU
			# it pops in, then shrinks away
			cross.node.scale = Vector3.ONE * clampf(minf((1.0 - left) * 8.0, left * 2.5), 0.01, 1.0)
			still.append(cross)
		else:
			cross.node.queue_free()
	_crosses = still


## The crosses playing right now.
func crosses() -> Array[Node3D]:
	var nodes: Array[Node3D] = []
	for cross: Cross in _crosses:
		nodes.append(cross.node)
	return nodes


func fading_count() -> int:
	return _crosses.size()
