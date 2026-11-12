extends GutTest

var _saved_loadout: Array[StringName] = []
## Character and 3-superpower loadout select: all 10 Filipino Barangay kids (`5x2` grid in 1280x720
## widescreen, `10x1` 3:4 row in portrait), 3 superpower sockets (`Q / E / R`), 3-column superpower
## grid, and the kid you pick is the kid you play.

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")

var _saved_avatar: String


func _rules() -> GameRules:
	return load("res://data/rules/game_rules.tres") as GameRules


func before_each() -> void:
	_saved_loadout = Session.saved_loadout.duplicate()
	Session.saved_loadout.clear()
	_saved_avatar = Session.avatar_id


func after_each() -> void:
	Session.saved_loadout = _saved_loadout
	Session.avatar_id = _saved_avatar
	Session.save()


func _picking() -> PracticeMatch:
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	return practice


func _screen(practice: PracticeMatch) -> WeaponPickScreen:
	return practice.get_node("%PickScreen") as WeaponPickScreen


func test_row_one_is_six_profile_cards_in_3_by_4() -> void:
	var screen: WeaponPickScreen = _screen(_picking())
	assert_true(screen.find_child("Characters", true, false) is ScrollContainer, "the kid selector stays scrollable")
	var grid: GridContainer = screen.find_child("CharacterGrid", true, false) as GridContainer
	assert_not_null(grid)
	var cards: Array[Node] = screen.find_children("Profile_*", "Button", true, false)
	assert_eq(cards.size(), 10, "all 10 Filipino Barangay kids are selectable")
	await wait_frames(2)
	var tops: Dictionary = {}
	for card: Node in cards:
		var btn: Button = card as Button
		assert_gt(btn.custom_minimum_size.x, 0.0)
		assert_gt(btn.custom_minimum_size.y, 0.0)
		tops[snappedf(btn.global_position.y, 1.0)] = true
	if grid.columns == LoadoutLayout.WIDE_KID_COLUMNS:
		assert_eq(tops.size(), 2, "10 kids arranged cleanly into 2 rows of 5 in 1280x720 widescreen")
	else:
		assert_eq(tops.size(), 1, "all 10 kids in one horizontal scroll row in portrait")


func test_the_weapons_are_a_grid_of_three_across_and_all_of_them_are_there() -> void:
	var screen: WeaponPickScreen = _screen(_picking())
	var grid: GridContainer = screen.find_child("WeaponGrid", true, false) as GridContainer
	assert_not_null(grid)
	assert_eq(grid.columns, 3)
	assert_eq(grid.get_child_count(), _rules().weapons.size(), "a tile for every superpower")
	assert_true(screen.find_child("Weapons", true, false) is ScrollContainer, "the grid scrolls, the slots and Ready stay")
	assert_null(screen.find_child("Tabs", true, false), "no tabs: one grid")
	await wait_frames(2)
	var tops: Dictionary = {}
	for tile: Node in grid.get_children():
		tops[snappedf((tile as Control).global_position.y, 1.0)] = true
	assert_gt(tops.size(), 1)
	var first_row: Array[float] = []
	for i: int in 3:
		first_row.append((grid.get_child(i) as Control).global_position.y)
	assert_eq(first_row[0], first_row[1], "three tiles in the first row")
	assert_eq(first_row[1], first_row[2])


func test_a_weapon_tile_has_art_a_name_and_its_kind_and_the_info_panel_has_the_words() -> void:
	var screen: WeaponPickScreen = _screen(_picking())
	var tile: Button = screen.find_child("Weapon_lata", true, false) as Button
	var art: SkillCardArt = tile.find_child("Art", true, false) as SkillCardArt
	assert_true(art.is_card(), "the weapon's own card picture")
	var def: WeaponDef = load("res://data/weapons/lata.tres") as WeaponDef
	assert_eq((tile.find_child("WeaponName", true, false) as Label).text, def.display_name)
	assert_eq((tile.find_child("Kind", true, false) as Label).text, "Control")
	assert_null(tile.find_child("Description", true, false), "the words are in the info panel, once")
	screen.tap(&"lata")
	assert_eq(screen.focused, &"lata")
	assert_eq((screen.find_child("Description", true, false) as Label).text, def.description)
	assert_eq((screen.find_child("InfoName", true, false) as Label).text, def.display_name)
	await wait_frames(2)
	assert_lt(art.global_position.y, (tile.find_child("WeaponName", true, false) as Label).global_position.y, "the art is above the name")


func test_every_weapon_has_a_description() -> void:
	for def: WeaponDef in _rules().weapons:
		assert_false(def.description.is_empty(), "%s has a description" % def.id)


