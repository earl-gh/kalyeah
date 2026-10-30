extends Node
## User and build settings. The online server address is never typed by players: it is the project
## setting `kalyeah/network/server_url` (stamped into the APK by the Android workflow, empty until a server
## is deployed, see server/README.md).

signal accessibility_changed
## The on-screen controls moved or changed size (left-handed layout, control size).
signal controls_changed
## The language was changed (texts re-translate by themselves; drawn text redraws).
signal language_changed

## The three control sizes the player can pick: the weapon buttons and the stick are scaled by this.
const CONTROL_SIZES: Dictionary[StringName, float] = {&"small": 0.85, &"normal": 1.0, &"large": 1.2}

## Languages the game can show: code to the language written in itself. One is offered once its translation file
## (res://data/i18n/strings.<code>.translation, made from the CSV) is there with at least MIN_TRANSLATED messages.
const LANGUAGES: Dictionary[String, String] = {"en": "English", "fil": "Filipino"}
const TRANSLATION_PATH: String = "res://data/i18n/strings.%s.translation"
const MIN_TRANSLATED: int = 30

const CARD_TRAYS: Array[StringName] = [&"bottom", &"sides"]
## The frame rates, performance modes and Turbo's resolution share the Graphics settings offer (docs/GDD.md "Graphics settings").
const FRAME_RATES: Array[int] = [30, 60]
const PERFORMANCES: Array[StringName] = [&"standard", &"turbo"]
const TURBO_SCALE: float = 0.75

const SAVE_PATH: String = "user://settings.cfg"
const PROJECT_KEY: String = "kalyeah/network/server_url"

var server_url: String = ""
## Whether button presses buzz the phone (owner-facing settings screen).
var vibration: bool = true
## The most frames a second the phone draws: 30 saves battery, 60 is smoother (`FRAME_RATES`). Only the phone is capped.
var frame_rate: int = 60
## `&"standard"` or `&"turbo"` (`PERFORMANCES`): Turbo draws the 3D street with no anti-aliasing and at `TURBO_SCALE` of the screen's
## resolution (the words and buttons stay sharp), so a slower phone keeps up.
var performance: StringName = &"standard"
## Whether the street shakes when you are hit or a wall breaks (off = it stays still).
var screen_shake: bool = true
var reduced_motion: bool = false
## Team colours that stay apart for colour-blind players: blue and orange instead of blue and red, and no green for you.
var colorblind: bool = false
## Mirrors the match controls: the stick on the right, the weapons on the left.
var left_handed: bool = false
## Where the weapon cards sit in a match: `&"bottom"` (the tray along the bottom) or `&"sides"` (two cards on each side of the street,
## the ice water bar alone along the bottom). One of `CARD_TRAYS`.
var cards_tray: StringName = &"bottom"
## The language of the words ("en" until another one is offered and chosen).
var language: String = "en"
## Whether holding a weapon shows its simple aim guide on the street (off = nothing is drawn).
var aim_guide: bool = true
## Tilt to run (off by default): lean the phone the way to run; the kid runs at its one speed, like a finger dragged ahead of it.
var tilt_to_run: bool = false
## Size of the match controls, one of `CONTROL_SIZES`.
var control_scale: float = 1.0
## The Practice difficulty chosen last (`GameRules.bot_levels`); "" until one is chosen, then the rules' default is used.
var bot_level: StringName = &""


func _ready() -> void:
	KalyeahTheme.apply()
	# every Button in the game clicks (menus, lobby, pick screen)
	get_tree().node_added.connect(_on_node_added)
	server_url = ProjectSettings.get_setting(PROJECT_KEY, "") as String
	var file: ConfigFile = ConfigFile.new()
	if file.load(SAVE_PATH) == OK:
		vibration = bool(file.get_value("input", "vibration", true))
		frame_rate = nearest_frame_rate(file.get_value("graphics", "frame_rate", 60))
		var mode: StringName = StringName(String(file.get_value("graphics", "performance", "standard")))
		performance = mode if PERFORMANCES.has(mode) else &"standard"
		var shake: Variant = file.get_value("graphics", "screen_shake", true)
		screen_shake = shake if shake is bool else true
		var motion: Variant = file.get_value("accessibility", "reduced_motion", false)
		reduced_motion = motion if motion is bool else false
		var blind: Variant = file.get_value("accessibility", "colorblind", false)
		colorblind = blind if blind is bool else false
		var hand: Variant = file.get_value("input", "left_handed", false)
		left_handed = hand if hand is bool else false
		var tray: StringName = StringName(String(file.get_value("input", "cards_tray", "bottom")))
		cards_tray = tray if CARD_TRAYS.has(tray) else &"bottom"
		control_scale = nearest_control_scale(file.get_value("input", "control_scale", 1.0))
		language = String(file.get_value("ui", "language", "en"))
		var guide: Variant = file.get_value("input", "aim_guide", true)
		aim_guide = guide if guide is bool else true
		var tilt: Variant = file.get_value("input", "tilt_to_run", false)
		tilt_to_run = tilt if tilt is bool else false
		bot_level = StringName(String(file.get_value("practice", "bot_level", "")))
	apply_graphics()
	lock_portrait()
	get_window().size_changed.connect(fit_portrait)
	fit_portrait()
	_load_translations()
	if not available_languages().has(language):
		language = "en"
	TranslationServer.set_locale(language)


