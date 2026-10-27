class_name CardHand
extends Control
## The bottom of the match screen (owner: "make the bottom part exactly like clash royale"; docs/GDD.md "Controls: tap to move,
## cards to cast"): the NEXT card small at one side, the four cards of the hand in a row, and the ice water bar along the bottom with
## the whole points in a drop at its left end and your health at its right end. Each card shows its cost in a drop at its top
## corner; a card you cannot afford is greyed, filling up from the bottom as the bar gets there.
##
## It reads raw touches so a finger on a card never blocks another finger on the street. Press a card that is
## ready and it lifts under the finger; drag it out over the street and it shrinks so the aim guide shows; let go there to cast,
## back on the hand to cancel, or quickly on the same card to tap (MatchInput decides which). A card that is not ready cannot be
## picked up: it shakes (flashes only, with Reduce motion). It reads the sim
## through `show_state()` and reports gestures with signals; it never decides a rule.

## A card was picked up (`slot`).
signal card_pressed(slot: int)
## The held card's finger is at `at` (viewport pixels): still on the card it pressed (`on_card`), and over the hand (`over_hand`).
signal card_moved(slot: int, at: Vector2, on_card: bool, over_hand: bool)
## The held card was let go at `at` (with the same flags as `card_moved`).
signal card_released(slot: int, at: Vector2, on_card: bool, over_hand: bool)
## The held card was dropped without a cast (a touch the system cancelled, the controls switched off).
signal card_cancelled(slot: int)
## A card that is not ready was pressed (it shakes).
signal card_denied(slot: int)

const SLOTS: int = 4
## Layout on the 720 px base (the tray's own height is MatchHud.CONSOLE_HEIGHT), like Clash Royale's bottom: a column BESIDE the plate
## (the chat button above NEXT, over the street's edge) and the plate with the four cards and the ice water bar. Side margin (the
## plate's rim), the gap between cards, the row's top (under the plate's top rim), the card's height for its width, the bar's
## height, the gap above it and the room under it (the bottom rim).
const COLUMN: float = 116.0
const SIDE: float = 40.0
const CARD_GAP: float = 10.0
const ROW_TOP: float = 28.0
## The card's height over its width, measured on the Clash Royale reference (about 276 x 219 px).
const CARD_ASPECT: float = 1.26
const BAR_HEIGHT: float = 26.0
const BAR_GAP: float = 14.0
const BOTTOM_PAD: float = 32.0
## The NEXT card is this share of a hand card, under its label, in the column beside the plate; the chat button is above it.
const NEXT_SCALE: float = 0.66
const COLUMN_BOTTOM: float = 18.0
const OUT_GAP: float = 8.0
const CHAT_HEIGHT: float = 80.0
const NEXT_FONT: int = UiMetrics.CAPTION
const NEXT_COLOR: Color = Color.WHITE
## The ice water bag on a card (its height) and its number; the bar's bag and its number.
const COST_BAG: float = 60.0
const COST_FONT: int = UiMetrics.CAPTION
const BAR_BAG: float = 56.0
## The "Max: 10" caption under the bar's start.
const MAX_FONT: int = UiMetrics.WORLD
const BAR_FONT: int = UiMetrics.ACTION
## The ice water colour: a code-drawn stand-in until the art-director sets a token (docs/ART.md has none yet).
const ICE_WATER: Color = Color(0.36, 0.8, 1.0)
## A full bar's edge: a light ice blue (it was gold; ice water stays blue).
const ICE_WATER_FULL_EDGE: Color = Color(0.72, 0.93, 1.0)
## The thin outline of the ice water bar.
const BAR_OUTLINE: int = 2
## The empty part of the ice water bar: a flat, opaque dark blue (just the colour: no sheen, no texture).
const ICE_EMPTY: Color = Color(0.03, 0.14, 0.42)
## The segment that is filling shows this much of its share in see-through white (like Clash Royale's elixir bar).
const REFILL_WHITE: Color = Color(1.0, 1.0, 1.0, 0.4)
## A card you cannot afford: a grey wash over the part the bar has not reached yet, a lighter one over the rest.
const GREY: Color = Color(0.2, 0.21, 0.25, 0.8)
const GREY_REACHED: Color = Color(0.2, 0.21, 0.25, 0.5)
## Blocked by a status (stunned...): the card dims and says why.
const BLOCK_DIM: float = 0.55
const TAG_COLOR: Color = Color(1.0, 0.78, 0.3)
const TAG_HEIGHT: float = 30.0
## A held card over the hand rises this far; over the street it shrinks to this share and stands above the finger (the finger and the
## card never hide the aim guide's point).
const HOLD_LIFT: float = 14.0
const STREET_SCALE: float = 0.6
## Pressing a card that is not ready: it shakes this far, this fast, for this long, with a red flash (no shake with Reduce motion).
const DENY_TIME: float = 0.32
const DENY_SHAKE: float = 9.0
const DENY_WAVES: float = 3.0
const DENY_FLASH: Color = Color(1.0, 0.25, 0.2, 0.45)
const DENY_BUZZ_MS: int = 30
## How fast the shown bar catches up with the real elixir (points per second); it snaps with Reduce motion.
const BAR_CATCH_UP: float = 8.0
const NO_POINTER: int = -1

