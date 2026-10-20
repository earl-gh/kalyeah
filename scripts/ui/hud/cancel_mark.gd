class_name CancelMark
extends Control
## The place to let go of an aimed weapon to cancel it: just a thin ring with a cross, no tile behind it. It
## lights up (a soft red fill, a bright cross) while the finger is on it. The line under the controls says what
## letting go does. Never takes a touch: the weapon button reads the raw touch and checks this control's rect.

const RADIUS: float = 34.0
const LINE_WIDTH: float = 4.0
const COLD: Color = Color(1.0, 1.0, 1.0, 0.6)
const HOT: Color = Color(1.0, 0.45, 0.4, 1.0)
const HOT_FILL: Color = Color(0.9, 0.15, 0.15, 0.4)
const SHADOW: Color = Color(0.05, 0.04, 0.1, 0.4)

var hot: bool = false:
	set(value):
		if value != hot:
			hot = value
			queue_redraw()


func _draw() -> void:
	var center: Vector2 = size / 2.0
	var color: Color = HOT if hot else COLD
	var reach: float = RADIUS * 0.4
	if hot:
		draw_circle(center, RADIUS, HOT_FILL)
	draw_arc(center, RADIUS, 0.0, TAU, 32, SHADOW, LINE_WIDTH + 3.0, true)
	draw_arc(center, RADIUS, 0.0, TAU, 32, color, LINE_WIDTH, true)
	for diagonal: Vector2 in [Vector2(1.0, 1.0), Vector2(1.0, -1.0)]:
		draw_line(center - diagonal * reach, center + diagonal * reach, SHADOW, LINE_WIDTH + 3.0, true)
		draw_line(center - diagonal * reach, center + diagonal * reach, color, LINE_WIDTH, true)
