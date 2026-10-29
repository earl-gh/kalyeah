extends SceneTree
## Plays Practice matches vs bots with nobody at the controls and measures how the bots play (docs/audits for the numbers). Your kid (id 1)
## is played by a stand-in: `--you rush` (always attacks the enemy base) or `--you home` (fights from its own base). Both teams are the
## real TeamCoach + TeamBrain of Practice.
## Run headless, after an import:
##   godot --headless -s tools/balance/practice_bots.gd -- [--seeds 40] [--seconds 180] [--you rush|home]
##                                                        [--set bot_rescue_aggro_range=4,bot_rest_time=8] (any GameRules number)
## Prints, averaged over the seeds:
##   enemy_start      seconds into a round before the first enemy bot leaves its base
##   ally_wait        seconds an ally bot waits tagged before it is freed (or the round ends)
##   you_wait         the same for your stand-in
##   rescues          rescues per match by a bot, for your team and the enemy team
##   idle_rescue      seconds per match a bot stands in its own rescue area while the area is still counting down
##   undefended       seconds per match a base is captured with no bot of its team inside it
##   points           points per match for your team and the enemy team, and matches won

const DT: float = 1.0 / 30.0
const YOU: int = 1
const ALLIES: Array[int] = [2, 3]
const ENEMIES: Array[int] = [4, 5, 6]


func _init() -> void:
	var options: Dictionary = {}
	var args: PackedStringArray = OS.get_cmdline_user_args()
	for i: int in range(0, args.size() - 1, 2):
		options[args[i].trim_prefix("--")] = args[i + 1]
	var seeds: int = int(options.get("seeds", "40"))
	var seconds: float = float(options.get("seconds", "180"))
	var you: String = options.get("you", "rush")
	var rules: GameRules = (load("res://data/rules/game_rules.tres") as GameRules).duplicate() as GameRules
	for pair: String in (options.get("set", "") as String).split(",", false):
		rules.set(pair.get_slice("=", 0), float(pair.get_slice("=", 1)))
	var layout: MapLayout = load("res://data/rules/map_layout.tres") as MapLayout
	var total: Dictionary = {"enemy_start": 0.0, "starts": 0, "ally_wait": 0.0, "ally_tags": 0, "you_wait": 0.0, "you_tags": 0,
		"idle_rescue": 0.0, "undefended": 0.0, "rescues_ours": 0, "rescues_theirs": 0, "ours": 0, "theirs": 0, "won": 0, "lost": 0}
	for seed_value: int in seeds:
		_play(rules, layout, seed_value, seconds, you, total)
	print("== you %s, %d seeds, %d s, bot_assault_delay %s, bot_rest_time %s, rescue_cooldown %s, bot_rescue_aggro_range %s" % [
		you, seeds, int(seconds), str(rules.bot_assault_delay), str(rules.bot_rest_time), str(rules.rescue_cooldown),
		str(rules.bot_rescue_aggro_range)])
	print("enemy_start  %.1f s" % (total["enemy_start"] / maxf(float(total["starts"]), 1.0)))
	print("ally_wait    %.1f s  (%d tags)" % [total["ally_wait"] / maxf(float(total["ally_tags"]), 1.0), total["ally_tags"]])
	print("you_wait     %.1f s  (%d tags)" % [total["you_wait"] / maxf(float(total["you_tags"]), 1.0), total["you_tags"]])
	print("rescues      %.2f ours, %.2f theirs per match" % [float(total["rescues_ours"]) / float(seeds), float(total["rescues_theirs"]) / float(seeds)])
	print("idle_rescue  %.1f s per match" % (total["idle_rescue"] / float(seeds)))
	print("undefended   %.1f s per match" % (total["undefended"] / float(seeds)))
	print("points       %.2f ours, %.2f theirs per match; won %d, lost %d, unfinished %d" % [
		float(total["ours"]) / float(seeds), float(total["theirs"]) / float(seeds), total["won"], total["lost"],
		seeds - int(total["won"]) - int(total["lost"])])
	quit()


func _play(rules: GameRules, layout: MapLayout, seed_value: int, seconds: float, you: String, total: Dictionary) -> void:
	var sim: MatchSim = MatchSim.new(rules, layout, seed_value)
	sim.add_player(YOU, 0)
	var allies: TeamCoach = TeamCoach.new(0, YOU)
	var foes: TeamCoach = TeamCoach.new(1)
	for id: int in ALLIES:
		sim.add_player(id, 0)
		allies.add(sim, id, rules.bot_skill)
	for id: int in ENEMIES:
		sim.add_player(id, 1)
		foes.add(sim, id, rules.bot_skill)
	sim.assign_characters()
	for id: int in sim.players:
		sim.equip(id, true)
	var stand_in: TeamBrain = TeamBrain.member(sim.players[YOU].spawn_position, layout.base_center(-sim.side_for_team(0)))
	stand_in.skill = rules.bot_skill
	stand_in.role = TeamBrain.Role.RUSH if you == "rush" else TeamBrain.Role.DEFEND
	sim.player_rescued.connect(func(id: int, by: int) -> void:
		if by >= 0:
			total["rescues_ours" if sim.players[id].team == 0 else "rescues_theirs"] += 1)
	var waiting: Dictionary[int, float] = {}
	var started: bool = false
	var round_seen: float = -1.0
	for i: int in ceili(seconds / DT):
		if sim.phase == MatchSim.Phase.MATCH_OVER:
			break
		if sim.phase == MatchSim.Phase.PLAYING:
			if sim.round_time < round_seen:
				started = false
			round_seen = sim.round_time
			allies.drive(sim)
			foes.drive(sim)
			if not sim.players[YOU].tagged:
				sim.set_input(YOU, stand_in.think(sim.players[YOU], sim))
			_measure(sim, waiting, total)
			if not started:
				for id: int in ENEMIES:
					if not layout.in_base(sim.side_for_team(1), sim.players[id].position):
						started = true
						total["enemy_start"] += sim.round_time
						total["starts"] += 1
						break
		sim.step(DT)
	for id: int in waiting:
		_count_wait(id, waiting[id], total)
	total["ours"] += sim.score.points[0]
	total["theirs"] += sim.score.points[1]
	if sim.phase == MatchSim.Phase.MATCH_OVER:
		if sim.score.points[0] > sim.score.points[1]:
			total["won"] += 1
		else:
			total["lost"] += 1


func _measure(sim: MatchSim, waiting: Dictionary[int, float], total: Dictionary) -> void:
	for id: int in sim.players:
		var state: PlayerState = sim.players[id]
		if state.tagged:
			waiting[id] = state.tag_time
		elif waiting.has(id):
			_count_wait(id, waiting[id], total)
			waiting.erase(id)
		if not state.tagged and sim.rescue_ready_left[state.team] > 0.0 and id != YOU and sim.objectives.in_rescue_area(sim, state):
			total["idle_rescue"] += DT
	if sim.capture_team >= 0 and sim.capture_kind == MatchSim.CaptureKind.BASE:
		var defending: int = 1 - sim.capture_team
		var home: int = sim.side_for_team(defending)
		var anyone: bool = false
		for id: int in sim.players:
			var state: PlayerState = sim.players[id]
			if state.team == defending and id != YOU and not state.tagged and sim.layout.in_base(home, state.position):
				anyone = true
		if not anyone:
			total["undefended"] += DT


func _count_wait(id: int, seconds: float, total: Dictionary) -> void:
	if id == YOU:
		total["you_wait"] += seconds
		total["you_tags"] += 1
	elif ALLIES.has(id):
		total["ally_wait"] += seconds
		total["ally_tags"] += 1
