class_name PageHarness
extends Control
## Hosts the old Characters or Weapons page on a phone-sized screen of its own and opens its detail sheets, the way the title screen once
## did. The Cards screens no longer use those two pages (they are `KidsScreen` and `SkillsScreen` now), but the pages and their sheets
## are still built, and their tests still hold them to their word.

var page: Control
var dialog: MenuDialog


func _init(hosted: Control) -> void:
	page = hosted
	custom_minimum_size = Vector2(720.0, 1280.0)
	size = Vector2(720.0, 1280.0)


func _ready() -> void:
	page.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(page)
	if page.has_signal("weapon_opened"):
		page.connect("weapon_opened", _open_weapon)
	if page.has_signal("character_opened"):
		page.connect("character_opened", _open_character)


func _open_weapon(weapon: WeaponDef) -> void:
	_show(WeaponsPage.detail(weapon))


func _open_character(character: CharacterDef) -> void:
	_show(CharactersPage.detail(character))


func _show(sheet: MenuDialog) -> void:
	if dialog != null and is_instance_valid(dialog) and dialog.is_open:
		dialog.close(false)
	dialog = sheet
	dialog.open(self)
