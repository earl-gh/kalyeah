class_name CardFace
extends Control
## A mode card drawn as an old Filipino teks card floating over the table (straight, flat, no coloured outline: owner): a soft
## shadow well below it, then the picture with its print worn away to brown paper at the corners (shader `card_picture`, one
## layer only), then the power line up the right edge, then the mode-coloured shade at the bottom (shader `card_shade`, which
## wears away at the same corners), then a subtle yellowish tint over all of it to make it look old (shader `card_tint`). The card's own
## button draws nothing behind it.

const PICTURE_SHADER: Shader = preload("res://shaders/card_picture.gdshader")
const SHADE_SHADER: Shader = preload("res://shaders/card_shade.gdshader")
const TINT_SHADER: Shader = preload("res://shaders/card_tint.gdshader")
## The slight radius of each corner: between these, a different one on every corner of every card (never sharp, never very round).
const CORNER_RADIUS_MIN: float = 12.0
const CORNER_RADIUS_MAX: float = 16.0
## The yellowish tint over the whole card (subtle: owner, slightly stronger than first).
const AGE_TINT: Color = Color(1.0, 0.86, 0.45, 0.11)
## Square corners, like a teks card (owner).
const RADIUS: float = 0.0
## The power line at the bottom of the right edge, turned -90 degrees (reading upwards), like a teks card's: white letters with a
## black outline. The number is the card's own (`power`); 0 draws no line.
const POWER_FORMAT: String = "POWER %d"
const POWER_SIZE_SHARE: float = 0.075
const POWER_COLOR: Color = Color.WHITE
const POWER_OUTLINE: Color = Color.BLACK
## Where the line sits: its baseline's distance from the right edge (share of the card's width) and where it starts, up from the
## bottom (share of the card's height), and the outline's width.
const POWER_EDGE_SHARE: float = 0.075
const POWER_BOTTOM_SHARE: float = 0.07
const POWER_OUTLINE_SIZE: int = 7
## The coloured shade in front of the picture at the bottom of the card, in the mode's colour, behind the mode's name: how
## tall it is (share of the card's height) and how strong it is at the bottom.
const SHADE_SHARE: float = 0.36
## The shadow: its layers, how far below the card it falls and how dark the whole of it is.
const SHADOW_LAYERS: int = 7
const SHADOW_DROP: Vector2 = Vector2(10.0, 40.0)
const SHADOW_ALPHA: float = 0.075
## How much further the shadow falls (and lighter it gets) as the card floats up.
const SHADOW_BREATH: float = 10.0

## How high the card floats (-1 sinking to 1 at the top of its drift): the middle card sets it as it drifts, and its shadow follows.
var lift: float = 0.0:
	set(value):
		if absf(value - lift) < 0.01:
			return
		lift = value
		queue_redraw()
## The mode's colour (the coloured shade at the bottom of the card) and its power number (the line up the right edge).
var accent: Color = Color("4aa0ff"):
	set(value):
		accent = value
		_shade_material.set_shader_parameter("accent", value)
var power: int = 0:
	set(value):
		power = value
		_power_line.text = UiText.f(POWER_FORMAT, [value]) if value > 0 else ""
		_power_line.queue_redraw()
var texture: Texture2D:
	set(value):
		texture = value
		_picture.texture = value
		_fit()
## Each card wears differently: this number seeds the noise and decides how round each of its corners is.
var wear_seed: float = 1.0:
	set(value):
		wear_seed = value
		for material: ShaderMaterial in _materials():
			material.set_shader_parameter("seed", value)
		_fit()
		queue_redraw()

var _picture: TextureRect
var _picture_material: ShaderMaterial
var _power_line: CardPowerLine
var _shade: ColorRect
var _shade_material: ShaderMaterial
var _tint_material: ShaderMaterial


func _init() -> void:
	name = "Face"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_picture = TextureRect.new()
	_picture.name = "Picture"
	_picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_picture.stretch_mode = TextureRect.STRETCH_SCALE
	_picture.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_picture_material = ShaderMaterial.new()
	_picture_material.shader = PICTURE_SHADER
	_picture_material.set_shader_parameter("seed", wear_seed)
	_picture.material = _picture_material
	add_child(_picture)
	_power_line = CardPowerLine.new()
	add_child(_power_line)
	_shade = ColorRect.new()
	_shade.name = "Shade"
	_shade.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_shade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_shade_material = ShaderMaterial.new()
	_shade_material.shader = SHADE_SHADER
	_shade_material.set_shader_parameter("share", SHADE_SHARE)
	_shade_material.set_shader_parameter("accent", accent)
	_shade_material.set_shader_parameter("seed", wear_seed)
	_shade.material = _shade_material
	add_child(_shade)
	var tint: ColorRect = ColorRect.new()
	tint.name = "Tint"
	tint.color = AGE_TINT
	tint.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	tint.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_tint_material = ShaderMaterial.new()
	_tint_material.shader = TINT_SHADER
	_tint_material.set_shader_parameter("tint", AGE_TINT)
	_tint_material.set_shader_parameter("seed", wear_seed)
	tint.material = _tint_material
	add_child(tint)


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		_fit()
		queue_redraw()


func _draw() -> void:
	if size.x < 8.0 or size.y < 8.0:
		return
	var radius: float = RADIUS
	var face: PackedVector2Array = CardShape.outline_corners(size, corner_radii())
	var centre: Vector2 = size / 2.0
	for layer: int in SHADOW_LAYERS:
		var grow: float = 1.0 + 0.012 * float(layer)
		var shadow: PackedVector2Array = PackedVector2Array()
		for point: Vector2 in face:
			shadow.append(centre + (point - centre) * grow + SHADOW_DROP + Vector2(0.0, SHADOW_BREATH * lift))
		draw_colored_polygon(shadow, Color(0.0, 0.02, 0.08, SHADOW_ALPHA * (1.0 - 0.3 * lift)))


## The share of the picture the card shows across and down (the picture fills the card, the rest is cropped).
func _cover_scale() -> Vector2:
	var picture: float = texture.get_size().x / texture.get_size().y
	var card: float = size.x / size.y
	if card < picture:
		return Vector2(card / picture, 1.0)
	return Vector2(1.0, picture / card)


## Tells the shaders the card's size and how the picture is cropped to it.
func _fit() -> void:
	for material: ShaderMaterial in _materials():
		material.set_shader_parameter("rect_size", size)
		material.set_shader_parameter("radii", corner_radii())
	if texture != null and size.y > 0.0:
		_picture_material.set_shader_parameter("cover", _cover_scale())


func _materials() -> Array[ShaderMaterial]:
	return [_picture_material, _shade_material, _tint_material]


## The radius of each corner of this card (top left, top right, bottom right, bottom left): slight, and different on every corner
## and every card.
func corner_radii() -> Vector4:
	var radii: Array[float] = []
	for corner: int in 4:
		var unit: float = fposmod(sin(wear_seed * (12.9898 + 21.7 * float(corner)) + 4.1 + 3.3 * float(corner)) * 43758.5453, 1.0)
		radii.append(lerpf(CORNER_RADIUS_MIN, CORNER_RADIUS_MAX, unit))
	return Vector4(radii[0], radii[1], radii[2], radii[3])
