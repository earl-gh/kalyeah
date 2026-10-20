class_name CodexPage
extends VBoxContainer
## The Book: the Characters and the Weapons of the game, one segment each under a big switcher.

var characters_page: CharactersPage
var weapons_page: WeaponsPage
var segmented: UiSegmented
var current: StringName = &"characters"
## The two segment buttons by id (characters, weapons).
var tab_buttons: Dictionary[StringName, Button] = {}


func _ready() -> void:
	name = "CodexPage"
	add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	segmented = UiSegmented.new([{"id": &"characters", "label": "Characters"}, {"id": &"weapons", "label": "Weapons"}])
	segmented.chosen.connect(show_tab)
	tab_buttons = segmented.buttons
	add_child(segmented)
	var holder: Control = Control.new()
	holder.size_flags_vertical = Control.SIZE_EXPAND_FILL
	add_child(holder)
	characters_page = CharactersPage.new()
	weapons_page = WeaponsPage.new()
	for page: Control in [characters_page, weapons_page]:
		page.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		holder.add_child(page)
	show_tab(&"characters")


## Opens the Characters or the Weapons tab of the book.
func show_tab(tab: StringName) -> void:
	current = tab
	characters_page.visible = tab == &"characters"
	weapons_page.visible = tab == &"weapons"
	segmented.select(tab, false)
	if tab == &"characters":
		characters_page.refresh()
