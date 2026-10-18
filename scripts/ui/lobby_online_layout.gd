class_name LobbyOnlineLayout
extends RefCounted
## Online setup layout; room state and networking stay in LobbyScreen.

var screen: LobbyScreen


func _init(p_screen: LobbyScreen) -> void:
	screen = p_screen


func build() -> void:
	LobbyWidgets.add_backdrop(screen, screen.BACKGROUND)
	var margin: MarginContainer = MarginContainer.new()
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	margin.add_theme_constant_override("margin_left", 20)
	margin.add_theme_constant_override("margin_right", 20)
	margin.add_theme_constant_override("margin_top", 28)
	margin.add_theme_constant_override("margin_bottom", 24)
	screen.add_child(margin)
	var column: VBoxContainer = VBoxContainer.new()
	column.add_theme_constant_override("separation", 16)
	margin.add_child(column)
	column.add_child(LobbyWidgets.header("CREATE ROOM" if screen.intent == screen.Intent.CREATE else "JOIN ROOM", screen._on_back))
	var scroll: ScrollContainer = ScrollContainer.new()
	scroll.name = "LobbyScroll"
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	column.add_child(scroll)
	var card: PanelContainer = PanelContainer.new()
	card.name = "LobbyCard"
	card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	card.add_theme_stylebox_override("panel", KalyeahTheme.painted_panel(20.0))
	scroll.add_child(card)
	var content: VBoxContainer = VBoxContainer.new()
	content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	content.add_theme_constant_override("separation", 16)
	card.add_child(content)
	_build_setup(content)
	_build_room(content)
	screen._status = LobbyWidgets.label("", 24, Color("ffb4a8"))
	screen._status.name = "Status"
	screen._status.custom_minimum_size = Vector2(0.0, 30.0)
	screen._status.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	column.add_child(screen._status)
	var gate: Node = (load("res://scenes/ui/orientation_gate.tscn") as PackedScene).instantiate()
	screen.add_child(gate)


func _build_setup(parent: Control) -> void:
	screen._setup = VBoxContainer.new()
	screen._setup.add_theme_constant_override("separation", 10)
	parent.add_child(screen._setup)
	screen._setup.add_child(LobbyWidgets.field_label("NAME"))
	screen._name_edit = LobbyWidgets.edit(Session.player_name, "Player", screen.FONT)
	screen._name_edit.max_length = 12
	screen._setup.add_child(screen._name_edit)
	if screen.intent == screen.Intent.CREATE:
		screen._setup.add_child(LobbyWidgets.field_label("TEAM SIZE"))
		var sizes: HBoxContainer = HBoxContainer.new()
		sizes.add_theme_constant_override("separation", 8)
		screen._setup.add_child(sizes)
		for size: int in [1, 2, 3]:
			var button: Button = LobbyWidgets.button("%dv%d" % [size, size], screen.choose_size.bind(size), "grey")
			LobbyWidgets.make_toggle(button)
			button.custom_minimum_size = Vector2(0.0, 66.0)
			button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			sizes.add_child(button)
			screen._size_buttons.append(button)
		screen.choose_size(1)
	elif not Session.lobby_hotspot:
		screen._setup.add_child(LobbyWidgets.field_label("ROOM CODE"))
		screen._code_edit = LobbyWidgets.edit("", "ABC234XY", screen.FONT)
		screen._code_edit.max_length = screen.NET_RULES.passcode_length
		screen._code_edit.alignment = HORIZONTAL_ALIGNMENT_CENTER
		screen._code_edit.add_theme_font_override("font", KalyeahTheme.DISPLAY_FONT)
		screen._code_edit.add_theme_font_size_override("font_size", screen.BIG_FONT)
		screen._code_edit.text_changed.connect(func(text: String) -> void:
			var caret: int = screen._code_edit.caret_column
			screen._code_edit.text = text.to_upper()
			screen._code_edit.caret_column = caret)
		screen._setup.add_child(screen._code_edit)
	screen._go_button = LobbyWidgets.button("CREATE" if screen.intent == screen.Intent.CREATE else "JOIN", screen.submit, "orange")
	screen._go_button.custom_minimum_size = Vector2(0, 96)
	screen._setup.add_child(screen._go_button)
	screen._rejoin_button = LobbyWidgets.button("REJOIN", screen.rejoin, "blue")
	screen._rejoin_button.visible = not Session.reconnect_token.is_empty()
	screen._setup.add_child(screen._rejoin_button)


