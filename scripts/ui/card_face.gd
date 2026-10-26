class_name CardFace
extends Control
## A mode card's picture drawn as a real card floating over the table: the picture on the tipped outline (`CardShape`), a thin
## paper-white edge along the bottom and the right (the card's thickness, about 4 px, almost paper thin) and a soft shadow
## well below it. The card's own button draws nothing behind it.

const EDGE_COLOR: Color = Color("e9e2d2")
const EDGE_LINE: Color = Color(0.36, 0.33, 0.27, 0.9)
const FACE_LINE: Color = Color(0.03, 0.05, 0.12, 0.85)
## The card's thickness: where the edge shows beyond the face.
const THICKNESS: Vector2 = Vector2(3.5, 5.0)
## The shadow: its layers, how far below the card it falls and how dark the whole of it is.
const SHADOW_LAYERS: int = 7
const SHADOW_DROP: Vector2 = Vector2(10.0, 40.0)
const SHADOW_ALPHA: float = 0.075
## How much further the shadow falls (and lighter it gets) as the card floats up.
const SHADOW_BREATH: float = 10.0

## The card's pose (`CardShape.warp`) and how high it floats (-1 sinking to 1 at the top of its drift): the moving cards set both.
var pose: Vector2 = CardShape.REST:
	set(value):
		if value.distance_to(pose) < 0.0004:
			return
		pose = value
		queue_redraw()
var lift: float = 0.0:
	set(value):
		if absf(value - lift) < 0.01:
			return
		lift = value
		queue_redraw()
var texture: Texture2D:
	set(value):
		texture = value
		queue_redraw()


func _init() -> void:
	name = "Face"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		queue_redraw()


func _draw() -> void:
	if size.x < 8.0 or size.y < 8.0:
		return
	var radius: float = float(KalyeahTheme.CORNER)
	var face: PackedVector2Array = CardShape.outline(size, radius, 0.0, pose)
	var centre: Vector2 = size / 2.0
	for layer: int in SHADOW_LAYERS:
		var grow: float = 1.0 + 0.012 * float(layer)
		var shadow: PackedVector2Array = PackedVector2Array()
		for point: Vector2 in face:
			shadow.append(centre + (point - centre) * grow + SHADOW_DROP + Vector2(0.0, SHADOW_BREATH * lift))
		draw_colored_polygon(shadow, Color(0.0, 0.02, 0.08, SHADOW_ALPHA * (1.0 - 0.3 * lift)))
	var edge: PackedVector2Array = PackedVector2Array()
	for point: Vector2 in face:
		edge.append(point + THICKNESS)
	draw_colored_polygon(edge, EDGE_COLOR)
	_outline(edge, EDGE_LINE, 1.5)
	if texture == null:
		draw_colored_polygon(face, Color("2c4685"))
	else:
		var flat: PackedVector2Array = CardShape.flat_outline(size, radius)
		var uvs: PackedVector2Array = PackedVector2Array()
		var colors: PackedColorArray = PackedColorArray()
		var cover: Vector2 = _cover_scale()
		for point: Vector2 in flat:
			uvs.append(Vector2(0.5 + (point.x / size.x - 0.5) * cover.x, 0.5 + (point.y / size.y - 0.5) * cover.y))
			colors.append(Color.WHITE)
		draw_polygon(face, colors, uvs, texture)
	_outline(face, FACE_LINE, 2.0)


## The share of the picture the card shows across and down (the picture fills the card, the rest is cropped).
func _cover_scale() -> Vector2:
	var picture: float = texture.get_size().x / texture.get_size().y
	var card: float = size.x / size.y
	if card < picture:
		return Vector2(card / picture, 1.0)
	return Vector2(1.0, picture / card)


func _outline(points: PackedVector2Array, color: Color, width: float) -> void:
	var closed: PackedVector2Array = points.duplicate()
	closed.append(points[0])
	draw_polyline(closed, color, width, true)
