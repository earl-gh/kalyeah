class_name StreetTouch
extends Node
## Taps on the street (docs/GDD.md "Moving: tap a point, the kid walks there"): a finger that lands on no HUD control is a move
## order at that screen point; while it stays down it drags the target with it. It listens to unhandled input,
## so a touch the hand, the header, a menu or a dialog took never reaches it, and it follows one pointer at a time by its index, so a
## finger holding a card never blocks a second finger tapping the street. It reports screen points; the match screen turns them into
## street points.

signal move_pressed(at: Vector2)
signal move_dragged(at: Vector2)
signal move_released

const NO_POINTER: int = -1

## Taps are taken (false under a menu, while tagged, during the opening show).
var active: bool = false
var _pointer: int = NO_POINTER


func _init() -> void:
	name = "StreetTouch"


func holding() -> bool:
	return _pointer != NO_POINTER


## Lets go of the street (the controls were switched off): the kid finishes its walk.
func release() -> void:
	if _pointer == NO_POINTER:
		return
	_pointer = NO_POINTER
	move_released.emit()


func _unhandled_input(event: InputEvent) -> void:
	handle(event)


## One input event (tests call it directly). Returns true when it was a street gesture.
func handle(event: InputEvent) -> bool:
	if event is InputEventScreenTouch:
		var touch: InputEventScreenTouch = event as InputEventScreenTouch
		if touch.pressed:
			return _press(touch.index, touch.position)
		if touch.index == _pointer:
			release()
			return true
	elif event is InputEventScreenDrag:
		var drag: InputEventScreenDrag = event as InputEventScreenDrag
		if drag.index == _pointer:
			move_dragged.emit(drag.position)
			return true
	return false


func _press(pointer: int, at: Vector2) -> bool:
	if not active:
		return false
	# a new finger takes over the street: the newest tap is the order
	_pointer = pointer
	move_pressed.emit(at)
	return true
