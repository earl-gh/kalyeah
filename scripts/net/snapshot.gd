class_name Snapshot
extends RefCounted
## Turns the server's MatchSim into plain data (SNAPSHOT message) and writes that
## data into a client's mirror MatchSim, which the match views read. The mirror is
## never stepped (except the local player's predicted movement), so the visual
## events that come from stepping are derived here from the differences.
##
## 5v5 Optimization: player floats are quantized with `snappedf(..., 0.01)` and
## default-zero/false/empty player & power-up fields are omitted on the wire
## (explicitly restored to defaults in `apply`), shrinking 10-player snapshots
## by ~40-50% with zero idle overhead for walk/crouch/jump/slide/buffs/powerups.


static func _snap(value: float) -> float:
	return snappedf(value, 0.01)


static func _snap_vec(v: Vector2) -> Vector2:
	return Vector2(snappedf(v.x, 0.01), snappedf(v.y, 0.01))


static func write(sim: MatchSim, acks: Dictionary = {}) -> Dictionary:
	var typed_acks: Dictionary[int, int] = {}
	for k: Variant in acks:
		var ack_val: Variant = acks[k]
		if k is int and ack_val is int:
			typed_acks[k] = ack_val
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
		if fx_snap.size() > 1 or _float_in(fx_snap, "slow", 0.0) != 0.0:
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
		# Overhaul v2 locomotion & street power-up buff state (omitted when false/0/0.0).
		if _prop_bool(p, &"walking"):
			entry["wlk"] = true
		if _prop_bool(p, &"crouching"):
			entry["cr"] = true
		var air_y: float = _prop_float(p, &"air_y")
		var vel_y: float = _prop_float(p, &"vel_y")
		if air_y > 0.0:
			entry["ay"] = _snap(air_y)
		if air_y > 0.0 or absf(vel_y) > 0.005:
			entry["vy"] = _snap(vel_y)
		var jump_cd: float = _prop_float(p, &"jump_cd")
		if jump_cd > 0.0:
			entry["jcd"] = _snap(jump_cd)
		var slide_left: float = _prop_float(p, &"slide_left")
		if slide_left > 0.0:
			entry["sll"] = _snap(slide_left)
			entry["sld"] = _snap_vec(_prop_vec2(p, &"slide_dir"))
		var slide_cd: float = _prop_float(p, &"slide_cd")
		if slide_cd > 0.0:
			entry["scd"] = _snap(slide_cd)
		var buff_type: int = _prop_int(p, &"buff_type")
		if buff_type != 0:
			entry["bt"] = buff_type
		var buff_left: float = _prop_float(p, &"buff_left")
		if buff_left > 0.0:
			entry["bl"] = _snap(buff_left)
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
	var out: Dictionary = {
		"t": NetProtocol.Msg.SNAPSHOT, "tick": sim.tick, "acks": acks.duplicate(),
		"players": players, "projectiles": projectiles, "zones": zones, "shields": shields,
		"phase": sim.phase, "freeze": sim.freeze_left, "intro": sim.intro_left, "clock": sim.match_time, "round": sim.round_time,
		"score": {"p": sim.score.points.duplicate(), "ft": sim.score.first_to, "w": sim.score.winner},
		"log": _log_of(sim),
		"cap": {"team": sim.capture_team, "k": sim.capture_kind, "e": sim.capture_elapsed, "need": sim.capture_needed, "r": sim.capture_rate},
		"resc": {"who": sim.rescue_rescuer.duplicate(), "e": sim.rescue_elapsed.duplicate(), "ready": sim.rescue_ready_left.duplicate()},
		"mode": sim.mode.snapshot() if sim.mode != null else {},
	}
	var raw_powerups: Variant = sim.get(&"powerups")
	if raw_powerups is Array and not raw_powerups.is_empty():
		var pu_wire: Array[Dictionary] = []
		for item: Variant in raw_powerups:
			if not (item is Dictionary):
				continue
			var pos: Vector2 = _vec2_in(item, "pos", Vector2(_float_in(item, "x", 0.0), _float_in(item, "y", 0.0)))
			var active: bool = _bool_in(item, "active", _bool_in(item, "a", true))
			var respawn_left: float = _float_in(item, "respawn_left", _float_in(item, "r", 0.0))
			var pu_entry: Dictionary = {
				"i": _int_in(item, "id", _int_in(item, "i", 0)),
				"k": _int_in(item, "kind", _int_in(item, "k", 0)),
				"x": _snap(pos.x),
				"y": _snap(pos.y),
			}
			if not active:
				pu_entry["a"] = false
			if respawn_left > 0.0:
				pu_entry["r"] = _snap(respawn_left)
			pu_wire.append(pu_entry)
		if not pu_wire.is_empty():
			out["pu"] = pu_wire
	return out


