class_name TagToast
extends Control
## The tag toast (owner): a horizontal bar above the card tray that pops in and out when somebody is tagged, instead of text over the street:
## the tagger's face in a circle on the left, "TAYA!" in the middle, the tagged kid's face in a circle on the right (the kid's profile picture
## only, no names), each with the player's number on a rounded square in their team's colour at the circle's bottom corner (the tagger's at the
## bottom left, the tagged one's at the bottom right). The bar is blue when the tagger is an ally, red when an enemy. The same in Agawan Base, Patintero and Tumbang Preso. Tags can come close together, so every toast is on screen for exactly
## `SECONDS` and the next waits its turn (`MAX_WAITING` at most: the oldest waiting one is dropped past that). A sound plays as it pops in and
## as it pops out (the owner's own files, reused). `tick` moves it on (the match's frame, or a test's); View only.

## It started to pop in / it started to pop out (the match plays `SOUND_IN` and `SOUND_OUT`).
signal popped_in
signal popped_out

## How long each toast is on screen, from the first frame of the pop in to the last of the pop out.
const SECONDS: float = 2.0
const IN_SECONDS: float = 0.22
const OUT_SECONDS: float = 0.2
## How many may wait behind the one on screen.
const MAX_WAITING: int = 6
const WORD: String = "TAYA!"
const AVATAR: float = 76.0
const HEIGHT: float = 108.0
## Space between the bar and the tray under it.
const GAP: float = 10.0
## The pop: it grows from this share of its size and rises this far (px) as it comes in; it shrinks to `OUT_SCALE` as it goes.
const IN_SCALE: float = 0.6
const OUT_SCALE: float = 0.8
const RISE: float = 24.0
## The owner's own sound files, reused (docs/ART.md "Audio"): the sound of picking a card as it pops in, of putting one back as it goes.
const SOUND_IN: String = "card_pick"
const SOUND_OUT: String = "card_cancel"
const SPEED_IN: float = 1.25
const SPEED_OUT: float = 1.1
const RING: Color = Color(1.0, 1.0, 1.0, 0.9)
## The number badge on a circle: its side, and how far it sticks out past the circle's corner.
const BADGE: float = 36.0
const BADGE_OUT: float = 8.0
const BADGE_FONT: int = UiMetrics.MIN_TEXT  # a control's text is held to the reading floor (the lint test)
## The bar's colour by who tagged: the ally's blue, the enemy's red (the colour-blind pair with that setting), as deep as the night plate.
const ALLY: StringName = &"ally"
const ENEMY: StringName = &"enemy"
const DEEPEN: float = 0.5
const WORD_COLOR: Color = Color(1.0, 0.82, 0.25)

var panel: PanelContainer
var tagger_slot: Control
var tagged_slot: Control
var word_label: Label
var _row: HBoxContainer
var _queue: Array[Dictionary] = []
var _showing: bool = false
var _age: float = 0.0
var _out_started: bool = false


func _ready() -> void:
	name = "TagToast"
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	visible = false
	var center: CenterContainer = CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	center.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(center)
	panel = PanelContainer.new()
	panel.name = "Bar"
	panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	panel.add_theme_stylebox_override("panel", UiSkin.plate("night", 14.0, UiSkin.RADIUS_XL))
	center.add_child(panel)
	_row = HBoxContainer.new()
	_row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_row.add_theme_constant_override("separation", UiMetrics.GAP * 2)
	panel.add_child(_row)
	tagger_slot = _slot("Tagger")
	_row.add_child(tagger_slot)
	word_label = Label.new()
	word_label.name = "Word"
	word_label.text = WORD
	word_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	word_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	UiKit.letter(word_label, UiMetrics.HEADLINE, WORD_COLOR)
	_row.add_child(word_label)
	tagged_slot = _slot("Tagged")
	_row.add_child(tagged_slot)


## Somebody was tagged: `tagger` and `tagged` say who, each {character: the kid's id, number: the player's number, color: the team's colour};
## an empty one (nobody tagged it) leaves an empty circle. `tone` is `ALLY` or `ENEMY` for the bar's colour (anything else: the night plate).
func say(tagger: Dictionary, tagged: Dictionary, tone: StringName = &"") -> void:
	_queue.append({"tagger": tagger, "tagged": tagged, "tone": tone})
	while _queue.size() > MAX_WAITING:
		_queue.pop_front()


## Moves the toast on by `delta` seconds.
func tick(delta: float) -> void:
	if not _showing:
		if _queue.is_empty():
			return
		_start(_queue.pop_front())
	_age += delta
	if _age >= SECONDS - 0.0001:
		_finish()
		return
	_apply()


## The toast on screen now?
func is_showing() -> bool:
	return _showing


## How many are waiting for their turn.
func waiting() -> int:
	return _queue.size()


## How long the toast on screen has been there (s).
func age() -> float:
	return _age


## Drops everything (the match ended).
func clear() -> void:
	_queue.clear()
	_showing = false
	visible = false


