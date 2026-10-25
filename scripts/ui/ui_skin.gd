class_name UiSkin
extends RefCounted
## The look of Kalyeah's interface, drawn in code (no image files), and the kit's tokens: the one radius scale, the one
## outline, the one shadow and the shared components (panel, buttons, pill, chip, bar). Screens never build a style box of
## their own: they ask for a component here, so a painted 9-slice texture can replace a component for every screen at once
## (`painted`).
##
## Buttons and panels are small anti-aliased textures rendered once (a rounded-rectangle distance field per pixel) and
## cached, used as nine-slice StyleBoxTextures, so any control can wear them at any size and the corners never stretch.
## Shadows live in the texture's padding and are drawn outside the control with the style box's expand margins.

# ---- tokens (art-director) ------------------------------------------------------------------------------------------

## The radius scale, the only corner radii in the interface. SMALL: chips' insides, bars, fields, swatches, anything inside
## a card. MEDIUM: buttons, cards, tiles, pills. LARGE: sheets, dialogs and full-width strips. ROUND turns a flat box into a
## pill or a dot (a flat box clamps its radius to half its short side).
const RADIUS_S: int = 12
const RADIUS_M: int = 24
const RADIUS_L: int = 34
const RADIUS_ROUND: int = 256
const RADII: Array[int] = [RADIUS_S, RADIUS_M, RADIUS_L, RADIUS_ROUND]
## One outline: every shape's dark edge is LINE px of LINE_COLOR (buttons, panels, chips, bars, wells, the HUD). A FRAME is
## the one heavier rim that carries meaning: a weapon card's kind colour and the gold rim of a picked card.
const LINE: int = 3
const FRAME: int = 5
const LINE_COLOR: Color = Color("071030")
## Outlined text: TEXT_LINE round body-sized words over the street or a button; TITLE_LINE round display titles and numbers.
const TEXT_LINE: int = 6
const TITLE_LINE: int = 12
## One shadow: a soft navy drop, SHADOW_OFFSET px down and SHADOW_SOFT px wide (texture and flat boxes alike). Titles cast
## the same colour at TITLE_SHADOW px.
const SHADOW_COLOR: Color = Color(0.0, 0.01, 0.06, 0.5)
const SHADOW_OFFSET: int = 6
const SHADOW_SOFT: int = 12
const TITLE_SHADOW: int = 4
## The room round a drawn texture for its shadow (below) and its mirror (above and at the sides).
const SHADOW_PAD: int = SHADOW_OFFSET + SHADOW_SOFT
## The dark of a well: a bar's track, a picture's round holder; and the light line along the top of a bar's fill.
const WELL: Color = Color("0a1433")
const BAR_GLOSS: Color = Color(1.0, 1.0, 1.0, 0.3)
## How far a chip's edge is darkened from its fill.
const CHIP_EDGE_DARKEN: float = 0.35

