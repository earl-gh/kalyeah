class_name StreetPage
extends VBoxContainer
## The Games page (the left tab of the bottom bar): every game of the barangay with its name and picture, what the game
## is and how it plays here. Agawan Base is open everywhere; Tumbang Preso and Patintero are played in Practice for now (the lobby's map
## list still says Coming soon). The numbers come from the rules, so the words cannot go stale.

## The games in order: the map id (RoomService.MAP_IDS), the game's name, its picture, a colour, and what the game is.
const STREETS: Array[Dictionary] = [
	{"id": &"street", "game": "Agawan Base", "picture": preload("res://assets/ui/games/agawan_base.webp"), "color": "blue",
		"blurb": "Two teams, two bases! Tag the other team, free your own, and hold their base."},
	{"id": &"tumbang_preso", "game": "Tumbang Preso", "picture": preload("res://assets/ui/games/tumbang_preso.webp"), "color": "orange",
		"blurb": "5 minutes: Knock the can over, fetch your slipper, and run to the other end to earn a score. The 'it' fetches the can and places it back to position. It can't tag players when the can is knocked over and not in position."},
	{"id": &"patintero", "game": "Patintero", "picture": preload("res://assets/ui/games/patintero.webp"), "color": "green",
		"blurb": "5 minutes: Get past the enemy guards, run to the other end and back to earn a team point. Enemy players that act as guards tag you or your ally to reduce the chances of your team making it to the other end and back."},
]
## How big a game's picture is drawn beside its name (the picture keeps its own shape inside this square).
const PICTURE_SIZE: float = 168.0
## What a street that is not built yet says where its mechanics will be.
const SOON_LINE: String = "How it plays here is coming soon."
const RULES_PATH: String = "res://data/rules/game_rules.tres"

var cards: Dictionary[StringName, Control] = {}
var _content: VBoxContainer


func _ready() -> void:
	name = "StreetPage"
	add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	add_child(UiKit.page_header("Games"))
	_content = VBoxContainer.new()
	_content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_content.add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	UiKit.add_gutter_scroll(self, _content, true)
	var rules: GameRules = load(RULES_PATH) as GameRules
	for street: Dictionary in STREETS:
		var id: StringName = street["id"] as StringName
		var card: Control = _card(street, rules)
		cards[id] = card
		_content.add_child(card)


## Whether the street can be played now in a room (Online or Local).
static func playable(id: StringName) -> bool:
	return id in RoomService.PLAYABLE_MAP_IDS


## Whether the street can be played in Practice only (Tumbang Preso, Patintero).
static func practice_only(id: StringName) -> bool:
	return not playable(id) and StreetMode.RULES_PATHS.has(id)


## The lines of how the street plays, with the numbers from `rules` (a street that is not built yet has one line saying so).
static func mechanics_for(id: StringName, rules: GameRules) -> Array[String]:
	if practice_only(id):
		return street_game_lines(StreetMode.rules_for(id))
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
		UiText.t("Tag the whole enemy team to score immediately!"),
	] as Array[String]


## How Tumbang Preso or Patintero plays, with its numbers.
static func street_game_lines(game: StreetModeRules) -> Array[String]:
	var minutes: int = roundi(game.match_seconds / 60.0)
	if game.id == &"tumbang_preso":
		return [
			UiText.f("%d minutes! Every kid plays for itself and the most points wins. Play it in Practice.", [minutes]),
			"Only one kid is the taya, the 'it'. Everyone else throws.",
			"Hold Throw to aim: the line grows the longer you hold. Let go to throw your slipper at the can.",
			"Knock over the can, fetch your slipper, and run to the other end to earn a score.",
			"The 'it' fetches the can and places it back. It can't tag you while the can is knocked over!",
			"Out in the fetch area you wear a tag circle: the 'it' tags you by moving into it, and you become the 'it'. Then everyone goes back to their places.",
			UiText.f("Stay behind the throw line while you hold your slipper. Too scared to fetch it for %d s? You're the taya!", [ceili(game.scared_seconds)]),
		] as Array[String]
	return [
		UiText.f("%d minutes! The team with more points wins. Play it in Practice.", [minutes]),
		"Get past the enemy guards, run to the other end and back to earn a team point.",
		"The court has two lines across the street and one down the middle, and two more lines that make a narrow corridor in the middle. Guards stay on their line; runners run free, but only in the corridor.",
		"Get past the last guard, then back to the base. ONE runner doing it is the team point, and every tagged runner is free again.",
		"Runners have a tag circle: a guard tags a runner by moving into it. A tagged runner waits in the pen at the side and can only cheer in quick chat.",
		UiText.f("Tag all three runners to earn the guards' point, and the teams swap roles. Runners who don't get past the first guard in %d s give the guards the point.", [ceili(game.stall_seconds)]),
		UiText.f("Dive to get past, or (a guard) to catch beyond your line: you run back to your line after. The cooldown of %d s starts when you are on your feet or back on your line.", [ceili(game.dive_cooldown)]),
	] as Array[String]


func _card(street: Dictionary, rules: GameRules) -> Control:
	var id: StringName = street["id"] as StringName
	var column: VBoxContainer = VBoxContainer.new()
	column.add_theme_constant_override("separation", UiMetrics.GAP)
	var head: HBoxContainer = HBoxContainer.new()
	head.add_theme_constant_override("separation", UiMetrics.GAP)
	var picture: TextureRect = TextureRect.new()
	picture.name = "Picture"
	picture.texture = street["picture"] as Texture2D
	picture.custom_minimum_size = Vector2(PICTURE_SIZE, PICTURE_SIZE)
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	picture.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	picture.size_flags_vertical = Control.SIZE_SHRINK_BEGIN
	picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	head.add_child(picture)
	var side: VBoxContainer = VBoxContainer.new()
	side.add_theme_constant_override("separation", UiMetrics.GAP)
	side.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	head.add_child(side)
	var title: Label = Label.new()
	title.name = "Name"
	title.text = String(street["game"])
	title.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	UiKit.letter(title, UiMetrics.HEADLINE, KalyeahTheme.semantic_color(String(street["color"])).lightened(0.55))
	side.add_child(title)
	var open: bool = playable(id) or practice_only(id)
	var chip: PanelContainer = UiKit.chip("Playable" if playable(id) else ("Practice" if open else "Coming soon"), UiKit.CHIP_GREEN if open else UiKit.CHIP_SLATE)
	chip.name = "Status"
	chip.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	side.add_child(chip)
	var blurb: Label = UiKit.text(String(street["blurb"]), UiMetrics.DETAIL, KalyeahTheme.TEXT)
	blurb.name = "Blurb"
	blurb.add_theme_constant_override("line_spacing", KalyeahTheme.READING_LINE_SPACING)
	side.add_child(blurb)
	column.add_child(head)
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