func _start(item: Dictionary) -> void:
	_fill(tagger_slot, item["tagger"] as Dictionary, true)
	_fill(tagged_slot, item["tagged"] as Dictionary, false)
	panel.add_theme_stylebox_override("panel", _plate(item["tone"] as StringName))
	_showing = true
	_age = 0.0
	_out_started = false
	visible = true
	_apply()
	popped_in.emit()


func _finish() -> void:
	_showing = false
	visible = false
	if not _out_started:
		popped_out.emit()
	if not _queue.is_empty():
		_start(_queue.pop_front())  # the next one comes straight on; each still gets its full `SECONDS`


## The pop: scale, fade and rise while it comes in, still while it stays, shrink and fade as it goes. Reduce motion cuts: it is just there.
func _apply() -> void:
	var out_from: float = SECONDS - OUT_SECONDS
	if not _out_started and _age >= out_from:
		_out_started = true
		popped_out.emit()
	var scale_now: float = 1.0
	var alpha: float = 1.0
	var lift: float = 0.0
	if not Settings.reduced_motion:
		if _age < IN_SECONDS:
			var t: float = _age / IN_SECONDS
			var eased: float = 1.0 - pow(1.0 - t, 3.0)
			scale_now = lerpf(IN_SCALE, 1.0, eased)
			alpha = t
			lift = (1.0 - eased) * RISE
		elif _age >= out_from:
			var t: float = clampf((_age - out_from) / OUT_SECONDS, 0.0, 1.0)
			scale_now = lerpf(1.0, OUT_SCALE, t)
			alpha = 1.0 - t
	panel.pivot_offset = panel.size / 2.0
	panel.scale = Vector2.ONE * scale_now
	panel.position.y = lift
	panel.modulate.a = alpha


func _slot(slot_name: String) -> Control:
	var slot: Control = Control.new()
	slot.name = slot_name
	slot.custom_minimum_size = Vector2.ONE * AVATAR
	slot.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return slot


## The bar's plate: blue for an ally's tag, red for an enemy's, the night plate when there is no tagger.
func _plate(tone: StringName) -> StyleBox:
	if tone != ALLY and tone != ENEMY:
		return UiSkin.plate("night", 14.0, UiSkin.RADIUS_XL)
	var base: Color = TeamColors.ui_own() if tone == ALLY else TeamColors.ui_enemy()
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = base.darkened(DEEPEN)
	box.border_color = base.lightened(0.15)
	box.set_border_width_all(4)
	box.set_corner_radius_all(UiSkin.RADIUS_XL)
	box.set_content_margin_all(14.0)
	box.shadow_color = Color(0.0, 0.0, 0.0, 0.45)
	box.shadow_size = 8
	box.anti_aliasing = true
	return box


## The circle of a kid's face in `slot` (a dark empty circle when there is no kid) and the player's number on a team-coloured rounded square at
## its bottom left (`badge_left`: the tagger's) or bottom right (the tagged one's).
func _fill(slot: Control, side: Dictionary, badge_left: bool) -> void:
	for old: Node in slot.get_children():
		old.queue_free()
		slot.remove_child(old)
	var ring: Panel = Panel.new()
	ring.name = "Ring"
	ring.mouse_filter = Control.MOUSE_FILTER_IGNORE
	ring.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = Color(0.06, 0.1, 0.2)
	box.border_color = RING
	box.set_border_width_all(4)
	box.set_corner_radius_all(int(AVATAR / 2.0))
	box.anti_aliasing = true
	ring.add_theme_stylebox_override("panel", box)
	slot.add_child(ring)
	if side.is_empty():
		return
	var face: TextureRect = UiKit.avatar(String(side["character"]), AVATAR - 8.0)
	face.name = "Face"
	face.position = Vector2.ONE * 4.0
	slot.add_child(face)
	slot.add_child(_badge(side["number"] as int, side["color"] as Color, badge_left))


## The number badge: white on a rounded square in the team's colour, outlined, over the circle's bottom corner.
func _badge(number: int, color: Color, at_left: bool) -> Control:
	var badge: Panel = Panel.new()
	badge.name = "Badge"
	badge.mouse_filter = Control.MOUSE_FILTER_IGNORE
	badge.size = Vector2.ONE * BADGE
	badge.position = Vector2((-BADGE_OUT) if at_left else (AVATAR - BADGE + BADGE_OUT), AVATAR - BADGE + BADGE_OUT)
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = color
	box.border_color = Icons.OUTLINE
	box.set_border_width_all(3)
	box.set_corner_radius_all(UiSkin.RADIUS_S)
	box.anti_aliasing = true
	badge.add_theme_stylebox_override("panel", box)
	var label: Label = Label.new()
	label.name = "Number"
	label.text = str(number)
	label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	UiKit.letter(label, BADGE_FONT, Color.WHITE)
	badge.add_child(label)
	return badge
