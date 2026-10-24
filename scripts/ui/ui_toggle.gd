class_name UiToggle
extends Button
## An on/off switch: a rounded track (green when on) with a round knob that slides across. Drawn in
## code, one thumb-sized target. `button_pressed` is the state; the knob eases over unless Reduce
## motion is on.

const WIDTH: float = 128.0
const HEIGHT: float = 72.0

var _knob: float = 0.0


func _init() -> void:
	toggle_mode = true
	focus_mode = Control.FOCUS_ALL
	custom_minimum_size = Vector2(WIDTH, UiMetrics.TOUCH)
	size_flags_vertical = Control.SIZE_SHRINK_CENTER
	for state: String in ["normal", "hover", "pressed", "hover_pressed", "disabled"]:
		add_theme_stylebox_override(state, StyleBoxEmpty.new())
	add_theme_stylebox_override("focus", UiSkin.focus_ring(UiSkin.round_radius(UiMetrics.TOUCH)))
	toggled.connect(_on_toggled)


func _ready() -> void:
	_knob = 1.0 if button_pressed else 0.0
	queue_redraw()


func _on_toggled(on: bool) -> void:
	var target: float = 1.0 if on else 0.0
	if Settings.reduced_motion or not is_inside_tree():
		_knob = target
		queue_redraw()
		return
	var tween: Tween = create_tween()
	tween.tween_method(_set_knob, _knob, target, 0.16).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)


func _set_knob(value: float) -> void:
	_knob = value
	queue_redraw()


## Sets the state without the knob animating (when a screen opens).
func set_state(on: bool) -> void:
	set_pressed_no_signal(on)
	_knob = 1.0 if on else 0.0
	queue_redraw()


func _draw() -> void:
	var track: Rect2 = Rect2((size.x - WIDTH) / 2.0, (size.y - HEIGHT) / 2.0, WIDTH, HEIGHT)
	var body: StyleBoxFlat = StyleBoxFlat.new()
	body.bg_color = Color("2e3d63").lerp(Color("2fbf5a"), _knob)
	body.set_corner_radius_all(UiSkin.round_radius(HEIGHT))
	body.set_border_width_all(3)
	body.border_color = Color("0a1230").lerp(Color("0c5a24"), _knob)
	body.shadow_color = Color(0.0, 0.0, 0.0, 0.35)
	body.shadow_size = 6
	body.shadow_offset = Vector2(0.0, 3.0)
	body.anti_aliasing = true
	draw_style_box(body, track)
	var radius: float = HEIGHT / 2.0 - 8.0
	var x: float = lerpf(track.position.x + HEIGHT / 2.0, track.end.x - HEIGHT / 2.0, _knob)
	var centre: Vector2 = Vector2(x, track.get_center().y)
	draw_circle(centre + Vector2(0.0, 3.0), radius, Color(0.0, 0.0, 0.0, 0.3))
	draw_circle(centre, radius, Color("e9eefc"))
	draw_circle(centre + Vector2(0.0, -radius * 0.18), radius * 0.74, Color.WHITE)
