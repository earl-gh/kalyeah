class_name AudioOptions
extends VBoxContainer
## The Sounds and Music switches (owner), shared by the Settings page, the room's settings and the match menu: each mutes its audio bus at once
## (`Settings.set_sounds`, `set_music`) and is saved. There is no music in the game yet: the Music switch is ready for when there is.

var sounds_toggle: UiToggle
var music_toggle: UiToggle


func _ready() -> void:
	name = "AudioOptions"
	add_theme_constant_override("separation", UiMetrics.GAP)
	sounds_toggle = UiToggle.new()
	sounds_toggle.name = "Sounds"
	sounds_toggle.set_state(Settings.sounds)
	sounds_toggle.toggled.connect(Settings.set_sounds)
	add_child(UiKit.setting_row("Sounds", "Throws, hits and the buttons' taps", sounds_toggle))
	add_child(UiKit.divider())
	music_toggle = UiToggle.new()
	music_toggle.name = "Music"
	music_toggle.set_state(Settings.music)
	music_toggle.toggled.connect(Settings.set_music)
	add_child(UiKit.setting_row("Music", "Background music", music_toggle))
	Settings.audio_changed.connect(_refresh)


func _refresh() -> void:
	if is_instance_valid(sounds_toggle):
		sounds_toggle.set_state(Settings.sounds)
	if is_instance_valid(music_toggle):
		music_toggle.set_state(Settings.music)
