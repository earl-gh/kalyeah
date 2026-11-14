extends SceneTree
## Records the street games for the Unity port (unity/Kalyeah.Sim/Street/), so the C# port can be checked against the Godot game:
##   unity/Golden/Street/street_<game>.json   a practice street match (as scripts/view/practice_match.gd `_setup_street_game` sets it up:
##   you, kid 1, plus kids 2..6 played by a StreetModeCoach), stepped tick by tick: every input in; the players, the game's whole
##   snapshot, the projectiles and a few of the game's queries out each tick; every signal of the sim and of the game with its tick.
## Kid 1's input is a fixed formula of the tick and the match state (it never draws on the match's random numbers), so the port only
## replays it; the bots are played by the port's own coach and their inputs are checked too.
## Run: godot --headless --path . -s tools/unity_export/export_street.gd

const OUT: String = "res://unity/Golden/Street/"
const DT: float = 1.0 / 30.0
const LOCAL: int = 1
const ALLIES: Array[int] = [2, 3]
const FOES: Array[int] = [4, 5, 6]

## `game` overrides the street game's numbers (shorter matches keep the files small), `rules` game_rules.tres's; `extra` seconds are
## played after the match clock (a tie plays on) before the recording stops anyway.
const SCENARIOS: Array[Dictionary] = [
	{"name": "street_patintero", "game_id": "patintero", "seed": 21, "game": {"match_seconds": 75.0}, "extra": 30.0},
	{"name": "street_tumbang_preso", "game_id": "tumbang_preso", "seed": 22, "game": {"match_seconds": 75.0}, "extra": 30.0},
	{"name": "street_langit_lupa", "game_id": "langit_lupa", "seed": 23, "game": {"match_seconds": 100.0},
		"rules": {"stamina_enabled": true}, "extra": 30.0},
]


func _initialize() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUT))
	for scenario: Dictionary in SCENARIOS:
		_record(scenario)
	quit()


func _record(scenario: Dictionary) -> void:
	var rules: GameRules = (load("res://data/rules/game_rules.tres") as GameRules).duplicate() as GameRules
	var rule_overrides: Dictionary = scenario.get("rules", {})
	for key: String in rule_overrides:
		rules.set(key, rule_overrides[key])
	var game: StreetModeRules = (StreetMode.rules_for(StringName(scenario["game_id"])) as StreetModeRules).duplicate() as StreetModeRules
	var game_overrides: Dictionary = scenario.get("game", {})
	for key: String in game_overrides:
		game.set(key, game_overrides[key])
	var layout: MapLayout = load("res://data/rules/map_layout.tres")
	var sim: MatchSim = MatchSim.new(rules, layout, scenario["seed"])
	# practice_match.gd `_setup_street_game`
	var for_all: bool = StreetMode.create(game).is_free_for_all()
	sim.add_player(LOCAL, 0)
	var coach: StreetModeCoach = StreetModeCoach.new()
	for id: int in ALLIES:
		sim.add_player(id, 0)
		coach.add(id)
	for id: int in FOES:
		sim.add_player(id, 0 if for_all else 1)
		coach.add(id)
	sim.assign_characters()
	sim.set_character(LOCAL, &"kiko")
	sim.start_mode(StreetMode.create(game))
	sim.begin_intro()
	var ids: Array[int] = []
	ids.assign(sim.players.keys())
	var start: Array = []
	for id: int in ids:
		var s: PlayerState = sim.players[id]
		start.append({"id": id, "team": s.team, "character": String(s.character_id), "x": s.position.x, "y": s.position.y})
	var events: Array = []
	_hook(sim, events)
	var frames: Array = []
	var inputs_log: Array = []
	var limit: int = ceili((rules.intro_time + game.roll_seconds + game.match_seconds + float(scenario["extra"])) / DT)
	var after_end: int = -1
	for t: int in limit:
		coach.drive(sim)
		sim.set_input(LOCAL, _local_input(sim, t))
		var row: Array = []
		for id: int in ids:
			var input: PlayerInput = sim._inputs.get(id) as PlayerInput
			if input != null:
				row.append([id, input.move.x, input.move.y, input.aim.x, input.aim.y, input.buttons])
		inputs_log.append(row)
		sim.step(DT)
		frames.append(_frame(sim, ids))
		if sim.phase == MatchSim.Phase.MATCH_OVER and after_end < 0:
			after_end = 15
		if after_end > 0:
			after_end -= 1
			if after_end == 0:
				break
	var result: Array = []
	for id: int in ids:
		var r: Dictionary = sim.mode.local_result(sim, id)
		result.append([id, r["won"], r["mine"], r["theirs"]])
	var ranking: Array = (sim.mode as TayaGame).ranking() if sim.mode is TayaGame else []
	_write(OUT + "%s.json" % scenario["name"], {"name": scenario["name"], "game_id": scenario["game_id"], "seed": scenario["seed"],
		"dt": DT, "rules": rule_overrides, "game": game_overrides, "local": LOCAL, "coached": ALLIES + FOES, "for_all": for_all,
		"start": start, "inputs": inputs_log, "frames": frames, "events": events, "result": result, "ranking": ranking,
		"winner": sim.score.winner, "phase": int(sim.phase)})
	print("%s: %d ticks, %d events, phase %d" % [scenario["name"], frames.size(), events.size(), int(sim.phase)])


