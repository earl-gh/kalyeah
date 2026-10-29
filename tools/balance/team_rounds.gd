extends SceneTree
## Plays whole rounds of tag between two teams of bots (TeamBrain) and counts who scores, how fast, and how often nobody does.
## Run headless, after an import:
##   godot --headless -s tools/balance/team_rounds.gd -- [--seeds 80] [--size 3] [--capture 5,8,12] [--seconds 120] [--skill 0.5]
##                                                      [--only rush-defend,mixed-mixed] [--ready 0,10,20]
##                                                      [--skill1 0.2]   (team 1's aim, when it should differ from team 0's)
##                                                      [--elixir 1|0] [--hand 4] [--stamina 1|0] (default: the rules' own)
## Every matchup is rush / defend / mixed (one rusher, the rest defend) for team 0 against team 1, so a team that always wins a mirror
## shows up as a bias, and a rule that makes rushing or turtling always right shows up as a lopsided matchup.
## Bots are crude (no cover use, rushers only rescue by a fixed rule), so read the direction and the size of a gap, not an exact number.
## Besides who scores it prints, per matchup (docs/audits/2026-10-24-tap-move-elixir-stamina.md): casts per player per minute,
## exhaustions per kid per round, the share of tags where every hit on the tagged kid (since it was last at full health) landed within
## BURST_SECONDS, and each weapon's share of all casts. Every player is dealt cards (`MatchSim.equip`), as in a match.

## A tag counts as a burst when every hit on the kid since it was last at full health landed within this many seconds.
const BURST_SECONDS: float = 3.0

const MATCHUPS: Array[Array] = [
	["rush", "rush"], ["rush", "defend"], ["defend", "rush"], ["mixed", "mixed"], ["mixed", "rush"], ["defend", "defend"],
]


func _init() -> void:
	var options: Dictionary[String, String] = _options(OS.get_cmdline_user_args())
	var rules: GameRules = (load("res://data/rules/game_rules.tres") as GameRules).duplicate() as GameRules
	var layout: MapLayout = load("res://data/rules/map_layout.tres") as MapLayout
	var seeds: int = int(options.get("seeds", "80"))
	var sizes: PackedStringArray = options.get("size", "1,3").split(",")
	var captures: PackedStringArray = options.get("capture", str(rules.capture_solo_time)).split(",")
	var readies: PackedStringArray = options.get("ready", str(rules.rescue_cooldown)).split(",")
	var seconds: float = float(options.get("seconds", "120"))
	var skill: float = float(options.get("skill", "0.5"))
	var skill1: float = float(options.get("skill1", str(skill)))
	if options.has("elixir"):
		rules.elixir_enabled = options["elixir"] != "0"
	if options.has("hand"):
		rules.hand_size = int(options["hand"])
	if options.has("stamina"):
		rules.stamina_enabled = options["stamina"] != "0"
	for capture: String in captures:
		for ready: String in readies:
			rules.capture_solo_time = float(capture)
			rules.rescue_cooldown = float(ready)
			print("== capture_solo_time %s, rescue_cooldown %s, cards %s, elixir %s, hand %d, stamina %s" % [
				capture, ready, str(rules.card_cycle), str(rules.elixir_enabled), rules.hand_size,
				str(rules.stamina_enabled),
			])
			for size_text: String in sizes:
				for matchup: Array in MATCHUPS:
					if options.has("only") and not (options["only"] as String).split(",").has("%s-%s" % [matchup[0], matchup[1]]):
						continue
					_report(rules, layout, matchup, int(size_text), seeds, seconds, [skill, skill1])
	quit()


