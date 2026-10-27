class_name GameOfMonthSheet
extends RefCounted
## The Game of the month (the calendar icon on the main screen): one Filipino street game for every month, taught in full (name, how many
## can play, what you need, the goal, how to set up, how to play, the rules, how to win, staying safe, a twist) so kids can go and play the
## real larong kalye in the street or the backyard. The games are in `data/rules/larong_kalye.tres`.

const GAMES_PATH: String = "res://data/rules/larong_kalye.tres"
const WIDTH: float = 640.0
const SECTION_INK: Color = Color("b8641a")


## The sheet for `month` (1 to 12; 0 is the current month on this phone's clock).
static func build(month: int = 0) -> MenuDialog:
	if month < 1 or month > 12:
		month = int(Time.get_date_dict_from_system()["month"])
	var games: LarongKalye = load(GAMES_PATH) as LarongKalye
	var game: Dictionary = games.for_month(month)
	var card: MenuDialog = MenuDialog.create(String(game["name"]), WIDTH)
	card.name = "GameOfMonthSheet"
	var kicker: Label = card.add_label("%s · Game of the month" % LarongKalye.month_name(month), UiMetrics.DETAIL, KalyeahTheme.MODAL_TEXT_MUTED)
	kicker.name = "Month"
	var also: Label = card.add_label(String(game["also"]), UiMetrics.DETAIL, KalyeahTheme.MODAL_TEXT_MUTED)
	also.name = "Also"
	var chips: HFlowContainer = HFlowContainer.new()
	chips.name = "Facts"
	chips.add_theme_constant_override("h_separation", 10)
	chips.add_theme_constant_override("v_separation", 8)
	chips.add_child(UiKit.chip(_players(int(game["players_min"]), int(game["players_max"])), UiKit.CHIP_BLUE))
	chips.add_child(UiKit.chip(String(game["time"]), UiKit.CHIP_GREEN))
	chips.add_child(UiKit.chip(String(game["place"]), UiKit.CHIP_PURPLE))
	card.body.add_child(chips)
	_section(card, "What you need", game["items"] as Array, false)
	_paragraph(card, "The goal", String(game["goal"]))
	_section(card, "Set up", game["setup"] as Array, true)
	_section(card, "How to play", game["how"] as Array, true)
	_section(card, "Rules", game["rules"] as Array, false)
	_paragraph(card, "How to win", String(game["win"]))
	_section(card, "Stay safe", game["safety"] as Array, false)
	_paragraph(card, "Try this twist", String(game["twist"]))
	card.add_hint("Put the phone down, gather your friends and play it for real!").horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	var close: Button = MenuDialog.make_button("Close", "grey", UiMetrics.BODY)
	close.name = "Close"
	close.pressed.connect(card.close)
	card.add_buttons([close])
	return card


## "6 to 12 players".
static func _players(low: int, high: int) -> String:
	return "%d to %d players" % [low, high] if low != high else "%d players" % low


static func _paragraph(card: MenuDialog, heading: String, text: String) -> void:
	var column: VBoxContainer = VBoxContainer.new()
	column.name = heading.replace(" ", "")
	column.add_theme_constant_override("separation", 4)
	var title: Label = UiKit.section_label(heading)
	title.add_theme_color_override("font_color", SECTION_INK)
	column.add_child(title)
	column.add_child(_label_of(text))
	card.body.add_child(column)


## A list under a heading: numbered steps or bullets.
static func _section(card: MenuDialog, heading: String, lines: Array, numbered: bool) -> void:
	var column: VBoxContainer = VBoxContainer.new()
	column.name = heading.replace(" ", "")
	column.add_theme_constant_override("separation", 4)
	var title: Label = UiKit.section_label(heading)
	title.add_theme_color_override("font_color", SECTION_INK)
	column.add_child(title)
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
