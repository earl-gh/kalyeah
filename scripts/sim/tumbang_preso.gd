class_name TumbangPreso
extends StreetMode
## Tumbang Preso (docs/GDD.md "Tumbang Preso"): one kid is the taya (the "it"), everyone else is a thrower, and every kid plays for itself.
## The throwers throw their slipper at the can from the throwing area (behind the throw line): hold the Throw button to aim, the aim line
## grows with the hold, let go to throw. While a thrower has its slipper in hand it cannot leave the throwing area. A hit flings the can and
## the slipper away; the taya has to fetch the can and put it back, and cannot tag until it stands again. A thrower whose slipper lies in
## the fetch area must go and fetch it and run to the other end for a point (and is brought back to the throwing area at once); it has
## `scared_seconds` to step into the fetch area, or it is too scared and becomes the taya. A tagged thrower becomes the taya. There are no
## skills in this game. The slipper is the weapons' own flying object (a `WeaponSystem.Projectile` of tsinelas, drawn like Agawan Base's).

## `id` threw its slipper (it is in the air now).
signal slipper_thrown(id: int)
## The slipper of `id` missed and landed at `at`.
signal slipper_landed(id: int, at: Vector2, hit: bool)
## The can was flung from `from`: it lands at each point of `path` in turn (one fling, or two when it bounced).
signal can_flung(from: Vector2, path: Array[Vector2])
## The slipper of `id` was flung from `from` the same way.
signal slipper_flung(id: int, from: Vector2, path: Array[Vector2])
## `id` kicked the can.
signal can_kicked(id: int)
## `id` picked its slipper up again.
signal slipper_fetched(id: int)
## A taya (`id`) stood the can back in its spot.
signal can_placed(id: int)
## `new_taya` took the "it" from `old_taya` (`why`: &"tagged" or &"scared").
signal taya_changed(old_taya: int, new_taya: int, why: StringName)

## The weapon whose model and animation the thrown slipper borrows (data/weapons/tsinelas_light.tres).
const SLIPPER_WEAPON: StringName = &"tsinelas_light"
## An aim stick shorter than this keeps the aim as it was.
const AIM_DEADZONE: float = 0.1
## The slipper's aim is only judged this close to a straight line to be a hit on the can (a dead-on throw has no lateral offset).
const DEAD_ON: float = 0.05
## Walls are this far from a flung thing's centre.
const FLING_MARGIN: float = 0.4

## The taya (the "it"): the one kid that guards the can.
var taya_id: int = -1
## id -> points (the match is won by the kid with the most).
var points: Dictionary[int, int] = {}
## The kid that won (-1 until the match is over).
var winner_id: int = -1
## Where the can is (on its spot, lying where it was flung or kicked, or in the taya's hands).
var can_position: Vector2 = Vector2.ZERO
## True while the can stands in its spot: only then can the taya tag.
var can_up: bool = true
## The taya carrying the fallen can (-1: nobody).
var can_carrier: int = -1
## Seconds until a flung can lands (nobody can pick it up or kick it before).
var can_busy_left: float = 0.0
## id -> where the thrower's slipper lies (absent: in hand or in the air), and seconds until a flung one lands.
var slippers: Dictionary[int, Vector2] = {}
var slipper_busy: Dictionary[int, float] = {}
## id -> the slipper in the air (the sim's projectile).
var flights: Dictionary[int, WeaponSystem.Projectile] = {}
## The throwers who picked their slipper up and are running for the other end (a point, and back to the throwing area).
var fetched: Dictionary[int, bool] = {}
## id -> seconds it may still spend outside the fetch area before it becomes the taya (absent: no timer; set while its slipper lies in the
## fetch area, it runs only while the thrower is out of it).
var scared_left: Dictionary[int, float] = {}
## id -> seconds the Throw button has been held (absent: not held) and the way the aim points (a unit vector).
var hold: Dictionary[int, float] = {}
var aim_dir: Dictionary[int, Vector2] = {}
## id -> seconds until the thrower can kick the can again.
var kick_left: Dictionary[int, float] = {}

