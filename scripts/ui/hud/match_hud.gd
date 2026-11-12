class_name MatchHud
extends Control
## Full in-match HUD built in code (no scene text to patch):
## - Landscape 1280x720 Deadlock-inspired (`image_1`) combat HUD (`BarangayMinimap` + compact bottom-right gear,
##   airy 5v5 Squad Wings, `WorldWaypoints`, delicate `Crosshair`, translucent `TakedownFeed`, waist-left vertical
##   `HeroVitalsPanel`, and minimalist pure-white `AbilityArc` hero & locomotion kit).
## - Preserves all portrait & street mini-game (`TumbangPreso`, `Patintero`, `LangitLupa`) properties, signals, and methods.

signal swap_pressed
signal again_pressed
signal exit_pressed
signal console_height_changed(height: float)
signal side_reserve_changed(width: float, left: bool)
signal header_height_changed(height: float)
signal primary_changed(held: bool)
signal dash_pressed
signal ultimate_pressed
signal jump_pressed
signal crouch_changed(held: bool)
signal slide_pressed

const BANNER_TIME: float = 2.0
const BANNER_FONT: int = UiMetrics.HERO
const BANNER_OUTLINE: int = roundi(BANNER_FONT * UiSkin.LETTER_OUTLINE_SHARE)
const BANNER_MARGIN: float = 40.0
const BANNER_POP_FROM: float = 1.5
const BANNER_POP_TIME: float = 0.28
const CAST_BLOCK_TAGS: Dictionary[int, String] = {
	StatusEffects.Type.STUN: "STUNNED",
	StatusEffects.Type.AIRBORNE: "AIRBORNE",
	StatusEffects.Type.BOUNCE: "BOUNCED",
	StatusEffects.Type.KNOCKOUT: "OUT",
	StatusEffects.Type.TRIPPED: "TRIPPED",
	StatusEffects.Type.POLYMORPH: "CAN",
}
const CAPTURING_TAG: String = "CAPTURING"
const RESCUING_TAG: String = "RESCUING"
const NO_TARGET_TAG: String = "NO TARGET"
const NO_TARGET_MS: int = 1200
const TOP_ROW_Y: float = 12.0
const HEADER_HEIGHT: float = TOP_ROW_Y * 2.0 + 88.0
const MARGIN: float = 14.0
const GEAR_SIZE: float = UiMetrics.TOUCH
const SCORE_SIZE: Vector2 = Vector2(380.0, 88.0)
const CLOCK_GAP: float = 4.0
const CAPTURE_COLOR: Color = Color(1.0, 0.95, 0.85)
const CAPTURE_LINGER: float = 0.4
const CONSOLE_HEIGHT: float = 255.0
const STREET_TRAY: float = CONSOLE_HEIGHT
const SCORE_COLUMN_GAP: float = 14.0
const SCORE_ROW_GAP: float = 12.0
const STREET_CHAT_SIZE: float = 84.0
const MAX_BOTTOM_INSET: float = 80.0

var overhead: OverheadHud
var _capture_linger: float = 0.0
var _capture_alerting: bool = false
var pulse: HurtPulse
var console: ConsolePanel
var chat: QuickChat
var chat_enabled: bool = true
var bottom_inset: float = 0.0
var top_inset: float = 0.0
var header: HeaderPanel
var top: Control
var scoreboard: Scoreboard
var clock_chip: ClockChip
var clock_flash: ClockStageFlash
var ally_wing: Control
var enemy_wing: Control
var minimap: BarangayMinimap
var waypoints: WorldWaypoints
var takedown_feed: TakedownFeed
var vitals_panel: HeroVitalsPanel
var sim_held: bool = false
var hand: CardHand
var street_pad: StreetGamePad
var score_column: ScoreColumn
var tag_toast: TagToast
var street_game: bool = false
var street: StreetTouch
var landscape: bool = false
var stick: MoveStick
var look: LookTouch
var crosshair: Crosshair
var shoulder: bool = false
var side_insets: Vector2 = Vector2.ZERO
var marker: MoveMarker
var tagged_panel: TaggedPanel
var intro_bar: Control
var intro_title: Label
var intro_sub: Label
var swap_button: Button
var again_button: Button
var point_result: PointResult
var _covered: bool = false
var results: MatchResults
var briefing: MatchBriefing
var results_mode: String = "Practice"
var capture_label: Label
var capture_alert: CaptureAlert
var settings_button: TextureButton
var menu: MatchMenu
var banner: Label

