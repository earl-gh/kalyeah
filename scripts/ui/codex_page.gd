class_name CodexPage
extends VBoxContainer
## The book at the top left of the main screen: the Characters and the Weapons of the game, one
## tab each.

var characters_page: CharactersPage
var weapons_page: WeaponsPage
var current: StringName = &"characters"
var tab_buttons: Dictionary[StringName, Button] = {}


func _ready() -> void:
	name = "CodexPage"
	add_theme_constant_override("separation", 12)
	var tabs: HBoxContainer = HBoxContainer.new()
	tabs.add_theme_constant_override("separation", 12)
	add_child(tabs)
	for tab: Dictionary in [{"id": &"characters", "label": "CHARACTERS"}, {"id": &"weapons", "label": "WEAPONS"}]:
		var button: Button = MenuDialog.make_button(String(tab["label"]), "grey", 26, Vector2(0.0, 76.0))
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		button.pressed.connect(show_tab.bind(tab["id"] as StringName))
		tabs.add_child(button)
		tab_buttons[tab["id"] as StringName] = button
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
	for id: StringName in tab_buttons:
		KalyeahTheme.style_button(tab_buttons[id], "blue" if id == tab else "grey")
	if tab == &"characters":
		characters_page.refresh()