func _report(rules: GameRules, layout: MapLayout, matchup: Array, size: int, seeds: int, seconds: float, skill: Array) -> void:
	var wins: Array[int] = [0, 0]
	var none: int = 0
	var total: float = 0.0
	var tagged: int = 0
	var casts: int = 0
	var played: float = 0.0
	var exhaustions: int = 0
	var tags: int = 0
	var bursts: int = 0
	var rescues: int = 0
	var per_weapon: Dictionary[StringName, int] = {}
	for seed_value: int in seeds:
		var result: Dictionary = play(rules, layout, matchup[0] as String, matchup[1] as String, size, 1000 + seed_value, seconds, skill)
		var winner: int = result["winner"] as int
		if winner < 0:
			none += 1
		else:
			wins[winner] += 1
			total += result["seconds"] as float
		tagged += result["tagged"] as int
		played += result["seconds"] as float
		exhaustions += result["exhaustions"] as int
		tags += result["tags"] as int
		bursts += result["bursts"] as int
		rescues += result["rescues"] as int
		var weapons: Dictionary = result["casts"] as Dictionary
		for weapon: StringName in weapons:
			per_weapon[weapon] = per_weapon.get(weapon, 0) + (weapons[weapon] as int)
			casts += weapons[weapon] as int
	var decided: int = seeds - none
	var kids: int = size * 2
	print("%-7s vs %-7s %dv%d: team0 %3d  team1 %3d  none %3d  avg %5.1fs  tags/round %.1f  rescues/round %.2f  casts/player/min %.2f  exhaustions/kid/round %.2f  burst tags %5.1f%% (%d of %d)" % [
		matchup[0], matchup[1], size, size, wins[0], wins[1], none, total / maxf(1.0, float(decided)), float(tagged) / float(seeds),
		float(rescues) / float(seeds), float(casts) / float(kids) / maxf(played / 60.0, 0.001), float(exhaustions) / float(kids * seeds),
		100.0 * float(bursts) / maxf(float(tags), 1.0), bursts, tags,
	])
	var shares: PackedStringArray = []
	for def: WeaponDef in rules.weapons:
		shares.append("%s %.1f%%" % [def.id, 100.0 * float(per_weapon.get(def.id, 0)) / maxf(float(casts), 1.0)])
	print("    cast share (%d casts): %s" % [casts, ", ".join(shares)])


## One round to the first point (or `seconds`). {winner: team or -1, seconds, tagged: kids tagged when it ended, casts: {weapon id: count},
## exhaustions, tags (every tag in the round), bursts (tags where every hit since full health landed within BURST_SECONDS), rescues
## (kids freed by a teammate; a Training dummy walking out does not count)}.
static func play(rules: GameRules, layout: MapLayout, plan0: String, plan1: String, size: int, seed_value: int, seconds: float, skill: Array) -> Dictionary:
	var sim: MatchSim = MatchSim.new(rules, layout, seed_value)
	var scored: Array[int] = []
	sim.point_scored.connect(func(team: int, _how: int) -> void: scored.append(team))
	var counts: Dictionary = {"exhaustions": 0, "tags": 0, "bursts": 0, "rescues": 0}
	var casts: Dictionary[StringName, int] = {}
	var first_hit: Dictionary[int, float] = {}
	# the handlers hold the players and a clock, never the sim itself (a sim -> handler -> sim cycle would leak every round)
	var players: Dictionary[int, PlayerState] = sim.players
	var clock: Array[float] = [0.0]
	sim.weapon_cast.connect(func(_id: int, weapon: StringName) -> void: casts[weapon] = casts.get(weapon, 0) + 1)
	sim.exhausted.connect(func(_id: int) -> void: counts["exhaustions"] = (counts["exhaustions"] as int) + 1)
	sim.player_damaged.connect(func(id: int, amount: int) -> void:
		if players[id].hp + amount >= rules.player_max_hp:
			first_hit[id] = clock[0])
	sim.player_tagged.connect(func(id: int) -> void:
		counts["tags"] = (counts["tags"] as int) + 1
		if first_hit.has(id) and clock[0] - first_hit[id] <= BURST_SECONDS + MatchSim.EPSILON:
			counts["bursts"] = (counts["bursts"] as int) + 1)
	sim.player_rescued.connect(func(_id: int, by_id: int) -> void:
		if by_id != MatchSim.RELEASED_ALONE:
			counts["rescues"] = (counts["rescues"] as int) + 1)
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
			brain.skill = skill[team] as float
			brains[next_id] = brain
			sim.equip(next_id)
			next_id += 1
	var dt: float = rules.tick_dt()
	var time: float = 0.0
	while time < seconds and scored.is_empty():
		for id: int in brains:
			if not sim.players[id].tagged:
				sim.set_input(id, brains[id].think(sim.players[id], sim))
		sim.step(dt)
		time += dt
		clock[0] = time
	var tagged: int = 0
	for id: int in sim.players:
		if sim.players[id].tagged:
			tagged += 1
	return {
		"winner": scored[0] if not scored.is_empty() else -1, "seconds": time, "tagged": tagged, "casts": casts,
		"exhaustions": counts["exhaustions"], "tags": counts["tags"], "bursts": counts["bursts"], "rescues": counts["rescues"],
	}


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
