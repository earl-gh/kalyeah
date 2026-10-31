class_name FilmFinish
extends ColorRect
## Reduced motion removes moving grain while keeping the static vignette.

var finish: ShaderMaterial


func _init() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	finish = ShaderMaterial.new()
	finish.shader = preload("res://shaders/cinematic.gdshader")
	material = finish


func _ready() -> void:
	Settings.accessibility_changed.connect(_refresh)
	_refresh()


## The vignette stays out of the part of the screen above `fraction` of its height (where the top boundary pictures stand), so it lies
## behind them instead of darkening them.
func clear_above(fraction: float) -> void:
	finish.set_shader_parameter("clear_above", clampf(fraction, 0.0, 1.0))


func _refresh() -> void:
	finish.set_shader_parameter("grain", 0.0 if Settings.reduced_motion or Settings.performance == &"turbo" else 0.025)
