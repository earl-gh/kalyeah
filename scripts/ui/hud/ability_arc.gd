class_name AbilityArc
extends CardHand
## The hand of the landscape match (owner, 2026-11-09: Wild Rift style; docs/GDD.md "Landscape layout"), under the right thumb: the four
## cards are round buttons in an arc round the bottom-right corner, each showing the middle of its card picture (or the skill icon on the
## kind colour), with its cost on an ice water drop at its foot. NEXT is the small round button in the corner the arc turns round, and the
## ice water bar lies along the bottom to the arc's left. There is no basic attack (docs/GDD.md), so nothing else is under the thumb.
##
## Aiming is Wild Rift's: press a button and drag away from it; the way and the length of the drag (up to `AIM_RADIUS`) are the way and the
## share of the card's range from your kid (`aim_way`, turned into a street point by the match screen). Let go to cast there; a quick tap
## without dragging casts at once (the sim aims, as on the old hand); let go on the cancel button that shows above the arc while aiming to
## put the card back. The same signals as CardHand, so the match screen and MatchInput are unchanged; it never decides a rule.

## The buttons' radius, NEXT's, the arc's radius round the corner point, the angles (degrees, screen: 180 = left, 270 = up) of the four
## buttons. The arc's corner point stands so the outermost buttons keep `HudStyle.EDGE` from the safe area's right and bottom edges (`corner_point`).
const BUTTON_RADIUS: float = 48.0
const NEXT_RADIUS: float = 30.0
const ARC_RADIUS: float = 160.0
const ANGLES: Array[float] = [175.0, 215.0, 255.0, 295.0]
## A drag this long (pixels) from the button is the card's whole range; less than `AIM_DEADZONE` is still a tap.
const AIM_RADIUS: float = 160.0
const AIM_DEADZONE: float = 22.0
## The cancel button above the bar while aiming, and its gap to the bar.
const CANCEL_RADIUS: float = 40.0
const CANCEL_GAP: float = 18.0
## The ice water bar: slim, over the arc (the right thumb's cluster, off your kid), from the leftmost button to the screen's edge, this far
## above the highest button, with the whole points on a drop of `SLIM_DROP` px at its left end.
const SLIM_BAR: float = 14.0
const SLIM_DROP: float = 34.0
const SLIM_BAR_GAP: float = 16.0
const BAR_TRACK: Color = Color(0.035, 0.1, 0.27, 0.6)
const BAR_DIVIDER: Color = Color(0.035, 0.1, 0.27, 0.7)
## The share of a card picture's width the round button shows (its middle: the art, without the torn frame).
const ART_SHARE: float = 0.78
## A ready card's rim is the HUD's cream, the held one gold; the cancel button is a control with a red rim, filled red under the finger.
const RIM: Color = Color(0.93, 0.95, 1.0, 0.75)
const RIM_HELD: Color = RoyaleStyle.GOLD
const COOL: Color = Color(0.02, 0.04, 0.1, 0.62)
const CANCEL_RIM: Color = Color(1.0, 0.35, 0.3, 0.9)
const CANCEL_HOT: Color = Color(0.85, 0.15, 0.12, 0.85)
const CIRCLE_STEPS: int = 40

## The safe area's right and bottom insets (UI pixels): the arc keeps clear of a notch or the home bar.
var inset: Vector2 = Vector2.ZERO
## While a card is held: the way and share of its range the drag points (screen: x right, y down; at most 1 long; zero inside the deadzone).
var aim_way: Vector2 = Vector2.ZERO
## Each button's middle, NEXT's, the arc's corner point and the cancel button's middle (own pixels).
var centers: Array[Vector2] = []
var next_center: Vector2 = Vector2.ZERO
var corner: Vector2 = Vector2.ZERO
var cancel_center: Vector2 = Vector2.ZERO
var _press_at: Vector2 = Vector2.ZERO
## Shoulder view (docs/GDD.md "Shoulder view"): a card is cast at the crosshair, so a finger dragged while holding one turns the view
## (`look_dragged`) instead of aiming from the button.
var shoulder: bool = false
var _last_finger: Vector2 = Vector2.ZERO

## Shoulder view: the finger holding a card moved this far (UI pixels): the view turns with it.
signal look_dragged(drag: Vector2)


