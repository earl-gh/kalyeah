class_name KidsScreen
extends CardsScreen
## The inline kid catalog; entries reuse the owner's pictures and authored descriptions.

const CHARACTER_PATH: String = "res://data/characters/%s.tres"
const NAME_SIZE: int = UiMetrics.ACTION


func _screen_name() -> String:
	return "KidsScreen"


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
			"kind": &"kid", "content": DrawerContent.of_kid.bind(definition),
		})
	return found
