class_name KalyeahTheme
extends RefCounted
## Kalyeah's portrait UI system: Dagsen display faces, compact spacing and raised,
## toy-like controls inspired by the visual weight of polished mobile strategy games.

const DISPLAY_FONT: Font = preload("res://assets/fonts/dagsen/Dagsen_Inked.otf")
const ACTION_FONT: Font = preload("res://assets/fonts/dagsen/Dagsen_Black.otf")
const FALLBACK_FONT: Font = preload("res://assets/fonts/nunito_sans/NunitoSans-Variable.ttf")
const POPPINS_FONT: Font = preload("res://assets/fonts/poppins/Poppins-Bold.ttf")
static var BODY_FONT: Font = _body_font()

const INK: Color = Color("10192d")
const INK_DEEP: Color = Color("080f1d")
const SURFACE: Color = Color("172743")
const SURFACE_RAISED: Color = Color("203657")
const CREAM: Color = Color("fff3d6")
const TEXT: Color = Color("f8fbff")
const TEXT_MUTED: Color = Color("aebbd0")
const AMBER: Color = Color("f6ae2d")
const AMBER_HOVER: Color = Color("ffc653")
const BLUE: Color = Color("3478d8")
const GREEN: Color = Color("26966a")
const RED: Color = Color("c94e56")
const SLATE: Color = Color("44546d")
const FOCUS: Color = Color("ffe59b")
const FIELD: Color = Color("0d1729")

const CORNER: int = 16
const BUTTON_SHADOW: int = 6
const FONT_SIZE: int = 26
const PANEL_PADDING: float = 30.0


static func _body_font() -> Font:
	# Poppins Bold for all regular text (owner), the same on every phone and in the browser
	var font: FontVariation = FontVariation.new()
	font.base_font = POPPINS_FONT
	font.fallbacks = [FALLBACK_FONT]
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
	theme.set_type_variation("DisplayLabel", "Label")
	theme.set_font("font", "DisplayLabel", DISPLAY_FONT)
	theme.set_color("font_color", "DisplayLabel", CREAM)
	theme.set_color("font_outline_color", "DisplayLabel", INK_DEEP)
	theme.set_constant("outline_size", "DisplayLabel", 3)
	theme.set_type_variation("SectionLabel", "Label")
	theme.set_font("font", "SectionLabel", DISPLAY_FONT)
	theme.set_color("font_color", "SectionLabel", AMBER)
	theme.set_constant("outline_size", "SectionLabel", 0)
	theme.set_type_variation("MutedLabel", "Label")
	theme.set_color("font_color", "MutedLabel", TEXT_MUTED)
	theme.set_constant("outline_size", "MutedLabel", 0)
	theme.set_type_variation("HudPill", "Label")
	theme.set_font("font", "HudPill", BODY_FONT)
	theme.set_stylebox("normal", "HudPill", painted_pill())
	theme.set_constant("outline_size", "HudPill", 0)

	theme.set_font("font", "Button", ACTION_FONT)
	theme.set_font_size("font_size", "Button", 30)
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
	theme.set_font_size("font_size", "LineEdit", 27)
	theme.set_stylebox("normal", "LineEdit", field_box(false))
	theme.set_stylebox("focus", "LineEdit", field_box(true))
	theme.set_stylebox("read_only", "LineEdit", field_box(false, true))
	theme.set_color("font_color", "LineEdit", TEXT)
	theme.set_color("font_selected_color", "LineEdit", INK_DEEP)
	theme.set_color("font_uneditable_color", "LineEdit", TEXT_MUTED)
	theme.set_color("font_placeholder_color", "LineEdit", Color(TEXT_MUTED, 0.62))
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
	return INK_DEEP if name == "orange" or name == "gold" else TEXT


## A clean raised action surface. The thicker lower shadow gives touch feedback
## without the plastic gloss of the old image buttons.
static func painted_button(color: String, pressed: bool = false, tint: Color = Color.WHITE) -> StyleBoxFlat:
	var face: Color = semantic_color(color) * tint
	if pressed:
		face = face.darkened(0.08)
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = face
	box.set_corner_radius_all(CORNER)
	box.set_border_width_all(2)
	box.border_color = face.lightened(0.2) if color != "grey" else SLATE.lightened(0.18)
	box.shadow_color = Color(INK_DEEP, 0.85)
	box.shadow_size = 1
	box.shadow_offset = Vector2(0.0, 2.0 if pressed else BUTTON_SHADOW)
	box.content_margin_left = 20.0
	box.content_margin_right = 20.0
	box.content_margin_top = 15.0 + (3.0 if pressed else 0.0)
	box.content_margin_bottom = 17.0 - (3.0 if pressed else 0.0)
	box.anti_aliasing = true
	return box


