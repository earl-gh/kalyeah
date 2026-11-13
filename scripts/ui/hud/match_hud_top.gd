class_name MatchHudTop
extends RefCounted
## Top-of-screen match HUD builder & layout:
## - Airy Deadlock-inspired 5v5 circular squad portrait wings flanking the centered Scoreboard & ClockChip.
## - Settings gear button that stays in the top-left header in portrait/street games, and tucks cleanly as a
##   compact `34px` white-outline badge onto the bottom-right corner of `BarangayMinimap` in landscape 5v5.

## Empty space round the gear's art inside its button, as a share of the art's size on each side.
const GEAR_PADDING: float = 0.3
## Standard match settings gear texture used in portrait & street games.
const GEAR: Texture2D = preload("res://assets/ui/match_gear.webp")

const WING_SLOTS: int = 5
const CHIP_W: float = 34.0
const CHIP_H: float = 42.0
const CHIP_GAP: float = 6.0
const WING_W: float = CHIP_W * float(WING_SLOTS) + CHIP_GAP * float(WING_SLOTS - 1) + 4.0
const WING_H: float = CHIP_H + 4.0
const WING_SCORE_GAP: float = 10.0


class GearBadge extends Control:
	const BADGE_RADIUS: float = 17.0
	const BADGE_BG: Color = Color(0.06, 0.08, 0.11, 0.42)
	const BADGE_BG_HELD: Color = Color(0.14, 0.17, 0.22, 0.56)

	var held: bool = false

	func _init() -> void:
		name = "GearBadge"
		mouse_filter = Control.MOUSE_FILTER_IGNORE
		set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)

	func _draw() -> void:
		var center: Vector2 = size * 0.5
		draw_circle(center, BADGE_RADIUS, BADGE_BG_HELD if held else BADGE_BG)
		draw_circle(center, BADGE_RADIUS, HudStyle.WHITE_BODY_HELD if held else HudStyle.WHITE_BODY)
		draw_arc(center, BADGE_RADIUS, 0.0, TAU, 36, HudStyle.WHITE_RIM, 2.0, true)
		HudStyle.draw_white_icon_or_glyph(self, center, BADGE_RADIUS, &"gear", 0.92)


class GearButton extends TextureButton:
	var _badge: GearBadge
	var _compact: bool = false

	func _init() -> void:
		_badge = GearBadge.new()
		_badge.visible = false
		add_child(_badge)
		button_down.connect(_sync_press)
		button_up.connect(_sync_press)

	func set_compact_badge(on: bool) -> void:
		if _compact == on and _badge.visible == on:
			return
		_compact = on
		self_modulate = Color(1.0, 1.0, 1.0, 0.0 if on else 1.0)
		_badge.visible = on
		_badge.queue_redraw()

	func _sync_press() -> void:
		if _badge != null:
			_badge.held = is_pressed()
			_badge.queue_redraw()


