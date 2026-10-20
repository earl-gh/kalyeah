class_name SettingsPage
extends ScrollContainer
## The Settings page: who you are (face and name, with a way to rename), sound, vibration (phones)
## and comfort, then quitting. There is no server address here: the online server is built into the
## game (the player only needs a room code).

signal profile_pressed
signal quit_pressed
signal gallery_pressed
signal howto_pressed

var profile_button: Button
var gallery_button: Button
var howto_button: Button
var quit_button: Button
var name_label: Label
var face: TextureRect
var _content: VBoxContainer


func _ready() -> void:
	name = "SettingsPage"
	horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	follow_focus = true
	_content = VBoxContainer.new()
	_content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_content.add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	add_child(_content)
	_content.add_child(UiKit.page_header("Settings", "Make Kalyeah yours."))
	_content.add_child(_profile_card())
	var preferences: VBoxContainer = VBoxContainer.new()
	preferences.add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	preferences.add_child(UiKit.section_label("Sound and feel"))
	var rows: VBoxContainer = VBoxContainer.new()
	rows.add_theme_constant_override("separation", UiMetrics.GAP)
	fill(rows)
	preferences.add_child(UiKit.card(rows))
	_content.add_child(preferences)
	howto_button = MenuDialog.make_button("How to play", "grey", UiMetrics.ACTION, Vector2(0.0, UiMetrics.TOUCH))
	howto_button.name = "HowToButton"
	howto_button.pressed.connect(howto_pressed.emit)
	_content.add_child(howto_button)
	gallery_button = MenuDialog.make_button("UI gallery", "grey", UiMetrics.ACTION, Vector2(0.0, UiMetrics.TOUCH))
	gallery_button.name = "GalleryButton"
	gallery_button.pressed.connect(gallery_pressed.emit)
	_content.add_child(gallery_button)
	quit_button = MenuDialog.make_button("Quit game", "red", UiMetrics.ACTION, Vector2(0.0, UiMetrics.TOUCH))
	quit_button.name = "QuitButton"
	quit_button.pressed.connect(quit_pressed.emit)
	_content.add_child(quit_button)
	visibility_changed.connect(refresh)


## The profile card: your face, your name and the button that changes it.
func _profile_card() -> Control:
	var row: HBoxContainer = HBoxContainer.new()
	row.add_theme_constant_override("separation", UiMetrics.GAP)
	face = UiKit.avatar(Session.avatar_id, 104.0)
	row.add_child(face)
	var words: VBoxContainer = VBoxContainer.new()
	words.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	words.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	words.add_theme_constant_override("separation", 6)
	name_label = Label.new()
	name_label.name = "PlayerName"
	name_label.theme_type_variation = &"DisplayLabel"
	name_label.add_theme_font_size_override("font_size", 40)
	name_label.add_theme_constant_override("outline_size", 6)
	name_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	name_label.text = Session.player_name
	words.add_child(name_label)
	profile_button = MenuDialog.make_button("Edit profile", "grey", UiMetrics.DETAIL, Vector2(0.0, 64.0))
	profile_button.name = "ProfileButton"
	profile_button.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	profile_button.pressed.connect(profile_pressed.emit)
	words.add_child(profile_button)
	row.add_child(words)
	return UiKit.card(row)


## Shows the name and face you have now (after a rename or a new profile picture).
func refresh() -> void:
	if name_label == null:
		return
	name_label.text = Session.player_name
	var picture: Texture2D = KidModel.minimap_face(StringName(Session.avatar_id))
	face.texture = picture if picture != null else KidModel.portrait(StringName(Session.avatar_id))


## Sound, vibration (phones only) and comfort, added to `parent` as setting rows.
static func fill(parent: Control) -> void:
	var sound: VBoxContainer = VBoxContainer.new()
	sound.add_theme_constant_override("separation", 0)
	var percent: Label = UiKit.text("%d%%" % roundi(Settings.volume * 100.0), UiMetrics.BODY, Color("ffd25a"), false)
	percent.name = "Percent"
	percent.custom_minimum_size = Vector2(96.0, 0.0)
	percent.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	sound.add_child(UiKit.setting_row("Sound", "", percent))
	var slider: HSlider = DialogButtons.slider(Settings.volume)
	slider.name = "VolumeSlider"
	sound.add_child(slider)
	slider.value_changed.connect(func(value: float) -> void:
		Settings.set_volume(value)
		percent.text = "%d%%" % roundi(value * 100.0))
	slider.drag_ended.connect(func(_changed: bool) -> void: Settings.play_ui())
	parent.add_child(sound)
	parent.add_child(UiKit.divider())
	if Settings.can_vibrate():
		var toggle: UiToggle = UiToggle.new()
		toggle.name = "VibrationToggle"
		toggle.set_state(Settings.vibration)
		toggle.pressed.connect(func() -> void: Settings.set_vibration(toggle.button_pressed))
		parent.add_child(UiKit.setting_row("Vibration", "Buzz when you press a button", toggle))
		parent.add_child(UiKit.divider())
	parent.add_child(AccessibilityOptions.new())
