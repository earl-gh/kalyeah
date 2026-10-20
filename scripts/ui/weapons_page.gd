class_name WeaponsPage
extends VBoxContainer
## The Weapons tab of the book: a row of filters (All, Attack, Control, Block, Heal) over a two-column
## grid of weapon tiles (the picture in a round holder, the name and its kind). Tapping a tile asks
## for its detail sheet (`detail`): the picture large, what it does and its damage, cooldown and range.

signal weapon_opened(weapon: WeaponDef)

const RULES_PATH: String = "res://data/rules/game_rules.tres"
const ICON_PATH: String = "res://assets/icons/art/%s.png"
const ALL: StringName = &"all"
const KIND_NAMES: Dictionary[int, String] = {
	WeaponDef.Kind.ATTACK: "Attack", WeaponDef.Kind.CROWD_CONTROL: "Control",
	WeaponDef.Kind.BLOCK: "Block", WeaponDef.Kind.HEAL: "Heal",
}
const KIND_COLORS: Dictionary[int, Color] = {
	WeaponDef.Kind.ATTACK: Color("e0463e"), WeaponDef.Kind.CROWD_CONTROL: Color("8a55e8"),
	WeaponDef.Kind.BLOCK: Color("2a7be6"), WeaponDef.Kind.HEAL: Color("2fbf5a"),
}
const COLUMNS: int = 2
const TILE_HEIGHT: float = 292.0
const TILE_ICON: float = 128.0
const DETAIL_ICON: float = 168.0
const PLATE_FOCUS_RADIUS: int = 32

## One tile per weapon, in the order of the rules.
var tiles: Array[Button] = []
## The filter buttons by id: `all` and one per kind of weapon (the kind's number).
var filter_buttons: Dictionary[StringName, Button] = {}
var filter: StringName = ALL

var _weapons: Array[WeaponDef] = []
var _grid: GridContainer


func _ready() -> void:
	name = "WeaponsPage"
	add_theme_constant_override("separation", UiMetrics.GAP)
	_weapons = (load(RULES_PATH) as GameRules).weapons
	add_child(_build_filters())
	var scroll: ScrollContainer = ScrollContainer.new()
	scroll.name = "Scroll"
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	add_child(scroll)
	_grid = GridContainer.new()
	_grid.columns = COLUMNS
	_grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_grid.add_theme_constant_override("h_separation", UiMetrics.GAP)
	_grid.add_theme_constant_override("v_separation", UiMetrics.GAP)
	scroll.add_child(_grid)
	for weapon: WeaponDef in _weapons:
		var tile: Button = _tile(weapon)
		_grid.add_child(tile)
		tiles.append(tile)
	show_filter(ALL)


## Shows only the weapons of `id` (`all`, or a weapon kind's number as a string name).
func show_filter(id: StringName) -> void:
	filter = id
	for i: int in tiles.size():
		tiles[i].visible = id == ALL or StringName(str(_weapons[i].kind)) == id
	for other: StringName in filter_buttons:
		filter_buttons[other].set_pressed_no_signal(other == id)


func _build_filters() -> Control:
	var row: HBoxContainer = HBoxContainer.new()
	row.name = "Filters"
	row.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP / 2)
	var options: Array[Dictionary] = [{"id": ALL, "label": "All"}]
	for kind: int in KIND_NAMES:
		options.append({"id": StringName(str(kind)), "label": KIND_NAMES[kind]})
	for option: Dictionary in options:
		var button: Button = MenuDialog.make_button(String(option["label"]), "grey", UiMetrics.CAPTION, Vector2(0.0, 80.0))
		button.name = "Filter_%s" % String(option["id"])
		LobbyWidgets.make_toggle(button)
		# five filters share one row: slimmer sides than a full-size button
		for state: String in ["normal", "hover", "pressed", "hover_pressed"]:
			var slim: StyleBoxTexture = button.get_theme_stylebox(state).duplicate() as StyleBoxTexture
			slim.content_margin_left = 6.0
			slim.content_margin_right = 6.0
			button.add_theme_stylebox_override(state, slim)
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		button.pressed.connect(show_filter.bind(option["id"] as StringName))
		row.add_child(button)
		filter_buttons[option["id"] as StringName] = button
	return row


