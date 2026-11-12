class_name MatchResults
extends Control
## Centered match result over the dimmed arena: outcome, format, score, Tagalog Ranked LP card, and actions.

const WIN_COLOR: Color = Color("ffc642")
const LOSS_COLOR: Color = Color("f2735c")
const WIDTH: float = 600.0
const CONFETTI: bool = true
## Confetti paper size range (px).
const CONFETTI_MIN: float = 14.0
const CONFETTI_MAX: float = 24.0

signal home_pressed


class RankEmblemControl extends Control:
	var lp: int = 0

	func set_lp(value: int) -> void:
		if lp == value:
			return
		lp = value
		queue_redraw()

	func _draw() -> void:
		var span: float = minf(size.x, size.y) * 0.45
		RankedSystem.draw_emblem(self, size * 0.5, span, lp)


class RankLpBar extends Control:
	var current_ratio: float = 0.0
	var target_ratio: float = 0.0
	var fill_color: Color = Color("f5a623")
	var accent_color: Color = Color("ffd97d")

	func set_state(cur: float, target: float, fill: Color, accent: Color) -> void:
		current_ratio = clampf(cur, 0.0, 1.0)
		target_ratio = clampf(target, 0.0, 1.0)
		fill_color = fill
		accent_color = accent
		queue_redraw()

	func set_current_ratio(cur: float) -> void:
		current_ratio = clampf(cur, 0.0, 1.0)
		queue_redraw()

	func _draw() -> void:
		var rect: Rect2 = Rect2(Vector2.ZERO, size)
		if rect.size.x <= 4.0 or rect.size.y <= 4.0:
			return
		# Sunlit 90s Kalye warm recessed sand bar track.
		draw_rect(rect, Color("dfd0b4"), true)
		var inner: Rect2 = rect.grow(-1.5)
		draw_rect(inner, Color("efe4cd"), true)
		if target_ratio > current_ratio:
			var preview_w: float = inner.size.x * target_ratio
			draw_rect(Rect2(inner.position, Vector2(preview_w, inner.size.y)), Color(accent_color, 0.42), true)
		var fill_w: float = inner.size.x * current_ratio
		if fill_w > 1.0:
			var fill_rect: Rect2 = Rect2(inner.position, Vector2(fill_w, inner.size.y))
			draw_rect(fill_rect, fill_color, true)
			var gloss_rect: Rect2 = Rect2(inner.position, Vector2(fill_w, inner.size.y * 0.42))
			draw_rect(gloss_rect, accent_color.lerp(Color("faf3e3"), 0.35), true)
		draw_rect(rect, Color(Color("2a2522"), 0.36), false, 1.5)


var title: Label
var subtitle: Label
var mine_label: Label
var theirs_label: Label
var primary: Button
var home_button: Button

var rank_card: PanelContainer
var promo_banner: PanelContainer
var promo_label: Label
var emblem_view: RankEmblemControl
var rank_tier_label: Label
var rank_delta_chip: PanelContainer
var rank_delta_label: Label
var rank_lp_bar: RankLpBar
var rank_lp_label: Label

var confetti: CPUParticles2D
var _card: PanelContainer
var _final_mine: int = 0
var _final_theirs: int = 0
var _shown: bool = false
var _bar_from: float = 0.0
var _bar_to: float = 0.0
var _lp_tween: Tween
var _rank_card_style: StyleBoxFlat
var _gain_chip_style: StyleBoxFlat
var _loss_chip_style: StyleBoxFlat
var _promo_style: StyleBoxFlat


