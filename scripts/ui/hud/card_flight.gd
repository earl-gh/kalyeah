class_name CardFlight
extends Control
## The card you just used flying from the hand into the shuffler (view only, no touches): it shrinks and slides along a short arc in
## `TIME` seconds, then removes itself. Not played with Reduce motion. The card is the same card the hand shows (`HandCard`: the weapon's picture
## when the owner made one, else the worn teks card with the same wear: it is drawn from the weapon).

const TIME: float = 0.32
const ARC: float = 30.0

var def: WeaponDef
var from_rect: Rect2
var to_rect: Rect2
var _age: float = 0.0
var _card: HandCard


## Starts a flight on `parent` (the HUD) from `from` to `to`, both in the parent's coordinates; returns null without motion.
static func fly(parent: Control, card: WeaponDef, from: Rect2, to: Rect2) -> CardFlight:
	if Settings.reduced_motion or card == null:
		return null
	var flight: CardFlight = CardFlight.new()
	flight.def = card
	flight.from_rect = from
	flight.to_rect = to
	flight.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(flight)
	return flight


func _ready() -> void:
	_card = HandCard.new()
	_card.name = "FlyingCard"
	add_child(_card)
	_card.show_weapon(def)
	_place()


func _process(delta: float) -> void:
	_age += delta
	if _age >= TIME:
		queue_free()
		return
	_place()


## Where the card is `t` (0 to 1) of the way, in the flight's own coordinates (it covers the HUD from its top-left).
func rect_at(t: float) -> Rect2:
	var eased: float = t * t * (3.0 - 2.0 * t)
	var rect: Rect2 = Rect2(from_rect.position.lerp(to_rect.position, eased), from_rect.size.lerp(to_rect.size, eased))
	rect.position.y -= ARC * sin(eased * PI)
	return rect


## The card of the weapon at its place now, a little darker as it goes.
func _place() -> void:
	var t: float = clampf(_age / TIME, 0.0, 1.0)
	_card.place(rect_at(t))
	_card.dim = 0.25 * t
	_card.visible = true
