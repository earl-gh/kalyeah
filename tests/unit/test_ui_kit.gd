extends GutTest
## The painted interface kit: the skin (buttons and panels drawn in code), the switch, the segmented
## control, the tab bar and the page pieces. Looks are checked by what they promise a thumb and a
## reader (size, contrast, state), not by pixels.


func test_every_button_variant_has_a_normal_a_hover_and_a_pressed_face() -> void:
	for variant: String in UiSkin.BUTTONS:
		for state: String in ["normal", "hover", "pressed"]:
			var box: StyleBoxTexture = UiSkin.button(variant, state)
			assert_not_null(box.texture, "%s/%s is painted" % [variant, state])
			assert_eq(UiSkin.button(variant, state), box, "%s/%s is built once and cached" % [variant, state])


func test_a_pressed_button_sinks_into_its_lip() -> void:
	var up: StyleBoxTexture = UiSkin.button("gold", "normal")
	var down: StyleBoxTexture = UiSkin.button("gold", "pressed")
	assert_gt(down.content_margin_top, up.content_margin_top, "the label moves down with the face")
	assert_lt(down.content_margin_bottom, up.content_margin_bottom, "and the lip under it gets thinner")
	assert_almost_eq(down.content_margin_top + down.content_margin_bottom, up.content_margin_top + up.content_margin_bottom, 0.01, "so the button never changes size")


func test_button_shadows_are_drawn_outside_the_control() -> void:
	var box: StyleBoxTexture = UiSkin.button("blue")
	assert_gt(box.expand_margin_bottom, 0.0, "the drop shadow lives in the padding")
	var image: Image = box.texture.get_image()
	assert_eq(image.get_pixel(0, 0).a, 0.0, "the corner of the texture is clear")
	var middle: Color = image.get_pixel(image.get_width() / 2, image.get_height() / 2)
	assert_eq(middle.a, 1.0, "the face is solid")


func test_label_colours_read_on_their_buttons() -> void:
	for variant: String in ["gold", "blue", "green", "red", "slate"]:
		var spec: Dictionary = UiSkin.BUTTONS[variant]
		var face: Color = (spec["top"] as Color).lerp(spec["bottom"] as Color, 0.5)
		var ink: Color = spec["text"] as Color
		assert_gt(absf(ink.get_luminance() - face.get_luminance()), 0.35, "%s label is readable on its face" % variant)


func test_panels_wear_their_padding_and_are_not_shared() -> void:
	var roomy: StyleBoxTexture = UiSkin.plate("glass", 40.0)
	var snug: StyleBoxTexture = UiSkin.plate("glass", 8.0)
	assert_eq(roomy.content_margin_left, 40.0)
	assert_eq(snug.content_margin_left, 8.0, "one panel's padding does not change another's")
	assert_eq(roomy.texture, snug.texture, "while the painted texture is shared")


func test_the_theme_buttons_use_the_skin() -> void:
	var button: Button = autofree(Button.new()) as Button
	KalyeahTheme.style_button(button, "green")
	assert_eq(button.get_theme_stylebox("normal"), UiSkin.button("green"))
	assert_eq(button.get_theme_stylebox("pressed"), UiSkin.button("green", "pressed"))
	assert_eq(KalyeahTheme.skin_variant("orange"), "gold", "the old colour names still work")
	assert_eq(KalyeahTheme.skin_variant("grey"), "slate")


func test_the_switch_is_a_thumb_sized_toggle_that_reports_its_state() -> void:
	var toggle: UiToggle = autofree(UiToggle.new()) as UiToggle
	add_child(toggle)
	assert_gte(toggle.custom_minimum_size.y, UiMetrics.TOUCH, "tall enough for a thumb")
	assert_gte(toggle.custom_minimum_size.x, 100.0)
	var changes: Array[bool] = []
	toggle.toggled.connect(func(on: bool) -> void: changes.append(on))
	toggle.set_state(true)
	assert_true(toggle.button_pressed)
	assert_eq(changes.size(), 0, "opening a screen with it on is not a change")
	toggle.button_pressed = false
	assert_eq(changes, [false] as Array[bool], "a real change is announced")


