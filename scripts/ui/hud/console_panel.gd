class_name ConsolePanel
extends Control
## The tray at the bottom of the match screen that the hand sits on (and the tagged panel, while you wait in the box): a dark navy strip
## under the street with rounded top corners, a gold trim and a bevel along its top, darker toward the screen's edge. It only draws; it
## never takes a touch. The health colours and the heart are shared with the hand, which shows your health at the bar's end.

## The bar keeps your own colour above this share of your health, turns amber below it and red below the second.
const HEALTH_LOW: float = 0.5
const HEALTH_CRITICAL: float = 0.25
const HEALTH_AMBER: Color = Color(1.0, 0.76, 0.2)
const HEALTH_RED: Color = Color(1.0, 0.3, 0.28)
## The tray's depth (visual pass; it was one flat navy): the body shades from `TRAY_TOP` to `TRAY_BOTTOM`, a light line
## and a dark line under the gold trim make a bevel.
const TRAY_TOP: Color = Color(0.11, 0.22, 0.5)
const TRAY_BOTTOM: Color = Color(0.03, 0.07, 0.19)
const TRAY_CORNER: int = UiSkin.RADIUS_XL
const TRAY_TRIM_Y: float = 7.0
const TRAY_LIGHT: Color = Color(1.0, 1.0, 1.0, 0.2)
const TRAY_BEVEL_DARK: Color = Color(0.0, 0.0, 0.0, 0.35)


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
	var w: float = size.x
	var h: float = size.y
	var shell: StyleBoxFlat = StyleBoxFlat.new()
	shell.bg_color = TRAY_TOP
	shell.corner_radius_top_left = TRAY_CORNER
	shell.corner_radius_top_right = TRAY_CORNER
	shell.border_color = RoyaleStyle.NAVY
	shell.border_width_top = 5
	shell.anti_aliasing = true
	shell.shadow_color = Color(0.0, 0.0, 0.0, 0.5)
	shell.shadow_size = 16
	draw_style_box(shell, Rect2(0.0, 0.0, w, h + 30.0))
	# the body darkens toward the screen's edge, under the rounded top
	var band: float = float(TRAY_CORNER)
	draw_polygon(PackedVector2Array([Vector2(0.0, band), Vector2(w, band), Vector2(w, h), Vector2(0.0, h)]),
		PackedColorArray([TRAY_TOP, TRAY_TOP, TRAY_BOTTOM, TRAY_BOTTOM]))
	# the gold trim, a light line under it and a dark one under that: a bevelled top edge
	draw_line(Vector2(band, TRAY_TRIM_Y), Vector2(w - band, TRAY_TRIM_Y), Color(RoyaleStyle.GOLD, 0.8), 3.0, true)
	draw_line(Vector2(band, TRAY_TRIM_Y + 4.0), Vector2(w - band, TRAY_TRIM_Y + 4.0), TRAY_LIGHT, 2.0, true)
	draw_line(Vector2(band, TRAY_TRIM_Y + 7.0), Vector2(w - band, TRAY_TRIM_Y + 7.0), TRAY_BEVEL_DARK, 2.0, true)
