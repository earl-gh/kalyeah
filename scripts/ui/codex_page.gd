class_name CodexPage
extends Control
## The Cards screen: the hub of two wide cards (Kids, Skills) and, behind each, its page: the Characters page or the Weapons
## page, full screen under a Back button that returns to the hub. Back always goes one step: page -> hub -> (the title
## screen's) home.

const HUB: StringName = &"hub"
const CHARACTERS: StringName = CardsHub.KIDS
const WEAPONS: StringName = CardsHub.SKILLS
const BACK_SIZE: Vector2 = Vector2(160.0, UiMetrics.TOUCH)

var hub: CardsHub
var characters_page: CharactersPage
var weapons_page: WeaponsPage
var back_button: Button
## The Back button over the room where the two pages sit; shown only while one of them is.
var sub: VBoxContainer
var holder: Control
## What shows now: `HUB`, `CHARACTERS` or `WEAPONS`.
var current: StringName = HUB


func _ready() -> void:
	name = "CodexPage"
	# a plain Control root asks for no height of its own: a container root that once grew past the screen kept its offsets and stayed too tall
	hub = CardsHub.new()
	hub.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	hub.chosen.connect(show_view)
	add_child(hub)
	sub = VBoxContainer.new()
	sub.name = "Sub"
	sub.add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	sub.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(sub)
	back_button = MenuDialog.make_button("Back", "grey", UiMetrics.ACTION, BACK_SIZE)
	back_button.name = "BackButton"
	back_button.accessibility_name = "Back to Cards"
	back_button.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	back_button.pressed.connect(show_view.bind(HUB))
	sub.add_child(back_button)
	holder = Control.new()
	holder.name = "Pages"
	holder.size_flags_vertical = Control.SIZE_EXPAND_FILL
	sub.add_child(holder)
	characters_page = CharactersPage.new()
	weapons_page = WeaponsPage.new()
	for page: Control in [characters_page, weapons_page]:
		page.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		holder.add_child(page)
	show_view(HUB)


## Shows the hub (`HUB`) or one of its pages (`CHARACTERS`, `WEAPONS`).
func show_view(view: StringName) -> void:
	current = view if view in [CHARACTERS, WEAPONS] else HUB
	hub.visible = current == HUB
	sub.visible = current != HUB
	characters_page.visible = current == CHARACTERS
	weapons_page.visible = current == WEAPONS


## Back one step: from a page to the hub. False when already at the hub (the caller goes on to close the screen).
func go_back() -> bool:
	if current == HUB:
		return false
	show_view(HUB)
	return true
