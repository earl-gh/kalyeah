class_name UiMotion
extends RefCounted
## The interface's motion, built once for every screen: a button squashes while pressed and bounces back on release;
## panels and dialogs rise and scale in with a slight overshoot; a title drops
## in with a bounce; a number counts up; the one primary Play button bobs gently while it waits.
##
## Each is a short tween on the control's own scale, alpha or vertical offset: no per-frame allocation, no extra drawing
## pass. With Reduce motion every one is skipped and the control is simply at rest.

## Press: the button sinks to PRESS_SCALE quickly, then springs back past its size and settles (TRANS_BACK).
const PRESS_SCALE: float = 0.94
const PRESS_SECONDS: float = 0.06
const RELEASE_SECONDS: float = 0.24
## Enter: from ENTER_SCALE and transparent to full size, overshooting a little; a free panel also rises ENTER_RISE px.
const ENTER_SCALE: float = 0.9
const ENTER_SECONDS: float = 0.34
const ENTER_FADE: float = 0.2
const ENTER_RISE: float = 40.0
## A title falls DROP_HEIGHT px into place and bounces.
const DROP_HEIGHT: float = 90.0
const DROP_SECONDS: float = 0.55
## Idle bob: BOB_HEIGHT px up and down over BOB_SECONDS, after BOB_DELAY of rest.
const BOB_HEIGHT: float = 5.0
const BOB_SECONDS: float = 1.8
const BOB_DELAY: float = 1.2

const PRESS_META: StringName = &"press_tween"
const ENTRANCE_META: StringName = &"entrance_tween"
const BOB_META: StringName = &"bob_tween"
const OFFSET_META: StringName = &"motion_offset"
const REST_META: StringName = &"motion_rest"


## Gives `button` the press feel (once, however often it is asked).
static func press(button: BaseButton) -> void:
	if button.has_meta(&"press_motion"):
		return
	button.set_meta(&"press_motion", true)
	button.button_down.connect(_squash.bind(button))
	button.button_up.connect(_release.bind(button))


static func _squash(button: BaseButton) -> void:
	var tween: Tween = _restart(button, PRESS_META)
	if tween == null:
		return
	tween.tween_property(button, "scale", Vector2.ONE * PRESS_SCALE, PRESS_SECONDS).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)


static func _release(button: BaseButton) -> void:
	var tween: Tween = _restart(button, PRESS_META)
	if tween == null:
		return
	tween.tween_property(button, "scale", Vector2.ONE, RELEASE_SECONDS).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)


## A fresh tween for `control` under `meta` (the previous one stopped), its pivot at its centre; null (and the control at
## rest) with Reduce motion or out of the tree.
static func _restart(control: Control, meta: StringName) -> Tween:
	stop(control, meta)
	if Settings.reduced_motion or not control.is_inside_tree():
		control.scale = Vector2.ONE
		return null
	control.pivot_offset = control.size / 2.0
	var tween: Tween = control.create_tween()
	control.set_meta(meta, tween)
	return tween


## Stops `control`'s motion under `meta` and leaves it at rest.
static func stop(control: Control, meta: StringName) -> void:
	var previous: Tween = (control.get_meta(meta) as Tween) if control.has_meta(meta) else null
	if is_instance_valid(previous):
		previous.kill()
	control.remove_meta(meta)
	if meta == PRESS_META or meta == ENTRANCE_META:
		control.scale = Vector2.ONE
	if meta == ENTRANCE_META:
		control.modulate.a = 1.0
	if meta == BOB_META or meta == ENTRANCE_META:
		_offset(0.0, control)


## Brings a panel or a dialog in: it rises ENTER_RISE px into place and grows from ENTER_SCALE, both with a slight
## overshoot, as it fades in, after `delay`. Returns the tween (null with Reduce motion).
static func enter(control: Control, delay: float = 0.0) -> Tween:
	stop(control, ENTRANCE_META)
	if Settings.reduced_motion or not control.is_inside_tree():
		return null
	control.pivot_offset = control.size / 2.0
	control.scale = Vector2.ONE * ENTER_SCALE
	control.modulate.a = 0.0
	var tween: Tween = control.create_tween().set_parallel(true)
	control.set_meta(ENTRANCE_META, tween)
	tween.tween_property(control, "modulate:a", 1.0, ENTER_FADE).set_delay(delay)
	tween.tween_property(control, "scale", Vector2.ONE, ENTER_SECONDS).set_delay(delay).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	_offset(ENTER_RISE, control)
	tween.tween_method(_offset.bind(control), ENTER_RISE, 0.0, ENTER_SECONDS).set_delay(delay).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	return tween