class SquadWing extends Control:
	const PORTRAIT_RADIUS: float = 15.0
	const ULT_PIP_RADIUS: float = 4.6
	const CIRCLE_STEPS: int = 28

	const DISC_BG: Color = Color(0.06, 0.08, 0.11, 0.42)
	const BAR_TRACK: Color = Color(0.05, 0.07, 0.10, 0.46)
	const ALLY_ACCENT: Color = Color(0.32, 0.68, 1.0, 0.86)
	const ENEMY_ACCENT: Color = Color(0.95, 0.36, 0.34, 0.86)
	const HP_ALLY: Color = Color(0.36, 0.92, 0.62, 0.95)
	const HP_WARN: Color = Color(1.0, 0.78, 0.28, 0.95)
	const HP_LOW: Color = Color(0.95, 0.36, 0.34, 0.95)
	const ULT_GLOW: Color = Color(1.0, 0.84, 0.32, 0.96)
	const TAG_CROSS: Color = Color(0.96, 0.38, 0.36, 0.78)
	const SHADOW: Color = Color(0.04, 0.05, 0.07, 0.55)

	var ally_side: bool = true
	var _count: int = 0
	var _ids: Array[int] = [0, 0, 0, 0, 0]
	var _chars: Array[StringName] = [&"", &"", &"", &"", &""]
	var _faces: Array[Texture2D] = [null, null, null, null, null]
	var _initials: Array[String] = ["?", "?", "?", "?", "?"]
	var _hp_shares: Array[float] = [1.0, 1.0, 1.0, 1.0, 1.0]
	var _ult_full: Array[bool] = [false, false, false, false, false]
	var _tagged: Array[bool] = [false, false, false, false, false]
	var _self_flag: Array[bool] = [false, false, false, false, false]

	func _init(is_ally: bool = true) -> void:
		ally_side = is_ally
		name = "AllySquadWing" if is_ally else "EnemySquadWing"
		custom_minimum_size = Vector2(WING_W, WING_H)
		size = Vector2(WING_W, WING_H)
		mouse_filter = Control.MOUSE_FILTER_IGNORE

	func sync_team(sim: MatchSim, team: int, local_id: int) -> void:
		if sim == null or sim.rules == null:
			return
		var roster: Array[int] = []
		for id: int in sim.players:
			var p: PlayerState = sim.players[id]
			if p != null and p.team == team:
				roster.append(id)
		roster.sort()
		var n: int = mini(roster.size(), WING_SLOTS)
		var max_hp: float = maxf(float(sim.rules.player_max_hp), 1.0)
		var dirty: bool = n != _count
		_count = n
		for i: int in n:
			var pid: int = roster[i]
			var state: PlayerState = sim.players[pid]
			var hp_s: float = 0.0 if state.tagged else clampf(float(state.hp) / max_hp, 0.0, 1.0)
			var ult_ok: bool = not state.tagged and state.ult_charge >= 99.99
			var is_me: bool = pid == local_id
			if _ids[i] != pid or _chars[i] != state.character_id:
				_ids[i] = pid
				_chars[i] = state.character_id
				_faces[i] = KidModel.minimap_face(state.character_id)
				var raw_name: String = String(state.character_id).strip_edges().to_upper()
				_initials[i] = raw_name.substr(0, 1) if not raw_name.is_empty() else "K"
				dirty = true
			if not is_equal_approx(_hp_shares[i], hp_s) or _ult_full[i] != ult_ok or _tagged[i] != state.tagged or _self_flag[i] != is_me:
				_hp_shares[i] = hp_s
				_ult_full[i] = ult_ok
				_tagged[i] = state.tagged
				_self_flag[i] = is_me
				dirty = true
		if dirty:
			queue_redraw()

	func _team_accent() -> Color:
		if ally_side:
			return Color(TeamColors.score_own(), 0.88) if Settings.colorblind else ALLY_ACCENT
		return Color(TeamColors.score_enemy(), 0.88) if Settings.colorblind else ENEMY_ACCENT

	func _bar_color(share: float) -> Color:
		if not ally_side:
			return Color(TeamColors.score_enemy(), 0.92) if Settings.colorblind else ENEMY_ACCENT
		if Settings.colorblind:
			return Color(TeamColors.BLIND_OWN, 0.95)
		if share <= 0.30:
			return HP_LOW
		if share <= 0.60:
			return HP_WARN
		return HP_ALLY

	func _draw() -> void:
		if _count <= 0:
			return
		var font: Font = KalyeahTheme.ACTION_FONT
		var accent: Color = _team_accent()
		var total_w: float = float(_count) * CHIP_W + float(maxi(_count - 1, 0)) * CHIP_GAP
		var start_x: float = (size.x - total_w - 2.0) if ally_side else 2.0

		for i: int in _count:
			var x: float = start_x + float(i) * (CHIP_W + CHIP_GAP)
			var center: Vector2 = Vector2(x + CHIP_W * 0.5, 2.0 + PORTRAIT_RADIUS + 2.0)
			var is_out: bool = _tagged[i]

			draw_circle(center, PORTRAIT_RADIUS + 0.5, DISC_BG)

			var tex: Texture2D = _faces[i]
			if tex != null:
				var face_tint: Color = Color(0.45, 0.47, 0.52, 0.46) if is_out else Color.WHITE
				_draw_round_face(center, PORTRAIT_RADIUS - 0.8, tex, face_tint)
			else:
				var fallback_bg: Color = Color(accent.r * 0.45, accent.g * 0.45, accent.b * 0.45, 0.35 if is_out else 0.58)
				draw_circle(center, PORTRAIT_RADIUS - 0.8, fallback_bg)
				var init_str: String = _initials[i]
				var iw: float = font.get_string_size(init_str, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL).x
				var ib: Vector2 = Vector2(center.x - iw * 0.5, center.y + 6.0)
				draw_string(font, ib + Vector2(1.0, 1.0), init_str, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL, SHADOW)
				draw_string(font, ib, init_str, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL, Color(1.0, 1.0, 1.0, 0.48 if is_out else 0.94))

			var ring_col: Color = Color(1.0, 1.0, 1.0, 0.24) if is_out else (Color(1.0, 1.0, 1.0, 0.92) if _self_flag[i] else accent)
			var ring_w: float = 2.0 if _self_flag[i] and not is_out else 1.5
			draw_arc(center, PORTRAIT_RADIUS, 0.0, TAU, CIRCLE_STEPS, ring_col, ring_w, true)

			# Slim airy HP strip beneath portrait: your own team's only (owner, 2026-11-13: an enemy's health is never shown during play)
			var bar_rect: Rect2 = Rect2(Vector2(x + 2.0, CHIP_H - 4.5), Vector2(CHIP_W - 4.0, 3.5))
			if ally_side:
				draw_rect(bar_rect, BAR_TRACK, true)
			if ally_side and not is_out and _hp_shares[i] > 0.01:
				var fill_w: float = bar_rect.size.x * _hp_shares[i]
				draw_rect(Rect2(bar_rect.position, Vector2(fill_w, bar_rect.size.y)), _bar_color(_hp_shares[i]), true)

			if is_out:
				var arm: float = PORTRAIT_RADIUS * 0.42
				draw_line(center + Vector2(-arm, -arm), center + Vector2(arm, arm), TAG_CROSS, 1.8, true)
				draw_line(center + Vector2(arm, -arm), center + Vector2(-arm, arm), TAG_CROSS, 1.8, true)
			elif _ult_full[i]:
				_draw_ult_pip(center + Vector2(PORTRAIT_RADIUS * 0.72, -PORTRAIT_RADIUS * 0.72))

	func _draw_round_face(center: Vector2, radius: float, tex: Texture2D, tint: Color) -> void:
		var pts: PackedVector2Array = PackedVector2Array()
		var uvs: PackedVector2Array = PackedVector2Array()
		for step_i: int in CIRCLE_STEPS:
			var way: Vector2 = Vector2.from_angle(TAU * float(step_i) / float(CIRCLE_STEPS))
			pts.append(center + way * radius)
			uvs.append(Vector2(0.5, 0.5) + way * 0.48)
		draw_polygon(pts, PackedColorArray([tint]), uvs, tex)

	func _draw_ult_pip(center: Vector2) -> void:
		draw_circle(center, ULT_PIP_RADIUS + 1.0, SHADOW)
		var pts: PackedVector2Array = PackedVector2Array()
		for k: int in 8:
			var ang: float = -PI * 0.5 + TAU * float(k) / 8.0
			var r: float = ULT_PIP_RADIUS if k % 2 == 0 else ULT_PIP_RADIUS * 0.44
			pts.append(center + Vector2.from_angle(ang) * r)
		draw_colored_polygon(pts, ULT_GLOW)


