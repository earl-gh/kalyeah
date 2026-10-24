class_name ConsolePanel
extends Control
## The tray at the bottom of the match screen that the hand sits on (and the tagged panel, while you wait in the box): the owner's cardboard
## plate, edge to edge. It only draws; it never takes a touch. The health colours and the heart are shared with the hand, which shows your health at the bar's end.

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


func _draw() -> void:
	# the owner's cardboard plate (assets/ui/hand/) under the cards, edge to edge and down to the bottom of the screen
	draw_texture_rect(HandArt.plate(), Rect2(Vector2.ZERO, size), false)
