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
## Where the scissors stand so the snip reads on a phone (a kid is about 70 px tall on screen): `SNIP_LEN` m long
## (`SNIP_LEN_HEAVY` for the big snip; at least 0.4 of a kid on screen), `SNIP_SIDE` m to the caster's side of the aim line
## (its right, or its left when the right points away from the camera) so they stand beside the column of kids, never on
## a head, their handles `SNIP_CASTER_CLEAR` m along the aim from the caster. Their middle is seen at `SCISSOR_Y` of the
## drawn kid's height (raised by however far the side step brings them toward the camera, never above `SCISSOR_MAX_Y`).
## A kid of the other team standing on their line (closer than `SNIP_TARGET_CLEAR` m plus the open blades' half spread)
## shortens them so the tips at full lunge stop `SNIP_TARGET_CLEAR` m short of it (never under `SNIP_MIN_LEN` m), and they
## always end inside the range at full lunge.
const SNIP_LEN: float = 2.1
const SNIP_LEN_HEAVY: float = 2.2
const SNIP_SIDE: float = 1.4
const SCISSOR_Y: float = 0.3
const SCISSOR_MAX_Y: float = 0.8
const SNIP_CASTER_CLEAR: float = 0.7
const SNIP_TARGET_CLEAR: float = 0.6
const SNIP_MIN_LEN: float = 1.0
## The small snip: each close lunges `SNIP_LUNGE_STEP` m along the aim (easing back as it opens); the smear arc sweeps
## through the blades' tips (round the caster, at the scissors' height) and lives `SMEAR_FRAMES` frames; later snips throw
## `SNIP_STARS` stars on each kid hit.
const SNIP_LUNGE_STEP: float = 0.15
const SMEAR_FRAMES: int = 3
const SNIP_STARS: int = 2
## The scissors pop in over `POP_FRAMES` frames, hold closed `HOLD_FRAMES` after the last snip, then shrink away over
## `EXIT_FRAMES` frames lifting `EXIT_LIFT` m.
const POP_FRAMES: int = 3
const HOLD_FRAMES: int = 1
const EXIT_FRAMES: int = 3
const EXIT_LIFT: float = 0.3
## The big snip: during the wind-up the scissors pull back `WINDUP_PULL` m and open to `ScissorsRig.WIDE_HEAVY` (ease-in);
## the last `TREMBLE_FRAMES` frames they tremble `TREMBLE_DEG` degrees at `TREMBLE_HZ`. On the strike they shut in one
## frame lunging `HEAVY_LUNGE` m, squash to `HEAVY_SQUASH` along their length for `FxSquash.IMPACT_FRAMES` frames, smear the
## whole wedge through their tips, hold closed `HEAVY_HOLD_FRAMES` frames and shrink away over
## `HEAVY_EXIT_FRAMES` frames.
const WINDUP_PULL: float = 0.4
const TREMBLE_FRAMES: int = 3
const TREMBLE_DEG: float = 2.0
const TREMBLE_HZ: float = 15.0
const HEAVY_LUNGE: float = 0.5
const HEAVY_SQUASH: float = 0.75
const HEAVY_HOLD_FRAMES: int = 3
const HEAVY_EXIT_FRAMES: int = 4
## A wind-up whose strike never came (a missed event online) is dropped this long after its delay (seconds).
const WINDUP_GRACE: float = 0.2
## Seen from the default camera (looking down 45 degrees toward -Z): its backward direction, used when no camera is found.
const CAMERA_BACK: Vector3 = Vector3(0.0, 0.70710678, 0.70710678)


## A wedge flash on the ground.
class Flash extends RefCounted:
	var node: MeshInstance3D
	var left: float
	var total: float
	var def: WeaponDef
	var owner_id: int
	var fresh: bool = true
	var snip: Snip


## A delayed cone winding up.
class Windup extends RefCounted:
	var node: Node3D
	var fill: Node3D
	var owner_id: int
	var left: float
	var total: float
	var snip: Snip


## One pair of scissors playing its snips.
class Snip extends RefCounted:
	var holder: Node3D
	var squash: Node3D
	var rig: ScissorsRig
	var def: WeaponDef
	var heavy: bool
	var rest: Vector3
	## The caster's ground point at the scissors' height (the smear arcs round it).
	var caster: Vector3
	## How far the blades' tips reach from the caster (the smear's radius).
	var tips: float
	var aim: Vector3
	## View time of its first frame (set when it is first drawn: the signal that made it came before this frame).
	var born: float = INF
	## View time of the strike (INF: struck, waiting for its first frame; -1: not struck).
	var struck: float = -1.0
	var exit_from: float = -1.0
	var n: int = 1
	var hit_kids: Array[int] = []
	var closes: int = 0
	var last_angle: float = 0.0


