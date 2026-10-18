class_name HotspotDialogs
extends RefCounted
## The two dialogs of the main screen's Hotspot mode:
##   Create  a dialog that checks the hotspot is on (a short loading moment), then goes to the room
##   Join    eight boxes for the room code, JOIN and CANCEL
## They only build and report; TitleScreen decides what to do next.

enum State { ON, OFF, UNSUPPORTED }

## How long the checking dialog shows its loading state before it answers (s).
const CHECK_SECONDS: float = 0.9


## What the phone can do for a hotspot room: it needs the Android app, and the hotspot (or Wi-Fi)
## switched on so the phone has a network address.
static func state(can_host: bool, addresses: PackedStringArray) -> State:
	if not can_host:
		return State.UNSUPPORTED
	if HotspotNet.local_ip(addresses).is_empty():
		return State.OFF
	return State.ON


## The Create dialog. It shows "checking", then (after CHECK_SECONDS) either calls `on_ready` (the
## hotspot is on: go to the room) or says what is wrong. `on_preview` (web builds only) opens the
## room screen with made-up players so it can be looked at without a phone that can host.
## `can_host` and `addresses` stand in for the phone in tests (addresses empty = look them up).
static func create_check(on_ready: Callable, on_preview: Callable, can_host: bool = Net.can_host(), addresses: PackedStringArray = PackedStringArray()) -> MenuDialog:
	var card: MenuDialog = MenuDialog.create("HOTSPOT", 600.0)
	card.name = "HotspotCheckDialog"
	var message: Label = card.add_label("Checking your hotspot...", 30, KalyeahTheme.CREAM)
	message.name = "Message"
	message.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	var dots: Label = card.add_label(".", 56)
	dots.name = "Dots"
	var buttons: HBoxContainer = HBoxContainer.new()
	buttons.name = "Buttons"
	buttons.add_theme_constant_override("separation", 16)
	card.body.add_child(buttons)
	var cancel: Button = MenuDialog.make_button("CANCEL", "grey", 30)
	cancel.name = "Cancel"
	cancel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	cancel.pressed.connect(card.close)
	buttons.add_child(cancel)
	var checking: Callable = _start_check.bind(card, message, dots, buttons, cancel, on_ready, on_preview, can_host, addresses)
	card.opened.connect(checking)
	return card


static func _start_check(card: MenuDialog, message: Label, dots: Label, buttons: HBoxContainer, cancel: Button, on_ready: Callable, on_preview: Callable, can_host: bool, addresses: PackedStringArray) -> void:
	message.text = "Checking your hotspot..."
	dots.visible = true
	for extra: Node in buttons.get_children():
		if extra != cancel:
			buttons.remove_child(extra)
			extra.queue_free()
	var timer: SceneTreeTimer = card.get_tree().create_timer(CHECK_SECONDS)
	var answer: Callable = _answer.bind(card, message, dots, buttons, on_ready, on_preview, can_host, addresses)
	timer.timeout.connect(answer)


static func _answer(card: MenuDialog, message: Label, dots: Label, buttons: HBoxContainer, on_ready: Callable, on_preview: Callable, can_host: bool, addresses: PackedStringArray) -> void:
	if not is_instance_valid(card) or not card.is_open:
		return
	var local: PackedStringArray = addresses if not addresses.is_empty() else IP.get_local_addresses()
	var now: State = state(can_host, local)
	dots.visible = false
	if now == State.ON:
		message.text = "Your hotspot is on."
		card.close(false)
		on_ready.call()
	elif now == State.OFF:
		message.text = "Your hotspot is off. Turn it on in your phone's settings, then try again."
		var again: Button = MenuDialog.make_button("TRY AGAIN", "blue", 30)
		again.name = "TryAgain"
		again.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		again.pressed.connect(_start_check.bind(card, message, dots, buttons, buttons.get_node("Cancel") as Button, on_ready, on_preview, can_host, addresses))
		buttons.add_child(again)
	else:
		message.text = "Hotspot rooms need the Android app."
		if OS.has_feature("web"):
			var preview: Button = MenuDialog.make_button("PREVIEW ROOM", "blue", 30)
			preview.name = "Preview"
			preview.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			preview.pressed.connect(_preview.bind(card, on_preview))
			buttons.add_child(preview)


static func _preview(card: MenuDialog, on_preview: Callable) -> void:
	card.close(false)
	on_preview.call()


## The Join dialog: eight boxes for the code, JOIN (once all eight are filled) and CANCEL. `on_join`
## gets the code.
static func join(on_join: Callable) -> MenuDialog:
	var card: MenuDialog = MenuDialog.create("JOIN ROOM", 640.0)
	card.name = "HotspotJoinDialog"
	card.add_hint("Type the %d letters and numbers the host sees on their screen." % HotspotCodeInput.NET_RULES.passcode_length)
	var input: HotspotCodeInput = HotspotCodeInput.new()
	card.body.add_child(input)
	var cancel: Button = MenuDialog.make_button("CANCEL", "grey", 32)
	cancel.name = "Cancel"
	var go: Button = MenuDialog.make_button("JOIN", "orange", 32)
	go.name = "Join"
	go.disabled = true
	cancel.pressed.connect(card.close)
	go.pressed.connect(func() -> void:
		var code: String = input.code()
		card.close(false)
		on_join.call(code))
	input.changed.connect(func(complete: bool) -> void: go.disabled = not complete)
	card.add_buttons([cancel, go])
	card.opened.connect(input.focus_first)
	card.set_meta("input", input)
	return card