## Mirrored for a left-handed player: NEXT at the right, the hand to its left.
var mirrored: bool = false
## Where everything sits, in this control's own pixels (set by `_layout()` from the size).
var card_rects: Array[Rect2] = []
var next_rect: Rect2 = Rect2()
## The quick chat button's rect, above NEXT (outside the tray, like it).
var chat_rect: Rect2 = Rect2()
var bar_rect: Rect2 = Rect2()

## What the hand shows (from `show_state()`).
var defs: Array[WeaponDef] = []
## The card can be picked up now (playing, affordable, not stunned...).
var pickable: Array[bool] = []
## How close a card is to being affordable (0 to 1; 1 = it is).
var reached: Array[float] = []
var blocked_text: String = ""
var notices: Array[String] = []
var next_def: WeaponDef
## Elixir off (the old shuffler, D26): the card in the shuffler's window fills as it reloads (0 to 1), and an empty slot's progress.
var next_fill: float = 1.0
var wait_fraction: float = 0.0
var wait_seconds: int = 0
var uses_elixir: bool = true
var elixir: float = 0.0
var elixir_max: float = 10.0
var shown_elixir: float = 0.0
## Gestures are taken (false under a menu, while tagged, during the opening show).
var active: bool = false

## The held card: its slot, the pointer holding it, where the finger is (own pixels), and whether it is on its card / over the hand.
var held_slot: int = -1
var finger: Vector2 = Vector2.ZERO
var on_card: bool = true
var over_hand: bool = true
var _pointer: int = NO_POINTER
var _deny: Array[float] = []
## A card arriving from NEXT is drawn by its flight, not in its slot, for this long.
var _arrive: Array[float] = []
## Where the last card was let go (own pixels) and when, so the card that leaves flies out from there.
var _released_rect: Rect2 = Rect2()
var _released_slot: int = -1


func _init() -> void:
	name = "CardHand"
	# the ice water drop is a big picture drawn small: its mipmaps keep it smooth
	texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	mouse_filter = Control.MOUSE_FILTER_STOP
	accessibility_name = "Your hand"
	defs.resize(SLOTS)
	pickable.resize(SLOTS)
	reached.resize(SLOTS)
	notices.resize(SLOTS)
	_deny.resize(SLOTS)
	_arrive.resize(SLOTS)
	for slot: int in SLOTS:
		pickable[slot] = false
		reached[slot] = 1.0
		notices[slot] = ""
		_deny[slot] = 0.0
		_arrive[slot] = 0.0
	resized.connect(_layout)


func _ready() -> void:
	_layout()


## The tray's height on the 720 px base for a `width` wide screen: the card row, the bar and their margins.
static func tray_height(width: float = 720.0) -> float:
	return ROW_TOP + card_size(width).y + BAR_GAP + BAR_HEIGHT + BOTTOM_PAD


## A hand card's size when the row is `width` wide: the four cards share what the column leaves.
static func card_size(width: float) -> Vector2:
	var card_width: float = floorf((width - COLUMN - SIDE * 2.0 - CARD_GAP * float(SLOTS - 1)) / float(SLOTS))
	return Vector2(card_width, roundf(card_width * CARD_ASPECT))


