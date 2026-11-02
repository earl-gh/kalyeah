class_name HotspotDialogs
extends RefCounted
## The two modals in front of a hotspot room (the decisions are HotspotFlow's). Create checks the hotspot is on, and can start a
## game hotspot when there is none; Join checks the Wi-Fi and looks for a room, and can join a friend's game hotspot from their
## QR or by holding the phones together (NFC). Each shows what it is doing, what to do when it cannot go on, Try again and
## Cancel, and carries its flow as a child, so nothing runs or calls back once the dialog is gone.

const Step := HotspotFlow.Step


## Create: `on_ready` runs once, when this phone's hotspot is on or it is connected to a Wi-Fi. `open_settings` replaces the button to the phone's
## network settings (tests); with none, the button is there only where Android can open them. `local` replaces `Net.local` (tests).
static func create_check(on_ready: Callable, interfaces: Callable = Callable(), open_settings: Callable = Callable(), local: LocalLink = null) -> MenuDialog:
	var flow: HotspotFlow = HotspotFlow.new(HotspotFlow.Mode.CREATE, interfaces, Callable(), local)
	return _dialog(flow, func(_url: String) -> void: on_ready.call(), open_settings, null)


## Join: `on_found` runs once with the `ws://` address of a verified room. `plan_for` replaces how to search, `local` the Android
## side and `camera` the scanner's camera (tests).
static func join_check(on_found: Callable, interfaces: Callable = Callable(), plan_for: Callable = Callable(), local: LocalLink = null,
		camera: IdCamera = null) -> MenuDialog:
	return _dialog(HotspotFlow.new(HotspotFlow.Mode.JOIN, interfaces, plan_for, local), on_found, Callable(), camera)


## What the modal says in `step` (for a failed search: the reason the flow found).
static func message(flow: HotspotFlow, step: Step) -> String:
	match step:
		Step.CHECKING:
			return UiText.t("Checking your network…") if flow.mode == HotspotFlow.Mode.CREATE else UiText.t("Checking your Wi-Fi…")
		Step.NO_NETWORK:
			if flow.link.available():
				return UiText.t("Start a game hotspot, or turn on your hotspot or Wi-Fi and tap Try again.")
			return UiText.t("Turn on your hotspot or Wi-Fi. Then tap Try again.")
		Step.NETWORK_OK:
			return UiText.t("Starting the room…")
		Step.STARTING_HOTSPOT:
			return UiText.t("Starting the game hotspot…")
		Step.HOTSPOT_FAILED:
			return hotspot_failure(flow.link_failure)
		Step.NO_WIFI:
			if not flow.link_failure.is_empty():
				return join_failure(flow.link_failure)
			if flow.link.available():
				return UiText.t(nfc_or_scan(flow, "Scan your friend's room QR%s. Or join their Wi-Fi, then tap Try again."))
			return UiText.t("Join your friend's Wi-Fi or hotspot. Then tap Try again.")
		Step.JOINING_WIFI:
			return UiText.t("Joining your friend's game…")
		Step.SEARCHING:
			return UiText.t("Looking for a room…")
		Step.FOUND:
			return UiText.t("Room found. Joining…")
	var reason: String = UiText.t(flow.failure)
	if flow.can_join_by_code():
		reason += " " + UiText.t(nfc_or_scan(flow, "Or scan your friend's room QR%s."))
	return reason


## `template` with ", or hold your phones together" put in at %s when this phone can read NFC.
static func nfc_or_scan(flow: HotspotFlow, template: String) -> String:
	return template % (", or hold your phones back to back" if flow.link.nfc_state() == "on" else "")


