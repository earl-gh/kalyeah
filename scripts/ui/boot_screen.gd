class_name BootScreen
extends Control
## Boot screen, laid out like the Clash Royale loading screen: the key art fills the screen above a
## matte black loading base at the bottom (a matte black layer is under everything). The cardboard
## fills the base from left to right as the game loads. The loaded percentage sits on the base's top
## edge (in the owner's Bungee font) and a tip floats just above it over the art. It
## loads the scenes the player is about to open, then hands over to the title screen. A dedicated
## server skips it.

const TITLE_SCENE: String = "res://scenes/ui/title_screen.tscn"
const WARM_SCENES: Array[String] = [
	"res://scenes/ui/title_screen.tscn",
	"res://scenes/ui/lobby.tscn",
	"res://scenes/match/practice.tscn",
]
const BACKGROUND: Texture2D = preload("res://assets/ui/boot/boot_bg.webp")
const FILL: Texture2D = preload("res://assets/ui/boot/boot_bar_fill.png")
## The cardboard picture has rounded ends and rough edges: only this straight middle part of it is
## used, so no curve shows.
const FILL_REGION: Rect2 = Rect2(70.0, 30.0, 740.0, 54.0)
## The font of the loaded percentage (owner). It only has capital letters, so the tip, which is
## sentence case, uses the body font.
const PERCENT_FONT: Font = KalyeahTheme.DISPLAY_FONT
## Matte black (not pure black) for the loading base and the layer under everything.
const BASE_COLOR: Color = Color(0.085, 0.085, 0.09)

## The loading base along the bottom edge (the phone's home-bar inset is added to it).
const BAND_HEIGHT: float = 52.0
## The percentage is this tall; it straddles the base's top edge (half over the art, half over the base).
const PERCENT_HEIGHT: float = 64.0
## One tip is shown per launch.
const TIPS: Array[String] = [
	"Lure the other team away from their post, then dash in to capture it.",
	"Tagged kids wait in the box. A teammate can run in and rescue them.",
	"Drag a weapon button to aim, then let go to throw it.",
	"Block a throw with a paper shield, or play it safe behind cover.",
	"Practice is the safest place to try every weapon.",
	"Hotspot rooms need no server: share a hotspot and play.",
	"Two weapons, two skills: pick a pair that covers each other.",
]
## The bar never fills faster than this, so the screen is seen even when loading is instant.
const MAX_FILL_PER_SECOND: float = 0.7

signal finished

var progress: float = 0.0
var _loaded: float = 0.0
var _done: bool = false
var _fill_clip: Control
var _fill_rect: TextureRect
var _base: ColorRect
var _band: ColorRect
var _art: TextureRect
var _percent: Label
var _tip: Label


## Width of the filled part of a base `width` wide at `fraction` (0..1): the cardboard grows from
## the left edge to the right edge.
static func fill_width(fraction: float, width: float) -> float:
	return width * clampf(fraction, 0.0, 1.0)


func _ready() -> void:
	if TitleScreen.is_server_run():
		get_tree().change_scene_to_file.call_deferred(TitleScreen.SERVER_SCENE)
		set_process(false)
		return
	_build()
	resized.connect(_layout)
	_layout()
	for path: String in WARM_SCENES:
		ResourceLoader.load_threaded_request(path)


func _build() -> void:
	# black is the base layer under everything
	var black: ColorRect = ColorRect.new()
	black.name = "Black"
	black.color = BASE_COLOR
	black.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(black)
	black.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_art = TextureRect.new()
	_art.texture = BACKGROUND
	_art.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_art.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	_art.clip_contents = true
	_art.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_art)
	_base = ColorRect.new()
	_base.name = "Base"
	_base.color = BASE_COLOR
	_base.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_base)
	_fill_clip = Control.new()
	_fill_clip.clip_contents = true
	_fill_clip.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_fill_clip)
	var cardboard: AtlasTexture = AtlasTexture.new()
	cardboard.atlas = FILL
	cardboard.region = FILL_REGION
	_fill_rect = _bar_layer(cardboard, _fill_clip)
	_fill_rect.stretch_mode = TextureRect.STRETCH_SCALE
	_percent = Label.new()
	_percent.add_theme_font_override("font", PERCENT_FONT)
	_percent.add_theme_color_override("font_color", Color.WHITE)
	_percent.add_theme_color_override("font_outline_color", Color(0.05, 0.03, 0.02))
	_percent.add_theme_constant_override("outline_size", 8)
	_percent.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_percent.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_percent.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_percent)
	_tip = Label.new()
	_tip.text = TIPS[randi() % TIPS.size()]
	_tip.add_theme_font_override("font", KalyeahTheme.BODY_FONT)
	_tip.add_theme_font_size_override("font_size", 26)
	_tip.add_theme_color_override("font_color", Color.WHITE)
	_tip.add_theme_color_override("font_outline_color", Color(0.03, 0.05, 0.12))
	_tip.add_theme_constant_override("outline_size", 8)
	_tip.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_tip.vertical_alignment = VERTICAL_ALIGNMENT_BOTTOM
	_tip.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_tip.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_tip)


