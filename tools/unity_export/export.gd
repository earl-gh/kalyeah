extends SceneTree
## Exports what the Unity port needs from the Godot game, so the two can be checked against each other:
##   unity/Data/*.json         every resource under data/ (rules, weapons, characters, bot levels), property by property
##   unity/Golden/rng.json     Godot's RandomNumberGenerator for a few seeds (randi, randf, randi_range, randf_range)
##   unity/Golden/match_*.json a scripted 5v5 match stepped tick by tick: inputs in, every player's state out
## Run: godot --headless --path . -s tools/unity_export/export.gd

const OUT: String = "res://unity/"


func _initialize() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUT + "Data"))
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUT + "Golden"))
	var count: int = _export_dir("res://data/")
	print("exported resources: ", count)
	_export_rng()
	for seed_value: int in [1, 7, 2026]:
		_export_match(seed_value, 900)
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


## A 5v5 match with every player driven by a fixed formula of the tick (move, aim, buttons): what the port must reproduce.
func _export_match(seed_value: int, ticks: int) -> void:
	var rules: GameRules = load("res://data/rules/game_rules.tres")
	var layout: MapLayout = load("res://data/rules/map_layout.tres")
	var sim: MatchSim = MatchSim.new(rules, layout, seed_value)
	var ids: Array[int] = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]
	for id: int in ids:
		sim.add_player(id, 0 if id <= 5 else 1)
	sim.assign_characters()
	for id: int in ids:
		sim.equip(id, true)
	var events: Array = []
	sim.player_tagged.connect(func(id: int, by: int) -> void: events.append({"t": sim.tick, "e": "tagged", "id": id, "by": by}))
	sim.point_scored.connect(func(team: int, how: int) -> void: events.append({"t": sim.tick, "e": "point", "team": team, "how": how}))
	sim.weapon_cast.connect(func(id: int, w: StringName) -> void: events.append({"t": sim.tick, "e": "cast", "id": id, "w": String(w)}))
	sim.player_damaged.connect(func(id: int, amount: int) -> void: events.append({"t": sim.tick, "e": "damaged", "id": id, "amount": amount}))
	var frames: Array = []
	var start: Array = []
	for id: int in ids:
		var s: PlayerState = sim.players[id]
		start.append({"id": id, "team": s.team, "character": String(s.character_id), "hand": _strs(s.get("hand")), "deck": _strs(s.get("deck"))})
	var inputs_log: Array = []
	for t: int in ticks:
		var tick_inputs: Array = []
		for id: int in ids:
			var angle: float = float(t) * 0.031 + float(id) * 0.7
			var move: Vector2 = Vector2(cos(angle), sin(angle * 1.3)) * (1.0 if (t / 60 + id) % 3 != 0 else 0.5)
			var aim: Vector2 = Vector2(cos(angle * 0.5 + 1.0), sin(angle * 0.5 + 1.0))
			var buttons: int = 0
			if (t + id * 7) % 45 == 0:
				buttons |= PlayerInput.CARD_BITS[(t / 45 + id) % 4]
			var input: PlayerInput = PlayerInput.create(move, aim, buttons, t)
			sim.set_input(id, input)
			tick_inputs.append([id, move.x, move.y, aim.x, aim.y, buttons])
		inputs_log.append(tick_inputs)
		sim.step(1.0 / 30.0)
		var players: Array = []
		for id: int in ids:
			var s: PlayerState = sim.players[id]
			players.append([id, s.position.x, s.position.y, s.hp, 1 if s.tagged else 0, s.get("elixir") if s.get("elixir") != null else 0.0, s.get("stamina") if s.get("stamina") != null else 0.0])
		frames.append({"tick": sim.tick, "phase": int(sim.phase), "score": [sim.score.get("points")[0] if sim.score.get("points") != null else 0, sim.score.get("points")[1] if sim.score.get("points") != null else 0], "capture_team": sim.capture_team, "capture_elapsed": sim.capture_elapsed, "players": players})
	_write(OUT + "Golden/match_%d.json" % seed_value, {"seed": seed_value, "dt": 1.0 / 30.0, "start": start, "inputs": inputs_log, "frames": frames, "events": events})


func _strs(v: Variant) -> Array:
	var out: Array = []
	if v is Array:
		for item: Variant in v:
			out.append(String(item) if item != null else "")
	return out


func _write(path: String, data: Variant) -> void:
	var f: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	f.store_string(JSON.stringify(data, " ", false, true))
	f.close()