func _init() -> void:
	super()
	name = "AbilityArc"
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func set_inset(value: Vector2) -> void:
	inset = value
	_layout()


## The arc's corner point on a `screen` of that size with those `insets` (right, bottom): the outermost buttons `HudStyle.EDGE` from the edges.
## The street games' action button stands here too, so the right thumb's home is the same in every game.
static func corner_point(screen: Vector2, insets: Vector2) -> Vector2:
	var reach: Vector2 = Vector2.ZERO
	for angle: float in ANGLES:
		var way: Vector2 = Vector2.from_angle(deg_to_rad(angle)) * ARC_RADIUS
		reach = reach.max(way)
	var offset: Vector2 = reach + Vector2.ONE * (BUTTON_RADIUS + HudStyle.EDGE)
	return screen - insets - offset


func _layout() -> void:
	corner = corner_point(size, inset)
	centers.clear()
	card_rects.clear()
	for angle: float in ANGLES:
		var middle: Vector2 = corner + Vector2.from_angle(deg_to_rad(angle)) * ARC_RADIUS
		centers.append(middle)
		card_rects.append(Rect2(middle - Vector2.ONE * BUTTON_RADIUS, Vector2.ONE * BUTTON_RADIUS * 2.0))
	next_center = corner + Vector2(NEXT_RADIUS * 0.6, NEXT_RADIUS * 0.4)
	next_rect = Rect2(next_center - Vector2.ONE * NEXT_RADIUS, Vector2.ONE * NEXT_RADIUS * 2.0)
	chat_rect = Rect2()
	var top: float = card_rects[0].position.y
	for rect: Rect2 in card_rects:
		top = minf(top, rect.position.y)
	var left: float = card_rects[0].position.x + SLIM_DROP * 0.6
	var right: float = size.x - inset.x - HudStyle.EDGE
	bar_rect = Rect2(Vector2(left, top - SLIM_BAR_GAP - SLIM_BAR), Vector2(maxf(0.0, right - left), SLIM_BAR))
	cancel_center = Vector2(corner.x, bar_rect.position.y - SLIM_DROP / 2.0 - CANCEL_GAP - CANCEL_RADIUS)
	_refresh()


## The faces of the old hand are not used: the buttons are drawn here.
func _sync_faces() -> void:
	for face: HandCard in [_next_face, _held_face] + _faces:
		face.visible = false


## A card that comes in just appears (no flight from NEXT: the buttons stay where they are).
func _card_changed(_slot: int, _old: WeaponDef, _new: WeaponDef, _old_next: WeaponDef) -> void:
	_released_slot = -1


func slot_at(local: Vector2) -> int:
	for slot: int in centers.size():
		if local.distance_to(centers[slot]) <= BUTTON_RADIUS + CARD_GAP:
			return slot
	return -1


## "Over the hand" is the cancel button: a card let go there is put back.
func _over_hand(local: Vector2) -> bool:
	return held_slot >= 0 and local.distance_to(cancel_center) <= CANCEL_RADIUS + CARD_GAP


func held_card_rect() -> Rect2:
	return Rect2()


func _press(pointer: int, viewport_position: Vector2) -> void:
	var on_button: bool = active and is_visible_in_tree() and held_slot < 0 and slot_at(_local(viewport_position)) >= 0
	super(pointer, viewport_position)
	if on_button and is_inside_tree():
		get_viewport().set_input_as_handled()  # a press on a button never turns the view behind it
	if held_slot >= 0:
		_press_at = finger
		_last_finger = finger
		aim_way = Vector2.ZERO
		over_hand = false


## The finger moved: still a tap inside the deadzone (`on_card`), else the drag aims (`aim_way`); over the cancel button it cancels.
func _move(viewport_position: Vector2) -> void:
	if held_slot < 0:
		return
	finger = _local(viewport_position)
	if shoulder:
		look_dragged.emit(finger - _last_finger)
		_last_finger = finger
		aim_way = Vector2.ZERO
	else:
		aim_way = way_for(held_slot, finger)
	on_card = aim_way == Vector2.ZERO and not over_hand and finger.distance_to(_press_at) <= AIM_DEADZONE
	over_hand = _over_hand(finger)
	if over_hand:
		on_card = false
	card_moved.emit(held_slot, viewport_position, on_card, over_hand)
	_refresh()


