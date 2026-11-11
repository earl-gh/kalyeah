class_name Snapshot
extends RefCounted
## Turns the server's MatchSim into plain data (SNAPSHOT message) and writes that
## data into a client's mirror MatchSim, which the match views read. The mirror is
## never stepped (except the local player's predicted movement), so the visual
## events that come from stepping are derived here from the differences.
##
## 5v5 Optimization: player floats are quantized with `snappedf(..., 0.01)` and
## default-zero/false/empty player fields are omitted on the wire (restored cleanly
## in `apply`), shrinking 10-player JSON/WebSocket snapshots by ~40-50%.


static func _snap(value: float) -> float:
	return snappedf(value, 0.01)


static func _snap_vec(v: Vector2) -> Vector2:
	return Vector2(snappedf(v.x, 0.01), snappedf(v.y, 0.01))


static func write(sim: MatchSim, acks: Dictionary = {}) -> Dictionary:
	var typed_acks: Dictionary[int, int] = {}
	for k: Variant in acks:
		if k is int and acks[k] is int:
			typed_acks[k as int] = acks[k] as int
	return capture(sim, typed_acks)


static func apply_to(sim: MatchSim, data: Dictionary, local_id: int = -1) -> void:
	apply(sim, data, local_id)


static func capture(sim: MatchSim, acks: Dictionary[int, int]) -> Dictionary:
	var players: Array[Dictionary] = []
	for id: int in sim.players:
		var p: PlayerState = sim.players[id]
		var entry: Dictionary = {
			"id": id,
			"pos": p.position if p.tagged else _snap_vec(p.position),
			"face": _snap_vec(p.facing),
			"hp": p.hp,
			"w": p.weapons.duplicate(),
		}
		if p.tagged:
			entry["tag"] = true
		if p.tag_time > 0.0:
			entry["tt"] = _snap(p.tag_time)
		var has_cd: bool = false
		for cd: float in p.weapon_cooldowns:
			if cd > 0.0:
				has_cd = true
				break
		if has_cd:
			var snapped_cds: Array[float] = []
			for cd: float in p.weapon_cooldowns:
				snapped_cds.append(_snap(cd))
			entry["wcd"] = snapped_cds
		var fx_snap: Dictionary = p.effects.snapshot()
		if not fx_snap.is_empty():
			entry["fx"] = fx_snap
		if p.cast_lock_left > 0.0:
			entry["cl"] = _snap(p.cast_lock_left)
		if p.next_card != &"":
			entry["nx"] = p.next_card
		if p.shuffle_card != &"":
			entry["sc"] = p.shuffle_card
		if p.shuffle_left > 0.0:
			entry["sl"] = snappedf(p.shuffle_left, 0.001)
		if p.shuffle_total > 0.0:
			entry["st"] = snappedf(p.shuffle_total, 0.001)
		if p.cards:
			entry["cd"] = true
		if not p.incoming.is_empty():
			entry["in"] = p.incoming.duplicate()
		if p.exhaust_left > 0.0:
			entry["ex"] = _snap(p.exhaust_left)
		if p.overclocked:
			entry["oc"] = true
		if p.tag_order != 0:
			entry["to"] = p.tag_order
		if p.primary_cd > 0.0:
			entry["pcd"] = _snap(p.primary_cd)
		if p.dash_cd > 0.0:
			entry["dcd"] = _snap(p.dash_cd)
		if p.dash_left > 0.0:
			entry["dl"] = _snap(p.dash_left)
		if p.dash_left > 0.0 or p.dash_dir != Vector2.ZERO:
			entry["dd"] = _snap_vec(p.dash_dir)
		if p.ult_charge > 0.0:
			entry["ult"] = _snap(p.ult_charge)
		if p.sprinting:
			entry["spr"] = true
		players.append(entry)
	var projectiles: Array[Dictionary] = []
	for projectile: WeaponSystem.Projectile in sim.weapons.projectiles:
		projectiles.append({"id": projectile.id, "w": projectile.def.id, "pos": projectile.position, "team": projectile.team})
	var zones: Array[Dictionary] = []
	for zone: WeaponSystem.Zone in sim.weapons.zones:
		zones.append({"id": zone.id, "w": zone.def.id, "k": zone.kind, "c": zone.center, "r": zone.radius, "team": zone.team})
	var shields: Array[Dictionary] = []
	for shield: WeaponSystem.Shield in sim.weapons.shields:
		shields.append({
			"id": shield.id, "w": shield.def.id, "c": shield.center, "al": shield.along,
			"hw": shield.half_width, "team": shield.team, "tl": shield.time_left,
			"o": shield.origin, "bhw": shield.base_half_width,
		})
	return {
		"t": NetProtocol.Msg.SNAPSHOT, "tick": sim.tick, "acks": acks.duplicate(),
		"players": players, "projectiles": projectiles, "zones": zones, "shields": shields,
		"phase": sim.phase, "freeze": sim.freeze_left, "intro": sim.intro_left, "clock": sim.match_time, "round": sim.round_time,
		"score": {"p": sim.score.points.duplicate(), "ft": sim.score.first_to, "w": sim.score.winner},
		"log": _log_of(sim),
		"cap": {"team": sim.capture_team, "k": sim.capture_kind, "e": sim.capture_elapsed, "need": sim.capture_needed, "r": sim.capture_rate},
		"resc": {"who": sim.rescue_rescuer.duplicate(), "e": sim.rescue_elapsed.duplicate(), "ready": sim.rescue_ready_left.duplicate()},
		"mode": sim.mode.snapshot() if sim.mode != null else {},
	}


