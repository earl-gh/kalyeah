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


## The tray's colours, from the Clash Royale reference: a dark navy outline, a light blue rim along the top, the bright blue body (lighter at
## the top, darker toward the bottom of the screen) and its shades.
const TRAY_OUTLINE: Color = Color(0.02, 0.12, 0.32)
const TRAY_TOP: Color = Color(0.09, 0.58, 0.93)
const TRAY_BODY: Color = Color(0.03, 0.5, 0.83)
const TRAY_BOTTOM: Color = Color(0.02, 0.27, 0.6)
const TRAY_RIM: Color = Color(0.36, 0.76, 1.0)
const TRAY_HIGHLIGHT: Color = Color(1.0, 1.0, 1.0, 0.42)
const OUTLINE: int = 3
## Both top corners are slightly rounded (owner).
const CORNER: int = UiSkin.RADIUS_M
const RIM_HEIGHT: float = 6.0
const HIGHLIGHT_HEIGHT: float = 2.0
## Polish copied from the main menu's nav bar (`UiTabBar`): a soft shadow cast up over the street (its height and colour), a shade under the
## rim, the sides shaded in a little and the bottom growing darker.
const UNDER_RIM_HEIGHT: float = 12.0
const UNDER_RIM_ALPHA: float = 0.3
const SIDE_SHADE_WIDTH: float = 14.0
const SIDE_SHADE_ALPHA: float = 0.22
const BOTTOM_SHADE_HEIGHT: float = 40.0
const BOTTOM_SHADE_ALPHA: float = 0.3
## The body is TRAY_BODY this share of the way down.
const BODY_AT: float = 0.4

## The width of the column beside the plate (NEXT and the chat button stand there, over the street) and whether it is at the left.
var column_width: float = 0.0
var column_left: bool = true


func set_column(width: float, at_left: bool) -> void:
	column_width = width
	column_left = at_left
	queue_redraw()


func _draw() -> void:
	# a bright blue tray drawn in code, copied from the Clash Royale reference (owner) and polished like the main menu's nav bar: a soft shadow
	# cast up over the street, a navy outline round its two slightly rounded top corners, a light rim with a highlight line along the top, a
	# shade under the rim, a body that is lighter at the top and darker toward the bottom, the sides shaded in a little and the bottom
	# growing darker. It runs off the screen's edge on the side away from the column (the column beside it shows the street).
	var left: float = column_width if column_left else 0.0
	var tray: Rect2 = Rect2(Vector2(left, 0.0), Vector2(maxf(size.x - column_width, 0.0), size.y))
	if tray.size.x <= 0.0:
		return
	_shade(Rect2(tray.position.x, tray.position.y - UiTabBar.SHADOW_HEIGHT, tray.size.x, UiTabBar.SHADOW_HEIGHT), UiTabBar.SHADOW_COLOR, true)
	var outline: StyleBoxFlat = _rounded(TRAY_OUTLINE, CORNER)
	draw_style_box(outline, tray)
	var inside: Rect2 = Rect2(tray.position + Vector2(OUTLINE, OUTLINE), tray.size - Vector2(OUTLINE * 2, OUTLINE))
	# the body with its light rim (a border along the top that follows the rounded corners)
	var body: StyleBoxFlat = _rounded(TRAY_TOP, CORNER - OUTLINE)
	body.border_color = TRAY_RIM
	body.border_width_top = int(RIM_HEIGHT)
	draw_style_box(body, inside)
	# below the corners the body goes from its light top to the bright blue and on to a darker blue at the bottom of the screen
	var straight: float = inside.position.y + float(CORNER - OUTLINE)
	var body_y: float = straight + (inside.end.y - straight) * BODY_AT
	_fill(Rect2(inside.position.x, straight, inside.size.x, body_y - straight), TRAY_TOP, TRAY_BODY)
	_fill(Rect2(inside.position.x, body_y, inside.size.x, inside.end.y - body_y), TRAY_BODY, TRAY_BOTTOM)
	var between: float = float(CORNER - OUTLINE)
	var run: Rect2 = Rect2(inside.position.x + between, inside.position.y, inside.size.x - between * 2.0, 0.0)
	draw_rect(Rect2(run.position + Vector2(0.0, 1.0), Vector2(run.size.x, HIGHLIGHT_HEIGHT)), TRAY_HIGHLIGHT)
	_shade(Rect2(run.position.x, inside.position.y + RIM_HEIGHT, run.size.x, UNDER_RIM_HEIGHT), Color(0.0, 0.03, 0.1, UNDER_RIM_ALPHA), false)
	# the sides are shaded in a little, and the bottom grows darker
	var below: Rect2 = Rect2(inside.position.x, straight, inside.size.x, inside.end.y - straight)
	_shade_across(Rect2(below.position.x, below.position.y, SIDE_SHADE_WIDTH, below.size.y), Color(0.0, 0.03, 0.1, SIDE_SHADE_ALPHA), true)
	_shade_across(Rect2(below.end.x - SIDE_SHADE_WIDTH, below.position.y, SIDE_SHADE_WIDTH, below.size.y), Color(0.0, 0.03, 0.1, SIDE_SHADE_ALPHA), false)
	_shade(Rect2(inside.position.x, inside.end.y - BOTTOM_SHADE_HEIGHT, inside.size.x, BOTTOM_SHADE_HEIGHT), Color(0.0, 0.02, 0.08, BOTTOM_SHADE_ALPHA), true)


## A tray shape with both top corners rounded to `radius` (the sides run off the screen or into its bottom edge).
func _rounded(color: Color, radius: int) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = color
	box.anti_aliasing = true
	box.corner_radius_top_left = maxi(radius, 0)
	box.corner_radius_top_right = maxi(radius, 0)
	return box


## A rectangle filled with a gradient from `top` to `bottom`.
func _fill(rect: Rect2, top: Color, bottom: Color) -> void:
	draw_polygon(PackedVector2Array([rect.position, Vector2(rect.end.x, rect.position.y), rect.end, Vector2(rect.position.x, rect.end.y)]), PackedColorArray([top, top, bottom, bottom]))


## A shade that is `color` at one end and clear at the other: `clear_at_top` true grows toward the bottom, false fades out toward it.
func _shade(rect: Rect2, color: Color, clear_at_top: bool) -> void:
	var clear: Color = Color(color, 0.0)
	_fill(rect, clear if clear_at_top else color, color if clear_at_top else clear)


## The same sideways: `strong_at_left` true is strongest at the left edge and clear at the right.
func _shade_across(rect: Rect2, color: Color, strong_at_left: bool) -> void:
	var clear: Color = Color(color, 0.0)
	var left: Color = color if strong_at_left else clear
	var right: Color = clear if strong_at_left else color
	draw_polygon(PackedVector2Array([rect.position, Vector2(rect.end.x, rect.position.y), rect.end, Vector2(rect.position.x, rect.end.y)]), PackedColorArray([left, right, right, left]))
