class_name KalyeahTheme
extends RefCounted
## Kalyeah's sunlit 90s Philippine Kalye UI system: warm sunlit cream (`#FAF3E3`), soft tropical
## slate-espresso (`#2A2522`), ripe mango amber (`#F5A623`), sari-sari jade teal (`#1F9D8B`), and
## terracotta coral (`#E2553E`), paired with open-licensed signboard display + clean sans typography.

## Open-licensed SIL OFL brand typefaces (`assets/fonts/brand/`):
## - `DISPLAY_FONT` (`KalyeahDisplay-Regular.ttf`, Londrina Solid): hand-painted 90s provincial signboard headings.
## - `SANS_FONT` (`KalyeahSans-Variable.ttf`, Source Sans 3 Variable): crisp readable body copy (`600`),
##   captions (`700`), and chunky buttons + compact HUD action figures (`850`).
const DISPLAY_FONT: Font = preload("res://assets/fonts/brand/KalyeahDisplay-Regular.ttf")
const SANS_FONT: Font = preload("res://assets/fonts/brand/KalyeahSans-Variable.ttf")
const WORDMARK_PATH: String = "res://assets/ui/brand/kalyeah_wordmark.png"
const CREST_PATH: String = "res://assets/ui/brand/kalyeah_crest.png"

## 32-bit OpenType `'wght'` axis tag (`TextServer.name_to_tag("wght") == 2003265652`).
const WGHT_TAG: int = 2003265652
const BODY_WEIGHT: int = 600
const BUTTON_WEIGHT: int = 850
const CAPTION_WEIGHT: int = 700
const ACTION_WEIGHT: int = 850

const BODY_TRACKING: int = 0
const BUTTON_TRACKING: int = 0
const CAPTION_TRACKING: int = 2
## Extra space at word gaps so tracked uppercase labels breathe cleanly.
const WORD_GAP: int = 2
## Extra space between the lines of wrapped text.
const LINE_SPACING: int = 0
## Paragraphs (descriptions, tips, hints) open their lines up a little more; tight tiles and buttons keep the default.
const READING_LINE_SPACING: int = 6

static var BODY_FONT: Font = _sans_variation(BODY_WEIGHT, BODY_TRACKING, WORD_GAP)
static var CAPTION_FONT: Font = _sans_variation(CAPTION_WEIGHT, CAPTION_TRACKING, WORD_GAP)
static var BUTTON_FONT: Font = _sans_variation(BUTTON_WEIGHT, BUTTON_TRACKING, 0)
## Numbers and the match's drawn text: extra-bold (`wght = 850`) and untracked so digits and scores stay crisp.
static var ACTION_FONT: Font = _sans_variation(ACTION_WEIGHT, 0, 0)
## Compact widescreen MOBA stage fonts with trimmed FreeType vertical air so 2-line glass capsules & gold CTAs never clip.
static var STAGE_TITLE_FONT: Font = _stage_sans_variation(780, 0.92, -5, -6, 0)
static var STAGE_SUB_FONT: Font = _stage_sans_variation(650, 0.81, -5, -6, -1)
static var STAGE_DISPLAY_FONT: Font = _stage_display_variation(-6, -6)

const INK: Color = Color("2a2522")
const INK_DEEP: Color = Color("14100e")
const KOMIKS_INK: Color = Color("2a2522")
const KOMIKS_PAPER: Color = Color("faf3e3")
const SURFACE: Color = Color("2a2522")
const SURFACE_RAISED: Color = Color("38312d")
const CREAM: Color = Color("faf3e3")
const CREAM_WARM: Color = Color("f2e2c4")
const TEXT: Color = Color("faf3e3")
const TEXT_MUTED: Color = Color("d8cbb5")
const AMBER: Color = Color("f5a623")
const AMBER_HOVER: Color = Color("ffc048")
const MANGO: Color = Color("f5a623")
const BLUE: Color = Color("2d9bf0")
const JADE: Color = Color("1f9d8b")
const GREEN: Color = Color("1f9d8b")
const CORAL: Color = Color("e2553e")
const RED: Color = Color("e2553e")
const SLATE: Color = Color("463d38")
const FOCUS: Color = UiSkin.FOCUS_COLOR
const SUCCESS: Color = Color("5fd9c6")
const ERROR: Color = Color("ff9f8e")
const TEAM_BLUE: Color = Color("1d56b5")
const TEAM_RED: Color = Color("b8352b")
const FIELD: Color = Color("211c19")