var _ctx: FxContext
var _flashes: Array[Flash] = []
var _windups: Array[Windup] = []
var _snips: Array[Snip] = []


func setup(ctx: FxContext) -> void:
	_ctx = ctx


## How long a strike's wedge flash stays: the big snip `CONE_FLASH_TIME` (its scissors are done by then); the small snip
## past its `n` snips so the wedge outlives the scissors.
static func flash_time_for(def: WeaponDef, n: int) -> float:
	if def.delay > 0.0:
		return CONE_FLASH_TIME
	var snips: float = float(n * FxMotion.snip_period(n)) / 30.0
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
		flash.snip = _snip(owner_id, def, origin, direction)
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
		windup.snip = _snip(owner_id, def, origin, direction)


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
	var playing: Array[Snip] = []
	for snip: Snip in _snips:
		if _pose(snip):
			playing.append(snip)
		else:
			snip.holder.queue_free()
	_snips = playing


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
	var rigs: Array[ScissorsRig] = []
	for snip: Snip in _snips:
		rigs.append(snip.rig)
	return rigs


func windup_count() -> int:
	return _windups.size()


func live_count() -> int:
	return 0


func fading_count() -> int:
	return _flashes.size() + _snips.size()


## Where a pair of scissors for `def` stands: [their length, their middle's distance along the aim] (on the line
## `side_for` beside the aim). Inside the range at full lunge, and short of any kid of the other team standing on their line.
func placement(owner_id: int, def: WeaponDef, origin: Vector2, direction: Vector2) -> Vector2:
	var length: float = _fit(owner_id, def, origin, direction, side_for(owner_id, def, origin, direction))
	return Vector2(length, SNIP_CASTER_CLEAR + length / 2.0)


## The step from the caster to the scissors' line: to the caster's right or left (never away from the camera), whichever
## leaves the scissors longest (a kid standing on one line shortens them there); the right on a tie.
func side_for(owner_id: int, def: WeaponDef, origin: Vector2, direction: Vector2) -> Vector3:
	var aim: Vector3 = Vector3(direction.x, 0.0, direction.y).normalized()
	var right: Vector3 = aim.cross(Vector3.UP).normalized() * SNIP_SIDE
	var back: Vector3 = _camera_back()
	var flat: Vector3 = Vector3(back.x, 0.0, back.z)
	var best: Vector3 = side_of(aim)
	var longest: float = _fit(owner_id, def, origin, direction, best)
	for side: Vector3 in [right, -right]:
		if side.dot(flat) < -0.01 or side.is_equal_approx(best):
			continue
		var length: float = _fit(owner_id, def, origin, direction, side)
		if length > longest + 0.001:
			best = side
			longest = length
	return best


## How long `def`'s scissors can be on the line `side` beside the aim (metres).
func _fit(owner_id: int, def: WeaponDef, origin: Vector2, direction: Vector2, side: Vector3) -> float:
	var lunge: float = HEAVY_LUNGE if def.delay > 0.0 else SNIP_LUNGE_STEP
	var length: float = minf(SNIP_LEN_HEAVY if def.delay > 0.0 else SNIP_LEN, def.max_range - SNIP_CASTER_CLEAR - lunge)
	var aim: Vector2 = direction.normalized()
	var line: Vector2 = origin + Vector2(side.x, side.z)
	var team: int = _ctx.team_of(owner_id)
	var reach: float = SNIP_TARGET_CLEAR + half_spread(def, length)
	if _ctx.sim != null:
		for id: int in _ctx.sim.players:
			var kid: PlayerState = _ctx.sim.players[id]
			if kid.team == team or kid.tagged:
				continue
			var offset: Vector2 = kid.position - line
			var along: float = offset.dot(aim)
			if along > 0.0 and absf(offset.dot(aim.orthogonal())) < reach:
				length = minf(length, maxf(along - SNIP_TARGET_CLEAR - SNIP_CASTER_CLEAR - lunge, SNIP_MIN_LEN))
	return length


