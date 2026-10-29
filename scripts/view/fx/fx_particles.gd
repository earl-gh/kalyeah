class_name FxParticles
extends Node3D
## The pooled particle puffs of the skill effects: dust, a landing ring of dust, paper confetti, soil, comic smoke, hit
## stars, heal sparkles, rock chips and paper scraps. Every emitter is a one-shot `CPUParticles3D` built once (a fixed
## amount, so a reuse only assigns properties; one draw each; safe on the Compatibility renderer, unlike GPU particles on
## many GLES3 phones). Decoration only: when the live cap is reached or no emitter is free, a puff is skipped (ground marks
## never use this pool). Nothing at all with Reduce motion. Busy time is the view's own clock, never `emitting`.

enum Preset { DUST, IMPACT, PAPER, SOIL, SMOKE, STARS, PLUS, CHIPS, SCRAPS }

## Particles per emitter -> how many emitters of that size (36 in all).
const BUCKETS: Dictionary[int, int] = {2: 8, 4: 12, 6: 8, 8: 6, 12: 2}
## The most particles alive at once across every puff.
const LIVE_CAP: int = 120
## An emitter stays busy for its lifetime plus this (seconds), so the last particle is gone before it is reused.
const BUSY_PAD: float = 0.05
## Particles are simulated at the sim's rate (cheap on a phone, steps on 2s at 60 fps).
const FIXED_FPS: int = 30
## Continuous emitters for the spinning top's dust trail: this many, each this many particles living this long (seconds),
## each puff this big (metres).
const TRAILS: int = 4
const TRAIL_AMOUNT: int = 5
const TRAIL_LIFE: float = 0.3
const TRAIL_SIZE: float = 0.45
## Particle sizes are for the kids as drawn (twice life size, CameraRules.kid_view_scale): a 0.35 m star read as a speck
## beside a 3.7 m kid. A dust puff: each particle this big at its fullest (metres), this see-through, born this far apart (so
## four puffs read as a little cloud, not one grey ball).
const DUST_SIZE: float = 0.6
const DUST_ALPHA: float = 0.75
const DUST_SPREAD: float = 0.3


## One preset's look and motion (built once).
class Spec extends RefCounted:
	var mesh: Mesh
	var material: Material
	var color: Color = Color.WHITE
	var lifetime: float = 0.4
	var speed_min: float = 1.0
	var speed_max: float = 2.0
	## Downward pull in m/s^2 (0 = none).
	var gravity: float = 0.0
	## Height above the given point where particles are born (0 when the emission points carry their own height).
	var spawn_y: float = 0.0
	var direction: Vector3 = Vector3.UP
	var spread: float = 90.0
	var flatness: float = 0.0
	var shape: CPUParticles3D.EmissionShape = CPUParticles3D.EMISSION_SHAPE_POINT
	var box: Vector3 = Vector3.ZERO
	var points: PackedVector3Array = PackedVector3Array()
	var normals: PackedVector3Array = PackedVector3Array()
	var size: float = 0.5
	var size_curve: Curve
	var random_angle: bool = false
	## True when a particle can fly up (the apex check uses the full speed upward); false for sideways-only stars.
	var rises: bool = true
	## The highest any particle may reach above the given point (metres; tested).
	var apex_bound: float = 1.2


static var _specs: Dictionary[int, Spec] = {}

var _ctx: FxContext
var _emitters: Array[CPUParticles3D] = []
var _sizes: PackedInt32Array = PackedInt32Array()
var _busy_until: PackedFloat32Array = PackedFloat32Array()
var _trails: Array[CPUParticles3D] = []
var _trail_used: Array[bool] = []


## Builds every emitter (once) for the effects of `ctx`.
func setup(ctx: FxContext) -> void:
	_ctx = ctx
	for size: int in BUCKETS:
		for i: int in BUCKETS[size]:
			var emitter: CPUParticles3D = _emitter(size)
			emitter.one_shot = true
			emitter.explosiveness = 1.0
			_emitters.append(emitter)
			_sizes.append(size)
			_busy_until.append(-1.0)
	var dust: Spec = spec(Preset.DUST)
	for i: int in TRAILS:
		var trail: CPUParticles3D = _emitter(TRAIL_AMOUNT)
		trail.one_shot = false
		trail.explosiveness = 0.0
		trail.lifetime = TRAIL_LIFE
		trail.mesh = dust.mesh
		trail.material_override = dust.material
		trail.color = dust.color
		trail.direction = Vector3.UP
		trail.spread = 60.0
		trail.initial_velocity_min = 0.2
		trail.initial_velocity_max = 0.5
		trail.gravity = Vector3.ZERO
		trail.scale_amount_min = TRAIL_SIZE
		trail.scale_amount_max = TRAIL_SIZE
		trail.scale_amount_curve = dust.size_curve
		_trails.append(trail)
		_trail_used.append(false)