static func build(hud: MatchHud) -> void:
	hud.header = HeaderPanel.new()
	hud.header.set_header_height(MatchHud.HEADER_HEIGHT)
	hud.add_child(hud.header)
	hud.top = Control.new()
	hud._place(hud.top, Vector4(0, 0, 1, 0), Vector4(0, 0, 0, 520))
	hud.top.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud.add_child(hud.top)
	# Settings button (`>= 80x80` touch rect; compact `34px` white-rimmed badge at minimap's bottom-right in landscape 5v5)
	var gear_btn: GearButton = GearButton.new()
	hud.settings_button = gear_btn
	var gear: AtlasTexture = AtlasTexture.new()
	gear.atlas = GEAR
	gear.region = Rect2(Vector2.ZERO, GEAR.get_size())
	var pad: Vector2 = gear.region.size * GEAR_PADDING
	gear.margin = Rect2(pad, pad * 2.0)
	hud.settings_button.texture_normal = gear
	hud.settings_button.ignore_texture_size = true
	hud.settings_button.stretch_mode = TextureButton.STRETCH_KEEP_ASPECT_CENTERED
	hud.settings_button.tooltip_text = "Match settings"
	UiA11y.name_it(hud.settings_button, "Match settings")
	hud.settings_button.pressed.connect(hud.toggle_menu)
	hud.top.add_child(hud.settings_button)
	# Airy 5v5 Squad Wings flanking the scoreboard
	hud.ally_wing = SquadWing.new(true)
	hud.ally_wing.visible = false
	hud.top.add_child(hud.ally_wing)
	hud.enemy_wing = SquadWing.new(false)
	hud.enemy_wing.visible = false
	hud.top.add_child(hud.enemy_wing)
	# Top-center scoreboard & match clock
	hud.scoreboard = Scoreboard.new()
	hud.scoreboard.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud.top.add_child(hud.scoreboard)
	hud.clock_chip = ClockChip.new()
	hud.top.add_child(hud.clock_chip)
	layout(hud)


