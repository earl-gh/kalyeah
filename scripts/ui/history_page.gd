class_name HistoryPage
extends VBoxContainer
## The History page: the last battles this phone played, newest first, each a card with a big
## Victory / Defeat badge, the score and when and where it was played.

## The empty state's button: go and play.
signal play_pressed

const FILTERS: Array[Dictionary] = [
	{"id": &"", "label": "All"}, {"id": &"practice", "label": "Practice"},
	{"id": &"hotspot", "label": "Local"}, {"id": &"online", "label": "Online"},
]

## Which battles are shown: every mode (an empty name) or one.
var filter: StringName = &""
var summary_card: Control
var filter_bar: UiSegmented
var played_label: Label
var rate_label: Label
var streak_label: Label

var _top: VBoxContainer
var _list: VBoxContainer


func _ready() -> void:
	name = "HistoryPage"
	add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	add_child(UiKit.page_header("History", "Your last battles on this phone."))
	_top = VBoxContainer.new()
	_top.name = "Top"
	_top.add_theme_constant_override("separation", UiMetrics.GAP)
	add_child(_top)
	summary_card = _summary()
	_top.add_child(summary_card)
	filter_bar = UiSegmented.new(FILTERS, UiMetrics.TOUCH, UiMetrics.CAPTION)
	filter_bar.chosen.connect(show_filter)
	_top.add_child(filter_bar)
	var scroll: ScrollContainer = ScrollContainer.new()
	scroll.name = "Scroll"
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	add_child(scroll)
	_list = VBoxContainer.new()
	_list.name = "List"
	_list.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_list.add_theme_constant_override("separation", UiMetrics.GAP)
	scroll.add_child(_list)
	refresh()


## Shows the battles of one mode ("practice", "hotspot", "online") or all of them (an empty name).
func show_filter(id: StringName) -> void:
	filter = id
	filter_bar.select(id, false)
	refresh()


## Played, win rate and streak: three big figures over the list.
func _summary() -> Control:
	var row: HBoxContainer = HBoxContainer.new()
	row.name = "Summary"
	row.add_theme_constant_override("separation", UiMetrics.GAP)
	played_label = _figure(row, "Played", Color("ffd25a"))
	rate_label = _figure(row, "Won", Color("6fe39a"))
	streak_label = _figure(row, "Streak", Color("8fd0ff"))
	return UiKit.card(row, "glass", 18.0)


func _figure(parent: HBoxContainer, caption: String, color: Color) -> Label:
	var column: VBoxContainer = VBoxContainer.new()
	column.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	column.add_theme_constant_override("separation", 0)
	var number: Label = Label.new()
	number.name = caption
	UiKit.letter(number, UiMetrics.DISPLAY, color)
	number.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(number)
	var label: Label = UiKit.text(caption, UiMetrics.CAPTION, Color("9fb0d6"), false)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(label)
	parent.add_child(column)
	return number


## Rebuilds the figures and the list from the saved battles (of the chosen mode).
func refresh() -> void:
	if _list == null:
		return
	for child: Node in _list.get_children():
		_list.remove_child(child)
		child.queue_free()
	var all: Array[Dictionary] = MatchHistory.entries()
	_top.visible = not all.is_empty()
	if all.is_empty():
		_list.add_child(_empty())
		return
	var found: Array[Dictionary] = MatchHistory.of_mode(all, String(filter))
	var numbers: Dictionary = MatchHistory.summary(found)
	played_label.text = str(numbers["played"])
	rate_label.text = "%d%%" % int(numbers["rate"]) if int(numbers["played"]) > 0 else "-"
	streak_label.text = str(numbers["streak"])
	if found.is_empty():
		_list.add_child(_empty("No %s battles yet" % String(filter), "Play one and it shows here."))
		return
	var now: int = int(Time.get_unix_time_from_system())
	for entry: Dictionary in found:
		_list.add_child(_row(entry, now))


## Nothing yet: a friendly card instead of a blank page.
func _empty(heading: String = "No battles yet", hint_text: String = "Play a match to start your history.") -> Control:
	var column: VBoxContainer = VBoxContainer.new()
	column.name = "Empty"
	column.alignment = BoxContainer.ALIGNMENT_CENTER
	column.add_theme_constant_override("separation", 8)
	var title: Label = UiKit.text(heading, UiMetrics.ACTION)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(title)
	var hint: Label = UiKit.text(hint_text, UiMetrics.DETAIL, Color("9fb0d6"))
	hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(hint)
	var play: Button = MenuDialog.make_button("Play a match", "orange", UiMetrics.ACTION, Vector2(0.0, UiMetrics.TOUCH))
	play.name = "PlayButton"
	play.pressed.connect(play_pressed.emit)
	column.add_child(play)
	return UiKit.card(column, "glass", 40.0)


func _row(entry: Dictionary, now: int) -> Control:
	var won: bool = bool(entry.get("won", false))
	var accent: Color = UiKit.CHIP_GREEN if won else UiKit.CHIP_RED
	var row: HBoxContainer = HBoxContainer.new()
	row.add_theme_constant_override("separation", UiMetrics.GAP)
	var badge: PanelContainer = UiKit.chip("Victory" if won else "Defeat", accent, Color.WHITE, UiMetrics.DETAIL)
	badge.name = "Result"
	badge.custom_minimum_size = Vector2(176.0, 0.0)
	badge.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	(badge.get_node("Text") as Label).horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	row.add_child(badge)
	var words: VBoxContainer = VBoxContainer.new()
	words.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	words.add_theme_constant_override("separation", 0)
	words.add_child(UiKit.text(String(entry.get("mode", "")).capitalize(), UiMetrics.BODY))
	var when: Label = UiKit.text(MatchHistory.ago(int(entry.get("at", now)), now), UiMetrics.CAPTION, Color("9fb0d6"))
	when.name = "When"
	words.add_child(when)
	row.add_child(words)
	var score: Label = Label.new()
	score.name = "Score"
	score.text = "%d - %d" % [int(entry.get("mine", 0)), int(entry.get("theirs", 0))]
	UiKit.letter(score, UiMetrics.HEADLINE)
	score.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	row.add_child(score)
	return UiKit.card(row, "glass", 22.0)
