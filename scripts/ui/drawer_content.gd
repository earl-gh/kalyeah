class_name DrawerContent
extends RefCounted
## Drawer details are one short description; the drawer supplies the name and picture.


static func of_kid(definition: CharacterDef) -> Array[Control]:
	if definition == null:
		return []
	return _description(definition.description)


static func of_weapon(weapon: WeaponDef) -> Array[Control]:
	return _description(weapon.description)


static func _description(words: String) -> Array[Control]:
	var about: Label = UiKit.text(words, UiMetrics.BODY, MenuDialog.TEXT_COLOR)
	about.name = "About"
	about.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	return [about] as Array[Control]
