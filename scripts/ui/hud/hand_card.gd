class_name HandCard
extends CardFace
## One card of the hand (a weapon card, the NEXT card, the card under the finger, the card in flight) as an old Filipino teks card, with the
## SAME look as the mode cards of the main screen (`CardFace`: `card_picture.gdshader` wear, `card_tint.gdshader` age tint, `CardPowerLine`),
## just smaller: it is printed in the weapon's kind colour with the round art on it, worn away at the corners (a little along the sides) to
## thin tan paper and, nearest the edge, to brown paperboard, with its own slight radius on each corner, an aged tint over all and the
## weapon's power up the right edge (`WeaponDef.card_power`, cosmetic). The wear comes from the weapon (`CardStyle.wear_seed`), so a card
## looks the same in the hand, in NEXT, under the finger and in flight.
##
## The card is a node of its own because a shader works on one CanvasItem: the hand draws what must lie over it (the ice water drop, the
## washes, the words, the flash, the gold edge of a lifted card) on overlays drawn after it.

## How much stronger the wear and the corners are than a plain scaling of the mode card's would be: a 123 px card has a third of the size
## of a mode card, and its wear would be a few pixels (this is tuned by eye on the phone-sized render).
const WEAR_BOOST: float = 1.5
## The power line on a card this small: a bigger share of its height than on a mode card, with a thinner outline.
const POWER_SHARE: float = 0.105
const POWER_OUTLINE_PX: int = 4
## A 1 px see-through picture: a card with no art (the back of a card) is printed on its ground alone.
static var _blank: ImageTexture

## The weapon this card shows (null: a card back, or nothing yet).
var weapon: WeaponDef
## The card shows its back.
var back: bool = false


func _init() -> void:
	super()
	name = "HandCard"
	# a card is placed by `place()`, never stretched with its parent
	set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
	shadowed = false
	power_size_share = POWER_SHARE
	power_outline = POWER_OUTLINE_PX
	visible = false


## Shows the card of `def` (nothing changes if it already does).
func show_weapon(def: WeaponDef) -> void:
	if def == weapon and not back:
		return
	weapon = def
	back = false
	var art: Texture2D = Icons.art(StringName("btn_%s" % String(def.id)))
	texture = art if art != null else _blank_texture()
	ground = CardStyle.ground(def.kind)
	power = def.card_power
	wear_seed = CardStyle.wear_seed(def.id)


## Shows the back of a card on its way to the slot `slot` (every slot's back wears differently).
func show_back(slot: int) -> void:
	if back and weapon == null:
		return
	weapon = null
	back = true
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
	art_inset = 0.0 if back else rect.size.x * CardStyle.ART_EDGE_SHARE


## The card's own outline (in its own pixels): the corners the shader cuts, for the washes and the gold edge drawn over it.
func outline() -> PackedVector2Array:
	return CardStyle.outline(size, corner_radii())


static func _blank_texture() -> ImageTexture:
	if _blank == null:
		var image: Image = Image.create(1, 1, false, Image.FORMAT_RGBA8)
		image.fill(Color(0.0, 0.0, 0.0, 0.0))
		_blank = ImageTexture.create_from_image(image)
	return _blank