func _init() -> void:
	name = "MatchResults"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	visible = false
	_build_cached_styles()
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
	column.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	_card.add_child(column)
	title = Label.new()
	title.name = "Title"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	UiKit.letter(title, UiMetrics.HERO, WIN_COLOR, Color("2a2522"))
	column.add_child(title)
	subtitle = UiKit.text("", UiMetrics.DETAIL, Color("faf3e3"), false)
	subtitle.name = "Subtitle"
	subtitle.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(subtitle)
	column.add_child(_score_card())
	promo_banner = _make_promo_banner()
	column.add_child(promo_banner)
	rank_card = _make_rank_card()
	column.add_child(rank_card)
	var buttons: HBoxContainer = HBoxContainer.new()
	buttons.name = "Buttons"
	buttons.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	column.add_child(buttons)
	home_button = MenuDialog.make_button("Home", "blue", UiMetrics.ACTION)
	home_button.name = "HomeButton"
	home_button.custom_minimum_size = Vector2(260.0, 88.0)
	home_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	home_button.pressed.connect(home_pressed.emit)
	buttons.add_child(home_button)
	confetti = _make_confetti()
	add_child(confetti)
	# the paper falls behind the card, over the dimmed street: it never covers a button's words
	move_child(confetti, dim.get_index() + 1)


func _build_cached_styles() -> void:
	_rank_card_style = StyleBoxFlat.new()
	_rank_card_style.bg_color = Color("faf3e3")
	_rank_card_style.border_color = Color("f5a623")
	_rank_card_style.set_border_width_all(2)
	_rank_card_style.set_corner_radius_all(UiSkin.RADIUS_L)
	_rank_card_style.shadow_color = Color(Color("2a2522"), 0.22)
	_rank_card_style.shadow_size = 5
	_rank_card_style.shadow_offset = Vector2(0.0, 3.0)
	_rank_card_style.set_content_margin_all(14.0)
	_rank_card_style.anti_aliasing = true

	_gain_chip_style = StyleBoxFlat.new()
	_gain_chip_style.bg_color = Color("136e61")
	_gain_chip_style.border_color = Color("0d4f46")
	_gain_chip_style.set_border_width_all(2)
	_gain_chip_style.set_corner_radius_all(UiSkin.RADIUS_M)
	_gain_chip_style.content_margin_left = 14.0
	_gain_chip_style.content_margin_right = 14.0
	_gain_chip_style.content_margin_top = 4.0
	_gain_chip_style.content_margin_bottom = 4.0
	_gain_chip_style.anti_aliasing = true

	_loss_chip_style = StyleBoxFlat.new()
	_loss_chip_style.bg_color = Color("b83a28")
	_loss_chip_style.border_color = Color("87281a")
	_loss_chip_style.set_border_width_all(2)
	_loss_chip_style.set_corner_radius_all(UiSkin.RADIUS_M)
	_loss_chip_style.content_margin_left = 14.0
	_loss_chip_style.content_margin_right = 14.0
	_loss_chip_style.content_margin_top = 4.0
	_loss_chip_style.content_margin_bottom = 4.0
	_loss_chip_style.anti_aliasing = true

	_promo_style = StyleBoxFlat.new()
	_promo_style.bg_color = Color("f5a623")
	_promo_style.border_color = Color("9e5f08")
	_promo_style.set_border_width_all(2)
	_promo_style.set_corner_radius_all(UiSkin.RADIUS_M)
	_promo_style.content_margin_left = 18.0
	_promo_style.content_margin_right = 18.0
	_promo_style.content_margin_top = 6.0
	_promo_style.content_margin_bottom = 6.0
	_promo_style.anti_aliasing = true


func _make_promo_banner() -> PanelContainer:
	var panel: PanelContainer = PanelContainer.new()
	panel.name = "PromoBanner"
	panel.visible = false
	panel.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	panel.add_theme_stylebox_override("panel", _promo_style)
	promo_label = Label.new()
	promo_label.name = "PromoText"
	promo_label.text = "ANGAT RANGGO!"
	promo_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	promo_label.add_theme_font_override("font", KalyeahTheme.DISPLAY_FONT)
	promo_label.add_theme_font_size_override("font_size", UiMetrics.ACTION)
	promo_label.add_theme_color_override("font_color", Color("2a2522"))
	promo_label.add_theme_constant_override("outline_size", 0)
	panel.add_child(promo_label)
	return panel


