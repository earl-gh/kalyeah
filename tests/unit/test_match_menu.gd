extends GutTest
## The match settings menu and the header it opens from (kept from the old test_local_input.gd; its keyboard tests are gone).


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
	assert_eq(score.size.y, 1280.0, "the score overlay spans the play area")
	assert_lt(hud.settings_button.get_global_rect().get_center().x, 360.0, "the settings button is on the left")


func test_the_match_menu_has_no_hand_buttons_and_right_handed_is_the_default() -> void:
	var hud: MatchHud = autofree(MatchHud.new()) as MatchHud
	add_child(hud)
	assert_false("left_button" in hud.menu, "the hand is chosen in Settings, not with buttons in the match menu")
	var fresh: Node = autofree(preload("res://scripts/autoload/settings.gd").new()) as Node
	assert_false(fresh.get("left_handed") as bool, "right-handed until the player says otherwise")
