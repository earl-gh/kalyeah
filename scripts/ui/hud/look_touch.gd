class_name LookTouch
extends Node
## Turning the view of the shoulder camera (owner, 2026-11-09: third-person like the hero shooters; docs/GDD.md "Shoulder view"): a finger
## that lands on the right part of the screen (right of the joystick's zone) on no button and drags turns the view by its move (`look_dragged`,
## UI pixels; the camera turns them into degrees). It listens to unhandled input, so a touch a button, the ability arc or the joystick took
## never reaches it, and it follows one pointer by its index, so the left thumb on the joystick is never blocked. It never decides a rule.

signal look_dragged(drag: Vector2)

const NO_POINTER: int = -1

## Touches are taken (false under a menu, while tagged, during the opening show).
var active: bool = false
## The left edge of the zone (UI pixels): set by the HUD from the screen's width and the joystick's share.
var zone_left: float = 0.0
var _pointer: int = NO_POINTER
var _last: Vector2 = Vector2.ZERO


func _init() -> void:
	name = "LookTouch"


func holding() -> bool:
	return _pointer != NO_POINTER


func release() -> void:
	_pointer = NO_POINTER


func _unhandled_input(event: InputEvent) -> void:
	if handle(event):
		get_viewport().set_input_as_handled()


## One input event (tests call it directly), positions in the UI's pixels. Returns true when it was a look gesture.
func handle(event: InputEvent) -> bool:
	if event is InputEventScreenTouch:
		var touch: InputEventScreenTouch = event as InputEventScreenTouch
		if touch.pressed:
			if not active or _pointer != NO_POINTER or touch.position.x < zone_left:
				return false
			_pointer = touch.index
			_last = touch.position
			return true
		if touch.index == _pointer:
			release()
			return true
	elif event is InputEventScreenDrag:
		var drag: InputEventScreenDrag = event as InputEventScreenDrag
		if drag.index == _pointer:
			look_dragged.emit(drag.position - _last)
			_last = drag.position
			return true
	return false
