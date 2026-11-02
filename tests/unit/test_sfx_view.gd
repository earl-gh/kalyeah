extends GutTest
## Every sound SfxView can play exists, and every weapon has a cast entry.


func _sounds() -> Array[String]:
	var names: Array[String] = []
	for value: String in SfxView.CAST.values():
		names.append(value)
	for list: Array in SfxView.EFFECT.values():
		for value: String in list:
			names.append(value)
	names.append_array(["bato_heavy_boom", "gunting_light", "gunting_big_snip", "tsinelas_hit", "tsinelas_hit_heavy", "heal", "card_ready", "victory"])
	return names


func test_every_sound_file_exists() -> void:
	for sound: String in _sounds():
		if sound.is_empty():
			continue
		assert_true(ResourceLoader.exists(SfxView.DIR + sound + ".mp3"), sound)


func test_every_weapon_has_a_cast_entry() -> void:
	var dir: DirAccess = DirAccess.open("res://data/weapons")
	for file: String in dir.get_files():
		if file.ends_with(".tres"):
			var def: WeaponDef = load("res://data/weapons/" + file) as WeaponDef
			assert_true(SfxView.CAST.has(def.id), String(def.id))


func test_every_sound_file_is_used_by_the_code() -> void:
	var source: String = ""
	for dir: String in ["res://scripts/view", "res://scripts/view/fx", "res://scripts/view/art"]:
		for file: String in DirAccess.get_files_at(dir):
			if file.ends_with(".gd"):
				source += FileAccess.get_file_as_string(dir + "/" + file)
	for file: String in DirAccess.get_files_at("res://assets/audio/sfx"):
		if file.ends_with(".mp3"):
			assert_true(source.contains("\"" + file.get_basename() + "\""), file + " is never played")


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
