class_name CharactersPage
extends VBoxContainer
## The Characters tab: the six kids as 3:4 cards. Tap one to make it your face on the main screen.

signal chosen(character_id: String)

var cards: Dictionary[String, Button] = {}


func _ready() -> void:
	name = "CharactersPage"
	add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	var title: Label = Label.new()
	title.theme_type_variation = &"DisplayLabel"
	title.text = "PROFILE PICTURE"
	title.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	title.add_theme_font_size_override("font_size", 38)
	add_child(title)
	var hint: Label = Label.new()
	hint.theme_type_variation = &"MutedLabel"
	hint.text = "Match characters are chosen before play."
	hint.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	hint.add_theme_font_size_override("font_size", UiMetrics.DETAIL)
	add_child(hint)
	var grid: GridContainer = GridContainer.new()
	grid.columns = 3
	grid.add_theme_constant_override("h_separation", 14)
	grid.add_theme_constant_override("v_separation", 14)
	grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	add_child(grid)
	for id: String in Session.AVATARS:
		var card: Button = _card(id)
		grid.add_child(card)
		cards[id] = card
	refresh()


func _card(id: String) -> Button:
	var definition: CharacterDef = load("res://data/characters/%s.tres" % id) as CharacterDef
	var card: Button = Button.new()
	card.name = id.capitalize()
	card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	card.custom_minimum_size = Vector2(0.0, 300.0)
	card.tooltip_text = "Use %s as profile picture" % id.capitalize()
	MenuDialog.feel_press(card)
	var picture: TextureRect = TextureRect.new()
	picture.texture = KidModel.card_face(StringName(id))
	if picture.texture == null:
		picture.texture = KidModel.portrait(StringName(id))
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	picture.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	picture.offset_bottom = -42.0
	picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(picture)
	var label: Label = Label.new()
	label.set_meta(UiAccessibility.FIXED, true)
	label.theme_type_variation = &"DisplayLabel"
	label.text = (definition.display_name if definition != null else id).to_upper()
	label.add_theme_font_size_override("font_size", UiMetrics.CAPTION)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	label.offset_top = -40.0
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(label)
	card.pressed.connect(_pick.bind(id))
	return card


func _pick(id: String) -> void:
	Session.avatar_id = id
	Session.save()
	refresh()
	chosen.emit(id)


## Outlines the kid that is your face.
func refresh() -> void:
	for id: String in cards:
		var style: StyleBoxFlat = KalyeahTheme.painted_pill()
		var mine: bool = id == Session.avatar_id
		style.set_border_width_all(5 if mine else 2)
		style.border_color = KalyeahTheme.AMBER if mine else Color(1, 1, 1, 0.18)
		for state: String in ["normal", "hover", "pressed", "hover_pressed"]:
			cards[id].add_theme_stylebox_override(state, style)
		cards[id].add_theme_stylebox_override("focus", KalyeahTheme.focus_ring(24))
