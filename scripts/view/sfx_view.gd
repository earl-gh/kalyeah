class_name SfxView
extends Node
## Plays the owner's sound effects. What the sim does at once (a cast, a hit, a heal, a status, a card drawn, a win) plays from its
## signals; what the picture shows later or in steps (a snip closing, a meteor or can landing, a wall blocking, a trap springing,
## the can leaving) is played by the effect that draws it (`FxContext.play_sound`), so the ear and the eye agree. View only: it reads sim
## events and changes no rule, timing or hit. The same offline and online (docs/ART.md "Audio").

const DIR: String = "res://assets/audio/sfx/"
## Simultaneous sounds; the oldest voice is reused when all are busy.
const VOICES: int = 12
## A hit of this much damage or more plays the heavy hit.
const HEAVY_HIT: int = 20

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

## StatusEffects.Type -> sounds played when it lands.
const EFFECT: Dictionary[int, Array] = {
	StatusEffects.Type.STUN: ["stunned"],
	StatusEffects.Type.SLOW: ["slowed_sting"],
	StatusEffects.Type.BOUNCE: ["bola_bounce"],
	StatusEffects.Type.TRIPPED: ["fall_thud", "laugh_at_fall_thud"],
}

var _sim: MatchSim
var _local_id: int = -1
var _players: Array[AudioStreamPlayer] = []
var _next: int = 0
var _cache: Dictionary[String, AudioStream] = {}


func _ready() -> void:
	for i: int in VOICES:
		var player: AudioStreamPlayer = AudioStreamPlayer.new()
		add_child(player)
		_players.append(player)


## Starts listening to `sim`; `local_id` is the kid whose cards make the "card ready" sound.
func watch(sim: MatchSim, local_id: int) -> void:
	_sim = sim
	_local_id = local_id
	sim.weapon_cast.connect(_on_cast)
	sim.player_damaged.connect(_on_damaged)
	sim.player_healed.connect(func(_id: int, _amount: int) -> void: play("heal"))
	sim.effect_applied.connect(_on_effect)
	sim.card_drawn.connect(_on_card_drawn)
	sim.match_won.connect(func(_team: int) -> void: play("victory"))


## Plays `sound` (a file name in `assets/audio/sfx/` without `.mp3`); unknown or empty names do nothing.
func play(sound: String) -> void:
	if sound.is_empty() or _players.is_empty():
		return
	var stream: AudioStream = _stream(sound)
	if stream == null:
		return
	var player: AudioStreamPlayer = _players[_next]
	_next = (_next + 1) % _players.size()
	player.stream = stream
	player.play()


func _stream(sound: String) -> AudioStream:
	if not _cache.has(sound):
		var path: String = DIR + sound + ".mp3"
		_cache[sound] = load(path) as AudioStream if ResourceLoader.exists(path) else null
	return _cache[sound]


func _on_cast(_id: int, weapon_id: StringName) -> void:
	play(CAST.get(weapon_id, ""))


func _on_damaged(_id: int, amount: int) -> void:
	play("tsinelas_hit_heavy" if amount >= HEAVY_HIT else "tsinelas_hit")


func _on_effect(_id: int, type: int) -> void:
	for sound: String in EFFECT.get(type, []):
		play(sound)


func _on_card_drawn(id: int, _slot: int) -> void:
	if id == _local_id:
		play("card_ready")
