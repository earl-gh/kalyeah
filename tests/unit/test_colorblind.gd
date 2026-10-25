extends GutTest
## Team identity without colour alone: a colour-blind palette (blue and orange, a pale bar for you instead of green). The circle and
## diamond that once sat beside every other kid's bar are gone (owner).

var _saved: bool


func before_each() -> void:
	_saved = Settings.colorblind
	Settings.set_colorblind(false)


func after_each() -> void:
	Settings.set_colorblind(_saved)


func test_the_default_palette_is_unchanged() -> void:
	assert_eq(TeamColors.ui_own(), KalyeahTheme.TEAM_BLUE)
	assert_eq(TeamColors.ui_enemy(), KalyeahTheme.TEAM_RED)
	assert_eq(TeamColors.bar_self(), OverheadHud.SELF_COLOR)
	assert_eq(TeamColors.world_enemy(), Palette.TEAM_ENEMY)


func test_the_colour_blind_palette_has_no_red_and_no_green_bar() -> void:
	Settings.set_colorblind(true)
	assert_eq(TeamColors.ui_own(), TeamColors.BLIND_OWN)
	assert_eq(TeamColors.ui_enemy(), TeamColors.BLIND_ENEMY)
	for color: Color in [TeamColors.bar_self(), TeamColors.bar_ally(), TeamColors.bar_enemy()]:
		assert_false(color.g > color.r + 0.2 and color.g > color.b + 0.2, "no green bar left: %s" % str(color))
	assert_gt(TeamColors.BLIND_ENEMY.r, TeamColors.BLIND_ENEMY.b + 0.5, "orange")
	assert_gt(TeamColors.BLIND_OWN.b, TeamColors.BLIND_OWN.r + 0.4, "blue")


func test_the_three_bars_are_told_apart_by_brightness_too() -> void:
	Settings.set_colorblind(true)
	var self_luma: float = TeamColors.bar_self().get_luminance()
	var enemy_luma: float = TeamColors.bar_enemy().get_luminance()
	var ally_luma: float = TeamColors.bar_ally().get_luminance()
	assert_gt(absf(enemy_luma - ally_luma), 0.08, "ally and enemy differ in lightness, not only in hue")
	assert_gt(self_luma, enemy_luma, "you are the brightest")


func test_the_hud_bar_follows_the_setting() -> void:
	var practice: PracticeMatch = autofree(preload("res://scenes/match/practice.tscn").instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	var hud: OverheadHud = practice.find_child("OverheadHud", true, false) as OverheadHud
	if hud == null:
		for node: Node in practice.find_children("*", "Control", true, false):
			if node is OverheadHud:
				hud = node as OverheadHud
	assert_not_null(hud)
	var enemy: PlayerState = practice.sim.players[PracticeMatch.ENEMY_STAND_ID]
	assert_eq(hud.bar_color(enemy), OverheadHud.ENEMY_COLOR)
	Settings.set_colorblind(true)
	assert_eq(hud.bar_color(enemy), TeamColors.bar_enemy())
	assert_ne(hud.bar_color(enemy), OverheadHud.ENEMY_COLOR)


func test_the_bars_carry_no_circle_or_diamond_any_more_owner() -> void:
	var overhead: OverheadHud = autofree(OverheadHud.new()) as OverheadHud
	assert_false(overhead.has_method("_draw_team_mark"), "no circle or diamond beside the health bar (owner); the bar colour and the ring say whose kid it is")
	assert_false(overhead.has_method("_diamond"))


func test_the_setting_is_saved_and_has_a_switch() -> void:
	var options: AccessibilityOptions = autofree(AccessibilityOptions.new()) as AccessibilityOptions
	add_child(options)
	assert_false(options.colorblind_toggle.button_pressed)
	options.colorblind_toggle.button_pressed = true
	assert_true(Settings.colorblind)
	var file: ConfigFile = ConfigFile.new()
	file.load(Settings.SAVE_PATH)
	assert_true(file.get_value("accessibility", "colorblind") as bool)
	Settings.set_colorblind(false)
	assert_false(options.colorblind_toggle.button_pressed, "a change from elsewhere shows here")
