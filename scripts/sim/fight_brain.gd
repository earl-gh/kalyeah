class_name FightBrain
extends DummyBrain
## A practice sparring partner: a training dummy that fights back. It keeps its spot until an enemy comes
## within `bot_aggro_range`, then closes to a standoff distance, strafes, and taps its weapons the way a
## finger would (press one tick, release the next, aimed at the enemy with a little error). A hurt one
## heals itself, a free one fetches a tagged teammate when no enemy is close. Same PlayerInput as everyone
## else, no RNG of its own (the match's seeded one for the aim error), so it is deterministic and testable.
## Its priorities, in order: 1. tag enemies by killing them (any enemy within `bot_aggro_range` is fought, whatever else is going on),
## 2. stay at its post (or the enemy base for a rusher), 3. rescue a tagged teammate, but only from its post, so a rescue never
## pulls a bot off the job it is doing: it sets off once it stands at its post, and keeps going until the teammate is free or an enemy shows up.
## `skill` (0 to 1, `bot_skill` in the rules unless set) is how well it aims: at 0 it throws at where the enemy
## is, at 1 where the enemy will be, and its aim error shrinks. At any skill it throws ground circles at the
## enemy's distance (not at full range) and never throws a projectile into cover, stepping aside until it can.
## It plays by the players' rules (docs/GDD.md "Bots and the coach play by the same rules"): it walks by move targets, casts only the
## cards it can afford (released at a street point, a heal as a tap), and with no enemy near it stops to refill its stamina below
## `bot_stamina_rest` until it is back to `bot_stamina_resume`.

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
## While fighting it steers by putting its move target this far ahead in the way it wants to go (re-sent every tick).
const STEER_AHEAD: float = 1.0

## Aim quality 0 to 1; -1 takes `bot_skill` from the rules.
var skill: float = -1.0

var _press_slot: int = -1
## True once a bot standing at its post has set off to rescue a tagged teammate (it keeps going until it is free or an enemy comes).
var _rescuing: bool = false
var _aim: Vector2 = Vector2.ZERO
## The street point the held card is released at; `_tap_release` = released as a tap (a heal on itself).
var _aim_point: Vector2 = Vector2.ZERO
var _tap_release: bool = false
## True while it stands to refill its stamina.
var _resting: bool = false
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
		# the second half of a tap: let go, at the aimed point (or as a tap)
		_press_slot = -1
		var release: PlayerInput = PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick)
		return release if _tap_release else release.with_aim_point(_aim_point)
	var enemy: PlayerState = sim.nearest_enemy(state, sim.rules.bot_aggro_range)
	if _rest_due(state, sim, enemy):
		return _stop(state, sim)
	if enemy == null:
		return _no_enemy(state, sim)
	_rescuing = false
	return _fight(state, sim, enemy)


## A brain choice, not a rule: with no enemy near and stamina below `bot_stamina_rest`, stand until it is back to `bot_stamina_resume`.
func _rest_due(state: PlayerState, sim: MatchSim, enemy: PlayerState) -> bool:
	if not sim.rules.stamina_enabled or not state.has_stamina or enemy != null:
		_resting = false
		return false
	var share: float = state.stamina / maxf(sim.rules.stamina_max, 0.001)
	if share < sim.rules.bot_stamina_rest:
		_resting = true
	elif share >= sim.rules.bot_stamina_resume:
		_resting = false
	return _resting


## Stands where it is (a move target on itself ends any walk).
func _stop(state: PlayerState, sim: MatchSim) -> PlayerInput:
	return PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick).with_move_target(state.position)


## Walks the way of `direction` (zero: stands), by a move target `STEER_AHEAD` in front of it.
func _steer(state: PlayerState, sim: MatchSim, direction: Vector2) -> PlayerInput:
	if direction.length_squared() <= 0.0:
		return _stop(state, sim)
	return PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, sim.tick).with_move_target(state.position + direction.normalized() * STEER_AHEAD)


## No enemy in reach: stay at the post, and rescue only from there (priorities 2 and 3).
func _no_enemy(state: PlayerState, sim: MatchSim) -> PlayerInput:
	if _rescue_due(state, sim):
		return _walk_to(state, sim.layout.jail_rect(sim.side_for_team(state.team)).get_center())
	return _walk_to(state, home)


## True while this bot should be off rescuing: a teammate is tagged and the bot is either already on its way or standing at its post.
func _rescue_due(state: PlayerState, sim: MatchSim) -> bool:
	if not _teammate_tagged(state, sim):
		_rescuing = false
		return false
	if not _rescuing and _at_post(state, sim):
		_rescuing = true
	return _rescuing


## Whether the bot stands where it is meant to stay (a fighter: its spot).
func _at_post(state: PlayerState, _sim: MatchSim) -> bool:
	return state.position.distance_to(home) <= HOME_TOLERANCE * 2.0


func _fight(state: PlayerState, sim: MatchSim, enemy: PlayerState) -> PlayerInput:
	var offset: Vector2 = enemy.position - state.position
	var distance: float = offset.length()
	var toward: Vector2 = offset.normalized() if distance > 0.001 else Vector2.DOWN
	var blocked: bool = _in_cover(state.position, enemy.position, sim)
	# only looked for when it could cast now (the hand scan is the brain's hottest path)
	var slot: int = _slot_to_cast(state, sim, distance, blocked) if _cooldown <= 0.0 and state.can_act() else -1
	if slot >= 0:
		_press_slot = slot
		_cooldown = sim.rules.bot_cast_gap
		var def: WeaponDef = sim.weapon_defs[state.weapons[slot]]
		# a tap on a heal heals yourself; anything else is released at the point it aims at
		_tap_release = def.kind == WeaponDef.Kind.HEAL
		_aim = Vector2.ZERO if _tap_release else _aim_at(state, enemy, def, sim)
		_aim_point = state.position + _aim * maxf(def.max_range, 1.0)
		var bit: int = WeaponCasting.CARD_BITS[slot]
		return PlayerInput.create(Vector2.ZERO, Vector2.ZERO, bit, sim.tick)
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
		move = toward.rotated(side * PI / 2.0)
	return _steer(state, sim, move)


## The first ready weapon that makes sense at `distance`: a heal when hurt, a shield when the enemy is
## near, anything else once the enemy is within its reach. -1 for none.
func _slot_to_cast(state: PlayerState, sim: MatchSim, distance: float, blocked: bool) -> int:
	var paying: bool = sim.pays_elixir(state)
	for slot: int in state.weapons.size():
		var def: WeaponDef = sim.weapon_defs.get(state.weapons[slot]) as WeaponDef
		# the cheap test first: most ticks of a fight wait for elixir
		if def == null or (paying and state.elixir + MatchSim.EPSILON < float(def.elixir_cost)) or not sim.weapon_ready(state, slot):
			continue
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
		if weapon_id == &"":
			continue  # an empty slot: its card is in the shuffler
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


## Walks to `target` by a move target (stands once within HOME_TOLERANCE).
func _walk_to(state: PlayerState, target: Vector2) -> PlayerInput:
	var point: Vector2 = state.position if state.position.distance_to(target) <= HOME_TOLERANCE else target
	return PlayerInput.create(Vector2.ZERO, Vector2.ZERO, 0, 0).with_move_target(point)
