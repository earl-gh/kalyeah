class_name CharactersPage
extends VBoxContainer
## The Characters tab: the six kids as 3:4 cards, to look at and read about. Tapping one asks for its
## detail sheet (`detail`); it never changes who you are: the kid you play is picked in the room and
## again at the start of every match.

signal character_opened(character: CharacterDef)

## Two columns of large faces: this tab is a gallery to look at, not a row of small buttons.
const COLUMNS: int = 2
const CARD: Vector2 = Vector2(325.0, 440.0)
const NOTE: String = "Only the look is yours to pick. Every kid has the same health, speed and size."

var cards: Dictionary[String, Button] = {}


func _ready() -> void:
	name = "CharactersPage"
	add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	add_child(UiKit.page_header("Characters", "Meet the six kids. Tap one to read about them."))
	var scroll: ScrollContainer = ScrollContainer.new()
	scroll.name = "Scroll"
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	add_child(scroll)
	var grid: GridContainer = GridContainer.new()
	grid.columns = COLUMNS
	grid.add_theme_constant_override("h_separation", 14)
	grid.add_theme_constant_override("v_separation", 14)
	grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(grid)
	for id: String in Session.AVATARS:
		var card: Button = _card(id)
		grid.add_child(card)
		cards[id] = card


func _card(id: String) -> Button:
	var definition: CharacterDef = load("res://data/characters/%s.tres" % id) as CharacterDef
	var card: Button = Button.new()
	card.name = id.capitalize()
	card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	card.custom_minimum_size = Vector2(0.0, CARD.y)
	card.tooltip_text = "About %s" % id.capitalize()
	for state: String in ["normal", "hover"]:
		card.add_theme_stylebox_override(state, UiSkin.plate("glass", 0.0, 22))
	for state: String in ["pressed", "hover_pressed"]:
		card.add_theme_stylebox_override(state, UiSkin.plate("raised", 0.0, 22))
	card.add_theme_stylebox_override("focus", KalyeahTheme.focus_ring(24))
	MenuDialog.feel_press(card)
	var picture: TextureRect = TextureRect.new()
	picture.name = "Picture"
	picture.texture = KidModel.card_face(StringName(id))
	if picture.texture == null:
		picture.texture = KidModel.portrait(StringName(id))
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	picture.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var crop: ShaderMaterial = ShaderMaterial.new()
	crop.shader = UiKit.CROP
	crop.set_shader_parameter("rect_size", CARD)
	crop.set_shader_parameter("radius", 22.0)
	picture.material = crop
	card.add_child(picture)
	# a dark fade under the name so it reads on any picture
	var fade: TextureRect = TextureRect.new()
	fade.texture = _fade()
	fade.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	fade.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	fade.offset_top = -120.0
	fade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(fade)
	var label: Label = Label.new()
	label.name = "Name"
	label.theme_type_variation = &"DisplayLabel"
	label.text = (definition.display_name if definition != null else id.capitalize()).to_upper()
	label.add_theme_font_size_override("font_size", 28)
	label.add_theme_constant_override("outline_size", 6)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	label.offset_top = -52.0
	label.offset_bottom = -10.0
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(label)
	card.pressed.connect(_open.bind(id))
	return card


static func _fade() -> GradientTexture2D:
	var texture: GradientTexture2D = GradientTexture2D.new()
	texture.width = 4
	texture.height = 64
	texture.fill_from = Vector2(0.0, 0.0)
	texture.fill_to = Vector2(0.0, 1.0)
	var gradient: Gradient = Gradient.new()
	gradient.offsets = PackedFloat32Array([0.0, 1.0])
	gradient.colors = PackedColorArray([Color(0.02, 0.04, 0.12, 0.0), Color(0.02, 0.04, 0.12, 0.9)])
	texture.gradient = gradient
	return texture


func _open(id: String) -> void:
	var definition: CharacterDef = load("res://data/characters/%s.tres" % id) as CharacterDef
	if definition != null:
		character_opened.emit(definition)


## The detail sheet of `character`: the face large, the name, what they wear, and a note that the look
## changes nothing about how they play.
static func detail(character: CharacterDef) -> MenuDialog:
	var card: MenuDialog = MenuDialog.create(character.display_name, 600.0)
	card.name = "CharacterDetail"
	var picture: TextureRect = TextureRect.new()
	picture.name = "Picture"
	var id: StringName = character.id
	picture.texture = KidModel.card_face(id)
	if picture.texture == null:
		picture.texture = KidModel.portrait(id)
	picture.custom_minimum_size = Vector2(240.0, 320.0)
	picture.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	var crop: ShaderMaterial = ShaderMaterial.new()
	crop.shader = UiKit.CROP
	crop.set_shader_parameter("rect_size", Vector2(240.0, 320.0))
	crop.set_shader_parameter("radius", 26.0)
	picture.material = crop
	card.body.add_child(picture)
	var about: Label = card.add_label(character.description, UiMetrics.BODY, KalyeahTheme.TEXT, HORIZONTAL_ALIGNMENT_CENTER)
	about.name = "About"
	card.add_hint(NOTE).horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	var close: Button = MenuDialog.make_button("Close", "grey", UiMetrics.ACTION)
	close.name = "Close"
	close.pressed.connect(card.close)
	card.add_buttons([close])
	return card
