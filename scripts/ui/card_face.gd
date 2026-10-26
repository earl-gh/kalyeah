class_name CardFace
extends Control
## A mode card's picture drawn as a real card floating over the table (straight): the picture on its outline (`CardShape`), a thin
## paper-white edge along the bottom and the right (the card's thickness, about 4 px, almost paper thin) and a soft shadow
## well below it. The card's own button draws nothing behind it.

const EDGE_COLOR: Color = Color("e9e2d2")
const EDGE_LINE: Color = Color(0.36, 0.33, 0.27, 0.9)
const FACE_LINE: Color = Color(0.03, 0.05, 0.12, 0.85)
## The card's thickness: where the edge shows beyond the face.
const THICKNESS: Vector2 = Vector2(3.5, 5.0)
## Square corners, like a teks card (owner).
const RADIUS: float = 0.0
## The word along the right edge, turned -90 degrees (reading upwards), like the power line on a teks card.
const POWER_TEXT: String = "POWER 9999"
const POWER_SIZE_SHARE: float = 0.075
const POWER_COLOR: Color = Color("fff1b8")
const POWER_OUTLINE: Color = Color("14142e")
## How far the power line is from the right edge of the card (its baseline), as a share of the card's width, and the outline.
const POWER_EDGE_SHARE: float = 0.075
const POWER_OUTLINE_SIZE: int = 7
## The shadow: its layers, how far below the card it falls and how dark the whole of it is.
const SHADOW_LAYERS: int = 7
const SHADOW_DROP: Vector2 = Vector2(10.0, 40.0)
const SHADOW_ALPHA: float = 0.075
## How much further the shadow falls (and lighter it gets) as the card floats up.
const SHADOW_BREATH: float = 10.0

## How high the card floats (-1 sinking to 1 at the top of its drift): the middle card sets it as it drifts, and its shadow follows.
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
	var radius: float = RADIUS
	var face: PackedVector2Array = CardShape.outline(size, radius)
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
		var flat: PackedVector2Array = CardShape.outline(size, radius)
		var uvs: PackedVector2Array = PackedVector2Array()
		var colors: PackedColorArray = PackedColorArray()
		var cover: Vector2 = _cover_scale()
		for point: Vector2 in flat:
			uvs.append(Vector2(0.5 + (point.x / size.x - 0.5) * cover.x, 0.5 + (point.y / size.y - 0.5) * cover.y))
			colors.append(Color.WHITE)
		draw_polygon(face, colors, uvs, texture)
	_outline(face, FACE_LINE, 2.0)
	_draw_power()


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


## "POWER 9999" along the right edge, turned -90 degrees so it reads upwards, in heavy outlined letters.
func _draw_power() -> void:
	var font: Font = KalyeahTheme.BUTTON_FONT
	var font_size: int = int(roundf(size.y * POWER_SIZE_SHARE))
	var text: String = UiText.t(POWER_TEXT)
	var width: float = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size).x
	var ascent: float = font.get_ascent(font_size)
	# the origin is the text's bottom left once it is turned: it runs up the card from there
	var origin: Vector2 = Vector2(size.x - size.x * POWER_EDGE_SHARE - (font.get_height(font_size) - ascent), (size.y + width) / 2.0)
	draw_set_transform(origin, -PI / 2.0, Vector2.ONE)
	draw_string_outline(font, Vector2.ZERO, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, POWER_OUTLINE_SIZE, POWER_OUTLINE)
	draw_string(font, Vector2.ZERO, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, POWER_COLOR)
	draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)
