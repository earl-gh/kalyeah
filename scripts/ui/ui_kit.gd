class_name UiKit
extends RefCounted
## The kit's components, built once and shared by every screen, so a title, a card, a chip, a bar or a face looks the
## same wherever it appears. (The look and its tokens are UiSkin; the sizes are UiMetrics; the motion is UiMotion.)

const CROP: Shader = preload("res://shaders/minimap_crop.gdshader")


## The gold of section labels and a heading's short bar.
const ACCENT: Color = Color("ffc233")
## The short bar under a page's heading.
const HEADER_BAR: Vector2 = Vector2(110.0, 8.0)


## A page's heading (the banner/title component): a big outlined title casting the one shadow, a quiet line under it
## and a short gold bar.
static func page_header(title_text: String, subtitle: String = "") -> VBoxContainer:
	var column: VBoxContainer = VBoxContainer.new()
	column.name = "Header"
	column.add_theme_constant_override("separation", 2)
	var title: Label = Label.new()
	title.name = "Title"
	title.text = title_text
	KalyeahTheme.style_title(title, UiMetrics.DISPLAY)
	column.add_child(title)
	if not subtitle.is_empty():
		var line: Label = Label.new()
		line.name = "Subtitle"
		line.text = subtitle
		line.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		line.add_theme_constant_override("line_spacing", KalyeahTheme.READING_LINE_SPACING)
		line.add_theme_font_size_override("font_size", UiMetrics.DETAIL)
		line.add_theme_color_override("font_color", KalyeahTheme.TEXT_MUTED)
		column.add_child(line)
	var bar: Panel = Panel.new()
	bar.custom_minimum_size = HEADER_BAR
	bar.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	bar.add_theme_stylebox_override("panel", UiSkin.bar_fill(ACCENT))
	column.add_child(bar)
	return column


## A card (the panel component) holding `content` (or empty, to fill later): a UiSkin plate of `kind`, MEDIUM corners.
static func card(content: Control = null, kind: String = "glass", padding: float = UiMetrics.PADDING) -> PanelContainer:
	var panel: PanelContainer = PanelContainer.new()
	panel.add_theme_stylebox_override("panel", UiSkin.plate(kind, padding, UiSkin.RADIUS_M))
	# a card never swallows a touch of its own: a drag that starts on it scrolls the page it sits in
	panel.mouse_filter = Control.MOUSE_FILTER_PASS
	if content != null:
		panel.add_child(content)
	return panel


## A small label for a group of settings or a list ("Sound and feel"), in sentence case.
static func section_label(text: String) -> Label:
	var label: Label = Label.new()
	label.text = text
	label.theme_type_variation = &"SectionLabel"
	label.add_theme_font_size_override("font_size", UiMetrics.CAPTION)
	label.add_theme_color_override("font_color", ACCENT)
	return label


## Chip fills that keep white text readable (every one is at least 4.5:1 against white; see `contrast`). A win and a loss
## wear the heal and attack hues; slate is a neutral tag.
const CHIP_GREEN: Color = TeamColors.KIND_HEAL
const CHIP_RED: Color = TeamColors.KIND_ATTACK
const CHIP_SLATE: Color = TeamColors.KIND_NONE


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
	panel.add_theme_stylebox_override("panel", UiSkin.chip(fill))
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


## A label with the page's body face; `size` defaults to the body size and the colour to body copy's.
static func text(content: String, size: int = UiMetrics.BODY, color: Color = KalyeahTheme.TEXT_BODY, wrap: bool = true) -> Label:
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
	title.name = "Title"
	words.add_child(title)
	if not detail.is_empty():
		var line: Label = text(detail, UiMetrics.CAPTION, KalyeahTheme.TEXT_MUTED)
		line.name = "Detail"
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


## A bar (the bar component) `fraction` full of `color`, `height` px tall: a dark outlined track and a round-ended fill.
static func bar(fraction: float, color: Color, height: float) -> Panel:
	var track: Panel = Panel.new()
	track.name = "Bar"
	track.custom_minimum_size = Vector2(0.0, height)
	track.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	track.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	track.mouse_filter = Control.MOUSE_FILTER_IGNORE
	track.add_theme_stylebox_override("panel", UiSkin.bar_track())
	var fill: Panel = Panel.new()
	fill.name = "Fill"
	fill.mouse_filter = Control.MOUSE_FILTER_IGNORE
	fill.add_theme_stylebox_override("panel", UiSkin.bar_fill(color))
	fill.anchor_bottom = 1.0
	fill.anchor_right = clampf(fraction, 0.0, 1.0)
	fill.offset_left = UiSkin.LINE
	fill.offset_top = UiSkin.LINE
	fill.offset_right = -UiSkin.LINE
	fill.offset_bottom = -UiSkin.LINE
	fill.custom_minimum_size = Vector2(height - 2.0 * UiSkin.LINE, 0.0)
	track.add_child(fill)
	return track


## A thin line between rows inside a card.
static func divider() -> ColorRect:
	var line: ColorRect = ColorRect.new()
	line.custom_minimum_size = Vector2(0.0, 2.0)
	line.color = Color(1.0, 1.0, 1.0, 0.08)
	line.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return line
