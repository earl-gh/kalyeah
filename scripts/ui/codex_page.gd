class_name CodexPage
extends Control
## One scrolling collection: category banners followed by their inline card grids.

## A card of either screen was tapped: the title screen shows its drawer.
signal drawer_requested(screen: CardsScreen, index: int)

const HUB: StringName = &"hub"
const CHARACTERS: StringName = CardsHub.KIDS
const WEAPONS: StringName = CardsHub.SKILLS

var hub: CardsHub
var kids_screen: KidsScreen
var skills_screen: SkillsScreen
## What shows now: `HUB`, `CHARACTERS` (the Kids screen) or `WEAPONS` (the Skills screen).
var current: StringName = HUB


func _ready() -> void:
	name = "CodexPage"
	# a plain Control root asks for no height of its own: a container root that once grew past the screen kept its offsets and stayed too tall
	hub = CardsHub.new()
	hub.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(hub)
	kids_screen = hub.kids_screen
	skills_screen = hub.skills_screen
	for screen: CardsScreen in [kids_screen, skills_screen]:
		screen.drawer_requested.connect(drawer_requested.emit)
	show_view(HUB)


## Return to the top, or scroll directly to a category; no inner page is opened.
func show_view(view: StringName) -> void:
	current = HUB
	hub.scroll_to(view)
