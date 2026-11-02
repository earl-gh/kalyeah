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
