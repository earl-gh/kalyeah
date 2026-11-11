class_name LoadoutLayout
extends RefCounted
## Responsive 90s Filipino Komiks loadout layout: two-column widescreen (`1280x720`) showing all 10 kids,
## `Q / E / R` superpower sockets, 3-column superpower grid, info panel and Ready button cleanly inside `720px`,
## while preserving single-column vertical layout for `720x1280` portrait screens.


## The height kept for the suggested-loadout button (it never changes the layout when it hides).
const SUGGEST_HEIGHT: float = 80.0
## The kid screen: the chosen kid's name is set large under the carousel of card pictures.
const HERO_NAME_FONT: int = UiMetrics.DISPLAY
## The description under the active kid reserves this many lines of body text (each `ABOUT_LINE_HEIGHT` times the font size).
const ABOUT_LINES: int = 3
const ABOUT_LINE_HEIGHT: float = 1.45
## A phone narrower than the design width wraps it to more lines: it shows at most this many, so the cards keep their room.
const ABOUT_LINES_MAX: int = 4
const WIDE_LEFT_WIDTH: float = 468.0
const WIDE_RING_SIZE: float = 84.0
const WIDE_KID_COLUMNS: int = 5


static func build(screen: WeaponPickScreen, defs: Array[WeaponDef]) -> void:
	for def: WeaponDef in defs:
		screen._defs[def.id] = def
	screen._maxima = WeaponsPage.stat_maxima(defs)
	screen.suggestion = WeaponPick.suggestion_for(defs, screen.slot_count())
	if screen.dimmed:
		var dim: ColorRect = ColorRect.new()
		dim.name = "Dim"
		dim.color = MenuDialog.DIM
		dim.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		screen.add_child(dim)
	else:
		screen.add_child(UiBackdrop.new())
	var margin: MarginContainer = MarginContainer.new()
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	screen.add_child(margin)
	screen._safe_margin = margin
	margin.resized.connect(func() -> void: _apply_margins(screen))
	var column: VBoxContainer = VBoxContainer.new()
	column.name = "RootColumn"
	column.add_theme_constant_override("separation", UiMetrics.GAP)
	margin.add_child(column)
	screen._root_column = column

	var header: HBoxContainer = HBoxContainer.new()
	header.name = "HeaderRow"
	header.add_theme_constant_override("separation", UiMetrics.GAP)
	column.add_child(header)
	screen.back_button = MenuDialog.make_button("Back", "grey", UiMetrics.ACTION, Vector2(120.0, UiMetrics.TOUCH))
	UiSound.set_role(screen.back_button, &"cancel")
	screen.back_button.pressed.connect(screen.request_back)
	screen.back_button.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	header.add_child(screen.back_button)
	var default_title: String = ("Choose %d superpowers" % screen.slot_count()) if screen.slot_count() >= 3 else "Choose 2 weapons"
	screen._title = LoadoutCards.label(default_title, UiMetrics.TITLE, KalyeahTheme.CREAM)
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

	# Widescreen two-column split container (populated when `screen.size.x > screen.size.y`).
	var split_row: HBoxContainer = HBoxContainer.new()
	split_row.name = "SplitBody"
	split_row.size_flags_vertical = Control.SIZE_EXPAND_FILL
	split_row.add_theme_constant_override("separation", UiMetrics.GAP)
	split_row.visible = false
	column.add_child(split_row)
	screen._split_row = split_row
	var left_col: VBoxContainer = VBoxContainer.new()
	left_col.name = "LeftColumn"
	left_col.custom_minimum_size.x = WIDE_LEFT_WIDTH
	left_col.add_theme_constant_override("separation", 6)
	split_row.add_child(left_col)
	screen._left_column = left_col
	var right_col: VBoxContainer = VBoxContainer.new()
	right_col.name = "RightColumn"
	right_col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	right_col.size_flags_vertical = Control.SIZE_EXPAND_FILL
	right_col.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	split_row.add_child(right_col)
	screen._right_column = right_col

	screen._character_row = _characters(screen)
	column.add_child(screen._character_row)

	var sockets: HBoxContainer = HBoxContainer.new()
	sockets.name = "SocketsRow"
	sockets.add_theme_constant_override("separation", 8)
	column.add_child(sockets)
	screen._sockets_row = sockets
	screen._weapon_parts.append(sockets)

	screen._hint = LoadoutCards.label("", UiMetrics.DETAIL, KalyeahTheme.AMBER)
	screen._hint.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	screen._hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	screen._hint.autowrap_mode = TextServer.AUTOWRAP_OFF
	screen._hint.clip_text = true
	column.add_child(screen._hint)
	screen._weapon_parts.append(screen._hint)

	var slot_box: Control = Control.new()
	slot_box.name = "SuggestSlot"
	slot_box.custom_minimum_size = Vector2(0.0, SUGGEST_HEIGHT)
	slot_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	column.add_child(slot_box)
	screen._suggest_slot = slot_box
	screen._weapon_parts.append(slot_box)
	screen._suggest_button = MenuDialog.make_button("", "blue", UiMetrics.DETAIL, Vector2(0.0, SUGGEST_HEIGHT))
	screen._suggest_button.name = "SuggestButton"
	screen._suggest_button.clip_text = true
	screen._suggest_button.visible = false
	screen._suggest_button.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	screen._suggest_button.pressed.connect(screen.use_suggestion)
	slot_box.add_child(screen._suggest_button)

	var grid_scroll: Control = _weapons(screen, defs)
	column.add_child(grid_scroll)
	screen._weapons_scroll = grid_scroll
	screen._weapon_parts.append(grid_scroll)

	var info: Control = LoadoutCards.info_panel(screen)
	column.add_child(info)
	screen._info_panel = info
	screen._weapon_parts.append(info)

	# Cards mode: the superpowers are hidden and this space holds the kid carousel and the kid's name, with Play at the bottom.
	var filler: Control = Control.new()
	filler.name = "Filler"
	filler.size_flags_vertical = Control.SIZE_EXPAND_FILL
	filler.mouse_filter = Control.MOUSE_FILTER_IGNORE
	filler.visible = false
	column.add_child(filler)
	screen._filler = filler
	var hero_box: VBoxContainer = VBoxContainer.new()
	hero_box.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	hero_box.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	hero_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	filler.add_child(hero_box)
	hero_box.alignment = BoxContainer.ALIGNMENT_CENTER
	if screen.dimmed:
		header.visible = false
		heading.remove_child(screen._title)
		screen._title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		hero_box.add_child(screen._title)
	screen._carousel = KidCarousel.new()
	screen._carousel.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	screen._carousel.selected.connect(screen._on_kid_swiped)
	hero_box.add_child(screen._carousel)
	filler.resized.connect(screen._fit_kids)

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
	screen._kid_about_panel = about_panel
	screen._hero_about = UiKit.text("", UiMetrics.BODY, MenuDialog.TEXT_COLOR)
	screen._hero_about.name = "KidAbout"
	screen._hero_about.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	screen._hero_about.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	screen._hero_about.mouse_filter = Control.MOUSE_FILTER_IGNORE
	screen._hero_about.max_lines_visible = ABOUT_LINES_MAX
	screen._hero_about.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	screen._hero_about.custom_minimum_size.y = ABOUT_LINES * UiMetrics.BODY * ABOUT_LINE_HEIGHT
	about_panel.add_child(screen._hero_about)

	screen._ready_button = MenuDialog.make_button("Ready", "orange", UiMetrics.BODY, Vector2(0.0, 88.0))
	screen._ready_button.pressed.connect(screen.press_ready)
	column.add_child(screen._ready_button)

	sync_slots(screen, defs)
	apply_responsive_layout(screen)


