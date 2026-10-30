class_name FxWalls
extends Node3D
## Draws the sim's shields keyed by id: the paper wall (papel shield) and the block of earth (langit lupa). The wall's
## place, width and facing are the sim's exact segment, every frame: the sim's wall glides forward (the paper wall,
## `WeaponDef.push_distance`) or widens (the block of earth, `expand_factor` times after it has risen) with its age
## (`WallMotion`), and what is drawn, the team ribbon on the ground included, takes the sim's current line, so the blocking line and
## the picture are never apart. Only height, rise, flutter, fade and sinking are the view's (the sim's wall is a line with no height).
## The paper fades out over its last `PAPER_FADE_TIME` s; the block sinks at its widened width over its last `SOIL_COLLAPSE_TIME` s.
## Tiny crumbs of earth fall off the block at its base as it rises, widens and sinks. Its time left comes with the snapshot, so online
## plays the same. A projectile of the other team that stops at the wall makes it react (the paper flexes, the earth shudders).

## The paper wall's drawn height and depth (metres; owner's call: 2.6 m so its top clears the caster's head when cast up the
## screen, it was 1.6).
const SHIELD_HEIGHT: float = 2.6
const PAPER_THICKNESS: float = 0.3
## The soil block's drawn height (metres; owner's call: 2.4, it was 1.7); it rises (overshooting to `SOIL_PEAK`, settled exactly
## at the sim's `expand_delay`, when the sim starts to widen it) and sinks over `SOIL_COLLAPSE_TIME` s at its end.
const SOIL_HEIGHT: float = 2.4
const SOIL_PEAK: float = 1.12
const SOIL_COLLAPSE_TIME: float = 0.6
## The rise time of a block whose weapon sets no `expand_delay`.
const SOIL_RISE_DEFAULT: float = 0.3
## The flattest a collapsing block gets before it goes (a true zero breaks the transform).
const SOIL_FLAT: float = 0.04
## The team ribbon on the ground along the wall's exact segment, this wide (metres).
const RIBBON_WIDTH: float = 0.12
## The paper unfolds over `UNFOLD_FRAMES` frames (`UNFOLD_FRAMES_REDUCED` with Reduce motion, no overshoot), flutters
## `FLUTTER_DEG` degrees at `FLUTTER_HZ`; `PAPER_BITS` paper bits fly at its base when it unfolds and when it is gone. Over its last
## `PAPER_FADE_TIME` s it fades out (a see-through ramp, fully gone as the sim's wall is). The glide itself is the sim's
## (`data/weapons/papel_shield.tres`: 1.5 m over 0.6 s, cubic ease-out: it reads, about 27 px along the street).
const UNFOLD_FRAMES: int = 6
const UNFOLD_FRAMES_REDUCED: int = 3
const FLUTTER_DEG: float = 2.0
const FLUTTER_HZ: float = 1.5
const PAPER_FADE_TIME: float = 0.5
const PAPER_BITS: int = 4
## The glide is smeared: a paper-white trail along it at `PAPER_TRAIL_Y` m (mid-wall), `PAPER_TRAIL_WIDTH` m across, for
## `PAPER_TRAIL_FRAMES` frames. The block's widening shows two earth streaks at its ends, `EXPAND_STREAK_FRAMES` frames long.
const PAPER_TRAIL_Y: float = 1.3
const PAPER_TRAIL_WIDTH: float = 1.2
const PAPER_TRAIL_FRAMES: int = 6
const EXPAND_STREAK_FRAMES: int = 6
## Crumbs fall off the block's base (`FxParticles.CRUMB_COUNT` each time, over the block's current width and `CRUMB_DEPTH` m
## deep, on the side the camera sees) at these beats: as it rises (age 0), as it begins to widen and half way through, as it begins
## to sink and half way through.
const CRUMB_DEPTH: float = 0.5
## The earth erupts: a crack strip of its exact footprint (`SOIL_CRACK_DEPTH` m deep, fading over `SOIL_CRACK_TIME` s),
## `SOIL_BURST` soil clods and `SOIL_CHIPS` chips; `SOIL_FALL` clods as it collapses.
const SOIL_CRACK_DEPTH: float = 0.4
const SOIL_CRACK_TIME: float = 0.6
const SOIL_BURST: int = 8
const SOIL_CHIPS: int = 4
const SOIL_FALL: int = 6
## A block: a projectile of the other team gone within `BLOCK_NEAR` m of the wall makes the paper flex `FLEX_DEG` degrees
## away (with a smear star and paper bits) or the earth shudder `SHUDDER` m (with `BLOCK_CHIPS` chips), for `REACT_FRAMES`.
const BLOCK_NEAR: float = 0.6
const FLEX_DEG: float = 6.0
const SHUDDER: float = 0.05
const BLOCK_CHIPS: int = 2
const REACT_FRAMES: int = 4
## The smear star where a projectile hit the paper: this long (metres), this long-lived (frames).
const BLOCK_STAR: float = 1.4
const BLOCK_STAR_FRAMES: int = 3