## Colours of every button variant: the face (top, bottom), the lip under it and the label (colour and outline).
## `gold` is the one main action on a screen. Every outline is LINE_COLOR.
const BUTTONS: Dictionary[String, Dictionary] = {
	"gold": {"top": Color("ffd95e"), "bottom": Color("ff9d1a"), "lip": Color("b4600c"),
		"text": Color("3a1c00"), "outline": Color(1.0, 1.0, 1.0, 0.0), "outline_size": 0},
	"blue": {"top": Color("58abff"), "bottom": Color("2368eb"), "lip": Color("123f9e"),
		"text": Color.WHITE, "outline": LINE_COLOR, "outline_size": TEXT_LINE},
	"green": {"top": Color("62dc72"), "bottom": Color("1f9a3b"), "lip": Color("126628"),
		"text": Color.WHITE, "outline": LINE_COLOR, "outline_size": TEXT_LINE},
	"red": {"top": Color("ff7f72"), "bottom": Color("d6342e"), "lip": Color("8c1d1b"),
		"text": Color.WHITE, "outline": LINE_COLOR, "outline_size": TEXT_LINE},
	"slate": {"top": Color("62779f"), "bottom": Color("3b4c74"), "lip": Color("222e4e"),
		"text": Color.WHITE, "outline": LINE_COLOR, "outline_size": TEXT_LINE},
	"disabled": {"top": Color("3a4562"), "bottom": Color("2d374f"), "lip": Color("232b40"),
		"text": Color(0.72, 0.76, 0.84, 0.7), "outline": Color(0.0, 0.0, 0.0, 0.0), "outline_size": 0},
}
## Panel kinds: the body (top, bottom), the edge (LINE_COLOR, or a lit lip for a sunken well, or the gold FRAME of a picked
## card), how bright the lit top edge is and whether it casts the shadow.
const PLATES: Dictionary[String, Dictionary] = {
	"glass": {"top": Color("22396f"), "bottom": Color("111f48"), "line": LINE_COLOR, "light": 0.2, "shadow": true},
	"raised": {"top": Color("2c4685"), "bottom": Color("182a5c"), "line": LINE_COLOR, "light": 0.26, "shadow": true},
	"inset": {"top": Color("060d24"), "bottom": Color("0b1635"), "line": Color("2b4072"), "light": 0.0, "shadow": false},
	"night": {"top": Color("0f1c42"), "bottom": Color("08112c"), "line": LINE_COLOR, "light": 0.12, "shadow": true},
	# a chosen card: a warm body in a thick gold rim
	"picked": {"top": Color("3a4a7e"), "bottom": Color("1d2d63"), "line": Color("ffc83a"), "line_width": float(FRAME), "light": 0.22, "shadow": true},
}
const BUTTON_RADIUS: int = RADIUS_M
## The lip the face sits on (visual pass: 9 px read flat next to the brand's chunky look) and how far the face sinks into it
## when pressed (down to `BUTTON_PRESSED_LIP`).
const BUTTON_LIP: int = 12
const BUTTON_PRESSED_LIP: int = 3
## The label's room above and below inside a button with a lip, in all: kept at the old 12 + 9 + 12 so no button changes
## size; a disabled button (a flat lip) keeps its old `BUTTON_TEXT_PAD` above and below.
const BUTTON_CONTENT_Y: float = 33.0
const BUTTON_TEXT_PAD: float = 12.0
## The label's room at the sides of a button.
const BUTTON_TEXT_SIDE: float = 26.0
## How bright the face's lit top edge and its gloss are, and the panels' lit edge round their sides.
const BUTTON_EDGE_LIGHT: float = 0.5
const BUTTON_GLOSS: float = 0.22
const PLATE_SIDE_LIGHT: float = 0.4
const BUTTON_PAD: int = SHADOW_PAD
const PLATE_RADIUS: int = RADIUS_L
const PLATE_PAD: int = SHADOW_PAD
## A pill's and a chip's room round their words.
const PILL_MARGIN: Vector4 = Vector4(18.0, 9.0, 18.0, 10.0)
const CHIP_MARGIN: Vector2 = Vector2(16.0, 4.0)

## The one place a painted 9-slice texture replaces a code-drawn component, for every screen at once: component name ->
## {"texture": res path, "margin": the 9-slice margin in px}. Names: "button/<variant>/<state>", "plate/<kind>", "pill",
## "chip", "bar/track", "bar/fill". Empty: no texture ships with the kit; the code-drawn look is the stand-in.
static var painted: Dictionary[String, Dictionary] = {}

static var _cache: Dictionary[String, StyleBoxTexture] = {}
## The flat box `draw_box` reuses: drawing records its shape at once, so one box serves every drawn shape (no allocation
## per frame).
static var _scratch: StyleBoxFlat

