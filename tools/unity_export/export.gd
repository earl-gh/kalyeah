extends SceneTree
## Exports what the Unity port needs from the Godot game, so the two can be checked against each other:
##   unity/Data/*.json         every resource under data/ (rules, weapons, characters, bot levels), property by property
##   unity/Golden/rng.json     Godot's RandomNumberGenerator for a few seeds (randi, randf, randi_range, randf_range)
##   unity/Golden/<scenario>.json scripted 5v5 matches (SCENARIOS) stepped tick by tick: inputs in, state and signals out
## Run: godot --headless --path . -s tools/unity_export/export.gd

const OUT: String = "res://unity/"


func _initialize() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUT + "Data"))
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUT + "Golden"))
	var count: int = _export_dir("res://data/")
	print("exported resources: ", count)
	_export_rng()
	_export_matches()
	quit()


func _export_dir(dir: String) -> int:
	var count: int = 0
	for file: String in DirAccess.get_files_at(dir):
		if file.ends_with(".tres"):
			var res: Resource = load(dir + file)
			var path: String = (dir + file).trim_prefix("res://data/").replace("/", "__").trim_suffix(".tres") + ".json"
			_write(OUT + "Data/" + path, {"source": dir + file, "class": _class_of(res), "data": _resource(res, 0)})
			count += 1
	for sub: String in DirAccess.get_directories_at(dir):
		if sub != "i18n":
			count += _export_dir(dir + sub + "/")
	return count


func _class_of(res: Resource) -> String:
	var script: Script = res.get_script() as Script
	if script != null and script.get_global_name() != &"":
		return String(script.get_global_name())
	return res.get_class()


func _resource(res: Resource, depth: int) -> Variant:
	if res == null:
		return null
	if depth > 0 and res.resource_path.begins_with("res://data/") and not res.resource_path.contains("::"):
		return {"$ref": res.resource_path}
	var out: Dictionary = {"$class": _class_of(res)}
	for prop: Dictionary in res.get_property_list():
		if not (int(prop["usage"]) & PROPERTY_USAGE_STORAGE) or not (int(prop["usage"]) & PROPERTY_USAGE_SCRIPT_VARIABLE):
			continue
		out[prop["name"]] = _value(res.get(prop["name"]), depth)
	return out


func _value(v: Variant, depth: int) -> Variant:
	match typeof(v):
		TYPE_OBJECT:
			return _resource(v as Resource, depth + 1) if v is Resource else null
		TYPE_VECTOR2, TYPE_VECTOR2I:
			return {"x": v.x, "y": v.y}
		TYPE_VECTOR3, TYPE_VECTOR3I:
			return {"x": v.x, "y": v.y, "z": v.z}
		TYPE_RECT2, TYPE_RECT2I:
			return {"x": v.position.x, "y": v.position.y, "w": v.size.x, "h": v.size.y}
		TYPE_COLOR:
			return {"r": v.r, "g": v.g, "b": v.b, "a": v.a}
		TYPE_STRING_NAME, TYPE_NODE_PATH:
			return String(v)
		TYPE_ARRAY, TYPE_PACKED_STRING_ARRAY, TYPE_PACKED_INT32_ARRAY, TYPE_PACKED_FLOAT32_ARRAY, TYPE_PACKED_FLOAT64_ARRAY, TYPE_PACKED_VECTOR2_ARRAY:
			var arr: Array = []
			for item: Variant in v:
				arr.append(_value(item, depth))
			return arr
		TYPE_DICTIONARY:
			var d: Dictionary = {}
			for key: Variant in v:
				d[str(key)] = _value(v[key], depth)
			return d
		TYPE_FLOAT:
			return v
		_:
			return v


func _export_rng() -> void:
	var out: Dictionary = {}
	for seed_value: int in [0, 1, 7, 12345, 2026, 987654321]:
		var rng: RandomNumberGenerator = RandomNumberGenerator.new()
		rng.seed = seed_value
		var seq: Array = []
		for i: int in 40:
			match i % 4:
				0: seq.append({"op": "randi", "v": rng.randi()})
				1: seq.append({"op": "randf", "v": rng.randf()})
				2: seq.append({"op": "randi_range", "a": -5, "b": 17, "v": rng.randi_range(-5, 17)})
				3: seq.append({"op": "randf_range", "a": -2.5, "b": 4.0, "v": rng.randf_range(-2.5, 4.0)})
		out[str(seed_value)] = seq
	_write(OUT + "Golden/rng.json", out)