static func sync_characters(screen: WeaponPickScreen) -> void:
	var grid: GridContainer = screen._character_row.find_child("CharacterGrid", true, false) as GridContainer
	if grid == null:
		return
	var same: bool = grid.get_child_count() == screen._characters.size()
	if same:
		for i: int in screen._characters.size():
			if not screen._profiles.has(screen._characters[i].id):
				same = false
				break
	if same:
		return
	for child: Node in grid.get_children():
		grid.remove_child(child)
		child.queue_free()
	screen._profiles.clear()
	screen._taken_labels.clear()
	for character: CharacterDef in screen._characters:
		var card: Button = LoadoutCards.profile(screen, character)
		grid.add_child(card)
		screen._profiles[character.id] = card
	screen._laid_out_wide = -1


static func sync_slots(screen: WeaponPickScreen, defs: Array[WeaponDef]) -> void:
	var count: int = screen.slot_count()
	screen.suggestion = WeaponPick.suggestion_for(defs, count)
	var names: Array[String] = []
	for id: StringName in screen.suggestion:
		if screen._defs.has(id):
			names.append((screen._defs[id] as WeaponDef).short_name)
	if screen._suggest_button != null:
		screen._suggest_button.text = "Suggested: %s" % " + ".join(names)
	if screen._sockets.size() == count:
		return
	for child: Node in screen._sockets_row.get_children():
		screen._sockets_row.remove_child(child)
		child.queue_free()
	screen._sockets.clear()
	var keycap_box: StyleBoxFlat = KalyeahTheme.komiks_plate(KalyeahTheme.AMBER, KalyeahTheme.KOMIKS_INK, 2, 0.0, UiSkin.RADIUS_S)
	for slot: int in count:
		var socket: _KomiksSocket = _KomiksSocket.new()
		socket.number = slot + 1
		socket.custom_minimum_size = Vector2(0.0, LoadoutSocket.HEIGHT)
		socket.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		socket.pressed.connect(screen.select_slot.bind(slot))
		if count == 3 and slot < WeaponPickScreen.SLOT_KEYS.size():
			socket.configure_keycap(WeaponPickScreen.SLOT_KEYS[slot], keycap_box)
		screen._sockets_row.add_child(socket)
		screen._sockets.append(socket)


