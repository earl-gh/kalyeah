class_name FxWalls
extends Node3D
## Draws the sim's shields keyed by id: the paper wall (papel shield) and the block of earth (langit lupa). The wall's
## width and place are the sim's exact segment (a team ribbon on the ground marks it from the first frame); only its height,
## rise, flutter and fall are the view's. Its time left comes with the snapshot, so online plays the same. A projectile of
## the other team that stops at the wall makes it react (the paper flexes, the earth shudders).

## The paper wall's drawn height and depth (metres; owner's call: 2.6 m so its top clears the caster's head when cast up the
## screen, it was 1.6).
const SHIELD_HEIGHT: float = 2.6
const PAPER_THICKNESS: float = 0.3
## The soil block's drawn height (metres; owner's call: 2.4, it was 1.7), how long it rises (overshooting to `SOIL_PEAK`,
## settled `SOIL_SETTLE_FRAMES` frames later) and how long it collapses (slumping `SOIL_SLUMP` wider) at its end.
const SOIL_HEIGHT: float = 2.4
const SOIL_RISE_TIME: float = 0.22
const SOIL_PEAK: float = 1.12
const SOIL_SETTLE_FRAMES: int = 2
const SOIL_COLLAPSE_TIME: float = 0.6
const SOIL_SLUMP: float = 1.15
## The flattest a collapsing block gets before it goes (a true zero breaks the transform).
const SOIL_FLAT: float = 0.04
## The team ribbon on the ground along the wall's exact segment, this wide (metres).
const RIBBON_WIDTH: float = 0.12
## The paper unfolds over `UNFOLD_FRAMES` frames (`UNFOLD_FRAMES_REDUCED` with Reduce motion, no overshoot), flutters
## `FLUTTER_DEG` degrees at `FLUTTER_HZ` and crumples over its last `CRUMPLE_TIME` seconds; `PAPER_BITS` paper bits fly at
## its base when it unfolds and when it is gone.
const UNFOLD_FRAMES: int = 6
const UNFOLD_FRAMES_REDUCED: int = 3
const FLUTTER_DEG: float = 2.0
const FLUTTER_HZ: float = 1.5
const CRUMPLE_TIME: float = 0.3
const PAPER_BITS: int = 4
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
	rec.node = Node3D.new()
	var width: float = shield.half_width * 2.0
	if rec.soil:
		rec.body = WeaponModel.soil(width, SOIL_HEIGHT, width)
	else:
		# a standing sheet of paper (the owner's model); its base sits on the ground
		rec.body = WeaponModel.shield(width, SHIELD_HEIGHT, PAPER_THICKNESS)
	rec.node.add_child(rec.body)
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
	return rec


func _update(rec: Rec, shield: WeaponSystem.Shield) -> void:
	rec.node.position = Vector3(shield.center.x, 0.0, shield.center.y)
	rec.node.rotation.y = -shield.along.angle()
	var age: float = (shield.def.duration if shield.def != null else 0.0) - shield.time_left
	var react: float = (_ctx.time - rec.react_at) / (REACT_FRAMES * FxSquash.FRAME) if rec.react_at >= 0.0 else 1.0
	var reacting: bool = react < 1.0 and not _ctx.reduced
	if rec.soil:
		_soil(rec, shield, age, reacting)
	else:
		_paper(rec, shield, age, reacting, react)


## The paper unfolds (low and narrow, up past its height, settled), flutters, flexes on a block and crumples at its end.
func _paper(rec: Rec, shield: WeaponSystem.Shield, age: float, reacting: bool, react: float) -> void:
	var shape: Vector2 = Vector2.ONE
	if _ctx.reduced:
		var t: float = clampf(age / (UNFOLD_FRAMES_REDUCED * FxSquash.FRAME), 0.0, 1.0)
		shape = Vector2(1.0, lerpf(FxMotion.UNFOLD_FROM, 1.0, t))
	else:
		shape = FxMotion.unfold(age / (UNFOLD_FRAMES * FxSquash.FRAME))
	var crumple: float = clampf(1.0 - shield.time_left / CRUMPLE_TIME, 0.0, 1.0)
	if crumple > 0.0:
		var fold: Vector2 = FxMotion.crumple(crumple)
		shape = Vector2(shape.x * fold.x, shape.y * fold.y)
		_fade_ribbon(rec, 1.0 - crumple)
	rec.body.scale = Vector3(shape.x, shape.y, 1.0)
	var lean: float = 0.0
	if not _ctx.reduced:
		lean = deg_to_rad(FLUTTER_DEG) * sin(TAU * FLUTTER_HZ * age)
		if reacting:
			# flexes away from the side it was hit on
			lean -= rec.react_side * deg_to_rad(FLEX_DEG) * sin(PI * react)
	rec.body.rotation.x = lean


## The earth erupts with an overshoot (wide while low, thinner at the stretch: the volume kept), stands still, shudders on a block and collapses wider at its end.
func _soil(rec: Rec, shield: WeaponSystem.Shield, age: float, reacting: bool) -> void:
	var rise_time: float = SOIL_RISE_TIME + SOIL_SETTLE_FRAMES * FxSquash.FRAME
	var rise: float = clampf(age / SOIL_RISE_TIME, 0.0, 1.0) if _ctx.reduced else FxSquash.overshoot(age / rise_time, SOIL_PEAK)
	var fall: float = clampf(shield.time_left / SOIL_COLLAPSE_TIME, 0.0, 1.0)
	var height: float = rise
	var spread: float = 1.0
	if fall >= 1.0 and not _ctx.reduced:
		# rising: wide while low, stretched up and thinner at the overshoot (the volume kept), exactly 1 once settled
		spread = FxSquash.along(rise).x
	if fall < 1.0:
		# ease-in: slow at first, then all at once, ending flat exactly as the wall ends
		var down: float = 1.0 - fall
		height = minf(rise, 1.0) * (1.0 - down * down)
		_fade_ribbon(rec, fall)
		if not rec.collapsing:
			rec.collapsing = true
			_ctx.particles.puff(FxParticles.Preset.SOIL, Vector3(shield.center.x, 0.0, shield.center.y), SOIL_FALL)
		if not _ctx.reduced:
			spread = lerpf(SOIL_SLUMP, 1.0, fall)
	rec.body.scale = Vector3(spread, maxf(height, SOIL_FLAT), spread)
	rec.body.position.x = SHUDDER * sin(TAU * 15.0 * (_ctx.time - rec.react_at)) if reacting else 0.0


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
