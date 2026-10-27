class_name UiBackdrop
extends Control
## The living background of the main screens: one of the owner's diamond pictures (blue, purple or green, whichever is nearest to the
## colour in focus; they cross-fade) dimmed under a night gradient, a soft glow in the colour of whatever is in focus, and a few motes
## of light drifting up. With Reduce motion on the motes stay home and the picture holds still.

const PICTURE: Texture2D = preload("res://assets/ui/main_bg.webp")
const PICTURE_PURPLE: Texture2D = preload("res://assets/ui/main_bg_purple.webp")
const PICTURE_GREEN: Texture2D = preload("res://assets/ui/main_bg_green.webp")
const MOTES: int = 14
## The colour each picture is in: the blue, the purple and the green mode cards' colours, in the order of `pictures`.
const PICTURE_BLUE: Color = Color("4aa0ff")
const PICTURE_COLOURS: Array[Color] = [PICTURE_BLUE, Color("a45cf0"), Color("45dd78")]
## How strongly the backlight is added to the picture.
const BACKLIGHT_STRENGTH: float = 0.4

var glow: TextureRect
## The dark of the vignette: the focus colour, very dark, so the dark that closes in on the picture is never another hue mixed into it
## (a navy dark turned an orange picture into grey brown).
var night: TextureRect
var motes: CPUParticles2D
## False while a page or a sheet covers the screen: the motes rest (nothing under a cover needs redrawing).
var ambient: bool = true:
	set(value):
		ambient = value
		if motes != null:
			_apply_motion()
var _accent: Color = PICTURE_BLUE
## The pictures (blue, purple, green): only the one nearest the focus colour is showing.
var pictures: Array[TextureRect] = []
var _tween: Tween


func _init() -> void:
	name = "Backdrop"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	var textures: Array[Texture2D] = [PICTURE, PICTURE_PURPLE, PICTURE_GREEN]
	var names: Array[String] = ["Picture", "PicturePurple", "PictureGreen"]
	for i: int in textures.size():
		var picture: TextureRect = TextureRect.new()
		picture.name = names[i]
		picture.texture = textures[i]
		picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
		picture.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
		picture.modulate.a = 1.0 if i == picture_for(_accent) else 0.0
		add_child(picture)
		pictures.append(picture)
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
	night = TextureRect.new()
	night.name = "Night"
	night.modulate = _night_color(_accent)
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
## picture nearest to it fades in.
func set_accent(color: Color) -> void:
	_accent = color
	if is_instance_valid(_tween):
		_tween.kill()
	var target: int = picture_for(color)
	var from_alpha: Array[float] = []
	for picture: TextureRect in pictures:
		from_alpha.append(picture.modulate.a)
	var from_light: Color = glow.modulate
	var from_night: Color = night.modulate
	if Settings.reduced_motion or not is_inside_tree():
		_apply(target, 1.0, from_alpha, _backlight_color(color), _night_color(color))
		return
	_tween = create_tween()
	_tween.tween_method(func(mix: float) -> void: _apply(target, mix, from_alpha, from_light.lerp(_backlight_color(color), mix), from_night.lerp(_night_color(color), mix)), 0.0, 1.0, 0.45).set_trans(Tween.TRANS_SINE)


## `mix` of the way from the pictures' alphas at the start (`from_alpha`) to only `target` showing.
func _apply(target: int, mix: float, from_alpha: Array[float], light: Color, dark: Color) -> void:
	for i: int in pictures.size():
		pictures[i].modulate.a = lerpf(from_alpha[i], 1.0 if i == target else 0.0, mix)
	glow.modulate = light
	night.modulate = dark


## Which picture (0 blue, 1 purple, 2 green) is nearest to a focus colour round the colour wheel.
static func picture_for(accent: Color) -> int:
	var best: int = 0
	var best_distance: float = INF
	for i: int in PICTURE_COLOURS.size():
		var distance: float = absf(wrapf(accent.h - PICTURE_COLOURS[i].h, -0.5, 0.5))
		if distance < best_distance:
			best = i
			best_distance = distance
	return best


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
