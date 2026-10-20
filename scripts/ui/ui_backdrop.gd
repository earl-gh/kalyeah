class_name UiBackdrop
extends Control
## The living background of the main screens: the owner's diamond picture dimmed under a night
## gradient, a soft glow in the colour of whatever is in focus, and a few motes of light drifting
## up. Everything is drawn by the engine from one tiny texture (cheap on phones); with Reduce
## motion on the motes stay home and the picture holds still.

const PICTURE: Texture2D = preload("res://assets/ui/main_bg.webp")
const MOTES: int = 14

var glow: TextureRect
var motes: CPUParticles2D
var _accent: Color = Color("3d7bff")
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
	picture.modulate = Color(0.62, 0.68, 0.9)
	picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(picture)
	glow = TextureRect.new()
	glow.name = "Glow"
	glow.texture = _radial(Color.WHITE, 0.0)
	glow.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	glow.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	glow.mouse_filter = Control.MOUSE_FILTER_IGNORE
	glow.modulate = Color(_accent, 0.5)
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


## The glow takes the colour of what is in focus (a mode, a map), easing over to it.
func set_accent(color: Color) -> void:
	_accent = color
	if is_instance_valid(_tween):
		_tween.kill()
	if Settings.reduced_motion or not is_inside_tree():
		glow.modulate = Color(color, 0.5)
		return
	_tween = create_tween()
	_tween.tween_property(glow, "modulate", Color(color, 0.5), 0.45).set_trans(Tween.TRANS_SINE)


func _apply_motion() -> void:
	motes.emitting = not Settings.reduced_motion
	motes.visible = not Settings.reduced_motion


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


## Darker at the top and the bottom, so the bars and the buttons sit on calm ground.
static func _night() -> GradientTexture2D:
	var texture: GradientTexture2D = GradientTexture2D.new()
	texture.width = 8
	texture.height = 256
	texture.fill_from = Vector2(0.0, 0.0)
	texture.fill_to = Vector2(0.0, 1.0)
	var gradient: Gradient = Gradient.new()
	gradient.offsets = PackedFloat32Array([0.0, 0.28, 0.62, 1.0])
	gradient.colors = PackedColorArray([Color(0.01, 0.03, 0.12, 0.88), Color(0.02, 0.05, 0.2, 0.3), Color(0.02, 0.05, 0.2, 0.28), Color(0.01, 0.02, 0.1, 0.92)])
	texture.gradient = gradient
	return texture
