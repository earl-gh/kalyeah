class_name LangitLupaView
extends Node3D
## Langit Lupa's street (docs/GDD.md "Langit Lupa"): the langit platforms, which darken and sink as they wear and are gone once broken (with a
## puff of dust), and the mounds the kids raise, which rise out of the street and sink back. Code-drawn stand-ins until the owner's models
## The platforms are the owner's crates model and the mounds the owner's sacks model (`PropModel`, the same matte look as the street's
## props), fitted to their size, until models of their own are made (docs/art-briefs/langit-platform.md, lupa-mound.md). Reads the sim only.
## A kid on a langit is drawn standing on it (`ground_height`).

## The owner's models standing in for the platform and the mound.
const PLATFORM_MODEL: String = "crates"
const MOUND_MODEL: String = "sacks"
## A platform stands this high (m); worn out it has sunk to `WORN_HEIGHT_SHARE` of that, and its chalk rim has turned from `RIM` to
## `RIM_WORN` (the warning that it is about to break).
const PLATFORM_HEIGHT: float = 0.6
const WORN_HEIGHT_SHARE: float = 0.55
## A rim of chalk round each platform, so it reads as a place to stand at phone size.
const RIM: Color = Color(1.0, 1.0, 1.0, 0.85)
const RIM_WORN: Color = Color(1.0, 0.35, 0.25, 0.9)
const RIM_WIDTH: float = 0.12
## A mound stands this high (m); it rises and sinks over `MOUND_RISE` s.
const MOUND_HEIGHT: float = 0.45
const MOUND_RISE: float = 0.25
const BROKEN_DUST: int = 8

var _sim: MatchSim
var _game: LangitLupa
var _ctx: FxContext
var _platforms: Array[Node3D] = []
var _rims: Array[StandardMaterial3D] = []
var _mounds: Dictionary[int, Node3D] = {}
## id -> how far up its mound is drawn (0 to 1); a sinking mound keeps its node until it is down.
var _rise: Dictionary[int, float] = {}
var _sinking: Dictionary[int, Node3D] = {}


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
			_rims[index].albedo_color = RIM.lerp(RIM_WORN, wear)
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
		var node: Node3D = _sinking[id]
		_place_mound(node, Vector2(node.position.x, node.position.z), delta, id, false)
		if _rise.get(id, 0.0) <= 0.0:
			node.queue_free()
			_sinking.erase(id)
			_rise.erase(id)


## The platforms drawn now (for tests): index -> visible.
func platform_shown(index: int) -> bool:
	return _platforms[index].visible


## The model a platform or a mound is drawn with (for tests): the owner's crates and sacks.
func model_of_platform(index: int) -> Node3D:
	return _platforms[index].get_node("Model") as Node3D


func model_of_mound(id: int) -> Node3D:
	return _mounds[id].get_node("Model") as Node3D if _mounds.has(id) else null


func mound_count() -> int:
	return _mounds.size()


func _place_mound(node: Node3D, at: Vector2, delta: float, id: int, up: bool) -> void:
	var step: float = delta / MOUND_RISE if MOUND_RISE > 0.0 else 1.0
	_rise[id] = clampf(_rise.get(id, 0.0) + (step if up else -step), 0.0, 1.0)
	var share: float = maxf(_rise[id], 0.01)
	node.scale = Vector3(1.0, share, 1.0)
	node.position = Vector3(at.x, 0.0, at.y)  # the model's foot is its origin: it grows up out of the street
	node.visible = _rise[id] > 0.0


func _build_platform(rect: Rect2) -> void:
	# the holder stands on the street and the model sits on it: sinking the holder (its height scale) keeps the model on the ground
	var platform: Node3D = Node3D.new()
	platform.name = "Langit%d" % _platforms.size()
	var center: Vector2 = rect.get_center()
	platform.position = Vector3(center.x, 0.0, center.y)
	var body: Node3D = PropModel.fit(PLATFORM_MODEL, rect.size, PLATFORM_HEIGHT, true)
	body.name = "Model"
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
	_rims.append(chalk)


## A mound: the holder grows (its height scale) and the fitted model stands on it.
func _build_mound() -> Node3D:
	var node: Node3D = Node3D.new()
	node.name = "Mound"
	var diameter: float = _game.rules.mound_radius * 2.0
	var body: Node3D = PropModel.fit(MOUND_MODEL, Vector2(diameter, diameter), MOUND_HEIGHT, true)
	body.name = "Model"
	node.add_child(body)
	add_child(node)
	return node


func _on_spot_broke(index: int) -> void:
	var at: Vector2 = _game.rules.langit_spots[index].get_center()
	if _ctx != null and _ctx.particles != null:
		_ctx.particles.puff(FxParticles.Preset.DUST, Vector3(at.x, 0.0, at.y), BROKEN_DUST)