## One drawn wall.
class Rec extends RefCounted:
	var id: int
	var def: WeaponDef
	var team: int
	var soil: bool
	var node: Node3D
	var body: Node3D
	var ribbon: MeshInstance3D
	var ribbon_fade: StandardMaterial3D
	var center: Vector2
	var along: Vector2
	var half_width: float
	var react_at: float = -1.0
	var react_side: float = 1.0
	var collapsing: bool = false
	var expanding: bool = false
	## Crumb beats already thrown (see `_crumb_beats`), the wall's width as a multiple of the cast width now, and the paper's fade.
	var beats: int = 0
	var base_half_width: float
	var width_scale: float = 1.0
	var fade: float = 0.0
	var meshes: Array[GeometryInstance3D] = []


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


## The wall body of shield `id` (the stretched model's holder), or null.
func node_for(id: int) -> Node3D:
	var rec: Rec = _recs.get(id) as Rec
	return rec.body if rec != null else null


## The team ribbon of shield `id` (tests check its exact width), or null.
func ribbon_for(id: int) -> MeshInstance3D:
	var rec: Rec = _recs.get(id) as Rec
	return rec.ribbon if rec != null else null


## A projectile of `team` left the sim at `at`: a wall of the other team it stopped at reacts.
func on_projectile_gone(_def: WeaponDef, team: int, at: Vector3) -> void:
	var ground: Vector2 = Vector2(at.x, at.z)
	for rec: Rec in _recs.values():
		if rec.team == team:
			continue
		var left: Vector2 = rec.center - rec.along * rec.half_width
		var right: Vector2 = rec.center + rec.along * rec.half_width
		var nearest: Vector2 = Geometry2D.get_closest_point_to_segment(ground, left, right)
		if nearest.distance_to(ground) > BLOCK_NEAR:
			continue
		rec.react_at = _ctx.time
		rec.react_side = signf((ground - rec.center).dot(rec.along.orthogonal()))
		if _ctx.reduced:
			continue
		var bits: Vector3 = Vector3(nearest.x, 0.0, nearest.y)
		if rec.soil:
			_ctx.particles.puff(FxParticles.Preset.CHIPS, bits + Vector3(0.0, SOIL_HEIGHT / 2.0, 0.0), BLOCK_CHIPS)
		else:
			var star: Transform3D = Transform3D(Basis(Vector3.UP, -rec.along.angle()), Vector3(nearest.x, at.y, nearest.y))
			_ctx.ground.smear(star, &"streak", BLOCK_STAR, FxRelease.TINT_PAPER, BLOCK_STAR_FRAMES * FxSquash.FRAME)
			_ctx.particles.puff(FxParticles.Preset.PAPER, bits, PAPER_BITS)


func _create(shield: WeaponSystem.Shield) -> Rec:
	var rec: Rec = Rec.new()
	rec.id = shield.id
	rec.def = shield.def
	rec.team = shield.team
	rec.soil = shield.def != null and shield.def.look == &"soil"
	rec.center = shield.center
	rec.along = shield.along
	rec.half_width = shield.half_width
	rec.base_half_width = shield.base_half_width if shield.base_half_width > 0.0 else shield.half_width
	rec.node = Node3D.new()
	var width: float = rec.base_half_width * 2.0
	if rec.soil:
		rec.body = WeaponModel.soil(width, SOIL_HEIGHT, width)
	else:
		# a standing sheet of paper (the owner's model); its base sits on the ground
		rec.body = WeaponModel.shield(width, SHIELD_HEIGHT, PAPER_THICKNESS)
	rec.node.add_child(rec.body)
	for node: Node in rec.body.find_children("*", "GeometryInstance3D", true, false):
		rec.meshes.append(node as GeometryInstance3D)
	rec.ribbon = MeshInstance3D.new()
	rec.ribbon.name = "Ribbon"
	rec.ribbon.mesh = FxGround.segment_mark(width, RIBBON_WIDTH, _ctx.team_color(shield.team))
	rec.ribbon.material_override = FxKit.overlay(FxKit.PRIO_RIM)
	rec.ribbon.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	rec.node.add_child(rec.ribbon)
	add_child(rec.node)
	_recs[rec.id] = rec
	var base: Vector3 = Vector3(shield.center.x, 0.0, shield.center.y)
	if rec.soil:
		_ctx.ground.crack(shield.center, -shield.along.angle(), &"strip", Vector2(width, SOIL_CRACK_DEPTH), SOIL_CRACK_TIME)
		_ctx.particles.puff(FxParticles.Preset.SOIL, base, SOIL_BURST)
		_ctx.particles.puff(FxParticles.Preset.CHIPS, base + Vector3(0.0, 0.5, 0.0), SOIL_CHIPS)
	else:
		_ctx.particles.puff(FxParticles.Preset.PAPER, base, PAPER_BITS)
		if not _ctx.reduced and shield.def != null:
			var forward: Vector2 = shield.forward() * shield.def.push_distance
			var ahead: Vector3 = Vector3(forward.x, 0.0, forward.y)
			var mid: Vector3 = Vector3(0.0, PAPER_TRAIL_Y, 0.0)
			_ctx.ground.trail(base + mid, base + mid + ahead, PAPER_TRAIL_WIDTH, FxRelease.TINT_PAPER, PAPER_TRAIL_FRAMES * FxSquash.FRAME)
	return rec


