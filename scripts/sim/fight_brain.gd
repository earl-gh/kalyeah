class_name FightBrain
extends DummyBrain
## A practice sparring partner: a training dummy that fights back. It keeps its spot until an enemy comes
## within `bot_aggro_range`, then closes to a standoff distance, strafes, and taps its weapons the way a
## finger would (press one tick, release the next, aimed at the enemy with a little error). A hurt one
## heals itself, a free one fetches a tagged teammate when no enemy is close. Same PlayerInput as everyone
## else, no RNG of its own (the match's seeded one for the aim error), so it is deterministic and testable.
## `skill` (0 to 1, `bot_skill` in the rules unless set) is how well it aims: at 0 it throws at where the enemy
## is, at 1 where the enemy will be, and its aim error shrinks. At any skill it throws ground circles at the
## enemy's distance (not at full range) and never throws a projectile into cover, stepping aside until it can.

## A tagged teammate is only fetched when no enemy is this close (metres).
const RESCUE_SAFE_DISTANCE: float = 7.0
## Below this share of health a bot heals itself.
const HEAL_BELOW: float = 0.5
## Closest and farthest a bot stands from an enemy, whatever its weapons reach.
const STANDOFF_MIN: float = 2.5
const STANDOFF_MAX: float = 7.0
## Backs off when the enemy is closer than the standoff by more than this.
const TOO_CLOSE_SLACK: float = 1.0
## Seconds between changes of strafing side.
const STRAFE_SWITCH: float = 1.5
## At full skill the aim error is this share of `bot_aim_error_degrees`.
const SKILLED_ERROR_SHARE: float = 0.3
## How far to the side (metres) a bot looks for a spot with a clear line, nearest first.
const SIDESTEPS: Array[float] = [2.0, 4.0, 6.0, 8.0]
## A projectile is blocked by cover this much bigger than the cover (its own width).
const COVER_MARGIN: float = 0.3

## Aim quality 0 to 1; -1 takes `bot_skill` from the rules.
var skill: float = -1.0

var _press_slot: int = -1
var _aim: Vector2 = Vector2.ZERO
var _cooldown: float = 0.0
## Where each enemy was at the last think, and how fast it is going (metres a second), for leading shots.
var _seen: Dictionary[int, Vector2] = {}
var _velocity: Dictionary[int, Vector2] = {}


## A fighter that waits at `spot`.
static func fighter(spot: Vector2) -> FightBrain:
	var brain: FightBrain = FightBrain.new()
	brain.mode = Mode.STAND
	brain.home = spot
	return brain


func think(state: PlayerState, sim: MatchSim) -> PlayerInput:
	_cooldown = maxf(0.0, _cooldown - sim.rules.tick_dt())
	_track(state, sim)
	if _press_slot >= 0:
		# the second half of a tap: let go, aimed
		_press_slot = -1
		return PlayerInput.create(Vector2.ZERO, _aim, 0, sim.tick)
	var enemy: PlayerState = sim.nearest_enemy(state, sim.rules.bot_aggro_range)
	if enemy == null:
		return _no_enemy(state, sim)
	return _fight(state, sim, enemy)


func _no_enemy(state: PlayerState, sim: MatchSim) -> PlayerInput:
	var target: Vector2 = home
	if _teammate_tagged(state, sim):
		target = sim.layout.jail_rect(sim.side_for_team(state.team)).get_center()
	return _walk_to(state, target)


