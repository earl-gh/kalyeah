class_name Hig
extends RefCounted
## Apple Human Interface Guidelines, as numbers and small builders for the menu screens (main
## screen, lobby, character and weapon select). The UI is laid out at 720 px wide, which is
## about 390 pt on an iPhone, so 1 pt is about 1.85 px (`POINT`).
## What the screens follow (see docs/ART.md "Apple HIG pass"):
##  - tap targets at least 44 pt (`MIN_TAP`) and text at least 11 pt (`MIN_TEXT`);
##  - 16 pt screen margins (`MARGIN`) on top of the notch / home-bar safe area, 8 pt spacing;
##  - one clear primary action per screen, everything else quieter;
##  - a navigation bar with "< Back" at the top left of every pushed screen;
##  - grouped sections (small grey header, rounded group, footnote) instead of loose controls;
##  - segmented controls for a short choice, a scrolling tab row for a long one;
##  - text and fills with enough contrast, selected state shown by more than colour.

const POINT: float = 720.0 / 390.0
const MIN_TAP: float = 84.0
const MIN_TEXT: int = 20
const MARGIN: float = 30.0
const GAP: float = 16.0
const RADIUS: int = 24

## Type scale (px at 720 wide): caption 11 pt, footnote 13 pt, body 17 pt, title 22 pt.
const CAPTION: int = 22
const FOOTNOTE: int = 24
const BODY: int = 31
const TITLE: int = 40

const LABEL: Color = Color(1.0, 1.0, 1.0)
const SECONDARY: Color = Color(0.8, 0.86, 1.0)
const TINT: Color = Color(0.45, 0.7, 1.0)
const DESTRUCTIVE: Color = Color(1.0, 0.5, 0.48)
const GROUP_FILL: Color = Color(0.04, 0.07, 0.2, 0.82)
const GROUP_EDGE: Color = Color(0.5, 0.6, 1.0, 0.28)
const SELECTED: Color = Color(1.0, 0.82, 0.25)
const ON_SELECTED: Color = Color(0.1, 0.06, 0.06)


## The unsafe screen edges (left, top, right, bottom) in UI pixels: the notch, the status bar,
## rounded corners and the home bar. Zero where the window has none.
static func safe_insets(viewport: Viewport) -> Vector4:
	var window: Vector2i = DisplayServer.window_get_size()
	if window.x <= 0 or window.y <= 0 or viewport == null:
		return Vector4.ZERO
	var safe: Rect2i = DisplayServer.get_display_safe_area()
	var origin: Vector2i = DisplayServer.window_get_position()
	var to_ui: Vector2 = viewport.get_visible_rect().size / Vector2(window)
	var left: float = maxf(0.0, float(safe.position.x - origin.x))
	var top: float = maxf(0.0, float(safe.position.y - origin.y))
	var right: float = maxf(0.0, float(origin.x + window.x - (safe.position.x + safe.size.x)))
	var bottom: float = maxf(0.0, float(origin.y + window.y - (safe.position.y + safe.size.y)))
	return Vector4(minf(left * to_ui.x, 120.0), minf(top * to_ui.y, 120.0), minf(right * to_ui.x, 120.0), minf(bottom * to_ui.y, 120.0))


## Plain text in the type scale: no outline (the painted outline is for the game, not for reading).
static func label(text: String, size: int = BODY, color: Color = LABEL, align: HorizontalAlignment = HORIZONTAL_ALIGNMENT_LEFT) -> Label:
	var item: Label = Label.new()
	item.text = text
	item.horizontal_alignment = align
	item.add_theme_font_size_override("font_size", size)
	item.add_theme_color_override("font_color", color)
	item.add_theme_constant_override("outline_size", 0)
	return item


## A rounded, lightly filled group that holds the rows of one section.
static func group_style() -> StyleBoxFlat:
	var style: StyleBoxFlat = StyleBoxFlat.new()
	style.bg_color = GROUP_FILL
	style.border_color = GROUP_EDGE
	style.set_border_width_all(2)
	style.set_corner_radius_all(RADIUS)
	style.content_margin_left = GAP
	style.content_margin_right = GAP
	style.content_margin_top = GAP * 0.75
	style.content_margin_bottom = GAP * 0.75
	style.anti_aliasing = true
	return style


static func group() -> PanelContainer:
	var card: PanelContainer = PanelContainer.new()
	card.add_theme_stylebox_override("panel", group_style())
	var rows: VBoxContainer = VBoxContainer.new()
	rows.name = "Rows"
	rows.add_theme_constant_override("separation", int(GAP / 2.0))
	card.add_child(rows)
	return card


## Where a group's rows go.
static func rows(card: PanelContainer) -> VBoxContainer:
	return card.get_child(0) as VBoxContainer