func _update(rec: Rec, shield: WeaponSystem.Shield) -> void:
	# the sim's line, as it is now: where it stands and how long it is
	rec.center = shield.center
	rec.half_width = shield.half_width
	rec.width_scale = shield.half_width / rec.base_half_width
	rec.node.position = Vector3(shield.center.x, 0.0, shield.center.y)
	rec.node.rotation.y = -shield.along.angle()
	rec.ribbon.scale.x = rec.width_scale
	var age: float = shield.age()
	var react: float = (_ctx.time - rec.react_at) / (REACT_FRAMES * FxSquash.FRAME) if rec.react_at >= 0.0 else 1.0
	var reacting: bool = react < 1.0 and not _ctx.reduced
	if rec.soil:
		_soil(rec, shield, age, reacting)
	else:
		_paper(rec, shield, age, reacting, react)


## How opaque the paper wall is with `time_left` s of the sim's wall to go: 1 until its last `PAPER_FADE_TIME` s, then a ramp to 0.
static func paper_alpha(time_left: float) -> float:
	return clampf(time_left / PAPER_FADE_TIME, 0.0, 1.0)


## The seconds the block takes to rise and settle at its full height (the sim starts widening it then).
static func soil_rise_time(def: WeaponDef) -> float:
	return def.expand_delay if def != null and def.expand_delay > 0.0 else SOIL_RISE_DEFAULT


## The block's height as a multiple of its full height: the eruption (overshooting once, settled exactly at 1 at `rise_time`) `age`
## seconds in, sunk by an ease-in over the last `SOIL_COLLAPSE_TIME` s of `time_left`, flat as the sim's wall ends.
static func soil_height(age: float, time_left: float, rise_time: float, reduced: bool) -> float:
	var rise: float = clampf(age / rise_time, 0.0, 1.0) if reduced else FxSquash.overshoot(age / rise_time, SOIL_PEAK)
	var fall: float = clampf(time_left / SOIL_COLLAPSE_TIME, 0.0, 1.0)
	if fall >= 1.0:
		return rise
	var down: float = 1.0 - fall
	return minf(rise, 1.0) * (1.0 - down * down)


## The paper unfolds (low and narrow, up past its height), flutters, flexes on a block and fades at its end. Its place and width are the sim's.
func _paper(rec: Rec, shield: WeaponSystem.Shield, age: float, reacting: bool, react: float) -> void:
	var shape: Vector2 = Vector2.ONE
	if _ctx.reduced:
		var t: float = clampf(age / (UNFOLD_FRAMES_REDUCED * FxSquash.FRAME), 0.0, 1.0)
		shape = Vector2(1.0, lerpf(FxMotion.UNFOLD_FROM, 1.0, t))
	else:
		shape = FxMotion.unfold(age / (UNFOLD_FRAMES * FxSquash.FRAME))
	# its width is exactly the sim's line from the first frame (the unfold is only taller, never narrower)
	rec.body.scale = Vector3(rec.width_scale, shape.y, 1.0)
	var alpha: float = paper_alpha(shield.time_left)
	if alpha < 1.0:
		_fade_ribbon(rec, alpha)
	_set_fade(rec, 1.0 - alpha)
	var lean: float = 0.0
	if not _ctx.reduced:
		lean = deg_to_rad(FLUTTER_DEG) * sin(TAU * FLUTTER_HZ * age)
		if reacting:
			# flexes away from the side it was hit on
			lean -= rec.react_side * deg_to_rad(FLEX_DEG) * sin(PI * react)
	rec.body.rotation.x = lean


