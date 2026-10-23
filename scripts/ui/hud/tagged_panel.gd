class_name TaggedPanel
extends Control
## What you see while tagged and waiting in the box: the controller deck at the bottom keeps its place and size (the same height as
## the console, so the screen does not jump) but its controls give way to the way out: a title, one line on how it ends and a
## Change weapons button. No other player can be picked or watched (owner: the whole street is on screen and the camera never moves).
## Built in code; MatchHud feeds it with `sync()` and the console's height with `set_console_height()`.

signal swap_pressed

const INK: Color = RoyaleStyle.NAVY
const TITLE_COLOR: Color = RoyaleStyle.GOLD
const HINT_COLOR: Color = RoyaleStyle.CREAM
const WAIT_TEXT: String = "Tagged · Wait for a rescue"
const HINT_TEXT: String = "Teammates can run into your box to set you free."
## The same side margin the controls keep.
const SIDE_MARGIN: float = 34.0

## How to get out (kept as a label for screen readers and tests).
var wait_label: Label
## One line on how it ends (the box, the teammate), under the title.
var hint_label: Label
var swap_button: Button
## What the title, the hint and the button sit in.
var column: VBoxContainer


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	visible = false
	column = VBoxContainer.new()
	column.name = "Column"
	column.alignment = BoxContainer.ALIGNMENT_CENTER
	column.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	column.mouse_filter = Control.MOUSE_FILTER_IGNORE
	column.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	column.offset_left = SIDE_MARGIN
	column.offset_right = -SIDE_MARGIN
	column.offset_top = 22.0
	column.offset_bottom = -22.0
	add_child(column)
	wait_label = _label(WAIT_TEXT, 34, TITLE_COLOR)
	wait_label.theme_type_variation = &"DisplayLabel"
	column.add_child(wait_label)
	hint_label = _label(HINT_TEXT, UiMetrics.CAPTION, HINT_COLOR)
	hint_label.name = "Hint"
	hint_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	column.add_child(hint_label)
	swap_button = Button.new()
	swap_button.text = "Change weapons"
	swap_button.custom_minimum_size = Vector2(420.0, UiMetrics.TOUCH)
	swap_button.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	swap_button.add_theme_font_size_override("font_size", UiMetrics.ACTION)
	KalyeahTheme.style_button(swap_button, "orange")
	swap_button.pressed.connect(swap_pressed.emit)
	column.add_child(swap_button)
	set_console_height(MatchHud.CONSOLE_HEIGHT)


## The panel is exactly as tall as the console it stands on.
func set_console_height(height: float) -> void:
	offset_top = -height
	offset_bottom = 0.0


## Shown while `player` is tagged.
func sync(_sim: MatchSim, player: PlayerState, pick_open: bool) -> void:
	# once the match is over the results card says what happened; the box's words would show through its dimmed backdrop
	visible = player.tagged and _sim.phase != MatchSim.Phase.MATCH_OVER
	# cards mode: the weapons are cards that keep cycling, so there is nothing to change in the box
	swap_button.visible = visible and not pick_open and not _sim.rules.card_cycle


func _label(text: String, font_size: int, color: Color) -> Label:
	var label: Label = Label.new()
	label.text = text
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", color)
	label.add_theme_color_override("font_outline_color", INK)
	label.add_theme_constant_override("outline_size", 6)
	return label
