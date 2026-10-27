class_name CodexPage
extends Control
## The Cards screen: the hub of two wide cards (Kids, Skills) and, behind each, its own screen of cards (`KidsScreen`, `SkillsScreen`),
## each with a Back button that returns to the hub. Back always goes one step: drawer -> screen -> hub -> (the title screen's) home.

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
	hub.chosen.connect(show_view)
	add_child(hub)
	kids_screen = KidsScreen.new()
	skills_screen = SkillsScreen.new()
	for screen: CardsScreen in [kids_screen, skills_screen]:
		screen.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		screen.back_pressed.connect(show_view.bind(HUB))
		screen.drawer_requested.connect(drawer_requested.emit)
		add_child(screen)
	show_view(HUB)


## Shows the hub (`HUB`) or one of its screens (`CHARACTERS`, `WEAPONS`).
func show_view(view: StringName) -> void:
	current = view if view in [CHARACTERS, WEAPONS] else HUB
	hub.visible = current == HUB
	kids_screen.visible = current == CHARACTERS
	skills_screen.visible = current == WEAPONS


## The screen shown, or null at the hub.
func screen_shown() -> CardsScreen:
	return kids_screen if current == CHARACTERS else (skills_screen if current == WEAPONS else null)


## Back one step: from a screen to the hub. False when already at the hub (the caller goes on to close the Cards screen).
func go_back() -> bool:
	if current == HUB:
		return false
	show_view(HUB)
	return true
