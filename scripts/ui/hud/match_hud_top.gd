class_name MatchHudTop
extends RefCounted


static func build(hud: MatchHud) -> void:
	hud.header = HeaderPanel.new()
	hud.header.set_header_height(MatchHud.HEADER_HEIGHT)
	hud.add_child(hud.header)
	hud.top = Control.new()
	hud._place(hud.top, Vector4(0, 0, 1, 0), Vector4(0, 0, 0, 520))
	hud.top.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud.add_child(hud.top)
	# left: the settings button (the whole street is always on screen: no minimap)
	hud.settings_button = TextureButton.new()
	hud.settings_button.texture_normal = Icons.art(&"gear")
	hud.settings_button.ignore_texture_size = true
	hud.settings_button.stretch_mode = TextureButton.STRETCH_KEEP_ASPECT_CENTERED
	hud.settings_button.tooltip_text = "Match settings"
	hud.settings_button.accessibility_name = "Match settings"
	hud.settings_button.pressed.connect(hud.toggle_menu)
	hud.top.add_child(hud.settings_button)
	# the TAGGED!/RESCUED! feed (placed in layout())
	hud.feed_box = VBoxContainer.new()
	hud.feed_box.add_theme_constant_override("separation", 6)
	hud.feed_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud.top.add_child(hud.feed_box)
	# the middle of the header: the score (points and the match clock)
	hud.scoreboard = Scoreboard.new()
	hud._place(hud.scoreboard, Vector4(0.5, 0, 0.5, 0), Vector4(-MatchHud.SCORE_SIZE.x / 2.0, MatchHud.TOP_ROW_Y, MatchHud.SCORE_SIZE.x / 2.0, MatchHud.TOP_ROW_Y + MatchHud.SCORE_SIZE.y))
	hud.scoreboard.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud.top.add_child(hud.scoreboard)
	# right: the match clock, the counterpart of the settings button
	hud.clock_chip = ClockChip.new()
	hud.top.add_child(hud.clock_chip)
	layout(hud)


## Places the settings button at the top left and the feed on the left, under the tagged box (which it must not cover).
static func layout(hud: MatchHud) -> void:
	if hud.intro_bar != null:
		hud._place(hud.intro_bar, Vector4(0, 1, 1, 1), Vector4(80, -(hud.console_height() + 150.0), -80, -(hud.console_height() + 6.0)))
	var gear: Vector4 = Vector4(MatchHud.MARGIN, MatchHud.TOP_ROW_Y, MatchHud.MARGIN + MatchHud.GEAR_SIZE, MatchHud.TOP_ROW_Y + MatchHud.GEAR_SIZE)
	hud._place(hud.settings_button, Vector4(0, 0, 0, 0), gear)
	place_feed(hud, 0.0)
	hud._place(hud.clock_chip, Vector4(1, 0, 1, 0), Vector4(-MatchHud.MARGIN - MatchHud.CLOCK_SIZE.x, MatchHud.TOP_ROW_Y, -MatchHud.MARGIN, MatchHud.TOP_ROW_Y + MatchHud.CLOCK_SIZE.y))


## The feed sits `FEED_DROP` under the header, or lower when your tagged box and its RESCUE marker reach further down the screen
## (`box_clear_y`, in screen space; a taller phone shows the box lower): a TAGGED! pill never covers the box it is about.
static func place_feed(hud: MatchHud, box_clear_y: float) -> void:
	# the feed lives in `top`, which the notch's inset moves down
	var feed_top: float = maxf(MatchHud.HEADER_HEIGHT + MatchHud.FEED_DROP, box_clear_y + MatchHud.MARGIN - hud.top.position.y)
	if hud.feed_box.position.y == feed_top:
		return
	hud._place(hud.feed_box, Vector4(0, 0, 0, 0), Vector4(MatchHud.MARGIN, feed_top, MatchHud.MARGIN + MatchHud.FEED_SIZE.x, feed_top + MatchHud.FEED_SIZE.y * MatchHud.FEED_MAX + 12.0))


## The opening objective appears as a compact floating sheet above the controls.
static func build_intro(hud: MatchHud) -> void:
	# no plate: the words stand on the street with an outline
	var panel: Control = Control.new()
	hud.intro_bar = panel
	hud._place(hud.intro_bar, Vector4(0, 1, 1, 1), Vector4(80, -(MatchHud.CONSOLE_HEIGHT + 150.0), -80, -(MatchHud.CONSOLE_HEIGHT + 6.0)))
	hud.intro_bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud.intro_bar.visible = false
	hud.add_child(hud.intro_bar)
	hud.intro_title = Label.new()
	hud._place(hud.intro_title, Vector4(0, 0, 1, 0), Vector4(0, 14, 0, 72))
	hud.intro_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	UiKit.letter(hud.intro_title, UiMetrics.HEADLINE, Color(1.0, 0.82, 0.25))
	hud.intro_bar.add_child(hud.intro_title)
	hud.intro_sub = Label.new()
	hud._place(hud.intro_sub, Vector4(0, 0, 1, 0), Vector4(0, 72, 0, 112))
	hud.intro_sub.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	hud.intro_sub.add_theme_font_size_override("font_size", UiMetrics.ACTION)
	hud.intro_sub.add_theme_color_override("font_outline_color", Color(0.1, 0.05, 0.1))
	hud.intro_sub.add_theme_constant_override("outline_size", 6)
	hud.intro_bar.add_child(hud.intro_sub)