func _hook(sim: MatchSim, events: Array) -> void:
	var add: Callable = func(e: String, args: Array) -> void: events.append([sim.tick, e] + args)
	sim.player_tagged.connect(func(id: int, by: int) -> void: add.call("tagged", [id, by]))
	sim.player_rescued.connect(func(id: int, by: int) -> void: add.call("rescued", [id, by]))
	sim.point_scored.connect(func(team: int, how: int) -> void: add.call("point", [team, how]))
	sim.match_won.connect(func(team: int) -> void: add.call("won", [team]))
	sim.point_reset.connect(func() -> void: add.call("reset", []))
	sim.intro_finished.connect(func() -> void: add.call("intro_finished", []))
	sim.weapon_cast.connect(func(id: int, w: StringName) -> void: add.call("cast", [id, String(w)]))
	sim.weapon_hit.connect(func(id: int, w: StringName) -> void: add.call("hit", [id, String(w)]))
	sim.player_damaged.connect(func(id: int, amount: int) -> void: add.call("damaged", [id, amount]))
	sim.exhausted.connect(func(id: int) -> void: add.call("exhausted", [id]))
	var mode: StreetMode = sim.mode
	mode.tagged.connect(func(tagger: int, target: int) -> void: add.call("m_tagged", [tagger, target]))
	mode.scored.connect(func(team: int, id: int) -> void: add.call("m_scored", [team, id]))
	mode.roles_swapped.connect(func(who: int) -> void: add.call("m_roles_swapped", [who]))
	if mode is Patintero:
		var lines: Patintero = mode as Patintero
		lines.runner_crossed.connect(func(id: int) -> void: add.call("runner_crossed", [id]))
		lines.dived.connect(func(id: int, way: Vector2, time: float, recover: float) -> void: add.call("dived", [id, _v(way), time, recover]))
		lines.jailed_runner.connect(func(id: int, side: int) -> void: add.call("jailed_runner", [id, side]))
	if mode is TumbangPreso:
		var tp: TumbangPreso = mode as TumbangPreso
		tp.slipper_thrown.connect(func(id: int) -> void: add.call("slipper_thrown", [id]))
		tp.slipper_landed.connect(func(id: int, at: Vector2, hit: bool) -> void: add.call("slipper_landed", [id, _v(at), hit]))
		tp.can_flung.connect(func(from: Vector2, path: Array[Vector2]) -> void: add.call("can_flung", [_v(from), _path(path)]))
		tp.slipper_flung.connect(func(id: int, from: Vector2, path: Array[Vector2]) -> void: add.call("slipper_flung", [id, _v(from), _path(path)]))
		tp.can_kicked.connect(func(id: int) -> void: add.call("can_kicked", [id]))
		tp.slipper_fetched.connect(func(id: int) -> void: add.call("slipper_fetched", [id]))
		tp.can_placed.connect(func(id: int) -> void: add.call("can_placed", [id]))
		tp.taya_changed.connect(func(old: int, now: int, why: StringName) -> void: add.call("taya_changed", [old, now, String(why)]))
	if mode is LangitLupa:
		var ll: LangitLupa = mode as LangitLupa
		ll.mound_raised.connect(func(id: int) -> void: add.call("mound_raised", [id]))
		ll.mound_fell.connect(func(id: int) -> void: add.call("mound_fell", [id]))
		ll.spot_broke.connect(func(index: int) -> void: add.call("spot_broke", [index]))
		ll.taya_changed.connect(func(old: int, now: int) -> void: add.call("taya_changed", [old, now]))


