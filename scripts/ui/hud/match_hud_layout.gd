class_name MatchHudLayout
extends RefCounted
## Where the match controls sit: the D-pad at the bottom left and the two skill buttons at the bottom right (weapon 2
## a little higher than weapon 1), all in sockets on the console shell. Mirrored for a left-handed player, scaled by the control size
## setting and lifted by the bottom inset (the home bar). Split out of MatchHud so the HUD keeps to what it shows.


static func apply(hud: MatchHud) -> void:
	var scale_by: float = Settings.control_scale
	var mirrored: bool = Settings.left_handed
	MatchHudTop.layout(hud)
	hud._place(hud.console, Vector4(0, 1, 1, 1), Vector4(0, -hud.console_height(), 0, 0))
	if hud.feedback != null:
		hud.feedback.set_console_height(hud.console_height())
	if hud.tagged_panel != null:
		hud.tagged_panel.set_console_height(hud.console_height())
	hud.console_height_changed.emit(hud.console_height())
	var lift: float = hud.bottom_inset
	if hud.card_mode:
		_apply_cards(hud, mirrored, lift)
		return
	var weapon: float = MatchHud.WEAPON_SIZE * scale_by
	var gap: float = MatchHud.WEAPON_GAP * scale_by
	var stagger: float = MatchHud.WEAPON_STAGGER * scale_by
	var zone: float = MatchHud.DPAD_ZONE
	# the pad keeps its zone; scaling it around its middle makes the disc and the grab area grow together
	put(hud, hud.dpad, mirrored, Vector2(MatchHud.DPAD_MARGIN, MatchHud.ROW_HEIGHT + lift), Vector2(zone, zone))
	hud.dpad.pivot_offset = Vector2(zone, zone) / 2.0
	hud.dpad.scale = Vector2.ONE * scale_by
	var second: float = MatchHud.WEAPON_MARGIN + weapon / 2.0
	var first: float = second + weapon + gap
	put(hud, hud.weapon_buttons[1], not mirrored, Vector2(second, MatchHud.ROW_HEIGHT + stagger + lift), Vector2(weapon, weapon))
	put(hud, hud.weapon_buttons[0], not mirrored, Vector2(first, MatchHud.ROW_HEIGHT - stagger + lift), Vector2(weapon, weapon))
	put(hud, hud.cancel_zone, not mirrored, Vector2((second + first) / 2.0, MatchHud.CANCEL_AT.y + lift), MatchHud.CANCEL_SIZE)
	# the sockets in the shell, in the panel's own coordinates (from its top-left)
	var width: float = hud.size.x
	var across: float = 1.0 if not mirrored else -1.0
	var pad_x: float = MatchHud.DPAD_MARGIN if not mirrored else width - MatchHud.DPAD_MARGIN
	var weapon_x: Callable = func(from_edge: float) -> float: return width - from_edge if not mirrored else from_edge
	var row: float = MatchHud.CONSOLE_HEIGHT - MatchHud.ROW_HEIGHT
	var sockets: Array[Dictionary] = [
		{"at": Vector2(pad_x, row), "radius": MatchHud.DPAD_SOCKET * scale_by},
		{"at": Vector2(weapon_x.call(second), row - stagger), "radius": MatchHud.WEAPON_SOCKET * scale_by},
		{"at": Vector2(weapon_x.call(first), row + stagger), "radius": MatchHud.WEAPON_SOCKET * scale_by},
	]
	hud.console.set_sockets(sockets, across > 0.0)


## Places `control` with its centre `at` = (from the right or left edge, from the bottom).
static func put(hud: MatchHud, control: Control, from_right: bool, at: Vector2, extent: Vector2) -> void:
	var edge: float = 1.0 if from_right else 0.0
	var side: float = -1.0 if from_right else 1.0
	var near: float = side * at.x
	var half: float = extent.x / 2.0
	var low: float = near - half
	var high: float = near + half
	hud._place(control, Vector4(edge, 1, edge, 1), Vector4(
		minf(low, high), -at.y - extent.y / 2.0, maxf(low, high), -at.y + extent.y / 2.0))


## Cards mode: the two hand cards at the right, the shuffler's window to their left of the D-pad, the health bar along the bottom (all
## mirrored for a left-handed player). Every x is a distance from the control's own screen edge; the card row is `CARD_TOP` below the tray's top.
static func _apply_cards(hud: MatchHud, mirrored: bool, lift: float) -> void:
	var zone: float = MatchHud.DPAD_ZONE
	var scale_by: float = Settings.control_scale
	# the pad sits above the health bar and a little smaller than on the console
	put(hud, hud.dpad, mirrored, Vector2(MatchHud.DPAD_MARGIN, MatchHud.ROW_HEIGHT + MatchHud.CARD_PAD_LIFT + lift), Vector2(zone, zone))
	hud.dpad.pivot_offset = Vector2(zone, zone) / 2.0
	hud.dpad.scale = Vector2.ONE * scale_by * MatchHud.CARD_PAD_SCALE
	var card: Vector2 = MatchHud.CARD_SIZE
	var from_bottom: float = MatchHud.CONSOLE_HEIGHT - MatchHud.CARD_TOP - card.y / 2.0 + lift
	var second: float = MatchHud.CARD_MARGIN + card.x / 2.0
	var first: float = second + card.x + MatchHud.CARD_GAP
	put(hud, hud.weapon_buttons[1], not mirrored, Vector2(second, from_bottom), card)
	put(hud, hud.weapon_buttons[0], not mirrored, Vector2(first, from_bottom), card)
	var window: Vector2 = MatchHud.SHUFFLER_SIZE
	var shuffler_x: float = first + card.x / 2.0 + MatchHud.CARD_GAP + window.x / 2.0
	put(hud, hud.shuffler, not mirrored, Vector2(shuffler_x, from_bottom + (card.y - window.y) / 2.0 - MatchHud.SHUFFLER_LIFT), window)
	put(hud, hud.cancel_zone, not mirrored, Vector2((second + first) / 2.0, MatchHud.CANCEL_AT.y + lift), MatchHud.CANCEL_SIZE)
	# the same places in the tray's own coordinates (from its top-left)
	var width: float = hud.size.x
	var tray_x: Callable = func(from_edge: float, extent: float) -> float: return width - from_edge - extent / 2.0 if not mirrored else from_edge - extent / 2.0
	var top: float = MatchHud.CARD_TOP
	var hands: Array[Rect2] = [
		Rect2(Vector2(tray_x.call(first + card.x / 2.0, card.x), top), card),
		Rect2(Vector2(tray_x.call(second + card.x / 2.0, card.x), top), card),
	]
	var shuffler_top: float = top + (card.y - window.y) / 2.0 + MatchHud.SHUFFLER_LIFT
	var shuffler_rect: Rect2 = Rect2(Vector2(tray_x.call(shuffler_x + window.x / 2.0, window.x), shuffler_top), window)
	var bar_top: float = MatchHud.CONSOLE_HEIGHT - MatchHud.BAR_BOTTOM - MatchHud.BAR_HEIGHT
	var bar: Rect2 = Rect2(Vector2(MatchHud.BAR_MARGIN, bar_top), Vector2(width - MatchHud.BAR_MARGIN * 2.0, MatchHud.BAR_HEIGHT))
	hud.console.set_card_layout(hands, shuffler_rect, bar)