## The aim of a finger at `local` holding the button of `slot`: its offset from the button over `AIM_RADIUS`, at most 1 long, zero inside the
## deadzone.
func way_for(slot: int, local: Vector2) -> Vector2:
	var offset: Vector2 = local - centers[slot]
	if offset.length() <= AIM_DEADZONE:
		return Vector2.ZERO
	return (offset / AIM_RADIUS).limit_length(1.0)


func _end_hold() -> void:
	aim_way = Vector2.ZERO
	super()


# ---- drawing --------------------------------------------------------------------

func _draw() -> void:
	if centers.size() < SLOTS:
		return
	_draw_bar()
	if next_def != null:
		_draw_button(next_center, NEXT_RADIUS, next_def, 1.0, false)
		if next_fill < 1.0:
			_draw_cooling_on(self, next_center, NEXT_RADIUS, next_fill)
	else:
		HudStyle.draw_disc(self, next_center, NEXT_RADIUS)
	_draw_next_label()
	for slot: int in SLOTS:
		var middle: Vector2 = centers[slot] + Vector2(deny_offset(slot), 0.0)
		var def: WeaponDef = defs[slot]
		if def == null:
			HudStyle.draw_disc(self, middle, BUTTON_RADIUS)
			continue
		var dim: float = BLOCK_DIM if not blocked_text.is_empty() else 0.0
		_draw_button(middle, BUTTON_RADIUS, def, 1.0 - dim, slot == held_slot)
	if held_slot >= 0:
		_draw_cancel()


func _draw_next_label() -> void:
	var font: Font = KalyeahTheme.ACTION_FONT
	var label: String = next_label()
	var width: float = font.get_string_size(label, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL).x
	var at: Vector2 = Vector2(next_center.x - width / 2.0, next_center.y - NEXT_RADIUS - 6.0)
	draw_string_outline(font, at, label, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL, HudStyle.outline(HudStyle.SMALL), HudStyle.INK)
	draw_string(font, at, label, HORIZONTAL_ALIGNMENT_LEFT, -1.0, HudStyle.SMALL, RoyaleStyle.CREAM)


## One round button: the middle of the card picture (or the skill icon on the kind colour) in a circle, with a cream rim (gold while held).
func _draw_button(middle: Vector2, radius: float, def: WeaponDef, light: float, held: bool) -> void:
	var tint: Color = Color(light, light, light)
	var picture: Texture2D = CardStyle.picture(def.id)
	if picture != null:
		var share: Vector2 = Vector2(ART_SHARE, ART_SHARE * float(picture.get_width()) / float(picture.get_height()))
		_draw_round_texture(middle, radius, picture, share, tint)
	else:
		draw_circle(middle, radius, CardStyle.ground(def.kind) * tint)
		var icon: Texture2D = Icons.skill(def.id)
		if icon != null:
			_draw_round_texture(middle, radius * 0.8, icon, Vector2.ONE, tint)
	draw_arc(middle, radius, 0.0, TAU, CIRCLE_STEPS, RIM_HELD if held else RIM, HudStyle.RIM_WIDTH, true)


## `texture` cut to a circle of `radius` at `middle`: the circle shows the middle `share` of the texture's width and height.
func _draw_round_texture(middle: Vector2, radius: float, texture: Texture2D, share: Vector2, tint: Color) -> void:
	var points: PackedVector2Array = PackedVector2Array()
	var uvs: PackedVector2Array = PackedVector2Array()
	for i: int in CIRCLE_STEPS:
		var way: Vector2 = Vector2.from_angle(TAU * float(i) / float(CIRCLE_STEPS))
		points.append(middle + way * radius)
		uvs.append(Vector2(0.5, 0.5) + way * share / 2.0)
	draw_polygon(points, PackedColorArray([tint]), uvs, texture)


func _draw_cancel() -> void:
	HudStyle.draw_disc(self, cancel_center, CANCEL_RADIUS, CANCEL_RIM)
	if over_hand:
		draw_circle(cancel_center, CANCEL_RADIUS - HudStyle.RIM_WIDTH, CANCEL_HOT)
	var arm: float = CANCEL_RADIUS * 0.38
	for way: Vector2 in [Vector2(1.0, 1.0), Vector2(1.0, -1.0)]:
		draw_line(cancel_center - way * arm, cancel_center + way * arm, HudStyle.THUMB, 5.0, true)


