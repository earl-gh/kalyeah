class_name UiBackdrop
extends Control
## The living sunlit background of the main screens: the owner's woven-pattern picture (`main_bg.webp`)
## bathed in a warm 90s Philippine Kalye afternoon glow, soft golden sun-rays, subtle barrio fiesta
## banderitas in the upper corners, a gentle card backlight, and warm dust motes drifting up.

const PICTURE: Texture2D = preload("res://assets/ui/main_bg.webp")
const MOTES: int = 14
## The blue the picture is in, which the backlight and the vignette hue follow.
const PICTURE_BLUE: Color = Color("4aa0ff")
## How strongly the backlight is added to the picture.
const BACKLIGHT_STRENGTH: float = 0.9
## The neon blue core: a saturated electric blue, how strongly it is added, and how big it is next to the backlight.
const NEON_BLUE: Color = Color("1f66ff")
const NEON_STRENGTH: float = 0.7
const NEON_SIZE: float = 0.55
const NEON_LIFT: float = 0.5
## How long the lights take to come back after a swipe (s).
const LIGHT_FADE: float = 0.25
## Both lights are drawn between the mode models: above the ones behind the chosen model (`MenuCarousel` z 5) and below the chosen one
## (z 10), so they shine from behind the front model but not through the ones behind it (owner).
const LIGHT_Z: int = 7
## How far (px) the picture may shift for the tilt parallax, and so how much larger than the screen it is drawn on each side.
const TILT_ROOM: float = 8.0

## Sunlit 90s Kalye atmospheric palette tokens (warm cream, mango amber, sari-sari jade, terracotta coral, soft espresso).
const SUNLIT_CREAM: Color = Color("faf3e3")
const SUNLIT_MANGO: Color = Color("f5a623")
const SUNLIT_JADE: Color = Color("1f9d8b")
const SUNLIT_CORAL: Color = Color("e2553e")
const SUNLIT_ESPRESSO: Color = Color("2a2522")

## The street picture: a little larger than the screen (`TILT_ROOM` on each side) so the Home parallax can shift it without an edge.
var picture: TextureRect
var glow: TextureRect
## The neon blue core above the backlight: smaller and stronger, centred on the same place.
var core: TextureRect
## The subtle edge vignette: follows the picture's hue at gentle sunlit opacity so headers and nav sit on calm ground.
var night: TextureRect
## Procedural sunlit 90s Philippine Kalye overlay: warm golden afternoon canopy wash, airy sun-rays,
## upper-corner barrio fiesta banderitas, and subtle woven banig dots (zero heavy dark comic slabs, zero per-frame allocations).
var komiks_art: Control
var motes: CPUParticles2D
## The two lights are on while the main page shows (above a page they would shine over its content, so they are switched off there).
var lights_on: bool = true:
	set(value):
		lights_on = value
		_sync_lights()
## True while the main page's models are being swiped or settling: the two lights are off then (nothing to chase the moving
## model), and fade back in on the chosen model once it has settled (`hold_lights`).
var _held: bool = false
var _fade: Tween
## False while a page or a sheet covers the screen: the motes rest (nothing under a cover needs redrawing).
var ambient: bool = true:
	set(value):
		ambient = value
		if motes != null:
			_apply_motion()