var feedback: MatchFeedback
var _no_target: Array[bool] = [false, false, false, false]
var _no_target_until: Array[int] = [0, 0, 0, 0]
var connection_blocked: bool = false

var _banner_tween: Tween
var _banner_left: float = 0.0
var _prev_primary_cd: float = 0.0
var _world_hud_synced: bool = false


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_build()
	Settings.accessibility_changed.connect(_on_accessibility_changed)
	Settings.controls_changed.connect(layout_controls)
	resized.connect(layout_controls)
	layout_controls()


func set_top_inset(pixels: float) -> void:
	top.position.y = pixels
	top_inset = pixels
	header.set_header_height(header_height())
	header_height_changed.emit(header_height())
	layout_controls()


func header_height() -> float:
	return HEADER_HEIGHT + top_inset


func set_bottom_inset(pixels: float) -> void:
	var inset: float = clampf(pixels, 0.0, MAX_BOTTOM_INSET)
	if is_equal_approx(inset, bottom_inset):
		return
	bottom_inset = inset
	layout_controls()


func set_chat_enabled(on: bool) -> void:
	chat_enabled = on
	chat.visible = on and hand.visible
	if not on:
		chat.set_open(false)


func console_height() -> float:
	if landscape and not street_game:
		return 0.0
	if street_game:
		return STREET_TRAY + bottom_inset
	return (CardHand.SIDES_TRAY if sides_tray() else CONSOLE_HEIGHT) + bottom_inset


func sides_tray() -> bool:
	return Settings.cards_tray == &"sides" and not street_game


func side_reserve() -> float:
	return CardHand.SIDES_COLUMN if sides_tray() else 0.0


func side_reserve_left() -> bool:
	return Settings.left_handed


func console_panel_height() -> float:
	if street_game:
		return console_height()
	if sides_tray() and hand != null and hand.visible:
		return console_height()
	return CONSOLE_HEIGHT + bottom_inset


func sync_plate_column() -> void:
	var shown: bool = hand != null and hand.visible
	var width: float = (CardHand.SIDES_COLUMN if sides_tray() else CardHand.COLUMN) if shown else 0.0
	console.set_column(width, Settings.left_handed if sides_tray() else not Settings.left_handed)


func set_controls_active(active: bool) -> void:
	active = active and not connection_blocked and not _covered and not (chat != null and chat.is_open())
	if not active:
		cancel_controls()
	hand.set_process_input(active)
	if stick != null:
		stick.set_process_input(active)
	if look != null:
		look.set_process_unhandled_input(active)
	street_pad.set_process_input(active)
	street.set_process_unhandled_input(active)


func cancel_controls() -> void:
	hand.cancel_hold()
	if stick != null:
		stick.cancel()
	if look != null:
		look.release()
	street_pad.cancel()
	street.release()


func playing_controls() -> bool:
	return street.active or (stick != null and stick.active)


func controls_active() -> bool:
	return hand.is_processing_input()