## The recorded matches: `sweep` (every kid on a fixed formula of the tick, far apart), `brawl` (both teams close together, aiming at
## each other, with every button: hits, tags, all-tagged points, effects, power-ups), `capture` (one team walks into the other's base,
## a scripted tag and a rescue, overtime). `rules` overrides game_rules.tres for that match (the port applies the same).
const SCENARIOS: Array[Dictionary] = [
	{"name": "match_1", "seed": 1, "kind": "sweep", "ticks": 900},
	{"name": "match_7", "seed": 7, "kind": "sweep", "ticks": 900},
	{"name": "match_2026", "seed": 2026, "kind": "sweep", "ticks": 900},
	{"name": "brawl_11", "seed": 11, "kind": "brawl", "ticks": 1500},
	{"name": "brawl_cards_5", "seed": 5, "kind": "brawl", "ticks": 1500,
		"rules": {"card_cycle": true, "elixir_enabled": true, "stamina_enabled": true, "hand_size": 4, "sprint_enabled": true}},
	{"name": "brawl_cooldown_cards_8", "seed": 8, "kind": "brawl", "ticks": 1200,
		"rules": {"card_cycle": true, "elixir_enabled": false, "stamina_enabled": true, "hand_size": 3}},
	{"name": "capture_3", "seed": 3, "kind": "capture", "ticks": 3300,
		"rules": {"overtime_after": 15.0, "extended_seconds": 5.0, "overtime_seconds": 30.0, "overtime_capture_time": 3.0,
			"rescue_cooldown": 2.0, "rescue_hold_time": 1.5}},
	{"name": "practice_4", "seed": 4, "kind": "practice", "ticks": 1500, "rules": {"rescue_cooldown": 2.0, "rescue_hold_time": 1.5}},
]


## Who plays: `id`, `team`, and for a training dummy its `brain` (stand, patrol, rescuer) and spot (`x`, `y`; a patrol also `origin`,
## `range`, `speed`). Practice: three kids and an ally rescuer dummy against three dummies (one patrolling); the rest: 5v5 kids.
func _roster(kind: String) -> Array:
	if kind == "practice":
		return [
			{"id": 1, "team": 0}, {"id": 2, "team": 0}, {"id": 3, "team": 0},
			{"id": 4, "team": 0, "brain": "rescuer", "x": -62.0, "y": 24.0},
			{"id": 6, "team": 1, "brain": "stand", "x": -8.0, "y": -2.0},
			{"id": 7, "team": 1, "brain": "patrol", "x": 8.0, "y": -4.0, "origin": 8.0, "range": 6.0, "speed": 0.6},
			{"id": 8, "team": 1, "brain": "stand", "x": 0.0, "y": -10.0},
		]
	var roster: Array = []
	for id: int in range(1, 11):
		roster.append({"id": id, "team": 0 if id <= 5 else 1})
	return roster


func _brain(entry: Dictionary) -> DummyBrain:
	match entry["brain"]:
		"patrol":
			return DummyBrain.patrolling(entry["origin"], entry["range"], entry["speed"])
		"rescuer":
			return DummyBrain.rescuer(Vector2(entry["x"], entry["y"]))
	return DummyBrain.standing()


func _export_matches() -> void:
	for scenario: Dictionary in SCENARIOS:
		_export_match(scenario)