## Half the distance between the tips of `def`'s scissors `length` m long at their widest (metres).
static func half_spread(def: WeaponDef, length: float) -> float:
	var widest: float = ScissorsRig.WIDE_HEAVY if def.delay > 0.0 else ScissorsRig.WIDE
	return length * ScissorsRig.TIP_ARM * sin(deg_to_rad(ScissorsRig.TIP_ANGLE + widest))


## The preferred step from the caster to the scissors' line for an aim `aim` (3D, unit): `SNIP_SIDE` m to the caster's
## right, or to its left when its right points away from the camera (they would hide behind the kids).
func side_of(aim: Vector3) -> Vector3:
	var right: Vector3 = aim.cross(Vector3.UP).normalized()
	var back: Vector3 = _camera_back()
	if right.dot(Vector3(back.x, 0.0, back.z)) < -0.01:
		right = -right
	return right * SNIP_SIDE


func _snip(owner_id: int, def: WeaponDef, origin: Vector2, direction: Vector2) -> Snip:
	var place: Vector2 = placement(owner_id, def, origin, direction)
	var snip: Snip = Snip.new()
	snip.def = def
	snip.heavy = def.delay > 0.0
	snip.n = def.snips_min
	snip.aim = Vector3(direction.x, 0.0, direction.y).normalized()
	var side: Vector3 = side_for(owner_id, def, origin, direction)
	snip.caster = Vector3(origin.x, scissor_height(side), origin.y) + side
	snip.rest = snip.caster + snip.aim * place.y
	snip.tips = place.y + place.x / 2.0
	snip.holder = Node3D.new()
	snip.holder.name = "Scissors"
	snip.holder.basis = _facing(snip.aim)
	snip.holder.position = snip.rest
	snip.squash = Node3D.new()
	snip.squash.name = "Squash"
	snip.holder.add_child(snip.squash)
	snip.rig = ScissorsRig.make(place.x, _ctx.team_color(_ctx.team_of(owner_id)))
	snip.squash.add_child(snip.rig)
	snip.rig.set_open(ScissorsRig.CLOSED if _ctx.reduced and not snip.heavy else (ScissorsRig.WIDE_HEAVY if _ctx.reduced else ScissorsRig.WIDE))
	if snip.heavy and not _ctx.reduced:
		snip.rig.set_open(ScissorsRig.OPEN_AUTHORED)
	snip.last_angle = snip.rig.angle()
	add_child(snip.holder)
	_snips.append(snip)
	return snip


## How high the scissors' middle stands after the side step `side`: seen at chest height on screen (a step toward the
## camera lowers a thing on screen, so it rises by as much), never above `SCISSOR_MAX_Y` of the drawn kid.
func scissor_height(side: Vector3) -> float:
	var tall: float = KidModel.KID_HEIGHT * _ctx.view_scale
	var back: Vector3 = _camera_back()
	var toward: float = maxf(side.dot(Vector3(back.x, 0.0, back.z).normalized()), 0.0)
	return clampf(SCISSOR_Y * tall + toward, SCISSOR_Y * tall, SCISSOR_MAX_Y * tall)


## The direction from the scene toward the camera.
func _camera_back() -> Vector3:
	var camera: Camera3D = get_viewport().get_camera_3d() if is_inside_tree() else null
	return camera.global_basis.z if camera != null else CAMERA_BACK


## The scissors' frame: tips along the aim, the flat face turned to the camera as far as it can while keeping the aim (so
## the blades are never seen edge-on, whatever the aim).
func _facing(aim: Vector3) -> Basis:
	var back: Vector3 = _camera_back()
	var face: Vector3 = (back - aim * back.dot(aim)).normalized()
	if face.length_squared() < 0.5:
		face = Vector3.UP
	return Basis(aim.cross(face).normalized(), aim, face)