## Drops a title into place from DROP_HEIGHT px above with a bounce, after `delay`. Returns the tween (null with Reduce
## motion).
static func drop_in(control: Control, delay: float = 0.0) -> Tween:
	stop(control, ENTRANCE_META)
	if Settings.reduced_motion or not control.is_inside_tree():
		return null
	_offset(-DROP_HEIGHT, control)
	control.modulate.a = 0.0
	var tween: Tween = control.create_tween().set_parallel(true)
	control.set_meta(ENTRANCE_META, tween)
	tween.tween_property(control, "modulate:a", 1.0, ENTER_FADE).set_delay(delay)
	tween.tween_method(_offset.bind(control), -DROP_HEIGHT, 0.0, DROP_SECONDS).set_delay(delay).set_trans(Tween.TRANS_BOUNCE).set_ease(Tween.EASE_OUT)
	return tween


## Counts `label` up from 0 to `to` over `seconds` after `delay` (straight to `to` with Reduce motion). `owner` runs the
## tween. Returns it (null with Reduce motion).
static func count_up(owner: Node, label: Label, to: int, seconds: float, delay: float = 0.0) -> Tween:
	if Settings.reduced_motion or not owner.is_inside_tree():
		label.text = str(to)
		return null
	label.text = "0"
	var tween: Tween = owner.create_tween()
	tween.tween_method(_count.bind(label, to), 0.0, 1.0, seconds).set_delay(delay)
	return tween


static func _count(progress: float, label: Label, to: int) -> void:
	label.text = str(roundi(to * progress))


## The one idle motion: `control` (the primary Play button, nothing else) bobs gently up and down after a short rest.
## Nothing happens with Reduce motion; `stop(control, BOB_META)` ends it.
static func idle_bob(control: Control) -> Tween:
	stop(control, BOB_META)
	if Settings.reduced_motion or not control.is_inside_tree():
		return null
	var tween: Tween = control.create_tween().set_loops()
	control.set_meta(BOB_META, tween)
	tween.tween_interval(BOB_DELAY)
	tween.tween_method(_bob.bind(control), 0.0, TAU, BOB_SECONDS)
	return tween


static func _bob(phase: float, control: Control) -> void:
	_offset(-BOB_HEIGHT * (0.5 - 0.5 * cos(phase)), control)


## Moves `control` `offset` px down from where its container (or its own layout) put it. The rest position is re-read
## whenever the container lays it out again, so the motion never fights the layout.
static func _offset(offset: float, control: Control) -> void:
	if not is_instance_valid(control):
		return
	var previous: float = control.get_meta(OFFSET_META, 0.0) as float
	if is_zero_approx(offset) and is_zero_approx(previous):
		return
	if not control.has_meta(REST_META):
		control.set_meta(REST_META, control.position.y - previous)
		var parent: Container = control.get_parent() as Container
		if parent != null and not parent.sort_children.is_connected(_on_sorted.bind(control)):
			parent.sort_children.connect(_on_sorted.bind(control))
			control.tree_exiting.connect(_forget.bind(control, parent), CONNECT_ONE_SHOT)
	control.set_meta(OFFSET_META, offset)
	control.position.y = (control.get_meta(REST_META) as float) + offset


static func _on_sorted(control: Control) -> void:
	control.set_meta(REST_META, control.position.y)
	control.position.y += control.get_meta(OFFSET_META, 0.0) as float


static func _forget(control: Control, parent: Container) -> void:
	if is_instance_valid(parent) and parent.sort_children.is_connected(_on_sorted.bind(control)):
		parent.sort_children.disconnect(_on_sorted.bind(control))
	control.remove_meta(REST_META)
	control.remove_meta(OFFSET_META)
