class_name HistoryPage
extends VBoxContainer
## The History page: prominent Tagalog Ranked Banner Card ("Kahoy" to "Batang Kalyeah") at the top,
## mode filters, battle KPIs, and the last battles this device played, newest first.

## The empty state's button: go and play.
signal play_pressed

const FILTERS: Array[Dictionary] = [
	{"id": &"", "label": "All"}, {"id": &"practice", "label": "Practice"},
	{"id": &"hotspot", "label": "Local"}, {"id": &"online", "label": "Online"},
]


class CrestIcon extends Control:
	var lp: int = 0

	func set_lp(value: int) -> void:
		if lp == value:
			return
		lp = value
		queue_redraw()

	func _draw() -> void:
		var span: float = minf(size.x, size.y) * 0.45
		RankedSystem.draw_emblem(self, size * 0.5, span, lp)


class RankedProgressBar extends Control:
	var ratio: float = 0.0
	var fill_color: Color = Color("f5a623")
	var accent_color: Color = Color("ffd97d")

	func set_progress(value: float, fill: Color, accent: Color) -> void:
		ratio = clampf(value, 0.0, 1.0)
		fill_color = fill
		accent_color = accent
		queue_redraw()

	func _draw() -> void:
		var rect: Rect2 = Rect2(Vector2.ZERO, size)
		if rect.size.x <= 4.0 or rect.size.y <= 4.0:
			return
		draw_rect(rect, Color("dfd0b4"), true)
		var inner: Rect2 = rect.grow(-1.5)
		draw_rect(inner, Color("efe4cd"), true)
		var fill_w: float = inner.size.x * ratio
		if fill_w > 1.0:
			draw_rect(Rect2(inner.position, Vector2(fill_w, inner.size.y)), fill_color, true)
			draw_rect(Rect2(inner.position, Vector2(fill_w, inner.size.y * 0.42)), accent_color.lerp(Color("faf3e3"), 0.35), true)
		draw_rect(rect, Color(Color("2a2522"), 0.36), false, 1.5)


## Which battles are shown: every mode (an empty name) or one.
var filter: StringName = &""
var rank_card: PanelContainer
var summary_card: Control
var filter_bar: UiSegmented
var played_label: Label
var rate_label: Label
var streak_label: Label

var rank_emblem: CrestIcon
var rank_tier_label: Label
var rank_lp_chip_label: Label
var rank_progress_bar: RankedProgressBar
var rank_next_label: Label
var tier_badges_flow: HFlowContainer

var _content: VBoxContainer
var _top: VBoxContainer
var _list: VBoxContainer
var _tier_badges: Array[PanelContainer] = []
var _tier_badge_labels: Array[Label] = []
var _rank_card_style: StyleBoxFlat
var _active_badge_style: StyleBoxFlat
var _unlocked_badge_style: StyleBoxFlat
var _locked_badge_style: StyleBoxFlat
var _lp_chip_style: StyleBoxFlat


func _ready() -> void:
	name = "HistoryPage"
	_build_badge_styles()
	add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	add_child(UiKit.page_header("History"))
	_content = VBoxContainer.new()
	_content.name = "Content"
	_content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_content.add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	_top = VBoxContainer.new()
	_top.name = "Top"
	_top.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_top.add_theme_constant_override("separation", UiMetrics.GAP)
	_content.add_child(_top)
	rank_card = _build_rank_banner()
	summary_card = _summary()
	filter_bar = UiSegmented.new(FILTERS, UiMetrics.TOUCH, UiMetrics.CAPTION)
	filter_bar.chosen.connect(show_filter)
	_top.add_child(rank_card)
	_top.add_child(filter_bar)
	_top.add_child(summary_card)
	_list = VBoxContainer.new()
	_list.name = "List"
	_list.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_list.add_theme_constant_override("separation", UiMetrics.GAP)
	_content.add_child(_list)
	UiKit.add_gutter_scroll(self, _content)
	refresh()


