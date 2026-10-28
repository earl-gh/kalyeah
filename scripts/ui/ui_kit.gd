class_name UiKit
extends RefCounted
## Small builders every screen shares, so a title, a card, a chip or a face looks the same wherever
## it appears. (The look itself is UiSkin; the sizes are UiMetrics.)

const CROP: Shader = preload("res://shaders/minimap_crop.gdshader")
## The press feel every styled button shares (Play again, Leave, Swap, Ready, the segmented control and the choice cards
## gave nothing under the thumb): held, the button sinks to `PRESS_SCALE` toward its centre in `PRESS_DOWN` seconds; let
## go, it springs back in `PRESS_UP` with a small overshoot. Under Reduce motion it does not move.
const PRESS_SCALE: float = 0.955
const PRESS_DOWN: float = 0.07
const PRESS_UP: float = 0.16
## A card nested in a big (`UiSkin.RADIUS_XL`) panel: it sits `NEST_GAP` in from the panel's edge and its corners are the
## panel's less that gap, so the two curves run parallel (docs/ART.md "Nested curves are parallel").
const NEST_GAP: int = UiMetrics.GAP
const NESTED_RADIUS: int = UiSkin.RADIUS_XL - NEST_GAP
## Main-menu titles share the Play logo's row and top spacing.
const PAGE_HEADER_ROW_HEIGHT: float = 92.0
const PAGE_HEADER_DROP: float = 62.0


## Wires `button` for the shared press feel. Safe to call again on the same button: it is wired once.
static func feel_press(button: BaseButton) -> void:
	if button.has_meta(&"feels_press"):
		return
	button.set_meta(&"feels_press", true)
	button.button_down.connect(_press_to.bind(button, PRESS_SCALE, PRESS_DOWN, Tween.TRANS_QUAD))
	button.button_up.connect(_press_to.bind(button, 1.0, PRESS_UP, Tween.TRANS_BACK))


static func _press_to(button: BaseButton, target: float, seconds: float, transition: Tween.TransitionType) -> void:
	if not is_instance_valid(button) or not button.is_inside_tree():
		return
	var previous: Tween = (button.get_meta(&"press_tween") as Tween) if button.has_meta(&"press_tween") else null
	if is_instance_valid(previous):
		previous.kill()
	if Settings.reduced_motion:
		button.scale = Vector2.ONE
		return
	button.pivot_offset = button.size / 2.0
	var tween: Tween = button.create_tween()
	button.set_meta(&"press_tween", tween)
	tween.tween_property(button, "scale", Vector2.ONE * target, seconds).set_trans(transition).set_ease(Tween.EASE_OUT)


## Letters a big word (`UiMetrics.HEADLINE`, `DISPLAY`, `HERO` or `SCORE`): the display face, an outline that grows with
## the size and a shadow dropped under it, so every title, banner and score reads as one family (docs/ART.md).
static func letter(label: Label, size: int, color: Color = KalyeahTheme.CREAM, ink: Color = UiSkin.LETTER_INK) -> void:
	label.theme_type_variation = &"DisplayLabel"
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", color)
	label.add_theme_color_override("font_outline_color", ink)
	label.add_theme_constant_override("outline_size", UiSkin.letter_outline(size))
	label.add_theme_color_override("font_shadow_color", UiSkin.LETTER_SHADOW)
	label.add_theme_constant_override("shadow_offset_x", 0)
	label.add_theme_constant_override("shadow_offset_y", UiSkin.letter_shadow(size))
	label.add_theme_constant_override("shadow_outline_size", UiSkin.letter_outline(size))


## A centered page title aligned with the Play logo row, without subtitle or decoration.
static func page_header(title_text: String) -> VBoxContainer:
	var column: VBoxContainer = VBoxContainer.new()
	column.name = "Header"
	column.add_theme_constant_override("separation", 0)
	column.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var air: Control = Control.new()
	air.custom_minimum_size.y = PAGE_HEADER_DROP
	air.mouse_filter = Control.MOUSE_FILTER_IGNORE
	column.add_child(air)
	var title: Label = Label.new()
	title.name = "Title"
	title.text = title_text
	title.custom_minimum_size.y = PAGE_HEADER_ROW_HEIGHT
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	letter(title, UiMetrics.DISPLAY)
	column.add_child(title)
	return column


## A glass card holding `content` (or empty, to fill later); `NESTED_RADIUS` for a card inside a big panel.
static func card(content: Control = null, kind: String = "glass", padding: float = UiMetrics.PADDING, radius: int = UiSkin.PLATE_RADIUS) -> PanelContainer:
	var panel: PanelContainer = PanelContainer.new()
	panel.add_theme_stylebox_override("panel", UiSkin.plate(kind, padding, radius))
	# a card never swallows a touch of its own: a drag that starts on it scrolls the page it sits in
	panel.mouse_filter = Control.MOUSE_FILTER_PASS
	if content != null:
		panel.add_child(content)
	return panel


## A small label for a group of settings or a list ("AUDIO", "COMFORT").
static func section_label(text: String) -> Label:
	var label: Label = Label.new()
	label.text = text.to_upper()
	label.add_theme_font_override("font", KalyeahTheme.BUTTON_FONT)
	label.add_theme_font_size_override("font_size", UiMetrics.CAPTION)
	label.add_theme_color_override("font_color", Color("ffc233"))
	return label