func _fight(state: PlayerState, sim: MatchSim, enemy: PlayerState) -> PlayerInput:
	var offset: Vector2 = enemy.position - state.position
	var distance: float = offset.length()
	var toward: Vector2 = offset.normalized() if distance > 0.001 else Vector2.DOWN
	if _teammate_tagged(state, sim) and distance > RESCUE_SAFE_DISTANCE:
		return _walk_to(state, sim.layout.jail_rect(sim.side_for_team(state.team)).get_center())
	var blocked: bool = _in_cover(state.position, enemy.position, sim)
	var slot: int = _slot_to_cast(state, sim, distance, blocked)
	if slot >= 0 and _cooldown <= 0.0 and state.can_act():
		_press_slot = slot
		_cooldown = sim.rules.bot_cast_gap
		var def: WeaponDef = sim.weapon_defs[state.weapons[slot]]
		# a tap on a heal heals yourself; a drag would look for a teammate
		_aim = Vector2.ZERO if def.kind == WeaponDef.Kind.HEAL else _aim_at(state, enemy, def, sim)
		var bit: int = WeaponCasting.WEAPON_BITS[slot]
		return PlayerInput.create(Vector2.ZERO, _aim, bit, sim.tick)
	var standoff: float = _standoff(state, sim)
	var move: Vector2 = Vector2.ZERO
	if blocked:
		move = _step_aside(state, enemy, sim)
	elif distance > standoff + 0.5:
		move = toward
	elif distance < standoff - TOO_CLOSE_SLACK:
		move = -toward
	else:
		var side: float = 1.0 if (int(float(sim.tick) * sim.rules.tick_dt() / STRAFE_SWITCH) + state.id) % 2 == 0 else -1.0
		move = toward.rotated(side * PI / 2.0) * 0.6
	return PlayerInput.create(move, Vector2.ZERO, 0, sim.tick)


## The first ready weapon that makes sense at `distance`: a heal when hurt, a shield when the enemy is
## near, anything else once the enemy is within its reach. -1 for none.
func _slot_to_cast(state: PlayerState, sim: MatchSim, distance: float, blocked: bool) -> int:
	for slot: int in state.weapons.size():
		if not sim.weapon_ready(state, slot):
			continue
		var def: WeaponDef = sim.weapon_defs[state.weapons[slot]]
		if def.kind == WeaponDef.Kind.HEAL:
			if float(state.hp) < float(sim.rules.player_max_hp) * HEAL_BELOW:
				return slot
			continue
		if def.shape == WeaponDef.Shape.SHIELD:
			if distance <= STANDOFF_MAX:
				return slot
			continue
		if blocked and _is_projectile(def):
			continue
		if distance <= def.max_range * 0.95:
			return slot
	return -1


## How far from the enemy it likes to stand: a share of its longest reach, kept inside STANDOFF_MIN..MAX.
func _standoff(state: PlayerState, sim: MatchSim) -> float:
	var reach: float = 0.0
	for weapon_id: StringName in state.weapons:
		var def: WeaponDef = sim.weapon_defs[weapon_id]
		if def.kind != WeaponDef.Kind.HEAL and def.shape != WeaponDef.Shape.SHIELD:
			reach = maxf(reach, def.max_range)
	return clampf(reach * sim.rules.bot_standoff_share, STANDOFF_MIN, STANDOFF_MAX)


## The aim to send for `def` at `enemy`: a direction for thrown and cone weapons, and for a ground circle,
## trap or field a length too (the weapon lands at `aim * max_range`, so the length is the distance), toward where
## the enemy will be when it lands (as far ahead as `skill` says), with a little error.
func _aim_at(state: PlayerState, enemy: PlayerState, def: WeaponDef, sim: MatchSim) -> Vector2:
	var point: Vector2 = enemy.position + _velocity.get(enemy.id, Vector2.ZERO) * _flight_time(state, enemy, def) * _skill(sim)
	var offset: Vector2 = point - state.position
	if offset.length() < 0.001:
		offset = enemy.position - state.position
	var aim: Vector2 = offset.normalized()
	if def.shape == WeaponDef.Shape.GROUND_AOE or def.shape == WeaponDef.Shape.TRAP or def.shape == WeaponDef.Shape.FIELD:
		aim = (offset / maxf(def.max_range, 0.001)).limit_length(1.0)
	var limit: float = deg_to_rad(sim.rules.bot_aim_error_degrees) * lerpf(1.0, SKILLED_ERROR_SHARE, _skill(sim))
	return aim.rotated(sim.rng.randf_range(-limit, limit))