func test_the_segmented_control_announces_changes_and_lights_one_segment() -> void:
	var control: UiSegmented = autofree(UiSegmented.new([{"id": &"a", "label": "A"}, {"id": &"b", "label": "B"}])) as UiSegmented
	add_child(control)
	var heard: Array[StringName] = []
	control.chosen.connect(func(id: StringName) -> void: heard.append(id))
	control.select(&"a", false)
	assert_eq(heard.size(), 0, "a quiet select says nothing")
	control.buttons[&"b"].pressed.emit()
	assert_eq(heard, [&"b"] as Array[StringName])
	control.buttons[&"b"].pressed.emit()
	assert_eq(heard.size(), 1, "choosing the open segment again is not a change")
	assert_eq(control.current, &"b")
	for button: Button in control.buttons.values():
		assert_gte(button.custom_minimum_size.y, 80.0, "each segment is a thumb target")


func test_the_tab_bar_lights_only_the_open_tab_and_announces_presses() -> void:
	var motion: bool = Settings.reduced_motion
	Settings.set_reduced_motion(true)
	var bar: UiTabBar = autofree(UiTabBar.new(TitleScreen.TABS)) as UiTabBar
	add_child(bar)
	bar.size = Vector2(720.0, UiTabBar.HEIGHT)
	assert_eq(bar.tabs.size(), 3, "Street, Play, Settings")
	var heard: Array[StringName] = []
	bar.tab_pressed.connect(func(id: StringName) -> void: heard.append(id))
	bar.set_active(&"street")
	assert_eq(bar.active, &"street")
	assert_true((bar.tabs[&"street"].get_node("Name") as Label).visible, "the open tab says its name")
	assert_false((bar.tabs[&"settings"].get_node("Name") as Label).visible, "the others are quiet")
	bar.tabs[&"settings"].pressed.emit()
	assert_eq(heard, [&"settings"] as Array[StringName])
	assert_gte(bar.custom_minimum_size.y, 112.0, "a deep bar: easy to hit with a thumb")
	Settings.set_reduced_motion(motion)


func test_the_profile_chip_shows_the_name_and_a_change_hint() -> void:
	var saved: String = Session.player_name
	Session.player_name = "Maria"
	var chip: UiProfileChip = autofree(UiProfileChip.new()) as UiProfileChip
	add_child(chip)
	chip.refresh()
	assert_eq(chip.name_label.text, "Maria")
	assert_false(chip.hint_label.text.is_empty(), "it says the name can be changed")
	assert_gte(chip.custom_minimum_size.y, UiMetrics.TOUCH)
	Session.player_name = saved


func test_page_pieces_name_their_parts() -> void:
	var header: VBoxContainer = autofree(UiKit.page_header("History", "Your last battles")) as VBoxContainer
	assert_not_null(header)
	var row: HBoxContainer = autofree(UiKit.setting_row("Sound", "Master volume", Control.new())) as HBoxContainer
	assert_not_null(row)
	var wide: Label = autofree(UiKit.text("A long sentence that should wrap onto a second line when the room is small", UiMetrics.BODY)) as Label
	assert_eq(wide.autowrap_mode, TextServer.AUTOWRAP_WORD_SMART)
	var inline: Label = autofree(UiKit.text("100 %", UiMetrics.BODY, KalyeahTheme.TEXT, false)) as Label
	assert_eq(inline.autowrap_mode, TextServer.AUTOWRAP_OFF, "figures never wrap")
	var chip: PanelContainer = autofree(UiKit.chip("Victory", Color("2fbf5a"))) as PanelContainer
	assert_eq((chip.get_node("Text") as Label).text, "Victory")


func test_a_picked_card_wears_a_gold_rim_and_a_plain_one_does_not() -> void:
	var picked: Image = UiSkin.plate("picked").texture.get_image()
	var plain: Image = UiSkin.plate("glass").texture.get_image()
	var rim: Color = picked.get_pixel(UiSkin.PLATE_PAD + 2, picked.get_height() / 2)
	assert_gt(rim.r, 0.85, "gold")
	assert_gt(rim.g, 0.6)
	assert_lt(rim.b, 0.4)
	var edge: Color = plain.get_pixel(UiSkin.PLATE_PAD + 2, plain.get_height() / 2)
	assert_lt(edge.r, 0.4, "a plain card has no gold")


func test_the_shared_choice_toggle_is_a_slate_button_that_turns_blue() -> void:
	var button: Button = autofree(Button.new()) as Button
	LobbyWidgets.make_toggle(button)
	assert_true(button.toggle_mode)
	assert_eq(button.get_theme_stylebox("normal"), UiSkin.button("slate"))
	assert_eq(button.get_theme_stylebox("pressed"), UiSkin.button("blue", "pressed"))


