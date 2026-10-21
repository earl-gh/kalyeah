class_name RoomPlayerCards
extends RefCounted
## Team-row presentation, with full-size touch targets and explicit player identity.

const BAR_HEIGHT: float = 96.0
const NUMBER_WIDTH: float = 56.0
const NAME_SIZE: int = 26
const NUMBER_SIZE: int = 32
const TAG_SIZE: int = 22
const TEAM_BLOCKS: Array[Color] = [KalyeahTheme.TEAM_BLUE, KalyeahTheme.TEAM_RED]
const NAME_COLOR: Color = Color("f4f7ff")
const BAR_COLOR: Color = Color("14234f")

static func player_card(member: Dictionary, team: int, room: Dictionary) -> Control:
	var card: Control = Control.new()
	card.name = "Player%d" % (member["player_id"] as int)
	card.custom_minimum_size = Vector2(0.0, BAR_HEIGHT)
	card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var bar: Panel = Panel.new()
	bar.name = "Bar"
	bar.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = BAR_COLOR
	box.border_color = TEAM_BLOCKS[team].lightened(0.15)
	box.set_border_width_all(2)
	box.set_corner_radius_all(14)
	box.anti_aliasing = true
	box.shadow_color = Color(0.0, 0.0, 0.05, 0.45)
	box.shadow_size = 6
	box.shadow_offset = Vector2(0.0, 3.0)
	bar.add_theme_stylebox_override("panel", box)
	card.add_child(bar)
	var row: HBoxContainer = HBoxContainer.new()
	row.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	row.add_theme_constant_override("separation", 0)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(row)
	var name_label: Label = Label.new()
	name_label.name = "Name"
	name_label.text = member["name"] as String
	name_label.tooltip_text = name_label.text
	name_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	name_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT if team == 0 else HORIZONTAL_ALIGNMENT_RIGHT
	name_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	name_label.add_theme_font_size_override("font_size", NAME_SIZE)
	name_label.add_theme_color_override("font_color", NAME_COLOR)
	name_label.add_theme_color_override("font_outline_color", Color.TRANSPARENT)
	name_label.add_theme_constant_override("outline_size", 0)
	name_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var number: Control = _number_block(slot_number(team, _row_of(member, team, room), room.get("team_size", 1) as int), team, false)
	if team == 0:
		name_label.add_theme_constant_override("line_spacing", 0)
		row.add_child(number)
		row.add_child(_padded(name_label, 14.0, 6.0))
	else:
		row.add_child(_padded(name_label, 6.0, 14.0))
		row.add_child(number)
	var tag_text: String = ""
	if member["player_id"] == room.get("host", -1):
		tag_text = "Host"
	elif member["ready"]:
		tag_text = "Ready"
	if member["player_id"] == room.get("you", -1):
		tag_text = "You" + (" · " + tag_text if not tag_text.is_empty() else "")
	if not tag_text.is_empty():
		card.add_child(_tag(tag_text, team))
	return card


## The slot number: the places in the room counted down the left column (blue) and then the right
## (red): 1 to 3 and 4 to 6 in a 3v3, 1 to 2 and 3 to 4 in a 2v2, 1 and 2 in a 1v1.
static func slot_number(team: int, row: int, size: int) -> int:
	return team * size + row + 1


static func _row_of(member: Dictionary, team: int, room: Dictionary) -> int:
	var row: int = 0
	for other: Dictionary in room.get("members", []) as Array:
		if other["team"] != team:
			continue
		if other["player_id"] == member["player_id"]:
			return row
		row += 1
	return row


## The number in a solid team-coloured block (`hollow`: only the outline and the number, for a slot
## nobody has taken yet).
static func _number_block(number: int, team: int, hollow: bool) -> Control:
	var block: PanelContainer = PanelContainer.new()
	block.name = "Number"
	block.custom_minimum_size = Vector2(NUMBER_WIDTH, 0.0)
	block.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var box: StyleBoxFlat = StyleBoxFlat.new()
	if hollow:
		box.bg_color = Color(TEAM_BLOCKS[team], 0.28)
		box.border_color = Color(TEAM_BLOCKS[team].lightened(0.3), 0.6)
		box.set_border_width_all(2)
	else:
		box.bg_color = TEAM_BLOCKS[team]
	box.set_corner_radius_all(12)
	box.anti_aliasing = true
	block.add_theme_stylebox_override("panel", box)
	var label: Label = Label.new()
	label.text = str(number)
	label.theme_type_variation = &"DisplayLabel"
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.add_theme_font_size_override("font_size", NUMBER_SIZE)
	label.add_theme_color_override("font_color", TEAM_BLOCKS[team].lightened(0.35) if hollow else Color.WHITE)
	label.add_theme_color_override("font_outline_color", Color.TRANSPARENT)
	label.add_theme_constant_override("outline_size", 0)
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	block.add_child(label)
	return block


