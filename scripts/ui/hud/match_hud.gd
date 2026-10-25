class_name MatchHud
extends Control
## The whole in-match HUD, built in code (no scene text to patch): overhead bars,
## top row (the settings button on the left, the score on the right; the whole street is
## always on screen, so there is no minimap), the TAGGED!/RESCUED! feed under the settings button,
## the capture countdown, the settings menu, and the controls: the movement stick
## at the bottom centre with weapon 1 on its left and weapon 2 on its right, all in
## one row within thumb reach. The score card (points and the match clock) is at the top right.
## The match screen feeds it sim state through `sync_player()` and `sync_score()`.

signal swap_pressed
signal again_pressed
signal hurt_pressed
## Practice: play the animation of status `type` (stunned, airborne, knocked down) on yourself.
signal status_test_pressed(type: int)
## A card in the tagged panel was tapped: follow player `id`.
signal exit_pressed
## The console's height changed (the bottom inset did): the camera keeps this much of the screen clear.
signal console_height_changed(height: float)
## The header's height (with the top inset) changed: the camera keeps this much of the screen clear at the top.
signal header_height_changed(height: float)

enum Feed { TAGGED, RESCUED }

const BANNER_TIME: float = 2.0
## GO!, SCORED and VICTORY / DEFEAT on the street (visual pass: were 44 px with a 12 px outline): big gold words in a thick navy
## outline with a drop shadow; a long one is set smaller to keep `BANNER_MARGIN` from the screen's sides. They pop in from
## `BANNER_POP_FROM` times their size over `BANNER_POP_TIME` (not with Reduce motion).
const BANNER_FONT: int = 72
const BANNER_OUTLINE: int = 18
const BANNER_SHADOW: Vector2 = Vector2(0.0, 6.0)
const BANNER_MARGIN: float = 40.0
const BANNER_POP_FROM: float = 1.5
const BANNER_POP_TIME: float = 0.28
## The short word on a weapon button for each status that stops casting.
const CAST_BLOCK_TAGS: Dictionary[int, String] = {
	StatusEffects.Type.STUN: "STUNNED",
	StatusEffects.Type.AIRBORNE: "AIRBORNE",
	StatusEffects.Type.BOUNCE: "BOUNCED",
	StatusEffects.Type.KNOCKOUT: "OUT",
	StatusEffects.Type.POLYMORPH: "CAN",
}
## Let go of a weapon with nobody to throw at in range: nothing is thrown, the weapon stays, and its button says why for this long.
const NO_TARGET_TAG: String = "NO TARGET"
const NO_TARGET_MS: int = 1200
## The header at the top of the screen (floating over the street, like Clash Royale): the settings button, the score and the clock sit in this row.
const TOP_ROW_Y: float = 12.0
const HEADER_HEIGHT: float = TOP_ROW_Y * 2.0 + 88.0
const MARGIN: float = 14.0
const GEAR_SIZE: float = UiMetrics.TOUCH
const SCORE_SIZE: Vector2 = Vector2(330.0, 88.0)
const CLOCK_SIZE: Vector2 = Vector2(152.0, 88.0)
const FEED_TIME: float = 2.6
const FEED_SIZE: Vector2 = Vector2(250.0, 46.0)
const FEED_MAX: int = 3
## The feed starts this far under the header: just below your team's tagged box, which is in the top left corner of the street.
const FEED_DROP: float = 130.0
const CAPTURE_COLOR: Color = Color(1.0, 0.95, 0.85)
## Controls (owner), as (distance from their screen edge, distance from the bottom) of each
## control's centre on the 720x1280 base: the movement stick at the bottom left, the two skill
## buttons at the bottom right, all in one row within thumb reach.
## The controller deck at the bottom of the screen: the arena sits above it. The camera reserves the same height.
const CONSOLE_HEIGHT: float = 294.0
const ROW_HEIGHT: float = CONSOLE_HEIGHT / 2.0
## The most the bottom inset can add (a tall gesture bar), so the console never eats the street.
const MAX_BOTTOM_INSET: float = 80.0
## How long the tick is when the thumb moves to another direction of the pad.
const PAD_TICK_MS: int = 8
## The square the D-pad sits in: its disc plus a margin for the thumb.
const DPAD_ZONE: float = (DPad.MAX_RADIUS + 20.0) * 2.0
## The D-pad's centre is this far from its screen edge (the left, or the right for a left-handed player).
const DPAD_MARGIN: float = 170.0
## The skill buttons: big enough for a thumb (Apple's 44 pt is 84 px), a gap apart, the
## second against the right margin and the first to its left.
const WEAPON_SIZE: float = 124.0
## Staggered like the face buttons of a controller: weapon 2 (the nearer the edge) a little higher, weapon 1 beside it a little lower.
const WEAPON_GAP: float = 20.0
const WEAPON_STAGGER: float = 24.0
const WEAPON_MARGIN: float = 34.0
## The round sockets in the shell around the D-pad and each skill button.
const DPAD_SOCKET: float = 120.0
const WEAPON_SOCKET: float = 74.0
## The cancel zone sits above the skill buttons (drag a skill onto it to cancel the cast); this is its height
## from the bottom (its distance from the edge follows the buttons).
## Cards mode: the hand cards (upright, like a Clash Royale card), how far they sit from the screen edge and the tray's top, the shuffler's
## window beside them, and the health bar along the bottom of the tray.
const CARD_SIZE: Vector2 = Vector2(132.0, 178.0)
const CARD_GAP: float = 14.0
const CARD_MARGIN: float = 22.0
const CARD_TOP: float = 20.0
const SHUFFLER_SIZE: Vector2 = Vector2(92.0, 124.0)
const SHUFFLER_LIFT: float = 0.0
const CARD_PAD_LIFT: float = 24.0
const CARD_PAD_SCALE: float = 0.92
const BAR_HEIGHT: float = 36.0
const BAR_BOTTOM: float = 22.0
const BAR_MARGIN: float = 26.0
const CANCEL_AT: Vector2 = Vector2(0.0, CONSOLE_HEIGHT + 78.0)
const CANCEL_SIZE: Vector2 = Vector2(110.0, 100.0)
## Cards mode: the cancel mark sits up and inward from the inner card, this far from any card's touch area (a full aim from anywhere on a
## card ends `CANCEL_MARGIN` short of it, so only a drag past the aim ring on purpose reaches it).
const CANCEL_MARGIN: float = 24.0
const CANCEL_CLEARANCE: float = AimButton.DRAG_RADIUS + CANCEL_MARGIN

