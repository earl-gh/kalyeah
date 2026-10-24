class_name CardStyle
extends RefCounted
## How a weapon card is drawn on the match deck (owner: like a Clash Royale card in the hand and in the shuffler, not a console button):
## an upright card with a frame in the weapon's kind colour, the round painted art on top and the weapon's name on a plate at the bottom.
## The hand's two buttons and the shuffler's window use the same drawing, at two sizes.

const RADIUS: int = UiSkin.RADIUS_M
const BORDER: int = 5
## How far the thin gold inner frame of a cooling card stands inside its edge; its corners are the card's less this, so the
## two curves stay parallel.
const INNER_INSET: int = BORDER + 9
const NAME_MAX: int = 22
## Names are never drawn smaller than the caption floor: a card too small for that (the shuffler's window) shows only its art.
const NAME_MIN: int = UiMetrics.CAPTION


## Draws the card of `def` in `rect` on `canvas`. `dim` (0 to 1) darkens it (locked, used, waiting); `lift` raises it with a gold edge
## (held or highlighted).
static func draw(canvas: CanvasItem, rect: Rect2, def: WeaponDef, dim: float = 0.0, lift: bool = false) -> void:
	var kind: Color = WeaponPickScreen.kind_color(def.kind)
	var frame: StyleBoxFlat = StyleBoxFlat.new()
	frame.bg_color = kind.darkened(0.62)
	frame.border_color = RoyaleStyle.GOLD if lift else kind
	frame.set_border_width_all(BORDER)
	frame.set_corner_radius_all(RADIUS)
	frame.anti_aliasing = true
	frame.shadow_color = Color(0.0, 0.0, 0.0, 0.45)
	frame.shadow_size = 6
	frame.shadow_offset = Vector2(0.0, 5.0)
	canvas.draw_style_box(frame, rect)
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
		var plate_box: StyleBoxFlat = StyleBoxFlat.new()
		plate_box.bg_color = RoyaleStyle.NAVY
		plate_box.corner_radius_bottom_left = RADIUS - BORDER
		plate_box.corner_radius_bottom_right = RADIUS - BORDER
		plate_box.anti_aliasing = true
		canvas.draw_style_box(plate_box, plate)
		_name(canvas, plate, def.short_name, name_size)
	if dim > 0.0:
		var shade: StyleBoxFlat = StyleBoxFlat.new()
		shade.bg_color = Color(0.0, 0.0, 0.0, dim)
		shade.set_corner_radius_all(RADIUS)
		shade.anti_aliasing = true
		canvas.draw_style_box(shade, rect)


## The empty slot of a hand: a faint card-shaped hollow, waiting for the shuffler.
static func draw_empty(canvas: CanvasItem, rect: Rect2) -> void:
	var hollow: StyleBoxFlat = StyleBoxFlat.new()
	hollow.bg_color = Color(0.0, 0.0, 0.0, 0.25)
	hollow.border_color = Color(RoyaleStyle.BLUE_LIGHT, 0.35)
	hollow.set_border_width_all(3)
	hollow.set_corner_radius_all(RADIUS)
	hollow.anti_aliasing = true
	canvas.draw_style_box(hollow, rect)


## The slot of a card that is on its way (owner: an empty slot is not a hole, the card is coming): a card back with a ring that fills as the
## shuffler works, and the seconds left. `progress` 0 to 1.
static func draw_back(canvas: CanvasItem, rect: Rect2, progress: float, seconds: int) -> void:
	var back: StyleBoxFlat = StyleBoxFlat.new()
	back.bg_color = Color(0.07, 0.14, 0.34)
	back.border_color = Color(RoyaleStyle.BLUE_LIGHT, 0.5)
	back.set_border_width_all(BORDER)
	back.set_corner_radius_all(RADIUS)
	back.anti_aliasing = true
	canvas.draw_style_box(back, rect)
	var inset: Rect2 = rect.grow(-float(INNER_INSET))
	var inner: StyleBoxFlat = StyleBoxFlat.new()
	inner.bg_color = Color(0.0, 0.0, 0.0, 0.0)
	inner.border_color = Color(RoyaleStyle.GOLD, 0.35)
	inner.set_border_width_all(2)
	inner.set_corner_radius_all(RADIUS - INNER_INSET)
	inner.anti_aliasing = true
	canvas.draw_style_box(inner, inset)
	var centre: Vector2 = rect.get_center()
	var radius: float = rect.size.x * 0.26
	canvas.draw_arc(centre, radius, 0.0, TAU, 40, Color(1.0, 1.0, 1.0, 0.15), 8.0, true)
	canvas.draw_arc(centre, radius, -PI / 2.0, -PI / 2.0 + TAU * clampf(progress, 0.0, 1.0), 40, RoyaleStyle.GOLD, 8.0, true)
	if seconds > 0:
		var font: Font = KalyeahTheme.ACTION_FONT
		var text: String = "%d" % seconds
		var width: float = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, 40).x
		canvas.draw_string(font, centre + Vector2(-width / 2.0, 14.0), text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, 40, Color(1.0, 1.0, 1.0, 0.9))


static func plate_height(rect: Rect2) -> float:
	return clampf(rect.size.y * 0.2, 22.0, 40.0)


## Draws `def` darkened from the top down: `ready` (0 to 1) of the card, from the bottom up, is bright (a cooldown that fills the card).
static func draw_filling(canvas: CanvasItem, rect: Rect2, def: WeaponDef, ready: float, dim: float = 0.55) -> void:
	draw(canvas, rect, def)
	var covered: float = rect.size.y * (1.0 - clampf(ready, 0.0, 1.0))
	if covered <= 1.0:
		return
	var shade: StyleBoxFlat = StyleBoxFlat.new()
	shade.bg_color = Color(0.0, 0.0, 0.0, dim)
	shade.set_corner_radius_all(RADIUS)
	shade.anti_aliasing = true
	canvas.draw_style_box(shade, Rect2(rect.position, Vector2(rect.size.x, covered)))


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
