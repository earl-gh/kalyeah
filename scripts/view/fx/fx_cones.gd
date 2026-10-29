class_name FxCones
extends Node3D
## The cone weapons (the snips): the wedge flash in the thrower's team colour, whole from its first frame until it goes, the
## scissors that snip in it, and the warning of a delayed cone (the big snip) that fills out from the caster until the
## strike. Driven by the relayed `cone_struck` / `cone_winding` signals, so online plays the same.

## How long a wedge flash stays (seconds).
const CONE_FLASH_TIME: float = 0.3
## The scissors of a snip sit this far along the cone (fraction of its range), lunge forward by `SNIP_LUNGE` of it.
const SNIP_AT: float = 0.55
const SNIP_LUNGE: float = 0.2
## The warning of a delayed cone: its edge `WINDUP_RIM` m wide shows at once, the fill (`WINDUP_FILL` alpha) grows from the
## caster until the strike.
const WINDUP_RIM: float = 0.08
const WINDUP_FILL: float = 0.22
## The growing fill starts this small (a true zero breaks the transform).
const WINDUP_START: float = 0.05


## A wedge flash on the ground.
class Flash extends RefCounted:
	var node: MeshInstance3D
	var left: float
	var total: float
	var scissors: Node3D
	var start: Vector3
	var lunge: Vector3


## A delayed cone winding up.
class Windup extends RefCounted:
	var node: Node3D
	var fill: Node3D
	var owner_id: int
	var left: float
	var total: float


var _ctx: FxContext
var _flashes: Array[Flash] = []
var _windups: Array[Windup] = []


func setup(ctx: FxContext) -> void:
	_ctx = ctx


## The cone's strike: the wedge in the thrower's team colour (and the scissors for a snip).
func on_struck(owner_id: int, def: WeaponDef, origin: Vector2, direction: Vector2) -> void:
	var flash: Flash = Flash.new()
	flash.node = MeshInstance3D.new()
	flash.node.mesh = FxGround.wedge_mark(def, _ctx.team_color(_ctx.team_of(owner_id)), FxKit.FILL_ALPHA)
	flash.node.material_override = FxKit.overlay(FxKit.PRIO_DECAL)
	flash.node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	flash.node.position = Vector3(origin.x, 0.0, origin.y)
	flash.node.rotation.y = atan2(-direction.x, -direction.y)
	flash.total = CONE_FLASH_TIME
	flash.left = flash.total
	add_child(flash.node)
	if def.id == &"gunting_light" or def.id == &"gunting_heavy":
		var scissors: Node3D = WeaponModel.scissors(def.max_range * SNIP_AT * 1.2)
		scissors.name = "Scissors"
		flash.start = Vector3(0.0, 0.0, -def.max_range * SNIP_AT)
		flash.lunge = Vector3(0.0, 0.0, -def.max_range * SNIP_LUNGE)
		scissors.position = flash.start
		flash.node.add_child(scissors)
		flash.scissors = scissors
	_flashes.append(flash)


## A delayed cone was cast: its warning shows on the ground until the strike.
func on_winding(owner_id: int, def: WeaponDef, origin: Vector2, direction: Vector2) -> void:
	var marks: Array[ArrayMesh] = FxGround.windup_marks(def, _ctx.team_color(_ctx.team_of(owner_id)), WINDUP_RIM, WINDUP_FILL)
	var windup: Windup = Windup.new()
	windup.node = Node3D.new()
	windup.node.position = Vector3(origin.x, 0.0, origin.y)
	windup.node.rotation.y = atan2(-direction.x, -direction.y)
	windup.node.add_child(_overlay(marks[0]))
	windup.fill = _overlay(marks[1])
	windup.fill.scale = Vector3(WINDUP_START, 1.0, WINDUP_START)
	windup.node.add_child(windup.fill)
	windup.owner_id = owner_id
	windup.total = maxf(def.delay, 0.01)
	windup.left = def.delay
	add_child(windup.node)
	_windups.append(windup)


## Settles the snip count of this frame's strikes from the hits each kid took (`hits`: kid id -> hits this frame).
func resolve(_hits: Dictionary[int, int]) -> void:
	pass


func step() -> void:
	var still: Array[Flash] = []
	for flash: Flash in _flashes:
		flash.left -= _ctx.delta
		if flash.scissors != null:
			var progress: float = clampf(1.0 - flash.left / flash.total, 0.0, 1.0)
			flash.scissors.position = flash.start + flash.lunge * progress
		if flash.left > 0.0:
			still.append(flash)
		else:
			flash.node.queue_free()
	_flashes = still
	var winding: Array[Windup] = []
	for windup: Windup in _windups:
		windup.left -= _ctx.delta
		var caster: PlayerState = _ctx.sim.players.get(windup.owner_id) as PlayerState if _ctx.sim != null else null
		# the strike lands (or the caster was tagged first and it never will): the warning goes
		if windup.left <= 0.0 or caster == null or caster.tagged:
			windup.node.queue_free()
			continue
		var size: float = lerpf(WINDUP_START, 1.0, 1.0 - windup.left / windup.total)
		windup.fill.scale = Vector3(size, 1.0, size)
		winding.append(windup)
	_windups = winding


## The wedge flashes on the ground right now.
func flashes() -> Array[MeshInstance3D]:
	var nodes: Array[MeshInstance3D] = []
	for flash: Flash in _flashes:
		nodes.append(flash.node)
	return nodes


## Seconds the flash `node` has left.
func flash_time(node: MeshInstance3D) -> float:
	for flash: Flash in _flashes:
		if flash.node == node:
			return flash.left
	return 0.0


func windup_count() -> int:
	return _windups.size()


func live_count() -> int:
	return 0


func fading_count() -> int:
	return _flashes.size()


func _overlay(mesh: Mesh) -> MeshInstance3D:
	var node: MeshInstance3D = MeshInstance3D.new()
	node.mesh = mesh
	node.material_override = FxKit.overlay(FxKit.PRIO_DECAL)
	node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	return node
