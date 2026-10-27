class_name MenuCarousel
extends Control
## A horizontal carousel of cards (1.75:2.5 on the main screen). The chosen card is in the middle at full size; every other
## card is stacked behind it, pushed only a little to its left or right, smaller and see-through but
## always visible. Drag or swipe sideways to scroll, tap a card to bring it to the middle.

signal selected(index: int)
## Emitted after every layout (the cards moved a little, or a lot when scrolling): whatever follows the cards can follow now.
signal laid_out

const CARD: Vector2 = ChoiceCard.CARD_SIZE
## Distance between the middles of two neighbouring cards: less than a card's width, so they overlap.
const STEP: float = 98.0
## A card in the back layer is this much smaller than the chosen one (there are two layers only: the chosen card and the rest).
const SCALE_LOSS: float = 0.17
## How far the art starts under the top of a mode model's card, as a share of its height (the PNGs keep a 6 px margin of 717).
const FRONT_ART_INSET: float = 6.0 / 717.0
## Cards further back are darker (and smaller), so the stack reads as depth; they all share the middle card's vertical centre.
const SHADE_LOSS: float = 0.36
## ... and slightly see-through too.
const BACK_ALPHA: float = 0.86
## How fast the middle card drifts (radians a second: one slow breath takes about six seconds).
const FLOAT_SPEED: float = 1.0
const DRAG_START: float = 12.0
const SNAP_TIME: float = 0.18
## A step of an endless carousel is slower and smoother, and a swipe faster than `FLING_SPEED` (px per ms) or longer than
## `STEP_DRAG` (of a step) makes one.
const WRAP_TIME: float = 0.36
const FLING_SPEED: float = 0.45
const STEP_DRAG: float = 0.12
## The cards behind the chosen one shift this share less for the tilt parallax.
const TILT_BACK_SHARE: float = 0.6

## The size of a card (1.75:2.5 on the main screen; the room's map carousel sets 3:4 before `setup`).
var card_size: Vector2 = CARD
## The cards behind the chosen one stand higher than it by this share of a card's height (the mode models on the main screen do, so the
## layer behind shows over the chosen one); 0 leaves them on one line.
var back_rise: float = 0.0
## How much smaller the cards behind are than the chosen one (0.17 = 83 % of its size; the mode models are 75 %).
var scale_loss: float = SCALE_LOSS
## An endless carousel (the mode models): the last model is left of the first and the first right of the last, so there is always one
## behind on each side of the chosen one; a swipe, however long or fast, moves exactly one step.
var wrap: bool = false
## How far the cards behind sit out to each side, as a multiple of `STEP` (the mode models sit further out).
var spread_scale: float = 1.0
## The Home parallax (`HomeParallax`): the chosen card shifts this many px, the ones behind it less (they are further away).
var tilt: Vector2 = Vector2.ZERO:
	set(value):
		tilt = value
		if not is_processing():
			_layout()
## How far (px) the middle card bobs up and down while the screen idles (0 = holds still).
var bob: float = 0.0:
	set(value):
		bob = value
		set_process(value > 0.0)
## When 0 or more, the cards sit this many pixels above the bottom of the carousel instead of in the middle of it (the main
## screen keeps the mode name, its phrase and the buttons close under the cards).
var bottom_gap: float = -1.0
## Whether the chosen card gets a thin glowing coloured outline (a `CardRim`).
var show_rim: bool = true
var index: int = 0
var cards: Array[Button] = []

var _position: float = 0.0
var _pressed: bool = false
var _dragging: bool = false
var _press_x: float = 0.0
var _press_position: float = 0.0
var _tween: Tween
var _last_x: float = 0.0
var _last_msec: int = 0
var _velocity: float = 0.0