## The slim ice water bar over the arc: a navy track, the fill (each whole point a segment, divided in navy), and the whole points on a drop at
## its left end. The old hand's chunky bar, its marker and its "Max" caption are left out: the cards' own costs say what each needs.
func _draw_bar() -> void:
	if not uses_elixir or bar_rect.size.x <= 0.0:
		return
	var radius: float = bar_rect.size.y / 2.0
	var track: StyleBoxFlat = StyleBoxFlat.new()
	track.bg_color = BAR_TRACK
	track.set_corner_radius_all(roundi(radius))
	track.anti_aliasing = true
	draw_style_box(track, bar_rect)
	var share: float = clampf(shown_elixir / maxf(elixir_max, 0.001), 0.0, 1.0)
	if share > 0.0:
		var fill: StyleBoxFlat = StyleBoxFlat.new()
		fill.bg_color = ICE_WATER
		fill.set_corner_radius_all(roundi(radius))
		fill.anti_aliasing = true
		draw_style_box(fill, Rect2(bar_rect.position, Vector2(maxf(bar_rect.size.y, bar_rect.size.x * share), bar_rect.size.y)))
	for index: int in range(1, bar_segments()):
		var x: float = bar_rect.position.x + bar_rect.size.x * float(index) / float(bar_segments())
		draw_line(Vector2(x, bar_rect.position.y + 2.0), Vector2(x, bar_rect.end.y - 2.0), BAR_DIVIDER, 2.0)
	HandArt.draw_ice(self, Vector2(bar_rect.position.x, bar_rect.get_center().y), SLIM_DROP, str(floori(elixir + 0.001)), HudStyle.SMALL)


## Over the buttons: the sweep of a cost the bar has not reached, the cost drop, the word (STUNNED, NO TARGET), the red flash of a press
## that was not ready, and the thumb of the drag while aiming.
func _draw_over_cards() -> void:
	if centers.size() < SLOTS:
		return
	for slot: int in SLOTS:
		var def: WeaponDef = defs[slot]
		if def == null:
			continue
		var middle: Vector2 = centers[slot] + Vector2(deny_offset(slot), 0.0)
		var blocked: bool = not blocked_text.is_empty()
		if not blocked and not pickable[slot] and reached[slot] < 1.0:
			_overlay.draw_circle(middle, BUTTON_RADIUS, Color(COOL, COOL.a * 0.4))
			_draw_cooling_on(_overlay, middle, BUTTON_RADIUS, reached[slot])
		if uses_elixir:
			var drop: float = BUTTON_RADIUS * 0.62
			HandArt.draw_ice(_overlay, middle + Vector2(0.0, BUTTON_RADIUS * 0.78), drop, str(def.elixir_cost), COST_FONT_MIN, pickable[slot] or reached[slot] >= 1.0)
		var tag: String = blocked_text if blocked else notices[slot]
		if not tag.is_empty():
			_draw_tag(_overlay, middle, UiText.t(tag))
		if _deny[slot] > 0.0:
			_overlay.draw_circle(middle, BUTTON_RADIUS, Color(DENY_FLASH, DENY_FLASH.a * _deny[slot] / DENY_TIME))
	if held_slot >= 0 and aim_way != Vector2.ZERO:
		_overlay.draw_circle(centers[held_slot] + aim_way * AIM_RADIUS, BUTTON_RADIUS * 0.45, HudStyle.THUMB)


## The dark sweep over a button that is not ready yet: what is still missing, clockwise from the top.
func _draw_cooling_on(canvas: CanvasItem, middle: Vector2, radius: float, reached_share: float) -> void:
	var missing: float = 1.0 - clampf(reached_share, 0.0, 1.0)
	if missing <= 0.0:
		return
	var points: PackedVector2Array = PackedVector2Array([middle])
	var steps: int = maxi(2, ceili(CIRCLE_STEPS * missing))
	for i: int in steps + 1:
		var angle: float = -PI / 2.0 + TAU * (1.0 - missing) + TAU * missing * float(i) / float(steps)
		points.append(middle + Vector2.from_angle(angle) * radius)
	canvas.draw_colored_polygon(points, COOL)


func _draw_over_held() -> void:
	pass
