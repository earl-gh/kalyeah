class_name CardHand
extends Control
## The bottom of the match screen (owner: "make the bottom part exactly like clash royale"; docs/GDD.md "Controls: tap to move,
## cards to cast"): the NEXT card small at one side, the four cards of the hand in a row, and the ice water bar along the bottom with
## the whole points in a drop at its left end and your health at its right end. Each card shows its cost in a drop inside its bottom
## edge, in the middle; a card you cannot afford is greyed, filling up from the bottom as the bar gets there. The bar marks in white how
## much the cheapest card in the hand needs, pops each segment as it completes, and keeps dark-blue dividers through the filled portion, including a full bar.
##
## It reads raw touches so a finger on a card never blocks another finger on the street. Press a card that is
## ready and it lifts under the finger; drag it out over the street and it shrinks so the aim guide shows; let go there to cast,
## back on the hand to cancel, or quickly on the same card to tap (MatchInput decides which). A card that is not ready cannot be
## picked up: it shakes (flashes only, with Reduce motion). It reads the sim
## through `show_state()` and reports gestures with signals; it never decides a rule.
##
## The cards are `HandCard` nodes: the owner's picture of the weapon when there is one (`CardStyle.picture`), else an old Filipino teks card (the mode card's worn look): the hand draws what is under them (the NEXT word,
## the hollow of a slot a card is out of, the bar) in its own `_draw`, the cards are its children, and what lies over them (the ice water
## drop, the washes, the words, the flash) is drawn on an overlay child after them, and over the card under the finger on another one.

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
## The ice water drop on a card: this share of the card's width (the drop's own width), standing inside the card's bottom edge with
## this share of the card's width of room under it (it never hangs over the edge); the bar's drop and its number.
const COST_SHARE: float = 0.34
const COST_PAD_SHARE: float = 0.04
const COST_FONT: int = UiMetrics.ACTION
## A card shrunk over the street (held) keeps its number readable down to this size.
const COST_FONT_MIN: int = UiMetrics.WORLD_SMALL
const BAR_BAG: float = 56.0
## The "Max: 10" caption under the bar's start.
const MAX_FONT: int = UiMetrics.WORLD
const MAX_OUTLINE: int = 12
const BAR_FONT: int = UiMetrics.ACTION
## The ice water colour: a code-drawn stand-in until the art-director sets a token (docs/ART.md has none yet).
const ICE_WATER: Color = Color(0.36, 0.8, 1.0)
## A full bar's edge: a light ice blue (it was gold; ice water stays blue).
const ICE_WATER_FULL_EDGE: Color = Color(0.72, 0.93, 1.0)
## Dark blue separates the ice water segments and outlines the full track.
const BAR_OUTLINE: int = 2
const BAR_EDGE: Color = Color("123966")
## The empty part of the ice water bar: a flat, opaque dark blue; the inner shadow under its top edge is drawn over it.
const ICE_EMPTY: Color = Color(0.03, 0.14, 0.42)
const TRACK_SHADOW: Color = Color(0.0, 0.04, 0.22, 0.6)
const TRACK_SHADOW_HEIGHT: float = 7.0
const TRACK_BEVEL: Color = Color(0.55, 0.78, 1.0, 0.2)
## The filled part: a light glossy top band over a deeper lower half (Clash Royale's elixir bar), a thin shine line near the top.
const ICE_FILL_TOP: Color = Color(0.45, 0.85, 1.0)
const ICE_FILL_BOTTOM: Color = Color(0.1, 0.5, 0.95)
const FILL_SHINE: Color = Color(1.0, 1.0, 1.0, 0.4)
const FILL_SHINE_Y: float = 4.0
## The polish (owner: the bar was just lines of colour): a glossy see-through band over the top third of every whole segment, a pale
## line along its lower edge, a lit edge beside each divider, and a soft glow trailing the filling segment's leading edge.
const GLOSS_BAND: Color = Color(1.0, 1.0, 1.0, 0.3)
const GLOSS_TOP: float = 2.5
const GLOSS_SHARE: float = 0.36
const GLOSS_SIDE: float = 3.0
const FILL_LOWER_LIGHT: Color = Color(0.7, 0.92, 1.0, 0.28)
const DIVIDER_LIGHT: Color = Color(0.65, 0.88, 1.0, 0.28)
const LEAD_GLOW: Color = Color(1.0, 1.0, 1.0, 0.5)
const LEAD_GLOW_WIDTH: float = 16.0
## The track is a glass tube: its dark blue is a touch lighter toward the bottom, under the shadow of its top edge.
const TRACK_BOTTOM: Color = Color(0.07, 0.26, 0.62)
const ROUND_STEPS: int = 6
## The fill reaches this far under the outline, so the outline (drawn last) hides the seam between them at the rounded corners.
const FILL_BLEED: int = 1
## A piece with rounded corners feathers ALL its edges, which would leave a faint line against its square neighbour: it reaches this far
## over the neighbour (same colour) to cover it.
const SEAM_OVERLAP: float = 1.0
## The segment that is filling shows this much of its share in see-through white (like Clash Royale's elixir bar), with a crisp edge.
const REFILL_WHITE: Color = Color(1.0, 1.0, 1.0, 0.5)
const REFILL_EDGE: Color = Color(1.0, 1.0, 1.0, 0.75)
## Dark partitions stop at the filled or refilling segment and remain on a full bar.
const DIVIDER: Color = BAR_EDGE
const DIVIDER_WIDTH: float = 2.0
## The white outline round the part of the bar the cheapest card in the hand needs.
const MARKER_WIDTH: int = 2
const MARKER_COLOR: Color = Color.WHITE
## A segment that just completed pops: its solid colour grows from this share of the bar's height back to all of it with an eased
## overshoot (clipped inside the track), starting this much lighter and fading to its colour, for this long.
const POP_TIME: float = 0.22
const POP_FROM: float = 0.6
const POP_FLASH: float = 0.6
const POP_BACK: float = 1.70158
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
const _CLEAR: Color = Color(0.0, 0.0, 0.0, 0.0)

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
## What the bar shows now: it catches up with `elixir` (it snaps with Reduce motion and on the first look).
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
## The bar's animation: whether it has looked at the sim yet (the first look snaps), how many segments were whole last time, the pop
## timers of the segments that just completed (seconds left), and how visible the dividers are (1 = shown, 0 = gone while full).
var _bar_synced: bool = false
var _bar_whole: int = 0
var _pops: PackedFloat32Array = PackedFloat32Array()
## What the last `show_state` was told, so an unchanged hand is not redrawn.
var _last_state: Array = []
## The cards as nodes (a shader works on one CanvasItem): NEXT, the four slots, the one under the finger, and what is drawn over them (the
## slots' washes, costs and words; then, over the held card, its gold edge and cost). Children in this order, so the held card is on top.
var _next_face: HandCard
var _faces: Array[HandCard] = []
var _held_face: HandCard
var _overlay: Control
var _held_overlay: Control


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
	_next_face = _new_face("NextCard")
	for slot: int in SLOTS:
		_faces.append(_new_face("Card%d" % slot))
	_overlay = _new_overlay("Over", _draw_over_cards)
	_held_face = _new_face("HeldCard")
	_held_face.shadowed = true
	_held_overlay = _new_overlay("OverHeld", _draw_over_held)


