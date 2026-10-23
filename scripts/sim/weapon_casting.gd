class_name WeaponCasting
extends RefCounted
## Authoritative weapon-button arbitration and cast-on-release state machine.
## A player can aim only one slot at a time; simultaneous presses prefer slot 0.

const WEAPON_BITS: Array[int] = [PlayerInput.BTN_WEAPON_1, PlayerInput.BTN_WEAPON_2]


## Advances one player's weapon input. Releasing an active aim consumes the tick,
## so another slot needs a fresh press on a later input before it can start.
func handle(sim: MatchSim, state: PlayerState, input: PlayerInput, dt: float) -> void:
	var active_slot: int = _active_slot(state)
	if active_slot >= 0:
		_step_active(sim, state, input, dt, active_slot)
	else:
		_start_fresh_aim(sim, state, input)
	state.previous_buttons = input.buttons


## A weapon slot can start aiming: equipped, off cooldown, and casting allowed.
func weapon_ready(state: PlayerState, slot: int) -> bool:
	return (
		not state.tagged and state.effects.can_cast()
		and slot >= 0 and slot < state.weapons.size()
		and slot < state.weapon_cooldowns.size()
		and state.weapon_cooldowns[slot] <= 0.0
	)


## The aim a release would use: the stick if it is past the deadzone, otherwise the
## auto-aim locked at the press (or a live auto-aim if the slot is not held yet).
func resolved_aim(sim: MatchSim, state: PlayerState, slot: int, stick: Vector2) -> Vector2:
	if stick.length() >= sim.rules.aim_deadzone:
		return stick.limit_length(1.0)
	if state.aim_hold[slot] >= 0.0:
		return state.aim_lock[slot]
	return sim.weapons.auto_aim(sim, state, sim.weapon_defs[state.weapons[slot]])


## The target a TARGETED weapon would throw at (locked at the press while held).
func resolved_target(sim: MatchSim, state: PlayerState, slot: int) -> int:
	if state.aim_hold[slot] >= 0.0:
		return state.aim_target[slot]
	var target: PlayerState = sim.nearest_enemy(state, sim.weapon_defs[state.weapons[slot]].max_range)
	return target.id if target != null else -1


## Finds the sole active aim. If malformed state contains more than one, the
## lower slot wins deterministically and every later slot is cancelled.
func _active_slot(state: PlayerState) -> int:
	var active: int = -1
	for slot: int in state.aim_hold.size():
		if state.aim_hold[slot] < 0.0:
			continue
		if active < 0:
			active = slot
		else:
			state.aim_hold[slot] = -1.0
	return active


func _step_active(sim: MatchSim, state: PlayerState, input: PlayerInput, dt: float, slot: int) -> void:
	var bit: int = WEAPON_BITS[slot]
	if input.is_pressed(bit):
		if state.tagged or not state.effects.can_cast():
			state.aim_hold[slot] = -1.0
		else:
			state.aim_hold[slot] += dt
		return
	if (state.previous_buttons & bit) != 0:
		if not input.is_pressed(PlayerInput.BTN_AIM_CANCEL):
			_try_fire(sim, state, slot, input.aim, state.aim_hold[slot])
	state.aim_hold[slot] = -1.0


func _start_fresh_aim(sim: MatchSim, state: PlayerState, input: PlayerInput) -> void:
	for slot: int in mini(WEAPON_BITS.size(), state.aim_hold.size()):
		var bit: int = WEAPON_BITS[slot]
		var fresh_press: bool = input.is_pressed(bit) and (state.previous_buttons & bit) == 0
		if not fresh_press:
			continue
		if weapon_ready(state, slot):
			state.aim_hold[slot] = 0.0
			_lock_aim(sim, state, slot)
		# The lowest freshly pressed slot owns this input even when it was not ready.
		break


func _lock_aim(sim: MatchSim, state: PlayerState, slot: int) -> void:
	var def: WeaponDef = sim.weapon_defs[state.weapons[slot]]
	state.aim_lock[slot] = sim.weapons.auto_aim(sim, state, def)
	var target: PlayerState = sim.nearest_enemy(state, def.max_range)
	state.aim_target[slot] = target.id if target != null else -1


func _try_fire(sim: MatchSim, state: PlayerState, slot: int, stick: Vector2, hold_seconds: float) -> void:
	if not state.can_act() or not weapon_ready(state, slot):
		return
	var def: WeaponDef = sim.weapon_defs[state.weapons[slot]]
	var aim: Vector2 = resolved_aim(sim, state, slot, stick)
	if sim.weapons.fire(sim, state, def, aim, hold_seconds, state.aim_target[slot]):
		state.weapon_cooldowns[slot] = def.cooldown
		sim.cycle_used(state, slot)
		state.cast_lock_left = def.cast_lock
		sim.weapon_cast.emit(state.id, def.id)