var _flight_from: Dictionary[int, Vector2] = {}
var _kick_down: Dictionary[int, bool] = {}
var _slipper_def: WeaponDef


func is_free_for_all() -> bool:
	return true


func is_attacker(state: PlayerState) -> bool:
	return state.id != taya_id


func speed_scale(state: PlayerState) -> float:
	return rules.taya_speed_share if state.id == taya_id else 1.0


## The last kid to join is the first taya (a bot, in Practice), then everyone to their place.
func setup(sim: MatchSim) -> void:
	for id: int in sim.players:
		taya_id = id
		points[id] = 0
	super.setup(sim)


## The kids by points, most first (ties by id).
func ranking() -> Array[int]:
	var ids: Array[int] = []
	ids.assign(points.keys())
	ids.sort_custom(func(a: int, b: int) -> bool: return points[a] > points[b] or (points[a] == points[b] and a < b))
	return ids


# ---- the thrower -------------------------------------------------------------------------

## True when `id` has its slipper in hand (not lying on the street, not in the air).
func has_slipper(id: int) -> bool:
	return not slippers.has(id) and not flights.has(id)


## True while `state` has to stay in the throwing area: a thrower that has its slipper and has not just fetched it.
func is_held_back(state: PlayerState) -> bool:
	return is_attacker(state) and has_slipper(state.id) and not fetched.has(state.id)


## True when `state` could throw now: a thrower with its slipper, in the throwing area.
func can_throw(state: PlayerState) -> bool:
	return is_attacker(state) and not state.tagged and has_slipper(state.id) and at_home(state.position)


## True when `state` could kick the can now: a thrower with no slipper in hand, within reach of the can lying or standing on the street.
func can_kick(state: PlayerState) -> bool:
	return is_attacker(state) and not state.tagged and not has_slipper(state.id) and kick_left.get(state.id, 0.0) <= 0.0 \
		and can_carrier < 0 and can_busy_left <= 0.0 and state.position.distance_to(can_position) <= rules.kick_reach


## How full the aim is (0 to 1) after `seconds` of holding.
func charge_for(seconds: float) -> float:
	return clampf(seconds / rules.throw_charge_time, 0.0, 1.0) if rules.throw_charge_time > 0.0 else 1.0


## The charge of `id` now (0 when it is not holding the button).
func charge(id: int) -> float:
	return charge_for(hold[id]) if hold.has(id) else 0.0


## How far the aim line reaches at `charge_value` (0 to 1).
func range_at(charge_value: float) -> float:
	return lerpf(rules.throw_min_range, rules.throw_max_range, charge_value)


## How far the aim line of `id` reaches now (0 while it is not holding the button).
func aim_range(id: int) -> float:
	return range_at(charge(id)) if hold.has(id) else 0.0


## True while `id` holds the Throw button and could throw.
func is_aiming(id: int) -> bool:
	return hold.has(id)


## Radians the aim turns per pixel of finger drag at `charge_value`: fine while the line is short, fast at full length.
func sensitivity_at(charge_value: float) -> float:
	return lerpf(rules.aim_sensitivity_low, rules.aim_sensitivity_high, charge_value)


## The aim that points `from` at the can.
func aim_at_can(from: Vector2) -> Vector2:
	var way: Vector2 = can_position - from
	return way.normalized() if way.length() > 0.01 else Vector2(0.0, -1.0)