func show_banner(text: String) -> void:
	if _covered:
		return
	banner.text = text
	banner.add_theme_font_size_override("font_size", banner_font_size(text))
	banner.visible = true
	_banner_left = BANNER_TIME
	if is_instance_valid(_banner_tween):
		_banner_tween.kill()
	if Settings.reduced_motion:
		banner.scale = Vector2.ONE
		banner.modulate.a = 1.0
		return
	banner.pivot_offset = banner.size / 2.0
	banner.scale = Vector2.ONE * BANNER_POP_FROM
	banner.modulate.a = 0.0
	_banner_tween = create_tween().set_parallel(true)
	_banner_tween.tween_property(banner, "scale", Vector2.ONE, BANNER_POP_TIME).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	_banner_tween.tween_property(banner, "modulate:a", 1.0, 0.12)


func banner_font_size(text: String) -> int:
	var room: float = maxf(size.x - BANNER_MARGIN * 2.0 - BANNER_OUTLINE * 2.0, 1.0)
	var width: float = KalyeahTheme.DISPLAY_FONT.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, BANNER_FONT).x
	if width <= room:
		return BANNER_FONT
	return int(floorf(float(BANNER_FONT) * room / width))


func banner_text() -> String:
	return banner.text if banner.visible else ""


## Section 7 contract: projects 3D objective waypoints and updates the Barangay radar + hero vitals.
func sync_world_hud(sim: MatchSim, camera: Camera3D, player: PlayerState, local_team: int, cam_yaw_deg: float) -> void:
	if street_game or sim == null or player == null:
		return
	_world_hud_synced = true
	if minimap != null and landscape:
		minimap.sync_radar(sim, player, local_team, cam_yaw_deg)
	if waypoints != null and landscape:
		waypoints.sync_waypoints(sim, camera, player, local_team)
	if vitals_panel != null and landscape:
		vitals_panel.sync_vitals(sim, player, 0.0)
	if crosshair != null:
		crosshair.sprinting = sim.phase == MatchSim.Phase.PLAYING and _is_running_fast(player)


func _is_running_fast(player: PlayerState) -> bool:
	if player == null or player.tagged:
		return false
	if player.sprinting:
		return true
	return not player.walking and not player.crouching and not player.overclocked and player.run_time > 0.02


## Section 7 contract: flashes the crosshair hitmarker (crimson on hit, blazing gold + crimson on lethal tag).
func notify_hit(lethal: bool = false) -> void:
	if crosshair != null:
		crosshair.flash_hit(lethal)
		crosshair.kick_recoil(1.0 if lethal else 0.55)


## Section 7 contract: pushes a 90s komiks takedown callout strip onto TakedownFeed.
func notify_takedown(tagger_name: String, victim_name: String, ally_scored: bool, detail: String = "") -> void:
	if takedown_feed != null:
		takedown_feed.push_tag(tagger_name, victim_name, ally_scored, detail)


## Section 7 contract: pushes a 90s komiks rescue callout strip onto TakedownFeed.
func notify_rescue(rescuer_name: String, saved_name: String, ours: bool) -> void:
	if takedown_feed != null:
		takedown_feed.push_rescue(rescuer_name, saved_name, ours)


