class_name MenuCarousel
extends Control
## A horizontal carousel of 2.5:4 cards. The chosen card is in the middle at full size; every other
## card is stacked behind it, pushed only a little to its left or right, smaller and see-through but
## always visible. Drag or swipe sideways to scroll, tap a card to bring it to the middle.

signal selected(index: int)

const CARD: Vector2 = ChoiceCard.CARD_SIZE
## Distance between the middles of two neighbouring cards: less than a card's width, so they overlap.
const STEP: float = 98.0
## Each step away from the middle shrinks a card by this much (and a card is never smaller than the
## one two steps away).
const SCALE_LOSS: float = 0.17
## Cards further back are darker and sit higher, so the stack reads as depth.
const SHADE_LOSS: float = 0.36
const RISE: float = 30.0
## ... and slightly see-through too.
const BACK_ALPHA: float = 0.86
const DRAG_START: float = 12.0
const SNAP_TIME: float = 0.18

## The size of a card (2.5:4 on the main screen; the room's map carousel sets 3:4 before `setup`).
var card_size: Vector2 = CARD
## How far (px) the middle card bobs up and down while the screen idles (0 = holds still).
var bob: float = 0.0:
	set(value):
		bob = value
		set_process(value > 0.0)
var index: int = 0
var cards: Array[Button] = []

var _position: float = 0.0
var _pressed: bool = false
var _dragging: bool = false
var _press_x: float = 0.0
var _press_position: float = 0.0
var _tween: Tween


## `options` are {id, title, color}; the card at `current` starts in the middle. `heading` is the
## 
func setup(heading: String, options: Array[Dictionary], current: int) -> void:
	for card: Button in cards:
		card.queue_free()
	cards.clear()
	for option: Dictionary in options:
		var card: Button = ChoiceCard.make_card(String(option.get("heading", heading)), String(option["title"]), String(option["color"]), card_size, String(option["id"]))
		card.accessibility_name = String(option["title"])
		card.size = card_size
		card.pivot_offset = card_size / 2.0
		card.mouse_filter = Control.MOUSE_FILTER_IGNORE
		card.focus_mode = Control.FOCUS_NONE
		card.add_child(_frame(String(option.get("accent", "ffd25a"))))
		add_child(card)
		cards.append(card)
	if accessibility_name.is_empty():
		accessibility_name = heading if not heading.is_empty() else "Choices"
	accessibility_description = "Swipe or use the arrow keys to change"
	index = clampi(current, 0, maxi(cards.size() - 1, 0))
	_position = float(index)
	_layout()


## The bright rim of the card in the middle: a glowing frame in the card's accent colour.
func _frame(accent: String) -> Panel:
	var frame: Panel = Panel.new()
	frame.name = "Frame"
	frame.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	frame.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var ring: StyleBoxFlat = StyleBoxFlat.new()
	ring.draw_center = false
	ring.set_corner_radius_all(KalyeahTheme.CORNER)
	ring.set_border_width_all(5)
	ring.border_color = Color(accent)
	ring.shadow_color = Color(Color(accent), 0.55)
	ring.shadow_size = 18
	ring.anti_aliasing = true
	frame.add_theme_stylebox_override("panel", ring)
	return frame


func _ready() -> void:
	Settings.accessibility_changed.connect(_on_accessibility_changed)
	clip_contents = true
	mouse_filter = Control.MOUSE_FILTER_STOP
	_layout()


func _process(_delta: float) -> void:
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
	if animate and not Settings.reduced_motion:
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
		var drift: float = sin(float(Time.get_ticks_msec()) / 1000.0 * 1.7) * bob * (1.0 - away / 2.0) if bob > 0.0 and not Settings.reduced_motion else 0.0
		card.position = Vector2(size.x / 2.0 + offset * STEP - card_size.x / 2.0, (size.y - card_size.y) / 2.0 - RISE * away + drift)
		# every card stays visible: the ones further back are darker and slightly see-through
		var shade: float = 1.0 - SHADE_LOSS * away
		card.modulate = Color(shade, shade, shade, lerpf(1.0, BACK_ALPHA, clampf(absf(offset), 0.0, 1.0)))
		card.z_index = 10 - int(roundf(absf(offset) * 5.0))
		var frame: Control = card.get_node_or_null("Frame") as Control
		if frame != null:
			frame.modulate.a = clampf(1.0 - absf(offset) * 1.6, 0.0, 1.0)


func _on_accessibility_changed() -> void:
	if Settings.reduced_motion:
		_pressed = false
		_dragging = false
		select(index, false)


func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		var button: InputEventMouseButton = event as InputEventMouseButton
		if button.button_index == MOUSE_BUTTON_LEFT:
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
		var half: Vector2 = card_size * card.scale / 2.0
		var centre: Vector2 = card.position + card_size / 2.0
		if absf(at.x - centre.x) <= half.x and absf(at.y - centre.y) <= half.y and card.z_index > best_z:
			best = i
			best_z = card.z_index
	return best
