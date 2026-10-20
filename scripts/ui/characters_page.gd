class_name CharactersPage
extends VBoxContainer
## The Characters tab: the six kids as 3:4 cards. Tap one to make it your face on the main screen.

signal chosen(character_id: String)

var cards: Dictionary[String, Button] = {}


func _ready() -> void:
	name = "CharactersPage"
	add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	add_child(UiKit.page_header("Characters", "Tap a kid to wear their face. You pick who you play before every match."))
	var scroll: ScrollContainer = ScrollContainer.new()
	scroll.name = "Scroll"
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	add_child(scroll)
	var grid: GridContainer = GridContainer.new()
	grid.columns = 3
	grid.add_theme_constant_override("h_separation", 14)
	grid.add_theme_constant_override("v_separation", 14)
	grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(grid)
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
	card.custom_minimum_size = Vector2(0.0, 330.0)
	card.tooltip_text = "Use %s as profile picture" % id.capitalize()
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
	crop.set_shader_parameter("rect_size", Vector2(206.0, 330.0))
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
	var mine: PanelContainer = UiKit.chip("You", Color("ffc233"), Color("3a1c00"), 20)
	mine.name = "Mine"
	mine.set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT, Control.PRESET_MODE_MINSIZE)
	mine.position = Vector2(10.0, 10.0)
	mine.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(mine)
	var frame: Panel = Panel.new()
	frame.name = "Frame"
	frame.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	frame.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(frame)
	card.pressed.connect(_pick.bind(id))
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


func _pick(id: String) -> void:
	Session.avatar_id = id
	Session.save()
	refresh()
	chosen.emit(id)


## Frames the kid that is your face in gold (and says "You"); the others keep a quiet edge.
func refresh() -> void:
	for id: String in cards:
		var mine: bool = id == Session.avatar_id
		var base: StyleBoxFlat = StyleBoxFlat.new()
		base.bg_color = Color("0a1433")
		base.set_corner_radius_all(22)
		base.shadow_color = Color(0.0, 0.0, 0.0, 0.45)
		base.shadow_size = 10
		base.shadow_offset = Vector2(0.0, 5.0)
		base.anti_aliasing = true
		for state: String in ["normal", "hover", "pressed", "hover_pressed", "focus"]:
			cards[id].add_theme_stylebox_override(state, base)
		cards[id].add_theme_stylebox_override("focus", KalyeahTheme.focus_ring(24))
		var ring: StyleBoxFlat = StyleBoxFlat.new()
		ring.draw_center = false
		ring.set_corner_radius_all(22)
		ring.set_border_width_all(7 if mine else 2)
		ring.border_color = Color("ffc233") if mine else Color(1.0, 1.0, 1.0, 0.18)
		ring.shadow_color = Color(Color("ffc233"), 0.5) if mine else Color.TRANSPARENT
		ring.shadow_size = 14 if mine else 0
		ring.anti_aliasing = true
		(cards[id].get_node("Frame") as Panel).add_theme_stylebox_override("panel", ring)
		(cards[id].get_node("Mine") as Control).visible = mine
