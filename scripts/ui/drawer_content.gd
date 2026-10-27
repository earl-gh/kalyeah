class_name DrawerContent
extends RefCounted
## What the bottom drawer says about a card: the sections of the existing detail sheets (`CharactersPage.detail`, `WeaponsPage.detail`),
## taken from them as built, so a kid's palette or a skill's meters are written once and cannot drift. The sheet's own picture and its Close
## row are left out (the drawer has its own picture, handle and X); everything else is kept, so a section added to a sheet shows up here too.

## The names a sheet gives the parts the drawer replaces.
const PICTURE_NAMES: Array[String] = ["Picture", "Holder"]
const CLOSE_NAME: String = "Close"


## The sections of kid `definition`: about, palette, the note that looks change nothing.
static func of_kid(definition: CharacterDef) -> Array[Control]:
	if definition == null:
		return []
	return _borrow(CharactersPage.detail(definition))


## The sections of skill `weapon`: kind and street game, about, the figures as bars, the game behind it.
static func of_weapon(weapon: WeaponDef) -> Array[Control]:
	return _borrow(WeaponsPage.detail(weapon))


static func _borrow(sheet: MenuDialog) -> Array[Control]:
	var parts: Array[Control] = []
	for child: Node in sheet.body.get_children():
		var part: Control = child as Control
		if part == null or part.name in PICTURE_NAMES or part.find_child(CLOSE_NAME, true, false) != null:
			continue
		sheet.body.remove_child(part)
		parts.append(part)
	sheet.queue_free()
	return parts