## The hand, the street taps and the hint for the local player.
func sync_player(sim: MatchSim, player: PlayerState, pick_open: bool) -> void:
	tagged_panel.sync(sim, player, pick_open)
	var playing: bool = not player.tagged and sim.phase == MatchSim.Phase.PLAYING and not pick_open and not connection_blocked
	var shown: bool = playing or (sim.phase == MatchSim.Phase.INTRO and not player.tagged and not pick_open and not connection_blocked)
	if street_game:
		_sync_street_pad(sim, player, shown, playing)
		return
	var shown_before: bool = hand.visible
	hand.visible = shown
	chat.visible = shown and chat_enabled
	if shown != shown_before and sides_tray():
		layout_controls()
	sync_plate_column()
	street.active = playing and not landscape
	if stick != null:
		stick.active = playing
		stick.visible = shown
		if not playing:
			stick.cancel()
	if look != null:
		look.active = playing and shoulder
		if not look.active:
			look.release()
	if crosshair != null:
		crosshair.visible = shown
		crosshair.held = hand.holding()
		crosshair.sprinting = playing and _is_running_fast(player)
		if playing and player.primary_cd > _prev_primary_cd + 0.02:
			crosshair.kick_recoil(0.38)
	_prev_primary_cd = player.primary_cd
	if vitals_panel != null:
		var hud_hidden: bool = _covered or briefing.running
		vitals_panel.visible = landscape and shown and not hud_hidden
		if vitals_panel.visible:
			vitals_panel.sync_vitals(sim, player, get_process_delta_time())
	if minimap != null and landscape and not _world_hud_synced:
		minimap.sync_radar(sim, player, player.team, 0.0)
	if not playing or landscape:
		street.release()
	for slot: int in _no_target.size():
		_no_target[slot] = playing and sim.lacks_target(player, slot)
	var notices: Array[String] = []
	for slot: int in _no_target.size():
		notices.append(_notice(slot) if playing else "")
	hand.show_state(sim, player, playing, _block_tag(sim, player) if playing else "", notices)
	if hand is AbilityArc:
		var stamina_on: bool = sim.rules.stamina_enabled and player.has_stamina and not player.tagged
		var stam_look: Array = overhead.stamina_fill(player) if stamina_on else [1.0, OverheadHud.STAMINA_COLOR]
		(hand as AbilityArc).show_stamina(stamina_on, stam_look[0] as float, stam_look[1] as Color)
	pulse.sync_health(player.hp, sim.rules.player_max_hp, playing)


func set_street_game(on: bool) -> void:
	street_game = on
	hand.visible = not on
	street_pad.visible = on
	console.visible = not on and not landscape
	if stick != null:
		stick.visible = not on
		stick.active = stick.active and not on
	for widget: Control in [minimap, waypoints, takedown_feed, vitals_panel, ally_wing, enemy_wing]:
		if widget != null:
			widget.visible = not on and landscape
	layout_controls()


func setup_street_scores(sim: MatchSim, local_id: int) -> void:
	score_column = ScoreColumn.new()
	add_child(score_column)
	move_child(briefing, -1)
	score_column.setup(sim, local_id)
	layout_controls()


func _sync_street_pad(sim: MatchSim, player: PlayerState, shown: bool, playing: bool) -> void:
	hand.visible = false
	street.active = false
	if not playing:
		street.release()
	var game: TumbangPreso = sim.mode as TumbangPreso
	var lines: Patintero = sim.mode as Patintero
	var langit: LangitLupa = sim.mode as LangitLupa
	var button: StreetGamePad.Action = StreetGamePad.Action.NONE
	var fill: float = 0.0
	var can_act: bool = false
	if playing and game != null and game.is_attacker(player):
		fill = game.charge(player.id)
		if game.can_throw(player):
			button = StreetGamePad.Action.THROW
		elif not game.has_slipper(player.id):
			button = StreetGamePad.Action.KICK
			can_act = game.can_kick(player)
	elif playing and lines != null and not lines.is_jailed(player.id):
		button = StreetGamePad.Action.DIVE
		fill = lines.dive_ready_share(player.id)
		can_act = lines.can_dive(player)
	elif playing and langit != null and langit.is_attacker(player):
		button = StreetGamePad.Action.LUPA
		fill = langit.mound_ready_share(player.id)
		can_act = langit.can_raise(player)
	var on_sidewalk: bool = lines != null and lines.is_jailed(player.id)
	var status: Array[String] = PracticeMatchPresentation.street_status(sim, player)
	chat.visible = shown and chat_enabled
	street_pad.visible = shown and not player.tagged
	street_pad.show_state(status[0], status[1], button, fill, can_act, on_sidewalk)
	if score_column != null:
		score_column.sync(sim)
	sync_plate_column()
	pulse.sync_health(player.hp, sim.rules.player_max_hp, playing)


func sync_hold_hint(player: PlayerState, playing: bool, release: MatchInput.Release, nobody: bool) -> void:
	feedback.sync_hint(player, playing, release, nobody)


