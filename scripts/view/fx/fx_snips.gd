class_name FxSnips
extends Node3D
## The scissors of the cone weapons (the snips), owned by FxCones. Two plays, both view only (the sim's wedge, delay and damage are
## untouched; every cut lands where and when the sim's does):
## - The small snip is a volley of small scissors, one per snip. Each appears above the caster's head, a little behind it, over the
##   caster's shoulder line (the first over the RIGHT shoulder, the next over the LEFT, and so on, alternating), flies forward and
##   down to the strike point on the aim closing (open to shut: the cut) and arrives shut. A new one is launched every
##   `FxMotion.snip_period` frames; the sim hits at the cast, so the flight is a short `FxMotion.SNIP_FLIGHT_FRAMES`.
## - The big snip is one huge pair of scissors (`HEAVY_SCALE` times the old size) with the caster standing at the screw and the
##   blades reaching out along the aim at the caster's waist: they open wide and pull back through the sim's wind-up (the delay),
##   then snap shut on the strike (the sim's strike), the blades' tips reaching the sim's range.
## Flat face turned to the camera, outlined in the thrower's team colour.

## The small scissors are `SNIP_LEN` m long. They start `SNIP_SHOULDER` m to the caster's side (its right or left shoulder, the
## line across the aim) and `SNIP_BEHIND` m behind it, their middle at `SPAWN_Y` of the drawn kid's height (above the head: the
## head's top is 1.0), and arrive with their tips `SNIP_REACH` of the weapon's range along the aim, at `STRIKE_Y` of the kid's height
## (the chest), on the aim line. The flight eases out (fast, then settling) so they land.
const SNIP_LEN: float = 2.1
const SNIP_SHOULDER: float = 0.9
const SNIP_BEHIND: float = 0.6
const SPAWN_Y: float = 1.5
const STRIKE_Y: float = 0.4
const SNIP_REACH: float = 0.85
## Reduce motion: the scissors stand shut at the strike point, `SNIP_STILL_SIDE` m to each side in turn and `SNIP_STILL_STEP` m
## further back each, for the wedge's life.
const SNIP_STILL_SIDE: float = 0.3
const SNIP_STILL_STEP: float = 0.25
## Each scissors' smear arc sweeps the wedge at the strike radius and the stars thrown on a kid it hit (from the second snip on).
const SMEAR_FRAMES: int = 2
const SNIP_STARS: int = 2
## The scissors pop in over `POP_FRAMES` frames, hold shut `HOLD_FRAMES` after arriving, then shrink away over `EXIT_FRAMES` frames
## lifting `EXIT_LIFT` m.
const POP_FRAMES: int = 3
const HOLD_FRAMES: int = 1
const EXIT_FRAMES: int = 3
const EXIT_LIFT: float = 0.3
## The big snip: `HEAVY_SCALE` times the old 2.2 m. The screw stands on the caster at `HEAVY_WAIST` of the drawn kid's height (the
## waist), so the blades reach `ScissorsRig.tip_reach` (3.2 m) along the aim and the handles trail behind the caster. During the
## wind-up the scissors pull back `WINDUP_PULL` m and open to `ScissorsRig.WIDE_HEAVY` (ease-in); the last `TREMBLE_FRAMES` frames
## they tremble `TREMBLE_DEG` degrees at `TREMBLE_HZ`. On the strike they shut in one frame lunging at most `HEAVY_LUNGE` m (never
## so far that the tips pass the weapon's range), stretched `HEAVY_STRETCH` along their length on that frame (a smear), squash to
## `HEAVY_SQUASH` along it for `FxSquash.IMPACT_FRAMES` frames and spring back through an overshoot (`FxSquash.landing`), smear
## the whole wedge through their tips lunged out and, lighter, where they rested (an echo, `SMEAR_FRAMES` frames), hold shut until
## `HEAVY_HOLD_FRAMES` frames past the squash and shrink away over `HEAVY_EXIT_FRAMES` frames.
const SNIP_LEN_HEAVY: float = 2.2
const HEAVY_SCALE: float = 3.0
const HEAVY_WAIST: float = 0.45
const WINDUP_PULL: float = 0.4
const TREMBLE_FRAMES: int = 3
const TREMBLE_DEG: float = 2.0
const TREMBLE_HZ: float = 15.0
const HEAVY_LUNGE: float = 0.5
const HEAVY_SQUASH: float = 0.75
const HEAVY_STRETCH: float = 1.1
## The echo arc of the big snip is this share of the strike arc's alpha.
const ECHO_ALPHA: float = 0.5
const HEAVY_HOLD_FRAMES: int = 3
const HEAVY_EXIT_FRAMES: int = 4