func _new_face(label: String) -> HandCard:
	var face: HandCard = HandCard.new()
	face.name = label
	add_child(face)
	return face


## A see-through layer over the hand that draws with `drawing` (so it comes after the cards, which are children too).
func _new_overlay(label: String, drawing: Callable) -> Control:
	var layer: Control = Control.new()
	layer.name = label
	layer.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	layer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	layer.draw.connect(drawing)
	add_child(layer)
	return layer


## Redraws the hand and puts its card nodes where the state says (everything that changes a card calls this, never `queue_redraw()`).
func _refresh() -> void:
	_sync_faces()
	queue_redraw()
	if _overlay != null:
		_overlay.queue_redraw()
		_held_overlay.queue_redraw()


## The card nodes show what the hand shows: NEXT, each slot (a card, the back of one on its way, nothing while the card is out of it or
## flying in), and the held card at the finger.
func _sync_faces() -> void:
	if _faces.size() < SLOTS:
		return
	var ready_layout: bool = card_rects.size() >= SLOTS
	_next_face.visible = ready_layout and next_def != null
	if _next_face.visible:
		_next_face.show_weapon(next_def)
		_next_face.place(next_rect)
		var filling: float = _next_face.picture_rect().size.y * (1.0 - next_fill) if next_fill < 1.0 else 0.0
		_next_face.set_shades(_CLEAR, CardStyle.FILLING_SHADE, filling, _CLEAR)
	var blocked: bool = not blocked_text.is_empty()
	for slot: int in SLOTS:
		var face: HandCard = _faces[slot]
		var def: WeaponDef = defs[slot]
		if not ready_layout or (def != null and (slot == held_slot or _arrive[slot] > 0.0)):
			face.visible = false
			continue
		var rect: Rect2 = card_rects[slot]
		if def == null:
			face.show_back(slot)
			face.dim = 0.0
		else:
			face.show_weapon(def)
			rect.position.x += deny_offset(slot)
			face.dim = BLOCK_DIM if blocked else 0.0
		face.place(rect)
		face.visible = true
		_shade_picture(slot, face, blocked)
	_held_face.visible = ready_layout and held_slot >= 0 and defs[held_slot] != null
	if _held_face.visible:
		_held_face.show_weapon(defs[held_slot])
		_held_face.place(held_card_rect())


