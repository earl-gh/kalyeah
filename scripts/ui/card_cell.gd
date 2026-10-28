class_name CardCell
extends Button
## One card of a Kids or Skills grid: the owner's card picture bare (no plate, no crop), a thin kind-colour tag under it when it has one,
## and the name under that. Tapping it asks for its drawer. While its drawer is open it wears the gold silhouette edge the hand gives a
## lifted card (`silhouette.gdshader`). A drag that starts on it scrolls the grid it sits in.

const PRESSED_DIM: Color = Color(0.82, 0.82, 0.82)
## The thin kind tag between the picture and the name.
const TAG_SIZE: Vector2 = Vector2(56.0, 8.0)
const TAG_GAP: float = 6.0
const NAME_OUTLINE: int = 5

var entry: Dictionary = {}
var picture: TextureRect
var edge: Control
var tag: Panel
var name_label: Label
var selected: bool = false

var _aspect: float = 0.7
var _name_lines: int = 1
var _name_size: int = UiMetrics.CAPTION
var _column: VBoxContainer
var _holder: Control


## A cell for `card` ({id, name, picture, tag (a Color or null), access, name_lines, name_size}).
static func create(card: Dictionary) -> CardCell:
	var cell: CardCell = CardCell.new()
	cell.entry = card
	cell._build()
	return cell


func _build() -> void:
	name = String(entry["id"]).capitalize()
	accessibility_name = String(entry["access"])
	tooltip_text = String(entry["name"])
	disabled = bool(entry.get("locked", false))
	_name_lines = int(entry.get("name_lines", 1))
	_name_size = int(entry.get("name_size", UiMetrics.CAPTION))
	size_flags_horizontal = Control.SIZE_EXPAND_FILL
	# a drag that starts on it scrolls the list it sits in: the scrollbar is not the only grip
	mouse_filter = Control.MOUSE_FILTER_PASS
	focus_mode = Control.FOCUS_ALL
	for state: String in ["normal", "hover", "pressed", "hover_pressed", "disabled"]:
		add_theme_stylebox_override(state, StyleBoxEmpty.new())
	add_theme_stylebox_override("focus", UiSkin.focus_ring(UiSkin.RADIUS_L))
	UiKit.feel_press(self)
	_column = VBoxContainer.new()
	_column.name = "Column"
	_column.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_column.add_theme_constant_override("separation", int(TAG_GAP))
	_column.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_column)
	_holder = Control.new()
	_holder.name = "Holder"
	_holder.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_column.add_child(_holder)
	var art: Texture2D = entry["picture"] as Texture2D
	if art != null and art.get_height() > 0:
		_aspect = float(art.get_width()) / float(art.get_height())
	# the gold edge is behind the picture
	edge = Control.new()
	edge.name = "Edge"
	edge.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	edge.mouse_filter = Control.MOUSE_FILTER_IGNORE
	edge.material = HandCard.silhouette_material()
	edge.visible = false
	edge.draw.connect(_draw_edge)
	_holder.add_child(edge)
	picture = TextureRect.new()
	picture.name = "Picture"
	picture.texture = art
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	picture.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_holder.add_child(picture)
	var tint: Variant = entry.get("tag")
	tag = Panel.new()
	tag.name = "Tag"
	tag.custom_minimum_size = TAG_SIZE
	tag.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	tag.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var dot: StyleBoxFlat = StyleBoxFlat.new()
	dot.bg_color = tint as Color if tint is Color else Color.TRANSPARENT
	dot.set_corner_radius_all(UiSkin.round_radius(TAG_SIZE.y))
	dot.anti_aliasing = true
	tag.add_theme_stylebox_override("panel", dot)
	tag.visible = tint is Color
	_column.add_child(tag)
	name_label = Label.new()
	name_label.name = "Name"
	name_label.theme_type_variation = &"DisplayLabel"
	name_label.text = String(entry["name"])
	name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	name_label.vertical_alignment = VERTICAL_ALIGNMENT_TOP
	name_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	name_label.add_theme_font_size_override("font_size", _name_size)
	name_label.add_theme_constant_override("outline_size", NAME_OUTLINE)
	name_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_column.add_child(name_label)
	if bool(entry.get("compact", false)):
		name_label.visible = false
		tag.visible = false
	if disabled:
		focus_mode = Control.FOCUS_NONE
		picture.modulate = Color(0.12, 0.12, 0.15, 1.0)
		var unknown: Label = Label.new()
		unknown.name = "Unknown"
		unknown.text = "?"
		UiKit.letter(unknown, UiMetrics.DISPLAY)
		unknown.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		unknown.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		unknown.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		unknown.mouse_filter = Control.MOUSE_FILTER_IGNORE
		_holder.add_child(unknown)
	button_down.connect(func() -> void: picture.modulate = PRESSED_DIM)
	button_up.connect(func() -> void: picture.modulate = Color.WHITE)


## Sizes the cell for a column `width` wide: the picture keeps its own shape, then the tag and the name (room for `name_lines` rows).
func fit(width: float) -> void:
	var picture_height: float = width / _aspect
	_holder.custom_minimum_size = Vector2(width, picture_height)
	if bool(entry.get("compact", false)):
		custom_minimum_size = Vector2(0.0, picture_height)
		return
	var row: float = name_label.get_theme_font("font").get_height(_name_size)
	name_label.custom_minimum_size = Vector2(0.0, row * float(_name_lines))
	var tag_height: float = (TAG_SIZE.y + TAG_GAP) if tag.visible else 0.0
	custom_minimum_size = Vector2(0.0, picture_height + TAG_GAP + tag_height + row * float(_name_lines))


## Where the picture really is in this cell (it is as wide as the cell and as tall as its own shape).
func picture_rect() -> Rect2:
	return CardStyle.picture_rect(Rect2(Vector2.ZERO, _holder.size), picture.texture)


func set_selected(on: bool) -> void:
	selected = on
	edge.visible = on
	edge.queue_redraw()


func _draw_edge() -> void:
	if picture.texture == null:
		return
	var shown: Rect2 = CardStyle.picture_rect(Rect2(Vector2.ZERO, edge.size), picture.texture)
	for step: int in CardStyle.PICTURE_EDGE_STEPS:
		var at: Vector2 = Vector2.from_angle(TAU * float(step) / float(CardStyle.PICTURE_EDGE_STEPS)) * CardStyle.LIFT_EDGE
		edge.draw_texture_rect(picture.texture, Rect2(shown.position + at, shown.size), false, RoyaleStyle.GOLD)