## A bare host becomes wss://host (ws:// for localhost).
static func normalize_url(raw: String) -> String:
	var url: String = raw.strip_edges()
	if url.is_empty() or url.begins_with("ws://") or url.begins_with("wss://"):
		return url
	url = url.trim_prefix("https://").trim_prefix("http://")
	if url.begins_with("localhost") or is_local_address(url):
		return "ws://" + url
	return "wss://" + url


## True for a private network address (a phone hotspot or home Wi-Fi), which has no https.
static func is_local_address(address: String) -> bool:
	if address.begins_with("127.") or address.begins_with("10.") or address.begins_with("192.168."):
		return true
	if address.begins_with("172."):
		var second: String = address.split(".")[1] if address.split(".").size() > 1 else ""
		return second.is_valid_int() and second.to_int() >= 16 and second.to_int() <= 31
	return false


## The allowed control size closest to `value` (anything that is not a number is the normal size).
static func nearest_control_scale(value: Variant) -> float:
	if not (value is float or value is int):
		return 1.0
	var best: float = 1.0
	for option: float in CONTROL_SIZES.values():
		if absf(option - float(value)) < absf(best - float(value)):
			best = option
	return best


## Loads every language file that exists (a language without one is simply not offered).
func _load_translations() -> void:
	for code: String in LANGUAGES:
		var path: String = TRANSLATION_PATH % code
		if code != "en" and ResourceLoader.exists(path):
			var translation: Translation = load(path) as Translation
			if translation != null:
				TranslationServer.add_translation(translation)


## The languages the player can pick now: English, and each other one whose translation has enough messages.
func available_languages() -> Array[String]:
	var found: Array[String] = ["en"]
	for code: String in LANGUAGES:
		if code == "en":
			continue
		var translation: Translation = TranslationServer.get_translation_object(code)
		if translation != null and translation.get_message_count() >= MIN_TRANSLATED:
			found.append(code)
	return found


func set_language(code: String) -> void:
	if code == language or not available_languages().has(code):
		return
	language = code
	TranslationServer.set_locale(code)
	_save_value("ui", "language", code)
	language_changed.emit()


func set_left_handed(on: bool) -> void:
	if on == left_handed:
		return
	left_handed = on
	_save_value("input", "left_handed", on)
	controls_changed.emit()


func set_cards_tray(tray: StringName) -> void:
	if not CARD_TRAYS.has(tray) or tray == cards_tray:
		return
	cards_tray = tray
	_save_value("input", "cards_tray", String(tray))
	controls_changed.emit()


func set_tilt_to_run(on: bool) -> void:
	if on == tilt_to_run:
		return
	tilt_to_run = on
	_save_value("input", "tilt_to_run", on)
	controls_changed.emit()


func set_aim_guide(on: bool) -> void:
	if on == aim_guide:
		return
	aim_guide = on
	_save_value("input", "aim_guide", on)
	controls_changed.emit()


func set_control_scale(value: float) -> void:
	var chosen: float = nearest_control_scale(value)
	if is_equal_approx(chosen, control_scale):
		return
	control_scale = chosen
	_save_value("input", "control_scale", chosen)
	controls_changed.emit()


func set_bot_level(id: StringName) -> void:
	if id == bot_level:
		return
	bot_level = id
	_save_value("practice", "bot_level", String(id))


func set_vibration(on: bool) -> void:
	vibration = on
	_save_value("input", "vibration", on)


