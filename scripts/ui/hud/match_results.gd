class_name MatchResults
extends Control
## Centered match result over the dimmed arena: outcome, format, score and actions, without a panel or delay.

const WIN_COLOR: Color = Color("ffd25a")
const LOSS_COLOR: Color = Color("ff8a80")
const WIDTH: float = 600.0
const CONFETTI: bool = true
## Confetti paper size range (px).
const CONFETTI_MIN: float = 14.0
const CONFETTI_MAX: float = 24.0

signal home_pressed

var title: Label
var subtitle: Label
var mine_label: Label
var theirs_label: Label
var primary: Button
var home_button: Button

var confetti: CPUParticles2D
var _card: PanelContainer
var _final_mine: int = 0
var _final_theirs: int = 0
var _shown: bool = false


func _init() -> void:
	name = "MatchResults"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	visible = false
	var dim: ColorRect = ColorRect.new()
	dim.color = MenuDialog.DIM
	dim.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	dim.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(dim)
	var center: CenterContainer = CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	center.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(center)
	_card = PanelContainer.new()
	_card.add_theme_stylebox_override("panel", StyleBoxEmpty.new())
	_card.custom_minimum_size = Vector2(WIDTH, 0.0)
	center.add_child(_card)
	var column: VBoxContainer = VBoxContainer.new()
	column.add_theme_constant_override("separation", UiMetrics.GAP)
	_card.add_child(column)
	title = Label.new()
	title.name = "Title"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	UiKit.letter(title, UiMetrics.HERO)
	column.add_child(title)
	subtitle = UiKit.text("", UiMetrics.DETAIL, Color("c9d6f2"), false)
	subtitle.name = "Subtitle"
	subtitle.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(subtitle)
	column.add_child(_score_card())
	var buttons: HBoxContainer = HBoxContainer.new()
	buttons.name = "Buttons"
	buttons.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	column.add_child(buttons)
	home_button = MenuDialog.make_button("Home", "blue", UiMetrics.ACTION)
	home_button.name = "HomeButton"
	home_button.custom_minimum_size = Vector2(280.0, 100.0)
	home_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	home_button.pressed.connect(home_pressed.emit)
	buttons.add_child(home_button)
	confetti = _make_confetti()
	add_child(confetti)
	# the paper falls behind the card, over the dimmed street: it never covers a button's words
	move_child(confetti, dim.get_index() + 1)


## Takes the HUD's existing main button (Play again or Return home) as the blue action, so the
## screen has one way on that every caller already knows. `with_home` adds Home beside it.
func adopt(button: Button, with_home: bool) -> void:
	if button.get_parent() != null:
		button.get_parent().remove_child(button)
	primary = button
	primary.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	primary.custom_minimum_size = Vector2(280.0, 100.0)
	var buttons: Control = find_child("Buttons", true, false)
	buttons.add_child(primary)
	KalyeahTheme.style_button(primary, "blue")
	home_button.visible = with_home


func _score_card() -> Control:
	var row: HBoxContainer = HBoxContainer.new()
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.add_theme_constant_override("separation", 28)
	var mine: VBoxContainer = _side("YOU", TeamColors.ui_own())
	mine_label = mine.get_node("Number") as Label
	var theirs: VBoxContainer = _side("THEM", TeamColors.ui_enemy())
	theirs_label = theirs.get_node("Number") as Label
	var dash: Label = Label.new()
	dash.text = "-"
	UiKit.letter(dash, UiMetrics.DISPLAY, Color("9fb0d6"))
	dash.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	row.add_child(mine)
	row.add_child(dash)
	row.add_child(theirs)
	return row


## "How the points went": a chip for each point, blue when you scored it and red when they did.
## The words for one point (the chip's colour says who scored it): blue held their base or tagged them all, red held yours or tagged you.
static func how_text(ours: bool, how: int) -> String:
	if how == MatchSim.CaptureKind.ALL_TAGGED:
		return "Tagged them all" if ours else "Tagged you all"
	if how == MatchSim.CaptureKind.TOUCHED:
		return "Tagged a runner" if ours else "Tagged by a guard"
	if how == MatchSim.CaptureKind.NONE:
		return "Ran it home" if ours else "They ran it home"  # Tumbang Preso, Patintero
	return "Held their base" if ours else "Held your base"


## The point log grouped: [ours, how, count] for each way a point was won, in the order each first happened.
static func how_groups(point_log: Array[Vector2i], local_team: int) -> Array[Array]:
	var groups: Array[Array] = []
	for entry: Vector2i in point_log:
		var ours: bool = entry.x == local_team
		var found: bool = false
		for group: Array in groups:
			if group[0] == ours and group[1] == entry.y:
				group[2] = (group[2] as int) + 1
				found = true
				break
		if not found:
			groups.append([ours, entry.y, 1])
	return groups


