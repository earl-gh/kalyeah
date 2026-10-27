class_name CardStyle
extends RefCounted
## How a weapon card of the hand looks (owner: like the old Filipino teks cards of the mode screen): a flat, straight card printed in the
## weapon's kind colour with its painted skill icon on it, worn like old cardstock by the shaders the mode card used (`HandCard`, which is a
## `CardFace`): no black outline, no gloss band. This holds what the hand draws round those cards in code (the empty slot, the back of a
## card on its way, the gold edge of a lifted card, the shades over a card that cannot be used) and the numbers of the print. The ice
## water cost sits on the card's bottom edge, drawn by the hand over the card.
##
## A weapon the owner has made a full-card picture for (`assets/ui/skill_cards/<weapon_id>.webp`, `picture`) is drawn AS that picture instead:
## bare, as made, with nothing printed behind it. Its outline is the picture's own torn edge (a transparent gap in each corner), so what
## lies over it (washes, flash, the gold edge of a lift) is drawn from the picture's own alpha, never as a rectangle.

## The printed surface is the kind colour (`WeaponPickScreen.kind_color`, so Attack, Control, Block and Heal read at a glance) darkened by
## this much, so the painted icon stands out from it.
const GROUND_DARKEN: float = 0.3
## The painted icon sits this far in from the card's edge, as a share of the card's width (9 px of a 123 px card).
const ART_EDGE_SHARE: float = 0.073
## The print of the back of a card on its way: the dark red brown of old cardboard.
const BACK_GROUND: Color = Color(0.42, 0.17, 0.12)
## A card lifted by a finger gets this edge round its worn outline (the lift stays clear on a flat card); a card shrunk over the street
## keeps the same line.
const LIFT_EDGE: float = 3.0
## Where the owner's full-card pictures live: a weapon with a file here is drawn as that picture in the match, one without keeps the teks
## card (adding a picture is adding the file and its `.import`, nothing else).
const PICTURE_PATH: String = "res://assets/ui/skill_cards/%s.webp"
## The soft shadow under a lifted picture card: this many copies of its silhouette, each one a little bigger and this much lower, each
## this dark (together about a third as dark in the middle).
const PICTURE_SHADOW_LAYERS: int = 3
const PICTURE_SHADOW_DROP: float = 7.0
const PICTURE_SHADOW_GROW: float = 2.0
const PICTURE_SHADOW_ALPHA: float = 0.13
## The gold edge of a lifted picture card: its silhouette in gold at this many offsets round it, LIFT_EDGE away.
const PICTURE_EDGE_STEPS: int = 8
## The wear seeds run from 1 up to this far above it (a different one for every weapon, so the four cards of a hand wear differently).
const SEED_SPAN: float = 977.0
## The empty slot of a hand (a card is held out of it, or on its way in): a faint hollow with a tan edge, like the dent a card leaves in
## the tray.
const HOLLOW: Color = Color(0.0, 0.0, 0.0, 0.25)
const HOLLOW_EDGE: Color = Color(0.73, 0.63, 0.47, 0.4)
const HOLLOW_EDGE_WIDTH: float = 3.0
## The ring of a card on its way: how thick it is and how big against the card's width, and its seconds.
const RING_WIDTH: float = 8.0
const RING_SHARE: float = 0.26
const RING_TRACK: Color = Color(1.0, 1.0, 1.0, 0.15)
## What darkens the part of NEXT the shuffler has not finished (it brightens from the bottom up as the card is shuffled in).
const FILLING_SHADE: Color = Color(0.0, 0.0, 0.0, 0.55)


## The pictures looked up so far (null: the weapon has none).
static var _pictures: Dictionary[StringName, Texture2D] = {}


## The owner's full-card picture of the weapon `id` (transparent, trimmed to its frame), or null when there is none yet.
static func picture(id: StringName) -> Texture2D:
	if not _pictures.has(id):
		var path: String = PICTURE_PATH % String(id)
		_pictures[id] = load(path) as Texture2D if ResourceLoader.exists(path) else null
	return _pictures[id]


## Where a picture card of `texture` is drawn in the slot `rect`: as big as fits with its own proportions (never stretched), centred.
static func picture_rect(rect: Rect2, texture: Texture2D) -> Rect2:
	if texture == null or texture.get_height() <= 0:
		return rect
	var shown: float = minf(rect.size.x / float(texture.get_width()), rect.size.y / float(texture.get_height()))
	var size: Vector2 = texture.get_size() * shown
	return Rect2(rect.get_center() - size / 2.0, size)