## Seconds from the press to the weapon landing on the enemy (a delay, and a flight at the weapon's speed).
func _flight_time(state: PlayerState, enemy: PlayerState, def: WeaponDef) -> float:
	var distance: float = state.position.distance_to(enemy.position)
	var flight: float = distance / def.speed if def.speed > 0.0 else 0.0
	return def.delay + flight


## Projectiles stop at cover; a ground circle, a trap, a field, a shield and a cone do not.
func _is_projectile(def: WeaponDef) -> bool:
	return def.shape == WeaponDef.Shape.TARGETED or def.shape == WeaponDef.Shape.BOOMERANG \
		or def.shape == WeaponDef.Shape.BOUNCER or def.shape == WeaponDef.Shape.SPINNER


func _skill(sim: MatchSim) -> float:
	return clampf(skill if skill >= 0.0 else sim.rules.bot_skill, 0.0, 1.0)


## Remembers where the enemies are, to know how fast they are going.
func _track(state: PlayerState, sim: MatchSim) -> void:
	var dt: float = sim.rules.tick_dt()
	for enemy: PlayerState in sim.enemies_of(state.team):
		if _seen.has(enemy.id):
			_velocity[enemy.id] = (enemy.position - _seen[enemy.id]) / dt
		_seen[enemy.id] = enemy.position


## True when cover stands between `from` and `to`.
func _in_cover(from: Vector2, to: Vector2, sim: MatchSim) -> bool:
	for rect: Rect2 in sim.obstacles:
		if segment_hits_rect(from, to, rect.grow(COVER_MARGIN)):
			return true
	return false


## The way to go to get a clear line to `enemy`: the nearest sidestep with one, else straight at them.
func _step_aside(state: PlayerState, enemy: PlayerState, sim: MatchSim) -> Vector2:
	var toward: Vector2 = (enemy.position - state.position).normalized()
	var side: Vector2 = toward.orthogonal()
	for step: float in SIDESTEPS:
		for way: float in [1.0, -1.0]:
			var spot: Vector2 = state.position + side * step * way
			if not _in_cover(spot, enemy.position, sim) and not _inside_cover(spot, sim):
				return (spot - state.position).normalized()
	return toward


func _inside_cover(point: Vector2, sim: MatchSim) -> bool:
	for rect: Rect2 in sim.obstacles:
		if rect.grow(COVER_MARGIN).has_point(point):
			return true
	return false


## True when the segment from `a` to `b` touches `rect` (the slab method).
static func segment_hits_rect(a: Vector2, b: Vector2, rect: Rect2) -> bool:
	var direction: Vector2 = b - a
	var near: float = 0.0
	var far: float = 1.0
	for axis: int in 2:
		var origin: float = a[axis]
		var step: float = direction[axis]
		var low: float = rect.position[axis]
		var high: float = rect.end[axis]
		if is_zero_approx(step):
			if origin < low or origin > high:
				return false
			continue
		var t1: float = (low - origin) / step
		var t2: float = (high - origin) / step
		near = maxf(near, minf(t1, t2))
		far = minf(far, maxf(t1, t2))
		if near > far:
			return false
	return true


func _teammate_tagged(state: PlayerState, sim: MatchSim) -> bool:
	for id: int in sim.players:
		var other: PlayerState = sim.players[id]
		if other.team == state.team and other.tagged:
			return true
	return false


func _walk_to(state: PlayerState, target: Vector2) -> PlayerInput:
	var offset: Vector2 = target - state.position
	if offset.length() <= HOME_TOLERANCE:
		return PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, 0)
	return PlayerInput.create(offset.normalized(), Vector2.ZERO, 0, 0)