func handle_input(sim: MatchSim, state: PlayerState, input: PlayerInput, _dt: float) -> void:
	var id: int = state.id
	var kicking: bool = input != null and input.is_pressed(PlayerInput.BTN_CARD_2)
	if kicking and not _kick_down.get(id, false) and can_kick(state):
		kick(sim, state)
	_kick_down[id] = kicking
	var held: bool = input != null and input.is_pressed(PlayerInput.BTN_CARD_1) and can_throw(state)
	if held:
		if not hold.has(id):
			hold[id] = 0.0
			aim_dir[id] = aim_at_can(state.position)
		hold[id] += 1.0 / float(sim.rules.tick_rate)
		if input.aim.length() > AIM_DEADZONE:
			aim_dir[id] = input.aim.normalized()
		return
	if not hold.has(id):
		return
	var cancelled: bool = input == null or input.is_pressed(PlayerInput.BTN_AIM_CANCEL) or not can_throw(state)
	var power: float = charge(id)
	var way: Vector2 = aim_dir[id]
	hold.erase(id)
	aim_dir.erase(id)
	if not cancelled:
		throw(sim, state, way, range_at(power))


## The slipper flies from `state` along `way` for `distance` m (the weapons' projectile, `throw_speed`); `state` stands still while it
## throws. The can is hit where the flight meets it (see `_step_flights`).
func throw(sim: MatchSim, state: PlayerState, way: Vector2, distance: float) -> void:
	var direction: Vector2 = way.normalized() if way.length() > 0.0 else Vector2(0.0, -1.0)
	var projectile: WeaponSystem.Projectile = sim.weapons.launch(state, _def_for(sim, distance), direction)
	flights[state.id] = projectile
	_flight_from[state.id] = projectile.position
	fetched.erase(state.id)
	state.facing = direction
	sim.end_move(state)
	sim.weapon_cast.emit(state.id, SLIPPER_WEAPON)
	slipper_thrown.emit(state.id)


## tsinelas' weapon with the game's own flight: it goes `distance` m straight out and stops, and hurts nobody.
func _def_for(sim: MatchSim, distance: float) -> WeaponDef:
	if _slipper_def == null:
		_slipper_def = (sim.weapon_defs[SLIPPER_WEAPON] as WeaponDef).duplicate() as WeaponDef
		_slipper_def.shape = WeaponDef.Shape.SPINNER
		_slipper_def.damage = 0
		_slipper_def.effect = StatusEffects.Type.NONE
		_slipper_def.radius = 0.0
		_slipper_def.count = 1
		_slipper_def.speed = rules.throw_speed
	var def: WeaponDef = _slipper_def.duplicate() as WeaponDef
	def.max_range = distance
	return def


## The Kick button: the can flies once, `kick_min_distance` to `kick_max_distance` m, in a random direction.
func kick(sim: MatchSim, state: PlayerState) -> void:
	var from: Vector2 = can_position
	var angle: float = sim.rng.randf() * TAU
	var distance: float = sim.rng.randf_range(rules.kick_min_distance, rules.kick_max_distance)
	var landing: Vector2 = sim.layout.keep_in_street(from + Vector2.from_angle(angle) * distance, FLING_MARGIN)
	can_up = false
	can_position = landing
	can_busy_left = rules.fling_time
	kick_left[state.id] = rules.kick_cooldown
	can_kicked.emit(state.id)
	can_flung.emit(from, [landing] as Array[Vector2])


# ---- what a throw does to the can --------------------------------------------------------

## Where a slipper thrown along `way` would send the can and the slipper, as the guide shows it (no error): the can goes on along the
## throw bent toward the side it was hit on, the slipper bounces off the other way. `lateral` (-1 to 1) is how far off the middle of
## the throw the can is.
func fling_directions(way: Vector2, lateral: float) -> Dictionary:
	var can_dir: Vector2 = way.rotated(deg_to_rad(lateral * rules.can_deflect_degrees))
	var side: float = -1.0 if lateral >= 0.0 else 1.0
	var back: float = lerpf(rules.slipper_back_degrees, rules.slipper_side_degrees, absf(lateral))
	return {"can_dir": can_dir, "slipper_dir": way.rotated(side * deg_to_rad(back))}


