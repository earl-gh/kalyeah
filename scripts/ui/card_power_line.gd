class_name CardPowerLine
extends Control
## "POWER 9999" and the like up the bottom of a mode card's right edge, turned -90 degrees so it reads upwards, in white letters
## with a black outline (the constants are on `CardFace`).

var text: String = ""


func _init() -> void:
	name = "PowerLine"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		queue_redraw()


func _draw() -> void:
	if text.is_empty():
		return
	var font: Font = KalyeahTheme.BUTTON_FONT
	var font_size: int = int(roundf(size.y * CardFace.POWER_SIZE_SHARE))
	var ascent: float = font.get_ascent(font_size)
	# the origin is the text's bottom left once it is turned: it runs up the card from there
	var origin: Vector2 = Vector2(size.x - size.x * CardFace.POWER_EDGE_SHARE - (font.get_height(font_size) - ascent), size.y - size.y * CardFace.POWER_BOTTOM_SHARE)
	draw_set_transform(origin, -PI / 2.0, Vector2.ONE)
	draw_string_outline(font, Vector2.ZERO, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, CardFace.POWER_OUTLINE_SIZE, CardFace.POWER_OUTLINE)
	draw_string(font, Vector2.ZERO, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, CardFace.POWER_COLOR)
	draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)
