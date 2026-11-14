extends SceneTree
## Records the Godot bots for the Unity port's bot tests (unity/Kalyeah.Sim.Tests/Bots/), so the C# TeamCoach/TeamBrain/FightBrain can be
## checked against the real thing:
##   unity/Golden/Bots/bots_<level>.json  a 5v5 Practice match against bots per bot level (data/rules/bot_levels), set up the way
##                                       scripts/view/practice_match.gd `_setup_bot_match` does it: you (id 1, team 0, scripted inputs),
##                                       allies 2-5 under a TeamCoach following you, enemies 6-10 under their own TeamCoach. Every tick: the
##                                       coaches' plans and jobs, every input, every player's state after the step, and every signal.
##   unity/Golden/Bots/balance_duel.json  BalanceDuel results, and one duel stepped tick by tick.
##   unity/Golden/Bots/ranked.json        RankedSystem tiers and LP outcomes (no randomness).
## The files live in a folder of their own: GoldenMatchTests replays every file directly in unity/Golden/ with its own setup.
## Run: godot --headless --path . -s tools/unity_export/export_bots.gd

const OUT: String = "res://unity/Golden/Bots/"
const TICKS: int = 1800
## The kid you picked (Session.avatar_id): `_assign_unique_roster` puts it first.
const AVATAR: StringName = &"ligaya"
const LEVEL_SEEDS: Dictionary = {&"easy": 101, &"medium": 202, &"hard": 303}
const ALLY_IDS: Array[int] = [2, 3, 4, 5]
const ENEMY_IDS: Array[int] = [6, 7, 8, 9, 10]


func _initialize() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUT))
	var base: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	for level: BotLevel in base.bot_levels:
		_export_bot_match(base, level.id)
	_export_duels(base)
	_export_ranked()
	quit()


# ---- the 5v5 practice match against bots ----------------------------------------------------------

func _export_bot_match(base: GameRules, level: StringName) -> void:
	var seed_value: int = LEVEL_SEEDS.get(level, 1)
	var rules: GameRules = base.with_bot_level(level)
	var layout: MapLayout = load("res://data/rules/map_layout.tres")
	var sim: MatchSim = MatchSim.new(rules, layout, seed_value)
	# _setup_bot_match
	sim.add_player(1, 0)
	var allies: TeamCoach = TeamCoach.new(0, 1)
	var foes: TeamCoach = TeamCoach.new(1)
	for id: int in ALLY_IDS:
		sim.add_player(id, 0)
		allies.add(sim, id, rules.bot_skill)
	for id: int in ENEMY_IDS:
		sim.add_player(id, 1)
		foes.add(sim, id, rules.bot_skill)
	var coaches: Array[TeamCoach] = [allies, foes]
	_assign_unique_roster(sim, rules)
	sim.equip(1)
	for id: int in ALLY_IDS + ENEMY_IDS:
		sim.equip(id, true)
	sim.begin_intro()  # finish_pick: the opening show, then play
	var ids: Array[int] = []
	for id: int in sim.players:
		ids.append(id)
	var start: Array = []
	for id: int in ids:
		var s: PlayerState = sim.players[id]
		start.append({"id": id, "team": s.team, "character": String(s.character_id), "hand": _strs(s.weapons), "next": String(s.next_card),
			"deck": _strs(s.deck), "spawn": [s.spawn_position.x, s.spawn_position.y], "pos": [s.position.x, s.position.y]})
	var coach_start: Array = []
	for coach: TeamCoach in coaches:
		var brains: Array = []
		for id: int in coach.members():
			var brain: TeamBrain = coach._brains[id]
			brains.append([id, brain.home.x, brain.home.y, brain.target.x, brain.target.y, brain.skill, int(brain.role)])
		coach_start.append({"team": coach.team, "follow": coach.follow_id, "brains": brains})
	var events: Array = _hook(sim)
	var inputs_log: Array = []
	var plans: Array = []
	var frames: Array = []
	for t: int in TICKS:
		var playing: bool = sim.phase == MatchSim.Phase.PLAYING
		for coach: TeamCoach in coaches:
			coach.drive(sim)
		var tick_plans: Array = []
		for coach: TeamCoach in coaches:
			tick_plans.append(_plan(sim, coach))
		plans.append(tick_plans)
		var local: PlayerInput = _local_input(sim, t)
		sim.set_input(1, local)
		var tick_inputs: Array = [_input_row(1, local)]
		for coach: TeamCoach in coaches:
			for id: int in coach.members():
				if playing and not sim.players[id].tagged:
					tick_inputs.append(_input_row(id, sim._inputs[id]))
		inputs_log.append(tick_inputs)
		sim.step(1.0 / 30.0)
		frames.append(_frame(sim, ids))
	var name: String = "bots_%s" % level
	_write(OUT + name + ".json", {"name": name, "level": String(level), "seed": seed_value, "dt": 1.0 / 30.0, "avatar": String(AVATAR),
		"bot_skill": rules.bot_skill, "start": start, "coaches": coach_start, "plans": plans, "inputs": inputs_log, "frames": frames,
		"events": events}, "")
	print("%s: %d ticks, %d events, score %d-%d" % [name, TICKS, events.size(), sim.score.points[0], sim.score.points[1]])


