class_name UiSkin
extends RefCounted
## The look of Kalyeah's interface, drawn in code (no image files): chunky game buttons with a
## gradient face, a glossy top edge and a darker lip they sink into when pressed, and layered
## panels with a lit top edge and a soft drop shadow.
##
## Every surface is a small anti-aliased texture rendered once (a rounded-rectangle distance field
## per pixel) and cached, used as a nine-slice StyleBoxTexture, so any control can wear it at any
## size and the corners never stretch. Shadows live in the texture's padding and are drawn outside
## the control with the style box's expand margins.

## Colours of every button variant: the face (top, bottom), the lip under it, the dark outline and
## the label (colour and outline). `gold` is the one main action on a screen.
const BUTTONS: Dictionary[String, Dictionary] = {
	"gold": {"top": Color("ffd95e"), "bottom": Color("ff9d1a"), "lip": Color("b4600c"), "line": Color("5e2e04"),
		"text": Color("3a1c00"), "outline": Color(1.0, 1.0, 1.0, 0.0), "outline_size": 0},
	"blue": {"top": Color("58abff"), "bottom": Color("2368eb"), "lip": Color("123f9e"), "line": Color("091f5c"),
		"text": Color.WHITE, "outline": Color("091f5c"), "outline_size": 5},
	"green": {"top": Color("62dc72"), "bottom": Color("1f9a3b"), "lip": Color("126628"), "line": Color("08381a"),
		"text": Color.WHITE, "outline": Color("08381a"), "outline_size": 5},
	"red": {"top": Color("ff7f72"), "bottom": Color("d6342e"), "lip": Color("8c1d1b"), "line": Color("4f0c0b"),
		"text": Color.WHITE, "outline": Color("4f0c0b"), "outline_size": 5},
	"slate": {"top": Color("62779f"), "bottom": Color("3b4c74"), "lip": Color("222e4e"), "line": Color("111830"),
		"text": Color.WHITE, "outline": Color("111830"), "outline_size": 4},
	"disabled": {"top": Color("3a4562"), "bottom": Color("2d374f"), "lip": Color("232b40"), "line": Color("151a2b"),
		"text": Color(0.72, 0.76, 0.84, 0.7), "outline": Color(0.0, 0.0, 0.0, 0.0), "outline_size": 0},
}
## Panel kinds: the body (top, bottom), the outer line, and the shadow strength.
const PLATES: Dictionary[String, Dictionary] = {
	"glass": {"top": Color("22396f"), "bottom": Color("111f48"), "line": Color("050b1e"), "light": 0.2, "shadow": 0.55},
	"raised": {"top": Color("2c4685"), "bottom": Color("182a5c"), "line": Color("050b1e"), "light": 0.26, "shadow": 0.6},
	"inset": {"top": Color("060d24"), "bottom": Color("0b1635"), "line": Color("2b4072"), "light": 0.0, "shadow": 0.0},
	"night": {"top": Color("0f1c42"), "bottom": Color("08112c"), "line": Color("040a1a"), "light": 0.12, "shadow": 0.5},
}
const BUTTON_RADIUS: int = 24
const BUTTON_LIP: int = 9
const BUTTON_PAD: int = 12
const PLATE_RADIUS: int = 30
const PLATE_PAD: int = 22

static var _cache: Dictionary[String, StyleBoxTexture] = {}


