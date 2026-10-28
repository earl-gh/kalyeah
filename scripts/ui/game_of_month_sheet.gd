class_name GameOfMonthSheet
extends RefCounted
## "Game of the Month" (the calendar icon on the main screen): one Filipino street game for every month, in a few short lines (Clash
## style: verbs first, one idea a line) so kids can go and play the real larong kalye in the street or the backyard: who can play, what you
## need, set up, play, win and one safety line. The games are in `data/rules/larong_kalye.tres`.

const TITLE: String = "Game of the Month"
const ICON: Texture2D = preload("res://assets/ui/tips_icon.png")
const GAMES_PATH: String = "res://data/rules/larong_kalye.tres"
const WIDTH: float = MenuDialog.WIDTH_SHEET
const SECTION_INK: Color = Color("b8641a")


## The sheet for `month` (1 to 12; 0 is the current month on this phone's clock).
static func build(month: int = 0) -> MenuDialog:
	if month < 1 or month > 12:
		month = int(Time.get_date_dict_from_system()["month"])
	var games: LarongKalye = load(GAMES_PATH) as LarongKalye
	var game: Dictionary = games.for_month(month)
	var card: MenuDialog = MenuDialog.create(TITLE, WIDTH, ICON)
	card.name = "GameOfMonthSheet"
	var kicker: Label = card.add_label(LarongKalye.month_name(month).to_upper(), UiMetrics.CAPTION, SECTION_INK, HORIZONTAL_ALIGNMENT_CENTER)
	kicker.name = "Month"
	var name_label: Label = UiKit.text(String(game["name"]), UiMetrics.HEADLINE, KalyeahTheme.MODAL_TEXT, false)
	name_label.name = "GameName"
	name_label.theme_type_variation = &"DisplayLabel"
	name_label.add_theme_constant_override("outline_size", 0)
	name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	card.body.add_child(name_label)
	card.body.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	var facts: Label = card.add_label("%s · %s" % [_players(int(game["players_min"]), int(game["players_max"])), game["time"]], UiMetrics.DETAIL, KalyeahTheme.MODAL_TEXT_MUTED, HORIZONTAL_ALIGNMENT_CENTER)
	facts.name = "Facts"
	_paragraph(card, "You need", _joined(game["items"] as Array))
	_paragraph(card, "Set up", String(game["quick_setup"]))
	_paragraph(card, "Play", String(game["quick_play"]))
	_paragraph(card, "Win", String(game["quick_win"]))
	_paragraph(card, "Stay safe", String(game["safety"]))
	(card.find_child("DialogScroll", true, false) as ScrollContainer).vertical_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
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


static func _paragraph(card: MenuDialog, heading: String, text: String) -> void:
	var label: Label = _label_of("%s: %s" % [heading, text])
	label.name = heading.replace(" ", "")
	card.body.add_child(label)


static func _label_of(text: String) -> Label:
	var label: Label = Label.new()
	label.text = text
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.add_theme_font_size_override("font_size", UiMetrics.DETAIL)
	label.add_theme_constant_override("line_spacing", KalyeahTheme.READING_LINE_SPACING)
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	return label