var overhead: OverheadHud
## The red glow round the screen edge when you are hit (under the controls).
var pulse: HurtPulse
## The controller deck at the bottom (it only draws).
var console: ConsolePanel
## UI pixels of the screen's bottom edge the system keeps (home bar), set by the match screen.
var bottom_inset: float = 0.0
## UI pixels the notch or status bar keeps at the top: the header grows by it.
var top_inset: float = 0.0
var header: HeaderPanel
var top: Control
var scoreboard: Scoreboard
## The match clock at the right of the header.
var clock_chip: ClockChip
var dpad: DPad
var weapon_buttons: Array[AimButton] = []
## Cards mode (GameRules.card_cycle), set by the match screen before the first layout: the hand of two cards, the shuffler and the health bar instead of two round buttons.
var card_mode: bool = false
var shuffler: ShufflerView
## Weapon 1, weapon 2 (the order MatchInput uses).
var aim_buttons: Array[AimButton] = []
var cancel_zone: CancelMark
var tagged_panel: TaggedPanel
## The opening show's banner along the bottom ("TAG THEIR BASE!").
var intro_bar: Control
var intro_title: Label
var intro_sub: Label
var swap_button: Button
var hurt_button: Button
var again_button: Button
var results: MatchResults
var coach: CoachMarks
## What the results card says the match was ("Practice", "Hotspot", "Online").
var results_mode: String = "Practice"
## Big countdown in the middle while a capture is running.
var capture_label: Label
## TAGGED! / RESCUED! pills under the settings button (newest at the bottom).
var feed_box: VBoxContainer
var settings_button: TextureButton
var menu: MatchMenu
var banner: Label

var feedback: MatchFeedback
## Per weapon slot: letting go now would throw nothing (nobody in range); until when the button says so after a release.
var _no_target: Array[bool] = [false, false]
var _no_target_until: Array[int] = [0, 0]
var connection_blocked: bool = false

var _banner_tween: Tween

