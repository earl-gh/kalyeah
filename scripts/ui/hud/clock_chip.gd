class_name ClockChip
extends Control
## The match clock in the top right corner, laid out like Clash Royale's "Time left:" box: "Time left:" over the time (m:ss, counting down;
## with the stages off in the rules it counts up), in a see-through dark box with a black outline. The caption is the round's stage:
## "Time left:" (Regular), "Extended", then a gold "Overtime"; each stage restarts the time. While the stage word flies in from the
## middle of the screen (`ClockStageFlash`) the caption is left empty for it to land on. It only draws; it never takes a touch.

const TIME_FONT: int = UiMetrics.HEADLINE
const CAPTION_FONT: int = UiMetrics.CAPTION
## Semi-transparent (the street shows through) with a black outline on its two open sides (owner: copy Clash Royale's box).
const BOX_COLOR: Color = Color(0.04, 0.05, 0.08, 0.5)
const BOX_OUTLINE: int = 3
## The room the words keep from the box's top, its right side and its bottom (the box is flush with the screen's corner, so more at the
## top and the right than at the open left side).
const INNER_TOP: float = 16.0
const INNER_RIGHT: float = 20.0
const INNER_LEFT: float = 8.0
const INNER_BOTTOM: float = 8.0
const TEXT_OUTLINE: int = 5
const LINE_GAP: float = 4.0

var seconds: int = 0
var stage: MatchSim.ClockStage = MatchSim.ClockStage.REGULAR
## True while the stage word is flying in: the caption is not drawn yet.
var caption_hidden: bool = false
## Whether `seconds` is what is left (it rounds up, so 0:00 shows only once the time is really out) or what has passed (it rounds down).
var counting_down: bool = false


func _init() -> void:
	name = "ClockChip"
	mouse_filter = Control.MOUSE_FILTER_IGNORE


## The seconds to show (what is left when `down`, else what has passed) and the round's clock stage. Redraws only when the shown
## text changes.
func show_time(value: float, in_stage: MatchSim.ClockStage, down: bool = false) -> void:
	var now: int = ceili(value - 0.0001) if down else floori(value)
	if now == seconds and in_stage == stage and down == counting_down:
		return
	seconds = now
	stage = in_stage
	counting_down = down
	accessibility_name = "%s %s" % [caption_text(), text()]
	queue_redraw()


## Hides (or shows) the caption while the stage word flies in.
func hide_caption(hidden: bool) -> void:
	if hidden != caption_hidden:
		caption_hidden = hidden
		queue_redraw()


## The small word over the time.
func caption_text() -> String:
	match stage:
		MatchSim.ClockStage.EXTENDED:
			return UiText.t("Extended")
		MatchSim.ClockStage.OVERTIME:
			return UiText.t("Overtime")
	return UiText.t("Time left:")


## Where the caption's middle is, in the chip's own coordinates (where the stage word lands).
func caption_center() -> Vector2:
	return text_rects()[0].get_center()


## The caption's colour: gold in Overtime, white otherwise.
func caption_color() -> Color:
	return RoyaleStyle.GOLD if stage == MatchSim.ClockStage.OVERTIME else Color.WHITE


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
	var lines: Array[Rect2] = text_rects()
	var caption_at: Vector2 = lines[0].position + Vector2(TEXT_OUTLINE, TEXT_OUTLINE + font.get_ascent(CAPTION_FONT))
	if not caption_hidden:
		_lettered(font, caption, caption_at, CAPTION_FONT, caption_color())
	var time_at: Vector2 = lines[1].position + Vector2(TEXT_OUTLINE, TEXT_OUTLINE + font.get_ascent(TIME_FONT))
	_lettered(font, text(), time_at, TIME_FONT, Color.WHITE)


## Full line bounds include the text stroke, so the caption and time keep real space between them.
func text_rects() -> Array[Rect2]:
	var font: Font = KalyeahTheme.ACTION_FONT
	var caption_size: Vector2 = font.get_string_size(caption_text(), HORIZONTAL_ALIGNMENT_LEFT, -1.0, CAPTION_FONT) + Vector2.ONE * TEXT_OUTLINE * 2.0
	var time_size: Vector2 = font.get_string_size(text(), HORIZONTAL_ALIGNMENT_LEFT, -1.0, TIME_FONT) + Vector2.ONE * TEXT_OUTLINE * 2.0
	# the words are centred in what is left once the inner room is taken off (more at the top and the right)
	var room: Rect2 = Rect2(INNER_LEFT, INNER_TOP, size.x - INNER_LEFT - INNER_RIGHT, size.y - INNER_TOP - INNER_BOTTOM)
	var top: float = room.position.y + (room.size.y - caption_size.y - time_size.y - LINE_GAP) / 2.0
	var middle: float = room.get_center().x
	return [Rect2(Vector2(middle - caption_size.x / 2.0, top), caption_size), Rect2(Vector2(middle - time_size.x / 2.0, top + caption_size.y + LINE_GAP), time_size)]


## White (or `color`) letters with a black outline.
func _lettered(font: Font, text_line: String, origin: Vector2, font_size: int, color: Color) -> void:
	draw_string_outline(font, origin, text_line, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, TEXT_OUTLINE, Color.BLACK)
	draw_string(font, origin, text_line, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, color)
