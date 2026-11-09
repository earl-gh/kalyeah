class_name HudStyle
extends RefCounted
## One look for the landscape match's HUD (docs/audits/2026-11-09-landscape-hud.md): every control and word over the street uses these, so
## nothing on screen is louder than the play. Controls hug the screen's edges by one margin; words come in three sizes with one ink and one
## outline per size; touch controls are see-through navy with a cream rim and a cream thumb or glyph.

## The gap between the screen's edge (inside the safe area) and every control.
const EDGE: float = 24.0
## The header (the score and the clock) is drawn this size in landscape: the screen is 720 px tall, not 1280.
const HEADER_SCALE: float = 0.8

## The words under the score (a role line and a hint of up to two lines): their gap below the score and the room they take.
const WORDS_GAP: float = 6.0
const WORDS_HEIGHT: float = 92.0

## The type ramp: a role or status line, a hint or a label under it, and the small words on buttons and tags.
const TITLE: int = UiMetrics.ACTION
const LABEL: int = UiMetrics.CAPTION
const SMALL: int = UiMetrics.WORLD
## Words over the street are lettered in navy, the outline this share of their size (at least 3 px).
const INK: Color = RoyaleStyle.NAVY
const OUTLINE_SHARE: float = 0.22

## The touch controls' look: the fill, the rim and the thumb or glyph.
const FILL: Color = Color(0.035, 0.1, 0.27, 0.38)
const RIM: Color = Color(0.93, 0.95, 1.0, 0.45)
const THUMB: Color = Color(0.93, 0.95, 1.0, 0.7)
const RIM_WIDTH: float = 3.0
## The movement controls (the joystick, the street games' knob) and the street games' main button (Throw, Kick, Dive, Lupa) are white only
## (owner, 2026-11-09): a white rim, a white thumb or word and a faint white body, no navy and no colour. A main button that is not ready is
## the same, dimmer. Its word has a soft shadow under it instead of an outline, so it reads on a bright street.
const WHITE_BODY: Color = Color(1.0, 1.0, 1.0, 0.14)
const WHITE_RIM: Color = Color(1.0, 1.0, 1.0, 0.85)
const WHITE_THUMB: Color = Color(1.0, 1.0, 1.0, 0.85)
const WHITE_OFF: float = 0.45
const WHITE_RIM_WIDTH: float = 4.0
const WORD_SHADOW: Color = Color(0.0, 0.0, 0.0, 0.35)
const WORD_SHADOW_DROP: float = 2.0
## A panel over the street (the minimap): navy, a little more solid than a control, with the same rim.
const PANEL: Color = Color(0.035, 0.1, 0.27, 0.55)
const CORNER: float = 10.0


## The outline width for words of `font_size`.
static func outline(font_size: int) -> int:
	return maxi(3, roundi(float(font_size) * OUTLINE_SHARE))


## Letters `label` in the HUD's ink at `font_size` in `color`.
static func letter(label: Label, font_size: int, color: Color) -> void:
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", color)
	label.add_theme_color_override("font_outline_color", INK)
	label.add_theme_constant_override("outline_size", outline(font_size))


## Draws a white-only control on `canvas` (the movement controls and the main button): a faint white body and a white rim, `strength` 1 when
## live and `WHITE_OFF` when not ready.
static func draw_white_disc(canvas: CanvasItem, middle: Vector2, radius: float, strength: float = 1.0) -> void:
	canvas.draw_circle(middle, radius, Color(WHITE_BODY, WHITE_BODY.a * strength))
	canvas.draw_arc(middle, radius, 0.0, TAU, 48, Color(WHITE_RIM, WHITE_RIM.a * strength), WHITE_RIM_WIDTH, true)


## Draws a round touch control on `canvas`: the see-through fill and the rim.
static func draw_disc(canvas: CanvasItem, middle: Vector2, radius: float, rim: Color = RIM) -> void:
	canvas.draw_circle(middle, radius, FILL)
	canvas.draw_arc(middle, radius, 0.0, TAU, 48, rim, RIM_WIDTH, true)
