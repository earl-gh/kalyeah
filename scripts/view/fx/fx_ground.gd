class_name FxGround
extends Node3D
## The flat things the skill effects lay on the street, pooled and built once: hit and heal rings, flash discs (a bounce
## landing), smear ribbons (a snip, a slap, a heal streak), cracks (a meteor landing, the soil block's eruption) and fade
## materials (a zone in its last moments). Over the cap the oldest ring is recycled and other kinds are skipped: they are
## decoration. Also the cached builders of the marks that ARE a hit area (a circle, a cone wedge, a wall's ribbon): they use
## the sim's exact numbers, never bigger or smaller (docs/ART.md "an effect says who threw it from its first frame").

const RINGS: int = 16
const FLASHES: int = 8
const SMEARS: int = 12
const CRACKS: int = 4
const FADES: int = 8
## A smear starts this see-through (white, 0.75) and is gone at the end of its life.
const SMEAR_ALPHA: float = 0.75
## A crack is the street's dark asphalt at this alpha, fading to nothing over its life.
const CRACK_ALPHA: float = 0.6


## One pooled decal.
class Slot extends RefCounted:
	var node: MeshInstance3D
	var material: StandardMaterial3D
	var active: bool = false
	var born: float = 0.0
	var life: float = 1.0
	var from_r: float = 0.0
	var to_r: float = 0.0
	var color: Color = Color.WHITE
	var peak: float = 1.0
	var rise: float = 0.0
	var still: bool = false


static var _marks: Dictionary[String, ArrayMesh] = {}

var _ctx: FxContext
var _rings: Array[Slot] = []
var _flashes: Array[Slot] = []
var _smears: Array[Slot] = []
var _cracks: Array[Slot] = []
var _fades: Array[StandardMaterial3D] = []
var _fade_used: Array[bool] = []


## Builds every slot (once).
func setup(ctx: FxContext) -> void:
	_ctx = ctx
	_rings = _pool(RINGS, FxKit.ring_mesh())
	_flashes = _pool(FLASHES, FxKit.disc_mesh())
	_smears = _pool(SMEARS, FxKit.smear_arc())
	_cracks = _pool(CRACKS, FxKit.crack_spokes())
	for i: int in FADES:
		_fades.append(FxKit.slot_material(FxKit.PRIO_FILL))
		_fade_used.append(false)


func _pool(count: int, mesh: Mesh) -> Array[Slot]:
	var slots: Array[Slot] = []
	for i: int in count:
		var slot: Slot = Slot.new()
		slot.node = MeshInstance3D.new()
		slot.node.mesh = mesh
		slot.material = FxKit.slot_material(FxKit.PRIO_DECAL)
		slot.node.material_override = slot.material
		slot.node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		slot.node.visible = false
		add_child(slot.node)
		slots.append(slot)
	return slots


## A ring on the ground round `at` growing from `from_r` to `to_r` metres while it fades over `life` seconds (held at
## `to_r` when `still`, for Reduce motion). Returns its slot (the oldest ring is recycled when all are busy).
func ring(at: Vector2, from_r: float, to_r: float, color: Color, life: float, still: bool = false) -> int:
	var index: int = _free(_rings)
	if index < 0:
		index = _oldest(_rings)
	var slot: Slot = _start(_rings[index], life)
	slot.from_r = from_r
	slot.to_r = to_r
	slot.color = color
	slot.still = still
	slot.node.position = Vector3(at.x, FxKit.Y_DECAL, at.y)
	_draw_ring(slot)
	return index


## Ends ring `index` now.
func drop_ring(index: int) -> void:
	if index >= 0 and index < _rings.size():
		_rings[index].active = false
		_rings[index].node.visible = false


## A disc of `radius` at `at` that rises to `peak_alpha` in `rise` seconds then fades over `fade`. False when none is free.
func flash_disc(at: Vector2, radius: float, color: Color, peak_alpha: float, rise: float, fade: float) -> bool:
	var index: int = _free(_flashes)
	if index < 0:
		return false
	var slot: Slot = _start(_flashes[index], rise + fade)
	slot.color = color
	slot.peak = peak_alpha
	slot.rise = rise
	slot.node.position = Vector3(at.x, FxKit.Y_DECAL, at.y)
	slot.node.scale = Vector3(radius, 1.0, radius)
	_draw_flash(slot)
	return true


