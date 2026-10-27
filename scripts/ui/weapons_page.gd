class_name WeaponsPage
extends VBoxContainer
## The Weapons tab of the book, in two groupings the player switches between: **by game** (the street
## game each weapon is inspired by: Bato-bato-pik, Tumbang preso ...) and **by type** (Attack, Control,
## Block, Heal). Each group is a headed section of weapon tiles in two columns (the picture, the name and
## the other axis as a chip). Tapping a tile asks for its detail sheet (`detail`).

signal weapon_opened(weapon: WeaponDef)

const RULES_PATH: String = "res://data/rules/game_rules.tres"
const GAMES_PATH: String = "res://data/rules/street_games.tres"
const ICON_PATH: String = "res://assets/icons/art/%s.png"
const BY_GAME: StringName = &"game"
const BY_TYPE: StringName = &"type"
const KIND_NAMES: Dictionary[int, String] = {
	WeaponDef.Kind.ATTACK: "Attack", WeaponDef.Kind.CROWD_CONTROL: "Control",
	WeaponDef.Kind.BLOCK: "Block", WeaponDef.Kind.HEAL: "Heal",
}
const KIND_COLORS: Dictionary[int, Color] = {
	WeaponDef.Kind.ATTACK: Color("e0463e"), WeaponDef.Kind.CROWD_CONTROL: Color("8a55e8"),
	WeaponDef.Kind.BLOCK: Color("2a7be6"), WeaponDef.Kind.HEAL: Color("2fbf5a"),
}
## The kind's chip fill: the rim colours above are bright for the round holder, these keep white text readable.
const KIND_CHIPS: Dictionary[int, Color] = {
	WeaponDef.Kind.ATTACK: UiKit.CHIP_RED, WeaponDef.Kind.CROWD_CONTROL: UiKit.CHIP_PURPLE,
	WeaponDef.Kind.BLOCK: UiKit.CHIP_BLUE, WeaponDef.Kind.HEAL: UiKit.CHIP_GREEN,
}
const COLUMNS: int = 2
## The grouping switch sits under the book's own switch, so it is a size smaller (still a thumb).
const SWITCH_HEIGHT: float = UiMetrics.MIN_TARGET
const TILE_HEIGHT: float = 292.0
const TILE_ICON: float = 128.0
const DETAIL_ICON: float = 168.0
## The colour of each stat bar: power (damage or healing), price (ice water, the hand's own drop colour) and reach (range).
const BAR_COLORS: Dictionary[String, Color] = {"Damage": Color("ffc233"), "Heal": Color("45dd78"), "Ice water": CardHand.ICE_WATER, "Range": Color("4aa0ff")}
const PLATE_FOCUS_RADIUS: int = UiSkin.PLATE_RADIUS

## One tile per weapon, in the order of the rules (whichever way they are grouped).
var tiles: Array[Button] = []
## The headed sections now on show, top to bottom (each has a `Header` and a `Grid`).
var sections: Array[Control] = []
var grouping: StringName = BY_GAME
var segmented: UiSegmented

var _weapons: Array[WeaponDef] = []
var _list: VBoxContainer
var _scroll: ScrollContainer
var _game_chips: Array[Control] = []
var _kind_chips: Array[Control] = []


func _ready() -> void:
	name = "WeaponsPage"
	add_theme_constant_override("separation", UiMetrics.GAP)
	_weapons = (load(RULES_PATH) as GameRules).weapons
	segmented = UiSegmented.new([{"id": BY_GAME, "label": "By game"}, {"id": BY_TYPE, "label": "By type"}], SWITCH_HEIGHT)
	segmented.chosen.connect(show_grouping)
	add_child(segmented)
	_scroll = ScrollContainer.new()
	_scroll.name = "Scroll"
	_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	add_child(_scroll)
	_list = VBoxContainer.new()
	_list.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_list.add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	_scroll.add_child(_list)
	for weapon: WeaponDef in _weapons:
		var tile: Button = _tile(weapon)
		tiles.append(tile)
	show_grouping(BY_GAME)


