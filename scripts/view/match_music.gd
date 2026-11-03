class_name MatchMusic
extends Node
## The match's background music (owner: `assets/audio/bgm.mp3`, 40% volume by default, `GameRules.bgm_volume`). It plays on the Music bus,
## so the Music switch in Settings mutes it, and it loops until the match scene goes away. A phone without the file (or a test run) is
## simply silent. View only: nothing in the sim depends on it.

const PATH: String = "res://assets/audio/bgm.mp3"

var player: AudioStreamPlayer


func _ready() -> void:
	name = "Music"
	player = AudioStreamPlayer.new()
	player.name = "Bgm"
	player.bus = Settings.MUSIC_BUS
	add_child(player)


## Starts the music at `volume` (0 to 1, the rules' `bgm_volume`); does nothing when there is no file.
func start(volume: float) -> void:
	player.volume_db = linear_to_db(clampf(volume, 0.0001, 1.0))
	if not ResourceLoader.exists(PATH):
		return
	var stream: AudioStream = load(PATH) as AudioStream
	if stream is AudioStreamMP3:
		(stream as AudioStreamMP3).loop = true
	player.stream = stream
	player.play()


func is_playing() -> bool:
	return player != null and player.playing
