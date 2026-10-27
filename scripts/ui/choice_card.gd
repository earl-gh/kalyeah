class_name ChoiceCard
extends RefCounted
## A 1.75:2.5 card: a small heading at the top left (when there is one), the choice's name big at the
## bottom left, no subtitle. If the owner's card art exists (assets/ui/cards/<id>.webp, pictures are cropped to fit) it
## fills the card.

const CARD_SIZE: Vector2 = Vector2(385.0, 550.0)
const PAD: float = 14.0
const CROP_SHADER: Shader = preload("res://shaders/minimap_crop.gdshader")
const ART_DIR: String = "res://assets/ui/cards/"
## The owner's dioramas: transparent PNGs (lossless, so their edges stay clean) shown bare (no card paper) on a card that is only a shell.
const BARE_ART_DIR: String = "res://assets/ui/dioramas/"


## A card: `heading` small at the top left, `choice` big at the bottom left, no subtitle.
static func make_card(heading: String, choice: String, color: String, size: Vector2 = CARD_SIZE, art_id: String = "", bare: bool = false) -> Button:
	var card: Button = MenuDialog.make_button("", color, UiMetrics.ACTION, size)
	var art_path: String = "%s%s.%s" % [BARE_ART_DIR if bare else ART_DIR, art_id, "png" if bare else "webp"]
	var has_art: bool = not art_id.is_empty() and ResourceLoader.exists(art_path)
	if has_art:
		var art: TextureRect = TextureRect.new()
		art.name = "Art"
		art.texture = load(art_path) as Texture2D
		art.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		art.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
		art.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		art.mouse_filter = Control.MOUSE_FILTER_IGNORE
		# the picture is not drawn here: it holds the owner's file, and a `CardFace` draws it as a thin, floating card
		art.visible = false
		card.add_child(art)
		var face: CardFace = CardFace.new()
		face.bare = bare
		face.texture = art.texture
		card.add_child(face)
		# the button's own face would show round the floating card
		for state: String in ["normal", "hover", "pressed", "hover_pressed", "disabled", "focus"]:
			card.add_theme_stylebox_override(state, StyleBoxEmpty.new())
	var top: Label = Label.new()
	top.name = "Heading"
	top.text = heading
	top.add_theme_font_size_override("font_size", UiMetrics.MIN_TEXT)
	top.add_theme_color_override("font_color", KalyeahTheme.semantic_text_color(color))
	top.modulate.a = 0.75
	top.set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
	top.offset_left = PAD
	top.offset_top = PAD - 2.0
	top.mouse_filter = Control.MOUSE_FILTER_IGNORE
	top.visible = not heading.is_empty() and not has_art
	card.add_child(top)
	var name_label: Label = Label.new()
	name_label.name = "Choice"
	name_label.theme_type_variation = &"DisplayLabel"
	name_label.add_theme_font_size_override("font_size", UiMetrics.TITLE)
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
	# a card with a picture has no text on it (the name stays on the node for the code to read)
	name_label.visible = not has_art
	card.add_child(name_label)
	return card


## Changes what a card made by `make_card` shows.
static func set_card(card: Button, heading: String, choice: String, color: String) -> void:
	(card.get_node("Heading") as Label).text = heading
	(card.get_node("Choice") as Label).text = choice
	KalyeahTheme.style_button(card, color)


static func choice_of(card: Button) -> String:
	return (card.get_node("Choice") as Label).text
