class_name CardsHubCard
extends Button
## One wide card of the Cards hub: a panel in the modals' own slate frame (`KalyeahTheme.modal_frame`, so the two never drift apart) with
## its title and a detail line at the bottom left, and a `CardFan` at the right that is tucked into the panel like cards in a pocket:
## the panel's rounded shape cuts the fan at the bottom and the right (`fan_mask.gdshader`), while the fan's upper part sticks out
## over the top edge, uncut. The whole thing is one big button, the part of the fan that sticks out included.

## The panel's height: wide and not tall.
const HEIGHT: float = 176.0
## One card of the fan, tall; its width follows its picture.
const FAN_CARD_HEIGHT: float = 240.0
## The fan protrudes above the panel and overhangs its right edge, where the pocket mask trims it.
const FAN_PROTRUSION: float = 64.0
const FAN_FROM_RIGHT: float = -24.0
## The text stops this far short of the front card's left edge.
const TEXT_CLEARANCE: float = 20.0
## The panel is the modals' frame, with no rim of its own (the text and fan are placed by hand); a press lightens it.
const PRESS_LIGHTEN: float = 0.12
## The banner is a bright blue panel (owner: grey was bland), with the modals' frame shape, shadow and a darker blue edge.
const PANEL_COLOR: Color = Color("2f6fc4")
const PANEL_EDGE: Color = Color("1a3f7a")
const PANEL_RADIUS: int = KalyeahTheme.MODAL_FRAME_RADIUS
## The title is a headline in the modals' title colours; the detail is the plain line under it, white on the slate.
const TITLE_SIZE: int = UiMetrics.HEADLINE
const DETAIL_SIZE: int = UiMetrics.DETAIL
const TITLE_COLOR: Color = KalyeahTheme.MODAL_TITLE
const TITLE_INK: Color = KalyeahTheme.MODAL_TITLE_OUTLINE
const DETAIL_COLOR: Color = KalyeahTheme.MODAL_TITLE

var id: StringName = &""
var title_label: Label
var detail_label: Label
var fan: CardFan
var words: VBoxContainer
var panel_box: StyleBoxFlat
## How far this card's fan sits lower than the default, so less of it sticks out above the panel.
var fan_drop: float = 0.0


## A card for the page `page_id` with its `title` and `detail` and the three `pictures` of its fan (front first).
static func create(page_id: StringName, title: String, detail: String, pictures: Array[Texture2D], drop: float = 0.0) -> CardsHubCard:
	var card: CardsHubCard = CardsHubCard.new()
	card.id = page_id
	card.fan_drop = drop
	card._build(title, detail, pictures)
	return card


func _build(title: String, detail: String, pictures: Array[Texture2D]) -> void:
	name = "%sCard" % String(id).capitalize()
	custom_minimum_size = Vector2(0.0, HEIGHT)
	size_flags_horizontal = Control.SIZE_EXPAND_FILL
	tooltip_text = title
	accessibility_name = "%s: %s" % [title, detail]
	panel_box = _blue_frame()
	var pressed_box: StyleBoxFlat = _blue_frame()
	pressed_box.bg_color = pressed_box.bg_color.lightened(PRESS_LIGHTEN)
	for state: String in ["normal", "hover"]:
		add_theme_stylebox_override(state, panel_box)
	for state: String in ["pressed", "hover_pressed"]:
		add_theme_stylebox_override(state, pressed_box)
	add_theme_stylebox_override("focus", UiSkin.focus_ring(PANEL_RADIUS))
	UiKit.feel_press(self)
	fan = CardFan.new()
	fan.name = "Fan"
	var reference: Texture2D = CharactersPage.card_picture(&"junjun")
	var width: float = FAN_CARD_HEIGHT * float(reference.get_width()) / float(reference.get_height())
	var height: float = width * float(pictures[0].get_height()) / float(pictures[0].get_width()) if not pictures.is_empty() else FAN_CARD_HEIGHT
	fan.build(pictures, height)
	add_child(fan)
	words = VBoxContainer.new()
	words.name = "Words"
	words.alignment = BoxContainer.ALIGNMENT_END
	words.add_theme_constant_override("separation", 4)
	words.mouse_filter = Control.MOUSE_FILTER_IGNORE
	words.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	words.offset_left = UiMetrics.PADDING
	words.offset_bottom = -UiMetrics.PADDING
	words.offset_top = UiMetrics.PADDING
	# the text may run in under the fan, up to the front card's left edge at the height of its last row
	var last_row: float = HEIGHT - UiMetrics.PADDING - fan_top()
	words.offset_right = -(fan.size.x - fan.left_edge_at(last_row) + FAN_FROM_RIGHT + TEXT_CLEARANCE)
	add_child(words)
	title_label = Label.new()
	title_label.name = "Title"
	title_label.text = title
	title_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	UiKit.letter(title_label, TITLE_SIZE, TITLE_COLOR, TITLE_INK)
	words.add_child(title_label)
	detail_label = Label.new()
	detail_label.name = "Detail"
	detail_label.text = detail
	detail_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	detail_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	detail_label.add_theme_font_size_override("font_size", DETAIL_SIZE)
	detail_label.add_theme_color_override("font_color", DETAIL_COLOR)
	words.add_child(detail_label)
	# the mask follows the card: when the card or anything over it moves, resizes or is squeezed by a press
	set_notify_transform(true)
	resized.connect(_place_fan)
	_place_fan()


func _blue_frame() -> StyleBoxFlat:
	var box: StyleBoxFlat = KalyeahTheme.modal_frame(0.0)
	box.bg_color = PANEL_COLOR
	box.border_color = PANEL_EDGE
	return box


## The fan's top, in the card's own coordinates: above the panel by `FAN_PROTRUSION`.
func fan_top() -> float:
	return -protrusion()


func _place_fan() -> void:
	fan.position = Vector2(size.x - FAN_FROM_RIGHT - fan.size.x, fan_top())
	_update_mask()


func _notification(what: int) -> void:
	if (what == NOTIFICATION_TRANSFORM_CHANGED or what == NOTIFICATION_ENTER_TREE) and fan != null:
		_update_mask()


## Gives the fan's shader this card's place and shape (the panel's own corner radius and border width).
func _update_mask() -> void:
	if not is_inside_tree():
		return
	fan.set_mask(get_global_transform(), size, float(PANEL_RADIUS), float(panel_box.get_border_width(SIDE_LEFT)))


## How far the fan sticks out above the panel's top edge (a positive number), so a page can leave room for it.
func protrusion() -> float:
	return FAN_PROTRUSION - fan_drop


## Whether a tap at `point` (in the card's own coordinates) is on this card: the panel, or the fan that sticks out of it.
func taps_at(point: Vector2) -> bool:
	return Rect2(Vector2.ZERO, size).has_point(point) or Rect2(fan.position, fan.size).has_point(point)


func _has_point(point: Vector2) -> bool:
	return taps_at(point)