## The panels (kind, corner radius) the first screens use. They are built one at a time behind the boot screen's
## loading bar (`warm_next`), so the first tap on Home or a dialog does not pay for drawing them.
const WARM_PLATES: Array[Array] = [
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


## The owner's painted art for `component`, or null while the code-drawn look stands in (see `painted`).
static func painted_box(component: String) -> StyleBoxTexture:
	if not painted.has(component):
		return null
	var spec: Dictionary = painted[component]
	var texture: Texture2D = load(spec["texture"] as String) as Texture2D
	if texture == null:
		return null
	var box: StyleBoxTexture = StyleBoxTexture.new()
	box.texture = texture
	box.set_texture_margin_all(float(spec.get("margin", RADIUS_M) as int))
	return box


# ---- components -----------------------------------------------------------------------------------------------------

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
	var art: StyleBoxTexture = painted_box("button/%s/%s" % [variant, state])
	if art != null:
		_button_margins(art, variant, lip, sink)
		_cache[key] = art
		return art
	var lift: float = 0.1 if state == "hover" else 0.0
	var size: int = (radius + BUTTON_PAD) * 2 + 8
	var image: Image = Image.create(size, size, false, Image.FORMAT_RGBA8)
	var top: Color = (spec["top"] as Color).lightened(lift)
	var bottom: Color = (spec["bottom"] as Color).lightened(lift)
	var body: Rect2 = Rect2(BUTTON_PAD, BUTTON_PAD + sink, size - BUTTON_PAD * 2, size - BUTTON_PAD * 2 - sink)
	var face: Rect2 = Rect2(body.position, body.size - Vector2(0.0, lip))
	var shadow_alpha: float = 0.0 if pressed or variant == "disabled" else SHADOW_COLOR.a
	# the shape is the same left and right: draw the left half and mirror it
	for y: int in size:
		for x: int in (size + 1) / 2:
			var at: Vector2 = Vector2(x + 0.5, y + 0.5)
			var d_body: float = _distance(at, body, radius)
			var d_face: float = _distance(at, face, radius)
			var t: float = clampf((at.y - face.position.y) / face.size.y, 0.0, 1.0)
			var color: Color = (spec["lip"] as Color).lerp(top.lerp(bottom, t), _cover(d_face))
			# the outline round the whole shape
			var inner: float = _cover(d_body + LINE)
			color = LINE_COLOR.lerp(color, inner / maxf(_cover(d_body), 0.001))
			# the gloss: a bright band over the top half of the face and a lit top edge
			var gloss_h: float = face.size.y * 0.5
			var g: float = clampf(1.0 - (at.y - face.position.y) / gloss_h, 0.0, 1.0)
			var edge: float = clampf(1.0 - (at.y - face.position.y - LINE) / 3.0, 0.0, 1.0)
			var on_face: float = _cover(d_face + LINE)
			color = color.lerp(Color.WHITE, (g * g * BUTTON_GLOSS + edge * BUTTON_EDGE_LIGHT) * on_face)
			var alpha: float = _cover(d_body)
			var out: Color = Color(color, alpha)
			if shadow_alpha > 0.0 and alpha < 1.0:
				out = _over(_shadow(at, body, radius, shadow_alpha, alpha), out)
			image.set_pixel(x, y, out)
			image.set_pixel(size - 1 - x, y, out)
	var box: StyleBoxTexture = _nine_slice(image, radius + BUTTON_PAD, BUTTON_PAD)
	_button_margins(box, variant, lip, sink)
	_cache[key] = box
	return box


static func _button_margins(box: StyleBoxTexture, variant: String, lip: int, sink: int) -> void:
	box.content_margin_left = BUTTON_TEXT_SIDE
	box.content_margin_right = BUTTON_TEXT_SIDE
	var text_pad: float = BUTTON_TEXT_PAD if variant == "disabled" else (BUTTON_CONTENT_Y - BUTTON_LIP) / 2.0
	box.content_margin_top = text_pad + sink
	box.content_margin_bottom = text_pad + lip


## The label colours of a button variant, applied with `style_label`.
static func style_label(control: Control, variant: String) -> void:
	var spec: Dictionary = BUTTONS.get(variant, BUTTONS["slate"]) as Dictionary
	for state: String in ["font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color"]:
		control.add_theme_color_override(state, spec["text"] as Color)
	control.add_theme_color_override("font_disabled_color", (BUTTONS["disabled"] as Dictionary)["text"] as Color)
	control.add_theme_color_override("font_outline_color", spec["outline"] as Color)
	control.add_theme_constant_override("outline_size", spec["outline_size"] as int)


## A panel of `kind` (glass, raised, inset, night, picked) with `padding` inside; `radius` from the scale (LARGE for sheets
## and strips, MEDIUM for cards and tiles).
static func plate(kind: String = "glass", padding: float = 24.0, radius: int = PLATE_RADIUS) -> StyleBoxTexture:
	var key: String = "p/%s/%d" % [kind, radius]
	var box: StyleBoxTexture
	if _cache.has(key):
		box = _cache[key].duplicate() as StyleBoxTexture
	else:
		var art: StyleBoxTexture = painted_box("plate/%s" % kind)
		box = art if art != null else _render_plate(kind, radius)
		_cache[key] = box
		box = box.duplicate() as StyleBoxTexture
	box.set_content_margin_all(padding)
	return box


static func _render_plate(kind: String, radius: int) -> StyleBoxTexture:
	var spec: Dictionary = PLATES.get(kind, PLATES["glass"]) as Dictionary
	var size: int = (radius + PLATE_PAD) * 2 + 8
	var image: Image = Image.create(size, size, false, Image.FORMAT_RGBA8)
	var body: Rect2 = Rect2(PLATE_PAD, PLATE_PAD, size - PLATE_PAD * 2, size - PLATE_PAD * 2)
	var shadow: bool = spec["shadow"] as bool
	var light: float = spec["light"] as float
	var line: float = spec.get("line_width", float(LINE)) as float
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
			if shadow and alpha < 1.0:
				out = _over(_shadow(at, body, radius, SHADOW_COLOR.a, alpha), out)
			image.set_pixel(x, y, out)
			image.set_pixel(size - 1 - x, y, out)
	return _nine_slice(image, radius + PLATE_PAD, PLATE_PAD)


## The one shadow, as a pixel under a texture's shape: SHADOW_OFFSET down, fading over SHADOW_SOFT px.
static func _shadow(at: Vector2, body: Rect2, radius: int, strength: float, cover: float) -> Color:
	var d_shadow: float = _distance(at, Rect2(body.position + Vector2(0.0, SHADOW_OFFSET), body.size), radius)
	return Color(SHADOW_COLOR, strength * (1.0 - smoothstep(0.0, float(SHADOW_SOFT), d_shadow)) * (1.0 - cover))


## A label's pill (the HUD's capture line, status messages): a night panel with the label's own side padding.
static func pill() -> StyleBoxTexture:
	var art: StyleBoxTexture = painted_box("pill")
	var box: StyleBoxTexture = art if art != null else plate("night", 0.0, RADIUS_M)
	box.content_margin_left = PILL_MARGIN.x
	box.content_margin_top = PILL_MARGIN.y
	box.content_margin_right = PILL_MARGIN.z
	box.content_margin_bottom = PILL_MARGIN.w
	return box


## A chip: a word or a number on a solid `fill` ("Victory", "Attack", "Host"), round-ended, its edge a darker `fill`.
static func chip(fill: Color) -> StyleBox:
	var art: StyleBoxTexture = painted_box("chip")
	var box: StyleBox
	if art != null:
		art.modulate_color = fill
		box = art
	else:
		box = flat(fill, RADIUS_ROUND, fill.darkened(CHIP_EDGE_DARKEN))
	box.content_margin_left = CHIP_MARGIN.x
	box.content_margin_right = CHIP_MARGIN.x
	box.content_margin_top = CHIP_MARGIN.y
	box.content_margin_bottom = CHIP_MARGIN.y
	return box


## A bar's track: dark, outlined, round-ended (health, stats, the sound slider, a heading's accent).
static func bar_track() -> StyleBox:
	var art: StyleBoxTexture = painted_box("bar/track")
	return art if art != null else flat(WELL, RADIUS_ROUND, LINE_COLOR)


## A bar's fill in `color`, round-ended.
static func bar_fill(color: Color) -> StyleBox:
	var art: StyleBoxTexture = painted_box("bar/fill")
	if art != null:
		art.modulate_color = color
		return art
	return flat(color, RADIUS_ROUND)


## Draws a bar on `canvas` (the HUD's health bars): the track in `rect`, `fraction` of it filled with `color` and a light
## line along the fill, and `marks` evenly spaced notches. Draws with the shared scratch box: nothing is allocated.
static func draw_bar(canvas: CanvasItem, rect: Rect2, fraction: float, color: Color, marks: int = 0) -> void:
	draw_box(canvas, rect, WELL, RADIUS_ROUND, LINE_COLOR)
	var inner: Rect2 = rect.grow(-float(LINE + 1))
	if fraction > 0.0:
		var width: float = maxf(inner.size.x * clampf(fraction, 0.0, 1.0), inner.size.y)
		draw_box(canvas, Rect2(inner.position, Vector2(width, inner.size.y)), color, RADIUS_ROUND)
		var gloss: float = maxf(inner.size.y * 0.25, 2.0)
		draw_box(canvas, Rect2(inner.position + Vector2(inner.size.y * 0.4, gloss * 0.6), Vector2(maxf(width - inner.size.y * 0.8, gloss), gloss)), BAR_GLOSS, RADIUS_ROUND)
	for mark: int in range(1, marks):
		var x: float = inner.position.x + inner.size.x * float(mark) / float(marks)
		canvas.draw_line(Vector2(x, inner.position.y + LINE), Vector2(x, inner.end.y - LINE), Color(LINE_COLOR, 0.3), 2.0)


## A thin lit outline for a focused control, round a control of corner `radius` (kept flat so it reads on any surface).
static func focus_ring(radius: int = BUTTON_RADIUS) -> StyleBoxFlat:
	var box: StyleBoxFlat = flat(Color.TRANSPARENT, radius, Color("fff0a8"))
	box.set_expand_margin_all(float(LINE + 1))
	return box


## The kit's one flat box (every StyleBoxFlat of the interface is made here): `fill` with a corner `radius` from the scale,
## and an edge LINE px wide in `edge` (none when transparent).
static func flat(fill: Color, radius: int, edge: Color = Color.TRANSPARENT) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	_shape(box, fill, radius, edge)
	return box


## Gives `box` the one shadow.
static func with_shadow(box: StyleBoxFlat) -> StyleBoxFlat:
	box.shadow_color = SHADOW_COLOR
	box.shadow_size = SHADOW_SOFT
	box.shadow_offset = Vector2(0.0, SHADOW_OFFSET)
	return box


## A card frame or a picked rim: `fill` inside a FRAME-wide `rim` (the one heavier edge, see FRAME).
static func framed(fill: Color, radius: int, rim: Color) -> StyleBoxFlat:
	var box: StyleBoxFlat = flat(fill, radius, rim)
	box.set_border_width_all(FRAME)
	return box


## Draws a flat box on `canvas` with the shared scratch box (the HUD's drawn shapes): `fill`, `radius`, an optional LINE
## `edge`, an optional heavier `frame` width instead of LINE, and the shadow when `shadow`.
static func draw_box(canvas: CanvasItem, rect: Rect2, fill: Color, radius: int, edge: Color = Color.TRANSPARENT, shadow: bool = false, frame: int = LINE) -> void:
	if _scratch == null:
		_scratch = StyleBoxFlat.new()
	_shape(_scratch, fill, radius, edge)
	if edge.a > 0.0:
		_scratch.set_border_width_all(frame)
	if shadow:
		with_shadow(_scratch)
	else:
		_scratch.shadow_size = 0
	canvas.draw_style_box(_scratch, rect)


static func _shape(box: StyleBoxFlat, fill: Color, radius: int, edge: Color) -> void:
	box.bg_color = fill
	box.set_corner_radius_all(radius)
	box.border_color = edge
	box.set_border_width_all(LINE if edge.a > 0.0 else 0)
	box.anti_aliasing = true


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