func _init() -> void:
	name = "Backdrop"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	picture = TextureRect.new()
	picture.name = "Picture"
	picture.texture = PICTURE
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	picture.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	set_tilt(Vector2.ZERO)
	picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(picture)
	glow = TextureRect.new()
	glow.name = "Glow"
	glow.texture = _backlight()
	glow.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	glow.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	glow.mouse_filter = Control.MOUSE_FILTER_IGNORE
	# light added to the picture (not painted over it): the bright middle lights the background from behind the card
	var additive: CanvasItemMaterial = CanvasItemMaterial.new()
	additive.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
	glow.material = additive
	glow.modulate = _backlight_color(PICTURE_BLUE)
	glow.z_as_relative = false
	glow.z_index = LIGHT_Z
	add_child(glow)
	# a smaller, stronger neon blue core on top of the backlight, right behind the chosen model (owner)
	core = TextureRect.new()
	core.name = "NeonCore"
	core.texture = _backlight()
	core.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	core.set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
	core.mouse_filter = Control.MOUSE_FILTER_IGNORE
	core.material = additive
	core.modulate = Color(NEON_BLUE, NEON_STRENGTH)
	core.z_as_relative = false
	core.z_index = LIGHT_Z
	add_child(core)
	night = TextureRect.new()
	night.name = "Night"
	night.modulate = _night_color(PICTURE_BLUE)
	night.texture = _night()
	night.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	night.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	night.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(night)
	komiks_art = _SunlitKalyeLayer.new()
	komiks_art.name = "KomiksArt"
	komiks_art.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	komiks_art.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(komiks_art)
	motes = CPUParticles2D.new()
	motes.name = "Motes"
	motes.amount = MOTES
	motes.lifetime = 9.0
	motes.preprocess = 9.0
	motes.texture = _radial(Color.WHITE, 0.0, 24)
	motes.emission_shape = CPUParticles2D.EMISSION_SHAPE_RECTANGLE
	motes.direction = Vector2(0.0, -1.0)
	motes.spread = 12.0
	motes.gravity = Vector2.ZERO
	motes.initial_velocity_min = 20.0
	motes.initial_velocity_max = 48.0
	motes.scale_amount_min = 0.25
	motes.scale_amount_max = 0.85
	var ramp: Gradient = Gradient.new()
	ramp.offsets = PackedFloat32Array([0.0, 0.22, 0.78, 1.0])
	ramp.colors = PackedColorArray([
		Color(SUNLIT_CREAM, 0.0),
		Color(SUNLIT_CREAM, 0.52),
		Color(SUNLIT_MANGO.lightened(0.25), 0.44),
		Color(SUNLIT_CREAM, 0.0),
	])
	motes.color_ramp = ramp
	add_child(motes)
	resized.connect(_fit_motes)
	_fit_motes()
	Settings.accessibility_changed.connect(_apply_motion)
	_apply_motion()


## Holds the two lights off (`held`) while the models are swiped, and fades them back in when they are let go (at once with Reduce motion).
func hold_lights(held: bool) -> void:
	if held == _held:
		return
	_held = held
	if _fade != null:
		_fade.kill()
	if held:
		_set_light_alpha(0.0)
	elif Settings.reduced_motion:
		_set_light_alpha(1.0)
	else:
		_set_light_alpha(0.0)
		_fade = create_tween()
		_fade.tween_method(_set_light_alpha, 0.0, 1.0, LIGHT_FADE)
	_sync_lights()


func _set_light_alpha(alpha: float) -> void:
	for light: TextureRect in [glow, core]:
		if light != null:
			light.self_modulate.a = alpha


func _sync_lights() -> void:
	var visible_now: bool = lights_on and not _held
	if glow != null:
		glow.visible = visible_now
	if core != null:
		core.visible = visible_now


## Lights the background from behind a card: the bright middle of the backlight sits at `centre` (this control's own pixels) and
## fades out over `radius`.
func aim_backlight(centre: Vector2, radius: float) -> void:
	glow.set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
	glow.position = centre - Vector2(radius, radius)
	glow.size = Vector2(radius, radius) * 2.0
	var inner: float = radius * NEON_SIZE
	# the core sits a little above the middle, so it shows over the model's roofs and walls, not only round its edges
	var up: Vector2 = Vector2(0.0, -inner * NEON_LIFT)
	core.position = centre + up - Vector2(inner, inner)
	core.size = Vector2(inner, inner) * 2.0


## The backlight's colour: the focus colour, lighter and richer, at a strength that reads as light behind the card.
static func _backlight_color(accent: Color) -> Color:
	var color: Color = accent.lightened(0.15)
	color.s = minf(color.s * 1.05, 1.0)
	return Color(color, BACKLIGHT_STRENGTH)


## The vignette's dark: the focus colour at a very low brightness (`test_card_shape.gd` checks hue & value).
static func _night_color(accent: Color) -> Color:
	return Color.from_hsv(accent.h, 0.9, 0.12)