## `options` are {id, title, color}; the card at `current` starts in the middle. `heading` is the
## 
func setup(heading: String, options: Array[Dictionary], current: int) -> void:
	for card: Button in cards:
		card.queue_free()
	cards.clear()
	for option: Dictionary in options:
		var card: Button = ChoiceCard.make_card(String(option.get("heading", heading)), String(option["title"]), String(option["color"]), card_size, String(option["id"]), bool(option.get("bare", false)))
		card.accessibility_name = String(option["title"])
		card.size = card_size
		card.pivot_offset = card_size / 2.0
		card.mouse_filter = Control.MOUSE_FILTER_IGNORE
		card.focus_mode = Control.FOCUS_NONE
		var face: CardFace = card.get_node_or_null("Face") as CardFace
		# a card with a picture has no glow (owner); a flat card gets a `CardRim` over it
		if show_rim and face == null:
			card.add_child(_frame(String(option.get("accent", "ffd25a"))))
		if face != null:
			face.power = int(option.get("power", 0))
			face.wear_seed = float(cards.size() + 1)
			if option.has("plate"):
				face.set_plate(String(option["title"]), option["plate"] as Rect2)

		add_child(card)
		cards.append(card)
	if accessibility_name.is_empty():
		accessibility_name = heading if not heading.is_empty() else "Choices"
	accessibility_description = "Swipe or use the arrow keys to change"
	index = clampi(current, 0, maxi(cards.size() - 1, 0))
	_position = float(index)
	_layout()


## The bright rim of the card in the middle: a glowing frame in the card's accent colour.
func _frame(accent: String) -> CardRim:
	var rim: CardRim = CardRim.new()
	rim.accent = Color(accent)
	return rim


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


## Brings card `at` to the middle. Emits `selected` when it is a different card. An endless carousel takes the shorter way round.
func select(at: int, animate: bool = true) -> void:
	var count: int = maxi(cards.size(), 1)
	var target: int = posmod(at, count) if wrap else clampi(at, 0, maxi(cards.size() - 1, 0))
	var changed: bool = target != index
	var goal: float = float(target)
	if wrap:
		# the step from where the models are now (nearest whole position) to `target`, the short way
		var from: int = roundi(_position)
		goal = float(from + posmod(target - posmod(from, count) + count / 2, count) - count / 2)
	index = target
	if _tween != null:
		_tween.kill()
	if animate and not Settings.reduced_motion:
		_tween = create_tween()
		_tween.tween_method(_move_to, _position, goal, WRAP_TIME if wrap else SNAP_TIME).set_trans(Tween.TRANS_CUBIC if not wrap else Tween.TRANS_SINE).set_ease(Tween.EASE_OUT if not wrap else Tween.EASE_IN_OUT)
		if wrap:
			_tween.tween_callback(_settle)
	else:
		_move_to(goal)
		if wrap:
			_settle()
	if changed:
		selected.emit(index)


## After a step of an endless carousel the position is made the plain index again (the layout looks the same, the number stays small).
func _settle() -> void:
	_position = float(index)
	_layout()


func _move_to(value: float) -> void:
	_position = value
	_layout()


## Places every card from the scroll position: the nearer the middle, the bigger, the clearer
## and the further in front.
func _layout() -> void:
	for i: int in cards.size():
		var offset: float = float(i) - _position
		if wrap:
			var half: float = float(cards.size()) / 2.0
			offset = fposmod(offset + half, float(cards.size())) - half
		# two layers only (owner): the chosen card in front and every other card in one layer behind it. A third card (when
		# the chosen one is at an end) slides in behind the chosen card, centred, where it is not seen.
		var distance: float = absf(offset)
		var away: float = minf(distance, 1.0)
		var spread: float = distance if distance <= 1.0 else maxf(2.0 - distance, 0.0)
		var card: Button = cards[i]
		card.scale = Vector2.ONE * (1.0 - scale_loss * away)
		var drift_phase: float = sin(float(Time.get_ticks_msec()) / 1000.0 * FLOAT_SPEED)
		var drift: float = drift_phase * bob * (1.0 - away / 2.0) if bob > 0.0 and not Settings.reduced_motion else 0.0
		card.position = Vector2(size.x / 2.0 + signf(offset) * spread * STEP * spread_scale - card_size.x / 2.0, ((size.y - card_size.y) / 2.0 if bottom_gap < 0.0 else size.y - card_size.y - bottom_gap) + drift - card_size.y * back_rise * away) + tilt * (1.0 - TILT_BACK_SHARE * away)
		# every card stays visible: the ones further back are darker and slightly see-through
		var shade: float = 1.0 - SHADE_LOSS * away
		card.modulate = Color(shade, shade, shade, lerpf(1.0, BACK_ALPHA, clampf(absf(offset), 0.0, 1.0)))
		card.z_index = 10 - int(roundf(away * 5.0)) - int(roundf(clampf(distance - 1.0, 0.0, 1.0) * 3.0))
		_float(card, away, drift_phase)
		var frame: Control = card.get_node_or_null("Frame") as Control
		if frame != null:
			frame.modulate.a = clampf(1.0 - absf(offset) * 1.6, 0.0, 1.0)
	laid_out.emit()


