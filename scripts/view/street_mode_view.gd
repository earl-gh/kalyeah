class_name StreetModeView
extends Node3D
## What a street game adds to the street (docs/GDD.md "Tumbang Preso and Patintero"): the chalk lines (the throw line and the far line, and
## Patintero's court), the can (the lata's model) and every slipper lying on the ground (the tsinelas' model), the ring on your own
## slipper, the taya's marker, and the local thrower's aim line with the short guide of where the can and the slipper would go. The flying
## slipper is the weapons' own, drawn by WeaponFxView like Agawan Base's; the can borrows the lata's tumble, landing squash and dust.
## Everything lies on the ground (nothing is drawn over a kid). Reads the sim only. The chalk is code-drawn until the owner's decal.

const CHALK_SHADER: Shader = preload("res://shaders/chalk_line.gdshader")
## Chalk lines: how wide (m) and how high above the ground.
const LINE_WIDTH: float = 0.32
const LINE_LIFT: float = 0.03
const HOME_CHALK: Color = Color(1.0, 1.0, 1.0, 0.95)
const GUARD_CHALK: Color = Color(1.0, 0.86, 0.35, 0.95)
## How much of a line is bare: the long white lines (the boundaries), the guards' lines.
const HOME_BREAKS: float = 0.2
const GUARD_BREAKS: float = 0.3
## A flung can or slipper arcs this high (m) over a fling; the can turns `FLING_TURNS` times end over end on each fling and ends on its side,
## resting `LYING_SHARE` of its height above the street. A slipper turns this many times a second in the air.
const FLING_PEAK: float = 1.6
const FLING_TURNS: float = 1.0
const LYING_SHARE: float = 0.4
const SLIPPER_SPIN: float = 3.0
## A carried can is held at this height (m, before the kids' view scale).
const CARRY_HEIGHT: float = 1.1
## Ground marks sit this high (m) so they lie on the street without fighting its texture.
const GROUND_LIFT: float = 0.06
## The aim line: how wide (m) and the ring at its end; it is white while short and turns gold, and pulses, at full length.
const AIM_WIDTH: float = 0.5
const AIM_RING: float = 0.9
const AIM_COLOR: Color = Color(1.0, 1.0, 1.0, 0.8)
const AIM_FULL_COLOR: Color = Color(1.0, 0.8, 0.2, 0.95)
const AIM_PULSE: float = 10.0
## The guide of where the can goes is a solid line, where the slipper goes a dashed one: how wide, and how many dashes.
const GUIDE_WIDTH: float = 0.4
const GUIDE_DASHES: int = 5
const CAN_GUIDE_COLOR: Color = Color(1.0, 0.55, 0.15, 0.95)
const SLIPPER_GUIDE_COLOR: Color = Color(0.4, 0.9, 1.0, 0.95)
## The ring on your own slipper: white outline and a white tint over the pick-up area; it breathes a little.
const PICKUP_TINT: Color = Color(1.0, 1.0, 1.0, 0.3)
const PICKUP_OUTLINE: Color = Color(1.0, 1.0, 1.0, 0.95)
const PICKUP_OUTLINE_WIDTH: float = 0.14
const PICKUP_BREATH: float = 4.0
## The taya's marker: a red ring on the ground round the kid and the word IT over its head.
const TAYA_COLOR: Color = Color(1.0, 0.25, 0.22, 0.9)
const TAYA_RING: float = 1.0
const TAYA_WORD_HEIGHT: float = 5.0
const TAYA_WORD_SIZE: float = 0.014

var _sim: MatchSim
var _mode: StreetMode
var _ctx: FxContext
var _local_id: int = -1
var _time: float = 0.0
var _lane_width: float = 0.0
var _guard_lines: Node3D
var _can: Node3D
var _can_fling: Dictionary = {}
var _slippers: Dictionary[int, Node3D] = {}
var _slipper_fling: Dictionary[int, Dictionary] = {}
var _aim_line: MeshInstance3D
var _aim_ring: MeshInstance3D
var _aim_material: StandardMaterial3D
var _can_guide: MeshInstance3D
var _slipper_guide: Array[MeshInstance3D] = []
var _pickup: Node3D
var _taya_marker: Node3D


