class_name KomiksStyle
extends RefCounted
## Sunlit 90s Philippine Kalye brand surface and signboard kit: hand-painted provincial wood/tin
## plaques, warm sunlit cream cards (`#FAF3E3`), sari-sari jade teal (`#1F9D8B`), mango amber
## (`#F5A623`), terracotta coral (`#E2553E`), and soft golden-hour sun-ray backdrops.
## Replaces heavy dark comic-boom borders with clean, airy tropical signboard craft.

const WORDMARK: Texture2D = preload("res://assets/ui/brand/kalyeah_wordmark.png")
const CREST: Texture2D = preload("res://assets/ui/brand/kalyeah_crest.png")

const SUNLIT_CREAM: Color = Color("faf3e3")
const CREAM_SHADE: Color = Color("f2e2c4")
const SLATE_ESPRESSO: Color = Color("2a2522")
const ESPRESSO_RAISED: Color = Color("38312d")
const MANGO_AMBER: Color = Color("f5a623")
const MANGO_GOLD: Color = Color("ffd466")
const JADE_TEAL: Color = Color("1f9d8b")
const TERRACOTTA_CORAL: Color = Color("e2553e")
const NARRA_WOOD: Color = Color("7a421d")
const SAND_BORDER: Color = Color("d9c5a3")
const WARM_SHADOW: Color = Color(0.08, 0.06, 0.05, 0.24)


## Warm sunlit cream (`#FAF3E3`) card with a soft 2px sand-wood border and gentle afternoon shadow.
static func cream_card(padding: float = UiMetrics.PADDING, radius: int = UiSkin.RADIUS_L, accent: Color = SAND_BORDER) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = SUNLIT_CREAM
	box.set_corner_radius_all(radius)
	box.set_border_width_all(2)
	box.border_color = accent
	box.shadow_color = WARM_SHADOW
	box.shadow_size = 4
	box.shadow_offset = Vector2(0.0, 3.0)
	box.set_content_margin_all(padding)
	box.anti_aliasing = true
	return box


## Hand-painted provincial signboard surface (`#2A2522`) trimmed in mango amber or sari-sari jade.
static func signboard_card(padding: float = UiMetrics.PADDING, radius: int = UiSkin.RADIUS_L, trim: Color = MANGO_AMBER) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = SLATE_ESPRESSO
	box.set_corner_radius_all(radius)
	box.set_border_width_all(2)
	box.border_color = trim
	box.shadow_color = Color(0.08, 0.06, 0.05, 0.30)
	box.shadow_size = 5
	box.shadow_offset = Vector2(0.0, 3.0)
	box.set_content_margin_all(padding)
	box.anti_aliasing = true
	return box


## Rounded sari-sari tag pill sized off `UiSkin.round_radius(height)`.
static func pill_badge(fill: Color = JADE_TEAL, border: Color = SLATE_ESPRESSO, height: float = 40.0, pad_x: float = 16.0, pad_y: float = 6.0) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = fill
	box.set_corner_radius_all(UiSkin.round_radius(height))
	box.set_border_width_all(2)
	box.border_color = border
	box.content_margin_left = pad_x
	box.content_margin_right = pad_x
	box.content_margin_top = pad_y
	box.content_margin_bottom = pad_y
	box.anti_aliasing = true
	return box


## Painted tin banner strip for section headers and callouts.
static func banner_strip(fill: Color = MANGO_AMBER, radius: int = UiSkin.RADIUS_M, pad_x: float = 20.0, pad_y: float = 10.0) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = fill
	box.set_corner_radius_all(radius)
	box.set_border_width_all(2)
	box.border_color = SLATE_ESPRESSO
	box.shadow_color = WARM_SHADOW
	box.shadow_size = 3
	box.shadow_offset = Vector2(0.0, 2.0)
	box.content_margin_left = pad_x
	box.content_margin_right = pad_x
	box.content_margin_top = pad_y
	box.content_margin_bottom = pad_y
	box.anti_aliasing = true
	return box


