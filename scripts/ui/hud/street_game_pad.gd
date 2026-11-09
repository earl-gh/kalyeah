class_name StreetGamePad
extends Control
## The controls of Tumbang Preso, Patintero and Langit Lupa, floating over the bottom of the street (there is no tray and no hand: no skills in these
## games): the movement knob at the left, the action button at the right, and in between what your kid is to do. The action button is the
## round Throw button for a thrower with its slipper (press and hold to aim: the line on the street grows with the hold and a finger dragged
## anywhere turns it, fine while the line is short and fast when it is full; let go to throw; let go back on the button after dragging away
## to cancel), the Kick button for a thrower without it, the Dive button in Patintero and the Lupa button in Langit Lupa (a ring round
## each fills while it cools down), and nothing for the taya.
## It reports fingers only: `knob_moved` (the way the knob points, -1 to 1 each way), `aim_dragged` (the finger's move in pixels), `action_pressed`; the match
## screen turns them into a PlayerInput. Built in code; MatchHud feeds it with `show_state()`.

signal knob_moved(way: Vector2)
signal aim_started
## The finger moved by `drag` pixels while aiming (the match screen takes the part across the throw line).
signal aim_dragged(drag: Vector2)
## The finger let go of the Throw button; `cancel` when it came back onto the button after leaving it.
signal aim_released(cancel: bool)
## The Kick, Dive or Lupa button was pressed (each is one tap).
signal action_pressed

## What the button at the right is.
enum Action { NONE, THROW, KICK, DIVE, LUPA }

const INK: Color = RoyaleStyle.NAVY
const TITLE_COLOR: Color = RoyaleStyle.GOLD
const HINT_COLOR: Color = RoyaleStyle.CREAM
## The buttons' radius (the knob's base, the Throw and Kick buttons), the knob's thumb, and how far from the screen's side they stand.
const BUTTON_RADIUS: float = 66.0
const KNOB_RADIUS: float = 84.0
const THUMB_RADIUS: float = 38.0
const SIDE_MARGIN: float = 26.0
## A knob pushed less than this share of its radius does not move the kid.
const KNOB_DEADZONE: float = 0.12
const RING_WIDTH: float = 10.0
const BUTTON_FACE: Color = Color(1.0, 0.76, 0.2)
const BUTTON_PRESSED: Color = Color(1.0, 0.6, 0.12)
const KICK_FACE: Color = Color(0.35, 0.82, 0.95)
const DIVE_FACE: Color = Color(0.4, 0.9, 0.55)
const LUPA_FACE: Color = Color(0.78, 0.55, 0.3)
const BUTTON_OFF: Color = Color(0.45, 0.52, 0.64, 0.75)
const RING_TRACK: Color = Color(0.05, 0.2, 0.46, 0.8)
const RING_FILL: Color = Color(1.0, 1.0, 1.0)
const RING_FULL: Color = Color(0.45, 1.0, 0.55)
const KNOB_BASE: Color = Color(0.04, 0.12, 0.3, 0.45)
const KNOB_RIM: Color = Color(1.0, 1.0, 1.0, 0.55)
const THUMB: Color = Color(1.0, 1.0, 1.0, 0.85)
const NO_POINTER: int = -1

## What your kid is to do now, and one line on how.
var title_label: Label
var hint_label: Label
## The action button now, how full its ring is (0 to 1: the aim of a throw, or how far a dive has cooled down), and whether a Kick would
## reach the can or the dive is ready.
var action: Action = Action.NONE
var charge: float = 0.0
var act_ready: bool = false
## A tagged runner (on the sidewalk) has no action button, and its movement knob stands in the middle of the bottom instead of at the left.
var knob_centered: bool = false
## The landscape match (docs/audits/2026-11-09-landscape-hud.md): the pad fills the screen, the knob rests where the joystick does, the action
## button stands where the ability arc turns (the right thumb's home in every game), both in the HUD's style, and the words stand at the top
## centre under the score instead of over your kid. `safe` is the safe area's insets (left, top, right, bottom) and `words_top` where the
## words start (UI pixels).
var landscape: bool = false
var safe: Vector4 = Vector4.ZERO
var words_top: float = 0.0
## The landscape action button's radius: the arc's buttons plus a little, it is the only button there.
const WIDE_BUTTON_RADIUS: float = 56.0
## The finger holds the Throw button.
var aiming: bool = false
## The way the knob points now, and whether a finger holds it.
var knob_way: Vector2 = Vector2.ZERO
var _knob_pointer: int = NO_POINTER
## The finger has been off the Throw button since it pressed.
var _left: bool = false
var _action_pointer: int = NO_POINTER


