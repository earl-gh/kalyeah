class_name CardsHub
extends VBoxContainer
## Two passive category banners with their five-column grids in one scrollable collection.

var kids_screen: KidsScreen
var skills_screen: SkillsScreen
var scroll: ScrollContainer

const RULES_PATH: String = "res://data/rules/game_rules.tres"
## The page ids a card opens (the ones `CodexPage` shows).
const KIDS: StringName = &"characters"
const SKILLS: StringName = &"weapons"
## Which pictures form each fan, front card first: kids are ids of `assets/ui/character_cards`, skills are weapon ids of
## `assets/ui/skill_cards`.
const KIDS_FAN: Array[StringName] = [&"toni", &"ligaya", &"popoy"]
const SKILLS_FAN: Array[StringName] = [&"gunting_light", &"heal", &"bola"]
## The Skills fan sits this much lower than the Kids fan (its cards are taller), so less of it sticks out above its banner.
const SKILLS_FAN_DROP: float = 28.0
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
	scroll = ScrollContainer.new()
	scroll.name = "Scroll"
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	add_child(scroll)
	var body: VBoxContainer = VBoxContainer.new()
	body.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	body.add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	scroll.add_child(body)
	var weapons: Array[WeaponDef] = (load(RULES_PATH) as GameRules).weapons
	kids_card = CardsHubCard.create(KIDS, "Kids", kids_detail(Session.AVATARS.size()), _kid_pictures())
	skills_card = CardsHubCard.create(SKILLS, "Skills", skills_detail(weapons.size()), _skill_pictures(), SKILLS_FAN_DROP)
	skills_card.title_label.set_meta(WORD_BY_OWNER, true)
	skills_card.detail_label.set_meta(WORD_BY_OWNER, true)
	kids_screen = KidsScreen.new()
	skills_screen = SkillsScreen.new()
	for index: int in 2:
		var card: CardsHubCard = kids_card if index == 0 else skills_card
		var grid: CardsScreen = kids_screen if index == 0 else skills_screen
		cards.append(card)
		card.mouse_filter = Control.MOUSE_FILTER_IGNORE
		card.focus_mode = Control.FOCUS_NONE
		var slot: MarginContainer = MarginContainer.new()
		slot.name = "%sSlot" % card.name
		slot.mouse_filter = Control.MOUSE_FILTER_IGNORE
		slot.add_theme_constant_override("margin_top", int(ceilf(card.protrusion())))
		slot.add_child(card)
		body.add_child(slot)
		grid.slots = 10 if index == 0 else 15
		grid.scroll = scroll
		body.add_child(grid)


func scroll_to(view: StringName) -> void:
	if view == KIDS or view == SKILLS:
		scroll.ensure_control_visible.call_deferred(kids_card if view == KIDS else skills_card)
	else:
		scroll.scroll_vertical = 0


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
