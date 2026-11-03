class_name SfxView
extends Node
## Plays the owner's sound effects. What the sim does at once (a cast, a hit, a heal, a status, a card drawn, a win) plays from its
## signals; what the picture shows later or in steps (a snip closing, a meteor or can landing, a wall blocking, a trap springing,
## the can leaving) is played by the effect that draws it (`FxContext.play_sound`), so the ear and the eye agree. View only: it reads sim
## events and changes no rule, timing or hit. The same offline and online (docs/ART.md "Audio").

const DIR: String = "res://assets/audio/sfx/"
## Simultaneous sounds; the oldest voice is reused when all are busy.
const VOICES: int = 12

## Weapon id -> sound played when it is cast ("" = silent here; its sound plays elsewhere).
const CAST: Dictionary[StringName, String] = {
	&"bato_light": "tsinelas_throw_light",
	&"bato_heavy": "bato_meteor_fall",
	&"bola": "bola_throw",
	&"gunting_heavy": "gunting_big_charge",
	&"gunting_light": "",
	&"heal": "",
	&"jacks": "jacks_scatter",
	&"langit_lupa": "soil_rise",
	&"lata": "lata_toss",
	&"papel_shield": "paper_shield",
	&"papel_trap": "trap_place",
	&"trumpo": "trumpo",
	&"tsinelas_light": "tsinelas_throw_light",
	&"tsinelas_heavy": "tsinelas_throw_heavy",
}

## Weapon id -> sound of its hit landing on a kid. The others play their own sound where the picture shows the impact
## (a landing, a snip, a bounce, a wall) or on the cast.
const HIT: Dictionary[StringName, String] = {
	&"bato_light": "tsinelas_hit",  # no sound of its own in the folder
	&"tsinelas_light": "tsinelas_hit",
	&"tsinelas_heavy": "tsinelas_hit_heavy",
}

## StatusEffects.Type -> sounds played when it lands. The laugh is only for a kid that tripped while running (TRIPPED, drawn at random by the
## sim): nothing else plays it (a test holds that).
const EFFECT: Dictionary[int, Array] = {
	StatusEffects.Type.STUN: ["stunned"],
	StatusEffects.Type.SLOW: ["slowed_sting"],
	StatusEffects.Type.TRIPPED: ["fall_thud", "laugh_at_fall_thud"],
	StatusEffects.Type.POLYMORPH: ["lata_hit"],  # the kid turns into a can: the can's tink (the pitched one below)
}

## How fast a sound plays (1 = as recorded; faster is higher and shorter). The can's hit is played higher so it reads as a small empty can,
## both when the slipper or foot meets it in Tumbang Preso and when a kid is turned into one in Agawang Base.
const EMPTY_CAN_PITCH: float = 1.5
## The sound audit (owner: "pitch them slightly to match the weapon or the action"). Each sound's own speed, for what its recording is not
## quite: the light scissors' snip is a touch higher and the big pair's (the same recording) lower, the marble's throw is light and quick, the
## paper trap laid down is light paper. Everything else is as the owner recorded it.
const PITCH: Dictionary[String, float] = {
	"lata_hit": EMPTY_CAN_PITCH,
	"gunting_light": 1.1,
	"gunting_big_snip": 0.8,
	"bola_throw": 1.1,
	"trap_place": 1.1,
}
## A sound the weapon borrows from another (it has none of its own in the folder) is sped up or slowed to fit it, on top of `PITCH`: the
## small rock's throw is the light slipper's whoosh, quicker and higher; its hit is the slipper's slap, deeper, a thud.
const CAST_SPEED: Dictionary[StringName, float] = {&"bato_light": 1.2}
const HIT_SPEED: Dictionary[StringName, float] = {&"bato_light": 0.75}
## A sound that plays again and again (a snip, a bounce, each slipper's slap, a card) varies by up to this much either way, a little each
## time, so it never sounds like the same recording twice.
const VARIETY: Dictionary[String, float] = {
	"gunting_light": 0.05, "bola_bounce": 0.05, "tsinelas_hit": 0.04, "card_pick": 0.03, "lata_hit": 0.04,
}

var _sim: MatchSim
var _local_id: int = -1
var _players: Array[AudioStreamPlayer] = []
var _next: int = 0
var _cache: Dictionary[String, AudioStream] = {}
var _random: RandomNumberGenerator = RandomNumberGenerator.new()  # the ear only: nothing the sim does depends on it


func _ready() -> void:
	_random.randomize()
	for i: int in VOICES:
		var player: AudioStreamPlayer = AudioStreamPlayer.new()
		player.bus = Settings.SFX_BUS  # the Sounds switch mutes this bus
		add_child(player)
		_players.append(player)


## Starts listening to `sim`; `local_id` is the kid whose cards make the "card ready" sound.
func watch(sim: MatchSim, local_id: int) -> void:
	_sim = sim
	_local_id = local_id
	# the effects play at the rules' share of the recordings' volume (owner: 0.8)
	for player: AudioStreamPlayer in _players:
		player.volume_db = linear_to_db(clampf(sim.rules.sfx_level, 0.0001, 1.0))
	sim.weapon_cast.connect(_on_cast)
	sim.weapon_hit.connect(_on_hit)
	sim.player_healed.connect(func(_id: int, _amount: int) -> void: play("heal"))
	sim.effect_applied.connect(_on_effect)
	sim.card_drawn.connect(_on_card_drawn)
	sim.match_won.connect(func(_team: int) -> void: play("victory"))


## Plays `sound` (a file name in `assets/audio/sfx/` without `.mp3`) at its own speed (`PITCH`, `VARIETY`) times `speed`; unknown or empty
## names do nothing.
func play(sound: String, speed: float = 1.0) -> void:
	if sound.is_empty() or _players.is_empty():
		return
	var stream: AudioStream = _stream(sound)
	if stream == null:
		return
	var player: AudioStreamPlayer = _players[_next]
	_next = (_next + 1) % _players.size()
	player.stream = stream
	var vary: float = VARIETY.get(sound, 0.0) as float
	player.pitch_scale = (PITCH.get(sound, 1.0) as float) * speed * (1.0 + _random.randf_range(-vary, vary))
	player.play()


func _stream(sound: String) -> AudioStream:
	if not _cache.has(sound):
		var path: String = DIR + sound + ".mp3"
		_cache[sound] = load(path) as AudioStream if ResourceLoader.exists(path) else null
	return _cache[sound]


func _on_cast(_id: int, weapon_id: StringName) -> void:
	play(CAST.get(weapon_id, ""), CAST_SPEED.get(weapon_id, 1.0) as float)


func _on_hit(_id: int, weapon_id: StringName) -> void:
	play(HIT.get(weapon_id, ""), HIT_SPEED.get(weapon_id, 1.0) as float)


func _on_effect(_id: int, type: int) -> void:
	for sound: String in EFFECT.get(type, []):
		play(sound)


func _on_card_drawn(id: int, _slot: int) -> void:
	if id == _local_id:
		play("card_ready")
