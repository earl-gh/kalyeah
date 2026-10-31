extends GutHookScript
## Runs once before the whole suite. Practice's sparring partner (`practice_fighters`) is switched off for every
## test, so the many tests that watch a practice match are not at the mercy of a bot's random weapons and a real
## cast; the tests of the bot itself ask for it explicitly (test_fight_brain.gd).


func run() -> void:
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	rules.practice_fighters = 0
	# The weapon cards (`card_cycle`) are on in the game; most of the suite is about the older pick of two weapons, so it runs with the
	# cards off. The tests of the cards (tests/unit/test_cards.gd) switch them on for themselves and put it back.
	rules.card_cycle = false
	# Practice plays Agawan Base unless a test picks a street game; what an earlier run saved must not change that
	Session.practice_game = &"street"