func test_your_last_kid_is_selected_first_and_tapping_changes_it() -> void:
	Session.avatar_id = "popoy"
	var practice: PracticeMatch = _picking()
	var screen: WeaponPickScreen = _screen(practice)
	assert_eq(screen.character_id, &"popoy")
	assert_true((screen.find_child("Profile_popoy", true, false) as Button).button_pressed)
	screen.pick_character(&"ligaya")
	assert_eq(screen.character_id, &"ligaya")
	assert_true((screen.find_child("Profile_ligaya", true, false) as Button).button_pressed)
	assert_false((screen.find_child("Profile_popoy", true, false) as Button).button_pressed)


func test_the_kid_you_pick_is_the_kid_you_play_and_your_main_screen_face() -> void:
	Session.avatar_id = "junjun"
	var practice: PracticeMatch = _picking()
	var screen: WeaponPickScreen = _screen(practice)
	screen.pick_character(&"inday")
	screen.tap(&"bato_light")
	screen.tap(&"lata")
	screen.tap(&"papel_shield")
	screen.press_ready()
	assert_eq(practice.sim.players[PracticeMatch.LOCAL_ID].character_id, &"inday")
	assert_eq(Session.avatar_id, "inday", "the main screen shows the kid you picked")
	var view: KidModel = practice._views[PracticeMatch.LOCAL_ID] as KidModel
	assert_eq(view.character.id, &"inday", "the 3D kid is rebuilt as the picked one")


func test_a_team_never_has_the_same_kid_twice() -> void:
	var practice: PracticeMatch = _picking()
	var screen: WeaponPickScreen = _screen(practice)
	screen.pick_character(&"toni")
	screen.tap(&"bato_light")
	screen.tap(&"lata")
	screen.tap(&"papel_shield")
	screen.press_ready()
	var seen: Dictionary = {}
	for id: int in practice.sim.players:
		var state: PlayerState = practice.sim.players[id]
		var key: String = "%d:%s" % [state.team, state.character_id]
		assert_false(seen.has(key), "%s is only on team %d once" % [state.character_id, state.team])
		seen[key] = true
		var view: KidModel = practice._views[id] as KidModel
		assert_eq(view.character.id, state.character_id, "every 3D kid matches its player")


func test_the_other_team_may_have_the_same_kid_but_only_once() -> void:
	var sim: MatchSim = MatchSim.new(_rules(), load("res://data/rules/map_layout.tres") as MapLayout, 1)
	for id: int in [1, 2, 3, 4]:
		sim.add_player(id, 0 if id <= 2 else 1)
	var preset: Array[StringName] = [&"junjun", &"ligaya", &"toni", &"popoy"]
	for id: int in [1, 2, 3, 4]:
		sim.players[id].character_id = preset[id - 1]
	assert_true(sim.set_character(1, &"toni"))
	assert_eq(sim.players[1].character_id, &"toni", "the opponent's toni does not stop us")
	assert_eq(sim.players[3].character_id, &"toni", "and they keep theirs")
	assert_true(sim.set_character(2, &"toni"))
	assert_eq(sim.players[2].character_id, &"toni")
	assert_eq(sim.players[1].character_id, &"ligaya", "but our team has toni only once: the teammate got the old kid")
	var per_team: Dictionary = {}
	for id: int in sim.players:
		var key: String = "%d:%s" % [sim.players[id].team, sim.players[id].character_id]
		assert_false(per_team.has(key), key)
		per_team[key] = true


func test_a_kid_a_teammate_locked_in_is_greyed_out_and_cannot_be_picked() -> void:
	var practice: PracticeMatch = _picking()
	var screen: WeaponPickScreen = _screen(practice)
	screen.pick_character(&"ligaya")
	var taken: Array[StringName] = [&"ligaya"]
	screen.set_taken(taken)
	assert_true(screen.is_taken(&"ligaya"))
	assert_true((screen.find_child("Profile_ligaya", true, false) as Button).disabled)
	assert_ne(screen.character_id, &"ligaya", "a free kid is selected instead")
	var before: StringName = screen.character_id
	screen.pick_character(&"ligaya")
	assert_eq(screen.character_id, before, "cannot be tapped")
	screen.set_taken([] as Array[StringName])
	assert_false((screen.find_child("Profile_ligaya", true, false) as Button).disabled)