## What lies over the picture card of `slot` (a teks card gets its polygons in `_draw_slot_over` instead): the grey wash of a cost the bar has
## not reached (lighter over the whole picture, dark over the share still missing, from the top) and the red flash of a denied press, all
## from the picture's own silhouette. Drawn under the ice water drop.
func _shade_picture(slot: int, face: HandCard, blocked: bool) -> void:
	if not face.has_picture() or defs[slot] == null:
		face.clear_shades()
		return
	var flash: Color = Color(DENY_FLASH, DENY_FLASH.a * _deny[slot] / DENY_TIME) if _deny[slot] > 0.0 else _CLEAR
	if blocked or pickable[slot] or reached[slot] >= 1.0:
		face.set_shades(_CLEAR, _CLEAR, 0.0, flash)
		return
	var missing: float = face.picture_rect().size.y * (1.0 - clampf(reached[slot], 0.0, 1.0))
	face.set_shades(GREY_REACHED, GREY, missing if missing > 1.0 else 0.0, flash)


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
	var bar_left: float = cards_x + BAR_BAG * HandArt.ICE_DRAW_ASPECT / 2.0
	var bar_right: float = cards_x + 4.0 * card.x + 3.0 * CARD_GAP
	bar_rect = Rect2(Vector2(bar_left, bar_top), Vector2(bar_right - bar_left, BAR_HEIGHT))
	_refresh()


## The card node of `slot` (what is drawn there: a card, a card back, or hidden while the card is out of it), NEXT's, and the one under the
## finger.
func card_node(slot: int) -> HandCard:
	return _faces[slot]


func next_node() -> HandCard:
	return _next_face


func held_node() -> HandCard:
	return _held_face


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
	_sync_bar()
	# the sim reports every frame: redraw only when it said something new (a moving bar redraws itself from `step`)
	var state: Array = [active, uses_elixir, elixir, elixir_max, blocked_text, defs.duplicate(), pickable.duplicate(), reached.duplicate(), notices.duplicate(), next_def, next_fill, wait_fraction, wait_seconds]
	if state != _last_state:
		_last_state = state
		_refresh()


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
	if step(delta):
		_refresh()


## Advances every timer by `delta` seconds (the shown bar, the pops, the dividers, a shake, a card arriving). Returns whether anything
## on screen changed, so the hand is redrawn only while something moves and sleeps when it is still.
func step(delta: float) -> bool:
	var moving: bool = _step_bar(delta)
	for slot: int in SLOTS:
		if _deny[slot] > 0.0 or _arrive[slot] > 0.0:
			moving = true
		_deny[slot] = maxf(0.0, _deny[slot] - delta)
		_arrive[slot] = maxf(0.0, _arrive[slot] - delta)
	return moving


# ---- the ice water bar's motion -------------------------------------------------

func bar_target() -> float:
	return elixir if uses_elixir else 0.0


func bar_segments() -> int:
	return maxi(roundi(elixir_max), 1)


## The first look at the sim shows the bar as it is (no fill-up, no pops); later changes animate.
func _sync_bar() -> void:
	if _bar_synced:
		return
	_bar_synced = true
	shown_elixir = bar_target()
	_bar_whole = _whole_segments(shown_elixir)


func _whole_segments(value: float) -> int:
	return clampi(floori(value + 0.0001), 0, bar_segments())


## The shown bar is full (the dividers let go of it).
func _bar_full() -> bool:
	return uses_elixir and shown_elixir >= elixir_max - 0.001


func _step_bar(delta: float) -> bool:
	_sync_bar()
	var moving: bool = false
	var before: float = shown_elixir
	shown_elixir = bar_target() if Settings.reduced_motion else move_toward(shown_elixir, bar_target(), BAR_CATCH_UP * delta)
	if shown_elixir != before:
		moving = true
	# a segment completing pops; a bar that jumps down (a card was spent) pops nothing and cancels the pops of the segments it lost
	var whole: int = _whole_segments(shown_elixir)
	if _pops.size() < bar_segments():
		_pops.resize(bar_segments())
	if whole > _bar_whole and uses_elixir and not Settings.reduced_motion:
		for segment: int in range(_bar_whole, whole):
			_pops[segment] = POP_TIME
	for segment: int in range(mini(whole, _pops.size()), _pops.size()):
		_pops[segment] = 0.0
	_bar_whole = whole
	for segment: int in _pops.size():
		if _pops[segment] > 0.0:
			moving = true
			_pops[segment] = maxf(0.0, _pops[segment] - delta)
	return moving