## Groups the tiles by street game (`game`) or by weapon type (`type`).
func show_grouping(id: StringName) -> void:
	grouping = id
	segmented.select(id, false)
	_scroll.scroll_vertical = 0
	for tile: Button in tiles:
		if tile.get_parent() != null:
			tile.get_parent().remove_child(tile)
	for section: Control in sections:
		_list.remove_child(section)
		section.queue_free()
	sections.clear()
	for i: int in tiles.size():
		_game_chips[i].visible = id == BY_TYPE
		_kind_chips[i].visible = id == BY_GAME
	for group: Dictionary in _groups(id):
		var grid: GridContainer = GridContainer.new()
		grid.name = "Grid"
		grid.columns = COLUMNS
		grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		grid.add_theme_constant_override("h_separation", UiMetrics.GAP)
		grid.add_theme_constant_override("v_separation", UiMetrics.GAP)
		for index: int in group["members"] as Array[int]:
			grid.add_child(tiles[index])
		var section: VBoxContainer = VBoxContainer.new()
		section.name = "Section_%s" % String(group["label"])
		section.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
		var header: Label = UiKit.section_label("%s · %d" % [String(group["label"]), (group["members"] as Array).size()])
		header.name = "Header"
		section.add_child(header)
		if id == BY_GAME:
			var blurb: String = street_games().blurb_of(String(group["label"]))
			if not blurb.is_empty():
				var line: Label = UiKit.text(blurb, UiMetrics.CAPTION, Color("9fb0d6"))
				line.name = "Blurb"
				section.add_child(line)
		section.add_child(grid)
		_list.add_child(section)
		sections.append(section)


## The groups of the weapons for a grouping: {label, members (indexes into the rules' list)}. Games come
## in the order they first appear in the rules; types in the order of the Kind enum.
func _groups(id: StringName) -> Array[Dictionary]:
	var found: Array[Dictionary] = []
	var labels: Array[String] = []
	if id == BY_GAME:
		for weapon: WeaponDef in _weapons:
			if not labels.has(weapon.street_game):
				labels.append(weapon.street_game)
	else:
		for kind: int in KIND_NAMES:
			labels.append(KIND_NAMES[kind])
	for label: String in labels:
		var members: Array[int] = []
		for i: int in _weapons.size():
			var key: String = _weapons[i].street_game if id == BY_GAME else String(KIND_NAMES.get(_weapons[i].kind, ""))
			if key == label:
				members.append(i)
		if not members.is_empty():
			found.append({"label": label, "members": members})
	return found