## One scissors of a volley (the big snip has just the one).
class Part extends RefCounted:
	var holder: Node3D
	var squash: Node3D
	var rig: ScissorsRig
	## +1: launched over the caster's right shoulder, -1: over the left.
	var side: int = 1
	## The frame (after the volley's first) it is launched, and where it starts and ends (its middle, in the world).
	var launch: int = 0
	var from: Vector3
	var to: Vector3
	var arrived: bool = false
	var last_angle: float = 0.0


## One volley (or the big snip) playing.
class Snip extends RefCounted:
	var def: WeaponDef
	var heavy: bool
	var parts: Array[Part] = []
	## The caster's ground point; `centre` is the same at the strike's height (the smear arcs round it).
	var origin: Vector2
	var centre: Vector3
	## The unit aim, the caster's right (aim x up) and how far the blades' tips reach from the caster (the smear's radius).
	var aim: Vector3
	var right: Vector3
	var tips: float
	## The big snip: how far its strike lunges.
	var lunge: float = 0.0
	var team_color: Color
	## View time of its first frame (set when it is first drawn: the signal that made it came before this frame).
	var born: float = INF
	## View time of the strike (INF: struck, waiting for its first frame; -1: not struck).
	var struck: float = -1.0
	var exit_from: float = -1.0
	var n: int = 1
	var hit_kids: Array[int] = []


var _ctx: FxContext
var _snips: Array[Snip] = []


func setup(ctx: FxContext) -> void:
	_ctx = ctx


## Poses every volley for this frame; frees the ones that are over.
func step() -> void:
	var playing: Array[Snip] = []
	for snip: Snip in _snips:
		if _pose(snip):
			playing.append(snip)
		else:
			for part: Part in snip.parts:
				part.holder.queue_free()
	_snips = playing


## Volleys playing right now.
func count() -> int:
	return _snips.size()


## The scissors playing right now, in launch order (tests).
func scissors() -> Array[ScissorsRig]:
	var rigs: Array[ScissorsRig] = []
	for snip: Snip in _snips:
		for part: Part in snip.parts:
			rigs.append(part.rig)
	return rigs


## The parts (each scissors' holder, launch frame, side and path) of every volley playing right now (tests).
func parts() -> Array[Part]:
	var found: Array[Part] = []
	for snip: Snip in _snips:
		found.append_array(snip.parts)
	return found


## How long `def`'s scissors are, metres: the small ones `SNIP_LEN`, the big pair `HEAVY_SCALE` times `SNIP_LEN_HEAVY`.
static func length_of(def: WeaponDef) -> float:
	return SNIP_LEN_HEAVY * HEAVY_SCALE if def.delay > 0.0 else SNIP_LEN


## How far the big snip's tips may lunge: `HEAVY_LUNGE`, less if that would take them past `def`'s range.
static func lunge_of(def: WeaponDef) -> float:
	return clampf(def.max_range - ScissorsRig.tip_reach(length_of(def)), 0.0, HEAVY_LUNGE)


## How far along the aim a small scissors' tips arrive (metres from the caster).
static func strike_distance(def: WeaponDef) -> float:
	return def.max_range * SNIP_REACH


## The height of the strike's centre: the cut at the chest for the small scissors, the waist for the big pair.
func strike_height(heavy: bool) -> float:
	return (HEAVY_WAIST if heavy else STRIKE_Y) * KidModel.KID_HEIGHT * _ctx.view_scale


## Where a small scissors launched over the shoulder `side` (+1 right, -1 left) of a caster at `origin` aiming `aim` (unit, 3D)
## starts: above the head, behind, on the shoulder line. Its middle.
func launch_point(origin: Vector2, aim: Vector3, side: int) -> Vector3:
	var right: Vector3 = aim.cross(Vector3.UP).normalized()
	var tall: float = KidModel.KID_HEIGHT * _ctx.view_scale
	return Vector3(origin.x, SPAWN_Y * tall, origin.y) - aim * SNIP_BEHIND + right * (SNIP_SHOULDER * float(side))


