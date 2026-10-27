class_name SkillCardArt
extends Control
## What a skill looks like on a menu screen (the Weapons tab of the Book, its detail sheet, the weapon pick / swap screen and its slots): the
## owner's full-card picture (`CardStyle.picture`, `assets/ui/skill_cards/<id>.webp`) drawn BARE: it already is a card with its own ragged
## frame, so there is no holder, plate, crop or rounded mask behind it. It keeps its own proportions (`CARD_ASPECT` wide over tall), fitted
## and centred in the control. A weapon without a picture keeps its painted round icon (`Icons.skill`), the fallback for a future weapon.
##
## `selected` draws a gold edge that follows the picture's own silhouette (the hand's lift cue: `silhouette.gdshader`, behind the picture),
## so a picked card reads as picked without a box around it. The edge is `EDGE_WIDTH` wide and is drawn outside the control's rect, so
## whoever places this leaves that much room.

## The proportions of the owner's card pictures (512 x 677 px): wide over tall.
const CARD_ASPECT: float = 0.756
const EDGE_WIDTH: float = CardStyle.LIFT_EDGE
const EDGE_STEPS: int = CardStyle.PICTURE_EDGE_STEPS

var weapon_id: StringName = &""
var selected: bool = false
var picture: TextureRect
var _edge: Control


## The size of a card picture `height` tall.
static func size_for_height(height: float) -> Vector2:
	return Vector2(roundf(height * CARD_ASPECT), height)


func _init() -> void:
	name = "Art"
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	# a big picture drawn small: its mipmaps keep it smooth (the same as the hand's cards)
	texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	_edge = Control.new()
	_edge.name = "Edge"
	_edge.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_edge.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_edge.material = HandCard.silhouette_material()
	_edge.draw.connect(_draw_edge)
	add_child(_edge)
	picture = TextureRect.new()
	picture.name = "Picture"
	picture.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(picture)
	resized.connect(_edge.queue_redraw)


## Shows the skill of the weapon `id`: its card picture, else its painted icon (else nothing, for an empty slot).
func show_weapon(id: StringName) -> void:
	weapon_id = id
	var texture: Texture2D = null
	if id != &"":
		texture = CardStyle.picture(id)
		if texture == null:
			texture = Icons.skill(id)
	picture.texture = texture
	_edge.queue_redraw()


## True when the weapon's card picture is what is shown (not the painted icon fallback).
func is_card() -> bool:
	return picture.texture != null and picture.texture == CardStyle.picture(weapon_id)


## Where the picture lies inside this control (its own pixels): as big as fits with its own proportions, centred.
func shown_rect() -> Rect2:
	return CardStyle.picture_rect(Rect2(Vector2.ZERO, size), picture.texture)


func set_selected(on: bool) -> void:
	if on == selected:
		return
	selected = on
	_edge.queue_redraw()


## Follows a toggle button: the card is selected whenever the button is pressed in (it is checked when the button redraws, which
## `set_pressed_no_signal` also causes).
func follow(button: BaseButton) -> void:
	button.draw.connect(_follow_once.bind(button))
	set_selected(button.button_pressed)


func _follow_once(button: BaseButton) -> void:
	set_selected(button.button_pressed)


## The gold edge: the picture's silhouette in gold at `EDGE_STEPS` offsets round it, behind the picture.
func _draw_edge() -> void:
	if not selected or picture.texture == null or size.x < 8.0:
		return
	var shown: Rect2 = shown_rect()
	for step: int in EDGE_STEPS:
		var at: Vector2 = Vector2.from_angle(TAU * float(step) / float(EDGE_STEPS)) * EDGE_WIDTH
		_edge.draw_texture_rect(picture.texture, Rect2(shown.position + at, shown.size), false, RoyaleStyle.GOLD)
