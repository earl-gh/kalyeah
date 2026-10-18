class_name RoomHeader
extends RefCounted
## Accessible hotspot header. Only Online exposes a room code.

static func build(view: HotspotRoomView) -> Control:
	var row: HBoxContainer = HBoxContainer.new()
	row.name = "TopRow"
	row.add_theme_constant_override("separation", 12)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	view.back_button = MenuDialog.make_button("BACK", "grey", 22, Vector2(120.0, 88.0))
	view.back_button.name = "BackButton"
	view.back_button.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	view.back_button.pressed.connect(view.back_pressed.emit)
	row.add_child(view.back_button)
	var title: Label = Label.new()
	title.name = "Title"
	title.text = "LOBBY"
	title.theme_type_variation = &"DisplayLabel"
	title.add_theme_font_size_override("font_size", 30)
	title.add_theme_color_override("font_color", KalyeahTheme.CREAM)
	title.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	row.add_child(title)
	var bar: Label = Label.new()
	bar.name = "Divider"
	bar.text = "|"
	bar.theme_type_variation = &"DisplayLabel"
	bar.add_theme_font_size_override("font_size", 38)
	bar.add_theme_color_override("font_color", Color(1.0, 1.0, 1.0, 0.45))
	bar.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	bar.visible = false
	row.add_child(bar)
	view.code_label = Label.new()
	view.code_label.name = "Code"
	view.code_label.theme_type_variation = &"DisplayLabel"
	view.code_label.add_theme_font_size_override("font_size", 38)
	view.code_label.add_theme_color_override("font_color", KalyeahTheme.AMBER)
	view.code_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	view.code_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	view.code_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	view.code_label.text = ""
	view.code_label.visible = false
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(view.code_label)
	# settings: the icon alone, no button container
	view.settings_button = Button.new()
	view.settings_button.name = "SettingsButton"
	view.settings_button.flat = true
	view.settings_button.focus_mode = Control.FOCUS_NONE
	view.settings_button.custom_minimum_size = Vector2(88.0, 88.0)
	view.settings_button.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	for state: String in ["normal", "hover", "pressed", "focus"]:
		view.settings_button.add_theme_stylebox_override(state, StyleBoxEmpty.new())
	var centre: CenterContainer = CenterContainer.new()
	centre.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	centre.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var icon: NavIcon = NavIcon.new(&"settings")
	icon.color = KalyeahTheme.CREAM
	centre.add_child(icon)
	view.settings_button.add_child(centre)
	view.settings_button.pressed.connect(view.settings_pressed.emit)
	row.add_child(view.settings_button)
	return row


## Section 1: the map card, 2:1, filling its own row, no buttons. The host taps it to open the
## carousel of maps. Under it, one row: 1v1, 2v2, 3v3.