static func hotspot_failure(reason: String) -> String:
	match reason:
		"permission":
			return UiText.t("Kalyeah needs your OK to make a hotspot. Tap Start game hotspot, then Allow.")
		"location_off":
			return UiText.t("Turn on Location to make a hotspot (this phone needs it). Then tap Start game hotspot.")
		"incompatible_mode", "tethering_disallowed":
			return UiText.t("This phone can't make a game hotspot right now. Turn on your hotspot or Wi-Fi, then tap Try again.")
		"unsupported":
			return UiText.t("This phone can't make a game hotspot. Turn on your hotspot or Wi-Fi, then tap Try again.")
	return UiText.t("The game hotspot didn't start. Tap Start game hotspot to try again.")


static func join_failure(reason: String) -> String:
	match reason:
		"declined":
			return UiText.t("Not joined. Scan again and tap Connect when your phone asks.")
		"permission":
			return UiText.t("Kalyeah needs your OK to join. Scan again, then Allow.")
		"bad_network":
			return UiText.t("That code can't be joined. Ask your friend to show their room QR again.")
		"unsupported":
			return UiText.t("This phone can't join from the game. Join your friend's Wi-Fi in Settings, then tap Try again.")
	return UiText.t("Couldn't join your friend's game. Scan again, or join their Wi-Fi and tap Try again.")


static func _dialog(flow: HotspotFlow, on_done: Callable, open_settings: Callable, camera: IdCamera) -> MenuDialog:
	var card: MenuDialog = MenuDialog.create("Local")
	card.name = "HotspotCheckDialog"
	var text: Label = card.add_label("", UiMetrics.BODY, KalyeahTheme.MODAL_TEXT)
	text.name = "Message"
	var start: Button = MenuDialog.make_button("Start game hotspot", "orange", UiMetrics.BODY)
	start.name = "StartHotspot"
	var scan: Button = MenuDialog.make_button("Scan QR", "orange", UiMetrics.BODY)
	scan.name = "ScanQr"
	var settings: Button = MenuDialog.make_button("Network settings", "blue", UiMetrics.BODY)
	settings.name = "Settings"
	var again: Button = MenuDialog.make_button("Try again", "blue", UiMetrics.BODY)
	again.name = "TryAgain"
	var start_row: HBoxContainer = card.add_buttons([start])
	var scan_row: HBoxContainer = card.add_buttons([scan])
	var settings_row: HBoxContainer = card.add_buttons([settings])
	card.add_buttons([again])
	var can_open_settings: bool = open_settings.is_valid() or HotspotSettings.available()
	var opener: Callable = open_settings if open_settings.is_valid() else HotspotSettings.open
	settings.pressed.connect(opener)
	again.pressed.connect(flow.begin)
	start.pressed.connect(flow.start_game_hotspot)
	scan.pressed.connect(func() -> void:
		var sheet: MenuDialog = QrScanSheet.build_wifi(flow.join_network, camera)
		sheet.open(card.get_parent()))
	var show: Callable = func(step: Step) -> void:
		var failed: bool = step == Step.NO_NETWORK or step == Step.NO_WIFI or step == Step.NO_ROOM or step == Step.HOTSPOT_FAILED
		text.text = message(flow, step)
		again.visible = failed
		settings_row.visible = (step == Step.NO_NETWORK or step == Step.HOTSPOT_FAILED) and can_open_settings
		start_row.visible = (step == Step.NO_NETWORK or step == Step.HOTSPOT_FAILED) and flow.link.available()
		scan_row.visible = flow.can_join_by_code()
	flow.step_changed.connect(show)
	flow.finished.connect(func(url: String) -> void:
		if card.is_open:
			card.close(false)
			on_done.call(url))
	card.closed.connect(flow.cancel)
	var driver: Driver = Driver.new()
	driver.flow = flow
	card.add_child(driver)
	card.opened.connect(flow.begin)
	return card


## Runs the flow while its dialog is in the tree, and tells it when the app is back from the phone's settings.
class Driver extends Node:
	var flow: HotspotFlow

	func _process(delta: float) -> void:
		flow.tick(delta)

	func _notification(what: int) -> void:
		if what == NOTIFICATION_APPLICATION_RESUMED and flow != null:
			flow.resumed()
