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
	"gold": {"top": Color("fed44f"), "bottom": Color("f5b61c"), "lip": Color("b07100"), "line": Color("5e3a04"),
		"text": Color("3a1c00"), "outline": Color(1.0, 1.0, 1.0, 0.0), "outline_size": 0},
	"blue": {"top": Color("62baff"), "bottom": Color("379ef0"), "lip": Color("0a66b4"), "line": Color("0a3d73"),
		"text": Color.WHITE, "outline": Color("0a3d73"), "outline_size": 5},
	"green": {"top": Color("62dc72"), "bottom": Color("1f9a3b"), "lip": Color("126628"), "line": Color("08381a"),
		"text": Color.WHITE, "outline": Color("08381a"), "outline_size": 5},
	"red": {"top": Color("ff7f72"), "bottom": Color("d6342e"), "lip": Color("8c1d1b"), "line": Color("4f0c0b"),
		"text": Color.WHITE, "outline": Color("4f0c0b"), "outline_size": 5},
	"slate": {"top": Color("62779f"), "bottom": Color("3b4c74"), "lip": Color("222e4e"), "line": Color("111830"),
		"text": Color.WHITE, "outline": Color("111830"), "outline_size": 4},
	"disabled": {"top": Color("3a4562"), "bottom": Color("2d374f"), "lip": Color("232b40"), "line": Color("151a2b"),
		"text": Color(0.72, 0.76, 0.84, 0.7), "outline": Color(0.0, 0.0, 0.0, 0.0), "outline_size": 0},
}
## Panel kinds: the body (top, bottom), the outer line (and its width), and the shadow strength.
const PLATES: Dictionary[String, Dictionary] = {
	"glass": {"top": Color("22396f"), "bottom": Color("111f48"), "line": Color("050b1e"), "light": 0.2, "shadow": 0.55},
	"raised": {"top": Color("2c4685"), "bottom": Color("182a5c"), "line": Color("050b1e"), "light": 0.26, "shadow": 0.6},
	"inset": {"top": Color("060d24"), "bottom": Color("0b1635"), "line": Color("2b4072"), "light": 0.0, "shadow": 0.0},
	"night": {"top": Color("0f1c42"), "bottom": Color("08112c"), "line": Color("040a1a"), "light": 0.12, "shadow": 0.5},
	# a chosen card: a warm body in a thick gold rim
	"picked": {"top": Color("3a4a7e"), "bottom": Color("1d2d63"), "line": Color("ffc83a"), "line_width": 5.0, "light": 0.22, "shadow": 0.6},
}
## The one corner scale (art direction, docs/ART.md "Shared UI hierarchy"): on the same 8 px rhythm as the spacing in
## `UiMetrics`. Small: bars' gloss, dots, small tags; medium: fields, code boxes, rows, strips, small tiles; large: buttons,
## cards, tiles, chips on panels; extra large: big panels, the toast, the tab bar, the card tray.
## A shape that should be fully round takes `round_radius` of its height; a shape nested in another takes the outer
## radius minus the gap between them, so the two curves stay parallel (the focus ring is the same rule outward).
const RADIUS_S: int = 8
const RADIUS_M: int = 16
const RADIUS_L: int = 24
const RADIUS_XL: int = 32
const BUTTON_RADIUS: int = RADIUS_M
## The lip the face sits on (visual pass: 9 px read flat next to the brand's chunky look) and how far the face sinks into it
## when pressed (down to `BUTTON_PRESSED_LIP`).
const BUTTON_LIP: int = 12
const BUTTON_PRESSED_LIP: int = 3
## The label's room above and below inside a button with a lip, in all: kept at the old 12 + 9 + 12 so no button changes
## size; a disabled button (a flat lip) keeps its old `BUTTON_TEXT_PAD` above and below.
const BUTTON_CONTENT_Y: float = 33.0
const BUTTON_TEXT_PAD: float = 12.0
## How bright the face's lit top edge and its gloss are, and the panels' lit edge round their sides.
const BUTTON_EDGE_LIGHT: float = 0.5
const BUTTON_GLOSS: float = 0.22
const PLATE_SIDE_LIGHT: float = 0.4
const BUTTON_PAD: int = 12
const PLATE_RADIUS: int = RADIUS_XL
const PLATE_PAD: int = 22
## The focus ring: how far it stands outside its control (also its line), and its one colour for the whole game.
const FOCUS_EXPAND: int = 4
const FOCUS_COLOR: Color = Color("fff0a8")
## Big lettered words (the display scale, `UiMetrics.HEADLINE` to `SCORE`): the outline and the drop of the shadow are
## shares of the font size so every size reads as one family; the ink is the HUD chrome's navy and the shadow the same
## blue-black the skin's button and panel shadows use.
const LETTER_OUTLINE_SHARE: float = 1.0 / 6.0
const LETTER_INK: Color = RoyaleStyle.NAVY
const LETTER_SHADOW_SHARE: float = 1.0 / 12.0
const LETTER_SHADOW: Color = Color(0.0, 0.01, 0.06, 0.5)

