class_name ClockChip
extends Control
## The match clock at the right of the header, the counterpart of the settings button at the left: a graphite chip with a lit edge and the
## time (m:ss) in it. In overtime a small amber OVERTIME sits over the time. It only draws; it never takes a touch.

const TIME_FONT: int = 42
const CAPTION_FONT: int = 17
const FACE: Color = Color(0.085, 0.09, 0.125)
const EDGE: Color = Color(0.62, 0.68, 0.85, 0.4)
const OVERTIME_COLOR: Color = Color(1.0, 0.78, 0.25)

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
	var chip: StyleBoxFlat = StyleBoxFlat.new()
	chip.bg_color = FACE
	chip.border_color = OVERTIME_COLOR if overtime else EDGE
	chip.set_border_width_all(2)
	chip.set_corner_radius_all(int(size.y / 2.0))
	chip.anti_aliasing = true
	chip.shadow_color = Color(0.0, 0.0, 0.0, 0.35)
	chip.shadow_size = 6
	chip.shadow_offset = Vector2(0.0, 3.0)
	draw_style_box(chip, Rect2(Vector2.ZERO, size))
	var font: Font = KalyeahTheme.ACTION_FONT
	var line: String = text()
	var line_size: Vector2 = font.get_string_size(line, HORIZONTAL_ALIGNMENT_LEFT, -1.0, TIME_FONT)
	var middle: float = size.y / 2.0 + (8.0 if overtime else 0.0)
	var origin: Vector2 = Vector2((size.x - line_size.x) / 2.0, middle + TIME_FONT * 0.35)
	draw_string_outline(font, origin, line, HORIZONTAL_ALIGNMENT_LEFT, -1.0, TIME_FONT, 6, Icons.OUTLINE)
	draw_string(font, origin, line, HORIZONTAL_ALIGNMENT_LEFT, -1.0, TIME_FONT, Color.WHITE)
	if overtime:
		var caption: String = UiText.t("OVERTIME")
		var caption_size: Vector2 = font.get_string_size(caption, HORIZONTAL_ALIGNMENT_LEFT, -1.0, CAPTION_FONT)
		draw_string(font, Vector2((size.x - caption_size.x) / 2.0, size.y * 0.3 + CAPTION_FONT * 0.35), caption, HORIZONTAL_ALIGNMENT_LEFT, -1.0, CAPTION_FONT, OVERTIME_COLOR)