## Draws the lines of `sim`'s game (nothing for Agawan Base). `ctx` is WeaponFxView's (its dust and the kids' view scale).
func setup(sim: MatchSim, ctx: FxContext, local_id: int) -> void:
	_sim = sim
	_mode = sim.mode
	_ctx = ctx
	_local_id = local_id
	if _mode == null:
		return
	_lane_width = sim.layout.lane_width
	_add_chalk(Vector2(0.0, _mode.rules.home_z), _lane_width, 0.0, HOME_CHALK, HOME_BREAKS, 1.0)
	_add_chalk(Vector2(0.0, _mode.rules.goal_z), _lane_width, 0.0, HOME_CHALK, HOME_BREAKS, 2.0)
	_guard_lines = Node3D.new()
	_guard_lines.name = "GuardLines"
	add_child(_guard_lines)
	if _mode is TumbangPreso:
		var game: TumbangPreso = _mode as TumbangPreso
		_can = WeaponModel.rig(WeaponModel.make(&"lata", true))
		_can.name = "Can"
		add_child(_can)
		game.can_flung.connect(_on_can_flung)
		game.slipper_flung.connect(_on_slipper_flung)
		game.slipper_landed.connect(_on_slipper_landed)
		_build_aim()
		_build_pickup()
		_build_taya_marker()
	if _mode is Patintero:
		var span: float = _mode.rules.line_span
		for index: int in 2:
			_add_chalk(Vector2(0.0, span if index == 0 else -span), _lane_width, 0.0, GUARD_CHALK, GUARD_BREAKS, 3.0 + float(index), _guard_lines)
		_add_chalk(Vector2.ZERO, span * 2.0, PI / 2.0, GUARD_CHALK, GUARD_BREAKS, 5.0, _guard_lines)


## One frame of the picture (PracticeMatch calls it from its presentation, like every other view): the can, the slippers, the marks.
func sync(delta: float) -> void:
	_time += delta
	if _mode is TumbangPreso:
		_sync_can()
		_sync_slippers(delta)
		_sync_aim()
		_sync_pickup()
		_sync_taya()


## The can (null outside Tumbang Preso), for tests.
func can_node() -> Node3D:
	return _can


func slipper_node(id: int) -> Node3D:
	return _slippers.get(id) as Node3D


func guard_line_count() -> int:
	return _guard_lines.get_child_count() if _guard_lines != null else 0


## The aim line and its ring are drawn right now.
func aim_visible() -> bool:
	return _aim_line != null and _aim_line.visible


## How long the aim line is drawn (m).
func aim_length() -> float:
	return _aim_line.scale.x if aim_visible() else 0.0


## The guide of where the can would go is drawn (solid) and where the slipper would go (dashed).
func guides_visible() -> bool:
	return _can_guide != null and _can_guide.visible and _slipper_guide[0].visible


## The can is mid-fling.
func can_flying() -> bool:
	return not _can_fling.is_empty()


## The ring on your own slipper is drawn.
func pickup_visible() -> bool:
	return _pickup != null and _pickup.visible


## The marker of the taya is drawn at `id`.
func taya_marker_at() -> Vector3:
	return _taya_marker.position


func _scale() -> float:
	return _ctx.view_scale if _ctx != null else 1.0


func _can_height() -> float:
	return (WeaponModel.OBJECTS[&"lata"][1] as float) * _scale()


func _drawn(state: PlayerState) -> Vector2:
	return _ctx.blend.kid(state) if _ctx != null and _ctx.blend != null else state.position


# ---- flings ------------------------------------------------------------------------------

