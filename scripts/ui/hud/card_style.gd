class_name CardStyle
extends RefCounted
## How a weapon card is drawn on the match deck (owner: like a Clash Royale card in the hand and in the shuffler, not a console button):
## an upright card with a frame in the weapon's kind colour, the round painted art on top and the weapon's name on a plate at the bottom.
## The hand's two buttons and the shuffler's window use the same drawing, at two sizes.

const RADIUS: int = 18
const BORDER: int = 5
const NAME_MAX: int = 22
const NAME_MIN: int = 12


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
	# the name on a navy plate along the bottom
	var plate: Rect2 = Rect2(rect.position + Vector2(BORDER, rect.size.y - BORDER - plate_height(rect)), Vector2(rect.size.x - 2.0 * BORDER, plate_height(rect)))
	var plate_box: StyleBoxFlat = StyleBoxFlat.new()
	plate_box.bg_color = RoyaleStyle.NAVY
	plate_box.corner_radius_bottom_left = RADIUS - BORDER
	plate_box.corner_radius_bottom_right = RADIUS - BORDER
	plate_box.anti_aliasing = true
	canvas.draw_style_box(plate_box, plate)
	_name(canvas, plate, def.short_name)
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


static func plate_height(rect: Rect2) -> float:
	return clampf(rect.size.y * 0.2, 22.0, 40.0)


static func _name(canvas: CanvasItem, plate: Rect2, text: String) -> void:
	var font: Font = KalyeahTheme.ACTION_FONT
	var size: int = NAME_MAX
	while size > NAME_MIN and font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, size).x > plate.size.x - 8.0:
		size -= 1
	size = mini(size, int(plate.size.y * 0.62))
	var width: float = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, size).x
	canvas.draw_string(font, Vector2(plate.position.x + (plate.size.x - width) / 2.0, plate.position.y + plate.size.y / 2.0 + size * 0.35), text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, size, Color.WHITE)