static func _is_widescreen(screen: WeaponPickScreen) -> bool:
	var extent: Vector2 = screen.size
	if extent.x <= 0.0 or extent.y <= 0.0:
		var vp: Viewport = screen.get_viewport()
		extent = vp.get_visible_rect().size if vp != null else Vector2(Settings.LANDSCAPE_BASE)
	return extent.x > extent.y


static func _apply_margins(screen: WeaponPickScreen) -> void:
	if screen._safe_margin == null:
		return
	if _is_widescreen(screen):
		LobbyWidgets.apply_safe_area(screen._safe_margin, 16, 8)
	else:
		LobbyWidgets.apply_safe_area(screen._safe_margin, UiMetrics.PAGE_MARGIN, UiMetrics.PAGE_MARGIN)


static func _move_to(node: Control, target_parent: Control) -> void:
	if node == null or target_parent == null:
		return
	var current_parent: Node = node.get_parent()
	if current_parent != target_parent:
		if current_parent != null:
			current_parent.remove_child(node)
		target_parent.add_child(node)


static func apply_responsive_layout(screen: WeaponPickScreen) -> void:
	if screen._root_column == null or screen._split_row == null:
		return
	_apply_margins(screen)
	var wide: bool = _is_widescreen(screen)
	var mode_code: int = (2 if screen.cards_mode else 1) if wide else 0
	var grid: GridContainer = screen._character_row.find_child("CharacterGrid", true, false) as GridContainer
	var char_scroll: ScrollContainer = screen._character_row.find_child("Characters", true, false) as ScrollContainer
	var desc_label: Label = screen._info_panel.find_child("Description", true, false) as Label if screen._info_panel != null else null
	var info_margin: MarginContainer = screen._info_panel.get_child(0) as MarginContainer if (screen._info_panel != null and screen._info_panel.get_child_count() > 0) else null

	screen._root_column.add_theme_constant_override("separation", 8 if wide else UiMetrics.GAP)
	if screen._ring != null:
		var ring_px: float = WIDE_RING_SIZE if wide else CountdownRing.LOADOUT_SIZE
		screen._ring.custom_minimum_size = Vector2(ring_px, ring_px)
	if screen._hero_about != null:
		screen._hero_about.custom_minimum_size.y = (2.0 * float(UiMetrics.BODY) * 1.3) if wide else (float(ABOUT_LINES) * float(UiMetrics.BODY) * ABOUT_LINE_HEIGHT)

	if wide and not screen.cards_mode:
		screen._split_row.visible = true
		if grid != null and char_scroll != null:
			grid.columns = WIDE_KID_COLUMNS
			grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			char_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
			char_scroll.vertical_scroll_mode = ScrollContainer.SCROLL_MODE_AUTO
			var rows: int = clampi(ceili(float(maxi(screen._characters.size(), 1)) / float(WIDE_KID_COLUMNS)), 1, 2)
			char_scroll.custom_minimum_size.y = float(rows) * WeaponPickScreen.WIDE_PROFILE_SIZE.y + float(maxi(rows - 1, 0) * WeaponPickScreen.PROFILE_GAP) + 4.0
			for card: Button in screen._profiles.values():
				card.custom_minimum_size = WeaponPickScreen.WIDE_PROFILE_SIZE
				card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		if desc_label != null:
			desc_label.custom_minimum_size.y = 54.0
		if info_margin != null:
			for side: String in ["left", "right", "top", "bottom"]:
				info_margin.add_theme_constant_override("margin_" + side, 12)
		if screen._laid_out_wide != mode_code:
			_move_to(screen._character_row, screen._left_column)
			_move_to(screen._hint, screen._left_column)
			_move_to(screen._suggest_slot, screen._left_column)
			_move_to(screen._info_panel, screen._left_column)
			_move_to(screen._sockets_row, screen._right_column)
			_move_to(screen._weapons_scroll, screen._right_column)
			_move_to(screen._ready_button, screen._right_column)
			screen._laid_out_wide = mode_code
	else:
		screen._split_row.visible = false
		if grid != null and char_scroll != null:
			grid.columns = maxi(screen._characters.size(), 1)
			grid.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
			char_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_AUTO
			char_scroll.vertical_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
			char_scroll.custom_minimum_size.y = WeaponPickScreen.PROFILE_SIZE.y + 12.0
			for card: Button in screen._profiles.values():
				card.custom_minimum_size = WeaponPickScreen.PROFILE_SIZE
				card.size_flags_horizontal = Control.SIZE_FILL
		if desc_label != null:
			desc_label.custom_minimum_size.y = 2.0 * 36.0
		if info_margin != null:
			for side: String in ["left", "right", "top", "bottom"]:
				info_margin.add_theme_constant_override("margin_" + side, 16)
		if screen._laid_out_wide != mode_code:
			_move_to(screen._character_row, screen._root_column)
			_move_to(screen._sockets_row, screen._root_column)
			_move_to(screen._hint, screen._root_column)
			_move_to(screen._suggest_slot, screen._root_column)
			_move_to(screen._weapons_scroll, screen._root_column)
			_move_to(screen._info_panel, screen._root_column)
			_move_to(screen._filler, screen._root_column)
			_move_to(screen._ready_button, screen._root_column)
			screen._laid_out_wide = mode_code