## The words on one chip: how, and "× N" when it happened more than once.
static func group_text(ours: bool, how: int, times: int) -> String:
	return how_text(ours, how) if times < 2 else "%s × %d" % [UiText.t(how_text(ours, how)), times]


func _side(_caption: String, color: Color) -> VBoxContainer:
	var column: VBoxContainer = VBoxContainer.new()
	column.add_theme_constant_override("separation", 0)
	column.custom_minimum_size = Vector2(80.0, 0.0)
	var number: Label = Label.new()
	number.name = "Number"
	number.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	UiKit.letter(number, UiMetrics.DISPLAY, color.lightened(0.55), color.darkened(0.6))
	column.add_child(number)
	return column


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


## A one-shot burst of paper squares from the top edge: gold, blue, red and green, tumbling as they fall.
func _make_confetti() -> CPUParticles2D:
	var burst: CPUParticles2D = CPUParticles2D.new()
	burst.name = "Confetti"
	burst.emitting = false
	burst.one_shot = true
	burst.amount = 70
	burst.lifetime = 2.6
	burst.explosiveness = 0.85
	burst.emission_shape = CPUParticles2D.EMISSION_SHAPE_RECTANGLE
	burst.emission_rect_extents = Vector2(360.0, 6.0)
	burst.position = Vector2(360.0, -30.0)
	burst.direction = Vector2(0.0, 1.0)
	burst.spread = 35.0
	burst.initial_velocity_min = 220.0
	burst.initial_velocity_max = 520.0
	burst.gravity = Vector2(0.0, 520.0)
	burst.angular_velocity_min = -360.0
	burst.angular_velocity_max = 360.0
	burst.angle_min = 0.0
	burst.angle_max = 360.0
	burst.scale_amount_min = CONFETTI_MIN
	burst.scale_amount_max = CONFETTI_MAX
	var ramp: Gradient = Gradient.new()
	ramp.offsets = PackedFloat32Array([0.0, 0.33, 0.66, 1.0])
	ramp.colors = PackedColorArray([Color("ffc233"), Color("4aa0ff"), Color("ff7a70"), Color("45dd78")])
	burst.color_initial_ramp = ramp
	return burst


func _fill(sim: MatchSim, local_id: int, local_team: int, mode_text: String) -> void:
	var won: bool = sim.score.winner == local_team
	var mine: int = sim.score.points[local_team]
	var theirs: int = sim.score.points[1 - local_team]
	if sim.mode != null:
		var result: Dictionary = sim.mode.local_result(sim, local_id)  # a street game knows its own winner and points
		won = result["won"] as bool
		mine = result["mine"] as int
		theirs = result["theirs"] as int
	title.text = "Victory" if won else "Defeat"
	title.add_theme_color_override("font_color", WIN_COLOR if won else LOSS_COLOR)
	var allies: int = 0
	var enemies: int = 0
	for state: PlayerState in sim.players.values():
		if state.team == local_team:
			allies += 1
		else:
			enemies += 1
	if sim.mode != null and sim.mode.is_free_for_all():
		subtitle.text = "%s (%s · %d min) (%d kids)" % [mode_text, sim.mode.rules.title, roundi(sim.mode.rules.match_seconds / 60.0), allies]
	elif sim.mode != null:
		subtitle.text = "%s (%s · %d min) (%dv%d)" % [mode_text, sim.mode.rules.title, roundi(sim.mode.rules.match_seconds / 60.0), allies, enemies]
	else:
		subtitle.text = "%s (First to %d) (%dv%d)" % [mode_text, sim.score.first_to, allies, enemies]
	_final_mine = mine
	_final_theirs = theirs
	_show_scores(1.0)



## The two scores at `progress` (0 to 1) of the way to their final numbers.
func _show_scores(progress: float) -> void:
	mine_label.text = str(roundi(_final_mine * progress))
	theirs_label.text = str(roundi(_final_theirs * progress))


func _reveal() -> void:
	visible = true
	confetti.emitting = false
	confetti.position = Vector2(size.x / 2.0, -30.0)
	confetti.emission_rect_extents = Vector2(size.x / 2.0, 6.0)
	if Settings.reduced_motion or not is_inside_tree():
		modulate.a = 1.0
		_card.scale = Vector2.ONE
		mouse_filter = Control.MOUSE_FILTER_STOP
		_show_scores(1.0)
		return
	# The dim, final numbers and touch shield appear together.
	modulate.a = 1.0
	mouse_filter = Control.MOUSE_FILTER_STOP
	_show_scores(1.0)
	_card.scale = Vector2.ONE
	if CONFETTI and _final_mine > _final_theirs:
		confetti.restart()



func _hide() -> void:
	visible = false
	confetti.emitting = false
	mouse_filter = Control.MOUSE_FILTER_IGNORE
