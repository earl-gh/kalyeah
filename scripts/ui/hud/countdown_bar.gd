class_name CountdownBar
extends Control
## The briefing's thin white line between its title row and the details: it empties from right to left as the countdown runs down.

const HEIGHT: float = 6.0

var total: float = 10.0
var left: float = 10.0


func _init() -> void:
	name = "CountdownBar"
	custom_minimum_size = Vector2(0.0, HEIGHT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func set_left(value: float, p_total: float) -> void:
	total = maxf(p_total, 0.1)
	left = clampf(value, 0.0, total)
	queue_redraw()


## The share still to run, 1 down to 0.
func share() -> float:
	return clampf(left / total, 0.0, 1.0)


func _draw() -> void:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.set_corner_radius_all(int(HEIGHT / 2.0))
	box.bg_color = Color(1.0, 1.0, 1.0, 0.25)
	draw_style_box(box, Rect2(Vector2.ZERO, size))
	if share() > 0.0:
		box.bg_color = Color.WHITE
		draw_style_box(box, Rect2(Vector2.ZERO, Vector2(maxf(size.x * share(), HEIGHT), size.y)))
