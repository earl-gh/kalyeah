class_name FxCones
extends Node3D
## The cone weapons (the snips): the wedge flash in the thrower's team colour, whole from its first frame until it goes, the
## scissors that snip in it, and the warning of a delayed cone (the big snip) that fills out from the caster while the
## scissors open wide and tremble, then snap shut on the strike. Driven by the relayed `cone_struck` / `cone_winding`
## signals, so online plays the same; the snip count comes from the hits each kid took that frame.
## The scissors stand in the air beside the aim at chest height, flat face turned to the camera, handles clear of the
## caster and tips short of the nearest kid in the wedge (they never pass through a kid).

## How long the big snip's wedge flash stays (seconds); the small snip's follows its snips (`flash_time_for`).
const CONE_FLASH_TIME: float = 0.3
## The small snip's wedge stays `FLASH_TAIL` seconds past its snips, between `FLASH_MIN` and `FLASH_MAX` in all.
const FLASH_TAIL: float = 0.13
const FLASH_MIN: float = 0.3
const FLASH_MAX: float = 0.55
## The warning of a delayed cone: its edge `WINDUP_RIM` m wide shows at once, the fill (`WINDUP_FILL` alpha) grows from the
## caster until the strike.
const WINDUP_RIM: float = 0.08
const WINDUP_FILL: float = 0.30
## The growing fill starts this small (a true zero breaks the transform).
const WINDUP_START: float = 0.05
## A wind-up whose strike never came (a missed event online) is dropped this long after its delay (seconds).
const WINDUP_GRACE: float = 0.2


## A wedge flash on the ground.
class Flash extends RefCounted:
	var node: MeshInstance3D
	var left: float
	var total: float
	var def: WeaponDef
	var owner_id: int
	var fresh: bool = true
	var snip: FxSnips.Snip


## A delayed cone winding up.
class Windup extends RefCounted:
	var node: Node3D
	var fill: Node3D
	var owner_id: int
	var left: float
	var total: float
	var snip: FxSnips.Snip


var _ctx: FxContext
var _flashes: Array[Flash] = []
var _windups: Array[Windup] = []
## The scissors (FxSnips).
var snips: FxSnips


func setup(ctx: FxContext) -> void:
	_ctx = ctx
	snips = FxSnips.new()
	snips.name = "Snips"
	add_child(snips)
	snips.setup(ctx)


## How long a strike's wedge flash stays: the big snip `CONE_FLASH_TIME` (its scissors are done by then); the small snip
## past its `n` snips so the wedge outlives the scissors.
static func flash_time_for(def: WeaponDef, n: int) -> float:
	if def.delay > 0.0:
		return CONE_FLASH_TIME
	var snips: float = float(FxMotion.snip_frames(n)) / 30.0
	return clampf(maxf(FLASH_MIN, snips + FLASH_TAIL), FLASH_MIN, FLASH_MAX)


## The cone's strike: the wedge in the thrower's team colour (and the scissors for a snip).
func on_struck(owner_id: int, def: WeaponDef, origin: Vector2, direction: Vector2) -> void:
	var flash: Flash = Flash.new()
	flash.node = MeshInstance3D.new()
	flash.node.mesh = FxGround.wedge_mark(def, _ctx.team_color(_ctx.team_of(owner_id)), FxKit.FILL_ALPHA)
	flash.node.material_override = FxKit.overlay(FxKit.PRIO_DECAL)
	flash.node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	flash.node.position = Vector3(origin.x, 0.0, origin.y)
	flash.node.rotation.y = atan2(-direction.x, -direction.y)
	flash.def = def
	flash.owner_id = owner_id
	flash.total = flash_time_for(def, def.snips_min)
	flash.left = flash.total
	add_child(flash.node)
	_flashes.append(flash)
	if not WeaponModel.has_scissors(def.id):
		return
	if def.delay > 0.0:
		# the big snip: the scissors that opened during the wind-up snap shut now
		for windup: Windup in _windups:
			if windup.owner_id == owner_id and windup.snip != null and windup.snip.struck < 0.0:
				flash.snip = windup.snip
				windup.snip = null
				break
	if flash.snip == null:
		flash.snip = snips.start(owner_id, def, origin, direction)
	flash.snip.struck = INF
	# the strike lands: the warning on the ground goes now
	for windup: Windup in _windups:
		if windup.owner_id == owner_id and windup.left < windup.total:
			windup.left = minf(windup.left, 0.0)


## A delayed cone was cast: its warning shows on the ground until the strike, and the big scissors start to open.
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
	if WeaponModel.has_scissors(def.id):
		windup.snip = snips.start(owner_id, def, origin, direction)


## Settles the snip count of this frame's strikes from the hits each kid took (`hits`: kid id -> hits this frame).
func resolve(hits: Dictionary[int, int]) -> void:
	for flash: Flash in _flashes:
		if not flash.fresh:
			continue
		flash.fresh = false
		if flash.snip == null or flash.def.delay > 0.0:
			continue
		var team: int = _ctx.team_of(flash.owner_id)
		var most: int = 0
		for id: int in hits:
			if _ctx.team_of(id) != team:
				most = maxi(most, hits[id])
				flash.snip.hit_kids.append(id)
		flash.snip.n = clampi(most, flash.def.snips_min, flash.def.snips_max) if most > 0 else flash.def.snips_min
		flash.total = flash_time_for(flash.def, flash.snip.n)
		flash.left = flash.total


func step() -> void:
	var still: Array[Flash] = []
	for flash: Flash in _flashes:
		flash.left -= _ctx.delta
		if flash.left > 0.0:
			still.append(flash)
		else:
			flash.node.queue_free()
	_flashes = still
	var winding: Array[Windup] = []
	for windup: Windup in _windups:
		windup.left -= _ctx.delta
		var caster: PlayerState = _ctx.sim.players.get(windup.owner_id) as PlayerState if _ctx.sim != null else null
		# the strike landed (or the caster was tagged first and it never will): the warning goes
		var struck: bool = windup.left <= 0.0 and (windup.snip == null or windup.snip.struck >= 0.0)
		if struck or windup.left < -WINDUP_GRACE or caster == null or caster.tagged:
			windup.node.queue_free()
			if windup.snip != null and windup.snip.struck < 0.0:
				windup.snip.exit_from = _ctx.time
			continue
		var size: float = lerpf(WINDUP_START, 1.0, clampf(1.0 - windup.left / windup.total, 0.0, 1.0))
		windup.fill.scale = Vector3(size, 1.0, size)
		winding.append(windup)
	_windups = winding
	snips.step()


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


## The scissors playing right now (tests).
func scissors() -> Array[ScissorsRig]:
	return snips.scissors()


func windup_count() -> int:
	return _windups.size()


func live_count() -> int:
	return 0


func fading_count() -> int:
	return _flashes.size() + snips.count()


func _overlay(mesh: Mesh) -> MeshInstance3D:
	var node: MeshInstance3D = MeshInstance3D.new()
	node.mesh = mesh
	node.material_override = FxKit.overlay(FxKit.PRIO_DECAL)
	node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	return node
