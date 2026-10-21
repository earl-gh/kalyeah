extends GutTest

const PRACTICE: PackedScene = preload("res://scenes/match/practice.tscn")
const SETTINGS_SCRIPT: Script = preload("res://scripts/autoload/settings.gd")

var _motion: bool
var _loadout: Array[StringName]


func before_each() -> void:
	_motion = Settings.reduced_motion
	_loadout = Session.saved_loadout.duplicate()
	Settings.set_reduced_motion(false)


func after_each() -> void:
	Session.saved_loadout = _loadout
	Session.save()
	Settings.set_reduced_motion(_motion)
	await wait_process_frames(3)


func test_motion_syncs_across_settings_and_obsolete_text_size_is_ignored() -> void:
	var file: ConfigFile = ConfigFile.new()
	file.load(Settings.SAVE_PATH)
	file.set_value("accessibility", "text_scale", 1.4)
	file.set_value("accessibility", "reduced_motion", true)
	file.save(Settings.SAVE_PATH)
	var loaded: Node = autofree(SETTINGS_SCRIPT.new()) as Node
	add_child(loaded)
	assert_true(loaded.get("reduced_motion") as bool)
	var first: AccessibilityOptions = autofree(AccessibilityOptions.new()) as AccessibilityOptions
	var second: AccessibilityOptions = autofree(AccessibilityOptions.new()) as AccessibilityOptions
	add_child(first)
	add_child(second)
	assert_eq(first.find_children("*", "Button", true, false).size(), 2, "settings has the motion and colour-blind toggles")
	var label: Label = autofree(Label.new()) as Label
	label.add_theme_font_size_override("font_size", UiMetrics.BODY)
	add_child(label)
	first.motion_toggle.button_pressed = true
	assert_true(second.motion_toggle.button_pressed, "the other switch follows")
	await wait_process_frames(3)
	assert_eq(label.get_theme_font_size("font_size"), UiMetrics.BODY, "legacy preference cannot enlarge the interface")
	file.load(Settings.SAVE_PATH)
	assert_true(file.get_value("accessibility", "reduced_motion") as bool)
	file.set_value("accessibility", "reduced_motion", "broken")
	file.save(Settings.SAVE_PATH)
	var fallback: Node = autofree(SETTINGS_SCRIPT.new()) as Node
	add_child(fallback)
	assert_false(fallback.get("reduced_motion") as bool)


func test_reduced_motion_clears_carousel_button_and_modal_animation() -> void:
	var carousel: MenuCarousel = autofree(MenuCarousel.new()) as MenuCarousel
	add_child(carousel)
	carousel.size = Vector2(720.0, 576.0)
	carousel.setup("", TitleScreen.MODES, 0)
	carousel.select(2)
	Settings.set_reduced_motion(true)
	assert_eq(carousel._position, 2.0)
	await wait_process_frames(2)
	assert_lt(carousel.cards[0].scale.x, 1.0, "layout depth is preserved; reduced motion only stops animation")
	var button: Button = autofree(MenuDialog.make_button("TEST", "orange")) as Button
	add_child(button)
	MenuDialog._squish(button, 0.955)
	assert_eq(button.scale, Vector2.ONE)
	var dialog: MenuDialog = MenuDialog.create("TEST")
	dialog.open(self)
	assert_eq(dialog.panel.scale, Vector2.ONE)
	assert_eq(dialog.modulate.a, 1.0)
	dialog.close(false)