## Partitions remain visible even at full ice water.
func divider_visibility() -> float:
	return 1.0


## How far the pop of `segment` is (0 just completed, 1 done), or -1 when it is not popping.
func pop_progress(segment: int) -> float:
	if segment < 0 or segment >= _pops.size() or _pops[segment] <= 0.0:
		return -1.0
	return 1.0 - _pops[segment] / POP_TIME


## Ease-out-back: runs a little past 1 and settles on it.
static func ease_out_back(t: float) -> float:
	var x: float = clampf(t, 0.0, 1.0) - 1.0
	return 1.0 + (POP_BACK + 1.0) * x * x * x + POP_BACK * x * x


## What the cheapest card in the hand costs (0 when the hand is empty); the bar marks it.
func cheapest_cost() -> int:
	var cheapest: int = 0
	var found: bool = false
	for def: WeaponDef in defs:
		if def != null and (not found or def.elixir_cost < cheapest):
			cheapest = def.elixir_cost
			found = true
	return cheapest


## The x where segment `index` starts on the bar (0 = the first, `bar_segments()` = the end of the last), inside its outline.
func boundary_x(index: int) -> float:
	var inner: Rect2 = bar_rect.grow(-float(BAR_OUTLINE))
	return inner.position.x + inner.size.x * float(index) / float(bar_segments())


## The white outline round the part of the bar the cheapest card needs: from the bar's start to the end of its cost in segments (its line
## is centred on that segment boundary); empty when elixir is off or the hand is.
func marker_rect() -> Rect2:
	var cost: int = mini(cheapest_cost(), bar_segments())
	if not uses_elixir or cost <= 0 or bar_rect.size.x <= 0.0:
		return Rect2()
	# a cost of the whole bar is the bar's own outline (its line would sit a pixel inside it otherwise)
	var right: float = bar_rect.end.x if cost >= bar_segments() else boundary_x(cost) + float(MARKER_WIDTH) / 2.0
	return Rect2(bar_rect.position, Vector2(right - bar_rect.position.x, bar_rect.size.y))


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
	_refresh()


func _move(viewport_position: Vector2) -> void:
	if held_slot < 0:
		return
	finger = _local(viewport_position)
	on_card = card_rects[held_slot].grow(CARD_GAP / 2.0).has_point(finger)
	over_hand = Rect2(Vector2.ZERO, size).has_point(finger)
	card_moved.emit(held_slot, viewport_position, on_card, over_hand)
	_refresh()


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
	_refresh()


## A card that is not ready was pressed: it shakes and flashes (only flashes with Reduce motion), and the phone buzzes once.
func deny(slot: int) -> void:
	_deny[slot] = DENY_TIME
	Settings.vibrate(DENY_BUZZ_MS)
	card_denied.emit(slot)
	_refresh()


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

## The hand's own drawing, under its cards: the word over NEXT, the hollow of a slot a card is out of (and of an empty NEXT), and the ice
## water bar. The cards are nodes drawn after this; what lies over them is `_draw_over_cards()`.
func _draw() -> void:
	_draw_next_label()
	if next_def == null:
		CardStyle.draw_empty(self, next_rect)
	for slot: int in card_rects.size():
		if defs[slot] != null and (slot == held_slot or _arrive[slot] > 0.0):
			CardStyle.draw_empty(self, card_rects[slot])
	_draw_bar()


## The word over the next card.
func next_label() -> String:
	return UiText.t("Next:")


func _draw_next_label() -> void:
	var font: Font = KalyeahTheme.ACTION_FONT
	var label: String = next_label()
	var width: float = font.get_string_size(label, HORIZONTAL_ALIGNMENT_LEFT, -1.0, NEXT_FONT).x
	var at: Vector2 = Vector2(next_rect.get_center().x - width / 2.0, next_rect.position.y - 8.0)
	draw_string_outline(font, at, label, HORIZONTAL_ALIGNMENT_LEFT, -1.0, NEXT_FONT, 7, Color.BLACK)
	draw_string(font, at, label, HORIZONTAL_ALIGNMENT_LEFT, -1.0, NEXT_FONT, NEXT_COLOR)


## Over the cards: the shade of NEXT while it is still being shuffled in, and on every slot the card's wash (a cost the bar has not
## reached), its ice water drop, its word, its red flash when denied, or the ring of a card on its way; then the word too wide for one card.
func _draw_over_cards() -> void:
	if card_rects.size() < SLOTS:
		return
	if next_def != null and next_fill < 1.0 and not _next_face.has_picture():
		CardStyle.shade_top(_overlay, _next_face.outline(), next_rect.position, next_rect.size.y * (1.0 - next_fill), CardStyle.FILLING_SHADE)
	for slot: int in SLOTS:
		_draw_slot_over(slot)
	if not blocked_text.is_empty() and _tag_too_wide(blocked_text):
		_draw_tag(_overlay, row_rect().get_center(), UiText.t(blocked_text))


