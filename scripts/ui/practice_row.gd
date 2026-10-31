class_name PracticeRow
extends HBoxContainer
## The main screen's Practice row, above Play (owner): the game button (80 %) picks the street game Practice plays (Agawan Base,
## Tumbang Preso, Patintero) and the kid button (20 %) picks the kid you play. Each opens a small picker; the choice is saved
## (`Session.practice_game`, `Session.avatar_id`). The title screen shows the pickers (`picker_opened`).

## A picker dialog was made: the owner opens it (one dialog at a time).
signal picker_opened(dialog: MenuDialog)
signal changed

## Game button : kid button (owner: 80 : 20).
const GAME_SHARE: float = 4.0
const KID_SHARE: float = 1.0
## The row's size in the 720 px design width: thumb-sized, a little narrower than the page.
const ROW_SIZE: Vector2 = Vector2(560.0, 96.0)
## The kid picker: faces in a grid of three columns, each this big.
const KID_COLUMNS: int = 3
const KID_FACE: float = 132.0

var game_button: Button
var kid_button: Button


func _init() -> void:
	name = "PracticeRow"
	custom_minimum_size = ROW_SIZE
	add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	game_button = MenuDialog.make_button("", "blue", UiMetrics.BODY, Vector2(0.0, ROW_SIZE.y))
	game_button.name = "GameButton"
	game_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	game_button.size_flags_stretch_ratio = GAME_SHARE
	game_button.expand_icon = true
	game_button.clip_text = true
	game_button.pressed.connect(open_game_picker)
	add_child(game_button)
	kid_button = MenuDialog.make_button("", "blue", UiMetrics.BODY, Vector2(0.0, ROW_SIZE.y))
	kid_button.name = "KidButton"
	kid_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	kid_button.size_flags_stretch_ratio = KID_SHARE
	kid_button.expand_icon = true
	kid_button.icon_alignment = HORIZONTAL_ALIGNMENT_CENTER
	kid_button.pressed.connect(open_kid_picker)
	add_child(kid_button)
	refresh()


## The game's name and picture, and your kid's face.
func refresh() -> void:
	var game: Dictionary = game_entry(Session.practice_game)
	game_button.text = String(game["game"])
	game_button.icon = game["picture"] as Texture2D
	game_button.tooltip_text = "Game: %s" % game_button.text
	kid_button.icon = KidModel.portrait(StringName(Session.avatar_id))
	kid_button.tooltip_text = "Kid: %s" % kid_name(StringName(Session.avatar_id))
	kid_button.accessibility_name = kid_button.tooltip_text


## The Games page's entry for `id` (Agawan Base for an unknown id).
static func game_entry(id: StringName) -> Dictionary:
	for street: Dictionary in StreetPage.STREETS:
		if street["id"] == id:
			return street
	return StreetPage.STREETS[0]


static func kid_name(id: StringName) -> String:
	var rules: GameRules = load(StreetPage.RULES_PATH) as GameRules
	for character: CharacterDef in rules.characters:
		if character.id == id:
			return character.display_name
	return String(id).capitalize()


## A dialog with one button for each game; the one Practice plays now is gold.
func open_game_picker() -> MenuDialog:
	var card: MenuDialog = MenuDialog.create("Pick a game")
	for street: Dictionary in StreetPage.STREETS:
		var id: StringName = street["id"] as StringName
		var button: Button = MenuDialog.make_button(String(street["game"]), "gold" if id == Session.practice_game else "blue", UiMetrics.BODY)
		button.name = "Game_%s" % String(id)
		button.icon = street["picture"] as Texture2D
		button.expand_icon = true
		card.body.add_child(button)
		button.pressed.connect(func() -> void:
			choose_game(id)
			card.close())
	picker_opened.emit(card)
	return card


## A dialog with the six kids' faces; yours is gold.
func open_kid_picker() -> MenuDialog:
	var card: MenuDialog = MenuDialog.create("Pick a kid")
	var grid: GridContainer = GridContainer.new()
	grid.name = "Kids"
	grid.columns = KID_COLUMNS
	grid.add_theme_constant_override("h_separation", UiMetrics.COMPACT_GAP)
	grid.add_theme_constant_override("v_separation", UiMetrics.COMPACT_GAP)
	var rules: GameRules = load(StreetPage.RULES_PATH) as GameRules
	for character: CharacterDef in rules.characters:
		var id: StringName = character.id
		var button: Button = MenuDialog.make_button("", "gold" if String(id) == Session.avatar_id else "blue", UiMetrics.BODY, Vector2.ONE * KID_FACE)
		button.name = "Kid_%s" % String(id)
		button.icon = KidModel.portrait(id)
		button.expand_icon = true
		button.icon_alignment = HORIZONTAL_ALIGNMENT_CENTER
		button.tooltip_text = character.display_name
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		grid.add_child(button)
		button.pressed.connect(func() -> void:
			choose_kid(id)
			card.close())
	card.body.add_child(grid)
	picker_opened.emit(card)
	return card


func choose_game(id: StringName) -> void:
	if not id in RoomService.MAP_IDS:
		return
	Session.practice_game = id
	Session.save()
	refresh()
	changed.emit()


func choose_kid(id: StringName) -> void:
	if not Session.AVATARS.has(String(id)):
		return
	Session.avatar_id = String(id)
	Session.save()
	refresh()
	changed.emit()
