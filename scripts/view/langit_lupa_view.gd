class_name LangitLupaView
extends Node3D
## Langit Lupa's street (docs/GDD.md "Langit Lupa"): the langit platforms, which darken and sink as they wear and are gone once broken (with a
## puff of dust), and the mounds the kids raise, which rise out of the street and sink back. Code-drawn stand-ins until the owner's models
## (a Blender platform, worn and broken looks, a mound). Reads the sim only. A kid on a langit is drawn standing on it (`ground_height`).

## A platform stands this high (m); worn out it has sunk to `WORN_HEIGHT_SHARE` of that and turned `WORN` from `WOOD`.
const PLATFORM_HEIGHT: float = 0.35
const WORN_HEIGHT_SHARE: float = 0.55
const WOOD: Color = Color("c68a4a")
const WORN: Color = Color("6b4a2c")
## A rim of chalk round each platform, so it reads as a place to stand at phone size.
const RIM: Color = Color(1.0, 1.0, 1.0, 0.85)
const RIM_WIDTH: float = 0.12
## A mound: an earth cone this high (m), its top `MOUND_TOP_SHARE` of its radius; it rises and sinks over `MOUND_RISE` s.
const MOUND_HEIGHT: float = 0.45
const MOUND_TOP_SHARE: float = 0.7
const MOUND_RISE: float = 0.25
const EARTH: Color = Color("8a5a32")
const BROKEN_DUST: int = 8

var _sim: MatchSim
var _game: LangitLupa
var _ctx: FxContext
var _platforms: Array[Node3D] = []
var _materials: Array[StandardMaterial3D] = []
var _mounds: Dictionary[int, MeshInstance3D] = {}
## id -> how far up its mound is drawn (0 to 1); a sinking mound keeps its node until it is down.
var _rise: Dictionary[int, float] = {}
var _sinking: Dictionary[int, MeshInstance3D] = {}


func setup(sim: MatchSim, ctx: FxContext) -> void:
	name = "LangitLupaView"
	_sim = sim
	_game = sim.mode as LangitLupa
	_ctx = ctx
	for index: int in _game.rules.langit_spots.size():
		_build_platform(_game.rules.langit_spots[index])
	_game.spot_broke.connect(_on_spot_broke)


## How high a kid standing at `at` is drawn: on a standing platform or a mound, on top of it; else on the street.
func ground_height(at: Vector2) -> float:
	var spot: int = _game.spot_at(at)
	if spot >= 0:
		return _platforms[spot].scale.y * PLATFORM_HEIGHT
	var mound: int = _game.mound_at(at)
	if mound >= 0:
		return MOUND_HEIGHT * _rise.get(mound, 1.0)
	return 0.0


func sync(delta: float) -> void:
	for index: int in _platforms.size():
		var standing: bool = _game.spot_standing(index)
		var platform: Node3D = _platforms[index]
		platform.visible = standing
		if standing:
			var wear: float = _game.wear_share(index)
			platform.scale.y = lerpf(1.0, WORN_HEIGHT_SHARE, wear)
			_materials[index].albedo_color = WOOD.lerp(WORN, wear)
	for id: int in _game.mounds:
		if not _mounds.has(id):
			_mounds[id] = _build_mound()
			_rise[id] = 0.0
		_place_mound(_mounds[id], _game.mounds[id], delta, id, true)
	for id: int in _mounds.keys():
		if not _game.mounds.has(id):
			_sinking[id] = _mounds[id]
			_mounds.erase(id)
	for id: int in _sinking.keys():
		var node: MeshInstance3D = _sinking[id]
		_place_mound(node, Vector2(node.position.x, node.position.z), delta, id, false)
		if _rise.get(id, 0.0) <= 0.0:
			node.queue_free()
			_sinking.erase(id)
			_rise.erase(id)


## The platforms drawn now (for tests): index -> visible.
func platform_shown(index: int) -> bool:
	return _platforms[index].visible


func mound_count() -> int:
	return _mounds.size()


func _place_mound(node: MeshInstance3D, at: Vector2, delta: float, id: int, up: bool) -> void:
	var step: float = delta / MOUND_RISE if MOUND_RISE > 0.0 else 1.0
	_rise[id] = clampf(_rise.get(id, 0.0) + (step if up else -step), 0.0, 1.0)
	var share: float = maxf(_rise[id], 0.01)
	node.scale = Vector3(1.0, share, 1.0)
	node.position = Vector3(at.x, MOUND_HEIGHT * share / 2.0, at.y)  # the cone's middle: its foot stays on the street
	node.visible = _rise[id] > 0.0


func _build_platform(rect: Rect2) -> void:
	var box: BoxMesh = BoxMesh.new()
	box.size = Vector3(rect.size.x, PLATFORM_HEIGHT, rect.size.y)
	var material: StandardMaterial3D = StandardMaterial3D.new()
	material.albedo_color = WOOD
	material.roughness = 0.9
	box.material = material
	# the holder stands on the street and the box sits on it: sinking the holder (its height scale) keeps the box on the ground
	var platform: Node3D = Node3D.new()
	platform.name = "Langit%d" % _platforms.size()
	var center: Vector2 = rect.get_center()
	platform.position = Vector3(center.x, 0.0, center.y)
	var body: MeshInstance3D = MeshInstance3D.new()
	body.name = "Box"
	body.mesh = box
	body.position = Vector3(0.0, PLATFORM_HEIGHT / 2.0, 0.0)
	platform.add_child(body)
	var rim: MeshInstance3D = MeshInstance3D.new()
	rim.name = "Rim"
	var frame: BoxMesh = BoxMesh.new()
	frame.size = Vector3(rect.size.x + RIM_WIDTH * 2.0, 0.02, rect.size.y + RIM_WIDTH * 2.0)
	var chalk: StandardMaterial3D = StandardMaterial3D.new()
	chalk.albedo_color = RIM
	chalk.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	chalk.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	frame.material = chalk
	rim.mesh = frame
	rim.position = Vector3(0.0, 0.015, 0.0)
	platform.add_child(rim)
	add_child(platform)
	_platforms.append(platform)
	_materials.append(material)


func _build_mound() -> MeshInstance3D:
	var cone: CylinderMesh = CylinderMesh.new()
	cone.bottom_radius = _game.rules.mound_radius
	cone.top_radius = _game.rules.mound_radius * MOUND_TOP_SHARE
	cone.height = MOUND_HEIGHT
	cone.radial_segments = 20
	cone.rings = 1
	var material: StandardMaterial3D = StandardMaterial3D.new()
	material.albedo_color = EARTH
	material.roughness = 1.0
	cone.material = material
	var node: MeshInstance3D = MeshInstance3D.new()
	node.name = "Mound"
	node.mesh = cone
	add_child(node)
	return node


func _on_spot_broke(index: int) -> void:
	var at: Vector2 = _game.rules.langit_spots[index].get_center()
	if _ctx != null and _ctx.particles != null:
		_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(at.x, 0.0, at.y), BROKEN_DUST)