## Where a thing flung from `from` over `path` is at `age` s: its place on the ground, how high in its arc (0 to 1), how far through the
## whole trip (0 to 1) and the hop it is on.
func _hop(from: Vector2, path: Array[Vector2], age: float) -> Dictionary:
	var time: float = (_mode as TumbangPreso).rules.fling_time
	var hops: int = path.size()
	var index: int = mini(int(age / time), hops - 1)
	var t: float = clampf((age - float(index) * time) / time, 0.0, 1.0)
	var start: Vector2 = from if index == 0 else path[index - 1]
	return {"at": start.lerp(path[index], t), "arc": 4.0 * t * (1.0 - t) / float(index + 1), "progress": clampf(age / (time * float(hops)), 0.0, 1.0), "index": index, "done": age >= time * float(hops)}


func _on_can_flung(from: Vector2, path: Array[Vector2]) -> void:
	_can_fling = {"from": from, "path": path, "start": _time, "hop": 0}


func _on_slipper_flung(id: int, from: Vector2, path: Array[Vector2]) -> void:
	var node: Node3D = _slipper_node_for(id)
	node.position = Vector3(from.x, 0.0, from.y)
	_slipper_fling[id] = {"from": from, "path": path, "start": _time}


func _on_slipper_landed(id: int, at: Vector2, _hit: bool) -> void:
	var node: Node3D = _slipper_node_for(id)
	node.position = Vector3(at.x, 0.0, at.y)
	node.rotation.y = randf() * TAU


func _slipper_node_for(id: int) -> Node3D:
	var node: Node3D = _slippers.get(id) as Node3D
	if node == null:
		node = WeaponModel.make(&"tsinelas_light", false)
		node.name = "Slipper%d" % id
		add_child(node)
		_slippers[id] = node
	return node


# ---- the can -----------------------------------------------------------------------------

func _sync_can() -> void:
	var game: TumbangPreso = _mode as TumbangPreso
	var height: float = _can_height()
	var carrier: PlayerState = _sim.players.get(game.can_carrier) as PlayerState
	if not _can_fling.is_empty():
		_fly_can(height)
	elif carrier != null:
		var at: Vector2 = _drawn(carrier)
		_place_can(Vector3(at.x, CARRY_HEIGHT * _scale() + height / 2.0, at.y), Basis.IDENTITY)
	elif game.can_up:
		_place_can(Vector3(game.can_position.x, height / 2.0, game.can_position.y), Basis.IDENTITY)
	else:
		_lay_can(game.can_position, height)


func _place_can(position_at: Vector3, basis_at: Basis, squash: Vector3 = Vector3.ONE) -> void:
	_can.position = position_at
	_can.basis = basis_at.scaled(Vector3.ONE * _scale())
	var node: Node3D = _can.get_node_or_null("Squash") as Node3D
	if node != null:
		node.basis = Basis.from_scale(squash)


## The can flies along its fling, tumbling end over end on every hop, and lands on its side with the lata's squash and a puff of dust.
func _fly_can(height: float) -> void:
	var from: Vector2 = _can_fling["from"] as Vector2
	var path: Array[Vector2] = _can_fling["path"] as Array[Vector2]
	var age: float = _time - (_can_fling["start"] as float)
	var step: Dictionary = _hop(from, path, age)
	var at: Vector2 = step["at"] as Vector2
	var index: int = step["index"] as int
	if index != (_can_fling["hop"] as int):
		_can_fling["hop"] = index
		_dust(from if index == 0 else path[index - 1])
	var way: Vector2 = (path[-1] - from).normalized() if from.distance_to(path[-1]) > 0.01 else Vector2(0.0, -1.0)
	var axis: Vector3 = Vector3(way.x, 0.0, way.y).cross(Vector3.UP).normalized()
	var progress: float = step["progress"] as float
	var angle: float = (TAU * FLING_TURNS * float(path.size()) + PI / 2.0) * progress
	var lift: float = (step["arc"] as float) * FLING_PEAK * _scale()
	var base: float = lerpf(height / 2.0, height * LYING_SHARE, progress)
	var squash: Vector3 = Vector3.ONE
	if step["done"] as bool:
		var frames: float = (age - (_mode as TumbangPreso).rules.fling_time * float(path.size())) / FxSquash.FRAME
		if not _can_fling.has("landed"):
			_can_fling["landed"] = true
			_dust(path[-1])
		if frames >= float(FxSquash.landing_frames(FxLandings.CAN_LAND_FRAMES)):
			_can_fling = {}
			return
		squash = FxSquash.along(FxSquash.landing(FxLandings.CAN_LAND_S, frames, FxLandings.CAN_LAND_FRAMES))
	_place_can(Vector3(at.x, base + lift, at.y), Basis(axis, angle), squash)