func test_a_button_that_leaves_the_tree_and_returns_is_wired_for_its_click_sound_once() -> void:
	var holder: Control = autofree(Control.new()) as Control
	add_child(holder)
	var button: Button = Button.new()
	holder.add_child(button)
	holder.remove_child(button)
	holder.add_child(button)
	var wired: int = 0
	for connection: Dictionary in button.pressed.get_connections():
		if (connection["callable"] as Callable).get_object() == Settings:
			wired += 1
	assert_eq(wired, 1, "one click sound per press, and no 'already connected' error")
	button.free()


## Every styled button gives under the thumb (Play again, Leave, Swap and the room's Ready had no press feel), and is wired
## once however often it is restyled (the segmented control restyles on every change).
func test_every_styled_button_is_wired_for_its_press_once() -> void:
	var button: Button = autofree(Button.new()) as Button
	add_child(button)
	KalyeahTheme.style_button(button, "orange")
	KalyeahTheme.style_button(button, "grey")
	UiKit.feel_press(button)
	assert_eq(button.button_down.get_connections().size(), 1, "restyling and wiring again do not sink the press twice")
	assert_eq(button.button_up.get_connections().size(), 1, "nor spring it back twice")
	var dialog_button: Button = autofree(MenuDialog.make_button("TEST", "orange")) as Button
	assert_eq(dialog_button.button_down.get_connections().size(), 1, "a dialog button is wired by the same one path")


func test_a_held_button_sinks_toward_its_centre_and_springs_back() -> void:
	var motion: bool = Settings.reduced_motion
	Settings.set_reduced_motion(false)
	var button: Button = autofree(Button.new()) as Button
	add_child(button)
	button.size = Vector2(200.0, 90.0)
	KalyeahTheme.style_button(button, "orange")
	assert_lt(UiKit.PRESS_SCALE, 1.0, "a press sinks, it does not swell")
	button.button_down.emit()
	await wait_seconds(UiKit.PRESS_DOWN + 0.1)
	assert_almost_eq(button.scale.x, UiKit.PRESS_SCALE, 0.005, "a held button sinks")
	assert_almost_eq(button.scale.y, UiKit.PRESS_SCALE, 0.005)
	assert_eq(button.pivot_offset, button.size / 2.0, "toward its centre")
	button.button_up.emit()
	await wait_seconds(UiKit.PRESS_UP + 0.1)
	assert_almost_eq(button.scale.x, 1.0, 0.001, "let go, it springs back to its own size")
	assert_almost_eq(button.scale.y, 1.0, 0.001)
	Settings.set_reduced_motion(motion)


func test_under_reduce_motion_a_pressed_button_keeps_its_size() -> void:
	var motion: bool = Settings.reduced_motion
	Settings.set_reduced_motion(true)
	var button: Button = autofree(Button.new()) as Button
	add_child(button)
	button.size = Vector2(200.0, 90.0)
	KalyeahTheme.style_button(button, "orange")
	button.button_down.emit()
	await wait_seconds(UiKit.PRESS_DOWN + 0.1)
	assert_eq(button.scale, Vector2.ONE, "held, it does not move")
	button.button_up.emit()
	await wait_seconds(UiKit.PRESS_UP + 0.1)
	assert_eq(button.scale, Vector2.ONE, "let go, it does not move")
	Settings.set_reduced_motion(motion)


## Every big word is lettered the same way: the display face, an outline that grows with it and a visible shadow dropped
## under it, at each step of the display scale.
func test_lettered_words_scale_their_outline_and_drop_a_shadow() -> void:
	var steps: Array[int] = [UiMetrics.HEADLINE, UiMetrics.DISPLAY, UiMetrics.HERO, UiMetrics.SCORE]
	assert_eq(steps, [40, 56, 80, 120] as Array[int], "the display scale")
	for size: int in steps:
		var label: Label = autofree(Label.new()) as Label
		UiKit.letter(label, size)
		assert_eq(label.theme_type_variation, &"DisplayLabel", "%d px is in the display face" % size)
		assert_eq(label.get_theme_font_size("font_size"), size)
		assert_eq(label.get_theme_constant("outline_size"), UiSkin.letter_outline(size), "the outline grows with the word")
		assert_eq(label.get_theme_color("font_outline_color"), UiSkin.LETTER_INK, "in the shared ink")
		assert_eq(label.get_theme_constant("shadow_offset_y"), UiSkin.letter_shadow(size), "the shadow drops by its share")
		assert_gt(label.get_theme_constant("shadow_offset_y"), 0, "under the word")
		assert_gt(label.get_theme_color("font_shadow_color").a, 0.0, "and can be seen")