## Modern MOBA glassmorphic & radiant gold header/rail surface tokens (Wild Rift / MLBB benchmark).
const GLASS_DARK: Color = Color(0.09, 0.08, 0.07, 0.76)
const GLASS_HOVER: Color = Color(0.15, 0.12, 0.10, 0.86)
const GLASS_ACTIVE: Color = Color(0.21, 0.16, 0.10, 0.88)
const GLASS_RIM: Color = Color(0.98, 0.91, 0.74, 0.28)
const GOLD_BRIGHT: Color = Color("ffe680")
const GOLD_CORE: Color = Color("f5a623")
const GOLD_DEEP: Color = Color("b8680a")

## Sunlit 90s Kalye modal sheet: warm roasted timber-slate header frame (`#3A302B`) around
## a bright sunlit cream (`#FAF3E3`) body with soft tropical espresso text.
const MODAL_FRAME: Color = Color("3a302b")
const MODAL_FRAME_EDGE: Color = Color("2a2522")
const MODAL_BODY: Color = Color("faf3e3")
const MODAL_BODY_EDGE: Color = Color("d6c3a3")
const MODAL_TITLE: Color = Color("faf3e3")
const MODAL_TITLE_OUTLINE: Color = Color("2a2522")
const MODAL_TEXT: Color = Color("2a2522")
const MODAL_TEXT_MUTED: Color = Color("635449")
## Modal corners (owner): rounder than the small skin radius, less than the big card rounding (steps of the UiSkin scale).
const MODAL_FRAME_RADIUS: int = UiSkin.RADIUS_L
const MODAL_BODY_RADIUS: int = UiSkin.RADIUS_M

## Flat surfaces share the skin's corner scale: CORNER is the button and card radius.
const CORNER: int = UiSkin.RADIUS_L
const BUTTON_SHADOW: int = 2
const KOMIKS_BORDER: int = 2
const FONT_SIZE: int = UiMetrics.BODY
const PANEL_PADDING: float = UiMetrics.PADDING


static func _sans_variation(weight: int, glyph: int = 0, word: int = 0) -> FontVariation:
	var font: FontVariation = FontVariation.new()
	font.base_font = SANS_FONT
	font.variation_opentype = {WGHT_TAG: weight}
	font.spacing_glyph = glyph
	font.spacing_space = word
	return font


static func _stage_sans_variation(weight: int, width_scale: float, top_trim: int, bottom_trim: int, glyph_spacing: int = 0) -> FontVariation:
	var font: FontVariation = FontVariation.new()
	font.base_font = SANS_FONT
	font.variation_opentype = {WGHT_TAG: weight}
	font.spacing_glyph = glyph_spacing
	font.spacing_space = 0
	font.spacing_top = top_trim
	font.spacing_bottom = bottom_trim
	font.variation_transform = Transform2D(Vector2(width_scale, 0.0), Vector2(0.0, 1.0), Vector2.ZERO)
	return font


static func _stage_display_variation(top_trim: int, bottom_trim: int) -> FontVariation:
	var font: FontVariation = FontVariation.new()
	font.base_font = DISPLAY_FONT
	font.spacing_top = top_trim
	font.spacing_bottom = bottom_trim
	return font


static func _tracked(glyph: int, word: int) -> Font:
	return _sans_variation(BODY_WEIGHT, glyph, word)


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
	theme.set_color("font_outline_color", "Label", INK)
	theme.set_constant("outline_size", "Label", 0)
	theme.set_constant("line_spacing", "Label", LINE_SPACING)
	theme.set_type_variation("DisplayLabel", "Label")
	theme.set_font("font", "DisplayLabel", DISPLAY_FONT)
	theme.set_color("font_color", "DisplayLabel", CREAM)
	theme.set_color("font_outline_color", "DisplayLabel", INK)
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
	theme.set_color("font_disabled_color", "Button", Color(TEXT_MUTED, 0.65))
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
		"orange", "gold", "mango":
			return AMBER
		"blue":
			return BLUE
		"green", "jade", "teal":
			return GREEN
		"red", "coral":
			return RED
		_:
			return SLATE


