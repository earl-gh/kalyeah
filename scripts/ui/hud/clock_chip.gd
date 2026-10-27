class_name ClockChip
extends Control
## The match clock in the top right corner, laid out like Clash Royale's "Time left:" box: "Time left:" over the time (m:ss, counting up: a
## match has no time limit), in a see-through dark box with a black outline. In overtime the caption turns gold and says OVERTIME. It only
## draws; it never takes a touch.

const TIME_FONT: int = UiMetrics.HEADLINE
const CAPTION_FONT: int = UiMetrics.CAPTION
## Semi-transparent (the street shows through) with a black outline on its two open sides (owner: copy Clash Royale's box).
const BOX_COLOR: Color = Color(0.04, 0.05, 0.08, 0.5)
const BOX_OUTLINE: int = 3

var seconds: int = 0
var overtime: bool = false


func _init() -> void:
	name = "ClockChip"
	mouse_filter = Control.MOUSE_FILTER_IGNORE


## Whole seconds of play and whether the round is in overtime. Redraws only when the shown text changes.
func show_time(match_time: float, in_overtime: bool) -> void:
	var now: int = floori(match_time)
	if now == seconds and in_overtime == overtime:
		return
	seconds = now
	overtime = in_overtime
	accessibility_name = "Time %s%s" % [text(), ", overtime" if overtime else ""]
	queue_redraw()


## The small word over the time.
func caption_text() -> String:
	return UiText.t("OVERTIME") if overtime else UiText.t("Time left:")


## The clock as m:ss.
func text() -> String:
	return "%d:%02d" % [seconds / 60, seconds % 60]


func _draw() -> void:
	# like Clash Royale's "Time left" box: a dark see-through box in the screen's top right corner (flush with the top and the side, only
	# its inner corner rounded), a small caption over a big clock, both white with a black outline
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = BOX_COLOR
	box.border_color = Color.BLACK
	box.border_width_left = BOX_OUTLINE
	box.border_width_bottom = BOX_OUTLINE
	box.corner_radius_bottom_left = UiSkin.RADIUS_M
	box.anti_aliasing = true
	draw_style_box(box, Rect2(Vector2.ZERO, size))
	var font: Font = KalyeahTheme.ACTION_FONT
	var caption: String = caption_text()
	var caption_color: Color = RoyaleStyle.GOLD if overtime else Color.WHITE
	var caption_size: Vector2 = font.get_string_size(caption, HORIZONTAL_ALIGNMENT_LEFT, -1.0, CAPTION_FONT)
	_lettered(font, caption, Vector2((size.x - caption_size.x) / 2.0, 28.0), CAPTION_FONT, caption_color)
	var line: String = text()
	var line_size: Vector2 = font.get_string_size(line, HORIZONTAL_ALIGNMENT_LEFT, -1.0, TIME_FONT)
	_lettered(font, line, Vector2((size.x - line_size.x) / 2.0, size.y - 12.0), TIME_FONT, Color.WHITE)


## White (or `color`) letters with a black outline.
func _lettered(font: Font, text_line: String, origin: Vector2, font_size: int, color: Color) -> void:
	draw_string_outline(font, origin, text_line, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, 7, Color.BLACK)
	draw_string(font, origin, text_line, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, color)
