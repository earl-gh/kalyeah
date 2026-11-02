extends GutTest
## The interface plays the owner's match sound effects, cut short and quiet (docs/ART.md "Audio"): every role has its file, a pressed
## button plays the role it has, the coloured buttons press and the grey ones tap, a window opens with a sound, a toast says its
## tone aloud. Seam: UiSound (its `played` signal), UiKit.feel_press, MenuDialog, UiToast, UiToggle, UiSegmented, MenuCarousel.

var _heard: Array[StringName] = []


func before_each() -> void:
	_heard.clear()
	var sound: UiSound = UiSound.shared()
	await wait_frames(2)  # the player joins the tree on the next frame
	if not sound.played.is_connected(_listen):
		sound.played.connect(_listen)


func after_each() -> void:
	if is_instance_valid(UiSound._instance) and UiSound._instance.played.is_connected(_listen):
		UiSound._instance.played.disconnect(_listen)


func _listen(role: StringName) -> void:
	_heard.append(role)


func _button(role: StringName = &"") -> Button:
	var button: Button = autofree(Button.new()) as Button
	add_child(button)
	UiKit.feel_press(button, role)
	return button


func test_every_role_has_a_sound_that_exists_and_sane_numbers() -> void:
	for role: StringName in UiSound.ROLES:
		var spec: Dictionary = UiSound.ROLES[role]
		assert_true(ResourceLoader.exists(SfxView.DIR + (spec["file"] as String) + ".mp3"), "%s: %s" % [role, spec["file"]])
		assert_lt(spec["db"] as float, 0.0, "%s is quieter than the match's own level" % role)
		var pitch: Array = spec["pitch"] as Array
		assert_lte(pitch[0] as float, pitch[1] as float, role)
		assert_gte(spec["max"] as float, 0.0, role)


func test_the_toast_tones_all_have_a_role() -> void:
	for tone: String in UiToast.KINDS:
		assert_true(UiSound.ROLES.has(UiSound.TONE_ROLE[tone]), tone)


func test_a_pressed_button_taps_and_a_given_role_wins() -> void:
	var plain: Button = _button()
	plain.pressed.emit()
	assert_eq(_heard, [&"tap"] as Array[StringName])
	var card: Button = _button(&"select")
	card.pressed.emit()
	assert_eq(_heard, [&"tap", &"select"] as Array[StringName])
	var quiet: Button = _button()
	UiSound.set_role(quiet, UiSound.SILENT)
	quiet.pressed.emit()
	assert_eq(_heard.size(), 2, "a silent button plays nothing")


func test_wiring_a_button_again_does_not_play_twice() -> void:
	var button: Button = _button()
	UiKit.feel_press(button)
	UiSound.hook(button)
	button.pressed.emit()
	assert_eq(_heard.size(), 1)


func test_the_coloured_buttons_press_and_the_grey_ones_tap() -> void:
	var gold: Button = autofree(MenuDialog.make_button("Create", "gold")) as Button
	var grey: Button = autofree(MenuDialog.make_button("Cancel", "grey")) as Button
	add_child(gold)
	add_child(grey)
	gold.pressed.emit()
	grey.pressed.emit()
	assert_eq(_heard, [&"press", &"tap"] as Array[StringName])


func test_a_segment_ticks_and_a_switch_taps() -> void:
	var segments: UiSegmented = autofree(UiSegmented.new([{"id": &"a", "label": "A"}, {"id": &"b", "label": "B"}] as Array[Dictionary])) as UiSegmented
	add_child(segments)
	segments.buttons[&"b"].pressed.emit()
	var toggle: UiToggle = autofree(UiToggle.new()) as UiToggle
	add_child(toggle)
	toggle.pressed.emit()
	assert_eq(_heard, [&"tick", &"tap"] as Array[StringName])


func test_the_tabs_select_and_the_back_buttons_cancel() -> void:
	var bar: UiTabBar = autofree(UiTabBar.new([{"id": &"one", "label": "One", "icon": &"settings"}] as Array[Dictionary])) as UiTabBar
	add_child(bar)
	bar.tabs[&"one"].pressed.emit()
	assert_eq(_heard, [&"select"] as Array[StringName])
	var back: Button = autofree(MenuDialog.make_button("Back", "grey")) as Button
	add_child(back)
	UiSound.set_role(back, &"cancel")
	back.pressed.emit()
	assert_eq(_heard.back(), &"cancel")


func test_a_dialog_opening_plays_the_window_sound() -> void:
	var dialog: MenuDialog = autofree(MenuDialog.create("Sound")) as MenuDialog
	dialog.open(self)
	assert_eq(_heard, [&"window"] as Array[StringName])


func test_a_toast_plays_its_tone() -> void:
	for pair: Array in [["ok", &"success"], ["info", &"alert"], ["warn", &"deny"], ["error", &"deny"]]:
		_heard.clear()
		UiToast.say(self, "x", pair[0] as String)
		assert_eq(_heard, [pair[1]] as Array[StringName], pair[0] as String)
	await wait_frames(2)


func test_the_carousel_swipes_when_it_moves_and_is_quiet_when_set_in_place() -> void:
	var carousel: MenuCarousel = autofree(MenuCarousel.new()) as MenuCarousel
	carousel.size = Vector2(720.0, 600.0)
	add_child(carousel)
	carousel.setup("", TitleScreen.MODES, 0)
	_heard.clear()
	carousel.select(1, false)
	assert_eq(_heard.size(), 0, "put in place: silent")
	carousel.select(0, true)
	assert_eq(_heard, [&"swipe"] as Array[StringName])


func test_a_voice_can_be_reused_when_every_voice_is_busy() -> void:
	var sound: UiSound = UiSound.shared()
	for i: int in UiSound.VOICES * 3:
		sound.play_role(&"press")
	assert_eq(_heard.size(), UiSound.VOICES * 3)


func test_a_role_without_a_player_or_a_name_stays_silent() -> void:
	UiSound.play(&"nope")
	assert_eq(_heard.size(), 0)
