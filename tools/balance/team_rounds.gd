extends SceneTree
## Plays whole rounds of tag between two teams of bots (TeamBrain) and counts who scores, how fast, and how often nobody does.
## Run headless, after an import:
##   godot --headless -s tools/balance/team_rounds.gd -- [--seeds 80] [--size 3] [--capture 5,8,12] [--seconds 120] [--skill 0.5]
##                                                      [--only rush-defend,mixed-mixed]
## Every matchup is rush / defend / mixed (one rusher, the rest defend) for team 0 against team 1, so a team that always wins a mirror
## shows up as a bias, and a rule that makes rushing or turtling always right shows up as a lopsided matchup.
## Bots are crude (no cover use, rushers only rescue by a fixed rule), so read the direction and the size of a gap, not an exact number.

const MATCHUPS: Array[Array] = [
	["rush", "rush"], ["rush", "defend"], ["defend", "rush"], ["mixed", "mixed"], ["mixed", "rush"], ["defend", "defend"],
]


func _init() -> void:
	var options: Dictionary[String, String] = _options(OS.get_cmdline_user_args())
	var rules: GameRules = (load("res://data/rules/game_rules.tres") as GameRules).duplicate() as GameRules
	var layout: MapLayout = load("res://data/rules/map_layout.tres") as MapLayout
	var seeds: int = int(options.get("seeds", "80"))
	var sizes: PackedStringArray = options.get("size", "1,3").split(",")
	var captures: PackedStringArray = options.get("capture", str(rules.capture_time)).split(",")
	var seconds: float = float(options.get("seconds", "120"))
	var skill: float = float(options.get("skill", "0.5"))
	for capture: String in captures:
		rules.capture_time = float(capture)
		print("== capture_time %s, contested %s" % [capture, str(rules.capture_contested)])
		for size_text: String in sizes:
			for matchup: Array in MATCHUPS:
				if options.has("only") and not (options["only"] as String).split(",").has("%s-%s" % [matchup[0], matchup[1]]):
					continue
				_report(rules, layout, matchup, int(size_text), seeds, seconds, skill)
	quit()


func _report(rules: GameRules, layout: MapLayout, matchup: Array, size: int, seeds: int, seconds: float, skill: float) -> void:
	var wins: Array[int] = [0, 0]
	var none: int = 0
	var total: float = 0.0
	var tagged: int = 0
	for seed_value: int in seeds:
		var result: Dictionary = play(rules, layout, matchup[0] as String, matchup[1] as String, size, 1000 + seed_value, seconds, skill)
		var winner: int = result["winner"] as int
		if winner < 0:
			none += 1
		else:
			wins[winner] += 1
			total += result["seconds"] as float
		tagged += result["tagged"] as int
	var decided: int = seeds - none
	print("%-7s vs %-7s %dv%d: team0 %3d  team1 %3d  none %3d  avg %5.1fs  tags/round %.1f" % [matchup[0], matchup[1], size, size, wins[0], wins[1], none, total / maxf(1.0, float(decided)), float(tagged) / float(seeds)])


## One round to the first point (or `seconds`). {winner: team or -1, seconds, tagged: kids tagged when it ended}.
static func play(rules: GameRules, layout: MapLayout, plan0: String, plan1: String, size: int, seed_value: int, seconds: float, skill: float) -> Dictionary:
	var sim: MatchSim = MatchSim.new(rules, layout, seed_value)
	var scored: Array[int] = []
	sim.point_scored.connect(func(team: int, _how: int) -> void: scored.append(team))
	var plans: Array[String] = [plan0, plan1]
	var brains: Dictionary[int, TeamBrain] = {}
	var next_id: int = 1
	for team: int in 2:
		var side: int = sim.side_for_team(team)
		for slot: int in size:
			var state: PlayerState = sim.add_player(next_id, team)
			var rushes: bool = plans[team] == "rush" or (plans[team] == "mixed" and slot == 0)
			var brain: TeamBrain
			if rushes:
				var spot: Vector2 = sim.layout.spawn_center(-side) + Vector2((float(slot) - float(size - 1) / 2.0) * 1.2, 0.0)
				brain = TeamBrain.rusher(state.spawn_position, spot)
			else:
				brain = TeamBrain.defender(state.spawn_position)
			brain.skill = skill
			brains[next_id] = brain
			var pair: Array[StringName] = sim.random_loadout()
			sim.set_loadout(next_id, pair[0], pair[1])
			next_id += 1
	var dt: float = rules.tick_dt()
	var time: float = 0.0
	while time < seconds and scored.is_empty():
		for id: int in brains:
			if not sim.players[id].tagged:
				sim.set_input(id, brains[id].think(sim.players[id], sim))
		sim.step(dt)
		time += dt
	var tagged: int = 0
	for id: int in sim.players:
		if sim.players[id].tagged:
			tagged += 1
	return {"winner": scored[0] if not scored.is_empty() else -1, "seconds": time, "tagged": tagged}


func _options(args: PackedStringArray) -> Dictionary[String, String]:
	var found: Dictionary[String, String] = {}
	var i: int = 0
	while i < args.size():
		if args[i].begins_with("--") and i + 1 < args.size():
			found[args[i].trim_prefix("--")] = args[i + 1]
			i += 2
		else:
			i += 1
	return found
