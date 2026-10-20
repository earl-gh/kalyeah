class_name MatchHud
extends Control
## The whole in-match HUD, built in code (no scene text to patch): overhead bars,
## top row (tall street minimap and settings button on the left, score in the
## centre, who is tagged on the right), the TAGGED!/RESCUED! feed under the minimap,
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
signal watch_requested(id: int)
signal exit_pressed

enum Feed { TAGGED, RESCUED }

const BANNER_TIME: float = 2.0
## The short word on a weapon button for each status that stops casting.
const CAST_BLOCK_TAGS: Dictionary[int, String] = {
	StatusEffects.Type.STUN: "STUNNED",
	StatusEffects.Type.AIRBORNE: "AIRBORNE",
	StatusEffects.Type.BOUNCE: "BOUNCED",
	StatusEffects.Type.KNOCKOUT: "DOWN",
	StatusEffects.Type.POLYMORPH: "CAN",
}
const TOP_ROW_Y: float = 40.0
const MARGIN: float = 14.0
## The street is 1:2 (width:length), and so is its minimap.
const MINIMAP_SIZE: Vector2 = Vector2(100.0, 200.0)
const GEAR_SIZE: float = UiMetrics.TOUCH
const SCORE_SIZE: Vector2 = Vector2(280.0, 88.0)
const FEED_TIME: float = 2.6
const FEED_SIZE: Vector2 = Vector2(250.0, 46.0)
const FEED_MAX: int = 3
const FEED_OWN: Color = Color(0.2, 0.42, 0.85, 0.92)
const FEED_ENEMY: Color = Color(0.82, 0.22, 0.2, 0.92)
const CAPTURE_COLOR: Color = Color(1.0, 0.95, 0.85)
## Controls (owner), as (distance from their screen edge, distance from the bottom) of each
## control's centre on the 720x1280 base: the movement stick at the bottom left, the two skill
## buttons at the bottom right, all in one row within thumb reach.
const ROW_HEIGHT: float = 150.0
## The square the stick listens in: its outer ring plus a margin for the thumb.
const STICK_ZONE: float = (VirtualJoystick.OUTER_RADIUS + 20.0) * 2.0
## The stick's outer ring stays this far from its screen edge.
const STICK_MARGIN: float = 6.0
## The skill buttons: big enough for a thumb (Apple's 44 pt is 84 px), a gap apart, the
## second against the right margin and the first to its left.
const WEAPON_SIZE: float = 124.0
const WEAPON_GAP: float = 30.0
const WEAPON_MARGIN: float = 34.0
## The cancel zone sits above the skill buttons (drag a skill onto it to cancel the cast); this is its height
## from the bottom (its distance from the edge follows the buttons).
const CANCEL_AT: Vector2 = Vector2(0.0, 440.0)
const CANCEL_SIZE: Vector2 = Vector2(110.0, 100.0)

var overhead: OverheadHud
## The red glow round the screen edge when you are hit (under the controls).
var pulse: HurtPulse
var top: Control
var scoreboard: Scoreboard
var minimap: LaneMinimap
var joystick: VirtualJoystick
var weapon_buttons: Array[AimButton] = []
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
var arrows: DirectionArrows
## What the results card says the match was ("Practice", "Hotspot", "Online").
var results_mode: String = "Practice"
## Big countdown in the middle while a capture is running.
var capture_label: Label
## TAGGED! / RESCUED! pills under the minimap (newest at the bottom).
var feed_box: VBoxContainer
var settings_button: TextureButton
var menu: MatchMenu
var banner: Label

var feedback: MatchFeedback
var connection_blocked: bool = false

var _banner_tween: Tween

var _banner_left: float = 0.0


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_build()
	Settings.accessibility_changed.connect(_on_accessibility_changed)
	Settings.controls_changed.connect(layout_controls)
	layout_controls()


## Pixels the top row is pushed down to clear a notch or status bar.
func set_top_inset(pixels: float) -> void:
	top.position.y = pixels


func set_controls_active(active: bool) -> void:
	active = active and not connection_blocked
	if not active:
		cancel_controls()
	joystick.set_process_input(active)
	for control: Control in aim_buttons:
		control.set_process_input(active)


## Clears every live gesture while leaving input processing in its current state.
func cancel_controls() -> void:
	joystick.release()
	for button: AimButton in aim_buttons:
		button.cancel_aim()


func controls_active() -> bool:
	return weapon_buttons[0].is_processing_input()


func show_banner(text: String) -> void:
	banner.text = text
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
	banner.scale = Vector2.ONE * 1.5
	banner.modulate.a = 0.0
	_banner_tween = create_tween().set_parallel(true)
	_banner_tween.tween_property(banner, "scale", Vector2.ONE, 0.28).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	_banner_tween.tween_property(banner, "modulate:a", 1.0, 0.12)


func banner_text() -> String:
	return banner.text if banner.visible else ""