func set_mirrored(value: bool) -> void:
	mirrored = value
	_layout()


func _layout() -> void:
	var card: Vector2 = card_size(size.x)
	var next_size: Vector2 = card * NEXT_SCALE
	var label_room: float = float(NEXT_FONT) + 8.0
	# the column is beside the plate: at the left, or at the right for a left-handed player
	var column_x: float = 0.0 if not mirrored else size.x - COLUMN
	var cards_x: float = (COLUMN if not mirrored else 0.0) + SIDE
	next_rect = Rect2(Vector2(column_x + (COLUMN - next_size.x) / 2.0, size.y - COLUMN_BOTTOM - next_size.y), next_size)
	var chat_size: Vector2 = Vector2(CHAT_HEIGHT * HandArt.CHAT_ASPECT, CHAT_HEIGHT)
	chat_rect = Rect2(Vector2(column_x + (COLUMN - chat_size.x) / 2.0, next_rect.position.y - label_room - OUT_GAP - chat_size.y), chat_size)
	card_rects.clear()
	for slot: int in SLOTS:
		card_rects.append(Rect2(Vector2(cards_x + float(slot) * (card.x + CARD_GAP), ROW_TOP), card))
	var bar_top: float = ROW_TOP + card.y + BAR_GAP
	var bar_left: float = cards_x + BAR_BAG * HandArt.ICE_ASPECT / 2.0
	var bar_right: float = cards_x + 4.0 * card.x + 3.0 * CARD_GAP
	bar_rect = Rect2(Vector2(bar_left, bar_top), Vector2(bar_right - bar_left, BAR_HEIGHT))
	queue_redraw()


## The four cards' rect, in own pixels.
func row_rect() -> Rect2:
	var rect: Rect2 = card_rects[0] if not card_rects.is_empty() else Rect2()
	for card: Rect2 in card_rects:
		rect = rect.merge(card)
	return rect


## Reads the local player's hand, elixir and health. `playing`: gestures may start; `block` names what stops casting ("" = nothing);
## `notice[slot]` is a passing note ("NO TARGET").
func show_state(sim: MatchSim, player: PlayerState, playing: bool, block: String, notice: Array[String]) -> void:
	active = playing
	uses_elixir = sim.pays_elixir(player)
	elixir = player.elixir
	elixir_max = sim.rules.elixir_max
	blocked_text = block
	var new_next: WeaponDef = sim.weapon_defs.get(player.next_card) as WeaponDef
	var shuffling: WeaponDef = sim.weapon_defs.get(player.shuffle_card) as WeaponDef
	next_fill = 1.0
	if shuffling != null:
		new_next = shuffling
		next_fill = 1.0 - clampf(player.shuffle_left / maxf(player.shuffle_total, 0.001), 0.0, 1.0)
	wait_fraction = 1.0 - clampf(player.shuffle_left / maxf(player.shuffle_total, 0.001), 0.0, 1.0)
	wait_seconds = ceili(player.shuffle_left)
	var old_next: WeaponDef = next_def
	for slot: int in SLOTS:
		var def: WeaponDef = null
		if slot < player.weapons.size():
			def = sim.weapon_defs.get(player.weapons[slot]) as WeaponDef
		if def != defs[slot]:
			_card_changed(slot, defs[slot], def, old_next)
		defs[slot] = def
		pickable[slot] = playing and def != null and sim.weapon_ready(player, slot)
		reached[slot] = _reached(sim, player, slot, def)
		notices[slot] = notice[slot] if slot < notice.size() else ""
	next_def = new_next
	if held_slot >= 0 and (not playing or not blocked_text.is_empty() or defs[held_slot] == null):
		cancel_hold()  # a stun or a tag drops the card (the sim spent nothing)
	_describe()
	queue_redraw()


## How close the card in `slot` is to ready: the bar against its cost (elixir), or its cooldown (elixir off, the old two weapons).
func _reached(sim: MatchSim, player: PlayerState, slot: int, def: WeaponDef) -> float:
	if def == null:
		return 0.0
	if sim.pays_elixir(player):
		return clampf(player.elixir / maxf(float(def.elixir_cost), 0.001), 0.0, 1.0)
	if slot < player.weapon_cooldowns.size() and def.cooldown > 0.0:
		return 1.0 - clampf(player.weapon_cooldowns[slot] / def.cooldown, 0.0, 1.0)
	return 1.0


