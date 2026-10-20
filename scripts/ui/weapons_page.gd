class_name WeaponsPage
extends VBoxContainer
## The Weapons tab of the book: every weapon as a card with its picture in a round holder, its name,
## what kind it is (attack, crowd control, block, heal), the street game it belongs to, what it
## does, and its damage, cooldown and range.

const RULES_PATH: String = "res://data/rules/game_rules.tres"
const ICON_PATH: String = "res://assets/icons/art/%s.png"
const KIND_NAMES: Dictionary[int, String] = {
	WeaponDef.Kind.ATTACK: "Attack", WeaponDef.Kind.CROWD_CONTROL: "Control",
	WeaponDef.Kind.BLOCK: "Block", WeaponDef.Kind.HEAL: "Heal",
}
const KIND_COLORS: Dictionary[int, Color] = {
	WeaponDef.Kind.ATTACK: Color("e0463e"), WeaponDef.Kind.CROWD_CONTROL: Color("8a55e8"),
	WeaponDef.Kind.BLOCK: Color("2a7be6"), WeaponDef.Kind.HEAL: Color("2fbf5a"),
}

var rows: Array[Control] = []


func _ready() -> void:
	name = "WeaponsPage"
	add_theme_constant_override("separation", UiMetrics.GAP)
	var scroll: ScrollContainer = ScrollContainer.new()
	scroll.name = "Scroll"
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	add_child(scroll)
	var list: VBoxContainer = VBoxContainer.new()
	list.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	list.add_theme_constant_override("separation", UiMetrics.GAP)
	scroll.add_child(list)
	var rules: GameRules = load(RULES_PATH) as GameRules
	for weapon: WeaponDef in rules.weapons:
		var row: Control = _row(weapon)
		list.add_child(row)
		rows.append(row)


func _row(weapon: WeaponDef) -> Control:
	var line: HBoxContainer = HBoxContainer.new()
	line.add_theme_constant_override("separation", UiMetrics.GAP)
	line.add_child(_holder(weapon))
	var texts: VBoxContainer = VBoxContainer.new()
	texts.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	texts.add_theme_constant_override("separation", 6)
	line.add_child(texts)
	var name_label: Label = Label.new()
	name_label.name = "Name"
	name_label.theme_type_variation = &"DisplayLabel"
	name_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	name_label.text = weapon.display_name
	name_label.add_theme_font_size_override("font_size", 30)
	name_label.add_theme_constant_override("outline_size", 5)
	texts.add_child(name_label)
	var tags: HBoxContainer = HBoxContainer.new()
	tags.add_theme_constant_override("separation", 8)
	tags.add_child(UiKit.chip(KIND_NAMES.get(weapon.kind, "") as String, KIND_COLORS.get(weapon.kind, Color("3b4c74")) as Color, Color.WHITE, 20))
	var game: Label = UiKit.text(weapon.street_game, UiMetrics.CAPTION, Color("9fb0d6"), false)
	game.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	tags.add_child(game)
	texts.add_child(tags)
	var about: Label = UiKit.text(weapon.description, UiMetrics.DETAIL)
	about.name = "About"
	texts.add_child(about)
	texts.add_child(_stats(weapon))
	var card: PanelContainer = UiKit.card(line, "glass", 22.0)
	card.name = String(weapon.id)
	return card


## The weapon's picture in a dark round holder with a coloured rim (its kind).
func _holder(weapon: WeaponDef) -> Control:
	var holder: PanelContainer = PanelContainer.new()
	holder.custom_minimum_size = Vector2(112.0, 112.0)
	holder.size_flags_vertical = Control.SIZE_SHRINK_BEGIN
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = Color("0a1433")
	box.set_corner_radius_all(56)
	box.set_border_width_all(5)
	box.border_color = KIND_COLORS.get(weapon.kind, Color("3b4c74")) as Color
	box.anti_aliasing = true
	holder.add_theme_stylebox_override("panel", box)
	var picture: TextureRect = TextureRect.new()
	var icon: String = ICON_PATH % String(weapon.id)
	if ResourceLoader.exists(icon):
		picture.texture = load(icon) as Texture2D
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	holder.add_child(picture)
	return holder


## Damage, cooldown and range as three small figures.
func _stats(weapon: WeaponDef) -> Control:
	var row: HBoxContainer = HBoxContainer.new()
	row.name = "Stats"
	row.add_theme_constant_override("separation", 18)
	var figures: Array[String] = []
	if weapon.damage > 0:
		figures.append("%d damage" % weapon.damage)
	if weapon.cooldown > 0.0:
		figures.append("%ss cooldown" % String.num(weapon.cooldown, 1).trim_suffix(".0"))
	if weapon.max_range > 0.0:
		figures.append("%s m range" % String.num(weapon.max_range, 1).trim_suffix(".0"))
	for figure: String in figures:
		row.add_child(UiKit.text(figure, UiMetrics.CAPTION, Color("ffd25a"), false))
	return row