func _tile(weapon: WeaponDef) -> Button:
	var tile: Button = Button.new()
	tile.name = String(weapon.id)
	tile.accessibility_name = "%s, %s: read about this weapon" % [weapon.display_name, weapon.kind_label()]
	tile.custom_minimum_size = Vector2(0.0, TILE_HEIGHT)
	tile.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	tile.focus_mode = Control.FOCUS_ALL
	# a drag that starts on it scrolls the list it sits in: the scrollbar is not the only grip
	tile.mouse_filter = Control.MOUSE_FILTER_PASS
	for state: String in ["normal", "hover"]:
		tile.add_theme_stylebox_override(state, UiSkin.plate("glass", 0.0))
	for state: String in ["pressed", "hover_pressed"]:
		tile.add_theme_stylebox_override(state, UiSkin.plate("raised", 0.0))
	tile.add_theme_stylebox_override("focus", UiSkin.focus_ring(PLATE_FOCUS_RADIUS))
	UiKit.feel_press(tile)
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
	name_label.add_theme_font_size_override("font_size", UiMetrics.ACTION)
	name_label.add_theme_constant_override("outline_size", 5)
	name_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	column.add_child(name_label)
	# the other axis of the grouping as a chip: the kind under a game, the game under a type
	var kind: PanelContainer = UiKit.chip(KIND_NAMES.get(weapon.kind, "") as String, KIND_CHIPS.get(weapon.kind, UiKit.CHIP_SLATE) as Color, Color.WHITE, UiMetrics.MIN_TEXT)
	var game: PanelContainer = UiKit.chip(weapon.street_game, UiKit.CHIP_SLATE, Color.WHITE, UiMetrics.MIN_TEXT)
	for chip: PanelContainer in [kind, game]:
		chip.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
		chip.mouse_filter = Control.MOUSE_FILTER_IGNORE
		column.add_child(chip)
	_kind_chips.append(kind)
	_game_chips.append(game)
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
	var card: MenuDialog = MenuDialog.create(weapon.display_name, MenuDialog.WIDTH_SHEET)
	card.name = "WeaponDetail"
	var picture: Control = holder(weapon, DETAIL_ICON)
	picture.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	card.body.add_child(picture)
	var tags: HBoxContainer = HBoxContainer.new()
	tags.alignment = BoxContainer.ALIGNMENT_CENTER
	tags.add_theme_constant_override("separation", 12)
	tags.add_child(UiKit.chip(KIND_NAMES.get(weapon.kind, "") as String, KIND_CHIPS.get(weapon.kind, UiKit.CHIP_SLATE) as Color))
	tags.add_child(UiKit.text(weapon.street_game, UiMetrics.DETAIL, KalyeahTheme.MODAL_TEXT_MUTED, false))
	card.body.add_child(tags)
	var about: Label = card.add_label(weapon.description, UiMetrics.BODY, KalyeahTheme.MODAL_TEXT, HORIZONTAL_ALIGNMENT_CENTER)
	about.name = "About"
	var figures: VBoxContainer = VBoxContainer.new()
	figures.name = "Figures"
	figures.add_theme_constant_override("separation", 10)
	var maxima: Dictionary = stat_maxima(_all_weapons())
	for figure: Dictionary in figures_of(weapon):
		figures.add_child(bar_row(String(figure["caption"]), String(figure["value"]), fraction_of(String(figure["caption"]), weapon, maxima)))
	card.body.add_child(UiKit.card(figures, "inset", 16.0, UiKit.NESTED_RADIUS))
	var blurb: String = street_games().blurb_of(weapon.street_game)
	if not blurb.is_empty():
		var column: VBoxContainer = VBoxContainer.new()
		column.add_theme_constant_override("separation", 4)
		var heading: Label = UiKit.section_label("The game · %s" % weapon.street_game)
		heading.add_theme_color_override("font_color", Color("b8641a"))  # a dark amber that reads on the light body
		column.add_child(heading)
		var story: Label = UiKit.text(blurb, UiMetrics.DETAIL, KalyeahTheme.MODAL_TEXT)
		story.name = "GameBlurb"
		column.add_child(story)
		card.body.add_child(column)
	var close: Button = MenuDialog.make_button("Close", "grey", UiMetrics.BODY)
	close.name = "Close"
	close.pressed.connect(card.close)
	card.add_buttons([close])
	return card


## The weapon's numbers as {value, caption}: damage (or healing), elixir cost and range.
static func figures_of(weapon: WeaponDef) -> Array[Dictionary]:
	var found: Array[Dictionary] = []
	if weapon.heal > 0:
		found.append({"value": str(weapon.heal), "caption": "Heal"})
	elif weapon.damage > 0:
		found.append({"value": str(weapon.damage), "caption": "Damage"})
	if weapon.elixir_cost > 0:
		found.append({"value": str(weapon.elixir_cost), "caption": "Ice water"})
	if weapon.max_range > 0.0:
		found.append({"value": "%s m" % String.num(weapon.max_range, 1).trim_suffix(".0"), "caption": "Range"})
	return found


## The biggest power (damage or healing), elixir cost and range among `weapons`: what a full bar means.
static func stat_maxima(weapons: Array[WeaponDef]) -> Dictionary:
	var found: Dictionary = {"power": 0.0, "elixir": 0.0, "range": 0.0}
	for weapon: WeaponDef in weapons:
		found["power"] = maxf(found["power"] as float, float(maxi(weapon.damage, weapon.heal)))
		found["elixir"] = maxf(found["elixir"] as float, float(weapon.elixir_cost))
		found["range"] = maxf(found["range"] as float, weapon.max_range)
	return found


