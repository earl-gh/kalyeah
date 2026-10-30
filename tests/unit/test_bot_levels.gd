extends GutTest
## Practice difficulty (docs/GDD.md "Practice difficulty"): Easy, Medium and Hard are data (`GameRules.bot_levels`); Medium is the bots'
## numbers as they stand, and the choice made on the Practice dialog is the one the next match against bots plays with.
## Seam: GameRules.with_bot_level, Settings.bot_level, PracticeMatch, TitleScreen.open_practice_confirm.

const TITLE_SCENE: PackedScene = preload("res://scenes/ui/title_screen.tscn")
const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")

var _rules: GameRules
var _saved_level: StringName
var _saved_kind: StringName


func before_each() -> void:
	_rules = load("res://data/rules/game_rules.tres") as GameRules
	_saved_level = Settings.bot_level
	_saved_kind = Session.practice_kind


func after_each() -> void:
	Settings.set_bot_level(_saved_level)
	Session.practice_kind = _saved_kind


func test_there_are_three_levels_in_order_and_medium_is_the_default() -> void:
	var ids: Array[StringName] = []
	for level: BotLevel in _rules.bot_levels:
		ids.append(level.id)
		assert_ne(level.label, "")
		assert_ne(level.blurb, "")
	assert_eq(ids, [&"easy", &"medium", &"hard"] as Array[StringName])
	assert_eq(_rules.bot_level_default, &"medium")


func test_medium_is_the_numbers_the_bots_always_had() -> void:
	var medium: GameRules = _rules.with_bot_level(&"medium")
	assert_eq(medium.bot_skill, _rules.bot_skill)
	assert_eq(medium.bot_aim_error_degrees, _rules.bot_aim_error_degrees)
	assert_eq(medium.bot_cast_gap, _rules.bot_cast_gap)
	assert_eq(medium.bot_aggro_range, _rules.bot_aggro_range)
	assert_eq(medium.bot_assault_delay, _rules.bot_assault_delay)
	assert_eq(medium.bot_assault_window, _rules.bot_assault_window)
	assert_eq(medium.bot_rest_time, _rules.bot_rest_time)


func test_each_step_up_is_harder_in_every_number() -> void:
	var easy: BotLevel = _rules.bot_level(&"easy")
	var medium: BotLevel = _rules.bot_level(&"medium")
	var hard: BotLevel = _rules.bot_level(&"hard")
	assert_lt(easy.skill, medium.skill)
	assert_lt(medium.skill, hard.skill)
	assert_gt(easy.aim_error_degrees, medium.aim_error_degrees)
	assert_gt(medium.aim_error_degrees, hard.aim_error_degrees)
	assert_gt(easy.cast_gap, medium.cast_gap)
	assert_gt(medium.cast_gap, hard.cast_gap)
	assert_lt(easy.aggro_range, medium.aggro_range)
	assert_lt(medium.aggro_range, hard.aggro_range)
	assert_gt(easy.assault_delay, medium.assault_delay)
	assert_gt(medium.assault_delay, hard.assault_delay)
	assert_lt(easy.assault_window, medium.assault_window)
	assert_lt(medium.assault_window, hard.assault_window)
	assert_gt(easy.rest_time, medium.rest_time)
	assert_gt(medium.rest_time, hard.rest_time)


func test_a_level_is_a_copy_and_never_changes_the_shared_rules() -> void:
	var skill: float = _rules.bot_skill
	var hard: GameRules = _rules.with_bot_level(&"hard")
	assert_eq(hard.bot_skill, _rules.bot_level(&"hard").skill)
	assert_eq(_rules.bot_skill, skill, "the shared rules keep their own numbers")
	assert_ne(hard, _rules)
	assert_eq(_rules.with_bot_level(&"nonsense").bot_skill, _rules.bot_level(&"medium").skill, "an unknown level plays as the default")


func test_the_chosen_level_is_remembered() -> void:
	Settings.set_bot_level(&"hard")
	assert_eq(Settings.bot_level, &"hard")
	var file: ConfigFile = ConfigFile.new()
	assert_eq(file.load(Settings.SAVE_PATH), OK)
	assert_eq(String(file.get_value("practice", "bot_level", "")), "hard")


func test_a_match_against_bots_plays_with_the_chosen_level_and_the_shared_rules_stay_put() -> void:
	Session.practice_kind = &"match"
	var shared_skill: float = _rules.bot_skill
	for id: StringName in [&"easy", &"hard"]:
		Settings.set_bot_level(id)
		var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
		add_child(practice)
		practice.set_process(false)
		assert_eq(practice.sim.rules.bot_skill, _rules.bot_level(id).skill, "%s: the sim's bots use it" % id)
		assert_eq(practice.sim.rules.bot_assault_delay, _rules.bot_level(id).assault_delay)
	assert_eq(_rules.bot_skill, shared_skill)


func test_training_does_not_take_a_level() -> void:
	Session.practice_kind = &"training"
	Settings.set_bot_level(&"hard")
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)
	assert_eq(practice.sim.rules.bot_skill, _rules.bot_skill)


func test_the_practice_dialog_offers_the_three_levels_and_remembers_the_pick() -> void:
	Settings.set_bot_level(&"")
	var screen: TitleScreen = autofree(TITLE_SCENE.instantiate()) as TitleScreen
	add_child(screen)
	var card: MenuDialog = screen.open_practice_confirm()
	var choice: UiSegmented = card.find_child("DifficultyChoice", true, false) as UiSegmented
	assert_not_null(choice, "a difficulty choice is on the dialog before Play")
	assert_eq(choice.buttons.size(), 3)
	assert_eq(choice.current, &"medium", "Medium until another is chosen")
	var blurb: Label = card.find_child("DifficultyBlurb", true, false) as Label
	assert_eq(blurb.text, _rules.bot_level(&"medium").blurb)
	choice.buttons[&"hard"].pressed.emit()
	assert_eq(Settings.bot_level, &"hard")
	assert_eq(blurb.text, _rules.bot_level(&"hard").blurb, "the line under it follows the pick")
	card.close(false)
	var again: MenuDialog = screen.open_practice_confirm()
	assert_eq((again.find_child("DifficultyChoice", true, false) as UiSegmented).current, &"hard", "it comes back as it was left")
	again.close(false)
