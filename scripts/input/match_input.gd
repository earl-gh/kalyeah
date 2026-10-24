class_name MatchInput
extends RefCounted
## Turns the local player's devices into one PlayerInput per sim tick (docs/GDD.md "Controls: tap to move, cards to cast"):
## a tap or a held drag on the street (a move target), the one card held from the hand (dragged onto the street, tapped, or
## dropped back on the hand) and the keyboard (WASD to walk, 1-4 to hold a card aimed at the mouse). No nodes; the match screen
## feeds it street points (sim XZ) and asks for `build()` every tick. It decides only what the gesture was, never a rule.
##
## A held card is a small state machine so a quick tap is never lost: the press sends the card's bit; the release sends one tick
## without it, with the aim point (a cast at that point), with nothing (a tap: the sim auto-aims) or with BTN_AIM_CANCEL.

## What letting go of a held card means.
enum Release { NONE, CAST, TAP, CANCEL }

## The longest press (seconds) on a card that still counts as a tap (GameRules.card_tap_time; set by the match screen).
var tap_time: float = 0.3
## A finger held on the street drags the move target with it (GameRules.move_follow_hold; set by the match screen).
var follow_hold: bool = true
## The camera's turn right now (CameraRules.yaw_offset_degrees): WASD follows it.
var camera_yaw: float = 0.0
var pending_buttons: int = 0

## The street point the kid should walk to, and whether a finger (or the right mouse button) still holds the street.
var move_point: Vector2 = Vector2.ZERO
var move_held: bool = false
var _move_fresh: bool = false

## The hand slot held right now (-1: none), and how the finger is doing with it.
var held_slot: int = -1
## The finger has left the card it pressed: letting go is a cast or a drop, never a tap.
var left_card: bool = false
## The finger is over the street (not over the hand): `aim_point` is the street point under it.
var over_street: bool = false
var aim_point: Vector2 = Vector2.ZERO
## The held card has been over the street at least once (`aim_point` means something).
var aimed: bool = false
## The card is held by a number key (aimed at the mouse) rather than by a finger or the left button.
var from_key: bool = false
var _pressed_ms: int = 0
var _key_cancel: bool = false
## Number keys down last tick (bit per slot): a key picks a card up only when it goes down, never by being held through a drop.
var _keys_down: int = 0
var _sent_held: bool = false
var _release: Release = Release.NONE
var _release_slot: int = -1
var _release_point: Vector2 = Vector2.ZERO


## A finger (or the right button) went down on the street at `point`: walk there.
func move_press(point: Vector2) -> void:
	move_point = point
	move_held = true
	_move_fresh = true


## The finger on the street moved to `point`: the target follows it while it is held (if `follow_hold`).
func move_drag(point: Vector2) -> void:
	if not move_held or not follow_hold:
		return
	move_point = point
	_move_fresh = true


## The finger left the street: the kid finishes the walk to the last point.
func move_release() -> void:
	move_held = false


## A card in `slot` was picked up at `now_ms`. False (ignored) while another card is held or a release is still to be sent.
func card_press(slot: int, now_ms: int) -> bool:
	if held_slot >= 0 or _release != Release.NONE or slot < 0 or slot >= PlayerInput.CARD_BITS.size():
		return false
	held_slot = slot
	_pressed_ms = now_ms
	left_card = false
	over_street = false
	aimed = false
	from_key = false
	_key_cancel = false
	_sent_held = false
	return true


## The held card's finger moved: still on the card it pressed (`on_card`), and over the street at `street_point` (null: over the hand).
func card_drag(on_card: bool, street_point: Variant) -> void:
	if held_slot < 0:
		return
	if not on_card:
		left_card = true
	over_street = street_point is Vector2
	if over_street:
		left_card = true
		aimed = true
		aim_point = street_point as Vector2