static func _characters(screen: WeaponPickScreen) -> Control:
	var column: VBoxContainer = VBoxContainer.new()
	column.name = "CharacterSection"
	column.add_theme_constant_override("separation", 6)
	var scroll: ScrollContainer = ScrollContainer.new()
	scroll.name = "Characters"
	scroll.custom_minimum_size.y = screen.PROFILE_SIZE.y + 12.0
	scroll.vertical_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	scroll.follow_focus = true
	column.add_child(scroll)
	var grid: GridContainer = GridContainer.new()
	grid.name = "CharacterGrid"
	grid.columns = maxi(screen._characters.size(), 1)
	grid.add_theme_constant_override("h_separation", WeaponPickScreen.PROFILE_GAP)
	grid.add_theme_constant_override("v_separation", WeaponPickScreen.PROFILE_GAP)
	scroll.add_child(grid)
	for character: CharacterDef in screen._characters:
		var card: Button = LoadoutCards.profile(screen, character)
		grid.add_child(card)
		screen._profiles[character.id] = card
	return column


## The superpowers as a 3-column grid of tiles that scrolls smoothly while sockets and Ready stay put.
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


class _KomiksSocket extends LoadoutSocket:
	const COMPACT_ART_LEFT: float = 10.0
	const COMPACT_ART_WIDTH: float = 60.0
	const COMPACT_TEXT_LEFT: float = 78.0
	var key_hint: String = ""
	var keycap_style: StyleBoxFlat = null

	func configure_keycap(p_key: String, p_style: StyleBoxFlat) -> void:
		key_hint = p_key
		keycap_style = p_style
		if not key_hint.is_empty():
			art.position = Vector2(COMPACT_ART_LEFT, ART_PAD)
			art.size = Vector2(COMPACT_ART_WIDTH, HEIGHT - ART_PAD * 2.0)

	func _draw() -> void:
		if key_hint.is_empty():
			super._draw()
			return
		draw_style_box(UiSkin.plate("picked" if active else "inset", 0.0, UiSkin.RADIUS_L), Rect2(Vector2.ZERO, size))
		var font: Font = KalyeahTheme.DISPLAY_FONT
		var key_rect: Rect2 = Rect2(size.x - 36.0, 8.0, 28.0, 26.0)
		if keycap_style != null:
			draw_style_box(keycap_style, key_rect)
		draw_string(font, Vector2(key_rect.position.x, key_rect.position.y + 20.0), key_hint, HORIZONTAL_ALIGNMENT_CENTER, key_rect.size.x, UiMetrics.MIN_TEXT, KalyeahTheme.KOMIKS_INK)
		var slot_room: float = maxf(key_rect.position.x - COMPACT_TEXT_LEFT - 6.0, 48.0)
		var slot_color: Color = KalyeahTheme.AMBER if active else KalyeahTheme.TEXT_MUTED
		draw_string(font, Vector2(COMPACT_TEXT_LEFT, 29.0), "SLOT %d" % number, HORIZONTAL_ALIGNMENT_LEFT, slot_room, UiMetrics.MIN_TEXT, slot_color)
		var text_room: float = maxf(size.x - COMPACT_TEXT_LEFT - 8.0, 56.0)
		if weapon == null:
			draw_string(font, Vector2(COMPACT_TEXT_LEFT, 68.0), "Empty", HORIZONTAL_ALIGNMENT_LEFT, text_room, UiMetrics.ACTION, KalyeahTheme.TEXT_MUTED)
			return
		var full_name: String = weapon.display_name
		if font.get_string_size(full_name, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.MIN_TEXT).x <= text_room - 2.0:
			draw_string(font, Vector2(COMPACT_TEXT_LEFT, 68.0), full_name, HORIZONTAL_ALIGNMENT_LEFT, text_room, UiMetrics.MIN_TEXT, KalyeahTheme.TEXT)
			return
		if full_name.contains(" · "):
			var parts: PackedStringArray = full_name.split(" · ", false, 1)
			var game_word: String = parts[0]
			var power_word: String = parts[1]
			if font.get_string_size(power_word, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.MIN_TEXT).x > text_room - 4.0 and not weapon.short_name.is_empty():
				power_word = weapon.short_name
			draw_string(font, Vector2(COMPACT_TEXT_LEFT, 57.0), game_word, HORIZONTAL_ALIGNMENT_LEFT, text_room, UiMetrics.MIN_TEXT, KalyeahTheme.CREAM)
			draw_string(font, Vector2(COMPACT_TEXT_LEFT, 82.0), power_word, HORIZONTAL_ALIGNMENT_LEFT, text_room, UiMetrics.MIN_TEXT, KalyeahTheme.TEXT)
			return
		var short_fallback: String = weapon.short_name if not weapon.short_name.is_empty() else full_name
		draw_string(font, Vector2(COMPACT_TEXT_LEFT, 68.0), short_fallback, HORIZONTAL_ALIGNMENT_LEFT, text_room, UiMetrics.MIN_TEXT, KalyeahTheme.TEXT)