## True while the Throw button shows (a thrower with its slipper in the throwing area).
var throw_shown: bool:
	get:
		return action == Action.THROW


var kick_shown: bool:
	get:
		return action == Action.KICK


func _ready() -> void:
	name = "StreetGamePad"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	visible = false
	title_label = _label("", UiMetrics.BODY, TITLE_COLOR)
	title_label.name = "Title"
	title_label.theme_type_variation = &"DisplayLabel"
	hint_label = _label("", UiMetrics.DETAIL, HINT_COLOR)
	hint_label.name = "Hint"
	for label: Label in [title_label, hint_label]:
		label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		add_child(label)
	resized.connect(_place)
	_place()


## What the pad shows now: the line and its hint, the action button (`fill`: how full its ring is; `can_act`: the Kick reaches the can, or
## the dive is ready).
func show_state(title: String, hint: String, button: Action, fill: float, can_act: bool, centered_knob: bool = false) -> void:
	if knob_centered != centered_knob:
		knob_centered = centered_knob
		_place()
	title_label.text = title
	hint_label.text = hint
	hint_label.visible = not hint.is_empty()
	charge = fill
	act_ready = can_act
	if action != button:
		if action == Action.THROW:
			_cancel_aim()
		action = button
	queue_redraw()


## The knob's middle and the action button's middle, in this control's own pixels.
func knob_center() -> Vector2:
	if landscape:
		var rest: Vector2 = MoveStick.rest_point(size, Vector2(safe.x, safe.w))
		return Vector2(size.x / 2.0, rest.y) if knob_centered else rest
	if knob_centered:
		return Vector2(size.x / 2.0, size.y * 0.5)
	return Vector2(SIDE_MARGIN + knob_radius(), size.y * 0.5)


func button_center() -> Vector2:
	if landscape:
		return AbilityArc.corner_point(size, Vector2(safe.z, safe.w))
	return Vector2(size.x - SIDE_MARGIN - button_radius(), size.y * 0.5)


## The action button's and the knob's radius now.
func button_radius() -> float:
	return WIDE_BUTTON_RADIUS if landscape else BUTTON_RADIUS


func knob_radius() -> float:
	return MoveStick.BASE_RADIUS if landscape else KNOB_RADIUS


## Switches the landscape layout on (sizes and places, the words' type), with the safe area's insets and where the words start.
func set_landscape(on: bool, insets: Vector4, words_at: float) -> void:
	landscape = on
	safe = insets
	words_top = words_at
	if on and title_label != null:
		HudStyle.letter(title_label, HudStyle.TITLE, TITLE_COLOR)
		HudStyle.letter(hint_label, HudStyle.LABEL, HINT_COLOR)
	_place()


## The action button's rectangle on the screen (tests and the match screen's hit test).
func button_rect() -> Rect2:
	var middle: Vector2 = get_global_transform() * button_center()
	return Rect2(middle - Vector2.ONE * button_radius(), Vector2.ONE * button_radius() * 2.0)


func knob_rect() -> Rect2:
	var middle: Vector2 = get_global_transform() * knob_center()
	return Rect2(middle - Vector2.ONE * knob_radius(), Vector2.ONE * knob_radius() * 2.0)


## Lets go of the knob and the Throw button without throwing (a menu opened, the controls were switched off).
func cancel() -> void:
	_set_knob(Vector2.ZERO)
	_knob_pointer = NO_POINTER
	_cancel_aim()
	queue_redraw()


func _cancel_aim() -> void:
	if _action_pointer == NO_POINTER:
		return
	_action_pointer = NO_POINTER
	if aiming:
		aiming = false
		aim_released.emit(true)


