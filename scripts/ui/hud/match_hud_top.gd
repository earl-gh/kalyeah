class_name MatchHudTop
extends RefCounted

## Empty space round the gear's art inside its button, as a share of the art's size on each side (0.3: the art is about 0.62 of the button).
const GEAR_PADDING: float = 0.3
## The owner's settings gear for the match (cropped to its visible art).
const GEAR: Texture2D = preload("res://assets/ui/match_gear.webp")

const WING_SLOTS: int = 5
const CHIP_W: float = 40.0
const CHIP_H: float = 52.0
const CHIP_GAP: float = 5.0
const WING_W: float = CHIP_W * float(WING_SLOTS) + CHIP_GAP * float(WING_SLOTS - 1) + 6.0
const WING_H: float = CHIP_H + 6.0
const WING_SCORE_GAP: float = 10.0


class SquadWing extends Control:
	const INK: Color = Color("141016")
	const SHADOW: Color = Color(0.08, 0.06, 0.09, 0.8)
	const PAPER: Color = Color("f5ebd6")
	const GOLD: Color = Color("ffbe2e")
	const COBALT: Color = Color("2b7bff")
	const CRIMSON: Color = Color("f23838")
	const HP_GREEN: Color = Color("2ee068")
	const TAG_GREY: Color = Color(0.36, 0.35, 0.4, 0.92)

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
			return TeamColors.score_own() if Settings.colorblind else COBALT
		return TeamColors.score_enemy() if Settings.colorblind else CRIMSON

	func _bar_color(share: float) -> Color:
		if not ally_side:
			return TeamColors.score_enemy() if Settings.colorblind else CRIMSON
		if Settings.colorblind:
			return TeamColors.BLIND_OWN
		if share <= 0.3:
			return CRIMSON
		if share <= 0.6:
			return GOLD
		return HP_GREEN

	func _draw() -> void:
		if _count <= 0:
			return
		var font: Font = KalyeahTheme.ACTION_FONT
		var accent: Color = _team_accent()
		var total_w: float = float(_count) * CHIP_W + float(maxi(_count - 1, 0)) * CHIP_GAP
		var start_x: float = (size.x - total_w - 3.0) if ally_side else 3.0

		for i: int in _count:
			var x: float = start_x + float(i) * (CHIP_W + CHIP_GAP)
			var y: float = 2.0
			var slant: float = 3.0 if ally_side else -3.0

			# Hard comic shadow + slanted backing plate
			var sh: PackedVector2Array = PackedVector2Array([
				Vector2(x + slant + 2.0, y + 3.0),
				Vector2(x + CHIP_W + slant + 2.0, y + 3.0),
				Vector2(x + CHIP_W + 2.0, y + CHIP_H + 3.0),
				Vector2(x + 2.0, y + CHIP_H + 3.0),
			])
			draw_colored_polygon(sh, SHADOW)

			var plate: PackedVector2Array = PackedVector2Array([
				Vector2(x + slant, y),
				Vector2(x + CHIP_W + slant, y),
				Vector2(x + CHIP_W, y + CHIP_H),
				Vector2(x, y + CHIP_H),
			])
			draw_colored_polygon(plate, TAG_GREY if _tagged[i] else PAPER)

			# Top team color header strip
			var top_h: float = 5.0
			var top_bar: PackedVector2Array = PackedVector2Array([
				Vector2(x + slant, y),
				Vector2(x + CHIP_W + slant, y),
				Vector2(x + CHIP_W + slant * 0.8, y + top_h),
				Vector2(x + slant * 0.8, y + top_h),
			])
			draw_colored_polygon(top_bar, accent.darkened(0.35) if _tagged[i] else accent)

			# Face art (`KidModel.minimap_face`) or komiks inked initial fallback
			var face_rect: Rect2 = Rect2(Vector2(x + 3.0, y + 6.0), Vector2(CHIP_W - 6.0, CHIP_W - 6.0))
			var tex: Texture2D = _faces[i]
			if tex != null:
				var tint: Color = Color(0.42, 0.42, 0.46, 0.85) if _tagged[i] else Color.WHITE
				draw_texture_rect(tex, face_rect, false, tint)
			else:
				draw_rect(face_rect, accent.darkened(0.45) if _tagged[i] else accent.lightened(0.15), true)
				var init_str: String = _initials[i]
				var iw: float = font.get_string_size(init_str, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.LABEL).x
				var ib: Vector2 = Vector2(face_rect.get_center().x - iw * 0.5, face_rect.get_center().y + 8.0)
				draw_string_outline(font, ib, init_str, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.LABEL, 4, INK)
				draw_string(font, ib, init_str, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.LABEL, PAPER)
			draw_rect(face_rect, INK, false, 1.6)

			# Bottom live HP bar
			var bar_rect: Rect2 = Rect2(Vector2(x + 3.0, y + CHIP_H - 9.0), Vector2(CHIP_W - 6.0, 6.0))
			draw_rect(bar_rect, INK, true)
			if not _tagged[i] and _hp_shares[i] > 0.01:
				var fill_w: float = (bar_rect.size.x - 2.0) * _hp_shares[i]
				draw_rect(Rect2(bar_rect.position + Vector2(1.0, 1.0), Vector2(fill_w, bar_rect.size.y - 2.0)), _bar_color(_hp_shares[i]), true)

			# Komiks ink border (barrio-gold highlight for local player)
			var border_col: Color = GOLD if _self_flag[i] else INK
			draw_polyline(PackedVector2Array([plate[0], plate[1], plate[2], plate[3], plate[0]]), border_col, 2.5, true)
			if _self_flag[i]:
				draw_polyline(PackedVector2Array([plate[0], plate[1], plate[2], plate[3], plate[0]]), INK, 1.2, true)

			# Greyed `TAG` stamp when tagged
			if _tagged[i]:
				var stamp_rect: Rect2 = Rect2(Vector2(x + 2.0, face_rect.get_center().y - 8.0), Vector2(CHIP_W - 4.0, 16.0))
				draw_rect(stamp_rect, INK, true)
				draw_rect(stamp_rect.grow(-1.2), CRIMSON.darkened(0.15), true)
				var tag_w: float = font.get_string_size("TAG", HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.WORLD_SMALL).x
				draw_string(font, Vector2(stamp_rect.get_center().x - tag_w * 0.5, stamp_rect.end.y - 2.5), "TAG", HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.WORLD_SMALL, PAPER)
			elif _ult_full[i]:
				# Gold star when `ult_charge >= 100`
				_draw_ult_star(Vector2(x + CHIP_W - 6.0, y + 7.0), 7.2)

	func _draw_ult_star(center: Vector2, r: float) -> void:
		var outer: PackedVector2Array = PackedVector2Array()
		var inner: PackedVector2Array = PackedVector2Array()
		for k: int in 10:
			var ang: float = -PI * 0.5 + TAU * float(k) / 10.0
			var rad_out: float = (r + 1.8) if k % 2 == 0 else (r * 0.45 + 1.4)
			var rad_in: float = r if k % 2 == 0 else r * 0.44
			outer.append(center + Vector2.from_angle(ang) * rad_out)
			inner.append(center + Vector2.from_angle(ang) * rad_in)
		draw_colored_polygon(outer, INK)
		draw_colored_polygon(inner, GOLD)


