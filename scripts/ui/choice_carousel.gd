class_name ChoiceCarousel
extends Control
## A tinted full-screen layer with a row of 2:1 choice cards. Tapping a card picks it and closes
## the layer; tapping the tint closes it without a change. The cards are the same as the ones on
## the main screen: the small heading at the top left, the choice's name big at the bottom left.

signal picked(index: int)

const TINT: Color = Color(0.01, 0.025, 0.06, 0.72)
## Three cards fit side by side across a phone (720 px wide design) with a margin.
const CARD_SIZE: Vector2 = Vector2(200.0, 100.0)
const CARD_GAP: float = 14.0
const PAD: float = 12.0

var cards: Array[Button] = []


## A 2:1 card: `heading` small at the top left, `choice` big at the bottom left, no subtitle.
static func make_card(heading: String, choice: String, color: String, size: Vector2 = CARD_SIZE) -> Button:
	var card: Button = MenuDialog.make_button("", color, 26, size)
	var top: Label = Label.new()
	top.name = "Heading"
	top.text = heading
	top.add_theme_font_size_override("font_size", 19)
	top.add_theme_color_override("font_color", KalyeahTheme.semantic_text_color(color))
	top.modulate.a = 0.75
	top.set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
	top.offset_left = PAD
	top.offset_top = PAD - 2.0
	top.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(top)
	var name_label: Label = Label.new()
	name_label.name = "Choice"
	name_label.theme_type_variation = &"DisplayLabel"
	name_label.add_theme_font_size_override("font_size", 22)
	name_label.add_theme_color_override("font_color", KalyeahTheme.semantic_text_color(color))
	name_label.add_theme_color_override("font_outline_color", Color.TRANSPARENT)
	name_label.add_theme_constant_override("outline_size", 0)
	name_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	name_label.vertical_alignment = VERTICAL_ALIGNMENT_BOTTOM
	name_label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	name_label.offset_left = PAD
	name_label.offset_right = -PAD
	name_label.offset_bottom = -PAD + 2.0
	name_label.text = choice
	name_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(name_label)
	return card


## Changes what a card made by `make_card` shows.
static func set_card(card: Button, heading: String, choice: String, color: String) -> void:
	(card.get_node("Heading") as Label).text = heading
	(card.get_node("Choice") as Label).text = choice
	KalyeahTheme.style_button(card, color)


static func choice_of(card: Button) -> String:
	return (card.get_node("Choice") as Label).text


## `options` are {title, color}; the card at `current` is the highlighted one.
static func create(heading: String, options: Array[Dictionary], current: int) -> ChoiceCarousel:
	var carousel: ChoiceCarousel = ChoiceCarousel.new()
	carousel.name = "ChoiceCarousel"
	carousel._build(heading, options, current)
	return carousel


func _build(heading: String, options: Array[Dictionary], current: int) -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var tint: ColorRect = ColorRect.new()
	tint.name = "Tint"
	tint.color = TINT
	tint.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	tint.gui_input.connect(_on_tint_input)
	add_child(tint)
	var center: CenterContainer = CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	center.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(center)
	var stack: VBoxContainer = VBoxContainer.new()
	stack.add_theme_constant_override("separation", 18)
	stack.mouse_filter = Control.MOUSE_FILTER_IGNORE
	center.add_child(stack)
	var title: Label = Label.new()
	title.text = "CHOOSE %s" % heading.to_upper()
	title.theme_type_variation = &"DisplayLabel"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.add_theme_font_size_override("font_size", 40)
	title.add_theme_color_override("font_color", KalyeahTheme.CREAM)
	title.mouse_filter = Control.MOUSE_FILTER_IGNORE
	stack.add_child(title)
	var scroll: ScrollContainer = ScrollContainer.new()
	scroll.name = "Scroll"
	scroll.vertical_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_SHOW_NEVER
	scroll.custom_minimum_size = Vector2(3.0 * CARD_SIZE.x + 2.0 * CARD_GAP, CARD_SIZE.y + 24.0)
	stack.add_child(scroll)
	var row: HBoxContainer = HBoxContainer.new()
	row.add_theme_constant_override("separation", int(CARD_GAP))
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(row)
	for i: int in options.size():
		var option: Dictionary = options[i]
		var card: Button = make_card(heading, String(option["title"]), String(option["color"]) if i == current else "grey")
		card.name = "Choice%d" % i
		card.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
		card.pressed.connect(_pick.bind(i))
		row.add_child(card)
		cards.append(card)
	modulate.a = 0.0
	create_tween().tween_property(self, "modulate:a", 1.0, 0.18)


func _pick(index: int) -> void:
	picked.emit(index)
	queue_free()


func _on_tint_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and (event as InputEventMouseButton).pressed:
		queue_free()