## The part of a snapshot only `id`'s own client gets (merged into its copy as "me"): its elixir, stamina and move target. Nobody
## else learns where a kid is walking or how much elixir it has (the other team sees only who is exhausted, in the public part).
static func private_for(sim: MatchSim, id: int) -> Dictionary:
	if not sim.players.has(id):
		return {}
	var p: PlayerState = sim.players[id]
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
	for raw_entry: Variant in _arr_in(message, "players"):
		if not (raw_entry is Dictionary):
			continue
		var entry_id: int = _int_in(raw_entry, "id", -1)
		if hidden.has(entry_id):
			var blank: Dictionary = raw_entry.duplicate()
			blank["pos"] = Vector2.ZERO
			blank["hid"] = true
			blank.erase("wlk")
			blank.erase("cr")
			blank.erase("ay")
			blank.erase("vy")
			blank.erase("sll")
			blank.erase("sld")
			players.append(blank)
		else:
			players.append(raw_entry)
	out["players"] = players
	var mode: Dictionary = sim.mode.filter_for(_dict_in(message, "mode"), hidden)
	var unseen: Array = []
	unseen.assign(hidden)
	mode["unseen"] = unseen
	out["mode"] = mode
	return out


## Writes `data` into `sim`. `local_id`'s position is left to the caller (prediction).
static func apply(sim: MatchSim, data: Dictionary, local_id: int) -> void:
	sim.tick = _int_in(data, "tick", sim.tick)
	for raw_entry: Variant in _arr_in(data, "players"):
		if not (raw_entry is Dictionary):
			continue
		var id: int = _int_in(raw_entry, "id", -1)
		if not sim.players.has(id):
			continue
		var p: PlayerState = sim.players[id]
		var is_hidden: bool = _bool_in(raw_entry, "hid", false)
		p.cast_lock_left = _float_in(raw_entry, "cl", 0.0)
		if id != local_id and not is_hidden:
			p.position = _vec2_in(raw_entry, "pos", p.position)  # (a kid hidden from this phone keeps where it was last seen; the view hides it)
		p.facing = _vec2_in(raw_entry, "face", p.facing)
		p.hp = _int_in(raw_entry, "hp", p.hp)
		p.tagged = _bool_in(raw_entry, "tag", false)
		p.tag_time = _float_in(raw_entry, "tt", 0.0)
		p.weapons.assign(_sname_array(_arr_in(raw_entry, "w")))
		p.resize_slots()
		for slot: int in p.weapon_cooldowns.size():
			p.weapon_cooldowns[slot] = 0.0
		var raw_wcd: Variant = raw_entry.get("wcd")
		if raw_wcd is Array:
			for slot: int in mini(raw_wcd.size(), p.weapon_cooldowns.size()):
				p.weapon_cooldowns[slot] = _number(raw_wcd[slot])
		p.cards = _bool_in(raw_entry, "cd", false)
		p.next_card = _sname_in(raw_entry, "nx", &"")
		p.shuffle_card = _sname_in(raw_entry, "sc", &"")
		p.shuffle_left = _float_in(raw_entry, "sl", 0.0)
		p.shuffle_total = _float_in(raw_entry, "st", 0.0)
		p.incoming.assign(_sname_array(_arr_in(raw_entry, "in")))
		p.exhaust_left = _float_in(raw_entry, "ex", 0.0)
		p.overclocked = _bool_in(raw_entry, "oc", false)
		p.tag_order = _int_in(raw_entry, "to", 0)
		p.primary_cd = _float_in(raw_entry, "pcd", 0.0)
		p.dash_cd = _float_in(raw_entry, "dcd", 0.0)
		p.dash_left = _float_in(raw_entry, "dl", 0.0)
		p.dash_dir = _vec2_in(raw_entry, "dd", Vector2.ZERO)
		p.ult_charge = _float_in(raw_entry, "ult", 0.0)
		p.sprinting = _bool_in(raw_entry, "spr", false)
		# Explicitly reset omitted Overhaul v2 locomotion & buff fields to defaults.
		p.set(&"walking", _bool_in(raw_entry, "wlk", false))
		p.set(&"crouching", _bool_in(raw_entry, "cr", false))
		p.set(&"air_y", _float_in(raw_entry, "ay", 0.0))
		p.set(&"vel_y", _float_in(raw_entry, "vy", 0.0))
		p.set(&"jump_cd", _float_in(raw_entry, "jcd", 0.0))
		var slide_left: float = _float_in(raw_entry, "sll", 0.0)
		p.set(&"slide_left", slide_left)
		p.set(&"slide_cd", _float_in(raw_entry, "scd", 0.0))
		p.set(&"slide_dir", _vec2_in(raw_entry, "sld", Vector2.ZERO) if slide_left > 0.0 else Vector2.ZERO)
		p.set(&"buff_type", _int_in(raw_entry, "bt", 0))
		p.set(&"buff_left", _float_in(raw_entry, "bl", 0.0))
		p.effects.restore(_dict_in(raw_entry, "fx"))
	_apply_private(sim, _dict_in(data, "me"), local_id)
	_apply_weapons(sim, data)
	_apply_powerups(sim, data)
	var was_intro: bool = sim.phase == MatchSim.Phase.INTRO
	sim.phase = _int_in(data, "phase", int(sim.phase)) as MatchSim.Phase
	sim.intro_left = _float_in(data, "intro", 0.0)
	sim.match_time = _float_in(data, "clock", sim.match_time)
	sim.round_time = _float_in(data, "round", sim.round_time)
	if was_intro and sim.phase == MatchSim.Phase.PLAYING:
		sim.intro_finished.emit()
	sim.freeze_left = _float_in(data, "freeze", 0.0)
	var score: Dictionary = _dict_in(data, "score")
	if not score.is_empty():
		var pts: Array[int] = []
		for pt: Variant in _arr_in(score, "p"):
			pts.append(int(_number(pt)))
		sim.score.points.assign(pts)
		sim.score.first_to = _int_in(score, "ft", sim.score.first_to)
		sim.score.winner = _int_in(score, "w", sim.score.winner)
	if data.has("log"):
		sim.point_log.clear()
		for raw_log: Variant in _arr_in(data, "log"):
			if raw_log is Array and raw_log.size() >= 2:
				sim.point_log.append(Vector2i(int(_number(raw_log[0])), int(_number(raw_log[1]))))
	var capture_dict: Dictionary = _dict_in(data, "cap")
	if not capture_dict.is_empty():
		sim.capture_team = _int_in(capture_dict, "team", sim.capture_team)
		sim.capture_kind = _int_in(capture_dict, "k", int(sim.capture_kind)) as MatchSim.CaptureKind
		sim.capture_elapsed = _float_in(capture_dict, "e", sim.capture_elapsed)
		sim.capture_needed = _float_in(capture_dict, "need", sim.capture_needed)
		sim.capture_rate = _float_in(capture_dict, "r", 1.0)
	var rescue: Dictionary = _dict_in(data, "resc")
	if not rescue.is_empty():
		var who: Array = _arr_in(rescue, "who")
		var elapsed: Array = _arr_in(rescue, "e")
		for team: int in mini(2, mini(who.size(), elapsed.size())):
			sim.rescue_rescuer[team] = int(_number(who[team]))
			sim.rescue_elapsed[team] = _number(elapsed[team])
		var ready: Array = _arr_in(rescue, "ready")
		for team: int in mini(2, ready.size()):
			sim.rescue_ready_left[team] = _number(ready[team])
	var mode: Variant = data.get("mode", {})
	if sim.mode != null and mode is Dictionary:
		sim.mode.restore(mode, sim)