## The colour a weapon card of `kind` is printed in.
static func ground(kind: WeaponDef.Kind) -> Color:
	return WeaponPickScreen.kind_color(kind).darkened(GROUND_DARKEN)


## The wear seed of a weapon's card: the same wherever that card is (hand, NEXT, held, flying), a different one for every weapon.
static func wear_seed(id: StringName) -> float:
	return 1.0 + float(String(id).hash() % int(SEED_SPAN))


## The shape of a card of `size` with the given corner radii, in the card's own pixels (shared with the shaders' cut).
static func outline(size: Vector2, radii: Vector4) -> PackedVector2Array:
	return CardShape.outline_corners(size, radii)


## `outline` moved to `at` and closed, for a line round the card.
static func closed(points: PackedVector2Array, at: Vector2) -> PackedVector2Array:
	var line: PackedVector2Array = PackedVector2Array()
	for point: Vector2 in points:
		line.append(point + at)
	if not points.is_empty():
		line.append(points[0] + at)
	return line


## The gold edge of a lifted card, on its own worn outline.
static func draw_lift_edge(canvas: CanvasItem, points: PackedVector2Array, at: Vector2) -> void:
	canvas.draw_polyline(closed(points, at), RoyaleStyle.GOLD, LIFT_EDGE, true)


## Colours the part of a card (its `points` at `at`) above `covered` px from its top in `color`: the wash over a card the ice water has
## not reached and the shade of a card that is still being shuffled in. Cut to the card's own corners.
static func shade_top(canvas: CanvasItem, points: PackedVector2Array, at: Vector2, covered: float, color: Color) -> void:
	if covered <= 0.0 or points.is_empty():
		return
	var box: Rect2 = Rect2(Vector2.ZERO, Vector2.ZERO)
	for point: Vector2 in points:
		box = box.expand(point)
	var band: PackedVector2Array = PackedVector2Array([Vector2(box.position.x - 1.0, box.position.y - 1.0), Vector2(box.end.x + 1.0, box.position.y - 1.0), Vector2(box.end.x + 1.0, box.position.y + covered), Vector2(box.position.x - 1.0, box.position.y + covered)])
	for piece: PackedVector2Array in Geometry2D.intersect_polygons(points, band):
		var moved: PackedVector2Array = PackedVector2Array()
		for point: Vector2 in piece:
			moved.append(point + at)
		canvas.draw_colored_polygon(moved, color)


## Colours the whole card (its `points` at `at`) in `color`.
static func shade_all(canvas: CanvasItem, points: PackedVector2Array, at: Vector2, color: Color) -> void:
	var moved: PackedVector2Array = PackedVector2Array()
	for point: Vector2 in points:
		moved.append(point + at)
	if moved.size() >= 3:
		canvas.draw_colored_polygon(moved, color)


## The empty slot of a hand: a faint card-shaped hollow, waiting for the shuffler (or for the card that was lifted out of it).
static func draw_empty(canvas: CanvasItem, rect: Rect2) -> void:
	var points: PackedVector2Array = outline(rect.size, hollow_radii(rect.size.x))
	shade_all(canvas, points, rect.position, HOLLOW)
	canvas.draw_polyline(closed(points, rect.position), HOLLOW_EDGE, HOLLOW_EDGE_WIDTH, true)


## The corners of a hollow: the middle of the cards' radii.
static func hollow_radii(width: float) -> Vector4:
	var radius: float = (CardFace.CORNER_RADIUS_MIN + CardFace.CORNER_RADIUS_MAX) / 2.0 * width / CardFace.REFERENCE_WIDTH * HandCard.WEAR_BOOST
	return Vector4(radius, radius, radius, radius)


## What goes over the back of a card that is on its way (owner: an empty slot is not a hole, the card is coming): a ring that fills as the
## shuffler works and the seconds left. `progress` 0 to 1.
static func draw_back_ring(canvas: CanvasItem, rect: Rect2, progress: float, seconds: int) -> void:
	var centre: Vector2 = rect.get_center()
	var radius: float = rect.size.x * RING_SHARE
	canvas.draw_arc(centre, radius, 0.0, TAU, 40, RING_TRACK, RING_WIDTH, true)
	canvas.draw_arc(centre, radius, -PI / 2.0, -PI / 2.0 + TAU * clampf(progress, 0.0, 1.0), 40, RoyaleStyle.GOLD, RING_WIDTH, true)
	if seconds > 0:
		var font: Font = KalyeahTheme.ACTION_FONT
		var text: String = "%d" % seconds
		var width: float = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.HEADLINE).x
		canvas.draw_string(font, centre + Vector2(-width / 2.0, 14.0), text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.HEADLINE, Color(1.0, 1.0, 1.0, 0.9))