## True where the phone can buzz (the Android app; headless runs and tests have no motor, so no toggle).
static func can_vibrate() -> bool:
	return OS.has_feature("android")


func vibrate(milliseconds: int = 12) -> void:
	if vibration and can_vibrate():
		Input.vibrate_handheld(milliseconds)


func _save_value(section: String, key: String, value: Variant) -> void:
	var file: ConfigFile = ConfigFile.new()
	file.load(SAVE_PATH)
	file.set_value(section, key, value)
	file.save(SAVE_PATH)


func _on_node_added(node: Node) -> void:
	if node is Control and reduced_motion:
		UiAccessibility.apply_weak.call_deferred(weakref(node))
	# a button that leaves the tree and comes back (a tile moved between groups) is not wired twice
	if node is BaseButton and not (node as BaseButton).pressed.is_connected(_on_button_pressed):
		(node as BaseButton).pressed.connect(_on_button_pressed, CONNECT_DEFERRED)


func _on_button_pressed() -> void:
	vibrate()


func set_colorblind(enabled: bool) -> void:
	if enabled == colorblind:
		return
	colorblind = enabled
	_save_value("accessibility", "colorblind", colorblind)
	accessibility_changed.emit()


func set_reduced_motion(enabled: bool) -> void:
	reduced_motion = enabled
	_save_value("accessibility", "reduced_motion", reduced_motion)
	UiAccessibility.apply_tree(get_tree().root)
	accessibility_changed.emit()


## The game is vertical on every phone and tablet (docs/GDD.md "Vertical layout"): asked of the phone at start, even when the device's
## auto-rotate is on. Android 16 may ignore the request on a large screen (a tablet or a folded-out phone); `fit_portrait` covers that.
func lock_portrait() -> void:
	if OS.has_feature("android"):
		DisplayServer.screen_set_orientation(DisplayServer.SCREEN_PORTRAIT)


## How the street fills a window of `size`: a tall window fills it (more street above and below on a long phone), but a window that is
## wider than tall (a tablet that did not stay vertical) keeps the vertical shape, centred, with bars at its sides, so the game
## never lays itself out sideways.
static func aspect_for(size: Vector2i) -> Window.ContentScaleAspect:
	return Window.CONTENT_SCALE_ASPECT_KEEP if size.x > size.y else Window.CONTENT_SCALE_ASPECT_EXPAND


## Applies `aspect_for` to the game window (on a phone or tablet; a desktop window is left as it is unless `force`).
func fit_portrait(force: bool = false) -> void:
	if not force and not OS.has_feature("android"):
		return
	var window: Window = get_window()
	window.content_scale_aspect = aspect_for(window.size)


## The listed frame rate nearest to `value` (a hand-edited or missing file falls back to 60).
static func nearest_frame_rate(value: Variant) -> int:
	if not (value is int or value is float):
		return 60
	var best: int = 60
	var best_gap: float = INF
	for rate: int in FRAME_RATES:
		var gap: float = absf(float(rate) - float(value))
		if gap < best_gap:
			best = rate
			best_gap = gap
	return best


func set_frame_rate(rate: int) -> void:
	var chosen: int = nearest_frame_rate(rate)
	if chosen == frame_rate:
		return
	frame_rate = chosen
	_save_value("graphics", "frame_rate", chosen)
	apply_graphics()


func set_performance(mode: StringName) -> void:
	if not PERFORMANCES.has(mode) or mode == performance:
		return
	performance = mode
	_save_value("graphics", "performance", String(mode))
	apply_graphics()
	accessibility_changed.emit()  # the film grain follows it


func set_screen_shake(on: bool) -> void:
	if on == screen_shake:
		return
	screen_shake = on
	_save_value("graphics", "screen_shake", on)


## What the engine is told to do for these settings: the phone's frame cap (never on a desktop or a test run, which would only slow
## them down) and the 3D street's anti-aliasing and resolution. The words and buttons are drawn at full size either way.
func apply_graphics() -> void:
	if OS.has_feature("android"):
		Engine.max_fps = frame_rate
	var viewport: Viewport = get_viewport()
	if viewport == null:
		return
	var turbo: bool = performance == &"turbo"
	viewport.msaa_3d = Viewport.MSAA_DISABLED if turbo else (ProjectSettings.get_setting("rendering/anti_aliasing/quality/msaa_3d", 2) as Viewport.MSAA)
	viewport.scaling_3d_scale = TURBO_SCALE if turbo else 1.0
