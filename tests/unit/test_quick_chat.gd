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


func test_the_chat_button_sits_above_next_in_the_column_beside_the_cardboard() -> void:
	var hud: MatchHud = _hud()
	var tray_top: float = hud.hand.get_global_rect().position.y
	var chat: Rect2 = hud.chat.get_global_rect()
	assert_lt(chat.end.y, hud.hand.next_rect.position.y + tray_top, "above NEXT")
	assert_lt(chat.end.x, hud.hand.card_rects[0].position.x, "beside the cardboard, not above it")
	assert_gte(minf(chat.size.x, chat.size.y), UiMetrics.MIN_TARGET, "thumb-sized")
	var art: AtlasTexture = hud.chat.button.texture_normal as AtlasTexture
	assert_eq(art.atlas, HandArt.CHAT, "the owner's speech bubble")
	assert_gt(art.margin.size.x, 0.0, "drawn a little smaller than its button")


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
	assert_gt(HandArt.plate().get_size().x / HandArt.plate().get_size().y, 2.0, "a wide cardboard plate")
	assert_almost_eq(HandArt.ice().get_size().x / HandArt.ice().get_size().y, HandArt.ICE_ASPECT, 0.03, "the ice water bag keeps its shape")
	assert_almost_eq(HandArt.chat().get_size().x / HandArt.chat().get_size().y, HandArt.CHAT_ASPECT, 0.03)
	assert_false(FileAccess.get_file_as_string("res://scripts/ui/hud/card_style.gd").contains("draw_drop"), "the drop is gone: the bag stands for ice water")