## The black band's height: its own height plus the phone's home-bar inset.
func band_height() -> float:
	var window: Vector2i = DisplayServer.window_get_size()
	var inset: float = 0.0
	if window.y > 0:
		var safe: Rect2i = DisplayServer.get_display_safe_area()
		var below: float = maxf(0.0, float(DisplayServer.window_get_position().y + window.y - (safe.position.y + safe.size.y)))
		inset = minf(below * size.y / float(window.y), 120.0)
	return BAND_HEIGHT + inset


func _bar_layer(texture: Texture2D, parent: Control = self) -> TextureRect:
	var rect: TextureRect = TextureRect.new()
	rect.texture = texture
	rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	rect.stretch_mode = TextureRect.STRETCH_SCALE
	rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(rect)
	return rect


func _layout() -> void:
	if _base == null:
		return
	var band: float = band_height()
	_art.position = Vector2.ZERO
	_art.size = Vector2(size.x, size.y - band)
	# the cement-coloured base and the cardboard on it are the same size: the whole band
	var origin: Vector2 = Vector2(0.0, size.y - band)
	var base_size: Vector2 = Vector2(size.x, band)
	_base.position = origin
	_base.size = base_size
	_fill_clip.position = origin
	# the cardboard keeps its own proportions at the base's scale (it covers the base's height) and
	# is cropped by the base, so the same picture is revealed as the progress grows
	var scale_to_base: float = maxf(base_size.y / FILL_REGION.size.y, base_size.x / FILL_REGION.size.x)
	_fill_rect.position = Vector2.ZERO
	_fill_rect.size = FILL_REGION.size * scale_to_base
	# the percentage is centred vertically on the base's top edge, overlapping it
	_percent.size = Vector2(size.x, PERCENT_HEIGHT)
	_percent.position = Vector2(0.0, origin.y - PERCENT_HEIGHT / 2.0)
	_percent.add_theme_font_size_override("font_size", int(PERCENT_HEIGHT * 0.6))
	# the tip floats just above the base, over the art
	var side: float = size.x * 0.08
	# a wrapping label works out its height from its width, so give it the width first
	_tip.custom_minimum_size = Vector2(size.x - side * 2.0, 0.0)
	_tip.size = Vector2(size.x - side * 2.0, 112.0)
	_tip.position = Vector2(side, size.y - band - PERCENT_HEIGHT / 2.0 - _tip.size.y - 6.0)
	_apply_progress()


func _apply_progress() -> void:
	if _base == null:
		return
	_fill_clip.size = Vector2(fill_width(progress, _base.size.x), _base.size.y)
	_percent.text = "%d%%" % roundi(clampf(progress, 0.0, 1.0) * 100.0)


func _process(delta: float) -> void:
	if _done or _base == null:
		return
	_loaded = _loading_fraction()
	progress = minf(_loaded, progress + MAX_FILL_PER_SECOND * delta)
	_apply_progress()
	if progress >= 1.0:
		_done = true
		finished.emit()
		var packed: PackedScene = ResourceLoader.load_threaded_get(TITLE_SCENE) as PackedScene
		get_tree().change_scene_to_packed(packed)


## Average load progress of the scenes being warmed up (0..1).
func _loading_fraction() -> float:
	var total: float = 0.0
	for path: String in WARM_SCENES:
		var info: Array = []
		var status: ResourceLoader.ThreadLoadStatus = ResourceLoader.load_threaded_get_status(path, info)
		if status == ResourceLoader.THREAD_LOAD_LOADED:
			total += 1.0
		elif status == ResourceLoader.THREAD_LOAD_IN_PROGRESS and not info.is_empty():
			total += float(info[0])
	return total / float(WARM_SCENES.size())
