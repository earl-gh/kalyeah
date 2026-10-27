class_name LoadoutCards
extends RefCounted
## Shared loadout cards: owner art, readable effects and explicit weapon statistics.


## A weapon tile of the grid: the card picture (`CARD_HEIGHT` tall), then the name (two short lines) and the kind. The grid's `WeaponPickScreen.TILE_HEIGHT`
## is the least a tile may be; the picture makes it this much.
const CARD_HEIGHT: float = 150.0
const NAME_HEIGHT: float = 62.0
const TILE_PAD: float = 12.0
const TILE_HEIGHT: float = TILE_PAD * 2.0 + CARD_HEIGHT + 2.0 + NAME_HEIGHT + 2.0 + 30.0


static func label(text: String, size: int, color: Color = KalyeahTheme.TEXT) -> Label:
	var result: Label = Label.new()
	result.text = text
	result.mouse_filter = Control.MOUSE_FILTER_IGNORE
	result.add_theme_font_size_override("font_size", size)
	result.add_theme_color_override("font_color", color)
	result.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	return result


static func box(fill: Color, border: Color, width: int = 2) -> StyleBoxFlat:
	var result: StyleBoxFlat = KalyeahTheme.surface_box(UiSkin.RADIUS_M, width, 0.0)
	result.bg_color = fill
	result.border_color = border
	return result


static func profile(screen: WeaponPickScreen, character: CharacterDef) -> Button:
	var button: Button = Button.new()
	button.name = "Profile_" + String(character.id)
	button.accessibility_name = "Play as %s" % character.display_name
	button.toggle_mode = true
	button.custom_minimum_size = screen.PROFILE_SIZE
	button.clip_contents = true
	for state: String in ["normal", "hover"]:
		button.add_theme_stylebox_override(state, UiSkin.plate("glass", 0.0, UiSkin.RADIUS_M))
	for state: String in ["pressed", "hover_pressed"]:
		button.add_theme_stylebox_override(state, UiSkin.plate("picked", 0.0, UiSkin.RADIUS_M))
	var art: TextureRect = TextureRect.new()
	art.texture = KidModel.card_face(character.id)
	if art.texture == null:
		art.texture = KidModel.portrait(character.id)
	art.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	art.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	art.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	art.offset_left = 5.0
	art.offset_right = -5.0
	art.offset_top = 5.0
	art.offset_bottom = -36.0
	art.mouse_filter = Control.MOUSE_FILTER_IGNORE
	button.add_child(art)
	var name_label: Label = label(character.display_name, UiMetrics.CAPTION)
	name_label.autowrap_mode = TextServer.AUTOWRAP_OFF
	name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	name_label.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	name_label.offset_top = -36.0
	button.add_child(name_label)
	var taken: Label = label("Teammate", UiMetrics.CAPTION)
	taken.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	taken.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	taken.offset_left = -60.0
	taken.offset_right = 60.0
	taken.offset_top = -20.0
	taken.offset_bottom = 20.0
	taken.add_theme_stylebox_override("normal", box(KalyeahTheme.INK_DEEP, KalyeahTheme.SLATE))
	taken.visible = false
	button.add_child(taken)
	screen._taken_labels[character.id] = taken
	button.pressed.connect(screen.pick_character.bind(character.id))
	return button


## One weapon as a tile in the grid: its card picture (bare, with a gold edge once picked), its name and its kind, with the slot it fills (1 or 2) in the
## corner once picked. Tapping it picks it and shows it in the info panel (`WeaponPickScreen.tap`).
static func weapon(screen: WeaponPickScreen, def: WeaponDef) -> Button:
	var button: Button = Button.new()
	button.name = "Weapon_" + String(def.id)
	button.accessibility_name = "%s, %s" % [def.display_name, def.kind_label()]
	button.accessibility_description = "Picks it for a slot"
	button.toggle_mode = true
	button.custom_minimum_size = Vector2(0.0, maxf(screen.TILE_HEIGHT, TILE_HEIGHT))
	button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	for state: String in ["normal", "hover"]:
		button.add_theme_stylebox_override(state, UiSkin.plate("glass", 0.0, UiSkin.RADIUS_L))
	for state: String in ["pressed", "hover_pressed"]:
		button.add_theme_stylebox_override(state, UiSkin.plate("picked", 0.0, UiSkin.RADIUS_L))
	var margin: MarginContainer = MarginContainer.new()
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for side: String in ["left", "right", "top", "bottom"]:
		margin.add_theme_constant_override("margin_" + side, int(TILE_PAD))
	margin.mouse_filter = Control.MOUSE_FILTER_IGNORE
	button.add_child(margin)
	var column: VBoxContainer = VBoxContainer.new()
	column.add_theme_constant_override("separation", 2)
	column.alignment = BoxContainer.ALIGNMENT_CENTER
	column.mouse_filter = Control.MOUSE_FILTER_IGNORE
	margin.add_child(column)
	var art: SkillCardArt = SkillCardArt.new()
	art.name = "Art"
	art.custom_minimum_size = SkillCardArt.size_for_height(CARD_HEIGHT)
	art.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	art.show_weapon(def.id)
	art.follow(button)
	column.add_child(art)
	var title: Label = label(def.display_name, UiMetrics.MIN_TEXT)
	title.name = "WeaponName"
	title.add_theme_font_override("font", KalyeahTheme.BUTTON_FONT)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	title.custom_minimum_size.y = NAME_HEIGHT
	column.add_child(title)
	var kind: Label = label(String(WeaponsPage.KIND_NAMES.get(def.kind, "")), UiMetrics.MIN_TEXT, WeaponPickScreen.kind_color(def.kind))
	kind.name = "Kind"
	kind.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(kind)
	var badge: Label = label("1", UiMetrics.ACTION, KalyeahTheme.INK)
	badge.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	badge.add_theme_stylebox_override("normal", box(screen.PICKED_COLOR, screen.INK))
	badge.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
	badge.offset_left = -42.0
	badge.offset_right = -6.0
	badge.offset_top = 6.0
	badge.offset_bottom = 42.0
	badge.visible = false
	button.add_child(badge)
	button.pressed.connect(screen.tap.bind(def.id))
	screen._buttons[def.id] = button
	screen._badges[def.id] = badge
	return button