## practice_match.gd `_assign_unique_roster`, with AVATAR as the picked kid.
func _assign_unique_roster(sim: MatchSim, rules: GameRules) -> void:
	if rules.characters.size() < sim.players.size():
		sim.assign_characters()
		sim.set_character(1, AVATAR)
		return
	var pool: Array[StringName] = []
	for character: CharacterDef in rules.characters:
		pool.append(character.id)
	for i: int in range(pool.size() - 1, 0, -1):
		var j: int = sim.rng.randi_range(0, i)
		var held: StringName = pool[i]
		pool[i] = pool[j]
		pool[j] = held
	var wanted_idx: int = pool.find(AVATAR)
	if wanted_idx > 0:
		pool[wanted_idx] = pool[0]
		pool[0] = AVATAR
	var cursor: int = 0
	for id: int in sim.players:
		sim.players[id].character_id = pool[cursor]
		cursor += 1


## The coach after its drive: plan, the jobs (recomputed from the same state: they read nothing the drive changed) and each brain.
func _plan(sim: MatchSim, coach: TeamCoach) -> Array:
	var rescuer: int = coach.rescuer_id(sim)
	var brains: Array = []
	for id: int in coach.members():
		var brain: TeamBrain = coach._brains[id]
		brains.append([id, int(brain.role), brain.rescue_duty, 1 if brain.hold else 0, brain.target.x, brain.target.y])
	return [int(coach.plan), rescuer, coach.guard_ids(sim, rescuer), coach.pusher_id(sim, rescuer), coach._holders.duplicate(), brains]


## You: hold at home, then walk to the enemy base by move targets routed through the street, then come back (every 30 s); cards held a
## few ticks and released at the nearest enemy in reach, the slingshot when one is near, a dash now and then.
func _local_input(sim: MatchSim, t: int) -> PlayerInput:
	var s: PlayerState = sim.players[1]
	var input: PlayerInput = PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, t)
	if s.tagged:
		return input
	var cycle: int = t % 900
	var goal: Vector2 = sim.layout.base_center(sim.side_for_team(0))
	if cycle >= 150 and cycle < 620:
		goal = sim.layout.base_center(-sim.side_for_team(0))
	if t % 10 == 0:
		input.with_move_target(sim.nav.next_waypoint(s.position, goal) if s.position.distance_to(goal) > 1.0 else s.position)
	var enemy: PlayerState = sim.nearest_enemy(s, 14.0)
	var phase_t: int = t % 36
	if enemy != null and s.weapons.size() > 0:
		var slot: int = (t / 36) % s.weapons.size()
		if phase_t <= 4:
			input.buttons |= PlayerInput.CARD_BITS[slot]
		elif phase_t == 5:
			input.with_aim_point(enemy.position)
	if enemy != null and t % 11 == 0:
		input.buttons |= PlayerInput.BTN_PRIMARY
		input.with_aim_point(enemy.position)
	if t % 240 == 120:
		input.buttons |= PlayerInput.BTN_DASH
	return input


