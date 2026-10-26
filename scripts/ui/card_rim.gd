class_name CardRim
extends Control
## The bright rim of the chosen mode card: a glow and a line in the card's accent colour, drawn on the card's outline
## (`CardShape`) so it hugs the card.

const GLOW_WIDTHS: Array[float] = [26.0, 18.0, 11.0]
const GLOW_ALPHAS: Array[float] = [0.08, 0.14, 0.24]
const LINE_WIDTH: float = 3.5
## The line runs this far inside the card's edge, so the card's paper edge stays visible outside it.
const LINE_INSET: float = 3.0

var accent: Color = Color.WHITE:
	set(value):
		accent = value
		queue_redraw()


func _init() -> void:
	name = "Frame"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		queue_redraw()


func _draw() -> void:
	if size.x < 8.0 or size.y < 8.0:
		return
	var radius: float = CardFace.RADIUS
	var outer: PackedVector2Array = CardShape.outline(size, radius)
	outer.append(outer[0])
	for i: int in GLOW_WIDTHS.size():
		draw_polyline(outer, Color(accent, GLOW_ALPHAS[i]), GLOW_WIDTHS[i], true)
	var line: PackedVector2Array = CardShape.outline(size, radius, LINE_INSET)
	line.append(line[0])
	draw_polyline(line, accent, LINE_WIDTH, true)
