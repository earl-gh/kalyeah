class_name MatchBriefing
extends Control
## What a Practice match opens with (owner, 2026-11-03): the match behind it dimmed and blurred, and over it only text, centred: the
## game's name, one line on how to score, then what to do to move and to act, a ten second countdown in a circle and "Tap to continue".
## The match stands still while it shows; the countdown ends it by itself and a tap ends it sooner. It replaces the first-run walkthrough:
## it opens before every Practice match against bots, in every game. Online matches never open it (the server does not wait).

signal finished

const BLUR: Shader = preload("res://shaders/briefing_blur.gdshader")
const GOLD: Color = Color("ffc233")
const LABEL_BLUE: Color = Color("8cc4ff")
const WIDTH: float = 640.0
const TITLE_SIZE: int = UiMetrics.DISPLAY
const GOAL_SIZE: int = UiMetrics.TITLE
const LABEL_SIZE: int = UiMetrics.CAPTION
const LINE_SIZE: int = UiMetrics.TITLE
const CLOCK_SIZE: float = 150.0
const CLOCK_RING: float = 10.0

var running: bool = false
var seconds_left: float = 0.0

var title_label: Label
var goal_label: Label
var rows: VBoxContainer
var continue_label: Label

var _hud: MatchHud
var _total: float = 10.0
var _clock: Control
var _pulse: Tween


## The words for a game (null = Agawan Base): its name, how to score in one line, and (label, line) for each thing the player does.
## Text only; the controls of the street games are the knob and the action button, Agawan Base's are taps and drags.
static func words_for(game: StreetModeRules) -> Dictionary:
	if game == null:
		return {
			"title": "Agawan Base",
			"goal": "Earn a point by capturing the enemy base: stay inside it, or tag all the enemies.",
			"rows": [["Move", "Tap to move, or use gyroscope"], ["Attack", "Tap to cast skills, drag to aim"]],
		}
	if game.id == &"tumbang_preso":
		return {
			"title": game.title,
			"goal": "Knock the can over, fetch your slipper and run to the other end to score. Don't get tagged by the taya!",
			"rows": [["Move", "Push the knob to run"], ["Throw", "Hold to aim, drag to turn, release"]],
		}
	return {
		"title": game.title,
		"goal": "Runners: get past every guard and back to base to score. Guards: tag all three runners.",
		"rows": [["Move", "Push the knob to run"], ["Dive", "Tap to dash past a guard"]],
	}


func _init(hud: MatchHud = null) -> void:
	_hud = hud
	name = "MatchBriefing"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP
	visible = false
	var backdrop: ColorRect = ColorRect.new()
	backdrop.name = "Backdrop"
	backdrop.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	backdrop.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var material: ShaderMaterial = ShaderMaterial.new()
	material.shader = BLUR
	backdrop.material = material
	add_child(backdrop)
	var center: CenterContainer = CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	center.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(center)
	var column: VBoxContainer = VBoxContainer.new()
	column.name = "Column"
	column.custom_minimum_size = Vector2(WIDTH, 0.0)
	column.add_theme_constant_override("separation", 44)
	column.mouse_filter = Control.MOUSE_FILTER_IGNORE
	center.add_child(column)
	title_label = Label.new()
	title_label.name = "Title"
	title_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	UiKit.letter(title_label, TITLE_SIZE, GOLD)
	column.add_child(title_label)
	goal_label = UiKit.text("", GOAL_SIZE, KalyeahTheme.TEXT)
	goal_label.name = "Goal"
	goal_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(goal_label)
	rows = VBoxContainer.new()
	rows.name = "Rows"
	rows.add_theme_constant_override("separation", 30)
	rows.mouse_filter = Control.MOUSE_FILTER_IGNORE
	column.add_child(rows)
	_clock = Control.new()
	_clock.name = "Countdown"
	_clock.custom_minimum_size = Vector2(CLOCK_SIZE, CLOCK_SIZE)
	_clock.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	_clock.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_clock.draw.connect(_draw_clock)
	column.add_child(_clock)
	continue_label = Label.new()
	continue_label.name = "Continue"
	continue_label.text = "Tap to continue"
	continue_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	continue_label.add_theme_font_override("font", KalyeahTheme.BUTTON_FONT)
	continue_label.add_theme_font_size_override("font_size", UiMetrics.ACTION)
	continue_label.add_theme_color_override("font_color", KalyeahTheme.TEXT)
	column.add_child(continue_label)


