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
## A card in the tagged panel was tapped: follow player `id`.
signal watch_requested(id: int)
signal exit_pressed

enum Feed { TAGGED, RESCUED }

const BANNER_TIME: float = 2.0
const TOP_ROW_Y: float = 40.0
const MARGIN: float = 14.0
## The street is 1:2 (width:length), and so is its minimap.
const MINIMAP_SIZE: Vector2 = Vector2(140.0, 140.0)
const GEAR_SIZE: float = 64.0
const SCORE_SIZE: Vector2 = Vector2(210.0, 64.0)
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
## The stick's centre from the left edge (its outer ring stays on screen).
const STICK_FROM_LEFT: float = STICK_ZONE / 2.0 + 6.0
## The skill buttons: big enough for a thumb (Apple's 44 pt is 84 px), a gap apart, the
## second against the right margin and the first to its left.
const WEAPON_SIZE: float = 124.0
const WEAPON_GAP: float = 30.0
const WEAPON_MARGIN: float = 34.0
## The cancel zone sits above the skill buttons (drag a skill onto it to cancel the cast).
const CANCEL_FROM_RIGHT: float = WEAPON_MARGIN + WEAPON_SIZE + WEAPON_GAP / 2.0
const CANCEL_AT: Vector2 = Vector2(CANCEL_FROM_RIGHT, 440.0)
const CANCEL_SIZE: Vector2 = Vector2(110.0, 100.0)

var overhead: OverheadHud
var top: Control
var scoreboard: Scoreboard
var minimap: LaneMinimap
var joystick: VirtualJoystick
var weapon_buttons: Array[AimButton] = []
## Weapon 1, weapon 2 (the order MatchInput uses).
var aim_buttons: Array[AimButton] = []
var cancel_zone: Control
var tagged_panel: TaggedPanel
## The opening show's banner along the bottom ("TAG THE ENEMY BASE!").
var intro_bar: Control
var intro_title: Label
var intro_sub: Label
var swap_button: Button
var hurt_button: Button
var again_button: Button
## Big countdown in the middle while a capture is running.
var capture_label: Label
## TAGGED! / RESCUED! pills under the minimap (newest at the bottom).
var feed_box: VBoxContainer
var settings_button: TextureButton
var menu: MatchMenu
var banner: Label

var _banner_left: float = 0.0


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_build()
	layout_controls()


## Pixels the top row is pushed down to clear a notch or status bar.
func set_top_inset(pixels: float) -> void:
	top.position.y = pixels


func set_controls_active(active: bool) -> void:
	if not active:
		joystick.release()
	joystick.set_process_input(active)
	for control: Control in aim_buttons:
		control.set_process_input(active)


func controls_active() -> bool:
	return weapon_buttons[0].is_processing_input()


func show_banner(text: String) -> void:
	banner.text = text
	banner.visible = true
	_banner_left = BANNER_TIME
	# pop in: starts big and transparent, settles with a little overshoot
	banner.pivot_offset = banner.size / 2.0
	banner.scale = Vector2.ONE * 1.5
	banner.modulate.a = 0.0
	var tween: Tween = create_tween().set_parallel(true)
	tween.tween_property(banner, "scale", Vector2.ONE, 0.28).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	tween.tween_property(banner, "modulate:a", 1.0, 0.12)


func banner_text() -> String:
	return banner.text if banner.visible else ""


## Buttons, labels and cooldowns for the local player.
## `watched` is the player the camera follows while you are tagged.
func sync_player(sim: MatchSim, player: PlayerState, watched: int, pick_open: bool) -> void:
	tagged_panel.sync(sim, player, watched, pick_open)
	# while you wait in the box the controls are hidden so the card sits alone at the bottom
	# no buttons while you wait in the box or during the opening show
	var playing: bool = not player.tagged and sim.phase != MatchSim.Phase.INTRO
	for button: AimButton in weapon_buttons:
		button.visible = playing
	joystick.visible = playing
	if not playing:
		joystick.release()
	# disabled while on cooldown too: no pressing or precasting until ready
	var weapons_off: bool = player.tagged or not player.effects.can_cast()
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
		button.set_locked(weapons_off or player.weapon_cooldowns[slot] > 0.0)
		button.set_cooldown(player.weapon_cooldowns[slot], def.cooldown)


