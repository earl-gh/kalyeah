class_name FxSnips
extends Node3D
## The scissors of the cone weapons (the snips), owned by FxCones: a pair stands beside the aim, flat face turned to the
## camera, handles clear of the caster and tips short of any kid on their line (they never pass through a kid). The small
## snip snaps shut once per snip and holds open between; the big snip opens wide and trembles through its wind-up, then
## snaps shut on the strike. FxCones hands each strike and wind-up its pair.

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
## through the blades' tips (round the caster, at the scissors' height), drawn on the closing frame and the one after
## (`SMEAR_FRAMES`), lighter at the start of its sweep and sweeping the other way on every other snip; later snips throw
## `SNIP_STARS` stars on each kid hit.
const SNIP_LUNGE_STEP: float = 0.15
const SMEAR_FRAMES: int = 2
const SNIP_STARS: int = 2
## The scissors pop in over `POP_FRAMES` frames, hold closed `HOLD_FRAMES` after the last snip, then shrink away over
## `EXIT_FRAMES` frames lifting `EXIT_LIFT` m.
const POP_FRAMES: int = 3
const HOLD_FRAMES: int = 1
const EXIT_FRAMES: int = 3
const EXIT_LIFT: float = 0.3
## The big snip: during the wind-up the scissors pull back `WINDUP_PULL` m and open to `ScissorsRig.WIDE_HEAVY` (ease-in);
## the last `TREMBLE_FRAMES` frames they tremble `TREMBLE_DEG` degrees at `TREMBLE_HZ`. On the strike they shut in one
## frame lunging `HEAVY_LUNGE` m, stretched `HEAVY_STRETCH` along their length on that frame (a smear), squash to `HEAVY_SQUASH`
## along it for `FxSquash.IMPACT_FRAMES` frames and spring back through an overshoot (`FxSquash.landing`), smear the whole
## wedge through their tips lunged out and, lighter, where they rested (an echo, `SMEAR_FRAMES` frames), hold closed until
## `HEAVY_HOLD_FRAMES` frames past the squash and shrink away over `HEAVY_EXIT_FRAMES` frames.
const WINDUP_PULL: float = 0.4
const TREMBLE_FRAMES: int = 3
const TREMBLE_DEG: float = 2.0
const TREMBLE_HZ: float = 15.0
const HEAVY_LUNGE: float = 0.5
const HEAVY_SQUASH: float = 0.75
const HEAVY_STRETCH: float = 1.5
## The echo arc of the big snip is this share of the strike arc's alpha.
const ECHO_ALPHA: float = 0.5
const HEAVY_HOLD_FRAMES: int = 3
const HEAVY_EXIT_FRAMES: int = 4


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
var _snips: Array[Snip] = []


func setup(ctx: FxContext) -> void:
	_ctx = ctx


## Poses every pair for this frame; frees the ones that are over.
func step() -> void:
	var playing: Array[Snip] = []
	for snip: Snip in _snips:
		if _pose(snip):
			playing.append(snip)
		else:
			snip.holder.queue_free()
	_snips = playing


## Pairs playing right now.
func count() -> int:
	return _snips.size()


## The scissors playing right now (tests).
func scissors() -> Array[ScissorsRig]:
	var rigs: Array[ScissorsRig] = []
	for snip: Snip in _snips:
		rigs.append(snip.rig)
	return rigs


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


## A new pair of scissors for `def` cast by `owner_id` from `origin` along `direction`.
func start(owner_id: int, def: WeaponDef, origin: Vector2, direction: Vector2) -> Snip:
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
	return camera.global_basis.z if camera != null else FxKit.CAMERA_BACK


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
			snip.squash.scale = Vector3.ONE
			if not _ctx.reduced:
				# the lunge frame is a smear along the blades; then the squash and its spring back
				snip.squash.scale = FxSquash.smear_scale(HEAVY_STRETCH) if since < 1 else FxSquash.along(FxSquash.landing(HEAVY_SQUASH, float(since - 1)))
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
			if frame >= int(FxCones.flash_time_for(snip.def, snip.n) / FxSquash.FRAME):
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
	_smear(snip, snip.closes % 2 == 0)
	if snip.closes < 2 or _ctx.sim == null:
		return
	for id: int in snip.hit_kids:
		var kid: PlayerState = _ctx.sim.players.get(id) as PlayerState
		if kid != null:
			_ctx.particles.puff(FxParticles.Preset.STARS, Vector3(kid.position.x, 0.0, kid.position.y), SNIP_STARS)


## The white smear arc across the wedge for a snip: round the caster at the scissors' height, through the blades' tips. A
## small snip's sweeps one way and the next snip's the other; the big snip's reaches `HEAVY_LUNGE` m further (where the
## lunge ends) with a lighter echo at the tips' rest.
func _smear(snip: Snip, mirror: bool = false) -> void:
	if _ctx.reduced:
		return
	var turn: Basis = Basis(Vector3.UP, atan2(-snip.aim.x, -snip.aim.z))
	var life: float = FxSquash.TRAIL_FRAMES * FxSquash.FRAME
	if snip.heavy:
		_ctx.ground.smear(Transform3D(turn, snip.caster), &"arc", snip.tips + HEAVY_LUNGE, Color.WHITE, life)
		_ctx.ground.smear(Transform3D(turn, snip.caster), &"arc", snip.tips, Color(1.0, 1.0, 1.0, ECHO_ALPHA), life)
		return
	var sweep: Basis = turn * Basis.from_scale(Vector3(-1.0 if mirror else 1.0, 1.0, 1.0))
	_ctx.ground.smear(Transform3D(sweep, snip.caster), &"arc", snip.tips, Color.WHITE, life)
