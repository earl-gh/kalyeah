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
	var bar: UiTabBar = autofree(UiTabBar.new(TitleScreen.TABS)) as UiTabBar
	add_child(bar)
	assert_eq(bar.tabs.size(), 4)
	var heard: Array[StringName] = []
	bar.tab_pressed.connect(func(id: StringName) -> void: heard.append(id))
	bar.set_active(&"history")
	assert_eq(bar.active, &"history")
	var lit: StyleBox = bar.tabs[&"history"].get_theme_stylebox("normal")
	assert_true(lit is StyleBoxTexture, "the open tab is a raised plate")
	assert_true(bar.tabs[&"codex"].get_theme_stylebox("normal") is StyleBoxEmpty, "the others are quiet")
	bar.tabs[&"settings"].pressed.emit()
	assert_eq(heard, [&"settings"] as Array[StringName])
	assert_gte(bar.custom_minimum_size.y, 112.0, "a deep bar: easy to hit with a thumb")


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
