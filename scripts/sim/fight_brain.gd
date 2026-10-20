class_name FightBrain
extends DummyBrain
## A practice sparring partner: a training dummy that fights back. It keeps its spot until an enemy comes
## within `bot_aggro_range`, then closes to a standoff distance, strafes, and taps its weapons the way a
## finger would (press one tick, release the next, aimed at the enemy with a little error). A hurt one
## heals itself, a free one fetches a tagged teammate when no enemy is close. Same PlayerInput as everyone
## else, no RNG of its own (the match's seeded one for the aim error), so it is deterministic and testable.

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

var _press_slot: int = -1
var _aim: Vector2 = Vector2.ZERO
var _cooldown: float = 0.0


## A fighter that waits at `spot`.
static func fighter(spot: Vector2) -> FightBrain:
	var brain: FightBrain = FightBrain.new()
	brain.mode = Mode.STAND
	brain.home = spot
	return brain


func think(state: PlayerState, sim: MatchSim) -> PlayerInput:
	_cooldown = maxf(0.0, _cooldown - sim.rules.tick_dt())
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
	var slot: int = _slot_to_cast(state, sim, distance)
	if slot >= 0 and _cooldown <= 0.0 and state.can_act():
		_press_slot = slot
		_cooldown = sim.rules.bot_cast_gap
		# a tap on a heal heals yourself; a drag would look for a teammate
		var heals: bool = sim.weapon_defs[state.weapons[slot]].kind == WeaponDef.Kind.HEAL
		_aim = Vector2.ZERO if heals else _with_error(toward, sim)
		var bit: int = WeaponCasting.WEAPON_BITS[slot]
		return PlayerInput.create(Vector2.ZERO, _aim, bit, sim.tick)
	var standoff: float = _standoff(state, sim)
	var move: Vector2 = Vector2.ZERO
	if distance > standoff + 0.5:
		move = toward
	elif distance < standoff - TOO_CLOSE_SLACK:
		move = -toward
	else:
		var side: float = 1.0 if (int(float(sim.tick) * sim.rules.tick_dt() / STRAFE_SWITCH) + state.id) % 2 == 0 else -1.0
		move = toward.rotated(side * PI / 2.0) * 0.6
	return PlayerInput.create(move, Vector2.ZERO, 0, sim.tick)


## The first ready weapon that makes sense at `distance`: a heal when hurt, a shield when the enemy is
## near, anything else once the enemy is within its reach. -1 for none.
func _slot_to_cast(state: PlayerState, sim: MatchSim, distance: float) -> int:
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


## `direction` turned by a random error of up to `bot_aim_error_degrees` either way (the match's own RNG).
func _with_error(direction: Vector2, sim: MatchSim) -> Vector2:
	var limit: float = deg_to_rad(sim.rules.bot_aim_error_degrees)
	return direction.rotated(sim.rng.randf_range(-limit, limit))


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
