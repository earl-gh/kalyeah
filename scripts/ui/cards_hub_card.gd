class_name CardsHubCard
extends Button
## One wide card of the Cards hub: a rounded panel with its title and a detail line at the bottom left, and a `CardFan` on the
## right that sits mostly inside the panel and sticks out above its top edge. The whole thing is one big button, the part of
## the fan that sticks out included.

## The panel's height: wide and not tall.
const HEIGHT: float = 210.0
## One card of the fan, tall; its width follows its picture.
const FAN_CARD_HEIGHT: float = 228.0
## The fan's bottom stays this far above the panel's bottom edge, and its right edge this far in from the panel's right edge.
const FAN_FROM_BOTTOM: float = 18.0
const FAN_FROM_RIGHT: float = 14.0
## The text stops this far short of the front card's lower left corner (the card's edge leans in to the left above it).
const TEXT_CLEARANCE: float = 48.0
## The title is a headline; the detail is the quiet line under it.
const TITLE_SIZE: int = UiMetrics.HEADLINE
const DETAIL_SIZE: int = UiMetrics.DETAIL
const DETAIL_COLOR: Color = Color("c3d0ee")

var id: StringName = &""
var title_label: Label
var detail_label: Label
var fan: CardFan


## A card for the page `page_id` with its `title` and `detail` and the three `pictures` of its fan (front first).
static func create(page_id: StringName, title: String, detail: String, pictures: Array[Texture2D]) -> CardsHubCard:
	var card: CardsHubCard = CardsHubCard.new()
	card.id = page_id
	card._build(title, detail, pictures)
	return card


func _build(title: String, detail: String, pictures: Array[Texture2D]) -> void:
	name = "%sCard" % String(id).capitalize()
	custom_minimum_size = Vector2(0.0, HEIGHT)
	size_flags_horizontal = Control.SIZE_EXPAND_FILL
	tooltip_text = title
	accessibility_name = "%s: %s" % [title, detail]
	for state: String in ["normal", "hover"]:
		add_theme_stylebox_override(state, UiSkin.plate("glass", 0.0, UiSkin.RADIUS_XL))
	for state: String in ["pressed", "hover_pressed"]:
		add_theme_stylebox_override(state, UiSkin.plate("raised", 0.0, UiSkin.RADIUS_XL))
	add_theme_stylebox_override("focus", UiSkin.focus_ring(UiSkin.RADIUS_XL))
	UiKit.feel_press(self)
	fan = CardFan.new()
	fan.name = "Fan"
	fan.build(pictures, FAN_CARD_HEIGHT)
	add_child(fan)
	var words: VBoxContainer = VBoxContainer.new()
	words.name = "Words"
	words.alignment = BoxContainer.ALIGNMENT_END
	words.add_theme_constant_override("separation", 4)
	words.mouse_filter = Control.MOUSE_FILTER_IGNORE
	words.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	words.offset_left = UiMetrics.PADDING
	words.offset_bottom = -UiMetrics.PADDING
	words.offset_top = UiMetrics.PADDING
	# the text may run in under the fan's empty lower left corner, up to the front card's edge
	words.offset_right = -(fan.size.x - fan.lower_left().x + FAN_FROM_RIGHT + TEXT_CLEARANCE)
	add_child(words)
	title_label = Label.new()
	title_label.name = "Title"
	title_label.text = title
	title_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	UiKit.letter(title_label, TITLE_SIZE)
	words.add_child(title_label)
	detail_label = Label.new()
	detail_label.name = "Detail"
	detail_label.text = detail
	detail_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	detail_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	detail_label.add_theme_font_size_override("font_size", DETAIL_SIZE)
	detail_label.add_theme_color_override("font_color", DETAIL_COLOR)
	words.add_child(detail_label)
	resized.connect(_place_fan)
	_place_fan()


## The fan sits at the right with its bottom inside the panel; whatever is taller than the panel sticks out above it.
func _place_fan() -> void:
	fan.position = Vector2(size.x - FAN_FROM_RIGHT - fan.size.x, maxf(size.y, HEIGHT) - FAN_FROM_BOTTOM - fan.size.y)


## How far the fan sticks out above the panel's top edge (a positive number), so a page can leave room for it.
func protrusion() -> float:
	return maxf(0.0, fan.size.y + FAN_FROM_BOTTOM - HEIGHT)


## Whether a tap at `point` (in the card's own coordinates) is on this card: the panel, or the fan that sticks out of it.
func taps_at(point: Vector2) -> bool:
	return Rect2(Vector2.ZERO, size).has_point(point) or Rect2(fan.position, fan.size).has_point(point)


func _has_point(point: Vector2) -> bool:
	return taps_at(point)