## The state after one tick: the sim, every player, the game's snapshot, the projectiles and the game's queries (`q`).
func _frame(sim: MatchSim, ids: Array[int]) -> Dictionary:
	var players: Array = []
	for id: int in ids:
		var s: PlayerState = sim.players[id]
		players.append([id, s.position.x, s.position.y, s.facing.x, s.facing.y, s.hp, 1 if s.tagged else 0, s.stamina,
			1 if s.has_stamina else 0, s.cast_lock_left, 1 if s.has_move_target else 0, 1 if s.overclocked else 0])
	var projectiles: Array = []
	for p: WeaponSystem.Projectile in sim.weapons.projectiles:
		projectiles.append([p.id, p.owner_id, p.position.x, p.position.y, p.travelled])
	return {"tick": sim.tick, "phase": int(sim.phase), "intro_left": sim.intro_left, "score": [sim.score.points[0], sim.score.points[1]],
		"winner": sim.score.winner, "log": sim.point_log.size(), "clock": sim.clock_left(), "players": players,
		"mode": _plain(sim.mode.snapshot()), "proj": projectiles, "q": _queries(sim, ids)}


## What the views ask the game, per kid.
func _queries(sim: MatchSim, ids: Array[int]) -> Dictionary:
	var mode: StreetMode = sim.mode
	var per: Array = []
	for id: int in ids:
		var s: PlayerState = sim.players[id]
		var row: Array = [id, 1 if mode.is_attacker(s) else 0, 1 if mode.is_taggable(s) else 0, mode.speed_scale(s), 1 if mode.can_rest(s) else 0]
		if mode is Patintero:
			var lines: Patintero = mode as Patintero
			row.append_array([1 if lines.can_dive(s) else 0, lines.dive_ready_share(id), 1 if lines.is_tagged_out(id) else 0])
		elif mode is TumbangPreso:
			var tp: TumbangPreso = mode as TumbangPreso
			var guide: Dictionary = tp.aim_guide(sim, id)
			row.append_array([1 if tp.can_kick(s) else 0, 1 if tp.can_throw(s) else 0, tp.aim_range(id), 1 if tp.is_held_back(s) else 0,
				[] if guide.is_empty() else [_v(guide["point"]), _v(guide["can_dir"]), _v(guide["slipper_dir"])]])
		elif mode is LangitLupa:
			var ll: LangitLupa = mode as LangitLupa
			row.append_array([1 if ll.is_safe(s) else 0, 1 if ll.can_raise(s) else 0, ll.safe_share(id), ll.spot_at(s.position),
				ll.mound_at(s.position), ll.hidden_from(sim, id)])
		per.append(row)
	var out: Dictionary = {"leader": mode._leader(sim), "resetting": mode.is_resetting(), "kids": per}
	if mode is Patintero:
		out["out"] = (mode as Patintero).runners_out()
	if mode is LangitLupa:
		out["brownout"] = (mode as LangitLupa).brownout_left()
	return out