func _draw_slot_over(slot: int) -> void:
	var rect: Rect2 = card_rects[slot]
	var def: WeaponDef = defs[slot]
	if def == null:
		CardStyle.draw_back_ring(_overlay, rect, wait_fraction, wait_seconds)
		return
	if slot == held_slot or _arrive[slot] > 0.0:
		return
	rect.position.x += deny_offset(slot)
	var face: HandCard = _faces[slot]
	var outline: PackedVector2Array = face.outline()
	var blocked: bool = not blocked_text.is_empty()
	# a picture card's wash and flash are its own silhouette, drawn by the card itself (`_shade_picture`): the polygons are for teks cards
	if not face.has_picture() and not blocked and not pickable[slot] and reached[slot] < 1.0:
		_draw_grey(outline, rect, reached[slot])
	_draw_cost(_overlay, art_rect(face, rect), def, pickable[slot] or reached[slot] >= 1.0)
	var tag: String = blocked_text if blocked else notices[slot]
	# a word wider than a card (CAPTURING) is said once across the hand, not four times over one another
	if not tag.is_empty() and not _tag_too_wide(tag):
		_draw_tag(_overlay, rect.get_center(), UiText.t(tag))
	if _deny[slot] > 0.0 and not face.has_picture():
		CardStyle.shade_all(_overlay, outline, rect.position, Color(DENY_FLASH, DENY_FLASH.a * _deny[slot] / DENY_TIME))


## Over the card under the finger: its gold edge (the lift stays clear on a flat, worn card) and its cost.
func _draw_over_held() -> void:
	if held_slot < 0 or defs[held_slot] == null or card_rects.size() < SLOTS:
		return
	var rect: Rect2 = held_card_rect()
	# a picture card's lift cue (shadow and gold edge from its silhouette) is behind it, drawn by the card itself
	if not _held_face.has_picture():
		CardStyle.draw_lift_edge(_held_overlay, _held_face.outline(), rect.position)
	_draw_cost(_held_overlay, art_rect(_held_face, rect), defs[held_slot], true)


## Greys a card the bar has not reached (cut to its worn corners): dark over the share still missing (from the top), lighter over the share
## already there.
func _draw_grey(outline: PackedVector2Array, rect: Rect2, share: float) -> void:
	CardStyle.shade_all(_overlay, outline, rect.position, GREY_REACHED)
	var missing: float = rect.size.y * (1.0 - clampf(share, 0.0, 1.0))
	if missing > 1.0:
		CardStyle.shade_top(_overlay, outline, rect.position, missing, GREY)


## Where the card drawn in `rect` really is: the rect itself for a teks card, the fitted picture for a picture card (what the drop sits on).
static func art_rect(face: HandCard, rect: Rect2) -> Rect2:
	return CardStyle.picture_rect(rect, face.picture) if face.has_picture() else rect


## Where the cost drop sits on the card `rect`: all of it inside the card, in the middle, its bottom edge a little above the card's
## bottom edge (the drop covers the least of the card's art there).
static func cost_drop_rect(rect: Rect2) -> Rect2:
	var width: float = rect.size.x * COST_SHARE
	var size: Vector2 = Vector2(width, width / HandArt.ICE_DRAW_ASPECT)
	return Rect2(Vector2(rect.get_center().x - size.x / 2.0, rect.end.y - rect.size.x * COST_PAD_SHARE - size.y), size)


## The card's cost on an ice water drop inside its bottom edge, in the middle (elixir only); a cost the bar has not reached is drawn in grey.
## A card shrunk over the street shrinks its drop with it (its number a little less, to stay readable).
func _draw_cost(canvas: CanvasItem, rect: Rect2, def: WeaponDef, affordable: bool) -> void:
	if not uses_elixir:
		return
	var drop: Rect2 = cost_drop_rect(rect)
	var share: float = rect.size.x / card_rects[0].size.x if not card_rects.is_empty() else 1.0
	var font_size: int = clampi(roundi(float(COST_FONT) * share), COST_FONT_MIN, COST_FONT)
	HandArt.draw_ice(canvas, drop.get_center(), drop.size.y, str(def.elixir_cost), font_size, affordable)


## The pill for `tag` would be wider than one card.
func _tag_too_wide(tag: String) -> bool:
	if card_rects.is_empty():
		return false
	var width: float = KalyeahTheme.ACTION_FONT.get_string_size(UiText.t(tag), HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.CAPTION).x + 20.0
	return width > card_rects[0].size.x