## Where a small scissors of `def` arrives (its middle: the tips are `strike_distance` out, on the aim line).
func arrival_point(origin: Vector2, aim: Vector3, def: WeaponDef) -> Vector3:
	return Vector3(origin.x, strike_height(false), origin.y) + aim * (strike_distance(def) - SNIP_LEN / 2.0)


## A new volley (or big pair) for `def` cast by `owner_id` from `origin` along `direction`.
func start(owner_id: int, def: WeaponDef, origin: Vector2, direction: Vector2) -> Snip:
	var snip: Snip = Snip.new()
	snip.def = def
	snip.heavy = def.delay > 0.0
	snip.n = 1 if snip.heavy else def.snips_min
	snip.origin = origin
	snip.aim = Vector3(direction.x, 0.0, direction.y).normalized()
	snip.right = snip.aim.cross(Vector3.UP).normalized()
	snip.centre = Vector3(origin.x, strike_height(snip.heavy), origin.y)
	snip.team_color = _ctx.team_color(_ctx.team_of(owner_id))
	if snip.heavy:
		snip.lunge = lunge_of(def)
		snip.tips = ScissorsRig.tip_reach(length_of(def))
		_add_part(snip, 1, 0, snip.centre, snip.centre)
	else:
		snip.tips = strike_distance(def)
	_snips.append(snip)
	return snip


## Adds one scissors to `snip`, launched `launch` frames in on shoulder `side`, flying `from` to `to` (its middle).
func _add_part(snip: Snip, side: int, launch: int, from: Vector3, to: Vector3) -> Part:
	var part: Part = Part.new()
	part.side = side
	part.launch = launch
	part.from = from
	part.to = to
	var length: float = length_of(snip.def)
	part.holder = Node3D.new()
	part.holder.name = "Scissors"
	part.holder.basis = _facing(snip.aim)
	part.holder.position = from
	part.squash = Node3D.new()
	part.squash.name = "Squash"
	part.holder.add_child(part.squash)
	part.rig = ScissorsRig.make(length, snip.team_color)
	# the big pair turns about its screw (the caster stands on it), the small ones about their middle
	part.rig.position = -ScissorsRig.screw_offset(length) if snip.heavy else Vector3.ZERO
	part.squash.add_child(part.rig)
	part.holder.visible = false
	if snip.heavy:
		part.rig.set_open(ScissorsRig.WIDE_HEAVY if _ctx.reduced else ScissorsRig.OPEN_AUTHORED)
	else:
		part.rig.set_open(ScissorsRig.CLOSED if _ctx.reduced else ScissorsRig.WIDE)
	part.last_angle = part.rig.angle()
	add_child(part.holder)
	snip.parts.append(part)
	return part


## The small volley's scissors, built on its first frame (the count is known by then: FxCones.resolve settled it).
func _build_volley(snip: Snip) -> void:
	var period: int = FxMotion.snip_period(snip.n)
	for k: int in snip.n:
		# the first on the right, then the left, alternating
		var side: int = 1 if k % 2 == 0 else -1
		var from: Vector3 = launch_point(snip.origin, snip.aim, side)
		var to: Vector3 = arrival_point(snip.origin, snip.aim, snip.def)
		if _ctx.reduced:
			from = to + snip.right * (SNIP_STILL_SIDE * float(side)) - snip.aim * (SNIP_STILL_STEP * float(k))
			to = from
		_add_part(snip, side, k * period, from, to)


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
		if not snip.heavy:
			_build_volley(snip)
	if snip.struck == INF:
		snip.struck = _ctx.time
		if snip.heavy:
			_smear(snip, snip.parts[0])
	var frame: int = int((_ctx.time - snip.born) / FxSquash.FRAME + 0.001)
	if snip.heavy:
		return _pose_heavy(snip, snip.parts[0], frame)
	var alive: bool = false
	for part: Part in snip.parts:
		alive = _pose_small(snip, part, frame) or alive
	return alive