## Top row: the score card; the capture countdown; banners and the feed fade out.
func sync_score(sim: MatchSim, local_team: int, delta: float) -> void:
	scoreboard.show_score(sim.score, local_team, sim.match_time)
	_sync_capture(sim, local_team)
	again_button.visible = sim.phase == MatchSim.Phase.MATCH_OVER
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


## Adds a TAGGED! or RESCUED! pill under the minimap: blue for your team, red for theirs.
func feed(kind: Feed, our_team: bool, who: String) -> void:
	var pill: Label = Label.new()
	pill.text = "%s  %s" % ["TAGGED!" if kind == Feed.TAGGED else "RESCUED!", who]
	pill.custom_minimum_size = FEED_SIZE
	pill.add_theme_font_size_override("font_size", 20)
	pill.add_theme_color_override("font_outline_color", Color(0.08, 0.05, 0.1))
	pill.add_theme_constant_override("outline_size", 6)
	pill.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	pill.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var style: StyleBoxFlat = StyleBoxFlat.new()
	style.bg_color = FEED_OWN if our_team else FEED_ENEMY
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
	_build_top_row()
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
	banner.add_theme_color_override("font_color", Color(1, 0.86, 0.3))
	banner.add_theme_color_override("font_outline_color", Color(0.1, 0.05, 0.1))
	banner.add_theme_constant_override("outline_size", 12)
	banner.add_theme_font_size_override("font_size", 44)
	banner.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	banner.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	banner.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(banner)
	capture_label = _pill_label("", 34, Vector4(0, 0.4, 1, 0.4), Vector4(120, -56, -120, 56))
	capture_label.theme_type_variation = &""
	capture_label.add_theme_color_override("font_outline_color", Color(0.08, 0.05, 0.1))
	capture_label.add_theme_constant_override("outline_size", 10)
	again_button = _button("Play again", 32, Vector4(0.5, 0.5, 0.5, 0.5), Vector4(-160, -30, 160, 60))
	again_button.pressed.connect(again_pressed.emit)
	again_button.visible = false
	tagged_panel = TaggedPanel.new()
	add_child(tagged_panel)
	swap_button = tagged_panel.swap_button
	tagged_panel.swap_pressed.connect(swap_pressed.emit)
	tagged_panel.watch_requested.connect(watch_requested.emit)
	_build_intro_bar()
	# last, so it draws over the skill buttons
	menu = MatchMenu.new()
	add_child(menu)
	hurt_button = menu.hurt_button
	menu.hurt_pressed.connect(hurt_pressed.emit)
	menu.exit_pressed.connect(exit_pressed.emit)
	menu.closed.connect(set_controls_active.bind(true))


## The stick at the bottom left, weapon 1 and weapon 2 side by side at the bottom right.
func layout_controls() -> void:
	_put(joystick, false, Vector2(STICK_FROM_LEFT, ROW_HEIGHT), Vector2(STICK_ZONE, STICK_ZONE))
	var second: float = WEAPON_MARGIN + WEAPON_SIZE / 2.0
	_put(weapon_buttons[1], true, Vector2(second, ROW_HEIGHT), Vector2(WEAPON_SIZE, WEAPON_SIZE))
	_put(weapon_buttons[0], true, Vector2(second + WEAPON_SIZE + WEAPON_GAP, ROW_HEIGHT), Vector2(WEAPON_SIZE, WEAPON_SIZE))
	_put(cancel_zone, true, CANCEL_AT, CANCEL_SIZE)


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