func _draw_tag(canvas: CanvasItem, at: Vector2, text: String) -> void:
	var font: Font = KalyeahTheme.ACTION_FONT
	var width: float = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.CAPTION).x + 20.0
	var pill: StyleBoxFlat = StyleBoxFlat.new()
	pill.bg_color = Color(0.1, 0.08, 0.14, 0.92)
	pill.border_color = TAG_COLOR
	pill.set_border_width_all(2)
	pill.set_corner_radius_all(UiSkin.round_radius(TAG_HEIGHT))
	canvas.draw_style_box(pill, Rect2(at - Vector2(width / 2.0, TAG_HEIGHT / 2.0), Vector2(width, TAG_HEIGHT)))
	canvas.draw_string_outline(font, at + Vector2(-width / 2.0 + 10.0, UiMetrics.CAPTION * 0.36), text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.CAPTION, 5, Icons.OUTLINE)
	canvas.draw_string(font, at + Vector2(-width / 2.0 + 10.0, UiMetrics.CAPTION * 0.36), text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.CAPTION, TAG_COLOR)


## The ice water bar, bottom to top: the dark track with an inner shadow, the fill (a glossy light band over a deeper lower half, a
## segment that just completed popping), the white progress of the segment being filled, the pale dividers (gone while it is full), the
## thin outline, the white marker of the cheapest card, and the whole points on an ice water drop at its left end.
func _draw_bar() -> void:
	if not uses_elixir or bar_rect.size.x <= 0.0:
		return
	var radius: int = UiSkin.bar_radius(bar_rect.size.y)
	var track: StyleBoxFlat = StyleBoxFlat.new()
	track.bg_color = ICE_EMPTY
	track.set_corner_radius_all(radius)
	track.anti_aliasing = true
	draw_style_box(track, bar_rect)
	_draw_track_glass(radius)
	_draw_track_shading(radius)
	_draw_fill(radius)
	_draw_dividers()
	_draw_edge(radius)
	# like Clash Royale: the drop overlaps the bar's start, the whole points in big letters just right of it (over the bar's start), and the
	# most you can hold in small letters under them
	var bag: Vector2 = Vector2(bar_rect.position.x, bar_rect.get_center().y - BAR_BAG * 0.12)
	var font: Font = KalyeahTheme.ACTION_FONT
	var number_at: Vector2 = Vector2(bag.x + BAR_BAG * HandArt.ICE_DRAW_ASPECT / 2.0 + 6.0, bar_rect.get_center().y - BAR_HEIGHT * 0.1)
	var points: String = str(floori(elixir + 0.001))
	var max_text: String = UiText.f("Max: %d", [roundi(elixir_max)])
	_draw_marker(radius)
	HandArt.draw_ice(self, bag, BAR_BAG, "", BAR_FONT)
	RoyaleStyle.lettered(self, font, points, number_at, BAR_FONT)
	# vertically centred on the bar's bottom outline; its outline is thicker than the chrome's so the bar's edge does not show through the
	# gap after "Max:" like a minus sign
	var caption_at: Vector2 = Vector2(number_at.x, max_caption_baseline(bar_rect))
	draw_string_outline(font, caption_at, max_text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, MAX_FONT, MAX_OUTLINE, RoyaleStyle.NAVY)
	draw_string(font, caption_at, max_text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, MAX_FONT, Color.WHITE)


## The white outline of the cheapest card (`marker_rect()`), drawn over the track and fill and under the drop and its letters. Where the
## letters overlap it, the outline still encloses the entire required span.
func _draw_marker(radius: int) -> void:
	var marker: Rect2 = marker_rect()
	if marker.size.x <= 0.0:
		return
	var line: StyleBoxFlat = StyleBoxFlat.new()
	line.draw_center = false
	line.border_color = MARKER_COLOR
	line.set_border_width_all(MARKER_WIDTH)
	line.anti_aliasing = true
	line.corner_radius_top_right = radius
	line.corner_radius_bottom_right = radius
	line.corner_radius_top_left = radius
	line.corner_radius_bottom_left = radius
	draw_style_box(line, marker)


## The empty track as a glass tube: its dark blue turns lighter toward the bottom (inside the rounded ends).
func _draw_track_glass(radius: int) -> void:
	var inner: Rect2 = bar_rect.grow(-float(BAR_OUTLINE))
	var points: PackedVector2Array = rounded_points(inner, float(maxi(radius - BAR_OUTLINE, 1)), true, true)
	var colors: PackedColorArray = PackedColorArray()
	for point: Vector2 in points:
		colors.append(ICE_EMPTY.lerp(TRACK_BOTTOM, clampf((point.y - inner.position.y) / inner.size.y, 0.0, 1.0)))
	draw_polygon(points, colors)


