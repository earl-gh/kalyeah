class_name LoadoutLayout
extends RefCounted
## Portrait loadout layout; sockets and confirmation stay visible while weapons scroll.


## The height kept for the suggested-pair button (it never changes the layout when it hides).
const SUGGEST_HEIGHT: float = 80.0
## The kid screen: the chosen kid's name is set large under the carousel of card pictures.
const HERO_NAME_FONT: int = UiMetrics.DISPLAY
## The description under the active kid reserves this many lines of body text (each `ABOUT_LINE_HEIGHT` times the font size).
const ABOUT_LINES: int = 3
const ABOUT_LINE_HEIGHT: float = 1.45
## A phone narrower than the design width wraps it to more lines: it shows at most this many (the rest is cut with "..."), so the cards keep their room.
const ABOUT_LINES_MAX: int = 4


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
	column.add_child(sockets)  # the sockets are the only plates: no second one around them
	screen._weapon_parts.append(sockets)
	for slot: int in WeaponPick.SLOTS:
		var socket: LoadoutSocket = LoadoutSocket.new()
		socket.number = slot + 1
		socket.custom_minimum_size = Vector2(0.0, LoadoutSocket.HEIGHT)
		socket.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		socket.pressed.connect(screen.select_slot.bind(slot))
		sockets.add_child(socket)
		screen._sockets.append(socket)
	# the hint on its own line, and under it a slot of fixed height for the suggested pair: the slot stays when the button is hidden,
	# so picking or un-picking a weapon never moves the grid under the finger
	screen._hint = LoadoutCards.label("", UiMetrics.DETAIL, KalyeahTheme.AMBER)
	screen._hint.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	screen._hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	screen._hint.autowrap_mode = TextServer.AUTOWRAP_OFF
	screen._hint.clip_text = true
	column.add_child(screen._hint)
	screen._weapon_parts.append(screen._hint)
	var pair_names: Array[String] = []
	for id: StringName in screen.suggestion:
		pair_names.append((screen._defs[id] as WeaponDef).short_name)
	var slot_box: Control = Control.new()
	slot_box.name = "SuggestSlot"
	slot_box.custom_minimum_size = Vector2(0.0, SUGGEST_HEIGHT)
	slot_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	column.add_child(slot_box)
	screen._weapon_parts.append(slot_box)
	screen._suggest_button = MenuDialog.make_button("Suggested: %s" % " + ".join(pair_names), "blue", UiMetrics.DETAIL, Vector2(0.0, SUGGEST_HEIGHT))
	screen._suggest_button.name = "SuggestButton"
	screen._suggest_button.visible = false
	screen._suggest_button.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	screen._suggest_button.pressed.connect(screen.use_suggestion)
	slot_box.add_child(screen._suggest_button)
	var grid_scroll: Control = _weapons(screen, defs)
	column.add_child(grid_scroll)
	screen._weapon_parts.append(grid_scroll)
	var info: Control = LoadoutCards.info_panel(screen)
	column.add_child(info)
	screen._weapon_parts.append(info)
	# cards mode: the weapons are gone and this space holds the kid carousel and the kid's name, with Play at the bottom
	var filler: Control = Control.new()
	filler.name = "Filler"
	filler.size_flags_vertical = Control.SIZE_EXPAND_FILL
	filler.mouse_filter = Control.MOUSE_FILTER_IGNORE
	filler.visible = false
	column.add_child(filler)
	screen._filler = filler
	var hero_box: VBoxContainer = VBoxContainer.new()
	hero_box.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	hero_box.add_theme_constant_override("separation", UiMetrics.GAP)
	hero_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	filler.add_child(hero_box)
	hero_box.alignment = BoxContainer.ALIGNMENT_CENTER
	screen._carousel = KidCarousel.new()
	screen._carousel.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	screen._carousel.selected.connect(screen._on_kid_swiped)
	hero_box.add_child(screen._carousel)
	filler.resized.connect(screen._fit_kids)
	# under the active card, like the Cards drawer: the kid's name, then a short description in the drawer's light body
	screen._hero_info = VBoxContainer.new()
	screen._hero_info.name = "KidInfo"
	screen._hero_info.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	screen._hero_info.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hero_box.add_child(screen._hero_info)
	screen._hero_info.minimum_size_changed.connect(screen._fit_kids.call_deferred)
	screen._hero_name = Label.new()
	screen._hero_name.name = "HeroName"
	screen._hero_name.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	UiKit.letter(screen._hero_name, HERO_NAME_FONT, RoyaleStyle.GOLD)
	screen._hero_name.mouse_filter = Control.MOUSE_FILTER_IGNORE
	screen._hero_info.add_child(screen._hero_name)
	var about_panel: PanelContainer = PanelContainer.new()
	about_panel.name = "KidAboutPanel"
	about_panel.add_theme_stylebox_override("panel", KalyeahTheme.modal_body(UiMetrics.PADDING))
	about_panel.theme = KalyeahTheme.modal_body_theme()
	about_panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	screen._hero_info.add_child(about_panel)
	screen._hero_about = UiKit.text("", UiMetrics.BODY, MenuDialog.TEXT_COLOR)
	screen._hero_about.name = "KidAbout"
	screen._hero_about.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	screen._hero_about.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	screen._hero_about.mouse_filter = Control.MOUSE_FILTER_IGNORE
	screen._hero_about.max_lines_visible = ABOUT_LINES_MAX
	screen._hero_about.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	# room for the longest description, so a swipe never moves the cards
	screen._hero_about.custom_minimum_size.y = ABOUT_LINES * UiMetrics.BODY * ABOUT_LINE_HEIGHT
	about_panel.add_child(screen._hero_about)
	screen._ready_button = MenuDialog.make_button("Ready", "orange", UiMetrics.BODY, Vector2(0.0, 88.0))
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
	row.add_theme_constant_override("separation", WeaponPickScreen.PROFILE_GAP)
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