func _input_row(id: int, input: PlayerInput) -> Array:
	return [id, input.move.x, input.move.y, input.aim.x, input.aim.y, input.buttons, input.move_target.x, input.move_target.y,
		input.aim_point.x, input.aim_point.y, input.tick]


func _hook(sim: MatchSim) -> Array:
	var events: Array = []
	sim.player_tagged.connect(func(id: int, by: int) -> void: events.append({"t": sim.tick, "e": "tagged", "id": id, "by": by}))
	sim.player_rescued.connect(func(id: int, by: int) -> void: events.append({"t": sim.tick, "e": "rescued", "id": id, "by": by}))
	sim.point_scored.connect(func(team: int, how: int) -> void: events.append({"t": sim.tick, "e": "point", "team": team, "how": how}))
	sim.match_won.connect(func(team: int) -> void: events.append({"t": sim.tick, "e": "won", "team": team}))
	sim.point_reset.connect(func() -> void: events.append({"t": sim.tick, "e": "reset"}))
	sim.intro_finished.connect(func() -> void: events.append({"t": sim.tick, "e": "intro"}))
	sim.effect_applied.connect(func(id: int, type: int) -> void: events.append({"t": sim.tick, "e": "effect", "id": id, "type": type}))
	sim.player_healed.connect(func(id: int, amount: int) -> void: events.append({"t": sim.tick, "e": "healed", "id": id, "amount": amount}))
	sim.weapon_cast.connect(func(id: int, w: StringName) -> void: events.append({"t": sim.tick, "e": "cast", "id": id, "w": String(w)}))
	sim.weapon_hit.connect(func(id: int, w: StringName) -> void: events.append({"t": sim.tick, "e": "hit", "id": id, "w": String(w)}))
	sim.card_drawn.connect(func(id: int, slot: int) -> void: events.append({"t": sim.tick, "e": "card", "id": id, "slot": slot}))
	sim.player_damaged.connect(func(id: int, amount: int) -> void: events.append({"t": sim.tick, "e": "damaged", "id": id, "amount": amount}))
	sim.exhausted.connect(func(id: int) -> void: events.append({"t": sim.tick, "e": "exhausted", "id": id}))
	sim.player_dashed.connect(func(id: int) -> void: events.append({"t": sim.tick, "e": "dashed", "id": id}))
	sim.ultimate_cast.connect(func(id: int) -> void: events.append({"t": sim.tick, "e": "ultimate", "id": id}))
	sim.powerup_taken.connect(func(id: int, pid: int, k: int) -> void: events.append({"t": sim.tick, "e": "powerup", "id": id, "stand": pid, "kind": k}))
	return events


## The same frame as tools/unity_export/export.gd, plus each player's move target and stuck time (the bots read them).
func _frame(sim: MatchSim, ids: Array[int]) -> Dictionary:
	var players: Array = []
	var extra: Array = []
	for id: int in ids:
		var s: PlayerState = sim.players[id]
		var effect: int = 0
		for type: int in StatusEffects.Type.values():
			if type != 0 and s.effects.has(type):
				effect = type
		players.append([id, s.position.x, s.position.y, s.hp, 1 if s.tagged else 0, s.elixir, s.stamina])
		extra.append([s.ult_charge, s.air_y, s.facing.x, s.facing.y, s.buff_type, effect, 1 if s.has_move_target else 0,
			",".join(_strs(s.weapons)), String(s.next_card), s.deck.size(), _sum(s.weapon_cooldowns), 1 if s.overclocked else 0,
			s.move_target.x, s.move_target.y, s.stuck_time, s.primary_cd, s.dash_cd, s.buff_left])
	var active: Array = []
	for entry: Dictionary in sim.powerups:
		active.append(1 if entry["active"] else 0)
	return {"tick": sim.tick, "phase": int(sim.phase), "score": [sim.score.points[0], sim.score.points[1]], "capture_team": sim.capture_team,
		"capture_kind": int(sim.capture_kind), "capture_elapsed": sim.capture_elapsed, "capture_needed": sim.capture_needed,
		"capture_rate": sim.capture_rate,
		"rescue": [sim.rescue_rescuer[0], sim.rescue_rescuer[1], sim.rescue_elapsed[0], sim.rescue_elapsed[1], sim.rescue_ready_left[0], sim.rescue_ready_left[1]],
		"round_time": sim.round_time, "objects": [sim.weapons.projectiles.size(), sim.weapons.zones.size(), sim.weapons.shields.size()],
		"powerups": active, "players": players, "extra": extra}


