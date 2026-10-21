class_name KalyeahTheme
extends RefCounted
## Kalyeah's portrait UI system: calm grouped surfaces and direct controls, with
## the display face reserved for game identity and high-salience match moments.

## Bungee is the one typeface (owner): headings, body copy, buttons, fields and HUD numbers all use it. Its capitals are tight and heavy, so the
## roles differ by size, colour and tracking (extra space between letters and words) instead of by face:
## display (titles, big numbers, the score) is untracked; body copy and buttons are opened a little; captions and section labels a little more.
const DISPLAY_FONT: Font = preload("res://assets/fonts/bungee/Bungee-Regular.ttf")
const BODY_TRACKING: int = 0
const BUTTON_TRACKING: int = 0
const CAPTION_TRACKING: int = 2
## Extra space at every word gap: all-capital words run together without it.
const WORD_GAP: int = 4
## Extra space between the lines of wrapped text.
const LINE_SPACING: int = 0
## Paragraphs (descriptions, tips, hints) open their lines up a little more; tight tiles and buttons keep the default.
const READING_LINE_SPACING: int = 6
static var BODY_FONT: Font = _tracked(BODY_TRACKING, WORD_GAP)
static var CAPTION_FONT: Font = _tracked(CAPTION_TRACKING, WORD_GAP)
static var BUTTON_FONT: Font = _tracked(BUTTON_TRACKING, WORD_GAP - 2)
## Numbers and the match's drawn text: untracked, so digits and scores stay compact.
static var ACTION_FONT: Font = DISPLAY_FONT

const INK: Color = Color("10192d")
const INK_DEEP: Color = Color("080f1d")
const SURFACE: Color = Color("172743")
const SURFACE_RAISED: Color = Color("203657")
const CREAM: Color = Color("fff3d6")
const TEXT: Color = Color("f8fbff")
const TEXT_MUTED: Color = Color("aebbd0")
const AMBER: Color = Color("ff9f0a")
const AMBER_HOVER: Color = Color("ffb340")
const BLUE: Color = Color("0a84ff")
const GREEN: Color = Color("30a46c")
const RED: Color = Color("ff5e57")
const SLATE: Color = Color("3a4b63")
const FOCUS: Color = Color("ffe59b")
const SUCCESS: Color = Color("79daa5")
const ERROR: Color = Color("ffb4ab")
const TEAM_BLUE: Color = Color("435bc2")
const TEAM_RED: Color = Color("b83246")
const FIELD: Color = Color("0d1729")

const CORNER: int = 18
const BUTTON_SHADOW: int = 0
const FONT_SIZE: int = UiMetrics.BODY
const PANEL_PADDING: float = UiMetrics.PADDING


static func _tracked(glyph: int, word: int) -> Font:
	var font: FontVariation = FontVariation.new()
	font.base_font = DISPLAY_FONT
	font.spacing_glyph = glyph
	font.spacing_space = word
	return font


static func apply() -> void:
	var default_theme: Theme = ThemeDB.get_default_theme()
	default_theme.merge_with(build())
	default_theme.default_font = BODY_FONT
	default_theme.default_font_size = FONT_SIZE


static func build() -> Theme:
	var theme: Theme = Theme.new()
	theme.default_font = BODY_FONT
	theme.default_font_size = FONT_SIZE
	theme.set_font("font", "Label", BODY_FONT)
	theme.set_color("font_color", "Label", TEXT)
	theme.set_color("font_outline_color", "Label", INK_DEEP)
	theme.set_constant("outline_size", "Label", 0)
	theme.set_constant("line_spacing", "Label", LINE_SPACING)
	theme.set_type_variation("DisplayLabel", "Label")
	theme.set_font("font", "DisplayLabel", DISPLAY_FONT)
	theme.set_color("font_color", "DisplayLabel", CREAM)
	theme.set_color("font_outline_color", "DisplayLabel", INK_DEEP)
	theme.set_constant("outline_size", "DisplayLabel", 3)
	theme.set_type_variation("SectionLabel", "Label")
	theme.set_font("font", "SectionLabel", CAPTION_FONT)
	theme.set_color("font_color", "SectionLabel", AMBER)
	theme.set_constant("outline_size", "SectionLabel", 0)
	theme.set_type_variation("MutedLabel", "Label")
	theme.set_font("font", "MutedLabel", CAPTION_FONT)
	theme.set_color("font_color", "MutedLabel", TEXT_MUTED)
	theme.set_constant("outline_size", "MutedLabel", 0)
	theme.set_type_variation("HudPill", "Label")
	theme.set_font("font", "HudPill", BODY_FONT)
	theme.set_stylebox("normal", "HudPill", UiSkin.pill())
	theme.set_constant("outline_size", "HudPill", 0)

	theme.set_font("font", "Button", BUTTON_FONT)
	theme.set_font_size("font_size", "Button", UiMetrics.ACTION)
	set_button_styles(theme, "Button", "orange")
	theme.set_stylebox("disabled", "Button", painted_button("grey"))
	theme.set_stylebox("focus", "Button", focus_ring(CORNER + 2))
	for state: String in ["font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color"]:
		theme.set_color(state, "Button", INK_DEEP)
	theme.set_color("font_disabled_color", "Button", Color(TEXT_MUTED, 0.62))
	theme.set_color("font_outline_color", "Button", Color.TRANSPARENT)
	theme.set_constant("outline_size", "Button", 0)
	theme.set_constant("h_separation", "Button", 10)

	theme.set_font("font", "LineEdit", BODY_FONT)
	theme.set_font_size("font_size", "LineEdit", UiMetrics.BODY)
	theme.set_stylebox("normal", "LineEdit", field_box(false))
	theme.set_stylebox("focus", "LineEdit", field_box(true))
	theme.set_stylebox("read_only", "LineEdit", field_box(false, true))
	theme.set_color("font_color", "LineEdit", TEXT)
	theme.set_color("font_selected_color", "LineEdit", INK_DEEP)
	theme.set_color("font_uneditable_color", "LineEdit", TEXT_MUTED)
	theme.set_color("font_placeholder_color", "LineEdit", TEXT_MUTED)
	theme.set_color("caret_color", "LineEdit", AMBER)
	theme.set_color("selection_color", "LineEdit", AMBER)

	for type: String in ["Panel", "PanelContainer"]:
		theme.set_stylebox("panel", type, surface_box(18, 2, 20.0))
	return theme


