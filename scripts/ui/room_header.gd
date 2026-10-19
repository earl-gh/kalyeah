class_name RoomHeader
extends RefCounted
## Shared room navigation and Online-only code sharing.

static func build(view: HotspotRoomView) -> Control:
	var row: HBoxContainer = HBoxContainer.new()
	row.name = "TopRow"
	row.add_theme_constant_override("separation", 12)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	view.back_button = MenuDialog.make_button("BACK", "grey", UiMetrics.ACTION, Vector2(120.0, 88.0))
	view.back_button.name = "BackButton"
	view.back_button.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	view.back_button.pressed.connect(view.back_pressed.emit)
	row.add_child(view.back_button)
	var title: Label = Label.new()
	title.name = "Title"
	title.text = "LOBBY"
	title.theme_type_variation = &"DisplayLabel"
	title.add_theme_font_size_override("font_size", UiMetrics.TITLE)
	title.add_theme_color_override("font_color", KalyeahTheme.CREAM)
	title.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	row.add_child(title)
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	# settings: the icon alone, no button container
	view.settings_button = Button.new()
	view.settings_button.name = "SettingsButton"
	view.settings_button.flat = true
	view.settings_button.tooltip_text = "Settings"
	view.settings_button.custom_minimum_size = Vector2(88.0, 88.0)
	view.settings_button.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	for state: String in ["normal", "hover", "pressed"]:
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


static func code_row(view: HotspotRoomView) -> HBoxContainer:
	var row: HBoxContainer = HBoxContainer.new()
	row.name = "RoomCode"
	row.add_theme_constant_override("separation", UiMetrics.GAP)
	var caption: Label = LobbyWidgets.label("Code", UiMetrics.DETAIL, KalyeahTheme.TEXT_MUTED)
	row.add_child(caption)
	view.code_label = LobbyWidgets.label("", UiMetrics.BODY, KalyeahTheme.TEXT)
	view.code_label.name = "Code"
	view.code_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	view.code_label.add_theme_font_override("font", KalyeahTheme.DISPLAY_FONT)
	row.add_child(view.code_label)
	view.copy_button = MenuDialog.make_button("COPY", "grey", UiMetrics.ACTION, Vector2(128.0, UiMetrics.TOUCH))
	view.copy_button.tooltip_text = "Copy room code"
	view.copy_button.pressed.connect(view.copy_code)
	row.add_child(view.copy_button)
	view._copy_timer = Timer.new()
	view._copy_timer.one_shot = true
	view._copy_timer.wait_time = 2.0
	view._copy_timer.timeout.connect(func() -> void: view.copy_button.text = "COPY")
	view.add_child(view._copy_timer)
	row.visible = false
	return row
