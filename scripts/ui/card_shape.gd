class_name CardShape
extends RefCounted
## The outline of a mode card tipped like a real card floating over the table: the top left corner is pushed away from the
## viewer (it shrinks towards the middle) and the bottom right corner is lifted towards the viewer (it grows outwards), the
## other two stay on the tipping line. Everything a card draws (face, edge, shadow, rim) is made from this one outline.

## How far the two tipped corners move, as a share of the card's width (about 10 px on a hero card).
const TILT: float = 0.03
## Straight segments per rounded corner.
const ARC_STEPS: int = 8


## Where a point of the flat card (0,0 to `size`) lands once the card is tipped. The four corners are moved by `TILT` and the
## whole card is shifted back by half of that so its middle stays where the flat card's middle was.
static func warp(point: Vector2, size: Vector2) -> Vector2:
	var move: float = TILT * size.x
	var u: float = point.x / size.x
	var v: float = point.y / size.y
	var top_left: Vector2 = Vector2(move, move) * 0.5
	var top_right: Vector2 = Vector2(size.x, 0.0) - Vector2(move, move) * 0.5
	var bottom_left: Vector2 = Vector2(0.0, size.y) - Vector2(move, move) * 0.5
	var bottom_right: Vector2 = size + Vector2(move, move) * 0.5
	return top_left.lerp(top_right, u).lerp(bottom_left.lerp(bottom_right, u), v)


## The rounded rectangle of the flat card (taken in by `inset` on every side), going clockwise from the top left corner's start,
## in card pixels.
static func flat_outline(size: Vector2, radius: float, inset: float = 0.0) -> PackedVector2Array:
	var points: PackedVector2Array = PackedVector2Array()
	var r: float = maxf(minf(radius, minf(size.x, size.y) / 2.0) - inset, 1.0)
	var low: Vector2 = Vector2(inset + r, inset + r)
	var high: Vector2 = size - Vector2(inset + r, inset + r)
	var centres: Array[Vector2] = [low, Vector2(high.x, low.y), high, Vector2(low.x, high.y)]
	for corner: int in 4:
		for step: int in ARC_STEPS + 1:
			var angle: float = PI + float(corner) * PI / 2.0 + float(step) / float(ARC_STEPS) * PI / 2.0
			points.append(centres[corner] + Vector2(cos(angle), sin(angle)) * r)
	return points


## The tipped card's outline in card pixels (the flat outline pushed through `warp`; `inset` is measured on the flat card).
static func outline(size: Vector2, radius: float, inset: float = 0.0) -> PackedVector2Array:
	var points: PackedVector2Array = flat_outline(size, radius, inset)
	for i: int in points.size():
		points[i] = warp(points[i], size)
	return points