## Chip fills that keep white text readable (every one is at least 4.5:1 against white; see `contrast`).
const CHIP_GREEN: Color = Color("1a8340")
const CHIP_RED: Color = Color("c2332e")
const CHIP_BLUE: Color = Color("2068d0")
const CHIP_PURPLE: Color = Color("8a55e8")
const CHIP_SLATE: Color = Color("3b4c74")


## The WCAG contrast ratio between two colours (1 to 21; 4.5 is the bar for small text).
static func contrast(a: Color, b: Color) -> float:
	var la: float = _luminance(a)
	var lb: float = _luminance(b)
	return (maxf(la, lb) + 0.05) / (minf(la, lb) + 0.05)


static func _luminance(color: Color) -> float:
	var channels: Array[float] = []
	for value: float in [color.r, color.g, color.b]:
		channels.append(value / 12.92 if value <= 0.03928 else pow((value + 0.055) / 1.055, 2.4))
	return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2]


## A round or pill tag: "Victory", "ATK", "Host" in a solid colour.
static func chip(text: String, fill: Color, ink: Color = Color.WHITE, size: int = UiMetrics.CAPTION) -> PanelContainer:
	var panel: PanelContainer = PanelContainer.new()
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = fill
	box.set_corner_radius_all(UiSkin.RADIUS_L)
	box.border_color = fill.darkened(0.35)
	box.set_border_width_all(2)
	box.content_margin_left = 16.0
	box.content_margin_right = 16.0
	box.content_margin_top = 4.0
	box.content_margin_bottom = 4.0
	box.anti_aliasing = true
	panel.add_theme_stylebox_override("panel", box)
	var label: Label = Label.new()
	label.name = "Text"
	label.text = text
	label.add_theme_font_override("font", KalyeahTheme.BUTTON_FONT)
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", ink)
	panel.add_child(label)
	return panel


## A tag that is only coloured words ("Attack", "Victory"): no pill behind it. Use it when the colour and the
## word already say it; a chip (solid fill) is for a number or a status that must stand out from its card.
static func tag(text: String, color: Color, size: int = UiMetrics.CAPTION) -> PanelContainer:
	var panel: PanelContainer = PanelContainer.new()
	panel.add_theme_stylebox_override("panel", StyleBoxEmpty.new())
	var label: Label = Label.new()
	label.name = "Text"
	label.text = text
	label.add_theme_font_override("font", KalyeahTheme.BUTTON_FONT)
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", color)
	panel.add_child(label)
	return panel


## A kid's face cropped to a circle (`px` wide), from the owner's key-art crops.
static func avatar(character_id: String, px: float) -> TextureRect:
	var face: TextureRect = TextureRect.new()
	face.custom_minimum_size = Vector2(px, px)
	face.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	face.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	face.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	face.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var picture: Texture2D = KidModel.minimap_face(StringName(character_id))
	face.texture = picture if picture != null else KidModel.portrait(StringName(character_id))
	var crop: ShaderMaterial = ShaderMaterial.new()
	crop.shader = CROP
	crop.set_shader_parameter("rect_size", Vector2(px, px))
	crop.set_shader_parameter("radius", px / 2.0)
	face.material = crop
	return face


## A label with the page's body face; `size` defaults to the body size.
static func text(content: String, size: int = UiMetrics.BODY, color: Color = KalyeahTheme.TEXT, wrap: bool = true) -> Label:
	var label: Label = Label.new()
	label.text = content
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", color)
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART if wrap else TextServer.AUTOWRAP_OFF
	return label


## The row of a settings list: a title with an optional line under it, and a control at the end.
static func setting_row(title_text: String, detail: String, control: Control) -> HBoxContainer:
	var row: HBoxContainer = HBoxContainer.new()
	row.add_theme_constant_override("separation", UiMetrics.GAP)
	var words: VBoxContainer = VBoxContainer.new()
	words.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	words.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	words.add_theme_constant_override("separation", 0)
	var title: Label = text(title_text, UiMetrics.BODY, KalyeahTheme.TEXT, false)
	# the colours come from the theme (light on a dark page, dark inside a dialog's light body: `KalyeahTheme.modal_body_theme`)
	title.remove_theme_color_override("font_color")
	title.name = "Title"
	words.add_child(title)
	if not detail.is_empty():
		var line: Label = text(detail, UiMetrics.CAPTION, Color("9fb0d6"))
		line.name = "Detail"
		line.remove_theme_color_override("font_color")
		line.theme_type_variation = &"MutedLabel"
		line.add_theme_constant_override("line_spacing", KalyeahTheme.READING_LINE_SPACING)
		words.add_child(line)
	row.add_child(words)
	row.add_child(control)
	# a switch or a slider has no words of its own: it is read by its row's title
	if control.accessibility_name.is_empty():
		control.accessibility_name = title_text
		if not detail.is_empty():
			control.accessibility_description = detail
	return row


## A thin line between rows inside a card.
static func divider() -> ColorRect:
	var line: ColorRect = ColorRect.new()
	line.custom_minimum_size = Vector2(0.0, 2.0)
	line.color = Color(1.0, 1.0, 1.0, 0.08)
	line.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return line
