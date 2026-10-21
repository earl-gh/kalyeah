extends GutTest
## Round 16 of the interface plan: the loadout as a grid of tiles with one info panel, instead of one tall card per weapon.

const DT: float = 1.0 / 30.0


func _rules() -> GameRules:
	return load("res://data/rules/game_rules.tres") as GameRules


func _screen(height: float = 1280.0, picks: Array[StringName] = []) -> WeaponPickScreen:
	var rules: GameRules = _rules()
	var phone: Control = autofree(Control.new()) as Control
	phone.size = Vector2(720.0, height)
	add_child(phone)
	var screen: WeaponPickScreen = WeaponPickScreen.new()
	phone.add_child(screen)
	screen.set_anchors_preset(Control.PRESET_TOP_LEFT)
	screen.size = phone.size
	var ids: Array[StringName] = []
	for weapon: WeaponDef in rules.weapons:
		ids.append(weapon.id)
	var pick: WeaponPick = WeaponPick.new(ids, 10.0)
	for id: StringName in picks:
		pick.toggle(id)
	screen.open(pick, rules.weapons, "", RandomNumberGenerator.new(), rules.characters, &"ligaya", true)
	return screen


func test_the_panel_starts_on_your_first_weapon_else_the_suggested_one() -> void:
	var picked: WeaponPickScreen = _screen(1280.0, [&"jacks", &"lata"] as Array[StringName])
	assert_eq(picked.focused, &"jacks")
	var fresh: WeaponPickScreen = _screen()
	assert_false(fresh.suggestion.is_empty())
	assert_eq(fresh.focused, fresh.suggestion[0], "no picks yet: the suggested weapon")


func test_a_short_and_a_tall_phone_both_show_whole_rows_of_tiles_above_the_panel() -> void:
	for height: float in [1280.0, 1558.0]:
		var screen: WeaponPickScreen = _screen(height)
		await wait_process_frames(6)
		var scroll: ScrollContainer = screen.find_child("Weapons", true, false) as ScrollContainer
		var visible_tiles: int = 0
		for tile: Node in screen.find_child("WeaponGrid", true, false).get_children():
			var rect: Rect2 = (tile as Control).get_global_rect()
			if scroll.get_global_rect().encloses(rect):
				visible_tiles += 1
		assert_gte(visible_tiles, 3, "a whole row at height %s" % height)
		if height > 1500.0:
			assert_gte(visible_tiles, 6, "two whole rows on a tall phone")


func test_every_tile_is_a_big_target_and_names_fit_inside_it() -> void:
	var screen: WeaponPickScreen = _screen()
	await wait_process_frames(6)
	for tile: Node in screen.find_child("WeaponGrid", true, false).get_children():
		var button: Button = tile as Button
		assert_gte(button.size.y, UiMetrics.TOUCH, "%s is a thumb target" % button.name)
		var name_label: Label = button.find_child("WeaponName", true, false) as Label
		var kind: Label = button.find_child("Kind", true, false) as Label
		assert_lte(kind.get_global_rect().end.y, button.get_global_rect().end.y + 1.0, "%s: the kind stays inside its tile" % button.name)
		assert_lte(name_label.get_global_rect().end.x, button.get_global_rect().end.x, "%s: the name stays inside" % button.name)


func test_the_grid_scrolls_to_the_last_weapon_and_a_tap_there_still_picks_it() -> void:
	var screen: WeaponPickScreen = _screen()
	await wait_process_frames(6)
	var scroll: ScrollContainer = screen.find_child("Weapons", true, false) as ScrollContainer
	scroll.scroll_vertical = 100000
	await wait_process_frames(2)
	var last: Button = screen.find_child("WeaponGrid", true, false).get_child(13) as Button
	assert_true(scroll.get_global_rect().intersects(last.get_global_rect()), "the last tile can be reached")
	last.pressed.emit()
	assert_true(screen.pick.picks.has(StringName(last.name.trim_prefix("Weapon_"))))


func test_the_swap_screen_uses_the_same_grid_and_applies_a_full_pair_at_once() -> void:
	var rules: GameRules = _rules()
	var phone: Control = autofree(Control.new()) as Control
	phone.size = Vector2(720.0, 1280.0)
	add_child(phone)
	var screen: WeaponPickScreen = WeaponPickScreen.new()
	phone.add_child(screen)
	screen.set_anchors_preset(Control.PRESET_TOP_LEFT)
	screen.size = phone.size
	var ids: Array[StringName] = []
	for weapon: WeaponDef in rules.weapons:
		ids.append(weapon.id)
	watch_signals(screen)
	screen.open_swap(WeaponPick.new(ids, 1.0), rules.weapons, "")
	await wait_process_frames(3)
	assert_not_null(screen.find_child("WeaponGrid", true, false))
	screen.tap(&"bato_light")
	screen.tap(&"jacks")
	assert_signal_emitted(screen, "swapped")