## A slot got a new card. When it is NEXT (a card was played) the one that went flies out (from where it was let go) and NEXT slides
## into the slot; a fresh deal flies nothing. No flights with Reduce motion.
func _card_changed(slot: int, old: WeaponDef, new: WeaponDef, old_next: WeaponDef) -> void:
	var from: Rect2 = _released_rect if _released_slot == slot else Rect2()
	_released_slot = -1
	if not is_visible_in_tree() or card_rects.size() <= slot or old == null or new == null or new != old_next:
		return
	if from.size == Vector2.ZERO:
		from = card_rects[slot]
	CardFlight.fly(self, old, from, Rect2(from.get_center() - Vector2(0.0, from.size.y * 0.5), Vector2.ONE))
	if CardFlight.fly(self, new, next_rect, card_rects[slot]) != null:
		_arrive[slot] = CardFlight.TIME


## What a screen reader says about the hand: each card, its cost and whether it is ready, and the elixir.
func _describe() -> void:
	var parts: PackedStringArray = PackedStringArray()
	for slot: int in SLOTS:
		var def: WeaponDef = defs[slot]
		if def == null:
			continue
		parts.append("Card %d: %s, %d ice water%s" % [slot + 1, def.display_name, def.elixir_cost, "" if pickable[slot] else ", not ready"])
	parts.append("Ice water %d of %d" % [floori(elixir), roundi(elixir_max)])
	var words: String = ". ".join(parts)
	if accessibility_description != words:
		accessibility_description = words


func _process(delta: float) -> void:
	var target: float = elixir if uses_elixir else 0.0
	shown_elixir = target if Settings.reduced_motion else move_toward(shown_elixir, target, BAR_CATCH_UP * delta)
	for slot: int in SLOTS:
		_deny[slot] = maxf(0.0, _deny[slot] - delta)
		_arrive[slot] = maxf(0.0, _arrive[slot] - delta)
	queue_redraw()


# ---- touches -------------------------------------------------------------------

## The slot whose card is under `local` (own pixels; each card's touch area reaches half way into the gaps), or -1.
func slot_at(local: Vector2) -> int:
	for slot: int in card_rects.size():
		if card_rects[slot].grow(CARD_GAP / 2.0).has_point(local):
			return slot
	return -1


func holding() -> bool:
	return held_slot >= 0


## Drops the held card without a cast (a menu opened, a stun, a system-cancelled touch).
func cancel_hold() -> void:
	if held_slot < 0:
		return
	var slot: int = held_slot
	_end_hold()
	card_cancelled.emit(slot)


func _input(event: InputEvent) -> void:
	if event is InputEventScreenTouch:
		var touch: InputEventScreenTouch = event as InputEventScreenTouch
		if touch.pressed:
			if _pointer == NO_POINTER:
				_press(touch.index, touch.position)
		elif touch.index == _pointer:
			if touch.canceled:
				cancel_hold()
			else:
				_release(touch.position)
	elif event is InputEventScreenDrag:
		var drag: InputEventScreenDrag = event as InputEventScreenDrag
		if drag.index == _pointer:
			_move(drag.position)


func _local(viewport_position: Vector2) -> Vector2:
	return get_global_transform_with_canvas().affine_inverse() * viewport_position


## A finger went down at `viewport_position`: pick up the card under it if it is ready, else shake it.
func press_at(pointer: int, viewport_position: Vector2) -> void:
	_press(pointer, viewport_position)


func _press(pointer: int, viewport_position: Vector2) -> void:
	if not active or not is_visible_in_tree() or held_slot >= 0:
		return
	var local: Vector2 = _local(viewport_position)
	var slot: int = slot_at(local)
	if slot < 0 or defs[slot] == null:
		return
	if not pickable[slot]:
		deny(slot)
		return
	_pointer = pointer
	held_slot = slot
	finger = local
	on_card = true
	over_hand = true
	card_pressed.emit(slot)
	queue_redraw()


