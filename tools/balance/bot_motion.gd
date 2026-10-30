extends SceneTree
## Plays Practice matches vs bots with nobody at the controls and measures how much the bots stand still and how often a capture
## flickers (docs/GDD.md "Bots and the coach play by the same rules"). Both teams are the real TeamCoach + TeamBrain of Practice; your
## kid (id 1) is a stand-in that always attacks.
## Run headless, after an import:
##   godot --headless -s tools/balance/bot_motion.gd -- [--seeds 20] [--seconds 120] [--set bot_skill=0.4] (any GameRules number)
## Prints, per bot per minute while it is free (not tagged, round playing):
##   still_<reason>  seconds it did not move, by what it was doing: capture (stands in the enemy base), post (in its own base),
##                   rescue (in its rescue area), cast (a card is being cast or held), rest (stamina rest), other
##   moving          seconds it moved
## and per match:
##   flickers        times a running capture stopped and the same team's capture started again within 2 s
##   base_exits      times a bot walked out of the enemy base within 3 s of walking in (in and out again)
## Every number here is for the bots only, never your stand-in.

const DT: float = 1.0 / 30.0
const YOU: int = 1
const ALLIES: Array[int] = [2, 3]
const ENEMIES: Array[int] = [4, 5, 6]
const FLICKER_WINDOW: float = 2.0
const IN_OUT_WINDOW: float = 3.0


func _init() -> void:
	var options: Dictionary = {}
	var args: PackedStringArray = OS.get_cmdline_user_args()
	for i: int in range(0, args.size() - 1, 2):
		options[args[i].trim_prefix("--")] = args[i + 1]
	var seeds: int = int(options.get("seeds", "20"))
	var seconds: float = float(options.get("seconds", "120"))
	var rules: GameRules = (load("res://data/rules/game_rules.tres") as GameRules).duplicate() as GameRules
	for pair: String in (options.get("set", "") as String).split(",", false):
		rules.set(pair.get_slice("=", 0), float(pair.get_slice("=", 1)))
	var layout: MapLayout = load("res://data/rules/map_layout.tres") as MapLayout
	var total: Dictionary = {"moving": 0.0, "flickers": 0, "base_exits": 0, "free": 0.0}
	for seed_value: int in seeds:
		_play(rules, layout, seed_value, seconds, total)
	var minutes: float = maxf(total["free"] / 60.0, 0.001)
	print("== %d seeds, %d s, bot_skill %s" % [seeds, int(seconds), str(rules.bot_skill)])
	print("per bot-minute: moving %.1f s" % (total["moving"] / minutes))
	for key: String in total:
		if key.begins_with("still_"):
			print("  %-14s %.1f s" % [key, total[key] / minutes])
	for key: String in total:
		if key.begins_with("ends_"):
			print("  %-18s %.2f per match" % [key, float(total[key]) / float(seeds)])
	print("per match: flickers %.2f, base_exits %.2f" % [float(total["flickers"]) / float(seeds), float(total["base_exits"]) / float(seeds)])
	quit()


func _play(rules: GameRules, layout: MapLayout, seed_value: int, seconds: float, total: Dictionary) -> void:
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
	stand_in.role = TeamBrain.Role.RUSH
	stand_in.hold = true  # a human who attacks stays in the base
	var brains: Dictionary = {}
	for coach: TeamCoach in [allies, foes]:
		for id: int in coach.members():
			brains[id] = coach._brains[id]
	var last_capture_end: Dictionary = {}
	var last_capture_team: int = -1
	var entered_at: Dictionary = {}
	var was_in: Dictionary = {}
	for i: int in ceili(seconds / DT):
		if sim.phase == MatchSim.Phase.MATCH_OVER:
			break
		if sim.phase == MatchSim.Phase.PLAYING:
			allies.drive(sim)
			foes.drive(sim)
			if not sim.players[YOU].tagged:
				sim.set_input(YOU, stand_in.think(sim.players[YOU], sim))
		var prev_inside: Dictionary = {}
		for id: int in sim.players:
			var was: PlayerState = sim.players[id]
			prev_inside[id] = not was.tagged and sim.layout.in_base(-sim.side_for_team(was.team), was.position)
		var before: Dictionary = {}
		for id: int in brains:
			before[id] = (sim.players[id] as PlayerState).position
		var time: float = float(sim.tick) * DT
		sim.step(DT)
		if sim.phase != MatchSim.Phase.PLAYING:
			continue
		for id: int in brains:
			var state: PlayerState = sim.players[id]
			if state.tagged:
				was_in[id] = false
				continue
			total["free"] += DT
			var enemy_side: int = -sim.side_for_team(state.team)
			var inside: bool = sim.layout.in_base(enemy_side, state.position)
			if inside and not was_in.get(id, false):
				entered_at[id] = time
			if not inside and was_in.get(id, false) and time - (entered_at.get(id, -99.0) as float) < IN_OUT_WINDOW:
				total["base_exits"] += 1
			was_in[id] = inside
			if state.position.distance_squared_to(before[id]) > 0.000001:
				total["moving"] += DT
				continue
			var key: String = "still_other"
			var brain: TeamBrain = brains[id]
			if inside:
				key = "still_capture"
			elif sim.layout.in_base(sim.side_for_team(state.team), state.position):
				key = "still_post"
			elif sim.objectives.in_rescue_area(sim, state):
				key = "still_rescue"
			elif false:
				key = "still_rest"
			elif state.cast_lock_left > 0.0 or brain._press_slot >= 0:
				key = "still_cast"
			else:
				var near: bool = sim.nearest_enemy(state, rules.bot_aggro_range) != null
				key = "still_other_%s_%s_%s" % ["rush" if brain.role == TeamBrain.Role.RUSH else "defend", "fight" if near else "free",
					"cannot_act" if not state.can_act() else ("blocked" if state.has_move_target else ("tired" if state.overclocked else "chose_stop"))]
			total[key] = (total.get(key, 0.0) as float) + DT
		if sim.capture_team != last_capture_team:
			if last_capture_team >= 0:
				last_capture_end[last_capture_team] = time
				var cause: String = "ends_" + _cause(sim, last_capture_team, prev_inside)
				total[cause] = (total.get(cause, 0) as int) + 1
			elif sim.capture_team >= 0 and time - (last_capture_end.get(sim.capture_team, -99.0) as float) < FLICKER_WINDOW:
				total["flickers"] += 1
			last_capture_team = sim.capture_team


## Why the capture of `team` just stopped: someone of the other team came into the base it takes (contested), one of its capturers
## was tagged, one stepped out, or nothing of those.
func _cause(sim: MatchSim, team: int, prev_inside: Dictionary) -> String:
	var other_inside: bool = false
	for id: int in sim.players:
		var state: PlayerState = sim.players[id]
		if state.team != team and not state.tagged and sim.layout.in_base(-sim.side_for_team(state.team), state.position):
			other_inside = true
	if other_inside:
		return "contested"
	var tagged: bool = false
	var stepped: bool = false
	for id: int in sim.players:
		var state: PlayerState = sim.players[id]
		if state.team == team and prev_inside.get(id, false):
			if state.tagged:
				tagged = true
			else:
				stepped = true
	if stepped:
		return "stepped_out"
	return "tagged" if tagged else "other"