## Floats a card with a picture: while the screen idles the middle card's shadow breathes with its drift.
func _float(card: Button, away: float, phase: float) -> void:
	var face: CardFace = card.get_node_or_null("Face") as CardFace
	if face == null:
		return
	var lift: float = 0.0
	if bob > 0.0 and not Settings.reduced_motion:
		lift = -phase * clampf(1.0 - away, 0.0, 1.0)
	face.lift = lift
	# only the chosen card has a shadow (and a glow): every other card has neither
	face.chosen = clampf(1.0 - away * 1.6, 0.0, 1.0)


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
				_last_x = button.position.x
				_last_msec = Time.get_ticks_msec()
				_velocity = 0.0
				if _tween != null:
					_tween.kill()
			elif _pressed:
				_pressed = false
				if _dragging and wrap:
					select(_swipe_target())
				elif _dragging:
					select(roundi(_position))
				else:
					select(_card_at(button.position))
	elif event is InputEventMouseMotion and _pressed:
		var motion: InputEventMouseMotion = event as InputEventMouseMotion
		var travelled: float = motion.position.x - _press_x
		if absf(travelled) > DRAG_START:
			_dragging = true
		var now: int = Time.get_ticks_msec()
		if now > _last_msec:
			_velocity = lerpf(_velocity, (motion.position.x - _last_x) / float(now - _last_msec), 0.5)
		_last_x = motion.position.x
		_last_msec = now
		if _dragging and wrap:
			# the models follow the finger, but never further than one step from where they were
			var start: float = float(roundi(_press_position))
			_position = clampf(_press_position - travelled / (STEP * spread_scale), start - 1.0, start + 1.0)
			_layout()
		elif _dragging:
			_position = clampf(_press_position - travelled / STEP, 0.0, float(maxi(cards.size() - 1, 0)))
			_layout()


## Where an endless carousel's swipe ends: one step in the direction it went when it went far enough or fast enough, else back where
## it started (never more than one step).
func _swipe_target() -> int:
	var start: int = roundi(_press_position)
	var moved: float = _position - float(start)
	var fast: bool = absf(_velocity) > FLING_SPEED
	if absf(moved) < STEP_DRAG and not fast:
		return posmod(start, maxi(cards.size(), 1))
	var way: int = signi(roundi(moved * 1000.0)) if absf(moved) >= 0.02 else -signi(roundi(_velocity * 1000.0))
	return posmod(start + way, maxi(cards.size(), 1))


## The top of the models behind the chosen one, in this control's pixels: the highest point of the carousel's back layer (what the name tag
## above it must stay clear of). Valid once `card_size`, `back_rise`, `scale_loss` and `bottom_gap` are set.
func back_top() -> float:
	var base: float = (size.y - card_size.y) / 2.0 if bottom_gap < 0.0 else size.y - card_size.y - bottom_gap
	return base - card_size.y * back_rise + card_size.y * scale_loss / 2.0


## The top of the chosen model's picture, in this control's pixels (at rest, without its drift): where the picture's art starts, a hair
## under the top of its card (the pictures keep a 6 px margin of 717). What the name tag above it is centred against.
func front_top() -> float:
	var base: float = (size.y - card_size.y) / 2.0 if bottom_gap < 0.0 else size.y - card_size.y - bottom_gap
	return base + card_size.y * FRONT_ART_INSET


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
