class_name FxShadows
extends Node3D
## Ground shadows under the skill objects in the air (a thrown rock, a slipper, the marble, the falling meteor, the tossed
## can). With an orthographic camera height has no depth cue, so the shadow says where a thing is and, with its rim in the
## thrower's team colour, who threw it. One quad each (shaders/fx_blob.gdshader), 24 pooled slots built once; over the cap
## an object simply has no shadow (it is still drawn). Smaller and lighter the higher the object is.

const SLOTS: int = 24
const SHADER: Shader = preload("res://shaders/fx_blob.gdshader")
## The shadow is this much of the object's drawn width on the ground.
const SHADOW_SCALE: float = 0.9
## At `HIGH` metres and above the shadow is `HIGH_SIZE` of its ground size and `HIGH_ALPHA` dark (on the ground
## `GROUND_ALPHA`).
const HIGH: float = 3.0
const HIGH_SIZE: float = 0.55
const GROUND_ALPHA: float = 0.45
const HIGH_ALPHA: float = 0.2
## The team ring round the shadow: this wide (metres) and this opaque.
const SHADOW_RIM: float = 0.08
const RIM_ALPHA: float = 0.9

static var _quad: PlaneMesh

var _ctx: FxContext
var _nodes: Array[MeshInstance3D] = []
var _materials: Array[ShaderMaterial] = []
var _widths: PackedFloat32Array = PackedFloat32Array()
var _teams: PackedInt32Array = PackedInt32Array()
var _colors: Array[Color] = []
## Object key -> slot.
var _slot_of: Dictionary[int, int] = {}


## Builds every slot (once).
func setup(ctx: FxContext) -> void:
	_ctx = ctx
	if _quad == null:
		_quad = PlaneMesh.new()
		_quad.size = Vector2.ONE
	for i: int in SLOTS:
		var node: MeshInstance3D = MeshInstance3D.new()
		node.mesh = _quad
		var material: ShaderMaterial = ShaderMaterial.new()
		material.shader = SHADER
		material.render_priority = FxKit.PRIO_SHADOW
		FxKit.count_material()
		node.material_override = material
		node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		node.visible = false
		add_child(node)
		_nodes.append(node)
		_materials.append(material)
		_widths.append(0.0)
		_teams.append(0)
		_colors.append(Color.TRANSPARENT)


## Gives object `key` (`width` metres wide as drawn, thrown by `team`) a shadow. False when every slot is in use.
func attach(key: int, width: float, team: int) -> bool:
	if _slot_of.has(key):
		return true
	for i: int in SLOTS:
		if not _nodes[i].visible:
			_slot_of[key] = i
			_widths[i] = width
			_teams[i] = team
			_colors[i] = Color.TRANSPARENT
			_nodes[i].visible = true
			return true
	return false


## Puts object `key`'s shadow on the ground under `ground` for an object `height` metres up. `size_factor` overrides the
## height rule when >= 0 (the meteor's shadow grows as it falls).
func place(key: int, ground: Vector2, height: float, size_factor: float = -1.0) -> void:
	if not _slot_of.has(key):
		return
	var i: int = _slot_of[key]
	var factor: float = size_factor if size_factor >= 0.0 else size_at(height)
	var size: float = maxf(SHADOW_SCALE * _widths[i] * factor, 0.01)
	_nodes[i].position = Vector3(ground.x, FxKit.Y_SHADOW, ground.y)
	_nodes[i].scale = Vector3(size, 1.0, size)
	var material: ShaderMaterial = _materials[i]
	material.set_shader_parameter(&"alpha", alpha_at(height))
	material.set_shader_parameter(&"rim_width", clampf(SHADOW_RIM / (size / 2.0), 0.0, 0.5))
	var rim: Color = Color(_ctx.team_color(_teams[i]), RIM_ALPHA)
	if rim != _colors[i]:
		_colors[i] = rim
		material.set_shader_parameter(&"rim_color", rim)


func detach(key: int) -> void:
	if _slot_of.has(key):
		_nodes[_slot_of[key]].visible = false
		_slot_of.erase(key)


func live() -> int:
	return _slot_of.size()


## The shadow node of object `key`, or null.
func node_of(key: int) -> MeshInstance3D:
	return _nodes[_slot_of[key]] if _slot_of.has(key) else null


## Every slot (tests check heights and draw order).
func all_nodes() -> Array[MeshInstance3D]:
	return _nodes


## Size of a shadow (of its ground size) under an object `height` metres up.
static func size_at(height: float) -> float:
	return lerpf(1.0, HIGH_SIZE, clampf(height / HIGH, 0.0, 1.0))


## Darkness of a shadow under an object `height` metres up.
static func alpha_at(height: float) -> float:
	return lerpf(GROUND_ALPHA, HIGH_ALPHA, clampf(height / HIGH, 0.0, 1.0))
