extends GutTest
## The Sounds and Music switches (owner): each mutes its audio bus at once, is saved, and sits in the Settings page (Audio), the room's settings
## and the match menu. Every sound effect plays on the Sfx bus. Seam: Settings (values, buses, save file), AudioOptions, SettingsPage.fill.

var _saved_sounds: bool
var _saved_music: bool


func before_each() -> void:
	_saved_sounds = Settings.sounds
	_saved_music = Settings.music
	Settings.set_sounds(true)
	Settings.set_music(true)


func after_each() -> void:
	Settings.set_sounds(_saved_sounds)
	Settings.set_music(_saved_music)


func _muted(bus: StringName) -> bool:
	return AudioServer.is_bus_mute(AudioServer.get_bus_index(bus))


func test_both_are_on_by_default_and_the_buses_exist_and_send_to_master() -> void:
	assert_true(Settings.sounds)
	assert_true(Settings.music)
	for bus: StringName in [Settings.SFX_BUS, Settings.MUSIC_BUS]:
		var index: int = AudioServer.get_bus_index(bus)
		assert_ne(index, -1, "%s exists" % bus)
		assert_eq(AudioServer.get_bus_send(index), &"Master")
		assert_false(_muted(bus))


func test_sounds_off_mutes_the_sound_bus_only_and_music_off_the_music_bus_only() -> void:
	Settings.set_sounds(false)
	assert_true(_muted(Settings.SFX_BUS))
	assert_false(_muted(Settings.MUSIC_BUS), "music is its own switch")
	Settings.set_sounds(true)
	Settings.set_music(false)
	assert_false(_muted(Settings.SFX_BUS))
	assert_true(_muted(Settings.MUSIC_BUS))
	Settings.set_music(true)
	assert_false(_muted(Settings.MUSIC_BUS))


func test_applying_the_audio_twice_makes_no_second_bus() -> void:
	var before: int = AudioServer.bus_count
	Settings.apply_audio()
	Settings.apply_audio()
	assert_eq(AudioServer.bus_count, before)


func test_the_choices_are_saved() -> void:
	Settings.set_sounds(false)
	Settings.set_music(false)
	var file: ConfigFile = ConfigFile.new()
	assert_eq(file.load(Settings.SAVE_PATH), OK)
	assert_eq(file.get_value("audio", "sounds"), false)
	assert_eq(file.get_value("audio", "music"), false)


func test_every_sound_effect_plays_on_the_sound_bus() -> void:
	var sfx: SfxView = add_child_autofree(SfxView.new())
	for voice: AudioStreamPlayer in sfx._players:
		assert_eq(voice.bus, Settings.SFX_BUS, "the match's sounds")
	var ui: UiSound = add_child_autofree(UiSound.new())
	for voice: AudioStreamPlayer in ui._voices:
		assert_eq(voice.bus, Settings.SFX_BUS, "the menus' sounds")


func test_the_switches_show_the_state_and_change_it() -> void:
	var options: AudioOptions = add_child_autofree(AudioOptions.new())
	assert_true(options.sounds_toggle.button_pressed)
	options.sounds_toggle.button_pressed = false
	options.sounds_toggle.toggled.emit(false)
	assert_false(Settings.sounds)
	assert_true(_muted(Settings.SFX_BUS))
	options.music_toggle.button_pressed = false
	options.music_toggle.toggled.emit(false)
	assert_false(Settings.music)
	Settings.set_sounds(true)
	assert_true(options.sounds_toggle.button_pressed, "a switch in another list is followed")


func test_the_settings_page_has_an_audio_section_between_controls_and_graphics() -> void:
	var page: SettingsPage = add_child_autofree(SettingsPage.new())
	await wait_frames(2)
	var audio: Node = page.find_child("Audio", true, false)
	assert_not_null(audio)
	assert_not_null(audio.find_child("Sounds", true, false))
	assert_not_null(audio.find_child("Music", true, false))
	var names: Array[String] = []
	for child: Node in audio.get_parent().get_children():
		names.append(String(child.name))
	assert_lt(names.find("Controls"), names.find("Audio"))
	assert_lt(names.find("Audio"), names.find("Graphics"))


func test_the_room_and_the_match_menu_have_both_switches_too() -> void:
	for in_match: bool in [false, true]:
		var column: VBoxContainer = add_child_autofree(VBoxContainer.new())
		SettingsPage.fill(column, in_match)
		await wait_frames(1)
		assert_not_null(column.find_child("Sounds", true, false), "in_match=%s" % in_match)
		assert_not_null(column.find_child("Music", true, false), "in_match=%s" % in_match)