## A game button of `variant` (gold, blue, green, red, slate, disabled) in `state` (normal, hover,
## pressed). The face sinks into its lip when pressed.
static func button(variant: String, state: String = "normal", radius: int = BUTTON_RADIUS) -> StyleBoxTexture:
	var key: String = "b/%s/%s/%d" % [variant, state, radius]
	if _cache.has(key):
		return _cache[key]
	var spec: Dictionary = BUTTONS.get(variant, BUTTONS["slate"]) as Dictionary
	var pressed: bool = state == "pressed"
	var lip: int = 3 if pressed or variant == "disabled" else BUTTON_LIP
	var sink: int = BUTTON_LIP - lip if variant != "disabled" else 0
	var lift: float = 0.1 if state == "hover" else 0.0
	var size: int = (radius + BUTTON_PAD) * 2 + 8
	var image: Image = Image.create(size, size, false, Image.FORMAT_RGBA8)
	var top: Color = (spec["top"] as Color).lightened(lift)
	var bottom: Color = (spec["bottom"] as Color).lightened(lift)
	var body: Rect2 = Rect2(BUTTON_PAD, BUTTON_PAD + sink, size - BUTTON_PAD * 2, size - BUTTON_PAD * 2 - sink)
	var face: Rect2 = Rect2(body.position, body.size - Vector2(0.0, lip))
	var shadow_alpha: float = 0.0 if pressed or variant == "disabled" else 0.5
	for y: int in size:
		for x: int in size:
			var at: Vector2 = Vector2(x + 0.5, y + 0.5)
			var d_body: float = _distance(at, body, radius)
			var d_face: float = _distance(at, face, radius)
			var t: float = clampf((at.y - face.position.y) / face.size.y, 0.0, 1.0)
			var color: Color = (spec["lip"] as Color).lerp(top.lerp(bottom, t), _cover(d_face))
			# the outline round the whole shape
			var inner: float = _cover(d_body + 2.0)
			color = (spec["line"] as Color).lerp(color, inner / maxf(_cover(d_body), 0.001))
			# the gloss: a bright band over the top half of the face and a lit top edge
			var gloss_h: float = face.size.y * 0.5
			var g: float = clampf(1.0 - (at.y - face.position.y) / gloss_h, 0.0, 1.0)
			var edge: float = clampf(1.0 - (at.y - face.position.y - 2.0) / 3.0, 0.0, 1.0)
			var on_face: float = _cover(d_face + 2.0)
			color = color.lerp(Color.WHITE, (g * g * 0.22 + edge * 0.35) * on_face)
			var alpha: float = _cover(d_body)
			var out: Color = Color(color, alpha)
			if shadow_alpha > 0.0 and alpha < 1.0:
				var d_shadow: float = _distance(at, Rect2(body.position + Vector2(0.0, 6.0), body.size), radius)
				var s: float = shadow_alpha * (1.0 - smoothstep(0.0, 10.0, d_shadow)) * (1.0 - alpha)
				out = _over(Color(0.0, 0.01, 0.06, s), out)
			image.set_pixel(x, y, out)
	var box: StyleBoxTexture = _nine_slice(image, radius + BUTTON_PAD, BUTTON_PAD)
	box.content_margin_left = 26.0
	box.content_margin_right = 26.0
	box.content_margin_top = 12.0 + sink
	box.content_margin_bottom = 12.0 + lip
	_cache[key] = box
	return box


## The label colours of a button variant, applied with `style_label`.
static func style_label(control: Control, variant: String) -> void:
	var spec: Dictionary = BUTTONS.get(variant, BUTTONS["slate"]) as Dictionary
	for state: String in ["font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color"]:
		control.add_theme_color_override(state, spec["text"] as Color)
	control.add_theme_color_override("font_disabled_color", (BUTTONS["disabled"] as Dictionary)["text"] as Color)
	control.add_theme_color_override("font_outline_color", spec["outline"] as Color)
	control.add_theme_constant_override("outline_size", spec["outline_size"] as int)


## A panel of `kind` (glass, raised, inset, night) with `padding` inside.
static func plate(kind: String = "glass", padding: float = 24.0, radius: int = PLATE_RADIUS) -> StyleBoxTexture:
	var key: String = "p/%s/%d" % [kind, radius]
	var box: StyleBoxTexture
	if _cache.has(key):
		box = _cache[key].duplicate() as StyleBoxTexture
	else:
		box = _render_plate(kind, radius)
		_cache[key] = box
		box = box.duplicate() as StyleBoxTexture
	box.set_content_margin_all(padding)
	return box