## The earth erupts with an overshoot (thinner at the stretch: the volume kept), stands as wide as the sim's line (it widens to
## `expand_factor` times after it has risen), shudders on a block and sinks at that width at its end. Crumbs fall off its base throughout.
func _soil(rec: Rec, shield: WeaponSystem.Shield, age: float, reacting: bool) -> void:
	var rise_time: float = soil_rise_time(rec.def)
	var height: float = soil_height(age, shield.time_left, rise_time, _ctx.reduced)
	var fall: float = clampf(shield.time_left / SOIL_COLLAPSE_TIME, 0.0, 1.0)
	var depth: float = 1.0
	if fall >= 1.0 and not _ctx.reduced:
		# rising: thinner front to back at the overshoot (the volume kept), exactly 1 once settled; its width is the sim's
		depth = FxSquash.along(height).x
	if age >= rise_time and not rec.expanding:
		rec.expanding = true
		_expand_streaks(rec)
	if fall < 1.0:
		_fade_ribbon(rec, fall)
		if not rec.collapsing:
			rec.collapsing = true
			_ctx.particles.puff(FxParticles.Preset.SOIL, Vector3(shield.center.x, 0.0, shield.center.y), SOIL_FALL)
	rec.body.scale = Vector3(rec.width_scale, maxf(height, SOIL_FLAT), depth)
	rec.body.position.x = SHUDDER * sin(TAU * 15.0 * (_ctx.time - rec.react_at)) if reacting else 0.0
	_crumb_beats(rec, shield, age)


## Two earth streaks at the block's ends, running outward the way it widens (decoration; none with Reduce motion).
func _expand_streaks(rec: Rec) -> void:
	if _ctx.reduced or rec.def == null:
		return
	var grow: float = rec.base_half_width * (rec.def.expand_factor - 1.0)
	for side: float in [-1.0, 1.0]:
		var middle: Vector2 = rec.center + rec.along * side * (rec.half_width + grow / 2.0)
		var xform: Transform3D = Transform3D(Basis(Vector3.UP, -rec.along.angle()), Vector3(middle.x, SOIL_HEIGHT * 0.2, middle.y))
		_ctx.ground.smear(xform, &"streak", grow, FxRelease.TINT_ROCK, EXPAND_STREAK_FRAMES * FxSquash.FRAME)


## Throws the crumbs due by `age`: as it rises, as it starts widening and half way, as it starts sinking and half way.
func _crumb_beats(rec: Rec, shield: WeaponSystem.Shield, age: float) -> void:
	var sink_at: float = shield.def.duration - SOIL_COLLAPSE_TIME if shield.def != null else INF
	var widen: float = soil_rise_time(rec.def)
	var half: float = rec.def.expand_time / 2.0 if rec.def != null else 0.0
	var due: Array[float] = [0.0, widen, widen + half, sink_at, sink_at + SOIL_COLLAPSE_TIME / 2.0]
	while rec.beats < due.size() and age >= due[rec.beats]:
		rec.beats += 1
		# on the side of the block the camera sees (south), over the block's width as it is now
		var at: Vector2 = rec.center + Vector2(0.0, rec.base_half_width)
		_ctx.particles.puff(FxParticles.Preset.CRUMBS, Vector3(at.x, 0.0, at.y), FxParticles.CRUMB_COUNT, Color.WHITE, Vector2(rec.half_width * 2.0, CRUMB_DEPTH), rec.node.rotation.y)


## Makes the wall's meshes `transparency` see-through (0 opaque, 1 gone); only touches them when it changes.
func _set_fade(rec: Rec, transparency: float) -> void:
	if is_equal_approx(rec.fade, transparency):
		return
	rec.fade = transparency
	for mesh: GeometryInstance3D in rec.meshes:
		mesh.transparency = transparency


## The ribbon fades with its wall (`alpha` of its own look).
func _fade_ribbon(rec: Rec, alpha: float) -> void:
	if rec.ribbon_fade == null:
		rec.ribbon_fade = _ctx.ground.fade_take()
		if rec.ribbon_fade == null:
			return
		rec.ribbon_fade.render_priority = FxKit.PRIO_RIM
		rec.ribbon.material_override = rec.ribbon_fade
	rec.ribbon_fade.albedo_color = Color(1.0, 1.0, 1.0, clampf(alpha, 0.0, 1.0))


func _remove(id: int) -> void:
	var rec: Rec = _recs[id]
	_recs.erase(id)
	if not rec.soil:
		_ctx.particles.puff(FxParticles.Preset.PAPER, rec.node.position, PAPER_BITS)
	if rec.ribbon_fade != null:
		rec.ribbon_fade.render_priority = FxKit.PRIO_FILL
		_ctx.ground.fade_give(rec.ribbon_fade)
	rec.node.queue_free()