## A white smear ribbon (`kind` &"arc": the snip's arc round the origin along -Z; &"streak": a 1 m lens along X; &"tail": the
## same lens lighter toward its tail at -X) placed by `xform` and scaled to `length` metres, fading over `life` seconds. A
## streak or tail is `width` metres across at its middle (0: the lens's own proportions); an arc's width is its mesh's.
## False when none is free.
func smear(xform: Transform3D, kind: StringName, length: float, color: Color, life: float, width: float = 0.0) -> bool:
	var index: int = _free(_smears)
	if index < 0:
		return false
	var slot: Slot = _start(_smears[index], life)
	var across: float = length
	if kind == &"arc":
		slot.node.mesh = FxKit.smear_arc()
	else:
		slot.node.mesh = FxKit.smear_tail() if kind == &"tail" else FxKit.smear_streak()
		if width > 0.0:
			across = width / FxKit.STREAK_WIDTH
	slot.node.transform = Transform3D(xform.basis.scaled_local(Vector3(length, length, across)), xform.origin)
	slot.color = color
	slot.peak = SMEAR_ALPHA
	_draw_fading(slot)
	return true


## A tapered trail from `from` to `to` (world, in the air): its head at `to`, lighter toward `from`, turned to face the camera
## so it reads as a streak on screen, `width` metres across, fading over `life` seconds. False when none is free.
func trail(from: Vector3, to: Vector3, width: float, color: Color, life: float) -> bool:
	var run: Vector3 = to - from
	if run.length() < 0.001:
		return false
	var x: Vector3 = run.normalized()
	var back: Vector3 = _camera_back()
	var y: Vector3 = back - x * back.dot(x)
	y = y.normalized() if y.length_squared() > 0.0001 else Vector3.UP
	var basis: Basis = Basis(x, y, x.cross(y).normalized())
	return smear(Transform3D(basis, (from + to) / 2.0), &"tail", run.length(), color, life, width)


## The direction from the scene toward the camera.
func _camera_back() -> Vector3:
	var camera: Camera3D = get_viewport().get_camera_3d() if is_inside_tree() else null
	return camera.global_basis.z if camera != null else FxKit.CAMERA_BACK


## Dark cracks on the street at `at` turned by `rotation_y` (`kind` &"spokes": a ring and spokes of radius `size.x`;
## &"strip": a `size.x` x `size.y` strip), fading over `life` seconds. False when none is free.
func crack(at: Vector2, rotation_y: float, kind: StringName, size: Vector2, life: float) -> bool:
	var index: int = _free(_cracks)
	if index < 0:
		return false
	var slot: Slot = _start(_cracks[index], life)
	slot.node.mesh = FxKit.crack_spokes() if kind == &"spokes" else FxKit.flat_quad()
	slot.node.position = Vector3(at.x, FxKit.Y_DECAL, at.y)
	slot.node.rotation = Vector3(0.0, rotation_y, 0.0)
	slot.node.scale = Vector3(size.x, 1.0, size.x) if kind == &"spokes" else Vector3(size.x, 1.0, size.y)
	slot.color = Palette.ASPHALT_DARK
	slot.peak = CRACK_ALPHA
	_draw_fading(slot)
	return true


## A pooled fade material (a zone in its last moments multiplies its alpha with it), or null when none is free.
func fade_take() -> StandardMaterial3D:
	for i: int in _fades.size():
		if not _fade_used[i]:
			_fade_used[i] = true
			_fades[i].albedo_color = Color.WHITE
			return _fades[i]
	return null


func fade_give(material: StandardMaterial3D) -> void:
	var i: int = _fades.find(material)
	if i >= 0:
		_fade_used[i] = false


## Advances every live decal to the view's clock.
func step() -> void:
	for slot: Slot in _rings:
		if _alive(slot):
			_draw_ring(slot)
	for slot: Slot in _flashes:
		if _alive(slot):
			_draw_flash(slot)
	for slot: Slot in _smears:
		if _alive(slot):
			_draw_fading(slot)
	for slot: Slot in _cracks:
		if _alive(slot):
			_draw_fading(slot)