func _build_top_row() -> void:
	top = Control.new()
	_place(top, Vector4(0, 0, 1, 0), Vector4(0, 0, 0, 520))
	top.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(top)
	# left: the tall street minimap, then the settings button beside it (top aligned)
	minimap = LaneMinimap.new()
	_place(minimap, Vector4(0, 0, 0, 0), Vector4(MARGIN, TOP_ROW_Y, MARGIN + MINIMAP_SIZE.x, TOP_ROW_Y + MINIMAP_SIZE.y))
	minimap.mouse_filter = Control.MOUSE_FILTER_IGNORE
	top.add_child(minimap)
	settings_button = TextureButton.new()
	settings_button.texture_normal = Icons.art(&"gear")
	settings_button.ignore_texture_size = true
	settings_button.stretch_mode = TextureButton.STRETCH_KEEP_ASPECT_CENTERED
	var gear_x: float = MARGIN * 1.6 + MINIMAP_SIZE.x
	_place(settings_button, Vector4(0, 0, 0, 0), Vector4(gear_x, TOP_ROW_Y, gear_x + GEAR_SIZE, TOP_ROW_Y + GEAR_SIZE))
	settings_button.pressed.connect(toggle_menu)
	top.add_child(settings_button)
	# under the minimap: the TAGGED!/RESCUED! feed
	feed_box = VBoxContainer.new()
	var feed_top: float = TOP_ROW_Y + MINIMAP_SIZE.y + 12.0
	_place(feed_box, Vector4(0, 0, 0, 0), Vector4(MARGIN, feed_top, MARGIN + FEED_SIZE.x, feed_top + FEED_SIZE.y * FEED_MAX + 12.0))
	feed_box.add_theme_constant_override("separation", 6)
	feed_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	top.add_child(feed_box)
	# right: the score card (points and the match clock)
	scoreboard = Scoreboard.new()
	_place(scoreboard, Vector4(1, 0, 1, 0), Vector4(-MARGIN - SCORE_SIZE.x, TOP_ROW_Y, -MARGIN, TOP_ROW_Y + SCORE_SIZE.y))
	scoreboard.mouse_filter = Control.MOUSE_FILTER_IGNORE
	top.add_child(scoreboard)


## The opening show's banner: a dark band at the bottom with the goal in gold.
func _build_intro_bar() -> void:
	intro_bar = ColorRect.new()
	(intro_bar as ColorRect).color = Color(0.05, 0.04, 0.08, 0.72)
	_place(intro_bar, Vector4(0, 1, 1, 1), Vector4(0, -260, 0, -120))
	intro_bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	intro_bar.visible = false
	add_child(intro_bar)
	intro_title = Label.new()
	_place(intro_title, Vector4(0, 0, 1, 0), Vector4(0, 18, 0, 78))
	intro_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	intro_title.add_theme_font_size_override("font_size", 46)
	intro_title.add_theme_color_override("font_color", Color(1.0, 0.82, 0.25))
	intro_title.add_theme_color_override("font_outline_color", Color(0.1, 0.05, 0.1))
	intro_title.add_theme_constant_override("outline_size", 10)
	intro_bar.add_child(intro_title)
	intro_sub = Label.new()
	_place(intro_sub, Vector4(0, 0, 1, 0), Vector4(0, 80, 0, 120))
	intro_sub.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	intro_sub.add_theme_font_size_override("font_size", 26)
	intro_sub.add_theme_color_override("font_outline_color", Color(0.1, 0.05, 0.1))
	intro_sub.add_theme_constant_override("outline_size", 6)
	intro_bar.add_child(intro_sub)


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


func _build_cancel_zone() -> Control:
	var zone: Control = Control.new()
	zone.mouse_filter = Control.MOUSE_FILTER_IGNORE
	zone.visible = false
	add_child(zone)
	var back: ColorRect = ColorRect.new()
	_place(back, Vector4(0, 0, 1, 1), Vector4.ZERO)
	back.color = Color(0.85, 0.15, 0.15, 0.55)
	back.mouse_filter = Control.MOUSE_FILTER_IGNORE
	zone.add_child(back)
	var label: Label = Label.new()
	_place(label, Vector4(0, 0, 1, 1), Vector4.ZERO)
	label.text = "Cancel"
	label.add_theme_font_size_override("font_size", 24)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	zone.add_child(label)
	return zone


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


func _skill_button(icon: StringName, label: String) -> TouchButton:
	var button: TouchButton = TouchButton.new()
	button.mouse_filter = Control.MOUSE_FILTER_IGNORE
	button.icon_id = icon
	button.label_text = label
	add_child(button)
	return button
