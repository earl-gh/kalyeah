class_name MatchHudLayout
extends RefCounted
## Where the match controls sit: the tray along the bottom (grown by the home bar) and the hand on it, above the home bar, with
## NEXT at the left (at the right for a left-handed player). Split out of MatchHud so the HUD keeps to what it shows.


static func apply(hud: MatchHud) -> void:
	if hud.hand != null:
		hud.hand.set_sides(hud.sides_tray())
	MatchHudTop.layout(hud)
	hud._place(hud.console, Vector4(0, 1, 1, 1), Vector4(0, -hud.console_panel_height(), 0, 0))
	if hud.feedback != null:
		hud.feedback.set_console_height(hud.console_height())
	if hud.tagged_panel != null:
		hud.tagged_panel.set_console_height(hud.console_panel_height())
	hud.console_height_changed.emit(hud.console_height())
	hud.side_reserve_changed.emit(hud.side_reserve(), hud.side_reserve_left())
	if hud.hand != null:
		# the hand rides above the home bar: a thumb resting on the bar never lands on a card
		hud._place(hud.hand, Vector4(0, 1, 1, 1), Vector4(0, -hud.console_height(), 0, -hud.bottom_inset))
		hud.hand.set_mirrored(Settings.left_handed)
		hud.sync_plate_column()
	if hud.street_pad != null:
		hud._place(hud.street_pad, Vector4(0, 1, 1, 1), Vector4(0, -hud.console_height(), 0, -hud.bottom_inset))
	if hud.score_column != null:
		# Tumbang Preso's points: a column on the left sidewalk under the header
		hud._place(hud.score_column, Vector4(0, 0, 0, 0), Vector4(4.0, hud.header_height() + MatchHud.SCORE_COLUMN_GAP, 4.0 + ScoreColumn.WIDTH, hud.header_height() + MatchHud.SCORE_COLUMN_GAP + ScoreColumn.ROW_HEIGHT * 8.0))
	if hud.street_game and hud.chat != null:
		_place_street_chat(hud)
	elif hud.chat != null:
		# the chat button rides above NEXT, outside the tray, on the same side as NEXT (Sides: in the strip, `chat_rect` says where)
		var at: Vector2 = hud.hand.chat_rect.position
		var top: float = -hud.console_height() + at.y
		hud._place(hud.chat, Vector4(0, 1, 0, 1), Vector4(at.x, top, at.x + hud.hand.chat_rect.size.x, top + hud.hand.chat_rect.size.y))

		if hud.sides_tray():
			# over the street, beside the column of cards, above the strip
			var from_x: float = CardHand.SIDES_COLUMN + 10.0 if Settings.left_handed else 10.0
			hud.chat.place_over_hand(Rect2(Vector2(from_x, hud.size.y - hud.console_height() - 280.0), Vector2(hud.size.x - CardHand.SIDES_COLUMN - 20.0, 270.0)))
		else:
			hud.chat.place_over_hand(Rect2(Vector2(hud.hand.card_rects[0].position.x, hud.size.y - hud.console_height() + 12.0), Vector2(hud.size.x - CardHand.COLUMN - CardHand.SIDE, hud.CONSOLE_HEIGHT - 24.0)))


## In the street games the quick chat button stands under the clock (top right) and opens a vertical list of phrases under itself.
static func _place_street_chat(hud: MatchHud) -> void:
	var right: float = -MatchHud.MARGIN
	var top: float = MatchHud.CLOCK_SIZE.y + 8.0
	hud._place(hud.chat, Vector4(1, 0, 1, 0), Vector4(right - MatchHud.STREET_CHAT_SIZE, top, right, top + MatchHud.STREET_CHAT_SIZE))
	var list: Rect2 = Rect2(Vector2(hud.size.x - MatchHud.MARGIN - MatchHud.STREET_CHAT_WIDTH, top + MatchHud.STREET_CHAT_SIZE + 8.0),
		Vector2(MatchHud.STREET_CHAT_WIDTH, float(QuickChat.PHRASES.size()) * (UiMetrics.TOUCH + UiMetrics.COMPACT_GAP)))
	hud.chat.place_over_hand(list)