func _build_badge_styles() -> void:
	_rank_card_style = StyleBoxFlat.new()
	_rank_card_style.bg_color = Color("faf3e3")
	_rank_card_style.border_color = Color("f5a623")
	_rank_card_style.set_border_width_all(2)
	_rank_card_style.set_corner_radius_all(UiSkin.PLATE_RADIUS)
	_rank_card_style.shadow_color = Color(Color("2a2522"), 0.20)
	_rank_card_style.shadow_size = 5
	_rank_card_style.shadow_offset = Vector2(0.0, 3.0)
	_rank_card_style.set_content_margin_all(18.0)
	_rank_card_style.anti_aliasing = true

	_active_badge_style = StyleBoxFlat.new()
	_active_badge_style.bg_color = Color("fff3d1")
	_active_badge_style.border_color = Color("f5a623")
	_active_badge_style.set_border_width_all(2)
	_active_badge_style.set_corner_radius_all(UiSkin.RADIUS_M)
	_active_badge_style.content_margin_left = 10.0
	_active_badge_style.content_margin_right = 12.0
	_active_badge_style.content_margin_top = 6.0
	_active_badge_style.content_margin_bottom = 6.0
	_active_badge_style.anti_aliasing = true

	_unlocked_badge_style = StyleBoxFlat.new()
	_unlocked_badge_style.bg_color = Color("e3f5f1")
	_unlocked_badge_style.border_color = Color("1f9d8b")
	_unlocked_badge_style.set_border_width_all(2)
	_unlocked_badge_style.set_corner_radius_all(UiSkin.RADIUS_M)
	_unlocked_badge_style.content_margin_left = 10.0
	_unlocked_badge_style.content_margin_right = 12.0
	_unlocked_badge_style.content_margin_top = 6.0
	_unlocked_badge_style.content_margin_bottom = 6.0
	_unlocked_badge_style.anti_aliasing = true

	_locked_badge_style = StyleBoxFlat.new()
	_locked_badge_style.bg_color = Color("efe5d2")
	_locked_badge_style.border_color = Color("cbbba0")
	_locked_badge_style.set_border_width_all(1)
	_locked_badge_style.set_corner_radius_all(UiSkin.RADIUS_M)
	_locked_badge_style.content_margin_left = 10.0
	_locked_badge_style.content_margin_right = 12.0
	_locked_badge_style.content_margin_top = 6.0
	_locked_badge_style.content_margin_bottom = 6.0
	_locked_badge_style.anti_aliasing = true

	_lp_chip_style = StyleBoxFlat.new()
	_lp_chip_style.bg_color = Color("136e61")
	_lp_chip_style.border_color = Color("0d4f46")
	_lp_chip_style.set_border_width_all(2)
	_lp_chip_style.set_corner_radius_all(UiSkin.RADIUS_M)
	_lp_chip_style.content_margin_left = 14.0
	_lp_chip_style.content_margin_right = 14.0
	_lp_chip_style.content_margin_top = 4.0
	_lp_chip_style.content_margin_bottom = 4.0
	_lp_chip_style.anti_aliasing = true