## Kept for toggle controls and small custom surfaces.
static func button_box(color: Color, pressed: bool = false) -> StyleBoxFlat:
	var box: StyleBoxFlat = painted_button("grey", pressed)
	box.bg_color = color.darkened(0.07) if pressed else color
	box.border_color = color.lightened(0.18)
	return box


static func set_button_styles(theme: Theme, type: String, color: String) -> void:
	theme.set_stylebox("normal", type, painted_button(color))
	theme.set_stylebox("hover", type, painted_button(color, false, Color(1.06, 1.06, 1.06)))
	theme.set_stylebox("pressed", type, painted_button(color, true))
	theme.set_stylebox("hover_pressed", type, painted_button(color, true))


static func style_button(button: Button, color: String) -> void:
	button.add_theme_stylebox_override("normal", painted_button(color))
	button.add_theme_stylebox_override("hover", painted_button(color, false, Color(1.06, 1.06, 1.06)))
	button.add_theme_stylebox_override("pressed", painted_button(color, true))
	button.add_theme_stylebox_override("hover_pressed", painted_button(color, true))
	button.add_theme_stylebox_override("disabled", painted_button("grey"))
	button.add_theme_stylebox_override("focus", focus_ring(CORNER + 2))
	var text_color: Color = semantic_text_color(color)
	for state: String in ["font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color"]:
		button.add_theme_color_override(state, text_color)
	button.add_theme_color_override("font_disabled_color", Color(TEXT_MUTED, 0.62))
	button.add_theme_color_override("font_outline_color", Color.TRANSPARENT)
	button.add_theme_constant_override("outline_size", 0)


static func surface_box(corner: int = 20, border: int = 2, padding: float = PANEL_PADDING) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = Color(SURFACE, 0.97)
	box.set_corner_radius_all(corner)
	box.set_border_width_all(border)
	box.border_color = Color(CREAM, 0.24)
	box.shadow_color = Color(INK_DEEP, 0.72)
	box.shadow_size = 10
	box.shadow_offset = Vector2(0.0, 8.0)
	box.set_content_margin_all(padding)
	box.anti_aliasing = true
	return box


static func painted_panel(padding: float = PANEL_PADDING) -> StyleBoxFlat:
	return surface_box(24, 2, padding)


static func painted_pill() -> StyleBoxFlat:
	var box: StyleBoxFlat = surface_box(22, 2, 0.0)
	box.bg_color = Color(INK, 0.9)
	box.border_color = Color(CREAM, 0.24)
	box.shadow_size = 3
	box.shadow_offset = Vector2(0.0, 3.0)
	box.content_margin_left = 18.0
	box.content_margin_right = 18.0
	box.content_margin_top = 9.0
	box.content_margin_bottom = 10.0
	return box


static func map_title_plate() -> StyleBoxFlat:
	var box: StyleBoxFlat = surface_box(14, 3, 0.0)
	box.bg_color = Color("213b61")
	box.border_color = AMBER
	box.shadow_size = 4
	box.shadow_offset = Vector2(0.0, 5.0)
	box.content_margin_left = 28.0
	box.content_margin_right = 28.0
	box.content_margin_top = 8.0
	box.content_margin_bottom = 10.0
	return box


static func field_box(focused: bool, read_only: bool = false) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = Color(SURFACE_RAISED if read_only else FIELD, 0.98)
	box.set_corner_radius_all(14)
	box.set_border_width_all(3 if focused else 2)
	box.border_color = AMBER if focused else Color(TEXT_MUTED, 0.38)
	box.content_margin_left = 18.0
	box.content_margin_right = 18.0
	box.content_margin_top = 15.0
	box.content_margin_bottom = 15.0
	box.shadow_color = Color(INK_DEEP, 0.48)
	box.shadow_size = 3
	box.shadow_offset = Vector2(0.0, 3.0)
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