func _make_rank_card() -> PanelContainer:
	var row: HBoxContainer = HBoxContainer.new()
	row.name = "RankRow"
	row.add_theme_constant_override("separation", UiMetrics.GAP)
	row.alignment = BoxContainer.ALIGNMENT_CENTER

	emblem_view = RankEmblemControl.new()
	emblem_view.name = "RankEmblem"
	emblem_view.custom_minimum_size = Vector2(72.0, 72.0)
	emblem_view.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	emblem_view.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_child(emblem_view)

	var details: VBoxContainer = VBoxContainer.new()
	details.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	details.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	details.add_theme_constant_override("separation", 6)
	row.add_child(details)

	var header: HBoxContainer = HBoxContainer.new()
	header.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	details.add_child(header)

	rank_tier_label = Label.new()
	rank_tier_label.name = "RankTier"
	rank_tier_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	rank_tier_label.add_theme_font_override("font", KalyeahTheme.DISPLAY_FONT)
	rank_tier_label.add_theme_font_size_override("font_size", UiMetrics.ACTION)
	rank_tier_label.add_theme_color_override("font_color", Color("2a2522"))
	rank_tier_label.add_theme_constant_override("outline_size", 0)
	header.add_child(rank_tier_label)

	rank_delta_chip = PanelContainer.new()
	rank_delta_chip.name = "RankDeltaChip"
	rank_delta_chip.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	rank_delta_chip.mouse_filter = Control.MOUSE_FILTER_IGNORE
	rank_delta_chip.add_theme_stylebox_override("panel", _gain_chip_style)
	rank_delta_label = Label.new()
	rank_delta_label.name = "RankDelta"
	rank_delta_label.add_theme_font_override("font", KalyeahTheme.BUTTON_FONT)
	rank_delta_label.add_theme_font_size_override("font_size", UiMetrics.CAPTION)
	rank_delta_label.add_theme_color_override("font_color", Color("faf3e3"))
	rank_delta_chip.add_child(rank_delta_label)
	header.add_child(rank_delta_chip)

	rank_lp_bar = RankLpBar.new()
	rank_lp_bar.name = "RankLpBar"
	rank_lp_bar.custom_minimum_size = Vector2(0.0, 16.0)
	rank_lp_bar.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	rank_lp_bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	details.add_child(rank_lp_bar)

	rank_lp_label = UiKit.text("", UiMetrics.CAPTION, Color("57493f"), false)
	rank_lp_label.name = "RankLpText"
	details.add_child(rank_lp_label)

	var card: PanelContainer = PanelContainer.new()
	card.name = "RankCard"
	card.mouse_filter = Control.MOUSE_FILTER_PASS
	card.add_theme_stylebox_override("panel", _rank_card_style)
	card.add_child(row)
	return card


## Takes the HUD's existing main button (Play again or Return home) as the blue action, so the
## screen has one way on that every caller already knows. `with_home` adds Home beside it.
func adopt(button: Button, with_home: bool) -> void:
	if button.get_parent() != null:
		button.get_parent().remove_child(button)
	primary = button
	primary.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	primary.custom_minimum_size = Vector2(260.0, 88.0)
	var buttons: Control = find_child("Buttons", true, false) as Control
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
	var separator: Label = Label.new()
	separator.text = "-"
	UiKit.letter(separator, UiMetrics.DISPLAY, Color("faf3e3"), Color("2a2522"))
	separator.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	row.add_child(mine)
	row.add_child(separator)
	row.add_child(theirs)
	return row


## "How the points went": a chip for each point, blue when you scored it and red when they did.
## The words for one point (the chip's colour says who scored it): blue held their base or tagged them all, red held yours or tagged you.
static func how_text(ours: bool, how: int) -> String:
	if how == MatchSim.CaptureKind.ALL_TAGGED:
		return "Tagged them all" if ours else "Tagged you all"
	if how == MatchSim.CaptureKind.TOO_SLOW:
		return "Runners too slow" if ours else "You were too slow"
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
	UiKit.letter(number, UiMetrics.DISPLAY, color.lightened(0.55), Color("2a2522"))
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