var _banner_left: float = 0.0


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_build()
	Settings.accessibility_changed.connect(_on_accessibility_changed)
	Settings.controls_changed.connect(layout_controls)
	resized.connect(layout_controls)
	layout_controls()


## Pixels the top row is pushed down to clear a notch or status bar.
## A short tick each time the thumb enters a new direction (phones only; off with the vibration setting).
func _on_pad_direction(direction: Vector2) -> void:
	if direction != Vector2.ZERO:
		Settings.vibrate(PAD_TICK_MS)


func set_top_inset(pixels: float) -> void:
	top.position.y = pixels
	top_inset = pixels
	header.set_header_height(header_height())
	header_height_changed.emit(header_height())


## The header's height now: its own plus the notch's inset.
func header_height() -> float:
	return HEADER_HEIGHT + top_inset


## The home bar or gesture area under the screen (UI pixels): the console grows by it and the controls ride above it,
## so a thumb that rests on the bar never lands on a button, and the camera keeps the taller console clear.
func set_bottom_inset(pixels: float) -> void:
	var inset: float = clampf(pixels, 0.0, MAX_BOTTOM_INSET)
	if is_equal_approx(inset, bottom_inset):
		return
	bottom_inset = inset
	layout_controls()


## Switches the bottom between the controller deck and the card tray (GameRules.card_cycle) and lays it out again.
func set_card_mode(on: bool) -> void:
	card_mode = on
	layout_controls()


## The console's height now: its own plus the bottom inset.
func console_height() -> float:
	return CONSOLE_HEIGHT + bottom_inset


func set_controls_active(active: bool) -> void:
	active = active and not connection_blocked
	if not active:
		cancel_controls()
	dpad.set_process_input(active)
	for control: Control in aim_buttons:
		control.set_process_input(active)


## Clears every live gesture while leaving input processing in its current state.
func cancel_controls() -> void:
	dpad.release()
	for button: AimButton in aim_buttons:
		button.cancel_aim()


func controls_active() -> bool:
	return weapon_buttons[0].is_processing_input()


func show_banner(text: String) -> void:
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
	# pop in: starts big and transparent, settles with a little overshoot
	banner.pivot_offset = banner.size / 2.0
	banner.scale = Vector2.ONE * BANNER_POP_FROM
	banner.modulate.a = 0.0
	_banner_tween = create_tween().set_parallel(true)
	_banner_tween.tween_property(banner, "scale", Vector2.ONE, BANNER_POP_TIME).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	_banner_tween.tween_property(banner, "modulate:a", 1.0, 0.12)


## The banner's font size for `text`: `BANNER_FONT`, or smaller so the words keep `BANNER_MARGIN` from both sides.
func banner_font_size(text: String) -> int:
	var room: float = maxf(size.x - BANNER_MARGIN * 2.0 - BANNER_OUTLINE * 2.0, 1.0)
	var width: float = KalyeahTheme.DISPLAY_FONT.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, BANNER_FONT).x
	if width <= room:
		return BANNER_FONT
	return int(floorf(float(BANNER_FONT) * room / width))


func banner_text() -> String:
	return banner.text if banner.visible else ""