func _place() -> void:
	if title_label == null:
		return
	if landscape:
		var wide: float = minf(size.x * 0.6, 760.0)
		title_label.position = Vector2((size.x - wide) / 2.0, words_top)
		title_label.size = Vector2(wide, HudStyle.TITLE + 12.0)
		hint_label.position = Vector2((size.x - wide) / 2.0, words_top + HudStyle.TITLE + 10.0)
		hint_label.size = Vector2(wide, HudStyle.LABEL * 2.0 + 12.0)
		queue_redraw()
		return
	var left: float = SIDE_MARGIN * 2.0 + knob_radius() * 2.0
	var width: float = maxf(size.x - left * 2.0 + 20.0, 120.0)
	var middle: float = size.x / 2.0
	var top: float = size.y * 0.5 - 62.0
	if knob_centered:
		# the knob is in the way in the middle: the words stand above it
		width = maxf(size.x - SIDE_MARGIN * 4.0, 120.0)
		top = size.y * 0.5 - knob_radius() - 200.0
	title_label.position = Vector2(middle - width / 2.0, top)
	title_label.size = Vector2(width, 70.0)
	hint_label.position = Vector2(middle - width / 2.0, top + 72.0)
	hint_label.size = Vector2(width, 80.0)
	queue_redraw()


func _input(event: InputEvent) -> void:
	if not visible:
		return
	if event is InputEventScreenTouch:
		var touch: InputEventScreenTouch = event as InputEventScreenTouch
		if touch.pressed:
			_press(touch.index, touch.position)
		elif touch.index == _knob_pointer:
			_knob_pointer = NO_POINTER
			_set_knob(Vector2.ZERO)
			get_viewport().set_input_as_handled()
		elif touch.index == _action_pointer:
			_release_action(touch.position)
	elif event is InputEventScreenDrag:
		var drag: InputEventScreenDrag = event as InputEventScreenDrag
		if drag.index == _knob_pointer:
			_set_knob(_knob_vector(drag.position))
			get_viewport().set_input_as_handled()
		elif drag.index == _action_pointer:
			if not _on_button(drag.position):
				_left = true
			aim_dragged.emit(drag.relative)
			get_viewport().set_input_as_handled()


func _press(index: int, at: Vector2) -> void:
	if _knob_pointer == NO_POINTER and knob_rect().grow(knob_radius() * 0.3).has_point(at):
		_knob_pointer = index
		_set_knob(_knob_vector(at))
		get_viewport().set_input_as_handled()
	elif action != Action.NONE and _action_pointer == NO_POINTER and button_rect().grow(button_radius() * 0.25).has_point(at):
		_action_pointer = index
		_left = false
		if action == Action.THROW:
			aiming = true
			aim_started.emit()
		else:
			action_pressed.emit()
		get_viewport().set_input_as_handled()
		queue_redraw()


func _release_action(at: Vector2) -> void:
	var cancel_it: bool = _left and _on_button(at)
	_action_pointer = NO_POINTER
	if aiming:
		aiming = false
		aim_released.emit(cancel_it)
	get_viewport().set_input_as_handled()
	queue_redraw()


## The way a finger at screen point `at` pushes the knob: its distance from the middle over the radius, each way -1 to 1, up the screen -1.
func _knob_vector(at: Vector2) -> Vector2:
	var way: Vector2 = (at - get_global_transform() * knob_center()) / knob_radius()
	return way.limit_length(1.0) if way.length() > KNOB_DEADZONE else Vector2.ZERO


func _set_knob(way: Vector2) -> void:
	if way == knob_way:
		return
	knob_way = way
	knob_moved.emit(way)
	queue_redraw()


func _on_button(at: Vector2) -> bool:
	return at.distance_to(get_global_transform() * button_center()) <= button_radius()


