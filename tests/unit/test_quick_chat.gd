extends GutTest
## The quick chat button, the owner's pictures for the hand, and the ice water bag.


func _hud() -> MatchHud:
	var phone: Control = autofree(Control.new()) as Control
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var hud: MatchHud = MatchHud.new()
	phone.add_child(hud)
	hud.layout_controls()
	return hud


func test_the_chat_button_is_fixed_at_the_top_right_whatever_the_tray_does() -> void:
	var hud: MatchHud = _hud()
	for sides: bool in [false, true]:
		for left: bool in [false, true]:
			Settings.cards_tray = &"sides" if sides else &"bottom"
			Settings.left_handed = left
			hud.layout_controls()
			var chat: Rect2 = hud.chat.get_global_rect()
			assert_almost_eq(chat.end.x, hud.size.x - MatchHud.MARGIN, 1.0, "sides=%s left=%s: on the right margin" % [sides, left])
			assert_almost_eq(chat.position.y, hud.top_inset + MatchHud.TOP_ROW_Y, 1.0, "on the header's row, at the top")
			assert_gte(minf(chat.size.x, chat.size.y), UiMetrics.MIN_TARGET, "thumb-sized")
	Settings.cards_tray = &"bottom"
	Settings.left_handed = false
	var art: AtlasTexture = hud.chat.button.texture_normal as AtlasTexture
	assert_eq(art.atlas, HandArt.CHAT, "the owner's speech bubble")
	assert_gt(art.margin.size.x, 0.0, "drawn a little smaller than its button")


func test_the_panel_is_a_white_rounded_rectangle_at_the_top_right_with_six_stickers_in_two_columns() -> void:
	var hud: MatchHud = _hud()
	hud.chat.set_open(true)
	await wait_process_frames(3)
	var panel: PanelContainer = hud.chat.menu
	var box: StyleBoxFlat = panel.get_theme_stylebox("panel") as StyleBoxFlat
	assert_eq(box.bg_color, Color.WHITE, "white")
	assert_gt(box.corner_radius_top_left, 8, "rounded")
	assert_eq(hud.chat.grid.columns, 2, "two columns")
	assert_eq(hud.chat.phrase_buttons.size(), 6, "six stickers")
	var rect: Rect2 = panel.get_global_rect()
	assert_almost_eq(rect.end.x, hud.size.x - MatchHud.MARGIN, 1.0, "at the right edge")
	assert_gte(rect.position.y, hud.chat.get_global_rect().end.y, "under the button, at the top")
	assert_lt(rect.end.y, hud.size.y / 2.0, "in the top half: nothing to do with the tray")
	var xs: Array[float] = []
	var ys: Array[float] = []
	for pick: Button in hud.chat.phrase_buttons:
		var at: Vector2 = pick.get_global_rect().position
		if not xs.has(snappedf(at.x, 1.0)):
			xs.append(snappedf(at.x, 1.0))
		if not ys.has(snappedf(at.y, 1.0)):
			ys.append(snappedf(at.y, 1.0))
	assert_eq(xs.size(), 2, "2 columns")
	assert_eq(ys.size(), 3, "3 rows")
	for pick: Button in hud.chat.phrase_buttons:
		assert_true(rect.encloses(pick.get_global_rect()), "every sticker is inside the panel")
		assert_gte(minf(pick.size.x, pick.size.y), UiMetrics.MIN_TARGET, "a sticker is thumb-sized")


func test_every_sticker_has_a_face_and_its_words_and_the_owners_picture_would_replace_the_face() -> void:
	var hud: MatchHud = _hud()
	for pick: Button in hud.chat.phrase_buttons:
		assert_not_null(pick.find_child("Face", true, false), "%s: a code-drawn face" % pick.name)
		assert_false((pick.find_child("Words", true, false) as Label).text.is_empty())
	assert_eq(QuickChat.slug("Good luck!"), "good_luck", "assets/ui/stickers/good_luck.png would replace its face")


func test_a_tap_opens_the_phrases_and_picking_one_says_it_and_closes() -> void:
	var hud: MatchHud = _hud()
	var said: Array[String] = []
	hud.chat.said.connect(func(text: String) -> void: said.append(text))
	assert_false(hud.chat.is_open())
	hud.chat.button.pressed.emit()
	assert_true(hud.chat.is_open())
	assert_eq(hud.chat.phrase_buttons.size(), QuickChat.PHRASES.size())
	hud.chat.phrase_buttons[1].pressed.emit()
	assert_eq(said, [QuickChat.PHRASES[1]] as Array[String])
	assert_false(hud.chat.is_open(), "the list closes after a pick")


func test_a_phrase_shows_in_a_bubble_over_the_kid_and_fades_away() -> void:
	var hud: MatchHud = _hud()
	hud.overhead.say(1, "Wow!")
	assert_eq(hud.overhead.says_count(), 1)
	hud.overhead.say(1, "Oops!")
	assert_eq(hud.overhead.says_count(), 1, "a new one replaces that kid's old bubble")
	hud.overhead.tick(QuickChat.SAY_TIME + 0.1)
	assert_eq(hud.overhead.says_count(), 0, "it goes after its time")


func test_the_hand_uses_the_owners_pictures() -> void:
	assert_almost_eq(HandArt.ice().get_size().x / HandArt.ice().get_size().y, HandArt.ICE_ASPECT, 0.03, "the ice water bag keeps its shape")
	assert_lt(HandArt.ICE_DRAW_ASPECT, HandArt.ICE_ASPECT, "but is drawn a little narrower, it looked stretched sideways on the cards")
	assert_almost_eq(HandArt.chat().get_size().x / HandArt.chat().get_size().y, HandArt.CHAT_ASPECT, 0.03)
	assert_false(FileAccess.get_file_as_string("res://scripts/ui/hud/card_style.gd").contains("draw_drop"), "the drop is gone: the bag stands for ice water")


func test_the_sticker_panel_draws_over_the_toasts() -> void:
	var hud: MatchHud = _hud()
	assert_gt(hud.chat.get_index(), hud.tag_toast.get_index(), "over the tag toast, which stands under the scoreboard beside it")
	assert_gt(hud.chat.get_index(), hud.capture_alert.get_index())
