class_name MenuCarousel
extends Control
## A horizontal carousel of 3:4 cards. The chosen card is in the middle at full size; every other
## card is stacked behind it, pushed only a little to its left or right, smaller and see-through but
## always visible. Drag or swipe sideways (or use the mouse wheel) to scroll, tap a card to bring it to
## the middle.

signal selected(index: int)

const CARD: Vector2 = ChoiceCard.CARD_SIZE
## Distance between the middles of two neighbouring cards: less than a card's width, so they overlap.
const STEP: float = 112.0
## Each step away from the middle shrinks a card by this much (and a card is never smaller than the
## one two steps away).
const SCALE_LOSS: float = 0.14
const SIDE_ALPHA: float = 0.6
const DRAG_START: float = 12.0
const SNAP_TIME: float = 0.18

var index: int = 0
var cards: Array[Button] = []

var _position: float = 0.0
var _pressed: bool = false
var _dragging: bool = false
var _press_x: float = 0.0
var _press_position: float = 0.0
var _tween: Tween


## `options` are {id, title, color}; the card at `current` starts in the middle. `heading` is the
## small word at the top left of every card.
func setup(heading: String, options: Array[Dictionary], current: int) -> void:
	for card: Button in cards:
		card.queue_free()
	cards.clear()
	for option: Dictionary in options:
		var card: Button = ChoiceCard.make_card(heading, String(option["title"]), String(option["color"]), CARD, String(option["id"]))
		card.size = CARD
		card.pivot_offset = CARD / 2.0
		card.mouse_filter = Control.MOUSE_FILTER_IGNORE
		add_child(card)
		cards.append(card)
	index = clampi(current, 0, maxi(cards.size() - 1, 0))
	_position = float(index)
	_layout()


func _ready() -> void:
	clip_contents = false
	mouse_filter = Control.MOUSE_FILTER_STOP
	_layout()


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		_layout()


## Brings card `at` to the middle. Emits `selected` when it is a different card.
func select(at: int, animate: bool = true) -> void:
	var target: int = clampi(at, 0, maxi(cards.size() - 1, 0))
	var changed: bool = target != index
	index = target
	if _tween != null:
		_tween.kill()
	if animate:
		_tween = create_tween()
		_tween.tween_method(_move_to, _position, float(target), SNAP_TIME).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	else:
		_move_to(float(target))
	if changed:
		selected.emit(index)


func _move_to(value: float) -> void:
	_position = value
	_layout()


## Places every card from the scroll position: the nearer the middle, the bigger, the clearer
## and the further in front.
func _layout() -> void:
	for i: int in cards.size():
		var offset: float = float(i) - _position
		var away: float = clampf(absf(offset), 0.0, 2.0)
		var card: Button = cards[i]
		card.scale = Vector2.ONE * (1.0 - SCALE_LOSS * away)
		card.position = Vector2(size.x / 2.0 + offset * STEP - CARD.x / 2.0, (size.y - CARD.y) / 2.0)
		# every card stays visible: the middle one is solid, the others a little see-through
		card.modulate.a = lerpf(1.0, SIDE_ALPHA, clampf(absf(offset), 0.0, 1.0))
		card.z_index = 10 - int(roundf(absf(offset) * 5.0))


func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		var button: InputEventMouseButton = event as InputEventMouseButton
		if button.button_index == MOUSE_BUTTON_WHEEL_UP and button.pressed:
			select(index - 1)
		elif button.button_index == MOUSE_BUTTON_WHEEL_DOWN and button.pressed:
			select(index + 1)
		elif button.button_index == MOUSE_BUTTON_LEFT:
			if button.pressed:
				_pressed = true
				_dragging = false
				_press_x = button.position.x
				_press_position = _position
				if _tween != null:
					_tween.kill()
			elif _pressed:
				_pressed = false
				if _dragging:
					select(roundi(_position))
				else:
					select(_card_at(button.position))
	elif event is InputEventMouseMotion and _pressed:
		var motion: InputEventMouseMotion = event as InputEventMouseMotion
		var travelled: float = motion.position.x - _press_x
		if absf(travelled) > DRAG_START:
			_dragging = true
		if _dragging:
			_position = clampf(_press_position - travelled / STEP, 0.0, float(maxi(cards.size() - 1, 0)))
			_layout()


## The card under `at` (the front-most one wins), or the current one when it hits none.
func _card_at(at: Vector2) -> int:
	var best: int = index
	var best_z: int = -999
	for i: int in cards.size():
		var card: Button = cards[i]
		var half: Vector2 = CARD * card.scale / 2.0
		var centre: Vector2 = card.position + CARD / 2.0
		if absf(at.x - centre.x) <= half.x and absf(at.y - centre.y) <= half.y and card.z_index > best_z:
			best = i
			best_z = card.z_index
	return best