## The part of a snapshot only `id`'s own client gets (merged into its copy as "me"): its elixir, stamina and move target. Nobody
## else learns where a kid is walking or how much elixir it has (the other team sees only who is exhausted, in the public part).
static func private_for(sim: MatchSim, id: int) -> Dictionary:
	var p: PlayerState = sim.players.get(id) as PlayerState
	if p == null:
		return {}
	var mine: Dictionary = {
		"id": id,
		"el": _snap(p.elixir),
		"sta": _snap(p.stamina),
		"still": _snap(p.still_time),
	}
	if p.has_move_target:
		mine["hmt"] = true
		mine["mt"] = _snap_vec(p.move_target)
	if p.stuck_time > 0.0:
		mine["stk"] = _snap(p.stuck_time)
	return {"me": mine}


## `message` (a snapshot for `viewer`'s phone) without what that phone may not know: in Langit Lupa's brownout, where the kids out of its
## sight are (their entry keeps its id and says "hid"; its position is the middle of the street) and their mounds. `message` itself is
## left as it was when nothing is hidden.
static func hide_for(sim: MatchSim, viewer: int, message: Dictionary) -> Dictionary:
	if sim.mode == null:
		return message
	var hidden: Array[int] = sim.mode.hidden_from(sim, viewer)
	if hidden.is_empty():
		return message
	var out: Dictionary = message.duplicate()
	var players: Array[Dictionary] = []
	for entry: Dictionary in message.get("players", []) as Array:
		if hidden.has(entry["id"] as int):
			var blank: Dictionary = entry.duplicate()
			blank["pos"] = Vector2.ZERO
			blank["hid"] = true
			players.append(blank)
		else:
			players.append(entry)
	out["players"] = players
	var mode: Dictionary = sim.mode.filter_for(message.get("mode", {}) as Dictionary, hidden)
	var unseen: Array = []
	unseen.assign(hidden)
	mode["unseen"] = unseen
	out["mode"] = mode
	return out


## Writes `data` into `sim`. `local_id`'s position is left to the caller (prediction).
static func apply(sim: MatchSim, data: Dictionary, local_id: int) -> void:
	sim.tick = data.get("tick", sim.tick) as int
	for entry: Dictionary in data.get("players", []) as Array:
		var id: int = entry["id"] as int
		var p: PlayerState = sim.players.get(id) as PlayerState
		if p == null:
			continue
		p.cast_lock_left = _number(entry.get("cl", 0.0))
		if id != local_id and not bool(entry.get("hid", false)):
			p.position = entry["pos"] as Vector2  # (a kid hidden from this phone keeps where it was last seen; the view hides it)
		p.facing = entry["face"] as Vector2
		p.hp = entry["hp"] as int
		p.tagged = bool(entry.get("tag", false))
		p.tag_time = _number(entry.get("tt", 0.0))
		p.weapons.assign(entry["w"] as Array)
		p.resize_slots()
		for slot: int in p.weapon_cooldowns.size():
			p.weapon_cooldowns[slot] = 0.0
		if entry.get("wcd") is Array:
			var cooldowns: Array = entry["wcd"] as Array
			for slot: int in mini(cooldowns.size(), p.weapon_cooldowns.size()):
				p.weapon_cooldowns[slot] = float(cooldowns[slot])
		p.cards = bool(entry.get("cd", false))
		p.next_card = entry.get("nx", &"") as StringName
		p.shuffle_card = entry.get("sc", &"") as StringName
		p.shuffle_left = _number(entry.get("sl", 0.0))
		p.shuffle_total = _number(entry.get("st", 0.0))
		p.incoming.assign(entry.get("in", []) as Array)
		p.exhaust_left = _number(entry.get("ex", 0.0))
		p.overclocked = bool(entry.get("oc", false))
		p.tag_order = int(_number(entry.get("to", 0)))
		p.primary_cd = _number(entry.get("pcd", 0.0))
		p.dash_cd = _number(entry.get("dcd", 0.0))
		p.dash_left = _number(entry.get("dl", 0.0))
		p.dash_dir = entry["dd"] as Vector2 if entry.get("dd") is Vector2 else Vector2.ZERO
		p.ult_charge = _number(entry.get("ult", 0.0))
		p.sprinting = bool(entry.get("spr", false))
		p.effects.restore(entry.get("fx", {}) as Dictionary)
	_apply_private(sim, data.get("me", {}) as Dictionary, local_id)
	_apply_weapons(sim, data)
	var was_intro: bool = sim.phase == MatchSim.Phase.INTRO
	sim.phase = data.get("phase", sim.phase) as MatchSim.Phase
	sim.intro_left = data.get("intro", 0.0) as float
	sim.match_time = data.get("clock", sim.match_time) as float
	sim.round_time = data.get("round", sim.round_time) as float
	if was_intro and sim.phase == MatchSim.Phase.PLAYING:
		sim.intro_finished.emit()
	sim.freeze_left = data.get("freeze", 0.0) as float
	var score: Dictionary = data.get("score", {}) as Dictionary
	if not score.is_empty():
		sim.score.points.assign(score["p"] as Array)
		sim.score.first_to = score["ft"] as int
		sim.score.winner = score["w"] as int
	if data.has("log"):
		sim.point_log.clear()
		for entry: Variant in data["log"] as Array:
			sim.point_log.append(Vector2i((entry as Array)[0] as int, (entry as Array)[1] as int))
	var capture_dict: Dictionary = data.get("cap", {}) as Dictionary
	if not capture_dict.is_empty():
		sim.capture_team = capture_dict["team"] as int
		sim.capture_kind = capture_dict["k"] as MatchSim.CaptureKind
		sim.capture_elapsed = capture_dict["e"] as float
		sim.capture_needed = capture_dict.get("need", sim.capture_needed) as float
		sim.capture_rate = _number(capture_dict.get("r", 1.0))
	var rescue: Dictionary = data.get("resc", {}) as Dictionary
	if not rescue.is_empty():
		var who: Array = rescue["who"] as Array
		var elapsed: Array = rescue["e"] as Array
		for team: int in mini(2, mini(who.size(), elapsed.size())):
			sim.rescue_rescuer[team] = int(_number(who[team]))
			sim.rescue_elapsed[team] = _number(elapsed[team])
		var ready: Array = rescue.get("ready", []) as Array
		for team: int in mini(2, ready.size()):
			sim.rescue_ready_left[team] = _number(ready[team])
	var mode: Variant = data.get("mode", {})
	if sim.mode != null and mode is Dictionary:
		sim.mode.restore(mode as Dictionary, sim)


