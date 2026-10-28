class_name CardsHub
extends VBoxContainer
## The Cards screen of the main menu: two big wide cards one above the other, Kids (the six kids) and Skills (the weapon
## cards), each with a fan of three real card pictures sticking out of its top edge like the Cards icon of the bottom bar.
## Tapping one asks for that page (`chosen`); the hub itself decides nothing. The counts come from the data, never from here.

signal chosen(page_id: StringName)

const RULES_PATH: String = "res://data/rules/game_rules.tres"
## The page ids a card opens (the ones `CodexPage` shows).
const KIDS: StringName = &"characters"
const SKILLS: StringName = &"weapons"
## Which pictures form each fan, front card first: kids are ids of `assets/ui/character_cards`, skills are weapon ids of
## `assets/ui/skill_cards`.
const KIDS_FAN: Array[StringName] = [&"toni", &"ligaya", &"popoy"]
const SKILLS_FAN: Array[StringName] = [&"gunting_light", &"heal", &"bola"]
## A label carrying this meta may say "skill" (the UI lint bans the word everywhere else: round 15 chose "weapons"): the owner calls the
## weapon cards skills and asked for them under that name on this screen, and only these two labels use it.
const WORD_BY_OWNER: StringName = &"word_by_owner"
const NUMBER_WORDS: Array[String] = ["no", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve"]

var kids_card: CardsHubCard
var skills_card: CardsHubCard
var cards: Array[CardsHubCard] = []


func _ready() -> void:
	name = "CardsHub"
	size_flags_vertical = Control.SIZE_EXPAND_FILL
	add_theme_constant_override("separation", 0)
	add_child(UiKit.page_header("Cards"))
	add_child(_spacer("Above"))
	var weapons: Array[WeaponDef] = (load(RULES_PATH) as GameRules).weapons
	kids_card = CardsHubCard.create(KIDS, "Kids", kids_detail(Session.AVATARS.size()), _kid_pictures())
	skills_card = CardsHubCard.create(SKILLS, "Skills", skills_detail(weapons.size()), _skill_pictures())
	skills_card.title_label.set_meta(WORD_BY_OWNER, true)
	skills_card.detail_label.set_meta(WORD_BY_OWNER, true)
	for card: CardsHubCard in [kids_card, skills_card]:
		cards.append(card)
		card.pressed.connect(chosen.emit.bind(card.id))
		# the fan sticks out above its card: this much room above it keeps it clear of what is over it (and a gap between the cards)
		var slot: MarginContainer = MarginContainer.new()
		slot.name = "%sSlot" % card.name
		slot.mouse_filter = Control.MOUSE_FILTER_IGNORE
		var gap: int = UiMetrics.SECTION_GAP if card != kids_card else 0
		slot.add_theme_constant_override("margin_top", int(ceilf(card.protrusion())) + gap)
		slot.add_child(card)
		add_child(slot)
	add_child(_spacer("Below"))


## An empty stretch that, with its twin, keeps the two cards in the middle of what is left under the header.
func _spacer(spacer_name: String) -> Control:
	var spacer: Control = Control.new()
	spacer.name = spacer_name
	spacer.size_flags_vertical = Control.SIZE_EXPAND_FILL
	spacer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return spacer


## "Meet the six kids": the count in words as far as there is a word for it.
static func kids_detail(count: int) -> String:
	var word: String = NUMBER_WORDS[count] if count >= 0 and count < NUMBER_WORDS.size() else str(count)
	return "Meet the %s kids" % word


static func skills_detail(count: int) -> String:
	return "%d skills to throw" % count


func _kid_pictures() -> Array[Texture2D]:
	var found: Array[Texture2D] = []
	for id: StringName in KIDS_FAN:
		var art: Texture2D = CharactersPage.card_picture(id)
		if art != null:
			found.append(art)
	return found


func _skill_pictures() -> Array[Texture2D]:
	var found: Array[Texture2D] = []
	for id: StringName in SKILLS_FAN:
		var art: Texture2D = CardStyle.picture(id)
		if art != null:
			found.append(art)
	return found