func _move(viewport_position: Vector2) -> void:
	if held_slot < 0:
		return
	finger = _local(viewport_position)
	on_card = card_rects[held_slot].grow(CARD_GAP / 2.0).has_point(finger)
	over_hand = Rect2(Vector2.ZERO, size).has_point(finger)
	card_moved.emit(held_slot, viewport_position, on_card, over_hand)
	queue_redraw()


func _release(viewport_position: Vector2) -> void:
	if held_slot < 0:
		_pointer = NO_POINTER
		return
	_move(viewport_position)
	var slot: int = held_slot
	_released_rect = held_card_rect()
	_released_slot = slot if not over_hand else -1
	var was_on_card: bool = on_card
	var was_over_hand: bool = over_hand
	_end_hold()
	card_released.emit(slot, viewport_position, was_on_card, was_over_hand)


func _end_hold() -> void:
	held_slot = -1
	_pointer = NO_POINTER
	on_card = true
	over_hand = true
	queue_redraw()


## A card that is not ready was pressed: it shakes and flashes (only flashes with Reduce motion), and the phone buzzes once.
func deny(slot: int) -> void:
	_deny[slot] = DENY_TIME
	Settings.vibrate(DENY_BUZZ_MS)
	card_denied.emit(slot)
	queue_redraw()


## How far a denied card is pushed sideways now (0 with Reduce motion or when it is not shaking).
func deny_offset(slot: int) -> float:
	if Settings.reduced_motion or _deny[slot] <= 0.0:
		return 0.0
	var t: float = 1.0 - _deny[slot] / DENY_TIME
	return sin(t * TAU * DENY_WAVES) * DENY_SHAKE * (1.0 - t)


func denied(slot: int) -> bool:
	return _deny[slot] > 0.0


## Where the held card is drawn (own pixels): under the finger over the hand, smaller and above the finger over the street.
func held_card_rect() -> Rect2:
	if held_slot < 0:
		return Rect2()
	var card: Vector2 = card_rects[held_slot].size
	if over_hand:
		return Rect2(finger - card / 2.0 - Vector2(0.0, HOLD_LIFT), card)
	var small: Vector2 = card * STREET_SCALE
	return Rect2(finger - Vector2(small.x / 2.0, small.y * 1.25), small)


# ---- drawing --------------------------------------------------------------------

func _draw() -> void:
	_draw_next()
	for slot: int in card_rects.size():
		_draw_slot(slot)
	if not blocked_text.is_empty() and _tag_too_wide(blocked_text) and not card_rects.is_empty():
		var cards: Rect2 = card_rects[0]
		for card: Rect2 in card_rects:
			cards = cards.merge(card)
		_draw_tag(cards.get_center(), UiText.t(blocked_text))
	_draw_bar()
	if held_slot >= 0 and defs[held_slot] != null:
		var rect: Rect2 = held_card_rect()
		CardStyle.draw(self, rect, defs[held_slot], 0.0, true)
		_draw_cost(rect, defs[held_slot], true)


## The word over the next card.
func next_label() -> String:
	return UiText.t("Next:")


func _draw_next() -> void:
	var font: Font = KalyeahTheme.ACTION_FONT
	var label: String = next_label()
	var width: float = font.get_string_size(label, HORIZONTAL_ALIGNMENT_LEFT, -1.0, NEXT_FONT).x
	var at: Vector2 = Vector2(next_rect.get_center().x - width / 2.0, next_rect.position.y - 8.0)
	draw_string_outline(font, at, label, HORIZONTAL_ALIGNMENT_LEFT, -1.0, NEXT_FONT, 7, Color.BLACK)
	draw_string(font, at, label, HORIZONTAL_ALIGNMENT_LEFT, -1.0, NEXT_FONT, NEXT_COLOR)
	if next_def == null:
		CardStyle.draw_empty(self, next_rect)
	elif next_fill < 1.0:
		CardStyle.draw_filling(self, next_rect, next_def, next_fill)
	else:
		CardStyle.draw(self, next_rect, next_def)


