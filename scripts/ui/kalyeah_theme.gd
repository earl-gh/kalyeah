class_name KalyeahTheme
extends RefCounted
## Kalyeah's 2D 90s Filipino Komiks UI system: bold hand-inked `#161219` charcoal panel borders
## with hard comic offset drop-shadows, warm sun-aged komiks newsprint (`#F6ECD8`), and vibrant
## retro Pinoy street inks (sari-sari mango gold, jeepney crimson, cobalt sky).

## Clash (the owner's font, `assets/fonts/clash/`) is the one typeface: headings, body copy, buttons, fields and HUD numbers all use it. It has real lowercase letters, so the
## roles differ by size, colour and tracking (extra space between letters and words) instead of by face:
## display (titles, big numbers, the score) is untracked; body copy and buttons are opened a little; captions and section labels a little more.
const DISPLAY_FONT: Font = preload("res://assets/fonts/clash/Clash-Regular.otf")
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

const INK: Color = Color("161219")
const INK_DEEP: Color = Color("120e16")
const KOMIKS_INK: Color = Color("161219")
const KOMIKS_PAPER: Color = Color("f6ecd8")
const SURFACE: Color = Color("1f263d")
const SURFACE_RAISED: Color = Color("293350")
const CREAM: Color = Color("f6ecd8")
const TEXT: Color = Color("fcf6ec")
const TEXT_MUTED: Color = Color("c6bba6")
const AMBER: Color = Color("ffab1a")
const AMBER_HOVER: Color = Color("ffc048")
const BLUE: Color = Color("2994ff")
const GREEN: Color = Color("2fb36d")
const RED: Color = Color("ff6256")
const SLATE: Color = Color("3b465c")
const FOCUS: Color = UiSkin.FOCUS_COLOR
const SUCCESS: Color = Color("79daa5")
const ERROR: Color = Color("ffb4ab")
const TEAM_BLUE: Color = Color("2b52b8")
const TEAM_RED: Color = Color("b52b38")
const FIELD: Color = Color("15192b")
## 90s Filipino Komiks modal sheet: dark charcoal-plum comic header frame (`#3B3042`) inked with `#161219`
## and sun-aged komiks newsprint cream (`#F6ECD8`) body with dark charcoal text.
const MODAL_FRAME: Color = Color("3b3042")
const MODAL_FRAME_EDGE: Color = Color("161219")
const MODAL_BODY: Color = Color("f6ecd8")
const MODAL_BODY_EDGE: Color = Color("161219")
const MODAL_TITLE: Color = Color.WHITE
const MODAL_TITLE_OUTLINE: Color = Color("161219")
const MODAL_TEXT: Color = Color("161219")
const MODAL_TEXT_MUTED: Color = Color("59483b")
## Modal corners (owner): rounder than the small skin radius, less than the big card rounding (steps of the UiSkin scale).
const MODAL_FRAME_RADIUS: int = UiSkin.RADIUS_L
const MODAL_BODY_RADIUS: int = UiSkin.RADIUS_M

## Flat surfaces share the skin's corner scale: CORNER is the button and card radius.
const CORNER: int = UiSkin.RADIUS_L
const BUTTON_SHADOW: int = 1
const KOMIKS_BORDER: int = 3
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
	theme.set_stylebox("focus", "Button", focus_ring(UiSkin.BUTTON_RADIUS))
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
		theme.set_stylebox("panel", type, surface_box(UiSkin.RADIUS_L, KOMIKS_BORDER, 20.0))
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


## The flat version of a button colour, styled with 3px `#161219` komiks ink borders and a hard comic drop-shadow.
static func painted_button(color: String, pressed: bool = false, tint: Color = Color.WHITE) -> StyleBoxFlat:
	var face: Color = semantic_color(color) * tint
	if pressed:
		face = face.darkened(0.08)
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = face
	box.set_corner_radius_all(CORNER)
	box.set_border_width_all(KOMIKS_BORDER)
	box.border_color = KOMIKS_INK
	box.shadow_color = Color(KOMIKS_INK, 0.88)
	box.shadow_size = BUTTON_SHADOW
	box.shadow_offset = Vector2(2.0, 2.0) if pressed else Vector2(4.0, 5.0)
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
	box.border_color = KOMIKS_INK
	return box


static func set_button_styles(theme: Theme, type: String, color: String) -> void:
	var variant: String = skin_variant(color)
	theme.set_stylebox("normal", type, UiSkin.button(variant))
	theme.set_stylebox("hover", type, UiSkin.button(variant, "hover"))
	theme.set_stylebox("pressed", type, UiSkin.button(variant, "pressed"))
	theme.set_stylebox("hover_pressed", type, UiSkin.button(variant, "pressed"))


