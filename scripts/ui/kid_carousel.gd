class_name KidCarousel
extends MenuCarousel
## The kid pick at the start of a match: the six character card pictures (`CharactersPage.card_picture`) as an endless carousel, the chosen
## kid's card big in the middle, bare (the picture is the whole card, no plate behind it), and the others smaller, darker and behind it,
## like the main screen's mode carousel. One swipe is one step. A kid a teammate has taken stays in the ring with a "Taken" tag and is
## stepped over by a swipe; a tap on it is refused. A kid with no picture keeps the glass plate with the face, like the Characters tab.

## The card pictures are all this shape (a test holds it): the cell is as wide to tall as the Characters tab's.
const RATIO: float = CharactersPage.CARD.x / CharactersPage.CARD.y
## The cards behind the chosen one are this share of its size (the same as the main screen's mode models).
const BACK_SCALE: float = TitleScreen.MODEL_BACK_SCALE
## How far the neighbours' middles sit from the chosen card's, as a share of its width.
const SIDE_SPREAD: float = 0.4
## The room kept above and below the chosen card for its bob.
const AIR: float = 16.0
## A taken kid's card is this dark, and says so in words (colour is never the only signal).
const TAKEN_DIM: Color = Color(0.45, 0.45, 0.5)
const TAKEN_WORD: String = "Taken"
const TAG_SIZE: Vector2 = Vector2(128.0, 56.0)
## A neighbour's middle is hidden behind the chosen card; only its outer edge shows. The tag sits that far out from a card's middle (as a
## share of its width) when the card is a neighbour, and in the middle when it is the chosen one.
const TAG_OUT: float = 0.3

var ids: Array[StringName] = []
var _names: Dictionary[StringName, String] = {}


func _init() -> void:
	name = "KidCarousel"
	wrap = true
	show_rim = false
	scale_loss = 1.0 - BACK_SCALE
	bob = TitleScreen.AMBIENT_BOB
	laid_out.connect(_place_tags)


## Builds a card for each of `characters` and puts `current` in the middle (no slide).
func setup_kids(characters: Array[CharacterDef], current: StringName) -> void:
	ids.clear()
	_names.clear()
	var made: Array[Button] = []
	for character: CharacterDef in characters:
		ids.append(character.id)
		_names[character.id] = character.display_name
		made.append(kid_card(character, card_size))
	accessibility_name = "Kid"
	setup_cards(made, maxi(ids.find(current), 0))
	set_taken([] as Array[StringName])


## The card of one kid: the owner's card picture as it is, or the plate with the face when there is none, and a tag over it that shows
## only while the kid is taken.
static func kid_card(character: CharacterDef, size: Vector2) -> Button:
	var card: Button = Button.new()
	card.name = "Kid_" + String(character.id)
	card.accessibility_name = character.display_name
	for state: String in ["normal", "hover", "pressed", "hover_pressed", "disabled", "focus"]:
		card.add_theme_stylebox_override(state, StyleBoxEmpty.new())
	var art: Texture2D = CharactersPage.card_picture(character.id)
	if art != null:
		var picture: TextureRect = TextureRect.new()
		picture.name = "Picture"
		picture.texture = art
		picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		picture.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
		card.add_child(picture)
	else:
		CharactersPage.plate_picture(card, String(character.id), size)
	var tag: Label = Label.new()
	tag.name = "Taken"
	tag.text = TAKEN_WORD
	tag.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	tag.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	tag.add_theme_stylebox_override("normal", LoadoutCards.box(KalyeahTheme.INK_DEEP, KalyeahTheme.SLATE, 3))
	UiKit.letter(tag, UiMetrics.ACTION, KalyeahTheme.CREAM)
	tag.size = TAG_SIZE
	tag.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tag.visible = false
	card.add_child(tag)
	return card


## The kids a teammate has taken: their cards go dark and say "Taken", and the carousel will not settle on them.
func set_taken(taken: Array[StringName]) -> void:
	blocked.clear()
	for i: int in cards.size():
		var is_taken: bool = taken.has(ids[i])
		if is_taken:
			blocked.append(i)
		var card: Button = cards[i]
		(card.get_node("Taken") as Label).visible = is_taken
		var picture: Control = card.get_node_or_null("Picture") as Control
		if picture != null:
			picture.modulate = TAKEN_DIM if is_taken else Color.WHITE
		card.accessibility_name = "%s, %s" % [_names[ids[i]], TAKEN_WORD.to_lower()] if is_taken else _names[ids[i]]
	_place_tags()


## Keeps each tag on the part of its card that shows: in the middle on the chosen card, near the outer edge on a neighbour (the chosen card
## covers a neighbour's middle). Follows the card through a slide.
func _place_tags() -> void:
	for card: Button in cards:
		var tag: Label = card.get_node_or_null("Taken") as Label
		if tag == null or not tag.visible:
			continue
		var out: float = clampf((card.position.x + card_size.x / 2.0 - size.x / 2.0) / (card_size.x * SIDE_SPREAD), -1.0, 1.0)
		tag.position = Vector2(card_size.x / 2.0 + out * card_size.x * TAG_OUT, card_size.y / 2.0) - TAG_SIZE / 2.0


func is_blocked(id: StringName) -> bool:
	return blocked.has(ids.find(id))


## The chosen kid's id ("" with no cards).
func current_id() -> StringName:
	return ids[index] if index < ids.size() else &""


## Moves the middle to kid `id`; without a slide when `animate` is false.
func show_kid(id: StringName, animate: bool = true) -> void:
	var at: int = ids.find(id)
	if at >= 0:
		select(at, animate)


## Fits the cards to a stage of `room` pixels: as big as fits with both neighbours inside the width, and the room above and below for the
## bob. Returns the height the carousel needs.
func fit(room: Vector2) -> float:
	var across: float = room.x / (2.0 * (SIDE_SPREAD + BACK_SCALE / 2.0))
	var width: float = minf(across, maxf(room.y - 2.0 * AIR, 0.0) * RATIO)
	var card: Vector2 = Vector2(width, width / RATIO)
	spread_scale = card.x * SIDE_SPREAD / MenuCarousel.STEP
	resize_cards(card)
	for kid: Button in cards:
		var plate: Control = kid.get_node_or_null("Picture") as Control
		if plate != null and plate.material is ShaderMaterial:
			(plate.material as ShaderMaterial).set_shader_parameter("rect_size", card)
	return card.y + 2.0 * AIR