## A one-shot burst of barrio fiesta banderitas paper from the top edge: sunlit mango, sari-sari jade, fiesta coral, and cream.
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
	ramp.colors = PackedColorArray([Color("f5a623"), Color("1f9d8b"), Color("e2553e"), Color("faf3e3")])
	burst.color_initial_ramp = ramp
	return burst


func _fill(sim: MatchSim, local_id: int, local_team: int, mode_text: String) -> void:
	var won: bool = sim.score.winner == local_team
	var mine: int = sim.score.points[local_team]
	var theirs: int = sim.score.points[1 - local_team]
	if sim.mode != null:
		var result: Dictionary = sim.mode.local_result(sim, local_id)
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
	_fill_ranked(mode_text, won, mine, theirs)


func _fill_ranked(mode_text: String, won: bool, mine: int, theirs: int) -> void:
	var outcome: Dictionary = RankedSystem.consume_last_result()
	if outcome.is_empty():
		outcome = RankedSystem.preview_match(mode_text, won, mine, theirs)
	var old_lp: int = outcome["old_lp"] as int
	var new_lp: int = outcome["new_lp"] as int
	var delta: int = outcome["delta"] as int
	var tier_title: String = outcome["tier"] as String
	var promoted: bool = outcome["promoted"] as bool
	var spec: Dictionary = RankedSystem.tier_for_lp(new_lp)
	var tier_color: Color = spec["color"] as Color
	var tier_accent: Color = spec["accent"] as Color
	var tier_ink: Color = Color("2a2522").lerp(spec["dark"] as Color, 0.72)

	emblem_view.set_lp(new_lp)
	rank_tier_label.text = tier_title.to_upper()
	rank_tier_label.add_theme_color_override("font_color", tier_ink)

	if delta >= 0:
		rank_delta_label.text = "+%d LP" % delta
		rank_delta_chip.add_theme_stylebox_override("panel", _gain_chip_style)
	else:
		rank_delta_label.text = "%d LP" % delta
		rank_delta_chip.add_theme_stylebox_override("panel", _loss_chip_style)

	var ceiling_lp: int = RankedSystem.next_tier_lp(new_lp)
	if ceiling_lp > 0:
		var next_name: String = RankedSystem.tier_name(ceiling_lp)
		rank_lp_label.text = "%d / %d LP · Next: %s" % [new_lp, ceiling_lp, next_name]
	else:
		rank_lp_label.text = "%d LP · Top Barrio Rank" % new_lp

	promo_banner.visible = promoted
	_bar_to = RankedSystem.tier_progress(new_lp)
	_bar_from = 0.0 if promoted else RankedSystem.tier_progress(old_lp)
	rank_lp_bar.set_state(_bar_from, _bar_to, tier_color, tier_accent)


## The two scores at `progress` (0 to 1) of the way to their final numbers.
func _show_scores(progress: float) -> void:
	mine_label.text = str(roundi(_final_mine * progress))
	theirs_label.text = str(roundi(_final_theirs * progress))


func _reveal() -> void:
	visible = true
	if is_instance_valid(_lp_tween):
		_lp_tween.kill()
	confetti.emitting = false
	confetti.position = Vector2(size.x / 2.0, -30.0)
	confetti.emission_rect_extents = Vector2(size.x / 2.0, 6.0)
	modulate.a = 1.0
	_card.scale = Vector2.ONE
	mouse_filter = Control.MOUSE_FILTER_STOP
	_show_scores(1.0)
	if Settings.reduced_motion or not is_inside_tree():
		rank_lp_bar.set_current_ratio(_bar_to)
		return
	rank_lp_bar.set_current_ratio(_bar_from)
	if absf(_bar_to - _bar_from) > 0.005:
		_lp_tween = create_tween()
		_lp_tween.tween_method(rank_lp_bar.set_current_ratio, _bar_from, _bar_to, 0.55).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	if CONFETTI and _final_mine > _final_theirs:
		confetti.restart()


func _hide() -> void:
	if is_instance_valid(_lp_tween):
		_lp_tween.kill()
	visible = false
	confetti.emitting = false
	mouse_filter = Control.MOUSE_FILTER_IGNORE
