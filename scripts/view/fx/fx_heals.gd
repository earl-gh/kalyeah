class_name FxHeals
extends Node3D
## The heal: on the healed kid a green ring grows on the ground, a green cross pops up in front of the chest facing the
## camera (never edge-on, never up at the bars) and rises a little as it shrinks away, with plus sparkles rising beside the
## kid; healing someone else draws a short green streak from the caster to them. Driven by the relayed `player_healed` (and
## `weapon_cast` for the caster), never by the offline-only `weapons.healed`, so it shows online too and offline once. A heal
## cast that healed nobody (all at full HP) still shows on its caster. Reduce motion: the cross pops in still, holds, goes.

## The ground ring grows from `RING_FROM` to `RING_TO` m over `RING_FRAMES` frames (the aim preview's friend green).
const RING_FROM: float = 0.5
const RING_TO: float = 1.0
const RING_FRAMES: int = 10
## The cross: `CROSS_FRONT` m in front of the kid toward the camera, tipped to face it, its middle seen on screen at
## `CROSS_SCREEN_FROM` of the drawn kid's height rising to `CROSS_SCREEN_TO` over `CROSS_TIME` s (the chest; a thing in front
## of the kid is seen lower, so its height is solved from the camera); it pops in over `FxSquash.POP_FRAMES` frames and has
## shrunk away by `CROSS_END` s (with Reduce motion it holds `CROSS_HOLD_REDUCED` s and goes).
const CROSS_SCREEN_FROM: float = 0.47
const CROSS_SCREEN_TO: float = 0.5
const CROSS_TIME: float = 0.6
const CROSS_END: float = 0.7
const CROSS_FRONT: float = 0.7
const CROSS_HOLD_REDUCED: float = 0.5
## The cross rises fast as it pops in: a green trail `RISE_LENGTH` m long runs up to it (just behind it, lighter toward its
## foot) for `FxSquash.SMEAR_FRAMES` frames and the cross is smeared up along it (FxSquash.smear_stretch at the speed that covers
## the trail in those frames), crisp after. Reduce motion: none.
const RISE_LENGTH: float = 1.2
const RISE_BEHIND: float = 0.1
## The trail is this share of the cross's width across.
const CROSS_WIDTH_SHARE: float = 0.9
## Plus sparkles rising beside and behind the kid.
const SPARKLES: int = 4
## The streak from the caster to the kid they healed: green, at `STREAK_Y` m, for `STREAK_FRAMES` frames.
const STREAK_Y: float = 1.3
const STREAK_FRAMES: int = 3


## One cross playing.
class Cross extends RefCounted:
	var node: Node3D
	var born: float
	var base: Vector3
	var front: Vector3


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


## Draws this frame's heals.
func flush() -> void:
	if _ctx.sim == null:
		_healed.clear()
		_casters.clear()
		return
	var patients: Array[int] = _healed.duplicate()
	if patients.is_empty():
		patients.append_array(_casters)
	for id: int in patients:
		if _ctx.sim.players.has(id):
			_heal(_ctx.sim.players[id].position)
	# healing someone else: a green streak from the caster to them
	for caster: int in _casters:
		for id: int in _healed:
			if id != caster and _ctx.sim.players.has(caster) and _ctx.sim.players.has(id) and _ctx.team_of(id) == _ctx.team_of(caster):
				_streak(_ctx.sim.players[caster].position, _ctx.sim.players[id].position)
				break
	_healed.clear()
	_casters.clear()


