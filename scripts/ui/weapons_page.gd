class_name WeaponsPage
extends VBoxContainer
## The Weapons tab: every weapon with its picture, street game and what it does.

const RULES_PATH: String = "res://data/rules/game_rules.tres"
const ICON_PATH: String = "res://assets/icons/art/%s.png"

var rows: Array[Control] = []


func _ready() -> void:
	name = "WeaponsPage"
	add_theme_constant_override("separation", 12)
	var title: Label = Label.new()
	title.theme_type_variation = &"DisplayLabel"
	title.text = "STREET-GAME GEAR"
	title.add_theme_font_size_override("font_size", 38)
	add_child(title)
	var scroll: ScrollContainer = ScrollContainer.new()
	scroll.name = "Scroll"
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	add_child(scroll)
	var list: VBoxContainer = VBoxContainer.new()
	list.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	list.add_theme_constant_override("separation", 10)
	scroll.add_child(list)
	var rules: GameRules = load(RULES_PATH) as GameRules
	for weapon: WeaponDef in rules.weapons:
		var row: Control = _row(weapon)
		list.add_child(row)
		rows.append(row)


func _row(weapon: WeaponDef) -> Control:
	var panel: PanelContainer = PanelContainer.new()
	panel.name = String(weapon.id)
	panel.add_theme_stylebox_override("panel", KalyeahTheme.painted_pill())
	var line: HBoxContainer = HBoxContainer.new()
	line.add_theme_constant_override("separation", 14)
	panel.add_child(line)
	var picture: TextureRect = TextureRect.new()
	var icon: String = ICON_PATH % String(weapon.id)
	if ResourceLoader.exists(icon):
		picture.texture = load(icon) as Texture2D
	picture.custom_minimum_size = Vector2(92.0, 92.0)
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	line.add_child(picture)
	var texts: VBoxContainer = VBoxContainer.new()
	texts.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	texts.add_theme_constant_override("separation", 2)
	line.add_child(texts)
	var name_label: Label = Label.new()
	name_label.name = "Name"
	name_label.theme_type_variation = &"DisplayLabel"
	name_label.text = weapon.display_name.to_upper()
	name_label.add_theme_font_size_override("font_size", 24)
	texts.add_child(name_label)
	var game: Label = Label.new()
	game.theme_type_variation = &"MutedLabel"
	game.text = weapon.street_game
	game.add_theme_font_size_override("font_size", 17)
	texts.add_child(game)
	var about: Label = Label.new()
	about.name = "About"
	about.text = weapon.description
	about.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	about.add_theme_font_size_override("font_size", 18)
	texts.add_child(about)
	return panel
