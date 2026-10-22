class_name Snapshot
extends RefCounted
## Turns the server's MatchSim into plain data (SNAPSHOT message) and writes that
## data into a client's mirror MatchSim, which the match views read. The mirror is
## never stepped (except the local player's predicted movement), so the visual
## events that come from stepping are derived here from the differences.


static func capture(sim: MatchSim, acks: Dictionary[int, int]) -> Dictionary:
	var players: Array[Dictionary] = []
	for id: int in sim.players:
		var p: PlayerState = sim.players[id]
		players.append({
			"id": id, "pos": p.position, "face": p.facing, "hp": p.hp, "tag": p.tagged, "tt": p.tag_time,
			"wcd": p.weapon_cooldowns.duplicate(), "w": p.weapons.duplicate(), "fx": p.effects.snapshot(),
			"cl": p.cast_lock_left, "nx": p.next_card, "sc": p.shuffle_card, "sl": p.shuffle_left, "st": p.shuffle_total, "cd": p.cards,
		})
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
		})
	return {
		"t": NetProtocol.Msg.SNAPSHOT, "tick": sim.tick, "acks": acks.duplicate(),
		"players": players, "projectiles": projectiles, "zones": zones, "shields": shields,
		"phase": sim.phase, "freeze": sim.freeze_left, "intro": sim.intro_left, "clock": sim.match_time,
		"score": {"p": sim.score.points.duplicate(), "ft": sim.score.first_to, "w": sim.score.winner},
		"log": _log_of(sim),
		"cap": {"team": sim.capture_team, "k": sim.capture_kind, "e": sim.capture_elapsed, "need": sim.capture_needed},
	}


## Writes `data` into `sim`. `local_id`'s position is left to the caller (prediction).
static func apply(sim: MatchSim, data: Dictionary, local_id: int) -> void:
	sim.tick = data.get("tick", sim.tick) as int
	for entry: Dictionary in data.get("players", []) as Array:
		var id: int = entry["id"] as int
		var p: PlayerState = sim.players.get(id) as PlayerState
		if p == null:
			continue
		p.cast_lock_left = entry.get("cl", p.cast_lock_left) as float
		if id != local_id:
			p.position = entry["pos"] as Vector2
		p.facing = entry["face"] as Vector2
		p.hp = entry["hp"] as int
		p.tagged = entry["tag"] as bool
		p.tag_time = entry["tt"] as float
		p.weapon_cooldowns.assign(entry["wcd"] as Array)
		p.weapons.assign(entry["w"] as Array)
		p.cards = entry.get("cd", false) as bool
		p.next_card = entry.get("nx", &"") as StringName
		p.shuffle_card = entry.get("sc", &"") as StringName
		p.shuffle_left = entry.get("sl", 0.0) as float
		p.shuffle_total = entry.get("st", 0.0) as float
		p.effects.restore(entry["fx"] as Dictionary)
	_apply_weapons(sim, data)
	var was_intro: bool = sim.phase == MatchSim.Phase.INTRO
	sim.phase = data.get("phase", sim.phase) as MatchSim.Phase
	sim.intro_left = data.get("intro", 0.0) as float
	sim.match_time = data.get("clock", sim.match_time) as float
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
	var capture: Dictionary = data.get("cap", {}) as Dictionary
	if not capture.is_empty():
		sim.capture_team = capture["team"] as int
		sim.capture_kind = capture["k"] as MatchSim.CaptureKind
		sim.capture_elapsed = capture["e"] as float
		sim.capture_needed = capture.get("need", sim.capture_needed) as float


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
		system.shields.append(shield)
