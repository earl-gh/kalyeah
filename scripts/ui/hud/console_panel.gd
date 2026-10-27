class_name ConsolePanel
extends Control
## The tray at the bottom of the match screen that the hand sits on (and the tagged panel, while you wait on the sidewalk): a bright blue tray drawn in code like Clash Royale's (owner), edge to edge. It only draws; it never takes a touch. The health colours and the heart are shared with the hand, which shows your health at the bar's end.

## The bar keeps your own colour above this share of your health, turns amber below it and red below the second.
const HEALTH_LOW: float = 0.5
const HEALTH_CRITICAL: float = 0.25
const HEALTH_AMBER: Color = Color(1.0, 0.76, 0.2)
const HEALTH_RED: Color = Color(1.0, 0.3, 0.28)

## The colour of your health: your own colour while healthy, amber below half, red below a quarter.
static func health_color(fraction: float) -> Color:
	if fraction >= HEALTH_LOW:
		return TeamColors.bar_self()
	return HEALTH_AMBER if fraction > HEALTH_CRITICAL else HEALTH_RED


## A heart centred on `at`, `radius` wide from its middle to a side: two lobes and a point, with a dark outline.
static func draw_heart(canvas: CanvasItem, at: Vector2, radius: float, color: Color) -> void:
	var lobe: float = radius * 0.56
	canvas.draw_circle(at + Vector2(-radius * 0.46, -radius * 0.22), lobe, Icons.OUTLINE)
	canvas.draw_circle(at + Vector2(radius * 0.46, -radius * 0.22), lobe, Icons.OUTLINE)
	canvas.draw_colored_polygon(PackedVector2Array([at + Vector2(-radius - 1.0, -radius * 0.12), at + Vector2(radius + 1.0, -radius * 0.12), at + Vector2(0.0, radius + 3.0)]), Icons.OUTLINE)
	canvas.draw_circle(at + Vector2(-radius * 0.46, -radius * 0.22), lobe - 1.5, color)
	canvas.draw_circle(at + Vector2(radius * 0.46, -radius * 0.22), lobe - 1.5, color)
	canvas.draw_colored_polygon(PackedVector2Array([at + Vector2(-radius, -radius * 0.12), at + Vector2(radius, -radius * 0.12), at + Vector2(0.0, radius)]), color)


## The tray's colours, from the Clash Royale reference: a dark navy outline, the body's bright blue, the light blue rim on top (with a
## darker line under it) and the dark blue the body darkens to at the bottom of the screen.
const TRAY_OUTLINE: Color = Color(0.02, 0.17, 0.42)
const TRAY_BODY: Color = Color(0.03, 0.5, 0.83)
const TRAY_RIM: Color = Color(0.2, 0.66, 1.0)
const TRAY_RIM_SHADOW: Color = Color(0.0, 0.3, 0.62, 0.55)
const TRAY_BOTTOM: Color = Color(0.02, 0.2, 0.5, 0.9)
const OUTLINE: int = 3
const CORNER: int = UiSkin.RADIUS_M
const RIM_HEIGHT: float = 7.0
## The body starts to darken this share of the way down, and the shade above the tray is this tall and this dark.
const SHADE_FROM: float = 0.45
const LIFT_HEIGHT: float = 22.0
const LIFT_ALPHA: float = 0.3

## The width of the column beside the plate (NEXT and the chat button stand there, over the street) and whether it is at the left.
var column_width: float = 0.0
var column_left: bool = true


func set_column(width: float, at_left: bool) -> void:
	column_width = width
	column_left = at_left
	queue_redraw()


func _draw() -> void:
	# a bright blue tray drawn in code, copied from the Clash Royale reference (owner; replaces the picture of a cardboard plate): a thin
	# dark outline, a light blue rim along its top edge, a body that darkens toward the bottom of the screen and a soft shade lifting it off
	# the street. It runs off the screen's edge on the side away from the column (the column beside it shows the street).
	var left: float = column_width if column_left else 0.0
	var tray: Rect2 = Rect2(Vector2(left, 0.0), Vector2(maxf(size.x - column_width, 0.0), size.y))
	if tray.size.x <= 0.0:
		return
	var corner: int = CORNER
	_draw_lift(tray)
	var outline: StyleBoxFlat = _flat(TRAY_OUTLINE, corner, column_left)
	draw_style_box(outline, tray)
	var inside: Rect2 = Rect2(tray.position + Vector2(OUTLINE if column_left else 0.0, OUTLINE), tray.size - Vector2(OUTLINE if column_left else 0.0, OUTLINE) - Vector2(0.0 if column_left else OUTLINE, 0.0))
	var body: StyleBoxFlat = _flat(TRAY_BODY, corner - OUTLINE, column_left)
	draw_style_box(body, inside)
	# the body darkens down the tray (a vertical gradient from clear to dark blue)
	var shade_top: float = inside.position.y + inside.size.y * SHADE_FROM
	var shade: PackedVector2Array = PackedVector2Array([Vector2(inside.position.x, shade_top), Vector2(inside.end.x, shade_top), Vector2(inside.end.x, inside.end.y), Vector2(inside.position.x, inside.end.y)])
	draw_polygon(shade, PackedColorArray([Color(TRAY_BOTTOM, 0.0), Color(TRAY_BOTTOM, 0.0), TRAY_BOTTOM, TRAY_BOTTOM]))
	# the light rim along the top and the thin shine just under it
	var rim: StyleBoxFlat = _flat(TRAY_RIM, corner - OUTLINE, column_left)
	draw_style_box(rim, Rect2(inside.position, Vector2(inside.size.x, RIM_HEIGHT)))
	draw_rect(Rect2(inside.position + Vector2(0.0, RIM_HEIGHT), Vector2(inside.size.x, 2.0)), TRAY_RIM_SHADOW)


## A tray shape with only the top corner on the column's side rounded (the rest runs off the screen or into the bottom edge).
func _flat(color: Color, radius: int, column_side_left: bool) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = color
	box.anti_aliasing = true
	if column_side_left:
		box.corner_radius_top_left = maxi(radius, 0)
	else:
		box.corner_radius_top_right = maxi(radius, 0)
	return box


## A soft dark shade just above the tray, so it stands off the street.
func _draw_lift(tray: Rect2) -> void:
	var top: float = tray.position.y - LIFT_HEIGHT
	var points: PackedVector2Array = PackedVector2Array([Vector2(tray.position.x, top), Vector2(tray.end.x, top), Vector2(tray.end.x, tray.position.y), Vector2(tray.position.x, tray.position.y)])
	draw_polygon(points, PackedColorArray([Color(0.0, 0.0, 0.0, 0.0), Color(0.0, 0.0, 0.0, 0.0), Color(0.0, 0.02, 0.1, LIFT_ALPHA), Color(0.0, 0.02, 0.1, LIFT_ALPHA)]))
