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
	if hud.chat != null:
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
