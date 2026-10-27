class_name HotspotDialogs
extends RefCounted
## The two modals in front of a hotspot room (the decisions are HotspotFlow's). Create checks the hotspot is on; Join checks
## the Wi-Fi and looks for a room. Each shows what it is doing, what to do when it cannot go on, Try again and Cancel,
## and carries its flow as a child, so nothing runs or calls back once the dialog is gone.

const Step := HotspotFlow.Step


## Create: `on_ready` runs once, when this phone's hotspot is on or it is connected to a Wi-Fi. `open_settings` replaces the button to the phone's
## network settings (tests); with none, the button is there only where Android can open them.
static func create_check(on_ready: Callable, interfaces: Callable = Callable(), open_settings: Callable = Callable()) -> MenuDialog:
	var flow: HotspotFlow = HotspotFlow.new(HotspotFlow.Mode.CREATE, interfaces)
	return _dialog(flow, func(_url: String) -> void: on_ready.call(), open_settings)


## Join: `on_found` runs once with the `ws://` address of a verified room. `plan_for` replaces how to search (tests).
static func join_check(on_found: Callable, interfaces: Callable = Callable(), plan_for: Callable = Callable()) -> MenuDialog:
	return _dialog(HotspotFlow.new(HotspotFlow.Mode.JOIN, interfaces, plan_for), on_found, Callable())


## What the modal says in `step` (for a failed search: the reason the flow found).
static func message(flow: HotspotFlow, step: Step) -> String:
	match step:
		Step.CHECKING:
			return UiText.t("Checking your network…") if flow.mode == HotspotFlow.Mode.CREATE else UiText.t("Checking your Wi-Fi…")
		Step.NO_NETWORK:
			return UiText.t("Turn on your hotspot or connect to Wi-Fi, then tap Try again.")
		Step.NETWORK_OK:
			return UiText.t("Starting the room…")
		Step.NO_WIFI:
			return UiText.t("Connect to the same Wi-Fi or hotspot as your friend, then tap Try again.")
		Step.SEARCHING:
			return UiText.t("Looking for a room…")
		Step.FOUND:
			return UiText.t("Room found. Joining…")
	return UiText.t(flow.failure)


static func _dialog(flow: HotspotFlow, on_done: Callable, open_settings: Callable) -> MenuDialog:
	var card: MenuDialog = MenuDialog.create("Local")
	card.name = "HotspotCheckDialog"
	var text: Label = card.add_label("", UiMetrics.BODY, KalyeahTheme.MODAL_TEXT)
	text.name = "Message"
	var settings: Button = MenuDialog.make_button("Network settings", "blue", UiMetrics.BODY)
	settings.name = "Settings"
	var again: Button = MenuDialog.make_button("Try again", "blue", UiMetrics.BODY)
	again.name = "TryAgain"
	var cancel: Button = MenuDialog.make_button("Cancel", "grey", UiMetrics.BODY)
	cancel.name = "Cancel"
	var settings_row: HBoxContainer = card.add_buttons([settings])
	card.add_buttons([again, cancel])
	var can_open_settings: bool = open_settings.is_valid() or HotspotSettings.available()
	var opener: Callable = open_settings if open_settings.is_valid() else HotspotSettings.open
	settings.pressed.connect(opener)
	cancel.pressed.connect(card.close)
	again.pressed.connect(flow.begin)
	flow.step_changed.connect(func(step: Step) -> void:
		var failed: bool = step == Step.NO_NETWORK or step == Step.NO_WIFI or step == Step.NO_ROOM
		text.text = message(flow, step)
		again.visible = failed
		settings_row.visible = step == Step.NO_NETWORK and can_open_settings)
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