## Ready-to-mount brand wordmark `TextureRect` (`assets/ui/brand/kalyeah_wordmark.png`, 3.2:1 aspect).
static func wordmark_rect(max_height: float = 104.0) -> TextureRect:
	var art: TextureRect = TextureRect.new()
	art.name = "KalyeahWordmark"
	art.texture = WORDMARK
	art.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	art.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	art.custom_minimum_size = Vector2(max_height * UiMetrics.WORDMARK_ASPECT, max_height)
	art.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return art


## Ready-to-mount brand crest `TextureRect` (`assets/ui/brand/kalyeah_crest.png`, 1:1 aspect).
static func crest_rect(edge: float = 88.0) -> TextureRect:
	var art: TextureRect = TextureRect.new()
	art.name = "KalyeahCrest"
	art.texture = CREST
	art.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	art.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	art.custom_minimum_size = Vector2(edge, edge)
	art.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return art


## Styles a heading `Label` in the hand-painted signboard display face (`KalyeahDisplay`).
static func style_signboard_title(label: Label, font_size: int = UiMetrics.TITLE, fill: Color = SUNLIT_CREAM, outline: Color = SLATE_ESPRESSO) -> void:
	label.add_theme_font_override("font", KalyeahTheme.DISPLAY_FONT)
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", fill)
	label.add_theme_color_override("font_outline_color", outline)
	label.add_theme_constant_override("outline_size", UiSkin.letter_outline(font_size))
	label.add_theme_color_override("font_shadow_color", WARM_SHADOW)
	label.add_theme_constant_override("shadow_offset_x", 0)
	label.add_theme_constant_override("shadow_offset_y", maxi(UiSkin.letter_shadow(font_size), 1))


## Draws warm afternoon Philippine sun-ray wedges behind a signboard or backdrop (subtle radial fans, never jagged boom spikes).
static func draw_sunlit_rays(canvas: CanvasItem, origin: Vector2, reach: float, ray_count: int = 12, color: Color = Color(0.96, 0.65, 0.14, 0.14), phase: float = 0.0) -> void:
	if ray_count <= 0 or reach <= 1.0:
		return
	var step: float = TAU / float(ray_count)
	var half_wedge: float = step * 0.24
	for i: int in ray_count:
		var mid: float = phase + float(i) * step
		var pts: PackedVector2Array = PackedVector2Array([
			origin,
			origin + Vector2(cos(mid - half_wedge), sin(mid - half_wedge)) * reach,
			origin + Vector2(cos(mid + half_wedge), sin(mid + half_wedge)) * reach,
		])
		canvas.draw_colored_polygon(pts, color)


## Draws a painted 90s sari-sari tin signboard plaque with inner trim and four brass corner rivets.
static func draw_signboard_frame(canvas: CanvasItem, rect: Rect2, fill: Color = SUNLIT_CREAM, trim: Color = JADE_TEAL, radius: int = UiSkin.RADIUS_L) -> void:
	var outer: StyleBoxFlat = StyleBoxFlat.new()
	outer.bg_color = fill
	outer.border_color = SLATE_ESPRESSO
	outer.set_border_width_all(2)
	outer.set_corner_radius_all(radius)
	outer.shadow_color = WARM_SHADOW
	outer.shadow_size = 4
	outer.shadow_offset = Vector2(0.0, 3.0)
	outer.anti_aliasing = true
	canvas.draw_style_box(outer, rect)
	var inset: float = 6.0
	if rect.size.x > inset * 4.0 and rect.size.y > inset * 4.0:
		var inner_rect: Rect2 = rect.grow(-inset)
		var inner_box: StyleBoxFlat = StyleBoxFlat.new()
		inner_box.bg_color = Color.TRANSPARENT
		inner_box.border_color = Color(trim, 0.72)
		inner_box.set_border_width_all(2)
		inner_box.set_corner_radius_all(UiSkin.RADIUS_M if radius >= UiSkin.RADIUS_L else UiSkin.RADIUS_S)
		inner_box.anti_aliasing = true
		canvas.draw_style_box(inner_box, inner_rect)
		var rivet_off: float = inset + 6.0
		var corners: Array[Vector2] = [
			rect.position + Vector2(rivet_off, rivet_off),
			Vector2(rect.end.x - rivet_off, rect.position.y + rivet_off),
			Vector2(rect.position.x + rivet_off, rect.end.y - rivet_off),
			rect.end - Vector2(rivet_off, rivet_off),
		]
		for pt: Vector2 in corners:
			canvas.draw_circle(pt, 3.0, MANGO_AMBER)
			canvas.draw_arc(pt, 3.0, 0.0, TAU, 12, SLATE_ESPRESSO, 1.0, true)


