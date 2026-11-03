class_name CountdownCircle
extends Control
## A countdown in a circle: a thin white ring that empties and the whole seconds left in the middle (the match briefing's).

## Whole seconds are shown, counting down to 1.
var total: float = 10.0
var left: float = 10.0
var ring: float = 4.0
var font_size: int = UiMetrics.HEADLINE


func _init(p_size: float = 104.0, p_ring: float = 4.0, p_font_size: int = UiMetrics.HEADLINE) -> void:
	name = "Countdown"
	ring = p_ring
	font_size = p_font_size
	custom_minimum_size = Vector2.ONE * p_size
	size = custom_minimum_size
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func set_left(value: float, p_total: float) -> void:
	total = maxf(p_total, 0.1)
	left = clampf(value, 0.0, total)
	queue_redraw()


func shown_seconds() -> int:
	return ceili(left)


func _draw() -> void:
	var middle: Vector2 = size / 2.0
	var radius: float = minf(size.x, size.y) / 2.0 - ring
	draw_circle(middle, radius + ring / 2.0, Color(0.03, 0.06, 0.14, 0.6))
	draw_arc(middle, radius, 0.0, TAU, 64, Color(1.0, 1.0, 1.0, 0.25), ring, true)
	var share: float = clampf(left / total, 0.0, 1.0)
	if share > 0.0:
		draw_arc(middle, radius, -PI / 2.0, -PI / 2.0 + TAU * share, 64, Color.WHITE, ring, true)
	var font: Font = KalyeahTheme.DISPLAY_FONT
	var text: String = str(shown_seconds())
	var extent: Vector2 = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size)
	var origin: Vector2 = middle + Vector2(-extent.x / 2.0, extent.y * 0.3)
	draw_string_outline(font, origin, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, 6, UiSkin.LETTER_INK)
	draw_string(font, origin, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, Color.WHITE)