func note_released(slot: int, cancelled: bool) -> void:
	if slot >= 0 and slot < _no_target.size() and not cancelled and _no_target[slot]:
		_no_target_until[slot] = Time.get_ticks_msec() + NO_TARGET_MS


func _notice(slot: int) -> String:
	return NO_TARGET_TAG if Time.get_ticks_msec() < _no_target_until[slot] else ""


func _block_tag(sim: MatchSim, player: PlayerState) -> String:
	if sim.casting_blocked(player):
		return CAPTURING_TAG if sim.layout.in_base(-sim.side_for_team(player.team), player.position) else RESCUING_TAG
	if player.effects.can_cast():
		return ""
	for type: int in CAST_BLOCK_TAGS:
		if player.effects.has(type as StatusEffects.Type):
			return CAST_BLOCK_TAGS[type] as String
	return ""


## Top row: the score card, 5v5 squad wings, takedown feed, capture countdown, and banners.
func sync_score(sim: MatchSim, local_team: int, delta: float, local_id: int = -1) -> void:
	scoreboard.show_score(sim.score, local_team, sim.match_time)
	scoreboard.show_capture(sim, local_team)
	_sync_clock(sim, delta)
	_sync_capture(sim, local_team, delta)
	if takedown_feed != null:
		takedown_feed.tick(delta)
	again_button.visible = sim.phase == MatchSim.Phase.MATCH_OVER
	point_result.sync(sim, local_team)
	results.set_over(sim.phase == MatchSim.Phase.MATCH_OVER, sim, local_id, local_team, results_mode)
	var covered: bool = sim.phase in [MatchSim.Phase.POINT_FREEZE, MatchSim.Phase.MATCH_OVER]
	if covered:
		chat.set_open(false)
		if menu.visible:
			menu.close()
	var hidden: bool = covered or briefing.running
	for item: CanvasItem in [header, top, scoreboard, marker, pulse, console, feedback]:
		item.visible = not hidden
	MatchHudTop.sync_wings(self, sim, local_team, local_id, not hidden)
	var show_combat_hud: bool = landscape and not street_game and not hidden
	if minimap != null:
		minimap.visible = show_combat_hud
	if waypoints != null:
		waypoints.visible = show_combat_hud
	if takedown_feed != null:
		takedown_feed.visible = show_combat_hud
	if score_column != null:
		score_column.visible = not hidden
	if landscape:
		console.visible = false
	if street_game:
		console.visible = false
		scoreboard.visible = not hidden and not sim.mode.is_free_for_all()
	overhead.visible = not hidden
	if hidden:
		for item: CanvasItem in [hand, street_pad, chat, tagged_panel, intro_bar, capture_label, capture_alert, banner]:
			item.hide()
		if stick != null:
			stick.hide()
		if vitals_panel != null:
			vitals_panel.hide()
		clock_flash.cancel()
	if covered:
		briefing.hide()
	if _covered != covered:
		_covered = covered
		set_controls_active(not covered and not menu.visible and not briefing.running)
	if sim.phase != MatchSim.Phase.MATCH_OVER and _banner_left > 0.0:
		_banner_left -= delta
		if _banner_left <= 0.0:
			banner.visible = false


func _sync_clock(sim: MatchSim, delta: float) -> void:
	var counting: bool = sim.counts_down()
	var stage: MatchSim.ClockStage = sim.clock_stage()
	clock_chip.show_time(sim.clock_left() if counting else sim.match_time, stage, counting)
	if not counting:
		clock_flash.cancel()
		return
	var caption_at: Vector2 = clock_flash.get_global_transform().affine_inverse() * (clock_chip.get_global_transform() * clock_chip.caption_center())
	var into: float = sim.round_time - sim.clock_stage_start(stage)
	clock_flash.sync(stage, into, 0.0 if sim_held else delta, caption_at, clock_chip.caption_color())
	clock_chip.hide_caption(clock_flash.flying())