static func semantic_text_color(name: String) -> Color:
	return TEXT if name == "grey" or name == "slate" else INK_DEEP


## The UiSkin variant that draws a semantic colour name.
static func skin_variant(color: String) -> String:
	match color:
		"orange", "gold":
			return "gold"
		"mango", "blue", "green", "jade", "red", "coral", "navy":
			return color
		_:
			return "slate"


## Flat painted button surface with soft 2px `#2A2522` slate-espresso trim and gentle warm afternoon shadow.
static func painted_button(color: String, pressed: bool = false, tint: Color = Color.WHITE) -> StyleBoxFlat:
	var face: Color = semantic_color(color) * tint
	if pressed:
		face = face.darkened(0.04)
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = face
	box.set_corner_radius_all(CORNER)
	box.set_border_width_all(KOMIKS_BORDER)
	box.border_color = INK
	box.shadow_color = Color(INK_DEEP, 0.28)
	box.shadow_size = BUTTON_SHADOW
	box.shadow_offset = Vector2(0.0, 1.0) if pressed else Vector2(0.0, 3.0)
	box.content_margin_left = 20.0
	box.content_margin_right = 20.0
	box.content_margin_top = 21.0
	box.content_margin_bottom = 21.0
	box.anti_aliasing = true
	return box


## Kept for toggle controls and small custom surfaces.
static func button_box(color: Color, pressed: bool = false) -> StyleBoxFlat:
	var box: StyleBoxFlat = painted_button("grey", pressed)
	box.bg_color = color.darkened(0.05) if pressed else color
	box.border_color = INK
	return box


static func set_button_styles(theme: Theme, type: String, color: String) -> void:
	var variant: String = skin_variant(color)
	theme.set_stylebox("normal", type, UiSkin.button(variant))
	theme.set_stylebox("hover", type, UiSkin.button(variant, "hover"))
	theme.set_stylebox("pressed", type, UiSkin.button(variant, "pressed"))
	theme.set_stylebox("hover_pressed", type, UiSkin.button(variant, "pressed"))


## Dresses a button in a game-button variant: a gradient face with a lip (UiSkin), a clean bold label, and the shared press
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


static func surface_box(corner: int = UiSkin.RADIUS_L, border: int = 2, padding: float = PANEL_PADDING) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = Color(SURFACE, 0.97)
	box.set_corner_radius_all(corner)
	box.set_border_width_all(maxi(border, 1))
	box.border_color = Color("4a3f39")
	box.shadow_color = Color(INK_DEEP, 0.28)
	box.shadow_size = 4
	box.shadow_offset = Vector2(0.0, 3.0)
	box.set_content_margin_all(padding)
	box.anti_aliasing = true
	return box


## A sunlit 90s Kalye cream/signboard plate with clean 2px warm trim and soft drop shadow.
static func komiks_plate(fill: Color = KOMIKS_PAPER, border_ink: Color = KOMIKS_INK, border_px: int = 2, padding: float = PANEL_PADDING, radius: int = UiSkin.RADIUS_M) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = fill
	box.set_corner_radius_all(radius)
	box.set_border_width_all(maxi(border_px, 1))
	box.border_color = border_ink
	box.shadow_color = Color(INK_DEEP, 0.24)
	box.shadow_size = 3
	box.shadow_offset = Vector2(0.0, 3.0)
	box.set_content_margin_all(padding)
	box.anti_aliasing = true
	return box


## Warm sunlit cream (`#FAF3E3`) card surface with subtle sand-timber trim.
static func sunlit_card(padding: float = PANEL_PADDING, radius: int = UiSkin.RADIUS_L, accent: Color = Color("d9c5a3")) -> StyleBoxFlat:
	return komiks_plate(CREAM, accent, 2, padding, radius)


## Hand-painted provincial signboard surface (`#2A2522`) with mango-amber (`#F5A623`) trim.
static func signboard_plate(padding: float = PANEL_PADDING, radius: int = UiSkin.RADIUS_L, trim: Color = AMBER) -> StyleBoxFlat:
	var box: StyleBoxFlat = surface_box(radius, 2, padding)
	box.bg_color = SURFACE
	box.border_color = trim
	return box


## The standard panel: a lit glass plate with a soft shadow (UiSkin).
static func painted_panel(padding: float = PANEL_PADDING) -> StyleBox:
	return UiSkin.plate("glass", padding)