## The small grey caps above a group ("GAME SERVER").
static func section_header(text: String) -> Label:
	var item: Label = label(text.to_upper(), CAPTION, SECONDARY)
	item.add_theme_constant_override("line_spacing", 0)
	return item


## The footnote under a group.
static func footnote(text: String, color: Color = SECONDARY) -> Label:
	var item: Label = label(text, FOOTNOTE, color)
	item.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	return item


## A flat text button (secondary action): tinted text on a quiet fill, at least 44 pt tall.
static func text_button(text: String, color: Color = TINT) -> Button:
	var button: Button = Button.new()
	button.text = text
	button.custom_minimum_size = Vector2(0.0, MIN_TAP)
	button.focus_mode = Control.FOCUS_NONE
	button.add_theme_font_size_override("font_size", BODY)
	for state: String in ["font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color"]:
		button.add_theme_color_override(state, color)
	button.add_theme_color_override("font_disabled_color", Color(color, 0.4))
	button.add_theme_constant_override("outline_size", 0)
	var rest: StyleBoxFlat = StyleBoxFlat.new()
	rest.bg_color = Color(1.0, 1.0, 1.0, 0.07)
	rest.set_corner_radius_all(RADIUS)
	rest.set_content_margin_all(GAP)
	var down: StyleBoxFlat = rest.duplicate() as StyleBoxFlat
	down.bg_color = Color(1.0, 1.0, 1.0, 0.18)
	for state: String in ["normal", "disabled", "focus"]:
		button.add_theme_stylebox_override(state, rest)
	for state: String in ["hover", "pressed", "hover_pressed"]:
		button.add_theme_stylebox_override(state, down)
	return button


## The navigation bar: "< Back" at the left, the title centred, a spacer the width of the back
## button at the right so the title is truly centred.
static func nav_bar(title: String, on_back: Callable, back_text: String = "Back") -> HBoxContainer:
	var bar: HBoxContainer = HBoxContainer.new()
	bar.name = "NavBar"
	var back: Button = text_button("‹ " + back_text)
	back.name = "BackButton"
	back.custom_minimum_size = Vector2(190.0, MIN_TAP)
	back.pressed.connect(on_back)
	bar.add_child(back)
	var heading: Label = label(title, TITLE, LABEL, HORIZONTAL_ALIGNMENT_CENTER)
	heading.name = "Title"
	heading.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	heading.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	bar.add_child(heading)
	var spacer: Control = Control.new()
	spacer.custom_minimum_size = Vector2(190.0, MIN_TAP)
	bar.add_child(spacer)
	return bar


## Turns toggle buttons into one segmented control (equal segments in a rounded rail; the
## selected segment is filled and its text turns dark: shape and contrast, not only colour).
static func segmented(buttons: Array[Button]) -> PanelContainer:
	var rail: PanelContainer = PanelContainer.new()
	var rail_style: StyleBoxFlat = StyleBoxFlat.new()
	rail_style.bg_color = Color(1.0, 1.0, 1.0, 0.1)
	rail_style.set_corner_radius_all(RADIUS)
	rail_style.set_content_margin_all(5.0)
	rail.add_theme_stylebox_override("panel", rail_style)
	var row: HBoxContainer = HBoxContainer.new()
	row.add_theme_constant_override("separation", 4)
	rail.add_child(row)
	for button: Button in buttons:
		style_segment(button)
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		row.add_child(button)
	return rail


## One segment (or one scrolling tab): quiet when off, filled when on.
static func style_segment(button: Button) -> void:
	button.toggle_mode = true
	button.focus_mode = Control.FOCUS_NONE
	button.custom_minimum_size = Vector2(button.custom_minimum_size.x, MIN_TAP)
	button.add_theme_font_size_override("font_size", FOOTNOTE + 2)
	button.add_theme_constant_override("outline_size", 0)
	for state: String in ["font_color", "font_hover_color", "font_focus_color"]:
		button.add_theme_color_override(state, LABEL)
	button.add_theme_color_override("font_disabled_color", Color(LABEL, 0.4))
	for state: String in ["font_pressed_color", "font_hover_pressed_color"]:
		button.add_theme_color_override(state, ON_SELECTED)
	var off: StyleBoxFlat = StyleBoxFlat.new()
	off.bg_color = Color(1.0, 1.0, 1.0, 0.0)
	off.set_corner_radius_all(RADIUS - 6)
	off.set_content_margin_all(10.0)
	var hover: StyleBoxFlat = off.duplicate() as StyleBoxFlat
	hover.bg_color = Color(1.0, 1.0, 1.0, 0.1)
	var on: StyleBoxFlat = off.duplicate() as StyleBoxFlat
	on.bg_color = SELECTED
	for state: String in ["normal", "disabled", "focus"]:
		button.add_theme_stylebox_override(state, off)
	button.add_theme_stylebox_override("hover", hover)
	for state: String in ["pressed", "hover_pressed"]:
		button.add_theme_stylebox_override(state, on)