func _build_room(parent: Control) -> void:
	screen._room_box = VBoxContainer.new()
	screen._room_box.add_theme_constant_override("separation", 12)
	parent.add_child(screen._room_box)
	var code_hint: Label = LobbyWidgets.label("SHARE WITH FRIENDS", 20, KalyeahTheme.TEXT_MUTED)
	code_hint.theme_type_variation = &"MutedLabel"
	screen._room_box.add_child(code_hint)
	screen._code_label = LobbyWidgets.label("", screen.BIG_FONT, KalyeahTheme.AMBER)
	screen._code_label.theme_type_variation = &"DisplayLabel"
	screen._code_label.visible = not Session.lobby_hotspot
	screen._room_box.add_child(screen._code_label)
	for team: int in 2:
		var team_card: PanelContainer = PanelContainer.new()
		var team_style: StyleBoxFlat = KalyeahTheme.surface_box(16, 2, 14.0)
		team_style.bg_color = Color(KalyeahTheme.INK, 0.72)
		team_style.border_color = Color(screen.TEAM_COLORS[team], 0.72)
		team_style.shadow_size = 3
		team_card.add_theme_stylebox_override("panel", team_style)
		screen._room_box.add_child(team_card)
		var team_column: VBoxContainer = VBoxContainer.new()
		team_column.add_theme_constant_override("separation", 8)
		team_card.add_child(team_column)
		var header: HBoxContainer = HBoxContainer.new()
		team_column.add_child(header)
		var name_label: Label = LobbyWidgets.label(screen.TEAM_NAMES[team], screen.FONT, screen.TEAM_COLORS[team])
		name_label.theme_type_variation = &"SectionLabel"
		name_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
		header.add_child(name_label)
		var join_team: Button = LobbyWidgets.button("JOIN", screen.client.pick_team.bind(team), "blue" if team == 0 else "red")
		join_team.custom_minimum_size = Vector2(104.0, 56.0)
		header.add_child(join_team)
		screen._team_buttons.append(join_team)
		var list: VBoxContainer = VBoxContainer.new()
		list.add_theme_constant_override("separation", 4)
		team_column.add_child(list)
		screen._team_lists.append(list)
	screen._best_of_label = LobbyWidgets.label("", screen.FONT, KalyeahTheme.CREAM)
	screen._room_box.add_child(screen._best_of_label)
	var counts: HBoxContainer = HBoxContainer.new()
	counts.add_theme_constant_override("separation", 8)
	screen._room_box.add_child(counts)
	for count: int in [3, 5, 7]:
		var choice: Button = LobbyWidgets.button("BO%d" % count, screen.client.set_best_of.bind(count), "grey", 25)
		LobbyWidgets.make_toggle(choice)
		choice.custom_minimum_size = Vector2(0.0, 62.0)
		choice.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		counts.add_child(choice)
		screen._best_of_buttons[count] = choice
	screen._ready_button = LobbyWidgets.button("READY UP", func() -> void: screen.client.set_ready(screen._ready_button.button_pressed), "grey")
	LobbyWidgets.make_toggle(screen._ready_button)
	screen._ready_button.custom_minimum_size = Vector2(0, 96)
	screen._room_box.add_child(screen._ready_button)
	screen._start_button = LobbyWidgets.button("START MATCH", screen.client.start_match, "orange")
	screen._start_button.custom_minimum_size = Vector2(0, 96)
	screen._room_box.add_child(screen._start_button)
	screen._room_box.add_child(LobbyWidgets.button("LEAVE ROOM", screen._on_leave, "red"))
