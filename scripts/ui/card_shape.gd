class_name CardShape
extends RefCounted
## The outline of a mode card: a rounded rectangle, straight (owner: no tilt). Everything a card draws (face, edge, shadow,
## rim) is made from this one outline.

## Straight segments per rounded corner.
const ARC_STEPS: int = 8


## The rounded rectangle of the card (taken in by `inset` on every side), going clockwise from the top left corner's start, in
## card pixels.
static func outline(size: Vector2, radius: float, inset: float = 0.0) -> PackedVector2Array:
	var points: PackedVector2Array = PackedVector2Array()
	var r: float = maxf(minf(radius, minf(size.x, size.y) / 2.0) - inset, 0.0)
	if r < 0.5:
		# square corners (the cards are like a Filipino teks card)
		return PackedVector2Array([Vector2(inset, inset), Vector2(size.x - inset, inset), Vector2(size.x - inset, size.y - inset), Vector2(inset, size.y - inset)])
	var low: Vector2 = Vector2(inset + r, inset + r)
	var high: Vector2 = size - Vector2(inset + r, inset + r)
	var centres: Array[Vector2] = [low, Vector2(high.x, low.y), high, Vector2(low.x, high.y)]
	for corner: int in 4:
		for step: int in ARC_STEPS + 1:
			var angle: float = PI + float(corner) * PI / 2.0 + float(step) / float(ARC_STEPS) * PI / 2.0
			points.append(centres[corner] + Vector2(cos(angle), sin(angle)) * r)
	return points


## The outline with its own radius on each corner (top left, top right, bottom right, bottom left); 0 is a square corner.
static func outline_corners(size: Vector2, radii: Vector4) -> PackedVector2Array:
	var points: PackedVector2Array = PackedVector2Array()
	var r: Array[float] = [radii.x, radii.y, radii.z, radii.w]
	var anchors: Array[Vector2] = [Vector2.ZERO, Vector2(size.x, 0.0), size, Vector2(0.0, size.y)]
	var inward: Array[Vector2] = [Vector2(1.0, 1.0), Vector2(-1.0, 1.0), Vector2(-1.0, -1.0), Vector2(1.0, -1.0)]
	for corner: int in 4:
		var radius: float = minf(r[corner], minf(size.x, size.y) / 2.0)
		if radius < 0.5:
			points.append(anchors[corner])
			continue
		var centre: Vector2 = anchors[corner] + inward[corner] * radius
		for step: int in ARC_STEPS + 1:
			var angle: float = PI + float(corner) * PI / 2.0 + float(step) / float(ARC_STEPS) * PI / 2.0
			points.append(centre + Vector2(cos(angle), sin(angle)) * radius)
	return points
