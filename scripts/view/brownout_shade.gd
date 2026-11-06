class_name BrownoutShade
extends CanvasLayer
## Langit Lupa's brownout over the street (under the HUD): the screen fades dark except a soft circle round your own kid, as big on screen as
## `brownout_sight` metres round it on the street. It fades in and out over `FADE` s; with Reduce motion it is on or off at once. View only.

const SHADER: Shader = preload("res://shaders/brownout.gdshader")
const FADE: float = 0.4
## The soft edge of the circle (a share of its radius).
const FEATHER_SHARE: float = 0.3

var shade: ColorRect
var strength: float = 0.0
var _material: ShaderMaterial


func _init() -> void:
	name = "BrownoutShade"
	layer = 0
	shade = ColorRect.new()
	shade.name = "Shade"
	shade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	shade.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_material = ShaderMaterial.new()
	_material.shader = SHADER
	shade.material = _material
	shade.visible = false
	add_child(shade)


## `dark`: the lights are out; `hole` and `radius`: where your kid is on screen and how far you see (pixels).
func show_state(dark: bool, hole: Vector2, radius: float, delta: float) -> void:
	var target: float = 1.0 if dark else 0.0
	if Settings.reduced_motion or FADE <= 0.0:
		strength = target
	else:
		strength = move_toward(strength, target, delta / FADE)
	shade.visible = strength > 0.0
	if not shade.visible:
		return
	_material.set_shader_parameter("rect_size", shade.size)
	_material.set_shader_parameter("hole", hole)
	_material.set_shader_parameter("radius", radius)
	_material.set_shader_parameter("feather", radius * FEATHER_SHARE)
	_material.set_shader_parameter("strength", strength)