## The recipient's own elixir, stamina and move target (absent from an old server: the mirror keeps its own).
static func _apply_private(sim: MatchSim, mine: Dictionary, local_id: int) -> void:
	if mine.is_empty() or mine.get("id", -1) != local_id or not sim.players.has(local_id):
		return
	var p: PlayerState = sim.players[local_id]
	p.elixir = _number(mine.get("el", p.elixir))
	p.stamina = _number(mine.get("sta", p.stamina))
	p.still_time = _number(mine.get("still", p.still_time))
	p.has_move_target = bool(mine.get("hmt", false))
	p.move_target = mine.get("mt", Vector2.ZERO) as Vector2 if mine.get("mt") is Vector2 else Vector2.ZERO
	p.stuck_time = _number(mine.get("stk", 0.0))


static func _number(value: Variant) -> float:
	return float(value) if value is float or value is int else 0.0


static func _log_of(sim: MatchSim) -> Array:
	var log: Array = []
	for entry: Vector2i in sim.point_log:
		log.append([entry.x, entry.y])
	return log


static func _apply_weapons(sim: MatchSim, data: Dictionary) -> void:
	var system: WeaponSystem = sim.weapons
	system.projectiles.clear()
	for entry: Dictionary in data.get("projectiles", []) as Array:
		var projectile: WeaponSystem.Projectile = WeaponSystem.Projectile.new()
		projectile.id = entry["id"] as int
		projectile.def = sim.weapon_defs[entry["w"] as StringName]
		projectile.position = entry["pos"] as Vector2
		projectile.team = entry["team"] as int
		system.projectiles.append(projectile)
	system.zones.clear()
	for entry: Dictionary in data.get("zones", []) as Array:
		var zone: WeaponSystem.Zone = WeaponSystem.Zone.new()
		zone.id = entry["id"] as int
		zone.def = sim.weapon_defs[entry["w"] as StringName]
		zone.kind = entry["k"] as WeaponSystem.ZoneKind
		zone.center = entry["c"] as Vector2
		zone.radius = entry["r"] as float
		zone.team = entry["team"] as int
		system.zones.append(zone)
	system.shields.clear()
	for entry: Dictionary in data.get("shields", []) as Array:
		var shield: WeaponSystem.Shield = WeaponSystem.Shield.new()
		shield.id = entry["id"] as int
		var weapon_id: StringName = entry.get("w", &"") as StringName
		shield.def = sim.weapon_defs.get(weapon_id) as WeaponDef
		shield.center = entry["c"] as Vector2
		shield.along = entry["al"] as Vector2
		shield.half_width = entry["hw"] as float
		shield.team = entry["team"] as int
		shield.time_left = entry.get("tl", shield.def.duration if shield.def != null else 0.0) as float
		# its cast place and width let the mirror move and widen it exactly as the server does (`WallMotion`); an older server
		# sends neither, and the wall then stays as sent
		shield.origin = entry.get("o", Vector2.INF) as Vector2
		shield.base_half_width = entry.get("bhw", shield.half_width) as float
		shield.prev_center = shield.center
		shield.prev_half_width = shield.half_width
		system.shields.append(shield)