## Builds an 8-point asymmetric chamfered MOBA polygon (angled top-left & bottom-right hero cuts, minor opposite bevels).
static func chamfered_points(rect: Rect2, cut_major: float = 16.0, cut_minor: float = 5.0) -> PackedVector2Array:
	var maj: float = minf(cut_major, minf(rect.size.x, rect.size.y) * 0.34)
	var mnr: float = minf(cut_minor, minf(rect.size.x, rect.size.y) * 0.18)
	var x0: float = rect.position.x
	var y0: float = rect.position.y
	var x1: float = rect.end.x
	var y1: float = rect.end.y
	return PackedVector2Array([
		Vector2(x0 + maj, y0),
		Vector2(x1 - mnr, y0),
		Vector2(x1, y0 + mnr),
		Vector2(x1, y1 - maj),
		Vector2(x1 - maj, y1),
		Vector2(x0 + mnr, y1),
		Vector2(x0, y1 - mnr),
		Vector2(x0, y0 + maj),
	])


## Draws a radiant metallic gold chamfered primary CTA button (Wild Rift / MLBB benchmark).
static func draw_chamfered_gold_cta(canvas: CanvasItem, rect: Rect2, hovered: bool, pressed: bool, glint_phase: float = -1.0) -> void:
	if rect.size.x < 12.0 or rect.size.y < 12.0:
		return
	var shift_y: float = 1.5 if pressed else 0.0
	var body_rect: Rect2 = Rect2(rect.position + Vector2(0.0, shift_y), rect.size)
	if not pressed:
		var shadow_pts: PackedVector2Array = chamfered_points(Rect2(body_rect.position + Vector2(0.0, 4.0), body_rect.size), 16.0, 5.0)
		canvas.draw_colored_polygon(shadow_pts, Color(0.04, 0.03, 0.02, 0.48))

	var outer_pts: PackedVector2Array = chamfered_points(body_rect, 16.0, 5.0)
	var top_gold: Color = Color("fff09e") if hovered else Color("ffe373")
	var mid_gold: Color = Color("ffbb38") if hovered else MANGO_AMBER
	var bot_gold: Color = Color("c97006") if pressed else Color("d67b08")
	var cols: PackedColorArray = PackedColorArray([
		top_gold,
		top_gold,
		mid_gold,
		bot_gold,
		bot_gold,
		bot_gold.darkened(0.06),
		mid_gold,
		top_gold,
	])
	canvas.draw_polygon(outer_pts, cols)

	# Specular champagne inner bevel & subtle upper gloss band
	var inner_rect: Rect2 = body_rect.grow(-3.0)
	if inner_rect.size.x > 8.0 and inner_rect.size.y > 8.0:
		var inner_pts: PackedVector2Array = chamfered_points(inner_rect, 14.0, 4.0)
		var inner_loop: PackedVector2Array = inner_pts.duplicate()
		inner_loop.append(inner_pts[0])
		canvas.draw_polyline(inner_loop, Color(SUNLIT_CREAM, 0.62 if hovered else 0.46), 1.6, true)

	if glint_phase >= 0.0 and glint_phase <= 1.0 and not pressed:
		var cx: float = lerpf(body_rect.position.x + 28.0, body_rect.end.x - 28.0, glint_phase)
		var band_w: float = 26.0
		var slant: float = 18.0
		var g_top: float = body_rect.position.y + 4.0
		var g_bot: float = body_rect.end.y - 4.0
		var glint_poly: PackedVector2Array = PackedVector2Array([
			Vector2(cx - band_w * 0.5 + slant, g_top),
			Vector2(cx + band_w * 0.5 + slant, g_top),
			Vector2(cx + band_w * 0.5 - slant, g_bot),
			Vector2(cx - band_w * 0.5 - slant, g_bot),
		])
		canvas.draw_colored_polygon(glint_poly, Color(1.0, 0.98, 0.88, 0.20))

	# Outer dark bronze-espresso armor border + gold outer hairline
	var rim_loop: PackedVector2Array = outer_pts.duplicate()
	rim_loop.append(outer_pts[0])
	canvas.draw_polyline(rim_loop, Color("261b12"), 2.4, true)