## Buttons, labels and cooldowns for the local player.
func sync_player(sim: MatchSim, player: PlayerState, pick_open: bool) -> void:
	tagged_panel.sync(sim, player, pick_open)
	# while you wait in the box the controls are hidden so the card sits alone at the bottom
	# no buttons while you wait in the box or during the opening show
	var playing: bool = not player.tagged and sim.phase == MatchSim.Phase.PLAYING and not pick_open and not connection_blocked
	# during the opening show the controls and the first hand are on the deck, dimmed and locked, instead of an empty deck
	var shown: bool = playing or (sim.phase == MatchSim.Phase.INTRO and not player.tagged and not pick_open and not connection_blocked)
	for button: AimButton in weapon_buttons:
		button.visible = shown
	dpad.visible = shown
	if not playing:
		dpad.release()
	# disabled while on cooldown too: no pressing or precasting until ready
	var weapons_off: bool = not playing or not player.effects.can_cast()
	for slot: int in _no_target.size():
		_no_target[slot] = playing and sim.lacks_target(player, slot)
	feedback.sync_hint(player, aim_buttons, playing, _no_target)
	pulse.sync_health(player.hp, sim.rules.player_max_hp, playing)
	console.set_health(player.hp, sim.rules.player_max_hp, playing)
	console.set_wells_shown(shown)
	shuffler.show_state(sim, player)
	shuffler.visible = shown and sim.rules.card_cycle and player.cards

	for slot: int in weapon_buttons.size():
		var button: AimButton = weapon_buttons[slot]
		if slot >= player.weapons.size():
			button.set_locked(true)
			continue
		if sim.rules.card_cycle:
			button.card_style = true
			var card: WeaponDef = sim.weapon_defs.get(player.weapons[slot]) as WeaponDef
			var old: WeaponDef = button.weapon_def
			if card != old and old != null and button.visible and (player.shuffle_card == old.id or player.incoming.has(old.id)):
				# the card you used flies from its slot to the shuffler
				CardFlight.fly(self, old, button.get_rect(), Rect2(shuffler.position + shuffler.card_rect().position, shuffler.card_rect().size))
			button.set_card(card)
			button.set_waiting(1.0 - clampf(player.shuffle_left / maxf(player.shuffle_total, 0.001), 0.0, 1.0), ceili(player.shuffle_left))
			var in_hand: bool = card != null
			button.accessibility_name = "Card %d: %s" % [slot + 1, card.display_name if in_hand else "waiting for the shuffler"]
			var card_words: String = "Hold and drag to aim, let go to throw" if in_hand else "Ready in %d seconds" % ceili(player.shuffle_left)
			if button.accessibility_description != card_words:
				button.accessibility_description = card_words
			button.blocked_text = _block_tag(player) if (playing and in_hand) else ""
			button.notice_text = _notice(slot) if (playing and in_hand) else ""
			button.set_locked(weapons_off or not in_hand)
			continue
		var def: WeaponDef = sim.weapon_defs[player.weapons[slot]]
		if button.label_text != def.short_name or button.sub_text != def.kind_label():
			button.label_text = def.short_name
			button.sub_text = def.kind_label()
			button.icon_id = def.id
			button.sub_color = TeamColors.kind_color(def.kind)
			button.queue_redraw()
			button.accessibility_name = "Weapon %d: %s, %s" % [slot + 1, def.display_name, def.kind_label()]
		var cooldown_words: String = "Hold and drag to aim, let go to throw" if player.weapon_cooldowns[slot] <= 0.0 else "Ready in %d seconds" % ceili(player.weapon_cooldowns[slot])
		if button.accessibility_description != cooldown_words:
			button.accessibility_description = cooldown_words
		button.blocked_text = _block_tag(player) if playing else ""
		button.notice_text = _notice(slot) if playing else ""
		button.set_locked(weapons_off or player.weapon_cooldowns[slot] > 0.0)
		button.set_cooldown(player.weapon_cooldowns[slot], def.cooldown)


## A weapon let go with nobody in range threw nothing: say so on its button for a moment.
func _on_aim_released(_aim: Vector2, cancelled: bool, slot: int) -> void:
	if not cancelled and _no_target[slot]:
		_no_target_until[slot] = Time.get_ticks_msec() + NO_TARGET_MS


func _notice(slot: int) -> String:
	return NO_TARGET_TAG if Time.get_ticks_msec() < _no_target_until[slot] else ""


## What to print on a weapon button while a status stops you casting ("" when nothing does).
func _block_tag(player: PlayerState) -> String:
	if player.effects.can_cast():
		return ""
	for type: int in CAST_BLOCK_TAGS:
		if player.effects.has(type as StatusEffects.Type):
			return CAST_BLOCK_TAGS[type] as String
	return ""


## Top row: the score card; the capture countdown; banners and the feed fade out.
func sync_score(sim: MatchSim, local_team: int, delta: float, local_id: int = -1) -> void:
	scoreboard.show_score(sim.score, local_team, sim.match_time)
	scoreboard.show_capture(sim, local_team)
	scoreboard.show_roster(sim, local_team)
	clock_chip.show_time(sim.match_time, sim.in_overtime())
	MatchHudTop.place_feed(self, overhead.box_clear_y())
	_sync_capture(sim, local_team)
	again_button.visible = sim.phase == MatchSim.Phase.MATCH_OVER
	results.set_over(sim.phase == MatchSim.Phase.MATCH_OVER, sim, local_id, local_team, results_mode)
	if sim.phase != MatchSim.Phase.MATCH_OVER and _banner_left > 0.0:
		_banner_left -= delta
		if _banner_left <= 0.0:
			banner.visible = false
	_tick_feed(delta)


