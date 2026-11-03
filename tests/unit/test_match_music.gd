extends GutTest
## The match's background music (`MatchMusic`, 40 % on the Music bus) and the sound effects' 0.8 level (`SfxView`).

const RULES: GameRules = preload("res://data/rules/game_rules.tres")


func test_the_rules_say_40_percent_music_and_0_8_effects() -> void:
	assert_eq(RULES.bgm_volume, 0.4)
	assert_eq(RULES.sfx_level, 0.8)


func test_the_music_plays_on_the_music_bus_at_the_rules_volume() -> void:
	var music: MatchMusic = autofree(MatchMusic.new()) as MatchMusic
	add_child(music)
	music.start(RULES.bgm_volume)
	assert_eq(music.player.bus, Settings.MUSIC_BUS, "the Music switch mutes it")
	assert_almost_eq(music.player.volume_db, linear_to_db(0.4), 0.001, "40 percent")
	assert_eq(MatchMusic.PATH, "res://assets/audio/bgm.mp3")


func test_without_the_file_the_music_is_silent_and_does_not_fail() -> void:
	if ResourceLoader.exists(MatchMusic.PATH):
		pending("the file is there: the other tests cover it")
		return
	var music: MatchMusic = autofree(MatchMusic.new()) as MatchMusic
	add_child(music)
	music.start(0.4)
	assert_false(music.is_playing())


func test_with_the_file_it_plays_and_loops() -> void:
	if not ResourceLoader.exists(MatchMusic.PATH):
		pending("assets/audio/bgm.mp3 is not in the project yet")
		return
	var music: MatchMusic = autofree(MatchMusic.new()) as MatchMusic
	add_child(music)
	music.start(0.4)
	assert_true(music.is_playing())
	if music.player.stream is AudioStreamMP3:
		assert_true((music.player.stream as AudioStreamMP3).loop, "it loops until the match ends")


func test_the_match_sound_effects_play_at_0_8_of_the_recordings() -> void:
	var sim: MatchSim = MatchSim.new(RULES, load("res://data/rules/map_layout.tres") as MapLayout, 1)
	sim.add_player(1, 0)
	var sfx: SfxView = autofree(SfxView.new()) as SfxView
	add_child(sfx)
	sfx.watch(sim, 1)
	for voice: Node in sfx.get_children():
		assert_almost_eq((voice as AudioStreamPlayer).volume_db, linear_to_db(0.8), 0.001)


func test_a_match_scene_has_the_music_node_on_the_music_bus() -> void:
	var practice: PracticeMatch = autofree((load("res://scenes/match/practice.tscn") as PackedScene).instantiate()) as PracticeMatch
	add_child(practice)
	var music: MatchMusic = practice.get_node("Music") as MatchMusic
	assert_not_null(music)
	assert_almost_eq(music.player.volume_db, linear_to_db(practice.rules.bgm_volume), 0.001)