## What letting go of the held card now (at `now_ms`) means: over the street a cast there; on the card it pressed, quickly, a tap;
## anywhere else on the hand (or a press held too long without leaving the card) nothing.
func release_kind(now_ms: int) -> Release:
	if held_slot < 0:
		return Release.NONE
	if over_street:
		return Release.CAST
	if not left_card and float(now_ms - _pressed_ms) <= tap_time * 1000.0:
		return Release.TAP
	return Release.CANCEL


## The held card was let go at `now_ms`. Returns what it meant (sent with the next ticks).
func card_release(now_ms: int) -> Release:
	var kind: Release = release_kind(now_ms)
	_finish(kind)
	return kind


## The held card was dropped by something else (right-click, Esc, a system-cancelled touch, a menu): nothing is cast.
func card_cancel() -> void:
	if held_slot >= 0:
		_finish(Release.CANCEL)


func card_held() -> bool:
	return held_slot >= 0


func _finish(kind: Release) -> void:
	_release = kind
	_release_slot = held_slot
	_release_point = aim_point
	held_slot = -1
	left_card = false
	over_street = false
	aimed = false
	from_key = false


## Forget everything held or pending (the pick screen or a menu opened over the controls).
func clear() -> void:
	pending_buttons = 0
	move_held = false
	_move_fresh = false
	held_slot = -1
	left_card = false
	over_street = false
	aimed = false
	from_key = false
	_release = Release.NONE
	_release_slot = -1
	_sent_held = false


## This tick's input. `flip` turns WASD around for the team that defends the -Z base; `key_point` is called with no arguments
## for the street point under the mouse (Vector2, or null off the street) while a number key holds a card.
func build(flip: bool, key_point: Callable, tick: int, now_ms: int = 0) -> PlayerInput:
	var world_move: Vector2 = LocalInput.to_world(LocalInput.keyboard_vector(), flip, camera_yaw)
	var input: PlayerInput = PlayerInput.create(world_move, Vector2.ZERO, pending_buttons, tick)
	pending_buttons = 0
	if _move_fresh or (move_held and follow_hold):
		input.with_move_target(move_point)
		_move_fresh = false
	_read_keys(key_point, now_ms)
	if held_slot >= 0:
		input.buttons |= PlayerInput.CARD_BITS[held_slot]
		_sent_held = true
		if over_street:
			input.with_aim_point(aim_point)
	elif _release != Release.NONE:
		var bit: int = PlayerInput.CARD_BITS[_release_slot]
		if not _sent_held:
			# a press and release inside one tick: the sim must see the card held once before the release
			input.buttons |= bit
			_sent_held = true
		else:
			match _release:
				Release.CAST:
					input.with_aim_point(_release_point)
				Release.CANCEL:
					input.buttons |= PlayerInput.BTN_AIM_CANCEL
			_release = Release.NONE
			_release_slot = -1
			_sent_held = false
	return input


## Keys 1-4 hold a card aimed at the mouse pointer (desktop testing); Esc while holding drops it.
func _read_keys(key_point: Callable, now_ms: int) -> void:
	var was_down: int = _keys_down
	_keys_down = 0
	for slot: int in PlayerInput.CARD_BITS.size():
		if LocalInput.card_key_held(slot):
			_keys_down |= 1 << slot
	if held_slot >= 0 and not from_key:
		return
	if held_slot >= 0:
		_key_cancel = _key_cancel or LocalInput.cancel_held()
		if LocalInput.card_key_held(held_slot):
			card_drag(false, key_point.call() if key_point.is_valid() else null)
			return
		if _key_cancel or not over_street:
			card_cancel()
		else:
			card_release(now_ms)
		return
	if _release != Release.NONE:
		return
	for slot: int in PlayerInput.CARD_BITS.size():
		var fresh: bool = (_keys_down & (1 << slot)) != 0 and (was_down & (1 << slot)) == 0
		if fresh and card_press(slot, now_ms):
			from_key = true
			card_drag(false, key_point.call() if key_point.is_valid() else null)
			return
