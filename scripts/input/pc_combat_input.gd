class_name PcCombatInput
extends RefCounted
## Desktop keyboard + mouse 3C hero-action translator into `MatchInput`.
## Coexists cleanly with mobile touch controls (`MoveStick`, `LookTouch`, `AbilityArc`):
## ignores touch-emulated mouse events and writes `input.stick` only while WASD is active.

const WALK_SCALE: float = 0.45
const RUN_SLIDE_THRESHOLD: float = 0.62

var rmb_held: bool = false
var lmb_held: bool = false
var mouse_captured: bool = false
var cursor_unlocked: bool = false
var last_screen_way: Vector2 = Vector2.ZERO

var _wasd_active: bool = false
var _ctrl_down: bool = false
var _c_down: bool = false
var _alt_down: bool = false
var _held_key_slot: int = -1
var _held_key_code: Key = KEY_NONE
var _keys_down: Dictionary[int, bool] = {}


## Translates a single unhandled mouse/keyboard `event` into `MatchInput` actions and returns
## the pixel look drag for `ArenaCamera.look` (`Vector2.ZERO` when mouse-look is inactive).
func handle_event(event: InputEvent, input: MatchInput, aim_pt: Vector2, active: bool) -> Vector2:
	if event is InputEventKey:
		var key_ev: InputEventKey = event as InputEventKey
		var key_code: Key = key_ev.physical_keycode if key_ev.physical_keycode != KEY_NONE else key_ev.keycode
		if key_code == KEY_ESCAPE and key_ev.pressed and not key_ev.echo:
			cursor_unlocked = true
			_release_mouse_capture()
			if _held_key_slot >= 0:
				_held_key_slot = -1
				_held_key_code = KEY_NONE
				input.card_cancel()
			return Vector2.ZERO
	if not active:
		reset(input, aim_pt)
		return Vector2.ZERO

	if event is InputEventMouseButton:
		var mb: InputEventMouseButton = event as InputEventMouseButton
		if mb.device == InputEvent.DEVICE_ID_EMULATION:
			return Vector2.ZERO
		if mb.button_index == MOUSE_BUTTON_RIGHT:
			if mb.pressed:
				cursor_unlocked = false
			rmb_held = mb.pressed
			_sync_mouse_mode()
			return Vector2.ZERO
		if mb.button_index == MOUSE_BUTTON_LEFT:
			if mb.pressed:
				cursor_unlocked = false
			lmb_held = mb.pressed
			input.set_primary(mb.pressed, aim_pt)
			return Vector2.ZERO
		return Vector2.ZERO

	if event is InputEventMouseMotion:
		var mm: InputEventMouseMotion = event as InputEventMouseMotion
		if mm.device == InputEvent.DEVICE_ID_EMULATION:
			return Vector2.ZERO
		if (not cursor_unlocked or rmb_held or mouse_captured) and mm.relative != Vector2.ZERO:
			return mm.relative
		return Vector2.ZERO

	if event is InputEventKey:
		var key: InputEventKey = event as InputEventKey
		var code: Key = key.physical_keycode if key.physical_keycode != KEY_NONE else key.keycode
		if code == KEY_ALT:
			if not key.echo:
				_alt_down = key.pressed
			return Vector2.ZERO
		if _is_wasd_code(code):
			if not key.echo:
				_keys_down[int(code)] = key.pressed
				if key.pressed and key.alt_pressed:
					_alt_down = true
			return Vector2.ZERO
		if code == KEY_CTRL or code == KEY_C:
			if key.echo:
				return Vector2.ZERO
			if code == KEY_CTRL:
				_ctrl_down = key.pressed
			else:
				_c_down = key.pressed
			var crouch_now: bool = _ctrl_down or _c_down
			if key.pressed and _is_running_for_slide(input):
				input.press_slide()
			input.set_crouch(crouch_now)
			return Vector2.ZERO
		if key.pressed and key.echo:
			return Vector2.ZERO
		if code == KEY_TAB and key.pressed:
			mouse_captured = not mouse_captured
			cursor_unlocked = not mouse_captured and not rmb_held
			_sync_mouse_mode()
			return Vector2.ZERO
		if code == KEY_SPACE and key.pressed:
			input.press_jump()
			return Vector2.ZERO
		if code == KEY_SHIFT and key.pressed:
			input.press_dash()
			return Vector2.ZERO
		if (code == KEY_F or code == KEY_4 or code == KEY_KP_4) and key.pressed:
			input.press_ultimate(aim_pt)
			return Vector2.ZERO
		var slot: int = _slot_for_key(code)
		if slot >= 0:
			if key.pressed:
				if input.card_press(slot, Time.get_ticks_msec()):
					input.card_drag(false, aim_pt, true)
					_held_key_slot = slot
					_held_key_code = code
			elif _held_key_slot == slot and (_held_key_code == code or _held_key_code == KEY_NONE):
				input.card_drag(false, aim_pt, true)
				input.card_release(Time.get_ticks_msec())
				_held_key_slot = -1
				_held_key_code = KEY_NONE
			return Vector2.ZERO

	return Vector2.ZERO


