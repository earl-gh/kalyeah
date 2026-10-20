extends SceneTree
## Plays every two-weapon loadout against every other, both ways round, as bot-versus-bot duels (BalanceDuel),
## and writes one line per duel to a CSV. Run headless, after an import:
##   godot --headless -s tools/balance/duels.gd -- --out duels.csv [--seeds 1] [--shard 0 --of 1] [--seconds 60]
## `--shard i --of n` plays every n-th pairing, so several processes can share the work.
## tools/balance/report.py turns the CSVs into the tables in docs/audits/.


func _init() -> void:
	var options: Dictionary[String, String] = _options(OS.get_cmdline_user_args())
	var rules: GameRules = load("res://data/rules/game_rules.tres") as GameRules
	var layout: MapLayout = load("res://data/rules/map_layout.tres") as MapLayout
	var seeds: int = int(options.get("seeds", "1"))
	var shard: int = int(options.get("shard", "0"))
	var shards: int = maxi(1, int(options.get("of", "1")))
	var seconds: float = float(options.get("seconds", str(BalanceDuel.MAX_SECONDS)))
	var out: FileAccess = FileAccess.open(options.get("out", "duels.csv"), FileAccess.WRITE)
	if out == null:
		push_error("cannot write %s" % options.get("out", "duels.csv"))
		quit(1)
		return
	out.store_line("first,second,first_side,seed,winner,seconds,hp_first,hp_second")
	var loadouts: Array[Array] = BalanceDuel.all_loadouts(rules)
	var index: int = 0
	for i: int in loadouts.size():
		for j: int in range(i + 1, loadouts.size()):
			index += 1
			if (index - 1) % shards != shard:
				continue
			for seed_value: int in seeds:
				for first_side: int in 2:
					# `first_side` 0: loadout i is team 0; 1: loadout j is team 0 (the sides swapped)
					var a: Array[StringName] = _ids(loadouts[i] if first_side == 0 else loadouts[j])
					var b: Array[StringName] = _ids(loadouts[j] if first_side == 0 else loadouts[i])
					var result: Dictionary = BalanceDuel.run(rules, layout, a, b, seed_value * 1000 + index, seconds)
					var winner: int = result["winner"] as int
					# report the winner as "first" (loadout i), "second" (loadout j) or draw
					var who: String = "draw"
					if winner != BalanceDuel.NO_WINNER:
						who = "first" if (winner == 0) == (first_side == 0) else "second"
					var hp: Array = result["hp"] as Array
					var hp_i: int = hp[0] as int if first_side == 0 else hp[1] as int
					var hp_j: int = hp[1] as int if first_side == 0 else hp[0] as int
					out.store_line("%s+%s,%s+%s,%d,%d,%s,%.2f,%d,%d" % [loadouts[i][0], loadouts[i][1], loadouts[j][0], loadouts[j][1], first_side, seed_value, who, result["seconds"] as float, hp_i, hp_j])
	out.close()
	quit()


func _ids(pair: Array) -> Array[StringName]:
	var ids: Array[StringName] = []
	for id: Variant in pair:
		ids.append(StringName(id))
	return ids


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
