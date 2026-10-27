class_name CardFace
extends Control
## A mode card drawn as an old Filipino teks card (straight, flat): a soft shadow just under it (only on the chosen card), then the
## picture with its print worn away to brown paper (shader `card_picture`), then the power line up the right edge, then a subtle
## yellowish tint over all of it to make it look old (shader `card_tint`). No coloured shade over the picture (owner). The card's own
## button draws nothing behind it; the chosen card's thin coloured outline glow is a `CardRim` over this.

const PICTURE_SHADER: Shader = preload("res://shaders/card_picture.gdshader")
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
## The glow behind the chosen card (no outline: owner): lines centred on the card's edge, so only the outer half shows behind the
## picture, a soft fall-off about 12 px wide: wide faint lines first, narrow stronger ones last.
const GLOW_WIDTHS: Array[float] = [24.0, 19.0, 15.0, 11.0, 8.0, 5.0]
const GLOW_ALPHAS: Array[float] = [0.04, 0.06, 0.09, 0.12, 0.16, 0.22]
## The shadow: compact, close under the card (its layers, how far it falls and how dark the whole of it is).
const SHADOW_LAYERS: int = 6
const SHADOW_DROP: Vector2 = Vector2(3.0, 13.0)
const SHADOW_GROW: float = 0.009
const SHADOW_ALPHA: float = 0.13
## How much further the shadow falls (and lighter it gets) as the card floats up.
const SHADOW_BREATH: float = 4.0

## How high the card floats (-1 sinking to 1 at the top of its drift): the middle card sets it as it drifts, and its shadow follows.
var lift: float = 0.0:
	set(value):
		if absf(value - lift) < 0.01:
			return
		lift = value
		queue_redraw()
## The mode's colour: the glow that comes from behind the chosen card.
var accent: Color = Color("4aa0ff"):
	set(value):
		accent = value
		queue_redraw()
## How much the card is the chosen one, 0 to 1: only the chosen card has a shadow and a glow (drawn by its `CardRim`) and is in colour;
## a card that is not chosen is dimmed with a dark tint (still in colour).
var chosen: float = 1.0:
	set(value):
		if absf(value - chosen) < 0.01:
			return
		chosen = value
		# a card that is not chosen is only dimmed (a dark tint, still in colour: owner)
		_picture_material.set_shader_parameter("dim", 1.0 - value)
		_tint_material.set_shader_parameter("tint", Color(AGE_TINT, AGE_TINT.a * value))
		queue_redraw()
## The mode's power number (the line up the right edge).
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
	_picture_material.set_shader_parameter("dim", 0.0)
	_picture.material = _picture_material
	add_child(_picture)
	_power_line = CardPowerLine.new()
	add_child(_power_line)
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
	if chosen < 0.02:
		return
	var face: PackedVector2Array = CardShape.outline_corners(size, corner_radii())
	var centre: Vector2 = size / 2.0
	for layer: int in SHADOW_LAYERS:
		var grow: float = 1.0 + SHADOW_GROW * float(layer)
		var shadow: PackedVector2Array = PackedVector2Array()
		for point: Vector2 in face:
			shadow.append(centre + (point - centre) * grow + SHADOW_DROP + Vector2(0.0, SHADOW_BREATH * lift))
		draw_colored_polygon(shadow, Color(0.0, 0.02, 0.08, SHADOW_ALPHA * (1.0 - 0.3 * lift) * chosen))
	# the glow comes from behind the card: it is drawn first, so the picture covers its inner half and only the outside shows
	for i: int in GLOW_WIDTHS.size():
		_closed(face, Color(accent, GLOW_ALPHAS[i] * chosen), GLOW_WIDTHS[i])


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
	return [_picture_material, _tint_material]


## The radius of each corner of this card (top left, top right, bottom right, bottom left): slight, and different on every corner
## and every card.
func corner_radii() -> Vector4:
	var radii: Array[float] = []
	for corner: int in 4:
		var unit: float = fposmod(sin(wear_seed * (12.9898 + 21.7 * float(corner)) + 4.1 + 3.3 * float(corner)) * 43758.5453, 1.0)
		radii.append(lerpf(CORNER_RADIUS_MIN, CORNER_RADIUS_MAX, unit))
	return Vector4(radii[0], radii[1], radii[2], radii[3])


func _closed(points: PackedVector2Array, color: Color, width: float) -> void:
	var line: PackedVector2Array = points.duplicate()
	line.append(points[0])
	draw_polyline(line, color, width, true)
