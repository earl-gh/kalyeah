class_name MatchHudTop
extends RefCounted


static func build(hud: MatchHud) -> void:
	hud.top = Control.new()
	hud._place(hud.top, Vector4(0, 0, 1, 0), Vector4(0, 0, 0, 520))
	hud.top.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud.add_child(hud.top)
	# left: the tall street minimap, then the settings button beside it (top aligned)
	hud.minimap = LaneMinimap.new()
	hud._place(hud.minimap, Vector4(0, 0, 0, 0), Vector4(MatchHud.MARGIN, MatchHud.TOP_ROW_Y, MatchHud.MARGIN + MatchHud.MINIMAP_SIZE.x, MatchHud.TOP_ROW_Y + MatchHud.MINIMAP_SIZE.y))
	hud.minimap.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud.top.add_child(hud.minimap)
	hud.settings_button = TextureButton.new()
	hud.settings_button.texture_normal = Icons.art(&"gear")
	hud.settings_button.ignore_texture_size = true
	hud.settings_button.stretch_mode = TextureButton.STRETCH_KEEP_ASPECT_CENTERED
	var gear_x: float = MatchHud.MARGIN * 1.6 + MatchHud.MINIMAP_SIZE.x
	hud._place(hud.settings_button, Vector4(0, 0, 0, 0), Vector4(gear_x, MatchHud.TOP_ROW_Y, gear_x + MatchHud.GEAR_SIZE, MatchHud.TOP_ROW_Y + MatchHud.GEAR_SIZE))
	hud.settings_button.pressed.connect(hud.toggle_menu)
	hud.top.add_child(hud.settings_button)
	# under the minimap: the TAGGED!/RESCUED! feed
	hud.feed_box = VBoxContainer.new()
	var feed_top: float = MatchHud.TOP_ROW_Y + MatchHud.MINIMAP_SIZE.y + 12.0
	hud._place(hud.feed_box, Vector4(0, 0, 0, 0), Vector4(MatchHud.MARGIN, feed_top, MatchHud.MARGIN + MatchHud.FEED_SIZE.x, feed_top + MatchHud.FEED_SIZE.y * MatchHud.FEED_MAX + 12.0))
	hud.feed_box.add_theme_constant_override("separation", 6)
	hud.feed_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud.top.add_child(hud.feed_box)
	# right: the score card (points and the match clock)
	hud.scoreboard = Scoreboard.new()
	hud._place(hud.scoreboard, Vector4(1, 0, 1, 0), Vector4(-MatchHud.MARGIN - MatchHud.SCORE_SIZE.x, MatchHud.TOP_ROW_Y, -MatchHud.MARGIN, MatchHud.TOP_ROW_Y + MatchHud.SCORE_SIZE.y))
	hud.scoreboard.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud.top.add_child(hud.scoreboard)


## The opening show's banner: a dark band at the bottom with the goal in gold.
static func build_intro(hud: MatchHud) -> void:
	hud.intro_bar = ColorRect.new()
	(hud.intro_bar as ColorRect).color = Color(0.05, 0.04, 0.08, 0.72)
	hud._place(hud.intro_bar, Vector4(0, 1, 1, 1), Vector4(0, -260, 0, -120))
	hud.intro_bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud.intro_bar.visible = false
	hud.add_child(hud.intro_bar)
	hud.intro_title = Label.new()
	hud.intro_title.theme_type_variation = &"DisplayLabel"
	hud._place(hud.intro_title, Vector4(0, 0, 1, 0), Vector4(0, 18, 0, 78))
	hud.intro_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	hud.intro_title.add_theme_font_size_override("font_size", 40)
	hud.intro_title.add_theme_color_override("font_color", Color(1.0, 0.82, 0.25))
	hud.intro_title.add_theme_color_override("font_outline_color", Color(0.1, 0.05, 0.1))
	hud.intro_title.add_theme_constant_override("outline_size", 10)
	hud.intro_bar.add_child(hud.intro_title)
	hud.intro_sub = Label.new()
	hud._place(hud.intro_sub, Vector4(0, 0, 1, 0), Vector4(0, 80, 0, 120))
	hud.intro_sub.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	hud.intro_sub.add_theme_font_size_override("font_size", 26)
	hud.intro_sub.add_theme_color_override("font_outline_color", Color(0.1, 0.05, 0.1))
	hud.intro_sub.add_theme_constant_override("outline_size", 6)
	hud.intro_bar.add_child(hud.intro_sub)
