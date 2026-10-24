class_name LobbyWidgets
extends RefCounted
## Small presentation-only builders shared by the lobby. LobbyScreen keeps the
## room state and networking; this file owns reusable visual construction.


static func add_backdrop(parent: Control, texture: Texture2D) -> void:
	var picture: TextureRect = TextureRect.new()
	picture.texture = texture
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	picture.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(picture)
	var shade: ColorRect = ColorRect.new()
	shade.color = Color(0.015, 0.035, 0.08, 0.72)
	shade.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	shade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(shade)


static func header(title_text: String, back_action: Callable) -> HBoxContainer:
	var row: HBoxContainer = HBoxContainer.new()
	row.add_theme_constant_override("separation", 12)
	var back: Button = button("Back", back_action, "grey", UiMetrics.ACTION)
	back.custom_minimum_size = Vector2(120.0, UiMetrics.TOUCH)
	row.add_child(back)
	var title: Label = label(title_text, 38)
	title.theme_type_variation = &"DisplayLabel"
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	title.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	row.add_child(title)
	return row


static func label(text: String, font_size: int, color: Color = Color.WHITE) -> Label:
	var result: Label = Label.new()
	result.text = text
	result.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	result.add_theme_font_size_override("font_size", font_size)
	result.add_theme_color_override("font_color", color)
	return result


## A grouped surface for related controls. Screens keep ownership of their content;
## this only supplies the shared material and padding.
static func group(content: Control, padding: float = UiMetrics.PADDING) -> PanelContainer:
	var panel: PanelContainer = PanelContainer.new()
	panel.add_theme_stylebox_override("panel", KalyeahTheme.painted_panel(padding))
	panel.mouse_filter = Control.MOUSE_FILTER_PASS
	panel.add_child(content)
	return panel


static func field_label(text: String) -> Label:
	var result: Label = label(text, UiMetrics.DETAIL, KalyeahTheme.TEXT_MUTED)
	result.theme_type_variation = &"MutedLabel"
	result.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	return result


static func edit(text: String, placeholder: String, font_size: int) -> LineEdit:
	var result: LineEdit = LineEdit.new()
	result.text = text
	result.placeholder_text = placeholder
	result.custom_minimum_size = Vector2(0.0, UiMetrics.TOUCH)
	result.add_theme_font_size_override("font_size", font_size)
	return result


static func make_toggle(button_control: Button) -> void:
	button_control.toggle_mode = true
	for state: String in ["normal", "hover"]:
		button_control.add_theme_stylebox_override(state, UiSkin.button("slate"))
	for state: String in ["pressed", "hover_pressed"]:
		button_control.add_theme_stylebox_override(state, UiSkin.button("blue", "pressed"))
	UiSkin.style_label(button_control, "slate")
	for state: String in ["font_pressed_color", "font_hover_pressed_color"]:
		button_control.add_theme_color_override(state, Color.WHITE)


static func button(text: String, action: Callable, color: String = "orange", font_size: int = 28) -> Button:
	var result: Button = Button.new()
	result.text = text
	result.custom_minimum_size = Vector2(120.0, UiMetrics.TOUCH)
	result.add_theme_font_size_override("font_size", font_size)
	KalyeahTheme.style_button(result, color)
	result.pressed.connect(action)
	return result


## Adds platform safe-area insets to design margins; browser viewports already exclude chrome.
static func apply_safe_area(margin: MarginContainer, horizontal: float, vertical: float) -> void:
	var inset: Vector4 = Vector4.ZERO
	var window: Vector2i = DisplayServer.window_get_size()
	var safe: Rect2i = DisplayServer.get_display_safe_area()
	if not OS.has_feature("web") and safe.size.x > 0 and safe.size.y > 0 and window.x > 0 and window.y > 0:
		var scale: Vector2 = margin.get_viewport().get_visible_rect().size / Vector2(window)
		var origin: Vector2i = DisplayServer.window_get_position()
		inset = Vector4(maxf(0.0, safe.position.x - origin.x) * scale.x,
			maxf(0.0, safe.position.y - origin.y) * scale.y,
			maxf(0.0, origin.x + window.x - safe.end.x) * scale.x,
			maxf(0.0, origin.y + window.y - safe.end.y) * scale.y)
	margin.add_theme_constant_override("margin_left", int(horizontal + minf(inset.x, 120.0)))
	margin.add_theme_constant_override("margin_right", int(horizontal + minf(inset.z, 120.0)))
	margin.add_theme_constant_override("margin_top", int(vertical + minf(inset.y, 120.0)))
	margin.add_theme_constant_override("margin_bottom", int(vertical + minf(inset.w, 120.0)))