## The can lies on its side where it fell, turned along the way it flew.
func _lay_can(at: Vector2, height: float) -> void:
	var axis: Vector3 = Vector3(0.0, 0.0, -1.0).cross(Vector3.UP).normalized()
	_place_can(Vector3(at.x, height * LYING_SHARE, at.y), Basis(axis, PI / 2.0))


func _dust(at: Vector2) -> void:
	if _ctx != null:
		_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(at.x, 0.0, at.y), FxLandings.CAN_DUST)


# ---- the slippers ------------------------------------------------------------------------

func _sync_slippers(delta: float) -> void:
	var game: TumbangPreso = _mode as TumbangPreso
	for id: int in _slippers:
		var node: Node3D = _slippers[id]
		node.scale = Vector3.ONE * _scale()
		var flying: bool = _slipper_fling.has(id)
		node.visible = flying or game.slippers.has(id)
		if flying:
			_fly_slipper(id, node, delta)
		elif game.slippers.has(id):
			node.position = Vector3(game.slippers[id].x, 0.0, game.slippers[id].y)


## A flung slipper hops to where it lies, spinning flat in the air.
func _fly_slipper(id: int, node: Node3D, delta: float) -> void:
	var fling: Dictionary = _slipper_fling[id]
	var path: Array[Vector2] = fling["path"] as Array[Vector2]
	var step: Dictionary = _hop(fling["from"] as Vector2, path, _time - (fling["start"] as float))
	var at: Vector2 = step["at"] as Vector2
	node.position = Vector3(at.x, (step["arc"] as float) * FLING_PEAK * _scale(), at.y)
	node.rotation.y += delta * TAU * SLIPPER_SPIN
	if step["done"] as bool:
		_slipper_fling.erase(id)
		_dust(path[-1])


## Your own slipper, once it lies still, has a white ring (and a white tint) as big as the area that picks it up; nobody else's has one.
func _sync_pickup() -> void:
	var game: TumbangPreso = _mode as TumbangPreso
	var shown: bool = game.slippers.has(_local_id) and not _slipper_fling.has(_local_id) and game.slipper_busy.get(_local_id, 0.0) <= 0.0
	_pickup.visible = shown
	if not shown:
		return
	var at: Vector2 = game.slippers[_local_id]
	_pickup.position = Vector3(at.x, GROUND_LIFT, at.y)
	var breath: float = 1.0 if Settings.reduced_motion else 1.0 + 0.04 * sin(_time * PICKUP_BREATH)
	_pickup.scale = Vector3(breath, 1.0, breath)


func _build_pickup() -> void:
	var radius: float = (_mode as TumbangPreso).rules.pickup_radius
	_pickup = Node3D.new()
	_pickup.name = "PickupRing"
	_pickup.visible = false
	add_child(_pickup)
	var fill: CylinderMesh = CylinderMesh.new()
	fill.top_radius = radius
	fill.bottom_radius = radius
	fill.height = 0.01
	fill.radial_segments = 32
	fill.rings = 1
	fill.material = _ground_material(PICKUP_TINT)
	_pickup.add_child(_ground_mesh(fill, "Tint", true))
	var ring: TorusMesh = TorusMesh.new()
	ring.inner_radius = radius - PICKUP_OUTLINE_WIDTH
	ring.outer_radius = radius
	ring.rings = 32
	ring.ring_segments = 4
	ring.material = _ground_material(PICKUP_OUTLINE)
	var outline: MeshInstance3D = _ground_mesh(ring, "Outline", true)
	outline.scale = Vector3(1.0, 0.04, 1.0)
	_pickup.add_child(outline)