func _apply_motion() -> void:
	var on: bool = ambient and not Settings.reduced_motion
	motes.emitting = on
	motes.visible = on


func _fit_motes() -> void:
	motes.position = Vector2(size.x / 2.0, size.y + 20.0)
	motes.emission_rect_extents = Vector2(size.x / 2.0, 4.0)


## A square soft disc: `core_color` is the colour in the middle fading to transparent at the edge.
static func _radial(core_color: Color, edge_alpha: float, pixels: int = 256) -> GradientTexture2D:
	var texture: GradientTexture2D = GradientTexture2D.new()
	texture.width = pixels
	texture.height = pixels
	texture.fill = GradientTexture2D.FILL_RADIAL
	texture.fill_from = Vector2(0.5, 0.5)
	texture.fill_to = Vector2(1.0, 0.5)
	var gradient: Gradient = Gradient.new()
	gradient.offsets = PackedFloat32Array([0.0, 1.0])
	gradient.colors = PackedColorArray([core_color, Color(core_color, edge_alpha)])
	texture.gradient = gradient
	return texture


## A soft disc of light: bright in the middle, falling off gently, gone at the edge (a radial gradient, brightness and a little colour).
static func _backlight() -> GradientTexture2D:
	var texture: GradientTexture2D = GradientTexture2D.new()
	texture.width = 256
	texture.height = 256
	texture.fill = GradientTexture2D.FILL_RADIAL
	texture.fill_from = Vector2(0.5, 0.5)
	texture.fill_to = Vector2(1.0, 0.5)
	var gradient: Gradient = Gradient.new()
	gradient.offsets = PackedFloat32Array([0.0, 0.3, 0.6, 0.85, 1.0])
	gradient.colors = PackedColorArray([Color(1, 1, 1, 1.0), Color(1, 1, 1, 0.6), Color(1, 1, 1, 0.22), Color(1, 1, 1, 0.05), Color(1, 1, 1, 0.0)])
	texture.gradient = gradient
	return texture


## Gentle top and bottom framing at light opacity so the sunlit daytime palette stays airy and open.
static func _night() -> GradientTexture2D:
	var texture: GradientTexture2D = GradientTexture2D.new()
	texture.width = 8
	texture.height = 256
	texture.fill_from = Vector2(0.0, 0.0)
	texture.fill_to = Vector2(0.0, 1.0)
	var gradient: Gradient = Gradient.new()
	gradient.offsets = PackedFloat32Array([0.0, 0.26, 0.68, 1.0])
	gradient.colors = PackedColorArray([Color(1, 1, 1, 0.18), Color(1, 1, 1, 0.03), Color(1, 1, 1, 0.02), Color(1, 1, 1, 0.22)])
	texture.gradient = gradient
	return texture


## The Home parallax (`HomeParallax`): shifts the street picture by `shift` px (each axis within `TILT_ROOM`), keeping it over the screen.
func set_tilt(shift: Vector2) -> void:
	var clamped: Vector2 = shift.clamp(-Vector2.ONE * TILT_ROOM, Vector2.ONE * TILT_ROOM)
	picture.offset_left = -TILT_ROOM + clamped.x
	picture.offset_right = TILT_ROOM + clamped.x
	picture.offset_top = -TILT_ROOM + clamped.y
	picture.offset_bottom = TILT_ROOM + clamped.y