## The track's depth: a soft shadow under its top edge and a faint light line over its bottom (both stop short of the rounded corners).
func _draw_track_shading(radius: int) -> void:
	var left: float = bar_rect.position.x + float(radius)
	var right: float = bar_rect.end.x - float(radius)
	var top: float = bar_rect.position.y + float(BAR_OUTLINE)
	var clear: Color = Color(TRACK_SHADOW, 0.0)
	draw_polygon(PackedVector2Array([Vector2(left, top), Vector2(right, top), Vector2(right, top + TRACK_SHADOW_HEIGHT), Vector2(left, top + TRACK_SHADOW_HEIGHT)]), PackedColorArray([TRACK_SHADOW, TRACK_SHADOW, clear, clear]))
	var bevel: float = bar_rect.end.y - float(BAR_OUTLINE) - 0.5
	draw_line(Vector2(left, bevel), Vector2(right, bevel), TRACK_BEVEL, 1.0, true)


func _draw_fill(radius: int) -> void:
	var segments: int = bar_segments()
	var fill: Rect2 = bar_rect.grow(-float(BAR_OUTLINE - FILL_BLEED))
	# concentric with the outline's inner curve, so no corner of the fill pokes out or leaves a gap
	var fill_radius: int = maxi(radius - (BAR_OUTLINE - FILL_BLEED), 1)
	var fills: PackedFloat32Array = segment_fills(shown_elixir, segments)
	for segment: int in segments:
		var left: float = fill.position.x if segment == 0 else boundary_x(segment)
		var right: float = fill.end.x if segment == segments - 1 else boundary_x(segment + 1)
		var first: bool = segment == 0
		var last: bool = segment == segments - 1
		var cell: Rect2 = Rect2(Vector2(left, fill.position.y), Vector2(right - left, fill.size.y))
		if segments > 1 and first:
			cell.size.x += SEAM_OVERLAP
		elif segments > 1 and last:
			cell = Rect2(cell.position - Vector2(SEAM_OVERLAP, 0.0), cell.size + Vector2(SEAM_OVERLAP, 0.0))
		if fills[segment] >= 1.0:
			_draw_solid_cell(cell, fill_radius, first, last, pop_progress(segment))
		elif fills[segment] > 0.0:
			# the segment being filled: a see-through white that grows smoothly (the shown bar is eased), with a crisp leading edge
			var width: float = cell.size.x * fills[segment]
			_draw_piece(Rect2(cell.position, Vector2(width, cell.size.y)), REFILL_WHITE, fill_radius, first, false, true, true)
			var x: float = cell.position.x + width
			draw_line(Vector2(x, cell.position.y + 2.0), Vector2(x, cell.end.y - 2.0), REFILL_EDGE, 2.0, true)
			# a soft glow trailing the leading edge
			var glow_from: float = maxf(x - LEAD_GLOW_WIDTH, cell.position.x + (float(fill_radius) if first else 0.0))
			var clear: Color = Color(LEAD_GLOW, 0.0)
			draw_polygon(PackedVector2Array([Vector2(glow_from, cell.position.y + 3.0), Vector2(x, cell.position.y + 3.0), Vector2(x, cell.end.y - 3.0), Vector2(glow_from, cell.end.y - 3.0)]), PackedColorArray([clear, LEAD_GLOW, LEAD_GLOW, clear]))


## One finished segment: a smooth light-to-deep gradient with a glossy band over its top third and a pale line along its lower edge; while it
## pops (`pop` 0 to 1) it grows from the middle with an eased overshoot, clipped to the track, and starts lighter.
func _draw_solid_cell(cell: Rect2, radius: int, first: bool, last: bool, pop: float) -> void:
	var rect: Rect2 = cell
	var top: Color = ICE_FILL_TOP
	var bottom: Color = ICE_FILL_BOTTOM
	if pop >= 0.0:
		var height: float = minf(cell.size.y * lerpf(POP_FROM, 1.0, ease_out_back(pop)), cell.size.y)
		rect = Rect2(Vector2(cell.position.x, cell.get_center().y - height / 2.0), Vector2(cell.size.x, height))
		var flash: float = POP_FLASH * (1.0 - pop)
		top = top.lerp(Color.WHITE, flash)
		bottom = bottom.lerp(Color.WHITE, flash)
	var points: PackedVector2Array = rounded_points(rect, float(radius), first, last)
	var colors: PackedColorArray = PackedColorArray()
	for point: Vector2 in points:
		colors.append(top.lerp(bottom, clampf((point.y - rect.position.y) / rect.size.y, 0.0, 1.0)))
	draw_polygon(points, colors)
	# the glossy band: see-through white, a little in from the sides (more at a rounded end of the bar), rounded itself
	var left_in: float = float(radius) * 0.7 if first else GLOSS_SIDE
	var right_in: float = float(radius) * 0.7 if last else GLOSS_SIDE
	var band: Rect2 = Rect2(rect.position + Vector2(left_in, GLOSS_TOP * rect.size.y / cell.size.y), Vector2(rect.size.x - left_in - right_in, rect.size.y * GLOSS_SHARE))
	var gloss: StyleBoxFlat = StyleBoxFlat.new()
	gloss.bg_color = GLOSS_BAND
	gloss.set_corner_radius_all(UiSkin.round_radius(band.size.y))
	gloss.anti_aliasing = true
	draw_style_box(gloss, band)
	var shine_y: float = rect.position.y + FILL_SHINE_Y * rect.size.y / cell.size.y
	var from_x: float = rect.position.x + (float(radius) if first else 0.0)
	var to_x: float = rect.end.x - (float(radius) if last else 0.0)
	draw_line(Vector2(from_x, shine_y), Vector2(to_x, shine_y), FILL_SHINE, 1.5, true)
	# a pale line along the lower edge, like light through the water
	var low_y: float = rect.end.y - 3.0
	draw_line(Vector2(from_x, low_y), Vector2(to_x, low_y), FILL_LOWER_LIGHT, 1.5, true)