func _build_rank_banner() -> PanelContainer:
	var wrapper: VBoxContainer = VBoxContainer.new()
	wrapper.name = "RankBannerBody"
	wrapper.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)

	var hero_row: HBoxContainer = HBoxContainer.new()
	hero_row.add_theme_constant_override("separation", UiMetrics.GAP)
	hero_row.alignment = BoxContainer.ALIGNMENT_CENTER
	wrapper.add_child(hero_row)

	rank_emblem = CrestIcon.new()
	rank_emblem.name = "RankEmblem"
	rank_emblem.custom_minimum_size = Vector2(84.0, 84.0)
	rank_emblem.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	rank_emblem.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hero_row.add_child(rank_emblem)

	var info_col: VBoxContainer = VBoxContainer.new()
	info_col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	info_col.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	info_col.add_theme_constant_override("separation", 6)
	hero_row.add_child(info_col)

	var title_row: HBoxContainer = HBoxContainer.new()
	title_row.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	info_col.add_child(title_row)

	rank_tier_label = Label.new()
	rank_tier_label.name = "RankTier"
	rank_tier_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	rank_tier_label.add_theme_font_override("font", KalyeahTheme.DISPLAY_FONT)
	rank_tier_label.add_theme_font_size_override("font_size", UiMetrics.HEADLINE)
	rank_tier_label.add_theme_color_override("font_color", Color("2a2522"))
	rank_tier_label.add_theme_constant_override("outline_size", 0)
	title_row.add_child(rank_tier_label)

	var lp_chip: PanelContainer = PanelContainer.new()
	lp_chip.name = "RankLpChip"
	lp_chip.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	lp_chip.mouse_filter = Control.MOUSE_FILTER_IGNORE
	lp_chip.add_theme_stylebox_override("panel", _lp_chip_style)
	rank_lp_chip_label = Label.new()
	rank_lp_chip_label.name = "RankLpValue"
	rank_lp_chip_label.add_theme_font_override("font", KalyeahTheme.BUTTON_FONT)
	rank_lp_chip_label.add_theme_font_size_override("font_size", UiMetrics.CAPTION)
	rank_lp_chip_label.add_theme_color_override("font_color", Color("faf3e3"))
	lp_chip.add_child(rank_lp_chip_label)
	title_row.add_child(lp_chip)

	rank_progress_bar = RankedProgressBar.new()
	rank_progress_bar.name = "RankProgressBar"
	rank_progress_bar.custom_minimum_size = Vector2(0.0, 16.0)
	rank_progress_bar.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	rank_progress_bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	info_col.add_child(rank_progress_bar)

	rank_next_label = UiKit.text("", UiMetrics.CAPTION, Color("57493f"))
	rank_next_label.name = "RankNext"
	info_col.add_child(rank_next_label)

	var divider_line: ColorRect = ColorRect.new()
	divider_line.custom_minimum_size = Vector2(0.0, 1.0)
	divider_line.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	divider_line.color = Color(Color("2a2522"), 0.14)
	divider_line.mouse_filter = Control.MOUSE_FILTER_IGNORE
	wrapper.add_child(divider_line)

	tier_badges_flow = HFlowContainer.new()
	tier_badges_flow.name = "TierBadges"
	tier_badges_flow.alignment = FlowContainer.ALIGNMENT_CENTER
	tier_badges_flow.add_theme_constant_override("h_separation", 8)
	tier_badges_flow.add_theme_constant_override("v_separation", 8)
	wrapper.add_child(tier_badges_flow)

	_tier_badges.clear()
	_tier_badge_labels.clear()
	for i: int in RankedSystem.TIERS.size():
		var spec: Dictionary = RankedSystem.TIERS[i]
		var badge: PanelContainer = PanelContainer.new()
		badge.name = "TierBadge_%d" % i
		badge.mouse_filter = Control.MOUSE_FILTER_PASS
		badge.add_theme_stylebox_override("panel", _locked_badge_style)
		var item_row: HBoxContainer = HBoxContainer.new()
		item_row.add_theme_constant_override("separation", 6)
		item_row.alignment = BoxContainer.ALIGNMENT_CENTER
		var mini_crest: CrestIcon = CrestIcon.new()
		mini_crest.custom_minimum_size = Vector2(28.0, 28.0)
		mini_crest.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		mini_crest.mouse_filter = Control.MOUSE_FILTER_IGNORE
		mini_crest.set_lp(spec["min_lp"] as int)
		item_row.add_child(mini_crest)
		var tier_text: Label = Label.new()
		tier_text.name = "BadgeName"
		tier_text.text = spec["name"] as String
		tier_text.add_theme_font_override("font", KalyeahTheme.BUTTON_FONT)
		tier_text.add_theme_font_size_override("font_size", UiMetrics.CAPTION)
		tier_text.add_theme_color_override("font_color", Color("635447"))
		item_row.add_child(tier_text)
		badge.add_child(item_row)
		tier_badges_flow.add_child(badge)
		_tier_badges.append(badge)
		_tier_badge_labels.append(tier_text)

	var card: PanelContainer = PanelContainer.new()
	card.name = "RankBannerCard"
	card.mouse_filter = Control.MOUSE_FILTER_PASS
	card.add_theme_stylebox_override("panel", _rank_card_style)
	card.add_child(wrapper)
	return card


func _refresh_rank_banner() -> void:
	if rank_card == null:
		return
	var lp: int = RankedSystem.current_lp()
	var best_lp: int = RankedSystem.peak_lp()
	var active_idx: int = RankedSystem.tier_index(lp)
	var spec: Dictionary = RankedSystem.TIERS[active_idx]
	var tier_color: Color = spec["color"] as Color
	var tier_accent: Color = spec["accent"] as Color
	var tier_ink: Color = Color("2a2522").lerp(spec["dark"] as Color, 0.72)

	rank_emblem.set_lp(lp)
	rank_tier_label.text = (spec["name"] as String).to_upper()
	rank_tier_label.add_theme_color_override("font_color", tier_ink)
	rank_lp_chip_label.text = "%d LP" % lp
	rank_progress_bar.set_progress(RankedSystem.tier_progress(lp), tier_color, tier_accent)

	var next_lp: int = RankedSystem.next_tier_lp(lp)
	if next_lp > 0:
		var remaining: int = maxi(0, next_lp - lp)
		var next_title: String = RankedSystem.tier_name(next_lp)
		rank_next_label.text = "%d LP to %s · Peak: %d LP" % [remaining, next_title, best_lp]
	else:
		rank_next_label.text = "Batang Kalyeah Legend · Peak: %d LP" % best_lp

	for i: int in _tier_badges.size():
		var badge: PanelContainer = _tier_badges[i]
		var label: Label = _tier_badge_labels[i]
		var badge_spec: Dictionary = RankedSystem.TIERS[i]
		if i == active_idx:
			badge.add_theme_stylebox_override("panel", _active_badge_style)
			badge.modulate = Color.WHITE
			label.add_theme_color_override("font_color", Color("2a2522").lerp(badge_spec["dark"] as Color, 0.72))
		elif i < active_idx:
			badge.add_theme_stylebox_override("panel", _unlocked_badge_style)
			badge.modulate = Color.WHITE
			label.add_theme_color_override("font_color", Color("13594e"))
		else:
			badge.add_theme_stylebox_override("panel", _locked_badge_style)
			badge.modulate = Color.WHITE
			label.add_theme_color_override("font_color", Color("635447"))


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
	played_label = _figure(row, "Played", Color("f5a623"))
	rate_label = _figure(row, "Won", Color("28c7ae"))
	streak_label = _figure(row, "Streak", Color("ff8c66"))
	return UiKit.card(row, "glass", 18.0)