## The recipient's own elixir, stamina and move target (absent from an old server: the mirror keeps its own).
static func _apply_private(sim: MatchSim, mine: Dictionary, local_id: int) -> void:
	if mine.is_empty() or _int_in(mine, "id", -1) != local_id or not sim.players.has(local_id):
		return
	var p: PlayerState = sim.players[local_id]
	p.elixir = _float_in(mine, "el", p.elixir)
	p.stamina = _float_in(mine, "sta", p.stamina)
	p.still_time = _float_in(mine, "still", p.still_time)
	p.has_move_target = _bool_in(mine, "hmt", false)
	p.move_target = _vec2_in(mine, "mt", Vector2.ZERO)
	p.stuck_time = _float_in(mine, "stk", 0.0)


static func _apply_powerups(sim: MatchSim, data: Dictionary) -> void:
	var target_pu: Variant = sim.get(&"powerups")
	if not (target_pu is Array):
		return
	target_pu.clear()
	for raw_item: Variant in _arr_in(data, "pu"):
		if not (raw_item is Dictionary):
			continue
		var pos: Vector2 = _vec2_in(
			raw_item,
			"pos",
			Vector2(_float_in(raw_item, "x", 0.0), _float_in(raw_item, "y", 0.0))
		)
		var restored: Dictionary = {
			"id": _int_in(raw_item, "i", _int_in(raw_item, "id", 0)),
			"kind": _int_in(raw_item, "k", _int_in(raw_item, "kind", 0)),
			"pos": pos,
			"active": _bool_in(raw_item, "a", _bool_in(raw_item, "active", true)),
			"respawn_left": _float_in(raw_item, "r", _float_in(raw_item, "respawn_left", 0.0)),
		}
		target_pu.append(restored)