static func sync_wings(hud: MatchHud, sim: MatchSim, local_team: int, local_id: int, shown: bool) -> void:
	if hud.ally_wing == null or hud.enemy_wing == null:
		return
	var show_wings: bool = shown and hud.landscape and not hud.street_game
	hud.ally_wing.visible = show_wings
	hud.enemy_wing.visible = show_wings
	if not show_wings:
		return
	(hud.ally_wing as SquadWing).sync_team(sim, local_team, local_id)
	(hud.enemy_wing as SquadWing).sync_team(sim, 1 - local_team, local_id)


## Places the top-center scoreboard & clock, 5v5 squad wings, and settings gear:
## - Portrait / street games: standard gear button in top-left header (`MARGIN, TOP_ROW_Y`).
## - Landscape 5v5: compact `34px` white-rimmed gear badge (`80x80` touch rect) at the minimap's bottom-right.
static func layout(hud: MatchHud) -> void:
	if hud.intro_bar != null:
		hud._place(hud.intro_bar, Vector4(0, 1, 1, 1), Vector4(80, -(hud.console_height() + 150.0), -80, -(hud.console_height() + 6.0)))
	if hud.tag_toast != null:
		var under: float = hud.header_height() + TagToast.GAP
		hud._place(hud.tag_toast, Vector4(0, 0, 1, 0), Vector4(0, under, 0, under + TagToast.HEIGHT))
	if hud.capture_alert != null:
		hud._place(hud.capture_alert, Vector4(0, 1, 1, 1), Vector4(0, -(hud.console_height() + TagToast.HEIGHT + TagToast.GAP), 0, -(hud.console_height() + TagToast.GAP)))
	hud._place(hud.scoreboard, Vector4(0.5, 0, 0.5, 0), Vector4(-MatchHud.SCORE_SIZE.x / 2.0, MatchHud.TOP_ROW_Y, MatchHud.SCORE_SIZE.x / 2.0, MatchHud.TOP_ROW_Y + MatchHud.SCORE_SIZE.y))

	var compact_gear: bool = hud.landscape and not hud.street_game
	if hud.settings_button is GearButton:
		(hud.settings_button as GearButton).set_compact_badge(compact_gear)
	if compact_gear:
		# Note: `hud.top.position.y == hud.top_inset`, so local `12.0` matches `minimap` global top `hud.top_inset + 12.0`.
		var gear_cx: float = hud.side_insets.x + 16.0 + BarangayMinimap.SIZE.x - 22.0
		var gear_cy: float = 12.0 + BarangayMinimap.SIZE.y - 22.0
		var half_hit: float = MatchHud.GEAR_SIZE * 0.5
		hud._place(hud.settings_button, Vector4(0, 0, 0, 0), Vector4(gear_cx - half_hit, gear_cy - half_hit, gear_cx + half_hit, gear_cy + half_hit))
	else:
		var gear_box: Vector4 = Vector4(MatchHud.MARGIN, MatchHud.TOP_ROW_Y, MatchHud.MARGIN + MatchHud.GEAR_SIZE, MatchHud.TOP_ROW_Y + MatchHud.GEAR_SIZE)
		hud._place(hud.settings_button, Vector4(0, 0, 0, 0), gear_box)

	var middle: Rect2 = Scoreboard.middle_rect(MatchHud.SCORE_SIZE, MatchHud.CLOCK_GAP)
	var left: float = middle.position.x - MatchHud.SCORE_SIZE.x / 2.0
	hud._place(hud.clock_chip, Vector4(0.5, 0, 0.5, 0), Vector4(left, MatchHud.TOP_ROW_Y + middle.position.y, left + middle.size.x, MatchHud.TOP_ROW_Y + middle.position.y + middle.size.y))
	if hud.ally_wing != null and hud.enemy_wing != null:
		var half_score: float = (MatchHud.SCORE_SIZE.x * HudStyle.HEADER_SCALE) * 0.5
		var wing_y: float = MatchHud.TOP_ROW_Y + 4.0
		hud._place(hud.ally_wing, Vector4(0.5, 0, 0.5, 0), Vector4(-(half_score + WING_SCORE_GAP + WING_W), wing_y, -(half_score + WING_SCORE_GAP), wing_y + WING_H))
		hud._place(hud.enemy_wing, Vector4(0.5, 0, 0.5, 0), Vector4(half_score + WING_SCORE_GAP, wing_y, half_score + WING_SCORE_GAP + WING_W, wing_y + WING_H))


