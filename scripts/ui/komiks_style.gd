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
