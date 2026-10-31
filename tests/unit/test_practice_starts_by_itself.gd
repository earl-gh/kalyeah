extends GutTest
## Practice must start by itself, the way a phone opens it: no test shortcut (`finish_pick`, `skip_intro`). With cards the kid pick
## finishes at once (Practice's kid is the main screen's), so the match must not wait for it: the opening show counts down, then
## everyone plays. Regression: the pick flag was set again after the pick had already finished, and every Practice game stood still.

const PRACTICE_SCENE: PackedScene = preload("res://scenes/match/practice.tscn")
const DT: float = 1.0 / 30.0

var _kind: StringName
var _game: StringName
var _cards: bool


## As the phone plays it: the weapon cards on (tests/pre_run.gd turns them off for the older two-weapon tests, which is how this slipped by).
func before_each() -> void:
	_kind = Session.practice_kind
	_game = Session.practice_game
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	_cards = rules.card_cycle
	rules.card_cycle = true


func after_each() -> void:
	Session.practice_kind = _kind
	Session.practice_game = _game
	(load("res://data/rules/game_rules.tres") as GameRules).card_cycle = _cards


func _open(kind: StringName, game: StringName) -> PracticeMatch:
	Session.practice_kind = kind
	Session.practice_game = game
	var practice: PracticeMatch = autofree(PRACTICE_SCENE.instantiate()) as PracticeMatch
	add_child(practice)
	practice.set_process(false)  # the test steps it, frame by frame, the same way `_process` does
	return practice


func _assert_plays(practice: PracticeMatch, what: String) -> void:
	assert_false(practice.is_picking(), "%s: no pick to wait for" % what)
	for i: int in ceili((practice.rules.intro_time + 1.0) / DT):
		practice.advance(DT)
	assert_eq(practice.sim.phase, MatchSim.Phase.PLAYING, "%s: the opening show ends and play starts" % what)
	assert_gt(practice.sim.match_time, 0.0, "%s: the clock runs" % what)
	assert_true(practice.controls_active(), "%s: the controls answer" % what)


func test_every_practice_game_starts_by_itself() -> void:
	for game: StringName in [&"street", &"patintero", &"tumbang_preso"]:
		_assert_plays(_open(&"match", game), String(game))


func test_training_starts_by_itself() -> void:
	_assert_plays(_open(&"training", &"street"), "training")


func test_the_bots_move_once_it_plays() -> void:
	var practice: PracticeMatch = _open(&"match", &"street")
	var start: Dictionary = {}
	for id: int in practice.sim.players:
		start[id] = practice.sim.players[id].position
	for i: int in ceili((practice.rules.intro_time + practice.rules.bot_assault_delay + 3.0) / DT):
		practice.advance(DT)
	var moved: int = 0
	for id: int in practice.sim.players:
		if id != practice.local_id and practice.sim.players[id].position.distance_to(start[id]) > 0.5:
			moved += 1
	assert_gt(moved, 0, "the bots are out of their bases")