## The warm timber-slate frame of a modal: clean 2px border and soft shadow.
static func modal_frame(rim: float) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = MODAL_FRAME
	box.set_corner_radius_all(MODAL_FRAME_RADIUS)
	box.set_border_width_all(2)
	box.border_color = MODAL_FRAME_EDGE
	box.shadow_color = Color(INK_DEEP, 0.34)
	box.shadow_size = 6
	box.shadow_offset = Vector2(0.0, 4.0)
	box.set_content_margin_all(rim)
	box.anti_aliasing = true
	return box


## The warm sunlit cream (`#FAF3E3`) body inside a modal's frame.
static func modal_body(padding: float) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = MODAL_BODY
	box.set_border_width_all(1)
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
	var box: StyleBoxFlat = surface_box(UiSkin.RADIUS_L, 2, 0.0)
	box.bg_color = Color(INK, 0.94)
	box.border_color = Color(AMBER, 0.65)
	box.content_margin_left = 18.0
	box.content_margin_right = 18.0
	box.content_margin_top = 9.0
	box.content_margin_bottom = 10.0
	return box


static func map_title_plate() -> StyleBoxFlat:
	var box: StyleBoxFlat = surface_box(UiSkin.RADIUS_M, 2, 0.0)
	box.bg_color = SURFACE_RAISED
	box.border_color = AMBER
	box.shadow_color = Color(INK_DEEP, 0.30)
	box.shadow_size = 3
	box.shadow_offset = Vector2(0.0, 3.0)
	box.content_margin_left = 28.0
	box.content_margin_right = 28.0
	box.content_margin_top = 8.0
	box.content_margin_bottom = 10.0
	return box


static func field_box(focused: bool, read_only: bool = false) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = Color(SURFACE_RAISED if read_only else FIELD, 0.98)
	box.set_corner_radius_all(UiSkin.RADIUS_M)
	box.set_border_width_all(3 if focused else 2)
	box.border_color = AMBER if focused else Color("594c44")
	box.content_margin_left = 18.0
	box.content_margin_right = 18.0
	box.content_margin_top = 15.0
	box.content_margin_bottom = 15.0
	box.shadow_color = Color(INK_DEEP, 0.22)
	box.shadow_size = 2
	box.shadow_offset = Vector2(0.0, 2.0)
	box.anti_aliasing = true
	return box


## The focus ring round a control whose corners are `radius`: the one ring, UiSkin's.
static func focus_ring(radius: int) -> StyleBoxFlat:
	return UiSkin.focus_ring(radius)


## Sleek translucent dark-espresso glass capsule for slim header bars, status pills, and utility clusters.
static func glass_capsule(radius: int = UiSkin.RADIUS_L, border_color: Color = GLASS_RIM, fill: Color = GLASS_DARK, pad_x: float = 16.0, pad_y: float = 8.0) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = fill
	box.set_corner_radius_all(radius)
	box.set_border_width_all(1)
	box.border_color = border_color
	box.shadow_color = Color(INK_DEEP, 0.30)
	box.shadow_size = 4
	box.shadow_offset = Vector2(0.0, 2.0)
	box.content_margin_left = pad_x
	box.content_margin_right = pad_x
	box.content_margin_top = pad_y
	box.content_margin_bottom = pad_y
	box.anti_aliasing = true
	return box


## Frameless vertical rail tab highlight box with luminous left gold indicator bar when active.
static func nav_rail_box(active: bool = false, hovered: bool = false) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	if active:
		box.bg_color = GLASS_ACTIVE
		box.border_width_left = 4
		box.border_width_top = 1
		box.border_width_bottom = 1
		box.border_width_right = 1
		box.border_color = GOLD_CORE
	elif hovered:
		box.bg_color = Color(GLASS_HOVER, 0.56)
		box.border_width_left = 2
		box.border_color = Color(GOLD_BRIGHT, 0.65)
	else:
		box.bg_color = Color(GLASS_DARK, 0.0)
	box.set_corner_radius_all(UiSkin.RADIUS_S)
	box.content_margin_left = 18.0
	box.content_margin_right = 14.0
	box.content_margin_top = 8.0
	box.content_margin_bottom = 8.0
	box.anti_aliasing = true
	return box