class _SunlitKalyeLayer extends Control:
	const RAY_COUNT: int = 11
	const DOT_SPACING: float = 24.0
	const DOT_BAND: float = 108.0
	const FRAME_INSET: float = 10.0

	var _cream_rays: Array[PackedVector2Array] = []
	var _mango_rays: Array[PackedVector2Array] = []
	var _top_canopy: PackedVector2Array = PackedVector2Array()
	var _top_canopy_cols: PackedColorArray = PackedColorArray()
	var _bottom_glow: PackedVector2Array = PackedVector2Array()
	var _bottom_glow_cols: PackedColorArray = PackedColorArray()
	var _bunting_cords: PackedVector2Array = PackedVector2Array()
	var _bunting_flags: Array[PackedVector2Array] = []
	var _bunting_colors: PackedColorArray = PackedColorArray()
	var _banig_dots: PackedVector2Array = PackedVector2Array()
	var _banig_radii: PackedFloat32Array = PackedFloat32Array()
	var _cached_size: Vector2 = Vector2.ZERO

	func _init() -> void:
		resized.connect(_rebuild)

	func _rebuild() -> void:
		if size.x < 16.0 or size.y < 16.0 or size.is_equal_approx(_cached_size):
			return
		_cached_size = size
		_cream_rays.clear()
		_mango_rays.clear()
		_bunting_flags.clear()

		# 1. Warm afternoon sunlight wash from top sky + warm golden ground bounce.
		var canopy_h: float = size.y * 0.44
		_top_canopy = PackedVector2Array([
			Vector2(0.0, 0.0),
			Vector2(size.x, 0.0),
			Vector2(size.x, canopy_h),
			Vector2(0.0, canopy_h),
		])
		var top_warm: Color = Color(UiBackdrop.SUNLIT_MANGO.lerp(UiBackdrop.SUNLIT_CREAM, 0.55), 0.20)
		var top_clear: Color = Color(UiBackdrop.SUNLIT_CREAM, 0.0)
		_top_canopy_cols = PackedColorArray([top_warm, top_warm, top_clear, top_clear])

		var bounce_top: float = size.y * 0.72
		_bottom_glow = PackedVector2Array([
			Vector2(0.0, bounce_top),
			Vector2(size.x, bounce_top),
			Vector2(size.x, size.y),
			Vector2(0.0, size.y),
		])
		var bot_clear: Color = Color(UiBackdrop.SUNLIT_MANGO, 0.0)
		var bot_warm: Color = Color(UiBackdrop.SUNLIT_MANGO, 0.10)
		_bottom_glow_cols = PackedColorArray([bot_clear, bot_clear, bot_warm, bot_warm])

		# 2. Airy golden-hour sun-rays fanning gently downward from above the signboard header.
		var sun_origin: Vector2 = Vector2(size.x * 0.5, -size.y * 0.14)
		var reach: float = size.length() * 0.92
		var start_angle: float = PI * 0.16
		var end_angle: float = PI * 0.84
		var span: float = (end_angle - start_angle) / float(RAY_COUNT)
		for i: int in RAY_COUNT:
			var mid: float = start_angle + (float(i) + 0.5) * span
			var half_w: float = span * 0.28
			var poly: PackedVector2Array = PackedVector2Array([
				sun_origin,
				sun_origin + Vector2(cos(mid - half_w), sin(mid - half_w)) * reach,
				sun_origin + Vector2(cos(mid + half_w), sin(mid + half_w)) * reach,
			])
			if i % 2 == 0:
				_cream_rays.append(poly)
			else:
				_mango_rays.append(poly)

		# 3. Delicate barrio fiesta banderitas tucked into the upper-left and upper-right eaves.
		var cords: PackedVector2Array = PackedVector2Array()
		var flag_cols: PackedColorArray = PackedColorArray()
		var palette: Array[Color] = [
			Color(UiBackdrop.SUNLIT_MANGO, 0.42),
			Color(UiBackdrop.SUNLIT_JADE, 0.40),
			Color(UiBackdrop.SUNLIT_CORAL, 0.38),
			Color(UiBackdrop.SUNLIT_CREAM, 0.44),
		]
		var eave_span: float = clampf(size.x * 0.19, 120.0, 248.0)
		var eave_drop: float = 34.0
		for side: int in 2:
			var p0: Vector2 = Vector2(6.0, eave_drop) if side == 0 else Vector2(size.x - 6.0, eave_drop)
			var p1: Vector2 = Vector2(eave_span, 6.0) if side == 0 else Vector2(size.x - eave_span, 6.0)
			var flag_count: int = 6
			var prev: Vector2 = p0
			for f: int in flag_count:
				var t0: float = float(f) / float(flag_count)
				var t1: float = float(f + 1) / float(flag_count)
				var sag0: float = sin(t0 * PI) * 8.0
				var sag1: float = sin(t1 * PI) * 8.0
				var a: Vector2 = p0.lerp(p1, t0) + Vector2(0.0, sag0)
				var b: Vector2 = p0.lerp(p1, t1) + Vector2(0.0, sag1)
				cords.append(prev if f == 0 else a)
				cords.append(b)
				prev = b
				var mid_pt: Vector2 = (a + b) * 0.5 + Vector2(0.0, 13.0)
				var inset_a: Vector2 = a.lerp(b, 0.12)
				var inset_b: Vector2 = a.lerp(b, 0.88)
				_bunting_flags.append(PackedVector2Array([inset_a, inset_b, mid_pt]))
				flag_cols.append(palette[(f + side * 2) % palette.size()])
		_bunting_cords = cords
		_bunting_colors = flag_cols

		# 4. Soft sunlit woven banig dots in the lower corners (warm cream, zero black ink).
		var dots: PackedVector2Array = PackedVector2Array()
		var radii: PackedFloat32Array = PackedFloat32Array()
		var cols: int = maxi(int(floorf(DOT_BAND / DOT_SPACING)), 1)
		var rows: int = maxi(int(floorf(DOT_BAND / DOT_SPACING)), 1)
		var corners: Array[Vector2] = [
			Vector2(20.0, size.y - 20.0),
			Vector2(size.x - 20.0, size.y - 20.0),
		]
		var dirs: Array[Vector2] = [
			Vector2(1.0, -1.0),
			Vector2(-1.0, -1.0),
		]
		for c_idx: int in corners.size():
			var origin: Vector2 = corners[c_idx]
			var dir: Vector2 = dirs[c_idx]
			for gx: int in cols:
				for gy: int in rows:
					var falloff: float = 1.0 - float(gx + gy) / float(cols + rows)
					if falloff <= 0.18:
						continue
					var shift_x: float = (DOT_SPACING * 0.5) if (gy % 2 == 1) else 0.0
					var pt: Vector2 = origin + Vector2((float(gx) * DOT_SPACING + shift_x) * dir.x, float(gy) * DOT_SPACING * dir.y)
					dots.append(pt)
					radii.append(1.2 + falloff * 2.2)
		_banig_dots = dots
		_banig_radii = radii
		queue_redraw()

	func _draw() -> void:
		if _cached_size == Vector2.ZERO and size.x >= 16.0 and size.y >= 16.0:
			_rebuild()
		if _top_canopy.size() == 4:
			draw_polygon(_top_canopy, _top_canopy_cols)
		if _bottom_glow.size() == 4:
			draw_polygon(_bottom_glow, _bottom_glow_cols)
		var cream_ray: Color = Color(UiBackdrop.SUNLIT_CREAM, 0.065)
		for wedge: PackedVector2Array in _cream_rays:
			draw_colored_polygon(wedge, cream_ray)
		var mango_ray: Color = Color(UiBackdrop.SUNLIT_MANGO, 0.05)
		for wedge: PackedVector2Array in _mango_rays:
			draw_colored_polygon(wedge, mango_ray)
		for i: int in _bunting_flags.size():
			draw_colored_polygon(_bunting_flags[i], _bunting_colors[i])
		if _bunting_cords.size() >= 2:
			draw_multiline(_bunting_cords, Color(UiBackdrop.SUNLIT_ESPRESSO, 0.24), 1.4)
		var warm_dot: Color = Color(UiBackdrop.SUNLIT_CREAM, 0.13)
		for i: int in _banig_dots.size():
			draw_circle(_banig_dots[i], _banig_radii[i], warm_dot)
		var inner_frame: Rect2 = Rect2(Vector2.ONE * FRAME_INSET, size - Vector2.ONE * (FRAME_INSET * 2.0))
		if inner_frame.size.x > 16.0 and inner_frame.size.y > 16.0:
			draw_rect(inner_frame, Color(UiBackdrop.SUNLIT_CREAM, 0.18), false, 1.2)
