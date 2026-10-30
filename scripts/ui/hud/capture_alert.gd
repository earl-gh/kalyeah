class_name CaptureAlert
extends Control
## "An enemy is capturing your base" (owner): a yellow warning triangle with a black exclamation mark in the middle of the scoreboard,
## and two lines under the scoreboard's row: a small "Enemy capturing base" over a large "DEFEND THE BASE!". It replaces the big words
## that used to stand in the middle of the screen. It only draws; it never takes a touch. The triangle pulses a little (it rests with
## Reduce motion).

const SMALL_TEXT: String = "Enemy capturing base"
const BIG_TEXT: String = "DEFEND THE BASE!"
## The triangle's box (its middle is the middle of the scoreboard) and how round its corners are.
const ICON_SIZE: Vector2 = Vector2(68.0, 60.0)
const CORNER: float = 6.0
const OUTLINE: float = 5.0
const YELLOW: Color = Color("ffd21f")
const BLACK: Color = Color("0b0b0f")
## The pulse: the triangle grows by this share and back, this many times a second.
const PULSE: float = 0.08
const PULSE_HZ: float = 1.6
const SMALL_FONT: int = UiMetrics.CAPTION
const BIG_FONT: int = UiMetrics.HEADLINE
const BIG_COLOR: Color = Color(1.0, 0.86, 0.25)
## The gaps under the scoreboard's row: its bottom to the small line, and the small line to the big one.
const SMALL_GAP: float = 8.0
const BIG_GAP: float = 30.0

var icon: Control
var small_label: Label
var big_label: Label


func _init() -> void:
	name = "CaptureAlert"
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	visible = false
	set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)
	offset_bottom = MatchHud.TOP_ROW_Y + MatchHud.SCORE_SIZE.y + SMALL_GAP + BIG_GAP + 60.0
	icon = Mark.new()
	icon.name = "Icon"
	icon.mouse_filter = Control.MOUSE_FILTER_IGNORE
	icon.size = ICON_SIZE
	icon.pivot_offset = ICON_SIZE / 2.0
	add_child(icon)
	small_label = _line("Small", SMALL_TEXT, SMALL_FONT, Color.WHITE)
	big_label = _line("Big", BIG_TEXT, BIG_FONT, BIG_COLOR)
	resized.connect(_place)
	_place()


func _line(label_name: String, text: String, font_size: int, color: Color) -> Label:
	var label: Label = Label.new()
	label.name = label_name
	label.text = UiText.t(text)
	label.theme_type_variation = &"DisplayLabel"
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	UiKit.letter(label, font_size, color, BLACK)
	add_child(label)
	return label


func _place() -> void:
	var row_middle: float = MatchHud.TOP_ROW_Y + MatchHud.SCORE_SIZE.y / 2.0
	var small_top: float = MatchHud.TOP_ROW_Y + MatchHud.SCORE_SIZE.y + SMALL_GAP
	icon.position = Vector2((size.x - ICON_SIZE.x) / 2.0, row_middle - ICON_SIZE.y / 2.0)
	small_label.position = Vector2(0.0, small_top)
	small_label.size = Vector2(size.x, 34.0)
	big_label.position = Vector2(0.0, small_top + BIG_GAP)
	big_label.size = Vector2(size.x, 56.0)


## Shows or hides the alert (the lines and the triangle come and go together).
func show_alert(on: bool) -> void:
	if visible == on:
		return
	visible = on
	set_process(on)
	icon.scale = Vector2.ONE
	if on:
		accessibility_name = "%s. %s" % [SMALL_TEXT, BIG_TEXT]


func _process(_delta: float) -> void:
	if not visible or Settings.reduced_motion:
		icon.scale = Vector2.ONE
		return
	icon.scale = Vector2.ONE * (1.0 + PULSE * 0.5 * (1.0 + sin(float(Time.get_ticks_msec()) / 1000.0 * TAU * PULSE_HZ)))


## The warning mark: a yellow triangle with rounded corners in a black outline, a black exclamation mark inside.
class Mark extends Control:
	func _draw() -> void:
		var w: float = size.x
		var h: float = size.y
		# the corners of the triangle, pulled in by the roundness so the thick strokes round them off
		var inset: float = CORNER + OUTLINE
		var apex: Vector2 = Vector2(w / 2.0, inset + 1.0)
		var left: Vector2 = Vector2(inset, h - inset)
		var right: Vector2 = Vector2(w - inset, h - inset)
		_rounded(PackedVector2Array([apex, right, left]), CORNER + OUTLINE, BLACK)
		_rounded(PackedVector2Array([apex, right, left]), CORNER, YELLOW)
		# the exclamation mark: a bar with rounded ends over a dot
		var middle: float = w / 2.0
		var top: float = h * 0.36
		var bottom: float = h * 0.64
		draw_line(Vector2(middle, top), Vector2(middle, bottom), BLACK, 7.0, true)
		draw_circle(Vector2(middle, top), 3.5, BLACK)
		draw_circle(Vector2(middle, bottom), 3.5, BLACK)
		draw_circle(Vector2(middle, h * 0.77), 4.2, BLACK)

	func _rounded(points: PackedVector2Array, radius: float, color: Color) -> void:
		draw_colored_polygon(points, color)
		for i: int in points.size():
			draw_line(points[i], points[(i + 1) % points.size()], color, radius * 2.0, true)
			draw_circle(points[i], radius, color)
