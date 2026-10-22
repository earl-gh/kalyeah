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
## At 0 HP a player is tagged: they wait in their team's box until a teammate
## steps in to rescue them (hp stays 0 meanwhile).
var tagged: bool = false
## Seconds spent in the box this time.
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


## Not tagged and not under a status that stops casting: free to use weapons.
func can_act() -> bool:
	return not tagged and effects.can_cast()
