class_name HandArt
extends RefCounted
## The owner's pictures for the hand in a match (`assets/ui/hand/`, unedited): the ice water drop (a blue drop with a snowflake, trimmed to its art, lossless) that stands for ice water everywhere (a
## card's cost, the bar's number) and the speech bubble of the quick chat button (the tray behind the cards is drawn in code, `ConsolePanel`). Each file has clear
## margins, so a part rect gives the visible art as shares of the file (an atlas, so it holds at any import size).

const ICE: Texture2D = preload("res://assets/ui/hand/ice_water.png")
const CHAT: Texture2D = preload("res://assets/ui/hand/chat.webp")
const ICE_PART: Rect2 = Rect2(0.0, 0.0, 1.0, 1.0)
const CHAT_PART: Rect2 = Rect2(0.11418, 0.14161, 0.77018, 0.73427)
## Width over height of the visible art (the picture's own).
const ICE_ASPECT: float = 0.8734
## The drop is DRAWN a little narrower than its picture: at its own ratio it reads stretched sideways on the cards (owner), and the
## snowflake and the tip stay recognisable squeezed by this much. Everything that lays out round the drop uses this, not `ICE_ASPECT`.
const ICE_DRAW_ASPECT: float = 0.80
const CHAT_ASPECT: float = 1.2607
## The bag's body is its middle: the number sits this far down from its top (a share of its height).
const ICE_NUMBER_AT: float = 0.6
## The drop's tip leans to the right, so its body (and the snowflake) is a little right of its middle: the number moves this share of the width.
const ICE_NUMBER_SHIFT: float = 0.05
## A bag the bar has not reached is drawn in this colour.
const ICE_DIM: Color = Color(0.55, 0.57, 0.62)

static var _ice: AtlasTexture
static var _chat: AtlasTexture


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


## Where the drop of `height` is drawn when centred on `centre`.
static func ice_rect(centre: Vector2, height: float) -> Rect2:
	var size: Vector2 = Vector2(height * ICE_DRAW_ASPECT, height)
	return Rect2(centre - size / 2.0, size)


## An ice water bag `height` tall centred on `centre`, with `text` (a cost, or the bar's whole points) lettered on its body at `font_size`.
## `affordable` false greys it (a cost the bar has not reached yet).
static func draw_ice(canvas: CanvasItem, centre: Vector2, height: float, text: String, font_size: int, affordable: bool = true) -> void:
	var rect: Rect2 = ice_rect(centre, height)
	canvas.draw_texture_rect(ice(), rect, false, Color.WHITE if affordable else ICE_DIM)
	if text.is_empty():
		return
	var font: Font = KalyeahTheme.ACTION_FONT
	var width: float = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size).x
	RoyaleStyle.lettered(canvas, font, text, Vector2(centre.x - width / 2.0 + rect.size.x * ICE_NUMBER_SHIFT, rect.position.y + height * ICE_NUMBER_AT + font_size * 0.36), font_size)