## How far off the throw's line the can is, from -1 to 1 (a share of `can_hit_radius`).
func lateral_of(from: Vector2, way: Vector2) -> float:
	var side: float = way.cross(can_position - from) / maxf(rules.can_hit_radius, 0.001)
	return 0.0 if absf(side) < DEAD_ON else clampf(side, -1.0, 1.0)


## The short guide a thrower sees while it aims at the can: where the can and the slipper start to go. {} when the aim would not hit
## (nothing standing to hit, or the line falls short or wide). It only shows the way, `guide_length` m of it, never where they land.
func aim_guide(sim: MatchSim, id: int) -> Dictionary:
	if not hold.has(id) or not can_up or not sim.players.has(id):
		return {}
	var from: Vector2 = sim.players[id].position
	var way: Vector2 = aim_dir[id]
	var reach: Vector2 = from + way * aim_range(id)
	var closest: Vector2 = Geometry2D.get_closest_point_to_segment(can_position, from, reach)
	if closest.distance_to(can_position) > rules.can_hit_radius:
		return {}
	var plan: Dictionary = fling_directions(way, lateral_of(from, way))
	return {"point": closest, "can_dir": plan["can_dir"], "slipper_dir": plan["slipper_dir"]}


## The landing points of one fling from `from` toward `way` over `distance` m: off by up to `fling_error_degrees` and a share of the
## length; with `bounce` a second, shorter fling follows.
func _fling_path(sim: MatchSim, from: Vector2, way: Vector2, distance: float, bounce: bool) -> Array[Vector2]:
	var turn: float = deg_to_rad(sim.rng.randf_range(-rules.fling_error_degrees, rules.fling_error_degrees))
	var first_way: Vector2 = way.rotated(turn)
	var first_distance: float = distance * (1.0 + sim.rng.randf_range(-rules.fling_distance_error, rules.fling_distance_error))
	var path: Array[Vector2] = [sim.layout.keep_in_street(from + first_way * first_distance, FLING_MARGIN)]
	if bounce:
		var second_way: Vector2 = first_way.rotated(deg_to_rad(sim.rng.randf_range(-rules.bounce_error_degrees, rules.bounce_error_degrees)))
		path.append(sim.layout.keep_in_street(path[0] + second_way * first_distance * rules.bounce_share, FLING_MARGIN))
	return path


# ---- one tick ------------------------------------------------------------------------------

func _step_game(sim: MatchSim, dt: float) -> void:
	can_busy_left = maxf(0.0, can_busy_left - dt)
	for id: int in kick_left.keys():
		kick_left[id] = maxf(0.0, kick_left[id] - dt)
	for id: int in slipper_busy.keys():
		slipper_busy[id] = maxf(0.0, slipper_busy[id] - dt)
	_step_flights(sim)
	_step_throwers(sim, dt)
	if sim.phase != MatchSim.Phase.PLAYING:
		return
	_step_can(sim)
	if can_up:
		check_tags(sim)


## A slipper in the air meets the standing can (the can and the slipper are flung), or flies its distance and lands.
func _step_flights(sim: MatchSim) -> void:
	for id: int in flights.keys():
		var projectile: WeaponSystem.Projectile = flights[id]
		var from: Vector2 = _flight_from[id]
		var now: Vector2 = projectile.position
		var hit: bool = can_up and Geometry2D.get_closest_point_to_segment(can_position, from, now).distance_to(can_position) <= rules.can_hit_radius
		var landed: bool = not sim.weapons.projectiles.has(projectile)
		_flight_from[id] = now
		if not hit and not landed:
			continue
		flights.erase(id)
		_flight_from.erase(id)
		if hit:
			sim.weapons.projectiles.erase(projectile)
			_fling(sim, id, now, projectile.direction, projectile.position - projectile.direction * projectile.travelled)
		else:
			slippers[id] = sim.layout.keep_in_street(now, FLING_MARGIN)
			slipper_busy[id] = 0.0
			slipper_landed.emit(id, slippers[id], false)
			_slipper_rests(sim, id)


