class_name HistoryPage
extends VBoxContainer
## The History page: the last battles this phone played, newest first, each a card with a big
## Victory / Defeat badge, the score and when and where it was played.


func _ready() -> void:
	name = "HistoryPage"
	add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	add_child(UiKit.page_header("History", "Your last battles on this phone."))
	var scroll: ScrollContainer = ScrollContainer.new()
	scroll.name = "Scroll"
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	add_child(scroll)
	var list: VBoxContainer = VBoxContainer.new()
	list.name = "List"
	list.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	list.add_theme_constant_override("separation", UiMetrics.GAP)
	scroll.add_child(list)
	refresh()


## Rebuilds the list from the saved battles.
func refresh() -> void:
	var list: VBoxContainer = find_child("List", true, false) as VBoxContainer
	if list == null:
		return
	for child: Node in list.get_children():
		list.remove_child(child)
		child.queue_free()
	var found: Array[Dictionary] = MatchHistory.entries()
	if found.is_empty():
		list.add_child(_empty())
		return
	var now: int = int(Time.get_unix_time_from_system())
	for entry: Dictionary in found:
		list.add_child(_row(entry, now))


## Nothing yet: a friendly card instead of a blank page.
func _empty() -> Control:
	var column: VBoxContainer = VBoxContainer.new()
	column.name = "Empty"
	column.alignment = BoxContainer.ALIGNMENT_CENTER
	column.add_theme_constant_override("separation", 8)
	var title: Label = UiKit.text("No battles yet", UiMetrics.ACTION)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(title)
	var hint: Label = UiKit.text("Play a match to start your history.", UiMetrics.DETAIL, Color("9fb0d6"))
	hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(hint)
	return UiKit.card(column, "glass", 40.0)


func _row(entry: Dictionary, now: int) -> Control:
	var won: bool = bool(entry.get("won", false))
	var accent: Color = Color("2fbf5a") if won else Color("e0463e")
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
	score.theme_type_variation = &"DisplayLabel"
	score.text = "%d - %d" % [int(entry.get("mine", 0)), int(entry.get("theirs", 0))]
	score.add_theme_font_size_override("font_size", 44)
	score.add_theme_constant_override("outline_size", 6)
	score.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	row.add_child(score)
	return UiKit.card(row, "glass", 22.0)