func _sync_capture(sim: MatchSim, local_team: int, delta: float) -> void:
	var running: bool = sim.capture_team >= 0 and sim.phase == MatchSim.Phase.PLAYING
	var ours: bool = sim.capture_team == local_team
	var alert: bool = running and not ours and sim.capture_kind == MatchSim.CaptureKind.BASE
	if running:
		_capture_linger = CAPTURE_LINGER
		_capture_alerting = alert
	elif sim.phase == MatchSim.Phase.PLAYING and _capture_linger > 0.0:
		_capture_linger -= delta
		running = _capture_linger > 0.0
		alert = running and _capture_alerting
	else:
		_capture_linger = 0.0
	capture_alert.show_alert(alert)
	capture_label.visible = running and not alert
	if not capture_label.visible or sim.capture_team < 0:
		return
	var seconds: int = ceili(sim.capture_time_left())
	var how: String
	if sim.capture_kind == MatchSim.CaptureKind.ALL_TAGGED:
		how = "ALL TAGGED!" if ours else "ALL OF YOU TAGGED!"
	else:
		how = "HOLD THE BASE!"
	capture_label.text = "%s\n%d" % [how, seconds]
	capture_label.add_theme_color_override("font_color", CAPTURE_COLOR.lerp(TeamColors.feed_own() if ours else TeamColors.feed_enemy(), 0.35))


# ---- building ------------------------------------------------------------------

func _build() -> void:
	overhead = OverheadHud.new()
	overhead.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_place(overhead, Vector4(0, 0, 1, 1), Vector4.ZERO)
	add_child(overhead)
	marker = MoveMarker.new()
	marker.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(marker)
	move_child(marker, 0)
	waypoints = WorldWaypoints.new()
	add_child(waypoints)
	pulse = HurtPulse.new()
	add_child(pulse)
	console = ConsolePanel.new()
	console.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(console)
	MatchHudTop.build(self)
	capture_alert = CaptureAlert.new()
	add_child(capture_alert)
	landscape = Settings.landscape
	minimap = BarangayMinimap.new()
	minimap.visible = landscape and not street_game
	add_child(minimap)
	move_child(minimap, top.get_index())
	takedown_feed = TakedownFeed.new()
	takedown_feed.visible = landscape and not street_game
	add_child(takedown_feed)
	vitals_panel = HeroVitalsPanel.new()
	vitals_panel.visible = landscape and not street_game
	add_child(vitals_panel)
	if landscape:
		stick = MoveStick.new()
		add_child(stick)
		look = LookTouch.new()
		add_child(look)
	hand = AbilityArc.new() if landscape else CardHand.new()
	add_child(hand)
	if hand is AbilityArc:
		var arc: AbilityArc = hand as AbilityArc
		arc.primary_changed.connect(primary_changed.emit)
		arc.dash_pressed.connect(dash_pressed.emit)
		arc.ultimate_pressed.connect(ultimate_pressed.emit)
		arc.jump_pressed.connect(jump_pressed.emit)
		arc.crouch_changed.connect(crouch_changed.emit)
		arc.slide_pressed.connect(slide_pressed.emit)
	street_pad = StreetGamePad.new()
	add_child(street_pad)
	chat = QuickChat.new()
	add_child(chat)
	chat.opened_changed.connect(func(opened: bool) -> void:
		set_controls_active(not opened and not menu.visible))
	street = StreetTouch.new()
	add_child(street)
	banner = Label.new()
	_place(banner, Vector4(0, 0.3, 1, 0.3), Vector4(0, -60, 0, 60))
	banner.visible = false
	UiKit.letter(banner, BANNER_FONT, RoyaleStyle.GOLD)
	banner.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	banner.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	banner.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(banner)
	tag_toast = TagToast.new()
	add_child(tag_toast)
	clock_flash = ClockStageFlash.new()
	add_child(clock_flash)
	capture_label = _pill_label("", UiMetrics.TITLE, Vector4(0, 0.4, 1, 0.4), Vector4(120, -56, -120, 56))
	capture_label.theme_type_variation = &"DisplayLabel"
	capture_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	capture_label.add_theme_color_override("font_outline_color", Color(0.08, 0.05, 0.1))
	capture_label.add_theme_constant_override("outline_size", 10)
	again_button = _button("Battle again!", UiMetrics.BODY, Vector4(0.5, 0.5, 0.5, 0.5), Vector4(-160, -30, 160, 60))
	KalyeahTheme.style_button(again_button, "orange")
	again_button.pressed.connect(again_pressed.emit)
	again_button.visible = false
	point_result = PointResult.new()
	add_child(point_result)
	results = MatchResults.new()
	add_child(results)
	results.adopt(again_button, true)
	tagged_panel = TaggedPanel.new()
	add_child(tagged_panel)
	swap_button = tagged_panel.swap_button
	tagged_panel.swap_pressed.connect(swap_pressed.emit)
	MatchHudTop.build_intro(self)
	briefing = MatchBriefing.new(self)
	add_child(briefing)
	move_child(chat, briefing.get_index())
	menu = MatchMenu.new()
	add_child(menu)
	menu.exit_pressed.connect(exit_pressed.emit)
	menu.closed.connect(set_controls_active.bind(true))
	feedback = MatchFeedback.new()
	add_child(feedback)
	feedback.return_home.connect(exit_pressed.emit)