## Draws a sleek chamfered dark-glass card/pill with luminous accent rim for mode selectors and action capsules.
static func draw_chamfered_glass_card(canvas: CanvasItem, rect: Rect2, accent: Color, active: bool = false, hovered: bool = false, cut_major: float = 11.0) -> void:
	if rect.size.x < 10.0 or rect.size.y < 10.0:
		return
	var pts: PackedVector2Array = chamfered_points(rect, cut_major, 4.0)
	var shadow_pts: PackedVector2Array = chamfered_points(Rect2(rect.position + Vector2(0.0, 2.5), rect.size), cut_major, 4.0)
	canvas.draw_colored_polygon(shadow_pts, Color(0.03, 0.02, 0.02, 0.34))
	var fill: Color = KalyeahTheme.GLASS_ACTIVE if active else (KalyeahTheme.GLASS_HOVER if hovered else KalyeahTheme.GLASS_DARK)
	canvas.draw_colored_polygon(pts, fill)
	var loop: PackedVector2Array = pts.duplicate()
	loop.append(pts[0])
	var rim_alpha: float = 0.88 if active else (0.58 if hovered else 0.30)
	canvas.draw_polyline(loop, Color(accent, rim_alpha), 2.0 if active else 1.4, true)
	if active:
		var bar_y0: float = rect.position.y + cut_major + 2.0
		var bar_y1: float = rect.end.y - 6.0
		if bar_y1 > bar_y0:
			canvas.draw_line(Vector2(rect.position.x + 2.5, bar_y0), Vector2(rect.position.x + 2.5, bar_y1), accent, 3.0, true)


