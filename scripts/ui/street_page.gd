class_name StreetPage
extends ScrollContainer
## The Street page (the left tab of the bottom bar): every game of the barangay is a street, with its name, what the street game
## is and how it plays here. Agawan Base is the only street open to play for now (the other two say Coming soon, like the lobby's
## map list); its numbers come from the game rules, so the words cannot go stale.

## The streets in order: the map id (RoomService.MAP_IDS), its name, the game it is, a colour, and what the street game is.
const STREETS: Array[Dictionary] = [
	{"id": &"street", "name": "A. Base Street", "game": "Agawan Base", "color": "blue",
		"blurb": "Two teams, two bases! Tag the other team, free your own, and hold their base."},
	{"id": &"tumbang_preso", "name": "T. Preso Street", "game": "Tumbang Preso", "color": "orange",
		"blurb": "Players throw a tsinelas to knock down a tin can while the guard, the taya, defends it."},
	{"id": &"patintero", "name": "Patintero Street", "game": "Patintero", "color": "green",
		"blurb": "Runners try to cross the lines drawn on the street while the taggers guard each line and try to touch them."},
]
## What a street that is not built yet says where its mechanics will be.
const SOON_LINE: String = "How it plays here is coming soon."
const RULES_PATH: String = "res://data/rules/game_rules.tres"

var cards: Dictionary[StringName, Control] = {}
var _content: VBoxContainer


func _ready() -> void:
	name = "StreetPage"
	horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	follow_focus = true
	_content = VBoxContainer.new()
	_content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_content.add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	add_child(_content)
	_content.add_child(UiKit.page_header("Street", "Every game is a street. Tap one to see how it plays!"))
	var rules: GameRules = load(RULES_PATH) as GameRules
	for street: Dictionary in STREETS:
		var id: StringName = street["id"] as StringName
		var card: Control = _card(street, rules)
		cards[id] = card
		_content.add_child(card)


## Whether the street can be played now.
static func playable(id: StringName) -> bool:
	return id in RoomService.PLAYABLE_MAP_IDS


## The lines of how the street plays, with the numbers from `rules` (a street that is not built yet has one line saying so).
static func mechanics_for(id: StringName, rules: GameRules) -> Array[String]:
	if not playable(id):
		return [SOON_LINE] as Array[String]
	var choices: PackedStringArray = PackedStringArray()
	for points: int in rules.first_to_choices:
		choices.append(str(points))
	var to_win: String = ", ".join(choices.slice(0, choices.size() - 1)) + " or " + choices[choices.size() - 1] if choices.size() > 1 else choices[0]
	return [
		"Two teams! The first to %s points wins, and the host picks." % to_win,
		"Tap the street to walk there. Drag a card onto the street to throw it, or drop it back on your hand to cancel.",
		"You hold four cards! Each costs ice water, which refills over time.",
		"Run too long and you can only walk until your stamina is full!",
		UiText.f("Tagged kids wait on the sidewalk. A teammate who stays in the rescue area for %d s frees the first one tagged!", [ceili(rules.rescue_hold_time)]),
		UiText.f("Stay in their base to capture it: %d s alone, faster with friends! You can't throw while capturing.", [ceili(rules.capture_solo_time)]),
		UiText.f("Or tag them all and hold it for %d s!", [ceili(rules.capture_time)]),
	] as Array[String]


func _card(street: Dictionary, rules: GameRules) -> Control:
	var id: StringName = street["id"] as StringName
	var column: VBoxContainer = VBoxContainer.new()
	column.add_theme_constant_override("separation", UiMetrics.GAP)
	var head: HBoxContainer = HBoxContainer.new()
	head.add_theme_constant_override("separation", UiMetrics.GAP)
	var title: Label = Label.new()
	title.name = "Name"
	title.text = String(street["name"])
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	title.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	UiKit.letter(title, UiMetrics.HEADLINE, KalyeahTheme.semantic_color(String(street["color"])).lightened(0.55))
	head.add_child(title)
	var open: bool = playable(id)
	var chip: PanelContainer = UiKit.chip("Playable" if open else "Coming soon", UiKit.CHIP_GREEN if open else UiKit.CHIP_SLATE)
	chip.name = "Status"
	chip.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	head.add_child(chip)
	column.add_child(head)
	var game: Label = UiKit.text(String(street["game"]), UiMetrics.DETAIL, KalyeahTheme.AMBER, false)
	game.name = "Game"
	game.theme_type_variation = &"SectionLabel"
	column.add_child(game)
	var blurb: Label = UiKit.text(String(street["blurb"]), UiMetrics.DETAIL, KalyeahTheme.TEXT)
	blurb.name = "Blurb"
	blurb.add_theme_constant_override("line_spacing", KalyeahTheme.READING_LINE_SPACING)
	column.add_child(blurb)
	column.add_child(UiKit.divider())
	column.add_child(UiKit.section_label("How it plays"))
	var lines: VBoxContainer = VBoxContainer.new()
	lines.name = "Mechanics"
	lines.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	for line: String in mechanics_for(id, rules):
		lines.add_child(_line(line))
	column.add_child(lines)
	var card: PanelContainer = UiKit.card(column, "raised" if open else "glass")
	card.name = "Street_%s" % String(id)
	return card


## One line of mechanics: a small gold dot and the words beside it.
func _line(text: String) -> Control:
	var row: HBoxContainer = HBoxContainer.new()
	row.add_theme_constant_override("separation", UiMetrics.GAP)
	var dot: Panel = Panel.new()
	dot.custom_minimum_size = Vector2(UiMetrics.COMPACT_GAP, UiMetrics.COMPACT_GAP)
	dot.size_flags_vertical = Control.SIZE_SHRINK_BEGIN
	dot.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var fill: StyleBoxFlat = StyleBoxFlat.new()
	fill.bg_color = Color("ffc233")
	fill.set_corner_radius_all(UiSkin.round_radius(dot.custom_minimum_size.y))
	dot.add_theme_stylebox_override("panel", fill)
	var holder: MarginContainer = MarginContainer.new()
	holder.add_theme_constant_override("margin_top", UiMetrics.COMPACT_GAP)
	holder.mouse_filter = Control.MOUSE_FILTER_IGNORE
	holder.add_child(dot)
	row.add_child(holder)
	var words: Label = UiKit.text(text, UiMetrics.DETAIL, KalyeahTheme.TEXT)
	words.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	words.add_theme_constant_override("line_spacing", KalyeahTheme.READING_LINE_SPACING)
	row.add_child(words)
	return row
