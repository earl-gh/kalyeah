extends GutTest
## Regression coverage for short-phone loadout space and unambiguous local identity.

var _scale: float


func before_each() -> void:
	_scale = Settings.text_scale


func after_each() -> void:
	Settings.set_text_scale(_scale)
	await wait_process_frames(3)


func test_first_weapon_details_and_ready_fit_the_short_phone_at_every_reading_size() -> void:
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
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
	screen.open(WeaponPick.new(ids, 10.0), rules.weapons, "", RandomNumberGenerator.new(), rules.characters, &"ligaya", true)
	var scroll: ScrollContainer = screen.find_child("Weapons", true, false) as ScrollContainer
	var first: Button = screen.find_child("Weapon_bato_light", true, false) as Button
	for scale: float in UiAccessibility.SCALES:
		Settings.set_text_scale(scale)
		await wait_process_frames(8)
		var stats: Label = first.find_child("Stats", true, false) as Label
		assert_lte(stats.get_global_rect().end.y, scroll.get_global_rect().end.y, "first weapon stats fit at %s" % scale)
		assert_lte(first.get_global_rect().end.x, scroll.get_global_rect().end.x)
		assert_lte(screen._ready_button.get_global_rect().end.y, phone.get_global_rect().end.y)
		assert_gte(screen._ready_button.size.y, UiMetrics.TOUCH)
		assert_false(scroll.is_ancestor_of(screen._ready_button))
		assert_eq((first.find_child("Description", true, false) as Label).get_theme_font_size("font_size"), roundi(UiMetrics.BODY * scale), "reading text remains enlarged")
		for portrait: Button in screen._profiles.values():
			assert_gte(portrait.size.x, UiMetrics.TOUCH, "compact portraits keep touch width")
	screen.open(WeaponPick.new(ids, 10.0), rules.weapons, "", RandomNumberGenerator.new(), rules.characters, &"ligaya")
	await wait_process_frames(8)
	assert_lte((first.find_child("Stats", true, false) as Label).get_global_rect().end.y, scroll.get_global_rect().end.y, "timed setup also fits with its explanation visible")
	assert_true(screen._ring.visible)
	screen.tap(&"bato_light")
	screen.tap(&"bato_heavy")
	screen.select_slot(0)
	screen.tap(&"lata")
	assert_eq(screen.pick.picks, [&"lata", &"bato_heavy"] as Array[StringName])
	assert_false(screen._ready_button.disabled)
	assert_eq(screen.character_id, &"ligaya")


func test_you_stays_in_the_status_tag_for_either_team_with_a_long_name() -> void:
	Settings.set_text_scale(1.4)
	for team: int in 2:
		for status: String in ["Host", "Ready", ""]:
			var member: Dictionary = {"player_id": 1, "name": "LongName1234", "team": team, "ready": status == "Ready"}
			var room: Dictionary = {"you": 1, "host": 1 if status == "Host" else 2, "team_size": 3, "members": [member]}
			var card: Control = autofree(RoomPlayerCards.player_card(member, team, room)) as Control
			card.size = Vector2(308.0, 108.0)
			add_child(card)
			await wait_process_frames(5)
			var name_label: Label = card.find_child("Name", true, false) as Label
			var tag: Control = card.find_child("Tag", true, false) as Control
			assert_eq(name_label.text, "LongName1234", "only the name can be ellipsized")
			assert_not_null(tag, "even an unready local player keeps a You tag")
			if tag == null:
				continue
			assert_eq((tag.get_child(0) as Label).text, "You" + (" · " + status if not status.is_empty() else ""))
			assert_true(card.get_global_rect().encloses(tag.get_global_rect()), "identity stays inside the narrow player card")
			assert_lte(tag.get_global_rect().end.y, name_label.get_global_rect().position.y, "identity never covers the name")
