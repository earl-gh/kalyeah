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
const PADDING: Vector2 = Vector2(16.0, 12.0)
const TEXT_OUTLINE: int = 5
const LINE_GAP: float = 4.0

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
	var lines: Array[Rect2] = text_rects()
	var caption_at: Vector2 = lines[0].position + Vector2(TEXT_OUTLINE, TEXT_OUTLINE + font.get_ascent(CAPTION_FONT))
	_lettered(font, caption, caption_at, CAPTION_FONT, caption_color)
	var time_at: Vector2 = lines[1].position + Vector2(TEXT_OUTLINE, TEXT_OUTLINE + font.get_ascent(TIME_FONT))
	_lettered(font, text(), time_at, TIME_FONT, Color.WHITE)


## Full line bounds include the text stroke, so the caption and time keep real space between them.
func text_rects() -> Array[Rect2]:
	var font: Font = KalyeahTheme.ACTION_FONT
	var caption_size: Vector2 = font.get_string_size(caption_text(), HORIZONTAL_ALIGNMENT_LEFT, -1.0, CAPTION_FONT) + Vector2.ONE * TEXT_OUTLINE * 2.0
	var time_size: Vector2 = font.get_string_size(text(), HORIZONTAL_ALIGNMENT_LEFT, -1.0, TIME_FONT) + Vector2.ONE * TEXT_OUTLINE * 2.0
	var top: float = (size.y - caption_size.y - time_size.y - LINE_GAP) / 2.0
	return [Rect2(Vector2((size.x - caption_size.x) / 2.0, top), caption_size), Rect2(Vector2((size.x - time_size.x) / 2.0, top + caption_size.y + LINE_GAP), time_size)]


## White (or `color`) letters with a black outline.
func _lettered(font: Font, text_line: String, origin: Vector2, font_size: int, color: Color) -> void:
	draw_string_outline(font, origin, text_line, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, TEXT_OUTLINE, Color.BLACK)
	draw_string(font, origin, text_line, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, color)