## Kid 1: a fixed formula of the tick that reads the match (never its random numbers): it plays each game roughly, with every button.
func _local_input(sim: MatchSim, t: int) -> PlayerInput:
	var s: PlayerState = sim.players[LOCAL]
	var input: PlayerInput = PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, t)
	var angle: float = float(t) * 0.031 + 0.7
	var wobble: Vector2 = Vector2(cos(angle), sin(angle * 1.3))
	if sim.mode is TumbangPreso:
		var tp: TumbangPreso = sim.mode as TumbangPreso
		if LOCAL == tp.taya_id:
			if not tp.can_up:
				input.move = _toward(s.position, tp.rules.can_spot if tp.can_carrier == LOCAL else tp.can_position)
			else:
				input.move = (wobble * 0.7).rotated(float(t / 90))
		elif tp.fetched.has(LOCAL):
			input.move = _toward(s.position, Vector2(s.position.x, tp.rules.goal_z - 2.0))
		elif tp.has_slipper(LOCAL):
			var c: int = t % 150
			var hold_ticks: int = 18 + ((t / 150) % 3) * 9
			if not tp.at_home(s.position):
				input.move = _toward(s.position, Vector2(s.position.x, tp.rules.home_z + 1.0))
			elif c >= 40 and c < 40 + hold_ticks:
				input.buttons = PlayerInput.BTN_CARD_1
				if c > 44:
					input.aim = tp.aim_at_can(s.position).rotated(sin(float(t) * 0.13) * 0.04)
			elif c == 40 + hold_ticks and (t / 150) % 4 == 3:
				input.buttons = PlayerInput.BTN_AIM_CANCEL
			else:
				input.move = Vector2(sin(float(t) * 0.05), 0.3) * 0.6
		elif tp.slippers.has(LOCAL):
			input.move = _toward(s.position, tp.slippers[LOCAL])
			if t % 37 == 0:
				input.buttons = PlayerInput.BTN_CARD_2
		else:
			input.move = wobble * 0.3
	elif sim.mode is Patintero:
		var lines: Patintero = sim.mode as Patintero
		if lines.is_attacker(s):
			if lines.is_tagged_out(LOCAL):
				input.move = wobble
			else:
				var going_home: bool = lines.crossed.has(LOCAL)
				var target_z: float = lines.rules.home_z + 1.5 if going_home else lines.rules.goal_z - 1.5
				var way: float = signf(target_z - s.position.y)
				if not going_home and not lines.middle_crossed.has(LOCAL) and absf(s.position.y) < lines.rules.line_span:
					input.move = Vector2(-signf(s.position.x) if absf(s.position.x) > 0.05 else 1.0, way * 0.4).normalized()
				else:
					input.move = Vector2(wobble.x * 0.6, way).normalized()
		else:
			input.move = Vector2(sin(float(t) * 0.04), cos(float(t) * 0.04))
		if t % 53 == 0:
			input.buttons = PlayerInput.BTN_CARD_2
	elif sim.mode is LangitLupa:
		var ll: LangitLupa = sim.mode as LangitLupa
		if LOCAL == ll.taya_id:
			var best: Vector2 = Vector2.ZERO
			var best_d: float = INF
			for id: int in sim.players:
				if id != LOCAL and ll.visible_to(sim, LOCAL, id) and sim.players[id].position.distance_to(s.position) < best_d:
					best_d = sim.players[id].position.distance_to(s.position)
					best = sim.players[id].position
			input.move = _toward(s.position, best)
		elif t % 300 < 100:
			input.move = _toward(s.position, ll.rules.langit_spots[(t / 300) % ll.rules.langit_spots.size()].get_center())
		else:
			input.move = wobble * (0.3 if t % 200 < 100 else 1.0)
		if t % 97 == 0:
			input.buttons = PlayerInput.BTN_CARD_2
	return input


static func _toward(from: Vector2, to: Vector2) -> Vector2:
	var offset: Vector2 = to - from
	return Vector2.ZERO if offset.length() <= 0.2 else offset.normalized()


static func _v(v: Vector2) -> Array:
	return [v.x, v.y]


static func _path(path: Array[Vector2]) -> Array:
	var out: Array = []
	for p: Vector2 in path:
		out.append(_v(p))
	return out


## A snapshot as JSON: Vector2 is [x, y], a Dictionary's keys are strings (in their order), everything else as it is.
static func _plain(value: Variant) -> Variant:
	match typeof(value):
		TYPE_VECTOR2:
			return _v(value)
		TYPE_DICTIONARY:
			var out: Dictionary = {}
			for key: Variant in value:
				out[str(key)] = _plain(value[key])
			return out
		TYPE_ARRAY:
			var arr: Array = []
			for item: Variant in value:
				arr.append(_plain(item))
			return arr
	return value


func _write(path: String, data: Variant) -> void:
	var f: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	f.store_string(JSON.stringify(data, "", false, true))
	f.close()
