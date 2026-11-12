class_name MatchHudLayout
extends RefCounted
## Match HUD screen-space layout controller:
## - Portrait & street mini-games: preserves bottom/side card tray, top header, and street score column.
## - Landscape 1280x720 Deadlock-inspired (`image_1`) combat layout:
##   - Top-left: `BarangayMinimap` (`152x152`) with compact `34px` white-outline `settings_button` at its bottom-right.
##   - Top-center: Airy 5 Ally portrait pips + `Scoreboard` & `ClockChip` + 5 Enemy portrait pips.
##   - Top-right: `QuickChat` + airy translucent `TakedownFeed` pills.
##   - Waist-left: Tapered angled vertical `HeroVitalsPanel` (`x = 212..300`, `y = 430..628` on `1280x720`), cleanly right of `MoveStick`.
##   - Bottom-right: Minimalist white-outline `AbilityArc` (`Tirador`, 3 superpowers, `Bagsak`, `Vault`, `Jump`, `Crouch/Slide`).


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
	else:
		if hud.header != null:
			hud.header.modulate.a = 1.0
		if hud.hand != null:
			hud._place(hud.hand, Vector4(0, 1, 1, 1), Vector4(0, -hud.console_height(), 0, -hud.bottom_inset))
			hud.hand.set_mirrored(Settings.left_handed)
			hud.sync_plate_column()
	if hud.street_pad != null and not hud.landscape:
		hud._place(hud.street_pad, Vector4(0, 1, 1, 1), Vector4(0, -hud.console_height(), 0, -hud.bottom_inset))
	if hud.score_column != null and not hud.landscape:
		hud._place(hud.score_column, Vector4(0, 0, 0, 0), Vector4(4.0, hud.header_height() + MatchHud.SCORE_COLUMN_GAP, 4.0 + ScoreColumn.WIDTH, hud.header_height() + MatchHud.SCORE_COLUMN_GAP + ScoreColumn.ROW_HEIGHT * 8.0))
	if hud.chat != null:
		_place_chat(hud)


## Quick chat button stands at top-right of every match on the header row.
static func _place_chat(hud: MatchHud) -> void:
	var right: float = -(hud.side_insets.y + MatchHud.MARGIN) if hud.landscape else -MatchHud.MARGIN
	var top: float = hud.top_inset + MatchHud.TOP_ROW_Y
	hud._place(hud.chat, Vector4(1, 0, 1, 0), Vector4(right - MatchHud.STREET_CHAT_SIZE, top, right, top + MatchHud.STREET_CHAT_SIZE))
	var panel: Vector2 = QuickChat.panel_size()
	var margin_r: float = (hud.side_insets.y + MatchHud.MARGIN) if hud.landscape else MatchHud.MARGIN
	hud.chat.place_panel(Rect2(Vector2(hud.size.x - margin_r - panel.x, top + MatchHud.STREET_CHAT_SIZE + 8.0), panel))


static func _apply_landscape(hud: MatchHud) -> void:
	hud.console.visible = false
	if hud.header != null:
		hud.header.modulate.a = 0.50
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
		var map_left: float = hud.side_insets.x + 16.0
		var map_top: float = hud.top_inset + 12.0
		hud._place(hud.minimap, Vector4(0, 0, 0, 0), Vector4(map_left, map_top, map_left + BarangayMinimap.SIZE.x, map_top + BarangayMinimap.SIZE.y))
	if hud.takedown_feed != null:
		var feed_right: float = -(hud.side_insets.y + MatchHud.MARGIN)
		var feed_top: float = hud.top_inset + MatchHud.TOP_ROW_Y + MatchHud.STREET_CHAT_SIZE + 6.0
		hud._place(hud.takedown_feed, Vector4(1, 0, 1, 0), Vector4(feed_right - TakedownFeed.WIDTH, feed_top, feed_right, feed_top + TakedownFeed.HEIGHT))
	if hud.vitals_panel != null:
		var vitals_left: float = hud.side_insets.x + 212.0
		var vitals_bottom: float = -(hud.bottom_inset + 92.0)
		hud._place(hud.vitals_panel, Vector4(0, 1, 0, 1), Vector4(vitals_left, vitals_bottom - HeroVitalsPanel.SIZE.y, vitals_left + HeroVitalsPanel.SIZE.x, vitals_bottom))


## Tumbang Preso's and Langit Lupa's points in landscape: row of cards around the top-center clock.
static func _place_score_row(hud: MatchHud) -> float:
	var clock: float = Scoreboard.middle_rect(MatchHud.SCORE_SIZE, MatchHud.CLOCK_GAP).size.x
	hud.score_column.set_row(clock + MatchHud.SCORE_ROW_GAP * 2.0)
	var width: float = hud.score_column.row_width()
	var top: float = hud.top_inset + MatchHud.TOP_ROW_Y
	hud._place(hud.score_column, Vector4(0.5, 0, 0.5, 0), Vector4(-width / 2.0, top, width / 2.0, top + ScoreColumn.ROW_HEIGHT))
	hud.score_column.pivot_offset = Vector2(width / 2.0, 0.0)
	hud.score_column.scale = Vector2.ONE * HudStyle.HEADER_SCALE
	return top + ScoreColumn.ROW_HEIGHT * HudStyle.HEADER_SCALE
