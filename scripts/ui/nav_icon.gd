class_name NavIcon
extends Control
## A section button icon drawn in code (the owner's own picture at assets/ui/sections/<id>.png replaces
## it when that file exists). Flat, one colour, readable at 44 px.

const SIZE: float = 46.0
const PICTURE_DIR: String = "res://assets/ui/sections/"

var kind: StringName = &"battle"
var color: Color = Color.WHITE:
	set(value):
		color = value
		queue_redraw()


func _init(tab: StringName = &"battle") -> void:
	kind = tab
	custom_minimum_size = Vector2(SIZE, SIZE)
	mouse_filter = Control.MOUSE_FILTER_IGNORE


## The owner's picture for `tab`, or null.
static func picture(tab: StringName) -> Texture2D:
	var path: String = "%s%s.png" % [PICTURE_DIR, String(tab)]
	return load(path) as Texture2D if ResourceLoader.exists(path) else null


func _draw() -> void:
	var art: Texture2D = picture(kind)
	if art != null:
		draw_texture_rect(art, Rect2(Vector2.ZERO, size), false, color)
		return
	var s: float = minf(size.x, size.y)
	var c: Vector2 = size / 2.0
	var w: float = s * 0.09
	match kind:
		&"characters":
			draw_circle(c + Vector2(0.0, -s * 0.14), s * 0.19, color)
			draw_arc(c + Vector2(0.0, s * 0.46), s * 0.34, PI * 1.12, PI * 1.88, 24, color, w * 2.2)
		&"weapons":
			# two crossed bars with round ends, like crossed sticks
			for side: float in [-1.0, 1.0]:
				draw_line(c + Vector2(-s * 0.32, -s * 0.32 * side), c + Vector2(s * 0.32, s * 0.32 * side), color, w * 1.6)
				draw_circle(c + Vector2(s * 0.32, s * 0.32 * side), w * 1.3, color)
				draw_circle(c + Vector2(-s * 0.32, -s * 0.32 * side), w * 1.3, color)
		&"book":
			# an open book: two pages meeting at the spine, with a few lines of text
			var left: PackedVector2Array = PackedVector2Array([c + Vector2(-s * 0.4, -s * 0.28), c + Vector2(-s * 0.02, -s * 0.2), c + Vector2(-s * 0.02, s * 0.36), c + Vector2(-s * 0.4, s * 0.28)])
			var right: PackedVector2Array = PackedVector2Array([c + Vector2(s * 0.02, -s * 0.2), c + Vector2(s * 0.4, -s * 0.28), c + Vector2(s * 0.4, s * 0.28), c + Vector2(s * 0.02, s * 0.36)])
			draw_colored_polygon(left, color)
			draw_colored_polygon(right, color)
			for line: int in 3:
				var y: float = -s * 0.1 + float(line) * s * 0.14
				draw_line(c + Vector2(-s * 0.32, y), c + Vector2(-s * 0.1, y + s * 0.02), Color(0, 0, 0, 0.35), w * 0.7)
				draw_line(c + Vector2(s * 0.1, y + s * 0.02), c + Vector2(s * 0.32, y), Color(0, 0, 0, 0.35), w * 0.7)
		&"history":
			draw_arc(c, s * 0.34, 0.0, TAU, 32, color, w)
			draw_line(c, c + Vector2(0.0, -s * 0.22), color, w)
			draw_line(c, c + Vector2(s * 0.16, s * 0.1), color, w)
		&"settings":
			# a gear: a ring, eight teeth and a hub
			draw_arc(c, s * 0.2, 0.0, TAU, 24, color, w * 1.5)
			for tooth: int in 8:
				var direction: Vector2 = Vector2.from_angle(float(tooth) * TAU / 8.0)
				draw_line(c + direction * s * 0.27, c + direction * s * 0.4, color, w * 1.7)
		_:
			# battle: a lightning bolt
			var points: PackedVector2Array = PackedVector2Array([
				c + Vector2(s * 0.08, -s * 0.42), c + Vector2(-s * 0.26, s * 0.06), c + Vector2(-s * 0.02, s * 0.06),
				c + Vector2(-s * 0.1, s * 0.42), c + Vector2(s * 0.28, -s * 0.1), c + Vector2(s * 0.03, -s * 0.1),
			])
			draw_colored_polygon(points, color)