## A control with a margin each side (so the name does not touch the number block).
static func _padded(child: Control, left: float, right: float) -> Control:
	var margin: MarginContainer = MarginContainer.new()
	margin.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	margin.add_theme_constant_override("margin_top", 44)
	margin.add_theme_constant_override("margin_left", int(left))
	margin.add_theme_constant_override("margin_right", int(right))
	margin.mouse_filter = Control.MOUSE_FILTER_IGNORE
	margin.add_child(child)
	return margin


## The small Host / Ready tag in the far top corner (the right for blue, the left for red).
static func _tag(text: String, team: int) -> Control:
	var tag: PanelContainer = PanelContainer.new()
	tag.name = "Tag"
	tag.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = TEAM_BLOCKS[team]
	box.set_corner_radius_all(8)
	box.content_margin_left = 8.0
	box.content_margin_right = 8.0
	box.content_margin_top = 1.0
	box.content_margin_bottom = 1.0
	tag.add_theme_stylebox_override("panel", box)
	var label: Label = Label.new()
	label.text = text
	label.add_theme_font_size_override("font_size", TAG_SIZE)
	label.add_theme_color_override("font_color", Color.WHITE)
	label.add_theme_color_override("font_outline_color", Color.TRANSPARENT)
	label.add_theme_constant_override("outline_size", 0)
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tag.add_child(label)
	# pinned to the top corner and left to size itself to its text
	var right: float = 1.0 if team == 0 else 0.0
	tag.anchor_left = right
	tag.anchor_right = right
	tag.anchor_top = 0.0
	tag.anchor_bottom = 0.0
	tag.offset_left = 0.0
	tag.offset_right = 0.0
	tag.offset_top = 0.0
	tag.offset_bottom = 0.0
	tag.grow_horizontal = Control.GROW_DIRECTION_BEGIN if team == 0 else Control.GROW_DIRECTION_END
	tag.grow_vertical = Control.GROW_DIRECTION_END
	return tag


## A slot nobody has taken yet: a soft frosted card (a faint fill, a team-coloured edge and a tinted
## number block) with a "+" and what to do: tap to join that team, or just waiting. Tap it to join.
static func open_slot(team: int, row: int, can_join: bool, room: Dictionary, on_team: Callable) -> Control:
	var card: Button = Button.new()
	card.name = "OpenSlot%d" % team
	card.accessibility_name = "Open slot in the %s team" % ("blue" if team == 0 else "red")
	card.custom_minimum_size = Vector2(0.0, BAR_HEIGHT)
	card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	card.focus_mode = Control.FOCUS_ALL
	card.disabled = not can_join
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = Color(1.0, 1.0, 1.0, 0.07)
	box.border_color = Color(TEAM_BLOCKS[team].lightened(0.3), 0.6)
	box.set_border_width_all(2)
	box.set_corner_radius_all(14)
	box.anti_aliasing = true
	for state: String in ["normal", "disabled"]:
		card.add_theme_stylebox_override(state, box)
	var lit: StyleBoxFlat = box.duplicate() as StyleBoxFlat
	lit.bg_color = Color(TEAM_BLOCKS[team].lightened(0.3), 0.18)
	for state: String in ["hover", "pressed", "focus"]:
		card.add_theme_stylebox_override(state, lit)
	card.pressed.connect(on_team.bind(team))
	var inner: HBoxContainer = HBoxContainer.new()
	inner.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	inner.add_theme_constant_override("separation", 0)
	inner.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(inner)
	var hint: Label = Label.new()
	hint.name = "Hint"
	hint.text = "Join team" if can_join else "Open slot"
	hint.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	hint.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	hint.add_theme_font_size_override("font_size", 24)
	hint.add_theme_color_override("font_color", KalyeahTheme.TEXT if can_join else KalyeahTheme.TEXT_MUTED)
	hint.add_theme_color_override("font_outline_color", Color.TRANSPARENT)
	hint.add_theme_constant_override("outline_size", 0)
	hint.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var number: Control = _number_block(slot_number(team, row, room.get("team_size", 1) as int), team, true)
	if team == 0:
		inner.add_child(number)
		inner.add_child(hint)
	else:
		inner.add_child(hint)
		inner.add_child(number)
	return card


