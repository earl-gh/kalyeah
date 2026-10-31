class_name PlayerState
extends RefCounted
## Per-player simulation state. Plain data; MatchSim owns and mutates it.

var id: int = 0
var team: int = 0
## Position in the XZ plane (x, z). +Z is the own-base end of the lane.
var position: Vector2 = Vector2.ZERO
## Last non-zero move direction.
var facing: Vector2 = Vector2.ZERO
var hp: int = 0
## At 0 HP a player is tagged: they wait on the sidewalk until a teammate
## steps in to rescue them (hp stays 0 meanwhile).
var tagged: bool = false
## Seconds spent tagged this time.
var tag_time: float = 0.0
var radius: float = 0.0
## Where this player starts, and comes back to after a rescue (next to the post).
var spawn_position: Vector2 = Vector2.ZERO

## Rooted by a heavy attack's cast animation (light attacks never root).
var cast_lock_left: float = 0.0

## Cosmetic only (CharacterDef id).
var character_id: StringName = &""
## The two equipped weapon ids (slot 0 = weapon 1, slot 1 = weapon 2).
var weapons: Array[StringName] = []
var weapon_cooldowns: Array[float] = [0.0, 0.0]
## Weapon cards (see GameRules.card_cycle). `weapons` is the hand (an empty id = an empty slot, waiting for the shuffler). `next_card` waits
## in the shuffler's window and drops into the hand when a card is used. A used card goes to the shuffler (`shuffle_card`), which plays that
## card's own cooldown (`shuffle_left` of `shuffle_total`) and then puts the front card of `deck` in a free slot, or in the window.
## Cards used meanwhile wait in `incoming`. The cards in the hand are never slowed by the cooldown of a card you just used.
var cards: bool = false
var deck: Array[StringName] = []
var next_card: StringName = &""
var shuffle_card: StringName = &""
var shuffle_left: float = 0.0
var shuffle_total: float = 0.0
var incoming: Array[StringName] = []
## Seconds each weapon button has been held; -1 when not held.
var aim_hold: Array[float] = [-1.0, -1.0]
## Buttons from the previous tick, to see presses and releases.
var previous_buttons: int = 0
## Auto-aim taken once when a weapon button is pressed (stick-style vector), so a
## held aim never follows a moving enemy. TARGETED weapons lock a target id instead.
var aim_lock: Array[Vector2] = [Vector2.ZERO, Vector2.ZERO]
var aim_target: Array[int] = [-1, -1]
var effects: StatusEffects = StatusEffects.new()

## docs/GDD.md "Controls: tap to move, cards to cast".
## Elixir (fraction kept; the bar shows whole points). Pays for cards when GameRules.uses_elixir().
var elixir: float = 0.0
## Stamina: moving drains it, standing still refills it after `still_time` reaches the regen delay.
var stamina: float = 0.0
## Overclock (stamina ran out): the kid only walks until the bar is full again. Its own state, not a status effect.
var overclocked: bool = false
## Seconds left of the overclock's blink (the bar blinks red and does not refill yet); 0 once it refills.
var exhaust_left: float = 0.0
## Order in which the kid was tagged (rescues free the lowest first); 0 while free.
var tag_order: int = 0
## Seconds the kid has been running without stopping (the next trip roll comes at `GameRules.trip_interval`); false `trips` for training dummies.
var run_time: float = 0.0
var trips: bool = true
## Seconds the kid has stood still (any movement restarts it).
var still_time: float = 0.0
## False for training dummies: no drain, no exhaustion.
var has_stamina: bool = true
## The street point the kid walks to (straight line, wall slide), while `has_move_target`.
var move_target: Vector2 = Vector2.ZERO
var has_move_target: bool = false
## Seconds of ticks that made too little progress toward the target (the move ends at `move_stuck_time`).
var stuck_time: float = 0.0


## One cooldown, hold timer and aim lock per hand slot; a slot that is new is idle (not held, no lock).
func resize_slots() -> void:
	var count: int = weapons.size()
	var had: int = aim_hold.size()
	weapon_cooldowns.resize(count)
	aim_hold.resize(count)
	aim_lock.resize(count)
	aim_target.resize(count)
	for slot: int in range(had, count):
		weapon_cooldowns[slot] = 0.0
		aim_hold[slot] = -1.0
		aim_lock[slot] = Vector2.ZERO
		aim_target[slot] = -1


## Not tagged and not under a status that stops casting: free to use weapons.
func can_act() -> bool:
	return not tagged and effects.can_cast()
