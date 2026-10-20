class_name MatchResults
extends Control
## The end-of-match card: VICTORY or DEFEAT, the final score, your kid and weapons, the match time and
## the way on (play again or home). It is built once; `show_result` fills it, a short beat after the
## winning moment so the street's own "VICTORY!" is seen first. It only reads the sim.

const DELAY: float = 1.0
const POP: float = 0.35
const WIN_COLOR: Color = Color("ffd25a")
const LOSS_COLOR: Color = Color("ff8a80")
const WIDTH: float = 600.0

signal home_pressed

var title: Label
var subtitle: Label
var mine_label: Label
var theirs_label: Label
var time_label: Label
var kid_holder: Control
var weapons_row: HBoxContainer
var primary: Button
var home_button: Button

var _card: PanelContainer
var _tween: Tween
var _shown: bool = false


func _init() -> void:
	name = "MatchResults"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	visible = false
	var dim: ColorRect = ColorRect.new()
	dim.color = Color(0.01, 0.025, 0.06, 0.62)
	dim.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	dim.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(dim)
	var center: CenterContainer = CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	center.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(center)
	_card = UiKit.card(null, "raised", 30.0)
	_card.custom_minimum_size = Vector2(WIDTH, 0.0)
	center.add_child(_card)
	var column: VBoxContainer = VBoxContainer.new()
	column.add_theme_constant_override("separation", UiMetrics.GAP)
	_card.add_child(column)
	title = Label.new()
	title.name = "Title"
	title.theme_type_variation = &"DisplayLabel"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.add_theme_font_size_override("font_size", 80)
	title.add_theme_color_override("font_outline_color", Color("1a0c02"))
	title.add_theme_constant_override("outline_size", 14)
	column.add_child(title)
	subtitle = UiKit.text("", UiMetrics.DETAIL, Color("c9d6f2"), false)
	subtitle.name = "Subtitle"
	subtitle.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(subtitle)
	column.add_child(_score_card())
	column.add_child(_you_row())
	var buttons: VBoxContainer = VBoxContainer.new()
	buttons.name = "Buttons"
	buttons.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	column.add_child(buttons)
	home_button = MenuDialog.make_button("Home", "grey", UiMetrics.ACTION)
	home_button.name = "HomeButton"
	home_button.pressed.connect(home_pressed.emit)
	buttons.add_child(home_button)


## Takes the HUD's existing main button (Play again or Return home) as the card's gold action, so the
## screen has one way on that every caller already knows. `with_home` adds a quieter Home button.
func adopt(button: Button, with_home: bool) -> void:
	if button.get_parent() != null:
		button.get_parent().remove_child(button)
	primary = button
	primary.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	primary.custom_minimum_size = Vector2(0.0, 100.0)
	var buttons: Control = find_child("Buttons", true, false)
	buttons.add_child(primary)
	buttons.move_child(primary, 0)
	home_button.visible = with_home


func _score_card() -> Control:
	var row: HBoxContainer = HBoxContainer.new()
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.add_theme_constant_override("separation", 28)
	var mine: VBoxContainer = _side("YOU", KalyeahTheme.TEAM_BLUE)
	mine_label = mine.get_node("Number") as Label
	var theirs: VBoxContainer = _side("THEM", KalyeahTheme.TEAM_RED)
	theirs_label = theirs.get_node("Number") as Label
	var dash: Label = Label.new()
	dash.text = "-"
	dash.theme_type_variation = &"DisplayLabel"
	dash.add_theme_font_size_override("font_size", 96)
	dash.add_theme_color_override("font_color", Color("9fb0d6"))
	dash.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	row.add_child(mine)
	row.add_child(dash)
	row.add_child(theirs)
	return UiKit.card(row, "inset", 18.0)


func _side(caption: String, color: Color) -> VBoxContainer:
	var column: VBoxContainer = VBoxContainer.new()
	column.add_theme_constant_override("separation", 0)
	column.custom_minimum_size = Vector2(150.0, 0.0)
	var number: Label = Label.new()
	number.name = "Number"
	number.theme_type_variation = &"DisplayLabel"
	number.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	number.add_theme_font_size_override("font_size", 120)
	number.add_theme_color_override("font_color", color.lightened(0.55))
	number.add_theme_color_override("font_outline_color", color.darkened(0.6))
	number.add_theme_constant_override("outline_size", 14)
	column.add_child(number)
	var chip: PanelContainer = UiKit.chip(caption, color, Color.WHITE, UiMetrics.CAPTION)
	chip.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	column.add_child(chip)
	return column