func live_rings() -> int:
	return _live(_rings)


func live_flashes() -> int:
	return _live(_flashes)


func live_smears() -> int:
	return _live(_smears)


func live_cracks() -> int:
	return _live(_cracks)


func live_fades() -> int:
	return _fade_used.count(true)


## The rings drawn right now (tests read their size).
func active_rings() -> Array[MeshInstance3D]:
	var found: Array[MeshInstance3D] = []
	for slot: Slot in _rings:
		if slot.active:
			found.append(slot.node)
	return found


## The smears drawn right now (tests read their place and size).
func active_smears() -> Array[MeshInstance3D]:
	var found: Array[MeshInstance3D] = []
	for slot: Slot in _smears:
		if slot.active:
			found.append(slot.node)
	return found


## Every decal node of the pools (tests check heights and draw order).
func all_nodes() -> Array[MeshInstance3D]:
	var found: Array[MeshInstance3D] = []
	for pool: Array[Slot] in [_rings, _flashes, _smears, _cracks]:
		for slot: Slot in pool:
			found.append(slot.node)
	return found


func _start(slot: Slot, life: float) -> Slot:
	slot.active = true
	slot.born = _ctx.time
	slot.life = maxf(life, 0.001)
	slot.node.visible = true
	return slot


func _progress(slot: Slot) -> float:
	return clampf((_ctx.time - slot.born) / slot.life, 0.0, 1.0)


## False (and hidden) once its life is over.
func _alive(slot: Slot) -> bool:
	if not slot.active:
		return false
	if _ctx.time - slot.born >= slot.life - 0.0001:
		slot.active = false
		slot.node.visible = false
		return false
	return true


func _draw_ring(slot: Slot) -> void:
	var t: float = _progress(slot)
	var grow: float = 1.0 if slot.still else 1.0 - (1.0 - t) * (1.0 - t)
	var radius: float = lerpf(slot.from_r, slot.to_r, grow)
	slot.node.scale = Vector3(radius, 1.0, radius)
	slot.material.albedo_color = Color(slot.color, slot.color.a * (1.0 - t))


func _draw_flash(slot: Slot) -> void:
	var age: float = _ctx.time - slot.born
	var alpha: float = slot.peak * (age / slot.rise if slot.rise > 0.0 and age < slot.rise else 1.0 - (age - slot.rise) / maxf(slot.life - slot.rise, 0.001))
	slot.material.albedo_color = Color(slot.color, clampf(alpha, 0.0, 1.0))


func _draw_fading(slot: Slot) -> void:
	slot.material.albedo_color = Color(slot.color, slot.color.a * slot.peak * (1.0 - _progress(slot)))


static func _free(pool: Array[Slot]) -> int:
	for i: int in pool.size():
		if not pool[i].active:
			return i
	return -1


static func _oldest(pool: Array[Slot]) -> int:
	var best: int = 0
	for i: int in pool.size():
		if pool[i].born < pool[best].born:
			best = i
	return best


static func _live(pool: Array[Slot]) -> int:
	var live: int = 0
	for slot: Slot in pool:
		live += 1 if slot.active else 0
	return live


# ---- the marks that are a hit area (cached, exact numbers) -------------------------------------------------------------

## A circle of exactly `radius` metres round the origin in `color`: a see-through fill (`fill_alpha`, none at 0) and an
## opaque rim straddling the edge, on the zone layers of the ladder. Built once per (radius, colour, alphas).
static func circle_mark(radius: float, color: Color, fill_alpha: float, rim_alpha: float = FxKit.RIM_ALPHA) -> ArrayMesh:
	var key: String = "circle|%.3f|%s|%.3f|%.3f" % [radius, color.to_html(), fill_alpha, rim_alpha]
	if not _marks.has(key):
		_marks[key] = mark(AimIndicator.circle(Vector2.ZERO, radius), color, fill_alpha, FxKit.Y_FILL, FxKit.RIM, rim_alpha, FxKit.Y_RIM)
	return _marks[key]


