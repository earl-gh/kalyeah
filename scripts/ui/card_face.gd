class_name CardFace
extends Control
## A mode card drawn as an old Filipino teks card (straight, flat): a soft shadow just under it (only on the chosen card), then the
## picture with its print worn away to brown paper (shader `card_picture`), then the power line up the right edge, then a subtle
## yellowish tint over all of it to make it look old (shader `card_tint`). No coloured shade over the picture (owner). The card's own
## button draws nothing behind it. No glow behind the card (owner).

const PICTURE_SHADER: Shader = preload("res://shaders/card_picture.gdshader")
const TINT_SHADER: Shader = preload("res://shaders/card_tint.gdshader")
const BLUR_SHADER: Shader = preload("res://shaders/model_blur.gdshader")
## The width of a full-size mode card, the size the wear, the radii and the shadow below are made for: a smaller card (the hand's) has a
## `unit` of its width over this (and the same look in proportion).
const REFERENCE_WIDTH: float = 385.0
## The slight radius of each corner: between these, a different one on every corner of every card (never sharp, never very round).
const CORNER_RADIUS_MIN: float = 12.0
const CORNER_RADIUS_MAX: float = 16.0
## The old brown-yellow tint over the whole card (owner: a browner yellow, a little stronger).
const AGE_TINT: Color = Color(0.86, 0.6, 0.24, 0.17)
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
## The shadow: compact, close under the card (its layers, how far it falls and how dark the whole of it is).
const SHADOW_LAYERS: int = 6
const SHADOW_DROP: Vector2 = Vector2(3.0, 13.0)
const SHADOW_GROW: float = 0.009
const SHADOW_ALPHA: float = 0.13
## How much further the shadow falls (and lighter it gets) as the card floats up.
const SHADOW_BREATH: float = 4.0
## A bare picture: what an unchosen one is multiplied by (a dark, nearly black tint) and how flat its ground shadow is.
const BARE_DIM: Color = Color(0.36, 0.36, 0.4)
const GROUND_SHADOW_SQUASH: float = 0.16
## How blurred a bare picture is when it stands far behind (the blur radius in texels of the picture, owner: depth of field): 0 when chosen.
const BLUR_RADIUS: float = 20.0
## The model is the active one, at the centre, from this `chosen` on (1 only when its swipe has settled).
const PLATE_ACTIVE: float = 0.98
## The name on the platform fades in over this many seconds after the model settles, and goes at once when it leaves.
const PLATE_FADE_IN: float = 0.3

## How high the card floats (-1 sinking to 1 at the top of its drift): the middle card sets it as it drifts, and its shadow follows.
var lift: float = 0.0:
	set(value):
		if absf(value - lift) < 0.01:
			return
		lift = value
		queue_redraw()
## How much the card is the chosen one, 0 to 1: only the chosen card has a shadow;
## a card that is not chosen is dimmed with a dark tint (still in colour).
var chosen: float = 1.0:
	set(value):
		if absf(value - chosen) < 0.01:
			return
		chosen = value
		# a card that is not chosen is only dimmed (a dark tint, still in colour: owner)
		dim = 1.0 - value
		# a bare picture has no shader: it is dimmed with a dark tint of its own (owner: darker)
		_picture.modulate = Color.WHITE.lerp(BARE_DIM, 1.0 - value) if bare else Color.WHITE
		# the name on the platform fades in once the model is the active one and at the centre, and is gone when it leaves
		_show_plate(value >= PLATE_ACTIVE)
		_blur_for_depth()
		queue_redraw()
## How dark the card is, 0 (as printed) to 1 (the dark tint of a card that is not chosen): the picture is multiplied by it and the yellow
## tint goes as it darkens. A card of the hand that cannot be used is dimmed with it.
var dim: float = 0.0:
	set(value):
		dim = clampf(value, 0.0, 1.0)
		_picture_material.set_shader_parameter("dim", dim)
		_tint_material.set_shader_parameter("tint", Color(AGE_TINT, AGE_TINT.a * (1.0 - dim)))
## A face with a ground is printed on that flat colour (alpha 1) with its `texture` as a small art on it, `art_inset` px in from the card's
## edges (the hand's weapon cards); with no ground (alpha 0) the picture is the whole print (the mode cards).
var ground: Color = Color(0.0, 0.0, 0.0, 0.0):
	set(value):
		ground = value
		_picture_material.set_shader_parameter("ground", value)
		_fit()
var art_inset: float = 0.0:
	set(value):
		art_inset = value
		_fit()
## This card's size in proportion to a full-size mode card (`REFERENCE_WIDTH`): the wear, the radii and the shadow keep their look at a
## smaller size.
var unit: float = 1.0:
	set(value):
		unit = maxf(value, 0.01)
		for material: ShaderMaterial in _materials():
			material.set_shader_parameter("unit", unit)
		_fit()
		queue_redraw()
## The soft shadow under the card (the chosen mode card, a card held in the hand).
var shadowed: bool = true:
	set(value):
		shadowed = value
		queue_redraw()
## The power line's letter height over the card's height and the width of its outline (a small card has a bigger share of both).
var power_size_share: float = POWER_SIZE_SHARE:
	set(value):
		power_size_share = value
		_power_line.size_share = value
var power_outline: int = POWER_OUTLINE_SIZE:
	set(value):
		power_outline = value
		_power_line.outline_size = value