func test_the_server_does_not_take_a_kid_from_a_confirmed_teammate() -> void:
	var rules: GameRules = _rules()
	var room: RoomService.Room = RoomService.Room.new()
	for i: int in 2:
		var member: RoomService.Member = RoomService.Member.new()
		member.player_id = i + 1
		member.team = 0
		room.members.append(member)
	var match_: ServerMatch = ServerMatch.new(null, room, rules, load("res://data/rules/map_layout.tres") as MapLayout, NetRules.new(), 5)
	match_.stage = ServerMatch.Stage.PICKING
	match_.pick(1, &"bato_light", &"lata", &"toni", &"papel_shield")
	assert_eq(match_.sim.players[1].character_id, &"toni")
	match_.pick(2, &"bato_light", &"lata", &"toni", &"papel_shield")
	assert_ne(match_.sim.players[2].character_id, &"toni", "player 2 cannot take player 1's kid")
	assert_eq(match_.sim.players[1].character_id, &"toni", "player 1 keeps it")
	assert_eq(match_.taken_kids(2), [&"toni"] as Array[StringName])


func test_the_respawn_swap_screen_only_changes_weapons() -> void:
	var practice: PracticeMatch = _picking()
	var screen: WeaponPickScreen = _screen(practice)
	screen.tap(&"bato_light")
	screen.tap(&"lata")
	screen.tap(&"papel_shield")
	screen.press_ready()
	practice.skip_intro()
	var ids: Array[StringName] = [&"bato_light", &"lata", &"papel_shield"]
	screen.open_swap(WeaponPick.new(ids, 0.0, ids, 3), _rules().weapons, "CHANGE WEAPONS")
	assert_true(screen.swap_mode)
	assert_false((screen.find_child("Characters", true, false) as Control).visible, "a kid is fixed for the match")
	var before: StringName = screen.character_id
	screen.pick_character(&"ligaya")
	assert_eq(screen.character_id, before, "no kid change while tagged")


func test_confirmed_pair_is_remembered_and_validated_on_next_setup() -> void:
	var practice: PracticeMatch = _picking()
	var screen: WeaponPickScreen = _screen(practice)
	screen.tap(&"lata")
	screen.tap(&"trumpo")
	screen.tap(&"papel_shield")
	screen.press_ready()
	var expected: Array[StringName] = [&"lata", &"trumpo", &"papel_shield"]
	assert_eq(Session.saved_loadout, expected)
	var probe: FileAccess = FileAccess.open("user://_test_tmp_session_probe", FileAccess.WRITE)
	if probe != null:
		probe.close()
		DirAccess.remove_absolute(ProjectSettings.globalize_path("user://_test_tmp_session_probe"))
		var saved: ConfigFile = ConfigFile.new()
		assert_eq(saved.load(Session.SAVE_PATH), OK)
		assert_eq(saved.get_value("player", "loadout"), Session.saved_loadout)
	var next: WeaponPickScreen = _screen(_picking())
	assert_eq(next.pick.picks, Session.saved_loadout)
	next.select_slot(0)
	next.tap(&"jacks")
	assert_eq(next.pick.picks, [&"jacks", &"trumpo", &"papel_shield"] as Array[StringName])
	assert_eq(Session.saved_loadout, expected, "changes are saved only after confirmation")
	Session.saved_loadout = [&"missing", &"lata", &"lata", &"jacks", &"papel_shield"]
	var allowed: Array[StringName] = [&"lata", &"jacks", &"papel_shield"]
	assert_eq(Session.loadout_for(allowed, 3), allowed)


func test_multiplayer_pick_keeps_timeout_while_practice_does_not() -> void:
	var screen: WeaponPickScreen = autofree(WeaponPickScreen.new()) as WeaponPickScreen
	add_child(screen)
	var ids: Array[StringName] = []
	for def: WeaponDef in _rules().weapons:
		ids.append(def.id)
	var rng: RandomNumberGenerator = RandomNumberGenerator.new()
	screen.open(WeaponPick.new(ids, 1.0, [] as Array[StringName], 3), _rules().weapons, "", rng)
	screen.tick(2.0)
	assert_false(screen.visible)
	assert_eq(screen.pick.picks.size(), 3)


func test_weapon_statistics_preserve_fractional_range_and_damage_units() -> void:
	var heavy: WeaponDef = load("res://data/weapons/gunting_heavy.tres") as WeaponDef
	assert_eq(LoadoutCards.stat_line(heavy), "38 dmg · CD 9s · Range 9m")
	var light: WeaponDef = load("res://data/weapons/gunting_light.tres") as WeaponDef
	assert_eq(LoadoutCards.stat_line(light), "9 dmg / snip · CD 5.4s · Range 7.5m")
	var field: WeaponDef = load("res://data/weapons/jacks.tres") as WeaponDef
	assert_eq(LoadoutCards.stat_line(field), "7 dmg / 0.5s · CD 10.5s · Range 19m")