# ---- BalanceDuel ----------------------------------------------------------------------------------

const DUELS: Array[Dictionary] = [
	{"first": [&"bato_light", &"tsinelas_light"], "second": [&"gunting_heavy", &"jacks"], "seed": 5, "seconds": 30.0},
	{"first": [&"bato_light", &"tsinelas_light"], "second": [&"papel_shield", &"papel_trap"], "seed": 1},
	{"first": [&"papel_shield", &"papel_trap"], "second": [&"bato_light", &"tsinelas_light"], "seed": 1},
	{"first": [&"bato_light", &"tsinelas_light"], "second": [&"papel_shield", &"papel_trap"], "seed": 2},
	{"first": [&"bato_heavy", &"heal"], "second": [&"gunting_light", &"lata"], "seed": 9, "skill": 1.0},
	{"first": [&"trumpo", &"bola"], "second": [&"tsinelas_light", &"jacks"], "seed": 13, "skill": 0.5, "cover": true},
	{"first": [&"gunting_heavy", &"tsinelas_heavy"], "second": [&"bato_heavy", &"papel_shield"], "seed": 21, "skill": 0.85, "cover": true},
]


func _export_duels(rules: GameRules) -> void:
	var layout: MapLayout = load("res://data/rules/map_layout.tres")
	var results: Array = []
	for duel: Dictionary in DUELS:
		var first: Array[StringName] = []
		first.assign(duel["first"])
		var second: Array[StringName] = []
		second.assign(duel["second"])
		var seconds: float = duel.get("seconds", BalanceDuel.MAX_SECONDS)
		var skill: float = duel.get("skill", 0.0)
		var cover: bool = duel.get("cover", false)
		var result: Dictionary = BalanceDuel.run(rules, layout, first, second, duel["seed"], seconds, skill, cover)
		results.append({"first": _strs(first), "second": _strs(second), "seed": duel["seed"], "seconds": seconds, "skill": skill, "cover": cover,
			"winner": result["winner"], "at": result["seconds"], "hp": result["hp"]})
	var loadouts: Array = []
	for pair: Array in BalanceDuel.all_loadouts(rules):
		loadouts.append(_strs(pair))
	_write(OUT + "balance_duel.json", {"results": results, "loadouts": loadouts, "trace": _duel_trace(rules, layout)}, "")
	print("balance duels: %d" % results.size())