## Poses `snip` for now; false once it is over.
func _pose(snip: Snip) -> bool:
	if snip.born == INF:
		snip.born = _ctx.time
	if snip.struck == INF:
		snip.struck = _ctx.time
		if snip.heavy:
			_smear(snip)
	var frame: int = int((_ctx.time - snip.born) / FxSquash.FRAME + 0.001)
	var offset: float = 0.0
	var size: float = 1.0
	var angle: float = snip.rig.angle()
	var exit: float = -1.0
	if snip.heavy:
		var exit_frames: int = HEAVY_EXIT_FRAMES
		if snip.struck >= 0.0:
			var since: int = int((_ctx.time - snip.struck) / FxSquash.FRAME + 0.001)
			angle = ScissorsRig.CLOSED
			offset = 0.0 if _ctx.reduced else HEAVY_LUNGE
			snip.squash.scale = FxSquash.along(HEAVY_SQUASH) if since < FxSquash.IMPACT_FRAMES and not _ctx.reduced else Vector3.ONE
			var exit_at: int = FxSquash.IMPACT_FRAMES + HEAVY_HOLD_FRAMES
			if since >= exit_at:
				exit = float(since - exit_at) / float(exit_frames)
		elif snip.exit_from >= 0.0:
			exit = (_ctx.time - snip.exit_from) / (exit_frames * FxSquash.FRAME)
		else:
			# the wind-up: open wide (ease-in) and pull back, trembling at the very end
			var t: float = clampf((_ctx.time - snip.born) / maxf(snip.def.delay, 0.01), 0.0, 1.0)
			angle = ScissorsRig.WIDE_HEAVY if _ctx.reduced else lerpf(ScissorsRig.OPEN_AUTHORED, ScissorsRig.WIDE_HEAVY, t * t)
			offset = 0.0 if _ctx.reduced else -WINDUP_PULL * (1.0 - (1.0 - t) * (1.0 - t))
			var left: float = snip.def.delay - (_ctx.time - snip.born)
			if not _ctx.reduced and left < TREMBLE_FRAMES * FxSquash.FRAME:
				angle += TREMBLE_DEG * sin(TAU * TREMBLE_HZ * (_ctx.time - snip.born))
			if not _ctx.reduced:
				size = FxSquash.pop(float(frame) / float(POP_FRAMES))
	else:
		if _ctx.reduced:
			# shown closed and still for the wedge's life
			angle = ScissorsRig.CLOSED
			if frame >= int(flash_time_for(snip.def, snip.n) / FxSquash.FRAME):
				return false
		else:
			angle = FxMotion.snip_angle(frame, snip.n)
			var opened: float = inverse_lerp(ScissorsRig.CLOSED, ScissorsRig.WIDE, angle)
			offset = SNIP_LUNGE_STEP * (1.0 - clampf(opened, 0.0, 1.0))
			if frame < POP_FRAMES:
				size = FxSquash.pop(float(frame + 1) / float(POP_FRAMES))
			var exit_at: int = FxMotion.snip_frames(snip.n) + HOLD_FRAMES
			if frame >= exit_at:
				exit = float(frame - exit_at) / float(EXIT_FRAMES)
			if angle <= ScissorsRig.CLOSED and snip.last_angle > ScissorsRig.CLOSED:
				_on_close(snip)
	if exit >= 1.0:
		return false
	if exit >= 0.0:
		size *= 1.0 - exit
	snip.rig.set_open(angle)
	snip.last_angle = angle
	snip.holder.position = snip.rest + snip.aim * offset + Vector3(0.0, EXIT_LIFT * maxf(exit, 0.0), 0.0)
	snip.holder.scale = Vector3.ONE * maxf(size, 0.01)
	return true


## A small snip closed: a smear across the wedge and, from the second snip on, stars on each kid it hit.
func _on_close(snip: Snip) -> void:
	snip.closes += 1
	_smear(snip)
	if snip.closes < 2 or _ctx.sim == null:
		return
	for id: int in snip.hit_kids:
		var kid: PlayerState = _ctx.sim.players.get(id) as PlayerState
		if kid != null:
			_ctx.particles.puff(FxParticles.Preset.STARS, Vector3(kid.position.x, 0.0, kid.position.y), SNIP_STARS)


## The white smear arc across the wedge for a snip: round the caster at the scissors' height, through the blades' tips.
func _smear(snip: Snip) -> void:
	if _ctx.reduced:
		return
	var xform: Transform3D = Transform3D(Basis(Vector3.UP, atan2(-snip.aim.x, -snip.aim.z)), snip.caster)
	_ctx.ground.smear(xform, &"arc", snip.tips, Color.WHITE, SMEAR_FRAMES * FxSquash.FRAME)


func _overlay(mesh: Mesh) -> MeshInstance3D:
	var node: MeshInstance3D = MeshInstance3D.new()
	node.mesh = mesh
	node.material_override = FxKit.overlay(FxKit.PRIO_DECAL)
	node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	return node