func _emitter(amount: int) -> CPUParticles3D:
	var emitter: CPUParticles3D = CPUParticles3D.new()
	emitter.amount = amount
	emitter.emitting = false
	emitter.local_coords = false
	emitter.fixed_fps = FIXED_FPS
	emitter.draw_order = CPUParticles3D.DRAW_ORDER_INDEX
	emitter.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	emitter.visible = false
	add_child(emitter)
	return emitter


## Presets built right now (tests).
static func spec_count() -> int:
	return _specs.size()


## Empties the preset cache (the last effect view left the tree).
static func clear_cache() -> void:
	_specs.clear()


## The look and motion of `preset` (shared by every puff of it).
static func spec(preset: Preset) -> Spec:
	if _specs.is_empty():
		_build_specs()
	return _specs[preset]


## The highest a particle of `s` can get above the point it was given (metres): its birth height, plus the climb of a
## straight-up throw under its gravity, or its whole flight when nothing pulls it down.
static func apex(s: Spec) -> float:
	var top: float = s.spawn_y
	for point: Vector3 in s.points:
		top = maxf(top, point.y + s.spawn_y)
	if s.shape == CPUParticles3D.EMISSION_SHAPE_BOX:
		top += s.box.y
	if not s.rises:
		return top
	if s.gravity > 0.0:
		return top + s.speed_max * s.speed_max / (2.0 * s.gravity)
	return top + s.speed_max * s.lifetime


## Throws a `preset` puff of `amount` particles at `at` (world), tinted by `tint`. False when it was skipped (Reduce motion,
## the live cap, or no free emitter of that size): decoration only.
func puff(preset: Preset, at: Vector3, amount: int, tint: Color = Color.WHITE) -> bool:
	if _ctx.reduced:
		return false
	var size: int = _bucket(amount)
	if size == 0 or live_particles() + size > LIVE_CAP:
		return false
	var index: int = _free(size)
	if index < 0:
		return false
	var s: Spec = spec(preset)
	var emitter: CPUParticles3D = _emitters[index]
	emitter.mesh = s.mesh
	emitter.material_override = s.material
	emitter.color = s.color * tint
	emitter.lifetime = s.lifetime
	emitter.initial_velocity_min = s.speed_min
	emitter.initial_velocity_max = s.speed_max
	emitter.gravity = Vector3(0.0, -s.gravity, 0.0)
	emitter.direction = s.direction
	emitter.spread = s.spread
	emitter.flatness = s.flatness
	emitter.emission_shape = s.shape
	emitter.emission_box_extents = s.box
	emitter.emission_points = s.points
	emitter.emission_normals = s.normals
	emitter.scale_amount_min = s.size
	emitter.scale_amount_max = s.size
	emitter.scale_amount_curve = s.size_curve
	emitter.angle_min = 0.0
	emitter.angle_max = 360.0 if s.random_angle else 0.0
	emitter.particle_flag_rotate_y = s.random_angle
	emitter.global_position = at + Vector3(0.0, s.spawn_y, 0.0)
	emitter.visible = true
	emitter.restart()
	emitter.emitting = true
	_busy_until[index] = _ctx.time + s.lifetime + BUSY_PAD
	return true


## A continuous dust trail emitter (the spinning top's), or null when all are in use. Give it back with `trail_give`.
func trail_take() -> CPUParticles3D:
	if _ctx.reduced:
		return null
	for i: int in _trails.size():
		if not _trail_used[i]:
			_trail_used[i] = true
			_trails[i].visible = true
			_trails[i].emitting = true
			return _trails[i]
	return null


func trail_give(trail: CPUParticles3D) -> void:
	var i: int = _trails.find(trail)
	if i >= 0:
		_trail_used[i] = false
		trail.emitting = false
		trail.visible = false


## Hides emitters whose puff is over (they are free again).
func step() -> void:
	for i: int in _emitters.size():
		if _busy_until[i] >= 0.0 and _ctx.time >= _busy_until[i]:
			_busy_until[i] = -1.0
			_emitters[i].emitting = false
			_emitters[i].visible = false


