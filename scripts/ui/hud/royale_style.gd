class_name RoyaleStyle
extends RefCounted
## The look of the match screen's chrome (owner: like Clash Royale instead of a game console): bright royal blue panels with the kit's one outline,
## a gold trim, a glossy sheen on the upper half, deep blue wells for the controls and chunky white text with a dark outline. The match's header,
## controller deck, D-pad, score cells and clock all draw with these colours and helpers so they read as one set.

const BLUE_TOP: Color = Color(0.2, 0.52, 0.92)
const BLUE_BOTTOM: Color = Color(0.1, 0.32, 0.7)
const BLUE_WELL: Color = Color(0.06, 0.2, 0.5)
const BLUE_LIGHT: Color = Color(0.45, 0.72, 1.0)
const GOLD: Color = Color(1.0, 0.8, 0.25)
const GOLD_DARK: Color = Color(0.85, 0.55, 0.1)
const CREAM: Color = Color(0.93, 0.95, 1.0)
const SHEEN: Color = Color(1.0, 1.0, 1.0, 0.22)
## The share of a chunky shape's height its sheen covers.
const SHEEN_SHARE: float = 0.4


## A chunky rounded shape: `fill`, the one outline, the one shadow under it and a glossy sheen on its upper half.
static func chunky(canvas: CanvasItem, rect: Rect2, fill: Color, radius: int, sheen: bool = true) -> void:
	UiSkin.draw_box(canvas, rect, fill, radius, UiSkin.LINE_COLOR, true)
	if sheen:
		var inset: float = UiSkin.LINE + 1.0
		UiSkin.draw_box(canvas, Rect2(rect.position + Vector2(inset, inset), Vector2(rect.size.x - 2.0 * inset, rect.size.y * SHEEN_SHARE)), SHEEN, maxi(radius - UiSkin.LINE, 2))


## A round well for a control: deep blue, a navy outline, a light lower rim and a shade under the upper rim.
static func well(canvas: CanvasItem, centre: Vector2, radius: float) -> void:
	canvas.draw_circle(centre, radius + UiSkin.LINE, UiSkin.LINE_COLOR)
	canvas.draw_circle(centre, radius, BLUE_WELL)
	canvas.draw_arc(centre, radius - 3.0, PI * 0.05, PI * 0.95, 40, Color(BLUE_LIGHT, 0.55), 4.0, true)
	canvas.draw_arc(centre, radius - 3.0, PI * 1.05, PI * 1.95, 40, Color(0.0, 0.0, 0.0, 0.35), 4.0, true)


## Text with a thick dark outline, the way the game's chrome is lettered.
static func lettered(canvas: CanvasItem, font: Font, text: String, origin: Vector2, size: int, color: Color = Color.WHITE) -> void:
	canvas.draw_string_outline(font, origin, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, size, UiSkin.TEXT_LINE, UiSkin.LINE_COLOR)
	canvas.draw_string(font, origin, text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, size, color)
