class_name CardStyle
extends RefCounted
## How a weapon card is drawn on the match deck (owner: like a Clash Royale card in the hand and in the shuffler, not a console button):
## an upright card with a frame in the weapon's kind colour, the round painted art on top and the weapon's name on a plate at the bottom.
## The hand's two buttons and the shuffler's window use the same drawing, at two sizes.

const RADIUS: int = UiSkin.RADIUS_M
const BORDER: int = UiSkin.FRAME
## A card's body is its kind colour darkened this far; the hollow and back are the well's dark.
const BODY_DARKEN: float = 0.62
const HOLLOW: Color = Color(0.0, 0.0, 0.0, 0.25)
const BACK: Color = Color(0.07, 0.14, 0.34)
const NAME_MAX: int = 22
## Names are never drawn smaller than the caption floor: a card too small for that (the shuffler's window) shows only its art.
const NAME_MIN: int = UiMetrics.CAPTION


## Draws the card of `def` in `rect` on `canvas`. `dim` (0 to 1) darkens it (locked, used, waiting); `lift` raises it with a gold edge
## (held or highlighted).
static func draw(canvas: CanvasItem, rect: Rect2, def: WeaponDef, dim: float = 0.0, lift: bool = false) -> void:
	var kind: Color = TeamColors.kind_color(def.kind)
	UiSkin.draw_box(canvas, rect, kind.darkened(BODY_DARKEN), RADIUS, RoyaleStyle.GOLD if lift else kind, true, BORDER)
	# the round art, as wide as the card allows
	var inner: float = rect.size.x - 2.0 * (BORDER + 3.0)
	var texture: Texture2D = Icons.art(StringName("btn_%s" % String(def.id)))
	var art_rect: Rect2 = Rect2(rect.position + Vector2(BORDER + 3.0, BORDER + 3.0), Vector2(inner, inner))
	if texture != null:
		canvas.draw_texture_rect(texture, art_rect, false)
	# the name on a navy plate along the bottom (no plate on a card too small for a readable name)
	var plate: Rect2 = Rect2(rect.position + Vector2(BORDER, rect.size.y - BORDER - plate_height(rect)), Vector2(rect.size.x - 2.0 * BORDER, plate_height(rect)))
	var name_size: int = _name_size(plate, def.short_name)
	if name_size > 0:
		UiSkin.draw_box(canvas, plate, UiSkin.LINE_COLOR, UiSkin.RADIUS_S)
		_name(canvas, plate, def.short_name, name_size)
	if dim > 0.0:
		UiSkin.draw_box(canvas, rect, Color(0.0, 0.0, 0.0, dim), RADIUS)


## The empty slot of a hand: a faint card-shaped hollow, waiting for the shuffler.
static func draw_empty(canvas: CanvasItem, rect: Rect2) -> void:
	UiSkin.draw_box(canvas, rect, HOLLOW, RADIUS, Color(RoyaleStyle.BLUE_LIGHT, 0.35))


## The slot of a card that is on its way (owner: an empty slot is not a hole, the card is coming): a card back with a ring that fills as the
## shuffler works, and the seconds left. `progress` 0 to 1.
static func draw_back(canvas: CanvasItem, rect: Rect2, progress: float, seconds: int) -> void:
	UiSkin.draw_box(canvas, rect, BACK, RADIUS, Color(RoyaleStyle.BLUE_LIGHT, 0.5), false, BORDER)
	UiSkin.draw_box(canvas, rect.grow(-(BORDER + 9.0)), Color.TRANSPARENT, UiSkin.RADIUS_S, Color(RoyaleStyle.GOLD, 0.35))
	var centre: Vector2 = rect.get_center()
	var radius: float = rect.size.x * 0.26
	canvas.draw_arc(centre, radius, 0.0, TAU, 40, Color(1.0, 1.0, 1.0, 0.15), 8.0, true)
	canvas.draw_arc(centre, radius, -PI / 2.0, -PI / 2.0 + TAU * clampf(progress, 0.0, 1.0), 40, RoyaleStyle.GOLD, 8.0, true)
	if seconds > 0:
		var font: Font = KalyeahTheme.ACTION_FONT
		var text: String = "%d" % seconds
		var width: float = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.HEADING).x
		canvas.draw_string(font, centre + Vector2(-width / 2.0, 14.0), text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.HEADING, Color(1.0, 1.0, 1.0, 0.9))


static func plate_height(rect: Rect2) -> float:
	return clampf(rect.size.y * 0.2, 22.0, 40.0)


## Draws `def` darkened from the top down: `ready` (0 to 1) of the card, from the bottom up, is bright (a cooldown that fills the card).
static func draw_filling(canvas: CanvasItem, rect: Rect2, def: WeaponDef, ready: float, dim: float = 0.55) -> void:
	draw(canvas, rect, def)
	var covered: float = rect.size.y * (1.0 - clampf(ready, 0.0, 1.0))
	if covered <= 1.0:
		return
	UiSkin.draw_box(canvas, Rect2(rect.position, Vector2(rect.size.x, covered)), Color(0.0, 0.0, 0.0, dim), RADIUS)


## The font size `text` fits its plate at, or 0 when that would be under `NAME_MIN`.
static func _name_size(plate: Rect2, text: String) -> int:
	var font: Font = KalyeahTheme.ACTION_FONT
	var size: int = NAME_MAX
	while size > NAME_MIN and font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, size).x > plate.size.x - 8.0:
		size -= 1
	size = mini(size, int(plate.size.y * 0.62))
	return size if size >= NAME_MIN else 0


static func _name(canvas: CanvasItem, plate: Rect2, text: String, size: int) -> void:
	var font: Font = KalyeahTheme.ACTION_FONT
	var width: float = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, size).x
	canvas.draw_string(font, Vector2(plate.position.x + (plate.size.x - width) / 2.0, plate.position.y + plate.size.y / 2.0 + size * 0.35), text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, size, Color.WHITE)