## The slipper of `id` hit the can at `at` (thrown along `way` from `origin`): the can and the slipper fly off, and with `bounce_chance`
## both bounce once more.
func _fling(sim: MatchSim, id: int, at: Vector2, way: Vector2, origin: Vector2) -> void:
	var plan: Dictionary = fling_directions(way, lateral_of(origin, way))
	var bounce: bool = sim.rng.randf() < rules.bounce_chance
	var can_from: Vector2 = can_position
	var can_path: Array[Vector2] = _fling_path(sim, can_from, plan["can_dir"] as Vector2, rules.can_fling_distance, bounce)
	var slipper_path: Array[Vector2] = _fling_path(sim, at, plan["slipper_dir"] as Vector2, rules.slipper_fling_distance, bounce)
	can_up = false
	can_position = can_path[-1]
	can_busy_left = rules.fling_time * float(can_path.size())
	slippers[id] = slipper_path[-1]
	slipper_busy[id] = rules.fling_time * float(slipper_path.size())
	can_flung.emit(can_from, can_path)
	slipper_flung.emit(id, at, slipper_path)
	_slipper_rests(sim, id)


## The slipper of `id` came to rest where the sim has it. At the far end it scores at once; in the fetch area it starts the thrower's
## scared timer (`scared_seconds` spent outside the fetch area, in all).
func _slipper_rests(sim: MatchSim, id: int) -> void:
	var state: PlayerState = sim.players.get(id) as PlayerState
	if state == null or not is_attacker(state):
		slippers.erase(id)
		return
	var at: Vector2 = slippers[id]
	if at_goal(at):
		slippers.erase(id)
		slipper_busy.erase(id)
		award(sim, state)  # the slipper went to the other end by itself: the point, and the thrower is back in the throwing area
		return
	if at.y < rules.home_z:
		scared_left[id] = rules.scared_seconds


func _step_throwers(sim: MatchSim, dt: float) -> void:
	for state: PlayerState in sim.players.values():
		if not is_attacker(state):
			continue
		var id: int = state.id
		if slippers.has(id) and slipper_busy.get(id, 0.0) <= 0.0 and state.position.distance_to(slippers[id]) <= rules.pickup_radius:
			slippers.erase(id)
			slipper_busy.erase(id)
			scared_left.erase(id)
			fetched[id] = true
			slipper_fetched.emit(id)
		if fetched.has(id) and at_goal(state.position):
			award(sim, state)  # the other end: a point, and it is back in the throwing area
			if sim.phase != MatchSim.Phase.PLAYING:
				return
		if scared_left.has(id) and state.position.y >= rules.home_z:
			# the clock runs only while the thrower stays out of the fetch area; stepping in stops it, stepping out again goes on with what is left
			scared_left[id] -= dt
			if scared_left[id] <= 0.0:
				become_taya(sim, id, &"scared")
				return


## A free taya who reaches the fallen can picks it up and carries it; at its spot it stands again.
func _step_can(sim: MatchSim) -> void:
	if can_up or can_busy_left > 0.0:
		return
	var taya: PlayerState = sim.players.get(taya_id) as PlayerState
	if taya == null:
		return
	if can_carrier != taya_id:
		can_carrier = -1
		if taya.position.distance_to(can_position) > rules.pickup_radius:
			return
		can_carrier = taya_id
	can_position = taya.position
	if taya.position.distance_to(rules.can_spot) <= rules.place_radius:
		can_up = true
		can_position = rules.can_spot
		can_carrier = -1
		can_placed.emit(taya_id)


func can_tag(sim: MatchSim, guard: PlayerState, runner: PlayerState) -> bool:
	return can_up and super.can_tag(sim, guard, runner)


## The taya touched a thrower in the fetch area: the thrower is the taya now.
func _tag(sim: MatchSim, _guard: PlayerState, runner: PlayerState) -> void:
	become_taya(sim, runner.id, &"tagged")