static var _cache: Dictionary[String, StyleBoxTexture] = {}

## The panels (kind, corner radius) the first screens use. They are built one at a time behind the boot screen's
## loading bar (`warm_next`), so the first tap on Home or a dialog does not pay for drawing them.
const WARM_PLATES: Array[Array] = [
	["glass", RADIUS_XL], ["raised", RADIUS_XL], ["inset", RADIUS_XL], ["night", RADIUS_XL], ["picked", RADIUS_XL],
	["glass", RADIUS_L], ["raised", RADIUS_L], ["inset", RADIUS_L], ["night", RADIUS_L], ["picked", RADIUS_L],
	["glass", RADIUS_M], ["raised", RADIUS_M], ["inset", RADIUS_M], ["night", RADIUS_M], ["picked", RADIUS_M],
]
static var _warmed: int = 0


## How many textures `warm_next` builds in all: every button in its three states, then the panels.
static func warm_total() -> int:
	return BUTTONS.size() * 3 + WARM_PLATES.size()


## Builds the next texture of the warm-up list (one per call). Returns true once everything is built.
static func warm_next() -> bool:
	if _warmed >= warm_total():
		return true
	var buttons: int = BUTTONS.size() * 3
	if _warmed < buttons:
		var variants: Array = BUTTONS.keys()
		var states: Array[String] = ["normal", "hover", "pressed"]
		button(variants[_warmed / 3] as String, states[_warmed % 3])
	else:
		var spec: Array = WARM_PLATES[_warmed - buttons]
		plate(spec[0] as String, 0.0, spec[1] as int)
	_warmed += 1
	return _warmed >= warm_total()


## 0 to 1: how much of the warm-up list is built.
static func warm_fraction() -> float:
	return float(_warmed) / float(warm_total())


