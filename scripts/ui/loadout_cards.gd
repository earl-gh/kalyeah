class_name LoadoutCards
extends RefCounted
## Shared loadout cards: owner art, readable effects and explicit weapon statistics.


static func label(text: String, size: int, color: Color = KalyeahTheme.TEXT) -> Label:
	var result: Label = Label.new()
	result.text = text
	result.mouse_filter = Control.MOUSE_FILTER_IGNORE
	result.add_theme_font_size_override("font_size", size)
	result.add_theme_color_override("font_color", color)
	result.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	return result


static func box(fill: Color, border: Color, width: int = 2) -> StyleBoxFlat:
	var result: StyleBoxFlat = KalyeahTheme.surface_box(14, width, 0.0)
	result.bg_color = fill
	result.border_color = border
	return result


static func profile(screen: WeaponPickScreen, character: CharacterDef) -> Button:
	var button: Button = Button.new()
	button.name = "Profile_" + String(character.id)
	button.toggle_mode = true
	button.custom_minimum_size = screen.PROFILE_SIZE
	button.clip_contents = true
	button.add_theme_stylebox_override("normal", box(screen.CARD_COLOR, KalyeahTheme.SLATE))
	button.add_theme_stylebox_override("pressed", box(screen.SURFACE_SELECTED, screen.PICKED_COLOR, 5))
	button.add_theme_stylebox_override("hover_pressed", box(screen.SURFACE_SELECTED, screen.PICKED_COLOR, 5))
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
	var name_label: Label = label(character.display_name, 24)
	name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	name_label.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	name_label.offset_top = -36.0
	button.add_child(name_label)
	var taken: Label = label("TEAMMATE", 22)
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


static func weapon(screen: WeaponPickScreen, def: WeaponDef) -> Button:
	var button: Button = Button.new()
	button.name = "Weapon_" + String(def.id)
	button.toggle_mode = true
	button.custom_minimum_size = Vector2(0.0, 180.0)
	button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	button.add_theme_stylebox_override("normal", box(screen.CARD_COLOR, screen.kind_color(def.kind).darkened(0.3)))
	for state: String in ["pressed", "hover_pressed"]:
		button.add_theme_stylebox_override(state, box(screen.SURFACE_SELECTED, screen.PICKED_COLOR, 4))
	var margin: MarginContainer = MarginContainer.new()
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for side: String in ["left", "right", "top", "bottom"]:
		margin.add_theme_constant_override("margin_" + side, 16)
	margin.mouse_filter = Control.MOUSE_FILTER_IGNORE
	button.add_child(margin)
	var row: HBoxContainer = HBoxContainer.new()
	row.add_theme_constant_override("separation", 14)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	margin.add_child(row)
	var art: TextureRect = TextureRect.new()
	art.texture = Icons.art(StringName("btn_" + String(def.id)))
	art.custom_minimum_size = Vector2(88.0, 88.0)
	art.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	art.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	art.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	art.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_child(art)
	var texts: VBoxContainer = VBoxContainer.new()
	texts.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	texts.add_theme_constant_override("separation", 5)
	texts.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_child(texts)
	texts.add_child(label("%s · %s" % [def.display_name, def.kind_label()], screen.NAME_FONT))
	var description: Label = label(def.description, screen.DESCRIPTION_FONT)
	description.name = "Description"
	texts.add_child(description)
	var stats: Label = label(stat_line(def), screen.STAT_FONT, KalyeahTheme.AMBER)
	stats.name = "Stats"
	texts.add_child(stats)
	var badge: Label = label("1", 26, KalyeahTheme.INK)
	badge.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	badge.add_theme_stylebox_override("normal", box(screen.PICKED_COLOR, screen.INK))
	badge.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
	badge.offset_left = -42.0
	badge.offset_right = -4.0
	badge.offset_top = 4.0
	badge.offset_bottom = 42.0
	badge.visible = false
	button.add_child(badge)
	button.pressed.connect(screen.tap.bind(def.id))
	margin.minimum_size_changed.connect(func() -> void: button.custom_minimum_size.y = maxf(180.0, margin.get_combined_minimum_size().y))
	screen._buttons[def.id] = button
	screen._badges[def.id] = badge
	return button


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
