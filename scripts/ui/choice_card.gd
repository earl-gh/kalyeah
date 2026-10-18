class_name ChoiceCard
extends RefCounted
## A 3:4 card: a small heading at the top left (when there is one), the choice's name big at the
## bottom left, no subtitle. If the owner's card art exists (assets/ui/cards/<id>.png, 3:4) it
## fills the card.

const CARD_SIZE: Vector2 = Vector2(312.0, 416.0)
const PAD: float = 14.0
const ART_DIR: String = "res://assets/ui/cards/"


## A card: `heading` small at the top left, `choice` big at the bottom left, no subtitle.
static func make_card(heading: String, choice: String, color: String, size: Vector2 = CARD_SIZE, art_id: String = "") -> Button:
	var card: Button = MenuDialog.make_button("", color, 26, size)
	var art_path: String = "%s%s.png" % [ART_DIR, art_id]
	if not art_id.is_empty() and ResourceLoader.exists(art_path):
		var art: TextureRect = TextureRect.new()
		art.name = "Art"
		art.texture = load(art_path) as Texture2D
		art.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		art.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
		art.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		art.mouse_filter = Control.MOUSE_FILTER_IGNORE
		card.add_child(art)
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
	top.visible = not heading.is_empty()
	card.add_child(top)
	var name_label: Label = Label.new()
	name_label.name = "Choice"
	name_label.theme_type_variation = &"DisplayLabel"
	name_label.add_theme_font_size_override("font_size", 34)
	name_label.add_theme_color_override("font_color", KalyeahTheme.semantic_text_color(color))
	name_label.add_theme_color_override("font_outline_color", Color.TRANSPARENT)
	name_label.add_theme_constant_override("outline_size", 0)
	name_label.autowrap_mode = TextServer.AUTOWRAP_OFF
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
