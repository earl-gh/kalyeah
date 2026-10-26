class_name HotspotDialogs
extends RefCounted
## Network checks only. Hotspot room discovery never asks for a code.

enum State { ON, OFF }
const CHECK_SECONDS: float = 0.01


static func state(addresses: PackedStringArray) -> State:
	return State.OFF if HotspotNet.local_ip(addresses).is_empty() else State.ON


static func create_check(on_ready: Callable, addresses: PackedStringArray = PackedStringArray()) -> MenuDialog:
	var card: MenuDialog = MenuDialog.create("Hotspot")
	card.name = "HotspotCheckDialog"
	var message: Label = card.add_label("Checking local connection…", UiMetrics.BODY, KalyeahTheme.CREAM)
	message.name = "Message"
	var cancel: Button = MenuDialog.make_button("Cancel", "grey", UiMetrics.BODY)
	cancel.name = "Cancel"
	cancel.pressed.connect(card.close)
	var buttons: HBoxContainer = card.add_buttons([cancel])
	# A child timer dies with the dialog: no callback can retain freed controls.
	var timer: Timer = Timer.new()
	timer.one_shot = true
	timer.wait_time = CHECK_SECONDS
	card.add_child(timer)
	var check: Callable = func() -> void:
		if not card.is_open:
			return
		var local: PackedStringArray = addresses if not addresses.is_empty() else IP.get_local_addresses()
		match state(local):
			State.ON:
				card.close(false)
				on_ready.call()
			State.OFF:
				message.text = "No local network. Turn on your hotspot or connect to Wi-Fi, then retry."
				if buttons.get_node_or_null("TryAgain") == null:
					var again: Button = MenuDialog.make_button("Try again", "blue", UiMetrics.BODY)
					again.name = "TryAgain"
					again.pressed.connect(timer.start)
					buttons.add_child(again)
	timer.timeout.connect(check)
	card.opened.connect(timer.start)
	return card