## Particles alive right now (busy one-shot emitters at their full amount, plus trails in use).
func live_particles() -> int:
	var live: int = 0
	for i: int in _emitters.size():
		if _busy_until[i] >= 0.0:
			live += _sizes[i]
	for used: bool in _trail_used:
		live += TRAIL_AMOUNT if used else 0
	return live


func busy_emitters() -> int:
	var busy: int = 0
	for until: float in _busy_until:
		busy += 1 if until >= 0.0 else 0
	return busy


## The busy one-shot emitters (tests read their preset and amount).
func busy() -> Array[CPUParticles3D]:
	var found: Array[CPUParticles3D] = []
	for i: int in _emitters.size():
		if _busy_until[i] >= 0.0:
			found.append(_emitters[i])
	return found


func emitter_count() -> int:
	return _emitters.size() + _trails.size()


## The smallest emitter size that holds `amount` (0 when too many).
static func _bucket(amount: int) -> int:
	var best: int = 0
	for size: int in BUCKETS:
		if size >= amount and (best == 0 or size < best):
			best = size
	return best


func _free(size: int) -> int:
	for i: int in _emitters.size():
		if _sizes[i] == size and _busy_until[i] < 0.0:
			return i
	return -1


static func _build_specs() -> void:
	var grow_fade: Curve = _curve([Vector2(0.0, 0.5), Vector2(0.3, 1.0), Vector2(1.0, 0.0)])
	var shrink: Curve = _curve([Vector2(0.0, 1.0), Vector2(1.0, 0.0)])
	var hold_shrink: Curve = _curve([Vector2(0.0, 1.0), Vector2(0.7, 1.0), Vector2(1.0, 0.0)])
	var puff_material: Material = FxKit.particle_material(true)
	var flat_material: Material = FxKit.particle_material(false)
	# DUST: a sidewalk-grey puff at the feet, low and short.
	var dust: Spec = Spec.new()
	dust.mesh = FxKit.puff_quad()
	dust.material = puff_material
	dust.color = Color(Palette.SIDEWALK, DUST_ALPHA)
	dust.lifetime = 0.4
	dust.speed_min = 1.2
	dust.speed_max = 2.0
	dust.gravity = 2.0
	dust.spawn_y = 0.1
	dust.flatness = 0.8
	dust.shape = CPUParticles3D.EMISSION_SHAPE_BOX
	dust.box = Vector3(DUST_SPREAD, 0.0, DUST_SPREAD)
	dust.size = DUST_SIZE
	dust.size_curve = grow_fade
	dust.apex_bound = 1.2
	_specs[Preset.DUST] = dust
	# IMPACT: a ring of dust thrown out from where the meteor lands.
	var impact: Spec = Spec.new()
	impact.mesh = FxKit.puff_quad()
	impact.material = puff_material
	impact.color = Palette.SIDEWALK
	impact.lifetime = 0.45
	impact.speed_min = 2.0
	impact.speed_max = 3.0
	impact.gravity = 6.0
	impact.spawn_y = 0.1
	impact.direction = Vector3(0.0, 0.0, 1.0)
	impact.spread = 4.0
	impact.shape = CPUParticles3D.EMISSION_SHAPE_DIRECTED_POINTS
	for i: int in 12:
		var a: float = TAU * float(i) / 12.0
		impact.points.append(Vector3(cos(a), 0.0, sin(a)) * 0.6)
		impact.normals.append(Vector3(cos(a), 0.35, sin(a)).normalized())
	impact.size = 0.75
	impact.size_curve = grow_fade
	impact.apex_bound = 0.85
	_specs[Preset.IMPACT] = impact
	# PAPER / SOIL: confetti and clods thrown up from a wall or a sprung trap.
	for preset: Preset in [Preset.PAPER, Preset.SOIL]:
		var bits: Spec = Spec.new()
		bits.mesh = FxKit.puff_quad()
		bits.material = puff_material
		bits.color = Palette.WHITE if preset == Preset.PAPER else Palette.CARDBOARD_DARK
		bits.lifetime = 0.4
		bits.speed_min = 1.2
		bits.speed_max = 2.5
		bits.gravity = 6.0
		bits.spawn_y = 0.2
		bits.spread = 70.0
		bits.size = 0.5
		bits.size_curve = grow_fade
		bits.apex_bound = 0.72
		_specs[preset] = bits
	# SMOKE: the comic puff that hides a kid turning into a can (and back).
	var smoke: Spec = Spec.new()
	smoke.mesh = FxKit.puff_quad()
	smoke.material = puff_material
	smoke.color = Palette.WHITE
	smoke.lifetime = 0.4
	smoke.speed_min = 0.6
	smoke.speed_max = 1.4
	smoke.gravity = 1.0
	smoke.spawn_y = 0.45
	smoke.spread = 180.0
	smoke.shape = CPUParticles3D.EMISSION_SHAPE_BOX
	smoke.box = Vector3(0.9, 0.15, 0.9)
	smoke.size = 1.1
	smoke.size_curve = grow_fade
	smoke.apex_bound = 2.0
	_specs[Preset.SMOKE] = smoke
	# STARS: comic hit stars beside the kid at waist height, flying sideways and back (never toward the camera).
	var stars: Spec = Spec.new()
	stars.mesh = FxKit.star_quad()
	stars.material = puff_material
	stars.lifetime = 0.3
	stars.speed_min = 3.0
	stars.speed_max = 4.3
	stars.direction = Vector3(0.0, 0.0, 1.0)
	stars.spread = 6.0
	stars.shape = CPUParticles3D.EMISSION_SHAPE_DIRECTED_POINTS
	stars.points = PackedVector3Array([Vector3(0.5, 1.3, 0.0), Vector3(-0.5, 1.3, 0.0), Vector3(0.35, 1.3, -0.35), Vector3(-0.35, 1.3, -0.35)])
	stars.normals = PackedVector3Array([Vector3(1.0, 0.0, 0.0), Vector3(-1.0, 0.0, 0.0), Vector3(0.7071, 0.0, -0.7071), Vector3(-0.7071, 0.0, -0.7071)])
	stars.size = 0.7
	stars.size_curve = shrink
	stars.rises = false
	stars.apex_bound = 1.3
	_specs[Preset.STARS] = stars
	# PLUS: heal sparkles rising beside and behind the kid (never in front of the face).
	var plus: Spec = Spec.new()
	plus.mesh = FxKit.plus_quad()
	plus.material = puff_material
	plus.lifetime = 0.6
	plus.speed_min = 4.5
	plus.speed_max = 4.5
	plus.direction = Vector3.UP
	plus.spread = 0.0
	plus.shape = CPUParticles3D.EMISSION_SHAPE_POINTS
	plus.points = PackedVector3Array([Vector3(0.7, 0.5, 0.0), Vector3(-0.7, 0.5, 0.0), Vector3(0.5, 0.5, -0.5), Vector3(-0.5, 0.5, -0.5)])
	plus.size = 0.5
	plus.size_curve = hold_shrink
	plus.apex_bound = 3.2
	_specs[Preset.PLUS] = plus
	# CHIPS: little copies of the rock falling to the street.
	var chips: Spec = Spec.new()
	chips.mesh = WeaponModel.mesh_of("meteor")
	chips.material = WeaponModel.look_of("meteor")
	chips.lifetime = 0.4
	chips.speed_min = 0.5
	chips.speed_max = 1.0
	chips.gravity = 9.0
	chips.spread = 80.0
	var natural: Vector3 = chips.mesh.get_aabb().size
	chips.size = 0.25 / maxf(natural.x, maxf(natural.y, natural.z))
	chips.size_curve = hold_shrink
	chips.random_angle = true
	chips.apex_bound = 0.1
	_specs[Preset.CHIPS] = chips
	# SCRAPS: flat paper scraps settling inside a sprung trap's circle.
	var scraps: Spec = Spec.new()
	scraps.mesh = FxKit.flat_quad()
	scraps.material = flat_material
	scraps.color = Palette.WHITE
	scraps.lifetime = 0.5
	scraps.speed_min = 1.0
	scraps.speed_max = 2.0
	scraps.gravity = 9.0
	scraps.spawn_y = 0.3
	scraps.flatness = 0.9
	scraps.size = 0.4
	scraps.size_curve = hold_shrink
	scraps.random_angle = true
	scraps.apex_bound = 0.6
	_specs[Preset.SCRAPS] = scraps


static func _curve(points: Array[Vector2]) -> Curve:
	var curve: Curve = Curve.new()
	for point: Vector2 in points:
		curve.add_point(point)
	return curve
