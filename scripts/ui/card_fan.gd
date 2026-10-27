class_name CardFan
extends Control
## Three bare card pictures fanned like the Cards icon of the bottom bar (`assets/ui/nav/nav_cards.webp`): the front card
## on the left tilted about 15 degrees to the left, the others behind it to the right, each peeking out a little further
## and tilted the other way, overlapping, the front one drawn last. It is built from rotated `TextureRect`s, so no new
## picture is needed; the pictures are the owner's full cards, drawn as made with no plate or crop behind them.
##
## The control is exactly the box the fan fills (rotation included): `custom_minimum_size` and `size` are that box, so
## whoever places a fan places what the eye sees.

## The tilt of the cards, front first, in degrees (negative = counter-clockwise, as Godot turns).
const TILTS: Array[float] = [-15.0, 3.0, 15.0]
## How far each card's centre sits right of the front card's centre, in widths of the front card.
const PEEKS: Array[float] = [0.0, 0.5, 0.86]
## How far each card's centre sits below the front card's centre, in heights of a card (the last one sits lower, as in the icon).
const DROPS: Array[float] = [0.0, 0.0, 0.07]

## The cards, front first.
var cards: Array[TextureRect] = []


## Builds the fan from `pictures` (front first, at most three), each `card_height` tall and as wide as its own picture is.
func build(pictures: Array[Texture2D], card_height: float) -> void:
	for old: TextureRect in cards:
		old.queue_free()
		remove_child(old)
	cards.clear()
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	var count: int = mini(pictures.size(), TILTS.size())
	if count == 0:
		custom_minimum_size = Vector2.ZERO
		size = Vector2.ZERO
		return
	var front_width: float = _width_of(pictures[0], card_height)
	var rects: Array[Rect2] = []
	var bounds: Rect2 = Rect2()
	for i: int in count:
		var card: TextureRect = TextureRect.new()
		card.name = "Card%d" % i
		card.texture = pictures[i]
		card.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		card.stretch_mode = TextureRect.STRETCH_SCALE
		card.mouse_filter = Control.MOUSE_FILTER_IGNORE
		var card_size: Vector2 = Vector2(_width_of(pictures[i], card_height), card_height)
		card.size = card_size
		card.pivot_offset = card_size / 2.0
		card.rotation = deg_to_rad(TILTS[i])
		var centre: Vector2 = Vector2(PEEKS[i] * front_width, DROPS[i] * card_height)
		card.position = centre - card_size / 2.0
		cards.append(card)
		var turned: Rect2 = _turned_rect(centre, card_size, card.rotation)
		bounds = turned if i == 0 else bounds.merge(turned)
	# the back card goes in first: the front card is the last child, so it is drawn on top
	for i: int in range(count - 1, -1, -1):
		var card: TextureRect = cards[i]
		card.position -= bounds.position
		add_child(card)
	custom_minimum_size = bounds.size
	size = bounds.size


## The box (in this control's own coordinates) that card `index` really covers once it is turned.
func card_box(index: int) -> Rect2:
	var card: TextureRect = cards[index]
	return _turned_rect(card.position + card.size / 2.0, card.size, card.rotation)


static func _width_of(picture: Texture2D, card_height: float) -> float:
	if picture == null or picture.get_height() <= 0:
		return card_height * 0.7
	return card_height * float(picture.get_width()) / float(picture.get_height())


## The smallest box round a `card_size` rectangle centred on `centre` and turned by `angle` radians.
static func _turned_rect(centre: Vector2, card_size: Vector2, angle: float) -> Rect2:
	var half: Vector2 = card_size / 2.0
	var box: Rect2 = Rect2(centre, Vector2.ZERO)
	for corner: Vector2 in [Vector2(-half.x, -half.y), Vector2(half.x, -half.y), Vector2(half.x, half.y), Vector2(-half.x, half.y)]:
		box = box.expand(centre + corner.rotated(angle))
	return box


## Where the front card's lower left corner is (in this control's own coordinates): the fan's box is empty below and left of it,
## so text can run in under the fan there.
func lower_left() -> Vector2:
	if cards.is_empty():
		return Vector2.ZERO
	var front: TextureRect = cards[0]
	return front.position + front.size / 2.0 + Vector2(-front.size.x / 2.0, front.size.y / 2.0).rotated(front.rotation)