func _draw() -> void:
	var middle: Vector2 = knob_center()
	if landscape:
		HudStyle.draw_white_disc(self, middle, knob_radius())
		draw_circle(middle + knob_way * (knob_radius() - MoveStick.THUMB_RADIUS * 0.5), MoveStick.THUMB_RADIUS, HudStyle.WHITE_THUMB)
	else:
		draw_circle(middle, knob_radius(), KNOB_BASE)
		draw_arc(middle, knob_radius(), 0.0, TAU, 48, KNOB_RIM, 4.0, true)
		draw_circle(middle + knob_way * (knob_radius() - THUMB_RADIUS * 0.5), THUMB_RADIUS, THUMB)
	match action:
		Action.THROW:
			_draw_button("AIM" if aiming else "THROW", BUTTON_PRESSED if aiming else BUTTON_FACE, true)
		Action.KICK:
			_draw_button("KICK", KICK_FACE if act_ready else BUTTON_OFF, false)
		Action.DIVE:
			_draw_button("DIVE", DIVE_FACE if act_ready else BUTTON_OFF, true)
		Action.LUPA:
			_draw_button("LUPA", LUPA_FACE if act_ready else BUTTON_OFF, true)


func _draw_button(words: String, face: Color, with_ring: bool) -> void:
	var center: Vector2 = button_center()
	if landscape:
		_draw_white_button(words, center, with_ring)
		return
	draw_circle(center + Vector2(0.0, 6.0), button_radius(), Color(0.03, 0.12, 0.3, 0.7))
	draw_circle(center, button_radius(), face)
	if with_ring:
		draw_arc(center, button_radius() + RING_WIDTH * 0.7, 0.0, TAU, 48, RING_TRACK, RING_WIDTH, true)
		if charge > 0.0:
			var color: Color = RING_FULL if charge >= 1.0 else RING_FILL
			draw_arc(center, button_radius() + RING_WIDTH * 0.7, -PI / 2.0, -PI / 2.0 + TAU * charge, 48, color, RING_WIDTH, true)
	var font: Font = get_theme_default_font()
	var size_px: int = HudStyle.TITLE if landscape else UiMetrics.BODY
	var width: float = font.get_string_size(words, HORIZONTAL_ALIGNMENT_LEFT, -1.0, size_px).x
	draw_string_outline(font, center + Vector2(-width / 2.0, size_px * 0.35), words, HORIZONTAL_ALIGNMENT_LEFT, -1.0, size_px, HudStyle.outline(size_px) if landscape else 6, INK)
	draw_string(font, center + Vector2(-width / 2.0, size_px * 0.35), words, HORIZONTAL_ALIGNMENT_LEFT, -1.0, size_px, Color.WHITE)


## How strong the landscape main button's word is drawn: full while it can act (and while a throw is aimed), `HudStyle.WHITE_OFF` while not
## ready. Its outline is never dimmed: it is the movement controls' rim.
func white_strength() -> float:
	return 1.0 if act_ready or action == Action.THROW else HudStyle.WHITE_OFF


## The landscape main button, white only (owner): the movement controls' disc and rim, its charge (the throw's aim, the dive's cool-down) as
## a faint white sweep inside it, and the white word on a soft shadow.
func _draw_white_button(words: String, center: Vector2, with_ring: bool) -> void:
	var strength: float = white_strength()
	HudStyle.draw_white_disc(self, center, button_radius())
	# a cooling dive or lupa fills up and is gone once ready (then it looks exactly like the joystick); a throw being aimed shows its charge
	if with_ring and charge > 0.0 and (charge < 1.0 or aiming):
		HudStyle.draw_white_sweep(self, center, button_radius(), charge)
	var font: Font = get_theme_default_font()
	var size_px: int = HudStyle.TITLE
	var at: Vector2 = center + Vector2(-font.get_string_size(words, HORIZONTAL_ALIGNMENT_LEFT, -1.0, size_px).x / 2.0, size_px * 0.35)
	draw_string(font, at + Vector2.ONE * HudStyle.WORD_SHADOW_DROP, words, HORIZONTAL_ALIGNMENT_LEFT, -1.0, size_px, HudStyle.WORD_SHADOW)
	draw_string(font, at, words, HORIZONTAL_ALIGNMENT_LEFT, -1.0, size_px, Color(1.0, 1.0, 1.0, strength))


func _label(text: String, font_size: int, color: Color) -> Label:
	var label: Label = Label.new()
	label.text = text
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", color)
	label.add_theme_color_override("font_outline_color", INK)
	label.add_theme_constant_override("outline_size", 6)
	return label