static func _render_plate(kind: String, radius: int) -> StyleBoxTexture:
	var spec: Dictionary = PLATES.get(kind, PLATES["glass"]) as Dictionary
	var size: int = (radius + PLATE_PAD) * 2 + 8
	var image: Image = Image.create(size, size, false, Image.FORMAT_RGBA8)
	var body: Rect2 = Rect2(PLATE_PAD, PLATE_PAD, size - PLATE_PAD * 2, size - PLATE_PAD * 2)
	var shadow: float = spec["shadow"] as float
	var light: float = spec["light"] as float
	for y: int in size:
		for x: int in size:
			var at: Vector2 = Vector2(x + 0.5, y + 0.5)
			var d: float = _distance(at, body, radius)
			var t: float = clampf((at.y - body.position.y) / body.size.y, 0.0, 1.0)
			var color: Color = (spec["top"] as Color).lerp(spec["bottom"] as Color, t)
			var alpha: float = _cover(d)
			# the outer line, then a lit 2px line just inside the top edge
			var inner: float = _cover(d + 2.0)
			color = (spec["line"] as Color).lerp(color, inner / maxf(alpha, 0.001))
			var rim: float = clampf(1.0 - (at.y - body.position.y - 2.0) / 3.0, 0.0, 1.0) * _cover(d + 2.0)
			var side: float = (1.0 - clampf((d + 3.0) / 2.0, 0.0, 1.0)) * _cover(d + 2.0) * 0.25
			color = color.lerp(Color.WHITE, (rim + side * (1.0 - t)) * light)
			var out: Color = Color(color, alpha)
			if shadow > 0.0 and alpha < 1.0:
				var d_shadow: float = _distance(at, Rect2(body.position + Vector2(0.0, 8.0), body.size), radius)
				out = _over(Color(0.0, 0.01, 0.06, shadow * (1.0 - smoothstep(0.0, 18.0, d_shadow)) * (1.0 - alpha)), out)
			image.set_pixel(x, y, out)
	return _nine_slice(image, radius + PLATE_PAD, PLATE_PAD)


## A thin lit outline for a focused control (kept flat so it reads on any surface).
static func focus_ring(radius: int = BUTTON_RADIUS + 2) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = Color.TRANSPARENT
	box.set_corner_radius_all(radius)
	box.set_border_width_all(4)
	box.border_color = Color("fff0a8")
	box.set_expand_margin_all(4.0)
	box.anti_aliasing = true
	return box


static func _nine_slice(image: Image, margin: int, pad: int) -> StyleBoxTexture:
	var box: StyleBoxTexture = StyleBoxTexture.new()
	box.texture = ImageTexture.create_from_image(image)
	box.set_texture_margin_all(float(margin))
	# the shadow lives in the padding: draw it outside the control's own rectangle
	box.set_expand_margin_all(float(pad))
	return box


## Signed distance from `at` to a rounded rectangle (negative inside).
static func _distance(at: Vector2, rect: Rect2, radius: int) -> float:
	var half: Vector2 = rect.size / 2.0
	var q: Vector2 = (at - (rect.position + half)).abs() - (half - Vector2(radius, radius))
	return Vector2(maxf(q.x, 0.0), maxf(q.y, 0.0)).length() + minf(maxf(q.x, q.y), 0.0) - radius


## Anti-aliased coverage from a distance (1 inside, 0 outside, a one-pixel soft edge).
static func _cover(distance: float) -> float:
	return clampf(0.5 - distance, 0.0, 1.0)


static func _over(front: Color, back: Color) -> Color:
	var alpha: float = front.a + back.a * (1.0 - front.a)
	if alpha <= 0.0001:
		return Color(0.0, 0.0, 0.0, 0.0)
	var rgb: Color = (front * front.a + back * back.a * (1.0 - front.a)) / alpha
	return Color(rgb.r, rgb.g, rgb.b, alpha)
