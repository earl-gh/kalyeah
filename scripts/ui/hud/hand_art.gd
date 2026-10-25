class_name HandArt
extends RefCounted
## The owner's pictures for the hand in a match (`assets/ui/hand/`, unedited): the cardboard plate behind the cards, the ice water bag
## that stands for ice water everywhere (a card's cost, the bar's number) and the speech bubble of the quick chat button. Each file has clear
## margins, so a part rect gives the visible art as shares of the file (an atlas, so it holds at any import size).

const PLATE: Texture2D = preload("res://assets/ui/hand/hand_plate.webp")
const ICE: Texture2D = preload("res://assets/ui/hand/ice_water.webp")
const CHAT: Texture2D = preload("res://assets/ui/hand/chat.webp")
## Only the plate's flat face and its top and left rims: its thick right and bottom sides are cut off, so it runs off the screen's right and
## bottom edges looking flat (owner). The full art is x 215 to 1435 and y 307 to 821 of the file; this is x 215 to 1378 and y 307 to 765.
const PLATE_PART: Rect2 = Rect2(0.14848, 0.28269, 0.80318, 0.4217)
const ICE_PART: Rect2 = Rect2(0.23604, 0.10287, 0.52951, 0.86124)
const CHAT_PART: Rect2 = Rect2(0.11418, 0.14161, 0.77018, 0.73427)
## Width over height of the visible art.
const ICE_ASPECT: float = 0.6148
const CHAT_ASPECT: float = 1.2607
## The bag's body is its middle: the number sits this far down from its top (a share of its height).
const ICE_NUMBER_AT: float = 0.54
## A bag the bar has not reached is drawn in this colour.
const ICE_DIM: Color = Color(0.55, 0.57, 0.62)

static var _plate: AtlasTexture
static var _ice: AtlasTexture
static var _chat: AtlasTexture


static func plate() -> AtlasTexture:
	if _plate == null:
		_plate = _part(PLATE, PLATE_PART)
	return _plate


static func ice() -> AtlasTexture:
	if _ice == null:
		_ice = _part(ICE, ICE_PART)
	return _ice


static func chat() -> AtlasTexture:
	if _chat == null:
		_chat = _part(CHAT, CHAT_PART)
	return _chat


## The speech bubble drawn smaller inside its button: `pad` is the empty space round the art as a share of its size, on each side.
static func chat_padded(pad: float) -> AtlasTexture:
	var atlas: AtlasTexture = _part(CHAT, CHAT_PART)
	var margin: Vector2 = atlas.region.size * pad
	atlas.margin = Rect2(margin, margin * 2.0)
	return atlas


static func _part(texture: Texture2D, part: Rect2) -> AtlasTexture:
	var atlas: AtlasTexture = AtlasTexture.new()
	atlas.atlas = texture
	var size: Vector2 = texture.get_size()
	atlas.region = Rect2(part.position * size, part.size * size)
	atlas.filter_clip = true
	return atlas


## An ice water bag `height` tall centred on `centre`, with `text` (a cost, or the bar's whole points) lettered on its body at `font_size`.
## `affordable` false greys it (a cost the bar has not reached yet).
static func draw_ice(canvas: CanvasItem, centre: Vector2, height: float, text: String, font_size: int, affordable: bool = true) -> void:
	var rect: Rect2 = Rect2(centre - Vector2(height * ICE_ASPECT, height) / 2.0, Vector2(height * ICE_ASPECT, height))
	canvas.draw_texture_rect(ice(), rect, false, Color.WHITE if affordable else ICE_DIM)
	if text.is_empty():
		return
	var font: Font = KalyeahTheme.ACTION_FONT
	var width: float = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size).x
	RoyaleStyle.lettered(canvas, font, text, Vector2(centre.x - width / 2.0, rect.position.y + height * ICE_NUMBER_AT + font_size * 0.36), font_size)
