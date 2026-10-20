class_name DialogButtons
extends RefCounted
## Small painted controls for the menu: a round icon button and a chunky slider. Drawn in code
## (the grabber is a smooth anti-aliased disc), matching the painted buttons and panels.

const TRACK: Color = Color(0.05, 0.08, 0.2, 0.9)
const FILL: Color = KalyeahTheme.AMBER
const GRABBER_SIZE: int = 44


## A square painted button with an icon (`color`: orange, green, red, blue, grey or gold).
static func icon_button(icon: Texture2D, color: String, size: float) -> Button:
	var button: Button = MenuDialog.make_button("", color, 30, Vector2(size, size))
	button.icon = icon
	button.expand_icon = true
	button.icon_alignment = HORIZONTAL_ALIGNMENT_CENTER
	button.vertical_icon_alignment = VERTICAL_ALIGNMENT_CENTER
	button.add_theme_constant_override("icon_max_width", int(size * 0.62))
	return button


## A 0..1 slider with a dark rounded track, an orange fill and a round white grabber.
static func slider(value: float) -> HSlider:
	var slider: HSlider = HSlider.new()
	slider.min_value = 0.0
	slider.max_value = 1.0
	slider.step = 0.01
	slider.value = value
	slider.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	slider.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	slider.custom_minimum_size = Vector2(0.0, UiMetrics.TOUCH)
	var track: StyleBoxFlat = StyleBoxFlat.new()
	track.bg_color = TRACK
	track.set_corner_radius_all(14)
	track.content_margin_top = 4.0
	track.content_margin_bottom = 4.0
	track.anti_aliasing = true
	var fill: StyleBoxFlat = StyleBoxFlat.new()
	fill.bg_color = FILL
	fill.set_corner_radius_all(14)
	fill.content_margin_top = 4.0
	fill.content_margin_bottom = 4.0
	fill.anti_aliasing = true
	slider.add_theme_stylebox_override("slider", track)
	slider.add_theme_stylebox_override("grabber_area", fill)
	slider.add_theme_stylebox_override("grabber_area_highlight", fill)
	var grabber: Texture2D = grabber_texture()
	slider.add_theme_icon_override("grabber", grabber)
	slider.add_theme_icon_override("grabber_highlight", grabber)
	slider.add_theme_icon_override("grabber_disabled", grabber)
	return slider


## A smooth round white knob with a soft dark rim, drawn pixel by pixel with anti-aliased edges.
static func grabber_texture() -> Texture2D:
	var image: Image = Image.create(GRABBER_SIZE, GRABBER_SIZE, false, Image.FORMAT_RGBA8)
	var center: Vector2 = Vector2(GRABBER_SIZE, GRABBER_SIZE) / 2.0 - Vector2(0.5, 0.5)
	var radius: float = GRABBER_SIZE / 2.0 - 2.0
	for y: int in GRABBER_SIZE:
		for x: int in GRABBER_SIZE:
			var d: float = Vector2(x, y).distance_to(center)
			var edge: float = clampf(radius - d + 0.5, 0.0, 1.0)
			var rim: float = clampf((d - (radius - 5.0)) / 3.0, 0.0, 1.0)
			var face: Color = Color(1.0, 0.97, 0.9).lerp(Color(0.32, 0.18, 0.1), rim * 0.9)
			image.set_pixel(x, y, Color(face, edge))
	return ImageTexture.create_from_image(image)


## One leave confirmation for loadout and gameplay; neither pauses the authoritative match.
static func leave_match(hosting: bool, on_leave: Callable) -> MenuDialog:
	var dialog: MenuDialog = MenuDialog.create("Leave match?")
	dialog.add_label("The hotspot room will close for everyone." if hosting else "Your place will be released.")
	var cancel: Button = MenuDialog.make_button("Cancel", "grey", UiMetrics.ACTION)
	var leave: Button = MenuDialog.make_button("Leave", "red", UiMetrics.ACTION)
	cancel.pressed.connect(dialog.close)
	leave.pressed.connect(func() -> void:
		dialog.close(false)
		on_leave.call())
	dialog.add_buttons([cancel, leave])
	return dialog
