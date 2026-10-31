class_name PracticeRow
extends HBoxContainer
## The main screen's Practice row, above Play (owner): the game button (80 %) picks the street game Practice plays (Agawan Base,
## Tumbang Preso, Patintero) and the kid button (20 %) picks the kid you play. Each opens a small picker; the choice is saved
## (`Session.practice_game`, `Session.avatar_id`). The title screen shows the pickers (`picker_opened`).

## A picker dialog was made: the owner opens it (one dialog at a time).
signal picker_opened(dialog: MenuDialog)
## The kid pick screen was made: the owner adds it on top of everything.
signal kid_screen_opened(screen: WeaponPickScreen)
signal changed

## Game button : kid button (owner: 80 : 20).
const GAME_SHARE: float = 4.0
const KID_SHARE: float = 1.0
## The row's size in the 720 px design width: thumb-sized, a little narrower than the page.
const ROW_SIZE: Vector2 = Vector2(560.0, 96.0)
## The kid pick's confirm button.
const KID_READY: String = "Choose"

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


## The kid pick of a Practice match (the card carousel, the kid's name and about), over the dimmed main screen (owner). The owner adds
## it to the tree (`kid_screen_opened`); Choose saves the kid, Back keeps yours.
func open_kid_picker() -> WeaponPickScreen:
	var rules: GameRules = load(StreetPage.RULES_PATH) as GameRules
	var ids: Array[StringName] = []
	for def: WeaponDef in rules.weapons:
		ids.append(def.id)
	var screen: WeaponPickScreen = WeaponPickScreen.new()
	screen.name = "KidPick"
	screen.dimmed = true
	screen.cards_mode = true
	screen.ready_label = KID_READY
	screen.z_index = MenuDialog.MODAL_Z
	kid_screen_opened.emit(screen)
	screen.open(WeaponPick.new(ids, 0.0), rules.weapons, "", RandomNumberGenerator.new(), rules.characters, StringName(Session.avatar_id), true)
	screen.confirmed.connect(func(_picks: Array[StringName]) -> void:
		choose_kid(screen.character_id)
		screen.queue_free())
	screen.exit_requested.connect(screen.queue_free)
	return screen


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
