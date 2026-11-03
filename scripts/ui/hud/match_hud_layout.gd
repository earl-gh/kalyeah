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
	if hud.chat != null:
		_place_chat(hud)


## The quick chat button stands at the top right of every match, whatever the tray does (cards at the bottom or at the sides, left- or
## right-handed, or the street games' knob and button): on the header's row, its right edge on the screen's margin, under the notch. The white
## sticker panel opens under it, also at the top right. `hud.top` is moved down by the notch's inset, so the button goes down with it.
static func _place_chat(hud: MatchHud) -> void:
	var right: float = -MatchHud.MARGIN
	var top: float = hud.top_inset + MatchHud.TOP_ROW_Y
	hud._place(hud.chat, Vector4(1, 0, 1, 0), Vector4(right - MatchHud.STREET_CHAT_SIZE, top, right, top + MatchHud.STREET_CHAT_SIZE))
	var panel: Vector2 = QuickChat.panel_size()
	hud.chat.place_panel(Rect2(Vector2(hud.size.x - MatchHud.MARGIN - panel.x, top + MatchHud.STREET_CHAT_SIZE + 8.0), panel))
