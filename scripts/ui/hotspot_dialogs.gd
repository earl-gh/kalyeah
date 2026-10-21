class_name HotspotDialogs
extends RefCounted
## Platform/network checks only. Hotspot room discovery never asks for a code.

enum State { ON, OFF, UNSUPPORTED }
const CHECK_SECONDS: float = 0.01


static func state(can_host: bool, addresses: PackedStringArray) -> State:
	if not can_host:
		return State.UNSUPPORTED
	return State.OFF if HotspotNet.local_ip(addresses).is_empty() else State.ON


static func create_check(on_ready: Callable, on_preview: Callable, can_host: bool = Net.can_host(), addresses: PackedStringArray = PackedStringArray()) -> MenuDialog:
	var card: MenuDialog = MenuDialog.create("Hotspot")
	card.name = "HotspotCheckDialog"
	var message: Label = card.add_label("Checking local connection…", 30, KalyeahTheme.CREAM)
	message.name = "Message"
	var cancel: Button = MenuDialog.make_button("Cancel", "grey", 30)
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
		match state(can_host, local):
			State.ON:
				card.close(false)
				on_ready.call()
			State.OFF:
				message.text = "No local network. Turn on your hotspot or connect to Wi-Fi, then retry."
				if buttons.get_node_or_null("TryAgain") == null:
					var again: Button = MenuDialog.make_button("Try again", "blue", 30)
					again.name = "TryAgain"
					again.pressed.connect(timer.start)
					buttons.add_child(again)
			State.UNSUPPORTED:
				message.text = "Hotspot rooms need the Android app."
				if OS.has_feature("web"):
					var preview: Button = MenuDialog.make_button("Preview room", "blue", 30)
					preview.name = "Preview"
					preview.pressed.connect(func() -> void:
						card.close(false)
						on_preview.call())
					buttons.add_child(preview)
	timer.timeout.connect(check)
	card.opened.connect(timer.start)
	return card


## Android proceeds immediately to the cancellable discovery screen. Web explains why it cannot.
static func join(on_join: Callable, supported: bool = Net.can_host()) -> MenuDialog:
	var card: MenuDialog = MenuDialog.create("Join hotspot")
	card.name = "HotspotJoinDialog"
	card.add_label("Finding the host's room…" if supported else "Hotspot rooms need the Android app.", 30)
	var cancel: Button = MenuDialog.make_button("Back", "grey", 30)
	cancel.name = "Cancel"
	cancel.pressed.connect(card.close)
	card.add_buttons([cancel])
	if supported:
		card.opened.connect(func() -> void:
			card.close(false)
			on_join.call(""))
	return card
