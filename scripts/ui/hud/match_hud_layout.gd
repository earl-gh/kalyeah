class_name MatchHudLayout
extends RefCounted
## Where the match controls sit: the tray along the bottom (grown by the home bar) and the hand on it, above the home bar, with
## NEXT at the left (at the right for a left-handed player). Split out of MatchHud so the HUD keeps to what it shows.


static func apply(hud: MatchHud) -> void:
	MatchHudTop.layout(hud)
	hud._place(hud.console, Vector4(0, 1, 1, 1), Vector4(0, -hud.console_height(), 0, 0))
	if hud.feedback != null:
		hud.feedback.set_console_height(hud.console_height())
	if hud.tagged_panel != null:
		hud.tagged_panel.set_console_height(hud.console_height())
	hud.console_height_changed.emit(hud.console_height())
	if hud.hand != null:
		# the hand rides above the home bar: a thumb resting on the bar never lands on a card
		hud._place(hud.hand, Vector4(0, 1, 1, 1), Vector4(0, -hud.console_height(), 0, -hud.bottom_inset))
		hud.hand.set_mirrored(Settings.left_handed)
	if hud.chat != null:
		# the chat button rides above NEXT, outside the tray, on the same side as NEXT
		var at: Vector2 = hud.hand.chat_rect.position
		var top: float = -hud.console_height() + at.y
		hud._place(hud.chat, Vector4(0, 1, 0, 1), Vector4(at.x, top, at.x + hud.hand.chat_rect.size.x, top + hud.hand.chat_rect.size.y))