static func build(hud: MatchHud) -> void:
	hud.header = HeaderPanel.new()
	hud.header.set_header_height(MatchHud.HEADER_HEIGHT)
	hud.add_child(hud.header)
	hud.top = Control.new()
	hud._place(hud.top, Vector4(0, 0, 1, 0), Vector4(0, 0, 0, 520))
	hud.top.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud.add_child(hud.top)
	# left: the settings button
	hud.settings_button = TextureButton.new()
	var gear: AtlasTexture = AtlasTexture.new()
	gear.atlas = GEAR
	gear.region = Rect2(Vector2.ZERO, GEAR.get_size())
	var pad: Vector2 = gear.region.size * GEAR_PADDING
	gear.margin = Rect2(pad, pad * 2.0)
	hud.settings_button.texture_normal = gear
	hud.settings_button.ignore_texture_size = true
	hud.settings_button.stretch_mode = TextureButton.STRETCH_KEEP_ASPECT_CENTERED
	hud.settings_button.tooltip_text = "Match settings"
	hud.settings_button.accessibility_name = "Match settings"
	hud.settings_button.pressed.connect(hud.toggle_menu)
	hud.top.add_child(hud.settings_button)
	# 5v5 Squad Wings flanking the scoreboard
	hud.ally_wing = SquadWing.new(true)
	hud.ally_wing.visible = false
	hud.top.add_child(hud.ally_wing)
	hud.enemy_wing = SquadWing.new(false)
	hud.enemy_wing.visible = false
	hud.top.add_child(hud.enemy_wing)
	# the middle of the header: the score (points and the match clock)
	hud.scoreboard = Scoreboard.new()
	hud.scoreboard.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud.top.add_child(hud.scoreboard)
	# right: the match clock, the counterpart of the settings button
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


## Places the settings button at the top left, the clock between the two scores, 5v5 squad wings flanking the score in landscape,
## the tag toast under them, the capture warning above the tray and the opening objective sheet.
static func layout(hud: MatchHud) -> void:
	if hud.intro_bar != null:
		hud._place(hud.intro_bar, Vector4(0, 1, 1, 1), Vector4(80, -(hud.console_height() + 150.0), -80, -(hud.console_height() + 6.0)))
	if hud.tag_toast != null:
		var under: float = hud.header_height() + TagToast.GAP
		hud._place(hud.tag_toast, Vector4(0, 0, 1, 0), Vector4(0, under, 0, under + TagToast.HEIGHT))
	if hud.capture_alert != null:
		hud._place(hud.capture_alert, Vector4(0, 1, 1, 1), Vector4(0, -(hud.console_height() + TagToast.HEIGHT + TagToast.GAP), 0, -(hud.console_height() + TagToast.GAP)))
	hud._place(hud.scoreboard, Vector4(0.5, 0, 0.5, 0), Vector4(-MatchHud.SCORE_SIZE.x / 2.0, MatchHud.TOP_ROW_Y, MatchHud.SCORE_SIZE.x / 2.0, MatchHud.TOP_ROW_Y + MatchHud.SCORE_SIZE.y))
	var gear_box: Vector4 = Vector4(MatchHud.MARGIN, MatchHud.TOP_ROW_Y, MatchHud.MARGIN + MatchHud.GEAR_SIZE, MatchHud.TOP_ROW_Y + MatchHud.GEAR_SIZE)
	hud._place(hud.settings_button, Vector4(0, 0, 0, 0), gear_box)
	var middle: Rect2 = Scoreboard.middle_rect(MatchHud.SCORE_SIZE, MatchHud.CLOCK_GAP)
	var left: float = middle.position.x - MatchHud.SCORE_SIZE.x / 2.0
	hud._place(hud.clock_chip, Vector4(0.5, 0, 0.5, 0), Vector4(left, MatchHud.TOP_ROW_Y + middle.position.y, left + middle.size.x, MatchHud.TOP_ROW_Y + middle.position.y + middle.size.y))
	if hud.ally_wing != null and hud.enemy_wing != null:
		var half_score: float = (MatchHud.SCORE_SIZE.x * HudStyle.HEADER_SCALE) * 0.5
		var wing_y: float = MatchHud.TOP_ROW_Y + 1.0
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
