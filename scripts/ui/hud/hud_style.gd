class_name HudStyle
extends RefCounted
## Shared minimalist white-outline HUD tokens & drawing helpers for landscape 5v5 combat
## and street mini-games: every action button and joystick uses faint translucent white fill,
## crisp white rim (`WHITE_RIM`), clockwise white sweep (`WHITE_SWEEP`), and pure white icons.

## The gap between the screen's edge (inside the safe area) and every control.
const EDGE: float = 24.0
## The header (the score and the clock) is drawn this size in landscape: the screen is 720 px tall, not 1280 (owner, 2026-11-09: smaller).
const HEADER_SCALE: float = 0.65

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

## Legacy elixir-mode touch control colors.
const FILL: Color = Color(0.035, 0.1, 0.27, 0.38)
const RIM: Color = Color(0.93, 0.95, 1.0, 0.45)
const THUMB: Color = Color(0.93, 0.95, 1.0, 0.7)
const RIM_WIDTH: float = 3.0

## White-only movement & combat button tokens (RGB strictly 1.0, 1.0, 1.0).
const WHITE_BODY: Color = Color(1.0, 1.0, 1.0, 0.14)
const WHITE_BODY_HELD: Color = Color(1.0, 1.0, 1.0, 0.26)
const WHITE_RIM: Color = Color(1.0, 1.0, 1.0, 0.85)
const WHITE_THUMB: Color = Color(1.0, 1.0, 1.0, 0.85)
const WHITE_OFF: float = 0.45
const WHITE_RIM_WIDTH: float = 3.0
const WHITE_SWEEP: Color = Color(1.0, 1.0, 1.0, 0.22)
const WORD_SHADOW: Color = Color(0.0, 0.0, 0.0, 0.35)
const WORD_SHADOW_DROP: float = 2.0

static var _icon_cache: Dictionary[StringName, Texture2D] = {}


## The outline width for words of `font_size`.
static func outline(font_size: int) -> int:
	return maxi(3, roundi(float(font_size) * OUTLINE_SHARE))


## Letters `label` in the HUD's ink at `font_size` in `color`.
static func letter(label: Label, font_size: int, color: Color) -> void:
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", color)
	label.add_theme_color_override("font_outline_color", INK)
	label.add_theme_constant_override("outline_size", outline(font_size))


## Draws a white-only control on `canvas` (the movement controls and every combat button): faint white body and crisp white rim.
static func draw_white_disc(canvas: CanvasItem, middle: Vector2, radius: float) -> void:
	canvas.draw_circle(middle, radius, WHITE_BODY)
	canvas.draw_arc(middle, radius, 0.0, TAU, 48, WHITE_RIM, WHITE_RIM_WIDTH, true)


## Same white-only disc with a soft white brighten while held/pressed (still pure RGB 1,1,1).
static func draw_white_button(canvas: CanvasItem, middle: Vector2, radius: float, held: bool = false) -> void:
	canvas.draw_circle(middle, radius, WHITE_BODY_HELD if held else WHITE_BODY)
	canvas.draw_arc(middle, radius, 0.0, TAU, 48, WHITE_RIM, WHITE_RIM_WIDTH, true)


## A button's charge or recovery inside its rim: a faint white sweep of `share` (0 to 1), clockwise from the top.
static func draw_white_sweep(canvas: CanvasItem, middle: Vector2, radius: float, share: float) -> void:
	var points: PackedVector2Array = sweep_points(middle, radius, share)
	if points.size() > 2:
		canvas.draw_colored_polygon(points, WHITE_SWEEP)


## The sweep's outline: the middle and the arc of `share` just inside the rim (empty for no charge).
static func sweep_points(middle: Vector2, radius: float, share: float) -> PackedVector2Array:
	var filled: float = clampf(share, 0.0, 1.0)
	var points: PackedVector2Array = PackedVector2Array()
	if filled <= 0.0:
		return points
	points.append(middle)
	var steps: int = maxi(2, ceili(48.0 * filled))
	for i: int in steps + 1:
		points.append(middle + Vector2.from_angle(-PI / 2.0 + TAU * filled * float(i) / float(steps)) * (radius - WHITE_RIM_WIDTH / 2.0))
	return points


## Draws a round touch control on `canvas`: the see-through fill and the rim.
static func draw_disc(canvas: CanvasItem, middle: Vector2, radius: float, rim: Color = RIM) -> void:
	canvas.draw_circle(middle, radius, FILL)
	canvas.draw_arc(middle, radius, 0.0, TAU, 48, rim, RIM_WIDTH, true)


## Loads `res://assets/ui/hud_icons/<icon_id>.png` with editorless raw PNG fallback before `--import` runs.
static func hud_icon(icon_id: StringName) -> Texture2D:
	if icon_id == &"":
		return null
	var resolved_id: StringName = &"tirador" if icon_id == &"tirador_bato" else icon_id
	if _icon_cache.has(resolved_id):
		return _icon_cache[resolved_id]
	var res_path: String = "res://assets/ui/hud_icons/%s.png" % String(resolved_id)
	var tex: Texture2D = null
	if ResourceLoader.exists(res_path):
		tex = load(res_path) as Texture2D
	if tex == null and FileAccess.file_exists(res_path):
		var img: Image = Image.new()
		if img.load(ProjectSettings.globalize_path(res_path)) == OK and not img.is_empty():
			tex = ImageTexture.create_from_image(img)
	if tex != null:
		_icon_cache[resolved_id] = tex
	return tex