# ---- the taya ----------------------------------------------------------------------------

func _build_taya_marker() -> void:
	_taya_marker = Node3D.new()
	_taya_marker.name = "TayaMarker"
	add_child(_taya_marker)
	var ring: TorusMesh = TorusMesh.new()
	ring.inner_radius = TAYA_RING * 0.82
	ring.outer_radius = TAYA_RING
	ring.rings = 32
	ring.ring_segments = 4
	ring.material = _ground_material(TAYA_COLOR)
	var disc: MeshInstance3D = _ground_mesh(ring, "Ring", true)
	disc.scale = Vector3(1.0, 0.04, 1.0)
	_taya_marker.add_child(disc)
	var word: Label3D = Label3D.new()
	word.name = "It"
	word.text = "IT"
	word.font = KalyeahTheme.ACTION_FONT
	word.font_size = 96
	word.pixel_size = TAYA_WORD_SIZE
	word.modulate = Color(1.0, 0.85, 0.3)
	word.outline_modulate = Color(0.45, 0.05, 0.05)
	word.outline_size = 24
	word.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	word.no_depth_test = true
	word.position = Vector3(0.0, TAYA_WORD_HEIGHT, 0.0)
	_taya_marker.add_child(word)


func _sync_taya() -> void:
	var taya: PlayerState = _sim.players.get((_mode as TumbangPreso).taya_id) as PlayerState
	_taya_marker.visible = taya != null
	if taya == null:
		return
	var at: Vector2 = _drawn(taya)
	_taya_marker.position = Vector3(at.x, GROUND_LIFT, at.y)


# ---- the aim line and the guides ---------------------------------------------------------

func _build_aim() -> void:
	_aim_material = _ground_material(AIM_COLOR)
	var box: BoxMesh = BoxMesh.new()
	box.size = Vector3(1.0, 0.01, AIM_WIDTH)
	box.material = _aim_material
	_aim_line = _ground_mesh(box, "AimLine")
	add_child(_aim_line)
	var ring: TorusMesh = TorusMesh.new()
	ring.inner_radius = AIM_RING * 0.7
	ring.outer_radius = AIM_RING
	ring.rings = 24
	ring.ring_segments = 6
	ring.material = _aim_material
	_aim_ring = _ground_mesh(ring, "AimRing")
	_aim_ring.scale = Vector3(1.0, 0.05, 1.0)
	add_child(_aim_ring)
	var solid: BoxMesh = BoxMesh.new()
	solid.size = Vector3(1.0, 0.01, GUIDE_WIDTH)
	solid.material = _ground_material(CAN_GUIDE_COLOR)
	_can_guide = _ground_mesh(solid, "CanGuide")
	add_child(_can_guide)
	var dash: BoxMesh = BoxMesh.new()
	dash.size = Vector3(1.0, 0.01, GUIDE_WIDTH)
	dash.material = _ground_material(SLIPPER_GUIDE_COLOR)
	for index: int in GUIDE_DASHES:
		var piece: MeshInstance3D = _ground_mesh(dash, "SlipperGuide%d" % index)
		add_child(piece)
		_slipper_guide.append(piece)