static func semantic_color(name: String) -> Color:
	match name:
		"orange", "gold":
			return AMBER
		"blue":
			return BLUE
		"green":
			return GREEN
		"red":
			return RED
		_:
			return SLATE


static func semantic_text_color(name: String) -> Color:
	return TEXT if name == "grey" else INK_DEEP


## The UiSkin variant that draws a semantic colour name.
static func skin_variant(color: String) -> String:
	match color:
		"orange", "gold":
			return "gold"
		"blue", "green", "red":
			return color
		_:
			return "slate"


## The flat version of a button colour, kept for the few places that need an editable StyleBoxFlat
## (tests and small custom surfaces). Buttons themselves use UiSkin.
static func painted_button(color: String, pressed: bool = false, tint: Color = Color.WHITE) -> StyleBoxFlat:
	var face: Color = semantic_color(color) * tint
	if pressed:
		face = face.darkened(0.08)
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = face
	box.set_corner_radius_all(CORNER)
	box.set_border_width_all(1)
	box.border_color = Color(face.lightened(0.22), 0.72) if color != "grey" else Color(TEXT_MUTED, 0.24)
	box.shadow_size = 0
	box.content_margin_left = 20.0
	box.content_margin_right = 20.0
	box.content_margin_top = 21.0
	box.content_margin_bottom = 21.0
	box.anti_aliasing = true
	return box


## Kept for toggle controls and small custom surfaces.
static func button_box(color: Color, pressed: bool = false) -> StyleBoxFlat:
	var box: StyleBoxFlat = painted_button("grey", pressed)
	box.bg_color = color.darkened(0.07) if pressed else color
	box.border_color = color.lightened(0.18)
	return box


static func set_button_styles(theme: Theme, type: String, color: String) -> void:
	var variant: String = skin_variant(color)
	theme.set_stylebox("normal", type, UiSkin.button(variant))
	theme.set_stylebox("hover", type, UiSkin.button(variant, "hover"))
	theme.set_stylebox("pressed", type, UiSkin.button(variant, "pressed"))
	theme.set_stylebox("hover_pressed", type, UiSkin.button(variant, "pressed"))


## Dresses a button in a game-button variant: a gradient face with a lip (UiSkin), a heavy label.
static func style_button(button: Button, color: String) -> void:
	var variant: String = skin_variant(color)
	button.add_theme_stylebox_override("normal", UiSkin.button(variant))
	button.add_theme_stylebox_override("hover", UiSkin.button(variant, "hover"))
	button.add_theme_stylebox_override("pressed", UiSkin.button(variant, "pressed"))
	button.add_theme_stylebox_override("hover_pressed", UiSkin.button(variant, "pressed"))
	button.add_theme_stylebox_override("disabled", UiSkin.button("disabled"))
	button.add_theme_stylebox_override("focus", UiSkin.focus_ring())
	button.add_theme_font_override("font", BUTTON_FONT)
	UiSkin.style_label(button, variant)


static func surface_box(corner: int = 20, border: int = 1, padding: float = PANEL_PADDING) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = Color(SURFACE, 0.97)
	box.set_corner_radius_all(corner)
	box.set_border_width_all(border)
	box.border_color = Color(TEXT_MUTED, 0.2)
	box.shadow_size = 0
	box.set_content_margin_all(padding)
	box.anti_aliasing = true
	return box


## The standard panel: a lit glass plate with a soft shadow (UiSkin).
static func painted_panel(padding: float = PANEL_PADDING) -> StyleBox:
	return UiSkin.plate("glass", padding)


static func painted_pill() -> StyleBoxFlat:
	var box: StyleBoxFlat = surface_box(22, 1, 0.0)
	box.bg_color = Color(INK, 0.88)
	box.border_color = Color(TEXT_MUTED, 0.2)
	box.content_margin_left = 18.0
	box.content_margin_right = 18.0
	box.content_margin_top = 9.0
	box.content_margin_bottom = 10.0
	return box


static func map_title_plate() -> StyleBoxFlat:
	var box: StyleBoxFlat = surface_box(14, 2, 0.0)
	box.bg_color = Color("213b61")
	box.border_color = AMBER
	box.shadow_size = 0
	box.content_margin_left = 28.0
	box.content_margin_right = 28.0
	box.content_margin_top = 8.0
	box.content_margin_bottom = 10.0
	return box


static func field_box(focused: bool, read_only: bool = false) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = Color(SURFACE_RAISED if read_only else FIELD, 0.98)
	box.set_corner_radius_all(14)
	box.set_border_width_all(3 if focused else 1)
	box.border_color = BLUE if focused else Color(TEXT_MUTED, 0.3)
	box.content_margin_left = 18.0
	box.content_margin_right = 18.0
	box.content_margin_top = 15.0
	box.content_margin_bottom = 15.0
	box.shadow_size = 0
	box.anti_aliasing = true
	return box


static func focus_ring(radius: int) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = Color.TRANSPARENT
	box.set_corner_radius_all(radius)
	box.set_border_width_all(3)
	box.border_color = FOCUS
	box.set_expand_margin_all(3.0)
	box.anti_aliasing = true
	return box
