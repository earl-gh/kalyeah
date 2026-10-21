class_name LoadoutLayout
extends RefCounted
## Portrait loadout layout; sockets and confirmation stay visible while weapons scroll.


static func build(screen: WeaponPickScreen, defs: Array[WeaponDef]) -> void:
	for def: WeaponDef in defs:
		screen._defs[def.id] = def
	screen._maxima = WeaponsPage.stat_maxima(defs)
	screen.suggestion = WeaponPick.suggestion(defs)
	screen.add_child(UiBackdrop.new())
	var margin: MarginContainer = MarginContainer.new()
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	screen.add_child(margin)
	screen._safe_margin = margin
	margin.resized.connect(LobbyWidgets.apply_safe_area.bind(margin, UiMetrics.PAGE_MARGIN, UiMetrics.PAGE_MARGIN))
	var column: VBoxContainer = VBoxContainer.new()
	column.add_theme_constant_override("separation", UiMetrics.GAP)
	margin.add_child(column)
	var header: HBoxContainer = HBoxContainer.new()
	header.add_theme_constant_override("separation", UiMetrics.GAP)
	column.add_child(header)
	screen.back_button = MenuDialog.make_button("Back", "grey", UiMetrics.ACTION, Vector2(120.0, UiMetrics.TOUCH))
	screen.back_button.pressed.connect(screen.request_back)
	screen.back_button.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	header.add_child(screen.back_button)
	screen._title = LoadoutCards.label("Choose 2 weapons", UiMetrics.TITLE, KalyeahTheme.CREAM)
	screen._title.theme_type_variation = &"DisplayLabel"
	screen._title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var heading: VBoxContainer = VBoxContainer.new()
	heading.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	heading.add_theme_constant_override("separation", 4)
	heading.add_child(screen._title)
	screen._mode_label = LoadoutCards.label("", UiMetrics.CAPTION, KalyeahTheme.TEXT_MUTED)
	heading.add_child(screen._mode_label)
	header.add_child(heading)
	screen._ring = CountdownRing.new()
	screen._ring.custom_minimum_size = Vector2(CountdownRing.LOADOUT_SIZE, CountdownRing.LOADOUT_SIZE)
	screen._ring.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	header.add_child(screen._ring)
	screen._timer = Label.new()
	screen._timer.visible = false
	header.add_child(screen._timer)
	screen._character_row = _characters(screen)
	column.add_child(screen._character_row)
	var sockets: HBoxContainer = HBoxContainer.new()
	sockets.add_theme_constant_override("separation", UiMetrics.GAP)
	column.add_child(LobbyWidgets.group(sockets, UiMetrics.GAP))
	for slot: int in WeaponPick.SLOTS:
		var socket: LoadoutSocket = LoadoutSocket.new()
		socket.number = slot + 1
		socket.custom_minimum_size = Vector2(0.0, 96.0)
		socket.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		socket.pressed.connect(screen.select_slot.bind(slot))
		sockets.add_child(socket)
		screen._sockets.append(socket)
	# the hint on the left and, for a player with no picks yet, a suggested pair on the right (no extra height)
	var hint_row: HBoxContainer = HBoxContainer.new()
	hint_row.add_theme_constant_override("separation", UiMetrics.GAP)
	column.add_child(hint_row)
	screen._hint = LoadoutCards.label("", 24, KalyeahTheme.AMBER)
	screen._hint.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	screen._hint.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	hint_row.add_child(screen._hint)
	var pair_names: Array[String] = []
	for id: StringName in screen.suggestion:
		pair_names.append((screen._defs[id] as WeaponDef).short_name)
	screen._suggest_button = MenuDialog.make_button("Suggested: %s" % " + ".join(pair_names), "blue", 24, Vector2(0.0, 64.0))
	screen._suggest_button.name = "SuggestButton"
	screen._suggest_button.visible = false
	screen._suggest_button.pressed.connect(screen.use_suggestion)
	hint_row.add_child(screen._suggest_button)
	column.add_child(_weapons(screen, defs))
	column.add_child(LoadoutCards.info_panel(screen))
	screen._ready_button = MenuDialog.make_button("Ready", "orange", 30, Vector2(0.0, 88.0))
	screen._ready_button.pressed.connect(screen.press_ready)
	column.add_child(screen._ready_button)


static func _characters(screen: WeaponPickScreen) -> Control:
	var column: VBoxContainer = VBoxContainer.new()
	column.add_theme_constant_override("separation", 8)
	var scroll: ScrollContainer = ScrollContainer.new()
	scroll.name = "Characters"
	scroll.custom_minimum_size.y = screen.PROFILE_SIZE.y + 12.0
	scroll.vertical_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	scroll.follow_focus = true
	column.add_child(scroll)
	var row: HBoxContainer = HBoxContainer.new()
	row.add_theme_constant_override("separation", 10)
	scroll.add_child(row)
	for character: CharacterDef in screen._characters:
		var card: Button = LoadoutCards.profile(screen, character)
		row.add_child(card)
		screen._profiles[character.id] = card
	return column


## The weapons as a grid of tiles that scrolls: three across, so six to eight can be compared at once. The slots
## above and the Ready button below stay put.
static func _weapons(screen: WeaponPickScreen, defs: Array[WeaponDef]) -> Control:
	var scroll: ScrollContainer = ScrollContainer.new()
	scroll.name = "Weapons"
	scroll.follow_focus = true
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	var grid: GridContainer = GridContainer.new()
	grid.name = "WeaponGrid"
	grid.columns = WeaponPickScreen.GRID_COLUMNS
	grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	grid.add_theme_constant_override("h_separation", UiMetrics.COMPACT_GAP)
	grid.add_theme_constant_override("v_separation", UiMetrics.COMPACT_GAP)
	scroll.add_child(grid)
	for def: WeaponDef in defs:
		grid.add_child(LoadoutCards.weapon(screen, def))
	return scroll
