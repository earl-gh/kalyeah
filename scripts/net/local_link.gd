class_name LocalLink
extends RefCounted
## Local play without the phone's settings, through Kalyeah's Android plugin (android/kalyeah_local): the host starts a game
## hotspot and shares it (a Wi-Fi QR, and NFC for a friend's phone held against this one); a friend joins it from inside the game.
## Everything that touches Android is here, so the flows can be tested with a stand-in that overrides these methods. Off a
## phone (or on a build without the plugin) `available()` is false and nothing else does anything. One lives in `Net.local`.

signal permissions_answered(granted: bool)
## The game hotspot is up; `network` is its {ssid, password, security} (WifiQr).
signal hotspot_started(network: Dictionary)
## Why it did not start: "permission", "location_off", "incompatible_mode", "tethering_disallowed", "no_channel", "busy",
## "unsupported" or "generic".
signal hotspot_failed(reason: String)
signal hotspot_stopped
signal wifi_joined
## "declined", "permission", "bad_network", "unsupported" or "generic".
signal wifi_failed(reason: String)
signal wifi_lost
## A Kalyeah phone held against this one shared `text` (a Wi-Fi QR text).
signal nfc_read(text: String)

const SINGLETON: String = "KalyeahLocal"

## The running game hotspot ({} when none).
var hotspot: Dictionary = {}
var _plugin: Object


func _init() -> void:
	if Engine.has_singleton(SINGLETON):
		_plugin = Engine.get_singleton(SINGLETON)
		_plugin.connect("permissions_answered", func(granted: bool) -> void: permissions_answered.emit(granted))
		_plugin.connect("hotspot_started", _on_hotspot_started)
		_plugin.connect("hotspot_failed", func(reason: String) -> void: hotspot_failed.emit(reason))
		_plugin.connect("hotspot_stopped", _on_hotspot_stopped)
		_plugin.connect("wifi_joined", func() -> void: wifi_joined.emit())
		_plugin.connect("wifi_failed", func(reason: String) -> void: wifi_failed.emit(reason))
		_plugin.connect("wifi_lost", func() -> void: wifi_lost.emit())
		_plugin.connect("nfc_read", func(text: String) -> void: nfc_read.emit(text))


func available() -> bool:
	return _plugin != null


func has_permissions() -> bool:
	return available() and bool(_plugin.call("hasPermissions"))


## Android's own prompt ("Nearby devices", or location on Android 8 to 12); `permissions_answered` follows.
func request_permissions() -> void:
	if not available():
		permissions_answered.emit.call_deferred(false)
		return
	_plugin.call("requestPermissions")


func start_hotspot() -> void:
	if not available():
		hotspot_failed.emit.call_deferred("unsupported")
		return
	_plugin.call("startHotspot")


func stop_hotspot() -> void:
	hotspot = {}
	if available():
		_plugin.call("stopHotspot")


func hotspot_running() -> bool:
	return not hotspot.is_empty()


## The game hotspot as a Wi-Fi QR text ("" when none): what the room's QR shows and what NFC hands over.
func share_text() -> String:
	return WifiQr.build(hotspot) if hotspot_running() else ""


func join_wifi(network: Dictionary) -> void:
	if not available():
		wifi_failed.emit.call_deferred("unsupported")
		return
	_plugin.call("joinWifi", String(network.get("ssid", "")), String(network.get("password", "")), String(network.get("security", "WPA")))


func leave_wifi() -> void:
	if available():
		_plugin.call("leaveWifi")


## "none" (no NFC that can do this), "off" (switched off) or "on".
func nfc_state() -> String:
	return String(_plugin.call("nfcState")) if available() else "none"


## While on, a Kalyeah phone held against this one sends `nfc_read`.
func nfc_read_on(on: bool) -> void:
	if available():
		_plugin.call("nfcRead", on)


## Back on the home screen: no game hotspot, no friend's Wi-Fi, no NFC listening.
func leave() -> void:
	nfc_read_on(false)
	stop_hotspot()
	leave_wifi()


func _on_hotspot_started(ssid: String, password: String, security: String) -> void:
	hotspot = {"ssid": ssid, "password": password, "security": security}
	if _plugin != null:
		_plugin.call("nfcShare", share_text())
	hotspot_started.emit(hotspot)


func _on_hotspot_stopped() -> void:
	hotspot = {}
	hotspot_stopped.emit()