## Your kid and the two weapons you carried, and how long the match took.
func _you_row() -> Control:
	var row: HBoxContainer = HBoxContainer.new()
	row.add_theme_constant_override("separation", UiMetrics.GAP)
	kid_holder = Control.new()
	kid_holder.name = "Kid"
	kid_holder.custom_minimum_size = Vector2(96.0, 96.0)
	kid_holder.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	row.add_child(kid_holder)
	var words: VBoxContainer = VBoxContainer.new()
	words.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	words.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	words.add_theme_constant_override("separation", 4)
	words.add_child(UiKit.section_label("Your weapons"))
	weapons_row = HBoxContainer.new()
	weapons_row.name = "Weapons"
	weapons_row.add_theme_constant_override("separation", 12)
	words.add_child(weapons_row)
	row.add_child(words)
	var clock: VBoxContainer = VBoxContainer.new()
	clock.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	clock.add_theme_constant_override("separation", 4)
	clock.add_child(UiKit.section_label("Time"))
	time_label = UiKit.text("", UiMetrics.BODY, KalyeahTheme.TEXT, false)
	time_label.name = "Time"
	time_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	clock.add_child(time_label)
	row.add_child(clock)
	return row


## Shows or hides the card as the match enters or leaves MATCH_OVER (called every frame; it acts on a change).
func set_over(over: bool, sim: MatchSim, local_id: int, local_team: int, mode_text: String) -> void:
	if over == _shown:
		return
	_shown = over
	if not over:
		_hide()
		return
	_fill(sim, local_id, local_team, mode_text)
	_reveal()


func _fill(sim: MatchSim, local_id: int, local_team: int, mode_text: String) -> void:
	var won: bool = sim.score.winner == local_team
	title.text = "VICTORY!" if won else "DEFEAT"
	title.add_theme_color_override("font_color", WIN_COLOR if won else LOSS_COLOR)
	subtitle.text = "%s · Best of %d" % [mode_text, sim.score.best_of]
	mine_label.text = str(sim.score.points[local_team])
	theirs_label.text = str(sim.score.points[1 - local_team])
	var seconds: int = int(sim.match_time)
	time_label.text = "%d:%02d" % [seconds / 60, seconds % 60]
	for child: Node in kid_holder.get_children():
		kid_holder.remove_child(child)
		child.queue_free()
	for child: Node in weapons_row.get_children():
		weapons_row.remove_child(child)
		child.queue_free()
	var player: PlayerState = sim.players[local_id] if local_id >= 0 and local_id < sim.players.size() else null
	kid_holder.visible = player != null
	if player == null:
		return
	kid_holder.add_child(UiKit.avatar(String(player.character_id), 96.0))
	for id: StringName in player.weapons:
		var def: WeaponDef = sim.weapon_defs.get(id) as WeaponDef
		if def != null:
			weapons_row.add_child(WeaponsPage.holder(def, 64.0))


func _reveal() -> void:
	visible = true
	if is_instance_valid(_tween):
		_tween.kill()
	if Settings.reduced_motion or not is_inside_tree():
		modulate.a = 1.0
		_card.scale = Vector2.ONE
		mouse_filter = Control.MOUSE_FILTER_STOP
		return
	# the street's own banner first, then the card eases in and takes the touches
	modulate.a = 0.0
	_card.pivot_offset = _card.size / 2.0
	_card.scale = Vector2.ONE * 0.9
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_tween = create_tween()
	_tween.tween_interval(DELAY)
	_tween.tween_callback(func() -> void:
		_card.pivot_offset = _card.size / 2.0
		mouse_filter = Control.MOUSE_FILTER_STOP)
	_tween.set_parallel(true)
	_tween.tween_property(self, "modulate:a", 1.0, POP)
	_tween.tween_property(_card, "scale", Vector2.ONE, POP).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)


func _hide() -> void:
	if is_instance_valid(_tween):
		_tween.kill()
	visible = false
	mouse_filter = Control.MOUSE_FILTER_IGNORE
