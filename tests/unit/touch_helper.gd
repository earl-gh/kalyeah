class_name TouchHelper
extends RefCounted
## What a phone sends, for tests. Touches are the only input the game plays with; a Control gets a touch as the left-button mouse
## event Godot makes from it (`input_devices/pointing/emulate_mouse_from_touch`), marked as emulated.


static func touch(index: int, at: Vector2, pressed: bool, canceled: bool = false) -> InputEventScreenTouch:
	var event: InputEventScreenTouch = InputEventScreenTouch.new()
	event.index = index
	event.position = at
	event.pressed = pressed
	event.canceled = canceled
	return event


static func drag(index: int, at: Vector2, relative: Vector2 = Vector2.ZERO) -> InputEventScreenDrag:
	var event: InputEventScreenDrag = InputEventScreenDrag.new()
	event.index = index
	event.position = at
	event.relative = relative
	return event


## The press or release a Control sees for a finger at `at`.
static func click(at: Vector2, pressed: bool) -> InputEventMouseButton:
	var event: InputEventMouseButton = InputEventMouseButton.new()
	event.device = InputEvent.DEVICE_ID_EMULATION
	event.button_index = MOUSE_BUTTON_LEFT
	event.position = at
	event.pressed = pressed
	return event


## The motion a Control sees for a finger that moved to `at`.
static func motion(at: Vector2) -> InputEventMouseMotion:
	var event: InputEventMouseMotion = InputEventMouseMotion.new()
	event.device = InputEvent.DEVICE_ID_EMULATION
	event.position = at
	event.button_mask = MOUSE_BUTTON_MASK_LEFT
	return event


## Tags the local kid of `practice` (the sparring dummies never hit hard enough in a short test).
static func hurt(practice: PracticeMatch, amount: int) -> void:
	practice.sim.damage(practice.local_id, amount)
	practice._presentation.sync(0.0)
