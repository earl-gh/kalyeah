class_name HistoryPage
extends VBoxContainer
## The History tab: the last battles this phone played, newest first.


func _ready() -> void:
	name = "HistoryPage"
	add_theme_constant_override("separation", 12)
	var title: Label = Label.new()
	title.theme_type_variation = &"DisplayLabel"
	title.text = "HISTORY"
	title.add_theme_font_size_override("font_size", UiMetrics.TITLE)
	add_child(title)
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
		var empty: Label = Label.new()
		empty.name = "Empty"
		empty.text = "Play a match to start your history."
		empty.theme_type_variation = &"MutedLabel"
		empty.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		empty.add_theme_font_size_override("font_size", UiMetrics.BODY)
		list.add_child(empty)
		return
	var now: int = int(Time.get_unix_time_from_system())
	for entry: Dictionary in found:
		list.add_child(_row(entry, now))


func _row(entry: Dictionary, now: int) -> Control:
	var won: bool = bool(entry.get("won", false))
	var panel: PanelContainer = PanelContainer.new()
	panel.add_theme_stylebox_override("panel", KalyeahTheme.painted_panel(UiMetrics.PADDING))
	var column: VBoxContainer = VBoxContainer.new()
	column.add_theme_constant_override("separation", 8)
	panel.add_child(column)
	var line: HBoxContainer = HBoxContainer.new()
	line.add_theme_constant_override("separation", 14)
	column.add_child(line)
	var result: Label = Label.new()
	result.name = "Result"
	result.theme_type_variation = &"DisplayLabel"
	result.text = "VICTORY" if won else "DEFEAT"
	result.add_theme_font_size_override("font_size", UiMetrics.ACTION)
	result.add_theme_color_override("font_color", KalyeahTheme.SUCCESS if won else KalyeahTheme.ERROR)
	result.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	line.add_child(result)
	var score: Label = Label.new()
	score.text = "%d - %d" % [int(entry.get("mine", 0)), int(entry.get("theirs", 0))]
	score.add_theme_font_size_override("font_size", UiMetrics.BODY)
	score.custom_minimum_size = Vector2(90.0, 0.0)
	line.add_child(score)
	var detail: Label = Label.new()
	detail.theme_type_variation = &"MutedLabel"
	detail.text = "%s · %s" % [String(entry.get("mode", "")).capitalize(), MatchHistory.ago(int(entry.get("at", now)), now)]
	detail.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	detail.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	detail.add_theme_font_size_override("font_size", UiMetrics.DETAIL)
	detail.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	column.add_child(detail)
	return panel
