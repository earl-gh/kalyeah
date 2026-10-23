class_name RoomCharacterCards
extends RefCounted
## Reusable room character cards; selection rules remain server-owned.


## One 3:4 profile card: the kid's picture, the name along the bottom.
static func build(view: HotspotRoomView, id: String) -> Button:
	var card: Button = Button.new()
	card.name = id.capitalize()
	card.custom_minimum_size = HotspotRoomView.KID_CARD
	card.tooltip_text = "Choose %s" % id.capitalize()
	card.accessibility_name = "Choose %s" % id.capitalize()
	MenuDialog.feel_press(card)
	card.pressed.connect(view.character_chosen.emit.bind(id))
	var picture: TextureRect = TextureRect.new()
	picture.texture = KidModel.card_face(StringName(id))
	if picture.texture == null:
		picture.texture = KidModel.portrait(StringName(id))
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	picture.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	picture.offset_bottom = -36.0
	picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	# the picture follows the card's rounded corners
	var crop: ShaderMaterial = ShaderMaterial.new()
	crop.shader = ChoiceCard.CROP_SHADER
	crop.set_shader_parameter("rect_size", HotspotRoomView.KID_CARD - Vector2(0.0, 36.0))
	crop.set_shader_parameter("radius", float(HotspotRoomView.KID_RADIUS))
	picture.material = crop
	card.add_child(picture)
	var definition: CharacterDef = load("res://data/characters/%s.tres" % id) as CharacterDef
	var label: Label = Label.new()
	label.text = definition.display_name if definition != null else id.capitalize()
	label.add_theme_font_size_override("font_size", UiMetrics.CAPTION)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	label.offset_top = -36.0
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(label)
	style(card, false)
	return card


static func style(card: Button, chosen: bool) -> void:
	# rounded corners and a soft drop shadow
	var box: StyleBoxFlat = KalyeahTheme.surface_box(HotspotRoomView.KID_RADIUS, 0, 0.0)
	box.bg_color = Color(KalyeahTheme.INK, 0.8)
	box.shadow_color = Color(0.0, 0.0, 0.0, 0.5)
	box.shadow_size = 4
	box.shadow_offset = Vector2(0.0, 2.0)
	for state: String in ["normal", "hover", "pressed", "disabled"]:
		card.add_theme_stylebox_override(state, box)
	card.add_theme_stylebox_override("focus", KalyeahTheme.focus_ring(HotspotRoomView.KID_RADIUS + 2))
	# the amber frame is an overlay (the picture covers the button's own border)
	var frame: Panel = card.get_node_or_null("Chosen") as Panel
	if frame == null:
		frame = Panel.new()
		frame.name = "Chosen"
		frame.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		frame.mouse_filter = Control.MOUSE_FILTER_IGNORE
		var ring: StyleBoxFlat = KalyeahTheme.surface_box(HotspotRoomView.KID_RADIUS, 5, 0.0)
		ring.draw_center = false
		ring.shadow_size = 0
		ring.border_color = KalyeahTheme.AMBER
		frame.add_theme_stylebox_override("panel", ring)
		card.add_child(frame)
	frame.visible = chosen
	var picture: Control = card.get_child(0) as Control
	picture.modulate = Color.WHITE if chosen else Color(0.78, 0.78, 0.82)
