extends GutTest


func test_other_team_stick_is_rotated_back() -> void:
	assert_eq(LocalInput.to_world(Vector2(0.5, -1.0), false), Vector2(0.5, -1.0))
	assert_eq(LocalInput.to_world(Vector2(0.5, -1.0), true), Vector2(-0.5, 1.0))


func test_keyboard_vector_is_zero_without_keys() -> void:
	assert_eq(LocalInput.keyboard_vector(), Vector2.ZERO)


func test_the_settings_menu_pauses_the_controls() -> void:
	var hud: MatchHud = autofree(MatchHud.new()) as MatchHud
	add_child(hud)
	assert_false(hud.menu.visible)
	assert_not_null(hud.settings_button.texture_normal, "painted gear")
	hud.toggle_menu()
	assert_true(hud.menu.visible)
	assert_false(hud.controls_active(), "no casting through the menu")
	hud.toggle_menu()
	assert_true(hud.controls_active())


func test_the_score_sits_in_the_middle_of_the_header() -> void:
	var screen_root: Control = autofree(Control.new()) as Control
	screen_root.size = Vector2(720.0, 1280.0)
	add_child(screen_root)
	var hud: MatchHud = MatchHud.new()
	screen_root.add_child(hud)
	var score: Rect2 = hud.scoreboard.get_global_rect()
	assert_almost_eq(score.get_center().x, 360.0, 1.0, "in the middle")
	assert_lt(score.end.y, MatchHud.HEADER_HEIGHT, "inside the header")
	assert_lt(hud.settings_button.get_global_rect().get_center().x, 360.0, "the settings button is on the left")


func test_the_match_menu_has_no_hand_buttons_and_right_handed_is_the_default() -> void:
	var hud: MatchHud = autofree(MatchHud.new()) as MatchHud
	add_child(hud)
	assert_false("left_button" in hud.menu, "the hand is chosen in Settings, not with buttons in the match menu")
	var fresh: Node = autofree(preload("res://scripts/autoload/settings.gd").new()) as Node
	assert_false(fresh.get("left_handed") as bool, "right-handed until the player says otherwise")


func test_the_stick_follows_the_turned_camera() -> void:
	# camera behind-right (45 degrees): screen up heads forward-left in the world
	var up: Vector2 = LocalInput.to_world(Vector2(0.0, -1.0), false, 45.0)
	assert_almost_eq(up.x, -0.7071, 0.001)
	assert_almost_eq(up.y, -0.7071, 0.001)
	for stick: Vector2 in [Vector2(0.3, -0.8), Vector2(-1.0, 0.0), Vector2(0.6, 0.6)]:
		for flip: bool in [false, true]:
			var back: Vector2 = LocalInput.to_screen(LocalInput.to_world(stick, flip, 45.0), flip, 45.0)
			assert_almost_eq(back.x, stick.x, 0.0001)
			assert_almost_eq(back.y, stick.y, 0.0001)