## The stats of `def` as one row of short meters, each under its caption (Damage, Cooldown, Range), scaled by
## `maxima` like the Book's bars: a full meter is the biggest number of any weapon.
static func bar_strip(def: WeaponDef, maxima: Dictionary) -> HBoxContainer:
	var row: HBoxContainer = HBoxContainer.new()
	row.name = "Bars"
	row.add_theme_constant_override("separation", UiMetrics.GAP)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	for figure: Dictionary in WeaponsPage.figures_of(def):
		var caption: String = String(figure["caption"])
		var column: VBoxContainer = VBoxContainer.new()
		column.name = "Stat_%s" % caption
		column.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		column.add_theme_constant_override("separation", 2)
		column.mouse_filter = Control.MOUSE_FILTER_IGNORE
		column.add_child(label(caption, UiMetrics.MIN_TEXT, Color("9fb0d6")))
		column.add_child(WeaponsPage.meter_bar(caption, WeaponsPage.fraction_of(caption, def, maxima), 14.0))
		row.add_child(column)
	return row


## The panel under the grid: the weapon you touched last, with its description, its bars and its exact numbers.
## Filled by `show_info`; the same bars as the Book's sheet, so a weapon looks the same wherever you read it.
static func info_panel(screen: WeaponPickScreen) -> Control:
	var panel: PanelContainer = PanelContainer.new()
	panel.name = "WeaponInfo"
	panel.add_theme_stylebox_override("panel", UiSkin.plate("night", 0.0, UiSkin.RADIUS_L))
	var margin: MarginContainer = MarginContainer.new()
	for side: String in ["left", "right", "top", "bottom"]:
		margin.add_theme_constant_override("margin_" + side, 16)
	panel.add_child(margin)
	var column: VBoxContainer = VBoxContainer.new()
	column.add_theme_constant_override("separation", 6)
	margin.add_child(column)
	var head: HBoxContainer = HBoxContainer.new()
	head.add_theme_constant_override("separation", UiMetrics.GAP)
	column.add_child(head)
	screen._info_name = label("", UiMetrics.ACTION, KalyeahTheme.CREAM)
	screen._info_name.name = "InfoName"
	screen._info_name.add_theme_font_override("font", KalyeahTheme.BUTTON_FONT)
	screen._info_name.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	head.add_child(screen._info_name)
	screen._info_kind = label("", UiMetrics.MIN_TEXT)
	screen._info_kind.name = "InfoKind"
	# a wrapping label in a row collapses to one letter a line
	screen._info_kind.autowrap_mode = TextServer.AUTOWRAP_OFF
	head.add_child(screen._info_kind)
	screen._info_text = label("", UiMetrics.DETAIL)
	screen._info_text.name = "Description"
	screen._info_text.custom_minimum_size.y = 2.0 * 36.0
	column.add_child(screen._info_text)
	screen._info_bars = VBoxContainer.new()
	screen._info_bars.name = "InfoBars"
	screen._info_bars.mouse_filter = Control.MOUSE_FILTER_IGNORE
	column.add_child(screen._info_bars)
	screen._info_stats = label("", UiMetrics.DETAIL, KalyeahTheme.AMBER)
	screen._info_stats.name = "Stats"
	column.add_child(screen._info_stats)
	return panel


static func stat_line(def: WeaponDef) -> String:
	var parts: PackedStringArray = []
	if def.heal > 0:
		parts.append("Heal %d HP" % def.heal)
	elif def.damage > 0:
		var damage: String = "%d dmg" % def.damage
		match def.shape:
			WeaponDef.Shape.CONE:
				if def.snips_max > 1:
					damage += " / snip"
			WeaponDef.Shape.BOOMERANG, WeaponDef.Shape.SPINNER:
				damage += " / hit"
			WeaponDef.Shape.BOUNCER:
				damage += " / bounce"
			WeaponDef.Shape.FIELD:
				damage += " / %ss" % String.num(def.interval, 1)
		parts.append(damage)
	else:
		parts.append("Control" if def.kind == WeaponDef.Kind.CROWD_CONTROL else "Block")
	parts.append("CD %ss" % String.num(def.cooldown, 0 if is_equal_approx(def.cooldown, roundf(def.cooldown)) else 1))
	if def.max_range > 0.0:
		parts.append("Range %sm" % String.num(def.max_range, 0 if is_equal_approx(def.max_range, roundf(def.max_range)) else 1))
	return " · ".join(parts)
