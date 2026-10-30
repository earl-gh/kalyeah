class_name CardsHub
extends VBoxContainer
## Two passive category banners with their five-column grids in one scrollable collection.

var kids_screen: KidsScreen
var skills_screen: SkillsScreen
var scroll: ScrollContainer

const RULES_PATH: String = "res://data/rules/game_rules.tres"
## The page ids a card opens (the ones `CodexPage` shows).
## The scrollbar: 12 px wide (easier to see and grab), inset 10 px from both ends, its grabber at least 72 px tall.
const BAR_WIDTH: int = 12
const BAR_INSET: float = 10.0
const GRABBER_MIN: float = 72.0

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
	# the scroll area reaches out into the page margin so the scrollbar sits in the middle of the gap between the content and the screen edge
	var holder: Control = Control.new()
	holder.name = "ScrollHolder"
	holder.size_flags_vertical = Control.SIZE_EXPAND_FILL
	holder.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(holder)
	scroll = ScrollContainer.new()
	scroll.name = "Scroll"
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	holder.add_child(scroll)
	scroll.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	style_scrollbar(scroll.get_v_scroll_bar())
	var bar: float = scroll.get_v_scroll_bar().get_combined_minimum_size().x
	scroll.offset_right = (TitleScreen.PAGE_SIDE_MARGIN + bar) / 2.0
	var inner: MarginContainer = MarginContainer.new()
	inner.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	inner.add_theme_constant_override("margin_right", int((TitleScreen.PAGE_SIDE_MARGIN - bar) / 2.0))
	scroll.add_child(inner)
	var body: VBoxContainer = VBoxContainer.new()
	body.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	body.add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	inner.add_child(body)
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


## A slim rounded scrollbar: a faint track inset from both ends and a light pill grabber that never shrinks below `GRABBER_MIN`.
static func style_scrollbar(bar: VScrollBar) -> void:
	var track: StyleBoxFlat = StyleBoxFlat.new()
	track.bg_color = Color(0.0, 0.03, 0.1, 0.3)
	track.set_corner_radius_all(UiSkin.round_radius(BAR_WIDTH))
	track.content_margin_left = BAR_WIDTH / 2.0
	track.content_margin_right = BAR_WIDTH / 2.0
	track.content_margin_top = BAR_INSET
	track.content_margin_bottom = BAR_INSET
	track.anti_aliasing = true
	bar.add_theme_stylebox_override("scroll", track)
	for state: String in ["grabber", "grabber_highlight", "grabber_pressed"]:
		var grabber: StyleBoxFlat = StyleBoxFlat.new()
		grabber.bg_color = Color(0.85, 0.93, 1.0, 0.55 if state == "grabber" else 0.85)
		grabber.set_corner_radius_all(UiSkin.round_radius(BAR_WIDTH))
		grabber.content_margin_top = GRABBER_MIN / 2.0
		grabber.content_margin_bottom = GRABBER_MIN / 2.0
		grabber.anti_aliasing = true
		bar.add_theme_stylebox_override(state, grabber)


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