## Only the fill of a circle of `radius` (the growing progress disc), no rim.
static func circle_fill(radius: float, color: Color, fill_alpha: float) -> ArrayMesh:
	var key: String = "disc|%.3f|%s|%.3f" % [radius, color.to_html(), fill_alpha]
	if not _marks.has(key):
		_marks[key] = mark(AimIndicator.circle(Vector2.ZERO, radius), color, fill_alpha, FxKit.Y_FILL + 0.005, 0.0, 0.0, 0.0)
	return _marks[key]


## The wedge of cone weapon `def` (tip on the origin, along -Z, its exact range and angle) on the flash layers.
static func wedge_mark(def: WeaponDef, color: Color, fill_alpha: float) -> ArrayMesh:
	var key: String = "wedge|%s|%s|%.3f" % [def.id, color.to_html(), fill_alpha]
	if not _marks.has(key):
		_marks[key] = mark(wedge(def), color, fill_alpha, FxKit.Y_FLASH, FxKit.RIM, FxKit.RIM_ALPHA, FxKit.Y_FLASH_RIM)
	return _marks[key]


## A delayed cone's warning: [its rim with a faint fill, the fill that grows to the strike] (tip on the origin, along -Z).
static func windup_marks(def: WeaponDef, color: Color, rim_width: float, fill_alpha: float) -> Array[ArrayMesh]:
	var key: String = "windup|%s|%s|%.3f|%.3f" % [def.id, color.to_html(), rim_width, fill_alpha]
	if not _marks.has(key):
		_marks[key] = mark(wedge(def), color, 0.06, FxKit.Y_WINDUP - 0.02, rim_width, 0.8, FxKit.Y_WINDUP_RIM)
		_marks[key + "|fill"] = mark(wedge(def), color, fill_alpha, FxKit.Y_WINDUP, 0.0, 0.0, 0.0)
	return [_marks[key], _marks[key + "|fill"]]


## A wall's team ribbon: a strip `length` x `width` metres along X on the rim layer.
static func segment_mark(length: float, width: float, color: Color) -> ArrayMesh:
	var key: String = "segment|%.3f|%.3f|%s" % [length, width, color.to_html()]
	if not _marks.has(key):
		var half: float = length / 2.0
		_marks[key] = mark(PackedVector2Array([Vector2(-half, 0.0), Vector2(half, 0.0)]), color, 0.0, 0.0, width, FxKit.RIM_ALPHA, FxKit.Y_RIM)
	return _marks[key]


## The outline of cone `def` from the origin along -Z: the tip, the arc, back to the tip.
static func wedge(def: WeaponDef) -> PackedVector2Array:
	var points: PackedVector2Array = PackedVector2Array([Vector2.ZERO])
	var half: float = deg_to_rad(def.angle_degrees / 2.0)
	var steps: int = 12
	for i: int in steps + 1:
		points.append(Vector2(0.0, -1.0).rotated(-half + 2.0 * half * float(i) / float(steps)) * def.max_range)
	points.append(Vector2.ZERO)
	return points


## A fill (`fill_alpha` at height `fill_y`, none at 0) and a rim (`rim_width` wide, `rim_alpha`, at `rim_y`, none at width 0)
## along `outline`, in `color`, as one mesh (the rim drawn after the fill).
static func mark(outline: PackedVector2Array, color: Color, fill_alpha: float, fill_y: float, rim_width: float, rim_alpha: float, rim_y: float) -> ArrayMesh:
	var mesh: ImmediateMesh = ImmediateMesh.new()
	mesh.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
	if fill_alpha > 0.0:
		AimIndicator.fill(mesh, outline, Color(color, fill_alpha), fill_y)
	if rim_width > 0.0:
		AimIndicator.ribbon(mesh, outline, rim_width, Color(color, rim_alpha), rim_y)
	return FxKit.bake(mesh)


## The number of cached hit-area marks (each built once per size and colour).
static func mark_count() -> int:
	return _marks.size()


## Empties the mark cache (the last effect view left the tree).
static func clear_cache() -> void:
	_marks.clear()