func _draw_slot(slot: int) -> void:
	var rect: Rect2 = card_rects[slot]
	var def: WeaponDef = defs[slot]
	if def == null:
		CardStyle.draw_back(self, rect, wait_fraction, wait_seconds)
		return
	if slot == held_slot:
		CardStyle.draw_empty(self, rect)
		return
	if _arrive[slot] > 0.0:
		CardStyle.draw_empty(self, rect)
		return
	rect.position.x += deny_offset(slot)
	var blocked: bool = not blocked_text.is_empty()
	CardStyle.draw(self, rect, def, BLOCK_DIM if blocked else 0.0)
	if not blocked and not pickable[slot] and reached[slot] < 1.0:
		_draw_grey(rect, reached[slot])
	_draw_cost(rect, def, pickable[slot] or reached[slot] >= 1.0)
	var tag: String = blocked_text if blocked else notices[slot]
	# a word wider than a card (CAPTURING) is said once across the hand, not four times over one another
	if not tag.is_empty() and not _tag_too_wide(tag):
		_draw_tag(rect.get_center(), UiText.t(tag))
	if _deny[slot] > 0.0:
		var flash: StyleBoxFlat = StyleBoxFlat.new()
		flash.bg_color = Color(DENY_FLASH, DENY_FLASH.a * _deny[slot] / DENY_TIME)
		flash.set_corner_radius_all(CardStyle.RADIUS)
		draw_style_box(flash, rect)


## Greys a card the bar has not reached: dark over the share still missing (from the top), lighter over the share already there.
func _draw_grey(rect: Rect2, share: float) -> void:
	var missing: float = rect.size.y * (1.0 - clampf(share, 0.0, 1.0))
	var wash: StyleBoxFlat = StyleBoxFlat.new()
	wash.bg_color = GREY_REACHED
	wash.set_corner_radius_all(CardStyle.RADIUS)
	wash.anti_aliasing = true
	draw_style_box(wash, rect)
	if missing <= 1.0:
		return
	var dark: StyleBoxFlat = StyleBoxFlat.new()
	dark.bg_color = GREY
	dark.corner_radius_top_left = CardStyle.RADIUS
	dark.corner_radius_top_right = CardStyle.RADIUS
	dark.anti_aliasing = true
	draw_style_box(dark, Rect2(rect.position, Vector2(rect.size.x, missing)))


## The card's cost on an ice water bag on its bottom edge, in the middle (elixir only); a cost the bar has not reached is drawn in grey.
func _draw_cost(rect: Rect2, def: WeaponDef, affordable: bool) -> void:
	if not uses_elixir:
		return
	var height: float = minf(COST_BAG, rect.size.x * 0.42)
	# like Clash Royale: on the card's bottom edge, in the middle, half over the edge
	var centre: Vector2 = Vector2(rect.get_center().x, rect.end.y - height * 0.2)
	HandArt.draw_ice(self, centre, height, str(def.elixir_cost), COST_FONT, affordable)


## The pill for `tag` would be wider than one card.
func _tag_too_wide(tag: String) -> bool:
	if card_rects.is_empty():
		return false
	var width: float = KalyeahTheme.ACTION_FONT.get_string_size(UiText.t(tag), HORIZONTAL_ALIGNMENT_LEFT, -1.0, COST_FONT).x + 20.0
	return width > card_rects[0].size.x


func _draw_tag(at: Vector2, text: String) -> void:
	var font: Font = KalyeahTheme.ACTION_FONT
	var width: float = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, COST_FONT).x + 20.0
	var pill: StyleBoxFlat = StyleBoxFlat.new()
	pill.bg_color = Color(0.1, 0.08, 0.14, 0.92)
	pill.border_color = TAG_COLOR
	pill.set_border_width_all(2)
	pill.set_corner_radius_all(UiSkin.round_radius(TAG_HEIGHT))
	draw_style_box(pill, Rect2(at - Vector2(width / 2.0, TAG_HEIGHT / 2.0), Vector2(width, TAG_HEIGHT)))
	draw_string_outline(font, at + Vector2(-width / 2.0 + 10.0, COST_FONT * 0.36), text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, COST_FONT, 5, Icons.OUTLINE)
	draw_string(font, at + Vector2(-width / 2.0 + 10.0, COST_FONT * 0.36), text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, COST_FONT, TAG_COLOR)


