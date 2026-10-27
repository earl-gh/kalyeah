class_name HandCard
extends CardFace
## One card of the hand (a weapon card, the NEXT card, the card under the finger, the card in flight) as an old Filipino teks card, with the
## SAME look as the mode cards of the main screen (`CardFace`: `card_picture.gdshader` wear, `card_tint.gdshader` age tint, `CardPowerLine`),
## just smaller: it is printed in the weapon's kind colour with the painted skill icon on it, worn away at the corners (a little along the sides) to
## thin tan paper and, nearest the edge, to brown paperboard, with its own slight radius on each corner, an aged tint over all and the
## weapon's power up the right edge (`WeaponDef.card_power`, cosmetic). The wear comes from the weapon (`CardStyle.wear_seed`), so a card
## looks the same in the hand, in NEXT, under the finger and in flight.
##
## A weapon the owner has made a picture for (`CardStyle.picture`) is drawn AS that picture instead, a bare card (`CardFace.bare`): the
## picture already is a card (its own torn cardboard frame, art and glow), so there is no ground, no wear, no age tint, no power line and no
## card behind it; it is fitted into the slot with its own proportions. Its silhouette is not a rectangle, so a wash, the red flash and the
## lift cue (the soft shadow and the gold edge, both behind it) are the picture's own alpha drawn flat (`silhouette.gdshader`).
##
## The card is a node of its own because a shader works on one CanvasItem: the hand draws what must lie over it (the ice water drop, the
## washes, the words, the flash, the gold edge of a lifted card) on overlays drawn after it.

## How much stronger the wear and the corners are than a plain scaling of the mode card's would be: a 123 px card has a third of the size
## of a mode card, and its wear would be a few pixels (this is tuned by eye on the phone-sized render).
const WEAR_BOOST: float = 1.5
## The power line on a card this small: a bigger share of its height than on a mode card, with a thinner outline.
const POWER_SHARE: float = 0.105
const POWER_OUTLINE_PX: int = 4
## The one flat-silhouette material every picture card shares.
static var _silhouette: ShaderMaterial
## A 1 px see-through picture: a card with no art (the back of a card) is printed on its ground alone.
static var _blank: ImageTexture

## The weapon this card shows (null: a card back, or nothing yet).
var weapon: WeaponDef
## The owner's picture the card is drawn as (null: the teks card).
var picture: Texture2D
## What lies over a picture card, from its own silhouette (a teks card gets these as polygons from the hand): a wash over all of it, a
## wash over the top `shade_top_px` pixels of the picture, and a flash over all of it, on top. Clear colours draw nothing.
var shade_all: Color = Color(0.0, 0.0, 0.0, 0.0)
var shade_top: Color = Color(0.0, 0.0, 0.0, 0.0)
var shade_top_px: float = 0.0
var flash: Color = Color(0.0, 0.0, 0.0, 0.0)
## The card shows its back.
var back: bool = false

var _shade: Control


func _init() -> void:
	super()
	name = "HandCard"
	# a card is placed by `place()`, never stretched with its parent
	set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
	shadowed = false
	power_size_share = POWER_SHARE
	power_outline = POWER_OUTLINE_PX
	visible = false
	# a picture is a big image drawn small: its mipmaps keep it smooth wherever the card is (also in flight, outside the hand)
	texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	_shade = Control.new()
	_shade.name = "Shade"
	_shade.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_shade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_shade.material = silhouette_material()
	_shade.draw.connect(_draw_shade)
	add_child(_shade)


## Shows the card of `def` (nothing changes if it already does): the owner's picture when the weapon has one, else the teks card.
func show_weapon(def: WeaponDef) -> void:
	if def == weapon and not back:
		return
	weapon = def
	back = false
	picture = CardStyle.picture(def.id)
	if picture != null:
		_show_picture()
		return
	_show_teks(def)


## The teks card: the kind colour with the painted icon on it, worn.
func _show_teks(def: WeaponDef) -> void:
	bare = false
	material = null
	var art: Texture2D = Icons.skill(def.id)
	texture = art if art != null else _blank_texture()
	ground = CardStyle.ground(def.kind)
	power = def.card_power
	wear_seed = CardStyle.wear_seed(def.id)
	clear_shades()


## The picture card: the picture alone, bare, nothing printed behind it.
func _show_picture() -> void:
	bare = true
	material = silhouette_material()
	ground = Color(0.0, 0.0, 0.0, 0.0)
	texture = picture
	power = 0
	art_inset = 0.0
	clear_shades()


## Shows the back of a card on its way to the slot `slot` (every slot's back wears differently).
func show_back(slot: int) -> void:
	if back and weapon == null:
		return
	weapon = null
	back = true
	picture = null
	bare = false
	material = null
	clear_shades()
	texture = _blank_texture()
	ground = CardStyle.BACK_GROUND
	power = 0
	wear_seed = CardStyle.wear_seed(StringName("back_%d" % slot))


