class_name UiProfileChip
extends Button
## Who you are, at the top of the main screen: your face (the kid you picked), your name, and a
## hint that tapping it changes the name. A rounded night-glass chip, one tap target.

const FACE: float = 72.0
const CROP: Shader = preload("res://shaders/minimap_crop.gdshader")

var face: TextureRect
var name_label: Label
var hint_label: Label


func _init() -> void:
	name = "ProfileChip"
	focus_mode = Control.FOCUS_ALL
	custom_minimum_size = Vector2(0.0, UiMetrics.TOUCH)
	for state: String in ["normal", "hover", "pressed", "hover_pressed"]:
		add_theme_stylebox_override(state, UiSkin.plate("night", 10.0, 34))
	add_theme_stylebox_override("focus", UiSkin.focus_ring(40))
	MenuDialog.feel_press(self)
	var row: HBoxContainer = HBoxContainer.new()
	row.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	row.offset_left = 10.0
	row.offset_right = -22.0
	row.add_theme_constant_override("separation", 14)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(row)
	face = TextureRect.new()
	face.name = "Face"
	face.custom_minimum_size = Vector2(FACE, FACE)
	face.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	face.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	face.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	face.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var crop: ShaderMaterial = ShaderMaterial.new()
	crop.shader = CROP
	crop.set_shader_parameter("rect_size", Vector2(FACE, FACE))
	crop.set_shader_parameter("radius", FACE / 2.0)
	face.material = crop
	row.add_child(face)
	var text: VBoxContainer = VBoxContainer.new()
	text.alignment = BoxContainer.ALIGNMENT_CENTER
	text.add_theme_constant_override("separation", -2)
	text.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_child(text)
	name_label = Label.new()
	name_label.name = "Name"
	name_label.add_theme_font_override("font", KalyeahTheme.BUTTON_FONT)
	name_label.add_theme_font_size_override("font_size", UiMetrics.ACTION)
	name_label.add_theme_color_override("font_color", KalyeahTheme.TEXT)
	name_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	name_label.custom_minimum_size = Vector2(150.0, 0.0)
	name_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	text.add_child(name_label)
	hint_label = Label.new()
	hint_label.name = "Hint"
	hint_label.text = "Edit profile"
	hint_label.add_theme_font_size_override("font_size", UiMetrics.MIN_TEXT)
	hint_label.add_theme_color_override("font_color", KalyeahTheme.TEXT_MUTED)
	hint_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	text.add_child(hint_label)
	custom_minimum_size.x = FACE + 14.0 + 190.0 + 40.0
	refresh()


## Shows the name and the face you have now.
func refresh() -> void:
	name_label.text = Session.player_name
	accessibility_name = "Your profile: %s" % Session.player_name
	accessibility_description = "Opens the name and face editor"
	var picture: Texture2D = KidModel.minimap_face(StringName(Session.avatar_id))
	face.texture = picture if picture != null else KidModel.portrait(StringName(Session.avatar_id))
