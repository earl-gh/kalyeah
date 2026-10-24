class_name CardStyle
extends RefCounted
## How a weapon card is drawn in the hand (owner: like a Clash Royale card): an upright card with a frame in the weapon's kind colour and
## the round painted art filling its width; no name on it (the ice water cost sits on its bottom edge, drawn by the hand). The hand's cards
## and the NEXT card use the same drawing, at two sizes.

const RADIUS: int = UiSkin.RADIUS_S
## The black outline round a card, and the polished gloss just inside it (Clash Royale's card look): ONE connected band that starts white
## along the top, turns the top right corner and fades through gray down the right side, and comes back as light gray along the bottom.
const BORDER: int = 3
const GLOSS: int = 5
const GLOSS_TOP: Color = Color(1.0, 1.0, 1.0)
const GLOSS_RIGHT: Color = Color(0.55, 0.58, 0.64)
const GLOSS_BOTTOM: Color = Color(0.84, 0.86, 0.9)
## How strong the band is at the top, in the middle of the right side (where it has faded most) and along the bottom.
const GLOSS_ALPHA_TOP: float = 0.95
const GLOSS_ALPHA_RIGHT: float = 0.45
const GLOSS_ALPHA_BOTTOM: float = 0.85
const GLOSS_STEPS: int = 6
const OUTLINE: Color = Color(0.0, 0.0, 0.0, 1.0)
## How far the thin gold inner frame of a cooling card stands inside its edge; its corners are the card's less this, so the
## two curves stay parallel.
const INNER_INSET: int = BORDER + 9


## Draws the card of `def` in `rect` on `canvas`. `dim` (0 to 1) darkens it (locked, used, waiting); `lift` raises it with a gold edge
## (held or highlighted).
static func draw(canvas: CanvasItem, rect: Rect2, def: WeaponDef, dim: float = 0.0, lift: bool = false) -> void:
	var kind: Color = WeaponPickScreen.kind_color(def.kind)
	var frame: StyleBoxFlat = StyleBoxFlat.new()
	frame.bg_color = kind.darkened(0.5)
	frame.border_color = RoyaleStyle.GOLD if lift else OUTLINE
	frame.set_border_width_all(BORDER)
	frame.set_corner_radius_all(RADIUS)
	frame.anti_aliasing = true
	frame.shadow_color = Color(0.0, 0.0, 0.0, 0.45)
	frame.shadow_size = 6
	frame.shadow_offset = Vector2(0.0, 5.0)
	canvas.draw_style_box(frame, rect)
	_glosses(canvas, rect)
	# the round art, as wide as the card allows
	var edge: float = float(BORDER + GLOSS + 1)
	var inner: float = rect.size.x - 2.0 * edge
	var texture: Texture2D = Icons.art(StringName("btn_%s" % String(def.id)))
	var art_rect: Rect2 = Rect2(rect.position + Vector2(edge, edge), Vector2(inner, inner))
	if texture != null:
		canvas.draw_texture_rect(texture, art_rect, false)
	if dim > 0.0:
		var shade: StyleBoxFlat = StyleBoxFlat.new()
		shade.bg_color = Color(0.0, 0.0, 0.0, dim)
		shade.set_corner_radius_all(RADIUS)
		shade.anti_aliasing = true
		canvas.draw_style_box(shade, rect)


## The gloss of a card: a band a few pixels inside its black outline that runs from the top left, along the top, round the top right corner,
## down the right side and round the bottom right corner to the bottom left, as one connected stroke (with a soft wider glow under it):
## white at the top, fading through gray on the right, light gray along the bottom. Nothing on the left side.
static func _glosses(canvas: CanvasItem, rect: Rect2) -> void:
	var inset: float = float(BORDER) + float(GLOSS) / 2.0
	var box: Rect2 = rect.grow(-inset)
	var corner: float = maxf(float(RADIUS) - inset, 2.0)
	var points: PackedVector2Array = PackedVector2Array()
	var marks: PackedFloat32Array = PackedFloat32Array()  # which part of the path each point is on: 0 top, 1 right, 2 bottom
	points.append(Vector2(box.position.x + corner, box.position.y))
	marks.append(0.0)
	points.append(Vector2(box.end.x - corner, box.position.y))
	marks.append(0.0)
	for step: int in range(1, GLOSS_STEPS + 1):
		var angle: float = -PI / 2.0 + PI / 2.0 * float(step) / float(GLOSS_STEPS)
		points.append(Vector2(box.end.x - corner, box.position.y + corner) + Vector2.from_angle(angle) * corner)
		marks.append(0.5)
	points.append(Vector2(box.end.x, box.end.y - corner))
	marks.append(1.0)
	for step: int in range(1, GLOSS_STEPS + 1):
		var angle: float = PI / 2.0 * float(step) / float(GLOSS_STEPS)
		points.append(Vector2(box.end.x - corner, box.end.y - corner) + Vector2.from_angle(angle) * corner)
		marks.append(1.5)
	points.append(Vector2(box.position.x + corner, box.end.y))
	marks.append(2.0)
	var lengths: PackedFloat32Array = PackedFloat32Array([0.0])
	for index: int in range(1, points.size()):
		lengths.append(lengths[index - 1] + points[index].distance_to(points[index - 1]))
	var total: float = maxf(lengths[lengths.size() - 1], 1.0)
	var colors: PackedColorArray = PackedColorArray()
	var glow: PackedColorArray = PackedColorArray()
	for index: int in points.size():
		var color: Color = gloss_color(marks[index], lengths[index] / total)
		colors.append(color)
		glow.append(Color(color, color.a * 0.3))
	canvas.draw_polyline_colors(points, glow, float(GLOSS) * 2.0, true)
	canvas.draw_polyline_colors(points, colors, float(GLOSS), true)


## The gloss colour at a point of its path: `part` 0 on the top, 1 on the right side, 2 on the bottom (halves between are the corners);
## `along` 0 to 1 is how far along the path it is (the band fades in at its start and out at its end).
static func gloss_color(part: float, along: float) -> Color:
	var color: Color
	var alpha: float
	if part <= 0.5:
		color = GLOSS_TOP.lerp(GLOSS_RIGHT, part * 2.0)
		alpha = lerpf(GLOSS_ALPHA_TOP, GLOSS_ALPHA_RIGHT, part * 2.0)
	elif part <= 1.0:
		color = GLOSS_RIGHT
		alpha = GLOSS_ALPHA_RIGHT
	elif part <= 1.5:
		color = GLOSS_RIGHT.lerp(GLOSS_BOTTOM, (part - 1.0) * 2.0)
		alpha = lerpf(GLOSS_ALPHA_RIGHT, GLOSS_ALPHA_BOTTOM, (part - 1.0) * 2.0)
	else:
		color = GLOSS_BOTTOM
		alpha = GLOSS_ALPHA_BOTTOM
	# fade in at the top left and out at the bottom left
	alpha *= clampf(along / 0.06, 0.0, 1.0) * clampf((1.0 - along) / 0.1, 0.0, 1.0)
	return Color(color, alpha)


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
		var width: float = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.HEADLINE).x
		canvas.draw_string(font, centre + Vector2(-width / 2.0, 14.0), text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.HEADLINE, Color(1.0, 1.0, 1.0, 0.9))


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
