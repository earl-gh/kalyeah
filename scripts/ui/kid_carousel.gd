class_name KidCarousel
extends MenuCarousel
## The kid pick at the start of a match: the six character card pictures (`CharactersPage.card_picture`) as an endless carousel, five cards
## in view at a time. The chosen kid's card is bare (the picture is the whole card, no plate behind it) and biggest in the middle; the two
## kids on each side step down in size and brightness and tuck a little behind the one before; the sixth waits out of sight. One swipe is
## one step. A kid a teammate has taken stays in the ring with a "Taken" tag and is stepped over by a swipe; a tap on it is refused. A kid
## with no picture keeps the glass plate with the face, like the Characters tab. The kid's name and a short description go under the
## carousel (`WeaponPickScreen`), not on the cards.

## The card pictures are all this shape (a test holds it): the cell is as wide to tall as the Characters tab's.
const RATIO: float = CharactersPage.CARD.x / CharactersPage.CARD.y
## Cards on each side of the chosen one, so five are in view at once.
const SIDE: int = 2
const VISIBLE: int = 1 + 2 * SIDE
## By how many places from the middle: the size of a card (1 = the chosen one). Every card behind the chosen one is dimmed the way the
## mode models on the Play tab are (`MenuCarousel.SHADE_LOSS` darker and `MenuCarousel.BACK_ALPHA` see-through), one dim for all of them.
const LEVEL_SCALE: Array[float] = [1.0, 0.8, 0.62]
## The middles of two neighbours sit this share of the way between touching and stacked (below 1 they tuck behind each other a little).
const OVERLAP: float = 0.82
## The room kept above and below the chosen card for its bob.
const AIR: float = 16.0
## A taken kid's card is this dark, and says so in words (colour is never the only signal).
const TAKEN_DIM: Color = Color(0.45, 0.45, 0.5)
const TAKEN_WORD: String = "Taken"
## The tag is this share of its card wide and this tall in the card's own pixels, in the middle of the card.
const TAG_WIDTH: float = 0.84
const TAG_HEIGHT: float = 44.0

var ids: Array[StringName] = []
var _names: Dictionary[StringName, String] = {}


func _init() -> void:
	name = "KidCarousel"
	wrap = true
	show_rim = false
	bob = TitleScreen.AMBIENT_BOB


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
	UiKit.letter(tag, UiMetrics.CAPTION, KalyeahTheme.CREAM)
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


## Puts each tag in the middle of its card, sized to it.
func _place_tags() -> void:
	for card: Button in cards:
		var tag: Label = card.get_node_or_null("Taken") as Label
		if tag == null:
			continue
		tag.size = Vector2(card_size.x * TAG_WIDTH, TAG_HEIGHT)
		tag.position = (card_size - tag.size) / 2.0


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


## How far (in card widths) the middle of a card `places` from the chosen one sits from it: the places in between are interpolated, and
## past the outermost card the position stays (the sixth kid tucks away behind the fifth).
static func spread(places: float) -> float:
	var first: float = OVERLAP * (LEVEL_SCALE[0] + LEVEL_SCALE[1]) / 2.0
	var second: float = first + OVERLAP * (LEVEL_SCALE[1] + LEVEL_SCALE[2]) / 2.0
	var at: float = minf(places, float(SIDE))
	return lerpf(0.0, first, minf(at, 1.0)) + lerpf(0.0, second - first, maxf(at - 1.0, 0.0))


## Half the width the five cards take, in widths of the chosen card.
static func half_span() -> float:
	return spread(float(SIDE)) + LEVEL_SCALE[2] / 2.0


## A value of `levels` at `places` from the middle, interpolated between the whole places.
static func level(levels: Array[float], places: float) -> float:
	var at: float = clampf(places, 0.0, float(levels.size() - 1))
	var low: int = int(at)
	return lerpf(levels[low], levels[mini(low + 1, levels.size() - 1)], at - float(low))


## Places every card from the scroll position: the nearer the middle, the bigger, the brighter and the further in front. The cards two
## places out are the last in view; the one three places out fades away.
func _layout() -> void:
	var drift_phase: float = sin(float(Time.get_ticks_msec()) / 1000.0 * FLOAT_SPEED)
	for i: int in cards.size():
		var offset: float = float(i) - _position
		if wrap:
			var half: float = float(cards.size()) / 2.0
			offset = fposmod(offset + half, float(cards.size())) - half
		var places: float = absf(offset)
		var card: Button = cards[i]
		var away: float = minf(places, 1.0)
		var drift: float = drift_phase * bob * (1.0 - away) if bob > 0.0 and not Settings.reduced_motion else 0.0
		card.scale = Vector2.ONE * level(LEVEL_SCALE, places)
		card.position = Vector2(size.x / 2.0 + signf(offset) * spread(places) * card_size.x - card_size.x / 2.0, (size.y - card_size.y) / 2.0 + drift)
		var shade: float = 1.0 - MenuCarousel.SHADE_LOSS * away
		var fade: float = clampf(float(SIDE + 1) - places, 0.0, 1.0)
		card.modulate = Color(shade, shade, shade, fade * lerpf(1.0, MenuCarousel.BACK_ALPHA, away))
		card.visible = fade > 0.0
		card.z_index = 10 - int(roundf(minf(places, 3.0) * 3.0))
		_float(card, away, drift_phase)
	laid_out.emit()


## Fits the cards to a stage of `room` pixels: as big as fits with all five inside the width, and the room above and below for the bob.
## Returns the height the carousel needs.
func fit(room: Vector2) -> float:
	var across: float = room.x / (2.0 * half_span())
	var width: float = minf(across, maxf(room.y - 2.0 * AIR, 0.0) * RATIO)
	var card: Vector2 = Vector2(width, width / RATIO)
	spread_scale = spread(1.0) * card.x / MenuCarousel.STEP
	resize_cards(card)
	_place_tags()
	for kid: Button in cards:
		var plate: Control = kid.get_node_or_null("Picture") as Control
		if plate != null and plate.material is ShaderMaterial:
			(plate.material as ShaderMaterial).set_shader_parameter("rect_size", card)
	return card.y + 2.0 * AIR