## How full a stat's bar is (0 to 1): the weapon's number over the biggest of any weapon.
static func fraction_of(caption: String, weapon: WeaponDef, maxima: Dictionary) -> float:
	var value: float = 0.0
	var top: float = 0.0
	match caption:
		"Damage", "Heal":
			value = float(maxi(weapon.damage, weapon.heal))
			top = maxima["power"] as float
		"Ice water":
			value = float(weapon.elixir_cost)
			top = maxima["elixir"] as float
		"Range":
			value = weapon.max_range
			top = maxima["range"] as float
	return clampf(value / top, 0.0, 1.0) if top > 0.0 else 0.0


## A stat meter: a dark track and a fill in the stat's colour, `fraction` full.
static func meter_bar(caption: String, fraction: float, height: float) -> ProgressBar:
	var meter: ProgressBar = ProgressBar.new()
	meter.name = "Meter"
	meter.min_value = 0.0
	meter.max_value = 1.0
	meter.step = 0.001
	meter.value = fraction
	meter.show_percentage = false
	meter.custom_minimum_size = Vector2(0.0, height)
	meter.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	meter.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	var track: StyleBoxFlat = StyleBoxFlat.new()
	track.bg_color = Color("0a1433")
	track.set_corner_radius_all(UiSkin.round_radius(height))
	track.anti_aliasing = true
	var fill: StyleBoxFlat = StyleBoxFlat.new()
	fill.bg_color = BAR_COLORS.get(caption, Color("ffc233")) as Color
	fill.set_corner_radius_all(UiSkin.round_radius(height))
	fill.anti_aliasing = true
	meter.add_theme_stylebox_override("background", track)
	meter.add_theme_stylebox_override("fill", fill)
	return meter


## One stat: its name, a bar as full as it is next to every other weapon, and the exact number (`compact`: a slimmer
## row without the number, for a card that already prints its numbers).
static func bar_row(caption: String, value: String, fraction: float, compact: bool = false) -> Control:
	var row: HBoxContainer = HBoxContainer.new()
	row.name = "Stat_%s" % caption
	row.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	var name_label: Label = UiKit.text(caption, UiMetrics.CAPTION, Color("9fb0d6"), false)
	name_label.custom_minimum_size = Vector2(90.0 if compact else 120.0, 0.0)
	name_label.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	row.add_child(name_label)
	var meter: ProgressBar = meter_bar(caption, fraction, 16.0 if compact else 26.0)
	row.add_child(meter)
	var number: Label = Label.new()
	number.name = "Value"
	number.text = value
	number.custom_minimum_size = Vector2(110.0, 0.0)
	number.visible = not compact
	number.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	number.add_theme_font_override("font", KalyeahTheme.BUTTON_FONT)
	number.add_theme_font_size_override("font_size", UiMetrics.DETAIL)
	number.add_theme_color_override("font_color", Color("ffd25a"))
	number.add_theme_constant_override("outline_size", 0)
	row.add_child(number)
	return row


## The stat bars of `weapon` as a column of compact rows (no numbers), scaled by `maxima` (see `stat_maxima`).
static func compact_bars(weapon: WeaponDef, maxima: Dictionary) -> VBoxContainer:
	var column: VBoxContainer = VBoxContainer.new()
	column.name = "Bars"
	column.add_theme_constant_override("separation", 4)
	column.mouse_filter = Control.MOUSE_FILTER_IGNORE
	for figure: Dictionary in figures_of(weapon):
		var row: Control = bar_row(String(figure["caption"]), String(figure["value"]), fraction_of(String(figure["caption"]), weapon, maxima), true)
		row.mouse_filter = Control.MOUSE_FILTER_IGNORE
		for node: Node in row.find_children("*", "Control", true, false):
			(node as Control).mouse_filter = Control.MOUSE_FILTER_IGNORE
		column.add_child(row)
	return column


static func street_games() -> StreetGames:
	return load(GAMES_PATH) as StreetGames


static func _all_weapons() -> Array[WeaponDef]:
	return (load(RULES_PATH) as GameRules).weapons