## Buttons, labels and cooldowns for the local player.
## `watched` is the player the camera follows while you are tagged.
func sync_player(sim: MatchSim, player: PlayerState, watched: int, pick_open: bool) -> void:
	tagged_panel.sync(sim, player, watched, pick_open)
	# while you wait in the box the controls are hidden so the card sits alone at the bottom
	# no buttons while you wait in the box or during the opening show
	var playing: bool = not player.tagged and sim.phase == MatchSim.Phase.PLAYING and not pick_open and not connection_blocked
	for button: AimButton in weapon_buttons:
		button.visible = playing
	joystick.visible = playing
	if not playing:
		joystick.release()
	# disabled while on cooldown too: no pressing or precasting until ready
	var weapons_off: bool = not playing or not player.effects.can_cast()
	feedback.sync_hint(player, joystick, aim_buttons, playing)
	for slot: int in weapon_buttons.size():
		var button: AimButton = weapon_buttons[slot]
		if slot >= player.weapons.size():
			button.set_locked(true)
			continue
		var def: WeaponDef = sim.weapon_defs[player.weapons[slot]]
		if button.label_text != def.short_name or button.sub_text != def.kind_label():
			button.label_text = def.short_name
			button.sub_text = def.kind_label()
			button.icon_id = def.id
			button.sub_color = WeaponPickScreen.kind_color(def.kind)
			button.queue_redraw()
		button.blocked_text = _block_tag(player) if playing else ""
		button.set_locked(weapons_off or player.weapon_cooldowns[slot] > 0.0)
		button.set_cooldown(player.weapon_cooldowns[slot], def.cooldown)


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
	capture_label.visible = sim.capture_team >= 0 and sim.phase == MatchSim.Phase.PLAYING
	if not capture_label.visible:
		return
	var seconds: int = ceili(sim.capture_time_left())
	var ours: bool = sim.capture_team == local_team
	var how: String
	if sim.capture_kind == MatchSim.CaptureKind.ALL_TAGGED:
		how = "ALL TAGGED!" if ours else "ALL OF YOU TAGGED!"
	else:
		how = "HOLD THE BASE!" if ours else "DEFEND YOUR BASE!"
	capture_label.text = "%s\n%d" % [how, seconds]
	capture_label.add_theme_color_override("font_color", CAPTURE_COLOR.lerp(FEED_OWN if ours else FEED_ENEMY, 0.35))


## Adds a TAGGED! or RESCUED! line under the minimap: blue for your team, red for theirs.
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
	# no pill behind it: the team colour is the text's colour
	pill.add_theme_color_override("font_color", FEED_OWN.lightened(0.3) if our_team else FEED_ENEMY.lightened(0.3))
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
	MatchHudTop.build(self)
	cancel_zone = _build_cancel_zone()
	var weapon_one: AimButton = _aim_button()
	var weapon_two: AimButton = _aim_button()
	weapon_buttons = [weapon_one, weapon_two]
	aim_buttons = [weapon_one, weapon_two]
	joystick = VirtualJoystick.new()
	joystick.rest_fraction = Vector2(0.5, 0.5)
	joystick.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(joystick)
	for button: AimButton in aim_buttons:
		button.cancel_zone = cancel_zone
	banner = Label.new()
	_place(banner, Vector4(0, 0.3, 1, 0.3), Vector4(0, -60, 0, 60))
	banner.visible = false
	banner.theme_type_variation = &"DisplayLabel"
	banner.add_theme_color_override("font_color", Color(1, 0.86, 0.3))
	banner.add_theme_color_override("font_outline_color", Color(0.1, 0.05, 0.1))
	banner.add_theme_constant_override("outline_size", 12)
	banner.add_theme_font_size_override("font_size", 44)
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
	tagged_panel.watch_requested.connect(watch_requested.emit)
	MatchHudTop.build_intro(self)
	arrows = DirectionArrows.new()
	add_child(arrows)
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


## The stick at the bottom left, weapon 1 and weapon 2 side by side at the bottom right: mirrored for
## a left-handed player, and scaled by the control size setting.
func layout_controls() -> void:
	var scale_by: float = Settings.control_scale
	var mirrored: bool = Settings.left_handed
	var weapon: float = WEAPON_SIZE * scale_by
	var gap: float = WEAPON_GAP * scale_by
	var stick_zone: float = STICK_ZONE * scale_by
	# the stick keeps its zone; scaling it around its middle makes the rings and the grab area grow together
	_put(joystick, mirrored, Vector2(stick_zone / 2.0 + STICK_MARGIN, ROW_HEIGHT), Vector2(STICK_ZONE, STICK_ZONE))
	joystick.pivot_offset = Vector2(STICK_ZONE, STICK_ZONE) / 2.0
	joystick.scale = Vector2.ONE * scale_by
	var second: float = WEAPON_MARGIN + weapon / 2.0
	_put(weapon_buttons[1], not mirrored, Vector2(second, ROW_HEIGHT), Vector2(weapon, weapon))
	_put(weapon_buttons[0], not mirrored, Vector2(second + weapon + gap, ROW_HEIGHT), Vector2(weapon, weapon))
	_put(cancel_zone, not mirrored, Vector2(WEAPON_MARGIN + weapon + gap / 2.0, CANCEL_AT.y), CANCEL_SIZE)


## Places `control` with its centre `at` = (from the right or left edge, from the bottom).
func _put(control: Control, from_right: bool, at: Vector2, extent: Vector2) -> void:
	var edge: float = 1.0 if from_right else 0.0
	var side: float = -1.0 if from_right else 1.0
	var near: float = side * at.x
	var half: float = extent.x / 2.0
	var low: float = near - half
	var high: float = near + half
	_place(control, Vector4(edge, 1, edge, 1), Vector4(
		minf(low, high), -at.y - extent.y / 2.0, maxf(low, high), -at.y + extent.y / 2.0))


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