## "CAPTURING 3" for us, "DEFEND! 3" against us, in the middle of the screen.
func _sync_capture(sim: MatchSim, local_team: int) -> void:
	var contested: bool = sim.base_contested_by(local_team) or sim.base_contested_by(1 - local_team)
	capture_label.visible = (sim.capture_team >= 0 or contested) and sim.phase == MatchSim.Phase.PLAYING
	if not capture_label.visible:
		return
	if sim.capture_team < 0:
		capture_label.text = "CONTESTED!"
		capture_label.add_theme_color_override("font_color", CAPTURE_COLOR)
		return
	var seconds: int = ceili(sim.capture_time_left())
	var ours: bool = sim.capture_team == local_team
	var how: String
	if sim.capture_kind == MatchSim.CaptureKind.ALL_TAGGED:
		how = "ALL TAGGED!" if ours else "ALL OF YOU TAGGED!"
	else:
		how = "HOLD THE BASE!" if ours else "DEFEND YOUR BASE!"
	capture_label.text = "%s\n%d" % [how, seconds]
	capture_label.add_theme_color_override("font_color", CAPTURE_COLOR.lerp(TeamColors.feed_own() if ours else TeamColors.feed_enemy(), 0.35))


## Adds a TAGGED! or RESCUED! pill under the settings button: blue for your team, red for theirs.
func feed(kind: Feed, our_team: bool, who: String) -> void:
	var pill: Label = Label.new()
	pill.text = "%s  %s" % ["TAGGED!" if kind == Feed.TAGGED else "RESCUED!", who]
	pill.add_theme_font_override("font", KalyeahTheme.BODY_FONT)
	pill.custom_minimum_size = FEED_SIZE
	pill.add_theme_font_size_override("font_size", UiMetrics.CAPTION)
	pill.add_theme_color_override("font_outline_color", Color(0.08, 0.05, 0.1))
	pill.add_theme_constant_override("outline_size", 6)
	pill.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	pill.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var style: StyleBoxFlat = StyleBoxFlat.new()
	style.bg_color = TeamColors.feed_own() if our_team else TeamColors.feed_enemy()
	style.set_corner_radius_all(10)
	style.content_margin_left = 14.0
	style.skew = Vector2(0.18, 0.0)
	pill.add_theme_stylebox_override("normal", style)
	pill.set_meta(&"left", FEED_TIME)
	feed_box.add_child(pill)
	while feed_box.get_child_count() > FEED_MAX:
		var oldest: Node = feed_box.get_child(0)
		feed_box.remove_child(oldest)
		oldest.queue_free()


## Text of the feed pills, oldest first (tests).
func feed_texts() -> PackedStringArray:
	var texts: PackedStringArray = PackedStringArray()
	for pill: Node in feed_box.get_children():
		texts.append((pill as Label).text)
	return texts


func _tick_feed(delta: float) -> void:
	for pill: Node in feed_box.get_children():
		var left: float = (pill.get_meta(&"left") as float) - delta
		pill.set_meta(&"left", left)
		(pill as Label).modulate.a = clampf(left / 0.4, 0.0, 1.0)
		if left <= 0.0:
			feed_box.remove_child(pill)
			pill.queue_free()


# ---- building ------------------------------------------------------------------

