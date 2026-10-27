class_name UiBackdrop
extends Control
## The living background of the main screens: the owner's diamond picture dimmed under a night
## gradient, a soft glow in the colour of whatever is in focus, and a few motes of light drifting
## up. Everything is drawn by the engine from one tiny texture (cheap on phones); with Reduce
## motion on the motes stay home and the picture holds still.

const PICTURE: Texture2D = preload("res://assets/ui/main_bg.webp")
const MOTES: int = 14
## The blue the picture is in (the blue mode card's colour): the focus colour only turns the picture's hue away from this.
const PICTURE_BLUE: Color = Color("4aa0ff")
const TINT_SHADER: Shader = preload("res://shaders/backdrop_tint.gdshader")
## How strongly the backlight is added to the picture.
const BACKLIGHT_STRENGTH: float = 0.4

var glow: TextureRect
var motes: CPUParticles2D
## False while a page or a sheet covers the screen: the motes rest (nothing under a cover needs redrawing).
var ambient: bool = true:
	set(value):
		ambient = value
		if motes != null:
			_apply_motion()
var _accent: Color = PICTURE_BLUE
var _picture_material: ShaderMaterial
var _hue_shift: float = 0.0
var _tween: Tween


func _init() -> void:
	name = "Backdrop"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	var picture: TextureRect = TextureRect.new()
	picture.name = "Picture"
	picture.texture = PICTURE
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	picture.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	# the picture takes the colour of what is in focus (the chosen mode card): see `set_accent`
	_picture_material = ShaderMaterial.new()
	_picture_material.shader = TINT_SHADER
	_picture_material.set_shader_parameter("hue_shift", _target_shift(_accent))
	_hue_shift = _target_shift(_accent)
	picture.material = _picture_material
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
	glow.modulate = _backlight_color(_accent)
	add_child(glow)
	var night: TextureRect = TextureRect.new()
	night.name = "Night"
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


## The background takes the colour of what is in focus (a mode, a map), easing over to it: the backlight takes the colour and the
## picture's blue turns to it (blue stays as it is behind the blue card).
func set_accent(color: Color) -> void:
	_accent = color
	if is_instance_valid(_tween):
		_tween.kill()
	var from_shift: float = _hue_shift
	# the shortest way round the colour wheel from where the picture is now to where it should be
	var to_shift: float = from_shift + wrapf(_target_shift(color) - from_shift, -0.5, 0.5)
	var from_light: Color = glow.modulate
	if Settings.reduced_motion or not is_inside_tree():
		_apply(to_shift, _backlight_color(color))
		return
	_tween = create_tween()
	_tween.tween_method(func(mix: float) -> void: _apply(lerpf(from_shift, to_shift, mix), from_light.lerp(_backlight_color(color), mix)), 0.0, 1.0, 0.45).set_trans(Tween.TRANS_SINE)


func _apply(shift: float, light: Color) -> void:
	_hue_shift = shift
	glow.modulate = light
	_picture_material.set_shader_parameter("hue_shift", shift)


## How far the focus colour is round the colour wheel from the picture's own blue (the blue mode card's colour).
static func _target_shift(accent: Color) -> float:
	return accent.h - PICTURE_BLUE.h


## Lights the background from behind a card: the bright middle of the backlight sits at `centre` (this control's own pixels) and
## fades out over `radius`.
func aim_backlight(centre: Vector2, radius: float) -> void:
	glow.set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
	glow.position = centre - Vector2(radius, radius)
	glow.size = Vector2(radius, radius) * 2.0


## The backlight's colour: the focus colour, lighter and richer, at a strength that reads as light behind the card.
static func _backlight_color(accent: Color) -> Color:
	var color: Color = accent.lightened(0.15)
	color.s = minf(color.s * 1.05, 1.0)
	return Color(color, BACKLIGHT_STRENGTH)


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
	gradient.colors = PackedColorArray([Color(0.01, 0.03, 0.12, 0.76), Color(0.02, 0.05, 0.2, 0.24), Color(0.02, 0.05, 0.2, 0.22), Color(0.01, 0.02, 0.1, 0.8)])
	texture.gradient = gradient
	return texture