static func _apply_weapons(sim: MatchSim, data: Dictionary) -> void:
	var system: WeaponSystem = sim.weapons
	system.projectiles.clear()
	for raw_entry: Variant in _arr_in(data, "projectiles"):
		if not (raw_entry is Dictionary):
			continue
		var wid: StringName = _sname_in(raw_entry, "w", &"")
		if not sim.weapon_defs.has(wid):
			continue
		var projectile: WeaponSystem.Projectile = WeaponSystem.Projectile.new()
		projectile.id = _int_in(raw_entry, "id", 0)
		projectile.def = sim.weapon_defs[wid]
		projectile.position = _vec2_in(raw_entry, "pos", Vector2.ZERO)
		projectile.team = _int_in(raw_entry, "team", 0)
		system.projectiles.append(projectile)
	system.zones.clear()
	for raw_entry: Variant in _arr_in(data, "zones"):
		if not (raw_entry is Dictionary):
			continue
		var wid: StringName = _sname_in(raw_entry, "w", &"")
		if not sim.weapon_defs.has(wid):
			continue
		var zone: WeaponSystem.Zone = WeaponSystem.Zone.new()
		zone.id = _int_in(raw_entry, "id", 0)
		zone.def = sim.weapon_defs[wid]
		zone.kind = _int_in(raw_entry, "k", 0) as WeaponSystem.ZoneKind
		zone.center = _vec2_in(raw_entry, "c", Vector2.ZERO)
		zone.radius = _float_in(raw_entry, "r", 0.0)
		zone.team = _int_in(raw_entry, "team", 0)
		system.zones.append(zone)
	system.shields.clear()
	for raw_entry: Variant in _arr_in(data, "shields"):
		if not (raw_entry is Dictionary):
			continue
		var shield: WeaponSystem.Shield = WeaponSystem.Shield.new()
		shield.id = _int_in(raw_entry, "id", 0)
		var weapon_id: StringName = _sname_in(raw_entry, "w", &"")
		shield.def = sim.weapon_defs[weapon_id] if sim.weapon_defs.has(weapon_id) else null
		shield.center = _vec2_in(raw_entry, "c", Vector2.ZERO)
		shield.along = _vec2_in(raw_entry, "al", Vector2.RIGHT)
		shield.half_width = _float_in(raw_entry, "hw", 0.0)
		shield.team = _int_in(raw_entry, "team", 0)
		var def_dur: float = shield.def.duration if shield.def != null else 0.0
		shield.time_left = _float_in(raw_entry, "tl", def_dur)
		# its cast place and width let the mirror move and widen it exactly as the server does (`WallMotion`); an older server
		# sends neither, and the wall then stays as sent
		shield.origin = _vec2_in(raw_entry, "o", Vector2.INF)
		shield.base_half_width = _float_in(raw_entry, "bhw", shield.half_width)
		shield.prev_center = shield.center
		shield.prev_half_width = shield.half_width
		system.shields.append(shield)


