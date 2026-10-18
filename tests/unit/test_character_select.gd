extends GutTest

var _saved_loadout: Array[StringName] = []
## The character and weapon select: six 3:4 profile cards, a tab per street game, weapon cards
## (art | name, description below), and the kid you pick is the kid you play.

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
	assert_true(screen.find_child("Characters", true, false) is ScrollContainer, "the kid row stays usable on narrow phones")
	var cards: Array[Node] = screen.find_children("Profile_*", "Button", true, false)
	assert_eq(cards.size(), 6)
	for card: Node in cards:
		var size: Vector2 = (card as Button).custom_minimum_size
		assert_almost_eq(size.x / size.y, 3.0 / 4.0, 0.001, "%s is 3:4" % card.name)
	var tops: Dictionary = {}
	for card: Node in cards:
		tops[snappedf((card as Button).global_position.y, 1.0)] = true
	assert_eq(tops.size(), 1, "all six are in one row")


func test_row_two_has_a_tab_per_street_game_and_one_page_shows() -> void:
	var screen: WeaponPickScreen = _screen(_picking())
	assert_true(screen.find_child("Tabs", true, false) is ScrollContainer, "game tabs scroll instead of clipping")
	var tabs: Array[Node] = screen.find_children("Tab_*", "Button", true, false)
	var games: Array[String] = []
	for def: WeaponDef in _rules().weapons:
		if not games.has(def.street_game):
			games.append(def.street_game)
	assert_eq(tabs.size(), games.size())
	assert_true(games.has("Bato-bato-pik") and games.has("Tumbang preso"))
	var shown: int = 0
	for page: Node in screen.find_children("Page_*", "VBoxContainer", true, false):
		if (page as Control).visible:
			shown += 1
	assert_eq(shown, 1, "only the open tab's weapons show")
	screen.open_tab("Tumbang preso")
	assert_eq(screen.current_tab(), "Tumbang preso")
	assert_true((screen.find_child("Weapon_lata", true, false) as Control).is_visible_in_tree())
	assert_false((screen.find_child("Weapon_bato_light", true, false) as Control).is_visible_in_tree())


func test_a_weapon_card_has_art_then_the_name_with_its_description_below() -> void:
	var screen: WeaponPickScreen = _screen(_picking())
	screen.open_tab("Tumbang preso")
	var card: Button = screen.find_child("Weapon_lata", true, false) as Button
	var art: TextureRect = card.find_children("*", "TextureRect", true, false)[0] as TextureRect
	assert_not_null(art.texture, "painted art")
	var description: Label = card.find_child("Description", true, false) as Label
	assert_false(description.text.is_empty(), "a description")
	var name_label: Label = card.find_children("*", "Label", true, false)[0] as Label
	var def: WeaponDef = load("res://data/weapons/lata.tres") as WeaponDef
	assert_string_contains(name_label.text, def.display_name)
	assert_string_contains(name_label.text, def.kind_label())
	await wait_frames(2)
	assert_lt(art.global_position.x, name_label.global_position.x, "the art is on the left")
	assert_gt(description.global_position.y, name_label.global_position.y, "the description is below the name")


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
	for id: int in [1, 2, 3, 4]:
		sim.players[id].character_id = [&"junjun", &"ligaya", &"toni", &"popoy"][id - 1]
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
	match_.pick(1, &"bato_light", &"lata", &"toni")
	assert_eq(match_.sim.players[1].character_id, &"toni")
	match_.pick(2, &"bato_light", &"lata", &"toni")
	assert_ne(match_.sim.players[2].character_id, &"toni", "player 2 cannot take player 1's kid")
	assert_eq(match_.sim.players[1].character_id, &"toni", "player 1 keeps it")
	assert_eq(match_.taken_kids(2), [&"toni"] as Array[StringName])


func test_the_respawn_swap_screen_only_changes_weapons() -> void:
	var practice: PracticeMatch = _picking()
	var screen: WeaponPickScreen = _screen(practice)
	screen.tap(&"bato_light")
	screen.tap(&"lata")
	screen.press_ready()
	practice.skip_intro()
	var ids: Array[StringName] = [&"bato_light", &"lata"]
	screen.open_swap(WeaponPick.new(ids, 0.0, ids), _rules().weapons, "CHANGE WEAPONS")
	assert_true(screen.swap_mode)
	assert_false((screen.find_child("Characters", true, false) as Control).visible, "a kid is fixed for the match")
	var before: StringName = screen.character_id
	screen.pick_character(&"ligaya")
	assert_eq(screen.character_id, before, "no kid change in the box")


func test_confirmed_pair_is_remembered_and_validated_on_next_setup() -> void:
	var practice: PracticeMatch = _picking()
	var screen: WeaponPickScreen = _screen(practice)
	screen.tap(&"lata")
	screen.tap(&"trumpo")
	screen.press_ready()
	assert_eq(Session.saved_loadout, [&"lata", &"trumpo"] as Array[StringName])
	var saved: ConfigFile = ConfigFile.new()
	assert_eq(saved.load(Session.SAVE_PATH), OK)
	assert_eq(saved.get_value("player", "loadout"), Session.saved_loadout)
	var next: WeaponPickScreen = _screen(_picking())
	assert_eq(next.pick.picks, Session.saved_loadout)
	next.select_slot(0)
	next.tap(&"jacks")
	assert_eq(next.pick.picks, [&"jacks", &"trumpo"] as Array[StringName])
	assert_eq(Session.saved_loadout, [&"lata", &"trumpo"] as Array[StringName], "changes are saved only after confirmation")
	Session.saved_loadout = [&"missing", &"lata", &"lata", &"jacks"]
	assert_eq(Session.loadout_for([&"lata", &"jacks"] as Array[StringName]), [&"lata", &"jacks"] as Array[StringName])


func test_multiplayer_pick_keeps_timeout_while_practice_does_not() -> void:
	var screen: WeaponPickScreen = autofree(WeaponPickScreen.new()) as WeaponPickScreen
	add_child(screen)
	var ids: Array[StringName] = []
	for def: WeaponDef in _rules().weapons:
		ids.append(def.id)
	var rng: RandomNumberGenerator = RandomNumberGenerator.new()
	screen.open(WeaponPick.new(ids, 1.0), _rules().weapons, "", rng)
	screen.tick(2.0)
	assert_false(screen.visible)
	assert_eq(screen.pick.picks.size(), 2)


func test_weapon_statistics_preserve_fractional_range_and_damage_units() -> void:
	var heavy: WeaponDef = load("res://data/weapons/gunting_heavy.tres") as WeaponDef
	assert_string_contains(LoadoutCards.stat_line(heavy), "Range 3.5m")
	var light: WeaponDef = load("res://data/weapons/gunting_light.tres") as WeaponDef
	assert_string_contains(LoadoutCards.stat_line(light), "4 dmg / snip")
	var field: WeaponDef = load("res://data/weapons/jacks.tres") as WeaponDef
	assert_string_contains(LoadoutCards.stat_line(field), "3 dmg / 0.5s")