func _tile(weapon: WeaponDef) -> Button:
	var tile: Button = Button.new()
	tile.name = String(weapon.id)
	tile.custom_minimum_size = Vector2(0.0, TILE_HEIGHT)
	tile.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	tile.focus_mode = Control.FOCUS_ALL
	for state: String in ["normal", "hover"]:
		tile.add_theme_stylebox_override(state, UiSkin.plate("glass", 0.0))
	for state: String in ["pressed", "hover_pressed"]:
		tile.add_theme_stylebox_override(state, UiSkin.plate("raised", 0.0))
	tile.add_theme_stylebox_override("focus", UiSkin.focus_ring(PLATE_FOCUS_RADIUS))
	MenuDialog.feel_press(tile)
	var column: VBoxContainer = VBoxContainer.new()
	column.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	column.offset_left = 14.0
	column.offset_right = -14.0
	column.offset_top = 16.0
	column.offset_bottom = -16.0
	column.alignment = BoxContainer.ALIGNMENT_CENTER
	column.add_theme_constant_override("separation", 8)
	column.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tile.add_child(column)
	var picture: Control = holder(weapon, TILE_ICON)
	picture.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	column.add_child(picture)
	var name_label: Label = Label.new()
	name_label.name = "Name"
	name_label.theme_type_variation = &"DisplayLabel"
	name_label.text = weapon.display_name
	name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	name_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	name_label.add_theme_font_size_override("font_size", 26)
	name_label.add_theme_constant_override("outline_size", 5)
	name_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	column.add_child(name_label)
	var kind: PanelContainer = UiKit.chip(KIND_NAMES.get(weapon.kind, "") as String, KIND_COLORS.get(weapon.kind, Color("3b4c74")) as Color, Color.WHITE, 20)
	kind.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	kind.mouse_filter = Control.MOUSE_FILTER_IGNORE
	column.add_child(kind)
	tile.pressed.connect(weapon_opened.emit.bind(weapon))
	return tile



## The weapon's picture in a dark round holder with a coloured rim (its kind).
static func holder(weapon: WeaponDef, diameter: float) -> Control:
	var frame: PanelContainer = PanelContainer.new()
	frame.name = "Holder"
	frame.custom_minimum_size = Vector2(diameter, diameter)
	frame.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = Color("0a1433")
	box.set_corner_radius_all(int(diameter / 2.0))
	box.set_border_width_all(5)
	box.border_color = KIND_COLORS.get(weapon.kind, Color("3b4c74")) as Color
	box.anti_aliasing = true
	frame.add_theme_stylebox_override("panel", box)
	var picture: TextureRect = TextureRect.new()
	var icon: String = ICON_PATH % String(weapon.id)
	if ResourceLoader.exists(icon):
		picture.texture = load(icon) as Texture2D
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	frame.add_child(picture)
	return frame


## The detail sheet of `weapon`: picture, kind and street game, what it does, and its figures.
static func detail(weapon: WeaponDef) -> MenuDialog:
	var card: MenuDialog = MenuDialog.create(weapon.display_name, 640.0)
	card.name = "WeaponDetail"
	var picture: Control = holder(weapon, DETAIL_ICON)
	picture.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	card.body.add_child(picture)
	var tags: HBoxContainer = HBoxContainer.new()
	tags.alignment = BoxContainer.ALIGNMENT_CENTER
	tags.add_theme_constant_override("separation", 12)
	tags.add_child(UiKit.chip(KIND_NAMES.get(weapon.kind, "") as String, KIND_COLORS.get(weapon.kind, Color("3b4c74")) as Color, Color.WHITE, 22))
	tags.add_child(UiKit.text(weapon.street_game, UiMetrics.DETAIL, Color("9fb0d6"), false))
	card.body.add_child(tags)
	var about: Label = card.add_label(weapon.description, UiMetrics.BODY, KalyeahTheme.TEXT, HORIZONTAL_ALIGNMENT_CENTER)
	about.name = "About"
	var figures: HBoxContainer = HBoxContainer.new()
	figures.name = "Figures"
	figures.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	for figure: Dictionary in figures_of(weapon):
		figures.add_child(_figure(String(figure["value"]), String(figure["caption"])))
	card.body.add_child(figures)
	var close: Button = MenuDialog.make_button("Close", "grey", UiMetrics.ACTION)
	close.name = "Close"
	close.pressed.connect(card.close)
	card.add_buttons([close])
	return card


## The weapon's numbers as {value, caption}: damage (or healing), cooldown and range.
static func figures_of(weapon: WeaponDef) -> Array[Dictionary]:
	var found: Array[Dictionary] = []
	if weapon.heal > 0:
		found.append({"value": str(weapon.heal), "caption": "Heal"})
	elif weapon.damage > 0:
		found.append({"value": str(weapon.damage), "caption": "Damage"})
	if weapon.cooldown > 0.0:
		found.append({"value": "%ss" % String.num(weapon.cooldown, 1).trim_suffix(".0"), "caption": "Cooldown"})
	if weapon.max_range > 0.0:
		found.append({"value": "%s m" % String.num(weapon.max_range, 1).trim_suffix(".0"), "caption": "Range"})
	return found


static func _figure(value: String, caption: String) -> Control:
	var column: VBoxContainer = VBoxContainer.new()
	column.add_theme_constant_override("separation", 0)
	# the heavy body face (not Bungee, which is all capitals: "8 m" would read "8 M")
	var number: Label = Label.new()
	number.add_theme_font_override("font", KalyeahTheme.BUTTON_FONT)
	number.text = value
	number.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	number.add_theme_font_size_override("font_size", 46)
	number.add_theme_color_override("font_color", Color("ffd25a"))
	number.add_theme_constant_override("outline_size", 6)
	column.add_child(number)
	column.add_child(_caption(caption))
	var box: PanelContainer = UiKit.card(column, "inset", 12.0)
	box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	return box


static func _caption(text: String) -> Label:
	var label: Label = UiKit.text(text, UiMetrics.CAPTION, Color("9fb0d6"), false)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	return label
