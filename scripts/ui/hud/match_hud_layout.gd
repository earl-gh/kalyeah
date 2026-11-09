class_name MatchHudLayout
extends RefCounted
## Where the match controls sit: the tray along the bottom (grown by the home bar) and the hand on it, above the home bar, with
## NEXT at the left (at the right for a left-handed player). Split out of MatchHud so the HUD keeps to what it shows.


static func apply(hud: MatchHud) -> void:
	if hud.hand != null and not hud.landscape:
		hud.hand.set_sides(hud.sides_tray())
	MatchHudTop.layout(hud)
	hud._place(hud.console, Vector4(0, 1, 1, 1), Vector4(0, -hud.console_panel_height(), 0, 0))
	if hud.feedback != null:
		hud.feedback.set_console_height(hud.console_height())
	if hud.tagged_panel != null:
		hud.tagged_panel.set_console_height(hud.console_panel_height())
	hud.console_height_changed.emit(hud.console_height())
	hud.side_reserve_changed.emit(hud.side_reserve(), hud.side_reserve_left())
	if hud.landscape:
		_apply_landscape(hud)
	elif hud.hand != null:
		# the hand rides above the home bar: a thumb resting on the bar never lands on a card
		hud._place(hud.hand, Vector4(0, 1, 1, 1), Vector4(0, -hud.console_height(), 0, -hud.bottom_inset))
		hud.hand.set_mirrored(Settings.left_handed)
		hud.sync_plate_column()
	if hud.street_pad != null and not hud.landscape:
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


## The landscape match (Wild Rift style): no tray; the joystick takes the left of the screen and the ability arc the bottom-right corner, both
## over the whole screen so they lay themselves out from its size, clear of the notch (left or right) and the home bar.
static func _apply_landscape(hud: MatchHud) -> void:
	hud.console.visible = false
	# the header is 80% of its portrait size: the screen is 720 px tall here (docs/audits/2026-11-09-landscape-hud.md); the score and the clock
	# shrink round the middle of the header's row, the tag toast round the middle of its own top edge
	var pivot: Vector2 = Vector2(hud.top.size.x / 2.0, MatchHud.TOP_ROW_Y)
	for item: Control in [hud.scoreboard, hud.clock_chip]:
		item.pivot_offset = pivot - item.position
		item.scale = Vector2.ONE * HudStyle.HEADER_SCALE
	var header_bottom: float = hud.top_inset + MatchHud.TOP_ROW_Y + MatchHud.SCORE_SIZE.y * HudStyle.HEADER_SCALE
	# the role lines and the hints stand right under the score, over the sky and the roofs: off your kid (the lower middle) and off the far end
	# of the street, where the far kids' tags are (round 2 of the audit); the tag toast pops in under them for its two seconds
	var words_top: float = header_bottom + HudStyle.WORDS_GAP
	var toast_top: float = words_top + HudStyle.WORDS_HEIGHT
	hud._place(hud.tag_toast, Vector4(0, 0, 1, 0), Vector4(0, toast_top, 0, toast_top + TagToast.HEIGHT))
	hud.tag_toast.pivot_offset = Vector2(hud.size.x / 2.0, 0.0)
	hud.tag_toast.scale = Vector2.ONE * HudStyle.HEADER_SCALE
	if hud.feedback != null:
		hud.feedback.landscape = true
		hud.feedback.set_top_line(words_top)
	if hud.street_pad != null:
		hud._place(hud.street_pad, Vector4(0, 0, 1, 1), Vector4.ZERO)
		hud.street_pad.set_landscape(true, Vector4(hud.side_insets.x, hud.top_inset, hud.side_insets.y, hud.bottom_inset), words_top)
	# your stamina is under the ice water bar at the bottom centre; the street games have no arc, so theirs stays under your bar
	hud.overhead.stamina_on_bar = hud.street_game
	if hud.hand is AbilityArc:
		hud._place(hud.hand, Vector4(0, 0, 1, 1), Vector4.ZERO)
		(hud.hand as AbilityArc).set_inset(Vector2(hud.side_insets.y, hud.bottom_inset))
	if hud.minimap != null:
		# top left, beside the settings button on the header's row (Tumbang Preso's points column stands under the header at the left), clear
		# of a notch on the left
		var at: Vector2 = Vector2(MatchHud.MARGIN + MatchHud.GEAR_SIZE + HudStyle.EDGE + hud.side_insets.x, hud.top_inset + MatchHud.TOP_ROW_Y)
		hud._place(hud.minimap, Vector4(0, 0, 0, 0), Vector4(at.x, at.y, at.x + StreetMinimap.SIZE.x, at.y + StreetMinimap.SIZE.y))
	if hud.look != null:
		hud.look.zone_left = hud.size.x * MoveStick.ZONE_SHARE
	if hud.stick != null:
		hud._place(hud.stick, Vector4(0, 0, 1, 1), Vector4.ZERO)
		hud.stick.inset = Vector2(hud.side_insets.x, hud.bottom_inset)
		hud.stick.cancel()