## Every player's PlayerInput is logged in `inputs[t]` as [id, move.x, move.y, aim.x, aim.y, buttons, move_target.x, move_target.y,
## aim_point.x, aim_point.y], then `sim.step(1/30)`; `frames[t]` is the state after the step and `events` every signal with its tick.
## `place` lists where the scenario put players before the first tick; `script` the sim calls it made between ticks (before the inputs).
func _export_match(scenario: Dictionary) -> void:
	var seed_value: int = scenario["seed"]
	var ticks: int = scenario["ticks"]
	var kind: String = scenario["kind"]
	var overrides: Dictionary = scenario.get("rules", {})
	var rules: GameRules = (load("res://data/rules/game_rules.tres") as GameRules).duplicate() as GameRules
	for key: String in overrides:
		rules.set(key, overrides[key])
	var layout: MapLayout = load("res://data/rules/map_layout.tres")
	var sim: MatchSim = MatchSim.new(rules, layout, seed_value)
	var roster: Array = _roster(kind)
	var ids: Array[int] = []
	var humans: Array[int] = []
	for entry: Dictionary in roster:
		var id: int = entry["id"]
		ids.append(id)
		if entry.has("brain"):
			sim.add_dummy(id, entry["team"], Vector2(entry["x"], entry["y"]), _brain(entry))
		else:
			sim.add_player(id, entry["team"])
			humans.append(id)
	sim.assign_characters()
	for id: int in ids:
		sim.equip(id, true)
	var place: Array = []
	if kind == "brawl":
		for id: int in ids:
			var row: int = (id - 1) % 5
			var pos: Vector2 = Vector2(-8.0 + 4.0 * row, 8.0 if id <= 5 else -8.0)
			sim.players[id].position = pos
			place.append([id, pos.x, pos.y])
	elif kind == "practice":
		for id: int in humans:
			var pos: Vector2 = Vector2(-4.0 + 4.0 * (id - 1), 8.0)
			sim.players[id].position = pos
			place.append([id, pos.x, pos.y])
	elif kind == "capture":
		for id: int in range(1, 6):
			var pos: Vector2 = Vector2(-6.0 + 3.0 * (id - 1), -30.0)
			sim.players[id].position = pos
			place.append([id, pos.x, pos.y])
	var events: Array = []
	sim.player_tagged.connect(func(id: int, by: int) -> void: events.append({"t": sim.tick, "e": "tagged", "id": id, "by": by}))
	sim.player_rescued.connect(func(id: int, by: int) -> void: events.append({"t": sim.tick, "e": "rescued", "id": id, "by": by}))
	sim.point_scored.connect(func(team: int, how: int) -> void: events.append({"t": sim.tick, "e": "point", "team": team, "how": how}))
	sim.match_won.connect(func(team: int) -> void: events.append({"t": sim.tick, "e": "won", "team": team}))
	sim.point_reset.connect(func() -> void: events.append({"t": sim.tick, "e": "reset"}))
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
	var frames: Array = []
	var start: Array = []
	for id: int in ids:
		var s: PlayerState = sim.players[id]
		start.append({"id": id, "team": s.team, "character": String(s.character_id), "hand": _strs(s.weapons), "next": String(s.next_card), "deck": _strs(s.deck)})
	var script_log: Array = []
	var inputs_log: Array = []
	for t: int in ticks:
		if kind == "capture" and (t == 30 or t == 360) and not sim.players[6].tagged:
			sim.kill(6)
			script_log.append({"t": t, "kill": 6})
		if kind == "practice" and (t == 60 or t == 700) and not sim.players[2].tagged:
			sim.kill(2)
			script_log.append({"t": t, "kill": 2})
		var tick_inputs: Array = []
		for id: int in humans:
			var input: PlayerInput
			match kind:
				"brawl", "practice":
					input = _brawl_input(sim, id, t)
				"capture":
					input = _capture_input(sim, id, t)
				_:
					input = _sweep_input(id, t)
			sim.set_input(id, input)
			tick_inputs.append([id, input.move.x, input.move.y, input.aim.x, input.aim.y, input.buttons, input.move_target.x, input.move_target.y, input.aim_point.x, input.aim_point.y])
		inputs_log.append(tick_inputs)
		sim.step(1.0 / 30.0)
		frames.append(_frame(sim, ids))
	_write(OUT + "Golden/%s.json" % scenario["name"], {"name": scenario["name"], "kind": kind, "seed": seed_value, "dt": 1.0 / 30.0,
		"rules": overrides, "roster": roster, "place": place, "script": script_log, "start": start, "inputs": inputs_log, "frames": frames, "events": events}, "")


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
			",".join(_strs(s.weapons)), String(s.next_card), s.deck.size(), _sum(s.weapon_cooldowns), 1 if s.overclocked else 0])
	var active: Array = []
	for entry: Dictionary in sim.powerups:
		active.append(1 if entry["active"] else 0)
	return {"tick": sim.tick, "phase": int(sim.phase), "score": [sim.score.points[0], sim.score.points[1]], "capture_team": sim.capture_team,
		"capture_elapsed": sim.capture_elapsed, "capture_needed": sim.capture_needed, "capture_rate": sim.capture_rate,
		"rescue": [sim.rescue_rescuer[0], sim.rescue_rescuer[1], sim.rescue_elapsed[0], sim.rescue_elapsed[1], sim.rescue_ready_left[0], sim.rescue_ready_left[1]],
		"round_time": sim.round_time, "objects": [sim.weapons.projectiles.size(), sim.weapons.zones.size(), sim.weapons.shields.size()],
		"powerups": active, "players": players, "extra": extra}


func _sum(values: Array[float]) -> float:
	var total: float = 0.0
	for v: float in values:
		total += v
	return total


## The original recording: a fixed formula of the tick, nothing read from the match.
func _sweep_input(id: int, t: int) -> PlayerInput:
	var angle: float = float(t) * 0.031 + float(id) * 0.7
	var move: Vector2 = Vector2(cos(angle), sin(angle * 1.3)) * (1.0 if (t / 60 + id) % 3 != 0 else 0.5)
	var aim: Vector2 = Vector2(cos(angle * 0.5 + 1.0), sin(angle * 0.5 + 1.0))
	var buttons: int = 0
	if (t + id * 7) % 45 == 0:
		buttons |= PlayerInput.CARD_BITS[(t / 45 + id) % 4]
	return PlayerInput.create(move, aim, buttons, t)


