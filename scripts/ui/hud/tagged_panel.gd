class_name TaggedPanel
extends Control
## What you see while tagged and waiting in the box: the tray at the bottom keeps its place and size (the same height as the hand's
## tray, so the screen does not jump) but the hand gives way to the way out: a title, your teammates, one line on how it ends and
## (the old two-weapon pick only) a Change weapons button. No other player can be picked or watched (owner: the whole street is on screen and the camera never moves).
## Built in code; MatchHud feeds it with `sync()` and the console's height with `set_console_height()`.

signal swap_pressed

const INK: Color = RoyaleStyle.NAVY
const TITLE_COLOR: Color = RoyaleStyle.GOLD
const HINT_COLOR: Color = RoyaleStyle.CREAM
const WAIT_TEXT: String = "Tagged · Wait for a rescue"
## `%d`: the seconds a teammate has to stay by your box (the rules say).
const HINT_TEXT: String = "A teammate frees you after %d s at your box."
## The countdown to walking out alone, when the match has one (Practice): a ring beside the faces.
const RELEASE_RING_SIZE: float = 64.0
## The same side margin the controls keep.
const SIDE_MARGIN: float = 34.0
## Change weapons (the old two-weapon pick) sits in the teammates' row, so it is this wide.
const SWAP_WIDTH: float = 360.0

## How to get out (kept as a label for screen readers and tests).
var wait_label: Label
## One line on how it ends (the box, the teammate), under the title.
var hint_label: Label
var swap_button: Button
## Your teammates: who is free and who is on the way.
var pips: TeammatePips
## Seconds until you walk out alone (shown only when the match sets a release time).
var release_ring: CountdownRing
## Seconds until the teammate who is rescuing frees you (shown while one stays by the box and you are next in line).
var rescue_ring: CountdownRing
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
	wait_label = _label(WAIT_TEXT, UiMetrics.TITLE, TITLE_COLOR)
	wait_label.theme_type_variation = &"DisplayLabel"
	column.add_child(wait_label)
	var row: HBoxContainer = HBoxContainer.new()
	row.name = "Teammates"
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.add_theme_constant_override("separation", UiMetrics.GAP)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	column.add_child(row)
	pips = TeammatePips.new()
	row.add_child(pips)
	release_ring = CountdownRing.new()
	release_ring.name = "ReleaseRing"
	release_ring.custom_minimum_size = Vector2.ONE * RELEASE_RING_SIZE
	release_ring.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	release_ring.mouse_filter = Control.MOUSE_FILTER_IGNORE
	release_ring.accessibility_name = "Out alone in"
	release_ring.visible = false
	row.add_child(release_ring)
	rescue_ring = CountdownRing.new()
	rescue_ring.name = "RescueRing"
	rescue_ring.custom_minimum_size = Vector2.ONE * RELEASE_RING_SIZE
	rescue_ring.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	rescue_ring.mouse_filter = Control.MOUSE_FILTER_IGNORE
	rescue_ring.accessibility_name = "Freed in"
	rescue_ring.visible = false
	row.add_child(rescue_ring)
	hint_label = _label(UiText.f(HINT_TEXT, [5]), UiMetrics.CAPTION, HINT_COLOR)
	hint_label.name = "Hint"
	hint_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	column.add_child(hint_label)
	# beside the teammates (the old two-weapon pick only): the tray is only as tall as the hand of cards, so the box's
	# title, the teammates with the button and the hint make three rows
	swap_button = Button.new()
	swap_button.text = "Change weapons"
	swap_button.custom_minimum_size = Vector2(SWAP_WIDTH, UiMetrics.TOUCH)
	swap_button.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	swap_button.add_theme_font_size_override("font_size", UiMetrics.ACTION)
	KalyeahTheme.style_button(swap_button, "orange")
	swap_button.pressed.connect(swap_pressed.emit)
	row.add_child(swap_button)
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
	if not visible:
		return
	pips.sync(_sim, player)
	hint_label.text = UiText.f(HINT_TEXT, [ceili(_sim.rules.rescue_hold_time)])
	var hold: float = _sim.rules.rescue_hold_time
	rescue_ring.visible = hold > 0.0 and _sim.rescue_rescuer[player.team] >= 0 and _is_next_in_line(_sim, player)
	if rescue_ring.visible:
		rescue_ring.show_time(hold - _sim.rescue_elapsed[player.team], hold)
	release_ring.visible = _sim.tag_release_time > 0.0
	if release_ring.visible:
		release_ring.show_time(_sim.tag_release_time - player.tag_time, _sim.tag_release_time)


## You were tagged first of the teammates waiting: the rescuer is working on you.
func _is_next_in_line(sim: MatchSim, player: PlayerState) -> bool:
	for id: int in sim.players:
		var other: PlayerState = sim.players[id]
		if other.team == player.team and other.tagged and other.tag_order < player.tag_order:
			return false
	return true


func _label(text: String, font_size: int, color: Color) -> Label:
	var label: Label = Label.new()
	label.text = text
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", color)
	label.add_theme_color_override("font_outline_color", INK)
	label.add_theme_constant_override("outline_size", 6)
	return label
