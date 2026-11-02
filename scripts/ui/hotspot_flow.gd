class_name HotspotFlow
extends RefCounted
## What happens behind a hotspot modal, with no widget in it. Create: is this phone's hotspot on, or is it on a Wi-Fi? If not,
## it can start a game hotspot (`start_game_hotspot`, through `LocalLink`). Join: is this phone on a Wi-Fi, and is there a
## verified room on it? If not, the friend's game hotspot can be joined from its QR or an NFC tap (`join_network`).
## It decides, it never creates or joins a room: `finished` tells the screen to go on (with the room's address when joining).
## Drive it with `tick(delta)` every frame; every state can be cancelled and ends by itself, and after `cancel` or `finished`
## nothing is emitted again.

signal step_changed(step: Step)
## The check passed. `url` is the room to join ("" when creating).
signal finished(url: String)

enum Mode { CREATE, JOIN }
enum Step { CHECKING, NO_NETWORK, NETWORK_OK, NO_WIFI, SEARCHING, FOUND, NO_ROOM, STARTING_HOTSPOT, HOTSPOT_FAILED, JOINING_WIFI }

const RULES: NetRules = preload("res://data/rules/net_rules.tres")

var mode: Mode
var step: Step = Step.CHECKING
var finder: HotspotFinder = HotspotFinder.new()
var link: LocalLink
## Why the room was not found (full, started, other version, or none), for a failure step.
var failure: String = ""
## Why the game hotspot did not start, or the friend's could not be joined (LocalLink's reason word; "" otherwise).
var link_failure: String = ""
var _interfaces: Callable
var _plan_for: Callable
var _check_next: bool = false
var _ended: bool = false
## Seconds left to wait for the address of a Wi-Fi just joined, and to the next look.
var _wifi_grace: float = 0.0
var _recheck_in: float = 0.0
## What the permission prompt was for: start the hotspot, or join `_pending_network`.
var _after_permission: Callable = Callable()
var _pending_network: Dictionary = {}


## `interfaces` returns the phone's `IP.get_local_interfaces()`; `plan_for(interfaces)` returns the HotspotNet.Plan to
## search with; `local` is the Android side (`Net.local`). All default to the real ones.
func _init(flow_mode: Mode, interfaces: Callable = Callable(), plan_for: Callable = Callable(), local: LocalLink = null) -> void:
	mode = flow_mode
	_interfaces = interfaces if interfaces.is_valid() else IP.get_local_interfaces
	_plan_for = plan_for if plan_for.is_valid() else _plan
	link = local if local != null else Net.local
	link.permissions_answered.connect(_on_permissions)
	link.hotspot_started.connect(_on_hotspot_started)
	link.hotspot_failed.connect(_on_hotspot_failed)
	link.wifi_joined.connect(_on_wifi_joined)
	link.wifi_failed.connect(_on_wifi_failed)
	link.nfc_read.connect(_on_nfc_read)


## Starts (or restarts, for Try again): the verdict comes on the next tick, so "checking" is on screen first.
func begin() -> void:
	if _ended:
		return
	finder.stop()
	link_failure = ""
	_check_next = true
	_enter(Step.CHECKING)


## The app came back from the phone's settings: look again if it was waiting for them.
func resumed() -> void:
	if step == Step.NO_NETWORK or step == Step.NO_WIFI or step == Step.HOTSPOT_FAILED:
		begin()


func cancel() -> void:
	_ended = true
	finder.stop()
	_listen_nfc(false)
	_disconnect()


## Create, with no hotspot and no Wi-Fi: this phone makes a game hotspot (Android asks for "Nearby devices" first).
func start_game_hotspot() -> void:
	if _ended or mode != Mode.CREATE or not link.available():
		return
	link_failure = ""
	_enter(Step.STARTING_HOTSPOT)
	if link.has_permissions():
		link.start_hotspot()
	else:
		_after_permission = link.start_hotspot
		link.request_permissions()


## Join: the friend's Wi-Fi from their QR or an NFC tap ({ssid, password, security}); the room search follows once it is joined.
func join_network(network: Dictionary) -> void:
	if _ended or mode != Mode.JOIN or network.is_empty() or not link.available():
		return
	link_failure = ""
	finder.stop()
	_listen_nfc(false)
	_enter(Step.JOINING_WIFI)
	_pending_network = network
	if link.has_permissions():
		link.join_wifi(network)
	else:
		_after_permission = func() -> void: link.join_wifi(_pending_network)
		link.request_permissions()