func _heal(at: Vector2) -> void:
	_ctx.ground.ring(at, RING_FROM, RING_TO, AimIndicator.REACH_FRIEND, RING_FRAMES * FxSquash.FRAME, _ctx.reduced)
	_ctx.particles.puff(FxParticles.Preset.PLUS, Vector3(at.x, 0.0, at.y), SPARKLES)
	var back: Vector3 = _camera_back()
	var front: Vector3 = Vector3(back.x, 0.0, back.z).normalized() * CROSS_FRONT
	var cross: Cross = Cross.new()
	cross.front = front
	cross.node = WeaponModel.cross()
	cross.node.name = "HealCross"
	cross.base = Vector3(at.x, 0.0, at.y) + front
	cross.born = _ctx.time
	# its face (model +Z) turned to the camera: upright for the eye, never edge-on
	cross.node.basis = Basis.looking_at(-back, Vector3.UP)
	add_child(cross.node)
	_crosses.append(cross)
	_pose(cross)
	if not _ctx.reduced:
		var head: Vector3 = cross.node.position - back * RISE_BEHIND
		_ctx.ground.trail(head - _camera_up() * RISE_LENGTH, head, CROSS_WIDTH_SHARE * WeaponModel.CROSS_WIDTH * _ctx.view_scale, Palette.GUAVA, FxSquash.TRAIL_FRAMES * FxSquash.FRAME)


func _streak(from: Vector2, to: Vector2) -> void:
	if _ctx.reduced:
		return
	var offset: Vector2 = to - from
	var middle: Vector2 = (from + to) / 2.0
	var xform: Transform3D = Transform3D(Basis(Vector3.UP, -offset.angle()), Vector3(middle.x, STREAK_Y, middle.y))
	_ctx.ground.smear(xform, &"streak", offset.length(), Palette.GUAVA, STREAK_FRAMES * FxSquash.FRAME)


func step() -> void:
	var still: Array[Cross] = []
	for cross: Cross in _crosses:
		if _pose(cross):
			still.append(cross)
		else:
			cross.node.queue_free()
	_crosses = still


## Poses `cross` for now; false once it is over.
func _pose(cross: Cross) -> bool:
	var age: float = _ctx.time - cross.born
	var size: float = _ctx.view_scale
	var y: float = _height_for(cross, CROSS_SCREEN_FROM)
	if _ctx.reduced:
		if age >= CROSS_HOLD_REDUCED:
			return false
	else:
		if age >= CROSS_END:
			return false
		y = _height_for(cross, lerpf(CROSS_SCREEN_FROM, CROSS_SCREEN_TO, clampf(age / CROSS_TIME, 0.0, 1.0)))
		var pop: float = FxSquash.pop(age / (FxSquash.POP_FRAMES * FxSquash.FRAME))
		var fade: float = clampf((CROSS_END - age) / (CROSS_END - CROSS_TIME + 0.0001), 0.0, 1.0)
		size *= maxf(minf(pop, fade) if age > CROSS_TIME else pop, 0.01)
	cross.node.position = cross.base + Vector3(0.0, y, 0.0)
	var smear: float = 1.0
	if not _ctx.reduced:
		var speed: float = RISE_LENGTH / (FxSquash.SMEAR_FRAMES * FxSquash.FRAME)
		smear = FxSquash.smear_stretch(speed, WeaponModel.CROSS_WIDTH * _ctx.view_scale, age / FxSquash.FRAME)
	cross.node.scale = Vector3.ONE * size * FxSquash.smear_scale(smear)
	return true


## The height above the street at which `cross` (standing `cross.front` in front of its kid) is seen at `share` of the
## drawn kid's height on screen.
func _height_for(cross: Cross, share: float) -> float:
	var up: Vector3 = _camera_up()
	var tall: float = KidModel.KID_HEIGHT * _ctx.view_scale
	return (share * tall * up.y - cross.front.dot(up)) / maxf(up.y, 0.01)


## The camera's screen-up direction in the world.
func _camera_up() -> Vector3:
	var camera: Camera3D = get_viewport().get_camera_3d() if is_inside_tree() else null
	return camera.global_basis.y if camera != null else FxKit.CAMERA_BACK.cross(Vector3.RIGHT).normalized()


## The direction from the scene toward the camera.
func _camera_back() -> Vector3:
	var camera: Camera3D = get_viewport().get_camera_3d() if is_inside_tree() else null
	return camera.global_basis.z if camera != null else FxKit.CAMERA_BACK


## The crosses playing right now.
func crosses() -> Array[Node3D]:
	var nodes: Array[Node3D] = []
	for cross: Cross in _crosses:
		nodes.append(cross.node)
	return nodes


func fading_count() -> int:
	return _crosses.size()