## A bare face shows its picture as it is, a transparent PNG (a diorama) with no card paper, wear, corners, tint or power line: the card
## is only the shell that sizes, places and dims it (owner). The worn teks card above stays for another part of the game.
var bare: bool = false:
	set(value):
		bare = value
		_picture.material = null if value else _picture_material
		_blur_for_depth()
		_picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED if value else TextureRect.STRETCH_SCALE
		_power_line.visible = not value
		_tint_rect.visible = not value
		_plate.visible = value and not _plate.text.is_empty()
		queue_redraw()
## Writes the mode's name on the front face of the platform (`face`: that wall on the picture, in the picture's pixels).
func set_plate(title: String, face: Rect2) -> void:
	_plate.picture_size = texture.get_size() if texture != null else Vector2.ONE
	_plate.face = face
	_plate.text = title
	_plate_on = chosen >= PLATE_ACTIVE
	_plate.modulate.a = 1.0 if _plate_on else 0.0
	_plate.visible = bare


## Fades the name in once the model is the centred one, hides it at once when it is not (instant with reduced motion).
func _show_plate(on: bool) -> void:
	if on == _plate_on:
		return
	_plate_on = on
	if _plate_tween != null:
		_plate_tween.kill()
	if not on or Settings.reduced_motion or not is_inside_tree():
		_plate.modulate.a = 1.0 if on else 0.0
		return
	_plate_tween = create_tween()
	_plate_tween.tween_property(_plate, "modulate:a", 1.0, PLATE_FADE_IN)


## The name written on the platform.
func plate() -> ModePlate:
	return _plate


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
var _plate: ModePlate
var _plate_on: bool = true
var _plate_tween: Tween
var _picture_material: ShaderMaterial
var _power_line: CardPowerLine
var _tint_material: ShaderMaterial
var _tint_rect: ColorRect
var _blur_material: ShaderMaterial


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
	_tint_rect = tint
	_plate = ModePlate.new()
	_plate.visible = false
	add_child(_plate)


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		_fit()
		queue_redraw()


func _draw() -> void:
	if size.x < 8.0 or size.y < 8.0:
		return
	if chosen < 0.02 or not shadowed:
		return
	if bare:
		_draw_ground_shadow()
		return
	var face: PackedVector2Array = CardShape.outline_corners(size, corner_radii())
	var centre: Vector2 = size / 2.0
	for layer: int in SHADOW_LAYERS:
		var grow: float = 1.0 + SHADOW_GROW * float(layer)
		var shadow: PackedVector2Array = PackedVector2Array()
		for point: Vector2 in face:
			shadow.append(centre + (point - centre) * grow + (SHADOW_DROP + Vector2(0.0, SHADOW_BREATH * lift)) * unit)
		draw_colored_polygon(shadow, Color(0.0, 0.02, 0.08, SHADOW_ALPHA * (1.0 - 0.3 * lift) * chosen))


## A bare picture standing behind the chosen one is blurred (it is far away): the blur grows as it goes back, and the chosen one has no
## blur material at all (so it costs nothing and stays sharp).
func _blur_for_depth() -> void:
	if not bare:
		return
	var radius: float = BLUR_RADIUS * (1.0 - chosen)
	if radius < 0.5:
		_picture.material = null
		return
	if _blur_material == null:
		_blur_material = ShaderMaterial.new()
		_blur_material.shader = BLUR_SHADER
	_blur_material.set_shader_parameter("radius", radius)
	_picture.material = _blur_material


## A bare picture stands on a soft shadow on the ground: a flat ellipse under its base, only for the chosen one.
func _draw_ground_shadow() -> void:
	if texture == null:
		return
	var picture_size: Vector2 = texture.get_size()
	var shown: float = minf(size.x / picture_size.x, size.y / picture_size.y)
	var width: float = picture_size.x * shown
	var foot: Vector2 = Vector2(size.x / 2.0, (size.y + picture_size.y * shown) / 2.0 - picture_size.y * shown * 0.04 + SHADOW_BREATH * lift)
	draw_set_transform(foot, 0.0, Vector2(1.0, GROUND_SHADOW_SQUASH))
	for layer: int in SHADOW_LAYERS:
		var grow: float = 1.0 - 0.07 * float(layer)
		draw_circle(Vector2.ZERO, width * 0.5 * grow, Color(0.0, 0.02, 0.08, SHADOW_ALPHA * 1.4 * chosen))
	draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)


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
	_picture_material.set_shader_parameter("art_rect", Vector4(art_inset, art_inset, maxf(size.x - 2.0 * art_inset, 1.0), maxf(size.x - 2.0 * art_inset, 1.0)))


func _materials() -> Array[ShaderMaterial]:
	return [_picture_material, _tint_material]


## The radius of each corner of this card (top left, top right, bottom right, bottom left): slight, and different on every corner
## and every card.
func corner_radii() -> Vector4:
	var radii: Array[float] = []
	for corner: int in 4:
		var unit_radius: float = fposmod(sin(wear_seed * (12.9898 + 21.7 * float(corner)) + 4.1 + 3.3 * float(corner)) * 43758.5453, 1.0)
		radii.append(lerpf(CORNER_RADIUS_MIN, CORNER_RADIUS_MAX, unit_radius) * unit)
	return Vector4(radii[0], radii[1], radii[2], radii[3])

