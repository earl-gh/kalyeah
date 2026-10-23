class_name WebTouchCancel
extends RefCounted
## The browser cancels a touch when the system takes it (notification shade, an OS gesture, a call). Godot's web runtime turns that
## `touchcancel` into a plain release, so `InputEventScreenTouch.canceled` is never true there and a held weapon would be thrown. A
## capture listener on the window sees the `touchcancel` before Godot does and notes it; the release Godot makes from it then
## counts as cancelled. Off the web (Android, desktop) nothing is installed and `canceled` works as usual.

## The release Godot makes from the same browser event reaches the game in the next frames; a note older than this is stale.
const WINDOW_MS: int = 250
const NEVER: int = -1

static var _noted_at: int = NEVER
## Kept alive for as long as the listener is installed (the browser holds only a weak handle to it).
static var _listener: JavaScriptObject


## Listens for the browser's touchcancel (web only, once).
static func install() -> void:
	if _listener != null or not OS.has_feature("web"):
		return
	_listener = JavaScriptBridge.create_callback(_on_browser_cancel)
	var window: JavaScriptObject = JavaScriptBridge.get_interface("window")
	window.call("addEventListener", "touchcancel", _listener, true)


static func _on_browser_cancel(_args: Array) -> void:
	note()


## A touch was cancelled by the system just now.
static func note() -> void:
	_noted_at = Time.get_ticks_msec()


## True (once) if a touch was cancelled within the last `WINDOW_MS`: the release being handled is that cancel.
static func take() -> bool:
	if _noted_at == NEVER:
		return false
	var fresh: bool = Time.get_ticks_msec() - _noted_at <= WINDOW_MS
	_noted_at = NEVER
	return fresh


## Forgets a pending note (tests).
static func clear() -> void:
	_noted_at = NEVER
