class_name LocalInput
extends RefCounted
## Reads the keyboard for PlayerInput (desktop testing): WASD / arrows walk, 1-4 hold a card, Esc drops it.


## WASD / arrow keys. Up is -1 on Y (screen up = toward the enemy base).
static func keyboard_vector() -> Vector2:
	var x: float = 0.0
	var y: float = 0.0
	if Input.is_physical_key_pressed(KEY_A) or Input.is_physical_key_pressed(KEY_LEFT):
		x -= 1.0
	if Input.is_physical_key_pressed(KEY_D) or Input.is_physical_key_pressed(KEY_RIGHT):
		x += 1.0
	if Input.is_physical_key_pressed(KEY_W) or Input.is_physical_key_pressed(KEY_UP):
		y -= 1.0
	if Input.is_physical_key_pressed(KEY_S) or Input.is_physical_key_pressed(KEY_DOWN):
		y += 1.0
	return Vector2(x, y).limit_length(1.0)


## Keys 1-4 = hand slots 1-4 (held to aim at the mouse, released to cast).
static func card_key_held(slot: int) -> bool:
	var keys: Array[Key] = [KEY_1, KEY_2, KEY_3, KEY_4]
	return slot >= 0 and slot < keys.size() and Input.is_physical_key_pressed(keys[slot])


## Escape while a number key holds a card drops it (the right button cancels through the hand, see CardHand).
static func cancel_held() -> bool:
	return Input.is_physical_key_pressed(KEY_ESCAPE)


## Screen-relative keys to world movement: screen up is where the camera looks.
## The camera is turned `yaw_degrees` (behind-right view), and the other team's view
## is turned another 180 degrees, so the stick is turned the same way.
static func to_world(stick: Vector2, flip: bool, yaw_degrees: float = 0.0) -> Vector2:
	var yaw: float = deg_to_rad(yaw_degrees + (180.0 if flip else 0.0))
	return Vector2(stick.x * cos(yaw) + stick.y * sin(yaw), -stick.x * sin(yaw) + stick.y * cos(yaw))


## The inverse of `to_world`: the screen direction that moves along `world`.
static func to_screen(world: Vector2, flip: bool, yaw_degrees: float = 0.0) -> Vector2:
	var yaw: float = deg_to_rad(yaw_degrees + (180.0 if flip else 0.0))
	return Vector2(world.x * cos(yaw) - world.y * sin(yaw), world.x * sin(yaw) + world.y * cos(yaw))