## The duel of DUELS[4] built the way BalanceDuel.run builds it, stepped tick by tick (positions, hp, inputs, events).
func _duel_trace(rules: GameRules, layout: MapLayout) -> Dictionary:
	var duel: Dictionary = DUELS[4]
	var sim: MatchSim = MatchSim.new(rules, BalanceDuel.arena(layout), duel["seed"])
	var top: Vector2 = Vector2(0.0, -BalanceDuel.START_DISTANCE / 2.0)
	var bottom: Vector2 = Vector2(0.0, BalanceDuel.START_DISTANCE / 2.0)
	var low: FightBrain = FightBrain.fighter(bottom)
	var high: FightBrain = FightBrain.fighter(top)
	low.skill = duel["skill"]
	high.skill = duel["skill"]
	sim.add_dummy(1, 0, bottom, low)
	sim.add_dummy(2, 1, top, high)
	sim.set_loadout(1, duel["first"][0], duel["first"][1])
	sim.set_loadout(2, duel["second"][0], duel["second"][1])
	var events: Array = _hook(sim)
	var ticks: Array = []
	for t: int in 900:
		sim.step(1.0 / 30.0)
		var row: Array = []
		for id: int in [1, 2]:
			var s: PlayerState = sim.players[id]
			var input: PlayerInput = sim._inputs[id]
			row.append([s.position.x, s.position.y, s.hp, 1 if s.tagged else 0, input.buttons, input.move_target.x, input.move_target.y,
				input.aim_point.x, input.aim_point.y])
		ticks.append(row)
		if sim.players[1].tagged or sim.players[2].tagged:
			break
	return {"duel": 4, "ticks": ticks, "events": events}


# ---- RankedSystem ---------------------------------------------------------------------------------

func _export_ranked() -> void:
	var tiers: Array = []
	for lp: int in [-50, 0, 1, 150, 199, 200, 399, 400, 599, 600, 849, 850, 1000, 1149, 1150, 1499, 1500, 2500]:
		tiers.append({"lp": lp, "index": RankedSystem.tier_index(lp), "name": RankedSystem.tier_name(lp), "next": RankedSystem.next_tier_lp(lp),
			"progress": RankedSystem.tier_progress(lp), "id": String(RankedSystem.tier_for_lp(lp)["id"])})
	var previews: Array = []
	for mode: String in ["online", "hotspot", " Practice ", "practice"]:
		for won: bool in [true, false]:
			for scores: Array in [[3, 0], [5, 4], [9, 1], [0, 3], [2, 2]]:
				for lp: int in [0, 10, 190, 195, 840, 1495, 1600]:
					var out: Dictionary = RankedSystem.preview_match(mode, won, scores[0], scores[1], lp)
					previews.append({"mode": mode, "won": won, "my": scores[0], "their": scores[1], "base": lp, "old": out["old_lp"],
						"new": out["new_lp"], "delta": out["delta"], "tier": out["tier"], "promoted": out["promoted"]})
	var saved: String = RankedSystem.path
	RankedSystem.path = "user://export_bots_ranked.cfg"
	RankedSystem.clear()
	var history: Array = []
	var plays: Array = [["online", false, 0, 3], ["online", true, 3, 1], ["practice", true, 5, 0], ["hotspot", true, 3, 0], ["online", false, 1, 3]]
	for i: int in 40:
		var play: Array = plays[i % plays.size()] if i < 10 else ["online", (i % 4) != 0, 3, i % 3]
		var out: Dictionary = RankedSystem.record_match(play[0], play[1], play[2], play[3])
		var last: Dictionary = RankedSystem.consume_last_result()
		history.append({"mode": play[0], "won": play[1], "my": play[2], "their": play[3], "old": out["old_lp"], "new": out["new_lp"],
			"delta": out["delta"], "tier": out["tier"], "promoted": out["promoted"], "lp": RankedSystem.current_lp(), "peak": RankedSystem.peak_lp(),
			"consumed": not last.is_empty()})
	RankedSystem.clear()
	RankedSystem.path = saved
	_write(OUT + "ranked.json", {"tiers": tiers, "previews": previews, "history": history})
	print("ranked: %d previews, %d recorded" % [previews.size(), history.size()])


func _sum(values: Array[float]) -> float:
	var total: float = 0.0
	for v: float in values:
		total += v
	return total


func _strs(v: Variant) -> Array:
	var out: Array = []
	if v is Array:
		for item: Variant in v:
			out.append(String(item) if item != null else "")
	return out


## `indent` "" writes one compact line (the large match recordings).
func _write(path: String, data: Variant, indent: String = " ") -> void:
	var f: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	f.store_string(JSON.stringify(data, indent, false, true))
	f.close()
