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
	if hud.waypoints != null:
		hud._place(hud.waypoints, Vector4(0, 0, 1, 1), Vector4.ZERO)
	if hud.landscape:
		_apply_landscape(hud)
	elif hud.hand != null:
		# the hand rides above the home bar: a thumb resting on the bar never lands on a card
		hud._place(hud.hand, Vector4(0, 1, 1, 1), Vector4(0, -hud.console_height(), 0, -hud.bottom_inset))
		hud.hand.set_mirrored(Settings.left_handed)
		hud.sync_plate_column()
	if hud.street_pad != null and not hud.landscape:
		hud._place(hud.street_pad, Vector4(0, 1, 1, 1), Vector4(0, -hud.console_height(), 0, -hud.bottom_inset))
	if hud.score_column != null and not hud.landscape:
		# Tumbang Preso's points: a column on the left sidewalk under the header
		hud._place(hud.score_column, Vector4(0, 0, 0, 0), Vector4(4.0, hud.header_height() + MatchHud.SCORE_COLUMN_GAP, 4.0 + ScoreColumn.WIDTH, hud.header_height() + MatchHud.SCORE_COLUMN_GAP + ScoreColumn.ROW_HEIGHT * 8.0))
	if hud.chat != null:
		_place_chat(hud)


## The quick chat button stands at the top right of every match, whatever the tray does (cards at the bottom or at the sides, left- or
## right-handed, or the street games' knob and button): on the header's row, its right edge on the screen's margin, under the notch.
static func _place_chat(hud: MatchHud) -> void:
	var right: float = -MatchHud.MARGIN
	var top: float = hud.top_inset + MatchHud.TOP_ROW_Y
	hud._place(hud.chat, Vector4(1, 0, 1, 0), Vector4(right - MatchHud.STREET_CHAT_SIZE, top, right, top + MatchHud.STREET_CHAT_SIZE))
	var panel: Vector2 = QuickChat.panel_size()
	hud.chat.place_panel(Rect2(Vector2(hud.size.x - MatchHud.MARGIN - panel.x, top + MatchHud.STREET_CHAT_SIZE + 8.0), panel))


## Landscape 1280x720 90s Filipino Komiks x Valorant layout:
## - Top-left: Settings gear + 156x156 BarangayMinimap
## - Top-center: 5 Ally Portrait Chips + Scoreboard & ClockChip + 5 Enemy Portrait Chips
## - Top-right: QuickChat + TakedownFeed slanted callout strips
## - Bottom-left: MoveStick + HeroVitalsPanel hero card
## - Bottom-right: AbilityArc (TIRADOR LMB, Q/E/R Superpowers, LUKSONG-TINIK SHIFT, BAGSAK F)
static func _apply_landscape(hud: MatchHud) -> void:
	hud.console.visible = false
	var pivot: Vector2 = Vector2(hud.top.size.x / 2.0, MatchHud.TOP_ROW_Y)
	for item: Control in [hud.scoreboard, hud.clock_chip]:
		item.pivot_offset = pivot - item.position
		item.scale = Vector2.ONE * HudStyle.HEADER_SCALE
	var header_bottom: float = hud.top_inset + MatchHud.TOP_ROW_Y + MatchHud.SCORE_SIZE.y * HudStyle.HEADER_SCALE
	if hud.score_column != null:
		header_bottom = maxf(header_bottom, _place_score_row(hud))
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
	hud.overhead.stamina_on_bar = hud.street_game
	if hud.hand is AbilityArc:
		hud._place(hud.hand, Vector4(0, 0, 1, 1), Vector4.ZERO)
		(hud.hand as AbilityArc).set_inset(Vector2(hud.side_insets.y, hud.bottom_inset))
	if hud.look != null:
		hud.look.zone_left = hud.size.x * MoveStick.ZONE_SHARE
	if hud.stick != null:
		hud._place(hud.stick, Vector4(0, 0, 1, 1), Vector4.ZERO)
		hud.stick.inset = Vector2(hud.side_insets.x, hud.bottom_inset)
		hud.stick.cancel()
	if hud.minimap != null:
		var map_left: float = MatchHud.MARGIN + MatchHud.GEAR_SIZE + 8.0
		var map_top: float = hud.top_inset + MatchHud.TOP_ROW_Y
		hud._place(hud.minimap, Vector4(0, 0, 0, 0), Vector4(map_left, map_top, map_left + BarangayMinimap.SIZE.x, map_top + BarangayMinimap.SIZE.y))
	if hud.takedown_feed != null:
		var feed_right: float = -(hud.side_insets.y + MatchHud.MARGIN)
		var feed_top: float = hud.top_inset + MatchHud.TOP_ROW_Y + MatchHud.STREET_CHAT_SIZE + 8.0
		hud._place(hud.takedown_feed, Vector4(1, 0, 1, 0), Vector4(feed_right - TakedownFeed.WIDTH, feed_top, feed_right, feed_top + TakedownFeed.HEIGHT))
	if hud.vitals_panel != null:
		var vitals_left: float = hud.side_insets.x + HudStyle.EDGE + MoveStick.BASE_RADIUS * 2.0 + 14.0
		var vitals_bottom: float = -(hud.bottom_inset + HudStyle.EDGE)
		hud._place(hud.vitals_panel, Vector4(0, 1, 0, 1), Vector4(vitals_left, vitals_bottom - HeroVitalsPanel.SIZE.y, vitals_left + HeroVitalsPanel.SIZE.x, vitals_bottom))


## Tumbang Preso's and Langit Lupa's points in the landscape match (owner, 2026-11-09): the kids' cards in a row at the top centre, half on
## each side of the clock (3, the clock, 3), at the header's scale round the middle of the header's row. Returns the row's bottom (UI pixels).
static func _place_score_row(hud: MatchHud) -> float:
	var clock: float = Scoreboard.middle_rect(MatchHud.SCORE_SIZE, MatchHud.CLOCK_GAP).size.x
	hud.score_column.set_row(clock + MatchHud.SCORE_ROW_GAP * 2.0)
	var width: float = hud.score_column.row_width()
	var top: float = hud.top_inset + MatchHud.TOP_ROW_Y
	hud._place(hud.score_column, Vector4(0.5, 0, 0.5, 0), Vector4(-width / 2.0, top, width / 2.0, top + ScoreColumn.ROW_HEIGHT))
	hud.score_column.pivot_offset = Vector2(width / 2.0, 0.0)
	hud.score_column.scale = Vector2.ONE * HudStyle.HEADER_SCALE
	return top + ScoreColumn.ROW_HEIGHT * HudStyle.HEADER_SCALE
