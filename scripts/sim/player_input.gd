class_name PlayerInput
extends RefCounted
## The one input format: touch, keyboard and network all produce this.
## Only move/aim/buttons/tick and street points, never any gameplay decision.
## docs/GDD.md "Controls: tap to move, cards to cast".

## Which hand slot is held (one card at a time); the bit going off is the release, which casts.
const BTN_CARD_1: int = 1 << 0
const BTN_CARD_2: int = 1 << 1
const BTN_CARD_3: int = 1 << 2
const BTN_CARD_4: int = 1 << 3
## The hand slots in order (slot 0 = BTN_CARD_1).
const CARD_BITS: Array[int] = [BTN_CARD_1, BTN_CARD_2, BTN_CARD_3, BTN_CARD_4]
## Superseded names (two weapon buttons) kept as aliases of the first two card bits while the HUD moves to four cards.
const BTN_WEAPON_1: int = BTN_CARD_1
const BTN_WEAPON_2: int = BTN_CARD_2
## Sent on the tick a card is released over the hand (or right-click / Esc): no cast, nothing spent.
const BTN_AIM_CANCEL: int = 1 << 5
## `move_target` is a new street point this tick: it replaces the kid's move target.
const BTN_MOVE_TARGET: int = 1 << 6
## `aim_point` is the street point under the held card. Off on the release tick = a tap (auto-aim).
const BTN_AIM_POINT: int = 1 << 7
## Every bit a client may send; anything else is dropped by the server.
const KNOWN_BITS: int = BTN_CARD_1 | BTN_CARD_2 | BTN_CARD_3 | BTN_CARD_4 | BTN_AIM_CANCEL | BTN_MOVE_TARGET | BTN_AIM_POINT

## Keyboard stick (WASD), x = left/right, y = along the lane (screen up = -1). Length <= 1. Non-zero clears the move target.
var move: Vector2 = Vector2.ZERO
## Stick-style aim relative to the kid (length 0..1 of the range; zero = auto-aim). Used when BTN_AIM_POINT is off.
var aim: Vector2 = Vector2.ZERO
var buttons: int = 0
var tick: int = 0
## A street point in sim XZ (only read with BTN_MOVE_TARGET). The server pulls it into the street and out of cover.
var move_target: Vector2 = Vector2.ZERO
## A street point in sim XZ (only read with BTN_AIM_POINT). The server clamps it to the weapon's range from the kid.
var aim_point: Vector2 = Vector2.ZERO


static func create(p_move: Vector2, p_aim: Vector2, p_buttons: int, p_tick: int) -> PlayerInput:
	var input: PlayerInput = PlayerInput.new()
	input.move = p_move
	input.aim = p_aim
	input.buttons = p_buttons
	input.tick = p_tick
	return input


## Sets `move_target` and BTN_MOVE_TARGET (a tap or a held drag on the street). Returns self.
func with_move_target(point: Vector2) -> PlayerInput:
	move_target = point
	buttons |= BTN_MOVE_TARGET
	return self


## Sets `aim_point` and BTN_AIM_POINT (the point under a dragged card). Returns self.
func with_aim_point(point: Vector2) -> PlayerInput:
	aim_point = point
	buttons |= BTN_AIM_POINT
	return self


func is_pressed(button: int) -> bool:
	return (buttons & button) != 0