## Dresses a button in a game-button variant: a gradient face with a lip (UiSkin), a heavy label, and the shared press
## feel (UiKit.feel_press), wired once.
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
	UiKit.feel_press(button)


static func surface_box(corner: int = UiSkin.RADIUS_L, border: int = 3, padding: float = PANEL_PADDING) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = Color(SURFACE, 0.98)
	box.set_corner_radius_all(corner)
	box.set_border_width_all(maxi(border, 1))
	box.border_color = KOMIKS_INK
	box.shadow_color = Color(KOMIKS_INK, 0.85)
	box.shadow_size = 1
	box.shadow_offset = Vector2(4.0, 5.0)
	box.set_content_margin_all(padding)
	box.anti_aliasing = true
	return box


## A 90s Filipino Komiks newsprint/ink badge plate with bold `#161219` borders and hard comic drop-shadow.
static func komiks_plate(fill: Color = KOMIKS_PAPER, border_ink: Color = KOMIKS_INK, border_px: int = 3, padding: float = PANEL_PADDING, radius: int = UiSkin.RADIUS_M) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = fill
	box.set_corner_radius_all(radius)
	box.set_border_width_all(border_px)
	box.border_color = border_ink
	box.shadow_color = Color(KOMIKS_INK, 0.9)
	box.shadow_size = 1
	box.shadow_offset = Vector2(4.0, 5.0)
	box.set_content_margin_all(padding)
	box.anti_aliasing = true
	return box


## The standard panel: a lit glass plate with a soft shadow (UiSkin).
static func painted_panel(padding: float = PANEL_PADDING) -> StyleBox:
	return UiSkin.plate("glass", padding)


## The charcoal-inked comic frame of a modal: 4px `#161219` border and hard offset comic drop-shadow.
static func modal_frame(rim: float) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = MODAL_FRAME
	box.set_corner_radius_all(MODAL_FRAME_RADIUS)
	box.set_border_width_all(4)
	box.border_color = MODAL_FRAME_EDGE
	box.shadow_color = Color(KOMIKS_INK, 0.92)
	box.shadow_size = 2
	box.shadow_offset = Vector2(6.0, 7.0)
	box.set_content_margin_all(rim)
	box.anti_aliasing = true
	return box


## The sun-aged komiks newsprint cream (`#F6ECD8`) body inside a modal's frame.
static func modal_body(padding: float) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = MODAL_BODY
	box.set_border_width_all(3)
	box.border_color = MODAL_BODY_EDGE
	box.set_corner_radius_all(MODAL_BODY_RADIUS)
	box.set_content_margin_all(padding)
	box.anti_aliasing = true
	return box


## What text inside a modal's light body looks like: dark, so every label put in a dialog reads without its own colour.
static func modal_body_theme() -> Theme:
	var theme: Theme = Theme.new()
	theme.set_color("font_color", "Label", MODAL_TEXT)
	theme.set_color("font_color", "MutedLabel", MODAL_TEXT_MUTED)
	return theme


static func painted_pill() -> StyleBoxFlat:
	var box: StyleBoxFlat = surface_box(UiSkin.RADIUS_L, 3, 0.0)
	box.bg_color = Color(INK, 0.94)
	box.border_color = Color(AMBER, 0.55)
	box.content_margin_left = 18.0
	box.content_margin_right = 18.0
	box.content_margin_top = 9.0
	box.content_margin_bottom = 10.0
	return box


static func map_title_plate() -> StyleBoxFlat:
	var box: StyleBoxFlat = surface_box(UiSkin.RADIUS_M, 3, 0.0)
	box.bg_color = Color("233454")
	box.border_color = AMBER
	box.shadow_color = Color(KOMIKS_INK, 0.9)
	box.shadow_size = 1
	box.shadow_offset = Vector2(4.0, 5.0)
	box.content_margin_left = 28.0
	box.content_margin_right = 28.0
	box.content_margin_top = 8.0
	box.content_margin_bottom = 10.0
	return box


static func field_box(focused: bool, read_only: bool = false) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = Color(SURFACE_RAISED if read_only else FIELD, 0.98)
	box.set_corner_radius_all(UiSkin.RADIUS_M)
	box.set_border_width_all(4 if focused else 3)
	box.border_color = AMBER if focused else KOMIKS_INK
	box.content_margin_left = 18.0
	box.content_margin_right = 18.0
	box.content_margin_top = 15.0
	box.content_margin_bottom = 15.0
	box.shadow_color = Color(KOMIKS_INK, 0.8)
	box.shadow_size = 1
	box.shadow_offset = Vector2(3.0, 4.0)
	box.anti_aliasing = true
	return box


## The focus ring round a control whose corners are `radius`: the one ring, UiSkin's.
static func focus_ring(radius: int) -> StyleBoxFlat:
	return UiSkin.focus_ring(radius)