## The opening objective appears as a compact floating sheet above the controls.
static func build_intro(hud: MatchHud) -> void:
	var panel: Control = Control.new()
	hud.intro_bar = panel
	hud._place(hud.intro_bar, Vector4(0, 1, 1, 1), Vector4(80, -(MatchHud.CONSOLE_HEIGHT + 150.0), -80, -(MatchHud.CONSOLE_HEIGHT + 6.0)))
	hud.intro_bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud.intro_bar.visible = false
	hud.add_child(hud.intro_bar)
	hud.intro_title = Label.new()
	hud._place(hud.intro_title, Vector4(0, 0, 1, 0), Vector4(0, 14, 0, 72))
	hud.intro_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	UiKit.letter(hud.intro_title, UiMetrics.HEADLINE, Color(1.0, 0.82, 0.25))
	hud.intro_bar.add_child(hud.intro_title)
	hud.intro_sub = Label.new()
	hud._place(hud.intro_sub, Vector4(0, 0, 1, 0), Vector4(0, 72, 0, 112))
	hud.intro_sub.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	hud.intro_sub.add_theme_font_size_override("font_size", UiMetrics.ACTION)
	hud.intro_sub.add_theme_color_override("font_outline_color", Color(0.1, 0.05, 0.1))
	hud.intro_sub.add_theme_constant_override("outline_size", 6)
	hud.intro_bar.add_child(hud.intro_sub)