## The friend can join by QR or NFC here (a phone with the plugin; NFC only when it is on).
func can_join_by_code() -> bool:
	return mode == Mode.JOIN and link.available() and (step == Step.NO_WIFI or step == Step.NO_ROOM)


func tick(delta: float) -> void:
	if _ended:
		return
	if _check_next:
		_check_next = false
		_check()
	elif step == Step.JOINING_WIFI and _wifi_grace > 0.0:
		_wifi_grace -= delta
		_recheck_in -= delta
		if _recheck_in <= 0.0 or _wifi_grace <= 0.0:
			_check()
	elif step == Step.SEARCHING:
		finder.poll(delta)
		if not finder.url.is_empty():
			_enter(Step.FOUND)
			_end(finder.url)
		elif finder.failed:
			failure = finder.failure_message
			_enter(Step.NO_ROOM)


func _check() -> void:
	var interfaces: Array = _interfaces.call() as Array
	if mode == Mode.CREATE:
		# the host has two ways to make a room: its own hotspot, or the Wi-Fi it is connected to
		if HotspotNet.hotspot_on(interfaces, RULES) or HotspotNet.wifi_connected(interfaces, RULES):
			_enter(Step.NETWORK_OK)
			_end("")
		else:
			_enter(Step.NO_NETWORK)
	elif not HotspotNet.wifi_connected(interfaces, RULES):
		if _wifi_grace > 0.0:
			# a friend's Wi-Fi just joined: its address is on its way
			_recheck_in = RULES.join_wifi_recheck
			_enter(Step.JOINING_WIFI)
		else:
			_enter(Step.NO_WIFI)
	else:
		_wifi_grace = 0.0
		finder.start(_plan_for.call(interfaces) as HotspotNet.Plan)
		_enter(Step.SEARCHING)


func _on_permissions(granted: bool) -> void:
	if _ended or not _after_permission.is_valid():
		return
	var then: Callable = _after_permission
	_after_permission = Callable()
	if granted:
		then.call()
	elif mode == Mode.CREATE:
		_on_hotspot_failed("permission")
	else:
		_on_wifi_failed("permission")


func _on_hotspot_started(_network: Dictionary) -> void:
	if _ended or step != Step.STARTING_HOTSPOT:
		return
	_enter(Step.NETWORK_OK)
	_end("")


func _on_hotspot_failed(reason: String) -> void:
	if _ended or step != Step.STARTING_HOTSPOT:
		return
	link_failure = reason
	_enter(Step.HOTSPOT_FAILED)


func _on_wifi_joined() -> void:
	if _ended or step != Step.JOINING_WIFI:
		return
	_wifi_grace = RULES.join_wifi_grace
	_check_next = true


func _on_wifi_failed(reason: String) -> void:
	if _ended or step != Step.JOINING_WIFI:
		return
	link_failure = reason
	_wifi_grace = 0.0
	_enter(Step.NO_WIFI)


func _on_nfc_read(text: String) -> void:
	if _ended or not can_join_by_code():
		return
	var network: Dictionary = WifiQr.parse(text)
	if not network.is_empty():
		join_network(network)


func _end(url: String) -> void:
	_ended = true
	finder.stop()
	_listen_nfc(false)
	_disconnect()
	finished.emit(url)


func _enter(next: Step) -> void:
	step = next
	if mode == Mode.JOIN:
		_listen_nfc(can_join_by_code())
	step_changed.emit(next)


func _listen_nfc(on: bool) -> void:
	if link.available() and link.nfc_state() == "on":
		link.nfc_read_on(on)


## The flow lives as long as its dialog; the shared link outlives it, so it lets go of the link's signals.
func _disconnect() -> void:
	for pair: Array in [[link.permissions_answered, _on_permissions], [link.hotspot_started, _on_hotspot_started],
			[link.hotspot_failed, _on_hotspot_failed], [link.wifi_joined, _on_wifi_joined], [link.wifi_failed, _on_wifi_failed],
			[link.nfc_read, _on_nfc_read]]:
		var event: Signal = pair[0] as Signal
		var handler: Callable = pair[1] as Callable
		if event.is_connected(handler):
			event.disconnect(handler)


func _plan(interfaces: Array) -> HotspotNet.Plan:
	return HotspotNet.search_plan(interfaces, RULES)