func _figure(parent: HBoxContainer, caption: String, color: Color) -> Label:
	var column: VBoxContainer = VBoxContainer.new()
	column.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	column.add_theme_constant_override("separation", 0)
	var number: Label = Label.new()
	number.name = caption
	UiKit.letter(number, UiMetrics.DISPLAY, color, Color("2a2522"))
	number.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(number)
	var label: Label = UiKit.text(caption, UiMetrics.CAPTION, KalyeahTheme.TEXT_MUTED, false)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(label)
	parent.add_child(column)
	return number


## Rebuilds the Ranked banner, the figures, and the list from the saved battles (of the chosen mode).
func refresh() -> void:
	if _list == null:
		return
	_refresh_rank_banner()
	for child: Node in _list.get_children():
		_list.remove_child(child)
		child.queue_free()
	var all: Array[Dictionary] = MatchHistory.entries()
	var found: Array[Dictionary] = MatchHistory.of_mode(all, String(filter))
	var numbers: Dictionary = MatchHistory.summary(found)
	played_label.text = str(numbers["played"])
	rate_label.text = "%d%%" % int(numbers["rate"]) if int(numbers["played"]) > 0 else "-"
	streak_label.text = str(numbers["streak"])
	if all.is_empty():
		_list.add_child(_empty())
		return
	if found.is_empty():
		_list.add_child(_empty("No %s battles yet" % String(filter), "Play one and it shows here."))
		return
	var now: int = int(Time.get_unix_time_from_system())
	for entry: Dictionary in found:
		_list.add_child(_row(entry, now))


## Nothing yet: a friendly card instead of a blank page.
func _empty(heading: String = "No battles yet", hint_text: String = "Win your first battle to fill your history!") -> Control:
	var column: VBoxContainer = VBoxContainer.new()
	column.name = "Empty"
	column.alignment = BoxContainer.ALIGNMENT_CENTER
	column.add_theme_constant_override("separation", 8)
	var title: Label = UiKit.text(heading, UiMetrics.ACTION)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(title)
	var hint: Label = UiKit.text(hint_text, UiMetrics.DETAIL, KalyeahTheme.TEXT_MUTED)
	hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(hint)
	var play: Button = MenuDialog.make_button("Start a battle!", "orange", UiMetrics.ACTION, Vector2(0.0, UiMetrics.TOUCH))
	play.name = "PlayButton"
	play.pressed.connect(play_pressed.emit)
	column.add_child(play)
	return UiKit.card(column, "glass", 40.0)


func _row(entry: Dictionary, now: int) -> Control:
	var won: bool = bool(entry.get("won", false))
	var accent: Color = Color("136e61") if won else Color("b83a28")
	var row: HBoxContainer = HBoxContainer.new()
	row.add_theme_constant_override("separation", UiMetrics.GAP)
	var badge: PanelContainer = UiKit.chip("Victory" if won else "Defeat", accent, Color("faf3e3"), UiMetrics.DETAIL)
	badge.name = "Result"
	badge.custom_minimum_size = Vector2(176.0, 0.0)
	badge.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	(badge.get_node("Text") as Label).horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	row.add_child(badge)
	var words: VBoxContainer = VBoxContainer.new()
	words.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	words.add_theme_constant_override("separation", 0)
	words.add_child(UiKit.text(String(entry.get("mode", "")).capitalize(), UiMetrics.BODY))
	var age_text: String = MatchHistory.ago(int(entry.get("at", now)), now)
	if entry.has("lp_delta"):
		var delta: int = int(entry.get("lp_delta", 0))
		var lp_part: String = "+%d LP" % delta if delta >= 0 else "%d LP" % delta
		age_text = "%s · %s" % [age_text, lp_part]
	var when: Label = UiKit.text(age_text, UiMetrics.CAPTION, KalyeahTheme.TEXT_MUTED)
	when.name = "When"
	words.add_child(when)
	row.add_child(words)
	var score: Label = Label.new()
	score.name = "Score"
	score.text = "%d - %d" % [int(entry.get("mine", 0)), int(entry.get("theirs", 0))]
	UiKit.letter(score, UiMetrics.HEADLINE, KalyeahTheme.CREAM, Color("2a2522"))
	score.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	row.add_child(score)
	return UiKit.card(row, "glass", 22.0)