func set_shoulder(on: bool) -> void:
	shoulder = on and landscape
	if shoulder and crosshair == null:
		crosshair = Crosshair.new()
		add_child(crosshair)
		move_child(crosshair, waypoints.get_index() + 1)
	if hand is AbilityArc:
		(hand as AbilityArc).shoulder = shoulder
	layout_controls()


func set_side_insets(pixels: Vector2) -> void:
	if pixels == side_insets:
		return
	side_insets = pixels
	layout_controls()


func layout_controls() -> void:
	MatchHudLayout.apply(self)


func show_intro(title: String, subtitle: String = "") -> void:
	intro_bar.visible = not title.is_empty()
	intro_title.text = title
	intro_sub.text = subtitle


func toggle_menu() -> void:
	if menu.visible:
		menu.close()
		return
	set_controls_active(false)
	menu.open()


func _place(control: Control, anchors: Vector4, offsets: Vector4) -> void:
	control.anchor_left = anchors.x
	control.anchor_top = anchors.y
	control.anchor_right = anchors.z
	control.anchor_bottom = anchors.w
	control.offset_left = offsets.x
	control.offset_top = offsets.y
	control.offset_right = offsets.z
	control.offset_bottom = offsets.w


func _pill_label(text: String, font_size: int, anchors: Vector4, offsets: Vector4) -> Label:
	var label: Label = Label.new()
	_place(label, anchors, offsets)
	label.theme_type_variation = &"HudPill"
	label.text = text
	label.visible = false
	label.add_theme_font_size_override("font_size", font_size)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(label)
	return label


func _button(text: String, font_size: int, anchors: Vector4, offsets: Vector4, parent: Control = null) -> Button:
	var button: Button = Button.new()
	_place(button, anchors, offsets)
	button.text = text
	button.add_theme_font_size_override("font_size", font_size)
	(parent if parent != null else self).add_child(button)
	return button


func set_connection(text: String, terminal: bool = false) -> void:
	connection_blocked = not text.is_empty()
	feedback.show_connection(text, terminal)
	if connection_blocked:
		set_controls_active(false)


func _on_accessibility_changed() -> void:
	if Settings.reduced_motion:
		if is_instance_valid(_banner_tween):
			_banner_tween.kill()
		banner.scale = Vector2.ONE
		banner.modulate.a = 1.0