## Draws the pure-white RGBA icon from `assets/ui/hud_icons/` or a crisp pure-white `#FFFFFF` vector silhouette fallback.
static func draw_white_icon_or_glyph(canvas: CanvasItem, center: Vector2, radius: float, icon_id: StringName, alpha: float = 0.95) -> void:
	var col: Color = Color(1.0, 1.0, 1.0, clampf(alpha, 0.0, 1.0))
	var tex: Texture2D = hud_icon(icon_id)
	if tex != null:
		var half: Vector2 = Vector2.ONE * (radius * 0.68)
		canvas.draw_texture_rect(tex, Rect2(center - half, half * 2.0), false, col)
		return
	_draw_white_vector_fallback(canvas, center, radius * 0.50, String(icon_id), col)


static func _draw_white_vector_fallback(canvas: CanvasItem, at: Vector2, s: float, key: String, col: Color) -> void:
	if key == "tirador" or key == "tirador_bato":
		var fl: Vector2 = at + Vector2(-s * 0.56, -s * 0.52)
		var fr: Vector2 = at + Vector2(s * 0.56, -s * 0.52)
		var mid: Vector2 = at + Vector2(0.0, s * 0.08)
		var bot: Vector2 = at + Vector2(0.0, s * 0.78)
		var pouch: Vector2 = at + Vector2(0.0, -s * 0.14)
		canvas.draw_line(fl, mid, col, 3.2, true)
		canvas.draw_line(fr, mid, col, 3.2, true)
		canvas.draw_line(mid, bot, col, 3.4, true)
		canvas.draw_line(fl, pouch, Color(1.0, 1.0, 1.0, col.a * 0.7), 1.8, true)
		canvas.draw_line(fr, pouch, Color(1.0, 1.0, 1.0, col.a * 0.7), 1.8, true)
		canvas.draw_circle(pouch, maxf(s * 0.2, 2.5), col)
	elif key == "ult_bagsak":
		var pts: PackedVector2Array = PackedVector2Array()
		for i: int in 8:
			var ang: float = -PI * 0.5 + TAU * float(i) / 8.0
			var rad: float = s * (0.95 if i % 2 == 0 else 0.40)
			pts.append(at + Vector2.from_angle(ang) * rad)
		canvas.draw_colored_polygon(pts, col)
	elif key == "vault":
		for off_y: float in [-s * 0.24, s * 0.26]:
			var l_pt: Vector2 = at + Vector2(-s * 0.58, off_y + s * 0.2)
			var tip: Vector2 = at + Vector2(0.0, off_y - s * 0.32)
			var r_pt: Vector2 = at + Vector2(s * 0.58, off_y + s * 0.2)
			canvas.draw_polyline(PackedVector2Array([l_pt, tip, r_pt]), col, 2.8, true)
	elif key == "jump":
		var tip_j: Vector2 = at + Vector2(0.0, -s * 0.68)
		canvas.draw_polyline(PackedVector2Array([at + Vector2(-s * 0.58, -s * 0.05), tip_j, at + Vector2(s * 0.58, -s * 0.05)]), col, 3.0, true)
		canvas.draw_line(tip_j, at + Vector2(0.0, s * 0.52), col, 2.8, true)
		canvas.draw_line(at + Vector2(-s * 0.45, s * 0.68), at + Vector2(s * 0.45, s * 0.68), Color(1.0, 1.0, 1.0, col.a * 0.65), 2.0, true)
	elif key == "crouch":
		canvas.draw_circle(at + Vector2(s * 0.12, -s * 0.42), maxf(s * 0.22, 2.4), col)
		canvas.draw_polyline(PackedVector2Array([
			at + Vector2(s * 0.05, -s * 0.18),
			at + Vector2(-s * 0.28, s * 0.14),
			at + Vector2(s * 0.28, s * 0.28),
			at + Vector2(s * 0.05, s * 0.62),
		]), col, 2.8, true)
	elif key == "slide":
		canvas.draw_circle(at + Vector2(-s * 0.34, -s * 0.22), maxf(s * 0.2, 2.2), col)
		canvas.draw_line(at + Vector2(-s * 0.25, 0.0), at + Vector2(s * 0.62, s * 0.32), col, 3.0, true)
		canvas.draw_line(at + Vector2(-s * 0.65, s * 0.48), at + Vector2(s * 0.55, s * 0.48), Color(1.0, 1.0, 1.0, col.a * 0.65), 1.8, true)
	elif key == "gear":
		var gr: float = s * 0.62
		canvas.draw_arc(at, gr, 0.0, TAU, 24, col, 2.2, true)
		canvas.draw_circle(at, gr * 0.34, col)
		for t_idx: int in 6:
			var a: float = TAU * float(t_idx) / 6.0
			var d: Vector2 = Vector2.from_angle(a)
			canvas.draw_line(at + d * (gr - 0.5), at + d * (gr + s * 0.32), col, 2.6, true)
	elif key == "heal":
		canvas.draw_line(at + Vector2(-s * 0.55, 0.0), at + Vector2(s * 0.55, 0.0), col, 3.4, true)
		canvas.draw_line(at + Vector2(0.0, -s * 0.55), at + Vector2(0.0, s * 0.55), col, 3.4, true)
	else:
		# Clean crisp diamond + inner ring fallback for any street-game weapon before PNG import
		var dm: PackedVector2Array = PackedVector2Array([
			at + Vector2(0.0, -s * 0.68),
			at + Vector2(s * 0.68, 0.0),
			at + Vector2(0.0, s * 0.68),
			at + Vector2(-s * 0.68, 0.0),
			at + Vector2(0.0, -s * 0.68),
		])
		canvas.draw_polyline(dm, col, 2.6, true)
		canvas.draw_circle(at, maxf(s * 0.22, 2.2), col)