## Polls WASD movement into `input.stick` (without overwriting an active touch `MoveStick`,
## scaling to `WALK_SCALE = 0.45` when `Alt` is held) and keeps held primary / superpower
## aim points locked to the live crosshair `aim_pt`.
func poll_frame(input: MatchInput, ground_way_fn: Callable, aim_pt: Vector2, active: bool) -> void:
	if not active:
		reset(input, aim_pt)
		return

	var screen_way: Vector2 = _read_wasd()
	last_screen_way = screen_way
	if screen_way != Vector2.ZERO:
		_wasd_active = true
		var converted: Variant = ground_way_fn.call(screen_way) if ground_way_fn.is_valid() else screen_way
		if converted is Vector2:
			input.stick = converted as Vector2
	elif _wasd_active:
		_wasd_active = false
		input.stick = Vector2.ZERO

	if input.primary_held:
		input.set_primary(true, aim_pt)

	if _held_key_slot >= 0 and input.held_slot != _held_key_slot:
		_held_key_slot = -1
		_held_key_code = KEY_NONE
	if input.card_held() and (input.over_street or _held_key_slot == input.held_slot):
		input.card_drag(false, aim_pt, true)


## Releases any held PC inputs and restores mouse cursor visibility.
func reset(input: MatchInput = null, aim_pt: Vector2 = Vector2.ZERO) -> void:
	if lmb_held and input != null:
		input.set_primary(false, aim_pt)
	lmb_held = false
	if (_ctrl_down or _c_down) and input != null:
		input.set_crouch(false)
	_ctrl_down = false
	_c_down = false
	_alt_down = false
	if _held_key_slot >= 0 and input != null:
		input.card_cancel()
	_held_key_slot = -1
	_held_key_code = KEY_NONE
	if _wasd_active and input != null:
		input.stick = Vector2.ZERO
	_wasd_active = false
	last_screen_way = Vector2.ZERO
	cursor_unlocked = false
	_keys_down.clear()
	_release_mouse_capture()


func _read_wasd() -> Vector2:
	var x: float = 0.0
	var y: float = 0.0
	if _is_move_key_down(KEY_A):
		x -= 1.0
	if _is_move_key_down(KEY_D):
		x += 1.0
	if _is_move_key_down(KEY_W):
		y -= 1.0
	if _is_move_key_down(KEY_S):
		y += 1.0
	var way: Vector2 = Vector2(x, y)
	if way == Vector2.ZERO:
		return Vector2.ZERO
	var unit: Vector2 = way.normalized() if way.length_squared() > 1.0 else way
	return unit * WALK_SCALE if _is_alt_walk_down() else unit


func _is_running_for_slide(input: MatchInput) -> bool:
	if _is_alt_walk_down():
		return false
	var wasd: Vector2 = _read_wasd()
	if wasd.length() >= RUN_SLIDE_THRESHOLD:
		return true
	return input.stick.length() >= RUN_SLIDE_THRESHOLD


func _is_alt_walk_down() -> bool:
	if _alt_down:
		return true
	return Input.is_physical_key_pressed(KEY_ALT) or Input.is_key_pressed(KEY_ALT)


func _is_move_key_down(code: Key) -> bool:
	var key_id: int = int(code)
	if _keys_down.has(key_id) and _keys_down[key_id]:
		return true
	return Input.is_physical_key_pressed(code) or Input.is_key_pressed(code)


static func _is_wasd_code(code: Key) -> bool:
	return code == KEY_W or code == KEY_A or code == KEY_S or code == KEY_D


static func _slot_for_key(code: Key) -> int:
	match code:
		KEY_Q, KEY_1, KEY_KP_1:
			return 0
		KEY_E, KEY_2, KEY_KP_2:
			return 1
		KEY_R, KEY_3, KEY_KP_3:
			return 2
	return -1


func _release_mouse_capture() -> void:
	rmb_held = false
	mouse_captured = false
	_sync_mouse_mode()


func _sync_mouse_mode() -> void:
	if OS.has_feature("android") or DisplayServer.get_name() == "headless":
		return
	var target_mode: Input.MouseMode = Input.MOUSE_MODE_CAPTURED if (rmb_held or mouse_captured) else Input.MOUSE_MODE_VISIBLE
	if Input.mouse_mode != target_mode:
		Input.mouse_mode = target_mode