## Opens the briefing for `game` (null = Agawan Base) and stills the match's controls until it ends.
func start(game: StreetModeRules, seconds: float) -> void:
	var words: Dictionary = words_for(game)
	title_label.text = words["title"] as String
	goal_label.text = words["goal"] as String
	for child: Node in rows.get_children():
		rows.remove_child(child)
		child.queue_free()
	for line: Array in words["rows"] as Array:
		rows.add_child(_row(line[0] as String, line[1] as String))
	_total = maxf(seconds, 0.1)
	seconds_left = _total
	running = true
	visible = true
	if _hud != null:
		_hud.set_controls_active(false)
	_clock.queue_redraw()
	_start_pulse()


## A label in small capitals over the line it explains, both centred.
func _row(label_text: String, line_text: String) -> VBoxContainer:
	var row: VBoxContainer = VBoxContainer.new()
	row.add_theme_constant_override("separation", 4)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var label: Label = Label.new()
	label.text = label_text.to_upper()
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.add_theme_font_override("font", KalyeahTheme.BUTTON_FONT)
	label.add_theme_font_size_override("font_size", LABEL_SIZE)
	label.add_theme_color_override("font_color", LABEL_BLUE)
	row.add_child(label)
	var line: Label = UiKit.text(line_text, LINE_SIZE, KalyeahTheme.TEXT)
	line.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	row.add_child(line)
	return row


func _process(delta: float) -> void:
	if not running:
		return
	seconds_left = maxf(seconds_left - delta, 0.0)
	_clock.queue_redraw()
	if seconds_left <= 0.0:
		finish()


## Any touch starts the match.
func _gui_input(event: InputEvent) -> void:
	if not running:
		return
	if (event is InputEventScreenTouch and (event as InputEventScreenTouch).pressed) or (event is InputEventMouseButton and (event as InputEventMouseButton).pressed):
		accept_event()
		finish()


func finish() -> void:
	if not running:
		return
	running = false
	visible = false
	if is_instance_valid(_pulse):
		_pulse.kill()
	if _hud != null:
		_hud.set_controls_active(true)
	finished.emit()


## The countdown's number: whole seconds, counting down to 1.
func shown_seconds() -> int:
	return ceili(seconds_left)


func _draw_clock() -> void:
	var middle: Vector2 = _clock.size / 2.0
	var radius: float = CLOCK_SIZE / 2.0 - CLOCK_RING
	_clock.draw_circle(middle, radius + CLOCK_RING / 2.0, Color(0.03, 0.06, 0.14, 0.55))
	_clock.draw_arc(middle, radius, 0.0, TAU, 64, Color(1.0, 1.0, 1.0, 0.22), CLOCK_RING, true)
	var left: float = clampf(seconds_left / _total, 0.0, 1.0)
	if left > 0.0:
		_clock.draw_arc(middle, radius, -PI / 2.0, -PI / 2.0 + TAU * left, 64, GOLD, CLOCK_RING, true)
	var font: Font = KalyeahTheme.DISPLAY_FONT
	var text: String = str(shown_seconds())
	var size: int = UiMetrics.DISPLAY
	var extent: Vector2 = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, size)
	_clock.draw_string_outline(font, middle + Vector2(-extent.x / 2.0, extent.y * 0.3), text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, size, 8, UiSkin.LETTER_INK)
	_clock.draw_string(font, middle + Vector2(-extent.x / 2.0, extent.y * 0.3), text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, size, Color.WHITE)


func _start_pulse() -> void:
	if is_instance_valid(_pulse):
		_pulse.kill()
	continue_label.modulate.a = 1.0
	if Settings.reduced_motion or not is_inside_tree():
		return
	_pulse = create_tween().set_loops()
	_pulse.tween_property(continue_label, "modulate:a", 0.45, 0.8).set_trans(Tween.TRANS_SINE)
	_pulse.tween_property(continue_label, "modulate:a", 1.0, 0.8).set_trans(Tween.TRANS_SINE)


## The phone's back button starts the match too (nothing to cancel: the briefing only says how to play).
func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_GO_BACK_REQUEST:
		finish.call_deferred()
