class_name KidsScreen
extends CardsScreen
## The Kids screen: the six kids as their own card pictures, two columns by three rows, name under each. A tap opens the kid's drawer
## (the same facts the old sheet had: who they are, what they wear, the palette, and that the look changes nothing about play).

const COLUMN_COUNT: int = 2
const CHARACTER_PATH: String = "res://data/characters/%s.tres"
const NAME_SIZE: int = UiMetrics.ACTION


func _screen_name() -> String:
	return "KidsScreen"


func _title() -> String:
	return "Kids"


func _subtitle() -> String:
	return "Tap a kid to learn more."


func _columns() -> int:
	return COLUMN_COUNT


func _entries() -> Array[Dictionary]:
	var found: Array[Dictionary] = []
	for id: String in Session.AVATARS:
		var definition: CharacterDef = load(CHARACTER_PATH % id) as CharacterDef if ResourceLoader.exists(CHARACTER_PATH % id) else null
		var shown_name: String = definition.display_name if definition != null else id.capitalize()
		var art: Texture2D = CharactersPage.card_picture(StringName(id))
		if art == null:
			art = KidModel.card_face(StringName(id))
		found.append({
			"id": StringName(id), "name": shown_name, "picture": art, "tag": null,
			"access": "%s: read about this kid" % shown_name, "name_lines": 1, "name_size": NAME_SIZE,
			"content": DrawerContent.of_kid.bind(definition),
		})
	return found