func _nearest_enemy(sim: MatchSim, s: PlayerState) -> PlayerState:
	var best: PlayerState = null
	var best_d: float = INF
	for other: int in sim.players:
		var o: PlayerState = sim.players[other]
		if o.team != s.team and not o.tagged and s.position.distance_to(o.position) < best_d:
			best_d = s.position.distance_to(o.position)
			best = o
	return best


## Close combat: walk at the nearest enemy (stick, move target, strafe or stand, in turns), cards held a few ticks and released at the
## enemy (a street point, the stick or the auto-aim), plus primary fire, dash, jump, slide, crouch, the ultimate and a cancelled card.
func _brawl_input(sim: MatchSim, id: int, t: int) -> PlayerInput:
	var s: PlayerState = sim.players[id]
	var input: PlayerInput = PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, t)
	if s.tagged:
		return input
	var enemy: PlayerState = _nearest_enemy(sim, s)
	var to: Vector2 = enemy.position - s.position if enemy != null else Vector2(0.0, -1.0 if s.team == 0 else 1.0)
	var dist: float = to.length()
	match (id + t / 90) % 4:
		0:
			input.move = to.normalized().rotated(sin(float(t) * 0.05 + float(id)) * 0.6) * (1.0 if dist > 4.0 else 0.3)
		1:
			if (t + id) % 15 == 0 and enemy != null:
				input.with_move_target(enemy.position + Vector2(float(id % 3) - 1.0, 1.5))
		2:
			input.move = to.normalized().orthogonal() * (0.7 if id % 2 == 0 else 1.0)
	var phase_t: int = (t + id * 5) % 40
	var hold: int = (id % 3) * 6
	var slot_count: int = s.weapons.size()
	if slot_count > 0 and phase_t <= hold:
		var slot: int = ((t + id * 5) / 40 + id) % slot_count
		input.buttons |= PlayerInput.CARD_BITS[slot]
	if slot_count > 0 and phase_t == hold + 1 and enemy != null:
		if id % 2 == 0:
			input.with_aim_point(enemy.position + Vector2(0.3, -0.2))
		elif (t / 40) % 2 == 0:
			input.aim = (to / 20.0).limit_length(1.0)
	if (t + id) % 9 == 0 and dist < 25.0:
		input.buttons |= PlayerInput.BTN_PRIMARY
	if (t + id * 13) % 150 == 0:
		input.buttons |= PlayerInput.BTN_DASH
	if (t + id * 11) % 70 == 0:
		input.buttons |= PlayerInput.BTN_JUMP
	if (t + id * 17) % 110 == 0:
		input.buttons |= PlayerInput.BTN_SLIDE
	if (t + id * 19) % 130 < 10:
		input.buttons |= PlayerInput.BTN_CROUCH
	if (t + id * 3) % 60 == 0:
		input.buttons |= PlayerInput.BTN_ULTIMATE
	if (t + id * 23) % 200 == hold + 1:
		input.buttons |= PlayerInput.BTN_AIM_CANCEL
	return input


## Team 0 walks into team 1's base by move targets; kid 7 walks to its rescue area for kid 6 (tagged by the script at ticks 30 and 360);
## the rest of team 1 stands still until tick 1000 (two base captures, the second one in overtime), then defends with the brawl's cards.
func _capture_input(sim: MatchSim, id: int, t: int) -> PlayerInput:
	var s: PlayerState = sim.players[id]
	if s.tagged or (id >= 6 and id != 7 and t < 1000):
		return PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, t)
	if id <= 5:
		var input: PlayerInput = PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, t)
		if (t + id) % 30 == 0:
			input.with_move_target(sim.layout.base_center(MapLayout.SIDE_ENEMY) + Vector2(float(id - 3) * 2.0, 0.0))
		return input
	if id == 7:
		var rescue: PlayerInput = PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, t)
		if (t + id) % 30 == 0:
			rescue.with_move_target(sim.layout.rescue_rect(MapLayout.SIDE_ENEMY).get_center())
		return rescue
	var fight: PlayerInput = _brawl_input(sim, id, t)
	fight.move = Vector2.ZERO
	fight.buttons &= ~(PlayerInput.BTN_MOVE_TARGET | PlayerInput.BTN_DASH | PlayerInput.BTN_SLIDE)
	return fight


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
