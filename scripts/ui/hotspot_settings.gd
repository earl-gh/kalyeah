class_name HotspotSettings
extends RefCounted
## Opens the phone's own "Network & internet" screen, where the hotspot switch and the Wi-Fi list are one tap away. An
## Android intent started through Godot's JavaClassWrapper and AndroidRuntime singleton (4.6): no permission, no plugin.
## Nothing happens off a phone.

const ACTION: String = "android.settings.WIRELESS_SETTINGS"


static func available() -> bool:
	return OS.get_name() == "Android" and Engine.has_singleton("AndroidRuntime") and Engine.has_singleton("JavaClassWrapper")


## True when the settings screen was asked for and Android did not refuse.
static func open() -> bool:
	if not available():
		return false
	var runtime: Object = Engine.get_singleton("AndroidRuntime")
	var wrapper: Object = Engine.get_singleton("JavaClassWrapper")
	var activity: Object = runtime.call("getActivity") as Object
	var intent_class: Object = wrapper.call("wrap", "android.content.Intent") as Object
	var intent: Object = intent_class.call("Intent", ACTION) as Object
	if activity == null or intent == null:
		return false
	activity.call("startActivity", intent)
	return wrapper.call("get_exception") == null
