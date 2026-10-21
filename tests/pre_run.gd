extends GutHookScript
## Runs once before the whole suite. Practice's sparring partner (`practice_fighters`) is switched off for every
## test, so the many tests that watch a practice match are not at the mercy of a bot's random weapons and a real
## cast; the tests of the bot itself ask for it explicitly (test_fight_brain.gd).


func run() -> void:
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	rules.practice_fighters = 0
