extends GutTest
## Every sound SfxView can play exists, and every weapon has a cast entry.


func _sounds() -> Array[String]:
	var names: Array[String] = []
	for value: String in SfxView.CAST.values():
		names.append(value)
	for list: Array in SfxView.EFFECT.values():
		for value: String in list:
			names.append(value)
	for value: String in SfxView.HIT.values():
		names.append(value)
	names.append_array(["heal", "card_ready", "victory"])
	return names


func test_every_sound_file_exists() -> void:
	var all: Array[String] = _sounds()
	all.append_array(PLAYED_BY.keys())
	for sound: String in all:
		if sound.is_empty():
			continue
		assert_true(ResourceLoader.exists(SfxView.DIR + sound + ".mp3"), sound)


func test_every_weapon_has_a_cast_entry() -> void:
	var dir: DirAccess = DirAccess.open("res://data/weapons")
	for file: String in dir.get_files():
		if file.ends_with(".tres"):
			var def: WeaponDef = load("res://data/weapons/" + file) as WeaponDef
			assert_true(SfxView.CAST.has(def.id), String(def.id))


## Sounds played by an effect where its picture shows the moment (not by SfxView's own tables): sound -> the script that plays it.
const PLAYED_BY: Dictionary[String, String] = {
	"bato_heavy_boom": "res://scripts/view/fx/fx_landings.gd",
	"lata_poof": "res://scripts/view/fx/fx_landings.gd",
	"lata_hit": "res://scripts/view/street_mode_view.gd",
	"paper_block": "res://scripts/view/fx/fx_walls.gd",
	"soil_block": "res://scripts/view/fx/fx_walls.gd",
	"soil_expand": "res://scripts/view/fx/fx_walls.gd",
	"trap_spring": "res://scripts/view/fx/fx_fields.gd",
	"gunting_light": "res://scripts/view/fx/fx_snips.gd",
	"gunting_big_snip": "res://scripts/view/fx/fx_snips.gd",
	"bola_bounce": "res://scripts/view/fx/fx_marble.gd",
	"card_pick": "res://scripts/view/practice_match.gd",
	"card_cancel": "res://scripts/view/practice_match.gd",
}


func test_every_sound_file_is_played_somewhere() -> void:
	var own: Array[String] = _sounds()
	for file: String in DirAccess.get_files_at("res://assets/audio/sfx"):
		if not file.ends_with(".mp3"):
			continue
		var sound: String = file.get_basename()
		if PLAYED_BY.has(sound):
			var call_text: String = "\"" + sound + "\""
			assert_true(FileAccess.get_file_as_string(PLAYED_BY[sound]).contains(call_text), sound + " is not played by " + PLAYED_BY[sound])
		else:
			assert_true(own.has(sound), sound + " is never played")


func test_each_hit_sound_is_per_weapon() -> void:
	assert_eq(SfxView.HIT[&"tsinelas_heavy"], "tsinelas_hit_heavy")
	assert_eq(SfxView.HIT[&"tsinelas_light"], "tsinelas_hit")
	assert_false(SfxView.HIT.has(&"bola"), "the marble has its bounce, not a slipper's hit")


func test_effects_play_sounds_through_the_context() -> void:
	var ctx: FxContext = FxContext.new()
	ctx.play_sound("nothing listens")  # no crash
	var heard: Array[String] = []
	ctx.sound = func(sound_name: String) -> void: heard.append(sound_name)
	ctx.play_sound("lata_hit")
	assert_eq(heard, ["lata_hit"] as Array[String])


func test_unknown_sound_is_silent() -> void:
	var sfx: SfxView = add_child_autofree(SfxView.new())
	sfx.play("nope")
	sfx.play("")
	pass_test("no crash")


func _voice_used(sfx: SfxView) -> AudioStreamPlayer:
	return sfx._players[(sfx._next - 1 + sfx._players.size()) % sfx._players.size()]


func test_the_can_is_played_higher_like_a_small_empty_can_and_everything_else_as_recorded() -> void:
	var sfx: SfxView = add_child_autofree(SfxView.new())
	sfx.play("lata_hit")
	assert_almost_eq(_voice_used(sfx).pitch_scale, SfxView.EMPTY_CAN_PITCH, 0.0001)
	assert_gt(SfxView.EMPTY_CAN_PITCH, 1.0, "higher")
	for sound: String in ["heal", "tsinelas_hit", "lata_poof"]:
		sfx.play(sound)
		assert_eq(_voice_used(sfx).pitch_scale, 1.0, "%s as recorded" % sound)
	for i: int in SfxView.VOICES:
		sfx.play("card_pick")
	sfx.play("lata_hit")
	assert_almost_eq(_voice_used(sfx).pitch_scale, SfxView.EMPTY_CAN_PITCH, 0.0001, "and a voice that was reused takes the new pitch")


func test_a_kid_turned_into_a_can_makes_the_cans_sound() -> void:
	assert_eq(SfxView.EFFECT[StatusEffects.Type.POLYMORPH], ["lata_hit"] as Array)
	var sim: MatchSim = MatchSim.new(load("res://data/rules/game_rules.tres") as GameRules, load("res://data/rules/map_layout.tres") as MapLayout, 3)
	sim.add_player(1, 0)
	sim.add_player(2, 1)
	var sfx: SfxView = add_child_autofree(SfxView.new())
	sfx.watch(sim, 1)
	sim.apply_effect(2, StatusEffects.Type.POLYMORPH, 2.0, 0.0)
	var voice: AudioStreamPlayer = _voice_used(sfx)
	assert_eq(voice.stream.resource_path, SfxView.DIR + "lata_hit.mp3")
	assert_almost_eq(voice.pitch_scale, SfxView.EMPTY_CAN_PITCH, 0.0001, "the same pitched sound as Tumbang Preso's")


func test_the_laugh_plays_only_for_a_kid_that_tripped() -> void:
	var tables: Array[Dictionary] = [SfxView.CAST, SfxView.HIT]
	for table: Dictionary in tables:
		assert_false(table.values().has("laugh_at_fall_thud"), "no weapon plays the laugh")
	for type: int in SfxView.EFFECT:
		var plays: bool = (SfxView.EFFECT[type] as Array).has("laugh_at_fall_thud")
		assert_eq(plays, type == StatusEffects.Type.TRIPPED, "only TRIPPED (the random fall while running) laughs")
	for path: String in ["res://scripts/view/practice_match_presentation.gd", "res://scripts/view/street_mode_view.gd", "res://scripts/view/practice_match.gd"]:
		assert_false(FileAccess.get_file_as_string(path).contains("laugh_at_fall_thud"), "%s never plays the laugh itself" % path)
