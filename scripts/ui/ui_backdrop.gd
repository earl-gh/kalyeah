class_name UiBackdrop
extends Control
## The living background of the main screens: the owner's blue woven-pattern picture (the same for every mode: owner) under a night gradient,
## a soft blue backlight, and a few motes of light drifting up. With Reduce motion on the motes stay home and the picture holds still.

const PICTURE: Texture2D = preload("res://assets/ui/main_bg.webp")
const MOTES: int = 14
## The blue the picture is in, which the backlight and the dark of the vignette follow.
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

## The street picture: a little larger than the screen (`TILT_ROOM` on each side) so the Home parallax can shift it without an edge.
var picture: TextureRect
var glow: TextureRect
## The neon blue core above the backlight: smaller and stronger, centred on the same place.
var core: TextureRect
## The dark of the vignette: the picture's blue, very dark, so the dark that closes in on the picture is never another hue mixed into it.
var night: TextureRect
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
	motes.initial_velocity_min = 22.0
	motes.initial_velocity_max = 52.0
	motes.scale_amount_min = 0.25
	motes.scale_amount_max = 0.9
	var ramp: Gradient = Gradient.new()
	ramp.offsets = PackedFloat32Array([0.0, 0.2, 0.8, 1.0])
	ramp.colors = PackedColorArray([Color(1, 1, 1, 0), Color(1, 1, 1, 0.5), Color(1, 1, 1, 0.5), Color(1, 1, 1, 0)])
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


## The vignette's dark: the focus colour at a very low brightness (the blue of the old navy gradient for the blue card).
static func _night_color(accent: Color) -> Color:
	return Color.from_hsv(accent.h, 0.9, 0.12)


func _apply_motion() -> void:
	var on: bool = ambient and not Settings.reduced_motion
	motes.emitting = on
	motes.visible = on


func _fit_motes() -> void:
	motes.position = Vector2(size.x / 2.0, size.y + 20.0)
	motes.emission_rect_extents = Vector2(size.x / 2.0, 4.0)


## A square soft disc: `core` is the colour in the middle fading to transparent at the edge.
static func _radial(core: Color, edge_alpha: float, pixels: int = 256) -> GradientTexture2D:
	var texture: GradientTexture2D = GradientTexture2D.new()
	texture.width = pixels
	texture.height = pixels
	texture.fill = GradientTexture2D.FILL_RADIAL
	texture.fill_from = Vector2(0.5, 0.5)
	texture.fill_to = Vector2(1.0, 0.5)
	var gradient: Gradient = Gradient.new()
	gradient.offsets = PackedFloat32Array([0.0, 1.0])
	gradient.colors = PackedColorArray([core, Color(core, edge_alpha)])
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


## Darker at the top and the bottom, so the bars and the buttons sit on calm ground.
static func _night() -> GradientTexture2D:
	var texture: GradientTexture2D = GradientTexture2D.new()
	texture.width = 8
	texture.height = 256
	texture.fill_from = Vector2(0.0, 0.0)
	texture.fill_to = Vector2(0.0, 1.0)
	var gradient: Gradient = Gradient.new()
	gradient.offsets = PackedFloat32Array([0.0, 0.28, 0.62, 1.0])
	gradient.colors = PackedColorArray([Color(1, 1, 1, 0.5), Color(1, 1, 1, 0.14), Color(1, 1, 1, 0.12), Color(1, 1, 1, 0.55)])
	texture.gradient = gradient
	return texture


## The Home parallax (`HomeParallax`): shifts the street picture by `shift` px (each axis within `TILT_ROOM`), keeping it over the screen.
func set_tilt(shift: Vector2) -> void:
	var clamped: Vector2 = shift.clamp(-Vector2.ONE * TILT_ROOM, Vector2.ONE * TILT_ROOM)
	picture.offset_left = -TILT_ROOM + clamped.x
	picture.offset_right = TILT_ROOM + clamped.x
	picture.offset_top = -TILT_ROOM + clamped.y
	picture.offset_bottom = TILT_ROOM + clamped.y
