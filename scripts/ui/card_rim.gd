class_name CardRim
extends Control
## The thin coloured outline glow of the chosen mode card: a fine line in the card's accent colour and a compact glow round it, drawn
## on the card's own outline (`CardShape`, with the card's own corner radii) so it hugs the card. The carousel fades it out on every
## card that is not chosen.

## The glow: a few lines of falling width and rising strength, so it fades out softly over about 7 px.
const GLOW_WIDTHS: Array[float] = [14.0, 9.0, 5.0]
const GLOW_ALPHAS: Array[float] = [0.1, 0.18, 0.32]
const LINE_WIDTH: float = 2.5

var accent: Color = Color.WHITE:
	set(value):
		accent = value
		queue_redraw()
## The card's corner radii (top left, top right, bottom right, bottom left), as on its `CardFace`.
var radii: Vector4 = Vector4.ZERO:
	set(value):
		radii = value
		queue_redraw()


func _init() -> void:
	name = "Frame"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		queue_redraw()


func _draw() -> void:
	if size.x < 8.0 or size.y < 8.0:
		return
	var outline: PackedVector2Array = CardShape.outline_corners(size, radii)
	outline.append(outline[0])
	for i: int in GLOW_WIDTHS.size():
		draw_polyline(outline, Color(accent, GLOW_ALPHAS[i]), GLOW_WIDTHS[i], true)
	draw_polyline(outline, accent, LINE_WIDTH, true)