func _build() -> void:
	overhead = OverheadHud.new()
	_place(overhead, Vector4(0, 0, 1, 1), Vector4.ZERO)
	add_child(overhead)
	pulse = HurtPulse.new()
	add_child(pulse)
	console = ConsolePanel.new()
	console.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(console)
	MatchHudTop.build(self)
	cancel_zone = _build_cancel_zone()
	var weapon_one: AimButton = _aim_button()
	var weapon_two: AimButton = _aim_button()
	weapon_buttons = [weapon_one, weapon_two]
	aim_buttons = [weapon_one, weapon_two]
	shuffler = ShufflerView.new()
	shuffler.visible = false
	add_child(shuffler)
	dpad = DPad.new()
	dpad.accessibility_name = "Movement pad"
	dpad.accessibility_description = "Press a direction to run; let go to stop"
	dpad.rest_fraction = Vector2(0.5, 0.5)
	dpad.mouse_filter = Control.MOUSE_FILTER_IGNORE
	dpad.direction_changed.connect(_on_pad_direction)
	add_child(dpad)
	for slot: int in aim_buttons.size():
		aim_buttons[slot].cancel_zone = cancel_zone
		aim_buttons[slot].aim_released.connect(_on_aim_released.bind(slot))
	banner = Label.new()
	_place(banner, Vector4(0, 0.3, 1, 0.3), Vector4(0, -60, 0, 60))
	banner.visible = false
	banner.theme_type_variation = &"DisplayLabel"
	banner.add_theme_color_override("font_color", RoyaleStyle.GOLD)
	banner.add_theme_color_override("font_outline_color", UiSkin.LINE_COLOR)
	banner.add_theme_constant_override("outline_size", BANNER_OUTLINE)
	banner.add_theme_color_override("font_shadow_color", Color(0.0, 0.0, 0.0, 0.45))
	banner.add_theme_constant_override("shadow_offset_x", int(BANNER_SHADOW.x))
	banner.add_theme_constant_override("shadow_offset_y", int(BANNER_SHADOW.y))
	banner.add_theme_constant_override("shadow_outline_size", BANNER_OUTLINE)
	banner.add_theme_font_size_override("font_size", BANNER_FONT)
	banner.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	banner.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	banner.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(banner)
	capture_label = _pill_label("", 34, Vector4(0, 0.4, 1, 0.4), Vector4(120, -56, -120, 56))
	capture_label.theme_type_variation = &"DisplayLabel"
	capture_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	capture_label.add_theme_color_override("font_outline_color", Color(0.08, 0.05, 0.1))
	capture_label.add_theme_constant_override("outline_size", 10)
	again_button = _button("Play again", 32, Vector4(0.5, 0.5, 0.5, 0.5), Vector4(-160, -30, 160, 60))
	KalyeahTheme.style_button(again_button, "orange")
	again_button.pressed.connect(again_pressed.emit)
	again_button.visible = false
	results = MatchResults.new()
	add_child(results)
	results.adopt(again_button, true)
	tagged_panel = TaggedPanel.new()
	add_child(tagged_panel)
	swap_button = tagged_panel.swap_button
	tagged_panel.swap_pressed.connect(swap_pressed.emit)
	MatchHudTop.build_intro(self)
	coach = CoachMarks.new(self)
	add_child(coach)
	# last, so it draws over the skill buttons
	menu = MatchMenu.new()
	add_child(menu)
	hurt_button = menu.hurt_button
	menu.hurt_pressed.connect(hurt_pressed.emit)
	menu.status_pressed.connect(status_test_pressed.emit)
	menu.exit_pressed.connect(exit_pressed.emit)
	menu.closed.connect(set_controls_active.bind(true))
	feedback = MatchFeedback.new()
	add_child(feedback)
	feedback.return_home.connect(exit_pressed.emit)


## Places the console, the D-pad, the skill buttons and the cancel zone (see MatchHudLayout).
func layout_controls() -> void:
	MatchHudLayout.apply(self)


## Shows the opening show's banner (`title`, `subtitle`), or hides it with "".
func show_intro(title: String, subtitle: String = "") -> void:
	intro_bar.visible = not title.is_empty()
	intro_title.text = title
	intro_sub.text = subtitle


## Open or close the settings menu; the controls ignore touches while it is open.
func toggle_menu() -> void:
	if menu.visible:
		menu.close()
		return
	set_controls_active(false)
	menu.open()


func _build_cancel_zone() -> CancelMark:
	var zone: CancelMark = CancelMark.new()
	zone.mouse_filter = Control.MOUSE_FILTER_IGNORE
	zone.visible = false
	add_child(zone)
	return zone


## The finger is (true) or is not (false) on the cancel mark: it lights up.
func set_cancel_hot(hot: bool) -> void:
	cancel_zone.hot = hot


## Anchors first, then offsets (setting an anchor can move offsets).
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


func _aim_button() -> AimButton:
	var button: AimButton = AimButton.new()
	button.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(button)
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
