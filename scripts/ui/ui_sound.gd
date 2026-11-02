class_name UiSound
extends Node
## The sounds of the interface (docs/ART.md "Audio"): the owner's own sound effects (`assets/audio/sfx/`, the ones the match plays) cut
## short and quiet for taps, selections, windows, toasts and screen swipes, so the menus and the match share one sound world. A button that
## has the shared press feel (`UiKit.feel_press`) and the switch (`UiToggle`) play their role when pressed; `MenuDialog.open`, `UiToast.say`
## and the mode carousel play theirs. View only. One node under the root, made on first use; a role whose file is missing stays silent.

## `role` was just played (read by tests).
signal played(role: StringName)

## The meta key a button can carry to choose its role (`SILENT` for none).
const META: StringName = &"sfx"
const SILENT: StringName = &"none"
const HOOKED: StringName = &"sfx_hooked"
const NODE_NAME: String = "UiSound"
## Simultaneous sounds; the oldest voice is reused when all are busy.
const VOICES: int = 6
## A cut sound fades out over this long (s), so it never ends on a click.
const FADE: float = 0.04
const FLOOR_DB: float = -60.0
## Role -> {file: a sound in `SfxView.DIR`, db: its volume, pitch: [low, high] picked at random for each play (the same sound never
## repeats exactly), max: the longest it plays (s; 0 = the whole file)}. The recordings are the match's: only the cut, the level and the
## pitch differ, and the UI ones are quieter and shorter than the match's so a button never sounds like a hit.
const ROLES: Dictionary[StringName, Dictionary] = {
	# the small taps: buttons, switches
	&"tap": {"file": "gunting_light", "db": -10.0, "pitch": [1.15, 1.35], "max": 0.1},
	# choosing among options: segments, the carousel's neighbours
	&"tick": {"file": "bola_bounce", "db": -14.0, "pitch": [1.0, 1.25], "max": 0.15},
	# picking a card, a kid, a tab
	&"select": {"file": "card_pick", "db": -9.0, "pitch": [0.95, 1.1], "max": 0.0},
	# the big coloured buttons (Create, Join, Play, Home)
	&"press": {"file": "paper_block", "db": -7.0, "pitch": [0.9, 1.0], "max": 0.3},
	# going back
	&"cancel": {"file": "card_cancel", "db": -10.0, "pitch": [0.95, 1.05], "max": 0.0},
	# a window opens
	&"window": {"file": "paper_shield", "db": -12.0, "pitch": [1.1, 1.25], "max": 0.25},
	# a toast: done, heads-up, no
	&"success": {"file": "heal", "db": -9.0, "pitch": [1.0, 1.0], "max": 0.5},
	&"alert": {"file": "stunned", "db": -16.0, "pitch": [1.0, 1.0], "max": 0.6},
	&"deny": {"file": "soil_block", "db": -9.0, "pitch": [0.95, 1.05], "max": 0.3},
	# the mode carousel moves
	&"swipe": {"file": "tsinelas_throw_light", "db": -15.0, "pitch": [1.3, 1.5], "max": 0.2},
}
## A toast's tone -> the role it plays.
const TONE_ROLE: Dictionary[String, StringName] = {
	"ok": &"success", "info": &"alert", "warn": &"deny", "error": &"deny",
}

static var _instance: UiSound

var _voices: Array[AudioStreamPlayer] = []
var _cuts: Array[Tween] = []
var _next: int = 0
var _cache: Dictionary[String, AudioStream] = {}
var _random: RandomNumberGenerator = RandomNumberGenerator.new()


func _init() -> void:
	name = NODE_NAME
	_random.randomize()
	for i: int in VOICES:
		var voice: AudioStreamPlayer = AudioStreamPlayer.new()
		add_child(voice)
		_voices.append(voice)
		_cuts.append(null)


## Plays `role` (a name in `ROLES`); nothing happens before the player is in the tree or for an unknown role or a missing file.
static func play(role: StringName) -> void:
	var sound: UiSound = shared()
	if sound != null:
		sound.play_role(role)


## Gives `button` its sound when it is pressed, once (a button that is wired again keeps the one hook).
static func hook(button: BaseButton) -> void:
	if button.has_meta(HOOKED):
		return
	button.set_meta(HOOKED, true)
	shared()
	button.pressed.connect(_on_pressed.bind(button))


## The role `button` plays: the one it was given (`META`), else by what it is: a switch and any other button tap, a segment ticks.
static func role_for(button: BaseButton) -> StringName:
	if button.has_meta(META):
		return button.get_meta(META) as StringName
	if button.get_parent() is UiSegmented:
		return &"tick"
	return &"tap"


## Gives `button` a role of its own (`SILENT` for none).
static func set_role(button: BaseButton, role: StringName) -> void:
	button.set_meta(META, role)


static func _on_pressed(button: BaseButton) -> void:
	if not is_instance_valid(button):
		return
	var role: StringName = role_for(button)
	if role != SILENT:
		play(role)


## The one player, made (under the root, on the next frame) the first time it is asked for.
static func shared() -> UiSound:
	if is_instance_valid(_instance):
		return _instance
	var tree: SceneTree = Engine.get_main_loop() as SceneTree
	if tree == null or tree.root == null:
		return null
	_instance = UiSound.new()
	tree.root.add_child.call_deferred(_instance)
	return _instance


func play_role(role: StringName) -> void:
	if not is_inside_tree() or not ROLES.has(role):
		return
	var spec: Dictionary = ROLES[role]
	var stream: AudioStream = _stream(spec["file"] as String)
	if stream == null:
		return
	var at: int = _next
	_next = (_next + 1) % VOICES
	var voice: AudioStreamPlayer = _voices[at]
	if _cuts[at] != null and _cuts[at].is_valid():
		_cuts[at].kill()
	var pitch: Array = spec["pitch"] as Array
	voice.stream = stream
	voice.volume_db = spec["db"] as float
	voice.pitch_scale = _random.randf_range(pitch[0] as float, pitch[1] as float)
	voice.play()
	played.emit(role)
	var longest: float = spec["max"] as float
	if longest > 0.0:
		var cut: Tween = create_tween()
		cut.tween_interval(longest)
		cut.tween_property(voice, "volume_db", FLOOR_DB, FADE)
		cut.tween_callback(voice.stop)
		_cuts[at] = cut


func _stream(file: String) -> AudioStream:
	if not _cache.has(file):
		var path: String = SfxView.DIR + file + ".mp3"
		_cache[file] = load(path) as AudioStream if ResourceLoader.exists(path) else null
	return _cache[file]
