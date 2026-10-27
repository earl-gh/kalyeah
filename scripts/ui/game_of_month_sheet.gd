class_name GameOfMonthSheet
extends RefCounted
## "Game of the Month" (the calendar icon on the main screen): one Filipino street game for every month, in a few short lines (Clash
## style: verbs first, one idea a line) so kids can go and play the real larong kalye in the street or the backyard: who can play, what you
## need, the goal, set up, how to play, the rules and one safety line. The games are in `data/rules/larong_kalye.tres`.

const TITLE: String = "Game of the Month"
const GAMES_PATH: String = "res://data/rules/larong_kalye.tres"
const WIDTH: float = 620.0
const SECTION_INK: Color = Color("b8641a")


## The sheet for `month` (1 to 12; 0 is the current month on this phone's clock).
static func build(month: int = 0) -> MenuDialog:
	if month < 1 or month > 12:
		month = int(Time.get_date_dict_from_system()["month"])
	var games: LarongKalye = load(GAMES_PATH) as LarongKalye
	var game: Dictionary = games.for_month(month)
	var card: MenuDialog = MenuDialog.create(TITLE, WIDTH)
	card.name = "GameOfMonthSheet"
	var kicker: Label = card.add_label(LarongKalye.month_name(month).to_upper(), UiMetrics.CAPTION, SECTION_INK, HORIZONTAL_ALIGNMENT_CENTER)
	kicker.name = "Month"
	var name_label: Label = UiKit.text(String(game["name"]), UiMetrics.HEADLINE, KalyeahTheme.MODAL_TEXT, false)
	name_label.name = "GameName"
	name_label.theme_type_variation = &"DisplayLabel"
	name_label.add_theme_constant_override("outline_size", 0)
	name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	card.body.add_child(name_label)
	var also: String = String(game["also"])
	if not also.is_empty():
		var other: Label = card.add_label(also, UiMetrics.DETAIL, KalyeahTheme.MODAL_TEXT_MUTED, HORIZONTAL_ALIGNMENT_CENTER)
		other.name = "Also"
	var chips: HFlowContainer = HFlowContainer.new()
	chips.name = "Facts"
	chips.alignment = FlowContainer.ALIGNMENT_CENTER
	chips.add_theme_constant_override("h_separation", 10)
	chips.add_theme_constant_override("v_separation", 8)
	chips.add_child(UiKit.chip(_players(int(game["players_min"]), int(game["players_max"])), UiKit.CHIP_BLUE))
	chips.add_child(UiKit.chip(String(game["time"]), UiKit.CHIP_GREEN))
	chips.add_child(UiKit.chip(String(game["place"]), UiKit.CHIP_PURPLE))
	card.body.add_child(chips)
	_paragraph(card, "You need", _joined(game["items"] as Array))
	_paragraph(card, "Goal", String(game["goal"]))
	_list(card, "Set up", game["setup"] as Array, true)
	_list(card, "How to play", game["how"] as Array, true)
	_list(card, "Rules", game["rules"] as Array, false)
	_paragraph(card, "Stay safe", String(game["safety"]))
	var close: Button = MenuDialog.make_button("Got it!", "orange", UiMetrics.BODY)
	close.name = "Close"
	close.pressed.connect(card.close)
	card.add_buttons([close])
	return card


## The items on one line: "1 tin can, 1 tsinelas each, chalk".
static func _joined(items: Array) -> String:
	var parts: PackedStringArray = []
	for i: int in items.size():
		var item: String = String(items[i])
		parts.append(item if i == 0 else item.substr(0, 1).to_lower() + item.substr(1))
	return ", ".join(parts)


## "6 to 12 players".
static func _players(low: int, high: int) -> String:
	return "%d to %d players" % [low, high] if low != high else "%d players" % low


static func _heading(text: String) -> Label:
	var label: Label = UiKit.section_label(text)
	label.add_theme_color_override("font_color", SECTION_INK)
	return label


static func _paragraph(card: MenuDialog, heading: String, text: String) -> void:
	var column: VBoxContainer = VBoxContainer.new()
	column.name = heading.replace(" ", "")
	column.add_theme_constant_override("separation", 2)
	column.add_child(_heading(heading))
	column.add_child(_label_of(text))
	card.body.add_child(column)


## A short list under a heading: numbered steps or dots.
static func _list(card: MenuDialog, heading: String, lines: Array, numbered: bool) -> void:
	var column: VBoxContainer = VBoxContainer.new()
	column.name = heading.replace(" ", "")
	column.add_theme_constant_override("separation", 2)
	column.add_child(_heading(heading))
	for i: int in lines.size():
		column.add_child(_label_of(("%d. %s" % [i + 1, lines[i]]) if numbered else ("· %s" % lines[i])))
	card.body.add_child(column)


static func _label_of(text: String) -> Label:
	var label: Label = Label.new()
	label.text = text
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.add_theme_font_size_override("font_size", UiMetrics.DETAIL)
	label.add_theme_constant_override("line_spacing", KalyeahTheme.READING_LINE_SPACING)
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	return label