static func _log_of(sim: MatchSim) -> Array:
	var log: Array = []
	for entry: Vector2i in sim.point_log:
		log.append([entry.x, entry.y])
	return log


static func _sname_array(raw: Array) -> Array[StringName]:
	var out: Array[StringName] = []
	for item: Variant in raw:
		if item is StringName:
			out.append(item)
		elif item is String:
			out.append(StringName(item))
	return out


static func _number(value: Variant) -> float:
	return float(value) if value is float or value is int else 0.0


static func _prop_bool(obj: Object, prop: StringName) -> bool:
	var v: Variant = obj.get(prop)
	if v is bool:
		return v
	return false


static func _prop_int(obj: Object, prop: StringName) -> int:
	var v: Variant = obj.get(prop)
	if v is int:
		return v
	if v is float:
		return int(v)
	return 0


static func _prop_float(obj: Object, prop: StringName) -> float:
	var v: Variant = obj.get(prop)
	if v is float:
		return v
	if v is int:
		return float(v)
	return 0.0


static func _prop_vec2(obj: Object, prop: StringName) -> Vector2:
	var v: Variant = obj.get(prop)
	if v is Vector2:
		return v
	return Vector2.ZERO


static func _int_in(d: Dictionary, key: Variant, fallback: int = 0) -> int:
	var v: Variant = d.get(key, fallback)
	if v is int:
		return v
	if v is float:
		return int(v)
	return fallback


static func _float_in(d: Dictionary, key: Variant, fallback: float = 0.0) -> float:
	var v: Variant = d.get(key, fallback)
	if v is float:
		return v
	if v is int:
		return float(v)
	return fallback


static func _bool_in(d: Dictionary, key: Variant, fallback: bool = false) -> bool:
	var v: Variant = d.get(key, fallback)
	if v is bool:
		return v
	return fallback


static func _vec2_in(d: Dictionary, key: Variant, fallback: Vector2 = Vector2.ZERO) -> Vector2:
	var v: Variant = d.get(key, fallback)
	if v is Vector2:
		return v
	return fallback


static func _sname_in(d: Dictionary, key: Variant, fallback: StringName = &"") -> StringName:
	var v: Variant = d.get(key, fallback)
	if v is StringName:
		return v
	if v is String:
		return StringName(v)
	return fallback


static func _dict_in(d: Dictionary, key: Variant) -> Dictionary:
	var v: Variant = d.get(key)
	if v is Dictionary:
		return v
	return {}


static func _arr_in(d: Dictionary, key: Variant) -> Array:
	var v: Variant = d.get(key)
	if v is Array:
		return v
	return []