## Crisp anti-aliased vector iconography for modern MOBA header, rail, and stage controls.
static func draw_stage_glyph(canvas: CanvasItem, glyph: StringName, center: Vector2, radius: float, color: Color) -> void:
	var r: float = maxf(radius, 4.0)
	match glyph:
		&"play":
			var tri: PackedVector2Array = PackedVector2Array([
				center + Vector2(-r * 0.46, -r * 0.68),
				center + Vector2(r * 0.74, 0.0),
				center + Vector2(-r * 0.46, r * 0.68),
			])
			canvas.draw_colored_polygon(tri, color)
		&"kids":
			canvas.draw_circle(center + Vector2(-r * 0.24, -r * 0.28), r * 0.28, color)
			canvas.draw_arc(center + Vector2(-r * 0.24, r * 0.56), r * 0.52, PI * 1.12, PI * 1.88, 12, color, 2.4, true)
			canvas.draw_circle(center + Vector2(r * 0.34, -r * 0.16), r * 0.22, Color(color, color.a * 0.82))
			canvas.draw_arc(center + Vector2(r * 0.34, r * 0.56), r * 0.40, PI * 1.15, PI * 1.85, 10, Color(color, color.a * 0.82), 2.0, true)
		&"arsenal":
			# Stylized crossed tirador Y-slingshot + star spark
			canvas.draw_line(center + Vector2(0.0, r * 0.72), center + Vector2(0.0, r * 0.05), color, 2.6, true)
			canvas.draw_line(center + Vector2(0.0, r * 0.05), center + Vector2(-r * 0.52, -r * 0.56), color, 2.5, true)
			canvas.draw_line(center + Vector2(0.0, r * 0.05), center + Vector2(r * 0.52, -r * 0.56), color, 2.5, true)
			canvas.draw_circle(center + Vector2(0.0, -r * 0.38), r * 0.20, color)
		&"ranked":
			var shield: PackedVector2Array = PackedVector2Array([
				center + Vector2(0.0, -r * 0.78),
				center + Vector2(r * 0.66, -r * 0.42),
				center + Vector2(r * 0.52, r * 0.26),
				center + Vector2(0.0, r * 0.78),
				center + Vector2(-r * 0.52, r * 0.26),
				center + Vector2(-r * 0.66, -r * 0.42),
			])
			var loop: PackedVector2Array = shield.duplicate()
			loop.append(shield[0])
			canvas.draw_polyline(loop, color, 2.2, true)
			var gem: PackedVector2Array = PackedVector2Array([
				center + Vector2(0.0, -r * 0.34),
				center + Vector2(r * 0.26, 0.0),
				center + Vector2(0.0, r * 0.34),
				center + Vector2(-r * 0.26, 0.0),
			])
			canvas.draw_colored_polygon(gem, color)
		&"trophy":
			canvas.draw_arc(center + Vector2(0.0, -r * 0.18), r * 0.48, 0.0, PI, 12, color, 2.3, true)
			canvas.draw_line(center + Vector2(-r * 0.48, -r * 0.18), center + Vector2(-r * 0.48, -r * 0.56), color, 2.3, true)
			canvas.draw_line(center + Vector2(r * 0.48, -r * 0.18), center + Vector2(r * 0.48, -r * 0.56), color, 2.3, true)
			canvas.draw_line(center + Vector2(-r * 0.54, -r * 0.56), center + Vector2(r * 0.54, -r * 0.56), color, 2.3, true)
			canvas.draw_line(center + Vector2(0.0, r * 0.30), center + Vector2(0.0, r * 0.64), color, 2.4, true)
			canvas.draw_line(center + Vector2(-r * 0.38, r * 0.64), center + Vector2(r * 0.38, r * 0.64), color, 2.5, true)
		&"settings":
			canvas.draw_arc(center, r * 0.34, 0.0, TAU, 16, color, 2.2, true)
			for i: int in 8:
				var ang: float = float(i) * TAU / 8.0
				var dir: Vector2 = Vector2(cos(ang), sin(ang))
				canvas.draw_line(center + dir * (r * 0.46), center + dir * (r * 0.74), color, 2.3, true)
		&"tips":
			var page: Rect2 = Rect2(center - Vector2(r * 0.60, r * 0.56), Vector2(r * 1.20, r * 1.16))
			canvas.draw_rect(page, color, false, 2.0)
			canvas.draw_line(page.position + Vector2(0.0, r * 0.30), Vector2(page.end.x, page.position.y + r * 0.30), color, 1.8, true)
			canvas.draw_circle(center + Vector2(0.0, r * 0.14), r * 0.18, color)
		&"mail":
			var env: Rect2 = Rect2(center - Vector2(r * 0.68, r * 0.46), Vector2(r * 1.36, r * 0.92))
			canvas.draw_rect(env, color, false, 2.0)
			var flap: PackedVector2Array = PackedVector2Array([
				env.position,
				center + Vector2(0.0, r * 0.08),
				Vector2(env.end.x, env.position.y),
			])
			canvas.draw_polyline(flap, color, 1.9, true)
		&"chevron_left":
			var cl: PackedVector2Array = PackedVector2Array([
				center + Vector2(r * 0.26, -r * 0.54),
				center + Vector2(-r * 0.30, 0.0),
				center + Vector2(r * 0.26, r * 0.54),
			])
			canvas.draw_polyline(cl, color, 2.6, true)
		&"chevron_right":
			var cr: PackedVector2Array = PackedVector2Array([
				center + Vector2(-r * 0.26, -r * 0.54),
				center + Vector2(r * 0.30, 0.0),
				center + Vector2(-r * 0.26, r * 0.54),
			])
			canvas.draw_polyline(cr, color, 2.6, true)
		&"chevron_down":
			var cd: PackedVector2Array = PackedVector2Array([
				center + Vector2(-r * 0.48, -r * 0.20),
				center + Vector2(0.0, r * 0.28),
				center + Vector2(r * 0.48, -r * 0.20),
			])
			canvas.draw_polyline(cd, color, 2.4, true)
		&"edit":
			canvas.draw_line(center + Vector2(-r * 0.46, r * 0.46), center + Vector2(r * 0.42, -r * 0.42), color, 2.2, true)
			canvas.draw_circle(center + Vector2(-r * 0.52, r * 0.52), 1.6, color)
