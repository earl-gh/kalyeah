class_name MatchBriefing
extends Control
## What a Practice match opens with (owner, 2026-11-03): the match behind it dimmed, and over it only text: the game's name with a ten second
## countdown (the number alone) at the right of the same row, a thin white line that empties with it, how to score, then what to do to move and to act. The block is centred on the screen and its
## text is left-aligned. The match stands still while it shows and starts when the countdown ends: there is nothing to tap, no skip (the
## overlay only keeps touches off the match). It replaces the first-run walkthrough: it opens before every Practice match against bots,
## in every game. Online matches never open it (the server does not wait).

signal finished

## The match behind it is only dimmed (no blur), and the words are drawn at this share of their size.
const DIM: Color = Color(0.01, 0.03, 0.08, 0.7)
const CONTENT_SCALE: float = 0.8
const GOLD: Color = Color("ffc233")
const WIDTH: float = 640.0
const TITLE_SIZE: int = UiMetrics.DISPLAY
const GOAL_SIZE: int = UiMetrics.TITLE
## The small headings (Move, Attack, Runners...): between the title and the plain text.
const LABEL_SIZE: int = UiMetrics.HEADLINE
const LINE_SIZE: int = UiMetrics.TITLE
## The title and its description stand close together.
const HEAD_GAP: int = 10
## More room under the hairline than over it, so the details do not crowd it.
const BAR_GAP: int = 22

var running: bool = false
var seconds_left: float = 0.0

var title_label: Label
var goal: VBoxContainer
var rows: VBoxContainer
var head: VBoxContainer
var title_row: HBoxContainer

var _hud: MatchHud
var _total: float = 10.0
var countdown: Label
var bar: CountdownBar


## The words for a game (null = Agawang Base): its name, how to score as (heading, line) blocks (no heading: one plain line), and
## (heading, line) for each thing the player does. Text only; the controls of the street games are the knob and the action button,
## Agawang Base's are taps and drags.
static func words_for(game: StreetModeRules) -> Dictionary:
	if game == null:
		return {
			"title": "Agawang Base",
			"goal": [["", "Capture the enemy base or tag all enemies to score. Don't forget to rescue your teammates."]],
			"rows": [["Move", "Tap to move, or use gyroscope"], ["Attack", "Tap to cast skills, drag to aim"]],
		}
	if game.id == &"tumbang_preso":
		return {
			"title": game.title,
			"goal": [["", "Knock the can over, fetch your slipper and run to the other end to score. Don't get caught."]],
			"rows": [["Move", "Push the knob to run"], ["Throw", "Hold to aim, drag to turn, release"]],
		}
	return {
		"title": game.title,
		"goal": [["Runners", "Get past every guard and back to base to score. Don't get caught."], ["Guards", "Tag all three runners to score."]],
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
	backdrop.color = DIM
	add_child(backdrop)
	var column: VBoxContainer = VBoxContainer.new()
	column.name = "Column"
	column.custom_minimum_size = Vector2(WIDTH, 0.0)
	column.add_theme_constant_override("separation", 44)
	column.mouse_filter = Control.MOUSE_FILTER_IGNORE
	# centred by anchors, not by a container (a container resets its child's scale), and drawn smaller about its middle: the layout, and so
	# the type scale the lint holds, stay as they are
	add_child(column)
	column.set_anchors_and_offsets_preset(Control.PRESET_CENTER, Control.PRESET_MODE_MINSIZE)
	column.grow_horizontal = Control.GROW_DIRECTION_BOTH
	column.grow_vertical = Control.GROW_DIRECTION_BOTH
	column.scale = Vector2.ONE * CONTENT_SCALE
	column.resized.connect(func() -> void: column.pivot_offset = column.size / 2.0)
	# the countdown and the game's name share a row; what to do to score stands close under it
	head = VBoxContainer.new()
	head.name = "Head"
	head.add_theme_constant_override("separation", HEAD_GAP)
	head.mouse_filter = Control.MOUSE_FILTER_IGNORE
	column.add_child(head)
	title_row = HBoxContainer.new()
	title_row.name = "TitleRow"
	title_row.add_theme_constant_override("separation", UiMetrics.GAP)
	title_row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	head.add_child(title_row)
	title_label = Label.new()
	title_label.name = "Title"
	title_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	title_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	title_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	title_label.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	UiKit.letter(title_label, TITLE_SIZE, GOLD)
	title_row.add_child(title_label)
	countdown = Label.new()
	countdown.name = "Countdown"
	countdown.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	countdown.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	UiKit.letter(countdown, UiMetrics.DISPLAY, Color.WHITE)
	title_row.add_child(countdown)
	# the thin line between the title row and the details
	bar = CountdownBar.new()
	head.add_child(bar)
	var under_bar: Control = Control.new()
	under_bar.name = "UnderBar"
	under_bar.custom_minimum_size = Vector2(0.0, BAR_GAP - HEAD_GAP)
	under_bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	head.add_child(under_bar)
	goal = VBoxContainer.new()
	goal.name = "Goal"
	goal.add_theme_constant_override("separation", 14)
	goal.mouse_filter = Control.MOUSE_FILTER_IGNORE
	head.add_child(goal)
	rows = VBoxContainer.new()
	rows.name = "Rows"
	rows.add_theme_constant_override("separation", 30)
	rows.mouse_filter = Control.MOUSE_FILTER_IGNORE
	column.add_child(rows)


## Opens the briefing for `game` (null = Agawang Base) and stills the match's controls until it ends.
func start(game: StreetModeRules, seconds: float) -> void:
	var words: Dictionary = words_for(game)
	title_label.text = words["title"] as String
	for child: Node in goal.get_children() + rows.get_children():
		child.get_parent().remove_child(child)
		child.queue_free()
	for block: Array in words["goal"] as Array:
		goal.add_child(_row(block[0] as String, block[1] as String))
	for line: Array in words["rows"] as Array:
		rows.add_child(_row(line[0] as String, line[1] as String))
	_total = maxf(seconds, 0.1)
	seconds_left = _total
	running = true
	visible = true
	if _hud != null:
		_hud.set_controls_active(false)
	_show_left()


## A label in small capitals over the line it explains, both left-aligned.
func _row(label_text: String, line_text: String) -> VBoxContainer:
	var row: VBoxContainer = VBoxContainer.new()
	row.add_theme_constant_override("separation", 4)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var label: Label = Label.new()
	label.visible = not label_text.is_empty()
	label.text = label_text.to_upper()
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	label.add_theme_font_override("font", KalyeahTheme.BUTTON_FONT)
	label.add_theme_font_size_override("font_size", LABEL_SIZE)
	label.add_theme_color_override("font_color", GOLD)
	row.add_child(label)
	var line: Label = UiKit.text(line_text, GOAL_SIZE if label_text.is_empty() else LINE_SIZE, KalyeahTheme.TEXT)
	line.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	row.add_child(line)
	return row


func _process(delta: float) -> void:
	if not running:
		return
	seconds_left = maxf(seconds_left - delta, 0.0)
	_show_left()
	if seconds_left <= 0.0:
		finish()


func finish() -> void:
	if not running:
		return
	running = false
	visible = false
	if _hud != null:
		_hud.set_controls_active(true)
	finished.emit()


## The countdown's number: whole seconds, counting down to 1.
func shown_seconds() -> int:
	return ceili(seconds_left)


func _show_left() -> void:
	countdown.text = str(shown_seconds())
	bar.set_left(seconds_left, _total)