## The ice water bar: a dark track cut into `elixir_max` segments, filled smoothly to the shown elixir, a gold edge when it is full,
## and the whole points on an ice water bag at its left end.
func _draw_bar() -> void:
	if not uses_elixir or bar_rect.size.x <= 0.0:
		return
	var segments: int = maxi(roundi(elixir_max), 1)
	var full: bool = elixir >= elixir_max - 0.001
	var track: StyleBoxFlat = StyleBoxFlat.new()
	track.bg_color = ICE_EMPTY
	track.border_color = ICE_WATER_FULL_EDGE if full else RoyaleStyle.NAVY
	track.set_border_width_all(BAR_OUTLINE)
	track.set_corner_radius_all(UiSkin.bar_radius(bar_rect.size.y))
	track.anti_aliasing = true
	draw_style_box(track, bar_rect)
	var inner: Rect2 = bar_rect.grow(-float(BAR_OUTLINE))
	# the bar fills one whole segment at a time (solid ice water); the segment being filled shows its progress in see-through white
	var fills: PackedFloat32Array = segment_fills(shown_elixir, segments)
	var span: float = inner.size.x / float(segments)
	for segment: int in segments:
		var cell: Rect2 = Rect2(Vector2(inner.position.x + span * float(segment), inner.position.y), Vector2(span, inner.size.y))
		if fills[segment] >= 1.0:
			draw_style_box(_segment_style(ICE_WATER.lightened(0.15) if full else ICE_WATER, segment, segments, inner.size.y), cell)
		elif fills[segment] > 0.0:
			draw_style_box(_segment_style(REFILL_WHITE, segment, segments, inner.size.y), Rect2(cell.position, Vector2(cell.size.x * fills[segment], cell.size.y)))
	for segment: int in range(1, segments):
		var x: float = inner.position.x + span * float(segment)
		draw_line(Vector2(x, inner.position.y + 2.0), Vector2(x, inner.end.y - 2.0), Color(0.0, 0.0, 0.0, 0.4), 2.0)
	# like Clash Royale: the bag overlaps the bar's start, the whole points in big letters just right of it (over the bar's start), and the
	# most you can hold in small letters under them
	var bag: Vector2 = Vector2(bar_rect.position.x, bar_rect.get_center().y - BAR_BAG * 0.12)
	HandArt.draw_ice(self, bag, BAR_BAG, "", BAR_FONT)
	var font: Font = KalyeahTheme.ACTION_FONT
	var number_at: Vector2 = Vector2(bag.x + BAR_BAG * HandArt.ICE_ASPECT / 2.0 + 6.0, bar_rect.get_center().y - BAR_HEIGHT * 0.1)
	RoyaleStyle.lettered(self, font, str(floori(elixir + 0.001)), number_at, BAR_FONT)
	var max_text: String = UiText.f("Max: %d", [roundi(elixir_max)])
	# vertically centred on the bar's bottom outline
	RoyaleStyle.lettered(self, font, max_text, Vector2(number_at.x, max_caption_baseline(bar_rect)), MAX_FONT)


## A filled piece of the ice water bar: flat, its outer corners (the first piece's left ones, the last piece's right ones) rounded like the track's.
func _segment_style(color: Color, segment: int, segments: int, height: float) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = color
	var radius: int = UiSkin.bar_radius(height)
	if segment == 0:
		box.corner_radius_top_left = radius
		box.corner_radius_bottom_left = radius
	if segment == segments - 1:
		box.corner_radius_top_right = radius
		box.corner_radius_bottom_right = radius
	box.anti_aliasing = true
	return box


## How full each segment of the ice water bar is for `elixir` points: 1 solid, 0 empty, and between 0 and 1 the one being filled (whole
## segments fill at once; only the next one shows progress).
static func segment_fills(elixir: float, segments: int) -> PackedFloat32Array:
	var fills: PackedFloat32Array = PackedFloat32Array()
	var whole: int = clampi(floori(elixir + 0.0001), 0, segments)
	for segment: int in segments:
		if segment < whole:
			fills.append(1.0)
		elif segment == whole:
			fills.append(clampf(elixir - float(whole), 0.0, 1.0))
		else:
			fills.append(0.0)
	return fills


## The baseline of the "Max: 10" caption: its middle is level with the middle of the bar's bottom outline.
static func max_caption_baseline(bar: Rect2) -> float:
	return bar.end.y - float(BAR_OUTLINE) / 2.0 + float(MAX_FONT) * 0.36