## `id` becomes the taya (tagged, or too scared to fetch its slipper); the old taya is a thrower again, back in the throwing area with
## its slipper. Nobody can be tagged for a moment.
func become_taya(sim: MatchSim, id: int, why: StringName) -> void:
	var old: int = taya_id
	if old == id or not sim.players.has(id):
		return
	var projectile: WeaponSystem.Projectile = flights.get(id) as WeaponSystem.Projectile
	if projectile != null:
		sim.weapons.projectiles.erase(projectile)
	for table: Dictionary in [flights, _flight_from, slippers, slipper_busy, fetched, scared_left, hold, aim_dir]:
		table.erase(id)
	taya_id = id
	if can_carrier == old:
		can_carrier = -1
	grace_left = rules.swap_grace
	for kid: int in [id, old]:
		var state: PlayerState = sim.players[kid]
		state.position = _spot(sim, state)
		state.spawn_position = state.position
		state.facing = Vector2(0.0, -1.0) if is_attacker(state) else Vector2(0.0, 1.0)
		sim.end_move(state)
	taya_changed.emit(old, id, why)
	roles_swapped.emit(id)


# ---- limits and points ---------------------------------------------------------------------

## The taya stays in the fetch area; a thrower with its slipper in hand stays in the throwing area.
func constrain(_sim: MatchSim, state: PlayerState) -> void:
	if state.id == taya_id:
		state.position.y = minf(state.position.y, rules.home_z - state.radius)
	elif is_held_back(state):
		state.position.y = maxf(state.position.y, rules.home_z)


## A point for `state`; with `send_home` it is back in the throwing area, its slipper in hand. Once the time is up the point wins the match.
func award(sim: MatchSim, state: PlayerState, send_home: bool = true) -> void:
	points[state.id] = points.get(state.id, 0) + 1
	fetched.erase(state.id)
	scared_left.erase(state.id)
	scored.emit(state.team, state.id)
	if send_home:
		state.position = _spot(sim, state)
		sim.end_move(state)
	if time_left <= 0.0:
		_end(sim)


## The kid with the most points, or -1 when two or more share the most (or nobody has any).
func _leader(_sim: MatchSim) -> int:
	var best: int = -1
	var tie: bool = false
	var top: int = 0
	for id: int in points:
		if points[id] > top:
			top = points[id]
			best = id
			tie = false
		elif points[id] == top and top > 0:
			tie = true
	return -1 if tie else best


func _end(sim: MatchSim) -> void:
	winner_id = _leader(sim)
	sim.score.winner = 0  # the sim's own winner is a team: everyone is on team 0, the kid that won is `winner_id`
	for state: PlayerState in sim.players.values():
		sim.end_move(state)
	sim.phase = MatchSim.Phase.MATCH_OVER
	sim.match_won.emit(0)


func local_result(_sim: MatchSim, local_id: int) -> Dictionary:
	var theirs: int = 0
	for id: int in points:
		if id != local_id:
			theirs = maxi(theirs, points[id])
	return {"won": winner_id == local_id, "mine": points.get(local_id, 0), "theirs": theirs}


func _reset_game(sim: MatchSim) -> void:
	can_up = true
	can_position = rules.can_spot
	can_carrier = -1
	can_busy_left = 0.0
	slippers.clear()
	slipper_busy.clear()
	fetched.clear()
	scared_left.clear()
	hold.clear()
	aim_dir.clear()
	kick_left.clear()
	for id: int in flights.keys():
		sim.weapons.projectiles.erase(flights[id])
	flights.clear()
	_flight_from.clear()


## The taya waits just in front of the can; the throwers stand in a row in the throwing area, in the order of their ids.
func _spot(sim: MatchSim, state: PlayerState) -> Vector2:
	if state.id == taya_id:
		return rules.can_spot + Vector2(0.0, rules.guard_offset)
	var throwers: Array[int] = []
	for id: int in sim.players:
		if id != taya_id:
			throwers.append(id)
	return spot_for(state, true, throwers.find(state.id), throwers.size())
