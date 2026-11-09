class_name Crosshair
extends Control
## The crosshair of the shoulder view (docs/GDD.md "Shoulder view"): a small ring with a dot in the middle of the screen, where the cards are
## cast. Drawn in code (a stand-in: owner art for it would be a 2D image). It turns gold while a card is held. View only.

const RADIUS: float = 14.0
const DOT: float = 3.0
const WIDTH: float = 3.0
const COLOR: Color = Color(1.0, 1.0, 1.0, 0.9)
const HELD: Color = Color(1.0, 0.82, 0.3, 1.0)
const SHADOW: Color = Color(0.0, 0.0, 0.0, 0.55)

## A card is held: the crosshair shows where it will go.
var held: bool = false:
	set(value):
		if value != held:
			held = value
			queue_redraw()


func _ready() -> void:
	name = "Crosshair"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	resized.connect(queue_redraw)


func _draw() -> void:
	var middle: Vector2 = size / 2.0
	var color: Color = HELD if held else COLOR
	draw_arc(middle, RADIUS, 0.0, TAU, 32, SHADOW, WIDTH + 2.0, true)
	draw_arc(middle, RADIUS, 0.0, TAU, 32, color, WIDTH, true)
	draw_circle(middle, DOT + 1.0, SHADOW)
	draw_circle(middle, DOT, color)
