class_name ClockChip
extends Control
## The match clock at the right of the header, the counterpart of the settings button at the left: a navy panel with "TIME" over the time
## (m:ss, counting up: a match has no time limit). In overtime the caption turns gold and says OVERTIME. It only draws; it never takes a touch.

const TIME_FONT: int = 40
const CAPTION_FONT: int = UiMetrics.CAPTION

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


## The clock as m:ss.
func text() -> String:
	return "%d:%02d" % [seconds / 60, seconds % 60]


func _draw() -> void:
	RoyaleStyle.chunky(self, Rect2(Vector2.ZERO, size), Color(0.05, 0.14, 0.36), 18, false)
	if overtime:
		draw_rect(Rect2(Vector2(10.0, size.y - 14.0), Vector2(size.x - 20.0, 4.0)), RoyaleStyle.GOLD)
	var font: Font = KalyeahTheme.ACTION_FONT
	var caption: String = UiText.t("OVERTIME") if overtime else UiText.t("TIME")
	var caption_color: Color = RoyaleStyle.GOLD if overtime else RoyaleStyle.BLUE_LIGHT
	var caption_size: Vector2 = font.get_string_size(caption, HORIZONTAL_ALIGNMENT_LEFT, -1.0, CAPTION_FONT)
	draw_string(font, Vector2((size.x - caption_size.x) / 2.0, 26.0), caption, HORIZONTAL_ALIGNMENT_LEFT, -1.0, CAPTION_FONT, caption_color)
	var line: String = text()
	var line_size: Vector2 = font.get_string_size(line, HORIZONTAL_ALIGNMENT_LEFT, -1.0, TIME_FONT)
	RoyaleStyle.lettered(self, font, line, Vector2((size.x - line_size.x) / 2.0, 70.0), TIME_FONT)
