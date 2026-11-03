class_name UiSound
extends Node
## The sounds of the interface (docs/ART.md "Audio"): the menus' own sounds, made in code (`UiTones`), not the match's weapon effects. They
## are short, soft and playful: bubbly taps, music-box picks, a bell chime for good news, a goofy bonk for no. A button that has the shared press feel (`UiKit.feel_press`) and
## the switch (`UiToggle`) play their role when pressed; `MenuDialog.open`, `UiToast.say` and the mode carousel play theirs. View only. One
## node under the root, made on first use.

## `role` was just played (read by tests).
signal played(role: StringName)

## The meta key a button can carry to choose its role (`SILENT` for none).
const META: StringName = &"sfx"
const SILENT: StringName = &"none"
const HOOKED: StringName = &"sfx_hooked"
const NODE_NAME: String = "UiSound"
## Simultaneous sounds; the oldest voice is reused when all are busy.
const VOICES: int = 6
## Role -> {tone: a sound of `UiTones.TONES`, db: its volume, pitch: [low, high] picked at random for each play (a few percent, so the same
## sound never repeats exactly yet stays in tune with its key)}. Every tone is already levelled to the same loudness (`UiTones.TARGET_RMS`, about
## -18 dBFS), so `db` is the role's place in the pecking order: the big coloured buttons and the good news are loudest, a tap and a tick
## sit under them, the carousel's swipe is the quietest. The tick is a few dB up because a very short sound reads quieter than a longer one.
const ROLES: Dictionary[StringName, Dictionary] = {
	# the small taps: buttons, switches
	&"tap": {"tone": "pop", "db": -8.0, "pitch": [0.98, 1.03]},
	# choosing among options: segments, the carousel's neighbours
	&"tick": {"tone": "click", "db": -7.0, "pitch": [0.98, 1.03]},
	# picking a card, a kid, a tab
	&"select": {"tone": "pick", "db": -7.0, "pitch": [0.99, 1.02]},
	# the big coloured buttons (Create, Join, Play, Home)
	&"press": {"tone": "thump", "db": -5.0, "pitch": [0.98, 1.03]},
	# going back
	&"cancel": {"tone": "back", "db": -8.0, "pitch": [0.99, 1.02]},
	# a window opens
	&"window": {"tone": "open", "db": -9.0, "pitch": [0.99, 1.02]},
	# a toast: done, heads-up, no
	&"success": {"tone": "chime", "db": -6.0, "pitch": [1.0, 1.0]},
	&"alert": {"tone": "ping", "db": -7.0, "pitch": [1.0, 1.0]},
	&"deny": {"tone": "buzz", "db": -6.0, "pitch": [0.99, 1.01]},
	# the mode carousel moves
	&"swipe": {"tone": "whoosh", "db": -11.0, "pitch": [0.95, 1.1]},
}
## A toast's tone -> the role it plays.
const TONE_ROLE: Dictionary[String, StringName] = {
	"ok": &"success", "info": &"alert", "warn": &"deny", "error": &"deny",
}

static var _instance: UiSound

var _voices: Array[AudioStreamPlayer] = []
var _next: int = 0
var _cache: Dictionary[String, AudioStreamWAV] = {}
var _random: RandomNumberGenerator = RandomNumberGenerator.new()


func _init() -> void:
	name = NODE_NAME
	set_process(true)  # `_process` makes the tones, one a frame
	_random.randomize()
	for i: int in VOICES:
		var voice: AudioStreamPlayer = AudioStreamPlayer.new()
		voice.bus = Settings.SFX_BUS  # the Sounds switch mutes this bus
		add_child(voice)
		_voices.append(voice)


## The tones are made ahead, one a frame, so no first tap has to wait for its sound (making the longest takes a few tens of milliseconds).
## A role asked for before its turn is made on the spot.
func _process(_delta: float) -> void:
	for tone: String in UiTones.TONES:
		if not _cache.has(tone):
			_stream(tone)
			return
	set_process(false)


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
	var stream: AudioStream = _stream(spec["tone"] as String)
	if stream == null:
		return
	var voice: AudioStreamPlayer = _voices[_next]
	_next = (_next + 1) % VOICES
	var pitch: Array = spec["pitch"] as Array
	voice.stream = stream
	voice.volume_db = spec["db"] as float
	voice.pitch_scale = _random.randf_range(pitch[0] as float, pitch[1] as float)
	voice.play()
	played.emit(role)


## The tone's sound, made the first time it is asked for.
func _stream(tone: String) -> AudioStream:
	if not _cache.has(tone):
		_cache[tone] = UiTones.make(tone)
	return _cache[tone]