## A game button of `variant` (gold, blue, green, red, slate, disabled) in `state` (normal, hover,
## pressed). The face sinks into its lip when pressed.
static func button(variant: String, state: String = "normal", radius: int = BUTTON_RADIUS) -> StyleBoxTexture:
	var key: String = "b/%s/%s/%d" % [variant, state, radius]
	if _cache.has(key):
		return _cache[key]
	var spec: Dictionary = BUTTONS.get(variant, BUTTONS["slate"]) as Dictionary
	var pressed: bool = state == "pressed"
	var lip: int = BUTTON_PRESSED_LIP if pressed or variant == "disabled" else BUTTON_LIP
	var sink: int = BUTTON_LIP - lip if variant != "disabled" else 0
	var lift: float = 0.1 if state == "hover" else 0.0
	var size: int = (radius + BUTTON_PAD) * 2 + 8
	# the sink moves the body's top corners down: the texture grows by it so the corners stay in the top slice and the
	# stretched middle rows are straight sides (docs/ART.md "Nine-slice")
	var height: int = size + sink
	var image: Image = Image.create(size, height, false, Image.FORMAT_RGBA8)
	var top: Color = (spec["top"] as Color).lightened(lift)
	var bottom: Color = (spec["bottom"] as Color).lightened(lift)
	var body: Rect2 = Rect2(BUTTON_PAD, BUTTON_PAD + sink, size - BUTTON_PAD * 2, size - BUTTON_PAD * 2)
	var face: Rect2 = Rect2(body.position, body.size - Vector2(0.0, lip))
	# the shape is the same left and right: draw the left half and mirror it
	for y: int in height:
		for x: int in (size + 1) / 2:
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
			color = color.lerp(Color.WHITE, (g * g * BUTTON_GLOSS + edge * BUTTON_EDGE_LIGHT) * on_face)
			var alpha: float = _cover(d_body)
			var out: Color = Color(color, alpha)
			image.set_pixel(x, y, out)
			image.set_pixel(size - 1 - x, y, out)
	var box: StyleBoxTexture = _nine_slice(image, radius + BUTTON_PAD, BUTTON_PAD)
	box.texture_margin_top = float(radius + BUTTON_PAD + sink)
	box.content_margin_left = 26.0
	box.content_margin_right = 26.0
	var text_pad: float = BUTTON_TEXT_PAD if variant == "disabled" else (BUTTON_CONTENT_Y - BUTTON_LIP) / 2.0
	box.content_margin_top = text_pad + sink
	box.content_margin_bottom = text_pad + lip
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
	var line: float = spec.get("line_width", 2.0) as float
	for y: int in size:
		for x: int in (size + 1) / 2:
			var at: Vector2 = Vector2(x + 0.5, y + 0.5)
			var d: float = _distance(at, body, radius)
			var t: float = clampf((at.y - body.position.y) / body.size.y, 0.0, 1.0)
			var color: Color = (spec["top"] as Color).lerp(spec["bottom"] as Color, t)
			var alpha: float = _cover(d)
			# the outer line, then a lit 2px line just inside the top edge
			var inner: float = _cover(d + line)
			color = (spec["line"] as Color).lerp(color, inner / maxf(alpha, 0.001))
			var rim: float = clampf(1.0 - (at.y - body.position.y - line) / 3.0, 0.0, 1.0) * _cover(d + line)
			var side: float = (1.0 - clampf((d + line + 1.0) / 2.0, 0.0, 1.0)) * _cover(d + line) * PLATE_SIDE_LIGHT
			color = color.lerp(Color.WHITE, (rim + side * (1.0 - t)) * light)
			var out: Color = Color(color, alpha)
			if shadow > 0.0 and alpha < 1.0:
				var d_shadow: float = _distance(at, Rect2(body.position + Vector2(0.0, 8.0), body.size), radius)
				out = _over(Color(0.0, 0.01, 0.06, shadow * (1.0 - smoothstep(0.0, 18.0, d_shadow)) * (1.0 - alpha)), out)
			image.set_pixel(x, y, out)
			image.set_pixel(size - 1 - x, y, out)
	return _nine_slice(image, radius + PLATE_PAD, PLATE_PAD)


## A label's pill (the HUD's capture line, status messages): a night panel with the label's own side padding. The
## radius is small enough that a one-line label is tall enough for the corners.
static func pill(radius: int = RADIUS_L) -> StyleBoxTexture:
	var box: StyleBoxTexture = plate("night", 0.0, radius)
	box.content_margin_left = 18.0
	box.content_margin_right = 18.0
	box.content_margin_top = 9.0
	box.content_margin_bottom = 10.0
	return box


## The one focus ring, round a control whose corners are `radius`: it stands `FOCUS_EXPAND` outside the control and its
## own corners grow by the same amount, so it follows the control's curve. Flat, so it reads on any surface.
static func focus_ring(radius: int = BUTTON_RADIUS) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = Color.TRANSPARENT
	box.set_corner_radius_all(radius + FOCUS_EXPAND)
	box.set_border_width_all(FOCUS_EXPAND)
	box.border_color = FOCUS_COLOR
	box.set_expand_margin_all(float(FOCUS_EXPAND))
	box.anti_aliasing = true
	return box


## The radius that makes a shape `height` tall fully round at its ends (a pill, a bar, a switch).
static func round_radius(height: float) -> int:
	return int(ceilf(height / 2.0))


## A bar's corners are this share of its height: a rounded rectangle, not a pill (the health, stamina and ice water bars).
const BAR_ROUND_SHARE: float = 0.25


## The corner radius of a rounded-rectangle bar `height` tall (never more than a step of the scale, never less than a pixel).
static func bar_radius(height: float) -> int:
	return clampi(roundi(height * BAR_ROUND_SHARE), 1, RADIUS_S)


## The outline of a lettered word `size` px tall (Label `outline_size`).
static func letter_outline(size: int) -> int:
	return roundi(size * LETTER_OUTLINE_SHARE)


## How far a lettered word's shadow drops below it at `size` px.
static func letter_shadow(size: int) -> int:
	return roundi(size * LETTER_SHADOW_SHARE)


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