## Poses one small scissors `frame` frames into its volley; false once it is gone.
func _pose_small(snip: Snip, part: Part, frame: int) -> bool:
	var local: int = frame - part.launch
	if local < 0 and not _ctx.reduced:
		return true
	var exit: float = -1.0
	var size: float = 1.0
	var angle: float = ScissorsRig.CLOSED
	var at: Vector3 = part.to
	if _ctx.reduced:
		# shown shut and still for the wedge's life
		if frame >= int(FxCones.flash_time_for(snip.def, snip.n) / FxSquash.FRAME):
			return false
	else:
		var flight: float = clampf(float(local) / float(FxMotion.SNIP_FLIGHT_FRAMES), 0.0, 1.0)
		var eased: float = 1.0 - (1.0 - flight) * (1.0 - flight)
		angle = FxMotion.cut_angle(flight)
		at = part.from.lerp(part.to, eased)
		if local < POP_FRAMES:
			size = FxSquash.pop(float(local + 1) / float(POP_FRAMES))
		var exit_at: int = FxMotion.SNIP_FLIGHT_FRAMES + HOLD_FRAMES
		if local >= exit_at:
			exit = float(local - exit_at) / float(EXIT_FRAMES)
		if local >= FxMotion.SNIP_FLIGHT_FRAMES and not part.arrived:
			part.arrived = true
			_on_close(snip, part)
	if exit >= 1.0:
		part.holder.visible = false
		return false
	if exit >= 0.0:
		size *= 1.0 - exit
	part.holder.visible = true
	part.rig.set_open(angle)
	part.last_angle = angle
	part.holder.position = at + Vector3(0.0, EXIT_LIFT * maxf(exit, 0.0), 0.0)
	part.holder.scale = Vector3.ONE * maxf(size, 0.01)
	return true


## Poses the big pair `frame` frames in; false once it is over.
func _pose_heavy(snip: Snip, part: Part, frame: int) -> bool:
	var offset: float = 0.0
	var size: float = 1.0
	var angle: float = part.rig.angle()
	var exit: float = -1.0
	var exit_frames: int = HEAVY_EXIT_FRAMES
	if snip.struck >= 0.0:
		var since: int = int((_ctx.time - snip.struck) / FxSquash.FRAME + 0.001)
		angle = ScissorsRig.CLOSED
		offset = 0.0 if _ctx.reduced else snip.lunge
		part.squash.scale = Vector3.ONE
		if not _ctx.reduced:
			# the lunge frame is a smear along the blades; then the squash and its spring back
			part.squash.scale = FxSquash.smear_scale(HEAVY_STRETCH) if since < 1 else FxSquash.along(FxSquash.landing(HEAVY_SQUASH, float(since - 1)))
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
	if exit >= 1.0:
		part.holder.visible = false
		return false
	if exit >= 0.0:
		size *= 1.0 - exit
	part.holder.visible = true
	part.rig.set_open(angle)
	part.last_angle = angle
	part.holder.position = part.from + snip.aim * offset + Vector3(0.0, EXIT_LIFT * maxf(exit, 0.0), 0.0)
	part.holder.scale = Vector3.ONE * maxf(size, 0.01)
	return true


## A small scissors arrived shut: its smear across the wedge and, from the second snip on, stars on each kid it hit.
func _on_close(snip: Snip, part: Part) -> void:
	_smear(snip, part)
	if snip.parts.find(part) < 1 or _ctx.sim == null:
		return
	for id: int in snip.hit_kids:
		var kid: PlayerState = _ctx.sim.players.get(id) as PlayerState
		if kid != null:
			_ctx.particles.puff(FxParticles.Preset.STARS, Vector3(kid.position.x, 0.0, kid.position.y), SNIP_STARS)


## The white smear arc across the wedge for a cut: round the caster at the strike's height, through the blades' tips. A small
## scissors launched over the right shoulder cuts across toward the left (the arc's head, its brightest end, on the left) and one
## from the left toward the right; the big snip's sweeps right to left and reaches `lunge` m further (where the lunge ends) with
## a lighter echo at the tips' rest.
func _smear(snip: Snip, part: Part) -> void:
	if _ctx.reduced:
		return
	var turn: Basis = Basis(Vector3.UP, atan2(-snip.aim.x, -snip.aim.z))
	var life: float = FxSquash.TRAIL_FRAMES * FxSquash.FRAME
	var sweep: Basis = turn * Basis.from_scale(Vector3(-1.0 if part.side > 0 else 1.0, 1.0, 1.0))
	if snip.heavy:
		_ctx.ground.smear(Transform3D(sweep, snip.centre), &"arc", snip.tips + snip.lunge, FxRelease.TINT_SCISSORS, life)
		_ctx.ground.smear(Transform3D(sweep, snip.centre), &"arc", snip.tips, Color(FxRelease.TINT_SCISSORS, ECHO_ALPHA), life)
		return
	_ctx.ground.smear(Transform3D(sweep, snip.centre), &"arc", snip.tips, FxRelease.TINT_SCISSORS, life)