## The local thrower's line from the kid along the aim, as long as the hold has made it (it stops where it meets the can); white while
## short, gold and pulsing at full length. When it meets the can, a short solid line shows where the can would go and a dashed one where the
## slipper would, both only `guide_length` long.
func _sync_aim() -> void:
	var game: TumbangPreso = _mode as TumbangPreso
	var aiming: bool = _sim.players.has(_local_id) and game.is_aiming(_local_id) and _sim.phase == MatchSim.Phase.PLAYING
	_aim_line.visible = aiming
	_aim_ring.visible = aiming
	var guide: Dictionary = game.aim_guide(_sim, _local_id) if aiming else {}
	_can_guide.visible = not guide.is_empty()
	for piece: MeshInstance3D in _slipper_guide:
		piece.visible = not guide.is_empty()
	if not aiming:
		return
	var state: PlayerState = _sim.players[_local_id]
	var start: Vector2 = _drawn(state)
	var way: Vector2 = game.aim_dir[_local_id]
	var length: float = game.aim_range(_local_id)
	if not guide.is_empty():
		length = minf(length, start.distance_to(guide["point"] as Vector2))
	_lay_line(_aim_line, start, way, length)
	var end: Vector2 = start + way * length
	_aim_ring.position = Vector3(end.x, GROUND_LIFT, end.y)
	var full: bool = game.charge(_local_id) >= 1.0
	var color: Color = AIM_FULL_COLOR if full else AIM_COLOR
	if full and not Settings.reduced_motion:
		color.a *= 0.75 + 0.25 * sin(_time * AIM_PULSE)
	_aim_material.albedo_color = color
	if guide.is_empty():
		return
	var reach: float = game.rules.guide_length
	_lay_line(_can_guide, game.can_position, guide["can_dir"] as Vector2, reach)
	var dash_length: float = reach / float(GUIDE_DASHES * 2)
	for index: int in GUIDE_DASHES:
		_lay_line(_slipper_guide[index], (guide["point"] as Vector2) + (guide["slipper_dir"] as Vector2) * dash_length * float(index * 2), guide["slipper_dir"] as Vector2, dash_length)


## A flat line mesh (a box one metre long) laid on the ground from `from` along `way` for `length` m.
func _lay_line(line: MeshInstance3D, from: Vector2, way: Vector2, length: float) -> void:
	var middle: Vector2 = from + way * length / 2.0
	line.scale = Vector3(length, 1.0, 1.0)
	line.position = Vector3(middle.x, GROUND_LIFT, middle.y)
	line.rotation.y = atan2(-way.y, way.x)


## An unshaded see-through material that is tested against the depth: a mark on the ground, hidden behind whatever stands on it.
func _ground_material(color: Color) -> StandardMaterial3D:
	var material: StandardMaterial3D = StandardMaterial3D.new()
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	material.albedo_color = color
	material.cull_mode = BaseMaterial3D.CULL_DISABLED
	return material


func _ground_mesh(mesh: Mesh, node_name: String, shown: bool = false) -> MeshInstance3D:
	var instance: MeshInstance3D = MeshInstance3D.new()
	instance.name = node_name
	instance.mesh = mesh
	instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	instance.visible = shown
	return instance


# ---- the chalk ---------------------------------------------------------------------------

## A chalk line `length` m long centred on `center` (x, z), turned `yaw` radians (a quarter turn for a line along the street); `seed` makes it
## break in its own places.
func _add_chalk(center: Vector2, length: float, yaw: float, chalk: Color, breaks: float, seed: float, parent: Node3D = self) -> void:
	var plane: PlaneMesh = PlaneMesh.new()
	plane.size = Vector2(length, LINE_WIDTH)
	var material: ShaderMaterial = ShaderMaterial.new()
	material.shader = CHALK_SHADER
	material.set_shader_parameter("chalk", chalk)
	material.set_shader_parameter("length_m", length)
	material.set_shader_parameter("width_m", LINE_WIDTH)
	material.set_shader_parameter("seed", seed * 7.31)
	material.set_shader_parameter("breaks", breaks)
	plane.material = material
	var line: MeshInstance3D = MeshInstance3D.new()
	line.name = "Line"
	line.mesh = plane
	line.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	line.position = Vector3(center.x, LINE_LIFT, center.y)
	line.rotation.y = yaw
	parent.add_child(line)
