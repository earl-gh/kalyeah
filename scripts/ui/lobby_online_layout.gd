class_name LobbyOnlineLayout
extends RefCounted
## Online setup layout; room state and networking stay in LobbyScreen.

var screen: LobbyScreen


func _init(p_screen: LobbyScreen) -> void:
	screen = p_screen


func build() -> void:
	var margin: MarginContainer = MarginContainer.new()
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	margin.add_theme_constant_override("margin_left", 20)
	margin.add_theme_constant_override("margin_right", 20)
	margin.add_theme_constant_override("margin_top", 28)
	margin.add_theme_constant_override("margin_bottom", 24)
	screen.add_child(margin)
	screen._setup_layer = margin
	LobbyWidgets.apply_safe_area(margin, UiMetrics.PAGE_MARGIN, UiMetrics.PAGE_MARGIN)
	margin.resized.connect(LobbyWidgets.apply_safe_area.bind(margin, UiMetrics.PAGE_MARGIN, UiMetrics.PAGE_MARGIN))
	var column: VBoxContainer = VBoxContainer.new()
	column.add_theme_constant_override("separation", 16)
	margin.add_child(column)
	column.add_child(LobbyWidgets.header("Create room" if screen.intent == screen.Intent.CREATE else "Join room", screen._on_back))
	var scroll: ScrollContainer = ScrollContainer.new()
	scroll.name = "LobbyScroll"
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	column.add_child(scroll)
	var card: PanelContainer = PanelContainer.new()
	card.name = "LobbyCard"
	card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	card.add_theme_stylebox_override("panel", KalyeahTheme.painted_panel(UiMetrics.PAGE_MARGIN))
	scroll.add_child(card)
	var content: VBoxContainer = VBoxContainer.new()
	content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	content.add_theme_constant_override("separation", 16)
	card.add_child(content)
	_build_setup(content)
	screen._status = LobbyWidgets.label("", UiMetrics.BODY, KalyeahTheme.ERROR)
	screen._setup_status = screen._status
	screen._status.name = "Status"
	screen._status.visible = false
	screen._status.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	column.add_child(screen._status)


func _build_setup(parent: Control) -> void:
	screen._setup = VBoxContainer.new()
	screen._setup.add_theme_constant_override("separation", UiMetrics.GAP)
	parent.add_child(screen._setup)
	# no Name field: your name is the profile's (Edit profile), one place to set it
	if screen.intent == screen.Intent.CREATE:
		screen._setup.add_child(LobbyWidgets.field_label("Team size"))
		var sizes: HBoxContainer = HBoxContainer.new()
		sizes.add_theme_constant_override("separation", 8)
		screen._setup.add_child(sizes)
		for size: int in [1, 2, 3]:
			var button: Button = LobbyWidgets.button("%dv%d" % [size, size], screen.choose_size.bind(size), "grey")
			LobbyWidgets.make_toggle(button)
			button.custom_minimum_size = Vector2(0.0, UiMetrics.TOUCH)
			button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			sizes.add_child(button)
			screen._size_buttons.append(button)
		screen.choose_size(1)
	elif not screen.hotspot:
		screen._setup.add_child(LobbyWidgets.field_label("Room code"))
		screen._code_boxes = UiCodeBoxes.new(screen.NET_RULES.passcode_length, screen.NET_RULES.passcode_alphabet)
		screen._code_edit = screen._code_boxes.edit
		screen._setup.add_child(screen._code_boxes)
		var paste_row: HBoxContainer = HBoxContainer.new()
		paste_row.add_theme_constant_override("separation", UiMetrics.GAP)
		screen._setup.add_child(paste_row)
		var paste: Button = LobbyWidgets.button("Paste the code", screen.paste_code, "grey", UiMetrics.DETAIL)
		paste.name = "PasteButton"
		paste.custom_minimum_size = Vector2(0.0, UiMetrics.MIN_TARGET)
		paste.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		paste_row.add_child(paste)
		var clear: Button = LobbyWidgets.button("Clear", screen.clear_code, "grey", UiMetrics.DETAIL)
		clear.name = "ClearButton"
		clear.custom_minimum_size = Vector2(160.0, UiMetrics.MIN_TARGET)
		paste_row.add_child(clear)
		screen._code_error = LobbyWidgets.label("", UiMetrics.DETAIL, KalyeahTheme.ERROR)
		screen._code_error.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		screen._code_error.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
		screen._code_error.hide()
		screen._setup.add_child(screen._code_error)
		screen._code_boxes.changed.connect(func(_text: String) -> void: screen._code_error.hide())
		screen._code_boxes.completed.connect(func(_text: String) -> void: screen._go_button.grab_focus())
	screen._go_button = LobbyWidgets.button("Create room" if screen.intent == screen.Intent.CREATE else "Join room", screen.submit, "orange")
	screen._go_button.custom_minimum_size = Vector2(0, 96)
	screen._setup.add_child(screen._go_button)
	screen._rejoin_button = LobbyWidgets.button("Rejoin battle", screen.rejoin, "grey")
	screen._rejoin_button.visible = not Session.reconnect_token.is_empty()
	screen._setup.add_child(screen._rejoin_button)