## Puts the card at `rect` (in its parent's pixels): its wear, corners and art edge keep their proportions at that size (a card shrunk over
## the street, a card in flight).
func place(rect: Rect2) -> void:
	position = rect.position
	size = rect.size
	var scale_now: float = rect.size.x / REFERENCE_WIDTH * WEAR_BOOST
	if not is_equal_approx(scale_now, unit):
		unit = scale_now
	art_inset = 0.0 if back or picture != null else rect.size.x * CardStyle.ART_EDGE_SHARE
	_shade.queue_redraw()
	queue_redraw()


## The card is the owner's picture.
func has_picture() -> bool:
	return picture != null


## Where the picture is drawn on the card (own pixels): fitted into the card with its own proportions, centred.
func picture_rect() -> Rect2:
	return CardStyle.picture_rect(Rect2(Vector2.ZERO, size), picture)


## Sets what lies over a picture card (see `shade_all`); a teks card ignores it. Redraws only when something changed.
func set_shades(all: Color, top: Color, top_px: float, flashing: Color) -> void:
	if picture == null:
		all = Color(0.0, 0.0, 0.0, 0.0)
		top = all
		flashing = all
		top_px = 0.0
	if all == shade_all and top == shade_top and is_equal_approx(top_px, shade_top_px) and flashing == flash:
		return
	shade_all = all
	shade_top = top
	shade_top_px = top_px
	flash = flashing
	_shade.queue_redraw()


func clear_shades() -> void:
	set_shades(Color(0.0, 0.0, 0.0, 0.0), Color(0.0, 0.0, 0.0, 0.0), 0.0, Color(0.0, 0.0, 0.0, 0.0))


## Draws the washes and the flash from the picture's own alpha (a black silhouette it is not: the flat colour is what is drawn).
func _draw_shade() -> void:
	if picture == null or size.x < 8.0:
		return
	var shown: Rect2 = picture_rect()
	if shade_all.a > 0.0:
		_shade.draw_texture_rect(picture, shown, false, shade_all)
	if shade_top.a > 0.0 and shade_top_px > 0.0:
		var share: float = clampf(shade_top_px / shown.size.y, 0.0, 1.0)
		var band: Rect2 = Rect2(shown.position, Vector2(shown.size.x, shown.size.y * share))
		var source: Rect2 = Rect2(Vector2.ZERO, Vector2(picture.get_width(), picture.get_height() * share))
		_shade.draw_texture_rect_region(picture, band, source, shade_top)
	if flash.a > 0.0:
		_shade.draw_texture_rect(picture, shown, false, flash)


## A lifted picture card (the one under the finger): a soft shadow, then a gold edge, both the picture's own silhouette and both BEHIND it
## (this is drawn before the card's children). A teks card keeps the polygon shadow and the gold line the hand draws over it.
func _draw() -> void:
	if picture == null:
		super._draw()
		return
	if not shadowed or size.x < 8.0:
		return
	var shown: Rect2 = picture_rect()
	for layer: int in CardStyle.PICTURE_SHADOW_LAYERS:
		var grow: float = CardStyle.PICTURE_SHADOW_GROW * float(layer)
		var shadow: Rect2 = Rect2(shown.position + Vector2(-grow, CardStyle.PICTURE_SHADOW_DROP - grow), shown.size + Vector2(grow, grow) * 2.0)
		draw_texture_rect(picture, shadow, false, Color(0.0, 0.0, 0.0, CardStyle.PICTURE_SHADOW_ALPHA))
	for step: int in CardStyle.PICTURE_EDGE_STEPS:
		var at: Vector2 = Vector2.from_angle(TAU * float(step) / float(CardStyle.PICTURE_EDGE_STEPS)) * CardStyle.LIFT_EDGE
		draw_texture_rect(picture, Rect2(shown.position + at, shown.size), false, RoyaleStyle.GOLD)


## The card's own outline (in its own pixels): the corners the shader cuts, for the washes and the gold edge drawn over it.
func outline() -> PackedVector2Array:
	return CardStyle.outline(size, corner_radii())


## The flat-silhouette material (`silhouette.gdshader`) every picture card shares.
static func silhouette_material() -> ShaderMaterial:
	if _silhouette == null:
		_silhouette = ShaderMaterial.new()
		_silhouette.shader = load("res://shaders/silhouette.gdshader") as Shader
	return _silhouette


static func _blank_texture() -> ImageTexture:
	if _blank == null:
		var image: Image = Image.create(1, 1, false, Image.FORMAT_RGBA8)
		image.fill(Color(0.0, 0.0, 0.0, 0.0))
		_blank = ImageTexture.create_from_image(image)
	return _blank