## The outline of `rect` as a polygon: square corners, except the two at the left when `left` and the two at the right when `right`, which are
## rounded to `radius` (the bar's own ends, never the seams between segments).
static func rounded_points(rect: Rect2, radius: float, left: bool, right: bool) -> PackedVector2Array:
	var points: PackedVector2Array = PackedVector2Array()
	var r: float = minf(radius, minf(rect.size.x, rect.size.y) / 2.0)
	# clockwise from the top left: top left, top right, bottom right, bottom left
	var corners: Array[Dictionary] = [
		{"at": rect.position, "round": left, "from": PI, "dir": Vector2(1.0, 1.0)},
		{"at": Vector2(rect.end.x, rect.position.y), "round": right, "from": -PI / 2.0, "dir": Vector2(-1.0, 1.0)},
		{"at": rect.end, "round": right, "from": 0.0, "dir": Vector2(-1.0, -1.0)},
		{"at": Vector2(rect.position.x, rect.end.y), "round": left, "from": PI / 2.0, "dir": Vector2(1.0, -1.0)},
	]
	for corner: Dictionary in corners:
		var at: Vector2 = corner["at"] as Vector2
		if not (corner["round"] as bool):
			points.append(at)
			continue
		var direction: Vector2 = corner["dir"] as Vector2
		var centre: Vector2 = at + direction * r
		for step: int in ROUND_STEPS + 1:
			var angle: float = (corner["from"] as float) + (PI / 2.0) * float(step) / float(ROUND_STEPS)
			points.append(centre + Vector2(cos(angle), sin(angle)) * r)
	return points


## A flat piece of the bar; `left`/`right` and `top`/`bottom` pick which of its corners are rounded (the bar's own ends, never the seams).
func _draw_piece(rect: Rect2, color: Color, radius: int, left: bool, right: bool, top: bool, bottom: bool) -> void:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = color
	if left and top:
		box.corner_radius_top_left = radius
	if right and top:
		box.corner_radius_top_right = radius
	if left and bottom:
		box.corner_radius_bottom_left = radius
	if right and bottom:
		box.corner_radius_bottom_right = radius
	box.anti_aliasing = true
	draw_style_box(box, rect)


func _draw_dividers() -> void:
	var visible_share: float = divider_visibility()
	if visible_share <= 0.0:
		return
	var inner: Rect2 = bar_rect.grow(-float(BAR_OUTLINE))
	var color: Color = Color(DIVIDER, DIVIDER.a * visible_share)
	for x: float in divider_positions():
		draw_line(Vector2(x, inner.position.y), Vector2(x, inner.end.y), color, DIVIDER_WIDTH, true)
		draw_line(Vector2(x + DIVIDER_WIDTH, inner.position.y + 2.0), Vector2(x + DIVIDER_WIDTH, inner.end.y - 2.0), Color(DIVIDER_LIGHT, DIVIDER_LIGHT.a * visible_share), 1.0, true)


func divider_positions() -> PackedFloat32Array:
	var positions: PackedFloat32Array = []
	for segment: int in range(1, mini(floori(shown_elixir + 0.0001), bar_segments() - 1) + 1):
		positions.append(boundary_x(segment))
	return positions


## The track keeps its dark-blue outline at every fill level.
func _draw_edge(radius: int) -> void:
	var edge: StyleBoxFlat = StyleBoxFlat.new()
	edge.draw_center = false
	edge.border_color = BAR_EDGE
	edge.set_border_width_all(BAR_OUTLINE)
	edge.set_corner_radius_all(radius)
	edge.anti_aliasing = true
	draw_style_box(edge, bar_rect)


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