func test_static_intro_preserves_timing_and_reduced_motion_clears_shake_and_grain() -> void:
	Settings.set_reduced_motion(true)
	var practice: PracticeMatch = autofree(PRACTICE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	practice.finish_pick()
	var camera: ArenaCamera = practice.get_node("%ArenaCamera") as ArenaCamera
	assert_eq(practice.sim.phase, MatchSim.Phase.INTRO)
	assert_almost_eq(practice.sim.intro_left, practice.rules.intro_time, 0.001)
	assert_true(camera.position.is_equal_approx(camera.desired_position(practice._flip())))
	camera.shake(0.4)
	camera._process(1.0 / 30.0)
	assert_eq(camera.h_offset, 0.0)
	assert_eq(camera.v_offset, 0.0)
	assert_eq(camera._shake, 0.0)
	var film: FilmFinish
	for node: Node in practice.find_children("*", "ColorRect", true, false):
		if node is FilmFinish:
			film = node as FilmFinish
	assert_not_null(film)
	if film == null:
		return
	assert_eq(film.finish.get_shader_parameter("grain"), 0.0)
	practice.advance(practice.rules.tick_dt())
	assert_almost_eq(practice.sim.intro_left, practice.rules.intro_time - practice.rules.tick_dt(), 0.001)
	Settings.set_reduced_motion(false)
	assert_almost_eq(film.finish.get_shader_parameter("grain") as float, 0.025, 0.00001)


func test_modal_tab_cycle_skips_disabled_controls_and_restores_opener() -> void:
	Settings.set_reduced_motion(true)
	var opener: Button = autofree(MenuDialog.make_button("OPEN", "grey")) as Button
	add_child(opener)
	opener.grab_focus()
	var dialog: MenuDialog = MenuDialog.create("NAME")
	var field: LineEdit = LineEdit.new()
	dialog.body.add_child(field)
	var disabled: Button = MenuDialog.make_button("DISABLED", "grey")
	disabled.disabled = true
	var cancel: Button = MenuDialog.make_button("CANCEL", "grey")
	var done: Button = MenuDialog.make_button("DONE", "orange")
	dialog.add_buttons([disabled, cancel, done])
	dialog.open(self)
	await wait_process_frames(2)
	assert_true(field.has_focus())
	var event: InputEventKey = InputEventKey.new()
	event.keycode = KEY_TAB
	event.pressed = true
	Input.parse_input_event(event)
	await wait_process_frames(1)
	assert_true(cancel.has_focus())
	done.grab_focus()
	Input.parse_input_event(event)
	await wait_process_frames(1)
	assert_true(field.has_focus(), "Tab wraps within the dialog")
	dialog.close(false)
	await wait_process_frames(2)
	assert_true(opener.has_focus())


func test_carousel_supports_keyboard_selection_with_visible_focus() -> void:
	Settings.set_reduced_motion(true)
	var carousel: MenuCarousel = autofree(MenuCarousel.new()) as MenuCarousel
	add_child(carousel)
	carousel.size = Vector2(720.0, 576.0)
	carousel.setup("", TitleScreen.MODES, 0)
	carousel.grab_focus()
	var event: InputEventAction = InputEventAction.new()
	event.action = "ui_right"
	event.pressed = true
	carousel._gui_input(event)
	assert_eq(carousel.index, 1)
	assert_eq(carousel._position, 1.0)
	assert_true(carousel._focus_frame.visible)
	assert_eq(carousel.cards[0].focus_mode, Control.FOCUS_NONE)


func test_decorative_sway_stops_and_can_resume_when_preference_changes() -> void:
	var subject: Node3D = autofree(Node3D.new()) as Node3D
	subject.rotation.y = 0.8
	add_child(subject)
	subject.add_child(DecorativeSway.new(subject, 6.0, 0.3))
	Settings.set_reduced_motion(true)
	await wait_process_frames(3)
	assert_almost_eq(subject.rotation.y, 0.8, 0.00001)
	Settings.set_reduced_motion(false)
	await wait_process_frames(3)
	assert_ne(subject.rotation.y, 0.8)


func test_room_content_and_dialog_stay_within_phone_width() -> void:
	Settings.set_reduced_motion(true)
	var phone: Control = autofree(Control.new()) as Control
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var room: HotspotRoomView = HotspotRoomView.new()
	phone.add_child(room)
	room.set_online(true)
	var members: Array = []
	for i: int in 6:
		members.append({"player_id": i + 1, "team": i % 2, "name": "LongName1234", "char": Session.AVATARS[i], "ready": true})
	room.show_room({"code": "ABC234XY", "host": 1, "team_size": 3, "best_of": 7, "members": members}, 1)
	await wait_process_frames(5)
	var scroll: ScrollContainer = room.find_child("RoomScroll", true, false) as ScrollContainer
	assert_lte(room._content.get_combined_minimum_size().x, scroll.size.x, "room columns must not force a horizontal overflow")
	assert_lte(room.map_card.get_global_rect().end.x, phone.get_global_rect().end.x)
	assert_false(scroll.is_ancestor_of(room.action_button))
	var dialog: MenuDialog = MenuDialog.create("SETTINGS")
	SettingsPage.fill(dialog.body)
	dialog.add_buttons([MenuDialog.make_button("DONE", "orange")])
	dialog.open(phone)
	await wait_process_frames(5)
	assert_lte(dialog.body.get_combined_minimum_size().x, dialog.panel.size.x - 64.0)
	assert_lte(dialog.panel.get_global_rect().end.y, phone.get_global_rect().end.y)
	dialog.close(false)


func test_menu_blocks_local_input_and_cancels_aim_without_moving_or_casting() -> void:
	Settings.set_reduced_motion(true)
	var practice: PracticeMatch = autofree(PRACTICE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	practice.finish_pick()
	practice.skip_intro()
	var player: PlayerState = practice.sim.players[practice.local_id]
	practice.input.aim_started(0)
	practice.advance(practice.rules.tick_dt())
	assert_gte(player.aim_hold[0], 0.0)
	practice.hud.toggle_menu()
	var start: Vector2 = player.position
	practice.input.set_stick(Vector2.RIGHT)
	practice.input.key_held[0] = true
	practice.input.key_aim[0] = Vector2.UP
	var blocked: PlayerInput = practice._local_input(123)
	assert_eq(blocked.move, Vector2.ZERO)
	assert_eq(blocked.buttons, PlayerInput.BTN_AIM_CANCEL)
	assert_eq(blocked.tick, 123)
	assert_false(practice.input.key_held[0], "discard stale keyboard aim on menu entry")
	practice.advance(practice.rules.tick_dt() * 3.0)
	assert_eq(player.position, start, "menu navigation must not move the player")
	assert_eq(player.aim_hold[0], -1.0, "opening a menu cancels a held aim")
	assert_eq(player.weapon_cooldowns[0], 0.0, "cancel must not fire a release cast")
	practice.hud.menu.close()
	practice.advance(practice.rules.tick_dt())
	assert_true(practice.controls_active())
	assert_eq(player.weapon_cooldowns[0], 0.0)
	practice.input.set_stick(Vector2.RIGHT)
	practice.advance(practice.rules.tick_dt())
	assert_ne(player.position, start, "fresh input works after closing the menu")
